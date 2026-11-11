using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// THE ART OF THE PREDATORS, PART TWO: THE OWL (and the prey it carries). Same static class as PredatorsArt.cs.
//
// THE OWL, looked up before it was drawn (a great grey owl, 1.4 m across the wings):
//   - a big ROUND head (a third of the body's length), a pale FACE DISC with a darker ring, small yellow eyes, a tiny
//     hooked beak; a body like an egg, feathered all the way down to the feet ("trousers"), a long square tail.
//   - the WINGS are broad and ROUNDED (a chord of 0.28 m at the root), seven or eight long finger feathers at the tip
//     that splay when it soars; the beat is SLOW and deep (a metre of wing, so about 1.5 beats a second at most), the
//     glide is long and tilting. Barred in grey-brown, pale underneath.
//   - in the SIDE view the body points along x (it faces right, the saver mirrors it); the near wing is a big fan
//     rising from the shoulder (dihedral in a glide, straight up at the top of the beat, down and forward at the bottom);
//     the far wing sits behind it, a shade darker and a little foreshortened.
//   - THE STOOP. Wings half folded and swept back, the body pitched down; at the last instant the BRAKE: the body swings
//     up, the wings thrown wide and back, the tail fanned, the legs swung out forward with the talons spread (the picture
//     people know). The strike: the wings MANTLE forward and down over the spot. The lift-off is three heavy beats.
//   - A PERCHED owl is upright (body pitched 72 degrees), head turning far round on a neck that has no limit (a
//     page for every 3 degrees of yaw) with long stops.
//
// EVERY PAGE'S ORIGIN is the BODY CENTRE, shifted up by OwlBase inside the page (a Book's origin is a row near the
// bottom of its sheet): the saver stamps a page at (the body's x, the body's y + OwlBase), so wings, legs and the prey
// all have room under the origin. The shadow is not on a page: it is drawn from the owl's height (Predators.cs).
// ============================================================================

/// <summary>One owl pose. Angles in degrees, counter-clockwise, x forward, y up.</summary>
internal sealed class OPose
{
    public float Pitch = 0f;                       // the body's long axis (plus = nose up); 72 is perched
    public float Rx = 0.27f, Ry = 0.155f;          // the body ellipse (long and short half axes)
    public PointF Head = new(0.30f, 0.06f);        // the head's middle in the body's own frame (before the pitch)
    public float HeadYaw, HeadTilt, Blink, Beak;   // head turned toward us (0 side, 90 face on, 180 away), head tilt (plus = nose up, relative), eye shut 0..1, beak open
    public float WA = 28f, WFold = 0.05f, WLag, WSpread = 0.6f, WScaleN = 1f, WScaleF = 1f, FarOff = 9f;   // the near wing: arm angle, fold of the hand, hand lag, finger splay; foreshortening of the near and far wing; the far wing's extra angle
    public float TailDown = 12f, TailFan = 0.2f;   // the tail's angle below straight back, and how fanned
    public float Reach;                            // the legs: 0 tucked up in the belly feathers, 1 swung out forward and down with the talons spread; negative = hanging straight down (perched)
    public float Stand;                            // 1 = the legs are the perch's (straight down under the body to the ground line)
    public float Prey;                             // 0 none, 1 a mouse, 2 a hare (drawn by Predators.cs as an overlay; the talon point is the page's Aux)
}

internal static partial class PredatorsArt
{
    public const float OwlBase = 0.62f;            // metres: how far under the body's centre a page's origin is
    public static readonly Color OBody = Color.FromArgb(150, 148, 152), OBelly = Color.FromArgb(212, 208, 204), ODark = Color.FromArgb(88, 84, 92), OFarWing = Color.FromArgb(104, 102, 110),
                                 ODisc = Color.FromArgb(214, 210, 204), ORing = Color.FromArgb(120, 116, 118), OEye = Color.FromArgb(255, 216, 44), OBeak = Color.FromArgb(214, 190, 120), OWing = Color.FromArgb(158, 156, 160);

    static float Deg(float r) => r * 57.2958f;

