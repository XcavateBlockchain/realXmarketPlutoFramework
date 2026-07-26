using PlutoFramework.Model;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The token detail page's chart data: mint plus interval in, plotted points out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin, matching <see cref="SolanaBalancesModel"/>. Everything decidable
    /// without a network — the interval mapping, the timestamp units, the response shape —
    /// lives in <see cref="SolanaChartInterval"/> and <see cref="SolanaPriceHistoryParser"/>,
    /// where it is tested. This method only fetches.
    /// </para>
    /// <para>
    /// The endpoint is <b>not</b> part of Jupiter's published API. It backs jup.ag's own
    /// charts and needs no key, but it can change without notice, which is why every failure
    /// here degrades to "no history" rather than surfacing an error.
    /// </para>
    /// </remarks>
    public static class SolanaPriceHistoryModel
    {
        /// <summary>
        /// Reused: a fresh HttpClient per call leaks sockets, matching
        /// <see cref="SolanaPriceModel"/>.
        /// </summary>
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<IReadOnlyList<SolanaPricePoint>> GetPriceHistoryAsync(
            string mint, Interval interval, int steps, CancellationToken token)
        {
            try
            {
                var url = SolanaChartInterval.BuildQuery(mint, interval, steps, DateTimeOffset.UtcNow);

                return SolanaPriceHistoryParser.Parse(await Client.GetStringAsync(url, token));
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // The chart is decoration; holdings are the feature. An outage must leave the
                // page showing the balance rather than nothing. Cancellation is excluded so
                // the caller's staleness guard still sees it.
                Console.WriteLine($"Solana price history fetch failed: {ex.Message}");

                return [];
            }
        }
    }
}
