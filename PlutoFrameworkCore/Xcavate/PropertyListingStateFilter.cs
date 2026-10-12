using System;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The marketplace feed's property-state filter - the filter popup's checkboxes -
    /// mapped onto the indexer's listing statuses by
    /// <see cref="XcavateMarketplaceIndexerModel.ToListingStatuses"/> and answered in SQL
    /// (ADR-39). The indexer has no PENDING_ASSETS value here on purpose: pre-sale rows
    /// never belonged to the feed, so no checkbox selects them.
    /// </summary>
    [Flags]
    public enum PropertyListingStateFilter
    {
        /// <summary>No state checked - the feed shows nothing.</summary>
        None = 0,

        /// <summary>LISTED - the listing is on sale and its shares can still be reserved.</summary>
        CanBeReserved = 1,

        /// <summary>
        /// SOLD_OUT + LEGAL - sold out and going through the legal/SPV phase while the
        /// reservers' claim window runs (ADR-35: tokens become claimable at sell-out).
        /// </summary>
        WaitingForClaim = 2,

        /// <summary>EXPIRED - the sale window closed before the listing sold out.</summary>
        Expired = 4,

        /// <summary>FINALIZED - settled; claiming is done and the property is fully sold.</summary>
        Finalized = 8,

        /// <summary>
        /// CANCELLED + REFUNDING - the listing died; whatever an investor paid in comes
        /// back through a refund.
        /// </summary>
        CancelledOrRefunding = 16,

        /// <summary>The feed's out-of-the-box selection: only reservable listings.</summary>
        Default = CanBeReserved,
    }
}
