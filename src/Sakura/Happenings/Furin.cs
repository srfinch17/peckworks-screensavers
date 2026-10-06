using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A furin, the little glass wind chime Japanese houses hang out in summer.
/// This one hangs from a blossom branch on a fine string. It fades in,
/// sways in the breeze, and every few seconds the clapper touches the glass:
/// a tiny sparkle pings off the bell and rings of light spread out from it,
/// the way a ring spreads on a pond. Then it fades away.
///
/// FEYNMAN VERSION: three parts, like a mobile.
///   the string  a thin line from the branch to the top of the bell (drawn
///               fresh each frame, because it tilts as the bell swings);
///   the bell    a flip-book: 2 designs (goldfish or waves) x 9 tilts x 3
///               clapper positions. The bell is a pendulum: it swings about
///               the top of the string, so each frame we work out its angle,
///               pick the page tilted nearest to that angle, and stamp it
///               with its top at the swinging end of the string;
///   the strip   the tanzaku, the paper slip that catches the wind. It is
///               the same pendulum idea, but it follows the bell a third of
///               a second LATE and swings further, which is what makes it
///               look like it is flapping behind in the breeze.
/// The sparkle is not stored at all: it is worked out from how long ago the
/// bell rang (t minus the ring time): a star that flashes and fades, and
/// three circles that grow outward one after another.
///
/// The chime is in FRONT of the branches (Layer 1) and says it claims the
/// branches so it does not share them with the birds or the squirrel.
/// </summary>
internal sealed class Furin : Happening
{
    private const float Total = 12f;
    private const float MaxBell = 14f, BellStep = 3.5f;   // bell tilt pages: -14 to +14 degrees in 3.5 steps (9 pages)
    private const float MaxStrip = 24f, StripStep = 6f;   // strip tilt pages: -24 to +24 in 6 steps (9 pages)

    private static readonly Color[] PaperA = [Color.FromArgb(176, 214, 240), Color.FromArgb(248, 190, 208)];   // pale blue, pale pink
    private static readonly Color[] PaperB = [Color.FromArgb(126, 176, 222), Color.FromArgb(232, 130, 164)];   // the darker marks on it

    private readonly Scenery _s;
    private readonly int _bw, _bh, _sl, _sw;            // bell width and height, strip length and width, in pixels
    private readonly int _bcx, _bpy, _scx, _spy;        // the pivot (where the string meets it) inside each sprite
    private readonly Sprite[,,] _bell = new Sprite[2, 9, 3];   // [design, tilt, clapper]
    private readonly Sprite[,] _strip = new Sprite[2, 9];      // [paper color, tilt]
    private readonly Sprite _glow;
    private readonly List<PointF> _good = [], _any = [];       // places it could hang

    private bool _ok;
    private PointF _anchor;
    private int _design, _paper;
    private float _phase, _stringLen;
    private float[] _rings = [3.2f, 6.3f, 9.1f];

    public override float Seconds => _ok ? Total : 0.1f;
    public override string? Claims => "branches";
    public override int Layer => 1;
    public override bool CanBegin => _any.Count > 0;

    public Furin(Scenery s)
    {
        _s = s;
        _bw = Math.Max(9, (int)(s.U * 0.03f));
        _bh = (int)(_bw * 0.95f);
        _sl = Math.Max(12, (int)(s.U * 0.05f));
        _sw = Math.Max(4, (int)(_bw * 0.42f));
        _bcx = (int)(_bw * 1.0f) + 2; _bpy = 3;
        _scx = _sl / 2 + 3; _spy = 3;
        _glow = Sprite.Glow(Math.Max(4, (int)(s.U * 0.022f)), Color.FromArgb(255, 250, 235));

        for (int d = 0; d < 2; d++)
            for (int a = 0; a < 9; a++)
                for (int c = 0; c < 3; c++)
                    _bell[d, a, c] = PaintBell(d, (a - 4) * BellStep, c - 1);
        for (int p = 0; p < 2; p++)
            for (int a = 0; a < 9; a++)
                _strip[p, a] = PaintStrip(p, (a - 4) * StripStep);

        // Hanging places: a cluster in the top 45%, at least 0.12 U from the sides, with wood or blossom at the spot.
        foreach (var p in s.BranchSpots)
        {
            if (p.Y > s.Height * 0.45f || p.Y < s.Height * 0.03f) continue;
            if (p.X < 0.12f * s.U || p.X > s.Width - 0.12f * s.U) continue;
            if (!Twig(p.X, p.Y)) continue;
            _any.Add(p);
            // "Good" = bare sky under it, so the clear glass shows up (over a pink blossom wall it would be lost).
            int sky = 0, n = 0;
            for (float dy = 0.05f; dy <= 0.12f; dy += 0.01f)
                for (float dx = -0.03f; dx <= 0.03f; dx += 0.01f)
                {
                    int ix = (int)(p.X + dx * s.U), iy = (int)(p.Y + dy * s.U);
                    if (ix < 0 || iy < 0 || ix >= s.Width || iy >= s.Height) continue;
                    n++; if (s.OpenSky[iy * s.Width + ix]) sky++;
                }
            if (n > 0 && sky >= n * 0.6f) _good.Add(p);
        }
    }

