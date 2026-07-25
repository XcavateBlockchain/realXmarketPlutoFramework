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
    }
}
