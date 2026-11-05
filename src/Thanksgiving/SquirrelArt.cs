using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How a squirrel is drawn, and the poses it can take. Shared by the fence
/// squirrel (Happenings/Squirrel.cs) and the acorn caper (AcornCaper.cs).
///
/// FEYNMAN VERSION: a pose is a handful of numbers (where the hips, the
/// chest and the head are, where each foot is, how the tail curls, how the
/// body is squashed or stretched), and one painter turns any pose into a
/// picture: a thigh and a bean of a body, four two-bone legs with the knees
/// worked out by a little sum, a big flame of a tail built as a chain of
/// short pieces with a scalloped edge of hair tips, and a round head with a
/// tall tufted ear. The pose BUILDERS below know the animal: how a squirrel
/// bounds (the forepaws land, the hind feet swing PAST them and land ahead,
/// the hind legs push off and the body flies stretched out), how it hops
/// from a sit (crouch, push, fly, land on the forepaws, settle with the
/// tail overshooting), how it turns round (through a three-quarter and a
/// front view, never by flipping the picture), how it sits up, digs,
/// crouches and chatters, and how it clings to a trunk.
///
/// Everything is in squirrel units (about a body length), x forward, y UP,
/// the origin at the feet on the ground. The colours are a tawny squirrel:
/// a grey one vanished against the orange ground.
/// </summary>
internal static class SquirrelArt
{
    public static readonly Color Fur = Color.FromArgb(156, 112, 78);
    public static readonly Color FurDark = Color.FromArgb(118, 80, 52);
    public static readonly Color Under = Color.FromArgb(88, 56, 34);
    public static readonly Color Belly = Color.FromArgb(236, 214, 176);
    public static readonly Color TailCore = Color.FromArgb(122, 82, 52);
    public static readonly Color TailHair = Color.FromArgb(214, 170, 112);
    public static readonly Color TailLit = Color.FromArgb(168, 122, 80);      // the plume's lit side, between the core and the hair tips
    public static readonly Color Ink = Color.FromArgb(40, 30, 28);
    public static readonly Color LimbNear = Color.FromArgb(132, 92, 62);
    public static readonly Color LimbFar = Color.FromArgb(104, 70, 46);