    /// <summary>The talon point of a pose (where a prey hangs from), in the page's own metres with the body centre at the origin.</summary>
    public static PointF OTalons(OPose q)
    {
        if (q.Stand > 0.5f) return Add(P(0f, 0f), Rot(P(0.02f, -0.30f), q.Pitch - 72f));
        PointF hip = Rot(P(-0.04f, -0.08f), q.Pitch);
        PointF foot = Rot(P(Lerp(-0.22f, 0.40f, Math.Clamp(q.Reach, 0f, 1f)), Lerp(-0.14f, -0.30f, Math.Clamp(q.Reach, 0f, 1f))), q.Pitch * (q.Reach > 0.3f ? 0.35f : 1f));
        return Add(hip, foot);
    }

    static void WingPart(Graphics g, PointF root, float alpha, float fold, float lag, float spread, float scale, Color tone, Color bar, bool near)
    {
        float a = alpha * MathF.PI / 180f;
        PointF u = P(MathF.Cos(a), MathF.Sin(a));
        PointF v = P(MathF.Sin(a), -MathF.Cos(a));                                  // the leading side
        if (v.X < -0.05f || (MathF.Abs(v.X) <= 0.05f && v.Y < 0)) v = P(-v.X, -v.Y);
        PointF trail = P(-v.X, -v.Y);
        float s = (u.X * trail.Y - u.Y * trail.X) >= 0 ? 1f : -1f;                   // which way folding turns the hand (toward the trailing side)
        PointF wr = P(root.X + u.X * 0.34f * scale, root.Y + u.Y * 0.34f * scale);
        float hAng = alpha + s * fold * 105f + lag;
        float ha = hAng * MathF.PI / 180f;
        PointF uh = P(MathF.Cos(ha), MathF.Sin(ha));
        PointF vh = P(MathF.Sin(ha), -MathF.Cos(ha));
        if (vh.X < -0.05f || (MathF.Abs(vh.X) <= 0.05f && vh.Y < 0)) vh = P(-vh.X, -vh.Y);
        PointF tipC = P(wr.X + uh.X * 0.40f * scale, wr.Y + uh.Y * 0.40f * scale);
        // the arm: a broad rounded piece
        PointF L0 = P(root.X + v.X * 0.065f, root.Y + v.Y * 0.065f), T0 = P(root.X + trail.X * 0.19f, root.Y + trail.Y * 0.19f);
        PointF L1 = P(wr.X + v.X * 0.05f, wr.Y + v.Y * 0.05f), T1 = P(wr.X + trail.X * 0.16f * scale, wr.Y + trail.Y * 0.16f * scale);
        Blob(g, tone, L0, P((L0.X + L1.X) / 2 + v.X * 0.01f, (L0.Y + L1.Y) / 2 + v.Y * 0.01f), L1, T1, P((T0.X + T1.X) / 2, (T0.Y + T1.Y) / 2), T0);
        // the hand: fingered feathers fanning from the wrist, the longest in the middle
        PointF vt = P(-vh.X, -vh.Y);
        for (int k = 0; k < 7; k++)
        {
            float f = k / 6f;                                                        // 0 leading edge .. 1 trailing edge
            float ang = hAng + s * (f - 0.2f) * 22f * (0.4f + spread) - s * 4f;
            float aa = ang * MathF.PI / 180f;
            float len = (0.34f + 0.12f * MathF.Sin(MathF.PI * (0.15f + 0.7f * f))) * scale;
            PointF b0 = P(wr.X + vt.X * 0.14f * f * scale + vh.X * 0.035f * (1f - f), wr.Y + vt.Y * 0.14f * f * scale + vh.Y * 0.035f * (1f - f));
            PointF b1 = P(b0.X + MathF.Cos(aa) * len, b0.Y + MathF.Sin(aa) * len);
            EllSeg(g, b0, b1, 0.085f, k % 2 == 0 ? tone : Mix(tone, bar, 0.3f), 0.0f);
            Ell(g, Lerp(b0, b1, 0.92f), 0.03f, 0.034f, bar, ang);                    // the dark feather tips
        }
        // the soft bars across the arm
        for (int k = 1; k <= 2; k++)
        {
            PointF c = Lerp(root, wr, 0.28f * k + 0.05f);
            EllSeg(g, P(c.X + v.X * 0.04f, c.Y + v.Y * 0.04f), P(c.X + trail.X * 0.15f, c.Y + trail.Y * 0.15f), 0.028f, Color.FromArgb(near ? 150 : 110, bar));
        }
    }

