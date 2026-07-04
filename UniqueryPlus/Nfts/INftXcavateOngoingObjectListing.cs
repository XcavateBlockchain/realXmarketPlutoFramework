using Substrate.NetApi.Model.Types.Primitive;

namespace UniqueryPlus.Nfts
{
    public record ShareOwner
    {
        public string Account { get; set; } = string.Empty;
        public uint ShareAmount { get; set; }
        public uint UpdatedBlock { get; set; }
        public uint RelistCount { get; set; }
    }

    public record XcavateOngoingObjectListingDetails
    {
        public required string RealEstateDeveloper { get; set; }
        public required bool TaxPaidByDeveloper { get; set; }

        /// <summary>
        /// Listing expiry in Block numbers
        /// </summary>
        public required uint ListingExpiry { get; set; }
        /// <summary>
        /// Claim expiry in Block numbers
        /// </summary>
        public required uint? ClaimExpiry { get; set; }
        public required uint ListedTokens { get; set; }
        public required uint UnclaimedTokens { get; set; }

        public required U32 AssetId { get; set; }

        public required U32 CollectionId { get; set; }

        public required U32 ItemId { get; set; }

        public Dictionary<string, ShareOwner>? ShareOwners { get; set; }
    }

    public interface INftXcavateOngoingObjectListing
    {
        public XcavateOngoingObjectListingDetails? OngoingObjectListingDetails { get; set; }
    }
}
