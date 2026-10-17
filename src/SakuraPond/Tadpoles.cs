using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// STUB: the tadpoles (a helper builds the real one in its own copy of the repo).
/// A clutch of tadpoles in the shallows, wriggling, feeding, fleeing; one by one
/// they grow legs, lose the tail and swim away as froglets, never to return.
/// </summary>
internal sealed class Tadpoles
{
    public Tadpoles(Pond pond, int clutches, Ripples ripples, bool[] where, Random rng, Petals petals) { }

    /// <param name="threats">Screen points of everything a tadpole flees: koi heads and middles, turtle noses and shells, the snake's head, the ducks' heads.</param>
    public void Update(float dt, PointF[] threats) { }

    /// <summary>The tadpoles and any froglet under the surface: after the koi, before the minnows.</summary>
    public void DrawUnder(FrameBuffer fb) { }

    /// <summary>A froglet at the surface, swimming away: after the frogs, before the turtles up on a perch.</summary>
    public void Draw(FrameBuffer fb) { }

    /// <summary>A froglet at the surface shoves the floating petals in its way aside.</summary>
    public void PushPetals(Petals petals) { }
}
