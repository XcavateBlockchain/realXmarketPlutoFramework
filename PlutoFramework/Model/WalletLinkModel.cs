using PlutoFramework.Model.Solana;
using PlutoFrameworkCore;
using PlutoFrameworkCore.PushNotificationServices.Core;
using PlutoFrameworkCore.PushNotificationServices.Core.Misc;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;
using System.Text;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Links the user's wallet addresses to this device on the notifications API, so
    /// notifications targeted at an address reach the device that holds its key.
    ///
    /// Solana links must carry an Ed25519 signature over the canonical link message;
    /// Polkadot links are recorded without ownership proof until the server implements
    /// sr25519 verification. Every entry point here is safe to fire and forget: failures
    /// are logged, never thrown, and a failed link is retried the next time the account
    /// passes through <see cref="PlutoFrameworkSolanaAccount.ResolveAsync"/> unlocked.
    /// </summary>
    public static class WalletLinkModel
    {
        /// <summary>
        /// Links the stored Polkadot address. No signature, no unlock, no prompt.
        /// </summary>
        public static async Task<bool> LinkPolkadotAsync()
        {
            try
            {
                if (!KeysModel.HasSubstrateKey())
                {
                    return false;
                }

                return await DeviceRegisterService.LinkWalletAsync(
                    WalletChain.Polkadot,
                    KeysModel.GetSubstrateKey());
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Polkadot wallet link failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Links a Solana address using the mnemonic phrase the caller already holds -
        /// the one moment signing needs no unlock prompt. Used at account creation and
        /// import time.
        /// </summary>
        public static async Task<bool> LinkSolanaMnemonicAsync(string address, string mnemonics)
        {
            try
            {
                var account = SolanaMnemonicsModel.GetAccountFromMnemonics(mnemonics);

                return await DeviceRegisterService.LinkWalletAsync(
                    WalletChain.Solana,
                    address,
                    message => Task.FromResult(
                        SolanaBase58.Encode(account.Sign(Encoding.UTF8.GetBytes(message)))));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Links a not-yet-linked Solana address using an account that is already
        /// unlocked and in memory, piggybacking on whatever action resolved it. This is
        /// how a link that failed at creation time (offline, device not yet registered)
        /// eventually lands without ever prompting the user.
        /// </summary>
        /// <remarks>
        /// Mobile Wallet Adapter accounts are skipped here: resolution happens right before
        /// the resolved account opens its own wallet session, and a concurrent link would
        /// collide with it. They link through <see cref="TryLinkSolanaMwaAsync"/> at
        /// moments when no session is open instead.
        /// </remarks>
        public static async Task TryLinkResolvedSolanaAccountAsync(PlutoFrameworkSolanaAccount account)
        {
            try
            {
                if (!account.CanSignLocally)
                {
                    return;
                }

                if (await DeviceRegisterService.IsWalletLinkedAsync(WalletChain.Solana, account.Address))
                {
                    return;
                }

                await DeviceRegisterService.LinkWalletAsync(
                    WalletChain.Solana,
                    account.Address,
                    async message => SolanaBase58.Encode(
                        await account.SignMessageAsync(
                            Encoding.UTF8.GetBytes(message),
                            "Enable notifications for your Solana wallet",
                            CancellationToken.None)));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link retry failed: {e.Message}");
            }
        }

        /// <summary>
        /// Guards against a second Mobile Wallet Adapter link starting while one is
        /// running. Without it, a failed link's own signature - which fires the
        /// after-signature retry below - would immediately launch the wallet again.
        /// </summary>
        private static int mwaLinkInProgress;

        /// <summary>
        /// Links a Mobile Wallet Adapter account's address. This launches the external
        /// wallet (behind the waiting popup), so it is only called from moments where a
        /// wallet trip is expected: right after a connect completes, and right after
        /// another signature session has closed.
        /// </summary>
        public static async Task TryLinkSolanaMwaAsync(PlutoFrameworkSolanaAccount account)
        {
            if (Interlocked.Exchange(ref mwaLinkInProgress, 1) == 1)
            {
                return;
            }

            try
            {
                if (await DeviceRegisterService.IsWalletLinkedAsync(WalletChain.Solana, account.Address))
                {
                    return;
                }

                await DeviceRegisterService.LinkWalletAsync(
                    WalletChain.Solana,
                    account.Address,
                    async message =>
                    {
                        try
                        {
                            return SolanaBase58.Encode(await account.SignMessageAsync(
                                Encoding.UTF8.GetBytes(message),
                                "Verify this wallet address to receive its notifications on this device",
                                CancellationToken.None));
                        }
                        catch (MwaAuthorizationException e)
                        {
                            // The user declined in the wallet. Reported as a cancellation
                            // so the retry loop stops instead of relaunching the wallet.
                            throw new OperationCanceledException(e.Message, e);
                        }
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link failed: {e.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref mwaLinkInProgress, 0);
            }
        }

        /// <summary>
        /// Links whatever Mobile Wallet Adapter account was just connected. Called when the
        /// connect flow completes - the user has just approved this app in their wallet, so
        /// one follow-up signature request is contextually clear rather than a surprise.
        /// </summary>
        public static async Task TryLinkSolanaMwaAfterConnectAsync()
        {
            try
            {
                var account = await PlutoFrameworkSolanaAccount.ResolveAsync(
                    "Enable notifications for your Solana wallet");

                // Mnemonic accounts were already linked silently when their key was saved.
                if (account is null || account.CanSignLocally)
                {
                    return;
                }

                await TryLinkSolanaMwaAsync(account);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link failed: {e.Message}");
            }
        }

        /// <summary>
        /// Unlinks every wallet this device linked. Used on logout/account clear, so the
        /// device stops receiving notifications for wallets it no longer holds.
        /// </summary>
        public static async Task UnlinkAllAsync()
        {
            try
            {
                await DeviceRegisterService.UnlinkAllWalletsAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Wallet unlink failed: {e.Message}");
            }
        }
    }
}
