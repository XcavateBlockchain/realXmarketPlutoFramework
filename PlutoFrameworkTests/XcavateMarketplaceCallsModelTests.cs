using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Wallet;
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
    }
}
