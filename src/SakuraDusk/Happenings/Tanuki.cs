using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// A tanuki (a Japanese raccoon dog, the round, cheerful one from the folk
/// tales and the Ghibli films) waddles in along a bank from off the edge of
/// the screen, stops by a cherry tree, plops down on its round bottom and
/// looks up at the falling blossom. One petal drifts down to it. It lifts its
/// little paws and bats at the petal, flings it away, watches it go, gets up,
/// turns round with a little hop, and waddles back off the way it came.
///
/// FEYNMAN VERSION: the tanuki is a flip-book again. Every page is the same
/// drawing made by ONE painting routine, fed a few knobs: how far into a
/// step the legs are, how sat-down it is (0 standing, 1 sitting), how far
/// the head is tipped up, how high each paw is raised, whether the eyes are
/// shut. We paint the pages once (a sticker each) and stamp the right one for
/// the moment. The walk has 24 pages that loop, and a gentle sway left and
/// right (a "waddle") is built into them.
///
/// Light: the sun is low and on the pond side, so a thin warm rim of light is
/// added along the edge of the fur that faces it. That is worked out from the
/// finished page's own pixels (any solid pixel with open air beside it on the
/// lit side). Because the tanuki turns round, there are two sets of walk
/// pages: rim on the front, rim on the back.
///
/// Where it goes: it stands on the bank's walk line (WalkY) at every x, so it
/// follows the slope. It stops on the OUTER side of a tree near the screen
/// edge, which keeps the whole visit about fourteen seconds.
/// </summary>
internal sealed class Tanuki : Happening
{
    private const int WalkPoses = 24;
    private const int BatPoses = 8;

    private readonly DuskScenery _s;
    private readonly float _u, _size;                       // size unit; the body's width in pixels
    private readonly Stamp[] _walkFront = new Stamp[WalkPoses];   // rim of light on the front
    private readonly Stamp[] _walkBack = new Stamp[WalkPoses];    // rim of light on the back
    private readonly Stamp[] _down = new Stamp[5];          // sitting down (standing up plays it backwards)
    private readonly Stamp[,] _idle = new Stamp[3, 2];      // sat: head level 0 / mid / up, eyes open / shut
    private readonly Stamp[] _bat = new Stamp[BatPoses];    // paws batting, one cycle
    private readonly Stamp[] _raise = new Stamp[2];         // paws coming up
    private readonly Stamp[] _petal = new Stamp[8];         // the petal, turning
    private readonly float _groundBelowCenter;              // how far under a page's middle the ground line is

    // This showing's dice (rolled in Begin).
    private float _dir;                                     // +1: walks right (from the left bank); -1: left
    private float _startX, _stopX;                          // off screen; and where it sits
    private float _tin, _tout, _seconds = 14f;
    private float _wobble;                                  // the petal's sideways drift, random per showing

    private const float Speed = 0.085f;                     // U per second
    private const float Stride = 0.062f;                    // U per full walk cycle (a short, quick, waddly step)

    public override float Seconds => _seconds;
    public override string? Claims => "bank";
    public override int Layer => 1;

