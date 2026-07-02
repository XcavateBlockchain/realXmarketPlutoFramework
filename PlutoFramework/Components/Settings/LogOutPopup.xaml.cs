namespace PlutoFramework.Components.Settings;

public partial class LogOutPopup : ContentView
{
    public LogOutPopup()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<LogOutPopupViewModel>();
    }
}