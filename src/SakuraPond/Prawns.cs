using System.Globalization;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The freshwater prawns: a few small, see-through shrimp that live on the bed
/// of the shallows, walk, feed, swim a little, and flick backward from anything
/// that comes over them. The scene holds this wrapper (the same shape as
/// Crayfish and Lizards); the animals are Prawn below. With the knob at 0 the
/// wrapper holds nothing and every call returns at once.
///
/// WORLD LAW, in the pond's own terms: a prawn is SOLID TO NOTHING. It lives
/// on the bed under everything that swims (a fish is kept a margin of water
/// above the bed by its own rules, so a fish passes OVER it), under the lily
/// pads and the petals. It reacts instead: a fish, a turtle or a duck near it
/// sends it flicking backward. Prawns do not react to each other and may pass
/// over each other (they are tiny and see-through). The things that stand on
/// the bed (rocks, the island, the formation, the cattails, the bank) are
/// walls to it: a step onto a pixel that is not open, shallow water is refused.
/// </summary>
internal sealed class Prawns : ISnackField
{
    /// <summary>
    /// The instrument log (PECKWORKS_PRAWNLOG names a file, set up once here, flushed at exit): one line per
    /// prawn per frame. Null in the real pond, and then it costs nothing (no line is built).
    /// </summary>
    public static Action<string>? MotionLog;

    private static readonly PointF[] None = [];
    private readonly Prawn[] _all;
    private readonly PointF[] _spots;
    private double _t;
    private long _frame;

    /// <param name="count">How many prawns (the "Prawns" knob). 0 = none, and every call returns at once.</param>
    /// <param name="waterInView">Open water that no branch hides: where a prawn may be drawn.</param>
    /// <param name="rng">The prawns' OWN dice (seeded from the scene with a salt of their own), so adding them moved nothing else.</param>
    public Prawns(Pond pond, Ripples ripples, int count, bool[] waterInView, Random rng)
    {
        if (count <= 0) { _all = []; _spots = None; return; }
        var bed = new PrawnBed(pond, waterInView);
        // Two dials per prawn, dealt so that no two are alike (Traits.Deal): BOLD and BUSY.
        float[] bold = Traits.Deal(rng, count), busy = Traits.Deal(rng, count);
        // The shallowest bed first: the fish keep a margin of water over the bed, so they cross the deeper part of the shallows
        // far more often, and a prawn homed there is scared (and walking home) half its life. Only if the seed has too little
        // of it does the whole 0.02u to 0.12u band come into play.
        // And the roomiest first: a home with open bed all round lets a scared prawn flick somewhere (in a nook between stones
        // a prawn froze or flicked on the spot at four scares in five). Each step down relaxes one of the two wishes.
        List<PointF> candidates = bed.HomeCandidates(rng, 0.065f, true);
        if (candidates.Count < 3 * count) candidates = bed.HomeCandidates(rng, 0.12f, true);
        if (candidates.Count < 3 * count) candidates = bed.HomeCandidates(rng, 0.065f, false);
        if (candidates.Count < 3 * count) candidates = bed.HomeCandidates(rng, 0.12f, false);
        var homes = new List<PointF>();
        var animals = new List<Prawn>();
        for (int k = 0; k < count; k++)
        {
            // A home at least 0.08u from every other home. If the seed has no such place for this one, it is simply not there.
            PointF? home = null;
            for (int tries = 0; tries < 60 && candidates.Count > 0 && home == null; tries++)
            {
                PointF c = candidates[rng.Next(candidates.Count)];
                bool apart = true;
                foreach (PointF h in homes)
                    if ((h.X - c.X) * (h.X - c.X) + (h.Y - c.Y) * (h.Y - c.Y) < bed.U * bed.U * 0.08f * 0.08f) { apart = false; break; }
                if (apart) home = c;
            }
            if (home is PointF hp)
            {
                homes.Add(hp);
                animals.Add(new Prawn(bed, k, hp, bold[k], busy[k], rng));
            }
        }
        _all = animals.ToArray();
        _spots = new PointF[_all.Length];
        for (int i = 0; i < _all.Length; i++) _spots[i] = _all[i].Spot;
        // The instrument log, unset in the real pond (the turtles' way, Turtles in Turtle.cs).
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_PRAWNLOG");
        if (!string.IsNullOrEmpty(file) && MotionLog == null)
        {
            var writer = new StreamWriter(file) { AutoFlush = false };
            writer.WriteLine("# F,t,idx,doing,x,y,z,heading,speed,pose,visible,noseX,noseY,tailX,tailY,homeX,homeY,stoppedShort,scares,threatIdx,threatN,inView,floorZ,murk,lift,freshScare,nearPx,reachPx");
            writer.WriteLine($"# prawns {_all.Length} of {count}");
            MotionLog = line => writer.WriteLine(line);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => writer.Flush();
        }
    }

    /// <param name="threats">Screen points of what sends a prawn flicking backward: koi heads and middles, turtle noses and shells, the ducks' heads (the crayfish's list).</param>
    public void Update(float dt, PointF[] threats)
    {
        if (_all.Length == 0) return;
        _t += dt;
        _frame++;
        for (int i = 0; i < _all.Length; i++)
        {
            _all[i].Update(dt, threats);
            _spots[i] = _all[i].Spot;
            if (MotionLog != null) _all[i].Log(_frame, _t, MotionLog);
        }
    }

    /// <summary>On the bed of the shallows: before every fish (everything that swims is above it).</summary>
    public void Draw(FrameBuffer fb)
    {
        for (int i = 0; i < _all.Length; i++) _all[i].Draw(fb);
    }

    /// <summary>Each prawn's middle, screen pixels, for the instruments (and for anything that might one day hunt them). Empty while none is out.</summary>
    public PointF[] Spots => _spots;

    /// <summary>The nearest living, unhunted prawn whose nose is within seePx of the mouth and passes "ok" (the predators' Snatch asks; surfaceOnly means nothing to a prawn, which is always on the bed).</summary>
    public ISnack? Spot(PointF mouth, float seePx, bool surfaceOnly = false, Func<ISnack, bool>? ok = null)
    {
        ISnack? best = null;
        float bestD2 = seePx * seePx;
        for (int i = 0; i < _all.Length; i++)
        {
            Prawn p = _all[i];
            if (!p.Alive || p.Hunter != null) continue;
            PointF n = p.Nose;
            float dx = n.X - mouth.X, dy = n.Y - mouth.Y, d2 = dx * dx + dy * dy;
            if (d2 >= bestD2 || (ok != null && !ok(p))) continue;
            bestD2 = d2; best = p;
        }
        return best;
    }

    /// <summary>A predator's mouth has closed on it: gone this frame, back at its home a few minutes later (Prawn.Eaten).</summary>
    public void Eat(ISnack s) => ((Prawn)s).Eaten();
}

