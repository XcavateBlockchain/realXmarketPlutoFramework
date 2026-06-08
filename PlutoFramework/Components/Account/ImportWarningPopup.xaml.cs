namespace PlutoFramework.Components.Account;

public partial class ImportWarningPopup : ContentView
{
    public ImportWarningPopup()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<ImportWarningPopupViewModel>();
    }
}
