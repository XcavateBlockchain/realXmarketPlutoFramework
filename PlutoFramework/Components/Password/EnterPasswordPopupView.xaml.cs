namespace PlutoFramework.Components.Password;

public partial class EnterPasswordPopupView : ContentView
{
	public EnterPasswordPopupView()
	{
		InitializeComponent();

		// Top-most by default so a hosting page that forgets to set a ZIndex still
		// renders the password prompt above the full-screen loading overlay (ZIndex=20)
		// and every other popup layer. The user must never be locked out of entering
		// a password because something is loading.
		ZIndex = 1000;

        BindingContext = DependencyService.Get<EnterPasswordPopupViewModel>();
    }

    void OnPasswordChanged(System.Object sender, Microsoft.Maui.Controls.TextChangedEventArgs e)
    {
        ((EnterPasswordPopupViewModel)BindingContext).ErrorIsVisible = false;
    }

    private void OnEyeballClicked(object sender, TappedEventArgs e)
    {
        passwordEntry.IsPassword = !passwordEntry.IsPassword;
        eyeball.IsVisible = passwordEntry.IsPassword;
        eyeballSlash.IsVisible = !passwordEntry.IsPassword;
    }
}