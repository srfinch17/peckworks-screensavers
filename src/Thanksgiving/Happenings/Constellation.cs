using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// Join-the-dots in the dark upper sky. A handful of warm white stars
/// brighten one after another (each pops bright and settles), then thin
/// lines draw themselves from star to star, one at a time, until the dots
/// have made a harvest picture: a turkey with its fanned tail, a cornucopia
/// with fruit spilling out, a maple leaf, a slice of pie with a dollop of
/// cream, or an acorn. The finished picture holds for a few seconds with its
/// stars twinkling, then everything fades out.
///
/// Each picture is a list of points and a list of lines between them, written
/// in a made-up grid. A point can be a STAR (it lights up) or just a CORNER
/// the lines bend through. The pictures are drawn on any scale and
/// MEASURED in the constructor: each is shifted so its middle is at (0, 0)
/// and shrunk so its widest reach is 1, so every picture is the same size
/// on the sky. (Think of tracing paper cut to a square.)
///
/// Everything goes through the OpenSky stencil, so a picture that drifts onto
/// a hill or behind a corner branch is hidden by it.
///
/// It sits in the upper middle of the sky, left of the harvest moon, and
/// shares the "skyshow" claim with the fireworks.
/// </summary>
internal sealed class Constellation : Happening
{
    private const int Pics = 5;
    private static readonly Color Warm = Color.FromArgb(255, 240, 206);

    private readonly ThanksgivingScenery _s;
    private readonly Sprite _halo, _core;
    private readonly List<(float X, float Y, bool Star)>[] _pts = new List<(float, float, bool)>[Pics];
    private readonly List<(int A, int B)>[] _edges = new List<(int, int)>[Pics];

    private int _pic;
    private PointF _center;
    private float _half;                                         // half the picture's size, in pixels
    private float[] _phase = [];                                 // each star's twinkle phase
    private float _starGap, _lineStart, _lineTime, _seconds;     // the timetable, worked out in Begin

    public override float Seconds => _seconds;
    public override string? Claims => "skyshow";                 // shares the sky with the fireworks

    public Constellation(ThanksgivingScenery s)
    {
        _s = s;
        _seconds = 11f;
        _halo = Sprite.Glow(Math.Max(3, (int)(s.U * 0.016f)), Warm);
        _core = Sprite.Glow(Math.Max(2, (int)(s.U * 0.006f)), Warm);
        Store(0, BuildTurkey());
        Store(1, BuildCornucopia());
        Store(2, BuildMaple());
        Store(3, BuildPie());
        Store(4, BuildAcorn());
    }

    // ---- A tiny builder: add points, join them with lines. ----

    private sealed class Pic
    {
        public readonly List<(float X, float Y, bool Star)> P = [];
        public readonly List<(int A, int B)> E = [];
        public int Add(float x, float y, bool star = true) { P.Add((x, y, star)); return P.Count - 1; }
        public void Join(int a, int b) => E.Add((a, b));
        /// <summary>Lines a to b, b to c, ... and back to the first when closed.</summary>
        public void Chain(int[] ids, bool closed)
        {
            for (int i = 0; i + 1 < ids.Length; i++) Join(ids[i], ids[i + 1]);
            if (closed && ids.Length > 2) Join(ids[^1], ids[0]);
        }
        /// <summary>A ring of "n" points round (cx, cy), joined up. Returns their numbers.</summary>
        public int[] Ring(float cx, float cy, float r, int n, bool star = true)
        {
            var ids = new int[n];
            for (int i = 0; i < n; i++)
            {
                float a = MathF.Tau * i / n;
                ids[i] = Add(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, star);
            }
            Chain(ids, true);
            return ids;
        }
    }

