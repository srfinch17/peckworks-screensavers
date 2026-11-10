using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// STUB (2026-11-10): the otters that play on the frozen pond.
/// This file is a placeholder with the agreed shape; the real resident replaces it.
/// </summary>
internal sealed class Otters : Resident
{
    public Otters(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board) { }
    public override void Update(float dt) { }
    public override void Collect(DepthQueue q) { }
    public override void DrawOne(FrameBuffer fb, int index) { }
}
