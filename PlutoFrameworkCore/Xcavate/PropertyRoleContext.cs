namespace PlutoFramework.Model.Xcavate
{
    public sealed record PropertyRoleContext
    {
        public required long ListingId { get; init; }
        public uint ReservedShares { get; init; }
        public uint OwnedShares { get; init; }
        public bool HasClaimedSpvCase { get; init; }
        public long? ActiveSpvCaseClaimRound { get; init; }
        public bool CanClaimSpvCase { get; init; }
        public string? LettingAgent { get; init; }
        public long LettingElectionRound { get; init; }
        public long LettingElectionExpiry { get; init; }
        public long LettingElectionCandidateCount { get; init; }
        public long ActiveProposalId { get; init; }
        public string? ActiveProposalProposer { get; init; }
        public long ActiveChallengeId { get; init; }
        public string? ActiveChallengeAgent { get; init; }
        public bool HasClaimedProperty { get; init; }
        public long? ActivePropertyClaimRound { get; init; }
        public bool CanClaimProperty { get; init; }
        public long? ResignationDueTimestamp { get; init; }
    }
}
