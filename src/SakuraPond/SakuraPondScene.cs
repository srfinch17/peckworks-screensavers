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
///   2. Under the water: the KOI (Koi.cs), each with a mind of its own (it
///      wanders, rests, goes to look at things, follows a friend, now and then
///      chases one), and just under the surface the
///      TURTLES (Turtle.cs) and the schools of MINNOWS (Minnows.cs).
///   3. On the water: RIPPLES (Ripples.cs), floating PETALS (Petals.cs), and
///      now and then a swimming SNAKE (Snake.cs). Standing in the shallows,
///      the CATTAILS, and on the bank the IRIS clumps (both the shared
///      Clumps), swaying with the lawn's wind.
///   4. In the air: the SHADOWS of the dragonflies and swallows on the water,
///      then the DRAGONFLIES (Dragonflies.cs) and SWALLOWS (Swallows.cs). The
///      dragonflies play a game with the minnows: tap the water near a
///      school, watch it rush to the rings, fly off, come back later.
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
    private readonly Minnows _minnows;
    private readonly Turtles _turtles;
    private readonly Petals _petals;
    private readonly Snake _snake;
    private readonly Lizards _lizards;
    private readonly Dragonflies _dragonflies;
    private readonly Swallows _swallows;

    public SakuraPondScene(int width, int height, SakuraPondSettings settings)
    {
        _pond = PondPainter.Paint(width, height, _rng);
        float u = _pond.U;
        _waterInView = new bool[_pond.Water.Length];
        for (int i = 0; i < _waterInView.Length; i++) _waterInView[i] = _pond.Water[i] && _pond.Open[i];
        _wind = new Wind(0.26f, u, Light.View);                   // to the right and a little toward us
        _ripples = new Ripples(_pond, _waterInView);
        _lawn = new Lawn(width, height, _pond.Carpet, _pond.Grassy, u, _wind, Light.View, _rng, _pond.Open, _pond.Sun);   // never over a branch; shaded by its shadow

        // The cattails: one clump in the shallows, 0.24u tall before foreshortening, stiff (0.7 of the grass's lean).
        _cattails = new Clumps(_wind, Light.View, width, _pond.Open, _pond.Sun);
        _cattails.Add(_pond.CattailSpot, Flipbook.CattailClump(u * 0.24f, u * 0.036f, Ripples.Squash, _rng), 0.7f, 30);

        // The irises: a fan of sword leaves at each spot the painter chose on the lawn behind the rocks, stiffer still.
        _irises = new Clumps(_wind, Light.View, width, _pond.Open, _pond.Sun);
        foreach ((PointF root, float toward) in _pond.IrisSpots)
            _irises.Add(root, Flipbook.IrisClump(u * 0.11f, toward, _rng), 0.45f, 40);

        _petals = new Petals(_pond, _ripples, _waterInView, settings.PetalPercent / 100f, _wind, _rng);
        _koi = new KoiSchool(_pond, settings.KoiCount, _ripples, _waterInView, _rng, settings.KoiPlayPercent / 100f, _petals);   // the koi nose at and eat the floating petals
        _minnows = new Minnows(_pond, settings.MinnowSchools, _waterInView, _ripples, _rng);
        _turtles = new Turtles(_pond, settings.TurtleCount, _ripples, _waterInView, _rng);
        _snake = new Snake(_pond, _ripples, settings.MinnowSchools > 0 ? _minnows : null, settings.SnakePercent / 100f, _rng);
        _lizards = new Lizards(_pond, settings.LizardCount, _ripples, settings.LizardPercent / 100f, _rng);
        _dragonflies = new Dragonflies(_pond, _ripples, settings.DragonflyCount, _rng, settings.MinnowSchools > 0 ? _minnows : null, _koi);
        _swallows = new Swallows(_pond, _ripples, settings.BirdPercent / 100f, _rng, _koi);   // a skimming swallow's shadow makes the koi flinch
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)Math.Min(elapsedSeconds, 0.1);
        _wind.Update(dt);                 // first: everything below reads it
        _koi.Update(dt);
        PointF[] koiHeads = _koi.Heads;
        _minnows.Update(dt, _snake.Live ? [.. koiHeads, _snake.Head] : koiHeads);   // the minnows keep clear of the snake as of a koi
        _turtles.Update(dt, _snake.Live ? _snake.Head : null);   // a basking turtle slips in when the snake comes near
        _petals.Update(dt);
        // The snake is told where everything that could touch it is: koi heads and
        // middles, turtle noses and shells, the nose of every lizard that is out (its spot
        // from last frame; they are updated after, which is close enough for a touch).
        PointF[] bumpers = [.. koiHeads, .. _turtles.Spots, .. _lizards.Noses];
        _snake.Update(dt, bumpers);
        _lizards.Update(dt, _snake.Live ? _snake.Head : null);   // the lizards run from the snake too
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
        Sections.Mark("lawn");
        _lizards.Draw(fb);
        Sections.Mark("lizard");
        _koi.Draw(fb, _turtles);                  // the koi and the turtles in one depth order (a turtle between the fish deeper and shallower than it)
        Sections.Mark("koi, turtles");
        _minnows.Draw(fb);
        Sections.Mark("minnows");
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
