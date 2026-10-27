using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// One registered on-chain location in the listing-region picker: the postcode as the
/// regions program stores it and the region it belongs to.
/// </summary>
public partial class SelectListingRegionOption : ObservableObject
{
    [ObservableProperty]
    private string postcode = string.Empty;

    [ObservableProperty]
    private ushort regionId;

    [ObservableProperty]
    private string regionText = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool showPostcode = true;
}

/// <summary>
/// Selects a registered location for a property listing or a region for lawyer registration.
/// </summary>
public partial class SelectListingRegionPopupViewModel : ObservableObject, IPopup
{
    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SelectListingRegionOption> options = new();

    private Action<SelectListingRegionOption>? onOptionSelected;

    public void Open(
        IEnumerable<(ushort RegionId, string Postcode)> locations,
        Action<SelectListingRegionOption> onOptionSelectedAction)
    {
        Title = "Select Listing Region";
        onOptionSelected = onOptionSelectedAction;

        Options = new ObservableCollection<SelectListingRegionOption>(
            locations.Select(location => new SelectListingRegionOption
            {
                Postcode = location.Postcode,
                RegionId = location.RegionId,
                RegionText = $"Region {location.RegionId}",
                IsSelected = false,
                ShowPostcode = true,
            }));

        IsVisible = true;
    }

    public void OpenRegions(
        IEnumerable<ushort> regionIds,
        Action<SelectListingRegionOption> onOptionSelectedAction)
    {
        Title = "Select your region";
        onOptionSelected = onOptionSelectedAction;

        Options = new ObservableCollection<SelectListingRegionOption>(
            regionIds.Select(regionId => new SelectListingRegionOption
            {
                RegionId = regionId,
                RegionText = $"Region {regionId}",
                IsSelected = false,
                ShowPostcode = false,
            }));

        IsVisible = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
    }

    [RelayCommand]
    private void SelectOption(SelectListingRegionOption option)
    {
        if (option is null)
        {
            return;
        }

        foreach (var item in Options)
        {
            item.IsSelected = ReferenceEquals(item, option);
        }

        onOptionSelected?.Invoke(option);
        IsVisible = false;
    }
}
