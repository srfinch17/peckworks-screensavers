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
    private readonly Log? _log;
    private readonly Minnows? _minnows;                    // what it may hunt (null when there are no schools)

    // ---- THE MIND ----
    // Two dials rolled at the start of each visit, 0 to 1 (see the class comment),
    // and what they turn into: how fast it cruises and how long a fright lasts.
    private float _bold, _nerve, _cruise, _fleeFor;
    private enum Doing { Cross, Explore, Hunt, Bask }
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

    // Where its head is (screen pixels, before the weave is added), which
    // way it is going, how fast, and how far it has come (the weave's clock).
    private float _x, _y, _heading, _speed, _travelled;
    private PointF _goal;                                  // the point beyond the far edge it is making for
    private float _wakeTimer, _next, _alive;
    private float _flee, _fleeAway;                        // startle: seconds left, and the way away
    private readonly Cooldown _calm = new();               // no startle again until this has run out
    private bool _live, _blocked;                          // _blocked: last frame's step was refused

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

        float a = (float)(_rng.NextDouble() * MathF.Tau), b = a + MathF.PI + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
        PointF from = OffScreen(a), to = OffScreen(b);
        _x = from.X; _y = from.Y;
        _heading = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        _speed = _cruise;
        _travelled = 0; _alive = 0; _flee = 0; _calm.Start(0); _blocked = false; _weaveAmp = 1;
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
        if (_alive > 200) wExplore = wHunt = wBask = 0;    // a long visit ends now
        switch (Dice.Pick(_rng, [wHunt, wBask, wExplore, wCross]))
        {
            case 0: BeginHunt(school!.Value); break;
            case 1: BeginBask(); break;
            case 2 when BeginExplore(): break;                  // nowhere to explore: it crosses instead
            default: BeginCross(); break;
        }
    }

    /// <summary>Leave: toward a point well beyond the far side of the screen, as seen from here through the pond's middle.</summary>
    private void BeginCross()
    {
        _mind.Begin(Doing.Cross); _phase = 0;
        PointF c = _pond.Centre;
        float b = Dist(new PointF(_x, _y), c) > _u * 0.15f ? MathF.Atan2(c.Y - _y, c.X - _x) + ((float)_rng.NextDouble() - 0.5f) * 1.2f : (float)(_rng.NextDouble() * MathF.Tau);
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
            _mind.Begin(Doing.Explore, 45); _phase = 0; _exploreLeft--;
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
        _basked = true; _mind.Begin(Doing.Bask, 60); _phase = 0;
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
                // Reached: close enough that its turning circle cannot orbit the spot.
                if (Dist(head, _goal) < _u * 0.08f || _mind.Over) Decide();
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
    private float Clear(float px, float py)
    {
        float best = _u;
        for (int i = 0; i < _obsCentre.Length; i++)
        {
            float dx = px - _obsCentre[i].X, dy = py - _obsCentre[i].Y;
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - _obsRadius[i]);
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
        foreach (float f in (ReadOnlySpan<float>)[0.08f, 0.2f, 0.35f, 0.55f, 0.78f, 1f])
            best = MathF.Min(best, Clear(_x + dx * dist * f, _y + dy * dist * f));
        return best;
    }

    /// <param name="animals">Points on the other animals this frame (koi heads and middles, turtles, the lizard), screen pixels.</param>
    public void Update(float dt, ReadOnlySpan<PointF> animals)
    {
        if (!_live || _doing != Doing.Bask) _pond.LogPerch.Release(this);   // the log is only the snake's while it is basking (or on its way to)
        if (!_live)
        {
            _next -= dt;
            if (_next <= 0 && _often > 0) { Begin(); _next = (150 + 150 * (float)_rng.NextDouble()) / _often; }
            return;
        }
        _alive += dt;

        // ---- A touch from another animal: startle ----
        // A bold snake feels a touch only when it is close (0.035u), a nervous one
        // from further (0.06u), and the nervous one stays frightened longer.
        _calm.Tick(dt);
        _flee = MathF.Max(0, _flee - dt);
        if (_calm.Ready)
        {
            float near = _u * (0.035f + 0.025f * _nerve), near2 = near * near;
            for (int k = 0; k < animals.Length && _calm.Ready; k++)
                for (int i = 0; i < Points; i += 3)
                {
                    float dx = Spine[i].X - animals[k].X, dy = Spine[i].Y - animals[k].Y;
                    if (dx * dx + dy * dy > near2) continue;
                    // Away from whatever touched it, judged from its head.
                    _fleeAway = MathF.Atan2(_y - animals[k].Y, _x - animals[k].X);
                    _flee = _fleeFor;
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

        Think(dt);
        if (_doing == Doing.Bask && _phase == 2) { UpdateLie(dt); return; }    // lying still: the spine is laid by the pose, nothing steers

        // ---- Speed: a steady swim, slower when stalking or climbing, a burst when startled or striking ----
        bool striking = _doing == Doing.Hunt && _phase == 1;
        float pace = _doing == Doing.Hunt && _phase == 0 ? StalkPace() : _doing == Doing.Bask && _phase == 1 ? 0.6f : 1f;
        float wantSpeed = striking ? _u * 0.18f : _cruise * pace * (1 + 2.2f * fear);
        _speed += (wantSpeed - _speed) * (1 - MathF.Exp(-dt * (striking ? 14f : fear > 0 ? 6f : 1.2f)));   // a lunge gets up to speed almost at once

        // ---- Steering ----
        // A gentle pull toward the goal the mind chose (firmer when climbing onto
        // the log or striking), a slow wander, and when startled a hard swing
        // away from whatever touched it.
        float pull = striking ? 2.5f : _doing == Doing.Bask && _phase == 1 ? 1.5f : 0.5f;
        float turn = Wrap(MathF.Atan2(_goal.Y - _y, _goal.X - _x) - _heading) * pull;
        if (!striking && !(_doing == Doing.Bask && _phase == 1)) turn += 0.2f * MathF.Sin(_alive * 0.35f);
        float most = (striking ? 2.5f : 1.0f) + 1.5f * fear;

        // Something in the way? Feel for the smallest turn that clears it.
        // "Enough room" is the head's own width a few times over; past that,
        // more room counts for nothing, so it skirts a pad rather than
        // heading for the far side of the pond.
        float look = _len * 0.8f + _speed * 0.8f, margin = _u * 0.035f;    // wider than the weave's swing, so the body clears too
        float clearAhead = _blocked ? 0 : ClearAlong(_heading, look);
        // A fright swings it away from what touched it, but never into something
        // else: the nearer an obstacle, the less the swing counts (an early
        // build fled straight into a lily pad and sat inside it).
        if (fear > 0) turn += Wrap(_fleeAway - _heading) * 2.5f * fear * Math.Clamp(clearAhead / margin, 0, 1);
        if (clearAhead < margin)
        {
            float best = _heading, bestScore = MathF.Min(clearAhead, margin * 2);
            for (int k = 1; k <= 5; k++)
                foreach (int side in (ReadOnlySpan<int>)[1, -1])
                {
                    float a = _heading + side * k * 0.35f;
                    float score = MathF.Min(ClearAlong(a, look), margin * 2) - k * margin * 0.15f;
                    if (score > bestScore) { bestScore = score; best = a; }
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
        _blocked = !(clearNext > 0 || clearNext >= Clear(_x, _y));   // never stuck: a step toward more room is always allowed
        if (!_blocked)
        {
            _x = nx; _y = ny;
            _travelled += step;
        }
        // Gone: leaving, and its head is far enough past an edge that its tail has left too.
        float m = _len * 1.25f;
        if (_doing == Doing.Cross && _alive > 3 && (_x < -m || _y < -m || _x > _pond.Width + m || _y > _pond.Height + m)) { _live = false; return; }

        // The weave: side to side, a full swing every 65% of a body length
        // travelled, so about one and a half waves lie along its body at once.
        // Distance drives it, so a burst of speed quickens the weave by itself.
        // Its width eases to the doing's: narrow while stalking or climbing (a
        // creeping snake does not thrash), nearly straight in a strike.
        float weaveWant = striking ? 0.3f : _doing == Doing.Hunt ? 0.5f : _doing == Doing.Bask && _phase == 1 ? 0.35f : 1f;
        weaveWant *= 0.4f + 0.6f * Math.Clamp(clearAhead / (margin * 2), 0, 1);       // and narrower still beside an obstacle, so the body's swing never brushes it
        _weaveAmp += (weaveWant - _weaveAmp) * (1 - MathF.Exp(-dt * 1.5f));
        float weave = _len * 0.085f * _weaveAmp * MathF.Sin(_travelled / (_len * 0.65f) * MathF.Tau);
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
    }

    /// <summary>The spine, laid along the remembered trail, nose first.</summary>
    private void LaySpine(PointF head)
    {
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
            HalfWidth[i] = _wide * Profile(Along[i]);
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

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        // Olive back, darker on the head, with four dark stripes running down the body.
        (r, g, b) = s < 0.06f ? (112, 104, 62) : (150, 140, 88);
        bool stripe = s > 0.07f && (MathF.Abs(av - 0.32f) < 0.08f || MathF.Abs(av - 0.76f) < 0.08f);
        if (stripe) { r = 66; g = 58; b = 36; }
        float shade = 0.72f + 0.28f * MathF.Sqrt(MathF.Max(0, 1 - v * v));             // a rounded back
        r = (int)(r * shade); g = (int)(g * shade); b = (int)(b * shade);
        alpha = 1;
        return true;
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
