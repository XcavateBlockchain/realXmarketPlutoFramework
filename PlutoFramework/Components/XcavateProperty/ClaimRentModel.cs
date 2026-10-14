using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// Runs rent claims (claim_income, with close_income_checkpoint riding inside when
    /// the checkpoint drains) through the marketplace transaction pipeline. Every claim
    /// entry point - the portfolio page's "Claim all" card and the per-property "Claim
    /// rent" chips and the property detail's income section - goes through here, so a
    /// single lock keeps every claim strictly sequential: one status popup per property,
    /// never two submissions racing the same popup view model.
    /// </summary>
    public static class ClaimRentModel
    {
        /// <summary>One property to claim the caller's rent from.</summary>
        public sealed record ClaimTarget(long AssetId, string PropertyName);

        private static readonly SemaphoreSlim ClaimLock = new(1, 1);

        /// <summary>True while any claim (single or claim-all) is being built, signed or submitted.</summary>
        public static bool ClaimInProgress => ClaimLock.CurrentCount == 0;

        /// <summary>
        /// Raised when <see cref="ClaimInProgress"/> flips, on whichever thread the claim
        /// ran on - subscribers that touch UI must marshal to the main thread. Static:
        /// the same never-unsubscribe convention as
        /// <see cref="XcavateMarketplaceTransactionModel.TransactionConfirmed"/> applies.
        /// </summary>
        public static event EventHandler? ClaimInProgressChanged;

        /// <summary>
        /// Claims the connected wallet's rent on each of <paramref name="targets"/>, one
        /// property at a time, in order. Each property gets its own
        /// <see cref="XcavateMarketplaceTransactionModel.SubmitAsync"/> call (its own
        /// status popup naming the property); a failure is reported by that popup and
        /// does not stop the remaining properties. Waits its turn when another claim is
        /// already running.
        /// </summary>
        public static async Task ClaimAsync(IReadOnlyList<ClaimTarget> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }

            await ClaimLock.WaitAsync().ConfigureAwait(false);

            try
            {
                ClaimInProgressChanged?.Invoke(null, EventArgs.Empty);

                foreach (var target in targets)
                {
                    var assetId = target.AssetId;

                    await XcavateMarketplaceTransactionModel.SubmitAsync(
                        $"Claim rent for {target.PropertyName}",
                        (holder, cluster, ct) => XcavatePropertyCallsModel.ClaimIncomeAsync(
                            cluster, holder, assetId, ct)).ConfigureAwait(false);
                }
            }
            finally
            {
                ClaimLock.Release();
                ClaimInProgressChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
