namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One plotted point on the token detail page's price chart.
    /// </summary>
    /// <remarks>
    /// Jupiter returns full OHLCV candles. A line chart plots closes, so open, high, low and
    /// volume are dropped at the parser rather than carried unused through three layers.
    /// </remarks>
    public sealed record SolanaPricePoint
    {
        public required DateTimeOffset Time { get; init; }

        public required double UsdPrice { get; init; }
    }
}
