using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Form
{
    /// <summary>
    /// One row of the country list: everything the template draws, already shaped, so it needs
    /// no converters.
    /// </summary>
    public sealed record CountryRow(PhoneCountry Country, bool IsSelected)
    {
        public string Flag => Country.Flag;

        public string Name => Country.Name;

        public string DialCode => $"+{Country.DialCode}";
    }

    /// <summary>
    /// Visibility, search and selection for the country picker stacked over whatever popup the
    /// phone field lives in.
    /// </summary>
    /// <remarks>
    /// A view model of its own, and registered app-wide, for the reason
    /// <c>SolanaTokenSelectViewModel</c> is: <c>BottomPopupCard</c> dismisses a card by casting
    /// its parent's BindingContext to <see cref="IPopup"/> and clearing <c>IsVisible</c>, so
    /// sharing one view model between two stacked cards would close the wrong one.
    ///
    /// One instance serves every <see cref="FormPhoneInputView"/> on screen, so
    /// <see cref="Show"/> - not a constructor - is what says which field asked and is owed the
    /// answer.
    /// </remarks>
    public partial class CountrySelectPopupViewModel : ObservableObject, IPopup
    {
        private Action<PhoneCountry>? onSelected = null;

        private string selectedIsoCode = "";

        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        private ObservableCollection<CountryRow> countries = new();

        [ObservableProperty]
        private string searchText = "";

        /// <summary>
        /// Opens the picker for one field. The callback replaces any earlier one, so only the
        /// field that opened it last is ever answered.
        /// </summary>
        public void Show(PhoneCountry? selected, Action<PhoneCountry> onSelected)
        {
            this.onSelected = onSelected;

            selectedIsoCode = selected?.IsoCode ?? "";

            // Assigned rather than routed through OnSearchTextChanged, so a reopened picker
            // starts from the whole list even when the text was already empty.
            SearchText = "";
            Countries = Rows(PhoneCountries.All);

            IsVisible = true;
        }

        partial void OnSearchTextChanged(string value) => Countries = Rows(PhoneCountries.Search(value));

        [RelayCommand]
        private void Select(CountryRow? row)
        {
            if (row is null)
            {
                return;
            }

            onSelected?.Invoke(row.Country);

            IsVisible = false;
        }

        private ObservableCollection<CountryRow> Rows(IReadOnlyList<PhoneCountry> countries) =>
            new(countries.Select(country => new CountryRow(
                country,
                country.IsoCode.Equals(selectedIsoCode, StringComparison.OrdinalIgnoreCase))));
    }
}
