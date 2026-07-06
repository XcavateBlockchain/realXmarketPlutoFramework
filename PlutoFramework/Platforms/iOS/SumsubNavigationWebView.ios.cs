#if IOS || MACCATALYST
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;

namespace PlutoFramework.Components.Sumsub;

public partial class SumsubNavigationWebView
{
    private WKWebView? nativeWebView;
    private NavigationMessageHandler? messageHandler;

    partial void InitializePlatformBridge(WebViewHandler handler)
    {
        if (handler.PlatformView is not WKWebView platformView)
        {
            return;
        }

        nativeWebView = platformView;

        messageHandler?.Dispose();
        messageHandler = new NavigationMessageHandler(this);
        platformView.Configuration.UserContentController.AddScriptMessageHandler(messageHandler, ScriptInterfaceName);
    }

    partial void DisconnectPlatformBridge()
    {
        if (nativeWebView is null)
        {
            return;
        }

        if (messageHandler is not null)
        {
            nativeWebView.Configuration.UserContentController.RemoveScriptMessageHandler(ScriptInterfaceName);
            messageHandler.Dispose();
            messageHandler = null;
        }

        nativeWebView = null;
    }

    private sealed class NavigationMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly WeakReference<SumsubNavigationWebView> owner;

        public NavigationMessageHandler(SumsubNavigationWebView owner)
        {
            this.owner = new(owner);
        }

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            if (!string.Equals(message.Name, ScriptInterfaceName, StringComparison.Ordinal))
            {
                return;
            }

            if (owner.TryGetTarget(out var view))
            {
                view.RequestNavigateToNextPage();
            }
        }
    }
}
#endif