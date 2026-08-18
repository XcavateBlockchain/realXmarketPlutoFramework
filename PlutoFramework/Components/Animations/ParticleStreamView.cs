using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PlutoFramework.Components.Animations;

/// <summary>
/// Small squares are emitted along the top edge and accelerate straight down, fading in as
/// they leave the edge and dissolving before the bottom, under a blue glow that shines down
/// from the top. Used as the pull-to-refresh indicator in place of the native spinner.
/// </summary>
/// <remarks>
/// When a refresh ends the field is not cut off: emission stops but the particles already in
/// flight are allowed to complete their fall and the glow eases out, so the band settles
/// rather than freezing on its last frame.
/// </remarks>
public class ParticleStreamView : ParticleSurfaceView
{
    private const int DEFAULT_PARTICLE_COUNT = 85;
    private const float MIN_PARTICLE_SIZE = 1.0f;
    private const float MAX_PARTICLE_SIZE = 5.0f;
    private const float MIN_LIFETIME_SECONDS = 0.2f;
    private const float MAX_LIFETIME_SECONDS = 1.0f;
    private const float SPAWN_JITTER = 0.05f;
    private const float FADE_IN_FRACTION = 0.00f;
    private const float FADE_OUT_FRACTION = 1f;
    private const float ALPHA_RAMP = 3.0f;

    // The glow eases toward the current intensity instead of snapping, which both softens
    // the pull ramp-up and gives the band a gentle tail as it winds down.
    private const float GLOW_MAX_ALPHA = 0.42f;
    private const float GLOW_EASE_SPEED = 7f;
    private const float GLOW_EPSILON = 0.01f;

    public static readonly BindableProperty PullProgressProperty = BindableProperty.Create(
        nameof(PullProgress), typeof(double), typeof(ParticleStreamView),
        defaultValue: 0d,
        propertyChanged: (bindable, oldValue, newValue) =>
            ((ParticleStreamView)bindable).UpdateTimerState());

    public static readonly BindableProperty GlowColorProperty = BindableProperty.Create(
        nameof(GlowColor), typeof(Color), typeof(ParticleStreamView),
        defaultValue: Color.FromArgb("#4C6EF5"));

    private readonly SKPaint glowPaint = new SKPaint { Style = SKPaintStyle.Fill };

    // Eased 0..1 opacity of the glow. Also keeps the timer alive during the tail so the
    // fade-out is actually drawn rather than left frozen.
    private float glowLevel;

    // How many particles are being emitted; frozen at this value while winding down so the
    // in-flight ones finish without new ones taking their place.
    private int activeVisibleCount;

    // Whether the last frame drew anything at all. Read by ShouldAnimate so the timer keeps
    // ticking through the wind-down and stops only once the band is empty.
    private bool hasLiveFrame;

    public ParticleStreamView()
    {
        ParticleCount = DEFAULT_PARTICLE_COUNT;
    }

    /// <summary>
    /// How far the user has pulled, 0..1 of the refresh threshold. Fades a few particles in
    /// while the gesture is still held, before the refresh itself starts.
    /// </summary>
    public double PullProgress
    {
        get => (double)GetValue(PullProgressProperty);
        set => SetValue(PullProgressProperty, value);
    }

    /// <summary>Colour of the glow that shines down from the top of the band.</summary>
    public Color GlowColor
    {
        get => (Color)GetValue(GlowColorProperty);
        set => SetValue(GlowColorProperty, value);
    }

    // Winding down (nothing left to emit but particles still in flight) must keep the timer
    // running, otherwise the surface freezes on its last frame instead of clearing.
    protected override bool ShouldAnimate => Playing || PullProgress > 0d || hasLiveFrame;

