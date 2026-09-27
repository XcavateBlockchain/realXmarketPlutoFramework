using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.WebUtilities;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore;

namespace PlutoFramework.Components.Keys
{
    public partial class ImportEncryptionX25519KeyPageViewModel : ObservableObject
    {
        public required Func<Task> Navigation;

        [ObservableProperty]
        private bool incorrectSecretKeyEntered = false;

        [ObservableProperty]
        private string secretKey = "";

        /// <summary>
        /// Pushes the new public key onto the stored profile, when there is one. Fire-and-forget:
        /// the key is already saved locally, and the service logs and swallows its own failures.
        /// </summary>
        private static void UpdateProfileX25519Key() =>
            _ = DependencyService.Get<XcavateProfileService>().UpdateX25519PublicKeyAsync();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            try
            {
                var secretKeyBytes = WebEncoders.Base64UrlDecode(SecretKey);

                if (secretKeyBytes.Length != 32)
                {
                    throw new FormatException("Invalid key length");
                }

                await Model.KeysModel.SaveEncryptionX25519KeyAsync(
                    secretKeyBytes
                );

                UpdateProfileX25519Key();

                await Navigation.Invoke();
            }
            catch
            {
                IncorrectSecretKeyEntered = true;
            }
        }

        [RelayCommand]
        public void ForgotKey()
        {
            var popupViewModel = DependencyService.Get<CanNotRecoverKeyPopupViewModel>();

            popupViewModel.ProceedFunc = GenerateNewKeyAsync;

            popupViewModel.IsVisible = true;
        }

        public async Task GenerateNewKeyAsync()
        {
            await Model.KeysModel.GenerateNewEncryptionX25519KeyAsync();

            UpdateProfileX25519Key();

            await Navigation.Invoke();
        }

        [RelayCommand]
        public async Task ImportJsonAsync()
        {
            // Cancelled or failed imports stay on the page: the toast already said why,
            // and leaving would strand the replace flow's caller two pages up.
            if (await KeysModel.ImportJsonX25519KeyAsync())
            {
                UpdateProfileX25519Key();

                await Navigation.Invoke();
            }
        }
    }
}
