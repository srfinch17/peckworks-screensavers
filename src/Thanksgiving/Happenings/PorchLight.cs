using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// The lamp beside the farmhouse door comes on (a little buzzing flicker first,
/// the way an old bulb warms up), and three pale moths find it at once. They
/// flutter round the light in loopy paths, bumping about the way moths do,
/// until the lamp clicks off. Then they scatter into the dusk.
///
/// FEYNMAN VERSION: three small pieces, each one easy.
///   1. The LAMP is a soft warm glow (a ready-made light sprite from the engine)
///      stamped over the porch, plus a tiny lantern, and a brightness number
///      worked out from t: it climbs when the lamp comes on and drops when it
///      goes off. The glow is kept faint so the porch is lit, not washed out.
///   2. A MOTH is a flip-book of TWO pages: wings spread flat, wings raised in
///      a V. Swapping them about ten times a second is a flutter.
///   3. A moth's PATH is two sine waves added together, one for left-right and
///      one for up-down, running at slightly different speeds. Two waves that
///      do not agree draw a loop that never quite repeats: the look of a moth
///      that cannot make up its mind. On top of that sits a slow "come in
///      from afar" offset at the start and a "fly away" offset at the end.
///
/// Everything is worked out from t alone. No stencil: the lamp is under the
/// porch roof and the moths are in front of the house, so they are drawn right
/// on the backdrop (layer 0), behind the smoke and the window glows.
/// </summary>
internal sealed class PorchLight : Happening
{
    private const float Total = 9f;
    private const float OnAt = 0.6f, OnFor = 0.9f;        // the lamp warms up from here
    private const float OffAt = 6.8f, OffFor = 0.7f;      // and fades out from here
    private const int Moths = 3;

    private readonly ThanksgivingScenery _s;
    private readonly Sprite _halo, _core, _lantern;       // the wide soft light, the tight bright light, the lamp itself
    private readonly Sprite[] _moth = new Sprite[2];      // wings flat, wings raised
    private readonly float _u;

    // This showing's dice (rolled in Begin).
    private readonly float[] _phaseX = new float[Moths], _phaseY = new float[Moths];
    private readonly float[] _loopX = new float[Moths], _loopY = new float[Moths];
    private readonly float[] _away = new float[Moths];    // the direction each moth arrives from and leaves towards, in radians
    private readonly float[] _beat = new float[Moths];    // each moth's own wingbeat offset, so they do not flap in step

    public override float Seconds => Total;
    public override string? Claims => "farmhouse";

    public PorchLight(ThanksgivingScenery s)
    {
        _s = s;
        _u = s.U;
        _halo = Sprite.Glow(Math.Max(6, (int)(_u * 0.030f)), Color.FromArgb(255, 176, 76));
        _core = Sprite.Glow(Math.Max(3, (int)(_u * 0.007f)), Color.FromArgb(255, 214, 140));

        // The lantern: a dark cap, a glass that is lit when the lamp is on, a dark base.
        int lw = Math.Max(3, (int)(_u * 0.005f)), lh = Math.Max(5, (int)(_u * 0.0085f));
        _lantern = Sprite.Paint(lw, lh, g =>
        {
            using var dark = new SolidBrush(Color.FromArgb(52, 36, 28));
            using var glass = new SolidBrush(Color.FromArgb(255, 238, 180));
            float cap = Math.Max(1f, lh * 0.22f);
            g.FillRectangle(glass, 0, cap, lw, lh - cap * 2);
            g.FillPolygon(dark, [new PointF(0, cap), new PointF(lw / 2f, 0), new PointF(lw, cap)]);
            g.FillRectangle(dark, 0, lh - cap, lw, cap);
        });

        // The moth: two pages. Each wing is a soft rounded triangle in cream, laid over a
        // slightly larger brown-grey one (its shadow side), so the moth reads on the pale
        // wall, on the dark roof and on the gold glow alike. The body is a dark plump dash.
        int ms = Math.Max(7, (int)(_u * 0.0125f));
        for (int page = 0; page < 2; page++)
        {
            int p = page;
            _moth[p] = Sprite.Paint(ms, ms, g =>
            {
                float c = ms / 2f;
                // One wing's outline in "moth units" (1 = the sheet's width), pointing right; the left one is its mirror.
                PointF[] outline = p == 0
                    ? [new(0.03f, 0.00f), new(0.20f, -0.22f), new(0.50f, -0.20f), new(0.44f, 0.02f), new(0.20f, 0.14f)]     // spread: broad, a little up
                    : [new(0.02f, -0.02f), new(0.10f, -0.26f), new(0.22f, -0.48f), new(0.30f, -0.30f), new(0.18f, -0.04f)]; // raised: tall and narrow
                using var fill = new SolidBrush(Color.FromArgb(255, 248, 228));
                using var shade = new SolidBrush(Color.FromArgb(205, 128, 98, 70));
                foreach (float side in new[] { -1f, 1f })
                {
                    PointF[] Place(float grow, float dy) => [.. outline.Select(o => new PointF(c + side * (o.X * ms) + side * grow * ms * (o.X > 0.1f ? 0.05f : 0), c + o.Y * ms + dy * ms + grow * ms * (o.Y > 0 ? 0.04f : -0.01f)))];
                    g.FillClosedCurve(shade, Place(1f, 0.05f), FillMode.Winding, 0.4f);
                    g.FillClosedCurve(fill, Place(0f, 0f), FillMode.Winding, 0.4f);
                }
                using var body = new SolidBrush(Color.FromArgb(92, 62, 40));
                g.FillEllipse(body, c - ms * 0.08f, c - ms * 0.15f, ms * 0.16f, ms * 0.38f);
            });
        }
    }
    public override void Begin(Random rng)
    {
        for (int i = 0; i < Moths; i++)
        {
            _phaseX[i] = (float)rng.NextDouble() * MathF.Tau;
            _phaseY[i] = (float)rng.NextDouble() * MathF.Tau;
            _loopX[i] = 0.008f + 0.006f * (float)rng.NextDouble();             // loop half width, in U
            _loopY[i] = 0.006f + 0.005f * (float)rng.NextDouble();             // loop half height, in U
            // Spread the three round the compass, so they do not all leave together as one clump (mostly upward: moths go up).
            _away[i] = MathF.Tau * (i / (float)Moths) + (float)rng.NextDouble() * 1.0f + 0.4f;
            _beat[i] = (float)rng.NextDouble() * 5f;
        }
    }

