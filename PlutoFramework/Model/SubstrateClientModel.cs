using Microsoft.VisualStudio.Threading;
using PlutoFramework.Components;
using PlutoFramework.Components.NetworkSelect;
using PlutoFramework.Constants;

namespace PlutoFramework.Model
{
    public class SubstrateClientModel
    {
        public static Dictionary<EndpointEnum, Task<PlutoFrameworkSubstrateClient>> Clients = new Dictionary<EndpointEnum, Task<PlutoFrameworkSubstrateClient>>();
        private static readonly object ClientsLock = new();

        /// <summary>
        /// Disconnects the endpoint efficiently
        /// </summary>
        /// <param name="endpointKey">Endpoint to disconnect</param>
        /// <param name="token">Cancellation token</param>
        public static async Task RemoveAndDisconnectSubstrateClientAsync(EndpointEnum endpointKey, CancellationToken token)
        {
            Task<PlutoFrameworkSubstrateClient>? clientTask;

            lock (ClientsLock)
            {
                if (!Clients.TryGetValue(endpointKey, out clientTask))
                {
                    return;
                }

                Clients.Remove(endpointKey);
            }

            (await clientTask.WithCancellation(token).ConfigureAwait(false)).Disconnect();
        }

        /// <summary>
        /// 
        /// - This method also waits for the client to connect to the websocket RPC if it has not connected yet.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>Main Substrate Client</returns>
        public static Task<PlutoFrameworkSubstrateClient> GetMainSubstrateClientAsync(CancellationToken token) => GetOrAddSubstrateClientAsync(DependencyService.Get<MultiNetworkSelectViewModel>().SelectedKey ?? EndpointEnum.None, token);

        /// <summary>
        ///
        /// - This method also waits for the client to connect to the websocket RPC if it has not connected yet.
        /// </summary>
        /// <param name="endpointKey">Endpoint to use</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Substrate client</returns>
        public static async Task<PlutoFrameworkSubstrateClient> GetOrAddSubstrateClientAsync(EndpointEnum endpointKey, CancellationToken token)
        {
            if (endpointKey == EndpointEnum.None)
            {
                throw new Exception("Endpoint was None");
            }

            Task<PlutoFrameworkSubstrateClient> clientTask;

            lock (ClientsLock)
            {
                if (!Clients.TryGetValue(endpointKey, out clientTask))
                {
                    clientTask = ConnectSubstrateClientAsync(endpointKey, token);
                    Clients[endpointKey] = clientTask;
                }
            }

            PlutoFrameworkSubstrateClient client;

            try
            {
                client = await clientTask.WithCancellation(token).ConfigureAwait(false);
            }
            catch
            {
                lock (ClientsLock)
                {
                    if (Clients.TryGetValue(endpointKey, out var cachedTask) && ReferenceEquals(cachedTask, clientTask))
                    {
                        Clients.Remove(endpointKey);
                    }
                }

                throw;
            }

            // Client is not connected, reconnect it
            if (!await client.IsConnectedAsync().WithCancellation(token).ConfigureAwait(false))
            {
                await client.ConnectAndLoadMetadataAsync().WithCancellation(token).ConfigureAwait(false);
            }

            return client;
        }

        private static async Task<PlutoFrameworkSubstrateClient> ConnectSubstrateClientAsync(EndpointEnum endpointKey, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            Endpoint endpoint = EndpointsModel.GetEndpoint(endpointKey);

            string bestWebSecket = await WebSocketModel.GetFastestWebSocketAsync(endpoint.URLs).WithCancellation(token).ConfigureAwait(false);

            var newClient = new PlutoFrameworkSubstrateClient(
                endpoint,
                new Uri(bestWebSecket),
                Substrate.NetApi.Model.Extrinsics.ChargeTransactionPayment.Default());

            await newClient.ConnectAndLoadMetadataAsync().WithCancellation(token).ConfigureAwait(false);

            return newClient;
        }

        /// <summary>
        /// Changes the substrate clients that are connected.
        /// Disconnects the ones that were connected before but are not present in the new list.
        /// Handles the UI changes
        /// </summary>
        /// <param name="endpointKeys">Endpoints that will be connected</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="reload">Reload data?</param>
        public static async Task ChangeConnectedClientsAsync(IEnumerable<EndpointEnum> endpointKeys, CancellationToken token, bool reload = true)
        {
            var requestedKeys = new HashSet<EndpointEnum>();
            foreach (var endpointKey in endpointKeys)
            {
                if (endpointKey != EndpointEnum.None)
                {
                    requestedKeys.Add(endpointKey);
                }
            }

            List<EndpointEnum> keysToRemove = [];

            lock (ClientsLock)
            {
                foreach (var key in Clients.Keys)
                {
                    if (!requestedKeys.Contains(key))
                    {
                        keysToRemove.Add(key);
                    }
                }
            }

            #region Remove clients that are not used anymore
            foreach (var key in keysToRemove)
            {
                await RemoveAndDisconnectSubstrateClientAsync(key, token).ConfigureAwait(false);
            }
            #endregion

            #region Connect new clients
            foreach (var endpointKey in requestedKeys)
            {
                await GetOrAddSubstrateClientAsync(endpointKey, token).ConfigureAwait(false);
            }
            #endregion

            if (!reload)
            {
                return;
            }

            await MainPageLayoutUpdater.ReloadAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles the UI changes
        /// </summary>
        /// <param name="selectedEndpointKey">Main endpoint</param>
        /// <param name="token">Cancellation token</param>
        public static async Task ChangeMainSubstrateClientAsync(EndpointEnum selectedEndpointKey, CancellationToken token)
        {
            var mainClient = await GetOrAddSubstrateClientAsync(selectedEndpointKey, token);

            await MainPageLayoutUpdater.ViewMainSubstrateClientLoadAsync(mainClient, token);

            // TODO update UI
        }
    }
}
