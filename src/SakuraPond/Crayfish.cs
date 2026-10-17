using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// STUB: the crayfish (a helper builds the real one in its own copy of the repo).
/// Now and then a crayfish comes out from under a rock on the bed of the
/// shallows, walks about, feeds, waves its claws, flips backward from a fish,
/// and goes back under.
/// </summary>
internal sealed class Crayfish
{
    public Crayfish(Pond pond, Ripples ripples, float often, Random rng) { }

    /// <summary>Is it out from under its rock (visible or on its way out)?</summary>
    public bool Out => false;

    /// <param name="threats">Screen points of what sends it flipping backward: koi heads and middles, turtle noses and shells, the ducks' heads.</param>
    public void Update(float dt, PointF[] threats) { }

    /// <summary>On the bed: before every fish (everything that swims is above it).</summary>
    public void Draw(FrameBuffer fb) { }
}
