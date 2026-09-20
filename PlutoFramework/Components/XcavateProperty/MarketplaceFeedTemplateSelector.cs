namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The marketplace feed is a flat item list: the risk-warning/search header, the loading
/// skeleton and the empty-state caption are list items, not CollectionView.Header/Footer.
/// On iOS a header or footer is hosted as a UICollectionView supplementary view, and
/// self-sizing ones (MeasureAllItems) crash the page on navigation.
/// </summary>
public class MarketplaceFeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? SkeletonTemplate { get; set; }
    public DataTemplate? EmptyStateTemplate { get; set; }
    public DataTemplate? PropertyTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            MarketplaceFeedHeaderItem => HeaderTemplate!,
            MarketplaceFeedSkeletonItem => SkeletonTemplate!,
            MarketplaceFeedEmptyStateItem => EmptyStateTemplate!,
            _ => PropertyTemplate!,
        };
    }
}

/// <summary>
/// Marker item for a non-property slot of the marketplace feed. It carries the page view
/// model so its DataTemplate can bind to the search, loading and empty-state properties.
/// </summary>
public abstract class MarketplaceFeedItem
{
    protected MarketplaceFeedItem(XcavateIndexedPropertyMarketplaceViewModel viewModel)
    {
        ViewModel = viewModel;
    }

    public XcavateIndexedPropertyMarketplaceViewModel ViewModel { get; }
}

public class MarketplaceFeedHeaderItem : MarketplaceFeedItem
{
    public MarketplaceFeedHeaderItem(XcavateIndexedPropertyMarketplaceViewModel viewModel) : base(viewModel) { }
}

public class MarketplaceFeedSkeletonItem : MarketplaceFeedItem
{
    public MarketplaceFeedSkeletonItem(XcavateIndexedPropertyMarketplaceViewModel viewModel) : base(viewModel) { }
}

public class MarketplaceFeedEmptyStateItem : MarketplaceFeedItem
{
    public MarketplaceFeedEmptyStateItem(XcavateIndexedPropertyMarketplaceViewModel viewModel) : base(viewModel) { }
}
