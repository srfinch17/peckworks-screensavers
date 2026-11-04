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
/// a fixed speed and turns it round at the ends; pecking swings the head
/// down to the ground and up again on a short cycle; the display opens the
/// fan over a second, holds it quivering, and folds it. The pictures are
/// the Turkey happening's own flip-book (Turkey.PaintPage), painted here in
/// two sizes: birds deeper in the field are a little smaller.
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
    }

    private const int WalkPages = 24, PeckPages = 8, FanPages = 8, QuiverPages = 4;
    private const float Speed = 0.045f;             // in U per second: a slow stroll
    private const float PeckCycle = 0.7f;           // seconds for one peck, down and up
    private const float FanOpen = 0.9f, FanHold = 3.2f;

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Bird[] _birds;
    private readonly float[] _k = new float[2];
    private readonly int[] _w = new int[2], _h = new int[2], _ox = new int[2], _by = new int[2];
    private readonly Sprite[][][] _walk = new Sprite[2][][];    // [size][dir][page]
    private readonly Sprite[][][] _peck = new Sprite[2][][];
    private readonly Sprite[][][] _fan = new Sprite[2][][];
    private readonly Sprite[][][] _quiver = new Sprite[2][][];

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
            _walk[z] = new Sprite[2][]; _peck[z] = new Sprite[2][]; _fan[z] = new Sprite[2][]; _quiver[z] = new Sprite[2][];
            for (int d = 0; d < 2; d++)
            {
                bool mirror = d == 1;
                _walk[z][d] = new Sprite[WalkPages];
                for (int i = 0; i < WalkPages; i++)
                    _walk[z][d][i] = Page(z, mirror, MathF.Tau * i / WalkPages, 1f, 0f, 0f, 0f, 0f);
                _peck[z][d] = new Sprite[PeckPages];
                for (int i = 0; i < PeckPages; i++)
                    _peck[z][d][i] = Page(z, mirror, 0f, 0f, 0f, 0f, 0f, (i + 1) / (float)PeckPages);
                _fan[z][d] = new Sprite[FanPages];
                for (int i = 0; i < FanPages; i++)
                    _fan[z][d][i] = Page(z, mirror, 0f, 0f, (i + 1) / (float)(FanPages + 1), 0f, 0f, 0f);
                _quiver[z][d] = new Sprite[QuiverPages];
                float[] shake = [0f, 1.2f, 0f, -1.2f];
                for (int i = 0; i < QuiverPages; i++)
                    _quiver[z][d][i] = Page(z, mirror, 0f, 0f, 1f, 0f, shake[i], 0f);
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
                Doing = Doing.Peck,
                Timer = 1f + 4f * (float)_rng.NextDouble(),
            };
        }
    }

    private Sprite Page(int z, bool mirror, float phase, float walk, float fan, float gobble, float shake, float peck) =>
        Turkey.PaintPage(_k[z], _w[z], _h[z], _ox[z], _by[z], mirror, phase, walk, fan, gobble, shake, peck);

    /// <summary>Where this bird's feet are on the field at x: between the lane and the near ground's crest, by its depth.</summary>
    private float FootY(in Bird b) => _s.FieldY(b.X) + (_s.Ground.YAt(Math.Clamp(b.X, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(b.X)) * b.Depth;

    public void Update(float dt)
    {
        float u = _s.U;
        for (int i = 0; i < _birds.Length; i++)
        {
            ref Bird b = ref _birds[i];
            b.Timer -= dt;
            b.Clock += dt;
            if (b.Doing == Doing.Walk)
            {
                float step = Speed * u * dt;
                b.X += b.Dir * step;
                b.Walked += step;
                // Turn round at the edges of the field (a margin keeps the whole bird in the picture).
                float margin = _k[b.Size] * 0.9f;
                if (b.X < margin) { b.X = margin; b.Dir = 1; }
                if (b.X > _s.Width - margin) { b.X = _s.Width - margin; b.Dir = -1; }
            }
            if (b.Timer > 0) continue;

            // The timer ran out: roll the next doing. Pecking is the usual
            // thing; a tom who has been quiet a while puts on his show.
            b.Clock = 0;
            double roll = _rng.NextDouble();
            if (b.Tom && roll < 0.12) { b.Doing = Doing.Display; b.Timer = FanOpen + FanHold + FanOpen; }
            else if (roll < 0.45) { b.Doing = Doing.Peck; b.Timer = PeckCycle * (2 + _rng.Next(5)); }
            else if (roll < 0.70) { b.Doing = Doing.Stand; b.Timer = 1f + 2.5f * (float)_rng.NextDouble(); if (_rng.Next(3) == 0) b.Dir = -b.Dir; }
            else { b.Doing = Doing.Walk; b.Timer = 2f + 6f * (float)_rng.NextDouble(); if (_rng.Next(4) == 0) b.Dir = -b.Dir; }
        }
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
            float stride = 4 * 0.11f * _k[z];
            Sprite page;
            switch (b.Doing)
            {
                case Doing.Walk:
                {
                    float cycle = b.Walked / stride;
                    page = _walk[z][d][(int)MathF.Round((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages];
                    break;
                }
                case Doing.Peck:
                {
                    // Down and up on a sine: the head is on the ground at the middle of each cycle.
                    float p = (b.Clock % PeckCycle) / PeckCycle;
                    float down = MathF.Sin(p * MathF.PI);
                    int idx = (int)MathF.Round(down * PeckPages) - 1;
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