    public Tanuki(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _size = Math.Max(16f, s.U * 0.045f);               // body width; with head and tail it is about 0.065 U long
        // Pages are 2.4 S wide and 2.0 S tall, the ground line at 92% of the height.
        int ph = (int)(2.0f * _size) + 4;
        _groundBelowCenter = 0.92f * ph - ph / 2f;

        for (int k = 0; k < WalkPoses; k++)
        {
            float phase = k / (float)WalkPoses;
            _walkFront[k] = Make(_size, new Look { Walk = phase }, +1);
            _walkBack[k] = Make(_size, new Look { Walk = phase }, -1);
        }
        for (int i = 0; i < 5; i++)
            _down[i] = Make(_size, new Look { Walk = 0.12f, Sit = (i + 1) / 6f }, +1);
        float[] tilts = [0f, 0.22f, 0.5f];
        for (int l = 0; l < 3; l++)
            for (int b = 0; b < 2; b++)
                _idle[l, b] = Make(_size, new Look { Sit = 1f, Tilt = tilts[l], Blink = b == 1 }, +1);
        for (int k = 0; k < BatPoses; k++)
        {
            float a = MathF.Tau * k / BatPoses;
            _bat[k] = Make(_size, new Look { Sit = 1f, Tilt = 0.22f, PawNear = 0.55f + 0.45f * MathF.Sin(a), PawFar = 0.55f + 0.45f * MathF.Sin(a + MathF.PI) }, +1);
        }
        _raise[0] = Make(_size, new Look { Sit = 1f, Tilt = 0.22f, PawNear = 0.25f, PawFar = 0.15f }, +1);
        _raise[1] = Make(_size, new Look { Sit = 1f, Tilt = 0.22f, PawNear = 0.50f, PawFar = 0.35f }, +1);

        int pd = Math.Max(8, (int)(s.U * 0.021f));
        for (int k = 0; k < 8; k++)
        {
            float rot = k * 45f;
            _petal[k] = Stamp.Paint(pd + 4, pd + 4, g =>
            {
                g.TranslateTransform((pd + 4) / 2f, (pd + 4) / 2f);
                g.RotateTransform(rot);
                // A petal: a pale pink teardrop (narrow tip, round shoulder) with a darker rim, so it reads against the blossom behind it.
                using var petal = new GraphicsPath();
                petal.AddBezier(-pd * 0.5f, 0f, -pd * 0.15f, -pd * 0.62f, pd * 0.55f, -pd * 0.55f, pd * 0.5f, 0f);
                petal.AddBezier(pd * 0.5f, 0f, pd * 0.55f, pd * 0.55f, -pd * 0.15f, pd * 0.62f, -pd * 0.5f, 0f);
                using var fillB = new SolidBrush(Color.FromArgb(255, 212, 226));
                g.FillPath(fillB, petal);
                using var rimP = new Pen(Color.FromArgb(224, 128, 160), Math.Max(1f, pd * 0.10f));
                g.DrawPath(rimP, petal);
            });
        }
    }

    // ------------------------------------------------------- the painting

    /// <summary>The knobs for one page of the flip-book.</summary>
    private struct Look
    {
        public float Walk;        // 0..1 through one step cycle
        public float Sit;         // 0 standing, 1 sat on its bottom
        public float Tilt;        // head tipped up, in radians
        public bool Blink;
        public float PawNear, PawFar;   // 0 resting on the belly .. 1 held up high
    }

