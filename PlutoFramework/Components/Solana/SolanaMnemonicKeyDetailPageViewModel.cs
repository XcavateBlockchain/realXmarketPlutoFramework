using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Settings;
using PlutoFramework.Model;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Components.Solana
{
    public partial class SolanaMnemonicKeyDetailPageViewModel : BaseDetailPageViewModel
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mnemonics))]
        [NotifyPropertyChangedFor(nameof(Address))]
        [NotifyPropertyChangedFor(nameof(QrAddress))]
        private SolanaMnemonicKey? unlockedKey;

        public string Mnemonics => UnlockedKey?.Mnemonics ?? "No seed phrase";

        public string Address => UnlockedKey?.Address ?? PublicKey;

        /// <summary>
        /// The Solana Pay URI scheme, which Solana wallets scan to prefill a transfer.
        /// </summary>
        public string QrAddress => $"solana:{Address}";

        /// <summary>
        /// Deletes the key, which is also the account, so confirmation goes through the
        /// logout popup and a confirmed delete logs out rather than popping back to a
        /// key list that no longer has an owner.
        /// </summary>
        [RelayCommand]
        public async Task DeleteSolanaKeyAsync()
        {
            var authentication = await RequirementsModel.CheckAuthenticationAsync();

            if (!authentication.Value || LockedKey is null)
            {
                return;
            }

            var lockedKey = LockedKey;

            var popupViewModel = DependencyService.Get<LogOutPopupViewModel>();

            popupViewModel.ContinueRequested = async () =>
            {
                await lockedKey.RemoveAsync();

                await LogOutModel.LogOutAsync();
            };

            popupViewModel.IsVisible = true;
        }
    }
}
