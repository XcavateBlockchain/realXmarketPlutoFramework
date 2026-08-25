using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using UniqueryPlus.Metadata;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The property document behind a Solana <c>PropertyAsset.metadataUri</c>: the
    /// marketplace webapp's upload format. It is close to, but not the same as, the
    /// Substrate-era <see cref="PropertyMetadata"/> contract the property views bind to -
    /// <c>finances</c> instead of <c>financials</c>, <c>numberOfShares</c> /
    /// <c>sharePrice</c> instead of <c>numberOfTokens</c> / <c>pricePerToken</c>,
    /// <c>propertyImages</c> instead of <c>files</c>, and flat <c>companyName</c> /
    /// <c>companyLogo</c> fields instead of a nested <c>company</c> - so it deserializes
    /// into this shape and maps across with <see cref="ToPropertyMetadata"/>.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public record XcavateSolanaPropertyMetadata
    {
        // The webapp's address and attributes objects are supersets of the records the
        // views bind to (extra "region" / "location" address fields), so they deserialize
        // into those records directly and the extras are ignored.
        [JsonPropertyName("address")] public PropertyAddress? Address { get; set; }

        [JsonPropertyName("attributes")] public PropertyAttributes? Attributes { get; set; }

        [JsonPropertyName("buildingControlCode")] public string? BuildingControlCode { get; set; }

        [JsonPropertyName("companyId")] public string? CompanyId { get; set; }

        [JsonPropertyName("companyLogo")] public string? CompanyLogo { get; set; }

        [JsonPropertyName("companyName")] public string? CompanyName { get; set; }

        [JsonPropertyName("companyWalletAddress")] public string? CompanyWalletAddress { get; set; }

        [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("finances")] public XcavateSolanaPropertyFinances? Finances { get; set; }

        [JsonPropertyName("floorPlan")] public string? FloorPlan { get; set; }

        [JsonPropertyName("map")] public string? Map { get; set; }

        [JsonPropertyName("otherDocuments")] public List<string> OtherDocuments { get; set; } = [];

        [JsonPropertyName("planningCode")] public string? PlanningCode { get; set; }

        [JsonPropertyName("propertyDescription")] public string? PropertyDescription { get; set; }

        [JsonPropertyName("propertyId")] public string? PropertyId { get; set; }

        [JsonPropertyName("propertyImages")] public List<string> PropertyImages { get; set; } = [];

        [JsonPropertyName("propertyName")] public string? PropertyName { get; set; }

        [JsonPropertyName("propertyType")] public string? PropertyType { get; set; }

        [JsonPropertyName("salesAgreement")] public string? SalesAgreement { get; set; }

        [JsonPropertyName("status")] public string? Status { get; set; }

        [JsonPropertyName("tenure")] public string? Tenure { get; set; }

        [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("userId")] public string? UserId { get; set; }

        /// <summary>
        /// A fresh <see cref="PropertyMetadata"/> in the shape the property views bind to.
        /// Fresh on every call on purpose: callers overwrite the chain-authoritative fields
        /// (financial figures, developer address, status) per listing, and this document is
        /// cached and shared between them.
        /// </summary>
        public PropertyMetadata ToPropertyMetadata() => new()
        {
            Status = Status,
            PropertyName = PropertyName,
            Financials = new PropertyFinancials
            {
                PropertyPrice = Finances?.PropertyPrice ?? 0,
                NumberOfTokens = Finances?.NumberOfShares ?? 0,
                PricePerToken = Finances?.SharePrice ?? 0,
                EstimatedRentalIncome = Finances?.EstimatedRentalIncome ?? 0,
                AnnualServiceCharge = Finances?.AnnualServiceCharge ?? 0,
                StampDutyTax = Finances?.StampDutyTax ?? 0,
                IsStampDutyPaid = Finances?.IsStampDutyPaid ?? false,
                IsAnnualServiceChargePaid = Finances?.IsAnnualServiceChargePaid ?? false,
            },
            // Files is what every view treats as the image list.
            Files = [.. PropertyImages],
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            Address = Address is null ? new PropertyAddress() : Address with { },
            Company = CompanyName is null && CompanyLogo is null
                ? null
                : new PropertyCompany
                {
                    Name = CompanyName,
                    Logo = CompanyLogo,
                },
            PropertyDescription = PropertyDescription,
            PropertyType = PropertyType,
            Map = Map,
            PlanningCode = PlanningCode,
            PropertyId = PropertyId,
            DeveloperAddress = CompanyWalletAddress ?? UserId,
            AccountAddress = UserId,
            Attributes = Attributes is null ? null : Attributes with { },
        };
    }

    /// <summary>The webapp format's <c>finances</c> object.</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public record XcavateSolanaPropertyFinances
    {
        [JsonPropertyName("propertyPrice")] public decimal PropertyPrice { get; set; }

        [JsonPropertyName("numberOfShares")] public int NumberOfShares { get; set; }

        [JsonPropertyName("sharePrice")] public decimal SharePrice { get; set; }

        [JsonPropertyName("estimatedRentalIncome")] public decimal EstimatedRentalIncome { get; set; }

        [JsonPropertyName("annualServiceCharge")] public decimal AnnualServiceCharge { get; set; }

        [JsonPropertyName("stampDutyTax")] public decimal StampDutyTax { get; set; }

        [JsonPropertyName("isStampDutyPaid")] public bool IsStampDutyPaid { get; set; }

        [JsonPropertyName("isAnnualServiceChargePaid")] public bool IsAnnualServiceChargePaid { get; set; }
    }

    /// <summary>
    /// Fetches and caches the property documents behind <c>PropertyAsset.metadataUri</c>.
    /// </summary>
    public static class XcavateSolanaPropertyMetadataClient
    {
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        /// <summary>
        /// Successful fetches only, keyed by URI. Failures are deliberately not cached:
        /// a metadata host that was briefly down should not blank the property for the
        /// rest of the session, and callers already degrade to chain-synthesized
        /// metadata on null.
        /// </summary>
        private static readonly ConcurrentDictionary<string, XcavateSolanaPropertyMetadata> cache = new();

        /// <summary>
        /// The document at <paramref name="metadataUri"/>, or null when the URI is empty
        /// (the on-chain field is empty until <c>init_property_assets</c> attaches it),
        /// not http(s), or the fetch or parse fails.
        /// </summary>
        public static async Task<XcavateSolanaPropertyMetadata?> GetAsync(string? metadataUri, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(metadataUri)
                || !Uri.TryCreate(metadataUri, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return null;
            }

            if (cache.TryGetValue(metadataUri, out var cached))
            {
                return cached;
            }

            try
            {
                var metadata = await httpClient.GetFromJsonAsync<XcavateSolanaPropertyMetadata>(uri, token).ConfigureAwait(false);

                if (metadata is not null)
                {
                    cache[metadataUri] = metadata;
                }

                return metadata;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"Failed to fetch property metadata from {metadataUri}:");
                Console.WriteLine(ex);

                return null;
            }
        }
    }
}
