using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// A few wild turkeys that LIVE in the field behind the fence, the way the
/// bats live in Halloween's sky and Santa in Christmas's: always there,
/// pecking at the stubble, wandering a few steps, standing to look about,
/// and one tom now and then fanning his tail for the hens.
///
/// FEYNMAN VERSION: each bird has a MIND from the shared kit (Core/Minds,
/// the same one the pond's koi and the patch's mice use): two personality
/// dials rolled once (a timid bird freezes and hurries off more; a busy one
/// never stands still), ONE doing at a time with a timer, and weighted dice
/// for what to do next when the timer runs out. It has SENSES too (Senses.cs):
/// the scene pins what happens on a noticeboard (the tractor, the raccoon, a
/// scuffle, a deer walking through) and each bird's own attention turns that
/// into a reaction by its dials: look that way, freeze with the head up, or
/// hurry off a few quick steps. One bird's hurry pins a notice of its own, so
/// the alarm spreads through the flock.
///
/// It MOVES like a bird, not a toy (Mover.cs): a walk leans in over a few
/// steps, slows into a stop so the stride ends on the page where both feet
/// are on the ground, and a turn comes round through three narrowing pages
/// with the facing flipped in the middle, never a mirror flip. The walk is
/// paged by the distance walked, so a planted foot stays planted. Pecking is
/// a run of STRIKES: the whole bird pitches forward in four frames, holds
/// with the beak on the ground, pulls back a little, strikes again two to
/// four times, then lifts over ten frames. The display opens the fan over a
/// second, holds it quivering, and folds it. The pictures are the Turkey
/// happening's own flip-book (Turkey.PaintPage), painted here in two sizes:
/// birds deeper in the field are a little smaller.
///
/// They are stamped through the painter's OpenField stencil, so the fence,
/// the near ground, the scarecrow and the maples hide them where they
/// should, and a bird walking behind the scarecrow's legs disappears and
/// comes out the other side.
///
/// THE FACE-OFF (2026-11-09: "make them a little more special ... funnier"): there are TWO toms now, on
/// neighbouring rows, and when one begins his display with the other close by (within 0.28 U) and idle, the
/// other comes over, both stand beak to beak with the fans out and quivering, and the less bold one gives in:
/// he folds his fan, turns, and slinks off a few quick steps with his head low (the walk pages pitched forward,
/// a hunch) while the winner stays fanned a moment longer, then folds. Never two within 60 s. The face-off is
/// a script over two birds (the same idea as a Story: a bird is LENT to it); anything with a Freeze or a Flee
/// in it (a Threat, a Loud) ends it at once and the birds react as they would. It posts the louder Call
/// (weight 1.0) so the hens look over, and the curious ones walk over to watch.
/// Test aid: PECKWORKS_FACEOFF=now puts the toms three lengths apart and starts one at 2.5 s of scene time.
/// </summary>
internal sealed class TurkeyFlock
{
    private enum Doing { Walk, Stand, Peck, Display, Alert, Hurry, Approach, Square, Slink }   // Approach: walking over to something (a tom's call, a bale's loose hay) and doing what it came for

    private sealed class Bird
    {
        public float X, Depth;          // where along the field, and how far down it (0 the lane, 1 the near crest)
        public int Dir;                 // +1 faces right, -1 left
        public int Size;                // 0 far (smaller), 1 near
        public bool Tom;                // the one who displays
        public float Timid, Busy;       // the dials
        public Mind<Doing> Mind = new(Doing.Stand);
        public Attention Senses = null!;
        public Pace Pace = null!;       // the walk's speed, eased, and the odometer the stride is paged by
        public float Clock;             // seconds into this doing
        public int Strikes;             // this peck's number of strikes
        public float Hold;              // seconds the beak stays on the ground each strike
        public bool Turning;            // coming round to face the other way through the turn pages (never a mirror flip)
        public float TurnT;             // seconds into the turn; Dir flips at the middle
        public float Restless;          // seed for the slow drift of a standing bird's head
        public float Goal;              // an approach's x: where it is walking to
        public bool GoalFood;           // and whether there is something to peck at when it gets there
        public float FoldAt, CloseFor;  // a face-off: the second the fan starts to fold, and how long the folding takes
        public float SlinkT;            // seconds into the loser's retreat
        public bool Slunk;              // the retreat has set off (after its turn)
    }

