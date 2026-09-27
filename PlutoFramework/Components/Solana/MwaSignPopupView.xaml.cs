namespace PlutoFramework.Components.Solana;

public partial class MwaSignPopupView : ContentView
{
    public MwaSignPopupView()
    {
        InitializeComponent();

        // Same pin as the message variant - the two share one layer, above the
        // full-screen loading overlay (see PopupLayers).
        ZIndex = PopupLayers.MwaSigning;

        BindingContext = DependencyService.Get<MwaSignPopupViewModel>();
    }
}
