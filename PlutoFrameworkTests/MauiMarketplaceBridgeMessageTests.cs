using PlutoFrameworkCore.Xcavate.Offers;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The mauiMarketplace bridge JSON contract (the dashboard's
    /// app/services/injection/mauiMarketplaceBridge.ts): what the page posts is what
    /// the native buy/reprice flows act on, so the parsing is pinned field by field -
    /// and so is the quiet refusal of anything malformed.
    /// </summary>
    public class MauiMarketplaceBridgeMessageTests
    {
        [Test]
        public void TryParse_BuyRelisted_WithBucketIds()
        {
            const string json = """
                {"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"seller-wallet","propertyName":"Sunset Villa","namespaceId":"42","bucketId":"43"}
                """;

            Assert.That(MauiMarketplaceBridgeMessage.TryParse(json, out var message), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(message!.Kind, Is.EqualTo(MauiMarketplaceBridgeMessageKind.BuyRelisted));
                Assert.That(message.ShareListingId, Is.EqualTo(7));
                Assert.That(message.AssetId, Is.EqualTo(12));
                Assert.That(message.Amount, Is.EqualTo(5u));
                Assert.That(message.SharePriceQuote, Is.EqualTo(123_000_000_000UL));
                Assert.That(message.Seller, Is.EqualTo("seller-wallet"));
                Assert.That(message.PropertyName, Is.EqualTo("Sunset Villa"));
                Assert.That(message.NamespaceId, Is.EqualTo(42L));
                Assert.That(message.BucketId, Is.EqualTo(43L));
            });
        }

        [Test]
        public void TryParse_BuyRelisted_WithoutOptionalFields()
        {
            const string json = """
                {"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"seller-wallet","propertyName":""}
                """;

            Assert.That(MauiMarketplaceBridgeMessage.TryParse(json, out var message), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(message!.PropertyName, Is.Null);
                Assert.That(message.NamespaceId, Is.Null);
                Assert.That(message.BucketId, Is.Null);
            });
        }

        [Test]
        public void TryParse_DelistAndRelist()
        {
            const string json = """
                {"type":"delistAndRelist","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"seller-wallet"}
                """;

            Assert.That(MauiMarketplaceBridgeMessage.TryParse(json, out var message), Is.True);
            Assert.That(message!.Kind, Is.EqualTo(MauiMarketplaceBridgeMessageKind.DelistAndRelist));
        }

        [Test]
        // Ids ride as numbers just as well: a future sender is not pinned to strings.
        [TestCase("""{"type":"buyRelisted","shareListingId":7,"assetId":12,"amount":5,"pricePerToken":123000000000,"seller":"s"}""", true)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":"5","pricePerToken":"123000000000","seller":"s"}""", true)]
        // Malformed, unknown or incomplete payloads are refused quietly.
        [TestCase("not json", false)]
        [TestCase("""{"type":"somethingElse","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","amount":5,"pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":0,"pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":5,"seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"123000000000"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"7","assetId":"12","amount":5,"pricePerToken":"abc","seller":"s"}""", false)]
        [TestCase("""{"type":"buyRelisted","shareListingId":"-7","assetId":"12","amount":5,"pricePerToken":"123000000000","seller":"s"}""", false)]
        [TestCase("""[1,2,3]""", false)]
        public void TryParse_ValidatesTheContract(string json, bool expected)
        {
            Assert.That(MauiMarketplaceBridgeMessage.TryParse(json, out _), Is.EqualTo(expected));
        }
    }
}
