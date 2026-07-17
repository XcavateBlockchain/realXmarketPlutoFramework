using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Menu;

public partial class MainMenuPage : PageTemplate
{
	public MainMenuPage()
	{
		InitializeComponent();

		BindingContext = new MainMenuPageViewModel();
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		if (BindingContext is MainMenuPageViewModel viewModel)
		{
			await viewModel.LoadProfileAsync();
		}
	}
}