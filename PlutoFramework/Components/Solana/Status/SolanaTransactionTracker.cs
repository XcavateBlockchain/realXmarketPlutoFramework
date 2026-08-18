using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status
{
    /// <summary>
    /// Follows a submitted transaction to finality by polling the cluster.
    /// </summary>
    /// <remarks>
    /// Solana has no equivalent of <c>SubmitAndWatchExtrinsicAsync</c>, which is how the
    /// Substrate side gets pushed status updates. <c>getSignatureStatuses</c> is the
    /// documented way to ask, so this polls it.
    /// </remarks>
    public static class SolanaTransactionTracker
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How long a transaction may go unseen before it is called dropped. A blockhash is
        /// valid for roughly 150 slots — about 60 seconds — after which the transaction can no
        /// longer land, so past this "dropped" is a fact rather than a guess.
        /// </summary>
        private static readonly TimeSpan UnseenTimeout = TimeSpan.FromSeconds(90);

        /// <summary>
        /// When to stop polling regardless. A transaction confirmed but not yet finalized is a
        /// real state, so the last known status is left on screen rather than replaced.
        /// </summary>
        private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Raised the first time a transaction reaches a confirmed-success state, so balances
        /// on screen stop showing the pre-transfer figures. The Solana counterpart of the
        /// <c>MainPageLayoutUpdater.ReloadAsync</c> call the Substrate tracker makes in-block.
        /// </summary>
        /// <remarks>
        /// Static, so every subscriber must unsubscribe or it keeps their view model alive —
        /// the same trap <c>SolanaNetworkModel.ClusterChanged</c> documents.
        /// </remarks>
        public static event EventHandler? TransactionConfirmed;

        public static async Task TrackAsync(
            string signature,
            SolanaCluster cluster,
            SolanaTransactionInfo info,
            CancellationToken token)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var announcedConfirmation = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PollInterval, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                SolanaTransactionStatus status;

                try
                {
                    var signatureStatus = await SolanaRpcModel.GetSignatureStatusAsync(
                        cluster, signature, token);

                    status = SolanaSignatureStatusMapper.Map(signatureStatus);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // One failed poll is a network blip, not an outcome. Losing a two-second
                    // tick must not end tracking or invent a status.
                    continue;
                }

                var elapsed = DateTimeOffset.UtcNow - startedAt;

                // Nothing seen for long enough that the blockhash cannot still be valid.
                if (status == SolanaTransactionStatus.Pending && elapsed > UnseenTimeout)
                {
                    SetStatus(info, SolanaTransactionStatus.Dropped);

                    return;
                }

                SetStatus(info, status);

                if (!announcedConfirmation && IsConfirmedSuccess(status))
                {
                    announcedConfirmation = true;

                    MainThread.BeginInvokeOnMainThread(
                        () => TransactionConfirmed?.Invoke(null, EventArgs.Empty));
                }

                if (status is SolanaTransactionStatus.FinalizedSuccess
                    or SolanaTransactionStatus.FinalizedFailed)
                {
                    return;
                }

                if (elapsed > TotalTimeout)
                {
                    // Leaves the last known status showing. Overwriting a genuine "confirmed"
                    // with a timeout would report a worse outcome than actually happened.
                    return;
                }
            }
        }

        private static bool IsConfirmedSuccess(SolanaTransactionStatus status) =>
            status is SolanaTransactionStatus.ConfirmedSuccess
                or SolanaTransactionStatus.FinalizedSuccess;

        private static void SetStatus(SolanaTransactionInfo info, SolanaTransactionStatus status)
        {
            if (info.Status == status)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => info.Status = status);
        }
    }
}
