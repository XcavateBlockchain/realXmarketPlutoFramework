using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// Asks how an existing Solana account should be brought in: the user's own seed phrase, or
/// a wallet app over Mobile Wallet Adapter.
/// </summary>
/// <remarks>
/// Reports the choice and nothing more. Onboarding continues into a password step afterwards
/// and the balances page does not, so the destination belongs to the caller.
/// </remarks>
public partial class ImportMethodPopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    [ObservableProperty]
    private bool isVisible = false;

    public Func<Task> SeedPhraseChosen { get; set; } = () => Task.CompletedTask;

    public Func<Task> MwaChosen { get; set; } = () => Task.CompletedTask;

    /// <summary>
    /// Mobile Wallet Adapter is specified for Android only, so on iOS the option explains
    /// itself instead of failing when tapped.
    /// </summary>
    public bool MwaIsSupported => SolanaMwaModel.IsSupported;

    public bool MwaIsUnsupported => !MwaIsSupported;

    public void SetToDefault()
    {
        IsVisible = false;
        SeedPhraseChosen = () => Task.CompletedTask;
        MwaChosen = () => Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ChooseSeedPhraseAsync()
    {
        IsVisible = false;

        await SeedPhraseChosen.Invoke();
    }

    [RelayCommand]
    public async Task ChooseMwaAsync()
    {
        if (!MwaIsSupported)
        {
            return;
        }

        IsVisible = false;

        await MwaChosen.Invoke();
    }
}
