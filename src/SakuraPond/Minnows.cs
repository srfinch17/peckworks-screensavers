using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Schools of minnows: dozens of tiny fish just under the surface, moving as
/// one, and the dragonfly's playthings.
///
/// WHAT A SCHOOL OF MINNOWS DOES (watch the shallows of any pond): the fish
/// hang close together, all facing much the same way, fidgeting with small
/// quick darts and then holding still. When the school turns, it turns
/// TOGETHER, and the silver sides of the fish catch the light as they roll
/// into the turn, so a flash runs through the school. Anything that touches
/// the surface (an insect, a crumb, a dragonfly's tail) sends out rings, and
/// the school rushes to the spot in a body and swirls there, every fish
/// snapping at nothing, then loses interest and drifts away. A big fish
/// cruising underneath scatters them like a thrown handful of rice, and a
/// moment later they are a school again.
///
/// HOW IT IS BUILT: each minnow is a tiny bending Body (Body.cs), its spine
/// laid along the trail its head has swum, exactly as the koi are, so it
/// turns by bending and never slides or flips. The school is three rules per
/// fish each frame, the classic flocking rules: stay with the school (head for
/// the school's shared target), face the way your neighbours face, and keep
/// a little room between you and the next fish. Add "flee a koi", "turn away
/// from the bank", and a shared MOOD for the whole school (drifting, rushing
/// to a lure, milling round it), and the dozen fish move as one creature.
///
/// The dragonfly's game: Dragonflies.cs asks where the nearest school is,
/// taps the water near it (Lure), and the school comes running.
/// </summary>
internal sealed class Minnows
{
    private readonly School[] _schools;
    private readonly float _u;

    public Minnows(Pond pond, int schools, bool[] where, Ripples ripples, Random rng)
    {
        _u = pond.U;
        _schools = new School[Math.Max(0, schools)];
        for (int i = 0; i < _schools.Length; i++) _schools[i] = new School(pond, where, ripples, rng, 12 + rng.Next(8));
    }

    /// <param name="koiHeads">Points along each koi this frame (head and middle), in screen pixels: the minnows keep clear of them.</param>
    public void Update(float dt, PointF[] koiHeads)
    {
        foreach (School s in _schools) s.Update(dt, koiHeads);
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (School s in _schools) s.Draw(fb);
    }

    private School? _lastLured;

    /// <summary>Something touched the water here (screen pixels): the nearest school, if near enough, rushes to it.</summary>
    public void Lure(float x, float y)
    {
        School? best = null;
        float bestD = _u * _u * 0.8f * 0.8f;
        foreach (School s in _schools)
        {
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s; }
        }
        best?.Lure(x, y);
        if (best != null) _lastLured = best;
    }

    /// <summary>
    /// The middle of the school a dragonfly should go and tease next (screen pixels), or null when
    /// there are no minnows: the nearest one, except that the school teased last time waits its turn
    /// (with one dragonfly darting about near one school, that school got every game).
    /// </summary>
    public PointF? NearestSchool(float x, float y)
    {
        PointF? best = null;
        float bestD = float.MaxValue;
        foreach (School s in _schools)
        {
            if (s == _lastLured && _schools.Length > 1) continue;
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s.Centre; }
        }
        return best;
    }
}

/// <summary>One school: a dozen or so minnows and the mood they share.</summary>
internal sealed class School
{
    private enum Mood { Drift, Rush, Mill }

    private readonly Pond _pond;
    private readonly Random _rng;
    private readonly Ripples _ripples;
    private readonly Minnow[] _fish;
    private readonly float _u;
    private Mood _mood = Mood.Drift;
    private float _nipTimer;             // milling fish nip at the surface now and then: a fizz of tiny rings
    private float _tx, _ty;              // where the school is heading, ground pixels
    private float _lx, _ly;              // the lure: where the water was touched, ground pixels
    private float _timer, _orbit;
    private float _cx, _cy;              // the school's middle, ground pixels

