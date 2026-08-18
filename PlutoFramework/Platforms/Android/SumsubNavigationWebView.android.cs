#if ANDROID
using Android.Webkit;
using Java.Interop;
using Microsoft.Maui.Handlers;

namespace PlutoFramework.Components.Sumsub;

public partial class SumsubNavigationWebView
{
    private Android.Webkit.WebView? nativeWebView;
    private NavigationJavascriptInterface? javascriptInterface;

    partial void InitializePlatformBridge(WebViewHandler handler)
    {
        if (handler.PlatformView is not Android.Webkit.WebView platformView)
        {
            return;
        }

        nativeWebView = platformView;

        javascriptInterface?.Dispose();
        javascriptInterface = new NavigationJavascriptInterface(this);
        platformView.AddJavascriptInterface(javascriptInterface, ScriptInterfaceName);
    }

    partial void DisconnectPlatformBridge()
    {
        if (nativeWebView is not null)
        {
            try
            {
                nativeWebView.RemoveJavascriptInterface(ScriptInterfaceName);
            }
            catch
            {
            }
        }

        javascriptInterface?.Dispose();
        javascriptInterface = null;
        nativeWebView = null;
    }

    private sealed class NavigationJavascriptInterface : Java.Lang.Object
    {
        private readonly WeakReference<SumsubNavigationWebView> owner;

        public NavigationJavascriptInterface(SumsubNavigationWebView owner)
        {
            this.owner = new(owner);
        }

        [JavascriptInterface]
        [Export("navigateToNextPage")]
        public void NavigateToNextPage()
        {
            if (owner.TryGetTarget(out var view))
            {
                view.RequestNavigateToNextPage();
            }
        }
    }
}
#endif