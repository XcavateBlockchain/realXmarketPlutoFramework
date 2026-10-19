using PlutoFrameworkCore.Solana;
using StrawberryShake;
using Substrate.NetApi.Model.Types.Primitive;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Reads marketplace property listings from the Xcavate Solana programs, through the
    /// Xcavate indexer - the replacement for the SubQuery-backed
    /// <c>UniqueryPlus.Nfts.XcavateIndexerModel</c> marketplace feed.
    /// <para>
    /// Each property asset nests the webapp's property document (name, address, images,
    /// finances) as <c>metadata</c>: the indexer's background enricher fetches and
    /// decomposes the <c>metadataUri</c> document server-side (ADR-27), so the app reads
    /// it from the same query instead of downloading and parsing the JSON itself. When
    /// the enricher has no snapshot for an asset yet (<c>metadata</c> is null), the mapped
    /// record degrades to a minimal <see cref="PropertyMetadata"/> synthesized from chain
    /// data so the existing views still render. The feed's state checkboxes filter
    /// server-side (<c>feedStates</c>, ADR-39 - see <see cref="ToFeedStates"/>); only
    /// the old town/type/name text filters have no server-side counterpart and are
    /// answered client-side by <see cref="MatchesFilter"/>.
    /// </para>
    /// <para>
    /// The cluster is the caller's - the app's selected Solana network. Callers gate on
    /// <see cref="XcavateDeploymentModel.IsDeployed"/> first: on a cluster with no deployment
    /// these readers throw <see cref="NotSupportedException"/> from the indexer client, and a
    /// marketplace that is not there must look like an empty placeholder, not an error.
    /// </para>
    /// </summary>
    public static class XcavateMarketplaceIndexerModel
    {
        /// <summary>
        /// Decimals of the listing's share price: sharePrice is denominated at 9 decimal
        /// places, whatever payment mint a purchase settles in - the program converts
        /// prices between mints by decimal count alone (see
        /// <see cref="XcavateMarketplaceCallsModel.ScaleToMintDecimals"/>).
        /// </summary>
        public const int SharePriceDecimals = 9;

        /// <summary>
        /// The on-chain default pubkey (base58 of all-zero bytes) the property letting
        /// seat and the listing's SPV-lawyer slot carry while vacant; the indexer keeps
        /// it verbatim.
        /// </summary>
        public const string VacantSeatPubkey = "11111111111111111111111111111111";

        /// <summary>
        /// Share holdings are read per property; one page of this size per request. Holder
        /// counts are unbounded in principle, so the reader pages until short.
        /// </summary>
        private const int HoldingsPageSize = 100;

        /// <summary>
        /// A listing's full governance surface for the property detail page: the
        /// pre-settlement SPV-lawyer election (the "terms" vote; marketplace id space,
        /// keyed by the marketplace listing id) and the letting seat's post-settlement
        /// votes (property id space, keyed by the asset id).
        /// </summary>
        public sealed record XcavateGovernanceState
        {
            /// <summary>
            /// The SPV-lawyer election the listing is running (the candidacy investors
            /// vote on - the "terms" flow). Rounds repeat until a lawyer wins, so the
            /// listing's own counters are the live round. Null while no election has
            /// opened (round 0, e.g. the listing is still selling).
            /// </summary>
            public sealed record SpvElectionState(
                ulong Round,
                long Expiry,
                long CandidateCount,
                long SoldShareAmount,
                long TotalVotePower,
                SpvCandidacy? LeadingCandidacy);

            /// <summary>One lawyer's candidacy with the votes backing it.</summary>
            public sealed record SpvCandidacy(string Lawyer, long VotePower, long Costs);

            /// <summary>The seat's live spending proposal with its running tallies.</summary>
            public sealed record ProposalState(
                long ProposalId,
                long Expiry,
                long TallyYes,
                long TallyNo,
                long TallyAbstain,
                int QuorumBps,
                int ThresholdBps);

            /// <summary>The live challenge against the sitting agent with its running tallies.</summary>
            public sealed record ChallengeState(
                long ChallengeId,
                long Expiry,
                long TallyYes,
                long TallyNo,
                long TallyAbstain,
                int QuorumBps);

            /// <summary>
            /// The connected wallet's share ledger and its free voting power: on-chain the
            /// effective lock is the LARGEST single reason's counter (locks overlap across
            /// reasons), and listed shares count in amount but not in votes.
            /// </summary>
            public sealed record HoldingState(uint Amount, uint Listed, uint MaxLock)
            {
                public uint Votable => Amount - Math.Min(Listed + MaxLock, Amount);
            }

            public SpvElectionState? SpvElection { get; init; }

            /// <summary>The letting seat's election counters; zeroes while no election runs.</summary>
            public long ElectionRound { get; init; }
            public long ElectionExpiry { get; init; }
            public long ElectionCandidateCount { get; init; }
            public int ElectionQuorumBps { get; init; }

            public ProposalState? ActiveProposal { get; init; }
            public ChallengeState? ActiveChallenge { get; init; }

            /// <summary>The connected wallet's ledger; null for wallets holding nothing.</summary>
            public HoldingState? Holding { get; init; }
        }

        /// <summary>
        /// One listing's governance state for the property detail page's voting sections.
        /// The terms vote lives in the marketplace id space (<paramref name="listingId"/>);
        /// the letting seat, proposals and challenges in the property program's
        /// (<paramref name="assetId"/>). The caller's own ledger joins in when
        /// <paramref name="voter"/> is known. A missing piece (no vote opened yet, no
        /// active proposal, no holding) reads as null rather than an error.
        /// </summary>
        public static async Task<XcavateGovernanceState> GetGovernanceStateAsync(
            SolanaCluster cluster,
            long listingId,
            long assetId,
            string? voter,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var listingIdArg = listingId.ToString(CultureInfo.InvariantCulture);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var listingResult = await client.MarketplaceListing
                .ExecuteAsync(listingIdArg, token)
                .ConfigureAwait(false);
            listingResult.EnsureNoErrors();
            var listingNode = listingResult.Data?.Listings.Nodes.FirstOrDefault();

            var candidaciesResult = await client.MarketplaceLawyerCandidacies
                .ExecuteAsync(listingIdArg, first: 5, offset: 0, token)
                .ConfigureAwait(false);
            candidaciesResult.EnsureNoErrors();
            var candidacies = candidaciesResult.Data?.LawyerCandidacies.Nodes;

            var lettingResult = await client.PropertyLettingGovernance
                .ExecuteAsync(assetIdArg, token)
                .ConfigureAwait(false);
            lettingResult.EnsureNoErrors();
            var lettingNode = lettingResult.Data?.PropertyLettings.Nodes.FirstOrDefault();

            var proposalsResult = await client.PropertyProposals
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            proposalsResult.EnsureNoErrors();
            var proposalNode = proposalsResult.Data?.Proposals.Nodes.FirstOrDefault();

            var challengesResult = await client.PropertyChallenges
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            challengesResult.EnsureNoErrors();
            var challengeNode = challengesResult.Data?.Challenges.Nodes.FirstOrDefault();

            XcavateGovernanceState.HoldingState? holding = null;
            if (voter is not null)
            {
                var holdingResult = await client.MarketplaceShareHoldingOf
                    .ExecuteAsync(assetIdArg, voter, token)
                    .ConfigureAwait(false);
                holdingResult.EnsureNoErrors();
                var holdingNode = holdingResult.Data?.ShareHoldings.Nodes.FirstOrDefault();
                if (holdingNode is not null)
                {
                    var maxLock = Math.Max(
                        Math.Max(ToUInt32(holdingNode.LockLawyerElection), ToUInt32(holdingNode.LockAgentElection)),
                        Math.Max(ToUInt32(holdingNode.LockProposal), ToUInt32(holdingNode.LockChallenge)));
                    holding = new XcavateGovernanceState.HoldingState(
                        ToUInt32(holdingNode.Amount),
                        ToUInt32(holdingNode.Listed),
                        maxLock);
                }
            }

            return new XcavateGovernanceState
            {
                SpvElection = BuildSpvElection(listingNode, candidacies),
                ElectionRound = ParseInt64(lettingNode?.ElectionRound),
                ElectionExpiry = ParseInt64(lettingNode?.ElectionExpiry),
                ElectionCandidateCount = ParseInt64(lettingNode?.ElectionCandidateCount),
                ElectionQuorumBps = lettingNode?.ElectionQuorumBps ?? 0,
                ActiveProposal = proposalNode is null
                    ? null
                    : new XcavateGovernanceState.ProposalState(
                        ParseInt64(proposalNode.ProposalId),
                        ParseInt64(proposalNode.Expiry),
                        ParseInt64(proposalNode.TallyYes),
                        ParseInt64(proposalNode.TallyNo),
                        ParseInt64(proposalNode.TallyAbstain),
                        proposalNode.QuorumBps,
                        proposalNode.ThresholdBps),
                ActiveChallenge = challengeNode is null
                    ? null
                    : new XcavateGovernanceState.ChallengeState(
                        ParseInt64(challengeNode.ChallengeId),
                        ParseInt64(challengeNode.Expiry),
                        ParseInt64(challengeNode.TallyYes),
                        ParseInt64(challengeNode.TallyNo),
                        ParseInt64(challengeNode.TallyAbstain),
                        challengeNode.QuorumBps),
                Holding = holding,
            };
        }

        /// <summary>
        /// The listing's live SPV-lawyer election assembled from its own counters (the
        /// fragment's spvElection* fields) plus the round's active candidacies: the summed
        /// vote power and the strongest candidacy - the one the page's Approve action
        /// backs. Null while no election has opened (round 0).
        /// </summary>
        private static XcavateGovernanceState.SpvElectionState? BuildSpvElection(
            IMarketplaceListing_Listings_Nodes? listing,
            IReadOnlyList<IMarketplaceLawyerCandidacies_LawyerCandidacies_Nodes>? candidacies)
        {
            if (listing is null)
            {
                return null;
            }

            var round = (ulong)Math.Max(0, ParseInt64(listing.SpvElectionRound));
            if (round == 0)
            {
                return null;
            }

            long totalPower = 0;
            XcavateGovernanceState.SpvCandidacy? leading = null;
            if (candidacies is not null)
            {
                foreach (var node in candidacies)
                {
                    if (ParseInt64(node.Round) != (long)round)
                    {
                        continue;
                    }

                    var power = ParseInt64(node.VotePower);
                    totalPower += power;
                    if (leading is null || power > leading.VotePower)
                    {
                        leading = new XcavateGovernanceState.SpvCandidacy(
                            node.Lawyer, power, ParseInt64(node.Costs));
                    }
                }
            }

            return new XcavateGovernanceState.SpvElectionState(
                round,
                ParseInt64(listing.SpvElectionExpiry),
                ParseInt64(listing.SpvElectionCandidateCount),
                ParseInt64(listing.SoldShareAmount),
                totalPower,
                leading);
        }

        /// <summary>
        /// One property's rental-income ledger as the holder sees it: every per-mint
        /// stream alongside their own settlement checkpoint, so the claimable amount of
        /// one mint is a pure read:
        /// <c>claimable = (stream.per_share - entry.per_share) * shares + entry.pending</c>.
        /// </summary>
        public sealed record PropertyIncomeState
        {
            /// <summary>
            /// One payment mint's income stream. <see cref="PerShare"/> is the on-chain
            /// u128 accumulator of mint base units paid per share; <see cref="Dust"/> is
            /// the base-unit remainder below one full unit per share, carried into the
            /// next distribution.
            /// </summary>
            public sealed record IncomeStream(string Mint, BigInteger PerShare, long Dust);

            /// <summary>
            /// The divisor of the settlement formula. The program accumulates raw base
            /// units with no fixed-point scaling - <c>distribute_income</c> adds
            /// <c>gain = (amount + stream.dust) / supply</c> to <c>per_share</c> and
            /// carries the remainder in <c>dust</c>
            /// (realxmarket-solana <c>property/src/instructions/income.rs</c>
            /// <c>distribute_income_handler</c>), and <c>bank()</c> then accrues
            /// <c>(per_share delta) * shares</c> with no division - so this is 1, kept as
            /// a named constant because the claimable formula is written in terms of it.
            /// </summary>
            public static readonly BigInteger PerShareScale = BigInteger.One;

            /// <summary>The property's streams, in ledger order (checkpoint entries pair by index).</summary>
            public required IReadOnlyList<IncomeStream> Streams { get; init; }

            /// <summary>
            /// The holder's checkpoint; <see cref="IncomeCheckpointState.Empty"/> when no
            /// checkpoint row exists, which the checkpoints semantics read as "held since
            /// before every stream opened" - full accrual from each stream's start.
            /// </summary>
            public required IncomeCheckpointState Checkpoint { get; init; }

            /// <summary>
            /// What one mint owes a holder of <paramref name="shares"/> shares right now,
            /// in BASE UNITS of the mint (the unit every indexer figure on this record is
            /// in). Mirrors the on-chain settlement exactly: entries pair with streams by
            /// index, a missing entry reads as a zero cursor (<c>sync_entries</c>), and
            /// banked <c>pending</c> survives a zero holding (<c>claim_income</c> pays it
            /// even with the ledger closed). An unclaimed mint returns zero. Convert with
            /// <see cref="ToDisplayAmount"/> for the UI.
            /// </summary>
            public BigInteger GetClaimable(string mint, ulong shares)
            {
                var index = -1;
                for (var i = 0; i < Streams.Count; i++)
                {
                    if (Streams[i].Mint == mint)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    return BigInteger.Zero;
                }

                var stream = Streams[index];
                var entry = index < Checkpoint.Entries.Count ? Checkpoint.Entries[index] : null;

                var delta = stream.PerShare - (entry?.PerShare ?? BigInteger.Zero);
                if (delta.Sign < 0)
                {
                    // On-chain unreachable (per_share is monotone); clamp rather than
                    // report a debt for a row that outran the ledger.
                    delta = BigInteger.Zero;
                }

                return delta * shares / PerShareScale + (BigInteger)(entry?.Pending ?? 0m);
            }

            /// <summary>
            /// Scales a mint base-unit amount (pennies of USDC, lamport-fractions...) down
            /// to whole-token display units. On-chain claimables fit a u64, so the decimal
            /// conversion cannot overflow for real income.
            /// </summary>
            public static decimal ToDisplayAmount(BigInteger baseUnits, int mintDecimals)
            {
                var scale = BigInteger.Pow(10, Math.Clamp(mintDecimals, 0, 28));
                return (decimal)baseUnits / (decimal)scale;
            }
        }

        /// <summary>
        /// One holder's income claim checkpoint on one property: where their per-stream
        /// settlement cursor sits. Never null; <see cref="Empty"/> (no entries) when the
        /// holder has never claimed.
        /// </summary>
        public sealed record IncomeCheckpointState(IReadOnlyList<IncomeCheckpointState.Entry> Entries)
        {
            /// <summary>
            /// The cursor of one stream: the <c>per_share</c> the holder settled up to
            /// (u128 base units per share) and the <c>pending</c> income banked for them
            /// in BASE UNITS of the stream's mint (on-chain u64 - <c>claim_income</c>
            /// transfers it with the mint's own decimals; carried as an exact decimal
            /// here because u64 fits it and the indexer ships it as a JSON number).
            /// </summary>
            public sealed record Entry(BigInteger PerShare, decimal Pending);

            public static IncomeCheckpointState Empty { get; } = new([]);
        }

        /// <summary>
        /// One election-round candidacy row as raw indexer input (agent elections and
        /// SPV-lawyer elections share this shape); filter to the live round with
        /// <see cref="AgentElectionState.ToCurrentRound"/>.
        /// </summary>
        public sealed record ElectionCandidacyRow(long Round, string Candidate, long VotePower);

        /// <summary>
        /// A property's agent-election surface: the letting seat, the live round, and the
        /// caller's own vote. Agent elections are plurality votes across candidacies - a
        /// vote's on-chain <c>choice</c> IS the backed candidacy's agent pubkey (the
        /// property program's <c>AgentVote.choice</c> is a Pubkey, not Yes/No/Abstain).
        /// </summary>
        public sealed record AgentElectionState
        {
            /// <summary>One candidacy of the current round with the share-weighted power behind it.</summary>
            public sealed record Candidacy(string Agent, long VotePower);

            /// <summary>
            /// The caller's vote in the current round: <see cref="Candidate"/> is the
            /// vote's on-chain <c>choice</c> - the backed candidacy's agent pubkey - and
            /// <see cref="Power"/> the shares it locked.
            /// </summary>
            public sealed record CastVote(string Candidate, long Power);

            /// <summary>
            /// One vote row as raw indexer input, before the current-round filter.
            /// <see cref="RentPayer"/> is the wallet the vote record refunds at
            /// unlock_agent_votes, recorded at vote time (the voter's own wallet for a
            /// dashboard-cast vote, the rent collector for an app-cast one); it is null
            /// only on rows synthesized client-side (the revote path's
            /// previous-candidacy shim), which never feed the unlock resolution.
            /// </summary>
            public sealed record VoteRow(long Round, string Candidate, long Power, string? RentPayer = null);

            /// <summary>
            /// The sitting agent, verbatim from the chain - <see cref="VacantSeatPubkey"/>
            /// while the seat is vacant, null when the property has no letting row yet.
            /// </summary>
            public string? Agent { get; init; }

            /// <summary>The live election round; 0 while no election has ever opened.</summary>
            public long Round { get; init; }

            /// <summary>The current round's close (unix seconds); 0 while no election runs.</summary>
            public long Expiry { get; init; }

            public int QuorumBps { get; init; }

            /// <summary>The on-chain candidate count of the current round.</summary>
            public long CandidateCount { get; init; }

            /// <summary>The current round's candidacies, vote power descending.</summary>
            public IReadOnlyList<Candidacy> Candidacies { get; init; } = [];

            /// <summary>The caller's vote in the current round, when they cast one.</summary>
            public CastVote? MyVote { get; init; }

            /// <summary>
            /// The caller's vote rows across EVERY round, as indexed (the unfiltered
            /// input of <see cref="Create"/>). The unlock wrapper re-reads this fresh at
            /// submit time and resolves the round's recorded <see cref="VoteRow.RentPayer"/>
            /// from it - the only rent_payer account unlock_agent_votes accepts.
            /// </summary>
            public IReadOnlyList<VoteRow> Votes { get; init; } = [];

            /// <summary>
            /// The newest round with an open (still locking) vote record of the caller's
            /// that is NOT the current round: a vote from an already-settled election
            /// whose locked shares <c>unlock_agent_votes</c> can still release. Null when
            /// no such record exists. A vote in the current round is not carried here -
            /// it becomes unlockable once that round's expiry passes, which the caller
            /// reads off <see cref="MyVote"/> and <see cref="Expiry"/> directly.
            /// </summary>
            public long? MyLockedRound { get; init; }

            /// <summary>
            /// The unique vote-power leader of the current round; null on a tie at the
            /// top (or no candidacies). Mirrors the strict-unique-max rule of
            /// <c>finalize_agent_election</c>: on a tie the seat reopens for a fresh
            /// round instead of electing anyone. Note the crank needs more - quorum and
            /// a non-zero tally are enforced on-chain, so a returned winner can still
            /// fail assignment and just reopen the round.
            /// </summary>
            public string? WinnerOrNull => PickPluralityWinner(Candidacies);

            /// <summary>The raw rows of <paramref name="round"/>, strongest first.</summary>
            public static IReadOnlyList<Candidacy> ToCurrentRound(long round, IReadOnlyList<ElectionCandidacyRow>? rows)
            {
                if (rows is null)
                {
                    return [];
                }

                var current = new List<Candidacy>();
                foreach (var row in rows)
                {
                    if (row.Round == round)
                    {
                        current.Add(new Candidacy(row.Candidate, row.VotePower));
                    }
                }

                return current.OrderByDescending(c => c.VotePower).ToList();
            }

            /// <summary>
            /// The strictly-unique maximum of <paramref name="currentRound"/>: equality
            /// against the running leader marks a tie, and only a later strictly-higher
            /// tally clears it (the same fold <c>finalize_agent_election</c> runs over
            /// the candidacy accounts).
            /// </summary>
            public static string? PickPluralityWinner(IReadOnlyList<Candidacy> currentRound)
            {
                long best = -1;
                string? winner = null;
                var tied = false;
                foreach (var candidacy in currentRound)
                {
                    if (candidacy.VotePower > best)
                    {
                        best = candidacy.VotePower;
                        winner = candidacy.Agent;
                        tied = false;
                    }
                    else if (candidacy.VotePower == best)
                    {
                        tied = true;
                    }
                }

                return winner is null || tied ? null : winner;
            }

            /// <summary>
            /// Assembles the state from raw indexer rows, keeping only the letting's
            /// current round (the indexer's candidacy/vote answers span every round).
            /// </summary>
            public static AgentElectionState Create(
                string? agent,
                long round,
                long expiry,
                int quorumBps,
                long candidateCount,
                IReadOnlyList<ElectionCandidacyRow>? candidacies,
                IReadOnlyList<VoteRow>? votes)
            {
                return new AgentElectionState
                {
                    Agent = agent,
                    Round = round,
                    Expiry = expiry,
                    QuorumBps = quorumBps,
                    CandidateCount = candidateCount,
                    Candidacies = ToCurrentRound(round, candidacies),
                    MyVote = votes?
                        .Where(v => v.Round == round)
                        .OrderByDescending(v => v.Power)
                        .Select(v => new CastVote(v.Candidate, v.Power))
                        .FirstOrDefault(),
                    Votes = votes ?? [],
                    MyLockedRound = votes?
                        .Where(v => v.Round != round)
                        .Select(v => (long?)v.Round)
                        .Max(),
                };
            }
        }

        /// <summary>
        /// Which finalization cranks one listing/property is ready for RIGHT NOW, each
        /// arrow carrying exactly the inputs the matching call wrapper needs - the UI
        /// never reasons about timelines itself, it shows a banner per non-null arrow.
        /// The rules mirror the on-chain handlers: a crank is offered only when the
        /// program would accept it (proposal/challenge/election votes closed but not yet
        /// finalized, the silent-verdict final stretch, the executable deal window).
        /// </summary>
        public sealed record PendingFinalizations
        {
            /// <summary>The letting-seat counters the crank rules read from.</summary>
            public sealed record LettingSnapshot(
                long ElectionRound,
                long ElectionExpiry,
                long ElectionCandidateCount,
                long ActiveProposal,
                long ActiveChallenge);

            /// <summary>The active proposal row (open = unfinalized; the account closes at finalization).</summary>
            public sealed record ProposalSnapshot(long ProposalId, long Expiry, string RentPayer);

            /// <summary>The active challenge row, carrying the parties its finalization pays out to.</summary>
            public sealed record ChallengeSnapshot(long ChallengeId, long Expiry, string RentPayer, string Challenger, string StruckAgent);

            /// <summary>The listing's legal-phase fields plus the asset's region.</summary>
            public sealed record ListingSnapshot(
                ListingStatus Status,
                long LegalDeadline,
                long LegalProcessTime,
                string Developer,
                string DeveloperLawyer,
                DocumentStatus DeveloperLawyerDocStatus,
                string SpvLawyer,
                DocumentStatus SpvLawyerDocStatus,
                long SpvElectionRound,
                long SpvElectionExpiry,
                long SpvElectionCandidateCount,
                int? RegionId,
                IReadOnlyList<string> CollectedMints);

            /// <summary>The marketplace singleton config (mints and treasury the cranks pay/derive with).</summary>
            public sealed record ConfigSnapshot(string XcavMint, string Treasury, IReadOnlyList<string> AcceptedPaymentMints);

            /// <summary>An ended, unfinalized spending proposal: its id (account seed) and rent payer.</summary>
            public sealed record ProposalFinalization(long ProposalId, string RentPayer);

            /// <summary>An ended, unfinalized challenge: its id plus the parties the payout accounts derive from.</summary>
            public sealed record ChallengeFinalization(long ChallengeId, string RentPayer, string Challenger, string StruckAgent);

            /// <summary>
            /// An ended, unfinalized election: the plurality winner (null on a tie - the
            /// seat reopens either way, so the crank still runs with no winner account),
            /// the settled round (the candidacy PDAs the finalize instruction walks are
            /// seeded with it), and the round's candidates.
            /// </summary>
            public sealed record ElectionFinalization(string? Winner, long Round, IReadOnlyList<string> Candidates);

            /// <summary>
            /// A deal ready to settle: the listing's parties and region plus the config's
            /// XCAV mint (deposit release) and the collected payment mints the payout
            /// loop iterates per mint.
            /// </summary>
            public sealed record ExecuteDealFinalization(
                ushort RegionId,
                string Developer,
                string DeveloperLawyer,
                string SpvLawyer,
                string XcavMint,
                IReadOnlyList<string> CollectedMints);

            public ProposalFinalization? Proposal { get; init; }
            public ChallengeFinalization? Challenge { get; init; }
            public ElectionFinalization? AgentElection { get; init; }
            public ElectionFinalization? SpvElection { get; init; }

            /// <summary>
            /// A silent-verdict crank is due: the SPV side ruled but the developer's
            /// lawyer let the review window run into its final stretch
            /// (<c>legal_deadline - legalProcessTime/5 &lt;= now &lt;= legal_deadline</c>,
            /// status SOLD_OUT - resolve_silent_verdict). Past the deadline the timeout
            /// refund path owns the vault and this crank no longer applies.
            /// </summary>
            public bool SilentVerdictOverdue { get; init; }

            public ExecuteDealFinalization? ExecuteDeal { get; init; }

            /// <summary>The marketplace config the arrows reference; null only pre-deployment.</summary>
            public ConfigSnapshot? Config { get; init; }
        }

        /// <summary>
        /// One holder's income ledger on one property: streams from the indexer, the
        /// checkpoint lookup skipped for an empty owner (nothing to claim for nobody).
        /// The caller's share count is NOT read here - it comes from the caller's
        /// holding (<see cref="XcavateGovernanceState.HoldingState"/> via
        /// <see cref="GetGovernanceStateAsync"/>) and feeds <see cref="PropertyIncomeState.GetClaimable"/>.
        /// <paramref name="rpc"/> is the caller's Solana RPC handle, pinned for the
        /// claim wrappers that compose on top of this read (mint programs/decimals);
        /// the indexer answers everything the state itself needs.
        /// </summary>
        public static async Task<PropertyIncomeState> GetIncomeStateAsync(
            SolanaCluster cluster,
            long assetId,
            string owner,
            Solnet.Rpc.IRpcClient rpc,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var incomesResult = await client.GetPropertyIncomes
                .ExecuteAsync(assetIdArg, token)
                .ConfigureAwait(false);
            incomesResult.EnsureNoErrors();

            var streams = ParseIncomeStreams(incomesResult.Data?.PropertyIncomes.Nodes.FirstOrDefault()?.Streams);

            var checkpoint = IncomeCheckpointState.Empty;
            if (!string.IsNullOrEmpty(owner))
            {
                var checkpointResult = await client.GetIncomeCheckpoints
                    .ExecuteAsync(assetIdArg, owner, token)
                    .ConfigureAwait(false);
                checkpointResult.EnsureNoErrors();

                checkpoint = ParseIncomeCheckpoint(checkpointResult.Data?.IncomeCheckpoints.Nodes.FirstOrDefault()?.Entries);
            }

            return new PropertyIncomeState { Streams = streams, Checkpoint = checkpoint };
        }

        /// <summary>
        /// One property's agent-election state in ONE round trip: the letting seat, the
        /// candidacies, and the caller's own votes. Candidacy and vote rows arrive
        /// unfiltered by round (the schema has no such filter); the letting's current
        /// round keeps the live ones. <paramref name="voter"/> empty reads the election
        /// without any own-vote section.
        /// </summary>
        public static async Task<AgentElectionState> GetAgentElectionStateAsync(
            SolanaCluster cluster,
            long assetId,
            string voter,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var result = await client.GetAgentElectionState
                .ExecuteAsync(assetIdArg, voter, token)
                .ConfigureAwait(false);
            result.EnsureNoErrors();

            var letting = result.Data?.PropertyLettings.Nodes.FirstOrDefault();

            var candidacies = result.Data?.AgentCandidacies.Nodes
                .Select(node => new ElectionCandidacyRow(
                    ParseInt64(node.Round), node.Agent, ParseInt64(node.VotePower)))
                .ToList();

            var votes = result.Data?.AgentVotes.Nodes
                .Select(node => new AgentElectionState.VoteRow(
                    ParseInt64(node.Round), node.Choice, ParseInt64(node.Power), node.RentPayer))
                .ToList();

            return AgentElectionState.Create(
                letting?.Agent,
                ParseInt64(letting?.ElectionRound),
                ParseInt64(letting?.ElectionExpiry),
                letting?.ElectionQuorumBps ?? 0,
                ParseInt64(letting?.ElectionCandidateCount),
                candidacies,
                votes);
        }

        /// <summary>
        /// One proposal/challenge vote record of a voter's, keyed by its record PDA:
        /// one account type sits behind both vote kinds and a proposal id and a
        /// challenge id can coincide (separate counters), so <see cref="VoteId"/> alone
        /// does not key a row - <see cref="RecordAddress"/> (the row's <c>id</c>, the
        /// account's PDA) is the exact join the unlock wrappers match their derived
        /// vote-record address against. <see cref="RentPayer"/> is the recorded wallet
        /// the unlock refunds.
        /// </summary>
        public sealed record GovVoteRow(long VoteId, string RecordAddress, string RentPayer);

        /// <summary>
        /// The caller's open proposal/challenge vote records on one asset, read by the
        /// unlock wrappers to resolve each record's stored rent payer (the program pins
        /// the passed rent_payer account to it). Closed records - an already-unlocked
        /// vote - drop out of the answer (<c>active: true</c>), which the wrappers read
        /// as "no row".
        /// </summary>
        public static async Task<IReadOnlyList<GovVoteRow>> GetGovVotesAsync(
            SolanaCluster cluster,
            long assetId,
            string voter,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var result = await client.PropertyGovVotes
                .ExecuteAsync(assetIdArg, voter, first: 50, offset: 0, token)
                .ConfigureAwait(false);
            result.EnsureNoErrors();

            var nodes = result.Data?.GovVotes.Nodes;
            return nodes is null
                ? []
                : [.. nodes.Select(node => new GovVoteRow(
                    ParseInt64(node.VoteId), node.Id, node.RentPayer))];
        }

        /// <summary>
        /// Which finalization cranks one listing/property is ready for, evaluated once
        /// against a single clock reading. Pure: the async reader maps indexer rows onto
        /// these snapshots so the rules themselves stay testable with frozen time.
        /// </summary>
        public static PendingFinalizations EvaluatePendingFinalizations(
            DateTimeOffset now,
            PendingFinalizations.LettingSnapshot? letting,
            PendingFinalizations.ProposalSnapshot? proposal,
            PendingFinalizations.ChallengeSnapshot? challenge,
            IReadOnlyList<ElectionCandidacyRow>? agentCandidacies,
            PendingFinalizations.ListingSnapshot? listing,
            IReadOnlyList<ElectionCandidacyRow>? spvCandidacies,
            PendingFinalizations.ConfigSnapshot? config)
        {
            var nowUnix = now.ToUnixTimeSeconds();

            PendingFinalizations.ProposalFinalization? proposalFinalization = null;
            if (letting is not null
                && letting.ActiveProposal > 0
                && proposal is not null
                && proposal.ProposalId == letting.ActiveProposal
                && nowUnix >= proposal.Expiry)
            {
                proposalFinalization = new PendingFinalizations.ProposalFinalization(
                    proposal.ProposalId, proposal.RentPayer);
            }

            PendingFinalizations.ChallengeFinalization? challengeFinalization = null;
            if (letting is not null
                && letting.ActiveChallenge > 0
                && challenge is not null
                && challenge.ChallengeId == letting.ActiveChallenge
                && nowUnix >= challenge.Expiry)
            {
                challengeFinalization = new PendingFinalizations.ChallengeFinalization(
                    challenge.ChallengeId, challenge.RentPayer, challenge.Challenger, challenge.StruckAgent);
            }

            PendingFinalizations.ElectionFinalization? agentElectionFinalization = null;
            if (letting is not null
                && letting.ElectionExpiry != 0
                && letting.ElectionCandidateCount > 0
                && nowUnix >= letting.ElectionExpiry)
            {
                var roundCandidacies = AgentElectionState.ToCurrentRound(letting.ElectionRound, agentCandidacies);
                agentElectionFinalization = new PendingFinalizations.ElectionFinalization(
                    AgentElectionState.PickPluralityWinner(roundCandidacies),
                    letting.ElectionRound,
                    roundCandidacies.Select(c => c.Agent).ToList());
            }

            PendingFinalizations.ElectionFinalization? spvElectionFinalization = null;
            var silentVerdictOverdue = false;
            PendingFinalizations.ExecuteDealFinalization? executeDeal = null;
            if (listing is not null)
            {
                if (listing.SpvElectionExpiry != 0
                    && listing.SpvElectionCandidateCount > 0
                    && nowUnix >= listing.SpvElectionExpiry
                    && IsVacantSeat(listing.SpvLawyer))
                {
                    var roundCandidacies = AgentElectionState.ToCurrentRound(listing.SpvElectionRound, spvCandidacies);
                    spvElectionFinalization = new PendingFinalizations.ElectionFinalization(
                        AgentElectionState.PickPluralityWinner(roundCandidacies),
                        listing.SpvElectionRound,
                        roundCandidacies.Select(c => c.Agent).ToList());
                }

                // resolve_silent_verdict: only while the sale is still SOLD_OUT, the SPV
                // side has ruled, the developer's side is silent, and the clock sits in
                // the review window's final stretch (before the deadline owns the exit).
                var finalStretch = listing.LegalDeadline - listing.LegalProcessTime / 5;
                silentVerdictOverdue = listing.Status is ListingStatus.SoldOut
                    && listing.DeveloperLawyerDocStatus is DocumentStatus.Pending
                    && listing.SpvLawyerDocStatus is not DocumentStatus.Pending
                    && nowUnix >= finalStretch
                    && nowUnix <= listing.LegalDeadline;

                // execute_deal: status LEGAL (both lawyers approved - the status flips
                // exactly then) and the legal window not yet lapsed.
                if (listing.Status is ListingStatus.Legal
                    && nowUnix <= listing.LegalDeadline
                    && listing.RegionId is { } regionId
                    && config is not null)
                {
                    executeDeal = new PendingFinalizations.ExecuteDealFinalization(
                        (ushort)Math.Clamp(regionId, 0, ushort.MaxValue),
                        listing.Developer,
                        listing.DeveloperLawyer,
                        listing.SpvLawyer,
                        config.XcavMint,
                        listing.CollectedMints);
                }
            }

            return new PendingFinalizations
            {
                Proposal = proposalFinalization,
                Challenge = challengeFinalization,
                AgentElection = agentElectionFinalization,
                SpvElection = spvElectionFinalization,
                SilentVerdictOverdue = silentVerdictOverdue,
                ExecuteDeal = executeDeal,
                Config = config,
            };
        }

        /// <summary>
        /// Which of the five finalization cranks one listing/property is ready for, one
        /// round of indexer queries answered against a single wall-clock reading. The
        /// clock has no seam in this codebase - <see cref="DateTimeOffset.UtcNow"/> is
        /// read once here and the rules live in the pure
        /// <see cref="EvaluatePendingFinalizations"/>, which tests freeze directly.
        /// <paramref name="viewer"/> is pinned for downstream per-viewer banners; the
        /// cranks themselves are permissionless, so no arrow depends on it.
        /// </summary>
        public static async Task<PendingFinalizations> GetPendingFinalizationsAsync(
            SolanaCluster cluster,
            long assetId,
            long listingId,
            string? viewer,
            CancellationToken token = default)
        {
            var now = DateTimeOffset.UtcNow;

            var client = XcavateWhitelistIndexer.GetClient(cluster);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);
            var listingIdArg = listingId.ToString(CultureInfo.InvariantCulture);

            var lettingResult = await client.PropertyLettingGovernance
                .ExecuteAsync(assetIdArg, token)
                .ConfigureAwait(false);
            lettingResult.EnsureNoErrors();
            var letting = lettingResult.Data?.PropertyLettings.Nodes.FirstOrDefault();

            var lettingSnapshot = letting is null
                ? null
                : new PendingFinalizations.LettingSnapshot(
                    ParseInt64(letting.ElectionRound),
                    ParseInt64(letting.ElectionExpiry),
                    ParseInt64(letting.ElectionCandidateCount),
                    ParseInt64(letting.GovernanceActiveProposal),
                    ParseInt64(letting.GovernanceActiveChallenge));

            var proposalsResult = await client.PropertyProposals
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            proposalsResult.EnsureNoErrors();
            var proposalNode = proposalsResult.Data?.Proposals.Nodes.FirstOrDefault();
            var proposalSnapshot = proposalNode is null
                ? null
                : new PendingFinalizations.ProposalSnapshot(
                    ParseInt64(proposalNode.ProposalId), ParseInt64(proposalNode.Expiry), proposalNode.RentPayer);

            var challengesResult = await client.PropertyChallenges
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            challengesResult.EnsureNoErrors();
            var challengeNode = challengesResult.Data?.Challenges.Nodes.FirstOrDefault();
            var challengeSnapshot = challengeNode is null
                ? null
                : new PendingFinalizations.ChallengeSnapshot(
                    ParseInt64(challengeNode.ChallengeId),
                    ParseInt64(challengeNode.Expiry),
                    challengeNode.RentPayer,
                    challengeNode.Challenger,
                    challengeNode.Agent);

            var candidaciesResult = await client.PropertyAgentCandidacies
                .ExecuteAsync(assetIdArg, first: 50, offset: 0, token)
                .ConfigureAwait(false);
            candidaciesResult.EnsureNoErrors();
            var agentCandidacies = candidaciesResult.Data?.AgentCandidacies.Nodes
                .Select(node => new ElectionCandidacyRow(
                    ParseInt64(node.Round), node.Agent, ParseInt64(node.VotePower)))
                .ToList();

            var listingResult = await client.MarketplaceListing
                .ExecuteAsync(listingIdArg, token)
                .ConfigureAwait(false);
            listingResult.EnsureNoErrors();
            var listing = listingResult.Data?.Listings.Nodes.FirstOrDefault();

            var listingSnapshot = listing is null
                ? null
                : new PendingFinalizations.ListingSnapshot(
                    listing.Status,
                    ParseInt64(listing.LegalDeadline),
                    ParseInt64(listing.LegalProcessTime),
                    listing.Developer,
                    listing.DeveloperLawyer,
                    listing.DeveloperLawyerDocStatus,
                    listing.SpvLawyer,
                    listing.SpvLawyerDocStatus,
                    ParseInt64(listing.SpvElectionRound),
                    ParseInt64(listing.SpvElectionExpiry),
                    ParseInt64(listing.SpvElectionCandidateCount),
                    listing.PropertyAsset?.RegionId,
                    ParseCollectedMints(listing.Collected));

            var lawyerCandidaciesResult = await client.MarketplaceLawyerCandidacies
                .ExecuteAsync(listingIdArg, first: 10, offset: 0, token)
                .ConfigureAwait(false);
            lawyerCandidaciesResult.EnsureNoErrors();
            var spvCandidacies = lawyerCandidaciesResult.Data?.LawyerCandidacies.Nodes
                .Select(node => new ElectionCandidacyRow(
                    ParseInt64(node.Round), node.Lawyer, ParseInt64(node.VotePower)))
                .ToList();

            var configResult = await client.MarketplaceConfigInfo
                .ExecuteAsync(token)
                .ConfigureAwait(false);
            configResult.EnsureNoErrors();
            var config = configResult.Data?.MarketplaceConfig;
            var configSnapshot = config is null
                ? null
                : new PendingFinalizations.ConfigSnapshot(
                    config.XcavMint, config.Treasury, ParseStringArray(config.AcceptedPaymentMints));

            return EvaluatePendingFinalizations(
                now, lettingSnapshot, proposalSnapshot, challengeSnapshot,
                agentCandidacies, listingSnapshot, spvCandidacies, configSnapshot);
        }

        /// <summary>True while the seat pubkey is empty or the all-zero default - the chain's "no one seated".</summary>
        public static bool IsVacantSeat(string? pubkey) =>
            string.IsNullOrEmpty(pubkey) || pubkey == VacantSeatPubkey;

        /// <summary>
        /// The ledger's raw <c>streams</c> JSON (<c>[{"mint": ..., "per_share": "...", "dust": N}, ...]</c>)
        /// parsed into typed stream records; u128 values stay in BigInteger all the way.
        /// Malformed input reads as no streams.
        /// </summary>
        public static IReadOnlyList<PropertyIncomeState.IncomeStream> ParseIncomeStreams(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return [];
            }

            try
            {
                var rows = JsonSerializer.Deserialize<List<ParsedStreamJson>>(rawJson);
                return rows?
                    .Where(row => !string.IsNullOrEmpty(row.Mint))
                    .Select(row => new PropertyIncomeState.IncomeStream(
                        row.Mint!, ParseBigInteger(row.PerShare), row.Dust))
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// One holder's raw checkpoint <c>entries</c> JSON (<c>[{"per_share": "...", "pending": N}, ...]</c>).
        /// A missing/malformed row means the holder has never claimed: the empty state,
        /// every cursor zero.
        /// </summary>
        public static IncomeCheckpointState ParseIncomeCheckpoint(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return IncomeCheckpointState.Empty;
            }

            try
            {
                var rows = JsonSerializer.Deserialize<List<ParsedEntryJson>>(rawJson);
                var entries = rows?
                    .Select(row => new IncomeCheckpointState.Entry(
                        ParseBigInteger(row.PerShare), row.Pending))
                    .ToList();
                return entries is null || entries.Count == 0
                    ? IncomeCheckpointState.Empty
                    : new IncomeCheckpointState(entries);
            }
            catch (JsonException)
            {
                return IncomeCheckpointState.Empty;
            }
        }

        /// <summary>
        /// The listing's <c>collected</c> JSON (<c>[{"mint": ..., "funds": N, ...}, ...]</c>),
        /// reduced to the payment mints settlement pays out. Malformed input reads as none.
        /// </summary>
        private static IReadOnlyList<string> ParseCollectedMints(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return [];
            }

            try
            {
                var rows = JsonSerializer.Deserialize<List<ParsedCollectedJson>>(rawJson);
                return rows?
                    .Select(row => row.Mint)
                    .Where(mint => !string.IsNullOrEmpty(mint))
                    .Cast<string>()
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private sealed record ParsedStreamJson(
            [property: System.Text.Json.Serialization.JsonPropertyName("mint")] string? Mint,
            [property: System.Text.Json.Serialization.JsonPropertyName("per_share")] string? PerShare,
            [property: System.Text.Json.Serialization.JsonPropertyName("dust")] long Dust);

        private sealed record ParsedEntryJson(
            [property: System.Text.Json.Serialization.JsonPropertyName("per_share")] string? PerShare,
            [property: System.Text.Json.Serialization.JsonPropertyName("pending")] decimal Pending);

        private sealed record ParsedCollectedJson(
            [property: System.Text.Json.Serialization.JsonPropertyName("mint")] string? Mint);

        /// <summary>
        /// The state-filtered marketplace feed, one page at a time. <paramref name="states"/>
        /// is the filter popup's checkbox selection, mapped to the indexer's derived feed
        /// states by <see cref="ToFeedStates"/> and answered in SQL - wall-clock overlays
        /// included (ADR-39); an empty selection answers nothing without a network call.
        /// </summary>
        public static async Task<IReadOnlyList<XcavateSolanaListingNft>> GetMarketplaceListedPropertiesAsync(
            SolanaCluster cluster,
            PropertyListingStateFilter states,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var feedStates = ToFeedStates(states);

            // Every state checkbox off: the feed shows nothing, and that needs no query.
            if (feedStates.Count == 0)
            {
                return [];
            }

            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceListings
                .ExecuteAsync(first, offset, feedStates, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listings = result.Data?.Listings.Nodes;
            if (listings is null || listings.Count == 0)
            {
                return [];
            }

            // The asset (and its nested metadata document) arrives joined on each listing,
            // so one query answers the whole page - no second per-asset lookup.
            return listings
                .Select(listing => MapListing(listing, listing.PropertyAsset))
                .ToList();
        }

        /// <summary>
        /// The indexer's derived feed states a checkbox selection maps to (ADR-39), one
        /// checkbox to one state: CanBeReserved → FOR_SALE, WaitingForClaim →
        /// WAITING_FOR_CLAIM, SoldOut → SOLD_OUT, Expired → EXPIRED, Finalized →
        /// FINALIZED, CancelledOrRefunding → CANCELLED_OR_REFUNDING. The wall-clock and
        /// availability overlays live in the indexer's SQL; PENDING_ASSETS is
        /// unreachable - no checkbox selects it.
        /// </summary>
        public static IReadOnlyList<ListingFeedState> ToFeedStates(PropertyListingStateFilter states)
        {
            var feedStates = new List<ListingFeedState>(6);

            if (states.HasFlag(PropertyListingStateFilter.CanBeReserved))
            {
                feedStates.Add(ListingFeedState.ForSale);
            }

            if (states.HasFlag(PropertyListingStateFilter.WaitingForClaim))
            {
                feedStates.Add(ListingFeedState.WaitingForClaim);
            }

            if (states.HasFlag(PropertyListingStateFilter.SoldOut))
            {
                feedStates.Add(ListingFeedState.SoldOut);
            }

            if (states.HasFlag(PropertyListingStateFilter.Expired))
            {
                feedStates.Add(ListingFeedState.Expired);
            }

            if (states.HasFlag(PropertyListingStateFilter.Finalized))
            {
                feedStates.Add(ListingFeedState.Finalized);
            }

            if (states.HasFlag(PropertyListingStateFilter.CancelledOrRefunding))
            {
                feedStates.Add(ListingFeedState.CancelledOrRefunding);
            }

            return feedStates;
        }

        /// <summary>
        /// One page of the secondary market's active share listings - the noticeboard
        /// feed's raw rows. The indexer's <c>id</c> is the ShareListing PDA; the I64
        /// scalars arrive as JSON strings and are parsed invariant-culturally, like
        /// everywhere else in this model. <paramref name="first"/> is capped at 100
        /// server-side; ordering is fixed slot DESC.
        /// </summary>
        public static async Task<(IReadOnlyList<XcavateShareListingInfo> Items, int TotalCount)> GetMarketplaceShareListingsAsync(
            SolanaCluster cluster,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceShareListings
                .ExecuteAsync(first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var connection = result.Data?.ShareListings;

            var items = connection?.Nodes
                .Select(MapShareListing)
                .ToList() ?? [];

            return (items, connection?.TotalCount ?? 0);
        }

        /// <summary>
        /// One share listing by its on-chain id (a separate id space from the primary
        /// listings), in ANY active state - a delisted or sold-out listing reads as null
        /// or inactive rather than hiding behind the feed's <c>active: true</c> filter.
        /// </summary>
        public static async Task<XcavateShareListingInfo?> GetShareListingAsync(
            SolanaCluster cluster,
            long shareListingId,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceShareListing
                .ExecuteAsync(shareListingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var node = result.Data?.ShareListings.Nodes.FirstOrDefault();

            return node is null ? null : MapShareListing(node);
        }

        /// <summary>
        /// Maps one indexer ShareListing row onto the app's record. The fragment shares
        /// its shape across the feed and single-listing operations, so one mapper serves
        /// both - the same trick <see cref="MapListing"/> plays with
        /// <c>IListingParts</c>.
        /// </summary>
        private static XcavateShareListingInfo MapShareListing(IShareListingParts node) =>
            new(
                Pda: node.Id,
                ShareListingId: ParseInt64(node.ShareListingId),
                AssetId: ParseInt64(node.AssetId),
                Seller: node.Seller,
                SharePriceQuote: ParseUInt64(node.SharePrice),
                Amount: ToUInt32(node.Amount),
                FeeBps: node.FeeBps,
                RentPayer: node.RentPayer);

        /// <summary>
        /// One listing refetched fresh for its detail page, with the share-owner
        /// dictionaries populated: the caller's open position under
        /// <c>OngoingObjectListingDetails.ShareOwners</c> (when
        /// <paramref name="investor"/> is known) and every holder of the underlying asset
        /// under <c>RealWorldAssetDetails.ShareOwners</c>. Null when the listing does not
        /// exist or its account has been closed.
        /// </summary>
        public static async Task<XcavateSolanaListingNft?> GetListingFullInfoAsync(
            SolanaCluster cluster,
            long listingId,
            string? investor,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceListing
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listing = result.Data?.Listings.Nodes.FirstOrDefault();
            if (listing is null)
            {
                return null;
            }

            // The joined asset arrives with the listing - no second lookup.
            var asset = listing.PropertyAsset;

            var nft = MapListing(listing, asset);

            if (investor is not null && nft.OngoingObjectListingDetails is not null)
            {
                var positionsResult = await client.MarketplaceInvestorPositions
                    .ExecuteAsync(listing.ListingId, investor, first: 1, offset: 0, token)
                    .ConfigureAwait(false);

                positionsResult.EnsureNoErrors();

                var position = positionsResult.Data?.InvestorPositions.Nodes.FirstOrDefault();
                if (position is not null)
                {
                    // Reserved only: the ongoing listing's ShareOwners feeds the wrapper's
                    // TokensBought, which the detail page prints as "You reserved" and the
                    // claim / cancel-reservation / refund states gate on. claim_shares
                    // moves the reserved shares into share_amount and zeroes
                    // reserved_share_amount, so adding the bought part here would show a
                    // claimed position as still reserved. The owned part arrives through
                    // the asset's share holdings below.
                    var reservedShares = ParseInt64(position.ReservedShareAmount);

                    if (reservedShares > 0)
                    {
                        nft.OngoingObjectListingDetails.ShareOwners[position.Investor] = new ShareOwner
                        {
                            Account = position.Investor,
                            ShareAmount = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                        };
                    }
                }
            }

            if (asset is not null && nft.RealWorldAssetDetails is not null)
            {
                var holdingsOffset = 0;

                while (true)
                {
                    var holdingsResult = await client.MarketplaceShareHoldings
                        .ExecuteAsync(asset.AssetId, HoldingsPageSize, holdingsOffset, token)
                        .ConfigureAwait(false);

                    holdingsResult.EnsureNoErrors();

                    var holdings = holdingsResult.Data?.ShareHoldings.Nodes ?? [];

                    foreach (var holding in holdings)
                    {
                        nft.RealWorldAssetDetails.ShareOwners[holding.Owner] = new ShareOwner
                        {
                            Account = holding.Owner,
                            ShareAmount = ToUInt32(holding.Amount),
                        };
                    }

                    if (holdings.Count < HoldingsPageSize)
                    {
                        break;
                    }

                    holdingsOffset += holdings.Count;
                }
            }

            return nft;
        }

        /// <summary>
        /// The properties one investor has bought or reserved shares in, one page at a
        /// time. A single <c>investorProperties</c> query answers the whole page
        /// (ADR-34): the indexer joins the investor's open positions to their listings
        /// and the listings' <c>propertyAsset</c> metadata server-side, so no per-listing
        /// lookups.
        /// <para>
        /// The filters run in SQL, not in the app: <paramref name="owned"/> keeps only
        /// positions that hold bought shares (the page's "purchased" toggle),
        /// <paramref name="reserved"/> only positions with reserved shares (the
        /// "reserved" toggle), <paramref name="name"/> case-insensitive-substring-matches
        /// the property name or postcode (the search bar), and
        /// <paramref name="townCity"/> / <paramref name="propertyType"/> match the
        /// filter popup's dropdowns. A null argument switches a filter off; the caller
        /// converts its empty/All values to null.
        /// </para>
        /// <para>
        /// A position with no bought and no reserved shares carries nothing to show and
        /// is dropped. On the nested listing the two counts are kept apart the way the
        /// views read them: the reserved count sits under the investor's address in
        /// <c>OngoingObjectListingDetails.ShareOwners</c> (the wrapper's
        /// <c>TokensBought</c> - "You reserved", the claim and cancel-reservation gates),
        /// the bought count in <c>RealWorldAssetDetails.ShareOwners</c> (the wrapper's
        /// <c>TokensOwned</c> - "You own"), the same split the detail page's
        /// <see cref="GetListingFullInfoAsync"/> produces, so a record wrapped from
        /// <see cref="Listing"/> agrees with it.
        /// </para>
        /// </summary>
        public static async Task<IReadOnlyList<XcavateSolanaInvestorProperty>> GetInvestorPropertiesAsync(
            SolanaCluster cluster,
            string investor,
            bool? owned,
            bool? reserved,
            string? name,
            string? townCity,
            string? propertyType,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.InvestorProperties
                .ExecuteAsync(investor, owned, reserved, name, townCity, propertyType, first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var nodes = result.Data?.InvestorProperties.Nodes ?? [];

            // Zero/zero positions hold nothing to show; drop them.
            var nodesToShow = nodes
                .Where(node => ParseInt64(node.ShareAmount) + ParseInt64(node.ReservedShareAmount) > 0)
                .ToList();

            var properties = new List<XcavateSolanaInvestorProperty>(nodesToShow.Count);

            foreach (var node in nodesToShow)
            {
                // The listing (with its asset and metadata) arrives joined with the
                // position - no second lookup.
                var listing = MapListing(node.Listing, node.Listing.PropertyAsset);

                var boughtShares = ParseInt64(node.ShareAmount);
                var reservedShares = ParseInt64(node.ReservedShareAmount);

                // The two figures feed different views: the ongoing listing's
                // ShareOwners becomes the wrapper's TokensBought ("You reserved", the
                // claim and cancel-reservation gates), the asset's ShareOwners becomes
                // TokensOwned ("You own"). claim_shares moves reserved_share_amount into
                // share_amount, so summing them into one bucket would show a claimed
                // position as still reserved. Zero entries are simply absent - every
                // reader falls back to 0 on a missing key.
                if (reservedShares > 0 && listing.OngoingObjectListingDetails is not null)
                {
                    listing.OngoingObjectListingDetails.ShareOwners[investor] = new ShareOwner
                    {
                        Account = investor,
                        ShareAmount = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                    };
                }

                if (boughtShares > 0 && listing.RealWorldAssetDetails is not null)
                {
                    // This path does not join share holdings; the position's share_amount
                    // stands in for the owned count. The two diverge only after
                    // secondary-market moves, which this position-scoped list (filtered
                    // on the same figure server-side) does not track either.
                    listing.RealWorldAssetDetails.ShareOwners[investor] = new ShareOwner
                    {
                        Account = investor,
                        ShareAmount = (uint)Math.Clamp(boughtShares, 0, uint.MaxValue),
                    };
                }

                properties.Add(new XcavateSolanaInvestorProperty
                {
                    Listing = listing,
                    BoughtShares = (uint)Math.Clamp(boughtShares, 0, uint.MaxValue),
                    ReservedShares = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                });
            }

            return properties;
        }

        /// <summary>
        /// The listings a lawyer can still be engaged on, one page at a time - the lawyer
        /// page's "Newly listed" filter. Server-side that is SOLD_OUT listings whose legal
        /// window is still open with at least one vacant lawyer slot and the asking wallet
        /// in neither slot, ordered by sold-out recency (<c>assignableTo</c> on the shared
        /// <c>listings</c> query).
        /// </summary>
        public static Task<(int TotalCount, IReadOnlyList<XcavateSolanaListingNft> Items)> GetLawyerAssignablePropertiesAsync(
            SolanaCluster cluster,
            string lawyer,
            int first,
            int offset,
            CancellationToken token = default) =>
            GetLawyerPropertiesAsync(cluster, assignedLawyer: null, assignableTo: lawyer, first, offset, token);

        /// <summary>
        /// The listings where the lawyer's wallet already sits in a lawyer slot (developer
        /// or SPV side), one page at a time - the lawyer page's "Under Instruction" filter
        /// and, through <c>TotalCount</c> at <c>first: 1</c>, its "Manage properties"
        /// count.
        /// </summary>
        public static Task<(int TotalCount, IReadOnlyList<XcavateSolanaListingNft> Items)> GetLawyerAssignedPropertiesAsync(
            SolanaCluster cluster,
            string lawyer,
            int first,
            int offset,
            CancellationToken token = default) =>
            GetLawyerPropertiesAsync(cluster, assignedLawyer: lawyer, assignableTo: null, first, offset, token);

        /// <summary>
        /// The shared <c>LawyerProperties</c> execution behind both lawyer filters: exactly
        /// one of the two lawyer arguments is set, the other stays null (absent = filter
        /// off). Nodes map through the same <see cref="MapListing"/> the investor path
        /// wraps with <c>ToXcavateNftWrapperAsync</c>.
        /// </summary>
        private static async Task<(int TotalCount, IReadOnlyList<XcavateSolanaListingNft> Items)> GetLawyerPropertiesAsync(
            SolanaCluster cluster,
            string? assignedLawyer,
            string? assignableTo,
            int first,
            int offset,
            CancellationToken token)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.LawyerProperties
                .ExecuteAsync(assignedLawyer, assignableTo, first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var connection = result.Data?.Listings;

            var items = connection?.Nodes is { } nodes
                ? nodes.Select(node => MapListing(node, node.PropertyAsset)).ToList()
                : [];

            return (connection?.TotalCount ?? 0, items);
        }

        /// <summary>
        /// One lifecycle filter of the developer page's property list. Each option is the
        /// set of on-chain <see cref="ListingStatus"/> values that answer it, so the merge
        /// happens inside the indexer's <c>totalCount</c> rather than over a fetched page.
        /// </summary>
        /// <remarks>
        /// These are the on-chain lifecycle states, deliberately NOT the web page's five
        /// tabs. The web's drafts / submitted / verified / rejected belong to the
        /// off-chain listing-review workflow stored in the web app's own database: no
        /// equivalent exists on chain or in this indexer, so they are omitted rather than
        /// invented. <see cref="Pending"/> is the closest honest stand-in for the web's
        /// draft/submitted pair (a listing whose asset PDA does not exist yet is created
        /// but not yet live), and <see cref="ForSale"/> is the web's "listed" tab.
        /// </remarks>
        public enum DeveloperPropertyFilter
        {
            /// <summary>No status filter: everything the developer created.</summary>
            All,

            /// <summary>Created but not live yet (the asset PDA does not exist).</summary>
            Pending,

            /// <summary>On the market with the sale window open.</summary>
            ForSale,

            /// <summary>
            /// Fully committed and through completion - the three states the
            /// "Properties sold" figure spans.
            /// </summary>
            Sold,

            /// <summary>Ended without completing: expired, cancelled or refunding.</summary>
            Ended,
        }

        /// <summary>
        /// The <see cref="ListingStatus"/> values answering one developer-page filter, or
        /// null for <see cref="DeveloperPropertyFilter.All"/> - absent argument, filter off.
        /// </summary>
        public static IReadOnlyList<ListingStatus>? ToDeveloperStatuses(DeveloperPropertyFilter filter) => filter switch
        {
            DeveloperPropertyFilter.Pending => [ListingStatus.PendingAssets],
            DeveloperPropertyFilter.ForSale => [ListingStatus.Listed],
            DeveloperPropertyFilter.Sold => [ListingStatus.SoldOut, ListingStatus.Legal, ListingStatus.Finalized],
            DeveloperPropertyFilter.Ended => [ListingStatus.Expired, ListingStatus.Cancelled, ListingStatus.Refunding],
            _ => null,
        };

        /// <summary>
        /// The listings created by one developer wallet, one page at a time - the developer
        /// page's property list. <paramref name="filter"/> narrows it to one lifecycle
        /// state; <see cref="DeveloperPropertyFilter.All"/> answers with everything.
        /// </summary>
        /// <remarks>
        /// <paramref name="developer"/> is matched against the listing's own
        /// <c>developer</c> column (the creator's base58 Solana wallet), not against a
        /// lawyer slot or a share position - unlike the lawyer and investor paths, the
        /// developer needs no join: the property is theirs because they created it.
        /// <c>TotalCount</c> reflects the filter, so the stat cells read it with
        /// <c>first: 1</c> instead of fetching a page to count.
        /// </remarks>
        public static async Task<(int TotalCount, IReadOnlyList<XcavateSolanaListingNft> Items)> GetDeveloperPropertiesAsync(
            SolanaCluster cluster,
            string developer,
            DeveloperPropertyFilter filter,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.DeveloperProperties
                .ExecuteAsync(developer, ToDeveloperStatuses(filter), first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var connection = result.Data?.Listings;

            var items = connection?.Nodes is { } nodes
                ? nodes.Select(node => MapListing(node, node.PropertyAsset)).ToList()
                : [];

            return (connection?.TotalCount ?? 0, items);
        }

        /// <summary>
        /// The "Properties sold" figure: the developer's listings across the three
        /// completing states, counted by <c>totalCount</c> at <c>first: 1</c> so no page is
        /// fetched just to count. It is its own call because the figure is shown whichever
        /// tab is active, while the list below it answers to the active tab's filter.
        /// </summary>
        public static async Task<int> GetDeveloperSoldCountAsync(
            SolanaCluster cluster,
            string developer,
            CancellationToken token = default)
        {
            var (totalCount, _) = await GetDeveloperPropertiesAsync(
                    cluster, developer, DeveloperPropertyFilter.Sold, first: 1, offset: 0, token)
                .ConfigureAwait(false);

            return totalCount;
        }

        /// <summary>
        /// The client-side stand-in for the old SubQuery <c>includesInsensitive</c> filters:
        /// an empty filter matches everything, a non-empty one is a case-insensitive
        /// substring match. The search text additionally matches the postcode, which is the
        /// only address component the chain knows.
        /// </summary>
        public static bool MatchesFilter(
            XcavateSolanaListingNft nft,
            string includesTownCity,
            string includesPropertyType,
            string includesPropertyName)
        {
            return MatchesAny(includesTownCity, nft.XcavateMetadata?.Address.TownCity)
                && MatchesAny(includesPropertyType, nft.XcavateMetadata?.PropertyType)
                && MatchesAny(includesPropertyName, nft.XcavateMetadata?.PropertyName, nft.XcavateMetadata?.Address.PostCode);
        }

        private static bool MatchesAny(string filter, params string?[] values)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return values.Any(value => value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static XcavateSolanaListingNft MapListing(
            IListingParts listing,
            IMarketplaceListings_Listings_Nodes_PropertyAsset? asset)
        {
            // The indexer's background enricher has already fetched and decomposed the
            // document `metadataUri` points at (ADR-27), nested on the asset itself; null
            // while the enricher has no snapshot for this PDA (fetch pending or failing).
            var offchainMetadata = asset?.Metadata;
            var listingId = ParseInt64(listing.ListingId);
            var assetId = ParseInt64(listing.AssetId);
            var listed = ParseInt64(listing.ListedShareAmount);
            var sold = ParseInt64(listing.SoldShareAmount);
            var reserved = ParseInt64(listing.ReservedShareAmount);

            // Shares are only actually for sale while the listing is LISTED; EXPIRED keeps
            // its real remainder so the "Listing expired" chip works (the wall clock already
            // blocks buying there). Everything else - PENDING_ASSETS before the asset
            // exists, CANCELLED, REFUNDING, and the sold-out/claim statuses - must not
            // present a buyable remainder, whatever the raw counters say.
            var purchasable = listing.Status is ListingStatus.Listed or ListingStatus.Expired;
            var availableShares = purchasable ? Math.Max(0, listed - sold - reserved) : 0;

            // The asset account is the authority on the property's total share supply; a
            // PENDING_ASSETS listing has no asset yet, and there the listed amount is the
            // whole offer.
            var totalShares = asset is null ? listed : ParseInt64(asset.ShareAmount);

            var pricePerShare = SolanaAmount.FromBaseUnits(listing.SharePrice, SharePriceDecimals);

            // Best name available: the webapp document's, then the on-chain asset name
            // (empty until init_property_assets attaches it), then chain-synthesized.
            var propertyName = FirstNonEmpty(
                offchainMetadata?.PropertyName,
                asset?.Name,
                string.IsNullOrWhiteSpace(asset?.Location) ? null : $"Property {asset!.Location}",
                $"Listing #{listingId}")!;

            // The indexer carries the document's image list as a raw JSON string (juniper
            // has no JSON scalar), so it is decoded here once and shared with the view.
            var images = ParseStringArray(offchainMetadata?.PropertyImages);

            // The mirror's compressed 720x720 copies of `images` (same order; null until
            // the mirror's first upload for the asset) arrive as a real list, unlike the
            // raw-JSON `propertyImages`. Every surface except the full-screen image page
            // shows these; an absent list falls back to the full-resolution originals.
            var thumbnails = offchainMetadata?.PropertyImageThumbnails?.Select(NormalizeMirrorUrl).ToList();

            var metadata = new MetadataBase
            {
                Name = propertyName,
                Description = offchainMetadata?.PropertyDescription ?? string.Empty,
                Image = thumbnails?.FirstOrDefault() ?? images.FirstOrDefault() ?? string.Empty,
            };

            // The indexer's decomposed document when the asset carries one, degraded to a
            // minimal record synthesized from chain state when not - the views need a
            // non-null PropertyMetadata to render at all.
            var propertyMetadata = offchainMetadata is not null
                ? MapPropertyMetadata(offchainMetadata)
                : new PropertyMetadata
                {
                    Financials = new PropertyFinancials(),
                    Files = [],
                    Address = new PropertyAddress(),
                    Attributes = new PropertyAttributes(),
                };

            // Chain-authoritative fields win over whatever the document said: the buy flow
            // prices and counts shares with these, and the sale's status and developer are
            // on-chain facts.
            propertyMetadata.Status = listing.Status.ToString();
            propertyMetadata.PropertyName = propertyName;
            propertyMetadata.DeveloperAddress = listing.Developer;
            propertyMetadata.AccountAddress = listing.Developer;
            propertyMetadata.PropertyId ??= listing.Id;
            propertyMetadata.Address.PostCode ??= asset?.Location;
            propertyMetadata.Financials.PricePerToken = pricePerShare;
            propertyMetadata.Financials.NumberOfTokens = (int)Math.Clamp(totalShares, 0, int.MaxValue);
            propertyMetadata.Financials.PropertyPrice = pricePerShare * totalShares;

            return new XcavateSolanaListingNft
            {
                CollectionId = BigInteger.Zero,
                Id = listingId,
                Owner = listing.Developer,
                ListingId = listingId,
                AssetId = assetId,
                ListingExpiryTimestamp = ParseInt64(listing.ListingExpiry),
                ClaimDeadlineTimestamp = ParseInt64(listing.ClaimDeadline),
                ListingStatus = listing.Status.ToString(),
                OpenForSale = listing.Status is not (ListingStatus.PendingAssets or ListingStatus.Cancelled or ListingStatus.Refunding),
                IsTornDown = listing.Status is ListingStatus.Cancelled or ListingStatus.Refunding,
                // The legal-phase lawyer seats ride the same listing row (the indexer
                // flattens both LawyerAssignments onto it); the detail page's lawyers
                // section reads them from here instead of querying the chain.
                DeveloperLawyer = listing.DeveloperLawyer,
                DeveloperLawyerDocStatus = listing.DeveloperLawyerDocStatus,
                SpvLawyer = listing.SpvLawyer,
                SpvLawyerDocStatus = listing.SpvLawyerDocStatus,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                OngoingObjectListingDetails = new XcavateOngoingObjectListingDetails
                {
                    RealEstateDeveloper = listing.Developer,
                    TaxPaidByDeveloper = listing.TaxPaidByDeveloper,
                    // Unix seconds, not block numbers: Solana deadlines are wall-clock.
                    // XcavateSolanaListingNft carries the untruncated values; these exist
                    // for the Substrate-era record shape.
                    ListingExpiry = ToUInt32(listing.ListingExpiry),
                    ClaimExpiry = ParseInt64(listing.ClaimDeadline) > 0 ? ToUInt32(listing.ClaimDeadline) : null,
                    ListedTokens = (uint)Math.Clamp(availableShares, 0, uint.MaxValue),
                    // Reserved-but-not-claimed shares are the Solana counterpart of the
                    // pallet's unclaimed tokens: the claim/refund states downstream key
                    // off this being non-zero.
                    UnclaimedTokens = (uint)Math.Clamp(reserved, 0, uint.MaxValue),
                    AssetId = new U32(ToUInt32(listing.AssetId)),
                    CollectionId = new U32(0),
                    ItemId = new U32(ToUInt32(listing.ListingId)),
                    ShareOwners = new(),
                },
                NftMarketplaceDetails = asset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = asset.SpvCreated,
                        AssetId = ToUInt32(asset.AssetId),
                        Region = (uint)Math.Clamp(asset.RegionId, 0, int.MaxValue),
                        Location = asset.Location,
                        Tokens = ToUInt32(asset.ShareAmount),
                    },
                RealWorldAssetDetails = asset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(asset.ShareAmount),
                        Price = ParseBigInteger(listing.SharePrice),
                        SpvCreated = asset.SpvCreated,
                        Finalized = asset.Finalized,
                        ShareOwners = new(),
                    },
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static long ParseInt64(string? value)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static ulong ParseUInt64(string? value)
        {
            return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static uint ToUInt32(string? value)
        {
            var parsed = ParseInt64(value);

            return (uint)Math.Clamp(parsed, 0, uint.MaxValue);
        }

        private static BigInteger ParseBigInteger(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BigInteger.Zero;
            }

            return BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : BigInteger.Zero;
        }

        private static decimal ParseDecimal(string? value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
        }

        private static int? ParseInt32(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }

        /// <summary>
        /// The mirror currently prefixes its public thumbnail URLs with a doubled scheme
        /// (<c>https://://host/...</c>), which no image loader can parse; collapsing it
        /// here keeps the views working until the mirror is fixed. Correct URLs contain
        /// no "://://" and pass through unchanged.
        /// </summary>
        private static string NormalizeMirrorUrl(string url) => url.Replace("://://", "://", StringComparison.Ordinal);

        /// <summary>
        /// The indexer's enricher stores the document's URL arrays (<c>propertyImages</c>,
        /// <c>otherDocuments</c>) as raw JSON strings - juniper has no JSON scalar - so the
        /// array is decoded here. Malformed or empty input degrades to no URLs.
        /// </summary>
        private static List<string> ParseStringArray(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(rawJson) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// The <see cref="PropertyMetadata"/> the property views bind to, built from the
        /// indexer's decomposed document. The chain-authoritative overwrite that
        /// <see cref="MapListing"/> applies afterwards is unchanged; only the source of
        /// the document data moved from an app-side HTTP fetch to the indexer.
        /// </summary>
        private static PropertyMetadata MapPropertyMetadata(IMarketplaceListings_Listings_Nodes_PropertyAsset_Metadata metadata)
        {
            var address = metadata.Address;
            var attributes = metadata.Attributes;
            var finances = metadata.Finances;

            var documentPropertyPrice = ParseDecimal(finances?.PropertyPrice);
            var documentSharePrice = ParseDecimal(finances?.SharePrice);
            var documentShares = (int)Math.Clamp(ParseInt64(finances?.NumberOfShares), 0, int.MaxValue);

            return new PropertyMetadata
            {
                Status = metadata.Status,
                PropertyName = metadata.PropertyName,
                Financials = new PropertyFinancials
                {
                    PropertyPrice = documentPropertyPrice,
                    NumberOfTokens = documentShares,
                    PricePerToken = documentSharePrice,
                    // MapListing overwrites the three live fields above with chain-derived
                    // values; yield math reads these document-scale copies so the ratio
                    // never mixes the document's scale with the chain's price decimals.
                    DocumentPropertyPrice = documentPropertyPrice,
                    DocumentPricePerToken = documentSharePrice,
                    DocumentNumberOfShares = documentShares,
                    EstimatedRentalIncome = ParseDecimal(finances?.EstimatedRentalIncome),
                    AnnualServiceCharge = ParseDecimal(finances?.AnnualServiceCharge),
                    StampDutyTax = ParseDecimal(finances?.StampDutyTax),
                    IsStampDutyPaid = finances?.IsStampDutyPaid ?? false,
                    IsAnnualServiceChargePaid = finances?.IsAnnualServiceChargePaid ?? false,
                },
                // DisplayImages is what every view treats as the image list: the mirror's
                // compressed thumbnails when the indexer supplied them, the full-resolution
                // Files otherwise. Files stays full-res for the full-screen image page.
                Files = ParseStringArray(metadata.PropertyImages),
                ThumbnailFiles = metadata.PropertyImageThumbnails?.Select(NormalizeMirrorUrl).ToList() ?? [],
                CreatedAt = metadata.CreatedAt ?? default,
                UpdatedAt = metadata.UpdatedAt ?? default,
                Address = address is null
                    ? new PropertyAddress()
                    : new PropertyAddress
                    {
                        Street = address.Street,
                        TownCity = address.TownCity,
                        FlatOrUnit = address.FlatOrUnit,
                        PostCode = address.PostCode,
                        LocalAuthority = address.LocalAuthority,
                    },
                Company = metadata.CompanyName is null && metadata.CompanyLogo is null
                    ? null
                    : new PropertyCompany
                    {
                        Name = metadata.CompanyName,
                        Logo = metadata.CompanyLogo,
                    },
                PropertyDescription = metadata.PropertyDescription,
                PropertyType = metadata.PropertyType,
                Map = metadata.MapUrl,
                PlanningCode = metadata.PlanningCode,
                PropertyId = metadata.PropertyId,
                // The developer identity the document knows; the listing's on-chain
                // developer overwrites it in MapListing.
                DeveloperAddress = metadata.CompanyWalletAddress ?? metadata.User,
                AccountAddress = metadata.User,
                Attributes = attributes is null
                    ? null
                    : new PropertyAttributes
                    {
                        Area = attributes.Area,
                        Quality = attributes.Quality,
                        OutdoorSpace = attributes.OutdoorSpace,
                        NumberOfBedrooms = ParseInt32(attributes.NumberOfBedrooms),
                        NumberOfBathrooms = ParseInt32(attributes.NumberOfBathrooms),
                        ConstructionDate = attributes.ConstructionDate,
                        OffStreetParking = attributes.OffStreetParking,
                    },
            };
        }
    }

    /// <summary>
    /// One secondary-market share listing as the indexer reports it: a holder's offer of
    /// <see cref="Amount"/> of their shares of asset <see cref="AssetId"/> at
    /// <see cref="SharePriceQuote"/> per share (the 9-decimal price scale -
    /// <see cref="XcavateMarketplaceIndexerModel.SharePriceDecimals"/>). This is the
    /// indexer-side counterpart of the on-chain <see cref="XcavateShareListing"/> record:
    /// the same account, read through the indexer for feeds and lookups instead of RPC.
    /// </summary>
    /// <param name="Pda">The ShareListing account's address (the indexer row's <c>id</c>).</param>
    /// <param name="ShareListingId">The on-chain share-listing id - a separate id space from the primary listings.</param>
    /// <param name="AssetId">The tokenised property's asset id (equal to its primary listing id).</param>
    /// <param name="Seller">The wallet offering the shares.</param>
    /// <param name="SharePriceQuote">The asking price per share, at the 9-decimal price scale.</param>
    /// <param name="Amount">Shares still for sale; partial buys draw it down.</param>
    /// <param name="FeeBps">The marketplace fee pinned at listing time, taken from the seller's proceeds.</param>
    /// <param name="RentPayer">The wallet that fronted the account's rent, refunded at delist.</param>
    public sealed record XcavateShareListingInfo(
        string Pda,
        long ShareListingId,
        long AssetId,
        string Seller,
        ulong SharePriceQuote,
        uint Amount,
        int FeeBps,
        string RentPayer);
}