    /// <summary>The middle of the school, screen pixels.</summary>
    public PointF Centre { get; private set; }

    public School(Pond pond, bool[] where, Ripples ripples, Random rng, int count)
    {
        _pond = pond;
        _rng = rng;
        _ripples = ripples;
        _u = pond.U;
        // A home in open water with room around it, and the fish gathered close round it, all facing much the same way.
        PointF home = pond.Centre;
        for (int tries = 0; tries < 300; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _u * 0.12f || !InView(x, y)) continue;
            home = new PointF(x, y);
            break;
        }
        (_cx, _cy) = Minnow.ToGround(pond, home.X, home.Y);
        float heading = (float)(rng.NextDouble() * MathF.Tau);
        _fish = new Minnow[count];
        for (int i = 0; i < count; i++)
        {
            float a = (float)(rng.NextDouble() * MathF.Tau), d = _u * 0.045f * MathF.Sqrt((float)rng.NextDouble());
            _fish[i] = new Minnow(pond, where, rng, _cx + MathF.Cos(a) * d, _cy + MathF.Sin(a) * d, heading + 0.5f * ((float)rng.NextDouble() - 0.5f));
        }
        Centre = home;
        NewTarget();
    }

    /// <summary>Open water that no branch hides (a school idling under the blossom would be a school nobody sees).</summary>
    private bool InView(float x, float y)
    {
        int i = (int)y * _pond.Width + (int)x;
        return _pond.Water[i] && _pond.Open[i];
    }

    /// <summary>Picks somewhere new in open water, in view, for the school to idle toward.</summary>
    private void NewTarget()
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _u * 0.1f || !InView(x, y)) continue;
            (_tx, _ty) = Minnow.ToGround(_pond, x, y);
            break;
        }
        _timer = 14 + 16 * (float)_rng.NextDouble();
    }

    public void Lure(float x, float y)
    {
        (_lx, _ly) = Minnow.ToGround(_pond, x, y);
        _mood = Mood.Rush;
        _timer = 5;
    }

    public void Update(float dt, PointF[] koiHeads)
    {
        // Where the school is now: the mean of its fish.
        float sx = 0, sy = 0;
        foreach (Minnow f in _fish) { sx += f.X; sy += f.Y; }
        _cx = sx / _fish.Length; _cy = sy / _fish.Length;
        Centre = Minnow.ToScreen(_pond, _cx, _cy);

        // The shared mood, and how hard the school swims in it (body lengths a second).
        float pace;
        _timer -= dt;
        switch (_mood)
        {
            case Mood.Rush:
                // Straight at the lure, flat out; there (or bored) it starts to mill.
                _tx = _lx; _ty = _ly;
                pace = 9f;
                float dx = _cx - _lx, dy = _cy - _ly;
                if (dx * dx + dy * dy < _u * _u * 0.035f * 0.035f || _timer <= 0)
                {
                    _mood = Mood.Mill;
                    _timer = 6 + 4 * (float)_rng.NextDouble();
                    _orbit = MathF.Atan2(dy, dx);
                }
                break;
            case Mood.Mill:
                // Round and round the spot where the rings were: a swirl, each fish on its own place
                // in a ring that turns about the spot (the ring is handed out below), then it loses interest.
                // Now and then one nips at the surface where the dragonfly touched (they take anything
                // that lands for food), leaving a ring far smaller than the dragonfly's.
                _orbit = (_orbit + 1.6f * dt) % MathF.Tau;
                pace = 3f;
                _nipTimer -= dt;
                if (_nipTimer <= 0)
                {
                    PointF nose = _fish[_rng.Next(_fish.Length)].Nose;
                    _ripples.Add(nose.X, nose.Y, 0.12f);
                    _nipTimer = 0.3f + 0.6f * (float)_rng.NextDouble();
                }
                if (_timer <= 0) { _mood = Mood.Drift; NewTarget(); }
                break;
            default:
                pace = 1.3f;
                float gx = _cx - _tx, gy = _cy - _ty;
                if (_timer <= 0 || gx * gx + gy * gy < _u * _u * 0.05f * 0.05f) NewTarget();
                break;
        }
        foreach (Minnow f in _fish)
        {
            float tx = _tx, ty = _ty;
            if (_mood == Mood.Mill)
            {
                float a = _orbit + f.RingPlace, r = _u * 0.045f * f.RingRadius;
                tx = _lx + MathF.Cos(a) * r; ty = _ly + MathF.Sin(a) * r;
            }
            f.Update(dt, _fish, tx, ty, pace, koiHeads);
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Minnow f in _fish) f.Draw(fb);
    }
}

