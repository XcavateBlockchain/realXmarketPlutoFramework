using CommunityToolkit.Mvvm.Input;
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
    public SolanaBalanceCellView()
    {
        InitializeComponent();

        cell.Command = new AsyncRelayCommand(OpenBalancesPageAsync);

        SolanaNetworkModel.ClusterChanged += OnClusterChanged;
    }

    private void OnClusterChanged(object? sender, SolanaCluster cluster) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(CancellationToken.None));

    private static Task OpenBalancesPageAsync() => NavigationModel.PushAsync(new SolanaBalancesPage());

    public async Task LoadAsync(CancellationToken token)
    {
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
                address, SolanaNetworkModel.SelectedCluster, token);

            cell.Value = SolanaBalanceAssembler.TotalUsd(rows).ToUsdCurrencyString();
        }
        catch (OperationCanceledException)
        {
            // The page went away mid-query.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Solana balance cell failed to load: {ex.Message}");

            cell.Value = "-";
        }
    }
}
