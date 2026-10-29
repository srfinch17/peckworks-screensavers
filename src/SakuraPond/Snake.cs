using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// Now and then a snake crosses: a shimahebi (the Japanese four-lined rat
/// snake, olive with four dark stripes running down its length), a good
/// swimmer and a common sight at ponds. It comes out of the grass on one
/// side, swims across the pond and slips away out of the other side.
///
/// HOW A SNAKE SWIMS: its head weaves side to side as it goes forward, and
/// every part of its body passes through exactly the same points the head
/// did, like a line of people following a leader through a winding path.
/// So we move only the head (forward along its heading, weaving), remember
/// where it has been, and lay the body along that trail (Body.cs draws it
/// bending). Its head pushes a small V-shaped wake out across the water.
///
/// HOW IT STEERS: it is not on rails. It has somewhere it wants to get to
/// (a point beyond the far side of the screen) and it feels its way there,
/// a little like the koi: each frame it looks a body length ahead, and if a
/// lily pad, a rock, the log, the cattails or an iris clump is in the way it
/// tries a few headings either side and takes the smallest turn that clears
/// the thing, then settles back toward its goal. The body follows the head,
/// so the whole snake flows round the pad rather than cutting across it.
/// On land the same rule keeps it off the rocks and the log.
///
/// AND IT IS NERVOUS: if a koi, a turtle or the lizard comes close enough to
/// touch any part of it, it startles: a burst of speed, a swing away from
/// whatever touched it, the weave quickening with the speed, rings thrown
/// off its head, then over a few seconds it calms back to its unhurried
/// crossing. It will not startle again for a while, so a koi that keeps
/// pace with it does not set it off thirty times a second.
///
/// AND IT HAS A MIND, a small one, like a koi's. Each visit is a new snake
/// with two dials rolled at the door: BOLDNESS (hunts more, lies in the sun
/// longer, startles only at a closer touch and calms sooner) and NERVES (the
/// opposite: crosses sooner, startles at a longer reach, stays frightened
/// longer, swims a little faster). It does one thing at a time (a Doing) and
/// when that is over it rolls weighted dice for the next:
///   Cross    swim for the far side of the screen and leave (the old way, and
///            the only way out);
///   Explore  swim to a spot in open water, then another, before leaving;
///   Hunt     creep up on a school of minnows at half speed, then strike: a
///            half-second dash at the school with a ring from its head (the
///            minnows already flee a snake's head, so they scatter on their own);
///   Bask     swim to the end of the floating log, climb on along its length,
///            lie there still for 10 to 30 seconds with a gentle S in its body,
///            then slip off the far end with a couple of rings.
/// Underneath, none of that changes how it steers: the mind only says where
/// to go and how fast. The feelers, the "never stuck" rule and the startle
/// still have the last word (except that the log is not an obstacle to a
/// snake that has come to lie on it).
///
/// FOUR SKINS (five until round thirty-nine). Every visit is also a different
/// KIND of snake, one of four that really swim in Japanese ponds, seen from
/// above (the back and the top of the head; the belly is never seen). The skin
/// is worked out from the two dials rolled at the door, never from a fresh roll
/// (one extra roll would change every later roll in the scene, and the owner
/// compares builds at one seed):
///   0 shimahebi    the four-lined rat snake: olive with four dark stripes;
///   1 aodaisho     the Japanese rat snake: plain olive green, faint darker
///                  blotches in a loose chain down the spine, a pale grey-green head;
///   2 yamakagashi  DROPPED 2026-10-28 at the owner's word ("drop the checkered
///                  one"): the tiger keelback's checker of black and rust read as
///                  a toy; the number is kept so the other skins keep their dials;
///   3 hibakari     a plain warm brown with a thin cream line along each side of
///                  the neck and a darker head;
///   4 karasuhebi   the black shimahebi: near black, the stripes only just blacker,
///                  a blue sheen down the ridge of the back.
/// The marks are looked up from (s, v), how far along and how far across, so
/// they ride along as the body bends, like the koi's patches. WHAT MAKES IT READ
/// AS A SNAKE AND NOT A ROPE (round thirty-nine, "make the snakes look more
/// natural"): a head wider than the neck with a real neck behind it (Profile), a
/// rounder body (the edges well darker than the ridge, a gleam down the ridge),
/// a lattice of scale rows a few percent light and dark (two crossing waves,
/// never drawn scales: at this size a drawn scale is a dot of noise), marks with
/// soft rounded edges, each its own size (the old blotches were hard rectangles,
/// the stripes hard bands), and the body behind the head a hair under the
/// surface, veiled by the water, while the head rides clear.
///
/// AND A HUNT OF ITS OWN (the order it happens in). A lizard or a mouse that is
/// OUT and comes within 0.14u of the snake's head, ahead of it, while the snake
/// is on the bank or at the water's edge, is a MEETING. The snake rolls once per
/// animal per visit whether to go for it (a bold snake more often). If it does:
///   Stalk    it goes still and CREEPS, head low and level, a third of its speed,
///            the weave almost gone, straight at the prey (the feelers stay on, so
///            it goes round a rock). The prey's own eyes decide the rest: one that
///            has seen the snake runs or freezes, and the stalk is dropped when it
///            gets more than 0.2u away; one facing away, basking, drinking or frozen
///            is caught up with;
///   Strike   at 0.05u it pulls its neck back into a tight S for a third of a second,
///            then the head shoots forward a tenth of a unit in a tenth of a second
///            with the mouth open (the head drawn half again as wide). The outcome is
///            rolled at the launch, 50 percent, plus 15 for a bold snake, minus 15 for
///            a bold lizard or a nervous mouse (they dodge better), held to 25 to 80;
///   Swallow  (success) the prey is held by the head, thrashes for a second, then goes
///            in head first over a few seconds (the head is painted over it and its
///            drawn body is cut shorter from the nose end); the tail goes last, still
///            twitching. Then a BULGE the size of the meal travels slowly down the
///            snake from behind the head to the middle and shrinks away. A fed snake
///            is slow, hunts no more, and would sooner bask or leave;
///   Miss     (failure) at the launch the prey DODGES (a lizard flings itself aside, a
///            mouse jumps straight up and lands off to the side), the jaws close on
///            nothing, the snake holds a second looking about, chases for three
///            seconds and gives up. A snake strikes at most twice in a visit.
/// The prey is told what is happening through IPrey (the bottom of this file); a
/// lizard or mouse that is eaten is gone, and the Lizards and Mice wrappers bring
/// a new one in a few minutes later.
///
/// AND A CHANCE MEAL OF TADPOLES (Snatch, in Tadpoles.cs). Nothing here goes LOOKING for the tadpoles
/// (his rule). But a snake that is simply swimming across or about, with its head on the water and a
/// tadpole or froglet at the top of the water within 0.15u of its nose (u is the screen's short side), may single that
/// one out: a few dice a second (a bold snake sooner), then a few seconds of chase at a brisk pace, and
/// a lunge (a ring off the head, then a dash much quicker than the minnow hunt's: a tadpole bolts at about
/// twice the snake's cruise, so only a strike can ever reach one) whenever it is close and facing it.
/// It only notices what is in front of its head. A catch is a mouthful, not a meal: the snake is NOT "fed" (that is the lizard's bulge).
/// It rests four to eight minutes after any chase, caught or not. The chase is only a goal and a pace: the
/// feelers, the wall of the picture's edge and the "never stuck" step rule steer it as always, so a
/// tadpole behind a rock is simply not reached, and a touch from another animal ends the chase.
///
/// AND AN APPETITE (round twenty-seven; the owner: "occasionally if it feels hungry, it will eat one or more of these
/// things"). Think of anyone who has skipped lunch: they do not stalk the fridge at once, but as the hours go by they
/// start looking, they notice food from further off, and they will have a snack, then another, until they are full.
/// The snake keeps a HUNGER dial from 0 (full) to 1 (famished) across visits. It creeps up with the clock (from 0 to
/// 1 in eight to fourteen minutes of pond time, a bolder snake sooner, worked out from the two dials and never rolled)
/// and a meal brings it down: a lizard or a mouse to nothing, a tadpole or froglet by a fifth, a minnow by an eighth.
/// What hunger changes: above a half the wait before the next visit shrinks (at 1 it is half as long); the prowl's
/// face in the dice grows, prey is noticed from further off and the dice for going to look land more often; the
/// tadpole look is likelier and the rest after a chase shorter; below 0.3 it never strikes at a minnow, above it the
/// minnow Hunt is a real lunge that TAKES the fish its jaws reach (Minnows.Take, a quick gulp and a small ring, no
/// bulge: a minnow is a mouthful), and a hungry snake hunts again, up to two or three strikes a visit, until it is
/// under 0.3 again. The lizard and mouse hunt itself (Stalk, Strike, Swallow, Miss) is exactly as before.
/// </summary>
internal sealed class Snake : Body
{
    private const int Points = 44;
    private const float SnatchLunge = 0.35f;              // seconds a lunge at a tadpole lasts (at 0.6u a second: up to 0.21u, from 0.12u away)

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often, _len, _wide;
    private readonly PointF[] _trail = new PointF[400];
    private int _trailHead, _trailCount;
    // THE DIVE (2026-10-27): the head's depth, where it is going, the depth the head had at each remembered spot (so the body
    // follows it down and up the way the koi's does, Koi._trailZ), each spine point's depth and haze this frame, and the
    // stencil for a deep snake (Open less the stones' collars and caps, Pond.StoneTop, which stand in front of it).
    private readonly float[] _trailZ = new float[400];
    private float _zHead, _zTarget;
    private readonly float[] _zAt = new float[Points], _murkAt = new float[Points];
    private readonly bool[] _openDeep;

    // Everything on the pond it must go round, as circles (lily pads, rocks,
    // the cattail clump, the iris clumps) plus the log as a thick line.
    private readonly PointF[] _obsCentre;
    private readonly float[] _obsRadius;
    // MOVING things it keeps off as it swims: the ducks' bodies this frame, as circles (the scene sets them before Update).
    // A duck's own feelers keep it off the snake only while the duck moves; this keeps the snake off a duck that sits still.
    private readonly float[] _mvX = new float[160], _mvY = new float[160], _mvR = new float[160];
    private int _mvN;
    private readonly Log? _log;
    private readonly Minnows? _minnows;                    // what it may hunt (null when there are no schools)

    // ---- THE MIND ----
    // Two dials rolled at the start of each visit, 0 to 1 (see the class comment),
    // and what they turn into: how fast it cruises and how long a fright lasts.
    private float _bold, _nerve, _cruise, _fleeFor;
    private enum Doing { Cross, Explore, Hunt, Bask, Stalk, Strike, Swallow, Miss, Snatch }
    private readonly Mind<Doing> _mind = new();          // the doing now and how long before it gives it up (Core/Minds)
    private Doing _doing => _mind.Doing;
    private int _phase;                                    // where in the doing it is (Hunt: 0 stalk, 1 strike. Bask: 0 swim to the log, 1 climb on, 2 lie, 3 slip off)
    private int _exploreLeft, _huntsLeft, _legs;           // what the visit still has on its list, and how many doings it has had
    private bool _basked;                                  // one lie on the log per visit
    private float _strikeT, _lieT, _lieFor, _slipFrom;
    private PointF _prey;                                  // the school it is stalking (last place seen)
    private int _logDir;                                   // +1 if it climbs on at the log's t=0 end and lies toward t=1, else -1
    private readonly PointF[] _lieFrom = new PointF[Points];   // the body as it was when it began to lie, to ease into the pose from
    private float _weaveAmp = 1;                           // how wide its weave is right now (1 = a cruise), eased so it never jumps
    private float _weaveGate = 1;                          // 0 when it has stopped (a snake at rest does not weave), eased

    // ---- THE WAY OUT IS THE WAY IN ----
    // A trail of crumbs: where the head has been, one every 0.03u of travel, with how open the water was
    // there (the pond's room grid: 0 on land). A prowl or a stalk takes the snake in among the bank rocks
    // and onto the lawn, where the next doing's goal lies beyond the rocks and the picture's edge is a wall:
    // the feelers find no way through, only along, and the snake paced a pocket for 73 seconds (natural
    // seed 11). Now a doing that begins in a tight spot first walks the crumbs back to open water, the
    // path it knows is passable, then goes for its goal.
    private readonly float[] _crumbX = new float[64], _crumbY = new float[64], _crumbRoom = new float[64];
    private int _crumbHead, _crumbCount;
    private readonly PointF[] _way = new PointF[40];       // the crumbs to walk back, newest first
    private int _wayN, _wayI;                              // how many, and the one it is walking to (equal: no retreat)
    private float _retreatT;                               // seconds into the retreat (a cap, in case a crumb is unreachable)
    private bool Retreating => _wayI < _wayN;

    // ---- THE SKIN ---- (see the class comment; chosen in Begin from the two dials, never rolled)
    private int _skin;
    private float _blot;                                   // where along the body this snake's blotches start (0 to 1 of a blotch's length)

    // ---- THE HUNT ---- (see the class comment)
    private const float CockFor = 0.33f, LungeFor = 0.12f; // seconds: the neck drawn back, then the head shot forward
    private Lizards? _lizards;                             // who it may meet (handed in by the scene every frame)
    private Mice? _mice;
    private Random _huntRng = new(1);                      // its own dice for the hunt, so a hunt never moves the scene's (made fresh each visit)
    private IPrey? _target;                                // the prey it is stalking, striking at, holding or chasing
    private readonly IPrey?[] _rolled = new IPrey?[24];    // the animals it has already rolled the dice on this visit
    private readonly IPrey?[] _looked = new IPrey?[24];    // and the ones it has already noticed from afar (and decided to go and look at, or not)
    private int _lookedCount;
    private int _rolledCount, _strikes, _prowls;           // and how many strikes it has made this visit (at most two), and how many times it has gone to look
    private bool _fed;                                     // it has eaten this visit: slow, no more hunts
    private bool _won;                                     // the outcome rolled at the launch of the strike in progress
    private float _stalkT;                                 // seconds into the stalk
    private float _phaseT, _heldT, _swallowed, _swallowFor, _preyDir, _chaseT;
    private float _poseK, _pullK, _mouth = 1;             // the S in the neck: the bow (1 = fully bowed; a small negative or positive is a kick), the retreat of the head (1 = fully drawn back) and the head's width (1 closed, 1.5 open)
    private PointF _lungeFrom, _lungeTo, _lastNose;
    private float _bulgeT = -1, _bulgeBase, _bulgeAmp, _bulgeTravel, _bulgeS;   // the meal moving down the body: -1 = none

    // ---- THE CHANCE MEAL ---- (see the class comment)
    private readonly Snatch _snatch;                       // who it is after, how long it will bother, when it may look again (Tadpoles.cs)
    private readonly Func<ISnack, bool> _ahead;            // "is it in front of the head?" made once, so a look allocates nothing
    private int _snatchSeen;                               // how many catches the snatch had counted when this chase began (to tell a catch from a give-up)
    internal Tadpoles? Prey { get; set; }                  // the pond's tadpoles (set by the scene; null in a pond without any)
    internal DeepDwellers? DeepPrey { get; set; }          // the crayfish and prawns of the deep bed (round thirty-seven): a DIVING snake may take one at its nose
    private int _snatchStage;                              // what the chase in hand is after (a tadpole's 0 to 4, a dweller's 6 or 7), for the meal's bookkeeping

