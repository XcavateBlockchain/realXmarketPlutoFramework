using System.Diagnostics;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PlutoFramework.Components.Animations;

/// <summary>
/// Renders a field of small squares that spawn uniformly across the control and
/// accelerate straight towards the centre, fading out before they can reach a
/// centred rectangular hole reserved for text.
/// </summary>
public partial class ParticleConvergenceView : ContentView
{
    private const int FRAME_INTERVAL_MILLISECONDS = 16;

    // A backgrounded app hands us a huge elapsed time on resume. Without this clamp
    // every particle would teleport into the centre on the first frame back.
    private const double MAX_FRAME_SECONDS = 0.05;

    private const float MIN_PARTICLE_SIZE = 3f;
    private const float MAX_PARTICLE_SIZE = 7f;
    private const float MIN_LIFETIME_SECONDS = 1.6f;
    private const float MAX_LIFETIME_SECONDS = 2.8f;

    private const float FADE_BAND = 48f;
    private const float EXCLUSION_MARGIN = 16f;
    private const float FALLBACK_EXCLUSION_RADIUS = 40f;

    private const int MAX_SPAWN_ATTEMPTS = 16;

    private struct Particle
    {
        // Normalised to [0, 1] so the field survives rotation and resize for free.
        public float OriginX;
        public float OriginY;
        public float T;
        public float Life;
        public float Size;
    }

    private readonly SKPaint paint = new SKPaint
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private readonly Stopwatch stopwatch = new Stopwatch();
    private readonly Random random = new Random();

    private Particle[] particles = [];
    private IDispatcherTimer? timer;
    private bool isLoaded;
    private bool isSeeded;

    public static readonly BindableProperty PlayingProperty = BindableProperty.Create(
        nameof(Playing), typeof(bool), typeof(ParticleConvergenceView),
        defaultValue: false,
        propertyChanged: (bindable, oldValue, newValue) =>
            ((ParticleConvergenceView)bindable).UpdateTimerState());

    public static readonly BindableProperty ParticleColorProperty = BindableProperty.Create(
        nameof(ParticleColor), typeof(Color), typeof(ParticleConvergenceView),
        defaultValue: Colors.White);

    public static readonly BindableProperty ParticleCountProperty = BindableProperty.Create(
        nameof(ParticleCount), typeof(int), typeof(ParticleConvergenceView),
        defaultValue: 90,
        propertyChanged: (bindable, oldValue, newValue) =>
            ((ParticleConvergenceView)bindable).ResizeField((int)newValue));

    public static readonly BindableProperty ExclusionWidthProperty = BindableProperty.Create(
        nameof(ExclusionWidth), typeof(double), typeof(ParticleConvergenceView),
        defaultValue: 0d);

    public static readonly BindableProperty ExclusionHeightProperty = BindableProperty.Create(
        nameof(ExclusionHeight), typeof(double), typeof(ParticleConvergenceView),
        defaultValue: 0d);

    public ParticleConvergenceView()
    {
        InitializeComponent();

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

    /// <summary>Width in device-independent units of the centred area particles must not enter.</summary>
    public double ExclusionWidth
    {
        get => (double)GetValue(ExclusionWidthProperty);
        set => SetValue(ExclusionWidthProperty, value);
    }

    /// <summary>Height in device-independent units of the centred area particles must not enter.</summary>
    public double ExclusionHeight
    {
        get => (double)GetValue(ExclusionHeightProperty);
        set => SetValue(ExclusionHeightProperty, value);
    }

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
        particles = new Particle[Math.Max(count, 0)];
        isSeeded = false;
    }

    /// <summary>
    /// The view is instantiated on every page that uses the page template, so an idle
    /// instance must not tick at all.
    /// </summary>
    private void UpdateTimerState()
    {
        if (Playing && isLoaded)
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

        // Re-seed so the next time the loader appears it starts from a fresh field
        // rather than resuming a half-collapsed one.
        isSeeded = false;
    }

    private void OnTick(object? sender, EventArgs e) => canvasView.InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float width = e.Info.Width;
        float height = e.Info.Height;

        if (width <= 0 || height <= 0 || particles.Length == 0)
        {
            return;
        }

        float scale = Width > 0 ? (float)(e.Info.Width / Width) : 1f;
        float fadeBand = FADE_BAND * scale;
        SKRect exclusion = GetExclusionRect(width, height, scale);

