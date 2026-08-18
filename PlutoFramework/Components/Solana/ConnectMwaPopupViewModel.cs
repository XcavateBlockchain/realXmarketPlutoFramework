using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// Authorizes an installed Solana wallet app over Mobile Wallet Adapter and reports the
    /// resulting key. Saving it is left to the caller - see <see cref="Completed"/>.
    /// </summary>
    /// <remarks>
    /// One instance is shared through <see cref="DependencyService"/>. Callers set
    /// <see cref="Completed"/> and then <see cref="IsVisible"/>.
    /// </remarks>
    public partial class ConnectMwaPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        /// <summary>
        /// Runs with the authorization once the wallet approves it. The popup closes itself
        /// first. Nothing is persisted here - the caller saves the key, because onboarding
        /// has to set a password before it can.
        /// </summary>
        public Func<SolanaMwaKey, Task> Completed { get; set; } = (SolanaMwaKey key) => Task.CompletedTask;

        /// <summary>
        /// The network is an app-wide setting, so this popup reports which one it will connect
        /// on instead of offering a second place to change it. Re-read every time the popup
        /// opens, because the shared instance outlives any single network selection.
        /// </summary>
        [ObservableProperty]
        private string networkName = SelectedCluster.GetName();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectButtonState))]
        private bool isConnecting = false;

        /// <summary>
        /// What the connection is currently waiting on. Shown verbatim, because "waiting
        /// for your wallet" and "authorizing" mean very different things to a user staring
        /// at a stalled screen.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        private string status = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ErrorIsVisible))]
        private string errorMessage = "";

        public bool StatusIsVisible => !string.IsNullOrEmpty(Status);

        public bool ErrorIsVisible => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Mobile Wallet Adapter is specified for Android only, so on iOS the popup shows an
        /// explanation instead of a dead button.
        /// </summary>
        public bool IsSupported => SolanaMwaModel.IsSupported;

        public bool IsUnsupported => !IsSupported;

        public ButtonStateEnum ConnectButtonState =>
            IsConnecting ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        private static SolanaCluster SelectedCluster => SolanaNetworkModel.SelectedCluster;

        partial void OnIsVisibleChanged(bool value)
        {
            if (value)
            {
                NetworkName = SelectedCluster.GetName();
            }
        }

        public void SetToDefault()
        {
            IsVisible = false;
            IsConnecting = false;
            Status = "";
            ErrorMessage = "";
            Completed = (SolanaMwaKey key) => Task.CompletedTask;
        }

        [RelayCommand]
        public async Task ConnectAsync()
        {
            if (IsConnecting || !IsSupported)
            {
                return;
            }

            IsConnecting = true;
            ErrorMessage = "";
            Status = "";

            var progress = new Progress<MwaConnectStage>(stage => Status = stage switch
            {
                MwaConnectStage.LaunchingWallet => "Opening your wallet app..",
                MwaConnectStage.WaitingForWallet => "Waiting for your wallet to connect..",
                MwaConnectStage.Authorizing => "Approve the request in your wallet..",
                _ => "",
            });

            SolanaMwaKey? connectedKey = null;

            try
            {
                var key = await SolanaMwaModel.ConnectAsync(SelectedCluster, progress, CancellationToken.None);

                // Set before the toast: the wallet has authorized at this point, so a toast
                // that fails to show must not be read as a failed connection.
                connectedKey = key;

                await Toast.Make($"Connected to {key.DisplayName}.").Show();
            }
            catch (MwaConnectFlow.PlatformNotSupportedException)
            {
                ErrorMessage = "Mobile Wallet Adapter is only available on Android.";
            }
            catch (MwaConnectFlow.NoWalletInstalledException)
            {
                ErrorMessage = "No compatible Solana wallet is installed. Install Phantom, Solflare or Backpack, then try again.";
            }
            catch (MwaAuthorizationException ex)
            {
                // The user declined, or the wallet refused the cluster. Not a fault.
                ErrorMessage = ex.Message;
            }
            catch (MwaProtocolException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not connect: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
                Status = "";
            }

            // A failed attempt leaves the popup open on its error message, so the user can
            // retry. Kept outside the try so a throw from the callback cannot be relabelled
            // as a connection failure on an already-closed popup.
            if (connectedKey is null)
            {
                return;
            }

            var completed = Completed;

            IsVisible = false;

            // Reset here rather than leaving it to the card's close animation: onboarding's
            // callback replaces the whole page, and a card torn down mid-animation never
            // reaches SetToDefault.
            SetToDefault();

            await completed.Invoke(connectedKey);

            // The user has just approved this app in their wallet, so asking that wallet
            // for one signature to register the address for notifications follows on
            // naturally. After the callback, which is what actually saves the key.
            await WalletLinkModel.TryLinkSolanaMwaAfterConnectAsync();
        }
    }
}