    private static Color Fur => Color.FromArgb(160, 130, 104);
    private static Color FurDark => Color.FromArgb(118, 94, 78);
    private static Color Belly => Color.FromArgb(218, 194, 160);
    private static Color Dark => Color.FromArgb(66, 48, 46);
    private static Color Mask => Color.FromArgb(58, 42, 42);
    private static Color Cream => Color.FromArgb(240, 226, 200);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Paints one page: the tanuki facing right, with the ground line at its bottom.</summary>
    private static Stamp Make(float S, Look k, int rimSide)
    {
        int w = (int)(2.4f * S) + 4, h = (int)(2.0f * S) + 4;
        float ox = 0.56f * w, oy = 0.92f * h;
        float sit = k.Sit, walk = k.Walk;
        float roll = (1 - sit) * 0.085f * MathF.Sin(MathF.Tau * walk);                 // the waddle: rock to one side then the other
        float bob = (1 - sit) * 0.03f * MathF.Abs(MathF.Sin(MathF.Tau * walk));

        return Stamp.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(S, S);
            using var outline = new Pen(Color.FromArgb(150, 52, 38, 38), 0.03f) { LineJoin = LineJoin.Round };

            // The ground shadow: a soft dark oval under the feet.
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-0.62f, -0.07f, 1.30f, 0.16f);
                using var sh = new PathGradientBrush(path) { CenterColor = Color.FromArgb(120, 16, 8, 28), SurroundColors = [Color.FromArgb(0, 16, 8, 28)] };
                g.FillPath(sh, path);
            }

            g.RotateTransform(roll * 180f / MathF.PI);                                  // everything rocks about the feet

            // ---- 1. the tail, behind everything ----
            {
                PointF baseP = new(Lerp(-0.40f, -0.36f, sit), Lerp(-0.56f, -0.20f, sit));
                float wag = 5f * MathF.Sin(MathF.Tau * walk + 1f) * (1 - sit);
                float ang = Lerp(-128f, -168f, sit) + wag;
                var st = g.Save();
                g.TranslateTransform(baseP.X, baseP.Y);
                g.RotateTransform(ang);
                using var tail = new GraphicsPath(FillMode.Winding);          // Winding: overlapping bumps join up instead of cancelling out
                tail.AddEllipse(0f, -0.25f, 0.84f, 0.50f);
                for (int i = 0; i < 9; i++)                                             // fur tufts round the edge: fluffy
                {
                    float a = MathF.Tau * i / 9f + 0.3f;
                    float tx = 0.42f + 0.40f * MathF.Cos(a), ty = 0.245f * MathF.Sin(a);
                    if (tx < 0.16f) continue;
                    tail.AddEllipse(tx - 0.075f, ty - 0.075f, 0.15f, 0.15f);
                }
                using (var tb = new LinearGradientBrush(new RectangleF(0f, -0.34f, 0.9f, 0.68f), Color.FromArgb(176, 146, 116), Color.FromArgb(130, 104, 84), 90f))
                    g.FillPath(tb, tail);
                var st2 = g.Save();
                g.SetClip(tail, CombineMode.Intersect);
                using (var band = new SolidBrush(Color.FromArgb(150, Dark)))            // the dark rings
                {
                    g.FillRectangle(band, 0.20f, -0.5f, 0.08f, 1.0f);
                    g.FillRectangle(band, 0.40f, -0.5f, 0.09f, 1.0f);
                    g.FillRectangle(band, 0.62f, -0.5f, 0.30f, 1.0f);                    // and the dark tip
                }
                g.Restore(st2);
                g.Restore(st);
            }

            // ---- 2. the far legs (walking) ----
            float hipFront = Lerp(-0.34f, -0.14f, sit), hipBack = hipFront;
            void Leg(float hipX, float off, Color col, float dx)
            {
                float swing = MathF.Sin(MathF.Tau * (walk + off)), lift = Math.Max(0f, MathF.Cos(MathF.Tau * (walk + off)));
                float fx = hipX + dx + 0.15f * swing * (1 - sit), fy = -0.04f - 0.075f * lift * (1 - sit);
                if (sit > 0.5f) return;                                                 // sat: the legs are folded away
                using var pen = new Pen(col, 0.19f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, hipX + dx, hipFront, fx, fy);
                using var paw = new SolidBrush(col);
                g.FillEllipse(paw, fx - 0.12f, fy - 0.07f, 0.25f, 0.13f);
            }
            Color farLeg = Color.FromArgb(48, 36, 36);
            Leg(0.30f, 0.5f, farLeg, -0.10f);     // far front
            Leg(-0.28f, 0.0f, farLeg, -0.10f);    // far back

            // ---- 3. the body: a plump pear ----
            PointF bc = new(Lerp(0f, -0.04f, sit), Lerp(-0.62f, -0.54f, sit) - bob);
            float rx = Lerp(0.52f, 0.43f, sit), ry = Lerp(0.41f, 0.52f, sit);
            float lean = Lerp(0f, -0.22f, sit);                                          // sat: leaning back a little
            {
                var st = g.Save();
                g.TranslateTransform(bc.X, bc.Y);
                g.RotateTransform(lean * 180f / MathF.PI);
                using var path = new GraphicsPath();
                path.AddEllipse(-rx, -ry, 2 * rx, 2 * ry);
                using (var gb = new LinearGradientBrush(new RectangleF(-rx, -ry - 0.01f, 2 * rx, 2 * ry + 0.02f), Color.FromArgb(172, 142, 112), Color.FromArgb(132, 106, 86), 90f))
                    g.FillPath(gb, path);
                var st2 = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var bb = new SolidBrush(Belly))                                   // a paler tummy
                    g.FillEllipse(bb, -rx * 0.15f, -ry * 0.25f, rx * 1.2f, ry * 1.25f);
                g.Restore(st2);
                g.DrawPath(outline, path);
                g.Restore(st);
            }

            // ---- 4. the near legs (walking), or the haunch and feet (sitting) ----
            Color nearLeg = Dark;
            Leg(0.30f, 0.0f, nearLeg, 0.0f);       // near front
            Leg(-0.28f, 0.5f, nearLeg, 0.0f);      // near back
            if (sit > 0.3f)
            {
                int a = (int)(255 * Math.Clamp((sit - 0.3f) / 0.7f, 0, 1));
                using var thigh = new SolidBrush(Color.FromArgb(a, 150, 122, 98));
                g.FillEllipse(thigh, -0.30f, -0.50f, 0.56f, 0.46f);
                using var th = new Pen(Color.FromArgb(a / 2, 52, 38, 38), 0.03f);
                g.DrawEllipse(th, -0.30f, -0.50f, 0.56f, 0.46f);
                using var sole = new SolidBrush(Color.FromArgb(a, Dark));                 // two feet, soles toward us
                g.FillEllipse(sole, 0.04f, -0.17f, 0.26f, 0.17f);
                g.FillEllipse(sole, 0.28f, -0.15f, 0.25f, 0.16f);
                using var pad = new SolidBrush(Color.FromArgb(a, 196, 150, 142));
                g.FillEllipse(pad, 0.10f, -0.12f, 0.12f, 0.08f);
                g.FillEllipse(pad, 0.34f, -0.11f, 0.12f, 0.07f);
            }

            // ---- 5. the head ----
            PointF hc = new(Lerp(0.42f, 0.24f, sit), Lerp(-0.80f, -1.04f, sit) - bob);
            {
                var st = g.Save();
                g.TranslateTransform(hc.X - 0.10f, hc.Y + 0.24f);                         // tip the head about the neck
                g.RotateTransform(-k.Tilt * 180f / MathF.PI);
                g.TranslateTransform(0.10f, -0.24f);
                const float hr = 0.31f;

                using var earOuter = new SolidBrush(Dark);
                using var earInner = new SolidBrush(Color.FromArgb(206, 160, 150));
                g.FillEllipse(earOuter, -0.30f, -0.37f, 0.20f, 0.20f);                    // far ear
                g.FillEllipse(earInner, -0.26f, -0.33f, 0.12f, 0.12f);

                // fluffy cheek ruff: a few little tufts
                using var tuft = new SolidBrush(Color.FromArgb(188, 158, 126));
                foreach (float a in new[] { 150f, 172f, 128f })
                {
                    float r = a * MathF.PI / 180f;
                    PointF[] tri =
                    [
                        new(MathF.Cos(r - 0.22f) * hr * 0.92f, MathF.Sin(r - 0.22f) * hr * 0.92f),
                        new(MathF.Cos(r) * (hr + 0.10f), MathF.Sin(r) * (hr + 0.10f)),
                        new(MathF.Cos(r + 0.22f) * hr * 0.92f, MathF.Sin(r + 0.22f) * hr * 0.92f),
                    ];
                    g.FillPolygon(tuft, tri);
                }
                using (var hb = new LinearGradientBrush(new RectangleF(-hr, -hr, 2 * hr, 2 * hr), Color.FromArgb(182, 152, 122), Color.FromArgb(156, 128, 104), 90f))
                    g.FillEllipse(hb, -hr, -hr, 2 * hr, 2 * hr);
                g.DrawEllipse(outline, -hr, -hr, 2 * hr, 2 * hr);

                g.FillEllipse(earOuter, 0.05f, -0.40f, 0.22f, 0.22f);                     // near ear
                g.FillEllipse(earInner, 0.09f, -0.36f, 0.13f, 0.13f);

                // the dark "bandit" mask: two rounded patches that sweep down and out
                using (var mb = new SolidBrush(Mask))
                {
                    var s1 = g.Save(); g.TranslateTransform(0.13f, -0.02f); g.RotateTransform(-18f);
                    g.FillEllipse(mb, -0.14f, -0.105f, 0.28f, 0.21f); g.Restore(s1);
                    var s2 = g.Save(); g.TranslateTransform(-0.10f, -0.03f); g.RotateTransform(16f);
                    g.FillEllipse(mb, -0.12f, -0.095f, 0.24f, 0.19f); g.Restore(s2);
                }
                // the pale muzzle, and the black button nose
                using (var cb = new SolidBrush(Cream))
                    g.FillEllipse(cb, 0.10f, 0.03f, 0.27f, 0.20f);
                using (var nb = new SolidBrush(Color.FromArgb(22, 16, 22)))
                    g.FillEllipse(nb, 0.285f, 0.045f, 0.095f, 0.07f);
                using (var nh = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.FillEllipse(nh, 0.31f, 0.052f, 0.03f, 0.02f);
                using (var mouth = new Pen(Color.FromArgb(150, 40, 28, 30), 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(mouth, 0.33f, 0.115f, 0.33f, 0.14f);
                    g.DrawCurve(mouth, new PointF[] { new(0.24f, 0.15f), new(0.29f, 0.175f), new(0.33f, 0.14f), new(0.37f, 0.17f), new(0.40f, 0.145f) });
                }
                // big dark shiny eyes
                foreach (var e in new[] { new PointF(0.145f, -0.025f), new PointF(-0.095f, -0.03f) })
                {
                    float er = e.X > 0 ? 0.062f : 0.056f;
                    if (k.Blink)
                    {
                        using var lid = new Pen(Color.FromArgb(240, 18, 12, 16), 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        g.DrawArc(lid, e.X - er, e.Y - er * 0.5f, er * 2, er * 1.4f, 200, 140);
                    }
                    else
                    {
                        using var eb = new SolidBrush(Color.FromArgb(10, 6, 12));
                        g.FillEllipse(eb, e.X - er, e.Y - er * 1.1f, er * 2, er * 2.2f);
                        using var wb = new SolidBrush(Color.White);
                        g.FillEllipse(wb, e.X - er * 0.55f, e.Y - er * 0.85f, er * 0.85f, er * 0.85f);
                        g.FillEllipse(wb, e.X + er * 0.15f, e.Y + er * 0.25f, er * 0.40f, er * 0.40f);
                    }
                }
                using (var bl = new SolidBrush(Color.FromArgb(110, 255, 150, 150)))        // blush
                    g.FillEllipse(bl, 0.00f, 0.07f, 0.12f, 0.07f);
                g.Restore(st);
            }

            // ---- 6. the arms (sitting): resting on the tummy, or raised to bat ----
            if (sit > 0.3f)
            {
                int a = (int)(255 * Math.Clamp((sit - 0.3f) / 0.7f, 0, 1));
                void Arm(PointF shoulder, PointF paw, Color c)
                {
                    using var pen = new Pen(Color.FromArgb(a, c), 0.15f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(pen, shoulder, paw);
                    using var pb = new SolidBrush(Color.FromArgb(a, Dark));
                    g.FillEllipse(pb, paw.X - 0.085f, paw.Y - 0.075f, 0.17f, 0.15f);
                }
                Arm(new PointF(0.08f, -0.68f), new PointF(0.44f + 0.14f * k.PawFar, -0.50f - 0.42f * k.PawFar), Color.FromArgb(104, 80, 66));
                Arm(new PointF(0.20f, -0.64f), new PointF(0.30f + 0.20f * k.PawNear, -0.44f - 0.46f * k.PawNear), FurDark);
            }
        }, rimSide: rimSide, rimPx: Math.Max(1, (int)(S * 0.05f)), rim: Color.FromArgb(255, 204, 128), rimAmount: 0.62f);
    }

    // -------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        int w = _s.Width;
        bool left = rng.NextDouble() < 0.5;
        _dir = left ? 1f : -1f;
        // The tree nearest the screen edge (but not hanging off it): the left bank's trees sit at
        // 0.02, 0.12 and 0.20 of the width, the right bank's at 0.80, 0.89 and 0.99.
        var trees = _s.Canopies.Where(c => left ? c.At.X > w * 0.08f && c.At.X < w * 0.45f : c.At.X < w * 0.92f && c.At.X > w * 0.55f).ToList();
        float treeX = trees.Count == 0 ? (left ? w * 0.12f : w * 0.89f)
                    : left ? trees.Min(c => c.At.X) : trees.Max(c => c.At.X);
        _stopX = treeX - _dir * _u * 0.05f;                      // on the OUTER side of the trunk, so the visit stays short
        _startX = left ? -_size * 1.6f : w + _size * 1.6f;
        float dist = Math.Abs(_stopX - _startX);
        float v = Speed * _u;
        _tin = dist / v + 0.225f;
        _tout = dist / v + 0.15f;
        _seconds = _tin + SitSeconds + _tout + 0.2f;
        _wobble = (float)rng.NextDouble() * MathF.Tau;
    }

    private const float SitSeconds = 6.3f;

    // -------------------------------------------------------------- draw

    /// <summary>How far along the path (pixels from the start) it has walked t seconds into the walk in. Eases to a stop at the end.</summary>
    private float DistIn(float t)
    {
        float v = Speed * _u, decel = 0.45f, t0 = _tin - decel;
        if (t < t0) return v * t;
        float tau = Math.Min(decel, t - t0);
        return v * t0 + v * tau - v * tau * tau / (2 * decel);
    }

    /// <summary>The same for the walk out: starts from rest.</summary>
    private float DistOut(float t)
    {
        float v = Speed * _u, accel = 0.30f;
        if (t < accel) return v * t * t / (2 * accel);
        return v * accel / 2 + v * (t - accel);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float v = Speed * _u;
        float gyAt(float x) => _s.WalkY(x);
        float cyFor(float gy) => gy - _groundBelowCenter;
        float alphaIn = Smooth(t / 0.25f);                       // (it starts off screen anyway)
        float a = _tin;                                          // the moment it arrives; times below are relative to it

        // ---- walking in ----
        if (t < a)
        {
            float d = DistIn(t);
            float x = _startX + _dir * d;
            int pose = (int)(d / (Stride * _u) % 1f * WalkPoses) % WalkPoses;
            // Slow the legs down with the walk as it eases to a stop.
            _walkFront[pose].Draw(fb, MathF.Round(x), MathF.Round(cyFor(gyAt(x))), alphaIn, _dir < 0, null);
            return;
        }

        float r = t - a;                                         // seconds since it sat
        float gy = gyAt(_stopX), cy = MathF.Round(cyFor(gy)), cx = MathF.Round(_stopX);
        bool mir = _dir < 0;

        // ---- sitting, looking up, batting, watching ----
        if (r < 5.3f)
        {
            Stamp page;
            if (r < 0.7f)                                        // sitting down
            {
                int i = Math.Min(_down.Length - 1, (int)(r / 0.7f * (_down.Length + 1)) - 0);
                page = i < 0 ? _walkFront[0] : _down[i];
            }
            else if (r < 2.9f || r > 4.1f)                       // sat: open eyes, blinking now and then, head up or level
            {
                int level = r < 1.1f ? 0 : r < 1.3f ? 1 : r < 2.0f ? 2 : r < 2.9f ? 1 : r < 4.1f ? 1 : r < 4.9f ? 2 : 1;
                bool blink = (r > 0.85f && r < 0.98f) || (r > 4.95f && r < 5.08f);
                page = _idle[level, blink ? 1 : 0];
            }
            else if (r < 3.0f) page = _raise[0];                 // paws come up
            else if (r < 3.1f) page = _raise[1];
            else                                                 // batting: three quick swipes
                page = _bat[(int)((r - 3.1f) * 2.6f * BatPoses) % BatPoses];
            page.Draw(fb, cx, cy, 1f, mir, null);
            DrawPetal(fb, r, gy);
            return;
        }

        // ---- getting up and turning round ----
        if (r < 5.9f)
        {
            float u = (r - 5.3f) / 0.6f;                         // 0..1 standing up (the sitting pages, backwards)
            int i = (int)((1 - u) * (_down.Length + 1)) - 1;
            Stamp page = i < 0 ? _walkFront[0] : _down[Math.Clamp(i, 0, _down.Length - 1)];
            page.Draw(fb, cx, cy, 1f, mir, null);
            return;
        }
        if (r < 6.3f)
        {
            // Turning: the tanuki hops and squashes thin and opens out facing the other way,
            // the way a paper cut-out turns round.
            float u = (r - 5.9f) / 0.4f;
            float xs = MathF.Abs(MathF.Cos(MathF.PI * u));
            xs = Math.Max(0.12f, xs);
            bool second = u > 0.5f;
            float hop = MathF.Sin(MathF.PI * u) * _size * 0.18f;
            Stamp page = second ? _walkBack[0] : _walkFront[0];
            bool m = second ? _dir > 0 : _dir < 0;                // after the turn it faces the other way
            page.Draw(fb, cx, cy - hop, 1f, m, null, xs);
            return;
        }

        // ---- walking out ----
        {
            float d = DistOut(r - 6.3f);
            float x = _stopX - _dir * d;
            int pose = (int)(d / (Stride * _u) % 1f * WalkPoses) % WalkPoses;
            float fadeOut = Smooth((Seconds - t) / 0.25f);
            _walkBack[pose].Draw(fb, MathF.Round(x), MathF.Round(cyFor(gyAt(x))), fadeOut, _dir > 0, null);
        }
    }

    /// <summary>
    /// The petal: it appears under the blossom, spins down to the tanuki's paws
    /// (swaying a little), gets batted away, flutters up and off, and fades.
    /// </summary>
    private void DrawPetal(FrameBuffer fb, float r, float gy)
    {
        const float appear = 1.2f, land = 3.3f, gone = 4.9f;
        if (r < appear || r > gone) return;
        PointF paw = new(_stopX + _dir * _size * 0.22f, gy - _size * 0.86f);
        float x, y, alpha = 1f;
        if (r < land)
        {
            float tau = (r - appear) / (land - appear);            // 0..1 falling
            float fall = tau * (0.4f + 0.6f * tau) / 1.0f;         // gathers speed a little
            y = paw.Y - _u * 0.30f * (1 - fall);
            x = paw.X + _dir * _u * 0.03f * MathF.Sin(MathF.Tau * (tau * 1.6f) + _wobble) * (1 - tau * 0.7f);
            alpha = Smooth(tau / 0.15f);
        }
        else
        {
            float u = (r - land) / (gone - land);                   // 0..1 after the swipe
            x = paw.X + _dir * _u * (0.015f + 0.085f * u) + _u * 0.012f * MathF.Sin(MathF.Tau * u * 2 + _wobble);
            y = paw.Y - _u * 0.05f * MathF.Sin(MathF.PI * Math.Min(1f, u * 1.15f)) + _u * 0.04f * u * u;
            alpha = Smooth((1 - u) / 0.35f);
        }
        _petal[(int)(r * 7f) % 8].Draw(fb, x, y, alpha, false, null);
    }

    // ------------------------------------------------ a stamp that can mirror

    /// <summary>
    /// A sprite like Core's, plus things it cannot do: stamp itself mirrored,
    /// squeezed thin (for turning round), and add a warm rim of light to one
    /// side from the finished picture's own pixels. (Core's Sprite is not changed.)
    /// </summary>
    private sealed class Stamp
    {
        private readonly uint[] _p;
        public int W { get; }
        public int H { get; }
        private Stamp(int w, int h, uint[] p) { W = w; H = h; _p = p; }

        public static Stamp Paint(int w, int h, Action<Graphics> paint, int rimSide = 0, int rimPx = 0, Color rim = default, float rimAmount = 0f)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                paint(g);
            }
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            uint[] px;
            try
            {
                var raw = new int[w * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                px = new uint[raw.Length];
                Buffer.BlockCopy(raw, 0, px, 0, raw.Length * 4);
            }
            finally { bmp.UnlockBits(data); }

            if (rimPx > 0 && rimSide != 0)
            {
                // A solid pixel with open air within rimPx pixels on the lit side is on the lit edge: warm it.
                var src = (uint[])px.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        uint c = src[y * w + x];
                        if ((c >> 24) < 235) continue;
                        for (int k = 1; k <= rimPx; k++)
                        {
                            int nx = x + rimSide * k;
                            if (nx >= 0 && nx < w && (src[y * w + nx] >> 24) >= 200) continue;
                            float f = rimAmount * (1f - (k - 1f) / rimPx);
                            int r = (int)(((c >> 16) & 0xFF) + (rim.R - ((c >> 16) & 0xFF)) * f);
                            int gg = (int)(((c >> 8) & 0xFF) + (rim.G - ((c >> 8) & 0xFF)) * f);
                            int b = (int)((c & 0xFF) + (rim.B - (c & 0xFF)) * f);
                            px[y * w + x] = (c & 0xFF000000u) | (uint)(r << 16) | (uint)(gg << 8) | (uint)b;
                            break;
                        }
                    }
            }
            return new Stamp(w, h, px);
        }

        /// <summary>
        /// Stamps with the CENTER at (cx, cy). mirror = flipped left to right;
        /// xScale (0 to 1) squeezes the picture narrower about its middle.
        /// </summary>
        public void Draw(FrameBuffer fb, float cx, float cy, float opacity, bool mirror, bool[]? only, float xScale = 1f)
        {
            if (opacity <= 0) return;
            int dw = Math.Max(1, (int)(W * xScale));
            int left = (int)(cx - dw / 2f), top = (int)(cy - H / 2f);
            int o256 = (int)(Math.Min(1f, opacity) * 256);
            uint[] frame = fb.Pixels;
            for (int sy = Math.Max(0, -top); sy < Math.Min(H, fb.Height - top); sy++)
                for (int dx = Math.Max(0, -left); dx < Math.Min(dw, fb.Width - left); dx++)
                {
                    int sx = dw == W ? dx : Math.Min(W - 1, (int)((dx + 0.5f) * W / dw));
                    uint c = _p[sy * W + (mirror ? W - 1 - sx : sx)];
                    int a = (int)(c >> 24) * o256 >> 8;
                    if (a == 0) continue;
                    int at = (top + sy) * fb.Width + left + dx;
                    if (only != null && !only[at]) continue;
                    uint bg = frame[at];
                    int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                    int r = br + (((int)((c >> 16) & 0xFF) - br) * a >> 8);
                    int g = bgn + (((int)((c >> 8) & 0xFF) - bgn) * a >> 8);
                    int b = bb + (((int)(c & 0xFF) - bb) * a >> 8);
                    frame[at] = (uint)((r << 16) | (g << 8) | b);
                }
        }
    }
}
