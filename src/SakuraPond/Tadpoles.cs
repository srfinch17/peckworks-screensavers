using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Tadpoles in the warm shallows, and the froglets they turn into.
///
/// WHAT TADPOLES DO (watch the edge of any pond in spring), IN THE ORDER THEY DO IT:
/// they hang in a loose cluster over the bed near the bank, no two facing the same way, tails
/// still or sculling in a slow lazy wriggle. Some graze: they nose up to a rock's wet flank,
/// the bank's edge or a lily pad's rim and bob their heads against it. Now and then one
/// BURSTS: it curls into a C, the whole tail whips in a fast S (about ten beats a second), the
/// body shoots a few of its own lengths and stops dead. Anything big that comes near scatters
/// the cluster in a flurry, and over the next several seconds it drifts back together.
/// Then, one at a time and minutes apart, one of them grows up: two buds at the base of the
/// tail, then hind legs folded back along it, then forelegs, the body narrowing into a frog's
/// wedge and the tail shrinking away. Now it is a FROGLET, a tiny frog. It rises to the surface
/// and swims off with frog kicks (a quick push, a long glide) over open water, shoving the
/// floating petals aside, to beyond the nearest screen edge, and is gone for good. The clutch
/// thins; when only a few are left a new hatch of tiny ones appears at the cattails' foot or a
/// pad's rim (the eggs were on the stems), so the shallows are never empty for long.
///
/// THE FOUR GATES (see the repo notes on creatures):
///   VISIBLE: they live only in shallow, sunlit water over a bed that shows, and are kept off
///   every pixel a branch or pad hides; a spot where the haze would swallow them is refused like land.
///   PERSPECTIVE: each is hung a little above the bed at the depth the bed allows, drawn shrunk,
///   hazed and sunk down the screen by that depth (Light.cs, as the koi are), squashed by the
///   view, with a small shadow hugging it on the bed.
///   BENDING: the body is a spine laid along the trail the head has swum, with the tail's wave
///   running along it; it turns by bending (and by a rigid pivot when it is nearly still),
///   never by flipping a sprite.
///   MOVES LIKE THE ANIMAL: hang, scull, burst and stop dead; a froglet kicks and glides.
///
/// WHAT IS SOLID (the pond's law: nothing clips through anything): a tadpole and a froglet are
/// far too small to be solid to a fish, so they pass under every koi, pad and turtle and
/// FLEE what comes near (they are not in the scene's surface list). They never go onto land, into
/// a rock or under the bank. A froglet at the surface is solid to the floating petals: it shoves
/// them aside as it goes.
/// </summary>
internal sealed class Tadpoles
{
    private readonly Clutch[] _clutches;
    private float[] _threats = new float[64];     // the threats in GROUND pixels, x y x y ... (grows once if ever needed)
    private int _threatCount;
    private readonly Pond _pond;

    internal Clutch[] Clutches => _clutches;

    public Tadpoles(Pond pond, int clutches, Ripples ripples, bool[] where, Random rng, Petals petals)
    {
        _pond = pond;
        _clutches = new Clutch[Math.Max(0, clutches)];
        for (int i = 0; i < _clutches.Length; i++) _clutches[i] = new Clutch(pond, ripples, where, rng);
    }

    /// <param name="threats">Screen points of everything a tadpole flees: koi heads and middles, turtle noses and shells, the snake's head, the ducks' heads.</param>
    public void Update(float dt, PointF[] threats)
    {
        if (_clutches.Length == 0) return;
        if (_threats.Length < threats.Length * 2) _threats = new float[threats.Length * 2];
        for (int i = 0; i < threats.Length; i++)
        {
            (_threats[i * 2], _threats[i * 2 + 1]) = Minnow.ToGround(_pond, threats[i].X, threats[i].Y);
        }
        _threatCount = threats.Length;
        foreach (Clutch c in _clutches) c.Update(dt, _threats, _threatCount);
    }

    /// <summary>The tadpoles and any froglet under the surface: after the koi, before the minnows.</summary>
    public void DrawUnder(FrameBuffer fb)
    {
        foreach (Clutch c in _clutches) c.Draw(fb, surface: false);
    }

    /// <summary>A froglet at the surface, swimming away: after the frogs, before the turtles up on a perch.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (Clutch c in _clutches) c.Draw(fb, surface: true);
    }

    /// <summary>A froglet at the surface shoves the floating petals in its way aside.</summary>
    public void PushPetals(Petals petals)
    {
        foreach (Clutch c in _clutches) c.PushPetals(petals);
    }
}

/// <summary>
/// One clutch: a cluster of tadpoles round a home in the shallows, the clock that decides
/// who grows up next, and the clock that hatches a new brood when the cluster has thinned.
/// The slots are made once; a froglet that leaves frees its slot for a later hatch, so
/// nothing grows however many days the saver runs.
/// </summary>
internal sealed class Clutch
{
    private const int Slots = 28;

    private readonly Pond _pond;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly float _u;
    private readonly PointF _cat;                     // the cattails' foot, screen pixels
    internal readonly Tadpole[] Fish = new Tadpole[Slots];

    /// <summary>The cluster's middle, ground pixels. The tadpoles gently keep to it; a flurry leaves it where it is, so they regroup.</summary>
    internal float HomeX, HomeY;
    internal bool HasHome;
    private float _changeTimer;                       // seconds until the next tadpole starts to grow up
    private Tadpole? _changing;
    private float _hatchTimer = -1;                   // seconds until a new hatch; -1 when none is due
    private int _hatchLeft;
    private float _hatchNext;
    private float _hatchX, _hatchY;                   // ground pixels
    private float _homeSettle;                        // after a hatch the home glides to its new place (never a jump)

    // Things the checks count (the build's instrument reads them).
    internal int Started, Finished, LeftAtEdge, DivedOut, Murked, Hatches, HatchedTotal;

