using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A grey squirrel bounds along the fence's upper rail with an acorn in its
/// mouth, hops up onto a post top, sits up, takes the acorn in its paws and
/// nibbles it (tail curled up behind and flicking, a glance up now and
/// then), then hops round, drops back onto the rail and bounds off the way
/// it came. It stays out of the stretch of fence by the scarecrow and the
/// corn shock, where the rail is busy: it only ever visits the left and
/// right ends of the fence, coming in from the nearer screen edge and going
/// back out of it.
///
/// FEYNMAN VERSION: a flip-book, with the pages picked by DISTANCE while it
/// runs. A squirrel does not run like a dog. It BOUNDS: the whole body
/// leaps in an arc, stretched out in the air, then gathers up (back arched,
/// hind feet reaching forward under the belly) and lands, again and again.
/// So one bound is one full turn of the flip-book (16 pages). The page is
/// worked out from how far it has gone, so the legs are always in step with
/// the ground: slow down and the bounds slow with it. The constructor
/// squeezes the bound length a touch so a WHOLE number of bounds fit
/// between the screen edge and the post, which makes the last bound end
/// exactly on the post top (the same trick the turkey uses to stop).
///
/// Each limb is two bones and a "knee" worked out by a tiny sum (two-bone
/// IK) so a foot placed on the rail always has a leg that reaches it. The
/// tail is a chain of segments, each turned a bit more than the one before,
/// so it curves and ripples; the sitting tail is the same chain with a
/// bigger curl.
///
/// In front of the fence (layer 1, no stencil). Facing left is the same
/// sticker turned over.
/// </summary>
internal sealed class Squirrel : Happening
{
    private const int RunPages = 16, RisePages = 5, SitTail = 4, SitNib = 4;
    private const float Speed = 0.27f;           // running speed, U per second
    private const float RiseFor = 0.5f, SitFor = 4.6f, TurnFor = 0.4f;
    private const float Contact = 0.2f;          // the share of a bound the feet are on the rail

    private static readonly Color Fur = Color.FromArgb(146, 140, 148);
    private static readonly Color FurDark = Color.FromArgb(104, 98, 108);
    private static readonly Color Silver = Color.FromArgb(206, 202, 210);
    private static readonly Color Belly = Color.FromArgb(226, 220, 208);
    private static readonly Color Ink = Color.FromArgb(70, 60, 68);
    private static readonly Color Limbs = Color.FromArgb(120, 114, 122);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;
    private readonly int _w, _h, _ox, _by;       // sheet size, the body origin's x in it, and the rail line's y in it
    private readonly Sprite[] _run = new Sprite[RunPages];
    private readonly Sprite[] _rise = new Sprite[RisePages];
    private readonly Sprite[,] _sit = new Sprite[SitTail, SitNib];
    private readonly Sprite[] _look = new Sprite[SitTail];
    private readonly List<int> _cand = new();    // posts it may stop on

    private int _dir = 1, _post;
    private float _stopX, _topRise, _bound, _dist, _t1, _t2;
    private float _total = 9f;

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "fence";
    public override bool CanBegin => _cand.Count > 0;

    public Squirrel(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(9f, s.U * 0.055f);
        _w = (int)(2.55f * _k) + 6;
        _h = (int)(1.9f * _k) + 6;
        _ox = (int)(1.75f * _k) + 3;
        _by = _h - 4;

        for (int i = 0; i < s.FencePosts.Length; i++)
        {
            float f = s.FencePosts[i] / s.Width;
            if ((f > 0.08f && f < 0.32f) || (f > 0.64f && f < 0.92f)) _cand.Add(i);
        }

        for (int i = 0; i < RunPages; i++) _run[i] = Page(i / (float)RunPages, 0f, 0f, 0, false);
        float[] rise = [0.2f, 0.45f, 0.7f, 0.9f, 1f];
        for (int i = 0; i < RisePages; i++) _rise[i] = Page(0.97f, rise[i], 0f, 0, false);
        float[] nib = [0f, 0.5f, 1f, 0.5f];
        for (int a = 0; a < SitTail; a++)
        {
            for (int b = 0; b < SitNib; b++) _sit[a, b] = Page(0.97f, 1f, nib[b], a, false);
            _look[a] = Page(0.97f, 1f, 0f, a, true);
        }
    }

