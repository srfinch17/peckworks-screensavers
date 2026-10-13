using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The koi in the pond: a handful of fish, each drawn along its own bending
/// spine (Body.cs), deepest first so a fish near the surface swims over one
/// lower down.
/// </summary>
internal sealed class KoiSchool
{
    private readonly Koi[] _fish;

    /// <param name="play">The "Koi play" knob: 1 = normal, 0 = the fish only wander and rest, 3 = three times as playful.</param>
    /// <param name="petals">The floating petals, for the fish to nose at and eat.</param>
    public KoiSchool(Pond pond, int count, Ripples ripples, bool[] where, Random rng, float play = 1, Petals? petals = null)
    {
        _fish = new Koi[Math.Max(0, count)];
        for (int i = 0; i < _fish.Length; i++)
            _fish[i] = new Koi(pond, ripples, where, rng, i, play, petals);
    }

    public void Update(float dt)
    {
        foreach (Koi k in _fish) k.Update(dt, _fish);
    }

    /// <summary>Something tapped the water here (screen pixels): curious fish nearby come to look.</summary>
    public void Notice(float x, float y)
    {
        foreach (Koi k in _fish) k.Notice(x, y);
    }

    /// <summary>Each fish's doing this frame, for logs and tests.</summary>
    public IEnumerable<(PointF Head, PointF Tail, string State)> States => _fish.Select(f => (f.Head, f.Tail, f.State));

    /// <summary>Two points along each koi this frame, its head and its middle, screen pixels (the minnows keep clear of them).</summary>
    public PointF[] Heads => _fish.SelectMany(f => new[] { f.Head, f.Middle }).ToArray();

    /// <summary>
    /// Draws the fish deepest first, so a fish nearer the surface covers one
    /// below it. Fish that do not touch cannot paint over each other, so they
    /// are drawn at the same time on different processor cores: going down
    /// the deepest-first list, each fish joins the current group if its box
    /// touches no box already in the group; otherwise the group is drawn and
    /// a new one starts. The picture comes out exactly as if drawn one by one.
    /// </summary>
    public void Draw(FrameBuffer fb)
    {
        var group = new List<Koi>(_fish.Length);
        foreach (Koi k in _fish.OrderByDescending(f => f.Z))
        {
            RectangleF box = k.Box;
            if (group.Any(o => o.Box.IntersectsWith(box))) { DrawGroup(fb, group); group.Clear(); }
            group.Add(k);
        }
        DrawGroup(fb, group);
    }

    private static void DrawGroup(FrameBuffer fb, List<Koi> group)
    {
        if (group.Count == 1) group[0].Draw(fb);
        else if (group.Count > 1) Parallel.ForEach(group, k => k.Draw(fb));
    }
}

/// <summary>
/// One koi, seen from above.
///
/// WHAT A KOI DOES (watch any garden pond): it cruises slowly, a few lazy
/// beats of the tail and then a long glide with the tail almost still, and
/// does it again. It turns in smooth curves, its whole body bending into the
/// turn. Its side fins scull and brake. It drifts up and down in the water,
/// paler and hazier when deep, sharp and bright near the top, and now and
/// then it comes right up and gulps at the surface, leaving a ring. It keeps
/// away from the edges and from other fish without fuss.
///
/// HOW THE BODY BENDS: the head swims a path, and the body follows the path
/// the head has ALREADY swum, the way the carriages of a train follow the
/// track the engine took. We remember where the head has been (a trail of
/// points), and lay the spine along that trail. So a turn bends the body
/// exactly through the curve. On top of that a swimming wave runs down the
/// body: the head barely moves, the tail swings widest, the way a fish's
/// body pushes against the water. Nothing is ever flipped or slid.
///
/// THE SLANT: we look down at the pond at an angle, so things lying on the
/// water look a little flatter top to bottom (Ripples.Squash). The fish
/// lives and moves in "ground" coordinates (as if seen straight down), and
/// each edge point is squashed to the screen only when drawn. So a koi
/// swimming toward the top of the screen looks a little shorter than one
/// swimming sideways, as it should.
/// </summary>
internal sealed class Koi : Body
{
    private const int Points = 22;
    private const int PatLen = 128, PatWide = 32;         // the markings' map: steps along the body, steps across
    private const int SkinLen = 256, SkinWide = 64;       // the finished colour sheet: twice as fine, edges already smoothed

    // How much light the rounded body catches at each step across it (the
    // backbone brightest, the flanks falling away): worked out once for all fish.
    private static readonly float[] Round = Enumerable.Range(0, SkinWide)
        .Select(i => { float v = (i + 0.5f) / SkinWide * 2 - 1; return 0.7f + 0.3f * MathF.Sqrt(MathF.Max(0, 1 - v * v)); }).ToArray();
    private const float FinEnd = 1.3f;                    // the tail fin reaches 30% of a body length past the tail stalk

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly float _len, _wide;                   // body length and half its widest width, ground pixels
    private readonly float _cruise;                       // this fish's beat speed, body lengths a second
    private readonly float _w1, _w2, _p1, _p2;            // its own two wander waves
    private readonly byte[] _pattern;                     // which colour at each (s, v): 128 steps along, 32 across
    private readonly (int R, int G, int B)[] _palette;
    private readonly (int R, int G, int B) _fin;
    private readonly float _finAlpha;
    private readonly bool _metal;
    private readonly uint[] _skin;                        // the body's finished colours, 0x00RRGGBB, SkinLen along by SkinWide across

    private float _x, _y, _heading, _speed, _amp, _phase;
    private bool _beating;                                // the beat-and-glide rhythm's own clock: beating now?
    private bool _swimming;                               // actually beating this frame (the rhythm, overruled by the mind: a chase beats, a rest glides)
    private float _beatTimer, _depthTimer, _gulpTimer, _zTarget;
    private bool _gulping;
    private float _bumpTimer, _bumpGap;                    // the back-bump: when next, and the gap between its rings
    private bool _bumping;
    private int _bumpRings;
    private float _goalX, _goalY, _goalTimer;            // where it is idly heading, ground pixels, and until when
    private double _time;

