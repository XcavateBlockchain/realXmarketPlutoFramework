using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The "Assign Developer Lawyer" sheet on a sold-out property page: the developer types
/// the Solana address of the lawyer they engage for the developer side of the legal
/// phase, and Continue submits assign_developer_lawyer through the page's executor
/// (<see cref="ContinueRequested"/>). The sheet only validates the address' shape - the
/// program enforces the lawyer's registration, region and compliance on chain.
/// </summary>
public partial class AssignDeveloperLawyerPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private bool isVisible = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private string lawyerAddress = "";

    public ButtonStateEnum ContinueButtonState =>
        IsVisible && SolanaAddressValidator.IsValidAddress(LawyerAddress)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.Disabled;

    /// <summary>
    /// The page view model's submission callback - runs after the sheet closes, with
    /// <see cref="LawyerAddress"/> still holding the validated address.
    /// </summary>
    public Func<Task> ContinueRequested { get; set; } = () => Task.FromResult(0);

    [RelayCommand]
    public void Cancel() => SetToDefault();

    [RelayCommand]
    public Task ContinueAsync()
    {
        if (!SolanaAddressValidator.IsValidAddress(LawyerAddress))
        {
            return Task.FromResult(0);
        }

        IsVisible = false;

        return MainThread.InvokeOnMainThreadAsync(ContinueRequested);
    }

    public void SetToDefault()
    {
        IsVisible = false;
        LawyerAddress = "";
        ContinueRequested = () => Task.FromResult(0);
    }
}
