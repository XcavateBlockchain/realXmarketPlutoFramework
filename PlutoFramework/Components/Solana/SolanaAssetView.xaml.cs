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
            control.amountLabel.Text = $"{FormatAmount(balance)} {balance.Symbol}";

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

    /// <summary>
    /// Trailing zeros are trimmed so a whole balance reads "40 USDC" rather than
    /// "40.000000 USDC", but a dust balance keeps enough places to stay visible.
    /// </summary>
    private static string FormatAmount(SolanaTokenBalance balance)
    {
        var rounded = Math.Round(balance.Amount, Math.Min(balance.Decimals, 6));

        return rounded.ToString("0.######");
    }
}
