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
/// </summary>
internal sealed class TurkeyFlock
{
    private enum Doing { Walk, Stand, Peck, Display, Alert, Hurry, Approach }   // Approach: walking over to something (a tom's call, a bale's loose hay) and doing what it came for

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
    private readonly StreamWriter? _log;
    private double _time;
    private readonly float[] _k = new float[2];
    private readonly int[] _w = new int[2], _h = new int[2], _ox = new int[2], _by = new int[2];
    private readonly Sprite[][][] _walk = new Sprite[2][][];    // [size][dir][page]
    private readonly Sprite[][][] _peck = new Sprite[2][][];
    private readonly Sprite[][][] _fan = new Sprite[2][][];
    private readonly Sprite[][][] _quiver = new Sprite[2][][];
    private readonly Sprite[][][] _turn = new Sprite[2][][];     // [size][dir][page]: the standing bird narrowed about its feet
    private readonly Sprite[][] _alert = new Sprite[2][];        // [size][dir]: standing with the head up and the neck stretched

    public TurkeyFlock(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());             // its own dice, so adding a bird never reshuffles the rest of the scene
        _log = MindLog.Open();
        float u = s.U;
        _k[0] = Math.Max(8f, u * 0.060f);
        _k[1] = Math.Max(8f, u * 0.074f);
        for (int z = 0; z < 2; z++)
        {
            _w[z] = (int)(1.45f * _k[z]) + 4;
            _ox[z] = (int)(0.80f * _k[z]) + 2;
            _h[z] = (int)(1.08f * _k[z]) + 8;
            _by[z] = _h[z] - 5;
            _walk[z] = new Sprite[2][]; _peck[z] = new Sprite[2][]; _fan[z] = new Sprite[2][]; _quiver[z] = new Sprite[2][]; _turn[z] = new Sprite[2][]; _alert[z] = new Sprite[2];
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
                Tom = i == 0 && count >= 2,                 // the first bird is the tom when there is anyone to show off to
                Timid = timid[i], Busy = busy[i],
                Restless = (float)_rng.NextDouble() * 10,
            };
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

            // ---- the senses: what happened, and what this bird makes of it (never while already fleeing, or on its way to something) ----
            if (b.Mind.Doing is not (Doing.Hurry or Doing.Approach))
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
            int face = Dice.Pick(_rng, [b.Tom && b.Mind.Doing != Doing.Display ? 0.12f : 0f, 0.38f, 0.22f + 0.2f * b.Timid, 0.28f + 0.3f * b.Busy]);
            switch (face)
            {
                case 0:
                    b.Mind.Begin(Doing.Display, FanOpen + FanHold + FanOpen);
                    _board.Post(Sense.Call, new PointF(b.X, FootY(b)), 0.6f * u, 0.9f, b);          // the hens look over, and the curious ones come to watch
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
