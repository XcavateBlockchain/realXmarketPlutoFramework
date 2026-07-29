using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.MessagePopup;
using PlutoFramework.Components.Solana.Status;
using PlutoFramework.Model;
using PlutoFramework.Model.Solana;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Transfer
{
    /// <summary>
    /// Drives both the transfer popup and the token picker stacked over it.
    /// </summary>
    /// <remarks>
    /// One view model for two views, unlike the Substrate side, where <c>TransferViewModel</c>
    /// and <c>AssetSelectViewModel</c> are separate because the asset picker is also used by
    /// the NFT and Xcavate flows. This picker serves one flow, and splitting it would mean
    /// keeping a balance list and a selection in step across two singletons.
    /// </remarks>
    public partial class SolanaTransferViewModel : ObservableObject
    {
        /// <summary>
        /// How often the balances refresh while the popup is open. Frequent enough that a
        /// transfer arriving mid-session shows up, rare enough not to hammer a rate-limited
        /// public endpoint.
        /// </summary>
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

        private CancellationTokenSource? pollCts;

        private bool subscribed;

        /// <summary>Applied once, when the first poll produces rows to choose from.</summary>
        private string? preselectedMint;

        public ObservableCollection<SolanaTransferBalance> Balances { get; } = [];

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private bool isTokenSelectVisible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SymbolText))]
        [NotifyPropertyChangedFor(nameof(BalanceText))]
        private SolanaTransferBalance? selectedToken;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddressErrorIsVisible))]
        private string addressError = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountErrorIsVisible))]
        private string amountError = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LoadErrorIsVisible))]
        private string loadError = string.Empty;

        [ObservableProperty]
        private ButtonStateEnum confirmButtonState = ButtonStateEnum.Disabled;

        /// <summary>
        /// Hand-written rather than <c>[ObservableProperty]</c> so every keystroke re-runs
        /// validation, matching <c>TransferViewModel</c>'s shape.
        /// </summary>
        private string recipient = string.Empty;

        public string Recipient
        {
            get => recipient;
            set
            {
                SetProperty(ref recipient, value);
                Validate();
            }
        }

        private string amount = string.Empty;

        public string Amount
        {
            get => amount;
            set
            {
                SetProperty(ref amount, value);
                Validate();
            }
        }

        public bool AddressErrorIsVisible => !string.IsNullOrEmpty(AddressError);

        public bool AmountErrorIsVisible => !string.IsNullOrEmpty(AmountError);

        public bool LoadErrorIsVisible => !string.IsNullOrEmpty(LoadError);

        public string SymbolText => SelectedToken?.Symbol ?? "-";

        public string BalanceText => SelectedToken is null
            ? string.Empty
            : $"Balance: {DisplayAmount(SelectedToken, SelectedToken.SpendableBaseUnits)} {SelectedToken.Symbol}";

        /// <summary>
        /// Opens the popup, optionally on a chosen token and with the recipient filled in.
        /// </summary>
        /// <remarks>
        /// The token is named by mint rather than passed as a row, because the only caller
        /// that preselects — the token detail page — holds a <see cref="SolanaTokenBalance"/>,
        /// whose amount is the sum across every account for the mint. Building a row from
        /// that would seed Max with an amount the transfer cannot spend. The first poll
        /// supplies the real spendable figure.
        /// </remarks>
        public void Appear(string? preselectMint = null, string? recipientAddress = null)
        {
            Recipient = recipientAddress ?? string.Empty;
            Amount = string.Empty;
            AddressError = string.Empty;
            AmountError = string.Empty;
            LoadError = string.Empty;
            SelectedToken = null;

            preselectedMint = preselectMint;

            IsVisible = true;

            Subscribe();

            _ = PollBalancesAsync();
        }

        public void SetToDefault()
        {
            IsVisible = false;
            IsTokenSelectVisible = false;

            Recipient = string.Empty;
            Amount = string.Empty;
            AddressError = string.Empty;
            AmountError = string.Empty;
            LoadError = string.Empty;

            Unsubscribe();

            pollCts?.Cancel();
            pollCts?.Dispose();
            pollCts = null;
        }

        [RelayCommand]
        private void Cancel() => SetToDefault();

        [RelayCommand]
        private void OpenTokenSelect() => IsTokenSelectVisible = true;

        [RelayCommand]
        private void SelectToken(SolanaTransferBalance? token)
        {
            if (token is not null)
            {
                SelectedToken = token;
            }

            IsTokenSelectVisible = false;

            Validate();
        }

        /// <summary>
        /// Fills the largest sendable amount: the whole balance for an SPL token, and the
        /// balance less a small reserve for SOL, so the transaction can pay its own fee.
        /// </summary>
        [RelayCommand]
        private void Max()
        {
            if (SelectedToken is null)
            {
                return;
            }

            var sendable = SolanaFees.MaxSendable(
                SelectedToken.SpendableBaseUnits, SelectedToken.IsNative);

            Amount = SolanaAmount
                .FromBaseUnits(sendable.ToString(), SelectedToken.Decimals)
                .ToString("0.#########");
        }

        [RelayCommand]
        private async Task TransferAsync()
        {
            if (ConfirmButtonState != ButtonStateEnum.Enabled || SelectedToken is null)
            {
                return;
            }

            var sender = KeysModel.GetSolanaAddress();

            if (string.IsNullOrEmpty(sender))
            {
                AddressError = "No Solana account";

                return;
            }

            var token = SelectedToken;
            var baseUnits = SolanaAmount.ToBaseUnits(decimal.Parse(Amount), token.Decimals);
            var cluster = SolanaNetworkModel.SelectedCluster;
            var description = $"Transfer {DisplayAmount(token, baseUnits)} {token.Symbol}";

            var stack = DependencyService.Get<SolanaTransactionStatusStackViewModel>();

            // Registered before anything slow, so the user sees the transfer acknowledged the
            // moment they tap rather than after an unlock prompt and a round trip.
            var info = stack.Register(description, cluster);

            var recipientAddress = Recipient;

            // Closed before submitting: a Mobile Wallet Adapter key launches an intent and
            // backgrounds the app, and coming back to a stale popup over a toast that already
            // says "Submitting" reads as a transfer that did not happen.
            SetToDefault();

            try
            {
                var plan = await SolanaTransferModel.BuildPlanAsync(
                    sender, recipientAddress, token, baseUnits, cluster, CancellationToken.None);

                var account = await PlutoFrameworkSolanaAccount.ResolveAsync(
                    $"Transfer {token.Symbol}", CancellationToken.None);

                if (account is null)
                {
                    // No key, or the unlock prompt was declined. Either way the toast must
                    // not sit at Submitting forever.
                    info.Status = SolanaTransactionStatus.Error;

                    return;
                }

                var signature = await account.SendAsync(
                    plan.Instructions, $"Transfer {token.Symbol}", CancellationToken.None);

                info.Signature = signature;
                info.Status = SolanaTransactionStatus.Pending;

                _ = SolanaTransactionTracker.TrackAsync(
                    signature, cluster, info, CancellationToken.None);
            }
            catch (Exception ex)
            {
                info.Status = SolanaTransactionStatus.Error;

                var messagePopup = DependencyService.Get<MessagePopupViewModel>();
                messagePopup.Title = "Transfer failed";
                messagePopup.Text = DescribeFailure(ex, token);
                messagePopup.IsVisible = true;
            }
        }

        /// <summary>
        /// The rent for a recipient's new token account is not disclosed before confirming,
        /// by decision. A failure it causes still has to be explainable — hiding a cost is a
        /// choice, hiding a failure is a bug.
        /// </summary>
        private static string DescribeFailure(Exception exception, SolanaTransferBalance token)
        {
            var message = exception.Message ?? string.Empty;

            if (message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
                || message.Contains("0x1", StringComparison.Ordinal))
            {
                return token.IsNative
                    ? "Not enough SOL to cover the transfer and its fee."
                    : "Not enough SOL to complete this transfer. Sending a token needs a small "
                        + "amount of SOL for the network fee, and for the recipient's token "
                        + "account when they do not have one yet.";
            }

            return message;
        }

        private void Validate()
        {
            AddressError = string.Empty;
            AmountError = string.Empty;

            var addressOk = SolanaAddressValidator.IsValidAddress(Recipient);

            if (!string.IsNullOrWhiteSpace(Recipient) && !addressOk)
            {
                AddressError = "Not a valid Solana address";
            }

            var amountOk = false;

            if (SelectedToken is not null
                && decimal.TryParse(Amount, out var typed)
                && typed > 0)
            {
                var baseUnits = SolanaAmount.ToBaseUnits(typed, SelectedToken.Decimals);

                if (baseUnits > SelectedToken.SpendableBaseUnits)
                {
                    AmountError = "Insufficient balance";
                }
                else if (SelectedToken.IsNative
                    && baseUnits + SolanaFees.LamportsPerSignature > SelectedToken.SpendableBaseUnits)
                {
                    // Sending the entire balance leaves nothing for the signature fee, so the
                    // transaction cannot pay for itself.
                    AmountError = "Leave a little SOL for the network fee";
                }
                else
                {
                    amountOk = true;
                }
            }

            ConfirmButtonState = addressOk && amountOk
                ? ButtonStateEnum.Enabled
                : ButtonStateEnum.Disabled;
        }

        /// <summary>
        /// Which row the popup should be on after a refresh.
        /// </summary>
        /// <remarks>
        /// A current selection is re-pointed at its refreshed row, so Max and validation read
        /// the new figure rather than a record captured by an earlier poll. Otherwise the
        /// preselected mint wins, and failing that SOL — the first row, and the one token
        /// every account holds.
        /// </remarks>
        private SolanaTransferBalance? ResolveSelection(IReadOnlyList<SolanaTransferBalance> rows)
        {
            if (SelectedToken is not null)
            {
                var refreshed = rows.FirstOrDefault(row =>
                    row.Mint == SelectedToken.Mint && row.IsNative == SelectedToken.IsNative);

                if (refreshed is not null)
                {
                    return refreshed;
                }
            }

            if (!string.IsNullOrEmpty(preselectedMint))
            {
                var preselected = rows.FirstOrDefault(row => row.Mint == preselectedMint);

                if (preselected is not null)
                {
                    return preselected;
                }
            }

            return SelectedToken ?? rows.FirstOrDefault();
        }

        private static string DisplayAmount(SolanaTransferBalance token, BigInteger baseUnits) =>
            SolanaAmount.ToDisplayString(
                SolanaAmount.FromBaseUnits(baseUnits.ToString(), token.Decimals), token.Decimals);

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;

            SolanaTransactionTracker.TransactionConfirmed += OnTransactionConfirmed;
            SolanaNetworkModel.ClusterChanged += OnClusterChanged;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;

            SolanaTransactionTracker.TransactionConfirmed -= OnTransactionConfirmed;
            SolanaNetworkModel.ClusterChanged -= OnClusterChanged;
        }

        private void OnTransactionConfirmed(object? sender, EventArgs e) =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshOnceAsync());

        /// <summary>
        /// A mint means a different token on another cluster, so a selection made on one is
        /// meaningless on the next. Closing is clearer than silently re-pricing.
        /// </summary>
        private void OnClusterChanged(object? sender, SolanaCluster cluster) =>
            MainThread.BeginInvokeOnMainThread(SetToDefault);

        /// <summary>
        /// Polls until the popup closes, so a balance arriving mid-session is picked up and
        /// Max never offers an amount the wallet no longer has.
        /// </summary>
        private async Task PollBalancesAsync()
        {
            pollCts?.Cancel();
            pollCts?.Dispose();

            var cts = new CancellationTokenSource();
            pollCts = cts;

            while (!cts.IsCancellationRequested && IsVisible)
            {
                await RefreshOnceAsync(cts.Token);

                try
                {
                    await Task.Delay(PollInterval, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task RefreshOnceAsync(CancellationToken token = default)
        {
            var address = KeysModel.GetSolanaAddress();

            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            try
            {
                var rows = await SolanaTransferModel.GetTransferableBalancesAsync(
                    address, SolanaNetworkModel.SelectedCluster, token);

                token.ThrowIfCancellationRequested();

                Balances.Clear();

                foreach (var row in rows)
                {
                    Balances.Add(row);
                }

                SelectedToken = ResolveSelection(rows);

                // Consumed once: after the first poll the user's own choice must win.
                preselectedMint = null;

                LoadError = string.Empty;

                Validate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                // The previous figures stay on screen. A picker that blanks itself on one bad
                // round trip is worse than one showing a ten-second-old number.
                LoadError = ex.Message;
            }
        }
    }
}
