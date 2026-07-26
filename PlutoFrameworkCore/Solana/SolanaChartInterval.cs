using PlutoFramework.Model;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Maps the app's chart intervals onto Jupiter's chart query. Pure, so the
    /// seconds-versus-milliseconds trap is settled in one tested place instead of at the
    /// call site.
    /// </summary>
    public static class SolanaChartInterval
    {
        public static string ToJupiterInterval(Interval interval) => interval switch
        {
            Interval.Hourly => "1_HOUR",
            Interval.Daily => "1_DAY",
            Interval.Weekly => "1_WEEK",
            _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, null),
        };

        /// <summary>
        /// One interval's own length. The window is this times the step count, with no
        /// padding: Jupiter caps at <c>candles</c> and returns the most recent, so a padded
        /// window would only discard the oldest points it fetched.
        /// </summary>
        private static TimeSpan StepLength(Interval interval) => interval switch
        {
            Interval.Hourly => TimeSpan.FromHours(1),
            Interval.Daily => TimeSpan.FromDays(1),
            Interval.Weekly => TimeSpan.FromDays(7),
            _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, null),
        };

        public static (DateTimeOffset From, DateTimeOffset To) GetWindow(
            Interval interval, int steps, DateTimeOffset now) =>
            (now - StepLength(interval) * steps, now);

        /// <summary>
        /// The full request URL. <c>from</c> and <c>to</c> are Unix milliseconds; sending
        /// seconds is not rejected, it just returns an empty candle array, which would reach
        /// the user as a blank chart rather than an error.
        /// </summary>
        public static string BuildQuery(string mint, Interval interval, int steps, DateTimeOffset now)
        {
            var (from, to) = GetWindow(interval, steps, now);

            return $"https://datapi.jup.ag/v2/charts/{mint}" +
                $"?interval={ToJupiterInterval(interval)}" +
                $"&from={from.ToUnixTimeMilliseconds()}" +
                $"&to={to.ToUnixTimeMilliseconds()}" +
                $"&candles={steps}" +
                "&type=price";
        }
    }
}
