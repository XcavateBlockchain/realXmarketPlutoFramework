namespace PlutoFramework.Components.Solana;

/// <summary>
/// Deliberately sets no <see cref="BindableObject.BindingContext"/>: hosts assign their own
/// <see cref="SolanaMnemonicsEntryViewModel"/>, which is what lets a page and a popup show
/// this without sharing one instance - and one user's phrase.
/// </summary>
public partial class SolanaMnemonicsEntryView : ContentView
{
    public SolanaMnemonicsEntryView()
    {
        InitializeComponent();
    }
}
