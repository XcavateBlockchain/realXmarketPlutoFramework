using PlutoFramework.Model.Xcavate;
using XcavateDevnetIndexer;
using DeveloperPropertyFilter = PlutoFramework.Model.Xcavate.XcavateMarketplaceIndexerModel.DeveloperPropertyFilter;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The developer page's lifecycle filters, mapped onto the on-chain
    /// <see cref="ListingStatus"/> values the indexer answers them with.
    /// </summary>
    public class DeveloperPropertyFilterTests
    {
        [Test]
        public void ToDeveloperStatuses_AllLeavesTheFilterOff()
        {
            // An absent argument, not an empty list: an empty list would match nothing and
            // the "All" tab would come back empty.
            Assert.That(
                XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.All),
                Is.Null);
        }

        [Test]
        public void ToDeveloperStatuses_PendingIsTheNotYetLiveState()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.Pending),
                Is.EquivalentTo(new[] { ListingStatus.PendingAssets }));
        }

        [Test]
        public void ToDeveloperStatuses_ForSaleIsTheListedState()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.ForSale),
                Is.EquivalentTo(new[] { ListingStatus.Listed }));
        }

        [Test]
        public void ToDeveloperStatuses_SoldMergesTheThreeCompletingStates()
        {
            // The "Properties sold" figure spans all three, which is why the query takes a
            // list of statuses rather than the singular `status` argument.
            Assert.That(
                XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.Sold),
                Is.EquivalentTo(new[] { ListingStatus.SoldOut, ListingStatus.Legal, ListingStatus.Finalized }));
        }

        [Test]
        public void ToDeveloperStatuses_EndedMergesTheThreeFailedStates()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.Ended),
                Is.EquivalentTo(new[] { ListingStatus.Expired, ListingStatus.Cancelled, ListingStatus.Refunding }));
        }

        [Test]
        public void ToDeveloperStatuses_SoldAndEndedDoNotOverlap()
        {
            var sold = XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.Sold)!;
            var ended = XcavateMarketplaceIndexerModel.ToDeveloperStatuses(DeveloperPropertyFilter.Ended)!;

            Assert.That(sold.Intersect(ended), Is.Empty);
        }

        [Test]
        public void ToDeveloperStatuses_FiltersCoverEveryListingStatus()
        {
            // Together the four narrowed tabs answer every state a listing can be in, so no
            // property becomes invisible outside the "All" tab.
            var covered = new[]
            {
                DeveloperPropertyFilter.Pending,
                DeveloperPropertyFilter.ForSale,
                DeveloperPropertyFilter.Sold,
                DeveloperPropertyFilter.Ended,
            }
                .SelectMany(filter => XcavateMarketplaceIndexerModel.ToDeveloperStatuses(filter) ?? []);

            Assert.That(
                covered.Distinct(),
                Is.EquivalentTo(Enum.GetValues<ListingStatus>()));
        }
    }
}