    // ---- THE APPETITE ---- (see the class comment)
    private float _hunger = 0.3f;                          // 0 full to 1 famished, kept across visits (the Snake object lives on; _live only toggles)
    private float _hungerSecs = 660f;                      // seconds from full to famished; dealt from the two dials at each visit (never rolled)
    private float _gulp;                                   // seconds left of the swallow after a mouthful (a tadpole or a minnow)
    private int _minnowTakes;                              // minnows taken this visit
    private float _landT;                                  // seconds the head has been on the lawn while crossing or exploring (the way out of it, below)
    private bool _minnowWin;                               // the lunge in progress was rolled at its launch to land (a quick fish still gets away)
    private const float GulpFor = 0.7f;
    private const float LungeSpeed = 0.6f;                 // the minnow lunge, in u a second (a tadpole's is the same): a minnow bolts at about a third of that
    private const float MinnowLunge = 0.35f;               // seconds a lunge at a minnow lasts
    /// <summary>Instruments only: how much faster than the real pond hunger rises. 1 in the real pond.</summary>
    internal static float Appetite = 1f;
    /// <summary>For the instruments and the drawing's docs: hunger now, 0 to 1.</summary>
    public float Hunger => _hunger;
    /// <summary>For the instruments: meals so far by kind, and the last one (a counter that goes up, what, hunger before and after).</summary>
    public int AteLizard, AteMouse, AteTadpole, AteMinnow, AteBed, MealSeq;   // (AteBed: a crayfish or prawn of the deep bed, taken on a dive)
    public string MealKind = "";
    public float MealBefore, MealAfter;

    /// <summary>0 at and below a hunger of 0.3, 1 when famished: how far past "peckish" it is.</summary>
    private float Want => Math.Clamp((_hunger - 0.3f) / 0.7f, 0f, 1f);
    /// <summary>How many strikes a visit may hold: two, or three when famished.</summary>
    private int MaxStrikes => _hunger >= 0.8f ? 3 : 2;

    /// <summary>A meal: hunger drops by "drop" (1 or more: full), and the instruments are told.</summary>
    private void Ate(string kind, float drop)
    {
        MealBefore = _hunger;
        _hunger = drop >= 1f ? 0f : MathF.Max(0f, _hunger - drop);
        MealAfter = _hunger; MealKind = kind; MealSeq++;
        switch (kind) { case "lizard": AteLizard++; break; case "mouse": AteMouse++; break; case "tadpole": AteTadpole++; break; case "crayfish": case "prawn": AteBed++; break; default: AteMinnow++; break; }
        Note("ate:" + kind);
    }

    // For the instruments only (nothing in the drawing or the mind reads these): the last thing that
    // happened in the hunt, as a fixed word, and a counter that goes up each time.
    public string HuntNote = "";
    /// <summary>The instrument log (PECKWORKS_SNAKELOG names a file, set up in the constructor): one line per frame while it is live. Null in the real pond.</summary>
    public static Action<string>? MotionLog;
    private double _clock;                                 // seconds since the scene began (the log's first column; the visit's own clock resets)
    public int HuntSeq, Meetings, Stalks, Abandoned, StrikeCount, Caught, Missed;
    private void Note(string what) { HuntNote = what; HuntSeq++; }

    // Where its head is (screen pixels, before the weave is added), which
    // way it is going, how fast, and how far it has come (the weave's clock).
    private float _x, _y, _heading, _speed, _travelled;
    private float _wakeRun;                                // pixels swum since the last wavelet of its bow wave (Ripples.Wake)
    private PointF _goal;                                  // the point beyond the far edge it is making for
    private float _wakeTimer, _next, _alive;
    private float _flee, _fleeAway;                        // startle: seconds left, and the way away
    private readonly Cooldown _calm = new();               // no startle again until this has run out
    private bool _live, _blocked;                          // _blocked: last frame's step was refused
    private float _blockedT;                               // how long in a row the step has been refused
    private float _spin;                                   // how far round the heading has turned lately, radians, signed (an orbit shows here as a whole circle; see the orbit breaker in Update)
    private float _detour;                                 // seconds left of swimming on with no pull toward the goal, after an orbit was broken

    public Snake(Pond pond, Ripples ripples, Minnows? minnows, float often, Random rng) : base(Points)
    {
        _pond = pond;
        _minnows = minnows;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        string? snakeLog = Environment.GetEnvironmentVariable("PECKWORKS_SNAKELOG");
        if (!string.IsNullOrEmpty(snakeLog) && MotionLog == null)
        {
            var writer = new StreamWriter(snakeLog) { AutoFlush = false };
            MotionLog = line => writer.WriteLine(line);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => writer.Flush();
        }
        _openDeep = new bool[pond.Open.Length];
        for (int i = 0; i < _openDeep.Length; i++) _openDeep[i] = pond.Open[i] && !pond.StoneTop[i];
        _len = _u * 0.3f;
        _wide = _u * 0.0068f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _next = 25 + 20 * (float)rng.NextDouble();       // the first visit comes within a minute
        _cruise = _u * 0.06f; _fleeFor = 2.6f;

        var centres = new List<PointF>();
        var radii = new List<float>();
        foreach (LilyPad p in pond.Pads) { centres.Add(p.Center); radii.Add(p.Radius); }
        foreach (BankRock r in pond.Rocks) { centres.Add(r.Centre); radii.Add(r.InWater ? r.Radius * 1.3f : r.Radius); }   // a cap standing in the water (the formation's, the island's) gets the island cap's margin: the lists must agree (2026-10-27)
        centres.Add(pond.CattailSpot); radii.Add(_u * 0.06f);
        foreach ((PointF root, _) in pond.IrisSpots) { centres.Add(root); radii.Add(_u * 0.045f); }
        if (pond.Island is Island isle) { centres.Add(isle.Cap); radii.Add(isle.CapRadius * 1.3f); }   // the island's dry cap
        _obsCentre = centres.ToArray();
        _obsRadius = radii.ToArray();
        _log = pond.Log;
        // Its own dice for the chance meal, made ONCE here (not each visit) so that the rest after a chase carries over
        // from one visit to the next (a visit is about two minutes and the rest is four to eight). Seeded from the pad
        // geometry the way the scene seeds the frogs, never from a roll of the scene's Random: one extra roll would move
        // every roll the scene makes afterwards, and the owner compares builds at one seed.
        _ahead = AheadOfHead;
        _snatch = new Snatch("snake", unchecked(pond.Pads.Count * 7919 + (pond.Pads.Count > 0 ? (int)pond.Pads[0].Center.X * 31 + (int)pond.Pads[0].Center.Y : 0) + pond.Width + 404));
    }

    /// <summary>Is it on screen (or coming or going) right now?</summary>
    public bool Live => _live;

    /// <summary>Its head, screen pixels (the minnows keep clear of it).</summary>
    public PointF Head => Spine[0];

    /// <summary>What it is doing now, for logs and tests (never read by the drawing).</summary>
    public string State => _live ? $"{_doing}:{_phase}" : "-";

    /// <summary>
    /// Is its body in the water right now (on screen and not up on the log)?
    /// Then it is SOLID to the koi and the turtles: the scene hands it to
    /// KoiSchool.Update as a surface body, the koi's feelers read its chain
    /// (Shape, filled in LaySpine) and steer under or round it, and a fish
    /// that still meets it is pushed down and out of it. Before this a koi
    /// could rise straight through the snake's body (the owner's law: nothing
    /// clips through anything in this universe). The snake is the lighter but
    /// is never moved by a fish: it answers a touch its own way, by startling.
    /// </summary>
    public bool InWater => _live && !(_doing == Doing.Bask && _phase is 1 or 2) && !Deep;

    /// <summary>Under the surface (the head more than 0.08 of the pond down): drawn under the fish shallower than it (DrawDeep), on no surface list, pushing no petal, hunting nothing.</summary>
    public bool Deep => _zHead > 0.08f;
    /// <summary>A stress hook for the instruments: PECKWORKS_SNAKEDIVE=1 makes every explore and crossing a dive (the dice still roll).</summary>
    private static readonly bool AlwaysDive = Environment.GetEnvironmentVariable("PECKWORKS_SNAKEDIVE") == "1";

    /// <summary>The head's depth, 0 the surface to 1 the pond's full depth (for the instruments).</summary>
    public float Z => _zHead;

    /// <summary>The bed's depth under a screen point, 0 to 1 of the pond's full depth (Pond.Floor).</summary>
    private float FloorZ(float x, float y)
    {
        int ix = Math.Clamp((int)x, 0, _pond.Width - 1), iy = Math.Clamp((int)y, 0, _pond.Height - 1);
        return _pond.Floor[iy * _pond.Width + ix] / 255f;
    }

