using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PlutoFramework.Components.Animations;

/// <summary>
/// Small squares spawn uniformly across the control and accelerate straight towards the
/// centre, fading out before they can reach a centred rectangular hole reserved for text.
/// </summary>
public class ParticleConvergenceView : ParticleSurfaceView
{
    private const float MIN_PARTICLE_SIZE = 1.0f;
    private const float MAX_PARTICLE_SIZE = 7.0f;
    private const float MIN_LIFETIME_SECONDS = 0.8f;
    private const float MAX_LIFETIME_SECONDS = 1.4f;
    private const float FADE_BAND = 86f;
    private const float EXCLUSION_MARGIN = 10f;

    private const float FALLBACK_EXCLUSION_RADIUS = 40f;
    private const int MAX_SPAWN_ATTEMPTS = 16;

    public static readonly BindableProperty ExclusionWidthProperty = BindableProperty.Create(
        nameof(ExclusionWidth), typeof(double), typeof(ParticleConvergenceView),
        defaultValue: 0d);

    public static readonly BindableProperty ExclusionHeightProperty = BindableProperty.Create(
        nameof(ExclusionHeight), typeof(double), typeof(ParticleConvergenceView),
        defaultValue: 0d);

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

    protected override void DrawFrame(SKCanvas canvas, float width, float height, float scale, float deltaSeconds)
    {
        float fadeBand = FADE_BAND * scale;
        SKRect exclusion = GetExclusionRect(width, height, scale);

        if (!IsSeeded)
        {
            Seed(width, height, exclusion, fadeBand);
        }

        float centerX = width / 2f;
        float centerY = height / 2f;
        SKColor color = ParticleColor.ToSKColor();

        for (int i = 0; i < Particles.Length; i++)
        {
            ref Particle particle = ref Particles[i];

            particle.T += deltaSeconds / particle.Life;

            float progress = Math.Min(MathF.Pow(particle.T, 2.5f), 1f);

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

            Paint.Color = color.WithAlpha((byte)(Math.Min(alpha, 1f) * 255f));

            float size = particle.Size * scale;
            float half = size / 2f;

            canvas.DrawRect(x - half, y - half, size, size, Paint);
        }
    }

    private void Seed(float width, float height, SKRect exclusion, float fadeBand)
    {
        for (int i = 0; i < Particles.Length; i++)
        {
            Respawn(ref Particles[i], width, height, exclusion, fadeBand);

            // A random starting phase, otherwise every particle converges in lockstep
            // and the field pulses.
            Particles[i].T = (float)Random.NextDouble();
        }

        IsSeeded = true;
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
            normalisedX = (float)Random.NextDouble();
            normalisedY = (float)Random.NextDouble();

            if (!blocked.Contains(normalisedX * width, normalisedY * height))
            {
                break;
            }
        }

        particle.OriginX = normalisedX;
        particle.OriginY = normalisedY;
        particle.T = 0f;
        particle.Life = Lerp(MIN_LIFETIME_SECONDS, MAX_LIFETIME_SECONDS, (float)Random.NextDouble());
        particle.Size = Lerp(MIN_PARTICLE_SIZE, MAX_PARTICLE_SIZE, (float)Random.NextDouble());
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
}
