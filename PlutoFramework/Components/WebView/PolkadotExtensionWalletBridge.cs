using PlutoFramework.Components.MessagePopup;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Xcavate;
using Plutonication;
using Substrate.NetApi;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Rpc;
using Substrate.NetApi.Model.Types;
using Substrate.NetApi.Model.Types.Base;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlutoFramework.Components.WebView;

public record EnablePayload
{
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = string.Empty;

    [JsonPropertyName("tabId")]
    public uint TabId { get; set; }
}

public record DAppInfo
{
    [JsonPropertyName("icon")]
    public required ImageSource Icon { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

public class PolkadotExtensionWalletBridge
{
    public static TaskCompletionSource<byte[]> SignatureTask = new();

    internal const string ProviderName = "polkadot-js";

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// When set and returning true, a <c>signRaw</c> whose bytes decode to a Profile API
    /// signing payload (see <see cref="ProfileApiPayloadModel"/>) is signed without the
    /// confirmation sheet. Left null everywhere except the messenger WebView, whose hosted
    /// dashboard authenticates every API call with such a signature and would otherwise
    /// raise the sheet each time. <c>signPayload</c> transactions never take this path.
    /// </summary>
    public Func<bool>? AllowProfileApiAutoSign { get; init; }

    public async Task<string> HandleAsync(string requestJson)
    {
        WalletBridgeRequest request;

        try
        {
            request = System.Text.Json.JsonSerializer.Deserialize<WalletBridgeRequest>(requestJson, SerializerOptions)
                ?? throw new InvalidOperationException("Unable to parse wallet bridge request.");
        }
        catch (Exception ex)
        {
            return SerializeResponse(new WalletBridgeResponse
            {
                Id = null,
                Error = ex.Message
            });
        }

        object? result = null;
        string? error = null;

        try
        {
            switch (request.Method)
            {
                case "enable":
                    result = await HandleEnableAsync(request.Payload);
                    break;
                case "accounts.get":
                    result = HandleAccounts();
                    break;
                case "signRaw":
                    result = await HandleSignRawAsync(request, AllowProfileApiAutoSign?.Invoke() == true)
                        .ConfigureAwait(false);
                    break;
                case "signPayload":
                    result = await HandleSignPayloadAsync(request).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException($"Method '{request.Method}' is not supported by Pluto wallet bridge.");
            }
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

    private static async Task<object> HandleEnableAsync(JsonElement? payload)
    {
        if (payload == null)
        {
            return new { approved = false, provider = ProviderName };
        }

        EnablePayload enablePayload = payload!.Value.Deserialize<EnablePayload>(SerializerOptions)!;

        var dAppInfo = ExtensionWebViewModel.TabInfos[enablePayload.TabId];

        // Shared with the injected Solana wallet so both show the same connection screen.
        var approved = await DAppApprovalModel.RequestAsync(dAppInfo);

        return new { approved, provider = ProviderName };
    }

    private static IEnumerable<InjectedAccount> HandleAccounts()
    {
        if (!KeysModel.HasSubstrateKey())
        {
            return [];
        }

        var address = Utils.GetAddressFrom(Utils.GetPublicKeyFrom(KeysModel.GetSubstrateKey()), 0);
        var appName = "Account";

        return [
            new InjectedAccount
            {
                Address = address,
                Meta = new InjectedAccountMeta
                {
                    Name = appName,
                    Source = ProviderName
                }
            }
        ];
    }

    private static async Task<SignerResultPayload> HandleSignRawAsync(WalletBridgeRequest request, bool allowProfileApiAutoSign)
    {
        Console.WriteLine("Sign raw called");

        SignatureTask = new TaskCompletionSource<byte[]>();

        if (!request.Payload.HasValue)
        {
            throw new InvalidOperationException("Missing payload for signRaw request.");
        }

        var signRawPayload = request.Payload.Value.Deserialize<SignRawPayload>(SerializerOptions)
            ?? throw new InvalidOperationException("Unable to parse signRaw payload.");

        Console.WriteLine(signRawPayload);

        if (!KeysModel.HasSubstrateKey())
        {
            throw new InvalidOperationException("No Substrate account is available.");
        }

        var expectedAddress = KeysModel.GetSubstrateKey();
        if (!string.Equals(expectedAddress, signRawPayload.Address, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Requested account does not match the active wallet address.");
        }

        // A recognised Profile API authentication signs without the sheet, through the same
        // signer the sheet's Sign button uses, so the signature is identical either way.
        // Anything else - including hex that fails to decode - falls through to the sheet.
        if (allowProfileApiAutoSign
            && signRawPayload.Type == "bytes"
            && TryDecodeHex(signRawPayload.Data, out var rawMessage)
            && ProfileApiPayloadModel.IsProfileApiSignPayload(rawMessage, DateTime.UtcNow))
        {
            var autoSignature = await WebSignRawPopupViewModel
                .SignWithSubstrateAccountAsync(rawMessage)
                .ConfigureAwait(false);

            return new SignerResultPayload
            {
                Id = signRawPayload.Id ?? request.Id ?? Guid.NewGuid().ToString("N"),
                Signature = Utils.Bytes2HexString(autoSignature).ToLowerInvariant()
            };
        }

        try
        {
            if (signRawPayload.Type != "bytes")
            {
                throw new Exception("Message signing is supported only for bytes format");
            }

            var messageSignRequest = DependencyService.Get<WebSignRawPopupViewModel>();

            messageSignRequest.SignatureTask = SignatureTask;

            messageSignRequest.Message = new RawMessage
            {
                type = signRawPayload.Type,
                data = signRawPayload.Data,
                address = signRawPayload.Address,
            };

            messageSignRequest.IsVisible = true;
        }
        catch (Exception ex)
        {
            var messagePopup = DependencyService.Get<MessagePopupViewModel>();

            messagePopup.Title = "ConnectionRequestView Error";
            messagePopup.Text = ex.Message;

            messagePopup.IsVisible = true;
        }

        var signature = await SignatureTask.Task.ConfigureAwait(false);

        return new SignerResultPayload
        {
            Id = signRawPayload.Id ?? request.Id ?? Guid.NewGuid().ToString("N"),
            Signature = Utils.Bytes2HexString(signature).ToLowerInvariant()
        };
    }

    private static async Task<SignerResultPayload> HandleSignPayloadAsync(WalletBridgeRequest request)
    {
        try
        {
            Console.WriteLine("Sign payload called");

            if (!request.Payload.HasValue)
            {
                throw new InvalidOperationException("Missing payload for signPayload request.");
            }

            var payload = request.Payload.Value.Deserialize<SignerPayloadJson>(SerializerOptions)
                ?? throw new InvalidOperationException("Unable to parse signPayload payload.");

            if (!KeysModel.HasSubstrateKey())
            {
                throw new InvalidOperationException("No Substrate account is available inside Pluto wallet.");
            }

            byte[] methodBytes = Utils.HexToByteArray(payload.Method);

            var genesisHashLower = payload.GenesisHash.ToLowerInvariant();

            Console.WriteLine("Genesis: " + genesisHashLower);

            if (!Constants.Endpoints.HashToKey.TryGetValue(genesisHashLower, out EndpointEnum endpointKey))
            {
                throw new InvalidOperationException($"Unsupported genesis hash: {payload.GenesisHash}");
            }

            var client = await Model.SubstrateClientModel.GetOrAddSubstrateClientAsync(endpointKey, CancellationToken.None);

            (var pallet, var call) = PalletCallModel.GetPalletAndCallName(client, methodBytes[0], methodBytes[1]);

            Console.WriteLine($"About to sign {pallet}.{call}");

            var account = await Model.KeysModel.GetAccountAsync($"Sign & submit {pallet}.{call} extrinsic");

            Console.WriteLine($"Account got");

            if (account is null)
            {
                throw new InvalidOperationException("Failed to retrieve account for signing.");
            }

            Console.WriteLine($"Was not null");

            SignatureTask = new TaskCompletionSource<byte[]>();

            var (unCheckedExtrinsic, runtime) = ToUnCheckedExtrinsic(payload, account);

            Console.WriteLine($"ToUnChecked");


            var substratePayload = unCheckedExtrinsic.GetPayload(runtime);

            Console.WriteLine($"About to sign");

            byte[] signature = account.Sign(substratePayload.Encode());

            Console.WriteLine("Account: " + account.Value);
            Console.WriteLine("Payload: " + Utils.Bytes2HexString(substratePayload.Encode()).ToLowerInvariant());
            Console.WriteLine("Signature: " + Utils.Bytes2HexString(signature).ToLowerInvariant());
            Console.WriteLine(account.Verify(signature, substratePayload.Encode()));


            Console.WriteLine($"Now signed");


            SignatureTask.SetResult(signature);

            var multiSignature = ToMultiSignatureBytes(account, signature);

            return new SignerResultPayload
            {
                Id = payload.Id ?? request.Id ?? Guid.NewGuid().ToString("N"),
                Signature = Utils.Bytes2HexString(multiSignature).ToLowerInvariant()
            };
        }
        catch (Exception ex)
        {
            var messagePopup = DependencyService.Get<MessagePopupViewModel>();
            messagePopup.Title = "ConnectionRequestView Error";
            messagePopup.Text = ex.Message;
            messagePopup.IsVisible = true;
            throw;
        }
    }

    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        try
        {
            bytes = Utils.HexToByteArray(hex);
            return true;
        }
        catch
        {
            bytes = [];
            return false;
        }
    }

    private static byte[] ToMultiSignatureBytes(Substrate.NetApi.Model.Types.Account account, byte[] signature)
    {
        byte signatureType = account.KeyType switch
        {
            KeyType.Ed25519 => 0x00,
            KeyType.Sr25519 => 0x01,
            _ => throw new InvalidOperationException($"Unsupported account key type '{account.KeyType}'.")
        };

        var multiSignature = new byte[signature.Length + 1];
        multiSignature[0] = signatureType;
        Buffer.BlockCopy(signature, 0, multiSignature, 1, signature.Length);

        return multiSignature;
    }

    private static (UnCheckedExtrinsic, RuntimeVersion) ToUnCheckedExtrinsic(SignerPayloadJson payload, Substrate.NetApi.Model.Types.Account account)
    {
        if (payload.Tip is null || payload.SpecVersion is null ||
                    payload.TransactionVersion is null || payload.Nonce is null)
        {
            throw new WrongMessageReceivedException();
        }

        byte[] methodBytes = Utils.HexToByteArray(payload.Method);

        List<byte> methodParameters = new List<byte>();

        for (int i = 2; i < methodBytes.Length; i++)
        {
            methodParameters.Add(methodBytes[i]);
        }

        Method method = new Method(methodBytes[0], methodBytes[1], methodParameters.ToArray());

        Hash eraHash = new Hash();
        eraHash.Create(Utils.HexToByteArray(payload.Era));

        Hash blockHash = new Hash();
        blockHash.Create(payload.BlockHash);

        Hash genesisHash = new Hash();
        genesisHash.Create(Utils.HexToByteArray(payload.GenesisHash));

        RuntimeVersion runtime = new RuntimeVersion
        {
            ImplVersion = payload.Version,
            SpecVersion = HexStringToUint(payload.SpecVersion),
            TransactionVersion = HexStringToUint(payload.TransactionVersion),
        };

        ChargeType charge;

        if (payload.Tip.Length == 34)
        {
            charge = new ChargeTransactionPayment(HexStringToUint(payload.Tip));
        }
        else
        {
            int _p = 0;

            charge = new ChargeAssetTxPayment(0, new());
            charge.Decode(Utils.HexToByteArray(payload.Tip), ref _p);
        }

        return (
            new UnCheckedExtrinsic(true, account, method, Era.Decode(Utils.HexToByteArray(payload.Era)),
                HexStringToUint(payload.Nonce), charge, genesisHash, blockHash),
            runtime
        );
    }

    private static string SerializeResponse(WalletBridgeResponse response)
        => System.Text.Json.JsonSerializer.Serialize(response, SerializerOptions);

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

    private sealed record SignRawPayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private sealed class SignerPayloadJson
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("blockHash")]
        public string BlockHash { get; set; } = string.Empty;

        [JsonPropertyName("blockNumber")]
        public string BlockNumber { get; set; } = string.Empty;

        [JsonPropertyName("era")]
        public string Era { get; set; } = string.Empty;

        [JsonPropertyName("genesisHash")]
        public string GenesisHash { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonPropertyName("specVersion")]
        public string SpecVersion { get; set; } = string.Empty;

        [JsonPropertyName("tip")]
        public string Tip { get; set; } = string.Empty;

        [JsonPropertyName("transactionVersion")]
        public string TransactionVersion { get; set; } = string.Empty;

        [JsonPropertyName("signedExtensions")]
        public string[] SignedExtensions { get; set; } = Array.Empty<string>();

        // Optional: a pre-encoded SCALE payload (hex) that we will sign.
        // Your JS shim should populate this.
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        // Optional: if your JS assigns a per-request id here
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("version")]
        public uint Version { get; set; }
    }


    private sealed class SignerResultPayload
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    private sealed class InjectedAccount
    {
        [JsonPropertyName("address")]
        public required string Address { get; set; }

        [JsonPropertyName("meta")]
        public required InjectedAccountMeta Meta { get; set; }
    }

    private sealed class InjectedAccountMeta
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("source")]
        public required string Source { get; set; }
    }

    /// <summary>
    /// Helper method that translates hex string to uint
    /// </summary>
    /// <param name="hex"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    private static uint HexStringToUint(string hex)
    {
        hex = hex.Replace("0x", ""); // remove the 0x if it's there
        if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint result))
        {
            return result;
        }
        else
        {
            throw new FormatException("The provided string is not a valid hexadecimal number");
        }
    }
}
