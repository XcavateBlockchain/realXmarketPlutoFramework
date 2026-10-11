using PlutoFramework.Model;

namespace PlutoFramework.Components.XcavateProperty;

public partial class XcavateIndexedPropertyNoticeboardPage : ContentPage
{
    private readonly XcavateIndexedPropertyNoticeboardViewModel viewModel;
    private readonly PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel navigationBarViewModel;
    private CancellationTokenSource? initializationCts;
    private bool isInitialized;

    public XcavateIndexedPropertyNoticeboardPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        viewModel = DependencyService.Get<XcavateIndexedPropertyNoticeboardViewModel>();
        navigationBarViewModel = DependencyService.Get<PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel>();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        // A visible popup (e.g. the transfer popup) is dismissed before the page itself
        // goes back.
        return PopupManager.TryCloseTopPopup() || base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        initializationCts?.Cancel();
        initializationCts?.Dispose();
        initializationCts = null;

        viewModel.CancelPendingOperations();
        base.OnDisappearing();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        initializationCts?.Cancel();
        initializationCts?.Dispose();
        initializationCts = new CancellationTokenSource();
        var token = initializationCts.Token;

        try
        {
            await Task.Delay(100, token);

            navigationBarViewModel.Selected = PlutoFramework.Components.Xcavate.XcavateNavigationBarViewModel.XcavateNavigationBarSelection.Marketplace;

            await viewModel.InitialLoadAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Page disappeared while initialization work was running.
        }
    }

    private async void OnMarketplaceTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Marketplace", animate: false);
    }

    private void OnNoticeboardTapped(object? sender, TappedEventArgs e)
    {
    }
}
