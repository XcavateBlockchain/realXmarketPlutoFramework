using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Solana transaction signatures are 64 bytes and are displayed base58, but Solnet's
    /// PublicKey rejects anything that is not 32 bytes and its own base58 encoder is not
    /// public. Hence our own, pinned against Solnet's output for a known key.
    /// </summary>
    public class SolanaBase58Tests
    {
        private const string TestMnemonics =
            "lens scheme misery search address destroy shallow police picture gown apart rural cotton vivid cage disagree enrich govern history kit early near cloth alarm";

        private const string KnownBase58Address = "ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL";

        /// <summary>
        /// The strongest available check: for a 32-byte input our encoder must produce exactly
        /// what Solnet produces, which is itself anchored to sollet.io's published output.
        /// This validates the alphabet and the big-integer division together.
        /// </summary>
        [Test]
        public void AgreesWithSolnetForAKnownPublicKey()
        {
            var keyBytes = SolanaMnemonicsModel.GetAccountFromMnemonics(TestMnemonics).PublicKey.KeyBytes;

            Assert.That(SolanaBase58.Encode(keyBytes), Is.EqualTo(KnownBase58Address));
        }

        /// <summary>
        /// The case that motivated this: a 64-byte signature, which PublicKey cannot hold.
        /// </summary>
        [Test]
        public void EncodesSixtyFourByteSignature()
        {
            var signature = new byte[64];
            Array.Fill(signature, (byte)0xAB);

            var encoded = SolanaBase58.Encode(signature);

            Assert.Multiple(() =>
            {
                Assert.That(encoded, Is.Not.Empty);
                Assert.That(SolanaBase58.Decode(encoded), Is.EqualTo(signature));
            });
        }

        [Test]
        public void RoundTripsArbitraryLengths()
        {
            foreach (var length in new[] { 1, 16, 31, 32, 33, 64, 65 })
            {
                var data = new byte[length];

                for (int i = 0; i < length; i++)
                {
                    data[i] = (byte)(i * 7 + 1);
                }

                Assert.That(SolanaBase58.Decode(SolanaBase58.Encode(data)), Is.EqualTo(data),
                    $"round trip failed for {length} bytes");
            }
        }

        /// <summary>
        /// Each leading zero byte is one '1' character, not a dropped byte. Getting this wrong
        /// silently shortens any value that begins with zero.
        /// </summary>
        [Test]
        public void PreservesLeadingZeroBytes()
        {
            var data = new byte[] { 0x00, 0x00, 0x01, 0x02 };

            var encoded = SolanaBase58.Encode(data);

            Assert.Multiple(() =>
            {
                Assert.That(encoded, Does.StartWith("11"));
                Assert.That(SolanaBase58.Decode(encoded), Is.EqualTo(data));
            });
        }

        [Test]
        public void EncodesAllZeroesAsRepeatedOnes()
        {
            Assert.That(SolanaBase58.Encode(new byte[4]), Is.EqualTo("1111"));
        }

        [Test]
        public void EncodesEmptyInputAsEmptyString()
        {
            Assert.That(SolanaBase58.Encode([]), Is.EqualTo(""));
        }

        [Test]
        public void UsesNoAmbiguousCharacters()
        {
            var data = new byte[256];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            var encoded = SolanaBase58.Encode(data);

            // 0, O, I and l are excluded from the base58 alphabet.
            Assert.That(encoded, Does.Not.Contain("0").And.Not.Contain("O").And.Not.Contain("I").And.Not.Contain("l"));
        }

        [Test]
        public void DecodeRejectsCharactersOutsideTheAlphabet()
        {
            Assert.Throws<FormatException>(() => SolanaBase58.Decode("abc0def"));
        }
    }
}
