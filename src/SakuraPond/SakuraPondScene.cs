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
    private readonly Boughs _boughs;
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
    private readonly Tadpoles _tadpoles;
    private readonly Crayfish _crayfish;
    private readonly Prawns _prawns;
    private readonly DeepDwellers _deep;
    private readonly Ducks _ducks;
    private readonly Dragonflies _dragonflies;
    private readonly Swallows _swallows;
    private readonly Sunlight _sun;               // the beams through the boughs' gaps, their pools on the bed, the glitter on the surface (2026-10-30)
    private readonly Pads _pads;                  // the lily pads, live: stamped on the water, shoved by what swims into them, held by their roots (round forty)
    private readonly List<Body> _surface = new();   // what else is solid in the water this frame (the snake, while it swims), refilled per frame, never reallocated

    public SakuraPondScene(int width, int height, SakuraPondSettings settings)
    {
        // How long the scene takes to build, part by part, when PECKWORKS_STARTLOG names a file
        // (free otherwise): the painter, the boughs, the lawn's pages. A screensaver that takes
        // seconds to appear is a screensaver that looks broken, so a build-time cost is a cost.
        var startClock = System.Diagnostics.Stopwatch.StartNew();
        string? startLog = Environment.GetEnvironmentVariable("PECKWORKS_STARTLOG");
        void Lap(string what) { if (!string.IsNullOrEmpty(startLog)) File.AppendAllText(startLog, $"{what} {startClock.Elapsed.TotalMilliseconds:F0} ms\r\n"); }
        _pond = PondPainter.Paint(width, height, _rng, settings.FallenLog);
        Lap("painter");
        // The masks the animals steer by, as pictures, when PECKWORKS_MASKDUMP names a folder (free otherwise):
        // an instrument. A minnow that circled for ever in a pocket between the formation's caps (2026-10-27)
        // was found by flood-filling the room grid from the open water and listing what the fill never reached.
        string? maskDump = Environment.GetEnvironmentVariable("PECKWORKS_MASKDUMP");
        if (!string.IsNullOrEmpty(maskDump)) DumpMasks(maskDump);
        Lap(_pond.Island is Island isle ? $"island at {isle.Centre.X:F0},{isle.Centre.Y:F0} r {isle.Radius:F0} cap {isle.Cap.X:F0},{isle.Cap.Y:F0} r {isle.CapRadius:F0}" : "island none");   // (the island too: a reader crops it by these numbers)
        Lap(_pond.Formation is Formation fm ? $"formation at {fm.Stone.Centre.X:F0},{fm.Stone.Centre.Y:F0} r {fm.Stone.Radius:F0} caps {fm.Caps.Length}" : "formation none");   // (a placement is counted, never assumed)
        Lap(_pond.Formation2 is Formation fm2 ? $"formation2 at {fm2.Stone.Centre.X:F0},{fm2.Stone.Centre.Y:F0} r {fm2.Stone.Radius:F0} caps {fm2.Caps.Length}" : "formation2 none");
        Lap($"shore band {_pond.Shore.Count} px, reach {_pond.Shore.Reach:F1} px");                                                                                                   // (the lapping edge is counted too)
        if (!string.IsNullOrEmpty(startLog))                                                                                                                                       // (and the pads' roots and sizes, round forty: a reader finds a koi crossing a pad by them)
        {
            var padLine = new System.Text.StringBuilder("pads");
            foreach (LilyPad pad in _pond.Pads) padLine.Append(FormattableString.Invariant($" {pad.Anchor.X:F0},{pad.Anchor.Y:F0},{pad.Radius:F0}"));
            Lap(padLine.ToString());
        }
        float u = _pond.U;
        // The dice salt every late-comer shares (the frogs, tadpoles, crayfish, ducks, sunlight each take their own Random
        // from it with a different offset), so adding any of them moves nothing else in the scene.
        int salt = unchecked(_pond.Pads.Count * 7919 + (_pond.Pads.Count > 0 ? (int)_pond.Pads[0].Center.X * 31 + (int)_pond.Pads[0].Center.Y : 0) + width);
        // The sunlight's beams are baked into the backdrop here, BEFORE the boughs bake their still parts over it (a bough
        // is over the water, a beam is in it).
        _sun = new Sunlight(_pond, settings.SunlightPercent / 100f, new Random(unchecked(salt + 606)));
        Lap($"sunlight gaps {_sun.Gaps}{_sun.Where}");
        _wind = new Wind(0.26f, u, Light.View);                   // to the right and a little toward us
        // The cherry boughs sway in that wind: full sway 0.7u out from a trunk, the tips swinging 0.018u at a full lean.
        _boughs = new Boughs(width, height, _pond.Branches, _pond.TreeRoots, u * 0.7f, u * 0.018f, _wind, Light.View, _pond.Pixels);   // the still parts go into the backdrop here
        _boughs.Uncover(_pond.Open);                               // and under the swaying parts the water is open: they cover whatever swims there themselves, each frame, wherever they have swayed to (round forty)
        Lap($"boughs (live pixels {_boughs.LivePixels}, tile pixels {_boughs.TilePixels})");
        _waterInView = new bool[_pond.Water.Length];
        for (int i = 0; i < _waterInView.Length; i++) _waterInView[i] = _pond.Water[i] && _pond.Open[i];
        _sun.PlaceGlints(_waterInView);
        Lap($"sunlight glint sites {_sun.GlintSites}");
        _ripples = new Ripples(_pond, _waterInView);
        _pads = new Pads(_pond, _pond.Open);                       // (no dice of its own: nothing else moves; the pads at rest are laid over the backdrop once, here)
        _lawn = new Lawn(width, height, _pond.Carpet, _pond.Grassy, u, _wind, Light.View, _rng, _pond.Open, _pond.Sun);   // never over a branch; shaded by its shadow
        Lap("lawn");

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
        _minnows = new Minnows(_pond, settings.MinnowSchools, _waterInView, _ripples, _rng);   // (the deep stencil too: a school that dips to the bed is drawn through it)
        _koi.Minnows = _minnows;   // the big koi may rush a school of minnows for the fun of it
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
        // The tadpoles, the crayfish and the ducks each take dice of their own (seeded from the scene, with a
        // different salt each), for the same reason as the frogs: nothing else in the scene moves when they are added.
        _tadpoles = new Tadpoles(_pond, settings.TadpoleClutches, _ripples, _waterInView, HeapFree(_waterInView), new Random(unchecked(salt + 101)), _petals);
        _crayfish = new Crayfish(_pond, _ripples, settings.CrayfishPercent / 100f, new Random(unchecked(salt + 202)));
        _prawns = new Prawns(_pond, _ripples, settings.PrawnCount, _waterInView, new Random(unchecked(salt + 404)));   // the prawns too: their own dice
        // The crayfish and long-armed prawns of the DEEP bed (round thirty-seven), their own dice as well; they take a
        // minnow of a school that dips down to them, so they are handed the schools.
        _deep = new DeepDwellers(_pond, settings.DeepDwellerCount, _waterInView, settings.MinnowSchools > 0 ? _minnows : null, new Random(unchecked(salt + 505)));
        Lap(_pond.Sunken is SunkenLog sl
            ? FormattableString.Invariant($"sunken log {sl.Centre.X:F0},{sl.Centre.Y:F0} angle {sl.Angle:F2} len {sl.Length:F0} wide {sl.Width:F0} den {sl.Den.X:F0},{sl.Den.Y:F0} homed {_deep.DenHomed}")
            : "sunken log none");
        // Who may eat a tadpole or froglet it happens upon (Snatch in Tadpoles.cs): nothing goes looking for them.
        _koi.Prey = _tadpoles;
        _turtles.Prey = _tadpoles;
        _koi.PrawnPrey = _prawns;                  // and a hungry koi, or the turtle, may take a prawn it happens upon (2026-10-27)
        _turtles.PrawnPrey = _prawns;
        _koi.DeepPrey = _deep;                     // and a dweller of the deep bed: one of the largest koi down there, a diving turtle, a diving snake (round thirty-seven)
        _turtles.DeepPrey = _deep;
        _snake.DeepPrey = _deep;
        Minnows.DipNear = _deep.Count > 0 ? _deep.AnySpot : null;   // and half the schools' dips to the bed go down near one of them (the claws' chance at a minnow)
        _frogs.Prey = _tadpoles;
        _crayfish.Prey = _tadpoles;
        _snake.Prey = _tadpoles;                   // and the swimming snake, when a tadpole is right at its nose
        _ducks = new Ducks(_pond, _ripples, settings.DuckPercent / 100f, new Random(unchecked(salt + 303)), _petals, settings.MinnowSchools > 0 ? _minnows : null, _waterInView);
        _ducks.Prey = _tadpoles;                   // a duck that happens to paddle past the clutch may single one out (Snatch in Tadpoles.cs)
    }

    /// <summary>
    /// The in-view water less the ZONE of the petals heaped against the bank: every heap pixel widened to the cell
    /// round it (cells of 0.012u, a petal and a half), so the scatter of petals becomes one patch with no gaps. The
    /// tadpoles live by this one: a clutch under the heap could not be seen (the heap is painted back over everything
    /// under the water each frame), and a stencil of the petals' own pixels would have refused a tadpole's steps at
    /// every single petal and cut its body at the gaps.
    /// </summary>
    private bool[] HeapFree(bool[] inView)
    {
        int w = _pond.Width, h = _pond.Height, cell = Math.Max(2, (int)(_pond.U * 0.012f));
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        var hot = new bool[gw * gh];
        foreach (int i in _pond.Heap) hot[(i / w / cell) * gw + (i % w) / cell] = true;
        var free = (bool[])inView.Clone();
        for (int cy = 0; cy < gh; cy++)
            for (int cx = 0; cx < gw; cx++)
            {
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && ny >= 0 && nx < gw && ny < gh && hot[ny * gw + nx]) near = true;
                    }
                if (!near) continue;
                for (int y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                    for (int x = cx * cell; x < Math.Min(w, cx * cell + cell); x++) free[y * w + x] = false;
            }
        // Nor where the lapping water ever leaves the bed bare (Shoreline): a tadpole settled there would lie on wet sand.
        for (int i = 0; i < free.Length; i++) if (free[i] && _pond.Shore.EverDry(i)) free[i] = false;
        // Nor under a lily pad (round forty: the pads are a live layer and the water mask runs on under them now, so a
        // clutch could otherwise be born under one, out of sight). A tadpole may still swim under a pad on its way.
        foreach (LilyPad pad in _pond.Pads)
        {
            float r = pad.Radius * 1.25f;
            int x0 = Math.Max(0, (int)(pad.Anchor.X - r)), x1 = Math.Min(w - 1, (int)(pad.Anchor.X + r));
            int y0 = Math.Max(0, (int)(pad.Anchor.Y - r)), y1 = Math.Min(h - 1, (int)(pad.Anchor.Y + r));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - pad.Anchor.X, dy = (y - pad.Anchor.Y) / 0.82f;
                    if (dx * dx + dy * dy <= r * r) free[y * w + x] = false;
                }
        }
        return free;
    }

    // The threat lists handed round each frame, kept from frame to frame and only re-made when their length changes
    // (see Scratch.Gather at the end of this file).
    private PointF[] _scary = [], _bumpers = [], _frogThreats = [], _bedThreats = [], _tadThreats = [], _deepThreats = [];

    public void Update(double elapsedSeconds)
    {
        float dt = (float)Math.Min(elapsedSeconds, 0.1);
        Sections.Lap();                   // the laps of Update, named "update: ..." in the section table (free unless PECKWORKS_SECTIONS is set)
        _wind.Update(dt);                 // first: everything below reads it
        _boughs.Update(dt);
        _pond.Shore.Update(dt, _ripples); // the water's edge moves (and a ring reaching the sand washes up it): the drinkers below ask it where the water is
        Sections.Mark("update: wind");
        // The fish steer round the turtles and the swimming snake, and are pushed out of
        // them if they meet (last frame's spines: near enough). The snake is solid too:
        // before this a koi could rise straight through it.
        _surface.Clear();
        if (_snake.InWater) _surface.Add(_snake);
        _frogs.AddSwimmers(_surface);              // and the frogs that are in the water (last frame's, as the snake's is)
        _ducks.AddSwimmers(_surface);              // and the ducks: a koi rising under one is pushed back down and steers round it
        _koi.Update(dt, _turtles, _surface);
        Sections.Mark("update: koi");
        PointF[] koiHeads = _koi.Heads;
        PointF[] duckHeads = _ducks.Heads;
        // Everything small flees the same list: koi near the surface, the snake's head, the ducks' heads (a duck eats minnows and tadpoles).
        PointF[] scary = Scratch.Gather(ref _scary, koiHeads, duckHeads, one: _snake.Live ? _snake.Head : null);
        _minnows.Update(dt, scary, _koi.BedHeads); // the minnows keep clear of the snake and the ducks as of a koi; a school down on the bed, of every koi whatever its depth
        Sections.Mark("update: minnows");
        _turtles.Update(dt, _snake.Live ? _snake.Head : null, _koi.Bodies);   // a basking turtle slips in when the snake comes near; a swimming one steers round the fish
        Sections.Mark("update: turtles");
        _petals.Update(dt);
        // Nothing passes through a floating petal: whatever has its back up at
        // the surface (a koi gulping or brushing it, a turtle paddling, the
        // snake) shoves the petals in its way aside. The rest swim under them.
        _koi.PushPetals(_petals);
        _turtles.PushPetals(_petals);
        _snake.PushPetals(_petals);
        _frogs.PushPetals(_petals);
        _ducks.PushPetals(_petals);
        _tadpoles.PushPetals(_petals);             // only a froglet swimming off at the surface; a tadpole is under them
        Sections.Mark("update: petals, pushes");
        // The snake is told where everything that could touch it is: koi heads and
        // middles, turtle noses and shells, the nose of every lizard that is out (its spot
        // from last frame; they are updated after, which is close enough for a touch).
        PointF[] turtleSpots = _turtles.Spots;
        PointF[] duckBeads = _ducks.Beads;                     // every other point along every duck and duckling on the picture (empty when none)
        PointF[] bumpers = Scratch.Gather(ref _bumpers, koiHeads, turtleSpots, _lizards.Noses, duckBeads);   // a touch from any of them startles the snake
        _snake.Avoid(duckBeads, _pond.U * 0.02f);             // and it swims round the ducks as it does round the rocks (nothing moves a duck)
        _snake.Update(dt, bumpers, _lizards, _mice);   // and who it may meet on the bank: it can stalk and eat a lizard or a mouse
        Sections.Mark("update: snake");
        _lizards.Update(dt, _snake.Live ? _snake.Head : null);   // the lizards run from the snake too
        _mice.Update(dt, _snake.Live ? _snake.Head : null, _lizards.Noses);   // the mice run from the snake and the lizards too
        Sections.Mark("update: lizards, mice");
        _frogs.Update(dt, _snake.Live ? _snake.Head : null, duckBeads.Length > 0 ? Scratch.Gather(ref _frogThreats, koiHeads, duckBeads) : koiHeads, turtleSpots, _lizards.Noses,
                      _snake.Live ? _snake : null, _turtles.All, _surface);   // the frogs leap or dive from all of them (any part of a duck counts as a koi's head: a swimming frog dives from it and never lands on one); and a swimming frog is pushed clear of the snake, the ducks and the turtles, and flees any part of the snake
        PointF[] bedThreats = Scratch.Gather(ref _bedThreats, _koi.BedHeads, turtleSpots, duckHeads);   // every koi whatever its depth: the fish passing over the shallows are the ones the bed animals must fear
        PointF[] tadThreats = bedThreats;
        if (_snake.Live || _crayfish.Spot != null)   // plus the snake's head, and the crayfish (the one animal that eats them)
            tadThreats = Scratch.Gather(ref _tadThreats, bedThreats, one: _snake.Live ? _snake.Head : null, two: _crayfish.Spot);
        _tadpoles.Update(dt, tadThreats);                                              // the tadpoles scatter from all of them
        Sections.Mark("update: frogs, tadpoles");
        _crayfish.Update(dt, bedThreats);                                                 // the crayfish flips backward from a fish or a turtle over it
        _ducks.Update(dt, _snake.Live ? _snake.Head : null, _turtles.All, _surface);   // the ducks feel round the turtle's shell, the snake's body and the swimming frogs
        _dragonflies.Update(dt);
        _swallows.Update(dt);
        _ripples.Update(dt);
        _prawns.Update(dt, bedThreats);                                                   // the prawns flick backward from the same things the crayfish does
        _deep.Update(dt, _snake.Live && _snake.Deep ? Scratch.Gather(ref _deepThreats, bedThreats, one: _snake.Head) : bedThreats);   // the deep dwellers too, and from the diving snake's head
        _pads.Update(dt, _surface, _koi.Bodies, _turtles.All);                              // last: the pads are shoved by where everything at the surface IS this frame
        _sun.Update(dt);
        Sections.Mark("update: crayfish to ripples");
    }

    // The Sections marks cost nothing unless PECKWORKS_SECTIONS names a
    // file; then a snapshot run writes what each part of the frame costs.
    /// <summary>
    /// Writes the steering masks as grey pictures (PGM, one byte a pixel: a format any script reads
    /// with no library) into a folder: Water, Open and Land as 0 or 255, the stones' nearness (near) and depth (stonez), and the room grid
    /// sampled at every pixel (RoomAt, pixels of water around that spot, capped at 255). An instrument
    /// for PECKWORKS_MASKDUMP, never on in a real run.
    /// </summary>
    private void DumpMasks(string folder)
    {
        Directory.CreateDirectory(folder);
        int w = _pond.Width, h = _pond.Height;
        void Pgm(string name, Func<int, int, byte> at)
        {
            using var f = File.Create(Path.Combine(folder, name + ".pgm"));
            byte[] head = System.Text.Encoding.ASCII.GetBytes($"P5\n{w} {h}\n255\n");
            f.Write(head, 0, head.Length);
            byte[] row = new byte[w];
            for (int y = 0; y < h; y++) { for (int x = 0; x < w; x++) row[x] = at(x, y); f.Write(row, 0, w); }
        }
        Pgm("water", (x, y) => _pond.Water[y * w + x] ? (byte)255 : (byte)0);
        Pgm("open", (x, y) => _pond.Open[y * w + x] ? (byte)255 : (byte)0);
        Pgm("land", (x, y) => _pond.Land[y * w + x] ? (byte)255 : (byte)0);
        Pgm("near", (x, y) => _pond.Near[y * w + x]);                                        // the drawn surface of the bed and the stones as nearness (0 none, 255 the cap); the bed too since step 3
        Pgm("stonez", (x, y) => _pond.Near[y * w + x] == 0 ? (byte)255 : (byte)(255 - _pond.Near[y * w + x]));   // the same as depth times 250 (the review's instrument, behind.py, reads this)
        Pgm("shoal", (x, y) => _pond.StoneShoal[y * w + x] ? (byte)255 : (byte)0);
        Pgm("room", (x, y) => (byte)Math.Clamp((int)(_pond.RoomAt(x, y) + 0.5f), 0, 255));
        Pgm("roomkoi", (x, y) => (byte)Math.Clamp((int)(_pond.KoiRoomAt(x, y) + 0.5f), 0, 255));   // the koi's grid, the stones' shoulders as walls (round thirty-nine)
        Pgm("floor", (x, y) => _pond.Floor[y * w + x]);    // the bed the swimmers feel, 0 at the edge to 255 at full depth (round thirty-eight: the shore's slope and the hollows read off it)
        Pgm("depth", (x, y) => _pond.Depth[y * w + x]);    // the painted depth: the same array as floor since round forty-one's step 3 (one terrain)
    }

    public void Render(FrameBuffer fb)
    {
        Sections.Begin();
        fb.Restore(_pond.Pixels);                 // (not with the lawn's box left out: the carpet reads the frame inside its box, round thirty-five found the smear)
        Sections.Mark("backdrop copy");
        _pond.Shore.Draw(fb);                     // the water lapping at the sand: first, so the lawn's blades overhang it
        _sun.DrawPools(fb);                       // the pools of sunlight on the bed: under every swimmer (the beams themselves are in the backdrop)
        Sections.Mark("shore");
        _lawn.Draw(fb);
        Sections.Mark("lawn");
        _lizards.Draw(fb);
        Sections.Mark("lizard");
        _mice.Draw(fb);
        Sections.Mark("mice");
        _crayfish.Draw(fb);                       // on the bed of the shallows: under every fish
        _prawns.Draw(fb);                         // the prawns, on the same bed: under every fish too
        _deep.Draw(fb);                           // the crayfish and prawns of the deep bed: under every fish (through the deep stencil, so a stone's cap stands in front of them)
        _minnows.DrawDeep(fb);                    // a school that has dipped toward the bed: under every fish (the rest of the minnows are drawn after the fish, below)
        Sections.Mark("crayfish, prawns");
        _tadpoles.DrawUnder(fb);                  // the tadpoles, just over the bed in the shallows, and a froglet deep under the surface: under every fish (drawn after the koi, a clutch sat on the back of a fish passing over it)
        _snake.DrawDeep(fb);                      // a diving snake: under the fish (every koi shallower than it paints over it)
        _koi.Draw(fb, _turtles);                  // the koi and the turtles in one depth order (a turtle between the fish deeper and shallower than it)
        _frogs.DrawUnder(fb);                     // a frog that has dived is under the surface: over the fish deeper than it, under the minnows
        _tadpoles.DrawRising(fb);                 // a froglet in the top of the water: over the fish below it, UNDER the ripples and the floating petals (drawn after the petals, it swam on top of them)
        Sections.Mark("koi, turtles");
        _minnows.Draw(fb);
        _pond.CoverWithHeap(fb);                  // the petals heaped against the bank float on the surface: back over everything under the water (a tadpole under them is hidden, as under the live petals)
        Sections.Mark("minnows");
        _ripples.Draw(fb);
        Sections.Mark("ripples");
        _pads.Draw(fb, _pond.Open);               // the lily pads float on the water: over the ripples and everything under the surface, under the petals that settle on them (round forty)
        _sun.DrawGlints(fb, _waterInView);        // the glitter on the surface: over the water and the pads (none under a pad), under the floating petals
        _petals.DrawFloating(fb);
        Sections.Mark("petals afloat");
        _snake.Draw(fb);
        _frogs.Draw(fb);                          // frogs at the surface, in the air or on a pad: over the ripples and the floating petals
        _tadpoles.Draw(fb);                       // a froglet at the surface, swimming away for good
        _turtles.DrawOut(fb);                     // a turtle up on the island or the log: over the ripples, the floating petals and the snake, which all paint the water under its overhanging shell
        _koi.DrawOut(fb);                         // a koi in the air (a leap) and the spray of its splash: over everything on the water too
        _ducks.Draw(fb);                          // the ducks ride on the water, above all of it; the cattails stand in front of them
        Sections.Mark("snake, ducks");
        _cattails.Draw(fb);
        _irises.Draw(fb);
        Sections.Mark("cattails, irises");
        _dragonflies.DrawShadows(fb);
        _swallows.DrawShadows(fb);
        _dragonflies.Draw(fb);
        _swallows.Draw(fb);
        Sections.Mark("flyers");
        _boughs.Draw(fb);                         // the cherry boughs, swaying: over everything on and under the water; only the petals still in the air above them come after
        Sections.Mark("boughs");
        _petals.DrawFalling(fb);
        Sections.Mark("petals falling");
    }

    public void Dispose() { }
}