    /// <summary>
    /// A new visit: rolls this snake's two dials, comes in from beyond one edge
    /// of the screen heading roughly for the pond's middle, and picks its
    /// first doing. Where it goes after that is the mind's business.
    /// </summary>
    private void Begin()
    {
        _bold = Traits.Roll(_rng); _nerve = Traits.Roll(_rng);
        _cruise = _u * 0.06f * (1.15f - 0.3f * _bold);    // a nervous snake swims a little quicker, a bold one more leisurely
        _fleeFor = 1.8f + 1.6f * _nerve;                  // and stays frightened longer
        _hungerSecs = 840f - 360f * (0.6f * _bold + 0.4f * _nerve);   // hungry sooner when bold: eight (bold) to fourteen (timid) minutes from full to famished
        _minnowTakes = 0; _gulp = 0; _landT = 0;
        _exploreLeft = 1 + _rng.Next(3); _huntsLeft = 1 + _rng.Next(2); _basked = false; _legs = 0;
        // The skin comes from the two dials already rolled (a fresh roll would move every later roll in the scene),
        // and so does the hunt's own dice, so a meeting never touches the scene's Random.
        int d1 = (int)(_bold * 1000), d2 = (int)(_nerve * 1000);
        _skin = (d1 + 7 * d2) % 4;
        if (_skin >= 2) _skin++;                            // 0, 1, 3, 4: the yamakagashi's checker (2) was dropped at the owner's word, 2026-10-28 ("drop the checkered one"); no fresh roll, the same two dials
        _blot = ((d1 * 31 + d2 * 17) % 100) / 100f;
        _huntRng = new Random(d1 * 7919 + d2 * 104729 + 17);
        _target = null; _strikes = 0; _prowls = 0; _fed = false; _won = false; Array.Clear(_rolled); _rolledCount = 0; Array.Clear(_looked); _lookedCount = 0;
        _poseK = 0; _pullK = 0; _mouth = 1; _bulgeT = -1; _bulgeAmp = 0; _weaveGate = 1;

        float a = (float)(_rng.NextDouble() * MathF.Tau), b = a + MathF.PI + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
        PointF from = OffScreen(a), to = OffScreen(b);
        _x = from.X; _y = from.Y;
        _heading = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        _speed = _cruise;
        _travelled = 0; _alive = 0; _flee = 0; _calm.Start(0); _blocked = false; _blockedT = 0; _weaveAmp = 1;
        _trailCount = 0;
        _crumbCount = 0; _wayN = _wayI = 0;
        // Its body starts laid out straight behind it, off the screen.
        for (int i = 60; i >= 0; i--) Remember(new PointF(_x - MathF.Cos(_heading) * _len * 1.2f * i / 60, _y - MathF.Sin(_heading) * _len * 1.2f * i / 60));
        _live = true;
        Decide();
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: like the koi, the snake has one thing on its mind at a
    // time. When it is done (or gives up) it rolls weighted dice for the next
    // thing. The dials move the weights: a bold snake has more "hunt" and
    // "lie on the log" faces; a nervous one has more "just cross and go" faces.
    // The "cross" face also grows heavier with every doing the visit has had,
    // so a visit is a few doings long and always ends in a crossing.

    /// <summary>The floating log, if there is one it can climb onto and be seen on.</summary>
    private bool LogReady()
    {
        if (_pond.Log is not Log log || !log.Afloat || !_pond.LogPerch.Free(this)) return false;   // not while a turtle has it, or is on its way to it
        int x = (int)log.Centre.X, y = (int)log.Centre.Y;
        return x >= 0 && y >= 0 && x < _pond.Width && y < _pond.Height && _pond.Open[y * _pond.Width + x];
    }

    /// <summary>Rolls the next doing, weighted by the dials and by what there is to do.</summary>
    private void Decide()
    {
        _legs++;
        // The minnow hunt is for a snake that is at least peckish (0.3), and only of a school that can spare a fish.
        float want = Want;
        PointF? school = _huntsLeft > 0 && _hunger >= 0.3f && _minnowTakes < MaxStrikes && _minnows != null ? _minnows.NearestHuntable(_x, _y) : null;
        float wExplore = _exploreLeft > 0 ? 0.7f + 0.5f * (1 - _nerve) : 0;
        float wHunt = school != null ? (0.15f + 1.1f * _bold) * (0.2f + 1.0f * want) : 0;
        float wBask = !_basked && LogReady() ? 0.15f + 0.9f * _bold : 0;
        float wCross = 0.3f + 1.2f * _nerve + 0.4f * _legs;
        // A bold snake with a lizard or a mouse out on the bank has a face for going to look (Prowl): not a
        // promise of a hunt, only a walk to where the meeting could happen.
        PointF? meal = !_fed && _strikes < MaxStrikes && _prowls < (_hunger >= 0.8f ? 4 : 3) ? NearestPrey(_u * (2f + want)) : null;
        float wProwl = meal != null ? (1.2f + 2f * _bold) * (1f + 2f * want) : 0;   // a hungry snake goes to look
        wCross *= 1f - 0.5f * want;                        // and is in less of a hurry to leave
        if (_fed)
        {
            // A snake that has eaten would sooner lie in the sun, or swim about a while with its meal on show (an Explore goal is
            // open water in view, walled in by the picture's edge); it crosses out later. A crossing straight from the land corner
            // hugged the bottom edge BELOW the picture for twenty seconds on three swallows of four, the bulge out of sight.
            if (_exploreLeft <= 0) _exploreLeft = 1;                           // one more look round, meal on show, even if its list was spent
            wHunt = 0; wExplore = 3f * (0.7f + 0.5f * (1 - _nerve)); wBask *= 3; wCross *= 0.3f;
        }
        if (_alive > 200 + 120 * want) wExplore = wHunt = wBask = wProwl = 0;    // a long visit ends now (a famished one stays a little longer)
        if (Deep) { wHunt = 0; wBask = 0; wProwl = 0; }                       // under the surface it explores on, or crosses out; the hunts are for a snake at the top
        int pick = Dice.Pick(_rng, [wHunt, wBask, wExplore, wCross, wProwl]);
        if (_fed && pick == 3 && _alive <= 200 && BeginExplore()) return;   // a fed snake keeps its meal on show: it explores before it crosses (the roll is made all the same)
        switch (pick)
        {
            case 0: BeginHunt(school!.Value); break;
            case 1: BeginBask(); break;
            case 2 when BeginExplore(): break;                  // nowhere to explore: it crosses instead
            case 4: BeginProwl(meal!.Value); break;
            default: BeginCross(); break;
        }
        MaybeRetreat();
    }

    /// <summary>
    /// An explore or a crossing that begins in a tight spot (the head among the bank rocks or on the lawn, where
    /// the room grid reads under 0.035u) first walks its crumbs back to the last one in open water. Nothing
    /// happens when the head is already in the open, or when no crumb within the last forty is.
    /// </summary>
    private void MaybeRetreat() => Retreat(force: false);

    /// <summary>The retreat itself; "force" skips the room test (the orbit breaker: a pocket on the lawn reads as roomy to the grid and is still a pocket). True if a way was found.</summary>
    private bool Retreat(bool force)
    {
        _wayN = _wayI = 0;
        if (!(_doing == Doing.Explore && _phase == 0 || _doing == Doing.Cross)) return false;
        if (!force && _pond.RoomAt(_x, _y) >= _u * 0.035f) return false;
        int n = Math.Min(_crumbCount, _way.Length);
        for (int k = 0; k < n; k++)
        {
            int i = (_crumbHead - k + _crumbX.Length) % _crumbX.Length;
            _way[k] = new PointF(_crumbX[i], _crumbY[i]);
            if (_crumbRoom[i] >= _u * 0.06f) { _wayN = k + 1; _retreatT = 0; Note("retreat"); return true; }
        }
        return false;
    }

    /// <summary>Go and look: swim toward the prey (heading for it, so it is ahead of the snake when they meet) and let the meeting happen, or not, on the way.</summary>
    private void BeginProwl(PointF prey)
    {
        _goal = prey; _prowls++; Note("prowl");
        // Long enough to get there at a prowl's pace (1.7 cruises) plus a quarter minute to look about: at a flat 30 s, a prowl
        // begun from the far side of the pond ran out with the snake still half way.
        float seconds = MathF.Min(75f, 15f + Dist(new PointF(_x, _y), prey) / MathF.Max(1e-3f, _cruise * 1.7f));
        _mind.Begin(Doing.Explore, seconds); _phase = 1;
    }

    /// <summary>Leave: toward a point well beyond the far side of the screen, as seen from here through the pond's middle.</summary>
    private void BeginCross()
    {
        _mind.Begin(Doing.Cross); _phase = 0;
        // Half of its crossings are dives too (an explore was often over, or a prowl, before the head reached water deep
        // enough to go down in: one dive begun and nothing seen on the first run). The roll is made either way.
        { bool dive = _rng.NextDouble() < 0.5; _zTarget = dive ? 0.3f + 0.4f * (float)_rng.NextDouble() : AlwaysDive ? 0.5f : 0f; }   // (the same two rolls as before, so the dice are unmoved; the stress hook adds no roll)
        Note(_zTarget > 0 ? "cross:dive" : "cross");
        PointF c = _pond.Centre;
        // A fed snake goes straight through the middle: with the usual spread it left along the bottom edge from the land
        // corner, the bulge below the picture for most of the next minute. (The roll is made either way: the dice must not move.)
        float spread = ((float)_rng.NextDouble() - 0.5f) * (_fed ? 0f : 1.2f);
        float b = Dist(new PointF(_x, _y), c) > _u * 0.15f ? MathF.Atan2(c.Y - _y, c.X - _x) + spread : (float)(_rng.NextDouble() * MathF.Tau);
        PointF to = OffScreen(b);
        // The goal sits a good way past the far edge, so the snake is well
        // out of sight (and over) before it could ever reach it and turn back.
        _goal = new PointF(to.X + MathF.Cos(b) * _u * 0.6f, to.Y + MathF.Sin(b) * _u * 0.6f);
    }

    /// <summary>Somewhere to swim to in open water that people can see, with room round it. False if none was found.</summary>
    private bool BeginExplore()
    {
        for (int tries = 0; tries < 40; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            int i = (int)y * _pond.Width + (int)x;
            if (!_pond.Water[i] || !_pond.Open[i] || _pond.RoomAt(x, y) < _u * 0.05f || Clear(x, y) < _u * 0.06f) continue;
            if (Dist(new PointF(x, y), new PointF(_x, _y)) < _u * 0.25f) continue;       // not just next door
            _goal = new PointF(x, y);
            // Half of its explores are DIVES (2026-10-27, "make the snake be willing to go deep also"): down to a third
            // to seven tenths of the pond on the way to the spot, the body following the head (Update, LaySpine).
            { bool dive = _rng.NextDouble() < 0.5; _zTarget = dive ? 0.3f + 0.4f * (float)_rng.NextDouble() : AlwaysDive ? 0.5f : 0f; }   // (the same two rolls as before, so the dice are unmoved; the stress hook adds no roll)
            _mind.Begin(Doing.Explore, 45); _phase = 0; _exploreLeft--; Note(_zTarget > 0 ? "explore:dive" : "explore");
            return true;
        }
        return false;
    }

    private void BeginHunt(PointF school)
    {
        _mind.Begin(Doing.Hunt, 60); _phase = 0;      // a hunt only counts against the visit's one or two once it has struck or given up
        _prey = school; _goal = school;
    }

    /// <summary>
    /// Head for the near end of the log (a little beyond it, so it lines up
    /// with the log), then climb on along the log's own line toward the far end.
    /// </summary>
    private void BeginBask()
    {
        Log log = _pond.Log!.Value;
        _pond.LogPerch.Take(this);
        _basked = true; _mind.Begin(Doing.Bask, 60); _phase = 0; Note("bask");
        _logDir = Dist(new PointF(_x, _y), log.At(0)) <= Dist(new PointF(_x, _y), log.At(1)) ? 1 : -1;
        _goal = LogPoint(log, -(log.Length * 0.5f + _u * 0.14f), 0);
    }

    /// <summary>
    /// A point on or beside the log: "axial" pixels along its middle line from
    /// its middle (positive toward the end the snake lies toward), and
    /// "lateral" pixels sideways off that line.
    /// </summary>
    private PointF LogPoint(Log log, float axial, float lateral)
    {
        float dx = MathF.Cos(log.Angle) * _logDir, dy = MathF.Sin(log.Angle) * _logDir;
        return new PointF(log.Centre.X + dx * axial - dy * lateral, log.Centre.Y + dy * axial + dx * lateral);
    }

    /// <summary>How far from the log's middle its head lies when it is basking: near the far end, or less if the snake is shorter than the log.</summary>
    private float LieHead(Log log) => MathF.Min(log.Length * 0.42f, _len * 0.5f);

    /// <summary>
    /// THE WAY OUT OF THE LAWN. A hungry snake goes to look at prey on the land corner more often, and now and then it
    /// ends up in the pocket between the bank rocks and the picture's edge with no crumb of open water to walk back to (it
    /// came in over the edge): its crossing then circled the lawn for twelve minutes on one run (round 27, seed 5, hunger
    /// four times quicker). A crossing or an explore whose head has been on the land for half a minute gives up its goal
    /// and heads straight for the nearest edge of the picture, which is open to it (Clear leaves the goal's edge unwalled),
    /// and the visit ends there as any crossing does.
    /// </summary>
    private void WatchLawn(float dt)
    {
        if (!(_doing == Doing.Cross || (_doing == Doing.Explore && _phase == 0)) || !HeadOnLand()) { _landT = 0; return; }
        _landT += dt;
        if (_landT < 30f) return;
        _landT = 0;
        LeaveByNearestEdge();
    }

    /// <summary>A crossing to the nearest edge of the picture (open to it, see Clear): the land corner touches two edges, so from the lawn this way out always exists.</summary>
    private void LeaveByNearestEdge()
    {
        float toL = _x, toR = _pond.Width - _x, toT = _y, toB = _pond.Height - _y, m = MathF.Min(MathF.Min(toL, toR), MathF.Min(toT, toB));
        _mind.Begin(Doing.Cross); _phase = 0; Note("leave");
        _wayN = _wayI = 0;
        _goal = m == toL ? new PointF(-_u * 0.6f, _y) : m == toR ? new PointF(_pond.Width + _u * 0.6f, _y) : m == toT ? new PointF(_x, -_u * 0.6f) : new PointF(_x, _pond.Height + _u * 0.6f);
    }

    /// <summary>Keeps the current doing going, moves it on a step, or ends it.</summary>
    private void Think(float dt)
    {
        _mind.Tick(dt);
        WatchLawn(dt);
        var head = new PointF(_x, _y);
        switch (_doing)
        {
            case Doing.Explore:
                if (_phase == 1)
                {
                    // A prowl: keep the aim on the prey it is going to look at. It ends when the prey is gone, or the
                    // snake is close (the meeting's dice are rolled in Scan on the way, once per animal), or it has had long enough.
                    if (NearestPrey() is PointF meal && !_mind.Over) { _goal = meal; if (Dist(head, meal) < _u * 0.04f) Decide(); }
                    else Decide();
                }
                // Reached: close enough that its turning circle cannot orbit the spot.
                else if (Dist(head, _goal) < _u * 0.08f || _mind.Over) Decide();
                break;
            case Doing.Hunt:
                // The school drifts about, so keep the aim on it (but ignore a jump to a different school). Near enough, the
                // aim is the nearest single fish (a lunge goes at one fish, not at the middle of a cloud of them).
                if (_minnows!.NearestHuntable(_x, _y) is PointF now && Dist(now, _prey) < _u * 0.25f) _prey = now;
                _goal = _prey;
                if (_phase == 0)
                {
                    bool aimed = _minnows.NearestFish(head.X, head.Y, _u * 0.2f, out PointF fish);
                    if (aimed) _goal = fish;
                    // The lunge at 0.6u a second cannot steer, so it goes only when the fish is nearly dead ahead (20 degrees),
                    // and from just outside the 0.11u at which a minnow bolts from a head.
                    if (aimed && Dist(head, fish) < _u * 0.125f && MathF.Abs(Wrap(MathF.Atan2(fish.Y - head.Y, fish.X - head.X) - _heading)) < 0.35f)
                    {
                        _phase = 1; _strikeT = MinnowLunge; _huntsLeft--; Note("lunge:minnow");
                        // The outcome is rolled at the launch, as the lizard's is: about six in ten, a bold snake a little better.
                        _minnowWin = _huntRng.NextDouble() < 0.5f + 0.2f * _bold;
                        _ripples.Add(Spine[0].X, Spine[0].Y, 0.9f);        // the lunge pushes the water
                    }
                    else if (_mind.Over) { _huntsLeft--; Decide(); }
                }
                else if (_phase == 1)
                {
                    _strikeT -= dt;
                    _mouth = 1.3f;                                          // jaws wide on the way in
                    // The jaws close on the nearest fish within reach (0.028u: a minnow is 0.02u long).
                    if (_minnowWin && _minnows.Take(Spine[0].X, Spine[0].Y, _u * 0.028f))
                    {
                        Ate("minnow", 0.1f + 0.05f * (float)_huntRng.NextDouble());
                        _minnowTakes++;
                        _phase = 2; _strikeT = GulpFor; _gulp = GulpFor;
                        _ripples.Add(Spine[0].X, Spine[0].Y, 0.45f);        // a small ring where it took it
                        // Still properly hungry (0.6 and over) and the visit has room: another hunt may follow ("one or more").
                        if (_hunger >= 0.6f) _huntsLeft = Math.Max(_huntsLeft, 1);
                    }
                    else if (_strikeT <= 0) { _mouth = 1; _ripples.Add(Spine[0].X, Spine[0].Y, 0.7f); Decide(); }
                }
                else
                {
                    _strikeT -= dt;                                         // the gulp: slow, jaws closing on the mouthful
                    if (_strikeT <= 0) Decide();
                }
                break;
            case Doing.Bask:
                Log log = _pond.Log!.Value;
                if (_phase == 0)
                {
                    if (Dist(head, _goal) < _u * 0.08f) { _phase = 1; _goal = LogPoint(log, LieHead(log), 0); }
                    else if (_mind.Over) Decide();
                }
                else if (_phase == 1)
                {
                    if (Dist(head, _goal) < _u * 0.03f) BeginLie();
                    else if (_mind.Over) StartSlip();                 // on the log and out of patience: off by the log's line, never just "another doing"
                }
                else if (_phase == 3 && (_travelled - _slipFrom > _len * 1.3f || _mind.Over)) Decide();
                break;
            case Doing.Stalk: ThinkStalk(dt); break;
            case Doing.Strike: ThinkStrike(dt); break;
            case Doing.Swallow: ThinkSwallow(dt); break;
            case Doing.Miss: ThinkMiss(dt); break;
            case Doing.Snatch: ThinkSnatch(dt); break;
        }
    }

    // ==================== THE CHANCE MEAL ====================
    //
    // FEYNMAN VERSION: think of a heron strolling a shallow stream that happens to see a tadpole at its
    // feet. It does not hunt for tadpoles; it was only passing. But this one is right there, so it takes a
    // few quick steps and a stab. If the tadpole is quicker, the heron shrugs and carries on, and it will
    // not bother again for a good while. The snake does the same with its head: the pond's own wandering
    // brought it here, a few dice say "that one", and for about five seconds its goal is the tadpole.

    /// <summary>
    /// Once a frame, while it is just swimming (Cross or Explore, not a prowl, which has a purpose): is a
    /// tadpole or froglet at the top of the water within 0.15u of its nose? Never when it has
    /// a meal in it, is frightened, or has its head off the picture or on land (the land corner), which is
    /// where a snake coming in or going out has its head. Snatch.Look rolls its own dice FIRST (Spot, the
    /// search over the clutch, runs only on the frames the dice chose).
    /// </summary>
    /// <summary>
    /// A snake only notices what is in front of its head (within 75 degrees of where it points, the same cone the strike
    /// uses). A tadpole behind it was "seen" in the first build, and the snake, whose turning circle is wide at a swim,
    /// spent its five seconds in a long U-turn with the tadpole bolting away behind it.
    /// </summary>
    private bool AheadOfHead(ISnack t)
    {
        PointF h = Spine[0], n = t.Nose;
        return MathF.Abs(Wrap(MathF.Atan2(n.Y - h.Y, n.X - h.X) - _heading)) < 1.309f;
    }

    private void LookForTadpole(float dt)
    {
        if (Deep) { LookOnBed(dt); return; }
        if (Prey == null || _fed || _flee > 0 || !(_doing is Doing.Cross or Doing.Explore) || _phase != 0) return;
        PointF mouth = Spine[0];
        int ix = (int)mouth.X, iy = (int)mouth.Y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        // It sees 0.15u round its nose, not the 0.1u first asked for: a tadpole bolts when a head comes within 0.09u of it
        // (UpdateTadpole's scare), so at 0.1u the snake would have a 0.01u ring, a fifth of a second of swimming, in which to
        // make up its mind, and almost never begin. From 0.15u it has about a second of approach.
        // A bold snake looks sooner (0.08 to 0.28 a second: a few seconds beside the clutch) and rests less after a
        // chase (4 minutes bold, 8 nervous). Five seconds' patience. Only what is up near the top of the water.
        // A hungry snake looks likelier (up to four times), a little further and rests less after a chase (never goes LOOKING
        // for tadpoles: it is still only what is at its nose as it swims about).
        float want = Want;
        if (!_snatch.Look(Prey, mouth, _u * 0.15f * (1f + 0.5f * want), (0.08f + 0.2f * _bold) * (1f + 3f * want), dt, 5f + 2f * want, (240f + 240f * _nerve) * (1f - 0.6f * want), surfaceOnly: true, ok: _ahead)) return;
        _snatchSeen = _snatch.Caught;
        _mind.Begin(Doing.Snatch, 9f);                      // the patience is the Snatch's (5 s); this is only a backstop
        _phase = 0; _strikeT = 1; _goal = _snatch.Prey.Nose;
    }

    /// <summary>
    /// A frame of the chase. Phase 0: close in on the prey's nose (the goal, read afresh every frame, because it
    /// moves). When it is within 0.12u (a little before a tadpole bolts, at 0.09u) AND the nose is within 20 degrees of where the snake points,
    /// phase 1: a lunge (SnatchLunge long, at 0.6u a second, a ring off the head at the start). A lunge that has not got it
    /// ends with the minnow hunt's second ring, a half second of closing in again, and another lunge if the
    /// patience lasts. The catch is the Snatch's: the prey's nose within 0.02u of the head.
    /// </summary>
    /// <summary>
    /// The DIVING snake's chance meal (round thirty-seven, "sometimes ... snakes"): down on the deep bed (the head past
    /// 0.55 of the depth), swimming (Cross or Explore), not fed, not frightened: a crayfish or prawn of the deep within
    /// 0.15u ahead of its nose, with the same dice as the tadpole look and more patience (a dweller flicks off, and the
    /// head has to come down to it). The chase is ThinkSnatch's, with the head held at the prey's depth.
    /// </summary>
    private void LookOnBed(float dt)
    {
        if (DeepPrey == null || _fed || _flee > 0 || !(_doing is Doing.Cross or Doing.Explore) || _phase != 0 || _zHead < 0.4f) return;   // (past 0.4 of the depth: a dive's target is 0.3 to 0.7 and the head eases toward it slowly, so 0.55 was never reached in ten stress minutes; the chase takes it the rest of the way down)
        PointF mouth = Spine[0];
        int ix = (int)mouth.X, iy = (int)mouth.Y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || !_pond.Water[iy * _pond.Width + ix]) return;
        float want = Want;
        if (!_snatch.Look(DeepPrey, mouth, _u * 0.15f * (1f + 0.5f * want), (0.1f + 0.2f * _bold) * (1f + 3f * want), dt, 8f + 2f * want, (240f + 240f * _nerve) * (1f - 0.6f * want), surfaceOnly: false, ok: _ahead)) return;
        _snatchSeen = _snatch.Caught;
        _snatchStage = _snatch.Prey.Stage;
        _mind.Begin(Doing.Snatch, 12f);
        _phase = 0; _strikeT = 1; _goal = _snatch.Prey.Nose;
    }

