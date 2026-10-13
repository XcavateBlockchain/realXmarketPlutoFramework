using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Wallet;
using System.Security.Cryptography;
using System.Text;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Pure instruction-shape tests for <see cref="XcavatePropertyProgram"/>, mirroring
    /// <see cref="XcavateMarketplaceProgramTests"/>. No devnet round trips: the
    /// governance accounts these PDAs address come and go with votes, so existence
    /// checks would be flaky; the seed layouts are pinned by construction tests
    /// instead.
    /// </summary>
    internal class XcavatePropertyProgramTests
    {
        private static readonly XcavateProgramSet Programs = XcavateProgramAddresses.Devnet;

        private static PublicKey SyntheticKey(byte seed) =>
            new(SolanaBase58.Encode([.. Enumerable.Repeat(seed, 32)]));

        [Test]
        [TestCase("vote_on_proposal")]
        [TestCase("vote_on_challenge")]
        [TestCase("vote_on_agent")]
        [TestCase("finalize_proposal")]
        [TestCase("unlock_proposal_votes")]
        [TestCase("unlock_challenge_votes")]
        [TestCase("unlock_agent_votes")]
        [TestCase("claim_income")]
        [TestCase("close_income_checkpoint")]
        [TestCase("finalize_challenge")]
        [TestCase("finalize_agent_election")]
        public void Discriminators_MatchTheAnchorFormula(string instructionName)
        {
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes($"global:{instructionName}"))[..8];

            var instruction = instructionName switch
            {
                "vote_on_proposal" => XcavatePropertyProgram.VoteOnProposal(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, XcavateVoteChoice.Yes, 5),
                "vote_on_challenge" => XcavatePropertyProgram.VoteOnChallenge(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, XcavateVoteChoice.No, 5),
                "vote_on_agent" => XcavatePropertyProgram.VoteOnAgent(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 1, SyntheticKey(3), null, 5),
                "finalize_proposal" => XcavatePropertyProgram.FinalizeProposal(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3),
                "unlock_proposal_votes" => XcavatePropertyProgram.UnlockProposalVotes(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3),
                "unlock_challenge_votes" => XcavatePropertyProgram.UnlockChallengeVotes(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3),
                "unlock_agent_votes" => XcavatePropertyProgram.UnlockAgentVotes(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 1),
                "claim_income" => XcavatePropertyProgram.ClaimIncome(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, SyntheticKey(3), SyntheticKey(4)),
                "close_income_checkpoint" => XcavatePropertyProgram.CloseIncomeCheckpoint(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7),
                "finalize_challenge" => XcavatePropertyProgram.FinalizeChallenge(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, null,
                    SyntheticKey(4), SyntheticKey(5), SyntheticKey(6), SyntheticKey(7)),
                "finalize_agent_election" => XcavatePropertyProgram.FinalizeAgentElection(
                    Programs, SyntheticKey(1), 7, 1, SyntheticKey(2), new[] { SyntheticKey(2) }),
                _ => throw new ArgumentOutOfRangeException(nameof(instructionName)),
            };

            Assert.That(instruction.Data[..8], Is.EqualTo(expected));
        }

        [Test]
        public void VoteInstructions_HaveTheIdlAccountShapes()
        {
            var voter = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var candidate = SyntheticKey(3);

            var proposal = XcavatePropertyProgram.VoteOnProposal(Programs, voter, payer, 7, 3, XcavateVoteChoice.Yes, 5);
            var challenge = XcavatePropertyProgram.VoteOnChallenge(Programs, voter, payer, 7, 3, XcavateVoteChoice.Yes, 5);
            var agent = XcavatePropertyProgram.VoteOnAgent(Programs, voter, payer, 7, 1, candidate, null, 5);
            var finalizeProposal = XcavatePropertyProgram.FinalizeProposal(Programs, voter, payer, 7, 3);
            var unlockProposal = XcavatePropertyProgram.UnlockProposalVotes(Programs, voter, payer, 7, 3);
            var unlockChallenge = XcavatePropertyProgram.UnlockChallengeVotes(Programs, voter, payer, 7, 3);
            var unlockAgent = XcavatePropertyProgram.UnlockAgentVotes(Programs, voter, payer, 7, 1);

            Assert.Multiple(() =>
            {
                Assert.That(proposal.Keys, Has.Count.EqualTo(10));
                Assert.That(challenge.Keys, Has.Count.EqualTo(10));
                Assert.That(agent.Keys, Has.Count.EqualTo(11));
                Assert.That(finalizeProposal.Keys, Has.Count.EqualTo(5));
                Assert.That(unlockProposal.Keys, Has.Count.EqualTo(7));
                Assert.That(unlockChallenge.Keys, Has.Count.EqualTo(7));
                Assert.That(unlockAgent.Keys, Has.Count.EqualTo(7));

                // The voter/cranker leads every instruction and is its signer, and every
                // account in the message is writable (the deployed-binary escalation
                // quirk documented on the marketplace program class).
                foreach (var instruction in new[] { proposal, challenge, agent, finalizeProposal, unlockProposal, unlockChallenge, unlockAgent })
                {
                    Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(voter.Key));
                    Assert.That(instruction.Keys[0].IsSigner, Is.True);

                    foreach (var key in instruction.Keys.Where(key => !key.IsSigner))
                    {
                        Assert.That(key.IsWritable, Is.True);
                    }
                }

                // The rent-fronting payer co-signs the votes; finalize and the unlocks
                // only hand the rent payer a refund, so it does not sign.
                foreach (var instruction in new[] { proposal, challenge, agent })
                {
                    Assert.That(instruction.Keys.Count(key => key.IsSigner), Is.EqualTo(2));
                }

                foreach (var instruction in new[] { finalizeProposal, unlockProposal, unlockChallenge, unlockAgent })
                {
                    Assert.That(instruction.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                }

                // Every instruction runs against the property program...
                foreach (var instruction in new[] { proposal, challenge, agent, finalizeProposal, unlockProposal, unlockChallenge, unlockAgent })
                {
                    Assert.That(new PublicKey(instruction.ProgramId).Key, Is.EqualTo(Programs.Property));
                }

                // ...while the holding it locks shares in is the MARKETPLACE program's
                // ShareHolding PDA, and the marketplace program itself rides along as an
                // account for the CPI.
                var holding = XcavateMarketplaceProgram.DeriveHolding(Programs, 7, voter).Key;
                Assert.That(proposal.Keys[5].PublicKey, Is.EqualTo(holding));
                Assert.That(challenge.Keys[5].PublicKey, Is.EqualTo(holding));
                Assert.That(agent.Keys[4].PublicKey, Is.EqualTo(holding));
                Assert.That(proposal.Keys[8].PublicKey, Is.EqualTo(Programs.Marketplace));
                Assert.That(challenge.Keys[8].PublicKey, Is.EqualTo(Programs.Marketplace));

                // The voter role is the whitelist's RealEstateInvestor role account.
                var role = XcavateMarketplaceProgram.DeriveRoleAccount(Programs, voter, XcavateRole.RealEstateInvestor).Key;
                Assert.That(proposal.Keys[2].PublicKey, Is.EqualTo(role));
                Assert.That(challenge.Keys[2].PublicKey, Is.EqualTo(role));
                Assert.That(agent.Keys[2].PublicKey, Is.EqualTo(role));
            });
        }

        [Test]
        public void VoteOnProposal_EncodesArgsLittleEndian()
        {
            var instruction = XcavatePropertyProgram.VoteOnProposal(
                Programs,
                voter: SyntheticKey(1),
                payer: SyntheticKey(2),
                assetId: 0x0102030405060708,
                proposalId: 3,
                choice: XcavateVoteChoice.Abstain,
                amount: 0x0C0D0E0F);

            // discriminator + u64 asset_id + u8 choice + u32 amount
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 1 + 4));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16], Is.EqualTo((byte)XcavateVoteChoice.Abstain));
            Assert.That(instruction.Data[17..21], Is.EqualTo(new byte[] { 0x0F, 0x0E, 0x0D, 0x0C }));
        }

        /// <summary>
        /// The borsh variant order of the on-chain VoteChoice enum is load-bearing:
        /// Yes=0, No=1, Abstain=2 (idls/devnet/property.json). Abstain counts toward
        /// quorum but neither side - scrambling the order silently miscasts votes.
        /// </summary>
        [Test]
        public void VoteChoice_MatchesTheOnChainVariantOrder()
        {
            Assert.That((byte)XcavateVoteChoice.Yes, Is.EqualTo(0));
            Assert.That((byte)XcavateVoteChoice.No, Is.EqualTo(1));
            Assert.That((byte)XcavateVoteChoice.Abstain, Is.EqualTo(2));
        }

        [Test]
        public void VoteOnAgent_PassesTheProgramIdForANonePreviousCandidacy()
        {
            var first = XcavatePropertyProgram.VoteOnAgent(
                Programs, SyntheticKey(1), SyntheticKey(2), 7, 1, SyntheticKey(3), null, 5);

            // Anchor encodes a None optional account as the program's own id.
            Assert.That(first.Keys[7].PublicKey, Is.EqualTo(Programs.Property));

            var revote = XcavatePropertyProgram.VoteOnAgent(
                Programs, SyntheticKey(1), SyntheticKey(2), 7, 1, SyntheticKey(3), SyntheticKey(4), 5);

            Assert.That(revote.Keys[7].PublicKey, Is.EqualTo(
                XcavatePropertyProgram.DeriveAgentCandidacy(Programs, 7, 1, SyntheticKey(4)).Key));

            // No asset id/amount arg mixup: discriminator + u64 asset_id + u32 amount.
            Assert.That(first.Data, Has.Length.EqualTo(8 + 8 + 4));
        }

        [Test]
        public void GovernancePdas_MatchTheIdlSeedLayouts()
        {
            var voter = SyntheticKey(1);
            var agent = SyntheticKey(2);
            var propertyProgram = new PublicKey(Programs.Property);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavatePropertyProgram.DeriveLetting(Programs, 7).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("letting"), new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 }).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveProposal(Programs, 7, 3).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram,
                        Encoding.UTF8.GetBytes("gov-proposal"),
                        new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 },
                        new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 }).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveChallengeVoteRecord(Programs, 7, 3, voter).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram,
                        Encoding.UTF8.GetBytes("gov-challenge-vote"),
                        new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 },
                        new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 },
                        voter.KeyBytes).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveAgentCandidacy(Programs, 7, 2, agent).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram,
                        Encoding.UTF8.GetBytes("agent-candidate"),
                        new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 },
                        new byte[] { 2, 0, 0, 0, 0, 0, 0, 0 },
                        agent.KeyBytes).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveCpiAuth(Programs).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("cpi-auth")).Key));
            });
        }

        [Test]
        public void UnlockInstructions_EncodeTheIdAndRoundAsU64()
        {
            var proposal = XcavatePropertyProgram.UnlockProposalVotes(Programs, SyntheticKey(1), SyntheticKey(2), 7, 0x0102030405060708);
            var agentVote = XcavatePropertyProgram.UnlockAgentVotes(Programs, SyntheticKey(1), SyntheticKey(2), 7, 0x0102030405060708);

            // discriminator + u64 asset_id + u64 id/round
            Assert.That(proposal.Data, Has.Length.EqualTo(8 + 8 + 8));
            Assert.That(proposal.Data[16..24], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(agentVote.Data, Has.Length.EqualTo(8 + 8 + 8));
            Assert.That(agentVote.Data[16..24], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
        }

        [Test]
        public void IncomeInstructions_HaveTheIdlAccountShapes()
        {
            var holder = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var mint = SyntheticKey(3);
            var tokenProgram = SyntheticKey(4);

            var claim = XcavatePropertyProgram.ClaimIncome(Programs, holder, payer, 7, mint, tokenProgram);
            var close = XcavatePropertyProgram.CloseIncomeCheckpoint(Programs, holder, payer, 7);

            Assert.Multiple(() =>
            {
                Assert.That(claim.Keys, Has.Count.EqualTo(11));
                Assert.That(close.Keys, Has.Count.EqualTo(4));

                // The holder signs both; the claim's rent-fronting payer co-signs, while
                // the close's rent payer only collects the refund. Every account in the
                // message is writable (the deployed-binary escalation quirk documented
                // on the class).
                Assert.That(claim.Keys[0].PublicKey, Is.EqualTo(holder.Key));
                Assert.That(claim.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(claim.Keys.Count(key => key.IsSigner), Is.EqualTo(2));
                Assert.That(close.Keys[0].PublicKey, Is.EqualTo(holder.Key));
                Assert.That(close.Keys[0].IsSigner, Is.True);
                Assert.That(close.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(close.Keys[1].IsSigner, Is.False);
                Assert.That(close.Keys.Count(key => key.IsSigner), Is.EqualTo(1));

                foreach (var key in claim.Keys.Concat(close.Keys))
                {
                    Assert.That(key.IsWritable, Is.True);
                }

                foreach (var instruction in new[] { claim, close })
                {
                    Assert.That(new PublicKey(instruction.ProgramId).Key, Is.EqualTo(Programs.Property));
                }

                // The income accumulator and the holder's checkpoint/vault are PROPERTY
                // program PDAs; the holding is the marketplace's ShareHolding.
                var income = XcavatePropertyProgram.DeriveIncome(Programs, 7);
                var checkpoint = XcavatePropertyProgram.DeriveIncomeCheckpoint(Programs, 7, holder);
                var vault = XcavatePropertyProgram.DeriveIncomeVault(Programs, 7);
                var holding = XcavateMarketplaceProgram.DeriveHolding(Programs, 7, holder);

                Assert.That(claim.Keys[2].PublicKey, Is.EqualTo(income.Key));
                Assert.That(claim.Keys[3].PublicKey, Is.EqualTo(checkpoint.Key));
                Assert.That(claim.Keys[4].PublicKey, Is.EqualTo(holding.Key));
                Assert.That(claim.Keys[5].PublicKey, Is.EqualTo(mint.Key));
                Assert.That(claim.Keys[6].PublicKey, Is.EqualTo(vault.Key));

                // The vault's payment account is pinned on-chain to the ATA derivation;
                // the holder's only has to be their mint account - the ATA is the
                // fall-through default.
                Assert.That(claim.Keys[7].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(vault, mint, tokenProgram).Key));
                Assert.That(claim.Keys[8].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(holder, mint, tokenProgram).Key));

                Assert.That(claim.Keys[9].PublicKey, Is.EqualTo(tokenProgram.Key));
                Assert.That(claim.Keys[10].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                Assert.That(close.Keys[2].PublicKey, Is.EqualTo(checkpoint.Key));
                Assert.That(close.Keys[3].PublicKey, Is.EqualTo(holding.Key));
            });
        }

        [Test]
        public void FinalizeChallenge_HasTheIdlAccountShape()
        {
            var cranker = SyntheticKey(1);
            var rentPayer = SyntheticKey(2);
            var struckAgent = SyntheticKey(3);
            var challenger = SyntheticKey(4);
            var xcavMint = SyntheticKey(5);
            var treasury = SyntheticKey(6);
            var tokenProgram = SyntheticKey(7);

            var finalize = XcavatePropertyProgram.FinalizeChallenge(
                Programs, cranker, rentPayer, 7, 3, struckAgent, challenger, xcavMint, treasury, tokenProgram);

            Assert.Multiple(() =>
            {
                Assert.That(finalize.Keys, Has.Count.EqualTo(16));

                // Only the cranker signs; the rent payer only collects the closed
                // challenge's refund.
                Assert.That(finalize.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(finalize.Keys[0].IsSigner, Is.True);
                Assert.That(finalize.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                Assert.That(finalize.Keys[1].PublicKey, Is.EqualTo(rentPayer.Key));
                Assert.That(finalize.Keys[1].IsSigner, Is.False);

                foreach (var key in finalize.Keys)
                {
                    Assert.That(key.IsWritable, Is.True);
                }

                Assert.That(new PublicKey(finalize.ProgramId).Key, Is.EqualTo(Programs.Property));

                // config/vault are the PROPERTY program's own [b"config"] / [b"vault"]
                // PDAs (its governance Config, not the marketplace's).
                Assert.That(finalize.Keys[2].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveConfig(Programs).Key));
                Assert.That(finalize.Keys[3].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveLetting(Programs, 7).Key));

                // [4] is the MARKETPLACE program's property PDA - the IDL pins
                // seeds::program = marketplace::ID for it.
                Assert.That(finalize.Keys[4].PublicKey, Is.EqualTo(XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));

                Assert.That(finalize.Keys[5].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveChallenge(Programs, 7, 3).Key));
                Assert.That(finalize.Keys[6].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveAgentEntry(Programs, struckAgent).Key));
                Assert.That(finalize.Keys[7].PublicKey, Is.EqualTo(xcavMint.Key));
                Assert.That(finalize.Keys[8].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveVault(Programs).Key));
                Assert.That(finalize.Keys[9].PublicKey, Is.EqualTo(treasury.Key));
                Assert.That(finalize.Keys[10].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(treasury, xcavMint, tokenProgram).Key));
                Assert.That(finalize.Keys[11].PublicKey, Is.EqualTo(challenger.Key));
                Assert.That(finalize.Keys[12].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(challenger, xcavMint, tokenProgram).Key));
                Assert.That(finalize.Keys[13].PublicKey, Is.EqualTo(tokenProgram.Key));
                Assert.That(finalize.Keys[14].PublicKey, Is.EqualTo(AssociatedTokenAccountProgram.ProgramIdKey.Key));
                Assert.That(finalize.Keys[15].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));
            });
        }

        [Test]
        public void FinalizeChallenge_PassesTheProgramIdForANoneAgentEntry()
        {
            var agentGone = XcavatePropertyProgram.FinalizeChallenge(
                Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, null,
                SyntheticKey(4), SyntheticKey(5), SyntheticKey(6), SyntheticKey(7));

            // Anchor encodes a None optional account as the program's own id: an agent
            // who already left the seat is past punishing.
            Assert.That(agentGone.Keys[6].PublicKey, Is.EqualTo(Programs.Property));

            var seated = XcavatePropertyProgram.FinalizeChallenge(
                Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, SyntheticKey(3),
                SyntheticKey(4), SyntheticKey(5), SyntheticKey(6), SyntheticKey(7));

            // A passed challenge against a still-seated agent slashes through their
            // registry entry, so the account has to ride along.
            Assert.That(seated.Keys[6].PublicKey, Is.EqualTo(
                XcavatePropertyProgram.DeriveAgentEntry(Programs, SyntheticKey(3)).Key));
        }

        [Test]
        public void FinalizeAgentElection_HasTheIdlAccountShape()
        {
            var cranker = SyntheticKey(1);
            var winner = SyntheticKey(2);
            var runnerUp = SyntheticKey(3);

            var finalize = XcavatePropertyProgram.FinalizeAgentElection(
                Programs, cranker, 7, 2, winner, new[] { winner, runnerUp });

            Assert.Multiple(() =>
            {
                // The four IDL accounts plus every candidacy of the round riding along
                // as a remaining account - the handler pins their count to the
                // election's candidate_count.
                Assert.That(finalize.Keys, Has.Count.EqualTo(4 + 2));

                Assert.That(finalize.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(finalize.Keys[0].IsSigner, Is.True);
                Assert.That(finalize.Keys.Count(key => key.IsSigner), Is.EqualTo(1));

                foreach (var key in finalize.Keys)
                {
                    Assert.That(key.IsWritable, Is.True);
                }

                Assert.That(new PublicKey(finalize.ProgramId).Key, Is.EqualTo(Programs.Property));

                Assert.That(finalize.Keys[1].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveLetting(Programs, 7).Key));

                // [2] is the MARKETPLACE program's property PDA, supplying the share
                // supply the quorum is measured against.
                Assert.That(finalize.Keys[2].PublicKey, Is.EqualTo(XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));

                Assert.That(finalize.Keys[3].PublicKey, Is.EqualTo(XcavatePropertyProgram.DeriveAgentEntry(Programs, winner).Key));

                // Remaining accounts: the candidacy PDAs in the order the caller
                // listed the agents, keyed by (asset_id, round, agent).
                Assert.That(finalize.Keys[4].PublicKey, Is.EqualTo(
                    XcavatePropertyProgram.DeriveAgentCandidacy(Programs, 7, 2, winner).Key));
                Assert.That(finalize.Keys[5].PublicKey, Is.EqualTo(
                    XcavatePropertyProgram.DeriveAgentCandidacy(Programs, 7, 2, runnerUp).Key));
            });
        }

        [Test]
        public void FinalizeAgentElection_PassesTheProgramIdForANoneWinnerEntry()
        {
            var noWinner = XcavatePropertyProgram.FinalizeAgentElection(
                Programs, SyntheticKey(1), 7, 2, null, new[] { SyntheticKey(2) });

            // Anchor encodes a None optional account as the program's own id.
            Assert.That(noWinner.Keys[3].PublicKey, Is.EqualTo(Programs.Property));
            Assert.That(noWinner.Keys, Has.Count.EqualTo(4 + 1));

            var withWinner = XcavatePropertyProgram.FinalizeAgentElection(
                Programs, SyntheticKey(1), 7, 2, SyntheticKey(2), new[] { SyntheticKey(2) });

            Assert.That(withWinner.Keys[3].PublicKey, Is.EqualTo(
                XcavatePropertyProgram.DeriveAgentEntry(Programs, SyntheticKey(2)).Key));
        }

        [Test]
        public void CrankAndIncomeInstructions_EncodeOnlyTheAssetIdAsU64()
        {
            var claim = XcavatePropertyProgram.ClaimIncome(
                Programs, SyntheticKey(1), SyntheticKey(2), 0x0102030405060708, SyntheticKey(3), SyntheticKey(4));
            var close = XcavatePropertyProgram.CloseIncomeCheckpoint(
                Programs, SyntheticKey(1), SyntheticKey(2), 0x0102030405060708);
            var challenge = XcavatePropertyProgram.FinalizeChallenge(
                Programs, SyntheticKey(1), SyntheticKey(2), 0x0102030405060708, 3, null,
                SyntheticKey(4), SyntheticKey(5), SyntheticKey(6), SyntheticKey(7));
            var election = XcavatePropertyProgram.FinalizeAgentElection(
                Programs, SyntheticKey(1), 0x0102030405060708, 2, null, new[] { SyntheticKey(2) });

            // Each takes a single u64 asset_id argument: discriminator + 8 bytes LE.
            // (The election's round and the challenge's id feed PDA derivations only.)
            foreach (var instruction in new[] { claim, close, challenge, election })
            {
                Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8));
                Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            }
        }

        [Test]
        public void IncomeAndConfigPdas_MatchTheIdlSeedLayouts()
        {
            var holder = SyntheticKey(1);
            var propertyProgram = new PublicKey(Programs.Property);
            var marketplaceProgram = new PublicKey(Programs.Marketplace);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavatePropertyProgram.DeriveIncome(Programs, 7).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("income"), new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 }).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveIncomeVault(Programs, 7).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("income-vault"), new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 }).Key));

                Assert.That(
                    XcavatePropertyProgram.DeriveIncomeCheckpoint(Programs, 7, holder).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram,
                        Encoding.UTF8.GetBytes("checkpoint"),
                        new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 },
                        holder.KeyBytes).Key));

                // The property program's own config and vault - same seeds as the
                // marketplace's, different program, different accounts.
                Assert.That(
                    XcavatePropertyProgram.DeriveConfig(Programs).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("config")).Key));
                Assert.That(
                    XcavatePropertyProgram.DeriveConfig(Programs).Key,
                    Is.Not.EqualTo(SolanaProgramAddress.Derive(
                        marketplaceProgram, Encoding.UTF8.GetBytes("config")).Key));
                Assert.That(
                    XcavatePropertyProgram.DeriveVault(Programs).Key,
                    Is.EqualTo(SolanaProgramAddress.Derive(
                        propertyProgram, Encoding.UTF8.GetBytes("vault")).Key));
                Assert.That(
                    XcavatePropertyProgram.DeriveVault(Programs).Key,
                    Is.Not.EqualTo(SolanaProgramAddress.Derive(
                        marketplaceProgram, Encoding.UTF8.GetBytes("vault")).Key));
            });
        }
    }
}
