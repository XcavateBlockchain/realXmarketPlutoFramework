using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;

namespace PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

/// <summary>
/// The API accepted the token but has no device row behind it - the device was removed
/// server-side, so the stored JWT pair is worthless and only a fresh registration brings
/// notifications back. Distinct from <see cref="UnauthorizedException"/>, which a token
/// refresh can still fix.
/// </summary>
public class DeviceNotFoundException(string message) : HttpRequestException(message);

/// <summary>
/// A wallet address the server has on file for this device.
/// </summary>
public record RegistrationWallet
{
    [JsonPropertyName("chain")]
    public string Chain { get; init; } = "";

    [JsonPropertyName("address")]
    public string Address { get; init; } = "";

    /// <summary>
    /// Whether the link carried a valid ownership signature. Polkadot links are recorded
    /// unverified until the server implements sr25519 verification.
    /// </summary>
    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    [JsonPropertyName("linked_at")]
    public DateTimeOffset? LinkedAt { get; init; }
}

/// <summary>
/// What the notifications API believes about this device.
/// </summary>
/// <remarks>
/// Every field is optional on the client side deliberately: this is read to diagnose a
/// device whose state is already suspect, so a response missing a field should still be
/// shown rather than thrown away.
/// </remarks>
public record RegistrationData
{
    /// <summary>
    /// Echoed from the JWT claim, so a client can spot a token issued for a device it is
    /// no longer.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    /// <summary>Either <c>android</c> or <c>ios</c>.</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>
    /// The legacy generic identifier (<c>/api/user/uid-update/</c>); null if never set.
    /// This app no longer sets it - registered wallet addresses are the main keys - but
    /// installs that predate that change may still carry the Polkadot address an earlier
    /// version stored here.
    /// </summary>
    [JsonPropertyName("uid")]
    public string? Uid { get; init; }

    /// <summary>
    /// Whether an FCM token is on file - that is, whether delivery would be attempted
    /// at all.
    /// </summary>
    [JsonPropertyName("notifications_enabled")]
    public bool NotificationsEnabled { get; init; }

    [JsonPropertyName("wallets")]
    public IReadOnlyList<RegistrationWallet> Wallets { get; init; } = [];
}

public abstract class RegistrationEndpoint : IApiEndpoint
{
    public static string EndpointPath => "/api/user/registration/";

    public static async Task<RegistrationData> GetAsync(HttpClient httpClient)
    {
        var res = await httpClient.GetAsync(EndpointPath);

        // 403 counts as unauthorized here - the server answers a malformed or claimless
        // token either way, and both are worth one refresh before giving up.
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedException(res.ReasonPhrase ?? "");
        }

        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DeviceNotFoundException(res.ReasonPhrase ?? "");
        }

        res.EnsureSuccessStatusCode();

        return await res.Content.ReadFromJsonAsync<RegistrationData>()
            ?? throw new HttpRequestException("Empty registration response.");
    }
}
