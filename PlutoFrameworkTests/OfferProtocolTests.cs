using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Xcavate.Offers;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The realXmarket offer protocol's payload shapes, pinned against the messenger
    /// dashboard's own builders (app/services/buckets/realxmarket.ts): the app seeds
    /// these messages and the dashboard parses them back, so any field-level drift
    /// reads as an unreadable message rather than an error.
    /// </summary>
    public class OfferProtocolTests
    {
        [Test]
        public void BuildOfferPayload_MatchesTheDashboardContractByteForByte()
        {
            var payload = OfferProtocol.BuildOfferPayload(
                shareListingId: 7, assetId: 12, sharePriceQuote: 123_000_000_000UL, amount: 5, from: "buyer-wallet");

            Assert.That(payload, Is.EqualTo(
                """{"kind":"offer","version":1,"shareListingId":"7","assetId":"12","pricePerToken":"123000000000","amount":5,"counterOf":null,"from":"buyer-wallet"}"""));
        }

        [Test]
        public void BuildPurchasedStatusPayload_CarriesTheTransactionWhenKnown()
        {
            var payload = OfferProtocol.BuildPurchasedStatusPayload(
                shareListingId: 7, assetId: 12, from: "buyer-wallet", tx: "signature-abc");

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.Multiple(() =>
            {
                Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("status"));
                Assert.That(root.GetProperty("version").GetInt32(), Is.EqualTo(1));
                Assert.That(root.GetProperty("shareListingId").GetString(), Is.EqualTo("7"));
                Assert.That(root.GetProperty("assetId").GetString(), Is.EqualTo("12"));
                Assert.That(root.GetProperty("counterOf").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(root.GetProperty("from").GetString(), Is.EqualTo("buyer-wallet"));
                Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("purchased"));
                Assert.That(root.GetProperty("tx").GetString(), Is.EqualTo("signature-abc"));
            });
        }

        [Test]
        public void BuildPurchasedStatusPayload_OmitsTxWhenUnknown()
        {
            var payload = OfferProtocol.BuildPurchasedStatusPayload(
                shareListingId: 7, assetId: 12, from: "buyer-wallet", tx: null);

            using var document = JsonDocument.Parse(payload);

            Assert.That(document.RootElement.TryGetProperty("tx", out _), Is.False);
        }

        [Test]
        public void BuildKeySharingPayload_HasTheShapeTheDashboardParses()
        {
            var (publicKeyHex, secretJwk) = OfferProtocol.GenerateBucketKeyPair();

            var payload = OfferProtocol.BuildKeySharingPayload("key-id-1", "buyer-wallet", "seller-wallet", secretJwk);

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.Multiple(() =>
            {
                Assert.That(root.GetProperty("id").GetString(), Is.EqualTo("key-id-1"));
                Assert.That(root.GetProperty("from").GetString(), Is.EqualTo("buyer-wallet"));
                Assert.That(root.GetProperty("to").EnumerateArray().Select(x => x.GetString()).ToArray(),
                    Is.EqualTo(new[] { "buyer-wallet", "seller-wallet" }));

                var key = root.GetProperty("keys").EnumerateArray().Single();
                Assert.That(key.GetProperty("kty").GetString(), Is.EqualTo("OKP"));
                Assert.That(key.GetProperty("crv").GetString(), Is.EqualTo("X25519"));
                Assert.That(WebEncoders.Base64UrlDecode(key.GetProperty("x").GetString()!), Has.Length.EqualTo(32));
                Assert.That(WebEncoders.Base64UrlDecode(key.GetProperty("d").GetString()!), Has.Length.EqualTo(32));
                // The JWK's public half must be the bucket key the API is told about.
                Assert.That(
                    Convert.ToHexString(WebEncoders.Base64UrlDecode(key.GetProperty("x").GetString()!)).ToLowerInvariant(),
                    Is.EqualTo(publicKeyHex));
            });
        }

        [Test]
        public void GenerateBucketKeyPair_PublicKeyIs64LowercaseHex_AndMatchesTheSecret()
        {
            var (publicKeyHex, secretJwk) = OfferProtocol.GenerateBucketKeyPair();

            Assert.Multiple(() =>
            {
                Assert.That(publicKeyHex, Has.Length.EqualTo(64));
                Assert.That(publicKeyHex, Is.EqualTo(publicKeyHex.ToLowerInvariant()));
                Assert.That(publicKeyHex.All(Uri.IsHexDigit), Is.True);

                var secret = WebEncoders.Base64UrlDecode(secretJwk["d"]!.GetValue<string>());
                var derivedPublic = X25519Model.DerivePublicKey(secret);
                Assert.That(Convert.ToHexString(derivedPublic).ToLowerInvariant(), Is.EqualTo(publicKeyHex));
            });
        }

        [Test]
        public void GenerateBucketKeyPair_GeneratesFreshKeys()
        {
            var (first, _) = OfferProtocol.GenerateBucketKeyPair();
            var (second, _) = OfferProtocol.GenerateBucketKeyPair();

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void ContentHash_IsSha256As0xLowercaseHex()
        {
            // The well-known sha256 of "abc", in the dashboard's sha256HexUtf8 shape.
            Assert.That(OfferProtocol.ContentHash("abc"), Is.EqualTo(
                "0xba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public void References_FollowTheProtocolFormats()
        {
            Assert.Multiple(() =>
            {
                Assert.That(OfferProtocol.KeySharingReference("key-id-1"), Is.EqualTo("key-sharing:key-id-1"));
                Assert.That(OfferProtocol.OfferReference(7, "buyer-wallet"), Is.EqualTo("offer:7:buyer-wallet"));
            });
        }
    }
}
