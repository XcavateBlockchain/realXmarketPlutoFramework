using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Builds the Solana property-program governance transactions behind the property
    /// pages: the investor's votes on the letting seat's spending proposals, challenges
    /// against the sitting agent, and agent-election candidacy picks; the investor's
    /// income claims; and the permissionless finalize/unlock cranks the pages offer once
    /// a vote or election closed. Pure instruction encoding lives in
    /// <see cref="XcavatePropertyProgram"/>; this layer resolves what the encoding needs
    /// from live state: the rent collector (pinned as the rent-fronting payer exactly
    /// the way <see cref="XcavateMarketplaceCallsModel.VoteOnTermsAsync"/> does, so the
    /// transaction model's two-signer flow covers the votes and the first income claim
    /// unchanged), the pending-finalization arrows the indexer reads out of the letting
    /// state, and the mint token programs and struck-agent registration only the chain
    /// answers.
    /// </summary>
    public static class XcavatePropertyCallsModel
    {
        /// <summary>
        /// vote_on_proposal for <paramref name="amount"/> shares: yes/no/abstain on the
        /// seat's live spending proposal. The shares lock inside the marketplace
        /// ShareHolding (the program CPIs back for the lock) until unlock_proposal_votes.
        /// <paramref name="proposalId"/> is the on-chain id, read from the indexer's
        /// proposals query first - it keys the proposal PDA and the vote record.
        /// </summary>
        public static async Task<List<TransactionInstruction>> VoteOnProposalAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            long proposalId,
            XcavateVoteChoice choice,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.VoteOnProposal(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    (ulong)proposalId,
                    choice,
                    amount),
            ];
        }

        /// <summary>
        /// vote_on_challenge for <paramref name="amount"/> shares: yes backs the
        /// challenger (a slash + strike for the sitting agent; three strikes remove it),
        /// no backs the agent. Same lock/rent shape as <see cref="VoteOnProposalAsync"/>.
        /// </summary>
        public static async Task<List<TransactionInstruction>> VoteOnChallengeAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            long challengeId,
            XcavateVoteChoice choice,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.VoteOnChallenge(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    (ulong)challengeId,
                    choice,
                    amount),
            ];
        }

        /// <summary>
        /// vote_on_agent for <paramref name="amount"/> shares: backs one candidacy in the
        /// seat's live election round. <paramref name="round"/> is the round being voted -
        /// read from the indexer's letting first: a stale round fails the vote record's
        /// seed check. Revotes resolve the previous candidacy from the indexer's vote of
        /// that SAME round: a switch to a different candidacy carries it along (on-chain
        /// requires it), a same-target revote or a first vote passes None.
        /// </summary>
        public static async Task<List<TransactionInstruction>> VoteOnAgentAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            ulong round,
            string agent,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            var election = await XcavateMarketplaceIndexerModel
                .GetAgentElectionStateAsync(cluster, assetId, voter, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.VoteOnAgent(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    round,
                    new PublicKey(agent),
                    XcavateMarketplaceCallsModel.ResolvePreviousCandidate(
                        (long)round,
                        election.MyVote is { } vote
                            ? [new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(
                                election.Round, vote.Candidate, vote.Power)]
                            : [],
                        agent),
                    amount),
            ];
        }

        /// <summary>
        /// claim_income for every mint the holder can pull on asset
        /// <paramref name="assetId"/> - one instruction per claimable mint - plus ONE
        /// close_income_checkpoint once the checkpoint can owe nothing more: every
        /// stream settled, nothing banked, and NO SHARES LEFT. The on-chain close pins
        /// the marketplace holding and rejects the checkpoint while it holds shares
        /// (<c>SharesStillHeld</c>) or any entry still banks income
        /// (<c>PendingIncome</c>) - income.rs close_income_checkpoint_handler - so a
        /// claim-while-holding never gets a close half-signed on-chain. Two signers -
        /// the holder and the rent collector, which fronts the checkpoint's rent on the
        /// first claim exactly like a purchase (see
        /// <see cref="XcavateMarketplaceCallsModel"/>'s payer remarks).
        /// </summary>
        public static async Task<List<TransactionInstruction>> ClaimIncomeAsync(
            SolanaCluster cluster,
            string holder,
            long assetId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            var income = await XcavateMarketplaceIndexerModel
                .GetIncomeStateAsync(cluster, assetId, holder, SolanaRpcModel.GetClient(cluster), token)
                .ConfigureAwait(false);

            var governance = await XcavateMarketplaceIndexerModel
                .GetGovernanceStateAsync(cluster, assetId, assetId, holder, token)
                .ConfigureAwait(false);
            var shares = governance.Holding?.Amount ?? 0u;

            var payer = new PublicKey(config.RentCollector);

            var tokenPrograms = new Dictionary<string, PublicKey>();
            foreach (var mint in SelectClaimableMints(income, shares))
            {
                var (tokenProgram, _) = await XcavateMarketplaceCallsModel
                    .ResolveMintAsync(cluster, new PublicKey(mint), token)
                    .ConfigureAwait(false);
                tokenPrograms[mint] = tokenProgram;
            }

            return ComposeIncomeClaims(programs, holder, payer, assetId, income, shares, tokenPrograms);
        }

        /// <summary>
        /// The income mints a holder of <paramref name="shares"/> shares can pull right
        /// now (claimable &gt; 0), in ledger order - one claim_income instruction settles
        /// the holder in ONE stream, so a property paying several mints composes one
        /// instruction per mint.
        /// </summary>
        public static IReadOnlyList<string> SelectClaimableMints(
            XcavateMarketplaceIndexerModel.PropertyIncomeState income, ulong shares) =>
            [.. income.Streams
                .Where(stream => income.GetClaimable(stream.Mint, shares) > 0)
                .Select(stream => stream.Mint)];

        /// <summary>
        /// The claim_income set for what the holder can pull: one instruction per mint of
        /// <see cref="SelectClaimableMints"/> (its token program from
        /// <paramref name="tokenProgramByMint"/>), then ONE close_income_checkpoint
        /// reaping the checkpoint's rent - appended only when the close cannot revert:
        /// every stream settled (claimed here, or already synced to the ledger head with
        /// nothing banked) AND <paramref name="shares"/> is zero. Throws when nothing is
        /// claimable - the UI's banner out of the same indexer state means the caller
        /// only gets here when there is income to pull.
        /// </summary>
        public static List<TransactionInstruction> ComposeIncomeClaims(
            XcavateProgramSet programs,
            string holder,
            PublicKey payer,
            long assetId,
            XcavateMarketplaceIndexerModel.PropertyIncomeState income,
            ulong shares,
            IReadOnlyDictionary<string, PublicKey> tokenProgramByMint)
        {
            var claimable = SelectClaimableMints(income, shares);
            if (claimable.Count == 0)
            {
                throw new InvalidOperationException("There is no income to claim on this property.");
            }

            var holderKey = new PublicKey(holder);

            var claims = claimable
                .Select(mint => XcavatePropertyProgram.ClaimIncome(
                    programs,
                    holderKey,
                    payer,
                    (ulong)assetId,
                    new PublicKey(mint),
                    tokenProgramByMint.TryGetValue(mint, out var tokenProgram)
                        ? tokenProgram
                        : throw new InvalidOperationException($"No token program resolved for income mint {mint}.")))
                .ToList();

            // The close's on-chain guards, predicted: the claimed streams settle with the
            // claims above; the rest must ALREADY sit at the ledger head with nothing
            // banked (a stale cursor would bank income the close then pins).
            var allSettled = income.Streams
                .Select((stream, index) =>
                {
                    if (claimable.Contains(stream.Mint))
                    {
                        return true;
                    }

                    var entry = index < income.Checkpoint.Entries.Count ? income.Checkpoint.Entries[index] : null;
                    return entry is not null && entry.PerShare == stream.PerShare && entry.Pending == 0m;
                })
                .All(settles => settles);

            if (allSettled && shares == 0)
            {
                claims.Add(XcavatePropertyProgram.CloseIncomeCheckpoint(
                    programs, holderKey, payer, (ulong)assetId));
            }

            return claims;
        }

        /// <summary>
        /// finalize_proposal for the property's ended spending proposal - the
        /// permissionless crank the proposals page offers once voting closed. The
        /// indexer answers the proposal id and its rent payer as one
        /// pending-finalization arrow; a stale banner (still open, or already settled)
        /// rejects here. Single signer - the cranker.
        /// </summary>
        public static async Task<List<TransactionInstruction>> FinalizeProposalAsync(
            SolanaCluster cluster,
            string cranker,
            long assetId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, assetId, assetId, cranker, token)
                .ConfigureAwait(false);

            return [ BuildFinalizeProposal(programs, cranker, assetId, pending.Proposal) ];
        }

        /// <summary>The finalize_proposal instruction for a pending proposal arrow, or a user-presentable rejection when the arrow is absent.</summary>
        public static TransactionInstruction BuildFinalizeProposal(
            XcavateProgramSet programs,
            string cranker,
            long assetId,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ProposalFinalization? finalization)
        {
            var arrow = XcavateMarketplaceCallsModel.RequireFinalization(finalization, "proposal finalization");

            return XcavatePropertyProgram.FinalizeProposal(
                programs,
                new PublicKey(cranker),
                new PublicKey(arrow.RentPayer),
                (ulong)assetId,
                (ulong)arrow.ProposalId);
        }

        /// <summary>
        /// finalize_challenge for the property's ended challenge - the permissionless
        /// crank settling the slash/versus payouts once voting closed. The indexer
        /// answers the challenge id, its rent payer and the two payout parties; the
        /// struck agent's registry entry rides along only while the agent is still
        /// REGISTERED, which the chain answers on the spot (an agent that already left
        /// passes the None sentinel - the builder encodes it from a null). The XCAV
        /// mint's token program comes from the chain too. Single signer - the cranker.
        /// </summary>
        public static async Task<List<TransactionInstruction>> FinalizeChallengeAsync(
            SolanaCluster cluster,
            string cranker,
            long assetId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, assetId, assetId, cranker, token)
                .ConfigureAwait(false);

            var arrow = XcavateMarketplaceCallsModel.RequireFinalization(pending.Challenge, "challenge finalization");
            var config = pending.Config
                ?? throw new InvalidOperationException($"The marketplace is not initialized on {cluster.GetName()}.");

            var struckAgentKey = new PublicKey(arrow.StruckAgent);
            var struckAgent = await SolanaRpcModel
                .GetAccountInfoAsync(cluster, XcavatePropertyProgram.DeriveAgentEntry(programs, struckAgentKey).Key, token)
                .ConfigureAwait(false) is not null
                    ? struckAgentKey
                    : (PublicKey?)null;

            var (xcavTokenProgram, _) = await XcavateMarketplaceCallsModel
                .ResolveMintAsync(cluster, new PublicKey(config.XcavMint), token)
                .ConfigureAwait(false);

            return [ BuildFinalizeChallenge(programs, cranker, assetId, arrow, config, struckAgent, xcavTokenProgram) ];
        }

        /// <summary>The finalize_challenge instruction for a pending challenge arrow, or a user-presentable rejection when the arrow is absent.</summary>
        public static TransactionInstruction BuildFinalizeChallenge(
            XcavateProgramSet programs,
            string cranker,
            long assetId,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ChallengeFinalization? finalization,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ConfigSnapshot config,
            PublicKey? struckAgent,
            PublicKey xcavTokenProgram)
        {
            var arrow = XcavateMarketplaceCallsModel.RequireFinalization(finalization, "challenge finalization");

            return XcavatePropertyProgram.FinalizeChallenge(
                programs,
                new PublicKey(cranker),
                new PublicKey(arrow.RentPayer),
                (ulong)assetId,
                (ulong)arrow.ChallengeId,
                struckAgent,
                new PublicKey(arrow.Challenger),
                new PublicKey(config.XcavMint),
                new PublicKey(config.Treasury),
                xcavTokenProgram);
        }

        /// <summary>
        /// finalize_agent_election for the seat's ended election - the permissionless
        /// crank seating the plurality winner (or reopening the round on a tie). The
        /// indexer answers the settled round, the unique leader and the round's
        /// candidates as one pending-finalization arrow; the candidacy accounts are
        /// seeded with that round (see <see cref="XcavatePropertyProgram.FinalizeAgentElection"/>),
        /// so a stale letter fails the PDA derivation. Single signer - the cranker.
        /// </summary>
        public static async Task<List<TransactionInstruction>> FinalizeAgentElectionAsync(
            SolanaCluster cluster,
            string cranker,
            long assetId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, assetId, assetId, cranker, token)
                .ConfigureAwait(false);

            return [ BuildFinalizeAgentElection(programs, cranker, assetId, pending.AgentElection) ];
        }

        /// <summary>The finalize_agent_election instruction for a pending election arrow, or a user-presentable rejection when the arrow is absent.</summary>
        public static TransactionInstruction BuildFinalizeAgentElection(
            XcavateProgramSet programs,
            string cranker,
            long assetId,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization? finalization)
        {
            var arrow = XcavateMarketplaceCallsModel.RequireFinalization(finalization, "agent election finalization");

            return XcavatePropertyProgram.FinalizeAgentElection(
                programs,
                new PublicKey(cranker),
                (ulong)assetId,
                (ulong)arrow.Round,
                arrow.Winner is null ? null : new PublicKey(arrow.Winner),
                [.. arrow.Candidates.Select(candidate => new PublicKey(candidate))]);
        }

        /// <summary>
        /// unlock_proposal_votes: releases the shares the voter's proposal vote locked
        /// inside the marketplace holding - and reclaims the vote record's rent - once
        /// the proposal finalized. The rent returns to the config's rent collector, the
        /// wallet that fronted it at vote time (the vote's recorded rent payer, no
        /// signature). Single signer - the voter.
        /// </summary>
        public static async Task<List<TransactionInstruction>> UnlockProposalVotesAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            long proposalId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.UnlockProposalVotes(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    (ulong)proposalId),
            ];
        }

        /// <summary>The challenge counterpart of <see cref="UnlockProposalVotesAsync"/>.</summary>
        public static async Task<List<TransactionInstruction>> UnlockChallengeVotesAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            long challengeId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.UnlockChallengeVotes(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    (ulong)challengeId),
            ];
        }

        /// <summary>
        /// unlock_agent_votes: releases the shares the voter backed a candidacy with
        /// once election <paramref name="round"/> is over, and reclaims the vote
        /// record's rent the same way <see cref="UnlockProposalVotesAsync"/> does.
        /// Single signer - the voter.
        /// </summary>
        public static async Task<List<TransactionInstruction>> UnlockAgentVotesAsync(
            SolanaCluster cluster,
            string voter,
            long assetId,
            ulong round,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceCallsModel.GetMarketplaceConfigAsync(cluster, token)
                .ConfigureAwait(false);

            return
            [
                XcavatePropertyProgram.UnlockAgentVotes(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    round),
            ];
        }
    }
}
