using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Solana;
using PlutoFramework.Components.WebView;
using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Redemption;

/// <summary>
/// Hosts the Xcavate redemption off-ramp (onramp.xcavate.io/staging|production/redemption/)
/// in an <see cref="Messages.X25519WebView"/>, which injects the user's Solana wallet
/// (Wallet Standard) into the page. The sell counterpart of <see cref="AddFunds.OnrampPage"/>.
/// </summary>
public partial class RedemptionPage : PageTemplate
{
    public RedemptionPage(string url)
    {
        InitializeComponent();

        webView.Url = url;

        WebViewLoadFailureMonitor.Attach(webView, webErrorView);

        ApplyDevnetBannerOffset();

        SolanaNetworkModel.ClusterChanged += OnClusterChanged;

        X25519WarningModel.AvailabilityChanged += OnX25519AvailabilityChanged;
    }

    private void OnClusterChanged(object? sender, SolanaCluster cluster)
    {
        // Same orphan guard as RolesPage.OnClusterChanged: a page left behind
        // when its parent was replaced stays subscribed to the static event forever.
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
    /// web view below it must move down by the same amount.
    /// </summary>
    private void ApplyDevnetBannerOffset()
    {
        contentLayout.Margin = new Thickness(0, 65 + SolanaDevnetWarningView.ExtraHeight + X25519MissingWarningView.ExtraHeight, 0, 0);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        WireNavigationBarBack();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        WireNavigationBarBack();
    }

    /// <summary>
    /// Points the navigation bar's back button at this page rather than at the template's
    /// default, which pops the page.
    /// </summary>
    /// <remarks>
    /// Re-applied on every appearance rather than only when the template is applied. The
    /// bar is reached through the control template, whose one-shot hook runs from the
    /// PageTemplate constructor - before this page's own constructor body - and a lookup
    /// that comes back empty there would leave the button silently wired to PopAsync for
    /// the life of the page, which is exactly the bug this page is meant not to have.
    /// </remarks>
    private void WireNavigationBarBack()
    {
        if (TopNavigationBar is not null)
        {
            TopNavigationBar.BackFunc = NavigateBackAsync;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // A visible popup (e.g. a dApp connection request raised by the hosted page) is
        // dismissed before the hosted page navigates back.
        if (PopupManager.TryCloseTopPopup())
        {
            return true;
        }

        if (NavigateBackInWebView())
        {
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private Task NavigateBackAsync()
    {
        if (NavigateBackInWebView())
        {
            return Task.CompletedTask;
        }

        return NavigationModel.PopAsync();
    }

    private bool NavigateBackInWebView()
    {
        // CanGoBackInPage / GoBackInPage rather than the WebView's own CanGoBack and
        // GoBack: those go through MAUI's cached copy of the back-forward list, which does
        // not keep up with client-side routing.
        if (!webView.CanGoBackInPage)
        {
            return false;
        }

        webView.GoBackInPage();

        return true;
    }
}
