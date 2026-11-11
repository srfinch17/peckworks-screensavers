using System.Drawing.Drawing2D;

namespace Peckworks.Screensavers.Core.Animals;

/// <summary>
/// How a squirrel is drawn, and the poses it can take. THE SHARED ANIMAL LIBRARY'S SQUIRREL (2026-11-10): moved here
/// from Thanksgiving's SquirrelArt.cs, which approved it over many rounds, so every saver draws the SAME squirrel.
/// Thanksgiving's fence squirrel, acorn caper and patch life use it, and so do Christmas's pine squirrels. The
/// matching flip-books (the bound, the turns, the bending climb, ...) are built by SquirrelBooks, at the bottom of
/// this file, so a saver asks for "the squirrel's books at this size and coat" and gets the approved animation.
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
public static class Squirrel
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
    /// <remarks>
    /// The last three are OPTIONAL extras (2026-11-10, for Christmas's winter coat) and every existing coat leaves them at
    /// their defaults, which paint exactly the pixels the coat painted before: EarTuft (0 none; 1 the long dark tufts a red
    /// squirrel grows on its ears in winter), Flank (a different colour for the sides and thighs, null = the fur), TailBulk
    /// (1 = the tail's width as drawn; a winter tail is bushier).
    /// </remarks>
    public sealed record Coat(Color Fur, Color FurDark, Color Belly, Color TailDark, Color TailMain, Color TailLight, float EarTuft = 0f, Color? Flank = null, float TailBulk = 1f)
    {
        public static readonly Coat Red = new(Squirrel.Fur, Squirrel.FurDark, Squirrel.Belly, Squirrel.TailDark, Squirrel.TailMain, Squirrel.TailLight);
        public static readonly Coat Grey = new(Color.FromArgb(138, 130, 120), Color.FromArgb(96, 88, 80), Color.FromArgb(238, 234, 226), Color.FromArgb(86, 80, 74), Color.FromArgb(150, 142, 132), Color.FromArgb(206, 200, 190));
        public static readonly Coat Fox = new(Color.FromArgb(172, 86, 40), Color.FromArgb(122, 56, 26), Color.FromArgb(236, 196, 140), Color.FromArgb(118, 52, 24), Color.FromArgb(182, 96, 44), Color.FromArgb(222, 150, 86));
        public static readonly Coat[] All = [Red, Grey, Fox];
        /// <summary>
        /// A red squirrel in its WINTER coat (not in All: the autumn savers never deal it). Real ones: the back stays a
        /// rust red but a shade deeper, the flanks go GREY, the belly stays white, the ears grow long dark tufts, and the
        /// tail thickens into a bushier, paler plume. On a night-blue snowfield the rust back and cream belly are the
        /// readable parts, so the greys are kept light enough not to sink into a dark pine.
        /// </summary>
        public static readonly Coat Winter = new(Color.FromArgb(190, 96, 52), Color.FromArgb(120, 74, 54), Color.FromArgb(246, 238, 226), Color.FromArgb(120, 68, 44), Color.FromArgb(196, 120, 72), Color.FromArgb(236, 190, 140),
            EarTuft: 1f, Flank: Color.FromArgb(150, 134, 124), TailBulk: 1.18f);
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
        public int Item;                           // WHAT the nut-sized thing is (2026-11-10, Christmas): 0 an acorn (every autumn page), 1 a pine cone, 2 a sunflower seed
        public float Nib;                          // the mouth chewing, 0 to 1
        public float BodyScale = 1f;               // squash (under 1) or stretch (over 1) of the body along its length
        public float Rotate;                       // the whole picture turned about the feet's origin (degrees): -90 is head up a trunk
        public float Pitch;                        // the FRONT half (chest, head, forelegs) turned about the hip, degrees, the hind half left where it is: the body BENDS in the middle (-90 reaches straight up a trunk from a level stance). A climb begins with this, not with the whole body tipping like a card (his line: "it should be able to BEND in the middle").
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
        q.BodyScale = 1f - 0.15f * f;                                       // the body's length foreshortens a LITTLE as it comes round: a squirrel seen from the front is nearly as wide as it is long in profile (at 0.45 here and 0.45 again in Paint's bw the front view was a pillar 0.3 of the profile wide: "2d flipping", a card turned edge on)
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
        q.BodyScale = 1f - 0.2f * e;                                        // foreshortened a little as it comes round (a crouching squirrel seen from the front is a round ball, not a slot)
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
        if (q.Pitch != 0f)
        {
            // The bend: the front half swings about the hip. Chest, head and both forepaws turn together; the head keeps its
            // own tilt on top; the back humps over the bend by how far it is bent (a spine is a curve, not a hinge).
            float pr = q.Pitch * MathF.PI / 180f, cp = MathF.Cos(pr), sp = MathF.Sin(pr);
            PointF Swing(PointF a) => P(hip.X + (a.X - hip.X) * cp + (a.Y - hip.Y) * sp, hip.Y - (a.X - hip.X) * sp + (a.Y - hip.Y) * cp);
            q = new Pose
            {
                Hip = q.Hip, Chest = Swing(q.Chest), Head = Swing(q.Head), HeadTilt = q.HeadTilt - q.Pitch, HeadTurn = q.HeadTurn,
                NearFore = Swing(q.NearFore), FarFore = Swing(q.FarFore), NearHind = q.NearHind, FarHind = q.FarHind,
                Hump = q.Hump + 0.16f * MathF.Abs(q.Pitch) / 90f, TailBase = q.TailBase, TailCurl = q.TailCurl, TailWave = q.TailWave, TailWavePhase = q.TailWavePhase,
                TailFlick = q.TailFlick, TailS = q.TailS, HindFlat = q.HindFlat, PawsUp = q.PawsUp, Acorn = q.Acorn, Item = q.Item, Nib = q.Nib, BodyScale = q.BodyScale, Rotate = q.Rotate, Coat = q.Coat,
            };
            chest = q.Chest;
        }
        PointF mid = P((hip.X + chest.X) / 2 - q.Hump * 0.3f, (hip.Y + chest.Y) / 2 + q.Hump);
        float front = q.HeadTurn;                                          // 0 side view, 1 facing us
        float bw = 1f - 0.15f * front;                                     // a little narrower seen from the front (the chest and belly face us: a round body, not a slot)
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
                    float w = Width(i / (float)N) * q.Coat.TailBulk * scale + shift;   // TailBulk is 1 for every autumn coat (the same pixels as before)
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
        // The far legs are always BEHIND the body (they are farther from us at every angle; at the front view they stand beside
        // the near ones, splayed, and show below it). The first version moved them in front of the body past the middle page,
        // a pop at the very page where the turn's halves meet.
        Arm(P(shoulder.X + 0.03f, shoulder.Y), q.PawsUp ? pawR : q.FarFore, FurDark, 0.075f);
        HindLeg(P(haunch.X + 0.03f, haunch.Y), q.FarHind, FurDark, 0.10f, q.HindFlat);

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
            // The thighs: round masses over the rump, the muscle, in the fur colour. In profile one haunch, over the rump. As the
            // body comes round they SPLAY: the near thigh swings to our right and the far one (a shade darker) shows on the left,
            // symmetric at the front view (so the mirrored second half of a turn meets the first on the same picture).
            PointF nearThigh = P(hip.X - 0.06f * bw * (1f - front) + 0.16f * front, hip.Y - 0.02f);
            PointF farThigh = P(hip.X - 0.06f * bw * (1f - front) - 0.16f * front, hip.Y - 0.02f);
            // The cream front: chin to the lap, wide, one soft shape. As the body comes round to face us the cream swings across
            // the chest and belly (its inner edge moves back by "swing"), so the front view is a broad cream bib down the middle.
            float swing = 0.20f * front;
            PointF[] bib = [P(chest.X + 0.10f * bw, chest.Y + 0.08f), P(chest.X + 0.19f * bw, chest.Y - 0.06f), P(mid.X + 0.14f * bw * sc, mid.Y - 0.18f), P(hip.X + 0.02f + swing * 0.5f, hip.Y - 0.20f), P(hip.X - 0.02f - swing, hip.Y - 0.08f), P(mid.X + 0.02f * sc - swing, mid.Y - 0.06f), P(chest.X + 0.02f * bw - swing * 0.7f, chest.Y - 0.02f)];
            // In profile the thigh is in front of the belly's rear (the leg folds up beside it); past the half-way page the belly
            // faces us and lies in front of both thighs, which splay to the sides.
            // A coat with a Flank colour (the winter coat's grey sides): a soft band along the lower side of the body, under
            // the cream front and behind the thigh, and the thighs in the same grey. Null for every autumn coat: not painted.
            Color flank = q.Coat.Flank ?? Fur;
            if (q.Coat.Flank != null)
                Blob([P(hip.X - 0.10f * bw, hip.Y - 0.10f), P(mid.X - 0.04f, mid.Y - 0.06f), P(chest.X - 0.02f * bw, chest.Y - 0.04f), P(mid.X + 0.12f * bw * sc, mid.Y - 0.17f), P(hip.X + 0.04f, hip.Y - 0.21f)], flank, 0.5f);
            if (front > 0.5f)
            {
                if (front > 0.15f) Ell2(farThigh, 0.19f * bw, 0.18f, FurDark);
                Ell2(nearThigh, 0.21f * bw, 0.19f, flank);
                Blob(bib, Belly, 0.5f);
            }
            else
            {
                if (front > 0.15f) Ell2(farThigh, 0.19f * bw, 0.18f, FurDark);
                Blob(bib, Belly, 0.5f);
                Ell2(nearThigh, 0.21f * bw, 0.19f, flank);   // (a translucent shade disc on it read as a patch; the mass alone is enough)
            }
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
                if (q.Coat.EarTuft > 0f)
                {
                    // WINTER EAR TUFTS: red squirrels grow a long dark plume off each ear tip in winter, a brush that streams up
                    // and a little back (about as long again as the ear). Three tapering strokes in the dark fur colour, fanned,
                    // each ending in a point: three thin lines would read as whiskers, so the middle one is fatter.
                    float L = 0.17f * q.Coat.EarTuft;
                    using var plume = new Pen(FurDark, 0.040f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    using var thin = new Pen(FurDark, 0.024f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(plume, top, P(top.X - 0.020f, top.Y + L));
                    g.DrawLine(thin, top, P(top.X - 0.075f, top.Y + L * 0.80f));
                    g.DrawLine(thin, top, P(top.X + 0.045f, top.Y + L * 0.86f));
                }
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
            Thing(g, P(q.Head.X + mx * ca - my * sa, q.Head.Y + mx * sa + my * ca), 8f + q.HeadTilt, q.Item);
        }
        else if (q.Acorn == 2) Thing(g, P(q.Head.X + 0.10f, q.Head.Y - 0.19f - 0.01f * q.Nib), 20f + 25f * q.Nib, q.Item);

        // ---- the near legs, in front of the body ----
        Arm(shoulder, q.PawsUp ? pawL : q.NearFore, Fur, 0.08f);
        HindLeg(haunch, q.NearHind, Fur, 0.10f, q.HindFlat);
    }

    /// <summary>The thing a squirrel holds, by Pose.Item: 0 the acorn (what every autumn page draws), 1 a pine cone, 2 a sunflower seed.</summary>
    public static void Thing(Graphics g, PointF at, float angDeg, int item)
    {
        if (item == 0) { Acorn(g, at, angDeg); return; }
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.RotateTransform(angDeg);
        if (item == 1)
        {
            // A PINE CONE: a long brown egg with rows of scales (dark crescents across it, each a little lighter on its lower edge).
            Ell(g, 0f, 0f, 0.105f, 0.062f, Color.FromArgb(128, 84, 52));
            using var scale = new Pen(Color.FromArgb(150, 62, 40, 24), 0.014f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = -2; i <= 2; i++) g.DrawArc(scale, i * 0.034f - 0.03f, -0.055f, 0.06f, 0.11f, -70, 140);
            using var lit = new Pen(Color.FromArgb(110, 190, 142, 96), 0.010f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = -2; i <= 2; i++) g.DrawArc(lit, i * 0.034f - 0.025f, -0.042f, 0.05f, 0.09f, 40, 60);
        }
        else
        {
            // A SUNFLOWER SEED: a small pale egg with a dark stripe down its length.
            Ell(g, 0f, 0f, 0.052f, 0.032f, Color.FromArgb(226, 214, 190));
            using var stripe = new Pen(Color.FromArgb(180, 70, 60, 54), 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(stripe, -0.040f, 0f, 0.040f, 0f);
        }
        g.Restore(st);
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

/// <summary>One flip-book: its pages, the sheet's size, and where the feet's origin is on it.</summary>
public struct SquirrelSheet
{
    public Sprite[] Pages;
    public int W, H, Ox, By;
}

/// <summary>
/// THE SQUIRREL'S FLIP-BOOKS at one size and coat, painted once (moved out of Thanksgiving's PatchLife.Build, 2026-11-10,
/// where each page count, size and anchor was settled over many rounds; every comment below is that record).
///
/// FEYNMAN VERSION: a flip-book is a stack of pictures of one motion; the saver turns the pages by how far the animal has
/// walked (so a planted foot never slides) and slides the whole picture across the screen. This class paints every motion
/// the approved squirrel has: bound, sit, look, dig, rise, turn, curl, eat, hop, climb up, climb down, and the two pitches
/// onto and off the bark. A saver asks for Squirrel books at "k" pixels to the squirrel unit and gets them all.
///
/// Page sizes: the ground sheets are 2.55 by 1.9 units with the feet at (1.75, 1.9) from the top left, so the sheet holds a
/// whole bound, a tail up over the head and a hop. The climbing sheets are tall (1.9 by 2.55). The mount sheets are square
/// (4.0 units) because the body turns about its hind feet through 90 degrees.
/// </summary>
public sealed class SquirrelBooks
{
    public const float BoundLen = 2.2f;                 // the bound the run pages are painted at, in squirrel units
    public const int RunPages = 32, RisePages = 6, TurnPages = 10, CurlPages = 12, MountPages = 12, HopPages = 22, LookPages = 4, DigPages = 8;

    public float K;                                     // pixels to the squirrel unit
    public Squirrel.Coat Coat = Squirrel.Coat.Red;
    public SquirrelSheet Run, RunNut, Look, Dig, Rise, Turn, Eat, Curl, Hop, Up, Down, MountUp, MountDown;
    public SquirrelSheet[] Sit = new SquirrelSheet[4];
    public int UpFoot, DownHead;                        // in the climbing sheets, how far below the sheet's origin the feet (Up) or the head (Down) actually are: measured off the painted page, so the climb is anchored where the body is
    public int UpHead;                                  // in the head-up climbing sheet, how far ABOVE the origin the top of the head is (the first row painted)
    public int UpCol, DownCol, MountUpCol, MountDownCol;   // the painted body's middle column on each climbing sheet (measured)
    public int GroundW, GroundH, GroundOx, GroundBy;    // the ground sheets' own size, so a saver can paint more pages on the same sheet (its own poses)

    public static SquirrelSheet Make(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
    {
        var sh = new SquirrelSheet { Pages = new Sprite[n], W = w, H = h, Ox = ox, By = by };
        for (int i = 0; i < n; i++) sh.Pages[i] = page(i);
        return sh;
    }

    /// <summary>The first row of a page that has any paint in it (the top of the body on that page).</summary>
    public static int HighestRow(Sprite page)
    {
        for (int y = 0; y < page.Height; y++)
            for (int x = 0; x < page.Width; x++)
                if (page.PixelAt(x, y) >> 24 != 0) return y;
        return 0;
    }

    /// <summary>The lowest row of a page that has any paint in it (the bottom of the body on that page).</summary>
    public static int LowestRow(Sprite page)
    {
        for (int y = page.Height - 1; y >= 0; y--)
            for (int x = 0; x < page.Width; x++)
                if (page.PixelAt(x, y) >> 24 != 0) return y;
        return page.Height - 1;
    }

    /// <summary>The middle column of what is painted on a page (the body's spine, for an upright squirrel).</summary>
    public static int MiddleColumn(Sprite page)
    {
        int lo = page.Width, hi = -1;
        for (int y = 0; y < page.Height; y++)
            for (int x = 0; x < page.Width; x++)
                if (page.PixelAt(x, y) >> 24 != 0) { if (x < lo) lo = x; if (x > hi) hi = x; }
        return hi < 0 ? page.Width / 2 : (lo + hi) / 2;
    }

    /// <summary>
    /// The two angles of a mount page at f (0 to 1): the whole body's turn and the front half's bend. Up onto the bark: the
    /// bend leads (reaching up) over the first 0.55, then the body turns up while the bend straightens. Down off it (the
    /// dismount, drawn mirrored, head first): the front half bends level toward the ground first, then the body comes down.
    /// </summary>
    public static (float Rot, float Pitch) MountAngles(float f, bool up)
    {
        static float S(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
        if (up)
        {
            if (f < 0.55f) return (0f, -90f * S(f / 0.55f));
            float e = S((f - 0.55f) / 0.45f);
            return (-90f * e, -90f * (1f - e));
        }
        else
        {
            if (f < 0.55f) return (90f, -90f * S(f / 0.55f));
            float e = S((f - 0.55f) / 0.45f);
            return (90f * (1f - e), -90f * (1f - e));
        }
    }

    /// <summary>The clinging pose of page i (of 32): the bound kept to the bark, the tail along the trunk (the bound's trailing arc stuck out sideways past the bark), turned by rot degrees.</summary>
    public static Squirrel.Pose Cling(int i, float rot, Squirrel.Coat coat)
    {
        var q = Squirrel.Bound(i / 32f, BoundLen, cling: true);
        q.TailBase = 172f; q.TailCurl = -12f; q.TailS = 6f; q.TailWave = 4f; q.Rotate = rot; q.Coat = coat;
        return q;
    }

    public static SquirrelBooks Build(Squirrel.Coat coat, float k)
    {
        var sq = new SquirrelBooks { K = k, Coat = coat };
        int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
        sq.GroundW = w; sq.GroundH = h; sq.GroundOx = ox; sq.GroundBy = by;
        SquirrelSheet Ground(int n, Func<int, Squirrel.Pose> pose) => Make(n, w, h, ox, by, i => { var q = pose(i); q.Coat = coat; return Squirrel.Page(q, k, w, h, ox, by); });
        sq.Run = Ground(RunPages, i => Squirrel.Bound(i / (float)RunPages, BoundLen));   // 32: at 16 a bound 2.2 lengths long stepped the tail a seventh of a wave a page
        for (int t = 0; t < 4; t++) { int tl = t; sq.Sit[t] = Ground(1, _ => Squirrel.Sit(1f, tl, false, false)); }
        sq.Look = Ground(LookPages, i =>
        {
            // Between the sit and the look: the head, its tilt and the tail's root eased between the two poses.
            float f = i / (LookPages - 1f); f = f * f * (3f - 2f * f);
            Squirrel.Pose a = Squirrel.Sit(1f, 0, false, false), b = Squirrel.Sit(1f, 0, true, false);
            a.Head = new PointF(a.Head.X + (b.Head.X - a.Head.X) * f, a.Head.Y + (b.Head.Y - a.Head.Y) * f);
            a.HeadTilt += (b.HeadTilt - a.HeadTilt) * f; a.TailBase += (b.TailBase - a.TailBase) * f;
            return a;
        });
        sq.Dig = Ground(DigPages, i => Squirrel.Dig(i / (float)DigPages));
        sq.Rise = Ground(RisePages, i => Squirrel.Sit(0.2f + 0.8f * i / (RisePages - 1), 0, false, false));   // the crouch (page 0) up to the sit (the last)
        sq.Turn = Ground(TurnPages, i => Squirrel.Turn(0.5f * (i + 1) / TurnPages));   // sitting, coming round from the profile to the front view; played back again mirrored
        sq.Curl = Ground(CurlPages, i => Squirrel.Curl(0.5f * (i + 1) / CurlPages));   // running, the same way round
        sq.RunNut = Ground(RunPages, i => { var q = Squirrel.Bound(i / (float)RunPages, BoundLen); q.Acorn = 1; return q; });   // the bound with a nut in its mouth
        sq.Eat = Ground(4, i => { var q = Squirrel.Sit(1f, 0, false, true); q.Acorn = 2; q.Nib = i / 3f; return q; });   // sitting up with the nut in its paws, chewing
        sq.Hop = Ground(HopPages, i => Squirrel.Hop((i + 0.5f) / HopPages));   // the hop from a sit onto a pumpkin, and off again
        // Climbing (the caper's pages): head up the trunk, and head-first down it; pitching up onto the bark and off it.
        int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
        sq.Up = Make(RunPages, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i => Squirrel.Page(Cling(i, -90f, coat), k, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3));
        sq.Down = Make(RunPages, cw, ch, 3, (int)(1.75f * k) + 3, i => Squirrel.Page(Cling(i, 90f, coat), k, cw, ch, 3, (int)(1.75f * k) + 3));
        // Where the body really is on those pages: the feet are the lowest painted row of the head-up page, the head the lowest of the head-down page (the first cut guessed 1.75 k and the climb's first frame jumped 59 px from the mount's last).
        // The head-up climb is anchored at its ORIGIN: the clinging pose's hind feet sit at the pose's origin, and the whole
        // picture turns about it, so the origin IS the hind feet. (The first version anchored by the lowest painted row, which on
        // a head-up squirrel is the TAIL'S TIP hanging below the feet: the climb's first frame sat 0.8 of a length above the
        // mount's last.) Head-first down, the lowest row really is the head.
        sq.UpFoot = 0;
        sq.DownHead = LowestRow(sq.Down.Pages[0]) - sq.Down.By;
        sq.UpHead = sq.Up.By - HighestRow(sq.Up.Pages[0]);
        sq.UpCol = MiddleColumn(sq.Up.Pages[0]); sq.DownCol = MiddleColumn(sq.Down.Pages[0]);
        int sqw = (int)(4.0f * k) + 6, so = sqw / 2;
        // The pitch onto the bark and off it: the CLINGING body (the same pose the climb's first page has, so the seam into the
        // climb is the same picture) swung about its hind feet from level to upright, eased (slow at both ends), twelve pages over
        // 0.42 s. The first version swung a SITTING squirrel up in four jumps and switched to the cling on the last page: a card flip.
        // The mount BENDS (his line: "it should be able to BEND in the middle so that it looks like it would look if an animal
        // went to climb a tree"): first the front half reaches up the bark (Pitch 0 to -90 about the hip, the hind feet still on
        // the ground, the back humped), then the hind end follows (the whole body turns up, Rotate 0 to -90, while the bend
        // straightens, Pitch back to 0), ending on exactly the climb's first page. The dismount is the same the other way about:
        // head down the trunk, the front half reaches for the ground and bends level, then the hind end comes down after it.
        // The hind feet stand OFF the trunk while the front half reaches up, and jump the gap onto the bark as the body turns up:
        // the DRAW moves the page's origin from where the run ended to the trunk's column over the second phase, so the run's last
        // position, the mount's first page and the climb's first page all agree on where the feet are.
        sq.MountUp = Make(MountPages, sqw, sqw, so, so, i => { var (rot, pitch) = MountAngles((i + 1) / (float)MountPages, up: true); var q = Cling(0, rot, coat); q.Pitch = pitch; return Squirrel.Page(q, k, sqw, sqw, so, so); });
        sq.MountDown = Make(MountPages, sqw, sqw, so, so, i => { var (rot, pitch) = MountAngles((i + 1) / (float)MountPages, up: false); var q = Cling(0, rot, coat); q.Pitch = pitch; return Squirrel.Page(q, k, sqw, sqw, so, so); });
        sq.MountUpCol = MiddleColumn(sq.MountUp.Pages[MountPages - 1]); sq.MountDownCol = MiddleColumn(sq.MountDown.Pages[0]);   // the upright page: the same picture as the climb's first, so the two seams meet
        return sq;
    }
}