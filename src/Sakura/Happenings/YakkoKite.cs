using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// An edo-dako, the tall rectangular Japanese kite with a bold painted face or
/// emblem, rises from low over the lake, dances in the spring wind for a
/// while, and then sinks away out of the bottom of the screen. Two long paper
/// tails stream below it, and a fine string runs down to whoever is holding it
/// (off screen, at the bottom).
///
/// Three parts, three techniques:
///  - The KITE is a sprite (it is a detailed painting). It tilts a little as it
///    dances, so we paint it at 13 slightly different tilts and stamp the one
///    nearest the tilt it should have right now, like a flip-book.
///  - The TAILS are chains of short straight pieces drawn with FrameBuffer.Line.
///    Each piece leans the way the kite was moving a moment EARLIER (further
///    down the tail, further back in time), which is exactly why a real tail
///    seems to follow and ripple behind the kite.
///  - The STRING is one long line that sags a little, like a clothesline.
///
/// Everything is worked out from t alone: where the kite is is a formula
/// (rise, then a figure-eight, then sink), and the tail asks the same formula
/// "where were you 0.1 seconds ago?" by plugging in an earlier t.
/// </summary>
internal sealed class YakkoKite : Happening
{
    private const float SecondsTotal = 15f;
    private const float RiseEnd = 3.2f;           // finished rising by here
    private const float DropStart = 11.6f;        // starts to sink here
    private const int Tilts = 13;                 // flip-book pages for the tilt
    private const float MaxTilt = 17f;            // degrees each way
    private const int Designs = 3;
    private const int TailPieces = 14;

    private readonly Scenery _s;
    private readonly bool[] _stencil;             // where the kite may be seen: everything behind the banks, trees and branches
    private readonly Sprite[,] _kite;             // [design, tilt]
    private readonly float _kw, _kh;              // the kite's width and height in pixels

    // This showing's dice
    private int _design;
    private float _x0, _yTop, _anchorX, _wind, _sag;
    private int _tailColorA, _tailColorB;

    public override float Seconds => SecondsTotal;

    private static readonly Color[][] TailColors =
    [
        [Color.FromArgb(214, 52, 44), Color.FromArgb(248, 244, 234)],   // red and white
        [Color.FromArgb(40, 44, 70), Color.FromArgb(248, 244, 234)],    // indigo and white
        [Color.FromArgb(214, 52, 44), Color.FromArgb(40, 44, 70)],      // red and indigo
    ];

    public YakkoKite(Scenery s)
    {
        _s = s;
        float u = s.U;
        // Wider than a pixel or two even in the tiny preview box.
        _kw = Math.Max(9f, 0.07f * u);
        _kh = Math.Max(12f, 0.09f * u);

        // The brief says "sky or mountain", but the kite also passes low over the
        // lake while it rises and sinks, so the right stencil is the wider one:
        // sky, mountain, shore AND lake are yes; banks, trees and branches are no.
        _stencil = s.OpenBehindBanks;

        _kite = new Sprite[Designs, Tilts];
        for (int d = 0; d < Designs; d++)
            for (int k = 0; k < Tilts; k++)
                _kite[d, k] = PaintKite(d, TiltDegrees(k), _kw, _kh);
    }

    private static float TiltDegrees(int k) => -MaxTilt + 2 * MaxTilt * k / (Tilts - 1);

