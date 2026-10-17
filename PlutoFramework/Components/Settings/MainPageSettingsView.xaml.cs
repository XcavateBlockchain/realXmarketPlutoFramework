namespace PlutoFramework.Components.Settings;

public partial class MainPageSettingsView : ContentView
{
    public MainPageSettingsView()
    {
        InitializeComponent();

        BindingContext = new MainPageSettingsViewModel();
    }
}
