using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Password;
using PlutoFramework.Components.Mnemonics;
using PlutoFramework.Components.Kilt;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore;

namespace PlutoFramework.Components.Account;

public partial class NoAccountPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    public NoAccountPopupViewModel()
    {
    }

    [ObservableProperty]
    private bool isVisible = false;

    public void SetToDefault()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void Cancel() => SetToDefault();

    [RelayCommand]
    public async Task CreateAccountAsync()
    {
        IsVisible = false;

        if (NavigationModel.StartImportAccount is not null)
        {
            await NavigationModel.StartImportAccount(ImportAccountFlowMode.Create);
            return;
        }

        // Fallback: inline create flow if app coordinator not registered
        await Shell.Current.Navigation.PushAsync(new SetupPasswordPage
        {
            Navigation = async () =>
            {
                await PlutoConfigurationModel.GenerateNewAccountAsync();

                OnboardingModel.SetOnboardingStage(OnboardingStage.SelectRole);

                await NavigationModel.NavigateAfterAccountCreation.Invoke();
            }
        });
    }


    [RelayCommand]
    public Task ImportAccountAsync()
    {
        SetToDefault();

        return NavigationModel.StartImportAccount(ImportAccountFlowMode.Import);
    }
}
