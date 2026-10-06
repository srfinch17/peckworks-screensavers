using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A drift of golden glitter, like the trail behind a magic wand. It swoops in
/// from one upper corner, dives toward the trees and the cabin, loops once
/// in the air, and sweeps up and out of the other side.
///
/// How it works, in two ideas.
///
/// 1. THE PATH. A wand tip follows a curve. The curve is a handful of control
///    points (like the dots in a join-the-dots drawing) joined by smooth
///    curves (a "Catmull-Rom spline": it passes through every dot and bends
///    gently between them). PathAt(k) answers "where is the wand when it is k
///    of the way along, 0 to 1?". The loop is simply a few dots that go round
///    in a circle.
///
/// 2. THE GLITTER. Each of the 84 specks is let go from the wand tip at its
///    own moment. From then on it hangs in the air, where it was dropped, and
///    drifts off a little as it ages (like dust in a sunbeam), twinkling on its
///    own fast beat, and fades out over 2 seconds. Its position is worked
///    out from t alone: PathAt(where the wand was when it let go) plus drift
///    times its age.
///
/// It is in front of the scenery, Santa and the lights (layer 1; only the
/// falling snow is nearer), so there is no stencil.
/// </summary>
internal sealed class FairyDust : Happening
{
    private const int Specks = 84;
    private const float Life = 2.0f;            // how long one speck lasts, in seconds
    private const float WandTime = 7.5f;        // seconds the wand takes to fly the whole path
    private static readonly Color Gold = Color.FromArgb(255, 220, 120);

    private readonly ChristmasScenery _s;
    private readonly Sprite _goldDot, _whiteDot, _bigSparkle, _head;
    private PointF[] _dots = [];                               // the control points of this showing's path
    private readonly float[] _born = new float[Specks];        // when each speck is let go, in seconds
    private readonly PointF[] _drift = new PointF[Specks];     // its drift, in pixels per second
    private readonly PointF[] _scatter = new PointF[Specks];   // where it starts, a little off the path, in pixels
    private readonly float[] _beat = new float[Specks];        // its twinkle speed
    private readonly float[] _phase = new float[Specks];       // and where in the twinkle it starts
    private readonly bool[] _white = new bool[Specks];
    private readonly bool[] _big = new bool[Specks];

    public override float Seconds => WandTime + Life;
    public override int Layer => 1;                        // in front of the scenery, Santa and the lights (see ChristmasScene.Render)

    public FairyDust(ChristmasScenery s)
    {
        _s = s;
        _goldDot = Glint(Math.Max(5, (int)(s.U * 0.016f)), Gold);
        _whiteDot = Glint(Math.Max(5, (int)(s.U * 0.013f)), Color.White);
        _bigSparkle = Glint(Math.Max(8, (int)(s.U * 0.032f)), Color.FromArgb(255, 235, 170));
        _head = Sprite.Glow(Math.Max(4, (int)(s.U * 0.05f)), Color.FromArgb(255, 225, 140));
    }

