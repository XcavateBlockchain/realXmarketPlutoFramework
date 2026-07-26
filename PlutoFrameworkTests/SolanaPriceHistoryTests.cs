using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaPriceHistoryParserTests
    {
        /// <summary>
        /// The exact shape returned by datapi.jup.ag/v2/charts, captured live on 2026-07-26.
        /// Note <c>time</c> is Unix seconds, while the request's from/to are milliseconds.
        /// </summary>
        private const string SampleResponse = """
        {"candles":[
          {"time":1784977200,"open":73.91344226642872,"high":73.97769893283103,
           "low":73.77231204963448,"close":73.85630878051603,"volume":4344541.16387761},
          {"time":1784980800,"open":73.85630878051603,"high":74.12373567166883,
           "low":73.85630878051603,"close":73.90661330950269,"volume":4666384.968467375}
        ]}
        """;

        [Test]
        public void ReadsClosingPricesInOrder()
        {
            var points = SolanaPriceHistoryParser.Parse(SampleResponse);

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Count.EqualTo(2));
                Assert.That(points[0].UsdPrice, Is.EqualTo(73.85630878051603).Within(0.0000001));
                Assert.That(points[1].UsdPrice, Is.EqualTo(73.90661330950269).Within(0.0000001));
            });
        }

        /// <summary>
        /// Jupiter sends Unix seconds here but expects milliseconds in the query string.
        /// Reading the response as milliseconds silently plots every point in 1970.
        /// </summary>
        [Test]
        public void TimeIsReadAsUnixSeconds()
        {
            var points = SolanaPriceHistoryParser.Parse(SampleResponse);

            Assert.That(
                points[0].Time,
                Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1784977200)));
        }

        /// <summary>
        /// Jupiter answers a request whose from/to are in the wrong unit with an empty array
        /// rather than an error, so "no candles" must never become a zero-priced point.
        /// </summary>
        [Test]
        public void EmptyCandleArrayYieldsNoPoints()
        {
            Assert.That(SolanaPriceHistoryParser.Parse("""{"candles":[]}"""), Is.Empty);
        }

        [Test]
        public void MissingCandlesPropertyYieldsNoPoints()
        {
            Assert.That(SolanaPriceHistoryParser.Parse("""{"status":400,"message":"nope"}"""), Is.Empty);
        }

        /// <summary>
        /// A malformed body is a feed problem. It must degrade to "no history", never throw
        /// into the page's load path.
        /// </summary>
        [Test]
        public void MalformedJsonYieldsNoPoints()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaPriceHistoryParser.Parse("not json"), Is.Empty);
                Assert.That(SolanaPriceHistoryParser.Parse(""), Is.Empty);
                Assert.That(SolanaPriceHistoryParser.Parse("[1,2,3]"), Is.Empty);
            });
        }

        [Test]
        public void CandleWithoutCloseIsSkippedAndSiblingsKept()
        {
            var points = SolanaPriceHistoryParser.Parse(
                """{"candles":[{"time":1,"open":5.0},{"time":2,"close":7.0}]}""");

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Count.EqualTo(1));
                Assert.That(points[0].UsdPrice, Is.EqualTo(7.0));
            });
        }

        [Test]
        public void CandleWithNonNumericCloseIsSkippedAndSiblingsKept()
        {
            var points = SolanaPriceHistoryParser.Parse(
                """{"candles":[{"time":1,"close":"oops"},{"time":2,"close":7.0}]}""");

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Count.EqualTo(1));
                Assert.That(points[0].UsdPrice, Is.EqualTo(7.0));
            });
        }

        [Test]
        public void CandleWithNonNumericTimeIsSkippedAndSiblingsKept()
        {
            var points = SolanaPriceHistoryParser.Parse(
                """{"candles":[{"time":"soon","close":5.0},{"time":2,"close":7.0}]}""");

            Assert.Multiple(() =>
            {
                Assert.That(points, Has.Count.EqualTo(1));
                Assert.That(points[0].UsdPrice, Is.EqualTo(7.0));
            });
        }
    }

    public class SolanaChartIntervalTests
    {
        [Test]
        public void EachIntervalMapsToItsJupiterName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaChartInterval.ToJupiterInterval(Interval.Hourly), Is.EqualTo("1_HOUR"));
                Assert.That(SolanaChartInterval.ToJupiterInterval(Interval.Daily), Is.EqualTo("1_DAY"));
                Assert.That(SolanaChartInterval.ToJupiterInterval(Interval.Weekly), Is.EqualTo("1_WEEK"));
            });
        }

        [Test]
        public void HourlyWindowSpansOneHourPerStep()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var (from, to) = SolanaChartInterval.GetWindow(Interval.Hourly, steps: 24, now);

            Assert.That(to - from, Is.EqualTo(TimeSpan.FromHours(24)));
        }

        [Test]
        public void DailyWindowSpansOneDayPerStep()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var (from, to) = SolanaChartInterval.GetWindow(Interval.Daily, steps: 24, now);

            Assert.That(to - from, Is.EqualTo(TimeSpan.FromDays(24)));
        }

        [Test]
        public void WeeklyWindowSpansOneWeekPerStep()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var (from, to) = SolanaChartInterval.GetWindow(Interval.Weekly, steps: 24, now);

            Assert.That(to - from, Is.EqualTo(TimeSpan.FromDays(24 * 7)));
        }

        [Test]
        public void WindowEndsAtTheGivenMoment()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var (_, to) = SolanaChartInterval.GetWindow(Interval.Daily, steps: 24, now);

            Assert.That(to, Is.EqualTo(now));
        }

        /// <summary>
        /// Jupiter takes from/to in milliseconds but returns each candle's time in seconds.
        /// Sending seconds is not an error - it quietly returns an empty array - so the unit
        /// is pinned here rather than discovered as a blank chart.
        /// </summary>
        [Test]
        public void QueryTimestampsAreUnixMilliseconds()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var query = SolanaChartInterval.BuildQuery(
                SolanaNativeToken.Mint, Interval.Hourly, steps: 24, now);

            Assert.Multiple(() =>
            {
                Assert.That(query, Does.Contain("to=1785000000000"));
                Assert.That(query, Does.Contain("from=1784913600000"));
            });
        }

        [Test]
        public void QueryCarriesTheMintIntervalAndStepCount()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_785_000_000);

            var query = SolanaChartInterval.BuildQuery(
                SolanaNativeToken.Mint, Interval.Weekly, steps: 24, now);

            Assert.Multiple(() =>
            {
                Assert.That(query, Does.Contain(SolanaNativeToken.Mint));
                Assert.That(query, Does.Contain("interval=1_WEEK"));
                Assert.That(query, Does.Contain("candles=24"));
                Assert.That(query, Does.Contain("type=price"));
            });
        }
    }
}
