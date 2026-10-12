using System;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The marketplace feed's property-state filter - the filter popup's checkboxes -
    /// mapped one-to-one onto the indexer's derived feed states by
    /// <see cref="XcavateMarketplaceIndexerModel.ToFeedStates"/> and answered in SQL
    /// (ADR-39). The indexer applies the wall-clock and availability overlays
    /// (<c>listing_expiry</c>, <c>claim_deadline</c>, available shares), so e.g. a
    /// listing still carrying the LISTED chain status past its sale deadline counts as
    /// Expired, not CanBeReserved. PENDING_ASSETS has no value here on purpose:
    /// pre-sale rows never belonged to the feed, so no checkbox selects them.
    /// </summary>
    [Flags]
    public enum PropertyListingStateFilter
    {
        /// <summary>No state checked - the feed shows nothing.</summary>
        None = 0,

        /// <summary>On sale: LISTED, sale window open, shares still available to reserve.</summary>
        CanBeReserved = 1,

        /// <summary>
        /// Fully committed (nothing left to buy) with the claim window pending or open:
        /// the SPV creation ("SPV to be created") and the claiming phase.
        /// </summary>
        WaitingForClaim = 2,

        /// <summary>SOLD_OUT or LEGAL - every share sold, the legal/settlement process runs.</summary>
        SoldOut = 4,

        /// <summary>
        /// EXPIRED on chain, or LISTED past its sale deadline with shares left (the chain
        /// status lags the wall clock until someone cranks withdraw_expired), or the claim
        /// window lapsed while shares stayed unclaimed ("claim period expired").
        /// </summary>
        Expired = 8,

        /// <summary>FINALIZED - settled; claiming is done and the property is fully sold.</summary>
        Finalized = 16,

        /// <summary>
        /// CANCELLED or REFUNDING - the listing died; whatever an investor paid in comes
        /// back through a refund.
        /// </summary>
        CancelledOrRefunding = 32,

        /// <summary>The feed's out-of-the-box selection: only reservable, unexpired listings.</summary>
        Default = CanBeReserved,
    }
}
