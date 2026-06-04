namespace PlutoFramework.Components.XcavateProperty;

public partial class XcavatePropertyMarketplacePage : ContentPage
{
    private readonly XcavatePropertyMarketplaceViewModel viewModel;

    public XcavatePropertyMarketplacePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        viewModel = DependencyService.Get<XcavatePropertyMarketplaceViewModel>();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        await viewModel.InitialLoadAsync(CancellationToken.None);
    }
}
