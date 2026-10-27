using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The freshwater prawns (STUB: the public surface the scene is wired to; the
/// animals themselves are built in a copy of the repo and merged back here).
/// A few small, see-through shrimp that live on the bed of the shallows, walk,
/// feed, and flick backward from anything that comes over them.
///
/// WORLD LAW, in the pond's own terms: a prawn is SOLID TO NOTHING. It lives
/// on the bed under everything that swims (a fish is kept a margin of water
/// above the bed by its own rules, so a fish passes OVER it), under the lily
/// pads and the petals. It reacts instead: a fish, a turtle or the crayfish
/// near it sends it flicking backward. The things that stand on the bed
/// (rocks, the island, the formation, the cattails, the bank) are walls to it.
/// </summary>
internal sealed class Prawns
{
    /// <param name="count">How many prawns (the "Prawns" knob). 0 = none, and every call returns at once.</param>
    /// <param name="waterInView">Open water that no branch hides: where a prawn may be drawn.</param>
    /// <param name="rng">The prawns' OWN dice (seeded from the scene with a salt of their own), so adding them moved nothing else.</param>
    public Prawns(Pond pond, Ripples ripples, int count, bool[] waterInView, Random rng)
    {
    }

    /// <param name="threats">Screen points of what sends a prawn flicking backward: koi heads and middles, turtle noses and shells, the ducks' heads (the crayfish's list).</param>
    public void Update(float dt, PointF[] threats)
    {
    }

    /// <summary>On the bed of the shallows: before every fish (everything that swims is above it).</summary>
    public void Draw(FrameBuffer fb)
    {
    }

    /// <summary>Each prawn's middle, screen pixels, for the instruments (and for anything that might one day hunt them). Empty while none is out.</summary>
    public PointF[] Spots => [];
}
