using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The small life of the pumpkin patch (his line: "little mice and squirrels
/// and birds and things running around throughout the patch"): field mice
/// that scurry between the pumpkins, sniff, nibble at them and hide behind
/// them; juncos and sparrows that hop and peck between them, fly up to sit on
/// a pumpkin and look about; and squirrels that bound through, sit up,
/// scratch at the earth, hop up onto a pumpkin to look about, chase each
/// other for the fun of it, and den in the holes up the two big maples.
///
/// FEYNMAN VERSION: each critter has a MIND from the shared kit the pond's
/// animals use (Core/Minds): personality dials rolled once (a timid one
/// dens more, a busy one never sits, a playful one darts and romps), ONE
/// doing at a time with a timer, and weighted dice for what to do next when
/// the timer runs out. The mind only chooses a goal; the steps are the kit's
/// ground rules (Ground.StepRound): the patch is a surface with holes in it
/// (every pumpkin's foot, the shock), and a step into a hole goes round it on
/// whichever side is open. A mouse that a squirrel bounds past bolts (a
/// startle with a cooldown, so it cannot bolt thirty times a second).
///
/// Every critter is stamped against the near ground's depth picture with the
/// nearness of its own feet, so a mouse behind a pumpkin is hidden by it and
/// one in front runs over it: that is the whole of "throughout the patch". A
/// bird or a squirrel on a pumpkin's top is stamped with that pumpkin's
/// nearness plus one. They are drawn far to near.
///
/// THE SQUIRRELS (2026-11-06 afternoon, his lines: "squirrels get hung up on
/// pumpkins", "fluidity in animation is great", "the abrupt flipping when
/// changing direction is jarring. maybe make it curl as it flips", "give them
/// some variations and personalities", "slightly bigger trees with holes they
/// can hide in", "a bit more playful"): a squirrel never hides behind a
/// pumpkin any more (that was the hang-up: a tail sticking out of a pumpkin
/// for fifteen seconds); it hides in a TREE, up the trunk and into the hole.
/// A reversal on the move is a CURL (SquirrelArt.Curl: the front end plants,
/// the back end comes round it, the tail whips over) that keeps its momentum;
/// a reversal from a standstill is the sitting turn, whose middle page is now
/// mirror-symmetric so the flip is invisible. Each squirrel has a coat (red,
/// grey, fox), a size, and four dials.
/// </summary>
internal sealed class PatchLife
{
    private enum Kind { Mouse, Bird, Squirrel }
    // Fetch: going to a loose nut. Carry: taking a nut somewhere to bury it. Bury: digging it in. Eat: sitting up with it,
    // chewing. Raid: going to another squirrel's stash (then Dig to open it). Chase: the stash's owner running at the
    // thief. Leave: a visiting squirrel bounding off the way it came. Mount: pitching up onto the bark at a trunk's foot.
    // Climb: up the trunk and into the hole. Den: inside it, out of sight. Descend: head out, then head-first down.
    // Dismount: pitching off the bark onto the ground. Romp: a playful squirrel running at another for the game of it.
    // HopUp: the hop onto a pumpkin. Perch: sitting on top of it. HopDown: the hop off.
    // Branch: a bird sitting up on a corner oak branch (2026-11-09, his ask: "birds should land up there sometimes"). Its flights
    // up to it and down from it are Fly with Critter.Flight set (1 climbing to the branch, 2 dropping off it).
    private enum Doing { Go, Still, Nibble, Hide, Peck, Fly, Perch, Dig, Bolt, Fetch, Carry, Bury, Eat, Raid, Chase, Leave, Mount, Climb, Enter, Den, Exit, Descend, Dismount, Romp, HopUp, HopDown, Branch }

    private sealed class Critter
    {
        public Kind Kind;
        public int Id;                              // its number at birth (the list is sorted by depth to draw, so an index is not a name)
        public int Look;                            // which bird; which squirrel coat and size (SqVariant)
        public float X, Y;                          // the feet, pixels
        public float Heading;                       // radians, screen (y down)
        public int Dir = 1;
        public float Timid, Busy, Curious, Playful; // the dials
        public Mind<Doing> Mind = new(Doing.Still);
        public Attention Senses = null!;            // what it makes of the noticeboard (Senses.cs)
        public Pace Pace = null!;                   // its speed, eased (Mover.cs): a mouse gathers speed over a few steps, it does not switch on
        public float NextPost;                      // a squirrel on the move tells the board every so often
        public PointF Goal;
        public float Travelled;                     // for the page
        public float Clock;
        public int Perched = -1;                    // the pumpkin it sits on (a bird, or a squirrel on its top)
        public float FlyFrom, FlyTo, FlyY0, FlyY1, FlyFor;   // a flight's arc; a squirrel's hop onto or off a pumpkin
        public int Refused;                         // frames in a row the ground refused
        public bool WasDown;                        // a squirrel: on all fours last frame (running, hiding, digging), or sitting up
        public bool Turning;                        // a squirrel coming round to face the other way (never a mirror flip)
        public bool Curl;                           // ...on the move: the running curl (keeps its momentum), else the sitting turn
        public float TurnT;                         // seconds into that
        public float TurnPre, TurnPost;             // a sitting turn's rise to the sit before the turn pages, and its sink to the crouch after: each TurnRise or 0 (a turn that starts from a sit has no rise; one whose next doing is sitting has no sink). The old fixed rise dipped a sitting squirrel into the crouch for three frames before it turned.
        public float TurnV;                         // the speed it went into a curl at
        public float Blend;                         // seconds left of its blend through the crouch pages between the two
        public bool Sinking;                        // the blend's direction: sinking from the sit into the run, or rising out of it
        public NutField.Nut? Nut;                   // the nut a squirrel is carrying (in its mouth on the move, in its paws sitting)
        public NutField.Nut? Target;                // the nut it is going for: a loose one to fetch, or a stash to raid
        public int ChaseId = -1;                    // the thief an owner is running at, or the playmate a romp runs at (a critter's Id)
        public int Legs;                            // a romp: how many more times the playmate is made to bolt before the game ends
        public bool Visitor;                        // a squirrel that came in for the frenzy and leaves when the nuts are gone
        public int EntrySide;                       // which edge a visitor came in from (-1 left, +1 right)
        public bool Done;                           // a visitor that has left: dropped from the list after the frame
        public float PauseAt;                       // a squirrel's planned stop-start: the Travelled at which it sits up for a moment mid-run (0 none)
        public PointF? Resume;                      // the goal it goes on to after that pause
        public PointF? Via;                         // a squirrel: the waypoint on the way to a steep goal (PlanVia), null when the goal is reached straight
        public int Curls;                           // curls since the goal was set: two drop the waypoint, a third ends the doing (a squirrel that curled back and forth on the spot for minutes, the nemesis's finding)
        public PointF ViaFor;                       // the goal that waypoint was planned for (a new goal plans again)
        public bool Dart;                           // this run ends in a curl straight back the other way, at speed (playful)
        public int Tree = -1;                       // the tree a Go is heading for (to den), and the one it is on
        public int ToPerch = -1;                    // the pumpkin a Go is heading for (to hop up on)
        public float Up;                            // how far up the trunk, pixels
        public float NextDen;                       // the clock (PatchLife time) before which it does not den again: a den is a rest, not a hiding habit
        public int Fails;                           // goals in a row that could not be reached from here (a pocket): after a few it LEAPS out instead of trying again
        public bool HopAfterTurn;                   // on a pumpkin top, facing the other way first: the hop down follows the turn
        public float Arrived;                       // a visitor: the clock when it came in; its stay has a limit (five squirrels raiding each other's stashes kept a frenzy going for four minutes)
        public PointF? DenFoot;                     // on the way to its tree: the trunk's foot, the second leg of the trip (the first runs along its own row to beside the tree)
        // ---- A bird's branch perch (2026-11-09) ----
        public int Flight;                          // a bird in Fly: 0 an ordinary flight, 1 climbing to a branch, 2 dropping from one
        public int Sub;                             // sitting on a branch: what it is up to (Sub* constants)
        public float SubLeft;                       // seconds left of that
        public float ShufFrom, ShufTo, ShufT;       // a shuffle's hop along the wood: from x, to x, seconds in
        public int ShufFace;                        // the way it will face after the hop (the flip is made at the top of the hop, in the air)
        public float NextTry;                       // after a search for a seat found none: not again before this clock (a threat on the board asks every frame)
        public float NextPerch;                     // the clock before which it does not choose a branch on its own (a visit is a treat, not a habit)
        public float PerchedAt;                     // the clock when it landed on its branch (a long stay is cut short even in a story)
    }
    private const int SubSit = 0, SubPreen = 1, SubPeck = 2, SubShuffle = 3;
    private const float ShuffleFor = 0.28f, FlareFor = 0.30f, TakeOffFor = 0.25f;   // a shuffle hop's length; the last pages of a landing are a flare; the first of a drop are wing beats

    private const float BlendFor = 0.15f;           // a squirrel drops into its run, or sits up out of it, over nine frames (it used to pop in one)
    private const int RisePages = 6, TurnPages = 10, CurlPages = 12, MountPages = 12, HopPages = 22;   // 10 and 12 turn pages over 0.42 s: at 6 and 8 over 0.30 a turn was three frames a page, a card flipped ("the squirrel 2d flipping")   // 12 mount pages over 0.42 s: at 4 over 0.25 the pitch onto the bark was a card flipped up in four jumps ("a 90 degree rotation")
    private const float TurnRise = 0.12f, TurnFor = 0.42f;   // a sitting turn: rise to the sit (0.12 s), the narrowing pages, the flip, the widening (0.42), sink (0.12)
    private const float CurlFor = 0.42f;            // the running curl: in and out again, never stopping
    private const float Standoff = 0.012f;          // how far (in U) a squirrel's hind feet stand off a trunk before it climbs and after it comes down: TreeFor's foot goal, the mount pages' origin and the dismount's landing all use it
    private const float MaxSlope = 0.7f;            // the steepest a squirrel RUNS on the screen: rise over run, 35 degrees. A side-view body moving straight up or down the screen slides ("90 turns"); a steeper goal is reached by a planned waypoint (PlanVia)
    private const float MountFor = 0.42f, HopFor = 0.55f, ClimbSpeed = 0.13f;   // ClimbSpeed in U a second: the climb is seen, not flashed (0.20 was a third of a second)
    private const float StartAt = 4.0f;             // the critters' first frame, seconds: pinned, so a seed is a scenario (their start used to wait on the sheets' side-thread build and drifted 0.4 s between runs)
    private const float ExitFor = 0.4f;                     // a moment inside the leaves before the head comes out (the den trip goes into the CROWN since 2026-11-10, not the hole: see the trunk stencil)

    private struct Sheet { public Sprite[] Pages; public int W, H, Ox, By; }

    /// <summary>A squirrel's coat and size (his "variations"): the sheets are painted once per variant.</summary>
    private sealed class SqSheets
    {
        public float K;                             // pixels to the squirrel unit for this variant
        public Sheet Run, RunNut, Look, Dig, Rise, Turn, Eat, Curl, Hop, Up, Down, MountUp, MountDown;
        public int UpFoot, DownHead;                // in the climbing sheets, how far below the sheet's origin the feet (Up) or the head (Down) actually are: measured off the painted page, so the climb is anchored where the body is
        public int UpHead;                          // in the head-up climbing sheet, how far ABOVE the origin the top of the head is (the first row painted): the entry into the hole begins when that row reaches the hole's bottom rim
        public int UpCol, DownCol, MountUpCol, MountDownCol;   // the painted body's middle column on each climbing sheet (measured): the climb is stamped so that column runs up the hole's middle, so the body goes INTO the hole, not up the bark beside it
        public Sheet[] Sit = new Sheet[4];
    }
    private static readonly (SquirrelArt.Coat Coat, float Scale)[] Variants = [(SquirrelArt.Coat.Red, 1.0f), (SquirrelArt.Coat.Grey, 1.08f), (SquirrelArt.Coat.Fox, 0.94f)];

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;          // PECKWORKS_MINDLOG: every critter's place and doing each frame (the birds' flights are judged from it)
    private double _time;
    private readonly List<Critter> _all = new();
    private readonly float _k, _m, _b;
    private readonly Ground.Solid _ground;
    private readonly NutField _nuts;                // the acorns on the ground (Nuts.cs)
    private readonly bool[] _trunkMask;             // the stencil for a squirrel on the BACK of a trunk: hands off where the trunk's bark is, so the bark hides what is behind it and the tail and feet show past its edges
    private Sheet _mouseRun, _mouseStill;
    private readonly SqSheets[] _sq = new SqSheets[Variants.Length];
    private const int LookPages = 4;                // Look: the head coming up into the look over four pages (page 0 the plain sit, page 3 the full look); it used to snap
    private Sheet[] _bird = new Sheet[2], _birdLook = new Sheet[2], _birdPeck = new Sheet[2], _birdHop = new Sheet[2];
    private Sheet[] _birdFlyKind = new Sheet[2];
    private Sheet[] _birdFlare = new Sheet[2], _birdDive = new Sheet[2], _birdPreen = new Sheet[2];   // the branch perch's poses (BirdArt: Flare 2 pages, Dive 1, Preen 2)
    // The branch perches roll their own dice: a new behaviour must not move the old rolls of every other critter.
    private readonly Random _perchRng;
    // PECKWORKS_PERCH: "now" sends the first bird up to a branch at 5.0 s (pinned to the process clock, so a frame dump lines up with the
    // log); "scare" pins a Threat on the board at 8.0 s over the patch (the birds should go up, and stay up while it is there).
    private readonly string? _perchTest;
    private bool _perchTestDone;
    private volatile bool _ready;

    public PatchLife(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board, NutField nuts)
    {
        _s = s;
        _board = board;
        _nuts = nuts;
        _log = MindLog.Open();
        _rng = new Random(sceneRng.Next());         // its own dice
        _perchRng = new Random(7919 + s.Width);     // the branch perches' own dice (a constant: it takes nothing from the scene's)
        _perchTest = Environment.GetEnvironmentVariable("PECKWORKS_PERCH");
        _k =Math.Max(9f, s.U * 0.030f);   // the near ground's scale: a pumpkin of 0.4 m is about 0.05 U, so a squirrel of 0.25 m is 0.030 U (at 0.055 it was "too big"), a mouse 0.55 of that, a sparrow 0.020
        _m = _k * 0.55f;
        _b = Math.Max(7f, s.U * 0.020f);
        _ground = CanStand;                         // made once: a new delegate every frame is garbage
        _nuts.CanStand = NutCanLie;                 // a nut lands only where a critter could stand AND not up a pumpkin's painted body (a nut buried behind a pumpkin was a tail sticking out of it)
        // The den stencil: true everywhere but the INSIDE of each inner trunk above its hole's bottom rim (the trunk's own
        // width at each height, and the hole's opening). A climbing body drawn over the bark is hidden exactly where it
        // has gone into the tree; nothing beside the trunk is touched. (A flat band across the trunk and the air beside it
        // cut the squirrel along a line: "sliding up through some pocket dimension".)
        // THE TRUNK STENCIL (2026-11-10). The den trip no longer goes into the HOLE (a flat sprite sliding up a flat trunk and
        // vanishing behind a line read as "levitate up into a dimensional portal and disappear", however smooth the seams). A
        // squirrel climbs the way real ones do: it SPIRALS round the trunk, passing BEHIND it (the bark hides it, its tail and
        // feet showing past the edges), and goes up INTO THE LEAVES, which hide it as leaves do. This stencil is the bark: hands
        // off inside the trunk's silhouette for its whole height, from the ground to the crown.
        _trunkMask = new bool[s.Width * s.Height];
        Array.Fill(_trunkMask, true);
        for (int t = 0; t < 2; t++)
        {
            PointF[] trunk = t == 0 ? s.LeftTrunk : s.RightTrunk;
            int y1 = Math.Clamp((int)(trunk[0].Y + s.U * 0.03f), 0, s.Height - 1);
            for (int y = 0; y < y1; y++)
            {
                float cx = TrunkX(t, y), half = TrunkHalf(t, y) * 1.0f;
                int x0 = Math.Max(0, (int)(cx - half)), x1 = Math.Min(s.Width - 1, (int)(cx + half));
                for (int x = x0; x <= x1; x++) _trunkMask[y * s.Width + x] = false;
            }
        }
        int mice = (count + 1) / 2, birds = count / 3, squirrels = Math.Max(0, count - mice - birds);
        if (count >= 4 && squirrels < 2) { squirrels = 2; mice = Math.Max(1, count - birds - 2); }   // two squirrels at least (one alone has no one to romp with); the mice give one up
        int n = mice + birds + squirrels;
        float[] timid = Traits.Deal(_rng, n), busy = Traits.Deal(_rng, n), curious = Traits.Deal(_rng, n), playful = Traits.Deal(_rng, n);
        n = 0;
        for (int i = 0; i < mice; i++) { Add(Kind.Mouse, timid[n], busy[n], curious[n], playful[n], _rng.Next(2)); n++; }
        for (int i = 0; i < birds; i++) { Add(Kind.Bird, timid[n], busy[n], curious[n], playful[n], _rng.Next(2)); n++; }
        for (int i = 0; i < squirrels; i++) { Add(Kind.Squirrel, timid[n], busy[n], curious[n], playful[n], i % Variants.Length); n++; }   // the residents wear different coats
        foreach (Critter c in _all) if (c.Kind == Kind.Bird) c.NextPerch = 10f + 25f * (float)_perchRng.NextDouble();   // not in the first seconds
        _nextId = _all.Count;                       // the residents took 0 to count-1; visitors count on from there
        _test = Environment.GetEnvironmentVariable("PECKWORKS_SQUIRREL");   // test aid: "dart" makes every squirrel playful and every run a dart (the curl within the first seconds); "den" sends them to the trees at once
        if (_test != null) foreach (Critter c in _all) if (c.Kind == Kind.Squirrel) { c.Playful = 1f; c.Timid = 1f; }
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    private void Add(Kind kind, float timid, float busy, float curious, float playful, int look)
    {
        var c = new Critter { Kind = kind, Id = _all.Count, Timid = timid, Busy = busy, Curious = curious, Playful = playful, Look = look, Dir = _rng.Next(2) == 0 ? 1 : -1 };
        PointF at = RandomSpot(0, _s.Width, 40) ?? new PointF(_s.Width * 0.5f, _s.WalkY(_s.Width * 0.5f));
        c.X = at.X; c.Y = at.Y;
        // Small things in the grass feel less of the world (Keen under 1); a mouse is timid by nature, a squirrel bold.
        c.Senses = new Attention(kind == Kind.Mouse ? 0.5f + 0.5f * timid : kind == Kind.Squirrel ? 0.5f * timid : timid, curious) { Keen = kind == Kind.Bird ? 1f : 0.8f };
        c.Pace = new Pace(0.9f * _s.U);             // gathers speed over about a quarter of a second
        c.Mind.Begin(Doing.Still, 0.5f + 3f * (float)_rng.NextDouble());
        _all.Add(c);
    }

    private int _nextId;                            // ids outlive the list (visitors come and go), so a name is never reused for the owner of a stash
    private readonly string? _test;

    /// <summary>
    /// THE FRENZY (his rule: "every so often like 10 nuts will fall at once and a bunch of squirrels will go into a
    /// frenzy to grab them"): the gust has shaken the nuts loose (NutField.Shake), and three or four squirrels come
    /// bounding in from the edges along the walk line to join the patch's own. They are ordinary critters with the
    /// Visitor flag: the same mind, the same dice; and when there is nothing loose left to fetch and nothing in their
    /// mouths, Decide sends them off the way they came (Leave), and the patch is quiet again. Never more than five
    /// squirrels on the ground at once.
    /// </summary>
    public void Frenzy()
    {
        int squirrels = 0;
        foreach (Critter c in _all) if (c.Kind == Kind.Squirrel && !c.Done) squirrels++;
        int bring = Math.Min(4, Math.Max(0, 5 - squirrels));
        float[] timid = Traits.Deal(_rng, Math.Max(1, bring)), busy = Traits.Deal(_rng, Math.Max(1, bring)), curious = Traits.Deal(_rng, Math.Max(1, bring)), playful = Traits.Deal(_rng, Math.Max(1, bring));
        for (int i = 0; i < bring; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            var c = new Critter { Kind = Kind.Squirrel, Id = _nextId++, Timid = timid[i], Busy = busy[i], Curious = curious[i], Playful = playful[i], Look = _rng.Next(Variants.Length), Dir = -side, Visitor = true, EntrySide = side };
            c.X = side < 0 ? -0.018f * _s.Width : 1.018f * _s.Width;        // just off the edge, inside the ground's own margin (CanStand allows 0.02 W): from two lengths out every line in was refused at its first step
            c.Y = _s.WalkY(Math.Clamp(c.X, 0, _s.Width - 1)) + _s.U * (0.01f + 0.04f * (float)_rng.NextDouble());
            c.Senses = new Attention(0.5f * c.Timid, curious[i]) { Keen = 0.8f };
            c.Pace = new Pace(0.9f * _s.U);
            c.Mind.Begin(Doing.Still, 2.5f + 0.7f * i);                      // the nuts take three to five seconds to come down; they arrive as the first ones land, one after another
            c.Arrived = (float)_time;
            _all.Add(c);
        }
        _log?.WriteLine($"{_time,8:F2} frenzy: {bring} squirrels come in");
    }

    // ================================================================ the world

    /// <summary>Can a critter stand here? On the near ground, and not inside a pumpkin's foot or the shock's foot.</summary>
    private bool CanStand(float x, float y)
    {
        float u = _s.U;
        if (x < -0.02f * _s.Width || x > 1.02f * _s.Width) return false;   // the margin is for a visitor at the edge, coming or going; a goal is picked on screen (RandomSpot)
        float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
        if (y < crest + 0.012f * u || y > _s.Height - 0.012f * u) return false;   // the feet stay ON the screen (they used to run 0.04 U below its bottom edge: a squirrel out of sight for a tenth of a run)
        foreach (var (at, r) in _s.Pumpkins)
        {
            float foot = at.Y + 0.8f * r;
            if (MathF.Abs(x - at.X) < 1.05f * r && MathF.Abs(y - foot) < 0.35f * r) return false;
        }
        float walk = _s.WalkY(x);
        if (MathF.Abs(x - _s.CornShockFoot.X) < _s.CornShockHalfWidth + 0.01f * u && MathF.Abs(y - walk) < 0.04f * u) return false;
        return true;
    }

    /// <summary>Where a nut may lie: where a critter can stand, and not inside the painted body of a pumpkin (above its foot, within its width): the depth picture would hide it and the squirrel digging for it.</summary>
    private bool NutCanLie(float x, float y)
    {
        if (!CanStand(x, y)) return false;
        foreach (var (at, r) in _s.Pumpkins)
            if (MathF.Abs(x - at.X) < 1.15f * r && y > at.Y - 1.1f * r && y < at.Y + 0.8f * r) return false;
        return true;
    }

    /// <summary>
    /// A spot a critter can stand on, between x0 and x1; given a row (yNear, within yReach) it keeps to that row, and
    /// given minDx it is at least that far from the range's middle. Given "from" (where the critter stands) the straight
    /// line there must be clear too, tested every two pixels, the way the step probe will test it: a goal straight behind
    /// a pumpkin was refused at the pumpkin (the step rule slides round a thing only when the heading is well off the
    /// goal, and a critter walking straight at its goal has no side to slide to), and one squirrel in a pocket between
    /// pumpkins drew five such goals in a row and sat refused for twelve seconds. Null when nothing can be reached.
    /// </summary>
    /// <summary>
    /// The waypoint for a steep goal: on the goal's row, out to the side the squirrel is going (or facing, if the goal is straight
    /// above or below), far enough that the run to it is no steeper than MaxSlope. If the screen's edge is in the way it goes the
    /// other way; if that spot is not standable it steps back toward the squirrel until one is. Null when the goal is reached
    /// straight (shallow enough, or within a length and a half up or down).
    /// </summary>
    private PointF? PlanVia(Critter c)
    {
        float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
        if (MathF.Abs(dy) <= MaxSlope * MathF.Abs(dx) || MathF.Abs(dy) < 1.5f * _k) return null;
        float run = MathF.Abs(dy) / MaxSlope;
        int side = MathF.Abs(dx) > 0.5f * _k ? MathF.Sign(dx) : c.Dir;
        float vx = Math.Clamp(c.X + side * run, 0.02f * _s.Width, 0.98f * _s.Width);
        if (MathF.Abs(vx - c.X) < 0.6f * run) { side = -side; vx = Math.Clamp(c.X + side * run, 0.02f * _s.Width, 0.98f * _s.Width); }
        // The goal's own row first; if nothing stands there (a pumpkin's foot is inside its keep-out ring, and the goal of a hop
        // up onto one IS its foot), rows a little above and below it, each tried stepping back toward the squirrel.
        foreach (float rowOff in (float[])[0f, 0.6f * _k, -0.6f * _k, 1.2f * _k, -1.2f * _k])
        {
            float vy = c.Goal.Y + rowOff, tx = vx;
            for (int i = 0; i < 8 && !CanStand(tx, vy); i++) tx -= side * 0.4f * _k;
            if (CanStand(tx, vy) && MathF.Abs(tx - c.X) >= 0.5f * run) return new PointF(tx, vy);
        }
        return null;
    }

    private PointF? RandomSpot(float x0, float x1, int tries, float? yNear = null, float yReach = 0f, float minDx = 0f, PointF? from = null)
    {
        float u = _s.U, xm = (x0 + x1) / 2;
        x0 = Math.Max(x0, 0.01f * _s.Width); x1 = Math.Min(x1, 0.99f * _s.Width);   // a goal is on the screen (one squirrel sat at x 1928 of 1920 for four and a half minutes)
        if (x1 <= x0) return null;
        for (int i = 0; i < tries; i++)
        {
            float x = x0 + (x1 - x0) * (float)_rng.NextDouble();
            if (MathF.Abs(x - xm) < minDx) continue;
            float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
            float y = yNear is float yc
                ? Math.Clamp(yc + yReach * (float)(_rng.NextDouble() * 2 - 1), crest + 0.012f * u, _s.Height + 0.04f * u)
                : crest + 0.012f * u + (_s.Height + 0.04f * u - crest - 0.012f * u) * (float)_rng.NextDouble();
            if (!CanStand(x, y)) continue;
            if (from is PointF f && !Ground.LineClear(_ground, f, new PointF(x, y), 2f)) continue;
            return new PointF(x, y);
        }
        return null;
    }

    /// <summary>A spot just past the screen's edge (inside CanStand's margin) with a clear line to it from "from", on the row: for a visitor leaving.</summary>
    private PointF? EdgeSpot(float edge, float yNear, float yReach, PointF from)
    {
        float u = _s.U;
        for (int i = 0; i < 20; i++)
        {
            float x = edge + 0.004f * _s.Width * (float)(_rng.NextDouble() * 2 - 1);
            float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
            float y = Math.Clamp(yNear + yReach * (float)(_rng.NextDouble() * 2 - 1), crest + 0.012f * u, _s.Height - 0.012f * u);
            if (CanStand(x, y) && Ground.LineClear(_ground, from, new PointF(x, y), 2f)) return new PointF(x, y);
        }
        return null;
    }

    /// <summary>
    /// A squirrel in a POCKET (no goal on its row can be reached in a straight line: three failed picks in a row) LEAPS out of
    /// it: a standing hop, on the hop pages along an arc, to any spot it can stand on within a few lengths, the vine or the
    /// pumpkin's foot passed over. True when it found one. (Without this a squirrel that hopped down beside a pumpkin sat
    /// there trying goals for minutes: 272 s on one seed, off the edge of the screen.)
    /// </summary>
    private bool Leap(Critter c)
    {
        if (++c.Fails < 3) return false;
        float u = _s.U, k = _sq[c.Look].K;
        _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} tries a leap (fails {c.Fails}) at {c.X:F0},{c.Y:F0}");
        for (int i = 0; i < 40; i++)
        {
            float x = Math.Clamp(c.X + (0.06f + 0.14f * (float)_rng.NextDouble()) * u * (_rng.Next(2) == 0 ? -1 : 1), 0.02f * _s.Width, 0.98f * _s.Width);
            float crest = _s.Ground.YAt(x);
            float y = Math.Clamp(c.Y + 0.04f * u * (float)(_rng.NextDouble() * 2 - 1), crest + 0.012f * u, _s.Height - 0.012f * u);
            if (!CanStand(x, y) || RandomSpot(x - 0.3f * u, x + 0.3f * u, 10, y, 0.05f * u, 3f * k, new PointF(x, y)) == null) continue;   // it must be a spot with a way on from it
            c.Fails = 0;
            c.FlyFrom = c.X; c.FlyTo = x; c.FlyY0 = c.Y; c.FlyY1 = y;
            int dir = x >= c.X ? 1 : -1;
            c.Mind.Begin(Doing.HopDown, HopFor); c.Clock = 0; c.Perched = -1;
            if (dir != c.Dir) { StartTurn(c); c.HopAfterTurn = true; }
            else c.Dir = dir;
            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} leaps out of a pocket at {c.X:F0},{c.Y:F0} to {x:F0},{y:F0}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// A spot just behind a pumpkin (above its foot, within its width): the depth picture then hides most of the critter
    /// there. The pumpkin is on the critter's OWN ROW (its foot within a little of the critter's y) so the way there is
    /// along the patch, and the spot is tested the way a step is tested, with a clear line to it from where the critter
    /// stands. (The first version put the spot a pixel above the pumpkin's keep-out box, on any row: the step probe,
    /// which looks a few pixels ahead, refused every approach, and the squirrel hung frozen mid-bound at the box's edge
    /// for a third of a second, sat, and tried again, for 46 of 120 seconds on one seed.) The mice's hide; a squirrel
    /// hides in a tree.
    /// </summary>
    private PointF? BehindPumpkin(PointF from, float within)
    {
        var picks = new List<(PointF At, float R)>();
        foreach (var p in _s.Pumpkins)
            if (MathF.Abs(p.At.X - from.X) < within && p.R > _m * 0.8f && MathF.Abs(p.At.Y + 0.8f * p.R - from.Y) < 0.08f * _s.U) picks.Add(p);
        for (int tries = 0; tries < 6 && picks.Count > 0; tries++)
        {
            var (at, r) = picks[_rng.Next(picks.Count)];
            float foot = at.Y + 0.8f * r;
            var spot = new PointF(at.X + r * 0.5f * (float)(_rng.NextDouble() - 0.5), foot - 0.55f * r);
            if (CanStand(spot.X, spot.Y) && Ground.LineClear(_ground, from, spot, 2f)) return spot;
        }
        return null;
    }

    /// <summary>
    /// A spot beside a pumpkin to nibble at, on the critter's row: at the pumpkin's foot, just outside its keep-out box
    /// on the near side, nose to the rind. (The old spot was below the foot, inside the box: every nibble was refused.)
    /// </summary>
    private PointF? BesidePumpkin(PointF from, float within)
    {
        var picks = new List<(PointF At, float R)>();
        foreach (var p in _s.Pumpkins)
            if (MathF.Abs(p.At.X - from.X) < within && p.R > _m * 0.8f && MathF.Abs(p.At.Y + 0.8f * p.R - from.Y) < 0.08f * _s.U) picks.Add(p);
        for (int tries = 0; tries < 6 && picks.Count > 0; tries++)
        {
            var (at, r) = picks[_rng.Next(picks.Count)];
            float side = from.X < at.X ? -1f : 1f;
            var spot = new PointF(at.X + side * (1.05f * r + 0.3f * _m), at.Y + 0.8f * r + 0.1f * r);
            if (CanStand(spot.X, spot.Y) && Ground.LineClear(_ground, from, spot, 2f)) return spot;
        }
        return null;
    }

    /// <summary>
    /// A pumpkin a squirrel could hop up onto, near it on its own row and big enough to sit on (its index), with the spot
    /// at its foot to hop from (on the squirrel's side, just outside the keep-out box). Null when there is none.
    /// </summary>
    private (int Index, PointF From)? PerchPumpkin(Critter c, float within)
    {
        float k = _sq[c.Look].K;
        var picks = new List<int>();
        for (int i = 0; i < _s.Pumpkins.Count; i++)
        {
            var (at, r) = _s.Pumpkins[i];
            if (MathF.Abs(at.X - c.X) < within && MathF.Abs(at.X - c.X) > 1.5f * r && r > 0.9f * k && r < 2.6f * k && MathF.Abs(at.Y + 0.8f * r - c.Y) < 0.08f * _s.U) picks.Add(i);
        }
        for (int tries = 0; tries < 6 && picks.Count > 0; tries++)
        {
            int i = picks[_rng.Next(picks.Count)];
            var (at, r) = _s.Pumpkins[i];
            float side = c.X < at.X ? -1f : 1f;
            var spot = new PointF(at.X + side * (1.05f * r + 0.45f * k), at.Y + 0.8f * r + 0.05f * r);
            if (CanStand(spot.X, spot.Y) && Ground.LineClear(_ground, new PointF(c.X, c.Y), spot, 2f)) return (i, spot);
        }
        return null;
    }

    /// <summary>The tree (0 left, 1 right) a squirrel at x would den in: the nearer, if the foot of its trunk can be reached; else -1.</summary>
    private int TreeFor(Critter c, out PointF foot)
    {
        PointF me = new(c.X, c.Y);
        int first = c.X < _s.Width * 0.5f ? 0 : 1;
        foreach (int t in new[] { first, 1 - first })
        {
            PointF[] trunk = t == 0 ? _s.LeftTrunk : _s.RightTrunk;
            // The foot: just beside the trunk on the squirrel's side, on the trunk's own row (the ground's crest plus the trunk's stand).
            float side = c.X < trunk[0].X ? -1f : 1f;
            foot = new PointF(trunk[0].X + side * Standoff * _s.U, trunk[0].Y);
            if (MathF.Abs(foot.X - c.X) < 1.4f * _s.U && CanStand(foot.X, foot.Y) && Ground.LineClear(_ground, me, foot, 2f)) return t;
        }
        foot = default;
        return -1;
    }

    /// <summary>Where the trunk's middle is at height y, for a squirrel clinging to it.</summary>
    private float TrunkX(int tree, float y)
    {
        PointF[] trunk = tree == 0 ? _s.LeftTrunk : _s.RightTrunk;
        float f = Math.Clamp((trunk[0].Y - y) / MathF.Max(1f, trunk[0].Y - trunk[1].Y), 0f, 1f);
        return trunk[0].X + (trunk[1].X - trunk[0].X) * f;
    }

    /// <summary>Half the trunk's width at height y (the painter's bole: 0.019 U at the foot tapering to 0.011 at the fork): the feet of a climbing squirrel are on its edge.</summary>
    private float TrunkHalf(int tree, float y)
    {
        PointF[] trunk = tree == 0 ? _s.LeftTrunk : _s.RightTrunk;
        float f = Math.Clamp((trunk[0].Y - y) / MathF.Max(1f, trunk[0].Y - trunk[1].Y), 0f, 1f);
        return _s.U * (0.026f - 0.010f * f);
    }

    // ================================================================ painting (once)

    private static float Smooth01(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

    /// <summary>The y of a maple crown's lower edge over its trunk: where a climbing squirrel goes into the leaves.</summary>
    private float CrownEdge(int tree) { var (at, r) = tree == 0 ? _s.LeftMaple : _s.RightMaple; return at.Y + 0.72f * r; }

    /// <summary>
    /// Where a squirrel on a trunk is drawn, and through what. The climb SPIRALS: over the height from the foot to the crown it
    /// goes three quarters of the way round the trunk (front, the near side, the back, the far side), so its x swings across the
    /// trunk by the trunk's half width, and on the back half it is stamped through the trunk stencil (the bark hides its body,
    /// the tail and feet show past the edges). Above the crown's edge it is stamped through the props' stencil (the leaves hide
    /// it). "y" is the sheet's anchor row (the feet for a head-up climb, the head for a head-down one).
    /// </summary>
    private (float X, bool[]? Stencil) OnTrunkPlace(Critter c, float y, float bodyTop, float centreX)
    {
        float height = MathF.Max(1f, c.Y - CrownEdge(c.Tree));
        float frac = Math.Clamp(c.Up / height, 0f, 1f);
        float theta = 0.75f * MathF.Tau * frac;
        float x = centreX + c.Dir * MathF.Sin(theta) * (TrunkHalf(c.Tree, y) + 0.35f * _sq[c.Look].K);   // at the sides the body sticks out a third of a length past the bark (swinging only to the half width, the bark hid the whole squirrel for most of the climb)
        bool[]? stencil = bodyTop <= CrownEdge(c.Tree) ? _s.OpenOfProps : MathF.Cos(theta) < 0f ? _trunkMask : null;
        return (x, stencil);
    }

    private Sheet Make(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
    {
        var sh = new Sheet { Pages = new Sprite[n], W = w, H = h, Ox = ox, By = by };
        for (int i = 0; i < n; i++) sh.Pages[i] = page(i);
        return sh;
    }

    private void Build()
    {
        int mw = (int)(1.9f * _m) + 4, mh = (int)(0.95f * _m) + 4, mox = (int)(1.25f * _m) + 2, mby = mh - 2;
        _mouseRun = Make(8, mw, mh, mox, mby, i => Sprite.Paint(mw, mh, g => { g.TranslateTransform(mox, mby); g.ScaleTransform(_m, -_m); MouseArt.Paint(g, i / 8f, false, 0); }));
        _mouseStill = Make(2, mw, mh, mox, mby, i => Sprite.Paint(mw, mh, g => { g.TranslateTransform(mox, mby); g.ScaleTransform(_m, -_m); MouseArt.Paint(g, 0f, false, i + 1); }));

        // THE SQUIRRELS: the shared animal library paints them (Core/Animals/Squirrel.cs, SquirrelBooks: moved out of here on
        // 2026-11-10 with every page count, size, anchor and comment intact). Each variant is its coat at its own size; the books
        // are copied into this class's own Sheet shape.
        for (int v = 0; v < Variants.Length; v++)
        {
            var (coat, scale) = Variants[v];
            float k = _k * scale;
            var bk = Peckworks.Screensavers.Core.Animals.SquirrelBooks.Build(coat, k);
            static Sheet Conv(Peckworks.Screensavers.Core.Animals.SquirrelSheet s) => new Sheet { Pages = s.Pages, W = s.W, H = s.H, Ox = s.Ox, By = s.By };
            var sq = new SqSheets
            {
                K = k, Run = Conv(bk.Run), RunNut = Conv(bk.RunNut), Look = Conv(bk.Look), Dig = Conv(bk.Dig), Rise = Conv(bk.Rise), Turn = Conv(bk.Turn), Eat = Conv(bk.Eat), Curl = Conv(bk.Curl), Hop = Conv(bk.Hop),
                Up = Conv(bk.Up), Down = Conv(bk.Down), MountUp = Conv(bk.MountUp), MountDown = Conv(bk.MountDown),
                UpFoot = bk.UpFoot, DownHead = bk.DownHead, UpHead = bk.UpHead, UpCol = bk.UpCol, DownCol = bk.DownCol, MountUpCol = bk.MountUpCol, MountDownCol = bk.MountDownCol,
            };
            for (int t = 0; t < 4; t++) sq.Sit[t] = Conv(bk.Sit[t]);
            _sq[v] = sq;
        }

        float b = _b;
        int bw = (int)(1.6f * b) + 4, bh = (int)(1.3f * b) + 4, box = (int)(0.9f * b) + 2, bby = bh - 2;   // 1.3 tall: the bird stands on legs now, and a hop lifts it a tenth more
        for (int kind = 0; kind < 2; kind++)
        {
            int kk = kind;
            _bird[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Stand, kk, b, bw, bh, box, bby));
            _birdLook[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Look, kk, b, bw, bh, box, bby));
            _birdPeck[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Peck, kk, b, bw, bh, box, bby));
            _birdHop[kind] = Make(3, bw, bh, box, bby, i => BirdArt.Page(i == 0 ? BirdArt.Pose.HopCrouch : i == 1 ? BirdArt.Pose.HopUp : BirdArt.Pose.HopLand, kk, b, bw, bh, box, bby));
            _birdFlyKind[kind] = Make(7, bw, bh, box, bby, i => BirdArt.Page(BirdArt.Pose.Fly, kk, b, bw, bh, box, bby, i < 6 ? i : -1));   // six beating pages and the fold
            _birdFlare[kind] = Make(2, bw, bh, box, bby, i => BirdArt.Page(BirdArt.Pose.Flare, kk, b, bw, bh, box, bby, i));
            _birdDive[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Dive, kk, b, bw, bh, box, bby));
            _birdPreen[kind] = Make(2, bw, bh, box, bby, i => BirdArt.Page(BirdArt.Pose.Preen, kk, b, bw, bh, box, bby, i));
        }
    }

    // ================================================================ living

    private static bool OnTree(Critter c) => c.Mind.Doing is Doing.Mount or Doing.Climb or Doing.Enter or Doing.Den or Doing.Exit or Doing.Descend or Doing.Dismount;
    /// <summary>
    /// Is any critter on, or running for, this tree's trunk (0 left, 1 right)? READ ONLY, for the possum (Possum.cs), which
    /// shares the right maple's trunk and never starts a climb while a squirrel is on it (or on its way to it: a Go that
    /// carries the tree's number). Never writes anything.
    /// </summary>
    public bool TrunkBusy(int tree)
    {
        foreach (Critter c in _all)
            if (c.Tree == tree && (OnTree(c) || c.Mind.Doing == Doing.Go)) return true;
        return false;
    }

    private static bool OnPumpkin(Critter c) => c.Mind.Doing is Doing.HopUp or Doing.Perch or Doing.HopDown;

    public void Update(float dt)
    {
        _time += dt;                                // counted from the first frame, not from the sheets being ready: the log's clock is the frame dump's (the build took 0.4 s and every logged second was 0.4 s early)
        if (!_ready || _time < StartAt) return;     // pinned: the critters' first frame is at StartAt on every run (unless the sheets are still building, at 4K)
        if (_perchTest != null && !_perchTestDone)
        {
            if (_perchTest == "now" && _time >= 5.0)
            {
                _perchTestDone = true;
                foreach (Critter c in _all) if (c.Kind == Kind.Bird) { BeginUp(c); break; }
            }
            else if (_perchTest == "scare" && _time >= 8.0)
            {
                _perchTestDone = true;
                _board.Post(Sense.Threat, new PointF(_s.Width * 0.5f, _s.WalkY(_s.Width * 0.5f)), 1.6f * _s.U, 1f, this);   // the test's own raccoon: a threat across the whole patch, for the one second a notice lives
            }
        }
        for (int i = 0; i < _all.Count; i++)
        {
            Critter c = _all[i];
            c.Mind.Tick(dt);
            c.Senses.Tick(dt);
            c.Clock += dt;
            _log?.WriteLine($"{_time,8:F3} {c.Kind.ToString().ToLowerInvariant()}{c.Id} frame x {c.X:F1} y {c.Y:F1} v {c.Pace.Speed:F2} {c.Mind.Doing}{(c.Turning ? (c.Curl ? " curl" : " turn") : "")} d{c.Dir}{(c.Mind.Doing == Doing.Branch ? $" sub{c.Sub}" : c.Mind.Doing == Doing.Fly && c.Flight != 0 ? $" flight{c.Flight}" : "")}");
            // A bird that sees a THREAT (the raccoon, a fox, the wolves) goes UP: "it saw the thing and went up". The look is wider
            // than the senses' own reach (a bird on a wire sees the whole lane), and it works from a pumpkin's shoulder too. A
            // timid bird also takes to a branch when a squirrel bounds right at it. Once up, a bird on a branch listens.
            if (c.Kind == Kind.Bird && c.Mind.Doing is not (Doing.Fly or Doing.Branch)
                && _board.NearestInReach(Sense.Threat, new PointF(c.X, c.Y), 1.0f * _s.U) is Notice) BeginUp(c);   // within the notice's OWN reach as well: the dog's hunt on the far hill (0.08 U) sent every patch bird up
            else if (c.Kind == Kind.Bird && c.Mind.Doing == Doing.Branch) ListenOnBranch(c);
            // A squirrel on the move is a thing the mice and birds notice: it tells the board every third of a second.
            if (c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Fetch or Doing.Carry or Doing.Raid or Doing.Chase or Doing.Leave or Doing.Romp && c.Clock >= c.NextPost)
            {
                _board.Post(Sense.Movement, new PointF(c.X, c.Y), 0.14f * _s.U, 0.9f, c);
                c.NextPost = c.Clock + 0.33f;
            }
            // The senses: a mouse bolts from a squirrel or a scuffle, freezes at a noise; a bird flies up; a squirrel sits up and looks.
            // A squirrel up a tree or on a pumpkin, or in the middle of a game, is past noticing.
            if (c.Mind.Doing is not (Doing.Bolt or Doing.Fly or Doing.Chase or Doing.Leave or Doing.Romp or Doing.Branch) && !OnTree(c) && !OnPumpkin(c))
            {
                Reaction r = c.Senses.Check(_board, new PointF(c.X, c.Y), c, out Notice cause);
                // A timid bird that a squirrel bounds right at takes to a branch instead of just freezing (a third of the time).
                if (c.Kind == Kind.Bird && r == Reaction.Freeze && cause.Kind == Sense.Movement && c.Timid > 0.5f && _perchRng.NextDouble() < 0.35 && BeginUp(c)) continue;
                // A squirrel busy with a nut (chewing, burying, digging one up) is not put off its work by a noise or a passing
                // squirrel: it only looks that way. (Every freeze restarted the meal: one squirrel chewed one nut for 31 s
                // through a frenzy bounding round it.)
                bool busyWithNut = c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Eat or Doing.Bury or Doing.Dig;
                if (r == Reaction.Flee && !busyWithNut) Startle(c, cause.At);
                else if (r is Reaction.Flee or Reaction.Freeze && busyWithNut) { }   // it keeps at its nut (a mirror flip toward the noise was one of the "abrupt" ones; the look is the head's, not the body's)
                else if (r == Reaction.Freeze) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 1f + 1.5f * c.Timid); c.Clock = 0; c.Turning = false; c.Curl = false; }
                else if (r == Reaction.Look && c.Mind.Doing is Doing.Still or Doing.Peck or Doing.Perch)
                {
                    c.Clock = 1.1f; c.Mind.Left = Math.Max(c.Mind.Left, 0.8f);
                    int toward = cause.At.X >= c.X ? 1 : -1;
                    if (c.Kind == Kind.Mouse) c.Dir = toward;
                    else if (c.Kind == Kind.Squirrel && toward != c.Dir && c.Perched < 0 && !c.Turning) { StartTurn(c); }   // a sitting squirrel comes round through the front view to look (it used to mirror in one frame: 63 flips an hour)
                }
            }
            if (c.Turning)
            {
                // The turn on the spot: the body rises to the sit, narrows through the front view, flips, widens, and sinks; the
                // doing's own clock waits so nothing else moves meanwhile. The running curl has no rise and no sink, and keeps
                // moving: it carries on along the old way, dying out by the middle, and gathers out along the new way.
                float was = c.TurnT; c.TurnT += dt;
                if (!c.Curl) c.Mind.Left += dt;                                    // a sitting turn holds the doing's clock; a running curl does NOT (a curl that froze the timer could loop for ever)
                float pre = c.Curl ? 0f : c.TurnPre, post = c.Curl ? 0f : c.TurnPost, span = c.Curl ? CurlFor : TurnFor;
                float mid = pre + span / 2;
                if (c.Curl)
                {
                    float q = c.TurnT / CurlFor;
                    float v = q < 0.5f ? c.TurnV * (1f - 2f * q) : 0.6f * c.TurnV * (2f * q - 1f);
                    float nx = c.X + c.Dir * v * dt;
                    if (_ground(nx, c.Y)) c.X = nx;
                }
                if (was < mid && c.TurnT >= mid) c.Dir = -c.Dir;
                if (c.TurnT >= pre + span + post)
                {
                    c.Turning = false; c.Blend = 0;
                    if (c.Curl) { c.Curl = false; c.Pace.Resume(0.6f * c.TurnV); PointF aim = c.Via ?? c.Goal; c.Heading = MathF.Atan2(aim.Y - c.Y, aim.X - c.X); }   // toward the WAYPOINT when there is one (aimed at the goal behind it, it curled again at once: the loop)
                    if (c.HopAfterTurn) { c.HopAfterTurn = false; c.Mind.Begin(Doing.HopDown, HopFor); c.Clock = 0; }   // on a pumpkin top: turned round, now the hop down the far side
                    if (c.Mind.Doing == Doing.HopUp) c.Clock = 0;                      // a hop up that waited for the turn starts its pages now
                }
                continue;
            }
            Act(c, dt);
            if (c.Mind.Over) Decide(c);
            // A squirrel never pops from its sit into a bound or back (one frame from upright to stretched flat was the
            // patch's one real jump): the moment its doing changes class (down on all fours, or sitting up) it plays a
            // short blend through the crouch pages, sinking into the run or rising out of it. The pace is still easing
            // in those frames, so the sink plays nearly on the spot. (The tree and pumpkin doings have their own pitches.)
            bool down = IsDown(c.Mind.Doing);
            if (c.Kind == Kind.Squirrel && down != c.WasDown && !OnTree(c) && !OnPumpkin(c)) { c.Blend = BlendFor; c.Sinking = down; }
            c.WasDown = down;
            if (c.Blend > 0) c.Blend -= dt;
        }
        _all.RemoveAll(c => c.Done);                // the visitors that have left
    }

    /// <summary>The nearest live critter with this id, or null.</summary>
    private Critter? ById(int id)
    {
        foreach (Critter c in _all) if (c.Id == id && !c.Done) return c;
        return null;
    }

    /// <summary>
    /// What a squirrel does about nuts, asked first in Decide. True when it chose something. In order: finish what the
    /// timer just ended (a bury, a meal, a raid's dig); with a nut in its mouth, eat it or carry it off to bury; with a
    /// loose nut in reach, fetch it (a visitor sees the whole patch, a resident a little less); with another's stash
    /// near and a bold roll, raid it; a visitor with nothing left to do leaves.
    /// </summary>
    private bool DecideNuts(Critter c)
    {
        float u = _s.U;
        PointF me = new(c.X, c.Y);
        switch (c.Mind.Doing)
        {
            case Doing.Bury when c.Nut != null:
                _nuts.Bury(c.Nut, Math.Clamp(c.X + c.Dir * 0.35f * _k, 0.01f * _s.Width, 0.99f * _s.Width), c.Y, c.Id);   // on screen: one was buried at x 1921 of 1920
                _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} buries a nut at {c.X:F0},{c.Y:F0}");
                c.Nut = null;
                c.Mind.Begin(Doing.Still, 0.8f + 1.2f * (float)_rng.NextDouble());   // sits up and looks about: was anyone watching
                c.Clock = 0;
                return true;
            case Doing.Eat when c.Nut != null:
                _nuts.Eat(c.Nut); c.Nut = null;
                _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} ate a nut");
                break;
            case Doing.Dig when c.Target != null:
            {
                NutField.Nut dug = c.Target; c.Target = null;
                if (dug.State != NutField.State.Buried) break;                    // someone else got there first
                int owner = dug.Owner;
                _nuts.Take(dug); c.Nut = dug;
                _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} digs up squirrel{owner}'s stash at {c.X:F0},{c.Y:F0}");
                // The owner, if it is about and not too far, comes running; the thief bolts with the nut.
                Critter? o = ById(owner);
                if (o != null && o.Kind == Kind.Squirrel && o.Mind.Doing is not (Doing.Chase or Doing.Bolt) && !OnTree(o) && !OnPumpkin(o) && MathF.Abs(o.X - c.X) < 1.2f * u)
                {
                    o.ChaseId = c.Id; o.Goal = me; o.Mind.Begin(Doing.Chase, 5f); o.Clock = 0; o.Turning = false; Head(o);
                    _log?.WriteLine($"{_time,8:F2} squirrel{o.Id} comes running at squirrel{c.Id}");
                    PointF? run = Ground.FleeRun(_ground, me, new PointF(o.X, o.Y), _m * 0.5f, 0.4f * u, 0.08f * u);
                    c.Goal = run ?? RandomSpot(c.X - 0.3f * u, c.X + 0.3f * u, 20, c.Y, 0.05f * u, 3f * _k, me) ?? me;
                    c.Mind.Begin(Doing.Bolt, 2.5f); c.Travelled = 0; c.Clock = 0; Head(c);
                    return true;
                }
                break;
            }
        }
        if (c.Nut != null)
        {
            // Eat it here, or carry it off a little way and bury it (the eating weighs a touch more: a stash needs a thief to pay off).
            if (_rng.NextDouble() < 0.5)
            {
                c.Mind.Begin(Doing.Eat, 4f + 4f * (float)_rng.NextDouble()); c.Clock = 0;
                return true;
            }
            PointF? spot = RandomSpot(c.X - 0.35f * u, c.X + 0.35f * u, 30, c.Y, 0.05f * u, 2.5f * _k, me);
            if (spot == null) { c.Mind.Begin(Doing.Eat, 4f + 4f * (float)_rng.NextDouble()); c.Clock = 0; return true; }
            c.Goal = spot.Value; c.Mind.Begin(Doing.Carry, 8f); c.Travelled = 0; Head(c);
            return true;
        }
        NutField.Nut? loose = c.Visitor && _time - c.Arrived > 70f + 40f * (1f - c.Timid) ? null : _nuts.NearestLoose(me, c.Visitor ? 1.6f * u : 0.8f * u);
        if (loose != null && Ground.LineClear(_ground, me, new PointF(loose.X, loose.Y), 2f))
        {
            c.Target = loose; c.Goal = new PointF(loose.X, loose.Y); c.Mind.Begin(Doing.Fetch, 8f); c.Travelled = 0; Head(c);
            return true;
        }
        bool overstayed = c.Visitor && _time - c.Arrived > 70f + 40f * (1f - c.Timid);   // a visitor's welcome runs out: no more raids, and it goes whether or not a nut still lies loose
        NutField.Nut? stash = overstayed ? null : _nuts.NearestStash(c.Id, me, 0.35f * u);
        if (stash != null && _rng.NextDouble() < 0.25 + 0.5 * (1 - c.Timid) && Ground.LineClear(_ground, me, new PointF(stash.X, stash.Y), 2f))
        {
            c.Target = stash; c.Goal = new PointF(stash.X, stash.Y); c.Mind.Begin(Doing.Raid, 8f); c.Travelled = 0; Head(c);
            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} goes for squirrel{stash.Owner}'s stash");
            return true;
        }
        if (c.Visitor && ((_nuts.LooseCount == 0 && _nuts.FallingCount == 0) || overstayed))
        {
            if (_rng.NextDouble() < 0.4 && c.Fails < 3) { c.Mind.Begin(Doing.Still, 1f + 2f * (float)_rng.NextDouble()); return true; }   // lingers a moment first: sits up, looks about
            // Off the way it came, or the other way if that is clear; the line along its row may bend up or down a little more than a
            // resident's. When neither edge can be reached it leaps out of the pocket and tries again (two of nine visitors never left).
            foreach (int side in new[] { c.EntrySide, -c.EntrySide })
            {
                float edge = side < 0 ? -0.015f * _s.Width : 1.015f * _s.Width;
                PointF? off = EdgeSpot(edge, c.Y, 0.05f * u + 0.03f * u * c.Fails, me);
                if (off == null) continue;
                c.Fails = 0;
                c.Goal = off.Value; c.Mind.Begin(Doing.Leave, 12f); c.Travelled = 0; Head(c);
                _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} leaves");
                return true;
            }
            if (!Leap(c)) c.Mind.Begin(Doing.Still, 1f);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Faces a critter at its new goal before it sets off, when it is standing still: a stopped animal turns to where
    /// it is going and then goes. (Left to the steering, it set off on its OLD heading and swung round in a curve at
    /// 7 radians a second, a squirrel bounding in an arc up the screen from a dig and then stopping to turn.) A
    /// squirrel whose goal is behind it plays its sitting turn first (Update holds the doing's clock meanwhile); a
    /// mouse at thirteen pixels just faces the other way. A moving critter is left to the steering's turn rate.
    /// </summary>
    private void Head(Critter c)
    {
        if (c.Pace.Speed > 0.02f * _s.U) return;
        float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
        if (dx * dx + dy * dy < 1f) return;
        c.Heading = MathF.Atan2(dy, dx);
        if (MathF.Abs(MathF.Cos(c.Heading)) <= 0.2f) return;
        int dir = MathF.Cos(c.Heading) > 0 ? 1 : -1;
        if (dir == c.Dir) return;
        if (c.Kind == Kind.Squirrel) { StartTurn(c); }
        else c.Dir = dir;
    }

    /// <summary>
    /// A squirrel on the move turning back the other way: the running curl, started at the speed it has now. The
    /// doing goes on toward the (new) goal when the curl ends.
    /// </summary>
    /// <summary>A doing on all fours (running, hiding, digging), as against sitting up.</summary>
    private static bool IsDown(Doing d) => d is Doing.Go or Doing.Bolt or Doing.Hide or Doing.Dig or Doing.Fetch or Doing.Carry or Doing.Bury or Doing.Raid or Doing.Chase or Doing.Leave or Doing.Romp;

    /// <summary>
    /// A squirrel starts a turn on the spot. It rises to the sit first only if it was on all fours (WasDown is last frame's
    /// class), and sinks to the crouch after only if the doing it has just begun is one on all fours: a sitting squirrel
    /// that turns to look stays sitting throughout, and one that turns to set off sinks into its run at the end.
    /// </summary>
    private void StartTurn(Critter c)
    {
        c.Turning = true; c.Curl = false; c.TurnT = 0;
        c.TurnPre = c.WasDown ? TurnRise : 0f;
        c.TurnPost = IsDown(c.Mind.Doing) ? TurnRise : 0f;
    }

    private void CurlBack(Critter c, float stepThisFrame = 0f, float dt = 1f / 60)
    {
        // A cap on curls per goal: the second drops the waypoint (it was the thing being argued over), the third ends the doing
        // in a sit. A squirrel that keeps finding its aim behind it has a goal it cannot steer to; sitting beats spinning.
        c.Curls++;
        if (c.Curls == 2) c.Via = null;
        if (c.Curls >= 3)
        {
            c.Pace.Halt(); c.Via = null; c.Curls = 0; c.Turning = false; c.Curl = false;
            c.Mind.Begin(Doing.Still, 0.6f + 0.6f * (float)_rng.NextDouble()); c.Clock = 0;
            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} gives up a goal it kept curling at, at {c.X:F0},{c.Y:F0}");
            return;
        }
        c.Turning = true; c.Curl = true; c.TurnT = 0;
        c.TurnV = MathF.Max(stepThisFrame > 0f ? stepThisFrame / dt : c.Pace.Speed, 0.1f * _s.U);   // the speed the body actually had (the bound's pulse runs it up to 1.45 times its pace)
        c.Pace.Halt();
        c.Refused = 0;
        // The curl's first frame moves too (it used to stand one dead frame between the last bound and the first curl page).
        float nx = c.X + c.Dir * c.TurnV * dt;
        if (_ground(nx, c.Y)) c.X = nx;
    }

    private void Startle(Critter c, PointF from)
    {
        if (c.Kind == Kind.Bird) { BeginFly(c, null); return; }
        PointF? run = Ground.FleeRun(_ground, new PointF(c.X, c.Y), from, _m * 0.5f, 0.25f * _s.U, 0.06f * _s.U);
        PointF? goal = run ?? RandomSpot(c.X - 0.3f * _s.U, c.X + 0.3f * _s.U, 20, c.Y, 0.06f * _s.U, 0f, new PointF(c.X, c.Y));
        if (goal == null) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 1f); c.Clock = 0; return; }   // nowhere to run: it freezes
        c.Goal = goal.Value;
        c.Mind.Begin(Doing.Bolt, 2.5f);
        c.Travelled = 0;
        Head(c);
    }

    /// <summary>
    /// While a story plays on the near ground the patch's own life keeps out of its way (a loose mouse sat at the
    /// squirrel's feet while "the" mouse hid in the jack-o'-lantern): a mouse that has to choose goes behind a pumpkin
    /// and stays there, a squirrel goes up its tree and dens until the story is over, a bird stands still or stays on
    /// its perch. Set by the scene each frame.
    /// </summary>
    public bool Lull;

    /// <summary>
    /// Sends a squirrel off to its tree to den: true when a tree can be reached. The trunk's foot is up on the ground's
    /// crest and the squirrel is usually lower on the patch, so the trip is two legs: along its own row to beside the
    /// tree, then diagonally up to the foot (a goal straight up the screen slides a side-view animal on the spot).
    /// </summary>
    private bool GoDen(Critter c)
    {
        int tree = TreeFor(c, out PointF foot);
        if (tree < 0 || Lull) return false;                                        // never during a story: the caper's squirrel climbs the left maple and three squirrels piled onto one foot (2026-11-09)
        c.Tree = tree; c.Mind.Begin(Doing.Go, 10f); c.Travelled = 0; c.PauseAt = 0; c.Dart = false; c.DenFoot = null;
        float dy = c.Y - foot.Y, side = c.X < foot.X ? -1f : 1f;
        var beside = new PointF(foot.X + side * MathF.Max(0.012f * _s.U, 1.5f * dy), c.Y);   // 1.5 times the rise: the diagonal up to the foot is 34 degrees (at 1.1 it was 42, a steep slide the slope cap could not plan round: the foot is not standable ground)
        if (dy > 0.02f * _s.U && CanStand(beside.X, beside.Y) && Ground.LineClear(_ground, new PointF(c.X, c.Y), beside, 2f) && Ground.LineClear(_ground, beside, foot, 2f)) { c.Goal = beside; c.DenFoot = foot; }
        else c.Goal = foot;
        Head(c);
        _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} goes to tree {tree}");
        return true;
    }

    /// <summary>Another squirrel on the ground within reach for a playful one to romp with, or null.</summary>
    private Critter? Playmate(Critter c, float within)
    {
        Critter? best = null; float bestD = within;
        foreach (Critter o in _all)
        {
            if (o == c || o.Kind != Kind.Squirrel || o.Done || o.Nut != null || OnTree(o) || OnPumpkin(o) || o.Mind.Doing is not (Doing.Still or Doing.Go)) continue;   // one that is about, not busy with a nut, a dig, a game or a tree
            float d = MathF.Abs(o.X - c.X);
            if (d < bestD && Ground.LineClear(_ground, new PointF(c.X, c.Y), new PointF(o.X, o.Y), 2f)) { best = o; bestD = d; }
        }
        return best;
    }

    private void Decide(Critter c)
    {
        float u = _s.U;
        c.Clock = 0;
        // On a pumpkin top there is one way down: the hop (a raid, a fetch, a lull or a frenzy decided from up there dug a stash
        // through the pumpkin's face, and one visitor sat up there, in a doing that was not Perch, for four minutes).
        if (c.Kind == Kind.Squirrel && c.Perched >= 0 && c.Mind.Doing is not (Doing.HopUp or Doing.HopDown))
        {
            if (c.Mind.Doing == Doing.Perch && _rng.NextDouble() < 0.4 && !(Lull && c.Visitor)) { c.Mind.Begin(Doing.Perch, 2f + 3f * (float)_rng.NextDouble()); return; }
            HopOff(c);
            return;
        }
        // A bird up on its branch: the timer ending is the end of a visit, unless a story is playing or a threat is about (it
        // stays up, and longer the longer the threat lasts), or it has been up so long (28 s in a story, 45 s in a scare) that it comes down.
        if (c.Kind == Kind.Bird && c.Mind.Doing == Doing.Branch)
        {
            bool scared = _board.Nearest(Sense.Threat, new PointF(c.X, c.Y), 1.2f * u) is Notice;
            if ((Lull || scared) && _time - c.PerchedAt < (scared ? 45.0 : 28.0)) { c.Mind.Left = 2.5f + 3f * (float)_perchRng.NextDouble(); return; }
            BeginDrop(c);
            return;
        }
        if (c.Resume is PointF back)
        {
            // Going on after a stop-start pause: the same goal, the page cycle carrying on from where it stood.
            c.Resume = null; c.Goal = back; c.Mind.Begin(Doing.Go, 6f); Head(c);
            return;
        }
        // The tree: a den ends when its timer does (a timid one may wait a story out), with the head coming out first.
        if (c.Mind.Doing == Doing.Den)
        {
            if (Lull && _rng.NextDouble() < 0.5 * c.Timid) { c.Mind.Left = 3f; return; }
            c.Mind.Begin(Doing.Exit, ExitFor); c.Clock = 0;
            c.NextDen = (float)_time + 40f + 40f * (1f - c.Timid);
            return;
        }
        if (_test == "den" && c.Kind == Kind.Squirrel && !c.Visitor && _time < 60 && c.NextDen <= _time && c.Nut == null && GoDen(c)) return;   // the test aid, ahead of everything
        // The nuts come before the lull: a squirrel with a nut in its mouth or one in reach goes on with it while a story
        // plays (the frenzy is his ask; lulled, the visitors hid behind pumpkins for the whole story and never fetched one).
        if (c.Kind == Kind.Squirrel && DecideNuts(c)) return;
        if (Lull)
        {
            if (c.Kind == Kind.Bird) c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : _rng.Next(3) == 0 ? Doing.Peck : Doing.Still, 2f + 2f * (float)_rng.NextDouble());
            else if (c.Kind == Kind.Squirrel)
            {
                // Out of the story's way: up its tree (if it has not just been), or up onto a pumpkin to watch from, else it sits.
                // (Denning for every story kept both squirrels in the trees two thirds of the time: a den is a rest, not a habit.)
                if (!c.Visitor && _time >= c.NextDen && GoDen(c)) return;
                if (c.Perched >= 0 && _rng.NextDouble() < 0.4) { HopOff(c); return; }   // down off the pumpkin for a while, it sits on the ground instead
                var pick = c.Perched < 0 ? PerchPumpkin(c, 0.45f * u) : null;
                if (pick != null) { c.ToPerch = pick.Value.Index; c.Goal = pick.Value.From; c.Mind.Begin(Doing.Go, 8f); c.Travelled = 0; c.PauseAt = 0; c.Dart = false; }
                else c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : Doing.Still, 2f + 2f * (float)_rng.NextDouble());
            }
            else
            {
                PointF? g = BehindPumpkin(new PointF(c.X, c.Y), 0.4f * u);
                if (g == null) c.Mind.Begin(Doing.Still, 2f);
                else { c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 5f + 3f * (float)_rng.NextDouble()); c.Travelled = 0; }
            }
            Head(c);
            return;
        }
        switch (c.Kind)
        {
            case Kind.Mouse:
            {
                // Faces: scurry somewhere, sniff, nibble a pumpkin, hide behind one.
                // (A hide weighed up to 0.65 and lasted eight seconds: the mice were behind the pumpkins half the time.)
                int face = Dice.Pick(_rng, [0.5f + 0.6f * c.Busy, 0.35f, 0.25f + 0.2f * (1 - c.Timid), 0.08f + 0.25f * c.Timid]);
                if (face == 0) { PointF? g = RandomSpot(c.X - 0.3f * u, c.X + 0.3f * u, 30, c.Y, 0.035f * u, 3f * _m, new PointF(c.X, c.Y)); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Go, 6f); c.Travelled = 0; }   // the row's reach is 0.035 U: at 0.06 a scurry three lengths along and two down was mostly down
                else if (face == 1) c.Mind.Begin(Doing.Still, 0.4f + 1.2f * (float)_rng.NextDouble());
                else if (face == 2) { PointF? g = BesidePumpkin(new PointF(c.X, c.Y), 0.3f * u); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Nibble, 6f); c.Travelled = 0; }
                else { PointF? g = BehindPumpkin(new PointF(c.X, c.Y), 0.35f * u); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 4f); c.Travelled = 0; }
                break;
            }
            case Kind.Bird:
            {
                // Faces: a hop or two, peck, look about, fly to a pumpkin's top, fly to another spot on the ground.
                // A songbird in a patch gets about by FLYING (his line: "they need to look like they're flying around
                // instead of hopping around"): the flights are the heavy faces, a hop is a few steps at most.
                bool perched = c.Perched >= 0;
                // Now and then, up to a branch in a top corner (about once a minute or two a bird; a timid one more). Rolled
                // on its own dice and ahead of the old ones, so a bird that does not go up rolls exactly as it always did.
                if (!perched && _time >= c.NextPerch && _perchRng.NextDouble() < 0.02 + 0.03 * c.Timid && BeginUp(c)) break;
                int face = Dice.Pick(_rng, [perched ? 0f : 0.15f + 0.15f * c.Busy, perched ? 0f : 0.45f, 0.35f, perched ? 0.15f : 0.3f + 0.3f * c.Timid, perched ? 0.6f : 0.45f + 0.4f * c.Busy]);
                if (face == 0) { PointF? g = RandomSpot(c.X - 0.06f * u, c.X + 0.06f * u, 30, from: new PointF(c.X, c.Y)); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Go, 1.5f); c.Travelled = 0; }
                else if (face == 1) c.Mind.Begin(Doing.Peck, 0.6f + 1.4f * (float)_rng.NextDouble());
                else if (face == 2) c.Mind.Begin(perched ? Doing.Perch : Doing.Still, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 3) BeginFly(c, PerchNear(c.X, 0.35f * u));
                else BeginFly(c, null);
                break;
            }
            default:
            {
                // Sitting on a pumpkin: sit a while longer, or hop down.
                if (c.Mind.Doing == Doing.Perch && c.Perched >= 0)
                {
                    if (_rng.NextDouble() < 0.4) { c.Mind.Begin(Doing.Perch, 2f + 3f * (float)_rng.NextDouble()); break; }
                    HopOff(c);
                    break;
                }
                // Faces, by the dials: bound somewhere (a playful one darts back the way it came), sit and look, scratch
                // at the earth, go up its tree and den (a timid one more), hop up onto a pumpkin (a playful one), romp
                // after another squirrel (a playful one, when there is one about). It never hides behind a pumpkin
                // (his line: "squirrels get hung up on pumpkins": that was a tail sticking out of one for fifteen seconds).
                Critter? mate = c.Playful > 0.3f ? Playmate(c, 1.2f * u) : null;
                int face = Dice.Pick(_rng, [0.5f + 0.5f * c.Busy, 0.25f + 0.3f * (1 - c.Busy), 0.25f, c.Visitor || _time < c.NextDen ? 0f : 0.03f + 0.15f * c.Timid, 0.04f + 0.28f * c.Playful, mate == null ? 0f : 0.35f * c.Playful]);
                // A bound goes ALONG the patch: the goal stays within a little of its own row (a goal straight up or down the
                // screen sent a side-view animal sliding on the spot, the "90 degree turns"), and at least a few lengths off.
                if (face == 0)
                {
                    PointF? g = RandomSpot(c.X - 0.45f * u, c.X + 0.45f * u, 30, c.Y, 0.05f * u, 4f * _k, new PointF(c.X, c.Y));
                    if (g == null) { if (!Leap(c)) c.Mind.Begin(Doing.Still, 0.5f); break; }
                    c.Fails = 0;
                    c.Goal = g.Value; c.Mind.Begin(Doing.Go, 6f); c.Travelled = 0;
                    // A long run is a stop-start one, four times in ten: it pulls up somewhere in the middle third, sits up, looks, goes on.
                    float far = MathF.Abs(g.Value.X - c.X);
                    c.PauseAt = far > 7f * _k && _rng.NextDouble() < 0.4 ? far * (0.35f + 0.3f * (float)_rng.NextDouble()) : 0f;
                    // Or a dart: it runs out and curls straight back, at speed, for the fun of it.
                    c.Dart = _test == "dart" || (c.PauseAt == 0f && _rng.NextDouble() < 0.5 * c.Playful);
                }
                else if (face == 1) c.Mind.Begin(Doing.Still, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 2) c.Mind.Begin(Doing.Dig, 1f + 1.5f * (float)_rng.NextDouble());
                else if (face == 3) { if (!GoDen(c)) c.Mind.Begin(Doing.Still, 1f); }
                else if (face == 4)
                {
                    var pick = PerchPumpkin(c, 0.45f * u);
                    if (pick == null) { c.Mind.Begin(Doing.Still, 1f); break; }
                    c.ToPerch = pick.Value.Index; c.Goal = pick.Value.From; c.Mind.Begin(Doing.Go, 8f); c.Travelled = 0; c.PauseAt = 0; c.Dart = false;
                }
                else
                {
                    c.ChaseId = mate!.Id; c.Legs = 2 + _rng.Next(2); c.Goal = new PointF(mate.X, mate.Y);
                    c.Mind.Begin(Doing.Romp, 9f); c.Travelled = 0;
                    _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} romps after squirrel{mate.Id}");
                }
                break;
            }
        }
        if (c.Mind.Doing is Doing.Go or Doing.Nibble or Doing.Hide or Doing.Romp) Head(c);
        else if (c.Mind.Doing is not (Doing.Fly or Doing.HopUp or Doing.HopDown)) c.Pace.Halt();   // a doing that stands has no speed (a curl's momentum once outlived its run: the log read a sitting squirrel at speed for eight seconds)
    }

    /// <summary>A squirrel hops down off its pumpkin: to the far side if the ground there is clear, else back the way it came up.</summary>
    private void HopOff(Critter c)
    {
        var (at, r) = _s.Pumpkins[c.Perched];
        float k = _sq[c.Look].K, u = _s.U;
        foreach (int dir in new[] { c.Dir, -c.Dir })
        {
            var to = new PointF(at.X + dir * (1.05f * r + 0.6f * k), at.Y + 0.8f * r + 0.08f * r);
            // The landing must be a spot with a way on from it (a landing hard against the pumpkin's foot, with every goal on
            // the row blocked, was a squirrel sitting there for minutes).
            if (!CanStand(to.X, to.Y) || RandomSpot(to.X - 0.3f * u, to.X + 0.3f * u, 10, to.Y, 0.05f * u, 3f * k, to) == null) continue;
            c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
            if (dir != c.Dir) { StartTurn(c); c.HopAfterTurn = true; }   // comes round on the top first, then hops down the far side
            else { c.Mind.Begin(Doing.HopDown, HopFor); c.Clock = 0; }
            return;
        }
        c.Mind.Begin(Doing.Perch, 2f);                                      // nowhere to land: sits on
    }

    /// <summary>A pumpkin top near x to sit on: its index, or -1.</summary>
    private int PerchNear(float x, float within)
    {
        var picks = new List<int>();
        for (int i = 0; i < _s.Pumpkins.Count; i++)
            if (MathF.Abs(_s.Pumpkins[i].At.X - x) < within && _s.Pumpkins[i].R > _b * 0.9f && _s.Pumpkins[i].At.Y - 1.1f * _s.Pumpkins[i].R > 0) picks.Add(i);
        return picks.Count == 0 ? -1 : picks[_rng.Next(picks.Count)];
    }

    private void BeginFly(Critter c, int? perch)
    {
        float u = _s.U;
        PointF to;
        if (perch.HasValue && perch.Value >= 0)
        {
            var (at, r) = _s.Pumpkins[perch.Value];
            to = new PointF(at.X - 0.1f * r, at.Y - 1.05f * r);          // beside the stem, on the shoulder of the pumpkin
            c.Perched = perch.Value;
        }
        else
        {
            // Anywhere across the patch (it used to be within half a U, a hop's worth), at least a few lengths away so
            // every flight is a flight and not a flutter on the spot.
            PointF fallback = new(c.X, c.Y);
            to = RandomSpot(c.X - 1.1f * u, c.X + 1.1f * u, 30) ?? fallback;
            for (int i = 0; i < 10 && MathF.Abs(to.X - c.X) < 6f * _b; i++) to = RandomSpot(c.X - 1.1f * u, c.X + 1.1f * u, 30) ?? fallback;
            c.Perched = -1;
        }
        c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
        c.FlyFor = Math.Max(0.6f, MathF.Abs(to.X - c.X) / (0.55f * u) + 0.4f);
        c.Dir = to.X >= c.X ? 1 : -1;
        c.Clock = 0;                                                    // the wing pages count from the take-off
        c.Mind.Begin(Doing.Fly, c.FlyFor);
    }

    // ================================================================ the branch perch (2026-11-09)
    //
    // FEYNMAN VERSION. The oak branches in the top corners are just wood the painter grew; it handed the scene their
    // geometry (BranchTimber: a list of fat round-ended bars, BranchTopY(x): the y of the top edge, the surface a foot
    // stands on). A bird picks a spot on that top edge, flies UP to it in bounds (beats, then a shut-wing dart, each
    // bound a little higher: the way a sparrow climbs the air), throws its wings up and its feet out in the last
    // quarter second (the FLARE), and sits. Up there it looks about, preens (head turned right round, beak in the
    // shoulder feathers), pecks at the bark, and now and then hops a step along the wood. Then it tips off the branch
    // and glides down into the patch with its wings half shut, flaring again onto the ground or a pumpkin's shoulder.
    // It posts nothing. It listens: a threat on the board (the raccoon, the wolves) sends a bird UP, and keeps it up.
    // WHAT IT IS SOLID TO: the wood only (its feet are exactly on the top edge). It passes over everything else; it is
    // drawn in the second pass at the end of Draw, with no depth stencil (nothing in the near ground can hide it), and
    // never over the falling leaves, which are drawn after the whole patch.

    /// <summary>Is a bird up in the high pass: its feet above the ground's crest and not standing on a pumpkin.</summary>
    private bool IsHigh(Critter c) => c.Perched < 0 && c.Y < _s.Ground.YAt(Math.Clamp(c.X, 0, _s.Width - 1));

    /// <summary>
    /// A spot on a branch to sit: the feet's place on the wood's top edge, or null. Candidates are random points along
    /// the timber that pass the tests (on screen, a gentle slope under the feet, the wood under the toes as well as the
    /// heels, clear of every leaf bunch's centre by a bird's length, clear of another bird's seat); the best of up to six
    /// wins by a score that likes the lower branches, the inner halves of the screen, and a short flight.
    /// </summary>
    private PointF? PickBranchSpot(Critter c, bool relaxed = false)
    {
        float leafR = relaxed ? 0.55f : 1f, slopeMax = relaxed ? 0.8f : 0.45f, thin = relaxed ? 0.1f : 0.2f;   // a scene whose branches are all thin or leafy gets a second, looser search
        float b = _b, w = _s.Width, h = _s.Height;
        var timber = _s.BranchTimber;
        if (timber.Count == 0) return null;
        PointF? best = null; float bestScore = float.MinValue; int found = 0;
        for (int tries = 0; tries < 400 && found < 6; tries++)
        {
            var (a, e, wd) = timber[_perchRng.Next(timber.Count)];
            if (wd < thin * b) continue;                                                    // a twig no thicker than a toe is no perch
            float x = a.X + (e.X - a.X) * (0.1f + 0.8f * (float)_perchRng.NextDouble());
            if (x < 0.04f * w || x > 0.96f * w) continue;
            float? top = _s.BranchTopY(x);
            if (top is not float y || y < 1.5f * b || y > 0.45f * h) continue;               // the whole bird on the screen, on the branches (not some far twig down the picture)
            float? fore = _s.BranchTopY(x + 0.6f * b), aft = _s.BranchTopY(x - 0.4f * b);
            if (fore is not float yf || aft is not float ya || MathF.Abs(yf - ya) > slopeMax * b) continue;   // wood under the toes and the heels, and not a steep one
            bool clear = true;
            foreach (PointF p in _s.CornerBranchSpots)
            {
                float dx = p.X - x, dy = p.Y - (y - 0.45f * b);
                if (dx * dx + dy * dy < leafR * leafR * b * b) { clear = false; break; }                    // never within a bird's length of a leaf bunch's centre
            }
            if (!clear) continue;
            foreach (Critter o in _all)
                if (o != c && o.Kind == Kind.Bird && (o.Mind.Doing == Doing.Branch || (o.Mind.Doing == Doing.Fly && o.Flight == 1)) && MathF.Abs(o.FlyTo - x) < 2.5f * b && MathF.Abs(o.FlyY1 - y) < 3f * b) { clear = false; break; }
            if (!clear) continue;
            found++;
            float inner = MathF.Min(x, w - x) / (0.5f * w);                                     // 0 at the edge, 1 in the middle
            float score = 1.0f * (y / (0.45f * h)) + 1.2f * inner - 0.8f * MathF.Abs(x - c.X) / w + 0.9f * (float)_perchRng.NextDouble();
            if (score > bestScore) { bestScore = score; best = new PointF(x, y + 1f); }       // a pixel into the wood: the toes' line overlaps the top edge, never hangs a hair above it
        }
        return best;
    }

    /// <summary>Sends a bird up to a branch (the flight up: bounds, flare). False when no seat could be found.</summary>
    private bool BeginUp(Critter c)
    {
        if (_time < c.NextTry) return false;
        PointF? spot = PickBranchSpot(c) ?? PickBranchSpot(c, true);
        if (spot == null) {
            c.NextTry = (float)_time + 3f;
            _log?.WriteLine($"{_time,8:F2} bird{c.Id} found NO branch spot (timber {_s.BranchTimber.Count}, w {_s.Width} h {_s.Height})"); return false; }
        float u = _s.U;
        PointF to = spot.Value;
        float dx = to.X - c.X, dy = to.Y - c.Y;
        c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
        c.FlyFor = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy) / (1.0f * u) + 0.6f, 1.3f, 3.2f);
        c.Dir = dx >= 0 ? 1 : -1;
        c.Perched = -1; c.Flight = 1; c.Clock = 0; c.Pace.Halt();
        c.Mind.Begin(Doing.Fly, c.FlyFor);
        _log?.WriteLine($"{_time,8:F2} bird{c.Id} goes UP to a branch spot {to.X:F0},{to.Y:F0} from {c.X:F0},{c.Y:F0} ({c.FlyFor:F1} s)");
        return true;
    }

    /// <summary>The bird's flight up has arrived: it sits, for six to twenty-five seconds.</summary>
    private void BeginBranch(Critter c)
    {
        c.X = c.FlyTo; c.Y = c.FlyY1; c.Flight = 0;
        c.Mind.Begin(Doing.Branch, 6f + 19f * (float)_perchRng.NextDouble());
        c.Clock = 0; c.PerchedAt = (float)_time;
        c.Sub = SubSit; c.SubLeft = 1.2f + 1.5f * (float)_perchRng.NextDouble();
        _log?.WriteLine($"{_time,8:F2} bird{c.Id} LANDS on its branch at {c.X:F0},{c.Y:F0} for {c.Mind.Left:F1} s");
    }

    /// <summary>The bird tips off its branch and glides down to the ground (or a pumpkin's shoulder) somewhere in the patch.</summary>
    private void BeginDrop(Critter c)
    {
        float u = _s.U;
        PointF to;
        int perch = _perchRng.NextDouble() < 0.4 ? PerchNear(c.X + (c.X < _s.Width * 0.5f ? 0.5f : -0.5f) * u, 0.5f * u) : -1;
        if (perch >= 0) { var (at, r) = _s.Pumpkins[perch]; to = new PointF(at.X - 0.1f * r, at.Y - 1.05f * r); }
        else
        {
            // Into the patch: toward the middle of the screen from the corner, a good way off.
            float lo = c.X < _s.Width * 0.5f ? c.X + 0.15f * u : c.X - 0.9f * u, hi = c.X < _s.Width * 0.5f ? c.X + 0.9f * u : c.X - 0.15f * u;
            to = RandomSpot(lo, hi, 40) ?? new PointF(c.X, _s.WalkY(c.X));
        }
        c.Perched = perch;
        c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
        float dx = to.X - c.X, dy = to.Y - c.Y;
        c.FlyFor = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy) / (0.75f * u) + 0.7f, 1.2f, 3.2f);
        c.Dir = dx >= 0 ? 1 : -1;
        c.Flight = 2; c.Clock = 0;
        c.NextPerch = (float)_time + 40f + 40f * (1f - c.Timid);
        c.Mind.Begin(Doing.Fly, c.FlyFor);
        _log?.WriteLine($"{_time,8:F2} bird{c.Id} LEAVES its branch for {to.X:F0},{to.Y:F0} ({c.FlyFor:F1} s, up {(float)(_time - c.PerchedAt):F1} s)");
    }

    /// <summary>A bird on a branch notices a thing near it: it turns to look (a hop round if it faces the wrong way), else just looks.</summary>
    private void ListenOnBranch(Critter c)
    {
        if (c.Sub == SubShuffle) return;
        Reaction r = c.Senses.Check(_board, new PointF(c.X, c.Y), c, out Notice cause);
        if (r == Reaction.None) return;
        int toward = cause.At.X >= c.X ? 1 : -1;
        if (toward != c.Dir) StartShuffle(c, 0f, toward);
        else { c.Sub = SubSit; c.Clock = 1.1f; c.SubLeft = Math.Max(c.SubLeft, 1.0f); }   // the page is chosen by Clock: 1.1 is the head-up look
    }

    /// <summary>A hop of "step" pixels along the wood (0 turns it round on the spot), facing "face" afterwards. False when the wood runs out.</summary>
    private void StartShuffle(Critter c, float step, int face)
    {
        float b = _b, tx = c.X + step;
        if (step != 0f)
        {
            float? t = _s.BranchTopY(tx), tf = _s.BranchTopY(tx + MathF.Sign(step) * 0.6f * b);
            if (t is not float ty || tf is not float tfy || MathF.Abs(tfy - ty) > 0.45f * b) { step = 0f; tx = c.X; face = -c.Dir; }   // no wood that way: it turns round instead
        }
        c.Sub = SubShuffle; c.ShufT = 0; c.ShufFrom = c.X; c.ShufTo = tx; c.ShufFace = face;
    }

    /// <summary>What a bird on a branch is up to this frame: the hop of a shuffle, or its sub-doing running out and the next one dealt.</summary>
    private void BranchAct(Critter c, float dt)
    {
        float b = _b;
        if (c.Sub == SubShuffle)
        {
            c.ShufT += dt;
            float k = Math.Clamp(c.ShufT / ShuffleFor, 0f, 1f), s = k * k * (3f - 2f * k);
            c.X = c.ShufFrom + (c.ShufTo - c.ShufFrom) * s;
            float? top = _s.BranchTopY(c.X);
            if (top is float y) c.Y = y + 1f;
            if (k >= 0.45f && c.Dir != c.ShufFace) c.Dir = c.ShufFace;                   // the flip is made at the top of the hop, in the air
            if (k >= 1f) { c.Sub = SubSit; c.SubLeft = 0.8f + 1.4f * (float)_perchRng.NextDouble(); c.Clock = 0; }
            return;
        }
        c.SubLeft -= dt;
        if (c.SubLeft > 0f) return;
        // The next thing: sit and look about, preen, peck at the bark, or a step along the branch (never two shuffles running).
        int pick = Dice.Pick(_perchRng, [0.35f, c.Sub == SubPreen ? 0f : 0.28f, 0.2f, c.Sub == SubShuffle ? 0f : 0.22f]);
        c.Clock = 0;
        switch (pick)
        {
            case 1: c.Sub = SubPreen; c.SubLeft = 1.6f + 1.2f * (float)_perchRng.NextDouble(); break;
            case 2: c.Sub = SubPeck; c.SubLeft = 0.8f + 0.9f * (float)_perchRng.NextDouble(); break;
            case 3:
            {
                int side = _perchRng.Next(2) == 0 ? -1 : 1;
                StartShuffle(c, side * (0.6f + 0.5f * (float)_perchRng.NextDouble()) * b, side);
                break;
            }
            default: c.Sub = SubSit; c.SubLeft = 1.2f + 2.0f * (float)_perchRng.NextDouble(); break;
        }
    }

    /// <summary>
    /// Where along its flight a bird is, 0 to 1 of the distance, from 0 to 1 of the time: a burst off the ground, a
    /// steady middle, and a flare that slows into the landing (a straight line in time arrived at full speed and
    /// stopped dead). In words: fast at first, easing to a stop.
    /// </summary>
    private static float FlightAlong(float t) => t < 0.7f ? t / 0.85f : 1f - (1f - t) * (1f - t) / 0.85f / 0.6f;   // full speed for 0.7 of the time, then slowing evenly to the landing (the two halves meet at 0.824)

    /// <summary>
    /// How high a flight is at t: a hump over the ground plus the bounding of a small bird's flight, which is a few
    /// beats then a fold, and each fold is a little dip, a bounce along the path (a smooth arc with wings beating all
    /// the way is a toy on a string).
    /// </summary>
    private float FlightHeight(Critter c, float t, float u)
    {
        float span = MathF.Abs(c.FlyTo - c.FlyFrom);
        // A patch bird flies LOW, a few lengths up, higher the farther it goes, never above the fence line's height
        // (0.12 U plus 0.22 of the span sent a sparrow up past the barn roof).
        float hump = MathF.Min(0.16f * u, 0.05f * u + 0.09f * span) * 4 * t * (1 - t);
        // The bound: two a second, in step with the fold the drawing makes (the last fifth of each half second): the bird
        // drops through the fold and climbs again on the first beats, a dip of half a length, absent at take-off and landing.
        float q = ((c.Clock % 0.5f) / 0.5f + 0.25f) % 1f;
        float dip = q < 0.4f ? 0.5f * _b * MathF.Sin(q / 0.4f * MathF.PI) : 0f;
        return hump - dip * 4 * t * (1 - t);
    }

    /// <summary>
    /// A squirrel's hop onto or off a pumpkin: where it is at "k" (0 to 1 of the hop's time). The feet stay put through
    /// the crouch and the push, fly on an arc through the flight pages, and are down again for the landing and the
    /// settle: the body's pages (SquirrelArt.Hop) and the arc agree.
    /// </summary>
    private static (float X, float Y) HopAt(Critter c, float k, float lift)
    {
        float a = Math.Clamp((k - SquirrelArt.HopPush) / (SquirrelArt.HopLand - SquirrelArt.HopPush), 0f, 1f);   // 0 until the push, 1 from the landing
        float s = a * a * (3f - 2f * a);
        return (c.FlyFrom + (c.FlyTo - c.FlyFrom) * s, c.FlyY0 + (c.FlyY1 - c.FlyY0) * s - lift * MathF.Sin(MathF.PI * a));
    }

    /// <summary>What the doing does this frame: a step toward the goal, a flight along its arc, or nothing.</summary>
    private void Act(Critter c, float dt)
    {
        float u = _s.U;
        switch (c.Mind.Doing)
        {
            case Doing.Go:
            case Doing.Bolt:
            case Doing.Nibble:
            case Doing.Hide:
            case Doing.Fetch:
            case Doing.Carry:
            case Doing.Raid:
            case Doing.Chase:
            case Doing.Leave:
            case Doing.Romp:
            {
                if (c.Mind.Doing is Doing.Chase or Doing.Romp)
                {
                    // The owner runs at the thief, wherever the thief is now; it gives up when close (the thief has bolted on) or when
                    // the timer ends. A romp runs at the playmate the same way; getting close makes the playmate bolt a little way,
                    // and the game goes on for a few such legs.
                    Critter? other = ById(c.ChaseId);
                    if (other == null || other.Done || OnTree(other) || OnPumpkin(other)) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 1f); c.Clock = 0; c.ChaseId = -1; return; }
                    c.Goal = new PointF(other.X, other.Y);
                }
                // A squirrel never runs steeply: a goal more than 35 degrees up or down the screen is reached by a waypoint on the
                // goal's row, a diagonal first and then along the row (a curl at the waypoint when it has run past). The waypoint
                // is planned once per goal and dropped when the squirrel is within a length of it.
                PointF aim = c.Goal;
                if (c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Fetch or Doing.Raid or Doing.Carry or Doing.Leave)
                {
                    if (c.Via == null && c.ViaFor != c.Goal || c.Via != null && c.ViaFor != c.Goal) { c.ViaFor = c.Goal; c.Via = PlanVia(c); c.Curls = 0; }
                    if (c.Via is PointF via)
                    {
                        if (MathF.Abs(via.X - c.X) < 1.2f * _k && MathF.Abs(via.Y - c.Y) < 1.2f * _k) c.Via = null;
                        else aim = via;
                    }
                }
                else c.Via = null;
                float dx = aim.X - c.X, dy = aim.Y - c.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float pathLeft = c.Via is PointF v2 ? dist + MathF.Sqrt((c.Goal.X - v2.X) * (c.Goal.X - v2.X) + (c.Goal.Y - v2.Y) * (c.Goal.Y - v2.Y)) : dist;   // the slow-in reads the whole way left, not the leg to the waypoint
                float arrive = c.Kind == Kind.Bird ? 0.2f * _b : c.Mind.Doing is Doing.Chase or Doing.Romp ? 1.8f * _k : c.Mind.Doing is Doing.Fetch or Doing.Raid ? 0.5f * _k : c.DenFoot != null ? 1f * _k : 0.25f * _m;
                if (c.Mind.Doing == Doing.Romp && dist < arrive)
                {
                    // Caught up: the playmate bolts a short way (if it can), and the chaser keeps after it; after the last leg both sit up.
                    Critter mate = ById(c.ChaseId)!;
                    if (mate.Nut == null && mate.Mind.Doing is not (Doing.Bolt or Doing.Eat or Doing.Bury or Doing.Dig) && !mate.Turning)
                    {
                        PointF? run = Ground.FleeRun(_ground, new PointF(mate.X, mate.Y), new PointF(c.X, c.Y), _m * 0.5f, 0.3f * u, 0.08f * u);
                        if (run != null)
                        {
                            mate.Goal = run.Value; mate.Mind.Begin(Doing.Bolt, 1.6f); mate.Travelled = 0; mate.Clock = 0; mate.PauseAt = 0; mate.Dart = false;
                            if (mate.Pace.Speed > 0.02f * u && MathF.Sign(run.Value.X - mate.X) != mate.Dir) CurlBack(mate); else Head(mate);
                            c.Legs--;
                        }
                    }
                    if (c.Legs <= 0 || mate.Mind.Doing != Doing.Bolt) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 1.5f); c.Clock = 0; c.ChaseId = -1; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} ends the romp"); return; }
                    // The game goes on: the chaser keeps running after the bolting mate (the step below; a stop here with the pace
                    // still up was 25 frames of a frozen run page on one seed).
                }
                if (dist < arrive)
                {
                    // Arrived: a scurry ends in a sniff; a nibble and a hide stay put until the timer; a fetch takes the nut;
                    // a carry turns into the burying; a raid into the digging up; a chase ends with the owner sitting up,
                    // a leaving visitor is gone, a run to a tree pitches up onto the bark, a run to a pumpkin hops up onto it.
                    float arriveSpeed = c.Pace.Speed;
                    c.Pace.Halt();
                    switch (c.Mind.Doing)
                    {
                        case Doing.Fetch:
                            if (c.Target != null && c.Target.State == NutField.State.Loose) { _nuts.Take(c.Target); c.Nut = c.Target; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} picks up a nut at {c.X:F0},{c.Y:F0}"); }
                            c.Target = null;
                            c.Mind.Begin(Doing.Still, 0.4f + 0.4f * (float)_rng.NextDouble()); c.Clock = 0;   // sits up with it, then Decide: eat or bury
                            break;
                        case Doing.Carry: c.Mind.Begin(Doing.Bury, 1.4f + 0.6f * (float)_rng.NextDouble()); c.Clock = 0; break;
                        case Doing.Raid: c.Mind.Begin(Doing.Dig, 1.0f + 0.5f * (float)_rng.NextDouble()); c.Clock = 0; break;   // Decide finishes the raid when the dig's timer ends
                        case Doing.Chase: c.Mind.Begin(Doing.Still, 1.5f); c.Clock = 0; c.ChaseId = -1; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} gives up the chase"); break;
                        case Doing.Leave: c.Done = true; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} gone"); break;
                        case Doing.Go when c.Tree >= 0 && c.DenFoot is PointF df:
                            // Beside the tree: the second leg, up to the trunk's foot, without stopping.
                            c.DenFoot = null; c.Goal = df; c.Pace.Resume(arriveSpeed); c.Mind.Left = 6f;
                            break;
                        case Doing.Go when c.Tree >= 0:
                            // At the trunk: faces it, pitches up onto the bark.
                            c.Dir = TrunkX(c.Tree, c.Y) >= c.X ? 1 : -1;
                            c.Mind.Begin(Doing.Mount, MountFor); c.Clock = 0; c.Up = 0;
                            break;
                        case Doing.Go when c.ToPerch >= 0:
                        {
                            var (at, r) = _s.Pumpkins[c.ToPerch];
                            if ((at.X >= c.X ? 1 : -1) != c.Dir) { StartTurn(c); }   // it arrived along the row and the pumpkin is beside it: comes round first
                            c.Perched = c.ToPerch; c.ToPerch = -1;
                            c.FlyFrom = c.X; c.FlyTo = at.X - c.Dir * 0.12f * r; c.FlyY0 = c.Y; c.FlyY1 = at.Y - 0.98f * r;   // onto its shoulder, by the stem
                            c.Mind.Begin(Doing.HopUp, HopFor); c.Clock = 0;
                            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} hops up onto a pumpkin at {at.X:F0},{at.Y:F0}");
                            break;
                        }
                        case Doing.Go: case Doing.Bolt: c.Mind.Begin(Doing.Still, c.Kind == Kind.Bird ? 0.5f : 0.4f + 0.8f * (float)_rng.NextDouble()); c.Clock = 0; break;
                        default: if (c.Mind.Left > 4f) c.Mind.Left = 2f + 3f * (float)_rng.NextDouble(); break;
                    }
                    return;
                }
                // The pace eases toward the doing's speed and slows into the goal (Mover.cs); the heading turns at a rate, easing as it settles.
                float speed = c.Kind switch
                {
                    Kind.Mouse => c.Mind.Doing == Doing.Bolt ? 0.30f : 0.19f,
                    Kind.Bird => 0.12f,
                    _ => c.Mind.Doing switch { Doing.Bolt => 0.32f, Doing.Chase => 0.34f, Doing.Romp => 0.30f, Doing.Fetch => 0.28f, Doing.Leave => 0.26f, _ => 0.24f },
                } * u;
                // The slow-in: the wanted speed falls with the distance left (2.5 a second: from a squirrel's full pace it begins
                // easing about three lengths out, and arrives at a quarter pace; at 4 it braked in the last length and the stop was a halt).
                // A dart does not slow: it curls back at speed.
                c.Pace.Want = c.Dart || c.DenFoot != null ? speed : Math.Min(speed, MathF.Max(0.12f * speed, pathLeft * 2.5f));   // the floor 0.12: at 0.25 the last two lengths were a creep at one speed, not an ease; a dart and the first leg to a tree do not slow
                float want = MathF.Atan2(dy, dx);
                c.Heading = Turn.Toward(c.Heading, want, dist < 2f * _k ? 7f : 3.5f, dt);   // 3.5 a second on the way: a bend the eye can follow (7 was a corner); 7 in the last two lengths so a goal beside it is never orbited
                float err = MathF.IEEERemainder(want - c.Heading, MathF.Tau);
                float step = c.Pace.Tick(dt);
                if (step <= 0f) return;
                // A REVERSAL IS A CURL, at once: a wanted heading more than 100 degrees off the way it is going means the goal is
                // behind, and a squirrel on the move plants its forefeet and brings its back end round (CurlBack, the curl pages),
                // never arcs round through straight up or down the screen (at the eased turning rate that arc was a side-view
                // body sliding vertically for a third of a second: "90 turns").
                if (c.Kind == Kind.Squirrel && MathF.Abs(err) > 1.75f && c.Pace.Speed > 0.03f * u) { CurlBack(c, step, dt); return; }
                if (c.Kind == Kind.Squirrel)
                {
                    // A bound PULSES: the body is fastest in flight (0.5 to 0.9 of the bound's page cycle) and slowest as it
                    // lands and gathers over the forepaws; one speed all the way round was a glide. The pulse averages one,
                    // so the pace's number is still the squirrel's speed; the page cycle follows the pulsed distance, so the
                    // pictures and the motion agree. (Mice patter too fast for the eye to read a pulse.)
                    float p = c.Travelled / (2.2f * _k); p -= MathF.Floor(p);
                    step *= 0.55f + 0.45f * (1f + MathF.Sin(MathF.Tau * (p - 0.35f)));
                    // The stop-start: at the planned point of a long run it pulls up, sits and looks about, then goes on (Decide).
                    // Not a halt: the goal becomes a spot two lengths ahead, so the slow-in brings it to the sit (a halt here was a 5 px step to nothing).
                    if (c.PauseAt > 0f && c.Travelled >= c.PauseAt && dist > 3f * _k && c.Mind.Doing == Doing.Go)
                    {
                        c.PauseAt = 0f; c.Resume = c.Goal;
                        c.Goal = new PointF(c.X + MathF.Cos(c.Heading) * 2f * _k, c.Y + MathF.Sin(c.Heading) * 2f * _k);
                    }
                    // The dart: two lengths short of its goal, at speed, it picks a spot back the way it came and curls round.
                    if (c.Dart && dist < 2.5f * _k && c.Mind.Doing == Doing.Go)
                    {
                        c.Dart = false;
                        PointF me = new(c.X, c.Y);
                        PointF? back = c.Dir > 0 ? RandomSpot(c.X - 0.45f * u, c.X - 4f * _k, 30, c.Y, 0.05f * u, 0f, me) : RandomSpot(c.X + 4f * _k, c.X + 0.45f * u, 30, c.Y, 0.05f * u, 0f, me);
                        if (back != null) { c.Goal = back.Value; c.Mind.Begin(Doing.Go, 6f); CurlBack(c, step, dt); _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} darts back"); return; }
                    }
                }
                float x = c.X, y = c.Y, hd = c.Heading;
                var stepped = Ground.StepRound(_ground, ref x, ref y, ref hd, step, err);
                // A refused step is a jam, not a pause: after a few frames the doing is dropped and the critter sits (its goal
                // was behind something the step test will not cross). Six frames, not twenty: at twenty the run page hung
                // frozen mid-bound for a third of a second each time.
                if (stepped == Ground.Stepped.Refused) { if (++c.Refused > 6) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 0.5f); c.Refused = 0; c.Tree = -1; c.ToPerch = -1; _log?.WriteLine($"{_time,8:F3} {c.Kind.ToString().ToLowerInvariant()}{c.Id} refused at {c.X:F0},{c.Y:F0} going to {c.Goal.X:F0},{c.Goal.Y:F0}"); } return; }
                c.Refused = 0;
                c.X = x; c.Y = y; c.Heading = hd;
                c.Travelled += step;
                if (MathF.Abs(MathF.Cos(hd)) > 0.2f)
                {
                    int dir = MathF.Cos(hd) > 0 ? 1 : -1;
                    if (dir != c.Dir && c.Kind == Kind.Squirrel) { CurlBack(c, step, dt); return; }   // a squirrel on the move comes round in a curl (Update); a mouse at 13 px just faces the other way
                    c.Dir = dir;
                }
                if (c.Mind.Doing == Doing.Hide && c.Mind.Over) c.Mind.Begin(Doing.Still, 0.3f);
                break;
            }
            case Doing.Fly:
            {
                float p = FlightAlong(Math.Clamp(1f - c.Mind.Left / c.FlyFor, 0f, 1f));
                if (c.Flight != 0)
                {
                    // The branch flights. UP: a straight climb from below with the bound's dip riding on it (the same dip as
                    // any flight: beats lift it, the fold drops it), easing into the flare. DROP: the bird tips off the wood
                    // and glides, the first of the way mostly forward and the last mostly down (p to the 1.6), then flares.
                    c.X = c.FlyFrom + (c.FlyTo - c.FlyFrom) * p;
                    float qd = ((c.Clock % 0.5f) / 0.5f + 0.25f) % 1f;
                    float dipUp = c.Flight == 1 && qd < 0.4f ? 0.5f * _b * MathF.Sin(qd / 0.4f * MathF.PI) * 4 * p * (1 - p) : 0f;
                    float yp = c.Flight == 1 ? p : MathF.Pow(p, 1.6f);
                    c.Y = c.FlyY0 + (c.FlyY1 - c.FlyY0) * yp - dipUp;
                    if (c.Mind.Over)
                    {
                        if (c.Flight == 1) BeginBranch(c);
                        else
                        {
                            c.X = c.FlyTo; c.Y = c.FlyY1; c.Flight = 0;
                            c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : Doing.Still, 1f + 3f * (float)_perchRng.NextDouble()); c.Clock = 0;
                        }
                    }
                    break;
                }
                c.X = c.FlyFrom + (c.FlyTo - c.FlyFrom) * p;
                c.Y = c.FlyY0 + (c.FlyY1 - c.FlyY0) * p - FlightHeight(c, p, u);
                if (c.Mind.Over) { c.X = c.FlyTo; c.Y = c.FlyY1; c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : Doing.Still, 1f + 3f * (float)_rng.NextDouble()); c.Clock = 0; }
                break;
            }
            case Doing.Branch:
                BranchAct(c, dt);
                break;
            case Doing.Perch:
                if (c.Kind == Kind.Bird && c.Mind.Over && c.Perched >= 0 && _rng.NextDouble() < 0.6) c.Mind.Left = 1f + 3f * (float)_rng.NextDouble();   // often sits a while longer
                break;
            case Doing.HopUp:
            case Doing.HopDown:
            {
                // The squirrel's hop onto a pumpkin, or off it: the feet follow HopAt; the pages are by the clock (Draw).
                float k = _sq[c.Look].K;
                var (x, y) = HopAt(c, Math.Clamp(c.Clock / HopFor, 0f, 1f), c.Mind.Doing == Doing.HopUp ? 0.5f * k : 0.3f * k);
                c.X = x; c.Y = y;
                if (c.Mind.Over)
                {
                    c.X = c.FlyTo; c.Y = c.FlyY1;
                    if (c.Mind.Doing == Doing.HopUp) c.Mind.Begin(Doing.Perch, 3f + 5f * (float)_rng.NextDouble());
                    else { c.Perched = -1; c.Mind.Begin(Doing.Still, 0.5f + 0.8f * (float)_rng.NextDouble()); }
                    c.Clock = 0;
                }
                break;
            }
            case Doing.Mount:
                if (c.Mind.Over) { c.Mind.Begin(Doing.Climb, 30f); c.Up = 0; c.Clock = 0; c.Travelled = 0; }
                break;
            case Doing.Climb:
            {
                // Up the trunk at a steady pace, the pages by the distance climbed, on the OUTSIDE of the bark (nothing hides it),
                // until the top of its head reaches the hole's bottom rim; then it goes in.
                SqSheets sq = _sq[c.Look];
                float d = ClimbSpeed * u * dt;
                c.Up += d; c.Travelled += d;
                float edge = CrownEdge(c.Tree), headTop = c.Y - sq.UpFoot - c.Up - sq.UpHead;
                if (headTop <= edge)
                {
                    // ENTERING THE LEAVES is the same climb carried on: the body keeps climbing at the same pace and the props' stencil
                    // (the painted leaves) hides whatever has passed into the crown, so it goes in head first and the tail is the
                    // last thing seen, exactly as long as the body takes to pass the crown's edge.
                    c.Mind.Begin(Doing.Enter, (sq.UpHead + sq.UpFoot + 0.3f * sq.K) / (ClimbSpeed * u)); c.Clock = 0;
                }
                break;
            }
            case Doing.Enter:
            {
                float d = ClimbSpeed * u * dt;
                c.Up += d; c.Travelled += d;
                if (c.Mind.Over) { c.Mind.Begin(Doing.Den, 6f + 14f * (float)_rng.NextDouble()); c.Clock = 0; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} dens in tree {c.Tree}"); }
                break;
            }
            case Doing.Den:
                break;                                                              // out of sight; Decide ends it
            case Doing.Exit:
                if (c.Mind.Over)
                {
                    // Out of the leaves: head-first down the trunk from the crown's edge, round the trunk the other way, then off the bark.
                    c.Up = c.Y - CrownEdge(c.Tree) + 0.2f * _sq[c.Look].K;          // the head starts just inside the leaves and comes out of them as the descent begins (the stencil uncovers it)
                    c.Dir = -c.Dir;                                                  // the head-down pages face the other way from the climb's: the facing is handed over HERE, so the sit at the foot faces the way the body came down (it used to flip at the foot)
                    c.Mind.Begin(Doing.Descend, 30f); c.Clock = 0;
                }
                break;
            case Doing.Descend:
            {
                float k = _sq[c.Look].K;
                c.Up = MathF.Max(0f, c.Up - ClimbSpeed * u * dt); c.Travelled += ClimbSpeed * u * dt;
                if (c.Up <= 0f) { c.Mind.Begin(Doing.Dismount, MountFor); c.Clock = 0; }
                break;
            }
            case Doing.Dismount:
                if (c.Mind.Over) { c.X = _s.TreeHoles[c.Tree].At.X - c.Dir * Standoff * _s.U; c.Tree = -1; c.Mind.Begin(Doing.Still, 0.3f); c.Clock = 0; c.WasDown = false; c.Blend = 0; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} is down from the tree"); }   // the feet landed a stand-off behind the trunk (the dismount's last page), so the sit is drawn there; a short sit: the trunk's foot is behind the front pumpkins
                break;
        }
    }

    // ================================================================ drawing

    private byte NearnessOf(Critter c) => c.Perched >= 0 && c.Mind.Doing is Doing.Perch or Doing.HopUp or Doing.HopDown
        ? (byte)Math.Min(254, _s.Nearness(_s.Pumpkins[c.Perched].At.Y + 0.8f * _s.Pumpkins[c.Perched].R) + 1)
        : _s.Nearness(c.Y);

    private void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, Critter c, float y)
    {
        bool mirror = c.Dir < 0;
        int left = (int)MathF.Round(c.X) - (mirror ? sh.W - sh.Ox : sh.Ox);
        bool high = c.Kind == Kind.Bird && IsHigh(c);                          // up in the branches: no depth stencil, nothing of the near ground can hide it
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, null, mirror, high ? null : _s.NearDepth, NearnessOf(c));
    }

    /// <summary>A stamp at a given place, mirrored or not, through a stencil, in a given light: the climbing pages.</summary>
    private void StampAt(FrameBuffer fb, in Sheet sh, Sprite page, float x, float y, bool mirror, bool[]? stencil, Critter c, int shade = 256)
    {
        int left = (int)MathF.Round(x) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, stencil, mirror, _s.NearDepth, NearnessOf(c), shade);
    }

    /// <summary>A guest on the near ground drawn in the critters' own far-to-near order (the possum, when it is on the ground): its feet's y and its draw call. Set by the scene each frame; null when there is none.</summary>
    public Action<FrameBuffer>? Guest;
    public float GuestY;

    public void Draw(FrameBuffer fb)
    {
        if (!_ready) return;
        // Far to near. The guest takes its turn by its own feet's y, so a nearer squirrel is drawn over it and a farther one under.
        _all.Sort((a, b) => a.Y.CompareTo(b.Y));
        bool guestDrawn = Guest == null;
        foreach (Critter c in _all)
        {
            if (!guestDrawn && GuestY <= c.Y) { Guest!(fb); guestDrawn = true; }
            if (c.Kind == Kind.Bird && IsHigh(c)) continue;                         // a bird above the ground's crest is drawn in the second pass below
            switch (c.Kind)
            {
                case Kind.Mouse:
                {
                    if (c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide || (c.Mind.Doing == Doing.Nibble && c.Mind.Left > 4f))
                    {
                        float cyc = c.Travelled / (0.8f * _m);
                        Stamp(fb, _mouseRun, _mouseRun.Pages[(int)((cyc - MathF.Floor(cyc)) * 8) % 8], c, c.Y);
                    }
                    else Stamp(fb, _mouseStill, _mouseStill.Pages[(int)(c.Clock * (c.Mind.Doing == Doing.Nibble ? 6 : 12)) % 2], c, c.Y);
                    break;
                }
                case Kind.Bird:
                    DrawBird(fb, c);
                    break;
                default:
                {
                    SqSheets sq = _sq[c.Look];
                    float k = sq.K;
                    if (c.Turning && c.Curl)
                    {
                        // The running curl: in through the pages to the front view, flip, out again.
                        float q = c.TurnT / CurlFor;
                        int pg = q < 0.5f ? Math.Min(CurlPages - 1, (int)(q * 2 * CurlPages)) : Math.Min(CurlPages - 1, (int)((1 - q) * 2 * CurlPages));
                        Stamp(fb, sq.Curl, sq.Curl.Pages[pg], c, c.Y);
                        break;
                    }
                    if (c.Turning)
                    {
                        float t = c.TurnT, pre = c.TurnPre, post = c.TurnPost;
                        if (t < pre) Stamp(fb, sq.Rise, sq.Rise.Pages[Math.Min(RisePages - 1, (int)(t / pre * RisePages))], c, c.Y);                           // up to the sit (only after a run)
                        else if (t < pre + TurnFor)
                        {
                            float q = (t - pre) / TurnFor;                                                                                            // coming round through the three-quarter views to the front, facing the old way; then the same pages back, mirrored, facing the new
                            int pg = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                            Stamp(fb, sq.Turn, sq.Turn.Pages[pg], c, c.Y);
                        }
                        else if (post > 0f) Stamp(fb, sq.Rise, sq.Rise.Pages[Math.Max(0, RisePages - 1 - (int)((t - pre - TurnFor) / post * RisePages))], c, c.Y);   // down to the crouch (only before a run)
                        else Stamp(fb, sq.Rise, sq.Rise.Pages[RisePages - 1], c, c.Y);
                        break;
                    }
                    if (OnTree(c))
                    {
                        // On the trunk, drawn over its face the way a cartoon climber is: the feet a little past the middle on the side it came
                        // from, the body over the bark; going in and coming out through the den stencil. (With the feet on the bole's edge
                        // and the body out from it, it hung in the air beside the tree.)
                        bool mirror = c.Dir < 0;
                        // The climb runs up the HOLE's own line (the trunk's middle at the hole), so what the stencil hides is inside the
                        // hole and the tree, never bark beside it; the feet's side of the line is the side the body came up on.
                        // Every climbing page is stamped so that its painted body's middle column runs up the trunk's middle (the hole's
                        // column, which is the trunk's), and the spiral swings it from there (OnTrunkPlace). StampAt puts the sheet's
                        // origin column at x; unmirrored, a page column c lands at x - Ox + c; mirrored at x + Ox - 1 - c.
                        PointF holeAt = _s.TreeHoles[c.Tree].At;
                        float OverHole(in Sheet sh, int col, bool m) => m ? holeAt.X - sh.Ox + 1 + col : holeAt.X + sh.Ox - col;
                        switch (c.Mind.Doing)
                        {
                            case Doing.Mount:
                            {
                                // The feet stay where the run ended through the bend, then travel to the trunk's column as the hind end jumps up.
                                float f = c.Clock / MountFor, e = f < 0.55f ? 0f : Smooth01((f - 0.55f) / 0.45f);
                                float onTrunk = OverHole(sq.MountUp, sq.MountUpCol, mirror), x = c.X + (onTrunk - c.X) * e;
                                StampAt(fb, sq.MountUp, sq.MountUp.Pages[Math.Clamp((int)(f * MountPages), 0, MountPages - 1)], x, c.Y, mirror, null, c);
                                break;
                            }
                            case Doing.Climb:
                            case Doing.Enter:
                            {
                                // The climb, anchored by the measured feet (at Up 0 they stand on the ground; the mount's last page is this
                                // page at the foot). On the outside of the bark nothing hides it; once it is entering, the den stencil hides
                                // what has passed the hole's rim, so it goes in head first and the tail last.
                                float y = c.Y - sq.UpFoot - c.Up;
                                float cyc = c.Travelled / (2.2f * k);
                                var (x, stencil) = OnTrunkPlace(c, y, y - sq.UpHead, OverHole(sq.Up, sq.UpCol, mirror));
                                StampAt(fb, sq.Up, sq.Up.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], x, y, mirror, stencil, c);
                                break;
                            }
                            case Doing.Exit:
                                break;                                                                  // inside the leaves still (a moment's pause before the head comes out)
                            case Doing.Descend:
                            {
                                float y = c.Y - sq.DownHead - c.Up;                                     // head-first: anchored by the measured head, at the foot when Up is 0
                                float cyc = c.Travelled / (2.2f * k);
                                var (x, stencil) = OnTrunkPlace(c, y, y - sq.Down.By, OverHole(sq.Down, sq.DownCol, mirror));   // the body's top is the sheet's top row (the tail's tip, head down)
                                StampAt(fb, sq.Down, sq.Down.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], x, y, mirror, stencil, c);   // Dir was handed over at the exit
                                break;
                            }
                            case Doing.Dismount:
                            {
                                // Head down the trunk: the front half reaches level toward the ground, then the hind end comes down and the feet
                                // land a stand-off behind the trunk (where the Dismount's end puts c.X).
                                float f = c.Clock / MountFor, e = f < 0.55f ? 0f : Smooth01((f - 0.55f) / 0.45f);
                                float onTrunk = OverHole(sq.MountDown, sq.MountDownCol, mirror), landing = holeAt.X - c.Dir * Standoff * _s.U, x = onTrunk + (landing - onTrunk) * e;
                                StampAt(fb, sq.MountDown, sq.MountDown.Pages[Math.Clamp((int)(f * MountPages), 0, MountPages - 1)], x, c.Y, mirror, null, c);
                                break;
                            }
                        }
                        break;
                    }
                    if (c.Mind.Doing is Doing.HopUp or Doing.HopDown)
                    {
                        Stamp(fb, sq.Hop, sq.Hop.Pages[Math.Clamp((int)(c.Clock / HopFor * HopPages), 0, HopPages - 1)], c, c.Y);
                        break;
                    }
                    if (c.Blend > 0)
                    {
                        float f = Math.Clamp(c.Blend / BlendFor, 0f, 1f);                    // 1 at the start of the blend, 0 at its end
                        float upright = c.Sinking ? f : 1f - f;
                        Stamp(fb, sq.Rise, sq.Rise.Pages[(int)(upright * (RisePages - 0.01f))], c, c.Y);
                    }
                    else if (c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide or Doing.Fetch or Doing.Carry or Doing.Raid or Doing.Chase or Doing.Leave or Doing.Romp)
                    {
                        float cyc = c.Travelled / (2.2f * _k);
                        Sheet run = c.Nut != null ? sq.RunNut : sq.Run;                 // with a nut it bounds with the nut in its mouth
                        Stamp(fb, run, run.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], c, c.Y);
                    }
                    else if (c.Mind.Doing is Doing.Dig or Doing.Bury)
                        Stamp(fb, sq.Dig, sq.Dig.Pages[(int)(c.Clock * 3.2f * 8) % 8], c, c.Y);
                    else if (c.Mind.Doing == Doing.Eat)
                        Stamp(fb, sq.Eat, sq.Eat.Pages[new[] { 0, 1, 2, 3, 2, 1 }[(int)(c.Clock * 9f) % 6]], c, c.Y);   // chewing: the nut dips to the mouth and back, a beat and a half a second
                    else if (c.Nut != null)
                        Stamp(fb, sq.Eat, sq.Eat.Pages[0], c, c.Y);                     // sitting up holding it
                    else
                    {
                        // Sitting (on the ground, or on a pumpkin): the tail flicks now and then (a nervous one more often), and every
                        // few seconds (a curious one more often) it looks up for a moment, the head rising into the look over 0.2 s.
                        float flickEvery = 0.9f + 1.6f * (1f - c.Timid), lookEvery = 2.4f + 2.6f * (1f - c.Curious);
                        float ft = c.Clock % flickEvery;
                        int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                        float lt = c.Clock % lookEvery;
                        float look = lt < 1.3f || lt >= 2.6f ? 0f : Math.Min(1f, Math.Min((lt - 1.3f) / 0.2f, (2.6f - lt) / 0.2f));
                        if (look > 0f) Stamp(fb, sq.Look, sq.Look.Pages[Math.Min(LookPages - 1, (int)MathF.Round(look * (LookPages - 1)))], c, c.Y);
                        else Stamp(fb, sq.Sit[tail], sq.Sit[tail].Pages[0], c, c.Y);
                    }
                    break;
                }
            }
        }
        if (!guestDrawn) Guest!(fb);                                                // the guest is nearer than every critter: last
        // The second pass (2026-11-09): the birds up in the branches, and the birds flying to or from them above the fence line, in
        // front of the whole patch and of every pumpkin (no depth stencil), after the ground pass. The falling leaves are drawn
        // later by the scene, so they still fall in front of a bird on its branch.
        foreach (Critter c in _all)
            if (c.Kind == Kind.Bird && IsHigh(c)) DrawBird(fb, c);
    }

    /// <summary>One bird, in the page its doing calls for: flights (the branch flights have their own wings), hops, pecks, looks, preens.</summary>
    private void DrawBird(FrameBuffer fb, Critter c)
    {
        int kind = c.Look;
        if (c.Mind.Doing == Doing.Fly && c.Flight != 0)
        {
            // The branch flights. Both END in the flare (the last 0.3 s: wings thrown up, then the downstroke settling the bird).
            // UP also bounds like any flight (beats, then a shut-wing dart) but never folds in the take-off or the flare. DROP
            // beats its wings for the take-off off the wood, then glides with them half shut (the dive pose), then flares.
            float t = c.Clock, left = c.Mind.Left;
            if (left < FlareFor) Stamp(fb, _birdFlare[kind], _birdFlare[kind].Pages[left > 0.12f ? 0 : 1], c, c.Y);
            else if (c.Flight == 2 && t >= TakeOffFor) Stamp(fb, _birdDive[kind], _birdDive[kind].Pages[0], c, c.Y);
            else
            {
                bool fold = c.Flight == 1 && (t % 0.5f) > 0.4f && t > 0.33f && left > 0.33f;
                Stamp(fb, _birdFlyKind[kind], _birdFlyKind[kind].Pages[fold ? 6 : (int)(t * 24) % 6], c, c.Y);
            }
        }
        else if (c.Mind.Doing == Doing.Fly)
        {
            // Bounding flight: the wings beat for most of each half second (four beats a second, six pages a
            // beat) and shut for the last fifth of it, the dip in FlightHeight; never shut in the first or
            // last third of a second (the take-off and the flare are all beating).
            float t = c.Clock, left = c.Mind.Left;
            bool fold = (t % 0.5f) > 0.4f && t > 0.33f && left > 0.33f;
            Stamp(fb, _birdFlyKind[kind], _birdFlyKind[kind].Pages[fold ? 6 : (int)(t * 24) % 6], c, c.Y);
        }
        else if (c.Mind.Doing == Doing.Branch)
        {
            // Up on the wood. Sit: stands, and every second or so the head comes up to look about. Preen: the head turned right
            // round, the beak working at the shoulder (two pages, swapped eight times a second). Peck: at the bark by its feet.
            // Shuffle: the hop of a step along the branch, crouch, up (a small arc), land; the bird is lifted only for the drawing.
            if (c.Sub == SubShuffle)
            {
                float k = Math.Clamp(c.ShufT / ShuffleFor, 0f, 1f);
                int page = k < 0.2f ? 0 : k < 0.75f ? 1 : 2;
                float lift = k >= 0.2f && k < 0.75f ? 0.25f * _b * MathF.Sin((k - 0.2f) / 0.55f * MathF.PI) : 0f;
                Stamp(fb, _birdHop[kind], _birdHop[kind].Pages[page], c, c.Y - lift);
            }
            else if (c.Sub == SubPreen) Stamp(fb, _birdPreen[kind], _birdPreen[kind].Pages[(int)(c.Clock * 8f) % 2], c, c.Y);
            else if (c.Sub == SubPeck)
                Stamp(fb, (c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind], ((c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind]).Pages[0], c, c.Y);
            else
            {
                bool look = ((int)(c.Clock / 1.1f)) % 2 == 1;
                Stamp(fb, look ? _birdLook[kind] : _bird[kind], (look ? _birdLook[kind] : _bird[kind]).Pages[0], c, c.Y);
            }
        }
        else if (c.Mind.Doing is Doing.Go or Doing.Bolt)
        {
            // Hops: four a second, each a small arc; crouch, up, land.
            float hop = (c.Clock * 4f) % 1f;
            int page = hop < 0.2f ? 0 : hop < 0.7f ? 1 : 2;
            float lift = hop >= 0.2f && hop < 0.7f ? 0.25f * _b * MathF.Sin((hop - 0.2f) / 0.5f * MathF.PI) : 0f;
            Stamp(fb, _birdHop[kind], _birdHop[kind].Pages[page], c, c.Y - lift);
        }
        else if (c.Mind.Doing == Doing.Peck)
            Stamp(fb, (c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind], ((c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind]).Pages[0], c, c.Y);
        else
        {
            bool look = ((int)(c.Clock / 1.1f)) % 2 == 1;
            Stamp(fb, look ? _birdLook[kind] : _bird[kind], (look ? _birdLook[kind] : _bird[kind]).Pages[0], c, c.Y);
        }
    }
}