    public Clutch(Pond pond, Ripples ripples, bool[] where, Random rng)
    {
        _pond = pond;
        _where = where;
        _rng = rng;
        _u = pond.U;
        _cat = pond.CattailSpot;
        for (int i = 0; i < Slots; i++) Fish[i] = new Tadpole(pond, where, rng, ripples);
        _changeTimer = 60 + 180 * (float)rng.NextDouble();      // the first one changes 60 to 240 s in
        if (!FindHome(out PointF home)) return;
        (HomeX, HomeY) = Minnow.ToGround(pond, home.X, home.Y);
        HasHome = true;
        int count = 8 + rng.Next(7);
        for (int i = 0; i < count; i++)
        {
            // Scattered loosely round the home, each on a standable spot (a cluster, with no shared heading).
            float x = HomeX, y = HomeY;
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)(rng.NextDouble() * MathF.Tau), d = _u * 0.05f * MathF.Sqrt((float)rng.NextDouble());
                float tx = HomeX + MathF.Cos(a) * d, ty = HomeY + MathF.Sin(a) * d;
                if (Tadpole.CanStand(pond, where, tx, ty)) { x = tx; y = ty; break; }
            }
            Fish[i].Birth(x, y, (float)(rng.NextDouble() * MathF.Tau), hatchling: false);
        }
    }

    /// <summary>
    /// A home in view, in shallow water with the bed 0.02 to 0.1u down and room all round,
    /// preferably within 0.3u of the cattails; then anywhere shallow; then anywhere at all
    /// that is standable (the last resort, so a clutch is never left without a home).
    /// </summary>
    private bool FindHome(out PointF home)
    {
        float room = _u * 0.05f;
        for (int tries = 0; tries < 200; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _u * 0.3f * MathF.Sqrt((float)_rng.NextDouble());
            if (Good(_cat.X + MathF.Cos(a) * d, _cat.Y + MathF.Sin(a) * d, 12.75f, 63.75f, room, out home)) return true;
        }
        for (int tries = 0; tries < 800; tries++)
            if (Good((float)_rng.NextDouble() * _pond.Width, (float)_rng.NextDouble() * _pond.Height, 12.75f, 63.75f, room, out home)) return true;
        for (int tries = 0; tries < 3000; tries++)
            if (Good((float)_rng.NextDouble() * _pond.Width, (float)_rng.NextDouble() * _pond.Height, 8f, 110f, _u * 0.025f, out home)) return true;
        // Last resort: the first standable cell of a coarse sweep.
        for (int y = 0; y < _pond.Height; y += 4)
            for (int x = 0; x < _pond.Width; x += 4)
                if (Good(x, y, 8f, 255f, 0, out home)) return true;
        home = default;
        return false;
    }

    private bool Good(float x, float y, float lo, float hi, float room, out PointF at)
    {
        at = new PointF(x, y);
        if (x < 0 || y < 0 || x >= _pond.Width || y >= _pond.Height) return false;
        int i = (int)y * _pond.Width + (int)x;
        float f = _pond.Floor[i];
        if (!_where[i] || _pond.Land[i] || f < lo || f > hi || !Tadpole.OkFloor[_pond.Floor[i]]) return false;
        return room <= 0 || _pond.RoomAt(x, y) >= room;
    }

    public void Update(float dt, float[] threats, int threatCount)
    {
        if (!HasHome) return;
        // ---- The home glides to a new place after a hatch ----
        if (_homeSettle > 0)
        {
            float k = 1 - MathF.Exp(-dt * 0.5f);
            HomeX += (_hatchX - HomeX) * k; HomeY += (_hatchY - HomeY) * k;
            _homeSettle -= dt;
        }
        // ---- Who is alive, and who is a free-swimming tadpole ----
        int tadpoles = 0;
        foreach (Tadpole t in Fish) if (t.Alive && t.Stage < 4) tadpoles++;

        // ---- Growing up: one at a time, minutes apart ----
        if (_changing != null && (!_changing.Alive || _changing.Stage >= 4))
        {
            if (_changing.Alive) { Finished++; }
            _changing = null;
            _changeTimer = 60 + 120 * (float)_rng.NextDouble();    // 60 to 180 s after the last one finished
        }
        if (_changing == null)
        {
            _changeTimer -= dt;
            if (_changeTimer <= 0)
            {
                Tadpole? pick = null;
                float most = 0;
                if (tadpoles >= 2)
                    foreach (Tadpole t in Fish)      // the biggest, grown-up-looking one (a hatchling is not ready)
                        if (t.Alive && t.Stage == 0 && t.Growth >= 0.95f && t.Size > most) { most = t.Size; pick = t; }
                if (pick != null) { pick.StartChange(); _changing = pick; Started++; }
                else _changeTimer = 5;
            }
        }

        // ---- A new hatch when the cluster has thinned ----
        if (_hatchLeft == 0)
        {
            if (tadpoles <= 3)
            {
                if (_hatchTimer < 0) _hatchTimer = 60 + 180 * (float)_rng.NextDouble();
                _hatchTimer -= dt;
                if (_hatchTimer <= 0 && PickHatchPoint())
                {
                    _hatchLeft = 8 + _rng.Next(7);
                    _hatchNext = 0;
                    _hatchTimer = -1;
                    Hatches++;
                    _homeSettle = 30;
                }
                else if (_hatchTimer <= 0) _hatchTimer = 5;
            }
            else _hatchTimer = -1;
        }
        else
        {
            _hatchNext -= dt;
            if (_hatchNext <= 0)
            {
                _hatchNext = 1 + (float)_rng.NextDouble();
                _hatchLeft--;
                for (int i = 0; i < Slots; i++)
                {
                    if (Fish[i].Alive) continue;
                    float x = _hatchX, y = _hatchY;
                    for (int tries = 0; tries < 20; tries++)
                    {
                        float a = (float)(_rng.NextDouble() * MathF.Tau), d = _u * 0.012f * (float)_rng.NextDouble();
                        if (Tadpole.CanStand(_pond, _where, _hatchX + MathF.Cos(a) * d, _hatchY + MathF.Sin(a) * d)) { x = _hatchX + MathF.Cos(a) * d; y = _hatchY + MathF.Sin(a) * d; break; }
                    }
                    Fish[i].Birth(x, y, (float)(_rng.NextDouble() * MathF.Tau), hatchling: true);
                    HatchedTotal++;
                    break;
                }
            }
        }

        // ---- Everybody moves ----
        foreach (Tadpole t in Fish)
        {
            if (!t.Alive) continue;
            t.Update(dt, HomeX, HomeY, Fish, threats, threatCount);
            if (t.Leaving != Tadpole.Exit.None)
            {
                switch (t.Leaving)
                {
                    case Tadpole.Exit.Edge: LeftAtEdge++; if (t.Dived) DivedOut++; break;
                    case Tadpole.Exit.Murk: Murked++; break;
                }
                if (t == _changing) { Finished++; _changing = null; _changeTimer = 60 + 120 * (float)_rng.NextDouble(); }
                t.Alive = false;
            }
        }
    }

    /// <summary>
    /// Where the next hatch appears: the cattails' foot (within 0.04u) or a pad's rim, whichever
    /// is nearer the home and standable and in view. False when none is (try again soon).
    /// </summary>
    private bool PickHatchPoint()
    {
        float bestD = float.MaxValue;
        bool found = false;
        (float hx, float hy) = (HomeX, HomeY);
        // The candidates: the cattails, and each pad's rim on the side facing the home.
        for (int k = -1; k < _pond.Pads.Count; k++)
        {
            PointF c; float r;
            if (k < 0) { c = _cat; r = 0; }
            else { c = _pond.Pads[k].Center; r = _pond.Pads[k].Radius; }
            (float gx, float gy) = Minnow.ToGround(_pond, c.X, c.Y);
            float dd = (gx - hx) * (gx - hx) + (gy - hy) * (gy - hy);
            if (dd >= bestD) continue;
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)(_rng.NextDouble() * MathF.Tau);
                float d = k < 0 ? _u * 0.04f * MathF.Sqrt((float)_rng.NextDouble()) : r * 1.08f + _u * 0.006f;
                // The offset is made on the SCREEN (a pad is a circle there; in ground pixels the squash
                // put the spot inside it), then taken back to the ground.
                (float tx, float ty) = Minnow.ToGround(_pond, c.X + MathF.Cos(a) * d, c.Y + MathF.Sin(a) * d);
                if (!Tadpole.CanStand(_pond, _where, tx, ty)) continue;
                bestD = dd; _hatchX = tx; _hatchY = ty; found = true;
                break;
            }
        }
        return found;
    }

    public void Draw(FrameBuffer fb, bool surface)
    {
        foreach (Tadpole t in Fish)
            if (t.Alive && t.AtSurface == surface) t.Draw(fb);
    }

    public void PushPetals(Petals petals)
    {
        foreach (Tadpole t in Fish)
            if (t.Alive && t.Stage == 4 && t.AtSurface) petals.PushAside(t.Drawn, t.DrawnHalfWidth);
    }
}

internal enum TadState { Hang, Scull, Windup, Burst, Flee, Graze, Rise, Swim, Dive }

/// <summary>
/// One tadpole, and (after it grows up) the froglet it becomes: a small bending Body on a
/// short trail, the tail's wave running along it. See the class comment of Tadpoles.
/// </summary>
internal sealed class Tadpole : Body
{
    public enum Exit { None, Edge, Murk }

    private const int Points = 7;                  // nose, middle, tail root, then four down the tail
    private const int TrailLen = 24;

    // Which bed depths are fine to hang over: the bed shows (not too deep to see) and there is water.
    // Z = 0.9 x the bed, and past 0.6 of murk the animal would be lost in the haze: refused like land.
    internal static readonly bool[] OkFloor = BuildOk();

    private static bool[] BuildOk()
    {
        var ok = new bool[256];
        for (int f = 0; f < 256; f++) ok[f] = f >= 8 && Light.Murk(0.9f * f / 255f) <= 0.6f;
        return ok;
    }

    private readonly Pond _pond;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly Ripples _ripples;
    private readonly float _u;

    // ---- identity of this life (set in Birth) ----
    public bool Alive;
    public int Stage;                              // 0 tadpole, 1 buds, 2 hind legs, 3 forelegs, 4 froglet
    public TadState State;
    public Exit Leaving;
    private float _bl0, _tl0;                      // body and tail length as a tadpole, ground pixels
    private float _burstEvery, _roam;              // temper: how often it bursts, how far it likes to be from the middle
    private bool _grazer;
    private int _seed;
    private float _grow;                           // a hatchling is half size and grows to 1 over a minute
    private float _meta, _metaDur;                 // 0 to 1 through the change, and how long it takes
    private float _frog;                           // 0 tadpole look, 1 frog look
    private float _stub;                           // after the change: the tail stub shrinking (0 to 1)

