using System.Globalization;
using System.Numerics;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Base units to display units.
    /// </summary>
    /// <remarks>
    /// Done explicitly rather than through Solnet's <c>AmountDecimal</c> / <c>AmountDouble</c>,
    /// whose scaling the names do not settle. A wrong choice there renders a balance orders
    /// of magnitude off, which no test of ours would catch if we delegated to it.
    /// </remarks>
    public static class SolanaAmount
    {
        public static decimal FromBaseUnits(string rawAmount, int decimals)
        {
            if (decimals < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals cannot be negative");
            }

            if (string.IsNullOrWhiteSpace(rawAmount))
            {
                return 0m;
            }

            // NumberStyles.None rejects signs, whitespace and separators: token amounts are
            // unsigned integers, and anything else is a malformed response, not a balance.
            if (!BigInteger.TryParse(rawAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var raw))
            {
                throw new FormatException($"'{rawAmount}' is not a base-unit token amount");
            }

            return (decimal)raw / (decimal)BigInteger.Pow(10, decimals);
        }

        /// <summary>
        /// A balance as the balances list and the detail page both print it: a fixed four
        /// decimal places, so a small balance reads "0.4000 tGBP" rather than collapsing to
        /// "0" under integer or trimmed formatting.
        /// </summary>
        /// <remarks>
        /// Rounded to four places whatever the mint declares: SOL's nine would push the USD
        /// column off a narrow screen, and the extra digits are noise at any realistic balance.
        /// </remarks>
        public static string ToDisplayString(decimal amount, int decimals) =>
            Math.Round(amount, 4)
                .ToString("0.0000", CultureInfo.InvariantCulture);

        /// <summary>
        /// Display units to base units — the inverse of <see cref="FromBaseUnits"/>, used to
        /// turn what the user typed into what the instruction carries.
        /// </summary>
        /// <remarks>
        /// Truncates, never rounds. Max fills the field with the exact balance, and rounding
        /// the last place up would build a transaction for one base unit more than the wallet
        /// holds — rejected by the chain, after the user has already confirmed.
        /// </remarks>
        public static BigInteger ToBaseUnits(decimal amount, int decimals)
        {
            if (decimals < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals cannot be negative");
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "A token amount cannot be negative");
            }

            var scaled = amount * (decimal)BigInteger.Pow(10, decimals);

            return (BigInteger)decimal.Truncate(scaled);
        }

        public static decimal FromLamports(ulong lamports) =>
            FromBaseUnits(lamports.ToString(CultureInfo.InvariantCulture), SolanaNativeToken.Decimals);
    }
}
