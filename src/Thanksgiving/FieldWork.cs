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
/// PECKWORKS_FIELD=now brings the machine on 1 s after the start (haulnow: the wagon); the log then also has "tractor bump k x .. front|rear|implement" lines
/// (the moment each wheel passes a bump's middle) and rh/hp/iw/tp/ip columns (rear wheel, hitch pin, implement wheel heights in pixels, tractor and implement pitch in degrees).
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
    private readonly Sprite[][] _tractor = new Sprite[2][];       // [dir][pitch page]: [0] heads right, [1] heads left; the body pitches on its two wheels
    private readonly Sprite[][][] _baler = new Sprite[2][][];     // [dir][pitch page][gate page]
    private readonly Sprite[][] _wagon = new Sprite[2][];         // [dir][pitch page]; its bales are stamped on separately
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
    private readonly float[] _bump;                 // the lane's bumps: pixels UP at each whole x (a dip is negative), fixed for the life of the scene
    private readonly List<float> _centres = new();  // where each bump's middle is (a dip's too), for the log
    private float _prevF, _prevR, _prevW; private bool _prevOk;   // last frame's wheel x's, to log the moment each wheel crosses a bump's middle

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
            _tractor[d] = new Sprite[TractorArt.PitchPages];
            for (int p = 0; p < TractorArt.PitchPages; p++) _tractor[d][p] = TractorArt.Tractor(_t, flip, TractorArt.PitchOf(p));
            _baler[d] = new Sprite[TractorArt.PitchPages][];
            _wagon[d] = new Sprite[TractorArt.PitchPages];
            for (int p = 0; p < TractorArt.PitchPages; p++)
            {
                _baler[d][p] = new Sprite[GatePages];
                for (int i = 0; i < GatePages; i++) _baler[d][p][i] = TractorArt.Baler(_t, flip, i / (float)(GatePages - 1), TractorArt.PitchOf(p));
                _wagon[d][p] = TractorArt.Wagon(_t, flip, TractorArt.PitchOf(p));
            }
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
        // THE LANE'S BUMPS (his line: "i want the tractor to go over a bump and then the baler does too"). Rolled ONCE, on
        // dice of their own taken last, so every pass meets the same bumps in the same places and no older roll moves.
        var bumps = new Random(_rng.Next());
        _bump = new float[s.Width + 2];
        BuildBumps(bumps);
        // Test aids: PECKWORKS_FIELD=haul starts with a full set of loose bales, so the first pass is the wagon's; =now (or
        // =haulnow) brings the machine on a second after the start instead of after six to fourteen.
        string fieldAid = Environment.GetEnvironmentVariable("PECKWORKS_FIELD") ?? "";
        if (fieldAid == "now" || fieldAid == "haulnow") _away = 1f;
        if (fieldAid == "haul" || fieldAid == "haulnow")
            for (int i = 0; i < BaleCap; i++) _bales.Add(s.Width * (0.08f + 0.84f * (i + 0.5f) / BaleCap));
    }

    /// <summary>
    /// FEYNMAN VERSION: the lane is not a ruled line, it has a few lumps. Four to seven humps and one or two dips along the
    /// screen's width, each a raised cosine (a smooth hill: zero at its two feet, flat on top) about one big wheel's
    /// diameter wide and one to three pixels high at 1440p (scaled with U, never under a pixel, never over 6 percent of a
    /// tractor unit). Nothing is DRAWN for them: they are felt, not seen, the way you feel a pothole through a car seat.
    /// Spaced at least a wheel and a half apart so each is one clear bump for each wheel that crosses it.
    /// </summary>
    private void BuildBumps(Random r)
    {
        float width = _t;                                        // one big wheel's diameter (the rear wheel is one unit across)
        float scale = _s.U / 1440f, cap = Math.Max(1f, 0.12f * _t);   // up to 12 percent of a tractor unit: at 6 (1 to 3 px) the helper's own strip read as "small pixel steps, not a big rock"
        int humps = 4 + r.Next(4), dips = 1 + r.Next(2);
        for (int n = 0; n < humps + dips; n++)
        {
            bool dip = n >= humps;
            float c = 0;
            for (int tries = 0; tries < 60; tries++)               // scatter by rejection with a spacing test
            {
                c = _s.Width * (0.03f + 0.94f * (float)r.NextDouble());
                bool clear = true;
                foreach (float o in _centres) if (MathF.Abs(o - c) < 1.5f * width) { clear = false; break; }
                if (clear) break;
            }
            float h = Math.Min(cap, Math.Max(1f, (dip ? 1.5f + 1.5f * (float)r.NextDouble() : 2f + 3f * (float)r.NextDouble()) * scale));   // humps 2 to 5 px at 1440p, dips 1.5 to 3
            if (dip) h = -h;
            _centres.Add(c);
            int x0 = Math.Max(0, (int)(c - width / 2)), x1 = Math.Min(_bump.Length - 1, (int)(c + width / 2) + 1);
            for (int x = x0; x <= x1; x++)
            {
                float d = x - c;
                if (MathF.Abs(d) < width / 2) _bump[x] += h * 0.5f * (1f + MathF.Cos(2f * MathF.PI * d / width));
            }
        }
        _centres.Sort();
    }
    /// <summary>The lane's bump under screen x: pixels up (a dip is negative), blended between whole pixels.</summary>
    private float BumpAt(float x)
    {
        float f = Math.Clamp(x, 0f, _s.Width - 1f);
        int i = (int)f;
        return _bump[i] + (_bump[i + 1] - _bump[i]) * (f - i);
    }
    private float _ground;                      // the lane under the machine's middle this frame (set by Seat)
    /// <summary>
    /// The lane a wheel stands on at screen x: the lane under the machine's middle, raised by the bump. A wheel's contact
    /// point is always this. (The painted lane's own slope, up to 6 degrees over a wheelbase on this ridge, is carried by
    /// moving the whole machine along it, as before; pitching the sheets with it would need three times the pages.)
    /// </summary>
    private float LaneAt(float x) => _ground - BumpAt(x);

    /// <summary>
    /// How the machine sits on the lane THIS frame. Heights are pixels up from `Ground` (the lane under the machine's middle).
    /// The tractor's body is stood on its two wheels: it rises with the rear wheel and pitches by the line between the
    /// two. Its hitch pin is a point on that body, so it rises and tips with the body. The implement is pinned to the
    /// pin at its front and stands on ITS OWN wheel (the wagon's two, as their middle) at the back, so its pitch is the
    /// angle from the pin down to its wheel's contact: when the tractor's wheel meets a bump the pin lifts a little
    /// at once, but the implement's wheel meets that same bump later, a wheelbase and a half behind, which is the gag.
    /// </summary>
    private struct Ride { public float Ground, Hr, Hf, Hp, Hw, TractorDeg, ImplDeg; public int TP, PP; }
    private Ride Seat()
    {
        _ground = GroundAt(_x + _len / 2f);
        var r = new Ride { Ground = _ground };
        r.Hr = r.Ground - LaneAt(AlongX(TractorArt.RearX));
        r.Hf = r.Ground - LaneAt(AlongX(TractorArt.FrontX));
        float wheelbase = (TractorArt.FrontX - TractorArt.RearX) * _t;
        float noseUp = MathF.Asin(Math.Clamp((r.Hf - r.Hr) / wheelbase, -0.25f, 0.25f)) * 180f / MathF.PI;
        r.TP = TractorArt.PageFor(-noseUp);                         // a rising nose is a falling tail: the sheet's sign is tail-up
        r.TractorDeg = TractorArt.PitchOf(r.TP);
        var pin = TractorArt.About(TractorArt.RearX, TractorArt.RearR, TractorArt.HitchX, TractorArt.HitchY, r.TractorDeg);
        r.Hp = r.Hr + (pin.Y - TractorArt.HitchY) * _t;
        float wx, hw;
        if (_job == Job.Hauling)
        {
            wx = 0.5f * (TractorArt.CartX0 + TractorArt.CartX1);
            hw = 0.5f * (r.Ground - LaneAt(AlongX(TractorArt.CartX0)) + r.Ground - LaneAt(AlongX(TractorArt.CartX1)));
        }
        else { wx = TractorArt.BalerWheelX; hw = r.Ground - LaneAt(AlongX(wx)); }
        r.Hw = hw;
        float arm = (TractorArt.HitchX - wx) * _t;
        float tailUp = MathF.Asin(Math.Clamp((hw - r.Hp) / arm, -0.25f, 0.25f)) * 180f / MathF.PI;   // the wheel higher than the pin lifts the tail
        r.PP = TractorArt.PageFor(tailUp);
        r.ImplDeg = TractorArt.PitchOf(r.PP);
        return r;
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
        if (_log != null)
        {
            string ride = "";
            if (_job != Job.Away)
            {
                var rd = Seat();
                float fx = AlongX(TractorArt.FrontX), rx = AlongX(TractorArt.RearX), wx = AlongX(_job == Job.Hauling ? 0.5f * (TractorArt.CartX0 + TractorArt.CartX1) : TractorArt.BalerWheelX);
                ride = $" rh {rd.Hr:F2} hp {rd.Hp:F2} iw {rd.Hw:F2} tp {rd.TractorDeg:F1} ip {rd.ImplDeg:F1} lane {LaneAt(wx):F2} w {wx:F1}";
                // The moment each wheel's middle passes a bump's middle: the implement's time minus the tractor's is the gag.
                if (_prevOk)
                    for (int k = 0; k < _centres.Count; k++)
                    {
                        float c = _centres[k];
                        if ((_prevF - c) * (fx - c) <= 0 && _prevF != fx) _log.WriteLine($"{_time,8:F3} tractor bump {k} x {c:F0} h {BumpAt(c):F1} front");
                        if ((_prevR - c) * (rx - c) <= 0 && _prevR != rx) _log.WriteLine($"{_time,8:F3} tractor bump {k} x {c:F0} h {BumpAt(c):F1} rear");
                        if ((_prevW - c) * (wx - c) <= 0 && _prevW != wx) _log.WriteLine($"{_time,8:F3} tractor bump {k} x {c:F0} h {BumpAt(c):F1} implement");
                    }
                _prevF = fx; _prevR = rx; _prevW = wx; _prevOk = true;
            }
            else _prevOk = false;
            _log.WriteLine($"{_time,8:F3} tractor frame x {AlongX(TractorArt.RearX):F1} v {(_job == Job.Away ? 0f : Cruise * u):F2} {_job}{ride}");
        }
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
        var ride = Seat();
        float ground = ride.Ground;
        int top = (int)MathF.Round(ground) - _gy;
        int shake = (int)(_clock * 16) % 2 == 0 ? 0 : Math.Max(1, (int)(_s.U / 1100f));   // the engine's tremble: the tractor's body only, a pixel at a time
        int left = (int)MathF.Round(_x);
        int tractorLift = (int)MathF.Round(ride.Hr), pinLift = (int)MathF.Round(ride.Hp);
        int pp = ride.PP;
        float pitchDeg = ride.ImplDeg;
        // THE HITCH (his lines: "move at the hitch so they look independent of each other, but connected", and "i want the
        // tractor to go over a bump and then the baler does too when it hits the bump"). Nothing here is a wave any more:
        // the lane has bumps (BumpAt), every wheel stands on the lane under it, and Seat() works out from those wheels how
        // the tractor's body rides (up with its rear wheel, tipped by the line to its front wheel), where the hitch pin is
        // on that body, and how far the implement tips about the pin to meet its OWN wheel. The bump reaches the baler a
        // wheelbase and a half after it reached the tractor, because that is how far behind its wheel is.
        // Where a point of the implement (units, the right-heading sheet) is on screen under this pitch and pin height.
        (float X, float Y) At(float ux, float uy) { var (x, y) = TractorArt.Pitched(ux, uy, pitchDeg); return (AlongX(x), ground - ride.Hp - y * _t); }

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
            _baler[d][pp][Math.Min(GatePages - 1, (int)(open * GatePages))].Draw(fb, left, top - pinLift, 1f, _stencil);
            // The bale on its way out: while the gate is up and before it has dropped, it shows in the drum's open back.
            if (_gateT >= 0f && !_dropped && _gateT > BaleOutAt * 0.4f)
            {
                float out_ = (_gateT - BaleOutAt * 0.4f) / (BaleOutAt * 0.6f);
                var (bx, by) = At(TractorArt.BalerRearX + 0.5f - 0.5f * out_, 0f);                 // from the drum's open back, which pitches with the baler
                bx -= Dir * _baleR * 0.6f * out_;
                StampBale(fb, bx, by - _baleR * 0.9f * (1 - out_));
            }
        }
        else
        {
            // The wagon, the bales already stacked on it (they tremble with the body), then the one on its way up: it
            // leaves the ground where it stood, rises over the rail in an arc and settles into its slot, which is
            // moving with the wagon, so it glides forward as it climbs. Smoothstep, so it neither jerks off nor thuds on.
            _wagon[d][pp].Draw(fb, left, top - pinLift, 1f, _stencil);
            for (int i = 0; i < _loaded; i++)
            {
                var (bx, by) = At(TractorArt.BaleSlots[i].X, TractorArt.BaleSlots[i].Y);   // the stack pitches with the bed
                StampBale(fb, bx, by);
            }
            if (_liftT >= 0f)
            {
                var slot = TractorArt.BaleSlots[Math.Min(_loaded, TractorArt.BaleSlots.Length - 1)];
                float s = Math.Clamp(_liftT / LiftFor, 0f, 1f), ease = s * s * (3f - 2f * s);
                var (sx, sy) = At(slot.X, slot.Y);
                float fromY = GroundAt(_liftX);
                StampBale(fb, _liftX + (sx - _liftX) * ease, fromY + (sy - fromY) * ease - 0.45f * _t * MathF.Sin(MathF.PI * s));
            }
        }
        _tractor[d][ride.TP].Draw(fb, left, top - shake - tractorLift, 1f, _stencil);

        // The wheels. Rolling right turns them clockwise; rolling left, the other way, which is the pages run backwards.
        // Every wheel, tractor's or implement's, stands ON THE LANE under it (contact point = LaneAt(x), the painted lane
        // raised by the bump), so none ever floats or sinks; the bodies are what ride up and tip to meet them.
        void Wheel(Sprite[] pages, float r, float unitsX)
        {
            float turn = _rolled / r / (MathF.PI / 3f);                                 // in sixths of a revolution
            int pg = (int)((turn - MathF.Floor(turn)) * TractorArt.WheelPages) % TractorArt.WheelPages;
            if (_left) pg = (TractorArt.WheelPages - pg) % TractorArt.WheelPages;
            Sprite sp = pages[pg];
            float cx2 = AlongX(unitsX), cy2 = LaneAt(cx2) - r;
            sp.Draw(fb, (int)MathF.Round(cx2 - sp.Width / 2f), (int)MathF.Round(cy2 - sp.Height / 2f), 1f, _stencil);
        }
        if (_job == Job.Baling) Wheel(_small, TractorArt.BalerR * _t, TractorArt.BalerWheelX);
        else { Wheel(_small, TractorArt.CartR * _t, TractorArt.CartX0); Wheel(_small, TractorArt.CartR * _t, TractorArt.CartX1); }
        Wheel(_rear, TractorArt.RearR * _t, TractorArt.RearX);
        Wheel(_front, TractorArt.FrontR * _t, TractorArt.FrontX);

        // The headlamp's glow, at the nose.
        var lamp = TractorArt.About(TractorArt.RearX, TractorArt.RearR, TractorArt.LampX, 0.80f, ride.TractorDeg);   // the nose rides the body's tip
        _lamp.DrawCentered(fb, AlongX(lamp.X) + Dir * _t * 0.15f, ground - ride.Hr - shake - lamp.Y * _t, 0.65f, _stencil);
    }
}
