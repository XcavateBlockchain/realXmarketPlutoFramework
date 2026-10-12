using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.XcavateProperty.Filters;

/// <summary>
/// One row of the marketplace filter popup's State section: a checkbox bound to
/// <see cref="IsSelected"/> that contributes <see cref="State"/> to the feed's
/// server-side status filter while checked.
/// </summary>
public partial class PropertyMarketplaceFilterStateOption : ObservableObject
{
    public required string Label { get; init; }

    public required PropertyListingStateFilter State { get; init; }

    [ObservableProperty]
    private bool isSelected;
}
