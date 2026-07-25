using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Application-level Mobile Wallet Adapter operations: builds this app's identity,
    /// connects, and persists or removes the resulting authorization.
    /// </summary>
    public static class SolanaMwaModel
    {
        /// <summary>
        /// Remembers the cluster the user last connected on, so the picker does not reset
        /// to a different network between visits.
        /// </summary>
        public const string PREFERRED_CLUSTER = "solanaPreferredCluster";

        public static bool IsSupported => MwaConnectFlow.IsSupported;

        public static SolanaCluster PreferredCluster
        {
            get => SolanaClusterExtensions.FromChainId(
                Preferences.Get(PREFERRED_CLUSTER, SolanaCluster.Devnet.ToChainId()));

            set => Preferences.Set(PREFERRED_CLUSTER, value.ToChainId());
        }

        /// <summary>
        /// How this app presents itself on the wallet's approval screen.
        /// </summary>
        public static MwaIdentity BuildIdentity() => new()
        {
            Name = AppInfo.Current.Name,
            Uri = $"https://{AppInfo.Current.PackageName}",
        };

        /// <summary>
        /// Connects to a wallet, authorizes an account, and stores the authorization.
        /// Replaces any existing Solana key, since the two variants share one slot.
        /// </summary>
        public static async Task<SolanaMwaKey> ConnectAndSaveAsync(
            SolanaCluster cluster,
            IProgress<MwaConnectStage>? progress,
            CancellationToken token)
        {
            var result = await MwaConnectFlow.ConnectAsync(
                BuildIdentity(),
                cluster,
                existingAuthToken: null,
                progress,
                token);

            var key = new SolanaMwaKey
            {
                AuthToken = result.AuthToken,
                Address = result.Address,
                Chain = result.Chain,
                WalletUriBase = result.WalletUriBase,
                AccountLabel = result.AccountLabel,
            };

            await KeysModel.SaveSolanaMwaKeyAsync(key);

            PreferredCluster = cluster;

            return key;
        }

        /// <summary>
        /// Revokes the authorization with the wallet where possible, then removes the local
        /// key regardless. A user asking to disconnect must end up disconnected even if
        /// their wallet app is gone or refuses to answer.
        /// </summary>
        /// <returns>True when the wallet confirmed the revocation.</returns>
        public static async Task<bool> DisconnectAsync(GenericLockedKey lockedKey, CancellationToken token)
        {
            var revoked = false;

            try
            {
                var mwaKey = await lockedKey.ToSolanaMwaKeyAsync("Disconnect Solana wallet");

                revoked = await MwaConnectFlow.TryDeauthorizeAsync(
                    BuildIdentity(),
                    new SolanaMwaKeyDeauthorizeRequest { AuthToken = mwaKey.AuthToken },
                    token);
            }
            catch
            {
                // The stored authorization could not be read, so there is nothing to
                // revoke remotely. Removing the local key below is still correct.
            }

            await lockedKey.RemoveAsync();

            return revoked;
        }
    }
}
