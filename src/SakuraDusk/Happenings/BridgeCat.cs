using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// A cat crosses the arched bridge. It walks up onto the deck at one foot,
/// follows the curve up (its body tilting with the slope), stops at the top,
/// sits down with its back to us to watch the sunset, its tail swishing,
/// washes a paw, then gets up and walks down the far side and off.
///
/// At dusk it is a silhouette: a dark warm brown, with a thin bright orange
/// rim of light all round its top edge (the sun is in front of it, so it is
/// lit from behind), a ginger patch on its back and cream toes, so it reads as
/// a calico. Big head, big pointed ears, a long tail: the kawaii proportions.
///
/// FEYNMAN VERSION: three flip-books, drawn once.
///  1. WALK: 24 side-on pages of a cat mid-stride (legs swapping, body bobbing,
///     tail swaying). The page shown depends on how FAR it has walked, so legs
///     slow down when the cat slows down and never "skate".
///  2. SIT: the cat from behind. 24 pages for the head-dip and paw-wash, and a
///     separate 24-page flip-book for just the tail, so the tail keeps
///     swishing while the cat washes.
/// The cat must tilt to follow the bridge, and a flip-book page cannot tilt on
/// its own, so the pages are kept as plain pixel lists and stamped by a small
/// "rotating stamp" written here (for each screen pixel, find which pixel of
/// the page lands on it, like looking through a turned stencil). The pages are
/// painted at twice the size and sampled four times per pixel, which keeps the
/// edges smooth even when tilted.
///
/// Where it walks: the bridge hands out DeckAt(t) for any t from 0 (left foot)
/// to 1 (right foot). t is not an even distance (it bunches up), so in the
/// constructor we measure the deck as a chain of short steps and add up the
/// length. The cat is given a distance walked, and we look up the matching t.
/// </summary>
internal sealed class BridgeCat : Happening
{
    private const int Pages = 24;
    private const int N = 240;                       // steps used to measure the deck
    private const float Turn = 0.7f;                 // the crossfade between walking and sitting, seconds
    private const float Sit = 7.0f;                  // how long it sits
    private const float Big = 2f;                    // pages are painted at 2x and sampled down

    private static readonly Color Fur = Color.FromArgb(255, 62, 36, 34);
    private static readonly Color Rim = Color.FromArgb(255, 255, 178, 96);
    private static readonly Color Ginger = Color.FromArgb(255, 150, 82, 44);
    private static readonly Color Cream = Color.FromArgb(255, 222, 176, 140);

    /// <summary>One flip-book page: its pixels, and where the cat's feet are on it (the pivot).</summary>
    private sealed class Page
    {
        public int W, H;
        public float Ox, Oy;
        public float Reach;                      // how far (in screen pixels) the cat's painted pixels stray from the feet
        public uint[] Argb = [];
    }

    private readonly DuskScenery _s;
    private readonly float _q;                       // the cat's length in screen pixels (head to tail tip)
    private readonly float _u;
    private readonly Page[] _walk = new Page[Pages], _wash = new Page[Pages], _tail = new Page[Pages];
    private readonly float[] _arc = new float[N + 1];    // the deck's length from the left foot to step i
    private readonly float _len, _walkSecs;
    private int _dir = 1;                            // +1 walks left to right, -1 right to left

    public override float Seconds => 2 * _walkSecs + 2 * Turn + Sit;
    public override string? Claims => "bridge";
    public override int Layer => 1;                  // on the bridge, in front of the boat

