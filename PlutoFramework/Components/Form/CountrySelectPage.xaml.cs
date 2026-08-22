using PlutoFramework.Model;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Form;

/// <summary>
/// The country picker a <see cref="FormPhoneInputView"/> opens: every ISO 3166-1 country with
/// its flag and calling code, filtered by a search box.
/// </summary>
/// <remarks>
/// A pushed page rather than one of the app's bottom popups. The phone field itself usually
/// sits inside a bottom popup, which only covers the lower 60% of the screen and cannot host a
/// second sheet, and a list of 249 rows wants the whole screen anyway.
/// </remarks>
public partial class CountrySelectPage : PageTemplate
{
    public CountrySelectPage(PhoneCountry? selected, Action<PhoneCountry> onSelected)
    {
        InitializeComponent();

        BindingContext = new CountrySelectPageViewModel(selected, async country =>
        {
            onSelected(country);

            await Navigation.PopAsync();
        });
    }
}
