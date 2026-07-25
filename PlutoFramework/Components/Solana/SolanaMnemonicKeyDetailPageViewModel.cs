using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Keys;
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
        /// Deletes the key and leaves the page, since staying on the detail view of a
        /// deleted key shows stale secrets.
        /// </summary>
        [RelayCommand]
        public async Task DeleteSolanaKeyAsync()
        {
            var authentication = await RequirementsModel.CheckAuthenticationAsync();

            if (!authentication.Value || LockedKey is null)
            {
                return;
            }

            await LockedKey.RemoveAsync();

            await Shell.Current.Navigation.PopAsync();
        }
    }
}
