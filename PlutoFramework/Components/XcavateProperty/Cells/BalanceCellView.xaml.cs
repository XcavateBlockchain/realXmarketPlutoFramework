using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Balance;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.HydraDX;
using PlutoFramework.Model.SQLite;

namespace PlutoFramework.Components.XcavateProperty.Cells;

public partial class BalanceCellView : ContentView, ISetEmptyView, ISubstrateClientLoadableAsyncView, ILocalLoadableAsyncView
{
    public BalanceCellView()
    {
        InitializeComponent();

        cell.Command = new AsyncRelayCommand(NavigationModel.NavigateToBalancesPageAsync);
    }

    public async Task LoadAsync(CancellationToken token)
    {
        if (!KeysModel.HasSubstrateKey())
        {
            return;
        }

        AssetsModel.LoadAssets(await BalancesDatabase.GetBalancesAsync());

        cell.Value = AssetsModel.UsdSum.ToCurrencyString();
    }

    public async Task LoadAsync(PlutoFrameworkSubstrateClient client, CancellationToken token)
    {
        if (!KeysModel.HasSubstrateKey())
        {
            return;
        }

        cell.Value = Model.AssetsModel.UsdSum.ToCurrencyString();
    }

    public void SetEmpty()
    {
        AssetsModel.UpdateUsdBalance();

        cell.Value = Model.AssetsModel.UsdSum.ToCurrencyString();
    }
}