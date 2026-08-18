using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyMarketplaceSelectionOption : ObservableObject
{
    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

public partial class PropertyMarketplaceSelectionPopupViewModel : ObservableObject, IPopup
{
    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PropertyMarketplaceSelectionOption> options = new();

    private Action<string>? onOptionSelected;

    public void Open(
        string title,
        IEnumerable<string> options,
        string selectedOption,
        Action<string> onOptionSelectedAction)
    {
        Title = title;
        onOptionSelected = onOptionSelectedAction;

        Options = new ObservableCollection<PropertyMarketplaceSelectionOption>(
            options.Select(option => new PropertyMarketplaceSelectionOption
            {
                Text = option,
                IsSelected = string.Equals(option, selectedOption, StringComparison.Ordinal)
            }));

        IsVisible = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
    }

    [RelayCommand]
    private void SelectOption(PropertyMarketplaceSelectionOption option)
    {
        if (option is null)
        {
            return;
        }

        foreach (var item in Options)
        {
            item.IsSelected = ReferenceEquals(item, option);
        }

        onOptionSelected?.Invoke(option.Text);
        IsVisible = false;
    }
}
