using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The noticeboard feed is a flat item list like the marketplace's: the loading
/// skeleton and the empty-state caption are list items, not CollectionView.Header/Footer
/// (on iOS those are hosted as UICollectionView supplementary views, and self-sizing
/// ones crash the page on navigation - see MarketplaceFeedTemplateSelector).
/// The page's Marketplace / Notice Board toggle is fixed chrome above the list, so
/// unlike the marketplace feed there is no header item type here.
/// </summary>
public class NoticeboardFeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SkeletonTemplate { get; set; }
    public DataTemplate? EmptyStateTemplate { get; set; }
    public DataTemplate? PropertyTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            NoticeboardFeedSkeletonItem => SkeletonTemplate!,
            NoticeboardFeedEmptyStateItem => EmptyStateTemplate!,
            _ => PropertyTemplate!,
        };
    }
}

/// <summary>
/// Marker item for a non-property slot of the noticeboard feed. It carries the page
/// view model so its DataTemplate can bind to the loading and empty-state properties.
/// </summary>
public abstract class NoticeboardFeedItem
{
    protected NoticeboardFeedItem(XcavateIndexedPropertyNoticeboardViewModel viewModel)
    {
        ViewModel = viewModel;
    }

    public XcavateIndexedPropertyNoticeboardViewModel ViewModel { get; }
}

public class NoticeboardFeedSkeletonItem : NoticeboardFeedItem
{
    public NoticeboardFeedSkeletonItem(XcavateIndexedPropertyNoticeboardViewModel viewModel) : base(viewModel) { }
}

public class NoticeboardFeedEmptyStateItem : NoticeboardFeedItem
{
    public NoticeboardFeedEmptyStateItem(XcavateIndexedPropertyNoticeboardViewModel viewModel) : base(viewModel) { }
}

/// <summary>
/// One noticeboard card: the same property thumbnail the marketplace shows, plus the
/// secondary-market figures - the badge line and the share listing the detail page
/// opens with when the card is tapped.
/// </summary>
public class NoticeboardPropertyItem
{
    public required long AssetId { get; init; }

    public required XcavateNftWrapper Wrapper { get; init; }

    /// <summary>The badge line, e.g. "12 tokens from £10.40/token".</summary>
    public required string BadgeText { get; init; }

    /// <summary>The cheapest active share listing for the asset - the card's offer context.</summary>
    public required XcavateShareListingContext ShareListingContext { get; init; }
}
