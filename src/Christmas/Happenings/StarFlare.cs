using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// The star on top of the tallest tree flares: a warm light swells around
/// it, a four-pointed sparkle turns slowly across it, and a shower of tiny
/// golden sparks drifts down the tree and dies out. Then it settles back
/// to its ordinary twinkle.
///
/// Three stamps. A wide soft glow (the light spilling into the night), the
/// sparkle on top of it, and one small glow per spark, each spark on its
/// own path worked out from t: it starts at the star when its turn comes,
/// falls faster and faster (gravity: distance grows with the square of the
/// time) and drifts a little sideways. The scene's own bulb glow for the
/// star is drawn after this, over it, which is fine: it only adds light.
///
/// Where the star is comes from the painter (StarAt).
/// </summary>
internal sealed class StarFlare : Happening
{
    private const int Sparks = 24;
    private static readonly Color Butter = Color.FromArgb(255, 240, 166);   // the same butter yellow as the star

    private readonly ChristmasScenery _s;
    private readonly Sprite _halo, _ray, _spark;
    private readonly float[] _sparkStart = new float[Sparks];              // when each spark leaves the star, in seconds
    private readonly float[] _sparkDrift = new float[Sparks];              // its sideways drift, in pixels per second

    public override float Seconds => 6f;
    public override string? Claims => "star";

    public StarFlare(ChristmasScenery s)
    {
        _s = s;
        _halo = Sprite.Glow((int)(s.U * 0.06f), Butter);
        _spark = Sprite.Glow(Math.Max(2, (int)(s.U * 0.005f)), Color.FromArgb(255, 250, 220));

        // The four-pointed sparkle: two thin diamonds, one lying down and
        // one standing up, crossing in the middle, like a glint on glass.
        int size = Math.Max(8, (int)(s.U * 0.09f));
        float c = size / 2f, thin = Math.Max(1f, size * 0.03f);
        _ray = Sprite.Paint(size, size, g =>
        {
            using var ray = new SolidBrush(Color.FromArgb(230, 255, 248, 220));
            g.FillPolygon(ray, [new PointF(0, c), new PointF(c, c - thin), new PointF(size, c), new PointF(c, c + thin)]);
            g.FillPolygon(ray, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, size), new PointF(c - thin, c)]);
        });
    }

    public override void Begin(Random rng)
    {
        for (int i = 0; i < Sparks; i++)
        {
            _sparkStart[i] = 0.8f + 2.4f * (float)rng.NextDouble();       // they leave over a couple of seconds, not all at once
            _sparkDrift[i] = _s.U * 0.03f * ((float)rng.NextDouble() * 2 - 1);
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 0.8f, 1.5f);
        // A slow throb on the halo, and a shimmer on top of it, so it breathes instead of holding still.
        float breathe = 0.8f + 0.2f * MathF.Sin(t * 3f);
        PointF star = _s.StarAt;
        _halo.DrawCentered(fb, star.X, star.Y, on * breathe * 0.85f);

        // The sparkle brightens with the flare and flickers: sin() to a high
        // power is near zero most of the time and spikes briefly.
        float glint = on * (0.35f + 0.65f * MathF.Pow(MathF.Max(0, MathF.Sin(t * 2.6f)), 6));
        _ray.DrawCentered(fb, star.X, star.Y, glint);

        for (int i = 0; i < Sparks; i++)
        {
            float age = t - _sparkStart[i];                                // seconds since this spark left the star
            if (age < 0 || age > 2.2f) continue;
            float x = star.X + _sparkDrift[i] * age;
            float y = star.Y + _s.U * 0.045f * age * age;                  // falling faster and faster
            float twinkle = 0.55f + 0.45f * MathF.Sin(age * 30 + i * 2);
            _spark.DrawCentered(fb, x, y, on * (1 - age / 2.2f) * twinkle);
        }
    }
}
