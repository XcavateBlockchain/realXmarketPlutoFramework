namespace PlutoFramework.Components.Agreement;

public partial class AgreementPage : ContentPage
{
    private IDispatcherTimer? _scrollPollTimer;
    private bool _checkingScroll;

    public AgreementPage(string url, Func<Task>? acceptFunction = null)
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        BindingContext = new AgreementPageViewModel
        {
            Url = url,
            AcceptFunction = acceptFunction ?? DefaultAcceptAsync,
        };
    }

    private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        StartScrollPolling();
        await UpdateCanAcceptAsync();
    }

    private async void OnWebViewScrolled(object sender, ScrolledEventArgs e)
    {
        await UpdateCanAcceptAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopScrollPolling();
    }

    private void StartScrollPolling()
    {
        if (_scrollPollTimer is not null)
        {
            _scrollPollTimer.Stop();
        }

        _scrollPollTimer = Dispatcher.CreateTimer();
        _scrollPollTimer.Interval = TimeSpan.FromMilliseconds(250);
        _scrollPollTimer.Tick += async (_, __) =>
        {
            await UpdateCanAcceptAsync();

            if (BindingContext is AgreementPageViewModel vm && vm.CanAccept)
            {
                StopScrollPolling();
            }
        };
        _scrollPollTimer.Start();
    }

    private void StopScrollPolling()
    {
        if (_scrollPollTimer is null)
        {
            return;
        }

        _scrollPollTimer.Stop();
        _scrollPollTimer = null;
    }

    private async Task UpdateCanAcceptAsync()
    {
        if (_checkingScroll)
        {
            return;
        }

        if (BindingContext is not AgreementPageViewModel viewModel || viewModel.CanAccept)
        {
            return;
        }

        try
        {
            _checkingScroll = true;

            if (await IsScrolledToBottomAsync())
            {
                viewModel.CanAccept = true;
                StopScrollPolling();
            }
        }
        finally
        {
            _checkingScroll = false;
        }
    }

    private async Task<bool> IsScrolledToBottomAsync()
    {
        if (webView.IsNativeScrolledToBottom())
        {
            return true;
        }

        try
        {
            var result = await webView.EvaluateJavaScriptAsync(@"(function() {
                var threshold = 80;

                var doc = document.documentElement || {};
                var body = document.body || {};
                var scrollingElement = document.scrollingElement || doc || body;

                var scrollTop = Math.max(
                    window.pageYOffset || 0,
                    window.scrollY || 0,
                    scrollingElement.scrollTop || 0,
                    body.scrollTop || 0,
                    doc.scrollTop || 0
                );

                if (scrollTop < 0) scrollTop = 0;

                var viewportHeight = Math.max(
                    window.innerHeight || 0,
                    doc.clientHeight || 0,
                    (window.visualViewport && window.visualViewport.height) || 0
                );

                var contentHeight = Math.max(
                    scrollingElement.scrollHeight || 0,
                    body.scrollHeight || 0,
                    doc.scrollHeight || 0,
                    body.offsetHeight || 0,
                    doc.offsetHeight || 0
                );

                if (contentHeight > 0 && viewportHeight > 0 && scrollTop > 0 && (scrollTop + viewportHeight) >= (contentHeight - threshold)) {
                    return true;
                }

                // iOS pages often scroll inside nested containers (overflow: auto/scroll)
                // while document scrollTop remains unchanged.
                var nodes = document.querySelectorAll('*');
                for (var i = 0; i < nodes.length; i++) {
                    var el = nodes[i];

                    if (!el || typeof el.scrollHeight !== 'number' || typeof el.clientHeight !== 'number') {
                        continue;
                    }

                    var maxScrollable = el.scrollHeight - el.clientHeight;
                    if (maxScrollable <= 1) {
                        continue;
                    }

                    var elScrollTop = typeof el.scrollTop === 'number' ? el.scrollTop : 0;
                    if (elScrollTop < 0) {
                        elScrollTop = 0;
                    }

                    if (elScrollTop > 0 && (elScrollTop + el.clientHeight) >= (el.scrollHeight - threshold)) {
                        return true;
                    }
                }

                return false;
            })();");

            return string.Equals(result?.Trim().Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task DefaultAcceptAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.Navigation.PopAsync();
        }
    }
}