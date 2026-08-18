using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Onboarding;

public class OnboardingStepperViewModel
{
    public const int TotalSteps = 8;

    public OnboardingStepperViewModel(OnboardingStage stage)
    {
        Stage = stage;
    }

    public OnboardingStage Stage { get; }

    public int Step => GetStep(Stage);

    public int Steps => TotalSteps;

    public static int GetStep(OnboardingStage stage)
    {
        return stage switch
        {
            OnboardingStage.SetupPassword => 0,
            OnboardingStage.SelectRole => 1,
            OnboardingStage.EnterUserDetails => 1,
            OnboardingStage.Questionaire => 2,
            OnboardingStage.AgreeTerms => 3,
            OnboardingStage.AgreeAgreement => 4,
            OnboardingStage.AgreePrivacy => 5,
            OnboardingStage.KYC => 6,
            OnboardingStage.ProfileRegistration => 7,
            _ => 0,
        };
    }
}