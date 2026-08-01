using PlutoFrameworkCore.PushNotificationServices.Core.Utils;

namespace PlutoFrameworkTests
{
    public class WalletLinkMessageTests
    {
        /// <summary>
        /// The server reconstructs this message from the request fields and verifies the
        /// signature against its own copy, so a single wrong byte fails verification.
        /// Pinned against the literal format in the API's docs/api-reference.md.
        /// </summary>
        [Test]
        public void BuildsTheDocumentedFormatByteForByte()
        {
            var message = WalletLinkMessage.Build(
                chain: "solana",
                address: "ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL",
                nonce: "A1PJjlqu8-KFVl36A5XlGAcbUEhOA2VITj30N8XPRmA",
                deviceId: "3f2504e0-4f89-11d3-9a0c-0305e82c3301");

            Assert.That(message, Is.EqualTo(
                "PlutoFramework wallet link\n" +
                "chain: solana\n" +
                "address: ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL\n" +
                "nonce: A1PJjlqu8-KFVl36A5XlGAcbUEhOA2VITj30N8XPRmA\n" +
                "device: 3f2504e0-4f89-11d3-9a0c-0305e82c3301"));
        }

        [Test]
        public void UsesLfSeparatorsNeverCrLf()
        {
            var message = WalletLinkMessage.Build("polkadot", "addr", "nonce", "device");

            Assert.That(message, Does.Not.Contain("\r"));
        }

        [Test]
        public void HasNoTrailingNewline()
        {
            var message = WalletLinkMessage.Build("polkadot", "addr", "nonce", "device");

            Assert.That(message, Does.Not.EndWith("\n"));
        }

        /// <summary>
        /// The nonce arrives as unpadded URL-safe base64 and the docs say to use it
        /// verbatim - decoding or re-encoding it would sign different bytes.
        /// </summary>
        [Test]
        public void UsesTheNonceVerbatim()
        {
            var nonce = "url-safe_base64-with-dash_and_underscore";

            var message = WalletLinkMessage.Build("solana", "addr", nonce, "device");

            Assert.That(message, Does.Contain($"nonce: {nonce}"));
        }
    }
}
