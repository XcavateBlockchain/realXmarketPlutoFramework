using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Solana;
using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Xcavate;
using Plutonication;
using Substrate.NetApi;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlutoFramework.Components.WebView
{
    /// <summary>
    /// Serves the injected Solana wallet, which a dapp reaches through the Wallet Standard
    /// and <c>@solana/wallet-adapter</c>.
    ///
    /// Sits beside <see cref="PolkadotExtensionWalletBridge"/> on the same WebView channel,
    /// distinguished by the <c>solana:</c> method prefix. The two share the connection
    /// approval screen and the sign-message sheet, so a dapp on either chain shows the user
    /// the same thing.
    /// </summary>
    public class SolanaWalletStandardBridge
    {
        /// <summary>
        /// Marks the methods this bridge answers, so one channel can carry both wallets
        /// without a second platform interface on each side.
        /// </summary>
        internal const string METHOD_PREFIX = "solana:";

        // Wallet Standard feature identifiers, reported to the page so it can populate the
        // account's feature list. Canonical strings — a typo here makes the wallet invisible.
        private const string STANDARD_CONNECT = "standard:connect";
        private const string STANDARD_DISCONNECT = "standard:disconnect";
        private const string STANDARD_EVENTS = "standard:events";
        private const string SOLANA_SIGN_MESSAGE = "solana:signMessage";
        private const string SOLANA_SIGN_TRANSACTION = "solana:signTransaction";
        private const string SOLANA_SIGN_AND_SEND_TRANSACTION = "solana:signAndSendTransaction";

        private const string SIGN_MESSAGE_REASON = "Sign a message for a web app";
        private const string SIGN_TRANSACTION_REASON = "Sign a transaction for a web app";
        private const string SEND_TRANSACTION_REASON = "Sign and send a transaction for a web app";

        /// <summary>
        /// Every Solana cluster this wallet will act on. Declared in full rather than limited
        /// to the app's own network setting: the dapp picks its own RPC endpoint, and
        /// wallet-adapter refuses to send when the account does not list the chain that
        /// endpoint maps to.
        /// </summary>
        internal static readonly string[] Chains = ["solana:mainnet", "solana:devnet", "solana:testnet"];

        internal static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>True when the method belongs to this bridge rather than the Polkadot one.</summary>
        public static bool Handles(string? method) =>
            method?.StartsWith(METHOD_PREFIX, StringComparison.Ordinal) == true;

        /// <summary>
        /// When set and returning true, a <c>solana:signMessage</c> whose bytes are a Profile
        /// API signing payload (see <see cref="ProfileApiPayloadModel"/>) is signed without
        /// the confirmation sheet. Left null everywhere except the messenger WebView, whose
        /// hosted dashboard authenticates every API call with such a signature and would
        /// otherwise raise the sheet each time. Transactions never take this path.
        /// </summary>
        public Func<bool>? AllowProfileApiAutoSign { get; init; }

        /// <summary>
        /// When set and returning true, a <c>solana:signMessage</c> whose bytes are a GraphQL
        /// POST signing payload (<see cref="ProfileApiPayloadModel.IsGraphqlPostPayload(byte[])"/>)
        /// signs without the confirmation sheet and without the password/biometric unlock:
        /// the key is read straight from secure storage. Left null everywhere except the
        /// messenger WebView, whose hosted dashboard signs such a payload on every
        /// state-changing GraphQL call. A Mobile Wallet Adapter key still shows its approval
        /// in the wallet app. Transactions never take this path.
        /// </summary>
        public Func<bool>? AllowNoAuthSign { get; init; }

        public async Task<string> HandleAsync(string requestJson)
        {
            WalletBridgeRequest request;

            try
            {
                request = JsonSerializer.Deserialize<WalletBridgeRequest>(requestJson, SerializerOptions)
                    ?? throw new InvalidOperationException("Unable to parse Solana wallet bridge request.");
            }
            catch (Exception ex)
            {
                return SerializeResponse(new WalletBridgeResponse { Id = null, Error = ex.Message });
            }

            object? result = null;
            string? error = null;

            try
            {
                result = request.Method switch
                {
                    "solana:connect" => await HandleConnectAsync(request.Payload),
                    "solana:disconnect" => HandleDisconnect(request.Payload),
                    "solana:signMessage" => await HandleSignMessageAsync(
                        request.Payload,
                        AllowProfileApiAutoSign?.Invoke() == true,
                        AllowNoAuthSign?.Invoke() == true),
                    "solana:signTransaction" => await HandleSignTransactionAsync(request.Payload),
                    "solana:signAndSendTransaction" => await HandleSignAndSendTransactionAsync(request.Payload),
                    _ => throw new NotSupportedException(
                        $"Method '{request.Method}' is not supported by the Pluto Solana wallet bridge."),
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return SerializeResponse(new WalletBridgeResponse
            {
                Id = request.Id,
                Result = result,
                Error = error
            });
        }

        /// <summary>
        /// Backs <c>standard:connect</c>. Returns the authorized account, or rejects when the
        /// user declines.
        /// </summary>
        /// <remarks>
        /// Reads the stored key without unlocking it, so connecting never costs a password or
        /// biometric prompt. Only an actual signature does.
        /// </remarks>
        private static async Task<object> HandleConnectAsync(JsonElement? payload)
        {
            var request = payload?.Deserialize<ConnectPayload>(SerializerOptions)
                ?? throw new InvalidOperationException("Missing payload for connect request.");

            var dAppInfo = ExtensionWebViewModel.TabInfos[request.TabId];

            if (!await DAppApprovalModel.RequestAsync(dAppInfo, request.Silent))
            {
                // A silent request is a reconnect the user did not initiate, so an empty
                // result is the answer. An explicit one they declined is a rejection.
                if (request.Silent)
                {
                    return new ConnectResult { Accounts = [] };
                }

                throw new InvalidOperationException("The connection request was rejected.");
            }

            var account = await LoadAccountAsync()
                ?? throw new InvalidOperationException("No Solana account is available.");

            return new ConnectResult { Accounts = [account] };
        }

        /// <summary>
        /// Backs <c>standard:disconnect</c>: forgets the approval so a later connect asks again.
        /// </summary>
        private static object? HandleDisconnect(JsonElement? payload)
        {
            var request = payload?.Deserialize<ConnectPayload>(SerializerOptions);

            if (request is not null && ExtensionWebViewModel.TabInfos.TryGetValue(request.TabId, out var dAppInfo))
            {
                DAppApprovalModel.Revoke(dAppInfo.Url);
            }

            return null;
        }

        /// <summary>
        /// Backs <c>solana:signMessage</c> through the same bottom sheet the Polkadot
        /// <c>signRaw</c> uses, so both chains show the user one screen.
        /// </summary>
        private static async Task<object> HandleSignMessageAsync(
            JsonElement? payload, bool allowProfileApiAutoSign, bool allowNoAuthSign)
        {
            var request = payload?.Deserialize<SignMessagePayload>(SerializerOptions)
                ?? throw new InvalidOperationException("Missing payload for signMessage request.");

            var message = Convert.FromBase64String(request.Message);

            // Read without unlocking: the key is only unlocked once the user taps Sign,
            // matching how the Polkadot sheet behaves.
            var address = KeysModel.GetSolanaAddress()
                ?? throw new InvalidOperationException("No Solana account is available.");

            if (!string.IsNullOrEmpty(request.Address) &&
                !string.Equals(address, request.Address, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Requested account does not match the active wallet address.");
            }

            // A GraphQL call the whitelisted dashboard makes: the strongest routine. It signs
            // without the sheet and without the password/biometric unlock, reading the key
            // straight from secure storage. A Mobile Wallet Adapter key still gets its
            // approval in the wallet app, which no local path can skip.
            if (allowNoAuthSign && ProfileApiPayloadModel.IsGraphqlPostPayload(message))
            {
                var noAuthAccount = await PlutoFrameworkSolanaAccount.ResolveNoAuthAsync()
                    ?? throw new InvalidOperationException("No Solana account is available.");

                var noAuthSignature = await noAuthAccount
                    .SignMessageAsync(message, SIGN_MESSAGE_REASON, CancellationToken.None)
                    .ConfigureAwait(false);

                return new SignMessageResult
                {
                    SignedMessage = request.Message,
                    Signature = Convert.ToBase64String(noAuthSignature),
                };
            }

            // A recognised Profile API authentication: routine, requested for every API call
            // the dashboard makes, so it signs without the sheet. The account still resolves
            // through the normal unlock, so a protected key keeps its own prompt.
            if (allowProfileApiAutoSign && ProfileApiPayloadModel.IsProfileApiSignPayload(message, DateTime.UtcNow))
            {
                var autoAccount = await PlutoFrameworkSolanaAccount.ResolveAsync(SIGN_MESSAGE_REASON)
                    ?? throw new InvalidOperationException("No Solana account is available.");

                var autoSignature = await autoAccount
                    .SignMessageAsync(message, SIGN_MESSAGE_REASON, CancellationToken.None)
                    .ConfigureAwait(false);

                return new SignMessageResult
                {
                    SignedMessage = request.Message,
                    Signature = Convert.ToBase64String(autoSignature),
                };
            }

            var signatureTask = new TaskCompletionSource<byte[]>();

            var popupViewModel = DependencyService.Get<WebSignRawPopupViewModel>();

            // Bridge callbacks arrive off the UI thread on Android, and these setters drive
            // bindings.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                popupViewModel.SignatureTask = signatureTask;

                popupViewModel.Signer = async bytes =>
                {
                    var account = await PlutoFrameworkSolanaAccount.ResolveAsync(SIGN_MESSAGE_REASON)
                        ?? throw new InvalidOperationException("No Solana account is available.");

                    return await account.SignMessageAsync(bytes, SIGN_MESSAGE_REASON, CancellationToken.None);
                };

                popupViewModel.Message = new RawMessage
                {
                    type = "bytes",
                    data = Utils.Bytes2HexString(message).ToLowerInvariant(),
                    address = address,
                };

                popupViewModel.IsVisible = true;
            });

            var signature = await signatureTask.Task.ConfigureAwait(false);

            return new SignMessageResult
            {
                // Solana signs the message as given, so what was signed is what arrived.
                SignedMessage = request.Message,
                Signature = Convert.ToBase64String(signature),
            };
        }

        /// <summary>
        /// Backs <c>solana:signTransaction</c>. Reachable only for a locally held key, which
        /// is the only account type that advertises the feature.
        /// </summary>
        private static async Task<object> HandleSignTransactionAsync(JsonElement? payload)
        {
            var request = payload?.Deserialize<SignTransactionPayload>(SerializerOptions)
                ?? throw new InvalidOperationException("Missing payload for signTransaction request.");

            var account = await ResolveForSigningAsync(SIGN_TRANSACTION_REASON);

            var signed = await account.SignWireTransactionAsync(
                Convert.FromBase64String(request.Transaction), SIGN_TRANSACTION_REASON, CancellationToken.None);

            return new SignTransactionResult { SignedTransaction = Convert.ToBase64String(signed) };
        }

        /// <summary>
        /// Backs <c>solana:signAndSendTransaction</c>, submitting on the network the dapp
        /// named rather than the app's own setting.
        /// </summary>
        private static async Task<object> HandleSignAndSendTransactionAsync(JsonElement? payload)
        {
            var request = payload?.Deserialize<SignAndSendTransactionPayload>(SerializerOptions)
                ?? throw new InvalidOperationException("Missing payload for signAndSendTransaction request.");

            var cluster = ParseChain(request.Chain);

            var account = await ResolveForSigningAsync(SEND_TRANSACTION_REASON);

            var signature = await account.SignAndSendWireTransactionAsync(
                Convert.FromBase64String(request.Transaction), cluster, SEND_TRANSACTION_REASON, CancellationToken.None);

            return new SignAndSendTransactionResult { Signature = Convert.ToBase64String(signature) };
        }

        /// <summary>
        /// Strict on purpose. <c>SolanaClusterExtensions.FromChainId</c> resolves anything
        /// unrecognised to Mainnet, which is the right default for a stored key but the wrong
        /// one here: it would submit a transaction meant for another network to Mainnet.
        /// </summary>
        private static SolanaCluster ParseChain(string? chain) => chain switch
        {
            "solana:mainnet" => SolanaCluster.Mainnet,
            "solana:devnet" => SolanaCluster.Devnet,
            "solana:testnet" => SolanaCluster.Testnet,
            _ => throw new NotSupportedException($"This wallet does not support the chain '{chain}'."),
        };

        private static async Task<PlutoFrameworkSolanaAccount> ResolveForSigningAsync(string reason) =>
            await PlutoFrameworkSolanaAccount.ResolveAsync(reason)
                ?? throw new InvalidOperationException("No Solana account is available.");

        /// <summary>
        /// Describes the configured Solana account to the page, reading only what is stored
        /// in the clear: the key's type and its public address.
        /// </summary>
        internal static async Task<InjectedSolanaAccount?> LoadAccountAsync()
        {
            var lockedKey = (await KeysDatabase.GetAllKeysOfTypeAsync(
                KeyTypeEnum.SolanaMnemonic, KeyTypeEnum.SolanaMwa)).FirstOrDefault();

            if (lockedKey is null)
            {
                return null;
            }

            return new InjectedSolanaAccount
            {
                Address = lockedKey.PublicKey,
                PublicKey = Convert.ToBase64String(SolanaBase58.Decode(lockedKey.PublicKey)),
                Chains = Chains,
                Features = FeaturesFor(lockedKey.Type),
                Label = lockedKey.Type.GetName(),
            };
        }

        /// <summary>
        /// wallet-adapter checks each call against the account's feature list, so this is
        /// where a key type declares what it can actually do.
        /// </summary>
        private static string[] FeaturesFor(KeyTypeEnum type) => type switch
        {
            KeyTypeEnum.SolanaMnemonic =>
            [
                STANDARD_CONNECT,
                STANDARD_DISCONNECT,
                STANDARD_EVENTS,
                SOLANA_SIGN_MESSAGE,
                SOLANA_SIGN_TRANSACTION,
                SOLANA_SIGN_AND_SEND_TRANSACTION,
            ],

            // Mobile Wallet Adapter 2.0 deprecated sign_transactions and made
            // sign_and_send_transactions mandatory, so signing without submitting is not
            // offered rather than advertised and then refused.
            _ =>
            [
                STANDARD_CONNECT,
                STANDARD_DISCONNECT,
                STANDARD_EVENTS,
                SOLANA_SIGN_MESSAGE,
                SOLANA_SIGN_AND_SEND_TRANSACTION,
            ],
        };

        private static string SerializeResponse(WalletBridgeResponse response) =>
            JsonSerializer.Serialize(response, SerializerOptions);

        private sealed class WalletBridgeRequest
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("method")]
            public string Method { get; set; } = string.Empty;

            [JsonPropertyName("payload")]
            public JsonElement? Payload { get; set; }
        }

        private sealed class WalletBridgeResponse
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("result")]
            public object? Result { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }

        private sealed record ConnectPayload
        {
            [JsonPropertyName("tabId")]
            public uint TabId { get; set; }

            /// <summary>Answer from cached approval only, never prompting.</summary>
            [JsonPropertyName("silent")]
            public bool Silent { get; set; }
        }

        private sealed record SignMessagePayload
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("address")]
            public string? Address { get; set; }
        }

        private sealed record SignTransactionPayload
        {
            [JsonPropertyName("transaction")]
            public string Transaction { get; set; } = string.Empty;
        }

        private sealed record SignAndSendTransactionPayload
        {
            [JsonPropertyName("transaction")]
            public string Transaction { get; set; } = string.Empty;

            [JsonPropertyName("chain")]
            public string? Chain { get; set; }
        }

        private sealed class ConnectResult
        {
            [JsonPropertyName("accounts")]
            public required IEnumerable<InjectedSolanaAccount> Accounts { get; set; }
        }

        private sealed class SignMessageResult
        {
            [JsonPropertyName("signedMessage")]
            public required string SignedMessage { get; set; }

            [JsonPropertyName("signature")]
            public required string Signature { get; set; }
        }

        private sealed class SignTransactionResult
        {
            [JsonPropertyName("signedTransaction")]
            public required string SignedTransaction { get; set; }
        }

        private sealed class SignAndSendTransactionResult
        {
            [JsonPropertyName("signature")]
            public required string Signature { get; set; }
        }
    }

    /// <summary>
    /// A Wallet Standard account as the page sees it. <c>publicKey</c> travels base64-encoded
    /// and is rebuilt into a Uint8Array there, JSON having no byte-array form.
    /// </summary>
    public sealed class InjectedSolanaAccount
    {
        [JsonPropertyName("address")]
        public required string Address { get; set; }

        [JsonPropertyName("publicKey")]
        public required string PublicKey { get; set; }

        [JsonPropertyName("chains")]
        public required string[] Chains { get; set; }

        [JsonPropertyName("features")]
        public required string[] Features { get; set; }

        [JsonPropertyName("label")]
        public required string Label { get; set; }
    }
}