/// <summary>
/// One minnow: a tiny fish seen from above, an olive back with a dark line
/// down it and silver sides that flash when it rolls into a turn, a small
/// forked tail. Drawn along a short bending spine like the koi (Body.cs).
/// </summary>
internal sealed class Minnow : Body
{
    private const int Points = 7;                  // six along the body, one for the tail fin
    private const float FinEnd = 1.25f;

    private readonly Pond _pond;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly float _len, _wide, _u;
    private readonly float _jitter;                // this fish swims a little faster or slower than its school

    /// <summary>Its own place in the swirl round a lure: where on the ring (radians) and how far out (0.5 to 1).</summary>
    public float RingPlace { get; }
    public float RingRadius { get; }
    private float _heading, _speed, _amp, _phase, _flash;
    private float _fidget, _burst;                 // when it next twitches, and how long the twitch lasts
    private readonly PointF[] _trail = new PointF[24];
    private int _trailHead, _trailCount;

    /// <summary>Where its nose is, ground pixels.</summary>
    public float X { get; private set; }
    public float Y { get; private set; }

    /// <summary>Where its nose is on the screen.</summary>
    public PointF Nose => Spine[0];

    public Minnow(Pond pond, bool[] where, Random rng, float x, float y, float heading) : base(Points)
    {
        _pond = pond;
        _where = where;
        _rng = rng;
        _u = pond.U;
        _len = _u * (0.017f + 0.006f * (float)rng.NextDouble());
        _wide = _len * 0.1f;
        _jitter = 0.85f + 0.3f * (float)rng.NextDouble();
        RingPlace = (float)(rng.NextDouble() * MathF.Tau);
        RingRadius = 0.75f + 0.25f * (float)rng.NextDouble();    // a narrow band, so the swirl reads as a ring and not a cloud
        X = x; Y = y;
        _heading = heading;
        _speed = _len * 1.3f;
        _phase = (float)(rng.NextDouble() * MathF.Tau);
        _fidget = (float)rng.NextDouble() * 3;
        for (int i = 0; i < Points; i++) Along[i] = i < 6 ? i / 5f : FinEnd;
        for (int i = 12; i >= 0; i--)
            Remember(new PointF(X - MathF.Cos(_heading) * _len * i / 8f, Y - MathF.Sin(_heading) * _len * i / 8f));
        LaySpine();
    }

