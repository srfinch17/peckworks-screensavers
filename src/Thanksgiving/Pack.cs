using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// A small pack of coyotes, and the rabbits they hunt, on the far lane at the back of the field. His line: "give me
/// a small pack of coyotes that runs around too. they occasionally dig for stuff. also some rabbits. they chase
/// rabbits sometimes. occasionally one coyote will catch a rabbit in the distance and they will play with its
/// carcass for a minute. it's supposed to be a tiny horrifying thing that happens way in the background to reward
/// viewers for watching."
///
/// FEYNMAN VERSION: the rabbits are always there, tiny, sitting and grazing and hopping about along the lane. The
/// pack is AWAY most of the time, beyond the edge of the picture. Every few minutes it trots in: the coyotes roam
/// about, stop, sit, dig at the ground, and when one is near a rabbit and feels like it, it gives chase at a
/// gallop while the rabbit flees in long hops. Most chases fail (the rabbit jinks and is faster over a short run,
/// and the coyote gives up); now and then one is caught. Then the catcher shakes it, drops it, and the whole pack
/// comes over to play with it for a minute: pouncing on it, tossing it in the air, two of them tugging it between
/// them, one carrying it a few steps and dropping it. Then one picks it up and they all trot off with it, and a
/// while later a new rabbit hops in from the edge. Every coyote and rabbit has a Mind from the shared kit
/// (Core/Minds) with dials dealt at birth, and the coyotes pin Threat notices as they pass, so the deer lift their
/// heads and the timid trot off, and the turkeys hurry: the whole field knows the pack is through.
///
/// The lane is the field's far edge, BEHIND every deer and turkey, so this is all drawn first through the field
/// stencil, after the tractor's bales, and everything nearer walks in front of it. Test aids: PECKWORKS_PACK=now
/// (the first bout at 5 s), PECKWORKS_PACK=catch (every chase is run and every chase catches), PECKWORKS_MINDLOG
/// (a line per coyote and rabbit each frame, and every event: chase, caught, play acts, leave).
/// </summary>
internal sealed class Pack
{
    private enum Doing { Away, Roam, Stand, Dig, Sit, Chase, Shake, Play, Leave }
    private enum RDoing { Sit, Graze, Hop, Flee, Dead, Gone }
    private enum Act { None, Pounce, Toss, Tug, Carry, Watch }

    private sealed class Coyote
    {
        public float X, Depth;                      // the feet: x in pixels, depth 0 (the lane) to MaxDepth (a little way into the field)
        public int Dir = 1;
        public float Bold;                          // the dial: a bold one chases more
        public Mind<Doing> Mind = new(Doing.Away);
        public Pace Pace = null!;
        public float Goal, GoalDepth;
        public int Target = -1;                     // the rabbit it is chasing
        public bool Doomed;                         // this chase ends in a catch (rolled when it starts)
        public float Clock;                         // seconds into this doing
        public bool Turning; public float TurnT;
        public Act Act; public float ActT;
        public bool Carrying;                       // the carcass is in its mouth
    }

    private sealed class Rabbit
    {
        public float X, Depth;
        public int Dir = 1;
        public float Timid;
        public Mind<RDoing> Mind = new(RDoing.Sit);
        public Pace Pace = null!;
        public float Goal, GoalDepth, Clock, Hops;  // Hops: the odometer in hop lengths, for the page
        public float CalmFor;                       // seconds no coyote has been near, for ending a flight
    }

    private const float MaxDepth = 0.13f;
    private const float Trot = 0.075f, Gallop = 0.22f;           // U per second
    private const float RabbitHop = 0.045f, RabbitFlee = 0.19f, RabbitDoomed = 0.13f;   // a fleeing rabbit nearly matches the gallop and the coyote's patience runs out; the one that is caught is the slow one (the chase is rolled, so the pace is)
    private const float TrotStride = 1.3f, GallopStride = 2.4f;   // in coyote lengths
    private const float HopLen = 0.9f, FleeHopLen = 1.6f;         // in rabbit lengths
    private const float BoutEvery = 240f, BoutFor = 75f, PlayFor = 60f, ChaseFor = 7f;
    private const float TurnFor = 0.28f;
    private const int TrotPages = 24, GallopPages = 24, DigPages = 8, ShakePages = 6, PouncePages = 12, HopPages = 12, TurnPages = 3;
    private static readonly float[] Squash = [0.72f, 0.42f, 0.14f];

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;
    private readonly bool[] _stencil;
    private readonly string? _test = Environment.GetEnvironmentVariable("PECKWORKS_PACK");
    private readonly Coyote[] _pack;
    private readonly Rabbit[] _rabbits;
    private readonly float _k, _r;                               // pixels to a coyote length, and to a rabbit length
    private readonly int _cw, _ch, _cox, _cby, _rw, _rh, _rox, _rby, _lw, _lh, _lox, _lby;
    private readonly Sprite[][] _trot = new Sprite[2][], _gallop = new Sprite[2][], _dig = new Sprite[2][], _shake = new Sprite[2][], _pounce = new Sprite[2][], _turn = new Sprite[2][];
    private readonly Sprite[] _stand = new Sprite[2], _sit = new Sprite[2], _carry = new Sprite[2], _pull = new Sprite[2];
    private readonly PointF[] _trotMouth = new PointF[TrotPages], _shakeMouth = new PointF[ShakePages];
    private readonly PointF _carryMouth, _pullMouth;
    private readonly Sprite[][] _hop = new Sprite[2][], _flee = new Sprite[2][];
    private readonly Sprite[] _rsit = new Sprite[2], _graze = new Sprite[2], _dead = new Sprite[2], _limp = new Sprite[2];
    private double _time;
    private float _nextBout, _boutClock, _playClock, _nextThreat;
    private bool _on;                                             // the pack is in the picture (or coming)
    private int _carcass = -1;                                    // the dead rabbit, or -1
    private float _carcassX, _carcassDepth, _tossT = -1f, _tossFromX, _tossToX;   // a tossed carcass flies for 0.6 s
    private int _carrier = -1;                                    // the coyote with the carcass in its mouth

