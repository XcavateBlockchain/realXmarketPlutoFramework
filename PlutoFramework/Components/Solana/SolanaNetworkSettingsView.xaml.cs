namespace PlutoFramework.Components.Solana;

public partial class SolanaNetworkSettingsView : ContentView
{
    public SolanaNetworkSettingsView()
    {
        InitializeComponent();

        BindingContext = new SolanaNetworkSettingsViewModel();
    }
}
