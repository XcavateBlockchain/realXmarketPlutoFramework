using System.Text.Json;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Reads Jupiter's price response: an object keyed by mint, each value carrying
    /// <c>usdPrice</c> among other fields.
    /// </summary>
    public static class SolanaPriceParser
    {
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
