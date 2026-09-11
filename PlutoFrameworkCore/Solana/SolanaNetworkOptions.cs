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
        /// who never opens Settings lands on the network where the app actually works.
        /// When the mainnet programs deploy, flip this to <see cref="SolanaCluster.Mainnet"/>.
        /// </summary>
        public const SolanaCluster Default = SolanaCluster.Devnet;

        /// <summary>
        /// In display order, default first. Testnet is deliberately absent: it exists to
        /// stage validator releases, not as a place this app's programs are deployed, so
        /// offering it would only give users a third way to end up somewhere nothing works.
        /// </summary>
        public static readonly SolanaCluster[] Selectable =
            [SolanaCluster.Devnet, SolanaCluster.Mainnet];
    }
}
