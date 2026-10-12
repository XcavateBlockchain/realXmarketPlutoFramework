using System.Collections.Generic;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.XcavateProperty.Filters;

public static class PropertyMarketplaceFilterOptions
{
    public static readonly IReadOnlyList<string> TownCities = new[]
    {
        "All"
    };

    public static readonly IReadOnlyList<string> PropertyTypes = new[]
    {
        "All",
        "Apartment",
        "Flat",
        "Bungalow",
        "Detached",
        "Semi-Detached",
        "Terraced"
    };

    /// <summary>
    /// The State section's checkboxes, in display order. Only "Can be reserved" is
    /// checked by default; the flags map onto indexer statuses in
    /// <see cref="XcavateMarketplaceIndexerModel.ToListingStatuses"/>.
    /// </summary>
    public static readonly IReadOnlyList<(string Label, PropertyListingStateFilter State, bool SelectedByDefault)> States = new[]
    {
        ("Can be reserved", PropertyListingStateFilter.CanBeReserved, true),
        ("Waiting for claim", PropertyListingStateFilter.WaitingForClaim, false),
        ("Expired", PropertyListingStateFilter.Expired, false),
        ("Finalized", PropertyListingStateFilter.Finalized, false),
        ("Cancelled / Refunding", PropertyListingStateFilter.CancelledOrRefunding, false),
    };
}