    // ---- where it is ----
    public float X, Y;                             // the nose, ground pixels
    public float Z;                                // depth, 0 surface to 1 the pond's full depth
    public float Heading, Speed;
    private readonly PointF[] _trail = new PointF[TrailLen];
    private int _trailHead, _trailCount;
    private float _stateT, _burstTimer, _age, _aliveFor;
    private float _tx, _ty;                        // where it is sculling or grazing to, ground pixels
    private float _gHeading;                       // grazing: the way it faces the thing it grazes
    private float _bobT, _noScull;       // _noScull: after a refused way, no new drift for a moment (else it flickers Hang, Scull, Hang)
    private float _phase, _amp, _freq, _curl, _turnRate;
    private float _fleeDir;

    // the froglet
    private float _kickT, _pose;
    private float _gx, _gy;                        // the goal beyond the edge, ground pixels
    private int _planEdge = -1;
    private int _side = 1;
    private float _dive, _checkT, _checkX, _checkY, _stuck;
    private bool _deep;
    private bool _dived;

    // ---- the drawn picture ----
    private readonly float[] _pos = new float[Points];      // ground distance of each spine point from the nose, before depth scaling
    private readonly float[] _murkAt = new float[Points];
    private float _bodyEnd;                        // s where the body ends and the tail begins
    private float _tailAlpha = 1;
    private float _shadowAlpha, _shadowDx, _shadowDy, _shadowSoft;
    private float _scale = 1;
    private float _bl, _tl;                        // body and tail length now, ground px, before depth scaling and growth
    private float _legScale;
    private float _eyeR;

    public Tadpole(Pond pond, bool[] where, Random rng, Ripples ripples) : base(Points)
    {
        _pond = pond; _where = where; _rng = rng; _ripples = ripples; _u = pond.U;
    }

    public bool AtSurface => Stage == 4 && Z <= 0.03f;
    public bool Dived => _dived;
    public float Growth => _grow;
    public float Size => _bl0 + _tl0;
    public bool Froglet => Stage == 4;
    /// <summary>Total length now, ground pixels (the unit its speeds are in).</summary>
    private float Len => (_bl + _tl) * _grow;
    public PointF Nose => Spine[0];
    public bool Gone => !Alive;

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private float R() => (float)_rng.NextDouble();

    /// <summary>Born (or hatched) at this ground spot, facing this way.</summary>
    public void Birth(float x, float y, float heading, bool hatchling)
    {
        float t0 = _u * (0.014f + 0.006f * R());
        _bl0 = t0 * 0.4f; _tl0 = t0 * 0.6f;        // the tail is one and a half times the body
        _burstEvery = 2 + 10 * R();
        _roam = _u * (0.02f + 0.035f * R());
        _grazer = R() < 0.45f;
        _seed = _rng.Next();
        _grow = hatchling ? 0.5f : 1f;
        Stage = 0; _meta = 0; _frog = 0; _stub = 0; _tailAlpha = 1; _legScale = 0;
        State = TadState.Hang; Leaving = Exit.None;
        X = x; Y = y; Heading = heading; Speed = 0;
        _stateT = 0.5f + 3 * R();
        _burstTimer = 1 + _burstEvery * R();
        _age = 0; _aliveFor = 0; _phase = R() * MathF.Tau; _amp = 0; _freq = 1.2f; _curl = 0; _turnRate = 0;
        _dived = false; _deep = false; _dive = 0;
        Dims();
        float f = FloorFrac(X, Y);
        Z = 0.9f * f;
        _trailHead = 0; _trailCount = 0;
        float step = Len / 10;
        for (int i = 12; i >= 0; i--)
            Remember(new PointF(X - MathF.Cos(Heading) * step * i, Y - MathF.Sin(Heading) * step * i));
        Alive = true;
        LaySpine();
    }

    /// <summary>The change begins: the buds, then everything else (see Stage).</summary>
    public void StartChange()
    {
        Stage = 1; _meta = 0; _metaDur = 90 + 60 * R();
    }

    // ================================================================ the world

    public static bool CanStand(Pond p, bool[] where, float gx, float gy)
    {
        PointF s = Minnow.ToScreen(p, gx, gy);
        int ix = (int)s.X, iy = (int)s.Y;
        if (s.X < 0 || s.Y < 0 || ix >= p.Width || iy >= p.Height) return false;
        int i = iy * p.Width + ix;
        if (!where[i] || p.Land[i] || !OkFloor[p.Floor[i]]) return false;
        // Where the animal is DRAWN (sunk down the screen by its depth) must be in view too, or it
        // hangs half behind the bank's edge and is cut off (5 of 14 hidden on one seed's first frame).
        int sy = (int)(s.Y + Light.SinkPx(0.9f * p.Floor[i] / 255f, p.U));
        return sy < p.Height && where[sy * p.Width + ix];
    }

    private bool CanStand(float gx, float gy) => CanStand(_pond, _where, gx, gy);

    /// <summary>The bed under a ground spot, as a fraction of the pond's full depth (0 at the edge).</summary>
    private float FloorFrac(float gx, float gy)
    {
        PointF s = Minnow.ToScreen(_pond, gx, gy);
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Floor[iy * _pond.Width + ix] / 255f;
    }

    /// <summary>A froglet's rule: free water (a pad, a rock, the log, land are walls) or, once hemmed in, anything but land. Off the screen is free: the pond goes on.</summary>
    private bool CanSwim(float gx, float gy)
    {
        PointF s = Minnow.ToScreen(_pond, gx, gy);
        int ix = (int)s.X, iy = (int)s.Y;
        if (s.X < 0 || s.Y < 0 || ix >= _pond.Width || iy >= _pond.Height) return true;
        int i = iy * _pond.Width + ix;
        return _deep ? !_pond.Land[i] : _pond.Water[i] && !_pond.Land[i];
    }

    /// <summary>Is the way ahead on this heading standable, for a tadpole (nose and halfway to reach)?</summary>
    private bool Clear(float h, float reach, float slope = 0.5f)
    {
        float c = MathF.Cos(h), s = MathF.Sin(h);
        if (!CanStand(X + c * reach * 0.5f, Y + s * reach * 0.5f) || !CanStand(X + c * reach, Y + s * reach)) return false;
        // The bed rising under the way ahead lifts the tadpole (it hangs 0.9 of the bed deep), and a
        // lift moves the PICTURE up the screen. Heading down the screen up a slope, the two cancel and
        // a burst is seen to go nowhere (cos -1 at 5 px/s in the first timeline). So the way ahead must
        // still move the picture along the way it is going by at least half of what it should.
        float z0 = 0.9f * FloorFrac(X, Y);
        for (int k = 1; k <= 4; k++)        // at four points along the way, so a steep step early on is not averaged away
        {
            float d = reach * k / 4f;
            float z1 = 0.9f * FloorFrac(X + c * d, Y + s * d);
            float up = (z0 - z1) * Pond.DepthU * _u * Light.Sink;       // pixels the picture rises over the way so far
            float fx = c * d, fy = s * d * Ripples.Squash;
            if (fx * fx + (fy - up) * fy < slope * (fx * fx + fy * fy)) return false;
        }
        return true;
    }

    private static readonly float[] Offsets = [0, 0.4f, 0.8f, 1.3f, 1.9f, 2.6f];

    /// <summary>The wanted heading, or the nearest to it either side that is clear for "reach" ahead; NaN if every one is blocked.</summary>
    private float FreeHeading(float want, float reach, float slope = 0.5f)
    {
        if (Clear(want, reach, slope)) return want;
        for (int k = 1; k < Offsets.Length; k++)
        {
            float a = Offsets[k];
            if (Clear(want + a, reach, slope)) return want + a;
            if (Clear(want - a, reach, slope)) return want - a;
        }
        return float.NaN;
    }

    // ================================================================ size and look

