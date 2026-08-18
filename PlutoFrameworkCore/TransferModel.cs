using Substrate.NetApi;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using PlutoFramework.Model.AjunaExt;
using System.Numerics;

namespace PlutoFramework.Model
{
    public class TransferModel
    {
        /// <summary>
        /// SCALE encoding of MultiAddress::Id - variant index 0 followed by the 32 byte AccountId
        /// </summary>
        private static byte[] EncodeMultiAddressId(byte[] publicKey)
        {
            var bytes = new List<byte> { 0 };
            bytes.AddRange(publicKey);
            return bytes.ToArray();
        }

        public static Method NativeTransfer(SubstrateClientExt client, string address, BigInteger amount)
        {
            // Later: Recognize what type of the address it is and convert it into ss58 one
            var publicKey = Utils.GetPublicKeyFrom(address);

            var baseComAmount = new BaseCom<U128>();
            baseComAmount.Create(amount);

            var (palletIndex, callIndex) = PalletCallModel.GetPalletAndCallIndex(client, "Balances", "transfer_keep_alive");

            System.Collections.Generic.List<byte> byteArray = new List<byte>();
            byteArray.AddRange(client.Endpoint.AddressVersion switch
            {
                0u => publicKey,
                // Maybe handle more variants?
                _ => EncodeMultiAddressId(publicKey),
            });
            byteArray.AddRange(baseComAmount.Encode());
            return new Method(palletIndex, "Balances", callIndex, "transfer_keep_alive", byteArray.ToArray());
        }

        public static Method AssetsTransfer(SubstrateClientExt client, string address, BigInteger assetId, CompactInteger amount)
        {
            // Even if the assetId is different type than U128,
            // like for example U32, it will still result in the same bytes after the .Encode().
            var baseComAssetId = new BaseCom<U128>();
            baseComAssetId.Create(assetId);

            // Later: Recognize what type of the address it is and convert it into ss58 one
            var publicKey = Utils.GetPublicKeyFrom(address);

            var baseComAmount = new BaseCom<U128>();
            baseComAmount.Create(amount);

            var (palletIndex, callIndex) = PalletCallModel.GetPalletAndCallIndex(client, "Assets", "transfer_keep_alive");

            Console.WriteLine("Pallet index: " + palletIndex + "    Call index: " + callIndex);

            System.Collections.Generic.List<byte> byteArray = new List<byte>();
            byteArray.AddRange(baseComAssetId.Encode());
            byteArray.AddRange(EncodeMultiAddressId(publicKey));
            byteArray.AddRange(baseComAmount.Encode());
            return new Method(palletIndex, "Assets", callIndex, "transfer_keep_alive", byteArray.ToArray());
        }

        public static Method ForeignAssetsTransfer(SubstrateClientExt client, string address, BigInteger assetId, CompactInteger amount)
        {
            // Even if the assetId is different type than U128,
            // like for example U32, it will still result in the same bytes after the .Encode().
            var baseComAssetId = new BaseCom<U128>();
            baseComAssetId.Create(assetId);

            // Later: Recognize what type of the address it is and convert it into ss58 one
            var publicKey = Utils.GetPublicKeyFrom(address);

            var baseComAmount = new BaseCom<U128>();
            baseComAmount.Create(amount);

            var (palletIndex, callIndex) = PalletCallModel.GetPalletAndCallIndex(client, "ForeignAssets", "transfer_keep_alive");

            Console.WriteLine("Pallet index: " + palletIndex + "    Call index: " + callIndex);

            System.Collections.Generic.List<byte> byteArray = new List<byte>();
            byteArray.AddRange(baseComAssetId.Encode());
            byteArray.AddRange(EncodeMultiAddressId(publicKey));
            byteArray.AddRange(baseComAmount.Encode());
            return new Method(palletIndex, "ForeignAssets", callIndex, "transfer_keep_alive", byteArray.ToArray());
        }

        public static Method TokensTransfer(SubstrateClientExt client, string address, BigInteger assetId, CompactInteger amount)
        {
            var publicKey = Utils.GetPublicKeyFrom(address);

            // Later: Check that the chain really supports U32 for token ids
            U32 currencyId = new U32((uint)assetId);

            var baseComAmount = new BaseCom<U128>();
            baseComAmount.Create(amount);

            System.Collections.Generic.List<byte> byteArray = new List<byte>();
            byteArray.AddRange(publicKey);
            byteArray.AddRange(currencyId.Encode());
            byteArray.AddRange(baseComAmount.Encode());

            var (palletIndex, callIndex) = PalletCallModel.GetPalletAndCallIndex(client, "Tokens", "transfer_keep_alive");

            Console.WriteLine("Pallet index: " + palletIndex + "    Call index: " + callIndex);

            return new Method(palletIndex, "Tokens", callIndex, "transfer_keep_alive", byteArray.ToArray());
        }
    }
}
