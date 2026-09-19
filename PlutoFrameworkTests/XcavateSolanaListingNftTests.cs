using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The direct-buy window decides whether a purchase goes through reserve_shares or
    /// buy_property_shares - the on-chain program rejects the wrong one for the phase
    /// (ClaimWindowClosed / DirectBuyNotOpen), so the boundary is pinned exactly.
    /// </summary>
    internal class XcavateSolanaListingNftTests
    {
        private static XcavateSolanaListingNft ListingWithClaimDeadline(long claimDeadline) =>
            new()
            {
                Owner = "developer",
                ListingId = 1,
                AssetId = 1,
                ListingExpiryTimestamp = long.MaxValue,
                ClaimDeadlineTimestamp = claimDeadline,
                ListingStatus = "Listed",
                OpenForSale = true,
                IsTornDown = false,
            };

        [Test]
        public void DirectBuyIsOpen_FalseBeforeTheSpvOpensTheWindow()
        {
            // Claim deadline 0: no create_spv yet, purchases are reservations.
            var listing = ListingWithClaimDeadline(0);

            Assert.That(listing.DirectBuyIsOpen(nowUnixSeconds: 1_800_000_000), Is.False);
        }

        [Test]
        public void DirectBuyIsOpen_FalseWhileTheClaimWindowIsOpen()
        {
            var listing = ListingWithClaimDeadline(claimDeadline: 1_800_000_100);

            Assert.That(listing.DirectBuyIsOpen(nowUnixSeconds: 1_800_000_099), Is.False);
        }

        [Test]
        public void DirectBuyIsOpen_TrueFromTheDeadlineOn()
        {
            // The program checks now >= claim_deadline, so the deadline second itself
            // already takes buy_property_shares.
            var listing = ListingWithClaimDeadline(claimDeadline: 1_800_000_100);

            Assert.That(listing.DirectBuyIsOpen(nowUnixSeconds: 1_800_000_100), Is.True);
            Assert.That(listing.DirectBuyIsOpen(nowUnixSeconds: 1_800_000_101), Is.True);
        }
    }
}
