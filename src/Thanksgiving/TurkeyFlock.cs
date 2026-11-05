using Peckworks.Screensavers.Core;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// A few wild turkeys that LIVE in the field behind the fence, the way the
/// bats live in Halloween's sky and Santa in Christmas's: always there,
/// pecking at the stubble, wandering a few steps, standing to look about,
/// and one tom now and then fanning his tail for the hens.
///
/// FEYNMAN VERSION: each bird is a little clockwork with four states (walk,
/// stand, peck, display) and a timer. When the timer runs out it rolls dice
/// for the next state and a new timer. Walking moves it along the field at
/// a slow stroll (about two steps a second) and turns it round at the ends;
/// a walk never stops in one frame: it eases down so the stride ends on the
/// page where both feet are on the ground. Pecking is a run of STRIKES: the
/// whole bird pitches forward in four frames, holds with the beak on the
/// ground for a few, pulls back a little, strikes again two to four times,
/// then lifts over ten frames (the first version swung the head down and up
/// on a slow, even sine, which is not how a bird feeds). The display opens
/// the fan over a second, holds it quivering, and folds it. The pictures
/// are the Turkey happening's own flip-book (Turkey.PaintPage), painted
/// here in two sizes: birds deeper in the field are a little smaller.
///
/// They are stamped through the painter's OpenField stencil, so the fence,
/// the near ground, the scarecrow and the maples hide them where they
/// should, and a bird walking behind the scarecrow's legs disappears and
/// comes out the other side.
/// </summary>
internal sealed class TurkeyFlock
{
    private enum Doing { Walk, Stand, Peck, Display }

    private struct Bird
    {
        public float X, Depth;          // where along the field, and how far down it (0 the lane, 1 the near crest)
        public int Dir;                 // +1 faces right, -1 left
        public int Size;                // 0 far (smaller), 1 near
        public bool Tom;                // the one who displays
        public Doing Doing;
        public float Timer;             // seconds left in this doing
        public float Walked;            // distance walked, for the stride page
        public float Clock;             // seconds into this doing
        public bool Stopping;           // the walk is easing down to the rest page
        public float StopFrom, StopDist, StopT, StopFor;
        public int Strikes;             // this peck's number of strikes
        public float Hold;              // seconds the beak stays on the ground each strike
        public bool Turning;            // coming round to face the other way through the turn pages (never a mirror flip)
        public float TurnT;             // seconds into the turn; Dir flips at the middle
    }

    // 72 walk pages: at 24 the held head stepped 1.9 px a page under a body moving 1 px a frame, and buzzed.
    private const int WalkPages = 72, PeckPages = 12, FanPages = 8, QuiverPages = 4, TurnPages = 3;
    private const float TurnFor = 0.2f;             // a turn on the spot: three narrowing pages facing the old way, three widening facing the new
    private static readonly float[] Squash = [0.72f, 0.42f, 0.14f];
    private const float Speed = 0.03f;              // in U per second: a slow stroll, about two steps a second
    private const float StrikeFor = 4f / 60, PullFor = 2f / 60, LiftFor = 11f / 60;
    private const float FanOpen = 0.9f, FanHold = 3.2f;

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Bird[] _birds;
    private readonly bool _turnTest = Environment.GetEnvironmentVariable("PECKWORKS_TURKEY") == "turn";
    private readonly float[] _k = new float[2];
    private readonly int[] _w = new int[2], _h = new int[2], _ox = new int[2], _by = new int[2];
    private readonly Sprite[][][] _walk = new Sprite[2][][];    // [size][dir][page]
    private readonly Sprite[][][] _peck = new Sprite[2][][];
    private readonly Sprite[][][] _fan = new Sprite[2][][];
    private readonly Sprite[][][] _quiver = new Sprite[2][][];
    private readonly Sprite[][][] _turn = new Sprite[2][][];     // [size][dir][page]: the standing bird narrowed about its feet

