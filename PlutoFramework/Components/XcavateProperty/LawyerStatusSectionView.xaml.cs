namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The property detail page's lawyers section: the developer's and the SPV's lawyers
/// (truncated wallets, or the vacant-seat copy) with the status of each one's document
/// verdict, read from the indexer's listing row. Shows from the listing's legal phase
/// onwards; the governing copy and colors mirror the page's other status surfaces.
/// </summary>
public partial class LawyerStatusSectionView : VerticalStackLayout
{
    public LawyerStatusSectionView()
    {
        InitializeComponent();
    }
}
