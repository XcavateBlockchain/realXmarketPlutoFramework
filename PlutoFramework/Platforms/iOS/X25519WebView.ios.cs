#if IOS || MACCATALYST
using System;
using System.Threading.Tasks;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
using WebKit;

namespace PlutoFramework.Components.Messages;

public partial class X25519WebView
{
    private WKWebView? _nativeWebView;
    private WalletMessageHandler? _messageHandler;
    private DownloadMessageHandler? _downloadMessageHandler;
    private EventHandler? _scrolledHandler;

    partial void InitializePlatformBridge(WebViewHandler handler)
    {
        if (handler.PlatformView is not WKWebView platformView)
        {
            return;
        }

        _nativeWebView = platformView;

        AttachScrollHandler(platformView);

        _messageHandler?.Dispose();
        _messageHandler = new WalletMessageHandler(this);
        platformView.Configuration.UserContentController.AddScriptMessageHandler(_messageHandler, ScriptInterfaceName);

        _downloadMessageHandler?.Dispose();
        _downloadMessageHandler = new DownloadMessageHandler(this);
        platformView.Configuration.UserContentController.AddScriptMessageHandler(_downloadMessageHandler, DownloadInterfaceName);
    }

    partial void DisconnectPlatformBridge()
    {
        if (_nativeWebView is null)
        {
            return;
        }

        DetachScrollHandler();

        if (_messageHandler is not null)
        {
            _nativeWebView.Configuration.UserContentController.RemoveScriptMessageHandler(ScriptInterfaceName);
            _messageHandler.Dispose();
            _messageHandler = null;
        }

        if (_downloadMessageHandler is not null)
        {
            _nativeWebView.Configuration.UserContentController.RemoveScriptMessageHandler(DownloadInterfaceName);
            _downloadMessageHandler.Dispose();
            _downloadMessageHandler = null;
        }

        _nativeWebView = null;
    }

    private void AttachScrollHandler(WKWebView platformView)
    {
        DetachScrollHandler();

        var scrollView = platformView.ScrollView;
        if (scrollView is null)
        {
            return;
        }

        _scrolledHandler = (sender, args) =>
        {
            var offset = scrollView.ContentOffset;
            RaiseScrolled(offset.X, offset.Y);
        };

        scrollView.Scrolled += _scrolledHandler;
    }

    private void DetachScrollHandler()
    {
        if (_nativeWebView?.ScrollView is not null && _scrolledHandler is not null)
        {
            _nativeWebView.ScrollView.Scrolled -= _scrolledHandler;
        }

        _scrolledHandler = null;
    }

    private partial Task DispatchScriptAsync(string script)
    {
        if (_nativeWebView is null)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _nativeWebView.EvaluateJavaScript(script, (result, error) =>
            {
                if (error is not null)
                {
                    tcs.TrySetException(new NSErrorException(error));
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            });
        });

        return tcs.Task;
    }

    private partial async Task SaveDownloadedFileAsync(string fileName, string? mimeType, byte[] data)
    {
        try
        {
            // iOS has no shared public "Downloads" folder for third-party apps. The
            // Documents directory is the closest equivalent — it is browsable in the
            // Files app when file sharing is enabled in Info.plist.
            var documents = NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User, true).FirstOrDefault()
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var targetPath = EnsureUniquePath(Path.Combine(documents, fileName));

            await File.WriteAllBytesAsync(targetPath, data).ConfigureAwait(false);

            await ShowSavedFeedbackAsync(fileName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] iOS save failed: {ex.Message}");
        }
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static Task ShowSavedFeedbackAsync(string fileName)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var toast = CommunityToolkit.Maui.Alerts.Toast.Make($"Saved to Files: {fileName}");
                await toast.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Toast failed: {ex.Message}");
            }
        });
    }

    private sealed class DownloadMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly WeakReference<X25519WebView> _owner;

        public DownloadMessageHandler(X25519WebView owner)
        {
            _owner = new(owner);
        }

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            if (!string.Equals(message.Name, DownloadInterfaceName, StringComparison.Ordinal))
            {
                return;
            }

            var raw = message.Body?.ToString();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (_owner.TryGetTarget(out var view))
            {
                view.EnqueueDownloadRequest(raw);
            }
        }
    }

    private sealed class WalletMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly WeakReference<X25519WebView> _owner;

        public WalletMessageHandler(X25519WebView owner)
        {
            _owner = new(owner);
        }

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            if (!string.Equals(message.Name, ScriptInterfaceName, StringComparison.Ordinal))
            {
                return;
            }

            var raw = message.Body?.ToString();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            if (_owner.TryGetTarget(out var view))
            {
                view.EnqueueWalletRequest(raw);
            }
        }
    }
}
#endif