    /// <summary>
    /// One speck of glitter: a small soft glow with a four-pointed sparkle across it, like a glint on
    /// a flake of foil. "r" is how far the points reach, in pixels.
    /// </summary>
    private static Sprite Glint(int r, Color color)
    {
        int size = r * 2;
        float c = r, thin = Math.Max(0.8f, r * 0.12f);
        return Sprite.Paint(size, size, g =>
        {
            // A faint round glow in the middle, then the two crossing diamonds on top.
            using var gb = new SolidBrush(Color.FromArgb(170, color));
            g.FillEllipse(gb, c - r * 0.35f, c - r * 0.35f, r * 0.7f, r * 0.7f);
            using var b = new SolidBrush(Color.FromArgb(240, (color.R + 255) / 2, (color.G + 255) / 2, (color.B + 255) / 2));   // the color, half way to white
            g.FillPolygon(b, [new PointF(0, c), new PointF(c, c - thin), new PointF(size, c), new PointF(c, c + thin)]);
            g.FillPolygon(b, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, size), new PointF(c - thin, c)]);
        });
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width, h = _s.Height;
        bool fromLeft = rng.Next(2) == 0;

        // The dots, written for a wand that starts at the LEFT. Across is a
        // fraction of the width, down a fraction of the height. The loop is
        // a circle of radius 0.09 U around (0.55 w, 0.50 h), walked round
        // once, so its shape is round on any screen.
        float cx = 0.55f * w, cy = 0.50f * h, r = 0.09f * u;
        var dots = new List<PointF>
        {
            new(-0.05f * w, 0.06f * h),
            new(0.08f * w, 0.12f * h),
            new(0.17f * w, 0.22f * h),
            new(0.26f * w, 0.32f * h),
            new(0.34f * w, 0.43f * h),
            new(cx - r, cy + 0.6f * r),         // arrives at the loop's lower left, heading right
            new(cx + 0.3f * r, cy + r),         // along the bottom
            new(cx + r, cy),                    // up the right side
            new(cx, cy - r),                    // over the top
            new(cx - r, cy),                    // down the left side
            new(cx + 0.5f * r, cy + 0.9f * r),  // round the bottom again, crossing itself
            new(0.70f * w, cy - 0.08f * h),
            new(0.78f * w, 0.38f * h),
            new(0.86f * w, 0.28f * h),
            new(0.94f * w, 0.17f * h),
            new(1.06f * w, 0.05f * h),
        };
        if (!fromLeft) for (int i = 0; i < dots.Count; i++) dots[i] = new PointF(w - dots[i].X, dots[i].Y);
        _dots = dots.ToArray();

        for (int i = 0; i < Specks; i++)
        {
            _born[i] = (WandTime - 0.2f) * i / (Specks - 1f) + 0.3f * (float)rng.NextDouble() - 0.1f;
            _born[i] = Math.Clamp(_born[i], 0f, WandTime);
            double a = rng.NextDouble() * Math.PI * 2;
            float sp = u * (0.01f + 0.03f * (float)rng.NextDouble());
            _drift[i] = new PointF((float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp + u * 0.012f);   // plus a hint of sinking
            float sc = u * 0.012f;
            _scatter[i] = new PointF(sc * ((float)rng.NextDouble() * 2 - 1), sc * ((float)rng.NextDouble() * 2 - 1));
            _beat[i] = 14f + 14f * (float)rng.NextDouble();
            _phase[i] = (float)rng.NextDouble() * MathF.Tau;
            _white[i] = rng.Next(3) == 0;
            _big[i] = rng.Next(9) == 0;
        }
    }

    /// <summary>Where the wand is when it is k of the way along (0 to 1): a smooth curve through the dots.</summary>
    private PointF PathAt(float k)
    {
        int n = _dots.Length - 1;                                   // the number of curved pieces
        float f = Math.Clamp(k, 0f, 1f) * n;
        int i = Math.Min(n - 1, (int)f);
        float x = f - i;                                            // how far through this piece, 0 to 1
        PointF p0 = _dots[Math.Max(0, i - 1)], p1 = _dots[i], p2 = _dots[i + 1], p3 = _dots[Math.Min(n, i + 2)];
        float x2 = x * x, x3 = x2 * x;
        // The standard Catmull-Rom formula: each of the four neighbouring dots pulls on the curve by a different amount.
        float Mix(float a, float b, float c, float d) =>
            0.5f * (2 * b + (-a + c) * x + (2 * a - 5 * b + 4 * c - d) * x2 + (-a + 3 * b - 3 * c + d) * x3);
        return new PointF(Mix(p0.X, p1.X, p2.X, p3.X), Mix(p0.Y, p1.Y, p2.Y, p3.Y));
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 0.2f, 0.3f);

        for (int i = 0; i < Specks; i++)
        {
            float age = t - _born[i];
            if (age < 0 || age > Life) continue;
            PointF p = PathAt(_born[i] / WandTime);
            float x = p.X + _scatter[i].X + _drift[i].X * age;
            float y = p.Y + _scatter[i].Y + _drift[i].Y * age;
            // Twinkle: a fast sine, squared so it spends more time dim and flashes bright.
            float tw = MathF.Sin(t * _beat[i] + _phase[i]);
            float twinkle = 0.5f + 0.5f * tw * tw;
            float life = 1 - age / Life;
            Sprite sp = _big[i] ? _bigSparkle : _white[i] ? _whiteDot : _goldDot;
            sp.DrawCentered(fb, x, y, on * life * twinkle);
        }

        // The wand tip itself: a brighter glow at the head of the stream.
        if (t < WandTime)
        {
            PointF head = PathAt(t / WandTime);
            _head.DrawCentered(fb, head.X, head.Y, on * (0.7f + 0.3f * MathF.Sin(t * 25)));
        }
    }
}
