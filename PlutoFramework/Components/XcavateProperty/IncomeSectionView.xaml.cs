namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The property detail page's rent-income section: the connected wallet's claimable
/// rent per payment mint with a "Claim rent" action, and each mint's lifetime
/// per-token distribution total. A plain view over the page's view model, exactly like
/// <see cref="GovernanceVotingSectionView"/>.
/// </summary>
public partial class IncomeSectionView : VerticalStackLayout
{
    public IncomeSectionView()
    {
        InitializeComponent();
    }
}
