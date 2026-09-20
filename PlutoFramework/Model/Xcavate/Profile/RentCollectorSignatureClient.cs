using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlutoFramework.Model.Solana;
using PlutoFrameworkCore.Solana;
using XcavateProfile.Client;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Asks the profile API for the rent collector's signature on a marketplace
    /// buy, reserve, or claim transaction.
    /// </summary>
    /// <remarks>
    /// The marketplace program pins the fee payer of buy, reserve, and claim to its configured
    /// rent collector and requires that key to have signed, but the investor's wallet
    /// cannot co-sign on its own machine - so the profile API holds the rent collector
    /// key (in its environment) and signs the exact compiled message the investor is
    /// about to sign and submit. The investor is authenticated to that endpoint the
    /// same way every other profile API request is: the app's Solana account signs the
    /// signed-payload over the body's Blake2b-128 hash, and the server verifies it.
    ///
    /// The pinned <c>XcavateProfileApiClient</c> (1.0.68) predates the endpoint's
    /// request model, so the body is serialized here against its known wire shape - a
    /// single <c>message</c> field with an explicit <see cref="JsonPropertyName"/> makes
    /// the hash naming-policy-independent - while the hash itself reuses the package's
    /// own <see cref="CryptoHelper.HashHex"/>, so the body hash is byte-identical to
    /// the server's by construction.
    /// </remarks>
    internal static class RentCollectorSignatureClient
    {
        private const string ApiBaseUrl = "https://profile-api.xcavate.io/";

        private const string EndpointPath = "/api/marketplace/rent-collector-signature";

        /// <summary>
        /// The rent collector's 64-byte Ed25519 signature over
        /// <paramref name="compiledMessage"/>.
        /// </summary>
        /// <remarks>
        /// The account signs the signed-payload for authentication. With a key held on
        /// this device that is free; under a Mobile Wallet Adapter wallet it is a
        /// user-facing prompt, so the marketplace's two-signature flow does not call
        /// this - it signs the payload inside the same wallet session as the
        /// transaction itself and posts with <see cref="PostSignedAsync"/>, keeping
        /// the blockhash's short lifetime to one trip through the wallet app. Either
        /// way the API call comes before the transaction prompt: a 400/503 from the
        /// server never costs a transaction approval too.
        /// </remarks>
        public static async Task<byte[]> GetRentCollectorSignatureAsync(
            PlutoFrameworkSolanaAccount account,
            byte[] compiledMessage,
            string description,
            CancellationToken token)
        {
            var body = BuildBody(compiledMessage);

            var timestamp = DateTime.UtcNow.ToString("o");

            var payload = BuildPayload(body, timestamp);

            var signer = new SolanaAccountRequestSigner(account, description);

            var signature = await signer.SignAsync(payload);

            return await PostSignedAsync(
                body, timestamp, signer.Address, signer.EncodeSignature(signature), token);
        }

        /// <summary>
        /// The HTTP half of <see cref="GetRentCollectorSignatureAsync"/> for a payload
        /// signed elsewhere - under Mobile Wallet Adapter, inside the wallet session
        /// that goes on to sign the transaction itself.
        /// </summary>
        public static async Task<byte[]> PostSignedAsync(
            string body,
            string timestamp,
            string signerAddress,
            string encodedSignature,
            CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + EndpointPath.TrimStart('/'));

            // "application/json", not just "json" - the .NET 10 media-type parser
            // throws a FormatException on the latter before the request is even sent.
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            request.Headers.Add("X-SS58-Address", signerAddress);
            request.Headers.Add("X-Signature", encodedSignature);
            request.Headers.Add("X-Timestamp", timestamp);

            using var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request, token);

            var serverBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"The marketplace signature service rejected the transaction ({response.StatusCode}): {serverBody}");
            }

            var rentCollectorSignature = JsonSerializer.Deserialize<RentCollectorSignatureResponse>(
                serverBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return rentCollectorSignature?.Signature is null
                ? throw new Exception("The marketplace signature service returned no signature.")
                : SolanaBase58.Decode(rentCollectorSignature.Signature);
        }

        /// <summary>
        /// The request body as the server hashes it: one <c>message</c> field holding the
        /// base64 of the compiled message.
        /// </summary>
        internal static string BuildBody(byte[] compiledMessage) =>
            JsonSerializer.Serialize(
                new { message = Convert.ToBase64String(compiledMessage) },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        /// <summary>
        /// The signed payload, in the profile API's <c>method:path:body_hash:timestamp</c>
        /// format with the hash exactly as the server recomputes it.
        /// </summary>
        internal static string BuildPayload(string body, string timestamp) =>
            $"POST:{EndpointPath}:{CryptoHelper.HashHex(body)}:{timestamp}";

        private sealed class RentCollectorSignatureResponse
        {
            /// <summary>Base58 of the rent collector's 64-byte signature.</summary>
            public required string Signature { get; init; }
        }
    }
}
