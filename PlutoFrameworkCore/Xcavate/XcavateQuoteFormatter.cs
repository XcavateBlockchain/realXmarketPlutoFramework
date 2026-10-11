using System.Globalization;

namespace PlutoFrameworkCore.Xcavate
{
    /// <summary>
    /// Formats the marketplace's 9-decimal share-price quote for display, mirroring the
    /// messenger dashboard's <c>formatGbpQuote</c> / <c>formatGbpTotal</c>: pounds with
    /// thousand separators and up to six fraction digits, the extra precision truncated
    /// (never rounded - a price must never display higher than what was agreed).
    /// </summary>
    public static class XcavateQuoteFormatter
    {
        /// <summary>The quote scale: one whole pound is 10^9 quote units.</summary>
        public const int QuoteDecimals = 9;

        /// <summary>How many fraction digits the display keeps, truncated like the dashboard's.</summary>
        public const int DisplayDecimals = 6;

        private const string CurrencySymbol = "£";

        /// <summary>One share's price as a currency string, e.g. "£123.456789".</summary>
        public static string Format(ulong quote) => FormatPounds(QuoteToPounds(quote));

        /// <summary>
        /// The order total (<paramref name="quote"/> x <paramref name="amount"/>) as a
        /// currency string. Computed in decimal, so a large quote times a large amount
        /// cannot overflow the program's u64 arithmetic the way a ulong multiply would.
        /// </summary>
        public static string FormatTotal(ulong quote, uint amount) =>
            FormatPounds(QuoteToPounds(quote) * amount);

        /// <summary>The quote as pounds, untruncated.</summary>
        public static decimal QuoteToPounds(ulong quote) => quote / 1_000_000_000m;

        /// <summary>
        /// Truncate to the display precision, then let the format string group thousands
        /// and drop trailing zeros. "#,##0.######" renders no decimal point at all for a
        /// whole number, exactly like the dashboard's formatter.
        /// </summary>
        private static string FormatPounds(decimal pounds)
        {
            var truncated = Math.Truncate(pounds * 1_000_000m) / 1_000_000m;

            return CurrencySymbol + truncated.ToString("#,##0.######", CultureInfo.InvariantCulture);
        }
    }
}