/// <summary>
/// What every prawn asks about the bed: is this ground point open, shallow,
/// visible water (so a prawn may stand on it), where is the floor under it, and
/// where are good homes. Ground points are the scene seen straight down (before
/// the view's squash); screen points are what is drawn. Shared so the homes can
/// be chosen before any prawn exists.
/// </summary>
internal sealed class PrawnBed
{
    public const float MurkLimit = 0.65f;      // a step to where the haze would pass this is refused (the bed is lost there)
    // And the shallows only: since the water was made clearer (Light.SeeU 0.70) the haze at the pond's FULL depth is 0.56, so
    // the murk limit above no longer keeps anything out of the deep water. A prawn is a thing of the shallows (its home is
    // 0.02u to 0.12u down), so a step to a floor deeper than this is refused too.
    public const float DeepestU = 0.13f;
    public readonly Pond Pond;
    public readonly bool[] View;               // open water in view: where a prawn may be drawn AND walk
    public readonly float U;

    public PrawnBed(Pond pond, bool[] waterInView)
    {
        Pond = pond;
        U = pond.U;
        View = new bool[pond.Width * pond.Height];
        for (int i = 0; i < View.Length; i++) View[i] = waterInView[i] && pond.Water[i] && pond.Open[i];
    }

    public float CentreY => Pond.Centre.Y;
    public PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    public (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);

    /// <summary>The floor under a ground point as a 0 to 1 depth (Pond.Floor), the Z everything that swims uses.</summary>
    public float ZAt(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = Math.Clamp((int)s.X, 0, Pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, Pond.Height - 1);
        return Pond.Floor[iy * Pond.Width + ix] / 255f;
    }

    /// <summary>
    /// May a body part be at this ground point? Open water in view, with the bed
    /// in sight (the haze there under the limit), and the sunk picture of it
    /// also on open water. A pad, the land, a rock, the island's cap, the
    /// cattails' foot and the water that is too deep are all "no".
    /// </summary>
    public bool Free(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = (int)s.X, iy = (int)s.Y;
        if (ix < 0 || iy < 0 || ix >= Pond.Width || iy >= Pond.Height) return false;
        int i = iy * Pond.Width + ix;
        if (!View[i]) return false;
        float f = Pond.Floor[i] / 255f;
        if (Light.Murk(f) > MurkLimit || f * Pond.DepthU > DeepestU) return false;
        int jy = (int)(s.Y + Light.SinkPx(f, U));
        return jy >= 0 && jy < Pond.Height && View[jy * Pond.Width + ix];
    }

    /// <summary>How many of 12 straight lines out from here stay on free bed for lengthU (the shallows are a narrow band by the stones): a nook ringed by stones has few, and a prawn that cannot flick backward anywhere only ever freezes.</summary>
    private int OpenSpokes(float gx, float gy, float lengthU)
    {
        int open = 0;
        for (int k = 0; k < 12; k++)
        {
            float c = MathF.Cos(k * MathF.Tau / 12), s = MathF.Sin(k * MathF.Tau / 12);
            bool ok = true;
            for (float d = 3; d <= U * lengthU && ok; d += 3) ok = Free(gx + c * d, gy + s * d);
            if (ok) open++;
        }
        return open;
    }

    /// <summary>
    /// Places a prawn could live, found once when the scene is made: a visible
    /// patch of shallow bed (the floor 0.02u to 0.12u down, so the haze there
    /// is under 0.6) within 0.15u of something that stands in the water (a bank
    /// rock in the water, a cap of the formation, the
    /// island's stone), with 0.015u of room all round it and free to step on.
    /// Returned as ground points; the wrapper deals them out to the prawns.
    /// </summary>
    public List<PointF> HomeCandidates(Random rng, float maxDepthU, bool roomy)
    {
        // Things that stand in the water: a centre (screen) and how far the stone reaches (pixels).
        var things = new List<(PointF c, float r)>();
        foreach (BankRock r in Pond.Rocks) if (r.InWater) things.Add((r.Centre, r.Radius));
        // (Not the cattails' foot: the leaves are a live layer drawn over the water after the prawns, so a prawn homed in the
        // clump was behind the leaves for most of every frame, in every run.)
        if (Pond.Formation is Formation fm)
            for (int i = 0; i < fm.Caps.Length; i++) things.Add((fm.Caps[i], fm.CapRadii[i]));
        if (Pond.Island is Island isl) things.Add((isl.Centre, isl.Radius));
        var found = new List<PointF>();
        for (int tries = 0; tries < 4000 && found.Count < 160; tries++)
        {
            var (c, r) = things[rng.Next(things.Count)];
            float a = (float)(rng.NextDouble() * MathF.Tau), d = r + U * (0.01f + 0.14f * (float)rng.NextDouble());
            float sx = c.X + MathF.Cos(a) * d, sy = c.Y + MathF.Sin(a) * d;
            if (sx < 0 || sy < 0 || sx >= Pond.Width || sy >= Pond.Height) continue;
            // Not under the cattails' arching leaves (a live layer over the water): a prawn homed there is hidden.
            float cdx = sx - Pond.CattailSpot.X, cdy = sy - Pond.CattailSpot.Y;
            if (cdx * cdx + cdy * cdy < U * U * 0.16f * 0.16f) continue;
            int ix = (int)sx, iy = (int)sy;
            float fl = Pond.Floor[iy * Pond.Width + ix] / 255f;
            float depthU = fl * Pond.DepthU;
            if (depthU < 0.02f || depthU > maxDepthU || Light.Murk(fl) >= 0.6f) continue;
            if (Pond.RoomAt(sx, sy) < U * 0.015f) continue;
            (float gx, float gy) = ToGround(sx, sy);
            if (!Free(gx, gy)) continue;
            bool ringOk = true;
            for (int k = 0; k < 6 && ringOk; k++)
                ringOk = Free(gx + MathF.Cos(k * MathF.Tau / 6) * U * 0.015f, gy + MathF.Sin(k * MathF.Tau / 6) * U * 0.015f);
            if (ringOk && (roomy ? OpenSpokes(gx, gy, 0.04f) >= 8 && OpenSpokes(gx, gy, 0.08f) >= 3 : OpenSpokes(gx, gy, 0.05f) >= 6)) found.Add(new PointF(gx, gy));
        }
        return found;
    }
}

