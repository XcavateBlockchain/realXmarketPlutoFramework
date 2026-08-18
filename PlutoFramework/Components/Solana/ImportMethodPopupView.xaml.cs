namespace PlutoFramework.Components.Solana;

public partial class ImportMethodPopupView : ContentView
{
    public ImportMethodPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<ImportMethodPopupViewModel>();
    }
}
