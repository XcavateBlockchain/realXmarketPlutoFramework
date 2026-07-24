using Microsoft.Maui.Handlers;
using PlutoFramework.Components.WebView;
using PlutoFramework.Model;
using PlutoFrameworkCore.AssetDidComm;
using System.Diagnostics;
using System.Text.Json;

namespace PlutoFramework.Components.Messages;

/// <summary>
/// WebView that hosts the Asset DIDComm dashboard and automatically
/// injects the user's X25519 secret key (JWK) so decryption works
/// without manual key entry.
/// </summary>
public partial class X25519WebView : Microsoft.Maui.Controls.WebView
{
    private const string ScriptInterfaceName = "mauiWallet";
    private const string DownloadInterfaceName = "mauiDownloads";
    private const string HeaderInterfaceName = "mauiHeader";

    private uint? tabId = null;

    private readonly PolkadotExtensionWalletBridge _walletBridge = new();

    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(X25519WebView),
            defaultValue: "https://realxmessage.xcavate.io/messages/my-buckets/?isHeaderVisible=false",
            propertyChanged: OnUrlChanged);

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public X25519WebView()
    {
        Source = new UrlWebViewSource { Url = Url };
        Navigated += OnNavigated;
    }

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

    private static void OnUrlChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is X25519WebView webView && newValue is string url)
        {
            webView.Source = new UrlWebViewSource { Url = url };
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
            return;

        _ = InjectProviderAsync();
        _ = InjectX25519KeyAsync();
        _ = InjectDownloadInterceptorAsync();
        _ = InjectHeaderBridgeAsync();
        _ = InjectTapHighlightStyleAsync();
    }

    /// <summary>
    /// Removes the translucent blue rectangle the WebView draws over a tapped
    /// element. Both the Android WebView and the iOS WKWebView honour the
    /// <c>-webkit-tap-highlight-color</c> CSS property, so setting it to
    /// transparent on every element suppresses the highlight on both platforms.
    /// </summary>
    private Task InjectTapHighlightStyleAsync()
        => DispatchScriptSafeAsync(@"(function () {
    if (window.__plutoTapHighlightInstalled) { return; }
    window.__plutoTapHighlightInstalled = true;
    var style = document.createElement('style');
    style.textContent = '* { -webkit-tap-highlight-color: transparent !important; }';
    (document.head || document.documentElement).appendChild(style);
})();");

    /// <summary>
    /// Raised whenever the hosted page's header (title + action buttons) changes,
    /// including on in-page SPA navigation. Always dispatched on the main thread so
    /// subscribers can safely update UI. See <see cref="BuildHeaderBridgeScript"/>.
    /// </summary>
    internal event EventHandler<WebPageHeader>? HeaderChanged;

    private Task InjectHeaderBridgeAsync()
        => DispatchScriptSafeAsync(BuildHeaderBridgeScript());

    /// <summary>
    /// Called from the platform bridge (Android JavascriptInterface / iOS
    /// WKScriptMessageHandler) when the injected header script reports the current
    /// page header as JSON.
    /// </summary>
    internal void EnqueueHeaderUpdate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var header = TryParseHeader(json);

        if (header is null)
        {
            return;
        }

        // The bridge callback arrives off the UI thread on Android — marshal so
        // subscribers can touch the native navigation bar directly.
        MainThread.BeginInvokeOnMainThread(() => HeaderChanged?.Invoke(this, header));
    }

    /// <summary>
    /// Forwards a tap on a mirrored navigation-bar icon back to the corresponding
    /// action button inside the web header.
    /// </summary>
    internal Task InvokeHeaderActionAsync(int index)
        => DispatchScriptSafeAsync($"if (window.__plutoHeaderClick) {{ window.__plutoHeaderClick({index}); }}");

    private static WebPageHeader? TryParseHeader(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var present = root.TryGetProperty("present", out var presentElement)
                && presentElement.ValueKind == JsonValueKind.True;

            var title = root.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString() ?? string.Empty
                : string.Empty;

            var subtitle = root.TryGetProperty("subtitle", out var subtitleElement)
                ? subtitleElement.GetString()
                : null;

            var buttons = new List<string>();

            if (root.TryGetProperty("buttons", out var buttonsElement)
                && buttonsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in buttonsElement.EnumerateArray())
                {
                    var label = item.GetString();

                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        buttons.Add(label.Trim());
                    }
                }
            }

            return new WebPageHeader(present, title, subtitle, buttons);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlutoHeader] Failed to parse header payload: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// JavaScript that mirrors the page-level header (see the Vue <c>page-header</c>
    /// component: a <c>.page-header__title</c> plus a <c>.page-header__actions</c>
    /// group) into the native navigation bar. It hides the in-page header and reclaims
    /// the space the hosted app reserves for the topbar it is told not to render, reports
    /// the title and the visible text of each action button to native, re-reports on
    /// SPA navigation via a MutationObserver, and exposes <c>__plutoHeaderClick</c>
    /// so a native icon tap triggers the matching web button. The transport is
    /// resolved at call time so the same script works over the Android
    /// JavascriptInterface and the iOS WKScriptMessageHandler.
    /// </summary>
    private static string BuildHeaderBridgeScript()
    {
        return @"(function () {
    if (window.__plutoHeaderBridgeInstalled) { return; }
    window.__plutoHeaderBridgeInstalled = true;

    // Hide the in-page header — its title and actions are mirrored into the native
    // TopNavigationBar. It stays in the DOM so we can still read it and forward taps
    // to its buttons.
    //
    // The page is also loaded with ?isHeaderVisible=false, so the hosted app drops its
    // own 56px mobile topbar and flags the shell with .header-hidden. That flag only
    // clears the shell's own padding though — the per-page rules still reserve the
    // topbar's height, which now shows up as a blank strip: the in-bucket chat keeps
    // 'padding-top: 56px' (gap above the content) and the other full-height pages size
    // themselves to 'calc(100vh - 56px)' (gap below it). Reclaim both. Scoping to
    // .header-hidden leaves the originals in force whenever the topbar is rendered, and
    // the overrides say 'fill the viewport' rather than restating 56px, so they survive
    // the hosted app changing its topbar height.
    var style = document.createElement('style');
    style.textContent = [
        '.page-header { display: none !important; }',
        '@media (max-width: 960px) {',
        '.app-shell-content.header-hidden:has(.chat-page-container.ib-custom-page) { padding-top: 0 !important; }',
        '.app-shell-content.header-hidden .chat-custom-page { height: 100vh !important; }',
        '}'
    ].join('');
    (document.head || document.documentElement).appendChild(style);

    function postToNative(payload) {
        var json = JSON.stringify(payload);
        try {
            if (window.mauiHeader && window.mauiHeader.updateHeader) {
                window.mauiHeader.updateHeader(json);
            } else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.mauiHeader) {
                window.webkit.messageHandlers.mauiHeader.postMessage(json);
            }
        } catch (err) {
            console.error('Pluto header post failed', err);
        }
    }

    function actionButtons() {
        var header = document.querySelector('.page-header');
        if (!header) { return []; }
        var actions = header.querySelector('.page-header__actions');
        if (!actions) { return []; }
        var nodes = actions.querySelectorAll('button, a, .btn');
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            if (result.indexOf(nodes[i]) === -1) { result.push(nodes[i]); }
        }
        return result;
    }

    function labelFor(el) {
        var text = (el.textContent || '').trim();
        if (text) { return text; }
        return el.getAttribute('aria-label') || el.getAttribute('title') || '';
    }

    // Invoked from native when a mirrored button is tapped in the nav bar.
    window.__plutoHeaderClick = function (index) {
        var buttons = actionButtons();
        if (index >= 0 && index < buttons.length) {
            buttons[index].click();
        }
    };

    var lastJson = null;

    function extract() {
        var header = document.querySelector('.page-header');
        var payload;
        if (!header) {
            payload = { present: false, title: '', subtitle: '', buttons: [] };
        } else {
            var titleEl = header.querySelector('.page-header__title');
            var subtitleEl = header.querySelector('.page-header__subtitle');
            var buttons = actionButtons().map(labelFor).filter(function (t) { return t.length > 0; });
            payload = {
                present: true,
                title: titleEl ? (titleEl.textContent || '').trim() : '',
                subtitle: subtitleEl ? (subtitleEl.textContent || '').trim() : '',
                buttons: buttons
            };
        }

        var json = JSON.stringify(payload);
        if (json !== lastJson) {
            lastJson = json;
            postToNative(payload);
        }
    }

    var scheduled = false;
    function scheduleExtract() {
        if (scheduled) { return; }
        scheduled = true;
        setTimeout(function () { scheduled = false; extract(); }, 60);
    }

    var observer = new MutationObserver(scheduleExtract);
    observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true });

    extract();
})();";
    }

    internal void EnqueueWalletRequest(string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return;
        }

        _ = ProcessWalletRequestAsync(requestJson);
    }

    private async Task ProcessWalletRequestAsync(string requestJson)
    {
        try
        {
            var responseJson = await _walletBridge.HandleAsync(requestJson).ConfigureAwait(false);
            await DispatchScriptSafeAsync($"window.__mauiWalletDeliver({responseJson});").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlutoWallet] Failed to process wallet request: {ex.Message}");

            var fallback = new
            {
                id = TryExtractId(requestJson),
                error = ex.Message
            };

            var fallbackJson = JsonSerializer.Serialize(fallback, PolkadotExtensionWalletBridge.SerializerOptions);
            await DispatchScriptSafeAsync($"window.__mauiWalletDeliver({fallbackJson});").ConfigureAwait(false);
        }
    }

    private Task InjectDownloadInterceptorAsync()
        => DispatchScriptSafeAsync(BuildDownloadInterceptorScript());

    /// <summary>
    /// Called from the platform bridge when the injected interceptor has read a
    /// file (blob:, data: or same-origin https:) into base64 and handed it back.
    /// </summary>
    internal void EnqueueDownloadRequest(string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return;
        }

        _ = ProcessDownloadRequestAsync(requestJson);
    }

    private async Task ProcessDownloadRequestAsync(string requestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : "complete";

            var id = root.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            var suggestedName = root.TryGetProperty("filename", out var nameElement)
                ? nameElement.GetString()
                : null;

            var mimeType = root.TryGetProperty("mime", out var mimeElement)
                ? mimeElement.GetString()
                : null;

            var fileName = SanitizeFileName(suggestedName, mimeType);

            switch (type)
            {
                case "start":
                    OnDownloadStarted(id, fileName);
                    return;

                case "error":
                    OnDownloadFailed(id, fileName);
                    return;

                default:
                    var base64 = root.TryGetProperty("base64", out var base64Element)
                        ? base64Element.GetString()
                        : null;

                    if (string.IsNullOrEmpty(base64))
                    {
                        return;
                    }

                    var bytes = Convert.FromBase64String(base64);
                    await SaveDownloadedFileAsync(id, fileName, mimeType, bytes).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlutoDownload] Failed to process download: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string? suggestedName, string? mimeType)
    {
        var name = suggestedName?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            // Keep only the final path segment and strip characters invalid on disk.
            name = name.Replace('\\', '/');
            name = name[(name.LastIndexOf('/') + 1)..];

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"download_{DateTime.Now:yyyyMMdd_HHmmss}{GuessExtension(mimeType)}";
        }

        return name;
    }

    private static string GuessExtension(string? mimeType)
    {
        return mimeType?.Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "application/json" => ".json",
            "application/zip" => ".zip",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => string.Empty
        };
    }

    /// <summary>
    /// JavaScript that intercepts download triggers and streams the bytes back to
    /// native. Handles blob:, data: and same-origin https: uniformly by fetching
    /// the URL into a Blob and base64-encoding it. Only acts on genuine downloads
    /// (an &lt;a download&gt; element or a blob:/data: href) so normal navigation is
    /// left untouched. The transport is resolved at call time so the same script
    /// works over the Android JavascriptInterface and the iOS WKScriptMessageHandler.
    /// </summary>
    private static string BuildDownloadInterceptorScript()
    {
        return @"(function () {
    if (window.__plutoDownloadInterceptorInstalled) { return; }
    window.__plutoDownloadInterceptorInstalled = true;

    function postToNative(payload) {
        var json = JSON.stringify(payload);
        try {
            if (window.mauiDownloads && window.mauiDownloads.saveFile) {
                window.mauiDownloads.saveFile(json);
            } else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.mauiDownloads) {
                window.webkit.messageHandlers.mauiDownloads.postMessage(json);
            } else {
                console.warn('Pluto native download bridge is unavailable.');
            }
        } catch (err) {
            console.error('Pluto download post failed', err);
        }
    }

    function deriveName(url, suggested) {
        if (suggested) { return suggested; }
        try {
            var parsed = new URL(url, window.location.href);
            var last = parsed.pathname.split('/').filter(Boolean).pop();
            if (last) { return decodeURIComponent(last); }
        } catch (err) { }
        return 'download';
    }

    function saveUrl(url, suggestedName) {
        if (!url) { return; }
        var id = 'dl-' + Date.now() + '-' + Math.random().toString(16).slice(2);
        var name = deriveName(url, suggestedName);
        postToNative({ type: 'start', id: id, filename: name });
        fetch(url).then(function (response) {
            return response.blob();
        }).then(function (blob) {
            return new Promise(function (resolve, reject) {
                var reader = new FileReader();
                reader.onloadend = function () {
                    var result = reader.result || '';
                    var comma = result.indexOf(',');
                    resolve({ base64: comma >= 0 ? result.slice(comma + 1) : result, type: blob.type });
                };
                reader.onerror = function () { reject(reader.error); };
                reader.readAsDataURL(blob);
            });
        }).then(function (data) {
            postToNative({ type: 'complete', id: id, filename: name, mime: data.type, base64: data.base64 });
        }).catch(function (err) {
            postToNative({ type: 'error', id: id, filename: name });
            console.error('Pluto download failed for ' + url, err);
        });
    }

    // Exposed so the native side can route a download it detected (e.g. Android's
    // DownloadListener firing on a blob: URL) back through the same fetch path.
    window.__plutoDownloadUrl = saveUrl;

    function isDownloadAnchor(anchor) {
        if (!anchor || anchor.tagName !== 'A') { return false; }
        if (anchor.hasAttribute('download')) { return true; }
        var href = anchor.getAttribute('href') || '';
        return href.indexOf('blob:') === 0 || href.indexOf('data:') === 0;
    }

    function handleAnchor(anchor) {
        var name = anchor.getAttribute('download');
        saveUrl(anchor.href, name && name.length ? name : null);
    }

    // Catches anchors that live in the DOM.
    document.addEventListener('click', function (event) {
        var node = event.target;
        while (node && node !== document) {
            if (node.tagName === 'A' && isDownloadAnchor(node)) {
                event.preventDefault();
                event.stopPropagation();
                handleAnchor(node);
                return;
            }
            node = node.parentNode;
        }
    }, true);

    // Catches detached anchors: createElement('a'); a.download = ...; a.click();
    var originalClick = HTMLAnchorElement.prototype.click;
    HTMLAnchorElement.prototype.click = function () {
        if (isDownloadAnchor(this)) {
            handleAnchor(this);
            return;
        }
        return originalClick.apply(this, arguments);
    };

    // Catches window.open('blob:...') / window.open('data:...').
    var originalOpen = window.open;
    window.open = function (url) {
        if (typeof url === 'string' && (url.indexOf('blob:') === 0 || url.indexOf('data:') === 0)) {
            saveUrl(url, null);
            return null;
        }
        return originalOpen.apply(this, arguments);
    };
})();";
    }

    private Task InjectProviderAsync()
    {
        if (tabId is null)
        {
            tabId = ExtensionWebViewModel.GetNextTabId();
        }

        ExtensionWebViewModel.TabInfos[tabId.Value] = GetDAppInfo();

        return DispatchScriptSafeAsync(BuildProviderInjectionScript());
    }

    private Task DispatchScriptSafeAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script) || Handler is null)
        {
            return Task.CompletedTask;
        }

        return DispatchScriptAsync(script);
    }

    private static string? TryExtractId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var idProperty))
            {
                return idProperty.GetString();
            }
        }
        catch
        {
            // ignored on purpose
        }

        return null;
    }

    private string BuildProviderInjectionScript()
    {
        return @$"(function () {{
    if (window.__plutoWalletInjected) {{ return; }}
    const channel = '{ScriptInterfaceName}';
    if (typeof window === 'undefined' || !window[channel] || !window[channel].walletCall) {{
        console.warn('Pluto wallet bridge is unavailable on this platform.');
        return;
    }}
    window.__plutoWalletInjected = true;
    const pending = {{}};
    window.__mauiWalletDeliver = function (message) {{
        try {{
            var payload = (typeof message === 'string') ? JSON.parse(message) : message;
            if (!payload || !payload.id || !pending[payload.id]) {{ return; }}
            var entry = pending[payload.id];
            delete pending[payload.id];
            if (payload.error) {{
                entry.reject(payload.error);
            }} else {{
                entry.resolve(payload.result);
            }}
        }} catch (err) {{
            console.error('Wallet deliver failure', err);
        }}
    }};
    function send(method, payload) {{
        return new Promise(function (resolve, reject) {{
            const id = `${{Date.now()}}-${{Math.random().toString(16).slice(2)}}`;
            pending[id] = {{ resolve: resolve, reject: reject }};
            try {{
                window[channel].walletCall(JSON.stringify({{ id: id, method: method, payload: payload }}));
            }} catch (err) {{
                delete pending[id];
                reject(err);
            }}
        }});
    }}
    const providerName = '{PolkadotExtensionWalletBridge.ProviderName}';
    window.injectedWeb3 = window.injectedWeb3 || {{}};
    if (window.injectedWeb3[providerName]) {{
        console.warn('Wallet provider already exists.');
        return;
    }}
    window.injectedWeb3[providerName] = {{
        name: '{AppInfo.Name}',
        version: '{AppInfo.VersionString}',
        enable: function (origin) {{
            return send('enable', {{ origin: origin, tabId: {tabId} }}).then(function () {{
                return {{
                    accounts: {{
                        get: function () {{ return send('accounts.get'); }},
                        subscribe: function (cb) {{
                            var active = true;
                            function emit() {{
                                send('accounts.get').then(function (accounts) {{
                                    if (active) {{ cb(accounts); }}
                                }}).catch(function (error) {{
                                    console.error('Wallet accounts.subscribe error', error);
                                }});
                            }}
                            emit();
                            return function () {{ active = false; }};
                        }}
                    }},
                    signer: {{
                        signRaw: function (raw) {{ return send('signRaw', raw); }},
                        signPayload: function (payloadJson) {{ return send('signPayload', payloadJson); }}
                    }}
                }};
            }});
        }}
    }};
}})();";
    }

    private DAppInfo GetDAppInfo()
    {
        var url = GetCurrentUrl() ?? string.Empty;
        var title = GetCurrentTitle();
        var icon = BuildFaviconSource(url);

        return new DAppInfo
        {
            Icon = icon,
            Name = string.IsNullOrWhiteSpace(title) ? (TryGetHost(url) ?? string.Empty) : title,
            Url = url
        };
    }

    private string? GetCurrentUrl()
    {
#if ANDROID
        if (!string.IsNullOrWhiteSpace(_nativeWebView?.Url))
        {
            return _nativeWebView.Url;
        }
#elif IOS || MACCATALYST
        if (_nativeWebView?.Url is not null)
        {
            return _nativeWebView.Url.ToString();
        }
#endif

        if (Source is UrlWebViewSource urlSource)
        {
            return urlSource.Url;
        }

        return null;
    }

    private string? GetCurrentTitle()
    {
#if ANDROID
        if (!string.IsNullOrWhiteSpace(_nativeWebView?.Title))
        {
            return _nativeWebView.Title;
        }
#elif IOS || MACCATALYST
        if (!string.IsNullOrWhiteSpace(_nativeWebView?.Title))
        {
            return _nativeWebView.Title;
        }
#endif

        return null;
    }

    private static ImageSource BuildFaviconSource(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            var faviconUri = new Uri($"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(host)}&sz=256");
            return ImageSource.FromUri(faviconUri);
        }

        return ImageSource.FromStream(() => Stream.Null);
    }

    private static string? TryGetHost(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : null;
    }

    partial void InitializePlatformBridge(WebViewHandler handler);

    partial void DisconnectPlatformBridge();

    private partial Task DispatchScriptAsync(string script);

    private partial void OnDownloadStarted(string? id, string fileName);

    private partial void OnDownloadFailed(string? id, string fileName);

    private partial Task SaveDownloadedFileAsync(string? id, string fileName, string? mimeType, byte[] data);

    internal void RaiseScrolled(double x, double y)
    {
        // No scroll event exposed on this control, but kept for platform consistency
    }

    private async Task InjectX25519KeyAsync()
    {
        try
        {
            var encryptionKey = await KeysModel.GetX25519KeyNoAuthAsync();

            if (encryptionKey?.SecretKey is null or { Length: not 32 })
            {
                System.Diagnostics.Debug.WriteLine("X25519 key injection skipped: no valid key found.");
                return;
            }

            var publicKey = X25519Model.DerivePublicKey(encryptionKey.SecretKey);

            var jwkJson = BuildX25519Jwk(encryptionKey.SecretKey, publicKey);
            var jsLiteral = JsonSerializer.Serialize(jwkJson);

            // Covers both "bridge already installed" and "app still booting".
            await EvaluateJavaScriptAsync(
                $"window.assetDidComm ? window.assetDidComm.injectX25519Key({jsLiteral}, {{ persist: false }}) " +
                $": (window.__assetDidCommPendingX25519Key = {jsLiteral})");

            System.Diagnostics.Debug.WriteLine("X25519 key injected into dashboard.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"X25519 key injection failed: {ex.Message}");
        }
    }

    private static string BuildX25519Jwk(byte[] secretKey, byte[] publicKey)
    {
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "OKP",
            ["crv"] = "X25519",
            ["d"] = Base64UrlEncode(secretKey),
            ["x"] = Base64UrlEncode(publicKey)
        };

        return JsonSerializer.Serialize(jwk);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// A snapshot of the hosted web page's header, mirrored from the in-page
/// <c>page-header</c> component into the native navigation bar.
/// </summary>
/// <param name="Present">Whether the current page renders a header at all.</param>
/// <param name="Title">The header title text.</param>
/// <param name="Subtitle">The optional header subtitle text.</param>
/// <param name="Buttons">The visible labels of the header's action buttons, in DOM order.</param>
public sealed record WebPageHeader(bool Present, string Title, string? Subtitle, IReadOnlyList<string> Buttons);