    // ----------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        if (_cand.Count == 0) { _total = 0.1f; return; }
        _post = _cand[rng.Next(_cand.Count)];
        _stopX = _s.FencePosts[_post];
        _dir = _stopX < _s.Width / 2f ? 1 : -1;                    // it comes from the nearer edge, and goes back to it
        float start = _dir > 0 ? -1.9f * _k : _s.Width + 1.9f * _k;
        _dist = Math.Abs(_stopX - start);
        int bounds = Math.Max(2, (int)MathF.Round(_dist / (2.2f * _k)));
        _bound = _dist / bounds;                                    // a whole number of bounds
        _topRise = 0.30f * _s.FencePostHeight;                      // the post top stands this far over the upper rail
        _t1 = _dist / (Speed * _s.U);
        _t2 = _t1;
        _total = _t1 + RiseFor + SitFor + TurnFor + _t2;
    }

    // ----------------------------------------------------------------- draw

    /// <summary>The upper rail's height at x (the painter's own curve: straight between posts with a small sag).</summary>
    private float RailAt(float x)
    {
        float[] p = _s.FencePosts;
        if (x <= p[0] || x >= p[^1]) return _s.RailY(x, 1);
        int i = 0;
        while (i + 2 < p.Length && p[i + 1] <= x) i++;
        float f = (x - p[i]) / (p[i + 1] - p[i]);
        return _s.RailY(p[i], 1) + (_s.RailY(p[i + 1], 1) - _s.RailY(p[i], 1)) * f + _s.U * 0.004f * 4 * f * (1 - f) * 0.5f;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_cand.Count == 0) return;
        float stopAt = _t1;                                          // on the post top from here
        float sitAt = stopAt + RiseFor, turnAt = sitAt + SitFor, outAt = turnAt + TurnFor;
        float foot = 1.5f;                                           // feet a hair into the rail, so they stand ON it
        bool mirror;
        float x, y;
        Sprite page;

        if (t < stopAt)
        {
            float dist = _dist * (t / _t1);
            mirror = _dir < 0;
            x = _dir > 0 ? _stopX - _dist + dist : _stopX + _dist - dist;
            y = RailAt(x) - foot;
            // The last bound lands UP on the post top.
            float last = Smooth(((dist - (_dist - _bound)) / _bound - 0.4f) / 0.6f);
            y -= _topRise * last;
            float cyc = dist / _bound;
            page = _run[(int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages];
        }
        else
        {
            x = _stopX;
            float top = RailAt(x) - foot - _topRise;
            y = top;
            mirror = _dir < 0;
            if (t < sitAt) page = _rise[Math.Min(RisePages - 1, (int)((t - stopAt) / RiseFor * RisePages))];
            else if (t < turnAt)
            {
                float u = t - sitAt;
                // A tail flick every 1.5 s (a quick swish through four poses and back), a nibble burst, a glance up near the end.
                float ft = u % 1.5f;
                int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                bool glance = u > 3.0f && u < 3.7f;
                if (glance) page = _look[tail];
                else
                {
                    float nt = u % 1.1f;
                    int nb = nt < 0.7f ? (int)(nt * 7) % SitNib : 0;   // chew about seven times a second, then pause
                    page = _sit[tail, nb];
                }
            }
            else if (t < outAt)
            {
                // Turn round with a little hop: up, flip at the top, down. The gathered flight page shows the body tucked.
                float s = (t - turnAt) / TurnFor;
                y = top - 4f * s * (1 - s) * 0.5f * _k;
                mirror = s < 0.5f ? _dir < 0 : _dir > 0;
                page = _run[8];
            }
            else
            {
                float dist = _dist * ((t - outAt) / _t2);
                mirror = _dir > 0;                                   // now facing the way it came from
                x = _dir > 0 ? _stopX - dist : _stopX + dist;
                y = RailAt(x) - foot - _topRise * (1 - Smooth(dist / (_bound * 0.6f)));
                float cyc = dist / _bound;
                page = _run[(int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages];
            }
        }
        int left = (int)MathF.Round(x) - (mirror ? _w - _ox : _ox);
        page.Draw(fb, left, (int)MathF.Round(y) - _by, 1f, null, mirror);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>
    /// One page. phase: where in a bound (0 = touching down). sit: 0 = running, 1 = sitting up (in between = rising).
    /// nib: how far the mouth has chewed (0 to 1). tail: which of four tail-flick poses. look: head tipped up.
    /// </summary>
    private Sprite Page(float phase, float sit, float nib, int tail, bool look) =>
        Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);                              // bird... squirrel units, y UP from the rail
            Squirrel_(g, phase, sit, nib, tail, look, _bound / _k);
        });

    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static PointF Lerp(PointF a, PointF b, float t) => P(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    private static void Squirrel_(Graphics g, float phase, float sit, float nib, int tail, bool look, float bound)
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
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }
        void Ell2(PointF c, float rx, float ry, Color col, float tilt = 0) => Ell(c.X, c.Y, rx, ry, col, tilt);

        float e = Smooth(sit);                                       // 0 running, 1 sitting up

        // ---- where is the body? ----
        // Running: the body leaps in an arc (the lift), stretched in the air's
        // start and end and gathered in its middle.
        float c = Contact;
        float lift = phase < c ? 0 : 0.20f * MathF.Sin(MathF.PI * (phase - c) / (1 - c));
        float s = phase < c ? 1f : MathF.Cos(MathF.PI * (phase - c) / 0.4f);      // +1 stretched, -1 gathered
        float gather = (1 - s) / 2;
        PointF hipRun = P(-0.30f + 0.26f * gather, 0.31f + lift);
        PointF chestRun = P(0.26f, 0.30f + lift - 0.02f * gather);
        PointF hipSit = P(-0.12f, 0.24f);
        PointF chestSit = P(hipSit.X + 0.60f * MathF.Cos(77f * MathF.PI / 180f), hipSit.Y + 0.60f * MathF.Sin(77f * MathF.PI / 180f));
        PointF hip = Lerp(hipRun, hipSit, e), chest = Lerp(chestRun, chestSit, e);
        PointF mid = P((hip.X + chest.X) / 2, (hip.Y + chest.Y) / 2);
        float arch = Lerp(0.07f * gather + 0.02f, 0.0f, e);
        mid = P(mid.X - arch * 0.3f, mid.Y + arch);                  // the back humps up when the body gathers

        float nibDip = 0.034f * nib * e;
        float lookUp = look ? 1f : 0f;
        PointF head = P(chest.X + Lerp(0.29f, 0.07f, e) + 0.03f * lookUp, chest.Y + Lerp(0.07f, 0.22f, e) - nibDip + 0.04f * lookUp);

        // ---- the tail first, behind the body ----
        {
            float baseAng = Lerp(165f, 118f, e) + 8f * lookUp;
            float curl = Lerp(-30f, -112f, e);
            float flick = new[] { 0f, 14f, 30f, 14f }[tail] * e;
            PointF root = P(hip.X - 0.10f, hip.Y + 0.06f);
            const int N = 9;
            float segLen = 1.12f / N;
            var spine = new PointF[N + 1];
            spine[0] = root;
            float ang = baseAng;
            for (int i = 0; i < N; i++)
            {
                float q = (i + 0.5f) / N;
                // The run's ripple (a wave along the tail, travelling back as the bound goes on) and the sitting flick.
                float wave = (1 - e) * 16f * MathF.Sin(MathF.Tau * phase - i * 0.55f) + flick * q;
                ang = baseAng + curl * q + wave;
                float r = ang * MathF.PI / 180f;
                spine[i + 1] = P(spine[i].X + segLen * MathF.Cos(r), spine[i].Y + segLen * MathF.Sin(r));
            }
            PointF[] Outline(float scale)
            {
                var pts = new PointF[(N + 1) * 2];
                for (int i = 0; i <= N; i++)
                {
                    float q = i / (float)N;
                    float w = (0.045f + 0.150f * MathF.Pow(MathF.Sin(MathF.PI * Math.Min(1f, 0.18f + 0.80f * q)), 0.8f) * (1 - 0.25f * q)) * scale;
                    PointF a = spine[Math.Max(0, i - 1)], b = spine[Math.Min(N, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float nx = -dy / len, ny = dx / len;
                    pts[i] = P(spine[i].X + nx * w, spine[i].Y + ny * w);
                    pts[2 * N + 1 - i] = P(spine[i].X - nx * w, spine[i].Y - ny * w);
                }
                return pts;
            }
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve(Outline(1f), 0.45f);
                using var b = new SolidBrush(Silver);
                g.FillPath(b, path);
                using var pen = new Pen(Color.FromArgb(150, Ink), 0.018f) { LineJoin = LineJoin.Round };
                g.DrawPath(pen, path);
            }
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve(Outline(0.58f), 0.45f);
                using var b = new SolidBrush(Color.FromArgb(200, FurDark));
                g.FillPath(b, path);
            }
        }

        // ---- the legs: each two bones, the foot placed, the knee worked out ----
        // Front: elbow points back. Hind: knee points forward.
        void Leg(PointF joint, PointF foot, bool hind, Color col, float thick)
        {
            const float L = 0.21f;
            float dx = foot.X - joint.X, dy = foot.Y - joint.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * L - 0.003f);
            float al = MathF.Acos(Math.Clamp(d / (2 * L), -1f, 1f));
            float th = MathF.Atan2(dy, dx) + (hind ? al : -al);
            PointF knee = P(joint.X + L * MathF.Cos(th), joint.Y + L * MathF.Sin(th));
            // If the foot is out of reach, the lower bone points at the foot anyway.
            PointF end = P(knee.X + (foot.X - knee.X) * 0.999f, knee.Y + (foot.Y - knee.Y) * 0.999f);
            Limb(joint, knee, thick, col);
            Limb(knee, end, thick * 0.78f, col);
            Ell(end.X + 0.03f, end.Y + 0.005f, 0.065f, 0.035f, col);   // the paw
        }

        // Where the feet go. In a bound each pair spends the "contact" share of the cycle
        // sliding back along the rail, then swings forward through the air.
        float slide = Contact * bound;                                // how far the body travels while a foot is down (squirrel units)
        PointF FrontFoot()
        {
            if (phase < c) return P(0.52f - slide * (phase / c), 0f);
            float q = (phase - c) / (1 - c);
            return P(Lerp(0.52f - slide, 0.52f, Smooth(q)), 0.34f * MathF.Pow(MathF.Sin(MathF.PI * q), 0.8f));
        }
        PointF HindFoot()
        {
            float p = (phase + 0.12f) % 1f;                           // the hind feet land a little after the front ones
            float hx0 = -0.02f;
            if (p < c) return P(hx0 - slide * (p / c) - 0.10f * gather, 0f);
            float q = (p - c) / (1 - c);
            return P(Lerp(hx0 - slide, hx0, Smooth(q)), 0.30f * MathF.Pow(MathF.Sin(MathF.PI * q), 0.8f));
        }
        PointF frontRun = FrontFoot(), hindRun = HindFoot();
        PointF acornAt = P(head.X + 0.13f, head.Y - 0.17f);           // the nut held at the chin
        PointF pawL = P(acornAt.X - 0.07f, acornAt.Y - 0.03f), pawR = P(acornAt.X + 0.06f, acornAt.Y - 0.03f);
        PointF hindSit = P(hipSit.X + 0.12f, 0.03f);
        PointF shoulder = P(chest.X - 0.02f, chest.Y - 0.08f), haunch = P(hip.X + 0.03f, hip.Y - 0.08f);

        // The far legs first (darker), a half beat behind the near ones.
        PointF farFront = Lerp(P(frontRun.X - 0.12f, Math.Max(0f, frontRun.Y)), pawR, e);
        PointF farHind = Lerp(P(hindRun.X + 0.10f, hindRun.Y), P(hindSit.X - 0.05f, 0.02f), e);
        Leg(P(shoulder.X + 0.04f, shoulder.Y), farFront, false, FurDark, 0.075f);
        Leg(P(haunch.X + 0.03f, haunch.Y), farHind, true, FurDark, 0.09f);

        // ---- the body: a hip, a chest and a hump between, with a cream belly ----
        float tilt = MathF.Atan2(chest.Y - hip.Y, chest.X - hip.X) * 180f / MathF.PI;
        Ell2(hip, 0.23f, 0.21f, Fur);
        Ell2(mid, 0.27f, 0.19f, Fur, tilt);
        Ell2(chest, 0.20f, 0.17f, Fur);
        Ell2(P(mid.X + 0.07f, mid.Y - 0.05f), 0.22f, 0.12f, Color.FromArgb(150, Belly), tilt);          // the pale belly, lower and toward the front
        Ell2(P(chest.X + 0.04f, chest.Y - 0.05f), 0.12f, 0.12f, Color.FromArgb(220, Belly));           // the cream chest
        using (var dorsal = new Pen(Color.FromArgb(120, FurDark), 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(dorsal, [P(hip.X - 0.08f, hip.Y + 0.15f), P(mid.X, mid.Y + 0.15f), P(chest.X - 0.05f, chest.Y + 0.13f)], 0.5f);   // a darker stripe down the back

        // ---- the head ----
        float headTilt = Lerp(-4f, 8f, e) + 14f * (look ? 1 : 0) + 6f * gather * (1 - e);
        {
            var st = g.Save();
            g.TranslateTransform(head.X, head.Y);
            g.RotateTransform(headTilt);
            Ell(-0.05f, 0.14f, 0.06f, 0.095f, Fur, -10f);                       // the ear
            Ell(-0.05f, 0.13f, 0.03f, 0.06f, Color.FromArgb(200, 168, 150, 150), -10f);
            using (var tuft = new Pen(FurDark, 0.02f) { StartCap = LineCap.Round }) g.DrawLine(tuft, -0.055f, 0.2f, -0.07f, 0.265f);
            Ell(0f, 0f, 0.185f, 0.165f, Fur);                                  // a big round head: the cute part
            Ell(0.10f, -0.05f, 0.115f, 0.09f, Color.FromArgb(235, 206, 200, 196)); // the paler muzzle and cheek
            Ell(0.19f, -0.015f, 0.032f, 0.026f, Color.FromArgb(40, 30, 34));      // the nose
            // the mouth: open a little as it chews
            using (var mouth = new Pen(Ink, 0.016f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(mouth, 0.12f, -0.08f - 0.012f * nib, 0.17f, -0.055f);
            Ell(0.075f, 0.045f, 0.045f, 0.047f, Color.FromArgb(20, 16, 20));      // the eye, big and round
            Ell(0.088f, 0.06f, 0.014f, 0.014f, Color.White);                      // its glint
            using (var wh = new Pen(Color.FromArgb(150, 230, 226, 230), 0.008f))
                for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.17f, -0.04f, 0.30f, -0.03f + i * 0.04f);
            g.Restore(st);
        }

        // ---- the acorn: in the mouth while running, in the paws when sitting ----
        void Acorn(PointF at, float angDeg)
        {
            var st = g.Save();
            g.TranslateTransform(at.X, at.Y);
            g.RotateTransform(angDeg);
            Ell(0f, 0f, 0.075f, 0.058f, Color.FromArgb(150, 98, 46));
            Ell(-0.018f, 0.012f, 0.03f, 0.02f, Color.FromArgb(200, 180, 128, 70));
            using (var cap = new SolidBrush(Color.FromArgb(94, 64, 36)))
                g.FillPie(cap, -0.058f, -0.07f, 0.116f, 0.14f, 70, 220);       // the scaly cap on its end
            g.Restore(st);
        }
        if (e < 0.45f) Acorn(P(head.X + 0.23f, head.Y - 0.075f), 8f);
        else Acorn(P(acornAt.X + 0.01f, acornAt.Y + 0.02f - 0.01f * nib), 20f + 25f * nib);

        // ---- the near legs, in front of the body ----
        PointF nearFront = Lerp(frontRun, pawL, e);
        PointF nearHind = Lerp(hindRun, hindSit, e);
        if (e > 0.6f) Ell(hindSit.X + 0.10f, 0.03f, 0.15f, 0.05f, Fur);        // the long hind foot flat on the post
        Leg(shoulder, nearFront, false, Limbs, 0.08f);
        Leg(haunch, nearHind, true, Limbs, 0.095f);
        if (e > 0.45f)
        {
            Ell2(pawL, 0.04f, 0.035f, Limbs);                                    // the little hands round the nut
            Ell2(pawR, 0.04f, 0.035f, Limbs);
        }
    }
}
