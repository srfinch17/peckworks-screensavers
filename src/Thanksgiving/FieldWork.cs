using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// The field being worked: an old red tractor drives slowly along the far
/// lane towing a round baler, and every so often the baler's tailgate swings
/// up and a finished bale rolls out the back and is left standing on the
/// field. The tractor goes off one side, comes back the other way after a
/// while, and bales the next row. When half a dozen bales stand loose it
/// comes back towing the hay wagon instead and picks them up as it passes:
/// as the wagon's tail clears a bale, the loader swings it up WHOLE and sets
/// it on the bed, and the stack grows three, two and one. Then it is back to
/// baling. (It used to turn each bale back into a heap of loose hay: "it's
/// gotta pick up the entire bale".) His line: "there should be a tractor out in
/// the field making the bales, slowly driving around with bales coming out
/// when they're done in the baling machine".
///
/// FEYNMAN VERSION: this is a resident like the turkeys, not a happening: it
/// keeps its own clock and is always somewhere (on the lane or away beyond
/// the edge of the picture). It drives the lane (ThanksgivingScenery.FieldY,
/// the field's far edge), which is BEHIND every deer and turkey (they stand
/// nearer the fence), so it is stamped before them through the field stencil
/// and they walk in front of it. The pictures are TractorArt's: the tractor
/// and the implement are two sheets stamped at the same place, the wheels
/// are separate pictures turned by the distance rolled. A bale on the field
/// is a sprite painted once with the painter's own bale brush, stamped at
/// its spot every frame, so it is exactly the kind of bale the painter put
/// there before the saver started.
///
/// The animals hear it: every two thirds of a second it pins a Loud notice at
/// its nose (the deer lift their heads and the timid ones trot off, the
/// turkeys look up or hurry), and each bale it drops pins a Food notice (the
/// turkeys come over to scratch at the loose hay). Test aids: PECKWORKS_MINDLOG
/// gets a "tractor" line every frame (x, speed, job) and one for each event;
/// PECKWORKS_FIELD=haul starts with six loose bales so the wagon comes first.
/// </summary>
internal sealed class FieldWork
{
    private enum Job { Away, Baling, Hauling }

    private const float Cruise = 0.062f;            // U per second: a slow drive, about half the old lane tractor's trundle
    private const float BaleEvery = 7.5f;           // seconds of driving per bale, jittered
    private const int BaleCap = 6;                  // loose bales on the field before the wagon comes for them
    private const float GateFor = 1.1f;             // the tailgate's swing up, the bale out, and the swing shut
    private const float BaleOutAt = 0.45f;          // when in that swing the bale is on the ground
    private const float AwayFor = 22f;              // seconds beyond the edge between passes, jittered
    private const float LoudEvery = 0.66f, PuffEvery = 1.1f, PuffLife = 2.6f;

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;
    private readonly bool[] _stencil;
    private readonly float _t;                      // the tractor unit in pixels
    private readonly int _len, _tall, _gy;
    private readonly Sprite[] _tractor = new Sprite[2];           // [0] heads right, [1] heads left
    private readonly Sprite[][] _baler = new Sprite[2][];         // [dir][gate page]
    private readonly Sprite[] _wagon = new Sprite[2];             // [0] heads right, [1] heads left; its bales are stamped on separately
    private const float LiftFor = 0.9f;             // seconds a bale takes from the ground to its slot on the wagon
    private readonly Sprite[] _rear = new Sprite[TractorArt.WheelPages], _front = new Sprite[TractorArt.WheelPages], _small = new Sprite[TractorArt.WheelPages];
    private readonly Sprite _lamp, _puff, _bale;
    private readonly float _baleR;
    private const int GatePages = 12;               // the tailgate's swing: at 6 pages a 70 degree swing in 0.45 s stepped 14 degrees a page

    private Job _job = Job.Away;
    private bool _left;                             // this pass heads left
    private float _x;                               // the sheet's left edge, pixels
    private float _rolled, _clock, _away, _nextBale, _nextLoud;
    private float _gateT = -1f;                     // seconds into the tailgate's swing, -1 shut
    private bool _dropped;                          // this swing's bale is out
    private readonly List<float> _bales = new();    // loose bales on the lane, by their x
    private int _loaded;                            // bales on the wagon this haul, filling TractorArt.BaleSlots in order
    private float _liftX, _liftT = -1f;             // a bale on its way up: where it stood, and seconds into the lift (-1 none)
    private double _time;

