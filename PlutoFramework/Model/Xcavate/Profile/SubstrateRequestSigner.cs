using Substrate.NetApi;
using Substrate.NetApi.Model.Types;
using XcavateProfile.Client;
using XcavateProfileApiClient.Signing;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Signs profile API requests with the app's Substrate account, mirroring the package's own
    /// <c>SubstrateRequestSigner</c> without pulling the full <c>XcavateProfileApiClient</c>
    /// assembly (and its Substrate.NET.API dependency) into PlutoFramework.
    /// </summary>
    internal sealed class SubstrateRequestSigner(Account account) : IRequestSigner
    {
        public string Address => account.Value;

        public Task<byte[]> SignAsync(string payload) =>
            account.SignAsync(CryptoHelper.Hash(payload));

        public string EncodeSignature(byte[] signature) =>
            Utils.Bytes2HexString(signature);
    }
}
