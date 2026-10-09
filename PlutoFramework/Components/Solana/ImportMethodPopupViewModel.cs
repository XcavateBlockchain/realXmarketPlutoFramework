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
        // Capture before hiding: the card's close path runs SetToDefault, which resets
        // the delegates, and with the animator effectively instant it does so before the
        // invoke below would read them.
        var chosen = SeedPhraseChosen;

        IsVisible = false;

        await chosen.Invoke();
    }

    [RelayCommand]
    public async Task ChooseMwaAsync()
    {
        if (!MwaIsSupported)
        {
            return;
        }

        // See ChooseSeedPhraseAsync: the close path resets the delegates.
        var chosen = MwaChosen;

        IsVisible = false;

        await chosen.Invoke();
    }
}