    public static void PaintOwl(Graphics g, OPose q)
    {
        g.TranslateTransform(0f, OwlBase);                                          // the body's middle is OwlBase above the page's origin
        PointF O = P(0f, 0f);
        PointF B(float f, float u) => Rot(P(f, u), q.Pitch);                       // a point in the body's frame -> page
        PointF shoulder = B(0.06f, 0.10f);

        // ---- far wing and the tail behind ----
        WingPart(g, B(0.04f, 0.115f), q.WA + q.FarOff, q.WFold, q.WLag, q.WSpread, q.WScaleF * 0.92f, OFarWing, ODark, false);
        {
            // the tail: long and square, fanned out and down at the brake
            float td = (180f + q.TailDown) * MathF.PI / 180f;
            PointF root = B(-0.22f, 0.0f);
            float fan = q.TailFan;
            PointF tip = P(root.X + 0.30f * MathF.Cos(td), root.Y + 0.30f * MathF.Sin(td));
            PointF w = P(-MathF.Sin(td), MathF.Cos(td));
            float wd = 0.055f + 0.10f * fan;
            Blob(g, Mix(OBody, ODark, 0.25f), P(root.X + w.X * 0.06f, root.Y + w.Y * 0.06f), P(tip.X + w.X * wd, tip.Y + w.Y * wd), P(tip.X - w.X * wd, tip.Y - w.Y * wd), P(root.X - w.X * 0.06f, root.Y - w.Y * 0.06f));
            Ell(g, Lerp(root, tip, 0.55f), 0.03f, wd * 0.9f, Color.FromArgb(110, ODark), Deg(td) + 90f);
            Ell(g, Lerp(root, tip, 0.82f), 0.028f, wd * 0.9f, Color.FromArgb(110, ODark), Deg(td) + 90f);
        }

        // ---- legs: feathered trousers; tucked, reaching with the talons spread, or the perch's straight pair ----
        void Leg(float lat, Color tone)
        {
            PointF hip = B(-0.04f + lat * 0.02f, -0.09f);
            PointF foot;
            if (q.Stand > 0.5f) foot = Add(hip, Rot(P(0.02f + lat * 0.03f, -0.28f), q.Pitch - 72f + 0f));
            else
            {
                float r = Math.Clamp(q.Reach, 0f, 1f);
                foot = Add(B(0, 0), Rot(P(Lerp(-0.18f, 0.40f, r) + lat * 0.03f, Lerp(-0.13f, -0.31f, r)), q.Pitch * (r > 0.3f ? 0.35f : 1f)));
            }
            EllSeg(g, hip, foot, 0.115f, tone, 0.01f);                              // the feathered leg
            Ell(g, foot.X, foot.Y, 0.05f, 0.04f, Mix(tone, OBelly, 0.5f));
            if (q.Reach > 0.35f || q.Stand > 0.5f)
            {
                float spread = q.Stand > 0.5f ? 0.4f : q.Reach;
                for (int k = 0; k < 3; k++)                                         // the talons: dark curved claws, spread when it brakes
                {
                    float aa = (-100f + (k - 1) * 26f * spread + (q.Stand > 0.5f ? 40f : 0f)) * MathF.PI / 180f;
                    PointF tt = P(foot.X + 0.075f * MathF.Cos(aa), foot.Y + 0.075f * MathF.Sin(aa));
                    Limb(g, foot, tt, 0.022f, Color.FromArgb(52, 46, 52));
                }
            }
        }
        Leg(-1f, Mix(OBody, OFarWing, 0.5f));

        // ---- the body, the pale breast with soft bars, the near leg ----
        Ell(g, O, q.Rx, q.Ry, OBody, q.Pitch);
        Ell(g, B(0.02f, -0.045f), q.Rx * 0.82f, q.Ry * 0.62f, OBelly, q.Pitch);
        for (int k = 0; k < 4; k++) Ell(g, B(-0.12f + 0.09f * k, -0.045f), 0.014f, q.Ry * 0.38f, Color.FromArgb(110, ODark), q.Pitch + 10f);
        Ell(g, B(-0.06f, 0.07f), q.Rx * 0.72f, q.Ry * 0.4f, Mix(OBody, ODark, 0.35f), q.Pitch);              // the darker back
        Leg(1f, OBody);

        // ---- the head, last of the body, then the near wing over the shoulder ----
        float hy = q.HeadYaw * MathF.PI / 180f, hc = MathF.Cos(hy), hs = MathF.Sin(hy);
        PointF hd = B(q.Head.X, q.Head.Y);
        float hr = 0.118f;
        float tiltAll = q.HeadTilt;
        PointF HP(float f, float u, float lat = 0f) { PointF r = Rot(P(f * hc + lat * hs, u), tiltAll); return P(hd.X + r.X, hd.Y + r.Y); }
        Ell(g, hd, hr * 1.03f, hr * 0.98f, OBody, 0f);
        // the face disc: a pale oval on the front of the head (edge-on in profile, a full round face when it looks at us)
        if (hc > -0.2f)
        {
            float vis = Math.Clamp(hc + 0.25f, 0f, 1f);
            PointF dc = HP(0.035f, -0.005f, 0f);
            float drx = 0.045f + 0.075f * hs;
            Ell(g, dc.X, dc.Y, drx, 0.105f, ORing, 0f);
            Ell(g, dc.X, dc.Y, drx * 0.9f, 0.095f, ODisc, 0f);
            // the two facial discs meet in a pale "V" over the beak
            if (hs > 0.5f) { Ell(g, dc.X, dc.Y + 0.01f, 0.012f, 0.085f, Color.FromArgb(150, ORing)); }
            // eyes
            float shut = Math.Clamp(q.Blink, 0f, 1f);
            void Eye(PointF e)
            {
                if (shut > 0.6f) { Ell(g, e, 0.024f, 0.005f, ODark); return; }
                Ell(g, e, 0.024f, 0.022f * (1f - shut), OEye);
                Ell(g, e, 0.011f, 0.011f * (1f - shut), Color.FromArgb(24, 20, 22));
                Ell(g, e.X + 0.006f, e.Y + 0.006f, 0.004f, 0.004f, Color.FromArgb(255, 255, 248, 214));
            }
            Eye(HP(0.075f, 0.025f, 0.055f));
            if (hs > 0.25f) Eye(HP(0.075f, 0.025f, -0.065f));
            // the beak: tiny, hooked, between the eyes and a little low
            PointF bk = HP(0.115f, -0.025f, 0f);
            PointF bk2 = P(bk.X + 0.03f * hc, bk.Y - 0.032f - 0.012f * q.Beak);
            Poly(g, OBeak, P(bk.X - 0.012f, bk.Y + 0.018f), P(bk.X + 0.022f * hc + 0.012f, bk.Y), bk2, P(bk.X - 0.012f, bk.Y - 0.018f));
        }
        else Ell(g, hd.X, hd.Y, hr * 0.8f, hr * 0.8f, Mix(OBody, ODark, 0.25f));     // the back of the head
        // the ear tufts are not an owl's (a great grey has none): a soft crown of darker feathers
        Ell(g, HP(-0.02f, 0.085f), 0.07f, 0.03f, Mix(OBody, ODark, 0.4f), q.HeadTilt);

        WingPart(g, shoulder, q.WA, q.WFold, q.WLag, q.WSpread, q.WScaleN, OWing, ODark, true);
    }

