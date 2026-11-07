using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How the farm's stock and the dog are drawn: a horse, a cow, a pig, a hen
/// and a farm dog, each in side view facing right, in its own body units
/// (x forward, y UP, the origin under the body at the feet's ground line, one
/// unit the body's length). The yard (FarmYard.cs) paints each pose to a
/// sheet once and stamps the pages.
///
/// FEYNMAN VERSION: every animal here is the same recipe at a different
/// scale: a body (one soft closed shape), a neck and head, a tail, and four
/// legs, each leg two bones with the joint worked out by a little sum (the
/// same sum the squirrel uses: two bones of known length reaching from the
/// hip to a chosen foot place bend at exactly one spot). A WALK is a phase 0
/// to 1: each foot swings forward and back along the ground, lifted a little
/// in the forward swing, the four feet a quarter turn apart in the order a
/// walking animal's feet go (left hind, left fore, right hind, right fore).
/// A trot is the same with the diagonal pairs together and more lift. The
/// head has a dial for down (grazing) and the tail one for its swish.
/// These animals stand on the far hill at the house's scale (a horse is 0.037
/// U long), so the drawing is bold and simple: at that size an eye is one
/// pixel and a mane is a dark edge.
/// </summary>
internal static class FarmArt
{
    private static PointF P(float x, float y) => new(x, y);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>A pose for any of the four-legged ones: the walk phase (0 to 1, or -1 standing), how far the head is down (0 up, 1 at the grass), the tail's swing, and for the dog whether it sits.</summary>
    public sealed class Pose
    {
        public float Walk = -1f;                   // -1: standing, feet planted; else the phase of the walk
        public bool Trot;                          // the walk as a trot: diagonal pairs, more lift, longer stride
        public float HeadDown;                     // 0 up, 1 down at the grass
        public float Tail;                         // -1 to 1, the swish
        public float Lift;                         // a chewing or a rooting nod of the head, 0 to 1
        public bool Sit, Lie;                      // the dog sits; the pig lies
        public bool Mouth;                         // the dog barking: mouth open, head up
    }

