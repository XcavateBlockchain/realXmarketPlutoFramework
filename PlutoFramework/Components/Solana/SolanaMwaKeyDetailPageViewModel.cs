using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Keys;
using PlutoFramework.Model;
using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana
{
    public partial class SolanaMwaKeyDetailPageViewModel : BaseDetailPageViewModel
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WalletName))]
        [NotifyPropertyChangedFor(nameof(Address))]
        [NotifyPropertyChangedFor(nameof(QrAddress))]
        [NotifyPropertyChangedFor(nameof(ClusterName))]
        private SolanaMwaKey? unlockedKey;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisconnectButtonState))]
        private bool isDisconnecting = false;

        public string WalletName => UnlockedKey?.DisplayName ?? "Solana wallet";

        public string Address => UnlockedKey?.Address ?? PublicKey;

        public string ClusterName => (UnlockedKey?.Cluster ?? SolanaCluster.Mainnet).GetName();

        public string QrAddress => $"solana:{Address}";

        public ButtonStateEnum DisconnectButtonState =>
            IsDisconnecting ? ButtonStateEnum.Disabled : ButtonStateEnum.Warning;

        /// <summary>
        /// Revokes the authorization with the wallet where possible, then removes it
        /// locally either way. Reopening the wallet app to revoke can fail for reasons
        /// outside the user's control, and that must not leave them stuck connected.
        /// </summary>
        [RelayCommand]
        public async Task DisconnectAsync()
        {
            if (IsDisconnecting)
            {
                return;
            }

            var authentication = await RequirementsModel.CheckAuthenticationAsync();

            if (!authentication.Value || LockedKey is null)
            {
                return;
            }

            IsDisconnecting = true;

            try
            {
                var revoked = await SolanaMwaModel.DisconnectAsync(LockedKey, CancellationToken.None);

                await Toast.Make(revoked
                    ? "Wallet disconnected."
                    : "Wallet removed. You may also want to revoke this app inside your wallet app.").Show();

                await Shell.Current.Navigation.PopAsync();
            }
            finally
            {
                IsDisconnecting = false;
            }
        }
    }
}
