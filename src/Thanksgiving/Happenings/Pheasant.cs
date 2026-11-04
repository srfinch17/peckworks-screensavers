using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A ring-necked pheasant bursts out of the corn shock. A few loose bits of
/// straw fly first (the rustle), then the cock bird shoots up out of the
/// stalks in the loud, fast whirr a pheasant is known for, climbs steeply,
/// flattens out, and goes off over the top of the screen or off one side,
/// gliding between short bursts of wingbeats the way a pheasant does.
///
/// FEYNMAN VERSION: the bird is a flip-book of 16 wing positions (one full
/// flap), plus one "wings held out, gliding" page. Drawing a bird at a slant
/// is harder than drawing it level: a stamp is a flat sticker and cannot
/// tilt. So this file has its own little "turn the sticker" routine: for
/// every pixel of the screen near the bird it asks "which pixel of the
/// sticker would land here if the sticker were turned?" and copies that
/// colour. That is slower than a plain stamp, so the bird's sticker is kept
/// small and the work is shared between the processor's cores.
///
/// The path is one smooth curve (a Bezier: three points, start, a "pull"
/// point and the end, and the bird follows the bend between them). The curve
/// starts by pulling STEEPLY up, so the first moments are a near vertical
/// burst, and the pull point is chosen so the bird levels off later. The bird
/// is turned to face along the curve at every moment.
///
/// It is drawn only where the corner branches did NOT paint
/// (OpenOfBranches), so it passes behind them but in front of everything
/// else. Because the corn shock is painted into the backdrop (it cannot
/// part to let a bird out), the bird starts a little see-through, as if
/// seen between the stalks, and the straw is drawn OVER it for the first
/// moments so the straw seems to be in front.
/// </summary>
internal sealed class Pheasant : Happening
{
    private const int FlapPages = 16;
    private const float Delay = 0.18f;          // seconds of straw before the bird shows
    private const float Whirr = 1.3f;           // seconds of fast beating (about 8 a second) right after the burst
    private const float WhirrHz = 8f;
    private const float BurstHz = 5f;           // later short flaps between glides
    private const float BurstFor = 0.6f, GlideFor = 0.7f;
    private const int Straws = 12;

    private static readonly Color Copper = Color.FromArgb(178, 92, 40);
    private static readonly Color CopperDark = Color.FromArgb(120, 58, 30);
    private static readonly Color Breast = Color.FromArgb(196, 98, 44);
    private static readonly Color Gold = Color.FromArgb(214, 160, 84);
    private static readonly Color WingBrown = Color.FromArgb(166, 108, 62);
    private static readonly Color Feather = Color.FromArgb(104, 66, 42);
    private static readonly Color HeadGreen = Color.FromArgb(34, 104, 96);
    private static readonly Color Line = Color.FromArgb(70, 38, 26);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;                  // pixels in one bird unit (about the body's length)
    private readonly int _w, _h, _ox, _oy;      // sprite size and where the body's middle is in it
    private readonly uint[][] _flap = new uint[FlapPages][];
    private readonly uint[] _glide;

    private float _total = 4f;                  // this showing's length, worked out in Begin
    private float _flight = 4f;                 // seconds the whole curve would take
    private float _exitAt = 1f;                 // how far along the curve (0 to 1) the bird is off screen
    private int _dir = 1;
    private PointF _a, _c, _b;                  // the curve: start, pull point, end
    private readonly float[] _strawAngle = new float[Straws], _strawSpeed = new float[Straws], _strawSpin = new float[Straws];
    private readonly bool[] _strawDark = new bool[Straws];

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "cornshock";

    public Pheasant(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(9f, s.U * 0.07f);
        _w = (int)(2.5f * _k) + 6;
        _h = (int)(2.4f * _k) + 6;
        _ox = (int)(1.65f * _k) + 3;
        _oy = _h / 2;
        for (int i = 0; i < FlapPages; i++) _flap[i] = Page(i / (float)FlapPages, false);
        _glide = Page(0, true);
    }

