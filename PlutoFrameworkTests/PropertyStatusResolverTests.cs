using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using Substrate.NetApi.Model.Types.Primitive;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;

namespace PlutoFrameworkTests
{
    internal class PropertyStatusResolverTests
    {
        private const long Now = 2_000_000;
        private const string Viewer = "developer";
        private const string Developer = "developer";
        private const string OtherLawyer = "other-lawyer";
        private const string OtherAgent = "other-agent";
        private const string Vacant = XcavateMarketplaceIndexerModel.VacantSeatPubkey;

        private static XcavateSolanaListingNft Listing(
            string status = "Listed",
            uint listedTokens = 10,
            uint unclaimedTokens = 0) => new()
        {
            Owner = Developer,
            ListingId = 1,
            AssetId = 1,
            ListingExpiryTimestamp = Now + 100,
            ClaimDeadlineTimestamp = 0,
            LegalDeadlineTimestamp = Now + 100,
            ListingStatus = status,
            OpenForSale = status is not ("PendingAssets" or "Cancelled" or "Refunding"),
            IsTornDown = status is "Cancelled" or "Refunding",
            DeveloperLawyer = Vacant,
            SpvLawyer = Vacant,
            OngoingObjectListingDetails = new XcavateOngoingObjectListingDetails
            {
                RealEstateDeveloper = Developer,
                TaxPaidByDeveloper = false,
                ListingExpiry = 0,
                ClaimExpiry = null,
                ListedTokens = listedTokens,
                UnclaimedTokens = unclaimedTokens,
                AssetId = new U32(1),
                CollectionId = new U32(0),
                ItemId = new U32(1),
                ShareOwners = [],
            },
        };

        private static XcavateNftWrapper Wrapper(
            MainPageRole role,
            XcavateSolanaListingNft listing,
            PropertyRoleContext? context = null,
            uint tokensBought = 0,
            uint tokensOwned = 0,
            bool spvCreated = true) => new()
        {
            NftBase = listing,
            Endpoint = PlutoFramework.Constants.Endpoints.GetEndpointDictionary[
                PlutoFramework.Constants.EndpointEnum.XcavatePaseo],
            TokensBought = tokensBought,
            TokensOwned = tokensOwned,
            Region = null,
            ListingHasExpired = listing.ListingExpiryTimestamp <= Now,
            ClaimHasExpired = listing.ClaimDeadlineTimestamp != 0
                && listing.ClaimDeadlineTimestamp <= Now,
            TimeLeftToBuy = TimeSpan.FromHours(2),
            TimeLeftToClaim = TimeSpan.FromHours(2),
            SpvCreated = spvCreated,
            ActiveRole = role,
            ViewerAddress = Viewer,
            RoleContext = context,
        };

        private static PropertyRoleContext Context() => new() { ListingId = 1 };

        private static string? Resolve(
            MainPageRole role,
            XcavateSolanaListingNft listing,
            PropertyRoleContext? context = null,
            uint tokensBought = 0,
            uint tokensOwned = 0,
            bool spvCreated = true,
            long now = Now) => PropertyStatusResolver.Resolve(
                Wrapper(role, listing, context, tokensBought, tokensOwned, spvCreated), now);

        [TestCase("PendingAssets", "Preparing assets")]
        [TestCase("Finalized", "Finalized")]
        [TestCase("Expired", "Listing expired")]
        [TestCase("Cancelled", "Cancelled")]
        [TestCase("Refunding", "Refunding")]
        [TestCase("Legal", "Ready to settle")]
        [TestCase("SoldOut", "Legal review in progress")]
        [TestCase("Listed", "For sale")]
        public void GenericLifecycle_CoversEveryListingStatus(string status, string expected)
        {
            Assert.That(Resolve(MainPageRole.Lawyer, Listing(status)), Is.EqualTo(expected));
        }

        [Test]
        public void Investor_ActiveSaleAndClaimWindows_UseCountdowns()
        {
            Assert.That(
                Resolve(MainPageRole.Investor, Listing()),
                Is.EqualTo("2 hours to reserve"));

            var claiming = Listing(listedTokens: 0, unclaimedTokens: 4) with
            {
                ClaimDeadlineTimestamp = Now + 100,
            };
            Assert.That(
                Resolve(MainPageRole.Investor, claiming, tokensBought: 4),
                Is.EqualTo("2 hours to claim"));
        }

