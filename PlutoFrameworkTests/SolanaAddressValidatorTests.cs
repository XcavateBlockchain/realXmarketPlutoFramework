using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaAddressValidatorTests
    {
        /// <summary>
        /// The mints the app already ships, so these are known-good 32-byte addresses.
        /// </summary>
        [TestCase("So11111111111111111111111111111111111111112")]
        [TestCase("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")]
        [TestCase("4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU")]
        public void AcceptsRealSolanaAddresses(string address)
        {
            Assert.That(SolanaAddressValidator.IsValidAddress(address), Is.True);
        }

        /// <summary>
        /// The Substrate transfer popup validates with <c>Length == 48</c>. An SS58 address is
        /// exactly that length and is entirely base58, so a length rule would wave it through
        /// and send funds to an address that does not exist on Solana.
        /// </summary>
        [Test]
        public void RejectsSubstrateAddress()
        {
            Assert.That(
                SolanaAddressValidator.IsValidAddress("5GrwvaEF5zXb26Fz9rcQpDWS57CtERHpNehXCPcNoHGKutQY"),
                Is.False);
        }

        /// <summary>
        /// Valid base58, wrong length. A character-set check alone would accept these.
        /// </summary>
        [TestCase("2g")]
        [TestCase("11111111111111111111111111111111111111111111111111111111111111111")]
        public void RejectsWrongByteLength(string address)
        {
            Assert.That(SolanaAddressValidator.IsValidAddress(address), Is.False);
        }

        /// <summary>
        /// 0, O, I and l are excluded from the alphabet. Decoding throws on them, and a
        /// pasted address is untrusted input — the validator must answer, not throw.
        /// </summary>
        [TestCase("So1111111111111111111111111111111111111111O")]
        [TestCase("So11111111111111111111111111111111111111l12")]
        [TestCase("not an address")]
        public void RejectsNonBase58WithoutThrowing(string address)
        {
            Assert.That(SolanaAddressValidator.IsValidAddress(address), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void RejectsMissingAddress(string? address)
        {
            Assert.That(SolanaAddressValidator.IsValidAddress(address), Is.False);
        }
    }
}
