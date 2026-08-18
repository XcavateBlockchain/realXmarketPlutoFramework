using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaAddressTests
    {
        private const string TestMnemonics =
            "lens scheme misery search address destroy shallow police picture gown apart rural cotton vivid cage disagree enrich govern history kit early near cloth alarm";

        /// <summary>
        /// Externally anchored: sollet.io's published output for the mnemonic above,
        /// the same value asserted in <c>SolanaMnemonics</c>.
        /// </summary>
        private const string KnownBase58Address = "ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL";

        /// <summary>
        /// Mobile Wallet Adapter carries account addresses base64-encoded, while every
        /// Solana UI shows base58. Getting this backwards yields a plausible-looking but
        /// completely wrong address, so it is pinned against a known-good vector.
        /// </summary>
        [Test]
        public void FromBase64ConvertsWireAddressToBase58()
        {
            var publicKeyBytes = SolanaMnemonicsModel.GetAccountFromMnemonics(TestMnemonics).PublicKey.KeyBytes;
            var wireAddress = Convert.ToBase64String(publicKeyBytes);

            Assert.That(SolanaAddress.FromBase64(wireAddress), Is.EqualTo(KnownBase58Address));
        }

        [Test]
        public void FromBase64IsNotAPassThrough()
        {
            var publicKeyBytes = SolanaMnemonicsModel.GetAccountFromMnemonics(TestMnemonics).PublicKey.KeyBytes;
            var wireAddress = Convert.ToBase64String(publicKeyBytes);

            Assert.That(SolanaAddress.FromBase64(wireAddress), Is.Not.EqualTo(wireAddress));
        }

        [Test]
        public void ToBase64RoundTripsFromBase58()
        {
            var wireAddress = SolanaAddress.ToBase64(KnownBase58Address);

            Assert.That(SolanaAddress.FromBase64(wireAddress), Is.EqualTo(KnownBase58Address));
        }

        [Test]
        public void FromBase64RejectsWrongLengthKey()
        {
            // A 16-byte value is valid base64 but not a 32-byte Ed25519 public key.
            var tooShort = Convert.ToBase64String(new byte[16]);

            Assert.Throws<FormatException>(() => SolanaAddress.FromBase64(tooShort));
        }

        [Test]
        public void FromBase64RejectsMalformedBase64()
        {
            Assert.Throws<FormatException>(() => SolanaAddress.FromBase64("not valid base64 !!!"));
        }
    }
}