        [TestCase(1u, 0u, "Can be refunded")]
        [TestCase(0u, 1u, "Can be refunded")]
        [TestCase(0u, 0u, "Claim period expired")]
        public void Investor_ClaimDeadlineItself_IsExpiredAndUsesRealPosition(
            uint bought,
            uint owned,
            string expected)
        {
            var listing = Listing(listedTokens: 0, unclaimedTokens: 4) with
            {
                ClaimDeadlineTimestamp = Now,
            };

            Assert.That(
                Resolve(MainPageRole.Investor, listing, tokensBought: bought, tokensOwned: owned),
                Is.EqualTo(expected));
        }

        [Test]
        public void Investor_ExpiredSaleWithReservation_CanBeRefunded()
        {
            var listing = Listing() with { ListingExpiryTimestamp = Now };

            Assert.That(
                Resolve(MainPageRole.Investor, listing, tokensBought: 2),
                Is.EqualTo("Can be refunded"));
            Assert.That(
                Resolve(MainPageRole.Investor, listing),
                Is.EqualTo("Listing expired"));
        }

        [Test]
        public void Investor_LegalDeadline_ExpiresOnlyAfterTheDeadlineSecond()
        {
            var atDeadline = Listing("SoldOut", listedTokens: 0) with { LegalDeadlineTimestamp = Now };
            Assert.That(
                Resolve(MainPageRole.Investor, atDeadline),
                Is.EqualTo("Sold out"));

            var expired = atDeadline with { LegalDeadlineTimestamp = Now - 1 };
            Assert.That(
                Resolve(MainPageRole.Investor, expired, tokensOwned: 2),
                Is.EqualTo("Legal process expired"));
        }

        [Test]
        public void Investor_FullyCommittedWithoutSpvAndFinalized_AreExplicit()
        {
            Assert.That(
                Resolve(MainPageRole.Investor, Listing(listedTokens: 0), spvCreated: false),
                Is.EqualTo("SPV to be created"));
            Assert.That(
                Resolve(MainPageRole.Investor, Listing("Finalized", listedTokens: 0), tokensOwned: 3),
                Is.EqualTo("Finalized"));
        }

        [TestCase(false, DocumentStatus.Pending, "Developer lawyer: review pending")]
        [TestCase(false, DocumentStatus.Approved, "Developer lawyer: documents approved")]
        [TestCase(false, DocumentStatus.Rejected, "Developer lawyer: documents rejected")]
        [TestCase(true, DocumentStatus.Pending, "SPV lawyer: review pending")]
        [TestCase(true, DocumentStatus.Approved, "SPV lawyer: documents approved")]
        [TestCase(true, DocumentStatus.Rejected, "SPV lawyer: documents rejected")]
        public void Lawyer_AssignedSeat_ShowsSideAndDocumentVerdict(
            bool spv,
            DocumentStatus documentStatus,
            string expected)
        {
            var listing = Listing("SoldOut", listedTokens: 0) with
            {
                DeveloperLawyer = spv ? OtherLawyer : Viewer,
                SpvLawyer = spv ? Viewer : Vacant,
                DeveloperLawyerDocStatus = documentStatus,
                SpvLawyerDocStatus = documentStatus,
            };

            Assert.That(Resolve(MainPageRole.Lawyer, listing), Is.EqualTo(expected));
        }

        [Test]
        public void Lawyer_OwnHistoricalClaim_WinsOverClaimEligibility()
        {
            var context = Context() with
            {
                HasClaimedSpvCase = true,
                ActiveSpvCaseClaimRound = null,
                CanClaimSpvCase = true,
            };

            Assert.That(
                Resolve(MainPageRole.Lawyer, Listing("SoldOut", listedTokens: 0), context),
                Is.EqualTo("Already claimed"));
        }

        [Test]
        public void Lawyer_EligibleWithoutOwnClaim_CanBeClaimed()
        {
            var context = Context() with { CanClaimSpvCase = true };

            Assert.That(
                Resolve(MainPageRole.Lawyer, Listing("SoldOut", listedTokens: 0), context),
                Is.EqualTo("Can be claimed"));
        }

        [Test]
        public void Lawyer_MissingContext_DoesNotInventClaimOrEligibility()
        {
            var listing = Listing("SoldOut", listedTokens: 0) with { SpvLawyer = OtherLawyer };
            Assert.That(
                Resolve(MainPageRole.Lawyer, listing),
                Is.EqualTo("SPV lawyer assigned"));

            listing = listing with { SpvLawyer = Vacant };
            Assert.That(
                Resolve(MainPageRole.Lawyer, listing),
                Is.EqualTo("Legal review in progress"));
        }