    private void ThinkSnatch(float dt)
    {
        PointF head = Spine[0];
        ISnackField? field = (ISnackField?)Prey ?? DeepPrey;                 // (the Snatch chases from the field the prey came from; this only has to be some field)
        bool going = field != null && _snatch.Chase(field, head, _u * 0.02f);
        if (!going)
        {
            // Caught or given up (or the prey was taken by another: lost). A catch rings once at the head; either
            // way the snake carries on with whatever it rolls next, and is not "fed" by a tadpole (a mouthful); a
            // crayfish or a prawn of the deep is a real meal (0.4 of its hunger).
            if (_snatch.Caught != _snatchSeen)
            {
                _ripples.Add(head.X, head.Y, _snatchStage >= 6 ? 0.4f : 0.9f);   // (a catch on the bed barely marks the surface)
                if (_snatchStage >= 6) Ate(_snatchStage == 7 ? "crayfish" : "prawn", 0.4f);
                else Ate("tadpole", 0.15f + 0.1f * (float)_huntRng.NextDouble());
                _gulp = GulpFor;
            }
            _snatchSeen = _snatch.Caught;
            Decide();
            return;
        }
        if (_mind.Over) { _snatch.Abandon(); Decide(); return; }
        ISnack prey = _snatch.Prey;
        _snatchStage = prey.Stage;
        // A tadpole that has dived out of reach (a froglet always is within it) is let go: the snake takes what is near the top,
        // and in the first runs it chased a tadpole three tenths of the pond's depth down for its whole five seconds.
        if (prey.Stage < 4 && prey.Z > 0.2f) { _snatch.Abandon(); Decide(); return; }
        // A dweller on the bed: the head goes down to it (the bed's own cap keeps it off the floor) and stays there for the chase.
        if (prey.Stage >= 6) _zTarget = MathF.Min(1f, prey.Z + 0.02f);
        PointF nose = prey.Nose;
        _goal = nose;
        // How far the nose is off the way the head points, in radians (0 dead ahead, 3.14 straight behind).
        float off = MathF.Abs(Wrap(MathF.Atan2(nose.Y - head.Y, nose.X - head.X) - _heading));
        if (_phase == 0)
        {
            _strikeT += dt;                                    // seconds since the last lunge ended
            // A lunge is AIMED: within 20 degrees of dead ahead. At 0.6u a second the head cannot steer, and a lunge begun
            // from 33 degrees off passed the tadpole at 38 px and carried on away from it.
            if (_strikeT >= 0.5f && off < 0.35f && Dist(head, nose) < _u * 0.12f)
            {
                _phase = 1; _strikeT = SnatchLunge;
                _ripples.Add(head.X, head.Y, 0.9f);            // the lunge pushes the water
            }
        }
        else
        {
            _strikeT -= dt;
            // Over when its time is up, or when the prey is behind the head (the lunge has gone past: no point carrying on away from it).
            if (_strikeT <= 0 || off > 1.57f) { _ripples.Add(head.X, head.Y, 0.7f); _phase = 0; _strikeT = 0; }
        }
    }

    // ==================== THE HUNT ====================
    //
    // FEYNMAN VERSION: think of a cat at a mousehole. It sees the mouse, goes
    // still, then creeps with its head low (STALK). At arm's length it draws
    // back, coiled like a spring (the S in the neck), and pounces (STRIKE). The
    // pounce either lands (SWALLOW: the meal goes in head first, and you can
    // watch a lump travel down the cat) or the mouse was already gone (MISS: the
    // jaws snap on air, the cat looks about, trots after it a little, gives up).
    // The prey does its own half: IPrey, at the bottom of this file.

