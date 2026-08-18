namespace PlutoFramework.Components.Onboarding;

public partial class OnboardingInProgressPopup : ContentView
{
    public OnboardingInProgressPopup()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<OnboardingInProgressPopupViewModel>();
    }
}