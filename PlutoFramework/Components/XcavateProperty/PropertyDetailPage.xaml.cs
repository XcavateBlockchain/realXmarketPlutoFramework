using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Solana;
using PlutoFramework.Model;
using PlutoFramework.Templates.PageTemplate;
using PlutoFrameworkCore.Solana;

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

        ApplyDevnetBannerOffset();

        SolanaNetworkModel.ClusterChanged += OnClusterChanged;

        X25519WarningModel.AvailabilityChanged += OnX25519AvailabilityChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Subscribed only while the page is on screen: a reserve submitted from this page
        // must re-read the listing once it confirms, but a popped page's view model must
        // not be kept alive by the static event.
        viewModel.SubscribeToMarketplaceTransactions();

        // The income, agent-election and pending-finalization sections' first reads
        // (later ones ride the transaction-confirmed refresh like the voting sections).
        // Fire-and-forget: the refreshes swallow and log their own failures.
        _ = viewModel.RefreshIncomeAsync(CancellationToken.None);
        _ = viewModel.RefreshAgentElectionAsync(CancellationToken.None);
        _ = viewModel.RefreshPendingFinalizationsAsync(CancellationToken.None);

        // The bottom action bar follows the Settings main-page role - re-read on every
        // appearance so a role switch while this page sits on the stack takes effect
        // (the lawyer claim-state probe rides along inside the role refresh).
        _ = viewModel.RefreshMainPageRoleAsync();
    }

    protected override void OnDisappearing()
    {
        viewModel.UnsubscribeFromMarketplaceTransactions();

        base.OnDisappearing();
    }

    private void OnClusterChanged(object? sender, SolanaCluster cluster)
    {
        // Same orphan guard as SolanaBalanceCellView.OnClusterChanged: a page left behind
        // when the main page was replaced stays subscribed to the static event forever.
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyDevnetBannerOffset);
    }

    private void OnX25519AvailabilityChanged(object? sender, EventArgs e)
    {
        // Same orphan guard as OnClusterChanged above.
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyDevnetBannerOffset);
    }

    /// <summary>
    /// The header grows by the warning strips' heights while they are showing, so the
    /// content below it must move down by the same amount. Shifted on the whole content
    /// grid - skeleton and loaded data alike - so the swap to real data does not jump.
    /// </summary>
    private void ApplyDevnetBannerOffset()
    {
        contentGrid.Margin = new Thickness(0, SolanaDevnetWarningView.ExtraHeight + X25519MissingWarningView.ExtraHeight, 0, 0);
    }
}
