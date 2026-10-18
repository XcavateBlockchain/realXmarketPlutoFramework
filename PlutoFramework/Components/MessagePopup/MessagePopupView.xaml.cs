namespace PlutoFramework.Components.MessagePopup;

public partial class MessagePopupView : ContentView
{
	public MessagePopupView()
	{
		InitializeComponent();

        BindingContext = DependencyService.Get<MessagePopupViewModel>();
    }

    void OnBackClicked(System.Object sender, System.EventArgs e)
    {
        var viewModel = DependencyService.Get<MessagePopupViewModel>();
        viewModel.IsVisible = false;
    }
}
