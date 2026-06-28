using PlutoFramework.Components.Agreement;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Onboarding;

public interface IOnboardingAgreementCoordinator
{
    Task StartAsync(Func<Task> completionNavigation);
    Task ContinueFromStageAsync(OnboardingStage stage, Func<Task> completionNavigation);
}

public class OnboardingAgreementCoordinator : IOnboardingAgreementCoordinator
{
    private const string TermsUrl = "https://app.realxmarket.io/terms";
    private const string AgreementUrl = "https://app.realxmarket.io/agreement";
    private const string PrivacyUrl = "https://app.realxmarket.io/privacy";

    private readonly INavigationService _navigationService;
    private Func<Task> _completionNavigation = () => Task.CompletedTask;

    public OnboardingAgreementCoordinator()
        : this(new MauiNavigationService())
    {
    }

    public OnboardingAgreementCoordinator(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task StartAsync(Func<Task> completionNavigation)
    {
        return ContinueFromStageAsync(OnboardingStage.AgreeTerms, completionNavigation);
    }

    public Task ContinueFromStageAsync(OnboardingStage stage, Func<Task> completionNavigation)
    {
        _completionNavigation = completionNavigation;

        return stage switch
        {
            OnboardingStage.AgreeAgreement => NavigateToAgreementAsync(),
            OnboardingStage.AgreePrivacy => NavigateToPrivacyAsync(),
            _ => NavigateToTermsAsync(),
        };
    }

    private Task NavigateToTermsAsync()
    {
        OnboardingModel.SetOnboardingStage(OnboardingStage.AgreeTerms);
        return _navigationService.NavigateToAsync(new AgreementPage(TermsUrl, AcceptTermsAsync));
    }

    private Task NavigateToAgreementAsync()
    {
        OnboardingModel.SetOnboardingStage(OnboardingStage.AgreeAgreement);
        return _navigationService.NavigateToAsync(new AgreementPage(AgreementUrl, AcceptAgreementAsync));
    }

    private Task NavigateToPrivacyAsync()
    {
        OnboardingModel.SetOnboardingStage(OnboardingStage.AgreePrivacy);
        return _navigationService.NavigateToAsync(new AgreementPage(PrivacyUrl, AcceptPrivacyAsync));
    }

    private Task AcceptTermsAsync()
    {
        return NavigateToAgreementAsync();
    }

    private Task AcceptAgreementAsync()
    {
        return NavigateToPrivacyAsync();
    }

    private async Task AcceptPrivacyAsync()
    {
        //await QuestionnaireModel.AcceptTermsAsync(KeysModel.GetPublicKey());

        OnboardingModel.SetOnboardingStage(OnboardingStage.KYC);

        await _completionNavigation.Invoke();
    }
}
