namespace PlutoFramework.Components.Solana.Transfer;

/// <summary>
/// The token picker, stacked over the transfer popup the way <c>AssetSelectView</c> stacks
/// over <c>TransferView</c>. Shares the transfer view model, so the list it shows and the
/// balance the popup validates against are the same data.
/// </summary>
public partial class SolanaTokenSelectView : ContentView
{
    public SolanaTokenSelectView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<SolanaTransferViewModel>();
    }
}
