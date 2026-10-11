using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlutoFrameworkCore.Solana;
using XcavateProfileApiClient;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Talks to the profile API's realXmarket offer surface: the per-property offers
    /// namespace and the per-share-listing offer buckets the secondary-market offer flow
    /// (Phase 3B) communicates through.
    /// </summary>
    /// <remarks>
    /// Raw POSTs to <c>/graphql</c> - the pinned <c>XcavateProfileApiClient</c> (1.0.68)
    /// predates these operations, the same reason
    /// <see cref="RentCollectorSignatureClient"/> hand-serializes its body. Unlike that
    /// client these mutations are SIGNED: the package's
    /// <see cref="SigningHttpMessageHandler"/> applies the X-SS58-Address / X-Signature /
    /// X-Timestamp headers over the exact bytes sent, with the signature coming from the
    /// user's main key via <see cref="MainKeyModel.GetSignerAsync"/> - so each mutation
    /// costs a signing prompt. <see cref="FindOffersNamespaceAsync"/> is a read and goes
    /// unsigned, like <see cref="PropertyNamespaceClient"/>.
    /// </remarks>
    public class RealXmarketOfferClient
    {
        private const string ApiBaseUrl = "https://profile-api.xcavate.io/";

        private const string GraphQlPath = "/graphql";

        /// <summary>
        /// camelCase (Web defaults) with null members omitted: GraphQL treats an explicit
        /// JSON null as a provided value, while an absent field falls back to the server's
        /// default - the optional inputs must never clobber those defaults.
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Creates the property's offers namespace - named "<paramref name="propertyName"/>
        /// offers" server-side, idempotent per (assetId, cluster), so a repeat call returns
        /// the existing namespace rather than failing. Returns its namespace id.
        /// </summary>
        /// <remarks>
        /// Called best-effort after a relist confirms (PropertyDetailViewModel) and lazily
        /// retried at offer time; both the signing prompt being declined and any server
        /// failure surface as exceptions for the caller to swallow or report.
        /// </remarks>
        public async Task<long> CreateOffersNamespaceAsync(
            string propertyName,
            long assetId,
            SolanaCluster cluster,
            CancellationToken token = default)
        {
            const string query =
                "mutation CreateOffersNamespace($input: CreateOffersNamespaceInput!) { createOffersNamespace(input: $input) { id namespaceId name creator } }";

            // assetId travels as a string: the API's BigInt scalars serialize as JSON
            // strings, and CreateOffersNamespaceInput declares it String!.
            var variables = new
            {
                input = new Dictionary<string, object?>
                {
                    ["name"] = propertyName,
                    ["assetId"] = assetId.ToString(CultureInfo.InvariantCulture),
                    ["cluster"] = ClusterName(cluster),
                },
            };

            using var document = await SendSignedAsync(
                query,
                variables,
                "To set up the offers channel for your property tokens.",
                token).ConfigureAwait(false);

            return ReadInt64Field(document, "createOffersNamespace", "namespaceId");
        }

        /// <summary>
        /// Creates the offer bucket for one share listing and plants the two seed messages
        /// (the key-sharing message and the offer itself; the server forces their
        /// didcomm/key-sharing-v1 and realXmarket/offer tags). Returns the namespace, bucket
        /// and offer-message ids.
        /// </summary>
        /// <remarks>
        /// Wired for the Phase 3B make-offer flow; nothing in the app calls it yet.
        /// </remarks>
        public async Task<(long NamespaceId, long BucketId, long OfferMessageId)> SetupOfferBucketAsync(
            SetupOfferBucketRequest request,
            CancellationToken token = default)
        {
            const string query =
                "mutation SetupOfferBucket($input: SetupOfferBucketInput!) { setupOfferBucket(input: $input) { namespaceId bucketId offerMessageId } }";

            // assetId and shareListingId travel as strings: the API's BigInt scalars
            // serialize as JSON strings.
            var variables = new
            {
                input = new Dictionary<string, object?>
                {
                    ["propertyName"] = request.PropertyName,
                    ["assetId"] = request.AssetId.ToString(CultureInfo.InvariantCulture),
                    ["shareListingId"] = request.ShareListingId.ToString(CultureInfo.InvariantCulture),
                    ["cluster"] = ClusterName(request.Cluster),
                    ["seller"] = request.Seller,
                    ["bucketEncryptionKey"] = request.BucketEncryptionKey,
                    ["buyerViewerKey"] = request.BuyerViewerKey,
                    ["sellerViewerKey"] = request.SellerViewerKey,
                    ["keySharingMessage"] = BuildMessage(request.KeySharingMessage),
                    ["offerMessage"] = BuildMessage(request.OfferMessage),
                },
            };

            using var document = await SendSignedAsync(
                query,
                variables,
                "To set up the offer for your property tokens.",
                token).ConfigureAwait(false);

            return (
                ReadInt64Field(document, "setupOfferBucket", "namespaceId"),
                ReadInt64Field(document, "setupOfferBucket", "bucketId"),
                ReadInt64Field(document, "setupOfferBucket", "offerMessageId"));
        }

        /// <summary>
        /// Writes one message into an existing bucket - the raw signed <c>write</c>
        /// mutation (call_index 12), used by the Phase 3B buy flow to plant the
        /// "purchased" status once the on-chain buy confirms. Unlike the two
        /// <see cref="SetupOfferBucketAsync"/> seed messages, the tag is NOT forced
        /// server-side here, so the caller sets it. Returns the new message id.
        /// </summary>
        /// <remarks>
        /// Signed, so it costs a signing prompt; the caller decides whether the write is
        /// worth one (the purchased status is, the moment the user has just approved the
        /// purchase itself).
        /// </remarks>
        public async Task<long> WriteMessageAsync(
            long namespaceId,
            long bucketId,
            OfferMessageInput message,
            CancellationToken token = default)
        {
            const string query =
                "mutation WriteMessage($namespaceId: BigInt, $bucketId: BigInt!, $message: MessageInput!) { write(namespaceId: $namespaceId, bucketId: $bucketId, message: $message) { messageId } }";

            // The ids travel as strings: the API's BigInt scalars serialize as JSON strings.
            var variables = new
            {
                namespaceId = namespaceId.ToString(CultureInfo.InvariantCulture),
                bucketId = bucketId.ToString(CultureInfo.InvariantCulture),
                message = BuildMessage(message),
            };

            using var document = await SendSignedAsync(
                query,
                variables,
                "To report the completed purchase in your offer channel.",
                token).ConfigureAwait(false);

            return ReadInt64Field(document, "write", "messageId");
        }

        /// <summary>
        /// The id of the offers namespace already created for (assetId, cluster), or null
        /// when the property has none yet or the server answers with an error - callers
        /// treat null as "create it" (<see cref="CreateOffersNamespaceAsync"/> is
        /// idempotent). Unsigned: reads need no signature, like
        /// <see cref="PropertyNamespaceClient"/>.
        /// </summary>
        /// <remarks>
        /// The namespace attributes mirror the creation input's assetId / cluster pair the
        /// server keys its idempotency on; <c>assetId</c> goes as a <c>Long</c> the way
        /// <see cref="PropertyNamespaceClient"/>'s propertyId filter does.
        /// </remarks>
        public async Task<long?> FindOffersNamespaceAsync(
            long assetId,
            SolanaCluster cluster,
            CancellationToken token = default)
        {
            const string query =
                "query OffersNamespace($assetId: Long!, $cluster: String!) { namespaces(first: 1, where: { assetId: { eq: $assetId }, cluster: { eq: $cluster } }) { nodes { namespaceId } } }";

            var variables = new
            {
                assetId,
                cluster = ClusterName(cluster),
            };

            using var document = await SendUnsignedAsync(query, variables, token).ConfigureAwait(false);

            if (document is null
                || !document.RootElement.TryGetProperty("data", out var data)
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
        /// The cluster name the API keys its per-chain records on: the lowercase names of
        /// its webhook routes (<c>webhooks/init_property_asset_devnet</c>). Testnet has no
        /// deployment, so it has no name.
        /// </summary>
        private static string ClusterName(SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => "devnet",
            SolanaCluster.Mainnet => "mainnet",
            _ => throw new NotSupportedException($"The profile API has no realXmarket surface on Solana {cluster.GetName()}."),
        };

        private static Dictionary<string, object?> BuildMessage(OfferMessageInput message) =>
            new()
            {
                ["reference"] = message.Reference,
                ["tag"] = message.Tag,
                ["ipfsContent"] = message.IpfsContent,
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["description"] = message.Metadata.Description,
                    ["contentType"] = message.Metadata.ContentType,
                    ["contentHash"] = message.Metadata.ContentHash,
                    ["properties"] = message.Metadata.Properties,
                },
            };

        /// <summary>
        /// POSTs the GraphQL document with the main key's signature over the exact body
        /// bytes and returns the parsed response. Throws when there is no key to sign with
        /// (or its prompt was declined), when the server rejects the request, or when the
        /// answer carries GraphQL errors - the callers decide what a failure means.
        /// </summary>
        private static async Task<JsonDocument> SendSignedAsync(
            string query,
            object variables,
            string signReason,
            CancellationToken token)
        {
            var signer = await MainKeyModel.GetSignerAsync(signReason, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "No key to sign the profile API request with, or the signing prompt was declined.");

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + GraphQlPath.TrimStart('/'));

            // "application/json", not just "json" - the .NET 10 media-type parser
            // throws a FormatException on the latter before the request is even sent.
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }, SerializerOptions),
                Encoding.UTF8,
                "application/json");

            // The handler signs the exact bytes staged above, so it must see the final
            // content; adding headers after this point is too late by design.
            using var httpClient = new HttpClient(new SigningHttpMessageHandler(signer));

            using var response = await httpClient.SendAsync(request, token).ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"The profile API rejected the request ({response.StatusCode}): {responseBody}");
            }

            return ParseResponse(responseBody);
        }

        /// <summary>
        /// POSTs the GraphQL document without a signature (reads are public) and returns
        /// the parsed response, or null when the server does not answer successfully -
        /// the same tolerance <see cref="PropertyNamespaceClient"/> applies.
        /// </summary>
        private static async Task<JsonDocument?> SendUnsignedAsync(
            string query,
            object variables,
            CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + GraphQlPath.TrimStart('/'));

            request.Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }, SerializerOptions),
                Encoding.UTF8,
                "application/json");

            using var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request, token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            try
            {
                return ParseResponse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read the profile API's answer: ");
                Console.WriteLine(ex);

                return null;
            }
        }

        /// <summary>
        /// Parses the response body and throws when it carries a GraphQL <c>errors</c>
        /// array (HTTP 200 with no usable <c>data</c>), quoting the server's own messages.
        /// </summary>
        private static JsonDocument ParseResponse(string responseBody)
        {
            var document = JsonDocument.Parse(responseBody);

            if (document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var messages = errors
                    .EnumerateArray()
                    .Select(error => error.TryGetProperty("message", out var message) ? message.GetString() : null)
                    .Where(message => !string.IsNullOrWhiteSpace(message));

                throw new Exception($"The profile API answered with an error: {string.Join("; ", messages)}");
            }

            return document;
        }

        /// <summary>
        /// Reads <c>data.{field}.{member}</c> as a long, whatever JSON shape the BigInt
        /// arrived in. A missing or unparseable value is a broken contract, not an empty
        /// answer - it throws rather than silently returning a default id.
        /// </summary>
        private static long ReadInt64Field(JsonDocument document, string field, string member)
        {
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty(field, out var result)
                && result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty(member, out var value)
                && TryReadInt64(value, out var parsed))
            {
                return parsed;
            }

            throw new Exception($"The profile API's answer carried no {field}.{member}: {document.RootElement}");
        }

        /// <summary>
        /// The server's BigInt scalar serializes as a JSON string ("13"), not a number,
        /// so both shapes are accepted.
        /// </summary>
        private static bool TryReadInt64(JsonElement element, out long value)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetInt64(out value);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return long.TryParse(
                    element.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
            }

            value = 0;

            return false;
        }
    }

    /// <summary>
    /// Everything <see cref="RealXmarketOfferClient.SetupOfferBucketAsync"/> needs to open
    /// an offer bucket for one share listing. Maps one-to-one onto the API's
    /// SetupOfferBucketInput; the server forces the didcomm/key-sharing-v1 and
    /// realXmarket/offer tags on the two seed messages, so they are not set here.
    /// </summary>
    public sealed record SetupOfferBucketRequest
    {
        public required string PropertyName { get; init; }

        public required long AssetId { get; init; }

        public required long ShareListingId { get; init; }

        public required SolanaCluster Cluster { get; init; }

        /// <summary>The seller's wallet address - the bucket's counterparty-facing owner.</summary>
        public required string Seller { get; init; }

        /// <summary>The bucket's 32-byte X25519 public key, lowercase hex.</summary>
        public required string BucketEncryptionKey { get; init; }

        public string? BuyerViewerKey { get; init; }

        public string? SellerViewerKey { get; init; }

        public required OfferMessageInput KeySharingMessage { get; init; }

        public required OfferMessageInput OfferMessage { get; init; }
    }

    /// <summary>The API's MessageInput: one seed message planted in the new bucket.</summary>
    public sealed record OfferMessageInput
    {
        public required string Reference { get; init; }

        public string? Tag { get; init; }

        public string? IpfsContent { get; init; }

        public required OfferMessageMetadataInput Metadata { get; init; }
    }

    /// <summary>The MessageInput's metadata block.</summary>
    public sealed record OfferMessageMetadataInput
    {
        public string? Description { get; init; }

        public required string ContentType { get; init; }

        public required string ContentHash { get; init; }

        public Dictionary<string, string>? Properties { get; init; }
    }
}
