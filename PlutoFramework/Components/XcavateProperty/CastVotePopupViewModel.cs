using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Windows.Input;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The confirmation sheet behind every governance vote on the property detail page:
/// the Figma "Approve terms" / "Reject terms" bottom sheet, generalized to the letting
/// seat's Yes/No votes. The opening view model sets <see cref="VoteKind"/> ("terms",
/// "proposal" or "challenge") and <see cref="IsApprove"/> first; the title, the
/// confirmation sentence and the outcome ride those two.
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

    /// <summary>"terms" (the SPV-terms ratification) | "proposal" | "challenge".</summary>
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
