using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A raccoon waddles in from the left along the near ground to the
/// cornucopia, stops with its nose over the fruit and sniffs, rears up on
/// its haunches, takes an apple in its paws, glances all about with that
/// guilty look (its masked face turns to us and back), startles into a little
/// hop that turns it round, drops to all fours with the apple in its mouth
/// and waddles back off to the left, the way it came.
///
/// FEYNMAN VERSION: a flip-book. While it walks, the page is picked by HOW
/// FAR it has gone (one full turn of 24 pages is one stride), so its feet
/// are always in step with the ground. Its four legs are two bones each;
/// each foot slides back along the ground for just over half a stride, then
/// swings forward, and a tiny sum (two-bone IK) bends the "knee" so the leg
/// always reaches. The four legs are a quarter of a stride apart, which is a
/// raccoon's walk: one foot at a time. The constructor squeezes the stride a
/// touch so a WHOLE number of strides fit into the walk up to the stop, as
/// the turkey does, so the feet are planted when it stops.
///
/// The head is the one routine that does every look: a "yaw" from 1 (profile,
/// facing right) through 0 (the whole masked face, two eyes, looking at the
/// viewer) to -1 (profile facing left), and a "nose" that tips the head down
/// to sniff. The sitting-up is the walking body blended toward a body stood
/// upright on its haunches: the same shapes, joined by a number between 0
/// and 1 ("rise"), so the pages in between are real in-between poses.
///
/// The apple in its paws is drawn right over the apple lying in the picture
/// (the picture's apple cannot be taken away, and there are other fruit
/// beside it, so one fewer is never missed). It stands on the walk line
/// (WalkY), nearer than the horn, so it is drawn IN FRONT of the horn: the
/// horn is behind its legs. Both directions are the same sticker turned
/// over (Sprite.Draw's mirror), so a leftward walk needs no second set.
/// </summary>
internal sealed class Raccoon : Happening
{
    private const int WalkPages = 24, RisePages = 6, YawPages = 5, SniffPages = 3;
    private const float Cruise = 0.12f;          // waddling speed, U per second
    private const float Ramp = 0.8f;             // seconds to slow into the stop, and to get going again
    private const float SniffFor = 1.7f, RiseFor = 0.8f, LookFor = 2.2f, HopFor = 0.30f, DropFor = 0.6f;
    private const float Stance = 0.62f;          // the share of a stride a foot is on the ground
    private static readonly PointF AppleRel = new(0.66f, 0.12f);   // where the picture's apple lies from the raccoon's origin, in raccoon units

    private static readonly Color Fur = Color.FromArgb(132, 122, 116);
    private static readonly Color FurDark = Color.FromArgb(84, 74, 72);
    private static readonly Color FurLight = Color.FromArgb(180, 170, 156);
    private static readonly Color Cream = Color.FromArgb(236, 230, 216);
    private static readonly Color Mask = Color.FromArgb(40, 34, 38);
    private static readonly Color Paw = Color.FromArgb(56, 48, 48);
    private static readonly Color PawFar = Color.FromArgb(40, 34, 36);
    private static readonly Color Ink = Color.FromArgb(60, 50, 50);
    private static readonly Color AppleRed = Color.FromArgb(206, 42, 38);

    private readonly ThanksgivingScenery _s;
    private readonly float _k;
    private readonly int _w, _h, _ox, _by;
    private readonly Sprite[] _walk = new Sprite[WalkPages], _walkApple = new Sprite[WalkPages];
    private readonly Sprite[] _sniff = new Sprite[SniffPages];
    private readonly Sprite _sniffA, _sniffB;
    private readonly Sprite[] _rise = new Sprite[RisePages], _drop = new Sprite[RisePages];
    private readonly Sprite[] _yaw = new Sprite[YawPages];

    private readonly float _total, _v0, _stride, _stopX, _startX, _tIn, _tOut, _a;

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "ground,cornucopia";

