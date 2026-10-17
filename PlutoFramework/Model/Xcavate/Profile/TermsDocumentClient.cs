using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using PlutoFrameworkCore.Xcavate;
using XcavateProfile.Client;
using XcavateProfileApiClient;
using XcavateProfileApiClient.Signing;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Talks to the profile API's terms-document surface: one terms PDF per lawyer wallet,
    /// pinned to IPFS server-side and readable publicly by counterparties.
    /// </summary>
    /// <remarks>
    /// Hand-rolled like <see cref="RentCollectorSignatureClient"/>: the pinned
    /// <c>XcavateProfileApiClient</c> (1.0.68) predates these endpoints. The upload is a
    /// SIGNED multipart POST - the X-SS58-Address / X-Signature / X-Timestamp headers are
    /// built over <see cref="EmptyPayloadBody"/> because the server deliberately hashes an
    /// empty body for multipart uploads rather than the file bytes. The package's own
    /// header writer (<c>RequestSigning</c>) is internal, so the same sequence is spelled
    /// out here through its public primitives (<see cref="CryptoHelper.ConstructPayload"/>,
    /// <see cref="SignedRequestHeaders"/>, <see cref="IRequestSigner"/>). The GET is public
    /// and unsigned.
    /// </remarks>
    public static class TermsDocumentClient
    {
        private const string ApiBaseUrl = "https://profile-api.xcavate.io/";

        private static string PathFor(string address) => $"api/profiles/{address}/terms-document";

        /// <summary>
        /// The wallet's terms document, or null when it has uploaded none (the endpoint's
        /// 404 is the normal "no document" answer, never an error). Any other non-success
        /// throws with the server's body quoted.
        /// </summary>
        public static async Task<TermsDocumentInfo?> GetAsync(string address, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiBaseUrl + PathFor(address));

            using var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var serverBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"The profile API rejected the terms document request ({response.StatusCode}): {serverBody}");
            }

            return TermsDocumentInfo.Parse(serverBody);
        }

        /// <summary>
        /// Uploads (or replaces - last write wins) the wallet's terms PDF and returns the
        /// stored record. Signing costs a wallet prompt via
        /// <see cref="MainKeyModel.GetSignerAsync"/>; a declined prompt, a rejection
        /// (wrong type, not the owner) and a server-side IPFS failure all throw with the
        /// server's body quoted for the caller to surface.
        /// </summary>
        public static async Task<TermsDocumentInfo> UploadAsync(
            string address,
            string fileName,
            Stream pdf,
            CancellationToken ct)
        {
            var signer = await MainKeyModel.GetSignerAsync("To publish your terms document.", ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "No key to sign the profile API request with, or the signing prompt was declined.");

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + PathFor(address));

            var fileContent = new StreamContent(pdf);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            // The form field name is the contract: the server reads `file`.
            request.Content = new MultipartFormDataContent
            {
                { fileContent, "file", fileName },
            };

            // Multipart requests sign the empty body - the file bytes sit outside the
            // signature. The path is the decoded route path the server binds. This is the
            // package's internal RequestSigning.ApplyAsync sequence spelled out over its
            // public primitives: payload `method:path:bodyHash:timestamp`, round-trip ("o")
            // UTC timestamp header.
            var timestamp = DateTime.UtcNow;
            var payload = CryptoHelper.ConstructPayload(
                "POST", "/" + PathFor(address), EmptyPayloadBody.Instance, timestamp);
            var signature = await signer.SignAsync(payload).ConfigureAwait(false);

            request.Headers.Add(SignedRequestHeaders.Address, signer.Address);
            request.Headers.Add(SignedRequestHeaders.Signature, signer.EncodeSignature(signature));
            request.Headers.Add(SignedRequestHeaders.Timestamp, timestamp.ToString("o"));

            using var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);

            var serverBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"The profile API rejected the terms document upload ({response.StatusCode}): {serverBody}");
            }

            return TermsDocumentInfo.Parse(serverBody);
        }

        /// <summary>
        /// Parses the endpoint's response JSON; the pure implementation lives on
        /// <see cref="TermsDocumentInfo.Parse"/> so the test project (which cannot
        /// reference this MAUI assembly) covers it.
        /// </summary>
        public static TermsDocumentInfo Parse(string json) => TermsDocumentInfo.Parse(json);
    }
}
