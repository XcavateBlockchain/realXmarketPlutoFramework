using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Whether the connected wallet holds the on-chain lawyer role.
    /// </summary>
    /// <remarks>
    /// Read through the whitelist program's indexer (<see cref="WhitelistModel.HasRoleAsync"/>),
    /// cached per (address, cluster) so repeated role checks do not re-query on every tap;
    /// a cluster switch invalidates the cache. When the query itself fails -
    /// offline, indexer down - the persisted profile's <see cref="UserRoleEnum.Lawyer"/>
    /// answers instead; the failure answer is never cached, so the next ask retries the indexer.
    /// </remarks>
    public static class LawyerRoleModel
    {
        private static readonly object CacheLock = new();
        private static string? cachedAddress;
        private static SolanaCluster? cachedCluster;
        private static bool cachedIsLawyer;

        static LawyerRoleModel()
        {
            // Static-class lifetime: subscribed once, never unsubscribed, same convention
            // as the other singleton models listening to the network switch.
            SolanaNetworkModel.ClusterChanged += (_, _) => Invalidate();
        }

        /// <summary>Clears the cached answer (wallet or cluster change).</summary>
        public static void Invalidate()
        {
            lock (CacheLock)
            {
                cachedAddress = null;
                cachedCluster = null;
                cachedIsLawyer = false;
            }
        }

        public static async Task<bool> IsLawyerAsync(SolanaCluster cluster, CancellationToken ct)
        {
            var address = KeysModel.GetSolanaAddress();

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            lock (CacheLock)
            {
                if (cachedAddress == address && cachedCluster == cluster)
                {
                    return cachedIsLawyer;
                }
            }

            try
            {
                // A cluster with no deployment has no indexer to ask and no lawyer page
                // content; treat the wallet as role-less there rather than throwing.
                var isLawyer = XcavateDeploymentModel.IsDeployed(cluster)
                    && await WhitelistModel.HasRoleAsync(address, XcavateRole.Lawyer, cluster, ct).ConfigureAwait(false);

                lock (CacheLock)
                {
                    cachedAddress = address;
                    cachedCluster = cluster;
                    cachedIsLawyer = isLawyer;
                }

                return isLawyer;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lawyer role check failed, falling back to the persisted profile role:");
                Console.WriteLine(ex);

                try
                {
                    var user = await XcavateUserDatabase.GetUserInformationAsync().ConfigureAwait(false);

                    return user?.Role == UserRoleEnum.Lawyer;
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine(dbEx);

                    return false;
                }
            }
        }
    }
}
