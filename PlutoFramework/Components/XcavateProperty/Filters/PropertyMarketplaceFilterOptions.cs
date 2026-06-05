using System.Collections.Generic;

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

}
