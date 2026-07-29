using PlutoFramework.Components.UniversalScannerView;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Transfer;

public partial class SolanaTransferView : ContentView
{
    private readonly SolanaTransferViewModel viewModel;

    public SolanaTransferView()
    {
        InitializeComponent();

        viewModel = DependencyService.Get<SolanaTransferViewModel>();

        // The page template's popup layout inherits the page's BindingContext, so a popup
        // bound to a singleton has to set its own.
        BindingContext = viewModel;
    }

    /// <summary>
    /// Two-way binding on an Entry does not survive the popup being reset, the same problem
    /// <c>AssetInputView</c> documents. Pushing on TextChanged keeps validation running on
    /// every keystroke.
    /// </summary>
    private void OnRecipientChanged(object sender, TextChangedEventArgs e) =>
        viewModel.Recipient = e.NewTextValue ?? string.Empty;

    private void OnAmountChanged(object sender, TextChangedEventArgs e) =>
        viewModel.Amount = e.NewTextValue ?? string.Empty;

    private void OnCancelClicked(object sender, EventArgs e) => viewModel.SetToDefault();

    private void OnTransferClicked(object sender, EventArgs e) =>
        viewModel.TransferCommand.Execute(null);

    /// <summary>
    /// Mirrors <c>IdentityAddressView.OnShowQRClicked</c>: push the scanner, take the first
    /// result, pop. Parsing is shared with the global scanner through
    /// <see cref="SolanaUri.TryParseRecipient"/>.
    /// </summary>
    private async void OnScanClicked(object sender, TappedEventArgs e)
    {
        var navigation = Navigation ?? Application.Current?.Windows[0].Page?.Navigation;

        if (navigation is null)
        {
            return;
        }

        await navigation.PushAsync(new UniversalScannerPage
        {
            OnScannedMethod = async (_, args) =>
            {
                var scanned = args.Results.LastOrDefault()?.Value;

                var recipient = SolanaUri.TryParseRecipient(scanned);

                if (recipient is not null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        viewModel.Recipient = recipient;
                        recipientEntry.Text = recipient;
                    });
                }

                await navigation.PopAsync();
            },
        });
    }
}
