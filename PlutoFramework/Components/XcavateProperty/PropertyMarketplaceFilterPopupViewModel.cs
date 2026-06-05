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
        private readonly PropertyMarketplaceSelectionPopupViewModel selectionPopupViewModel;

        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedTownCity = PropertyMarketplaceFilterOptions.TownCities[0];

        [ObservableProperty]
        private string selectedPropertyType = PropertyMarketplaceFilterOptions.PropertyTypes[0];

        public ObservableCollection<string> TownCities { get; } = new(PropertyMarketplaceFilterOptions.TownCities);

        public ObservableCollection<string> PropertyTypes { get; } = new(PropertyMarketplaceFilterOptions.PropertyTypes);

        public Func<Task>? ApplyRequested { get; set; }

        public ButtonStateEnum ContinueButtonState => ButtonStateEnum.Enabled;

        public PropertyMarketplaceFilterPopupViewModel()
        {
            selectionPopupViewModel = DependencyService.Get<PropertyMarketplaceSelectionPopupViewModel>();
        }

        public void SetToDefault()
        {
            IsVisible = false;
            SearchText = string.Empty;
            SelectedTownCity = PropertyMarketplaceFilterOptions.TownCities[0];
            SelectedPropertyType = PropertyMarketplaceFilterOptions.PropertyTypes[0];
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (ApplyRequested != null)
            {
                await ApplyRequested().ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private void OpenTownCitySelector()
        {
            selectionPopupViewModel.Open(
                title: "Town City",
                options: TownCities,
                selectedOption: SelectedTownCity,
                onOptionSelectedAction: option => SelectedTownCity = option);
        }

        [RelayCommand]
        private void OpenPropertyTypeSelector()
        {
            selectionPopupViewModel.Open(
                title: "Property Type",
                options: PropertyTypes,
                selectedOption: SelectedPropertyType,
                onOptionSelectedAction: option => SelectedPropertyType = option);
        }
    }
}
