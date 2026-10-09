using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Builds the Solana property-program governance transactions behind the property
    /// pages: the investor's votes on the letting seat's spending proposals, challenges
    /// against the sitting agent, and agent-election candidacy picks. Pure instruction
    /// encoding lives in <see cref="XcavatePropertyProgram"/>; this layer resolves what
    /// the encoding needs from live state - today just the rent collector, pinned as the
    /// rent-fronting payer exactly the way
    /// <see cref="XcavateMarketplaceCallsModel.VoteOnTermsAsync"/> does, so the
    /// transaction model's two-signer flow covers these votes unchanged.
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
        /// seat's live election round. First votes only — switching an existing vote to a
        /// different candidate additionally needs the previous candidacy account, which
        /// the property page does not offer (revotes on the SAME candidacy work).
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

            return
            [
                XcavatePropertyProgram.VoteOnAgent(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    round,
                    new PublicKey(agent),
                    previousCandidate: null,
                    amount),
            ];
        }
    }
}
