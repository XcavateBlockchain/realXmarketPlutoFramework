using System.Numerics;
using UniqueryPlus.Collections;
using UniqueryPlus.Metadata;

namespace UniqueryPlus.Nfts
{
    public record XcavatePaseoNftsPalletNft : INftXcavateBase, INftBase, INftXcavateMetadata, INftXcavateNftMarketplace, INftXcavateOngoingObjectListing, INftXcavateRealWorldAssetDetails
    {
        public NftTypeEnum Type => NftTypeEnum.XcavatePaseo;
        public BigInteger CollectionId { get; set; }
        public BigInteger Id { get; set; }
        public required string Owner { get; set; }
        public NftMarketplaceDetails? NftMarketplaceDetails { get; set; }

        public XcavateOngoingObjectListingDetails? OngoingObjectListingDetails { get; set; }
        public XcavateRealWorldAssetDetails? RealWorldAssetDetails { get; set; }

        public MetadataBase? Metadata { get; set; }
        public PropertyMetadata? XcavateMetadata { get; set; }

        public Task<ICollectionBase> GetCollectionAsync(CancellationToken token) => throw new NotSupportedException();

        public Task<INftBase> GetFullAsync(CancellationToken token) => Task.FromResult<INftBase>(this);
    }
}