    /// <summary>One pose. Positions in squirrel units from the feet's origin; angles in degrees.</summary>
    public sealed class Pose
    {
        public PointF Hip = new(-0.30f, 0.31f), Chest = new(0.26f, 0.30f);
        public float Hump;                         // how far the back humps up between hip and chest
        public PointF Head = new(0.55f, 0.37f);
        public float HeadTilt;                     // nose up is positive
        public float HeadTurn;                     // 0 profile, 1 facing the viewer
        public PointF NearFore = new(0.52f, 0f), FarFore = new(0.40f, 0f), NearHind = new(-0.02f, 0f), FarHind = new(0.08f, 0f);
        public float TailBase = 165f, TailCurl = -30f, TailWave, TailWavePhase;   // the tail's root angle, how much it curls along its length, a travelling ripple
        public float TailFlick;                    // extra turn at the tip (a lash)
        public float TailS;                        // a bulge back in the tail's middle, so a curl becomes an S (the question mark of a sitting squirrel's tail)
        public bool HindFlat;                      // the long hind foot flat on the ground (sitting)
        public bool PawsUp;                        // the hands held up at the chest (sitting with a nut)
        public int Acorn;                          // 0 none, 1 in the mouth, 2 in the paws
        public float Nib;                          // the mouth chewing, 0 to 1
        public float BodyScale = 1f;               // squash (under 1) or stretch (over 1) of the body along its length
        public float Rotate;                       // the whole picture turned about the feet's origin (degrees): -90 is head up a trunk
    }

    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static PointF Lerp(PointF a, PointF b, float t) => P(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    /// <summary>A value that passes through the given (time, value) keys, smoothly between them.</summary>
    private static float Keys(float t, params (float T, float V)[] keys)
    {
        if (t <= keys[0].T) return keys[0].V;
        for (int i = 1; i < keys.Length; i++)
            if (t <= keys[i].T) return Lerp(keys[i - 1].V, keys[i].V, Smooth((t - keys[i - 1].T) / (keys[i].T - keys[i - 1].T)));
        return keys[^1].V;
    }

    // ================================================================ poses

    /// <summary>
    /// One moment of a bound (p 0 to 1). The real sequence (Muybridge, the half-bound): the forepaws touch down,
    /// the back arches as the body gathers over them, the hind feet swing past the forepaws and land AHEAD of
    /// where they were, the hind legs push off with the body stretching, and the squirrel flies stretched out,
    /// tail streaming, to land on its forepaws again. "bound" is the bound's length in squirrel units, so the
    /// planted feet slide back at exactly the body's speed. "cling" is for a trunk: no flight lift, the body kept
    /// to the bark.
    /// </summary>
    public static Pose Bound(float p, float bound, bool cling = false)
    {
        p = ((p % 1f) + 1f) % 1f;
        var q = new Pose();
        float hipLift = Keys(p, (0f, 0.10f), (0.12f, 0.02f), (0.25f, 0f), (0.40f, 0f), (0.50f, 0.06f), (0.74f, 0.40f), (1f, 0.10f));
        float chestLift = Keys(p, (0f, 0.02f), (0.12f, 0f), (0.25f, 0.05f), (0.45f, 0.20f), (0.74f, 0.42f), (1f, 0.02f));
        float stretch = Keys(p, (0f, 1.0f), (0.25f, 0.76f), (0.47f, 1.15f), (0.70f, 1.05f), (1f, 1.0f));
        if (cling) { hipLift *= 0.15f; chestLift *= 0.15f; }
        float gather = Keys(p, (0f, 0f), (0.25f, 1f), (0.47f, 0f), (1f, 0f));
        q.BodyScale = stretch;
        float half = 0.28f * stretch;
        q.Hip = P(-half, 0.31f + hipLift);
        q.Chest = P(half, 0.30f + chestLift);
        q.Hump = 0.03f + 0.12f * gather;
        q.Head = P(q.Chest.X + 0.28f, q.Chest.Y + 0.07f - 0.03f * gather);
        q.HeadTilt = -6f + 10f * gather;

        // The forepaws: down from 0 to 0.25 (sliding back at the body's speed), then swinging forward through the air.
        float foreX0 = 0.52f;
        float ForeContact(float t) => foreX0 - bound * t;                // the paw's place on the body while it is planted
        PointF Fore(float lag)
        {
            float t = ((p - lag) % 1f + 1f) % 1f;
            if (t < 0.25f) return P(ForeContact(t), 0f);
            float a = (t - 0.25f) / 0.75f;
            return P(Lerp(ForeContact(0.25f), foreX0, Smooth(a)), 0.30f * MathF.Pow(MathF.Sin(MathF.PI * a), 0.9f) * (cling ? 0.3f : 1f));
        }
        // The hind feet: land at 0.35 just behind the body's middle (AHEAD of the fore prints in the world), push off at 0.50.
        float hindX0 = -0.04f;
        PointF Hind(float lag)
        {
            float t = ((p - 0.35f - lag) % 1f + 1f) % 1f;
            if (t < 0.15f) return P(hindX0 - bound * t, 0f);
            float a = (t - 0.15f) / 0.85f;
            return P(Lerp(hindX0 - bound * 0.15f, hindX0, Smooth(a)), 0.26f * MathF.Pow(MathF.Sin(MathF.PI * a), 0.9f) * (cling ? 0.3f : 1f));
        }
        q.NearFore = Fore(0f); q.FarFore = Fore(0.03f);
        q.NearHind = Hind(0f); q.FarHind = Hind(0.03f);

        // The tail: trails in an arc behind the body, its tip curling up, lifting a little as the body gathers over the
        // forepaws and streaming out in flight, with a ripple that lags the hips. (The first keys stood it up like a
        // paddle at the gather: a vertical plank on every other page.)
        q.TailBase = Keys(p, (0f, 150f), (0.25f, 138f), (0.5f, 155f), (0.74f, 165f), (1f, 150f));
        q.TailCurl = Keys(p, (0f, -45f), (0.25f, -60f), (0.5f, -35f), (1f, -45f));
        q.TailS = 25f;
        q.TailWave = 12f; q.TailWavePhase = MathF.Tau * (p - 0.15f);
        return q;
    }

    /// <summary>Where the hands rest in a sit without the nut: on the thighs, within the forelimb's reach.</summary>
    private static readonly PointF SitHandNear = P(0.08f, 0.36f), SitHandFar = P(0.14f, 0.34f);   // 0.40 from the shoulder: the elbow bends (at 0.46 the bones, 0.42 together, drew straight)

    /// <summary>Sitting up. "upright" 0 is a crouch on all fours, 1 fully up. tail: 0 calm, up to 3 lashed. look: head tipped up.</summary>
    public static Pose Sit(float upright = 1f, int tail = 0, bool look = false, bool paws = true)
    {
        float e = Smooth(upright);
        var q = new Pose();
        PointF hipRun = P(-0.30f, 0.31f), chestRun = P(0.26f, 0.30f);
        PointF hipSit = P(-0.12f, 0.24f);
        PointF chestSit = P(hipSit.X + 0.60f * MathF.Cos(77f * MathF.PI / 180f), hipSit.Y + 0.60f * MathF.Sin(77f * MathF.PI / 180f));
        q.Hip = Lerp(hipRun, hipSit, e); q.Chest = Lerp(chestRun, chestSit, e);
        q.Hump = Lerp(0.03f, 0.0f, e);
        float lookUp = look ? 1f : 0f;
        q.Head = P(q.Chest.X + Lerp(0.29f, 0.07f, e) + 0.03f * lookUp, q.Chest.Y + Lerp(0.07f, 0.22f, e) + 0.04f * lookUp);
        q.HeadTilt = Lerp(-4f, 8f, e) + 14f * lookUp;
        // Sitting up with the paws held to the chest (paws true) the forefeet are drawn as the paws; with them down they rest on the
        // thighs, within the two bones' reach of the shoulder (0.42). (At chest height out front the legs pulled straight and every
        // no-nut sit, turn and daze pointed a rod; on the ground in front of the hind feet they were out of reach and drew a
        // straight diagonal with the hand 20 px short of the ground.)
        q.NearFore = Lerp(P(0.52f, 0f), paws ? P(0.50f, 0.55f) : SitHandNear, e); q.FarFore = Lerp(P(0.40f, 0f), paws ? P(0.60f, 0.55f) : SitHandFar, e);
        q.NearHind = Lerp(P(-0.02f, 0f), P(0.0f, 0.03f), e); q.FarHind = Lerp(P(0.08f, 0f), P(-0.05f, 0.02f), e);
        q.HindFlat = e > 0.6f; q.PawsUp = paws && e > 0.45f;
        // Sitting, the tail is a question mark: up the back, leaning back in the middle, curling forward over the head.
        q.TailBase = Lerp(150f, 95f, e) + 8f * lookUp;
        q.TailCurl = Lerp(-45f, -100f, e);
        q.TailS = Lerp(25f, 70f, e);
        q.TailFlick = new[] { 0f, 14f, 30f, 14f }[tail] * e;
        return q;
    }

    /// <summary>Digging: the rump up, the chest down, the nose at the ground, the forepaws scrabbling by "phase" (0 to 1).</summary>
    public static Pose Dig(float phase, bool sniffOnly = false)
    {
        var q = new Pose();
        q.Hip = P(-0.24f, 0.30f); q.Chest = P(0.28f, 0.21f); q.Hump = 0.09f;
        q.Head = P(q.Chest.X + 0.22f, q.Chest.Y - 0.04f); q.HeadTilt = -35f;
        float s = sniffOnly ? 0f : MathF.Sin(phase * MathF.Tau);
        q.NearFore = P(0.50f + 0.09f * s, 0.02f + 0.06f * MathF.Max(0f, s));
        q.FarFore = P(0.50f - 0.09f * s, 0.02f + 0.06f * MathF.Max(0f, -s));
        q.NearHind = P(-0.08f, 0f); q.FarHind = P(0.02f, 0f);
        q.TailBase = 100f; q.TailCurl = -110f; q.TailS = 50f;          // up and over, the way a digging squirrel's tail arches
        return q;
    }

    /// <summary>
    /// A hop from a sit, k 0 to 1 over the whole hop: crouch (the body a sixth shorter, the tail dipping), push
    /// (a fifth longer, the hind legs straight, the tail lagging), flight (stretched, then tucking), landing on the
    /// forepaws (a squash), and the settle (the tail overshoots and comes back after the body has stopped).
    /// The hop's arc through the air is the caller's; this is only the body.
    /// </summary>
    /// <summary>Phases of the hop's 22 frames: crouch 0-4, push 4-6, flight 6-15, land 15-18, settle 18-22. The feet are on the ground outside the flight.</summary>
    public const float HopCrouch = 4 / 22f, HopPush = 6 / 22f, HopFlight = 15 / 22f, HopLand = 18 / 22f;

    public static Pose Hop(float k)
    {
        const float C = HopCrouch, Pu = HopPush, F = HopFlight, L = HopLand;
        Pose q;
        // The crouch: the paws stay at the chest (with the nut) for its first half, then come down to tuck under the chin, where the
        // push's swing begins (on the ground out front they were a straight rod, and the nut jumped to the mouth on the crouch's first frame).
        if (k < C) { q = Sit(1f); float c = k / C; q.BodyScale = 1 - 0.15f * c; q.Hip = P(q.Hip.X, q.Hip.Y - 0.05f * c); q.Chest = P(q.Chest.X - 0.08f * c, q.Chest.Y - 0.12f * c); q.Head = P(q.Head.X - 0.06f * c, q.Head.Y - 0.12f * c); q.TailBase -= 20f * c; q.PawsUp = c < 0.5f; q.NearFore = Lerp(SitHandNear, P(0.14f, 0.18f), c); q.FarFore = Lerp(SitHandFar, P(0.10f, 0.20f), c); }
        else if (k < Pu) { float c = (k - C) / (Pu - C); q = Bound(0.47f, 2.2f); q.BodyScale = 1.05f + 0.15f * c; q.Hip = P(q.Hip.X, 0.26f + 0.08f * c); q.NearHind = P(-0.30f - 0.1f * c, 0f); q.FarHind = P(-0.22f - 0.1f * c, 0f); q.TailBase = 150f - 30f * c; q.TailCurl = -60f; }
        else if (k < F) { float c = (k - Pu) / (F - Pu); q = Bound(0.74f + 0.2f * c, 2.2f); q.BodyScale = Lerp(1.15f, 0.95f, Smooth(c)); q.Hip = P(q.Hip.X, 0.31f); q.Chest = P(q.Chest.X, 0.30f + 0.1f * (1 - c)); q.Head = P(q.Chest.X + 0.28f, q.Chest.Y + 0.08f); }
        else if (k < L) { float c = (k - F) / (L - F); q = Bound(0.08f, 2.2f); q.BodyScale = 1 - 0.12f * MathF.Sin(MathF.PI * c); q.Hump = 0.10f * c; q.NearFore = P(0.50f, 0f); q.FarFore = P(0.42f, 0f); q.NearHind = P(-0.10f + 0.1f * c, 0.06f * (1 - c)); q.FarHind = P(-0.02f + 0.1f * c, 0.04f * (1 - c)); }
        else { float c = (k - L) / (1 - L); q = Sit(Smooth(c)); q.TailBase += 25f * MathF.Sin(MathF.PI * Smooth(c)); q.TailCurl -= 20f * MathF.Sin(MathF.PI * c); }   // the settle: the tail overshoots past the sitting curl and comes back
        return q;
    }

    /// <summary>A turn on the spot, sitting: k 0 is the profile, 0.5 facing the viewer, 1 the profile the other way (the caller mirrors from 0.5 on).</summary>
    public static Pose Turn(float k, bool paws = false)
    {
        float f = 1f - MathF.Abs(k - 0.5f) * 2f;                           // 0 profile, 1 front
        Pose q = Sit(1f, 0, false, paws);
        q.HeadTurn = f;
        q.BodyScale = 1f - 0.45f * f;                                       // the body narrows as it comes round
        q.TailBase = 95f - 10f * f; q.TailCurl = -100f + 30f * f; q.TailS = 70f - 30f * f;
        return q;
    }

    /// <summary>A forward crouch, chattering: the tail snapped ("snap" 0 to 3), one forepaw stamped forward ("paw" 0 or 1).</summary>
    public static Pose Chatter(int snap, int paw)
    {
        Pose q = Sit(0.35f, 0, false, false);
        q.Head = P(q.Head.X + 0.04f, q.Head.Y + 0.02f); q.HeadTilt = 6f;
        q.NearFore = P(paw == 0 ? 0.52f : 0.42f, paw == 0 ? 0f : 0.06f);
        q.FarFore = P(paw == 0 ? 0.40f : 0.52f, paw == 0 ? 0.06f : 0f);
        q.TailBase = 105f; q.TailCurl = -100f; q.TailS = 45f;
        q.TailFlick = new[] { 0f, 22f, 40f, 22f }[snap];
        return q;
    }

    /// <summary>Sitting dazed after the scuffle: the head lolling by "wobble" degrees.</summary>
    public static Pose Dazed(float wobble)
    {
        Pose q = Sit(1f, 0, false, false);
        q.HeadTilt += wobble; q.Head = P(q.Head.X + 0.01f * wobble, q.Head.Y);
        q.TailBase = 120f; q.TailCurl = -80f; q.TailS = 40f;
        return q;
    }

    // ================================================================ painting

    /// <summary>
    /// Paints a pose on a sheet "w" by "h" pixels, "k" pixels to the squirrel unit, the feet's origin at (ox, by).
    /// The caller's sheet must be big enough for the pose (2.55 by 1.9 units for the ground poses).
    /// </summary>
    public static Sprite Page(Pose q, float k, int w, int h, int ox, int by) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            if (q.Rotate != 0f) g.RotateTransform(q.Rotate);
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
        void Ell2(PointF c, float rx, float ry, Color col, float tilt = 0) => Ell(g, c.X, c.Y, rx, ry, col, tilt);
        void Bone(PointF a, PointF b, float wa, float wb, Color c)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            using var br = new SolidBrush(c);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }

