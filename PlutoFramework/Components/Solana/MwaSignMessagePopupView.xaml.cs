namespace PlutoFramework.Components.Solana;

public partial class MwaSignMessagePopupView : ContentView
{
    public MwaSignMessagePopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<MwaSignMessagePopupViewModel>();
    }
}