    /// <summary>The kite painted at one tilt. Design 0: a kabuki face. 1: a big red sun with black stripes. 2: a bold kanji-like "king" over stripes.</summary>
    private static Sprite PaintKite(int design, float tiltDeg, float kw, float kh)
    {
        int box = (int)MathF.Ceiling(MathF.Sqrt(kw * kw + kh * kh)) + 6;
        Color paper = Color.FromArgb(250, 245, 232);
        Color red = Color.FromArgb(214, 52, 44);
        Color black = Color.FromArgb(28, 24, 30);
        Color bamboo = Color.FromArgb(198, 158, 96);

        return Sprite.Paint(box, box, g =>
        {
            g.TranslateTransform(box / 2f, box / 2f);
            g.RotateTransform(tiltDeg);
            // Draw in "fractions of the kite": x from -0.5 to 0.5 across, y from -0.5 to 0.5 down.
            g.ScaleTransform(kw, kh);
            using var fill = new SolidBrush(paper);
            g.FillRectangle(fill, -0.5f, -0.5f, 1f, 1f);

            using var rb = new SolidBrush(red);
            using var kb = new SolidBrush(black);
            using var wb = new SolidBrush(paper);
            // A pen is also scaled by (kw, kh), so a line's thickness is in kite fractions too.
            Pen Brush(Color c, float w) => new(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            if (design == 0)
            {
                // Kabuki face: hair, red face, fierce brows, white eyes with black pupils, a bold mouth.
                g.FillEllipse(rb, -0.40f, -0.30f, 0.80f, 0.70f);
                g.FillPolygon(kb, [new PointF(-0.5f, -0.5f), new PointF(0.5f, -0.5f), new PointF(0.5f, -0.27f),
                                   new PointF(0.08f, -0.20f), new PointF(0f, -0.13f), new PointF(-0.08f, -0.20f), new PointF(-0.5f, -0.27f)]);
                using (var brow = Brush(black, 0.075f))
                {
                    g.DrawLine(brow, -0.31f, -0.12f, -0.07f, -0.02f);
                    g.DrawLine(brow, 0.31f, -0.12f, 0.07f, -0.02f);
                }
                g.FillEllipse(wb, -0.30f, 0.03f, 0.22f, 0.10f);
                g.FillEllipse(wb, 0.08f, 0.03f, 0.22f, 0.10f);
                g.FillEllipse(kb, -0.22f, 0.04f, 0.09f, 0.085f);
                g.FillEllipse(kb, 0.13f, 0.04f, 0.09f, 0.085f);
                using (var mouth = Brush(black, 0.06f))
                    g.DrawArc(mouth, -0.18f, 0.14f, 0.36f, 0.20f, 10, 160);
                g.FillRectangle(kb, -0.5f, 0.42f, 1f, 0.08f);                     // a black band along the bottom edge
            }
            else if (design == 1)
            {
                // The rising sun: a big red disc with a black ring and black stripes below.
                using (var ring = Brush(black, 0.045f))
                    g.DrawEllipse(ring, -0.38f, -0.40f, 0.76f, 0.60f);
                g.FillEllipse(rb, -0.355f, -0.375f, 0.71f, 0.55f);
                g.FillRectangle(kb, -0.5f, 0.28f, 1f, 0.08f);
                g.FillRectangle(rb, -0.5f, 0.38f, 1f, 0.12f);
                g.FillRectangle(kb, -0.5f, -0.5f, 1f, 0.05f);
            }
            else
            {
                // A bold brush "king" (three bars and a post) over red and black bands.
                using var bar = Brush(black, 0.075f);
                g.DrawLine(bar, -0.30f, -0.34f, 0.30f, -0.34f);
                g.DrawLine(bar, -0.22f, -0.12f, 0.22f, -0.12f);
                g.DrawLine(bar, -0.34f, 0.10f, 0.34f, 0.10f);
                g.DrawLine(bar, 0f, -0.36f, 0f, 0.10f);
                g.FillRectangle(rb, -0.5f, 0.22f, 1f, 0.12f);
                g.FillRectangle(kb, -0.5f, 0.36f, 1f, 0.08f);
                g.FillRectangle(rb, -0.5f, 0.46f, 1f, 0.04f);
            }

            // The bamboo frame showing at the edges, plus the spine and a cross-bar.
            float edge = Math.Max(1.6f, 0.014f * kh);
            // Pens scale with the transform, so give x and y their own thickness.
            using var fx = new Pen(bamboo, edge / kw);
            using var fy = new Pen(bamboo, edge / kh);
            g.DrawLine(fy, -0.5f, -0.5f, -0.5f, 0.5f);
            g.DrawLine(fy, 0.5f, -0.5f, 0.5f, 0.5f);
            g.DrawLine(fx, -0.5f, -0.5f, 0.5f, -0.5f);
            g.DrawLine(fx, -0.5f, 0.5f, 0.5f, 0.5f);
            using var spine = new Pen(Color.FromArgb(120, bamboo), edge * 0.7f / kw);
            g.DrawLine(spine, 0f, -0.5f, 0f, 0.5f);
        });
    }

    public override void Begin(Random rng)
    {
        _design = rng.Next(Designs);
        // Find a spot of open sky for the dance. The branches hang in from the top corners and
        // differ on every launch, so test the stencil: sample a grid over the area the kite will
        // sweep, and keep the best of 24 tries (the first one that is nearly all clear wins).
        float bestScore = -1;
        for (int tries = 0; tries < 24 && bestScore < 0.97f; tries++)
        {
            bool left = rng.Next(2) == 0;
            float cx = _s.Width * (left ? 0.22f + 0.18f * rng.NextSingle() : 0.60f + 0.18f * rng.NextSingle());
            float cy = _s.Height * (0.20f + 0.15f * rng.NextSingle());
            int open = 0, total = 0;
            for (int gy = -4; gy <= 4; gy++)
                for (int gx = -4; gx <= 4; gx++)
                {
                    int px = (int)Math.Clamp(cx + gx * 0.04f * _s.U, 0, _s.Width - 1), py = (int)Math.Clamp(cy + gy * 0.025f * _s.U, 0, _s.Height - 1);
                    total++;
                    if (_stencil[py * _s.Width + px]) open++;
                }
            float score = open / (float)total;
            if (score > bestScore) { bestScore = score; _x0 = cx; _yTop = cy; }
        }
        // The string runs to someone off the bottom, on the side away from the middle of the screen... or toward it.
        _anchorX = _x0 + (rng.Next(2) == 0 ? -1f : 1f) * _s.Width * (0.12f + 0.08f * rng.NextSingle());
        _anchorX = Math.Clamp(_anchorX, _s.Width * 0.05f, _s.Width * 0.95f);
        _wind = rng.NextSingle() * MathF.Tau;                                      // each showing dances differently
        _sag = 0.5f + 0.5f * rng.NextSingle();
        _tailColorA = _tailColorB = rng.Next(TailColors.Length);
    }

    /// <summary>How much the kite is dancing at time t: 0 while it climbs, 1 mid-flight, 0 again as it sinks.</summary>
    private static float Dance(float t) => Smooth((t - 1.6f) / 2.4f) * (1f - Smooth((t - (DropStart - 1.2f)) / 2f));

    /// <summary>Where the kite's center is at time t (a pure formula, so the tail can ask about the past).</summary>
    private PointF Center(float t)
    {
        float u = _s.U, H = _s.Height;
        float low = H + 0.34f * u;                                                  // far enough below the screen that kite and tails are hidden
        float y = low + (_yTop - low) * Smooth(t / RiseEnd);                       // rise
        y = y + (low - y) * Smooth((t - DropStart) / (SecondsTotal - DropStart));  // sink
        float dance = Dance(t);
        float w = 1.25f;                                                           // radians per second of the dance
        float x = _x0 + dance * 0.13f * u * MathF.Sin(w * t + _wind);
        y += dance * (0.045f * u * MathF.Sin(2 * w * t + 2 * _wind) + 0.008f * u * MathF.Sin(3.3f * t));   // the figure-eight
        return new PointF(x, y);
    }

    /// <summary>The tilt in degrees: leans into the swoops and wobbles a little.</summary>
    private float Tilt(float t)
    {
        float d = Dance(t);
        float tilt = d * (10f * MathF.Sin(1.25f * t + _wind + 0.7f) + 5f * MathF.Sin(2.9f * t + 1.3f * _wind));
        return Math.Clamp(tilt, -MaxTilt, MaxTilt);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        PointF c = Center(t);
        if (c.Y - _kh > _s.Height + 0.3f * u && t < DropStart) return;            // still hidden below the screen
        float fade = Fade(t, SecondsTotal, 0.3f, 0.3f);

        float tilt = Tilt(t);
        float rad = tilt * MathF.PI / 180f, cs = MathF.Cos(rad), sn = MathF.Sin(rad);
        PointF Rot(float lx, float ly) => new(c.X + lx * cs - ly * sn, c.Y + lx * sn + ly * cs);   // a point on the kite, in screen terms

        // ---- the string, behind the kite: from its bridle down to off the bottom of the screen ----
        PointF from = Rot(0, 0.14f * _kh);
        PointF to = new(_anchorX, _s.Height + 0.08f * u);
        float thick = Math.Max(1f, 0.0011f * u);
        float sagPx = (0.03f + 0.03f * _sag) * u;
        float bow = 0.035f * u * MathF.Sin(0.8f * t + _wind);                      // the wind bows it sideways
        PointF prev = from;
        for (int i = 1; i <= 24; i++)
        {
            float s = i / 24f, k = 4 * s * (1 - s);
            var p = new PointF(from.X + (to.X - from.X) * s + bow * k, from.Y + (to.Y - from.Y) * s + sagPx * k);
            fb.Line(prev, p, Color.FromArgb(246, 242, 228), 0.78f * fade, thick, _stencil);
            prev = p;
        }

        // ---- the tails: each piece leans the way the kite was moving a little earlier ----
        float segLen = 0.0135f * u;
        for (int tail = 0; tail < 2; tail++)
        {
            float side = tail == 0 ? -1f : 1f;
            PointF p0 = Rot(side * 0.30f * _kw, 0.5f * _kh);
            for (int j = 0; j < TailPieces; j++)
            {
                float tj = t - 0.085f * (j + 1);                                   // further down the tail = longer ago
                float vx = (Center(tj).X - Center(tj - 0.05f).X) / 0.05f;          // sideways speed back then (pixels per second)
                float lean = Math.Clamp(vx / u * 1.9f, -0.9f, 0.9f);               // moving right: the tail trails to the left
                float ripple = MathF.Sin(t * 5.2f + j * 0.65f + side) * 0.55f * (j + 1f) / TailPieces;   // grows toward the tip
                float ang = MathF.PI / 2 + lean + ripple;
                var p1 = new PointF(p0.X + MathF.Cos(ang) * segLen, p0.Y + MathF.Sin(ang) * segLen);
                Color col = TailColors[_tailColorA][j % 2];
                // Paper strips are wider toward the tip, like a real ribbon.
                fb.Line(p0, p1, col, fade, Math.Max(2f, (0.0055f + 0.0009f * j) * u), _stencil);
                p0 = p1;
            }
        }

        // ---- the kite itself ----
        int ti = (int)MathF.Round((tilt + MaxTilt) / (2 * MaxTilt) * (Tilts - 1));
        _kite[_design, Math.Clamp(ti, 0, Tilts - 1)].DrawCentered(fb, c.X, c.Y, fade, _stencil);
    }
}
