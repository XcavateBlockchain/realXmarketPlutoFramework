using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Onboarding;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// Imports an existing Solana wallet from its seed phrase and sets the app password, on one
/// screen.
/// </summary>
/// <remarks>
/// The two belong together because the phrase cannot be saved without the password:
/// <see cref="KeysModel.SaveSolanaMnemonicKeyAsync"/> reads the stored password to encrypt
/// it. Splitting them across two screens is what forced onboarding to ask for a password
/// before the user had done anything about their wallet.
/// </remarks>
public partial class ImportSolanaWalletPage : PageTemplate
{
    /// <summary>
    /// Invoked with the imported phrase. The caller derives the rest of the account's keys
    /// from it, and this page is the only place it ever exists on the device.
    /// </summary>
    public required new Func<string, Task> Navigation;

    /// <summary>
    /// Whether the page is part of the first-run onboarding. Onboarding shows the stepper
    /// bar in place of the top navigation bar, the way the other onboarding pages do; any
    /// other host shows the regular bar with the page title.
    /// </summary>
    public bool FirstSetup
    {
        get => _firstSetup;

        set
        {
            _firstSetup = value;

            UpdateNavigationBars();
        }
    }

    private bool _firstSetup = false;

    /// <summary>
    /// The onboarding step this page sits on, so the stepper bar shows where the user is.
    /// </summary>
    private readonly OnboardingStepperViewModel _stepper = new(OnboardingStage.SetupPassword);

    /// <summary>
    /// Owned by this page rather than resolved from <see cref="DependencyService"/>, so the
    /// phrase typed here reaches nothing else.
    /// </summary>
    /// <remarks>
    /// Assigned to the entry view directly in the constructor. PageTemplate still pushes the
    /// page's context down onto its content, but an explicit binding context on a child is
    /// not overridden by the inherited one, so the entry view keeps this instance.
    /// </remarks>
    private readonly SolanaMnemonicsEntryViewModel _entry = new();

    private bool _clicked = false;

    public ImportSolanaWalletPage()
    {
        InitializeComponent();

        BindingContext = _stepper;

        mnemonicsEntry.BindingContext = _entry;

        _entry.PropertyChanged += (_, _) => UpdateContinueState();

        UpdateNavigationBars();
    }

    /// <summary>
    /// The stepper bar and the top navigation bar are mutually exclusive: onboarding shows
    /// the stepper, like the other onboarding pages, and every other host shows the bar.
    /// </summary>
    private void UpdateNavigationBars()
    {
        stepperBar.IsVisible = FirstSetup;

        NavigationBarIsVisible = !FirstSetup;
    }

    private void OnPasswordValidityChanged(object? sender, EventArgs e) => UpdateContinueState();

    private void UpdateContinueState()
    {
        continueButton.ButtonState = _entry.IsValid && setPasswordView.IsValid
            ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;
    }

    private async void ContinueClicked(object sender, EventArgs e)
    {
        if (_clicked || !_entry.IsValid || !setPasswordView.IsValid) return;

        _clicked = true;

        try
        {
            // This order is the whole reason the two are on one screen:
            // SaveSolanaMnemonicKeyAsync reads the stored password to encrypt the phrase.
            await PasswordSetupModel.SaveNewPasswordAsync(setPasswordView.Password);

            await KeysModel.SaveSolanaMnemonicKeyAsync(_entry.Mnemonics);
        }
        catch
        {
            // The phrase was validated before Continue was enabled, so calling this an
            // invalid phrase would be untrue and would send the user hunting for a typo
            // that is not there.
            _entry.ShowError("Could not save your wallet. Please try again.");

            _clicked = false;

            return;
        }

        // Held for the callback, which derives the account's remaining keys from it. Read
        // before the reset below, because this page stays alive behind whatever the callback
        // navigates to and must not be the thing still holding somebody's seed phrase.
        var mnemonics = _entry.Mnemonics;

        _entry.Reset();

        await Navigation.Invoke(mnemonics);

        _clicked = false;
    }
}
