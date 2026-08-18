namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One SPL token the app displays, on one cluster. Symbol and decimals are configured
    /// rather than read from chain so a token the user holds no account for can still be
    /// listed at zero.
    /// </summary>
    public sealed record SolanaTokenWhitelistEntry
    {
        public required SolanaCluster Cluster { get; init; }

        /// <summary>Base58 mint address. Cluster-specific: the same token has a different mint on each.</summary>
        public required string Mint { get; init; }

        public required string Symbol { get; init; }

        public required int Decimals { get; init; }

        /// <summary>
        /// A fixed USD price, for stablecoins. Null means priced from the live feed. A pinned
        /// price never reaches the network, so a feed outage cannot misprice a stablecoin.
        /// </summary>
        public double? PinnedUsdPrice { get; init; }

        /// <summary>
        /// Whether the detail page draws a price chart for this token. Off by default:
        /// every token configured so far is a stablecoin, and a flat line implies a
        /// volatility they do not have. A token earns a chart deliberately, so a new entry
        /// cannot acquire one by omission.
        /// </summary>
        public bool ShowPriceChart { get; init; }

        public string ProgramId { get; init; } = SolanaTokenProgram.Legacy;
    }

    public static class SolanaTokenWhitelist
    {
        /// <summary>
        /// The tokens configured for one cluster.
        /// </summary>
        /// <remarks>
        /// An empty result means "no SPL tokens", not "all of them" — the inverse of
        /// <see cref="PlutoConfigurationModel.WhitelistedTokens"/>, which filters a set
        /// discovered on chain. This list *is* the set.
        /// </remarks>
        public static IReadOnlyList<SolanaTokenWhitelistEntry> ForCluster(SolanaCluster cluster) =>
            PlutoConfigurationModel.WhitelistedSolanaTokens
                .Where(entry => entry.Cluster == cluster)
                .ToList();
    }
}