/// <summary>
/// Lists of points made afresh each frame without making garbage, and
/// without a first-use compile in the middle of a frame.
///
/// FEYNMAN VERSION: writing a list as [.. a, .. b] (a "collection spread")
/// reads well, but it builds a new array every frame (garbage for the
/// collector to sweep up, a pause now and then), and the first time a given
/// spread runs the runtime compiles, at full strength, the framework's
/// general-purpose code behind it for THAT element type: the shared array
/// pool for PointF took 7 ms on the frame the snake first appeared
/// (2026-10-20). Copying into an array kept from last frame does neither.
/// The array is only remade when the count changes (a duck family arriving
/// or leaving), because the animals that read these lists take plain arrays
/// and read their Length.
/// </summary>
internal static class Scratch
{
    private static readonly List<PointF> List = new(256);

    /// <summary>All of a, b, c and d (any may be null) and the two optional single points, in that order, in an array kept across frames.</summary>
    public static PointF[] Gather(ref PointF[] keep, IEnumerable<PointF>? a, IEnumerable<PointF>? b = null, IEnumerable<PointF>? c = null, IEnumerable<PointF>? d = null, PointF? one = null, PointF? two = null)
    {
        List.Clear();
        if (a != null) List.AddRange(a);
        if (b != null) List.AddRange(b);
        if (c != null) List.AddRange(c);
        if (d != null) List.AddRange(d);
        if (one is PointF p1) List.Add(p1);
        if (two is PointF p2) List.Add(p2);
        return Keep(ref keep, List);
    }

    /// <summary>The list's points in an array kept across frames (remade only when the count changes).</summary>
    public static PointF[] Keep(ref PointF[] keep, List<PointF> from)
    {
        if (keep.Length != from.Count) keep = new PointF[from.Count];
        from.CopyTo(keep);
        return keep;
    }
}

