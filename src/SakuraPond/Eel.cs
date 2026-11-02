using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// STUB (round forty-six, 2026-11-02): a Japanese freshwater eel (unagi) that lives INSIDE the old sunken log's hollow
/// on the deep bed. His line: "the underwater log looks less like a log than it does a log shaped pile of sediment. try
/// that log again. and let animals go inside. maybe some new type of fish or crab or some other freshwater thing I
/// can't think of right now but is super interesting lives in there? I'm trying to create a fun and interesting little
/// ecosystem here." This stub is the public surface the scene is wired to; a helper replaces its insides. Nothing here
/// rolls any dice (the Random handed in is the eel's own, the scene's rolls do not move when it is added or removed).
/// </summary>
internal sealed class Eel
{
    /// <param name="pond">The painted pond: the log (Pond.Sunken), the bed (Floor), the stacked room grid, the depth test (Near).</param>
    /// <param name="view">Which water pixels are in view (under the boughs' still parts is not).</param>
    /// <param name="minnows">The minnow schools, or null: a dipped school is what it hunts (Minnows.Take).</param>
    /// <param name="deep">The deep bed's crayfish and prawns: the dweller homed at the log's mouth shares the hollow, and a prawn is a meal (ISnackField).</param>
    /// <param name="count">The knob "Eel": 0 none, 1 one.</param>
    /// <param name="rng">The eel's OWN Random.</param>
    public Eel(Pond pond, bool[] view, Minnows? minnows, DeepDwellers deep, int count, Random rng)
    {
    }

    /// <summary>Is there an eel at all (the knob is on and the pond has a sunken log with its hollow on open deep bed)?</summary>
    public bool Lives => false;

    /// <summary>Where its head is when any of it is out of the log (ground pixels, for the minnows and the dwellers to fear); null while it is inside or there is no eel.</summary>
    public PointF? Head => null;

    /// <summary>Once a frame. threats: every koi's head, the turtles, the diving snake's head (the same list the deep dwellers get).</summary>
    public void Update(float dt, PointF[] threats)
    {
    }

    /// <summary>On the deep bed: right after the deep dwellers, under every fish, through the depth test (Pond.Near hides what is inside the log).</summary>
    public void DrawDeep(FrameBuffer fb)
    {
    }
}
