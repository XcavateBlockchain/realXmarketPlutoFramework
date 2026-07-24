#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using AndroidX.Core.App;
using Java.Interop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;

namespace PlutoFramework.Components.Messages;

public partial class X25519WebView
{
    private const string DownloadChannelId = "downloads";

    private static int _downloadNotificationId = 5000;

    // Maps a JS-generated download id to its Android notification id so the
    // "Downloading…" notification can be updated in place to "Download complete".
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _downloadNotifications = new();

    private Android.Webkit.WebView? _nativeWebView;
    private WalletJavascriptInterface? _javascriptInterface;
    private DownloadJavascriptInterface? _downloadInterface;
    private NativeDownloadListener? _downloadListener;
    private ScrollChangedListener? _scrollListener;

    partial void InitializePlatformBridge(WebViewHandler handler)
    {
        if (handler.PlatformView is not Android.Webkit.WebView platformView)
        {
            return;
        }

        _nativeWebView = platformView;

        AttachScrollListener(platformView);

        _javascriptInterface?.Dispose();
        _javascriptInterface = new WalletJavascriptInterface(this);
        platformView.AddJavascriptInterface(_javascriptInterface, ScriptInterfaceName);

        _downloadInterface?.Dispose();
        _downloadInterface = new DownloadJavascriptInterface(this);
        platformView.AddJavascriptInterface(_downloadInterface, DownloadInterfaceName);

        // Fallback for downloads the JS anchor hook can't see (e.g. a full-page
        // navigation to a Content-Disposition: attachment response).
        _downloadListener = new NativeDownloadListener(this);
        platformView.SetDownloadListener(_downloadListener);

        // Ask for POST_NOTIFICATIONS up front (Android 13+) so download progress /
        // completion notifications can actually be shown later.
        _ = EnsureNotificationPermissionAsync();
    }

