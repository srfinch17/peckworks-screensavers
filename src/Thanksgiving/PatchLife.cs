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
    private enum Doing { Go, Still, Nibble, Hide, Peck, Fly, Perch, Dig, Bolt, Fetch, Carry, Bury, Eat, Raid, Chase, Leave, Mount, Climb, Enter, Den, Exit, Descend, Dismount, Romp, HopUp, HopDown }

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
        public bool Dart;                           // this run ends in a curl straight back the other way, at speed (playful)
        public int Tree = -1;                       // the tree a Go is heading for (to den), and the one it is on
        public int ToPerch = -1;                    // the pumpkin a Go is heading for (to hop up on)
        public float Up;                            // how far up the trunk, pixels
        public float NextDen;                       // the clock (PatchLife time) before which it does not den again: a den is a rest, not a hiding habit
        public PointF? DenFoot;                     // on the way to its tree: the trunk's foot, the second leg of the trip (the first runs along its own row to beside the tree)
    }

    private const float BlendFor = 0.15f;           // a squirrel drops into its run, or sits up out of it, over nine frames (it used to pop in one)
    private const int RisePages = 6, TurnPages = 6, CurlPages = 8, MountPages = 4, HopPages = 22;
    private const float TurnRise = 0.12f, TurnFor = 0.30f;   // a sitting turn: rise to the sit (0.12 s), the narrowing pages, the flip, the widening (0.30), sink (0.12)
    private const float CurlFor = 0.30f;            // the running curl: in and out again in a third of a second, never stopping
    private const float MountFor = 0.25f, HopFor = 0.55f, ClimbSpeed = 0.20f;   // ClimbSpeed in U a second
    private const int EnterPages = 10;
    private const float EnterFor = 0.6f, ExitFor = 1.2f;    // going in: the rear view shrinking down the hole; coming out: the face growing in it, then a look round

    private struct Sheet { public Sprite[] Pages; public int W, H, Ox, By; }

    /// <summary>A squirrel's coat and size (his "variations"): the sheets are painted once per variant.</summary>
    private sealed class SqSheets
    {
        public float K;                             // pixels to the squirrel unit for this variant
        public Sheet Run, RunNut, Look, Dig, Rise, Turn, Eat, Curl, Hop, Up, Down, MountUp, MountDown, Enter, Exit;
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
    private readonly bool[] _denMask;               // the stencil for a squirrel climbing into or out of a tree hole: hands off inside the trunk above the hole's bottom rim (the part of the body that is inside the tree)
    private readonly bool[] _denMaskHole;           // the stencil for the face in the opening: hands off everywhere BUT the holes' openings (a face growing in the dark is seen only through the hole)
    private Sheet _mouseRun, _mouseStill;
    private readonly SqSheets[] _sq = new SqSheets[Variants.Length];
    private const int LookPages = 4;                // Look: the head coming up into the look over four pages (page 0 the plain sit, page 3 the full look); it used to snap
    private Sheet[] _bird = new Sheet[2], _birdLook = new Sheet[2], _birdPeck = new Sheet[2], _birdHop = new Sheet[2];
    private Sheet[] _birdFlyKind = new Sheet[2];
    private volatile bool _ready;

    public PatchLife(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board, NutField nuts)
    {
        _s = s;
        _board = board;
        _nuts = nuts;
        _log = MindLog.Open();
        _rng = new Random(sceneRng.Next());         // its own dice
        _k = Math.Max(9f, s.U * 0.030f);   // the near ground's scale: a pumpkin of 0.4 m is about 0.05 U, so a squirrel of 0.25 m is 0.030 U (at 0.055 it was "too big"), a mouse 0.55 of that, a sparrow 0.020
        _m = _k * 0.55f;
        _b = Math.Max(7f, s.U * 0.020f);
        _ground = CanStand;                         // made once: a new delegate every frame is garbage
        _nuts.CanStand = _ground;                   // a nut lands only where a critter could stand
        // The den stencil: true everywhere but the INSIDE of each inner trunk above its hole's bottom rim (the trunk's own
        // width at each height, and the hole's opening). A climbing body drawn over the bark is hidden exactly where it
        // has gone into the tree; nothing beside the trunk is touched. (A flat band across the trunk and the air beside it
        // cut the squirrel along a line: "sliding up through some pocket dimension".)
        _denMask = new bool[s.Width * s.Height];
        Array.Fill(_denMask, true);
        for (int t = 0; t < s.TreeHoles.Count; t++)
        {
            var (at, hw) = s.TreeHoles[t];
            float hh = hw * 16f / 11f;
            int y1 = Math.Clamp((int)(at.Y + hh), 0, s.Height - 1);
            for (int y = 0; y < y1; y++)
            {
                float cx = TrunkX(t, y), half = TrunkHalf(t, y) * 1.05f;
                int x0 = Math.Max(0, (int)(cx - half)), x1 = Math.Min(s.Width - 1, (int)(cx + half));
                for (int x = x0; x <= x1; x++) _denMask[y * s.Width + x] = false;
            }
        }
        _denMaskHole = new bool[s.Width * s.Height];
        foreach (var (at, hw) in s.TreeHoles)
        {
            float hh = hw * 16f / 11f;
            for (int y = Math.Max(0, (int)(at.Y - hh)); y <= Math.Min(s.Height - 1, (int)(at.Y + hh)); y++)
                for (int x = Math.Max(0, (int)(at.X - hw)); x <= Math.Min(s.Width - 1, (int)(at.X + hw)); x++)
                {
                    float dx = (x - at.X) / hw, dy = (y - at.Y) / hh;
                    if (dx * dx + dy * dy <= 1f) _denMaskHole[y * s.Width + x] = true;
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
            _all.Add(c);
        }
        _log?.WriteLine($"{_time,8:F2} frenzy: {bring} squirrels come in");
    }

    // ================================================================ the world

    /// <summary>Can a critter stand here? On the near ground, and not inside a pumpkin's foot or the shock's foot.</summary>
    private bool CanStand(float x, float y)
    {
        float u = _s.U;
        if (x < -0.02f * _s.Width || x > 1.02f * _s.Width) return false;
        float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
        if (y < crest + 0.012f * u || y > _s.Height + 0.04f * u) return false;
        foreach (var (at, r) in _s.Pumpkins)
        {
            float foot = at.Y + 0.8f * r;
            if (MathF.Abs(x - at.X) < 1.05f * r && MathF.Abs(y - foot) < 0.35f * r) return false;
        }
        float walk = _s.WalkY(x);
        if (MathF.Abs(x - _s.CornShockFoot.X) < _s.CornShockHalfWidth + 0.01f * u && MathF.Abs(y - walk) < 0.04f * u) return false;
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
    private PointF? RandomSpot(float x0, float x1, int tries, float? yNear = null, float yReach = 0f, float minDx = 0f, PointF? from = null)
    {
        float u = _s.U, xm = (x0 + x1) / 2;
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
            foot = new PointF(trunk[0].X + side * 0.012f * _s.U, trunk[0].Y);
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

        for (int v = 0; v < Variants.Length; v++)
        {
            var (coat, scale) = Variants[v];
            float k = _k * scale;
            var sq = new SqSheets { K = k };
            int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
            Sheet Ground(int n, Func<int, SquirrelArt.Pose> pose) => Make(n, w, h, ox, by, i => { var q = pose(i); q.Coat = coat; return SquirrelArt.Page(q, k, w, h, ox, by); });
            sq.Run = Ground(32, i => SquirrelArt.Bound(i / 32f, 2.2f));   // 32: at 16 a bound 2.2 lengths long stepped the tail a seventh of a wave a page
            for (int t = 0; t < 4; t++) { int tl = t; sq.Sit[t] = Ground(1, _ => SquirrelArt.Sit(1f, tl, false, false)); }
            sq.Look = Ground(LookPages, i =>
            {
                // Between the sit and the look: the head, its tilt and the tail's root eased between the two poses.
                float f = i / (LookPages - 1f); f = f * f * (3f - 2f * f);
                SquirrelArt.Pose a = SquirrelArt.Sit(1f, 0, false, false), b = SquirrelArt.Sit(1f, 0, true, false);
                a.Head = new PointF(a.Head.X + (b.Head.X - a.Head.X) * f, a.Head.Y + (b.Head.Y - a.Head.Y) * f);
                a.HeadTilt += (b.HeadTilt - a.HeadTilt) * f; a.TailBase += (b.TailBase - a.TailBase) * f;
                return a;
            });
            sq.Dig = Ground(8, i => SquirrelArt.Dig(i / 8f));
            sq.Rise = Ground(RisePages, i => SquirrelArt.Sit(0.2f + 0.8f * i / (RisePages - 1), 0, false, false));   // the crouch (page 0) up to the sit (the last)
            sq.Turn = Ground(TurnPages, i => SquirrelArt.Turn(0.5f * (i + 1) / TurnPages));   // sitting, coming round from the profile to the front view; played back again mirrored
            sq.Curl = Ground(CurlPages, i => SquirrelArt.Curl(0.5f * (i + 1) / CurlPages));   // running, the same way round
            sq.RunNut = Ground(32, i => { var q = SquirrelArt.Bound(i / 32f, 2.2f); q.Acorn = 1; return q; });   // the bound with a nut in its mouth
            sq.Eat = Ground(4, i => { var q = SquirrelArt.Sit(1f, 0, false, true); q.Acorn = 2; q.Nib = i / 3f; return q; });   // sitting up with the nut in its paws, chewing
            sq.Hop = Ground(HopPages, i => SquirrelArt.Hop((i + 0.5f) / HopPages));   // the hop from a sit onto a pumpkin, and off again
            // Climbing (the caper's pages): head up the trunk, and head-first down it; pitching up onto the bark and off it.
            int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
            sq.Up = Make(32, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i => { var q = SquirrelArt.Bound(i / 32f, 2.2f, cling: true); q.Rotate = -90f; q.Coat = coat; return SquirrelArt.Page(q, k, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3); });
            sq.Down = Make(32, cw, ch, 3, (int)(1.75f * k) + 3, i => { var q = SquirrelArt.Bound(i / 32f, 2.2f, cling: true); q.Rotate = 90f; q.Coat = coat; return SquirrelArt.Page(q, k, cw, ch, 3, (int)(1.75f * k) + 3); });
            // Going in: the rear view, ever smaller about the head (the hole's middle), the last page a third the size. Coming
            // out: the sitting front view (the symmetric middle of the turn) growing from a third to full, the head at the hole.
            int ew = (int)(2.0f * k) + 6, eo = ew / 2, eb = (int)(1.3f * k) + 3;
            sq.Enter = Make(EnterPages, ew, ew, eo, eb, i => { float sc = 1f - 0.68f * i / (EnterPages - 1f); return Sprite.Paint(ew, ew, g => { g.TranslateTransform(eo, eb); g.ScaleTransform(k * sc, -k * sc); SquirrelArt.PaintRear(g, coat); }); });
            sq.Exit = Make(EnterPages, ew, ew, eo, eb, i =>
            {
                float sc = 0.32f + 0.68f * i / (EnterPages - 1f);
                var q = SquirrelArt.Turn(0.5f, false); q.Coat = coat;
                return Sprite.Paint(ew, ew, g => { g.TranslateTransform(eo, eb); g.ScaleTransform(k * sc, -k * sc); g.TranslateTransform(-q.Head.X, -q.Head.Y); SquirrelArt.Paint(g, q); });
            });
            int sqw = (int)(4.0f * k) + 6, so = sqw / 2;
            sq.MountUp = Make(MountPages, sqw, sqw, so, so, i => { var q = i < MountPages - 1 ? SquirrelArt.Sit(0.2f, 0, false, false) : SquirrelArt.Bound(0f, 2.2f, cling: true); q.Rotate = -90f * (i + 1) / MountPages; q.Coat = coat; return SquirrelArt.Page(q, k, sqw, sqw, so, so); });
            sq.MountDown = Make(MountPages, sqw, sqw, so, so, i => { var q = i == 0 ? SquirrelArt.Bound(0f, 2.2f, cling: true) : SquirrelArt.Sit(0.2f, 0, false, false); q.Rotate = 90f * (1f - (i + 1) / (float)MountPages); q.Coat = coat; return SquirrelArt.Page(q, k, sqw, sqw, so, so); });
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
        }
    }

    // ================================================================ living

    private static bool OnTree(Critter c) => c.Mind.Doing is Doing.Mount or Doing.Climb or Doing.Enter or Doing.Den or Doing.Exit or Doing.Descend or Doing.Dismount;
    private static bool OnPumpkin(Critter c) => c.Mind.Doing is Doing.HopUp or Doing.Perch or Doing.HopDown;

    public void Update(float dt)
    {
        _time += dt;                                // counted from the first frame, not from the sheets being ready: the log's clock is the frame dump's (the build took 0.4 s and every logged second was 0.4 s early)
        if (!_ready) return;
        for (int i = 0; i < _all.Count; i++)
        {
            Critter c = _all[i];
            c.Mind.Tick(dt);
            c.Senses.Tick(dt);
            c.Clock += dt;
            _log?.WriteLine($"{_time,8:F3} {c.Kind.ToString().ToLowerInvariant()}{c.Id} frame x {c.X:F1} y {c.Y:F1} v {c.Pace.Speed:F2} {c.Mind.Doing}{(c.Turning ? (c.Curl ? " curl" : " turn") : "")}");
            // A squirrel on the move is a thing the mice and birds notice: it tells the board every third of a second.
            if (c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Fetch or Doing.Carry or Doing.Raid or Doing.Chase or Doing.Leave or Doing.Romp && c.Clock >= c.NextPost)
            {
                _board.Post(Sense.Movement, new PointF(c.X, c.Y), 0.14f * _s.U, 0.9f, c);
                c.NextPost = c.Clock + 0.33f;
            }
            // The senses: a mouse bolts from a squirrel or a scuffle, freezes at a noise; a bird flies up; a squirrel sits up and looks.
            // A squirrel up a tree or on a pumpkin, or in the middle of a game, is past noticing.
            if (c.Mind.Doing is not (Doing.Bolt or Doing.Fly or Doing.Chase or Doing.Leave or Doing.Romp) && !OnTree(c) && !OnPumpkin(c))
            {
                Reaction r = c.Senses.Check(_board, new PointF(c.X, c.Y), c, out Notice cause);
                // A squirrel busy with a nut (chewing, burying, digging one up) is not put off its work by a noise or a passing
                // squirrel: it only looks that way. (Every freeze restarted the meal: one squirrel chewed one nut for 31 s
                // through a frenzy bounding round it.)
                bool busyWithNut = c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Eat or Doing.Bury or Doing.Dig;
                if (r == Reaction.Flee && !busyWithNut) Startle(c, cause.At);
                else if (r is Reaction.Flee or Reaction.Freeze && busyWithNut) { if (MathF.Abs(MathF.Cos(c.Heading)) > 0.2f || c.Mind.Doing == Doing.Eat) c.Dir = cause.At.X >= c.X ? 1 : -1; }
                else if (r == Reaction.Freeze) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 1f + 1.5f * c.Timid); c.Clock = 0; c.Turning = false; c.Curl = false; }
                else if (r == Reaction.Look && c.Mind.Doing is Doing.Still or Doing.Peck or Doing.Perch) { c.Clock = 1.1f; c.Mind.Left = Math.Max(c.Mind.Left, 0.8f); if (c.Kind != Kind.Bird && c.Perched < 0) c.Dir = cause.At.X >= c.X ? 1 : -1; }
            }
            if (c.Turning)
            {
                // The turn on the spot: the body rises to the sit, narrows through the front view, flips, widens, and sinks; the
                // doing's own clock waits so nothing else moves meanwhile. The running curl has no rise and no sink, and keeps
                // moving: it carries on along the old way, dying out by the middle, and gathers out along the new way.
                float was = c.TurnT; c.TurnT += dt; c.Mind.Left += dt;
                float rise = c.Curl ? 0f : TurnRise, span = c.Curl ? CurlFor : TurnFor;
                float mid = rise + span / 2;
                if (c.Curl)
                {
                    float q = c.TurnT / CurlFor;
                    float v = q < 0.5f ? c.TurnV * (1f - 2f * q) : 0.6f * c.TurnV * (2f * q - 1f);
                    float nx = c.X + c.Dir * v * dt;
                    if (_ground(nx, c.Y)) c.X = nx;
                }
                if (was < mid && c.TurnT >= mid) c.Dir = -c.Dir;
                if (c.TurnT >= rise + span + rise)
                {
                    c.Turning = false; c.Blend = 0;
                    if (c.Curl) { c.Curl = false; c.Pace.Resume(0.6f * c.TurnV); c.Heading = MathF.Atan2(c.Goal.Y - c.Y, c.Goal.X - c.X); }
                }
                continue;
            }
            Act(c, dt);
            if (c.Mind.Over) Decide(c);
            // A squirrel never pops from its sit into a bound or back (one frame from upright to stretched flat was the
            // patch's one real jump): the moment its doing changes class (down on all fours, or sitting up) it plays a
            // short blend through the crouch pages, sinking into the run or rising out of it. The pace is still easing
            // in those frames, so the sink plays nearly on the spot. (The tree and pumpkin doings have their own pitches.)
            bool down = c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide or Doing.Dig or Doing.Fetch or Doing.Carry or Doing.Bury or Doing.Raid or Doing.Chase or Doing.Leave or Doing.Romp;
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
        NutField.Nut? loose = _nuts.NearestLoose(me, c.Visitor ? 1.6f * u : 0.8f * u);
        if (loose != null && Ground.LineClear(_ground, me, new PointF(loose.X, loose.Y), 2f))
        {
            c.Target = loose; c.Goal = new PointF(loose.X, loose.Y); c.Mind.Begin(Doing.Fetch, 8f); c.Travelled = 0; Head(c);
            return true;
        }
        NutField.Nut? stash = _nuts.NearestStash(c.Id, me, 0.35f * u);
        if (stash != null && _rng.NextDouble() < 0.25 + 0.5 * (1 - c.Timid) && Ground.LineClear(_ground, me, new PointF(stash.X, stash.Y), 2f))
        {
            c.Target = stash; c.Goal = new PointF(stash.X, stash.Y); c.Mind.Begin(Doing.Raid, 8f); c.Travelled = 0; Head(c);
            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} goes for squirrel{stash.Owner}'s stash");
            return true;
        }
        if (c.Visitor && _nuts.LooseCount == 0 && _nuts.FallingCount == 0)
        {
            if (_rng.NextDouble() < 0.4) { c.Mind.Begin(Doing.Still, 1f + 2f * (float)_rng.NextDouble()); return true; }   // lingers a moment first: sits up, looks about
            float edge = c.EntrySide < 0 ? -0.015f * _s.Width : 1.015f * _s.Width;
            PointF? off = RandomSpot(edge - 0.004f * _s.Width, edge + 0.004f * _s.Width, 20, c.Y, 0.05f * u, 0f, me);
            if (off == null) { c.Mind.Begin(Doing.Still, 1f); return true; }
            c.Goal = off.Value; c.Mind.Begin(Doing.Leave, 12f); c.Travelled = 0; Head(c);
            _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} leaves");
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
        if (c.Kind == Kind.Squirrel) { c.Turning = true; c.Curl = false; c.TurnT = 0; }
        else c.Dir = dir;
    }

    /// <summary>
    /// A squirrel on the move turning back the other way: the running curl, started at the speed it has now. The
    /// doing goes on toward the (new) goal when the curl ends.
    /// </summary>
    private void CurlBack(Critter c)
    {
        c.Turning = true; c.Curl = true; c.TurnT = 0; c.TurnV = MathF.Max(c.Pace.Speed, 0.1f * _s.U);
        c.Pace.Halt();
        c.Refused = 0;
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
        if (tree < 0) return false;
        c.Tree = tree; c.Mind.Begin(Doing.Go, 10f); c.Travelled = 0; c.PauseAt = 0; c.Dart = false; c.DenFoot = null;
        float dy = c.Y - foot.Y, side = c.X < foot.X ? -1f : 1f;
        var beside = new PointF(foot.X + side * MathF.Max(0.012f * _s.U, 1.1f * dy), c.Y);
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
        if (_test == "den" && c.Kind == Kind.Squirrel && !c.Visitor && _time < 20 && c.Nut == null && GoDen(c)) return;   // the test aid, ahead of everything
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
                    if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; }
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
        float k = _sq[c.Look].K;
        foreach (int dir in new[] { c.Dir, -c.Dir })
        {
            var to = new PointF(at.X + dir * (1.05f * r + 0.6f * k), at.Y + 0.8f * r + 0.08f * r);
            if (!CanStand(to.X, to.Y)) continue;
            c.Dir = dir;
            c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
            c.Mind.Begin(Doing.HopDown, HopFor); c.Clock = 0;
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
                float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
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
                            c.Dir = at.X >= c.X ? 1 : -1;
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
                c.Pace.Want = c.Dart || c.DenFoot != null ? speed : Math.Min(speed, MathF.Max(0.12f * speed, dist * 2.5f));   // the floor 0.12: at 0.25 the last two lengths were a creep at one speed, not an ease; a dart and the first leg to a tree do not slow
                float want = MathF.Atan2(dy, dx);
                c.Heading = Turn.Toward(c.Heading, want, 7f, dt);
                float err = MathF.IEEERemainder(want - c.Heading, MathF.Tau);
                float step = c.Pace.Tick(dt);
                if (step <= 0f) return;
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
                        if (back != null) { c.Goal = back.Value; c.Mind.Begin(Doing.Go, 6f); CurlBack(c); _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} darts back"); return; }
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
                    if (dir != c.Dir && c.Kind == Kind.Squirrel) { CurlBack(c); return; }   // a squirrel on the move comes round in a curl (Update); a mouse at 13 px just faces the other way
                    c.Dir = dir;
                }
                if (c.Mind.Doing == Doing.Hide && c.Mind.Over) c.Mind.Begin(Doing.Still, 0.3f);
                break;
            }
            case Doing.Fly:
            {
                float p = FlightAlong(Math.Clamp(1f - c.Mind.Left / c.FlyFor, 0f, 1f));
                c.X = c.FlyFrom + (c.FlyTo - c.FlyFrom) * p;
                c.Y = c.FlyY0 + (c.FlyY1 - c.FlyY0) * p - FlightHeight(c, p, u);
                if (c.Mind.Over) { c.X = c.FlyTo; c.Y = c.FlyY1; c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : Doing.Still, 1f + 3f * (float)_rng.NextDouble()); c.Clock = 0; }
                break;
            }
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
                // Up the trunk at a steady pace, the pages by the distance climbed, until the head is at the hole; then it goes in.
                float k = _sq[c.Look].K;
                float d = ClimbSpeed * u * dt;
                c.Up += d; c.Travelled += d;
                float hole = _s.TreeHoles[c.Tree].At.Y;
                if (c.Y - c.Up < hole + 1.45f * k) { c.Mind.Begin(Doing.Enter, EnterFor); c.Clock = 0; }
                break;
            }
            case Doing.Enter:
                if (c.Mind.Over) { c.Mind.Begin(Doing.Den, 6f + 14f * (float)_rng.NextDouble()); c.Clock = 0; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} dens in tree {c.Tree}"); }
                break;
            case Doing.Den:
                break;                                                              // out of sight; Decide ends it
            case Doing.Exit:
                if (c.Mind.Over)
                {
                    // Out of the hole: head-first down the trunk from the hole's bottom rim, then off the bark.
                    var (at, hw) = _s.TreeHoles[c.Tree];
                    c.Up = c.Y - (at.Y + hw * 16f / 11f) - 0.1f * _sq[c.Look].K;
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
                if (c.Mind.Over) { c.Tree = -1; c.Mind.Begin(Doing.Still, 0.3f); c.Clock = 0; c.WasDown = false; c.Blend = 0; _log?.WriteLine($"{_time,8:F2} squirrel{c.Id} is down from the tree"); }   // a short sit: the trunk's foot is behind the front pumpkins, a long sit there is a tail out of a pumpkin
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
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, null, mirror, _s.NearDepth, NearnessOf(c));
    }

    /// <summary>A stamp at a given place, mirrored or not, through a stencil, in a given light: the climbing pages.</summary>
    private void StampAt(FrameBuffer fb, in Sheet sh, Sprite page, float x, float y, bool mirror, bool[]? stencil, Critter c, int shade = 256)
    {
        int left = (int)MathF.Round(x) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, stencil, mirror, _s.NearDepth, NearnessOf(c), shade);
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_ready) return;
        // Far to near.
        _all.Sort((a, b) => a.Y.CompareTo(b.Y));
        foreach (Critter c in _all)
        {
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
                {
                    int kind = c.Look;
                    if (c.Mind.Doing == Doing.Fly)
                    {
                        // Bounding flight: the wings beat for most of each half second (four beats a second, six pages a
                        // beat) and shut for the last fifth of it, the dip in FlightHeight; never shut in the first or
                        // last third of a second (the take-off and the flare are all beating).
                        float t = c.Clock, left = c.Mind.Left;
                        bool fold = (t % 0.5f) > 0.4f && t > 0.33f && left > 0.33f;
                        Stamp(fb, _birdFlyKind[kind], _birdFlyKind[kind].Pages[fold ? 6 : (int)(t * 24) % 6], c, c.Y);
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
                    break;
                }
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
                        float t = c.TurnT;
                        if (t < TurnRise) Stamp(fb, sq.Rise, sq.Rise.Pages[Math.Min(RisePages - 1, (int)(t / TurnRise * RisePages))], c, c.Y);                // up to the sit
                        else if (t < TurnRise + TurnFor)
                        {
                            float q = (t - TurnRise) / TurnFor;                                                                                       // narrowing facing the old way, widening facing the new
                            int pg = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                            Stamp(fb, sq.Turn, sq.Turn.Pages[pg], c, c.Y);
                        }
                        else Stamp(fb, sq.Rise, sq.Rise.Pages[Math.Max(0, RisePages - 1 - (int)((t - TurnRise - TurnFor) / TurnRise * RisePages))], c, c.Y);   // down to the crouch
                        break;
                    }
                    if (OnTree(c))
                    {
                        // On the trunk, drawn over its face the way a cartoon climber is: the feet a little past the middle on the side it came
                        // from, the body over the bark; going in and coming out through the den stencil. (With the feet on the bole's edge
                        // and the body out from it, it hung in the air beside the tree.)
                        bool mirror = c.Dir < 0;
                        float Edge(float y, bool m) => TrunkX(c.Tree, y) + (m ? TrunkHalf(c.Tree, y) : -TrunkHalf(c.Tree, y)) * 0.2f;   // m: the stamp's own mirror (the body lies on the side the sheet puts it)
                        switch (c.Mind.Doing)
                        {
                            case Doing.Mount:
                                StampAt(fb, sq.MountUp, sq.MountUp.Pages[Math.Clamp((int)(c.Clock / MountFor * MountPages), 0, MountPages - 1)], Edge(c.Y, mirror), c.Y, mirror, null, c);
                                break;
                            case Doing.Climb:
                            {
                                float y = c.Y - 1.75f * k - 3 - c.Up;                                   // the pages' reference is the head end; the feet stand at the foot when Up is 0
                                float cyc = c.Travelled / (2.2f * k);
                                StampAt(fb, sq.Up, sq.Up.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], Edge(y + 0.9f * k, mirror), y, mirror, _denMask, c);
                                break;
                            }
                            case Doing.Enter:
                            {
                                // Head in first: seen from behind at the hole, shrinking down it into the dark (the light falls away with the size).
                                float q = Math.Clamp(c.Clock / EnterFor, 0f, 0.999f);
                                int pg = (int)(q * EnterPages);
                                var (at, _) = _s.TreeHoles[c.Tree];
                                StampAt(fb, sq.Enter, sq.Enter.Pages[pg], at.X, at.Y, false, null, c, 256 - (int)(190 * q));
                                break;
                            }
                            case Doing.Exit:
                            {
                                // The face coming up out of the dark, growing to full size in the opening, then held there for the look round.
                                float q = Math.Clamp(c.Clock / (ExitFor * 0.5f), 0f, 0.999f);
                                int pg = (int)(q * EnterPages);
                                var (at, _) = _s.TreeHoles[c.Tree];
                                StampAt(fb, sq.Exit, sq.Exit.Pages[pg], at.X, at.Y, false, _denMaskHole, c, 70 + (int)(186 * q));
                                break;
                            }
                            case Doing.Descend:
                            {
                                float y = c.Y - 0.8f * k - 3 - c.Up;                                    // head-first: the reference is the head end, at the foot when Up is 0
                                float cyc = c.Travelled / (2.2f * k);
                                StampAt(fb, sq.Down, sq.Down.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], Edge(y - 0.9f * k, !mirror), y, !mirror, _denMask, c);
                                break;
                            }
                            case Doing.Dismount:
                                StampAt(fb, sq.MountDown, sq.MountDown.Pages[Math.Clamp((int)(c.Clock / MountFor * MountPages), 0, MountPages - 1)], Edge(c.Y, !mirror), c.Y, !mirror, null, c);
                                break;
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
    }
}
