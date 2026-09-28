using Microsoft.Maui.Controls;

namespace PlutoFramework.Components.WebView;

/// <summary>
/// Watches a <see cref="Microsoft.Maui.Controls.WebView"/> and raises <see cref="FailureOccurred"/>
/// when the hosted page fails to load. A failed navigation (no network, dns failure, refused
/// connection) is reported through the <c>Navigated</c> event with a <see cref="WebNavigationResult.Failure"/>
/// result - an http error page such as a 404 still loads "successfully" - so after every
/// successful navigation this monitor also reads the navigation entry's response status
/// through javascript and reports statuses of 400 or higher.
/// </summary>
public sealed class WebViewLoadFailureMonitor
{
    private const string ResponseStatusProbe =
        "(function(){try{var entry=performance.getEntriesByType('navigation')[0];" +
        "return entry&&typeof entry.responseStatus==='number'?entry.responseStatus:0}catch(e){return 0}})()";

    private readonly Microsoft.Maui.Controls.WebView webView;

    /// <summary>
    /// Urls for which an http error status is not a failure. Single-page apps on static
    /// hosting answer client-side routes with the complete, working app shell but an
    /// error status (the messenger returns 404 for /messages/namespace/{id}), and only
    /// the hosted app itself can tell such a route apart from a real error page.
    /// </summary>
    private readonly Func<string?, bool>? ignoreHttpErrorStatusFor;

    /// <summary>
    /// Raised when the hosted page fails to load.
    /// </summary>
    public event EventHandler<WebViewLoadFailureEventArgs>? FailureOccurred;

    public WebViewLoadFailureMonitor(Microsoft.Maui.Controls.WebView webView, Func<string?, bool>? ignoreHttpErrorStatusFor = null)
    {
        this.webView = webView;
        this.ignoreHttpErrorStatusFor = ignoreHttpErrorStatusFor;

        webView.Navigated += OnNavigated;
    }

    /// <summary>
    /// Wires a web view and its error page together: failures raise the page, Retry reloads
    /// the last url, and a successful navigation hides the page again.
    /// </summary>
    /// <param name="ignoreHttpErrorStatusFor">
    /// Optional predicate naming the urls whose host serves working pages with an http
    /// error status; those statuses are not reported as failures. Genuine load failures
    /// (no network, dns, refused connection) are reported regardless.
    /// </param>
    public static WebViewLoadFailureMonitor Attach(Microsoft.Maui.Controls.WebView webView, WebViewErrorView errorView, Func<string?, bool>? ignoreHttpErrorStatusFor = null)
    {
        var monitor = new WebViewLoadFailureMonitor(webView, ignoreHttpErrorStatusFor);

        monitor.FailureOccurred += (_, args) => errorView.ShowFailure(args);
        errorView.RetryRequested += (_, _) => monitor.Retry();
        webView.Navigated += (_, args) =>
        {
            if (args.Result == WebNavigationResult.Success)
            {
                errorView.Hide();
            }
        };

        return monitor;
    }

    /// <summary>
    /// Loads the last requested url again. A url source is re-assigned as a new instance so
    /// platforms that skip identical-value source changes still reload; anything else is
    /// reloaded in place.
    /// </summary>
    public void Retry()
    {
        if (webView.Source is UrlWebViewSource urlSource)
        {
            webView.Source = new UrlWebViewSource { Url = urlSource.Url };

            return;
        }

        webView.Reload();
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Failure)
        {
            FailureOccurred?.Invoke(this, new WebViewLoadFailureEventArgs(e.Url, null));

            return;
        }

        if (e.Result != WebNavigationResult.Success)
        {
            return;
        }

        int statusCode;

        try
        {
            var result = await webView.EvaluateJavaScriptAsync(ResponseStatusProbe);
            statusCode = int.TryParse(result?.Trim(), out var parsed) ? parsed : 0;
        }
        catch
        {
            return;
        }

        if (statusCode >= 400 && ignoreHttpErrorStatusFor?.Invoke(e.Url) != true)
        {
            FailureOccurred?.Invoke(this, new WebViewLoadFailureEventArgs(e.Url, statusCode));
        }
    }
}
