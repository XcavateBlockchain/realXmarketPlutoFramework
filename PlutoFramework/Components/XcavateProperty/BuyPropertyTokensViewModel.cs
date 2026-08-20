using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.Xcavate;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class BuyPropertyTokensViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private PropertyMetadata? metadata;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxValue))]
        private XcavateOngoingObjectListingDetails? listingDetails;

        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        [NotifyPropertyChangedFor(nameof(TokensPrice))]
        [NotifyPropertyChangedFor(nameof(Fees))]
        [NotifyPropertyChangedFor(nameof(PriceTotal))]
        private string tokens = "";

        public string TokensPrice
        {
            get
            {
                int parsedTokens;
                if (!int.TryParse(Tokens, out parsedTokens) || parsedTokens < 1 || parsedTokens > ListingDetails?.ListedTokens)
                {
                    return "-";
                }

                decimal usd = parsedTokens * Metadata?.Financials.PricePerToken ?? 0;
                return usd.ToCurrencyString();
            }
        }

        public string Fees
        {
            get
            {
                int parsedTokens;
                if (!int.TryParse(Tokens, out parsedTokens) || parsedTokens < 1 || parsedTokens > ListingDetails?.ListedTokens)
                {
                    return "-";
                }

                var usd = (decimal)0.01 * (decimal)parsedTokens * Metadata?.Financials.PricePerToken ?? 0;
                return usd.ToCurrencyString();
            }
        }

        public string PriceTotal
        {
            get
            {
                int parsedTokens;
                if (!int.TryParse(Tokens, out parsedTokens) || parsedTokens < 1 || parsedTokens > ListingDetails?.ListedTokens)
                {
                    return "-";
                }

                var usd = (decimal)1.01 * (decimal)parsedTokens * Metadata?.Financials.PricePerToken ?? 0;
                return usd.ToCurrencyString();
            }
        }

        public string MaxValue => ListingDetails?.ListedTokens.ToString() ?? "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        [NotifyPropertyChangedFor(nameof(ErrorIsVisible))]
        private string errorMessage = "";

        public bool ErrorIsVisible => ErrorMessage != "";

        public ButtonStateEnum ContinueButtonState => ErrorMessage == "" && Tokens != "" ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        // The payment asset is no longer picked here: the Solana marketplace charges in
        // an accepted payment mint that XcavateMarketplaceCallsModel resolves from the
        // program's config when the transaction is built.
        public EndpointEnum EndpointKey { get; set; }

        public void SetToDefault()
        {
            IsVisible = false;
            Tokens = "";
            ErrorMessage = "";
            Metadata = null;
            ListingDetails = null;
            EndpointKey = EndpointEnum.None;
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (ListingDetails is null)
            {
                return;
            }

            uint parsedTokens;
            if (!uint.TryParse(Tokens, out parsedTokens))
            {
                return;
            }

            // The marketplace program is keyed by the listing id, which ItemId carries -
            // AssetId is the property asset's id, a different id space.
            long listingId = ListingDetails.ItemId.Value;

            // Closed before submitting: a Mobile Wallet Adapter key launches an intent
            // and backgrounds the app, and coming back to a stale popup over a toast
            // that already says "Submitting" reads as a purchase that did not happen.
            SetToDefault();

            // reserve_shares, not buy_property_shares: while a listing sells, purchases
            // are reservations (paid at claim time); the direct buy only opens after the
            // claim window closes.
            await XcavateMarketplaceTransactionModel.SubmitAsync(
                parsedTokens == 1 ? "Reserve 1 property share" : $"Reserve {parsedTokens} property shares",
                (investor, ct) => XcavateMarketplaceCallsModel.ReserveSharesAsync(investor, listingId, parsedTokens, ct));
        }

        [RelayCommand]
        public async Task FormChangedAsync()
        {
            if (Tokens == "")
            {
                ErrorMessage = "";

                return;
            }

            int parsedTokens;
            if (!int.TryParse(Tokens, out parsedTokens))
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

            ErrorMessage = "";
        }
    }
}
