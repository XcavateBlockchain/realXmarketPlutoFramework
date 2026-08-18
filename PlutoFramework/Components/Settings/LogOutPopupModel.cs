using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Windows.Input;

namespace PlutoFramework.Components.Settings;

public partial class LogOutPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    private bool _isVisible;

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public ICommand ContinueCommand { get; }

    public ICommand CancelCommand { get; }

    public LogOutPopupViewModel()
    {
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        CancelCommand = new RelayCommand(Cancel);
    }

    public Func<Task> ContinueRequested { get; set; } = () => Task.FromResult(0);

    public void SetToDefault()
    {
        IsVisible = false;
    }

    public Task ContinueAsync()
    {
        SetToDefault();

        return MainThread.InvokeOnMainThreadAsync(ContinueRequested);
    }

    public void Cancel()
    {
        SetToDefault();
    }
}