    public Pack(ThanksgivingScenery s, int coyotes, int rabbits, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());         // its own dice
        _log = MindLog.Open();
        _stencil = s.OpenField;
        float u = s.U;
        _k = Math.Max(14f, u * 0.062f);
        _r = Math.Max(7f, u * 0.026f);
        _cw = (int)(2.2f * _k) + 4; _ch = (int)(1.35f * _k) + 4; _cox = (int)(1.0f * _k) + 2; _cby = _ch - 2;
        _rw = (int)(1.7f * _r) + 4; _rh = (int)(1.15f * _r) + 4; _rox = (int)(0.85f * _r) + 2; _rby = _rh - 2;
        _lw = (int)(0.9f * _r) + 4; _lh = (int)(1.3f * _r) + 4; _lox = _lw / 2; _lby = (int)(0.12f * _r) + 2;
        for (int d = 0; d < 2; d++)
        {
            bool m = d == 1;
            Sprite C(CoyoteArt.Pose q) => CoyoteArt.Page(q, _k, _cw, _ch, _cox, _cby, m);
            _trot[d] = new Sprite[TrotPages]; _gallop[d] = new Sprite[GallopPages]; _dig[d] = new Sprite[DigPages]; _shake[d] = new Sprite[ShakePages]; _pounce[d] = new Sprite[PouncePages]; _turn[d] = new Sprite[TurnPages];
            for (int i = 0; i < TrotPages; i++) { var q = CoyoteArt.Trot(i / (float)TrotPages, TrotStride); _trot[d][i] = C(q); _trotMouth[i] = CoyoteArt.MouthPoint(q); }
            for (int i = 0; i < GallopPages; i++) _gallop[d][i] = C(CoyoteArt.Gallop(i / (float)GallopPages, GallopStride));
            for (int i = 0; i < DigPages; i++) _dig[d][i] = C(CoyoteArt.Dig(i / (float)DigPages));
            for (int i = 0; i < ShakePages; i++) { var q = CoyoteArt.Shake(28f * MathF.Sin(MathF.Tau * i / ShakePages)); _shake[d][i] = C(q); _shakeMouth[i] = CoyoteArt.MouthPoint(q); }
            for (int i = 0; i < PouncePages; i++) _pounce[d][i] = C(CoyoteArt.Pounce((i + 0.5f) / PouncePages));
            for (int i = 0; i < TurnPages; i++) _turn[d][i] = Sprite.Paint(_cw, _ch, g => { if (m) { g.TranslateTransform(_cw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_cox, _cby); g.ScaleTransform(_k * Squash[i], -_k); CoyoteArt.Paint(g, CoyoteArt.Stand()); });
            _stand[d] = C(CoyoteArt.Stand()); _sit[d] = C(CoyoteArt.Sit());
            var carry = CoyoteArt.Carry(); _carry[d] = C(carry); _carryMouth = CoyoteArt.MouthPoint(carry);
            var pull = CoyoteArt.Pull(); _pull[d] = C(pull); _pullMouth = CoyoteArt.MouthPoint(pull);
            Sprite R(RabbitArt.Pose q) => Sprite.Paint(_rw, _rh, g => { if (m) { g.TranslateTransform(_rw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_rox, _rby); g.ScaleTransform(_r, -_r); RabbitArt.Paint(g, q); });
            _hop[d] = new Sprite[HopPages]; _flee[d] = new Sprite[HopPages];
            for (int i = 0; i < HopPages; i++) { _hop[d][i] = R(RabbitArt.Hop(i / (float)HopPages)); _flee[d][i] = R(RabbitArt.Hop(i / (float)HopPages, true)); }
            _rsit[d] = R(RabbitArt.Sit()); _graze[d] = R(RabbitArt.Graze()); _dead[d] = R(RabbitArt.Dead());
            _limp[d] = Sprite.Paint(_lw, _lh, g => { if (m) { g.TranslateTransform(_lw, 0); g.ScaleTransform(-1, 1); } g.TranslateTransform(_lox, _lby); g.ScaleTransform(_r, -_r); RabbitArt.Paint(g, RabbitArt.Limp()); });
        }

        float[] bold = Traits.Deal(_rng, Math.Max(1, coyotes)), timid = Traits.Deal(_rng, Math.Max(1, rabbits));
        _pack = new Coyote[coyotes];
        for (int i = 0; i < coyotes; i++)
            _pack[i] = new Coyote { X = -10 * _k, Depth = 0.02f + 0.09f * (float)_rng.NextDouble(), Bold = bold[i], Pace = new Pace(Gallop * u * 2.5f) };
        _rabbits = new Rabbit[rabbits];
        for (int i = 0; i < rabbits; i++)
        {
            var r = new Rabbit { X = s.Width * (0.05f + 0.9f * (float)_rng.NextDouble()), Depth = 0.01f + 0.11f * (float)_rng.NextDouble(), Dir = _rng.Next(2) == 0 ? 1 : -1, Timid = timid[i], Pace = new Pace(RabbitFlee * u * 4f) };
            r.Mind.Begin(RDoing.Sit, 1f + 4f * (float)_rng.NextDouble());
            _rabbits[i] = r;
        }
        _nextBout = _test is "now" or "catch" ? 5f : 40f + 60f * (float)_rng.NextDouble();   // the first bout comes sooner than the rest
    }

    public int Coyotes => _pack.Length;

    private float FeetY(float x, float depth) => _s.FieldY(x) + (_s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(x)) * depth;
    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} pack {what}");
    private bool OnScreen(float x, float half) => x > -half && x < _s.Width + half;