    // ================================================================ THE OWL'S POSES

    /// <summary>The flapping flight at phase p (0 and 0.5: the wings pass through the glide's angle). amp 1 = a full slow beat, 0 = soaring.</summary>
    public static OPose OFly(float p, float amp, float bank = 0f, float tremor = 0f, bool labour = false)
    {
        float a = MathF.Tau * p, s = MathF.Sin(a), c = MathF.Cos(a);
        var q = new OPose();
        float A1 = labour ? 82f : 62f;
        q.WA = 28f + amp * A1 * s + bank * 6f + tremor * 1.5f * MathF.Sin(3f * a);
        q.FarOff = 9f + amp * 6f * s;
        q.WLag = -amp * (labour ? 38f : 28f) * c + tremor * 3f * MathF.Sin(5f * a);
        q.WFold = 0.05f + amp * 0.40f * MathF.Max(0f, s) + (labour ? 0.1f : 0f);
        q.WSpread = labour ? 0.9f : 0.55f + 0.2f * (1f - amp);
        q.WScaleN = 1f + 0.22f * bank; q.WScaleF = 1f - 0.22f * bank;
        q.Pitch = (labour ? 14f : 2f) + amp * 3.5f * c * (labour ? 1.6f : 1f);
        q.TailDown = 10f - amp * 5f * s; q.TailFan = 0.2f;
        q.Head = P(0.30f, 0.06f);
        q.HeadTilt = -q.Pitch;                                                     // owls keep the head level whatever the body does
        q.Reach = labour ? 0.55f : 0f;
        return q;
    }

