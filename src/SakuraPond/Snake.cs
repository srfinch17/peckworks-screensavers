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
/// FIVE SKINS. Every visit is also a different KIND of snake, one of five that
/// really swim in Japanese ponds, seen from above (the back and the top of the
/// head; the belly is never seen). The skin is worked out from the two dials
/// rolled at the door, never from a fresh roll (one extra roll would change
/// every later roll in the scene, and the owner compares builds at one seed):
///   0 shimahebi    the four-lined rat snake: olive with four dark stripes;
///   1 aodaisho     the Japanese rat snake: plain olive green, faint darker
///                  blotches in a loose chain down the spine, a pale grey-green head;
///   2 yamakagashi  the tiger keelback: olive, a CHECKER of black and orange-red
///                  blotches along the front half, a yellow collar behind a dark head;
///   3 hibakari     a plain warm brown with a thin cream line along each side of
///                  the neck and a darker head;
///   4 karasuhebi   the black shimahebi: near black, the stripes only just blacker,
///                  a blue sheen down the ridge of the back.
/// The marks are looked up from (s, v), how far along and how far across, so
/// they ride along as the body bends, like the koi's patches.
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
/// </summary>
internal sealed class Snake : Body
{
    private const int Points = 44;
    private const float StrikeFor = 0.6f;                 // seconds a strike lasts

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often, _len, _wide;
    private readonly PointF[] _trail = new PointF[400];
    private int _trailHead, _trailCount;

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
    private enum Doing { Cross, Explore, Hunt, Bask, Stalk, Strike, Swallow, Miss }
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

    // For the instruments only (nothing in the drawing or the mind reads these): the last thing that
    // happened in the hunt, as a fixed word, and a counter that goes up each time.
    public string HuntNote = "";
    public int HuntSeq, Meetings, Stalks, Abandoned, StrikeCount, Caught, Missed;
    private void Note(string what) { HuntNote = what; HuntSeq++; }

    // Where its head is (screen pixels, before the weave is added), which
    // way it is going, how fast, and how far it has come (the weave's clock).
    private float _x, _y, _heading, _speed, _travelled;
    private PointF _goal;                                  // the point beyond the far edge it is making for
    private float _wakeTimer, _next, _alive;
    private float _flee, _fleeAway;                        // startle: seconds left, and the way away
    private readonly Cooldown _calm = new();               // no startle again until this has run out
    private bool _live, _blocked;                          // _blocked: last frame's step was refused
    private float _blockedT;                               // how long in a row the step has been refused