    public BridgeCat(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _q = Math.Max(16f, s.U * 0.07f);

        for (int i = 1; i <= N; i++)
        {
            PointF a = s.Bridge.DeckAt((i - 1) / (float)N), b = s.Bridge.DeckAt(i / (float)N);
            _arc[i] = _arc[i - 1] + MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        }
        _len = _arc[N];
        _walkSecs = _len / 2 / (0.065f * s.U);       // an average walking pace of 0.065 U per second

        for (int i = 0; i < Pages; i++)
        {
            float ph = MathF.Tau * i / Pages;
            _walk[i] = Make(g => Both(g, (c, grow, detail) => WalkShapes(g, c, grow, ph, detail)));
            float p = i / (Pages - 1f);
            float a = Smooth(p / 0.16f) * Smooth((1 - p) / 0.16f);       // 0 at both ends: the idle sit
            float lick = MathF.Tau * 3 * p;
            _wash[i] = Make(g => Both(g, (c, grow, detail) => SitShapes(g, c, grow, a, lick, detail)));
            _tail[i] = Make(g => Both(g, (c, grow, detail) => TailShape(g, c, grow, ph)));
        }
    }

    public override void Begin(Random rng) => _dir = rng.Next(2) == 0 ? 1 : -1;

    // ================================================================ painting the pages

