using Substrate.NetApi;
using Newtonsoft.Json;
using PlutoFramework.Types;
using Substrate.NetApi.Model.Extrinsics;
using PlutoFramework.Constants;
using Substrate.NetApi.Model.Rpc;
using Substrate.NetApi.Model.Types;
using Substrate.NetApi.Model.Types.Base;

namespace PlutoFramework.Model.AjunaExt
{
	public class SubstrateClientExt
	{
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly SemaphoreSlim connectionLock = new(1, 1);
        private readonly Uri websocket;
        private readonly EndpointEnum clientEndpointKey;

        public ChargeType DefaultCharge;

        public bool CheckMetadata = false;
        public Endpoint Endpoint { get; set; }
        public Metadata CustomMetadata { get; set; }
        public SubstrateClient SubstrateClient { get; set; }

        private TaskCompletionSource<bool> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool hasConnectionAttempted;
        public async Task<bool> IsConnectedAsync()
        {
            if (SubstrateClient.IsConnected)
            {
                return true;
            }

            if (!hasConnectionAttempted)
            {
                return false;
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var completedTask = await Task.WhenAny(taskCompletionSource.Task, timeoutTask);

            if (completedTask == taskCompletionSource.Task)
            {
                // Task completed within timeout. Also verify the current websocket state because
                // the mobile OS can disconnect it after a successful connection while backgrounded.
                return await taskCompletionSource.Task && SubstrateClient.IsConnected;
            }
            else
            {
                // Timeout occurred.
                return SubstrateClient.IsConnected;
            }
        }

        public SubstrateClientExt(Endpoint endpoint, Uri fastestWebSocket, Substrate.NetApi.Model.Extrinsics.ChargeType chargeType) 
        {
            Endpoint = endpoint;
            websocket = fastestWebSocket;
            clientEndpointKey = endpoint.Key;

            SubstrateClient = GetSubstrateClient(endpoint.Key, fastestWebSocket);
        }

        /// <summary>
        /// Used only for Testing
        /// </summary>
        public SubstrateClientExt(EndpointEnum mockKey, Endpoint endpoint, Uri fastestWebSocket, Substrate.NetApi.Model.Extrinsics.ChargeType chargeType)
        {
            Endpoint = endpoint;
            websocket = fastestWebSocket;
            clientEndpointKey = mockKey;

            SubstrateClient = GetSubstrateClient(mockKey, fastestWebSocket);
        }

        public async Task<bool> EnsureConnectedAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            if (await IsConnectedAsync())
            {
                return true;
            }

            await connectionLock.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                if (await IsConnectedAsync())
                {
                    return true;
                }

                return await ConnectAndLoadMetadataAsync();
            }
            finally
            {
                connectionLock.Release();
            }
        }

        /// <summary>
        /// Appart from connecting to the endpoint, this method also loads the metadata
        /// </summary>
        /// <returns>True if connected successfully, False otherwise</returns>
        public virtual async Task<bool> ConnectAndLoadMetadataAsync()
        {
            try
            {
                Console.WriteLine("Connect base");

                if (hasConnectionAttempted && !SubstrateClient.IsConnected)
                {
                    try
                    {
                        SubstrateClient.Dispose();
                    }
                    catch
                    {
                    }

                    SubstrateClient = GetSubstrateClient(clientEndpointKey, websocket);
                }

                hasConnectionAttempted = true;

                await SubstrateClient.ConnectAsync();

                Console.WriteLine(SubstrateClient.MetaData is null);
                CustomMetadata = JsonConvert.DeserializeObject<Metadata>(SubstrateClient.MetaData.Serialize());

                Console.WriteLine("Serialized");

                foreach (SignedExtension signedExtension in CustomMetadata.NodeMetadata.Extrinsic.SignedExtensions)
                {
                    if (signedExtension.SignedIdentifier == "ChargeTransactionPayment")
                    {
                        DefaultCharge = ChargeTransactionPayment.Default();
                    }

                    if (signedExtension.SignedIdentifier == "ChargeAssetTxPayment")
                    {
                        DefaultCharge = ChargeAssetTxPayment.Default();
                    }

                    if (signedExtension.SignedIdentifier == "CheckMetadataHash")
                    {
                        CheckMetadata = true;
                    }

                    Console.WriteLine(signedExtension.SignedIdentifier);
                }

                SetConnectionResult(SubstrateClient.IsConnected);

                Console.WriteLine($"Actually connected: {Endpoint.Key} - {SubstrateClient.IsConnected}");

                return SubstrateClient.IsConnected;
            }
            catch(Exception e)
            {
                Console.WriteLine("SubstrateClientExt error: ");
                Console.WriteLine(e);
                SetConnectionResult(false);

                return false;
            }
        }

        private void SetConnectionResult(bool connected)
        {
            if (taskCompletionSource.Task.IsCompleted)
            {
                taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            taskCompletionSource.TrySetResult(connected);
        }

        public virtual async Task<string> SubmitExtrinsicAsync(Method method, Account account, TaskCompletionSource<string?> txHash, Action<string, ExtrinsicStatus> callback, uint lifeTime = 64, CancellationToken token = default)
        {

            ///
            /// This part is temporary fix before the next Substrate.Net.Api version, that would fix the code gen and sign metadata checks
            ///
            #region Temp
            var unCheckedExtrinsic = await GetTempUnCheckedExtrinsicAsync(method, account, lifeTime, token);
            #endregion


            string extrinsicId = await this.SubstrateClient.Author.SubmitAndWatchExtrinsicAsync(callback, Utils.Bytes2HexString(unCheckedExtrinsic.Encode()).ToLower(), token);

            return extrinsicId;
        }

        public async Task<TempUnCheckedExtrinsic> GetTempUnCheckedExtrinsicAsync(Method method, Account account, uint lifeTime, CancellationToken token, bool signed = true)
        {
            ///
            /// This part is temporary fix before the next Substrate.Net.Api version, that would fix the code gen and sign metadata checks
            ///
            #region Temp
            uint nonce = await SubstrateClient.System.AccountNextIndexAsync(account.Value, token);

            Hash startEra = await SubstrateClient.Chain.GetFinalizedHeadAsync(token);
            Era era = Era.Create(lifeTime, (await SubstrateClient.Chain.GetHeaderAsync(startEra, token)).Number.Value);

            TempUnCheckedExtrinsic uncheckedExtrinsic = new TempUnCheckedExtrinsic(signed, account, method, era, nonce, DefaultCharge, SubstrateClient.GenesisHash, startEra, this.Endpoint.AddressVersion, CheckMetadata);

            if (!signed) {
                return uncheckedExtrinsic;
            }

            TempPayload payload = uncheckedExtrinsic.GetPayload(SubstrateClient.RuntimeVersion);
            uncheckedExtrinsic.AddPayloadSignature(await account.SignAsync(payload.Encode()));
            #endregion

            return uncheckedExtrinsic;
        }

        private SubstrateClient GetSubstrateClient(EndpointEnum endpointKey, Uri websocket)
        {
            return new SubstrateClient(websocket, ChargeTransactionPayment.Default());
        }
    }
}

