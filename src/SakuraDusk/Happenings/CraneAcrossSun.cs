using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// A red-crowned crane (tancho) flies slowly across the face of the setting
/// sun. It is one of the best-loved pictures in Japanese art: luck and long
/// life, a black-plum silhouette against a glowing disc.
///
/// HOW IT IS BUILT: the crane is a flip-book. Its wings are painted ONCE, in
/// 30 poses that make one slow, deep wingbeat followed by a short glide, and
/// each frame we stamp the pose that matches the clock. (Six poses would
/// snap; thirty read as smooth.) There are two flip-books, one facing right
/// and one facing left, because each showing picks its direction at random.
///
/// THE WINGS ARE DRAWN IN "FAKE 3D". We look up at the bird from the ground,
/// a little from the side. Its two wings stretch toward us and away from us.
/// If you hold your two hands flat out in front of you and look at them from
/// slightly below, the near hand looks higher than the far one. That is what
/// the maths below does: each wing is first drawn as a flat shape (u along
/// the wing, v across it), then slid up or down the screen according to its
/// flap angle and which side of the bird it is on.
///
/// The crane only shows through OpenSky, so it goes behind the hills, the
/// pagoda and the branches, and in front of the sun's disc.
/// </summary>
internal sealed class CraneAcrossSun : Happening
{
    private const int Poses = 30;                 // pictures in one wingbeat
    private const float Period = 2.1f;            // seconds for one beat and glide: slow and majestic
    private const float SpeedU = 0.11f;           // flight speed, in U per second
    private const float ViewTilt = 0.4363f;       // 25 degrees: how far below the bird we are looking up from

    private static readonly Color Plum = Color.FromArgb(50, 26, 60);
    private static readonly Color FarPlum = Color.FromArgb(70, 38, 80);    // the far wing is a hair lighter so the two can be told apart
    private static readonly Color Rim = Color.FromArgb(205, 255, 176, 112); // the sun's warm light along the edges

    private readonly DuskScenery _s;
    private readonly Sprite[][] _book = new Sprite[2][];   // [0] faces right, [1] faces left
    private readonly float _anchorX, _anchorY;             // where the body's middle is inside each sprite
    private readonly int _w, _h;
    private readonly float _xStart, _xEnd;                 // the two ends of the flight (left end, right end)
    private readonly float _seconds;

    private bool _rightward;                               // this showing's direction
    private float _phase;                                  // this showing's starting point in the wingbeat

    public override float Seconds => _seconds;
    public override string? Claims => "sun,clouds";

    public CraneAcrossSun(DuskScenery s)
    {
        _s = s;
        float L = Math.Max(6f, s.U * 0.08f);              // half the wingspan: the wings are 0.16 U tip to tip
        _w = (int)MathF.Ceiling(L * 2.35f) + 6;
        _h = (int)MathF.Ceiling(L * 2.25f) + 6;
        _anchorX = L * 1.2f + 3;                          // the beak reaches 1.1 L forward, the legs 1.0 L back
        _anchorY = L * 1.1f + 3;                          // the near wing rises about 1 L above the body, the far one dips under it

        _xStart = s.Width * 0.07f;
        _xEnd = s.Width * 0.98f;
        _seconds = Math.Clamp((_xEnd - _xStart) / (SpeedU * s.U), 10f, 18f);

        for (int d = 0; d < 2; d++)
        {
            _book[d] = new Sprite[Poses];
            for (int i = 0; i < Poses; i++)
            {
                float p = i / (float)Poses;
                bool mirror = d == 1;
                _book[d][i] = Sprite.Paint(_w, _h, g => PaintCrane(g, L, WingAngle(p), mirror));
            }
        }
    }

    public override void Begin(Random rng)
    {
        _rightward = rng.Next(2) == 0;
        _phase = (float)rng.NextDouble();
    }

    // ---------------------------------------------------------------- the wingbeat

