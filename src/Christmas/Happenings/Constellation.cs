using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// Join-the-dots in the sky. In the dark upper right, a handful of stars
/// brighten one after another (each pops bright and settles), then thin pale
/// lines draw themselves from star to star, one at a time, until the dots
/// have made a picture: a snowflake, a five-pointed star, a Christmas tree,
/// a candy cane or a bell. The finished picture holds for a few seconds with
/// its stars twinkling, and then everything fades out.
///
/// Each picture is a list of points and a list of lines between them, written
/// in a little made-up grid where the picture fits between -1 and +1. A point
/// can be a star (it lights up) or just a corner the lines pass through (the
/// notch in a tree's skirt, the end of a candy stripe). Begin picks a picture
/// and where it sits; Draw only works from t:
///   stars light up one by one  -> lines grow one by one -> hold -> fade.
///
/// Everything goes through the OpenSky stencil so a picture that sags into
/// the mountains or the corner branches is hidden behind them.
/// </summary>
internal sealed class Constellation : Happening
{
    private static readonly Color Pale = Color.FromArgb(232, 240, 255);

    private readonly ChristmasScenery _s;
    private readonly Sprite _halo, _core;
    private readonly List<(float X, float Y, bool Star)>[] _pts = new List<(float, float, bool)>[5];
    private readonly List<(int A, int B)>[] _edges = new List<(int, int)>[5];

    private int _pic;
    private PointF _center;
    private float _half;                                         // half the picture's size, in pixels
    private float[] _phase = [];                                 // each star's twinkle phase
    private float _starGap, _lineStart, _lineTime, _seconds;     // the timetable, worked out in Begin

    public override float Seconds => _seconds;
    public override string? Claims => "skyshow";                 // shares the upper-right sky with the fireworks

    public Constellation(ChristmasScenery s)
    {
        _s = s;
        _seconds = 11f;
        _halo = Sprite.Glow(Math.Max(3, (int)(s.U * 0.016f)), Pale);
        _core = Sprite.Glow(Math.Max(2, (int)(s.U * 0.006f)), Pale);
        BuildSnowflake(); BuildStar(); BuildTree(); BuildCane(); BuildBell();
    }

    // ---- The five pictures. Each fills slot 0 to 4 of _pts and _edges. ----

    /// <summary>Six arms, each with a star in the middle and one at the tip, and feathery barbs.</summary>
    private void BuildSnowflake()
    {
        var p = new List<(float, float, bool)> { (0, 0, true) };
        var e = new List<(int, int)>();
        for (int i = 0; i < 6; i++)
        {
            float a = (-90 + 60 * i) * MathF.PI / 180f;
            float dx = MathF.Cos(a), dy = MathF.Sin(a);
            int mid = p.Count; p.Add((dx * 0.5f, dy * 0.5f, true));
            int tip = p.Count; p.Add((dx, dy, true));
            e.Add((0, mid)); e.Add((mid, tip));
            // Two pairs of barbs, angled 60 degrees off the arm: a big pair at the middle star, a small pair near the tip.
            foreach (var (along, len) in new[] { (0.5f, 0.3f), (0.78f, 0.2f) })
            {
                int from = along == 0.5f ? mid : p.Count;
                if (along != 0.5f) p.Add((dx * along, dy * along, false));
                foreach (float turn in new[] { -60f, 60f })
                {
                    float b = a + turn * MathF.PI / 180f;
                    int end = p.Count; p.Add((dx * along + MathF.Cos(b) * len, dy * along + MathF.Sin(b) * len, false));
                    e.Add((from, end));
                }
            }
        }
        _pts[0] = p; _edges[0] = e;
    }

    /// <summary>The classic five-pointed star outline: ten corners, outer and inner in turn.</summary>
    private void BuildStar()
    {
        var p = new List<(float, float, bool)>();
        var e = new List<(int, int)>();
        for (int i = 0; i < 10; i++)
        {
            float a = (-90 + 36 * i) * MathF.PI / 180f;
            float r = i % 2 == 0 ? 1f : 0.42f;
            p.Add((MathF.Cos(a) * r, MathF.Sin(a) * r + 0.05f, true));
            e.Add((i, (i + 1) % 10));
        }
        _pts[1] = p; _edges[1] = e;
    }

    /// <summary>A three-tier tree with a short trunk, drawn as one outline.</summary>
    private void BuildTree()
    {
        // The notches (false) are corners the outline turns through; the rest are stars.
        float[,] v =
        {
            { 0, -1, 1 }, { .32f, -.4f, 1 }, { .14f, -.4f, 0 }, { .54f, .12f, 1 }, { .2f, .12f, 0 }, { .78f, .58f, 1 },
            { .14f, .58f, 1 }, { .14f, .95f, 1 }, { -.14f, .95f, 1 }, { -.14f, .58f, 1 }, { -.78f, .58f, 1 },
            { -.2f, .12f, 0 }, { -.54f, .12f, 1 }, { -.14f, -.4f, 0 }, { -.32f, -.4f, 1 },
        };
        var p = new List<(float, float, bool)>();
        var e = new List<(int, int)>();
        for (int i = 0; i < v.GetLength(0); i++) { p.Add((v[i, 0], v[i, 1], v[i, 2] > 0)); e.Add((i, (i + 1) % v.GetLength(0))); }
        _pts[2] = p; _edges[2] = e;
    }

