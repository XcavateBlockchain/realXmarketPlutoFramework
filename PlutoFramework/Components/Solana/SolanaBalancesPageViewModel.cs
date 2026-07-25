using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFrameworkCore.Solana;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Solana
{
    public partial class SolanaBalancesPageViewModel : ObservableObject
    {
        public ObservableCollection<SolanaTokenBalance> Balances { get; } = [];

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAccount))]
        [NotifyPropertyChangedFor(nameof(NoAccount))]
        [NotifyPropertyChangedFor(nameof(QrAddress))]
        private string address = string.Empty;

        [ObservableProperty]
        private string totalText = "-";

        [ObservableProperty]
        private string networkName = SolanaNetworkModel.SelectedCluster.GetName();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ErrorIsVisible))]
        private string errorMessage = string.Empty;

        public bool HasAccount => !string.IsNullOrEmpty(Address);

        public bool NoAccount => !HasAccount;

        public bool ErrorIsVisible => !string.IsNullOrEmpty(ErrorMessage);

        public string QrAddress => $"solana:{Address}";

        public SolanaBalancesPageViewModel()
        {
            SolanaNetworkModel.ClusterChanged += OnClusterChanged;
        }

        /// <summary>
        /// Called by the page when it disappears. Without it the static event keeps every
        /// view model this page ever created alive, each re-querying on a network change.
        /// </summary>
        public void Unsubscribe() => SolanaNetworkModel.ClusterChanged -= OnClusterChanged;

        private void OnClusterChanged(object? sender, SolanaCluster cluster)
        {
            NetworkName = cluster.GetName();

            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(CancellationToken.None));
        }

        [RelayCommand]
        public Task RefreshAsync() => LoadAsync(CancellationToken.None);

        public async Task LoadAsync(CancellationToken token)
        {
            Address = KeysModel.GetSolanaAddress() ?? string.Empty;
            NetworkName = SolanaNetworkModel.SelectedCluster.GetName();

            if (!HasAccount)
            {
                Balances.Clear();
                TotalText = "-";
                return;
            }

            IsRefreshing = true;
            ErrorMessage = string.Empty;

            try
            {
                var rows = await SolanaBalancesModel.GetBalancesAsync(
                    Address, SolanaNetworkModel.SelectedCluster, token);

                Balances.Clear();

                foreach (var row in rows)
                {
                    Balances.Add(row);
                }

                TotalText = SolanaBalanceAssembler.TotalUsd(rows).ToUsdCurrencyString();
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (SolanaRpcException ex)
            {
                // Distinguished from an empty wallet on purpose: showing zeros here would
                // claim a balance we never actually read.
                ErrorMessage = ex.Message;
                TotalText = "-";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load balances: {ex.Message}";
                TotalText = "-";
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }
}