    // The fish lives in "ground" coordinates (as if seen straight down); the screen squashes them (see Koi.cs).
    public static (float, float) ToGround(Pond p, float x, float y) => (x, p.Centre.Y + (y - p.Centre.Y) / Ripples.Squash);
    public static PointF ToScreen(Pond p, float gx, float gy) => new(gx, p.Centre.Y + (gy - p.Centre.Y) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <param name="pace">How hard the school is swimming, body lengths a second.</param>
    public void Update(float dt, Minnow[] school, float tx, float ty, float pace, PointF[] koiHeads)
    {
        // ---- The flocking rules, summed into one "which way" vector ----
        // 0. Keep going: a fish prefers the way it is already swimming, so the pushes below
        //    bend its path rather than flipping it (without this, two fish passing close
        //    shoved each other's heading round every frame and they spun on the spot).
        float sx = MathF.Cos(_heading), sy = MathF.Sin(_heading);
        // 1. Stay with the school: toward its target, harder the further off.
        float dx = tx - X, dy = ty - Y, dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > 1) { float k = MathF.Min(1.5f, dist / (_u * 0.08f)); sx += dx / dist * k; sy += dy / dist * k; }
        // 2. Face the way the neighbours face. 3. Keep a little room.
        float ax = 0, ay = 0; int n = 0;
        float near = _u * 0.08f, close = _len * 1.4f;
        foreach (Minnow o in school)
        {
            if (o == this) continue;
            float ox = o.X - X, oy = o.Y - Y, d2 = ox * ox + oy * oy;
            if (d2 > near * near) continue;
            ax += MathF.Cos(o._heading); ay += MathF.Sin(o._heading); n++;
            if (d2 < close * close && d2 > 0.01f)
            {
                float d = MathF.Sqrt(d2), push = 1.2f * (1 - d / close);
                sx -= ox / d * push; sy -= oy / d * push;
            }
        }
        if (n > 0) { sx += ax / n * 1.2f; sy += ay / n * 1.2f; }
        // 4. A koi underneath: flee, fast.
        bool fleeing = false;
        float scare = _u * 0.11f;
        foreach (PointF k in koiHeads)
        {
            (float kx, float ky) = ToGround(_pond, k.X, k.Y);
            float ox = X - kx, oy = Y - ky, d2 = ox * ox + oy * oy;
            if (d2 > scare * scare || d2 < 0.01f) continue;
            float d = MathF.Sqrt(d2), push = 4f * (1 - d / scare);
            sx += ox / d * push; sy += oy / d * push;
            fleeing = true;
        }
        // 5. The bank (or a rock, or the edge of the view): turn toward more open water.
        PointF here = ToScreen(_pond, X, Y);
        float roomHere = _pond.RoomAt(here.X, here.Y), wall = _u * 0.04f;
        if (roomHere < wall)
        {
            PointF ow = _pond.OpenWay(here.X, here.Y);
            float l = MathF.Sqrt(ow.X * ow.X + ow.Y * ow.Y);
            if (l > 1e-4f) { float push = 2f * (1 - roomHere / wall); sx += ow.X / l * push; sy += ow.Y / l * push; }
        }

        // ---- Turn toward it, and flash as it rolls ----
        // How sharply depends on how fast it is going: a fish turns on a circle no smaller than
        // about its own length (tighter, and the body curls into a C with its nose off the line
        // it is moving along). Slow fish turn slowly; a rushing fish can whip round at 4 radians a second.
        float want = sx * sx + sy * sy > 1e-6f ? MathF.Atan2(sy, sx) : _heading;
        float most = MathF.Min(4f, MathF.Max(1.2f, _speed / (_len * 0.8f)));
        float turn = Math.Clamp(Wrap(want - _heading), -most * dt, most * dt);
        _heading = Wrap(_heading + turn);
        float rolling = Math.Clamp(MathF.Abs(turn) / dt / 3f, 0, 1);
        _flash += (rolling - _flash) * (1 - MathF.Exp(-dt * 8));

        // ---- Speed: the school's pace, a twitch now and then when idling, flat out from a koi ----
        _fidget -= dt;
        if (_fidget <= 0) { _burst = 0.3f; _fidget = 1 + 3 * (float)_rng.NextDouble(); }
        _burst -= dt;
        float target = _len * pace * _jitter * (_burst > 0 && pace < 2 ? 3f : 1f);
        if (fleeing) target = _len * 10f;
        _speed += (target - _speed) * (1 - MathF.Exp(-dt * (target > _speed ? 5f : 2.5f)));   // quick off the mark, slower to settle

        // ---- Move, never onto land ----
        float step = _speed * dt;
        PointF next = ToScreen(_pond, X + MathF.Cos(_heading) * step, Y + MathF.Sin(_heading) * step);
        float roomNext = _pond.RoomAt(next.X, next.Y);
        if (roomNext > 2 || roomNext >= roomHere) { X += MathF.Cos(_heading) * step; Y += MathF.Sin(_heading) * step; }
        PointF last = _trail[_trailHead];
        if ((last.X - X) * (last.X - X) + (last.Y - Y) * (last.Y - Y) > (_len / 10) * (_len / 10)) Remember(new PointF(X, Y));

        // ---- The tail beat: quick, quicker still when darting, a little wider when working hard.
        // A minnow's body stays nearly straight; only the last third flicks (a wide wave down the
        // whole body made them wriggle like worms).
        _phase = (_phase + MathF.Tau * (2.5f + 6f * MathF.Min(1, _speed / (_len * 7))) * dt) % MathF.Tau;
        float wantAmp = _len * (0.02f + 0.05f * MathF.Min(1, _speed / (_len * 5)));
        _amp += (wantAmp - _amp) * (1 - MathF.Exp(-dt * 4));
        LaySpine();
    }

