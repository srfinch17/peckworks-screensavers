using System.Drawing.Drawing2D;
using System.Numerics;
using Peckworks.Screensavers.Core;

namespace Christmas;

// ============================================================================
// OTTER ART: how a river otter is drawn, and every pose it can take.
//
// WHAT AN OTTER IS (written down BEFORE any code, from what I know of the
// animal). A river otter is LONG and LOW: 1.1 m from nose to tail tip, and a
// third of that is TAIL. So head and body are about 0.73 m and the tail 0.37 m.
// It stands only 0.25 m high at the shoulder because the legs are SHORT (about
// 0.10 m of thigh and 0.10 m of shin, the paws webbed and rounded). The body is
// a thick muscular tube, the same width from chest to hip (about 0.15 m across),
// and it is the TAIL that tapers: thick as a wrist at the root, thin at the tip,
// carried low and trailing. The HEAD is flat and broad, a little like a cat's
// squashed flat, with small round ears set low and far apart, small bead eyes
// set near the front, and a big MUZZLE with a broad pad and long pale whiskers.
// The coat is dark chocolate brown, glossy when wet (a few pale streaks of
// moonlight along the back), with a pale silver-buff throat, chin and muzzle.
//
// HOW IT MOVES. On land an otter does not walk, it GALLOPS in a looping arch,
// the way a caterpillar loops: the back HUMPS up as the hind feet swing forward
// to land just AHEAD of the forefeet (the gather), then the body STRETCHES long
// and low and flies with the forelegs reaching and the hind legs trailing, then
// the forefeet land and the back humps again. On ice it does the otter's
// famous trick: a few bounds, then it throws itself on its BELLY, forelegs
// tucked back along its sides, and glides a long way, steering with its body.
// It stands upright like a periscope on its hind legs with its tail as a third
// leg. It sits up to eat a fish held in both forepaws, head tilted back. It
// rolls on its back in snow, paws waving.
//
// HOW IT IS DRAWN (the cheap trick that makes an animal that turns in space).
// Painting one picture of an otter for every direction it could face would be
// hundreds of hand-drawn pictures. Instead the otter is a SKELETON of 3D points
// (a spine, a head, a tail of seven beads, four legs of two bones each) and one
// painter turns any skeleton into a picture seen from the viewer's place: the
// body is turned about its upright axis by a "yaw" (0 = side on, 90 = coming
// straight at us, -90 = going straight away), and every part is a thick tapered
// stroke (a "capsule", like a sausage with round ends) or a ball, drawn far to
// near. That is why a turn is a real turn: the same skeleton is painted at nine
// yaws, and the otter steps through them as its heading comes round. Left
// facing is the same picture mirrored.
//
// UNITS: metres in the body frame. x forward, y UP, z toward the viewer's side
// of the body (the near side when side on). The origin is on the ground under
// the middle of the body. One pixel scale "p" (pixels in a metre) paints it.
// ============================================================================
internal sealed class Skel
{
    // The indexes of the points in P.
    public const int Head = 0, Muzzle = 1, Neck = 2, Chest = 3, Mid = 4, Hip = 5;
    public const int Tail0 = 6, TailBeads = 7;              // 7 beads: Tail0 .. Tail0 + 6
    public const int Top0 = 13, Knee0 = 17, Paw0 = 21;      // four legs: 0 near fore, 1 far fore, 2 near hind, 3 far hind
    public const int FishA = 25, FishB = 26, Count = 27;    // the fish's head end and tail end

    public readonly Vector3[] P = new Vector3[Count];
    public float Mouth;     // 0 shut, 1 wide open
    public float Blink;     // 0 open, 1 shut
    public float Fish;      // 0 none, else the fish's size (1 whole, 0.3 a stub)
    public float Ear, Whisk;  // an ear flick (0 to 1), whiskers twitched up (0 to 1)
    public bool Ring;         // paint the pale waterline ring round a head in the water
    public float Clip = 0f; // 1: the part of the body below the water (y under 0) is hidden; 0: nothing is hidden

    public Skel Copy()
    {
        var k = new Skel { Mouth = Mouth, Blink = Blink, Fish = Fish, Clip = Clip, Ear = Ear, Whisk = Whisk, Ring = Ring };
        Array.Copy(P, k.P, Count);
        return k;
    }

    /// <summary>A pose part way between two poses (every point slides straight from one to the other): the "tween" between two key drawings.</summary>
    public static Skel Lerp(Skel a, Skel b, float t)
    {
        var k = new Skel();
        for (int i = 0; i < Count; i++) k.P[i] = Vector3.Lerp(a.P[i], b.P[i], t);
        k.Mouth = a.Mouth + (b.Mouth - a.Mouth) * t;
        k.Blink = a.Blink + (b.Blink - a.Blink) * t;
        k.Fish = t < 0.5f ? a.Fish : b.Fish;
        k.Clip = t < 0.5f ? a.Clip : b.Clip;
        k.Ear = a.Ear + (b.Ear - a.Ear) * t; k.Whisk = a.Whisk + (b.Whisk - a.Whisk) * t;
        k.Ring = t < 0.5f ? a.Ring : b.Ring;
        return k;
    }
}

internal static class OttersArt
{
    public const int Bins = 9;                                           // yaws from -90 to 90 in steps of 22.5 degrees
    public static float YawDeg(int bin) => (bin - 4) * 22.5f;
    public static int BinOf(float deg) => Math.Clamp((int)MathF.Round(deg / 22.5f) + 4, 0, Bins - 1);

    /// <summary>How high above the ground the picture looks down on the ice: a low look (about 12 degrees), because the ice is a flat plate seen nearly edge on.</summary>
    private const float Elev = 12f * MathF.PI / 180f;

