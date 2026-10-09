using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
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
    }
}
