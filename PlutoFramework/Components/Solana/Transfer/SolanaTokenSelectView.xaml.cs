namespace PlutoFramework.Components.Solana.Transfer;

/// <summary>
/// The token picker, stacked over the transfer popup the way <c>AssetSelectView</c> stacks
/// over <c>TransferView</c>.
/// </summary>
/// <remarks>
/// Its own view model, because <c>BottomPopupCard</c> closes a dismissed popup through the
/// <c>IPopup</c> on its parent's BindingContext — one popup per view model. The balances it
/// lists still come from the transfer view model, so the two cannot disagree.
/// </remarks>
public partial class SolanaTokenSelectView : ContentView
{
    public SolanaTokenSelectView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<SolanaTokenSelectViewModel>();
    }
}
