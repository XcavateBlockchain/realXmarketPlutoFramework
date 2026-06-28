using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;

namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnairePassPageViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool termsAgreed = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool agreementAgreed = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool privacyPolicyAgreed = false;

        public ButtonStateEnum ContinueButtonState =>
            TermsAgreed && AgreementAgreed && PrivacyPolicyAgreed ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        [ObservableProperty]
        private string text = "";

        [ObservableProperty]
        private Func<Task> navigation = () => Task.FromResult(0);

        [RelayCommand]
        public async Task NavigateAsync()
        {
            //await QuestionnaireModel.AcceptTermsAsync(KeysModel.GetPublicKey());

            await Navigation.Invoke();
        }

    }
}
