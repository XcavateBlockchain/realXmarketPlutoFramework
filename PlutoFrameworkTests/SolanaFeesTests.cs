using System.Numerics;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaFeesTests
    {
        /// <summary>
        /// An SPL transfer spends no SPL on fees — the fee comes out of SOL. Reserving here
        /// would strand tokens the user asked to send in full.
        /// </summary>
        [Test]
        public void SplMaxIsTheWholeBalance()
        {
            Assert.That(SolanaFees.MaxSendable(new BigInteger(40_000_000), isNative: false),
                Is.EqualTo(new BigInteger(40_000_000)));
        }

        /// <summary>
        /// A SOL send must leave enough behind to pay for itself. Filling the entire balance
        /// builds a transaction that cannot cover its own signature fee.
        /// </summary>
        [Test]
        public void SolMaxHoldsBackTheReserve()
        {
            Assert.That(SolanaFees.MaxSendable(new BigInteger(2_000_000), isNative: true),
                Is.EqualTo(new BigInteger(2_000_000 - 1_000_000)));
        }

        /// <summary>
        /// A balance smaller than the reserve must floor at zero, never go negative — a
        /// negative amount would reach ToBaseUnits and throw in the middle of a tap handler.
        /// </summary>
        [TestCase(500_000)]
        [TestCase(1_000_000)]
        [TestCase(0)]
        public void SolMaxFloorsAtZero(int lamports)
        {
            Assert.That(SolanaFees.MaxSendable(new BigInteger(lamports), isNative: true),
                Is.EqualTo(BigInteger.Zero));
        }

        [Test]
        public void SplMaxOfNothingIsNothing()
        {
            Assert.That(SolanaFees.MaxSendable(BigInteger.Zero, isNative: false),
                Is.EqualTo(BigInteger.Zero));
        }

        /// <summary>
        /// The reserve exists so a Max SOL send can pay for itself with room to spare. It is
        /// deliberately NOT sized to cover associated-token-account rent (~0.00204 SOL): the
        /// user asked to send SOL, and withholding twice as much again to fund a hypothetical
        /// future SPL transfer would silently under-send.
        /// </summary>
        [Test]
        public void ReserveComfortablyCoversTheSignatureFee()
        {
            Assert.That(SolanaFees.MaxReserveLamports,
                Is.GreaterThanOrEqualTo(SolanaFees.LamportsPerSignature * 100));
        }
    }
}
