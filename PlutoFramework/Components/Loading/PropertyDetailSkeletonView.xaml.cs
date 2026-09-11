using Microsoft.Maui.Dispatching;

namespace PlutoFramework.Components.Loading;

/// <summary>
/// A loading placeholder shaped like the property detail page
/// (<c>XcavateProperty.PropertyDetailPage</c>): the same scrollable stack, the same section
/// order, spacing and block sizes (square image, 2x2 stat cells, sliders, description block
/// and map), with every dynamic value replaced by a placeholder bar. A soft shimmer band
/// sweeps across the page while it is visible. Shown by the page until the details finish
/// loading, so the navigation there is instant.
/// </summary>
public partial class PropertyDetailSkeletonView : ContentView
{
    // One full shimmer period: the band is only over the page for the middle half of it,
    // so each pass gets a short rest between sweeps.
    private const int SHIMMER_PERIOD_MILLISECONDS = 1400;
    private const int FRAME_INTERVAL_MILLISECONDS = 16;

    /// <summary>
    /// Number of placeholder rows in the details block, matching the 12 attribute rows the
    /// real page renders (post code, flat/unit, local authority, town/city, location, area,
    /// off-street parking, outdoor space, bedrooms, construction date, bathrooms, quality).
    /// </summary>
    private const int ATTRIBUTE_ROW_COUNT = 12;

    private readonly LinearGradientBrush _shimmerBrush;
    private IDispatcherTimer? _shimmerTimer;
    private bool _isLoaded;
    private double _shimmerPhase;

    // Same theme-aware colors as the placeholder styles in the XAML.
    private static readonly IBrush BarBackground = new AppThemeBinding
    {
        Light = Color.FromHex("#E0E0E0"),
        Dark = Color.FromHex("#2F2F2F"),
    };

    // Same theme-aware colors as the real NftAttributeView rows.
    private static readonly IBrush AttributeRowBackground = new AppThemeBinding
    {
        Light = Color.FromHex("#fdfdfd"),
        Dark = Color.FromHex("#0a0a0a"),
    };

    public PropertyDetailSkeletonView()
    {
        InitializeComponent();

        _shimmerBrush = new LinearGradientBrush
        {
            // Start off-page so the first visible frame carries no flash of light.
            StartPoint = new Point(-1, 0),
            EndPoint = new Point(0, 1),
        };

        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.40f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Color.FromArgb("#66FFFFFF"), 0.5f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.60f));
        _shimmerBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1f));

        shimmerBorder.Background = _shimmerBrush;

        // The real main image (NftImageView) is square: its height tracks its width. Do the
        // same so the placeholder block matches whatever the device width is.
        mainImageBorder.SizeChanged += OnMainImageSizeChanged;

        BuildAttributeRows();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PropertyChanged += OnIsVisibleChanged;
    }

    private void OnMainImageSizeChanged(object? sender, EventArgs e)
    {
        if (mainImageBorder.Width <= 0)
        {
            return;
        }

        // Guard against layout loops by only updating when the value actually changed.
        if (Math.Abs(mainImageBorder.HeightRequest - mainImageBorder.Width) > 0.5)
        {
            mainImageBorder.HeightRequest = mainImageBorder.Width;
        }
    }

    private void BuildAttributeRows()
    {
        for (var i = 0; i < ATTRIBUTE_ROW_COUNT; i++)
        {
            attributesStack.Children.Add(CreateAttributeRow());
        }
    }

    // A rounded row with a label bar on the left and a value bar on the right, mirroring the
    // label/value layout of the real NftAttributeView rows.
    private Border CreateAttributeRow()
    {
        var labelBar = CreateBar(height: 14, cornerRadius: 7);
        labelBar.WidthRequest = 90;
        labelBar.HorizontalOptions = LayoutOptions.Start;
        labelBar.VerticalOptions = LayoutOptions.Center;

        var valueBar = CreateBar(height: 14, cornerRadius: 7);
        valueBar.WidthRequest = 60;
        valueBar.HorizontalOptions = LayoutOptions.End;
        valueBar.VerticalOptions = LayoutOptions.Center;

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            ColumnSpacing = 10,
        };

        grid.Children.Add(labelBar);

        Grid.SetColumn(valueBar, 1);
        grid.Children.Add(valueBar);

        return new Border
        {
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(10, 8, 10, 8),
            BackgroundColor = AttributeRowBackground,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            Content = grid,
        };
    }

    private static Border CreateBar(double height, double cornerRadius)
    {
        return new Border
        {
            StrokeThickness = 0,
            HeightRequest = height,
            BackgroundColor = BarBackground,
            StrokeShape = new RoundRectangle { CornerRadius = cornerRadius },
        };
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
        // drifts diagonally from off-left to off-right of the page.
        var t = _shimmerPhase * 2;
        _shimmerBrush.StartPoint = new Point(t - 1, 0);
        _shimmerBrush.EndPoint = new Point(t, 1);
    }
}
