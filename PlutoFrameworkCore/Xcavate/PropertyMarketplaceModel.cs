using PlutoFramework.Constants;
using PlutoFramework.Types;
using PlutoFrameworkCore.Xcavate;
using Substrate.NetApi;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using System.Numerics;
using UniqueryPlus;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;
using XcavatePaseo.NetApi.Generated.Model.pallet_marketplace.types;
using XcavatePaseo.NetApi.Generated.Model.sp_core.crypto;
using XcavatePaseo.NetApi.Generated.Storage;
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

    public class PropertyMarketplaceModel
    {
        public static IEnumerable<AssetKey> GetAcceptedAssets(EndpointEnum endpointKey) => endpointKey switch
        {
            EndpointEnum.XcavatePaseo => /*new MarketplaceConstants().AcceptedAssets().Value*/
                                         new List<U32>() { new U32(1337), new U32(1984) }
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

        public static async Task<RecursiveReturn<PropertyOwnership>> GetPropertiesOwnedByAsync(SubstrateClientExt client, string address, uint limit, byte[]? lastKey, CancellationToken token)
        {
            Console.WriteLine($"Finding properties owned by {address}.");

            // 0x + Twox64 pallet + Twox64 storage + Blake2_128Concat accountId32
            var keyPrefixLength = 162;

            var accountId = new AccountId32();
            accountId.Create(Utils.GetPublicKeyFrom(address));

            var keyPrefix = Utils.HexToByteArray(MarketplaceStorage.ShareOwnerParams(new BaseTuple<AccountId32, U32>(accountId, new U32(0))).Substring(0, keyPrefixLength));

            var fullKeys = await client.State.GetKeysPagedAsync(keyPrefix, limit, lastKey, string.Empty, token).ConfigureAwait(false);

            // No more nfts found
            if (fullKeys == null || !fullKeys.Any())
            {
                return new RecursiveReturn<PropertyOwnership>
                {
                    Items = [],
                    LastKey = lastKey,
                };
            }

            var idKeys = fullKeys.Select(p => p.ToString().Substring(keyPrefixLength));

            var storageChangeSets = await client.State.GetQueryStorageAtAsync(fullKeys.Select(p => Utils.HexToByteArray(p.ToString())).ToList(), string.Empty, token).ConfigureAwait(false);

            var shareOwnerDetails = new List<ShareOwnerDetails>();

            foreach (var change in storageChangeSets.First().Changes)
            {
                if (change[1] == null)
                {
                    continue;
                }

                var details = new ShareOwnerDetails();
                details.Create(change[1]);

                shareOwnerDetails.Add(details);

                // Combine the amount owned with the rest of the property details
            }

            var propertyAssetDetails = await GetPropertyAssetDetailsAsync(client, idKeys, lastKey, token);

            return new RecursiveReturn<PropertyOwnership>
            {
                Items = propertyAssetDetails.Items.Zip(shareOwnerDetails, (propertyDetails, ownerDetails) => new PropertyOwnership
                {
                    TokensBought = ownerDetails.ShareAmount,
                    TokensOwned = 0,
                    NftBase = propertyDetails,
                }),
                LastKey = Utils.HexToByteArray(fullKeys.Last().ToString())
            };
        }

        public static async Task<RecursiveReturn<INftXcavateBase>> GetPropertiesAsync(SubstrateClientExt client, uint limit, byte[]? lastKey, CancellationToken token)
        {
            // 0x + Twox64 pallet + Twox64 storage + Blake2_128Concat U32
            var keyPrefixLength = 66;

            var keyPrefix = Utils.HexToByteArray(MarketplaceStorage.OngoingObjectListingParams(new U32(0)).Substring(0, keyPrefixLength));

            var fullKeys = await client.State.GetKeysPagedAsync(keyPrefix, limit, lastKey, string.Empty, token).ConfigureAwait(false);

            Console.WriteLine("Keys found: " + fullKeys.Count());

            // No more nfts found
            if (fullKeys == null || !fullKeys.Any())
            {
                return new RecursiveReturn<INftXcavateBase>
                {
                    Items = [],
                    LastKey = lastKey,
                };
            }

            var idKeys = fullKeys.Select(p => p.ToString().Substring(keyPrefixLength));

            return await GetPropertyAssetDetailsAsync(client, idKeys, lastKey, token);
        }

        public static async Task<RecursiveReturn<INftXcavateBase>> GetPropertyAssetDetailsAsync(SubstrateClientExt client, IEnumerable<string> propertyIds, byte[]? lastKey, CancellationToken token)
        {
            const int keyPrefixLength = 66;

            var keyPrefix = MarketplaceStorage.OngoingObjectListingParams(new U32(0))
                .Substring(0, keyPrefixLength);

            var fullKeys = propertyIds.Select(id => keyPrefix + id).ToList();

            var storageChangeSets = await client.State
                .GetQueryStorageAtAsync(fullKeys.Select(k => Utils.HexToByteArray(k)).ToList(), string.Empty, token)
                .ConfigureAwait(false);

            var nftIds = new List<(U32 CollectionId, U32 ItemId)>();
            foreach (var change in storageChangeSets.First().Changes)
            {
                if (change[1] == null)
                {
                    continue;
                }

                var listing = new PropertyListingDetails();
                listing.Create(change[1]);

                nftIds.Add((listing.CollectionId, listing.ItemId));
            }

            return await XcavatePaseoNftModel
                .GetNftsNftsPalletAsync(client, nftIds, fullKeys.Last(), token)
                .ConfigureAwait(false);
        }

        public static async Task<INftBase> GetPropertyByIdAsync(SubstrateClientExt client, uint propertyId, CancellationToken token)
        {
            const int keyPrefixLength = 66;
            string[] idKeys =
            [
                MarketplaceStorage.OngoingObjectListingParams(new U32(propertyId))
                    .Substring(keyPrefixLength)
            ];

            var propertyDetails = await GetPropertyAssetDetailsAsync(client, idKeys, null, token);
            if (!propertyDetails.Items.Any())
            {
                throw new Exception("Unexpected failure");
            }

            return propertyDetails.Items.First();
        }

        public static IAsyncEnumerable<INftXcavateBase> GetPropertiesAsync(
            SubstrateClientExt client,
            uint limit = 25
        )
        {
            return RecursionHelper.ToIAsyncEnumerableAsync(
                [client],
                (SubstrateClient client, NftTypeEnum _type, byte[]? lastKey, CancellationToken token) => GetPropertiesAsync((SubstrateClientExt)client, limit, lastKey, token),
                limit
            );
        }

        public static IAsyncEnumerable<PropertyOwnership> GetPropertiesOwnedByAsync(
            SubstrateClientExt client,
            string owner,
            uint limit = 25
        )
        {
            return RecursionHelper.ToIAsyncEnumerableAsync(
                [client],
                (SubstrateClient client, NftTypeEnum _type, byte[]? lastKey, CancellationToken token) => GetPropertiesOwnedByAsync((SubstrateClientExt)client, owner, limit, lastKey, token),
                limit
            );
        }

        public static IAsyncEnumerable<INftBase> GetIndexedPropertiesForSaleAsync(
            SubstrateClientExt client,
            uint limit = 25
        )
        {
            return RecursionHelper.ToIAsyncEnumerableAsync(
                [client],
                (SubstrateClient client, NftTypeEnum _type, int limit, int offset, CancellationToken token) => XcavateSubqueryModel.GetPropertiesForSaleAsync((SubstrateClientExt)client, limit, offset, token),
                (int)limit
            );
        }
    }
}
