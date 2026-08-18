using PlutoFrameworkCore.PushNotificationServices.Api;
using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;
using PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;
using PlutoFrameworkCore.PushNotificationServices.Core.Misc;
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
            // A null token means Firebase itself is unavailable - posting it anyway
            // would just make the server reject a token that was never fetched.
            await RetryHelper.RunWithRetryAsync(async () =>
                await ApiClient.UpdateFcmTokenRequestAsync(
                    await FcmTokenService.GetTokenAsync()
                        ?? throw new InvalidOperationException("No FCM token available.")
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
    /// Registers a wallet address as a main key of this device on the notifications API:
    /// notifications addressed to the bare address (<c>user_id</c> targeting, no chain
    /// qualifier) or scoped to chain + address both reach this device. Chains are recorded
    /// independently, so a device registered for Polkadot and Solana holds both keys side
    /// by side.
    /// </summary>
    /// <param name="signMessageAsync">
    /// Canonical link message → base58 signature. Required for Solana, null for Polkadot.
    /// </param>
    /// <param name="force">
    /// Link even when this address is already recorded as linked. The cached record is an
    /// optimisation, not a source of truth - if the server has since lost the link, only a
    /// forced call can put it back.
    /// </param>
    /// <remarks>
    /// This app holds one account slot per chain, so linking an address first unlinks any
    /// different address previously linked on the same chain - otherwise a replaced account
    /// would keep receiving that wallet's notifications on this device forever.
    /// </remarks>
    public static async Task<bool> LinkWalletAsync(
        string chain,
        string address,
        Func<string, Task<string>>? signMessageAsync = null,
        bool force = false)
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

            if (!force && linked.Any(w => w.Chain == chain && w.Address == address))
            {
                Console.WriteLine($"[PlutoNotifications] {chain} wallet is already linked, skipping.");
                return true;
            }

            // Unlinking the address being relinked would undo the link this call is about
            // to make, so a forced relink only clears genuinely different addresses.
            foreach (var stale in linked.Where(w => w.Chain == chain && w.Address != address))
            {
                Console.WriteLine($"[PlutoNotifications] Unlinking replaced {chain} wallet...");
                await RetryHelper.RunWithRetryAsync(async () =>
                    await ApiClient.UnlinkWalletRequestAsync(stale.Chain, stale.Address)
                );
            }

            Console.WriteLine($"[PlutoNotifications] Trying to link {chain} wallet...");
            // A cancellation is the user declining the signature, not a flaky network -
            // retrying would re-prompt (and under MWA, relaunch the wallet app).
            await RetryHelper.RunWithRetryAsync(
                async () => await ApiClient.LinkWalletRequestAsync(chain, address, signMessageAsync),
                isTransient: ex => ex is not OperationCanceledException
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

    /// <summary>
    /// Asks the API what it holds for this device, so a user can see whether notifications
    /// would actually reach them rather than infer it from cached local flags.
    /// </summary>
    /// <remarks>
    /// Never throws: every failure is an outcome the caller is expected to display.
    /// </remarks>
    public static async Task<RegistrationCheck> CheckRegistrationAsync()
    {
        if (!SecureStorageManager.IsInitialized)
        {
            Console.WriteLine("[PlutoNotifications] Notification services never started.");
            return new RegistrationCheck(RegistrationCheckOutcome.ServicesNotStarted);
        }

        if (!(await SecureStorageManager.Storage.GetIsRegisteredAsync() ?? false))
        {
            Console.WriteLine("[PlutoNotifications] Device is not registered, nothing to check.");
            return new RegistrationCheck(RegistrationCheckOutcome.NotRegisteredLocally);
        }

        try
        {
            var data = await ApiClient.GetRegistrationRequestAsync();

            Console.WriteLine("[PlutoNotifications] Registration checked.");
            return new RegistrationCheck(RegistrationCheckOutcome.Registered, data);
        }
        catch (DeviceNotFoundException)
        {
            // The token outlived its device row. Nothing this device sends will land until
            // it registers again, so the local flag is corrected here rather than left
            // claiming a registration the server has forgotten.
            Console.WriteLine("[PlutoNotifications] Server does not know this device.");
            await SecureStorageManager.Storage.SaveIsRegisteredAsync(false);

            return new RegistrationCheck(RegistrationCheckOutcome.DeviceUnknownToServer);
        }
        catch (UnauthorizedException e)
        {
            Console.WriteLine("[PlutoNotifications] Registration check was not authorized.");
            return new RegistrationCheck(RegistrationCheckOutcome.Unauthorized, Detail: e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[PlutoNotifications] Registration check failed: {e.Message}");
            return new RegistrationCheck(RegistrationCheckOutcome.Failed, Detail: e.Message);
        }
    }
}