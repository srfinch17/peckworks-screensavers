using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// A koi pond seen from above, under cherry trees in bloom.
///
/// Layers, back to front, the order things really sit in:
///
///   1. The BACKDROP (PondPainter.cs): banks, the pond bed and water, rocks,
///      lily pads, the overhanging branches and their shadows. Painted once.
///   2. Under the water: the KOI (Koi.cs).
///   3. On the water: RIPPLES (Ripples.cs), floating PETALS (Petals.cs), and
///      now and then a swimming SNAKE (Snake.cs).
///   4. In the air: the SHADOWS of the dragonflies and swallows on the water,
///      then the DRAGONFLIES (Dragonflies.cs) and SWALLOWS (Swallows.cs).
///   5. Falling PETALS, nearest of all.
///
/// Everything below the branches is drawn only where no branch is in the
/// way, so the branches stay in front of it.
/// </summary>
internal sealed class SakuraPondScene : IScreensaverScene
{
    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly Pond _pond;
    private readonly bool[] _waterInView;     // open water that no branch hides
    private readonly Ripples _ripples;
    private readonly KoiSchool _koi;
    private readonly Petals _petals;
    private readonly Snake _snake;
    private readonly Dragonflies _dragonflies;
    private readonly Swallows _swallows;

    public SakuraPondScene(int width, int height, SakuraPondSettings settings)
    {
        _pond = PondPainter.Paint(width, height, _rng);
        _waterInView = new bool[_pond.Water.Length];
        for (int i = 0; i < _waterInView.Length; i++) _waterInView[i] = _pond.Water[i] && _pond.Open[i];
        _ripples = new Ripples(_pond, _waterInView);
        _koi = new KoiSchool(_pond, settings.KoiCount, _ripples, _waterInView, _rng);
        _petals = new Petals(_pond, _ripples, _waterInView, settings.PetalPercent / 100f, _rng);
        _snake = new Snake(_pond, _ripples, settings.SnakePercent / 100f, _rng);
        _dragonflies = new Dragonflies(_pond, _ripples, settings.DragonflyCount, _rng);
        _swallows = new Swallows(_pond, _ripples, settings.BirdPercent / 100f, _rng);
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)Math.Min(elapsedSeconds, 0.1);
        _koi.Update(dt);
        _petals.Update(dt);
        _snake.Update(dt);
        _dragonflies.Update(dt);
        _swallows.Update(dt);
        _ripples.Update(dt);
    }

    public void Render(FrameBuffer fb)
    {
        Array.Copy(_pond.Pixels, fb.Pixels, fb.Pixels.Length);
        _koi.Draw(fb);
        _ripples.Draw(fb);
        _petals.DrawFloating(fb);
        _snake.Draw(fb);
        _dragonflies.DrawShadows(fb);
        _swallows.DrawShadows(fb);
        _dragonflies.Draw(fb);
        _swallows.Draw(fb);
        _petals.DrawFalling(fb);
    }

    public void Dispose() { }
}