    private static void Blob(Graphics g, Color c, float tension, params PointF[] pts)
    {
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, tension);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }
    private static void Ell(Graphics g, float cx, float cy, float rx, float ry, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }
    private static void Bone(Graphics g, PointF a, PointF b, float wa, float wb, Color c)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
        using var br = new SolidBrush(c);
        g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
        g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
        g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
    }

    /// <summary>
    /// A two-bone leg from the hip to the foot: the joint bends forward (a knee) or backward (a hock) by "kneeForward".
    /// The two bones are each "half" long; a foot farther than their reach is pulled in to it.
    /// </summary>
    private static void Leg(Graphics g, PointF hip, PointF foot, float half, Color c, float thick, bool kneeForward, Color? hoof = null)
    {
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y;
        float d = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), 2 * half - 0.002f);
        float al = MathF.Acos(Math.Clamp(d / (2 * half), -1f, 1f));
        float th = MathF.Atan2(dy, dx) + (kneeForward ? -al : al);
        PointF joint = P(hip.X + half * MathF.Cos(th), hip.Y + half * MathF.Sin(th));
        Bone(g, hip, joint, thick, thick * 0.8f, c);
        Bone(g, joint, foot, thick * 0.8f, thick * 0.6f, c);
        if (hoof is Color hc) Ell(g, foot.X + 0.005f, foot.Y + 0.01f, thick * 0.55f, thick * 0.35f, hc);
    }

    /// <summary>
    /// Where each foot is at walk phase p: the foot swings back along the ground while planted (the body goes over it)
    /// and forward through the air. "stride" is the stride length in body units, "lift" how high a swinging foot rises.
    /// Returns the four feet (near fore, far fore, near hind, far hind) as offsets from their rest places.
    /// </summary>
    private static (PointF NF, PointF FF, PointF NH, PointF FH) Feet(float p, bool trot, float stride, float lift)
    {
        PointF One(float phase)
        {
            float t = ((phase % 1f) + 1f) % 1f;
            if (t < 0.6f) return P(stride * (0.5f - t / 0.6f), 0f);                   // planted: sliding back under the body
            float a = (t - 0.6f) / 0.4f;
            return P(Lerp(-stride * 0.5f, stride * 0.5f, a * a * (3 - 2 * a)), lift * MathF.Sin(MathF.PI * a));   // swinging forward
        }
        // A walk: left hind, left fore, right hind, right fore, a quarter apart. A trot: diagonal pairs together, half a cycle apart.
        return trot
            ? (One(p), One(p + 0.5f), One(p + 0.5f), One(p))
            : (One(p + 0.25f), One(p + 0.75f), One(p), One(p + 0.5f));
    }

    // ================================================================ the horse

    /// <summary>A horse's colours: the coat, its mane and tail and lower legs (often black), the muzzle.</summary>
    public sealed record HorseCoat(Color Body, Color Points, Color Muzzle)
    {
        public static readonly HorseCoat Bay = new(Color.FromArgb(126, 74, 42), Color.FromArgb(44, 32, 30), Color.FromArgb(90, 60, 44));
        public static readonly HorseCoat Chestnut = new(Color.FromArgb(160, 90, 48), Color.FromArgb(120, 62, 34), Color.FromArgb(150, 100, 70));
        public static readonly HorseCoat Grey = new(Color.FromArgb(196, 190, 184), Color.FromArgb(120, 116, 112), Color.FromArgb(110, 104, 100));
    }

    /// <summary>A horse, one unit long nose to tail root, standing 0.68 high at the withers.</summary>
    public static void Horse(Graphics g, Pose q, HorseCoat coat)
    {
        float legLen = 0.40f, belly = 0.42f;
        var (nf, ff, nh, fh) = q.Walk < 0 ? (P(0, 0), P(0, 0), P(0, 0), P(0, 0)) : Feet(q.Walk, q.Trot, q.Trot ? 0.34f : 0.26f, q.Trot ? 0.10f : 0.06f);
        float bob = q.Walk < 0 ? 0f : 0.012f * MathF.Sin(MathF.Tau * 2f * q.Walk);
        Color far = Color.FromArgb(Math.Max(0, coat.Body.R - 30), Math.Max(0, coat.Body.G - 25), Math.Max(0, coat.Body.B - 20));
        // The far legs first.
        Leg(g, P(0.30f, belly + bob), P(0.32f + ff.X, ff.Y), legLen * 0.56f, far, 0.07f, true, coat.Points);
        Leg(g, P(-0.30f, belly + 0.02f + bob), P(-0.34f + fh.X, fh.Y), legLen * 0.56f, far, 0.08f, false, coat.Points);
        // The tail: a hanging plume from the rump, swung by the swish.
        Blob(g, coat.Points, 0.4f, P(-0.46f, 0.62f + bob), P(-0.52f - 0.06f * q.Tail, 0.45f + bob), P(-0.54f - 0.12f * q.Tail, 0.22f + bob), P(-0.47f - 0.10f * q.Tail, 0.18f + bob), P(-0.44f - 0.04f * q.Tail, 0.40f + bob), P(-0.42f, 0.60f + bob));
        // The body: deep through the chest, the back dipping a little, the rump round.
        Blob(g, coat.Body, 0.55f, P(0.36f, belly - 0.02f + bob), P(0.40f, 0.60f + bob), P(0.30f, 0.70f + bob), P(0.0f, 0.66f + bob), P(-0.30f, 0.70f + bob), P(-0.46f, 0.62f + bob), P(-0.44f, 0.44f + bob), P(-0.30f, belly - 0.02f + bob), P(0.0f, belly - 0.06f + bob));
        // The neck and head: the neck rises from the withers, the head a wedge on its end; both come down with HeadDown.
        float hd = q.HeadDown;
        PointF neckTop = P(Lerp(0.56f, 0.70f, hd), Lerp(0.92f, 0.45f, hd) + bob);
        PointF nose = P(Lerp(0.78f, 0.86f, hd), Lerp(0.78f, 0.06f, hd) + bob - 0.02f * q.Lift);
        Bone(g, P(0.34f, 0.64f + bob), neckTop, 0.20f, 0.14f, coat.Body);
        Blob(g, coat.Points, 0.3f, P(0.30f, 0.72f + bob), P(0.40f, 0.78f + bob), neckTop, P(neckTop.X - 0.05f, neckTop.Y + 0.02f));   // the mane along the neck's crest
        Blob(g, coat.Body, 0.4f, P(neckTop.X - 0.06f, neckTop.Y + 0.06f), P(neckTop.X + 0.04f, neckTop.Y + 0.08f), P(nose.X, nose.Y + 0.05f), P(nose.X + 0.02f, nose.Y - 0.03f), P(neckTop.X + 0.02f, neckTop.Y - 0.10f), P(neckTop.X - 0.08f, neckTop.Y - 0.06f));
        Ell(g, nose.X - 0.01f, nose.Y, 0.05f, 0.045f, coat.Muzzle);
        Ell(g, neckTop.X - 0.02f, neckTop.Y + 0.09f, 0.025f, 0.05f, coat.Body);                  // an ear
        Ell(g, neckTop.X + 0.02f, neckTop.Y - 0.02f, 0.014f, 0.014f, Color.FromArgb(30, 24, 22)); // the eye
        // The near legs.
        Leg(g, P(0.30f, belly + bob), P(0.30f + nf.X, nf.Y), legLen * 0.56f, coat.Body, 0.08f, true, coat.Points);
        Leg(g, P(-0.30f, belly + 0.02f + bob), P(-0.32f + nh.X, nh.Y), legLen * 0.56f, coat.Body, 0.09f, false, coat.Points);
    }

    // ================================================================ the cow

    public sealed record CowCoat(Color Body, Color Patch, Color Udder)
    {
        public static readonly CowCoat Holstein = new(Color.FromArgb(230, 226, 218), Color.FromArgb(40, 34, 34), Color.FromArgb(226, 176, 160));
        public static readonly CowCoat Jersey = new(Color.FromArgb(176, 120, 72), Color.FromArgb(140, 90, 52), Color.FromArgb(220, 170, 150));
    }

    /// <summary>A cow, one unit long, boxy and deep, 0.60 high at the back, the head carried low; "patches" places the Holstein's black.</summary>
    public static void Cow(Graphics g, Pose q, CowCoat coat, int patches)
    {
        float belly = 0.34f;
        var (nf, ff, nh, fh) = q.Walk < 0 ? (P(0, 0), P(0, 0), P(0, 0), P(0, 0)) : Feet(q.Walk, false, 0.22f, 0.05f);
        float bob = q.Walk < 0 ? 0f : 0.008f * MathF.Sin(MathF.Tau * 2f * q.Walk);
        Color far = Color.FromArgb(Math.Max(0, coat.Body.R - 36), Math.Max(0, coat.Body.G - 36), Math.Max(0, coat.Body.B - 36)), hoof = Color.FromArgb(50, 40, 36);
        Leg(g, P(0.30f, belly + bob), P(0.32f + ff.X, ff.Y), 0.20f, far, 0.07f, true, hoof);
        Leg(g, P(-0.32f, belly + bob), P(-0.34f + fh.X, fh.Y), 0.20f, far, 0.08f, false, hoof);
        // The tail: a thin rope with a tuft, hanging from the rump, swung by the swish.
        using (var rope = new Pen(coat.Body, 0.03f) { EndCap = LineCap.Round }) g.DrawLine(rope, 0.46f * -1f, 0.58f + bob, -0.50f - 0.10f * q.Tail, 0.22f + bob);
        Ell(g, -0.50f - 0.11f * q.Tail, 0.18f + bob, 0.04f, 0.05f, coat.Patch);
        // The body: a long deep box with rounded corners; the hip bones stand up a little at the rump.
        Blob(g, coat.Body, 0.35f, P(0.42f, belly - 0.04f + bob), P(0.44f, 0.56f + bob), P(0.30f, 0.62f + bob), P(-0.30f, 0.62f + bob), P(-0.46f, 0.60f + bob), P(-0.47f, 0.40f + bob), P(-0.36f, belly - 0.04f + bob), P(0.0f, belly - 0.08f + bob));
        // The patches (Holstein): a few dark blobs at fixed places chosen by "patches".
        if (patches > 0)
        {
            Blob(g, coat.Patch, 0.5f, P(-0.36f, 0.58f + bob), P(-0.10f, 0.60f + bob), P(-0.14f, 0.42f + bob), P(-0.32f, 0.36f + bob));
            if (patches > 1) Blob(g, coat.Patch, 0.5f, P(0.10f, 0.50f + bob), P(0.30f, 0.56f + bob), P(0.36f, 0.40f + bob), P(0.16f, 0.34f + bob));
        }
        Ell(g, -0.22f, belly - 0.08f + bob, 0.12f, 0.07f, coat.Udder);                            // the udder
        // The head: low and heavy on a short thick neck, a broad muzzle, small horns and a drooping ear; down to the grass with HeadDown.
        float hd = q.HeadDown;
        PointF head = P(Lerp(0.62f, 0.66f, hd), Lerp(0.50f, 0.12f, hd) + bob - 0.02f * q.Lift);
        Bone(g, P(0.40f, 0.52f + bob), P(head.X - 0.08f, head.Y + 0.04f), 0.22f, 0.16f, coat.Body);
        Blob(g, coat.Body, 0.4f, P(head.X - 0.12f, head.Y + 0.12f), P(head.X + 0.02f, head.Y + 0.12f), P(head.X + 0.14f, head.Y - 0.02f), P(head.X + 0.12f, head.Y - 0.12f), P(head.X - 0.04f, head.Y - 0.12f), P(head.X - 0.14f, head.Y - 0.02f));
        Ell(g, head.X + 0.11f, head.Y - 0.06f, 0.06f, 0.05f, Color.FromArgb(190, 150, 140));       // the muzzle
        Ell(g, head.X - 0.10f, head.Y + 0.06f, 0.05f, 0.03f, coat.Body);                           // the ear, out sideways
        using (var horn = new Pen(Color.FromArgb(200, 190, 170), 0.025f) { EndCap = LineCap.Round }) g.DrawLine(horn, head.X - 0.02f, head.Y + 0.12f, head.X + 0.02f, head.Y + 0.19f);
        Ell(g, head.X + 0.02f, head.Y + 0.02f, 0.013f, 0.013f, Color.FromArgb(30, 24, 22));
        Leg(g, P(0.30f, belly + bob), P(0.30f + nf.X, nf.Y), 0.20f, coat.Body, 0.08f, true, hoof);
        Leg(g, P(-0.32f, belly + bob), P(-0.32f + nh.X, nh.Y), 0.20f, coat.Body, 0.09f, false, hoof);
    }

    // ================================================================ the pig

    /// <summary>A pig, one unit long: a fat pink barrel on short legs, a flat snout, a flap of an ear, a curl of a tail; it roots with its snout down and lies on its side in the wallow.</summary>
    public static void Pig(Graphics g, Pose q)
    {
        Color pink = Color.FromArgb(222, 164, 150), dark = Color.FromArgb(186, 126, 114), snout = Color.FromArgb(200, 130, 120);
        if (q.Lie)
        {
            // On its side: the barrel flat on the ground, the legs stuck out in front, the head resting.
            Blob(g, pink, 0.5f, P(0.40f, 0.02f), P(0.42f, 0.22f), P(0.20f, 0.34f), P(-0.20f, 0.34f), P(-0.42f, 0.22f), P(-0.40f, 0.02f));
            Ell(g, 0.52f, 0.14f, 0.12f, 0.10f, pink); Ell(g, 0.62f, 0.12f, 0.04f, 0.035f, snout);
            Bone(g, P(0.20f, 0.08f), P(0.46f, 0.03f), 0.06f, 0.05f, dark); Bone(g, P(-0.10f, 0.08f), P(0.14f, 0.03f), 0.06f, 0.05f, dark);
            return;
        }
        var (nf, ff, nh, fh) = q.Walk < 0 ? (P(0, 0), P(0, 0), P(0, 0), P(0, 0)) : Feet(q.Walk, false, 0.16f, 0.05f);
        float belly = 0.22f;
        Leg(g, P(0.26f, belly), P(0.27f + ff.X, ff.Y), 0.12f, dark, 0.07f, true);
        Leg(g, P(-0.26f, belly), P(-0.27f + fh.X, fh.Y), 0.12f, dark, 0.08f, false);
        using (var curl = new Pen(pink, 0.03f) { EndCap = LineCap.Round }) g.DrawArc(curl, -0.50f, 0.42f, 0.10f, 0.10f, 200f + 60f * q.Tail, 250f);   // the curly tail
        Blob(g, pink, 0.6f, P(0.40f, belly - 0.02f), P(0.44f, 0.40f), P(0.20f, 0.54f), P(-0.20f, 0.54f), P(-0.44f, 0.42f), P(-0.40f, belly - 0.02f), P(0.0f, belly - 0.06f));
        float hd = q.HeadDown;
        PointF head = P(Lerp(0.54f, 0.52f, hd), Lerp(0.36f, 0.12f, hd) - 0.02f * q.Lift);
        Ell(g, head.X, head.Y, 0.14f, 0.12f, pink);
        Ell(g, head.X + 0.14f, head.Y - 0.03f * hd, 0.045f, 0.04f, snout);
        Blob(g, dark, 0.3f, P(head.X - 0.06f, head.Y + 0.08f), P(head.X + 0.02f, head.Y + 0.16f), P(head.X + 0.08f, head.Y + 0.06f));   // the ear flap
        Ell(g, head.X + 0.05f, head.Y + 0.03f, 0.012f, 0.012f, Color.FromArgb(30, 24, 22));
        Leg(g, P(0.26f, belly), P(0.26f + nf.X, nf.Y), 0.12f, pink, 0.08f, true);
        Leg(g, P(-0.26f, belly), P(-0.26f + nh.X, nh.Y), 0.12f, pink, 0.09f, false);
    }

    // ================================================================ the hen

    /// <summary>A hen (or the rooster, with "rooster"), one unit long: a plump body, a tail up behind, a small head on a short neck with the red comb, two stick legs. Pecks with HeadDown; "Walk" steps the legs.</summary>
    public static void Hen(Graphics g, Pose q, bool rooster)
    {
        Color body = rooster ? Color.FromArgb(150, 60, 30) : Color.FromArgb(214, 150, 70), tail = rooster ? Color.FromArgb(30, 60, 50) : Color.FromArgb(150, 98, 44);
        Color leg = Color.FromArgb(210, 170, 60), comb = Color.FromArgb(206, 40, 40);
        float step = q.Walk < 0 ? 0f : 0.16f * MathF.Sin(MathF.Tau * q.Walk);
        using (var l = new Pen(leg, 0.035f) { EndCap = LineCap.Round })
        {
            g.DrawLine(l, 0.02f, 0.30f, 0.02f + step, 0.0f);
            g.DrawLine(l, 0.10f, 0.30f, 0.10f - step, 0.0f);
        }
        Blob(g, tail, 0.5f, P(-0.30f, 0.46f), P(-0.52f, rooster ? 0.80f : 0.62f), P(-0.44f, rooster ? 0.86f : 0.66f), P(-0.20f, 0.56f));   // the tail, up
        Blob(g, body, 0.6f, P(0.30f, 0.30f), P(0.36f, 0.50f), P(0.14f, 0.62f), P(-0.20f, 0.58f), P(-0.34f, 0.42f), P(-0.16f, 0.26f), P(0.10f, 0.24f));
        float hd = q.HeadDown;
        PointF head = P(Lerp(0.42f, 0.56f, hd), Lerp(0.74f, 0.22f, hd));
        Bone(g, P(0.28f, 0.52f), head, 0.12f, 0.10f, body);
        Ell(g, head.X, head.Y, 0.09f, 0.08f, body);
        Blob(g, comb, 0.3f, P(head.X - 0.06f, head.Y + 0.07f), P(head.X - 0.02f, head.Y + 0.14f), P(head.X + 0.04f, head.Y + 0.13f), P(head.X + 0.06f, head.Y + 0.07f));
        Ell(g, head.X + 0.10f, head.Y - 0.01f, 0.04f, 0.025f, Color.FromArgb(230, 190, 80));     // the beak
        Ell(g, head.X + 0.02f, head.Y + 0.02f, 0.012f, 0.012f, Color.FromArgb(30, 24, 22));
    }

    // ================================================================ the dog

    /// <summary>A farm dog (a collie type), one unit long: a long body, a plumed tail carried up, a pointed muzzle, pricked ears; it trots, sits and barks.</summary>
    public static void Dog(Graphics g, Pose q)
    {
        Color coat = Color.FromArgb(150, 96, 50), white = Color.FromArgb(236, 230, 218), dark = Color.FromArgb(60, 42, 30);
        if (q.Sit)
        {
            // Sitting up: the haunches folded on the ground, the forelegs straight, the chest up, the head up.
            Blob(g, coat, 0.5f, P(-0.36f, 0.02f), P(-0.40f, 0.30f), P(-0.20f, 0.56f), P(0.08f, 0.62f), P(0.20f, 0.44f), P(0.12f, 0.02f));
            Blob(g, white, 0.5f, P(0.08f, 0.10f), P(0.14f, 0.44f), P(0.04f, 0.46f), P(-0.02f, 0.14f));                 // the white chest
            Bone(g, P(0.10f, 0.40f), P(0.18f, 0.02f), 0.09f, 0.07f, coat); Bone(g, P(0.04f, 0.38f), P(0.10f, 0.02f), 0.08f, 0.06f, dark);
            Blob(g, coat, 0.4f, P(-0.40f, 0.06f), P(-0.60f, 0.30f + 0.10f * q.Tail), P(-0.52f, 0.46f + 0.10f * q.Tail), P(-0.36f, 0.26f));   // the tail round the haunch
            PointF sh = P(0.22f, 0.78f);
            Bone(g, P(0.06f, 0.60f), sh, 0.16f, 0.14f, coat);
            Ell(g, sh.X, sh.Y, 0.12f, 0.11f, coat);
            Blob(g, coat, 0.3f, P(sh.X + 0.04f, sh.Y - 0.06f), P(sh.X + 0.22f, sh.Y - 0.02f + 0.06f * (q.Mouth ? 1 : 0)), P(sh.X + 0.06f, sh.Y + 0.04f));
            if (q.Mouth) Blob(g, Color.FromArgb(200, 90, 90), 0.3f, P(sh.X + 0.06f, sh.Y - 0.06f), P(sh.X + 0.20f, sh.Y - 0.10f), P(sh.X + 0.06f, sh.Y - 0.10f));
            Blob(g, dark, 0.3f, P(sh.X - 0.08f, sh.Y + 0.06f), P(sh.X - 0.04f, sh.Y + 0.22f), P(sh.X + 0.02f, sh.Y + 0.08f));   // an ear, pricked
            Ell(g, sh.X + 0.04f, sh.Y + 0.02f, 0.014f, 0.014f, dark);
            Ell(g, sh.X + 0.22f, sh.Y - 0.01f, 0.025f, 0.02f, dark);
            return;
        }
        var (nf, ff, nh, fh) = q.Walk < 0 ? (P(0, 0), P(0, 0), P(0, 0), P(0, 0)) : Feet(q.Walk, true, 0.30f, 0.10f);
        float belly = 0.36f, bob = q.Walk < 0 ? 0f : 0.015f * MathF.Sin(MathF.Tau * 2f * q.Walk);
        Leg(g, P(0.26f, belly + bob), P(0.28f + ff.X, ff.Y), 0.20f, dark, 0.06f, true);
        Leg(g, P(-0.28f, belly + bob), P(-0.30f + fh.X, fh.Y), 0.20f, dark, 0.07f, false);
        Blob(g, coat, 0.45f, P(-0.42f, 0.52f + bob), P(-0.58f, 0.68f + 0.12f * q.Tail + bob), P(-0.50f, 0.76f + 0.12f * q.Tail + bob), P(-0.36f, 0.60f + bob));   // the plume, carried up
        Blob(g, coat, 0.55f, P(0.34f, belly - 0.02f + bob), P(0.38f, 0.56f + bob), P(0.10f, 0.62f + bob), P(-0.30f, 0.60f + bob), P(-0.44f, 0.52f + bob), P(-0.40f, 0.36f + bob), P(0.0f, belly - 0.06f + bob));
        Blob(g, white, 0.5f, P(0.34f, belly - 0.02f + bob), P(0.36f, 0.50f + bob), P(0.14f, 0.50f + bob), P(0.10f, belly - 0.04f + bob));   // the white chest and collar
        float hd = q.HeadDown;
        PointF head = P(Lerp(0.52f, 0.56f, hd), Lerp(0.74f, 0.22f, hd) + bob);
        Bone(g, P(0.32f, 0.56f + bob), head, 0.16f, 0.13f, coat);
        Ell(g, head.X, head.Y, 0.12f, 0.11f, coat);
        Blob(g, coat, 0.3f, P(head.X + 0.04f, head.Y - 0.06f), P(head.X + 0.22f, head.Y - 0.02f), P(head.X + 0.06f, head.Y + 0.04f));   // the muzzle
        if (q.Mouth) Blob(g, Color.FromArgb(200, 90, 90), 0.3f, P(head.X + 0.06f, head.Y - 0.06f), P(head.X + 0.20f, head.Y - 0.12f), P(head.X + 0.06f, head.Y - 0.10f));
        Blob(g, dark, 0.3f, P(head.X - 0.08f, head.Y + 0.06f), P(head.X - 0.04f, head.Y + 0.22f), P(head.X + 0.02f, head.Y + 0.08f));
        Ell(g, head.X + 0.04f, head.Y + 0.02f, 0.014f, 0.014f, dark);
        Ell(g, head.X + 0.22f, head.Y - 0.01f, 0.025f, 0.02f, dark);
        Leg(g, P(0.26f, belly + bob), P(0.26f + nf.X, nf.Y), 0.20f, coat, 0.07f, true);
        Leg(g, P(-0.28f, belly + bob), P(-0.28f + nh.X, nh.Y), 0.20f, coat, 0.08f, false);
    }

    /// <summary>Paints a pose to a sheet "w" by "h" pixels, "k" pixels to the body unit, the feet's origin at (ox, by); "squash" narrows the picture about the feet (a turn's pages).</summary>
    public static Sprite Page(Action<Graphics> paint, float k, int w, int h, int ox, int by, float squash = 1f) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(k * squash, -k);
            paint(g);
        });
}
