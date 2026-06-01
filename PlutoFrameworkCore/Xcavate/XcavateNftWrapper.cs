using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

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
        public TimeSpan? TimeLeftToClaim { get; set; } = null;
        public string? Status
        {
            get
            {
                if (TimeLeftToBuy != null)
                {
                    return TimeModel.GetTimeLeftText(TimeLeftToBuy.Value, " to buy");
                }

                if (TimeLeftToClaim != null)
                {
                    return TimeModel.GetTimeLeftText(TimeLeftToClaim.Value, " to claim");
                }

                if (!SpvCreated)
                {
                    return "SPV to be created";
                }

                return null;
            }
        }

        public bool StatusIsVisible => Status is not null;
    }
}
