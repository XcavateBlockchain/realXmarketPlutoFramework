using Substrate.NetApi;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using XcavatePaseo.NetApi.Generated;
using XcavatePaseo.NetApi.Generated.Model.sp_core.crypto;

namespace PlutoFramework.Model.Xcavate
{
    public class RealWorldAssetsModel
    {
        public static async Task<uint> GetRealWorldAssetTokensOwnedAsync(SubstrateClientExt substrateClient, U32 propertyId, string address, CancellationToken token)
        {
            var accountId = new AccountId32();
            accountId.Create(Utils.GetPublicKeyFrom(address));

            var tokensOwned = await substrateClient.RealWorldAssetStorage.PropertyOwnerToken(new BaseTuple<U32, AccountId32>(propertyId, accountId), null, token);

            return tokensOwned;
        }
    }
}
