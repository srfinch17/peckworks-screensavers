using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>STUB: not built yet. CanBegin is false, so the director never deals it.</summary>
internal sealed class Pheasant : Happening
{
    public override float Seconds => 0.1f;
    public override bool CanBegin => false;
    public Pheasant(ThanksgivingScenery s) { }
    public override void Draw(FrameBuffer fb, float t) { }
}
