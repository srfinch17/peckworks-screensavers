using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How a coyote is drawn, and the poses it can take: a trot, a flat-out gallop, standing, sitting on its haunches,
/// digging, carrying something in its mouth, shaking it, pouncing on it and pulling at it. Everything is in coyote
/// units (a body length from chest to rump), x forward, y UP, the origin at the feet on the ground.
///
/// FEYNMAN VERSION: a pose is where the chest and the rump are, where each of the four feet is, how the head is
/// held and which way the tail hangs. One painter turns any pose into a picture: four two-bone legs with the joints
/// worked out by the little sum the deer uses (hocks back, knees forward), a lean body that is deep at the chest, a
/// wedge of a head with big pointed ears and a long muzzle, and a bushy tail that hangs low with a dark tip. The
/// gait builders know how a dog moves: a TROT is diagonal pairs (near fore with far hind, far fore with near hind)
/// swinging in turn, the body level; a GALLOP is the fore feet landing one just after the other, the back arching
/// as the hind feet swing under and PAST them, then the whole body stretched out in the air.
/// </summary>
internal static class CoyoteArt
{
    public static readonly Color Coat = Color.FromArgb(166, 142, 108);
    public static readonly Color CoatDark = Color.FromArgb(120, 96, 68);
    public static readonly Color Belly = Color.FromArgb(220, 208, 184);
    public static readonly Color Tip = Color.FromArgb(58, 42, 32);
    public static readonly Color Ink = Color.FromArgb(34, 26, 24);

    /// <summary>One pose, in coyote units. The feet are from their hips (near fore, far fore, near hind, far hind).</summary>
    public sealed class Pose
    {
        public PointF Chest = new(0.32f, 0.54f), Rump = new(-0.32f, 0.55f);
        public PointF[] Feet = [new(0.02f, 0f), new(-0.02f, 0f), new(0.02f, 0f), new(-0.02f, 0f)];
        public float[] Lift = [0f, 0f, 0f, 0f];
        public PointF Head = new(0.70f, 0.72f);    // the middle of the skull
        public float HeadTilt;                     // nose up is positive
        public float HeadYaw;                      // the head turned toward or away from the viewer, degrees (a shake)
        public float Tail = -65f;                  // the tail's angle from forward: -65 hangs down and back, -10 streams level, 30 up
        public float TailCurl = -20f;
        public float Ears = 60f;                   // the ears' angle: 60 up, 110 flat back
        public bool Mouth;                         // open (carrying, or a snarl)
        public float Gape;                         // the jaw dropped wide, 0 shut to 1 as wide as a howl
        public float Arch;                         // the back humped (gathered) or sagging (stretched): body heights
        public bool Sitting;
    }

    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    private static float Keys(float t, params (float T, float V)[] keys)
    {
        if (t <= keys[0].T) return keys[0].V;
        for (int i = 1; i < keys.Length; i++)
            if (t <= keys[i].T) return Lerp(keys[i - 1].V, keys[i].V, Smooth((t - keys[i - 1].T) / (keys[i].T - keys[i - 1].T)));
        return keys[^1].V;
    }

    public static Pose Stand() => new();

    public static Pose Sit()
    {
        var q = new Pose { Sitting = true, Chest = P(0.22f, 0.62f), Rump = P(-0.26f, 0.30f), Head = P(0.52f, 0.86f), HeadTilt = 8f, Tail = -75f, TailCurl = 30f };
        q.Feet = [P(0.10f, 0f), P(0.06f, 0f), P(0.26f, 0f), P(0.22f, 0f)];   // the forelegs straight under the chest, the hind feet forward under the haunches
        return q;
    }

    /// <summary>
    /// A trot, p 0 to 1 over one stride: diagonal pairs. "stride" is the ground covered in a stride (units), so a
    /// planted foot slides back at exactly the body's speed; each foot is down for 0.55 of the stride.
    /// </summary>
    public static Pose Trot(float p, float stride)
    {
        p = ((p % 1f) + 1f) % 1f;
        var q = new Pose();
        float[] offsets = [0f, 0.5f, 0.5f, 0f];                                        // near fore with far hind, far fore with near hind
        for (int j = 0; j < 4; j++)
        {
            float t = ((p + offsets[j]) % 1f + 1f) % 1f;
            const float duty = 0.55f, reach = 0.16f;
            if (t < duty) { q.Feet[j] = P(reach - 2 * reach * t / duty, 0f); q.Lift[j] = 0f; }
            else { float s = (t - duty) / (1 - duty); q.Feet[j] = P(-reach + 2 * reach * Smooth(s), 0f); q.Lift[j] = 0.10f * MathF.Sin(MathF.PI * s); }
        }
        float bob = 0.015f * MathF.Sin(p * MathF.Tau * 2);
        q.Chest = P(0.32f, 0.54f + bob); q.Rump = P(-0.32f, 0.55f + bob);
        q.Head = P(0.70f, 0.68f + bob); q.HeadTilt = -4f;
        q.Tail = -40f; q.TailCurl = -10f;
        return q;
    }

