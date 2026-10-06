using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// "Diamond Fuji" (Daiya Fuji): twice a year the sun sets or rises exactly
/// behind Fuji's summit, and for a minute the crater rim holds it like the
/// stone on a ring, sparkling. Crowds gather to see it.
///
/// The sun rises from behind the crater. We get "behind the mountain" by
/// stamping everything through the open-sky stencil: the mountain simply
/// refuses the paint, so only the part of the sun above the rim shows. As it
/// crests, a starburst blooms: a dozen thin gold rays (long and short, in
/// turn, each twinkling), a wide faint halo, and two little six-sided "lens
/// ghosts", the colored polygons a camera lens bounces about when it looks
/// into a bright light. They drift along the line from the sun toward the
/// middle of the screen.
///
/// It should feel like a gasp: a sudden sparkle, not a cartoon sun with
/// spikes. So the core is small, the halo faint, the rays thin.
/// </summary>
internal sealed class DiamondFuji : Happening
{
    private static readonly Color Gold = Color.FromArgb(255, 214, 150);

    private readonly Scenery _s;
    private readonly Sprite _core, _halo, _ghostA, _ghostB;
    private readonly float _u, _sx, _sy, _dirX, _dirY;

    public override float Seconds => 9f;
    public override string? Claims => "fuji";

    public DiamondFuji(Scenery s)
    {
        _s = s; _u = s.U;
        _sx = s.FujiSummit.X; _sy = s.FujiSummit.Y;
        // Direction from the sun toward the middle of the screen (straight down if they coincide).
        float dx = s.Width * 0.5f - _sx, dy = s.Height * 0.5f - _sy;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) { dx = 0; dy = 1; len = 1; }
        _dirX = dx / len; _dirY = dy / len;

        // Warm-white core: a small hot glow with a soft falloff.
        _core = Sprite.Glow(Math.Max(4, (int)(s.U * 0.05f)), Color.FromArgb(255, 250, 230));
        // The wide, faint halo (radius about 0.15 U), stamped at low opacity.
        _halo = Sprite.Glow(Math.Max(6, (int)(s.U * 0.15f)), Color.FromArgb(255, 236, 190));
        _ghostA = Hexagon(Math.Max(4, (int)(s.U * 0.032f)), Color.FromArgb(255, 196, 120));
        _ghostB = Hexagon(Math.Max(5, (int)(s.U * 0.055f)), Color.FromArgb(236, 150, 200));
    }

    /// <summary>A lens ghost: a soft six-sided blob with a brighter rim.</summary>
    private static Sprite Hexagon(int r, Color c) => Sprite.Paint(r * 2 + 4, r * 2 + 4, g =>
    {
        var pts = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            float a = MathF.PI / 3 * i + MathF.PI / 6;
            pts[i] = new PointF(r + 2 + MathF.Cos(a) * r, r + 2 + MathF.Sin(a) * r);
        }
        using var fill = new SolidBrush(Color.FromArgb(90, c));
        g.FillPolygon(fill, pts);
        using var rim = new Pen(Color.FromArgb(200, c), Math.Max(1f, r * 0.12f));
        g.DrawPolygon(rim, pts);
    });

    public override void Draw(FrameBuffer fb, float t)
    {
        float env = Fade(t, Seconds, 1.2f, 3f);
        if (env < 0.01f) return;

        // The sun climbs from below the rim to just above it over the first 3 s.
        float rise = Smooth(t / 3f);
        float sy = _sy + _u * 0.045f - _u * 0.05f * rise;
        float bloom = Smooth((t - 1.8f) / 1.6f);                  // the starburst opens as it crests
        float twinkle = 0.8f + 0.2f * (float)Math.Sin(t * 6.0);   // the whole light shimmers a little

        // Halo and core, only where the sky is open (the mountain hides the lower part).
        _halo.DrawCentered(fb, _sx, sy, 0.30f * env * (0.35f + 0.65f * bloom), _s.OpenSky);
        _core.DrawCentered(fb, _sx, sy, env * (0.55f + 0.45f * rise), _s.OpenSky);

        // The rays: twelve, long and short in turn, drifting round a few degrees.
        if (bloom > 0.01f)
        {
            float spin = 0.06f * (float)Math.Sin(0.5 * t);
            for (int i = 0; i < 12; i++)
            {
                bool longRay = i % 2 == 0;
                float tw = 0.65f + 0.35f * (float)Math.Sin(t * 7.0 + i * 2.1);
                float len = _u * (longRay ? 0.25f : 0.15f) * bloom * (0.92f + 0.08f * tw);
                float ang = i * MathF.PI / 6 + spin;
                float cs = MathF.Cos(ang), sn = MathF.Sin(ang);
                // Four short pieces, each fainter and thinner than the last, so the ray tapers to a point.
                const int Pieces = 4;
                for (int j = 0; j < Pieces; j++)
                {
                    float r0 = _u * 0.012f + len * j / Pieces, r1 = _u * 0.012f + len * (j + 1) / Pieces;
                    float fall = 1f - j / (float)Pieces;
                    float alpha = 0.75f * MathF.Pow(fall, 1.4f) * tw * env * (longRay ? 1f : 0.8f);
                    float thick = Math.Max(1f, _u * (longRay ? 0.0042f : 0.003f) * (0.4f + 0.6f * fall));
                    fb.Line(new PointF(_sx + cs * r0, sy + sn * r0), new PointF(_sx + cs * r1, sy + sn * r1),
                        Gold, alpha, thick, _s.OpenSky);
                }
            }

            // Two lens ghosts drift along the line toward the middle of the screen.
            float d1 = _u * (0.13f + 0.015f * (float)Math.Sin(0.6 * t));
            float d2 = _u * (0.24f + 0.020f * (float)Math.Sin(0.5 * t + 1.0));
            _ghostA.DrawCentered(fb, _sx + _dirX * d1, sy + _dirY * d1, 0.55f * bloom * env * twinkle, _s.OpenBehindBanks);
            _ghostB.DrawCentered(fb, _sx + _dirX * d2, sy + _dirY * d2, 0.40f * bloom * env * twinkle, _s.OpenBehindBanks);
        }
    }
}
