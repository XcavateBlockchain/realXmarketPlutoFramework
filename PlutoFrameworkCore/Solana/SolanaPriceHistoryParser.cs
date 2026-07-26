using System.Text.Json;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Reads Jupiter's chart response: an object holding a <c>candles</c> array of OHLCV
    /// entries. Only <c>time</c> and <c>close</c> are kept — the chart is a line, not candles.
    /// </summary>
    public static class SolanaPriceHistoryParser
    {
        public static IReadOnlyList<SolanaPricePoint> Parse(string json)
        {
            var points = new List<SolanaPricePoint>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return points;
            }

            try
            {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("candles", out var candles) ||
                    candles.ValueKind != JsonValueKind.Array)
                {
                    // Jupiter reports a rejected request as a plain object carrying "status"
                    // and "message" rather than an error status code, so a missing array is
                    // an ordinary outcome here, not a surprise.
                    return points;
                }

                foreach (var candle in candles.EnumerateArray())
                {
                    if (candle.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!candle.TryGetProperty("time", out var time) ||
                        time.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    if (!candle.TryGetProperty("close", out var close) ||
                        close.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    points.Add(new SolanaPricePoint
                    {
                        // Unix seconds. The request's from/to are milliseconds - the opposite
                        // unit - so reading this as milliseconds plots every point in 1970.
                        Time = DateTimeOffset.FromUnixTimeSeconds(time.GetInt64()),
                        UsdPrice = close.GetDouble(),
                    });
                }
            }
            catch (JsonException)
            {
                // A malformed body is a feed problem. Degrade to "no history" rather than
                // throwing into the page's load path.
                return [];
            }

            return points;
        }
    }
}
