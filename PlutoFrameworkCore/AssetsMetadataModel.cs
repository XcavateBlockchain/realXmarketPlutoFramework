
using PlutoFramework.Model.AjunaExt;
using PlutoFramework.Types;
using System.Numerics;
using AssetKey = (PlutoFramework.Constants.EndpointEnum, PlutoFramework.Types.AssetPallet, System.Numerics.BigInteger);
using AssetMetadata = PlutoFramework.Types.AssetMetadata;

namespace PlutoFramework.Model
{
    public class AssetsMetadataModel
    {
        public static Dictionary<AssetKey, AssetMetadata> AssetsMetadataDict = new System.Collections.Generic.Dictionary<AssetKey, AssetMetadata>();

        public static async Task<AssetMetadata> GetAssetMetadataAsync(SubstrateClientExt client, AssetPallet pallet, BigInteger id, CancellationToken token)
        {
            Console.WriteLine("Here is what I got:");
            Console.WriteLine(client.Endpoint.Key);
            Console.WriteLine(pallet);
            Console.WriteLine(id);

            var basePallet = pallet.ToBaseAssetPallet();

            if (AssetsMetadataDict.ContainsKey((client.Endpoint.Key, basePallet, id)))
            {
                var asset = (AssetMetadata)AssetsMetadataDict[(client.Endpoint.Key, basePallet, id)].Clone();

                asset.Pallet = pallet;

                return asset;
            }

            if (AssetsModel.AssetsDict.ContainsKey((client.Endpoint.Key, basePallet, id)))
            {
                var asset = (AssetMetadata)AssetsModel.AssetsDict[(client.Endpoint.Key, basePallet, id)].Clone();

                asset.Pallet = pallet;

                return asset;
            }

            throw new NotImplementedException();
        }
    }
}
