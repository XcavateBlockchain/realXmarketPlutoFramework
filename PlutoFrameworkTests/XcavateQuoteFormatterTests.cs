using PlutoFrameworkCore.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The 9-decimal quote formatting, pinned against the messenger dashboard's
    /// formatGbpQuote / formatGbpTotal: pounds, thousand separators, at most six
    /// fraction digits, truncated rather than rounded.
    /// </summary>
    public class XcavateQuoteFormatterTests
    {
        [Test]
        [TestCase(123_000_000_000UL, "£123")]
        [TestCase(123_456_789_000UL, "£123.456789")]
        [TestCase(1_000UL, "£0.000001")]
        [TestCase(1UL, "£0")]
        [TestCase(0UL, "£0")]
        [TestCase(1_234_567_000_000_000UL, "£1,234,567")]
        [TestCase(1_500_000_000UL, "£1.5")]
        // Truncation, never rounding: the displayed price must not exceed the agreed one.
        [TestCase(123_456_789_900UL, "£123.456789")]
        public void Format_RendersTheQuoteLikeTheDashboard(ulong quote, string expected)
        {
            Assert.That(XcavateQuoteFormatter.Format(quote), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(123_000_000_000UL, 5u, "£615")]
        [TestCase(100_000_000_000UL, 3u, "£300")]
        [TestCase(1_000UL, 2u, "£0.000002")]
        [TestCase(123_456_789_000UL, 2u, "£246.913578")]
        public void FormatTotal_RendersTheOrderTotalLikeTheDashboard(ulong quote, uint amount, string expected)
        {
            Assert.That(XcavateQuoteFormatter.FormatTotal(quote, amount), Is.EqualTo(expected));
        }

        [Test]
        public void FormatTotal_HandlesTotalsBeyondU64QuoteUnits()
        {
            // 2^63 quote units per token times two tokens: the intermediate quote-units
            // total overflows a signed 64-bit integer, so this only works in decimal.
            Assert.That(
                XcavateQuoteFormatter.FormatTotal(9_223_372_036_854_775_808UL, 2u),
                Is.EqualTo("£18,446,744,073.709551"));
        }

        [Test]
        public void QuoteToPounds_ScalesByNineDecimals()
        {
            Assert.That(XcavateQuoteFormatter.QuoteToPounds(123_456_789_000UL), Is.EqualTo(123.456789m));
        }
    }
}