    /// <summary>Works out the body and tail lengths and the look for this moment of growing up.</summary>
    private void Dims()
    {
        float bodyGrow = 0, tailF = 1;
        if (Stage >= 1 && Stage < 4)
        {
            bodyGrow = Smooth(Math.Clamp((_meta - 0.3f) / 0.7f, 0, 1));
            tailF = _meta < 0.6f ? 1 : 1 - 0.67f * Math.Clamp((_meta - 0.6f) / 0.4f, 0, 1);
            _frog = Smooth(Math.Clamp((_meta - 0.6f) / 0.4f, 0, 1));
            _legScale = _meta < 0.3f ? 0 : 0.3f + 0.7f * Smooth(Math.Clamp((_meta - 0.3f) / 0.7f, 0, 1));
        }
        else if (Stage == 4)
        {
            bodyGrow = 1; _frog = 1; _legScale = 1;
            tailF = 0.33f - 0.25f * _stub;
            _tailAlpha = 1 - _stub;
        }
        _bl = Lerp(_bl0, _u * 0.022f, bodyGrow);
        _tl = _tl0 * tailF;
    }

    private static float Smooth(float t) => t * t * (3 - 2 * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // ================================================================ one frame

    public void Update(float dt, float homeX, float homeY, Tadpole[] all, float[] threats, int threatCount)
    {
        _aliveFor += dt;
        if (_grow < 1) _grow = MathF.Min(1, _grow + 0.5f * dt / 60f);       // a hatchling: half size to full over a minute
        if (Stage >= 1 && Stage < 4)
        {
            _meta += dt / _metaDur;
            Stage = _meta < 0.3f ? 1 : _meta < 0.6f ? 2 : 3;
            if (_meta >= 1) BecomeFroglet();
        }
        if (Stage == 4) { _stub = MathF.Min(1, _stub + dt / 10f); }
        Dims();
        if (Stage == 4) UpdateFroglet(dt, threats, threatCount);
        else UpdateTadpole(dt, homeX, homeY, all, threats, threatCount);
        LaySpine();
    }

    private void BecomeFroglet()
    {
        Stage = 4; _meta = 1; _stub = 0;
        State = TadState.Rise;
        _age = 0; _kickT = 0; _planEdge = -1; _deep = false; _dive = 0; _stuck = 0;
        _checkT = 0; _checkX = X; _checkY = Y;
        PlanGoal(-1);
    }

    // ---------------------------------------------------------------- the tadpole's day

    private void UpdateTadpole(float dt, float homeX, float homeY, Tadpole[] all, float[] threats, int threatCount)
    {
        float len = Len, scare = _u * 0.09f;
        _stateT -= dt;
        _burstTimer -= dt;
        _bobT += dt;

        // ---- Anything big near: scatter, directly away, at burst speed ----
        int near = -1;
        float bestD2 = scare * scare;
        for (int i = 0; i < threatCount; i++)
        {
            float dx = X - threats[i * 2], dy = Y - threats[i * 2 + 1], d2 = dx * dx + dy * dy;
            if (d2 < bestD2) { bestD2 = d2; near = i; }
        }
        if (near >= 0)
        {
            float away = MathF.Atan2(Y - threats[near * 2 + 1], X - threats[near * 2]);
            // Straight away, or as near as a way that does not run up a steep shoal (the lift would cancel the flight); a cornered one takes any way there is.
            float free = FreeHeading(away, len * 3f, 0.9f);
            if (float.IsNaN(free)) free = FreeHeading(away, len * 3f, 0.5f);
            if (float.IsNaN(free)) free = FreeHeading(away, len * 3f, -1f);
            if (!float.IsNaN(free))
            {
                _fleeDir = free;
                if (State != TadState.Flee) { State = TadState.Flee; _freq = 10; }
                _stateT = MathF.Max(_stateT, 0.45f);
                _burstTimer = MathF.Max(_burstTimer, 1.5f);
            }
        }

        switch (State)
        {
            case TadState.Flee:
                {
                    float err = Turn(_fleeDir, 30f, dt);
                    Speed = MathF.Abs(err) < 1.2f ? len * 10f : 0;
                    if (_stateT <= 0) { State = TadState.Hang; _stateT = 0.5f + 1.5f * R(); Speed = 0; }
                    break;
                }
            case TadState.Windup:
                {
                    // Curl into a C and turn on the spot, then shoot.
                    float err = Turn(_fleeDir, 16f, dt);
                    Speed = 0;
                    _curl = Math.Clamp(err * 0.6f, -0.7f, 0.7f);
                    if (MathF.Abs(err) < 0.12f || _stateT <= 0) { State = TadState.Burst; _stateT = 0.3f; _freq = 10; }
                    break;
                }
            case TadState.Burst:
                {
                    Speed = len * 10f;
                    _curl = 0;
                    if (_stateT <= 0) { State = TadState.Hang; _stateT = 0.4f + 1.6f * R(); Speed = 0; }
                    break;
                }
            case TadState.Graze:
                Graze(dt, len);
                break;
            default:
                Idle(dt, homeX, homeY, all, len);
                break;
        }

        // ---- Move; never onto land, never where the bed is lost in the haze ----
        if (Speed > 0.01f)
        {
            float step = Speed * dt;
            float nx = X + MathF.Cos(Heading) * step, ny = Y + MathF.Sin(Heading) * step;
            if (CanStand(nx, ny)) { X = nx; Y = ny; }
            else
            {
                Speed = 0;
                if (State == TadState.Burst || State == TadState.Flee || State == TadState.Scull) { State = TadState.Hang; _stateT = 0.5f + R(); }
            }
        }
        PointF last = _trail[_trailHead];
        float stepLen = len / 10;
        if ((last.X - X) * (last.X - X) + (last.Y - Y) * (last.Y - Y) > stepLen * stepLen) Remember(new PointF(X, Y));

        // ---- Depth: a little above the bed, never below it ----
        float zc = 0.9f * FloorFrac(X, Y);
        Z = HoldZ(Z, zc, dt);
        if (Z > zc) Z = zc;

        // ---- The tail ----
        float wantAmp, wantFreq;
        switch (State)
        {
            case TadState.Burst: case TadState.Flee: wantAmp = 0.30f; wantFreq = 10; break;
            case TadState.Windup: wantAmp = 0.05f; wantFreq = 4; break;
            case TadState.Scull: wantAmp = 0.11f; wantFreq = 2.6f; break;
            case TadState.Graze: wantAmp = 0.07f + 0.1f * Math.Clamp(MathF.Sin(_bobT * MathF.Tau / 0.9f), 0, 1); wantFreq = 3f; break;
            default: wantAmp = 0.05f; wantFreq = 1.1f; break;
        }
        float k = 1 - MathF.Exp(-dt * (wantAmp > _amp ? 20f : 5f));
        _amp += (wantAmp * len - _amp) * k;
        _freq += (wantFreq - _freq) * (1 - MathF.Exp(-dt * 10f));
        if (State != TadState.Windup) _curl *= MathF.Exp(-dt * 12f);
        _phase = (_phase + MathF.Tau * _freq * dt) % MathF.Tau;
    }

    /// <summary>
    /// Turns the heading toward "want", at most maxRate radians a second, and returns the angle
    /// still to go. A moving tadpole turns by bending along its path; a nearly still one swings its
    /// whole body round, rigidly, about the middle of its body (a share of the turn that goes
    /// down as it speeds up). Without the swing, a tadpole told to face another way would not
    /// change shape at all, because the spine is laid along where the head HAS been.
    /// </summary>
    private float Turn(float want, float maxRate, float dt)
    {
        float err = Wrap(want - Heading);
        ApplyTurn(Math.Clamp(err, -maxRate * dt, maxRate * dt));
        return Wrap(want - Heading);
    }

    /// <summary>Turns the heading by d radians (see Turn: a rigid swing for the share of it that motion does not carry).</summary>
    private void ApplyTurn(float d)
    {
        float share = 1 - Math.Clamp(Speed / (Len * 1.5f), 0, 1);
        float pv = d * share;
        if (MathF.Abs(pv) > 1e-5f)
        {
            float back = _bl * _grow * 0.4f;
            float cx = X - MathF.Cos(Heading) * back, cy = Y - MathF.Sin(Heading) * back;
            float ox = X, oy = Y;
            Pivot(cx, cy, pv);
            if (!(Stage == 4 ? CanSwim(X, Y) : CanStand(X, Y))) { Pivot(cx, cy, -pv); X = ox; Y = oy; }   // not into the bank
        }
        Heading = Wrap(Heading + d);
    }

    /// <summary>Swings the nose and the whole remembered trail about a point.</summary>
    private void Pivot(float cx, float cy, float a)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        (X, Y) = Rot(X, Y, cx, cy, c, s);
        for (int i = 0; i < _trailCount; i++)
        {
            int slot = (_trailHead - i + TrailLen) % TrailLen;
            (float px, float py) = Rot(_trail[slot].X, _trail[slot].Y, cx, cy, c, s);
            _trail[slot] = new PointF(px, py);
        }
    }

