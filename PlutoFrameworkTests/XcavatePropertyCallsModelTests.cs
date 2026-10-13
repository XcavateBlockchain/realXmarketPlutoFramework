using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Wallet;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Pure-seam tests for <see cref="XcavatePropertyCallsModel"/>: the income claim
    /// composition (one claim per claimable mint, the close gated on a fully drained
    /// empty-holding checkpoint) and the finalize cranks' arrow mappings. The async
    /// wrappers' live state stays behind these seams; the instruction shapes and
    /// discriminators themselves are pinned in <see cref="XcavatePropertyProgramTests"/>.
    /// </summary>
    internal class XcavatePropertyCallsModelTests
    {
        private static readonly XcavateProgramSet Programs = XcavateProgramAddresses.Devnet;

        private static PublicKey SyntheticKey(byte seed) =>
            new(SolanaBase58.Encode([.. Enumerable.Repeat(seed, 32)]));

        private static byte[] Discriminator(string instructionName) =>
            SHA256.HashData(Encoding.UTF8.GetBytes($"global:{instructionName}"))[..8];

        private static XcavateMarketplaceIndexerModel.PropertyIncomeState Income(
            IReadOnlyList<(string Mint, BigInteger PerShare)> streams,
            IReadOnlyList<(BigInteger PerShare, decimal Pending)> entries) =>
            new()
            {
                Streams =
                [
                    .. streams.Select(s =>
                        new XcavateMarketplaceIndexerModel.PropertyIncomeState.IncomeStream(s.Mint, s.PerShare, 0L)),
                ],
                Checkpoint = new XcavateMarketplaceIndexerModel.IncomeCheckpointState(
                [
                    .. entries.Select(e =>
                        new XcavateMarketplaceIndexerModel.IncomeCheckpointState.Entry(e.PerShare, e.Pending)),
                ]),
            };

        [Test]
        public void SelectClaimableMints_KeepsLedgerOrderAndSkipsZeroClaimables()
        {
            var mintA = SyntheticKey(201);
            var mintB = SyntheticKey(202);
            var mintC = SyntheticKey(203);

            var income = Income(
                [(mintA.Key, 10), (mintB.Key, 7), (mintC.Key, 3)],
                [(0, 0m), (7, 0m), (0, 0m)]);

            // A: (10 - 0) * 4 = 40; B: fully claimed; C: (3 - 0) * 4 = 12 - ledger order kept.
            Assert.That(
                XcavatePropertyCallsModel.SelectClaimableMints(income, shares: 4),
                Is.EqualTo(new[] { mintA.Key, mintC.Key }));

            // Banked pending survives a zero holding (claim_income pays it even with the
            // ledger closed) - the close flows through it too.
            var banked = Income([(mintA.Key, 9)], [(9, 3m)]);

            Assert.Multiple(() =>
            {
                Assert.That(XcavatePropertyCallsModel.SelectClaimableMints(banked, shares: 0), Is.EqualTo(new[] { mintA.Key }));
                Assert.That(XcavatePropertyCallsModel.SelectClaimableMints(income, shares: 0), Is.Empty);
            });
        }

        [Test]
        public void ComposeIncomeClaims_ClaimsOnePerClaimableMintInLedgerOrder()
        {
            var holder = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var mintA = SyntheticKey(201);
            var tokenA = SyntheticKey(211);
            var mintC = SyntheticKey(203);
            var tokenC = SyntheticKey(213);

            var income = Income(
                [(mintA.Key, 10), (SyntheticKey(202).Key, 7), (mintC.Key, 3)],
                [(0, 0m), (7, 0m), (0, 0m)]);

            var claims = XcavatePropertyCallsModel.ComposeIncomeClaims(
                Programs,
                holder.Key,
                payer,
                assetId: 7,
                income,
                shares: 4,
                new Dictionary<string, PublicKey> { [mintA.Key] = tokenA, [mintC.Key] = tokenC });

            Assert.Multiple(() =>
            {
                // No close: shares remain, so SharesStillHeld would reject it.
                Assert.That(claims, Has.Count.EqualTo(2));
                Assert.That(claims.Select(c => c.Data[..8]), Has.All.EqualTo(Discriminator("claim_income")));
                Assert.That(claims[0].Keys[5].PublicKey, Is.EqualTo(mintA.Key));
                Assert.That(claims[1].Keys[5].PublicKey, Is.EqualTo(mintC.Key));
                Assert.That(claims[0].Keys[9].PublicKey, Is.EqualTo(tokenA.Key));
                Assert.That(claims[1].Keys[9].PublicKey, Is.EqualTo(tokenC.Key));
                Assert.That(claims[0].Keys[0].PublicKey, Is.EqualTo(holder.Key));
                Assert.That(claims[0].Keys[1].PublicKey, Is.EqualTo(payer.Key));
            });
        }

        [Test]
        public void ComposeIncomeClaims_AppendsTheCloseOnlyWhenDrainedAndEmpty()
        {
            var holder = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var mintA = SyntheticKey(201);
            var tokenA = SyntheticKey(211);

            // A: banked pending 200 (claimable at shares 0); B: synced to the ledger head
            // with nothing banked - the close cannot revert.
            var income = Income(
                [(mintA.Key, 10), (SyntheticKey(202).Key, 7)],
                [(10, 200m), (7, 0m)]);

            var claims = XcavatePropertyCallsModel.ComposeIncomeClaims(
                Programs,
                holder.Key,
                payer,
                assetId: 7,
                income,
                shares: 0,
                new Dictionary<string, PublicKey> { [mintA.Key] = tokenA });

            Assert.Multiple(() =>
            {
                Assert.That(claims, Has.Count.EqualTo(2));
                Assert.That(claims[0].Data[..8], Is.EqualTo(Discriminator("claim_income")));
                Assert.That(claims[1].Data[..8], Is.EqualTo(Discriminator("close_income_checkpoint")));
                Assert.That(claims[1].Keys[0].PublicKey, Is.EqualTo(holder.Key));
                Assert.That(claims[1].Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(
                    claims[1].Keys[2].PublicKey,
                    Is.EqualTo(XcavatePropertyProgram.DeriveIncomeCheckpoint(Programs, 7, holder).Key));
                Assert.That(
                    claims[1].Keys[3].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveHolding(Programs, 7, holder).Key));
            });
        }

        [Test]
        public void ComposeIncomeClaims_OmitsTheCloseForAStaleUnclaimedStream()
        {
            var mintA = SyntheticKey(201);

            // A: claimable banked pending; B: its cursor trails the ledger (per_share 3
            // of 7), so claiming A would bank B - the close's PendingIncome check would
            // reject, and it stays out.
            var income = Income(
                [(mintA.Key, 10), (SyntheticKey(202).Key, 7)],
                [(10, 200m), (3, 0m)]);

            var claims = XcavatePropertyCallsModel.ComposeIncomeClaims(
                Programs,
                SyntheticKey(1).Key,
                SyntheticKey(2),
                assetId: 7,
                income,
                shares: 0,
                new Dictionary<string, PublicKey> { [mintA.Key] = SyntheticKey(211) });

            Assert.Multiple(() =>
            {
                Assert.That(claims, Has.Count.EqualTo(1));
                Assert.That(claims[0].Data[..8], Is.EqualTo(Discriminator("claim_income")));
            });
        }

        [Test]
        public void ComposeIncomeClaims_RejectsAClaimableMintWithoutItsProgram()
        {
            var mintA = SyntheticKey(201);

            var income = Income([(mintA.Key, 10)], [(0, 0m)]);

            Assert.Throws<InvalidOperationException>(() =>
                XcavatePropertyCallsModel.ComposeIncomeClaims(
                    Programs,
                    SyntheticKey(1).Key,
                    SyntheticKey(2),
                    assetId: 7,
                    income,
                    shares: 4,
                    new Dictionary<string, PublicKey>()));
        }

        [Test]
        public void ComposeIncomeClaims_RejectsWhenNothingIsClaimable()
        {
            var mintA = SyntheticKey(201);

            var income = Income([(mintA.Key, 10)], [(10, 0m)]);

            Assert.Throws<InvalidOperationException>(() =>
                XcavatePropertyCallsModel.ComposeIncomeClaims(
                    Programs,
                    SyntheticKey(1).Key,
                    SyntheticKey(2),
                    assetId: 7,
                    income,
                    shares: 4,
                    new Dictionary<string, PublicKey> { [mintA.Key] = SyntheticKey(211) }));
        }

        [Test]
        public void BuildFinalizeProposal_MapsTheArrow()
        {
            var cranker = SyntheticKey(1);
            var rentPayer = SyntheticKey(2);

            var instruction = XcavatePropertyCallsModel.BuildFinalizeProposal(
                Programs,
                cranker.Key,
                assetId: 7,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ProposalFinalization(3, rentPayer.Key));

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Data[..8], Is.EqualTo(Discriminator("finalize_proposal")));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(instruction.Keys[1].PublicKey, Is.EqualTo(rentPayer.Key));
                // governance.rs (finalize_proposal) pins the property account with
                // seeds::program = marketplace::ID - the MARKETPLACE program's PDA.
                Assert.That(
                    instruction.Keys[3].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(
                    instruction.Keys[4].PublicKey,
                    Is.EqualTo(XcavatePropertyProgram.DeriveProposal(Programs, 7, 3).Key));
            });
        }

        [Test]
        public void BuildFinalizeProposal_RejectsAMissingArrow()
        {
            Assert.Throws<InvalidOperationException>(() =>
                XcavatePropertyCallsModel.BuildFinalizeProposal(
                    Programs, SyntheticKey(1).Key, assetId: 7, finalization: null));
        }

        [Test]
        public void BuildFinalizeChallenge_MapsTheArrow()
        {
            var rentPayer = SyntheticKey(2);
            var challenger = SyntheticKey(5);
            var struckAgent = SyntheticKey(6);
            var xcavMint = SyntheticKey(8);
            var treasury = SyntheticKey(10);
            var tokenProgram = SyntheticKey(13);

            var config = new XcavateMarketplaceIndexerModel.PendingFinalizations.ConfigSnapshot(
                xcavMint.Key, treasury.Key, ["usdc"]);

            var instruction = XcavatePropertyCallsModel.BuildFinalizeChallenge(
                Programs,
                SyntheticKey(1).Key,
                assetId: 7,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ChallengeFinalization(
                    3, rentPayer.Key, challenger.Key, struckAgent.Key),
                config,
                struckAgent,
                tokenProgram);

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Data[..8], Is.EqualTo(Discriminator("finalize_challenge")));
                Assert.That(instruction.Keys, Has.Count.EqualTo(16));
                Assert.That(instruction.Keys[1].PublicKey, Is.EqualTo(rentPayer.Key));
                Assert.That(
                    instruction.Keys[4].PublicKey,
                    Is.EqualTo(XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(
                    instruction.Keys[5].PublicKey,
                    Is.EqualTo(XcavatePropertyProgram.DeriveChallenge(Programs, 7, 3).Key));
                Assert.That(
                    instruction.Keys[6].PublicKey,
                    Is.EqualTo(XcavatePropertyProgram.DeriveAgentEntry(Programs, struckAgent).Key));
                Assert.That(instruction.Keys[7].PublicKey, Is.EqualTo(xcavMint.Key));
                Assert.That(instruction.Keys[9].PublicKey, Is.EqualTo(treasury.Key));
                Assert.That(instruction.Keys[11].PublicKey, Is.EqualTo(challenger.Key));
                Assert.That(instruction.Keys[13].PublicKey, Is.EqualTo(tokenProgram.Key));
            });
        }

        [Test]
        public void BuildFinalizeChallenge_EncodesADeregisteredAgentAsTheProgramSentinel()
        {
            var config = new XcavateMarketplaceIndexerModel.PendingFinalizations.ConfigSnapshot(
                SyntheticKey(8).Key, SyntheticKey(10).Key, ["usdc"]);

            var instruction = XcavatePropertyCallsModel.BuildFinalizeChallenge(
                Programs,
                SyntheticKey(1).Key,
                assetId: 7,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ChallengeFinalization(
                    3, SyntheticKey(2).Key, SyntheticKey(5).Key, SyntheticKey(6).Key),
                config,
                struckAgent: null,
                SyntheticKey(13));

            Assert.That(instruction.Keys[6].PublicKey, Is.EqualTo(new PublicKey(Programs.Property).Key));
        }

        [Test]
        public void BuildFinalizeChallenge_RejectsAMissingArrow()
        {
            var config = new XcavateMarketplaceIndexerModel.PendingFinalizations.ConfigSnapshot(
                SyntheticKey(8).Key, SyntheticKey(10).Key, ["usdc"]);

            Assert.Throws<InvalidOperationException>(() =>
                XcavatePropertyCallsModel.BuildFinalizeChallenge(
                    Programs, SyntheticKey(1).Key, assetId: 7, finalization: null, config, null, SyntheticKey(13)));
        }

        [Test]
        public void BuildFinalizeAgentElection_MapsTheRoundWinnerAndCandidacies()
        {
            var cranker = SyntheticKey(1);
            var winner = SyntheticKey(3);
            var candidates = new[] { SyntheticKey(4), SyntheticKey(5) };

            var instruction = XcavatePropertyCallsModel.BuildFinalizeAgentElection(
                Programs,
                cranker.Key,
                assetId: 7,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization(
                    winner.Key, Round: 2, [.. candidates.Select(c => c.Key)]));

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Data[..8], Is.EqualTo(Discriminator("finalize_agent_election")));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(
                    instruction.Keys[3].PublicKey,
                    Is.EqualTo(XcavatePropertyProgram.DeriveAgentEntry(Programs, winner).Key));
                Assert.That(instruction.Keys, Has.Count.EqualTo(4 + 2));
                Assert.That(
                    instruction.Keys.Skip(4).Select(key => key.PublicKey).ToArray(),
                    Is.EqualTo(candidates
                        .Select(candidate => XcavatePropertyProgram.DeriveAgentCandidacy(Programs, 7, 2, candidate).Key)
                        .ToArray()));
            });
        }

        [Test]
        public void BuildFinalizeAgentElection_EncodesATieAsTheProgramSentinel()
        {
            var instruction = XcavatePropertyCallsModel.BuildFinalizeAgentElection(
                Programs,
                SyntheticKey(1).Key,
                assetId: 7,
                new XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization(
                    null, Round: 2, [SyntheticKey(4).Key]));

            Assert.That(instruction.Keys[3].PublicKey, Is.EqualTo(new PublicKey(Programs.Property).Key));
        }

        [Test]
        public void BuildFinalizeAgentElection_RejectsAMissingArrow()
        {
            Assert.Throws<InvalidOperationException>(() =>
                XcavatePropertyCallsModel.BuildFinalizeAgentElection(
                    Programs, SyntheticKey(1).Key, assetId: 7, finalization: null));
        }
    }
}
