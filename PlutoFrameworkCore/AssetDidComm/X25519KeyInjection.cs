using System.Text.Json;

namespace PlutoFrameworkCore.AssetDidComm
{
    /// <summary>
    /// Builds the JavaScript that hands the wallet's X25519 secret key to the Asset DIDComm
    /// dashboard hosted in the app's web view, so the user never types a key in by hand.
    /// </summary>
    public static class X25519KeyInjection
    {
        /// <summary>
        /// The key pair as a JWK, the shape the dashboard's <c>injectX25519Key</c> reads.
        /// Both members are base64url: the dashboard's decoder rejects base64 padding.
        /// </summary>
        public static string BuildJwk(byte[] secretKey, byte[] publicKey)
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

        /// <summary>
        /// The statement to evaluate in the page. It covers both "bridge already installed"
        /// and "app still booting" - the second branch parks the key where the dashboard
        /// collects it once its bundle comes online.
        /// </summary>
        /// <remarks>
        /// Must reach the page verbatim, through the platform web view. MAUI's
        /// <c>WebView.EvaluateJavaScriptAsync</c> is not a usable transport for it: on every
        /// platform but Android it rewrites the script into
        /// <c>try{JSON.stringify(eval('...'))}catch(e){'null'}</c> and escapes only single
        /// quotes on the way in. The JWK travels as a JSON string, whose quotes are
        /// <c>"</c> escapes, and those collapse into bare quotes inside that string
        /// literal - leaving the page an unparseable script whose SyntaxError the generated
        /// catch swallows, so the failure surfaces as nothing at all.
        /// </remarks>
        public static string BuildInjectionScript(byte[] secretKey, byte[] publicKey)
        {
            var jsLiteral = JsonSerializer.Serialize(BuildJwk(secretKey, publicKey));

            return $"window.assetDidComm ? window.assetDidComm.injectX25519Key({jsLiteral}, {{ persist: false }}) "
                + $": (window.__assetDidCommPendingX25519Key = {jsLiteral})";
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
