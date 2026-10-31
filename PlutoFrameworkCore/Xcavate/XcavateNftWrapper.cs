using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using UniqueryPlus.Nfts;

namespace PlutoFrameworkCore.Xcavate
{
    public record XcavateNftWrapper : NftWrapper
    {
        public required uint TokensBought { get; set; }
        public required uint TokensOwned { get; set; }
        public required XcavateRegion? Region { get; set; }
        public required bool ListingHasExpired { get; set; }
        public TimeSpan? TimeLeftToBuy { get; set; } = null;
        public required bool SpvCreated { get; set; }
        public required bool ClaimHasExpired { get; set; }
        public TimeSpan? TimeLeftToClaim { get; set; } = null;

        private bool hasClaimableRent = false;

        /// <summary>
        /// Whether the connected wallet currently has claimable rent on this property.
        /// Computed by the portfolio page's income scan (indexer income state x the
        /// holding's share count) and bound to the thumbnail's "Claim rent" chip;
        /// false until the scan says otherwise.
        /// </summary>
        public bool HasClaimableRent
        {
            get => hasClaimableRent;
            set
            {
                if (hasClaimableRent != value)
                {
                    hasClaimableRent = value;
                    OnPropertyChanged(nameof(HasClaimableRent));
                }
            }
        }
        public string? Status
        {
            get
            {
                var ListingDetails = ((INftXcavateOngoingObjectListing)NftBase).OngoingObjectListingDetails;

                if (ListingDetails is null)
                {
                    return null;
                }

                if (NftBase is XcavateSolanaListingNft solanaListing
                    && solanaListing.LegalProcessHasExpired(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                {
                    return "Legal process expired";
                }

                if (ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0)
                {
                    return "Can be refunded";
                }

                if (ListingHasExpired && ListingDetails?.ListedTokens > 0)
                {
                    return "Listing expired";
                }

                if (!ListingHasExpired && ListingDetails?.ListedTokens > 0)
                {
                    return TimeModel.GetTimeLeftText(TimeLeftToBuy.Value, "to reserve");
                }

                if (ListingDetails?.ListedTokens == 0 && !SpvCreated)
                {
                    return "SPV to be created";
                }

                if (ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensOwned > 0)
                {
                    return "Can be refunded";
                }

                if (ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
                {
                    return "Can be refunded";
                }

                if (ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0)
                {
                    return "Claim period expired";
                }

                if (!ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
                {
                    return TimeModel.GetTimeLeftText(TimeLeftToClaim.Value, "to claim");
                }

                if (!ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought == 0)
                {
                    return TimeModel.GetTimeLeftText(TimeLeftToClaim.Value, "to claim");
                }

                if (ListingDetails?.UnclaimedTokens == 0 && TokensOwned > 0)
                {
                    return null;
                }

                if (!ListingHasExpired && ListingDetails?.ListedTokens == 0 && TokensBought == 0 && TokensOwned == 0)
                {
                    return "Sold out";
                }

                return null;
            }
        }

        public bool StatusIsVisible => Status is not null;
    }
}
