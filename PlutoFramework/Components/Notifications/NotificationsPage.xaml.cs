using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Notifications;

public partial class NotificationsPage : PageTemplate
{
    private readonly NotificationsPageViewModel viewModel = new();

    public NotificationsPage()
	{
		InitializeComponent();

		BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        viewModel.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        viewModel.OnDisappearing();
    }
}
