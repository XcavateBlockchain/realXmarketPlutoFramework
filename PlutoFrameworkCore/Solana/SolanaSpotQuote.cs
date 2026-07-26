namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One token's current price and recent movement, as the detail page's price row shows it.
    /// </summary>
    public sealed record SolanaSpotQuote
    {
        public required double UsdPrice { get; init; }

        /// <summary>
        /// Percent, so 1.67 means +1.67%. Null when Jupiter omitted it — distinct from 0,
        /// which would assert the price held steady.
        /// </summary>
        public double? Change24h { get; init; }
    }
}
