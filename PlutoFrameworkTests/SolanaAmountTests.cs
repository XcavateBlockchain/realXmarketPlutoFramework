using System.Numerics;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaAmountTests
    {
        [Test]
        public void ConvertsSixDecimalTokens()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.FromBaseUnits("40000000", 6), Is.EqualTo(40m));
                Assert.That(SolanaAmount.FromBaseUnits("1", 6), Is.EqualTo(0.000001m));
                Assert.That(SolanaAmount.FromBaseUnits("1234567", 6), Is.EqualTo(1.234567m));
            });
        }

        [Test]
        public void ConvertsNineDecimalTokens()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.FromBaseUnits("1000000000", 9), Is.EqualTo(1m));
                Assert.That(SolanaAmount.FromBaseUnits("1", 9), Is.EqualTo(0.000000001m));
            });
        }

        [Test]
        public void ZeroAndEmptyBecomeZero()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.FromBaseUnits("0", 6), Is.EqualTo(0m));
                Assert.That(SolanaAmount.FromBaseUnits("", 6), Is.EqualTo(0m));
            });
        }

        [Test]
        public void ZeroDecimalsIsIdentity()
        {
            Assert.That(SolanaAmount.FromBaseUnits("42", 0), Is.EqualTo(42m));
        }

        /// <summary>
        /// SPL amounts are u64. The largest possible value must not overflow decimal.
        /// </summary>
        [Test]
        public void HandlesMaximumUnsignedLong()
        {
            Assert.That(SolanaAmount.FromBaseUnits("18446744073709551615", 9),
                Is.EqualTo(18446744073.709551615m));
        }

        [Test]
        public void LamportsConvertToSol()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.FromLamports(1_000_000_000UL), Is.EqualTo(1m));
                Assert.That(SolanaAmount.FromLamports(0UL), Is.EqualTo(0m));
                Assert.That(SolanaAmount.FromLamports(12_345UL), Is.EqualTo(0.000012345m));
            });
        }

        [Test]
        public void RejectsNegativeDecimals()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaAmount.FromBaseUnits("1", -1));
        }

        [Test]
        public void RejectsNonNumericAmounts()
        {
            Assert.Throws<FormatException>(() => SolanaAmount.FromBaseUnits("not-a-number", 6));
        }

        /// <summary>
        /// A fixed four places: whole balances keep the places ("40.0000") so the format
        /// never collapses a value, and small balances stay legible ("0.4000" instead of "0").
        /// </summary>
        [Test]
        public void DisplayStringShowsFourDecimalPlaces()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.ToDisplayString(40m, decimals: 6), Is.EqualTo("40.0000"));
                Assert.That(SolanaAmount.ToDisplayString(0.4m, decimals: 9), Is.EqualTo("0.4000"));
            });
        }

        [Test]
        public void DisplayStringRoundsSmallBalancesToFourPlaces()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.ToDisplayString(0.00004m, decimals: 9), Is.EqualTo("0.0000"));
                Assert.That(SolanaAmount.ToDisplayString(0.00006m, decimals: 9), Is.EqualTo("0.0001"));
            });
        }

        /// <summary>
        /// Four places is the cap regardless of the mint's own decimals, so a nine-decimal
        /// SOL balance does not push the USD column off a narrow screen.
        /// </summary>
        [Test]
        public void DisplayStringCapsAtFourPlaces()
        {
            Assert.That(SolanaAmount.ToDisplayString(1.123456789m, decimals: 9), Is.EqualTo("1.1235"));
        }

        [Test]
        public void DisplayStringPadsFewerMintDecimals()
        {
            Assert.That(SolanaAmount.ToDisplayString(1.129m, decimals: 2), Is.EqualTo("1.1290"));
        }

        [Test]
        public void DisplayStringRendersZero()
        {
            Assert.That(SolanaAmount.ToDisplayString(0m, decimals: 6), Is.EqualTo("0.0000"));
        }

        /// <summary>
        /// The one rule that matters for sending: rounding up a Max-filled balance would
        /// build a transaction for one base unit more than the wallet holds, which the
        /// chain rejects after the user has already confirmed.
        /// </summary>
        [Test]
        public void ToBaseUnitsTruncatesRatherThanRounds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.ToBaseUnits(0.9999999m, 6), Is.EqualTo(new BigInteger(999999)));
                Assert.That(SolanaAmount.ToBaseUnits(1.9999999999m, 9), Is.EqualTo(new BigInteger(1999999999)));
            });
        }

        [Test]
        public void ToBaseUnitsRoundTripsWithFromBaseUnits()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.ToBaseUnits(SolanaAmount.FromBaseUnits("40000000", 6), 6),
                    Is.EqualTo(new BigInteger(40000000)));
                Assert.That(SolanaAmount.ToBaseUnits(SolanaAmount.FromBaseUnits("1234567890", 9), 9),
                    Is.EqualTo(new BigInteger(1234567890)));
            });
        }

        [Test]
        public void ToBaseUnitsHandlesZeroAndZeroDecimals()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaAmount.ToBaseUnits(0m, 9), Is.EqualTo(BigInteger.Zero));
                Assert.That(SolanaAmount.ToBaseUnits(42m, 0), Is.EqualTo(new BigInteger(42)));
            });
        }

        /// <summary>
        /// A u64-sized SOL balance must survive the conversion. decimal carries 28 significant
        /// digits, so 18446744073.709551615 SOL scales back without loss.
        /// </summary>
        [Test]
        public void ToBaseUnitsHandlesMaximumUnsignedLong()
        {
            Assert.That(SolanaAmount.ToBaseUnits(18446744073.709551615m, 9),
                Is.EqualTo(BigInteger.Parse("18446744073709551615")));
        }

        [Test]
        public void ToBaseUnitsRejectsNegativeDecimals()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaAmount.ToBaseUnits(1m, -1));
        }

        [Test]
        public void ToBaseUnitsRejectsNegativeAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaAmount.ToBaseUnits(-1m, 9));
        }
    }
}
