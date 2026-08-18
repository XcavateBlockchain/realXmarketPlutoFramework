namespace PlutoFramework.Components.Solana;

public partial class CreateSolanaMnemonicsPopupView : ContentView
{
    public CreateSolanaMnemonicsPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<CreateSolanaMnemonicsPopupViewModel>();
    }
}
