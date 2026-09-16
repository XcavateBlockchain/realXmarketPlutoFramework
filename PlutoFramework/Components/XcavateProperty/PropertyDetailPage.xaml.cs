using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyDetailPage : PageTemplate
{
    private readonly PropertyDetailViewModel viewModel;

    public PropertyDetailPage(PropertyDetailViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;

        // The details may still be loading, so the metadata (and with it the map) is bound
        // rather than captured here.
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Subscribed only while the page is on screen: a reserve submitted from this page
        // must re-read the listing once it confirms, but a popped page's view model must
        // not be kept alive by the static event.
        viewModel.SubscribeToMarketplaceTransactions();
    }

    protected override void OnDisappearing()
    {
        viewModel.UnsubscribeFromMarketplaceTransactions();

        base.OnDisappearing();
    }
}