    protected override void DrawFrame(SKCanvas canvas, float width, float height, float scale, float deltaSeconds)
    {
        // A refresh in flight always plays at full strength; otherwise the pull drives it.
        bool active = Playing || PullProgress > 0d;
        float intensity = Playing ? 1f : (float)Math.Clamp(PullProgress, 0d, 1d);

        // Ease the glow toward the current intensity; when the band goes inactive the target
        // is 0 and this is what fades the glow out over the tail. The same ALPHA_RAMP the
        // particles use lets the glow read early in the pull as a scroll-to-refresh hint.
        float glowTarget = active ? Math.Min(intensity * ALPHA_RAMP, 1f) : 0f;
        glowLevel += (glowTarget - glowLevel) * Math.Min(1f, deltaSeconds * GLOW_EASE_SPEED);

        if (glowLevel < GLOW_EPSILON)
        {
            glowLevel = 0f;
        }

        DrawGlow(canvas, width, height, glowLevel);

        if (active && !IsSeeded)
        {
            Seed();
        }

        int drawn = 0;

        if (IsSeeded)
        {
            // Revealing particles by count is what makes "a very few" appear early in the
            // pull. Held steady while winding down so the in-flight ones can finish.
            if (active)
            {
                activeVisibleCount = (int)Math.Ceiling(Particles.Length * intensity);
            }

            float alphaFactor = active ? Math.Min(intensity * ALPHA_RAMP, 1f) : 1f;
            SKColor color = ParticleColor.ToSKColor();

            for (int i = 0; i < activeVisibleCount; i++)
            {
                ref Particle particle = ref Particles[i];

                particle.T += deltaSeconds / particle.Life;

                if (particle.T >= 1f)
                {
                    // While winding down, a finished particle is simply retired rather than
                    // respawned, so the band empties out instead of looping.
                    if (!active)
                    {
                        continue;
                    }

                    Respawn(ref particle);
                }

                // Same cubic ease-in as the convergence field. Here the clustering it
                // produces near the start is the point: particles gather at the emission
                // edge and then shoot downwards, which is what reads as firing.
                float progress = MathF.Pow(particle.T, 2.5f);

                float normalisedY = particle.OriginY + ((1f - particle.OriginY) * progress);

                float x = particle.OriginX * width;
                float y = normalisedY * height;

                float alpha = Math.Min(
                    normalisedY / FADE_IN_FRACTION,
                    (1f - normalisedY) / FADE_OUT_FRACTION);

                alpha = Math.Min(alpha, 1f) * alphaFactor;

                if (alpha <= 0f)
                {
                    continue;
                }

                Paint.Color = color.WithAlpha((byte)(alpha * 255f));

                float size = particle.Size * scale;
                float half = size / 2f;

                canvas.DrawRect(x - half, y - half, size, size, Paint);
                drawn++;
            }
        }

        hasLiveFrame = drawn > 0 || glowLevel > 0f;

        // Nothing left to emit and nothing left on screen: drop the seed and let the timer
        // stop now that ShouldAnimate has gone false.
        if (!active && !hasLiveFrame)
        {
            IsSeeded = false;
            activeVisibleCount = 0;
            UpdateTimerState();
        }
    }

    private void DrawGlow(SKCanvas canvas, float width, float height, float level)
    {
        if (level <= 0f)
        {
            return;
        }

        byte topAlpha = (byte)(GLOW_MAX_ALPHA * level * 255f);

        if (topAlpha == 0)
        {
            return;
        }

        SKColor glow = GlowColor.ToSKColor();

        using SKShader shader = SKShader.CreateLinearGradient(
            new SKPoint(0f, 0f),
            new SKPoint(0f, height),
            [glow.WithAlpha(topAlpha), glow.WithAlpha(0)],
            [0f, 1f],
            SKShaderTileMode.Clamp);

        glowPaint.Shader = shader;
        canvas.DrawRect(0f, 0f, width, height, glowPaint);
        glowPaint.Shader = null;
    }

    private void Seed()
    {
        for (int i = 0; i < Particles.Length; i++)
        {
            Respawn(ref Particles[i]);

            // A random starting phase, otherwise the whole band falls as one wave.
            Particles[i].T = (float)Random.NextDouble();
        }

        IsSeeded = true;
    }

    private void Respawn(ref Particle particle)
    {
        particle.OriginX = (float)Random.NextDouble();
        particle.OriginY = (float)Random.NextDouble() * SPAWN_JITTER;
        particle.T = 0f;
        particle.Life = Lerp(MIN_LIFETIME_SECONDS, MAX_LIFETIME_SECONDS, (float)Random.NextDouble());
        particle.Size = Lerp(MIN_PARTICLE_SIZE, MAX_PARTICLE_SIZE, (float)Random.NextDouble());
    }
}
