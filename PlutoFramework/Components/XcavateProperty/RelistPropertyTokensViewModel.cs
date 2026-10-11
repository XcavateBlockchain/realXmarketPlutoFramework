using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.Xcavate;
using System.Globalization;
using System.Numerics;
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

        /// <summary>
        /// The property asset id the relist_shares instruction is keyed by, set by the
        /// detail page from the untruncated Solana record (its fallback is the listing
        /// details' U32). The marketplace program derives the share mint from it, so the
        /// truncated Substrate-era fields must never be the source here.
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// The smallest asking price the marketplace program accepts, in human units -
        /// <see cref="XcavateMarketplaceCallsModel.MinSharePriceQuote"/> at the 9-decimal
        /// price scale (0.000001) - for the validation message.
        /// </summary>
        private static readonly string MinPricePerTokenText =
            ((decimal)XcavateMarketplaceCallsModel.MinSharePriceQuote / 1_000_000_000m)
                .ToString(CultureInfo.InvariantCulture);

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
                    || !TryParsePrice(PricePerToken, out var parsedPrice)
                    || parsedPrice <= 0)
                {
                    return "-";
                }

                var total = parsedTokens * parsedPrice;
                return total.ToCurrencyString();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string errorMessage = "";

        public ButtonStateEnum ContinueButtonState => ErrorMessage == "" && Tokens != "" && PricePerToken != "" ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        // The payment asset is not picked here: relist_shares prices the shares at the
        // program's 9-decimal quote scale, and the mint a later purchase settles in is
        // resolved from the marketplace config when that purchase is built.
        public EndpointEnum EndpointKey { get; set; } = EndpointEnum.None;

        public void SetToDefault()
        {
            IsVisible = false;

            PricePerToken = "";
            Tokens = "";
            ErrorMessage = "";
            TokensOwned = 0;
            AssetId = 0;
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

            // The form's own validation (FormChanged) has already said all of this;
            // Continue re-checks so a stale button state can never submit a bad relist.
            if (!uint.TryParse(Tokens, out var parsedTokens) || parsedTokens < 1 || parsedTokens > TokensOwned)
            {
                return;
            }

            if (!TryParsePrice(PricePerToken, out var parsedPrice))
            {
                return;
            }

            ulong quote;
            try
            {
                quote = XcavateMarketplaceCallsModel.QuoteFromDecimal(parsedPrice);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Non-positive prices and >9-decimal prices land here; both are the same
                // user-facing mistake - a price the program's scale cannot represent.
                ErrorMessage = "Price per token must be positive and have at most nine decimal places";

                return;
            }

            if (quote < XcavateMarketplaceCallsModel.MinSharePriceQuote)
            {
                ErrorMessage = $"Price per token must be at least {MinPricePerTokenText}";

                return;
            }

            // The program multiplies the price by the amount in u64; fail here with a
            // readable reason rather than in the transaction pipeline with an overflow.
            if ((BigInteger)quote * parsedTokens > ulong.MaxValue)
            {
                ErrorMessage = "That price and token amount together are too large";

                return;
            }

            // Captured before SetToDefault resets the form.
            var assetId = AssetId;

            // Closed before submitting, like the buy popup: a Mobile Wallet Adapter key
            // launches an intent and backgrounds the app, and coming back to a stale
            // popup over a toast that already says "Submitting" reads as a relist that
            // did not happen.
            SetToDefault();

            var description = parsedTokens == 1
                ? "Relist 1 property token"
                : $"Relist {parsedTokens} property tokens";

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                description,
                (address, cluster, token) => XcavateMarketplaceCallsModel.RelistSharesAsync(
                    address, cluster, assetId, parsedTokens, quote, token));
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

                ErrorMessage = "Tokens is not valid number";

                return;
            }

            if (parsedTokens < 1)
            {
                ErrorMessage = "Tokens must be greater than 0";

                return;
            }

            if (parsedTokens > TokensOwned)
            {
                ErrorMessage = $"Tokens must be less than {TokensOwned}";

                return;
            }

            if (!TryParsePrice(PricePerToken, out var parsedPrice))
            {
                ErrorMessage = "Price per token is not valid number";

                return;
            }

            if (parsedPrice <= 0)
            {
                ErrorMessage = "Price per token must be greater than 0";
                return;
            }

            ErrorMessage = "";
        }

        /// <summary>
        /// The asking price as a decimal, accepting both the device's own number format
        /// and the invariant one - a UK price is typed with a point whatever the phone's
        /// locale says.
        /// </summary>
        private static bool TryParsePrice(string text, out decimal price) =>
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out price)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}