    /// <summary>A candy cane drawn as a fat outline (a straight shaft and a round hook), with three stripes.</summary>
    private void BuildCane()
    {
        var p = new List<(float, float, bool)>();
        var e = new List<(int, int)>();
        // Outer edge: up the right side of the shaft, over the hook, down to the hook's tip.
        p.Add((.4f, .95f, true));
        for (int i = 0; i <= 4; i++)
        {
            float a = -i * MathF.PI / 4f;
            p.Add((MathF.Cos(a) * .4f, -.35f + MathF.Sin(a) * .4f, true));
        }
        p.Add((-.4f, -.1f, true));
        // Inner edge: back from the hook's tip, round the inside of the hook, and down the left of the shaft.
        p.Add((-.2f, -.1f, true));
        for (int i = 4; i >= 0; i--)
        {
            float a = -i * MathF.PI / 4f;
            p.Add((MathF.Cos(a) * .2f, -.35f + MathF.Sin(a) * .2f, true));
        }
        p.Add((.2f, .95f, true));
        for (int i = 0; i < p.Count; i++) e.Add((i, (i + 1) % p.Count));
        // Stripes: slanted lines across the shaft, from its left edge to its right edge.
        foreach (float y in new[] { .05f, .4f, .75f })
        {
            int a = p.Count; p.Add((.2f, y, false));
            int b = p.Count; p.Add((.4f, y - .16f, false));
            e.Add((a, b));
        }
        // Shift so the whole picture sits in the middle of its box.
        for (int i = 0; i < p.Count; i++) p[i] = (p[i].Item1 + 0.1f, p[i].Item2, p[i].Item3);
        _pts[3] = p; _edges[3] = e;
    }

    /// <summary>A bell: a round crown, a dome that flares to a wide lip, and a clapper hanging below.</summary>
    private void BuildBell()
    {
        float[,] right = { { 0, -.9f }, { .25f, -.7f }, { .4f, -.3f }, { .5f, .2f }, { .74f, .55f } };
        var p = new List<(float, float, bool)>();
        var e = new List<(int, int)>();
        for (int i = 0; i < 5; i++) p.Add((right[i, 0], right[i, 1], true));
        for (int i = 4; i >= 1; i--) p.Add((-right[i, 0], right[i, 1], true));
        for (int i = 0; i < p.Count; i++) e.Add((i, (i + 1) % p.Count));
        // The clapper: a small diamond just under the lip.
        int c = p.Count;
        p.Add((0, .66f, true)); p.Add((.1f, .78f, true)); p.Add((0, .9f, true)); p.Add((-.1f, .78f, true));
        for (int i = 0; i < 4; i++) e.Add((c + i, c + (i + 1) % 4));
        _pts[4] = p; _edges[4] = e;
    }

    // ---- One showing ----

    public override void Begin(Random rng)
    {
        _pic = rng.Next(5);
        // Upper right, away from the moon. The picture is 0.2 U across, and kept clear of the very top.
        float cx = _s.Width * (0.50f + 0.26f * (float)rng.NextDouble());
        float cy = _s.Height * (0.08f + 0.22f * (float)rng.NextDouble());
        _half = _s.U * 0.10f;
        _center = new PointF(cx, Math.Max(cy, _half * 1.15f));

        var pts = _pts[_pic];
        _phase = new float[pts.Count];
        for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)rng.NextDouble() * 6.28f;

        // The timetable. Stars light over about 2.5 s, then each line grows in about 0.22 s
        // (a little quicker for a picture with many lines), then 3 s holding, then 1.2 s of fade.
        int stars = pts.Count(q => q.Star);
        _starGap = 2.5f / Math.Max(1, stars);
        _lineStart = 0.4f + 2.5f + 0.3f;
        _lineTime = Math.Clamp(4.2f / _edges[_pic].Count, 0.12f, 0.22f);
        _seconds = _lineStart + _edges[_pic].Count * _lineTime + 3f + 1.2f;
    }

    private PointF At(int i)
    {
        var q = _pts[_pic][i];
        return new PointF(_center.X + q.X * _half, _center.Y + q.Y * _half);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float out_ = Math.Min(1f, Smooth((_seconds - t) / 1.2f));         // everything fades over the last 1.2 s
        if (out_ <= 0) return;
        var pts = _pts[_pic];

        // The lines first, so the stars sit on top of the joins.
        float thick = Math.Max(1f, _s.U * 0.002f);
        for (int i = 0; i < _edges[_pic].Count; i++)
        {
            float k = (t - _lineStart - i * _lineTime) / _lineTime;       // 0 to 1 while this line is growing
            if (k <= 0) break;                                           // the later lines have not started either
            k = Math.Min(1f, k);
            PointF a = At(_edges[_pic][i].A), b = At(_edges[_pic][i].B);
            fb.Line(a, new PointF(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k), Pale, 0.6f * out_, thick, _s.OpenSky);
        }

        // Then the stars. Number n lights at 0.4 + n * gap seconds: a quick pop (big halo that
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
            _core.DrawCentered(fb, p.X, p.Y, Math.Min(1f, Smooth(age / 0.12f)) * twinkle * out_, _s.OpenSky);
            _halo.DrawCentered(fb, p.X, p.Y, (0.30f * twinkle + 0.7f * pop) * out_, _s.OpenSky);
        }
    }
}