        [Test]
        public void Developer_OwnListing_GetsActionableLegalStates()
        {
            var soldOut = Listing("SoldOut", listedTokens: 0);
            Assert.That(
                Resolve(MainPageRole.Developer, soldOut),
                Is.EqualTo("Assign developer lawyer"));

            soldOut = soldOut with { DeveloperLawyer = OtherLawyer };
            Assert.That(
                Resolve(MainPageRole.Developer, soldOut),
                Is.EqualTo("Waiting for SPV lawyer"));

            soldOut = soldOut with { SpvLawyer = OtherLawyer };
            Assert.That(
                Resolve(MainPageRole.Developer, soldOut),
                Is.EqualTo("Legal review in progress"));

            soldOut = soldOut with { DeveloperLawyerDocStatus = DocumentStatus.Rejected };
            Assert.That(
                Resolve(MainPageRole.Developer, soldOut),
                Is.EqualTo("Documents to revise"));
        }

        [Test]
        public void Developer_OwnVersusOtherListingAndAssetSetup_AreDistinct()
        {
            Assert.That(
                Resolve(MainPageRole.Developer, Listing("PendingAssets")),
                Is.EqualTo("Assets to initialize"));
            Assert.That(
                Resolve(MainPageRole.Developer, Listing()),
                Is.EqualTo("Sale active"));

            var otherDeveloper = Listing() with
            {
                Owner = "someone-else",
                OngoingObjectListingDetails = Listing().OngoingObjectListingDetails! with
                {
                    RealEstateDeveloper = "someone-else",
                },
            };
            Assert.That(
                Resolve(MainPageRole.Developer, otherDeveloper),
                Is.EqualTo("For sale"));
        }

        [Test]
        public void LettingAgent_ResignationThenChallengeThenProposal_TakePriority()
        {
            var listing = Listing("Finalized", listedTokens: 0);
            var context = Context() with
            {
                LettingAgent = Viewer,
                ResignationDueTimestamp = Now + 100,
                ActiveChallengeId = 2,
                ActiveChallengeAgent = Viewer,
                ActiveProposalId = 1,
                ActiveProposalProposer = Viewer,
            };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, context),
                Is.EqualTo("Resignation pending"));

