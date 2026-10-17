using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// STUB: the ducks (a helper builds the real one in its own copy of the repo).
/// Now and then a duck swims by, often a mother with a line of ducklings,
/// of one of several species; the ducklings play.
/// </summary>
internal sealed class Ducks
{
    private static readonly PointF[] NoHeads = [];

    public Ducks(Pond pond, Ripples ripples, float often, Random rng, Petals petals, Minnows? minnows) { }

    /// <summary>Is a duck family on the water right now?</summary>
    public bool Visiting => false;

    /// <summary>The head of every duck and duckling on the water, screen pixels (the minnows, tadpoles and frogs keep clear of them). Empty when none.</summary>
    public PointF[] Heads => NoHeads;

    /// <param name="snakeHead">The snake's head if it is out (the mother herds the ducklings away from it), else null.</param>
    public void Update(float dt, PointF? snakeHead) { }

    /// <summary>Adds every duck on the water to the scene's list of what is solid at the surface, without allocating.</summary>
    public void AddSwimmers(List<Body> into) { }

    /// <summary>The ducks shove the floating petals in their way aside.</summary>
    public void PushPetals(Petals petals) { }

    /// <summary>On the water, above everything that swims: after the leaping koi, before the cattails.</summary>
    public void Draw(FrameBuffer fb) { }
}
