using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How the possum is drawn, and the poses it can take (Possum.cs is the animal that uses them).
///
/// FEYNMAN VERSION: a pose is a handful of numbers (how far through its waddle, where the head points, how open the
/// mouth is, how the tail curls, whether it is sitting up or curled over), and one painter turns any pose into a
/// picture. The pose BUILDERS below know the animal. They were written from cute flat-vector Virginia opossums, not
/// from memory: a long LOW grey body (about 2.2 units from rump to nose, 1.0 tall counting the short legs), the back a
/// darker grey and the belly paler; a POINTED WHITE FACE well over half the body's length, tapering to a PINK nose; two
/// round BLACK ears set well back on the skull; a small dark eye with a highlight; short BLACK legs ending in PINK hands
/// and feet; and a bare PINK TAIL as long as the body, thin, carried in a lazy curve.
///
/// HOW IT MOVES (looked up first). It WADDLES: a slow flat-footed walk in diagonal pairs (the near fore with the far
/// hind), the feet barely lifting, the body level with only a small bob and the head swinging a little. It has none of
/// the squirrel's bound. It HANGS by the tail, wrapped once over a branch, head DOWN, the body a straight vertical
/// line, the legs dangling loose with the pink hands open, the head turning to look about. To climb back up it CURLS
/// (the head swings up the belly side) and reaches for its own tail. When it PLAYS DEAD (its famous trick) it lies on
/// its side, stiff, legs sticking up, mouth open, tongue out, eyes shut (an X). Then it gets up, shakes and goes on.
///
/// One body frame for everything: units of "p" pixels (a body length is 2.2 p), x forward, y DOWN (the ordinary
/// drawing way), the origin on the ground under the middle of the body. The caller puts the body's middle
/// (0, -0.66) wherever the sheet wants it and may turn the whole frame (a climb is the walk, turned a quarter).
/// </summary>
internal static class PossumArt
{
    // ---- the colours ----
    public static readonly Color Fur = Color.FromArgb(150, 144, 140);
    public static readonly Color Back = Color.FromArgb(104, 100, 100);
    public static readonly Color Belly = Color.FromArgb(214, 210, 204);
    public static readonly Color Face = Color.FromArgb(246, 242, 236);
    public static readonly Color FaceShade = Color.FromArgb(216, 210, 204);
    public static readonly Color Ear = Color.FromArgb(40, 36, 38);
    public static readonly Color Leg = Color.FromArgb(58, 54, 56);
    public static readonly Color LegFar = Color.FromArgb(38, 35, 37);
    public static readonly Color Pink = Color.FromArgb(240, 156, 164);
    public static readonly Color TailPink = Color.FromArgb(230, 154, 156);
    public static readonly Color TailEdge = Color.FromArgb(196, 124, 128);
    public static readonly Color Ink = Color.FromArgb(30, 26, 28);

    /// <summary>The body's middle, where the caller pins a page: (0, -0.66) in the body frame.</summary>
    public const float PivotY = -0.66f;

    /// <summary>Every number that makes a pose. The defaults are a standing possum looking ahead with its tail trailing.</summary>
    public struct Pose
    {
        public float Phase;                 // 0..1 through one waddle (when Gait)
        public bool Gait;                   // the feet follow the waddle; false: Feet says where each one is
        public float Bob;                   // the body's rise and fall, units
        public float HeadAng;               // degrees, + nose down, from the body's own line
        public float Turn;                  // the head's view: 0 profile, 0.5 three-quarter, 1 face on
        public float Yaw;                   // the whole body turning toward us: 0 profile, 1 end-on (the turn pages)
        public float Mouth, Tongue;         // 0..1
        public int Eye;                     // 0 open, 1 shut, 2 an X, 3 one eye open a slit (the peek)
        public float SitAng;                // degrees the upper body is raised about the haunches (sitting up)
        public float Bend;                  // degrees the chest and head curl toward the belly about the hip (the pull-up)
        public float Hands;                 // 0 fists, 1 open
        public int Nut;                     // 1: an acorn held in both hands at the mouth
        public float Wipe;                  // 0 none, 0..1 one paw sweeping over the face
        public float TailBase, TailCurl, TailS, TailWave, TailPhase;   // degrees along the tail, 180 straight back
        public PointF[]? Feet;              // four foot spots (far fore, far hind, near fore, near hind) when not Gait
        public float Pitch;                 // degrees the FRONT half (chest, shoulders, head, forelegs) turns about the hip, - is nose up; the hind half stays (the mount and dismount)
        public bool NoTail;                 // the tail is not painted into the page: the scene draws it live, as a chain (Possum.cs)
        public bool FlatFeet;               // paint the pads flat as on a gait page even though Feet places them (the mount's pages)
        public bool Dead;                   // lying on its side, stiff
        public float Quiver;                // 0..1: the shake (the whole figure's lean, painted as a sideways lean)
    }

    /// <summary>Standing at rest: exactly the waddle's first page (the rest page every walk ends on), so a stop never pops.</summary>
    public static Pose Stand() => Walk(0f);

    /// <summary>The waddle: page "ph" of one cycle (0..1). The body rides a small bob twice a cycle and the head swings a little.</summary>
    public static Pose Walk(float ph) => new()
    {
        Phase = ph, Gait = true,
        Bob = 0.022f * MathF.Sin(MathF.Tau * ph * 2f),
        HeadAng = 9f + 5f * MathF.Sin(MathF.Tau * ph),
        TailBase = 176f, TailCurl = 46f, TailS = -14f, TailWave = 5f, TailPhase = MathF.Tau * ph,
    };

