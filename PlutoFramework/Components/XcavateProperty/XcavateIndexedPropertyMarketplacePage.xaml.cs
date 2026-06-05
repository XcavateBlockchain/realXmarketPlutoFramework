namespace PlutoFramework.Components.XcavateProperty;

public partial class XcavateIndexedPropertyMarketplacePage : ContentPage
{
    private readonly XcavateIndexedPropertyMarketplaceViewModel viewModel;

    public XcavateIndexedPropertyMarketplacePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        viewModel = DependencyService.Get<XcavateIndexedPropertyMarketplaceViewModel>();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        await viewModel.InitialLoadAsync(CancellationToken.None);
    }
}