    public Snake(Pond pond, Ripples ripples, Minnows? minnows, float often, Random rng) : base(Points)
    {
        _pond = pond;
        _minnows = minnows;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _len = _u * 0.3f;
        _wide = _u * 0.0068f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _next = 25 + 20 * (float)rng.NextDouble();       // the first visit comes within a minute
        _cruise = _u * 0.06f; _fleeFor = 2.6f;

        var centres = new List<PointF>();
        var radii = new List<float>();
        foreach (LilyPad p in pond.Pads) { centres.Add(p.Center); radii.Add(p.Radius); }
        foreach (BankRock r in pond.Rocks) { centres.Add(r.Centre); radii.Add(r.Radius); }
        centres.Add(pond.CattailSpot); radii.Add(_u * 0.06f);
        foreach ((PointF root, _) in pond.IrisSpots) { centres.Add(root); radii.Add(_u * 0.045f); }
        _obsCentre = centres.ToArray();
        _obsRadius = radii.ToArray();
        _log = pond.Log;
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
    public bool InWater => _live && !(_doing == Doing.Bask && _phase is 1 or 2);

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
        _exploreLeft = 1 + _rng.Next(3); _huntsLeft = 1 + _rng.Next(2); _basked = false; _legs = 0;
        // The skin comes from the two dials already rolled (a fresh roll would move every later roll in the scene),
        // and so does the hunt's own dice, so a meeting never touches the scene's Random.
        int d1 = (int)(_bold * 1000), d2 = (int)(_nerve * 1000);
        _skin = (d1 + 7 * d2) % 5;
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
        PointF? school = _huntsLeft > 0 && _minnows != null ? _minnows.NearestSchool(_x, _y) : null;
        float wExplore = _exploreLeft > 0 ? 0.7f + 0.5f * (1 - _nerve) : 0;
        float wHunt = school != null ? 0.15f + 1.1f * _bold : 0;
        float wBask = !_basked && LogReady() ? 0.15f + 0.9f * _bold : 0;
        float wCross = 0.3f + 1.2f * _nerve + 0.4f * _legs;
        // A bold snake with a lizard or a mouse out on the bank has a face for going to look (Prowl): not a
        // promise of a hunt, only a walk to where the meeting could happen.
        PointF? meal = !_fed && _strikes < 2 && _prowls < 3 ? NearestPrey(_u * 2f) : null;
        float wProwl = meal != null ? 1.2f + 2f * _bold : 0;
        if (_fed)
        {
            // A snake that has eaten would sooner lie in the sun, or swim about a while with its meal on show (an Explore goal is
            // open water in view, walled in by the picture's edge); it crosses out later. A crossing straight from the land corner
            // hugged the bottom edge BELOW the picture for twenty seconds on three swallows of four, the bulge out of sight.
            if (_exploreLeft <= 0) _exploreLeft = 1;                           // one more look round, meal on show, even if its list was spent
            wHunt = 0; wExplore = 3f * (0.7f + 0.5f * (1 - _nerve)); wBask *= 3; wCross *= 0.3f;
        }
        if (_alive > 200) wExplore = wHunt = wBask = wProwl = 0;    // a long visit ends now
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
        _mind.Begin(Doing.Cross); _phase = 0; Note("cross");
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
            _mind.Begin(Doing.Explore, 45); _phase = 0; _exploreLeft--; Note("explore");
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

    /// <summary>Keeps the current doing going, moves it on a step, or ends it.</summary>
    private void Think(float dt)
    {
        _mind.Tick(dt);
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
                // The school drifts about, so keep the aim on it (but ignore a jump to a different school).
                if (_minnows!.NearestSchool(_x, _y) is PointF now && Dist(now, _prey) < _u * 0.25f) _prey = now;
                _goal = _prey;
                if (_phase == 0)
                {
                    if (Dist(head, _prey) < _u * 0.12f)
                    {
                        _phase = 1; _strikeT = StrikeFor; _huntsLeft--;
                        _ripples.Add(Spine[0].X, Spine[0].Y, 0.9f);        // the lunge pushes the water
                    }
                    else if (_mind.Over) { _huntsLeft--; Decide(); }
                }
                else
                {
                    _strikeT -= dt;
                    if (_strikeT <= 0) { _ripples.Add(Spine[0].X, Spine[0].Y, 0.7f); Decide(); }
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
        if (_fed || _strikes >= 2 || (_lizards == null && _mice == null)) return;
        if (!(_doing is Doing.Cross or Doing.Explore or Doing.Hunt || (_doing == Doing.Bask && _phase == 0))) return;
        PointF head = Spine[0];
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
                if (MathF.Abs(Wrap(MathF.Atan2(nose.Y - head.Y, nose.X - head.X) - _heading)) > 1.309f) continue;   // within 75 degrees of its heading
                if (!NearLand(head)) continue;
                bool prowling = _doing == Doing.Explore && _phase == 1;
                if (WasRolled(p) && !prowling) continue;
                if (_rolledCount < _rolled.Length && !WasRolled(p)) _rolled[_rolledCount++] = p;
                Meetings++;
                if (!prowling && _huntRng.NextDouble() >= 0.5 + 0.4 * _bold) { Note("meet:no"); continue; }
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
        if (_fed || _strikes >= 2 || _prowls >= 3 || (_lizards == null && _mice == null)) return;
        if (!(_doing == Doing.Cross || (_doing == Doing.Explore && _phase == 0))) return;
        PointF head = Spine[0];
        for (int pass = 0; pass < 2; pass++)
        {
            int n = pass == 0 ? _lizards?.Count ?? 0 : _mice?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                IPrey p = pass == 0 ? _lizards![i] : _mice![i];
                if (!p.Huntable || Dist(head, p.PreyNose) > _u * 0.9f) continue;
                bool seen = false;
                for (int k = 0; k < _lookedCount; k++) if (ReferenceEquals(_looked[k], p)) { seen = true; break; }
                if (seen) continue;
                if (_lookedCount < _looked.Length) _looked[_lookedCount++] = p;
                if (_huntRng.NextDouble() < 0.5 + 0.5 * _bold) { BeginProwl(p.PreyNose); return; }
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
            if (p == null || !p.Huntable || Dist(Spine[0], p.PreyNose) > _u * 0.13f)
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
        // An exploring snake stays on the picture: beyond the edge counts as a wall while its head is on the picture (a fed
        // snake crawled along below the bottom edge for twenty seconds with its bulge out of sight). A crossing is meant to
        // leave, and a prowl (phase 1) is not walled either: the prey sits on the land corner by the picture's edge, and with
        // the wall up no prowl on two natural seeds got within 0.3u of it.
        // On a crossing the same, for every edge but the one the goal lies beyond: a crossing begun at the land corner went round
        // the bank rocks, under the bottom edge, and ran east BELOW the picture for a thousand pixels, the goal's pull too weak
        // to bend it back. The exit edge stays open, so the crossing can leave.
        if ((_doing == Doing.Explore && _phase == 0 || _doing == Doing.Cross) && _x >= 0 && _y >= 0 && _x < _pond.Width && _y < _pond.Height)
        {
            float wall = _u;
            if (!(_doing == Doing.Cross && _goal.X < 0)) wall = MathF.Min(wall, px);
            if (!(_doing == Doing.Cross && _goal.X >= _pond.Width)) wall = MathF.Min(wall, _pond.Width - px);
            if (!(_doing == Doing.Cross && _goal.Y < 0)) wall = MathF.Min(wall, py);
            if (!(_doing == Doing.Cross && _goal.Y >= _pond.Height)) wall = MathF.Min(wall, _pond.Height - py);
            best = MathF.Min(best, wall - _u * 0.03f);
        }
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
        if (!_live || _doing != Doing.Bask) _pond.LogPerch.Release(this);   // the log is only the snake's while it is basking (or on its way to)
        if (!_live)
        {
            // Drawn out by prey: while a lizard or a mouse is out on the bank the wait for the next visit runs twice as
            // fast (a snake comes to where there is something to eat). Without it, most prowls arrived after the prey had gone.
            _next -= dt * (NearestPrey() != null ? 2f : 1f);
            if (_next <= 0 && _often > 0) { Begin(); _next = (150 + 150 * (float)_rng.NextDouble()) / _often; }
            return;
        }
        _alive += dt;

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
                    else if (_doing == Doing.Bask && _phase == 0) Decide();
                    else if (_doing == Doing.Bask && _phase <= 2) StartSlip();     // already on the log: off it by the log's own line (Decide here left it inside the log, stuck, for 88 s)
                    break;
                }
        }
        float fear = _flee / _fleeFor;                     // 1 the instant it is touched, fading to 0 as it calms

        Scan();
        Notice();
        Think(dt);
        UpdateBulge(dt);
        if (_doing == Doing.Bask && _phase == 2) { UpdateLie(dt); return; }    // lying still: the spine is laid by the pose, nothing steers

        // ---- Speed: a steady swim, slower when stalking or climbing, a burst when startled or striking ----
        bool striking = _doing == Doing.Hunt && _phase == 1;
        // The creep of a stalk is about a third of a cruise (0.4: a third let the prey wander off first); a prowl, going to look, is
        // a brisk 1.7 (prey is out for only half a minute); the chase after a miss a third faster than a cruise; a fed snake is slow.
        float pace = _doing == Doing.Hunt && _phase == 0 ? StalkPace() : _doing == Doing.Bask && _phase == 1 ? 0.6f
                   : _doing == Doing.Stalk ? 0.4f : _doing == Doing.Miss ? 1.3f : _doing == Doing.Explore && _phase == 1 ? 1.7f : 1f;
        if (_fed) pace *= 0.6f;
        float wantSpeed = striking ? _u * 0.18f : _cruise * pace * (1 + 2.2f * fear);
        // The chase after a miss holds off at 0.08u from the prey's nose (the prey is faster, and its tail is between its nose and
        // the jaws: without this the jaws ran onto a fleeing lizard's tail with nothing happening, a bite that was not one).
        if (_doing == Doing.Miss && _phase == 1 && _target != null && Dist(Spine[0], _target.PreyNose) < _u * 0.08f) wantSpeed = 0;
        _speed += (wantSpeed - _speed) * (1 - MathF.Exp(-dt * (striking ? 14f : fear > 0 ? 6f : 1.2f)));   // a lunge gets up to speed almost at once
        // Holding still for a strike, a mouthful or the look-about after a miss: not a crawl, a stop.
        if (_doing is Doing.Strike or Doing.Swallow || (_doing == Doing.Miss && _phase == 0)) _speed = 0;

        // ---- Steering ----
        // A gentle pull toward the goal the mind chose (firmer when climbing onto
        // the log or striking), a slow wander, and when startled a hard swing
        // away from whatever touched it.
        float pull = striking ? 2.5f : _doing == Doing.Bask && _phase == 1 ? 1.5f : _doing is Doing.Stalk or Doing.Miss ? 1.8f : 0.5f;
        float turn = Wrap(MathF.Atan2(_goal.Y - _y, _goal.X - _x) - _heading) * pull;
        if (!striking && !(_doing == Doing.Bask && _phase == 1) && _doing is not (Doing.Stalk or Doing.Strike or Doing.Swallow or Doing.Miss)) turn += 0.2f * MathF.Sin(_alive * 0.35f);
        float most = (striking ? 2.5f : 1.0f) + 1.5f * fear;

        // Something in the way? Feel for the smallest turn that clears it.
        // "Enough room" is the head's own width a few times over; past that,
        // more room counts for nothing, so it skirts a pad rather than
        // heading for the far side of the pond.
        // Stalking or chasing a miss it goes closer past rocks to get at what sits on them (0.02u, not 0.035u; less and the body swung into the stone).
        float look = _len * 0.8f + _speed * 0.8f, margin = _u * (_doing is Doing.Stalk or Doing.Miss ? 0.02f : 0.035f);    // wider than the weave's swing, so the body clears too
        float clearAhead = _blocked ? 0 : ClearAlong(_heading, look);
        // A fright swings it away from what touched it, but never into something
        // else: the nearer an obstacle, the less the swing counts (an early
        // build fled straight into a lily pad and sat inside it).
        if (fear > 0) turn += Wrap(_fleeAway - _heading) * 2.5f * fear * Math.Clamp(clearAhead / margin, 0, 1);
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
        _heading = Wrap(_heading + Math.Clamp(turn, -most, most) * dt);

        // ---- Forward, unless the next step would be inside something ----
        float step = _speed * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        float clearNext = Clear(nx, ny);
        float clearHere = Clear(_x, _y);
        _blocked = !(clearNext > 0 || clearNext >= clearHere);   // never stuck: a step toward more room is always allowed
        if (clearHere < 0 && !(_doing is Doing.Strike or Doing.Swallow or Doing.Stalk or Doing.Miss) && !(_doing == Doing.Bask && _phase >= 1))
        {
            // INSIDE something (a bank rock's circle, entered on a stalk or a strike through its margin): the head walks straight
            // out, uphill on its own clearance, the goal's pull set aside until it is clear. With the pull still turning the head
            // back toward the rock every frame it jittered two pixels in a pocket between two stones for nineteen minutes.
            float gx = Clear(_x + 2, _y) - Clear(_x - 2, _y), gy = Clear(_x, _y + 2) - Clear(_x, _y - 2), gl = MathF.Sqrt(gx * gx + gy * gy);
            if (gl > 1e-4f)
            {
                _heading = MathF.Atan2(gy, gx);
                nx = _x + gx / gl * step; ny = _y + gy / gl * step;
                _blocked = false;
            }
        }
        if (!_blocked)
        {
            _x = nx; _y = ny;
            _travelled += step;
        }
        // A jam breaker (found by a 20 minute run: on one seed a snake sat 10 minutes at the screen's edge inside a lily pad's
        // margin, trying every heading and refused every step). Blocked for 5 seconds: if it is out of sight it has left; if it is
        // on screen it gives up what it was doing and sets off across the pond again.
        _blockedT = _blocked ? _blockedT + dt : 0;
        if (_blockedT > 5f && !(_doing is Doing.Strike or Doing.Swallow))
        {
            _blockedT = 0;
            if (_x < 0 || _y < 0 || _x > _pond.Width || _y > _pond.Height) { _target = null; _live = false; return; }
            _target = null; BeginCross();
        }
        // Gone: leaving, and its head is far enough past an edge that its tail has left too.
        float m = _len * 1.25f;
        if (_doing == Doing.Cross && _alive > 3 && (_x < -m || _y < -m || _x > _pond.Width + m || _y > _pond.Height + m)) { _live = false; return; }

        // The weave: side to side, a full swing every 65% of a body length
        // travelled, so about one and a half waves lie along its body at once.
        // Distance drives it, so a burst of speed quickens the weave by itself.
        // Its width eases to the doing's: narrow while stalking or climbing (a
        // creeping snake does not thrash), nearly straight in a strike.
        float weaveWant = striking ? 0.3f : _doing == Doing.Hunt ? 0.5f : _doing == Doing.Bask && _phase == 1 ? 0.35f : _doing == Doing.Stalk ? 0.25f : _doing == Doing.Strike ? 0.1f : 1f;
        weaveWant *= 0.4f + 0.6f * Math.Clamp(clearAhead / (margin * 2), 0, 1);       // and narrower still beside an obstacle, so the body's swing never brushes it
        _weaveAmp += (weaveWant - _weaveAmp) * (1 - MathF.Exp(-dt * 1.5f));
        // A snake that has stopped does not weave: the gate closes (quickly) when it is not moving, so the head
        // does not hang off to one side of a held pose, and opens when it moves.
        _weaveGate += ((_speed > _cruise * 0.15f ? 1f : 0f) - _weaveGate) * (1 - MathF.Exp(-dt * 8f));
        float weave = _len * 0.085f * _weaveAmp * _weaveGate * MathF.Sin(_travelled / (_len * 0.65f) * MathF.Tau);
        float hx = MathF.Cos(_heading), hy = MathF.Sin(_heading);
        var head = new PointF(_x - hy * weave, _y + hx * weave * Ripples.Squash);
        if (Dist(head, _trail[_trailHead]) > _len / 150) Remember(head);

        // The wake: little rings pushed out from its head while it is in the water.
        _wakeTimer -= dt;
        int ix = (int)head.X, iy = (int)head.Y;
        if (_wakeTimer <= 0 && ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix])
        {
            _ripples.Add(head.X, head.Y, 0.4f + 0.2f * fear);
            _wakeTimer = 0.4f - 0.2f * fear;
        }
        LaySpine(head);
        if (_poseK != 0 || _pullK != 0) ApplyPose();
        if (_doing == Doing.Swallow && _target != null)
        {
            // The prey is held by the head: its nose at the jaws THIS frame, its body out in front (the thrash
            // is violent for the first second and dies to a twitch by the time the tail goes in).
            float thrash = _phase == 0 ? 3f : 2.2f - 1.6f * _swallowed;
            _target.Hold(Spine[0], _preyDir, _swallowed, thrash, dt);
        }
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
        float done = 0, seg = Dist(a, b);
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (done + seg < want && left > 0)
            {
                done += seg; a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length; left--;
                b = _trail[slot]; seg = Dist(a, b);
            }
            float t = seg > 1e-4f ? Math.Clamp((want - done) / seg, 0, 1) : 0;
            Spine[i] = new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            Shape.Radius[i] = _wide * Profile(Along[i]);
            // The same point for touching (Core/Minds Chain), on the GROUND
            // plane: the spine is screen pixels, and the pond is squashed down
            // the screen by Ripples.Squash, so the ground y is the screen y
            // stretched back out from the pond's middle (the koi's ToGround).
            // Its depth is 0: it swims ON the water, so a koi below it reads
            // the gap up to it as the gap to any body above, stretched.
            Shape.Points[i] = new System.Numerics.Vector3(Spine[i].X, cy + (Spine[i].Y - cy) / Ripples.Squash, 0);
        }
        Shape.Seal();
        Widths();
    }

    /// <summary>
    /// How wide it is DRAWN: the plain width (the chain's radius) with the head opened for a strike and the
    /// meal's bulge moving down it. Only the drawing sees these; the fish still feel the plain snake.
    /// </summary>
    private void Widths()
    {
        for (int i = 0; i < Points; i++)
        {
            float w = Shape.Radius[i];
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
        if (s < 0.035f) return 0.55f + 0.65f * MathF.Sin(s / 0.035f * MathF.PI * 0.5f);   // the snout rounding out to the head
        if (s < 0.06f) return 1.2f - 0.4f * (s - 0.035f) / 0.025f;                        // the head narrowing to the neck
        if (s < 0.12f) return 0.8f + 0.2f * (s - 0.06f) / 0.06f;                          // the neck filling out
        if (s < 0.6f) return 1f;
        return MathF.Max(0.05f, 1 - (s - 0.6f) / 0.4f);                                    // the long taper to the tail
    }

    /// <summary>Which of the five skins this snake wears (0 shimahebi, 1 aodaisho, 2 yamakagashi, 3 hibakari, 4 karasuhebi), for the instruments.</summary>
    public int Skin => _skin;

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        alpha = 1;
        float shade = 0.72f + 0.28f * MathF.Sqrt(MathF.Max(0, 1 - v * v));             // a rounded back
        int lift = 0;                                                                    // a cool highlight added after the shading (the black snake's sheen)
        switch (_skin)
        {
            default:
                // Shimahebi: olive back, darker on the head, with four dark stripes running down the body.
                (r, g, b) = s < 0.06f ? (112, 104, 62) : (150, 140, 88);
                if (s > 0.07f && (MathF.Abs(av - 0.32f) < 0.08f || MathF.Abs(av - 0.76f) < 0.08f)) { r = 66; g = 58; b = 36; }
                break;
            case 1:
                // Aodaisho: plain olive green, a pale grey-green head, and faint darker blotches in a loose chain
                // down the spine (a blotch every 0.09 of the length, a little to one side and then the other).
                if (s < 0.07f) (r, g, b) = (152, 160, 124);
                else
                {
                    (r, g, b) = (124, 134, 72);
                    float ph = s * 11f + _blot;
                    int cell = (int)ph;
                    float f = ph - cell;
                    float centre = (cell & 1) == 0 ? 0.12f : -0.12f;
                    if (f > 0.12f && f < 0.62f && MathF.Abs(v - centre) < 0.42f) { r = 98; g = 108; b = 56; }
                }
                break;
            case 2:
                // Yamakagashi: olive, a dark head, a yellow collar just behind it, and along the front half a CHECKER of
                // black and orange-red blotches (two lanes, one each side of the back; the colours swap every cell).
                if (s < 0.07f) (r, g, b) = (44, 48, 32);
                else if (s < 0.115f) (r, g, b) = (222, 192, 52);
                else
                {
                    (r, g, b) = (112, 120, 64);
                    float fade = Math.Clamp((0.66f - s) / 0.18f, 0f, 1f);
                    if (fade > 0 && av > 0.18f)
                    {
                        float ph = s * 24f;
                        int cell = (int)ph;
                        float f = ph - cell;
                        if (f > 0.12f && f < 0.88f)
                        {
                            bool black = (((cell + (v > 0 ? 1 : 0)) & 1) == 0);
                            (int tr, int tg, int tb) = black ? (24, 24, 22) : (178, 96, 60);   // rust, not toy coral-snake orange
                            r += (int)((tr - r) * fade); g += (int)((tg - g) * fade); b += (int)((tb - b) * fade);
                        }
                    }
                }
                break;
            case 3:
                // Hibakari: a plain warm brown, a darker head, and a thin cream line along the side of the neck for a short way.
                (r, g, b) = s < 0.06f ? (108, 76, 44) : (146, 104, 62);
                if (s > 0.012f && s < 0.26f && MathF.Abs(av - 0.7f) < 0.075f)
                {
                    float k = Math.Clamp((0.26f - s) / 0.1f, 0f, 1f);
                    r += (int)((230 - r) * k); g += (int)((212 - g) * k); b += (int)((166 - b) * k);
                }
                break;
            case 4:
                // Karasuhebi: near black, the four stripes only just blacker, and a blue sheen down the ridge of the back.
                (r, g, b) = (34, 35, 36);
                if (s > 0.07f && (MathF.Abs(av - 0.32f) < 0.08f || MathF.Abs(av - 0.76f) < 0.08f)) { r = 20; g = 20; b = 21; }
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
        if (_live) petals.PushAside(Drawn, DrawnHalfWidth);
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_live) return;
        Paint(fb, _pond.Open, _pond.Sun, 0, MathF.Max(1f, _u * 0.0012f));
        // The eyes: tiny dark dots either side of the head.
        PointF e0 = Spine[1], e1 = Spine[2];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.7f, _wide * 0.3f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, _pond.Open, new PointF(e0.X - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ex / el * HalfWidth[1] * 0.7f * side), 0, er, er, 20, 20, 16, 0.9f, 0, _pond.Sun);
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
