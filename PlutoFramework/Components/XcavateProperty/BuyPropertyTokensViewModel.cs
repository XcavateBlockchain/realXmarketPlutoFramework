using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
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

        // Field names start with "tgBp", not "tGbp": the source generator Pascal-cases
        // the leading single letter as its own word, so tGbpBalance would generate
        // TGbpBalance, while tgBpBalance generates the expected TgBpBalance.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TgBpBalanceText))]
        private decimal tgBpBalance;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TgBpBalanceText))]
        [NotifyPropertyChangedFor(nameof(TgBpBalanceRowIsVisible))]
        private bool tgBpBalanceLoaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BalanceWarningIsVisible))]
        private bool tgBpBalanceLoadFailed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedValue))]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedText))]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedRowIsVisible))]
        [NotifyPropertyChangedFor(nameof(TgBpBalanceText))]
        private uint alreadyReservedShares;

        /// <summary>
        /// The user's shares already reserved on this listing (bought plus reserved -
        /// both bind tGBP in the wallet until claim), keyed by the wallet's own address.
        /// </summary>
        public decimal AlreadyReservedValue => (decimal)AlreadyReservedShares * (Metadata?.Financials.PricePerToken ?? 0);

        public bool AlreadyReservedRowIsVisible => AlreadyReservedShares > 0;

        public string AlreadyReservedText =>
            $"{AlreadyReservedShares} share(s) = {XcavateReserveBalanceModel.Format(AlreadyReservedValue)} tGBP";

        /// <summary>
        /// The whole listing's reserved value: every investor's reserved shares at the
        /// current price - the listing's total reserved value, whatever this wallet holds.
        /// </summary>
        public uint TotalReservedShares => ListingDetails?.UnclaimedTokens ?? 0;

        public decimal TotalReservedValue => (decimal)TotalReservedShares * (Metadata?.Financials.PricePerToken ?? 0);

        public bool TotalReservedRowIsVisible => TotalReservedShares > 0;

        public string TotalReservedText =>
            $"{TotalReservedShares} shares = {XcavateReserveBalanceModel.Format(TotalReservedValue)} tGBP";

        public bool TgBpBalanceRowIsVisible => TgBpBalanceLoaded;

        // Shows the spendable figure - balance minus what is already reserved on this
        // listing - so the popup matches every other place the tGBP balance is displayed.
        public string TgBpBalanceText => TgBpBalanceLoaded
            ? $"{XcavateReserveBalanceModel.Format(Math.Max(TgBpBalance - AlreadyReservedValue, 0m))} tGBP"
            : "…";

        public bool BalanceWarningIsVisible => TgBpBalanceLoadFailed;

        public string BalanceWarningText =>
            "Could not check your tGBP balance, so this reservation is blocked until it can be verified. Check your connection and try again.";

        partial void OnIsVisibleChanged(bool value)
        {
            if (value)
            {
                _ = LoadBalanceAsync();
            }
        }

        /// <summary>
        /// Populates the balance checks the popup shows: the wallet's tGBP balance, its
        /// already-reserved shares on this listing, and the listing's total reserved
        /// value. The affordability check itself runs in FormChangedAsync and again at
        /// ContinueAsync, both against the latest known balance.
        /// </summary>
        private async Task LoadBalanceAsync()
        {
            TgBpBalanceLoaded = false;
            TgBpBalanceLoadFailed = false;

            var address = KeysModel.GetSolanaAddress();

            AlreadyReservedShares =
                !string.IsNullOrEmpty(address) && ListingDetails?.ShareOwners.TryGetValue(address, out var owner) == true
                    ? owner.ShareAmount
                    : 0u;

            if (string.IsNullOrEmpty(address))
            {
                // No Solana wallet configured: nothing to check. The submit flow fails
                // with its own message when it cannot find an account to sign with.
                return;
            }

            try
            {
                TgBpBalance = await XcavateReserveBalanceModel.GetTgBpBalanceAsync(
                    XcavateMarketplaceCallsModel.MarketplaceCluster, address, CancellationToken.None);

                TgBpBalanceLoaded = true;
            }
            catch (Exception ex)
            {
                // The balance cannot be verified, so the affordability check cannot run:
                // ContinueAsync refuses to submit while the load is in this state.
                Console.WriteLine(ex);
                TgBpBalanceLoadFailed = true;
            }

            // Re-validate whatever the user has already typed now that the balance is known.
            await FormChangedAsync();
        }

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
            TgBpBalance = 0;
            TgBpBalanceLoaded = false;
            TgBpBalanceLoadFailed = false;
            AlreadyReservedShares = 0;
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

            // Re-fetch the balance at reserve time rather than trusting the value loaded
            // when the popup opened: a transfer or another reservation may have landed
            // since, and the check must hold for the balance as it is now.
            var address = KeysModel.GetSolanaAddress();

            if (!string.IsNullOrEmpty(address))
            {
                try
                {
                    TgBpBalance = await XcavateReserveBalanceModel.GetTgBpBalanceAsync(
                        XcavateMarketplaceCallsModel.MarketplaceCluster, address, CancellationToken.None);

                    TgBpBalanceLoaded = true;
                    TgBpBalanceLoadFailed = false;
                }
                catch (Exception ex)
                {
                    // Without the balance the non-negative check cannot be made, so the
                    // reservation is not submitted - the popup stays open with the reason.
                    Console.WriteLine(ex);
                    TgBpBalanceLoadFailed = true;
                    ErrorMessage = BalanceWarningText;

                    return;
                }
            }

            var cost = XcavateReserveBalanceModel.ComputeReservationTotalCost(
                parsedTokens, (Metadata?.Financials.PricePerToken ?? 0));

            if (TgBpBalanceLoaded && !XcavateReserveBalanceModel.CanAfford(TgBpBalance, AlreadyReservedValue, cost))
            {
                ErrorMessage = XcavateReserveBalanceModel.InsufficientBalanceMessage(
                    TgBpBalance, AlreadyReservedValue, cost);

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

            if (TgBpBalanceLoaded)
            {
                var cost = XcavateReserveBalanceModel.ComputeReservationTotalCost(
                    (uint)parsedTokens, (Metadata?.Financials.PricePerToken ?? 0));

                if (!XcavateReserveBalanceModel.CanAfford(TgBpBalance, AlreadyReservedValue, cost))
                {
                    ErrorMessage = XcavateReserveBalanceModel.InsufficientBalanceMessage(
                        TgBpBalance, AlreadyReservedValue, cost);

                    return;
                }
            }

            ErrorMessage = "";
        }
    }
}
