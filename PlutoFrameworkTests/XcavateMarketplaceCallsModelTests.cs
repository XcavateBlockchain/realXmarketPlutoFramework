using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Wallet;
using System.Security.Cryptography;
using System.Text;
using XcavateDevnetIndexer;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Pure-seam tests for <see cref="XcavateMarketplaceCallsModel"/>: the send
    /// validation and transferability arithmetic, the revote's previous-candidacy
    /// resolution, the pending-finalization guard and the SPV-election crank's arrow
    /// mapping. The async wrappers' live state (indexer/RPC) stays behind these seams;
    /// the instruction shapes themselves are pinned in <see cref="XcavateMarketplaceProgramTests"/>.
    /// </summary>
    internal class XcavateMarketplaceCallsModelTests
    {
        private static readonly XcavateProgramSet Programs = XcavateProgramAddresses.Devnet;

        private static PublicKey SyntheticKey(byte seed) =>
            new(SolanaBase58.Encode([.. Enumerable.Repeat(seed, 32)]));

        [Test]
        public void ValidateShareSend_AcceptsATransferThatFitsBothBounds()
        {
            Assert.DoesNotThrow(() =>
                XcavateMarketplaceCallsModel.ValidateShareSend(
                    amount: 4, senderTransferable: 4, receiverOwned: 5, maxShares: 10));
        }

        [Test]
        [TestCase(0u, 10u)]
        [TestCase(11u, 10u)]
        public void ValidateShareSend_RejectsZeroAndOverdraw(uint amount, uint transferable)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                XcavateMarketplaceCallsModel.ValidateShareSend(
                    amount, senderTransferable: transferable, receiverOwned: 0, maxShares: 100));

            Assert.That(ex!.Message, Does.Contain("transferable holding"));
        }

        [Test]
        public void ValidateShareSend_PinsTheStrictOwnershipCapBoundary()
        {
            // On-chain the cap is owned_after < max_shares - an exact hit rejects too.
            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    XcavateMarketplaceCallsModel.ValidateShareSend(
                        4, senderTransferable: 10, receiverOwned: 5, maxShares: 9));
                Assert.DoesNotThrow(() =>
                    XcavateMarketplaceCallsModel.ValidateShareSend(
                        3, senderTransferable: 10, receiverOwned: 5, maxShares: 9));
            });
        }

        [Test]
        public void TransferableShares_SubtractsTheLargestLockThenListed_Saturating()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateMarketplaceCallsModel.TransferableShares(
                        new MarketplaceShareHoldingOf_ShareHoldings_Nodes_ShareHolding(
                            "1", "w", "100", "10", "20", "5", "0", "0")),
                    Is.EqualTo(70ul));
                Assert.That(
                    XcavateMarketplaceCallsModel.TransferableShares(
                        new MarketplaceShareHoldingOf_ShareHoldings_Nodes_ShareHolding(
                            "1", "w", "10", "0", "12", "0", "0", "0")),
                    Is.EqualTo(0ul));
                Assert.That(XcavateMarketplaceCallsModel.TransferableShares(null), Is.EqualTo(0ul));
            });
        }

        [Test]
        public void ResolvePreviousCandidate_MovesPowerOffOnlyADifferentChoice()
        {
            // needs_previous == record.power > 0 && record.choice != choice - a
            // same-target revote or a first vote must pass None, a switch carries the
            // old choice's account (property election.rs:305-307).
            var other = SyntheticKey(7);
            var same = SyntheticKey(8);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(other.Key, same.Key)?.Key,
                    Is.EqualTo(other.Key));
                Assert.That(
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(same.Key, same.Key),
                    Is.Null);
                Assert.That(
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(null, same.Key),
                    Is.Null);
            });
        }

        [Test]
        public void RequireFinalization_ReturnsTheArrow_OrRejectsAMiss()
        {
            var arrow = new XcavateMarketplaceIndexerModel.PendingFinalizations.ProposalFinalization(
                3, SyntheticKey(1).Key);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateMarketplaceCallsModel.RequireFinalization(arrow, "proposal finalization"),
                    Is.SameAs(arrow));
                Assert.Throws<InvalidOperationException>(() =>
                    XcavateMarketplaceCallsModel.RequireFinalization<XcavateMarketplaceIndexerModel.PendingFinalizations.ProposalFinalization>(
                        null, "proposal finalization"));
            });
        }

        [Test]
        public void BuildFinalizeSpvElection_MapsTheRoundWinnerAndCandidaciesOntoTheInstruction()
        {
            var cranker = SyntheticKey(1);
            var winner = SyntheticKey(2);
            var candidates = new[] { SyntheticKey(3), SyntheticKey(4), SyntheticKey(5) };

            var instruction = XcavateMarketplaceCallsModel.BuildFinalizeSpvElection(
                Programs,
                cranker.Key,
                listingId: 9,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization(
                    winner.Key, Round: 4, [.. candidates.Select(c => c.Key)]));

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(
                    instruction.Keys[3].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveLawyer(Programs, winner).Key));
                Assert.That(instruction.Keys, Has.Count.EqualTo(4 + 3));
                Assert.That(
                    instruction.Keys.Skip(4).Select(key => key.PublicKey).ToArray(),
                    Is.EqualTo(candidates
                        .Select(candidate => XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 9, 4, candidate).Key)
                        .ToArray()));
            });
        }

        [Test]
        public void BuildFinalizeSpvElection_EncodesANoWinnerTieAsTheMarketplaceSentinel()
        {
            var instruction = XcavateMarketplaceCallsModel.BuildFinalizeSpvElection(
                Programs,
                SyntheticKey(1).Key,
                listingId: 9,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization(
                    null, Round: 4, [SyntheticKey(3).Key]));

            Assert.That(
                instruction.Keys[3].PublicKey,
                Is.EqualTo(new PublicKey(Programs.Marketplace).Key));
        }

        [Test]
        public void BuildFinalizeSpvElection_RejectsAMissingArrow()
        {
            Assert.Throws<InvalidOperationException>(() =>
                XcavateMarketplaceCallsModel.BuildFinalizeSpvElection(
                    Programs, SyntheticKey(1).Key, listingId: 9, finalization: null));
        }

        private static byte[] Discriminator(string instructionName) =>
            SHA256.HashData(Encoding.UTF8.GetBytes($"global:{instructionName}"))[..8];

        [Test]
        public void ResolvePreviousCandidateFromVotes_KeepsOnlyTheVotedRound()
        {
            var current = SyntheticKey(5);
            var stale = SyntheticKey(6);
            var target = SyntheticKey(9);

            var votes = new[]
            {
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(2, stale.Key, 7),
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(4, current.Key, 3),
            };

            Assert.Multiple(() =>
            {
                // The round-4 vote backs a different candidacy: it rides along. The
                // stale round-2 row must not leak into a round-4 transaction.
                Assert.That(
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(4, votes, target.Key)?.Key,
                    Is.EqualTo(current.Key));
                Assert.That(
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(2, votes, target.Key)?.Key,
                    Is.EqualTo(stale.Key));
            });
        }

        [Test]
        public void ResolvePreviousCandidateFromVotes_PassesNoneForSameTargetOrNoCurrentVote()
        {
            var same = SyntheticKey(8);
            var stale = SyntheticKey(6);

            var votes = new[]
            {
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(2, stale.Key, 7),
                new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(4, same.Key, 3),
            };

            Assert.Multiple(() =>
            {
                // Same target in the voted round: None - needs_previous is false even
                // though a stale round held a different choice.
                Assert.That(XcavateMarketplaceCallsModel.ResolvePreviousCandidate(4, votes, same.Key), Is.Null);
                // No vote in the voted round at all: None - a stale record belongs to a
                // different vote-record PDA than the one this transaction passes.
                Assert.That(XcavateMarketplaceCallsModel.ResolvePreviousCandidate(9, votes, same.Key), Is.Null);
            });
        }

        [Test]
        public void BuildExecuteDeal_MapsTheArrowWithTheProgramSlotRule()
        {
            var cranker = SyntheticKey(1);
            var devLawyer = SyntheticKey(2);
            var spvLawyer = SyntheticKey(4);
            var developer = SyntheticKey(7);
            var xcavMint = SyntheticKey(8);
            var tokenX = SyntheticKey(18);
            var regionOwner = SyntheticKey(9);
            var treasury = SyntheticKey(10);
            var mint1 = SyntheticKey(11);
            var token1 = SyntheticKey(21);
            var mint2 = SyntheticKey(12);
            var token2 = SyntheticKey(22);

            var arrow = new XcavateMarketplaceIndexerModel.PendingFinalizations.ExecuteDealFinalization(
                RegionId: 3, developer.Key, devLawyer.Key, spvLawyer.Key, xcavMint.Key,
                [mint1.Key, mint2.Key]);

            var instruction = XcavateMarketplaceCallsModel.BuildExecuteDeal(
                Programs, cranker.Key, listingId: 9, arrow, regionOwner.Key, treasury.Key,
                new Dictionary<string, PublicKey>
                {
                    [xcavMint.Key] = tokenX,
                    [mint1.Key] = token1,
                    [mint2.Key] = token2,
                });

            var listingVault = XcavateMarketplaceProgram.DeriveListingVault(Programs, 9);

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Data[..8], Is.EqualTo(Discriminator("execute_deal")));
                // 13 base accounts + one 7-account payout group per collected mint.
                Assert.That(instruction.Keys, Has.Count.EqualTo(13 + 7 * 2));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(
                    instruction.Keys[4].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveRegion(Programs, 3).Key));
                Assert.That(
                    instruction.Keys[5].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveLawyer(Programs, devLawyer).Key));
                Assert.That(
                    instruction.Keys[6].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveLawyer(Programs, spvLawyer).Key));
                Assert.That(instruction.Keys[8].PublicKey, Is.EqualTo(xcavMint.Key));
                Assert.That(instruction.Keys[11].PublicKey, Is.EqualTo(tokenX.Key));
                // The payment-token-program slot is the FIRST collected mint's program
                // - not the last mint's, not always the XCAV mint's.
                Assert.That(instruction.Keys[12].PublicKey, Is.EqualTo(token1.Key));

                // The payout groups follow the arrow's collected order, each with the
                // mint's own token program.
                Assert.That(
                    instruction.Keys.Skip(13).Take(7).Select(key => key.PublicKey).ToArray(),
                    Is.EqualTo(new[]
                    {
                        mint1.Key,
                        SolanaAssociatedTokenAccount.Derive(listingVault, mint1, token1).Key,
                        SolanaAssociatedTokenAccount.Derive(developer, mint1, token1).Key,
                        SolanaAssociatedTokenAccount.Derive(devLawyer, mint1, token1).Key,
                        SolanaAssociatedTokenAccount.Derive(spvLawyer, mint1, token1).Key,
                        SolanaAssociatedTokenAccount.Derive(regionOwner, mint1, token1).Key,
                        SolanaAssociatedTokenAccount.Derive(treasury, mint1, token1).Key,
                    }));
                Assert.That(
                    instruction.Keys.Skip(20).Take(7).Select(key => key.PublicKey).ToArray(),
                    Is.EqualTo(new[]
                    {
                        mint2.Key,
                        SolanaAssociatedTokenAccount.Derive(listingVault, mint2, token2).Key,
                        SolanaAssociatedTokenAccount.Derive(developer, mint2, token2).Key,
                        SolanaAssociatedTokenAccount.Derive(devLawyer, mint2, token2).Key,
                        SolanaAssociatedTokenAccount.Derive(spvLawyer, mint2, token2).Key,
                        SolanaAssociatedTokenAccount.Derive(regionOwner, mint2, token2).Key,
                        SolanaAssociatedTokenAccount.Derive(treasury, mint2, token2).Key,
                    }));
            });
        }

        [Test]
        public void BuildExecuteDeal_PassesTheXcavProgramWhenNothingWasCollected()
        {
            var xcavMint = SyntheticKey(8);
            var tokenX = SyntheticKey(18);

            var arrow = new XcavateMarketplaceIndexerModel.PendingFinalizations.ExecuteDealFinalization(
                RegionId: 3, SyntheticKey(7).Key, SyntheticKey(2).Key, SyntheticKey(4).Key,
                xcavMint.Key, []);

            var instruction = XcavateMarketplaceCallsModel.BuildExecuteDeal(
                Programs, SyntheticKey(1).Key, listingId: 9, arrow, SyntheticKey(9).Key,
                SyntheticKey(10).Key,
                new Dictionary<string, PublicKey> { [xcavMint.Key] = tokenX });

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Keys, Has.Count.EqualTo(13));
                // With no collected mints the program slot is an unused pass-through -
                // the XCAV mint's program.
                Assert.That(instruction.Keys[12].PublicKey, Is.EqualTo(tokenX.Key));
            });
        }

        [Test]
        public void BuildExecuteDeal_RejectsAnUnresolvedMint()
        {
            var xcavMint = SyntheticKey(8);
            var mint1 = SyntheticKey(11);

            var arrow = new XcavateMarketplaceIndexerModel.PendingFinalizations.ExecuteDealFinalization(
                RegionId: 3, SyntheticKey(7).Key, SyntheticKey(2).Key, SyntheticKey(4).Key,
                xcavMint.Key, [mint1.Key]);

            Assert.Multiple(() =>
            {
                // A missing collected mint's program...
                Assert.Throws<InvalidOperationException>(() =>
                    XcavateMarketplaceCallsModel.BuildExecuteDeal(
                        Programs, SyntheticKey(1).Key, listingId: 9, arrow, SyntheticKey(9).Key,
                        SyntheticKey(10).Key,
                        new Dictionary<string, PublicKey> { [xcavMint.Key] = SyntheticKey(18) }));
                // ...and a missing XCAV mint's program both reject.
                Assert.Throws<InvalidOperationException>(() =>
                    XcavateMarketplaceCallsModel.BuildExecuteDeal(
                        Programs, SyntheticKey(1).Key, listingId: 9, arrow, SyntheticKey(9).Key,
                        SyntheticKey(10).Key,
                        new Dictionary<string, PublicKey> { [mint1.Key] = SyntheticKey(21) }));
            });
        }

        [Test]
        public void BuildResolveSilentVerdict_BuildsOnlyWhileTheGateIsOpen()
        {
            var cranker = SyntheticKey(1);

            Assert.Throws<InvalidOperationException>(() =>
                XcavateMarketplaceCallsModel.BuildResolveSilentVerdict(
                    Programs, cranker.Key, listingId: 9, silentVerdictOverdue: false));

            var instruction = XcavateMarketplaceCallsModel.BuildResolveSilentVerdict(
                Programs, cranker.Key, listingId: 9, silentVerdictOverdue: true);

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Data[..8], Is.EqualTo(Discriminator("resolve_silent_verdict")));
                Assert.That(instruction.Keys, Has.Count.EqualTo(2));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(
                    instruction.Keys[1].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveListing(Programs, 9).Key));
            });
        }

        [Test]
        public async Task AssignDeveloperLawyerAsync_ReturnsTheSingleProgramInstruction()
        {
            var developer = SyntheticKey(1);
            var lawyer = SyntheticKey(2);

            var instructions = await XcavateMarketplaceCallsModel.AssignDeveloperLawyerAsync(
                SolanaCluster.Devnet, developer.Key, 9, lawyer.Key);

            Assert.Multiple(() =>
            {
                Assert.That(instructions, Has.Count.EqualTo(1));
                Assert.That(instructions[0].Data[..8], Is.EqualTo(Discriminator("assign_developer_lawyer")));
                Assert.That(instructions[0].Keys[0].PublicKey, Is.EqualTo(developer.Key));
                Assert.That(
                    instructions[0].Keys[3].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveLawyer(Programs, lawyer).Key));
            });
        }

        [Test]
        public async Task ClaimSpvCaseAsync_ReturnsTheSingleProgramInstructionKeyedByRound()
        {
            var lawyer = SyntheticKey(1);

            var instructions = await XcavateMarketplaceCallsModel.ClaimSpvCaseAsync(
                SolanaCluster.Devnet, lawyer.Key, 9, round: 4, costsQuote: 10_000);

            Assert.Multiple(() =>
            {
                Assert.That(instructions, Has.Count.EqualTo(1));
                Assert.That(instructions[0].Data[..8], Is.EqualTo(Discriminator("claim_spv_case")));
                Assert.That(instructions[0].Keys[0].PublicKey, Is.EqualTo(lawyer.Key));
                // payer == lawyer: the single-signer path.
                Assert.That(instructions[0].Keys[1].PublicKey, Is.EqualTo(lawyer.Key));
                Assert.That(
                    instructions[0].Keys[6].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 9, 4, lawyer).Key));
            });
        }

        [Test]
        public void QuoteCostsFromDecimal_AllowsZeroAndScalesPositives()
        {
            Assert.Multiple(() =>
            {
                Assert.That(XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(0m), Is.EqualTo(0UL));
                Assert.That(XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(25m), Is.EqualTo(25_000_000_000UL));
                Assert.That(XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(0.000000001m), Is.EqualTo(1UL));
            });
        }

        [Test]
        public void QuoteCostsFromDecimal_RejectsNegativeAndOverflowing()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(-1m));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(decimal.MaxValue));
            });
        }
    }
}
