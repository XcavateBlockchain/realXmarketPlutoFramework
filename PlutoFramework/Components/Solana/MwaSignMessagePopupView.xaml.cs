namespace PlutoFramework.Components.Solana;

public partial class MwaSignMessagePopupView : ContentView
{
    public MwaSignMessagePopupView()
    {
        InitializeComponent();

        // Pinned above the full-screen loading overlay, like the password prompt: a
        // signature can be requested while something is loading, and a host that forgets
        // to set a ZIndex must never bury the popup under it.
        ZIndex = PopupLayers.MwaSigning;

        BindingContext = DependencyService.Get<MwaSignMessagePopupViewModel>();
    }
}