    /// <summary>Screen point of the nearest lizard or mouse it could hunt, if any is out.</summary>
    private PointF? NearestPrey(float within = float.MaxValue)
    {
        PointF head = new(_x, _y);
        float best = float.MaxValue;
        PointF? found = null;
        for (int pass = 0; pass < 2; pass++)
        {
            int n = pass == 0 ? _lizards?.Count ?? 0 : _mice?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                IPrey p = pass == 0 ? _lizards![i] : _mice![i];
                if (!p.Huntable || WasRolled(p)) continue;
                float d = Dist(head, p.PreyNose);
                if (d < best && d < within) { best = d; found = p.PreyNose; }
            }
        }
        return found;
    }

    private bool WasRolled(IPrey p)
    {
        for (int k = 0; k < _rolledCount; k++) if (ReferenceEquals(_rolled[k], p)) return true;
        return false;
    }

    /// <summary>Is a screen point on the land, or within 0.03u of it? (The snake hunts on the bank and at the water's edge.)</summary>
    private bool NearLand(PointF p)
    {
        float r = _u * 0.03f;
        return LandAt(p.X, p.Y) || LandAt(p.X + r, p.Y) || LandAt(p.X - r, p.Y) || LandAt(p.X, p.Y + r) || LandAt(p.X, p.Y - r);
    }

    private bool LandAt(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Land[iy * _pond.Width + ix];
    }

    /// <summary>
    /// Looks for a meeting: a lizard or mouse that is out, within 0.14u of the head and within
    /// 75 degrees of where the snake is going, while the snake is on or beside the bank. An animal
    /// the snake happens on gets ONE roll per visit (50 percent for the nervous, up to 90 for the
    /// bold); one the snake went to LOOK at (a prowl) is stalked without a roll: the prowl's own
    /// dice were the roll. At 40 to 80 and one roll each the owner saw about one strike an hour.
    /// </summary>
    private void Scan()
    {
        if (Deep) return;
        if (_fed || _strikes >= MaxStrikes || (_lizards == null && _mice == null)) return;
        if (!(_doing is Doing.Cross or Doing.Explore or Doing.Hunt || (_doing == Doing.Bask && _phase == 0))) return;
        PointF head = Spine[0];
        if (head.X < 0 || head.Y < 0 || head.X >= _pond.Width || head.Y >= _pond.Height) return;   // a meeting is on the picture (a lizard at the edge was snatched from nothing)
        float reach = _u * 0.14f;
        for (int pass = 0; pass < 2; pass++)
        {
            int n = pass == 0 ? _lizards?.Count ?? 0 : _mice?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                IPrey p = pass == 0 ? _lizards![i] : _mice![i];
                if (!p.Huntable) continue;
                PointF nose = p.PreyNose;
                if (Dist(head, nose) > reach) continue;
                bool prowling = _doing == Doing.Explore && _phase == 1;
                // Within 75 degrees of its heading; a snake that came to LOOK sees further round (115 degrees): three prowls
                // in seven that got within reach passed the prey broadside and never noticed it.
                if (MathF.Abs(Wrap(MathF.Atan2(nose.Y - head.Y, nose.X - head.X) - _heading)) > (prowling ? 2.0f : 1.309f)) continue;
                if (!NearLand(head)) continue;
                if (WasRolled(p) && !prowling) continue;
                if (_rolledCount < _rolled.Length && !WasRolled(p)) _rolled[_rolledCount++] = p;
                Meetings++;
                if (!prowling && _huntRng.NextDouble() >= 0.5 + 0.4 * _bold + 0.2 * Want) { Note("meet:no"); continue; }
                _target = p; Stalks++; p.Stalked = true;
                _mind.Begin(Doing.Stalk, 25); _phase = 0; _goal = nose; _stalkT = 0;
                _speed = MathF.Min(_speed, _cruise * 0.5f);      // the creep begins at once (a prowl is brisk, and the speed only eases down slowly)
                Note("stalk");
                return;
            }
        }
    }

    /// <summary>
    /// A snake swimming by notices a lizard or mouse out on the bank within 0.9u and may turn to go and
    /// look (a prowl): one roll per animal per visit, 50 percent for the nervous to 100 for the bold. This is
    /// what makes a meeting likely at all: left to chance the snake's crossings pass the little corner of
    /// land a few times an hour.
    /// </summary>
    private void Notice()
    {
        if (Deep) return;
        if (_fed || _strikes >= MaxStrikes || _prowls >= (_hunger >= 0.8f ? 4 : 3) || (_lizards == null && _mice == null)) return;
        if (!(_doing == Doing.Cross || (_doing == Doing.Explore && _phase == 0))) return;
        float want = Want;
        PointF head = Spine[0];
        for (int pass = 0; pass < 2; pass++)
        {
            int n = pass == 0 ? _lizards?.Count ?? 0 : _mice?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                IPrey p = pass == 0 ? _lizards![i] : _mice![i];
                if (!p.Huntable || Dist(head, p.PreyNose) > _u * 0.9f * (1f + 0.6f * want)) continue;   // a hungry snake notices from further
                bool seen = false;
                for (int k = 0; k < _lookedCount; k++) if (ReferenceEquals(_looked[k], p)) { seen = true; break; }
                if (seen) continue;
                if (_lookedCount < _looked.Length) _looked[_lookedCount++] = p;
                if (_huntRng.NextDouble() < Math.Min(1.0, 0.5 + 0.5 * _bold + 0.5 * want)) { BeginProwl(p.PreyNose); return; }
            }
        }
    }

    /// <summary>The prey is out of reach for good (it ran, hid or was gone): the stalk is dropped and the snake carries on.</summary>
    private void Abandon(string why)
    {
        if (_target != null) _target.Stalked = false;
        _target = null; Abandoned++;
        Note(why);
        Decide();
    }

    private void ThinkStalk(float dt)
    {
        IPrey? p = _target;
        if (p == null || !p.Huntable) { Abandon(p != null && p.Out ? "abandon:ran" : "abandon:hid"); return; }
        PointF nose = p.PreyNose, hd = Spine[0];
        float d = Dist(hd, nose);
        _goal = nose;
        if (d > _u * 0.2f) { Abandon("abandon:fled"); return; }
        if (_mind.Over) { Abandon("abandon:timeout"); return; }
        // It strikes at 0.05u; one that cannot get that close (the prey is up against a rock the snake must keep off) is
        // struck at from a little further each second after the first 2.5 (up to 0.086u): a head's length is not exact.
        _stalkT += dt;
        float reach = _u * (0.05f + 0.012f * Math.Clamp(_stalkT - 2.5f, 0f, 3f));
        if (d < reach && MathF.Abs(Wrap(MathF.Atan2(nose.Y - hd.Y, nose.X - hd.X) - _heading)) < 1.309f)
        {
            // Close enough: draw the neck back. It stops dead for this.
            _mind.Begin(Doing.Strike, 5); _phase = 0; _phaseT = 0; _speed = 0; _strikes++;
            Note("cock");
        }
    }

    private static float Ease(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    private void ThinkStrike(float dt)
    {
        _phaseT += dt;
        if (_phase == 0)
        {
            // The S: the neck draws back. The mouth opens over the last half of it.
            _poseK = Ease(_phaseT / CockFor);
            _pullK = 1;                                      // the head snaps back its few pixels in one frame and is held there
            _mouth = 1 + 0.5f * Ease((_phaseT / CockFor - 0.5f) * 2);
            if (_phaseT < CockFor) return;
            IPrey? p = _target;
            if (p == null || !p.Huntable || Dist(Spine[0], p.PreyNose) > _u * 0.13f || Spine[0].X < 0 || Spine[0].Y < 0 || Spine[0].X >= _pond.Width || Spine[0].Y >= _pond.Height)
            {
                // It has gone while the snake was drawing back: nothing to strike at.
                _poseK = 0; _pullK = 0; _mouth = 1; _strikes--;
                Abandon("abandon:slipped");
                return;
            }
            // THE LAUNCH. The outcome is rolled here: 50 percent, plus 15 for a bold snake, minus 15 for a
            // lizard that is bold (it dodges better) or a mouse that is nervous (already jumpy); held to 25..80.
            float chance = Math.Clamp(0.5f + 0.15f * (2 * _bold - 1) - 0.15f * (2 * p.Agility - 1), 0.25f, 0.8f);
            _won = _huntRng.NextDouble() < chance;
            _lastNose = p.PreyNose;
            PointF lastTail = p.PreyTail;                // the body as it lay, read before the dodge moves it
            p.Stalked = false;
            p.Strike(Spine[0], _lastNose, _won);       // a miss: the prey dodges THIS frame; a catch: it freezes where it is
            _lungeFrom = new PointF(_x, _y);
            if (_won) _lungeTo = _lastNose;             // a catch: the jaws go to the nose
            else
            {
                // A miss: the jaws snap shut short of the animal, seven tenths of the way to where its nose was and never nearer
                // than 0.025u to the line its body lay along (the prey throws itself aside, but jaws that arrived on the spot
                // closed on its body, and a strike from behind or beside found the body between the jaws and the nose).
                float along = 0.7f;
                while (along > 0.2f && DistToLine(new PointF(_x + (_lastNose.X - _x) * along, _y + (_lastNose.Y - _y) * along), _lastNose, lastTail) < _u * 0.025f) along -= 0.05f;
                _lungeTo = new PointF(_x + (_lastNose.X - _x) * along, _y + (_lastNose.Y - _y) * along);
            }
            _heading = MathF.Atan2(_lastNose.Y - Spine[0].Y, _lastNose.X - Spine[0].X);
            _phase = 1; _phaseT = 0; StrikeCount++;
            Note(_won ? "launch:catch" : "launch:miss");
            return;
        }
        // The lunge: the head shoots out, the S unwinds as it goes.
        float k = Math.Clamp(_phaseT / LungeFor, 0, 1);
        _poseK = 1 - k; _pullK = 1 - k; _mouth = 1.5f;
        float e = k * (2 - k);
        float nx = _lungeFrom.X + (_lungeTo.X - _lungeFrom.X) * e, ny = _lungeFrom.Y + (_lungeTo.Y - _lungeFrom.Y) * e;
        _travelled += Dist(new PointF(_x, _y), new PointF(nx, ny));
        _x = nx; _y = ny;
        if (_phaseT < LungeFor) return;
        _poseK = 0; _pullK = 0;
        if (_won)
        {
            Caught++;
            _mind.Begin(Doing.Swallow, Mind<Doing>.Forever); _phase = 0; _heldT = 0; _swallowed = 0;
            _swallowFor = 3f + 2f * (float)_huntRng.NextDouble();
            _preyDir = ChoosePreyDir();
            _mouth = 1.25f;
            Note("catch");
        }
        else
        {
            Missed++;
            _mind.Begin(Doing.Miss, Mind<Doing>.Forever); _phase = 0; _phaseT = 0;
            Note("miss");
        }
    }

    /// <summary>The way the prey's body points while it is held: out in front of the snake, but turned toward land if the straight way is water.</summary>
    private float ChoosePreyDir()
    {
        float len = _target?.PreyLength ?? _u * 0.05f;
        foreach (float off in (ReadOnlySpan<float>)[0f, 0.7f, -0.7f, 1.4f, -1.4f])
        {
            float a = _heading + off;
            if (LandAt(Spine[0].X + MathF.Cos(a) * len * 0.8f, Spine[0].Y + MathF.Sin(a) * len * 0.8f * Ripples.Squash)) return a;
        }
        return _heading;
    }

    private void ThinkSwallow(float dt)
    {
        _speed = 0;
        _heldT += dt;
        if (_phase == 0)
        {
            if (_heldT >= 1f) _phase = 1;      // a second of thrashing in the jaws, then it goes in
        }
        else
        {
            _swallowed = Ease((_heldT - 1f) / _swallowFor);
            _mouth = 1.25f - 0.15f * _swallowed;
            if (_heldT - 1f >= _swallowFor)
            {
                _target?.Finish();
                float size = _target?.MealSize ?? 0.33f;
                Ate(_target is Mouse ? "mouse" : "lizard", 1f);
                _target = null; _fed = true; _mouth = 1;
                _bulgeBase = size; _bulgeT = 0; _bulgeTravel = 20 + 10 * (float)_huntRng.NextDouble();
                Note("swallowed");
                Decide();
            }
        }
    }

    private void ThinkMiss(float dt)
    {
        _phaseT += dt;
        if (_phase == 0)
        {
            // The jaws shut on nothing: the head is back to its width at once, with a small kick of the neck, then a second of looking about.
            _speed = 0;
            _mouth = 1 + 0.5f * MathF.Max(0, 1 - _phaseT / 0.1f);
            _poseK = _phaseT < 0.5f ? 0.3f * MathF.Exp(-_phaseT * 7) * MathF.Sin(_phaseT * 26) : 0;
            if (_phaseT >= 1f) { _phase = 1; _chaseT = 0; _poseK = 0; _mouth = 1; Note("chase"); }
        }
        else
        {
            _chaseT += dt;
            if (_target != null && _target.Huntable) _goal = _target.PreyNose;
            else if (_target != null) _goal = _lastNose;
            if (_chaseT >= 3f) { _target = null; Note("giveup"); Decide(); }
        }
    }

    /// <summary>
    /// How fast to stalk, as a share of the cruise: a full swim while the school
    /// is far off (a creep from across the pond would take half a minute and the
    /// school drifts as fast as that), easing to half speed over the last 0.3u,
    /// the slow careful approach of a snake that does not want to be noticed.
    /// </summary>
    private float StalkPace() => 0.5f + 0.5f * Math.Clamp((Dist(new PointF(_x, _y), _prey) - _u * 0.2f) / (_u * 0.3f), 0, 1);

    /// <summary>It has reached the pose spot: stop, remember how the body lies, and start easing into the pose.</summary>
    private void BeginLie()
    {
        _phase = 2; _lieT = 0; _speed = 0;
        _lieFor = 10 + 20 * (0.15f + 0.85f * _bold) * (float)_rng.NextDouble();    // a bold snake lies longer
        Array.Copy(Spine, _lieFrom, Points);
    }

    /// <summary>
    /// While it lies: each spine point sits on the log's middle line, with a
    /// gentle S (one slow wave, a third of the log's half-width) so it is not
    /// a ruler. The body eases in from wherever it was over a second and a half.
    /// A snake longer than the log hangs its tail off the near end into the water.
    /// </summary>
    private void UpdateLie(float dt)
    {
        _lieT += dt;
        if (_lieT >= _lieFor) { StartSlip(); return; }
        Log log = _pond.Log!.Value;
        float k = Math.Clamp(_lieT / 1.5f, 0, 1); k = k * k * (3 - 2 * k);
        float head = LieHead(log), amp = log.Width * 0.3f;
        for (int i = 0; i < Points; i++)
        {
            PointF pose = LogPoint(log, head - Along[i] * _len, amp * MathF.Sin(Along[i] * MathF.Tau));
            Spine[i] = new PointF(_lieFrom[i].X + (pose.X - _lieFrom[i].X) * k, _lieFrom[i].Y + (pose.Y - _lieFrom[i].Y) * k);
        }
        Widths();
    }

    /// <summary>
    /// Off the log: its body as it lies becomes its trail, it faces along the
    /// log's line out the far end, and a ring leaves its head and its middle.
    /// </summary>
    private void StartSlip()
    {
        _trailCount = 0;
        for (int i = Points - 1; i >= 0; i--) Remember(Spine[i]);
        _x = Spine[0].X; _y = Spine[0].Y;
        _heading = Wrap(_pond.Log!.Value.Angle + (_logDir < 0 ? MathF.PI : 0));
        _weaveAmp = 0;                                      // eases back to a full weave, so the head does not jump sideways
        _phase = 3; _slipFrom = _travelled; _mind.Left = 10; _blocked = false;
        _speed = _cruise * 0.8f;                            // it slides off at once; it has been lying there long enough
        _ripples.Add(Spine[0].X, Spine[0].Y, 0.6f);
        _ripples.Add(Spine[Points / 2].X, Spine[Points / 2].Y, 0.5f);
        _goal = new PointF(_x + MathF.Cos(_heading) * _u * 0.3f, _y + MathF.Sin(_heading) * _u * 0.3f);
    }

    /// <summary>A point beyond the screen's edge in direction a from the pond's middle.</summary>
    private PointF OffScreen(float a)
    {
        PointF c = _pond.Centre;
        float dx = MathF.Cos(a), dy = MathF.Sin(a), margin = _u * 0.08f;
        float tx = dx > 0 ? (_pond.Width + margin - c.X) / dx : dx < 0 ? (-margin - c.X) / dx : float.MaxValue;
        float ty = dy > 0 ? (_pond.Height + margin - c.Y) / dy : dy < 0 ? (-margin - c.Y) / dy : float.MaxValue;
        float t = MathF.Min(tx, ty);
        return new PointF(c.X + dx * t, c.Y + dy * t);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>How far point p is from the line piece a to b.</summary>
    private static float DistToLine(PointF p, PointF a, PointF b)
    {
        float ex = b.X - a.X, ey = b.Y - a.Y, e2 = ex * ex + ey * ey;
        float t = e2 < 1e-6f ? 0 : Math.Clamp(((p.X - a.X) * ex + (p.Y - a.Y) * ey) / e2, 0, 1);
        return Dist(p, new PointF(a.X + ex * t, a.Y + ey * t));
    }
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailZ[_trailHead] = _zHead;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <summary>
    /// How much room there is at a point: the distance to the edge of the
    /// nearest obstacle, in pixels (negative inside one). The log counts as
    /// a thick line: the distance to its centre line, less half its width.
    /// </summary>
    /// <summary>What the snake must swim round this frame besides the scenery: points along the ducks' bodies, screen pixels, each a circle of "radius". Set before Update; an empty span clears it.</summary>
    public void Avoid(ReadOnlySpan<PointF> beads, float radius)
    {
        _mvN = Math.Min(beads.Length, _mvX.Length);
        for (int i = 0; i < _mvN; i++) { _mvX[i] = beads[i].X; _mvY[i] = beads[i].Y; _mvR[i] = radius; }
    }

    private float Clear(float px, float py)
    {
        float best = _u;
        for (int i = 0; i < _obsCentre.Length; i++)
        {
            float dx = px - _obsCentre[i].X, dy = py - _obsCentre[i].Y;
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - _obsRadius[i]);
        }
        for (int i = 0; i < _mvN; i++)
        {
            float dx = px - _mvX[i], dy = py - _mvY[i];
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - _mvR[i]);
        }
        // THE BED IS A WALL TO A DIVED SNAKE (round forty: "they appear to stretch and swim through rocks", "the
        // rock just needs to be a 3d thing that's in the way"). Until now the feelers knew only the circles and the
        // land; the one thing that knew a stone's underwater hill was the step rule, which refused the step only
        // once the head was at the hill's foot. So a deep snake swam straight at a stone, stopped dead, and pivoted
        // on the spot: the right angle he saw laid into its body. Now a point whose bed is shallower than the head
        // is deep counts as inside something, by the shortfall (a fifth of u per unit of depth, about the slope of a
        // stone's hill), so the feelers see a hill coming the way they see the bank and the head turns early and
        // round it. Only once it is really under (0.06): on the surface the shallows are no wall, or it could never
        // cross to the land.
        if (_zHead > 0.06f) best = MathF.Min(best, (0.9f * FloorZ(px, py) - _zHead) * _u * 0.2f);
        // An exploring snake stays on the picture: beyond the edge counts as a wall while its head is on the picture (a fed
        // snake crawled along below the bottom edge for twenty seconds with its bulge out of sight). A crossing is meant to
        // leave, and a prowl (phase 1) is not walled either: the prey sits on the land corner by the picture's edge, and with
        // the wall up no prowl on two natural seeds got within 0.3u of it.
        // On a crossing the same, for every edge but the one the goal lies beyond: a crossing begun at the land corner went round
        // the bank rocks, under the bottom edge, and ran east BELOW the picture for a thousand pixels, the goal's pull too weak
        // to bend it back. The exit edge stays open, so the crossing can leave.
        // A retreat along its crumbs has no walls: the crumbs were laid without them (a prowl's may lie a little off the picture).
        if ((_doing == Doing.Explore && _phase == 0 || _doing is Doing.Cross or Doing.Snatch) && !Retreating && _x >= 0 && _y >= 0 && _x < _pond.Width && _y < _pond.Height)
        {
            float wall = _u;
            if (!(_doing == Doing.Cross && _goal.X < 0)) wall = MathF.Min(wall, px);
            if (!(_doing == Doing.Cross && _goal.X >= _pond.Width)) wall = MathF.Min(wall, _pond.Width - px);
            if (!(_doing == Doing.Cross && _goal.Y < 0)) wall = MathF.Min(wall, py);
            if (!(_doing == Doing.Cross && _goal.Y >= _pond.Height)) wall = MathF.Min(wall, _pond.Height - py);
            best = MathF.Min(best, wall - _u * 0.03f);
        }
        // A prowl may put its head a twentieth of the screen past an edge (the prey sits on the land corner, right by it) and
        // no further: with no wall at all, one prowl toured the whole pond off the picture for 46 seconds and never arrived.
        if (_doing == Doing.Explore && _phase == 1)
            best = MathF.Min(best, MathF.Min(MathF.Min(px, _pond.Width - px), MathF.Min(py, _pond.Height - py)) + _u * 0.05f);
        if (_log is Log log && !(_doing == Doing.Bask && _phase >= 1))   // a snake climbing on, lying on or slipping off the log does not mind it
        {
            PointF a = log.At(0), b = log.At(1);
            float ex = b.X - a.X, ey = b.Y - a.Y, e2 = MathF.Max(1e-3f, ex * ex + ey * ey);
            float t = Math.Clamp(((px - a.X) * ex + (py - a.Y) * ey) / e2, 0, 1);
            float dx = px - (a.X + ex * t), dy = py - (a.Y + ey * t);
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - log.Width * 0.5f);
        }
        return best;
    }

    /// <summary>Is the head on the land corner (the lawn, the sand, a bank rock)? A snake on the land takes the stalk's margin: it got in
    /// by the gaps between the bank rocks at 0.02u, and at the swimmer's 0.035u every gap read as shut, so after a meal it circled the lawn
    /// (seed 5 stress: 150 s; seed 21: 68 s) with nothing to retreat along when the stalk's crumbs had been overwritten.</summary>
    private bool HeadOnLand()
    {
        int ix = (int)_x, iy = (int)_y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Land[iy * _pond.Width + ix];
    }

    /// <summary>Is the straight line from the head to a point clear, with the stalk's margin (0.015u) all the way? Sampled every 0.015u.</summary>
    private bool LineClear(PointF to)
    {
        float dx = to.X - _x, dy = to.Y - _y, d = MathF.Sqrt(dx * dx + dy * dy);
        int n = Math.Min(60, (int)MathF.Ceiling(d / (_u * 0.015f)));
        for (int k = 1; k <= n; k++)
        {
            float f = (float)k / n;
            if (Clear(_x + dx * f, _y + dy * f) < _u * 0.015f) return false;
        }
        return true;
    }

    /// <summary>
    /// The least room along a heading over the next "dist" pixels, felt at
    /// six points from just past the snout to the full distance. The near
    /// ones matter: a lily pad is smaller than the gap between far feelers,
    /// and the first build sat jammed against one for two minutes because
    /// every feeler landed in clear water beyond it.
    /// </summary>
    private float ClearAlong(float a, float dist)
    {
        float dx = MathF.Cos(a), dy = MathF.Sin(a), best = _u;
        // The first sample is the very next step (0.02 of the look, about five pixels): the head once sat on the rim of a small
        // stone with every step refused, because the first sample at 0.08 landed beyond the stone and called the heading clear.
        foreach (float f in (ReadOnlySpan<float>)[0.02f, 0.08f, 0.2f, 0.35f, 0.55f, 0.78f, 1f])
            best = MathF.Min(best, Clear(_x + dx * dist * f, _y + dy * dist * f));
        return best;
    }

    /// <param name="animals">Points on the other animals this frame (koi heads and middles, turtles, the lizard), screen pixels.</param>
    /// <param name="lizards">The lizards, so it can meet one on the bank (null: none).</param>
    /// <param name="mice">The mice, likewise.</param>
    public void Update(float dt, ReadOnlySpan<PointF> animals, Lizards? lizards = null, Mice? mice = null)
    {
        _lizards = lizards; _mice = mice;
        _clock += dt;
        _hunger = MathF.Min(1f, _hunger + dt * Appetite / _hungerSecs);      // hunger creeps up whether it is on screen or not
        _snatch.Tick(dt);                                                    // the rest after a chase runs whether it is on screen or not
        // ANY other doing taking over (a fright, a hunt, the jam breaker, the visit ending) ends a chase in progress: one
        // check here covers every Begin that could replace it. (It also frees the tadpole it had singled out for the others.)
        if (!_live || _doing != Doing.Snatch) _snatch.Abandon();
        if (!_live || _doing != Doing.Bask) _pond.LogPerch.Release(this);   // the log is only the snake's while it is basking (or on its way to)
        if (!_live)
        {
            // Drawn out by prey: while a lizard or a mouse is out on the bank the wait for the next visit runs twice as
            // fast (a snake comes to where there is something to eat). Without it, most prowls arrived after the prey had gone.
            // And a snake past half hungry comes out sooner: at famished the wait runs twice as fast.
            _next -= dt * (NearestPrey() != null ? 2f : 1f) / (1f - 0.5f * Math.Clamp((_hunger - 0.5f) * 2f, 0f, 1f));
            if (_next <= 0 && _often > 0) { Begin(); _next = (150 + 150 * (float)_rng.NextDouble()) / _often; }
            return;
        }
        _alive += dt;
        // ---- Depth ----
        // An explore may be a dive (BeginExplore): the head eases down to its target and the body follows it
        // down the trail (LaySpine); any other doing, and the end of the explore, eases it back up to the
        // surface over ten seconds or so. Never deeper than the bed under the head allows. Deep, it hunts and
        // basks at nothing (Decide, Scan, Notice, LookForTadpole), pushes no petal and is on no surface list.
        if (_doing is not (Doing.Explore or Doing.Cross) && !(_doing == Doing.Snatch && _snatch.Hunting && _snatch.Prey.Stage >= 6)) _zTarget = 0;   // (a chase after a dweller of the deep bed keeps the head down: ThinkSnatch sets its depth)
        // The bed under the head AND a little ahead of it: the step rule refuses a step into a bed shallower than the
        // head, and a head that rose only with the bed already under it waited at every steep slope (a rock's hill,
        // where the prawns live: "snake gets hung up on prawn somehow. happened near a rock") for the 5 s jam breaker.
        float aheadPx = _u * 0.06f;
        float bedZ = MathF.Max(0, 0.9f * MathF.Min(FloorZ(_x, _y), FloorZ(_x + MathF.Cos(_heading) * aheadPx, _y + MathF.Sin(_heading) * aheadPx)) - 0.05f);
        bool onBed = _doing == Doing.Snatch && _snatch.Hunting && _snatch.Prey.Stage >= 6;   // after a dweller of the deep bed: the head goes down to it four times as fast as a dive eases (the chase is eight seconds; at the dive's ease the head was still a reach above the prey when the patience ran out)
        _zHead += (MathF.Min(_zTarget, bedZ) - _zHead) * (1 - MathF.Exp(-dt * (onBed ? 1f : 0.25f)));
        if (_zHead > bedZ) _zHead = MathF.Max(bedZ, _zHead - 0.2f * dt);              // too deep for the bed ahead: up at a set rate, not the slow ease (which never got there while the step stood refused)              // too deep for the bed ahead: up at a set rate, not the slow ease (which never got there while the step stood refused)
        MotionLog?.Invoke(FormattableString.Invariant($"F,{_clock:F3},{_doing},{_phase},{_x:F1},{_y:F1},{_zHead:F3},{_zTarget:F2},{_speed:F1},{HuntNote},{(_blocked ? 1 : 0)},{bedZ:F3},{_heading:F3},{_drawnLen / _len:F3},{_capped}"));   // (round forty added the heading, the drawn body's length over its own, and how many points the bed lifted last frame: the stretch and the swim-through, in numbers)

        // ---- A touch from another animal: startle ----
        // A bold snake feels a touch only when it is close (0.035u), a nervous one
        // from further (0.06u), and the nervous one stays frightened longer.
        _calm.Tick(dt);
        _flee = MathF.Max(0, _flee - dt);
        // A snake with a mouthful, or in the middle of a strike, does not startle: it cannot swing away from
        // anything. The lizard it is stalking (or chasing) does not startle it either: that lizard IS the hunt.
        bool busy = _doing is Doing.Strike or Doing.Swallow;
        bool hunting = _target != null && _doing is Doing.Stalk or Doing.Miss;
        PointF preyNose = hunting ? _target!.PreyNose : default;
        if (_calm.Ready && !busy)
        {
            float near = _u * (0.035f + 0.025f * _nerve), near2 = near * near;
            for (int k = 0; k < animals.Length && _calm.Ready; k++)
                for (int i = 0; i < Points; i += 3)
                {
                    if (hunting && Dist(animals[k], preyNose) < 3f) break;
                    float dx = Spine[i].X - animals[k].X, dy = Spine[i].Y - animals[k].Y;
                    if (dx * dx + dy * dy > near2) continue;
                    // Away from whatever touched it, judged from its head.
                    _fleeAway = MathF.Atan2(_y - animals[k].Y, _x - animals[k].X);
                    _flee = _fed ? MathF.Min(_fleeFor, 0.5f) : _fleeFor;      // a fed snake only flinches
                    _calm.Start(7 + 5 * _nerve + 6 * (float)_rng.NextDouble());
                    _ripples.Add(Spine[0].X, Spine[0].Y, 0.7f);
                    // A fright ends a lie on the log, and a nervous snake's hunt; a bold snake gets over
                    // it and goes back to stalking (every hunt was being cut short by a koi on the way).
                    if (_doing == Doing.Hunt && _nerve > 0.5f) Decide();
                    else if (_doing == Doing.Snatch) Decide();                   // a touch ends the chase (the check at the top of Update frees the tadpole)
                    else if (_doing == Doing.Bask && _phase == 0) Decide();
                    else if (_doing == Doing.Bask && _phase <= 2) StartSlip();     // already on the log: off it by the log's own line (Decide here left it inside the log, stuck, for 88 s)
                    break;
                }
        }
        float fear = _flee / _fleeFor;                     // 1 the instant it is touched, fading to 0 as it calms

        Scan();
        Notice();
        LookForTadpole(dt);
        Think(dt);
        UpdateGulp(dt);
        UpdateBulge(dt);
        if (_doing == Doing.Bask && _phase == 2) { UpdateLie(dt); return; }    // lying still: the spine is laid by the pose, nothing steers

        // ---- Speed: a steady swim, slower when stalking or climbing, a burst when startled or striking ----
        bool striking = _phase == 1 && _doing is Doing.Hunt or Doing.Snatch;
        // The creep of a stalk is about a third of a cruise (0.4: a third let the prey wander off first); a prowl, going to look, is
        // a brisk 1.7 (prey is out for only half a minute); the chase after a miss a third faster than a cruise; a fed snake is slow.
        float pace = _doing == Doing.Hunt && _phase == 0 ? StalkPace() : _doing == Doing.Bask && _phase == 1 ? 0.6f
                   : _doing == Doing.Stalk ? 0.4f : _doing == Doing.Miss ? 1.3f : _doing == Doing.Snatch ? 1.4f : _doing == Doing.Explore && _phase == 1 ? 1.7f : 1f;
        if (_fed) pace *= 0.6f;
        if (_gulp > 0) pace *= 0.35f;                                                     // a mouthful: it slows to swallow
        float wantSpeed = striking ? _u * LungeSpeed : _cruise * pace * (1 + 2.2f * fear);
        // The chase after a miss holds off at 0.08u from the prey's nose (the prey is faster, and its tail is between its nose and
        // the jaws: without this the jaws ran onto a fleeing lizard's tail with nothing happening, a bite that was not one).
        if (_doing == Doing.Miss && _phase == 1 && _target != null && Dist(Spine[0], _target.PreyNose) < _u * 0.08f) wantSpeed = 0;
        _speed += (wantSpeed - _speed) * (1 - MathF.Exp(-dt * (striking ? 14f : _gulp > 0 ? 9f : fear > 0 ? 6f : 1.2f)));   // a lunge gets up to speed almost at once; a mouthful brings it up short (it is at lunge speed when the jaws close, and coasting on through the school would not read as a swallow)
        // Holding still for a strike, a mouthful or the look-about after a miss: not a crawl, a stop.
        if (_doing is Doing.Strike or Doing.Swallow || (_doing == Doing.Miss && _phase == 0)) _speed = 0;

        // ---- Steering ----
        // A gentle pull toward the goal the mind chose (firmer when climbing onto
        // the log or striking), a slow wander, and when startled a hard swing
        // away from whatever touched it.
        // A prowl pulls as hard as a stalk (1.8, not the cruise's 0.5): at 0.5 the avoidance turns round the bank rocks won
        // every time and the prowl toured the pond's far side instead of arriving. A retreat pulls the same, crumb to crumb.
        bool prowling = _doing == Doing.Explore && _phase == 1;
        float pull = striking ? 2.5f : _doing == Doing.Bask && _phase == 1 ? 1.5f : _doing is Doing.Stalk or Doing.Miss or Doing.Snatch || prowling || Retreating ? 1.8f : 0.5f;
        // A DETOUR (the orbit breaker below fired): the pull toward the goal is off for a few seconds and the snake simply
        // swims on, steering round whatever is in the way, so it comes out past the thing it was circling before the goal
        // takes hold of it again. A hunt, a stalk, a strike or a chase keeps its pull: those aim at a moving animal, not a spot.
        if (_detour > 0 && !striking && !(_doing is Doing.Stalk or Doing.Strike or Doing.Swallow or Doing.Miss or Doing.Snatch or Doing.Hunt)) pull = 0;
        PointF goal = Retreating ? _way[_wayI] : _goal;
        float turn = Wrap(MathF.Atan2(goal.Y - _y, goal.X - _x) - _heading) * pull;
        if (!striking && !Retreating && !(_doing == Doing.Bask && _phase == 1) && _doing is not (Doing.Stalk or Doing.Strike or Doing.Swallow or Doing.Miss or Doing.Snatch)) turn += 0.2f * MathF.Sin(_alive * 0.35f);
        float most = (striking ? 2.5f : _doing == Doing.Snatch ? 1.8f : 1.0f) + 1.5f * fear;   // a chase turns a little tighter than a cruise

        // Something in the way? Feel for the smallest turn that clears it.
        // "Enough room" is the head's own width a few times over; past that,
        // more room counts for nothing, so it skirts a pad rather than
        // heading for the far side of the pond.
        // Stalking or chasing a miss it goes closer past rocks to get at what sits on them (0.02u, not 0.035u; less and the body swung into the stone).
        // A retreat keeps to the gaps a stalk came in by, so it takes the stalk's margin too.
        // On a detour it feels its way closer (the look cut to two fifths): the full look sees the rim of the pocket it is in
        // on every heading and steers along it, which is the orbit the detour is meant to leave.
        float look = (_len * 0.8f + _speed * 0.8f) * (_detour > 0 ? 0.4f : 1f), margin = _u * (_doing is Doing.Stalk or Doing.Miss || Retreating || HeadOnLand() ? 0.02f : 0.035f);    // wider than the weave's swing, so the body clears too
        float clearAhead = _blocked ? 0 : ClearAlong(_heading, look);
        // A fright swings it away from what touched it, but never into something
        // else: the nearer an obstacle, the less the swing counts (an early
        // build fled straight into a lily pad and sat inside it).
        if (fear > 0) turn += Wrap(_fleeAway - _heading) * 2.5f * fear * Math.Clamp(clearAhead / margin, 0, 1);
        // A chase's pull toward the prey gives way to an obstacle ahead, the nearer the less it counts. With the prey behind
        // the head and a rock in front, the pull (turn back toward the prey) and the avoidance (turn to the clear heading) were
        // equal and opposite, and the snake sat on the rock's margin for three seconds, head still, blocked every frame.
        if (_doing == Doing.Snatch) turn *= Math.Clamp(clearAhead / margin, 0f, 1f);
        if (clearAhead < margin)
        {
            float best = _heading, bestScore = MathF.Min(clearAhead, margin * 2);
            // The whole circle, not a hundred degrees either side: in a dead end among the bank rocks (after a catch on the land)
            // every near heading is blocked, and a search that stops there returns the heading it has and the snake stands on
            // the stone for as long as the run lasts. The turn penalty still prefers the smallest turn that clears.
            for (int k = 1; k <= 9; k++)
                foreach (int side in (ReadOnlySpan<int>)[1, -1])
                {
                    float a = _heading + side * k * 0.35f;
                    float score = MathF.Min(ClearAlong(a, look), margin * 2) - k * margin * 0.15f;
                    if (score > bestScore) { bestScore = score; best = a; }
                }
            if (bestScore <= 0)
            {
                // Nothing clears within the look (deep among the bank rocks every long line hits a stone or the edge): look again
                // at a third of the distance, and failing that go uphill on the clearance itself. Without this the snake stood on
                // the bank, every step refused, for the rest of a run.
                for (int k = 0; k <= 9; k++)
                    foreach (int side in (ReadOnlySpan<int>)[1, -1])
                    {
                        float a = _heading + side * k * 0.35f;
                        float score = MathF.Min(ClearAlong(a, look * 0.3f), margin * 2) - k * margin * 0.15f;
                        if (score > bestScore) { bestScore = score; best = a; }
                        if (k == 0) break;
                    }
                if (bestScore <= 0)
                {
                    float gx = Clear(_x + 2, _y) - Clear(_x - 2, _y), gy = Clear(_x, _y + 2) - Clear(_x, _y - 2);
                    if (MathF.Abs(gx) + MathF.Abs(gy) > 1e-4f) best = MathF.Atan2(gy, gx);
                }
            }
            float urgency = Math.Clamp(1 - clearAhead / margin, 0, 1);
            turn += Wrap(best - _heading) * (1.5f + 3f * urgency);
            most = MathF.Max(most, 2.2f);
        }
        // THE TURNING CIRCLE (round forty): the head may not turn tighter than a body can follow. At its cruise
        // (0.06u a second, a fifth of its length) the 2.2 radians a second above was a circle a tenth of a body
        // length across: a right angle in a quarter of a second, laid into the trail the body is drawn along, so
        // the body showed a corner at every stone ("janky 90 turns around the rock"). The floor is the koi's: a
        // circle 0.4 body lengths across, 0.3 with something inside the margin, at the speed it is going (never
        // under an eighth of a length a second, so a stopped snake can still turn). A strike is faster than the
        // floor binds; the walk out from inside an obstacle (below) keeps its own rate.
        most = MathF.Min(most, MathF.Max(_speed, _len / 8f) / ((clearAhead < margin ? 0.3f : 0.4f) * _len));
        float swung = Math.Clamp(turn, -most, most) * dt;
        _heading = Wrap(_heading + swung);
        // ---- The orbit breaker ----
        // A goal on the far side of a lily pad cluster: the pull toward the goal turns the head one way, the avoidance
        // round the pads turns it the same way, and the head goes round and round the cluster at full speed in a circle
        // a third of a body length across, with the body laid along that circle: a snake balled up on a lily pad (seed 7
        // natural, 2026-10-26: a 12.6 s orbit of 100 by 76 pixels at 720p, nothing blocking it, four shorter ones either side).
        // The heading's turning is summed with an eight second memory; a whole circle and a bit (6.9 radians, more than any
        // honest turn round a stone) means an orbit. The answer: THE WAY OUT IS THE WAY IN, a retreat along its crumbs to
        // the last one in open water, forced (the lawn pocket it circled read as roomy to the room grid, 0.06u of clear on
        // every side, so the ordinary retreat test passed it by: a pull-free detour alone left seed 7 circling 13 s, since
        // the goal pull was not the only thing turning it, the avoidance round the pocket's rim was). With no crumb in
        // open water to walk back to, a detour: no pull for four seconds (above) and it simply swims on.
        _spin = _spin * MathF.Exp(-dt / 8f) + swung;
        _detour = MathF.Max(0, _detour - dt);
        // (At 6.9 radians, a circle and a bit, the breaker caught the lawn orbit on seed 3 stress after 6.5 s of circling and the
        // coil was on the picture 7.3 s; at 5.5, seven eighths of a circle, still past any honest turn, it is caught sooner.)
        if (MathF.Abs(_spin) > 5.5f)
        {
            _spin = 0;
            if (!Retreating && Retreat(force: true)) Note("orbit:retreat");
            else if (HeadOnLand() && !(_doing is Doing.Strike or Doing.Swallow or Doing.Stalk or Doing.Miss)) { LeaveByNearestEdge(); Note("orbit:leave"); }   // on the lawn with no crumb to walk back on: out by the nearest edge, which the land corner touches (seed 3 stress kept a 6 s orbit there through a detour)
            else { _detour = 4f; _wayI = _wayN; Note("orbit:detour"); }
        }

        // ---- Forward, unless the next step would be inside something ----
        float step = _speed * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        float clearNext = Clear(nx, ny);
        float clearHere = Clear(_x, _y);
        _blocked = !(clearNext > 0 || clearNext >= clearHere)    // never stuck: a step toward more room is always allowed
                   || (_zHead > 0.9f * FloorZ(nx, ny) && FloorZ(nx, ny) < FloorZ(_x, _y));   // and never into a bed too shallow for its depth (a dive turns at a hill; a step toward deeper water is always allowed)
        if (clearHere < 0 && !(_doing is Doing.Strike or Doing.Swallow or Doing.Stalk or Doing.Miss) && !(_doing == Doing.Bask && _phase >= 1))
        {
            // INSIDE something (a bank rock's circle, entered on a stalk or a strike through its margin): the head walks straight
            // out, uphill on its own clearance, the goal's pull set aside until it is clear. With the pull still turning the head
            // back toward the rock every frame it jittered two pixels in a pocket between two stones for nineteen minutes.
            float gx = Clear(_x + 2, _y) - Clear(_x - 2, _y), gy = Clear(_x, _y + 2) - Clear(_x, _y - 2), gl = MathF.Sqrt(gx * gx + gy * gy);
            if (gl > 1e-4f)
            {
                // The heading TURNS toward the way out, at most 4 radians a second, instead of being set to it: inside two
                // things at once (the picture's edge wall and a bank rock) the way out flips between "up" and "right" from
                // one frame to the next, as whichever is deeper wins, and a heading set outright shook the head 10 pixels a
                // frame for 21 seconds. Eased, it settles between the two, which clears both.
                float outA = MathF.Atan2(gy, gx);
                _heading = Wrap(_heading + Math.Clamp(Wrap(outA - _heading), -4f * dt, 4f * dt));
                float ex = _x + MathF.Cos(_heading) * step, ey = _y + MathF.Sin(_heading) * step;
                if (Clear(ex, ey) >= clearHere) { nx = ex; ny = ey; }
                else { nx = _x + gx / gl * step; ny = _y + gy / gl * step; }
                _blocked = false;
            }
        }
        if (!_blocked)
        {
            _x = nx; _y = ny;
            _travelled += step;
        }
        // A crumb every 0.03u of the way, with how open the water is there; and on a retreat, the next crumb once this one is
        // reached. A retreat that has not got there in 15 seconds (a crumb the body cannot reach) is dropped for the doing's own goal.
        {
            int last = _crumbHead;
            float cx = _x - _crumbX[last], cy = _y - _crumbY[last];
            if (_crumbCount == 0 || cx * cx + cy * cy > _u * _u * 0.03f * 0.03f)
            {
                _crumbHead = (_crumbHead + 1) % _crumbX.Length;
                _crumbX[_crumbHead] = _x; _crumbY[_crumbHead] = _y; _crumbRoom[_crumbHead] = _pond.RoomAt(_x, _y);
                _crumbCount = Math.Min(_crumbCount + 1, _crumbX.Length);
            }
            if (Retreating)
            {
                _retreatT += dt;
                // String-pulling: the crumbs of a looping stalk loop too, and walked one by one they replayed the loops
                // (45 s and 1.7u of path to get 0.3u away on one seed). So each frame the snake looks for the FURTHEST
                // crumb along the way it can reach in a straight clear line, and goes for that one.
                for (int j = _wayN - 1; j > _wayI; j--)
                    if (LineClear(_way[j])) { _wayI = j; break; }
                // Arrived: within the crumb's own 0.025u, but never closer than the snake's TURNING CIRCLE. A fed snake creeps at
                // 0.6 of a cruise (27 px a second at 720 lines) and turns at most a radian a second, so it cannot make a circle
                // under 27 px across: with the old flat 18 px it went round and round a crumb 34 px away until the 25 s ran out
                // (seed 5, stress: the retreat from the lawn never reached its first crumb, and the snake then circled the
                // sealed lawn for two minutes). Like a car told to stop within a lane's width of a post it cannot turn that tight to reach.
                if (Dist(new PointF(_x, _y), _way[_wayI]) < MathF.Max(_u * 0.025f, 1.3f * _speed)) _wayI++;
                if (_retreatT > 25f || !(_doing == Doing.Explore && _phase == 0 || _doing == Doing.Cross)) _wayI = _wayN;
            }
        }
        // A jam breaker (found by a 20 minute run: on one seed a snake sat 10 minutes at the screen's edge inside a lily pad's
        // margin, trying every heading and refused every step). Blocked for 5 seconds: if it is out of sight it has left; if it is
        // on screen it gives up what it was doing and sets off across the pond again.
        _blockedT = _blocked ? _blockedT + dt : 0;
        if (_blockedT > 5f && !(_doing is Doing.Strike or Doing.Swallow))
        {
            _blockedT = 0;
            if (_x < 0 || _y < 0 || _x > _pond.Width || _y > _pond.Height) { _target = null; _live = false; return; }
            _target = null; BeginCross(); MaybeRetreat();
        }
        // Gone: leaving, and its head is far enough past an edge that its tail has left too.
        float m = _len * 1.25f;
        if (_doing == Doing.Cross && _alive > 3 && (_x < -m || _y < -m || _x > _pond.Width + m || _y > _pond.Height + m)) { _live = false; return; }

        // The weave: side to side, a full swing every 65% of a body length
        // travelled, so about one and a half waves lie along its body at once.
        // Distance drives it, so a burst of speed quickens the weave by itself.
        // Its width eases to the doing's: narrow while stalking or climbing (a
        // creeping snake does not thrash), nearly straight in a strike.
        float weaveWant = striking ? 0.3f : _doing is Doing.Hunt or Doing.Snatch ? 0.5f : _doing == Doing.Bask && _phase == 1 ? 0.35f : _doing == Doing.Stalk ? 0.25f : _doing == Doing.Strike ? 0.1f : 1f;
        weaveWant *= 0.4f + 0.6f * Math.Clamp(clearAhead / (margin * 2), 0, 1);       // and narrower still beside an obstacle, so the body's swing never brushes it
        _weaveAmp += (weaveWant - _weaveAmp) * (1 - MathF.Exp(-dt * 1.5f));
        // A snake that has stopped does not weave: the gate closes (quickly) when it is not moving, so the head
        // does not hang off to one side of a held pose, and opens when it moves.
        _weaveGate += ((_speed > _cruise * 0.15f ? 1f : 0f) - _weaveGate) * (1 - MathF.Exp(-dt * 8f));
        float weave = _len * 0.085f * _weaveAmp * _weaveGate * MathF.Sin(_travelled / (_len * 0.65f) * MathF.Tau);
        float hx = MathF.Cos(_heading), hy = MathF.Sin(_heading);
        var head = new PointF(_x - hy * weave, _y + hx * weave * Ripples.Squash);
        if (Dist(head, _trail[_trailHead]) > _len / 150) Remember(head);

        // A frightened snake thrashes: rings pushed out from its head while it flees. (Until 2026-10-23 it
        // rang its head every 0.4 s whenever it swam; its ordinary wake is now the bow wave below, Ripples.Wake,
        // the same thin V every swimmer at the surface leaves, so the pond stays calm.)
        _wakeTimer -= dt;
        int ix = (int)head.X, iy = (int)head.Y;
        if (_wakeTimer <= 0 && fear > 0.05f && ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix])
        {
            _ripples.Add(head.X, head.Y, 0.4f + 0.2f * fear);
            _wakeTimer = 0.4f - 0.2f * fear;
        }
        LaySpine(head);
        // Swimming, its head pushes a small bow wave (Ripples.Wake), as a koi's does at the surface.
        if (InWater) _ripples.Wake(ref _wakeRun, Spine[0].X, Spine[0].Y, _heading, _speed, dt, 0.5f * MathF.Min(1, _speed / _cruise));
        if (_poseK != 0 || _pullK != 0) ApplyPose();
        if (_doing == Doing.Swallow && _target != null)
        {
            // The prey is held by the head: its nose at the jaws THIS frame, its body out in front (the thrash
            // is violent for the first second and dies to a twitch by the time the tail goes in).
            float thrash = _phase == 0 ? 3f : 2.2f - 1.6f * _swallowed;
            _target.Hold(Spine[0], _preyDir, _swallowed, thrash, dt);
        }
    }

    /// <summary>
    /// The gulp after a mouthful (a tadpole or a minnow): for 0.7 s the head swells a little as the jaws work and the neck
    /// bows in a small S (the strike's pose, a quarter as deep), then settles. No bulge: a minnow is a mouthful. The
    /// strike, the swallow and the miss own the mouth while they run, so a gulp that meets one of them is dropped.
    /// </summary>
    private void UpdateGulp(float dt)
    {
        if (_gulp <= 0) return;
        _gulp = MathF.Max(0f, _gulp - dt);
        if (_doing is Doing.Strike or Doing.Swallow or Doing.Miss) { _gulp = 0; return; }
        float k = 1f - _gulp / GulpFor, s = MathF.Sin(MathF.PI * k);
        _mouth = 1f + 0.3f * s; _poseK = 0.25f * s; _pullK = 0;
        if (_gulp <= 0) { _mouth = 1; _poseK = 0; }
    }

    /// <summary>Counts the meal on its way down the body: it moves from behind the head to the middle, then slowly shrinks.</summary>
    private void UpdateBulge(float dt)
    {
        if (_bulgeT < 0) return;
        _bulgeT += dt;
        _bulgeS = 0.14f + 0.31f * MathF.Min(1, _bulgeT / _bulgeTravel);
        _bulgeAmp = _bulgeBase * (_bulgeT < _bulgeTravel ? 1f : MathF.Max(0, 1 - (_bulgeT - _bulgeTravel) / 25f));
        if (_bulgeAmp <= 0) { _bulgeT = -1; _bulgeAmp = 0; }
    }

    /// <summary>
    /// The S in the neck of a strike (and the kick of a miss), laid over the spine for the DRAWING only
    /// (the chain the koi feel is the plain one). The front third of the body is moved: the head is
    /// pulled back along the way it points and the neck bowed sideways, most at the head and fading to
    /// nothing a third of the way down, so the rest of the snake holds still behind it. k = 1 is the
    /// full cock (the head 0.0035u back, a third of the body's width, and a bow of 0.014u); a small
    /// negative or positive k is a kick. The bow builds over the whole third of a second; the few
    /// pixels of retreat happen at once and are held (a slow backward creep of the drawn head would
    /// read as the snake going backward).
    /// </summary>
    private void ApplyPose()
    {
        float fx = Spine[0].X - Spine[4].X, fy = Spine[0].Y - Spine[4].Y, fl = MathF.Max(1e-3f, MathF.Sqrt(fx * fx + fy * fy));
        fx /= fl; fy /= fl;
        float pull = _u * 0.0035f * _pullK, bow = _u * 0.014f * _poseK;
        for (int i = 0; i < Points; i++)
        {
            float t = Along[i] / 0.33f;
            if (t >= 1) break;
            float w = (1 - t) * (1 - t);
            float side = bow * MathF.Sin(t * MathF.Tau) * (1 - t);
            Spine[i] = new PointF(Spine[i].X - fx * pull * w - fy * side, Spine[i].Y - fy * pull * w * Ripples.Squash + fx * side * Ripples.Squash);
        }
    }

    /// <summary>The spine, laid along the remembered trail, nose first.</summary>
    private void LaySpine(PointF head)
    {
        float cy = _pond.Centre.Y;
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = head, b = _trail[slot];
        float za = _zHead, zb = _trailZ[slot];
        // THE TRAIL IS MEASURED IN THREE DIMENSIONS (round forty). The body is laid out along the trail by length,
        // and that length used to be the ground distance alone: a snake rising a whole pond depth while it swam a
        // body length was still laid a full length along the ground, and then each point was sunk down the screen
        // by its own depth, so the drawn body was longer than the snake ("they appear to stretch"). Now a stretch
        // of trail that goes up or down counts its rise in the length, as a real body does, so a steeply rising snake
        // takes up less ground and the drawn body stays its own length.
        float zPx = Pond.DepthU * _u;                       // pixels of depth per unit of Z
        float done = 0, seg = Seg(a, b, za, zb, zPx);
        _capped = 0;
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (done + seg < want && left > 0)
            {
                done += seg; a = b; za = zb;
                slot = (slot - 1 + _trail.Length) % _trail.Length; left--;
                b = _trail[slot]; zb = _trailZ[slot]; seg = Seg(a, b, za, zb, zPx);
            }
            float t = seg > 1e-4f ? Math.Clamp((want - done) / seg, 0, 1) : 0;
            // Each point's depth is the depth the head had when it passed here (the trail remembers it), so a
            // diving snake goes down head first and the tail follows; drawn, the point sinks down the screen by
            // its depth as every swimmer does (Light.SinkPx) and is hazed by it (Light.Murk, nothing at the very
            // top: on the water it swims clear, as before).
            float z = za + (zb - za) * t;
            float sx = a.X + (b.X - a.X) * t, sy = a.Y + (b.Y - a.Y) * t;
            // On the water only the HEAD rides clear (round thirty-nine): the body behind the neck lies a hair under
            // the surface, a thirtieth of the depth, so it is veiled a little by the water and sits a pixel lower on the
            // screen than the head. Drawn at the surface entire, the snake was an opaque rope laid on the picture.
            if (!Deep && z < 0.035f && Along[i] > 0.06f) z = MathF.Max(z, 0.035f * MathF.Min(1f, (Along[i] - 0.06f) / 0.1f));
            // THE BED UNDER EVERY POINT (round forty). The trail remembers the depth the head had, and the head rises
            // over a stone's hill before its body gets there; the body behind it was then laid at the depth of the deep
            // water it had come through, UNDER the hill it was now crossing, and drawn sunk down the screen onto the
            // painted stone ("swim through rocks"). A point is never deeper than the bed under it allows (the head's
            // own rule, with a little less margin).
            float cap = MathF.Max(0f, 0.9f * FloorZ(sx, sy) - 0.03f);
            if (z > cap) { z = cap; _capped++; }
            _zAt[i] = z;
            Spine[i] = new PointF(sx, sy);                   // the ground point for now: the sink is added once the depths are smoothed
        }
        // THE BODY BENDS, IT DOES NOT STEP (round forty): a cap that lifts one point alone would put a kink in the
        // drawn body (a sink of a few pixels between neighbours). No point may be deeper than its neighbour by more
        // than the ground distance between them times 1.7 (a slope of sixty degrees, as steep as a snake dives), run
        // once each way along the body, so a point lifted by the bed lifts a smooth ramp of the body beside it. The
        // pass only ever makes a point shallower, so nothing it does puts a point back under the bed.
        float slope = 1.7f * (_len / (Points - 1)) / zPx;
        for (int i = 1; i < Points; i++) _zAt[i] = MathF.Min(_zAt[i], _zAt[i - 1] + slope);
        for (int i = Points - 2; i >= 0; i--) _zAt[i] = MathF.Min(_zAt[i], _zAt[i + 1] + slope);
        _drawnLen = 0;
        for (int i = 0; i < Points; i++)
        {
            float z = _zAt[i], sx = Spine[i].X, sy = Spine[i].Y;
            _murkAt[i] = Light.Murk(z) * Math.Clamp(z / 0.1f, 0, 1);
            Spine[i] = new PointF(sx, sy + Light.SinkPx(z, _u));
            if (i > 0) _drawnLen += Dist(Spine[i], Spine[i - 1]);
            Shape.Radius[i] = _wide * Profile(Along[i]);
            // The same point for touching (Core/Minds Chain), on the GROUND
            // plane: the spine is screen pixels, and the pond is squashed down
            // the screen by Ripples.Squash, so the ground y is the screen y
            // stretched back out from the pond's middle (the koi's ToGround),
            // and the depth is the point's own, in pixels.
            Shape.Points[i] = new System.Numerics.Vector3(sx, cy + (sy - cy) / Ripples.Squash, z * Pond.DepthU * _u);
        }
        Shape.Seal();
        Widths();
    }

    /// <summary>The length of one stretch of the trail in three dimensions: across the ground and up or down through the water (zPx pixels per unit of depth).</summary>
    private static float Seg(PointF a, PointF b, float za, float zb, float zPx)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = (za - zb) * zPx;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private int _capped;                                   // this frame: how many of the body's points the bed lifted (the instrument log carries it)
    private float _drawnLen;                               // this frame: the drawn spine's length, screen pixels (against _len: the stretch, for the log)

    /// <summary>
    /// How wide it is DRAWN: the plain width (the chain's radius) with the head opened for a strike and the
    /// meal's bulge moving down it. Only the drawing sees these; the fish still feel the plain snake.
    /// </summary>
    private void Widths()
    {
        for (int i = 0; i < Points; i++)
        {
            float w = Shape.Radius[i] * Light.Scale(_zAt[i]);      // smaller the deeper, as a koi is (Light.EyeU)
            if (Along[i] < 0.07f) w *= _mouth;
            if (_bulgeAmp > 0)
            {
                float d = MathF.Abs(Along[i] - _bulgeS) / 0.05f;       // the lump is a tenth of the body long
                if (d < 1) w *= 1 + _bulgeAmp * (0.5f + 0.5f * MathF.Cos(d * MathF.PI));
            }
            HalfWidth[i] = w;
        }
    }

    /// <summary>
    /// How wide the snake is along its length: a rounded head a little wider
    /// than the neck behind it, the body full width, then tapering steadily
    /// to the tip of the tail.
    /// </summary>
    private static float Profile(float s)
    {
        // The head seen from above (round thirty-nine): a rounded snout widening to the eyes, a third wider than the
        // neck, then a real neck before the body fills out. The old head was a bullet a fifth wider than the body
        // with no neck to speak of, and the whole animal read as a rope with two dots on the end.
        if (s < 0.045f) return 0.45f + 0.85f * MathF.Sin(s / 0.045f * MathF.PI * 0.5f);   // the snout rounding out to the head's widest, at the eyes
        if (s < 0.075f) return 1.3f - 0.55f * (s - 0.045f) / 0.03f;                       // the head narrowing to the neck (0.75)
        if (s < 0.14f) return 0.75f + 0.25f * (s - 0.075f) / 0.065f;                      // the neck filling out
        if (s < 0.6f) return 1f;
        float t = (s - 0.6f) / 0.4f;
        return MathF.Max(0.05f, (1 - t) * (1 - 0.35f * t));                                // the long taper to the tail, gentle at first and quicker near the tip
    }

    /// <summary>Which of the five skins this snake wears (0 shimahebi, 1 aodaisho, 2 yamakagashi, 3 hibakari, 4 karasuhebi), for the instruments.</summary>
    public int Skin => _skin;

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        alpha = 1;
        // A ROUND BODY (round thirty-nine). The edges are now well darker than the ridge (0.56 at the edge against
        // 0.72 before: the old shading left a flat tube with a faint rim), with a soft gleam down the ridge of the
        // back, and the whole lit by a lattice of scale rows: two waves crossing along and across the body, a few
        // percent light and dark, one row about every body width. Nothing is drawn as a scale (at this size a scale
        // is a dot, and a grid of dots is noise); the lattice only breaks the painted-tube look the way real
        // keeled scales catch the light in rows.
        float shade = 0.56f + 0.44f * MathF.Sqrt(MathF.Max(0, 1 - v * v)) + 0.07f * MathF.Max(0, 1 - av / 0.3f);
        float rows = _len / MathF.Max(1f, _wide * 2.2f);                                 // scale rows along the body
        float lattice = MathF.Sin((s * rows + v * 0.75f) * MathF.Tau) * MathF.Sin((s * rows - v * 0.75f) * MathF.Tau);
        shade *= 1 + 0.06f * lattice;
        bool head = s < 0.075f;                                                          // the head's own colour (Profile: the head ends at 0.075)
        int lift = 0;                                                                    // a cool highlight added after the shading (the black snake's sheen)
        switch (_skin)
        {
            default:
                // Shimahebi: olive back, darker on the head, with four dark stripes running down the body, soft-edged.
                (r, g, b) = head ? (104, 98, 60) : (138, 132, 84);
                if (s > 0.09f)
                {
                    float st = MathF.Min(MathF.Abs(av - 0.32f), MathF.Abs(av - 0.76f));
                    float k = Math.Clamp((0.09f - st) / 0.04f, 0f, 1f);
                    r += (int)((62 - r) * k); g += (int)((56 - g) * k); b += (int)((36 - b) * k);
                }
                break;
            case 1:
                // Aodaisho: olive green (cooler than before: the old olive painted yellow in the sun), a pale grey-green
                // head, and darker blotches in a loose chain down the spine (a blotch every 0.09 of the length, a little
                // to one side and then the other), each a soft rounded patch of its own size.
                if (head) (r, g, b) = (148, 156, 124);
                else
                {
                    (r, g, b) = (110, 124, 74);
                    float ph = s * 11f + _blot;
                    int cell = (int)ph;
                    float f = ph - cell;
                    float centre = (cell & 1) == 0 ? 0.12f : -0.12f;
                    float size = 0.8f + 0.1f * ((cell * 7) % 5);                          // 0.8 to 1.2, from the cell number (no roll)
                    float dx = (f - 0.37f) / (0.25f * size), dy = (v - centre) / (0.42f * size);
                    float d = MathF.Sqrt(MathF.Sqrt(dx * dx * dx * dx + dy * dy * dy * dy));   // a rounded box: 1 at its soft edge
                    float k = Math.Clamp((1.15f - d) / 0.35f, 0f, 1f);
                    r += (int)((84 - r) * k); g += (int)((96 - g) * k); b += (int)((56 - b) * k);
                }
                break;
            case 3:
                // Hibakari: a plain warm brown, a darker head, and a thin cream line along the side of the neck for a short way.
                (r, g, b) = head ? (104, 74, 44) : (140, 102, 62);
                if (s > 0.012f && s < 0.26f && MathF.Abs(av - 0.7f) < 0.09f)
                {
                    float k = Math.Clamp((0.26f - s) / 0.1f, 0f, 1f) * Math.Clamp((0.09f - MathF.Abs(av - 0.7f)) / 0.03f, 0f, 1f);
                    r += (int)((230 - r) * k); g += (int)((212 - g) * k); b += (int)((166 - b) * k);
                }
                break;
            case 4:
                // Karasuhebi: near black, the four stripes only just blacker, and a blue sheen down the ridge of the back.
                (r, g, b) = (34, 35, 36);
                if (s > 0.09f)
                {
                    float st = MathF.Min(MathF.Abs(av - 0.32f), MathF.Abs(av - 0.76f));
                    float k = Math.Clamp((0.09f - st) / 0.04f, 0f, 1f);
                    r += (int)((20 - r) * k); g += (int)((20 - g) * k); b += (int)((21 - b) * k);
                }
                if (av < 0.34f)
                {
                    float k = 1 - av / 0.34f;
                    lift = (int)(k * k * 100);
                }
                break;
        }
        r = (int)(r * shade); g = (int)(g * shade); b = (int)(b * shade);
        if (lift > 0) { r += lift * 26 >> 8; g += lift * 80 >> 8; b += lift * 170 >> 8; }
        return true;
    }

    /// <summary>The floating petals in its way are shoved aside (Petals.PushAside): it swims ON the water, so nothing of it passes under a petal.</summary>
    public void PushPetals(Petals petals)
    {
        if (Deep) return;                                   // under the surface it brushes no petal
        if (_live) petals.PushAside(Drawn, DrawnHalfWidth);
    }

    /// <summary>At the surface: drawn over the ripples and the floating petals, as before. (A deep snake is drawn by DrawDeep, under the fish.)</summary>
    public void Draw(FrameBuffer fb)
    {
        if (!_live || Deep) return;
        DrawBody(fb);
    }

    /// <summary>Under the surface: drawn before the koi school, so every fish shallower than it is painted over it (a fish deeper still is a known pass).</summary>
    public void DrawDeep(FrameBuffer fb)
    {
        if (!_live || !Deep) return;
        DrawBody(fb);
    }

    private void DrawBody(FrameBuffer fb)
    {
        bool[] st = Deep ? _openDeep : _pond.Open;
        Paint(fb, st, _pond.Sun, 0, MathF.Max(1f, _u * 0.0012f) * (1 + 2 * _zHead), water: _pond.Pixels, murkAt: _murkAt);
        // The eyes: tiny dark dots either side of the head.
        PointF e0 = Spine[1], e1 = Spine[2];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.7f, _wide * 0.3f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, st, new PointF(e0.X - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ex / el * HalfWidth[1] * 0.7f * side), 0, er, er, 20, 20, 16, 0.9f, _murkAt[1], _pond.Sun, water: _pond.Pixels);
    }
}