    public TurkeyFlock(ThanksgivingScenery s, int count, Random sceneRng)
    {
        _s = s;
        _rng = new Random(sceneRng.Next());             // its own dice, so adding a bird never reshuffles the rest of the scene
        float u = s.U;
        _k[0] = Math.Max(8f, u * 0.060f);
        _k[1] = Math.Max(8f, u * 0.074f);
        for (int z = 0; z < 2; z++)
        {
            _w[z] = (int)(1.45f * _k[z]) + 4;
            _ox[z] = (int)(0.80f * _k[z]) + 2;
            _h[z] = (int)(1.08f * _k[z]) + 8;
            _by[z] = _h[z] - 5;
            _walk[z] = new Sprite[2][]; _peck[z] = new Sprite[2][]; _fan[z] = new Sprite[2][]; _quiver[z] = new Sprite[2][]; _turn[z] = new Sprite[2][];
            for (int d = 0; d < 2; d++)
            {
                bool mirror = d == 1;
                _walk[z][d] = new Sprite[WalkPages];
                for (int i = 0; i < WalkPages; i++)
                    _walk[z][d][i] = Page(z, mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f, 0f);
                // The standing pages (a peck, the display, a turn) keep the feet where the walk's rest page (page 0) leaves them,
                // so nothing snaps when a bird that has just stopped starts to peck (the feet used to jump together).
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
            }
        }

        _birds = new Bird[count];
        for (int i = 0; i < count; i++)
        {
            float depth = 0.30f + 0.65f * (float)_rng.NextDouble();
            _birds[i] = new Bird
            {
                X = s.Width * (0.06f + 0.88f * (float)_rng.NextDouble()),
                Depth = depth,
                Size = depth < 0.62f ? 0 : 1,
                Dir = _rng.Next(2) == 0 ? 1 : -1,
                Tom = i == 0 && count >= 2,                 // the first bird is the tom when there is anyone to show off to
                Doing = Doing.Stand,
                Timer = 1f + 3f * (float)_rng.NextDouble(),
            };
        }
    }

    private Sprite Page(int z, bool mirror, float phase, float walk, float fan, float gobble, float shake, float peck, float legs = -1f, float squash = 1f) =>
        Turkey.PaintPage(_k[z], _w[z], _h[z], _ox[z], _by[z], mirror, phase, walk, fan, gobble, shake, peck, legs, squash);

    /// <summary>Starts a turn to face "dir" (nothing if already facing it): the bird stands while the turn pages play, and Dir flips at the middle.</summary>
    private static void BeginTurn(ref Bird b, int dir)
    {
        if (dir == b.Dir || b.Turning) return;
        b.Turning = true; b.TurnT = 0;
    }

