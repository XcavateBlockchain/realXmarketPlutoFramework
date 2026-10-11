using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Loading;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Offers;
using PlutoFrameworkCore.Xcavate;
using PlutoFrameworkCore.Xcavate.Offers;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// The sheet the messenger hands a realXmarket negotiation to: the buyer's purchase
    /// of the agreed shares (<c>buyRelisted</c>) and, in seller-update mode, the seller's
    /// reprice of their own listing to the agreed terms (<c>delistAndRelist</c>). The
    /// agreed price and amount arrive with the bridge message; the listing is re-read
    /// from the indexer when the sheet opens, so a listing that went stale since the
    /// agreement is refused here rather than on-chain. Structured like
    /// <see cref="BuyPropertyTokensViewModel"/>, minus the primary-sale reservation
    /// machinery the secondary market does not have.
    /// </summary>
    public partial class BuyRelistedSharesPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        /// <summary>The bridge message the sheet was opened with; null in the default state.</summary>
        private MauiMarketplaceBridgeMessage? bridgeMessage;

        /// <summary>Double-tap guard for the requirements-check gap before the sheet closes.</summary>
        private bool submitting;

        [ObservableProperty]
        private bool isVisible = false;

        /// <summary>True in seller-update mode: the seller repricing their own listing.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PopupTitle))]
        [NotifyPropertyChangedFor(nameof(PaymentNote))]
        private bool isSellerUpdate;

        public string PopupTitle => IsSellerUpdate ? "Update listing price" : "Buy tokens";

        public string PaymentNote => IsSellerUpdate
            ? "The listing is delisted and relisted at the new price in a single transaction - it never sits unlisted in between."
            : "You pay exactly the agreed price; the marketplace fee comes out of the seller's proceeds.";

        [ObservableProperty]
        private string propertyName = "";

        [ObservableProperty]
        private string amountText = "";

        [ObservableProperty]
        private string pricePerTokenText = "";

        [ObservableProperty]
        private string totalText = "";

        /// <summary>
        /// True once the open-time re-read of the listing validated it against the
        /// agreed terms. Continue stays disabled until then - the agreed price and
        /// amount are only worth acting on while the chain still says them.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool listingChecked = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        [NotifyPropertyChangedFor(nameof(ErrorIsVisible))]
        private string errorMessage = "";

        public bool ErrorIsVisible => ErrorMessage != "";

        public ButtonStateEnum ContinueButtonState =>
            ErrorMessage == "" && ListingChecked && !submitting
                ? ButtonStateEnum.Enabled
                : ButtonStateEnum.Disabled;

        /// <summary>
        /// Opens the sheet on one parsed bridge message. The listing itself is re-read
        /// from the indexer as the sheet appears (<see cref="RefreshListingAsync"/>):
        /// the agreed terms are only worth acting on while the chain still says them.
        /// </summary>
        public void Open(MauiMarketplaceBridgeMessage message)
        {
            SetToDefault();

            bridgeMessage = message;
            IsSellerUpdate = message.Kind == MauiMarketplaceBridgeMessageKind.DelistAndRelist;
            PropertyName = message.PropertyName ?? "this property";
            AmountText = message.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            PricePerTokenText = XcavateQuoteFormatter.Format(message.SharePriceQuote);
            TotalText = XcavateQuoteFormatter.FormatTotal(message.SharePriceQuote, message.Amount);
            IsVisible = true;
        }

        partial void OnIsVisibleChanged(bool value)
        {
            if (value)
            {
                _ = RefreshListingAsync();
            }
        }

        /// <summary>
        /// Re-reads the share listing from the indexer and refuses the sheet when the
        /// chain no longer says what the bridge message claimed: a delisted or sold-out
        /// listing, a price the seller moved since the agreement, fewer shares left
        /// than agreed, or a wallet on the wrong side of the deal.
        /// </summary>
        private async Task RefreshListingAsync()
        {
            if (bridgeMessage is null)
            {
                return;
            }

            ListingChecked = false;

            try
            {
                var listing = await XcavateMarketplaceIndexerModel.GetShareListingAsync(
                    SolanaNetworkModel.SelectedCluster, bridgeMessage.ShareListingId, CancellationToken.None);

                if (listing is null)
                {
                    ErrorMessage = "This share listing is no longer active.";

                    return;
                }

                var wallet = KeysModel.GetSolanaAddress();

                if (IsSellerUpdate)
                {
                    if (!string.Equals(wallet, listing.Seller, StringComparison.Ordinal))
                    {
                        ErrorMessage = "Only the seller can update this listing's price.";

                        return;
                    }
                }
                else
                {
                    if (string.Equals(wallet, listing.Seller, StringComparison.Ordinal))
                    {
                        ErrorMessage = "This is your own listing - delist or reprice it instead of buying it.";

                        return;
                    }

                    if (listing.SharePriceQuote != bridgeMessage.SharePriceQuote)
                    {
                        ErrorMessage =
                            $"The price changed since the agreement: the seller now asks {XcavateQuoteFormatter.Format(listing.SharePriceQuote)} per token.";

                        return;
                    }

                    if (listing.Amount < bridgeMessage.Amount)
                    {
                        ErrorMessage = $"Only {listing.Amount} token(s) remain listed.";

                        return;
                    }
                }

                // A validated listing leaves whatever error a failed submit already put
                // here: the user is meant to read it and retry, not have it wiped.
                ListingChecked = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to re-read the share listing: ");
                Console.WriteLine(ex);

                ErrorMessage = "The listing could not be verified. Check your connection and try again.";
            }
        }

        public void SetToDefault()
        {
            IsVisible = false;
            bridgeMessage = null;
            IsSellerUpdate = false;
            PropertyName = "";
            AmountText = "";
            PricePerTokenText = "";
            TotalText = "";
            ErrorMessage = "";
            ListingChecked = false;
            submitting = false;
        }

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (bridgeMessage is null || !ListingChecked || submitting)
            {
                return;
            }

            submitting = true;
            OnPropertyChanged(nameof(ContinueButtonState));

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                submitting = false;
                OnPropertyChanged(nameof(ContinueButtonState));

                return;
            }

            // The buyer must be a whitelisted investor, like the primary purchase; the
            // seller update mirrors RelistAsync, which requires no role check.
            if (!IsSellerUpdate
                && !await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                submitting = false;
                OnPropertyChanged(nameof(ContinueButtonState));

                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            // Captured before SetToDefault resets the sheet: the message everything
            // below is keyed by, and the mode that decides the transaction.
            var message = bridgeMessage;
            var sellerUpdate = IsSellerUpdate;

            // Closed before submitting, like the buy popup on the primary market: a
            // Mobile Wallet Adapter key launches an intent and backgrounds the app, and
            // coming back to a stale popup over a toast that already says "Submitting"
            // reads as a purchase that did not happen.
            SetToDefault();

            // The build's user-presentable rejections (insufficient balance, an
            // unaccepted mint, someone beating the buyer to the shares) are reported by
            // the status popup already; surfacing them in the sheet as well lets the
            // user correct and retry from where they were. One-shot, subscribed before
            // the submission starts.
            EventHandler<MarketplaceSubmissionFailedEventArgs>? onFailed = null;
            onFailed = (_, args) =>
            {
                XcavateMarketplaceTransactionModel.SubmissionFailed -= onFailed;

                if (args.Exception is not InvalidOperationException)
                {
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Open(message);
                    ErrorMessage = args.Exception.Message;
                });
            };
            XcavateMarketplaceTransactionModel.SubmissionFailed += onFailed;

            if (sellerUpdate)
            {
                await XcavateMarketplaceTransactionModel.SubmitAsync(
                    "Update listing price",
                    (address, cluster, token) => XcavateMarketplaceCallsModel.DelistAndRelistAsync(
                        address, cluster,
                        (ulong)message.ShareListingId, (ulong)message.AssetId,
                        message.SharePriceQuote, message.Amount, token));

                return;
            }

            // The purchased status goes into the offer bucket once the buy confirms, so
            // the negotiation closes for both parties. One-shot: the first confirmation
            // after this submission is this purchase's.
            if (message.NamespaceId is not null && message.BucketId is not null)
            {
                EventHandler<MarketplaceTransactionConfirmedEventArgs>? onConfirmed = null;
                onConfirmed = (_, args) =>
                {
                    XcavateMarketplaceTransactionModel.TransactionConfirmed -= onConfirmed;

                    _ = OfferNegotiationModel.WritePurchasedStatusAsync(
                        message.NamespaceId.Value,
                        message.BucketId.Value,
                        new XcavateShareListingContext(
                            message.ShareListingId, message.AssetId,
                            message.Seller, message.SharePriceQuote, message.Amount),
                        KeysModel.GetSolanaAddress() ?? "",
                        args.Signature,
                        CancellationToken.None);
                };
                XcavateMarketplaceTransactionModel.TransactionConfirmed += onConfirmed;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Buy tokens",
                (address, cluster, token) => XcavateMarketplaceCallsModel.BuyRelistedSharesAsync(
                    address, cluster,
                    (ulong)message.AssetId, (ulong)message.ShareListingId,
                    message.Amount, null, token));
        }
    }
}
