using PlutoFramework.Constants;
using PlutoFramework.Model.AjunaExt;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Types;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using System.Numerics;
using UniqueryPlus;
using UniqueryPlus.Nfts;
using AssetKey = (PlutoFramework.Constants.EndpointEnum, PlutoFramework.Types.AssetPallet, System.Numerics.BigInteger);
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);
using XcavatePropertyKey = (PlutoFramework.Constants.EndpointEnum, uint);

namespace PlutoFramework.Model
{
    public enum ExtrinsicResult
    {
        Unknown,
        Success,
        Failed,
    }

    public enum NftOperation
    {
        // Has to be there due to binding
        None,

        Sent,
        Received,
    }

    public class TransactionAnalyzerModel
    {
        public static ExtrinsicResult GetExtrinsicResult(IEnumerable<ExtrinsicEvent> events)
        {
            if (events.Count() == 0)
                return ExtrinsicResult.Unknown;
            else
                return events.Last() switch
                {
                    ExtrinsicEvent { PalletName: "System", EventName: "ExtrinsicSuccess" } => ExtrinsicResult.Success,
                    ExtrinsicEvent { PalletName: "System", EventName: "ExtrinsicFailed" } => ExtrinsicResult.Failed,
                    _ => ExtrinsicResult.Unknown,
                };
        }

        public static string GetExtrinsicFailedMessage(IEnumerable<ExtrinsicEvent> events) => events.Last() switch
        {
            ExtrinsicEvent { PalletName: "System", EventName: "ExtrinsicFailed" } => events.Last().Parameters[0].Value,
            _ => string.Empty,
        };