            context = context with { ResignationDueTimestamp = null };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, context),
                Is.EqualTo("Challenge against you active"));

            context = context with { ActiveChallengeId = 0, ActiveChallengeAgent = null };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, context),
                Is.EqualTo("Your proposal is active"));
        }

        [Test]
        public void LettingAgent_PredecessorGovernance_IsNotPersonal()
        {
            var listing = Listing("Finalized", listedTokens: 0);
            var context = Context() with
            {
                LettingAgent = Viewer,
                ActiveChallengeId = 2,
                ActiveChallengeAgent = OtherAgent,
                ActiveProposalId = 1,
                ActiveProposalProposer = OtherAgent,
            };

            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, context),
                Is.EqualTo("Governance vote active"));
        }

        [Test]
        public void LettingAgent_OwnSeatAndOwnHistoricalClaim_AreDistinct()
        {
            var listing = Listing("Finalized", listedTokens: 0);
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, Context() with { LettingAgent = Viewer }),
                Is.EqualTo("Managing property"));

            var claimed = Context() with { HasClaimedProperty = true, CanClaimProperty = true };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, claimed),
                Is.EqualTo("Already claimed"));
        }

        [Test]
        public void LettingAgent_EligibleVacantSeat_CanBeClaimed()
        {
            var context = Context() with { CanClaimProperty = true };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, Listing("Finalized", listedTokens: 0), context),
                Is.EqualTo("Can be claimed"));
        }

        [Test]
        public void LettingAgent_AssignedElectionAndVacantStates_AreCovered()
        {
            var listing = Listing("Finalized", listedTokens: 0);
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, Context() with { LettingAgent = OtherAgent }),
                Is.EqualTo("Letting agent assigned"));

            var openElection = Context() with
            {
                LettingAgent = Vacant,
                LettingElectionRound = 2,
                LettingElectionCandidateCount = 2,
                LettingElectionExpiry = Now + 7200,
            };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, openElection),
                Is.EqualTo("2 hours to vote"));

            Assert.That(
                Resolve(
                    MainPageRole.LettingAgent,
                    listing,
                    openElection with { LettingElectionExpiry = Now }),
                Is.EqualTo("Agent election to finalize"));

            Assert.That(
                Resolve(MainPageRole.LettingAgent, listing, Context() with { LettingAgent = Vacant }),
                Is.EqualTo("Letting seat vacant"));
        }

        [Test]
        public void LettingAgent_PreFinalizedProperty_NeverLooksClaimable()
        {
            var context = Context() with { CanClaimProperty = true, HasClaimedProperty = true };
            Assert.That(
                Resolve(MainPageRole.LettingAgent, Listing("SoldOut", listedTokens: 0), context),
                Is.EqualTo("Legal review in progress"));
        }

        [TestCase("Cancelled", 0u, "Cancelled")]
        [TestCase("Cancelled", 1u, "Can be refunded")]
        [TestCase("Refunding", 0u, "Legal process expired")]
        [TestCase("Refunding", 1u, "Legal process expired")]
        public void Investor_TornDownStates_UsePositionAndLegalPrecedence(
            string status,
            uint bought,
            string expected)
        {
            Assert.That(
                Resolve(MainPageRole.Investor, Listing(status, listedTokens: 0), tokensBought: bought),
                Is.EqualTo(expected));
        }

        [Test]
        public void LettingAgent_ResignationFromAnotherSeat_IsNotShownAsOwn()
        {
            var context = Context() with
            {
                LettingAgent = OtherAgent,
                ResignationDueTimestamp = Now + 100,
            };

            Assert.That(
                Resolve(MainPageRole.LettingAgent, Listing("Finalized", listedTokens: 0), context),
                Is.EqualTo("Letting agent assigned"));
        }

        [Test]
        public void Lawyer_LegalTimeoutAndHistoricalClaim_HaveDefinedPrecedence()
        {
            var listing = Listing("SoldOut", listedTokens: 0) with
            {
                LegalDeadlineTimestamp = Now - 1,
                SpvLawyer = OtherLawyer,
            };
            Assert.That(
                Resolve(MainPageRole.Lawyer, listing),
                Is.EqualTo("Legal process expired"));

            Assert.That(
                Resolve(MainPageRole.Lawyer, listing, Context() with { HasClaimedSpvCase = true }),
                Is.EqualTo("Already claimed"));
        }

        [Test]
        public void LettingAgent_MissingContext_ShowsOnlyKnownLifecycle()
        {
            Assert.That(
                Resolve(MainPageRole.LettingAgent, Listing("Finalized", listedTokens: 0)),
                Is.EqualTo("Finalized"));
        }

        [Test]
        public void Resolve_UsesTheNewlySelectedRoleBeforeTheWrapperIsUpdated()
        {
            var wrapper = Wrapper(
                MainPageRole.Investor,
                Listing("Finalized", listedTokens: 0),
                Context() with { CanClaimProperty = true });

            Assert.That(
                PropertyStatusResolver.Resolve(wrapper, Now, MainPageRole.LettingAgent),
                Is.EqualTo("Can be claimed"));
        }

        [Test]
        public void WrapperStatus_IsTheSharedCardAndDetailValue()
        {
            var wrapper = Wrapper(MainPageRole.Lawyer, Listing("Finalized", listedTokens: 0));

            Assert.That(wrapper.Status, Is.EqualTo("Finalized"));
            Assert.That(wrapper.StatusIsVisible, Is.True);

            var withoutDetails = wrapper with
            {
                NftBase = wrapper.NftBase is XcavateSolanaListingNft listing
                    ? listing with { OngoingObjectListingDetails = null }
                    : wrapper.NftBase,
            };
            Assert.That(withoutDetails.Status, Is.Null);
            Assert.That(withoutDetails.StatusIsVisible, Is.False);
        }

        [Test]
        public void NullCountdown_DoesNotThrowOrFabricateExpiry()
        {
            var wrapper = Wrapper(MainPageRole.Investor, Listing()) with { TimeLeftToBuy = null };

            Assert.DoesNotThrow(() => _ = PropertyStatusResolver.Resolve(wrapper, Now));
            Assert.That(PropertyStatusResolver.Resolve(wrapper, Now), Is.EqualTo("For sale"));
        }
    }
}