    // ----------------------------------------------------------------- the dice

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        bool top = rng.NextDouble() < 0.4;
        float shockTall = _s.CornShockFoot.Y - _s.CornShockTop.Y;
        float W = _s.Width, H = _s.Height, k = _k;
        _a = new PointF(_s.CornShockFoot.X + (rng.NextSingle() - 0.5f) * _s.CornShockHalfWidth * 0.8f, _s.CornShockFoot.Y - shockTall * 0.5f);
        _b = top ? new PointF(_a.X + _dir * 0.40f * W, -4f * k)
                 : new PointF(_dir > 0 ? W + 4f * k : -4f * k, _a.Y - (0.55f + 0.25f * rng.NextSingle()) * H);
        _c = new PointF(_a.X + _dir * Math.Abs(_b.X - _a.X) * 0.16f, _a.Y + (_b.Y - _a.Y) * 0.85f);

        // How long the curve is, and so how long it takes at a calm average speed (a fraction of U a second).
        float len = 0;
        PointF prev = _a;
        for (int i = 1; i <= 64; i++)
        {
            PointF p = At(i / 64f);
            len += MathF.Sqrt((p.X - prev.X) * (p.X - prev.X) + (p.Y - prev.Y) * (p.Y - prev.Y));
            prev = p;
        }
        _flight = Math.Clamp(len / (0.30f * _s.U), 3.2f, 6.5f);

        // The first moment the bird is wholly off screen: the showing ends there.
        _exitAt = 1f;
        float margin = 1.8f * k;
        for (int i = 5; i <= 400; i++)
        {
            float f = i / 400f;
            PointF p = At(Ease(f));
            if (p.X < -margin || p.X > W + margin || p.Y < -margin) { _exitAt = f; break; }
        }
        _total = Delay + _flight * _exitAt + 0.1f;

