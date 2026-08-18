using PlutoFramework.Constants;
using PlutoFramework.Model.AjunaExt;
using UniqueryPlus.Nfts;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);
namespace PlutoFramework.Model.Xcavate
{
    public static class XcavateOwnedPropertiesModel
    {
        private const int LIMIT = 100;
        public static Dictionary<NftKey, PropertyOwnership> ItemsDict = new Dictionary<NftKey, PropertyOwnership>();
        private static Dictionary<EndpointEnum, DateTime> timeUsedDict = new Dictionary<EndpointEnum, DateTime>();
        private static Dictionary<EndpointEnum, TaskCompletionSource> waitUsedDict = new Dictionary<EndpointEnum, TaskCompletionSource>();

        public static bool Loading = false;

        public static async Task LoadAsync(SubstrateClientExt client, string address, CancellationToken token, bool forceReload = false)
        {
            // If it has been used <1 minute ago, do not load again
            if (timeUsedDict.TryGetValue(client.Endpoint.Key, out var lastUsedTime) && (DateTime.UtcNow - lastUsedTime).TotalMinutes < 1)
            {
                if (waitUsedDict.TryGetValue(client.Endpoint.Key, out var wait))
                {
                    await wait.Task;
                }

                return;
            }

            timeUsedDict[client.Endpoint.Key] = DateTime.UtcNow;

            waitUsedDict[client.Endpoint.Key] = new TaskCompletionSource();

            Loading = true;

            try
            {
                var properties = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(
                    first: LIMIT,
                    tokenOwner: address
                ).ConfigureAwait(false);

                foreach (var property in properties)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    uint tokensBought = 0;
                    if (property.OngoingObjectListingDetails?.ShareOwners.TryGetValue(address, out var boughtShareOwner) ?? false)
                    {
                        tokensBought = boughtShareOwner.ShareAmount;
                    }

                    uint tokensOwned = 0;
                    if (property.RealWorldAssetDetails?.ShareOwners.TryGetValue(address, out var ownedShareOwner) ?? false)
                    {
                        tokensOwned = ownedShareOwner.ShareAmount;
                    }

                    var propertyOwnership = new PropertyOwnership
                    {
                        TokensBought = tokensBought,
                        TokensOwned = tokensOwned,
                        NftBase = property,
                    };

                    if (!ItemsDict.ContainsKey(propertyOwnership.Key))
                    {
                        Console.WriteLine("New property added to dict");
                        ItemsDict.Add(propertyOwnership.Key, propertyOwnership);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Loading = false;

            waitUsedDict[client.Endpoint.Key].TrySetResult();
        }

        public static long GetTotalPropertiesOwned() => ItemsDict.Values.Sum(x => x.TokensBought + x.TokensOwned);

        public static long GetTotalInvested() => ItemsDict.Values.Sum(x => (long)((x.TokensBought + x.TokensOwned) * ((INftXcavateMetadata)x.NftBase).XcavateMetadata?.Financials.PricePerToken ?? 0));
    }
}
