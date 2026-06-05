using Substrate.NetApi.Model.Extrinsics;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;

namespace UniqueryPlusTests
{
    internal class XcavateIndexerTests
    {
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
    }
}