    /// <summary>How bright the lamp is, 0 to 1: a flickering warm-up, a steady glow with a faint shimmer, then a soft off.</summary>
    private static float Lamp(float t)
    {
        float up = Smooth((t - OnAt) / OnFor);
        float buzz = 1f - 0.40f * MathF.Max(0f, MathF.Sin(t * 33f)) * (1f - up) * (t > OnAt ? 1f : 0f);   // stutters while it warms up
        float b = up * buzz * (1f + 0.035f * MathF.Sin(t * 9f));
        return Math.Clamp(b, 0f, 1f) * (1f - Smooth((t - OffAt) / OffFor));
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float lx = _s.PorchLight.X, ly = _s.PorchLight.Y;
        float lamp = Lamp(t);

        if (lamp > 0.01f)
        {
            // The lamp itself, then its soft light. Kept faint (0.7 at most) so the clapboard still shows through.
            _lantern.DrawCentered(fb, lx, ly, Math.Min(1f, lamp * 3f));
            _halo.DrawCentered(fb, lx, ly, 0.70f * lamp);
            _core.DrawCentered(fb, lx, ly, 0.85f * lamp);
        }

        // The moths: seen only while there is a light to find (they come in just after it comes on).
        for (int i = 0; i < Moths; i++)
        {
            float arrive = OnAt + 0.5f + 0.45f * i;                     // when this moth turns up
            float leave = OffAt + 0.12f * i;                            // when it gives up and goes
            float inS = Smooth((t - arrive) / 1.3f);                    // 0 = still far off, 1 = at the lamp
            float s = MathF.Max(0f, t - leave);                          // seconds since the lamp went out
            float alpha = Smooth((t - arrive) / 0.6f) * Smooth((Total - t) / 0.9f);
            if (alpha <= 0.01f) continue;

            // Two waves that disagree: the loop. Plus a faster little wobble, so it is never smooth.
            float px = _loopX[i] * _u * (MathF.Sin(t * 2.3f + _phaseX[i]) + 0.35f * MathF.Sin(t * 5.9f + _phaseY[i]));
            float py = _loopY[i] * _u * (MathF.Cos(t * 3.1f + _phaseY[i]) + 0.30f * MathF.Sin(t * 7.3f + _phaseX[i]));

            // Far off at first (arriving), and speeding away at the end (the square of the seconds: it accelerates).
            float far = (1f - inS) * _u * 0.12f + s * s * _u * 0.05f;
            float ax = MathF.Cos(_away[i]), ay = MathF.Sin(_away[i]) - 0.35f;   // the extra -0.35 tips every escape upward
            float x = lx + px + ax * far, y = ly - _u * 0.004f + py + ay * far;

            // Wings: flutter fast near the lamp, a touch slower while it is just cruising in.
            int page = (int)((t * 20f + _beat[i]) % 2f);
            _moth[page].DrawCentered(fb, MathF.Round(x), MathF.Round(y), alpha);
        }
    }
}
