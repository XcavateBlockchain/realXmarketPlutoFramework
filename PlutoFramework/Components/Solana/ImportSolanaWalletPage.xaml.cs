using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
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
    public required new Func<Task> Navigation;

    /// <summary>
    /// Owned by this page rather than resolved from <see cref="DependencyService"/>, so the
    /// phrase typed here reaches nothing else.
    /// </summary>
    /// <remarks>
    /// Assigned to the entry view directly in the constructor. Do not give this page a
    /// <see cref="BindableObject.BindingContext"/> to bind it through: <c>PageTemplate</c>
    /// pushes the page's context down onto its content, which would overwrite the
    /// assignment below.
    /// </remarks>
    private readonly SolanaMnemonicsEntryViewModel _entry = new();

    private bool _clicked = false;

    public ImportSolanaWalletPage()
    {
        InitializeComponent();

        mnemonicsEntry.BindingContext = _entry;

        _entry.PropertyChanged += (_, _) => UpdateContinueState();
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

        // The phrase has served its purpose, and this page stays alive behind whatever the
        // callback navigates to.
        _entry.Reset();

        await Navigation.Invoke();

        _clicked = false;
    }
}
