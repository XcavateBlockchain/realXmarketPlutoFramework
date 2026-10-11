using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using PlutoFrameworkCore.AssetDidComm;

namespace PlutoFrameworkCore.Xcavate.Offers
{
    /// <summary>
    /// The realXmarket secondary-market offer protocol, as the messenger dashboard
    /// (AssetDidCommDashboard, app/services/buckets/realxmarket.ts) implements it: the
    /// plaintext JSON payloads the app seeds into a new offer bucket (the key-sharing
    /// document and the opening offer) and the purchased-status message the app writes
    /// once the on-chain purchase confirms. Every builder here matches the dashboard's
    /// own builders field for field - the dashboard parses these payloads back, so a
    /// deviation reads as an unreadable message, not as an error anyone reports.
    /// </summary>
    public static class OfferProtocol
    {
        /// <summary>The protocol version every payload is stamped with (a JSON number, 1).</summary>
        public const int ProtocolVersion = 1;

        /// <summary>The content type plaintext negotiation messages are written with.</summary>
        public const string JsonContentType = "application/json";

        /// <summary>The tag the offer message is written under (server-forced at bucket setup).</summary>
        public const string OfferTag = "realXmarket/offer";

        /// <summary>The tag the purchased-status message is written under.</summary>
        public const string StatusTag = "realXmarket/status";

        /// <summary>The tag the key-sharing message is written under (server-forced at bucket setup).</summary>
        public const string KeySharingTag = "didcomm/key-sharing-v1";

        /// <summary>
        /// A fresh X25519 keypair for one offer bucket: the public key as 64 lowercase
        /// hex characters (the SetupOfferBucket mutation's bucketEncryptionKey shape) and
        /// the secret as the JWK the key-sharing document carries.
        /// </summary>
        public static (string PublicKeyHex, JsonObject SecretJwk) GenerateBucketKeyPair()
        {
            var keyPair = X25519Model.GenerateX25519KeyPair();

            return (Convert.ToHexString(keyPair.PublicKey).ToLowerInvariant(), BuildSecretJwk(keyPair));
        }

        /// <summary>
        /// The key-sharing document, seeded as plaintext JSON:
        /// <c>{"id":"&lt;guid&gt;","from":"&lt;buyer&gt;","to":["&lt;buyer&gt;","&lt;seller&gt;"],"keys":[&lt;secretJwk&gt;]}</c>.
        /// The dashboard's <c>parsePlaintextKeySharingBody</c> requires exactly this
        /// shape: a non-empty id and from, an all-strings to array, and at least one key
        /// with kty/crv/x/d set.
        /// </summary>
        public static string BuildKeySharingPayload(string id, string buyer, string seller, JsonObject secretJwk) =>
            new JsonObject
            {
                ["id"] = id,
                ["from"] = buyer,
                ["to"] = new JsonArray(buyer, seller),
                ["keys"] = new JsonArray(secretJwk),
            }.ToJsonString();

        /// <summary>The key-sharing message's reference: "key-sharing:&lt;id&gt;".</summary>
        public static string KeySharingReference(string id) => $"key-sharing:{id}";

        /// <summary>The offer message's reference: "offer:&lt;shareListingId&gt;:&lt;buyerWallet&gt;".</summary>
        public static string OfferReference(long shareListingId, string buyerWallet) =>
            $"offer:{shareListingId.ToString(CultureInfo.InvariantCulture)}:{buyerWallet}";

        /// <summary>
        /// The opening offer, seeded as plaintext JSON and matched byte-for-byte against
        /// the dashboard's <c>buildOfferPayload</c>: kind, numeric version, the two ids
        /// as decimal strings, the 9-decimal quote as a decimal string, the whole-token
        /// amount, a null counterOf and the proposer's wallet. The offer never counters
        /// an earlier message and the app collects no note, so neither is emitted.
        /// </summary>
        public static string BuildOfferPayload(
            long shareListingId,
            long assetId,
            ulong sharePriceQuote,
            uint amount,
            string from) =>
            new JsonObject
            {
                ["kind"] = "offer",
                ["version"] = ProtocolVersion,
                ["shareListingId"] = shareListingId.ToString(CultureInfo.InvariantCulture),
                ["assetId"] = assetId.ToString(CultureInfo.InvariantCulture),
                ["pricePerToken"] = sharePriceQuote.ToString(CultureInfo.InvariantCulture),
                ["amount"] = amount,
                ["counterOf"] = null,
                ["from"] = from,
            }.ToJsonString();

        /// <summary>
        /// The terminal "purchased" status the buyer writes after the on-chain buy
        /// confirms, matched against the dashboard's <c>buildStatusPayload</c>: same id
        /// pair as the offer, a null counterOf, the status marker, and the confirming
        /// transaction's signature when the submission pipeline reported one.
        /// </summary>
        public static string BuildPurchasedStatusPayload(
            long shareListingId,
            long assetId,
            string from,
            string? tx)
        {
            var payload = new JsonObject
            {
                ["kind"] = "status",
                ["version"] = ProtocolVersion,
                ["shareListingId"] = shareListingId.ToString(CultureInfo.InvariantCulture),
                ["assetId"] = assetId.ToString(CultureInfo.InvariantCulture),
                ["counterOf"] = null,
                ["from"] = from,
                ["status"] = "purchased",
            };

            if (!string.IsNullOrWhiteSpace(tx))
            {
                payload["tx"] = tx;
            }

            return payload.ToJsonString();
        }

        /// <summary>
        /// The message metadata's content hash: sha256 over the exact body bytes as
        /// 0x-prefixed lowercase hex - the dashboard's <c>sha256HexUtf8</c> output shape,
        /// which the server's 32-byte-hex validation also accepts.
        /// </summary>
        public static string ContentHash(string body) =>
            "0x" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        /// <summary>
        /// The secret JWK the key-sharing document carries: kty/crv plus the public x
        /// and private d coordinates, both base64url without padding (the dashboard's
        /// decoder rejects padding).
        /// </summary>
        private static JsonObject BuildSecretJwk(X25519KeyPair keyPair) =>
            new()
            {
                ["kty"] = "OKP",
                ["crv"] = "X25519",
                ["x"] = WebEncoders.Base64UrlEncode(keyPair.PublicKey),
                ["d"] = WebEncoders.Base64UrlEncode(keyPair.PrivateKey),
            };
    }
}
