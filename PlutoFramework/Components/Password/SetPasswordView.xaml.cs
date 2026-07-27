using PlutoFramework.Model;

namespace PlutoFramework.Components.Password;

/// <summary>
/// The password half of a setup screen: both entries, their reveal toggles, and live rule
/// and mismatch feedback.
/// </summary>
/// <remarks>
/// Owns no storage and no navigation. The host page decides what a valid password is for,
/// which is what lets the same view serve the create flow and the seed-phrase import.
/// </remarks>
public partial class SetPasswordView : ContentView
{
    public SetPasswordView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised whenever <see cref="IsValid"/> may have changed, so a host page can drive its
    /// own Continue button without polling.
    /// </summary>
    public event EventHandler? ValidityChanged;

    public string Password => passwordEntry.Text ?? "";

    private string Confirmation => confirmPasswordEntry.Text ?? "";

    /// <summary>
    /// Both fields are on screen together, so the confirmation is checked as it is typed
    /// rather than on the Continue tap.
    /// </summary>
    public bool IsValid => PasswordRulesModel.IsValid(Password) && Confirmation == Password;

    private void OnEyeballClicked(object sender, TappedEventArgs e)
    {
        passwordEntry.IsPassword = !passwordEntry.IsPassword;
        eyeball.IsVisible = passwordEntry.IsPassword;
        eyeballSlash.IsVisible = !passwordEntry.IsPassword;
    }

    private void OnConfirmEyeballClicked(object sender, TappedEventArgs e)
    {
        confirmPasswordEntry.IsPassword = !confirmPasswordEntry.IsPassword;
        confirmEyeball.IsVisible = confirmPasswordEntry.IsPassword;
        confirmEyeballSlash.IsVisible = !confirmPasswordEntry.IsPassword;
    }

    private async void OnEnterPressedAsync(object sender, EventArgs e)
    {
        var entry = (Entry)sender;

        if (entry.IsSoftInputShowing())
        {
            await entry.HideSoftInputAsync(CancellationToken.None);
        }
    }

    private void OnPasswordPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Text") return;

        UpdateFeedback();
    }

    private void OnConfirmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Text") return;

        UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        var password = Password;

        lengthRequirementLabel.TextColor = PasswordRulesModel.HasAllowedLength(password)
            ? Colors.Green : Colors.DarkRed;
        lowercaseRequirementLabel.TextColor = PasswordRulesModel.HasLowercase(password)
            ? Colors.Green : Colors.DarkRed;
        uppercaseRequirementLabel.TextColor = PasswordRulesModel.HasUppercase(password)
            ? Colors.Green : Colors.DarkRed;
        numberRequirementLabel.TextColor = PasswordRulesModel.HasDigit(password)
            ? Colors.Green : Colors.DarkRed;

        // Silent until there is something to disagree with, so the label does not accuse the
        // user of a mismatch after their first keystroke in the confirmation field.
        passwordMatchLabel.IsVisible = Confirmation.Length > 0 && Confirmation != password;

        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }
}