    // 72 walk pages: at 24 the held head stepped 1.9 px a page under a body moving 1 px a frame, and buzzed.
    private const int WalkPages = 72, PeckPages = 12, FanPages = 8, QuiverPages = 4, TurnPages = 3;
    private const float Speed = 0.03f;              // in U per second: a slow stroll, about two steps a second
    private const float HurrySpeed = 0.055f;        // a startled bird's quick steps
    private const float StrikeFor = 4f / 60, PullFor = 2f / 60, LiftFor = 11f / 60;
    private const float FanOpen = 0.9f, FanHold = 3.2f;
    private const float TurnFor = 0.2f;             // a turn on the spot: three narrowing pages facing the old way, three widening facing the new
    private static readonly float[] Squash = [0.72f, 0.42f, 0.14f];

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly Bird[] _birds;
    private readonly bool _turnTest = Environment.GetEnvironmentVariable("PECKWORKS_TURKEY") == "turn";
    private readonly bool _faceTest = Environment.GetEnvironmentVariable("PECKWORKS_FACEOFF") == "now";
    private bool _faceTestDone;
    private int _tomWalks;
    // The face-off script: phase 0 none, 1 the comer walks over while the stander watches, 2 beak to beak, fanned.
    private int _foPhase, _foA = -1, _foB = -1, _foLoser, _foSide;
    private float _foT;
    private double _nextFace = 20;                      // never two within 60 s; the first not in the opening twenty seconds
    private readonly StreamWriter? _log;
    private double _time;
    private readonly float[] _k = new float[2];
    private readonly int[] _w = new int[2], _h = new int[2], _ox = new int[2], _by = new int[2];
    private readonly Sprite[][][] _walk = new Sprite[2][][];    // [size][dir][page]
    private readonly Sprite[][][] _peck = new Sprite[2][][];
    private readonly Sprite[][][] _fan = new Sprite[2][][];
    private readonly Sprite[][][] _quiver = new Sprite[2][][];
    private readonly Sprite[][][] _turn = new Sprite[2][][];     // [size][dir][page]: the standing bird narrowed about its feet
    private readonly Sprite[][][][] _slink = new Sprite[2][][][]; // [size][dir][set][page]: the walk with the body pitched and the head low (set 0 a half hunch, set 1 the full one)
    private readonly Sprite[][] _alert = new Sprite[2][];        // [size][dir]: standing with the head up and the neck stretched

    public TurkeyFlock(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());             // its own dice, so adding a bird never reshuffles the rest of the scene
        _log = MindLog.Open();
        float u = s.U;
        // Sized by the field's one scale (ThanksgivingScenery.MetresToU): a wild turkey is about 1.1 m beak to tail; the far
        // class stands at depth 0.45, the near at 0.8. (At 0.060 and 0.074 U they were as long as the tractor.)
        _k[0] = Math.Max(8f, u * s.MetresToU(0.45f) * 1.1f);
        _k[1] = Math.Max(8f, u * s.MetresToU(0.80f) * 1.1f);
        for (int z = 0; z < 2; z++)
        {
            _w[z] = (int)(1.45f * _k[z]) + 4;
            _ox[z] = (int)(0.80f * _k[z]) + 2;
            _h[z] = (int)(1.08f * _k[z]) + 8;
            _by[z] = _h[z] - 5;
            _walk[z] = new Sprite[2][]; _peck[z] = new Sprite[2][]; _fan[z] = new Sprite[2][]; _quiver[z] = new Sprite[2][]; _turn[z] = new Sprite[2][]; _alert[z] = new Sprite[2]; _slink[z] = new Sprite[2][][];
            for (int d = 0; d < 2; d++)
            {
                bool mirror = d == 1;
                _walk[z][d] = new Sprite[WalkPages];
                for (int i = 0; i < WalkPages; i++)
                    _walk[z][d][i] = Page(z, mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f, 0f);
                // The standing pages (a peck, the display, a turn, the alert) keep the feet where the walk's rest page (page 0)
                // leaves them, so nothing snaps when a bird that has just stopped starts to peck (the feet used to jump together).
                _peck[z][d] = new Sprite[PeckPages];
                for (int i = 0; i < PeckPages; i++)
                    _peck[z][d][i] = Page(z, mirror, 0f, 0f, 0f, 0f, 0f, (i + 1) / (float)PeckPages, legs: 1f);
                // The slink: the walk's own pages with the body pitched forward and the neck reaching down (the peck dial at a
                // third and at two fifths), the head low and the back hunched: a bird that has lost an argument. Set 0 is used for
                // the first quarter second and the last stride (so the hunch eases in and out), set 1 between.
                _slink[z][d] = [new Sprite[WalkPages], new Sprite[WalkPages]];
                for (int i = 0; i < WalkPages; i++)
                {
                    _slink[z][d][0][i] = Page(z, mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f, 0.16f);
                    _slink[z][d][1][i] = Page(z, mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f, 0.36f);
                }
                _fan[z][d] = new Sprite[FanPages];
                for (int i = 0; i < FanPages; i++)
                    _fan[z][d][i] = Page(z, mirror, 0f, 0f, (i + 1) / (float)(FanPages + 1), 0f, 0f, 0f, legs: 1f);
                _quiver[z][d] = new Sprite[QuiverPages];
                float[] shake = [0f, 1.2f, 0f, -1.2f];
                for (int i = 0; i < QuiverPages; i++)
                    _quiver[z][d][i] = Page(z, mirror, 0f, 0f, 1f, 0f, shake[i], 0f, legs: 1f);
                _turn[z][d] = new Sprite[TurnPages];
                for (int i = 0; i < TurnPages; i++)
                    _turn[z][d][i] = Page(z, mirror, 0f, 0f, 0f, 0f, 0f, 0f, legs: 1f, squash: Squash[i]);
                _alert[z][d] = Page(z, mirror, 0f, 0f, 0f, 0f, 0f, -0.18f, legs: 1f);      // a negative peck: the neck stretched up, the head high
            }
        }