/// <summary>
/// What the snake needs to know about an animal it might eat, and what it tells it.
/// A lizard and a mouse each implement this (Lizard.cs, Mouse.cs); the Lizards and
/// Mice wrappers hand them out by number. The snake never reaches into either
/// animal's mind: it says "I am striking here, and you are (or are not) caught", and
/// the animal does the rest in its own way (a dodge, a jump, a thrash).
/// </summary>
internal interface IPrey
{
    /// <summary>Out on the screen and worth hunting: not hidden, not running for its life, not already caught or eaten.</summary>
    bool Huntable { get; }

    /// <summary>Where its nose is, screen pixels.</summary>
    PointF PreyNose { get; }

    /// <summary>Where the end of its tail is, screen pixels: with the nose, the line its body lies along.</summary>
    PointF PreyTail { get; }

    /// <summary>Out of the grass and on the screen (even if running): false once it has hidden, left or been eaten.</summary>
    bool Out { get; }

    /// <summary>
    /// The snake sets this while it is creeping up on this animal. A snake that has gone still and is creeping
    /// is hard to see: the animal only notices it from about two thirds of the usual distance.
    /// </summary>
    bool Stalked { get; set; }

    /// <summary>0 to 1: how well it dodges (a bold lizard, a calm mouse). Cuts the snake's chance.</summary>
    float Agility { get; }

    /// <summary>How big a lump it makes in the snake: 0.33 for a lizard, 0.5 for a mouse (the half-width swells by that much).</summary>
    float MealSize { get; }

    /// <summary>Nose to tail tip, screen pixels.</summary>
    float PreyLength { get; }

    /// <summary>
    /// The strike has been launched from <paramref name="from"/> (the snake's head) at <paramref name="at"/>.
    /// If <paramref name="caught"/> the animal freezes where it is and waits for the jaws; else it DODGES
    /// at once (a lizard flings itself aside, a mouse jumps straight up) and runs.
    /// </summary>
    void Strike(PointF from, PointF at, bool caught);

    /// <summary>
    /// Every frame while the animal is held: its nose goes to <paramref name="jaws"/>, its body points out
    /// along <paramref name="screenDir"/> (a screen angle), the first <paramref name="swallowed"/> share of
    /// it (0 to 1) is inside the snake and not drawn, and it thrashes by <paramref name="thrash"/> (1 is a
    /// hard run's wave, 3 is violent).
    /// </summary>
    void Hold(PointF jaws, float screenDir, float swallowed, float thrash, float dt);

    /// <summary>The last of it has gone in: it is gone for good (the wrapper brings a new one later).</summary>
    void Finish();
}
