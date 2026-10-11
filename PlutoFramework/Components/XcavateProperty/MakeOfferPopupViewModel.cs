using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Offers;
using System.Globalization;
using System.Numerics;
using UniqueryPlus.Metadata;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// The "Make offer" sheet on a secondary-market property page: the buyer types the
    /// price per token they are willing to pay (anchored at the listing's ask) and how
    /// many of the listed tokens they want, and Continue opens the offer bucket through
    /// <see cref="OfferNegotiationModel"/> - one signing prompt, then the messenger.
    /// Structured like <see cref="RelistPropertyTokensViewModel"/>: same form, same
    /// validation, same live total - but nothing here touches the chain; the offer
    /// itself is a profile-API message.
    /// </summary>
    public partial class MakeOfferPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private PropertyMetadata? metadata = null;

        /// <summary>
        /// The listing the offer targets, set by the detail page together with the ask
        /// prefill. Carries the share-listing id, seller, asking price and listed amount
        /// the offer payload is built from.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxValue))]
        private XcavateShareListingContext? shareListingContext = null;

        public string MaxValue => ShareListingContext?.Amount.ToString() ?? "";

        /// <summary>
        /// The smallest offer the marketplace's price scale can express, in human units -
        /// <see cref="XcavateMarketplaceCallsModel.MinSharePriceQuote"/> at the 9-decimal
        /// price scale - for the validation message.
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
                    || parsedTokens > (ShareListingContext?.Amount ?? 0)
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

        /// <summary>
        /// True while the bucket setup round trip runs. Continue is disabled for the
        /// duration: the setup is signed, and a second tap would cost a second prompt.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool isSubmitting = false;

        public ButtonStateEnum ContinueButtonState =>
            ErrorMessage == "" && Tokens != "" && PricePerToken != "" && !IsSubmitting
                ? ButtonStateEnum.Enabled
                : ButtonStateEnum.Disabled;

        public void SetToDefault()
        {
            IsVisible = false;

            PricePerToken = "";
            Tokens = "";
            ErrorMessage = "";
            IsSubmitting = false;
            Metadata = null;
            ShareListingContext = null;
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (ShareListingContext is null || Metadata?.PropertyName is not { Length: > 0 } propertyName)
            {
                return;
            }

            // The form's own validation (FormChanged) has already said all of this;
            // Continue re-checks so a stale button state can never send a bad offer.
            if (!uint.TryParse(Tokens, out var parsedTokens) || parsedTokens < 1 || parsedTokens > ShareListingContext.Amount)
            {
                return;
            }

            if (!TryParsePrice(PricePerToken, out var parsedPrice))
            {
                return;
            }

            // The same guards the relist form applies: the quote must be representable
            // at the program's 9-decimal scale, at least its minimum, and the total must
            // fit the program's u64 arithmetic - the on-chain buy at the agreed price is
            // built from these numbers later.
            ulong quote;
            try
            {
                quote = XcavateMarketplaceCallsModel.QuoteFromDecimal(parsedPrice);
            }
            catch (ArgumentOutOfRangeException)
            {
                ErrorMessage = "Price per token must be positive and have at most nine decimal places";

                return;
            }

            if (quote < XcavateMarketplaceCallsModel.MinSharePriceQuote)
            {
                ErrorMessage = $"Price per token must be at least {MinPricePerTokenText}";

                return;
            }

            if ((BigInteger)quote * parsedTokens > ulong.MaxValue)
            {
                ErrorMessage = "That price and token amount together are too large";

                return;
            }

            // Captured before the async gap: the context record itself is immutable.
            var context = ShareListingContext;

            IsSubmitting = true;

            try
            {
                var result = await OfferNegotiationModel.CreateOfferAsync(
                    context, propertyName, parsedPrice, parsedTokens);

                if (!result.Success)
                {
                    // Null means a dedicated popup (no account, no encryption key) has
                    // already named the problem; the sheet stays open either way.
                    if (result.ErrorMessage is not null)
                    {
                        ErrorMessage = result.ErrorMessage;
                    }

                    return;
                }
            }
            finally
            {
                IsSubmitting = false;
            }

            SetToDefault();
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

            if (!uint.TryParse(Tokens, out var parsedTokens))
            {
                ErrorMessage = "Tokens is not valid number";

                return;
            }

            if (parsedTokens < 1)
            {
                ErrorMessage = "Tokens must be greater than 0";

                return;
            }

            if (parsedTokens > (ShareListingContext?.Amount ?? 0))
            {
                ErrorMessage = $"Tokens must be less than {ShareListingContext?.Amount}";

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
        /// The offer price as a decimal, accepting both the device's own number format
        /// and the invariant one - a UK price is typed with a point whatever the phone's
        /// locale says.
        /// </summary>
        private static bool TryParsePrice(string text, out decimal price) =>
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out price)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}
