using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// THE ART OF THE PREDATORS, PART ONE: the shared tools and THE WOLF.
// (Part two, the owl and the prey it carries, is PredatorsOwlArt.cs: the same static class.)
//
// FEYNMAN VERSION. An animal here is a flip-book (the hares and the fox are drawn the same way, see FoxAndHaresArt.cs):
// every page is painted ONCE at the start from a POSE, which is only a list of numbers (where the body is, where the
// four toes are, how high the head is held). Because a pose is numbers, two poses can be BLENDED, and every "get up",
// "settle" and "start walking" is a short run of pages that are the blend, so nothing ever jumps from one picture to
// another. The saver then stamps the right page each frame, at a fraction of a pixel (ChristmasScenery.Stamp).
//
// UNITS. A wolf is drawn in METRES, x forward (it faces right; the saver mirrors a page to face left), y UP, the
// origin on the snow under the middle of the trunk. The caller scales a metre to pixels by the field's one scale
// (ChristmasScenery.Metre), so a wolf by the pines and a wolf by the log are the same wolf.
//
// THE WOLF, looked up before it was drawn (a grey timber wolf):
//   - 0.80 m at the withers, 1.5 m from nose to rump plus a 0.45 m brush: nearly twice the fox. The trunk is about
//     0.95 m from chest to rump, DEEP in the chest (0.40 m) and tucked up at the waist; the back is straight and a touch
//     higher at the shoulders. LONG legs (the elbow sits at the chest's bottom, 0.40 m up; the wrist 0.14 m up), big
//     paws. The hind leg has the HOCK, the backward knee, 0.27 m up; the thigh is hidden in the haunch.
//   - the neck is thick and wears a RUFF (pale, big, round the cheeks and shoulders); the head is a long wedge: a pale
//     muzzle a third again as long as the skull, a black nose, amber eyes set slightly slanted, ears upright and
//     rounded at the tips (not as big as the fox's). Colour: grey saddle with darker guard hairs along the back and
//     over the shoulders, pale buff chest, belly and legs, pale muzzle. The brush hangs low with a dark tip.
//   - ITS GAITS. TROT (the travelling gait, diagonal pairs: near fore with far hind, far fore with near hind; a
//     stride of 1.15 m, the head level or low). WALK-STALK (a creeping version: belly low, shoulders dropped,
//     the head below the back line, each paw lifted high and placed, long ground contact). GALLOP (a rotary double
//     suspension: the hind feet land AHEAD, under the belly, with the back hunched; the forefeet reach; the spine
//     stretches in the air). The turn on the spot goes through a head-on view.
//   - EVERY CYCLE starts at page 0 = THE REST PAGE: one diagonal pair planted at the neutral stance, the other pair
//     passing through the same x in the air. Stopping brakes to a page 0 and a SETTLE sheet lowers the other pair and
//     eases the body and head into the Stand pose (and the same sheet backwards starts a walk). So every doing
//     meets the next on the one STAND pose (the transition table is in Predators.cs).
// ============================================================================

/// <summary>One wolf pose: where the trunk, head, tail and each toe are (metres), and a few flags.</summary>
internal sealed class WPose
{
    public float BX, BY = 0.62f;                            // the trunk's middle (BY: its height over the snow)
    public float Pitch, Arch, Spin;                         // trunk pitch (degrees, plus = nose up), back hump (metres), whole-pose spin about the trunk's middle (the tumble)
    public PointF Head = new(0.90f, 0.64f);                 // the skull's middle
    public float HeadTilt = -8f, HeadYaw, Jaw, Blink, Ear = 78f;   // head pitch (plus = nose up), head turned toward us (0 side, 90 face on), jaw open (degrees), eye shut 0..1, ear angle (90 straight up, 150 laid back)
    public PointF NF = new(0.44f, 0f), FF = new(0.38f, 0f), NH = new(-0.38f, 0f), FH = new(-0.46f, 0f);   // toes: near fore, far fore, near hind, far hind
    public float TailDown = 38f, TailSway;                  // the brush's angle below straight back (degrees), and its curl to the side of that
    public float Yaw;                                       // the whole body turned toward us (0 side on, 90 head on)
    public float Shadow = 1f;
    public bool Carry; public float Swing;                  // a hare in the jaws, and its swing (degrees)
    public float Sneeze;                                    // the nose scrunched (0..1)
    public float Open;                                      // the mouth's pant (0..1) for the howl and the gallop
}

