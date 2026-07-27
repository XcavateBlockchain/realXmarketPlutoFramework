using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Password;

public partial class SetupPasswordPage : PageTemplate
{
    public required Func<Task> Navigation;

    private bool _clicked = false;

    public SetupPasswordPage()
    {
        InitializeComponent();
    }

    private void OnPasswordValidityChanged(object? sender, EventArgs e)
    {
        continueButton.ButtonState = setPasswordView.IsValid
            ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;
    }

    private async void ContinueToMainPageClicked(System.Object sender, System.EventArgs e)
    {
        if (_clicked || !setPasswordView.IsValid) return;

        _clicked = true;

        try
        {
            await PasswordSetupModel.SaveNewPasswordAsync(setPasswordView.Password);
        }
        catch (Exception ex)
        {
            await Toast.Make($"Could not save your password: {ex.Message}").Show();

            _clicked = false;

            return;
        }

        await Navigation.Invoke();

        _clicked = false;
    }
}