    public static OPose OPerch(float t = 0f, bool onGround = false)
    {
        float s = MathF.Sin(MathF.Tau * t);
        var q = new OPose { Pitch = 72f, Rx = 0.27f, Ry = 0.165f + 0.006f * s, Head = P(0.31f, 0.0f), WA = -68f, WFold = 1f, WSpread = 0f, WScaleN = 0.8f, WScaleF = 0.8f, FarOff = 6f, TailDown = 62f + 3f * MathF.Sin(2f * MathF.Tau * t), TailFan = 0.1f, Stand = 1f };
        q.HeadTilt = -72f + 3f * s;
        if (t > 0.40f && t < 0.46f) q.Blink = MathF.Sin((t - 0.40f) / 0.06f * MathF.PI);
        return q;
    }

    public static OPose OBlend(OPose a, OPose b, float t)
    {
        float s = Sm(t);
        return new OPose
        {
            Pitch = Lerp(a.Pitch, b.Pitch, s), Rx = Lerp(a.Rx, b.Rx, s), Ry = Lerp(a.Ry, b.Ry, s), Head = Lerp(a.Head, b.Head, s), HeadYaw = Lerp(a.HeadYaw, b.HeadYaw, s), HeadTilt = Lerp(a.HeadTilt, b.HeadTilt, s),
            Blink = Lerp(a.Blink, b.Blink, s), Beak = Lerp(a.Beak, b.Beak, s), WA = Lerp(a.WA, b.WA, s), WFold = Lerp(a.WFold, b.WFold, s), WLag = Lerp(a.WLag, b.WLag, s), WSpread = Lerp(a.WSpread, b.WSpread, s),
            WScaleN = Lerp(a.WScaleN, b.WScaleN, s), WScaleF = Lerp(a.WScaleF, b.WScaleF, s), FarOff = Lerp(a.FarOff, b.FarOff, s), TailDown = Lerp(a.TailDown, b.TailDown, s), TailFan = Lerp(a.TailFan, b.TailFan, s),
            Reach = Lerp(a.Reach, b.Reach, s), Stand = Lerp(a.Stand, b.Stand, s), Prey = b.Prey,
        };
    }

    /// <summary>The stoop pose: wings half folded and swept back, the body pitched down, the head steady on the prey.</summary>
    public static OPose OStoopPose(float tremor = 0f)
    {
        var q = new OPose { Pitch = -40f, WA = 52f + tremor * 2.5f, WFold = 0.82f, WLag = 6f, WSpread = 0.2f, WScaleN = 0.95f, WScaleF = 0.88f, FarOff = 8f, TailDown = 4f, TailFan = 0.3f, Reach = 0.15f };
        q.Head = P(0.31f, 0.045f); q.HeadTilt = 24f;
        return q;
    }