internal static partial class PredatorsArt
{
    // ---------------------------------------------------------------- tools
    public static PointF P(float x, float y) => new(x, y);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    public static float Sm(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
    public static PointF Add(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);
    public static PointF Rot(PointF p, float deg) { float a = deg * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a); return new PointF(p.X * c - p.Y * s, p.X * s + p.Y * c); }
    static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)Lerp(a.A, b.A, t), (int)Lerp(a.R, b.R, t), (int)Lerp(a.G, b.G, t), (int)Lerp(a.B, b.B, t));
    static float Cyc(float p) => p - MathF.Floor(p);

    static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0f)
    {
        if (rx < 0.0005f || ry < 0.0005f) return;
        var st = g.Save();
        g.TranslateTransform(cx, cy); g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }
    static void Ell(Graphics g, PointF p, float rx, float ry, Color c, float tilt = 0f) => Ell(g, p.X, p.Y, rx, ry, c, tilt);
    static void EllSeg(Graphics g, PointF a, PointF b, float w, Color c, float extra = 0f)
    {
        float ang = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
        Ell(g, (a.X + b.X) / 2, (a.Y + b.Y) / 2, Dist(a, b) / 2 + extra, w / 2, c, ang);
    }
    static void Limb(Graphics g, PointF a, PointF b, float w, Color c)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, a, b);
    }
    static void Blob(Graphics g, Color c, params PointF[] pts)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, 0.5f);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }
    static void Poly(Graphics g, Color c, params PointF[] pts) { using var b = new SolidBrush(c); g.FillPolygon(b, pts); }
    /// <summary>Two-bone leg: where must the joint be so a bone of l1 from the hip and l2 to the foot meet? "forward" picks the knee in front, else behind.</summary>
    static PointF Knee(PointF hip, PointF foot, float l1, float l2, bool forward)
    {
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y, real = MathF.Sqrt(dx * dx + dy * dy);
        if (real < 1e-5f) return hip;
        float d = Math.Clamp(real, MathF.Abs(l1 - l2) + 0.004f, l1 + l2 - 0.002f);
        float a = (l1 * l1 - l2 * l2 + d * d) / (2 * d), h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - a * a));
        float ux = dx / real, uy = dy / real;
        PointF k1 = P(hip.X + ux * a - uy * h, hip.Y + uy * a + ux * h), k2 = P(hip.X + ux * a + uy * h, hip.Y + uy * a - ux * h);
        return forward == (k1.X >= k2.X) ? k1 : k2;
    }
    /// <summary>The soft blue shadow on the moonlit snow: a flat ellipse, up to about 70 alpha.</summary>
    static void ShadowAt(Graphics g, float cx, float rx, float ry, float k = 1f)
    {
        if (k <= 0.02f) return;
        Ell(g, cx, 0f, rx * (0.6f + 0.4f * k), ry, Color.FromArgb((int)(64 * k), 50, 70, 150));
    }

    // ================================================================ THE WOLF'S PAINTER
    public static readonly Color WCoat = Color.FromArgb(118, 122, 132);       // the grey saddle
    public static readonly Color WDark = Color.FromArgb(66, 70, 82);          // guard hairs along the back, the tail tip, the ear backs
    public static readonly Color WPale = Color.FromArgb(188, 184, 178);       // chest, belly, ruff
    public static readonly Color WLeg = Color.FromArgb(166, 160, 150);        // the pale legs
    public static readonly Color WMuzzle = Color.FromArgb(214, 208, 198);
    public static readonly Color WFar = Color.FromArgb(84, 90, 104);          // the far side, a shade down
    static readonly Color WNose = Color.FromArgb(22, 20, 26);
    static readonly Color WEye = Color.FromArgb(255, 192, 60);
    static readonly Color WBlood = Color.FromArgb(176, 36, 40);

    /// <summary>The nose tip, in the pose's own metres (the page's Aux: where the breath plume starts).</summary>
    public static PointF WNose_(WPose q) => Add(q.Head, Rot(P(0.32f * MathF.Cos(q.HeadYaw * 0.01745f), 0.0f), q.HeadTilt));

    public static void PaintWolf(Graphics g, WPose q)
    {
        ShadowAt(g, q.BX - 0.02f, 0.95f, 0.13f, q.Shadow);
        var spin = g.Save();
        if (MathF.Abs(q.Spin) > 0.01f) { g.TranslateTransform(q.BX, q.BY); g.RotateTransform(q.Spin); g.TranslateTransform(-q.BX, -q.BY); }
        float yaw = q.Yaw * MathF.PI / 180f, cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        // PROJECT a side-view point to this yaw: x squeezes by cos, "lat" (how far toward the viewer) shows by sin.
        PointF Pr(PointF p, float lat = 0f) => P(q.BX + (p.X - q.BX) * cy + lat * sy, p.Y);
        PointF Body(float f, float u, float lat = 0f) => Pr(Add(P(q.BX, q.BY), Rot(P(f, u + 0f), q.Pitch)), lat);

        // ---- legs: joint positions from the trunk, joints from two-bone IK ----
        void Fore(PointF toe, float lat, Color tone)
        {
            PointF sh = Body(0.40f, -0.08f, lat * 0.5f), wr = Pr(P(toe.X - 0.015f, toe.Y + 0.145f), lat), tp = Pr(P(toe.X + 0.03f, toe.Y + 0.02f), lat);
            PointF el = Knee(sh, wr, 0.22f, 0.23f, false);
            Limb(g, sh, el, 0.125f, tone);                  // the upper arm, thick
            Limb(g, el, wr, 0.082f, tone);                  // the forearm
            EllSeg(g, wr, tp, 0.068f, Mix(tone, WLeg, 0.4f), 0.02f);    // the pastern and the big round paw
        }
        void Hind(PointF toe, float lat, Color tone)
        {
            PointF hp = Body(-0.40f, -0.04f, lat * 0.5f), tp = Pr(P(toe.X + 0.03f, toe.Y + 0.02f), lat);
            // the hock stands behind and above the toe: the metatarsus leans forward from the hock to the paw
            PointF hk = Pr(P(toe.X - 0.07f, toe.Y + 0.27f), lat);
            PointF st = Knee(hp, hk, 0.24f, 0.26f, true);   // the stifle (the knee), forward of the hip
            EllSeg(g, hp, st, 0.20f, tone, 0.03f);          // the thigh: a big round haunch
            Limb(g, st, hk, 0.10f, tone);                   // the shank down to the hock
            Limb(g, hk, tp, 0.062f, Mix(tone, WLeg, 0.5f));   // the metatarsus
            Ell(g, tp.X + 0.02f, tp.Y + 0.012f, 0.062f, 0.036f, Mix(tone, WLeg, 0.5f));
        }
        Fore(q.FF, -0.10f, WFar);
        Hind(q.FH, -0.10f, WFar);

        // ---- the brush: a bushy taper from the rump, hanging low, dark at the tip ----
        {
            PointF root = Body(-0.50f, 0.12f);
            // direction: straight back (-x) rotated down by TailDown, then curled by TailSway
            float down = q.TailDown * MathF.PI / 180f, curl = (q.TailDown + q.TailSway) * MathF.PI / 180f;
            PointF m1 = P(root.X - 0.20f * cy * MathF.Cos(down), root.Y - 0.20f * MathF.Sin(down));
            PointF m2 = P(m1.X - 0.24f * cy * MathF.Cos(curl), m1.Y - 0.24f * MathF.Sin(curl));
            Blob(g, WCoat, P(root.X + 0.04f, root.Y + 0.09f), P(m1.X, m1.Y + 0.13f), P(m2.X + 0.02f, m2.Y + 0.09f), P(m2.X - 0.07f, m2.Y - 0.02f), P(m1.X, m1.Y - 0.14f), P(root.X + 0.02f, root.Y - 0.08f));
            Blob(g, WPale, P(m1.X + 0.02f, m1.Y - 0.01f), P(m1.X - 0.05f, m1.Y + 0.07f), P(m2.X, m2.Y + 0.04f), P(m1.X - 0.04f, m1.Y - 0.10f));       // the paler under-fur
            Ell(g, m2.X - 0.015f, m2.Y + 0.0f, 0.10f, 0.075f, WDark, curl * -1f + 180f);                                                                     // the dark tip
        }

        // ---- the trunk: deep chest, tucked waist, a hump that comes and goes with the gallop ----
        Blob(g, WCoat,
            Body(0.42f, 0.19f), Body(0.20f, 0.17f + q.Arch * 0.6f), Body(-0.02f, 0.155f + q.Arch), Body(-0.24f, 0.165f + q.Arch * 0.6f), Body(-0.46f, 0.125f),
            Body(-0.53f, -0.02f), Body(-0.42f, -0.17f), Body(-0.24f, -0.15f), Body(0.0f, -0.14f), Body(0.22f, -0.25f), Body(0.42f, -0.25f), Body(0.56f, -0.08f), Body(0.52f, 0.10f));
        // the dark saddle: guard hairs along the back, over the shoulders and down the flank
        Blob(g, WDark, Body(0.38f, 0.185f), Body(0.10f, 0.165f + q.Arch * 0.9f), Body(-0.20f, 0.165f + q.Arch * 0.7f), Body(-0.42f, 0.12f), Body(-0.36f, 0.04f), Body(-0.12f, 0.06f), Body(0.12f, 0.07f), Body(0.34f, 0.075f));
        // the pale belly and chest
        Blob(g, WPale, Body(0.46f, -0.17f), Body(0.20f, -0.19f), Body(0.0f, -0.115f), Body(-0.22f, -0.13f), Body(-0.1f, -0.065f), Body(0.2f, -0.10f), Body(0.44f, -0.08f));

        Hind(q.NH, 0.10f, WLeg);

        // ---- the neck and the ruff, then the near foreleg over the shoulder ----
        PointF hd = q.Head;
        PointF neckA = Body(0.42f, 0.02f), neckB = Add(hd, Rot(P(-0.10f, 0f), q.HeadTilt));
        {
            PointF a = Pr(neckA), b = Pr(neckB);
            Blob(g, WCoat, P(a.X + 0.04f, a.Y + 0.22f), P((a.X + b.X) / 2 + 0.02f, (a.Y + b.Y) / 2 + 0.17f), P(b.X, b.Y + 0.11f), P(b.X + 0.02f, b.Y - 0.12f), P((a.X + b.X) / 2, (a.Y + b.Y) / 2 - 0.18f), P(a.X + 0.02f, a.Y - 0.22f));
            Blob(g, WDark, P(a.X + 0.0f, a.Y + 0.215f), P((a.X + b.X) / 2 + 0.02f, (a.Y + b.Y) / 2 + 0.165f), P(b.X - 0.02f, b.Y + 0.105f), P((a.X + b.X) / 2, (a.Y + b.Y) / 2 + 0.06f), P(a.X + 0.02f, a.Y + 0.08f));
        }
        Fore(q.NF, 0.10f, WLeg);
        {
            // THE RUFF: pale, big, a cloud round the shoulders and the cheeks (it is the first thing that says WOLF against the fox)
            PointF a = Pr(neckA), b = Pr(neckB);
            PointF mid = P((a.X + b.X) / 2 + 0.03f, (a.Y + b.Y) / 2 - 0.02f);
            Ell(g, mid.X, mid.Y - 0.03f, 0.20f, 0.19f, WPale, q.HeadTilt * 0.5f);
            Ell(g, b.X + 0.03f, b.Y - 0.05f, 0.14f, 0.15f, WPale, 0f);
            Ell(g, mid.X - 0.04f, mid.Y + 0.05f, 0.13f, 0.08f, Mix(WPale, WCoat, 0.45f), 12f);
            Blob(g, Mix(WPale, WMuzzle, 0.5f), P(b.X + 0.05f, b.Y - 0.01f), P(b.X + 0.10f, b.Y - 0.10f), P(b.X + 0.02f, b.Y - 0.14f), P(b.X - 0.06f, b.Y - 0.07f));
        }

        // ---- the head ----
        float hy = q.HeadYaw * MathF.PI / 180f, hc = MathF.Cos(hy), hs = MathF.Sin(hy);
        PointF HP(float f, float u, float lat = 0f) { PointF r = Rot(P(f * hc + lat * hs, u), q.HeadTilt); return P(Pr(hd).X + r.X, hd.Y + r.Y); }
        float earA = q.Ear;
        void EarDraw(bool near)
        {
            float lat = near ? 0.075f : -0.075f;
            PointF root = HP(-0.025f, 0.10f, lat * 1.0f);
            float a = (earA + (near ? 0f : 6f)) * MathF.PI / 180f;
            float spread = (near ? 1f : -1f) * 0.06f * hs;
            PointF tip = P(root.X + 0.14f * MathF.Cos(a) + spread, root.Y + 0.15f * MathF.Sin(a));
            PointF bk = P(root.X - 0.055f, root.Y + 0.0f), fr = P(root.X + 0.055f, root.Y - 0.005f);
            Color col = near ? WCoat : WFar;
            Poly(g, col, bk, tip, fr);
            Ell(g, Lerp(root, tip, 0.45f), 0.045f, 0.012f, Color.FromArgb(150, 212, 170, 160), (a * 57.2958f));
            Ell(g, Lerp(root, tip, 0.88f), 0.025f, 0.02f, WDark);
        }
        EarDraw(false);
        Ell(g, HP(0f, 0f), 0.142f * (0.85f + 0.15f * hc) + 0.03f * hs, 0.128f, WCoat, q.HeadTilt);                         // the skull
        Ell(g, HP(0.02f, 0.06f), 0.10f, 0.05f, WDark, q.HeadTilt);                                                        // the dark crown and brow
        {
            // the muzzle: a long pale wedge (foreshortened as the head turns), the jaw hinging below it
            float mx = 0.30f * hc, jaw = q.Jaw * MathF.PI / 180f;
            float sneeze = q.Sneeze * 0.012f;
            if (hs < 0.93f)
            {
                PointF m0 = HP(0.05f, 0.02f), m1 = HP(0.17f, 0.028f), m2 = HP(0.31f, 0.006f), m3 = HP(0.305f, -0.045f), m4 = HP(0.16f, -0.078f), m5 = HP(0.05f, -0.080f);
                // the lower jaw, hinged near the cheek, swinging down by Jaw
                PointF hinge = HP(0.06f, -0.05f);
                PointF J(float f, float u) { PointF r = Rot(P(f * hc, u), q.HeadTilt - q.Jaw); return P(hinge.X + (r.X - Rot(P(0.06f * hc, -0.05f), q.HeadTilt - q.Jaw).X), hinge.Y + (r.Y - Rot(P(0.06f * hc, -0.05f), q.HeadTilt - q.Jaw).Y)); }
                if (q.Jaw > 1f)
                {
                    Blob(g, Mix(WMuzzle, WCoat, 0.2f), J(0.07f, -0.035f), J(0.20f, -0.045f), J(0.285f, -0.05f), J(0.275f, -0.075f), J(0.16f, -0.083f), J(0.07f, -0.07f));
                    Blob(g, Color.FromArgb(120, 40, 36, 50), m4, m3, J(0.28f, -0.052f), J(0.12f, -0.05f));       // the dark mouth between
                }
                Blob(g, WMuzzle, m0, m1, m2, m3, m4, m5);
                Ell(g, Lerp(m4, m3, 0.45f), 0.07f * hc + 0.02f, 0.016f, Color.FromArgb(70, 120, 110, 110), q.HeadTilt - 2f);   // the lip line, faint
                Ell(g, m2.X + 0.004f, m2.Y + sneeze, 0.034f, 0.030f, WNose, q.HeadTilt);                                          // the black nose
                if (q.Carry) Ell(g, Lerp(m4, m3, 0.55f), 0.035f, 0.016f, WBlood, q.HeadTilt);                                    // a smear at the mouth
            }
            else
            {
                // FACE ON: the muzzle is a pale oval below the eyes, the nose its dark tip
                PointF mf = HP(0.02f, -0.045f);
                Ell(g, mf.X, mf.Y, 0.055f, 0.062f, WMuzzle, 0f);
                Ell(g, mf.X, mf.Y - 0.040f, 0.026f, 0.020f, WNose, 0f);
            }
        }
        {
            // cheek ruff under the jaw
            PointF c0 = HP(-0.05f, -0.07f, 0.02f);
            Blob(g, WPale, P(c0.X - 0.06f, c0.Y + 0.03f), P(c0.X + 0.07f, c0.Y + 0.05f), P(c0.X + 0.09f, c0.Y - 0.02f), P(c0.X + 0.01f, c0.Y - 0.085f), P(c0.X - 0.08f, c0.Y - 0.05f));
        }
        EarDraw(true);
        {
            // the eye: amber, slanted, a dark pupil, a glint that catches the moon
            float shut = Math.Clamp(q.Blink, 0f, 1f);
            PointF e = HP(0.095f, 0.045f, 0.05f);
            if (shut > 0.6f) Ell(g, e, 0.024f, 0.006f, WDark, q.HeadTilt + 8f);
            else
            {
                Ell(g, e, 0.025f, 0.017f * (1f - shut), WEye, q.HeadTilt + 10f);
                Ell(g, e.X + 0.004f, e.Y, 0.011f, 0.014f * (1f - shut), WNose);
                Ell(g, e.X + 0.008f, e.Y + 0.006f, 0.005f, 0.005f, Color.FromArgb(255, 255, 250, 220));
            }
            if (hs > 0.35f)
            {
                PointF e2 = HP(0.085f, 0.030f, -0.065f);
                Ell(g, e2, 0.022f, 0.016f * (1f - shut), WEye, q.HeadTilt + 10f);
                Ell(g, e2.X, e2.Y, 0.009f, 0.012f * (1f - shut), WNose);
            }
        }

        // ---- a hare hung from the jaws: limp, white, long ears and big hind feet hanging ----
        if (q.Carry)
        {
            PointF jaw = HP(0.20f, -0.05f);
            PaintLimpHare(g, jaw, q.Swing, 1f);
        }
        g.Restore(spin);
    }

    /// <summary>A hare hung limp from a point (the scruff in the jaws): the body hangs, the head and ears swing, the big hind feet dangle. "swing" degrees sway it; "scale" 1 = a hare of 0.585 m.</summary>
    public static void PaintLimpHare(Graphics g, PointF hang, float swing, float scale)
    {
        var st = g.Save();
        g.TranslateTransform(hang.X, hang.Y); g.RotateTransform(swing); g.ScaleTransform(scale, scale);
        Color coat = Color.FromArgb(244, 242, 240), shade = Color.FromArgb(168, 184, 212), tip = Color.FromArgb(40, 34, 44), pink = Color.FromArgb(232, 190, 198);
        // body hangs straight down from the hold: the shaded back toward us, the pale belly
        Blob(g, coat, P(-0.075f, -0.02f), P(0.085f, -0.03f), P(0.12f, -0.22f), P(0.09f, -0.42f), P(-0.07f, -0.44f), P(-0.11f, -0.22f));
        Blob(g, shade, P(-0.03f, -0.05f), P(0.0f, -0.20f), P(-0.02f, -0.40f), P(-0.08f, -0.38f), P(-0.10f, -0.20f));
        // the head and the long ears hang on the far side of the hold, ears dangling down
        Ell(g, P(-0.07f, -0.07f), 0.07f, 0.06f, coat, 20f);
        EllSeg(g, P(-0.09f, -0.10f), P(-0.20f, -0.32f), 0.06f, Mix(coat, shade, 0.4f));
        Ell(g, P(-0.20f, -0.32f), 0.03f, 0.02f, tip, 60f);
        EllSeg(g, P(-0.06f, -0.10f), P(-0.12f, -0.34f), 0.05f, Mix(coat, shade, 0.2f));
        // the big hind feet dangle at the bottom
        EllSeg(g, P(0.02f, -0.40f), P(0.12f, -0.64f), 0.07f, coat);
        EllSeg(g, P(-0.04f, -0.40f), P(0.0f, -0.65f), 0.065f, Mix(coat, shade, 0.3f));
        Ell(g, P(0.06f, -0.44f), 0.06f, 0.05f, Color.FromArgb(252, 252, 250));          // the tail puff
        g.Restore(st);
    }

    // ================================================================ THE WOLF'S POSES AND GAITS

    /// <summary>The stand: the shared pose every sheet starts from or ends on. "t" is the idle phase 0..1 (page 0 is exactly the neutral pose).</summary>
    public static WPose WStand(float t = 0f, bool carry = false)
    {
        float s = MathF.Sin(MathF.Tau * t);
        var q = new WPose { BY = 0.62f + 0.006f * s, Head = P(0.90f + 0.012f * s, 0.64f + 0.008f * s), HeadTilt = -8f + 1.5f * s, TailDown = 40f, TailSway = 5f * MathF.Sin(2f * MathF.Tau * t) };
        // an ear flick and a slow blink, placed so both happen at fixed moments of the idle cycle (never at page 0)
        if (t > 0.52f && t < 0.62f) q.Ear = 78f - 16f * MathF.Sin((t - 0.52f) / 0.10f * MathF.PI);
        if (t > 0.28f && t < 0.34f) q.Blink = MathF.Sin((t - 0.28f) / 0.06f * MathF.PI);
        if (carry) { q.Carry = true; q.Head = P(0.95f + 0.012f * s, 0.78f + 0.008f * s); q.HeadTilt = 6f; q.Swing = 4f * MathF.Sin(MathF.Tau * t * 2f); q.TailDown = 48f; }
        return q;
    }

    public sealed class WGait
    {
        public float C = 1.15f, Beta = 0.55f, Lift = 0.10f, BodyY = 0.62f, Bob = 0.012f, Pitch = 0f;
        public PointF Head = P(0.90f, 0.64f); public float HeadTilt = -8f, HeadBob = 0.015f, TailDown = 40f, Ear = 78f;
        public bool Carry; public float Reach = 0.06f;
    }
    // the gaits: each is a set of numbers handed to the one gait function below
    public static readonly WGait Lope = new() { C = 1.15f, Beta = 0.56f, Lift = 0.085f, BodyY = 0.60f, Bob = 0.010f, Pitch = -2f, Head = P(0.88f, 0.55f), HeadTilt = -18f, HeadBob = 0.020f, TailDown = 44f, Ear = 72f };
    public static readonly WGait Stalk = new() { C = 0.85f, Beta = 0.72f, Lift = 0.13f, BodyY = 0.46f, Bob = 0.006f, Pitch = -9f, Head = P(0.84f, 0.40f), HeadTilt = -26f, HeadBob = 0.012f, TailDown = 12f, Ear = 125f, Reach = 0.05f };
    public static readonly WGait CarryG = new() { C = 1.20f, Beta = 0.55f, Lift = 0.10f, BodyY = 0.62f, Bob = 0.012f, Pitch = 1f, Head = P(0.95f, 0.78f), HeadTilt = 6f, HeadBob = 0.012f, TailDown = 30f, Ear = 100f, Carry = true };

    static void StepFoot(float q, WGait g, float n, out float x, out float y)
    {
        float L = g.Beta * g.C;                        // how far a planted foot slides back, in the trunk's frame, during its stance: the trunk moves exactly this far in that time, so the foot STAYS PUT on the snow
        q = q - MathF.Floor(q + 0.5f);                  // -0.5 .. 0.5, stance is centred on 0
        if (MathF.Abs(q) <= g.Beta * 0.5f) { float s = (q + g.Beta * 0.5f) / g.Beta; x = n + L * 0.5f - L * s; y = 0f; return; }
        float w = (q > 0 ? q - g.Beta * 0.5f : q + 1f - g.Beta * 0.5f) / (1f - g.Beta);      // the swing, 0 (lifting at the back) to 1 (landing in front)
        x = n - L * 0.5f + L * Sm(w);
        y = g.Lift * MathF.Pow(MathF.Sin(MathF.PI * w), 1.2f);
    }

    /// <summary>A diagonal-pair gait at phase p (0 = the rest page). "amp" (0..1) scales how far the feet sweep (the settle sheets use it with the blend instead).</summary>
    public static WPose WGaitPose(float p, WGait g)
    {
        var q = new WPose { Carry = g.Carry };
        float p2 = MathF.Tau * p;
        q.BY = g.BodyY - g.Bob * MathF.Cos(2f * p2);
        q.Pitch = g.Pitch + 1.2f * MathF.Sin(2f * p2);
        q.Head = P(g.Head.X + 0.008f * MathF.Sin(2f * p2), g.Head.Y + g.HeadBob * MathF.Sin(2f * p2 + 0.8f));
        q.HeadTilt = g.HeadTilt + 2.5f * MathF.Sin(2f * p2 + 0.8f);
        q.TailDown = g.TailDown; q.TailSway = 7f * MathF.Sin(p2 + 1f); q.Ear = g.Ear;
        // pair A: near fore + far hind (phase p); pair B: far fore + near hind (phase p + 0.5)
        StepFoot(p, g, 0.44f, out float x, out float y); q.NF = P(x, y);
        StepFoot(p, g, -0.46f, out x, out y); q.FH = P(x, y);
        StepFoot(p + 0.5f, g, 0.38f, out x, out y); q.FF = P(x, y);
        StepFoot(p + 0.5f, g, -0.38f, out x, out y); q.NH = P(x, y);
        if (g.Carry) q.Swing = 9f * MathF.Sin(2f * p2 + 0.5f);
        return q;
    }

    // ---- the gallop: rotary double suspension, keyed on the phase (0 = the forefeet land) ----
    static readonly float[][] GFootNF = [[0.00f, 0.80f, 0.00f], [0.24f, 0.05f, 0.00f], [0.40f, -0.08f, 0.40f], [0.70f, 0.45f, 0.50f], [0.88f, 0.86f, 0.30f], [1.00f, 0.80f, 0.00f]];
    static readonly float[][] GFootNH = [[0.00f, 0.00f, 0.30f], [0.17f, 0.02f, 0.00f], [0.42f, -0.78f, 0.00f], [0.56f, -0.84f, 0.36f], [0.78f, -0.22f, 0.55f], [0.93f, 0.10f, 0.22f], [1.00f, 0.00f, 0.30f]];
    static readonly float[][] GBodyY = [[0.00f, 0.58f], [0.17f, 0.52f], [0.35f, 0.60f], [0.55f, 0.74f], [0.75f, 0.80f], [0.90f, 0.68f], [1.00f, 0.58f]];
    static readonly float[][] GPitch = [[0.00f, -12f], [0.17f, -4f], [0.35f, 7f], [0.55f, 9f], [0.75f, 1f], [0.90f, -8f], [1.00f, -12f]];
    static readonly float[][] GArch = [[0.00f, -0.05f], [0.17f, 0.10f], [0.40f, 0.09f], [0.60f, 0.02f], [0.80f, -0.03f], [1.00f, -0.05f]];

    static float Key1(float[][] k, int col, float p)
    {
        p = Cyc(p);
        for (int i = 0; i < k.Length - 1; i++)
            if (p >= k[i][0] && p <= k[i + 1][0]) { float u = (p - k[i][0]) / MathF.Max(1e-4f, k[i + 1][0] - k[i][0]); u = 0.5f - 0.5f * MathF.Cos(MathF.PI * u); return Lerp(k[i][col], k[i + 1][col], u); }
        return k[^1][col];
    }

    public static WPose WGallop(float p, bool jaw = true)
    {
        var q = new WPose { Shadow = 0.8f };
        q.BY = Key1(GBodyY, 1, p); q.Pitch = Key1(GPitch, 1, p); q.Arch = Key1(GArch, 1, p);
        float ext = Math.Clamp((Key1(GArch, 1, p) * -1f + 0.0f) * 8f, 0f, 1f);               // 1 when the back is stretched
        q.Head = P(0.92f + 0.05f * ext, 0.60f - 0.03f * ext + 0.015f * MathF.Sin(MathF.Tau * p)); q.HeadTilt = -14f + 4f * MathF.Sin(MathF.Tau * p); q.Ear = 140f; q.Open = 1f; q.Jaw = jaw ? 10f + 6f * ext : 0f;
        q.TailDown = 8f; q.TailSway = 6f * MathF.Sin(MathF.Tau * p + 0.6f);
        float xn(float[][] k, float pp, int col) => Key1(k, col, pp);
        q.NF = P(xn(GFootNF, p, 1), xn(GFootNF, p, 2)); q.FF = P(xn(GFootNF, p + 0.97f, 1) - 0.05f, xn(GFootNF, p + 0.97f, 2));
        q.NH = P(xn(GFootNH, p, 1), xn(GFootNH, p, 2)); q.FH = P(xn(GFootNH, p + 0.96f, 1) - 0.05f, xn(GFootNH, p + 0.96f, 2));
        return q;
    }

    /// <summary>
    /// A BLEND of two poses at t (0..1). Everything eases (slow, fast, slow). A foot that has to MOVE along the snow does not slide: it
    /// LIFTS and steps (a small bump of height), the four feet taking their turns, so a planted foot is never dragged.
    /// </summary>
    public static WPose WBlend(WPose a, WPose b, float t)
    {
        float s = Sm(t);
        WPose q = new()
        {
            BX = Lerp(a.BX, b.BX, s), BY = Lerp(a.BY, b.BY, s), Pitch = Lerp(a.Pitch, b.Pitch, s), Arch = Lerp(a.Arch, b.Arch, s), Spin = Lerp(a.Spin, b.Spin, s),
            Head = Lerp(a.Head, b.Head, s), HeadTilt = Lerp(a.HeadTilt, b.HeadTilt, s), HeadYaw = Lerp(a.HeadYaw, b.HeadYaw, s), Jaw = Lerp(a.Jaw, b.Jaw, s),
            Blink = Lerp(a.Blink, b.Blink, s), Ear = Lerp(a.Ear, b.Ear, s), TailDown = Lerp(a.TailDown, b.TailDown, s), TailSway = Lerp(a.TailSway, b.TailSway, s),
            Yaw = Lerp(a.Yaw, b.Yaw, s), Shadow = Lerp(a.Shadow, b.Shadow, s), Carry = a.Carry && b.Carry, Swing = Lerp(a.Swing, b.Swing, s), Sneeze = Lerp(a.Sneeze, b.Sneeze, s), Open = Lerp(a.Open, b.Open, s),
        };
        PointF Foot(PointF fa, PointF fb, float start)
        {
            float u = Math.Clamp((t - start) / 0.62f, 0f, 1f), us = Sm(u);
            float x = Lerp(fa.X, fb.X, us), y = Lerp(fa.Y, fb.Y, us);
            if (fa.Y < 0.01f && fb.Y < 0.01f) y += MathF.Min(0.12f, 0.45f * MathF.Abs(fb.X - fa.X)) * MathF.Sin(MathF.PI * u);        // a planted foot that must move steps: it lifts
            return P(x, y);
        }
        q.NF = Foot(a.NF, b.NF, 0.00f); q.FH = Foot(a.FH, b.FH, 0.10f); q.FF = Foot(a.FF, b.FF, 0.20f); q.NH = Foot(a.NH, b.NH, 0.30f);
        return q;
    }

    // ================================================================ THE WOLF'S SHEETS
    // book indexes (Predators.cs uses these names)
    public const int WStandB = 0, WLopeB = 1, WLopeSet = 2, WStalkB = 3, WStalkSet = 4, WGallopB = 5, WRushIn = 6, WSkid = 7, WPounce = 8, WCStand = 9, WCShake = 10, WCLook = 11, WCarryB = 12, WCarrySet = 13,
                     WSniff = 14, WLook = 15, WHowlIn = 16, WHowl = 17, WHowlOut = 18, WTurn = 19, WDigIn = 20, WDig = 21, WDigOut = 22, WRearIn = 23, WRearB = 24, WRearOut = 25;
    public const int NStand = 48, NLope = 28, NLopeSet = 12, NStalk = 30, NStalkSet = 16, NGallop = 28, NRushIn = 14, NSkid = 22, NPounce = 44, NCStand = 36, NCShake = 24, NCLook = 32, NCarry = 28, NCarrySet = 12,
                     NSniff = 54, NLook = 40, NHowlIn = 24, NHowl = 24, NHowlOut = 24, NTurn = 21, NDigIn = 18, NDig = 24, NDigOut = 24, NRearIn = 30, NRear = 30, NRearOut = 30;
    /// <summary>The distance (metres) of the wolf's trunk's middle to the hole's middle during the dig: the muzzle goes in the mouth.</summary>
    public const float DigReach = 1.34f;

    public static WPose WSkidPose(float u)
    {
        // the brake: forelegs braced straight forward, haunches sunk, the body leaning back; then it rises into the stand
        WPose g0 = WGallop(0f, false), brake = new WPose { BY = 0.50f, Pitch = 12f, Head = P(0.92f, 0.78f), HeadTilt = 6f, Ear = 120f, TailDown = 20f, Open = 1f, Jaw = 12f };
        brake.NF = P(0.86f, 0f); brake.FF = P(0.80f, 0f); brake.NH = P(-0.24f, 0f); brake.FH = P(-0.30f, 0f);
        WPose stand = WStand(0f);
        return u < 0.45f ? WBlend(g0, brake, u / 0.45f) : WBlend(brake, stand, (u - 0.45f) / 0.55f);
    }

    static WPose WHeadUp(WPose from, float up, float tilt, float jaw = 0f)
    {
        var q = WBlend(from, from, 0f);
        q.Head = P(from.Head.X - 0.12f * up, from.Head.Y + 0.30f * up); q.HeadTilt = Lerp(from.HeadTilt, tilt, up); q.Jaw = jaw;
        q.BY = from.BY + 0.005f * up; q.Pitch = from.Pitch + 3f * up; q.Ear = Lerp(from.Ear, 70f, up);
        return q;
    }

    public static BookSpec[] WolfBooks()
    {
        BookSpec Make(string name, int n, Func<int, WPose> pose, float halfW = 1.45f, float above = 1.30f) => new()
        {
            Name = name, N = n, HalfW = halfW, Above = above,
            Paint = (g, i) => PaintWolf(g, pose(i)),
            Aux = i => WNose_(pose(i)),
        };
        WPose Stand0 = WStand(0f), CStand0 = WStand(0f, true);
        WPose LopeRest = WGaitPose(0f, Lope), StalkRest = WGaitPose(0f, Stalk), CarryRest = WGaitPose(0f, CarryG), GalRest = WGallop(0f);
        // the HOWL's own poses: muzzle straight up and a little back, the neck stretched, the chest swelling
        WPose HowlTop(float s) { var q = WHeadUp(Stand0, 1f, 62f, 14f); q.Head = P(0.72f, 0.97f + 0.006f * s); q.HeadTilt = 64f; q.Ear = 118f; q.TailDown = 62f; q.BY = 0.63f + 0.01f * s; q.Pitch = 5f; q.Open = 1f; return q; }
        return
        [
            Make("stand", NStand, i => WStand(i / (float)NStand)),
            Make("lope", NLope, i => WGaitPose(i / (float)NLope, Lope)),
            Make("lope settle", NLopeSet, i => WBlend(LopeRest, Stand0, i / (float)(NLopeSet - 1))),
            Make("stalk", NStalk, i => WGaitPose(i / (float)NStalk, Stalk)),
            Make("stalk settle", NStalkSet, i => WBlend(StalkRest, Stand0, i / (float)(NStalkSet - 1))),
            Make("gallop", NGallop, i => WGallop(i / (float)NGallop), 1.6f, 1.45f),
            Make("rush in", NRushIn, i => { float u = i / (float)(NRushIn - 1); var crouch = WGaitPose(0f, Stalk); crouch.BY = 0.40f; crouch.Pitch = -6f; crouch.Head = P(0.84f, 0.50f); crouch.HeadTilt = -14f; return u < 0.5f ? WBlend(StalkRest, crouch, u / 0.5f) : WBlend(crouch, GalRest, (u - 0.5f) / 0.5f); }, 1.6f, 1.45f),
            Make("skid", NSkid, i => WSkidPose(i / (float)(NSkid - 1)), 1.6f, 1.45f),
            Make("pounce", NPounce, WPounceAt, 1.7f, 1.6f),
            Make("carry stand", NCStand, i => WStand(i / (float)NCStand, true)),
            Make("carry shake", NCShake, i => { var q = WStand(0f, true); float u = i / (float)(NCShake - 1), env = MathF.Sin(MathF.PI * u); q.Spin = 0f; q.HeadTilt += 6f * env * MathF.Sin(u * 9f * MathF.PI); q.Head = P(q.Head.X + 0.03f * env * MathF.Sin(u * 9f * MathF.PI), q.Head.Y); q.BY += 0.015f * env * MathF.Sin(u * 9f * MathF.PI + 1f); q.Swing = 22f * env * MathF.Sin(u * 9f * MathF.PI); q.Pitch = 2.5f * env * MathF.Sin(u * 9f * MathF.PI + 0.5f); q.Ear = 98f; return q; }),
            Make("carry look", NCLook, i => { var q = WStand(0f, true); float u = i / (float)(NCLook - 1), yawu = u < 0.35f ? Sm(u / 0.35f) : u < 0.65f ? 1f : 1f - Sm((u - 0.65f) / 0.35f); q.HeadYaw = 62f * yawu; q.Head = P(q.Head.X, q.Head.Y + 0.03f * yawu); q.Ear = 90f; return q; }),
            Make("carry trot", NCarry, i => WGaitPose(i / (float)NCarry, CarryG)),
            Make("carry settle", NCarrySet, i => WBlend(CarryRest, CStand0, i / (float)(NCarrySet - 1))),
            Make("sniff", NSniff, i => { float u = i / (float)(NSniff - 1); float up = u < 0.2f ? Sm(u / 0.2f) : u < 0.8f ? 1f : 1f - Sm((u - 0.8f) / 0.2f); var q = WHeadUp(Stand0, up, 32f); float s = u > 0.2f && u < 0.8f ? MathF.Sin((u - 0.2f) / 0.6f * MathF.Tau * 3.5f) : 0f; q.Head = P(q.Head.X + 0.02f * s, q.Head.Y + 0.02f * s); q.HeadTilt += 4f * s; q.Sneeze = 0.5f + 0.5f * s * (up > 0.9f ? 1 : 0); q.Jaw = up * 3f; return q; }),
            Make("look", NLook, i => { float u = i / (float)(NLook - 1); float up = u < 0.2f ? Sm(u / 0.2f) : u < 0.8f ? 1f : 1f - Sm((u - 0.8f) / 0.2f); var q = WHeadUp(Stand0, up * 0.75f, 12f); float sweep = u > 0.2f && u < 0.8f ? MathF.Sin((u - 0.2f) / 0.6f * MathF.PI * 2f) : 0f; q.HeadYaw = 40f * Math.Max(0f, sweep) * up; q.Head = P(q.Head.X + 0.03f * sweep, q.Head.Y); return q; }),
            Make("howl in", NHowlIn, i => WBlend(Stand0, HowlTop(0f), i / (float)(NHowlIn - 1))),
            Make("howl", NHowl, i => HowlTop(MathF.Sin(MathF.Tau * i / NHowl))),
            Make("howl out", NHowlOut, i => WBlend(HowlTop(0f), Stand0, i / (float)(NHowlOut - 1))),
            Make("turn", NTurn, i => { var q = WStand(0f); float u = i / (float)(NTurn - 1); q.Yaw = 90f * Sm(u); q.HeadYaw = 90f * Sm(Math.Min(1f, u * 1.4f)); q.Head = P(q.Head.X, q.Head.Y); if (u > 0.05f) q.TailDown = Lerp(40f, 40f, u); return q; }),
            Make("dig in", NDigIn, i => WBlend(Stand0, WDigPose(0f), i / (float)(NDigIn - 1)), 1.8f, 1.2f),
            Make("dig", NDig, i => WDigPose(i / (float)NDig), 1.8f, 1.2f),
            Make("rear in", NRearIn, i => WBlend(Stand0, WRearPose(0f), i / (float)(NRearIn - 1)), 1.8f, 2.0f),
            Make("rear", NRear, i => WRearPose(i / (float)NRear), 1.8f, 2.0f),
            Make("rear out", NRearOut, i => WBlend(WRearPose(0f), Stand0, i / (float)(NRearOut - 1)), 1.8f, 2.0f),
            Make("dig out", NDigOut, i => { float u = i / (float)(NDigOut - 1); var d = WDigPose(0f); var s = WStand(0f); if (u < 0.45f) { var up = WHeadUp(Stand0, 0.8f, 10f); up.Sneeze = 1f; var q = WBlend(d, up, Sm(u / 0.45f)); return q; } var up2 = WHeadUp(Stand0, 0.8f, 10f); up2.Sneeze = 1f; var r = WBlend(up2, s, (u - 0.45f) / 0.55f); r.Sneeze = Lerp(1f, 0f, Sm((u - 0.45f) / 0.25f)); return r; }, 1.8f, 1.2f),
        ];
    }

    /// <summary>
    /// UP AGAINST A TREE (the wolf that chased a squirrel up a pine): reared on the hind legs, the body pitched about 52 degrees nose up, the hind paws
    /// planted well under the haunch, the forepaws up high ahead of the chest as if on the trunk (they scrape a little, one then the other), the head
    /// thrown back with the muzzle at the squirrel, the ears pricked, the brush hanging. "p" 0..1 is the hold's cycle (page 0 is the pose the rear-in ends on).
    /// Checked against the legs' reach: the hip is about 0.52 m up and the shoulder 1.13 m, so each two-bone leg has room to bend.
    /// </summary>
    public static WPose WRearPose(float p)
    {
        float a = MathF.Tau * p, s = MathF.Sin(a), s2 = MathF.Sin(2f * a);
        var q = new WPose { BX = 0.34f, BY = 0.86f + 0.008f * s2, Pitch = 52f + 0.8f * s2, Head = P(0.95f + 0.01f * s, 1.50f + 0.012f * s2), HeadTilt = 48f + 3.5f * s, Ear = 62f, TailDown = 72f, TailSway = 7f * MathF.Sin(a + 1f), Jaw = 4f + 3f * MathF.Max(0f, MathF.Sin(2f * a + 0.6f)) };
        q.NH = P(-0.05f, 0f); q.FH = P(-0.17f, 0f);
        // the forepaws scrape up and down the bark in turn
        q.NF = P(0.95f + 0.01f * s, 1.30f + 0.07f * MathF.Max(0f, MathF.Sin(a)));
        q.FF = P(0.88f, 1.20f + 0.07f * MathF.Max(0f, MathF.Sin(a + MathF.PI)));
        return q;
    }

    /// <summary>The dig at the burrow: the forebody low and forward, the muzzle in the mouth, the forepaws scraping in turn.</summary>
    public static WPose WDigPose(float p)
    {
        var q = new WPose { BY = 0.52f, Pitch = -16f, Head = P(1.12f, 0.17f), HeadTilt = -52f, Ear = 100f, TailDown = 14f, Jaw = 0f };
        float a = MathF.Tau * p;
        // the forepaws scrape in turn: reach out, pull back with a lift
        PointF paw(float ph) { float c = Cyc(ph); float x = c < 0.5f ? Lerp(0.98f, 0.58f, c / 0.5f) : Lerp(0.58f, 0.98f, Sm((c - 0.5f) / 0.5f)); float y = c < 0.5f ? 0f : 0.10f * MathF.Sin(MathF.PI * (c - 0.5f) / 0.5f); return P(x, y); }
        q.NF = paw(p); q.FF = paw(p + 0.5f);
        q.NH = P(-0.36f, 0f); q.FH = P(-0.44f, 0f);
        q.BY += 0.006f * MathF.Sin(2f * a); q.Pitch += 0.9f * MathF.Sin(2f * a);
        q.Head = P(q.Head.X + 0.006f * MathF.Sin(2f * a), q.Head.Y);
        return q;
    }

    /// <summary>The catch: the last bound (a pounce), the strike, a tumble in a cloud, the scramble up with the hare in the jaws, and the settle onto the carrying stand.</summary>
    public static WPose WPounceAt(int i)
    {
        float t = i;
        WPose gal = WGallop(0f);
        if (i < 8)
        {
            // THE POUNCE (0.27 s): out of the gallop's landing, the whole body reaches: forelegs thrown forward and down at the prey, hind trailing, jaws open
            var reach = new WPose { BY = 0.70f, Pitch = -26f, Arch = -0.05f, Head = P(1.00f, 0.50f), HeadTilt = -38f, Ear = 140f, TailDown = 4f, Jaw = 18f, Open = 1f, Shadow = 0.6f };
            reach.NF = P(1.02f, 0.10f); reach.FF = P(0.94f, 0.16f); reach.NH = P(-0.80f, 0.32f); reach.FH = P(-0.86f, 0.38f);
            return WBlend(gal, reach, i / 7f);
        }
        var reach2 = new WPose { BY = 0.70f, Pitch = -26f, Arch = -0.05f, Head = P(1.00f, 0.50f), HeadTilt = -38f, Ear = 140f, TailDown = 4f, Jaw = 18f, Open = 1f, Shadow = 0.6f };
        reach2.NF = P(1.02f, 0.10f); reach2.FF = P(0.94f, 0.16f); reach2.NH = P(-0.80f, 0.32f); reach2.FH = P(-0.86f, 0.38f);
        // the tucked, tumbling body: legs drawn in round the trunk
        var tuck = new WPose { BY = 0.40f, Pitch = 0f, Arch = 0.10f, Head = P(0.78f, 0.34f), HeadTilt = -50f, Ear = 150f, TailDown = 60f, Jaw = 6f, Shadow = 0.3f };
        tuck.NF = P(0.52f, 0.20f); tuck.FF = P(0.44f, 0.30f); tuck.NH = P(-0.20f, 0.10f); tuck.FH = P(-0.30f, 0.24f);
        var land = new WPose { BY = 0.30f, Pitch = 0f, Head = P(0.88f, 0.42f), HeadTilt = -22f, Ear = 130f, TailDown = 50f, Carry = true, Shadow = 0.6f };
        land.NF = P(0.52f, 0f); land.FF = P(0.46f, 0f); land.NH = P(-0.30f, 0f); land.FH = P(-0.40f, 0f);
        if (i < 12) { var q = WBlend(reach2, tuck, (i - 7) / 5f); q.Spin = -40f * Sm((i - 7) / 5f); return q; }
        if (i < 28)
        {
            // the roll: a full forward turn about the trunk's middle, the legs flailing a little as it goes
            float u = (i - 12) / 15f;
            var q = WBlend(tuck, tuck, 0f);
            q.Spin = Lerp(-40f, -330f, Sm(u));
            q.NF = P(0.52f + 0.12f * MathF.Sin(u * 9f), 0.22f + 0.10f * MathF.Cos(u * 7f)); q.FF = P(0.44f + 0.12f * MathF.Sin(u * 9f + 1f), 0.28f + 0.10f * MathF.Cos(u * 7f + 1f));
            q.NH = P(-0.22f + 0.10f * MathF.Sin(u * 8f), 0.14f + 0.08f * MathF.Cos(u * 6f)); q.FH = P(-0.30f + 0.10f * MathF.Sin(u * 8f + 1.4f), 0.26f + 0.08f * MathF.Cos(u * 6f + 1f));
            return q;
        }
        if (i < 32)
        {
            // the roll's end: the spin finishes (-330 to -360, which looks exactly like 0), the legs reach for the snow
            float u = (i - 27) / 4f;
            var tk = WBlend(tuck, tuck, 0f);
            var q = WBlend(tuck, land, u); q.Spin = Lerp(-330f, -360f, Sm(u)); q.Carry = i >= 30;
            return q;
        }
        // the scramble up: body rising from low to the carrying stand, the head lifting, the hare swinging in the jaws
        float k = (i - 32) / (float)(NPounce - 33);
        var up = WBlend(land, WStand(0f, true), k);
        up.Carry = true; up.Swing = Lerp(18f, 0f, Sm(k)) * MathF.Cos(k * 8f);
        return up;
    }

    // ================================================================ THE POSE SHEET

    /// <summary>PECKWORKS_SHEET=predators;path.png: every page of every flip-book, labelled, on the snow's colour; and a night strip.</summary>
    public static void WriteSheet(string path)
    {
        var wolf = WolfBooks(); var owl = OwlBooks();
        var all = new List<BookSpec>(); all.AddRange(wolf); all.AddRange(owl);
        float k = 74f;
        int perRow = 10, margin = 6;
        int cellW = (int)(2 * all.Max(b => b.HalfW) * k) + margin, cellH = (int)(all.Max(b => b.Above) * k) + 30;
        int rows = all.Sum(b => (b.N + perRow - 1) / perRow);
        using var bmp = new Bitmap(cellW * perRow, cellH * rows + 8, PixelFormat.Format32bppArgb);
        using var night = new Bitmap(cellW * perRow, cellH * ((all.Count + perRow - 1) / perRow), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var gn = Graphics.FromImage(night))
        {
            g.Clear(Color.FromArgb(222, 233, 252)); gn.Clear(Color.FromArgb(20, 36, 92));
            g.SmoothingMode = SmoothingMode.AntiAlias; gn.SmoothingMode = SmoothingMode.AntiAlias;
            int row = 0, bi = 0;
            using var font = new Font("Consolas", 9f);
            foreach (BookSpec b in all)
            {
                for (int i = 0; i < b.N; i++)
                {
                    int cx = (i % perRow) * cellW + cellW / 2, cy = (row + i / perRow) * cellH + cellH - 12;
                    g.DrawString(i == 0 ? $"{b.Name} ({b.N})" : i.ToString(), font, Brushes.Black, cx - cellW / 2 + 2, cy - cellH + 12);
                    var st = g.Save();
                    g.TranslateTransform(cx, cy); g.ScaleTransform(k, -k);
                    b.Paint(g, i);
                    g.Restore(st);
                    g.DrawLine(Pens.Gray, cx - cellW / 2, cy, cx - cellW / 2 + 6, cy);
                }
                int nx = (bi % perRow) * cellW + cellW / 2, ny = (bi / perRow) * cellH + cellH - 12;
                var sn = gn.Save();
                gn.TranslateTransform(nx, ny); gn.ScaleTransform(k, -k);
                b.Paint(gn, 0);
                gn.Restore(sn);
                gn.DrawString(b.Name, font, Brushes.White, nx - cellW / 2 + 2, ny - cellH + 12);
                row += (b.N + perRow - 1) / perRow; bi++;
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        bmp.Save(path, ImageFormat.Png);
        night.Save(Path.ChangeExtension(path, null) + "_night.png", ImageFormat.Png);
    }
}
