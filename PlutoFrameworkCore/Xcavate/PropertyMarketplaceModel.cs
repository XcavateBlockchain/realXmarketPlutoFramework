using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Model.Xcavate
{
    // The hand-encoded XcavatePaseo Marketplace pallet calls that used to live here
    // (MarketplaceCalls and the BuyPropertyTokens/RelistPropertyTokens factories) are
    // replaced by the Solana program calls in XcavateMarketplaceProgram and
    // XcavateMarketplaceCallsModel.

    public record PropertyOwnership
    {
        public required uint TokensBought { get; set; }
        public required uint TokensOwned { get; set; }
        public required INftXcavateBase NftBase { get; set; }

        public NftKey Key => (NftBase.Type, NftBase.CollectionId, NftBase.Id);
    }

    public enum XcavatePropertyOperation
    {
        // Has to be there due to binding
        None,

        Buy
    }

    public record PropertyTokenOwnershipChangeInfo : XcavateNftWrapper
    {
        public required uint Amount { get; set; }
        public required XcavatePropertyOperation Operation { get; set; }
    }
}