    /// <summary>Where this bird's feet are on the field at x: between the lane and the near ground's crest, by its depth.</summary>
    private float FootY(in Bird b) => _s.FieldY(b.X) + (_s.Ground.YAt(Math.Clamp(b.X, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(b.X)) * b.Depth;
    private float Stride(in Bird b) => 4 * 0.11f * _k[b.Size];
    private float PeckFor(in Bird b) => b.Strikes * (StrikeFor + b.Hold + PullFor) - PullFor + LiftFor;

    public void Update(float dt)
    {
        float u = _s.U;
        for (int i = 0; i < _birds.Length; i++)
        {
            ref Bird b = ref _birds[i];
            b.Timer -= dt;
            b.Clock += dt;
            if (b.Turning)
            {
                // A turn on the spot: whatever it was doing waits (its clock too, so a peck is not half spent when it begins).
                b.Clock -= dt;
                float was = b.TurnT;
                b.TurnT += dt;
                if (was < TurnFor / 2 && b.TurnT >= TurnFor / 2) b.Dir = -b.Dir;
                if (b.TurnT >= TurnFor) b.Turning = false;
                continue;
            }
            if (b.Doing == Doing.Walk)
            {
                if (b.Stopping)
                {
                    // Slowing evenly to a halt exactly on the rest page (both feet on the ground).
                    b.StopT += dt;
                    float k = Math.Min(1f, b.StopT / b.StopFor);
                    float walked = b.StopFrom + b.StopDist * (2 * k - k * k);
                    b.X += b.Dir * (walked - b.Walked);
                    b.Walked = walked;
                    if (k >= 1f) { b.Stopping = false; b.Doing = Doing.Stand; b.Walked = 0; b.Clock = 0; b.Timer = 0.8f + 2f * (float)_rng.NextDouble(); }
                }
                else
                {
                    float step = Speed * u * dt;
                    b.X += b.Dir * step;
                    b.Walked += step;
                    if (b.Timer <= 0) BeginStop(ref b);
                }
                // Turn round at the edges of the field (a margin keeps the whole bird in the picture).
                float margin = _k[b.Size] * 0.9f;
                if (b.X < margin) { b.X = margin; BeginTurn(ref b, 1); }
                if (b.X > _s.Width - margin) { b.X = _s.Width - margin; BeginTurn(ref b, -1); }
                continue;
            }
            if (b.Timer > 0) continue;

            // The timer ran out: roll the next doing. Pecking is the usual
            // thing; a tom who has been quiet a while puts on his show.
            b.Clock = 0;
            double roll = _rng.NextDouble();
            if (_turnTest) { b.Doing = Doing.Stand; b.Timer = 1.2f; BeginTurn(ref b, -b.Dir); continue; }   // PECKWORKS_TURKEY=turn: every bird turns at every roll, for watching the turn pages
            if (b.Tom && roll < 0.12) { b.Doing = Doing.Display; b.Timer = FanOpen + FanHold + FanOpen; }
            else if (roll < 0.50)
            {
                b.Doing = Doing.Peck;
                b.Strikes = 2 + _rng.Next(3);
                b.Hold = (3 + _rng.Next(4)) / 60f;
                b.Timer = PeckFor(in b) + 0.3f + 0.6f * (float)_rng.NextDouble();
            }
            else if (roll < 0.72) { b.Doing = Doing.Stand; b.Timer = 1f + 2.5f * (float)_rng.NextDouble(); if (_rng.Next(3) == 0) BeginTurn(ref b, -b.Dir); }
            else { b.Doing = Doing.Walk; b.Walked = 0; b.Timer = 2f + 6f * (float)_rng.NextDouble(); if (_rng.Next(4) == 0) BeginTurn(ref b, -b.Dir); }
        }
    }

    private void BeginStop(ref Bird b)
    {
        float stride = Stride(in b);
        float into = b.Walked % stride;
        float dist = stride - into;
        if (dist < 0.25f * stride) dist += stride;
        b.Stopping = true;
        b.StopFrom = b.Walked; b.StopDist = dist; b.StopT = 0;
        b.StopFor = 2 * dist / (Speed * _s.U);
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
            ref Bird b = ref _birds[pick];
            int z = b.Size, d = b.Dir > 0 ? 0 : 1;
            Sprite page;
            if (b.Turning)
            {
                // Narrowing while it still faces the old way (Dir flips at the middle), then widening facing the new.
                float q = b.TurnT / TurnFor;
                int tp = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                page = _turn[z][d][tp];
            }
            else switch (b.Doing)
            {
                case Doing.Walk:
                {
                    float cycle = b.Walked / Stride(in b);
                    page = _walk[z][d][(int)MathF.Floor((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages];
                    break;
                }
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
                default:
                    page = _walk[z][d][0];
                    break;
            }
            int left = (int)MathF.Round(b.X - (d == 0 ? _ox[z] : _w[z] - _ox[z]));
            int top = (int)MathF.Round(FootY(in b)) - _by[z];
            page.Draw(fb, left, top, 1f, _s.OpenField);
        }
    }
}
