using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// HOW A WOOD MOUSE IS DRAWN (LogMiceArt.cs)
//
// Looked up first (the proportions every shape below comes from):
//   - A wood mouse is about 9 cm from nose to rump, with a tail as long again, and it is the
//     size of a thumb. A "storybook" factor of 2.2 makes it about 20 pixels long on a 1080p
//     screen, so every shape here is round and bold: nothing smaller than a pixel matters.
//   - Fur warm brown on the back, cream on the belly. HUGE round ears (pink inside), big dark
//     bead eyes, a pointed snout with a pink nose, long whiskers, big hind feet, a long thin tail.
//   - It runs in a BOUND: the front feet land, the hind feet land AHEAD of them under the body
//     (the back arches into a hump), then both push off and the body stretches out in a short
//     flight. In a hurry it runs in short DASHES with frozen stops between, never a steady glide.
//     A stopped mouse is a hunched crouch with the nose twitching; it also sits UP on its
//     haunches with the forepaws at its chest, and washes its face with both paws.
//
// FEYNMAN VERSION. A mouse here is a flip-book. One painter (PaintSide) draws a mouse from a
// handful of numbers, a POSE: how far it has risen onto its haunches, where it is in its bound,
// how far the nose has twitched, whether its eyes are shut. The pages of every flip-book are that
// one painter called with different numbers, so a standing mouse and a running one are the SAME
// mouse in two poses, and the join between two flip-books (a stop, a rise onto the haunches) is
// the same body at the same anchor: nothing jumps. Legs are two-bone limbs: the foot is where
// the gait puts it (slide back while it is on the ground, swing forward through the air) and a
// little sum ("IK", the way your elbow goes wherever it must for your hand to touch the table)
// finds where the knee must be.
//
// Everything is in MOUSE UNITS (one unit = the body's length), x forward, y UP, the origin at the
// feet. The caller sets up that frame (see Sheet). The tail is NOT in the pages: it is a live
// chain the resident lays behind the animal every frame, so it trails along the path.
// ============================================================================

/// <summary>Every number a mouse's page is painted from. A page is a pose frozen.</summary>
internal readonly record struct MousePose(
    float Rise = 0f,        // 0 on all fours .. 1 sitting up on the haunches
    float Gait = -1f,       // the bound's phase 0 to 1; -1 = standing (the rest page, all four feet down)
    float Walk = -1f,       // the upright waddle's phase 0 to 1; -1 = not walking upright (the story's mice)
    float Arms = 0f,        // upright only: 0 forepaws at the chest .. 1 held up over the head
    float Lift = 0f,        // head tilt in degrees, nose up positive (negative: nose down to the snow)
    float Nose = -1f,       // sniff phase 0 to 1 (-1: the nose is still)
    bool Blink = false,
    float Ear = 0f,         // 0 to 1: an ear flick (the big ear tips back)
    float Wash = -1f,       // paws-at-the-face phase 0 to 1 (-1: not washing)
    float Box = -1f,        // boxing phase 0 to 1 (forepaws out, one then the other; -1: not boxing)
    float Pitch = 0f);      // the whole body tipped nose up (a leap) or down (a plunge), degrees

/// <summary>Places on a painted page the live parts hang from, in mouse units.</summary>
internal readonly record struct MouseAnchors(PointF Tail, PointF Mouth, PointF Hand);

internal static class LogMiceArt
{
    // ---- the colours (a few flat tones: warm back, cream belly, darker far side, pink details) ----
    private static readonly Color Fur = Color.FromArgb(158, 108, 70);
    private static readonly Color FurDark = Color.FromArgb(112, 74, 52);      // the far legs and the far ear
    private static readonly Color FurLight = Color.FromArgb(186, 134, 92);    // the back where the moon touches it
    private static readonly Color Belly = Color.FromArgb(238, 218, 184);
    private static readonly Color Pink = Color.FromArgb(236, 150, 158);
    private static readonly Color Nose = Color.FromArgb(238, 128, 144);
    private static readonly Color Eye = Color.FromArgb(22, 16, 20);
    public static readonly Color TailColor = Color.FromArgb(196, 140, 124);

    public const float Stride = 1.3f;       // the bound's stride, in body lengths: one full cycle of the flip-book
    public const float RestPhase = 0.275f;  // the bound's phase at which all four feet are on the ground (the rest page)
    public const float WalkStride = 0.62f;  // the upright waddle's stride, body lengths
    public const int RunPages = 24, WalkPages = 8;      // SILKY: the dash cycle has 24 pages (a page for every 15 degrees of the bound)

    // ------------------------------------------------------------------
    //  Little helpers
    // ------------------------------------------------------------------

    private static float Ease(float s) { s = Math.Clamp(s, 0f, 1f); return s * s * (3 - 2 * s); }
    private static float Rad(float deg) => deg * MathF.PI / 180f;

