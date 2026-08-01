using PlutoFramework.Model.Solana;
using PlutoFrameworkCore;
using PlutoFrameworkCore.PushNotificationServices.Core;
using PlutoFrameworkCore.PushNotificationServices.Core.Misc;
using PlutoFrameworkCore.Solana;
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
        /// Mobile Wallet Adapter accounts are skipped: they sign by launching the wallet
        /// app, which must never happen as a surprise side effect of an unrelated action.
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
