using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Onboarding;

namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnaireWarningPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string text = "";

        [ObservableProperty]
        private Func<Task> navigation = () => Task.FromResult(0);

        [RelayCommand]
        public async Task ContinueAsync()
        {
            var onboardingAgreementCoordinator = new OnboardingAgreementCoordinator();
            await onboardingAgreementCoordinator.StartAsync(Navigation);
        }

        [RelayCommand]
        public Task CancelAsync()
        {
            return Shell.Current.Navigation.PopToRootAsync();
        }
    }
}