    public FieldWork(ThanksgivingScenery s, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());         // its own dice
        _log = MindLog.Open();
        _stencil = s.OpenField;
        _t = Math.Max(9f, s.U * 0.040f);
        (_len, _tall, _gy) = TractorArt.Sheet(_t);
        for (int d = 0; d < 2; d++)
        {
            bool flip = d == 1;
            _tractor[d] = TractorArt.Tractor(_t, flip);
            _baler[d] = new Sprite[GatePages];
            for (int i = 0; i < GatePages; i++) _baler[d][i] = TractorArt.Baler(_t, flip, i / (float)(GatePages - 1));
            _wagon[d] = TractorArt.Wagon(_t, flip);
        }
        for (int i = 0; i < TractorArt.WheelPages; i++)
        {
            _rear[i] = TractorArt.Wheel(TractorArt.RearR * _t, i, TractorArt.Spoke, true);
            _front[i] = TractorArt.Wheel(TractorArt.FrontR * _t, i, TractorArt.Spoke, false);
            _small[i] = TractorArt.Wheel(TractorArt.CartR * _t, i, Color.FromArgb(206, 160, 98), false);
        }
        _lamp = Sprite.Glow(Math.Max(3, (int)(s.U * 0.020f)), Color.FromArgb(255, 226, 150));
        _puff = Sprite.Glow(Math.Max(2, (int)(s.U * 0.010f)), Color.FromArgb(206, 192, 180));
        // A bale the size the painter makes them at the lane (its far bales are 0.024 U at depth 0.35; the lane is depth 0).
        _baleR = s.U * 0.024f * 0.75f;
        int bw = (int)(_baleR * 2.4f) + 4, bh = (int)(_baleR * 2.3f) + 4;
        float r = _baleR;
        var brush = new Random(_rng.Next());
        _bale = Sprite.Paint(bw, bh, g => ThanksgivingPainter.PaintBale(g, bw / 2f, bh - 2 - r * 0.0f, r, brush));
        _away = 6f + 8f * (float)_rng.NextDouble();  // the first pass comes soon
        _left = _rng.Next(2) == 0;
        // Test aid: PECKWORKS_FIELD=haul starts with a full set of loose bales, so the first pass is the wagon's.
        if (Environment.GetEnvironmentVariable("PECKWORKS_FIELD") == "haul")
            for (int i = 0; i < BaleCap; i++) _bales.Add(s.Width * (0.08f + 0.84f * (i + 0.5f) / BaleCap));
    }

    private float Dir => _left ? -1f : 1f;
    /// <summary>The ground under the middle of the machine: the lane, a hair down into the field.</summary>
    private float GroundAt(float x) => _s.FieldY(Math.Clamp(x, 0, _s.Width - 1)) + _s.U * 0.004f;
    /// <summary>A point "units" along the sheet from the machine's tail, in pixels, whichever way it heads.</summary>
    private float AlongX(float units) => _left ? _x + _len - (1 + units * _t) : _x + 1 + units * _t;
    private bool OnScreen => _x < _s.Width && _x + _len > 0;

    public void Update(float dt)
    {
        float u = _s.U;
        _time += dt;
        switch (_job)
        {
            case Job.Away:
                _away -= dt;
                if (_away <= 0f)
                {
                    // Back on, the other way from last time: baling, or hauling when enough bales stand loose.
                    _left = !_left;
                    _job = _bales.Count >= BaleCap ? Job.Hauling : Job.Baling;
                    _x = _left ? _s.Width : -_len;
                    _rolled = 0; _clock = 0; _loaded = 0; _gateT = -1f; _dropped = false; _liftT = -1f;
                    _nextBale = Organic.Jitter(_rng, BaleEvery * 0.7f);
                    _nextLoud = 0;
                    Log($"on, {_job}, {(_left ? "left" : "right")}, {_bales.Count} loose");
                }
                break;

            case Job.Baling:
            case Job.Hauling:
                float step = Cruise * u * dt;
                _x += Dir * step; _rolled += step; _clock += dt;
                _nextLoud -= dt;
                if (_nextLoud <= 0f && OnScreen)
                {
                    _board.Post(Sense.Loud, new PointF(AlongX(TractorArt.LampX), GroundAt(AlongX(TractorArt.RearX))), 0.5f * u, 0.6f, this);
                    _nextLoud = LoudEvery;
                }
                if (_job == Job.Baling)
                {
                    if (_gateT < 0f)
                    {
                        _nextBale -= dt;
                        if (_nextBale <= 0f && _bales.Count < BaleCap && AlongX(TractorArt.BalerRearX) > 0.03f * _s.Width && AlongX(TractorArt.BalerRearX) < 0.97f * _s.Width)
                        { _gateT = 0f; _dropped = false; }
                    }
                    else
                    {
                        _gateT += dt;
                        if (!_dropped && _gateT >= BaleOutAt)
                        {
                            // The bale is out: it stands where the baler's back was, and the turkeys hear of the loose hay.
                            float bx = AlongX(TractorArt.BalerRearX) - Dir * _baleR * 0.6f;
                            _bales.Add(bx); _dropped = true;
                            _board.Post(Sense.Food, new PointF(bx, GroundAt(bx)), 0.55f * u, 1f, this);
                            Log($"bale {_bales.Count} at {bx:F0}");
                        }
                        if (_gateT >= GateFor) { _gateT = -1f; _nextBale = Organic.Jitter(_rng, BaleEvery); }
                    }
                }
                else
                {
                    // Hauling: as the wagon's tail clears a loose bale, the loader takes it off the ground and swings it whole
                    // up onto the bed, into the next slot of the stack (LiftFor seconds; drawn in Draw). One at a time: the
                    // bales stand about eleven tractor units apart, so the next is never due before this one is down.
                    if (_liftT >= 0f)
                    {
                        _liftT += dt;
                        if (_liftT >= LiftFor) { _liftT = -1f; _loaded++; Log($"loaded {_loaded}"); }
                    }
                    else if (_loaded < TractorArt.BaleSlots.Length)
                    {
                        float tail = AlongX(TractorArt.BedX0);
                        for (int i = _bales.Count - 1; i >= 0; i--)
                            if (MathF.Abs(_bales[i] - tail) < 0.25f * _t) { _liftX = _bales[i]; _bales.RemoveAt(i); _liftT = 0f; Log($"lifting bale at {_liftX:F0}"); break; }
                    }
                }
                if ((_left && _x + _len < 0) || (!_left && _x > _s.Width))
                {
                    _job = Job.Away;
                    _away = Organic.Jitter(_rng, AwayFor);
                    Log($"off, {_bales.Count} loose, away {_away:F0} s");
                }
                break;
        }
        _log?.WriteLine($"{_time,8:F3} tractor frame x {AlongX(TractorArt.RearX):F1} v {(_job == Job.Away ? 0f : Cruise * u):F2} {_job}");
    }

    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} tractor {what}");

    /// <summary>One bale, its foot (the bottom of the picture) at footY and its middle at cx.</summary>
    private void StampBale(FrameBuffer fb, float cx, float footY) =>
        _bale.Draw(fb, (int)MathF.Round(cx - _bale.Width / 2f), (int)MathF.Round(footY) - _bale.Height + 2, 1f, _stencil);

    /// <summary>The loose bales (behind everything on the field), then the machine if it is on.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (float bx in _bales) StampBale(fb, bx, GroundAt(bx));
        if (_job == Job.Away || !OnScreen) return;

        int d = _left ? 1 : 0;
        float cx = _x + _len / 2f;
        float ground = GroundAt(cx);
        int top = (int)MathF.Round(ground) - _gy;
        int shake = (int)(_clock * 16) % 2 == 0 ? 0 : Math.Max(1, (int)(_s.U / 1100f));   // the engine's tremble: the body only, a pixel at a time
        int left = (int)MathF.Round(_x);

        // The smoke first, so the machine is stamped over it. Puff j left the stack at time j * PuffEvery of this pass.
        for (int j = 0; j * PuffEvery < _clock; j++)
        {
            float age = _clock - j * PuffEvery;
            if (age > PuffLife) continue;
            float bornX = _x - Dir * Cruise * _s.U * age;                                 // where the sheet's edge was then
            float stackDX = _left ? _len - (1 + TractorArt.StackX * _t) : 1 + TractorArt.StackX * _t;
            float a = age / PuffLife;
            float px = bornX + stackDX - Dir * _s.U * 0.02f * a;                          // blown a little back along the way it came
            float py = ground - 1.56f * _t - _s.U * 0.045f * MathF.Sqrt(a);               // floats up, slowing as it goes
            _puff.DrawCentered(fb, px, py, 0.55f * (1 - a) * Math.Min(1f, age * 3f), _stencil);
        }

        // The implement, then the tractor, on the same sheet.
        if (_job == Job.Baling)
        {
            float open = _gateT < 0f ? 0f : _gateT < BaleOutAt ? _gateT / BaleOutAt : MathF.Max(0f, 1f - (_gateT - BaleOutAt) / (GateFor - BaleOutAt));
            _baler[d][Math.Min(GatePages - 1, (int)(open * GatePages))].Draw(fb, left, top - shake, 1f, _stencil);
            // The bale on its way out: while the gate is up and before it has dropped, it shows in the drum's open back.
            if (_gateT >= 0f && !_dropped && _gateT > BaleOutAt * 0.4f)
            {
                float out_ = (_gateT - BaleOutAt * 0.4f) / (BaleOutAt * 0.6f);
                float bx = AlongX(TractorArt.BalerRearX + 0.5f - 0.5f * out_) - Dir * _baleR * 0.6f * out_;
                StampBale(fb, bx, GroundAt(bx) - _baleR * 0.9f * (1 - out_));
            }
        }
        else
        {
            // The wagon, the bales already stacked on it (they tremble with the body), then the one on its way up: it
            // leaves the ground where it stood, rises over the rail in an arc and settles into its slot, which is
            // moving with the wagon, so it glides forward as it climbs. Smoothstep, so it neither jerks off nor thuds on.
            _wagon[d].Draw(fb, left, top - shake, 1f, _stencil);
            for (int i = 0; i < _loaded; i++)
                StampBale(fb, AlongX(TractorArt.BaleSlots[i].X), ground - TractorArt.BaleSlots[i].Y * _t - shake);
            if (_liftT >= 0f)
            {
                var slot = TractorArt.BaleSlots[Math.Min(_loaded, TractorArt.BaleSlots.Length - 1)];
                float s = Math.Clamp(_liftT / LiftFor, 0f, 1f), ease = s * s * (3f - 2f * s);
                float fromY = GroundAt(_liftX), toY = ground - slot.Y * _t - shake;
                StampBale(fb, _liftX + (AlongX(slot.X) - _liftX) * ease, fromY + (toY - fromY) * ease - 0.45f * _t * MathF.Sin(MathF.PI * s));
            }
        }
        _tractor[d].Draw(fb, left, top - shake, 1f, _stencil);

        // The wheels. Rolling right turns them clockwise; rolling left, the other way, which is the pages run backwards.
        void Wheel(Sprite[] pages, float r, float unitsX)
        {
            float turn = _rolled / r / (MathF.PI / 3f);                                 // in sixths of a revolution
            int pg = (int)((turn - MathF.Floor(turn)) * TractorArt.WheelPages) % TractorArt.WheelPages;
            if (_left) pg = (TractorArt.WheelPages - pg) % TractorArt.WheelPages;
            Sprite sp = pages[pg];
            sp.Draw(fb, (int)MathF.Round(AlongX(unitsX) - sp.Width / 2f), (int)MathF.Round(ground - r - sp.Height / 2f), 1f, _stencil);
        }
        if (_job == Job.Baling) Wheel(_small, TractorArt.BalerR * _t, TractorArt.BalerWheelX);
        else { Wheel(_small, TractorArt.CartR * _t, TractorArt.CartX0); Wheel(_small, TractorArt.CartR * _t, TractorArt.CartX1); }
        Wheel(_rear, TractorArt.RearR * _t, TractorArt.RearX);
        Wheel(_front, TractorArt.FrontR * _t, TractorArt.FrontX);

        // The headlamp's glow, at the nose.
        _lamp.DrawCentered(fb, AlongX(TractorArt.LampX) + Dir * _t * 0.15f, ground - 0.80f * _t, 0.65f, _stencil);
    }
}
