namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Which Solana networks the app lets a user choose between, and the one it uses until
    /// they choose. Kept here rather than in the UI layer so the default is a single stated
    /// fact instead of a literal repeated at every call site.
    /// </summary>
    public static class SolanaNetworkOptions
    {
        /// <summary>
        /// Devnet. The Xcavate Solana programs are only deployed there today, so a user
        /// who never opens Settings lands on the network where everything works.
        /// For the production release - once idls/mainnet/ is filled in and
        /// XcavateProgramAddresses.Mainnet and XcavateWhitelistIndexer.MainnetUrl point at
        /// the mainnet deployment - flip this to <see cref="SolanaCluster.Mainnet"/>. The
        /// picker, the stored-preference repair and every consumer follow automatically.
        /// </summary>
        public const SolanaCluster Default = SolanaCluster.Devnet;

        /// <summary>
        /// Both networks the wallet layer works on. Where the Xcavate programs are not
        /// deployed the marketplace degrades to a placeholder
        /// (XcavateDeploymentModel.IsDeployed) rather than blocking the network. Testnet is
        /// deliberately absent: it exists to stage validator releases, not as a place this
        /// app's programs are deployed.
        /// </summary>
        public static readonly SolanaCluster[] Selectable =
            [SolanaCluster.Devnet, SolanaCluster.Mainnet];
    }
}