    /// <summary>The point at (u along, v across) of a frame whose origin is o and whose axis points at angle a (radians, y up).</summary>
    private static PointF Fr(PointF o, float a, float u, float v) =>
        new(o.X + u * MathF.Cos(a) - v * MathF.Sin(a), o.Y + u * MathF.Sin(a) + v * MathF.Cos(a));

    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static void Blob(Graphics g, Color c, float tension, params PointF[] pts)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, tension);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    private static void Ell(Graphics g, Color c, float cx, float cy, float rx, float ry)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    private static void Bone(Graphics g, Color c, PointF a, PointF b, float w)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, a, b);
    }

    /// <summary>
    /// Two-bone leg: where the middle joint (elbow, knee) goes so a limb of two bones l1 and l2 reaches from
    /// "root" to "target". "side" picks which way the joint bends (+1 or -1 across the root-to-target line).
    /// </summary>
    private static PointF Joint(PointF root, PointF target, float l1, float l2, int side)
    {
        float dx = target.X - root.X, dy = target.Y - root.Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        d = Math.Clamp(d, 0.02f, l1 + l2 - 0.005f);
        float a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
        float h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - a * a));
        float ux = dx / MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy)), uy = dy / MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
        return new PointF(root.X + ux * a - uy * h * side, root.Y + uy * a + ux * h * side);
    }

    /// <summary>The ground shadow: moonlit snow is blue, so the shadow is a faint blue ellipse (up to about 70 alpha).</summary>
    private static void Shadow(Graphics g, float cx, float rx, int alpha)
    {
        using var b = new SolidBrush(Color.FromArgb(alpha, 54, 72, 140));
        g.FillEllipse(b, cx - rx, -0.075f, rx * 2, 0.15f);
    }

    // ------------------------------------------------------------------
    //  The feet: where the gait puts them
    // ------------------------------------------------------------------

    /// <summary>
    /// A foot's track in the bound: on the ground it slides BACK under the body at the body's own speed (that is
    /// what keeps it planted: the page follows the DISTANCE walked), then it swings forward through the air.
    /// "start" is where it lands, "stanceEnd" the fraction of the cycle it stays down. Returns x and the lift.
    /// </summary>
    private static (float X, float Lift) Foot(float cyc, float start, float stanceEnd, float swingHeight)
    {
        cyc -= MathF.Floor(cyc);
        float end = start - Stride * stanceEnd;                         // how far back it has slid when it lifts
        if (cyc < stanceEnd) return (start - Stride * cyc, 0f);
        float s = (cyc - stanceEnd) / (1f - stanceEnd);
        return (end + (start - end) * Ease(s), swingHeight * MathF.Sin(MathF.PI * s));
    }

    // ------------------------------------------------------------------
    //  THE SIDE VIEW: one mouse, facing right
    // ------------------------------------------------------------------

    public static MouseAnchors PaintSide(Graphics g, MousePose m)
    {
        bool upright = m.Walk >= 0f;
        float rise = upright ? 1f : Math.Clamp(m.Rise, 0f, 1f);
        float rs = Ease(rise);

        // ---- where in the bound ----
        float p = m.Gait >= 0f ? m.Gait - MathF.Floor(m.Gait) : RestPhase;
        bool running = m.Gait >= 0f;
        float a01 = 0.5f + 0.5f * MathF.Cos(MathF.Tau * (p - RestPhase));      // 1 at the gather (hunched), 0 at full stretch
        float arch = running ? 0.15f + 0.40f * a01 : 0.55f;                    // a standing mouse is half hunched
        float flight = running && p > 0.55f ? MathF.Sin(MathF.PI * (p - 0.55f) / 0.45f) : 0f;
        float bob = 0.075f * flight;

        // ---- the torso frame: it pivots about the hips as the mouse rises onto its haunches ----
        float theta = Rad(upright ? 80f : 70f * rs);
        var hip = upright ? new PointF(-0.14f, 0.34f + 0.02f * MathF.Sin(MathF.Tau * m.Walk * 2f)) : new PointF(-0.30f + 0.14f * rs, 0.50f - 0.27f * rs + bob);
        if (upright) hip = new PointF(hip.X + 0.02f * MathF.Sin(MathF.Tau * m.Walk), hip.Y);   // the waddle's sway
        float torsoLen = 0.60f;
        PointF shoulder = Fr(hip, theta, torsoLen, 0f);
        float bodyLean = 0f;

        // whole-body pitch (a leap, a plunge): everything but the shadow turns about the middle
        var st = g.Save();
        if (MathF.Abs(m.Pitch) > 0.01f)
        {
            g.TranslateTransform(0f, 0.28f);
            g.RotateTransform(m.Pitch);
            g.TranslateTransform(0f, -0.28f);
        }

        // ---- the head's direction and place ----
        float psi = Rad(25f * rs + m.Lift + (upright ? 10f : 0f) - (m.Wash >= 0f ? 18f : 0f));
        float nose = m.Nose >= 0f ? MathF.Sin(MathF.Tau * m.Nose) : 0f;
        PointF headC = new(shoulder.X + 0.15f, shoulder.Y + 0.05f + 0.04f * rs + (m.Lift < 0 ? m.Lift / 90f * 0.18f : 0f) + 0.008f * nose);
        if (!upright && rs < 0.01f) headC = new PointF(shoulder.X + 0.17f, shoulder.Y + 0.06f + (m.Lift < 0 ? m.Lift / 90f * 0.22f : 0f) + 0.006f * nose);
        PointF Hd(float u, float v) => Fr(headC, psi, u, v);

        // ---- the feet ----
        float fx0, fl0, fx1, fl1;       // near / far fore foot
        float hx0, hl0, hx1, hl1;       // near / far hind toe
        if (upright)
        {
            // the waddle: two hind feet alternate; the forepaws are held up or at the chest
            (hx0, hl0) = WalkFoot(m.Walk);
            (hx1, hl1) = WalkFoot(m.Walk + 0.5f);
            fx0 = fx1 = 0; fl0 = fl1 = 0;
        }
        else if (running)
        {
            (fx0, fl0) = Foot(p, 0.50f, 0.30f, 0.14f);
            (fx1, fl1) = Foot(p + 0.03f, 0.50f, 0.30f, 0.14f);
            (hx0, hl0) = Foot(p - 0.25f, -0.05f, 0.30f, 0.16f);
            (hx1, hl1) = Foot(p - 0.22f, -0.05f, 0.30f, 0.16f);
        }
        else
        {
            (fx0, fl0) = Foot(RestPhase, 0.50f, 0.30f, 0.14f);
            (hx0, hl0) = Foot(RestPhase - 0.25f, -0.05f, 0.30f, 0.16f);
            (fx1, fl1) = Foot(RestPhase + 0.03f, 0.50f, 0.30f, 0.14f);     // the far feet are the SAME as the bound's own rest page, so a stop joins with no jump
            (hx1, hl1) = Foot(RestPhase - 0.22f, -0.05f, 0.30f, 0.16f);
        }

        // sitting up: the hind feet lie flat and forward, under the haunches
        float sitToe = 0.26f;
        if (!upright)
        {
            hx0 = hx0 + (sitToe - hx0) * rs; hx1 = hx1 + (sitToe - 0.05f - hx1) * rs;
            hl0 *= 1 - rs; hl1 *= 1 - rs;
        }

        // The mouse's far side first (a shade darker): far fore leg, far hind leg, far ear.
        // ---- paws: where the forepaws are ----
        PointF chestPaw = Fr(shoulder, theta, 0.14f, -0.14f);
        PointF farFoot, nearFoot;
        float washP = m.Wash;
        float boxP = m.Box;
        if (upright)
        {
            PointF down = Fr(shoulder, theta, 0.16f, -0.18f);
            PointF up = new(headC.X + 0.16f, headC.Y + 0.30f);
            float ar = Ease(m.Arms);
            nearFoot = Lerp(down, up, ar);
            farFoot = Lerp(down, new PointF(up.X - 0.07f, up.Y - 0.02f), ar);
        }
        else
        {
            nearFoot = Lerp(new PointF(fx0, fl0), chestPaw, rs);
            farFoot = Lerp(new PointF(fx1, fl1), new PointF(chestPaw.X - 0.05f, chestPaw.Y), rs);
            if (washP >= 0f)
            {
                // both paws at the face, rubbing round it in little circles
                float w = MathF.Tau * washP * 2f;
                PointF face = Hd(0.16f + 0.05f * MathF.Cos(w), -0.02f + 0.07f * MathF.Sin(w));
                nearFoot = face;
                farFoot = Hd(0.13f + 0.05f * MathF.Cos(w + 1.6f), -0.03f + 0.07f * MathF.Sin(w + 1.6f));
            }
            else if (boxP >= 0f)
            {
                float b = MathF.Tau * boxP;
                nearFoot = new PointF(shoulder.X + 0.26f + 0.16f * MathF.Max(0f, MathF.Sin(b)), shoulder.Y - 0.02f + 0.04f * MathF.Sin(b));
                farFoot = new PointF(shoulder.X + 0.22f + 0.16f * MathF.Max(0f, MathF.Sin(b + MathF.PI)), shoulder.Y - 0.04f);
            }
        }

        PointF shLeg = Fr(shoulder, theta, -0.04f, -0.10f);
        PointF hipLeg = Fr(hip, theta, 0.05f, -0.06f);
        bool noLift = true;

        // ---- far fore leg ----
        {
            PointF elbow = Joint(shLeg, farFoot, 0.23f, 0.22f, upright || rs > 0.5f ? -1 : 1);
            Bone(g, FurDark, shLeg, elbow, 0.085f); Bone(g, FurDark, elbow, farFoot, 0.07f);
            Ell(g, Color.FromArgb(196, 134, 134), farFoot.X + 0.02f, farFoot.Y + 0.01f, 0.055f, 0.04f);
        }
        // ---- far hind leg ----
        DrawHind(g, FurDark, hipLeg, hx1, hl1, upright);
        // ---- far ear (behind the head) ----
        {
            PointF ec = Hd(-0.10f - 0.04f * m.Ear, 0.27f - 0.03f * m.Ear);
            Ell(g, FurDark, ec.X, ec.Y, 0.17f, 0.19f);
        }

        // ---- the torso: an egg turned about the hips ----
        // points in the torso's own frame (u along the back from the hips, v across, up positive)
        {
            float bump = 0.12f * arch;
            PointF T(float u, float v) => Fr(hip, theta, u, v);
            Blob(g, Fur, 0.55f,
                T(-0.28f, 0.02f), T(-0.20f, 0.30f), T(0.12f, 0.33f + bump), T(0.42f, 0.25f + bump * 0.5f), T(0.64f, 0.12f),
                T(0.70f, -0.06f), T(0.54f, -0.20f), T(0.26f, -0.27f), T(-0.02f, -0.28f), T(-0.22f, -0.18f));
            // the lit back: a lighter stripe along the top where the moon touches it
            Blob(g, FurLight, 0.55f, T(-0.12f, 0.25f), T(0.12f, 0.32f + bump), T(0.42f, 0.24f + bump * 0.5f), T(0.28f, 0.18f + bump * 0.4f), T(0.02f, 0.18f));
            // the cream belly, the lower front of the egg
            Blob(g, Belly, 0.55f, T(0.02f, -0.25f), T(0.26f, -0.26f), T(0.52f, -0.19f), T(0.64f, -0.07f), T(0.40f, -0.10f), T(0.12f, -0.14f));
        }

        // ---- near hind leg and haunch (over the torso) ----
        DrawHind(g, Fur, hipLeg, hx0, hl0, upright);
        Ell(g, Fur, hip.X + 0.05f, hip.Y - 0.03f, 0.15f + 0.05f * rs, 0.13f + 0.07f * rs);    // the thigh's muscle: a big round haunch

        // ---- head ----
        var tipCol = Fur;
        Blob(g, tipCol, 0.55f, Hd(-0.20f, 0.10f), Hd(-0.06f, 0.20f), Hd(0.12f, 0.16f), Hd(0.27f, 0.04f), Hd(0.35f, -0.05f), Hd(0.24f, -0.13f), Hd(0.02f, -0.16f), Hd(-0.17f, -0.08f));
        Blob(g, Belly, 0.55f, Hd(0.02f, -0.15f), Hd(0.24f, -0.12f), Hd(0.33f, -0.05f), Hd(0.14f, -0.07f));
        PointF nosePt = Hd(0.33f + 0.012f * nose, -0.035f);
        Ell(g, Nose, nosePt.X, nosePt.Y, 0.058f, 0.05f);
        // the eye: a round bead with a tiny white glint, or a closed line when it blinks
        PointF eye = Hd(0.12f, 0.055f);
        if (m.Blink)
        {
            using var pen = new Pen(Eye, 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, eye.X - 0.04f, eye.Y, eye.X + 0.04f, eye.Y - 0.01f);
        }
        else
        {
            Ell(g, Eye, eye.X, eye.Y, 0.055f, 0.06f);
            Ell(g, Color.FromArgb(240, 255, 255, 255), eye.X + 0.018f, eye.Y + 0.022f, 0.02f, 0.02f);
        }
        // the near ear: a big round ear, pink inside, tipping back when it flicks
        {
            PointF ec = Hd(-0.02f - 0.05f * m.Ear, 0.29f - 0.04f * m.Ear);
            Ell(g, Fur, ec.X, ec.Y, 0.19f, 0.21f);
            Ell(g, Pink, ec.X + 0.012f, ec.Y - 0.005f, 0.125f, 0.145f);
        }
        // whiskers: a faint fan of three from the muzzle
        {
            PointF w0 = Hd(0.26f, -0.04f);
            float spread = 0.012f * nose;
            using var pen = new Pen(Color.FromArgb(150, 236, 232, 240), 0.045f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = -1; i <= 1; i++)
            {
                PointF e = Hd(0.58f, -0.03f + i * (0.09f + spread));
                g.DrawLine(pen, w0, e);
            }
        }

        // ---- near fore leg (over the body: the paws held at the chest, face or out) ----
        {
            PointF elbow = Joint(shLeg, nearFoot, 0.23f, 0.22f, upright || rs > 0.5f ? -1 : 1);
            Bone(g, Fur, shLeg, elbow, 0.09f); Bone(g, Fur, elbow, nearFoot, 0.075f);
            Ell(g, Pink, nearFoot.X + 0.012f, nearFoot.Y + 0.005f, 0.05f, 0.04f);
        }
        _ = noLift; _ = bodyLean;

        MouseAnchors anch = new(Fr(hip, theta, -0.20f, 0.0f), Hd(0.34f, -0.075f), new PointF((nearFoot.X + farFoot.X) / 2f, (nearFoot.Y + farFoot.Y) / 2f));
        g.Restore(st);
        // the shadow goes on FIRST in the picture's order, but it is not tipped with the body, so draw it behind with a trick:
        // painting order here would hide it under the mouse, so the caller paints Shadow first (see PaintPage).
        return anch;
    }

    /// <summary>A hind leg: the thigh runs forward-down to the knee, the shin back to the hock, and the long hind foot lies forward on the ground.</summary>
    private static void DrawHind(Graphics g, Color c, PointF hipLeg, float toeX, float lift, bool upright)
    {
        float toeY = lift * 0.7f;
        PointF toe = new(toeX, toeY);
        PointF heel = new(toeX - 0.17f, lift * 0.25f + 0.015f);
        PointF hock = new(heel.X + 0.01f, heel.Y + 0.065f);
        PointF knee = Joint(hipLeg, hock, 0.28f, 0.26f, upright ? 1 : -1);
        Bone(g, c, hipLeg, knee, 0.11f);
        Bone(g, c, knee, hock, 0.085f);
        Bone(g, c, hock, heel, 0.07f);
        Blob(g, c == Fur ? Color.FromArgb(238, 176, 170) : Color.FromArgb(196, 134, 134), 0.4f, new PointF(heel.X, heel.Y + 0.045f), new PointF(toe.X - 0.02f, toe.Y + 0.045f), new PointF(toe.X + 0.045f, toe.Y + 0.02f), new PointF(toe.X, toe.Y - 0.005f), new PointF(heel.X, heel.Y - 0.015f));
    }

    private static (float X, float Lift) WalkFoot(float phase)
    {
        // the waddle: down for 0.6 of the cycle, sliding back at the body's speed, then a short step through the air
        float cyc = phase - MathF.Floor(phase);
        const float start = 0.24f, duty = 0.60f;
        float end = start - WalkStride * duty;
        if (cyc < duty) return (start - WalkStride * cyc, 0f);
        float s = (cyc - duty) / (1f - duty);
        return (end + (start - end) * Ease(s), 0.07f * MathF.Sin(MathF.PI * s));
    }

    // ------------------------------------------------------------------
    //  THE FRONT VIEW: the cute one (a turn, a peek out of the hollow, a look at us)
    // ------------------------------------------------------------------

    /// <summary>A mouse seen head on: round face, two big ears, two bead eyes, a pink nose. "snow" lays a little cap of snow on its head.</summary>
    public static void PaintFront(Graphics g, float ear, bool blink, float nose, bool snow, float tilt, float yaw = 0f)
    {
        // YAW (0 head on .. 1 nearly side on, the face turned to +x): a three-quarter view for the turn. Every
        // feature is squeezed toward the middle (c) and slid toward the way the face looks (sh), the far ear and
        // far eye narrow more than the near ones, which is how a round head turning away looks at 20 pixels.
        float c = 1f - 0.5f * yaw, sh = 0.17f * yaw;
        if (yaw > 0f)
        {
            Shadow(g, 0.05f * yaw, 0.38f + 0.2f * yaw, 55);
            Ell(g, Fur, sh * 0.6f, 0.33f, 0.31f + 0.22f * yaw, 0.33f);
            Ell(g, Belly, sh * 1.3f, 0.28f, 0.19f * c + 0.03f, 0.26f);
            Ell(g, Pink, -0.15f * c, 0.03f, 0.09f, 0.045f);
            Ell(g, Pink, 0.15f * c + sh, 0.03f, 0.09f, 0.045f);
            var st2 = g.Save();
            g.TranslateTransform(0f, 0.66f); g.RotateTransform(tilt); g.TranslateTransform(0f, -0.66f);
            Ell(g, FurDark, -0.215f * c + sh * 0.6f, 0.93f, 0.155f * c, 0.165f);                    // the far ear, narrow and dark
            Ell(g, Pink, -0.215f * c + sh * 0.6f, 0.93f, 0.10f * c * 0.8f, 0.11f);
            Ell(g, Fur, 0.215f * c + sh + 0.02f * yaw, 0.93f, 0.155f * (0.7f + 0.3f * c), 0.165f);   // the near ear, nearly full
            Ell(g, Pink, 0.215f * c + sh + 0.02f * yaw, 0.93f, 0.10f * (0.7f + 0.3f * c), 0.11f);
            Ell(g, Fur, sh, 0.70f, 0.27f * (0.8f + 0.2f * c), 0.25f);
            Ell(g, FurLight, sh, 0.78f, 0.18f * c, 0.12f);
            Ell(g, Belly, sh * 1.6f, 0.60f, 0.14f * c + 0.03f, 0.10f);
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = s * 0.115f * c + sh * (s > 0 ? 1.15f : 0.9f), erx = 0.055f * (s > 0 ? 1f : 0.6f + 0.4f * c);
                if (blink) { using var pen = new Pen(Eye, 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(pen, ex - 0.035f, 0.73f, ex + 0.035f, 0.73f); }
                else { Ell(g, Eye, ex, 0.73f, erx, 0.06f); Ell(g, Color.FromArgb(240, 255, 255, 255), ex + 0.017f, 0.755f, 0.02f, 0.02f); }
            }
            Ell(g, Nose, sh * 1.9f, 0.625f, 0.052f, 0.04f);
            g.Restore(st2);
            return;
        }
        Shadow(g, 0f, 0.38f, 55);
        // the body behind the head: a plump egg, cream down the front
        Ell(g, Fur, 0f, 0.33f, 0.31f, 0.33f);
        Ell(g, Belly, 0f, 0.28f, 0.19f, 0.26f);
        // two little feet
        Ell(g, Pink, -0.15f, 0.03f, 0.09f, 0.045f);
        Ell(g, Pink, 0.15f, 0.03f, 0.09f, 0.045f);
        var st = g.Save();
        g.TranslateTransform(0f, 0.66f);
        g.RotateTransform(tilt);
        g.TranslateTransform(0f, -0.66f);
        // ears first (behind the head), big and round, pink inside; "ear" tips them out
        float e = 0.03f * ear;
        Ell(g, Fur, -0.215f - e, 0.93f, 0.155f, 0.165f);
        Ell(g, Fur, 0.215f + e, 0.93f, 0.155f, 0.165f);
        Ell(g, Pink, -0.215f - e, 0.93f, 0.10f, 0.11f);
        Ell(g, Pink, 0.215f + e, 0.93f, 0.10f, 0.11f);
        // the head: a round face, a paler muzzle
        Ell(g, Fur, 0f, 0.70f, 0.27f, 0.25f);
        Ell(g, FurLight, 0f, 0.78f, 0.18f, 0.12f);
        Ell(g, Belly, 0f, 0.60f, 0.14f, 0.10f);
        // the eyes
        for (int s = -1; s <= 1; s += 2)
        {
            if (blink)
            {
                using var pen = new Pen(Eye, 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, s * 0.12f - 0.04f, 0.73f, s * 0.12f + 0.04f, 0.73f);
            }
            else
            {
                Ell(g, Eye, s * 0.115f, 0.73f, 0.055f, 0.06f);
                Ell(g, Color.FromArgb(240, 255, 255, 255), s * 0.115f + 0.017f, 0.755f, 0.02f, 0.02f);
            }
        }
        float nz = 0.012f * nose;
        Ell(g, Nose, 0f, 0.625f + nz, 0.052f, 0.04f);
        using (var pen = new Pen(Color.FromArgb(140, 236, 232, 240), 0.045f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int s = -1; s <= 1; s += 2)
                for (int i = -1; i <= 1; i++)
                    g.DrawLine(pen, s * 0.07f, 0.61f, s * 0.36f, 0.61f + i * 0.07f);
        if (snow)
        {
            // a cap of snow on the head and a dab on each ear and the nose (it has just come up out of the drift)
            Ell(g, Color.FromArgb(240, 244, 255), 0f, 0.93f, 0.22f, 0.08f);
            Ell(g, Color.FromArgb(240, 244, 255), -0.215f, 1.04f, 0.10f, 0.045f);
            Ell(g, Color.FromArgb(240, 244, 255), 0.215f, 1.04f, 0.10f, 0.045f);
            Ell(g, Color.FromArgb(235, 244, 255), 0.02f, 0.585f, 0.05f, 0.03f);
        }
        g.Restore(st);
    }

    // ------------------------------------------------------------------
    //  THE CLIMB OUT OF THE SNOW (Book.Emerge): r runs 0 (a swelling mound, the ear tips just showing) to 1 (standing head on, the snow gone)
    // ------------------------------------------------------------------

    /// <summary>
    /// One page of the climb out. The head-on mouse is painted sunk below the snow line by (1 minus the eased r) of its height with a CLIP so nothing
    /// below the line shows (the snow hides it), a little wriggle of the head while it heaves (the tilt), the cap of snow on its head melting away
    /// by r 0.6, and in front a mound of snow that swells as it pushes up and sags as it climbs out. Page 23 is the head-on standing page.
    /// </summary>
    public static void PaintEmerge(Graphics g, float r)
    {
        float e = Ease(r);
        var st = g.Save();
        g.SetClip(new RectangleF(-2f, 0f, 4f, 2f));
        g.TranslateTransform(0f, -(1f - e) * 0.98f);
        PaintFront(g, r > 0.8f && ((int)(r * 40f)) % 7 == 0 ? 1f : 0f, false, 0f, r < 0.6f, 7f * MathF.Sin(r * 9f) * (1f - r));
        g.Restore(st);
        float hm = (0.17f + 0.22f * MathF.Sin(MathF.PI * MathF.Min(1f, r * 1.25f))) * (1f - e * 0.94f), rx = 0.82f * (1f - 0.35f * e);
        if (hm < 0.012f) return;
        using var shade = new SolidBrush(Color.FromArgb(215, 150, 172, 222));
        g.FillEllipse(shade, -rx, -0.10f, rx * 2f, hm * 0.55f + 0.14f);
        using var snow = new SolidBrush(Color.FromArgb(246, 238, 245, 255));
        g.FillEllipse(snow, -rx * 0.93f, 0f, rx * 1.86f, hm * 2f);
        using var lit = new SolidBrush(Color.FromArgb(255, 248, 252, 255));
        g.FillEllipse(lit, -rx * 0.62f, hm * 0.55f, rx * 0.95f, hm * 0.9f);
    }
    // ------------------------------------------------------------------
    //  Props the live parts use
    // ------------------------------------------------------------------

    /// <summary>A mound of snow, seen from the side, with a blue shadow on its lower rim: what a mouse tunnelling under the snow pushes up. Painted 2 units wide (a unit is "k" pixels).</summary>
    public static Sprite PaintMound(float k, float heightUnits)
    {
        int w = Math.Max(4, (int)MathF.Ceiling(2.0f * k)), h = Math.Max(3, (int)MathF.Ceiling((heightUnits + 0.22f) * k));
        return Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(w / 2f, h - 0.11f * k);
            g.ScaleTransform(k, -k);
            using var shade = new SolidBrush(Color.FromArgb(220, 150, 172, 222));
            g.FillEllipse(shade, -0.95f, -0.10f, 1.9f, heightUnits * 0.55f + 0.14f);
            using var snow = new SolidBrush(Color.FromArgb(246, 238, 245, 255));
            g.FillEllipse(snow, -0.88f, 0.0f, 1.76f, heightUnits * 2f);
            using var lit = new SolidBrush(Color.FromArgb(255, 248, 252, 255));
            g.FillEllipse(lit, -0.6f, heightUnits * 0.55f, 0.9f, heightUnits * 0.9f);
        });
    }

    /// <summary>
    /// The ridge a mouse pushes up as it tunnels under the snow: a NARROW raised line (about a unit and a quarter
    /// wide, a quarter unit high), a bright lit top edge where the moon touches it, and a thin blue shadow line
    /// along its lower side.
    /// </summary>
    public static Sprite PaintRidge(float k)
    {
        int w = Math.Max(5, (int)MathF.Ceiling(1.3f * k)), h = Math.Max(5, (int)MathF.Ceiling(0.62f * k));
        return Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(w / 2f, h - 0.12f * k);
            g.ScaleTransform(k, -k);
            using var shadow = new SolidBrush(Color.FromArgb(240, 92, 120, 192));
            g.FillEllipse(shadow, -0.58f, -0.10f, 1.16f, 0.22f);                    // the blue shadow line under it
            using var body = new SolidBrush(Color.FromArgb(252, 206, 220, 246));
            g.FillEllipse(body, -0.50f, -0.03f, 1.0f, 0.62f);                       // the raised snow, its shaded flank
            using var mid = new SolidBrush(Color.FromArgb(255, 232, 240, 254));
            g.FillEllipse(mid, -0.42f, 0.10f, 0.84f, 0.46f);
            using var lit = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
            g.FillEllipse(lit, -0.30f, 0.30f, 0.56f, 0.20f);                        // the lit top, where the moon touches it
        });
    }

    /// <summary>A little sunflower seed (the mouse's prize): a pale pear with a dark stripe.</summary>
    public static Sprite PaintSeed(float k)
    {
        int s = Math.Max(3, (int)MathF.Ceiling(0.30f * k));
        return Sprite.Paint(s, s, g =>
        {
            g.TranslateTransform(s / 2f, s / 2f);
            g.ScaleTransform(s, -s);
            Blob(g, Color.FromArgb(60, 52, 48), 0.5f, new PointF(-0.46f, 0f), new PointF(0f, 0.26f), new PointF(0.46f, 0.1f), new PointF(0.46f, -0.1f), new PointF(0f, -0.26f));
            Blob(g, Color.FromArgb(232, 226, 206), 0.5f, new PointF(-0.3f, 0f), new PointF(0f, 0.12f), new PointF(0.36f, 0.04f), new PointF(0.36f, -0.04f), new PointF(0f, -0.12f));
        });
    }

    // ------------------------------------------------------------------
    //  THE FLIP-BOOKS
    // ------------------------------------------------------------------

    /// <summary>Which pages exist. Every flip-book is a range in one array (Pages), so a page is addressed by Book and number.</summary>
    public enum Book { Run, Stand, Rise, Sit, Wash, Box, Leap, Plunge, Walk, WalkUp, Front, Head, Turn, TurnUp, Emerge }

    public const int StandPages = 10;      // 0..3 sniff, 4 blink, 5 ear flick, 6 head down, 7 head way down (eating, digging), 8 head up, 9 head high (a look)
    public const int EmergePages = 24;    // the climb out of the snow: the head in a swelling mound, shoulders, up to the standing head-on mouse, the mound sagging away
    public const int TurnPages = 7;        // three-quarter views for a turn, from nearly side on (0) to nearly head on (2)
    public const int RisePages = 16;       // 1/4, 2/4, 3/4 up and the sat page; played forward to sit up and backward to drop
    public const int SitPages = 6;         // sniff cycle with a blink and an ear flick
    public const int WashPages = 24;
    public const int BoxPages = 18;
    public const int PlungePages = 6;      // nose down at 10, 25, 50, 75 and 88 degrees (the first is the bound's own reach, tipped a little)
    public const int FrontPages = 4;       // 0 plain, 1 blink, 2 ear out, 3 snowy head

    /// <summary>One set of pages at one size: a sheet per page, all with the same canvas so the same origin (feet) fits every one.</summary>
    internal sealed class Sheet
    {
        public required Sprite[][] Books;           // by Book
        public required MouseAnchors[][] Anchors;   // the same shape
        public required int OriginX, OriginY;       // the feet's place on every page
        public required float K;                    // pixels in one mouse unit
        public Sprite Page(Book b, int i) => Books[(int)b][Math.Clamp(i, 0, Books[(int)b].Length - 1)];
        public MouseAnchors Anchor(Book b, int i) => Anchors[(int)b][Math.Clamp(i, 0, Anchors[(int)b].Length - 1)];
    }

    private static int PagesIn(Book b) => b switch
    {
        Book.Run => RunPages, Book.Stand => StandPages, Book.Rise => RisePages, Book.Sit => SitPages, Book.Wash => WashPages,
        Book.Box => BoxPages, Book.Leap => 2, Book.Plunge => PlungePages, Book.Walk => WalkPages, Book.WalkUp => WalkPages,
        Book.Front => FrontPages, Book.Head => 1, Book.Turn => TurnPages, Book.TurnUp => 5, Book.Emerge => EmergePages, _ => 1,
    };

    /// <summary>The pose of one page of one book.</summary>
    public static MousePose PoseOf(Book b, int i) => b switch
    {
        Book.Run => new MousePose(Gait: (i / (float)RunPages + RestPhase) % 1f, Nose: 0f),     // page 0 is the rest (all feet down) page
        Book.Stand => i switch
        {
            < 4 => new MousePose(Nose: i / 4f),
            4 => new MousePose(Nose: 0.1f, Blink: true),
            5 => new MousePose(Nose: 0.3f, Ear: 1f),
            6 => new MousePose(Lift: -22f, Nose: 0.2f),
            8 => new MousePose(Lift: 22f, Nose: 0.3f),
            9 => new MousePose(Lift: 40f, Nose: 0.5f, Ear: 1f),
            _ => new MousePose(Lift: -42f, Nose: 0.6f),
        },
        Book.Rise => new MousePose(Rise: (i + 1) / (float)RisePages, Nose: 0f),
        Book.Sit => i switch
        {
            < 4 => new MousePose(Rise: 1f, Nose: i / 4f),
            4 => new MousePose(Rise: 1f, Nose: 0.2f, Blink: true),
            _ => new MousePose(Rise: 1f, Nose: 0.5f, Ear: 1f),
        },
        Book.Wash => new MousePose(Rise: 1f, Wash: i / (float)WashPages, Nose: 0.1f, Blink: i is 8 or 9 or 10, Ear: i is 17 or 18 ? 1f : 0f),
        Book.Box => new MousePose(Rise: 1f, Box: i / (float)BoxPages, Nose: 0.2f),
        Book.Leap => i == 0 ? new MousePose(Gait: 0.80f, Pitch: 12f) : new MousePose(Gait: 0.80f, Pitch: -18f),
        Book.Plunge => new MousePose(Gait: 0.80f, Pitch: -(i == 0 ? 4f : i == 1 ? 10f : i == 2 ? 25f : i == 3 ? 50f : i == 4 ? 75f : 88f)),
        Book.Walk => new MousePose(Walk: i / (float)WalkPages, Arms: 0f, Nose: 0f),
        Book.WalkUp => new MousePose(Walk: i / (float)WalkPages, Arms: 1f, Nose: 0f),
        _ => new MousePose(),
    };

    /// <summary>
    /// Paints every page of every book at "k" pixels per mouse unit. Called on the helper thread (no dice, no state
    /// that changes). The canvas is the same for every page: x from -0.9 to 1.3 units, y from -0.2 up to 1.65.
    /// </summary>
    public static Sheet BuildSheet(float k)
    {
        int w = (int)MathF.Ceiling(2.3f * k) + 1, h = (int)MathF.Ceiling(1.9f * k) + 1;
        int ox = (int)MathF.Round(0.95f * k), oy = (int)MathF.Round(1.68f * k);
        var books = new Sprite[Enum.GetValues<Book>().Length][];
        var anchors = new MouseAnchors[books.Length][];
        foreach (Book b in Enum.GetValues<Book>())
        {
            if (b == Book.Turn)
            {
                // THE TURN, by in-betweens: five key poses (standing side on, the side view squashed lengthwise, two three-quarter heads, head
                // on) with a 50/50 blend of each neighbouring pair between them, so a half turn is nine drawn pictures (seven of them here)
                // and a whole turn there and back is seventeen. The blends are what an animator calls smear frames.
                var keys = new Sprite[3];
                for (int q = 0; q < 3; q++)
                {
                    int qq = q;
                    keys[q] = Sprite.Paint(w, h, g =>
                    {
                        g.TranslateTransform(ox, oy);
                        g.ScaleTransform(k, -k);
                        if (qq == 0)
                        {
                            Shadow(g, 0.05f, 0.5f, 60);
                            var st0 = g.Save(); g.TranslateTransform(0.2f, 0f); g.ScaleTransform(0.84f, 1f); g.TranslateTransform(-0.2f, 0f);
                            PaintSide(g, PoseOf(Book.Stand, 0));
                            g.Restore(st0);
                        }
                        else PaintFront(g, 0f, false, 0f, false, 0f, qq == 1 ? 0.6f : 0.25f);
                    });
                }
                Sprite[] ks = { books[(int)Book.Stand][0], keys[0], keys[1], keys[2], books[(int)Book.Front][0] };
                var seq = new List<Sprite>();
                for (int q = 0; q < 4; q++) { if (q > 0) seq.Add(ks[q]); seq.Add(Mix(ks[q], ks[q + 1], 0.5f)); }
                books[(int)b] = seq.ToArray();
                anchors[(int)b] = new MouseAnchors[seq.Count];
                for (int q = 0; q < seq.Count; q++) anchors[(int)b][q] = anchors[(int)Book.Stand][0];
                continue;
            }
            if (b == Book.TurnUp)
            {
                // the story's upright mouse turning: standing page, two blends toward the squashed page, the squashed page, two blends toward head on
                Sprite sq = Sprite.Paint(w, h, g =>
                {
                    g.TranslateTransform(ox, oy); g.ScaleTransform(k, -k);
                    Shadow(g, 0.05f, 0.5f, 60);
                    var st1 = g.Save(); g.TranslateTransform(0.1f, 0f); g.ScaleTransform(0.72f, 1f); g.TranslateTransform(-0.1f, 0f);
                    PaintSide(g, PoseOf(Book.Walk, 0));
                    g.Restore(st1);
                });
                Sprite w0 = books[(int)Book.Walk][0], f0 = books[(int)Book.Front][0];
                books[(int)b] = new[] { Mix(w0, sq, 0.33f), Mix(w0, sq, 0.67f), sq, Mix(sq, f0, 0.33f), Mix(sq, f0, 0.67f) };
                anchors[(int)b] = new MouseAnchors[5];
                for (int q = 0; q < 5; q++) anchors[(int)b][q] = anchors[(int)Book.Walk][0];
                continue;
            }
            int n = PagesIn(b);
            books[(int)b] = new Sprite[n];
            anchors[(int)b] = new MouseAnchors[n];
            for (int i = 0; i < n; i++)
            {
                MouseAnchors an = default;
                MousePose pose = PoseOf(b, i);
                books[(int)b][i] = Sprite.Paint(w, h, g =>
                {
                    g.TranslateTransform(ox, oy);
                    g.ScaleTransform(k, -k);
                    if (b == Book.Front) PaintFront(g, i == 2 ? 1f : 0f, i == 1, 0f, i == 3, 0f);
                    else if (b == Book.Head) PaintFront(g, 0f, false, 0f, false, 0f);
                    else if (b == Book.Emerge) PaintEmerge(g, i / (float)(EmergePages - 1));
                    else
                    {
                        bool airborne = b is Book.Leap or Book.Plunge;
                        Shadow(g, 0.05f, airborne ? 0.38f : 0.58f, airborne ? 35 : 60);
                        an = PaintSide(g, pose);
                    }
                });
                anchors[(int)b][i] = an;
            }
        }
        return new Sheet { Books = books, Anchors = anchors, OriginX = ox, OriginY = oy, K = k };
    }

    // ------------------------------------------------------------------
    //  SMEAR FRAMES AND THE SEAM TABLE
    /// <summary>A picture half way between two others (a smear frame): every pixel's colour is the average of the two, weighted by how solid each is there, and its solidity is the average of the two.</summary>
    public static Sprite Mix(Sprite a, Sprite b, float t)
    {
        var pa = a.Argb; var pb = b.Argb; var o = new uint[pa.Length];
        for (int i = 0; i < o.Length; i++)
        {
            float aa = pa[i] >> 24, ab = pb[i] >> 24, w0 = aa * (1f - t), w1 = ab * t, wt = w0 + w1;
            if (wt < 1f) continue;
            float r = (((pa[i] >> 16) & 255) * w0 + ((pb[i] >> 16) & 255) * w1) / wt, gg = (((pa[i] >> 8) & 255) * w0 + ((pb[i] >> 8) & 255) * w1) / wt, bb = ((pa[i] & 255) * w0 + (pb[i] & 255) * w1) / wt;
            uint al = (uint)MathF.Round(aa * (1f - t) + ab * t);
            o[i] = (al << 24) | ((uint)r << 16) | ((uint)gg << 8) | (uint)bb;
        }
        return Sprite.FromPixels(a.Width, a.Height, o);
    }

    // ------------------------------------------------------------------
    //  THE SEAM TABLE (PECKWORKS_SHEET=LogMiceSeams;file.txt): how different two pages look, for every pair
    // ------------------------------------------------------------------

    /// <summary>Writes "bookA pageA bookB pageB difference" for every ordered pair of pages: the share of the pixels either page paints
    /// that differ clearly between them (0 same picture, 1 nothing in common). A change of page that is a pop shows as a big number.</summary>
    public static void WriteSeams(string path, float k)
    {
        Sheet s = BuildSheet(k);
        var all = new List<(Book B, int I, Sprite S)>();
        foreach (Book b in Enum.GetValues<Book>()) for (int i = 0; i < PagesIn(b); i++) all.Add((b, i, s.Page(b, i)));
        using var w = new StreamWriter(path);
        foreach (var a in all)
            foreach (var b in all)
            {
                if (a.B == b.B && a.I == b.I) continue;
                int union = 0, diff = 0;
                for (int y = 0; y < a.S.Height; y++)
                    for (int x = 0; x < a.S.Width; x++)
                    {
                        uint p = a.S.PixelAt(x, y), q = b.S.PixelAt(x, y);
                        int pa = (int)(p >> 24), qa = (int)(q >> 24);
                        if (pa < 60 && qa < 60) continue;
                        union++;
                        int d = Math.Abs(pa - qa) + Math.Abs((int)((p >> 16) & 255) - (int)((q >> 16) & 255)) + Math.Abs((int)((p >> 8) & 255) - (int)((q >> 8) & 255)) + Math.Abs((int)(p & 255) - (int)(q & 255));
                        if (d > 120) diff++;
                    }
                w.WriteLine($"{a.B} {a.I} {b.B} {b.I} {(union == 0 ? 0f : diff / (float)union):F3}");
            }
    }
    // ------------------------------------------------------------------
    //  THE POSE SHEET (PECKWORKS_SHEET=LogMice;file.png): every page, labelled, on snow and on night blue
    // ------------------------------------------------------------------

    public static void WriteSheet(string path, float k)
    {
        Sheet s = BuildSheet(k);
        const int zoom = 4;
        int cw = s.Page(Book.Run, 0).Width * zoom, ch = s.Page(Book.Run, 0).Height * zoom;
        int total = 0;
        foreach (Book b in Enum.GetValues<Book>()) total += PagesIn(b);
        int perRow = 8, rows = (total + perRow - 1) / perRow;
        using var snowBmp = new Bitmap(cw * perRow, (ch + 14) * rows * 2);
        using var g = Graphics.FromImage(snowBmp);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using var font = new Font("Segoe UI", 8f);
        int idx = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            Color back = pass == 0 ? Color.FromArgb(222, 233, 252) : Color.FromArgb(20, 36, 92);
            int baseY = pass * rows * (ch + 14);
            using var bb = new SolidBrush(back);
            g.FillRectangle(bb, 0, baseY, cw * perRow, rows * (ch + 14));
            idx = 0;
            foreach (Book b in Enum.GetValues<Book>())
                for (int i = 0; i < PagesIn(b); i++, idx++)
                {
                    Sprite sp = s.Page(b, i);
                    int cx = (idx % perRow) * cw, cy = baseY + (idx / perRow) * (ch + 14);
                    using var bmp = new Bitmap(sp.Width, sp.Height);
                    for (int y = 0; y < sp.Height; y++)
                        for (int x = 0; x < sp.Width; x++) bmp.SetPixel(x, y, Color.FromArgb((int)sp.PixelAt(x, y)));
                    g.DrawImage(bmp, cx, cy + 14, sp.Width * zoom, sp.Height * zoom);
                    g.DrawString($"{b} {i}", font, pass == 0 ? Brushes.Black : Brushes.White, cx + 1, cy);
                    // the feet line
                    using var pen = new Pen(Color.FromArgb(90, 255, 0, 0), 1);
                    g.DrawLine(pen, cx, cy + 14 + s.OriginY * zoom, cx + cw, cy + 14 + s.OriginY * zoom);
                }
        }
        snowBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