    // ================================================================ living

    public void Update(float dt)
    {
        float u = _s.U, w = _s.Width;
        _time += dt;
        if (_pack.Length > 0)
        {
            if (!_on)
            {
                _nextBout -= dt;
                if (_nextBout <= 0f) BeginBout();
            }
            else
            {
                _boutClock += dt;
                _nextThreat -= dt;
                bool anyPlaying = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing is Doing.Play or Doing.Shake) anyPlaying = true;
                if (_nextThreat <= 0f)
                {
                    // The pack is a threat to everything that can feel it: pinned at each coyote in the picture.
                    foreach (Coyote c in _pack)
                        if (OnScreen(c.X, 0f) && c.Mind.Doing != Doing.Away) _board.Post(Sense.Threat, new PointF(c.X, FeetY(c.X, c.Depth)), 0.42f * u, 0.9f, c);
                    _nextThreat = 0.5f;
                }
                if (anyPlaying) _playClock += dt;
                // The bout ends: the pack leaves, with the carcass if there is one, when the play is done or the time is up.
                bool anyChasing = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing is Doing.Chase or Doing.Shake) anyChasing = true;
                if (!anyChasing && ((anyPlaying && _playClock >= PlayFor) || (!anyPlaying && _boutClock >= BoutFor)))
                    foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Leave && c.Mind.Doing != Doing.Away) BeginLeave(c);
                bool allAway = true;
                foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Away) allAway = false;
                if (allAway)
                {
                    _on = false;
                    _nextBout = Organic.Jitter(_rng, BoutEvery);
                    if (_test is "now" or "catch") _nextBout = 20f;
                    if (_carcass >= 0) { _carcass = -1; _carrier = -1; }
                    // The rabbits that ran come back one at a time over the next while, the dead one last of all, as a new rabbit.
                    for (int j = 0; j < _rabbits.Length; j++) if (_rabbits[j].Mind.Doing is RDoing.Gone or RDoing.Dead) _rabbits[j].Mind.Begin(RDoing.Gone, 8f + 40f * (float)_rng.NextDouble() + (_rabbits[j].Mind.Doing == RDoing.Dead ? 60f : 0f));
                    Log($"away, next bout in {_nextBout:F0} s");
                }
            }
            for (int i = 0; i < _pack.Length; i++) StepCoyote(i, dt);
        }
        for (int i = 0; i < _rabbits.Length; i++) StepRabbit(i, dt);
        if (_tossT >= 0f) { _tossT += dt; if (_tossT >= 0.6f) { _tossT = -1f; _carcassX = _tossToX; } }
    }

    private void BeginBout()
    {
        _on = true; _boutClock = 0; _playClock = 0;
        bool fromLeft = _rng.Next(2) == 0;
        for (int i = 0; i < _pack.Length; i++)
        {
            Coyote c = _pack[i];
            c.Dir = fromLeft ? 1 : -1;
            c.X = fromLeft ? -_k * (1.2f + 1.3f * i) : _s.Width + _k * (1.2f + 1.3f * i);
            c.Depth = 0.02f + 0.09f * (float)_rng.NextDouble();
            c.Goal = _s.Width * (0.15f + 0.7f * (float)_rng.NextDouble()); c.GoalDepth = c.Depth;
            c.Mind.Begin(Doing.Roam, 30f); c.Clock = 0; c.Pace.Want = Trot * _s.U; c.Act = Act.None; c.Carrying = false;
        }
        Log($"bout: in from the {(fromLeft ? "left" : "right")}");
    }

    private void BeginLeave(Coyote c)
    {
        bool left = c.X < _s.Width / 2f;
        c.Goal = left ? -2.5f * _k : _s.Width + 2.5f * _k; c.GoalDepth = c.Depth;
        c.Mind.Begin(Doing.Leave, 60f); c.Clock = 0; c.Pace.Want = Trot * _s.U; c.Act = Act.None;
        if (_carcass >= 0 && _carrier < 0 && _tossT < 0f) { _carrier = Array.IndexOf(_pack, c); c.Carrying = true; }   // the first to leave takes it
        Log($"coyote{Array.IndexOf(_pack, c)} leaves {(left ? "left" : "right")}{(c.Carrying ? " with the carcass" : "")}");
    }

    private void StartTurn(Coyote c, int dir) { if (dir == c.Dir || c.Turning) return; c.Turning = true; c.TurnT = 0; }

    private void StepCoyote(int i, float dt)
    {
        Coyote c = _pack[i];
        float u = _s.U, w = _s.Width;
        c.Mind.Tick(dt);
        c.Clock += dt;
        _log?.WriteLine($"{_time,8:F3} coyote{i} frame x {c.X:F1} y {FeetY(c.X, c.Depth):F1} v {c.Pace.Speed:F2} {c.Mind.Doing}{(c.Act != Act.None ? "/" + c.Act : "")}");
        if (c.Mind.Doing == Doing.Away) return;
        if (c.Turning)
        {
            float was = c.TurnT; c.TurnT += dt;
            if (was < TurnFor / 2 && c.TurnT >= TurnFor / 2) c.Dir = -c.Dir;
            if (c.TurnT >= TurnFor) c.Turning = false;
            return;
        }
        // Moving toward a goal: the pace eases, the body follows the heading along the lane, the depth drifts toward its goal.
        void Travel(float speed)
        {
            int toward = c.Goal >= c.X ? 1 : -1;
            if (toward != c.Dir && MathF.Abs(c.Goal - c.X) > 0.3f * _k) { StartTurn(c, toward); return; }
            c.Pace.Want = speed;
            float step = c.Pace.Tick(dt);
            c.X += c.Dir * step;
            c.Depth += Math.Clamp(c.GoalDepth - c.Depth, -0.04f * dt, 0.04f * dt);
        }
        bool Arrived() => MathF.Abs(c.Goal - c.X) < 0.25f * _k;
        switch (c.Mind.Doing)
        {
            case Doing.Roam:
                Travel(Trot * u);
                if (Arrived() || c.Mind.Over) { c.Pace.Halt(); RollIdle(c, i); }
                else TryChase(c, i);
                break;
            case Doing.Stand:
            case Doing.Sit:
            case Doing.Dig:
                if (c.Mind.Over) RollIdle(c, i);
                else if (c.Mind.Doing == Doing.Stand) TryChase(c, i);
                break;
            case Doing.Chase:
            {
                Rabbit r = c.Target >= 0 ? _rabbits[c.Target] : null!;
                if (r == null || r.Mind.Doing is RDoing.Gone or RDoing.Dead || c.Mind.Over || !OnScreen(c.X, -0.5f * _k))
                {
                    // Lost it, or gave up: stand and pant.
                    c.Target = -1; c.Pace.Halt(); c.Mind.Begin(Doing.Stand, 2.5f); c.Clock = 0; Log($"coyote{i} gives up");
                    break;
                }
                c.Goal = r.X; c.GoalDepth = r.Depth;
                Travel(Gallop * u);
                if (r.Mind.Doing != RDoing.Flee) BeginFlee(r, c.Target, c);
                float gap = (r.X - c.X) * c.Dir;                                              // ahead of the nose (positive) or behind
                if (c.Doomed && gap < 0.5f * _k && gap > -0.3f * _k && MathF.Abs(r.Depth - c.Depth) < 0.06f)
                {
                    // CAUGHT. The rabbit dies where it is; the catcher stops with it in its mouth and shakes it.
                    r.Mind.Begin(RDoing.Dead, 1f); r.Pace.Halt();
                    _carcass = c.Target; _carcassX = r.X; _carcassDepth = r.Depth; _carrier = i; c.Carrying = true;
                    c.Target = -1; c.Pace.Halt(); c.Mind.Begin(Doing.Shake, 3f); c.Clock = 0; _playClock = 0;
                    c.X = r.X - c.Dir * _carryMouth.X * _k; c.Depth = r.Depth;
                    Log($"coyote{i} CAUGHT rabbit{_carcass} at {r.X:F0}");
                    foreach (Coyote o in _pack) if (o != c && o.Mind.Doing != Doing.Away) { o.Mind.Begin(Doing.Play, PlayFor + 5f); o.Clock = 0; o.Act = Act.None; o.Goal = r.X + (o.X < r.X ? -1 : 1) * (0.8f + 0.6f * (float)_rng.NextDouble()) * _k; o.GoalDepth = r.Depth + 0.02f * (float)(_rng.NextDouble() - 0.5); }
                }
                break;
            }
            case Doing.Shake:
                if (c.Mind.Over)
                {
                    // Drops it at its feet and the play begins.
                    c.Carrying = false; _carrier = -1; _carcassX = c.X + c.Dir * _carryMouth.X * _k; _carcassDepth = c.Depth;
                    c.Mind.Begin(Doing.Play, PlayFor + 5f); c.Clock = 0; c.Act = Act.None;
                    Log($"coyote{i} drops it at {_carcassX:F0}; the play begins");
                }
                break;
            case Doing.Play:
                StepPlay(c, i, dt);
                break;
            case Doing.Leave:
                Travel(Trot * u);
                if (!OnScreen(c.X, 1.5f * _k) || c.Mind.Over) { c.Pace.Halt(); c.Mind.Begin(Doing.Away, 1f); if (c.Carrying) { c.Carrying = false; } }
                break;
        }
    }

    private void RollIdle(Coyote c, int i)
    {
        // What next: roam on, stand, dig, sit. A bold one roams more.
        int face = Dice.Pick(_rng, [0.35f + 0.3f * c.Bold, 0.25f, 0.18f, 0.15f]);
        switch (face)
        {
            case 0:
                c.Goal = _s.Width * (0.06f + 0.88f * (float)_rng.NextDouble()); c.GoalDepth = 0.02f + 0.09f * (float)_rng.NextDouble();
                c.Mind.Begin(Doing.Roam, 25f); break;
            case 1: c.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 2.2f)); break;
            case 2: c.Mind.Begin(Doing.Dig, 3f + 3f * (float)_rng.NextDouble()); break;
            default: c.Mind.Begin(Doing.Sit, 3f + 4f * (float)_rng.NextDouble()); break;
        }
        c.Clock = 0;
        Log($"coyote{i} {c.Mind.Doing}");
    }

    private void TryChase(Coyote c, int i)
    {
        if (_carcass >= 0) return;                                                           // one kill a bout
        for (int j = 0; j < _rabbits.Length; j++)
        {
            Rabbit r = _rabbits[j];
            if (r.Mind.Doing is RDoing.Gone or RDoing.Dead) continue;
            if (MathF.Abs(r.X - c.X) > 0.4f * _s.U) continue;
            // Once a second, the dice: a bold coyote goes more often. In the catch test it always goes.
            if (_test != "catch" && _rng.NextDouble() > (0.25 + 0.5 * c.Bold) / 60.0) continue;
            c.Target = j; c.Doomed = _test == "catch" || _rng.NextDouble() < 0.35;
            c.Mind.Begin(Doing.Chase, ChaseFor + 2f * (float)_rng.NextDouble()); c.Clock = 0;
            BeginFlee(r, j, c);
            Log($"coyote{i} chases rabbit{j}{(c.Doomed ? " (doomed)" : "")}");
            return;
        }
    }

    // ---- the play: the pack with the carcass for a minute ----
    private void StepPlay(Coyote c, int i, float dt)
    {
        float u = _s.U;
        float near = MathF.Abs(c.X - _carcassX);
        if (c.Act == Act.None)
        {
            // Come over if far; at hand, pick an act (a Tug needs a partner at hand too); otherwise watch a moment.
            if (near > 1.1f * _k && !c.Carrying)
            {
                c.Goal = _carcassX + (c.X < _carcassX ? -1 : 1) * 0.7f * _k; c.GoalDepth = _carcassDepth + 0.015f * (float)(_rng.NextDouble() - 0.5);
                int toward = c.Goal >= c.X ? 1 : -1;
                if (toward != c.Dir) { StartTurn(c, toward); return; }
                c.Pace.Want = Trot * u; c.X += c.Dir * c.Pace.Tick(dt); c.Depth += Math.Clamp(c.GoalDepth - c.Depth, -0.04f * dt, 0.04f * dt);
                return;
            }
            c.Pace.Halt();
            if (_carrier >= 0 && _carrier != i) { c.Act = Act.Watch; c.ActT = 1f + (float)_rng.NextDouble(); return; }   // someone has it
            int facing = _carcassX >= c.X ? 1 : -1;
            if (facing != c.Dir) { StartTurn(c, facing); return; }
            if (_tossT >= 0f) { c.Act = Act.Watch; c.ActT = 0.8f; return; }
            bool partner = false;
            foreach (Coyote o in _pack) if (o != c && o.Mind.Doing == Doing.Play && MathF.Abs(o.X - _carcassX) < 1.5f * _k && (o.X - _carcassX) * (c.X - _carcassX) < 0 && o.Act is Act.None or Act.Watch) partner = true;
            int face = Dice.Pick(_rng, [0.35f, 0.3f, partner ? 0.3f : 0f, 0.2f, 0.3f]);
            c.Act = face switch { 0 => Act.Pounce, 1 => Act.Toss, 2 => Act.Tug, 3 => Act.Carry, _ => Act.Watch };
            c.ActT = c.Act switch { Act.Pounce => 1.0f, Act.Toss => 1.6f, Act.Tug => 2.2f, Act.Carry => 2.8f, _ => 1f + 2f * (float)_rng.NextDouble() };
            c.Clock = 0;
            if (c.Act == Act.Tug)
                foreach (Coyote o in _pack) if (o != c && o.Mind.Doing == Doing.Play && MathF.Abs(o.X - _carcassX) < 1.5f * _k && (o.X - _carcassX) * (c.X - _carcassX) < 0) { o.Act = Act.Tug; o.ActT = 2.2f; o.Clock = 0; break; }
            if (c.Act == Act.Pounce) { c.X = _carcassX - c.Dir * 0.55f * _k; }
            Log($"coyote{i} {c.Act}");
            return;
        }
        c.ActT -= dt;
        switch (c.Act)
        {
            case Act.Toss:
                // Picks it up over the first half, flings it over the second: the carcass flies a short arc the way it faces.
                if (c.Clock < 0.6f) { c.X = _carcassX - c.Dir * _carryMouth.X * _k; }
                else if (_carrier < 0 && c.Clock >= 0.6f && c.Clock < 0.9f) { _carrier = i; c.Carrying = true; }
                else if (c.Carrying && c.Clock >= 0.9f) { c.Carrying = false; _carrier = -1; _tossT = 0f; _tossFromX = c.X + c.Dir * _carryMouth.X * _k; _tossToX = _tossFromX + c.Dir * (0.5f + 0.4f * (float)_rng.NextDouble()) * _k; _carcassX = _tossFromX; }
                break;
            case Act.Carry:
                if (c.Clock < 0.4f) c.X = _carcassX - c.Dir * _carryMouth.X * _k;
                else if (!c.Carrying && _carrier < 0 && c.Clock < 0.6f) { _carrier = i; c.Carrying = true; }
                else if (c.Carrying && c.Clock < 2.2f) { c.Pace.Want = Trot * 0.7f * u; c.X += c.Dir * c.Pace.Tick(dt); _carcassX = c.X + c.Dir * _trotMouth[0].X * _k; _carcassDepth = c.Depth; }
                else if (c.Carrying) { c.Carrying = false; _carrier = -1; c.Pace.Halt(); _carcassX = c.X + c.Dir * _carryMouth.X * _k; }
                break;
            case Act.Tug:
                c.X = _carcassX - c.Dir * (_pullMouth.X + 0.08f * MathF.Sin(c.Clock * 9f)) * _k;      // braced at it, heaving
                break;
        }
        if (c.ActT <= 0f) { c.Act = Act.None; c.Clock = 0; }
    }

    // ---- the rabbits ----
    private void BeginFlee(Rabbit r, int j, Coyote from)
    {
        r.Dir = r.X >= from.X ? 1 : -1;
        r.Goal = r.Dir > 0 ? _s.Width + 2f * _r : -2f * _r;                                 // off the edge away from it
        r.GoalDepth = Math.Clamp(r.Depth + 0.04f * (float)(_rng.NextDouble() - 0.5), 0.005f, MaxDepth);
        r.Mind.Begin(RDoing.Flee, 20f); r.Clock = 0; r.CalmFor = 0;
        Log($"rabbit{j} flees {(r.Dir > 0 ? "right" : "left")}");
    }

    private void StepRabbit(int j, float dt)
    {
        Rabbit r = _rabbits[j];
        float u = _s.U, w = _s.Width;
        r.Mind.Tick(dt);
        r.Clock += dt;
        _log?.WriteLine($"{_time,8:F3} rabbit{j} frame x {r.X:F1} y {FeetY(r.X, r.Depth):F1} v {r.Pace.Speed:F2} {r.Mind.Doing}");
        float nearest = float.MaxValue;
        foreach (Coyote c in _pack) if (c.Mind.Doing != Doing.Away) nearest = MathF.Min(nearest, MathF.Abs(c.X - r.X));
        switch (r.Mind.Doing)
        {
            case RDoing.Sit:
            case RDoing.Graze:
                if (nearest < 0.22f * u) { Coyote near = _pack[0]; foreach (Coyote c in _pack) if (MathF.Abs(c.X - r.X) <= nearest) near = c; BeginFlee(r, j, near); break; }
                if (r.Mind.Over)
                {
                    int face = Dice.Pick(_rng, [0.45f, 0.3f, 0.25f + 0.2f * (1 - r.Timid)]);
                    if (face == 2) { r.Goal = Math.Clamp(r.X + (0.06f + 0.14f * (float)_rng.NextDouble()) * u * (_rng.Next(2) == 0 ? 1 : -1), 0.03f * w, 0.97f * w); r.GoalDepth = 0.01f + 0.11f * (float)_rng.NextDouble(); r.Dir = r.Goal >= r.X ? 1 : -1; r.Mind.Begin(RDoing.Hop, 10f); r.Hops = 0; }
                    else r.Mind.Begin(face == 0 ? RDoing.Graze : RDoing.Sit, 2f + 5f * (float)_rng.NextDouble());
                    r.Clock = 0;
                }
                break;
            case RDoing.Hop:
            {
                if (nearest < 0.22f * u) { Coyote near = _pack[0]; foreach (Coyote c in _pack) if (MathF.Abs(c.X - r.X) <= nearest) near = c; BeginFlee(r, j, near); break; }
                r.Pace.Want = RabbitHop * u;
                float step = r.Pace.Tick(dt);
                r.X += r.Dir * step; r.Hops += step / (HopLen * _r);
                r.Depth += Math.Clamp(r.GoalDepth - r.Depth, -0.03f * dt, 0.03f * dt);
                if (MathF.Abs(r.Goal - r.X) < 0.3f * _r || r.Mind.Over) { r.Pace.Halt(); r.Mind.Begin(RDoing.Sit, 1f + 3f * (float)_rng.NextDouble()); r.Clock = 0; }
                break;
            }
            case RDoing.Flee:
            {
                bool doomed = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Chase && c.Target == j && c.Doomed) doomed = true;
                r.Pace.Want = (doomed ? RabbitDoomed : RabbitFlee) * u;
                float step = r.Pace.Tick(dt);
                r.X += r.Dir * step; r.Hops += step / (FleeHopLen * _r);
                // The jink: the goal depth wanders, so the line of flight weaves across the lane.
                if (_rng.NextDouble() < dt * 1.5) r.GoalDepth = Math.Clamp(r.Depth + 0.05f * (float)(_rng.NextDouble() - 0.5), 0.005f, MaxDepth);
                r.Depth += Math.Clamp(r.GoalDepth - r.Depth, -0.06f * dt, 0.06f * dt);
                if (!OnScreen(r.X, -1.5f * _r)) { r.Pace.Halt(); r.Mind.Begin(RDoing.Gone, 25f + 50f * (float)_rng.NextDouble()); Log($"rabbit{j} off the edge"); break; }
                bool chased = false;
                foreach (Coyote c in _pack) if (c.Mind.Doing == Doing.Chase && c.Target == j) chased = true;
                r.CalmFor = nearest > 0.40f * u && !chased ? r.CalmFor + dt : 0f;
                if (r.CalmFor > 1.5f) { r.Pace.Halt(); r.Mind.Begin(RDoing.Sit, 2f); r.Clock = 0; Log($"rabbit{j} stops, safe"); }
                break;
            }
            case RDoing.Dead:
                break;
            case RDoing.Gone:
                if (r.Mind.Over && !_on)
                {
                    // Hops back in from an edge.
                    r.Dir = _rng.Next(2) == 0 ? 1 : -1;
                    r.X = r.Dir > 0 ? -1.5f * _r : w + 1.5f * _r;
                    r.Depth = 0.01f + 0.11f * (float)_rng.NextDouble(); r.GoalDepth = r.Depth;
                    r.Goal = r.Dir > 0 ? w * (0.08f + 0.3f * (float)_rng.NextDouble()) : w * (0.62f + 0.3f * (float)_rng.NextDouble());
                    r.Mind.Begin(RDoing.Hop, 30f); r.Hops = 0; r.Clock = 0;
                    Log($"rabbit{j} hops in from the {(r.Dir > 0 ? "left" : "right")}");
                }
                break;
        }
    }

    // ================================================================ drawing

    /// <summary>Everything on the lane, far to near: the carcass, the rabbits and the coyotes by their depth.</summary>
    public void Draw(FrameBuffer fb)
    {
        var order = new List<(float Depth, int Kind, int Index)>();
        for (int i = 0; i < _rabbits.Length; i++) if (_rabbits[i].Mind.Doing is not RDoing.Gone and not RDoing.Dead) order.Add((_rabbits[i].Depth, 0, i));
        for (int i = 0; i < _pack.Length; i++) if (_pack[i].Mind.Doing != Doing.Away) order.Add((_pack[i].Depth, 1, i));
        if (_carcass >= 0 && _carrier < 0) order.Add((_carcassDepth, 2, _carcass));
        order.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, kind, idx) in order)
        {
            if (kind == 0) DrawRabbit(fb, _rabbits[idx]);
            else if (kind == 1) DrawCoyote(fb, _pack[idx]);
            else DrawCarcass(fb);
        }
    }

    private void DrawCarcass(FrameBuffer fb)
    {
        float x = _carcassX, y = FeetY(_carcassX, _carcassDepth);
        if (_tossT >= 0f)
        {
            float p = _tossT / 0.6f;
            x = _tossFromX + (_tossToX - _tossFromX) * p;
            y = FeetY(x, _carcassDepth) - 0.9f * _r * 4 * p * (1 - p) * 1.4f;
        }
        int d = _tossToX >= _tossFromX ? 0 : 1;
        _dead[d].Draw(fb, (int)MathF.Round(x) - (d == 0 ? _rox : _rw - _rox), (int)MathF.Round(y) - _rby, 1f, _stencil);
    }

    private void DrawRabbit(FrameBuffer fb, Rabbit r)
    {
        int d = r.Dir > 0 ? 0 : 1;
        Sprite page = r.Mind.Doing switch
        {
            RDoing.Hop => _hop[d][(int)((r.Hops - MathF.Floor(r.Hops)) * HopPages) % HopPages],
            RDoing.Flee => _flee[d][(int)((r.Hops - MathF.Floor(r.Hops)) * HopPages) % HopPages],
            RDoing.Graze => ((int)(r.Clock * 2) % 3 == 2) ? _rsit[d] : _graze[d],          // lifts its head now and then
            _ => _rsit[d],
        };
        page.Draw(fb, (int)MathF.Round(r.X) - (d == 0 ? _rox : _rw - _rox), (int)MathF.Round(FeetY(r.X, r.Depth)) - _rby, 1f, _stencil);
    }

    private void DrawCoyote(FrameBuffer fb, Coyote c)
    {
        int d = c.Dir > 0 ? 0 : 1;
        float y = FeetY(c.X, c.Depth);
        Sprite page; PointF mouth = _carryMouth;
        if (c.Turning)
        {
            float q = c.TurnT / TurnFor;
            page = _turn[d][q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages))];
        }
        else if (!c.Pace.Still && c.Mind.Doing == Doing.Chase) page = _gallop[d][c.Pace.Page(GallopStride * _k, GallopPages)];
        else if (!c.Pace.Still) { int pg = c.Pace.Page(TrotStride * _k, TrotPages); page = _trot[d][pg]; mouth = _trotMouth[pg]; }
        else switch (c.Mind.Doing)
        {
            case Doing.Sit: page = _sit[d]; break;
            case Doing.Dig: page = _dig[d][(int)(c.Clock * 3.0f * DigPages) % DigPages]; break;
            case Doing.Shake: { int pg = (int)(c.Clock * 2.2f * ShakePages) % ShakePages; page = _shake[d][pg]; mouth = _shakeMouth[pg]; break; }
            case Doing.Play when c.Act == Act.Pounce: page = _pounce[d][Math.Min(PouncePages - 1, (int)(c.Clock / 1.0f * PouncePages))]; break;
            case Doing.Play when c.Act == Act.Tug: page = _pull[d]; mouth = _pullMouth; break;
            case Doing.Play when c.Carrying: page = _carry[d]; break;
            default: page = c.Carrying ? _carry[d] : _stand[d]; break;
        }
        page.Draw(fb, (int)MathF.Round(c.X) - (d == 0 ? _cox : _cw - _cox), (int)MathF.Round(y) - _cby, 1f, _stencil);
        if (c.Carrying)
        {
            // The rabbit hangs from its mouth: the limp sheet's origin is the grip.
            float mx = c.X + c.Dir * mouth.X * _k, my = y - mouth.Y * _k;
            _limp[d].Draw(fb, (int)MathF.Round(mx) - (d == 0 ? _lox : _lw - _lox), (int)MathF.Round(my) - _lby, 1f, _stencil);
        }
    }
}
