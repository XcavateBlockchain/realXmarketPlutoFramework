using PlutoFramework.Components.UniversalScannerView;
using PlutoFramework.Model;
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
        // Through NavigationModel rather than this view's own Navigation: the app is Shell
        // based, and NavigationModel.GetCurrentNavigation prefers Shell.Current.Navigation.
        // A view living in the page template's control template is a long way from the page,
        // and resolving the stack by hand here would be a worse copy of that method.

        // ZXing keeps delivering frames it had already queued after IsDetecting goes false,
        // so the handler can run more than once. A second PopAsync would pop the page under
        // the scanner.
        var handled = false;

        await NavigationModel.PushAsync(new UniversalScannerPage
        {
            OnScannedMethod = (_, args) =>
            {
                // BarcodesDetected is raised on ZXing's analysis thread. Everything below
                // touches the navigation stack and bound properties, so all of it has to be
                // marshalled — not just the property writes. Popping from the detection
                // thread throws, and in an async void handler that exception is unhandled
                // and takes the app down.
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (handled)
                    {
                        return;
                    }

                    handled = true;

                    try
                    {
                        var scanned = args.Results.LastOrDefault()?.Value;

                        // A code that is not a Solana address goes into the field as-is, so
                        // the popup's own "Not a valid Solana address" explains why nothing
                        // was filled in. Silently returning an empty field reads as the scan
                        // having failed to happen at all.
                        var recipient = SolanaUri.TryParseRecipient(scanned) ?? scanned;

                        if (!string.IsNullOrEmpty(recipient))
                        {
                            recipientEntry.Text = recipient;
                            viewModel.Recipient = recipient;
                        }

                        await NavigationModel.PopAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Solana address scan failed: {ex.Message}");
                    }
                });
            },
        });
    }
}
