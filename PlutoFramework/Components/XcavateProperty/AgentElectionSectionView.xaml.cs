namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The property detail page's letting-agent section: the sitting agent, the live
/// election round's candidacies with their vote power and Vote / Change-vote actions,
/// the unlock of shares locked by an ended round's vote, and the finalize-election
/// crank once the round has closed.
/// </summary>
public partial class AgentElectionSectionView : VerticalStackLayout
{
    public AgentElectionSectionView()
    {
        InitializeComponent();
    }
}