    private static async Task EnsureNotificationPermissionAsync()
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return;
            }

            var status = await Permissions.CheckStatusAsync<PlutoFramework.Platforms.Android.NotificationPermission>();

            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<PlutoFramework.Platforms.Android.NotificationPermission>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Notification permission request failed: {ex.Message}");
        }
    }

    partial void DisconnectPlatformBridge()
    {
        DetachScrollListener();

        if (_nativeWebView is not null)
        {
            try
            {
                _nativeWebView.SetDownloadListener(null);
                _nativeWebView.RemoveJavascriptInterface(ScriptInterfaceName);
                _nativeWebView.RemoveJavascriptInterface(DownloadInterfaceName);
            }
            catch
            {
                // Ignored — RemoveJavascriptInterface is not available on older APIs
            }
        }

        _javascriptInterface?.Dispose();
        _javascriptInterface = null;
        _downloadInterface?.Dispose();
        _downloadInterface = null;
        _downloadListener?.Dispose();
        _downloadListener = null;
        _nativeWebView = null;
    }

    private void AttachScrollListener(Android.Webkit.WebView platformView)
    {
        DetachScrollListener();

        if (platformView.ViewTreeObserver?.IsAlive != true)
        {
            return;
        }

        var listener = new ScrollChangedListener(this, platformView);
        platformView.ViewTreeObserver.AddOnScrollChangedListener(listener);
        _scrollListener = listener;
    }

    private void DetachScrollListener()
    {
        if (_nativeWebView?.ViewTreeObserver?.IsAlive == true && _scrollListener is not null)
        {
            _nativeWebView.ViewTreeObserver.RemoveOnScrollChangedListener(_scrollListener);
        }

        _scrollListener?.Dispose();
        _scrollListener = null;
    }

    private partial Task DispatchScriptAsync(string script)
    {
        if (_nativeWebView is null)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();

        _nativeWebView.Post(new Java.Lang.Runnable(() =>
        {
            try
            {
                _nativeWebView.EvaluateJavascript(script, null);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));

        return tcs.Task;
    }

    private partial void OnDownloadStarted(string? id, string fileName)
    {
        ShowToast($"Downloading {fileName}…");
        ShowProgressNotification(global::Android.App.Application.Context, ResolveNotificationId(id), fileName);
    }

    private partial void OnDownloadFailed(string? id, string fileName)
    {
        var notificationId = ResolveNotificationId(id);

        if (id is not null)
        {
            _downloadNotifications.TryRemove(id, out _);
        }

        ShowFailedNotification(global::Android.App.Application.Context, notificationId, fileName);
    }

    private static int ResolveNotificationId(string? id)
    {
        if (id is null)
        {
            return System.Threading.Interlocked.Increment(ref _downloadNotificationId);
        }

        return _downloadNotifications.GetOrAdd(id, _ => System.Threading.Interlocked.Increment(ref _downloadNotificationId));
    }

    private partial async Task SaveDownloadedFileAsync(string? id, string fileName, string? mimeType, byte[] data)
    {
        var notificationId = ResolveNotificationId(id);

        try
        {
            var context = global::Android.App.Application.Context;
            var resolver = context.ContentResolver
                ?? throw new InvalidOperationException("ContentResolver unavailable.");

            var mime = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType;

            // API 29+ (our minimum) uses scoped storage via MediaStore — no storage
            // permission required to write into the public Downloads collection.
            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, mime);
            values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryDownloads);
            values.Put(MediaStore.IMediaColumns.IsPending, 1);

            var collection = MediaStore.Downloads.ExternalContentUri
                ?? throw new InvalidOperationException("Downloads collection unavailable.");

            var itemUri = resolver.Insert(collection, values)
                ?? throw new IOException("MediaStore insert returned null.");

            using (var stream = resolver.OpenOutputStream(itemUri)
                ?? throw new IOException("Unable to open output stream."))
            {
                await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            values.Clear();
            values.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(itemUri, values, null, null);

            ShowDownloadNotification(context, notificationId, fileName, mime, itemUri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Android save failed: {ex.Message}");
            ShowFailedNotification(global::Android.App.Application.Context, notificationId, fileName);
        }
        finally
        {
            if (id is not null)
            {
                _downloadNotifications.TryRemove(id, out _);
            }
        }
    }

    // Hands a URL the WebView flagged as a download to Android's DownloadManager.
    private void EnqueueSystemDownload(string url, string? userAgent, string? contentDisposition, string? mimeType)
    {
        try
        {
            var context = global::Android.App.Application.Context;

            if (context.GetSystemService(Context.DownloadService) is not DownloadManager downloadManager)
            {
                return;
            }

            var uri = global::Android.Net.Uri.Parse(url);
            if (uri is null)
            {
                return;
            }

            var request = new DownloadManager.Request(uri);
            var fileName = URLUtil.GuessFileName(url, contentDisposition, mimeType);

            if (!string.IsNullOrEmpty(mimeType))
            {
                request.SetMimeType(mimeType);
            }

            // Forward cookies / UA so authenticated downloads succeed.
            var cookies = CookieManager.Instance?.GetCookie(url);
            if (!string.IsNullOrEmpty(cookies))
            {
                request.AddRequestHeader("cookie", cookies);
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                request.AddRequestHeader("User-Agent", userAgent);
            }

            request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);
            request.SetDestinationInExternalPublicDir(global::Android.OS.Environment.DirectoryDownloads, fileName);

            downloadManager.Enqueue(request);
            ShowToast($"Downloading {fileName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] DownloadManager failed: {ex.Message}");
        }
    }

    private Task DispatchDownloadForUrlAsync(string url, string? fileName)
    {
        var urlLiteral = System.Text.Json.JsonSerializer.Serialize(url);
        var nameLiteral = fileName is null ? "null" : System.Text.Json.JsonSerializer.Serialize(fileName);

        return DispatchScriptSafeAsync(
            $"if (window.__plutoDownloadUrl) {{ window.__plutoDownloadUrl({urlLiteral}, {nameLiteral}); }}");
    }

    private static void EnsureDownloadChannel(NotificationManager manager)
    {
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(DownloadChannelId, "Downloads", NotificationImportance.Default);
            manager.CreateNotificationChannel(channel);
        }
    }

    private static void ShowProgressNotification(Context context, int notificationId, string fileName)
    {
        try
        {
            if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
            {
                return;
            }

            EnsureDownloadChannel(manager);

            if (!manager.AreNotificationsEnabled())
            {
                System.Diagnostics.Debug.WriteLine("[PlutoDownload] Notifications disabled — skipping progress notification.");
                return;
            }

            var notification = new NotificationCompat.Builder(context, DownloadChannelId)
                .SetContentTitle("Downloading")
                .SetContentText(fileName)
                .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
                .SetProgress(0, 0, true)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .Build();

            manager.Notify(notificationId, notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Progress notification failed: {ex.Message}");
        }
    }

    private static void ShowDownloadNotification(Context context, int notificationId, string fileName, string mimeType, global::Android.Net.Uri fileUri)
    {
        try
        {
            if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
            {
                return;
            }

            EnsureDownloadChannel(manager);

            if (!manager.AreNotificationsEnabled())
            {
                System.Diagnostics.Debug.WriteLine("[PlutoDownload] Notifications disabled — falling back to toast.");
                ShowToast($"Downloaded {fileName}");
                return;
            }

            var openIntent = new Intent(Intent.ActionView);
            openIntent.SetDataAndType(fileUri, mimeType);
            openIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

            var pendingFlags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.S)
            {
                pendingFlags |= PendingIntentFlags.Immutable;
            }

            var pendingIntent = PendingIntent.GetActivity(context, notificationId, openIntent, pendingFlags);

            var notification = new NotificationCompat.Builder(context, DownloadChannelId)
                .SetContentTitle("Download complete")
                .SetContentText(fileName)
                .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownloadDone)
                .SetContentIntent(pendingIntent)
                .SetProgress(0, 0, false)
                .SetOngoing(false)
                .SetAutoCancel(true)
                .Build();

            manager.Notify(notificationId, notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Notification failed: {ex.Message}");
            ShowToast($"Downloaded {fileName}");
        }
    }

    private static void ShowFailedNotification(Context context, int notificationId, string fileName)
    {
        try
        {
            if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
            {
                return;
            }

            EnsureDownloadChannel(manager);

            if (!manager.AreNotificationsEnabled())
            {
                ShowToast($"Download failed: {fileName}");
                return;
            }

            var notification = new NotificationCompat.Builder(context, DownloadChannelId)
                .SetContentTitle("Download failed")
                .SetContentText(fileName)
                .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyError)
                .SetProgress(0, 0, false)
                .SetOngoing(false)
                .SetAutoCancel(true)
                .Build();

            manager.Notify(notificationId, notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlutoDownload] Failed notification error: {ex.Message}");
        }
    }

    private static void ShowToast(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var context = global::Android.App.Application.Context;
            global::Android.Widget.Toast.MakeText(context, message, global::Android.Widget.ToastLength.Short)?.Show();
        });
    }

    private sealed class DownloadJavascriptInterface : Java.Lang.Object
    {
        private readonly WeakReference<X25519WebView> _owner;

        public DownloadJavascriptInterface(X25519WebView owner)
        {
            _owner = new(owner);
        }

        [JavascriptInterface]
        [Export("saveFile")]
        public void SaveFile(string json)
        {
            if (_owner.TryGetTarget(out var view))
            {
                view.EnqueueDownloadRequest(json);
            }
        }
    }

    private sealed class NativeDownloadListener : Java.Lang.Object, IDownloadListener
    {
        private readonly WeakReference<X25519WebView> _owner;

        public NativeDownloadListener(X25519WebView owner)
        {
            _owner = new(owner);
        }

        public void OnDownloadStart(string? url, string? userAgent, string? contentDisposition, string? mimetype, long contentLength)
        {
            if (string.IsNullOrEmpty(url) || !_owner.TryGetTarget(out var view))
            {
                return;
            }

            // blob:/data: can't be fetched natively — route back through the JS path.
            if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = URLUtil.GuessFileName(url, contentDisposition, mimetype);
                _ = view.DispatchDownloadForUrlAsync(url, fileName);
                return;
            }

            view.EnqueueSystemDownload(url, userAgent, contentDisposition, mimetype);
        }
    }

    private sealed class WalletJavascriptInterface : Java.Lang.Object
    {
        private readonly WeakReference<X25519WebView> _owner;

        public WalletJavascriptInterface(X25519WebView owner)
        {
            _owner = new(owner);
        }

        [JavascriptInterface]
        [Export("walletCall")]
        public void WalletCall(string json)
        {
            if (_owner.TryGetTarget(out var view))
            {
                view.EnqueueWalletRequest(json);
            }
        }
    }

    private sealed class ScrollChangedListener : Java.Lang.Object, ViewTreeObserver.IOnScrollChangedListener
    {
        private readonly WeakReference<X25519WebView> _owner;
        private readonly WeakReference<Android.Webkit.WebView> _nativeWebView;

        // Required JNI constructor so Android can rehydrate this listener from native handles
        protected ScrollChangedListener(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public ScrollChangedListener(X25519WebView owner, Android.Webkit.WebView nativeWebView)
        {
            _owner = new(owner);
            _nativeWebView = new(nativeWebView);
        }

        public void OnScrollChanged()
        {
            if (_owner is not null && _owner.TryGetTarget(out var owner) && _nativeWebView.TryGetTarget(out var native))
            {
                owner.RaiseScrolled(native.ScrollX, native.ScrollY);
            }
        }
    }
}
#endif