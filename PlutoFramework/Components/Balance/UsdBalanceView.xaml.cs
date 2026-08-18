using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.HydraDX;
using PlutoFramework.Model.SQLite;

namespace PlutoFramework.Components.Balance;

public partial class UsdBalanceView : ContentView, ISubstrateClientLoadableAsyncView, ILocalLoadableAsyncView, ISetEmptyView
{
	public UsdBalanceView()
	{
		InitializeComponent();

        BindingContext = new UsdBalanceViewModel();
    }
    public async Task LoadAsync(CancellationToken token)
    {
        if (KeysModel.HasSubstrateKey())
        {
            return;
        }

        AssetsModel.LoadAssets(await BalancesDatabase.GetBalancesAsync());

        var viewModel = (UsdBalanceViewModel)BindingContext;
        viewModel.ReloadIsVisible = false;
        viewModel.UsdSum = AssetsModel.UsdSum.ToCurrencyString();
    }

    public async Task LoadAsync(PlutoFrameworkSubstrateClient client, CancellationToken token)
    {
        if (!KeysModel.HasSubstrateKey())
        {
            return;
        }

        var viewModel = (UsdBalanceViewModel)BindingContext;
        viewModel.UpdateBalances();
    }

    public void SetEmpty()
    {
        if (!KeysModel.HasSubstrateKey())
        {
            return;
        }

        var viewModel = (UsdBalanceViewModel)BindingContext;
        viewModel.ReloadIsVisible = true;
    }
}

