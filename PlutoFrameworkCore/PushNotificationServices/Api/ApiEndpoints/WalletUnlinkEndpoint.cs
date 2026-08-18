using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;

namespace PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

public record WalletUnlinkData
{
    [JsonPropertyName("chain")]
    public required string Chain { get; set; }

    [JsonPropertyName("address")]
    public required string Address { get; set; }
}

public abstract class WalletUnlinkEndpoint : IApiEndpoint
{
    public static string EndpointPath => "/api/user/wallet-unlink/";

    public static async Task UnlinkAsync(HttpClient httpClient, WalletUnlinkData input)
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
