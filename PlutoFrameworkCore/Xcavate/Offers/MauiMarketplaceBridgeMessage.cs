using System.Globalization;
using System.Text.Json;

namespace PlutoFrameworkCore.Xcavate.Offers
{
    /// <summary>
    /// One message the messenger dashboard posts through the injected
    /// <c>window.mauiMarketplace</c> bridge (see the dashboard's
    /// app/services/injection/mauiMarketplaceBridge.ts): the buyer's purchase hand-off
    /// after a price was agreed (<see cref="MauiMarketplaceBridgeMessageKind.BuyRelisted"/>),
    /// or the seller's price-update hand-off (<see cref="MauiMarketplaceBridgeMessageKind.DelistAndRelist"/>).
    /// </summary>
    public enum MauiMarketplaceBridgeMessageKind
    {
        BuyRelisted,
        DelistAndRelist,
    }

    /// <summary>The parsed bridge payload. Ids travel as decimal strings in the JSON.</summary>
    /// <param name="Kind">Which hand-off the page requested.</param>
    /// <param name="ShareListingId">The on-chain share-listing id the action targets.</param>
    /// <param name="AssetId">The tokenised property's asset id.</param>
    /// <param name="Amount">The agreed whole-token amount.</param>
    /// <param name="SharePriceQuote">The agreed price per token at the 9-decimal quote scale.</param>
    /// <param name="Seller">The listing's seller wallet.</param>
    /// <param name="PropertyName">The property name the page parsed out of the bucket name, when known.</param>
    /// <param name="NamespaceId">The offer bucket's namespace, when the page reported it - lets the app write the purchased status back.</param>
    /// <param name="BucketId">The offer bucket, when the page reported it.</param>
    public sealed record MauiMarketplaceBridgeMessage(
        MauiMarketplaceBridgeMessageKind Kind,
        long ShareListingId,
        long AssetId,
        uint Amount,
        ulong SharePriceQuote,
        string Seller,
        string? PropertyName,
        long? NamespaceId,
        long? BucketId)
    {
        /// <summary>
        /// Parses one bridge message; false for anything malformed, unknown, or missing a
        /// required field. The bridge is a page-to-app channel, so a bad payload is logged
        /// by the caller and quietly dropped - never surfaced to the user.
        /// </summary>
        public static bool TryParse(string json, out MauiMarketplaceBridgeMessage? message)
        {
            message = null;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var kind = ReadString(root, "type") switch
                {
                    "buyRelisted" => MauiMarketplaceBridgeMessageKind.BuyRelisted,
                    "delistAndRelist" => MauiMarketplaceBridgeMessageKind.DelistAndRelist,
                    _ => (MauiMarketplaceBridgeMessageKind?)null,
                };

                if (kind is null
                    || !ReadInt64(root, "shareListingId", out var shareListingId)
                    || shareListingId < 0
                    || !ReadInt64(root, "assetId", out var assetId)
                    || assetId < 0
                    || !ReadUInt32(root, "amount", out var amount)
                    || amount < 1
                    || !ReadUInt64(root, "pricePerToken", out var sharePriceQuote)
                    || ReadString(root, "seller") is not { Length: > 0 } seller)
                {
                    return false;
                }

                var propertyName = ReadString(root, "propertyName");
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    propertyName = null;
                }

                message = new MauiMarketplaceBridgeMessage(
                    kind.Value,
                    shareListingId,
                    assetId,
                    amount,
                    sharePriceQuote,
                    seller,
                    propertyName,
                    ReadInt64(root, "namespaceId", out var namespaceId) ? namespaceId : null,
                    ReadInt64(root, "bucketId", out var bucketId) ? bucketId : null);

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string? ReadString(JsonElement root, string property) =>
            root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;

        /// <summary>
        /// The dashboard sends ids as decimal strings; a plain JSON number is accepted
        /// too, so a future sender is not pinned to one encoding.
        /// </summary>
        private static bool ReadInt64(JsonElement root, string property, out long value)
        {
            value = 0;

            if (!root.TryGetProperty(property, out var element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetInt64(out value);
            }

            return element.ValueKind == JsonValueKind.String
                && long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool ReadUInt64(JsonElement root, string property, out ulong value)
        {
            value = 0;

            if (!root.TryGetProperty(property, out var element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetUInt64(out value);
            }

            return element.ValueKind == JsonValueKind.String
                && ulong.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool ReadUInt32(JsonElement root, string property, out uint value)
        {
            value = 0;

            if (!root.TryGetProperty(property, out var element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetUInt32(out value);
            }

            return element.ValueKind == JsonValueKind.String
                && uint.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
