using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaUriTests
    {
        private const string Address = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

        /// <summary>
        /// The form the app emits itself, from the balances page, the token detail page and
        /// both key detail pages. Before this existed, scanning the app's own QR code fell
        /// through to "Unable to read QR code".
        /// </summary>
        [Test]
        public void ParsesTheUriTheAppEmits()
        {
            Assert.That(SolanaUri.TryParseRecipient($"solana:{Address}"), Is.EqualTo(Address));
        }

        [Test]
        public void ParsesABareAddress()
        {
            Assert.That(SolanaUri.TryParseRecipient(Address), Is.EqualTo(Address));
        }

        /// <summary>
        /// Solana Pay puts amount and token in the query. Those are deliberately discarded —
        /// honouring spl-token would mean resolving an arbitrary mint against the whitelist
        /// and deciding what to do when it is absent. The recipient is still usable.
        /// </summary>
        [Test]
        public void DiscardsSolanaPayParameters()
        {
            Assert.That(
                SolanaUri.TryParseRecipient($"solana:{Address}?amount=1&spl-token=xyz&label=Shop"),
                Is.EqualTo(Address));
        }

        /// <summary>
        /// URI schemes are case-insensitive, and QR encoders sometimes uppercase to reach a
        /// denser encoding mode.
        /// </summary>
        [TestCase("SOLANA:")]
        [TestCase("Solana:")]
        public void SchemeIsCaseInsensitive(string scheme)
        {
            Assert.That(SolanaUri.TryParseRecipient($"{scheme}{Address}"), Is.EqualTo(Address));
        }

        /// <summary>
        /// A Substrate address is valid base58 of the wrong length. Accepting one would aim a
        /// Solana transfer at an address that does not exist on the network.
        /// </summary>
        [TestCase("substrate:5GrwvaEF5zXb26Fz9rcQpDWS57CtERHpNehXCPcNoHGKutQY")]
        [TestCase("5GrwvaEF5zXb26Fz9rcQpDWS57CtERHpNehXCPcNoHGKutQY")]
        public void RejectsSubstrateAddresses(string scanned)
        {
            Assert.That(SolanaUri.TryParseRecipient(scanned), Is.Null);
        }

        [TestCase("solana:not-an-address")]
        [TestCase("plutonication:wss://example.com")]
        [TestCase("https://example.com")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void RejectsAnythingElse(string? scanned)
        {
            Assert.That(SolanaUri.TryParseRecipient(scanned), Is.Null);
        }

        /// <summary>
        /// The scheme with nothing after it is not a recipient.
        /// </summary>
        [Test]
        public void RejectsAnEmptySchemeBody()
        {
            Assert.That(SolanaUri.TryParseRecipient("solana:"), Is.Null);
        }

        [Test]
        public void TrimsSurroundingWhitespace()
        {
            Assert.That(SolanaUri.TryParseRecipient($"  solana:{Address}  "), Is.EqualTo(Address));
        }
    }
}