    /// <summary>Paints one page: a canvas 1.4 cat-lengths wide, with the feet on a line near the bottom, at the middle.</summary>
    private Page Make(Action<Graphics> paint)
    {
        float Q = _q * Big;
        int w = (int)(1.4f * Q), h = (int)(0.85f * Q);
        float ox = w / 2f, oy = h - 0.04f * Q;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(Q, Q);                  // from here on, one unit = the cat's whole length
            paint(g);
        }
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var raw = new int[w * h];
        try { Marshal.Copy(data.Scan0, raw, 0, raw.Length); } finally { bmp.UnlockBits(data); }
        var argb = new uint[raw.Length];
        Buffer.BlockCopy(raw, 0, argb, 0, raw.Length * 4);
        // Measure how far the painted pixels reach from the feet, so the stamp only scans that box.
        float far2 = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((argb[y * w + x] >> 24) != 0) far2 = MathF.Max(far2, (x - ox) * (x - ox) + (y - oy) * (y - oy));
        return new Page { W = w, H = h, Ox = ox, Oy = oy, Argb = argb, Reach = MathF.Sqrt(far2) / Big + 2 };
    }

    /// <summary>
    /// Draws a pose twice: first a slightly fatter copy in the rim-light orange, nudged
    /// UP, then the dark cat on top. What peeks out round the top and sides is the thin
    /// bright edge that makes a backlit silhouette glow.
    /// </summary>
    private void Both(Graphics g, Action<Color, float, bool> shapes)
    {
        float rim = Math.Max(0.016f, 2.2f / (_q * Big));
        var st = g.Save();
        g.TranslateTransform(0, -rim * 0.7f);
        shapes(Rim, rim, false);
        g.Restore(st);
        shapes(Fur, 0f, true);
    }

    // Tiny drawing helpers. "grow" fattens an outline by that much on every side
    // (the rim pass); in the dark pass it is 0.
    private static void Oval(Graphics g, Color c, float grow, float cx, float cy, float rx, float ry)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
        if (grow > 0) { using var p = new Pen(c, grow * 2); g.DrawEllipse(p, cx - rx, cy - ry, rx * 2, ry * 2); }
    }

    private static void Poly(Graphics g, Color c, float grow, params PointF[] pts)
    {
        using var b = new SolidBrush(c);
        g.FillPolygon(b, pts);
        if (grow > 0) { using var p = new Pen(c, grow * 2) { LineJoin = LineJoin.Round }; g.DrawPolygon(p, pts); }
    }

    private static void Stroke(Graphics g, Color c, float grow, float width, PointF a, PointF b)
    {
        using var p = new Pen(c, width + grow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(p, a, b);
    }

    private static void Curl(Graphics g, Color c, float grow, float width, PointF a, PointF b, PointF c2, PointF d)
    {
        using var p = new Pen(c, width + grow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawBezier(p, a, b, c2, d);
    }

    /// <summary>The side view, walking right. ph is where in the stride we are (0 to 2 pi).</summary>
    private static void WalkShapes(Graphics g, Color c, float grow, float ph, bool detail)
    {
        // The body bobs up twice per stride (once per footfall).
        float bob = -0.016f * MathF.Abs(MathF.Sin(ph));
        float tail = MathF.Sin(ph + 1f);

        // Far legs first, then near legs (they are the same shape here, but order is tidy).
        (float hip, float a)[] legs = [(-0.14f, ph), (0.16f, ph + MathF.PI), (-0.14f, ph + MathF.PI), (0.16f, ph)];
        foreach (var (hip, a) in legs)
        {
            // A foot swings back and forth under the hip, and lifts while it swings forward.
            PointF foot = new(hip + 0.085f * MathF.Sin(a), -0.031f - 0.05f * Math.Max(0f, MathF.Cos(a)));
            Stroke(g, c, grow, 0.062f, new PointF(hip, -0.19f + bob), foot);
            if (detail) Oval(g, Cream, 0, foot.X, foot.Y + 0.004f, 0.034f, 0.03f);
        }

        // Tail: up and back in a hook, tip swaying.
        Curl(g, c, grow, 0.05f, new PointF(-0.19f, -0.29f + bob), new PointF(-0.34f, -0.27f),
             new PointF(-0.44f + 0.03f * tail, -0.42f), new PointF(-0.37f + 0.04f * tail, -0.58f));

        Oval(g, c, grow, 0f, -0.265f + bob, 0.22f, 0.115f);                  // body
        float hy = -0.36f + bob * 0.6f;
        Oval(g, c, grow, 0.27f, hy, 0.125f, 0.108f);                          // head
        Poly(g, c, grow, new PointF(0.185f, hy - 0.06f), new PointF(0.20f, hy - 0.2f), new PointF(0.265f, hy - 0.095f));    // far ear
        Poly(g, c, grow, new PointF(0.285f, hy - 0.095f), new PointF(0.35f, hy - 0.2f), new PointF(0.375f, hy - 0.04f));    // near ear

        if (detail)
        {
            Oval(g, Ginger, 0, -0.04f, -0.31f + bob, 0.115f, 0.05f);          // the ginger patch on its back
            Oval(g, Ginger, 0, 0.23f, hy - 0.05f, 0.06f, 0.035f);            // and a cap on its head
            Oval(g, Color.FromArgb(255, 255, 238, 214), 0, 0.325f, hy - 0.005f, 0.019f, 0.021f);   // the bright eye
            Oval(g, Color.FromArgb(150, 214, 96, 110), 0, 0.345f, hy + 0.04f, 0.026f, 0.015f);    // a blush
        }
    }

    /// <summary>The cat sitting, seen from behind. a = how far into the wash (0 idle, 1 head right down); lick = the paw's up-down beat.</summary>
    private static void SitShapes(Graphics g, Color c, float grow, float a, float lick, bool detail)
    {
        Oval(g, c, grow, 0f, -0.15f, 0.165f, 0.15f);                       // the round bottom
        Oval(g, c, grow, 0f, -0.28f, 0.12f, 0.14f);                        // the shoulders
        Oval(g, c, grow, -0.11f, -0.07f, 0.075f, 0.075f);                  // hind feet, peeking out either side
        Oval(g, c, grow, 0.11f, -0.07f, 0.075f, 0.075f);

        // The paw: while washing, one arm comes up beside the head and licks up and down.
        if (a > 0.04f)
        {
            float pawY = -0.22f + (-0.17f - 0.025f * MathF.Sin(lick)) * a;
            PointF paw = new(-0.15f - 0.01f * a, pawY);
            Stroke(g, c, grow, 0.06f, new PointF(-0.09f, -0.25f), paw);
            Oval(g, c, grow, paw.X, paw.Y, 0.04f, 0.045f);
        }

        // The head, dipped and tilted toward the paw as it washes (turned about its neck).
        var st = g.Save();
        g.TranslateTransform(-0.035f * a, 0.025f * a);
        g.TranslateTransform(0f, -0.33f);
        g.RotateTransform(-24f * a);
        g.TranslateTransform(0f, 0.33f);
        Oval(g, c, grow, 0f, -0.43f, 0.125f, 0.105f);
        Poly(g, c, grow, new PointF(-0.125f, -0.47f), new PointF(-0.1f, -0.62f), new PointF(-0.03f, -0.52f));
        Poly(g, c, grow, new PointF(0.125f, -0.47f), new PointF(0.1f, -0.62f), new PointF(0.03f, -0.52f));
        if (detail)
        {
            Oval(g, Ginger, 0, 0.03f, -0.47f, 0.075f, 0.045f);
            Poly(g, Ginger, 0, new PointF(0.105f, -0.5f), new PointF(0.09f, -0.58f), new PointF(0.04f, -0.52f));
        }
        g.Restore(st);

        if (detail)
            Oval(g, Ginger, 0, -0.04f, -0.25f, 0.085f, 0.09f);             // the ginger patch on its back
    }

    /// <summary>The sitting cat's tail, lying along the ground and curling up at the tip. ph swishes it.</summary>
    private static void TailShape(Graphics g, Color c, float grow, float ph)
    {
        float s1 = MathF.Sin(ph), s2 = MathF.Sin(ph + 1f);
        Curl(g, c, grow, 0.05f, new PointF(0.09f, -0.05f), new PointF(0.26f, -0.005f),
             new PointF(0.37f + 0.03f * s1, -0.07f), new PointF(0.33f + 0.06f * s1, -0.2f - 0.05f * (0.5f + 0.5f * s2)));
    }

    // ================================================================ placing the cat

    /// <summary>
    /// Where the cat's feet are, and the tilt of the deck, after walking a
    /// distance d from the left foot. Returns (x, y, angle in radians).
    /// </summary>
    private (float X, float Y, float Ang) Place(float d)
    {
        d = Math.Clamp(d, 0f, _len);
        int lo = 0, hi = N;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (_arc[mid] <= d) lo = mid; else hi = mid; }
        float span = Math.Max(0.0001f, _arc[hi] - _arc[lo]);
        float t = (lo + (d - _arc[lo]) / span) / N;
        PointF p = _s.Bridge.DeckAt(t);
        PointF a = _s.Bridge.DeckAt(Math.Max(0f, t - 0.01f)), b = _s.Bridge.DeckAt(Math.Min(1f, t + 0.01f));
        float ang = MathF.Atan2(b.Y - a.Y, b.X - a.X);
        // The deck line is the middle of a thick stroke: stand on its top surface.
        float up = Math.Max(1.5f, _u * 0.004f);
        return (p.X + MathF.Sin(ang) * up, p.Y - MathF.Cos(ang) * up, ang);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float W = _walkSecs, half = _len / 2;
        float op = Fade(t, Seconds, 0.7f, 1.0f);
        float stride = 0.6f * _q;                    // how far one full leg cycle carries the cat

        float tSitStart = W + Turn;
        float tStand = tSitStart + Sit;
        float tDown = tStand + Turn;

        if (t < W || t >= tDown)
        {
            // Walking. Up: easing in from the foot's pace and settling to a stop at the top.
            // Down: easing away from rest, and still moving as it leaves.
            float d;
            if (t < W) d = half * (2 * Smooth(0.5f + 0.5f * t / W) - 1);
            else d = half * (1 + 2 * Smooth(0.5f * (t - tDown) / W));
            Stamp(fb, _walk[WalkPage(d, stride)], d, op, mirror: false);
            return;
        }

        if (t < tSitStart || t >= tStand)
        {
            // Turning between walking and sitting: the two stamps overlap, a dissolve.
            float x = t < tSitStart ? Smooth((t - W) / Turn) : 1 - Smooth((t - tStand) / Turn);
            float walkOp = 1 - Smooth((x - 0.35f) / 0.65f);
            Stamp(fb, _walk[WalkPage(half, stride)], half, op * walkOp, mirror: false);
            SitStamp(fb, t - tSitStart, Smooth(x), op);
            return;
        }
        SitStamp(fb, t - tSitStart, 1f, op);
    }

    private static int WalkPage(float d, float stride) =>
        (int)(((d / stride * Pages) % Pages + Pages) % Pages);

    /// <summary>Stamps the sitting cat (body page by wash progress, tail page by time) at the top of the bridge.</summary>
    private void SitStamp(FrameBuffer fb, float ts, float opacity, float op)
    {
        float wp = Math.Clamp((ts - 1.6f) / 3.0f, 0f, 1f);               // the wash happens between 1.6 s and 4.6 s of the sit
        int wi = (int)MathF.Round(wp * (Pages - 1));
        int ti = (int)(((ts / 3.2f * Pages) % Pages + Pages) % Pages);   // one tail swish per 3.2 s
        Stamp(fb, _wash[wi], _len / 2, opacity * op, mirror: false);
        Stamp(fb, _tail[ti], _len / 2, opacity * op, mirror: false);
    }

    /// <summary>
    /// The rotating stamp. Places page p with its feet at distance d along the deck,
    /// tilted to the deck's slope. mirror is applied here from the walking direction
    /// (so a cat walking right to left faces left); the sitting cat is symmetrical
    /// enough that its mirror only moves its tail to the other side, which is fine.
    /// </summary>
    private void Stamp(FrameBuffer fb, Page p, float d, float opacity, bool mirror)
    {
        if (opacity <= 0.004f) return;
        // For a cat going right to left the distance is measured from the right foot.
        float dd = _dir > 0 ? d : _len - d;
        var (px, py, ang) = Place(dd);
        bool flip = _dir < 0;
        float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
        float reach = p.Reach;
        int x0 = Math.Max(0, (int)(px - reach)), x1 = Math.Min(fb.Width - 1, (int)(px + reach));
        int y0 = Math.Max(0, (int)(py - reach)), y1 = Math.Min(fb.Height - 1, (int)(py + reach));
        uint[] frame = fb.Pixels;
        // Four sub-pixel sample spots per screen pixel (a 2 by 2 grid), found in the page
        // by turning each spot back through the tilt. Walking one pixel to the right
        // moves every spot by the same small step, so the turn is worked out once per
        // row and then only added to (much cheaper than redoing it for every pixel).
        float stepX = (flip ? -cos : cos) * Big, stepY = -sin * Big;       // where one pixel right lands in the page
        Span<float> sx = stackalloc float[4], sy = stackalloc float[4];
        for (int y = y0; y <= y1; y++)
        {
            for (int k = 0; k < 4; k++)
            {
                float dx = x0 + 0.5f + ((k & 1) - 0.5f) * 0.5f - px, dy = y + 0.5f + ((k >> 1) - 0.5f) * 0.5f - py;
                float lx = cos * dx + sin * dy, ly = -sin * dx + cos * dy;
                sx[k] = p.Ox + (flip ? -lx : lx) * Big;
                sy[k] = p.Oy + ly * Big;
            }
            for (int x = x0; x <= x1; x++)
            {
                int sa = 0, sr = 0, sg = 0, sb = 0;
                for (int k = 0; k < 4; k++)
                {
                    float fx = sx[k], fy = sy[k];
                    sx[k] = fx + stepX; sy[k] = fy + stepY;
                    if (fx < 0 || fy < 0) continue;
                    int ix = (int)fx, iy = (int)fy;
                    if (ix >= p.W || iy >= p.H) continue;
                    uint c = p.Argb[iy * p.W + ix];
                    int a = (int)(c >> 24);
                    if (a == 0) continue;
                    sa += a; sr += a * (int)((c >> 16) & 0xFF); sg += a * (int)((c >> 8) & 0xFF); sb += a * (int)(c & 0xFF);
                }
                if (sa == 0) continue;
                int i = y * fb.Width + x;
                frame[i] = FrameBuffer.Blend(frame[i], sr / sa, sg / sa, sb / sa, sa / (4f * 255f) * opacity);
            }
        }
    }
}