    public Raccoon(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(10f, s.U * 0.085f);
        _w = (int)(2.75f * _k) + 6;
        _h = (int)(1.45f * _k) + 6;
        _ox = (int)(1.55f * _k) + 3;
        _by = _h - 4;

        // ---- the timetable (the turkey's: full speed except while slowing, stopped, or getting going) ----
        float apple = s.CornucopiaMouth.X + 0.3f * s.CornucopiaLength * 0.22f;       // the picture's first apple
        _stopX = apple - AppleRel.X * _k;
        _startX = -1.7f * _k;
        float dist = _stopX - _startX;
        _v0 = Cruise * s.U;
        _a = dist / _v0 - Ramp / 2;                                                  // when the slowing starts
        _tIn = _a + Ramp;
        float wanted = 0.55f * _k;
        int strides = Math.Max(1, (int)MathF.Round(dist / wanted));
        _stride = dist / strides;
        _tOut = dist / _v0 + Ramp / 2;
        _total = _tIn + SniffFor + RiseFor + LookFor + HopFor + DropFor + _tOut;

        float st = _stride / _k;
        for (int i = 0; i < WalkPages; i++)
        {
            _walk[i] = Page(i / (float)WalkPages, 0f, 0f, 0f, 1f, 0, st);
            _walkApple[i] = Page(i / (float)WalkPages, 0f, 0f, 0f, 1f, 2, st);
        }
        float[] nose = [0.35f, 0.7f, 1f];
        for (int i = 0; i < SniffPages; i++) _sniff[i] = Page(0f, 0f, nose[i], 0f, 1f, 0, st);
        _sniffA = Page(0f, 0f, 1f, 1f, 1f, 0, st);                                   // the nose twitching, two poses
        _sniffB = Page(0f, 0f, 0.92f, -1f, 1f, 0, st);
        float[] rise = [0.08f, 0.25f, 0.45f, 0.65f, 0.85f, 1f];
        for (int i = 0; i < RisePages; i++)
        {
            _rise[i] = Page(0f, rise[i], Math.Max(0f, 1f - rise[i] * 2.2f), 0f, 1f, 1, st);
            _drop[i] = Page(0f, rise[i], 0f, 0f, rise[i] > 0.5f ? 0.3f : 1f, rise[i] >= 0.45f ? 1 : 2, st);   // the apple goes from paws to mouth on the way down
        }
        float[] yaws = [1f, 0.5f, 0f, -0.5f, -1f];
        for (int i = 0; i < YawPages; i++) _yaw[i] = Page(0f, 1f, 0f, 0f, yaws[i], 1, st);
    }

    // ----------------------------------------------------------------- timing

    /// <summary>How far, in pixels, it has gone from the start by time t (slows into the stop, waits, and the walk out starts again from the stop).</summary>
    private float WalkedIn(float t)
    {
        if (t <= _a) return _v0 * t;
        if (t >= _tIn) return _stopX - _startX;
        float x = (t - _a) / Ramp;
        return _v0 * _a + _v0 * Ramp * (x - (x * x * x - x * x * x * x / 2));   // the slowing: the smoothstep's integral is x^3 - x^4/2
    }

    private float WalkedOut(float t)
    {
        if (t <= 0) return 0;
        if (t < Ramp)
        {
            float x = t / Ramp;
            return _v0 * Ramp * (x * x * x - x * x * x * x / 2);                    // getting going
        }
        return _v0 * Ramp / 2 + _v0 * (t - Ramp);
    }

    // ------------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float tSniff = _tIn, tRise = tSniff + SniffFor, tLook = tRise + RiseFor, tHop = tLook + LookFor, tDrop = tHop + HopFor, tOut = tDrop + DropFor;
        float x = _stopX, lift = 0;
        bool mirror = false;
        Sprite page;

