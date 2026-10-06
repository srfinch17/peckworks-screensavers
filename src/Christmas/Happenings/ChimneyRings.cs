using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// The cabin's chimney puffs out smoke rings, the way a cozy old man with a
/// pipe might: three or four soft see-through rings float up from the
/// chimney, widening and wobbling a little sideways as they go, and fade
/// away. The last one is a heart.
///
/// A sprite cannot be stretched, so each shape (ring or heart) is painted
/// ONCE at ten sizes, from small to big. At any moment a ring's age says
/// how big it should be; we stamp the two nearest sizes, one fading out as
/// the next fades in, so it grows smoothly instead of jumping a size at a time.
///
/// The soft edge: the outline is stroked three times, a wide faint stroke,
/// a medium one and a thin bright one, one on top of the other. That is the
/// cheap way to get a glowing line, like a pencil line smudged with a thumb.
///
/// Everything is worked out from t: ring number i is born at a set time,
/// and its age is t minus that. No stencil: smoke is in front of the
/// backdrop (and rises through the backdrop's own painted puffs, which is fine).
/// </summary>
internal sealed class ChimneyRings : Happening
{
    private const int Sizes = 10;
    private const float Life = 3f;                          // seconds from puff to gone
    private const float Gap = 2.1f;                         // seconds between rings
    private static readonly Color Pale = Color.FromArgb(220, 230, 245);

    private readonly ChristmasScenery _s;
    private readonly Sprite[] _ring = new Sprite[Sizes];    // round rings, small to big
    private readonly Sprite[] _heart = new Sprite[Sizes];   // hearts, small to big
    private int _count;                                     // how many rings this showing (3 or 4); the last is the heart
    private readonly float[] _phase = new float[4];         // each ring's own wobble phase

    public override float Seconds => 10f;

    public ChimneyRings(ChristmasScenery s)
    {
        _s = s;
        for (int i = 0; i < Sizes; i++)
        {
            float across = s.U * (0.01f + 0.04f * i / (Sizes - 1));    // 0.01 U up to 0.05 U wide
            _ring[i] = PaintShape(across, heart: false);
            _heart[i] = PaintShape(across, heart: true);
        }
    }

    private static Sprite PaintShape(float across, bool heart)
    {
        int pad = Math.Max(3, (int)(across * 0.15f));
        int size = Math.Max(8, (int)across) + pad * 2;
        float mid = size / 2f, r = Math.Max(3f, across / 2f);

        using var path = new GraphicsPath();
        if (!heart)
            path.AddEllipse(mid - r, mid - r * 0.85f, r * 2, r * 1.7f);   // a touch squashed, like a ring seen at a slant
        else
        {
            // The classic heart curve, sampled as 48 points joined into a closed shape.
            // (x = 16 sin^3 a, y = 13 cos a - 5 cos 2a - 2 cos 3a - cos 4a: the textbook heart.)
            var pts = new PointF[48];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i * MathF.Tau / pts.Length;
                float hx = 16 * MathF.Pow(MathF.Sin(a), 3);
                float hy = 13 * MathF.Cos(a) - 5 * MathF.Cos(2 * a) - 2 * MathF.Cos(3 * a) - MathF.Cos(4 * a);
                pts[i] = new PointF(mid + hx / 34f * r * 2, mid - hy / 34f * r * 2 + r * 0.1f);   // y is flipped: screen y grows downward
            }
            path.AddPolygon(pts);
        }

        return Sprite.Paint(size, size, g =>
        {
            float w = Math.Max(1.5f, r * 0.4f);
            foreach (var (width, alpha) in new[] { (w * 2.2f, 70), (w * 1.3f, 140), (w * 0.6f, 235) })
            {
                using var pen = new Pen(Color.FromArgb(alpha, Pale), width) { LineJoin = LineJoin.Round };
                g.DrawPath(pen, path);
            }
        });
    }

    public override void Begin(Random rng)
    {
        _count = 3 + rng.Next(2);
        for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)rng.NextDouble() * MathF.Tau;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        for (int i = 0; i < _count; i++)
        {
            float age = t - (0.3f + i * Gap);
            if (age < 0 || age > Life) continue;
            float a = age / Life;                                         // 0 at the puff, 1 when gone

            // Quick to widen at first, then slower (1 - (1-a)^2), as a puff does when it leaves the chimney.
            float grow = (1 - (1 - a) * (1 - a)) * (Sizes - 1);
            int lo = Math.Min(Sizes - 2, (int)grow);
            float frac = grow - lo;

            // Rising, slowing as it goes; swaying on a slow sine whose sway grows with age.
            float x = _s.ChimneyTop.X + _s.U * (0.004f + 0.012f * a) * MathF.Sin(age * 1.5f + _phase[i]) + _s.U * 0.006f * a;
            float y = _s.ChimneyTop.Y - _s.U * 0.11f * (1 - (1 - a) * (1 - a)) - _s.U * 0.006f;

            float op = Fade(age, Life, 0.35f, 1.9f) * 1f;
            Sprite[] set = i == _count - 1 ? _heart : _ring;
            set[lo].DrawCentered(fb, x, y, op * (1 - frac));
            set[lo + 1].DrawCentered(fb, x, y, op * frac);
        }
    }
}
