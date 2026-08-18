using System.Text.Json;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Reads Jupiter's price response: an object keyed by mint, each value carrying
    /// <c>usdPrice</c> among other fields.
    /// </summary>
    public static class SolanaPriceParser
    {
        /// <summary>
        /// The same body as <see cref="Parse"/>, read for the detail page's price row, which
        /// needs the movement as well as the price. Kept separate rather than widening
        /// <see cref="Parse"/>: the balances page has no use for the change, and its
        /// dictionary-of-double shape is what the assembler already consumes.
        /// </summary>
        public static IReadOnlyDictionary<string, SolanaSpotQuote> ParseQuotes(string json)
        {
            var quotes = new Dictionary<string, SolanaSpotQuote>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(json))
            {
                return quotes;
            }

            try
            {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return quotes;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!property.Value.TryGetProperty("usdPrice", out var price) ||
                        price.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    var hasChange =
                        property.Value.TryGetProperty("priceChange24h", out var change) &&
                        change.ValueKind == JsonValueKind.Number;

                    quotes[property.Name] = new SolanaSpotQuote
                    {
                        UsdPrice = price.GetDouble(),
                        Change24h = hasChange ? change.GetDouble() : null,
                    };
                }
            }
            catch (JsonException)
            {
                return new Dictionary<string, SolanaSpotQuote>(StringComparer.Ordinal);
            }

            return quotes;
        }

        public static IReadOnlyDictionary<string, double> Parse(string json)
        {
            var prices = new Dictionary<string, double>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(json))
            {
                return prices;
            }

            try
            {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return prices;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!property.Value.TryGetProperty("usdPrice", out var price) ||
                        price.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    prices[property.Name] = price.GetDouble();
                }
            }
            catch (JsonException)
            {
                // A malformed body is a feed problem. Degrade to "no prices" rather than
                // throwing into the page's load path.
                return new Dictionary<string, double>(StringComparer.Ordinal);
            }

            return prices;
        }
    }
}
