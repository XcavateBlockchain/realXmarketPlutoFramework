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

        /// <summary>
        /// The refund router sends withdraw_cancelled only to a Cancelled listing - the
        /// program rejects it for every other status with ListingNotActive (6013). A
        /// Refunding listing (the legal deadline blew out and exits already started)
        /// still belongs to withdraw_legal_process_expired, so it must not read as
        /// cancelled here. This is the regression pin for the claim-phase refund that
        /// used to route on IsTornDown (Cancelled OR Refunding).
        /// </summary>
        [Test]
        [TestCase("Cancelled", true)]
        [TestCase("Refunding", false)]
        [TestCase("SoldOut", false)]
        [TestCase("Legal", false)]
        [TestCase("Expired", false)]
        [TestCase("Listed", false)]
        public void IsCancelled_TrueOnlyForTheCancelledStatus(string status, bool expected)
        {
            var listing = ListingWithClaimDeadline(0) with { ListingStatus = status };

            Assert.That(listing.IsCancelled, Is.EqualTo(expected));
        }

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
