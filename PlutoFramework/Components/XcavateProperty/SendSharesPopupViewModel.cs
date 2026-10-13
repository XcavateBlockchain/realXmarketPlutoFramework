using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Loading;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// The sheet behind the property page's "Send" action: a direct investor-to-investor
    /// transfer of property tokens (send_property_shares), with no listing and no payment
    /// in the middle. Structured like <see cref="BuyRelistedSharesPopupViewModel"/>: the
    /// sheet closes before the transaction is submitted because a Mobile Wallet Adapter
    /// key backgrounds the app for signing, and a build-time rejection reopens the sheet
    /// with the reason so the user can correct and retry. Pending rent settles for both
    /// wallets inside the same instruction, so the sheet says so instead of asking.
    /// </summary>
    public partial class SendSharesPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        /// <summary>The receiver-role rejection the chain would raise on receiver_role.</summary>
        private const string ReceiverNotInvestorMessage =
            "This wallet is not a verified investor - the marketplace would reject the transfer.";

        private const string ReceiverUnverifiableMessage =
            "The recipient could not be verified. Check your connection and try again.";

        /// <summary>What the sheet was opened with; zeroed in the default state.</summary>
        private ulong assetId;
        private uint holdingAmount;
        private uint locked;
        private uint listed;

        /// <summary>Double-tap guard for the requirements-check gap before the sheet closes.</summary>
        private bool submitting;

        /// <summary>
        /// Bumps every time the recipient changes, so a slow role lookup answering for an
        /// address the user has since edited is dropped instead of applied.
        /// </summary>
        private int receiverCheckGeneration;

        /// <summary>The trimmed address the latest role check answered for.</summary>
        private string? roleCheckedAddress;

        /// <summary>The form's own validation error, recomputed on every edit.</summary>
        private string formError = "";

        /// <summary>
        /// The reason the last submission's build gave up, set by the one-shot
        /// SubmissionFailed handler. Kept apart from <see cref="formError"/> so the form
        /// passing again cannot wipe the reason the user is meant to read and retry on;
        /// any edit clears it (<see cref="OnRecipientChanged"/> / <see cref="OnAmountChanged"/>).
        /// </summary>
        private string submissionError = "";

        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string recipient = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string amount = "";

        /// <summary>
        /// True once the indexer has confirmed the typed receiver holds the
        /// RealEstateInvestor role (checkAccess.compliant). Continue stays disabled until
        /// then - the program rejects receiver_role otherwise, so an unverified receiver
        /// never reaches the transaction pipeline.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private bool receiverChecked = false;

        public string ErrorMessage => formError != "" ? formError : submissionError;

        public bool ErrorIsVisible => ErrorMessage != "";

        /// <summary>
        /// The most the sender can hand over: the holding minus what the live votes lock
        /// and the open listing pins down - the same clamp HoldingState.Votable computes.
        /// </summary>
        public uint Sendable
        {
            get
            {
                var unavailable = (ulong)locked + listed;

                return unavailable >= holdingAmount ? 0 : holdingAmount - (uint)unavailable;
            }
        }

        public string MaxValue => Sendable.ToString();

        public ButtonStateEnum ContinueButtonState =>
            ErrorMessage == "" && Recipient != "" && Amount != "" && ReceiverChecked && !submitting
                ? ButtonStateEnum.Enabled
                : ButtonStateEnum.Disabled;

        /// <summary>
        /// Opens the sheet against the host page's holding snapshot: the indexer holding's
        /// amount, the largest lock it carries and its listed-for-sale amount, plus the
        /// property's asset id. The max sendable amount is computed here from exactly
        /// those three, so the sheet never promises more than the chain would release.
        /// </summary>
        public void SetContext(uint holdingAmount, uint locked, uint listed, ulong assetId)
        {
            SetToDefault();

            this.holdingAmount = holdingAmount;
            this.locked = locked;
            this.listed = listed;
            this.assetId = assetId;

            OnPropertyChanged(nameof(Sendable));
            OnPropertyChanged(nameof(MaxValue));

            IsVisible = true;
        }

        public void SetToDefault()
        {
            IsVisible = false;
            Recipient = "";
            Amount = "";
            formError = "";
            submissionError = "";
            RaiseErrorChanged();
            ReceiverChecked = false;
            roleCheckedAddress = null;
            receiverCheckGeneration++;
            submitting = false;
            assetId = 0;
            holdingAmount = 0;
            locked = 0;
            listed = 0;

            OnPropertyChanged(nameof(Sendable));
            OnPropertyChanged(nameof(MaxValue));
        }

        partial void OnRecipientChanged(string value)
        {
            submissionError = "";
            ReceiverChecked = false;
            roleCheckedAddress = null;
            receiverCheckGeneration++;

            ValidateForm();

            var address = value.Trim();

            if (SolanaAddressValidator.IsValidAddress(address) && !IsSelf(address))
            {
                _ = RefreshReceiverRoleAsync(address, receiverCheckGeneration);
            }
        }

        partial void OnAmountChanged(string value)
        {
            submissionError = "";

            ValidateForm();
        }

        /// <summary>
        /// Recomputes the inline error from the raw inputs. Silent while either field is
        /// empty - a form the user has not filled in does not start out accusing them,
        /// the same courtesy the relist form extends.
        /// </summary>
        private void ValidateForm()
        {
            var address = Recipient.Trim();

            if (Recipient == "" || Amount == "")
            {
                formError = "";
                RaiseErrorChanged();

                return;
            }

            if (!SolanaAddressValidator.IsValidAddress(address))
            {
                formError = "Not a valid Solana address.";
                RaiseErrorChanged();

                return;
            }

            if (IsSelf(address))
            {
                formError = "This is your own wallet - send the tokens to someone else.";
                RaiseErrorChanged();

                return;
            }

            if (!uint.TryParse(Amount, out var parsed))
            {
                formError = "Tokens is not a valid number.";
                RaiseErrorChanged();

                return;
            }

            if (parsed < 1)
            {
                formError = "Tokens must be greater than 0.";
                RaiseErrorChanged();

                return;
            }

            if (parsed > Sendable)
            {
                formError = $"You can send at most {MaxValue} token(s) - the rest is locked in votes or listed for sale.";
                RaiseErrorChanged();

                return;
            }

            // The role check's own verdict, once it has answered for this exact address;
            // while it is still in flight the disabled Continue button says enough.
            if (!ReceiverChecked && roleCheckedAddress == address)
            {
                formError = ReceiverNotInvestorMessage;
                RaiseErrorChanged();

                return;
            }

            formError = "";
            RaiseErrorChanged();
        }

        /// <summary>
        /// The checkAccess pre-flight: the receiver must hold RealEstateInvestor or the
        /// program rejects the transfer on receiver_role. Runs as the address is typed so
        /// the warning is already there when the user reaches for Continue; a stale answer
        /// (the address changed mid-query) is dropped by the generation guard.
        /// </summary>
        private async Task RefreshReceiverRoleAsync(string address, int generation)
        {
            var cluster = SolanaNetworkModel.SelectedCluster;

            // On a cluster with no deployment there is no indexer to ask; the submission
            // pipeline itself reports the undeployed network.
            if (!XcavateDeploymentModel.IsDeployed(cluster))
            {
                roleCheckedAddress = address;
                ReceiverChecked = true;

                return;
            }

            try
            {
                var compliant = await WhitelistModel.HasRoleAsync(
                    address, XcavateRole.RealEstateInvestor, cluster, CancellationToken.None);

                if (generation != receiverCheckGeneration)
                {
                    return;
                }

                roleCheckedAddress = address;
                ReceiverChecked = compliant;

                if (!compliant)
                {
                    formError = ReceiverNotInvestorMessage;
                    RaiseErrorChanged();
                }
                else if (formError == ReceiverNotInvestorMessage || formError == ReceiverUnverifiableMessage)
                {
                    formError = "";
                    RaiseErrorChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to verify the recipient's investor role: ");
                Console.WriteLine(ex);

                if (generation != receiverCheckGeneration)
                {
                    return;
                }

                // Fail closed: an unverifiable receiver never submits, because the chain
                // would reject a non-investor anyway.
                roleCheckedAddress = address;
                ReceiverChecked = false;

                formError = ReceiverUnverifiableMessage;
                RaiseErrorChanged();
            }
        }

        private void RaiseErrorChanged()
        {
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ErrorIsVisible));
            OnPropertyChanged(nameof(ContinueButtonState));
        }

        private static bool IsSelf(string address) =>
            string.Equals(address, KeysModel.GetSolanaAddress(), StringComparison.Ordinal);

        [RelayCommand]
        public void Cancel() => SetToDefault();

        [RelayCommand]
        public async Task ContinueAsync()
        {
            // The form's own validation has already said all of this; Continue re-checks
            // so a stale button state can never submit a bad send (the relist idiom).
            var recipient = Recipient.Trim();

            if (!SolanaAddressValidator.IsValidAddress(recipient) || IsSelf(recipient))
            {
                return;
            }

            if (!uint.TryParse(Amount, out var parsedAmount) || parsedAmount < 1 || parsedAmount > Sendable)
            {
                return;
            }

            // The receiver's investor role was verified as the address was typed; a wallet
            // that lost the role since fails on-chain, which the status popup reports.
            if (!ReceiverChecked || roleCheckedAddress != recipient || submitting)
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

            // The sender must be a whitelisted investor, like the buyer on the secondary market.
            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                submitting = false;
                OnPropertyChanged(nameof(ContinueButtonState));

                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            // Captured before SetToDefault resets the sheet: the context the failure
            // reopen below re-keys itself by.
            var assetId = this.assetId;
            var holdingAmount = this.holdingAmount;
            var locked = this.locked;
            var listed = this.listed;

            // Closed before submitting, like the buy popup: a Mobile Wallet Adapter key
            // launches an intent and backgrounds the app, and coming back to a stale
            // popup over a toast that already says "Submitting" reads as a send that did
            // not happen.
            SetToDefault();

            // The build's user-presentable rejections (a receiver holding that appeared
            // since the role check, a holding smaller than the typed amount) are reported
            // by the status popup already; surfacing them in the sheet as well lets the
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
                    SetContext(holdingAmount, locked, listed, assetId);
                    Recipient = recipient;
                    Amount = parsedAmount.ToString();
                    submissionError = args.Exception.Message;
                    RaiseErrorChanged();
                });
            };
            XcavateMarketplaceTransactionModel.SubmissionFailed += onFailed;

            var description = parsedAmount == 1
                ? "Send 1 property token"
                : $"Send {parsedAmount} property tokens";

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                description,
                (address, cluster, token) => XcavateMarketplaceCallsModel.SendPropertySharesAsync(
                    cluster, address, (long)assetId, recipient, parsedAmount, token));
        }
    }
}
