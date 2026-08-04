using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;

namespace PlutoFrameworkCore.PushNotificationServices.Core.Utils;

public static class SecureStorageManager
{
    private static IPushNotificationsSecureStorage? _storage { get; set; }

    /// <summary>
    /// Whether the notification services ever started. Reading <see cref="Storage"/> before
    /// they do throws, so anything that runs regardless of them - a diagnostic, say - has to
    /// ask first.
    /// </summary>
    public static bool IsInitialized => _storage is not null;

    public static IPushNotificationsSecureStorage Storage
    {
        get => _storage ?? throw new InvalidOperationException(
            "SecureStorage has not been initialized. Call SecureStorageManager.Storage = ... first.");
        set => _storage = value;
    }
}