        for (int i = 0; i < Straws; i++)
        {
            _strawAngle[i] = -MathF.PI * (0.12f + 0.76f * rng.NextSingle());      // up and out, in screen angles
            _strawSpeed[i] = _s.U * (0.10f + 0.20f * rng.NextSingle());
            _strawSpin[i] = (rng.NextSingle() * 2 - 1) * 14f;
            _strawDark[i] = rng.Next(3) == 0;
        }
    }

    /// <summary>The bird is quick off the mark and slows a little: 0 to 1 in, 0 to 1 out.</summary>
    private static float Ease(float f) => 1f - MathF.Pow(1f - f, 1.25f);

    private PointF At(float u) => new((1 - u) * (1 - u) * _a.X + 2 * (1 - u) * u * _c.X + u * u * _b.X,
                                     (1 - u) * (1 - u) * _a.Y + 2 * (1 - u) * u * _c.Y + u * u * _b.Y);

    /// <summary>The direction of travel at u, as an angle up from level (radians, always measured along the heading).</summary>
    private float Pitch(float u)
    {
        float dx = 2 * (1 - u) * (_c.X - _a.X) + 2 * u * (_b.X - _c.X);
        float dy = 2 * (1 - u) * (_c.Y - _a.Y) + 2 * u * (_b.Y - _c.Y);
        return MathF.Atan2(-dy, Math.Abs(dx) + 0.0001f);
    }

    // ------------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float ts = t - Delay;                                      // seconds since the bird burst out
        if (ts >= 0)
        {
            float f = Math.Min(1f, ts / _flight);
            float u = Ease(f);
            PointF p = At(u);
            // A little see-through for the first moments: it is behind the stalks.
            float opacity = 0.55f + 0.45f * Smooth(ts / 0.30f);
            opacity *= Smooth((_total - t) / 0.1f);

            uint[] page;
            if (ts < Whirr) page = _flap[(int)(ts * WhirrHz * FlapPages) % FlapPages];
            else
            {
                float q = (ts - Whirr) % (BurstFor + GlideFor);       // flap for a moment, then glide, again and again
                page = q < BurstFor ? _flap[(int)(q * BurstHz * FlapPages) % FlapPages] : _glide;
            }
            // The pitch is the climb angle of the path, eased a little so it never snaps.
            DrawTurned(fb, page, p.X, p.Y, _dir, Pitch(u) * 0.9f, opacity);
        }

        // The straw: loose bits thrown up out of the shock, tumbling, falling back and fading, over the bird.
        for (int i = 0; i < Straws; i++)
        {
            float age = t - 0.02f * i;
            if (age < 0 || age > 1.1f) continue;
            float vx = MathF.Cos(_strawAngle[i]) * _strawSpeed[i], vy = MathF.Sin(_strawAngle[i]) * _strawSpeed[i];
            float x = _a.X + vx * age, y = _a.Y + vy * age + 0.5f * _s.U * 0.55f * age * age;
            float ang = _strawSpin[i] * age;
            float half = Math.Max(2f, _s.U * 0.009f);
            float dx = MathF.Cos(ang) * half, dy = MathF.Sin(ang) * half;
            Color c = _strawDark[i] ? Color.FromArgb(176, 134, 64) : Color.FromArgb(236, 204, 118);
            fb.Line(new PointF(x - dx, y - dy), new PointF(x + dx, y + dy), c, 1f - age / 1.1f, Math.Max(1f, _s.U * 0.0025f), _s.OpenOfBranches);
        }
    }

    /// <summary>
    /// Stamps a bird sticker turned to a slant. "face" is 1 for a bird that
    /// heads right, -1 for left (the sticker is turned over); "pitch" is the
    /// nose-up angle in radians. For each screen pixel in the turned
    /// sticker's box, find where in the sticker it comes from and blend the
    /// four nearest sticker pixels (so the edge stays smooth).
    /// </summary>
    private void DrawTurned(FrameBuffer fb, uint[] page, float cx, float cy, int face, float pitch, float opacity)
    {
        float th = -face * pitch;                                  // the screen turn (y runs down, so a positive turn is clockwise)
        float c = MathF.Cos(th), s = MathF.Sin(th);
        // The box that holds the turned sticker.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            float px = (i & 1) == 0 ? -_ox : _w - _ox, py = (i & 2) == 0 ? -_oy : _h - _oy;
            float wx = face * px, wy = py;
            float rx = wx * c - wy * s, ry = wx * s + wy * c;
            minX = Math.Min(minX, rx); maxX = Math.Max(maxX, rx); minY = Math.Min(minY, ry); maxY = Math.Max(maxY, ry);
        }
        int x0 = Math.Max(0, (int)MathF.Floor(cx + minX)), x1 = Math.Min(fb.Width, (int)MathF.Ceiling(cx + maxX) + 1);
        int y0 = Math.Max(0, (int)MathF.Floor(cy + minY)), y1 = Math.Min(fb.Height, (int)MathF.Ceiling(cy + maxY) + 1);
        if (x1 <= x0 || y1 <= y0) return;

        uint[] frame = fb.Pixels;
        bool[] open = _s.OpenOfBranches;
        int fw = fb.Width, sw = _w, sh = _h;
        int op = (int)(Math.Min(1f, opacity) * 256);
        float ox = _ox, oy = _oy;

        void Rows(int ya, int yb)
        {
            for (int y = ya; y < yb; y++)
                for (int x = x0; x < x1; x++)
                {
                    // Back from the screen pixel to the sticker (turn the other way, undo the flip).
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float a = dx * c + dy * s, b = -dx * s + dy * c;
                    float fx = a * face + ox - 0.5f, fy = b + oy - 0.5f;
                    int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
                    if (ix < -1 || iy < -1 || ix >= sw || iy >= sh) continue;
                    float wx = fx - ix, wy = fy - iy;
                    // Four neighbours, each weighted by how close and how solid it is.
                    float sumA = 0, sumR = 0, sumG = 0, sumB = 0;
                    for (int n = 0; n < 4; n++)
                    {
                        int jx = ix + (n & 1), jy = iy + (n >> 1);
                        if (jx < 0 || jy < 0 || jx >= sw || jy >= sh) continue;
                        uint px = page[jy * sw + jx];
                        float al = px >> 24;
                        if (al == 0) continue;
                        float wgt = ((n & 1) == 0 ? 1 - wx : wx) * ((n >> 1) == 0 ? 1 - wy : wy) * al;
                        sumA += wgt; sumR += wgt * ((px >> 16) & 0xFF); sumG += wgt * ((px >> 8) & 0xFF); sumB += wgt * (px & 0xFF);
                    }
                    if (sumA < 1f) continue;
                    int idx = y * fw + x;
                    if (!open[idx]) continue;                      // a corner branch is in front here
                    int alpha = (int)sumA * op >> 8;
                    if (alpha <= 0) continue;
                    uint bg = frame[idx];
                    int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                    int r = br + (((int)(sumR / sumA) - br) * alpha >> 8);
                    int g = bgn + (((int)(sumG / sumA) - bgn) * alpha >> 8);
                    int bl = bb + (((int)(sumB / sumA) - bb) * alpha >> 8);
                    frame[idx] = (uint)((r << 16) | (g << 8) | bl);
                }
        }
        if ((x1 - x0) * (y1 - y0) > 40_000) Parallel.For(0, (y1 - y0 + 31) / 32, band => Rows(y0 + band * 32, Math.Min(y1, y0 + band * 32 + 32)));
        else Rows(y0, y1);
    }

    // ------------------------------------------------------------------ paint

    /// <summary>One page: the wings at "phase" of a flap (0 = straight up, 0.5 = down), or held out for a glide.</summary>
    private uint[] Page(float phase, bool glide) =>
        Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _oy);
            g.ScaleTransform(_k, -_k);                             // from here on: units of one bird, y runs UP
            Bird(g, phase, glide);
        }).Argb.ToArray();

    private static PointF P(float x, float y) => new(x, y);

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    private static void Bird(Graphics g, float phase, bool glide)
    {
        void Ell(float cx, float cy, float rx, float ry, Color c, float tilt = 0)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);
            using var b = new SolidBrush(c);
            g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
            g.Restore(st);
        }
        void Poly(PointF[] pts, Color c)
        {
            using var b = new SolidBrush(c);
            g.FillPolygon(b, pts);
        }

        // The wing angle: straight up (84 degrees) at phase 0, down (-60) at 0.5. Gliding: held out, bowed a little down.
        float a = glide ? -12f : 12f + 72f * MathF.Cos(MathF.Tau * phase);

        // A wing is built from its leading edge (the root out to the tip at angle "a") and a trailing edge
        // dragged straight back from it, a chord wide at the root and narrower toward the tip, with the last
        // part notched into feather fingers.
        PointF[] Wing(PointF root, float ang, float len, float chord, float s0, float s1)
        {
            float r = ang * MathF.PI / 180f;
            PointF d = P(MathF.Cos(r), MathF.Sin(r));
            PointF n = P(-d.Y, d.X);
            if (n.X < 0) n = P(d.Y, -d.X);                         // "n" points toward the front of the bird
            const int N = 10;
            var pts = new PointF[(N + 1) * 2];
            for (int i = 0; i <= N; i++)
            {
                float s = s0 + (s1 - s0) * i / N;
                float bow = 0.06f * len * MathF.Sin(MathF.PI * s);
                pts[i] = P(root.X + d.X * s * len + n.X * bow, root.Y + d.Y * s * len + n.Y * bow);
            }
            for (int i = 0; i <= N; i++)
            {
                float s = s1 - (s1 - s0) * i / N;
                float bow = 0.06f * len * MathF.Sin(MathF.PI * s);
                float c = chord * (0.18f + 0.82f * MathF.Pow(1 - s, 0.8f));
                if (s > 0.55f && i % 2 == 1) c *= 0.78f;           // notch the feather tips
                pts[N + 1 + i] = P(root.X + d.X * s * len + n.X * bow - c, root.Y + d.Y * s * len + n.Y * bow);
            }
            return pts;
        }

        // ---- the long tail, behind everything: a barred wedge streaming back, with a little flutter ----
        {
            float tilt = (glide ? -2f : -3f + 5f * MathF.Sin(MathF.Tau * phase * 2)) * MathF.PI / 180f;
            PointF root = P(-0.30f, 0.0f);
            PointF Ax(float s) => P(root.X - MathF.Cos(tilt) * 1.25f * s, root.Y + MathF.Sin(tilt) * 1.25f * s);
            float Wd(float s) => 0.12f * MathF.Pow(1 - s, 0.5f) + 0.015f;
            var top = new PointF[7]; var bot = new PointF[7];
            for (int i = 0; i < 7; i++) { float s = i / 6f; top[i] = P(Ax(s).X, Ax(s).Y + Wd(s)); bot[i] = P(Ax(s).X, Ax(s).Y - Wd(s)); }
            var all = new PointF[14];
            for (int i = 0; i < 7; i++) { all[i] = top[i]; all[13 - i] = bot[i]; }
            Poly(all, Color.FromArgb(188, 142, 74));
            for (int bar = 1; bar < 7; bar++)                       // the dark bars across the tail
            {
                float s0 = bar * 0.15f, s1 = s0 + 0.07f;
                Poly([P(Ax(s0).X, Ax(s0).Y + Wd(s0)), P(Ax(s1).X, Ax(s1).Y + Wd(s1)), P(Ax(s1).X, Ax(s1).Y - Wd(s1)), P(Ax(s0).X, Ax(s0).Y - Wd(s0))], Feather);
            }
        }

        // ---- the far wing, behind the body, darker and a touch smaller ----
        {
            PointF[] far = Wing(P(0.05f, 0.10f), a * 0.9f, 0.88f, 0.60f, 0f, 1f);
            Poly(far, Mix(WingBrown, Color.Black, 0.35f));
        }

        // ---- the body ----
        Ell(0f, 0f, 0.46f, 0.24f, Copper, -3f);                    // the whole body, copper
        Ell(-0.02f, -0.12f, 0.36f, 0.12f, CopperDark);             // the darker belly
        Ell(0.14f, -0.02f, 0.30f, 0.20f, Breast, 6f);              // the red breast, pushed out in front
        Ell(-0.06f, 0.11f, 0.38f, 0.10f, Gold, -4f);               // the golden back with its dark scale marks
        for (int i = 0; i < 5; i++) Ell(-0.28f + i * 0.13f, 0.12f - 0.004f * i, 0.035f, 0.025f, Feather);
        Ell(-0.34f, 0.02f, 0.14f, 0.10f, Color.FromArgb(132, 154, 140), -6f);   // the pale blue-grey rump
        for (int i = 0; i < 4; i++) Ell(0.10f + i * 0.07f, -0.04f - 0.01f * (i % 2), 0.014f, 0.014f, Feather);   // spots on the breast

        // ---- the near wing: the big, beating one ----
        {
            PointF root = P(0.07f, 0.09f);
            PointF[] wing = Wing(root, a, 0.95f, 0.64f, 0f, 1f);
            Poly(wing, WingBrown);
            Poly(Wing(root, a, 0.95f, 0.64f, 0f, 0.5f), Mix(WingBrown, Gold, 0.35f));   // lighter coverts near the body
            Poly(Wing(root, a, 0.95f, 0.64f, 0.62f, 1f), Feather);                       // dark flight feathers at the tip
            using var edge = new Pen(Color.FromArgb(120, Line), 0.02f) { LineJoin = LineJoin.Round };
            g.DrawPolygon(edge, wing);
        }

        // ---- the neck, the white ring, the head ----
        Ell(0.42f, 0.10f, 0.13f, 0.15f, HeadGreen, 30f);           // the neck, dark green
        Ell(0.35f, 0.045f, 0.045f, 0.15f, Color.FromArgb(244, 240, 226), 35f);   // the white ring
        Ell(0.54f, 0.22f, 0.125f, 0.115f, HeadGreen);              // the head
        Ell(0.51f, 0.27f, 0.07f, 0.04f, Color.FromArgb(70, 150, 150), 8f);       // the blue sheen on top
        Ell(0.60f, 0.20f, 0.065f, 0.055f, Color.FromArgb(208, 48, 46));          // the bare red face
        Poly([P(0.63f, 0.235f), P(0.76f, 0.195f), P(0.64f, 0.165f)], Color.FromArgb(232, 222, 176));   // the beak
        Ell(0.58f, 0.23f, 0.034f, 0.034f, Color.FromArgb(244, 232, 160));        // the eye: a pale ring
        Ell(0.585f, 0.23f, 0.02f, 0.02f, Color.FromArgb(24, 14, 14));
        Ell(0.592f, 0.24f, 0.007f, 0.007f, Color.White);           // and its glint
        Ell(0.46f, 0.33f, 0.03f, 0.04f, HeadGreen, 20f);           // the little ear tuft
    }
}
