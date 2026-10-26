using System.Numerics;
using UniqueryPlus;
using UniqueryPlus.Collections;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// A property listing read from the Xcavate Solana marketplace program through the
    /// Xcavate devnet indexer, shaped like the Substrate-era NFT records so every existing
    /// view keeps rendering it.
    /// <para>
    /// The indexer only knows on-chain state, so <see cref="XcavateMetadata"/> is
    /// synthesized from that state (postcode, share counts, prices) rather than carrying
    /// the old off-chain property details - see
    /// <see cref="XcavateMarketplaceIndexerModel"/>.
    /// </para>
    /// </summary>
    public record XcavateSolanaListingNft : INftXcavateBase, INftBase, INftXcavateMetadata, INftXcavateNftMarketplace, INftXcavateOngoingObjectListing, INftXcavateRealWorldAssetDetails
    {
        /// <summary>
        /// Still XcavatePaseo: the endpoint lookup, favourites keys and the (yet to be
        /// migrated) Substrate buy flow all switch on this. The concrete record type -
        /// not <see cref="Type"/> - is what marks an item as Solana-sourced.
        /// </summary>
        public NftTypeEnum Type => NftTypeEnum.XcavatePaseo;

        /// <summary>Always 0 - Solana listings have no NFT collection.</summary>
        public BigInteger CollectionId { get; set; }

        /// <summary>The on-chain listing id, which is unique per listing.</summary>
        public BigInteger Id { get; set; }

        public required string Owner { get; set; }

        /// <summary>The on-chain listing id, untruncated.</summary>
        public required long ListingId { get; set; }

        /// <summary>The tokenised property's asset id (equal to the listing id today).</summary>
        public required long AssetId { get; set; }

        /// <summary>Unix seconds - Solana deadlines are wall-clock, not block numbers.</summary>
        public required long ListingExpiryTimestamp { get; set; }

        /// <summary>Unix seconds; 0 while claiming has not started.</summary>
        public required long ClaimDeadlineTimestamp { get; set; }

        public long LegalDeadlineTimestamp { get; set; }
        public ulong CollectedFeeQuote { get; set; }
        public ulong SpvElectionRound { get; set; }
        public long SpvElectionExpiryTimestamp { get; set; }
        public long SpvElectionCandidateCount { get; set; }
        public ushort? PropertyRegionId { get; set; }

        /// <summary>The marketplace program's ListingStatus, e.g. "Listed" or "SoldOut".</summary>
        public required string ListingStatus { get; set; }

        /// <summary>
        /// False for the statuses in which the listing is not something an investor can
        /// engage with at all - PENDING_ASSETS (shares do not exist yet), CANCELLED and
        /// REFUNDING. Sold-out and claim-phase listings stay true so their status chips
        /// still render.
        /// </summary>
        public required bool OpenForSale { get; set; }

        /// <summary>
        /// True for CANCELLED and REFUNDING: the listing died and whatever an investor
        /// paid in comes back through a refund rather than a settlement. Distinct from
        /// !<see cref="OpenForSale"/>, which also covers the pre-sale PENDING_ASSETS
        /// state where there is nothing to refund.
        /// </summary>
        public required bool IsTornDown { get; set; }

        /// <summary>
        /// True only for CANCELLED - the single status the program's withdraw_cancelled
        /// accepts. REFUNDING is mid-exit of the legal-deadline refund and still belongs
        /// to withdraw_legal_process_expired; sending withdraw_cancelled for it is
        /// rejected with ListingNotActive.
        /// </summary>
        public bool IsCancelled => ListingStatus == "Cancelled";

        /// <summary>
        /// The lawyer engaged on the developer side of the legal phase, as its base58
        /// wallet; the all-zero pubkey (see
        /// <see cref="XcavateMarketplaceIndexerModel.IsVacantSeat"/>) while the seat is
        /// vacant. Read from the indexer's listing row, never from an RPC call.
        /// </summary>
        public string DeveloperLawyer { get; set; } = string.Empty;

        /// <summary>The developer lawyer's document verdict; meaningless while the seat is vacant.</summary>
        public DocumentStatus DeveloperLawyerDocStatus { get; set; }

        /// <summary>
        /// The lawyer engaged on the SPV side of the legal phase, as its base58 wallet;
        /// the all-zero pubkey while the seat is vacant (elected or claimed via the
        /// SPV-lawyer election).
        /// </summary>
        public string SpvLawyer { get; set; } = string.Empty;

        /// <summary>The SPV lawyer's document verdict; meaningless while the seat is vacant.</summary>
        public DocumentStatus SpvLawyerDocStatus { get; set; }

        /// <summary>
        /// True only for FINALIZED - the single primary-listing status the marketplace
        /// program's relist_shares accepts; every other status is refused on-chain with
        /// PropertyNotFinalized (6073). The detail page gates its relist action on this
        /// flag rather than submitting a transaction the program must reject.
        /// </summary>
        public bool IsFinalized => ListingStatus is "Finalized";

        /// <summary>
        /// True once the listing enters the legal phase - the window in which the two
        /// lawyers review and rule on the sale documents - and through the settled and
        /// wound-down states that keep their verdicts readable. Before this (PENDING_ASSETS,
        /// LISTED, EXPIRED) there are no lawyers to show.
        /// </summary>
        public bool InLegalPhase => ListingStatus is "SoldOut" or "Legal" or "Finalized" or "Cancelled" or "Refunding";

        public MetadataBase? Metadata { get; set; }
        public PropertyMetadata? XcavateMetadata { get; set; }
        public NftMarketplaceDetails? NftMarketplaceDetails { get; set; }
        public XcavateOngoingObjectListingDetails? OngoingObjectListingDetails { get; set; }
        public XcavateRealWorldAssetDetails? RealWorldAssetDetails { get; set; }

        /// <summary>
        /// True once the claim window create_spv opened has closed - the phase in which
        /// the marketplace program takes buy_property_shares (the direct purchase) and
        /// rejects reserve_shares. False before the SPV exists (deadline 0) and while
        /// claims are still running; the program checks <c>now &gt;= claim_deadline</c>,
        /// so the deadline second itself already counts.
        /// </summary>
        public bool DirectBuyIsOpen(long nowUnixSeconds) =>
            ClaimDeadlineTimestamp != 0 && nowUnixSeconds >= ClaimDeadlineTimestamp;

        public Task<ICollectionBase> GetCollectionAsync(CancellationToken token) => throw new NotSupportedException();

        public Task<INftBase> GetFullAsync(CancellationToken token) => Task.FromResult<INftBase>(this);
    }
}
