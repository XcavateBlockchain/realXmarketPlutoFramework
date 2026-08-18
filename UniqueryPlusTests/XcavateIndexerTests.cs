using UniqueryPlus.Nfts;

namespace UniqueryPlusTests
{
    internal class XcavateIndexerTests
    {
        private const string OwnerAddress = "14XAmaujtAthi7KdWsJrKh1QEjiNXwabW1YdYUCbAM6TeGk";

        [Test]
        public async Task GetMarketplaceListedPropertiesAsync()
        {
            var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(first: 5);

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
            var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(
                first: 5,
                includesTownCity: "lon",
                includesPropertyType: "",
                includesPropertyName: "");

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedAndBoughtPropertiesAsync()
        {
            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(first: 5, tokenOwner: OwnerAddress);

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
            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(first: 5);

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedAndBoughtPropertiesWithFilterAsync()
        {
            var results = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesWithFilterAsync(
                first: 5,
                includesTownCity: "lon",
                includesPropertyType: "",
                includesPropertyName: "");

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetOwnedPropertiesAsync()
        {
            var results = await XcavateIndexerModel.GetOwnedPropertiesAsync(first: 5, tokenOwner: OwnerAddress);

            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task GetBoughtPropertiesAsync()
        {
            var results = await XcavateIndexerModel.GetBoughtPropertiesAsync(first: 5, tokenOwner: OwnerAddress);

            Assert.That(results, Is.Not.Null);
        }
    }
}
