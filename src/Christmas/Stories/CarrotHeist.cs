using Peckworks.Screensavers.Core;

namespace Christmas.Stories;

/// <summary>
/// STUB (2026-11-10): a hare steals the snowman's carrot nose, and a bird brings him a new one.
/// This file is a placeholder with the agreed shape; the real story replaces it.
/// </summary>
internal sealed class CarrotHeist : Story
{
    private readonly ChristmasWorld _w;
    public CarrotHeist(ChristmasWorld w) { _w = w; }
    public override string Name => nameof(CarrotHeist);
    public override string Claims => "ground,snowman";
    public override bool CanBegin => false;          // the stub never plays
    public override float Begin(Random rng) => 0.1f;
    public override void Draw(FrameBuffer fb, float t) { }
}