    /// <summary>Standing and sniffing: the nose down to the ground and back up, "k" 0 level to 1 nose to the earth.</summary>
    public static Pose Sniff(float k) { var q = Stand(); q.HeadAng += 34f * k; q.Bob = 0.03f * k; return q; }

    /// <summary>Standing with the head up, looking about.</summary>
    public static Pose LookUp() { var q = Stand(); q.HeadAng = -14f; return q; }

    /// <summary>
    /// Sitting up on the haunches, "k" 0 (standing) to 1 (up): the body rises about the hips, the hind feet come flat
    /// out in front, the forepaws leave the ground for the chest. "chew" 0..1 opens the mouth; with a nut it is held in
    /// both hands at the mouth (the painter finds the mouth). "wipe" sweeps one paw over the face.
    /// </summary>
    public static Pose Sit(float k, float chew, bool nut, float wipe = 0f)
    {
        var q = Stand();
        q.SitAng = 58f * k; q.Nut = nut && k > 0.99f ? 1 : 0; q.Mouth = 0.5f * chew; q.Wipe = wipe;
        q.HeadAng = 9f + 11f * k;
        q.TailBase = 176f + 6f * k; q.TailCurl = 46f - 26f * k; q.TailS = -14f + 22f * k; q.TailWave = 0f;
        return q;
    }

    /// <summary>The turn pages, "k" 0 profile to 1 end-on.</summary>
    public static Pose TurnPose(float k)
    {
        var q = Stand();
        q.Yaw = k; q.Turn = k;
        return q;
    }

    /// <summary>The shake after playing dead: standing, the body leaning to one side ("s" -1 to 1), the head down a little.</summary>
    public static Pose Shake(float s) { var q = Stand(); q.Quiver = s; q.HeadAng = 18f; q.Mouth = 0.15f; return q; }

    /// <summary>The faint: "k" 0 standing to 1 collapsing: the body sinks, the legs splay, the eyes go to Xs, the mouth falls open. The next page is the dead pose.</summary>
    public static Pose Faint(float k)
    {
        var q = Stand();
        q.Gait = false; q.Bob = 0.22f * k; q.Mouth = 0.6f * k; q.HeadAng = 9f + 16f * k;
        q.Feet = [new PointF(0.58f, 0f), new PointF(-0.78f, 0f), new PointF(0.46f, 0f), new PointF(-0.62f, 0f)];
        q.Eye = k > 0.5f ? 2 : 1;
        q.TailWave = 0f;
        return q;
    }

    /// <summary>Lying on its side, stiff: mouth open, tongue out, an X for an eye ("peek": one eye open a slit, once, as a possum will).</summary>
    public static Pose Dead(float tongueSway, bool peek)
    {
        var q = Stand();
        q.HeadAng = 14f;
        q.Dead = true; q.Mouth = 0.85f; q.Tongue = 0.8f + 0.1f * tongueSway; q.Eye = peek ? 3 : 2;
        q.TailBase = 178f; q.TailCurl = 24f; q.TailS = 6f; q.TailWave = 4f; q.TailPhase = tongueSway;
        return q;
    }

    /// <summary>
    /// Hanging by the tail, head down. "look": 0 head straight down in profile, 1 three-quarter, 2 face on, 3 the head
    /// swung out to the side to look at the patch. "hands" open or shut. "bend" the pull-up's curl (0 hanging free).
    /// </summary>
    public static Pose Hang(int look, float hands, float bend = 0f)
    {
        var q = new Pose { Gait = false, Hands = hands, Bend = bend, TailBase = 180f, TailCurl = 0f, TailS = 2f };
        q.Turn = look == 1 ? 0.5f : look == 2 ? 1f : 0f;
        q.HeadAng = look == 3 ? -34f : 0f;                   // + is nose toward the back; the hang frame turns it to "out to the side"
        // The legs dangle loose: the forelegs beside the chest, hanging forward (toward the ground), the hind legs likewise, a little splayed.
        q.Feet = [new PointF(0.88f, -0.30f), new PointF(-0.06f, -0.20f), new PointF(0.94f, -0.58f), new PointF(-0.02f, -0.60f)];
        if (bend > 40f)                                      // reaching for its own tail: the hands go to the tail's root
        {
            q.Feet[0] = new PointF(-1.05f, -1.00f); q.Feet[2] = new PointF(-1.20f, -0.86f);
            q.HeadAng = 10f;
        }
        return q;
    }

    // ======================================================================== painting

