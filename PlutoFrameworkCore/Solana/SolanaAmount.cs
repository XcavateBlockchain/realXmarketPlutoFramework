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

        public static decimal FromLamports(ulong lamports) =>
            FromBaseUnits(lamports.ToString(CultureInfo.InvariantCulture), SolanaNativeToken.Decimals);
    }
}
