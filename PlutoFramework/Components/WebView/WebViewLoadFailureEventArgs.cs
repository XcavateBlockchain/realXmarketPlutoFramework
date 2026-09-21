namespace PlutoFramework.Components.WebView;

/// <summary>
/// Describes why a hosted web page failed to load.
/// </summary>
public sealed class WebViewLoadFailureEventArgs : EventArgs
{
    public WebViewLoadFailureEventArgs(string? url, int? httpStatusCode)
    {
        Url = url;
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>
    /// The url that failed, when known.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// The http status code when the server answered with an error (404, 5xx, ...),
    /// otherwise null - a null status means the page never reached the server.
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// True when the server answered 404 not found.
    /// </summary>
    public bool IsNotFound => HttpStatusCode == 404;
}
