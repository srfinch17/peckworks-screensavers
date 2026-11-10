using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// STUB (2026-11-10): the cardinals, chickadees and the jay that come to the bird table.
/// This file is a placeholder with the agreed shape; the real resident replaces it.
/// </summary>
internal sealed class FeederBirds : Resident
{
    public FeederBirds(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board) { }
    public override void Update(float dt) { }
    public override void Collect(DepthQueue q) { }
    public override void DrawOne(FrameBuffer fb, int index) { }
}
