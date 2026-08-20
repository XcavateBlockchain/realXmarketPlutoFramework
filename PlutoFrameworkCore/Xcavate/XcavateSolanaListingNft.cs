using System.Numerics;
using UniqueryPlus;
using UniqueryPlus.Collections;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;

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

        /// <summary>The marketplace program's ListingStatus, e.g. "Listed" or "SoldOut".</summary>
        public required string ListingStatus { get; set; }

        /// <summary>
        /// False for the statuses in which the listing is not something an investor can
        /// engage with at all - PENDING_ASSETS (shares do not exist yet), CANCELLED and
        /// REFUNDING. The marketplace feed hides these; sold-out and claim-phase listings
        /// stay true so their status chips still render.
        /// </summary>
        public required bool OpenForSale { get; set; }

        public MetadataBase? Metadata { get; set; }
        public PropertyMetadata? XcavateMetadata { get; set; }
        public NftMarketplaceDetails? NftMarketplaceDetails { get; set; }
        public XcavateOngoingObjectListingDetails? OngoingObjectListingDetails { get; set; }
        public XcavateRealWorldAssetDetails? RealWorldAssetDetails { get; set; }

        public Task<ICollectionBase> GetCollectionAsync(CancellationToken token) => throw new NotSupportedException();

        public Task<INftBase> GetFullAsync(CancellationToken token) => Task.FromResult<INftBase>(this);
    }
}
