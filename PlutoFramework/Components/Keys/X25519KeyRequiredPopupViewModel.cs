using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Components.Keys;

/// <summary>
/// The gate popup raised by <see cref="Components.Messages.MessengerAccessModel"/> when an
/// account holds a wallet but no X25519 encryption key - the key the messenger dashboard
/// decrypts with - so the page refuses to open until one is created or imported.
/// </summary>
public partial class X25519KeyRequiredPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    [ObservableProperty]
    private bool isVisible = false;

    public void SetToDefault()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void Cancel() => SetToDefault();

    /// <summary>
    /// Creates the key the way the Keys page's Add button does for a missing key. Its
    /// replace warning stays out of the way: this popup is only raised when no key exists.
    /// </summary>
    [RelayCommand]
    public async Task CreateKeyAsync()
    {
        SetToDefault();

        await KeysModel.GenerateNewEncryptionX25519KeyAsync();

        // Fire-and-forget: the key is saved locally, and the service logs and swallows
        // its own failures.
        _ = DependencyService.Get<XcavateProfileService>().UpdateX25519PublicKeyAsync();

        var toast = Toast.Make($"{KeyTypeEnum.EncryptionX25519.GetName()} created successfully.");

        await toast.Show();
    }

    [RelayCommand]
    public Task ImportKeyAsync()
    {
        SetToDefault();

        return Shell.Current.Navigation.PushAsync(new ImportEncryptionX25519KeyPage(new ImportEncryptionX25519KeyPageViewModel
        {
            Navigation = Shell.Current.Navigation.PopAsync,
        }));
    }
}
