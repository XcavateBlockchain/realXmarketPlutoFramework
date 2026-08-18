using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class HelpPage : PageTemplate
{
	private readonly XcavateNavigationBarViewModel navigationBarViewModel;

	public HelpPage()
	{
		navigationBarViewModel = DependencyService.Get<XcavateNavigationBarViewModel>();
        InitializeComponent();
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		await Task.Delay(100);

		navigationBarViewModel.Selected = XcavateNavigationBarViewModel.XcavateNavigationBarSelection.Help;
	}
}