/// <summary>
/// A freshwater prawn: the sujiebi (Palaemon paucidens, the "striped shrimp")
/// of a Japanese pond, about 0.04u from the tip of its rostrum to its tail fan,
/// nearly transparent with a faint grey-green cast, so the bed shows through
/// it. A dark thread of a gut runs down its back, a few thin dark oblique
/// stripes mark the sides of its abdomen (that is the "suji"), two dark eyes
/// stand on short stalks either side of a pointed rostrum, two long thin
/// antennae sweep back past the tail (one and a half body lengths) and are
/// never quite still.
///
/// WHAT A PRAWN DOES, in the order it does it (watch one in a ditch):
///   * FEEDS most of the time: stands still and picks at the silt with its front
///     legs, tiny repeated motions, for ten to forty seconds.
///   * WALKS slowly on five pairs of thin legs (a wave down each side), about
///     0.012u a second, three to twelve seconds at a time, and stops very often.
///   * SWIMS, rarely and briefly: lifts a little off the bed and goes forward
///     slowly on its swimmerets (0.025u a second), the legs trailing, then
///     settles again.
///   * ESCAPES with the TAIL FLIP: the abdomen snaps under and the whole animal
///     shoots BACKWARD about 0.06u in 0.12 s, two to four times with a quarter
///     second between, then sits still a moment (the SETTLE, feelers up) and
///     walks back toward home.
/// It turns by pivoting on its legs. It never bends sideways; only the abdomen
/// curls, and only UNDER (seen from above that is a shortening).
///
/// THE FOUR CREATURE GATES (see "Creatures and anything alive" in CLAUDE.md):
///   1. VISIBLE here? Only over a lit shallow bed, which is why it lives in the
///      shallows: its home is chosen where the floor is 0.02u to 0.12u down
///      (the bed shows), and a step to where the haze would pass 0.65 is
///      refused. It is see-through, so the bed shows through its body too.
///   2. In this view's PERSPECTIVE? It is drawn sunk down the screen by its
///      depth (Light.SinkPx), hazed toward the water (Light.Murk), shrunk
///      (Light.Scale) and squashed by the view (Ripples.Squash), like a koi.
///   3. Turns by BENDING or by a pivot? It pivots on its legs; the body is one
///      stiff shell on a straight spine, and the abdomen only curls under.
///   4. MOVES like the animal? A slow walk, long feeds, a rare swim, and the
///      escape goes BACKWARD (a computed test in the instruments: the way the
///      shell faces against the way it moves).
///
/// THE MIND (the same recipe as the crayfish; Core/Minds). Two dials, dealt at
/// birth so no two are alike: BOLD (feeds longer in the open, flicks only from
/// a nearer threat, ranges further from home) and BUSY (walks and swims more,
/// feeds less). They only load the dice. A prawn keeps within 0.2u of its home
/// (a bold one 0.3u); a scare may carry it one flick (0.06u) further, and then it
/// walks back.
/// </summary>
internal sealed class Prawn : Body, ISnack
{
    private const int Points = 10;
    private enum Doing
    {
        Feed,      // standing still, the front legs picking at the silt
        Walk,      // walking to a spot on the bed
        Swim,      // lifted off the bed a little, going forward on the swimmerets
        Turn,      // a pivot on the spot (0.3 s a quarter turn; a quick one before a scare's flicks)
        Flick,     // the escape: backward tail flips with a pause between
        Settle,    // still a second or two after the flicks, feelers up
    }

    private const float FlickTime = 0.12f, FlickPause = 0.25f;
    private const float WalkRate = 0.012f, SwimRate = 0.025f;     // u per second
    private const float CurlBreak = 0.42f;                         // where the carapace ends and the abdomen begins, as a fraction of the length

    private readonly PrawnBed _bed;
    private readonly Pond _pond;
    private readonly Random _rng;
    private readonly bool[] _view;
    private readonly float _u, _L;                       // _L = rostrum to tail fan length at the surface, ground pixels
    private readonly int _idx;
    private readonly float _bold, _busy, _hx, _hy, _range;   // the dials, the home (ground), how far it ranges
    private readonly Mind<Doing> _mind = new(Doing.Feed);
    private Doing _doing => _mind.Doing;
    private readonly Cooldown _scareCool = new();
    private readonly float[] _dice = new float[3];

    // ---- the animal: where its middle is (ground pixels), which way it faces, how deep, how curled ----
    private float _x, _y, _heading, _zBed, _z, _lift, _curl;
    private float _sc = 1, _len, _sinkPx;
    private float _spd, _vel, _speedLog;                 // commanded speed (ground px/s), smoothed speed along the heading, speed for the log
    private float _walkK, _gait, _pickPh, _pickK, _wob0, _wob1, _antUp, _beat, _trail;
    // ---- going somewhere ----
    private float _gx, _gy, _walkBan;
    private Doing _next = Doing.Walk;                    // what a Turn leads into
    private float _turnTo; private bool _turnFast;
    private float _swimT, _swimTotal, _swimPeak;
    private bool _lastWasFeed;
    // ---- the escape ----
    private int _flicksLeft, _stoppedShort, _scares;
    private float _flickT, _flickX, _flickY;
    private bool _flickActive, _flickStopped, _inScare, _freshScare;
    private int _threatIdx = -1, _threatN;
    // ---- for the log ----
    private bool _logVisible;
    private float _nearPx = -1;

