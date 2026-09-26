using System.Net.Http;
using System.Text;
using System.Text.Json;
using PlutoFrameworkCore.Solana;

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
    /// about to sign and submit. The endpoint does not authenticate the request: the
    /// server only ever signs a message that also requires the investor's own on-chain
    /// signature, so the rent collector's half is useless to anyone else. The investor's
    /// address goes in the X-SS58-Address header just so the server can check that.
    ///
    /// The pinned <c>XcavateProfileApiClient</c> (1.0.68) predates the endpoint's
    /// request model, so the body is serialized here against its known wire shape - a
    /// single <c>message</c> field, already lowercase, so the naming policy cannot
    /// change it.
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
        /// A plain HTTP call - nothing is signed locally, so it costs no wallet prompt
        /// and always comes before the transaction approval: a 400/503 from the server
        /// never costs a transaction approval too.
        /// </remarks>
        public static async Task<byte[]> GetRentCollectorSignatureAsync(
            string investorAddress,
            byte[] compiledMessage,
            CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + EndpointPath.TrimStart('/'));

            // "application/json", not just "json" - the .NET 10 media-type parser
            // throws a FormatException on the latter before the request is even sent.
            request.Content = new StringContent(BuildBody(compiledMessage), Encoding.UTF8, "application/json");

            request.Headers.Add("X-SS58-Address", investorAddress);

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
        /// The request body: one <c>message</c> field holding the base64 of the
        /// compiled message.
        /// </summary>
        internal static string BuildBody(byte[] compiledMessage) =>
            JsonSerializer.Serialize(
                new { message = Convert.ToBase64String(compiledMessage) },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        private sealed class RentCollectorSignatureResponse
        {
            /// <summary>Base58 of the rent collector's 64-byte signature.</summary>
            public required string Signature { get; init; }
        }
    }
}