        PointF hip = q.Hip, chest = q.Chest;
        PointF mid = P((hip.X + chest.X) / 2 - q.Hump * 0.3f, (hip.Y + chest.Y) / 2 + q.Hump);
        float tilt = MathF.Atan2(chest.Y - hip.Y, chest.X - hip.X) * 180f / MathF.PI;
        float front = q.HeadTurn;                                          // 0 side view, 1 facing us

        // ---- the tail first, behind everything: a big plume, the squirrel's banner ----
        // A chain of short pieces (the spine) bent by the pose: the root angle, a curl that grows along the length,
        // a travelling ripple, and a lash at the tip. The plume round it is as big as the body (half-width 0.24 at
        // its fullest; at 0.13 it read as a rat's), rounded at the end: a dark core, a lit band along its outer edge,
        // and a FRINGE of long hair strokes fanning out from both edges, which is what a tail's hair actually looks
        // like (the first version scalloped the edge with round tufts, which read as a plated lizard's).
        {
            PointF root = P(hip.X - 0.12f, hip.Y + 0.04f);
            const int N = 14;
            float segLen = 1.15f / N;
            var spine = new PointF[N + 1];
            spine[0] = root;
            for (int i = 0; i < N; i++)
            {
                float s = (i + 0.5f) / N;
                float ang = q.TailBase + q.TailCurl * s + q.TailS * MathF.Sin(MathF.PI * s) + q.TailWave * MathF.Sin(q.TailWavePhase - i * 0.5f) + q.TailFlick * s * s;
                float r = ang * MathF.PI / 180f;
                spine[i + 1] = P(spine[i].X + segLen * MathF.Cos(r), spine[i].Y + segLen * MathF.Sin(r));
            }
            // Half the plume's width along it: narrow at the root, fullest at 0.45, still fat at 0.85, then a rounded end.
            float Width(float s) => s < 0.45f ? Lerp(0.07f, 0.24f, Smooth(s / 0.45f)) : s < 0.85f ? Lerp(0.24f, 0.17f, Smooth((s - 0.45f) / 0.4f)) : Lerp(0.17f, 0.03f, Smooth((s - 0.85f) / 0.15f));
            PointF[] Edge(float side, float scale)
            {
                var pts = new PointF[N + 1];
                for (int i = 0; i <= N; i++)
                {
                    PointF a = spine[Math.Max(0, i - 1)], b = spine[Math.Min(N, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float w = Width(i / (float)N) * scale;
                    pts[i] = P(spine[i].X - dy / len * w * side, spine[i].Y + dx / len * w * side);
                }
                return pts;
            }
            PointF[] left = Edge(1f, 1f), right = Edge(-1f, 1f);
            // The fringe: strokes of hair fanning out from both edges, leaning back toward the root, longer where the
            // plume is fuller; pale on the outer (lit) edge, the core's dark on the inner. Drawn first so the core
            // covers their roots and only the free ends show.
            using (var pale = new Pen(TailHair, 0.024f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var dark = new Pen(TailCore, 0.024f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 1; i < N; i++)
                {
                    float w = Width(i / (float)N);
                    PointF a = spine[i - 1], b = spine[i + 1];
                    float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float tx = dx / len, ty = dy / len;                                   // along the tail, root to tip
                    foreach (float side in (float[])[1f, -1f])
                    {
                        float nx = -ty * side, ny = tx * side;                             // out of this edge
                        Pen pen = side > 0 ? pale : dark;
                        for (int j = 0; j < 2; j++)
                        {
                            // Every hair its own: a fixed scatter from its place in the row (never a comb of equal teeth).
                            float scatter = MathF.Sin(i * 7.3f + j * 2.1f + side * 1.7f), scatter2 = MathF.Sin(i * 3.9f + j * 5.3f);
                            float along = (j - 0.5f) * 0.5f + 0.15f * scatter2;             // two strokes a segment, spread along it
                            PointF at = P(spine[i].X + tx * along * segLen + nx * w * 0.75f, spine[i].Y + ty * along * segLen + ny * w * 0.75f);
                            float lean = 0.55f + 0.3f * scatter;                             // the hair leans back toward the root, each a little differently
                            float hx = nx - tx * lean, hy = ny - ty * lean, hl = MathF.Sqrt(hx * hx + hy * hy);
                            float length = (0.05f + 0.5f * w) * (0.75f + 0.35f * scatter2) * (side > 0 ? 1f : 0.45f);   // the inner edge's hair is short: long dark spikes on the ground side read as thorns
                            g.DrawLine(pen, at, P(at.X + hx / hl * length, at.Y + hy / hl * length));
                        }
                    }
                }
            var core = new PointF[(N + 1) * 2];
            for (int i = 0; i <= N; i++) { core[i] = left[i]; core[2 * N + 1 - i] = right[i]; }
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve(core, 0.4f);
                using var b = new SolidBrush(TailCore);
                g.FillPath(b, path);
            }
            // The lit side of the plume: one smooth band inside the core, off centre toward the outer edge, in a tone
            // between the core and the hair tips (the hair colour itself made a hard pale stripe: a two-tone leaf).
            PointF[] li = Edge(1f, 0.62f), ri = Edge(-1f, 0.05f);
            var inner = new PointF[(N + 1) * 2];
            for (int i = 0; i <= N; i++) { inner[i] = li[i]; inner[2 * N + 1 - i] = ri[i]; }
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve(inner, 0.4f);
                using var b = new SolidBrush(TailLit);
                g.FillPath(b, path);
            }
            // A frosting of pale hair tips along the lit band, in short strokes that follow the hair's lean.
            using (var frost = new Pen(Color.FromArgb(170, 240, 206, 156), 0.018f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 2; i < N - 1; i++)
                {
                    PointF a = li[i], b = spine[i];
                    g.DrawLine(frost, P(a.X * 0.85f + b.X * 0.15f, a.Y * 0.85f + b.Y * 0.15f), P(a.X * 0.5f + b.X * 0.5f, a.Y * 0.5f + b.Y * 0.5f));
                }
        }

        // ---- the legs: each two bones, the foot placed, the knee worked out. Front: elbow back. Hind: knee forward. ----
        void Leg(PointF joint, PointF foot, bool hind, Color col, float thick)
        {
            const float L = 0.21f;
            float dx = foot.X - joint.X, dy = foot.Y - joint.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * L - 0.003f);
            float al = MathF.Acos(Math.Clamp(d / (2 * L), -1f, 1f));
            float th = MathF.Atan2(dy, dx) + (hind ? al : -al);
            PointF knee = P(joint.X + L * MathF.Cos(th), joint.Y + L * MathF.Sin(th));
            PointF end = P(knee.X + (foot.X - knee.X) * 0.999f, knee.Y + (foot.Y - knee.Y) * 0.999f);
            Bone(joint, knee, thick, thick * 0.8f, col);
            Bone(knee, end, thick * 0.8f, thick * 0.6f, col);
            if (hind) Ell(g, end.X + 0.06f, end.Y + 0.01f, 0.10f, 0.03f, col);      // the long hind foot
            else Ell(g, end.X + 0.03f, end.Y + 0.005f, 0.055f, 0.03f, col);         // the hand
        }
        PointF shoulder = P(chest.X - 0.02f, chest.Y - 0.08f), haunch = P(hip.X + 0.04f, hip.Y - 0.06f);
        PointF pawL = P(q.Head.X + 0.06f, q.Head.Y - 0.20f), pawR = P(q.Head.X + 0.19f, q.Head.Y - 0.20f);
        if (front < 0.5f)
        {
            Leg(P(shoulder.X + 0.04f, shoulder.Y), q.PawsUp ? pawR : q.FarFore, false, LimbFar, 0.075f);
            Leg(P(haunch.X + 0.03f, haunch.Y), q.FarHind, true, LimbFar, 0.10f);
        }

        // ---- the body: a plump round haunch at the rump, a bean from hip to chest with a hump, the cream belly and chest ----
        // Rounder than the first version (a longer, flatter body with a thin tail was a rat's): the haunch is a big
        // disc that IS the outline at the back, the belly is deep, and nothing on the body is drawn with a pen.
        float bw = 1f - 0.5f * front;                                        // narrower seen from the front
        Ell2(P(hip.X - 0.01f, hip.Y - 0.01f), 0.27f * bw, 0.25f, FurDark, tilt - 25f);       // the haunch, big and round
        Ell2(hip, 0.24f * bw, 0.23f, Fur);
        Ell2(mid, 0.27f * q.BodyScale * bw, 0.21f, Fur, tilt);
        Ell2(chest, 0.20f * bw, 0.18f, Fur);
        Ell2(P(mid.X + 0.05f, mid.Y - 0.08f), 0.25f * q.BodyScale * bw, 0.12f, Belly, tilt);   // the pale belly, deep (the thigh's shading disc is gone: it read as a hole in the back)
        Ell2(P(chest.X + 0.04f, chest.Y - 0.04f), 0.14f * bw, 0.14f, Belly);                   // the cream chest
        Ell2(P(mid.X - 0.02f, mid.Y + 0.175f), 0.22f * q.BodyScale * bw, 0.05f, FurDark, tilt);   // a darker ridge along the top of the back, half over the outline (inside the body it was a dark slot)

        // ---- the head: big and nearly round, a short muzzle, a round pale cheek, small round ears, a big eye with a pale ring and a glint ----
        // What makes a squirrel's face cute rather than a rat's: the skull is round and big for the body, the muzzle is
        // short, the ears are small rounded cups (not tall and pointed), and the eye is large, high and ringed with
        // pale fur. The first version had a long muzzle and tall tufted ears: a rat in a tawny coat.
        {
            var st = g.Save();
            g.TranslateTransform(q.Head.X, q.Head.Y);
            g.RotateTransform(q.HeadTilt);
            float hw = 0.21f * (1f - 0.1f * front), muzzle = 0.09f * (1 - front);
            // The ears: one in profile; both when turned toward us.
            void Ear(float ex, float tiltDeg)
            {
                Ell(g, ex, 0.165f, 0.062f, 0.072f, Fur, tiltDeg);                               // a small round cup
                Ell(g, ex + 0.004f, 0.16f, 0.036f, 0.046f, Color.FromArgb(220, 200, 150, 130), tiltDeg);   // its pink inside
                Ell(g, ex - 0.004f, 0.232f, 0.024f, 0.026f, FurDark, tiltDeg);                  // a little tuft on top
            }
            Ear(-0.05f - 0.06f * front, -8f);
            if (front > 0.3f) Ear(0.08f + 0.04f * front, 8f);
            Ell(g, 0f, 0f, hw, 0.20f, Fur);                                                    // the skull, nearly round
            Ell(g, 0.09f + 0.05f * (1 - front), -0.06f, 0.11f * (1 - 0.3f * front), 0.095f, Belly);   // the pale cheek and short muzzle
            Ell(g, 0.11f + muzzle * 0.6f, -0.02f, 0.028f, 0.024f, Ink);                       // the nose, small and dark
            using (var mouth = new Pen(Ink, 0.014f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(mouth, 0.08f, -0.085f - 0.012f * q.Nib, 0.13f, -0.06f);
            void Eye(float ex, float ey)
            {
                Ell(g, ex, ey, 0.058f, 0.06f, Color.FromArgb(226, 206, 178));                  // the pale ring round the eye, soft (a white one was a goggle)
                Ell(g, ex, ey, 0.05f, 0.052f, Ink);                                            // the eye, big and round
                Ell(g, ex + 0.015f, ey + 0.017f, 0.017f, 0.017f, Color.White);                  // the glint
            }
            Eye(0.055f - 0.05f * front, 0.05f);
            if (front > 0.3f) Eye(-0.075f, 0.05f);
            using (var wh = new Pen(Color.FromArgb(120, 236, 226, 210), 0.008f))
                for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.14f, -0.045f, 0.25f * (1 - front), -0.035f + i * 0.04f);
            g.Restore(st);
        }

        // ---- the acorn: in the mouth, or in the paws ----
        if (q.Acorn == 1)
        {
            float ca = MathF.Cos(q.HeadTilt * MathF.PI / 180f), sa = MathF.Sin(q.HeadTilt * MathF.PI / 180f);
            const float mx = 0.21f, my = -0.03f;
            Acorn(g, P(q.Head.X + mx * ca - my * sa, q.Head.Y + mx * sa + my * ca), 8f + q.HeadTilt);
        }
        else if (q.Acorn == 2) Acorn(g, P(q.Head.X + 0.13f, q.Head.Y - 0.165f - 0.01f * q.Nib), 20f + 25f * q.Nib);

        // ---- the near legs, in front of the body ----
        if (q.HindFlat) Ell(g, q.NearHind.X + 0.10f, 0.03f, 0.15f, 0.05f, Fur);               // the long hind foot flat on the ground
        Leg(shoulder, q.PawsUp ? pawL : q.NearFore, false, LimbNear, 0.08f);
        Leg(haunch, q.NearHind, true, LimbNear, 0.095f);
        if (front >= 0.5f)
        {
            Leg(P(shoulder.X + 0.04f, shoulder.Y), q.PawsUp ? pawR : q.FarFore, false, LimbFar, 0.075f);
            Leg(P(haunch.X + 0.03f, haunch.Y), q.FarHind, true, LimbFar, 0.10f);
        }
        if (q.PawsUp)
        {
            Ell2(pawL, 0.04f, 0.035f, LimbNear);                                               // the little hands round the nut
            Ell2(pawR, 0.04f, 0.035f, LimbNear);
        }
    }

    /// <summary>The acorn, at "at", turned by "angDeg": the nut with a lighter stripe and a dark rim, and the scaly cap on its end.</summary>
    public static void Acorn(Graphics g, PointF at, float angDeg)
    {
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.RotateTransform(angDeg);
        Ell(g, 0f, 0f, 0.075f, 0.058f, Color.FromArgb(150, 98, 46));
        Ell(g, -0.018f, 0.012f, 0.03f, 0.02f, Color.FromArgb(200, 180, 128, 70));
        using (var rim = new Pen(Color.FromArgb(160, 60, 34, 14), 0.012f))
            g.DrawEllipse(rim, -0.075f, -0.058f, 0.15f, 0.116f);
        using (var cap = new SolidBrush(Color.FromArgb(84, 54, 28)))
            g.FillPie(cap, -0.058f, -0.07f, 0.116f, 0.14f, 70, 220);
        g.Restore(st);
    }

    /// <summary>The head alone, poking out of a hole: for the caper's hollow. outBy 0 to 1 is how far up it has come; the sheet's origin is the hole's middle.</summary>
    public static void PaintPeek(Graphics g, float outBy, bool blink)
    {
        using (var lipClip = new GraphicsPath())
        {
            lipClip.AddRectangle(new RectangleF(-2f, -0.02f, 4f, 3f));
            g.SetClip(lipClip);
        }
        float rise = -0.30f + 0.42f * outBy;
        var q = new Pose { Head = P(0.02f, rise), HeadTilt = -6f };
        var st = g.Save();
        g.TranslateTransform(q.Head.X, q.Head.Y);
        g.RotateTransform(q.HeadTilt);
        // The same face as Paint's: a round skull, a small cup of an ear with a tuft, a pale cheek, a ringed eye.
        Ell(g, -0.05f, 0.165f, 0.062f, 0.072f, Fur, -8f);
        Ell(g, -0.046f, 0.16f, 0.036f, 0.046f, Color.FromArgb(220, 200, 150, 130), -8f);
        Ell(g, -0.054f, 0.232f, 0.024f, 0.026f, FurDark, -8f);
        Ell(g, 0f, 0f, 0.21f, 0.20f, Fur);
        Ell(g, 0.14f, -0.06f, 0.11f, 0.095f, Belly);
        Ell(g, 0.164f, -0.02f, 0.028f, 0.024f, Ink);
        Ell(g, 0.055f, 0.05f, 0.058f, 0.06f, Color.FromArgb(226, 206, 178));
        if (blink)
        {
            using var lid = new Pen(Ink, 0.016f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(lid, 0.01f, 0.05f, 0.10f, 0.05f);
        }
        else { Ell(g, 0.055f, 0.05f, 0.05f, 0.052f, Ink); Ell(g, 0.07f, 0.067f, 0.017f, 0.017f, Color.White); }
        g.Restore(st);
        Ell(g, -0.12f, rise - 0.12f, 0.045f, 0.035f, LimbNear);
        Ell(g, 0.16f, rise - 0.12f, 0.045f, 0.035f, LimbNear);
    }
}
