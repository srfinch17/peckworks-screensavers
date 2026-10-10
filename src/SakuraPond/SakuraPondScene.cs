using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// A koi pond seen from above, under cherry trees in bloom.
///
/// Layers, back to front, the order things really sit in:
///
///   1. The BACKDROP (PondPainter.cs, ShorePainter in Shore.cs): the pond bed
///      and water, the corner of land with its lawn, rocks and sand, lily
///      pads, the overhanging branches and their shadows. Painted once. Then
///      the LAWN in the wind (the shared Lawn in Core/Sakura/Plants.cs): the
///      carpet of blades slid by each gust, and the tufts swaying in it.
///   2. Under the water: the KOI (Koi.cs), and just under the surface the
///      TURTLES (Turtle.cs).
///   3. On the water: RIPPLES (Ripples.cs), floating PETALS (Petals.cs), and
///      now and then a swimming SNAKE (Snake.cs). Standing in the shallows,
///      the CATTAILS, and on the bank the IRIS clumps (both the shared
///      Clumps), swaying with the lawn's wind.
///   4. In the air: the SHADOWS of the dragonflies and swallows on the water,
///      then the DRAGONFLIES (Dragonflies.cs) and SWALLOWS (Swallows.cs).
///   5. Falling PETALS, nearest of all.
///
/// Everything below the branches is drawn only where no branch is in the
/// way, so the branches stay in front of it.
///
/// ONE WIND: the lawn, the tufts, the cattails, the irises and the petals
/// all read the same Wind, updated once a frame, so a gust reaches them all
/// as one travelling wave with no plant on a clock of its own.
/// </summary>
internal sealed class SakuraPondScene : IScreensaverScene
{
    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly Pond _pond;
    private readonly bool[] _waterInView;     // open water that no branch hides
    private readonly Wind _wind;
    private readonly Ripples _ripples;
    private readonly Lawn _lawn;
    private readonly Clumps _cattails, _irises;
    private readonly KoiSchool _koi;
    private readonly Turtles _turtles;
    private readonly Petals _petals;
    private readonly Snake _snake;
    private readonly Dragonflies _dragonflies;
    private readonly Swallows _swallows;

    public SakuraPondScene(int width, int height, SakuraPondSettings settings)
    {
        _pond = PondPainter.Paint(width, height, _rng);
        float u = _pond.U;
        _waterInView = new bool[_pond.Water.Length];
        for (int i = 0; i < _waterInView.Length; i++) _waterInView[i] = _pond.Water[i] && _pond.Open[i];
        _wind = new Wind(0.26f, u, Ripples.Squash);              // to the right and a little toward us
        _ripples = new Ripples(_pond, _waterInView);
        _lawn = new Lawn(width, height, _pond.Carpet, _pond.Grassy, u, _wind, Light.View, _rng);

        // The cattails: one clump in the shallows, 0.24u tall before foreshortening, stiff (0.7 of the grass's lean).
        _cattails = new Clumps(_wind, Light.View, _pond.Open);
        float ch = u * 0.24f;
        int cw = (int)(ch * 1.6f);
        _cattails.Add(_pond.CattailSpot, Flipbook.CattailClump(ch, u * 0.036f, Ripples.Squash, _rng), 0.7f, cw, cw, cw / 2, (int)ch, 30);

        // The irises: a fan of sword leaves at each spot the painter chose on the lawn behind the rocks, stiffer still.
        _irises = new Clumps(_wind, Light.View, _pond.Open);
        float ih = u * 0.11f;
        foreach ((PointF root, float toward) in _pond.IrisSpots)
            _irises.Add(root, Flipbook.IrisClump(ih, toward, _rng), 0.45f, (int)(ih * 2.6f), (int)(ih * 2f), (int)(ih * 1.3f), (int)ih, 40);

        _koi = new KoiSchool(_pond, settings.KoiCount, _ripples, _waterInView, _rng);
        _turtles = new Turtles(_pond, settings.TurtleCount, _ripples, _waterInView, _rng);
        _petals = new Petals(_pond, _ripples, _waterInView, settings.PetalPercent / 100f, _wind, _rng);
        _snake = new Snake(_pond, _ripples, settings.SnakePercent / 100f, _rng);
        _dragonflies = new Dragonflies(_pond, _ripples, settings.DragonflyCount, _rng);
        _swallows = new Swallows(_pond, _ripples, settings.BirdPercent / 100f, _rng);
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)Math.Min(elapsedSeconds, 0.1);
        _wind.Update(dt);                 // first: everything below reads it
        _koi.Update(dt);
        _turtles.Update(dt);
        _petals.Update(dt);
        _snake.Update(dt);
        _dragonflies.Update(dt);
        _swallows.Update(dt);
        _ripples.Update(dt);
    }

    // The Sections marks cost nothing unless PECKWORKS_SECTIONS names a
    // file; then a snapshot run writes what each part of the frame costs.
    public void Render(FrameBuffer fb)
    {
        Sections.Begin();
        fb.Restore(_pond.Pixels);
        Sections.Mark("backdrop copy");
        _lawn.Draw(fb);
        Sections.Mark("lawn: tufts");
        _koi.Draw(fb);
        Sections.Mark("koi");
        _turtles.Draw(fb);
        Sections.Mark("turtles");
        _ripples.Draw(fb);
        Sections.Mark("ripples");
        _petals.DrawFloating(fb);
        Sections.Mark("petals afloat");
        _snake.Draw(fb);
        Sections.Mark("snake");
        _cattails.Draw(fb);
        _irises.Draw(fb);
        Sections.Mark("cattails, irises");
        _dragonflies.DrawShadows(fb);
        _swallows.DrawShadows(fb);
        _dragonflies.Draw(fb);
        _swallows.Draw(fb);
        Sections.Mark("flyers");
        _petals.DrawFalling(fb);
        Sections.Mark("petals falling");
    }

    public void Dispose() { }
}
