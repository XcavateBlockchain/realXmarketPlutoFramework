using System.Numerics;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Base58 for arbitrary byte lengths.
    ///
    /// Solnet's own encoder is not public, and its <c>PublicKey</c> cannot stand in: that type
    /// rejects anything other than 32 bytes, while a Solana transaction signature is 64.
    /// </summary>
    public static class SolanaBase58
    {
        /// <summary>
        /// The Bitcoin/Solana alphabet. 0, O, I and l are omitted because they are easy to
        /// confuse when read aloud or retyped.
        /// </summary>
        private const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        private const char ZERO = '1';

        private static readonly BigInteger Radix = 58;

        public static string Encode(byte[] data)
        {
            if (data.Length == 0)
            {
                return "";
            }

            // Each leading zero byte encodes as one '1' rather than being consumed by the
            // arithmetic, which would silently shorten the result.
            var leadingZeroes = 0;

            while (leadingZeroes < data.Length && data[leadingZeroes] == 0)
            {
                leadingZeroes++;
            }

            // isUnsigned so a high first bit is not read as a negative number, and
            // isBigEndian to match the wire order.
            var value = new BigInteger(data, isUnsigned: true, isBigEndian: true);

            var digits = new Stack<char>();

            while (value > 0)
            {
                value = BigInteger.DivRem(value, Radix, out var remainder);

                digits.Push(ALPHABET[(int)remainder]);
            }

            return new string(ZERO, leadingZeroes) + new string([.. digits]);
        }

        public static byte[] Decode(string encoded)
        {
            if (encoded.Length == 0)
            {
                return [];
            }

            var leadingZeroes = 0;

            while (leadingZeroes < encoded.Length && encoded[leadingZeroes] == ZERO)
            {
                leadingZeroes++;
            }

            var value = BigInteger.Zero;

            foreach (var character in encoded)
            {
                var digit = ALPHABET.IndexOf(character);

                if (digit < 0)
                {
                    throw new FormatException($"'{character}' is not a base58 character");
                }

                value = (value * Radix) + digit;
            }

            var decoded = value.IsZero
                ? []
                : value.ToByteArray(isUnsigned: true, isBigEndian: true);

            var result = new byte[leadingZeroes + decoded.Length];

            decoded.CopyTo(result, leadingZeroes);

            return result;
        }
    }
}