        if (!isSeeded)
        {
            Seed(width, height, exclusion, fadeBand);
        }

        float deltaSeconds = (float)Math.Min(stopwatch.Elapsed.TotalSeconds, MAX_FRAME_SECONDS);
        stopwatch.Restart();

        float centerX = width / 2f;
        float centerY = height / 2f;
        SKColor color = ParticleColor.ToSKColor();

        for (int i = 0; i < particles.Length; i++)
        {
            ref Particle particle = ref particles[i];

            particle.T += deltaSeconds / particle.Life;

            // Cubic ease-in. At T=0.5 a particle has covered only 12.5% of its path, so it
            // lingers near where it spawned and then streaks inwards. That is what keeps the
            // visible field looking evenly spread instead of clumping into the centre.
            float progress = Math.Min(particle.T * particle.T * particle.T, 1f);

            float originX = particle.OriginX * width;
            float originY = particle.OriginY * height;

            float x = originX + (centerX - originX) * progress;
            float y = originY + (centerY - originY) * progress;

            float alpha = DistanceToRect(x, y, exclusion) / fadeBand;

            if (alpha <= 0f || particle.T >= 1f)
            {
                Respawn(ref particle, width, height, exclusion, fadeBand);
                continue;
            }

            paint.Color = color.WithAlpha((byte)(Math.Min(alpha, 1f) * 255f));

            float size = particle.Size * scale;
            float half = size / 2f;

            canvas.DrawRect(x - half, y - half, size, size, paint);
        }
    }

    private void Seed(float width, float height, SKRect exclusion, float fadeBand)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            Respawn(ref particles[i], width, height, exclusion, fadeBand);

            // A random starting phase, otherwise every particle converges in lockstep
            // and the field pulses.
            particles[i].T = (float)random.NextDouble();
        }

        isSeeded = true;
    }

    private void Respawn(ref Particle particle, float width, float height, SKRect exclusion, float fadeBand)
    {
        // Spawning inside the fade band would make the particle appear already transparent,
        // so reject those points and keep the visible distribution uniform.
        SKRect blocked = SKRect.Inflate(exclusion, fadeBand, fadeBand);

        float normalisedX = 0f;
        float normalisedY = 0f;

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            normalisedX = (float)random.NextDouble();
            normalisedY = (float)random.NextDouble();

            if (!blocked.Contains(normalisedX * width, normalisedY * height))
            {
                break;
            }
        }

        particle.OriginX = normalisedX;
        particle.OriginY = normalisedY;
        particle.T = 0f;
        particle.Life = Lerp(MIN_LIFETIME_SECONDS, MAX_LIFETIME_SECONDS, (float)random.NextDouble());
        particle.Size = Lerp(MIN_PARTICLE_SIZE, MAX_PARTICLE_SIZE, (float)random.NextDouble());
    }

    private SKRect GetExclusionRect(float width, float height, float scale)
    {
        float halfWidth;
        float halfHeight;

        if (ExclusionWidth > 0 && ExclusionHeight > 0)
        {
            halfWidth = ((float)ExclusionWidth / 2f + EXCLUSION_MARGIN) * scale;
            halfHeight = ((float)ExclusionHeight / 2f + EXCLUSION_MARGIN) * scale;
        }
        else
        {
            // No text to protect yet. Still reserve a hole, otherwise every particle
            // converges onto a single point and reads as a blob.
            halfWidth = FALLBACK_EXCLUSION_RADIUS * scale;
            halfHeight = FALLBACK_EXCLUSION_RADIUS * scale;
        }

        float centerX = width / 2f;
        float centerY = height / 2f;

        return new SKRect(
            centerX - halfWidth,
            centerY - halfHeight,
            centerX + halfWidth,
            centerY + halfHeight);
    }

    /// <summary>
    /// Distance from a point to the nearest edge of a rectangle, or 0 when inside it.
    /// Alpha is driven by this, so a particle is fully transparent exactly at the boundary
    /// and can never be drawn overlapping the text.
    /// </summary>
    private static float DistanceToRect(float x, float y, SKRect rect)
    {
        float dx = Math.Max(Math.Max(rect.Left - x, x - rect.Right), 0f);
        float dy = Math.Max(Math.Max(rect.Top - y, y - rect.Bottom), 0f);

        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);
}
