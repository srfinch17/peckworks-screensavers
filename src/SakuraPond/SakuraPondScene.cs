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
    private readonly Mice _mice;
    private readonly Frogs _frogs;
    private readonly Dragonflies _dragonflies;
    private readonly Swallows _swallows;
    private readonly List<Body> _surface = new();   // what else is solid in the water this frame (the snake, while it swims), refilled per frame, never reallocated

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
        _koi = new KoiSchool(_pond, settings.KoiCount, _ripples, _waterInView, _rng, settings.KoiPlayPercent / 100f, _petals, settings.GoldfishCount);   // the koi (and goldfish) nose at and eat the floating petals
        _petals.Landed = (x, y) => _koi.Notice(x, y, petal: true);                                                                // and the hungry ones come when one settles
        _minnows = new Minnows(_pond, settings.MinnowSchools, _waterInView, _ripples, _rng);
        _turtles = new Turtles(_pond, settings.TurtleCount, _ripples, _waterInView, _rng, _petals);   // a floating turtle nibbles at a petal
        _snake = new Snake(_pond, _ripples, settings.MinnowSchools > 0 ? _minnows : null, settings.SnakePercent / 100f, _rng);
        _dragonflies = new Dragonflies(_pond, _ripples, settings.DragonflyCount, _rng, settings.MinnowSchools > 0 ? _minnows : null, _koi);
        _lizards = new Lizards(_pond, settings.LizardCount, _ripples, settings.LizardPercent / 100f, _rng, _dragonflies);   // a lizard stalks a dragonfly sitting on a rock
        _mice = new Mice(_pond, settings.MouseCount, _ripples, 1f, _rng);   // field mice in the grass: out for a while, scurrying between cover, hiding from the snake, the lizards and shadows
        _swallows = new Swallows(_pond, _ripples, settings.BirdPercent / 100f, _rng, _koi);   // a skimming swallow's shadow makes the koi flinch
        // The frogs come last, with dice of their own, so adding them moved nothing else in the scene (every other animal's
        // placement and every later roll is what it was without them).
        _frogs = new Frogs(_pond, settings.FrogCount, _ripples, _waterInView, new Random(unchecked(_pond.Pads.Count * 7919 + (_pond.Pads.Count > 0 ? (int)_pond.Pads[0].Center.X * 31 + (int)_pond.Pads[0].Center.Y : 0) + width)), _dragonflies);
        _swallows.Shadow = (x, y) => { _lizards.Startle(x, y); _mice.Startle(x, y); _frogs.Startle(x, y); };   // and sends the lizards, mice and frogs bolting for cover
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)Math.Min(elapsedSeconds, 0.1);
        _wind.Update(dt);                 // first: everything below reads it
        // The fish steer round the turtles and the swimming snake, and are pushed out of
        // them if they meet (last frame's spines: near enough). The snake is solid too:
        // before this a koi could rise straight through it.
        _surface.Clear();
        if (_snake.InWater) _surface.Add(_snake);
        _frogs.AddSwimmers(_surface);              // and the frogs that are in the water (last frame's, as the snake's is)
        _koi.Update(dt, _turtles, _surface);
        PointF[] koiHeads = _koi.Heads;
        _minnows.Update(dt, _snake.Live ? [.. koiHeads, _snake.Head] : koiHeads);   // the minnows keep clear of the snake as of a koi
        _turtles.Update(dt, _snake.Live ? _snake.Head : null, _koi.Bodies);   // a basking turtle slips in when the snake comes near; a swimming one steers round the fish
        _petals.Update(dt);
        // Nothing passes through a floating petal: whatever has its back up at
        // the surface (a koi gulping or brushing it, a turtle paddling, the
        // snake) shoves the petals in its way aside. The rest swim under them.
        _koi.PushPetals(_petals);
        _turtles.PushPetals(_petals);
        _snake.PushPetals(_petals);
        _frogs.PushPetals(_petals);
        // The snake is told where everything that could touch it is: koi heads and
        // middles, turtle noses and shells, the nose of every lizard that is out (its spot
        // from last frame; they are updated after, which is close enough for a touch).
        PointF[] turtleSpots = _turtles.Spots;
        PointF[] bumpers = [.. koiHeads, .. turtleSpots, .. _lizards.Noses];
        _snake.Update(dt, bumpers);
        _lizards.Update(dt, _snake.Live ? _snake.Head : null);   // the lizards run from the snake too
        _mice.Update(dt, _snake.Live ? _snake.Head : null, _lizards.Noses);   // the mice run from the snake and the lizards too
        _frogs.Update(dt, _snake.Live ? _snake.Head : null, koiHeads, turtleSpots, _lizards.Noses);   // the frogs leap or dive from all of them
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
        _mice.Draw(fb);
        Sections.Mark("mice");
        _koi.Draw(fb, _turtles);                  // the koi and the turtles in one depth order (a turtle between the fish deeper and shallower than it)
        _frogs.DrawUnder(fb);                     // a frog that has dived is under the surface: over the fish deeper than it, under the minnows
        Sections.Mark("koi, turtles");
        _minnows.Draw(fb);
        Sections.Mark("minnows");
        _ripples.Draw(fb);
        Sections.Mark("ripples");
        _petals.DrawFloating(fb);
        Sections.Mark("petals afloat");
        _snake.Draw(fb);
        _frogs.Draw(fb);                          // frogs at the surface, in the air or on a pad: over the ripples and the floating petals
        _turtles.DrawOut(fb);                     // a turtle up on the island or the log: over the ripples, the floating petals and the snake, which all paint the water under its overhanging shell
        _koi.DrawOut(fb);                         // a koi in the air (a leap) and the spray of its splash: over everything on the water too
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