    /// <summary>
    /// A gallop, p 0 to 1 over one stride of "stride" units: the fore feet land one after the other (0.00 and 0.10)
    /// and push, the hind feet swing under and land AHEAD of them (0.45, 0.55), the push-off at 0.7 and a stretched
    /// flight. The back arches as the hind feet come under, the head reaches low, the tail streams.
    /// </summary>
    public static Pose Gallop(float p, float stride)
    {
        p = ((p % 1f) + 1f) % 1f;
        var q = new Pose();
        float[] land = [0.00f, 0.10f, 0.45f, 0.55f];
        for (int j = 0; j < 4; j++)
        {
            float t = ((p - land[j]) % 1f + 1f) % 1f;
            const float duty = 0.30f;
            float reach = j < 2 ? 0.22f : 0.26f;
            if (t < duty) { q.Feet[j] = P(reach - 2 * reach * t / duty, 0f); q.Lift[j] = 0f; }
            else { float s = (t - duty) / (1 - duty); q.Feet[j] = P(-reach + 2 * reach * Smooth(s), 0f); q.Lift[j] = (j < 2 ? 0.22f : 0.26f) * MathF.Sin(MathF.PI * s); }
        }
        float gather = Keys(p, (0f, 0.2f), (0.2f, 0.6f), (0.45f, 1f), (0.7f, 0.2f), (0.85f, 0f), (1f, 0.2f));   // 1 = the body bunched, the back arched
        float flight = Keys(p, (0f, 0.1f), (0.3f, 0f), (0.6f, 0.2f), (0.8f, 1f), (0.92f, 0.5f), (1f, 0.1f));    // 1 = stretched out in the air
        float half = Lerp(0.30f, 0.38f, flight) - 0.04f * gather;
        float lift = 0.16f * flight;
        q.Chest = P(half, 0.52f + lift + 0.03f * gather); q.Rump = P(-half, 0.56f + lift - 0.02f * gather);
        q.Arch = 0.10f * gather - 0.04f * flight;
        q.Head = P(half + 0.38f, 0.60f + lift + 0.04f * flight); q.HeadTilt = -10f + 6f * flight;
        q.Tail = -5f - 10f * gather; q.TailCurl = 10f; q.Ears = 95f;
        return q;
    }

    /// <summary>
    /// THE HOWL, p 0 to 1 over the whole howl. How a coyote does it: it sits on its haunches, draws the chest up, and
    /// lifts its muzzle until the nose points at the sky, the jaw dropped open, ears swept back, holding the note while the
    /// throat trembles, then lets the head sink. So: the head RISES (0 to 0.2), HOLDS with a slow tremble of the tilt
    /// (0.2 to 0.85), and SINKS (0.85 to 1). It ends on the plain Sit pose, so both seams of the pose change (the sit
    /// before it, the stand it ends in) are a small step.
    /// </summary>
    public static Pose Howl(float p)
    {
        float up = Keys(p, (0f, 0f), (0.2f, 1f), (0.85f, 1f), (1f, 0f));
        var q = Sit();
        q.Chest = P(0.22f + 0.02f * up, 0.62f + 0.04f * up);
        q.Head = P(Lerp(0.52f, 0.38f, up), Lerp(0.86f, 1.03f, up));
        q.HeadTilt = Lerp(8f, 68f, up) + 3f * MathF.Sin(p * MathF.Tau * 3f) * up;     // the held note: the muzzle shivers
        q.Gape = MathF.Min(1f, up * 1.4f);
        q.Ears = Lerp(60f, 100f, up);
        q.Tail = -80f;
        return q;
    }
    /// <summary>Digging: the rump up, the chest low, the nose at the ground, the forepaws scrabbling by "phase".</summary>
    public static Pose Dig(float phase)
    {
        var q = new Pose { Chest = P(0.30f, 0.40f), Rump = P(-0.30f, 0.60f), Head = P(0.62f, 0.34f), HeadTilt = -40f, Tail = 5f, TailCurl = 10f, Arch = 0.06f };
        float s = MathF.Sin(phase * MathF.Tau);
        q.Feet = [P(0.10f + 0.12f * s, 0f), P(0.10f - 0.12f * s, 0f), P(0.0f, 0f), P(-0.04f, 0f)];
        q.Lift = [0.06f * MathF.Max(0f, s), 0.06f * MathF.Max(0f, -s), 0f, 0f];
        return q;
    }

