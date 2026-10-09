using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using System.Buffers.Binary;
using System.Text;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// A governance vote's direction, the property program's VoteChoice enum. Borsh
    /// order is load-bearing: the instruction data carries the variant index as one
    /// byte. Abstain counts toward quorum but neither side.
    /// </summary>
    public enum XcavateVoteChoice : byte
    {
        Yes = 0,
        No = 1,
        Abstain = 2,
    }

    /// <summary>
    /// Hand-built instructions of the Xcavate property Solana program's governance
    /// surface, transcribed from <c>idls/devnet/property.json</c> the same way
    /// <see cref="XcavateMarketplaceProgram"/> transcribes the marketplace IDL. Covered:
    /// the post-settlement votes an investor casts from the property pages - spending
    /// proposals, challenges against the sitting letting agent, and letting-agent
    /// elections - plus the cheap finalize/unlock cranks. The token-moving instructions
    /// (propose, challenge_agent, finalize_challenge, income) are out of scope for the
    /// app today.
    /// <para>
    /// Two cross-program accounts recur: <c>holding</c> is the MARKETPLACE program's
    /// ShareHolding PDA (the property program CPIs back into the marketplace to move the
    /// vote's share lock), and <c>cpi_auth</c> is the PROPERTY program's own signer PDA
    /// for that CPI. Both are derived here from the program set.
    /// </para>
    /// <para>
    /// Writable flags deliberately do NOT follow the IDL - the same deployed-binary
    /// quirk <see cref="XcavateMarketplaceProgram"/> documents applies here: every
    /// account meta is writable, because the deployed programs escalate the whole
    /// message and a readonly non-signer dies with PrivilegeEscalation in simulation.
    /// </para>
    /// </summary>
    public static class XcavatePropertyProgram
    {
        // Anchor instruction discriminators: sha256("global:<instruction_name>")[0..8],
        // as listed in the IDL. VerifyDiscriminators in the tests recomputes them.
        private static readonly byte[] VoteOnProposalDiscriminator = [188, 239, 13, 88, 119, 199, 251, 119];
        private static readonly byte[] VoteOnChallengeDiscriminator = [19, 36, 128, 75, 123, 135, 79, 64];
        private static readonly byte[] VoteOnAgentDiscriminator = [203, 127, 40, 102, 254, 243, 11, 244];
        private static readonly byte[] FinalizeProposalDiscriminator = [23, 68, 51, 167, 109, 173, 187, 164];
        private static readonly byte[] UnlockProposalVotesDiscriminator = [102, 99, 64, 235, 192, 142, 63, 49];
        private static readonly byte[] UnlockChallengeVotesDiscriminator = [141, 196, 219, 149, 47, 78, 103, 248];
        private static readonly byte[] UnlockAgentVotesDiscriminator = [181, 212, 17, 222, 41, 184, 97, 159];

        // PDA seed prefixes, from the IDL's constants section.
        private static readonly byte[] LettingSeed = Encoding.UTF8.GetBytes("letting");
        private static readonly byte[] PropertySeed = Encoding.UTF8.GetBytes("property");
        private static readonly byte[] ProposalSeed = Encoding.UTF8.GetBytes("gov-proposal");
        private static readonly byte[] ChallengeSeed = Encoding.UTF8.GetBytes("gov-challenge");
        private static readonly byte[] ProposalVoteSeed = Encoding.UTF8.GetBytes("gov-proposal-vote");
        private static readonly byte[] ChallengeVoteSeed = Encoding.UTF8.GetBytes("gov-challenge-vote");
        private static readonly byte[] AgentCandidacySeed = Encoding.UTF8.GetBytes("agent-candidate");
        private static readonly byte[] AgentVoteSeed = Encoding.UTF8.GetBytes("agent-vote");
        private static readonly byte[] AgentSeed = Encoding.UTF8.GetBytes("agent");
        private static readonly byte[] CpiAuthSeed = Encoding.UTF8.GetBytes("cpi-auth");

        /// <summary>A property's letting seat and current election: [b"letting", asset_id].</summary>
        public static PublicKey DeriveLetting(XcavateProgramSet programs, ulong assetId) =>
            SolanaProgramAddress.Derive(new(programs.Property), LettingSeed, U64(assetId));

        /// <summary>
        /// The property program's own asset account: [b"property", asset_id]. Distinct from
        /// the marketplace program's property PDA of the same seed shape
        /// (<see cref="XcavateMarketplaceProgram.DeriveProperty"/>) - same seeds, different
        /// program, different account.
        /// </summary>
        public static PublicKey DeriveProperty(XcavateProgramSet programs, ulong assetId) =>
            SolanaProgramAddress.Derive(new(programs.Property), PropertySeed, U64(assetId));

        /// <summary>One spending proposal: [b"gov-proposal", asset_id, proposal_id].</summary>
        public static PublicKey DeriveProposal(XcavateProgramSet programs, ulong assetId, ulong proposalId) =>
            SolanaProgramAddress.Derive(new(programs.Property), ProposalSeed, U64(assetId), U64(proposalId));

        /// <summary>One challenge against the sitting agent: [b"gov-challenge", asset_id, challenge_id].</summary>
        public static PublicKey DeriveChallenge(XcavateProgramSet programs, ulong assetId, ulong challengeId) =>
            SolanaProgramAddress.Derive(new(programs.Property), ChallengeSeed, U64(assetId), U64(challengeId));

        /// <summary>One voter's vote record on one proposal: [b"gov-proposal-vote", asset_id, proposal_id, voter].</summary>
        public static PublicKey DeriveProposalVoteRecord(XcavateProgramSet programs, ulong assetId, ulong proposalId, PublicKey voter) =>
            SolanaProgramAddress.Derive(new(programs.Property), ProposalVoteSeed, U64(assetId), U64(proposalId), voter.KeyBytes);

        /// <summary>One voter's vote record on one challenge: [b"gov-challenge-vote", asset_id, challenge_id, voter].</summary>
        public static PublicKey DeriveChallengeVoteRecord(XcavateProgramSet programs, ulong assetId, ulong challengeId, PublicKey voter) =>
            SolanaProgramAddress.Derive(new(programs.Property), ChallengeVoteSeed, U64(assetId), U64(challengeId), voter.KeyBytes);

        /// <summary>One agent's candidacy in one election round: [b"agent-candidate", asset_id, round, agent].</summary>
        public static PublicKey DeriveAgentCandidacy(XcavateProgramSet programs, ulong assetId, ulong round, PublicKey agent) =>
            SolanaProgramAddress.Derive(new(programs.Property), AgentCandidacySeed, U64(assetId), U64(round), agent.KeyBytes);

        /// <summary>One voter's vote record in one election round: [b"agent-vote", asset_id, round, voter].</summary>
        public static PublicKey DeriveAgentVoteRecord(XcavateProgramSet programs, ulong assetId, ulong round, PublicKey voter) =>
            SolanaProgramAddress.Derive(new(programs.Property), AgentVoteSeed, U64(assetId), U64(round), voter.KeyBytes);

        /// <summary>A registered letting agent's registry entry: [b"agent", agent].</summary>
        public static PublicKey DeriveAgentEntry(XcavateProgramSet programs, PublicKey agent) =>
            SolanaProgramAddress.Derive(new(programs.Property), AgentSeed, agent.KeyBytes);

        /// <summary>
        /// The property program's CPI signer PDA, [b"cpi-auth"] under the property
        /// program - it signs the cross-program call that moves the vote's share lock in
        /// the marketplace's ShareHolding.
        /// </summary>
        public static PublicKey DeriveCpiAuth(XcavateProgramSet programs) =>
            SolanaProgramAddress.Derive(new(programs.Property), CpiAuthSeed);

        /// <summary>
        /// vote_on_proposal(asset_id, choice, amount): a holder's share-weighted vote on
        /// the letting seat's active spending proposal. The shares stay locked in the
        /// marketplace ShareHolding until <see cref="UnlockProposalVotes"/>; revoting
        /// reuses the record and moves the power.
        /// </summary>
        /// <param name="payer">
        /// Whoever fronts the vote record's rent - the program accepts any willing
        /// wallet; the app sends the config's rent collector, so the record's refund at
        /// unlock returns to the sponsor.
        /// </param>
        public static TransactionInstruction VoteOnProposal(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey payer,
            ulong assetId,
            ulong proposalId,
            XcavateVoteChoice choice,
            uint amount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveRoleAccount(programs, voter, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveLetting(programs, assetId), false),
                    AccountMeta.Writable(DeriveProposal(programs, assetId, proposalId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveProposalVoteRecord(programs, assetId, proposalId, voter), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(VoteOnProposalDiscriminator, U64(assetId), [(byte)choice], U32(amount)),
            };
        }

        /// <summary>
        /// vote_on_challenge(asset_id, choice, amount): a holder's share-weighted vote on
        /// the active challenge against the sitting letting agent. Same account shape as
        /// <see cref="VoteOnProposal"/> with the challenge's PDAs.
        /// </summary>
        public static TransactionInstruction VoteOnChallenge(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey payer,
            ulong assetId,
            ulong challengeId,
            XcavateVoteChoice choice,
            uint amount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveRoleAccount(programs, voter, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveLetting(programs, assetId), false),
                    AccountMeta.Writable(DeriveChallenge(programs, assetId, challengeId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveChallengeVoteRecord(programs, assetId, challengeId, voter), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(VoteOnChallengeDiscriminator, U64(assetId), [(byte)choice], U32(amount)),
            };
        }

        /// <summary>
        /// vote_on_agent(asset_id, amount): a holder's share-weighted vote for one
        /// letting-agent candidate in the current election round. The candidacy account
        /// carries the tally; <paramref name="previousCandidate"/> is the candidate a
        /// revote moves power away from - required exactly when the voter's recorded
        /// vote backs a different candidate, and passed as the property program's own id
        /// (Anchor's None encoding for an optional account) otherwise.
        /// </summary>
        public static TransactionInstruction VoteOnAgent(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey payer,
            ulong assetId,
            ulong round,
            PublicKey candidate,
            PublicKey? previousCandidate,
            uint amount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveRoleAccount(programs, voter, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveLetting(programs, assetId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveAgentVoteRecord(programs, assetId, round, voter), false),
                    AccountMeta.Writable(DeriveAgentCandidacy(programs, assetId, round, candidate), false),
                    AccountMeta.Writable(
                        previousCandidate is null
                            ? new PublicKey(programs.Property)
                            : DeriveAgentCandidacy(programs, assetId, round, previousCandidate),
                        false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(VoteOnAgentDiscriminator, U64(assetId), U32(amount)),
            };
        }

        /// <summary>
        /// finalize_proposal(asset_id): settles the seat's active proposal once its window
        /// closed. Permissionless; the proposal account closes and its rent returns to
        /// <paramref name="rentPayer"/> (the recorded rent payer).
        /// </summary>
        public static TransactionInstruction FinalizeProposal(
            XcavateProgramSet programs,
            PublicKey cranker,
            PublicKey rentPayer,
            ulong assetId,
            ulong proposalId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(cranker, true),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveLetting(programs, assetId), false),
                    AccountMeta.Writable(DeriveProperty(programs, assetId), false),
                    AccountMeta.Writable(DeriveProposal(programs, assetId, proposalId), false),
                ],
                Data = Encode(FinalizeProposalDiscriminator, U64(assetId)),
            };
        }

        /// <summary>
        /// unlock_proposal_votes(asset_id, id): releases the shares a proposal vote
        /// locked once the proposal is finalized, closing the vote record. The rent
        /// returns to <paramref name="rentPayer"/> (the recorded rent payer, no
        /// signature needed). Not role-gated.
        /// </summary>
        public static TransactionInstruction UnlockProposalVotes(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey rentPayer,
            ulong assetId,
            ulong proposalId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveProposal(programs, assetId, proposalId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveProposalVoteRecord(programs, assetId, proposalId, voter), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                ],
                Data = Encode(UnlockProposalVotesDiscriminator, U64(assetId), U64(proposalId)),
            };
        }

        /// <summary>
        /// unlock_challenge_votes(asset_id, id): the challenge counterpart of
        /// <see cref="UnlockProposalVotes"/>.
        /// </summary>
        public static TransactionInstruction UnlockChallengeVotes(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey rentPayer,
            ulong assetId,
            ulong challengeId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveChallenge(programs, assetId, challengeId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveChallengeVoteRecord(programs, assetId, challengeId, voter), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                ],
                Data = Encode(UnlockChallengeVotesDiscriminator, U64(assetId), U64(challengeId)),
            };
        }

        /// <summary>
        /// unlock_agent_votes(asset_id, round): releases the shares an election vote
        /// locked once that round is over, closing the vote record. The rent returns to
        /// <paramref name="rentPayer"/> (the recorded rent payer). Not role-gated.
        /// </summary>
        public static TransactionInstruction UnlockAgentVotes(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey rentPayer,
            ulong assetId,
            ulong round)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Property).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveLetting(programs, assetId), false),
                    AccountMeta.Writable(XcavateMarketplaceProgram.DeriveHolding(programs, assetId, voter), false),
                    AccountMeta.Writable(DeriveAgentVoteRecord(programs, assetId, round, voter), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(new PublicKey(programs.Marketplace), false),
                ],
                Data = Encode(UnlockAgentVotesDiscriminator, U64(assetId), U64(round)),
            };
        }

        private static byte[] U64(ulong value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] U32(uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] Encode(byte[] discriminator, params byte[][] args)
        {
            var data = new List<byte>(discriminator);

            foreach (var arg in args)
            {
                data.AddRange(arg);
            }

            return [.. data];
        }
    }
}
