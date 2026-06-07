using Substrate.NetApi.Model.Extrinsics;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;

namespace UniqueryPlusTests
{
    internal class XcavateIndexerTests
    {
        private const string OwnerAddress = "14XAmaujtAthi7KdWsJrKh1QEjiNXwabW1YdYUCbAM6TeGk";

        private static SubstrateClientExt CreateXcavateClient()
        {
            return new SubstrateClientExt(new Uri("wss://xcavate-paseo.api.onfinality.io/public-ws"), ChargeTransactionPayment.Default());
        }

        [Test]
        public async Task GetMarketplaceListedPropertiesAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(client, first: 5);

            Assert.That(results, Is.Not.Null);

            if (results.Count > 0)
            {
                var nft = results[0];
                Assert.That(nft.XcavateMetadata, Is.Not.Null);
                Assert.That(nft.OngoingObjectListingDetails, Is.Not.Null);
            }
        }

        [Test]
        public async Task GetMarketplaceListedPropertiesWithFiltersAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(
                client,
                first: 5,
                includesTownCity: "lon",
                includesPropertyType: "",
                includesPropertyName: "");

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedAndBoughtPropertiesAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(client, first: 5, tokenOwner: OwnerAddress);

            Assert.That(results, Is.Not.Null);

            if (results.Count > 0)
            {
                var nft = results[0];
                Assert.That(nft.XcavateMetadata, Is.Not.Null);
                Assert.That(nft.OngoingObjectListingDetails, Is.Not.Null);
            }
        }

        [Test]
        public async Task GetOwnedAndBoughtPropertiesUsesDefaultOwnerAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(client, first: 5);

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedAndBoughtPropertiesWithFilterAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesWithFilterAsync(
                client,
                first: 5,
                includesTownCity: "lon",
                includesPropertyType: "",
                includesPropertyName: "");

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedPropertiesAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetOwnedPropertiesAsync(client, first: 5, tokenOwner: OwnerAddress);

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetBoughtPropertiesAsync()
        {
            var client = CreateXcavateClient();

            var results = await XcavateIndexerModel.GetBoughtPropertiesAsync(client, first: 5, tokenOwner: OwnerAddress);

            Assert.That(results, Is.Not.Null);
        }
    }
}
