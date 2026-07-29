using System.ComponentModel;
using PlutoFramework.Components.WebView;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status;

/// <summary>
/// One Solana transaction toast. Mirrors <c>ExtrinsicStatusView</c>: swipe or tap the cross
/// to dismiss, five seconds of grace after success, tap to open the explorer.
/// </summary>
public partial class SolanaTransactionStatusView : ContentView
{
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(5);

    private CancellationTokenSource? autoDismissCts;

    private SolanaTransactionInfo? info;

    private bool navigating;

    private Queue<(float x, float y)> positions = new();

    public SolanaTransactionStatusView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The row is recycled by the bindable layout, so the previous subscription has to go or
    /// a dismissed toast keeps reacting to a transaction it no longer shows.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (info is not null)
        {
            info.PropertyChanged -= OnInfoPropertyChanged;
        }

        CancelAutoDismiss();

        info = BindingContext as SolanaTransactionInfo;

        if (info is null)
        {
            return;
        }

        info.PropertyChanged += OnInfoPropertyChanged;

        ScheduleAutoDismissIfSettled();
    }

    private void OnInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SolanaTransactionInfo.Status))
        {
            ScheduleAutoDismissIfSettled();
        }
    }

    /// <summary>
    /// Only a finalized success clears itself. A failure stays until dismissed — the user has
    /// to find out somehow, and a toast that removes itself is easy to miss.
    /// </summary>
    private void ScheduleAutoDismissIfSettled()
    {
        CancelAutoDismiss();

        if (info is null || info.Status != SolanaTransactionStatus.FinalizedSuccess)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        autoDismissCts = cts;

        var dismissing = info;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(AutoDismissDelay, cts.Token);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Re-checked on the UI thread: the row may have been rebound, or the
                    // status changed, while the delay was running.
                    if (cts.IsCancellationRequested
                        || dismissing.Status != SolanaTransactionStatus.FinalizedSuccess)
                    {
                        return;
                    }

                    Remove(dismissing.Id);
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

    private static void Remove(string id) =>
        DependencyService.Get<SolanaTransactionStatusStackViewModel>().Remove(id);

    private void OnCloseClicked(object sender, TappedEventArgs e)
    {
        CancelAutoDismiss();

        if (info is not null)
        {
            Remove(info.Id);
        }
    }

    private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
        {
            positions = new Queue<(float, float)>();
        }

        if (e.StatusType == GestureStatus.Running)
        {
            positions.Enqueue(((float)e.TotalX, (float)e.TotalY));

            if (positions.Count > 10)
            {
                positions.Dequeue();
            }

            card.TranslationX = positions.Average(item => item.x);
        }

        if (e.StatusType != GestureStatus.Completed)
        {
            return;
        }

        if (card.TranslationX < -50)
        {
            await card.TranslateToAsync((card.Width * -1) - 30, 0, 500, Easing.CubicIn);
        }
        else if (card.TranslationX > 50)
        {
            await card.TranslateToAsync(card.Width + 30, 0, 500, Easing.CubicIn);
        }
        else
        {
            await card.TranslateToAsync(0, 0, 500, Easing.CubicOut);

            return;
        }

        CancelAutoDismiss();

        if (info is not null)
        {
            Remove(info.Id);
        }
    }

    private async void OnClicked(object sender, TappedEventArgs e)
    {
        // No signature means submission never got that far, so there is nothing to look up.
        if (info is null || !info.HasExplorerLink || navigating)
        {
            return;
        }

        navigating = true;

        try
        {
            await Navigation.PushAsync(new WebViewPage(info.ExplorerUrl));
        }
        finally
        {
            navigating = false;
        }
    }
}