    /// <summary>THE BRAKE: wings thrown wide up and back, the tail fanned and swung down, the legs out forward, the talons spread: the picture people know.</summary>
    public static OPose OBrakePose(float flutter = 0f)
    {
        var q = new OPose { Pitch = 38f, WA = 82f + flutter * 3f, WFold = 0f, WLag = -10f, WSpread = 1f, WScaleN = 1.08f, WScaleF = 1.0f, FarOff = -12f, TailDown = 64f, TailFan = 1f, Reach = 1f };
        q.Head = P(0.31f, 0.02f); q.HeadTilt = -38f; q.Beak = 0.3f;
        return q;
    }

    /// <summary>The mantle: the wings drawn forward and down over the spot (over the prey), the body low, the head bowed, the tail down.</summary>
    public static OPose OMantlePose(float t = 0f)
    {
        float s = MathF.Sin(MathF.Tau * t);
        var q = new OPose { Pitch = 52f, WA = -38f + 3f * s, WFold = 0.28f, WLag = 10f, WSpread = 0.8f, WScaleN = 1.15f, WScaleF = 1.0f, FarOff = 10f, TailDown = 55f, TailFan = 0.7f, Reach = 0.5f };
        q.Head = P(0.31f, 0.0f); q.HeadTilt = -64f + 5f * MathF.Sin(2f * MathF.Tau * t) + 3f * s; q.HeadYaw = 20f * MathF.Sin(MathF.Tau * t); q.Rx = 0.26f;
        return q;
    }

    // ================================================================ THE OWL'S SHEETS
    public const int OPerchB = 0, OSwivelB = 1, OFlapB = 2, OGlideB = 3, OFlapGlide = 4, OGlideFlap = 5, OStoopIn = 6, OStoopHold = 7, OBrakeB = 8, OMissLand = 9, OMantleIn = 10, OMantleIdle = 11, OHeave = 12, OLabourB = 13, OTakeoff = 14, OLandB = 15, OLookDown = 16, OHopB = 17,
                     OPreyMouse = 18, OPreyHare = 19, OPreySquirrel = 20;
    public const int NPerch = 36, NSwivel = 61, NFlap = 24, NGlide = 48, NFlapGlide = 24, NGlideFlap = 24, NStoopIn = 24, NStoopHold = 12, NBrake = 24, NMissLand = 28, NMantleIn = 22, NMantleIdle = 24, NHeave = 20, NLabour = 24, NTakeoff = 24, NLand = 30, NLookDown = 24, NHop = 16, NPrey = 7;

    /// <summary>The heave: out of the mantle, three heavy beats' worth of lift in one sheet (wings up wide, then a hard down-stroke), ending on the labouring flight at page NLabour / 2.</summary>
    static OPose OHeaveAt(float u)
    {
        OPose m = OMantlePose(0f), top = OFly(0.25f, 1.1f, 0f, 0f, true), mid = OFly(0.5f, 1.25f, 0f, 0f, true);
        top.WA = 96f; top.WFold = 0.45f; top.Pitch = 30f; top.Reach = 0.8f;
        if (u < 0.45f) return OBlend(m, top, u / 0.45f);
        return OBlend(top, mid, (u - 0.45f) / 0.55f);
    }

