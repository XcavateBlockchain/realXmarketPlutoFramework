using PlutoFramework.Types;
using PlutoFrameworkCore;
using AssetKey = (PlutoFramework.Constants.EndpointEnum, PlutoFramework.Types.AssetPallet, System.Numerics.BigInteger);

namespace PlutoFramework.Model
{
    public interface IBalancesDatabaseSaver
    {
        public Task<int> SaveBalanceAsync(Asset asset);
    }
    public class AssetsModel
    {
        public static void Clear()
        {
            AssetsDict.Clear();
            UsdSum = 0.0;
        }
        public static IBalancesDatabaseSaver? DatabaseSaver { get; set; } = null;

        public static double UsdSum = 0.0;

        public static Dictionary<AssetKey, Asset> AssetsDict = new Dictionary<AssetKey, Asset>();

        // Check whether the given asset is allowed by the whitelist.
        public static bool IsAssetWhitelisted(Asset asset)
        {
            var whitelist = PlutoConfigurationModel.WhitelistedTokens;
            if (whitelist == null || whitelist.Count == 0)
            {
                return true; // no whitelisting applied
            }

            var key = (asset.Endpoint.Key, asset.Pallet, asset.AssetId);
            return whitelist.Contains(key);
        }

        // Add or update an asset only if it passes whitelist checks.
        public static void AddOrUpdateAsset(Asset asset, bool overwrite = true)
        {
            if (!IsAssetWhitelisted(asset))
            {
                return;
            }

            var key = (asset.Endpoint.Key, asset.Pallet, asset.AssetId);

            if (!AssetsDict.ContainsKey(key) || overwrite)
            {
                AssetsDict[key] = asset;
            }
        }

        // Remove any assets that are not on the current whitelist.
        public static void EnforceWhitelist()
        {
            var whitelist = PlutoConfigurationModel.WhitelistedTokens;
            if (whitelist == null || whitelist.Count == 0)
            {
                return; // no whitelisting applied
            }

            var keysToRemove = AssetsDict.Where(kv => !IsAssetWhitelisted(kv.Value)).Select(kv => kv.Key).ToList();
            foreach (var key in keysToRemove)
            {
                AssetsDict.Remove(key);
            }
        }

        public static IEnumerable<Asset> GetAssetsWithSymbol(string symbol)
        {
            return AssetsDict.Values
                     .Where(asset => asset.Symbol.Equals(symbol, StringComparison.Ordinal));
        }

        public static void LoadAssets(IEnumerable<Asset> assets, bool overwrite = false)
        {
            foreach (var asset in assets)
            {
                AddOrUpdateAsset(asset, overwrite);
            }

            CalculateTotalUsdBalance();
        }

        public static Asset? GetFirstOwnedAsset(IEnumerable<AssetKey> assetKeys)
        {
            var assetKeysList = assetKeys.ToList();

            if (assetKeysList.Count == 0)
            {
                return null;
            }

            var filteredAssets = AssetsDict
                .Where((pair) => pair.Value.Amount > 0)
                .Where((pair) => assetKeysList.Contains(pair.Key));

            var firstOwnedAsset = filteredAssets
                .Select(pair => pair.Value)
                .FirstOrDefault();

            if (firstOwnedAsset is not null)
            {
                return firstOwnedAsset;
            }

            if (AssetsDict.TryGetValue(assetKeysList.First(), out Asset? value))
            {
                return value;
            }

            return null;
        }

        public static void UpdateUsdBalance()
        {
            double usdSumValue = 0.0;

            foreach (var asset in AssetsDict.Values.Where(a => a.Pallet == AssetPallet.Native || a.Pallet == AssetPallet.Assets || a.Pallet == AssetPallet.Tokens))
            {
                double spotPrice = Model.HydraDX.Sdk.GetSpotPrice(asset.Symbol) ?? 0;
                asset.UsdValue = asset.Amount * spotPrice;
                usdSumValue += asset.UsdValue;
            }

            UsdSum = usdSumValue;
        }

        public static void CalculateTotalUsdBalance()
        {
            double usdSumValue = 0.0;

            foreach (var asset in AssetsDict.Values.Where(a => a.Pallet == AssetPallet.Native || a.Pallet == AssetPallet.Assets || a.Pallet == AssetPallet.Tokens))
            {
                try
                {
                    usdSumValue += asset.UsdValue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Calculate total usd exception: ");
                    Console.WriteLine(ex);
                }
            }

            UsdSum = usdSumValue;
        }
    }
}

