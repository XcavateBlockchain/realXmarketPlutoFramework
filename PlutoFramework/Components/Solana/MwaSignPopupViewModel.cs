using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// Shown for the whole of every Mobile Wallet Adapter signature request - from
    /// launching the wallet app to the signature coming back - so the user is never
    /// staring at a screen that silently waits on another app.
    ///
    /// Cancelling (the button, or dragging the card down) cancels the underlying
    /// operation, not just the popup: the session token is linked, so the wallet
    /// round trip is torn down with it.
    /// </summary>
    /// <remarks>
    /// One instance is shared through <see cref="DependencyService"/> and hosted in the
    /// page template, so it appears above whichever page triggered the signature.
    /// </remarks>
    public partial class MwaSignPopupViewModel : ObservableObject, IPopup, ISetToDefault
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

        private CancellationTokenSource? signingCts;

        /// <summary>
        /// Shows the popup for the duration of <paramref name="operation"/> and hides it
        /// again whatever happens. The operation receives a token that the popup's Cancel
        /// button and swipe-down dismissal cancel, linked to <paramref name="token"/>, and
        /// a progress sink that narrates the connection stages.
        /// </summary>
        public async Task<T> ShowWhileAsync<T>(
            string reason,
            Func<IProgress<MwaConnectStage>, CancellationToken, Task<T>> operation,
            CancellationToken token)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

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
                Reason = reason;
                Status = "";
                IsVisible = true;
            });

            try
            {
                return await operation(progress, cts.Token);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Nulled before hiding, so the hide is not read as a cancellation.
                    signingCts = null;

                    SetToDefault();
                });

                cts.Dispose();
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            signingCts?.Cancel();

            IsVisible = false;
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

        public void SetToDefault()
        {
            signingCts?.Cancel();
            IsVisible = false;
            Reason = "";
            Status = "";
        }
    }
}
