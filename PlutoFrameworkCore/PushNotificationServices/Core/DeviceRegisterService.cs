using PlutoFrameworkCore.PushNotificationServices.Api;
using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;
using PlutoFrameworkCore.PushNotificationServices.Core.Utils;

namespace PlutoFrameworkCore.PushNotificationServices.Core;

public static class DeviceRegisterService
{
    private static readonly SemaphoreSlim _updateLock = new(1, 1);
    
    public static async Task<bool> RegisterDeviceAsync()
    {
        Console.WriteLine("[PlutoNotifications] Trying to register device...");
        try
        {
            await RetryHelper.RunWithRetryAsync(ApiClient.RegisterDeviceRequestAsync);
        }
        catch
        {
            Console.WriteLine("[PlutoNotifications] Device registration failed.");
            return false;
        }

        await SecureStorageManager.Storage.SaveIsRegisteredAsync(true);
        Console.WriteLine("[PlutoNotifications] Device has been registered.");
        return true;
    }

    public static async Task<bool> UpdateFcmTokenAsync()
    {
        await _updateLock.WaitAsync();

        try
        {
            if (!(await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false))
            {
                Console.WriteLine("[PlutoNotifications] Device is not registered, cannot update FCM token.");
                return false;
            }

            if (!(await SecureStorageManager.Storage.GetFcmTokenExpiredAsync() ?? true))
            {
                Console.WriteLine("[PlutoNotifications] FCM token is up-to-date, skipping.");
                return true;
            }

            Console.WriteLine("[PlutoNotifications] Trying to update FCM token...");
            await RetryHelper.RunWithRetryAsync(async () =>
                await ApiClient.UpdateFcmTokenRequestAsync(
                    (await FcmTokenService.GetTokenAsync())!
                )
            );

            await SecureStorageManager.Storage.SaveFcmTokenExpiredAsync(false);
            Console.WriteLine("[PlutoNotifications] Token has been updated.");
            return true;
        }
        catch
        {
            Console.WriteLine("[PlutoNotifications] Token update failed.");
            return false;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Links a wallet address to this device on the notifications API, so notifications
    /// targeted at that chain + address reach this device.
    /// </summary>
    /// <param name="signMessageAsync">
    /// Canonical link message → base58 signature. Required for Solana, null for Polkadot.
    /// </param>
    /// <remarks>
    /// This app holds one account slot per chain, so linking an address first unlinks any
    /// different address previously linked on the same chain - otherwise a replaced account
    /// would keep receiving that wallet's notifications on this device forever.
    /// </remarks>
    public static async Task<bool> LinkWalletAsync(
        string chain,
        string address,
        Func<string, Task<string>>? signMessageAsync = null)
    {
        await _updateLock.WaitAsync();

        try
        {
            if (!(await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false))
            {
                Console.WriteLine("[PlutoNotifications] Device is not registered, cannot link wallet.");
                return false;
            }

            var linked = await SecureStorageManager.Storage.GetLinkedWalletsAsync();

            if (linked.Any(w => w.Chain == chain && w.Address == address))
            {
                Console.WriteLine($"[PlutoNotifications] {chain} wallet is already linked, skipping.");
                return true;
            }

            foreach (var stale in linked.Where(w => w.Chain == chain))
            {
                Console.WriteLine($"[PlutoNotifications] Unlinking replaced {chain} wallet...");
                await RetryHelper.RunWithRetryAsync(async () =>
                    await ApiClient.UnlinkWalletRequestAsync(stale.Chain, stale.Address)
                );
            }

            Console.WriteLine($"[PlutoNotifications] Trying to link {chain} wallet...");
            await RetryHelper.RunWithRetryAsync(async () =>
                await ApiClient.LinkWalletRequestAsync(chain, address, signMessageAsync)
            );

            await SecureStorageManager.Storage.SaveLinkedWalletsAsync([
                .. linked.Where(w => w.Chain != chain),
                new LinkedWallet(chain, address)
            ]);

            Console.WriteLine($"[PlutoNotifications] {chain} wallet has been linked.");
            return true;
        }
        catch
        {
            Console.WriteLine($"[PlutoNotifications] {chain} wallet link failed.");
            return false;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Unlinks every wallet this device previously linked. Used when the user logs out or
    /// clears their accounts, so the device stops receiving wallet-targeted notifications.
    /// </summary>
    public static async Task<bool> UnlinkAllWalletsAsync()
    {
        await _updateLock.WaitAsync();

        try
        {
            if (!(await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false))
            {
                Console.WriteLine("[PlutoNotifications] Device is not registered, cannot unlink wallets.");
                return false;
            }

            var linked = await SecureStorageManager.Storage.GetLinkedWalletsAsync();

            foreach (var wallet in linked)
            {
                Console.WriteLine($"[PlutoNotifications] Trying to unlink {wallet.Chain} wallet...");
                await RetryHelper.RunWithRetryAsync(async () =>
                    await ApiClient.UnlinkWalletRequestAsync(wallet.Chain, wallet.Address)
                );
            }

            await SecureStorageManager.Storage.SaveLinkedWalletsAsync([]);

            Console.WriteLine("[PlutoNotifications] All wallets have been unlinked.");
            return true;
        }
        catch
        {
            Console.WriteLine("[PlutoNotifications] Wallet unlink failed.");
            return false;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Whether the given address is already linked on the given chain. A cheap storage
    /// read, so callers can decide against an expensive signing round trip early.
    /// </summary>
    public static async Task<bool> IsWalletLinkedAsync(string chain, string address)
    {
        var linked = await SecureStorageManager.Storage.GetLinkedWalletsAsync();

        return linked.Any(w => w.Chain == chain && w.Address == address);
    }

    public static async Task<bool> UpdateUserIdAsync(string newUserId)
    {
        await _updateLock.WaitAsync();

        try
        {
            if (!(await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false))
            {
                Console.WriteLine("[PlutoNotifications] Device is not registered, cannot update user ID.");
                return false;
            }

            await SecureStorageManager.Storage.SaveIsUserIdUpdatedAsync(false);

            Console.WriteLine("[PlutoNotifications] Trying to update user ID...");
            await RetryHelper.RunWithRetryAsync(async () =>
                await ApiClient.UpdateUserIdRequestAsync(newUserId)
            );

            await SecureStorageManager.Storage.SaveIsUserIdUpdatedAsync(true);
            Console.WriteLine("[PlutoNotifications] User ID has been updated.");
            return true;
        }
        catch
        {
            Console.WriteLine("[PlutoNotifications] User ID update failed.");
            return false;
        }
        finally
        {
            _updateLock.Release();
        }
    }
}