    // ---- THE MIND ----
    // PERSONALITY: three numbers rolled once, 0 to 1, that every decision reads.
    // Think of three dials on each fish. Energy: how much of its time it spends
    // beating rather than gliding, and how fast it cruises. Sociable: how much
    // it likes company (follows a friend, starts a chase). Curious: how often
    // it goes to look at things (a petal, a lily pad's edge, a dragonfly's tap).
    private readonly float _energy, _social, _curious;
    private readonly float _play;                         // the "Koi play" knob: 1 = normal, 0 = only wander and rest
    private readonly Petals? _petals;                     // the floating petals, to nose at and eat

    /// <summary>What a koi can be doing. One at a time; Decide() picks the next when the current one ends.</summary>
    private enum Doing
    {
        Wander,        // idle: a spot somewhere in the pond, a slow cruise toward it
        Rest,          // hang deep and still, tail barely moving
        Investigate,   // go and nose at something: a petal, a pad's rim, the cattails, the log, a tap on the water
        Follow,        // swim behind and beside a friend
        Chase,         // a short burst after another fish
        Flee,          // being chased: speed up and swerve away
    }
    private Doing _doing;
    private float _doingTimer;                            // how long the current doing may last
    private float _targetX, _targetY;                     // what it is investigating, ground pixels
    private bool _targetIsPetal;                          // a petal drifts: the target is refreshed each frame
    private float _hold;                                  // seconds left nosing at the thing it reached (0 = still on the way)
    private Koi? _partner;                                // the fish it follows, chases or flees
    private int _side;                                    // which side of the partner it follows on (+1 or -1)
    private float _chaseCool;                             // seconds until it may start another chase
    private float _stuckFor;                              // seconds its steps have been refused (nosed against something)

    /// <summary>The doing now, for logs and tests (never read by the drawing).</summary>
    public string State => _doing.ToString();

    // The trail the head has swum, newest last, in ground pixels. A ring
    // buffer: a fixed row of slots used round and round, so remembering never
    // allocates memory while the screensaver runs.
    private readonly PointF[] _trail = new PointF[96];
    private int _trailHead, _trailCount;

    /// <summary>How deep it is now: 0 at the surface, 1 as deep as a fish goes.</summary>
    public float Z { get; private set; }

    public PointF Head => ToScreen(_x, _y);

    /// <summary>The middle of its body, screen pixels.</summary>
    public PointF Middle => Spine[8];

    /// <summary>The base of its tail, screen pixels (with the head, it says which way the body points).</summary>
    public PointF Tail => Spine[17];

    /// <summary>Just behind the head, screen pixels (head to neck is the way the nose points).</summary>
    public PointF Neck => Spine[2];

