#if ANDROID
using Android.Views;
#endif
#if IOS || MACCATALYST
using UIKit;
#endif

namespace PlutoFramework.Components.Animations;

/// <summary>
/// Reports how far a <see cref="RefreshView"/> has been pulled down, as 0..1 of the distance
/// needed to trigger a refresh. RefreshView exposes nothing about the in-progress gesture, so
/// this observes the native scroll/touch stream directly.
/// </summary>
/// <remarks>
/// Strictly read-only with respect to the gesture: no touch event is consumed and no gesture
/// is intercepted, so the platform refresh behaviour is exactly what it was without this
/// behaviour attached. On Android it additionally nudges the content down while a refresh is
/// running, since (unlike iOS) SwipeRefreshLayout does not push content aside for its spinner
/// and the particle band would otherwise overlap the first rows of content.
/// </remarks>
public class PullProgressBehavior : Behavior<RefreshView>
{
    // SwipeRefreshLayout's DEFAULT_CIRCLE_TARGET, which iOS approximates closely enough.
    private const float TRIGGER_DISTANCE_DIP = 64f;

    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(PullProgressBehavior),
        defaultValue: 0d);

    private RefreshView? refreshView;

    /// <summary>Pull distance as a fraction of the refresh trigger threshold, clamped to 0..1.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        private set => SetValue(ProgressProperty, value);
    }

    protected override void OnAttachedTo(RefreshView bindable)
    {
        base.OnAttachedTo(bindable);

        refreshView = bindable;
        bindable.HandlerChanged += OnHandlerChanged;
        bindable.Loaded += OnLoaded;
        bindable.PropertyChanged += OnRefreshViewPropertyChanged;

        TryAttachPlatform();
    }

    protected override void OnDetachingFrom(RefreshView bindable)
    {
        base.OnDetachingFrom(bindable);

        bindable.HandlerChanged -= OnHandlerChanged;
        bindable.Loaded -= OnLoaded;
        bindable.PropertyChanged -= OnRefreshViewPropertyChanged;

        DetachPlatform();
        refreshView = null;
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        DetachPlatform();
        TryAttachPlatform();
    }

    // The native scroll view is not always a child yet when the handler is first created.
    private void OnLoaded(object? sender, EventArgs e) => TryAttachPlatform();

    private void OnRefreshViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == RefreshView.IsRefreshingProperty.PropertyName)
        {
            OnRefreshingChanged(refreshView?.IsRefreshing ?? false);
        }
    }

    private void SetProgressFromPull(float pulledDip, float triggerDistanceDip)
    {
        double next = Math.Clamp(pulledDip / triggerDistanceDip, 0d, 1d);

        if (Math.Abs(next - Progress) > 0.001d)
        {
            Progress = next;
        }
    }

    // On iOS the OS already slides content down under UIRefreshControl, so only Android needs
    // a nudge to keep the particle band clear of the content.
    private void OnRefreshingChanged(bool refreshing)
    {
#if ANDROID
        if (refreshView?.Content is not VisualElement content)
        {
            return;
        }

        double target = refreshing ? CONTENT_PUSH_DIP : 0d;

        content.TranslateTo(0d, target, CONTENT_ANIMATION_MILLISECONDS,
            refreshing ? Easing.CubicOut : Easing.CubicIn);
#endif
    }

#if ANDROID

    // How far the band ramps: full band a little before the native refresh actually fires,
    // so the pull reads clearly as a hint the whole way down.
    private const float TRIGGER_DISTANCE_DIP_ANDROID = 80f;

    private const double CONTENT_PUSH_DIP = 80d;
    private const uint CONTENT_ANIMATION_MILLISECONDS = 250;

    // The listener sits on the scrolling content view, not the SwipeRefreshLayout. Modern
    // SwipeRefreshLayout drives pull-to-refresh through the nested-scrolling API, so the
    // content (a NestedScrollView / RecyclerView) keeps the touch stream during the pull
    // while the layout itself never sees an onTouchEvent.
    private Android.Views.View? contentView;
    private float density = 1f;
    private float downY;
    private bool armed;

    private void TryAttachPlatform()
    {
        if (contentView is not null)
        {
            return;
        }

        if (refreshView?.Content?.Handler?.PlatformView is not Android.Views.View child)
        {
            return;
        }

        contentView = child;

        float reported = child.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        density = reported > 0f ? reported : 1f;

        child.Touch += OnPlatformTouch;
    }

    private void DetachPlatform()
    {
        if (contentView is null)
        {
            return;
        }

        contentView.Touch -= OnPlatformTouch;
        contentView = null;

        armed = false;
        Progress = 0d;
    }

    private void OnPlatformTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        // Observe only: the content must still scroll and its children must still be tappable.
        e.Handled = false;

        MotionEvent? motion = e.Event;

        if (motion is null || contentView is null)
        {
            return;
        }

        switch (motion.ActionMasked)
        {
            case MotionEventActions.Down:
                downY = motion.GetY();
                armed = true;
                break;

            case MotionEventActions.Move:
                // A tappable child can swallow the DOWN, so the first event we see may be a
                // MOVE. Establish the origin from here in that case.
                if (!armed)
                {
                    downY = motion.GetY();
                    armed = true;
                }

                // A pull only exists while the content is scrolled to the very top; once it
                // can scroll up we are scrolling into content, so reset the origin.
                if (contentView.CanScrollVertically(-1))
                {
                    downY = motion.GetY();

                    if (Progress != 0d)
                    {
                        Progress = 0d;
                    }
                }
                else
                {
                    float pulledDip = (motion.GetY() - downY) / density;
                    SetProgressFromPull(Math.Max(pulledDip, 0f), TRIGGER_DISTANCE_DIP_ANDROID);
                }

                break;

            default:
                armed = false;
                Progress = 0d;
                break;
        }
    }

#elif IOS || MACCATALYST

    private UIScrollView? scrollView;

    private void TryAttachPlatform()
    {
        if (scrollView is not null)
        {
            return;
        }

        if (refreshView?.Handler?.PlatformView is not UIView platformView)
        {
            return;
        }

        scrollView = FindScrollView(platformView);

        if (scrollView is null)
        {
            return;
        }

        scrollView.Scrolled += OnPlatformScrolled;
    }

    private void DetachPlatform()
    {
        if (scrollView is null)
        {
            return;
        }

        scrollView.Scrolled -= OnPlatformScrolled;
        scrollView = null;

        Progress = 0d;
    }

    private void OnPlatformScrolled(object? sender, EventArgs e)
    {
        if (scrollView is null)
        {
            return;
        }

        // Overscrolling past the top drives ContentOffset.Y negative.
        double pulled = -(scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top);

        SetProgressFromPull((float)pulled, TRIGGER_DISTANCE_DIP);
    }

    private static UIScrollView? FindScrollView(UIView view)
    {
        if (view is UIScrollView found)
        {
            return found;
        }

        foreach (UIView child in view.Subviews)
        {
            UIScrollView? nested = FindScrollView(child);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

#else

    private void TryAttachPlatform()
    {
    }

    private void DetachPlatform()
    {
    }

#endif
}
