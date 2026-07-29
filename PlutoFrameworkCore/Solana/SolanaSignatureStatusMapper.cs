using Solnet.Rpc.Models;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One entry of a <c>getSignatureStatuses</c> response, as a toast status.
    /// </summary>
    public static class SolanaSignatureStatusMapper
    {
        private const string Processed = "processed";
        private const string Confirmed = "confirmed";
        private const string Finalized = "finalized";

        /// <summary>
        /// Maps one signature's status. A null entry is <see cref="SolanaTransactionStatus.Pending"/>.
        /// </summary>
        /// <remarks>
        /// The null case is the one to get right. <c>getSignatureStatuses</c> returns null at
        /// the index of any signature the node has not observed, which is every transaction
        /// for its first moments — and, on a node that has dropped it from the recent cache,
        /// one that has been forgotten. Neither is a failure, and reading either as one paints
        /// a red toast over a transfer that is going through.
        ///
        /// The same reasoning covers an unrecognised confirmation level: a status string this
        /// client does not know is not evidence of anything. Only an explicit <c>err</c> means
        /// the transaction failed.
        /// </remarks>
        public static SolanaTransactionStatus Map(SignatureStatusInfo? status)
        {
            if (status is null)
            {
                return SolanaTransactionStatus.Pending;
            }

            var failed = status.Error is not null;

            // Finality is checked first: a transaction can be finalized and still have
            // failed, and reporting it as success is the worse of the two mistakes.
            if (Is(status.ConfirmationStatus, Finalized))
            {
                return failed
                    ? SolanaTransactionStatus.FinalizedFailed
                    : SolanaTransactionStatus.FinalizedSuccess;
            }

            if (Is(status.ConfirmationStatus, Confirmed))
            {
                return failed
                    ? SolanaTransactionStatus.ConfirmedFailed
                    : SolanaTransactionStatus.ConfirmedSuccess;
            }

            // An error attached before the level is known still means failure. A node does
            // not report an error against a transaction that succeeded, and holding it at
            // Pending would leave the toast spinning on a transfer that is already dead.
            if (failed)
            {
                return SolanaTransactionStatus.ConfirmedFailed;
            }

            // "processed", absent, or something this client has not heard of.
            return SolanaTransactionStatus.Pending;
        }

        private static bool Is(string? actual, string expected) =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
