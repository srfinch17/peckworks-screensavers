using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// Where the sun is and how we look at the pond, shared by everything that
/// flies, falls or swims, so heights, depths and shadows agree across petals,
/// dragonflies, swallows, koi and turtles.
///
/// We look down at the pond from above and a little in front. A thing HIGH
/// above a spot on the water therefore shows a little further UP the screen
/// than the spot itself ("Lift", per unit of height), and its shadow, cast
/// by the sun up and to the left, falls down and to the right of the spot
/// ("ShadowX", "ShadowY", per unit of height). Heights are in u, the size unit.
/// </summary>
internal static class Light
{
    public const float Lift = 0.75f;
    public const float ShadowX = 0.45f, ShadowY = 0.6f;

    /// <summary>
    /// The same rule run the other way for things UNDER the water: a thing at
    /// a depth below the surface shows this much further DOWN the screen, per
    /// unit of depth, than the spot of surface above it. Less than Lift,
    /// because water bends the light: looking into a pond at a slant, the
    /// bottom looks shallower than it is (about three quarters of the true
    /// depth, which is why a stick in water looks bent at the surface).
    /// </summary>
    public const float Sink = 0.55f;

    /// <summary>
    /// How high the eye is above the water, in u. Things under the water are
    /// drawn smaller the further they are from the eye, and this is what sets
    /// how fast: a fish at depth d (as it appears through the water, three
    /// quarters of its true depth) is EyeU / (EyeU + 0.75 d) of its size at
    /// the surface. The eye is close, a few fish lengths up, so the shrink is
    /// something you can see: a fish at the limit of sight is about six tenths
    /// of its size at the top.
    /// </summary>
    public const float EyeU = 0.35f;

    /// <summary>
    /// The depth, in u, past which a thing in the water is lost from view:
    /// the pond's murk has turned it fully the water's own colour, exactly as
    /// the painted bed is lost in the middle of the pond. A koi that dives
    /// below this is gone until it comes up again. DEEPER than the pond's
    /// deepest water (Pond.DepthU) since 2026-10-26 (it was shallower, so the
    /// middle of the pond could hide a fish): the murk now tops out near 0.55
    /// on the bed, no fish is ever quite lost, and the painters' "nothing left
    /// to paint" culls past 0.985 are dormant (kept: a smaller SeeU wakes them).
    /// </summary>
    public const float SeeU = 0.92f;   // 0.84 until 2026-10-28 ("i want the water a tiny bit more translucent at depth": the haze at the pond's full depth 0.62 to 0.57, and Body.Deepen dims what is deep a little more, so a deep fish is dimmer and less veiled at once); 0.70 until 2026-10-27 (raised with Pond.DepthU 0.4 to 0.48, "make the pond a little deeper", so a fish on the bed keeps the haze it had); 0.26 until 2026-10-23 ("a little tiny bit" deeper), 0.30 until 2026-10-26 ("make the deep parts a little more visible"), 0.36 until the same evening ("make the water a little more transparent so i can kinda see what's going on down there"), 0.44 until later that evening ("again. make the water more transparent": the fourth ask, so the step is twice the last one, and the painted bed's step was made to show at last, see PondPainter.BedClear): well past the pond's full depth now, so a fish on the bed keeps nearly half of its own colour

    /// <summary>How deep a thing at Z (0 at the surface, 1 at the pond's full depth) is, in u.</summary>
    public static float DepthOf(float z) => z * Pond.DepthU;

    /// <summary>How much smaller a thing at Z is drawn than at the surface (perspective, see EyeU).</summary>
    public static float Scale(float z) => EyeU / (EyeU + 0.75f * DepthOf(z));

