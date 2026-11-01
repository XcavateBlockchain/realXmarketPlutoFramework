using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;

namespace PlutoFramework.Components.XcavateProperty;

public partial class DeveloperListPropertyPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    private IReadOnlyList<(ushort RegionId, string Postcode)> locations = [];
    private Func<SelectListingRegionOption, Task> onListProperty = _ => Task.CompletedTask;
    private Action? onClosed;
    private SelectListingRegionPopupViewModel? regionPicker;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRegionText))]
    [NotifyPropertyChangedFor(nameof(ListButtonState))]
    private SelectListingRegionOption? selectedOption;

    public string SelectedRegionText => SelectedOption is { } option
        ? $"Region {option.RegionId} · {option.Postcode}"
        : "Select a region";

    public ButtonStateEnum ListButtonState => SelectedOption is not null
        ? ButtonStateEnum.Enabled
        : ButtonStateEnum.Disabled;

    public void Open(
        IReadOnlyList<(ushort RegionId, string Postcode)> registeredLocations,
        Func<SelectListingRegionOption, Task> listProperty,
        Action closed)
    {
        locations = registeredLocations;
        SelectedOption = null;
        onListProperty = listProperty;
        onClosed = closed;
        IsVisible = true;
    }

    [RelayCommand]
    private void SelectRegion()
    {
        regionPicker = DependencyService.Get<SelectListingRegionPopupViewModel>();
        regionPicker.Open(locations, option => SelectedOption = option);
    }

    [RelayCommand]
    private async Task ListPropertyAsync()
    {
        if (SelectedOption is not { } option)
        {
            return;
        }

        var listing = onListProperty(option);
        Reset(notifyClosed: false);
        await listing;
    }

    public void SetToDefault() => Reset(notifyClosed: true);

    private void Reset(bool notifyClosed)
    {
        regionPicker?.IsVisible = false;
        regionPicker = null;
        IsVisible = false;
        SelectedOption = null;
        locations = [];
        onListProperty = _ => Task.CompletedTask;

        var closed = onClosed;
        onClosed = null;

        if (notifyClosed)
        {
            closed?.Invoke();
        }
    }
}
