using PlutoFramework.Model.Xcavate;
using UniqueryPlus.Metadata;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The listing-feed tests hit the live Xcavate devnet indexer, which routinely holds
    /// zero listings - they assert the query and mapping succeed, not that anything is
    /// listed. The filter tests are pure.
    /// </summary>
    internal class XcavateMarketplaceIndexerModelTests
    {
        [Test]
        public async Task GetMarketplaceListedPropertiesAsync_QueriesAndMapsWithoutErrorsAsync()
        {
            var properties = await XcavateMarketplaceIndexerModel.GetMarketplaceListedPropertiesAsync(
                first: 20,
                offset: 0,
                CancellationToken.None);

            Assert.That(properties, Is.Not.Null);

            // Whatever is listed on devnet right now must come out renderable: the
            // marketplace views need these to be non-null.
            foreach (var property in properties)
            {
                Assert.That(property.XcavateMetadata, Is.Not.Null);
                Assert.That(property.OngoingObjectListingDetails, Is.Not.Null);
                Assert.That(property.Metadata, Is.Not.Null);

                Console.WriteLine($"{property.ListingId}: {property.XcavateMetadata!.PropertyName} ({property.ListingStatus})");
            }
        }

        [Test]
        public async Task GetListingFullInfoAsync_ReturnsNullForUnknownListingAsync()
        {
            // No deployment will ever mint this listing id, so null is a real "not found"
            // answer rather than a lookup that silently failed.
            var listing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                long.MaxValue,
                investor: null,
                CancellationToken.None);

            Assert.That(listing, Is.Null);
        }

        [Test]
        [TestCase("", "", "", true)]
        [TestCase("", "", "ab1", true)]
        [TestCase("", "", "AB1 2CD", true)]
        [TestCase("", "", "property", true)]
        [TestCase("", "", "Manchester", false)]
        // The chain knows no town or property type, so any concrete selection excludes -
        // the same answer the old server-side filter gave for rows without those fields.
        [TestCase("London", "", "", false)]
        [TestCase("", "Apartment", "", false)]
        public void MatchesFilter_MatchesTheOldIncludesInsensitiveSemantics(
            string includesTownCity,
            string includesPropertyType,
            string includesPropertyName,
            bool expected)
        {
            var nft = new XcavateSolanaListingNft
            {
                Owner = "H7VMSTwha14CzNvuo9GQ1WrhfJhqrHeNwfcrRwuv7iDz",
                ListingId = 0,
                AssetId = 0,
                ListingExpiryTimestamp = 0,
                ClaimDeadlineTimestamp = 0,
                ListingStatus = "Listed",
                OpenForSale = true,
                IsTornDown = false,
                XcavateMetadata = new PropertyMetadata
                {
                    PropertyName = "Property AB1 2CD",
                    Financials = new PropertyFinancials(),
                    Address = new PropertyAddress
                    {
                        PostCode = "AB1 2CD",
                    },
                },
            };

            var matches = XcavateMarketplaceIndexerModel.MatchesFilter(
                nft,
                includesTownCity,
                includesPropertyType,
                includesPropertyName);

            Assert.That(matches, Is.EqualTo(expected));
        }
    }
}
