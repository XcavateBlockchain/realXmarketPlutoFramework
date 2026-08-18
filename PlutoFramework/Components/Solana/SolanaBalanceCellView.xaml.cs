using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Solana.Status;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// The main page's Balance cell, showing the Solana total and opening the balances page.
/// </summary>
/// <remarks>
/// Implements <see cref="ILocalLoadableAsyncView"/> only. Unlike the Substrate cell it
/// replaces, nothing here needs a connected Substrate client.
/// </remarks>
public partial class SolanaBalanceCellView : ContentView, ILocalLoadableAsyncView
{
    /// <summary>
    /// Cancels and replaces itself at the top of every <see cref="LoadAsync"/> call. Three
    /// callers can overlap here - <c>OnLoaded</c>, <c>InvestorMainPageViewModel.RefreshAsync</c>
    /// and <see cref="OnClusterChanged"/> - all passing <see cref="CancellationToken.None"/>,
    /// so without this a pull-to-refresh that overlaps a cluster switch lets the older RPC
    /// response write last and the headline number shows the other network's balance.
    /// </summary>
    private CancellationTokenSource? loadCts;

    public SolanaBalanceCellView()
    {
        InitializeComponent();

        cell.Command = new AsyncRelayCommand(OpenBalancesPageAsync);

        SolanaNetworkModel.ClusterChanged += OnClusterChanged;
        SolanaTransactionTracker.TransactionConfirmed += OnTransactionConfirmed;
    }

    /// <summary>
    /// A confirmed transfer leaves this headline showing the pre-transfer total. Guarded by
    /// the same orphan check as <see cref="OnClusterChanged"/>: this cell has no disposal
    /// hook, so a cell left behind when the main page was replaced stays subscribed.
    /// </summary>
    private void OnTransactionConfirmed(object? sender, EventArgs e)
    {
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(CancellationToken.None));
    }

    private void OnClusterChanged(object? sender, SolanaCluster cluster)
    {
        // An orphaned cell - one left behind when Application.Current.MainPage was replaced -
        // stays subscribed to the static event forever. Without this it would keep firing RPC
        // calls for a view nothing can see.
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(CancellationToken.None));
    }

    private static Task OpenBalancesPageAsync() => NavigationModel.PushAsync(new SolanaBalancesPage());

    /// <summary>
    /// Cancels and disposes the previous load's token source, then hands back a fresh token
    /// linked to <paramref name="externalToken"/>. Mirrors
    /// <see cref="SolanaBalancesPageViewModel"/>: every caller runs on the UI thread's single
    /// synchronization context, so only sequential interleaving of awaits has to be resolved,
    /// which cancellation alone already does.
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

    public async Task LoadAsync(CancellationToken token)
    {
        var loadToken = ReplaceLoadingToken(token);

        var address = KeysModel.GetSolanaAddress();

        if (string.IsNullOrEmpty(address))
        {
            // A dash, not a formatted zero: "you have no account" and "you have no money"
            // are different statements.
            cell.Value = "-";
            return;
        }

        try
        {
            var rows = await SolanaBalancesModel.GetBalancesAsync(
                address, SolanaNetworkModel.SelectedCluster, loadToken);

            // Guards against a load that finished normally (its RPC calls may not have observed
            // the token) after a newer load already superseded it. ReplaceLoadingToken cancels
            // the previous source synchronously before the newer load starts, so a stale token
            // already reports cancellation here regardless of how the await completed.
            loadToken.ThrowIfCancellationRequested();

            cell.Value = SolanaBalanceAssembler.TotalUsd(rows).ToUsdCurrencyString();
        }
        catch (OperationCanceledException)
        {
            // The page went away mid-query, or a newer load (network switch, pull-to-refresh)
            // superseded this one before it finished.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Solana balance cell failed to load: {ex.Message}");

            cell.Value = "-";
        }
    }
}