        _birds = new Bird[count];
        float[] timid = Traits.Deal(_rng, Math.Max(1, count)), busy = Traits.Deal(_rng, Math.Max(1, count)), curious = Traits.Deal(_rng, Math.Max(1, count));
        for (int i = 0; i < count; i++)
        {
            float depth = 0.30f + 0.65f * (float)_rng.NextDouble();
            var b = new Bird
            {
                X = s.Width * (0.06f + 0.88f * (float)_rng.NextDouble()),
                Depth = depth,
                Size = depth < 0.62f ? 0 : 1,
                Dir = _rng.Next(2) == 0 ? 1 : -1,
                Tom = (i == 0 && count >= 2) || (i == 1 && count >= 3),   // the first bird is the tom when there is anyone to show off to; with three or more a second tom (the rival)
                Timid = timid[i], Busy = busy[i],
                Restless = (float)_rng.NextDouble() * 10,
            };
            if (i == 1 && b.Tom)
            {
                // The rival feeds on the first tom's row (a hair nearer or farther), so two side-view birds can stand beak to beak.
                Bird t0 = _birds[0];
                b.Depth = Math.Clamp(t0.Depth + (t0.Depth > 0.62f ? -0.03f : 0.03f), 0.30f, 0.95f);
                b.Size = t0.Size;
            }
            b.Senses = new Attention(timid[i], curious[i]) { Keen = 1.1f, Hungry = 0.5f + 0.5f * busy[i] };   // a turkey is always ready to eat: loose hay from the baler draws it over
            b.Pace = new Pace(Speed * u * 3f);              // leans into a walk over about a third of a second
            b.Mind.Begin(Doing.Stand, 1f + 3f * (float)_rng.NextDouble());
            _birds[i] = b;
        }
    }

    private Sprite Page(int z, bool mirror, float phase, float walk, float fan, float gobble, float shake, float peck, float legs = -1f, float squash = 1f) =>
        Turkey.PaintPage(_k[z], _w[z], _h[z], _ox[z], _by[z], mirror, phase, walk, fan, gobble, shake, peck, legs, squash);

    /// <summary>Where this bird's feet are on the field at x: between the lane and the near ground's crest, by its depth.</summary>
    private float FootY(Bird b) => _s.FieldY(b.X) + (_s.Ground.YAt(Math.Clamp(b.X, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(b.X)) * b.Depth;
    private float Stride(Bird b) => 4 * 0.11f * _k[b.Size];
    private static float PeckFor(Bird b) => b.Strikes * (StrikeFor + b.Hold + PullFor) - PullFor + LiftFor;

    /// <summary>Starts a turn to face "dir" (nothing if already facing it): the bird stands while the turn pages play, and Dir flips at the middle.</summary>
    private static void BeginTurn(Bird b, int dir)
    {
        if (dir == b.Dir || b.Turning) return;
        b.Turning = true; b.TurnT = 0;
    }

    public void Update(float dt)
    {
        float u = _s.U;
        _time += dt;
        if (_faceTest && !_faceTestDone && _time >= 2.5) StartFaceTest();
        if (_foPhase != 0) TickFaceoff(dt);
        for (int i = 0; i < _birds.Length; i++)
        {
            Bird b = _birds[i];
            b.Mind.Tick(dt);
            b.Senses.Tick(dt);
            b.Clock += dt;
            _log?.WriteLine($"{_time,8:F3} turkey{i} frame x {b.X:F1} v {b.Pace.Speed:F2} {b.Mind.Doing}");
            if (b.Turning)
            {
                // A turn on the spot: whatever it was doing waits (its clock and timer too, so a peck is not half spent when it begins).
                b.Clock -= dt; b.Mind.Left += dt;
                float was = b.TurnT;
                b.TurnT += dt;
                if (was < TurnFor / 2 && b.TurnT >= TurnFor / 2) b.Dir = -b.Dir;
                if (b.TurnT >= TurnFor) b.Turning = false;
                continue;
            }

            // ---- a bird in the face-off: only a Freeze or a Flee gets through to it (and ends the script); the body below still moves it ----
            bool inFace = _foPhase != 0 && (i == _foA || i == _foB);
            if (inFace)
            {
                Reaction fr = b.Senses.Check(_board, new PointF(b.X, FootY(b)), b, out Notice fcause);
                if (fr is Reaction.Freeze or Reaction.Flee) { AbortFaceoff(); React(b, fr, fcause); continue; }
            }
            // ---- the senses: what happened, and what this bird makes of it (never while already fleeing, or on its way to something) ----
            else if (b.Mind.Doing is not (Doing.Hurry or Doing.Approach))
            {
                Reaction r = b.Senses.Check(_board, new PointF(b.X, FootY(b)), b, out Notice cause);
                if (r != Reaction.None) React(b, r, cause);
            }

            // ---- the body: the walk's speed eases, the stride is paged by distance; a stop runs down onto the rest page whatever the mind does next ----
            float margin = _k[b.Size] * 0.9f;
            if (!b.Pace.Still || b.Pace.Want > 0f)
            {
                float moved = b.Pace.Tick(dt);
                b.X += b.Dir * moved;
                // At the edges of the field (a margin keeps the whole bird in the picture) the walk ends: stop square, then turn.
                if (((b.X < margin && b.Dir < 0) || (b.X > _s.Width - margin && b.Dir > 0)) && !b.Pace.Braking)
                {
                    b.X = Math.Clamp(b.X, margin, _s.Width - margin);
                    b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));
                    b.Mind.End();
                }
            }
            if (inFace) continue;                                   // TickFaceoff owns its doings from here
            if (b.Mind.Doing is Doing.Walk or Doing.Hurry or Doing.Approach)
            {
                // An approach ends when the bird is a length and a bit from what it came for (or its patience runs out).
                bool arrived = b.Mind.Doing == Doing.Approach && MathF.Abs(b.X - b.Goal) < 1.4f * _k[b.Size];
                if ((b.Mind.Over || arrived) && !b.Pace.Braking && !b.Pace.Still) b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));   // the timer ran out: ease onto the rest page
                if (b.Pace.Still)
                {
                    b.Clock = 0;
                    if (b.Mind.Doing == Doing.Approach && arrived && b.GoalFood)
                    {
                        // Here is the loose hay: a long burst of pecks.
                        b.Mind.Begin(Doing.Peck, 0f);
                        b.Strikes = 4 + _rng.Next(3);
                        b.Hold = (3 + _rng.Next(4)) / 60f;
                        b.Mind.Left = PeckFor(b) + Organic.Jitter(_rng, 0.5f);
                        Log(b, i, "arrived: peck");
                    }
                    else if (b.Mind.Doing == Doing.Approach && arrived)
                    {
                        // Come to see the tom: stand by him, head up, and watch.
                        b.Mind.Begin(Doing.Alert, 2f + 3f * b.Senses.Curious);
                        Log(b, i, "arrived: watch");
                    }
                    else
                    {
                        // Stopped square: stand a moment, turning round if the walk ended at an edge.
                        b.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 1.5f));
                        if (b.X <= margin + 0.5f) BeginTurn(b, 1); else if (b.X >= _s.Width - margin - 0.5f) BeginTurn(b, -1);
                    }
                }
                continue;
            }
            if (!b.Mind.Over || !b.Pace.Still) continue;

            // The timer ran out: roll the next doing. Pecking is the usual thing; a tom who has been quiet a while puts on
            // his show; a busy bird walks more and a timid one looks about more.
            b.Clock = 0;
            if (_turnTest) { b.Mind.Begin(Doing.Stand, 1.2f); BeginTurn(b, -b.Dir); continue; }   // PECKWORKS_TURKEY=turn: every bird turns at every roll, for watching the turn pages
            Bird? rival = b.Tom ? Rival(b) : null;
            bool rivalNear = rival != null && MathF.Abs(rival.X - b.X) <= 0.28f * u && _time >= _nextFace;
            int face = Dice.Pick(_rng, [b.Tom && b.Mind.Doing != Doing.Display ? (rivalNear ? 0.3f : 0.12f) : 0f, 0.38f, 0.22f + 0.2f * b.Timid, 0.28f + 0.3f * b.Busy]);
            switch (face)
            {
                case 0:
                    b.Mind.Begin(Doing.Display, FanOpen + FanHold + FanOpen);
                    _board.Post(Sense.Call, new PointF(b.X, FootY(b)), 0.6f * u, 0.9f, b);          // the hens look over, and the curious ones come to watch
                    if (b.Tom) TryFaceoff(i);                                                       // a rival close by and idle: he comes to answer it
                    break;
                case 1:
                    b.Mind.Begin(Doing.Peck, 0f);
                    b.Strikes = 2 + _rng.Next(3);
                    b.Hold = (3 + _rng.Next(4)) / 60f;
                    b.Mind.Left = PeckFor(b) + Organic.Jitter(_rng, 0.5f);
                    break;
                case 2:
                    b.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 2.2f));
                    if (_rng.Next(3) == 0) BeginTurn(b, -b.Dir);
                    break;
                default:
                    b.Mind.Begin(Doing.Walk, Organic.Jitter(_rng, 4.5f));
                    b.Pace.Want = Speed * u;
                    if (_rng.Next(4) == 0) BeginTurn(b, -b.Dir);
                    // The toms keep an eye on each other: every other walk of a tom who is not near his rival heads that way (so they end up
                    // within each other's reach now and then, which is what the face-off needs). Deterministic, no new dice.
                    if (rival != null && MathF.Abs(rival.X - b.X) > 0.28f * u && (_tomWalks++ & 1) == 0) BeginTurn(b, rival.X > b.X ? 1 : -1);
                    break;
            }
            Log(b, i, "roll");
        }
    }

    /// <summary>What a reaction is for a turkey: a look is a glance that way; a freeze is the head up, still, facing it; a flight is a few quick steps away, which also tells the flock.</summary>
    private void React(Bird b, Reaction r, Notice cause)
    {
        float u = _s.U;
        int toward = cause.At.X >= b.X ? 1 : -1;
        switch (r)
        {
            case Reaction.Look:
                if (b.Mind.Doing is Doing.Stand or Doing.Peck) { b.Mind.Begin(Doing.Alert, 0.6f + 0.8f * b.Senses.Curious); b.Clock = 0; BeginTurn(b, toward); }
                break;
            case Reaction.Approach:
                // Over to it: a tom's call or loose hay. The turn comes first; the walk waits for it (Update skips the body while turning).
                b.Goal = cause.At.X; b.GoalFood = cause.Kind == Sense.Food;
                b.Mind.Begin(Doing.Approach, 7f);
                b.Clock = 0;
                b.Pace.Want = Speed * u;
                BeginTurn(b, toward);
                break;
            case Reaction.Freeze:
                if (!b.Pace.Still && !b.Pace.Braking) b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));   // a walking bird stops square first (the walk pages play on until it is still)
                b.Mind.Begin(Doing.Alert, 1.5f + 2.5f * b.Timid);
                b.Clock = 0;
                BeginTurn(b, toward);
                break;
            case Reaction.Flee:
                b.Mind.Begin(Doing.Hurry, 1.2f + 1.5f * b.Timid);
                b.Clock = 0;
                b.Pace.Want = HurrySpeed * u;
                BeginTurn(b, -toward);
                _board.Post(Sense.Friend, new PointF(b.X, FootY(b)), 0.45f * u, 0.9f, b);         // the alarm spreads: the others look up
                break;
        }
        Log(b, Array.IndexOf(_birds, b), $"react {r} to {cause.Kind}");
    }

    // ================================================================ the face-off

    /// <summary>The test aid: the toms stand three lengths apart in the middle of the field, tom 0 begins his display.</summary>
    private void StartFaceTest()
    {
        _faceTestDone = true;
        Bird a = _birds[0];
        int other = -1;
        for (int i = 1; i < _birds.Length; i++) if (_birds[i].Tom) other = i;
        if (other < 0) return;
        Bird b = _birds[other];
        a.X = _s.Width * 0.5f; b.X = a.X + 3f * _k[a.Size]; b.Size = a.Size; b.Depth = a.Depth + 0.02f;
        a.Pace.Halt(); b.Pace.Halt(); a.Turning = b.Turning = false;
        a.Dir = 1; b.Dir = 1;                                       // the stander faces right, away from his rival: he has to turn
        a.Mind.Begin(Doing.Display, FanOpen + FanHold + FanOpen); b.Mind.Begin(Doing.Stand, 20f);
        a.Clock = b.Clock = 0;
        _nextFace = 0;
        TryFaceoff(0);
    }

    /// <summary>
    /// Tom "a" has just begun his display. If the other tom is close (0.28 U), idle and not near an edge, and it is
    /// not too soon after the last one, the other comes over: phase 1 (a watches, turned to him; b walks to a beak's
    /// length away). Returns whether it began.
    /// </summary>
    private bool TryFaceoff(int ai)
    {
        if (_foPhase != 0 || _time < _nextFace) return false;
        Bird a = _birds[ai];
        float k = _k[a.Size];
        int bi = -1;
        for (int i = 0; i < _birds.Length; i++)
        {
            if (i == ai || !_birds[i].Tom) continue;
            Bird c = _birds[i];
            if (c.Size != a.Size || c.Turning || c.Mind.Doing is not (Doing.Stand or Doing.Peck or Doing.Walk)) continue;
            float gap = MathF.Abs(c.X - a.X);
            if (gap < 2f * k || gap > 0.28f * _s.U) continue;
            if (MathF.Min(a.X, c.X) < 4f * k || MathF.Max(a.X, c.X) > _s.Width - 4f * k) continue;     // room for the loser to slink off
            bi = i; break;
        }
        if (bi < 0) return false;
        Bird b = _birds[bi];
        _foA = ai; _foB = bi; _foPhase = 1; _foT = 0f;
        _foSide = b.X > a.X ? 1 : -1;                                // which side of the stander the comer is on
        _foLoser = _birds[ai].Timid > b.Timid ? ai : bi;             // the less bold gives in
        a.Mind.Begin(Doing.Alert, Mind<Doing>.Forever); a.Clock = 0; BeginTurn(a, _foSide);
        if (!b.Pace.Still && !b.Pace.Braking) b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));
        b.Mind.Begin(Doing.Square, Mind<Doing>.Forever); b.Clock = 0; b.FoldAt = float.MaxValue; b.CloseFor = FanOpen;
        a.FoldAt = float.MaxValue; a.CloseFor = FanOpen;
        BeginTurn(b, -_foSide);
        Log(b, bi, $"faceoff begins with turkey{ai}, loser turkey{_foLoser}");
        return true;
    }

    /// <summary>The other tom (or null).</summary>
    private Bird? Rival(Bird b)
    {
        foreach (Bird c in _birds) if (c != b && c.Tom) return c;
        return null;
    }

    private void AbortFaceoff()
    {
        foreach (int i in new[] { _foA, _foB })
        {
            Bird b = _birds[i];
            if (!b.Pace.Still && !b.Pace.Braking) b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));
            b.Mind.Begin(Doing.Stand, 1f); b.Clock = 0; b.Slunk = false;
        }
        Log(_birds[_foA], _foA, "faceoff aborted");
        _foPhase = 0; _nextFace = _time + 60;
    }

    private void TickFaceoff(float dt)
    {
        Bird a = _birds[_foA], b = _birds[_foB], l = _birds[_foLoser], w = _birds[_foLoser == _foA ? _foB : _foA];
        float k = _k[a.Size], u = _s.U;
        _foT += dt;
        if (_foPhase == 1)
        {
            // The comer walks to a beak's length (1.1 lengths) from the stander once he has turned; he brakes onto a rest page
            // so the stop is even, and the square-up begins when both stand still.
            float target = a.X + _foSide * 1.1f * k;
            float remaining = (target - b.X) * b.Dir;                          // positive while he still has to walk toward it
            if (!b.Turning && !b.Pace.Braking && b.Pace.Still && remaining > 0.3f * k) b.Pace.Want = Speed * u * 1.15f;
            if (!b.Turning && !b.Pace.Braking && !b.Pace.Still && remaining <= b.Pace.ToRestPage(Stride(b))) b.Pace.Brake(b.Pace.ToRestPage(Stride(b)));
            if (!b.Turning && !a.Turning && b.Pace.Still && remaining <= 0.3f * k)
            {
                // Beak to beak: both fan (the loser folds first), and the hens are told it is on.
                _foPhase = 2; _foT = 0f;
                b.Pace.Want = 0f;
                a.Mind.Begin(Doing.Square, Mind<Doing>.Forever); b.Mind.Begin(Doing.Square, Mind<Doing>.Forever);
                a.Clock = b.Clock = 0;
                l.FoldAt = 3.0f; l.CloseFor = 0.45f; w.FoldAt = 5.0f; w.CloseFor = FanOpen;
                l.Slunk = false; l.SlinkT = 0f;
                _board.Post(Sense.Call, new PointF((a.X + b.X) / 2, FootY(a)), 0.6f * u, 1.0f, a);     // the louder call
                Log(a, _foA, "faceoff: square up");
            }
            else if (_foT > 16f) AbortFaceoff();                                // he never got there (patience)
            return;
        }
        // Phase 2. The loser's fan is folded by 3.45 s; then he turns away and slinks off; the winner folds at 5.0 s.
        if (_foT >= l.FoldAt + l.CloseFor && !l.Slunk && l.Mind.Doing == Doing.Square)
        {
            int away = l.X > w.X ? 1 : -1;
            l.Mind.Begin(Doing.Slink, Mind<Doing>.Forever);
            l.Clock = 0; l.SlinkT = 0f;
            BeginTurn(l, away);
            l.Slunk = true;
            Log(l, _foLoser, "faceoff: backs down");
        }
        if (l.Mind.Doing == Doing.Slink)
        {
            if (!l.Turning)
            {
                l.SlinkT += dt;
                if (l.SlinkT < 1.7f) { if (!l.Pace.Braking) l.Pace.Want = HurrySpeed * u * 0.9f; }
                else if (!l.Pace.Braking && !l.Pace.Still) l.Pace.Brake(l.Pace.ToRestPage(Stride(l)));
                if (l.SlinkT >= 1.7f && l.Pace.Still) { l.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 1.5f)); l.Clock = 0; l.Slunk = false; Log(l, _foLoser, "faceoff: loser stops"); }
            }
        }
        if (w.Mind.Doing == Doing.Square && _foT >= w.FoldAt + w.CloseFor)
        {
            w.Mind.Begin(Doing.Stand, Organic.Jitter(_rng, 1f)); w.Clock = 0;
            Log(w, _foLoser == _foA ? _foB : _foA, "faceoff: winner folds");
        }
        if (w.Mind.Doing != Doing.Square && l.Mind.Doing != Doing.Slink && l.Mind.Doing != Doing.Square)
        {
            _foPhase = 0; _nextFace = _time + 60;
            Log(a, _foA, "faceoff over");
        }
    }

    private void Log(Bird b, int i, string what)
    {
        _log?.WriteLine($"{_time,8:F2} turkey{i} x {b.X:F0} v {b.Pace.Speed:F1} {b.Mind.Doing} {what}");
    }

    /// <summary>How far the bird is pitched into its peck at "t" seconds into the doing: strikes, holds, pull-backs, then the lift.</summary>
    private static float PeckAt(float t, int strikes, float hold)
    {
        for (int n = 0; n < strikes; n++)
        {
            float from = n == 0 ? 0f : 0.7f;
            if (t < StrikeFor) return from + (1 - from) * (t / StrikeFor);           // the strike: down fast
            t -= StrikeFor;
            if (t < hold) return 1f;                                                 // the beak on the ground
            t -= hold;
            if (n == strikes - 1) break;
            if (t < PullFor) return 1f - 0.3f * (t / PullFor);                       // a small pull back before the next strike
            t -= PullFor;
        }
        if (t < LiftFor) return 1f - t / LiftFor;                                    // the lift, slower
        return 0f;
    }

    /// <summary>Stamps the flock, farthest bird first, through the field stencil.</summary>
    public void Draw(FrameBuffer fb)
    {
        // Far to near: a simple pass picking the shallowest bird not yet drawn (the flock is a handful, so no sort is needed).
        Span<bool> done = stackalloc bool[_birds.Length];
        for (int n = 0; n < _birds.Length; n++)
        {
            int pick = -1;
            for (int i = 0; i < _birds.Length; i++)
                if (!done[i] && (pick < 0 || _birds[i].Depth < _birds[pick].Depth)) pick = i;
            done[pick] = true;
            Bird b = _birds[pick];
            int z = b.Size, d = b.Dir > 0 ? 0 : 1;
            Sprite page;
            if (b.Turning)
            {
                // Narrowing while it still faces the old way (Dir flips at the middle), then widening facing the new.
                float q = b.TurnT / TurnFor;
                int tp = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                page = _turn[z][d][tp];
            }
            else if (!b.Pace.Still && b.Mind.Doing == Doing.Slink)
            {
                // Hunched: a half hunch for the first quarter second and for the last stride's braking, the full one between.
                int set = b.SlinkT < 0.25f || b.Pace.Braking ? 0 : 1;
                page = _slink[z][d][set][b.Pace.Page(Stride(b), WalkPages)];
            }
            else if (!b.Pace.Still) page = _walk[z][d][b.Pace.Page(Stride(b), WalkPages)];      // moving: the walk pages, whatever the mind is up to
            else switch (b.Mind.Doing)
            {
                case Doing.Walk:
                case Doing.Hurry:
                case Doing.Approach:
                    page = _walk[z][d][0];
                    break;
                case Doing.Peck:
                {
                    float p = PeckAt(b.Clock, b.Strikes, b.Hold);
                    int idx = (int)MathF.Round(p * PeckPages) - 1;
                    page = idx < 0 ? _walk[z][d][0] : _peck[z][d][Math.Min(PeckPages - 1, idx)];
                    break;
                }
                case Doing.Display:
                {
                    float t = b.Clock;
                    if (t < FanOpen) page = _fan[z][d][Math.Min(FanPages - 1, (int)(t / FanOpen * FanPages))];
                    else if (t < FanOpen + FanHold) page = _quiver[z][d][(int)((t - FanOpen) * 7) % QuiverPages];
                    else page = _fan[z][d][Math.Max(0, FanPages - 1 - (int)((t - FanOpen - FanHold) / FanOpen * FanPages))];
                    break;
                }
                case Doing.Alert:
                    page = _alert[z][d];
                    break;
                case Doing.Square:
                {
                    // Fan out over a second, quiver (the two toms out of step), fold at this bird's own second.
                    float t = b.Clock;
                    if (_foPhase != 2) page = _alert[z][d];                       // still walking over or turning to face him: the head up
                    else if (t < FanOpen) page = _fan[z][d][Math.Min(FanPages - 1, (int)(t / FanOpen * FanPages))];
                    else if (t < b.FoldAt) page = _quiver[z][d][((int)((t - FanOpen) * 7) + (pick == _foB ? 2 : 0)) % QuiverPages];
                    else page = _fan[z][d][Math.Max(0, FanPages - 1 - (int)((t - b.FoldAt) / b.CloseFor * FanPages))];
                    break;
                }
                default:
                    page = _walk[z][d][0];
                    break;
            }
            int left = (int)MathF.Round(b.X - (d == 0 ? _ox[z] : _w[z] - _ox[z]));
            int top = (int)MathF.Round(FootY(b)) - _by[z];
            page.Draw(fb, left, top, 1f, _s.OpenField);
        }
    }
}
