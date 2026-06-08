namespace PlutoFramework.Model.Xcavate
{
    public enum OnboardingStage
    {
        None = 0,
        SetupPassword = 1,
        SelectRole = 2,
        Questionaire = 3,
        AgreeTerms = 4,
        AgreeAgreement = 5,
        AgreePrivacy = 6,
        EnterUserDetails = 7,
        KYC = 8,
        Finished = 9,
    }
    public class OnboardingModel
    {
        private const string ONBOARDING_STAGE_KEY = "OnboardingStage";

        public static void Clear()
        {
            Preferences.Remove(ONBOARDING_STAGE_KEY);
        }

        public static OnboardingStage GetOnboardingStage()
        {
            int stageValue = Preferences.Get(ONBOARDING_STAGE_KEY, (int)OnboardingStage.None);
            return (OnboardingStage)stageValue;
        }
        public static void SetOnboardingStage(OnboardingStage stage)
        {
            Preferences.Set(ONBOARDING_STAGE_KEY, (int)stage);
        }

        public static bool IsOnboardingCompleted()
        {
            return GetOnboardingStage() == OnboardingStage.Finished;
        }

        public static bool IsOnboardingInProgress()
        {
            var stage = GetOnboardingStage();
            return stage != OnboardingStage.None && stage != OnboardingStage.Finished;
        }
    }
}
