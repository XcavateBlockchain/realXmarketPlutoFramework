using System.Diagnostics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PlutoFramework.Components.Animations;

/// <summary>
/// Hosts a Skia surface and a frame timer for particle effects. Subclasses only supply
/// the per-frame drawing in <see cref="DrawFrame"/>.
/// </summary>
public abstract class ParticleSurfaceView : ContentView
{
    private const int FRAME_INTERVAL_MILLISECONDS = 16;

    // A backgrounded app hands us a huge elapsed time on resume. Without this clamp every
    // particle would jump a whole journey forward on the first frame back.
    private const double MAX_FRAME_SECONDS = 0.05;

    protected struct Particle
    {
        // Normalised to [0, 1] so the field survives rotation and resize for free.
        public float OriginX;
        public float OriginY;
        public float T;
        public float Life;
        public float Size;
    }

    private readonly SKCanvasView canvasView;
    private readonly Stopwatch stopwatch = new Stopwatch();

    private IDispatcherTimer? timer;
    private bool isLoaded;

    protected readonly Random Random = new Random();

    protected readonly SKPaint Paint = new SKPaint
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    protected Particle[] Particles = [];
    protected bool IsSeeded;

    public static readonly BindableProperty PlayingProperty = BindableProperty.Create(
        nameof(Playing), typeof(bool), typeof(ParticleSurfaceView),
        defaultValue: false,
        propertyChanged: (bindable, oldValue, newValue) =>
            ((ParticleSurfaceView)bindable).UpdateTimerState());

    public static readonly BindableProperty ParticleColorProperty = BindableProperty.Create(
        nameof(ParticleColor), typeof(Color), typeof(ParticleSurfaceView),
        defaultValue: Colors.White);

    public static readonly BindableProperty ParticleCountProperty = BindableProperty.Create(
        nameof(ParticleCount), typeof(int), typeof(ParticleSurfaceView),
        defaultValue: 90,
        propertyChanged: (bindable, oldValue, newValue) =>
            ((ParticleSurfaceView)bindable).ResizeField((int)newValue));

    protected ParticleSurfaceView()
    {
        // These surfaces always sit over interactive content, so they must never eat touches.
        InputTransparent = true;

        canvasView = new SKCanvasView();
        canvasView.PaintSurface += OnPaintSurface;

        Content = canvasView;

        ResizeField(ParticleCount);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool Playing
    {
        get => (bool)GetValue(PlayingProperty);
        set => SetValue(PlayingProperty, value);
    }

    public Color ParticleColor
    {
        get => (Color)GetValue(ParticleColorProperty);
        set => SetValue(ParticleColorProperty, value);
    }

    public int ParticleCount
    {
        get => (int)GetValue(ParticleCountProperty);
        set => SetValue(ParticleCountProperty, value);
    }

    /// <summary>
    /// Advances and draws one frame. Canvas units are pixels; multiply device-independent
    /// constants by <paramref name="scale"/>.
    /// </summary>
    protected abstract void DrawFrame(SKCanvas canvas, float width, float height, float scale, float deltaSeconds);

    /// <summary>
    /// Whether the frame timer should be running. Subclasses widen this when they have
    /// other reasons to animate, such as tracking an in-progress pull gesture.
    /// </summary>
    protected virtual bool ShouldAnimate => Playing;

    /// <summary>Marks the field for a fresh re-seed on the next frame.</summary>
    protected void InvalidateField() => IsSeeded = false;

    protected static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);

    private void OnLoaded(object? sender, EventArgs e)
    {
        isLoaded = true;
        UpdateTimerState();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        isLoaded = false;
        UpdateTimerState();
    }

    private void ResizeField(int count)
    {
        Particles = new Particle[Math.Max(count, 0)];
        IsSeeded = false;
    }

    /// <summary>
    /// These views are instantiated on pages that are always in the tree, so an idle
    /// instance must not tick at all.
    /// </summary>
    protected void UpdateTimerState()
    {
        if (ShouldAnimate && isLoaded)
        {
            if (timer is null)
            {
                timer = Dispatcher.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(FRAME_INTERVAL_MILLISECONDS);
                timer.Tick += OnTick;
            }

            if (!timer.IsRunning)
            {
                stopwatch.Restart();
                timer.Start();
            }

            return;
        }

        timer?.Stop();
        stopwatch.Stop();

        // Re-seed so the next time the effect plays it starts from a fresh field
        // rather than resuming a half-finished one.
        IsSeeded = false;
    }

    private void OnTick(object? sender, EventArgs e) => canvasView.InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float width = e.Info.Width;
        float height = e.Info.Height;

        if (width <= 0 || height <= 0 || Particles.Length == 0)
        {
            return;
        }

        float scale = Width > 0 ? (float)(e.Info.Width / Width) : 1f;

        float deltaSeconds = (float)Math.Min(stopwatch.Elapsed.TotalSeconds, MAX_FRAME_SECONDS);
        stopwatch.Restart();

        DrawFrame(canvas, width, height, scale, deltaSeconds);
    }
}
