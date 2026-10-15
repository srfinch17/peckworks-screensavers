using System.Numerics;

namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// SPACE: moving through a volume without passing through things.
//
// FEYNMAN VERSION. The minds in Mind.cs decide WHAT an animal does. This
// file is about HOW it gets there in three dimensions (left and right, forward
// and back, up and down) without clipping through another animal, the bed or
// the surface. It knows nothing about ponds, fish or pixels: everything is in
// one unit of length for all three axes (a saver picks the unit; Sakura Pond
// uses ground pixels, with z = depth in pixels), and the WORLD is a question
// the animal answers ("how much room is there at this point?"), not a thing
// this file knows. So a saver with birds in the air, or fish in a tank seen
// from the side, can use the same pieces.
//
// Three pieces:
//
//   1. Chain: what an animal IS for touching: beads along its spine, each
//      with a radius, like a string of pearls of different sizes. Any bendy
//      body (a koi, a snake, a turtle with a straight spine) is one. Two
//      chains touch when any two beads' pearls overlap, and Touch says which
//      way to push them apart and by how much.
//   2. Feelers: a straight feeler out from the nose, sampled at a few points,
//      asking the world "how much room here?" at each. The least answer is the
//      room along that feeler (0 = something in the way).
//   3. Feelers.Best: the smallest change of course that has enough room. It
//      tries the course as it is, then a few headings either side, each one
//      level, climbing and diving, and takes the best score: room, minus a
//      little for every step of turn and a little for a climb or dive. So an
//      animal with another one ahead goes over, under or round it, whichever
//      is the smaller change. "Enough" room counts the same as more: past a
//      body length of clear space nothing is gained by turning further.
//
// The rules learned on the pond's koi apply here too and are in docs/MINDS.md:
// feelers start closer than the smallest obstacle is wide; a refused step
// must itself force the search; a touch between animals has a cooldown.
// ============================================================================

/// <summary>
/// A bendy body's shape for touching and keeping clear: beads along its
/// spine, each with a radius, in one unit for x, y and z. The animal fills
/// Points and Radius each frame from its own spine, then calls Seal.
/// </summary>
public sealed class Chain
{
    /// <summary>The bead centres, nose first.</summary>
    public readonly Vector3[] Points;

    /// <summary>Each bead's radius (half the body's width there).</summary>
    public readonly float[] Radius;

    /// <summary>The box round the whole chain, pearls included (set by Seal): a cheap "could these two touch at all?" test.</summary>
    public Vector3 Min, Max;

    public Chain(int beads)
    {
        Points = new Vector3[beads];
        Radius = new float[beads];
    }

    /// <summary>Call after filling Points and Radius: works out the box.</summary>
    public void Seal()
    {
        Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        for (int i = 0; i < Points.Length; i++)
        {
            Vector3 r = new(Radius[i]);
            lo = Vector3.Min(lo, Points[i] - r);
            hi = Vector3.Max(hi, Points[i] + r);
        }
        Min = lo; Max = hi;
    }

    /// <summary>Could a ball of radius r at p be within "margin" of this chain? (The box test, before the exact one.)</summary>
    public bool Near(Vector3 p, float r, float margin)
    {
        float m = r + margin;
        return p.X + m >= Min.X && p.X - m <= Max.X && p.Y + m >= Min.Y && p.Y - m <= Max.Y && p.Z + m >= Min.Z && p.Z - m <= Max.Z;
    }

