using System.Globalization;
using System.Text.Json.Serialization;

namespace PlutoFramework.Model.Sumsub
{
    public record SumsubApplicant
    {
        [JsonPropertyName("id")] public string? Id { get; init; }

        [JsonPropertyName("createdAt")] public string? CreatedAt { get; init; }
        public DateTime CreatedAtDateTime => DateTime.ParseExact(
            CreatedAt,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );

        [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }
        [JsonPropertyName("key")] public required string Key { get; init; }
        [JsonPropertyName("clientId")] public string? ClientId { get; init; }
        [JsonPropertyName("inspectionId")] public string? InspectionId { get; init; }
        [JsonPropertyName("externalUserId")] public string? ExternalUserId { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("phone")] public string? Phone { get; init; }
        [JsonPropertyName("applicantPlatform")] public required string ApplicantPlatform { get; init; }
        [JsonPropertyName("requiredIdDocs")] public SumsubRequiredIdDocs? RequiredIdDocs { get; init; }
        [JsonPropertyName("review")] public SumsubReview? Review { get; init; }
        [JsonPropertyName("lang")] public string? Lang { get; init; }
        [JsonPropertyName("type")] public string? Type { get; init; }
    }

    public record SumsubRequiredIdDocs
    {
        [JsonPropertyName("docSets")] public required List<SumsubDocSet> DocSets { get; init; }
    }

    public record SumsubDocSet
    {
        [JsonPropertyName("idDocSetType")] public string? IdDocSetType { get; init; }
        [JsonPropertyName("types")] public List<string>? Types { get; init; }
        [JsonPropertyName("videoRequired")] public string? VideoRequired { get; init; }
    }

    public record SumsubReview
    {
        [JsonPropertyName("reviewId")] public string? ReviewId { get; init; }
        [JsonPropertyName("attemptId")] public string? AttemptId { get; init; }
        [JsonPropertyName("attemptCnt")] public int? AttemptCnt { get; init; }
        [JsonPropertyName("levelName")] public string? LevelName { get; init; }
        [JsonPropertyName("levelAutoCheckMode")] public string? LevelAutoCheckMode { get; init; }
        [JsonPropertyName("createDate")] public string? CreateDate { get; init; }
        [JsonPropertyName("reviewDate")] public string? ReviewDate { get; init; }
        [JsonPropertyName("reviewStatus")] public string? ReviewStatus { get; init; }
        [JsonPropertyName("reviewResult")] public SumsubReviewResult? ReviewResult { get; init; }
        [JsonPropertyName("priority")] public int? Priority { get; init; }
    }

    public record SumsubReviewResult
    {
        [JsonPropertyName("reviewAnswer")] public string? ReviewAnswer { get; init; }
        [JsonPropertyName("rejectLabels")] public List<string>? RejectLabels { get; init; }
        [JsonPropertyName("reviewRejectType")] public string? ReviewRejectType { get; init; }
        [JsonPropertyName("clientComment")] public string? ClientComment { get; init; }
        [JsonPropertyName("moderationComment")] public string? ModerationComment { get; init; }
        [JsonPropertyName("buttonIds")] public List<string>? ButtonIds { get; init; }
    }

    public record SumsubReviewHistoryResponse
    {
        [JsonPropertyName("items")] public List<SumsubReviewHistoryItem>? Items { get; init; }
        [JsonPropertyName("totalItems")] public int? TotalItems { get; init; }
    }

    public record SumsubReviewHistoryItem
    {
        [JsonPropertyName("attemptId")] public string? AttemptId { get; init; }
        [JsonPropertyName("levelName")] public string? LevelName { get; init; }
        [JsonPropertyName("reviewDate")] public string? ReviewDate { get; init; }
        [JsonPropertyName("reviewResult")] public SumsubReviewResult? ReviewResult { get; init; }
        [JsonPropertyName("reviewStatus")] public string? ReviewStatus { get; init; }
    }

    public record SumsubVerificationStepStatus
    {
        [JsonPropertyName("reviewResult")] public SumsubReviewResult? ReviewResult { get; init; }
        [JsonPropertyName("country")] public string? Country { get; init; }
        [JsonPropertyName("idDocType")] public string? IdDocType { get; init; }
        [JsonPropertyName("imageIds")] public List<string>? ImageIds { get; init; }
        [JsonPropertyName("imageReviewResults")] public Dictionary<string, SumsubReviewResult>? ImageReviewResults { get; init; }
        [JsonPropertyName("reviewStatus")] public string? ReviewStatus { get; init; }
    }

    public record SumsubApplicantNotesResponse
    {
        [JsonPropertyName("list")] public SumsubApplicantNotesList? List { get; init; }
    }

    public record SumsubApplicantNotesList
    {
        [JsonPropertyName("items")] public List<SumsubApplicantNote>? Items { get; init; }
        [JsonPropertyName("totalItems")] public int? TotalItems { get; init; }
    }

    public record SumsubApplicantNote
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("applicantId")] public string? ApplicantId { get; init; }
        [JsonPropertyName("note")] public string? Note { get; init; }
        [JsonPropertyName("createdAt")] public string? CreatedAt { get; init; }
        [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }
        [JsonPropertyName("updatedAt")] public string? UpdatedAt { get; init; }
        [JsonPropertyName("updatedBy")] public string? UpdatedBy { get; init; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    }
}
