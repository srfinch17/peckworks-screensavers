using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// STUB (2026-11-10): the reindeer that graze the far side of the snowfield, in front of the pines.
/// This file is a placeholder with the agreed shape; the real resident replaces it.
/// </summary>
internal sealed class ReindeerHerd : Resident
{
    public ReindeerHerd(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board) { }
    public override void Update(float dt) { }
    public override void Collect(DepthQueue q) { }
    public override void DrawOne(FrameBuffer fb, int index) { }
}
