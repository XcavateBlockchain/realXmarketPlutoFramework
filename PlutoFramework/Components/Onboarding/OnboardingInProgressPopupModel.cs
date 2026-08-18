using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Onboarding;

public partial class OnboardingInProgressPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    [ObservableProperty]
    private bool isVisible = false;

    public Func<Task> ContinueRequested { get; set; } = () => Task.FromResult(0);

    public void SetToDefault()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public Task ContinueAsync()
    {
        SetToDefault();

        return MainThread.InvokeOnMainThreadAsync(ContinueRequested);
    }

    [RelayCommand]
    public async Task StartOverAsync()
    {
        SetToDefault();
    }
}