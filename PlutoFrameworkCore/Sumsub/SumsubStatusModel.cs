using System.Globalization;

namespace PlutoFramework.Model.Sumsub
{
    /// <summary>
    /// High-level classification of a Sumsub applicant verification outcome.
    /// </summary>
    public enum SumsubStatusType
    {
        NotReviewed,
        Pending,
        Approved,
        Rejected,
        NeedsResubmit,
    }

    /// <summary>
    /// Carries the parsed verification status for UI consumption.
    /// </summary>
    public record SumsubStatusData
    {
        public required SumsubStatusType StatusType { get; init; }
        public required string ReviewStatus { get; init; } = "";
        public required string Reason { get; init; } = "";
        public required DateTime Timestamp { get; init; }
        public required int AttemptCount { get; init; }
        public required string? ReviewId { get; init; }
        public required string? AttemptId { get; init; }
        public required string? LevelName { get; init; }
        public required string ApplicantId { get; init; } = "";
        public required string ApplicantEmail { get; init; } = "";
        public required string ApplicantExternalUserId { get; init; } = "";
    }

    /// <summary>
    /// Parses a SumsubApplicant into a structured SumsubStatusData.
    /// </summary>
    public static class SumsubStatusModelParser
    {
        /// <summary>
        /// Derives a typed status from the raw Sumsub applicant data.
        /// Maps reviewStatus values (approved, rejected, pending, needsAction) to SumsubStatusType.
        /// </summary>
        public static SumsubStatusData ParseStatus(SumsubApplicant applicant)
        {
            var review = applicant.Review;

            SumsubStatusType statusType = SumsubStatusType.NotReviewed;
            string reviewStatus = "";
            string reason = "";
            DateTime timestamp = applicant.CreatedAtDateTime;
            int attemptCount = 0;

            if (review != null)
            {
                reviewStatus = review.ReviewStatus ?? "";
                attemptCount = review.AttemptCnt ?? 0;

                if (!string.IsNullOrEmpty(review.CreateDate))
                {
                    try
                    {
                        timestamp = DateTime.ParseExact(
                            review.CreateDate,
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                        );
                    }
                    catch
                    {
                        // keep createdAt as fallback
                    }
                }

                statusType = reviewStatus.ToLowerInvariant() switch
                {
                    "approved"      => SumsubStatusType.Approved,
                    "rejected"      => SumsubStatusType.Rejected,
                    "pending"       => SumsubStatusType.Pending,
                    "needsaction"   => SumsubStatusType.NeedsResubmit,
                    "deleted"       => SumsubStatusType.NotReviewed,
                    _               => SumsubStatusType.NotReviewed,
                };
            }

            // Extract rejection/resubmit reason
            if (statusType is SumsubStatusType.Rejected or SumsubStatusType.NeedsResubmit)
            {
                reason = reviewStatus.ToLowerInvariant() switch
                {
                    "rejected" => "Your verification was declined. Please try again or contact support for assistance.",
                    "needsaction" => "Additional information or correction is needed for your verification documents. Please resubmit.",
                    _ => "Verification could not be completed. Please try again.",
                };
            }

            return new SumsubStatusData
            {
                StatusType = statusType,
                ReviewStatus = reviewStatus,
                Reason = reason,
                Timestamp = timestamp,
                AttemptCount = attemptCount,
                ReviewId = review?.ReviewId,
                AttemptId = review?.AttemptId,
                LevelName = review?.LevelName,
                ApplicantId = applicant.Id ?? "",
                ApplicantEmail = applicant.Email ?? "",
                ApplicantExternalUserId = applicant.ExternalUserId ?? "",
            };
        }
    }
}