    // ---- the colours: a few flat tones, a lighter belly/throat, a darker far side ----
    private static readonly Color Fur = Color.FromArgb(86, 58, 42);
    private static readonly Color FurFar = Color.FromArgb(58, 39, 30);
    private static readonly Color FurLit = Color.FromArgb(124, 90, 66);
    private static readonly Color Throat = Color.FromArgb(206, 184, 150);
    private static readonly Color Muzz = Color.FromArgb(176, 146, 112);
    private static readonly Color Pad = Color.FromArgb(46, 32, 28);
    private static readonly Color Ink = Color.FromArgb(12, 8, 10);
    private static readonly Color Sheen = Color.FromArgb(176, 196, 232);
    private static readonly Color FishBack = Color.FromArgb(122, 142, 176);
    private static readonly Color FishSide = Color.FromArgb(206, 218, 236);

    // ======================================================================== the skeleton builders

    private static float Sm(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }
    private static Vector3 V(float x, float y, float z = 0f) => new(x, y, z);
    private static float Rad(float deg) => deg * MathF.PI / 180f;

    /// <summary>Puts the head on the neck: "yaw" turns it left or right, "pitch" lifts it (degrees). The muzzle points a little lower than the skull.</summary>
    private static void HeadAt(Skel k, Vector3 neck, float yaw, float pitch)
    {
        float cy = MathF.Cos(Rad(yaw)), sy = MathF.Sin(Rad(yaw));
        Vector3 Dir(float pit) => new(MathF.Cos(Rad(pit)) * cy, MathF.Sin(Rad(pit)), MathF.Cos(Rad(pit)) * sy);
        k.P[Skel.Neck] = neck;
        k.P[Skel.Head] = neck + Dir(pitch) * 0.09f;
        k.P[Skel.Muzzle] = k.P[Skel.Head] + Dir(pitch - 9f) * 0.07f;
    }

    /// <summary>
    /// The tail: seven beads laid out from its root along a direction that bends a little more at each bead.
    /// "baseDeg" is the angle of the first piece above straight back (negative: drooping); "wave" is how much each
    /// piece bends more (degrees), moving along the tail with "phase"; "zWave" swings it sideways. With "ground", a
    /// bead never sinks below the snow it lies on.
    /// </summary>
    private static void Tail(Skel k, Vector3 root, float baseDeg, float wave, float phase, float zWave = 0f, bool ground = true, float zBase = 0f)
    {
        float ang = Rad(baseDeg);
        Vector3 p = root;
        for (int i = 0; i < Skel.TailBeads; i++)
        {
            ang += Rad(wave * MathF.Sin(phase - 0.75f * i));
            float step = 0.05f;
            p += new Vector3(-MathF.Cos(ang) * step, MathF.Sin(ang) * step, 0f);
            float z = zBase * (1f - i / 7f) + zWave * MathF.Sin(phase - 0.7f * i) * (i + 1) / 7f;
            float y = p.Y;
            if (ground) y = MathF.Max(y, TailR(i) * 0.9f);
            k.P[Skel.Tail0 + i] = new Vector3(p.X, y, z);
        }
    }

    /// <summary>The tail's thickness at bead i: a wrist at the root, a pencil at the tip.</summary>
    private static float TailR(int i) => 0.044f * MathF.Pow(1f - i / 8.2f, 1.0f) + 0.007f;

