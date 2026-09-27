using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Whether the Xcavate feature set that lives on the custom Solana programs - the
    /// property marketplace, roles, reservations - is deployed on a given cluster at all.
    /// <para>
    /// This is the single gate every UI decision goes through, so "the marketplace is not
    /// on this network" looks the same everywhere. The answer is computed from the two
    /// per-cluster registries: the programs (<see cref="XcavateProgramAddresses"/>) and the
    /// indexer (<see cref="XcavateWhitelistIndexer"/>). Both keep their own per-cluster
    /// values, so a devnet deployment that runs ahead of mainnet never disturbs mainnet's
    /// entries - filling in the two Mainnet placeholders is all it takes for
    /// <see cref="IsDeployed"/> to light mainnet up.
    /// </para>
    /// </summary>
    public static class XcavateDeploymentModel
    {
        /// <summary>
        /// True when the programs AND the indexer are both deployed on
        /// <paramref name="cluster"/> - the marketplace works end to end only then.
        /// </summary>
        public static bool IsDeployed(SolanaCluster cluster) =>
            XcavateProgramAddresses.Get(cluster) is not null && XcavateWhitelistIndexer.IsSupported(cluster);

        /// <summary>
        /// The shared wording for every "not on this network" surface (empty feeds, toasts,
        /// gated actions). Points at a network where the marketplace does work, when one is
        /// selectable, so the message is an instruction rather than a dead end.
        /// </summary>
        public static string NotDeployedMessage(SolanaCluster cluster)
        {
            var message = $"The Xcavate marketplace is not available on Solana {cluster.GetName()} yet.";

            SolanaCluster? fallback = SolanaNetworkOptions.Selectable
                .Cast<SolanaCluster?>()
                .FirstOrDefault(candidate => candidate != cluster && IsDeployed(candidate.Value));

            return fallback is SolanaCluster deployed
                ? $"{message} Switch to {deployed.GetName()} in Settings to use it."
                : message;
        }
    }
}
