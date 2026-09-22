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
        /// When the mainnet programs deploy, flip this to <see cref="SolanaCluster.Mainnet"/>
        /// and re-add mainnet to <see cref="Selectable"/>.
        /// </summary>
        public const SolanaCluster Default = SolanaCluster.Devnet;

        /// <summary>
        /// Devnet only, for now: mainnet has no Xcavate programs deployed
        /// (XcavateProgramAddresses.Mainnet is null), so offering it would put the whole
        /// app on a network where nothing works - and a wallet in testnet mode rejects
        /// the mainnet authorization outright. Re-add <see cref="SolanaCluster.Mainnet"/>
        /// when idls/mainnet/ is filled in. Testnet is deliberately absent too: it exists
        /// to stage validator releases, not as a place this app's programs are deployed.
        /// </summary>
        public static readonly SolanaCluster[] Selectable =
            [SolanaCluster.Devnet];
    }
}