    /// <summary>A leg of two bones from "top" to "paw". The hind leg's knee points FORWARD (its hock bends back), the foreleg's elbow points BACK.</summary>
    private static void Leg(Skel k, int i, Vector3 top, Vector3 paw)
    {
        bool hind = i >= 2;
        float l1 = hind ? 0.105f : 0.095f, l2 = hind ? 0.105f : 0.095f;
        float dx = paw.X - top.X, dy = paw.Y - top.Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1e-4f) { dx = 0; dy = -1; d = 1e-4f; }
        float dc = Math.Clamp(d, 0.03f, l1 + l2 - 0.003f);          // out of reach: the paw is drawn in at the reach (a hair of slide, never a stretched leg)
        float ux = dx / d, uy = dy / d;
        float a = (l1 * l1 - l2 * l2 + dc * dc) / (2f * dc);
        float h = MathF.Sqrt(MathF.Max(0f, l1 * l1 - a * a));
        float sign = hind ? 1f : -1f;
        float kx = top.X + ux * a + (-uy) * h * sign, ky = top.Y + uy * a + ux * h * sign;
        k.P[Skel.Top0 + i] = top;
        k.P[Skel.Knee0 + i] = new Vector3(kx, ky, top.Z);
        k.P[Skel.Paw0 + i] = new Vector3(top.X + ux * dc, top.Y + uy * dc, paw.Z);
    }

    private static float LegZ(int i) => (i % 2 == 0) ? 0.056f : -0.056f;

    /// <summary>The four legs stood on the ground under their tops, a little forward (fore) or back (hind): the resting stance.</summary>
    private static void StandLegs(Skel k, float footDown = 0.0f)
    {
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2;
            Vector3 root = k.P[hind ? Skel.Hip : Skel.Chest];
            Vector3 top = new(root.X, root.Y - 0.02f, LegZ(i));
            Leg(k, i, top, new Vector3(root.X + (hind ? 0.03f : 0.02f) + (i == 1 || i == 3 ? 0.012f : 0f), 0.024f + footDown, LegZ(i) * 1.05f));
        }
    }

    /// <summary>Standing on four legs at rest (also the pose every walk and every transition begins and ends on).</summary>
    public static Skel Rest(float headYaw = 0f, float headPitch = 4f, float by = 0.15f, float blink = 0f, float ear = 0f, float whisk = 0f, float curl = 0f)
    {
        var k = new Skel { Blink = blink, Ear = ear, Whisk = whisk };
        k.P[Skel.Hip] = V(-0.17f, by);
        k.P[Skel.Mid] = V(0f, by + 0.012f);
        k.P[Skel.Chest] = V(0.17f, by);
        HeadAt(k, V(0.265f, by + 0.022f), headYaw, headPitch);
        StandLegs(k);
        Tail(k, k.P[Skel.Hip] + V(-0.05f, -0.025f), -16f, 3f, 0f);
        // The tail tip curls up (an idle fidget): the last three beads lift, each more than the one before.
        if (curl > 0f) for (int i = 4; i < Skel.TailBeads; i++) { Vector3 p = k.P[Skel.Tail0 + i]; k.P[Skel.Tail0 + i] = new Vector3(p.X + 0.012f * curl * (i - 3), p.Y + 0.03f * curl * (i - 3) * (i - 3) * 0.5f, p.Z); }
        return k;
    }

    /// <summary>Scratching with a hind foot: the near hind paw comes up toward the neck and strokes ("stroke" -1 to 1), the head turned and dipped to meet it ("a" 0 to 1 is how far up the leg is, "hy" how far the head has turned).</summary>
    public static Skel Scratch(float a, float stroke, float hy)
    {
        Skel k = Rest(30f * hy, 4f - 14f * hy);
        Vector3 hip = k.P[Skel.Hip];
        Leg(k, 2, new Vector3(hip.X, hip.Y - 0.02f, LegZ(2)), new Vector3(hip.X + 0.03f + 0.21f * a + 0.025f * stroke * a, 0.024f + 0.15f * a, LegZ(2) * 1.1f));
        return k;
    }

    /// <summary>A copy of a pose moved "dx" metres along x, and turned to face the other way when "flip" (x and z reversed): two otters facing each other on one sheet.</summary>
    public static Skel Place(Skel s, float dx, bool flip)
    {
        Skel k = s.Copy();
        for (int i = 0; i < Skel.Count; i++) { Vector3 p = s.P[i]; k.P[i] = new Vector3((flip ? -p.X : p.X) + dx, p.Y, flip ? -p.Z : p.Z); }
        return k;
    }

    /// <summary>
    /// Where a foot is on one page of the gallop. A foot is ON THE GROUND from "a" to "b" (fractions of the cycle) and
    /// then swings through the air to land again at "a"; while it is down it slides back under the body by exactly the
    /// ground the body covers (stride "D" metres a cycle), which is what keeps a planted foot planted. "xl" is where it
    /// lands (metres ahead of the body's middle).
    /// </summary>
    private static Vector3 Foot(float ph, float a, float b, float xl, float D, float lift, float swing, float z)
    {
        float u = ph - a; u -= MathF.Floor(u);
        float c = b - a;
        if (u < c) return V(xl - D * u, 0.024f, z);
        float v = (u - c) / (1f - c), x0 = xl - D * c;
        float w = MathF.Sin(MathF.PI * v);
        // The swing back to the landing spot is held late (a power of the eased curve) so a hind leg TRAILS behind the stretched body before it snaps forward to land.
        float x = x0 + (xl - x0) * MathF.Pow(Sm(v), 1.8f) + swing * w;
        return V(x, 0.024f + lift * w, z);
    }

    /// <summary>
    /// The gallop, page "ph" of a cycle (0 to 1). Gathered at 0 (back humped, hind feet landed ahead under the body),
    /// stretched and flying at about 0.55 (long and low, forelegs reaching, hind legs trailing), fore feet land at 0.70.
    /// An otter's real bound, a looping arch: hump, stretch, hump.
    /// </summary>
    public static Skel Gallop(float ph)
    {
        const float D = 0.80f;
        ph -= MathF.Floor(ph);
        var k = new Skel();
        float s = 0.5f + 0.5f * MathF.Cos(MathF.Tau * (ph - 0.55f));        // 1 stretched, 0 gathered
        float lb = 0.26f + 0.17f * s;                                         // chest to hip
        float air = MathF.Sin(MathF.PI * Math.Clamp((ph - 0.28f) / 0.42f, 0f, 1f));
        float by = 0.15f + 0.075f * air;
        float arch = 0.115f * (1f - s) * (1f - s);
        k.P[Skel.Hip] = V(-lb / 2, by + 0.05f * (1f - s));
        k.P[Skel.Chest] = V(lb / 2, by - 0.005f);
        k.P[Skel.Mid] = V(0f, (k.P[Skel.Hip].Y + k.P[Skel.Chest].Y) / 2 + arch);
        HeadAt(k, V(lb / 2 + 0.095f, by + 0.03f - 0.035f * s + 0.03f * (1f - s)), 0f, 10f - 18f * s);
        float ph2 = ph - 0.03f;                                               // the far legs a hair behind the near ones
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2; bool near = i % 2 == 0;
            float pp = near ? ph : ph2;
            Vector3 root = k.P[hind ? Skel.Hip : Skel.Chest];
            Vector3 top = new(root.X, root.Y - 0.02f, LegZ(i));
            Vector3 paw = hind ? Foot(pp, 0.05f, 0.28f, -0.04f, D, 0.075f, -0.10f * s, LegZ(i) * 1.05f)
                               : Foot(pp, 0.70f, 0.95f, 0.30f, D, 0.065f, 0.07f * s, LegZ(i) * 1.05f);
            Leg(k, i, top, paw);
        }
        Tail(k, k.P[Skel.Hip] + V(-0.05f, -0.02f), 12f - 34f * (1f - s), 7f, MathF.Tau * ph, 0.03f);
        k.Mouth = 0.18f * (1f - s);
        return k;
    }

    /// <summary>The belly slide: flat on the ice, forelegs tucked back along the sides, hind legs trailing, the head up. "w" (-1 to 1) swings the body in a slow S so it reads as steering.</summary>
    public static Skel Glide(float w)
    {
        var k = new Skel();
        float by = 0.078f;
        k.P[Skel.Hip] = V(-0.17f, by, -0.03f * w);
        k.P[Skel.Mid] = V(0f, by + 0.004f, 0f);
        k.P[Skel.Chest] = V(0.17f, by + 0.006f, 0.034f * w);
        HeadAt(k, V(0.265f, by + 0.05f, 0.05f * w), 14f * w, 16f);
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2; float z = LegZ(i);
            Vector3 root = k.P[hind ? Skel.Hip : Skel.Chest];
            Vector3 top = new(root.X, root.Y - 0.015f, root.Z + z);
            Vector3 paw = hind ? new Vector3(root.X - 0.20f, 0.034f, root.Z + z * 0.9f + 0.02f * w)
                               : new Vector3(root.X - 0.13f, 0.040f, root.Z + z * 1.45f);
            Leg(k, i, top, paw);
        }
        Tail(k, k.P[Skel.Hip] + V(-0.05f, -0.01f), 3f, 6f, 1.3f * w, 0.05f, true, -0.03f * w);
        return k;
    }

    /// <summary>Standing UPRIGHT on its hind feet and tail like a periscope. "up" lifts the head (0 level, 1 right back looking at the sky).</summary>
    public static Skel Upright(float up, float alpha = 76f, float sway = 0f)
    {
        var k = new Skel();
        float a = Rad(alpha);
        Vector3 hip = V(-0.12f, 0.085f);
        Vector3 chest = hip + V(MathF.Cos(a), MathF.Sin(a)) * 0.34f;
        k.P[Skel.Hip] = hip;
        k.P[Skel.Chest] = chest;
        k.P[Skel.Mid] = (hip + chest) / 2 + V(0.025f, 0.012f);
        k.P[Skel.Hip].Z = 0f;
        HeadAt(k, chest + V(MathF.Cos(a + 0.15f), MathF.Sin(a + 0.15f)) * 0.10f, sway * 25f, 10f + 55f * up - Alpha(alpha) * 0.2f);
        for (int i = 0; i < 4; i++)
        {
            float z = LegZ(i);
            if (i < 2)    // forelegs held up at the chest, paws curled
            {
                Vector3 top = chest + V(0.03f, -0.03f, z);
                Leg(k, i, top, chest + V(0.085f, -0.13f + 0.01f * sway, z * 0.7f));
            }
            else          // hind legs: knees forward, big flat feet on the snow in front of the haunches
            {
                Vector3 top = hip + V(0.0f, -0.03f, z);
                Leg(k, i, top, V(hip.X + 0.12f, 0.026f, z * 1.1f));
            }
        }
        Tail(k, hip + V(-0.05f, -0.04f), -6f, 4f, 0.5f);
        return k;
    }
    private static float Alpha(float a) => a - 76f;

    /// <summary>Sitting up to eat: hunched (more slumped than the periscope), the head back, a fish held in both forepaws at the chest. "chew" 0 to 1 opens and shuts the mouth; "fish" is how much of it is left.</summary>
    public static Skel SitEat(float chew, float fish)
    {
        var k = Upright(0.5f, 62f);
        float a = Rad(62f);
        Vector3 chest = k.P[Skel.Chest];
        k.Mouth = 0.55f * chew;
        k.Fish = fish;
        Vector3 paws = chest + V(0.085f, -0.075f);
        k.P[Skel.FishA] = paws + V(0.075f * fish, 0.035f);
        k.P[Skel.FishB] = paws + V(-0.115f * fish, -0.025f);
        for (int i = 0; i < 2; i++) Leg(k, i, chest + V(0.03f, -0.03f, LegZ(i)), paws + V(0f, 0f, LegZ(i) * 0.8f));
        HeadAt(k, chest + V(MathF.Cos(a + 0.15f), MathF.Sin(a + 0.15f)) * 0.10f, 0f, 38f + 6f * chew);
        return k;
    }

    /// <summary>
    /// Rolling on its back. "rho" is how far the body has rolled about its own long axis (0 upright, 180 belly up);
    /// "by" is how high the body's middle rides; "paddle" waves the legs in the air.
    /// </summary>
    public static Skel Roll(float rho, float by, float paddle, float bend = 0f)
    {
        var k = new Skel();
        k.P[Skel.Hip] = V(-0.17f, by, -0.04f * bend);
        k.P[Skel.Mid] = V(0f, by + 0.012f, 0f);
        k.P[Skel.Chest] = V(0.17f, by, 0.04f * bend);
        HeadAt(k, V(0.265f, by + 0.022f, 0.05f * bend), 0f, 4f + 8f * bend);
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2;
            Vector3 root = k.P[hind ? Skel.Hip : Skel.Chest];
            float sgn = (i % 2 == 0) ? 1f : -1f, phase = hind ? 1.6f : 0f;
            Vector3 top = new(root.X, root.Y - 0.02f, root.Z + LegZ(i));
            float pad = paddle * MathF.Sin(phase + (sgn > 0 ? 0 : 1.2f));
            // Legs hang "down" from the body in its own frame (x forward as at rest; the roll then turns them up).
            Leg(k, i, top, new Vector3(root.X + (hind ? 0.03f : 0.02f) + 0.07f * pad, by - 0.125f + 0.0f, root.Z + LegZ(i) * (1.05f + 0.5f * MathF.Abs(pad))));
        }
        Tail(k, k.P[Skel.Hip] + V(-0.05f, -0.025f), -16f, 3f, 0f, 0f, false, 0.03f * bend);
        RollAbout(k, rho, by);
        return k;
    }

    /// <summary>Turns every point of a pose about the body's long axis (the line y = axisY, z = 0 along x), and lets the ground stop the part under it.</summary>
    private static void RollAbout(Skel k, float rho, float axisY)
    {
        if (rho == 0f) return;
        float c = MathF.Cos(Rad(rho)), s = MathF.Sin(Rad(rho));
        for (int i = 0; i < Skel.Count; i++)
        {
            Vector3 p = k.P[i];
            float y = p.Y - axisY, z = p.Z;
            k.P[i] = new Vector3(p.X, axisY + y * c - z * s, y * s + z * c);
        }
    }

    /// <summary>Head and neck up out of a hole in the ice, the body hanging in the water below the clip. "k" 0 (just the nose) to 1 (head and shoulders), "look" turns the head, "fish" holds a fish across the mouth.</summary>
    public static Skel Surf(float rise, float fish, float lookPitch = 2f)
    {
        // Only the top of a flat, broad head shows: ears, eyes, nose and whiskers above the ripple line; the neck and the whole body
        // hang below it (hidden by the clip). "rise" lifts the head from just the nose to the top of the skull.
        var k = new Skel();
        float r = Sm(rise);
        float hy = -0.05f + 0.095f * r;                // the head's middle, against a water line at 0 (the clip shows down to -0.05)
        k.P[Skel.Chest] = V(-0.16f, hy - 0.24f);
        k.P[Skel.Mid] = V(-0.34f, hy - 0.27f);
        k.P[Skel.Hip] = V(-0.52f, hy - 0.30f);
        HeadAt(k, V(-0.09f, hy - 0.03f), 0f, lookPitch);
        for (int i = 0; i < 4; i++) Leg(k, i, k.P[Skel.Chest] + V(i >= 2 ? -0.3f : 0f, 0, LegZ(i)), k.P[Skel.Chest] + V(i >= 2 ? -0.35f : 0.05f, -0.12f, LegZ(i)));
        Tail(k, k.P[Skel.Hip] + V(-0.05f, -0.02f), -30f, 8f, 1f, 0.03f, false);
        if (fish > 0f) Hold(k, fish);
        k.Clip = 1f; k.Ring = true;
        return k;
    }

    /// <summary>A small silver fish held CROSSWISE in the jaws (set after the head is placed): its head end shows on one side of the muzzle and its tail on the other, a little slanted so it still shows from the side.</summary>
    private static void Hold(Skel k, float size)
    {
        k.Fish = size;
        Vector3 m = k.P[Skel.Muzzle], h = k.P[Skel.Head];
        Vector3 f = Vector3.Normalize(m - h);
        Vector3 side = Vector3.Cross(f, Vector3.UnitY); if (side.LengthSquared() < 1e-4f) side = Vector3.UnitZ; side = Vector3.Normalize(side);
        Vector3 c = m - f * 0.004f + V(0f, -0.012f);
        Vector3 axis = Vector3.Normalize(side * 0.62f + f * 0.62f);
        float half = 0.075f * size;
        k.P[Skel.FishA] = c + axis * half;
        k.P[Skel.FishB] = c - axis * half;
        k.Mouth = 0.22f;
    }
    /// <summary>
    /// Hauling out onto the ice. The origin is where the otter ends up standing (the rest pose). At 0 only the head and
    /// shoulders are up in the water 0.9 m back; the forepaws find the rim, and the body is pulled out over them, the
    /// hips last, until it stands.
    /// </summary>
    public static Skel Haul(float t, float fish)
    {
        var k = new Skel();
        float e = Sm(t);
        Vector3 chest = Vector3.Lerp(V(-0.88f, 0.12f), V(0.17f, 0.15f), e);
        Vector3 hip = Vector3.Lerp(V(-1.2f, -0.17f), V(-0.17f, 0.15f), MathF.Pow(e, 0.8f));
        k.P[Skel.Chest] = chest; k.P[Skel.Hip] = hip;
        k.P[Skel.Mid] = (chest + hip) / 2 + V(0f, 0.02f + 0.04f * MathF.Sin(MathF.PI * e));
        HeadAt(k, chest + V(0.1f, 0.03f + 0.09f * (1f - e)), 0f, 18f * (1f - e) + 4f * e);
        float plant = Sm((t - 0.1f) / 0.9f);
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2;
            Vector3 root = hind ? hip : chest;
            Vector3 top = new(root.X, root.Y - 0.02f, LegZ(i));
            Vector3 paw;
            if (!hind)
            {
                // The forepaws grip the rim (at x = -0.55) while the chest passes over, then step on to their rest places.
                float x = Math.Clamp(-0.55f, root.X - 0.12f, root.X + 0.25f);
                float rel = Sm((t - 0.55f) / 0.45f);
                paw = V(x + (root.X + 0.02f - x) * rel, 0.024f + (t < 0.1f ? -0.1f * (1f - t * 10f) + 0.1f * 0 : 0f), LegZ(i) * 1.05f);
            }
            else paw = Vector3.Lerp(V(hip.X - 0.1f, hip.Y - 0.12f, LegZ(i)), V(hip.X + 0.03f, 0.024f, LegZ(i) * 1.05f), Sm((t - 0.55f) / 0.45f));
            Leg(k, i, top, paw);
        }
        Tail(k, hip + V(-0.05f, -0.025f), -34f * (1f - e) - 16f * e, 4f, 0.5f + 2f * t, 0.03f * (1f - e), t > 0.97f);
        if (fish > 0f) Hold(k, fish);
        k.Clip = 1f;
        return k;
    }

    /// <summary>
    /// The dive: from standing at rest 0.93 m from the hole's edge, a short leap and a plunge, head first, the body
    /// following the head along ONE path (an arch, then down), so it bends like a real otter and never kinks. The
    /// water line is y = 0 at the hole.
    /// </summary>
    public static Skel Dive(float t)
    {
        float e = t * (2f - 0.35f * t);
        float uh = 0.365f + 1.6f * e;                       // where the head is along the path
        // The path: flat ground until x = 0.30, then an arch up and over, plunging past the hole's edge (x about 0.93) into the water.
        static Vector3 Path(float u)
        {
            if (u <= 0.30f) return V(u, 0.16f);
            float tau = (u - 0.30f) / 0.9f;
            return V(u, 0.16f + 0.9f * tau * (1f - 2.2f * tau));
        }
        // Walk back along the path by arc length "d" from the head's place (numerically, in small steps: it is only the build).
        Vector3 Back(float d)
        {
            float u = uh; float acc = 0f; Vector3 prev = Path(u);
            while (acc < d)
            {
                u -= 0.004f; Vector3 cur = Path(u);
                acc += Vector3.Distance(cur, prev); prev = cur;
                if (u < -2f) break;
            }
            return prev;
        }
        var k = new Skel();
        k.P[Skel.Head] = Back(0f);
        k.P[Skel.Muzzle] = Back(0f) + (Path(uh + 0.01f) - Path(uh)) * 7f;
        k.P[Skel.Neck] = Back(0.09f);
        k.P[Skel.Chest] = Back(0.19f);
        k.P[Skel.Mid] = Back(0.36f);
        k.P[Skel.Hip] = Back(0.53f);
        for (int i = 0; i < Skel.TailBeads; i++) k.P[Skel.Tail0 + i] = Back(0.58f + 0.055f * (i + 1));
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2;
            Vector3 root = k.P[hind ? Skel.Hip : Skel.Chest];
            Vector3 back = Vector3.Normalize(root - k.P[hind ? Skel.Mid : Skel.Neck]);
            Vector3 top = new(root.X, root.Y - 0.02f, LegZ(i));
            Leg(k, i, top, new Vector3(root.X + back.X * 0.16f, root.Y + back.Y * 0.16f - 0.01f, LegZ(i) * 1.2f));
        }
        k.Mouth = 0.1f; k.Clip = 1f;
        // The first part of the dive is a blend from the rest pose, so the leap begins from the crouch with no jump.
        { Skel r = Skel.Lerp(Rest(), k, Sm(t / 0.2f)); r.Clip = 1f; return r; }
    }

    /// <summary>
    /// One otter of a wrestling pair: the body bent into a C round a centre, the whole C turning by "theta" degrees
    /// (so a pair of them, set half a turn apart, rolls like a ball), the legs out and scrabbling ("wig" swings them).
    /// </summary>
    public static Skel Curl(float theta, float R, float z, float wig)
    {
        var k = new Skel();
        const float cy = 0.285f;
        Vector3 At(float aDeg, float r, float zz = 0f) { float a = Rad(aDeg); return new Vector3(MathF.Cos(a) * r, cy + MathF.Sin(a) * r, zz); }
        k.P[Skel.Hip] = At(theta, R, z);
        k.P[Skel.Mid] = At(theta + 50f, R, z);
        k.P[Skel.Chest] = At(theta + 100f, R, z);
        k.P[Skel.Neck] = At(theta + 128f, R, z);
        k.P[Skel.Head] = At(theta + 150f, R + 0.012f, z);
        k.P[Skel.Muzzle] = At(theta + 166f, R + 0.02f, z);
        for (int i = 0; i < Skel.TailBeads; i++) k.P[Skel.Tail0 + i] = At(theta - 22f - 17f * i, R + 0.045f + 0.012f * i, z);
        for (int i = 0; i < 4; i++)
        {
            bool hind = i >= 2;
            float aSp = theta + (hind ? 8f : 96f);
            float sw = wig * (i % 2 == 0 ? 1f : -1f) * 22f;
            Vector3 top = At(aSp, R * 0.8f, z + LegZ(i) * 0.5f);
            Vector3 paw = At(aSp + (hind ? -20f : 20f) + sw, R + 0.16f, z + LegZ(i) * 0.6f);
            Leg(k, i, top, paw);
        }
        k.Mouth = 0.5f; k.Clip = 0f;
        return k;
    }

    // ======================================================================== the painter

    private struct Part
    {
        public int Kind;                // 0 capsule, 1 ball, 2 whisker line, 3 fish tail fin
        public Vector3 A, B;
        public float Ra, Rb;
        public Color C;
        public float Key;               // the draw order: nearer the viewer is bigger
    }

    /// <summary>
    /// Paints a pose as seen with the body turned "yawDeg" about its upright axis, "p" pixels to the metre, on a sheet
    /// w by h whose point (ox, oy) is the body frame's origin (the ground under the middle of the body).
    /// </summary>
    public static Sprite Page(Skel k, float yawDeg, float p, int w, int h, float ox, float oy) => Page([k], yawDeg, p, w, h, ox, oy);

    /// <summary>The same, for several animals on one sheet (two otters tumbling in a ball), drawn together far to near.</summary>
    public static Sprite Page(Skel[] ks, float yawDeg, float p, int w, int h, float ox, float oy) =>
        Sprite.Paint(w, h, g =>
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cyaw = MathF.Cos(Rad(yawDeg)), syaw = MathF.Sin(Rad(yawDeg));
            // The turn: world x (right) and world z (toward the viewer) of any body-frame point.
            Vector3 W(Vector3 b) => new(b.X * cyaw - b.Z * syaw, b.Y, b.X * syaw + b.Z * cyaw);
            PointF S(Vector3 wv) => new(ox + p * wv.X, oy - p * wv.Y * MathF.Cos(Elev) + p * wv.Z * MathF.Sin(Elev));

            var parts = new List<Part>(40);
            void Cap(Vector3 a, Vector3 b, float ra, float rb, Color c, float bias = 0f)
            {
                Vector3 wa = W(a), wb = W(b);
                parts.Add(new Part { Kind = 0, A = wa, B = wb, Ra = ra, Rb = rb, C = c, Key = (wa.Z + wb.Z) / 2 + bias });
            }
            void Ball(Vector3 a, float r, Color c, float bias = 0f)
            {
                Vector3 wa = W(a);
                parts.Add(new Part { Kind = 1, A = wa, Ra = r, C = c, Key = wa.Z + bias });
            }

            foreach (Skel k in ks)
            {
            Vector3[] P = k.P;
            // Tail (the first bead rides inside the hip).
            for (int i = 0; i < Skel.TailBeads - 1; i++)
                Cap(i == 0 ? P[Skel.Hip] + (P[Skel.Tail0] - P[Skel.Hip]) * 0.3f : P[Skel.Tail0 + i - 1], P[Skel.Tail0 + i], i == 0 ? 0.062f : TailR(i - 1), TailR(i), Fur, -0.002f);
            Cap(P[Skel.Tail0 + 5], P[Skel.Tail0 + 6], TailR(5), TailR(6), Fur, -0.002f);

            // Legs: the thigh, the shin and the paw. The far side's are a shade darker.
            for (int i = 0; i < 4; i++)
            {
                Color lc = i % 2 == 0 ? Fur : FurFar;
                Vector3 top = P[Skel.Top0 + i], knee = P[Skel.Knee0 + i], paw = P[Skel.Paw0 + i];
                bool hind = i >= 2;
                Cap(top, knee, hind ? 0.040f : 0.034f, 0.028f, lc, 0.001f);
                Cap(knee, paw, 0.027f, 0.021f, lc, 0.001f);
                Ball(paw + V(0.012f, 0f), hind ? 0.030f : 0.026f, i % 2 == 0 ? Pad : Color.FromArgb(36, 25, 22), 0.002f);
            }

            // The body: one thick tube, hip to chest, neck to head. A lighter cap along the top reads as a rounded back.
            Cap(P[Skel.Hip], P[Skel.Mid], 0.076f, 0.082f, Fur);
            Cap(P[Skel.Mid], P[Skel.Chest], 0.082f, 0.077f, Fur);
            Cap(P[Skel.Chest], P[Skel.Neck], 0.077f, 0.066f, Fur);
            Cap(P[Skel.Neck], P[Skel.Head], 0.066f, 0.062f, Fur);
            Ball(P[Skel.Head], 0.064f, Fur, 0.0005f);

            // The pale throat and muzzle, the ears, the eyes, the nose.
            Vector3 hd = P[Skel.Head], mz = P[Skel.Muzzle];
            Vector3 f = Vector3.Normalize(mz - hd);
            Vector3 side = Vector3.Cross(f, Vector3.UnitY); if (side.LengthSquared() < 1e-4f) side = Vector3.UnitZ; side = Vector3.Normalize(side);
            Vector3 up = Vector3.Cross(side, f);
            Ball(hd - up * 0.032f + f * 0.012f, 0.05f, Throat, 0.0006f);
            Ball(mz, 0.043f, Muzz, 0.0008f);
            Ball(mz + f * 0.032f + up * 0.006f, 0.0155f, Ink, 0.0012f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Color ec = sgn > 0 ? Fur : FurFar;
                float flick = sgn > 0 ? k.Ear : 0f;   // the near ear folds back and down in a flick
                Ball(hd - f * (0.028f + 0.022f * flick) + up * (0.052f - 0.016f * flick) + side * (0.044f * sgn), 0.021f, ec, 0.003f * sgn);
                Vector3 eye = hd + f * 0.028f + up * 0.02f + side * (0.050f * sgn);
                Ball(eye, k.Blink > 0.5f ? 0.005f : 0.0115f, Ink, 0.004f * sgn);
                if (k.Blink <= 0.5f) Ball(eye + f * 0.004f + up * 0.005f + side * (0.003f * sgn), 0.0036f, Color.White, 0.0042f * sgn);
            }
            // Whiskers: three fine pale lines each side of the muzzle (drawn as lines, after the shapes).
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int j = 0; j < 3; j++)
                {
                    Vector3 root = mz + f * 0.012f + up * (0.006f - 0.012f * j) + side * (0.022f * sgn);
                    Vector3 tip = root + side * (0.075f * sgn) + f * (0.012f - 0.016f * j + 0.03f * k.Whisk) + up * (0.012f - 0.02f * j + 0.03f * k.Whisk);
                    parts.Add(new Part { Kind = 2, A = W(root), B = W(tip), C = Color.FromArgb(205, 232, 226, 214), Key = W(root).Z + 0.0013f * sgn + 0.01f });
                }

            if (k.Fish > 0f)
            {
                Vector3 a = P[Skel.FishA], b = P[Skel.FishB];
                Vector3 axis = Vector3.Normalize(b - a);
                Cap(a, b, 0.030f * (0.5f + 0.5f * k.Fish), 0.014f * (0.5f + 0.5f * k.Fish), FishSide, 0.02f);
                Cap(a + V(0, 0.01f), b + V(0, 0.01f), 0.016f * (0.5f + 0.5f * k.Fish), 0.008f, FishBack, 0.0205f);
                parts.Add(new Part { Kind = 3, A = W(b), B = W(b + axis * 0.05f * k.Fish + V(0, 0.022f * k.Fish)), Ra = 0.026f * k.Fish, C = FishBack, Key = W(b).Z + 0.021f });
            }

            // Mouth: a dark gap under the muzzle when open.
            if (k.Mouth > 0.05f) Ball(mz - up * (0.02f + 0.014f * k.Mouth) - f * 0.006f, 0.014f + 0.016f * k.Mouth, Color.FromArgb(70, 22, 26), 0.0009f);

            }
            parts.Sort((a, b) => a.Key.CompareTo(b.Key));
            float clipY = ks[0].Clip > 0.5f ? oy + 0.05f * p : h;
            g.SetClip(new RectangleF(0, 0, w, Math.Min(h, clipY)));
            foreach (Part pt in parts)
            {
                PointF a = S(pt.A);
                using var br = new SolidBrush(pt.C);
                switch (pt.Kind)
                {
                    case 1:
                        { float r = pt.Ra * p; g.FillEllipse(br, a.X - r, a.Y - r, r * 2, r * 2); break; }
                    case 0:
                        {
                            PointF b = S(pt.B);
                            float ra = pt.Ra * p, rb = pt.Rb * p;
                            g.FillEllipse(br, a.X - ra, a.Y - ra, ra * 2, ra * 2);
                            g.FillEllipse(br, b.X - rb, b.Y - rb, rb * 2, rb * 2);
                            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy);
                            if (l > 0.2f)
                            {
                                float nx = -dy / l, ny = dx / l;
                                g.FillPolygon(br, [new PointF(a.X + nx * ra, a.Y + ny * ra), new PointF(b.X + nx * rb, b.Y + ny * rb), new PointF(b.X - nx * rb, b.Y - ny * rb), new PointF(a.X - nx * ra, a.Y - ny * ra)]);
                            }
                            break;
                        }
                    case 2:
                        { PointF b = S(pt.B); using var pen = new Pen(pt.C, Math.Max(1f, p * 0.0065f)); g.DrawLine(pen, a, b); break; }
                    case 3:
                        {
                            // The fish's tail fin: a small triangle fanning from the tail end.
                            PointF b = S(pt.B); float r = pt.Ra * p;
                            g.FillPolygon(br, [a, new PointF(b.X, b.Y - r), new PointF(b.X + r * 0.2f, b.Y + r)]);
                            break;
                        }
                }
            }

            // A head in the water wears the pale ring of its own ripple round the water line.
            if (ks[0].Ring)
            {
                g.ResetClip();
                using var ring1 = new Pen(Color.FromArgb(150, 214, 232, 252), Math.Max(1f, p * 0.012f));
                using var ring2 = new Pen(Color.FromArgb(80, 214, 232, 252), Math.Max(1f, p * 0.008f));
                g.DrawEllipse(ring1, ox - 0.17f * p, oy + 0.045f * p - 0.032f * p, 0.34f * p, 0.064f * p);
                g.DrawEllipse(ring2, ox - 0.27f * p, oy + 0.045f * p - 0.05f * p, 0.54f * p, 0.10f * p);
            }

            // Moonlight on the wet coat: a few pale streaks along the top of the back (the moon is up and to the left).
            foreach (Skel k in ks)
            {
            Vector3[] P = k.P;
            Vector3 hd = P[Skel.Head], mz = P[Skel.Muzzle];
            Vector3 f = Vector3.Normalize(mz - hd);
            Vector3 side = Vector3.Cross(f, Vector3.UnitY); if (side.LengthSquared() < 1e-4f) side = Vector3.UnitZ; side = Vector3.Normalize(side);
            Vector3 up = Vector3.Cross(side, f);
            using (var pen = new Pen(Color.FromArgb(120, Sheen), Math.Max(1f, p * 0.012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                Vector3 off = V(0f, 0.062f);
                PointF s0 = S(W(P[Skel.Hip] + V(0.03f, 0f) + off)), s1 = S(W(P[Skel.Mid] + off)), s2 = S(W(P[Skel.Chest] + V(-0.03f, 0f) + off));
                g.DrawCurve(pen, [s0, s1, s2], 0.5f);
                using var pen2 = new Pen(Color.FromArgb(90, Sheen), Math.Max(1f, p * 0.008f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                PointF h0 = S(W(P[Skel.Neck] + V(0f, 0.05f))), h1 = S(W(P[Skel.Head] + up * 0.05f - f * 0.01f));
                g.DrawLine(pen2, h0, h1);
            }
            }
        });

    /// <summary>A soft blue-grey shadow on the ice or snow under the otter: an ellipse as long as the body seen at this yaw, flat to the ground.</summary>
    public static Sprite Shadow(float yawDeg, float p, float lengthM = 0.95f, float alpha = 66f)
    {
        float cy = MathF.Abs(MathF.Cos(Rad(yawDeg))), sy = MathF.Abs(MathF.Sin(Rad(yawDeg)));
        float rx = p * (0.5f * lengthM * cy + 0.1f * sy + 0.06f), ry = p * (0.5f * lengthM * sy + 0.08f * cy + 0.03f) * MathF.Sin(Elev) * 1.2f + 1.5f;
        int w = (int)(rx * 2 + 6), h = (int)(ry * 2 + 6);
        return Sprite.Paint(w, h, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(2, 2, w - 4, h - 4);
            using var br = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb((int)alpha, 30, 44, 100),
                SurroundColors = [Color.FromArgb(0, 30, 44, 100)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(br, path);
        });
    }
}