    public static BookSpec[] OwlBooks()
    {
        BookSpec Make(string name, int n, Func<int, OPose> pose) => new()
        {
            Name = name, N = n, HalfW = 1.05f, Above = 1.55f + OwlBase,
            Paint = (g, i) => PaintOwl(g, pose(i)),
            Aux = i => OTalons(pose(i)),
        };
        OPose perch0 = OPerch(0f), glide0 = OFly(0f, 0f), flap0 = OFly(0f, 1f), stoop0 = OStoopPose(), brake0 = OBrakePose(), mantle0 = OMantlePose(0f);
        return
        [
            Make("perch", NPerch, i => OPerch(i / (float)NPerch)),
            Make("swivel", NSwivel, i => { var q = OPerch(0f); q.HeadYaw = i * 3f; q.Head = P(0.31f, 0.0f); return q; }),
            Make("flap", NFlap, i => OFly(i / (float)NFlap, 1f)),
            Make("glide", NGlide, i => { float p = i / (float)NGlide; return OFly(0f, 0f, 1.4f * MathF.Sin(MathF.Tau * p), MathF.Sin(MathF.Tau * p * 3f)); }),
            Make("flap to glide", NFlapGlide, i => { float u = i / (float)NFlapGlide; return OFly(u, 1f - Sm(u)); }),
            Make("glide to flap", NGlideFlap, i => { float u = i / (float)NGlideFlap; return OFly(u, Sm(u)); }),
            Make("stoop in", NStoopIn, i => OBlend(glide0, stoop0, i / (float)(NStoopIn - 1))),
            Make("stoop hold", NStoopHold, i => OStoopPose(MathF.Sin(MathF.Tau * i / NStoopHold))),
            Make("brake", NBrake, i => { float u = i / (float)(NBrake - 1); return OBlend(stoop0, OBrakePose(MathF.Sin(u * 7f)), u); }),
            Make("miss land", NMissLand, i => { float u = i / (float)(NMissLand - 1); var b = OBrakePose(); var p = OPerch(0f); p.Stand = 1f; var q = OBlend(b, p, u); return q; }),
            Make("mantle in", NMantleIn, i => OBlend(brake0, mantle0, i / (float)(NMantleIn - 1))),
            Make("mantle idle", NMantleIdle, i => OMantlePose(i / (float)NMantleIdle)),
            Make("heave", NHeave, i => OHeaveAt(i / (float)(NHeave - 1))),
            Make("labour", NLabour, i => OFly(i / (float)NLabour, 1.25f, 0f, 0f, true)),
            Make("takeoff", NTakeoff, i => { float u = i / (float)(NTakeoff - 1); var crouch = OPerch(0f); crouch.Pitch = 50f; crouch.Ry = 0.17f; crouch.WA = -60f; crouch.WFold = 0.9f; var up = OFly(0.25f, 1f); up.WA = 92f; up.Pitch = 22f; var down = OFly(0.5f, 1f); down.Reach = 0f; return u < 0.25f ? OBlend(perch0, crouch, u / 0.25f) : u < 0.62f ? OBlend(crouch, up, (u - 0.25f) / 0.37f) : OBlend(up, down, (u - 0.62f) / 0.38f); }),
            Make("land", NLand, i => { float u = i / (float)(NLand - 1); var flare = OBrakePose(); flare.Pitch = 58f; flare.WA = 70f; flare.TailFan = 0.9f; return u < 0.4f ? OBlend(glide0, flare, u / 0.4f) : OBlend(flare, perch0, (u - 0.4f) / 0.6f); }),
            Make("look down", NLookDown, i => { float u = i / (float)(NLookDown - 1); float k = u < 0.3f ? Sm(u / 0.3f) : u < 0.7f ? 1f : 1f - Sm((u - 0.7f) / 0.3f); var q = OPerch(0f); q.HeadTilt = -72f - 38f * k; q.Head = P(0.30f, -0.02f * k); q.Blink = (u > 0.5f && u < 0.58f) ? MathF.Sin((u - 0.5f) / 0.08f * MathF.PI) : 0f; return q; }),
            Make("hop", NHop, i => { float u = i / (float)(NHop - 1); var q = OPerch(0f); float k = MathF.Sin(MathF.PI * u); q.Pitch = 72f - 22f * k; q.WA = -68f + 90f * k; q.WFold = 1f - 0.8f * k; q.Ry = 0.165f; q.Stand = 1f - k; q.Reach = 0.5f * k; return q; }),
            new BookSpec { Name = "prey mouse", N = NPrey, HalfW = 0.4f, Above = 0.9f, Paint = (g, i) => { g.TranslateTransform(0f, 0.70f); PaintLimpMouse(g, P(0f, 0f), -18f + 6f * i); } },
            new BookSpec { Name = "prey hare", N = NPrey, HalfW = 0.5f, Above = 0.9f, Paint = (g, i) => { g.TranslateTransform(0f, 0.70f); PaintLimpHare(g, P(0f, 0f), -18f + 6f * i, 1f); } },
            new BookSpec { Name = "prey squirrel", N = NPrey, HalfW = 0.5f, Above = 0.9f, Paint = (g, i) => { g.TranslateTransform(0f, 0.70f); PaintLimpSquirrel(g, P(0f, 0f), -18f + 6f * i); } },
        ];
    }