    private bool Twig(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _s.Width || iy >= _s.Height) return false;
        uint p = _s.Pixels[iy * _s.Width + ix];
        return (int)((p >> 16) & 0xFF) >= (int)(p & 0xFF) - 2;
    }

    public override void Begin(Random rng)
    {
        _ok = false;
        var pool = _good.Count > 0 ? _good : _any;
        if (pool.Count == 0) return;
        _anchor = pool[rng.Next(pool.Count)];
        _design = rng.Next(2);
        _paper = rng.Next(2);
        _phase = (float)(rng.NextDouble() * MathF.Tau);
        _stringLen = (0.04f + 0.02f * (float)rng.NextDouble()) * _s.U;
        _rings = [3.0f + (float)rng.NextDouble() * 0.6f, 6.0f + (float)rng.NextDouble() * 0.6f, 8.8f + (float)rng.NextDouble() * 0.6f];
        _ok = true;
    }

    // ================================================================ drawing

    /// <summary>The bell's swing angle in degrees at time t (positive = swinging to the right): two slow waves of different length added together, so it never repeats exactly.</summary>
    private float Swing(float t)
    {
        float env = Smooth(t / 1.5f);
        double a = 0.65 * Math.Sin(Math.Tau * t / 2.9 + _phase) + 0.35 * Math.Sin(Math.Tau * t / 1.9 + 1.3 + _phase);
        return (float)(a * 9.0) * env;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float u = _s.U;
        float fade = Fade(t, Total, 1.2f, 1.2f);
        if (fade < 0.01f) return;

        float th = Swing(t);
        float r = th * MathF.PI / 180f;
        PointF down = new(MathF.Sin(r), MathF.Cos(r)), right = new(MathF.Cos(r), -MathF.Sin(r));
        PointF top = new(_anchor.X + down.X * _stringLen, _anchor.Y + down.Y * _stringLen);

        // The string.
        fb.Line(_anchor, top, Color.FromArgb(240, 240, 246), 0.85f * fade, Math.Max(1f, u * 0.0012f));

        // Which way the clapper hangs: it swings against the bell, and when the bell rings it strikes the right side of the glass.
        int clap = 1;                                     // 0 left, 1 middle, 2 right
        float sinceRing = 99;
        foreach (float tr in _rings) if (t >= tr && t - tr < sinceRing) sinceRing = t - tr;
        float vel = Swing(t + 0.05f) - Swing(t - 0.05f);
        clap = vel > 1.2f ? 0 : vel < -1.2f ? 2 : 1;
        if (sinceRing < 0.1f) clap = 2;
        else if (sinceRing < 0.25f) clap = 0;

        int ba = Math.Clamp((int)MathF.Round(th / BellStep) + 4, 0, 8);
        Stamp(fb, _bell[_design, ba, clap], top, _bcx, _bpy, fade);

        // The strip hangs from the bottom of the bell and lags behind it.
        PointF sp = new(top.X + down.X * _bh * 0.93f, top.Y + down.Y * _bh * 0.93f);
        float lagged = Swing(t - 0.35f);
        float psi = Math.Clamp(1.7f * lagged + 4f * MathF.Sin(MathF.Tau * 1.3f * t + _phase) * Smooth(t / 1.5f), -MaxStrip, MaxStrip);
        int sa = Math.Clamp((int)MathF.Round(psi / StripStep) + 4, 0, 8);
        Stamp(fb, _strip[_paper, sa], sp, _scx, _spy, fade);

        // The ring: star glint + three circles, all worked out from how long ago it rang.
        foreach (float tr in _rings)
        {
            float dt = t - tr;
            if (dt < 0 || dt > 1.0f) continue;
            // The point on the glass the clapper strikes: right side, lower down.
            float gx = top.X + right.X * 0.42f * _bw + down.X * 0.62f * _bh;
            float gy = top.Y + right.Y * 0.42f * _bw + down.Y * 0.62f * _bh;
            float env = Smooth(dt / 0.05f) * (1 - Smooth((dt - 0.05f) / 0.45f));
            if (env > 0.01f)
            {
                _glow.DrawCentered(fb, gx, gy, 0.8f * env * fade);
                float ls = Math.Max(4f, u * 0.026f) * (0.5f + 0.5f * env);
                float th2 = Math.Max(1f, u * 0.0015f);
                var white = Color.FromArgb(255, 255, 250);
                fb.Line(new PointF(gx - ls, gy), new PointF(gx + ls, gy), white, env * fade, th2);
                fb.Line(new PointF(gx, gy - ls), new PointF(gx, gy + ls), white, env * fade, th2);
                float ld = ls * 0.55f;
                fb.Line(new PointF(gx - ld, gy - ld), new PointF(gx + ld, gy + ld), white, env * fade * 0.8f, th2);
                fb.Line(new PointF(gx - ld, gy + ld), new PointF(gx + ld, gy - ld), white, env * fade * 0.8f, th2);
            }
            for (int j = 0; j < 3; j++)
            {
                float dj = dt - j * 0.13f;
                if (dj < 0 || dj > 0.75f) continue;
                float q = dj / 0.75f;
                float rad = Math.Max(3f, u * (0.008f + 0.05f * (1 - (1 - q) * (1 - q))));
                float alpha = 0.75f * (1 - q) * (1 - q) * fade;
                const int segs = 28;
                PointF prev = new(gx + rad, gy);
                for (int i = 1; i <= segs; i++)
                {
                    float a = MathF.Tau * i / segs;
                    PointF cur = new(gx + MathF.Cos(a) * rad, gy + MathF.Sin(a) * rad);
                    fb.Line(prev, cur, Color.FromArgb(235, 248, 255), alpha, Math.Max(1f, u * 0.0014f));
                    prev = cur;
                }
            }
        }
    }

    /// <summary>Stamps a pivoted sprite so its pivot (px, py inside the sprite) lands on p. Rounded once.</summary>
    private static void Stamp(FrameBuffer fb, Sprite sp, PointF p, int px, int py, float opacity) =>
        sp.Draw(fb, (int)MathF.Round(p.X) - px, (int)MathF.Round(p.Y) - py, opacity);

    // ================================================================ painting

    private Sprite PaintBell(int design, float angleDeg, int clapper)
    {
        float r = _bw / 2f, h = _bh;
        return Sprite.Paint(_bcx * 2, (int)(h * 1.3f) + _bpy + 4, g =>
        {
            g.TranslateTransform(_bcx, _bpy);
            g.RotateTransform(-angleDeg);

            // The dome: up from the left rim, over the top, down to the right rim, closed with a gentle curve.
            using var shape = new GraphicsPath();
            shape.AddBezier(-r, h * 0.86f, -r * 1.02f, h * 0.30f, -r * 0.5f, h * 0.06f, 0, h * 0.06f);
            shape.AddBezier(0, h * 0.06f, r * 0.5f, h * 0.06f, r * 1.02f, h * 0.30f, r, h * 0.86f);
            shape.AddBezier(r, h * 0.86f, r * 0.5f, h * 0.99f, -r * 0.5f, h * 0.99f, -r, h * 0.86f);
            shape.CloseFigure();

            // Behind the glass: the clapper (a bead on a short stem) shows through, then the glass is laid over it.
            float cxp = clapper * r * 0.34f;
            using (var pen = new Pen(Color.FromArgb(190, 150, 140, 130), Math.Max(1f, _bw * 0.04f)))
                g.DrawLine(pen, 0, h * 0.1f, cxp * 0.8f, h * 0.7f);
            using (var bead = new SolidBrush(Color.FromArgb(235, 210, 176, 112)))
            {
                float br = Math.Max(1.5f, _bw * 0.075f);
                g.FillEllipse(bead, cxp - br, h * 0.74f - br, br * 2, br * 2);
            }
            using (var thread = new Pen(Color.FromArgb(200, 235, 235, 240), 1f))
                g.DrawLine(thread, cxp, h * 0.76f, 0, h * 0.97f);

            // Glass: mostly clear with a faint cool tint, lighter toward the upper left.
            using (var fill = new SolidBrush(Color.FromArgb(46, 196, 232, 248))) g.FillPath(fill, shape);
            using (var lit = new LinearGradientBrush(new PointF(-r, 0), new PointF(r, h), Color.FromArgb(80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)))
                g.FillPath(lit, shape);

            // The painted picture on the glass.
            if (design == 0) Goldfish(g, r, h);
            else Waves(g, r, h);

            // Pale edge, and the lip at the bottom.
            using (var edge = new Pen(Color.FromArgb(215, 232, 246, 255), Math.Max(1f, _bw * 0.055f))) g.DrawPath(edge, shape);
            using (var lip = new Pen(Color.FromArgb(170, 240, 250, 255), Math.Max(1f, _bw * 0.04f)))
                g.DrawEllipse(lip, -r * 0.98f, h * 0.80f, r * 1.96f, h * 0.12f);

            // Glassy highlight: a bright curved streak on the upper left, and a dot.
            using (var hl = new Pen(Color.FromArgb(235, 255, 255, 255), Math.Max(1.2f, _bw * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(hl, -r * 0.8f, h * 0.18f, r * 1.2f, h * 0.7f, 195, 55);
            using (var dot = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            {
                float dr = Math.Max(1f, _bw * 0.05f);
                g.FillEllipse(dot, -r * 0.55f - dr, h * 0.62f - dr, dr * 2, dr * 2);
            }

            // The cap and the loop the string ties to.
            using (var cap = new SolidBrush(Color.FromArgb(168, 120, 84)))
                g.FillEllipse(cap, -r * 0.2f, -1f, r * 0.4f, h * 0.14f);
            using (var loop = new Pen(Color.FromArgb(220, 220, 214), 1f))
                g.DrawEllipse(loop, -r * 0.1f, -2.5f, r * 0.2f, 3f);
        });
    }

    private static void Goldfish(Graphics g, float r, float h)
    {
        // A little red kingyo: round body, fan tail, one dark eye.
        using var body = new SolidBrush(Color.FromArgb(215, 226, 62, 48));
        float cx = r * 0.05f, cy = h * 0.5f, bw = r * 0.62f, bh = h * 0.2f;
        g.FillEllipse(body, cx - bw / 2, cy - bh / 2, bw, bh);
        g.FillPolygon(body, [new PointF(cx - bw * 0.4f, cy), new PointF(cx - bw * 0.95f, cy - bh * 0.75f), new PointF(cx - bw * 0.85f, cy + bh * 0.75f)]);
        using var eye = new SolidBrush(Color.FromArgb(230, 40, 20, 20));
        float e = Math.Max(1f, r * 0.07f);
        g.FillEllipse(eye, cx + bw * 0.22f - e, cy - bh * 0.15f - e, e * 2, e * 2);
    }

    private static void Waves(Graphics g, float r, float h)
    {
        // Three rows of blue wave arcs (seigaiha, the classic Japanese wave pattern, in miniature).
        using var pen = new Pen(Color.FromArgb(200, 52, 104, 196), Math.Max(1f, r * 0.08f));
        for (int row = 0; row < 3; row++)
        {
            float y = h * (0.36f + row * 0.15f);
            float w = r * 0.5f;
            for (int i = -1; i <= 0; i++)
                g.DrawArc(pen, i * w + w * 0.2f - w / 2, y, w, w * 0.8f, 180, 180);
        }
    }

    private Sprite PaintStrip(int paper, float angleDeg)
    {
        float sl = _sl, sw = _sw;
        return Sprite.Paint(_scx * 2, _sl + _spy + 6 + (int)(sl * 0.2f), g =>
        {
            g.TranslateTransform(_scx, _spy);
            g.RotateTransform(-angleDeg);
            // A thin tie, then the paper: a long slip with a V cut in its bottom end, bowed a little like paper in the wind.
            using (var tie = new Pen(Color.FromArgb(230, 230, 230), 1f)) g.DrawLine(tie, 0, 0, 0, sl * 0.1f);
            float y0 = sl * 0.08f, y1 = sl * 0.96f, hw = sw / 2f;
            using var shape = new GraphicsPath();
            shape.AddLine(-hw, y0, hw, y0);
            shape.AddBezier(hw, y0, hw * 1.15f, sl * 0.4f, hw * 0.95f, sl * 0.7f, hw, y1);
            shape.AddLine(hw, y1, 0, y1 - sw * 0.5f);
            shape.AddLine(0, y1 - sw * 0.5f, -hw, y1);
            shape.AddBezier(-hw, y1, -hw * 0.95f, sl * 0.7f, -hw * 1.15f, sl * 0.4f, -hw, y0);
            shape.CloseFigure();
            using (var b = new SolidBrush(PaperA[paper])) g.FillPath(b, shape);
            using (var mark = new Pen(Color.FromArgb(200, PaperB[paper]), Math.Max(1f, sw * 0.14f)))
            {
                g.DrawLine(mark, 0, sl * 0.3f, 0, sl * 0.42f);
                g.DrawLine(mark, 0, sl * 0.5f, 0, sl * 0.62f);
            }
            using (var edge = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(edge, shape);
        });
    }
}
