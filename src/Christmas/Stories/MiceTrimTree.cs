using Peckworks.Screensavers.Core;

namespace Christmas.Stories;

/// <summary>
/// STUB (2026-11-10): the mice carry baubles and a star out of the log and trim the tiny fir, which lights up.
/// This file is a placeholder with the agreed shape; the real story replaces it.
/// </summary>
internal sealed class MiceTrimTree : Story
{
    private readonly ChristmasWorld _w;
    public MiceTrimTree(ChristmasWorld w) { _w = w; }
    public override string Name => nameof(MiceTrimTree);
    public override string Claims => "ground,sapling";
    public override bool CanBegin => false;          // the stub never plays
    public override float Begin(Random rng) => 0.1f;
    public override void Draw(FrameBuffer fb, float t) { }
}
