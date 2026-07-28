namespace PlutoFramework.Components.Settings;

public partial class MainKeySettingsView : ContentView
{
    public MainKeySettingsView()
    {
        InitializeComponent();

        BindingContext = new MainKeySettingsViewModel();
    }
}