    /// <summary>
    /// The least gap between a ball of radius r at p and this body's surface:
    /// positive when clear by that much, negative when inside it. Walks the
    /// spine one piece at a time (bead to bead), measuring to the nearest
    /// point of each piece, with the radius blended along the piece.
    /// </summary>
    /// <param name="zStretch">
    /// How much more a gap UP or DOWN counts than the same gap sideways (1 =
    /// the same). For steering, passing over or under a thing needs less
    /// clearance than passing beside it (a fish clears another's back by a
    /// fraction of its width, while beside a bank its body swings as it
    /// turns), so an animal's "how much room here?" can hand this 2 or 3 and
    /// a body half a width below reads as well clear. Touch never stretches:
    /// bodies meet where they really meet.
    /// </param>
    public float Gap(Vector3 p, float r, float zStretch = 1)
    {
        float best = float.MaxValue;
        int n = Points.Length;
        Vector3 q = p with { Z = p.Z * zStretch };
        if (n == 1) return Vector3.Distance(q, Points[0] with { Z = Points[0].Z * zStretch }) - Radius[0] - r;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a = Points[i] with { Z = Points[i].Z * zStretch }, ab = (Points[i + 1] with { Z = Points[i + 1].Z * zStretch }) - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 1e-8f ? Math.Clamp(Vector3.Dot(q - a, ab) / len2, 0, 1) : 0;
            float d = Vector3.Distance(q, a + ab * t) - (Radius[i] + (Radius[i + 1] - Radius[i]) * t) - r;
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>
    /// Do these two bodies overlap? If so, "push" is the unit direction a
    /// should move to come apart (b moves the other way) and "depth" is how
    /// far, in all. Tests every piece of one against every piece of the other
    /// (the closest points of two line pieces, Ericson's method), after the
    /// box test has said they could touch at all. The deepest overlap wins,
    /// so two bodies crossing are pushed apart where they cross, not at the
    /// tail fin's tip.
    /// </summary>
    public static bool Touch(Chain a, Chain b, out Vector3 push, out float depth)
    {
        push = Vector3.UnitZ;
        depth = 0;
        if (a.Max.X < b.Min.X || a.Min.X > b.Max.X || a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y || a.Max.Z < b.Min.Z || a.Min.Z > b.Max.Z) return false;
        int na = a.Points.Length, nb = b.Points.Length;
        for (int i = 0; i < Math.Max(1, na - 1); i++)
            for (int j = 0; j < Math.Max(1, nb - 1); j++)
            {
                Vector3 p1 = a.Points[i], q1 = b.Points[j];
                Vector3 d1 = na > 1 ? a.Points[i + 1] - p1 : Vector3.Zero, d2 = nb > 1 ? b.Points[j + 1] - q1 : Vector3.Zero;
                Closest(p1, d1, q1, d2, out float s, out float t);
                Vector3 ca = p1 + d1 * s, cb = q1 + d2 * t;
                float ra = na > 1 ? a.Radius[i] + (a.Radius[i + 1] - a.Radius[i]) * s : a.Radius[0];
                float rb = nb > 1 ? b.Radius[j] + (b.Radius[j + 1] - b.Radius[j]) * t : b.Radius[0];
                Vector3 away = ca - cb;
                float dist = away.Length(), over = ra + rb - dist;
                if (over > depth)
                {
                    depth = over;
                    // Two pieces exactly on top of each other have no direction between them: push straight up, the one way that is always open in a pond.
                    push = dist > 1e-5f ? away / dist : Vector3.UnitZ;
                }
            }
        return depth > 0;
    }

    /// <summary>
    /// Where two line pieces come closest: s along the first (0 to 1), t
    /// along the second. The standard closed form (Ericson, Real-Time
    /// Collision Detection, 5.1.9), with the degenerate cases (a piece of no
    /// length, two parallel pieces) handled.
    /// </summary>
    private static void Closest(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2, out float s, out float t)
    {
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        const float eps = 1e-8f;
        if (a <= eps && e <= eps) { s = t = 0; return; }
        if (a <= eps) { s = 0; t = Math.Clamp(f / e, 0, 1); return; }
        float c = Vector3.Dot(d1, r);
        if (e <= eps) { t = 0; s = Math.Clamp(-c / a, 0, 1); return; }
        float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
        s = denom > eps ? Math.Clamp((b * f - c * e) / denom, 0, 1) : 0;
        t = (b * s + f) / e;
        if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
        else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
    }
}

/// <summary>
/// Feelers: straight lines out from an animal, asking the world how much room
/// there is along them, and the search for the smallest change of course with
/// enough room.
/// </summary>
public static class Feelers
{
    /// <summary>
    /// The world, as a question: how much open space is there round this
    /// point? 0 means "something is here" (land, the bed, inside another
    /// animal); more means at least that far to the nearest thing. The animal
    /// answers it from whatever it knows (a mask, a distance grid, the other
    /// animals' chains), so this file never needs to know what a pond is.
    /// </summary>
    public delegate float Room(in Vector3 at);

    /// <summary>
    /// Where along a feeler the world is asked, as fractions of its length.
    /// The first is close in, so a thing narrower than half a feeler cannot
    /// slip between the samples (a koi once nosed into a small rock that sat
    /// between "halfway" and "the end" for a minute).
    /// </summary>
    public static ReadOnlySpan<float> Samples => [0.12f, 0.35f, 0.65f, 1f];

