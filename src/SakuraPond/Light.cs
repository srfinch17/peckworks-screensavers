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
    /// below this is gone until it comes up again. Shallower than the pond's
    /// deepest water (Pond.DepthU), so the middle of the pond really can hide
    /// a fish; deeper than most of the water near the bank, so fish there are
    /// always seen.
    /// </summary>
    public const float SeeU = 0.26f;

    /// <summary>How deep a thing at Z (0 at the surface, 1 at the pond's full depth) is, in u.</summary>
    public static float DepthOf(float z) => z * Pond.DepthU;

    /// <summary>How much smaller a thing at Z is drawn than at the surface (perspective, see EyeU).</summary>
    public static float Scale(float z) => EyeU / (EyeU + 0.75f * DepthOf(z));

    /// <summary>
    /// How lost in the water a thing at Z is: 0 seen clearly, 1 gone (the
    /// murk mixes it toward the water's colour by this much). A little even at
    /// the surface (the water is never glass), then rising with depth, a touch
    /// slowly at first so the top of the water stays clear, to 1 at SeeU.
    /// </summary>
    public static float Murk(float z) => 0.06f + 0.94f * MathF.Pow(Math.Clamp(DepthOf(z) / SeeU, 0, 1), 1.5f);

    /// <summary>How far down the screen a thing at Z is drawn from the water above it, in pixels (see Sink).</summary>
    public static float SinkPx(float z, float u) => DepthOf(z) * u * Sink;

    /// <summary>
    /// Holds a change of depth to what the animal's own forward motion can
    /// hide, and returns the depth it may have this frame. Depth moves the
    /// picture up and down the screen (SinkPx), so a thing rising faster than
    /// it swims is seen sliding up the screen whichever way its nose points,
    /// and one heading down the screen is seen going backward, tail first.
    /// The allowed rise is half the forward motion with the nose across the
    /// screen, none at all with the nose straight down it (every pixel of rise
    /// would be a pixel backward), all of it with the nose straight up it (the
    /// rise only adds to the way it is going); a sink is the mirror. The same
    /// rule for every swimmer: the koi had it, and the turtle, which did not,
    /// was seen drifting down the screen on a dive (34 backward frames in a
    /// 36 s run) and up it while it floated still.
    /// </summary>
    /// <param name="z">The depth now (0 surface, 1 the pond's full depth).</param>
    /// <param name="zNext">The depth it wants this frame.</param>
    /// <param name="heading">Its ground heading, radians (sin is the down-screen part).</param>
    /// <param name="speed">Its forward speed, ground pixels a second.</param>
    public static float HoldDepth(float z, float zNext, float heading, float speed, float dt, float u)
    {
        if (zNext < z)
        {
            float share = MathF.Max(0.02f, 0.5f * (1 - MathF.Sin(heading)));
            return MathF.Max(zNext, z - dt * share * speed / (Pond.DepthU * u * Sink));
        }
        if (zNext > z)
        {
            float share = MathF.Max(0.02f, 0.5f * (1 + MathF.Sin(heading)));
            return MathF.Min(zNext, z + dt * share * speed / (Pond.DepthU * u * Sink));
        }
        return zNext;
    }

    /// <summary>The same numbers, plus the water's squash, for the shared plants (Core/Sakura/Plants.cs).</summary>
    public static readonly Overhead View = new(Ripples.Squash, Lift, ShadowX, ShadowY);
}
