using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>STUB: not built yet. CanBegin is false, so the director never deals it.</summary>
internal sealed class Squirrel : Happening
{
    public override float Seconds => 0.1f;
    public override bool CanBegin => false;
    public Squirrel(ThanksgivingScenery s) { }
    public override void Draw(FrameBuffer fb, float t) { }
}
