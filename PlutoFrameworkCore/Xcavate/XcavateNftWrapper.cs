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
        public MainPageRole ActiveRole { get; set; } = MainPageRole.Investor;
        public string? ViewerAddress { get; set; }
        public PropertyRoleContext? RoleContext { get; set; }

        public string? Status => PropertyStatusResolver.Resolve(
            this, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public bool StatusIsVisible => !string.IsNullOrEmpty(Status);
    }
}
