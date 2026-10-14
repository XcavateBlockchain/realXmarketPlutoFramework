using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Windows.Input;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The confirmation sheet behind every governance vote on the property detail page:
/// the Figma "Approve terms" / "Reject terms" bottom sheet, generalized to the letting
/// seat's Yes/No votes and the agent-election candidacy vote. The opening view model
/// sets <see cref="VoteKind"/> ("terms", "proposal", "challenge" or "agent") first -
/// plus <see cref="IsApprove"/> for the Yes/No kinds, <see cref="CandidateIdentity"/>
/// and <see cref="VotePower"/> for "agent"; the title, the confirmation sentence and
/// the outcome ride those.
/// </summary>
public partial class CastVotePopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    private bool _isVisible;

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    private string _voteKind = "terms";

    /// <summary>"terms" (the SPV-terms ratification) | "proposal" | "challenge" | "agent" (agent-election candidacy vote).</summary>
    public string VoteKind
    {
        get => _voteKind;
        set
        {
            if (SetProperty(ref _voteKind, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Message));
            }
        }
    }

    private string _candidateIdentity = "";

    /// <summary>The candidacy's display identity the "agent" vote backs (already truncated for display).</summary>
    public string CandidateIdentity
    {
        get => _candidateIdentity;
        set
        {
            if (SetProperty(ref _candidateIdentity, value))
            {
                OnPropertyChanged(nameof(Message));
            }
        }
    }

    private uint _votePower;

    /// <summary>The shares the "agent" vote will lock - the voter's full free voting power.</summary>
    public uint VotePower
    {
        get => _votePower;
        set
        {
            if (SetProperty(ref _votePower, value))
            {
                OnPropertyChanged(nameof(Message));
            }
        }
    }

    private bool _isApprove = true;

    public bool IsApprove
    {
        get => _isApprove;
        set
        {
            if (SetProperty(ref _isApprove, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Message));
            }
        }
    }

    /// <summary>The Figma sheet titles; the terms vote is the Approve/Reject pair.</summary>
    public string Title => VoteKind switch
    {
        "terms" => IsApprove ? "Approve terms" : "Reject terms",
        "proposal" => IsApprove ? "Vote Yes — spending proposal" : "Vote No — spending proposal",
        "agent" => "Vote for letting agent",
        _ => IsApprove ? "Vote Yes — challenge" : "Vote No — challenge",
    };

    /// <summary>The confirmation sentence the voter signs off on.</summary>
    public string Message => VoteKind switch
    {
        "terms" => IsApprove
            ? "I have reviewed the document and agree with the proposed management terms."
            : "I do not agree with the current terms and would like a revision.",
        "proposal" => IsApprove
            ? "I vote Yes on this spending proposal."
            : "I vote No on this spending proposal.",
        "agent" => $"Vote for {CandidateIdentity} as the letting agent? Your vote power: {VotePower} tokens.",
        _ => IsApprove
            ? "I vote Yes to back this challenge against the letting agent."
            : "I vote No to back the letting agent.",
    };

    public ICommand ContinueCommand { get; }

    public ICommand CancelCommand { get; }

    public CastVotePopupViewModel()
    {
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        CancelCommand = new RelayCommand(Cancel);
    }

    public Func<Task> ContinueRequested { get; set; } = () => Task.FromResult(0);

    public void SetToDefault()
    {
        IsVisible = false;
        VoteKind = "terms";
        IsApprove = true;
        CandidateIdentity = "";
        VotePower = 0;
        ContinueRequested = () => Task.FromResult(0);
    }

    public Task ContinueAsync()
    {
        var continueRequested = ContinueRequested;

        IsVisible = false;

        return MainThread.InvokeOnMainThreadAsync(continueRequested);
    }

    public void Cancel()
    {
        SetToDefault();
    }
}
