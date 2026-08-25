namespace PlutoFramework.Components.Loading;

/// <summary>
/// A loading placeholder shaped exactly like the marketplace property card
/// (<c>XcavateProperty.PropertyThumbnailView</c>): the same card chrome, the same 200px
/// image block, and the same rows, margins and spacing, with the dynamic values replaced
/// by placeholder bars. A soft shimmer band sweeps across the card while it is visible.
/// </summary>
public partial class PropertyCardSkeletonView : ContentView
{
    // One full shimmer period: the band is only over the card for the middle half of it,
    // so each pass gets a short rest between sweeps.
    private const int SHIMMER_PERIOD_MILLISECONDS = 1400;
    private const int FRAME_INTERVAL_MILLISECONDS = 16;

    private readonly LinearGradientBrush _shimmerBrush;
    private Microsoft.Maui.Dispatching.IDispatcherTimer? _shimmerTimer;
    private bool _isLoaded;
    private double _shimmerPhase;

    public PropertyCardSkeletonView()
    {
        InitializeComponent();

        _shimmerBrush = new LinearGradientBrush
        {
            // Start off-card so the first visible frame carries no flash of light.
            StartPoint = new Point(-1, 0),
            EndPoint = new Point(0, 1),
        };

        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.40f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Color.FromArgb("#66FFFFFF"), 0.5f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.60f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1f));

        shimmerBorder.Background = _shimmerBrush;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PropertyChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == IsVisibleProperty.PropertyName)
        {
            UpdateShimmerState();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _isLoaded = true;
        UpdateShimmerState();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _isLoaded = false;
        UpdateShimmerState();
    }

    private void UpdateShimmerState()
    {
        var shouldShimmer = _isLoaded && IsVisible;

        if (shouldShimmer)
        {
            if (_shimmerTimer is null)
            {
                _shimmerTimer = Dispatcher.CreateTimer();
                _shimmerTimer.Interval = TimeSpan.FromMilliseconds(FRAME_INTERVAL_MILLISECONDS);
                _shimmerTimer.Tick += OnShimmerTick;
            }

            _shimmerTimer.Start();
        }
        else
        {
            _shimmerTimer?.Stop();
        }

        shimmerBorder.IsVisible = shouldShimmer;
    }

    private void OnShimmerTick(object? sender, EventArgs e)
    {
        _shimmerPhase += (double)FRAME_INTERVAL_MILLISECONDS / SHIMMER_PERIOD_MILLISECONDS;

        if (_shimmerPhase >= 1)
        {
            _shimmerPhase -= 1;
        }

        // Phase 0..1 maps to sweep 0..2, with a fixed (1, 1) direction vector, so the band
        // drifts diagonally from off-left to off-right of the card.
        var t = _shimmerPhase * 2;
        _shimmerBrush.StartPoint = new Point(t - 1, 0);
        _shimmerBrush.EndPoint = new Point(t, 1);
    }
}
