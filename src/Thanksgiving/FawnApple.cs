using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// The fawn and the apple. An apple rolls out of the cornucopia, along the
/// ground, and its last bounce takes it up the bank and under the fence's
/// lower rail into the field. The fawn at the far side notices, trots down
/// to the fence, puts her head down through the rail, eats the apple in
/// three bites (the herd's own chewing pages), lifts her head, and walks
/// back up the field to her mother.
///
/// HOW THE FAWN IS BORROWED: the herd owns the fawn's body, its walk pages
/// and its neck. The story asks the herd to LEND it (DeerHerd.Lend): the
/// herd stops deciding for it, and each frame the story DRIVES it (where it
/// stands, how far down the field, which way it faces, where its neck
/// should be) and the herd still draws it with its own walk, nod, bites and
/// ears. At the end the story RELEASES it and the herd takes over again. The
/// herd draws the fawn in its own layer (through the field stencil, so the
/// fence is in front of her); only the apple is drawn here.
///
/// What it is solid to: the apple rolls on the walk line and passes behind
/// any nearer pumpkin; once over the crest it is drawn through the field
/// stencil (the fence and the near ground are in front of it) and smaller,
/// in four steps, as it goes further off.
/// Claims: ground, cornucopia (the apple's own), field (the fawn walks it).
/// </summary>
internal sealed class FawnApple : Story
{
    private const float AtFence = 0.93f;                   // the fawn's depth when she stands at the fence (1 would be the crest itself)

    public override string Name => "FawnApple";
    public override string Claims => "ground,cornucopia,field";
    public override bool CanBegin => _herd.FawnIndex >= 0;

    private readonly Cast _c;
    private readonly ThanksgivingScenery _s;
    private readonly DeerHerd _herd;
    private int _fawn = -1;
    private float _x0, _d0, _xStand, _appleX, _appleY, _rollFor, _inField, _trotAt, _arrive, _eatAt, _upAt, _turnAt, _back, _end;
    private int _dir, _chews;
    private bool _told;

    public FawnApple(Cast cast, DeerHerd herd) { _c = cast; _s = cast.S; _herd = herd; }

