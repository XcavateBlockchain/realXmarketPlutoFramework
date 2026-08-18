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
        /// Mainnet. A user who never opens Settings must end up on the network their real
        /// funds and the deployed programs live on, never on a test network.
        /// </summary>
        public const SolanaCluster Default = SolanaCluster.Mainnet;

        /// <summary>
        /// In display order. Testnet is deliberately absent: it exists to stage validator
        /// releases, not as a place this app's programs are deployed, so offering it would
        /// only give users a third way to end up somewhere nothing works.
        /// </summary>
        public static readonly SolanaCluster[] Selectable =
            [SolanaCluster.Mainnet, SolanaCluster.Devnet];
    }
}
