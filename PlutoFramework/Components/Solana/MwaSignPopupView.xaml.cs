namespace PlutoFramework.Components.Solana;

public partial class MwaSignPopupView : ContentView
{
    public MwaSignPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<MwaSignPopupViewModel>();
    }
}
