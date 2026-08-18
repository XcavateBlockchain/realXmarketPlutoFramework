using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

namespace PlutoFrameworkCore.PushNotificationServices.Core.Misc;

public enum RegistrationCheckOutcome
{
    /// <summary>The API confirmed this device. <see cref="RegistrationCheck.Data"/> is set.</summary>
    Registered,

    /// <summary>
    /// This device never completed registration, so there is nothing to ask the API about.
    /// </summary>
    NotRegisteredLocally,

    /// <summary>
    /// The notification services never started - typically no API URL configured - so the
    /// device could not have registered in the first place.
    /// </summary>
    ServicesNotStarted,

    /// <summary>
    /// The token was accepted but its device row is gone server-side. Only a fresh
    /// registration fixes this.
    /// </summary>
    DeviceUnknownToServer,

    /// <summary>
    /// The stored credentials were rejected and could not be renewed.
    /// </summary>
    Unauthorized,

    /// <summary>The request never got an answer - offline, or the API is down.</summary>
    Failed,
}

/// <summary>
/// The result of asking the notifications API what it knows about this device. Failures
/// are values rather than exceptions because every one of them is something a user-facing
/// diagnostic wants to report verbatim.
/// </summary>
public record RegistrationCheck(
    RegistrationCheckOutcome Outcome,
    RegistrationData? Data = null,
    string? Detail = null);