    /// <summary>The screen box the fish (fins and all) can paint in this frame.</summary>
    public RectangleF Box
    {
        get
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (PointF p in Spine) { x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y); x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y); }
            float m = _wide * 1.3f + _len * 0.25f + _pond.U * 0.01f;      // body width, side fins, soft edge
            return RectangleF.FromLTRB(x0 - m, y0 - m, x1 + m, y1 + m);
        }
    }

    public Koi(Pond pond, Ripples ripples, bool[] where, Random rng, int index, float play = 1, Petals? petals = null) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _where = where;
        _rng = rng;
        _play = play;
        _petals = petals;
        float u = pond.U;
        _len = u * (0.095f + 0.06f * (float)rng.NextDouble());
        _wide = _len * (0.12f + 0.02f * (float)rng.NextDouble());
        // The personality dials. Each is rolled on its own, so a fish can be
        // lazy and sociable, or energetic and solitary: no two alike.
        _energy = (float)rng.NextDouble();
        _social = (float)rng.NextDouble();
        _curious = (float)rng.NextDouble();
        _cruise = 0.45f + 0.35f * _energy;                // an energetic fish cruises faster
        _chaseCool = 30 + 90 * (float)rng.NextDouble();   // nobody chases in the first half minute
        _w1 = 0.11f + 0.12f * (float)rng.NextDouble(); _w2 = 0.31f + 0.2f * (float)rng.NextDouble();
        _p1 = (float)(rng.NextDouble() * MathF.Tau); _p2 = (float)(rng.NextDouble() * MathF.Tau);
        (_palette, _pattern, _fin, _finAlpha, _metal) = Variety(index, rng);
        _skin = Skin();

        // Along each spine point: 18 on the body, 4 on the tail fin.
        for (int i = 0; i < Points; i++)
            Along[i] = i < 18 ? i / 17f : 1 + (i - 17) / 4f * (FinEnd - 1);

        // A start in open water, well away from the bank, facing anywhere.
        for (int tries = 0; tries < 500; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _len * 1.4f) continue;
            (_x, _y) = ToGround(x, y);
            break;
        }
        _heading = (float)(rng.NextDouble() * MathF.Tau);
        _speed = _len * 0.3f;
        _beating = rng.Next(2) == 0;
        _beatTimer = (float)rng.NextDouble() * 2;
        Z = 0.2f + 0.4f * (float)rng.NextDouble();
        _zTarget = Z;
        _depthTimer = 3 + (float)rng.NextDouble() * 8;
        _gulpTimer = 10 + (float)rng.NextDouble() * 60;
        _bumpTimer = 40 + (float)rng.NextDouble() * 200;
        NewGoal();
        // A straight trail behind it to start.
        for (int i = 40; i >= 0; i--)
            Remember(new PointF(_x - MathF.Cos(_heading) * _len * i / 30f, _y - MathF.Sin(_heading) * _len * i / 30f));
    }

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    public void Update(float dt, Koi[] school)
    {
        _time += dt;
        float t = (float)(_time % 100000.0);

        // ---- The mind decides what it is doing ----
        Think(dt, school);
        bool rush = _doing is Doing.Chase or Doing.Flee;                          // flat out
        bool still = _doing == Doing.Rest || (_doing == Doing.Investigate && _hold > 0);   // hanging in the water, nosing at something

        // ---- Beat and glide ----
        // The rhythm's own clock: an energetic fish beats for longer and glides for less.
        _beatTimer -= dt;
        if (_beatTimer <= 0)
        {
            _beating = !_beating;
            _beatTimer = _beating ? (1.2f + 1.8f * (float)_rng.NextDouble()) * (0.6f + 0.8f * _energy)
                                  : (1.5f + 3.5f * (float)_rng.NextDouble()) * (1.6f - 0.8f * _energy);
        }
        // The mind overrules the rhythm: a chase is all beating, a rest is all glide.
        _swimming = rush || (_beating && !still);
        float targetSpeed = rush ? _len * _cruise * 1.6f : _swimming ? _len * _cruise : still ? _len * 0.07f : _len * 0.16f;
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (rush ? 2.2f : _swimming ? 1.1f : 0.45f)));
        float targetAmp = rush ? _len * 0.11f : _swimming ? _len * 0.085f : _len * 0.022f;
        _amp += (targetAmp - _amp) * (1 - MathF.Exp(-dt * 2.5f));
        float beatsPerSecond = rush ? 2.3f : _swimming ? 1.5f : 0.7f;
        _phase = (_phase + MathF.Tau * beatsPerSecond * dt) % MathF.Tau;

        // ---- Steering ----
        // Its own idle wander (quieter when it has somewhere to be), plus a
        // pull toward wherever the mind says: a spot, a thing, a friend, or
        // away from the fish behind it. The pull is gentle for an idle
        // cruise and firm for a chase, so a chase really turns.
        float idle = rush || _doing == Doing.Investigate ? 0.35f : 1f;
        float turn = idle * (0.32f * MathF.Sin(t * _w1 + _p1) + 0.22f * MathF.Sin(t * _w2 + _p2));
        (float gx, float gy, float pull) = Goal();
        float gdx = gx - _x, gdy = gy - _y;
        if (gdx * gdx + gdy * gdy > _len * _len * 0.04f)                         // right on top of it: no direction to pull toward
            turn += Wrap(MathF.Atan2(gdy, gdx) - _heading) * pull;
        float look = _len * 1.0f + _speed * 1.0f;
        PointF here = ToScreen(_x, _y);
        float roomAhead = RoomAlong(_heading, look), roomHere = _pond.RoomAt(here.X, here.Y);
        bool cornered = false;
        if (roomAhead < _len * 0.55f || roomHere < _len * 0.35f || _stuckFor > 0)       // a refused step last frame also sends it looking for a way
        {
            // The bank is coming. Like a person feeling their way: try a few
            // headings either side, and take the one with the most open water
            // ahead, preferring a smaller turn when two are about as good.
            float best = _heading, bestScore = MathF.Min(roomAhead, _len * 0.7f);
            for (int k = 1; k <= 4; k++)
                foreach (int side in (ReadOnlySpan<int>)[1, -1])
                {
                    float a = _heading + side * k * 0.42f;
                    // "Enough room" is enough: past a body length more open water
                    // counts for nothing, so it takes the smallest turn that clears
                    // the bank and follows the edge, rather than heading for the middle.
                    float score = MathF.Min(RoomAlong(a, look), _len * 0.7f) - k * _len * 0.08f;
                    if (score > bestScore) { bestScore = score; best = a; }
                }
            float urgency = Math.Clamp(1 - MathF.Min(roomAhead, roomHere * 1.5f) / (_len * 0.55f), 0, 1);
            turn += Wrap(best - _heading) * (1f + 3f * urgency);
            cornered = urgency > 0.5f;
        }
        foreach (Koi other in school)
        {
            if (other == this) continue;
            float dx = _x - other._x, dy = _y - other._y, d2 = dx * dx + dy * dy, near = (_len + other._len) * 0.55f;
            if (other == _partner) near *= _doing == Doing.Chase ? 0.3f : 0.7f;   // a friend may come closer; a chaser nearly touches
            if (d2 > near * near || d2 < 1) continue;
            float away = MathF.Atan2(dy, dx);
            turn += Wrap(away - _heading) * 0.5f * (1 - MathF.Sqrt(d2) / near);
        }
        // A fish cannot turn on the spot: the faster, the wider its turns, and
        // however slowly it is moving its turning circle is never tighter than
        // about 0.4 of its length (0.3 when cornered: it slows and swings round
        // tighter, as koi do at a wall). Without that floor a fish holding
        // nearly still at a petal could swing its heading right round in a
        // second and creep off through its own body, tail first to the eye.
        float most = (cornered ? 1.4f : 0.45f) + _speed / (_len * 0.5f) * 0.6f;
        most = MathF.Min(most, MathF.Max(0.12f, _speed / (_len * (cornered ? 0.3f : 0.4f))));
        if (cornered) _speed *= 1 - 0.6f * dt;
        turn = Math.Clamp(turn, -most, most);
        _heading = Wrap(_heading + turn * dt);

        float step = _speed * dt;
        PointF next = ToScreen(_x + MathF.Cos(_heading) * step, _y + MathF.Sin(_heading) * step);
        float roomNext = _pond.RoomAt(next.X, next.Y);
        // Never stuck: a step is refused only onto land or a rock, or when the
        // water is closing in AND it does not open up again half a length on.
        // (A pocket between two rocks is a tiny hill in the coarse room grid:
        // every step off its top loses a little room, so "only step toward
        // more room" wedged a fish there for minutes, facing open water.)
        bool refused = OnLand(next) || (roomNext <= _len * 0.15f && roomNext < roomHere && RoomAlong(_heading, _len * 0.5f) <= roomHere);
        if (!refused)
        {
            _x += MathF.Cos(_heading) * step;
            _y += MathF.Sin(_heading) * step;
            _stuckFor = 0;
        }
        else
        {
            // Nose right at the bank: hold here (still turning) rather than swim into it;
            // and after two seconds of that, whatever it was doing is given up.
            _stuckFor += dt;
            if (_stuckFor > 2) { _stuckFor = 0; Begin(Doing.Wander); }
        }
        PointF last = _trail[_trailHead];
        if ((last.X - _x) * (last.X - _x) + (last.Y - _y) * (last.Y - _y) > (_len / 40) * (_len / 40))
            Remember(new PointF(_x, _y));

        // ---- Up and down in the water, and the occasional gulp ----
        _depthTimer -= dt;
        if (_depthTimer <= 0 && !_gulping && !_bumping)
        {
            _zTarget = 0.12f + 0.6f * (float)_rng.NextDouble();
            _depthTimer = 6 + 10 * (float)_rng.NextDouble();
        }
        _gulpTimer -= dt;
        if (_gulpTimer <= 0 && !_gulping && !_bumping) { _gulping = true; _zTarget = 0; }

        // ---- The back-bump: now and then a cruising koi rises slowly until its back
        // brushes the surface from below, and three thin rings slip off its back as it
        // glides on; then it sinks back. Rare, slow and quiet: the rise takes ten seconds
        // or so at the ordinary drift rate, and the rings are the thin kind. ----
        _bumpTimer -= dt;
        if (_bumpTimer <= 0 && !_gulping && !_bumping) { _bumping = true; _zTarget = 0; _depthTimer = 40; }
        if (_bumping && Z < 0.06f)
        {
            _bumpGap -= dt;
            if (_bumpGap <= 0)
            {
                PointF back = Spine[7];                                   // the highest point of the back, behind the shoulders
                _ripples.Add(back.X, back.Y, 0.5f, thin: true);
                _bumpRings++;
                _bumpGap = 0.4f;
            }
            if (_bumpRings >= 3)
            {
                _bumping = false;
                _bumpRings = 0;
                _bumpTimer = 120 + 240 * (float)_rng.NextDouble();
                _zTarget = 0.15f + 0.3f * (float)_rng.NextDouble();
                _depthTimer = 6 + 10 * (float)_rng.NextDouble();
            }
        }
        int ix = Math.Clamp((int)here.X, 0, _pond.Width - 1), iy = Math.Clamp((int)here.Y, 0, _pond.Height - 1);
        float bottom = 0.05f + 0.9f * _pond.Depth[iy * _pond.Width + ix] / 255f;   // shallow water: it cannot be deep
        float zWant = MathF.Min(_zTarget, bottom);
        Z += (zWant - Z) * (1 - MathF.Exp(-dt * (_gulping ? 0.6f : 0.18f)));
        if (_gulping && Z < 0.05f)
        {
            PointF mouth = Spine[0];
            _ripples.Add(mouth.X, mouth.Y, 1f);
            _petals?.Take(mouth.X, mouth.Y, _len * 0.45f);                       // a petal at its mouth is eaten
            _gulping = false;
            _gulpTimer = 40 + 60 * (float)_rng.NextDouble();
            _zTarget = 0.2f + 0.4f * (float)_rng.NextDouble();
            _depthTimer = 8;
        }

        LaySpine();
    }

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    /// <summary>Picks somewhere new to idle toward: a random spot in open water, and how long to keep at it.</summary>
    /// <param name="deep">Only a deep spot (a resting place), if one turns up.</param>
    private void NewGoal(bool deep = false)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _len * 0.4f) continue;
            if (deep && tries < 40 && _pond.Depth[(int)y * _pond.Width + (int)x] < 150) continue;
            (_goalX, _goalY) = ToGround(x, y);
            break;
        }
        _goalTimer = 12 + 18 * (float)_rng.NextDouble();
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: a koi has one thing on its mind at a time (a Doing), a
    // feeling for how long to keep at it (a timer), and when that runs out it
    // decides what to do next by rolling weighted dice. The weights are where
    // the personality lives: a curious fish's dice have more "go and look"
    // faces, a sociable fish's more "follow a friend" and "chase" faces, a lazy
    // one's more "rest" faces. Two things can interrupt: being chased (it
    // flees at once), and a dragonfly tapping the water (a curious fish goes
    // to see). The steering below the mind only ever receives a point to swim
    // toward and how firmly, so whatever the mind asks, the bank, the other
    // fish and the "never stuck" rule still have the last word.

    /// <summary>Keeps the current doing going, ends it when it is done, and reacts to being chased.</summary>
    private void Think(float dt, Koi[] school)
    {
        _doingTimer -= dt;
        _chaseCool -= dt;

        // Someone is after me: the moment a chaser is close behind, flee (whatever I was doing, except already fleeing).
        if (_doing != Doing.Flee)
            foreach (Koi o in school)
                if (o != this && o._doing == Doing.Chase && o._partner == this && Near(o, 1.6f) && Toward(o, this) > 0.2f)
                {
                    Begin(Doing.Flee, o);
                    break;
                }

        switch (_doing)
        {
            case Doing.Flee:
                // Safe again once the chaser gives up or falls well behind.
                if (_partner == null || _partner._doing != Doing.Chase || _partner._partner != this || !Near(_partner, 3f))
                    Begin(Doing.Wander);
                break;
            case Doing.Chase:
                if (_doingTimer <= 0 || _partner == null) Decide(school);
                break;
            case Doing.Follow:
                // Lose interest in time, or when the friend has got well away.
                if (_doingTimer <= 0 || _partner == null || !Near(_partner, 4f)) Decide(school);
                break;
            case Doing.Investigate:
                if (_targetIsPetal && _petals != null)
                {
                    // The petal drifts: keep the target on it (or give up if it sank).
                    PointF was = ToScreen(_targetX, _targetY);
                    if (_petals.NearestFloating(was.X, was.Y, _pond.U * 0.08f, out PointF now)) (_targetX, _targetY) = ToGround(now.X, now.Y);
                    else { Decide(school); break; }
                }
                float tdx = _targetX - _x, tdy = _targetY - _y;
                if (_hold > 0)
                {
                    _hold -= dt;
                    if (_hold <= 0) Decide(school);
                }
                else if (tdx * tdx + tdy * tdy < _len * _len * 0.36f)
                {
                    // Arrived: hang here nosing at it for a moment; a petal gets gulped at (and eaten, if the mouth lands on it).
                    _hold = 1 + 2.5f * (float)_rng.NextDouble();
                    if (_targetIsPetal && !_gulping && !_bumping) { _gulping = true; _zTarget = 0; }
                }
                else if (_doingTimer <= 0) Decide(school);
                break;
            case Doing.Rest:
                if (_doingTimer <= 0) Decide(school);
                break;
            default:
                _goalTimer -= dt;
                float gdx = _goalX - _x, gdy = _goalY - _y;
                if (_goalTimer <= 0 || gdx * gdx + gdy * gdy < _len * _len * 2.25f) Decide(school);
                break;
        }
    }

    /// <summary>Where the mind says to swim (ground pixels), and how firmly to turn toward it.</summary>
    private (float X, float Y, float Pull) Goal()
    {
        switch (_doing)
        {
            case Doing.Chase:
                return (_partner!._x, _partner._y, 1.1f);
            case Doing.Flee:
                {
                    // Straight away from the chaser, a few body lengths ahead of me.
                    float ax = _x - _partner!._x, ay = _y - _partner._y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                    return (_x + ax / l * _len * 3, _y + ay / l * _len * 3, 0.9f);
                }
            case Doing.Follow:
                {
                    // A body length behind the friend and half a length to one side, as fish swim in company.
                    float hx = MathF.Cos(_partner!._heading), hy = MathF.Sin(_partner._heading);
                    return (_partner._x - hx * _len * 1.0f - hy * _side * _len * 0.5f, _partner._y - hy * _len * 1.0f + hx * _side * _len * 0.5f, 0.5f);
                }
            case Doing.Investigate:
                return (_targetX, _targetY, _hold > 0 ? 0.25f : 0.6f);
            case Doing.Rest:
                return (_goalX, _goalY, 0.25f);
            default:
                return (_goalX, _goalY, 0.4f);
        }
    }

    /// <summary>Rolls the next doing, weighted by personality and by what there is to do.</summary>
    private void Decide(Koi[] school)
    {
        // A friend to follow must be near, and a fish to chase nearer still (a
        // chase begun across the pond was over before the chaser got anywhere).
        Koi? friend = Nearest(school, 4f), quarry = Nearest(school, 2.5f);
        bool chaseOn = quarry != null && _chaseCool <= 0 && !school.Any(k => k._doing is Doing.Chase or Doing.Flee);   // one chase in the pond at a time
        // The dice faces: each doing's weight. Wander is always on the dice.
        float wWander = 1f;
        float wRest = (0.25f + 0.9f * (1 - _energy)) * (_doing == Doing.Rest ? 0.4f : 1f);               // and a rest is less likely straight after a rest
        float wLook = (0.08f + 0.45f * _curious) * _play * (_doing == Doing.Investigate ? 0.4f : 1f);   // less likely straight after a look
        float wFollow = friend != null ? (0.1f + 0.7f * _social) * _play : 0;
        float wChase = chaseOn ? (0.1f + 0.7f * _social * _energy) * _play : 0;
        float roll = (float)_rng.NextDouble() * (wWander + wRest + wLook + wFollow + wChase);
        if ((roll -= wChase) < 0) { Begin(Doing.Chase, quarry); return; }
        if ((roll -= wFollow) < 0) { Begin(Doing.Follow, friend); return; }
        if ((roll -= wLook) < 0 && PickTarget()) { Begin(Doing.Investigate); return; }
        if ((roll -= wRest) < 0) { Begin(Doing.Rest); return; }
        Begin(Doing.Wander);
    }

    /// <summary>Starts a doing: its timer, its partner, and where in the water to be for it.</summary>
    private void Begin(Doing doing, Koi? partner = null)
    {
        _doing = doing;
        _partner = partner;
        _hold = 0;
        switch (doing)
        {
            case Doing.Chase:
                _doingTimer = 4 + 5 * (float)_rng.NextDouble();
                _chaseCool = (60 + 140 * (float)_rng.NextDouble()) / MathF.Max(0.1f, _play);
                Depth(0.1f + 0.15f * (float)_rng.NextDouble(), 20);
                break;
            case Doing.Flee:
                _doingTimer = 10;
                Depth(0.08f + 0.15f * (float)_rng.NextDouble(), 20);
                break;
            case Doing.Follow:
                _doingTimer = 10 + 20 * (float)_rng.NextDouble();
                _side = _rng.Next(2) * 2 - 1;
                Depth(MathF.Max(0.05f, partner!.Z + 0.1f), _doingTimer);      // a little under the friend
                break;
            case Doing.Investigate:
                _doingTimer = 25;                                              // give up if it is not reached by then
                Depth(0.06f, 30);                                              // right up near the surface, where it can be seen looking
                break;
            case Doing.Rest:
                _doingTimer = 8 + 14 * (float)_rng.NextDouble();
                NewGoal(deep: true);
                Depth(0.55f + 0.3f * (float)_rng.NextDouble(), _doingTimer);
                break;
            default:
                NewGoal();
                break;
        }
    }

    /// <summary>Asks for a depth and holds it for a while (the idle up-and-down waits).</summary>
    private void Depth(float z, float holdFor)
    {
        if (_gulping || _bumping) return;
        _zTarget = z;
        _depthTimer = holdFor;
    }

    /// <summary>The nearest other fish within "lengths" body lengths, or null.</summary>
    private Koi? Nearest(Koi[] school, float lengths)
    {
        Koi? best = null;
        float bestD = _len * _len * lengths * lengths;
        foreach (Koi o in school)
        {
            if (o == this) continue;
            float dx = o._x - _x, dy = o._y - _y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    /// <summary>
    /// Something to go and look at, near enough to bother: a floating petal
    /// (first choice when there is one close), the rim of a lily pad, the foot
    /// of the cattails, or the side of the floating log. False if nothing is
    /// in reach, and the fish wanders instead.
    /// </summary>
    private bool PickTarget()
    {
        PointF here = ToScreen(_x, _y);
        float u = _pond.U;
        _targetIsPetal = false;
        if (_petals != null && _rng.NextDouble() < 0.4 && _petals.NearestFloating(here.X, here.Y, u * 0.6f, out PointF petal) && _pond.RoomAt(petal.X, petal.Y) >= _len * 0.6f)
        {
            (_targetX, _targetY) = ToGround(petal.X, petal.Y);
            _targetIsPetal = true;
            return true;
        }
        for (int tries = 0; tries < 6; tries++)
        {
            PointF p;
            int kind = _rng.Next(3);
            if (kind == 0 && _pond.Pads.Count > 0)
            {
                // A point just off a pad's rim, on the side facing me.
                LilyPad pad = _pond.Pads[_rng.Next(_pond.Pads.Count)];
                float ax = here.X - pad.Center.X, ay = here.Y - pad.Center.Y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                p = new PointF(pad.Center.X + ax / l * (pad.Radius + _len * 0.3f), pad.Center.Y + ay / l * (pad.Radius + _len * 0.3f));
            }
            else if (kind == 1)
            {
                // The foot of the cattails, from the open-water side.
                PointF c = _pond.CattailSpot;
                float ax = _pond.Centre.X - c.X, ay = _pond.Centre.Y - c.Y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                p = new PointF(c.X + ax / l * u * 0.1f, c.Y + ay / l * u * 0.1f);
            }
            else if (_pond.Log is Log log && log.Afloat)
            {
                // Alongside the floating log, a little off its side.
                float along = ((float)_rng.NextDouble() - 0.5f) * log.Length * 0.8f;
                float nx = -MathF.Sin(log.Angle), ny = MathF.Cos(log.Angle);
                int side = (here.X - log.Centre.X) * nx + (here.Y - log.Centre.Y) * ny > 0 ? 1 : -1;
                p = new PointF(log.Centre.X + MathF.Cos(log.Angle) * along + nx * side * (log.Width * 0.5f + _len * 0.35f),
                               log.Centre.Y + MathF.Sin(log.Angle) * along + ny * side * (log.Width * 0.5f + _len * 0.35f));
            }
            else continue;
            float dx = p.X - here.X, dy = p.Y - here.Y;
            if (dx * dx + dy * dy > u * u * 0.8f * 0.8f) continue;                // too far to bother
            if (!InWater(p) || _pond.RoomAt(p.X, p.Y) < _len * 0.6f) continue;   // on land, or too tight against the bank or the rocks to swim up to
            (_targetX, _targetY) = ToGround(p.X, p.Y);
            return true;
        }
        return false;
    }

    /// <summary>A dragonfly tapped the water here (screen pixels): a curious fish not otherwise busy goes to see.</summary>
    public void Notice(float x, float y)
    {
        if (_doing is Doing.Chase or Doing.Flee || _rng.NextDouble() > 0.25 + 0.7 * _curious * _play) return;
        PointF here = ToScreen(_x, _y);
        float dx = x - here.X, dy = y - here.Y;
        if (dx * dx + dy * dy > _pond.U * _pond.U * 0.5f * 0.5f || !InWater(new PointF(x, y)) || _pond.RoomAt(x, y) < _len * 0.6f) return;
        (_targetX, _targetY) = ToGround(x, y);
        _targetIsPetal = false;
        Begin(Doing.Investigate);
    }

    /// <summary>The ground, a rock or the log: a koi can never step here. (A lily pad is not land: koi swim under them.)</summary>
    private bool OnLand(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || _pond.Land[iy * _pond.Width + ix];
    }

    /// <summary>Open water in view (not under a pad, not under the branches): where a fish nosing at something can be seen doing it.</summary>
    private bool InWater(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _where[iy * _pond.Width + ix];
    }

    /// <summary>Is the other fish within "lengths" of my body length?</summary>
    private bool Near(Koi o, float lengths)
    {
        float dx = o._x - _x, dy = o._y - _y;
        return dx * dx + dy * dy < _len * _len * lengths * lengths;
    }

    /// <summary>How squarely a is heading at b: 1 straight at it, 0 sideways, -1 away.</summary>
    private static float Toward(Koi a, Koi b)
    {
        float dx = b._x - a._x, dy = b._y - a._y, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        return (dx * MathF.Cos(a._heading) + dy * MathF.Sin(a._heading)) / l;
    }

    /// <summary>
    /// The open water along a feeler "dist" long on heading a: the least room
    /// at four points along it, the nearer points counting for more (0 on
    /// land). The first point is close in, so a small rock in the shallows,
    /// narrower than half a feeler, cannot slip between the samples: with only
    /// the halfway point and the end tested, a koi nosed into one such rock
    /// behind the cattails, found open water beyond it, and stood there for a
    /// minute while every step onto the rock was refused.
    /// </summary>
    private float RoomAlong(float a, float dist)
    {
        float least = float.MaxValue, ca = MathF.Cos(a), sa = MathF.Sin(a);
        foreach (float f in (ReadOnlySpan<float>)[0.12f, 0.35f, 0.65f, 1f])
        {
            PointF p = ToScreen(_x + ca * dist * f, _y + sa * dist * f);
            if (OnLand(p)) return 0;
            least = MathF.Min(least, _pond.RoomAt(p.X, p.Y) * (2 - f));
        }
        return least;
    }

    /// <summary>
    /// Lays the spine along the remembered trail, adds the swimming wave,
    /// and squashes each pair of edge points to the screen.
    /// </summary>
    private void LaySpine()
    {
        // Walk back along the trail, dropping a spine point every so often.
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(_x, _y), b = _trail[slot];
        float travelled = 0, segLen = Dist(a, b);
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (travelled + segLen < want && left > 0)
            {
                travelled += segLen;
                a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length;
                left--;
                b = _trail[slot];
                segLen = Dist(a, b);
            }
            float f = segLen > 1e-4f ? (want - travelled) / segLen : 0;
            if (left <= 0 && travelled + segLen < want)
            {
                // Ran out of trail (just started): carry on straight back.
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(_heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(_heading);
                mid[i] = new PointF(b.X + ex * (want - travelled - segLen), b.Y + ey * (want - travelled - segLen));
            }
            else mid[i] = new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
        }
        // The direction at each point (nose to tail), for the sideways wave and the edges.
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            // The swimming wave: tiny at the head, widest at the tail, running from head to tail.
            float sway = _amp * (0.06f + s * s) * MathF.Sin(MathF.Tau * 0.85f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;                 // sideways
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s);
            PointF l = ToScreen(cx + nx * hw, cy + ny * hw), r = ToScreen(cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
    }

    /// <summary>
    /// How wide the body is at s, as a fraction of its widest: a blunt
    /// rounded snout, widest just behind the head (at the shoulders), tapering
    /// to a slim tail stalk at s = 1, and then the tail fin fanning out.
    /// </summary>
    private static float Profile(float s)
    {
        if (s < 0.28f) { float k = (0.28f - s) / 0.31f; return MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        if (s <= 1f) return 1 - 0.84f * MathF.Pow((s - 0.28f) / 0.72f, 1.4f);
        float f = (s - 1) / (FinEnd - 1);
        return 0.16f + 0.5f * MathF.Sqrt(f);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        if (s > 1f)
        {
            // The tail fin: forked (the middle ends sooner), see-through, with rays.
            float m = 1 - av, end = FinEnd - 0.09f * m * MathF.Sqrt(m);
            if (s > end) { r = g = b = 0; alpha = 0; return false; }
            (r, g, b) = _fin;
            alpha = _finAlpha * (0.72f + 0.28f * MathF.Cos(v * 15)) * Math.Clamp((end - s) * 30, 0, 1);
            return true;
        }
        int vi = Math.Clamp((int)((v + 1) * 0.5f * SkinWide), 0, SkinWide - 1);
        uint c = _skin[Math.Clamp((int)(s * SkinLen), 0, SkinLen - 1) * SkinWide + vi];
        float shade = Round[vi];
        r = (int)(((c >> 16) & 0xFF) * shade); g = (int)(((c >> 8) & 0xFF) * shade); b = (int)((c & 0xFF) * shade);
        alpha = 1;
        return true;
    }

    /// <summary>
    /// Works out the body's colours once, when the fish is made: the
    /// markings (each sheet step averaged from four looks at the markings'
    /// map, so their edges come out soft), a metallic koi's sheen, and the
    /// darker line of the dorsal fin folded along its back. Drawing then only
    /// looks a colour up (see Colour), which matters at 4K, where a big koi
    /// is tens of thousands of pixels every frame.
    /// </summary>
    private uint[] Skin()
    {
        var skin = new uint[SkinLen * SkinWide];
        for (int si = 0; si < SkinLen; si++)
            for (int vi = 0; vi < SkinWide; vi++)
            {
                float cr = 0, cg = 0, cb = 0;
                for (int k = 0; k < 4; k++)
                {
                    float s = (si + 0.25f + 0.5f * (k & 1)) / SkinLen, v = (vi + 0.25f + 0.5f * (k >> 1)) / SkinWide * 2 - 1;
                    var c = _palette[_pattern[Math.Min(PatLen - 1, (int)(s * PatLen)) * PatWide + Math.Clamp((int)((v + 1) * 0.5f * PatWide), 0, PatWide - 1)]];
                    cr += c.R / 4f; cg += c.G / 4f; cb += c.B / 4f;
                }
                float sc = (si + 0.5f) / SkinLen, vc = (vi + 0.5f) / SkinWide * 2 - 1, av = MathF.Abs(vc);
                float shade = 1;
                if (_metal) shade *= 0.95f + 0.14f * (1 - av) * (1 - av);                                         // a metallic koi: a soft sheen down the back
                if (av < 0.1f && sc > 0.34f && sc < 0.74f) shade *= 0.86f;                                       // the dorsal fin, folded flat along the back
                skin[si * SkinWide + vi] = (uint)(Math.Min(255, (int)(cr * shade)) << 16 | Math.Min(255, (int)(cg * shade)) << 8 | Math.Min(255, (int)(cb * shade)));
            }
        return skin;
    }

    public void Draw(FrameBuffer fb)
    {
        float murk = 0.06f + 0.62f * Z;
        float soft = MathF.Max(1f, _pond.U * 0.0012f) * (1 + 3 * Z);
        // Side fins first, so the body covers where they join it.
        float t = (float)(_time % 100000.0);
        float scull = 0.22f * MathF.Sin(t * 1.4f + _p1) + (_swimming ? 0f : 0.25f);    // gliding: fins out, braking a little
        Fins(fb, 4, 0.2f, 0.12f, 0.95f + scull, murk);
        Fins(fb, 11, 0.11f, 0.065f, 0.75f + scull * 0.5f, murk);
        Paint(fb, _where, _pond.Sun, murk, soft);
        // The eyes: a small dark dot on each side of the head.
        PointF e0 = Spine[1], e1 = Spine[2];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.8f, _len * 0.017f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X - ey / el * HalfWidth[1] * 0.62f * side, e0.Y + ex / el * HalfWidth[1] * 0.62f * side);
            Oval(fb, _where, c, 0, er, er, 24, 26, 28, 0.95f, murk * 0.7f, _pond.Sun);
        }
    }

    /// <summary>A pair of side fins at spine point i, swept back from the body by "sweep" radians.</summary>
    private void Fins(FrameBuffer fb, int i, float len, float wid, float sweep, float murk)
    {
        PointF p = Spine[i], q = Spine[Math.Max(0, i - 1)];
        float back = MathF.Atan2(p.Y - q.Y, p.X - q.X);                         // pointing toward the tail
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            float a = back - side * (MathF.PI / 2 - sweep);
            float nx = MathF.Cos(back + side * -MathF.PI / 2), ny = MathF.Sin(back + side * -MathF.PI / 2);
            float l = _len * len;
            var c = new PointF(p.X + nx * HalfWidth[i] * 0.8f + MathF.Cos(a) * l * 0.5f, p.Y + ny * HalfWidth[i] * 0.8f + MathF.Sin(a) * l * 0.5f * Ripples.Squash);
            Oval(fb, _where, c, a, l * 0.5f, _len * wid * 0.5f, _fin.R, _fin.G, _fin.B, _finAlpha * 0.9f, murk, _pond.Sun);
        }
    }

    /// <summary>
    /// A koi variety: its colours and where they go. Real varieties, chosen
    /// so a pond has a mix (and the first few fish are the classic ones):
    /// kohaku (white with red), sanke (white, red and small black), showa
    /// (black with red and white), yamabuki ogon (metallic gold), chagoi
    /// (bronze), asagi (blue-grey back, orange sides), tancho (white with one
    /// red spot on the head), and plain red-orange.
    /// </summary>
    private static ((int, int, int)[] Palette, byte[] Pattern, (int, int, int) Fin, float FinAlpha, bool Metal) Variety(int index, Random rng)
    {
        (int, int, int) white = (244, 240, 230), red = (214, 58, 34), black = (28, 28, 32), gold = (238, 178, 52),
            bronze = (156, 116, 74), slate = (112, 134, 152), orange = (232, 108, 40);
        int[] order = [0, 1, 2, 3, 0, 4, 5, 6, 1, 7, 3, 2];
        int kind = index < order.Length ? order[index] : rng.Next(8);
        var pattern = new byte[PatLen * PatWide];
        void Blob(byte colour, float s0, float v0, float rs, float rv)
        {
            float wob = (float)(rng.NextDouble() * MathF.Tau);
            for (int si = 0; si < PatLen; si++)
                for (int vi = 0; vi < PatWide; vi++)
                {
                    float s = si / (PatLen - 1f), v = vi / (PatWide - 1f) * 2 - 1;
                    float ds = (s - s0) / rs, dv = (v - v0) / rv;
                    float edge = 1 + 0.22f * MathF.Sin(s * 23 + v * 5 + wob) + 0.12f * MathF.Sin(v * 13 - s * 7);   // ragged, like real markings
                    if (ds * ds + dv * dv < edge) pattern[si * PatWide + vi] = colour;
                }
        }
        // Palette slot 0 is the base colour; the others are the markings.
        (int, int, int)[] palette;
        (int, int, int) fin = (236, 232, 226);
        float finAlpha = 0.5f;
        bool metal = false;
        switch (kind)
        {
            case 0: // kohaku
                palette = [white, red];
                for (int k = 0; k < 2 + rng.Next(2); k++)
                    Blob(1, 0.12f + 0.62f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.6f, 0.1f + 0.12f * (float)rng.NextDouble(), 0.7f + 0.5f * (float)rng.NextDouble());
                break;
            case 1: // sanke
                palette = [white, red, black];
                for (int k = 0; k < 2 + rng.Next(2); k++)
                    Blob(1, 0.12f + 0.62f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.6f, 0.09f + 0.1f * (float)rng.NextDouble(), 0.6f + 0.5f * (float)rng.NextDouble());
                for (int k = 0; k < 3 + rng.Next(3); k++)
                    Blob(2, 0.25f + 0.6f * (float)rng.NextDouble(), (float)(rng.NextDouble() * 2 - 1) * 0.7f, 0.035f + 0.03f * (float)rng.NextDouble(), 0.25f + 0.2f * (float)rng.NextDouble());
                break;
            case 2: // showa
                palette = [black, red, white];
                for (int k = 0; k < 2; k++)
                    Blob(1, 0.1f + 0.5f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.8f, 0.1f + 0.1f * (float)rng.NextDouble(), 0.6f + 0.4f * (float)rng.NextDouble());
                for (int k = 0; k < 2; k++)
                    Blob(2, 0.3f + 0.5f * (float)rng.NextDouble(), (float)(rng.NextDouble() * 2 - 1) * 0.6f, 0.07f + 0.08f * (float)rng.NextDouble(), 0.4f + 0.3f * (float)rng.NextDouble());
                fin = (60, 56, 60);
                break;
            case 3: // yamabuki ogon
                palette = [gold];
                fin = (246, 206, 110);
                metal = true;
                break;
            case 4: // chagoi
                palette = [bronze];
                fin = (170, 132, 92);
                finAlpha = 0.55f;
                metal = true;
                break;
            case 5: // asagi: blue-grey back, orange along the sides
                palette = [slate, orange];
                for (int si = 0; si < PatLen; si++)
                    for (int vi = 0; vi < PatWide; vi++)
                        if (MathF.Abs(vi / (PatWide - 1f) * 2 - 1) > 0.72f - 0.1f * MathF.Sin(si * 0.2f)) pattern[si * PatWide + vi] = 1;
                fin = (226, 140, 96);
                break;
            case 6: // tancho: one red spot on the head
                palette = [white, red];
                Blob(1, 0.1f, 0, 0.065f, 0.55f);
                break;
            default: // red-orange
                palette = [orange];
                fin = (240, 150, 96);
                break;
        }
        return (palette, pattern, fin, finAlpha, metal);
    }
}
