using System.Text.Json;
using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

namespace PlutoFrameworkTests
{
    public class WalletLinkDataTests
    {
        [Test]
        public void SerializesWithSnakeCaseFieldNames()
        {
            var json = JsonSerializer.Serialize(new WalletLinkData
            {
                Nonce = "n",
                Chain = "solana",
                Address = "a",
                Signature = "s",
            });

            Assert.That(json, Is.EqualTo(
                "{\"nonce\":\"n\",\"chain\":\"solana\",\"address\":\"a\",\"signature\":\"s\"}"));
        }

        /// <summary>
        /// Polkadot links carry no signature. The server treats the field as
        /// Solana-only, so a null must disappear rather than serialize as
        /// <c>"signature":null</c>.
        /// </summary>
        [Test]
        public void OmitsNullSignature()
        {
            var json = JsonSerializer.Serialize(new WalletLinkData
            {
                Nonce = "n",
                Chain = "polkadot",
                Address = "a",
            });

            Assert.That(json, Does.Not.Contain("signature"));
        }

        [Test]
        public void UnlinkDataCarriesOnlyChainAndAddress()
        {
            var json = JsonSerializer.Serialize(new WalletUnlinkData
            {
                Chain = "solana",
                Address = "a",
            });

            Assert.That(json, Is.EqualTo("{\"chain\":\"solana\",\"address\":\"a\"}"));
        }
    }
}