    /// <summary>
    /// How high the wings are at a point p (0 to 1) in the cycle, in radians
    /// (0 = level, positive = up). The key moments: a glide with the wings
    /// a little raised, a lift to the top, a long deep downstroke, a slower
    /// recovery, and back to the glide. Between keys we ease (Smooth), so the
    /// wings slow down at the top and the bottom like a real heavy bird's.
    /// </summary>
    private static float WingAngle(float p)
    {
        ReadOnlySpan<float> at = [0f, 0.14f, 0.46f, 0.80f, 1f];
        ReadOnlySpan<float> deg = [12f, 58f, -42f, 18f, 12f];
        for (int i = 1; i < at.Length; i++)
            if (p <= at[i])
            {
                float k = Smooth((p - at[i - 1]) / (at[i] - at[i - 1]));
                return (deg[i - 1] + (deg[i] - deg[i - 1]) * k) * MathF.PI / 180f;
            }
        return deg[^1] * MathF.PI / 180f;
    }

    // ---------------------------------------------------------------- the painting

    /// <summary>
    /// One pose of the crane, facing right (or mirrored to face left). All the
    /// numbers are in units of L (half the wingspan): x runs forward, y runs down.
    /// </summary>
    private void PaintCrane(Graphics g, float L, float theta, bool mirror)
    {
        if (mirror)
        {
            g.TranslateTransform(_w, 0);
            g.ScaleTransform(-1, 1);
        }

        // The body bobs: lowest when the wings are up, highest at the bottom of the downstroke.
        float bob = 0.05f * theta / (58f * MathF.PI / 180f);
        PointF P(float x, float y) => new(_anchorX + x * L, _anchorY + (y + bob) * L);

        // ---- a wing: a flat outline (u along it, v across it) slid by the flap angle ----
        // Leading edge, five splayed finger feathers at the tip (tips alternate with notches), then the trailing edge.
        (float u, float v)[] wing =
        [
            (0.00f, -0.05f), (0.30f, -0.10f), (0.62f, -0.10f), (0.86f, -0.07f),
            (1.00f, -0.03f), (0.895f, 0.03f), (0.97f, 0.07f), (0.855f, 0.12f), (0.92f, 0.17f),
            (0.80f, 0.22f), (0.86f, 0.26f), (0.735f, 0.31f), (0.79f, 0.34f),
            (0.60f, 0.38f), (0.40f, 0.40f), (0.20f, 0.36f), (0.00f, 0.20f),
        ];
        // side = +1 for the near wing, -1 for the far one. Looking up from below, the near wing appears HIGHER.
        float cosE = MathF.Cos(ViewTilt), sinE = MathF.Sin(ViewTilt);
        PointF[] Wing(float side, float shoulderX)
        {
            float lift = MathF.Sin(theta) * cosE + side * MathF.Cos(theta) * sinE;   // how far up the screen the wingtip is, per unit of wing length
            var pts = new PointF[wing.Length];
            for (int i = 0; i < wing.Length; i++)
                pts[i] = P(shoulderX - (0.18f * wing[i].u + 1.35f * wing[i].v), -0.05f - lift * wing[i].u);   // 0.18: the wing is swept back a little
            return pts;
        }

        // ---- the neck: a gentle curve stretched forward ----
        PointF[] neck = new PointF[12];
        PointF n0 = new(0.24f, -0.02f), n1 = new(0.45f, 0.02f), n2 = new(0.62f, -0.10f), n3 = new(0.80f, -0.075f);
        for (int i = 0; i < neck.Length; i++)
        {
            float t = i / (neck.Length - 1f), s = 1 - t;
            neck[i] = P(s * s * s * n0.X + 3 * s * s * t * n1.X + 3 * s * t * t * n2.X + t * t * t * n3.X,
                        s * s * s * n0.Y + 3 * s * s * t * n1.Y + 3 * s * t * t * n2.Y + t * t * t * n3.Y);
        }

        // ---- everything the silhouette is made of, back to front ----
        float legW = Math.Max(1.2f, 0.024f * L), neckW = Math.Max(2f, 0.05f * L);
        var shapes = new List<(PointF[] pts, bool line, float width, Color col)>
        {
            (Wing(-1, 0.10f), false, 0, FarPlum),                                       // far wing
            (new[] { P(-0.22f, 0.06f), P(-0.92f, 0.13f) }, true, legW, Plum),           // legs trail behind
            (new[] { P(-0.24f, 0.075f), P(-0.98f, 0.17f) }, true, legW, Plum),
            (new[] { P(-0.25f, -0.07f), P(-0.46f, -0.02f), P(-0.46f, 0.04f), P(-0.25f, 0.08f) }, false, 0, Plum),   // tail
            (Ellipse(P(0f, 0f), 0.30f * L, 0.085f * L), false, 0, Plum),                // body
            (neck, true, neckW, Plum),
            (Ellipse(P(0.84f, -0.075f), 0.06f * L, 0.04f * L), false, 0, Plum),         // head
            (new[] { P(0.87f, -0.095f), P(1.08f, -0.082f), P(0.87f, -0.054f) }, false, 0, Plum),   // beak
            (Wing(+1, 0.06f), false, 0, Plum),                                          // near wing, in front of the body
        };

        // Pass 1: every shape stroked in warm rim light, a little fat. Pass 2: every shape filled in
        // plum over it. What peeks out around the OUTSIDE of the whole bird is the thin warm rim, and no
        // stray rim lines show inside it where one part crosses another.
        float rim = Math.Max(1f, 0.012f * L);
        using (var rimPen = new Pen(Rim, 2 * rim) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            foreach (var sh in shapes)
            {
                if (sh.line)
                {
                    rimPen.Width = sh.width + 2 * rim;
                    g.DrawLines(rimPen, sh.pts);
                }
                else
                {
                    rimPen.Width = 2 * rim;
                    g.DrawPolygon(rimPen, sh.pts);
                }
            }
        foreach (var sh in shapes)
        {
            if (sh.line)
            {
                using var pen = new Pen(sh.col, sh.width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, sh.pts);
            }
            else
            {
                using var brush = new SolidBrush(sh.col);
                g.FillPolygon(brush, sh.pts);
            }
        }

        // The red crown patch on top of the head: the "tancho" itself.
        PointF crown = P(0.82f, -0.108f);
        float cw = Math.Max(1.4f, 0.036f * L), ch = Math.Max(1f, 0.018f * L);
        using var red = new SolidBrush(Color.FromArgb(225, 214, 34, 50));
        g.FillEllipse(red, crown.X - cw, crown.Y - ch, cw * 2, ch * 2);
    }

    /// <summary>An ellipse as a list of points (so it can go through the same stroke-then-fill steps as the other shapes).</summary>
    private static PointF[] Ellipse(PointF c, float rx, float ry)
    {
        var pts = new PointF[20];
        for (int i = 0; i < pts.Length; i++)
        {
            double a = i * Math.PI * 2 / pts.Length;
            pts[i] = new PointF(c.X + rx * (float)Math.Cos(a), c.Y + ry * (float)Math.Sin(a));
        }
        return pts;
    }

    // ---------------------------------------------------------------- the showing

    public override void Draw(FrameBuffer fb, float t)
    {
        float fade = Fade(t, Seconds, 1.0f, 1.2f);
        float run = Math.Clamp(t / Seconds, 0f, 1f);
        float x = _rightward ? _xStart + (_xEnd - _xStart) * run : _xEnd - (_xEnd - _xStart) * run;

        // The flight line: a shallow hill of a path that peaks a little above the sun's middle, so the
        // crane crosses the disc near its widest, and sinks a little toward each end (not far:
        // the hills are low here, and a deeper sink would put the crane behind them).
        float u = _s.U;
        float away = (x - _s.Sun.At.X) / (0.95f * u);
        float y = _s.Sun.At.Y - 0.045f * u + 0.05f * u * MathF.Min(1f, away * away);

        // Which picture of the wingbeat? A cycle that never stops, wrapped so the number stays small.
        float cycle = (t / Period + _phase) % 1f;
        int pose = Math.Min(Poses - 1, (int)(cycle * Poses));

        int d = _rightward ? 0 : 1;
        float ax = _rightward ? _anchorX : _w - _anchorX;      // the mirrored sprite has its body middle on the other side
        _book[d][pose].Draw(fb, (int)MathF.Round(x - ax), (int)MathF.Round(y - _anchorY), fade, _s.OpenSky);
    }
}
