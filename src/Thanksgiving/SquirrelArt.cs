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
    public static readonly Color Fur = Color.FromArgb(206, 108, 46);          // a red squirrel's rust: bright against the patch's dull earth
    public static readonly Color FurDark = Color.FromArgb(158, 74, 32);
    public static readonly Color Belly = Color.FromArgb(248, 230, 200);
    public static readonly Color EarIn = Color.FromArgb(226, 160, 136);
    public static readonly Color TailDark = Color.FromArgb(150, 70, 30);     // the plume's inner band
    public static readonly Color TailMain = Color.FromArgb(214, 118, 52);
    public static readonly Color TailLight = Color.FromArgb(240, 168, 96);   // the streaks along it
    public static readonly Color Ink = Color.FromArgb(40, 30, 28);
    public static readonly Color TailHair = TailLight, TailCore = TailDark, Under = FurDark;   // the stories' dust cloud paints squirrel-coloured tufts and limbs by these names
    public static readonly Color LimbNear = Color.FromArgb(206, 108, 46);
    public static readonly Color LimbFar = Color.FromArgb(158, 74, 32);

    /// <summary>
    /// A squirrel's colours (his ask: "give them some variations and personalities"). Red is the tawny one above;
    /// Grey an eastern grey, pale-bellied; Fox a fox squirrel, darker rust with a cinnamon front.
    /// </summary>
    public sealed record Coat(Color Fur, Color FurDark, Color Belly, Color TailDark, Color TailMain, Color TailLight)
    {
        public static readonly Coat Red = new(SquirrelArt.Fur, SquirrelArt.FurDark, SquirrelArt.Belly, SquirrelArt.TailDark, SquirrelArt.TailMain, SquirrelArt.TailLight);
        public static readonly Coat Grey = new(Color.FromArgb(138, 130, 120), Color.FromArgb(96, 88, 80), Color.FromArgb(238, 234, 226), Color.FromArgb(86, 80, 74), Color.FromArgb(150, 142, 132), Color.FromArgb(206, 200, 190));
        public static readonly Coat Fox = new(Color.FromArgb(172, 86, 40), Color.FromArgb(122, 56, 26), Color.FromArgb(236, 196, 140), Color.FromArgb(118, 52, 24), Color.FromArgb(182, 96, 44), Color.FromArgb(222, 150, 86));
        public static readonly Coat[] All = [Red, Grey, Fox];
    }

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
        public Coat Coat = Coat.Red;               // its colours
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
        q.TailBase = Lerp(150f, 100f, e) + 8f * lookUp;
        q.TailCurl = Lerp(-45f, -170f, e);                                 // the tip ends pointing forward and down: the hook of the question mark
        q.TailS = Lerp(25f, 80f, e);
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
        // At the front view the picture is MIRROR-SYMMETRIC: hip, chest and head on the middle line, the tail straight up
        // behind, the feet either side. The caller draws the second half of the turn as the first half mirrored, and a
        // symmetric middle page is the same picture both ways, so the flip is invisible. (With the tail curled to one
        // side at the middle, the plume jumped across the body in one frame: "the abrupt flipping".)
        q.Hip = P(Lerp(q.Hip.X, 0f, f), q.Hip.Y); q.Chest = P(Lerp(q.Chest.X, 0f, f), q.Chest.Y); q.Head = P(Lerp(q.Head.X, 0f, f), q.Head.Y);
        q.NearHind = P(Lerp(q.NearHind.X, 0.06f, f), q.NearHind.Y); q.FarHind = P(Lerp(q.FarHind.X, -0.10f, f), q.FarHind.Y);
        q.TailBase = 100f - 10f * f; q.TailCurl = Lerp(-170f, 0f, Smooth(f)); q.TailS = 80f * (1f - f);
        return q;
    }

    /// <summary>
    /// A RUNNING turn, on all fours (his ask: "maybe make it curl as it flips or something. some kind of transition
    /// animation instead of an abrupt 180degree flip"): k 0 is the profile, bounding; 0.5 the front view, the body
    /// bunched low over the planted forepaws with the head come round to face us and the tail whipped up and over;
    /// the caller mirrors from 0.5 on. The real move: a squirrel checks, plants its forefeet, swings its hindquarters
    /// round them with the tail lashing the other way for balance, and is off the way it came. In words: the
    /// front end stops, the back end comes round it.
    /// </summary>
    public static Pose Curl(float k)
    {
        float f = 1f - MathF.Abs(k - 0.5f) * 2f;                           // 0 profile, 1 front
        float e = Smooth(f);
        Pose q = Bound(0.22f, 2.2f);                                        // the gather: low over the forepaws
        q.HeadTurn = f;
        q.BodyScale = 1f - 0.5f * e;                                        // foreshortened as it comes round
        q.Hump = 0.08f + 0.14f * e;                                         // bunched
        q.Hip = P(Lerp(q.Hip.X, 0f, e), q.Hip.Y + 0.04f * e); q.Chest = P(Lerp(q.Chest.X, 0f, e), q.Chest.Y - 0.02f * e);
        q.Head = P(Lerp(q.Head.X, 0f, e), q.Head.Y - 0.02f * e); q.HeadTilt = Lerp(q.HeadTilt, 4f, e);
        q.NearFore = P(Lerp(q.NearFore.X, 0.10f, e), 0f); q.FarFore = P(Lerp(q.FarFore.X, -0.10f, e), 0f);
        q.NearHind = P(Lerp(q.NearHind.X, 0.14f, e), 0f); q.FarHind = P(Lerp(q.FarHind.X, -0.14f, e), 0f);
        // The tail: from streaming behind, up and over to straight up at the front view, with a lash that is largest a
        // third of the way round and gone at the middle (the middle page must mirror into itself).
        q.TailBase = Lerp(150f, 95f, e); q.TailCurl = Lerp(-45f, 0f, e); q.TailS = 25f * (1f - e);
        q.TailWave = 0f; q.TailFlick = 28f * MathF.Sin(MathF.PI * Math.Clamp(k * 2f, 0f, 1f));
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

    /// <summary>
    /// THE LOOK, taken from cute flat-vector squirrels (his line: "develop an eye for what cute animals look like in
    /// line art"): every shape is one smooth closed curve, never a stack of ovals with a pen line. The TAIL is the
    /// hero and as tall as the animal: a fat, smooth plume that leaves the rump going back and up and curls forward
    /// over the top in an S, rounded at the tip, with three long pale streaks following its curve and a darker band
    /// along its inner edge; no fringe, no spikes, no scallops. The BODY is a pear: a big round rump and belly at
    /// the bottom, narrowing up to the chest, a wide cream front from the chin down between the feet. The HEAD is
    /// a round skull with a short snout tipped a little down, one big round eye high and forward, a cream cheek,
    /// and tall ears with a tuft on top. Little hands tucked at the chest; long flat hind feet along the ground.
    /// Colours: a red squirrel's bright rust against the patch's dull earth.
    /// </summary>
    public static void Paint(Graphics g, Pose q)
    {
        Color Fur = q.Coat.Fur, FurDark = q.Coat.FurDark, Belly = q.Coat.Belly, TailDark = q.Coat.TailDark, TailMain = q.Coat.TailMain, TailLight = q.Coat.TailLight;   // the coat's colours shadow the red statics
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
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
        float front = q.HeadTurn;                                          // 0 side view, 1 facing us
        float bw = 1f - 0.45f * front;                                     // narrower seen from the front
        float upright = Math.Clamp((chest.Y - hip.Y) / 0.5f, 0f, 1f);      // 0 on all fours, 1 sitting up: the body's shape follows
        float sc = q.BodyScale;

        // ---- the tail first, behind everything ----
        {
            PointF root = P(hip.X - 0.14f * (1f - front), hip.Y + 0.02f);   // on the middle line at the front view (symmetric, see Turn)
            const int N = 16;
            float segLen = 1.25f / N;
            var spine = new PointF[N + 1];
            spine[0] = root;
            for (int i = 0; i < N; i++)
            {
                float s = (i + 0.5f) / N;
                float ang = q.TailBase + q.TailCurl * s + q.TailS * MathF.Sin(MathF.PI * s) + q.TailWave * MathF.Sin(q.TailWavePhase - i * 0.5f) + q.TailFlick * s * s;
                float r = ang * MathF.PI / 180f;
                spine[i + 1] = P(spine[i].X + segLen * MathF.Cos(r), spine[i].Y + segLen * MathF.Sin(r));
            }
            // Half the plume's width along it: narrow at the root, fat from a third of the way to near the end, a round tip.
            float Width(float s) => s < 0.35f ? Lerp(0.10f, 0.27f, Smooth(s / 0.35f)) : s < 0.80f ? Lerp(0.27f, 0.23f, (s - 0.35f) / 0.45f) : Lerp(0.23f, 0.06f, Smooth((s - 0.80f) / 0.20f));
            PointF[] Edge(float side, float scale, float shift = 0f)
            {
                var pts = new PointF[N + 2];
                for (int i = 0; i <= N; i++)
                {
                    PointF a = spine[Math.Max(0, i - 1)], b = spine[Math.Min(N, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float w = Width(i / (float)N) * scale + shift;
                    pts[i] = P(spine[i].X - dy / len * w * side, spine[i].Y + dx / len * w * side);
                }
                // The round tip: one point a little past the end along the tail's direction.
                PointF e = spine[N], f = spine[N - 1];
                float ex = e.X - f.X, ey = e.Y - f.Y, el = MathF.Sqrt(ex * ex + ey * ey) + 1e-5f;
                pts[N + 1] = P(e.X + ex / el * 0.07f * scale, e.Y + ey / el * 0.07f * scale);
                return pts;
            }
            PointF[] Plume(float scale, float shiftOuter, float shiftInner)
            {
                PointF[] l = Edge(1f, scale, shiftOuter), r = Edge(-1f, scale, shiftInner);
                var outline = new PointF[(N + 1) * 2 + 1];
                for (int i = 0; i <= N; i++) { outline[i] = l[i]; outline[2 * N + 2 - i] = r[i]; }
                outline[N + 1] = l[N + 1];
                return outline;
            }
            Blob(Plume(1f, 0f, 0f), TailDark, 0.45f);                                              // the whole plume, dark
            Blob(Plume(1f, -0.01f, -0.09f), TailMain, 0.45f);                                      // the main colour, leaving a dark band along the inner edge
            // Three long pale streaks following the curve, each a smooth line from a fifth of the way to near the tip.
            using (var streak = new Pen(TailLight, 0.030f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach ((float off, int from, int to) in ((float, int, int)[])[(0.55f, 3, N - 2), (0.15f, 5, N - 1), (-0.25f, 4, N - 4)])   // three streaks, each its own length, never a set of rails
                {
                    var pts = new List<PointF>();
                    for (int i = from; i <= to; i++)
                    {
                        PointF a = spine[i - 1], b = spine[i + 1];
                        float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                        float w = Width(i / (float)N) * off;
                        pts.Add(P(spine[i].X - dy / len * w, spine[i].Y + dx / len * w));
                    }
                    g.DrawCurve(streak, pts.ToArray(), 0.5f);
                }
        }

        // ---- the far legs: a hind foot and a hand, behind the body ----
        void Hand(PointF at, Color c) => Ell2(at, 0.05f, 0.04f, c);
        void FootFlat(PointF at, Color c) => Blob([P(at.X - 0.06f, at.Y + 0.0f), P(at.X - 0.04f, at.Y + 0.07f), P(at.X + 0.12f, at.Y + 0.06f), P(at.X + 0.24f, at.Y + 0.035f), P(at.X + 0.25f, at.Y + 0.0f)], c, 0.3f);
        void Arm(PointF shoulder, PointF hand, Color c, float thick)
        {
            const float L = 0.21f;
            float dx = hand.X - shoulder.X, dy = hand.Y - shoulder.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * L - 0.003f);
            float al = MathF.Acos(Math.Clamp(d / (2 * L), -1f, 1f));
            float th = MathF.Atan2(dy, dx) - al;                                           // the elbow back
            PointF elbow = P(shoulder.X + L * MathF.Cos(th), shoulder.Y + L * MathF.Sin(th));
            Bone(shoulder, elbow, thick, thick * 0.85f, c);
            Bone(elbow, hand, thick * 0.85f, thick * 0.7f, c);
            Hand(hand, c);
        }
        void HindLeg(PointF haunch, PointF foot, Color c, float thick, bool flat)
        {
            if (flat) { FootFlat(foot, c); return; }
            const float L = 0.21f;
            float dx = foot.X - haunch.X, dy = foot.Y - haunch.Y;
            float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * L - 0.003f);
            float al = MathF.Acos(Math.Clamp(d / (2 * L), -1f, 1f));
            float th = MathF.Atan2(dy, dx) + al;                                           // the hock forward (a squirrel's hind leg folds like a Z)
            PointF knee = P(haunch.X + L * MathF.Cos(th), haunch.Y + L * MathF.Sin(th));
            Bone(haunch, knee, thick, thick * 0.8f, c);
            Bone(knee, foot, thick * 0.8f, thick * 0.6f, c);
            Blob([P(foot.X - 0.04f, foot.Y), P(foot.X - 0.03f, foot.Y + 0.05f), P(foot.X + 0.10f, foot.Y + 0.045f), P(foot.X + 0.17f, foot.Y + 0.02f), P(foot.X + 0.17f, foot.Y - 0.005f)], c, 0.3f);
        }
        PointF shoulder = P(chest.X + 0.02f, chest.Y - 0.02f), haunch = P(hip.X + 0.02f, hip.Y - 0.04f);
        PointF pawL = P(q.Head.X + Lerp(0.04f, -0.07f, front), q.Head.Y - 0.22f), pawR = P(q.Head.X + Lerp(0.16f, 0.07f, front), q.Head.Y - 0.22f);   // either side of the chin at the front view
        if (front < 0.5f)
        {
            Arm(P(shoulder.X + 0.03f, shoulder.Y), q.PawsUp ? pawR : q.FarFore, FurDark, 0.075f);
            HindLeg(P(haunch.X + 0.03f, haunch.Y), q.FarHind, FurDark, 0.10f, q.HindFlat);
        }

        // ---- the body: one pear, built on the hip and the chest ----
        {
            // The outline runs from the throat, down the front over the belly, under the rump, up the back to the neck.
            PointF throat = P(chest.X + 0.14f * bw, chest.Y + 0.10f);
            PointF breast = P(chest.X + 0.22f * bw, chest.Y - 0.08f);
            PointF belly = P(mid.X + (0.16f + 0.10f * upright) * bw * sc, mid.Y - 0.22f - 0.04f * upright);
            PointF under = P(hip.X - 0.02f, hip.Y - 0.26f + 0.02f * upright);
            PointF rumpLow = P(hip.X - (0.28f + 0.06f * upright) * bw, hip.Y - 0.16f);
            PointF rumpBack = P(hip.X - (0.30f + 0.04f * upright) * bw, hip.Y + 0.06f);
            PointF back = P(mid.X - 0.10f * sc, mid.Y + 0.24f + 0.02f * upright);
            PointF wither = P(chest.X - 0.06f, chest.Y + 0.20f);
            PointF nape = P(chest.X + 0.04f, chest.Y + 0.20f);
            Blob([throat, breast, belly, under, rumpLow, rumpBack, back, wither, nape], Fur, 0.55f);
            // The cream front: chin to the lap, wide, one soft shape.
            Blob([P(chest.X + 0.10f * bw, chest.Y + 0.08f), P(chest.X + 0.19f * bw, chest.Y - 0.06f), P(mid.X + 0.14f * bw * sc, mid.Y - 0.18f), P(hip.X + 0.02f, hip.Y - 0.20f), P(hip.X - 0.02f, hip.Y - 0.08f), P(mid.X + 0.02f * sc, mid.Y - 0.06f), P(chest.X + 0.02f * bw, chest.Y - 0.02f)], Belly, 0.5f);
            // The haunch: a round mass over the rump, the thigh's muscle, in the fur colour with a soft shade line.
            Ell2(P(hip.X - 0.06f * bw, hip.Y - 0.02f), 0.21f * bw, 0.19f, Fur);   // (a translucent shade disc on it read as a patch; the mass alone is enough)
        }

        // ---- the head: a round skull, a short snout, the big eye, a cream cheek, tall tufted ears ----
        {
            var st = g.Save();
            g.TranslateTransform(q.Head.X, q.Head.Y);
            g.RotateTransform(q.HeadTilt);
            float sk = 0.20f * (1f - 0.08f * front), snout = 1f - front;
            // The ears: tall leaves standing up from the back of the skull, pink inside, a tuft of hair at the tip.
            void Ear(float ex, float tiltDeg, Color c)
            {
                Ell(g, ex, 0.22f, 0.062f, 0.13f, c, tiltDeg);
                Ell(g, ex + 0.004f, 0.21f, 0.034f, 0.085f, EarIn, tiltDeg);
                using var tuft = new Pen(FurDark, 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float a = (90f - tiltDeg) * MathF.PI / 180f;
                PointF top = P(ex + 0.13f * MathF.Cos(a), 0.22f + 0.13f * MathF.Sin(a));
                g.DrawLine(tuft, top, P(top.X - 0.015f, top.Y + 0.07f));
                g.DrawLine(tuft, top, P(top.X + 0.03f, top.Y + 0.06f));
            }
            Ear(-0.05f - 0.05f * front, -8f, front > 0.3f ? FurDark : Fur);
            if (front > 0.3f) Ear(0.08f + 0.03f * front, 8f, Fur);
            Ell(g, 0f, 0f, sk, 0.195f, Fur);                                               // the skull
            Blob([P(0.06f, 0.08f), P(0.26f * snout + 0.02f, -0.02f), P(0.28f * snout + 0.02f, -0.07f), P(0.20f * snout + 0.02f, -0.12f), P(0.02f, -0.14f)], Fur, 0.35f);   // the snout, short and tipped down
            Blob([P(0.04f, -0.04f), P(0.22f * snout + 0.02f, -0.07f), P(0.19f * snout + 0.02f, -0.13f), P(0.04f, -0.16f), P(-0.08f, -0.12f)], Belly, 0.4f);                 // the cream cheek and chin
            Ell(g, 0.27f * snout + 0.02f, -0.065f, 0.03f, 0.026f, Ink);                     // the nose
            using (var mouth = new Pen(Ink, 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(mouth, 0.14f * snout + 0.02f, -0.105f - 0.012f * q.Nib, 0.20f * snout + 0.02f, -0.085f);
            void Eye(float ex, float ey)
            {
                Ell(g, ex, ey, 0.058f, 0.062f, Ink);                                        // big and round
                Ell(g, ex + 0.018f, ey + 0.022f, 0.02f, 0.02f, Color.White);                  // the glint
                Ell(g, ex - 0.012f, ey - 0.03f, 0.009f, 0.009f, Color.FromArgb(200, 255, 255, 255));
            }
            Eye(0.07f - 0.05f * front, 0.05f);
            if (front > 0.3f) Eye(-0.08f, 0.05f);
            g.Restore(st);
        }

        // ---- the acorn: in the mouth, or in the paws ----
        if (q.Acorn == 1)
        {
            float ca = MathF.Cos(q.HeadTilt * MathF.PI / 180f), sa = MathF.Sin(q.HeadTilt * MathF.PI / 180f);
            const float mx = 0.24f, my = -0.09f;
            Acorn(g, P(q.Head.X + mx * ca - my * sa, q.Head.Y + mx * sa + my * ca), 8f + q.HeadTilt);
        }
        else if (q.Acorn == 2) Acorn(g, P(q.Head.X + 0.10f, q.Head.Y - 0.19f - 0.01f * q.Nib), 20f + 25f * q.Nib);

        // ---- the near legs, in front of the body ----
        Arm(shoulder, q.PawsUp ? pawL : q.NearFore, Fur, 0.08f);
        HindLeg(haunch, q.NearHind, Fur, 0.10f, q.HindFlat);
        if (front >= 0.5f)
        {
            Arm(P(shoulder.X + 0.03f, shoulder.Y), q.PawsUp ? pawR : q.FarFore, FurDark, 0.075f);
            HindLeg(P(haunch.X + 0.03f, haunch.Y), q.FarHind, FurDark, 0.10f, q.HindFlat);
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

    /// <summary>
    /// A squirrel seen from BEHIND, going away from us into a hole: the round rump with the hind feet either side, the
    /// back narrowing up to the shoulders, the back of the head with both ears, and the tail standing up behind it
    /// all, a tall plume curling over. Squirrel units, y up, the origin at the middle of the head (the hole's middle
    /// when it is stamped there): drawn ever smaller about that point, it goes down the hole.
    /// </summary>
    public static void PaintRear(Graphics g, Coat coat)
    {
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        // The tail first, behind everything: up from the rump, leaning a little right, curling over at the top.
        Blob([P(-0.02f, -0.50f), P(0.10f, -0.30f), P(0.22f, 0.05f), P(0.26f, 0.40f), P(0.14f, 0.62f), P(-0.06f, 0.58f), P(-0.10f, 0.30f), P(-0.06f, -0.05f), P(-0.12f, -0.40f)], coat.TailDark, 0.5f);
        Blob([P(-0.01f, -0.46f), P(0.09f, -0.28f), P(0.18f, 0.05f), P(0.21f, 0.36f), P(0.12f, 0.54f), P(-0.02f, 0.52f), P(-0.05f, 0.28f), P(-0.03f, -0.04f), P(-0.08f, -0.38f)], coat.TailMain, 0.5f);
        using (var streak = new Pen(coat.TailLight, 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawCurve(streak, [P(0.02f, -0.30f), P(0.10f, 0.0f), P(0.14f, 0.30f), P(0.08f, 0.48f)], 0.5f);
            g.DrawCurve(streak, [P(-0.03f, -0.20f), P(0.03f, 0.10f), P(0.05f, 0.34f)], 0.5f);
        }
        // The hind feet, flat on the bark either side of the rump.
        Ell(g, -0.24f, -0.66f, 0.11f, 0.045f, coat.FurDark);
        Ell(g, 0.24f, -0.66f, 0.11f, 0.045f, coat.FurDark);
        // The body: a pear from behind, wide at the rump, narrowing to the shoulders.
        Blob([P(-0.34f, -0.50f), P(-0.30f, -0.20f), P(-0.20f, 0.0f), P(-0.17f, -0.12f), P(0.17f, -0.12f), P(0.20f, 0.0f), P(0.30f, -0.20f), P(0.34f, -0.50f), P(0.18f, -0.66f), P(-0.18f, -0.66f)], coat.Fur, 0.55f);
        Ell(g, 0f, -0.42f, 0.26f, 0.22f, coat.Fur);
        Ell(g, 0.08f, -0.44f, 0.16f, 0.17f, coat.FurDark);                       // the shaded side of the rump (the moon is to the left)
        // The head from behind: a round skull, the two ears up, no face.
        Ell(g, 0f, 0f, 0.20f, 0.19f, coat.Fur);
        Ell(g, 0.07f, -0.02f, 0.12f, 0.15f, coat.FurDark);
        Ell(g, -0.12f, 0.20f, 0.062f, 0.12f, coat.Fur, -10f);
        Ell(g, 0.12f, 0.20f, 0.062f, 0.12f, coat.Fur, 10f);
        Ell(g, -0.12f, 0.19f, 0.03f, 0.08f, coat.FurDark, -10f);
        Ell(g, 0.12f, 0.19f, 0.03f, 0.08f, coat.FurDark, 10f);
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
