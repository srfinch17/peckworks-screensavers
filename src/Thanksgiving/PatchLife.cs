using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The small life of the pumpkin patch (his line: "little mice and squirrels
/// and birds and things running around throughout the patch"): field mice
/// that scurry between the pumpkins, sniff, nibble at them and hide behind
/// them; juncos and sparrows that hop and peck between them, fly up to sit on
/// a pumpkin and look about; and a squirrel or two that bound through, sit
/// up, scratch at the earth and dart behind a pumpkin.
///
/// FEYNMAN VERSION: each critter has a MIND from the shared kit the pond's
/// animals use (Core/Minds): two personality dials rolled once (a timid one
/// hides more, a busy one never sits), ONE doing at a time with a timer, and
/// weighted dice for what to do next when the timer runs out. The mind only
/// chooses a goal; the steps are the kit's ground rules (Ground.Step): the
/// patch is a surface with holes in it (every pumpkin's foot, the
/// shock), and a step into a hole slides round it. A mouse that a squirrel
/// bounds past bolts (a startle with a cooldown, so it cannot bolt thirty
/// times a second).
///
/// Every critter is stamped against the near ground's depth picture with the
/// nearness of its own feet, so a mouse behind a pumpkin is hidden by it and
/// one in front runs over it: that is the whole of "throughout the patch". A
/// bird on a pumpkin's top is stamped with that pumpkin's nearness plus one.
/// They are drawn far to near.
/// </summary>
internal sealed class PatchLife
{
    private enum Kind { Mouse, Bird, Squirrel }
    private enum Doing { Go, Still, Nibble, Hide, Peck, Fly, Perch, Dig, Bolt }

    private sealed class Critter
    {
        public Kind Kind;
        public int Id;                              // its number at birth (the list is sorted by depth to draw, so an index is not a name)
        public int Look;                            // which bird
        public float X, Y;                          // the feet, pixels
        public float Heading;                       // radians, screen (y down)
        public int Dir = 1;
        public float Timid, Busy;                   // the dials
        public Mind<Doing> Mind = new(Doing.Still);
        public Attention Senses = null!;            // what it makes of the noticeboard (Senses.cs)
        public Pace Pace = null!;                   // its speed, eased (Mover.cs): a mouse gathers speed over a few steps, it does not switch on
        public float NextPost;                      // a squirrel on the move tells the board every so often
        public PointF Goal;
        public float Travelled;                     // for the page
        public float Clock;
        public int Perched = -1;                    // the pumpkin it sits on
        public float FlyFrom, FlyTo, FlyY0, FlyY1, FlyFor;   // a flight's arc
        public int Refused;                         // frames in a row the ground refused
        public bool WasDown;                        // a squirrel: on all fours last frame (running, hiding, digging), or sitting up
        public bool Turning;                        // a squirrel coming round to face the other way: stop, sit, turn through the front view, sink, and on (never a mirror flip)
        public float TurnT;                         // seconds into that
        public float Blend;                         // seconds left of its blend through the crouch pages between the two
        public bool Sinking;                        // the blend's direction: sinking from the sit into the run, or rising out of it
    }

    private const float BlendFor = 0.15f;           // a squirrel drops into its run, or sits up out of it, over nine frames (it used to pop in one)
    private const int RisePages = 6, TurnPages = 3;
    private const float TurnRise = 0.12f, TurnFor = 0.30f;   // a turn: rise to the sit (0.12 s), the three narrowing pages, the flip, the three widening (0.30), sink (0.12)

    private struct Sheet { public Sprite[] Pages; public int W, H, Ox, By; }

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;          // PECKWORKS_MINDLOG: every critter's place and doing each frame (the birds' flights are judged from it)
    private double _time;
    private readonly List<Critter> _all = new();
    private readonly float _k, _m, _b;
    private readonly Ground.Solid _ground;
    private Sheet _mouseRun, _mouseStill, _sqRun, _sqLook, _sqDig, _sqRise, _sqTurn;
    private Sheet[] _sqSit = new Sheet[4];
    private Sheet[] _bird = new Sheet[2], _birdLook = new Sheet[2], _birdPeck = new Sheet[2], _birdHop = new Sheet[2];
    private Sheet[] _birdFlyKind = new Sheet[2];
    private volatile bool _ready;

    public PatchLife(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _log = MindLog.Open();
        _rng = new Random(sceneRng.Next());         // its own dice
        _k = Math.Max(9f, s.U * 0.030f);   // the near ground's scale: a pumpkin of 0.4 m is about 0.05 U, so a squirrel of 0.25 m is 0.030 U (at 0.055 it was "too big"), a mouse 0.55 of that, a sparrow 0.020
        _m = _k * 0.55f;
        _b = Math.Max(7f, s.U * 0.020f);
        _ground = CanStand;                         // made once: a new delegate every frame is garbage
        int mice = (count + 1) / 2, birds = count / 3, squirrels = Math.Max(0, count - mice - birds);
        float[] timid = Traits.Deal(_rng, Math.Max(1, count)), busy = Traits.Deal(_rng, Math.Max(1, count)), curious = Traits.Deal(_rng, Math.Max(1, count));
        int n = 0;
        for (int i = 0; i < mice; i++) { Add(Kind.Mouse, timid[n], busy[n], curious[n]); n++; }
        for (int i = 0; i < birds; i++) { Add(Kind.Bird, timid[n], busy[n], curious[n]); n++; }
        for (int i = 0; i < squirrels; i++) { Add(Kind.Squirrel, timid[n], busy[n], curious[n]); n++; }
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    private void Add(Kind kind, float timid, float busy, float curious)
    {
        var c = new Critter { Kind = kind, Id = _all.Count, Timid = timid, Busy = busy, Look = _rng.Next(2), Dir = _rng.Next(2) == 0 ? 1 : -1 };
        PointF at = RandomSpot(0, _s.Width, 40) ?? new PointF(_s.Width * 0.5f, _s.WalkY(_s.Width * 0.5f));
        c.X = at.X; c.Y = at.Y;
        // Small things in the grass feel less of the world (Keen under 1); a mouse is timid by nature, a squirrel bold.
        c.Senses = new Attention(kind == Kind.Mouse ? 0.5f + 0.5f * timid : kind == Kind.Squirrel ? 0.5f * timid : timid, curious) { Keen = kind == Kind.Bird ? 1f : 0.8f };
        c.Pace = new Pace(0.9f * _s.U);             // gathers speed over about a quarter of a second
        c.Mind.Begin(Doing.Still, 0.5f + 3f * (float)_rng.NextDouble());
        _all.Add(c);
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
    /// for a third of a second, sat, and tried again, for 46 of 120 seconds on one seed.)
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

        float k = _k;
        int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
        _sqRun = Make(32, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Bound(i / 32f, 2.2f), k, w, h, ox, by));   // 32: at 16 a bound 2.2 lengths long stepped the tail a seventh of a wave a page
        for (int t = 0; t < 4; t++) { int tl = t; _sqSit[t] = Make(1, w, h, ox, by, _ => SquirrelArt.Page(SquirrelArt.Sit(1f, tl, false, false), k, w, h, ox, by)); }
        _sqLook = Make(1, w, h, ox, by, _ => SquirrelArt.Page(SquirrelArt.Sit(1f, 0, true, false), k, w, h, ox, by));
        _sqDig = Make(8, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Dig(i / 8f), k, w, h, ox, by));
        _sqRise = Make(RisePages, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Sit(0.2f + 0.8f * i / (RisePages - 1), 0, false, false), k, w, h, ox, by));   // the crouch (page 0) up to the sit (the last)
        _sqTurn = Make(TurnPages, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Turn(0.15f + 0.175f * i), k, w, h, ox, by));   // sitting, coming round: 0.15, 0.325, 0.5 (the front view); played back again mirrored

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
            _log?.WriteLine($"{_time,8:F3} {c.Kind.ToString().ToLowerInvariant()}{c.Id} frame x {c.X:F1} y {c.Y:F1} v {c.Pace.Speed:F2} {c.Mind.Doing}");
            // A squirrel on the move is a thing the mice and birds notice: it tells the board every third of a second.
            if (c.Kind == Kind.Squirrel && c.Mind.Doing is Doing.Go or Doing.Bolt && c.Clock >= c.NextPost)
            {
                _board.Post(Sense.Movement, new PointF(c.X, c.Y), 0.14f * _s.U, 0.9f, c);
                c.NextPost = c.Clock + 0.33f;
            }
            // The senses: a mouse bolts from a squirrel or a scuffle, freezes at a noise; a bird flies up; a squirrel sits up and looks.
            if (c.Mind.Doing is not (Doing.Bolt or Doing.Fly))
            {
                Reaction r = c.Senses.Check(_board, new PointF(c.X, c.Y), c, out Notice cause);
                if (r == Reaction.Flee) Startle(c, cause.At);
                else if (r == Reaction.Freeze) { c.Pace.Halt(); c.Mind.Begin(c.Kind == Kind.Squirrel && c.Perched < 0 ? Doing.Still : Doing.Still, 1f + 1.5f * c.Timid); c.Clock = 0; }
                else if (r == Reaction.Look && c.Mind.Doing is Doing.Still or Doing.Peck or Doing.Perch) { c.Clock = 1.1f; c.Mind.Left = Math.Max(c.Mind.Left, 0.8f); if (c.Kind != Kind.Bird) c.Dir = cause.At.X >= c.X ? 1 : -1; }
            }
            if (c.Turning)
            {
                // The turn on the spot: the body rises to the sit, narrows through the front view, flips, widens, and sinks; the
                // doing's own clock waits so nothing else moves meanwhile.
                float was = c.TurnT; c.TurnT += dt; c.Mind.Left += dt;
                float mid = TurnRise + TurnFor / 2;
                if (was < mid && c.TurnT >= mid) c.Dir = -c.Dir;
                if (c.TurnT >= TurnRise + TurnFor + TurnRise) { c.Turning = false; c.Blend = 0; }
                continue;
            }
            Act(c, dt);
            if (c.Mind.Over) Decide(c);
            // A squirrel never pops from its sit into a bound or back (one frame from upright to stretched flat was the
            // patch's one real jump): the moment its doing changes class (down on all fours, or sitting up) it plays a
            // short blend through the crouch pages, sinking into the run or rising out of it. The pace is still easing
            // in those frames, so the sink plays nearly on the spot.
            bool down = c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide or Doing.Dig;
            if (c.Kind == Kind.Squirrel && down != c.WasDown) { c.Blend = BlendFor; c.Sinking = down; }
            c.WasDown = down;
            if (c.Blend > 0) c.Blend -= dt;
        }
    }

    /// <summary>
    /// Faces a critter at its new goal before it sets off, when it is standing still: a stopped animal turns to where
    /// it is going and then goes. (Left to the steering, it set off on its OLD heading and swung round in a curve at
    /// 7 radians a second, a squirrel bounding in an arc up the screen from a dig and then stopping to turn.) A
    /// squirrel whose goal is behind it plays its turn on the spot first (Update holds the doing's clock meanwhile); a
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
        if (c.Kind == Kind.Squirrel) { c.Turning = true; c.TurnT = 0; }
        else c.Dir = dir;
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

    /// <summary>The dice: what next, by kind, dials and what is at hand.</summary>
    /// <summary>
    /// While a story plays on the near ground the patch's own life keeps out of its way (a loose mouse sat at the
    /// squirrel's feet while "the" mouse hid in the jack-o'-lantern): a mouse or squirrel that has to choose goes
    /// behind a pumpkin and stays there, a bird stands still or stays on its perch. Set by the scene each frame.
    /// </summary>
    public bool Lull;

    private void Decide(Critter c)
    {
        float u = _s.U;
        c.Clock = 0;
        if (Lull)
        {
            if (c.Kind == Kind.Bird) c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : _rng.Next(3) == 0 ? Doing.Peck : Doing.Still, 2f + 2f * (float)_rng.NextDouble());
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
                // Faces: bound somewhere, sit and look, scratch at the earth, dart behind a pumpkin.
                // (A hide weighed up to 0.5 and lasted six seconds: on one seed the squirrel was out of sight 67 of 120 s.)
                int face = Dice.Pick(_rng, [0.5f + 0.5f * c.Busy, 0.4f, 0.25f, 0.05f + 0.2f * c.Timid]);
                // A bound goes ALONG the patch: the goal stays within a little of its own row (a goal straight up or down the
                // screen sent a side-view animal sliding on the spot, the "90 degree turns"), and at least a few lengths off.
                if (face == 0) { PointF? g = RandomSpot(c.X - 0.45f * u, c.X + 0.45f * u, 30, c.Y, 0.05f * u, 4f * _k, new PointF(c.X, c.Y)); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Go, 6f); c.Travelled = 0; }
                else if (face == 1) c.Mind.Begin(Doing.Still, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 2) c.Mind.Begin(Doing.Dig, 1f + 1.5f * (float)_rng.NextDouble());
                else { PointF? g = BehindPumpkin(new PointF(c.X, c.Y), 0.4f * u); if (g == null) { c.Mind.Begin(Doing.Still, 1f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 3f); c.Travelled = 0; }
                break;
            }
        }
        if (c.Mind.Doing is Doing.Go or Doing.Nibble or Doing.Hide) Head(c);
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
            {
                float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float arrive = c.Kind == Kind.Bird ? 0.2f * _b : 0.25f * _m;
                if (dist < arrive)
                {
                    // Arrived: a scurry ends in a sniff; a nibble and a hide stay put until the timer.
                    c.Pace.Halt();
                    if (c.Mind.Doing is Doing.Go or Doing.Bolt) { c.Mind.Begin(Doing.Still, c.Kind == Kind.Bird ? 0.5f : 0.4f + 0.8f * (float)_rng.NextDouble()); c.Clock = 0; }
                    else if (c.Mind.Left > 4f) c.Mind.Left = 2f + 3f * (float)_rng.NextDouble();
                    return;
                }
                // The pace eases toward the doing's speed and slows into the goal (Mover.cs); the heading turns at a rate, easing as it settles.
                float speed = c.Kind switch { Kind.Mouse => c.Mind.Doing == Doing.Bolt ? 0.30f : 0.19f, Kind.Bird => 0.12f, _ => c.Mind.Doing == Doing.Bolt ? 0.32f : 0.24f } * u;
                c.Pace.Want = Math.Min(speed, MathF.Max(0.25f * speed, dist * 4f));
                float want = MathF.Atan2(dy, dx);
                c.Heading = Turn.Toward(c.Heading, want, 7f, dt);
                float err = MathF.IEEERemainder(want - c.Heading, MathF.Tau);
                float step = c.Pace.Tick(dt);
                if (step <= 0f) return;
                float x = c.X, y = c.Y, hd = c.Heading;
                var stepped = Ground.Step(_ground, ref x, ref y, ref hd, step, err);
                // A refused step is a jam, not a pause: after a few frames the doing is dropped and the critter sits (its goal
                // was behind something the step test will not cross). Six frames, not twenty: at twenty the run page hung
                // frozen mid-bound for a third of a second each time.
                if (stepped == Ground.Stepped.Refused) { if (++c.Refused > 6) { c.Pace.Halt(); c.Mind.Begin(Doing.Still, 0.5f); c.Refused = 0; _log?.WriteLine($"{_time,8:F3} {c.Kind.ToString().ToLowerInvariant()}{c.Id} refused at {c.X:F0},{c.Y:F0} going to {c.Goal.X:F0},{c.Goal.Y:F0}"); } return; }
                c.Refused = 0;
                c.X = x; c.Y = y; c.Heading = hd;
                c.Travelled += step;
                if (MathF.Abs(MathF.Cos(hd)) > 0.2f)
                {
                    int dir = MathF.Cos(hd) > 0 ? 1 : -1;
                    if (dir != c.Dir && c.Kind == Kind.Squirrel) { c.Turning = true; c.TurnT = 0; c.Pace.Halt(); return; }   // a squirrel stops and comes round (Update); a mouse at 13 px just faces the other way
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
                if (c.Mind.Over && c.Perched >= 0 && _rng.NextDouble() < 0.6) c.Mind.Left = 1f + 3f * (float)_rng.NextDouble();   // often sits a while longer
                break;
        }
    }

    // ================================================================ drawing

    private byte NearnessOf(Critter c) => c.Perched >= 0 && c.Mind.Doing == Doing.Perch
        ? (byte)Math.Min(254, _s.Nearness(_s.Pumpkins[c.Perched].At.Y + 0.8f * _s.Pumpkins[c.Perched].R) + 1)
        : _s.Nearness(c.Y);

    private void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, Critter c, float y)
    {
        bool mirror = c.Dir < 0;
        int left = (int)MathF.Round(c.X) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, null, mirror, _s.NearDepth, NearnessOf(c));
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
                    if (c.Turning)
                    {
                        float t = c.TurnT;
                        if (t < TurnRise) Stamp(fb, _sqRise, _sqRise.Pages[Math.Min(RisePages - 1, (int)(t / TurnRise * RisePages))], c, c.Y);                // up to the sit
                        else if (t < TurnRise + TurnFor)
                        {
                            float q = (t - TurnRise) / TurnFor;                                                                                       // narrowing facing the old way, widening facing the new
                            int pg = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                            Stamp(fb, _sqTurn, _sqTurn.Pages[pg], c, c.Y);
                        }
                        else Stamp(fb, _sqRise, _sqRise.Pages[Math.Max(0, RisePages - 1 - (int)((t - TurnRise - TurnFor) / TurnRise * RisePages))], c, c.Y);   // down to the crouch
                        break;
                    }
                    if (c.Blend > 0)
                    {
                        float f = Math.Clamp(c.Blend / BlendFor, 0f, 1f);                    // 1 at the start of the blend, 0 at its end
                        float upright = c.Sinking ? f : 1f - f;
                        Stamp(fb, _sqRise, _sqRise.Pages[(int)(upright * (RisePages - 0.01f))], c, c.Y);
                    }
                    else if (c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide)
                    {
                        float cyc = c.Travelled / (2.2f * _k);
                        Stamp(fb, _sqRun, _sqRun.Pages[(int)((cyc - MathF.Floor(cyc)) * 32) % 32], c, c.Y);
                    }
                    else if (c.Mind.Doing == Doing.Dig)
                        Stamp(fb, _sqDig, _sqDig.Pages[(int)(c.Clock * 3.2f * 8) % 8], c, c.Y);
                    else
                    {
                        float ft = c.Clock % 1.5f;
                        int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                        bool look = ((int)(c.Clock / 1.3f)) % 3 == 1;
                        Sheet sh = look ? _sqLook : _sqSit[tail];
                        Stamp(fb, sh, sh.Pages[0], c, c.Y);
                    }
                    break;
                }
            }
        }
    }
}
