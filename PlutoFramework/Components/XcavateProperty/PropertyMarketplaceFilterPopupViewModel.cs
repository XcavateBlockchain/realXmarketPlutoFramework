using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using System.Collections.ObjectModel;
using PlutoFramework.Components.XcavateProperty.Filters;
using PlutoFramework.Model;

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class PropertyMarketplaceFilterPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedCountry = PropertyMarketplaceFilterOptions.Countries[0];

        [ObservableProperty]
        private string selectedTownCity = PropertyMarketplaceFilterOptions.TownCities[0];

        [ObservableProperty]
        private string selectedPropertyType = PropertyMarketplaceFilterOptions.PropertyTypes[0];

        [ObservableProperty]
        private string selectedPriceSort = PropertyMarketplaceFilterOptions.PriceSortOptions[0];

        public ObservableCollection<string> Countries { get; } = new(PropertyMarketplaceFilterOptions.Countries);

        public ObservableCollection<string> TownCities { get; } = new(PropertyMarketplaceFilterOptions.TownCities);

        public ObservableCollection<string> PropertyTypes { get; } = new(PropertyMarketplaceFilterOptions.PropertyTypes);

        public ObservableCollection<string> PriceSortOptions { get; } = new(PropertyMarketplaceFilterOptions.PriceSortOptions);

        public ButtonStateEnum ContinueButtonState => ButtonStateEnum.Enabled;

        public void SetToDefault()
        {
            IsVisible = false;
            SearchText = string.Empty;
            SelectedCountry = PropertyMarketplaceFilterOptions.Countries[0];
            SelectedTownCity = PropertyMarketplaceFilterOptions.TownCities[0];
            SelectedPropertyType = PropertyMarketplaceFilterOptions.PropertyTypes[0];
            SelectedPriceSort = PropertyMarketplaceFilterOptions.PriceSortOptions[0];
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            // TODO
        }
    }
}
