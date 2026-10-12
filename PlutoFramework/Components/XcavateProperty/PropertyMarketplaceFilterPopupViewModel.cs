using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using System.Collections.ObjectModel;
using PlutoFramework.Components.XcavateProperty.Filters;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

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

        /// <summary>
        /// Whether the State checkboxes show. This view model is a shared singleton: the
        /// marketplace page's feed filters by listing state server-side, the investor
        /// page's query has no such filter, so each page's OpenFilter decides.
        /// </summary>
        [ObservableProperty]
        private bool isStateFilterVisible = false;

        public ObservableCollection<string> TownCities { get; } = new(PropertyMarketplaceFilterOptions.TownCities);

        public ObservableCollection<string> PropertyTypes { get; } = new(PropertyMarketplaceFilterOptions.PropertyTypes);

        public ObservableCollection<PropertyMarketplaceFilterStateOption> StateOptions { get; } =
            new(PropertyMarketplaceFilterOptions.States.Select(state => new PropertyMarketplaceFilterStateOption
            {
                Label = state.Label,
                State = state.State,
                IsSelected = state.SelectedByDefault,
            }));

        /// <summary>The checkbox selection aggregated into the feed's state filter flags.</summary>
        public PropertyListingStateFilter SelectedStates
        {
            get
            {
                var states = PropertyListingStateFilter.None;

                foreach (var option in StateOptions)
                {
                    if (option.IsSelected)
                    {
                        states |= option.State;
                    }
                }

                return states;
            }
        }

        public Func<Task>? ApplyRequested { get; set; }
        public Func<Task>? CancelRequested { get; set; }

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

            foreach (var (option, definition) in StateOptions.Zip(PropertyMarketplaceFilterOptions.States))
            {
                option.IsSelected = definition.SelectedByDefault;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            SetToDefault();
            CancelRequested?.Invoke();
        }

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
