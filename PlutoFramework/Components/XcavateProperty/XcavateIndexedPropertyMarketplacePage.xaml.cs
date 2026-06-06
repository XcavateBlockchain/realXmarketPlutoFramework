namespace PlutoFramework.Components.XcavateProperty;

public partial class XcavateIndexedPropertyMarketplacePage : ContentPage
{
    private readonly XcavateIndexedPropertyMarketplaceViewModel viewModel;
    private readonly PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel navigationBarViewModel;
    private bool manualSearchRequested;

    public XcavateIndexedPropertyMarketplacePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        viewModel = DependencyService.Get<XcavateIndexedPropertyMarketplaceViewModel>();
        navigationBarViewModel = DependencyService.Get<PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel>();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.AutoSearchCompleted -= OnAutoSearchCompleted;
        viewModel.AutoSearchCompleted += OnAutoSearchCompleted;
    }

    protected override void OnDisappearing()
    {
        viewModel.AutoSearchCompleted -= OnAutoSearchCompleted;
        base.OnDisappearing();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        await Task.Delay(100);

        navigationBarViewModel.Selected = PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel.XcavateNavigationBarSelection.Marketplace;

        await viewModel.InitialLoadAsync(CancellationToken.None);
    }

    private void OnMarketplaceTapped(object? sender, TappedEventArgs e)
    {
    }

    private void OnSearchButtonTapped(object? sender, TappedEventArgs e)
    {
        manualSearchRequested = true;
    }

    private void OnSearchEntryCompleted(object? sender, EventArgs e)
    {
        manualSearchRequested = true;
    }

    private void OnAutoSearchCompleted()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (manualSearchRequested)
            {
                manualSearchRequested = false;
                return;
            }

            if (!MarketplaceSearchEntry.IsFocused)
            {
                MarketplaceSearchEntry.Focus();
            }
        });
    }

    private async void OnNoticeboardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Noticeboard", animate: false);
    }
}
