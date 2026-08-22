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
    /// The searchable list behind <see cref="CountrySelectPage"/>.
    /// </summary>
    /// <remarks>
    /// Constructed with its callback rather than resolved from <see cref="DependencyService"/>
    /// like the popup view models, because the page is opened by whichever phone field was
    /// tapped and has to answer that one field.
    /// </remarks>
    public partial class CountrySelectPageViewModel : ObservableObject
    {
        private readonly Func<PhoneCountry, Task> onSelected;

        private readonly string selectedIsoCode;

        public CountrySelectPageViewModel(PhoneCountry? selected, Func<PhoneCountry, Task> onSelected)
        {
            this.onSelected = onSelected;

            selectedIsoCode = selected?.IsoCode ?? "";
            countries = Rows(PhoneCountries.All);
        }

        [ObservableProperty]
        private ObservableCollection<CountryRow> countries;

        [ObservableProperty]
        private string searchText = "";

        partial void OnSearchTextChanged(string value) => Countries = Rows(PhoneCountries.Search(value));

        [RelayCommand]
        public Task SelectAsync(CountryRow? row) =>
            row is null ? Task.CompletedTask : onSelected(row.Country);

        private ObservableCollection<CountryRow> Rows(IReadOnlyList<PhoneCountry> countries) =>
            new(countries.Select(country => new CountryRow(
                country,
                country.IsoCode.Equals(selectedIsoCode, StringComparison.OrdinalIgnoreCase))));
    }
}
