using PlutoFramework.Model;
using PlutoFrameworkCore.AssetDidComm;
using System.Text.Json;

namespace PlutoFramework.Components.Messages;

/// <summary>
/// WebView that hosts the Asset DIDComm dashboard and automatically
/// injects the user's X25519 secret key (JWK) so decryption works
/// without manual key entry.
/// </summary>
public class X25519WebView : Microsoft.Maui.Controls.WebView
{
    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(X25519WebView),
            defaultValue: "https://realxmessage.xcavate.io/",
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

    private static void OnUrlChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is X25519WebView webView && newValue is string url)
        {
            webView.Source = new UrlWebViewSource { Url = url };
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
            return;

        await InjectX25519KeyAsync();
    }

    private async Task InjectX25519KeyAsync()
    {
        try
        {
            var encryptionKey = await KeysModel.GetX25519KeyAsync();

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