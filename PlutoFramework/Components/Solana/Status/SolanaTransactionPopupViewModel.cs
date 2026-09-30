using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.WebView;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status
{
    /// <summary>
    /// Drives the Solana transaction status popup: one bottom card that follows a submitted
    /// transaction from submission to finality.
    /// </summary>
    /// <remarks>
    /// The replacement for the toast stack (<c>SolanaTransactionStatusStackViewModel</c>).
    /// Submitters call <see cref="Register"/> before any slow work, so the user sees the
    /// action acknowledged the moment they tap rather than after an unlock prompt and a
    /// round trip; the tracker then drives the returned <see cref="SolanaTransactionInfo"/>
    /// and the popup follows it through PropertyChanged.
    ///
    /// One transaction at a time: a new registration replaces the one on screen. The
    /// replaced transaction is still tracked to finality in the background — the tracker
    /// holds its own reference and its confirmation events still fire — it just stops
    /// being shown.
    /// </remarks>
    public partial class SolanaTransactionPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        /// <summary>
        /// How long a finalized success stays on screen before the popup closes itself.
        /// A failure never auto-dismisses — the user has to find out somehow, and a popup
        /// that removes itself is easy to miss.
        /// </summary>
        private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(5);

        private CancellationTokenSource? autoDismissCts;

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private SolanaTransactionInfo? currentInfo;

        /// <summary>
        /// Incremented on every status change so the view can replay its status animation.
        /// PropertyChanged on the derived display properties alone would not tell the view
        /// when to animate: several statuses share the same texts.
        /// </summary>
        [ObservableProperty]
        private int statusVersion;

        public string Title => CurrentInfo?.Status switch
        {
            SolanaTransactionStatus.Submitting => "Submitting transaction",
            SolanaTransactionStatus.Pending => "Transaction submitted",
            SolanaTransactionStatus.ConfirmedSuccess => "Transaction confirmed",
            SolanaTransactionStatus.FinalizedSuccess => "Transaction successful",
            SolanaTransactionStatus.Dropped => "Transaction dropped",
            SolanaTransactionStatus.Error => "Transaction error",
            null => string.Empty,
            _ => "Transaction failed",
        };

        public string StatusText => CurrentInfo?.StatusText ?? string.Empty;

        public Color StatusColor => CurrentInfo?.StatusColor ?? Colors.Gray;

        /// <summary>
        /// One sentence per status, explaining what is happening now and what happens next.
        /// </summary>
        public string StatusDescription => CurrentInfo?.Status switch
        {
            SolanaTransactionStatus.Submitting =>
                "Your transaction is being signed and sent to the Solana network.",
            SolanaTransactionStatus.Pending =>
                "Sent to the network. Waiting for the cluster to confirm it - this usually takes a few seconds.",
            SolanaTransactionStatus.ConfirmedSuccess =>
                "The network confirmed your transaction. Waiting for it to finalize...",
            SolanaTransactionStatus.FinalizedSuccess =>
                "Finalized. Your transaction is now permanent on Solana.",
            SolanaTransactionStatus.ConfirmedFailed or SolanaTransactionStatus.FinalizedFailed =>
                "The network processed your transaction but it failed. No changes were made on-chain.",
            SolanaTransactionStatus.Dropped =>
                "The network never picked up your transaction and it expired. Nothing was charged - you can try again.",
            SolanaTransactionStatus.Error =>
                "Your transaction could not be submitted.",
            _ => string.Empty,
        };

        /// <summary>
        /// True while the outcome is still open: the spinner shows and the popup keeps
        /// following the tracker. Confirmed-but-unfinalized counts — finality is what
        /// settles a Solana transaction.
        /// </summary>
        public bool IsInFlight => CurrentInfo?.Status is
            SolanaTransactionStatus.Submitting
            or SolanaTransactionStatus.Pending
            or SolanaTransactionStatus.ConfirmedSuccess;

        public bool IsSuccess => CurrentInfo?.Status is SolanaTransactionStatus.FinalizedSuccess;

        public bool IsFailure => CurrentInfo?.IsFailure ?? false;

        /// <summary>
        /// Null until submission returns a signature, and permanently null when submission
        /// failed — so the explorer button is hidden rather than pointing at nothing.
        /// </summary>
        public bool ExplorerIsVisible => CurrentInfo?.HasExplorerLink ?? false;

        public bool ExplorerIsHidden => !ExplorerIsVisible;

        public bool ErrorIsVisible => IsFailure && (CurrentInfo?.HasErrorMessage ?? false);

        /// <summary>
        /// Shows the popup tracking a new transaction and returns its info, so the caller
        /// can fill in the signature and let the tracker drive the rest.
        /// </summary>
        public SolanaTransactionInfo Register(string description, SolanaCluster cluster)
        {
            Detach();

            var info = new SolanaTransactionInfo
            {
                Description = description,
                Cluster = cluster,
            };

            info.PropertyChanged += OnInfoPropertyChanged;

            CurrentInfo = info;

            RefreshStatus();

            IsVisible = true;

            return info;
        }

        [RelayCommand]
        private void Close() => SetToDefault();

        /// <summary>
        /// Opens the transaction in Solana Explorer. The popup stays open underneath:
        /// the pushed page covers the whole screen, and the status is where the user
        /// returns to.
        /// </summary>
        [RelayCommand]
        private async Task OpenExplorerAsync()
        {
            if (CurrentInfo?.HasExplorerLink != true)
            {
                return;
            }

            await Shell.Current.Navigation.PushAsync(new WebViewPage(CurrentInfo.ExplorerUrl));
        }

        /// <summary>
        /// Also called by <c>BottomPopupCard</c> when the user swipes the popup away, which
        /// is why the cleanup lives here rather than in the Close handler. Dismissing never
        /// cancels tracking — the tracker keeps polling and its confirmation events keep
        /// firing; only the screen loses sight of the transaction.
        /// </summary>
        public void SetToDefault()
        {
            IsVisible = false;

            CancelAutoDismiss();

            Detach();

            CurrentInfo = null;

            RefreshStatus();
        }

        private void OnInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SolanaTransactionInfo.Status)
                or nameof(SolanaTransactionInfo.Signature)
                or nameof(SolanaTransactionInfo.ErrorMessage))
            {
                RefreshStatus();
            }
        }

        private void RefreshStatus()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(IsInFlight));
            OnPropertyChanged(nameof(IsSuccess));
            OnPropertyChanged(nameof(IsFailure));
            OnPropertyChanged(nameof(ExplorerIsVisible));
            OnPropertyChanged(nameof(ExplorerIsHidden));
            OnPropertyChanged(nameof(ErrorIsVisible));

            StatusVersion++;

            ScheduleAutoDismissIfSettled();
        }

        private void ScheduleAutoDismissIfSettled()
        {
            CancelAutoDismiss();

            if (CurrentInfo?.Status != SolanaTransactionStatus.FinalizedSuccess)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            autoDismissCts = cts;

            var dismissing = CurrentInfo;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(AutoDismissDelay, cts.Token);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Re-checked on the UI thread: the popup may have been closed, or
                        // re-registered to a newer transaction, while the delay was running.
                        if (cts.IsCancellationRequested
                            || CurrentInfo != dismissing
                            || dismissing.Status != SolanaTransactionStatus.FinalizedSuccess)
                        {
                            return;
                        }

                        SetToDefault();
                    });
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private void CancelAutoDismiss()
        {
            autoDismissCts?.Cancel();
            autoDismissCts?.Dispose();
            autoDismissCts = null;
        }

        private void Detach()
        {
            if (CurrentInfo is not null)
            {
                CurrentInfo.PropertyChanged -= OnInfoPropertyChanged;
            }
        }
    }
}
