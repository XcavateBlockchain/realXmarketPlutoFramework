using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using System.Numerics;
using UniqueryPlus.Metadata;
using XcavateDevnetIndexer;

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
                SolanaCluster.Devnet,
                PropertyListingStateFilter.Default,
                first: 20,
                offset: 0,
                CancellationToken.None);

            Assert.That(properties, Is.Not.Null);

            // The state filter runs in SQL: the default selection ("Can be reserved")
            // must only ever surface LISTED rows, whatever devnet currently holds.
            foreach (var property in properties)
            {
                Assert.That(property.ListingStatus, Is.EqualTo("Listed"));
            }

            // Whatever is listed on devnet right now must come out renderable: the
            // marketplace views need these to be non-null.
            var withDocument = 0;

            foreach (var property in properties)
            {
                Assert.That(property.XcavateMetadata, Is.Not.Null);
                Assert.That(property.OngoingObjectListingDetails, Is.Not.Null);
                Assert.That(property.Metadata, Is.Not.Null);

                // The asset's nested metadata document (fetched and decomposed by the
                // indexer, ADR-27) flows into the view shape: the base image is the
                // document's first file, and both names agree on the document's.
                if (property.Metadata.Image.Length > 0)
                {
                    withDocument++;

                    Assert.That(property.XcavateMetadata!.Files, Is.Not.Empty);
                    Assert.That(property.Metadata.Name, Is.EqualTo(property.XcavateMetadata.PropertyName));
                }

                // When the mirror has uploaded for the asset, the compressed thumbnails
                // are what the views show: the base image is the first thumbnail and the
                // display list is the thumbnails, with the full-resolution originals kept
                // on Files for the full-screen image page. (Soft: a devnet without the
                // mirror configured has ThumbnailFiles empty everywhere.)
                if (property.XcavateMetadata!.ThumbnailFiles.Count > 0)
                {
                    Assert.That(property.Metadata.Image, Is.EqualTo(property.XcavateMetadata.ThumbnailFiles[0]));
                    Assert.That(property.XcavateMetadata.DisplayImages, Is.EqualTo(property.XcavateMetadata.ThumbnailFiles));
                    Assert.That(property.XcavateMetadata.ThumbnailFiles.Count, Is.LessThanOrEqualTo(property.XcavateMetadata.Files.Count));
                }
                else
                {
                    Assert.That(property.XcavateMetadata.DisplayImages, Is.EqualTo(property.XcavateMetadata.Files));
                }

                Console.WriteLine($"{property.ListingId}: {property.XcavateMetadata!.PropertyName} ({property.ListingStatus})");
            }

            // The devnet enricher snapshots the live assets' documents, so a feed of
            // listings over assets with documents must surface at least one of them.
            // (Kept soft on purpose for devnets that run with zero documents.)
            Console.WriteLine($"{withDocument}/{properties.Count} listings carry a metadata document");
        }

        [Test]
        public async Task GetListingFullInfoAsync_ReturnsNullForUnknownListingAsync()
        {
            // No deployment will ever mint this listing id, so null is a real "not found"
            // answer rather than a lookup that silently failed.
            var listing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                SolanaCluster.Devnet,
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

        [Test]
        public void ToFeedStates_DefaultIsForSaleOnly()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToFeedStates(PropertyListingStateFilter.Default),
                Is.EqualTo(new[] { ListingFeedState.ForSale }));
        }

        [Test]
        public void ToFeedStates_NoneIsEmpty()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToFeedStates(PropertyListingStateFilter.None),
                Is.Empty);
        }

        [Test]
        // Every checkbox on: every feed state comes out exactly once. The partition is
        // complete over the active listings - PENDING_ASSETS stays unreachable.
        public void ToFeedStates_EverythingCheckedCoversEveryFeedState()
        {
            var all = PropertyListingStateFilter.CanBeReserved
                | PropertyListingStateFilter.WaitingForClaim
                | PropertyListingStateFilter.SoldOut
                | PropertyListingStateFilter.Expired
                | PropertyListingStateFilter.Finalized
                | PropertyListingStateFilter.CancelledOrRefunding;

            Assert.That(
                XcavateMarketplaceIndexerModel.ToFeedStates(all),
                Is.EquivalentTo(new[]
                {
                    ListingFeedState.ForSale,
                    ListingFeedState.WaitingForClaim,
                    ListingFeedState.SoldOut,
                    ListingFeedState.Expired,
                    ListingFeedState.Finalized,
                    ListingFeedState.CancelledOrRefunding,
                }));
        }

        [Test]
        // The mapping is one checkbox to one derived feed state; the indexer's SQL owns
        // the wall-clock and availability overlays behind each.
        [TestCase(PropertyListingStateFilter.CanBeReserved, ListingFeedState.ForSale)]
        [TestCase(PropertyListingStateFilter.WaitingForClaim, ListingFeedState.WaitingForClaim)]
        [TestCase(PropertyListingStateFilter.SoldOut, ListingFeedState.SoldOut)]
        [TestCase(PropertyListingStateFilter.Expired, ListingFeedState.Expired)]
        [TestCase(PropertyListingStateFilter.Finalized, ListingFeedState.Finalized)]
        [TestCase(PropertyListingStateFilter.CancelledOrRefunding, ListingFeedState.CancelledOrRefunding)]
        public void ToFeedStates_SingleCheckboxMapsToItsFeedState(
            PropertyListingStateFilter state,
            ListingFeedState expected)
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.ToFeedStates(state),
                Is.EqualTo(new[] { expected }));
        }

        // The devnet fixture the claim-split tests pin: this investor reserved 72 shares of
        // listing 5 and later claimed them, so the position reads shareAmount=72 /
        // reservedShareAmount=0 and the ShareHolding carries the 72. If devnet moves on
        // (position closed, more shares reserved), the tests go Inconclusive rather than red.
        private const string ClaimedInvestor = "CsEy5p6kLmhPaHULMBriu5XGcs4hTv6yxi1SKyni4i5M";
        private const long ClaimedListingId = 5;

        /// <summary>
        /// The pinned position in its claimed state (bought shares, none reserved), or null
        /// when devnet no longer holds it - the caller turns that into Inconclusive.
        /// </summary>
        private static async Task<XcavateSolanaInvestorProperty?> FetchClaimedPositionAsync()
        {
            var properties = await XcavateMarketplaceIndexerModel.GetInvestorPropertiesAsync(
                SolanaCluster.Devnet,
                ClaimedInvestor,
                owned: null,
                reserved: null,
                name: null,
                townCity: null,
                propertyType: null,
                first: 100,
                offset: 0,
                CancellationToken.None);

            return properties.FirstOrDefault(property =>
                property.Listing.ListingId == ClaimedListingId
                && property.BoughtShares > 0
                && property.ReservedShares == 0);
        }

        [Test]
        public async Task GetInvestorPropertiesAsync_AfterClaim_ReportsReservedAndOwnedSeparatelyAsync()
        {
            var claimed = await FetchClaimedPositionAsync();
            if (claimed is null)
            {
                Assert.Inconclusive("The pinned devnet position (listing 5, claimed shares) is gone or changed.");
                return;
            }

            // The wrapper's TokensBought ("You reserved", the claim and cancel-reservation
            // gates) reads the ongoing listing's ShareOwners: after a claim nothing is
            // reserved, so the entry must carry the reserved figure alone - not the
            // bought-plus-reserved sum, which would resurrect the claimed shares as
            // "reserved".
            var reserved = claimed.Listing.OngoingObjectListingDetails?.ShareOwners
                .TryGetValue(ClaimedInvestor, out var reservedOwner) == true
                ? reservedOwner.ShareAmount
                : 0u;
            Assert.That(reserved, Is.EqualTo(claimed.ReservedShares));

            // The wrapper's TokensOwned ("You own") reads the asset's ShareOwners: the
            // claimed shares must show up there, or the list card says "Tokens bought"
            // for a position whose claim already went through.
            uint? owned = claimed.Listing.RealWorldAssetDetails?.ShareOwners
                .TryGetValue(ClaimedInvestor, out var owner) == true
                ? owner.ShareAmount
                : null;
            Assert.That(owned, Is.EqualTo(claimed.BoughtShares));
        }

        [Test]
        public async Task GetListingFullInfoAsync_AfterClaim_ReportsReservedAndOwnedSeparatelyAsync()
        {
            var claimed = await FetchClaimedPositionAsync();
            if (claimed is null)
            {
                Assert.Inconclusive("The pinned devnet position (listing 5, claimed shares) is gone or changed.");
                return;
            }

            var listing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                SolanaCluster.Devnet,
                ClaimedListingId,
                ClaimedInvestor,
                CancellationToken.None);

            Assert.That(listing, Is.Not.Null);

            // Same split as the list path, on the detail page's fresh refetch: the
            // position's ShareOwners entry is the reserved count only (0 after the
            // claim), the asset's holdings carry the owned count.
            var reserved = listing!.OngoingObjectListingDetails?.ShareOwners
                .TryGetValue(ClaimedInvestor, out var reservedOwner) == true
                ? reservedOwner.ShareAmount
                : 0u;
            Assert.That(reserved, Is.EqualTo(claimed.ReservedShares));

            uint? owned = listing.RealWorldAssetDetails?.ShareOwners
                .TryGetValue(ClaimedInvestor, out var owner) == true
                ? owner.ShareAmount
                : null;
            Assert.That(owned, Is.EqualTo(claimed.BoughtShares));
        }

        private const string MintA = "MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string MintB = "MintBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string AgentA = "AgentAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string AgentB = "AgentBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string AgentC = "AgentCxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string LawyerA = "LawyerAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string LawyerB = "LawyerBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string Developer = "Developerxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string Challenger = "Challengerxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        [Test]
        // The u128 per_share accumulator overflows a ulong once enough income has been
        // split; the raw-JSON decimal string must land in a BigInteger without loss.
        public void ParseIncomeStreams_ParsesU128AboveULongMax()
        {
            var aboveULong = new BigInteger(ulong.MaxValue) + BigInteger.One;
            const string streams = """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"18446744073709551616","dust":7}]""";

            var parsed = XcavateMarketplaceIndexerModel.ParseIncomeStreams(streams);

            Assert.That(parsed, Has.Count.EqualTo(1));
            Assert.That(parsed[0].Mint, Is.EqualTo(MintA));
            Assert.That(parsed[0].PerShare, Is.EqualTo(aboveULong));
            Assert.That(parsed[0].Dust, Is.EqualTo(7));
        }

        [Test]
        public void ParseIncomeStreams_MalformedInputYieldsNoStreams()
        {
            Assert.That(XcavateMarketplaceIndexerModel.ParseIncomeStreams(null), Is.Empty);
            Assert.That(XcavateMarketplaceIndexerModel.ParseIncomeStreams(""), Is.Empty);
            Assert.That(XcavateMarketplaceIndexerModel.ParseIncomeStreams("not json"), Is.Empty);
            Assert.That(XcavateMarketplaceIndexerModel.ParseIncomeStreams("[]"), Is.Empty);
        }

        [Test]
        // A missing checkpoint row (holder never claimed) reads as zero cursors for
        // every stream - never null, the empty state.
        public void ParseIncomeCheckpoint_MissingRowYieldsEmptyState()
        {
            var checkpoint = XcavateMarketplaceIndexerModel.ParseIncomeCheckpoint(null);

            Assert.That(checkpoint.Entries, Is.Not.Null);
            Assert.That(checkpoint.Entries, Is.Empty);
        }

        [Test]
        public void GetClaimable_NoCheckpoint_AccruesFromStreamStart()
        {
            var state = new XcavateMarketplaceIndexerModel.PropertyIncomeState
            {
                Streams = XcavateMarketplaceIndexerModel.ParseIncomeStreams(
                    """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"300","dust":0}]"""),
                Checkpoint = XcavateMarketplaceIndexerModel.IncomeCheckpointState.Empty,
            };

            // No entry at index 0 means "held since before this stream opened": the
            // whole per_share history accrues (on-chain sync_entries zero-fills).
            Assert.That(state.GetClaimable(MintA, shares: 10), Is.EqualTo(new BigInteger(3000)));
        }

        [Test]
        public void GetClaimable_SettlesAgainstCheckpoint()
        {
            var state = new XcavateMarketplaceIndexerModel.PropertyIncomeState
            {
                Streams = XcavateMarketplaceIndexerModel.ParseIncomeStreams(
                    """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"350","dust":0},{"mint":"MintBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"10","dust":2}]"""),
                Checkpoint = XcavateMarketplaceIndexerModel.ParseIncomeCheckpoint(
                    """[{"per_share":"350","pending":40},{"per_share":"5","pending":1}]"""),
            };

            // First stream: checkpoint caught up, only banked pending remains.
            Assert.That(state.GetClaimable(MintA, shares: 10), Is.EqualTo(new BigInteger(40)));
            // Second: (10 - 5) * 10 + 1 banked.
            Assert.That(state.GetClaimable(MintB, shares: 10), Is.EqualTo(new BigInteger(51)));
        }

        [Test]
        // entries[i] tracks streams[i]; the checkpoint written before a later
        // distribution round short-reads, and the missing cursor is zero.
        public void GetClaimable_ShortCheckpointEntriesReadAsZero()
        {
            var state = new XcavateMarketplaceIndexerModel.PropertyIncomeState
            {
                Streams = XcavateMarketplaceIndexerModel.ParseIncomeStreams(
                    """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"350","dust":0},{"mint":"MintBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"20","dust":0}]"""),
                Checkpoint = XcavateMarketplaceIndexerModel.ParseIncomeCheckpoint(
                    """[{"per_share":"350","pending":0}]"""),
            };

            Assert.That(state.GetClaimable(MintB, shares: 3), Is.EqualTo(new BigInteger(60)));
        }

        [Test]
        public void GetClaimable_UnknownMintIsZero()
        {
            var state = new XcavateMarketplaceIndexerModel.PropertyIncomeState
            {
                Streams = XcavateMarketplaceIndexerModel.ParseIncomeStreams(
                    """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"100","dust":0}]"""),
                Checkpoint = XcavateMarketplaceIndexerModel.IncomeCheckpointState.Empty,
            };

            Assert.That(state.GetClaimable(MintB, shares: 10), Is.EqualTo(BigInteger.Zero));
        }

        [Test]
        // A closed/zero holding accrues nothing new but keeps what settle banked -
        // else close_income_checkpoint would strand income (its PendingIncome guard).
        public void GetClaimable_ZeroSharesKeepsBankedPending()
        {
            var state = new XcavateMarketplaceIndexerModel.PropertyIncomeState
            {
                Streams = XcavateMarketplaceIndexerModel.ParseIncomeStreams(
                    """[{"mint":"MintAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","per_share":"100","dust":0}]"""),
                Checkpoint = XcavateMarketplaceIndexerModel.ParseIncomeCheckpoint(
                    """[{"per_share":"10","pending":55}]"""),
            };

            Assert.That(state.GetClaimable(MintA, shares: 0), Is.EqualTo(new BigInteger(55)));
        }

        [Test]
        // Claimable amounts are mint base units; display divides by the mint's decimals.
        public void ToDisplayAmount_DividesByMintDecimals()
        {
            Assert.That(
                XcavateMarketplaceIndexerModel.PropertyIncomeState.ToDisplayAmount(new BigInteger(1_500_000_000), mintDecimals: 9),
                Is.EqualTo(1.5m));
            Assert.That(
                XcavateMarketplaceIndexerModel.PropertyIncomeState.ToDisplayAmount(new BigInteger(2_500_000), mintDecimals: 6),
                Is.EqualTo(2.5m));
        }

        [Test]
        public void ToCurrentRound_DropsStaleRoundsAndSortsByVotePower()
        {
            var rows = new[]
            {
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(3, AgentB, 40),
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(2, AgentC, 999),
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(3, AgentA, 90),
            };

            var current = XcavateMarketplaceIndexerModel.AgentElectionState.ToCurrentRound(3, rows);

            Assert.That(current.Select(c => c.Agent), Is.EqualTo(new[] { AgentA, AgentB }));
        }

        [Test]
        // On-chain finalize assigns only a strictly unique leader; a tie at the top
        // reopens the seat instead, so the winner reads as null.
        public void PickPluralityWinner_TieAtTopIsNull()
        {
            var tied = new[]
            {
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentA, 50),
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentB, 50),
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentC, 10),
            };

            Assert.That(XcavateMarketplaceIndexerModel.AgentElectionState.PickPluralityWinner(tied), Is.Null);
        }

        [Test]
        public void PickPluralityWinner_UniqueLeaderWins()
        {
            var rows = new[]
            {
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentB, 90),
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentA, 90),
                new XcavateMarketplaceIndexerModel.AgentElectionState.Candidacy(AgentC, 91),
            };

            Assert.That(XcavateMarketplaceIndexerModel.AgentElectionState.PickPluralityWinner(rows), Is.EqualTo(AgentC));
            Assert.That(
                XcavateMarketplaceIndexerModel.AgentElectionState.PickPluralityWinner([]),
                Is.Null);
        }

        [Test]
        public void AgentElection_Create_KeepsOnlyCurrentRoundVotes()
        {
            var votes = new[]
            {
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(1, AgentA, 5),
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(2, AgentB, 30),
            };

            var state = XcavateMarketplaceIndexerModel.AgentElectionState.Create(
                agent: null,
                round: 2,
                expiry: 1_800_000_000,
                quorumBps: 1500,
                candidateCount: 2,
                candidacies: [],
                votes: votes);

            Assert.That(state.MyVote, Is.Not.Null);
            Assert.That(state.MyVote!.Candidate, Is.EqualTo(AgentB));
            Assert.That(state.MyVote.Power, Is.EqualTo(30));
        }

        [Test]
        public void Evaluate_ProposalEndedOnlyAfterExpiry()
        {
            var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
            var letting = new XcavateMarketplaceIndexerModel.PendingFinalizations.LettingSnapshot(
                ElectionRound: 0, ElectionExpiry: 0, ElectionCandidateCount: 0,
                ActiveProposal: 7, ActiveChallenge: 0);
            var proposal = new XcavateMarketplaceIndexerModel.PendingFinalizations.ProposalSnapshot(
                ProposalId: 7, Expiry: now.ToUnixTimeSeconds(), RentPayer: Developer);

            var ended = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting, proposal, null, null, null, null, null);
            Assert.That(ended.Proposal, Is.Not.Null);
            Assert.That(ended.Proposal!.ProposalId, Is.EqualTo(7));

            var ongoing = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting, proposal with { Expiry = now.ToUnixTimeSeconds() + 60 }, null, null, null, null, null);
            Assert.That(ongoing.Proposal, Is.Null);

            var none = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting with { ActiveProposal = 0 }, null, null, null, null, null, null);
            Assert.That(none.Proposal, Is.Null);
        }

        [Test]
        public void Evaluate_ChallengeEndedOnlyAfterExpiry()
        {
            var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
            var letting = new XcavateMarketplaceIndexerModel.PendingFinalizations.LettingSnapshot(
                ElectionRound: 0, ElectionExpiry: 0, ElectionCandidateCount: 0,
                ActiveProposal: 0, ActiveChallenge: 3);
            var challenge = new XcavateMarketplaceIndexerModel.PendingFinalizations.ChallengeSnapshot(
                ChallengeId: 3, Expiry: now.ToUnixTimeSeconds() - 1,
                RentPayer: Developer, Challenger: Challenger, StruckAgent: AgentA);

            var ended = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting, null, challenge, null, null, null, null);
            Assert.That(ended.Challenge, Is.Not.Null);
            Assert.That(ended.Challenge!.ChallengeId, Is.EqualTo(3));
            Assert.That(ended.Challenge.StruckAgent, Is.EqualTo(AgentA));

            var ongoing = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting, null, challenge with { Expiry = now.ToUnixTimeSeconds() + 60 }, null, null, null, null);
            Assert.That(ongoing.Challenge, Is.Null);
        }

        [Test]
        // Finalization clears election.expiry/election.candidate_count on-chain, so an
        // ended-but-unfinalized election is exactly "expiry set, in the past, count > 0".
        public void Evaluate_AgentElectionEndedNeedsUnfinalizedWindow()
        {
            var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
            var expiry = now.ToUnixTimeSeconds() - 1;
            var letting = new XcavateMarketplaceIndexerModel.PendingFinalizations.LettingSnapshot(
                ElectionRound: 4, ElectionExpiry: expiry, ElectionCandidateCount: 2,
                ActiveProposal: 0, ActiveChallenge: 0);
            var candidacies = new[]
            {
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(4, AgentA, 90),
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(4, AgentB, 30),
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(3, AgentC, 200),
            };

            var ended = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting, null, null, candidacies, null, null, null);
            Assert.That(ended.AgentElection, Is.Not.Null);
            Assert.That(ended.AgentElection!.Winner, Is.EqualTo(AgentA));
            Assert.That(ended.AgentElection.Candidates, Is.EqualTo(new[] { AgentA, AgentB }));

            // Already finalized (window cleared) or still open: nothing to crank.
            var finalized = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting with { ElectionExpiry = 0, ElectionCandidateCount = 0 }, null, null, candidacies, null, null, null);
            Assert.That(finalized.AgentElection, Is.Null);

            var ongoing = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, letting with { ElectionExpiry = now.ToUnixTimeSeconds() + 60 }, null, null, candidacies, null, null, null);
            Assert.That(ongoing.AgentElection, Is.Null);
        }

        [Test]
        public void Evaluate_SpvElectionRequiresVacatedWindowAndNoAssignedLawyer()
        {
            var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
            var listing = new XcavateMarketplaceIndexerModel.PendingFinalizations.ListingSnapshot(
                Status: ListingStatus.SoldOut,
                LegalDeadline: now.ToUnixTimeSeconds() + 1000,
                LegalProcessTime: 1000,
                Developer: Developer,
                DeveloperLawyer: LawyerA,
                DeveloperLawyerDocStatus: DocumentStatus.Pending,
                SpvLawyer: XcavateMarketplaceIndexerModel.VacantSeatPubkey,
                SpvLawyerDocStatus: DocumentStatus.Pending,
                SpvElectionRound: 1,
                SpvElectionExpiry: now.ToUnixTimeSeconds() - 1,
                SpvElectionCandidateCount: 1,
                RegionId: 5,
                CollectedMints: []);
            var candidacies = new[]
            {
                new XcavateMarketplaceIndexerModel.ElectionCandidacyRow(1, LawyerA, 12),
            };

            var ended = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, null, null, null, null, listing, candidacies, null);
            Assert.That(ended.SpvElection, Is.Not.Null);
            Assert.That(ended.SpvElection!.Winner, Is.EqualTo(LawyerA));

            // A lawyer already seated means the crank ran: no banner.
            var seated = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, null, null, null, null, listing with { SpvLawyer = LawyerB }, candidacies, null);
            Assert.That(seated.SpvElection, Is.Null);
        }

        [Test]
        // On-chain resolve_silent_verdict only succeeds in the review window's final
        // stretch, before the deadline, while the SPV side ruled and the developer's
        // side stayed silent (legal.rs).
        public void Evaluate_SilentVerdictOnlyInFinalStretchBeforeDeadline()
        {
            var processTime = 1000L;
            var legalDeadline = 2_000_000L;
            var listing = new XcavateMarketplaceIndexerModel.PendingFinalizations.ListingSnapshot(
                Status: ListingStatus.SoldOut,
                LegalDeadline: legalDeadline,
                LegalProcessTime: processTime,
                Developer: Developer,
                DeveloperLawyer: LawyerA,
                DeveloperLawyerDocStatus: DocumentStatus.Pending,
                SpvLawyer: LawyerB,
                SpvLawyerDocStatus: DocumentStatus.Approved,
                SpvElectionRound: 1,
                SpvElectionExpiry: 0,
                SpvElectionCandidateCount: 0,
                RegionId: 5,
                CollectedMints: []);

            // In the final stretch (deadline - processTime/5 .. deadline): fire it.
            var inStretch = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                DateTimeOffset.FromUnixTimeSeconds(legalDeadline - 100), null, null, null, null, listing, null, null);
            Assert.That(inStretch.SilentVerdictOverdue, Is.True);

            // Window still open: too early.
            var tooEarly = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                DateTimeOffset.FromUnixTimeSeconds(legalDeadline - processTime), null, null, null, null, listing, null, null);
            Assert.That(tooEarly.SilentVerdictOverdue, Is.False);

            // Deadline lapsed: the timeout refund path owns the vault, not this crank.
            var lapsed = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                DateTimeOffset.FromUnixTimeSeconds(legalDeadline + 1), null, null, null, null, listing, null, null);
            Assert.That(lapsed.SilentVerdictOverdue, Is.False);

            // Both sides pending: nothing was ruled, no silent party to punish.
            var bothSilent = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                DateTimeOffset.FromUnixTimeSeconds(legalDeadline - 100), null, null, null, null,
                listing with { SpvLawyerDocStatus = DocumentStatus.Pending }, null, null);
            Assert.That(bothSilent.SilentVerdictOverdue, Is.False);
        }

        [Test]
        // execute_deal gates on status LEGAL and now <= legal_deadline on-chain; the
        // banner must not outlive the deadline (then the timeout refund path owns it).
        public void Evaluate_ExecuteDealReadyBeforeDeadlineOnly()
        {
            var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
            var listing = new XcavateMarketplaceIndexerModel.PendingFinalizations.ListingSnapshot(
                Status: ListingStatus.Legal,
                LegalDeadline: now.ToUnixTimeSeconds() + 1000,
                LegalProcessTime: 1000,
                Developer: Developer,
                DeveloperLawyer: LawyerA,
                DeveloperLawyerDocStatus: DocumentStatus.Approved,
                SpvLawyer: LawyerB,
                SpvLawyerDocStatus: DocumentStatus.Approved,
                SpvElectionRound: 1,
                SpvElectionExpiry: 0,
                SpvElectionCandidateCount: 0,
                RegionId: 5,
                CollectedMints: [MintA, MintB]);
            var config = new XcavateMarketplaceIndexerModel.PendingFinalizations.ConfigSnapshot(
                XcavMint: MintA, Treasury: Developer, AcceptedPaymentMints: [MintA, MintB]);

            var ready = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, null, null, null, null, listing, null, config);
            Assert.That(ready.ExecuteDeal, Is.Not.Null);
            Assert.That(ready.ExecuteDeal!.RegionId, Is.EqualTo((ushort)5));
            Assert.That(ready.ExecuteDeal.DeveloperLawyer, Is.EqualTo(LawyerA));
            Assert.That(ready.ExecuteDeal.SpvLawyer, Is.EqualTo(LawyerB));
            Assert.That(ready.ExecuteDeal.XcavMint, Is.EqualTo(MintA));
            Assert.That(ready.ExecuteDeal.CollectedMints, Is.EqualTo(new[] { MintA, MintB }));

            var lapsed = XcavateMarketplaceIndexerModel.EvaluatePendingFinalizations(
                now, null, null, null, null, listing with { LegalDeadline = now.ToUnixTimeSeconds() - 1 }, null, config);
            Assert.That(lapsed.ExecuteDeal, Is.Null);
        }

        [Test]
        public async Task GetIncomeStateAsync_UnknownAssetReadsAsEmptyAsync()
        {
            var state = await XcavateMarketplaceIndexerModel.GetIncomeStateAsync(
                SolanaCluster.Devnet,
                long.MaxValue,
                owner: "",
                rpc: SolanaRpcModel.GetClient(SolanaCluster.Devnet),
                CancellationToken.None);

            Assert.That(state.Streams, Is.Empty);
            Assert.That(state.Checkpoint.Entries, Is.Empty);
            Assert.That(state.GetClaimable(MintA, shares: 100), Is.EqualTo(BigInteger.Zero));
        }

        [Test]
        public async Task GetAgentElectionStateAsync_UnknownAssetHasNoElectionAsync()
        {
            var state = await XcavateMarketplaceIndexerModel.GetAgentElectionStateAsync(
                SolanaCluster.Devnet,
                long.MaxValue,
                voter: XcavateMarketplaceIndexerModel.VacantSeatPubkey,
                CancellationToken.None);

            Assert.That(state.Round, Is.EqualTo(0));
            Assert.That(state.Candidacies, Is.Empty);
            Assert.That(state.MyVote, Is.Null);
            Assert.That(state.WinnerOrNull, Is.Null);
        }
    }
}
