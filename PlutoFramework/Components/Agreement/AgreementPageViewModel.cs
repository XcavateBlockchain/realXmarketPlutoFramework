using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;

namespace PlutoFramework.Components.Agreement;

public partial class AgreementPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AcceptButtonState))]
    [NotifyPropertyChangedFor(nameof(AcceptButtonText))]
    private bool canAccept = false;

    [ObservableProperty]
    private Func<Task> acceptFunction = () => Task.CompletedTask;

    public ButtonStateEnum AcceptButtonState => CanAccept ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

    public string AcceptButtonText => CanAccept ? "Accept" : "Scroll to bottom";

    [RelayCommand]
    public async Task AcceptAsync()
    {
        if (!CanAccept)
        {
            return;
        }

        await AcceptFunction.Invoke();
    }
}