    /// <summary>Paints a pose into a sheet "w" by "h" pixels, "p" pixels to the unit, the body's middle at (cx, cy), the frame turned "rot" degrees.</summary>
    public static Sprite Page(Pose q, float p, int w, int h, float cx, float cy, float rot = 0f) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(cx, cy);
            if (rot != 0f) g.RotateTransform(rot);
            g.ScaleTransform(p, p);
            g.TranslateTransform(0f, -PivotY);
            Paint(g, q);
        });

    /// <summary>The hanging pages: the whole figure swung "sway" degrees about the tail's wrap point (the sheet's (cx, cy)).</summary>
    public static Sprite HangPage(Pose q, float p, int w, int h, float cx, float cy, float sway) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(cx, cy);
            g.RotateTransform(sway);
            g.TranslateTransform(0f, 2.3f * p);              // the body's middle hangs 2.3 units below the wrap (1.3 of tail shows)
            g.RotateTransform(90f);                          // head down: the frame's +x points at the ground, the belly to the left
            g.ScaleTransform(p, p);
            g.TranslateTransform(0f, -PivotY);
            Paint(g, q);
        });

    private static PointF P(float x, float y) => new(x, y);

    private static void Fill(Graphics g, PointF[] pts, Color c, float tension = 0.5f)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, tension);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    private static void Disc(Graphics g, float cx, float cy, float rx, float ry, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - rx, cy - ry, rx * 2f, ry * 2f);
    }

    private static void Bone(Graphics g, PointF a, PointF b, float wa, float wb, Color c)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
        using var br = new SolidBrush(c);
        g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
        g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
        g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
    }

    private static PointF Rot(PointF v, float deg)
    {
        float r = deg * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
        return P(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private static float Sm(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    private static PointF Lerp(PointF a, PointF b, float t) => P(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>
    /// Where a point of the FRONT part of the body goes when the animal sits up, curls or PITCHES. A pitch is not a hinge: the
    /// turn about the hip grows smoothly from nothing at the rump to all of it at the chest (so the spine is one curve through the
    /// hip), and the back swells a little over the bend (a hump that grows with the pitch) the way a real back rounds when the
    /// front end goes up. Sitting and curling turn everything by the same angle, as they always did.
    /// </summary>
    private static PointF Upper(in Pose q, float x, float y)
    {
        PointF hip = P(-0.58f, -0.44f);
        float a = q.Bend - q.SitAng, hump = 0f;
        if (q.Pitch != 0f)
        {
            float w = Sm((x + 0.75f) / 0.85f);                                   // 0 at the rump, 1 from the chest forward
            a += q.Pitch * w;
            hump = -0.16f * MathF.Abs(q.Pitch) / 90f * MathF.Sin(MathF.PI * w) * Math.Clamp((hip.Y - y) / 0.4f, 0f, 1f);
        }
        var v = Rot(P(x - hip.X, y - hip.Y), a);
        return P(hip.X + v.X, hip.Y + v.Y + hump);
    }

    // ---- the live tail: where its root is, and the shape it would rest in ----

    /// <summary>The number of points in the live tail's chain (so 11 pieces). Possum.cs steps the chain; this is its rest shape.</summary>
    public const int TailPoints = 12;

    /// <summary>Where the tail grows out of the body, in the body frame, for this pose (turned with the haunches, foreshortened with the body's yaw).</summary>
    public static PointF TailRoot(in Pose q)
    {
        PointF r = P(-1.0f, -0.74f + q.Bob);
        if (q.SitAng > 0f || q.Bend != 0f || q.Pitch != 0f) r = Upper(q, r.X, r.Y);
        if (q.Yaw > 0.01f) r = P(r.X * (1f - 0.58f * q.Yaw), r.Y);
        return r;
    }

    /// <summary>The painted tail's own curve (the pose's TailBase, TailCurl, TailS and TailWave) as "TailPoints" points from the root: the shape the live chain is gently pulled back toward, so it keeps the lazy curve and the wag but lags, swings and droops on its own.</summary>
    public static PointF[] TailRest(in Pose q, float length = 2.0f)
    {
        int n = TailPoints - 1;
        var s = new PointF[TailPoints];
        s[0] = TailRoot(q);
        float seg = length / n, xs = q.Yaw > 0.01f ? 1f - 0.58f * q.Yaw : 1f;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float ang = q.TailBase + q.TailCurl * t + q.TailS * MathF.Sin(MathF.PI * t) + q.TailWave * MathF.Sin(q.TailPhase - i * 0.5f * 16f / n);
            float r = ang * MathF.PI / 180f;
            s[i + 1] = P(s[i].X + seg * MathF.Cos(r) * xs, s[i].Y + seg * MathF.Sin(r));
        }
        return s;
    }

    /// <summary>A body-frame point as pixels from the sheet's pivot, for a page painted by Page() with the frame turned "rot" (not mirrored).</summary>
    public static PointF PageOffset(PointF b, float p, float rot) { var v = Rot(P(b.X, b.Y - PivotY), rot); return P(v.X * p, v.Y * p); }

    /// <summary>The same for a hanging page painted by HangPage() (the wrap point is the pivot; swung "sway" degrees).</summary>
    public static PointF HangOffset(PointF b, float p, float sway)
    {
        var v = Rot(P(b.X, b.Y - PivotY), 90f);
        return Rot(P(v.X * p, v.Y * p + 2.3f * p), sway);
    }

    // ---- the mount and the dismount: a bend, not a rotation ----

    private static PointF ToWorld(PointF b, PointF mid, float rot) { var v = Rot(P(b.X, b.Y - PivotY), rot); return P(mid.X + v.X, mid.Y + v.Y); }
    private static PointF ToBody(PointF w, PointF mid, float rot) { var v = Rot(P(w.X - mid.X, w.Y - mid.Y), -rot); return P(v.X, v.Y + PivotY); }
    private static PointF Reach(PointF joint, PointF foot, float r = 0.66f)
    {
        float dx = foot.X - joint.X, dy = foot.Y - joint.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        return l <= r ? foot : P(joint.X + dx * r / l, joint.Y + dy * r / l);          // a leg cannot stretch: the foot drags behind instead
    }

    /// <summary>The four gait feet of the rest page (far fore, far hind, near fore, near hind), body frame: the page every walk starts and ends on.</summary>
    private static PointF[] RestFeet() => [Foot(0.26f, 0.5f), Foot(-0.64f, 0f), Foot(0.34f, 0f), Foot(-0.56f, 0.5f)];

    /// <summary>
    /// THE MOUNT (a possum going up a trunk, looked up first): it does NOT turn like a plank. It stands facing the bark, REARS (the
    /// front half pitches up about the hip, the hands reaching up the bark, the hind feet still planted on the ground), then the hind
    /// end FOLLOWS: the whole body rotates up while the pitch comes back to nothing as the body straightens along the bark, the hind
    /// feet walking up after the front. "k" 0..1; 0 is the rest page and 1 is exactly the climb's first page (Walk(0) turned a
    /// quarter). The body's middle travels from its start to the climb's by the fractions fx, fy; "dx" is that sideways distance in
    /// units (the feet are worked out against it) and "rot" the whole body's turn.
    /// </summary>
    public static Pose Mount(float k, float dx, out float rot, out float fx, out float fy)
    {
        fx = Sm(k / 0.9f); fy = Sm((k - 0.3f) / 0.7f);
        rot = -90f * Sm((k - 0.35f) / 0.65f);
        float front = -90f * Sm(k / 0.4f);                                       // the front half's own angle: up the bark early, and it stays there
        var q = Stand();
        q.Gait = false; q.FlatFeet = true; q.NoTail = true;
        q.Pitch = front - rot;
        q.HeadAng = 9f - 18f * MathF.Sin(MathF.PI * k);                          // looks up the trunk as it goes
        PointF mid = P(dx * fx, PivotY - 0.94f * fy), mid0 = P(0f, PivotY), mid1 = P(dx, PivotY - 0.94f);
        var rest = RestFeet();
        var feet = new PointF[4];
        // the front legs: from the planted rest feet, reaching up along the front half (forward and a little to the belly side of the shoulder) and back to the rest feet
        float a = Sm(k / 0.4f) * (1f - Sm((k - 0.6f) / 0.4f));
        for (int i = 0; i < 4; i += 2)
        {
            PointF j = Upper(q, i == 0 ? 0.26f : 0.34f, -0.46f);
            PointF up = Rot(P(0.42f, 0.22f), q.Pitch);
            feet[i] = Reach(j, Lerp(rest[i], P(j.X + up.X, j.Y + up.Y), a));
        }
        // the hind legs: planted on the ground where they were, then walking up the bark after the body, never below the ground
        float s = Sm((k - 0.3f) / 0.65f);
        for (int i = 1; i < 4; i += 2)
        {
            PointF w0 = ToWorld(rest[i], mid0, 0f), w1 = ToWorld(rest[i], mid1, -90f), w = Lerp(w0, w1, s);
            w.Y = MathF.Min(w.Y, 0f);
            PointF j = Upper(q, i == 1 ? -0.64f : -0.56f, -0.46f);
            feet[i] = Reach(j, ToBody(w, mid, rot));
        }
        q.Feet = feet;
        q.Hands = a > 0.5f ? 1f : 0f;
        return q;
    }

    /// <summary>
    /// THE DISMOUNT: head first down the bark, then the front end reaches for the ground (the front half pitches out and down while the
    /// hind end is still on the trunk), the hands land, and the hind end comes down after, until it stands on the rest page. The page is
    /// painted unmirrored, facing right, the trunk to its left; "dx" is the trunk's distance to the left of the landing, "h" the
    /// middle's height above the ground at the start, in units. "k" 0 is the descent's rest page (Walk(0) turned the other quarter).
    /// </summary>
    public static Pose Dismount(float k, float dx, float h, out float rot, out float fx, out float fy)
    {
        fx = Sm(k / 0.85f); fy = Sm((k - 0.2f) / 0.8f);
        rot = 90f * (1f - Sm((k - 0.25f) / 0.75f));
        float front = 90f * (1f - Sm(k / 0.55f));
        var q = Stand();
        q.Gait = false; q.FlatFeet = true; q.NoTail = true;
        q.Pitch = front - rot;
        PointF mid0 = P(-dx, -h), mid1 = P(0f, PivotY);
        PointF mid = P(mid0.X + (mid1.X - mid0.X) * fx, mid0.Y + (mid1.Y - mid0.Y) * fy);
        var rest = RestFeet();
        var feet = new PointF[4];
        float sf = Sm(k / 0.6f), sh = Sm((k - 0.55f) / 0.45f);
        for (int i = 0; i < 4; i++)
        {
            bool fore = i is 0 or 2;
            float s = fore ? sf : sh;
            PointF w = Lerp(ToWorld(rest[i], mid0, 90f), ToWorld(rest[i], mid1, 0f), s);
            w.Y = MathF.Min(w.Y, 0f);
            PointF j = Upper(q, fore ? (i == 0 ? 0.26f : 0.34f) : (i == 1 ? -0.64f : -0.56f), -0.46f);
            feet[i] = Reach(j, ToBody(w, mid, rot));
        }
        q.Feet = feet;
        return q;
    }

    /// <summary>The tail: a chain of short pieces from "root", pink, thin, tapering a little, with a darker edge along its underside.</summary>
    private static PointF[] Tail(Graphics g, PointF root, in Pose q, float length = 2.0f)
    {
        const int N = 16;
        float seg = length / N;
        var spine = new PointF[N + 1];
        spine[0] = root;
        for (int i = 0; i < N; i++)
        {
            float s = (i + 0.5f) / N;
            float ang = q.TailBase + q.TailCurl * s + q.TailS * MathF.Sin(MathF.PI * s) + q.TailWave * MathF.Sin(q.TailPhase - i * 0.5f);
            float r = ang * MathF.PI / 180f;
            spine[i + 1] = P(spine[i].X + seg * MathF.Cos(r), spine[i].Y + seg * MathF.Sin(r));
        }
        for (int i = 0; i < N; i++)
        {
            float w0 = 0.15f - 0.07f * i / N, w1 = 0.15f - 0.07f * (i + 1) / N;
            Bone(g, spine[i], spine[i + 1], w0, w1, TailEdge);
        }
        for (int i = 0; i < N; i++)
        {
            float w0 = 0.115f - 0.055f * i / N, w1 = 0.115f - 0.055f * (i + 1) / N;
            Bone(g, P(spine[i].X, spine[i].Y - 0.012f), P(spine[i + 1].X, spine[i + 1].Y - 0.012f), w0, w1, TailPink);
        }
        return spine;
    }

    /// <summary>One foot's spot on the waddle: the foot plants and slides back for 60 percent of the cycle, then swings forward, barely lifting.</summary>
    private static PointF Foot(float jx, float ph)
    {
        ph -= MathF.Floor(ph);
        const float a = 0.42f;                                   // half a stride
        if (ph < 0.6f) return P(jx + a - 2f * a * (ph / 0.6f), 0f);
        float k = (ph - 0.6f) / 0.4f;
        return P(jx - a + 2f * a * k, -0.13f * MathF.Sin(MathF.PI * k));
    }

    /// <summary>A hand or a foot: a flat pink pad at the end of a leg; open, the fingers fan out as short strokes.</summary>
    private static void Paw(Graphics g, PointF at, float open, bool flat, float dirDeg = 0f)
    {
        if (flat) { Disc(g, at.X + 0.05f, at.Y - 0.045f, 0.15f, 0.06f, Pink); return; }
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.RotateTransform(dirDeg);
        Disc(g, 0.04f, 0f, 0.10f, 0.085f, Pink);
        if (open > 0.5f)
        {
            using var pen = new Pen(Pink, 0.045f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 4; i++)
            {
                float a = (-48f + i * 32f) * MathF.PI / 180f;
                g.DrawLine(pen, 0.08f * MathF.Cos(a), 0.08f * MathF.Sin(a), 0.22f * MathF.Cos(a), 0.22f * MathF.Sin(a));
            }
        }
        g.Restore(st);
    }

    private static void Acorn(Graphics g, PointF at, float angDeg)
    {
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.RotateTransform(angDeg);
        Disc(g, 0f, 0.02f, 0.11f, 0.15f, Color.FromArgb(176, 118, 60));
        Disc(g, 0f, -0.07f, 0.12f, 0.08f, Color.FromArgb(104, 68, 34));
        g.Restore(st);
    }

    /// <summary>
    /// The head, in its own frame (+x along the face toward the nose, +y toward the throat), origin at the neck. In
    /// profile: a long white wedge with a pink tip, the ears well back, an eye with a highlight, the lower jaw hinged so
    /// the mouth can open and a pink tongue come out. Face on: a pointed white shield with two ears and two eyes.
    /// </summary>
    private static void Head(Graphics g, in Pose q)
    {
        float eyeOpen = q.Eye == 0 ? 1f : 0f;
        if (q.Turn > 0.2f)
        {
            // THE FACE ON (and the three-quarter, the same shield shifted toward the way it is turning): the nose is foreshortened.
            float lean = (1f - q.Turn) * 0.9f;                                   // 0.5 turn: the muzzle swings a little off the middle line
            float L = 1f - 0.0f;
            Disc(g, 0.04f, -0.46f * L, 0.19f, 0.19f, Ear);
            Disc(g, 0.04f, 0.46f * L, 0.19f, 0.19f, Ear);
            Fill(g, [P(-0.08f, -0.30f), P(0.14f, -0.44f), P(0.42f, -0.38f), P(0.66f, -0.17f + lean * 0.12f), P(0.82f, -0.07f + lean * 0.3f), P(0.90f, 0.0f + lean * 0.4f),
                     P(0.82f, 0.07f + lean * 0.3f), P(0.66f, 0.17f + lean * 0.12f), P(0.42f, 0.38f), P(0.14f, 0.44f), P(-0.08f, 0.30f)], Face, 0.5f);
            Fill(g, [P(0.40f, -0.12f), P(0.80f, -0.03f), P(0.86f, 0.0f), P(0.80f, 0.03f), P(0.40f, 0.12f)], FaceShade, 0.5f);
            Disc(g, 0.86f, 0.0f + lean * 0.4f, 0.095f, 0.095f, Pink);
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = 0.50f, ey = s * 0.17f + lean * 0.14f * 0f;
                EyeAt(g, q, ex, ey, 0.062f, eyeOpen, s);
            }
            if (q.Mouth > 0.2f) Disc(g, 0.78f, 0.0f, 0.07f, 0.045f, Color.FromArgb(150, 50, 60));
            if (q.Tongue > 0.2f) Disc(g, 0.86f, 0.0f, 0.06f, 0.10f, Color.FromArgb(240, 120, 140));
            return;
        }
        // ---- the profile ----
        float jaw = q.Mouth * 34f;
        Disc(g, -0.12f, -0.34f, 0.15f, 0.15f, Color.FromArgb(28, 25, 27));          // the far ear, a shade darker
        Disc(g, 0.10f, -0.40f, 0.17f, 0.17f, Ear);                                  // the near ear
        // inside the mouth (drawn first; the head covers its top)
        if (q.Mouth > 0.05f)
        {
            PointF hinge = P(0.48f, 0.13f);
            PointF tip = Rot(P(0.62f, 0f), jaw);
            Fill(g, [P(hinge.X, hinge.Y - 0.05f), P(1.10f, 0.07f), P(hinge.X + tip.X, hinge.Y + tip.Y + 0.02f), P(hinge.X + 0.1f, hinge.Y + 0.08f)], Color.FromArgb(150, 50, 60), 0.2f);
            if (q.Tongue > 0.05f)                                                    // a pink tongue lolling out over the lower lip
            {
                PointF t0 = P(hinge.X + 0.45f, hinge.Y + 0.02f);
                PointF t1 = P(hinge.X + 0.45f + 0.18f * q.Tongue, hinge.Y + 0.12f * q.Tongue + tip.Y * 0.4f);
                Bone(g, t0, P(t1.X + 0.12f, t1.Y + 0.20f * q.Tongue), 0.15f, 0.16f, Color.FromArgb(240, 120, 140));
            }
            // the lower jaw, swung down about the hinge
            var st = g.Save();
            g.TranslateTransform(hinge.X, hinge.Y);
            g.RotateTransform(jaw);
            Fill(g, [P(-0.02f, -0.02f), P(0.45f, -0.02f), P(0.62f, 0.0f), P(0.58f, 0.07f), P(0.30f, 0.15f), P(-0.30f, 0.18f)], Face, 0.4f);
            g.Restore(st);
        }
        else
        {
            Fill(g, [P(0.0f, 0.16f), P(0.5f, 0.13f), P(0.95f, 0.10f), P(0.90f, 0.15f), P(0.5f, 0.28f), P(0.1f, 0.34f)], Face, 0.4f);   // the closed jaw
        }
        // the head itself: a long wedge, domed at the crown, narrowing to the tip
        Fill(g, [P(-0.10f, -0.28f), P(0.22f, -0.42f), P(0.56f, -0.32f), P(0.96f, -0.15f), P(1.18f, -0.03f), P(1.16f, 0.07f), P(0.90f, 0.10f), P(0.5f, 0.16f), P(0.12f, 0.34f), P(-0.12f, 0.20f)], Face, 0.45f);
        Fill(g, [P(0.10f, 0.10f), P(0.5f, 0.12f), P(0.9f, 0.09f), P(0.5f, 0.24f), P(0.18f, 0.32f)], FaceShade, 0.4f);        // a soft grey under the cheek
        Disc(g, 1.14f, 0.0f, 0.105f, 0.095f, Pink);                                                                   // the nose
        using (var pen = new Pen(Color.FromArgb(150, 90, 90), 0.025f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            if (q.Mouth < 0.1f) g.DrawLine(pen, 1.06f, 0.10f, 0.72f, 0.16f);                                           // the closed mouth line
        EyeAt(g, q, 0.64f, -0.10f, 0.072f, eyeOpen, 0);
    }

    private static void EyeAt(Graphics g, in Pose q, float x, float y, float r, float open, int side)
    {
        if (q.Eye == 2)                                                              // an X
        {
            using var pen = new Pen(Ink, 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, x - r * 1.1f, y - r * 1.1f, x + r * 1.1f, y + r * 1.1f);
            g.DrawLine(pen, x - r * 1.1f, y + r * 1.1f, x + r * 1.1f, y - r * 1.1f);
            return;
        }
        if (q.Eye == 1 || (q.Eye == 3 && side != 0 && side != 1))                  // shut: a short curved line
        {
            using var pen = new Pen(Ink, 0.045f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, x - r, y - r * 0.6f, r * 2f, r * 1.2f, 0, 180);
            return;
        }
        if (q.Eye == 3)                                                              // the peek: a slit of white with a dark pupil
        {
            Disc(g, x, y, r * 1.1f, r * 0.55f, Color.White);
            Disc(g, x + r * 0.2f, y, r * 0.5f, r * 0.5f, Ink);
            return;
        }
        Disc(g, x, y, r, r, Ink);
        Disc(g, x + r * 0.30f, y - r * 0.35f, r * 0.38f, r * 0.38f, Color.White);
        if (open < 0.5f) Disc(g, x, y, r, r * 0.4f, Face);
    }

    /// <summary>The whole possum, in its body frame (see the class comment).</summary>
    public static void Paint(Graphics g, Pose q)
    {
        if (q.Dead) { PaintDead(g, q); return; }
        var outer = g.Save();
        if (q.Yaw > 0.01f) g.ScaleTransform(1f - 0.58f * q.Yaw, 1f);                 // turning toward us: the body's length foreshortens
        if (q.Quiver != 0f) { g.TranslateTransform(0f, -PivotY); g.RotateTransform(7f * q.Quiver); g.TranslateTransform(0f, PivotY); }   // the shake: a lean about the feet

        // Where the upper body goes when it sits up or curls: turned about the hips.
        float th = (q.Bend - q.SitAng);
        PointF U(float x, float y) => Upper(q, x, y);
        float b = q.Bob;

        // ---- the legs' spots ----
        PointF[] jt = [P(0.26f, -0.46f + b), P(-0.64f, -0.46f + b), P(0.34f, -0.46f + b), P(-0.56f, -0.46f + b)];   // far fore, far hind, near fore, near hind
        PointF[] ft = new PointF[4];
        if (q.Gait)
        {
            float ph = q.Phase;
            ft[2] = Foot(jt[2].X, ph); ft[1] = Foot(jt[1].X, ph);                    // the near fore and the far hind go together
            ft[0] = Foot(jt[0].X, ph + 0.5f); ft[3] = Foot(jt[3].X, ph + 0.5f);     // the far fore and the near hind go together
        }
        else if (q.Feet != null) for (int i = 0; i < 4; i++) ft[i] = q.Feet[i];
        if (q.SitAng > 0f)
        {
            // Sitting up: from the standing feet toward the sitting ones as the body rises. The hind feet come flat in front of the
            // haunches; the forepaws leave the ground for the chest (or the face, below).
            float f = Math.Clamp(q.SitAng / 58f, 0f, 1f);
            PointF[] sit = [U(0.52f, -0.42f), P(-0.36f, 0f), U(0.58f, -0.34f), P(-0.10f, 0f)];
            for (int i = 0; i < 4; i++) ft[i] = P(ft[i].X + (sit[i].X - ft[i].X) * f, ft[i].Y + (sit[i].Y - ft[i].Y) * f);
        }
        PointF shoulder0 = U(jt[0].X, jt[0].Y), shoulder1 = U(jt[2].X, jt[2].Y);
        PointF neck = U(0.02f, -0.80f + b);
        float headAng = q.HeadAng + th + q.Pitch;
        // sitting up with a nut (or wiping): the hands go to the face, worked out from where the head ended up
        PointF M(float u, float v) { var r = Rot(P(u, v), headAng); return P(neck.X + r.X, neck.Y + r.Y); }
        bool toFace = q.Nut == 1 || q.Wipe > 0f;
        // A foot is a flat pink pad while it walks or sits (the hind ones always); a hand is round, fanned when open.
        bool Flat(int i) => (q.Gait || q.FlatFeet) && (i is 1 or 3 || q.SitAng <= 0f && q.Hands < 0.5f) && !(toFace && i is 0 or 2);
        if (toFace)
        {
            ft[0] = M(0.86f, 0.30f); ft[2] = M(0.96f, 0.44f);
            if (q.Wipe > 0f) { ft[2] = M(0.55f - 0.5f * q.Wipe, -0.05f + 0.1f * q.Wipe); }       // one paw sweeping back over the cheek and ear
        }

        // ---- the tail first, behind everything ----
        PointF tailRoot = P(-1.0f, -0.74f + b);
        if (q.SitAng > 0f || q.Bend != 0f) tailRoot = U(-1.0f, -0.74f + b);
        if (!q.NoTail) Tail(g, tailRoot, q);              // a live tail (NoTail) is drawn by the scene every frame, as a chain

        // ---- the far legs ----
        for (int i = 0; i < 2; i++)
        {
            PointF j = i == 0 ? shoulder0 : U(jt[1].X, jt[1].Y);
            if (q.SitAng > 0f && i == 1) j = P(-0.55f, -0.40f);
            Bone(g, j, ft[i], 0.19f, 0.15f, LegFar);
            Paw(g, ft[i], q.Hands, Flat(i), i is 0 or 2 ? q.Pitch : 0f);   // a reaching hand points along the front half
        }

        // ---- the body: grey, a darker back and a paler belly, one smooth shape each ----
        PointF[] torso = [U(-1.06f, -0.60f + b), U(-0.92f, -0.90f + b), U(-0.2f, -1.0f + b), U(0.36f, -0.95f + b), U(0.64f, -0.72f + b), U(0.46f, -0.40f + b), U(-0.1f, -0.33f + b), U(-0.72f, -0.36f + b)];
        Fill(g, torso, Fur, 0.5f);
        Fill(g, [U(-1.02f, -0.66f + b), U(-0.92f, -0.90f + b), U(-0.2f, -1.0f + b), U(0.36f, -0.95f + b), U(0.52f, -0.84f + b), U(0.0f, -0.78f + b), U(-0.7f, -0.76f + b)], Back, 0.5f);
        Fill(g, [U(-0.7f, -0.42f + b), U(-0.1f, -0.37f + b), U(0.40f, -0.43f + b), U(0.30f, -0.52f + b), U(-0.2f, -0.52f + b)], Belly, 0.5f);

        // ---- the near legs ----
        for (int i = 2; i < 4; i++)
        {
            PointF j = i == 2 ? shoulder1 : U(jt[3].X, jt[3].Y);
            if (q.SitAng > 0f && i == 3) j = P(-0.50f, -0.40f);
            Bone(g, j, ft[i], 0.20f, 0.16f, Leg);
            Paw(g, ft[i], q.Hands, Flat(i), i is 0 or 2 ? q.Pitch : 0f);   // a reaching hand points along the front half
        }

        // ---- the head, over the body ----
        {
            var st = g.Save();
            g.TranslateTransform(neck.X, neck.Y);
            g.RotateTransform(headAng);
            Head(g, q);
            g.Restore(st);
        }
        // ---- the nut and the hands over the face ----
        if (q.Nut == 1) Acorn(g, M(0.88f, 0.24f), 90f + headAng);
        if (toFace)
        {
            Paw(g, ft[0], 1f - q.Hands * 0f, false, headAng - 80f);
            Paw(g, ft[2], 1f, false, headAng - 80f);
        }
        g.Restore(outer);
    }

    /// <summary>On its side, stiff: the belly toward us (pale), four legs sticking up, the head on the ground with its mouth open and tongue out, the tail limp.</summary>
    private static void PaintDead(Graphics g, in Pose q)
    {
        PointF root = P(-1.0f, -0.30f);
        Tail(g, root, q, 1.9f);
        // the stiff legs, straight up and a little out, the hands open
        PointF[] hips = [P(0.30f, -0.50f), P(-0.62f, -0.50f), P(0.10f, -0.52f), P(-0.80f, -0.52f)];
        float[] ang = [-62f, -80f, -100f, -118f];
        for (int i = 0; i < 4; i++)
        {
            float r = ang[i] * MathF.PI / 180f;
            PointF foot = P(hips[i].X + 0.62f * MathF.Cos(r), hips[i].Y + 0.62f * MathF.Sin(r));
            Bone(g, hips[i], foot, 0.19f, 0.15f, i < 2 ? LegFar : Leg);
            Paw(g, foot, 1f, false, ang[i] + 90f);
        }
        // the flattened body, belly up: dark fur at the edges, the pale belly showing
        Fill(g, [P(-1.08f, -0.32f), P(-0.9f, -0.60f), P(-0.2f, -0.68f), P(0.36f, -0.60f), P(0.66f, -0.38f), P(0.40f, -0.12f), P(-0.2f, -0.06f), P(-0.85f, -0.10f)], Back, 0.5f);
        Fill(g, [P(-0.86f, -0.34f), P(-0.7f, -0.56f), P(-0.2f, -0.60f), P(0.30f, -0.54f), P(0.50f, -0.38f), P(0.30f, -0.20f), P(-0.2f, -0.16f), P(-0.7f, -0.18f)], Belly, 0.5f);
        // the head, on the ground at the front, the mouth wide
        var st = g.Save();
        g.TranslateTransform(0.52f, -0.30f);
        g.RotateTransform(q.HeadAng - 4f);
        Head(g, q);
        g.Restore(st);
    }

    // ======================================================================== the test sheet

    /// <summary>PECKWORKS_POSSUMSHEET=path.png paints every pose, big (110 pixels to the unit) on the patch's earth, and writes it. Judge a painter here first.</summary>
    public static void WriteSheet(string path)
    {
        const float k = 110f;
        int w = (int)(5.2f * k), h = (int)(3.4f * k);
        var poses = new List<(string, Pose, float)>
        {
            ("walk 0", Walk(0f), 0f), ("walk .17", Walk(0.17f), 0f), ("walk .33", Walk(0.33f), 0f), ("walk .5", Walk(0.5f), 0f),
            ("walk .67", Walk(0.67f), 0f), ("walk .83", Walk(0.83f), 0f), ("sniff 1", Sniff(1f), 0f), ("look up", LookUp(), 0f),
            ("rise .5", Sit(0.5f, 0f, false), 0f), ("sit nut", Sit(1f, 0.2f, true), 0f), ("sit chew", Sit(1f, 1f, true), 0f), ("sit wipe", Sit(1f, 0f, false, 0.7f), 0f), ("turn .5", TurnPose(0.5f), 0f),
            ("turn 1", TurnPose(1f), 0f), ("faint .5", Faint(0.5f), 0f), ("faint 1", Faint(1f), 0f), ("dead", Dead(0f, false), 0f), ("dead peek", Dead(1f, true), 0f),
            ("shake", Shake(1f), 0f), ("climb down", Walk(0.2f), 90f), ("climb up", Walk(0.2f), -90f),
        };
        // the mount and the dismount, a page every fifth of the way (the sheet pins each page's body middle, so the travel is not shown)
        for (int m = 0; m <= 5; m++)
        {
            float kk = m / 5f;
            var mq = Mount(kk, 2.3f, out float mr, out _, out _);
            poses.Add(($"mount {kk:F1}", mq, mr));
        }
        for (int m = 0; m <= 5; m++)
        {
            float kk = m / 5f;
            var dq = Dismount(kk, 2.3f, 2.0f, out float dr, out _, out _);
            poses.Add(($"dismount {kk:F1}", dq, dr));
        }
        int perRow = 4, rows = (poses.Count + 3 + 1 + perRow - 1) / perRow;
        using var sheet = new Bitmap(w * perRow, h * rows, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(150, 110, 60));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int i = 0;
            foreach (var (name, pose, rot) in poses)
            {
                int cx = (i % perRow) * w, cy = (i / perRow) * h;
                var st = g.Save();
                g.SetClip(new Rectangle(cx, cy, w, h));
                g.TranslateTransform(cx + w * 0.58f, cy + h * 0.55f);
                if (rot != 0f) g.RotateTransform(rot);
                g.ScaleTransform(k, k);
                g.TranslateTransform(0f, -PivotY);
                Paint(g, pose);
                g.Restore(st);
                g.DrawString(name, SystemFonts.DefaultFont, Brushes.White, cx + 4, cy + 4);
                i++;
            }
            // the hang, on a pretend branch line (the wrap) at the top of its cell
            (string, Pose, float)[] hangs = [("hang 0", Hang(0, 1f), 0f), ("hang 1 look", Hang(1, 0f), 0f), ("hang 3 side", Hang(3, 1f), 3f), ("pull-up 110", Hang(0, 0f, 110f), 0f)];
            foreach (var (name, pose, sway) in hangs)
            {
                int cx = (i % perRow) * w, cy = (i / perRow) * h;
                var st = g.Save();
                g.SetClip(new Rectangle(cx, cy, w, h));
                g.TranslateTransform(cx + w * 0.5f, cy + 0.3f * k);
                g.RotateTransform(sway);
                g.TranslateTransform(0f, 2.3f * k);
                g.RotateTransform(90f);
                g.ScaleTransform(k, k);
                g.TranslateTransform(0f, -PivotY);
                Paint(g, pose);
                g.Restore(st);
                using var pen = new Pen(Color.FromArgb(120, 0, 0, 0), 1f);
                g.DrawLine(pen, cx, cy + 0.3f * k, cx + w, cy + 0.3f * k);
                g.DrawString(name, SystemFonts.DefaultFont, Brushes.White, cx + 4, cy + 4);
                i++;
            }
        }
        sheet.Save(path, ImageFormat.Png);
    }
}