    /// <summary>Lays the spine along the trail behind the head, adds the swimming wave, squashes to the screen (the koi's method, shorter).</summary>
    private void LaySpine()
    {
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(X, Y), b = _trail[slot];
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
            if (left <= 0 && travelled + segLen < want)
            {
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(_heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(_heading);
                mid[i] = new PointF(b.X + ex * (want - travelled - segLen), b.Y + ey * (want - travelled - segLen));
            }
            else
            {
                float f = segLen > 1e-4f ? (want - travelled) / segLen : 0;
                mid[i] = new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
            }
        }
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            float sway = _amp * (0.03f + s * s * s) * MathF.Sin(MathF.Tau * 0.9f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s);
            PointF l = ToScreen(_pond, cx + nx * hw, cy + ny * hw), r = ToScreen(_pond, cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
    }

    /// <summary>A blunt little head, widest at the shoulders, tapering to a thin tail stalk, then the fin fanning out.</summary>
    private static float Profile(float s)
    {
        if (s < 0.22f) { float k = (0.22f - s) / 0.25f; return MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        if (s <= 1f) return 1 - 0.7f * (s - 0.22f) / 0.78f;
        return 0.35f + 0.75f * (s - 1) / (FinEnd - 1);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        if (s > 1f)
        {
            // The tail fin: see-through, forked (the middle ends sooner).
            float end = FinEnd - 0.1f * (1 - av);
            if (s > end) { r = g = b = 0; alpha = 0; return false; }
            r = 150; g = 160; b = 150; alpha = 0.5f;
            return true;
        }
        // Back: a dark line down the middle, olive beside it, silver flanks that go white as it rolls into a turn.
        float stripe = Math.Clamp((0.3f - av) * 8, 0, 1), flank = Math.Clamp((av - 0.55f) * 5, 0, 1);
        r = (int)(120 + (62 - 120) * stripe + (196 - 120) * flank);
        g = (int)(128 + (70 - 128) * stripe + (204 - 128) * flank);
        b = (int)(104 + (60 - 104) * stripe + (200 - 104) * flank);
        if (_flash > 0.01f && flank > 0)
        {
            float f = _flash * flank;
            r += (int)((238 - r) * f); g += (int)((242 - g) * f); b += (int)((238 - b) * f);
        }
        alpha = 0.92f;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        const float murk = 0.2f;                   // just under the surface: a little of the water's colour, no haze
        Paint(fb, _where, _pond.Sun, murk, MathF.Max(0.8f, _u * 0.0007f), _pond.Pixels);   // hazed toward the water, not toward a koi it crosses
        // Two tiny dark eyes either side of the head.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.6f, _len * 0.035f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ex / el * HalfWidth[1] * 0.5f - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ey / el * HalfWidth[1] * 0.5f + ex / el * HalfWidth[1] * 0.7f * side);
            Oval(fb, _where, c, 0, er, er, 20, 22, 24, 0.9f, murk, _pond.Sun, rays: false);
        }
    }
}
