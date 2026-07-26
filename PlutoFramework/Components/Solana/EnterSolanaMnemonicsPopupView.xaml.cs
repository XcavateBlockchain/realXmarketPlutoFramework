namespace PlutoFramework.Components.Solana;

public partial class EnterSolanaMnemonicsPopupView : ContentView
{
    public EnterSolanaMnemonicsPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<EnterSolanaMnemonicsPopupViewModel>();
    }
}