    /// <summary>Centre the picture and scale it so its widest reach is 1, then keep it.</summary>
    private void Store(int slot, Pic pic)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var q in pic.P)
        {
            minX = Math.Min(minX, q.X); maxX = Math.Max(maxX, q.X);
            minY = Math.Min(minY, q.Y); maxY = Math.Max(maxY, q.Y);
        }
        float cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        float reach = Math.Max(0.001f, Math.Max(maxX - minX, maxY - minY) / 2);
        var list = new List<(float, float, bool)>(pic.P.Count);
        foreach (var q in pic.P) list.Add(((q.X - cx) / reach, (q.Y - cy) / reach, q.Star));
        _pts[slot] = list;
        _edges[slot] = pic.E;
    }

    // ---- The five pictures (x to the right, y DOWN, roughly -1 to +1) ----

    /// <summary>A turkey facing right: a zigzag fan of five feathers rising behind, a round body, a neck, head and beak, two legs.</summary>
    private static Pic BuildTurkey()
    {
        var p = new Pic();
        // The body is an oval centred at (0, 0.5). The fan goes from its left side up and round to its top.
        int bl = p.Add(-0.5f, 0.5f, true);
        int bt = p.Add(0.26f, 0.12f, true);
        var fan = new List<int> { bl };
        float fx = -0.12f, fy = 0.3f;
        float[] tipAng = [-196f, -164f, -132f, -100f, -68f];
        for (int i = 0; i < tipAng.Length; i++)
        {
            if (i > 0)
            {
                float mid = (tipAng[i - 1] + tipAng[i]) / 2 * MathF.PI / 180f;
                fan.Add(p.Add(fx + MathF.Cos(mid) * 0.64f, fy + MathF.Sin(mid) * 0.64f, false));
            }
            float a = tipAng[i] * MathF.PI / 180f;
            fan.Add(p.Add(fx + MathF.Cos(a) * 1.0f, fy + MathF.Sin(a) * 1.0f, true));
        }
        fan.Add(bt);
        p.Chain(fan.ToArray(), false);
        // The body: the lower right of the oval, from the fan's top foot round the bottom to its left foot.
        int prev = bt;
        for (int i = 1; i <= 6; i++)
        {
            float a = -1.0f + (MathF.PI + 1.0f + 0.35f) * i / 7f;         // from up-right, clockwise round the bottom
            int id = p.Add(MathF.Cos(a) * 0.52f, 0.5f + MathF.Sin(a) * 0.46f, true);
            p.Join(prev, id); prev = id;
        }
        p.Join(prev, bl);
        // Neck and head (a star), beak (a star) and a wattle.
        int neckLow = p.Add(0.46f, 0.44f, false);
        int head = p.Add(0.74f, 0.1f, true);
        int beak = p.Add(0.96f, 0.18f, true);
        int wattle = p.Add(0.80f, 0.3f, true);
        p.Join(bt, head); p.Join(neckLow, head); p.Join(head, beak); p.Join(beak, wattle);
        // Two legs.
        foreach (float lx in new[] { -0.14f, 0.16f })
        {
            int top = p.Add(lx, 0.94f, false), foot = p.Add(lx + 0.05f, 1.22f, true);
            p.Join(top, foot);
        }
        return p;
    }
    /// <summary>A cornucopia: a curved horn narrowing to a tip at the upper left, opening at the right, with fruit spilling out.</summary>
    private static Pic BuildCornucopia()
    {
        var p = new Pic();
        // The centre line of the horn is a gentle bend (a quadratic curve) from tip to mouth.
        PointF a = new(-0.95f, -0.6f), b = new(-0.6f, 0.5f), c = new(0.35f, 0.4f);
        const int steps = 5;
        var upper = new int[steps + 1];
        var lower = new int[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float k = i / (float)steps, m = 1 - k;
            float x = m * m * a.X + 2 * m * k * b.X + k * k * c.X;
            float y = m * m * a.Y + 2 * m * k * b.Y + k * k * c.Y;
            float dx = 2 * m * (b.X - a.X) + 2 * k * (c.X - b.X), dy = 2 * m * (b.Y - a.Y) + 2 * k * (c.Y - b.Y);
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float nx = -dy / len, ny = dx / len;                          // sideways to the horn's direction
            float w = 0.03f + 0.36f * k;                                  // the horn widens toward its mouth
            upper[i] = p.Add(x + nx * w, y + ny * w, true);
            lower[i] = p.Add(x - nx * w, y - ny * w, true);
        }
        p.Chain(upper, false);
        p.Chain(lower, false);
        p.Join(upper[steps], lower[steps]);                               // the mouth
        p.Join(upper[0], lower[0]);                                       // the tip
        // Fruit spilling from the mouth: three round things.
        p.Ring(0.62f, 0.12f, 0.22f, 8);
        p.Ring(0.74f, 0.6f, 0.19f, 8);
        p.Ring(0.3f, 0.9f, 0.16f, 8);
        return p;
    }

    /// <summary>A maple leaf: five pointed lobes (stars at the tips) and a stem, with veins.</summary>
    private static Pic BuildMaple()
    {
        var p = new Pic();
        // The right half, top to bottom; the left half is its mirror image.
        (float X, float Y, bool Star)[] half =
        [
            (0f, -1f, true), (0.17f, -0.62f, false), (0.46f, -0.74f, true), (0.38f, -0.3f, false), (0.88f, -0.4f, true),
            (0.7f, 0f, false), (0.98f, 0.16f, true), (0.46f, 0.32f, false), (0.52f, 0.58f, true), (0.1f, 0.44f, false),
        ];
        var ids = new List<int>();
        foreach (var q in half) ids.Add(p.Add(q.X, q.Y, q.Star));
        // The stem: down the right of the stem, the foot (a star), up the left of the stem.
        ids.Add(p.Add(0.07f, 0.72f, false));
        ids.Add(p.Add(0f, 1.05f, true));
        ids.Add(p.Add(-0.07f, 0.72f, false));
        // Mirror back up the left side (skip the top, which is on the middle line).
        int[] left = new int[half.Length];
        for (int i = half.Length - 1; i >= 1; i--) { left[i] = p.Add(-half[i].X, half[i].Y, half[i].Star); ids.Add(left[i]); }
        // The outline: down the right, the stem, and up the left to the top, then closed.
        p.Chain(ids.ToArray(), true);        // Veins: from the leaf's base up to the tips of the three big lobes and the two side lobes.
        int hub = p.Add(0f, 0.46f, false);
        p.Join(hub, ids[0]);
        p.Join(hub, ids[4]); p.Join(hub, ids[6]);
        p.Join(hub, left[4]); p.Join(hub, left[6]);
        return p;
    }

    /// <summary>A slice of pie seen from the side: a wedge with a rounded crust, and a dollop of cream on top.</summary>
    private static Pic BuildPie()
    {
        var p = new Pic();
        int tip = p.Add(-0.9f, 0.45f, true);
        int top = p.Add(0.7f, -0.45f, true);
        int c1 = p.Add(0.88f, -0.2f, false), c2 = p.Add(0.96f, 0.1f, true), c3 = p.Add(0.88f, 0.4f, false);
        int bottom = p.Add(0.7f, 0.65f, true);
        p.Chain([tip, top, c1, c2, c3, bottom], true);
        p.Join(top, bottom);                                              // where the crust meets the filling
        // The cream: three tiers up to a curl, sitting on the wedge's top edge.
        int b1 = p.Add(-0.3f, -0.12f, true), b2 = p.Add(0.3f, -0.2f, true);
        int m1 = p.Add(-0.2f, -0.42f, true), m2 = p.Add(0.2f, -0.46f, true);
        int peak = p.Add(0.02f, -0.8f, true);
        p.Chain([b1, b2, m2, peak, m1], true);
        return p;
    }

    /// <summary>An acorn: a domed cap with a stem and cross-hatching, and a pointed nut below.</summary>
    private static Pic BuildAcorn()
    {
        var p = new Pic();
        int capL = p.Add(-0.62f, -0.1f, true), capR = p.Add(0.62f, -0.1f, true);
        var dome = new List<int> { capR };
        (float X, float Y)[] arc = [(0.56f, -0.42f), (0.3f, -0.68f), (0f, -0.76f), (-0.3f, -0.68f), (-0.56f, -0.42f)];
        foreach (var q in arc) dome.Add(p.Add(q.X, q.Y, true));
        dome.Add(capL);
        p.Chain(dome.ToArray(), false);
        p.Join(capL, capR);                                               // the cap's lower rim
        int stem = p.Add(0.05f, -1.05f, true);
        p.Join(dome[3], stem);
        // Cross-hatching on the cap: two slanted lines.
        int h1 = p.Add(-0.3f, -0.1f, false), h2 = p.Add(-0.12f, -0.7f, false);
        int h3 = p.Add(0.3f, -0.1f, false), h4 = p.Add(0.12f, -0.7f, false);
        p.Join(h1, h2); p.Join(h3, h4);
        // The nut: from the cap's right end round the bottom to the left end.
        int prev = capR;
        (float X, float Y)[] nut = [(0.52f, 0.3f), (0.3f, 0.66f), (0f, 1f), (-0.3f, 0.66f), (-0.52f, 0.3f)];
        foreach (var q in nut) { int id = p.Add(q.X, q.Y, true); p.Join(prev, id); prev = id; }
        p.Join(prev, capL);
        return p;
    }

    // ---- One showing ----

    public override void Begin(Random rng)
    {
        _pic = rng.Next(Pics);
        _half = Math.Max(8f, _s.U * 0.10f);
        // The upper middle of the sky: well left of the moon (at 0.74 w), and high enough that the
        // bottom of the picture stays far above the hills.
        float cx = _s.Width * (0.30f + 0.26f * (float)rng.NextDouble());
        float cy = _s.Height * (0.14f + 0.12f * (float)rng.NextDouble());
        _center = new PointF(cx, Math.Max(cy, _half * 1.25f));

        var pts = _pts[_pic];
        _phase = new float[pts.Count];
        for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)rng.NextDouble() * MathF.Tau;

        // The timetable. Stars light over about 2.5 s, then each line grows in a fraction of a
        // second (quicker for a picture with many lines), then 3 s holding, then 1.2 s of fade.
        int stars = 0;
        foreach (var q in pts) if (q.Star) stars++;
        _starGap = 2.5f / Math.Max(1, stars);
        _lineStart = 0.4f + 2.5f + 0.3f;
        _lineTime = Math.Clamp(4.2f / _edges[_pic].Count, 0.1f, 0.22f);
        _seconds = _lineStart + _edges[_pic].Count * _lineTime + 3f + 1.2f;
    }

    private PointF At(int i)
    {
        var q = _pts[_pic][i];
        return new PointF(_center.X + q.X * _half, _center.Y + q.Y * _half);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float fadeOut = Math.Min(1f, Smooth((_seconds - t) / 1.2f));      // everything fades over the last 1.2 s
        if (fadeOut <= 0) return;
        var pts = _pts[_pic];
        var edges = _edges[_pic];

        // The lines first, so the stars sit on top of the joins.
        float thick = Math.Max(1f, _s.U * 0.002f);
        for (int i = 0; i < edges.Count; i++)
        {
            float k = (t - _lineStart - i * _lineTime) / _lineTime;       // 0 to 1 while this line is growing
            if (k <= 0) break;                                           // the later lines have not started either
            k = Math.Min(1f, k);
            PointF a = At(edges[i].A), b = At(edges[i].B);
            fb.Line(a, new PointF(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k), Warm, 0.6f * fadeOut, thick, _s.OpenSky);
        }

        // Then the stars. Number n lights at 0.4 + n * gap seconds: a quick pop (a big halo that
        // settles) and then a steady shine with a gentle twinkle.
        int n = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if (!pts[i].Star) continue;
            float age = t - (0.4f + n++ * _starGap);
            if (age < 0) continue;
            PointF p = At(i);
            float pop = MathF.Exp(-age * 5f);                             // 1 at the instant it lights, then settles
            float twinkle = 0.8f + 0.2f * MathF.Sin(t * 3.2f + _phase[i]);
            _core.DrawCentered(fb, p.X, p.Y, Math.Min(1f, Smooth(age / 0.12f)) * twinkle * fadeOut, _s.OpenSky);
            _halo.DrawCentered(fb, p.X, p.Y, (0.30f * twinkle + 0.7f * pop) * fadeOut, _s.OpenSky);
        }
    }
}
