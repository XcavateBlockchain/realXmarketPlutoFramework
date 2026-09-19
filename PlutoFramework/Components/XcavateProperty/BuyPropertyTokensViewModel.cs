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

        /// <summary>
        /// The token this listing's shares sell for. tGBP for every listing today; when
        /// the marketplace prices listings in other tokens, this listing's symbol is what
        /// keeps the balance row, the affordability check and every message in the right
        /// currency.
        /// </summary>
        public string PaymentTokenSymbol { get; private set; } = XcavateReserveBalanceModel.TgBpSymbol;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PaymentTokenBalanceText))]
        private decimal paymentTokenBalance;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PaymentTokenBalanceText))]
        [NotifyPropertyChangedFor(nameof(PaymentTokenBalanceRowIsVisible))]
        private bool paymentTokenBalanceLoaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BalanceWarningIsVisible))]
        [NotifyPropertyChangedFor(nameof(BalanceWarningText))]
        private bool balanceLoadFailed;

        /// <summary>
        /// The payment token value this wallet has reserved across EVERY listing, not
        /// just this one. reserve_shares binds funds wallet-wide, so the spendable
        /// balance and the affordability check must subtract all of it - subtracting
        /// only this listing's share overstates what the user can still spend whenever
        /// another property holds a reservation.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PaymentTokenBalanceText))]
        [NotifyPropertyChangedFor(nameof(WalletReservedText))]
        [NotifyPropertyChangedFor(nameof(WalletReservedRowIsVisible))]
        private decimal walletReservedValue;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedValue))]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedText))]
        [NotifyPropertyChangedFor(nameof(AlreadyReservedRowIsVisible))]
        private uint alreadyReservedShares;

        /// <summary>
        /// The user's tokens already reserved on this listing (bought plus reserved),
        /// keyed by the wallet's own address. Informational only - the balance row and
        /// the affordability check run on <see cref="WalletReservedValue"/>.
        /// </summary>
        public decimal AlreadyReservedValue => (decimal)AlreadyReservedShares * (Metadata?.Financials.PricePerToken ?? 0);

        public bool AlreadyReservedRowIsVisible => AlreadyReservedShares > 0;

        public string AlreadyReservedText =>
            $"{AlreadyReservedShares} token(s) = {XcavateReserveBalanceModel.Format(AlreadyReservedValue)} {PaymentTokenSymbol}";

        /// <summary>
        /// The whole listing's reserved value: every investor's reserved tokens at the
        /// current price - the listing's total reserved value, whatever this wallet holds.
        /// </summary>
        public uint TotalReservedShares => ListingDetails?.UnclaimedTokens ?? 0;

        public decimal TotalReservedValue => (decimal)TotalReservedShares * (Metadata?.Financials.PricePerToken ?? 0);

        public bool TotalReservedRowIsVisible => TotalReservedShares > 0;

        public string TotalReservedText =>
            $"{TotalReservedShares} tokens = {XcavateReserveBalanceModel.Format(TotalReservedValue)} {PaymentTokenSymbol}";

        public bool PaymentTokenBalanceRowIsVisible => PaymentTokenBalanceLoaded;

        public string PaymentTokenBalanceLabel => $"Your available {PaymentTokenSymbol} balance:";

        // Shows the spendable figure - balance minus what is already reserved on every
        // listing - so the popup matches every other place the payment token balance is
        // displayed.
        public string PaymentTokenBalanceText => PaymentTokenBalanceLoaded
            ? $"{XcavateReserveBalanceModel.Format(Math.Max(PaymentTokenBalance - WalletReservedValue, 0m))} {PaymentTokenSymbol}"
            : "…";

        public bool WalletReservedRowIsVisible => PaymentTokenBalanceLoaded && WalletReservedValue > 0m;

        public string WalletReservedText =>
            $"{XcavateReserveBalanceModel.Format(WalletReservedValue)} {PaymentTokenSymbol}";

        public bool BalanceWarningIsVisible => BalanceLoadFailed;

        public string BalanceWarningText =>
            $"Could not verify your {PaymentTokenSymbol} balance and reservations, so this reservation is blocked until it can be verified. Check your connection and try again.";

        partial void OnIsVisibleChanged(bool value)
        {
            if (value)
            {
                _ = LoadBalanceAsync();
            }
        }

        /// <summary>
        /// Populates the balance checks the popup shows: the wallet's payment token
        /// balance, its reserved value across every listing, and its already-reserved
        /// tokens on this listing. The affordability check itself runs in
        /// FormChangedAsync and again at ContinueAsync, both against the latest known
        /// balance. Fails closed: without a verified balance AND a verified
        /// wallet-wide reserved figure the subtraction cannot be trusted, so the
        /// reservation is blocked until both load.
        /// </summary>
        private async Task LoadBalanceAsync()
        {
            PaymentTokenBalanceLoaded = false;
            BalanceLoadFailed = false;

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
                var balanceTask = XcavateReserveBalanceModel.GetPaymentTokenBalanceAsync(
                    XcavateMarketplaceCallsModel.MarketplaceCluster, address, PaymentTokenSymbol, CancellationToken.None);

                var reservedTask = XcavateReserveBalanceModel.GetReservedValuesAsync(address, CancellationToken.None);

                await Task.WhenAll(balanceTask, reservedTask);

                PaymentTokenBalance = await balanceTask;
                WalletReservedValue = XcavateReserveBalanceModel.ValueFor(await reservedTask, PaymentTokenSymbol);

                PaymentTokenBalanceLoaded = true;
            }
            catch (Exception ex)
            {
                // The balance cannot be verified, so the affordability check cannot run:
                // ContinueAsync refuses to submit while the load is in this state.
                Console.WriteLine(ex);
                BalanceLoadFailed = true;
            }

            // Re-validate whatever the user has already typed now that the balance is known.
            await FormChangedAsync();
        }

        // The payment asset is no longer picked here: the Solana marketplace charges in
        // an accepted payment mint that XcavateMarketplaceCallsModel resolves from the
        // program's config when the transaction is built.
        public EndpointEnum EndpointKey { get; set; }

        /// <summary>
        /// Set by the detail page when this listing's claim window has closed: the
        /// purchase then goes through buy_property_shares (tokens paid for and
        /// delivered immediately) instead of reserve_shares, and the copy follows.
        /// </summary>
        public bool DirectBuyIsOpen { get; set; }

        public string PopupTitle => DirectBuyIsOpen ? "Buy Property Tokens" : "Reserve Property Tokens";

        public void SetToDefault()
        {
            IsVisible = false;
            Tokens = "";
            ErrorMessage = "";
            Metadata = null;
            ListingDetails = null;
            EndpointKey = EndpointEnum.None;
            DirectBuyIsOpen = false;
            PaymentTokenSymbol = XcavateReserveBalanceModel.TgBpSymbol;
            PaymentTokenBalance = 0;
            PaymentTokenBalanceLoaded = false;
            BalanceLoadFailed = false;
            WalletReservedValue = 0;
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

            // Re-fetch the balance and the wallet-wide reserved value at reserve time
            // rather than trusting the values loaded when the popup opened: a transfer
            // or another reservation may have landed since, and the check must hold for
            // the balance as it is now.
            var address = KeysModel.GetSolanaAddress();

            if (!string.IsNullOrEmpty(address))
            {
                try
                {
                    var balanceTask = XcavateReserveBalanceModel.GetPaymentTokenBalanceAsync(
                        XcavateMarketplaceCallsModel.MarketplaceCluster, address, PaymentTokenSymbol, CancellationToken.None);

                    var reservedTask = XcavateReserveBalanceModel.GetReservedValuesAsync(address, CancellationToken.None);

                    await Task.WhenAll(balanceTask, reservedTask);

                    PaymentTokenBalance = await balanceTask;
                    WalletReservedValue = XcavateReserveBalanceModel.ValueFor(await reservedTask, PaymentTokenSymbol);

                    PaymentTokenBalanceLoaded = true;
                    BalanceLoadFailed = false;
                }
                catch (Exception ex)
                {
                    // Without the balance the non-negative check cannot be made, so the
                    // reservation is not submitted - the popup stays open with the reason.
                    Console.WriteLine(ex);
                    BalanceLoadFailed = true;
                    ErrorMessage = BalanceWarningText;

                    return;
                }
            }

            var cost = XcavateReserveBalanceModel.ComputeReservationTotalCost(
                parsedTokens, (Metadata?.Financials.PricePerToken ?? 0));

            if (PaymentTokenBalanceLoaded
                && !XcavateReserveBalanceModel.CanAfford(PaymentTokenBalance, WalletReservedValue, cost))
            {
                ErrorMessage = XcavateReserveBalanceModel.InsufficientBalanceMessage(
                    PaymentTokenSymbol, PaymentTokenBalance, WalletReservedValue, cost);

                return;
            }

            // The marketplace program is keyed by the listing id, which ItemId carries -
            // AssetId is the property asset's id, a different id space.
            long listingId = ListingDetails.ItemId.Value;

            // Captured before SetToDefault resets it: while a listing sells, purchases
            // are reservations (paid at claim time); once the claim window closes the
            // program rejects reserve_shares and the direct buy takes over.
            var directBuyIsOpen = DirectBuyIsOpen;

            // Closed before submitting: a Mobile Wallet Adapter key launches an intent
            // and backgrounds the app, and coming back to a stale popup over a toast
            // that already says "Submitting" reads as a purchase that did not happen.
            SetToDefault();

            var description = (directBuyIsOpen, parsedTokens) switch
            {
                (true, 1) => "Buy 1 property token",
                (true, _) => $"Buy {parsedTokens} property tokens",
                (false, 1) => "Reserve 1 property token",
                (false, _) => $"Reserve {parsedTokens} property tokens",
            };

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                description,
                (investor, ct) => directBuyIsOpen
                    ? XcavateMarketplaceCallsModel.BuyPropertySharesAsync(investor, listingId, parsedTokens, ct)
                    : XcavateMarketplaceCallsModel.ReserveSharesAsync(investor, listingId, parsedTokens, ct));
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

                ErrorMessage = "Tokens is not valid number";

                return;
            }

            if (parsedTokens < 1)
            {
                ErrorMessage = "Tokens must be greater than 0";

                return;
            }

            if (parsedTokens > ListingDetails?.ListedTokens)
            {
                ErrorMessage = $"Tokens must be less than {ListingDetails.ListedTokens}";

                return;
            }

            if (PaymentTokenBalanceLoaded)
            {
                var cost = XcavateReserveBalanceModel.ComputeReservationTotalCost(
                    (uint)parsedTokens, (Metadata?.Financials.PricePerToken ?? 0));

                if (!XcavateReserveBalanceModel.CanAfford(PaymentTokenBalance, WalletReservedValue, cost))
                {
                    ErrorMessage = XcavateReserveBalanceModel.InsufficientBalanceMessage(
                        PaymentTokenSymbol, PaymentTokenBalance, WalletReservedValue, cost);

                    return;
                }
            }

            ErrorMessage = "";
        }
    }
}
