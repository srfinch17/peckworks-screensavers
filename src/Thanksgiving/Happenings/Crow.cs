using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A crow flies in out of the sky, flares (wings thrown up, feet reaching
/// down) and lands on one end of the scarecrow's crossbar. It folds its
/// wings, looks about (the head swings to look over its shoulder and back),
/// caws three times (beak open, head thrown up, body leaning into it), hops
/// once, and flies off.
///
/// FEYNMAN VERSION: a flip-book again. Every pose is a pre-painted page:
///  - 16 pages of flapping flight,
///  - 6 pages of the landing flare (the same pages played backwards are the
///    take-off),
///  - 3 pages of the wings folding down,
///  - 5 pages of the head turning (profile, three quarter, front, and back),
///  - 5 pages of one caw (head thrown up, beak open),
///  - 1 crouch page for the moment before it springs.
/// All pages are painted at the same spot on the sheet: the middle of the
/// body. Perched pages are nudged inside the sheet so the FEET always land
/// on the same sheet pixel (otherwise a head turn would make the bird skid
/// on its perch, because the whole body is tilted about its middle).
///
/// The head is the one clever part: a "yaw" number from 1 (profile facing
/// right) through 0 (looking straight at us, two eyes, beak pointing at the
/// viewer) to -1 (profile facing left) squeezes the beak and slides the eyes,
/// so one drawing routine makes every head turn.
///
/// It is drawn only where the corner branches did NOT paint, so it passes
/// behind them and in front of everything else. Facing left is the same
/// sticker turned over (the sheet is square about the body's middle so the
/// turn-over stays put).
/// </summary>
internal sealed class Crow : Happening
{
    private const int FlapPages = 16, FlarePages = 6, FoldPages = 3, LookPages = 5, CawPages = 5;
    private const float FlareFor = 0.55f;       // the last stretch of the flight in, and the first of the flight out
    private const float FoldFor = 0.40f, LookFor = 1.7f, CawEvery = 0.80f, CawFor = 0.45f, SettleFor = 0.30f;
    private const float HopFor = 0.45f, PauseFor = 0.15f, CrouchFor = 0.15f;
    private const float FlapHz = 3.4f;
    private const float PerchPitch = 36f;       // degrees the perched body is tilted head up

    private static readonly Color Black = Color.FromArgb(38, 36, 50);
    private static readonly Color BlackDeep = Color.FromArgb(24, 22, 32);
    private static readonly Color WingCol = Color.FromArgb(32, 30, 44);
    private static readonly Color Gloss = Color.FromArgb(120, 128, 176);
    private static readonly Color Rim = Color.FromArgb(226, 150, 108);
    private static readonly Color BeakCol = Color.FromArgb(58, 54, 60);
    private static readonly Color LegCol = Color.FromArgb(86, 78, 82);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;
    private readonly int _w;                     // the sheet is square: _w by _w, the body's middle at its center
    private readonly PointF _f0;                 // where the feet are from the body's middle when perched, in bird units (y up)
    private readonly Sprite[] _flap = new Sprite[FlapPages], _flare = new Sprite[FlarePages], _fold = new Sprite[FoldPages];
    private readonly Sprite[] _look = new Sprite[LookPages], _caw = new Sprite[CawPages];
    private readonly Sprite _folded, _crouch;

    private readonly PointF[] _perch = new PointF[2];     // where the feet go on each end of the bar
    private readonly bool[] _ok = new bool[2];

    private int _dir = 1, _arm;
    private PointF _centerEnd;                   // where the body's middle is when perched
    private PointF _a, _c, _b;                   // the way in: start, pull point, landing
    private PointF _xa, _xc, _xb;                // the way out
    private float _tin, _tout, _exitAt = 1f;
    private float _total = 10f;
    private float _tLand, _tFold, _tLook, _tCaw, _tSettle, _tHop, _tPause, _tCrouch, _tTake;

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "scarecrow";
    public override bool CanBegin => _ok[0] || _ok[1];

    public Crow(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(9f, s.U * 0.06f);
        _w = (int)(2.9f * _k) + 8;
        _f0 = FeetOff(PerchPitch, 1f);

        // The two perches: just in from the end of the crossbar, on top of it (half the bar's thickness up).
        float bar = Math.Max(2f, s.ScarecrowHeight * 0.035f) / 2f;
        _perch[0] = new PointF(s.ScarecrowArmL.X + s.ScarecrowHeight * 0.03f, s.ScarecrowArmL.Y - bar);
        _perch[1] = new PointF(s.ScarecrowArmR.X - s.ScarecrowHeight * 0.03f, s.ScarecrowArmR.Y - bar);
        for (int i = 0; i < 2; i++)                           // a corner branch over the perch would cut the bird: skip such an end
        {
            int open = 0, all = 0;
            for (int dy = (int)(-1.4f * _k); dy <= (int)(0.3f * _k); dy += 3)
                for (int dx = (int)(-1f * _k); dx <= (int)(1f * _k); dx += 3)
                {
                    int x = (int)_perch[i].X + dx, y = (int)_perch[i].Y + dy;
                    if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) continue;
                    all++;
                    if (s.OpenOfBranches[y * s.Width + x]) open++;
                }
            _ok[i] = all > 0 && open == all;
        }

        // ---- the pages ----
        for (int i = 0; i < FlapPages; i++)
            _flap[i] = Page(-4f, 12f + 72f * MathF.Cos(MathF.Tau * i / FlapPages), 1f, 0f, 1f, 0f, 0f, 0f, false, false);
        float[] flareWing = [-20f, 55f, 82f, 20f, 78f, 70f];
        for (int i = 0; i < FlarePages; i++)
        {
            float q = i / (FlarePages - 1f);
            float e = q * q * (3 - 2 * q);
            _flare[i] = Page(-4f + (PerchPitch + 4f) * e, flareWing[i], 1f, e, 1f, 0f, 0f, 0.3f + 0.4f * e, false, false);
        }
        float[] foldWing = [60f, 25f, -15f];
        float[] foldLen = [1f, 0.85f, 0.7f];
        for (int i = 0; i < FoldPages; i++)
            _fold[i] = Page(PerchPitch, foldWing[i], foldLen[i], 1f, 1f, 0f, 0f, 0f, false, true);
        _folded = Page(PerchPitch, 0, 1f, 1f, 1f, 0f, 0f, 0f, true, true);
        float[] yaws = [1f, 0.5f, 0f, -0.5f, -1f];
        for (int i = 0; i < LookPages; i++) _look[i] = Page(PerchPitch, 0, 1f, 1f, yaws[i], 0f, 0f, 0f, true, true);
        (float Up, float Beak, float Pitch)[] caw = [(0f, 0f, 36f), (0.5f, 0.35f, 33f), (1f, 1f, 27f), (0.7f, 0.6f, 30f), (0.3f, 0.1f, 34f)];
        for (int i = 0; i < CawPages; i++) _caw[i] = Page(caw[i].Pitch, 0, 1f, 1f, 1f, caw[i].Up, caw[i].Beak, 0f, true, true);
        _crouch = Page(20f, 0, 1f, 1f, 1f, 0f, 0f, 0f, true, true);
    }

    // ---------------------------------------------------------------- the dice

    public override void Begin(Random rng)
    {
        _arm = _ok[0] && _ok[1] ? rng.Next(2) : (_ok[0] ? 0 : 1);
        _dir = rng.Next(2) == 0 ? 1 : -1;
        float U = _s.U, W = _s.Width, H = _s.Height, k = _k;
        _centerEnd = new PointF(_perch[_arm].X - _dir * _f0.X * k, _perch[_arm].Y + _f0.Y * k);

        // The way in: it comes from up and behind, heading the way it will face.
        float dist = U * (0.75f + 0.30f * rng.NextSingle());
        _a = new PointF(_centerEnd.X - _dir * dist, -3f * k);
        float ang = 22f * MathF.PI / 180f;
        float l2 = 0.42f * MathF.Sqrt((_a.X - _centerEnd.X) * (_a.X - _centerEnd.X) + (_a.Y - _centerEnd.Y) * (_a.Y - _centerEnd.Y));
        _b = _centerEnd;
        _c = new PointF(_b.X - _dir * MathF.Cos(ang) * l2, _b.Y - MathF.Sin(ang) * l2);
        _tin = Math.Clamp(Length(_a, _c, _b) / (0.26f * U), 2.4f, 4.4f);

        // The way out: up and across, off the edge.
        bool top = rng.NextDouble() < 0.5;
        _xa = _centerEnd;
        _xb = top ? new PointF(_xa.X + _dir * 0.35f * W, -3f * k)
                  : new PointF(_dir > 0 ? W + 3f * k : -3f * k, _xa.Y - (0.35f + 0.2f * rng.NextSingle()) * H);
        _xc = new PointF(_xa.X + _dir * Math.Abs(_xb.X - _xa.X) * 0.30f, _xa.Y + (_xb.Y - _xa.Y) * 0.75f);
        _tout = Math.Clamp(Length(_xa, _xc, _xb) / (0.30f * U), 2.4f, 5f);
        _exitAt = 1f;
        for (int i = 4; i <= 400; i++)
        {
            float f = i / 400f;
            PointF p = Bez(_xa, _xc, _xb, OutEase(f));
            if (p.X < -1.8f * k || p.X > W + 1.8f * k || p.Y < -1.8f * k) { _exitAt = f; break; }
        }

        // The timetable.
        _tLand = _tin;
        _tFold = _tLand;
        _tLook = _tFold + FoldFor;
        _tCaw = _tLook + LookFor;
        _tSettle = _tCaw + 3 * CawEvery;
        _tHop = _tSettle + SettleFor;
        _tPause = _tHop + HopFor;
        _tCrouch = _tPause + PauseFor;
        _tTake = _tCrouch + CrouchFor;
        _total = _tTake + _tout * _exitAt + 0.1f;
    }

    private static float OutEase(float f) => MathF.Pow(f, 1.4f);                 // it picks up speed as it goes
    private static float InEase(float f) => 1f - MathF.Pow(1f - f, 1.7f);        // and slows into the landing

    private static PointF Bez(PointF a, PointF c, PointF b, float u) => new((1 - u) * (1 - u) * a.X + 2 * (1 - u) * u * c.X + u * u * b.X,
                                                                              (1 - u) * (1 - u) * a.Y + 2 * (1 - u) * u * c.Y + u * u * b.Y);

    private static float Length(PointF a, PointF c, PointF b)
    {
        float len = 0;
        PointF prev = a;
        for (int i = 1; i <= 48; i++)
        {
            PointF p = Bez(a, c, b, i / 48f);
            len += MathF.Sqrt((p.X - prev.X) * (p.X - prev.X) + (p.Y - prev.Y) * (p.Y - prev.Y));
            prev = p;
        }
        return len;
    }

    // ------------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        bool mirror = _dir < 0;
        void Stamp(Sprite sp, PointF center, float opacity = 1f) =>
            sp.Draw(fb, (int)MathF.Round(center.X) - _w / 2, (int)MathF.Round(center.Y) - _w / 2, opacity, _s.OpenOfBranches, mirror);

        // ---- the flight in ----
        if (t < _tin)
        {
            PointF p = Bez(_a, _c, _b, InEase(t / _tin));
            float fl = (t - (_tin - FlareFor)) / FlareFor;
            Sprite pg = fl < 0 ? _flap[(int)(t * FlapHz * FlapPages) % FlapPages]
                               : _flare[Math.Min(FlarePages - 1, (int)(fl * FlarePages))];
            Stamp(pg, p);
            return;
        }

        PointF rest = _centerEnd;
        // ---- on the bar ----
        if (t < _tTake)
        {
            Sprite pg;
            PointF at = rest;
            if (t < _tLook) pg = _fold[Math.Min(FoldPages - 1, (int)((t - _tFold) / FoldFor * FoldPages))];
            else if (t < _tCaw)
            {
                // The head swings round to look over the shoulder, and back: a run of steps through the five head pages.
                int[] seq = [0, 0, 1, 2, 3, 4, 4, 4, 3, 2, 1, 0, 0];
                pg = _look[seq[Math.Min(seq.Length - 1, (int)((t - _tLook) / LookFor * seq.Length))]];
            }
            else if (t < _tSettle)
            {
                float q = (t - _tCaw) % CawEvery;
                int[] seq = [0, 1, 2, 2, 3, 4];
                pg = q < CawFor ? _caw[seq[Math.Min(seq.Length - 1, (int)(q / CawFor * seq.Length))]] : _look[0];
            }
            else if (t < _tHop) pg = _look[0];
            else if (t < _tPause)
            {
                // One hop in place: wings flicked half open, up and back down.
                float s = (t - _tHop) / HopFor;
                at = new PointF(rest.X, rest.Y - 4f * s * (1 - s) * 0.45f * _k);
                pg = s < 0.2f ? _fold[1] : s < 0.7f ? _fold[0] : s < 0.9f ? _fold[1] : _folded;
            }
            else if (t < _tCrouch) pg = _folded;
            else pg = _crouch;
            Stamp(pg, at);
            return;
        }

        // ---- the flight out ----
        float te = t - _tTake;
        float f = Math.Min(1f, te / _tout);
        PointF q2 = Bez(_xa, _xc, _xb, OutEase(f));
        Sprite page = te < FlareFor ? _flare[FlarePages - 1 - Math.Min(FlarePages - 1, (int)(te / FlareFor * FlarePages))]
                                    : _flap[(int)(te * FlapHz * FlapPages) % FlapPages];
        Stamp(page, q2);
    }

    // ------------------------------------------------------------------ paint

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>Where the feet are from the body's middle (y up) when the body is tilted "pitch" degrees head up.</summary>
    private static PointF FeetOff(float pitch, float legs)
    {
        float lx = -0.12f + 0.14f * legs, ly = -0.17f - 0.13f * legs;     // tucked up (flying) to hanging down (landing, perched)
        float r = pitch * MathF.PI / 180f;
        return P(lx * MathF.Cos(r) - ly * MathF.Sin(r), lx * MathF.Sin(r) + ly * MathF.Cos(r));
    }

    private Sprite Page(float pitch, float wing, float wlen, float legs, float yaw, float headUp, float beak, float fan, bool folded, bool perched) =>
        Sprite.Paint(_w, _w, g =>
        {
            g.TranslateTransform(_w / 2f, _w / 2f);
            g.ScaleTransform(_k, -_k);                                    // bird units, y up, the body's middle at the sheet's middle
            // A perched pose nudges the whole bird so the feet stay on one fixed spot of the sheet.
            if (perched)
            {
                PointF now = FeetOff(pitch, legs);
                g.TranslateTransform(_f0.X - now.X, _f0.Y - now.Y);
            }
            g.RotateTransform(pitch);
            Crow_(g, wing, wlen, legs, yaw, headUp, beak, fan, folded);
        });

    private static void Crow_(Graphics g, float wing, float wlen, float legs, float yaw, float headUp, float beak, float fan, bool folded)
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
        void Blob(PointF[] pts, Color c)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, 0.5f);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }
        PointF[] Wing(PointF root, float ang, float len, float chord)
        {
            float r = ang * MathF.PI / 180f;
            PointF d = P(MathF.Cos(r), MathF.Sin(r));
            PointF n = P(-d.Y, d.X);
            if (n.X < 0) n = P(d.Y, -d.X);
            const int N = 10;
            var pts = new PointF[(N + 1) * 2];
            for (int i = 0; i <= N; i++)
            {
                float s = i / (float)N, bow = 0.05f * len * MathF.Sin(MathF.PI * s);
                pts[i] = P(root.X + d.X * s * len + n.X * bow, root.Y + d.Y * s * len + n.Y * bow);
            }
            for (int i = 0; i <= N; i++)
            {
                float s = 1 - i / (float)N, bow = 0.05f * len * MathF.Sin(MathF.PI * s);
                float c = chord * (0.25f + 0.75f * MathF.Pow(1 - s, 0.7f));
                if (s > 0.5f && i % 2 == 1) c *= 0.7f;               // the fingered tips
                pts[N + 1 + i] = P(root.X + d.X * s * len + n.X * bow - c, root.Y + d.Y * s * len + n.Y * bow);
            }
            return pts;
        }

        // ---- the tail, behind everything: a wedge, fanned a little in the flare ----
        float tw = 0.075f + 0.09f * fan;
        Poly([P(-0.24f, 0.07f), P(-0.84f, 0.015f + tw), P(-0.88f, 0.0f), P(-0.84f, -0.02f - tw), P(-0.24f, -0.07f)], BlackDeep);

        // ---- the far wing (flying only), then the legs ----
        if (!folded) Poly(Wing(P(0.04f, 0.10f), wing * 0.9f, 0.92f * wlen, 0.44f), Color.FromArgb(22, 20, 30));
        PointF hip = P(0.0f, -0.10f);
        PointF foot = P(-0.12f + 0.14f * legs, -0.17f - 0.13f * legs);
        Limb(hip, foot, 0.04f, LegCol);
        foreach (float dy in (float[])[-0.03f, 0.0f, 0.03f]) Limb(foot, P(foot.X + 0.09f, foot.Y + dy - 0.015f * legs), 0.022f, LegCol);
        Limb(foot, P(foot.X - 0.04f, foot.Y), 0.02f, LegCol);

        // ---- the body ----
        Ell(0f, 0f, 0.34f, 0.20f, Black, -4f);
        Ell(0.12f, -0.02f, 0.24f, 0.17f, Black, 0f);
        using (var gloss = new Pen(Color.FromArgb(90, Gloss), 0.035f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(gloss, [P(-0.22f, 0.11f), P(-0.02f, 0.15f), P(0.18f, 0.13f)], 0.5f);          // the glossy sheen along the back
        using (var rim = new Pen(Color.FromArgb(70, Rim), 0.018f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(rim, [P(-0.26f, 0.15f), P(-0.04f, 0.20f), P(0.16f, 0.185f)], 0.5f);           // the last of the sunset on its back

        // ---- the near wing: folded along the side, or spread ----
        if (folded)
        {
            Blob([P(0.16f, 0.08f), P(0.0f, 0.15f), P(-0.28f, 0.10f), P(-0.62f, -0.07f), P(-0.68f, -0.13f), P(-0.42f, -0.10f), P(-0.06f, -0.13f), P(0.15f, -0.03f)], WingCol);
            using var shine = new Pen(Color.FromArgb(110, Gloss), 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawCurve(shine, [P(0.06f, 0.10f), P(-0.2f, 0.09f), P(-0.5f, -0.04f)], 0.5f);
        }
        else
        {
            PointF[] w = Wing(P(0.06f, 0.10f), wing, wlen, 0.46f);
            Poly(w, WingCol);
            using var shine = new Pen(Color.FromArgb(110, Gloss), 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float r = wing * MathF.PI / 180f;
            g.DrawLine(shine, P(0.08f + 0.12f * MathF.Cos(r), 0.11f + 0.12f * MathF.Sin(r)), P(0.06f + 0.6f * wlen * MathF.Cos(r), 0.10f + 0.6f * wlen * MathF.Sin(r)));
        }

        // ---- the head: a neck, a round head, a beak, and a big round eye ----
        {
            PointF neck = P(0.20f, 0.10f);
            var st = g.Save();
            g.TranslateTransform(neck.X, neck.Y);
            g.RotateTransform(headUp * 32f);                                // thrown up for a caw
            g.TranslateTransform(-neck.X, -neck.Y);
            Ell(0.27f, 0.12f, 0.15f, 0.13f, Black);
            PointF hc = P(0.36f, 0.17f);
            Ell(hc.X, hc.Y, 0.155f, 0.15f, Black);
            using (var rim = new Pen(Color.FromArgb(60, Rim), 0.016f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(rim, hc.X - 0.15f, hc.Y - 0.145f, 0.30f, 0.29f, 200, 80);
            float ay = Math.Abs(yaw);
            float bx = hc.X + 0.09f * yaw;
            PointF tip = P(hc.X + 0.31f * yaw, hc.Y - 0.015f - 0.08f * (1 - ay));
            PointF mouth = P(bx, hc.Y + 0.005f);
            // Upper half of the beak, and the lower half dropped by the opening.
            Poly([P(bx, hc.Y + 0.065f), P(tip.X, tip.Y + 0.025f), mouth], BeakCol);
            float open = beak * 30f * MathF.PI / 180f * (yaw >= 0 ? -1 : 1);
            PointF lt = P(mouth.X + (tip.X - mouth.X) * 0.9f, mouth.Y + (tip.Y - mouth.Y) * 0.9f);
            float dxl = lt.X - mouth.X, dyl = lt.Y - mouth.Y;
            PointF low = P(mouth.X + dxl * MathF.Cos(open) - dyl * MathF.Sin(open), mouth.Y + dxl * MathF.Sin(open) + dyl * MathF.Cos(open));
            if (beak > 0.15f) Poly([mouth, P(tip.X, tip.Y + 0.02f), low], Color.FromArgb(190, 60, 66));     // the pink inside of the open beak
            Poly([mouth, low, P(bx, hc.Y - 0.05f)], BeakCol);
            // The eye (or two, looking at us): a pale ring, a dark pupil, and a tiny white glint.
            float e1 = hc.X + 0.065f * yaw - (1 - ay) * 0.075f, e2 = hc.X + 0.065f * yaw + (1 - ay) * 0.075f;
            foreach (float ex in (1 - ay) > 0.3f ? new[] { e1, e2 } : new[] { hc.X + 0.065f * yaw })
            {
                Ell(ex, hc.Y + 0.045f, 0.037f, 0.037f, Color.FromArgb(168, 138, 112));
                Ell(ex, hc.Y + 0.045f, 0.025f, 0.025f, Color.FromArgb(14, 12, 16));
                Ell(ex + 0.009f, hc.Y + 0.056f, 0.01f, 0.01f, Color.White);
            }
            g.Restore(st);
        }
    }
}
