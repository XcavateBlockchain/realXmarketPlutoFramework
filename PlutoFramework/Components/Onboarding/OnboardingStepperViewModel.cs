using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Onboarding;

public class OnboardingStepperViewModel
{
    public const int TotalSteps = 5;

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
            OnboardingStage.Questionaire => 0,
            OnboardingStage.AgreeTerms => 1,
            OnboardingStage.AgreeAgreement => 2,
            OnboardingStage.AgreePrivacy => 3,
            OnboardingStage.KYC => 4,
            _ => 0,
        };
    }
}