        if (t < _tIn)
        {
            float d = WalkedIn(t);
            x = _startX + d;
            float cyc = d / _stride;
            page = _walk[(int)MathF.Round((cyc - MathF.Floor(cyc)) * WalkPages) % WalkPages];
        }
        else if (t < tRise)
        {
            float u = t - tSniff;
            if (u < 0.4f) page = _sniff[Math.Min(SniffPages - 1, (int)(u / 0.4f * SniffPages))];
            else page = ((int)((u - 0.4f) * 8) & 1) == 0 && u < SniffFor - 0.2f ? _sniffA : (u < SniffFor - 0.2f ? _sniffB : _sniff[SniffPages - 1]);   // sniffs about eight times a second
        }
        else if (t < tLook) page = _rise[Math.Min(RisePages - 1, (int)((t - tRise) / RiseFor * RisePages))];
        else if (t < tHop)
        {
            // The guilty glance: the head swings round to look back over the shoulder, then at us, then forward again.
            int[] seq = [0, 0, 1, 2, 3, 4, 4, 3, 2, 1, 0, 2];
            page = _yaw[seq[Math.Min(seq.Length - 1, (int)((t - tLook) / LookFor * seq.Length))]];
        }
        else if (t < tDrop)
        {
            // Startled: a little hop that turns it round, looking at us as it goes.
            float s = (t - tHop) / HopFor;
            lift = 4f * s * (1 - s) * 0.35f * _k;
            mirror = s > 0.5f;
            page = _yaw[2];
        }
        else if (t < tOut)
        {
            mirror = true;
            page = _drop[RisePages - 1 - Math.Min(RisePages - 1, (int)((t - tDrop) / DropFor * RisePages))];
        }
        else
        {
            mirror = true;
            float d = WalkedOut(t - tOut);
            x = _stopX - d;
            float cyc = d / _stride;
            page = _walkApple[(int)MathF.Round((cyc - MathF.Floor(cyc)) * WalkPages) % WalkPages];
        }

