using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class RelistPropertyTokensViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private PropertyMetadata? metadata = null;

        [ObservableProperty]
        private XcavateOngoingObjectListingDetails? listingDetails = null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxValue))]
        private uint tokensOwned = 0;

        public string MaxValue => TokensOwned.ToString();

        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        [NotifyPropertyChangedFor(nameof(PriceTotal))]
        private string tokens = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        [NotifyPropertyChangedFor(nameof(PriceTotal))]
        private string pricePerToken = "";

        public string PriceTotal
        {
            get
            {
                if (!uint.TryParse(Tokens, out var parsedTokens)
                    || parsedTokens <= 0
                    || parsedTokens > TokensOwned
                    || !uint.TryParse(PricePerToken, out var parsedPrice)
                    || parsedPrice <= 0)
                {
                    return "-";
                }

                var usd = parsedTokens * parsedPrice;
                return ((double)usd).ToCurrencyString();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string errorMessage = "";

        public ButtonStateEnum ContinueButtonState => ErrorMessage == "" && Tokens != "" && PricePerToken != "" ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        // No asset picker to feed any more: the pallet's relist_shares extrinsic is gone
        // and the Solana marketplace program has nothing to relist with - see
        // ContinueAsync.
        public EndpointEnum EndpointKey { get; set; } = EndpointEnum.None;

        public void SetToDefault()
        {
            IsVisible = false;

            PricePerToken = "";
            Tokens = "";
            ErrorMessage = "";
            TokensOwned = 0;
            Metadata = null;
            ListingDetails = null;
            EndpointKey = EndpointEnum.None;
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public Task ContinueAsync()
        {
            // The pallet's relist_shares extrinsic has no successor in the Solana
            // marketplace program (idls/devnet/marketplace.json has no share-relisting
            // instruction), so this popup cannot submit anything yet.
            ErrorMessage = "Relisting shares is not available on the Solana marketplace yet.";

            return Task.CompletedTask;
        }
        [RelayCommand]
        public void FormChanged()
        {
            OnPropertyChanged(nameof(PriceTotal));

            if (Tokens == "" || PricePerToken == "")
            {
                ErrorMessage = "";

                return;
            }

            uint parsedTokens;
            if (!uint.TryParse(Tokens, out parsedTokens))
            {

                ErrorMessage = "Shares is not valid number";

                return;
            }

            if (parsedTokens < 1)
            {
                ErrorMessage = "Shares must be greater than 0";

                return;
            }

            if (parsedTokens > ListingDetails?.ListedTokens)
            {
                ErrorMessage = $"Shares must be less than {ListingDetails.ListedTokens}";

                return;
            }

            uint pricePerToken;
            if (!uint.TryParse(PricePerToken, out pricePerToken))
            {
                ErrorMessage = "Price per share is not valid number";

                return;
            }

            if (pricePerToken < 1)
            {
                ErrorMessage = "Price per share must be greater than 0";
                return;
            }

            ErrorMessage = "";
        }
    }
}