    /// <summary>A red squirrel hung limp by the scruff (the owl's third kind of prey): a russet body hanging, pale belly, a tufted ear, and the big bushy tail hanging below, its tip curling.</summary>
    public static void PaintLimpSquirrel(Graphics g, PointF hang, float swing)
    {
        var st = g.Save();
        g.TranslateTransform(hang.X, hang.Y); g.RotateTransform(swing);
        Color red = Color.FromArgb(176, 84, 48), dark = Color.FromArgb(124, 56, 36), belly = Color.FromArgb(232, 214, 190);
        // the bushy tail first (behind): a long plume hanging from the rump, curling at its tip
        Blob(g, dark, P(-0.03f, -0.22f), P(0.07f, -0.26f), P(0.12f, -0.42f), P(0.09f, -0.58f), P(0.02f, -0.64f), P(-0.04f, -0.52f), P(-0.04f, -0.36f));
        Blob(g, red, P(-0.02f, -0.24f), P(0.05f, -0.28f), P(0.09f, -0.42f), P(0.06f, -0.55f), P(0.0f, -0.54f), P(-0.03f, -0.40f));
        Blob(g, red, P(-0.07f, -0.02f), P(0.07f, -0.03f), P(0.09f, -0.14f), P(0.06f, -0.27f), P(-0.06f, -0.27f), P(-0.09f, -0.14f));
        Ell(g, P(0.015f, -0.14f), 0.035f, 0.08f, belly);
        Ell(g, P(-0.02f, -0.03f), 0.05f, 0.045f, red, 20f);
        Poly(g, dark, P(-0.055f, 0.0f), P(-0.04f, 0.075f), P(-0.015f, 0.01f));                   // the tufted ear
        EllSeg(g, P(-0.05f, -0.16f), P(-0.10f, -0.30f), 0.035f, dark);                           // a hind leg dangling
        g.Restore(st);
    }

    /// <summary>A mouse hung limp by the scruff: a small brown body, round ear, the long tail hanging.</summary>
    public static void PaintLimpMouse(Graphics g, PointF hang, float swing)
    {
        var st = g.Save();
        g.TranslateTransform(hang.X, hang.Y); g.RotateTransform(swing);
        Color brown = Color.FromArgb(150, 108, 76), belly = Color.FromArgb(224, 206, 178), pink = Color.FromArgb(232, 160, 160);
        Blob(g, brown, P(-0.05f, -0.01f), P(0.05f, -0.02f), P(0.07f, -0.11f), P(0.04f, -0.20f), P(-0.04f, -0.20f), P(-0.07f, -0.10f));
        Ell(g, P(0.015f, -0.10f), 0.03f, 0.065f, belly);
        Ell(g, P(-0.01f, -0.025f), 0.033f, 0.03f, brown, 20f);
        Ell(g, P(-0.03f, -0.005f), 0.022f, 0.026f, brown); Ell(g, P(-0.03f, -0.005f), 0.012f, 0.016f, pink);
        // the tail, a thin curve hanging below
        using (var pen = new Pen(Color.FromArgb(206, 160, 150), 0.018f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(pen, [P(0.0f, -0.19f), P(0.03f, -0.30f), P(-0.02f, -0.40f), P(0.03f, -0.48f)]);
        Ell(g, P(0.0f, -0.20f), 0.03f, 0.022f, Color.FromArgb(244, 168, 170));
        g.Restore(st);
    }
}