        float y = _s.WalkY(x);
        int left = (int)MathF.Round(x) - (mirror ? _w - _ox : _ox);
        page.Draw(fb, left, (int)MathF.Round(y - lift) - _by, 1f, null, mirror);
    }

    // ------------------------------------------------------------------ paint

    private Sprite Page(float phase, float rise, float nose, float twitch, float yaw, int apple, float stride) =>
        Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);                                // raccoon units, y UP from the ground
            Raccoon_(g, phase, rise, nose, twitch, yaw, apple, stride);
        });

    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static PointF Lerp(PointF a, PointF b, float t) => P(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    private static void Raccoon_(Graphics g, float phase, float rise, float nose, float twitch, float yaw, int apple, float stride)
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

        float e = Smooth(rise);
        float bob = 0.014f * MathF.Sin(2 * MathF.Tau * phase + 0.6f) * (1 - e);          // the body rises and dips twice a stride (a waddle)
        PointF hipW = P(-0.30f, 0.33f + bob), chestW = P(0.24f, 0.29f - 0.5f * bob);
        PointF hipS = P(-0.30f, 0.17f);
        float ang = 74f * MathF.PI / 180f;
        PointF chestS = P(hipS.X + 0.62f * MathF.Cos(ang), hipS.Y + 0.62f * MathF.Sin(ang));
        PointF hip = Lerp(hipW, hipS, e), chest = Lerp(chestW, chestS, e);
        PointF mid = P((hip.X + chest.X) / 2, (hip.Y + chest.Y) / 2 + 0.03f * (1 - e));
        PointF head = P(chest.X + Lerp(0.38f, 0.10f, e) + 0.03f * nose * (1 - e), chest.Y + Lerp(-0.02f, 0.21f, e) - 0.13f * nose * (1 - e) + 0.012f * twitch);
        PointF shoulder = P(chest.X - 0.02f, chest.Y - 0.07f), haunch = P(hip.X + 0.02f, hip.Y - 0.10f);

        // ---- the tail, behind: a ringed bush of nine bands ending in black ----
        {
            const int N = 9;
            float baseAng = Lerp(188f, 204f, e), curl = Lerp(14f, -6f, e);
            float wave = (1 - e) * 5f * MathF.Sin(MathF.Tau * phase);
            PointF root = P(hip.X - 0.16f, hip.Y + 0.08f * (1 - e) - 0.04f * e);
            var spine = new PointF[N + 1];
            spine[0] = root;
            for (int i = 0; i < N; i++)
            {
                float q = (i + 0.5f) / N;
                float a = (baseAng + curl * q + wave * MathF.Sin(q * 4 - MathF.Tau * phase)) * MathF.PI / 180f;
                spine[i + 1] = P(spine[i].X + 0.085f * MathF.Cos(a), spine[i].Y + 0.085f * MathF.Sin(a));
            }
            float Wd(int i) => 0.075f + 0.050f * MathF.Sin(MathF.PI * Math.Min(1f, 0.15f + 0.9f * i / N)) - 0.02f * (i / (float)N);
            for (int i = 0; i < N; i++)
            {
                PointF a = spine[i], b = spine[i + 1];
                float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                float nx = -dy / len, ny = dx / len, w0 = Wd(i), w1 = Wd(i + 1);
                using var br = new SolidBrush(i == N - 1 ? Mask : (i % 2 == 0 ? FurLight : Color.FromArgb(58, 50, 50)));
                g.FillPolygon(br, [P(a.X + nx * w0, a.Y + ny * w0), P(b.X + nx * w1, b.Y + ny * w1), P(b.X - nx * w1, b.Y - ny * w1), P(a.X - nx * w0, a.Y - ny * w0)]);
            }
            Ell(spine[N].X, spine[N].Y, Wd(N) * 0.95f, Wd(N), Mask);                      // the round black tip
            Ell(root.X, root.Y, Wd(0), Wd(0), FurLight);
        }

        // ---- the legs ----
        float slide = Stance * stride;                                                  // how far the body travels while a foot is down
        PointF WalkFoot(float xc, float off)
        {
            float u = (phase + off) % 1f;
            float r = slide / 2;
            if (u < Stance) return P(xc + r * (1 - 2 * u / Stance), 0f);
            float q = (u - Stance) / (1 - Stance);
            float sm = q * q * (3 - 2 * q);
            return P(xc - r + 2 * r * sm, 0.07f * MathF.Sin(MathF.PI * q));
        }
        void Leg(PointF joint, PointF foot, float L, bool hind, Color col, float thick)
        {
            float dx = foot.X - joint.X, dy = foot.Y - joint.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * L - 0.003f);
            float al = MathF.Acos(Math.Clamp(d / (2 * L), -1f, 1f));
            float th = MathF.Atan2(dy, dx) + (hind ? al : -al);
            PointF knee = P(joint.X + L * MathF.Cos(th), joint.Y + L * MathF.Sin(th));
            Limb(joint, knee, thick, col);
            Limb(knee, foot, thick * 0.85f, col);
            Ell(foot.X + 0.025f, foot.Y + 0.015f, 0.07f, 0.04f, col);                    // the plantigrade paw (a raccoon walks on its whole hand)
        }
        PointF acornPaws;
        // The apple, and the paws: the paws go from the ground by the apple to the chest as the body rises.
        PointF pawSit = P(head.X + 0.17f, head.Y - 0.20f);
        PointF pawGround = P(AppleRel.X - 0.05f, AppleRel.Y - 0.02f);
        PointF pawTarget = Lerp(pawGround, pawSit, e);
        acornPaws = P(Lerp(AppleRel.X, pawSit.X + 0.02f, e), Lerp(AppleRel.Y, pawSit.Y + 0.01f, e));

        PointF nearHindW = WalkFoot(-0.28f, 0f), nearFrontW = WalkFoot(0.30f, 0.25f), farHindW = WalkFoot(-0.28f, 0.5f), farFrontW = WalkFoot(0.30f, 0.75f);
        PointF nearHindS = P(hipS.X + 0.26f, 0.035f), farHindS = P(hipS.X + 0.16f, 0.035f);
        bool pawsOn = apple == 1;                                                       // hands busy with the apple
        PointF nearFront = pawsOn ? Lerp(nearFrontW, pawTarget, Math.Max(e, 0.9f * (nose > 0 ? 1 : 0))) : Lerp(nearFrontW, pawTarget, e);
        PointF farFront = Lerp(farFrontW, P(pawTarget.X + 0.05f, pawTarget.Y + 0.01f), e);
        // the sniffing pages reach the paws toward the apple a little too
        Leg(P(shoulder.X + 0.03f, shoulder.Y), farFront, 0.17f, false, PawFar, 0.10f);
        Leg(P(haunch.X + 0.03f, haunch.Y), Lerp(farHindW, farHindS, e), 0.15f, true, PawFar, 0.11f);

        // ---- the body: hip, belly and chest, a darker back and a pale underside ----
        float tilt = MathF.Atan2(chest.Y - hip.Y, chest.X - hip.X) * 180f / MathF.PI;
        Ell(hip.X, hip.Y, 0.28f, 0.25f, Fur);
        Ell(mid.X, mid.Y, 0.36f, 0.23f, Fur, tilt);
        Ell(chest.X, chest.Y, 0.25f, 0.21f, Fur);
        Ell(mid.X + 0.04f, mid.Y - 0.08f, 0.30f, 0.12f, Color.FromArgb(150, FurLight), tilt);
        using (var back = new Pen(Color.FromArgb(120, FurDark), 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(back, [P(hip.X - 0.12f, hip.Y + 0.19f), P(mid.X, mid.Y + 0.19f), P(chest.X - 0.05f, chest.Y + 0.17f)], 0.5f);

        // ---- the head ----
        float ay = Math.Abs(yaw), front = 1 - ay;
        {
            var st = g.Save();
            g.TranslateTransform(head.X, head.Y);
            g.RotateTransform(-12f * nose * (1 - e));                                    // the nose tips down to sniff
            // the ears, then the head, then the cheek ruff and the mask
            float ex1 = -0.05f * yaw - front * 0.13f, ex2 = -0.05f * yaw + front * 0.13f;
            foreach (float ex in front > 0.25f ? new[] { ex1, ex2 } : new[] { ex1 })
            {
                Ell(ex, 0.17f, 0.065f, 0.075f, FurDark);
                Ell(ex, 0.175f, 0.04f, 0.048f, Cream);
                Ell(ex, 0.22f, 0.03f, 0.022f, Cream);
            }
            Ell(0f, 0f, 0.21f + 0.03f * front, 0.17f, Fur);
            Ell(0.02f * yaw, -0.09f, 0.17f + 0.04f * front, 0.07f, Cream);               // the pale cheek ruff along the bottom
            Ell(0.04f * yaw, 0.12f, 0.08f, 0.04f, Color.FromArgb(220, FurLight));        // a pale brow
            // the mask, a band across the eyes
            Ell(0.05f * yaw, 0.015f, 0.14f + 0.09f * front, 0.065f, Mask, -6f * yaw);
            // the snout and nose
            float sx = 0.17f * yaw;
            Ell(sx, -0.05f, 0.11f + 0.02f * front, 0.075f, Cream, -10f * yaw);
            Ell(0.27f * yaw, -0.035f - 0.03f * front, 0.038f, 0.032f, Color.FromArgb(24, 20, 22));
            // the eyes: a warm iris, a dark pupil and a glint, in the dark mask
            float e1 = 0.08f * yaw - front * 0.085f, e2 = 0.08f * yaw + front * 0.085f;
            foreach (float exx in front > 0.3f ? new[] { e1, e2 } : new[] { 0.08f * yaw })
            {
                Ell(exx, 0.03f, 0.042f, 0.042f, Color.FromArgb(168, 124, 72));
                Ell(exx, 0.03f, 0.029f, 0.029f, Color.FromArgb(12, 10, 12));
                Ell(exx + 0.011f, 0.044f, 0.012f, 0.012f, Color.White);
            }
            // a mouth line, and whiskers
            using (var mouth = new Pen(Ink, 0.014f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(mouth, 0.2f * yaw, -0.085f, 0.13f * yaw - 0.02f, -0.095f);
            using (var wh = new Pen(Color.FromArgb(150, 240, 236, 230), 0.008f))
                for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.22f * yaw, -0.055f, 0.36f * yaw, -0.03f + i * 0.04f);
            if (apple == 2) Apple(P(0.23f * yaw + 0.04f, -0.12f), 0.13f);                // carried in its mouth
            g.Restore(st);
        }

        void Apple(PointF at, float r)
        {
            Ell(at.X, at.Y, r, r, AppleRed);
            Ell(at.X - r * 0.35f, at.Y + r * 0.4f, r * 0.3f, r * 0.22f, Color.FromArgb(170, 255, 190, 170));
            Limb(P(at.X, at.Y + r * 0.8f), P(at.X + r * 0.1f, at.Y + r * 1.35f), 0.018f, Color.FromArgb(80, 56, 30));
        }

        // ---- the near legs, in front; the apple in its paws ----
        Leg(shoulder, nearFront, 0.17f, false, Paw, 0.11f);
        Leg(haunch, Lerp(nearHindW, nearHindS, e), 0.15f, true, Paw, 0.12f);
        if (e > 0.6f) Ell(hipS.X + 0.28f, 0.03f, 0.17f, 0.045f, Paw);                 // the long hind foot flat on the ground
        if (apple == 1)
        {
            Apple(acornPaws, 0.135f);
            Ell(acornPaws.X - 0.10f, acornPaws.Y - 0.09f, 0.04f, 0.032f, Paw);            // little dark hands round it
            Ell(acornPaws.X + 0.08f, acornPaws.Y - 0.10f, 0.04f, 0.032f, Paw);
        }
    }
}
