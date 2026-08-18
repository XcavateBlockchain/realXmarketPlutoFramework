using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Onboarding;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Agreement;

public partial class AgreementPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AcceptButtonState))]
    [NotifyPropertyChangedFor(nameof(AcceptButtonText))]
    private bool canAccept = false;

    [ObservableProperty]
    private Func<Task> acceptFunction = () => Task.CompletedTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Step))]
    [NotifyPropertyChangedFor(nameof(Steps))]
    private OnboardingStage onboardingStage = OnboardingStage.AgreeTerms;

    public ButtonStateEnum AcceptButtonState => CanAccept ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

    public string AcceptButtonText => CanAccept ? "Accept" : "Scroll to bottom";

    public int Step => OnboardingStepperViewModel.GetStep(OnboardingStage);

    public int Steps => OnboardingStepperViewModel.TotalSteps;

    [RelayCommand]
    public async Task AcceptAsync()
    {
        if (!CanAccept)
        {
            return;
        }

        await AcceptFunction.Invoke();
    }
}