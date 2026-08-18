namespace PlutoFramework.Components.XcavateProperty;

public partial class XcavateIndexedPropertyNoticeboardPage : ContentPage
{
    private readonly PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel navigationBarViewModel;

    public XcavateIndexedPropertyNoticeboardPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        navigationBarViewModel = DependencyService.Get<PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel>();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        await Task.Delay(100);

        navigationBarViewModel.Selected = PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel.XcavateNavigationBarSelection.Marketplace;
    }

    private async void OnMarketplaceTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Marketplace", animate: false);
    }

    private void OnNoticeboardTapped(object? sender, TappedEventArgs e)
    {
    }
}
