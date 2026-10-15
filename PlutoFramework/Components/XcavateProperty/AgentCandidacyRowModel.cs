using PlutoFramework.Components.Buttons;
using System.Windows.Input;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// One row of the agent-election section's candidacy list: the candidate's display
/// identity, the vote power behind it, the caller's own-vote marker and the row's
/// Vote / Change-vote button. Rows are rebuilt wholesale on every election refresh
/// (the same idiom as the income section's lines), so the properties are immutable
/// snapshots rather than observable.
/// </summary>
public sealed class AgentCandidacyRowModel(
    string agent,
    string identity,
    string votePowerText,
    bool isMyVote,
    ButtonStateEnum voteButtonState,
    ICommand voteCommand)
{
    /// <summary>The candidate agent's full pubkey - the vote call is keyed by it.</summary>
    public string Agent { get; } = agent;

    /// <summary>The truncated display form of <see cref="Agent"/>.</summary>
    public string Identity { get; } = identity;

    /// <summary>The share-weighted power behind this candidacy, formatted for display.</summary>
    public string VotePowerText { get; } = votePowerText;

    /// <summary>True when the connected wallet's current-round vote backs this candidacy.</summary>
    public bool IsMyVote { get; } = isMyVote;

    public string VoteButtonText => IsMyVote ? "Change vote" : "Vote";

    /// <summary>Disabled while the round has ended or the wallet has no free voting power.</summary>
    public ButtonStateEnum VoteButtonState { get; } = voteButtonState;

    public ICommand VoteCommand { get; } = voteCommand;
}
