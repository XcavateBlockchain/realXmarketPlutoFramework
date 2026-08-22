namespace PlutoFramework.Components.Form;

/// <summary>
/// The country picker a <see cref="FormPhoneInputView"/> opens: every ISO 3166-1 country with
/// its flag and calling code, filtered by a search box.
/// </summary>
/// <remarks>
/// Hosted in the page template rather than per page, and stacked above the popup layer, so a
/// phone field can raise it from inside another bottom card - which is where the field usually
/// lives - the way the Solana token picker stacks over the transfer popup.
/// </remarks>
public partial class CountrySelectPopupView : ContentView
{
    public CountrySelectPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<CountrySelectPopupViewModel>();
    }
}