    /// <summary>
    /// How lost in the water a thing at Z is: 0 seen clearly, 1 gone (the
    /// murk mixes it toward the water's colour by this much). Some even at
    /// the surface (the water is never glass: a fish right under it still
    /// takes a little of the water's colour, which is half of what says
    /// "under water" rather than "lying on a teal sheet"), then rising with
    /// depth, a touch slowly at first so the top of the water stays clear, to
    /// 1 at SeeU. (Until 2026-10-23 the floor was 0.06 and the curve 1.5: the
    /// topmost fish looked out of the water and the deep ones were lost sooner.)
    /// </summary>
    public static float Murk(float z) => 0.12f + 0.50f * MathF.Pow(Math.Clamp(DepthOf(z) / SeeU, 0, 1), 1.2f);   // ROUND FORTY: the veil tops out at 0.62 instead of 1 ("a little murky down there but not so murky that I can't see the bottom": a thing on the deepest bed keeps six tenths of its own colour, and that colour is what darkens and dulls with depth now, Body.Deepen). (0.14 + 0.86 x^1.3 until 2026-10-29; 1.6 until 2026-10-26 evening, 1.45 until later that evening: the middle depths clear a little more each time the water does)

    /// <summary>How far down the screen a thing at Z is drawn from the water above it, in pixels (see Sink).</summary>
    public static float SinkPx(float z, float u) => DepthOf(z) * u * Sink;

    /// <summary>
    /// How near to the eye a thing at depth Z is, on the sprites' 0 (far) to 255 (near) scale (round forty-one,
    /// the review's depth test): the stones' drawn surface is kept per screen pixel on the same scale (Pond.Near),
    /// and a painter skips a pixel where the stone there is nearer than the thing it paints. The 260 is a margin
    /// of 0.02 of the depth: a thing right on a stone's surface is drawn, not hidden by its own rounding.
    /// </summary>
    public static int Nearness(float z) => Math.Clamp((int)(260 - z * 250), 0, 255);

    /// <summary>
    /// Holds a change of depth to what the animal's own forward motion can
    /// hide, and returns the depth it may have this frame. Depth moves the
    /// picture up and down the screen (SinkPx), so a thing rising faster than
    /// it swims is seen sliding up the screen whichever way its nose points,
    /// and one heading down the screen is seen going backward, tail first.
    /// The allowed rise is half the forward motion with the nose across the
    /// screen, a third of it with the nose straight down it (the nose still
    /// advances two thirds of its speed on the screen, so it never reads as
    /// going backward), all of it with the nose straight up it (the rise only
    /// adds to the way it is going); a sink is the mirror. The same rule for
    /// every swimmer: the koi had it, and the turtle, which did not, was seen
    /// drifting down the screen on a dive (34 backward frames in a 36 s run)
    /// and up it while it floated still.
    /// (Until 2026-10-23 the share with the nose straight down was 0.02, no
    /// rise at all: a fish heading down the screen toward the island's slope
    /// could not rise to clear it, every step onto the bed was refused, and it
    /// hung nose-on against the stone for seconds at a time, on every seed.)
    /// </summary>
    /// <param name="z">The depth now (0 surface, 1 the pond's full depth).</param>
    /// <param name="zNext">The depth it wants this frame.</param>
    /// <param name="heading">Its ground heading, radians (sin is the down-screen part).</param>
    /// <param name="speed">Its forward speed, ground pixels a second.</param>
    public static float HoldDepth(float z, float zNext, float heading, float speed, float dt, float u)
    {
        if (zNext < z)
        {
            float share = MathF.Max(0.35f, 0.5f * (1 - MathF.Sin(heading)));
            return MathF.Max(zNext, z - dt * share * speed / (Pond.DepthU * u * Sink));
        }
        if (zNext > z)
        {
            float share = MathF.Max(0.35f, 0.5f * (1 + MathF.Sin(heading)));
            return MathF.Min(zNext, z + dt * share * speed / (Pond.DepthU * u * Sink));
        }
        return zNext;
    }

    /// <summary>The same numbers, plus the water's squash, for the shared plants (Core/Sakura/Plants.cs).</summary>
    public static readonly Overhead View = new(Ripples.Squash, Lift, ShadowX, ShadowY);
}
