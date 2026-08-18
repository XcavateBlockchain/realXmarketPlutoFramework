using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Account;

public partial class ImportWarningPopupViewModel : ObservableObject, IPopup, ISetToDefault
{

    [ObservableProperty]
    private bool isVisible = false;

    [ObservableProperty]
    private string warningText = string.Empty;

    public Func<Task> ContinueAction { get; set; } = () => Task.FromResult(0);

    public void SetToDefault()
    {
        IsVisible = false;
        WarningText = string.Empty;
        ContinueAction = () => Task.FromResult(0);
    }

    [RelayCommand]
    public async Task ContinueAsync()
    {
        IsVisible = false;

        await ContinueAction.Invoke();
    }
}
