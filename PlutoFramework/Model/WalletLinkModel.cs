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
    /// Only Solana wallets are linked, each carrying an Ed25519 signature over the
    /// canonical link message. Polkadot wallets are deliberately not registered: the
    /// server records their links without ownership proof until it implements sr25519
    /// verification. Every entry point here is safe to fire and forget: failures are
    /// logged, never thrown, and a failed link is retried the next time the account
    /// passes through <see cref="PlutoFrameworkSolanaAccount.ResolveAsync"/> unlocked.
    /// </summary>
    public static class WalletLinkModel
    {
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
        /// <param name="force">Relink even when this address is already recorded as linked.</param>
        public static async Task<bool> TryLinkResolvedSolanaAccountAsync(
            PlutoFrameworkSolanaAccount account,
            bool force = false)
        {
            try
            {
                if (!account.CanSignLocally)
                {
                    return false;
                }

                if (!force && await DeviceRegisterService.IsWalletLinkedAsync(WalletChain.Solana, account.Address))
                {
                    return true;
                }

                return await DeviceRegisterService.LinkWalletAsync(
                    WalletChain.Solana,
                    account.Address,
                    async message => SolanaBase58.Encode(
                        await account.SignMessageAsync(
                            Encoding.UTF8.GetBytes(message),
                            "Enable notifications for your Solana wallet",
                            CancellationToken.None)),
                    force);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link retry failed: {e.Message}");
                return false;
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
        /// <param name="force">Relink even when this address is already recorded as linked.</param>
        public static async Task<bool> TryLinkSolanaMwaAsync(
            PlutoFrameworkSolanaAccount account,
            bool force = false)
        {
            if (Interlocked.Exchange(ref mwaLinkInProgress, 1) == 1)
            {
                return false;
            }

            try
            {
                if (!force && await DeviceRegisterService.IsWalletLinkedAsync(WalletChain.Solana, account.Address))
                {
                    return true;
                }

                return await DeviceRegisterService.LinkWalletAsync(
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
                    },
                    force);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet link failed: {e.Message}");
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref mwaLinkInProgress, 0);
            }
        }

        /// <summary>
        /// Relinks the Solana address on demand, even when a link is already recorded.
        /// Unlike every other entry point here this one is user-initiated, so the signature
        /// prompt - or the trip out to the wallet app under Mobile Wallet Adapter - is
        /// expected rather than a surprise.
        /// </summary>
        public static async Task<bool> RelinkSolanaAsync()
        {
            try
            {
                var account = await PlutoFrameworkSolanaAccount.ResolveAsync(
                    "Enable notifications for your Solana wallet");

                if (account is null)
                {
                    return false;
                }

                return account.CanSignLocally
                    ? await TryLinkResolvedSolanaAccountAsync(account, force: true)
                    : await TryLinkSolanaMwaAsync(account, force: true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[PlutoNotifications] Solana wallet relink failed: {e.Message}");
                return false;
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
