using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// Base of the popups shown for the whole of every Mobile Wallet Adapter request -
    /// from launching the wallet app to the answer coming back - so the user is never
    /// staring at a screen that silently waits on another app. The transaction variant is
    /// <see cref="MwaSignPopupViewModel"/>, the message variant
    /// <see cref="MwaSignMessagePopupViewModel"/>.
    ///
    /// Cancelling (the button, or dragging the card down) cancels the underlying
    /// operation, not just the popup: the session token is linked, so the wallet
    /// round trip is torn down with it.
    ///
    /// Open Wallet repeats the association intent, for when the first launch went
    /// unanswered or the user left the wallet app without deciding. Re-firing the same
    /// URI is safe once the wallet has connected: it just comes back to the foreground
    /// with the pending request.
    /// </summary>
    /// <remarks>
    /// One instance of each variant is shared through <see cref="DependencyService"/> and
    /// hosted in the page template, so it appears above whichever page triggered the
    /// request.
    /// </remarks>
    public abstract partial class MwaSigningPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        /// <summary>What is being signed and why, from the caller's reason string.</summary>
        [ObservableProperty]
        private string reason = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        private string status = "";

        public bool StatusIsVisible => !string.IsNullOrEmpty(Status);

        /// <summary>The reason phrased for the popup's description line.</summary>
        public virtual string Description => Reason;

        private CancellationTokenSource? signingCts;

        private MwaWalletReopener? walletReopener;

        /// <summary>
        /// Shows the popup for the duration of <paramref name="operation"/> and hides it
        /// again whatever happens. The operation receives a token that the popup's Cancel
        /// button and swipe-down dismissal cancel, linked to <paramref name="token"/>, a
        /// progress sink that narrates the connection stages, and a reopener the Open
        /// Wallet button fires to repeat the wallet intent.
        /// </summary>
        public async Task<T> ShowWhileAsync<T>(
            string reason,
            Func<IProgress<MwaConnectStage>, MwaWalletReopener, CancellationToken, Task<T>> operation,
            CancellationToken token)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            var reopener = new MwaWalletReopener();

            // Dispatches inside the handler rather than relying on Progress<T> capturing a
            // synchronization context - the signing call may start on a background thread,
            // where there is none to capture.
            var progress = new Progress<MwaConnectStage>(stage =>
                MainThread.BeginInvokeOnMainThread(() => Status = stage switch
                {
                    MwaConnectStage.LaunchingWallet => "Opening your wallet app..",
                    MwaConnectStage.WaitingForWallet => "Waiting for your wallet to connect..",
                    MwaConnectStage.Authorizing => "Approve the request in your wallet..",
                    _ => "",
                }));

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                signingCts = cts;
                walletReopener = reopener;
                Reason = reason;
                Status = "Waiting for your wallet..";
                IsVisible = true;
            });

            try
            {
                return await operation(progress, reopener, cts.Token);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Nulled before hiding, so the hide is not read as a cancellation.
                    signingCts = null;
                    walletReopener = null;

                    SetToDefault();
                });

                cts.Dispose();
            }
        }

        /// <summary>
        /// The same as the plain overload, for an operation that signs a message. The
        /// message popup overrides this to display the message; the transaction popup has
        /// no message to show.
        /// </summary>
        public virtual Task<T> ShowWhileAsync<T>(
            byte[] message,
            string reason,
            Func<IProgress<MwaConnectStage>, MwaWalletReopener, CancellationToken, Task<T>> operation,
            CancellationToken token) =>
            ShowWhileAsync(reason, operation, token);

        [RelayCommand]
        public void Cancel()
        {
            signingCts?.Cancel();

            IsVisible = false;
        }

        /// <summary>
        /// Repeats the wallet intent. No-ops before the association URI exists and after
        /// the operation ends - a wallet that never connected cannot be brought forward,
        /// and a finished session has nothing to return to.
        /// </summary>
        [RelayCommand]
        public async Task OpenWalletAsync()
        {
            var reopener = walletReopener;

            if (reopener is null)
            {
                return;
            }

            Status = "Opening your wallet app..";

            if (!await reopener.ReopenAsync())
            {
                Status = "No wallet app answered. Install a Solana wallet, then try again..";
            }
        }

        /// <summary>
        /// Dragging the card down closes it through the card's own gesture handling, which
        /// flips <see cref="IsVisible"/> - so any hide while an operation is still running
        /// means the user dismissed it, and must cancel exactly like the button.
        /// </summary>
        partial void OnIsVisibleChanged(bool value)
        {
            if (!value)
            {
                signingCts?.Cancel();
            }
        }

        partial void OnReasonChanged(string value) => OnPropertyChanged(nameof(Description));

        public virtual void SetToDefault()
        {
            signingCts?.Cancel();
            IsVisible = false;
            Reason = "";
            Status = "";
        }
    }
}
