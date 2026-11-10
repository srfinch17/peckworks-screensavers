using Peckworks.Screensavers.Core;

namespace Christmas.Stories;

/// <summary>
/// STUB (2026-11-10): Santa brings the sleigh down onto the cabin's roof, goes down the chimney, and flies on.
/// This file is a placeholder with the agreed shape; the real story replaces it.
/// </summary>
internal sealed class SantaDelivery : Story
{
    private readonly ChristmasWorld _w;
    public SantaDelivery(ChristmasWorld w) { _w = w; }
    public override string Name => nameof(SantaDelivery);
    public override string Claims => "santa,cabin,skyshow";
    public override bool CanBegin => false;          // the stub never plays
    public override float Begin(Random rng) => 0.1f;
    public override void Draw(FrameBuffer fb, float t) { }
}