    /// <summary>Standing with the head level and the mouth closed on something (the thing is drawn by the caller at MouthPoint).</summary>
    public static Pose Carry() { var q = new Pose { Head = P(0.70f, 0.66f), HeadTilt = -8f, Mouth = true, Tail = -30f }; return q; }

    /// <summary>Shaking what it holds: the head thrown from side to side ("yaw" degrees), the body braced.</summary>
    public static Pose Shake(float yaw) { var q = Carry(); q.HeadYaw = yaw; q.HeadTilt = -14f; q.Feet = [P(0.10f, 0f), P(-0.06f, 0f), P(-0.06f, 0f), P(0.08f, 0f)]; return q; }

    /// <summary>A pounce, p 0 to 1: a crouch (0 to 0.25), a leap with the forepaws up (0.25 to 0.7), the landing with the forepaws down on it.</summary>
    public static Pose Pounce(float p)
    {
        var q = new Pose();
        float crouch = Keys(p, (0f, 0f), (0.25f, 1f), (0.4f, 0f), (1f, 0f));
        float leap = Keys(p, (0.25f, 0f), (0.5f, 1f), (0.75f, 0f), (1f, 0f));
        float land = Keys(p, (0.7f, 0f), (0.8f, 1f), (1f, 0.4f));
        q.Chest = P(0.30f, 0.54f - 0.12f * crouch + 0.22f * leap - 0.10f * land); q.Rump = P(-0.32f, 0.55f - 0.08f * crouch + 0.10f * leap);
        q.Head = P(0.66f, 0.64f - 0.12f * crouch + 0.26f * leap - 0.22f * land); q.HeadTilt = -8f + 20f * leap - 30f * land;
        q.Feet = [P(0.02f + 0.16f * leap + 0.12f * land, 0f), P(-0.02f + 0.14f * leap + 0.10f * land, 0f), P(0.02f - 0.08f * crouch, 0f), P(-0.02f - 0.08f * crouch, 0f)];
        q.Lift = [0.30f * leap, 0.28f * leap, 0.10f * leap, 0.10f * leap];
        q.Tail = -20f + 40f * leap; q.Mouth = land > 0.5f;
        return q;
    }

    /// <summary>Pulling at something held in the mouth: the body leaning back, the legs braced forward, the head low.</summary>
    public static Pose Pull() { var q = new Pose { Chest = P(0.26f, 0.50f), Rump = P(-0.34f, 0.58f), Head = P(0.58f, 0.50f), HeadTilt = -16f, Mouth = true, Tail = -20f }; q.Feet = [P(0.16f, 0f), P(0.12f, 0f), P(0.10f, 0f), P(0.06f, 0f)]; return q; }

    /// <summary>
    /// HEAD UP, t 0 to 1: standing, the neck stretching and the muzzle lifting until it points up at the sky (t 1 is about 62
    /// degrees), the jaw hanging a little open, the ears up and forward: how a dog stares at something big going over (the
    /// vulture). One dial, so the caller can hold any tilt in between to follow a bird up the sky.
    /// </summary>
    public static Pose Look(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        // t 0 is exactly the plain Stand pose (so the stare begins and ends with no join to see).
        return new Pose { Chest = P(0.32f, 0.54f + 0.04f * t), Head = P(Lerp(0.70f, 0.60f, t), Lerp(0.72f, 1.00f, t)), HeadTilt = Lerp(0f, 62f, t), Gape = 0.45f * t, Tail = -65f - 5f * t };
    }

    /// <summary>
    /// A puzzled shake of the head, yaw in degrees: the head turned toward and away from us, the way a dog shakes the idea out
    /// of its ears (the muzzle shortens as the face turns to us, so the shake reads even from the side), a small nod with it.
    /// </summary>
    public static Pose Puzzle(float yaw) => new() { Head = P(0.70f, 0.66f), HeadTilt = 6f - 6f * MathF.Abs(MathF.Sin(yaw * MathF.PI / 90f)), HeadYaw = yaw, Ears = 85f, Tail = -75f };