    private float CentreY => _bed.CentreY;
    private PointF ToScreen(float gx, float gy) => _bed.ToScreen(gx, gy);
    private (float, float) ToGround(float x, float y) => _bed.ToGround(x, y);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float p) => p * p * (3 - 2 * p);
    private float WalkSpeed => _u * WalkRate * (0.85f + 0.3f * _busy);

    /// <summary>The prawn's middle, screen pixels.</summary>
    public PointF Spot => ToScreen(_x, _y);

    // ---- The snack's view of it (ISnack): a koi at least half hungry, or the turtle, may single it out (Snatch) ----
    private bool _alive = true;
    private float _reborn;                                // seconds until an eaten prawn is back at its home
    public bool Alive => _alive;
    public Snatch? Hunter { get; set; }
    public PointF Nose => Spine[0];                       // the rostrum as drawn
    float ISnack.X => _x;
    float ISnack.Y => _y;
    float ISnack.Z => _z;
    public int Stage => 5;                                // (a tadpole's stages are 0 to 4: the predators' log tells a prawn by this)
    public bool OnTheWayOut => false;

    /// <summary>
    /// Eaten: nothing is drawn or updated for a while, then it is back at its home, as a new prawn from
    /// under the stones (90 to 240 s later, its own dice). The hunter is released; a prawn's mind goes on
    /// ticking while it is gone, so it comes back doing whatever the dice next say.
    /// </summary>
    public void Eaten()
    {
        _alive = false;
        Hunter = null;
        _reborn = 90 + 150 * (float)_rng.NextDouble();
    }

    private void Reborn()
    {
        _alive = true;
        _x = _hx; _y = _hy;
        _lift = 0; _curl = 0;
        _flickActive = false; _inScare = false; _flicksLeft = 0;
    }

    public Prawn(PrawnBed bed, int index, PointF home, float bold, float busy, Random rng) : base(Points)
    {
        _bed = bed; _pond = bed.Pond; _view = bed.View; _rng = rng; _u = bed.U;
        _idx = index; _bold = bold; _busy = busy;
        _hx = home.X; _hy = home.Y;
        _range = _u * (0.2f + 0.1f * bold);
        _L = _u * (0.036f + 0.008f * (float)rng.NextDouble());     // 0.036u to 0.044u: no two quite alike
        // Where the spine's points sit along the body, nose 0 to tail fan 1 (closer together at the ends, where the shape changes fastest).
        float[] along = [0f, 0.07f, 0.18f, 0.30f, 0.42f, 0.54f, 0.66f, 0.78f, 0.89f, 1f];
        for (int i = 0; i < Points; i++) Along[i] = along[i];
        _x = _hx; _y = _hy;
        _heading = (float)(rng.NextDouble() * MathF.Tau) - MathF.PI;
        _zBed = bed.ZAt(_x, _y); _z = _zBed;
        _sc = Light.Scale(_z); _len = _L * _sc;
        _wob0 = (float)(rng.NextDouble() * MathF.Tau); _wob1 = (float)(rng.NextDouble() * MathF.Tau);
        _scareCool.Start(0);
        // Each starts in a feed of its own length, so three prawns never change what they are doing together.
        _mind.Begin(Doing.Feed, 3 + 12 * (float)rng.NextDouble());
        _lastWasFeed = true;
        LaySpine();
    }

    // ==================== UPDATE ====================

    public void Update(float dt, PointF[] threats)
    {
        if (!_alive) { _reborn -= dt; if (_reborn <= 0) Reborn(); else return; }
        _mind.Tick(dt);
        _scareCool.Tick(dt);
        _walkBan -= dt;
        _freshScare = false;
        float ox = _x, oy = _y;
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        if (Prawns.MotionLog != null) _nearPx = NearestThreat(threats);     // for the log only: how close the nearest threat is, so a reader can time the reaction from the moment it came within reach
        React(threats);

        switch (_doing)
        {
            case Doing.Feed:
                if (_mind.Over) Decide();
                break;
            case Doing.Walk:
                DoMove(dt, WalkSpeed, false);
                break;
            case Doing.Swim:
                DoMove(dt, _u * SwimRate, true);
                break;
            case Doing.Turn:
                DoTurn(dt);
                break;
            case Doing.Flick:
                DoFlick(dt);
                break;
            case Doing.Settle:
                if (_mind.Over) AfterScare();
                break;
        }

        // ---- legs, feelers, curl, lift and depth ----
        float dx = _x - ox, dy = _y - oy;
        _speedLog = dt > 0 ? MathF.Sqrt(dx * dx + dy * dy) / dt : 0;
        float v = dt > 0 ? (dx * MathF.Cos(_heading) + dy * MathF.Sin(_heading)) / dt : 0;
        _vel += (v - _vel) * (1 - MathF.Exp(-dt * 12));
        float nominal = _u * WalkRate;
        _walkK = _doing == Doing.Walk ? Math.Min(1f, MathF.Abs(_vel) / nominal) : 0f;
        if (_doing == Doing.Turn) _walkK = 0.5f;               // the legs shuffle while it swings round
        float legRate = MathF.Tau * 4f * (_vel / nominal);     // four steps a second at a full walk
        if (_doing == Doing.Turn) legRate = MathF.Tau * 2f;
        _gait = (_gait + legRate * dt) % MathF.Tau;
        _wob0 = (_wob0 + dt * (1.1f + 0.6f * _walkK)) % MathF.Tau;
        _wob1 = (_wob1 + dt * (1.5f + 0.6f * _walkK)) % MathF.Tau;
        _pickK += ((_doing == Doing.Feed ? 1f : 0f) - _pickK) * (1 - MathF.Exp(-dt * 6));
        _pickPh = (_pickPh + dt * MathF.Tau * (1.8f + 0.9f * MathF.Sin(_wob0))) % MathF.Tau;   // the picking is not a metronome
        _antUp += ((_doing == Doing.Settle ? 1f : 0f) - _antUp) * (1 - MathF.Exp(-dt * 5));
        _beat = (_beat + dt * MathF.Tau * 9f) % MathF.Tau;
        float liftTarget = 0;
        if (_doing == Doing.Swim)
        {
            _swimT += dt;
            float p = Math.Clamp(_swimT / MathF.Max(0.1f, _swimTotal), 0f, 1f);
            liftTarget = _swimPeak * MathF.Sin(MathF.PI * p);   // up, along, and down again over the swim
        }
        _lift += (liftTarget - _lift) * (1 - MathF.Exp(-dt * 6));
        _trail = Math.Clamp(_lift / 0.012f, 0f, 1f);
        float zt = _bed.ZAt(_x, _y);
        _zBed += (zt - _zBed) * (1 - MathF.Exp(-dt / 0.3f));
        if (_zBed > zt) _zBed = zt;                            // never deeper than the bed under it (rising ground lifts it at once; sinking is eased)
        _z = MathF.Max(0f, _zBed - _lift / Pond.DepthU);
        _curl += ((_flickActive ? 1f : 0f) - _curl) * (1 - MathF.Exp(-dt * 30));
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        LaySpine();
        _logVisible = false;
        for (int i = 0; i < Points && !_logVisible; i++)
        {
            int ix = (int)Spine[i].X, iy = (int)Spine[i].Y;
            if (ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _view[iy * _pond.Width + ix]) _logVisible = true;
        }
    }

    /// <summary>One line of the instrument log (see the Prawns constructor for the columns). Only called while a log is open.</summary>
    public void Log(long frame, double t, Action<string> write)
    {
        PointF mid = ToScreen(_x, _y);
        int ix = Math.Clamp((int)mid.X, 0, _pond.Width - 1), iy = Math.Clamp((int)mid.Y, 0, _pond.Height - 1);
        float floorZ = _pond.Floor[iy * _pond.Width + ix] / 255f;
        write(string.Create(CultureInfo.InvariantCulture,
            $"{frame},{t:F3},{_idx},{_doing},{_x:F2},{_y:F2},{_z:F4},{_heading:F3},{_speedLog:F2},{_curl:F2},{(_logVisible ? 1 : 0)},{Spine[0].X:F1},{Spine[0].Y:F1},{Spine[Points - 1].X:F1},{Spine[Points - 1].Y:F1},{_hx:F1},{_hy:F1},{_stoppedShort},{_scares},{_threatIdx},{_threatN},{(_view[iy * _pond.Width + ix] ? 1 : 0)},{floorZ:F4},{Light.Murk(_z):F3},{_lift:F4},{(_freshScare ? 1 : 0)},{_nearPx:F1},{(_u * (0.07f - 0.02f * _bold)):F1}"));
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: a prawn has one thing on its mind at a time. When it runs
    // out of it, it rolls weighted dice for the next. The two dials (bold, busy)
    // only change how big each face of the dice is. Underneath, the steering
    // refuses any step onto a pixel that is not open, shallow water, and the
    // scare check can cut in on almost anything.

    /// <summary>Rolls the next doing. A feed is the biggest face, a walk the next, a swim a small one.</summary>
    private void Decide()
    {
        _spd = 0;
        float dh = Dist(_x, _y, _hx, _hy);
        if (dh > _range * 0.85f && _walkBan <= 0 && PickGoal(0, 0, false, true)) { StartMove(Doing.Walk); return; }
        bool canGo = _walkBan <= 0;
        _dice[0] = (1.6f - 0.8f * _busy + 0.3f * _bold) * (_lastWasFeed ? 0.6f : 1f);     // the biggest face; a little less likely right after a long feed
        _dice[1] = canGo ? 0.45f + 0.9f * _busy : 0f;
        _dice[2] = canGo ? 0.04f + 0.10f * _busy : 0f;
        int face = Dice.Pick(_rng, _dice);
        if (face == 1 && PickGoal(_u * 0.03f, _u * 0.12f, false, false)) { _lastWasFeed = false; StartMove(Doing.Walk); return; }
        if (face == 2 && PickGoal(_u * 0.10f, _u * 0.20f, true, false)) { _lastWasFeed = false; StartMove(Doing.Swim); return; }
        // Feed (also the answer when no goal could be found): 10 to 40 s, longer for a bold one and a lazy one.
        float dur = (10 + 30 * (float)_rng.NextDouble()) * (0.85f + 0.3f * _bold - 0.3f * _busy);
        _mind.Begin(Doing.Feed, Math.Clamp(dur, 10f, 40f));
        _lastWasFeed = true;
    }

    /// <summary>
    /// A spot on the bed to go to, within range of home, reached by a line that is
    /// free all the way. A swim goes roughly the way it faces (it swims forward);
    /// a walk may go any way. "Home" asks for a spot near home to come back to.
    /// </summary>
    private bool PickGoal(float minD, float maxD, bool ahead, bool home)
    {
        for (int tries = 0; tries < 36; tries++)
        {
            float gx, gy;
            if (home)
            {
                // Back toward home, not necessarily all the way (0.2u at 0.012u a second is a quarter of a minute of walking
                // after every scare): to a spot on the line to home that is inside two fifths of the range, with a little scatter.
                float hd = Dist(_x, _y, _hx, _hy), keep = MathF.Min(hd, _range * 0.4f);
                float ux = hd > 1e-3f ? (_x - _hx) / hd : 0, uy = hd > 1e-3f ? (_y - _hy) / hd : 0;
                float a0 = (float)(_rng.NextDouble() * MathF.Tau), r0 = _u * 0.02f * (float)_rng.NextDouble();
                gx = _hx + ux * keep * 0.6f + MathF.Cos(a0) * r0; gy = _hy + uy * keep * 0.6f + MathF.Sin(a0) * r0;
            }
            else
            {
                float a = ahead ? _heading + ((float)_rng.NextDouble() - 0.5f) * 1.6f : (float)(_rng.NextDouble() * MathF.Tau);
                // The shallows are a narrow band by the stones: after twenty-four tries at the wanted distance, shorter steps (a shuffle of a body length or two) rather than none.
                float r = tries < 24 ? minD + (maxD - minD) * (float)_rng.NextDouble() : _u * (0.012f + 0.02f * (float)_rng.NextDouble());
                gx = _x + MathF.Cos(a) * r; gy = _y + MathF.Sin(a) * r;
                if (Dist(gx, gy, _hx, _hy) > _range) continue;
            }
            if (!_bed.Free(gx, gy) || !LineFree(_x, _y, gx, gy)) continue;
            _gx = gx; _gy = gy;
            return true;
        }
        if (home) { _gx = _hx; _gy = _hy; return true; }   // home itself was free when it was chosen
        return false;
    }

    /// <summary>
    /// Sets off for the goal: a pivot first if it faces well away from it (a prawn
    /// does not walk sideways), then the walk or the swim, timed by the distance.
    /// </summary>
    private void StartMove(Doing what)
    {
        float err = Wrap(MathF.Atan2(_gy - _y, _gx - _x) - _heading);
        if (MathF.Abs(err) > 0.5f)
        {
            _next = what; _turnTo = MathF.Atan2(_gy - _y, _gx - _x); _turnFast = false;
            _mind.Begin(Doing.Turn, Mind<Doing>.Forever);
            return;
        }
        float d = Dist(_x, _y, _gx, _gy);
        if (what == Doing.Swim)
        {
            float t = d / (_u * SwimRate) + 0.8f;
            _swimT = 0; _swimTotal = t; _swimPeak = _u > 0 ? 0.01f + 0.01f * (float)_rng.NextDouble() : 0.01f;
            _mind.Begin(Doing.Swim, t + 1f);
        }
        else
            _mind.Begin(Doing.Walk, Math.Clamp(d / WalkSpeed * 1.4f + 1.5f, 3f, 12f));
    }

    /// <summary>
    /// One frame of walking or swimming toward the goal. It steers a little as it
    /// goes (a prawn's walk is nearly straight), speeds up and slows down gently,
    /// and a refused step ends the move and bans walking for two seconds (rolled
    /// again on the spot it picked the same wall again, frame after frame).
    /// </summary>
    private void DoMove(float dt, float pace, bool swimming)
    {
        float dx = _gx - _x, dy = _gy - _y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < _len * 0.2f || _mind.Over) { Decide(); return; }
        float err = Wrap(MathF.Atan2(dy, dx) - _heading);
        if (MathF.Abs(err) > 0.9f) { StartMove(_doing); return; }        // well off: pivot first
        _heading = Wrap(_heading + Math.Clamp(err, -1.5f * dt, 1.5f * dt));
        float target = MathF.Min(pace, d * 1.5f);
        _spd += (target - _spd) * (1 - MathF.Exp(-dt * 3f));
        float step = _spd * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        if (FreeStep(nx, ny, _heading)) { _x = nx; _y = ny; }
        else { _walkBan = 2f; Decide(); }
    }

    private void DoTurn(float dt)
    {
        float err = Wrap(_turnTo - _heading);
        float rate = _turnFast ? 22f : MathF.PI / 2 / 0.3f;      // 0.3 s a quarter turn; the scare's pivot is 0.15 s for half a turn
        _heading = Wrap(_heading + Math.Clamp(err, -rate * dt, rate * dt));
        if (MathF.Abs(Wrap(_turnTo - _heading)) < 0.03f || _mind.Over)
        {
            _spd = 0;
            if (_next == Doing.Flick) BeginFlick();
            else StartMove(_next);
        }
    }

    // ---- the scare ----

    /// <summary>
    /// A fish, a turtle or a duck near it, while it feeds, walks or swims: it
    /// flicks at once (swinging round first, in 0.15 s, if the threat is behind
    /// it, so that backward means away). Within 0.07u (bold: 0.05u) of its
    /// middle. A 3 second cooldown, so a koi keeping pace cannot set it off
    /// every frame.
    /// </summary>
    private void React(PointF[] threats)
    {
        if (threats.Length == 0 || !_scareCool.Ready || _inScare) return;
        if (_doing is Doing.Flick or Doing.Settle) return;
        PointF mid = ToScreen(_x, _y);
        float best = float.MaxValue;
        int bi = -1;
        for (int i = 0; i < threats.Length; i++)
        {
            float dx = threats[i].X - mid.X, dy = threats[i].Y - mid.Y, d2 = dx * dx + dy * dy;
            if (d2 < best) { best = d2; bi = i; }
        }
        if (bi < 0) return;
        float reach = _u * (0.07f - 0.02f * _bold);
        if (best > reach * reach) return;
        _scareCool.Start(3f);
        _inScare = true; _freshScare = true; _scares++;
        _threatIdx = bi; _threatN = threats.Length;
        _spd = 0;
        (float tx, float ty) = ToGround(threats[bi].X, threats[bi].Y);
        float toward = MathF.Atan2(ty - _y, tx - _x), rel = Wrap(toward - _heading);
        // Backward must mean away: with the threat ahead (or beside) it flicks as it faces; with it behind, it puts it ahead first.
        float h0 = MathF.Abs(rel) > 1.2f ? toward : _heading;
        _flicksLeft = 2 + _rng.Next(3);
        // The bed by a stone is narrow: aim the tail at the roomiest way back, preferring the threat ahead and widening to the threat at its side.
        float bestRoom = BackRoom(h0), bestH = h0;
        if (bestRoom < _u * 0.12f)
            foreach (float off in (ReadOnlySpan<float>)[0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f, 1.6f, -1.6f])
            {
                float room = BackRoom(h0 + off);
                if (room > bestRoom + _u * 0.01f) { bestRoom = room; bestH = Wrap(h0 + off); }
            }
        if (bestRoom < _u * 0.02f) { _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble()); return; }   // no room behind it: it freezes, feelers up, rather than flick into a wall
        if (MathF.Abs(Wrap(bestH - _heading)) > 0.03f)
        {
            _next = Doing.Flick; _turnTo = bestH; _turnFast = true;
            _mind.Begin(Doing.Turn, 0.4f);
            return;
        }
        BeginFlick();
    }

    private float NearestThreat(PointF[] threats)
    {
        PointF mid = ToScreen(_x, _y);
        float best = float.MaxValue;
        for (int i = 0; i < threats.Length; i++)
        {
            float dx = threats[i].X - mid.X, dy = threats[i].Y - mid.Y, d2 = dx * dx + dy * dy;
            if (d2 < best) best = d2;
        }
        return best == float.MaxValue ? -1 : MathF.Sqrt(best);
    }

    /// <summary>How far it could flick backward from here along heading h, in ground pixels (up to one flick), testing every few pixels.</summary>
    private float BackRoom(float h)
    {
        float c = MathF.Cos(h), s = MathF.Sin(h), room = 0;
        for (float d = 3; d <= _u * 0.12f; d += 3)
        {
            if (!FreeStep(_x - c * d, _y - s * d, h)) break;
            if (Dist(_x - c * d, _y - s * d, _hx, _hy) > _range + _u * 0.06f) break;
            room = d;
        }
        return room;
    }

    private void BeginFlick()
    {
        _mind.Begin(Doing.Flick, Mind<Doing>.Forever);
        _flickActive = true; _flickStopped = false;
        _flickT = 0; _flickX = _x; _flickY = _y;
    }

    /// <summary>
    /// One frame of the escape: while a flick is active, slide the middle backward
    /// along its own axis 0.06u over 0.12 s (eased, in sub-steps of two pixels); a
    /// step onto a refused pixel stops the flick short. Between flicks a quarter
    /// second pause; after the last, sit still.
    /// </summary>
    private void DoFlick(float dt)
    {
        _flickT += dt;
        if (_flickActive)
        {
            float p = Math.Min(1f, _flickT / FlickTime), e = Smooth(p);
            if (!_flickStopped)
            {
                float tx = _flickX - MathF.Cos(_heading) * _u * 0.06f * e, ty = _flickY - MathF.Sin(_heading) * _u * 0.06f * e;
                float mx = tx - _x, my = ty - _y;
                int n = (int)(MathF.Sqrt(mx * mx + my * my) / 2) + 1;
                for (int k = 0; k < n; k++)
                {
                    float nx = _x + mx / n, ny = _y + my / n;
                    PointF sc = ToScreen(nx, ny);
                    if (!FreeStep(nx, ny, _heading) || Dist(nx, ny, _hx, _hy) > _range + _u * 0.06f || sc.X < _len || sc.X > _pond.Width - _len || sc.Y < _len || sc.Y > _pond.Height - _len)
                    { _flickStopped = true; break; }   // and never off the picture
                    _x = nx; _y = ny;
                }
            }
            if (p >= 1)
            {
                if (_flickStopped) _stoppedShort++;
                _flickActive = false;
                _flicksLeft--;
                _flickT = 0;
                if (_flicksLeft <= 0) _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble());
            }
        }
        else if (_flickT >= FlickPause)
        {
            // The next flick only if there is room behind it: the first was aimed at the roomiest way back, but a
            // wall can still be nearer than three more flicks. Fewer flicks that go somewhere beat flicks on the spot.
            if (BackRoom(_heading) < _u * 0.025f) _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble());
            else BeginFlick();
        }
    }

    /// <summary>After the settle: the scare is over; if it was carried well away from home (beyond seven tenths of its range) it walks back, else it feeds on where it landed.</summary>
    private void AfterScare()
    {
        _inScare = false;
        _spd = 0;
        if (Dist(_x, _y, _hx, _hy) > _range * 0.7f && PickGoal(0, 0, false, true)) { StartMove(Doing.Walk); return; }
        Decide();
    }

    // ==================== THE BED ====================

    /// <summary>Is a step to this middle position allowed? Its middle and both ends must be free (the same rule as the crayfish's, applied to the whole shell).</summary>
    private bool FreeStep(float nx, float ny, float heading)
    {
        float c = MathF.Cos(heading), s = MathF.Sin(heading);
        return _bed.Free(nx, ny) && _bed.Free(nx + c * _len * 0.4f, ny + s * _len * 0.4f) && _bed.Free(nx - c * _len * 0.4f, ny - s * _len * 0.4f);
    }

    /// <summary>Is the straight line from a to b walkable (every few pixels, middle and both ends free)?</summary>
    private bool LineFree(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1e-3f) return true;
        float c = dx / d, s = dy / d;
        int steps = (int)(d / 3) + 1;
        for (int i = 1; i <= steps; i++)
        {
            float px = ax + dx * i / steps, py = ay + dy * i / steps;
            if (!_bed.Free(px, py) || !_bed.Free(px + c * _len * 0.4f, py + s * _len * 0.4f) || !_bed.Free(px - c * _len * 0.4f, py - s * _len * 0.4f)) return false;
        }
        return true;
    }

    private static float Dist(float ax, float ay, float bx, float by)
    {
        float dx = ax - bx, dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // ==================== BODY ====================

    // Half the body's width along its length, as a fraction of the length: a pointed rostrum, the carapace widest
    // about a third of the way back, a slim abdomen, the tail fan flaring.
    private static readonly float[] ProfS = [0f, 0.07f, 0.18f, 0.30f, 0.42f, 0.54f, 0.66f, 0.78f, 0.89f, 1f];
    private static readonly float[] ProfW = [0.006f, 0.030f, 0.058f, 0.066f, 0.058f, 0.052f, 0.048f, 0.044f, 0.062f, 0.060f];

    private static float Profile(float s)
    {
        for (int i = 1; i < ProfS.Length; i++)
            if (s <= ProfS[i]) return ProfW[i - 1] + (ProfW[i] - ProfW[i - 1]) * (s - ProfS[i - 1]) / (ProfS[i] - ProfS[i - 1]);
        return ProfW[^1];
    }

    /// <summary>Where a body point at fraction s of the length is along the axis, with the curl: the abdomen tucks under, which from above is a SHORTENING of everything behind the carapace.</summary>
    private float PosOf(float s) => s < CurlBreak ? s : CurlBreak + (s - CurlBreak) * (1 - 0.55f * _curl);

    /// <summary>A ground point on the body at fraction s of its length (rostrum 0), out to one side by "side" ground pixels.</summary>
    private (float, float) G(float s, float side)
    {
        float c = MathF.Cos(_heading), sn = MathF.Sin(_heading), pos = PosOf(s);
        return (_x + c * _len * (0.5f - pos) - sn * side, _y + sn * _len * (0.5f - pos) + c * side);
    }

    /// <summary>
    /// A straight spine back from the rostrum, centred on the middle so a pivot
    /// turns it about its middle. In a flick the abdomen curls under: a
    /// shortening of everything behind the carapace and a slightly wider tail.
    /// The whole body is drawn _sc smaller for its depth, moved down the screen
    /// by its depth, and the ground is squashed by the view.
    /// </summary>
    private void LaySpine()
    {
        _sinkPx = Light.SinkPx(_z, _u);
        for (int i = 0; i < Points; i++)
        {
            float a = Along[i];
            float hw = _len * Profile(a) * (a > CurlBreak ? 1 + 0.25f * _curl : 1);
            (float lx, float ly) = G(a, -hw);
            (float rx, float ry) = G(a, hw);
            PointF l = ToScreen(lx, ly), r = ToScreen(rx, ry);
            (float mx, float my) = G(a, 0);
            PointF m = ToScreen(mx, my);
            Spine[i] = new PointF(m.X, m.Y + _sinkPx);
            HalfWidth[i] = MathF.Max(0.7f, MathF.Sqrt((l.X - r.X) * (l.X - r.X) + (l.Y - r.Y) * (l.Y - r.Y)) / 2);
        }
    }

    private bool _shadowPass;
    private float _gutV = 0.14f;      // how far out from the backbone the gut thread reaches, as a share of the half width: wide enough to be a pixel wide on a small prawn (a thread under a pixel came out as a row of dots)

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        r = g = b = 0; alpha = 1;
        // The tail fan's end is scalloped into three lobes (tips at v = 0, +-0.67).
        if (s > 0.9f && s > 0.93f + 0.07f * (0.5f + 0.5f * MathF.Cos(3 * MathF.PI * v))) return false;
        if (_shadowPass) return true;
        float av = MathF.Abs(v);
        float cr = 168, cg = 190, cb = 176, a;      // a pale grey-green glass
        if (s < CurlBreak)
        {
            a = 0.68f;                               // the carapace: a touch more solid than the abdomen
        }
        else if (s < 0.88f)
        {
            // The abdomen: six plates, each joint a little paler, and a few thin dark oblique stripes down the sides (the "suji").
            a = 0.58f;
            float u6 = (s - CurlBreak) / 0.46f * 6f, f = u6 - MathF.Floor(u6);
            float joint = Math.Clamp(1 - f * 10, 0, 1);
            cr += 24 * joint; cg += 20 * joint; cb += 20 * joint;
            float ph = (s - CurlBreak) / 0.46f * 2f + 0.5f * av;
            ph -= MathF.Floor(ph);
            float stripe = av > 0.25f ? Math.Clamp(1 - MathF.Abs(ph - 0.5f) * 14, 0, 1) : 0f;
            cr += (46 - cr) * 0.85f * stripe; cg += (54 - cg) * 0.85f * stripe; cb += (46 - cb) * 0.85f * stripe;
            a += 0.18f * stripe;
        }
        else
        {
            // The tail fan: paler and more see-through toward its edge.
            float edge = Math.Clamp((av - 0.45f) * 1.6f + (s - 0.93f) * 5f, 0, 1);
            cr = 196; cg = 212; cb = 202;
            a = 0.46f - 0.24f * edge;
        }
        // The gut: a dark thread along the backbone from behind the eyes nearly to the tail fan.
        if (s > 0.12f && s < 0.9f)
        {
            float k = Math.Clamp((_gutV - av) / (_gutV * 0.35f), 0, 1);
            cr += (62 - cr) * k; cg += (74 - cg) * k; cb += (60 - cb) * k;
            a += (0.74f - a) * k;
        }
        r = Math.Clamp((int)cr, 0, 255); g = Math.Clamp((int)cg, 0, 255); b = Math.Clamp((int)cb, 0, 255);
        alpha = a;
        return true;
    }

    // ==================== DRAW ====================

    public void Draw(FrameBuffer fb)
    {
        if (!_alive) return;
        float murk = Light.Murk(_z);
        if (_len < 8f)
        {
            // The preview size: a prawn is a few pixels long. One soft dash of its colour and nothing else.
            Stroke(fb, Spine[0], Spine[Points - 1], 200, 218, 204, 0.75f, 1.5f, murk * 0.5f);
            return;
        }
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        _gutV = Math.Clamp(0.6f / MathF.Max(1f, _len * 0.05f), 0.14f, 0.4f);
        // The shadow on the bed: its own shape, a hair down and to the right (within 0.006u on the bed; more as it lifts, never past 0.012u), faint and soft.
        float reach = MathF.Min(_u * 0.012f, _u * 0.006f + _lift * _u * 0.5f);
        _shadowPass = true;
        PaintShadow(fb, _view, reach * 0.6f, reach * 0.8f, 0.15f * (1 - murk), soft * 2f);
        _shadowPass = false;
        DrawLegs(fb, murk);
        if (_len > 40f && _lift > 0.002f) DrawSwimmerets(fb, murk);
        DrawFeelers(fb, murk);
        Paint(fb, _view, _pond.Sun, murk, soft, _pond.Pixels);
        DrawEyes(fb, murk);
    }

    /// <summary>A small oval given in GROUND terms (a centre, an angle on the ground, semi-length and semi-width), turned into the squashed, sunk picture.</summary>
    private void OvalG(FrameBuffer fb, float gx, float gy, float ang, float semiLen, float semiWid, int r, int g, int b, float alpha, float murk)
    {
        float ca = MathF.Cos(ang), sa = MathF.Sin(ang) * Ripples.Squash;
        float lenK = MathF.Sqrt(ca * ca + sa * sa);
        float pa = -MathF.Sin(ang), pb = MathF.Cos(ang) * Ripples.Squash;
        float widK = MathF.Sqrt(pa * pa + pb * pb);
        PointF c = ToScreen(gx, gy);
        Oval(fb, _view, new PointF(c.X, c.Y + _sinkPx), MathF.Atan2(sa, ca), MathF.Max(0.8f, semiLen * lenK), MathF.Max(0.9f, semiWid * widK),
            r, g, b, alpha, murk, _pond.Sun, rays: false, water: _pond.Pixels);
    }

    /// <summary>
    /// Five pairs of thin walking legs under the carapace, each swinging fore and
    /// aft on a wave down the body (front to back), the two sides half a step
    /// apart. Still when it is still. In a feed the FRONT pair reaches in and
    /// picks (tiny repeated motions); in a swim they all trail back; in a flick
    /// they fold in.
    /// </summary>
    private void DrawLegs(FrameBuffer fb, float murk)
    {
        float ll = _len * 0.075f;
        for (int k = 0; k < 5; k++)
        {
            float along = 0.17f + 0.058f * k;
            float hw = _len * Profile(along);
            for (int side = -1; side <= 1; side += 2)
            {
                float swing = 0.5f * _walkK * MathF.Sin(_gait + 0.8f * k + (side > 0 ? 0 : MathF.PI));
                float basic = MathF.PI / 2 - 0.55f + 0.3f * k;
                float ang = basic + swing + 0.9f * _curl + 0.7f * _trail * (0.4f + 0.15f * k);
                if (k < 2 && _pickK > 0.01f)
                {
                    // Picking: the front pairs reach in toward the mouth and back out, the two sides out of step.
                    float pick = 0.5f + 0.5f * MathF.Sin(_pickPh * (1 + 0.3f * k) + (side > 0 ? 0 : 2.1f) + k);
                    ang += (0.35f - 0.2f * pick - basic * 0.55f) * _pickK;
                }
                (float ax, float ay) = G(along, side * hw * 0.7f);
                float a = _heading + side * ang;
                OvalG(fb, ax + MathF.Cos(a) * ll, ay + MathF.Sin(a) * ll, a, ll, _len * 0.006f, 125, 150, 134, 0.42f, murk);
            }
        }
    }

    /// <summary>Four tiny paddles under the abdomen, beating while it swims (drawn only when the prawn is long enough on the screen to show them).</summary>
    private void DrawSwimmerets(FrameBuffer fb, float murk)
    {
        for (int k = 0; k < 4; k++)
        {
            float along = 0.50f + 0.09f * k;
            for (int side = -1; side <= 1; side += 2)
            {
                float beat = 0.5f + 0.5f * MathF.Sin(_beat + 0.9f * k);
                (float ax, float ay) = G(along, side * _len * Profile(along) * 0.6f);
                float a = _heading + side * (MathF.PI / 2 + 0.5f * beat);
                OvalG(fb, ax + MathF.Cos(a) * _len * 0.025f, ay + MathF.Sin(a) * _len * 0.025f, a, _len * 0.03f, _len * 0.01f, 150, 172, 158, 0.5f * Math.Clamp(_lift / 0.006f, 0, 1), murk);
            }
        }
    }

    /// <summary>
    /// Two long thin antennae swept back past the tail (one and a half body
    /// lengths), each of three pieces whose tips wave slowly; two short ones
    /// ahead of the rostrum. They spread wider when it settles after a scare.
    /// </summary>
    private void DrawFeelers(FrameBuffer fb, float murk)
    {
        float th = MathF.Max(1f, _len * 0.012f);
        for (int side = -1; side <= 1; side += 2)
        {
            float ph = side > 0 ? _wob0 : _wob1;
            (float px, float py) = G(0.05f, side * _len * 0.02f);
            PointF prev = D(px, py);
            float spread = 0.28f + 0.35f * _antUp;
            for (int j = 0; j < 3; j++)
            {
                float a = _heading + MathF.PI - side * (spread - 0.06f * j + 0.25f * MathF.Sin(ph + 1.3f * j));
                px += MathF.Cos(a) * _len * 0.5f; py += MathF.Sin(a) * _len * 0.5f;
                PointF next = D(px, py);
                Stroke(fb, prev, next, 120, 138, 124, 0.5f - 0.1f * j, th, murk);
                prev = next;
            }
            // The short antenna ahead of the rostrum.
            (float sx, float sy) = G(0f, 0f);
            float aa = _heading + side * (0.3f + 0.15f * MathF.Sin(ph * 1.5f));
            Stroke(fb, D(sx, sy), D(sx + MathF.Cos(aa) * _len * 0.2f, sy + MathF.Sin(aa) * _len * 0.2f), 130, 148, 134, 0.5f, th, murk);
        }
    }

    /// <summary>Two dark eyes on short stalks, either side of the pointed rostrum.</summary>
    private void DrawEyes(FrameBuffer fb, float murk)
    {
        float er = MathF.Max(0.9f, _len * 0.022f);
        for (int side = -1; side <= 1; side += 2)
        {
            (float bx, float by) = G(0.15f, side * _len * 0.02f);
            (float ex, float ey) = G(0.09f, side * _len * 0.05f);
            if (_len > 14f) Stroke(fb, D(bx, by), D(ex, ey), 90, 104, 94, 0.6f, MathF.Max(1f, _len * 0.012f), murk);
            OvalG(fb, ex, ey, 0, er, er, 18, 14, 14, 0.95f, murk);
        }
    }

    private PointF D(float gx, float gy)
    {
        PointF p = ToScreen(gx, gy);
        return new PointF(p.X, p.Y + _sinkPx);
    }

    /// <summary>A thin line hazed toward the water under its start (so a feeler at depth fades as the body does).</summary>
    private void Stroke(FrameBuffer fb, PointF a, PointF b, int r, int g, int bl, float alpha, float thick, float murk)
    {
        int ix = Math.Clamp((int)a.X, 0, _pond.Width - 1), iy = Math.Clamp((int)a.Y, 0, _pond.Height - 1);
        uint wc = _pond.Pixels[iy * _pond.Width + ix];
        r += (int)((((wc >> 16) & 0xFF) - r) * murk); g += (int)((((wc >> 8) & 0xFF) - g) * murk); bl += (int)(((wc & 0xFF) - bl) * murk);
        fb.Line(a, b, Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(bl, 0, 255)), alpha, thick, _view);
    }
}
