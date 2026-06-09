namespace PlutoFramework.Components.Agreement;

public partial class AgreementPage : ContentPage
{
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
        await UpdateCanAcceptAsync();
    }

    private async void OnWebViewScrolled(object sender, ScrolledEventArgs e)
    {
        await UpdateCanAcceptAsync();
    }

    private async Task UpdateCanAcceptAsync()
    {
        if (BindingContext is not AgreementPageViewModel viewModel || viewModel.CanAccept)
        {
            return;
        }

        if (await IsScrolledToBottomAsync())
        {
            viewModel.CanAccept = true;
        }
    }

    private async Task<bool> IsScrolledToBottomAsync()
    {
        try
        {
            var result = await webView.EvaluateJavaScriptAsync(@"(function() {
                var scrollingElement = document.scrollingElement || document.documentElement || document.body;

                if (!scrollingElement) {
                    return false;
                }

                var scrollTop = Math.max(window.pageYOffset || 0, scrollingElement.scrollTop || 0);
                var viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
                var contentHeight = Math.max(
                    scrollingElement.scrollHeight || 0,
                    document.body ? document.body.scrollHeight || 0 : 0,
                    document.documentElement ? document.documentElement.scrollHeight || 0 : 0
                );

                return scrollTop + viewportHeight >= contentHeight - 100;
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