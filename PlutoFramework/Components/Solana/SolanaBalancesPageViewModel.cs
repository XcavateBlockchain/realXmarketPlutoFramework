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
        /// <summary>
        /// Cancels and replaces itself at the top of every <see cref="LoadAsync"/> call, so an
        /// older in-flight load (e.g. one still waiting on an RPC response for the previous
        /// cluster) can never win a race against a newer one and write stale rows under the
        /// new cluster's badge.
        /// </summary>
        private CancellationTokenSource? loadCts;

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
        /// Also cancels any in-flight load, so a request started before navigating away does
        /// not resolve later and write into a view model nothing is looking at anymore.
        /// </summary>
        public void Unsubscribe()
        {
            SolanaNetworkModel.ClusterChanged -= OnClusterChanged;

            loadCts?.Cancel();
        }

        /// <summary>
        /// Cancels and disposes the previous load's token source, then hands back a fresh
        /// token linked to <paramref name="externalToken"/> for the caller to use. Not
        /// lock-protected: unlike <c>InvestorMainPageViewModel</c>, every caller of
        /// <see cref="LoadAsync"/> here (OnAppearing, the refresh command, and the
        /// cluster-changed handler via <c>MainThread.BeginInvokeOnMainThread</c>) runs on the
        /// UI thread's single synchronization context, so there is no genuine concurrent
        /// access to guard - only sequential interleaving of awaits, which cancellation alone
        /// already resolves.
        /// </summary>
        private CancellationToken ReplaceLoadingToken(CancellationToken externalToken)
        {
            var previousCts = loadCts;
            var newCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            loadCts = newCts;

            previousCts?.Cancel();
            previousCts?.Dispose();

            return newCts.Token;
        }

        private void OnClusterChanged(object? sender, SolanaCluster cluster)
        {
            NetworkName = cluster.GetName();

            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(CancellationToken.None));
        }

        [RelayCommand]
        public Task RefreshAsync() => LoadAsync(CancellationToken.None);

        public async Task LoadAsync(CancellationToken token)
        {
            var loadToken = ReplaceLoadingToken(token);

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
                    Address, SolanaNetworkModel.SelectedCluster, loadToken);

                // Guards against a load that finished normally (its RPC calls may not have
                // observed the token) after a newer load already superseded it. ReplaceLoadingToken
                // cancels the previous source synchronously before this one starts, so if this
                // token is stale, IsCancellationRequested is already true here regardless of how
                // the awaited call itself completed.
                loadToken.ThrowIfCancellationRequested();

                Balances.Clear();

                foreach (var row in rows)
                {
                    Balances.Add(row);
                }

                TotalText = SolanaBalanceAssembler.TotalUsd(rows).ToUsdCurrencyString();
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query, or a newer load (network switch, pull-to-refresh)
                // superseded this one before it finished.
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
                // Only the load that is still current should clear the spinner - otherwise a
                // superseded load's finally could turn it off while its replacement is still
                // running.
                if (loadCts is not null && loadToken == loadCts.Token)
                {
                    IsRefreshing = false;
                }
            }
        }
    }
}
