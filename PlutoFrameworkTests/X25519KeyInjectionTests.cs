using PlutoFrameworkCore.AssetDidComm;
using System.Text.Json;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The messenger dashboard runs in a web view and decrypts nothing until the wallet
    /// hands it the X25519 secret key. The handover is a single line of JavaScript, and a
    /// malformed one fails silently - the page just never decrypts - so the payload is
    /// pinned down here rather than left to be noticed on a device.
    /// </summary>
    public class X25519KeyInjectionTests
    {
        private static readonly byte[] SecretKey = BuildKey(seed: 1);

        private static readonly byte[] PublicKey = BuildKey(seed: 200);

        private static byte[] BuildKey(int seed)
        {
            var key = new byte[32];

            for (var i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(seed + i);
            }

            return key;
        }

        /// <summary>
        /// Pulls the first argument back out of the built script and undoes both layers of
        /// serialization, mirroring what the page does: the argument is a JSON string, and
        /// the string holds the JWK.
        /// </summary>
        private static Dictionary<string, string> ReadInjectedJwk(string script)
        {
            const string marker = "window.assetDidComm.injectX25519Key(";
            const string terminator = ", { persist: false })";

            var start = script.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "script does not call injectX25519Key");

            start += marker.Length;

            var end = script.IndexOf(terminator, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "injectX25519Key call is not terminated as expected");

            var argument = script[start..end];

            var jwkJson = JsonSerializer.Deserialize<string>(argument);
            Assert.That(jwkJson, Is.Not.Null, "the first argument is not a JSON string");

            var jwk = JsonSerializer.Deserialize<Dictionary<string, string>>(jwkJson!);
            Assert.That(jwk, Is.Not.Null, "the argument does not hold a JWK object");

            return jwk!;
        }

        [Test]
        public void BuildJwk_DescribesAnX25519KeyPair()
        {
            var jwk = JsonSerializer.Deserialize<Dictionary<string, string>>(
                X25519KeyInjection.BuildJwk(SecretKey, PublicKey));

            Assert.That(jwk, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(jwk!["kty"], Is.EqualTo("OKP"));
                Assert.That(jwk["crv"], Is.EqualTo("X25519"));
                Assert.That(jwk["d"], Is.EqualTo(Convert.ToBase64String(SecretKey).TrimEnd('=').Replace('+', '-').Replace('/', '_')));
                Assert.That(jwk["x"], Is.EqualTo(Convert.ToBase64String(PublicKey).TrimEnd('=').Replace('+', '-').Replace('/', '_')));
            });
        }

        /// <summary>
        /// The JWK members are base64url, not base64. Plain base64 would carry '+', '/' and
        /// '=' padding, none of which the dashboard's decoder accepts.
        /// </summary>
        [Test]
        public void BuildJwk_EncodesTheKeyMembersAsBase64Url()
        {
            var jwk = JsonSerializer.Deserialize<Dictionary<string, string>>(
                X25519KeyInjection.BuildJwk(SecretKey, PublicKey))!;

            Assert.Multiple(() =>
            {
                foreach (var member in new[] { "d", "x" })
                {
                    Assert.That(jwk[member], Does.Not.Contain("+"), $"{member} is not base64url");
                    Assert.That(jwk[member], Does.Not.Contain("/"), $"{member} is not base64url");
                    Assert.That(jwk[member], Does.Not.Contain("="), $"{member} is not base64url");
                }
            });
        }

        [Test]
        public void BuildInjectionScript_DeliversTheKeyPairToTheDashboard()
        {
            var jwk = ReadInjectedJwk(X25519KeyInjection.BuildInjectionScript(SecretKey, PublicKey));

            Assert.Multiple(() =>
            {
                Assert.That(jwk["kty"], Is.EqualTo("OKP"));
                Assert.That(jwk["crv"], Is.EqualTo("X25519"));
                Assert.That(jwk["d"], Is.EqualTo(Convert.ToBase64String(SecretKey).TrimEnd('=').Replace('+', '-').Replace('/', '_')));
                Assert.That(jwk["x"], Is.EqualTo(Convert.ToBase64String(PublicKey).TrimEnd('=').Replace('+', '-').Replace('/', '_')));
            });
        }

        /// <summary>
        /// The script is evaluated as soon as a navigation completes, which can be before the
        /// dashboard's own bundle has installed the bridge. The second branch parks the key
        /// where the dashboard collects it once it comes online.
        /// </summary>
        [Test]
        public void BuildInjectionScript_ParksTheKeyWhenTheBridgeIsNotUpYet()
        {
            var script = X25519KeyInjection.BuildInjectionScript(SecretKey, PublicKey);

            Assert.That(script, Does.Contain("window.__assetDidCommPendingX25519Key ="));
        }

        /// <summary>
        /// The script is handed to the platform web view verbatim, on one line. A newline
        /// would end the statement early on any host that concatenates it into a larger
        /// expression, and this payload has no reason to carry one.
        /// </summary>
        [Test]
        public void BuildInjectionScript_IsASingleLine()
        {
            var script = X25519KeyInjection.BuildInjectionScript(SecretKey, PublicKey);

            Assert.Multiple(() =>
            {
                Assert.That(script, Does.Not.Contain("\n"));
                Assert.That(script, Does.Not.Contain("\r"));
            });
        }
    }
}
