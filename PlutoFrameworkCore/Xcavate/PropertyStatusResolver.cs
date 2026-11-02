using PlutoFrameworkCore;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    public static class PropertyStatusResolver
    {
        public static string? Resolve(
            XcavateNftWrapper wrapper,
            long nowUnixSeconds,
            MainPageRole? activeRole = null)
        {
            if (wrapper.NftBase is not INftXcavateOngoingObjectListing ongoing
                || ongoing.OngoingObjectListingDetails is not { } listingDetails)
            {
                return null;
            }

            if (wrapper.NftBase is not XcavateSolanaListingNft listing)
            {
                return ResolveLegacy(wrapper, listingDetails);
            }

            return (activeRole ?? wrapper.ActiveRole) switch
            {
                MainPageRole.Developer => ResolveDeveloper(wrapper, listing, listingDetails, nowUnixSeconds),
                MainPageRole.Lawyer => ResolveLawyer(wrapper, listing, nowUnixSeconds),
                MainPageRole.LettingAgent => ResolveLettingAgent(wrapper, listing, nowUnixSeconds),
                _ => ResolveInvestor(wrapper, listing, listingDetails, nowUnixSeconds),
            };
        }

        private static string? ResolveInvestor(
            XcavateNftWrapper wrapper,
            XcavateSolanaListingNft listing,
            XcavateOngoingObjectListingDetails details,
            long now)
        {
            if (listing.LegalProcessHasExpired(now))
            {
                return "Legal process expired";
            }

            var hasPosition = wrapper.TokensBought > 0 || wrapper.TokensOwned > 0;

            if (listing.IsCancelled)
            {
                return hasPosition ? "Can be refunded" : "Cancelled";
            }

            if (listing.ListingStatus == "Refunding")
            {
                return "Refunding";
            }

            if (listing.ListingStatus == "Expired"
                || (listing.ListingStatus == "Listed" && details.ListedTokens > 0 && now >= listing.ListingExpiryTimestamp))
            {
                return wrapper.TokensBought > 0 ? "Can be refunded" : "Listing expired";
            }

            if (listing.ListingStatus == "PendingAssets")
            {
                return "Preparing assets";
            }

            if (listing.ListingStatus == "Finalized")
            {
                return "Finalized";
            }

            if (listing.ListingStatus == "Legal")
            {
                return "Ready to settle";
            }

            if (listing.ListingStatus == "SoldOut")
            {
                return "Sold out";
            }

            if (listing.ListingStatus != "Listed")
            {
                return null;
            }

            if (details.ListedTokens > 0)
            {
                return Countdown(wrapper.TimeLeftToBuy, "to reserve", "For sale");
            }

            if (!wrapper.SpvCreated)
            {
                return "SPV to be created";
            }

            if (details.UnclaimedTokens > 0)
            {
                if (ClaimHasExpired(listing, now))
                {
                    return hasPosition ? "Can be refunded" : "Claim period expired";
                }

                return Countdown(wrapper.TimeLeftToClaim, "to claim", "Claim period open");
            }

            return "Sold out";
        }

        private static string? ResolveDeveloper(
            XcavateNftWrapper wrapper,
            XcavateSolanaListingNft listing,
            XcavateOngoingObjectListingDetails details,
            long now)
        {
            if (!IsViewer(wrapper, listing.Owner) && !IsViewer(wrapper, details.RealEstateDeveloper))
            {
                return ResolveGeneric(wrapper, listing, details, now);
            }

            if (listing.ListingStatus == "PendingAssets")
            {
                return "Assets to initialize";
            }

            if (listing.ListingStatus == "Listed")
            {
                if (details.ListedTokens > 0)
                {
                    return now >= listing.ListingExpiryTimestamp ? "Listing expired" : "Sale active";
                }

                if (!wrapper.SpvCreated)
                {
                    return "SPV to be created";
                }

                if (details.UnclaimedTokens > 0)
                {
                    return ClaimHasExpired(listing, now) ? "Claim period expired" : "Claim period open";
                }

                return "Sold out";
            }

            if (listing.ListingStatus == "SoldOut" && !listing.LegalProcessHasExpired(now))
            {
                if (XcavateMarketplaceIndexerModel.IsVacantSeat(listing.DeveloperLawyer))
                {
                    return "Assign developer lawyer";
                }

                if (XcavateMarketplaceIndexerModel.IsVacantSeat(listing.SpvLawyer))
                {
                    return "Waiting for SPV lawyer";
                }

                if (listing.DeveloperLawyerDocStatus == DocumentStatus.Rejected
                    || listing.SpvLawyerDocStatus == DocumentStatus.Rejected)
                {
                    return "Documents to revise";
                }

                return "Legal review in progress";
            }

            return ResolveGeneric(wrapper, listing, details, now);
        }

        private static string? ResolveLawyer(
            XcavateNftWrapper wrapper,
            XcavateSolanaListingNft listing,
            long now)
        {
            if (!XcavateMarketplaceIndexerModel.IsVacantSeat(listing.DeveloperLawyer)
                && IsViewer(wrapper, listing.DeveloperLawyer))
            {
                return LawyerStatus("Developer lawyer", listing.DeveloperLawyerDocStatus);
            }

            if (!XcavateMarketplaceIndexerModel.IsVacantSeat(listing.SpvLawyer)
                && IsViewer(wrapper, listing.SpvLawyer))
            {
                return LawyerStatus("SPV lawyer", listing.SpvLawyerDocStatus);
            }

            if (wrapper.RoleContext?.HasClaimedSpvCase == true)
            {
                return "Already claimed";
            }

            if (wrapper.RoleContext?.CanClaimSpvCase == true)
            {
                return "Can be claimed";
            }

            if (listing.ListingStatus == "SoldOut"
                && !listing.LegalProcessHasExpired(now)
                && !XcavateMarketplaceIndexerModel.IsVacantSeat(listing.SpvLawyer))
            {
                return "SPV lawyer assigned";
            }

            var details = ((INftXcavateOngoingObjectListing)listing).OngoingObjectListingDetails;
            return details is null ? null : ResolveGeneric(wrapper, listing, details, now);
        }

        private static string? ResolveLettingAgent(
            XcavateNftWrapper wrapper,
            XcavateSolanaListingNft listing,
            long now)
        {
            var details = ((INftXcavateOngoingObjectListing)listing).OngoingObjectListingDetails;
            if (!listing.IsFinalized)
            {
                return details is null ? null : ResolveGeneric(wrapper, listing, details, now);
            }

            var context = wrapper.RoleContext;
            if (context is null)
            {
                return details is null ? null : ResolveGeneric(wrapper, listing, details, now);
            }

            var ownSeat = context is not null
                && !XcavateMarketplaceIndexerModel.IsVacantSeat(context.LettingAgent)
                && IsViewer(wrapper, context.LettingAgent);

            if (ownSeat && context!.ResignationDueTimestamp is not null)
            {
                return "Resignation pending";
            }

            if (context?.ActiveChallengeId > 0 && IsViewer(wrapper, context.ActiveChallengeAgent))
            {
                return "Challenge against you active";
            }

            if (context?.ActiveProposalId > 0 && IsViewer(wrapper, context.ActiveProposalProposer))
            {
                return "Your proposal is active";
            }

            if (ownSeat)
            {
                return context!.ActiveChallengeId > 0 || context.ActiveProposalId > 0
                    ? "Governance vote active"
                    : "Managing property";
            }

            if (context?.HasClaimedProperty == true)
            {
                return "Already claimed";
            }

            if (context?.CanClaimProperty == true)
            {
                return "Can be claimed";
            }

            if (!XcavateMarketplaceIndexerModel.IsVacantSeat(context?.LettingAgent))
            {
                return "Letting agent assigned";
            }

            if (context?.LettingElectionCandidateCount > 0 && context.LettingElectionExpiry != 0)
            {
                if (now >= context.LettingElectionExpiry)
                {
                    return "Agent election to finalize";
                }

                return TimeModel.GetTimeLeftText(
                    TimeSpan.FromSeconds(context.LettingElectionExpiry - now), "to vote");
            }

            return "Letting seat vacant";
        }

        private static string? ResolveGeneric(
            XcavateNftWrapper wrapper,
            XcavateSolanaListingNft listing,
            XcavateOngoingObjectListingDetails details,
            long now)
        {
            switch (listing.ListingStatus)
            {
                case "PendingAssets":
                    return "Preparing assets";
                case "Expired":
                    return "Listing expired";
                case "Cancelled":
                    return "Cancelled";
                case "Refunding":
                    return "Refunding";
                case "Finalized":
                    return "Finalized";
                case "Legal":
                    return listing.LegalProcessHasExpired(now) ? "Legal process expired" : "Ready to settle";
                case "SoldOut":
                    return listing.LegalProcessHasExpired(now) ? "Legal process expired" : "Legal review in progress";
                case "Listed":
                    break;
                default:
                    return null;
            }

            if (details.ListedTokens > 0)
            {
                return now >= listing.ListingExpiryTimestamp ? "Listing expired" : "For sale";
            }

            if (!wrapper.SpvCreated)
            {
                return "SPV to be created";
            }

            if (details.UnclaimedTokens > 0)
            {
                return ClaimHasExpired(listing, now) ? "Claim period expired" : "Claim period open";
            }

            return "Sold out";
        }

        private static string? ResolveLegacy(
            XcavateNftWrapper wrapper,
            XcavateOngoingObjectListingDetails details)
        {
            if (wrapper.ListingHasExpired && details.ListedTokens > 0 && wrapper.TokensBought > 0)
            {
                return "Can be refunded";
            }

            if (wrapper.ListingHasExpired && details.ListedTokens > 0)
            {
                return "Listing expired";
            }

            if (!wrapper.ListingHasExpired && details.ListedTokens > 0)
            {
                return Countdown(wrapper.TimeLeftToBuy, "to reserve", "For sale");
            }

            if (details.ListedTokens == 0 && !wrapper.SpvCreated)
            {
                return "SPV to be created";
            }

            if (wrapper.ClaimHasExpired && details.UnclaimedTokens > 0
                && (wrapper.TokensOwned > 0 || wrapper.TokensBought > 0))
            {
                return "Can be refunded";
            }

            if (wrapper.ClaimHasExpired && details.UnclaimedTokens > 0)
            {
                return "Claim period expired";
            }

            if (!wrapper.ClaimHasExpired && details.UnclaimedTokens > 0)
            {
                return Countdown(wrapper.TimeLeftToClaim, "to claim", "Claim period open");
            }

            if (details.UnclaimedTokens == 0 && wrapper.TokensOwned > 0)
            {
                return null;
            }

            if (!wrapper.ListingHasExpired && details.ListedTokens == 0
                && wrapper.TokensBought == 0 && wrapper.TokensOwned == 0)
            {
                return "Sold out";
            }

            return null;
        }

        private static string LawyerStatus(string side, DocumentStatus status) => status switch
        {
            DocumentStatus.Approved => $"{side}: documents approved",
            DocumentStatus.Rejected => $"{side}: documents rejected",
            _ => $"{side}: review pending",
        };

        private static bool ClaimHasExpired(XcavateSolanaListingNft listing, long now) =>
            listing.IsTornDown
            || (listing.ClaimDeadlineTimestamp != 0 && now >= listing.ClaimDeadlineTimestamp);

        private static string Countdown(TimeSpan? timeLeft, string suffix, string fallback) =>
            timeLeft is { } value ? TimeModel.GetTimeLeftText(value, suffix) : fallback;

        private static bool IsViewer(XcavateNftWrapper wrapper, string? address) =>
            !string.IsNullOrEmpty(wrapper.ViewerAddress)
            && string.Equals(wrapper.ViewerAddress, address, StringComparison.Ordinal);
    }
}
