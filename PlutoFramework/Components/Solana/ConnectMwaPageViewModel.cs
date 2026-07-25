using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Components.Solana
{
    public partial class ConnectMwaPageViewModel : ObservableObject
    {
        public Func<Task> Navigation { get; set; } = () => Task.CompletedTask;

        /// <summary>
        /// The network is an app-wide setting, so this page reports which one it will connect
        /// on instead of offering a second place to change it.
        /// </summary>
        public string NetworkName => SelectedCluster.GetName();

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
        /// Mobile Wallet Adapter is specified for Android only, so on iOS the page shows an
        /// explanation instead of a dead button.
        /// </summary>
        public bool IsSupported => SolanaMwaModel.IsSupported;

        public bool IsUnsupported => !IsSupported;

        public ButtonStateEnum ConnectButtonState =>
            IsConnecting ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        private static SolanaCluster SelectedCluster => SolanaNetworkModel.SelectedCluster;

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

            try
            {
                var key = await SolanaMwaModel.ConnectAndSaveAsync(SelectedCluster, progress, CancellationToken.None);

                await Toast.Make($"Connected to {key.DisplayName}.").Show();

                await Navigation.Invoke();
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
        }
    }
}
