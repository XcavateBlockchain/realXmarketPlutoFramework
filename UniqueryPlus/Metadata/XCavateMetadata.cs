using System.Text.Json.Serialization;

namespace UniqueryPlus.Metadata
{
    public record XcavateNftMetadata
    {
        [JsonPropertyName("data")] public required PropertyMetadata Data { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public record PropertyMetadata
    {
        [JsonPropertyName("status")] public string? Status { get; set; }

        [JsonPropertyName("propertyName")] public string? PropertyName { get; set; }

        [JsonPropertyName("financials")] public required PropertyFinancials Financials { get; set; }

        [JsonPropertyName("files")] public List<string> Files { get; set; } = [];

        /// <summary>
        /// The indexer's compressed (720x720 JPEG) mirrors of <see cref="Files"/>, in the
        /// same order. Only populated by the GraphQL indexer feed; empty when the mirror
        /// has not uploaded for the asset (or is not configured), which is also the
        /// fallback trigger for <see cref="DisplayImages"/>.
        /// </summary>
        [JsonPropertyName("thumbnailFiles")] public List<string> ThumbnailFiles { get; set; } = [];

        /// <summary>
        /// The image list every surface except the full-screen image page should bind to:
        /// the compressed <see cref="ThumbnailFiles"/> when the indexer supplied them, the
        /// full-resolution <see cref="Files"/> otherwise.
        /// </summary>
        [JsonIgnore] public IReadOnlyList<string> DisplayImages => ThumbnailFiles.Count > 0 ? ThumbnailFiles : Files;

        [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("address")] public required PropertyAddress Address { get; set; }

        [JsonPropertyName("company")] public PropertyCompany? Company { get; set; }

        [JsonPropertyName("propertyDescription")] public string? PropertyDescription { get; set; }

        [JsonPropertyName("propertyType")] public string? PropertyType { get; set; }

        [JsonPropertyName("map")] public string? Map { get; set; }

        [JsonPropertyName("developerAddress")] public string? DeveloperAddress { get; set; }

        [JsonPropertyName("planningCode")] public string? PlanningCode { get; set; }

        [JsonPropertyName("propertyId")] public string? PropertyId { get; set; }

        [JsonPropertyName("id")] public Guid Id { get; set; }

        [JsonPropertyName("accountAddress")] public string? AccountAddress { get; set; }

        [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("legalRepresentative")] public string? LegalRepresentative { get; set; }

        [JsonPropertyName("attributes")] public PropertyAttributes? Attributes { get; set; }
    }

    public record PropertyAttributes
    {
        [JsonPropertyName("area")] public string? Area { get; set; }

        [JsonPropertyName("offStreetParking")] public string? OffStreetParking { get; set; }

        [JsonPropertyName("outdoorSpace")] public string? OutdoorSpace { get; set; }

        [JsonPropertyName("numberOfBedrooms")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? NumberOfBedrooms { get; set; }

        [JsonPropertyName("constructionDate")] public string? ConstructionDate { get; set; }

        [JsonPropertyName("numberOfBathrooms")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? NumberOfBathrooms { get; set; }

        [JsonPropertyName("quality")] public string? Quality { get; set; }
    }

    public record PropertyAddress
    {
        [JsonPropertyName("postCode")] public string? PostCode { get; set; }

        [JsonPropertyName("flatOrUnit")] public string? FlatOrUnit { get; set; }

        [JsonPropertyName("localAuthority")] public string? LocalAuthority { get; set; }

        [JsonPropertyName("street")] public string? Street { get; set; }

        [JsonPropertyName("townCity")] public string? TownCity { get; set; }
    }

    public record PropertyCompany
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("logo")] public string? Logo { get; set; }
    }

    public record PropertyFinancials
    {
        [JsonPropertyName("stampDutyTax")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal StampDutyTax { get; set; }

        [JsonPropertyName("isAnnualServiceChargePaid")] public bool IsAnnualServiceChargePaid { get; set; }

        [JsonPropertyName("estimatedRentalIncome")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal EstimatedRentalIncome { get; set; }

        [JsonPropertyName("pricePerToken")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal PricePerToken { get; set; }

        [JsonPropertyName("numberOfTokens")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int NumberOfTokens { get; set; }

        [JsonPropertyName("isStampDutyPaid")] public bool IsStampDutyPaid { get; set; }

        [JsonPropertyName("propertyPrice")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal PropertyPrice { get; set; }

        [JsonPropertyName("annualServiceCharge")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal AnnualServiceCharge { get; set; }
    }
}
