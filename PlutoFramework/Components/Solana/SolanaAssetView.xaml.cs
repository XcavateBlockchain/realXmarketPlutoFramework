using PlutoFramework.Model.Constants;
using PlutoFramework.Model.Currency;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana;

public partial class SolanaAssetView : ContentView
{
    public static readonly BindableProperty BalanceProperty = BindableProperty.Create(
        nameof(Balance), typeof(SolanaTokenBalance), typeof(SolanaAssetView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (SolanaAssetView)bindable;

            if (newValue is not SolanaTokenBalance balance)
            {
                return;
            }

            control.assetIcon.Source = Assets.GetAssetIcon(balance.Symbol);
            control.symbolLabel.Text = balance.Symbol;
            control.amountLabel.Text =
                $"{SolanaAmount.ToDisplayString(balance.Amount, balance.Decimals)} {balance.Symbol}";

            // An unknown price shows nothing at all. "$0.00" would read as "your money is
            // gone" rather than "we could not reach the price feed".
            control.usdLabel.Text = balance.UsdValue is double usd ? usd.ToUsdCurrencyString() : string.Empty;
        });

    public SolanaAssetView()
    {
        InitializeComponent();
    }

    public SolanaTokenBalance Balance
    {
        get => (SolanaTokenBalance)GetValue(BalanceProperty);
        set => SetValue(BalanceProperty, value);
    }

    private async void OnClicked(object sender, TappedEventArgs e)
    {
        // BindableProperty defaults to null, so a row whose binding has not resolved yet is
        // still tappable.
        if (Balance is null)
        {
            return;
        }

        await Navigation.PushAsync(new SolanaTokenDetailPage(Balance));
    }
}
