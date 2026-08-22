using Microsoft.Maui.Handlers;
using PlutoFramework.Components.WebView;
using PlutoFramework.Model;
using PlutoFrameworkCore;
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

    private readonly PolkadotExtensionWalletBridge _walletBridge;

    private readonly SolanaWalletStandardBridge _solanaBridge;

    /// <summary>
    /// Whether the hosted page is one of the configured trusted dapps, refreshed on every
    /// completed navigation. Gates the bridges' Profile API auto-signing: the messenger
    /// dashboard signs a Profile API payload for every call it makes, but a page the user
    /// navigated away to gets the ordinary signing sheet again.
    /// </summary>
    private volatile bool _hostIsWhitelistedDApp;

    /// <summary>
    /// The wallet icon as a data URI, read once from the packaged asset. Wallet Standard
    /// requires a data URI, and the asset never changes during a run.
    /// </summary>
    private static string? _walletIconDataUri;

    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(X25519WebView),
            defaultValue: "https://realxmessenger.xcavate.io/messages/my-buckets/?isHeaderVisible=false&primaryColor=%233B4F74",
            propertyChanged: OnUrlChanged);

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public X25519WebView()
    {
        // Auto-signing is this view's decision rather than the bridges' default: only here
        // is the hosted page meant to be the whitelisted messenger dashboard.
        _walletBridge = new() { AllowProfileApiAutoSign = () => _hostIsWhitelistedDApp };
        _solanaBridge = new() { AllowProfileApiAutoSign = () => _hostIsWhitelistedDApp };

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

    /// <summary>
    /// Whether the hosted page has an earlier entry to return to, read from the native web
    /// view instead of from <see cref="Microsoft.Maui.Controls.WebView.CanGoBack"/>.
    /// </summary>
    /// <remarks>
    /// MAUI only refreshes its own CanGoBack while handling a cross-document navigation.
    /// The messenger dashboard is a client-routed SPA, so moving between its screens is a
    /// history.pushState call, and WebKit reports those only through the private
    /// same-document navigation callback that MAUI does not implement - leaving CanGoBack
    /// stuck at whatever the last real page load set it to on iOS. Android's WebViewClient
    /// raises OnPageFinished for same-document navigations too, which is why the cached
    /// value happens to keep up there. The native back-forward list is current on both.
    ///
    /// <see cref="Microsoft.Maui.Controls.WebView.GoBack"/> needs no equivalent treatment:
    /// its handler consults the native list before it moves.
    /// </remarks>
    internal bool CanGoBackInPage
    {
        get
        {
#if ANDROID
            if (_nativeWebView is not null)
            {
                return _nativeWebView.CanGoBack();
            }
#elif IOS || MACCATALYST
            if (_nativeWebView is not null)
            {
                return _nativeWebView.CanGoBack;
            }
#endif

            return CanGoBack;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
            return;

        // Evaluated here, on the UI thread, because reading the native WebView's URL from
        // the bridges' background callbacks would not be safe on Android.
        _hostIsWhitelistedDApp = IsWhitelistedDAppHost(GetCurrentUrl());

        // Registered before the injections rather than inside one of them: both wallets
        // report the same tab, and they are dispatched concurrently.
        RegisterTab();

        _ = InjectProviderAsync();
        _ = InjectSolanaWalletAsync();
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

    /// <summary>
    /// Both injected wallets share one channel, so the method name decides which bridge
    /// answers and which reply function the page is waiting on. The two keep separate
    /// delivery globals rather than chaining onto one, which would depend on the order the
    /// concurrently dispatched injections happen to land in.
    /// </summary>
    private async Task ProcessWalletRequestAsync(string requestJson)
    {
        var isSolana = SolanaWalletStandardBridge.Handles(TryExtractMethod(requestJson));

        var deliver = isSolana ? "window.__plutoSolanaDeliver" : "window.__mauiWalletDeliver";

        try
        {
            var responseJson = isSolana
                ? await _solanaBridge.HandleAsync(requestJson).ConfigureAwait(false)
                : await _walletBridge.HandleAsync(requestJson).ConfigureAwait(false);

            await DispatchScriptSafeAsync($"{deliver}({responseJson});").ConfigureAwait(false);
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
            await DispatchScriptSafeAsync($"{deliver}({fallbackJson});").ConfigureAwait(false);
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

    private void RegisterTab()
    {
        tabId ??= ExtensionWebViewModel.GetNextTabId();

        ExtensionWebViewModel.TabInfos[tabId.Value] = GetDAppInfo();
    }

    private Task InjectProviderAsync() => DispatchScriptSafeAsync(BuildProviderInjectionScript());

    /// <summary>
    /// Registers the app's Solana account with the page as a Wallet Standard wallet, which is
    /// how <c>@solana/wallet-adapter</c> discovers wallets.
    /// </summary>
    /// <remarks>
    /// Injected after load like everything else here, which the Wallet Standard is built for:
    /// a wallet both dispatches <c>register-wallet</c> and listens for <c>app-ready</c>, so
    /// registration lands whichever side comes up first.
    /// </remarks>
    private async Task InjectSolanaWalletAsync()
    {
        try
        {
            var initialAccounts = "[]";

            // Pre-populated only when the page is already cleared to connect, so a dapp's
            // autoConnect works without a prompt on load. Reads the stored address only,
            // never unlocking the key.
            if (DAppApprovalModel.IsAlreadyApproved(GetCurrentUrl() ?? string.Empty))
            {
                var account = await SolanaWalletStandardBridge.LoadAccountAsync();

                if (account is not null)
                {
                    initialAccounts = JsonSerializer.Serialize(
                        new[] { account }, SolanaWalletStandardBridge.SerializerOptions);
                }
            }

            await DispatchScriptSafeAsync(BuildSolanaWalletScript(initialAccounts, await LoadWalletIconAsync()));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlutoSolana] Wallet injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// A last-resort icon. Wallet Standard requires a data URI, and a wallet with a broken
    /// icon is worse than a plain one.
    /// </summary>
    private const string FALLBACK_WALLET_ICON_SVG =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"64\">" +
        "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#111827\"/></svg>";

    private static async Task<string> LoadWalletIconAsync()
    {
        if (_walletIconDataUri is not null)
        {
            return _walletIconDataUri;
        }

        try
        {
            using var asset = await FileSystem.OpenAppPackageFileAsync("solanawalleticon.svg");
            using var buffer = new MemoryStream();

            await asset.CopyToAsync(buffer);

            _walletIconDataUri = $"data:image/svg+xml;base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlutoSolana] Wallet icon asset unavailable: {ex.Message}");

            _walletIconDataUri =
                $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(FALLBACK_WALLET_ICON_SVG))}";
        }

        return _walletIconDataUri;
    }

    private string BuildSolanaWalletScript(string initialAccountsJson, string icon)
    {
        // Placeholder substitution rather than interpolation: the script is mostly braces,
        // and escaping every one of them for an interpolated string makes it unreadable.
        return SolanaWalletScript
            .Replace("__PLUTO_CHANNEL__", JsonSerializer.Serialize(ScriptInterfaceName))
            .Replace("__PLUTO_TAB_ID__", (tabId ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("__PLUTO_NAME__", JsonSerializer.Serialize(AppInfo.Name))
            .Replace("__PLUTO_ICON__", JsonSerializer.Serialize(icon))
            .Replace("__PLUTO_ACCOUNTS__", initialAccountsJson);
    }

    private Task DispatchScriptSafeAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script) || Handler is null)
        {
            return Task.CompletedTask;
        }

        return DispatchScriptAsync(script);
    }

    private static string? TryExtractId(string json) => TryExtractProperty(json, "id");

    private static string? TryExtractMethod(string json) => TryExtractProperty(json, "method");

    private static string? TryExtractProperty(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(name, out var property))
            {
                return property.GetString();
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

    /// <summary>
    /// The Wallet Standard wallet, as vanilla JavaScript. The registration handshake is
    /// reproduced from <c>@wallet-standard/wallet</c>'s <c>registerWallet</c> rather than
    /// imported: this runs as an evaluated string, where no package can be pulled in.
    ///
    /// The wallet advertises every feature; each account then lists the subset its key type
    /// can actually honour, which is what wallet-adapter checks each call against.
    /// </summary>
    private const string SolanaWalletScript = @"(function () {
    if (window.__plutoSolanaInjected) { return; }
    window.__plutoSolanaInjected = true;

    var channel = __PLUTO_CHANNEL__;
    var tabId = __PLUTO_TAB_ID__;
    var pending = {};

    // Separate from the Polkadot bridge's __mauiWalletDeliver so neither injection can
    // clobber the other's reply router, whichever lands first.
    window.__plutoSolanaDeliver = function (message) {
        try {
            var payload = (typeof message === 'string') ? JSON.parse(message) : message;
            if (!payload || !payload.id || !pending[payload.id]) { return; }
            var entry = pending[payload.id];
            delete pending[payload.id];
            if (payload.error) { entry.reject(new Error(payload.error)); }
            else { entry.resolve(payload.result); }
        } catch (err) {
            console.error('Solana wallet deliver failure', err);
        }
    };

    function post(method, payload) {
        return new Promise(function (resolve, reject) {
            var id = 'sol-' + Date.now() + '-' + Math.random().toString(16).slice(2);
            pending[id] = { resolve: resolve, reject: reject };
            try {
                var body = JSON.stringify({ id: id, method: method, payload: payload });
                if (window[channel] && window[channel].walletCall) {
                    window[channel].walletCall(body);
                } else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers[channel]) {
                    window.webkit.messageHandlers[channel].postMessage(body);
                } else {
                    throw new Error('Pluto wallet bridge is unavailable on this platform.');
                }
            } catch (err) {
                delete pending[id];
                reject(err);
            }
        });
    }

    // JSON carries no byte arrays, so everything binary crosses the bridge base64-encoded.
    function toBytes(base64) {
        var binary = atob(base64);
        var bytes = new Uint8Array(binary.length);
        for (var i = 0; i < binary.length; i++) { bytes[i] = binary.charCodeAt(i); }
        return bytes;
    }

    function toBase64(bytes) {
        var view = new Uint8Array(bytes);
        var binary = '';
        for (var i = 0; i < view.length; i++) { binary += String.fromCharCode(view[i]); }
        return btoa(binary);
    }

    function toAccount(dto) {
        return {
            address: dto.address,
            publicKey: toBytes(dto.publicKey),
            chains: dto.chains,
            features: dto.features,
            label: dto.label
        };
    }

    var accounts = (__PLUTO_ACCOUNTS__ || []).map(toAccount);
    var listeners = [];

    // Mirrors accounts[0] for the legacy provider below, which exposes a single active
    // account rather than a list.
    var connectedAddress = accounts.length ? accounts[0].address : null;

    function setAccounts(next) {
        accounts = next;
        connectedAddress = next.length ? next[0].address : null;
        listeners.slice().forEach(function (listener) {
            try { listener({ accounts: wallet.accounts }); }
            catch (err) { console.error('Solana wallet change listener failed', err); }
        });
    }

    // Sequential rather than parallel: each input may show a screen, or under Mobile Wallet
    // Adapter leave the app entirely, and those must not overlap.
    function runAll(args, handler) {
        return Array.prototype.slice.call(args).reduce(function (chain, input) {
            return chain.then(function (results) {
                return handler(input).then(function (output) {
                    results.push(output);
                    return results;
                });
            });
        }, Promise.resolve([]));
    }

    function signMessage(input) {
        return post('solana:signMessage', {
            message: toBase64(input.message),
            address: input.account ? input.account.address : null
        }).then(function (result) {
            return {
                signedMessage: toBytes(result.signedMessage),
                signature: toBytes(result.signature),
                signatureType: 'ed25519'
            };
        });
    }

    function signTransaction(input) {
        return post('solana:signTransaction', {
            transaction: toBase64(input.transaction)
        }).then(function (result) {
            return { signedTransaction: toBytes(result.signedTransaction) };
        });
    }

    function signAndSendTransaction(input) {
        return post('solana:signAndSendTransaction', {
            transaction: toBase64(input.transaction),
            chain: input.chain
        }).then(function (result) {
            return { signature: toBytes(result.signature) };
        });
    }

    var features = {
        'standard:connect': {
            version: '1.0.0',
            connect: function (input) {
                return post('solana:connect', {
                    tabId: tabId,
                    silent: !!(input && input.silent)
                }).then(function (result) {
                    setAccounts(((result && result.accounts) || []).map(toAccount));
                    return { accounts: wallet.accounts };
                });
            }
        },
        'standard:disconnect': {
            version: '1.0.0',
            disconnect: function () {
                return post('solana:disconnect', { tabId: tabId }).then(function () {
                    setAccounts([]);
                });
            }
        },
        'standard:events': {
            version: '1.0.0',
            on: function (event, listener) {
                if (event !== 'change') { return function () { }; }
                listeners.push(listener);
                return function () {
                    listeners = listeners.filter(function (existing) { return existing !== listener; });
                };
            }
        },
        'solana:signMessage': {
            version: '1.1.0',
            signMessage: function () { return runAll(arguments, signMessage); }
        },
        'solana:signTransaction': {
            version: '1.0.0',
            supportedTransactionVersions: ['legacy', 0],
            signTransaction: function () { return runAll(arguments, signTransaction); }
        },
        'solana:signAndSendTransaction': {
            version: '1.0.0',
            supportedTransactionVersions: ['legacy', 0],
            signAndSendTransaction: function () { return runAll(arguments, signAndSendTransaction); }
        }
    };

    var wallet = {
        get version() { return '1.0.0'; },
        get name() { return __PLUTO_NAME__; },
        get icon() { return __PLUTO_ICON__; },
        get chains() { return ['solana:mainnet', 'solana:devnet', 'solana:testnet']; },
        get features() { return features; },
        get accounts() { return accounts.slice(); }
    };

    // Dispatch and listen both, so registration lands whether the app is already up or
    // comes up later. This is what makes injecting after page load safe.
    var callback = function (api) { api.register(wallet); };

    try {
        window.dispatchEvent(new CustomEvent('wallet-standard:register-wallet', { detail: callback }));
    } catch (err) {
        console.error('wallet-standard:register-wallet could not be dispatched', err);
    }

    try {
        window.addEventListener('wallet-standard:app-ready', function (event) { callback(event.detail); });
    } catch (err) {
        console.error('wallet-standard:app-ready listener could not be added', err);
    }

    // The dashboard finds Solana wallets the Phantom way and never looks at the Wallet
    // Standard: app/services/wallet/solanaProvider.ts probes window.phantom.solana, then
    // window.solflare, then window.backpack, and app/services/wallet/walletCatalog.ts
    // decides a wallet is installed from those same globals. Registering the standard
    // wallet above is therefore not enough on its own, so the same bridge is served again
    // through the legacy provider shape that page actually reads.
    function publicKeyOf(address) {
        if (!address) { return null; }
        return {
            toBase58: function () { return address; },
            toString: function () { return address; },
            toBytes: function () { return accounts.length ? accounts[0].publicKey : new Uint8Array(0); }
        };
    }

    var legacyProvider = {
        isPhantom: true,
        get publicKey() { return publicKeyOf(connectedAddress); },
        get isConnected() { return connectedAddress !== null; },

        connect: function (options) {
            return post('solana:connect', {
                tabId: tabId,
                silent: !!(options && options.onlyIfTrusted)
            }).then(function (result) {
                setAccounts(((result && result.accounts) || []).map(toAccount));

                // onlyIfTrusted resolves empty when the page was never approved. The
                // dashboard reads a rejection as 'not trusted yet' and leaves the stored
                // session alone, whereas resolving with no key would throw deeper in its
                // own resolveAddress with a misleading message.
                if (!connectedAddress) { throw new Error('WALLET_CONNECTION_REJECTED'); }

                return { publicKey: publicKeyOf(connectedAddress) };
            });
        },

        disconnect: function () {
            return post('solana:disconnect', { tabId: tabId }).then(function () {
                setAccounts([]);
            });
        },

        signMessage: function (message) {
            return post('solana:signMessage', {
                message: toBase64(message),
                address: connectedAddress
            }).then(function (result) {
                // A real Uint8Array, not a plain object: the page hands this straight to
                // @polkadot/util-crypto's base58Encode.
                return {
                    signature: toBytes(result.signature),
                    publicKey: publicKeyOf(connectedAddress)
                };
            });
        },

        // Present because a Phantom-shaped provider is expected to have them.
        on: function () { },
        off: function () { },
        removeListener: function () { }
    };

    try {
        window.phantom = window.phantom || {};
        if (!window.phantom.solana) { window.phantom.solana = legacyProvider; }
    } catch (err) {
        console.error('Solana legacy provider could not be installed', err);
    }
})();";

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

    /// <summary>
    /// The configured <see cref="PlutoConfigurationModel.WhitelistedDApps"/> only, matched
    /// the way <see cref="DAppApprovalModel"/> matches them. Session approvals the user
    /// tapped through deliberately do not count: they clear a page to connect, not to sign
    /// without being shown what.
    /// </summary>
    private static bool IsWhitelistedDAppHost(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && PlutoConfigurationModel.WhitelistedDApps.Any(pattern => uri.Host.Contains(pattern));

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

            // Dispatched through the platform bridge, like every other injection here.
            // MAUI's EvaluateJavaScriptAsync is not usable for this payload: outside
            // Android it re-wraps the script inside an eval'd string literal, which
            // collapses the JWK's escaped quotes and leaves the page a script it cannot
            // parse - silently, on iOS. See X25519KeyInjection for the detail.
            await DispatchScriptSafeAsync(
                X25519KeyInjection.BuildInjectionScript(encryptionKey.SecretKey, publicKey));

            System.Diagnostics.Debug.WriteLine("X25519 key injected into dashboard.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"X25519 key injection failed: {ex.Message}");
        }
    }

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