    private static (float, float) Rot(float x, float y, float cx, float cy, float c, float s)
        => (cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c);

    /// <summary>
    /// Hanging, sculling and the choosing of what to do next. A tadpole hangs a while; then it
    /// either drifts to a new spot near the cluster's middle, goes to graze at something, or (on
    /// its own timer) bursts. A crowded one drifts away from its neighbour.
    /// </summary>
    private void Idle(float dt, float homeX, float homeY, Tadpole[] all, float len)
    {
        // Its neighbours: keep room (1.2 body lengths), but no alignment: tadpoles do not face one way.
        float px = 0, py = 0;
        float close = len * 1.2f;
        foreach (Tadpole o in all)
        {
            if (o == this || !o.Alive || o.Stage == 4) continue;
            float dx = X - o.X, dy = Y - o.Y, d2 = dx * dx + dy * dy;
            if (d2 < close * close && d2 > 0.01f) { float d = MathF.Sqrt(d2), w = 1 - d / close; px += dx / d * w; py += dy / d * w; }
        }
        float hdx = homeX - X, hdy = homeY - Y, hd = MathF.Sqrt(hdx * hdx + hdy * hdy);
        bool crowded = px * px + py * py > 0.04f;
        bool far = hd > _roam * 1.3f;

        // A burst, when its timer says so and it is not busy.
        if (_burstTimer <= 0 && State != TadState.Graze)
        {
            _burstTimer = _burstEvery * (0.6f + 0.8f * R());
            float want = (hd > _roam && R() < 0.7f) ? MathF.Atan2(hdy, hdx) + (R() - 0.5f) * 1.2f : R() * MathF.Tau;
            float free = FreeHeading(want, len * 4.5f, 0.8f);   // a burst is picked to be SEEN going somewhere: the lift of a shoaling bed may cancel at most a fifth of it (at 0.9 bursts fell to 0.5 a minute by steep banks)
            if (!float.IsNaN(free))
            {
                _fleeDir = free;
                State = TadState.Windup; _stateT = 0.4f;
                return;
            }
        }

        if (State == TadState.Scull)
        {
            // Toward its spot at a lazy 0.3 body lengths a second, gently faster when far from the middle.
            float dx = _tx - X, dy = _ty - Y, d = MathF.Sqrt(dx * dx + dy * dy);
            float sx = dx / MathF.Max(d, 1e-3f), sy = dy / MathF.Max(d, 1e-3f);
            sx += px * 1.5f; sy += py * 1.5f;
            float want = MathF.Atan2(sy, sx);
            float free = FreeHeading(want, len * 1.2f);
            if (float.IsNaN(free)) { State = TadState.Hang; _stateT = 1 + R(); Speed = 0; _noScull = 1.5f; return; }
            Turn(free, 3f, dt);
            float pace = 0.3f + 0.9f * Math.Clamp((hd - _roam) / (_u * 0.05f), 0, 1);   // lazy near its place, quicker the further it has been scattered
            float target = len * pace;
            Speed += (target - Speed) * (1 - MathF.Exp(-dt * 4f));
            if (d < len * 0.4f || _stateT <= 0) { State = TadState.Hang; _stateT = 1.5f + 3 * R(); }
            return;
        }

        // Hanging: the speed bleeds off at once; the body bobs a little on its tail.
        Speed += (0 - Speed) * (1 - MathF.Exp(-dt * 10f));
        if (Speed < 0.02f * len) Speed = 0;
        _noScull -= dt;
        if ((crowded || far) && _noScull <= 0)
        {
            PickSpot(homeX, homeY, px, py, far);
            State = TadState.Scull; _stateT = 6;
            return;
        }
        if (_stateT <= 0)
        {
            if (_grazer && R() < 0.6f && FindGraze())
            {
                State = TadState.Graze; _stateT = 5 + 6 * R(); _bobT = 0;
                return;
            }
            PickSpot(homeX, homeY, 0, 0, false);
            State = TadState.Scull; _stateT = 6;
        }
    }

