using PlutoFramework.Model.Constants;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Transfer;

/// <summary>
/// One token in the picker. Follows <c>SolanaAssetView</c>'s shape: a single bindable
/// property pushes values into named controls.
/// </summary>
public partial class SolanaTokenSelectRowView : ContentView
{
    public static readonly BindableProperty BalanceProperty = BindableProperty.Create(
        nameof(Balance), typeof(SolanaTransferBalance), typeof(SolanaTokenSelectRowView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (SolanaTokenSelectRowView)bindable;

            if (newValue is not SolanaTransferBalance balance)
            {
                return;
            }

            control.assetIcon.Source = Assets.GetAssetIcon(balance.Symbol);
            control.symbolLabel.Text = balance.Symbol;

            // The spendable figure, not what the wallet holds. Inside this flow every number
            // answers "what can I send", so Max can never offer an unsendable amount.
            var amount = SolanaAmount.FromBaseUnits(
                balance.SpendableBaseUnits.ToString(), balance.Decimals);

            control.amountLabel.Text =
                $"{SolanaAmount.ToDisplayString(amount, balance.Decimals)} {balance.Symbol}";
        });

    public SolanaTokenSelectRowView()
    {
        InitializeComponent();
    }

    public SolanaTransferBalance Balance
    {
        get => (SolanaTransferBalance)GetValue(BalanceProperty);
        set => SetValue(BalanceProperty, value);
    }

    private void OnClicked(object sender, TappedEventArgs e)
    {
        // BindableProperty defaults to null, so a row whose binding has not resolved is
        // still tappable.
        if (Balance is null)
        {
            return;
        }

        DependencyService.Get<SolanaTransferViewModel>().SelectTokenCommand.Execute(Balance);
    }
}
