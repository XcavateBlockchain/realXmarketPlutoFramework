using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

namespace PlutoFrameworkCore.PushNotificationServices.Core.Interfaces;

/// <summary>
/// A wallet address this device has successfully linked on the notifications API,
/// so link attempts can be skipped rather than repeated on every start.
/// </summary>
public record LinkedWallet(string Chain, string Address);

public interface IPushNotificationsSecureStorage
{
    public Task EnsurePerInstallIsolationAsync();
    public Task SaveDeviceIdAsync(string uuid);
    public Task<string?> GetDeviceIdAsync();
    public Task SaveAuthTokenPairAsync(TokenPair tokenPair);
    public Task<TokenPair?> GetAuthTokenPairAsync();
    public Task SaveIsRegisteredAsync(bool registered);
    public Task<bool?> GetIsRegisteredAsync();
    public Task SaveFcmTokenExpiredAsync(bool expired);
    public Task<bool?> GetFcmTokenExpiredAsync();
    public Task SaveIsUserIdUpdatedAsync(bool isUpdated);
    public Task<bool?> GetIsUserIdUpdatedAsync();
    public Task SaveLinkedWalletsAsync(IReadOnlyList<LinkedWallet> wallets);
    public Task<IReadOnlyList<LinkedWallet>> GetLinkedWalletsAsync();
}