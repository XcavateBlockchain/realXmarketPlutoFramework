using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;

namespace PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

public record WalletLinkData
{
    [JsonPropertyName("nonce")]
    public required string Nonce { get; set; }

    [JsonPropertyName("chain")]
    public required string Chain { get; set; }

    [JsonPropertyName("address")]
    public required string Address { get; set; }

    /// <summary>
    /// Base58-encoded 64-byte Ed25519 signature over <see cref="Core.Utils.WalletLinkMessage"/>.
    /// Required for Solana; Polkadot links are recorded without ownership proof until the
    /// server implements sr25519 verification, so the field is omitted entirely when null.
    /// </summary>
    [JsonPropertyName("signature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Signature { get; set; }
}

public abstract class WalletLinkEndpoint : IApiEndpoint
{
    public static string EndpointPath => "/api/user/wallet-link/";

    public static async Task LinkAsync(HttpClient httpClient, WalletLinkData input)
    {
        StringContent jsonContent = new(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");

        var res = await httpClient.PostAsync(EndpointPath, jsonContent);
        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException(res.ReasonPhrase ?? "");
        }
        res.EnsureSuccessStatusCode();
    }
}
