using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using Substrate.NetApi.Model.Types.Primitive;
using UniqueryPlus.Nfts;

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
        [TestCase("Listed", XcavateRefundInstruction.Expired)]
        [TestCase("Expired", XcavateRefundInstruction.Expired)]
        [TestCase("Cancelled", XcavateRefundInstruction.Cancelled)]
        [TestCase("SoldOut", XcavateRefundInstruction.LegalProcessExpired)]
        [TestCase("Legal", XcavateRefundInstruction.LegalProcessExpired)]
        [TestCase("Refunding", XcavateRefundInstruction.LegalProcessExpired)]
        public void RefundInstruction_MatchesTheProgramsAcceptedListingStatus(
            string status,
            XcavateRefundInstruction expected)
        {
            var listing = ListingWithClaimDeadline(0) with { ListingStatus = status };

            Assert.That(listing.RefundInstruction, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("PendingAssets")]
        [TestCase("Finalized")]
        public void RefundInstruction_RejectsAStatusWithoutAnInvestorRefund(string status)
        {
            var listing = ListingWithClaimDeadline(0) with { ListingStatus = status };

            Assert.Throws<InvalidOperationException>(() => _ = listing.RefundInstruction);
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

        /// <summary>
        /// relist_shares is accepted only on a Finalized primary listing - every other
        /// status is refused on-chain with PropertyNotFinalized (6073) - so the detail
        /// page gates the relist action on this flag instead of submitting a transaction
        /// the program must reject.
        /// </summary>
        [Test]
        [TestCase("Finalized", true)]
        [TestCase("PendingAssets", false)]
        [TestCase("Listed", false)]
        [TestCase("SoldOut", false)]
        [TestCase("Legal", false)]
        [TestCase("Expired", false)]
        [TestCase("Cancelled", false)]
        [TestCase("Refunding", false)]
        public void IsFinalized_TrueOnlyForTheFinalizedStatus(string status, bool expected)
        {
            var listing = ListingWithClaimDeadline(0) with { ListingStatus = status };

            Assert.That(listing.IsFinalized, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(0u, false)]
        [TestCase(1u, true)]
        public void Status_LegalProcessExpiredOverridesOtherTerminalStates(
            uint unclaimedTokens,
            bool claimHasExpired)
        {
            var listing = ListingWithClaimDeadline(0) with
            {
                ListingStatus = "Legal",
                LegalDeadlineTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
                OngoingObjectListingDetails = ListingDetails(unclaimedTokens),
            };
            var wrapper = new XcavateNftWrapper
            {
                NftBase = listing,
                Endpoint = PlutoFramework.Constants.Endpoints.GetEndpointDictionary[
                    PlutoFramework.Constants.EndpointEnum.XcavatePaseo],
                TokensBought = 0,
                TokensOwned = 0,
                Region = null,
                ListingHasExpired = false,
                ClaimHasExpired = claimHasExpired,
                SpvCreated = true,
            };

            Assert.That(wrapper.Status, Is.EqualTo("Legal process expired"));
        }

        [Test]
        [TestCase("SoldOut", 100L, 99L, false)]
        [TestCase("SoldOut", 100L, 100L, false)]
        [TestCase("SoldOut", 100L, 101L, true)]
        [TestCase("Legal", 100L, 101L, true)]
        [TestCase("Refunding", 0L, 101L, true)]
        [TestCase("Finalized", 100L, 101L, false)]
        [TestCase("Cancelled", 100L, 101L, false)]
        [TestCase("Listed", 100L, 101L, false)]
        [TestCase("SoldOut", 0L, 101L, false)]
        public void LegalProcessHasExpired_MatchesTheProgramsLegalExitStates(
            string status,
            long legalDeadline,
            long now,
            bool expected)
        {
            var listing = ListingWithClaimDeadline(0) with
            {
                ListingStatus = status,
                LegalDeadlineTimestamp = legalDeadline,
            };

            Assert.That(listing.LegalProcessHasExpired(now), Is.EqualTo(expected));
        }

        private static XcavateOngoingObjectListingDetails ListingDetails(uint unclaimedTokens) => new()
        {
            RealEstateDeveloper = "developer",
            TaxPaidByDeveloper = false,
            ListingExpiry = 0,
            ClaimExpiry = null,
            ListedTokens = 0,
            UnclaimedTokens = unclaimedTokens,
            AssetId = new U32(1),
            CollectionId = new U32(0),
            ItemId = new U32(1),
            ShareOwners = [],
        };
    }
}
