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

            DateTime timestamp = applicant.CreatedAtDateTime;
            int attemptCount = 0;

            var statusType = applicant switch
            {
                // 1. Passed correctly
                { Review.ReviewResult.ReviewAnswer: "GREEN" }
                    => SumsubStatusType.Approved,

                // 2. Minor violation - needs to fix and resubmit
                { Review.ReviewResult.ReviewAnswer: "RED", Review.ReviewResult.ReviewRejectType: "RETRY" }
                    => SumsubStatusType.NeedsResubmit,

                // 3. Major violation - hard rejected
                { Review.ReviewResult.ReviewAnswer: "RED", Review.ReviewResult.ReviewRejectType: "FINAL" }
                    => SumsubStatusType.Rejected,

                // 4. Pending - Sumsub is actively working on it
                { Review.ReviewStatus: "pending" or "queued" or "onHold" or "prechecked" }
                    => SumsubStatusType.Pending,

                // 4b. Pending (Catch-all) - The review exists and has started, but has no answer yet
                { Review.ReviewResult.ReviewAnswer: null, Review.ReviewStatus: not null and not "init" }
                    => SumsubStatusType.Pending,

                // 5. Default fallback (Applicant created but review is "init", deleted, or Review is completely null)
                _ => SumsubStatusType.NotReviewed
            };

            // Extract rejection/resubmit reason
            string reason = statusType switch
            {
                SumsubStatusType.Rejected => "Your verification was declined. Contact support for assistance.",
                SumsubStatusType.NeedsResubmit => "Additional information or correction is needed for your verification documents. Please resubmit.",
                _ => "",
            };

            return new SumsubStatusData
            {
                StatusType = statusType,
                ReviewStatus = review?.ReviewStatus ?? "",
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