    /// <summary>
    /// The room along a feeler from "from" in the direction "dir" (unit
    /// length in the plane; its z is the climb per unit forward), "dist"
    /// long along the plane: the least of the samples, the nearer ones
    /// counting for more (a thing close in matters more than the same thing
    /// at the feeler's end). 0 the moment any sample is blocked.
    /// </summary>
    public static float Along(Room room, in Vector3 from, in Vector3 dir, float dist)
    {
        float least = float.MaxValue;
        foreach (float f in Samples)
        {
            Vector3 p = from + dir * (dist * f);
            float r = room(in p);
            if (r <= 0) return 0;
            least = MathF.Min(least, r * (2 - f));
        }
        return least;
    }

    /// <summary>
    /// The smallest change of course with enough room. The course is a
    /// heading (radians in the x-y plane) and a climb (how much z changes per
    /// unit of distance forward: 0 level, negative up, positive down; z grows
    /// downward here, as depth does). Tries the course as it is, then "steps"
    /// headings either side, each one level, climbing and diving, and returns
    /// the best scored: the room along it, capped at "enough" (more than
    /// enough counts the same, so it takes the smallest turn that clears the
    /// thing and follows its edge rather than heading for the middle), less
    /// "turnCost" per step of turn and "climbCost" for a climb or dive.
    ///
    /// Whether a climb is worth more than a turn is the WORLD's business, not
    /// this search's: an animal that should pass over or under a thing on
    /// less clearance than it would pass beside it answers "how much room?"
    /// with the gap up or down stretched (Chain.Gap's zStretch). Then a
    /// climbing course simply has more room and wins on that. (Scoring
    /// climbs by their own, smaller "enough" in here was tried first: a
    /// climb then beat the level course beside a wall it did nothing about,
    /// and a fish kept diving under another long after it was clear, because
    /// the search's idea of "clear" and the trigger's did not agree.)
    /// </summary>
    /// <param name="room">The world.</param>
    /// <param name="from">Where the animal is.</param>
    /// <param name="heading">Its heading now.</param>
    /// <param name="look">How far ahead the feelers reach.</param>
    /// <param name="enough">Room past which more counts for nothing (about a body length).</param>
    /// <param name="step">The angle between tried headings.</param>
    /// <param name="steps">How many headings to try either side.</param>
    /// <param name="climb">The climb (and dive) to try, z per unit forward; 0 tries only level courses.</param>
    /// <param name="turnCost">Score taken off per step of turn.</param>
    /// <param name="climbCost">Score taken off for a climb or a dive.</param>
    public static (float Heading, float Climb, float Room) Best(Room room, in Vector3 from, float heading, float look, float enough,
        float step, int steps, float climb, float turnCost, float climbCost)
    {
        float bestH = heading, bestC = 0, bestScore = float.MinValue, bestRoom = 0;
        ReadOnlySpan<float> climbs = climb > 0 ? [0, -climb, climb] : [0f];
        for (int k = 0; k <= steps; k++)
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
            {
                if (k == 0 && side < 0) continue;
                float h = heading + side * k * step, ch = MathF.Cos(h), sh = MathF.Sin(h);
                // Climbs and dives are tried straight ahead and one step either
                // side only: a hard turn AND a climb is never the smallest change,
                // and each feeler asks the world four times (every other body's
                // chain each time). 13 feelers instead of 27 took the search's
                // burst off the pond's worst second.
                foreach (float c in k <= 1 ? climbs : climbs[..1])
                {
                    // The feeler's direction: one unit forward along h in the
                    // plane, and c of z with it. NOT made unit length: a climbing
                    // animal here still covers the same ground per step and
                    // changes depth on top of it (that is how Koi moves), and a
                    // shortened feeler would find more room from a wall ahead
                    // simply by not reaching it (the self-check caught a climb
                    // "clearing" a bank that way).
                    Vector3 dir = new(ch, sh, c);
                    float r = Along(room, in from, in dir, look);
                    float score = MathF.Min(r, enough) - k * turnCost - (c != 0 ? climbCost : 0);
                    if (score > bestScore) { bestScore = score; bestH = h; bestC = c; bestRoom = r; }
                }
            }
        return (bestH, bestC, bestRoom);
    }
}