        /// <summary>
        /// Analyze the events and return the currency changes for each address
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<string, Dictionary<AssetKey, Asset>>> AnalyzeCurrencyChangesInEventsAsync(
            SubstrateClientExt client,
            IEnumerable<ExtrinsicEvent> events,
            Endpoint endpoint,
            CancellationToken token,
            Dictionary<string, Dictionary<AssetKey, Asset>>? existingCurrencyChanges = null)
        {
            var result = existingCurrencyChanges ?? new Dictionary<string, Dictionary<AssetKey, Asset>>();

            foreach (var e in events)
            {
                IEnumerable<(string, AssetKey, BigInteger)> evaluated = e switch
                {
                    // Balances
                    ExtrinsicEvent { PalletName: "Balances", EventName: "Transfer" } => [
                        // From negative
                        (e.Parameters[0].Value, (endpoint.Key, AssetPallet.Native, 0), -BigInteger.Parse(e.Parameters[2].Value)),
                        // To positive
                        (e.Parameters[1].Value, (endpoint.Key, AssetPallet.Native, 0), BigInteger.Parse(e.Parameters[2].Value))
                    ],
                    ExtrinsicEvent { PalletName: "Balances", EventName: "Deposit" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.Native, 0), BigInteger.Parse(e.Parameters[1].Value))],
                    ExtrinsicEvent { PalletName: "Balances", EventName: "Withdraw" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.Native, 0), -BigInteger.Parse(e.Parameters[1].Value))],
                    ExtrinsicEvent { PalletName: "Balances", EventName: "Minted" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.Native, 0), BigInteger.Parse(e.Parameters[1].Value))],
                    ExtrinsicEvent { PalletName: "Balances", EventName: "Burned" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.Native, 0), -BigInteger.Parse(e.Parameters[1].Value))],

                    // Tokens
                    ExtrinsicEvent { PalletName: "Tokens", EventName: "Transfer" } => [
                        // From negative
                        (e.Parameters[1].Value, (endpoint.Key, AssetPallet.Tokens, BigInteger.Parse(e.Parameters[0].Value)), -BigInteger.Parse(e.Parameters[3].Value)),
                        // To positive
                        (e.Parameters[2].Value, (endpoint.Key, AssetPallet.Tokens, BigInteger.Parse(e.Parameters[0].Value)), BigInteger.Parse(e.Parameters[3].Value))
                    ],
                    ExtrinsicEvent { PalletName: "Tokens", EventName: "Deposited" } => [(e.Parameters[1].Value, (endpoint.Key, AssetPallet.Tokens, BigInteger.Parse(e.Parameters[0].Value)), BigInteger.Parse(e.Parameters[2].Value))],
                    ExtrinsicEvent { PalletName: "Tokens", EventName: "Withdrawn" } => [(e.Parameters[1].Value, (endpoint.Key, AssetPallet.Tokens, BigInteger.Parse(e.Parameters[0].Value)), -BigInteger.Parse(e.Parameters[2].Value))],

                    // Assets
                    ExtrinsicEvent { PalletName: "Assets", EventName: "Transferred" } => [
                        // From negative
                        (e.Parameters[1].Value, (endpoint.Key, AssetPallet.Assets, BigInteger.Parse(e.Parameters[0].Value)), -BigInteger.Parse(e.Parameters[3].Value)),
                        // To positive
                        (e.Parameters[2].Value, (endpoint.Key, AssetPallet.Assets, BigInteger.Parse(e.Parameters[0].Value)), BigInteger.Parse(e.Parameters[3].Value))
                    ],
                    ExtrinsicEvent { PalletName: "Assets", EventName: "Issued" } => [(e.Parameters[1].Value, (endpoint.Key, AssetPallet.Assets, BigInteger.Parse(e.Parameters[0].Value)), BigInteger.Parse(e.Parameters[2].Value))],
                    ExtrinsicEvent { PalletName: "Assets", EventName: "Burned" } => [(e.Parameters[1].Value, (endpoint.Key, AssetPallet.Assets, BigInteger.Parse(e.Parameters[0].Value)), BigInteger.Parse(e.Parameters[2].Value))],
                    ExtrinsicEvent { PalletName: "AssetsFreezer", EventName: "Frozen" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.AssetsFrozen, BigInteger.Parse(e.Parameters[1].Value)), -BigInteger.Parse(e.Parameters[2].Value))],
                    ExtrinsicEvent { PalletName: "AssetsHolder", EventName: "Held" } => [(e.Parameters[0].Value, (endpoint.Key, AssetPallet.AssetsReserved, BigInteger.Parse(e.Parameters[1].Value)), -BigInteger.Parse(e.Parameters[3].Value))],

                    // Fees
                    ExtrinsicEvent { PalletName: "TransactionPayment", EventName: "TransactionFeePaid" } => [("fee", (endpoint.Key, AssetPallet.Native, 0), -BigInteger.Parse(e.Parameters[1].Value) - BigInteger.Parse(e.Parameters[2].Value))],

                    // Handle more events ...
                    _ => []
                };

                foreach (var (address, key, amount) in evaluated)
                {
                    if (!result.ContainsKey(address))
                    {
                        result[address] = new Dictionary<AssetKey, Asset>();
                    }

                    if (!result[address].ContainsKey(key))
                    {
                        result[address][key] = key.Item2 switch
                        {
                            AssetPallet.Native => new Asset
                            {
                                Amount = 0,
                                Pallet = key.Item2,
                                Symbol = endpoint.Unit,
                                ChainIcon = endpoint.Icon,
                                DarkChainIcon = endpoint.DarkIcon,
                                AssetId = key.Item3,
                                Endpoint = endpoint,
                                Decimals = endpoint.Decimals
                            },
                            _ => (await AssetsMetadataModel.GetAssetMetadataAsync(client, key.Item2, key.Item3, token)).ToAsset()
                        };
                    }

                    result[address][key].Amount += (double)amount / Math.Pow(10, result[address][key].Decimals);
                }
            }

            // Remove emptry values
            foreach (var address in result.Keys)
            {
                foreach (var assetKey in result[address].Keys)
                {
                    if (result[address][assetKey].Amount == 0)
                    {
                        result[address].Remove(assetKey);
                    }
                }

                if (result[address].Keys.Count() == 0)
                {
                    result.Remove(address);
                }
            }

            return result;
        }

        /// <summary>
        /// Analyze the events and return the nft changes for each address
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<string, Dictionary<NftKey, NftAssetWrapper>>> AnalyzeNftChangesInEventsAsync(
            SubstrateClientExt client,
            IEnumerable<ExtrinsicEvent> events,
            Endpoint endpoint,
            CancellationToken token,
            Dictionary<string, Dictionary<NftKey, NftAssetWrapper>>? existingNftChanges = null)
        {
            return existingNftChanges ?? new Dictionary<string, Dictionary<NftKey, NftAssetWrapper>>();
        }

        /// <summary>
        /// Analyze the events and return the Xcavate property changes for each address
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<string, Dictionary<XcavatePropertyKey, PropertyTokenOwnershipChangeInfo>>> AnalyzeXcavatePropertyChangesInEventsAsync(
            SubstrateClientExt client,
            IEnumerable<ExtrinsicEvent> events,
            Endpoint endpoint,
            CancellationToken token,
            Dictionary<string, Dictionary<XcavatePropertyKey, PropertyTokenOwnershipChangeInfo>>? existingPropertyChanges = null)
        {
            return existingPropertyChanges ?? new Dictionary<string, Dictionary<XcavatePropertyKey, PropertyTokenOwnershipChangeInfo>>();
        }
    }
}
