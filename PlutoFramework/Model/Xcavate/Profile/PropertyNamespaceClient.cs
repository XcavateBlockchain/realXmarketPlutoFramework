using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Resolves the realxmessenger namespace that belongs to a marketplace listing.
    /// </summary>
    /// <remarks>
    /// The profile API's property-asset webhook (<c>webhooks/init_property_asset_devnet</c>)
    /// creates one namespace per registered property asset and stamps it with the
    /// marketplace <c>listing_id</c> in its <c>propertyId</c> attribute. The GraphQL read
    /// side needs no signature, so this is a plain unsigned POST to <c>/graphql</c> -
    /// the same endpoint the messenger SPA itself talks to.
    /// </remarks>
    internal static class PropertyNamespaceClient
    {
        private const string ApiBaseUrl = "https://profile-api.xcavate.io/";

        private const string GraphQlPath = "/graphql";

        /// <summary>
        /// The id of the namespace created for <paramref name="propertyId"/> (the marketplace
        /// listing id), or null when the property has none yet - the webhook runs off-chain,
        /// so a fresh listing can legitimately lag behind - or when the server answers with
        /// an error. Callers treat null as "open the generic messenger instead".
        /// </summary>
        public static async Task<long?> GetNamespaceIdByPropertyIdAsync(long propertyId, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + GraphQlPath.TrimStart('/'));

            // "application/json", not just "json" - the .NET 10 media-type parser
            // throws a FormatException on the latter before the request is even sent.
            request.Content = new StringContent(BuildBody(propertyId), Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request, token);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return ParseNamespaceId(await response.Content.ReadAsStringAsync(token));
        }

        /// <summary>
        /// The GraphQL document, filtered on the namespace attribute the webhook stamps.
        /// </summary>
        internal static string BuildBody(long propertyId) =>
            JsonSerializer.Serialize(
                new
                {
                    query = "query NamespaceByPropertyId($propertyId: Long!) { namespaces(first: 1, where: { propertyId: { eq: $propertyId } }) { nodes { namespaceId } } }",
                    variables = new { propertyId },
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        /// <summary>
        /// Reads <c>data.namespaces.nodes[0].namespaceId</c>. A GraphQL error response
        /// (HTTP 200 with an <c>errors</c> array and no usable <c>data</c>) and an empty
        /// result both come back as null rather than throwing.
        /// </summary>
        internal static long? ParseNamespaceId(string responseBody)
        {
            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("namespaces", out var namespaces)
                || namespaces.ValueKind != JsonValueKind.Object
                || !namespaces.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array
                || nodes.GetArrayLength() == 0)
            {
                return null;
            }

            var firstNode = nodes[0];

            return firstNode.ValueKind == JsonValueKind.Object
                && firstNode.TryGetProperty("namespaceId", out var namespaceId)
                && TryReadInt64(namespaceId, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// The server's <c>Long</c> scalar serializes as a JSON string ("13"), not a
        /// number, so both shapes are accepted.
        /// </summary>
        private static bool TryReadInt64(JsonElement element, out long value)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetInt64(out value);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return long.TryParse(element.GetString(), out value);
            }

            value = 0;

            return false;
        }
    }
}
