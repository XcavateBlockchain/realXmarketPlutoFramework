using Microsoft.Maui.Handlers;

namespace PlutoFramework.Components.Sumsub;

public partial class SumsubNavigationWebView : Microsoft.Maui.Controls.WebView
{
    private const string ScriptInterfaceName = "sumsubNavigation";

    public event EventHandler? NextPageRequested;

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is WebViewHandler handler)
        {
            InitializePlatformBridge(handler);
        }
        else
        {
            DisconnectPlatformBridge();
        }
    }

    internal void RequestNavigateToNextPage()
    {
        MainThread.BeginInvokeOnMainThread(() => NextPageRequested?.Invoke(this, EventArgs.Empty));
    }

    partial void InitializePlatformBridge(WebViewHandler handler);

    partial void DisconnectPlatformBridge();
}