using PlutoFramework.Model.DeviceSecureStorage;
using PlutoFrameworkCore.PushNotificationServices.Api;
using PlutoFrameworkCore.PushNotificationServices.Core;
using PlutoFrameworkCore.PushNotificationServices.Core.Misc;
using PlutoFrameworkCore.PushNotificationServices.Core.Utils;
# if ANDROID
using Firebase;
using PlutoFramework.Platforms.Android;
using PlutoFramework.Platforms.Android.Attestation;
# elif IOS
using PlutoFrameworkCore.PushNotificationServices.Platforms.iOS;
# endif

namespace PlutoFramework.Model.Initializers;

using NotificationsPlatform = PlutoFrameworkCore.PushNotificationServices.Core.Misc.Platform;

public static class PushNotificationsAppInitializer
{
    public static void Initialize(string apiUrl)
    {
        _ = InitializeAsync(apiUrl);
    }

    private static async Task InitializeAsync(string apiUrl)
    {
        Console.WriteLine($"[PlutoNotifications] Trying to start notification services ...");
        ApiClient.SetBaseUrl(apiUrl);
        Console.WriteLine($"[PlutoNotifications] API URL set: {apiUrl}");

        SecureStorageManager.Storage = new PushNotificationsSecureStorageService();
        await SecureStorageManager.Storage.EnsurePerInstallIsolationAsync();

        Console.WriteLine($"[PlutoNotifications] Trying to request notification permission ...");
#if ANDROID
        try
        {
            await Permissions.RequestAsync<NotificationPermission>();
        }
        catch (PermissionException e)
        {
            Console.WriteLine($"[PlutoNotifications] Permission exception: {e.Message}");
        }

        NotificationsPlatform.Current = PlatformType.Android;
        NotificationsPlatform.AttestationService = new PlayIntegrityService(SecureStorageManager.Storage);
#elif IOS
        Firebase.Core.App.Configure();
        await Platforms.iOS.NotificationPermission.RequestAsync();

        NotificationsPlatform.Current = PlatformType.iOS;
        NotificationsPlatform.AttestationService = new AppAttestService(SecureStorageManager.Storage);
#endif
        Console.WriteLine($"[PlutoNotifications] Platform type set: {NotificationsPlatform.Current.ToStringValue()}");

        await SyncAsync();

        Console.WriteLine($"[PlutoNotifications] Background jobs processed.");
    }

    /// <summary>
    /// Brings the notifications API up to date with this device: registration, FCM token
    /// and user id. Safe to call repeatedly - each step skips itself when local state
    /// says it is already done.
    /// </summary>
    /// <param name="force">
    /// Redo every step regardless of local state. For when the device looks registered but
    /// the server disagrees, which is exactly what those cached flags cannot tell you.
    /// </param>
    /// <remarks>
    /// Requires <see cref="Initialize"/> to have run first - it sets the API base URL, the
    /// secure storage and the platform attestation service this depends on.
    /// </remarks>
    public static async Task SyncAsync(bool force = false)
    {
        var isRegistered = await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false;

        if ((isRegistered && !force) || await DeviceRegisterService.RegisterDeviceAsync())
        {
            // UpdateFcmTokenAsync skips a token it believes is current, so a forced sync
            // has to mark it stale first or the push it exists to make never happens.
            if (force)
                await SecureStorageManager.Storage.SaveFcmTokenExpiredAsync(true);

            await DeviceRegisterService.UpdateFcmTokenAsync();
        }

        var hasAddress = KeysModel.HasSubstrateKey();
        var isUserIdUpdated = await SecureStorageManager.Storage.GetIsUserIdUpdatedAsync() ?? false;
        if ((force || !isUserIdUpdated) && hasAddress)
            await DeviceRegisterService.UpdateUserIdAsync(KeysModel.GetSubstrateKey());

        // No wallet link here. Only Solana wallets register for notifications, and a
        // Solana link must be signed - neither prompting to unlock a mnemonic key nor
        // launching an external wallet belongs in a background sync - so Solana links
        // happen at account creation/connect and ride on later unlocks and wallet
        // sessions (PlutoFrameworkSolanaAccount, MwaSolanaAccount), or on demand through
        // WalletLinkModel.RelinkSolanaAsync. Polkadot wallets are not registered at all:
        // the server would record their links without ownership proof (sr25519
        // verification is not implemented yet).
    }
}