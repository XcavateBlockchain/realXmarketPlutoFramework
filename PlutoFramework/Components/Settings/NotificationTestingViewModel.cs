using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Initializers;
using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;
using PlutoFrameworkCore.PushNotificationServices.Core;
using PlutoFrameworkCore.PushNotificationServices.Core.Misc;
using PlutoFrameworkCore.PushNotificationServices.Core.Utils;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Settings;

/// <summary>One labelled fact about the notification setup.</summary>
public record DiagnosticRow(string Label, string Value);

/// <summary>
/// Drives the notification testing page: reports whether this device would actually
/// receive push notifications, and offers the two repairs that can be triggered by hand.
/// </summary>
/// <remarks>
/// The point of this page is that local state can disagree with the server - a device can
/// hold a registration flag for a row the server has since dropped - so the two are shown
/// side by side rather than merged into one verdict.
/// </remarks>
public partial class NotificationTestingViewModel : ObservableObject
{
    /// <summary>The one-line verdict, e.g. "Ready to receive notifications".</summary>
    [ObservableProperty]
    private string summary = "Checking ...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummaryDetail))]
    private string summaryDetail = "";

    public bool HasSummaryDetail => !string.IsNullOrEmpty(SummaryDetail);

    /// <summary>
    /// Whether <see cref="Summary"/> reports a problem, so the page can colour it.
    /// </summary>
    [ObservableProperty]
    private bool hasProblem;

    /// <summary>
    /// True while a check, re-registration or relink is running. Every button binds its
    /// enabled state to the inverse, so a slow attestation cannot be started twice.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isBusy;

    public bool IsIdle => !IsBusy;

    /// <summary>Whether the server answered, so its section is worth showing at all.</summary>
    [ObservableProperty]
    private bool hasServerData;

    /// <summary>What this device believes, read from its own secure storage.</summary>
    public ObservableCollection<DiagnosticRow> DeviceRows { get; } = [];

    /// <summary>What the API reports for this device.</summary>
    public ObservableCollection<DiagnosticRow> ServerRows { get; } = [];

    /// <summary>The wallets the API has linked to this device.</summary>
    public ObservableCollection<DiagnosticRow> ServerWallets { get; } = [];

    [ObservableProperty]
    private bool hasServerWallets;

    /// <summary>
    /// What the last button press did. Kept apart from <see cref="Summary"/> so the verdict
    /// stays a verdict rather than turning into a log line.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastAction))]
    private string lastAction = "";

    public bool HasLastAction => !string.IsNullOrEmpty(LastAction);

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            Summary = "Checking ...";
            SummaryDetail = "";

            await LoadDeviceRowsAsync();

            var check = await DeviceRegisterService.CheckRegistrationAsync();

