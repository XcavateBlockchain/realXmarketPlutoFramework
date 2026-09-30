using System.ComponentModel;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status;

/// <summary>
/// The Solana transaction status popup. Runs the status animations: a pulsing ring and
/// spinner while the transaction is in flight, a springing check with a ripple on
/// finality, a shaking alert on failure, and a fade whenever the status text moves on.
/// </summary>
public partial class SolanaTransactionPopupView : ContentView
{
    private readonly SolanaTransactionPopupViewModel viewModel;

    /// <summary>
    /// Generation counter for the pulse loop and the settle animations, so a superseded
    /// run (the transaction settled mid-pulse, or the popup closed) stops instead of
    /// fighting the newer one over the same icons. Bumped only when the icons reset or a
    /// settle animation starts — the pulse survives Submitting to Pending to Confirmed.
    /// </summary>
    private int animationRun;

    /// <summary>The animation run the pulse loop is serving, or -1 while it is stopped.</summary>
    private int pulseLoopRun = -1;

    public SolanaTransactionPopupView()
    {
        InitializeComponent();

        BindingContext = viewModel = DependencyService.Get<SolanaTransactionPopupViewModel>();
    }

    /// <summary>
    /// The view model is an app-wide singleton, so a subscription outlives the page. The
    /// view exists once per visited page (the page template hosts one), and each would
    /// keep animating long after its page was popped without the detach below.
    /// </summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        else
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SolanaTransactionPopupViewModel.CurrentInfo))
        {
            // A fresh registration always starts at Submitting; a closed popup has none.
            // Either way the icons go back to neutral before any animation runs.
            ResetIcons();

            if (viewModel.CurrentInfo is not null)
            {
                StartPulse();
            }
        }
        else if (e.PropertyName == nameof(SolanaTransactionPopupViewModel.StatusVersion))
        {
            _ = AnimateStatusAsync();
        }
    }

    private async Task AnimateStatusAsync()
    {
        var status = viewModel.CurrentInfo?.Status;

        // Null means the popup just closed; ResetIcons has already run.
        if (status is null)
        {
            return;
        }

        FadeStatusText();

        if (status is SolanaTransactionStatus.Submitting
            or SolanaTransactionStatus.Pending
            or SolanaTransactionStatus.ConfirmedSuccess)
        {
            StartPulse();

            return;
        }

        // Settled: bumping the run stops the pulse loop (the spinner hides through its
        // IsInFlight binding) and supersedes any settle animation still playing.
        var run = ++animationRun;

        try
        {
            if (status is SolanaTransactionStatus.FinalizedSuccess)
            {
                await AnimateSuccessAsync(run);
            }
            else
            {
                await AnimateFailureAsync(run);
            }
        }
        catch (Exception ex)
        {
            // An animation that fails mid-flight must not take the status display down
            // with it — the bound texts already carry the outcome.
            Console.WriteLine(ex);
        }
    }

    /// <summary>
    /// The check springs in while a green ring ripples out behind it.
    /// </summary>
    private async Task AnimateSuccessAsync(int run)
    {
        failureIcon.IsVisible = false;

        successIcon.IsVisible = true;
        successIcon.Opacity = 0;
        successIcon.Scale = 0.2;

        rippleRing.IsVisible = true;
        rippleRing.Opacity = 0.7;
        rippleRing.Scale = 0.8;

        var settle = Task.WhenAll(
            successIcon.FadeToAsync(1, 150),
            successIcon.ScaleToAsync(1, 600, Easing.SpringOut));

        var ripple = Task.WhenAll(
            rippleRing.ScaleToAsync(1.5, 550, Easing.CubicOut),
            rippleRing.FadeToAsync(0, 550, Easing.CubicOut));

        await settle;

        if (run != animationRun)
        {
            return;
        }

        await ripple;

        if (run == animationRun)
        {
            rippleRing.IsVisible = false;
        }
    }

    /// <summary>
    /// The alert pops in, then shakes.
    /// </summary>
    private async Task AnimateFailureAsync(int run)
    {
        successIcon.IsVisible = false;
        rippleRing.IsVisible = false;

        failureIcon.IsVisible = true;
        failureIcon.Opacity = 0;
        failureIcon.Scale = 0.2;
        failureIcon.TranslationX = 0;

        await Task.WhenAll(
            failureIcon.FadeToAsync(1, 150),
            failureIcon.ScaleToAsync(1, 400, Easing.SpringOut));

        foreach (var offset in new[] { -7.0, 7.0, -5.0, 5.0, 0.0 })
        {
            if (run != animationRun)
            {
                return;
            }

            await failureIcon.TranslateToAsync(offset, 0, 50, Easing.Linear);
        }
    }

    /// <summary>
    /// Quick fade-in, so a status swap reads as a change rather than a flicker. The texts
    /// are bound and have already updated by the time this runs.
    /// </summary>
    private void FadeStatusText()
    {
        statusLabel.Opacity = 0;
        statusDescriptionLabel.Opacity = 0;

        _ = statusLabel.FadeToAsync(1, 300);
        _ = statusDescriptionLabel.FadeToAsync(1, 300);
    }

    /// <summary>
    /// Gently scales and fades the ring behind the spinner while the transaction is in
    /// flight. Idempotent per animation run: a status that moves Submitting to Pending
    /// keeps the pulse going instead of restarting it. Closing the popup kills the loop
    /// through ResetIcons, so visibility is not part of the condition - which matters,
    /// because Register reports the new transaction before it flips IsVisible.
    /// </summary>
    private void StartPulse()
    {
        if (pulseLoopRun == animationRun)
        {
            return;
        }

        pulseLoopRun = animationRun;

        var run = animationRun;

        _ = Task.Run(async () =>
        {
            try
            {
                while (run == animationRun && viewModel.IsInFlight)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => Task.WhenAll(
                        pulseRing.ScaleToAsync(1.12, 650, Easing.SinInOut),
                        pulseRing.FadeToAsync(0.5, 650, Easing.SinInOut)));

                    if (run != animationRun || !viewModel.IsInFlight)
                    {
                        return;
                    }

                    await MainThread.InvokeOnMainThreadAsync(() => Task.WhenAll(
                        pulseRing.ScaleToAsync(1, 650, Easing.SinInOut),
                        pulseRing.FadeToAsync(0.25, 650, Easing.SinInOut)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (pulseLoopRun == run)
                {
                    pulseLoopRun = -1;
                }
            }
        });
    }

    private void ResetIcons()
    {
        animationRun++;

        successIcon.IsVisible = false;
        successIcon.Opacity = 0;

        failureIcon.IsVisible = false;
        failureIcon.Opacity = 0;

        rippleRing.IsVisible = false;
        rippleRing.Opacity = 0;
    }
}
