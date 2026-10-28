namespace PlutoFramework.Model.Xcavate
{
    public static class SpvLawyerClaimEligibility
    {
        public sealed record State(
            bool LawyerMainPageSelected,
            bool HasCompliantLawyerRole,
            ushort? LawyerRegionId,
            string WalletAddress,
            string ListingStatus,
            bool SpvCreated,
            string SpvLawyer,
            string DeveloperLawyer,
            ushort? PropertyRegionId,
            long LegalDeadline,
            long SpvElectionExpiry,
            long SpvElectionCandidateCount);

        public static bool CanDisplay(State state, long nowUnixSeconds) =>
            state.LawyerMainPageSelected
            && state.HasCompliantLawyerRole
            && state.LawyerRegionId is not null
            && state.LawyerRegionId == state.PropertyRegionId
            && !string.IsNullOrEmpty(state.WalletAddress)
            && state.ListingStatus == "SoldOut"
            && state.SpvCreated
            && XcavateMarketplaceIndexerModel.IsVacantSeat(state.SpvLawyer)
            && !string.Equals(state.WalletAddress, state.DeveloperLawyer, StringComparison.Ordinal)
            && nowUnixSeconds <= state.LegalDeadline
            && (state.SpvElectionExpiry == 0 || nowUnixSeconds < state.SpvElectionExpiry)
            && state.SpvElectionCandidateCount < 5;

        public static bool CanSubmit(State state, bool ownCandidacyStanding, long nowUnixSeconds) =>
            !ownCandidacyStanding && CanDisplay(state, nowUnixSeconds);

        public static ulong GetClaimRound(ulong currentRound, long expiry) =>
            expiry > 0 ? currentRound : currentRound + 1;

        public static string FormatCostsQuote(ulong quote) =>
            ((decimal)quote / 1_000_000_000m).ToString(
                "0.#########", System.Globalization.CultureInfo.InvariantCulture);

        public static bool TryCreateCostsQuote(decimal costs, ulong collectedFeeQuote, out ulong quote)
        {
            quote = 0;

            if (costs < 0)
            {
                return false;
            }

            ulong candidate;
            try
            {
                candidate = XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(costs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            if (candidate > collectedFeeQuote)
            {
                return false;
            }

            quote = candidate;
            return true;
        }
    }
}