    /// <summary>Where the mouth is for a pose, in coyote units: where a carried thing hangs from.</summary>
    public static PointF MouthPoint(Pose q)
    {
        float a = q.HeadTilt * MathF.PI / 180f;
        const float mx = 0.30f, my = -0.07f;
        return P(q.Head.X + mx * MathF.Cos(a) - my * MathF.Sin(a), q.Head.Y + mx * MathF.Sin(a) + my * MathF.Cos(a));
    }

    public static Sprite Page(Pose q, float k, int w, int h, int ox, int by, bool mirror = false) =>
        Sprite.Paint(w, h, g =>
        {
            if (mirror) { g.TranslateTransform(w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(ox, by);
            g.ScaleTransform(k, -k);
            Paint(g, q);
        });

    private static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c, float tilt = 0)
    {
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(tilt);
        using var b = new SolidBrush(c);
        g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
        g.Restore(st);
    }

    public static void Paint(Graphics g, Pose q)
    {
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Bone(PointF a, PointF b, float wa, float wb, Color c)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            using var br = new SolidBrush(c);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }
        PointF chest = q.Chest, rump = q.Rump;
        PointF shoulder = P(chest.X - 0.02f, chest.Y - 0.04f), haunch = P(rump.X + 0.04f, rump.Y - 0.06f);
        // A leg: two bones from the hip to the foot, the joint by the law of cosines; the hock swings back, the knee forward.
        void Leg(int j, Color upper, Color lower)
        {
            bool hind = j >= 2;
            PointF hip = hind ? haunch : shoulder;
            PointF foot = P(hip.X + q.Feet[j].X, 0.02f + q.Lift[j]);
            float bone = hind ? 0.30f : 0.28f;
            float ddx = foot.X - hip.X, ddy = foot.Y - hip.Y;
            float d = MathF.Min(MathF.Sqrt(ddx * ddx + ddy * ddy), 2 * bone - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * bone), -1, 1));
            float theta = MathF.Atan2(ddy, ddx) + (hind ? alpha : -alpha);
            PointF joint = P(hip.X + bone * MathF.Cos(theta), hip.Y + bone * MathF.Sin(theta));
            Bone(hip, joint, hind ? 0.13f : 0.09f, 0.055f, upper);
            Bone(joint, foot, 0.055f, 0.04f, lower);
            Ell(g, foot.X + 0.025f, foot.Y - 0.005f, 0.05f, 0.028f, lower);                    // the paw
        }
        // The tail first, behind the body: a bushy plume hanging from the rump, dark at the tip.
        {
            // The tail points BACK: its direction is 180 degrees less the pose's angle, so a negative angle hangs down behind.
            PointF root = P(rump.X - 0.08f, rump.Y + 0.02f);
            float d0 = MathF.PI - q.Tail * MathF.PI / 180f, d1 = MathF.PI - (q.Tail + q.TailCurl) * MathF.PI / 180f;
            PointF mid = P(root.X + 0.24f * MathF.Cos(d0), root.Y + 0.24f * MathF.Sin(d0));
            PointF tip = P(mid.X + 0.24f * MathF.Cos(d1), mid.Y + 0.24f * MathF.Sin(d1));
            Bone(root, mid, 0.14f, 0.16f, CoatDark);
            Bone(mid, tip, 0.16f, 0.09f, CoatDark);
            Ell(g, tip.X, tip.Y, 0.07f, 0.06f, Tip);
            Bone(P(root.X + (mid.X - root.X) * 0.3f, root.Y + (mid.Y - root.Y) * 0.3f), mid, 0.07f, 0.09f, Coat);   // the lit top of the brush
        }
        Leg(1, CoatDark, CoatDark);
        Leg(3, CoatDark, CoatDark);
        // The body: lean, deep at the chest, the back a line from withers to rump that arches with the gait; the pale belly.
        float arch = q.Arch;
        float cx = (chest.X + rump.X) / 2, cy = (chest.Y + rump.Y) / 2;
        Blob([P(chest.X + 0.14f, chest.Y + 0.02f), P(chest.X + 0.06f, chest.Y + 0.22f), P(cx, cy + 0.25f + arch), P(rump.X - 0.04f, rump.Y + 0.20f), P(rump.X - 0.16f, rump.Y + 0.02f), P(rump.X - 0.08f, rump.Y - 0.16f), P(cx, cy - 0.14f - arch * 0.5f), P(chest.X + 0.04f, chest.Y - 0.22f), P(chest.X + 0.16f, chest.Y - 0.10f)], Coat);
        Blob([P(chest.X + 0.06f, chest.Y + 0.22f), P(cx, cy + 0.25f + arch), P(rump.X - 0.04f, rump.Y + 0.20f), P(rump.X - 0.02f, rump.Y + 0.12f), P(cx, cy + 0.16f + arch), P(chest.X + 0.04f, chest.Y + 0.14f)], CoatDark, 0.4f);   // the dark saddle along the back
        Blob([P(chest.X + 0.12f, chest.Y - 0.12f), P(chest.X, chest.Y - 0.20f), P(cx, cy - 0.13f - arch * 0.5f), P(rump.X - 0.02f, rump.Y - 0.14f), P(rump.X, rump.Y - 0.06f), P(cx, cy - 0.04f), P(chest.X + 0.06f, chest.Y - 0.04f)], Belly, 0.45f);   // the pale underside
        if (q.Sitting) Ell(g, rump.X + 0.02f, rump.Y - 0.02f, 0.24f, 0.22f, Coat);          // the haunch sat on
        // The neck and head: a wedge from the withers to a long muzzle, the skull round, big pointed ears, a dark nose.
        {
            PointF neck0 = P(chest.X + 0.08f, chest.Y + 0.10f);
            Bone(neck0, q.Head, 0.22f, 0.20f, Coat);
            var st = g.Save();
            g.TranslateTransform(q.Head.X, q.Head.Y);
            g.RotateTransform(q.HeadTilt);
            float yaw = MathF.Abs(q.HeadYaw) / 60f;                                           // 0 profile, toward 1 turned to or from us
            float len = 1f - 0.5f * yaw;
            Ell(g, 0f, 0f, 0.14f, 0.13f, Coat);                                                // the skull
            Blob([P(0.04f, 0.09f), P(0.30f * len, 0.0f), P(0.33f * len, -0.05f), P(0.26f * len, -0.09f), P(0.02f, -0.11f)], Coat, 0.3f);   // the muzzle, a wedge
            Blob([P(0.06f, -0.03f), P(0.28f * len, -0.05f), P(0.26f * len, -0.09f), P(0.04f, -0.11f)], Belly, 0.3f);                       // pale under the muzzle
            if (q.Mouth) Blob([P(0.12f, -0.09f), P(0.30f * len, -0.07f), P(0.28f * len, -0.13f), P(0.12f, -0.13f)], Ink, 0.3f);           // the jaw open
            if (q.Gape > 0f)
            {
                Blob([P(0.10f, -0.04f), P(0.30f * len, -0.05f), P(0.25f * len, -0.15f - 0.05f * q.Gape), P(0.10f, -0.14f)], Ink, 0.3f);   // the inside of the mouth
                var js = g.Save();
                g.TranslateTransform(0.10f, -0.08f);
                g.RotateTransform(-24f * q.Gape);                                              // the lower jaw swings down about the hinge
                Blob([P(0f, 0.03f), P(0.20f * len, 0.02f), P(0.22f * len, -0.025f), P(0f, -0.045f)], Coat, 0.3f);
                g.Restore(js);
            }
            Ell(g, 0.32f * len, -0.03f, 0.035f, 0.03f, Ink);                                   // the nose
            foreach (float ex in (float[])[-0.06f, 0.0f])
            {
                float ea = q.Ears + (ex < -0.03f ? 8f : -8f);
                var es = g.Save();
                g.TranslateTransform(ex, 0.09f);
                g.RotateTransform(90f - ea);
                using (var ear = new SolidBrush(ex < -0.03f ? CoatDark : Coat)) g.FillPolygon(ear, [P(-0.05f, 0f), P(0.05f, 0f), P(0.0f, 0.20f)]);
                using (var inner = new SolidBrush(Belly)) g.FillPolygon(inner, [P(-0.025f, 0.01f), P(0.025f, 0.01f), P(0.0f, 0.13f)]);
                g.Restore(es);
            }
            Ell(g, 0.09f, 0.02f, 0.03f, 0.03f, Ink);                                           // the eye
            Ell(g, 0.098f, 0.028f, 0.01f, 0.01f, Color.FromArgb(240, 220, 150));              // a glint, amber
            g.Restore(st);
        }
        Leg(0, Coat, CoatDark);
        Leg(2, Coat, CoatDark);
    }
}
