using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// JSON-RPC 2.0 over an established Mobile Wallet Adapter session.
    ///
    /// The deprecated <c>reauthorize</c> and <c>sign_transactions</c> methods are not
    /// implemented. Reauthorization goes through <see cref="AuthorizeAsync"/> with an
    /// existing token, and signing a transaction without submitting it is not something
    /// this app needs.
    /// </summary>
    public sealed class MwaClient
    {
        /// <summary>
        /// Mobile Wallet Adapter error codes. -1 covers both a declining user and an
        /// authorization the wallet no longer honours.
        /// </summary>
        private const int ERROR_AUTHORIZATION_FAILED = -1;
        private const int ERROR_INVALID_PAYLOADS = -2;
        private const int ERROR_NOT_SIGNED = -3;
        private const int ERROR_TOO_MANY_PAYLOADS = -4;
        private const int ERROR_CLUSTER_NOT_SUPPORTED = -5;

        private readonly MwaSession session;

        private int nextRequestId = 1;

        public MwaClient(MwaSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Requests access to an account. Passing an existing <paramref name="authToken"/>
        /// reauthorizes it, which the wallet may grant without prompting the user again.
        /// </summary>
        public async Task<MwaAuthorizationResult> AuthorizeAsync(
            MwaIdentity identity,
            SolanaCluster cluster,
            string? authToken,
            CancellationToken token)
        {
            var chain = cluster.ToChainId();

            var response = await InvokeAsync<MwaAuthorizeRequest, MwaAuthorizeResponse>(
                "authorize",
                new MwaAuthorizeRequest
                {
                    Identity = identity,
                    Chain = chain,
                    AuthToken = authToken,
                },
                token);

            if (string.IsNullOrEmpty(response.AuthToken))
            {
                throw new MwaProtocolException("The wallet authorized the request but returned no auth token");
            }

            var account = response.Accounts?.FirstOrDefault();

            if (account?.Address is null)
            {
                throw new MwaProtocolException("The wallet authorized the request but returned no account");
            }

            return new MwaAuthorizationResult
            {
                AuthToken = response.AuthToken,
                Address = SolanaAddress.FromBase64(account.Address),
                Chain = chain,
                WalletUriBase = response.WalletUriBase,
                AccountLabel = account.Label,
            };
        }

        public Task DeauthorizeAsync(string authToken, CancellationToken token) =>
            InvokeAsync<MwaDeauthorizeRequest, JsonObject>(
                "deauthorize",
                new MwaDeauthorizeRequest { AuthToken = authToken },
                token);

        public Task<MwaCapabilities> GetCapabilitiesAsync(CancellationToken token) =>
            InvokeAsync<JsonObject, MwaCapabilities>("get_capabilities", new JsonObject(), token);

        /// <summary>
        /// Asks the wallet to sign arbitrary messages with the given base58 address.
        /// Returns the signed payloads, each the message with its signature appended.
        /// </summary>
        public async Task<IReadOnlyList<byte[]>> SignMessagesAsync(
            string base58Address,
            IEnumerable<byte[]> messages,
            CancellationToken token)
        {
            var response = await InvokeAsync<MwaSignMessagesRequest, MwaSignMessagesResponse>(
                "sign_messages",
                new MwaSignMessagesRequest
                {
                    Addresses = [SolanaAddress.ToBase64(base58Address)],
                    Payloads = messages.Select(Convert.ToBase64String).ToList(),
                },
                token);

            if (response.SignedPayloads is null)
            {
                throw new MwaProtocolException("The wallet returned no signed payloads");
            }

            return response.SignedPayloads.Select(Convert.FromBase64String).ToList();
        }

        /// <summary>
        /// Hands fully-formed transactions to the wallet, which signs and submits them.
        /// Returns the transaction signatures. No RPC endpoint is needed on this side.
        /// </summary>
        public async Task<IReadOnlyList<byte[]>> SignAndSendTransactionsAsync(
            IEnumerable<byte[]> transactions,
            CancellationToken token)
        {
            var response = await InvokeAsync<MwaSignAndSendTransactionsRequest, MwaSignAndSendTransactionsResponse>(
                "sign_and_send_transactions",
                new MwaSignAndSendTransactionsRequest
                {
                    Payloads = transactions.Select(Convert.ToBase64String).ToList(),
                },
                token);

            if (response.Signatures is null)
            {
                throw new MwaProtocolException("The wallet returned no transaction signatures");
            }

            return response.Signatures.Select(Convert.FromBase64String).ToList();
        }

        private async Task<TResponse> InvokeAsync<TRequest, TResponse>(
            string method,
            TRequest parameters,
            CancellationToken token)
        {
            var id = nextRequestId++;

            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(parameters),
            };

            await session.SendAsync(Encoding.UTF8.GetBytes(request.ToJsonString()), token);

            var responseBytes = await session.ReceiveAsync(token);

            JsonNode? response;

            try
            {
                response = JsonNode.Parse(Encoding.UTF8.GetString(responseBytes));
            }
            catch (JsonException ex)
            {
                throw new MwaProtocolException($"The wallet's reply to {method} was not valid JSON", ex);
            }

            if (response is null)
            {
                throw new MwaProtocolException($"The wallet's reply to {method} was empty");
            }

            ThrowIfError(method, response);

            var result = response["result"];

            if (result is null)
            {
                throw new MwaProtocolException($"The wallet's reply to {method} contained neither result nor error");
            }

            var deserialized = result.Deserialize<TResponse>();

            if (deserialized is null)
            {
                throw new MwaProtocolException($"The wallet's {method} result could not be deserialized");
            }

            return deserialized;
        }

        private static void ThrowIfError(string method, JsonNode response)
        {
            var error = response["error"];

            if (error is null)
            {
                return;
            }

            var code = error["code"]?.GetValue<int>();
            var message = error["message"]?.GetValue<string>() ?? "no message";

            throw code switch
            {
                ERROR_AUTHORIZATION_FAILED => new MwaAuthorizationException(
                    $"The wallet declined authorization: {message}"),
                ERROR_CLUSTER_NOT_SUPPORTED => new MwaAuthorizationException(
                    $"The wallet does not support the requested cluster: {message}"),
                ERROR_NOT_SIGNED => new MwaAuthorizationException(
                    $"The request was not signed: {message}"),
                ERROR_INVALID_PAYLOADS => new MwaProtocolException(
                    $"The wallet rejected the {method} payloads as invalid: {message}"),
                ERROR_TOO_MANY_PAYLOADS => new MwaProtocolException(
                    $"Too many payloads for a single {method} request: {message}"),
                _ => new MwaProtocolException($"The wallet returned error {code} for {method}: {message}"),
            };
        }
    }
}
