namespace PlutoFramework.Components.WebView;

public partial class WebViewErrorView : ContentView
{
    /// <summary>
    /// Raised when the user taps the Retry button.
    /// </summary>
    public event EventHandler? RetryRequested;

    public WebViewErrorView()
    {
        InitializeComponent();

        retryButton.Clicked += (_, __) => RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows the error page with the title and message that fit the kind of failure.
    /// </summary>
    public void ShowFailure(WebViewLoadFailureEventArgs failure)
    {
        if (failure.IsNotFound)
        {
            titleLabel.Text = "Page Not Found";
            messageLabel.Text = "The requested page could not be found.";
        }
        else if (failure.HttpStatusCode is { } statusCode)
        {
            titleLabel.Text = "Page Unavailable";
            messageLabel.Text = $"The website returned an error (HTTP {statusCode}). Please try again later.";
        }
        else
        {
            titleLabel.Text = "Page Unavailable";
            messageLabel.Text = "The website could not be loaded. Check your internet connection and try again.";
        }

        IsVisible = true;
    }

    /// <summary>
    /// Hides the error page.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
    }
}