            ApplyOutcome(check);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Re-runs the whole registration sequence - attestation, FCM token, user id - and
    /// then reports the result. Deliberately excludes the Solana wallet link, which needs
    /// a signature and so gets its own button. Polkadot is never linked (see
    /// <see cref="WalletLinkModel"/>).
    /// </summary>
    [RelayCommand]
    private async Task ReRegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!SecureStorageManager.IsInitialized)
        {
            LastAction = "Notification services are not running, so there is nothing to register.";
            return;
        }

        IsBusy = true;

        try
        {
            LastAction = "Registering ...";
            Summary = "Registering ...";
            SummaryDetail = "";

            await PushNotificationsAppInitializer.SyncAsync(force: true);

            await LoadDeviceRowsAsync();

            var check = await DeviceRegisterService.CheckRegistrationAsync();

            ApplyOutcome(check);

            LastAction = check.Outcome == RegistrationCheckOutcome.Registered
                ? "Registered."
                : "Registration did not complete. See above.";
        }
        catch (Exception e)
        {
            LastAction = $"Registration failed: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Relinks the Solana address. Separate from re-registration because it prompts for a
    /// signature, and under Mobile Wallet Adapter leaves the app for the wallet.
    /// </summary>
    [RelayCommand]
    private async Task RelinkSolanaAsync()
    {
        if (IsBusy)
        {
            return;
        }

        // The database rather than the stored public key, so this agrees with what
        // PlutoFrameworkSolanaAccount.ResolveAsync will look for a moment later.
        if (!await KeysModel.HasSolanaKeyAsync())
        {
            LastAction = "No Solana account on this device.";
            return;
        }

        IsBusy = true;

        try
        {
            LastAction = "Linking Solana wallet ...";

            var linked = await WalletLinkModel.RelinkSolanaAsync();

            LastAction = linked
                ? "Solana wallet linked."
                : "Solana wallet link failed or was declined.";

            await LoadDeviceRowsAsync();

            ApplyOutcome(await DeviceRegisterService.CheckRegistrationAsync());
        }
        catch (Exception e)
        {
            LastAction = $"Solana wallet link failed: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDeviceRowsAsync()
    {
        DeviceRows.Clear();

        if (!SecureStorageManager.IsInitialized)
        {
            DeviceRows.Add(new DiagnosticRow("Services", "Not started"));
            return;
        }

        var storage = SecureStorageManager.Storage;

        var isRegistered = await storage.GetIsRegisteredAsync() ?? false;
        var deviceId = await storage.GetDeviceIdAsync();
        var fcmTokenExpired = await storage.GetFcmTokenExpiredAsync();
        var isUserIdUpdated = await storage.GetIsUserIdUpdatedAsync() ?? false;
        var linkedWallets = await storage.GetLinkedWalletsAsync();

        DeviceRows.Add(new DiagnosticRow("Registered", isRegistered ? "Yes" : "No"));
        DeviceRows.Add(new DiagnosticRow("Device ID", string.IsNullOrEmpty(deviceId) ? "None" : deviceId));
        DeviceRows.Add(new DiagnosticRow("FCM token", fcmTokenExpired switch
        {
            null => "Never sent",
            true => "Needs sending",
            false => "Sent",
        }));
        DeviceRows.Add(new DiagnosticRow("User ID sent", isUserIdUpdated ? "Yes" : "No"));
        DeviceRows.Add(new DiagnosticRow(
            "Linked wallets",
            linkedWallets.Count == 0
                ? "None"
                : string.Join("\n", linkedWallets.Select(w => $"{w.Chain}: {Shorten(w.Address)}"))));
    }

    private void ApplyOutcome(RegistrationCheck check)
    {
        ServerRows.Clear();
        ServerWallets.Clear();
        HasServerData = false;
        HasServerWallets = false;

        switch (check.Outcome)
        {
            case RegistrationCheckOutcome.Registered:
                ApplyRegistered(check);
                return;

            case RegistrationCheckOutcome.ServicesNotStarted:
                SetProblem(
                    "Notifications are off",
                    "The notification services never started on this device. The app was most likely built without a notifications API URL.");
                return;

            case RegistrationCheckOutcome.NotRegisteredLocally:
                SetProblem(
                    "This device is not registered",
                    "It has never completed registration with the notifications API. Register it below.");
                return;

            case RegistrationCheckOutcome.DeviceUnknownToServer:
                SetProblem(
                    "The server does not know this device",
                    "Its registration was removed server-side, so nothing will be delivered until it registers again.");
                return;

            case RegistrationCheckOutcome.Unauthorized:
                SetProblem(
                    "This device is not authorized",
                    $"The notifications API rejected its credentials and they could not be renewed. {check.Detail}".Trim());
                return;

            default:
                SetProblem(
                    "Could not reach the notifications API",
                    $"The check got no answer, so the state above is only what this device believes. {check.Detail}".Trim());
                return;
        }
    }

    private void ApplyRegistered(RegistrationCheck check)
    {
        // A Registered outcome always carries a payload; the fallback is here only because
        // the record cannot say so, and an empty one reads better than a crash.
        var data = check.Data ?? new RegistrationData();

        HasServerData = true;

        ServerRows.Add(new DiagnosticRow("Device ID", data.DeviceId ?? "Unknown"));
        ServerRows.Add(new DiagnosticRow("Platform", data.Platform ?? "Unknown"));
        ServerRows.Add(new DiagnosticRow("User ID", string.IsNullOrEmpty(data.Uid) ? "Not set" : data.Uid));
        ServerRows.Add(new DiagnosticRow(
            "Delivery",
            data.NotificationsEnabled ? "Enabled" : "No FCM token on file"));

        foreach (var wallet in data.Wallets)
        {
            ServerWallets.Add(new DiagnosticRow(
                wallet.Chain,
                $"{Shorten(wallet.Address)}\n{(wallet.Verified ? "Verified" : "Unverified")}"
                    + (wallet.LinkedAt is null ? "" : $" · linked {wallet.LinkedAt:yyyy-MM-dd HH:mm}")));
        }

        HasServerWallets = ServerWallets.Count > 0;

        // The device id is echoed from the JWT claim, so a mismatch means this device is
        // holding a token minted for a different device - it would read another device's
        // state and push its own state onto that row.
        var localDeviceId = DeviceRows
            .FirstOrDefault(row => row.Label == "Device ID")?.Value;

        if (data.DeviceId is not null && localDeviceId is not null && data.DeviceId != localDeviceId)
        {
            SetProblem(
                "This device is registered as another device",
                $"The API answers for device {data.DeviceId}, but this one is {localDeviceId}. Register it again.");
            return;
        }

        if (!data.NotificationsEnabled)
        {
            SetProblem(
                "Registered, but nothing can be delivered",
                "The API has no FCM token for this device, so notifications have nowhere to go. Register it again.");
            return;
        }

        if (ServerWallets.Count == 0)
        {
            SetProblem(
                "Registered, but no wallet is linked",
                "General notifications will arrive; anything addressed to your wallet will not.");
            return;
        }

        HasProblem = false;
        Summary = "Ready to receive notifications";
        SummaryDetail = ServerWallets.Count == 1
            ? "This device is registered and one wallet is linked to it."
            : $"This device is registered and {ServerWallets.Count} wallets are linked to it.";
    }

    private void SetProblem(string summary, string detail)
    {
        HasProblem = true;
        Summary = summary;
        SummaryDetail = detail;
    }

    /// <summary>
    /// Addresses are long enough to wrap over several lines on a phone, which buries
    /// everything under them. Both ends are kept because that is what a reader compares.
    /// </summary>
    private static string Shorten(string address) =>
        address.Length <= 16 ? address : $"{address[..8]}...{address[^8..]}";
}
