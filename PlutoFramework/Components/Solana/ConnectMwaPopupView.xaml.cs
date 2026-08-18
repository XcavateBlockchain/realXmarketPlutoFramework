namespace PlutoFramework.Components.Solana;

public partial class ConnectMwaPopupView : ContentView
{
    public ConnectMwaPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<ConnectMwaPopupViewModel>();
    }
}