    /// <summary>A new place to drift to: near the middle of the cluster (within how far this one likes to be), standable.</summary>
    private void PickSpot(float homeX, float homeY, float px, float py, bool far)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            float a = R() * MathF.Tau, d = _roam * MathF.Sqrt(R());
            float tx = homeX + MathF.Cos(a) * d, ty = homeY + MathF.Sin(a) * d;
            if (!CanStand(tx, ty)) continue;
            _tx = tx; _ty = ty;
            return;
        }
        _tx = X + px * _u * 0.02f; _ty = Y + py * _u * 0.02f;
    }

    /// <summary>
    /// Finds something to graze at within 0.1u: a rock's flank, a lily pad's rim, or the bank's
    /// edge. Sets the spot to go to (just outside it, standable) and the way to face it.
    /// </summary>
    private bool FindGraze()
    {
        PointF here = Minnow.ToScreen(_pond, X, Y);
        float reach = _u * 0.1f, best = reach;
        bool found = false;
        foreach (BankRock rock in _pond.Rocks)
            TryGraze(here, rock.Centre, rock.Radius, ref best, ref found);
        foreach (LilyPad pad in _pond.Pads)
            TryGraze(here, pad.Center, pad.Radius, ref best, ref found);
        if (!found)
        {
            // The bank's edge: step against the way of more room until the water stops.
            PointF ow = _pond.OpenWay(here.X, here.Y);
            float l = MathF.Sqrt(ow.X * ow.X + ow.Y * ow.Y);
            if (l > 1e-4f)
            {
                float dirx = -ow.X / l, diry = -ow.Y / l;
                (float gdx, float gdy) = (dirx, diry / Ripples.Squash);
                float gl = MathF.Sqrt(gdx * gdx + gdy * gdy);
                gdx /= gl; gdy /= gl;
                float lx = X, ly = Y;
                for (float d = _u * 0.004f; d <= reach; d += _u * 0.004f)
                {
                    float tx = X + gdx * d, ty = Y + gdy * d;
                    if (!CanStand(tx, ty)) break;
                    lx = tx; ly = ty;
                }
                if ((lx - X) * (lx - X) + (ly - Y) * (ly - Y) > 1)
                {
                    _tx = lx; _ty = ly; _gHeading = MathF.Atan2(gdy, gdx);
                    found = true;
                }
            }
        }
        return found;
    }

    private void TryGraze(PointF here, PointF centre, float radius, ref float best, ref bool found)
    {
        float dx = here.X - centre.X, dy = here.Y - centre.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float rim = d - radius;
        if (rim >= best || d < 1) return;
        // A spot just outside the rim on the tadpole's side, in ground pixels.
        float want = radius + Len * 0.35f;
        PointF spot = new(centre.X + dx / d * want, centre.Y + dy / d * want);
        (float gx, float gy) = Minnow.ToGround(_pond, spot.X, spot.Y);
        if (!CanStand(gx, gy)) return;
        (float cgx, float cgy) = Minnow.ToGround(_pond, centre.X, centre.Y);
        best = rim; found = true;
        _tx = gx; _ty = gy; _gHeading = MathF.Atan2(cgy - gy, cgx - gx);
    }

    /// <summary>Goes to the spot, faces the thing, and bobs its head against it.</summary>
    private void Graze(float dt, float len)
    {
        float dx = _tx - X, dy = _ty - Y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > len * 0.35f)
        {
            float free = FreeHeading(MathF.Atan2(dy, dx), len * 1.0f);
            if (float.IsNaN(free)) { State = TadState.Hang; _stateT = 1; Speed = 0; return; }
            Turn(free, 3f, dt);
            Speed += (len * 0.45f - Speed) * (1 - MathF.Exp(-dt * 4f));
            if (_stateT <= 0) { State = TadState.Hang; _stateT = 1; }
            return;
        }
        // There: face it and nod. The nod is a short push every 0.9 s, which the wall refuses, and a tail flick.
        Turn(_gHeading, 4f, dt);
        float ph = (_bobT % 0.9f);
        Speed = ph < 0.12f ? len * 0.9f : 0;
        if (_stateT <= 0) { State = TadState.Hang; _stateT = 1 + 2 * R(); Speed = 0; }
    }

    /// <summary>
    /// A change of depth held to what the animal's forward motion can hide (Light.HoldDepth's
    /// rule), but never slower than a quarter of the forward motion, so a froglet told to
    /// come up while heading down the screen still arrives.
    /// </summary>
    private float HoldZ(float z, float zt, float dt)
    {
        float per = 1 / (Pond.DepthU * _u * Light.Sink);
        float sinH = MathF.Sin(Heading);
        if (zt < z) return MathF.Max(zt, z - dt * MathF.Max(0.25f, 0.5f * (1 - sinH)) * Speed * per);
        if (zt > z) return MathF.Min(zt, z + dt * MathF.Max(0.25f, 0.5f * (1 + sinH)) * Speed * per);
        return z;
    }

    // ---------------------------------------------------------------- the froglet

    private static readonly int[] EdgeSteps = [1, 2, 3, 4, 5, 6, 7];

    /// <summary>
    /// Picks where to swim to: a spot beyond one of the screen's four edges. Each candidate is
    /// scored by how much of the straight line to it is NOT free water (land, pads, rocks) plus a
    /// little for distance, so the land corner is never the goal and a froglet in the shallows
    /// heads for the open side. "skip" is an edge to avoid (the one that just failed).
    /// </summary>
    private void PlanGoal(int skip)
    {
        PointF s = Minnow.ToScreen(_pond, X, Y);
        float w = _pond.Width, h = _pond.Height, out_ = _u * 0.06f;
        float bestScore = float.MaxValue;
        float bx = s.X, by = s.Y;
        int bestEdge = -1;
        for (int e = 0; e < 4; e++)
        {
            if (e == skip) continue;
            foreach (int k in EdgeSteps)
            {
                float f = k / 8f;
                float tx, ty;
                switch (e)
                {
                    case 0: tx = -out_; ty = h * f; break;
                    case 1: tx = w + out_; ty = h * f; break;
                    case 2: tx = w * f; ty = -out_; break;
                    default: tx = w * f; ty = h + out_; break;
                }
                int bad = 0;
                for (int i = 1; i <= 40; i++)
                {
                    float px = s.X + (tx - s.X) * i / 40f, py = s.Y + (ty - s.Y) * i / 40f;
                    int ix = (int)px, iy = (int)py;
                    if (px < 0 || py < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
                    int idx = iy * _pond.Width + ix;
                    if (!_pond.Water[idx] || _pond.Land[idx]) bad++;
                }
                float dist = MathF.Sqrt((tx - s.X) * (tx - s.X) + (ty - s.Y) * (ty - s.Y));
                float score = bad * 3f + dist / _u;
                if (score < bestScore) { bestScore = score; bx = tx; by = ty; bestEdge = e; }
            }
        }
        _planEdge = bestEdge;
        (_gx, _gy) = Minnow.ToGround(_pond, bx, by);
    }

    private void UpdateFroglet(float dt, float[] threats, int threatCount)
    {
        _age += dt;
        float L = _u * 0.022f;
        _stateT -= dt;

        // ---- A threat near: dive (Z 0.15 for 3 s, then up again) ----
        float scare = _u * 0.06f;
        if (_dive <= 0)
        {
            for (int i = 0; i < threatCount; i++)
            {
                float dx = X - threats[i * 2], dy = Y - threats[i * 2 + 1];
                if (dx * dx + dy * dy < scare * scare) { _dive = 3; State = TadState.Dive; break; }
            }
        }
        else { _dive -= dt; if (_dive <= 0) State = Z > 0.03f ? TadState.Rise : TadState.Swim; }

        // ---- Hemmed in: after 60 s dive and swim deeper for the edge (pads are no wall under the water); after 90 s, gone under the murk ----
        if (_age > 60 && !_deep) { _deep = true; PlanGoal(-1); }
        if (_age > 90) { Leaving = Exit.Murk; return; }

        // ---- Heading: toward the goal, round whatever is ahead ----
        float want = MathF.Atan2(_gy - Y, _gx - X);
        float reachA = _u * 0.035f, reachB = _u * 0.07f;
        float chosen = float.NaN;
        for (int k = 0; k < 5 && float.IsNaN(chosen); k++)
        {
            float a = k * 0.45f;
            for (int sgn = 0; sgn < 2; sgn++)
            {
                float off = k == 0 ? 0 : a * (sgn == 0 ? _side : -_side);
                if (FroClear(want + off, reachA, reachB)) { chosen = want + off; if (k > 0) _side = off > 0 ? 1 : -1; break; }
                if (k == 0) break;
            }
        }
        if (float.IsNaN(chosen))
        {
            PointF s0 = Minnow.ToScreen(_pond, X, Y);
            PointF ow = _pond.OpenWay(s0.X, s0.Y);
            chosen = MathF.Abs(ow.X) + MathF.Abs(ow.Y) > 1e-3f ? MathF.Atan2(ow.Y / Ripples.Squash, ow.X) : want + 1.5f;
        }
        float rate = Math.Clamp(Wrap(chosen - Heading) * 3f, -2.6f, 2.6f);
        _turnRate += (rate - _turnRate) * (1 - MathF.Exp(-dt * 8f));
        ApplyTurn(_turnRate * dt);      // a turn at a crawl swings the whole body (a gliding froglet would otherwise bend into a hairpin); at speed the trail does it

        // ---- The kick: a push 0.25 s at 0.07u/s, a glide 0.7 s down to 0.015u/s ----
        const float push = 0.25f, glide = 0.7f;
        _kickT += dt;
        if (_kickT >= push + glide)
        {
            _kickT -= push + glide;
            PointF n = Minnow.ToScreen(_pond, X, Y);
            if (Z < 0.05f) _ripples.Add(n.X, n.Y, 0.12f, thin: true);
        }
        if (_kickT < push) { Speed = _u * 0.07f; _pose = Smooth(_kickT / push); }
        else
        {
            float g = _kickT - push;
            Speed = _u * (0.015f + 0.055f * MathF.Exp(-4.5f * g / glide));
            _pose = g < glide * 0.6f ? 1 : 1 - Smooth((g - glide * 0.6f) / (glide * 0.4f));
        }

        // ---- Move: a step into land or a pad is refused (and the heading turns away next frame) ----
        float step = Speed * dt;
        float nx = X + MathF.Cos(Heading) * step, ny = Y + MathF.Sin(Heading) * step;
        if (CanSwim(nx, ny)) { X = nx; Y = ny; }
        else { Speed = 0; _stuck += dt; _side = -_side; _refusedT = 1; _refusedH = Heading; }
        _refusedT -= dt;
        PointF last = _trail[_trailHead];
        float stepLen = Len / 10;
        if ((last.X - X) * (last.X - X) + (last.Y - Y) * (last.Y - Y) > stepLen * stepLen) Remember(new PointF(X, Y));

        // ---- Not getting anywhere (no progress in 6 s): another way ----
        _checkT += dt;
        if (_checkT > 6)
        {
            float mx = X - _checkX, my = Y - _checkY;
            if (mx * mx + my * my < (_u * 0.03f) * (_u * 0.03f)) PlanGoal(_planEdge);
            _checkT = 0; _checkX = X; _checkY = Y;
        }

        // ---- Depth: up to the surface in 2 s (or 0.15 when diving, never below the bed) ----
        float floorZ = 0.9f * FloorFrac(X, Y);
        float zt = _dive > 0 ? MathF.Min(0.15f, floorZ) : _deep ? MathF.Min(0.3f, floorZ) : 0;
        Z = HoldZ(Z, zt, dt);
        if (Z > floorZ + 1e-4f && Z > 0) Z = MathF.Max(0, MathF.Min(Z, floorZ));
        if (State == TadState.Rise && Z <= 0.03f) State = TadState.Swim;
        else if (State == TadState.Swim && Z > 0.03f && _dive <= 0) State = TadState.Rise;

        // ---- The tail stub just trembles ----
        _amp += (0.03f * Len - _amp) * (1 - MathF.Exp(-dt * 4));
        _phase = (_phase + MathF.Tau * 3f * dt) % MathF.Tau;
        _curl = 0;

        // ---- Gone when its middle has left the screen ----
        PointF mid = Minnow.ToScreen(_pond, X - MathF.Cos(Heading) * _bl * 0.5f, Y - MathF.Sin(Heading) * _bl * 0.5f);
        if (mid.X < 0 || mid.Y < 0 || mid.X >= _pond.Width || mid.Y >= _pond.Height) { Leaving = Exit.Edge; if (_dive > 0) _dived = true; }
    }

    /// <summary>
    /// Is the way ahead free? Every couple of pixels out to "a" (a thin wall, a pad's stem, a stalk,
    /// must not slip between two far samples: the first version tested only two far points and a
    /// froglet sat nose to a sliver of wall for 86 s believing the way clear), then the far point "b".
    /// A heading that just had a step refused counts as blocked for a second.
    /// </summary>
    private bool FroClear(float h, float a, float b)
    {
        if (_refusedT > 0 && MathF.Abs(Wrap(h - _refusedH)) < 0.35f) return false;
        float c = MathF.Cos(h), s = MathF.Sin(h);
        for (float d = 2; d <= a; d += 2) if (!CanSwim(X + c * d, Y + s * d)) return false;
        return CanSwim(X + c * b, Y + s * b);
    }
    private float _refusedT, _refusedH;

    // ================================================================ the spine

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % TrailLen;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(TrailLen, _trailCount + 1);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Lays the spine along the trail behind the nose (the minnow's method), adds the tail's wave
    /// (the body is a stiff lump; only the tail swings, more towards its tip), squashes it to the
    /// screen, shrinks it and sinks it by its depth, and works out the shadow on the bed.
    /// </summary>
    private void LaySpine()
    {
        float sc = _grow * Light.Scale(Z);
        _scale = sc;
        // Distances along the animal, nose first, for the seven points.
        float bl = _bl, tl = _tl;
        _pos[0] = 0; _pos[1] = bl * 0.5f; _pos[2] = bl;
        for (int i = 3; i < Points; i++) _pos[i] = bl + tl * (i - 2) / 4f;
        float total = _pos[Points - 1];
        for (int i = 0; i < Points; i++) Along[i] = _pos[i] / total;
        _bodyEnd = Along[2] + _frog * 0.6f * (Along[3] - Along[2]);

        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(X, Y), b = _trail[slot];
        float travelled = 0, segLen = Dist(a, b);
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            float want = _pos[i] * sc;
            while (travelled + segLen < want && left > 0)
            {
                travelled += segLen;
                a = b;
                slot = (slot - 1 + TrailLen) % TrailLen;
                left--;
                b = _trail[slot];
                segLen = Dist(a, b);
            }
            if (left <= 0 && travelled + segLen < want)
            {
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(Heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(Heading);
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
        // How wide each point is (half width, ground pixels). Tadpole: an egg with the widest part
        // forward and a narrow rear; frog: a wedge, widest at the hips. Then the tail, thin.
        float hw = bl * (0.4f - 0.06f * _frog) * sc;
        float tailW = _bl0 * 0.4f * sc;
        Span<float> wid = stackalloc float[Points];
        wid[0] = hw * Lerp(0.5f, 0.5f, _frog);
        wid[1] = hw * Lerp(1.0f, 0.85f, _frog);
        wid[2] = hw * Lerp(0.3f, 0.75f, _frog);
        wid[3] = tailW * Lerp(0.42f, 0.35f, _frog);
        wid[4] = tailW * 0.34f; wid[5] = tailW * 0.25f; wid[6] = tailW * 0.14f;
        float u = _u, depthPx = Pond.DepthU * u;
        for (int i = 0; i < Points; i++)
        {
            float tt = i < 3 ? 0 : (_pos[i] - bl) / MathF.Max(1e-3f, tl);
            float sway = tt <= 0 ? 0 : (_amp * tt * MathF.Sqrt(tt) * MathF.Sin(MathF.Tau * 0.9f * tt - _phase) + _curl * Len * tt * tt * 0.6f) * sc;
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            PointF l = Minnow.ToScreen(_pond, cx + nx * wid[i], cy + ny * wid[i]), r = Minnow.ToScreen(_pond, cx - nx * wid[i], cy - ny * wid[i]);
            float sink = Light.SinkPx(Z, u);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2 + sink);
            // Never thinner than a pixel and a bit down the tail: a tail narrower than that fell between the
            // pixel centres and drew as a row of dots (the first close-up strip), not a line.
            HalfWidth[i] = MathF.Max(i >= 3 ? 1.0f : 0.5f, Dist(l, r) / 2);
            Shape.Points[i] = new System.Numerics.Vector3(cx, cy, Z * depthPx);
            Shape.Radius[i] = wid[i];
            _murkAt[i] = Light.Murk(Z);
        }
        Shape.Seal();
        _eyeR = MathF.Max(0.55f, bl * sc * Lerp(0.045f, 0.09f, _frog));

        // ---- The shadow on the bed: hugging the animal, faint and soft with the gap (the koi's rule) ----
        PointF nose = Minnow.ToScreen(_pond, X, Y);
        int under = Math.Clamp((int)nose.Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)nose.X, 0, _pond.Width - 1);
        float floor = _pond.Floor[under] / 255f;
        float painted = MathF.Min(_pond.Depth[under], _pond.Floor[under]) / 255f;
        float clear = 0.85f * MathF.Pow(1 - painted, 1.4f);
        float lit = Math.Clamp((_pond.Sun[under] - 154) / 101f, 0, 1);
        float gap = MathF.Max(0, floor - Z) * depthPx;
        float hug = MathF.Min(1f, 3f * clear);
        float faded = MathF.Exp(-gap / (0.06f * u));
        _shadowAlpha = clear > 0.06f && Z > 0.03f ? MathF.Min(0.6f, 0.9f * clear) * lit * (0.25f + 0.75f * faded) : 0;
        float off = MathF.Min(gap * hug, 0.012f * u), norm = MathF.Sqrt(Light.ShadowX * Light.ShadowX + (Light.ShadowY + Light.Sink) * (Light.ShadowY + Light.Sink));
        _shadowDx = Light.ShadowX / norm * off;
        _shadowDy = (Light.ShadowY + Light.Sink) / norm * off;
        _shadowSoft = MathF.Max(1f, MathF.Min(gap * 0.2f, 0.012f * u));
    }

    /// <summary>True when any drawn point is on a pixel it may be seen on (the build's instrument asks).</summary>
    public bool Visible()
    {
        if (!Alive || _murkAt[0] > 0.985f) return false;
        bool[] stencil = Z > 0.03f ? _where : _pond.Open;
        for (int i = 0; i < Points; i++)
        {
            int x = (int)Spine[i].X, y = (int)Spine[i].Y;
            if (x >= 0 && y >= 0 && x < _pond.Width && y < _pond.Height && stencil[y * _pond.Width + x]) return true;
        }
        return false;
    }

    // ================================================================ the picture

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        if (s > _bodyEnd)
        {
            // The tail: a dark muscle down the middle and a see-through fin either side of it (from above,
            // a faint wider halo); both fade toward the tip, and away altogether as the stub is absorbed.
            float tt = Math.Clamp((s - _bodyEnd) / MathF.Max(1e-3f, 1 - _bodyEnd), 0, 1);
            if (av < 0.4f) { r = 30; g = 28; b = 17; alpha = 0.95f * _tailAlpha * (1 - 0.3f * tt); }
            else { r = 84; g = 80; b = 50; alpha = 0.38f * _tailAlpha * (1 - 0.4f * tt); }
            return alpha > 0.01f;
        }
        float sb = _bodyEnd > 1e-3f ? s / _bodyEnd : 0;
        float dome = 0.72f + 0.28f * MathF.Sqrt(MathF.Max(0, 1 - v * v));
        // Tadpole: nearly black olive with a faint gold speckle (a fixed scatter on the body, so it rides along).
        float tr = 34, tg = 31, tb = 19;
        int hsh = ((int)(sb * 17) * 73856093) ^ ((int)((v + 1) * 5.5f) * 19349663) ^ _seed;
        if ((hsh & 15) == 0) { tr = 92; tg = 76; tb = 34; }          // a faint gold speck (the first look was far too bold)
        else if ((hsh & 15) == 1) { tr = 52; tg = 45; tb = 24; }
        float cr = tr, cg = tg, cb = tb;
        if (_frog > 0)
        {
            // Froglet: olive, paler along the flanks, a pale line down the back, a few dark blotches.
            float fr = 100, fg = 126, fb = 56;
            float flank = Math.Clamp((av - 0.62f) / 0.3f, 0, 1);
            fr += (158 - fr) * flank * 0.7f; fg += (178 - fg) * flank * 0.7f; fb += (94 - fb) * flank * 0.7f;
            float n = MathF.Sin(sb * 14 + _seed % 7) * MathF.Sin(v * 6 + _seed % 5) + 0.4f;
            if (n > 0.35f && av < 0.85f) { float bl = Math.Clamp((n - 0.35f) * 3, 0, 0.8f); fr += (62 - fr) * bl; fg += (48 - fg) * bl; fb += (26 - fb) * bl; }
            if (sb > 0.12f && av < 0.09f) { fr += (198 - fr) * 0.85f; fg += (210 - fg) * 0.85f; fb += (114 - fb) * 0.85f; }
            cr = tr + (fr - tr) * _frog; cg = tg + (fg - tg) * _frog; cb = tb + (fb - tb) * _frog;
        }
        r = (int)(cr * dome); g = (int)(cg * dome); b = (int)(cb * dome);
        alpha = 0.97f;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        float murk = Light.Murk(Z);
        if (murk > 0.985f) return;
        bool under = Z > 0.03f;
        bool[] stencil = under ? _where : _pond.Open;
        float soft = MathF.Max(0.9f, _u * 0.0007f);
        if (_shadowAlpha > 0.01f) PaintShadow(fb, _where, _shadowDx, _shadowDy, _shadowAlpha, _shadowSoft);

        // Legs first (the body covers where they join it), then the body, then the eyes.
        if (_legScale > 0)
        {
            float L = Dist(Spine[0], Spine[2]);
            float lm = murk;
            float p = Stage == 4 ? _pose : 0.9f;
            Hind(fb, stencil, 1, L, p, lm);
            Hind(fb, stencil, -1, L, p, lm);
            if (Stage >= 3) Fore(fb, stencil, 1, L, p, lm);
            if (Stage >= 3) Fore(fb, stencil, -1, L, p, lm);
        }
        if (Stage == 1) Buds(fb, stencil, murk);
        Paint(fb, stencil, _pond.Sun, murk, soft, _pond.Pixels);

        // The eyes: two tiny dark dots high on a tadpole; domes on top of a froglet's head with a pale rim.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float along = Lerp(0.55f, 0.35f, _frog), outv = HalfWidth[1] * Lerp(0.5f, 0.62f, _frog);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ex / el * el * along - ey / el * outv * side, e0.Y + ey / el * el * along + ex / el * outv * side);
            if (_frog > 0.5f)
            {
                c.Y -= _eyeR * 0.3f;
                Oval(fb, stencil, c, 0, _eyeR * 1.35f, _eyeR * 1.2f, 200, 208, 142, 0.95f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
                Oval(fb, stencil, c, 0, _eyeR, _eyeR * 0.9f, 28, 30, 18, 0.97f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
            }
            else Oval(fb, stencil, c, 0, _eyeR, _eyeR, 14, 14, 10, 0.95f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
        }
    }

    /// <summary>Stage 1: two dark dots at the base of the tail, where the hind legs will grow.</summary>
    private void Buds(FrameBuffer fb, bool[] stencil, float murk)
    {
        PointF a = Spine[2], b = Spine[3];
        float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        float nx = -dy / l, ny = dx / l, w = HalfWidth[2] * 0.9f + 0.4f, r = MathF.Max(1.0f, Dist(Spine[0], Spine[2]) * 0.14f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, stencil, new PointF(a.X + dx / l * r + nx * w * side, a.Y + dy / l * r + ny * w * side), 0, r, r, 140, 116, 62, 0.95f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
    }

    private void Limb(FrameBuffer fb, bool[] stencil, PointF a, PointF b, float wid, int r, int g, int bl, float murk)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        var c = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        Oval(fb, stencil, c, MathF.Atan2(dy, dx), d / 2 + wid * 0.6f, MathF.Max(0.6f, wid), r, g, bl, 1f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
    }

    /// <summary>
    /// A hind leg, the frog's (Frog.HindLeg) at a smaller size: folded (pose 0) the thigh runs
    /// forward and out and the shin comes back; stretched (pose 1) it is straight out behind. A
    /// growing tadpole's legs lie stretched back along the tail root, short at first.
    /// </summary>
    private void Hind(FrameBuffer fb, bool[] stencil, int side, float body, float p, float murk)
    {
        float L = body * _legScale;
        PointF nose = Spine[0], tail = Spine[2];
        float fx = nose.X - tail.X, fy = nose.Y - tail.Y, fl = MathF.Max(1e-3f, MathF.Sqrt(fx * fx + fy * fy));
        fx /= fl; fy /= fl;
        float ox = -fy * side, oy = fx * side;
        PointF hip = new(Lerp(tail.X, nose.X, Lerp(0, 0.22f, _frog)) + ox * HalfWidth[1] * 0.55f, Lerp(tail.Y, nose.Y, Lerp(0, 0.22f, _frog)) + oy * HalfWidth[1] * 0.55f);
        float tf = Lerp(0.17f, -0.36f, p) * L, to = Lerp(0.24f, 0.07f, p) * L;
        float sf = Lerp(-0.30f, -0.40f, p) * L, so = Lerp(-0.04f, 0.04f, p) * L;
        float ff = Lerp(0.20f, -0.20f, p) * L, fo = Lerp(0.03f, 0.08f, p) * L;
        PointF knee = new(hip.X + fx * tf + ox * to, hip.Y + fy * tf + oy * to);
        PointF ankle = new(knee.X + fx * sf + ox * so, knee.Y + fy * sf + oy * so);
        PointF toe = new(ankle.X + fx * ff + ox * fo, ankle.Y + fy * ff + oy * fo);
        int lr = (int)Lerp(80, 142, _frog), lg = (int)Lerp(72, 154, _frog), lb = (int)Lerp(36, 84, _frog);
        Limb(fb, stencil, ankle, toe, L * (0.05f + 0.045f * p), lr, lg, lb, murk);
        Limb(fb, stencil, knee, ankle, L * 0.07f, (int)(lr * 0.62f), (int)(lg * 0.7f), (int)(lb * 0.55f), murk);
        Limb(fb, stencil, hip, knee, L * 0.1f, (int)(lr * 0.68f), (int)(lg * 0.77f), (int)(lb * 0.6f), murk);
    }

    private void Fore(FrameBuffer fb, bool[] stencil, int side, float body, float p, float murk)
    {
        float L = body * _legScale;
        PointF nose = Spine[0], tail = Spine[2];
        float fx = nose.X - tail.X, fy = nose.Y - tail.Y, fl = MathF.Max(1e-3f, MathF.Sqrt(fx * fx + fy * fy));
        fx /= fl; fy /= fl;
        float ox = -fy * side, oy = fx * side;
        PointF sh = new(Lerp(tail.X, nose.X, 0.66f) + ox * HalfWidth[1] * 0.7f, Lerp(tail.Y, nose.Y, 0.66f) + oy * HalfWidth[1] * 0.7f);
        float f = Lerp(0.17f, -0.02f, p) * L, o = Lerp(0.10f, 0.02f, p) * L;
        Limb(fb, stencil, sh, new PointF(sh.X + fx * f + ox * o, sh.Y + fy * f + oy * o), L * 0.05f, (int)Lerp(70, 112, _frog), (int)Lerp(66, 134, _frog), (int)Lerp(34, 62, _frog), murk);
    }
}
