using PlutoFramework.Constants;
using PlutoFramework.Types;
using PlutoFrameworkCore.Xcavate;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Types.Primitive;
using System.Numerics;
using UniqueryPlus.Nfts;
using AssetKey = (PlutoFramework.Constants.EndpointEnum, PlutoFramework.Types.AssetPallet, System.Numerics.BigInteger);
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Model.Xcavate
{
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

    /// <summary>
    /// Hand-encoded calls of the XcavatePaseo Marketplace pallet (index 25),
    /// previously provided by the generated XcavatePaseo.NetApi bindings.
    /// </summary>
    public static class MarketplaceCalls
    {
        private const byte PALLET_INDEX = 25;
        private const string PALLET_NAME = "Marketplace";

        public static Method BuyPropertyShares(U32 listingId, U32 amount, U32 paymentAsset) => new Method(PALLET_INDEX, PALLET_NAME, 1, "buy_property_shares",
            new List<byte>()
                .Concat(listingId.Encode())
                .Concat(amount.Encode())
                .Concat(paymentAsset.Encode())
                .ToArray());

        public static Method ClaimPropertyShares(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 2, "claim_property_shares", listingId.Encode());

        public static Method CreateSpv(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 4, "create_spv", listingId.Encode());

        public static Method RelistShares(U32 assetId, U128 sharePrice, U32 amount) => new Method(PALLET_INDEX, PALLET_NAME, 5, "relist_shares",
            new List<byte>()
                .Concat(assetId.Encode())
                .Concat(sharePrice.Encode())
                .Concat(amount.Encode())
                .ToArray());

        public static Method CancelPropertyPurchase(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 7, "cancel_property_purchase", listingId.Encode());

        public static Method WithdrawExpired(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 13, "withdraw_expired", listingId.Encode());

        public static Method WithdrawClaimingExpired(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 15, "withdraw_claiming_expired", listingId.Encode());

        public static Method WithdrawUnclaimed(U32 listingId) => new Method(PALLET_INDEX, PALLET_NAME, 16, "withdraw_unclaimed", listingId.Encode());
    }

    public class PropertyMarketplaceModel
    {
        public static IEnumerable<AssetKey> GetAcceptedAssets(EndpointEnum endpointKey) => endpointKey switch
        {
            EndpointEnum.XcavatePaseo => new List<U32>() { new U32(1337), new U32(1984) }
                                         .Select(u32 => (EndpointEnum.XcavatePaseo, AssetPallet.Assets, new BigInteger(u32.Value))),
            _ => [],
        };

        public static Method BuyPropertyTokens(EndpointEnum endpointKey, uint listingId, uint amount, AssetKey paymentAsset) => endpointKey switch
        {
            EndpointEnum.XcavatePaseo => MarketplaceCalls.BuyPropertyShares(new U32(listingId), new U32(amount), new U32((uint)paymentAsset.Item3)),
            _ => throw new NotImplementedException($"BuyPropertyTokens not implemented for {endpointKey}"),
        };

        public static Method RelistPropertyTokens(EndpointEnum endpointKey, uint assetId, uint amount, BigInteger pricePerToken, AssetKey paymentAsset) => endpointKey switch
        {
            EndpointEnum.XcavatePaseo => MarketplaceCalls.RelistShares(new U32(assetId), new U128(pricePerToken), new U32(amount)),
            _ => throw new NotImplementedException($"RelistPropertyTokens not implemented for {endpointKey}"),
        };
    }
}