    public override float Begin(Random rng)
    {
        float k = _c.K, u = _s.U;
        _fawn = _herd.FawnIndex;
        _herd.Lend(_fawn);
        var (fx, _, fh, fd) = _herd.Where(_fawn);
        _x0 = fx; _d0 = fd;

        // The apple stops in the open stretch between the horn and the corn shock, then hops under the rail there.
        float shockEdge = _s.CornShockFoot.X - _s.CornShockHalfWidth - 2.0f * k;
        float lo = Math.Max(_s.CornucopiaMouth.X + 2.5f * k, _s.Width * 0.30f), hi = Math.Min(_s.Width * 0.46f, shockEdge);
        float stopX = hi > lo ? lo + (hi - lo) * (float)rng.NextDouble() : (lo + hi) / 2;
        _rollFor = 1.6f + 0.4f * (float)rng.NextDouble();
        _appleX = stopX + 0.3f * k;
        _appleY = _herd.FeetYAt(_fawn, _appleX, AtFence);
        _inField = _rollFor + 0.45f;

        // The fawn: stands so that her nose, down at the ground, is on the apple; trots down at a trot's pace, walks back at a walk's.
        _dir = _appleX >= _x0 ? 1 : -1;
        _xStand = _appleX - _dir * 0.55f * fh;
        float way = MathF.Sqrt((_xStand - _x0) * (_xStand - _x0) + (_appleY - _herd.FeetYAt(_fawn, _x0)) * (_appleY - _herd.FeetYAt(_fawn, _x0)));
        _trotAt = _inField + 0.6f;
        _arrive = _trotAt + way / (0.09f * u);
        _eatAt = _arrive + 0.7f;
        _upAt = _eatAt + 2.2f;
        _turnAt = _upAt + 1.0f;
        _back = _turnAt + way / (0.05f * u);
        _end = _back + 0.6f;
        _chews = 0;
        _told = false;

        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"fawnapple: fawn {_fawn} at {_x0:F0} depth {_d0:F2} stand {_xStand:F0} apple ({_appleX:F0},{_appleY:F0}) roll {_rollFor:F2} inField {_inField:F2} trot {_trotAt:F2} arrive {_arrive:F2} eat {_eatAt:F2} up {_upAt:F2} turn {_turnAt:F2} back {_back:F2} end {_end:F2}");
        }
        return _end;
    }

    public override void Update(float t)
    {
        if (_fawn < 0) return;
        float x, depth, neck = 0f;
        int dir = _dir;
        if (t < _trotAt) { x = _x0; depth = _d0; }
        else if (t < _arrive) { float p = Cast.Smooth((t - _trotAt) / (_arrive - _trotAt)); x = _x0 + (_xStand - _x0) * p; depth = _d0 + (AtFence - _d0) * p; }
        else if (t < _turnAt) { x = _xStand; depth = AtFence; neck = t < _upAt ? 1f : 0f; }
        else if (t < _back) { float p = Cast.Smooth((t - _turnAt) / (_back - _turnAt)); x = _xStand + (_x0 - _xStand) * p; depth = AtFence + (_d0 - AtFence) * p; dir = -_dir; }
        else { x = _x0; depth = _d0; dir = -_dir; }
        _herd.Drive(_fawn, x, dir, neck, depth);
        if (!_told && t >= _trotAt) { _told = true; _c.Board.Post(Peckworks.Screensavers.Core.Minds.Sense.Movement, new PointF(_x0, _appleY), 0.5f * _s.U, 0.7f, this); }   // the fawn trotting off: the turkeys look
        // Three bites, half a second apart, once her head is down.
        if (t >= _eatAt + _chews * 0.5f && _chews < 3) { _herd.Chew(_fawn); _chews++; }
    }

    public override void End()
    {
        if (_fawn >= 0) _herd.Release(_fawn);
        _fawn = -1;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float k = _c.K;
        float mouthX = _s.CornucopiaMouth.X, stopX = _appleX - 0.3f * k;
        float r0 = _c.Apple[0].Pages[0].Width * 0.33f;           // the full-size apple's radius, for its height off the ground and its turning
        float x, y, turn;
        int size = 0;
        bool field = false;
        if (t < _rollFor)
        {
            // Rolling out of the horn and slowing to a stop: the distance covered eases out, so the wheel slows.
            float p = t / _rollFor, q = 1 - (1 - p) * (1 - p);
            x = mouthX + (stopX - mouthX) * q;
            y = _s.WalkY(x) - r0;
            turn = (x - mouthX) / (r0 * MathF.Tau);
        }
        else if (t < _inField)
        {
            // The last bounce: a short arc up the bank and under the rail, onto the field at the fawn's line; smaller as it goes.
            float p = (t - _rollFor) / (_inField - _rollFor);
            x = stopX + (_appleX - stopX) * p;
            float y0 = _s.WalkY(stopX) - r0, y1 = _appleY - r0 * 0.55f;
            y = y0 + (y1 - y0) * p - 0.5f * k * 4 * p * (1 - p);
            field = p > 0.4f;
            size = Math.Min(Cast.AppleSizes - 1, (int)(p * Cast.AppleSizes));
            turn = (stopX - mouthX) / (r0 * MathF.Tau) + p * 0.6f;
        }
        else
        {
            x = _appleX; y = _appleY - r0 * 0.55f; field = true; size = Cast.AppleSizes - 1;
            turn = (stopX - mouthX) / (r0 * MathF.Tau) + 0.6f;
        }
        float opacity = t < _eatAt ? 1f : 1 - Cast.Smooth((t - _eatAt) / 0.9f);     // gone in three bites
        if (opacity <= 0f) return;
        int page = ((int)(turn * Cast.ApplePages) % Cast.ApplePages + Cast.ApplePages) % Cast.ApplePages;
        if (field) _c.Apple[size].Pages[page].DrawCentered(fb, x, y, opacity, _s.OpenField);
        else _c.Apple[size].Pages[page].DrawCentered(fb, x, y, opacity, null, false, _s.NearDepth, _s.Nearness(_s.WalkY(x)));
    }
}
