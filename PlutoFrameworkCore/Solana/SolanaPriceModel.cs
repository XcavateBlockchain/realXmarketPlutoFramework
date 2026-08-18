namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// USD prices for the tokens on the balances page. Stablecoins carry a pinned price and
    /// never reach the network; everything else, SOL included, comes from Jupiter.
    /// </summary>
    public static class SolanaPriceModel
    {
        private const string PRICE_ENDPOINT = "https://lite-api.jup.ag/price/v3?ids=";

        /// <summary>
        /// Reused: a fresh HttpClient per call leaks sockets, matching how
        /// <see cref="SolanaRpcModel"/> reuses its RPC clients.
        /// </summary>
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

        public static IReadOnlyList<string> MintsNeedingLivePrice(
            IReadOnlyList<SolanaTokenWhitelistEntry> whitelist)
        {
            var mints = new List<string> { SolanaNativeToken.Mint };

            mints.AddRange(whitelist
                .Where(entry => entry.PinnedUsdPrice is null)
                .Select(entry => entry.Mint));

            return mints;
        }

        public static IReadOnlyDictionary<string, double> ResolvePrices(
            IReadOnlyList<SolanaTokenWhitelistEntry> whitelist,
            IReadOnlyDictionary<string, double> livePrices)
        {
            var resolved = new Dictionary<string, double>(livePrices, StringComparer.Ordinal);

            // Applied last so a pinned price overrides whatever the feed said. A depegged
            // quote for a token we treat as a dollar is noise, not news.
            foreach (var entry in whitelist)
            {
                if (entry.PinnedUsdPrice is double pinned)
                {
                    resolved[entry.Mint] = pinned;
                }
            }

            return resolved;
        }

        /// <summary>
        /// One mint's current price and 24-hour movement, for the token detail page's price
        /// row. Null when the feed failed or omitted the mint — never a zeroed quote, which
        /// would read as "worthless" rather than "unknown".
        /// </summary>
        /// <remarks>
        /// The unit price cannot be recovered from the balances page's rows instead: a row
        /// carries amount × price, which is 0 at a zero balance and yields no price at all.
        /// </remarks>
        public static async Task<SolanaSpotQuote?> GetSpotQuoteAsync(
            string mint, CancellationToken token)
        {
            try
            {
                var body = await Client.GetStringAsync(PRICE_ENDPOINT + mint, token);

                return SolanaPriceParser.ParseQuotes(body).TryGetValue(mint, out var quote)
                    ? quote
                    : null;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Console.WriteLine($"Solana spot quote fetch failed: {ex.Message}");

                return null;
            }
        }

        public static async Task<IReadOnlyDictionary<string, double>> GetUsdPricesAsync(
            IReadOnlyList<SolanaTokenWhitelistEntry> whitelist,
            CancellationToken token)
        {
            var livePrices = new Dictionary<string, double>(StringComparer.Ordinal);

            var mints = MintsNeedingLivePrice(whitelist);

            try
            {
                var body = await Client.GetStringAsync(PRICE_ENDPOINT + string.Join(',', mints), token);

                foreach (var (mint, price) in SolanaPriceParser.Parse(body))
                {
                    livePrices[mint] = price;
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // Prices are decoration; balances are the feature. A feed outage must leave
                // the page showing amounts rather than nothing.
                Console.WriteLine($"Solana price fetch failed: {ex.Message}");
            }

            return ResolvePrices(whitelist, livePrices);
        }
    }
}
