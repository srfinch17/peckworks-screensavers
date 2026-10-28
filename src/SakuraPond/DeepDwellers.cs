using System.Globalization;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The animals of the DEEP bed (round thirty-seven, his line: "i want a crayfish
/// and prawns of maybe different species to occupy the deep. they will
/// occasionally battle each other. they will skitter along the deep areas barely
/// visible, foraging. they will sometimes get eaten by some of the largest koi,
/// sometimes turtles, and snakes. if minnows swim down by them they have a chance
/// of catching a minnow in its pincers"): a signal crayfish or two and a few
/// long-armed prawns, down where the water is deepest, seen through most of the
/// pond's haze. The scene holds this wrapper (the same shape as Prawns); the
/// animals are Dweller below, one class for both species. With the knob at 0 the
/// wrapper holds nothing and every call returns at once.
///
/// WORLD LAW, in the pond's own terms: a deep dweller is SOLID TO NOTHING. It
/// lives on the bed under everything that swims (a fish keeps a margin of water
/// over the bed, so it passes OVER it), under the pads and the petals. It reacts
/// instead: a koi, turtle, duck or diving snake over it sends it flicking
/// backward, or a bold one raises its claws. Two dwellers keep a little apart on
/// their walks and touch only in a BOUT, which is a thing they do on purpose.
/// What stands on the bed (the island's hill, the formations, the bank's slope
/// where the water is under a third of the pond's depth) is a wall to it: a step
/// onto a pixel that is not open, deep water is refused.
///
/// IT IS FOOD and IT EATS: a dweller is an ISnack (the shared Snatch in
/// Tadpoles.cs works on it: a big hungry koi down on the bed, a diving turtle or a
/// diving snake may single one out), and it takes a minnow of a school that has
/// dipped to the bed, with its claws (Dweller.Lunge, through Minnows.Take).
/// </summary>
internal sealed class DeepDwellers : ISnackField
{
    /// <summary>The instrument log (PECKWORKS_DEEPLOG names a file, set up once here, flushed at exit): one line per dweller per frame. Null in the real pond, and then it costs nothing.</summary>
    public static Action<string>? MotionLog;
    /// <summary>Instruments only: how much oftener than the real pond two dwellers pick a fight (the chance times this, the rest between fights divided by it). PECKWORKS_DEEPBOUT. 1 in the real pond.</summary>
    public static float BoutOften = 1f;

    private readonly Dweller[] _all;
    private readonly Bout[] _bouts;
    private readonly float[] _pairCool;          // seconds before the pair (a, b) may fight again, a * n + b
    private readonly DeepBed _bed;
    private readonly Random _rng;                // the dwellers' OWN dice (the fights' rolls, a newcomer's edge): nothing else in the scene moves when they are added
    private double _t;
    private long _frame;
    public int Bouts;                            // fights so far (the instruments read it)

    /// <param name="count">How many dwellers (the "Deep dwellers" knob). Every third from the first is a crayfish, the rest are prawns. 0 = none.</param>
    /// <param name="viewDeep">Open water in view less the stones' caps and collars (the scene's deep stencil): where a dweller may stand and be drawn.</param>
    /// <param name="minnows">The schools, so a dweller may take a fish that comes down to it (null without minnows).</param>
    public DeepDwellers(Pond pond, int count, bool[] viewDeep, Minnows? minnows, Random rng)
    {
        _rng = rng;
        _bed = new DeepBed(pond, viewDeep);
        if (count <= 0) { _all = []; _bouts = []; _pairCool = []; return; }
        float[] bold = Traits.Deal(rng, count), busy = Traits.Deal(rng, count);
        List<PointF> homes = _bed.Spots(rng, count, pond.U * 0.15f);
        var animals = new List<Dweller>();
        for (int k = 0; k < homes.Count; k++)
            animals.Add(new Dweller(_bed, k, k % 3 == 0 ? DeepKind.Crayfish : DeepKind.Prawn, homes[k], bold[k], busy[k], rng, minnows));
        _all = animals.ToArray();
        _bouts = new Bout[Math.Max(1, _all.Length / 2)];
        for (int i = 0; i < _bouts.Length; i++) _bouts[i] = new Bout();
        _pairCool = new float[_all.Length * _all.Length];
        for (int i = 0; i < _pairCool.Length; i++) _pairCool[i] = 60f / BoutOften;   // nobody fights in the first minute
        string? often = Environment.GetEnvironmentVariable("PECKWORKS_DEEPBOUT");
        if (!string.IsNullOrEmpty(often) && float.TryParse(often, CultureInfo.InvariantCulture, out float o)) BoutOften = o;
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_DEEPLOG");
        if (!string.IsNullOrEmpty(file) && MotionLog == null)
        {
            var writer = new StreamWriter(file) { AutoFlush = false };
            writer.WriteLine("# F,t,idx,kind,doing,x,y,z,heading,speed,curl,visible,noseX,noseY,bout,phase,lunges,catches,wins,losses,scares,nearPx,alive,heldS");
            writer.WriteLine($"# dwellers {_all.Length} of {count}");
            MotionLog = line => writer.WriteLine(line);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => writer.Flush();
        }
    }

    /// <summary>How many were placed (a seed with little deep water in view has fewer).</summary>
    public int Count => _all.Length;

    /// <summary>
    /// The ground middle of one living dweller, picked with the caller's dice (the minnows' schools ask, with their
    /// own: half their dips to the bed are to a spot near one of these, so that "if minnows swim down by them"
    /// happens in an hour of pond rather than a night). Null when none is out.
    /// </summary>
    public PointF? AnySpot(Random rng)
    {
        int alive = 0;
        foreach (Dweller d in _all) if (d.Alive) alive++;
        if (alive == 0) return null;
        int pick = rng.Next(alive);
        foreach (Dweller d in _all) if (d.Alive && pick-- == 0) return new PointF(d.GX, d.GY);
        return null;
    }

    /// <param name="threats">Screen points of what sends a dweller flicking backward: every koi's head and middle, turtle noses and shells, the ducks' heads, and the diving snake's head.</param>
    public void Update(float dt, PointF[] threats)
    {
        if (_all.Length == 0) return;
        _t += dt;
        _frame++;
        for (int i = 0; i < _all.Length; i++) _all[i].Update(dt, threats, _all);
        foreach (Bout b in _bouts) if (b.Active) b.Update(dt, _rng);
        Referee(dt);
        if (MotionLog != null) for (int i = 0; i < _all.Length; i++) _all[i].Log(_frame, _t, MotionLog);
    }

    /// <summary>
    /// The fights. Two dwellers both idle (feeding, walking or settled), both free, within 0.14u
    /// of each other on the bed, and the pair rested from its last fight: a roll a second, likelier
    /// for two bold ones, and about one chance in thirty a second of nearness in the real pond (a
    /// stress dial multiplies it). The winner is rolled when the fight begins, from size, kind and
    /// boldness (a crayfish nearly always beats a prawn; the smaller of two of a kind wins one time
    /// in five), and the Bout plays it out: face, display, close, grapple, break.
    /// </summary>
    private void Referee(float dt)
    {
        int n = _all.Length;
        for (int i = 0; i < _pairCool.Length; i++) if (_pairCool[i] > 0) _pairCool[i] -= dt;
        for (int a = 0; a < n; a++)
        {
            Dweller da = _all[a];
            if (!da.MayFight) continue;
            for (int b = a + 1; b < n; b++)
            {
                Dweller db = _all[b];
                if (!db.MayFight || _pairCool[a * n + b] > 0) continue;
                float dx = da.GX - db.GX, dy = da.GY - db.GY, d2 = dx * dx + dy * dy, reach = _bed.U * 0.14f;
                if (d2 > reach * reach) continue;
                float chance = 0.035f * (0.5f + da.Bold) * (0.5f + db.Bold) * BoutOften;
                if (_rng.NextDouble() >= 1 - Math.Exp(-chance * dt)) continue;
                Bout? free = null;
                foreach (Bout bt in _bouts) if (!bt.Active) { free = bt; break; }
                if (free == null) continue;
                float sa = da.Strength, sb = db.Strength;
                Dweller winner = _rng.NextDouble() < 0.8 ? (sa >= sb ? da : db) : (sa >= sb ? db : da);
                free.Start(da, db, winner, _rng);
                _pairCool[a * n + b] = (240 + 240 * (float)_rng.NextDouble()) / BoutOften;
                Bouts++;
                Snatch.Log?.Invoke(FormattableString.Invariant($"bout,{da.Kind}{a},{db.Kind}{b},winner {(winner == da ? a : b)},{da.Spot.X:F0},{da.Spot.Y:F0}"));
            }
        }
    }

    /// <summary>On the deep bed: before every fish (everything that swims is above it).</summary>
    public void Draw(FrameBuffer fb)
    {
        for (int i = 0; i < _all.Length; i++) _all[i].Draw(fb);
    }

    /// <summary>The nearest living, unhunted dweller whose nose is within seePx of the mouth and passes "ok" (the predators' Snatch asks; surfaceOnly means nothing on the bed).</summary>
    public ISnack? Spot(PointF mouth, float seePx, bool surfaceOnly = false, Func<ISnack, bool>? ok = null)
    {
        ISnack? best = null;
        float bestD2 = seePx * seePx;
        for (int i = 0; i < _all.Length; i++)
        {
            Dweller p = _all[i];
            if (!p.Alive || p.Hunter != null) continue;
            PointF n = p.Nose;
            float dx = n.X - mouth.X, dy = n.Y - mouth.Y, d2 = dx * dx + dy * dy;
            if (d2 >= bestD2 || (ok != null && !ok(p))) continue;
            bestD2 = d2; best = p;
        }
        return best;
    }

    /// <summary>A predator's mouth has closed on it: gone this frame, and a newcomer of its kind walks in from the picture's edge minutes later (Dweller.Eaten).</summary>
    public void Eat(ISnack s) => ((Dweller)s).Eaten();
}

/// <summary>Which of the two deep species a dweller is.</summary>
internal enum DeepKind
{
    Crayfish,   // the signal crayfish (uchida-zarigani): brown-olive, big claws with a pale patch at the hinge, about 0.075u
    Prawn,      // the oriental river prawn (tenagaebi, "long-armed prawn"): see-through olive-brown, a second pair of legs grown into long thin arms, about 0.055u
}

/// <summary>
/// What every deep dweller asks about the bed: is this ground point open, DEEP,
/// visible water (so a dweller may stand on it), where is the floor under it, and
/// where are good spots. The prawns' PrawnBed turned over: it keeps to the
/// shallows, this keeps to the deep. Ground points are the scene seen straight
/// down (before the view's squash); screen points are what is drawn.
/// </summary>
internal sealed class DeepBed
{
    /// <summary>The floor (0 the surface, 1 the pond's full depth) a dweller needs under it: the deep is where the water is at least this share of the depth (0.33u).</summary>
    public const float DeepZ = 0.33f / Pond.DepthU;   // (0.62 of the 0.54u pond until 2026-10-28: the same 0.33u of water, kept in u when the depth changed)
    public readonly Pond Pond;
    public readonly bool[] View;               // open deep water in view: where a dweller may be drawn AND walk (the stones' caps and collars left out)
    public readonly float U;

    public DeepBed(Pond pond, bool[] viewDeep)
    {
        Pond = pond;
        U = pond.U;
        View = new bool[pond.Width * pond.Height];
        for (int i = 0; i < View.Length; i++) View[i] = viewDeep[i] && pond.Water[i] && pond.Open[i];
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
    /// May a body part be at this ground point? Open water in view, deep (the floor at
    /// least DeepZ of the pond's depth), and the SUNK picture of it also on open water.
    /// A pad, the land, a stone's hill, a cap, the shallows and the slope up to the bank
    /// are all "no".
    /// </summary>
    public bool Free(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = (int)s.X, iy = (int)s.Y;
        if (ix < 0 || iy < 0 || ix >= Pond.Width || iy >= Pond.Height) return false;
        int i = iy * Pond.Width + ix;
        if (!View[i]) return false;
        float f = Pond.Floor[i] / 255f;
        if (f < DeepZ) return false;
        int jy = (int)(s.Y + Light.SinkPx(f, U));
        return jy >= 0 && jy < Pond.Height && View[jy * Pond.Width + ix];
    }

    /// <summary>How many of 12 straight lines out from here stay on free bed for lengthU: a spot with few has a dweller flicking into a wall at every scare.</summary>
    public int OpenSpokes(float gx, float gy, float lengthU)
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
    /// Homes, found once when the scene is made: deep free spots in view with room all round
    /// (a ring of 0.02u free, nine of twelve spokes of 0.05u free), each at least "apart"
    /// ground pixels from the others. Fewer than asked when the seed's deep water in view
    /// has no room for them all.
    /// </summary>
    public List<PointF> Spots(Random rng, int count, float apart)
    {
        var found = new List<PointF>();
        for (int tries = 0; tries < 6000 && found.Count < count; tries++)
        {
            float sx = (float)rng.NextDouble() * Pond.Width, sy = (float)rng.NextDouble() * Pond.Height;
            int ix = (int)sx, iy = (int)sy;
            if (ix < 0 || iy < 0 || ix >= Pond.Width || iy >= Pond.Height || !View[iy * Pond.Width + ix]) continue;
            if (Pond.Floor[iy * Pond.Width + ix] / 255f < DeepZ + 0.05f) continue;    // well inside the deep, not on its rim
            // Not under the cattails' arching leaves (a live layer drawn over the water after the dwellers, 0.24u tall): a
            // crayfish homed beside the clump on one seed spent its claw catch half behind the leaves (the prawns' rule).
            float cdx = sx - Pond.CattailSpot.X, cdy = sy - Pond.CattailSpot.Y;
            if (cdx * cdx + cdy * cdy < U * U * 0.22f * 0.22f) continue;
            (float gx, float gy) = ToGround(sx, sy);
            if (!Free(gx, gy)) continue;
            bool ringOk = true;
            for (int k = 0; k < 6 && ringOk; k++) ringOk = Free(gx + MathF.Cos(k * MathF.Tau / 6) * U * 0.02f, gy + MathF.Sin(k * MathF.Tau / 6) * U * 0.02f);
            if (!ringOk || OpenSpokes(gx, gy, 0.05f) < 9) continue;
            bool clear = true;
            foreach (PointF h in found) if ((h.X - gx) * (h.X - gx) + (h.Y - gy) * (h.Y - gy) < apart * apart) { clear = false; break; }
            if (clear) found.Add(new PointF(gx, gy));
        }
        return found;
    }

    /// <summary>
    /// A way in from beyond the picture for a newcomer: a deep free pixel one step inside the
    /// picture's edge with 0.1u of free bed straight inward from it (the pond goes on past the
    /// view, so an animal arriving from off the picture arrives as a minnow does). The ground
    /// point and the inward heading; false when this seed's deep water touches no edge in view.
    /// </summary>
    public bool EdgeWay(Random rng, out float gx, out float gy, out float heading)
    {
        gx = gy = heading = 0;
        for (int tries = 0; tries < 60; tries++)
        {
            int side = rng.Next(4);
            float along = (float)rng.NextDouble();
            int ix = side == 0 ? 2 : side == 1 ? Pond.Width - 3 : (int)(along * (Pond.Width - 6)) + 3;
            int iy = side == 2 ? 2 : side == 3 ? Pond.Height - 3 : (int)(along * (Pond.Height - 6)) + 3;
            (float ex, float ey) = ToGround(ix + 0.5f, iy + 0.5f);
            if (!Free(ex, ey)) continue;
            float h = side == 0 ? 0 : side == 1 ? MathF.PI : side == 2 ? MathF.PI / 2 : -MathF.PI / 2;
            bool ok = true;
            for (float d = 3; d <= U * 0.1f && ok; d += 3) ok = Free(ex + MathF.Cos(h) * d, ey + MathF.Sin(h) * d);
            if (!ok) continue;
            gx = ex; gy = ey; heading = h;
            return true;
        }
        return false;
    }
}

/// <summary>
/// One animal of the deep bed, of either kind. A signal crayfish (uchida-zarigani,
/// Pacifastacus leniusculus, loose in Japan's lakes since the 1920s): brown to
/// olive-brown, bigger than the red swamp crayfish of the shallows, smooth
/// claws with a chalk-white patch at the hinge of each. Or an oriental river
/// prawn (tenagaebi, Macrobrachium nipponense): see-through olive-brown with
/// dark freckles, half again the sujiebi's size, and its SECOND pair of legs
/// grown into two long thin arms, longer than its body, each ending in a small
/// pincer. Both live on the bottom of the deepest water, where this pond's haze
/// leaves them about four tenths of their own colour: barely visible, as asked.
///
/// WHAT THEY DO (watch either in a tank with a dark floor):
///   * FORAGE: stand and pick at the silt with the small front legs (a crayfish
///     brings a claw to its mouth now and then), six to twenty-five seconds.
///   * SKITTER: short brisk walks of one to four seconds between stops, the
///     legs rippling in a wave down each side, feelers sweeping; a crayfish
///     walks at 0.02u a second, a prawn a little slower.
///   * THREATEN: both claws up and spread (the "meral spread") at a fish that
///     comes over, held a second or two, by the bold ones.
///   * ESCAPE with the TAIL FLIP: the abdomen snaps under and the whole animal
///     shoots BACKWARD two or three times, then sits still, feelers up.
///   * FIGHT (Bout): two meet, face off, display, close until the claws touch,
///     grapple (locked, shoving to and fro, the stronger gaining ground), and
///     the loser flips away while the winner stands with its claws up.
///   * SNAP (Lunge) at a minnow that comes within a claw's reach: a quick turn,
///     the near claw shoots out and closes; a hit holds the fish and it is
///     eaten over a quarter of a minute.
/// It turns by pivoting on its legs about its middle, never bends sideways;
/// only the abdomen curls, and only UNDER (from above, a shortening).
///
/// THE FOUR CREATURE GATES (see "Creatures and anything alive" in CLAUDE.md):
///   1. VISIBLE here? Barely, and that is the brief: it keeps to a floor at least
///      0.62 of the pond's depth (DeepBed.DeepZ), where the haze is about 0.6 and
///      Body.Deepen has greyed and darkened its own colour; it is drawn through
///      the deep stencil, so a stone's cap stands in front of it.
///   2. In this view's PERSPECTIVE? Sunk down the screen by its depth
///      (Light.SinkPx), hazed toward the water (Light.Murk), shrunk (Light.Scale)
///      and squashed by the view (Ripples.Squash), like a koi on the bed.
///   3. Turns by BENDING or by a pivot? A pivot on its legs; one stiff shell on a
///      straight spine, the abdomen only curls under.
///   4. MOVES like the animal? Short walks and long stops, the backward escape,
///      the claws-up display, the shove of a fight, the snap of a claw.
///
/// THE MIND (the prawns' recipe; Core/Minds). Two dials dealt at birth, BOLD
/// (threatens instead of flicking, fights harder, ranges further) and BUSY
/// (walks more, feeds less). A dweller keeps within 0.25u of its home (bold
/// 0.35u) and walks back when a scare or a fight carries it beyond.
/// </summary>
internal sealed class Dweller : Body, ISnack
{
    private const int Points = 12;
    private enum Doing
    {
        Feed,      // standing still, the front legs picking at the silt (a crayfish's claw to its mouth now and then)
        Walk,      // a short brisk walk to a spot on the bed
        Turn,      // a pivot on the spot (0.3 s a quarter turn; a quick one before a scare's flicks)
        Flick,     // the escape: backward tail flips with a pause between
        Settle,    // still a second or two after the flicks, feelers up
        Threat,    // both claws up and spread at a fish over it
        Bout,      // a fight with another dweller (the Bout object moves both)
        Lunge,     // a claw snapped at a minnow within reach
    }

    private const float FlickTime = 0.13f, FlickPause = 0.3f;
    private const float LungeTime = 0.32f, LungeSnapAt = 0.2f;    // the claw shoots out for LungeSnapAt seconds, then closes and draws back

    private readonly DeepBed _bed;
    private readonly Pond _pond;
    private readonly Random _rng;
    private readonly bool[] _view;
    private readonly Minnows? _minnows;
    private readonly float _u, _L;                       // _L = rostrum to tail fan length at the surface, ground pixels
    private readonly int _idx;
    private readonly float _bold, _busy, _range;
    private float _hx, _hy;                              // the home (ground): moved to the edge a newcomer came in by
    private readonly Mind<Doing> _mind = new(Doing.Feed);
    private Doing _doing => _mind.Doing;
    private readonly Cooldown _scareCool = new(), _lungeCool = new();
    private readonly float[] _dice = new float[2];
    public readonly DeepKind Kind;

    // ---- the animal: where its middle is (ground pixels), which way it faces, how deep, how curled ----
    private float _x, _y, _heading, _zBed, _z, _curl;
    private float _sc = 1, _len, _sinkPx;
    private float _spd, _vel, _speedLog;
    private float _walkK, _gait, _pickPh, _pickK, _wob0, _wob1, _antUp;
    // ---- going somewhere ----
    private float _gx, _gy, _walkBan;
    private Doing _next = Doing.Walk;
    private float _turnTo; private bool _turnFast;
    private bool _lastWasFeed;
    // ---- the escape ----
    private int _flicksLeft, _scares;
    private float _flickT, _flickX, _flickY;
    private bool _flickActive, _flickStopped, _inScare;
    private float _nearPx = -1;
    // ---- claws: arm angle, pincer bend, jaw opening, reach, per claw (0 = left, 1 = right), eased toward the pose the doing wants ----
    private readonly float[] _cArm = new float[2], _cPin = new float[2], _cOpen = new float[2], _cExt = new float[2];
    private readonly float[] _reachSm = new float[2], _reachLeft = new float[2];
    private float _feedNext;
    private int _feedSide;
    // ---- the fight ----
    private Bout? _bout;
    public int Wins, Losses;
    // ---- the snap at a minnow ----
    private float _lungeT, _lungeAim;                    // seconds into the snap; the aim as an angle off the heading (the claw turns toward it)
    private int _lungeSide;                              // which claw (0 left, 1 right)
    private float _lungeTX, _lungeTY;                    // the minnow's nose when the snap began, ground pixels
    private float _held;                                 // seconds left eating a caught minnow (drawn in the claw)
    public int Lunges, Catches;
    // ---- for the log ----
    private bool _logVisible;

    private float CentreY => _bed.CentreY;
    private PointF ToScreen(float gx, float gy) => _bed.ToScreen(gx, gy);
    private (float, float) ToGround(float x, float y) => _bed.ToGround(x, y);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float p) => p * p * (3 - 2 * p);
    private float WalkSpeed => _u * (Kind == DeepKind.Crayfish ? 0.024f : 0.02f) * (0.85f + 0.3f * _busy);   // (brisk: the skitter; the shallows' prawn walks at 0.012u)
    private float CurlBreak => Kind == DeepKind.Crayfish ? 0.5f : 0.42f;    // where the carapace ends and the abdomen begins, as a fraction of the length

    /// <summary>The dweller's middle, screen pixels (sunk by its depth).</summary>
    public PointF Spot { get { PointF p = ToScreen(_x, _y); return new PointF(p.X, p.Y + _sinkPx); } }
    /// <summary>Its middle, ground pixels (the referee measures pairs on the ground).</summary>
    public float GX => _x;
    public float GY => _y;
    public float Bold => _bold;
    /// <summary>Idle, free, alive and not rested from a fight: may it be picked for one?</summary>
    public bool MayFight => _alive && _bout == null && _doing is Doing.Feed or Doing.Walk or Doing.Settle && !_inScare && _held <= 0;
    /// <summary>How hard it fights: its length, its kind (a crayfish's claws are the real thing) and its boldness.</summary>
    public float Strength => _L * (Kind == DeepKind.Crayfish ? 1.5f : 1f) * (0.6f + 0.8f * _bold);

    // ---- The snack's view of it (ISnack): a big hungry koi on the bed, a diving turtle or a diving snake may single it out (Snatch) ----
    private bool _alive = true;
    private float _reborn;
    public bool Alive => _alive;
    public Snatch? Hunter { get; set; }
    public PointF Nose => Spine[0];                      // the rostrum as drawn
    float ISnack.X => _x;
    float ISnack.Y => _y;
    float ISnack.Z => _z;
    public int Stage => Kind == DeepKind.Crayfish ? 7 : 6;   // (a tadpole's stages are 0 to 4, a shallows prawn 5: the predators' log tells them apart by this)
    public bool OnTheWayOut => !_alive;

    /// <summary>
    /// Eaten: nothing is drawn or updated for two to five minutes, then a newcomer of its kind
    /// walks in from the picture's edge (the pond goes on past the view) and makes its home a
    /// little way in. A fight it was in ends at once (the other wins by default).
    /// </summary>
    public void Eaten()
    {
        _alive = false;
        Hunter = null;
        _held = 0;
        _bout?.Forfeit(this);
        _reborn = 120 + 180 * (float)_rng.NextDouble();
    }

    private void Reborn()
    {
        if (!_bed.EdgeWay(_rng, out float ex, out float ey, out float h)) { _reborn = 10; return; }   // no deep water at an edge this frame's dice found: try again in ten seconds
        _alive = true;
        _x = ex; _y = ey; _heading = h; _curl = 0; _spd = 0; _vel = 0;
        _flickActive = false; _inScare = false; _flicksLeft = 0;
        float inX = ex + MathF.Cos(h) * _u * 0.12f, inY = ey + MathF.Sin(h) * _u * 0.12f;
        if (_bed.Free(inX, inY)) { _hx = inX; _hy = inY; } else { _hx = ex; _hy = ey; }
        _zBed = _bed.ZAt(_x, _y); _z = _zBed;
        ResetClaws();
        _gx = _hx; _gy = _hy;
        _mind.Begin(Doing.Walk, 12f);
        LaySpine();
    }

    public Dweller(DeepBed bed, int index, DeepKind kind, PointF home, float bold, float busy, Random rng, Minnows? minnows) : base(Points)
    {
        _bed = bed; _pond = bed.Pond; _view = bed.View; _rng = rng; _u = bed.U; _minnows = minnows;
        _idx = index; Kind = kind; _bold = bold; _busy = busy;
        _hx = home.X; _hy = home.Y;
        _range = _u * (0.25f + 0.1f * bold);
        _L = kind == DeepKind.Crayfish ? _u * (0.072f + 0.01f * (float)rng.NextDouble()) : _u * (0.05f + 0.008f * (float)rng.NextDouble());
        // Where the spine's points sit along the body, nose 0 to tail fan 1 (closer together at the ends, where the shape changes fastest).
        float[] along = [0f, 0.06f, 0.14f, 0.24f, 0.34f, 0.44f, 0.54f, 0.64f, 0.74f, 0.84f, 0.92f, 1f];
        for (int i = 0; i < Points; i++) Along[i] = along[i];
        _x = _hx; _y = _hy;
        _heading = (float)(rng.NextDouble() * MathF.Tau) - MathF.PI;
        _zBed = bed.ZAt(_x, _y); _z = _zBed;
        _sc = Light.Scale(_z); _len = _L * _sc;
        _wob0 = (float)(rng.NextDouble() * MathF.Tau); _wob1 = (float)(rng.NextDouble() * MathF.Tau);
        _scareCool.Start(0); _lungeCool.Start(5 + 10 * bold);
        ResetClaws();
        // Each starts in a feed of its own length, so no two change what they are doing together.
        _mind.Begin(Doing.Feed, 3 + 10 * (float)rng.NextDouble());
        _lastWasFeed = true;
        LaySpine();
    }

    // ==================== UPDATE ====================

    /// <param name="threats">Screen points of what scares it (see DeepDwellers.Update).</param>
    /// <param name="all">Every dweller (it keeps a little apart from the others on its walks).</param>
    public void Update(float dt, PointF[] threats, Dweller[] all)
    {
        if (!_alive) { _reborn -= dt; if (_reborn <= 0) Reborn(); else return; }
        _mind.Tick(dt);
        _scareCool.Tick(dt);
        _lungeCool.Tick(dt);
        _walkBan -= dt;
        if (_held > 0) _held -= dt;
        float ox = _x, oy = _y;
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        if (DeepDwellers.MotionLog != null) _nearPx = NearestThreat(threats);
        if (_bout == null && _doing != Doing.Lunge) React(threats);
        if (_bout == null && _doing is Doing.Feed or Doing.Walk or Doing.Settle && _minnows != null && _lungeCool.Ready && _held <= 0) LookForMinnow();

        switch (_doing)
        {
            case Doing.Feed:
                DoFeed(dt);
                break;
            case Doing.Walk:
                DoMove(dt, WalkSpeed, all);
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
            case Doing.Threat:
                if (_mind.Over) Decide();
                break;
            case Doing.Bout:
                if (_bout == null) Decide();                       // the fight is over and nothing else was begun: roll on
                break;
            case Doing.Lunge:
                DoLunge(dt);
                break;
        }

        // ---- legs, feelers, claws, curl and depth ----
        float dx = _x - ox, dy = _y - oy;
        _speedLog = dt > 0 ? MathF.Sqrt(dx * dx + dy * dy) / dt : 0;
        float v = dt > 0 ? (dx * MathF.Cos(_heading) + dy * MathF.Sin(_heading)) / dt : 0;
        _vel += (v - _vel) * (1 - MathF.Exp(-dt * 12));
        float nominal = WalkSpeed;
        _walkK = _doing is Doing.Walk or Doing.Bout or Doing.Lunge ? Math.Min(1f, MathF.Abs(_vel) / nominal) : 0f;
        if (_doing == Doing.Turn || (_doing == Doing.Bout && _walkK < 0.3f)) _walkK = MathF.Max(_walkK, 0.5f);   // the legs shuffle while it swings round or shoves
        float legRate = MathF.Tau * 3.5f * (_vel / nominal);
        if (_doing is Doing.Turn or Doing.Bout) legRate = MathF.Max(MathF.Abs(legRate), MathF.Tau * 2f);
        _gait = (_gait + legRate * dt) % MathF.Tau;
        _wob0 = (_wob0 + dt * (1.1f + 0.6f * _walkK + 2.5f * _antUp)) % MathF.Tau;    // the feelers whip in a display
        _wob1 = (_wob1 + dt * (1.5f + 0.6f * _walkK + 2.5f * _antUp)) % MathF.Tau;
        _pickK += ((_doing == Doing.Feed ? 1f : 0f) - _pickK) * (1 - MathF.Exp(-dt * 6));
        _pickPh = (_pickPh + dt * MathF.Tau * (1.8f + 0.9f * MathF.Sin(_wob0))) % MathF.Tau;
        bool display = _doing is Doing.Settle or Doing.Threat || (_doing == Doing.Bout && _bout != null && _bout.Displaying);
        _antUp += ((display ? 1f : 0f) - _antUp) * (1 - MathF.Exp(-dt * 5));
        float zt = _bed.ZAt(_x, _y);
        _zBed += (zt - _zBed) * (1 - MathF.Exp(-dt / 0.3f));
        if (_zBed > zt) _zBed = zt;                            // never deeper than the bed under it (rising ground lifts it at once; sinking is eased)
        _z = _zBed;
        _curl += ((_flickActive ? 1f : 0f) - _curl) * (1 - MathF.Exp(-dt * 30));
        UpdateClaws(dt);
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

    /// <summary>One line of the instrument log (see the DeepDwellers constructor for the columns). Only called while a log is open.</summary>
    public void Log(long frame, double t, Action<string> write)
    {
        write(string.Create(CultureInfo.InvariantCulture,
            $"{frame},{t:F3},{_idx},{Kind},{_doing},{_x:F2},{_y:F2},{_z:F4},{_heading:F3},{_speedLog:F2},{_curl:F2},{(_logVisible ? 1 : 0)},{Spine[0].X:F1},{Spine[0].Y:F1},{(_bout == null ? -1 : _bout.Other(this)._idx)},{(_bout == null ? "-" : _bout.PhaseName)},{Lunges},{Catches},{Wins},{Losses},{_scares},{_nearPx:F1},{(_alive ? 1 : 0)},{_held:F1}"));
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: one thing on its mind at a time. When it runs out of it,
    // it rolls weighted dice for the next: a feed (the big face) or a short walk.
    // The two dials only change how big each face is. Underneath, the steering
    // refuses any step onto a pixel that is not open, deep water, the scare check
    // can cut in on almost anything, and the referee (DeepDwellers) can hand two
    // of them a fight.

    private void Decide()
    {
        _spd = 0;
        float dh = Dist(_x, _y, _hx, _hy);
        if (dh > _range * 0.85f && _walkBan <= 0 && PickGoal(0, 0, true)) { StartMove(); return; }
        bool canGo = _walkBan <= 0;
        _dice[0] = (1.2f - 0.5f * _busy + 0.3f * _bold) * (_lastWasFeed ? 0.5f : 1f);
        _dice[1] = canGo ? 0.9f + 0.9f * _busy : 0f;
        int face = Dice.Pick(_rng, _dice);
        if (face == 1 && PickGoal(_u * 0.03f, _u * 0.12f, false)) { _lastWasFeed = false; StartMove(); return; }
        float dur = (6 + 19 * (float)_rng.NextDouble()) * (0.85f + 0.3f * _bold - 0.3f * _busy);
        _mind.Begin(Doing.Feed, Math.Clamp(dur, 6f, 25f));
        _feedNext = 1 + 2 * (float)_rng.NextDouble();
        _lastWasFeed = true;
    }

    /// <summary>A spot on the bed to go to, within range of home, reached by a line that is free all the way, and not on top of another dweller. "Home" asks for a spot on the way back.</summary>
    private bool PickGoal(float minD, float maxD, bool home)
    {
        for (int tries = 0; tries < 36; tries++)
        {
            float gx, gy;
            if (home)
            {
                float hd = Dist(_x, _y, _hx, _hy), keep = MathF.Min(hd, _range * 0.4f);
                float ux = hd > 1e-3f ? (_x - _hx) / hd : 0, uy = hd > 1e-3f ? (_y - _hy) / hd : 0;
                float a0 = (float)(_rng.NextDouble() * MathF.Tau), r0 = _u * 0.02f * (float)_rng.NextDouble();
                gx = _hx + ux * keep * 0.6f + MathF.Cos(a0) * r0; gy = _hy + uy * keep * 0.6f + MathF.Sin(a0) * r0;
            }
            else
            {
                float a = (float)(_rng.NextDouble() * MathF.Tau);
                float r = tries < 24 ? minD + (maxD - minD) * (float)_rng.NextDouble() : _u * (0.015f + 0.02f * (float)_rng.NextDouble());
                gx = _x + MathF.Cos(a) * r; gy = _y + MathF.Sin(a) * r;
                if (Dist(gx, gy, _hx, _hy) > _range) continue;
            }
            if (!_bed.Free(gx, gy) || !LineFree(_x, _y, gx, gy)) continue;
            _gx = gx; _gy = gy;
            return true;
        }
        if (home) { _gx = _hx; _gy = _hy; return true; }
        return false;
    }

    /// <summary>Sets off for the goal: a pivot first if it faces well away from it (it does not walk sideways), then the walk, timed by the distance.</summary>
    private void StartMove()
    {
        float err = Wrap(MathF.Atan2(_gy - _y, _gx - _x) - _heading);
        if (MathF.Abs(err) > 0.5f)
        {
            _next = Doing.Walk; _turnTo = MathF.Atan2(_gy - _y, _gx - _x); _turnFast = false;
            _mind.Begin(Doing.Turn, Mind<Doing>.Forever);
            return;
        }
        float d = Dist(_x, _y, _gx, _gy);
        _mind.Begin(Doing.Walk, Math.Clamp(d / WalkSpeed * 1.4f + 1f, 1.5f, 8f));
    }

    /// <summary>
    /// One frame of walking toward the goal: it steers a little as it goes, speeds up and
    /// slows down briskly (the skitter), stops short of another dweller in its way, and a
    /// refused step ends the walk and bans walking for two seconds (rolled again on the
    /// spot it picked the same wall again, frame after frame).
    /// </summary>
    private void DoMove(float dt, float pace, Dweller[] all)
    {
        float dx = _gx - _x, dy = _gy - _y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < _len * 0.2f || _mind.Over) { Decide(); return; }
        float err = Wrap(MathF.Atan2(dy, dx) - _heading);
        if (MathF.Abs(err) > 0.9f) { StartMove(); return; }
        _heading = Wrap(_heading + Math.Clamp(err, -1.5f * dt, 1.5f * dt));
        float target = MathF.Min(pace, d * 1.5f);
        _spd += (target - _spd) * (1 - MathF.Exp(-dt * 4f));
        float step = _spd * dt;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        // Another dweller within half a length ahead of the new middle: stop here and roll again (a bout is the only touch).
        foreach (Dweller o in all)
        {
            if (o == this || !o._alive) continue;
            float ox = o._x - nx, oy = o._y - ny;
            if (ox * ox + oy * oy < _L * _L * 0.6f * 0.6f && ox * MathF.Cos(_heading) + oy * MathF.Sin(_heading) > 0) { _walkBan = 2f; Decide(); return; }
        }
        if (FreeStep(nx, ny, _heading)) { _x = nx; _y = ny; }
        else { _walkBan = 2f; Decide(); }
    }

    private void DoTurn(float dt)
    {
        float err = Wrap(_turnTo - _heading);
        float rate = _turnFast ? 22f : MathF.PI / 2 / 0.3f;
        _heading = Wrap(_heading + Math.Clamp(err, -rate * dt, rate * dt));
        if (MathF.Abs(Wrap(_turnTo - _heading)) < 0.03f || _mind.Over)
        {
            _spd = 0;
            if (_next == Doing.Flick) BeginFlick();
            else if (_next == Doing.Lunge) BeginSnap();
            else StartMove();
        }
    }

    private void DoFeed(float dt)
    {
        // A crayfish brings one claw, then the other, to its mouth every few seconds; a prawn picks with its small legs only.
        for (int c = 0; c < 2; c++) _reachLeft[c] -= dt;
        if (Kind == DeepKind.Crayfish || _held > 0)
        {
            _feedNext -= dt;
            if (_feedNext <= 0)
            {
                _feedSide = _held > 0 ? _lungeSide : _feedSide ^ 1;
                _reachLeft[_feedSide] = 1.2f;
                _feedNext = 2 + 2 * (float)_rng.NextDouble();
            }
        }
        if (_mind.Over) Decide();
    }

    // ---- the scare ----

    /// <summary>
    /// A fish, a turtle, a duck or the diving snake near it, while it feeds, walks or is settled:
    /// within 0.08u (bold: 0.055u) of its middle. A bold one with the threat ahead raises its
    /// claws (Threat) and flicks only if it comes within 0.045u; otherwise it flicks at once
    /// (swinging round first, in 0.15 s, if the threat is behind it, so that backward means
    /// away). A 4 second cooldown, so a koi keeping pace cannot set it off every frame.
    /// </summary>
    private void React(PointF[] threats)
    {
        if (threats.Length == 0 || _inScare) return;
        if (_doing is Doing.Flick or Doing.Settle or Doing.Turn) return;
        PointF mid = Spot;
        float best = float.MaxValue;
        int bi = -1;
        for (int i = 0; i < threats.Length; i++)
        {
            float dx = threats[i].X - mid.X, dy = threats[i].Y - mid.Y, d2 = dx * dx + dy * dy;
            if (d2 < best) { best = d2; bi = i; }
        }
        if (bi < 0) return;
        float dist = MathF.Sqrt(best);
        if (_doing == Doing.Threat)
        {
            if (dist < _u * 0.045f) { _scareCool.Start(4); StartScare(threats[bi]); }
            return;
        }
        if (!_scareCool.Ready || dist > _u * (0.08f - 0.025f * _bold)) return;
        _scareCool.Start(4);
        (float tx, float ty) = ToGround(threats[bi].X, threats[bi].Y - _sinkPx);
        float rel = Wrap(MathF.Atan2(ty - _y, tx - _x) - _heading);
        if (MathF.Abs(rel) < MathF.PI / 3 && dist > _u * 0.045f && _rng.NextDouble() < _bold)
        {
            _mind.Begin(Doing.Threat, 1 + 2 * (float)_rng.NextDouble());
            return;
        }
        StartScare(threats[bi]);
    }

    /// <summary>Begins the flicks away from a threat (screen point): the tail toward the roomiest way back, the threat kept ahead, two to four flicks.</summary>
    private void StartScare(PointF threat)
    {
        _inScare = true; _scares++;
        _spd = 0;
        _held = 0;                                             // a meal in the claw is dropped
        (float tx, float ty) = ToGround(threat.X, threat.Y - _sinkPx);
        float toward = MathF.Atan2(ty - _y, tx - _x), rel = Wrap(toward - _heading);
        float h0 = MathF.Abs(rel) > 1.2f ? toward : _heading;
        _flicksLeft = 2 + _rng.Next(3);
        BeginFlicksToward(h0, 0.12f);
    }

    /// <summary>Aims the tail at the roomiest way back within about 90 degrees of h0 and flicks, or freezes (Settle) when there is no room behind it at all. The flick length in u sizes the room wanted.</summary>
    private void BeginFlicksToward(float h0, float flickU)
    {
        float bestRoom = BackRoom(h0, flickU), bestH = h0;
        if (bestRoom < _u * flickU)
            foreach (float off in (ReadOnlySpan<float>)[0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f, 1.6f, -1.6f])
            {
                float room = BackRoom(h0 + off, flickU);
                if (room > bestRoom + _u * 0.01f) { bestRoom = room; bestH = Wrap(h0 + off); }
            }
        if (bestRoom < _u * 0.02f) { _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble()); return; }
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
        PointF mid = Spot;
        float best = float.MaxValue;
        for (int i = 0; i < threats.Length; i++)
        {
            float dx = threats[i].X - mid.X, dy = threats[i].Y - mid.Y, d2 = dx * dx + dy * dy;
            if (d2 < best) best = d2;
        }
        return best == float.MaxValue ? -1 : MathF.Sqrt(best);
    }

    /// <summary>How far it could flick backward from here along heading h, in ground pixels (up to one flick of flickU), testing every few pixels.</summary>
    private float BackRoom(float h, float flickU)
    {
        float c = MathF.Cos(h), s = MathF.Sin(h), room = 0;
        for (float d = 3; d <= _u * flickU; d += 3)
        {
            if (!FreeStep(_x - c * d, _y - s * d, h)) break;
            if (Dist(_x - c * d, _y - s * d, _hx, _hy) > _range + _u * 0.12f) break;
            room = d;
        }
        return room;
    }

    private float FlickU => Kind == DeepKind.Crayfish ? 0.1f : 0.07f;

    private void BeginFlick()
    {
        _mind.Begin(Doing.Flick, Mind<Doing>.Forever);
        _flickActive = true; _flickStopped = false;
        _flickT = 0; _flickX = _x; _flickY = _y;
    }

    /// <summary>One frame of the escape: a flick slides the middle backward along its own axis (eased, in sub-steps of two pixels); a step onto a refused pixel stops the flick short. Between flicks a pause; after the last, sit still.</summary>
    private void DoFlick(float dt)
    {
        _flickT += dt;
        if (_flickActive)
        {
            float p = Math.Min(1f, _flickT / FlickTime), e = Smooth(p);
            if (!_flickStopped)
            {
                float tx = _flickX - MathF.Cos(_heading) * _u * FlickU * e, ty = _flickY - MathF.Sin(_heading) * _u * FlickU * e;
                float mx = tx - _x, my = ty - _y;
                int n = (int)(MathF.Sqrt(mx * mx + my * my) / 2) + 1;
                for (int k = 0; k < n; k++)
                {
                    float nx = _x + mx / n, ny = _y + my / n;
                    PointF sc = ToScreen(nx, ny);
                    if (!FreeStep(nx, ny, _heading) || Dist(nx, ny, _hx, _hy) > _range + _u * 0.12f || sc.X < _len || sc.X > _pond.Width - _len || sc.Y < _len || sc.Y > _pond.Height - _len)
                    { _flickStopped = true; break; }   // and never off the picture
                    _x = nx; _y = ny;
                }
            }
            if (p >= 1)
            {
                _flickActive = false;
                _flicksLeft--;
                _flickT = 0;
                if (_flicksLeft <= 0) _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble());
            }
        }
        else if (_flickT >= FlickPause)
        {
            if (BackRoom(_heading, FlickU) < _u * 0.025f) _mind.Begin(Doing.Settle, 1 + (float)_rng.NextDouble());
            else BeginFlick();
        }
    }

    /// <summary>After the settle: the scare (or the lost fight) is over; if it was carried well away from home it walks back, else it feeds where it landed.</summary>
    private void AfterScare()
    {
        _inScare = false;
        _spd = 0;
        if (Dist(_x, _y, _hx, _hy) > _range * 0.7f && PickGoal(0, 0, true)) { StartMove(); return; }
        Decide();
    }

    // ---- the fight (the Bout object drives both; these are what it may ask of me) ----

    /// <summary>The referee has picked me for a fight.</summary>
    public void EnterBout(Bout b)
    {
        _bout = b;
        _spd = 0; _held = 0;
        _mind.Begin(Doing.Bout, Mind<Doing>.Forever);
    }

    /// <summary>Which way I would turn to face a ground point (the pivot rate of a bout's facing).</summary>
    public void FaceToward(float gx, float gy, float dt, float rate)
    {
        float err = Wrap(MathF.Atan2(gy - _y, gx - _x) - _heading);
        _heading = Wrap(_heading + Math.Clamp(err, -rate * dt, rate * dt));
    }

    /// <summary>How far off I am from facing a ground point, radians.</summary>
    public float OffFacing(float gx, float gy) => MathF.Abs(Wrap(MathF.Atan2(gy - _y, gx - _x) - _heading));

    /// <summary>A step along my heading of this many ground pixels (negative: backward) if the bed allows it; true if taken.</summary>
    public bool BoutStep(float along)
    {
        float nx = _x + MathF.Cos(_heading) * along, ny = _y + MathF.Sin(_heading) * along;
        if (!FreeStep(nx, ny, _heading)) return false;
        _x = nx; _y = ny;
        return true;
    }

    /// <summary>A step by a ground vector (the grapple moves the locked pair along the line between them, whatever their headings) if the bed allows it; true if taken.</summary>
    public bool BoutStepTo(float dx, float dy)
    {
        float nx = _x + dx, ny = _y + dy;
        if (!FreeStep(nx, ny, _heading)) return false;
        _x = nx; _y = ny;
        return true;
    }

    /// <summary>A small swing of the heading during the grapple (the shove rocks both).</summary>
    public void BoutRock(float rad) => _heading = Wrap(_heading + rad);

    /// <summary>The claws' reach ahead of my middle, ground pixels AS DRAWN (the body is laid out at its perspective size, _len, not its surface size): when two reaches meet, the claws touch on the screen. (With the surface size the first fights grappled 35 px apart.)</summary>
    public float ClawReach => _len * (Kind == DeepKind.Crayfish ? 0.55f : 0.9f);
    /// <summary>My walking pace, ground pixels a second (the fight's closing and shoving are in walks).</summary>
    public float Pace => WalkSpeed;
    /// <summary>How far ahead of my middle the pincer's tip is at the snap (the claw shot out half again its length), ground pixels: where a minnow must be when the claw closes. About 1.85 times the resting reach for either kind (the geometry in ClawPoints). The dash stops short of this, not of the resting reach: aimed at the resting reach, the extended claw overshot the fish by half a body and every snap in twenty stress minutes missed.</summary>
    public float SnapReach => ClawReach * 1.85f;

    /// <summary>The fight is over and I lost: flick away (my heading faces the winner, so backward is away) and settle.</summary>
    public void LoseBout()
    {
        _bout = null; Losses++;
        _inScare = true;
        _flicksLeft = 2 + _rng.Next(2);
        BeginFlicksToward(_heading, FlickU);
    }

    /// <summary>The fight is over and I won: claws up for a moment, then on with my day.</summary>
    public void WinBout()
    {
        _bout = null; Wins++;
        _mind.Begin(Doing.Threat, 1.5f + (float)_rng.NextDouble());
    }

    /// <summary>The fight was cut short (one of us was eaten, or the bed gave no room): nobody wins.</summary>
    public void LeaveBout()
    {
        _bout = null;
        if (_alive) _mind.Begin(Doing.Settle, 0.5f + (float)_rng.NextDouble());
    }

    // ---- the snap at a minnow ----

    /// <summary>
    /// A minnow of a school that has come down to the bed, within two and a half claws' reach
    /// (ground pixels, and no further off my depth than the top of my own body): turn to it
    /// if it is not nearly ahead, dash at it, and snap. The schools are rarely down here and the
    /// search is a few dozen fish, so a look every frame costs nothing worth saving.
    /// (With the look at one reach and a snap from where it stood, ten stress minutes with the
    /// schools on the bed most of the time gave three snaps and no hit: a dweller is slow and a
    /// milling minnow is seldom within a claw of it.)
    /// </summary>
    private void LookForMinnow()
    {
        PointF mid = Spot;
        float within = SnapReach * 2f;
        if (!_minnows!.NearestFishDeep(mid.X, mid.Y, within * 1.3f, _z - 0.12f, out PointF nose)) return;   // (the screen distance is a little more than the ground one: the view's squash)
        (float tx, float ty) = ToGround(nose.X, nose.Y - _sinkPx);
        float d = Dist(_x, _y, tx, ty);
        if (d > within) return;
        _lungeTX = tx; _lungeTY = ty;
        float toward = MathF.Atan2(ty - _y, tx - _x), rel = Wrap(toward - _heading);
        _lungeSide = rel > 0 ? 1 : 0;
        Lunges++;
        _lungeCool.Start(6 + 6 * (1 - _bold));
        if (MathF.Abs(rel) > 0.6f)
        {
            _next = Doing.Lunge; _turnTo = toward; _turnFast = true;
            _mind.Begin(Doing.Turn, 0.3f);
            return;
        }
        BeginSnap();
    }

    private void BeginSnap()
    {
        _lungeT = -0.6f;                                        // the dash first: up to 0.6 s of it, then the snap's own clock from 0
        _lungeAim = Wrap(MathF.Atan2(_lungeTY - _y, _lungeTX - _x) - _heading);
        _mind.Begin(Doing.Lunge, LungeTime + 0.9f);
    }

    /// <summary>
    /// The lunge. First a DASH: up to 0.6 s at three times its walk straight at the minnow (its
    /// nose read afresh each frame while a fish is still within sight), over in a moment once the
    /// fish is within a claw's reach. Then the SNAP: the near claw shoots out toward where the
    /// minnow is (the arm swings to the aim, the pincer opens, the reach grows half again), and
    /// at the snap the pincer closes on whatever living minnow's nose is within three tenths of
    /// a length of its tip (Minnows.Take): a hit is held and eaten over 12 to 20 s, a miss is a
    /// miss, and either way the school bursts away from the claw (Minnows.Scatter: that is the
    /// reaction a snapping claw gets). Then the claw draws back.
    /// </summary>
    private void DoLunge(float dt)
    {
        float before = _lungeT;
        _lungeT += dt;
        if (_lungeT < 0)
        {
            PointF mid = Spot;
            if (_minnows!.NearestFishDeep(mid.X, mid.Y, SnapReach * 3f, _z - 0.12f, out PointF nose))
                (_lungeTX, _lungeTY) = ToGround(nose.X, nose.Y - _sinkPx);
            float d = Dist(_x, _y, _lungeTX, _lungeTY);
            float toward = MathF.Atan2(_lungeTY - _y, _lungeTX - _x);
            _heading = Wrap(_heading + Math.Clamp(Wrap(toward - _heading), -6f * dt, 6f * dt));
            if (d <= SnapReach * 0.95f) _lungeT = 0;                                  // where the shot-out claw will close: snap now
            else
            {
                float step = MathF.Min(d - SnapReach * 0.85f, WalkSpeed * 3f * dt);
                float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
                if (FreeStep(nx, ny, _heading)) { _x = nx; _y = ny; } else { Decide(); return; }   // a wall between it and the fish: the lunge is off (a snap from here was a sure miss, and a claw snapping at nothing reads as a twitch)
            }
            _lungeAim = Wrap(MathF.Atan2(_lungeTY - _y, _lungeTX - _x) - _heading);
            _lungeSide = _lungeAim > 0 ? 1 : 0;
            return;
        }
        if (before < LungeSnapAt && _lungeT >= LungeSnapAt)
        {
            ClawPoints(_lungeSide == 1 ? 1 : -1, out _, out _, out _, out float tipX, out float tipY);
            PointF tip = D(tipX, tipY);
            bool hit = _minnows!.Take(tip.X, tip.Y, _len * 0.45f, _z - 0.12f, 2f);   // (a minnow is about two thirds of a dweller's length: a pincer closing anywhere on its front half is a hold; at three tenths, six snaps in ten stress minutes held nothing. The depth window's TOP must be given: Take's default top is the snake's, 0.3, and a snap with a minnow 4 px from the tip at 0.74 took nothing, four builds running)
            if (hit) { Catches++; _held = 12 + 8 * (float)_rng.NextDouble(); }
            _minnows.Scatter(tip.X, tip.Y, _u * 0.15f);
            Snatch.Log?.Invoke(FormattableString.Invariant($"{(hit ? "clawhit" : "clawmiss")},{Kind}{_idx},{_z:F3},{tip.X:F0},{tip.Y:F0}"));
        }
        if (_lungeT >= LungeTime)
        {
            if (_held > 0) { _mind.Begin(Doing.Feed, _held); _feedNext = 0.3f; _lastWasFeed = true; }
            else Decide();
        }
    }

    // ==================== THE BED ====================

    /// <summary>Is a step to this middle position allowed? Its middle and both ends must be free (the prawn's rule, applied to the whole shell).</summary>
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

    // Half the body's width along its length, as a fraction of the length, per kind: a pointed rostrum, the carapace
    // widest about a third of the way back, the abdomen narrowing, the tail fan flaring. The crayfish is the stouter.
    private static readonly float[] CrayS = [0f, 0.05f, 0.18f, 0.32f, 0.48f, 0.5f, 0.86f, 0.9f, 0.96f, 1f];
    private static readonly float[] CrayW = [0.012f, 0.04f, 0.085f, 0.1f, 0.092f, 0.085f, 0.068f, 0.075f, 0.095f, 0.07f];
    private static readonly float[] PrawnS = [0f, 0.07f, 0.18f, 0.30f, 0.42f, 0.54f, 0.66f, 0.78f, 0.89f, 1f];
    private static readonly float[] PrawnW = [0.006f, 0.030f, 0.060f, 0.068f, 0.060f, 0.054f, 0.050f, 0.046f, 0.064f, 0.062f];

    private float Profile(float s)
    {
        float[] ps = Kind == DeepKind.Crayfish ? CrayS : PrawnS, pw = Kind == DeepKind.Crayfish ? CrayW : PrawnW;
        for (int i = 1; i < ps.Length; i++)
            if (s <= ps[i]) return pw[i - 1] + (pw[i] - pw[i - 1]) * (s - ps[i - 1]) / (ps[i] - ps[i - 1]);
        return pw[^1];
    }

    /// <summary>Where a body point at fraction s of the length is along the axis, with the curl: the abdomen tucks under, which from above is a SHORTENING of everything behind the carapace.</summary>
    private float PosOf(float s) => s < CurlBreak ? s : CurlBreak + (s - CurlBreak) * (1 - 0.55f * _curl);

    /// <summary>A ground point on the body at fraction s of its length (rostrum 0), out to one side by "side" ground pixels.</summary>
    private (float, float) G(float s, float side)
    {
        float c = MathF.Cos(_heading), sn = MathF.Sin(_heading), pos = PosOf(s);
        return (_x + c * _len * (0.5f - pos) - sn * side, _y + sn * _len * (0.5f - pos) + c * side);
    }

    /// <summary>A straight spine back from the rostrum, centred on the middle so a pivot turns it about its middle. In a flick the abdomen curls under. Drawn _sc smaller for its depth, sunk down the screen by it, the ground squashed by the view.</summary>
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
    private float _gutV = 0.14f;

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        r = g = b = 0; alpha = 1;
        int lobes = Kind == DeepKind.Crayfish ? 5 : 3;
        if (s > 0.9f && s > 0.93f + 0.07f * (0.5f + 0.5f * MathF.Cos(lobes * MathF.PI * v))) return false;   // the tail fan's scalloped end
        if (_shadowPass) return true;
        float av = MathF.Abs(v);
        float cr, cg, cb, a;
        if (Kind == DeepKind.Crayfish)
        {
            a = 1f;
            if (s < 0.5f)
            {
                // The carapace: brown-olive (a signal crayfish is brown, not the red of the shallows' zarigani), a darker ridge
                // down the middle, a groove across at the neck, a few pale speckles (a cheap hash of the cell, two in thirty-two).
                cr = 108; cg = 78; cb = 46;
                float ridge = Math.Clamp((0.16f - av) * 6, 0, 1);
                cr += (70 - cr) * 0.6f * ridge; cg += (54 - cg) * 0.6f * ridge; cb += (34 - cb) * 0.6f * ridge;
                float groove = Math.Clamp(1 - MathF.Abs(s - 0.33f) * 70, 0, 1) * 0.5f;
                cr -= 40 * groove; cg -= 26 * groove; cb -= 16 * groove;
                if ((((int)(s * 120f) * 73856093) ^ ((int)((v + 1f) * 9f) * 19349663)) % 16 == 0) { cr += 50; cg += 44; cb += 36; }
            }
            else if (s < 0.9f)
            {
                // The abdomen: six plates, a dark seam at each joint, the plate's edges a touch paler.
                float u6 = (s - 0.5f) / 0.4f * 6f, f = u6 - MathF.Floor(u6);
                cr = 100; cg = 72; cb = 42;
                float seam = Math.Clamp(1 - MathF.Abs(f - 0.05f) * 9, 0, 1);
                cr -= 44 * seam; cg -= 30 * seam; cb -= 18 * seam;
                float edge = Math.Clamp((av - 0.7f) * 3, 0, 1);
                cr += 30 * edge; cg += 26 * edge; cb += 18 * edge;
            }
            else
            {
                // The tail fan: lobes with dark lines between, paler toward the edge.
                float line = Math.Clamp(1 - MathF.Abs(MathF.Sin(2.5f * MathF.PI * v)) * 7, 0, 1) * 0.5f;
                cr = 112; cg = 80; cb = 48;
                float pale = Math.Clamp((s - 0.93f) * 12, 0, 1) * 0.6f + Math.Clamp((av - 0.6f) * 2, 0, 1) * 0.4f;
                cr += 40 * pale; cg += 44 * pale; cb += 36 * pale;
                cr -= 40 * line; cg -= 26 * line; cb -= 16 * line;
            }
        }
        else
        {
            // The long-armed prawn: see-through olive-brown glass with dark freckles, a gut thread, plated abdomen.
            cr = 134; cg = 142; cb = 110;
            if (s < CurlBreak) a = 0.7f;
            else if (s < 0.88f)
            {
                a = 0.6f;
                float u6 = (s - CurlBreak) / 0.46f * 6f, f = u6 - MathF.Floor(u6);
                float joint = Math.Clamp(1 - f * 10, 0, 1);
                cr += 22 * joint; cg += 20 * joint; cb += 18 * joint;
            }
            else
            {
                float edge = Math.Clamp((av - 0.45f) * 1.6f + (s - 0.93f) * 5f, 0, 1);
                cr = 160; cg = 170; cb = 140;
                a = 0.5f - 0.24f * edge;
            }
            // The freckles: dark flecks over the carapace and abdomen (one cell in nine), what tells a tenagaebi from glass.
            if (s < 0.88f && (((int)(s * 90f) * 73856093) ^ ((int)((v + 1f) * 7f) * 19349663)) % 9 == 0) { cr -= 60; cg -= 62; cb -= 50; a += 0.2f; }
            if (s > 0.12f && s < 0.9f)
            {
                float k = Math.Clamp((_gutV - av) / (_gutV * 0.35f), 0, 1);
                cr += (56 - cr) * k; cg += (62 - cg) * k; cb += (48 - cb) * k;
                a += (0.78f - a) * k;
            }
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
            // The preview size: a dweller is a few pixels long. One soft dash of its colour and nothing else.
            if (Kind == DeepKind.Crayfish) Stroke(fb, Spine[0], Spine[Points - 1], 110, 80, 48, 0.8f, 1.5f, murk);
            else Stroke(fb, Spine[0], Spine[Points - 1], 150, 160, 128, 0.6f, 1.2f, murk);
            return;
        }
        float soft = MathF.Max(0.8f, _u * 0.0007f);
        _gutV = Math.Clamp(0.6f / MathF.Max(1f, _len * 0.05f), 0.14f, 0.4f);
        // The shadow on the bed: its own shape, a hair down and to the right (within 0.006u on the bed), faint and soft.
        float reach = _u * 0.006f;
        _shadowPass = true;
        PaintShadow(fb, _view, reach * 0.6f, reach * 0.8f, 0.2f * (1 - murk), soft * 2f);
        _shadowPass = false;
        DrawLegs(fb, murk);
        DrawFeelers(fb, murk);
        Paint(fb, _view, _pond.Sun, murk, soft, _pond.Pixels);
        DrawClaws(fb, murk);
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
    /// The walking legs under the carapace (four pairs on a crayfish, whose first pair are the
    /// claws; four small pairs behind the arms on a prawn), each swinging fore and aft on a
    /// wave down the body, the two sides half a step apart. Still when it is still. In a feed the
    /// front pair reaches in and picks; in a flick they fold in.
    /// </summary>
    private void DrawLegs(FrameBuffer fb, float murk)
    {
        bool cray = Kind == DeepKind.Crayfish;
        float ll = _len * (cray ? 0.13f : 0.09f);
        int r = cray ? 104 : 118, g = cray ? 74 : 128, b = cray ? 44 : 100;
        float alpha = cray ? 1f : 0.5f;
        for (int k = 0; k < 4; k++)
        {
            float along = (cray ? 0.17f : 0.2f) + (cray ? 0.09f : 0.06f) * k;
            float hw = _len * Profile(along);
            for (int side = -1; side <= 1; side += 2)
            {
                float swing = 0.45f * _walkK * MathF.Sin(_gait + 0.9f * k + (side > 0 ? 0 : MathF.PI));
                float basic = MathF.PI / 2 - 0.55f + 0.34f * k;
                float ang = basic + swing + 0.9f * _curl;
                if (k == 0 && _pickK > 0.01f && !cray)
                {
                    float pick = 0.5f + 0.5f * MathF.Sin(_pickPh + (side > 0 ? 0 : 2.1f));
                    ang += (0.35f - 0.2f * pick - basic * 0.55f) * _pickK;
                }
                (float ax, float ay) = G(along, side * hw * 0.8f);
                float a = _heading + side * ang;
                OvalG(fb, ax + MathF.Cos(a) * ll, ay + MathF.Sin(a) * ll, a, ll, _len * (cray ? 0.012f : 0.007f), r, g, b, alpha, murk);
            }
        }
    }

    /// <summary>Two long feelers swept back past the tail (half again the body), each of three pieces whose tips wave slowly, whipping in a display; two short ones ahead of the rostrum.</summary>
    private void DrawFeelers(FrameBuffer fb, float murk)
    {
        bool cray = Kind == DeepKind.Crayfish;
        float th = MathF.Max(1f, _len * 0.012f);
        int r = cray ? 100 : 110, g = cray ? 70 : 120, b = cray ? 44 : 96;
        for (int side = -1; side <= 1; side += 2)
        {
            float ph = side > 0 ? _wob0 : _wob1;
            (float px, float py) = G(0.05f, side * _len * 0.025f);
            PointF prev = D(px, py);
            float spread = 0.3f + 0.4f * _antUp;
            for (int j = 0; j < 3; j++)
            {
                float a = _heading + MathF.PI - side * (spread - 0.06f * j + (0.25f + 0.2f * _antUp) * MathF.Sin(ph + 1.3f * j));
                px += MathF.Cos(a) * _len * 0.5f; py += MathF.Sin(a) * _len * 0.5f;
                PointF next = D(px, py);
                Stroke(fb, prev, next, r, g, b, (cray ? 0.85f : 0.5f) - 0.12f * j, th, murk);
                prev = next;
            }
            (float sx, float sy) = G(0f, 0f);
            float aa = _heading + side * (0.3f + 0.15f * MathF.Sin(ph * 1.5f));
            Stroke(fb, D(sx, sy), D(sx + MathF.Cos(aa) * _len * 0.2f, sy + MathF.Sin(aa) * _len * 0.2f), r + 10, g + 10, b + 8, cray ? 0.8f : 0.5f, th, murk);
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
            if (_len > 14f) Stroke(fb, D(bx, by), D(ex, ey), 80, 70, 56, 0.6f, MathF.Max(1f, _len * 0.012f), murk);
            OvalG(fb, ex, ey, 0, er, er, 18, 14, 14, 0.95f, murk);
        }
    }

    private void ResetClaws()
    {
        for (int c = 0; c < 2; c++) { _cArm[c] = 0.38f; _cPin[c] = -0.30f; _cOpen[c] = 0.22f; _cExt[c] = 1f; _reachLeft[c] = 0; _reachSm[c] = 0; }
    }

    /// <summary>
    /// Eases each claw toward the pose its doing wants: held ahead and a touch open, raised and
    /// wide (a threat, a settle, a display in a fight), locked ahead and closed (the grapple),
    /// folded (a flick), one reaching to the mouth (a crayfish's feed, or either kind eating a
    /// minnow), or shot out at the aim and snapped shut (a lunge).
    /// </summary>
    private void UpdateClaws(float dt)
    {
        float k = 1 - MathF.Exp(-dt * 9);
        bool grapple = _doing == Doing.Bout && _bout != null && _bout.Grappling;
        bool display = _doing is Doing.Threat or Doing.Settle || (_doing == Doing.Bout && _bout != null && !_bout.Grappling);
        for (int c = 0; c < 2; c++)
        {
            float a = 0.38f, p = -0.30f, o = 0.22f + 0.07f * MathF.Sin(_wob0 + c * 2), e = 1f;
            if (display) { a = 0.95f; p = -0.55f; o = 1.0f; e = 1.08f; }
            else if (grapple) { a = 0.3f; p = -0.25f; o = 0.05f; e = 1.1f; }
            else if (_doing == Doing.Flick) { a = 0.2f; p = -0.15f; o = 0.04f; }
            if (_doing == Doing.Lunge && c == _lungeSide && _lungeT >= 0)
            {
                // The snap: the arm swings to the aim (as an angle off the heading, signed for the side), the pincer
                // opens wide on the way out and shuts at the snap, the reach grows by half.
                float side = c == 1 ? 1 : -1;
                float out1 = Math.Clamp(_lungeT / LungeSnapAt, 0, 1), back = Math.Clamp((_lungeT - LungeSnapAt) / (LungeTime - LungeSnapAt), 0, 1);
                a = Math.Clamp(_lungeAim * side, -0.3f, 1.2f) * out1 + 0.38f * (1 - out1) * (1 - back);
                p = a; e = 1f + 0.5f * out1 * (1 - back); o = out1 < 1 ? 0.9f : 0.03f;   // (the palm along the arm: the whole claw points at the aim; bent at the wrist, the tip missed the line to the fish)
                k = 1 - MathF.Exp(-dt * 30);                            // a snap is quick
            }
            _reachSm[c] += ((_doing == Doing.Feed && _reachLeft[c] > 0 ? 1f : 0f) - _reachSm[c]) * k;
            float rch = _reachSm[c];
            a += (0.55f - a) * rch; p += (-1.25f - p) * rch; o += (0.25f - o) * rch; e += (0.85f - e) * rch;
            _cArm[c] += (a - _cArm[c]) * k; _cPin[c] += (p - _cPin[c]) * k; _cOpen[c] += (o - _cOpen[c]) * k; _cExt[c] += (e - _cExt[c]) * k;
        }
    }

    /// <summary>
    /// The joints of one claw (side -1 left, +1 right), ground pixels: the shoulder on the
    /// body, the elbow at the end of the upper arm, the base of the palm (a prawn's second long
    /// segment ends here) and the pincer's tip. One geometry for the drawing and the snap.
    /// </summary>
    private void ClawPoints(int side, out (float, float) shoulder, out (float, float) elbow, out (float, float) palm, out float tipX, out float tipY)
    {
        int cl = side > 0 ? 1 : 0;
        bool cray = Kind == DeepKind.Crayfish;
        float ext = _cExt[cl];
        float hw = _len * Profile(cray ? 0.14f : 0.3f);
        shoulder = G(cray ? 0.14f : 0.3f, side * hw * 0.9f);
        float aa = _heading + side * _cArm[cl], armLen = _len * (cray ? 0.22f : 0.5f) * ext;
        elbow = (shoulder.Item1 + MathF.Cos(aa) * armLen, shoulder.Item2 + MathF.Sin(aa) * armLen);
        float ap = _heading + side * _cPin[cl], palmLen = _len * (cray ? 0.11f : 0.4f) * ext;
        palm = (elbow.Item1 + MathF.Cos(ap) * palmLen, elbow.Item2 + MathF.Sin(ap) * palmLen);
        float fl = _len * (cray ? 0.13f : 0.07f) * ext;
        tipX = palm.Item1 + MathF.Cos(ap) * fl; tipY = palm.Item2 + MathF.Sin(ap) * fl;
    }

    /// <summary>
    /// The claws. A crayfish's: a stout arm, a broad palm with the signal crayfish's chalk-white
    /// patch at the hinge, two fingers that open and close. A prawn's: two long thin segments
    /// (longer than the body, bluish grey) ending in a small pincer. Either may hold a caught
    /// minnow (a silver sliver across the fingers, eaten away over the feed).
    /// </summary>
    private void DrawClaws(FrameBuffer fb, float murk)
    {
        bool cray = Kind == DeepKind.Crayfish;
        for (int side = -1; side <= 1; side += 2)
        {
            int cl = side > 0 ? 1 : 0;
            ClawPoints(side, out var sh, out var el, out var pm, out float tx, out float ty);
            float aa = MathF.Atan2(el.Item2 - sh.Item2, el.Item1 - sh.Item1), ap = MathF.Atan2(pm.Item2 - el.Item2, pm.Item1 - el.Item1);
            float armLen = Dist(sh.Item1, sh.Item2, el.Item1, el.Item2), palmLen = Dist(el.Item1, el.Item2, pm.Item1, pm.Item2);
            if (cray)
            {
                OvalG(fb, (sh.Item1 + el.Item1) / 2, (sh.Item2 + el.Item2) / 2, aa, armLen * 0.5f, _len * 0.04f, 104, 74, 44, 1f, murk);
                float pcx = (el.Item1 + pm.Item1) / 2, pcy = (el.Item2 + pm.Item2) / 2;
                OvalG(fb, pcx, pcy, ap, palmLen * 0.5f, _len * 0.058f, 112, 78, 48, 1f, murk);
                OvalG(fb, el.Item1 + MathF.Cos(ap) * _len * 0.02f, el.Item2 + MathF.Sin(ap) * _len * 0.02f, ap, _len * 0.02f, _len * 0.016f, 214, 222, 210, 0.85f, murk);   // the white patch at the hinge
                float fl = _len * 0.13f * _cExt[cl];
                for (int j = -1; j <= 1; j += 2)
                {
                    float af = ap + j * _cOpen[cl] * 0.5f;
                    OvalG(fb, pm.Item1 + MathF.Cos(af) * fl * 0.5f, pm.Item2 + MathF.Sin(af) * fl * 0.5f, af, fl * 0.5f, _len * 0.022f, 118, 70, 46, 1f, murk);
                }
            }
            else
            {
                float th = MathF.Max(1f, _len * 0.014f);
                Stroke(fb, D(sh.Item1, sh.Item2), D(el.Item1, el.Item2), 98, 110, 112, 0.7f, th, murk);
                Stroke(fb, D(el.Item1, el.Item2), D(pm.Item1, pm.Item2), 92, 104, 108, 0.7f, th, murk);
                float fl = _len * 0.07f * _cExt[cl];
                for (int j = -1; j <= 1; j += 2)
                {
                    float af = ap + j * _cOpen[cl] * 0.5f;
                    Stroke(fb, D(pm.Item1, pm.Item2), D(pm.Item1 + MathF.Cos(af) * fl, pm.Item2 + MathF.Sin(af) * fl), 70, 76, 72, 0.85f, th, murk);
                }
            }
            // A caught minnow in this claw: a silver sliver across the fingers, shorter as it is eaten.
            if (_held > 0 && cl == _lungeSide)
            {
                float left = Math.Clamp(_held / 14f, 0.15f, 1f);
                OvalG(fb, tx, ty, ap + MathF.PI / 2, _len * 0.1f * left, _len * 0.018f, 190, 198, 190, 0.8f, murk);
            }
        }
    }

    private PointF D(float gx, float gy)
    {
        PointF p = ToScreen(gx, gy);
        return new PointF(p.X, p.Y + _sinkPx);
    }

    /// <summary>A thin line, darkened and greyed by its depth and hazed toward the water under its start (so a feeler at depth fades as the body does).</summary>
    private void Stroke(FrameBuffer fb, PointF a, PointF b, int r, int g, int bl, float alpha, float thick, float murk)
    {
        int ix = Math.Clamp((int)a.X, 0, _pond.Width - 1), iy = Math.Clamp((int)a.Y, 0, _pond.Height - 1);
        Deepen(ref r, ref g, ref bl, (int)(murk * 256));
        uint wc = _pond.Pixels[iy * _pond.Width + ix];
        r += (int)((((wc >> 16) & 0xFF) - r) * murk); g += (int)((((wc >> 8) & 0xFF) - g) * murk); bl += (int)(((wc & 0xFF) - bl) * murk);
        fb.Line(a, b, Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(bl, 0, 255)), alpha, thick, _view);
    }
}

/// <summary>
/// A fight between two dwellers, played out by one object that moves both, so the pair
/// stays in step (two minds each guessing what the other does next would drift apart).
///
/// FEYNMAN VERSION: watch two crayfish meet in a tank. They turn to face each other
/// (FACE). Both lift their claws high and spread them wide, the feelers whipping, and
/// edge forward a little (DISPLAY: the "meral spread", which settles most fights
/// before they start; here it never does, the referee only hands out fights that go
/// on). They close until the claws touch (CLOSE), lock claws and shove, rocking to
/// and fro, the stronger gaining ground (GRAPPLE), and then the loser lets go and
/// flips backward away while the winner stands tall for a moment (BREAK). The
/// winner was rolled when the fight was handed out (size, kind, boldness, a chance
/// in five for the weaker); the shoving is the picture of that roll.
/// </summary>
internal sealed class Bout
{
    private enum Phase { Face, Display, Close, Grapple, Break }
    private Dweller? _a, _b, _winner;
    private Phase _phase;
    private float _t, _total, _pushPh, _stuck;
    public bool Active { get; private set; }
    /// <summary>Claws up and spread, feelers whipping: the Face and Display phases.</summary>
    public bool Displaying => Active && _phase is Phase.Face or Phase.Display;
    /// <summary>Claws locked: the Grapple.</summary>
    public bool Grappling => Active && _phase == Phase.Grapple;
    public string PhaseName => Active ? _phase.ToString() : "-";
    public Dweller Other(Dweller me) => me == _a ? _b! : _a!;

    public void Start(Dweller a, Dweller b, Dweller winner, Random rng)
    {
        _a = a; _b = b; _winner = winner;
        _phase = Phase.Face; _t = 0; _total = 2.5f; _pushPh = 0; _stuck = 0;
        Active = true;
        a.EnterBout(this); b.EnterBout(this);
    }

    /// <summary>One of the pair was eaten mid-fight: the fight ends, the other stands down.</summary>
    public void Forfeit(Dweller gone)
    {
        if (!Active) return;
        Dweller other = Other(gone);
        Active = false;
        other.LeaveBout();
        gone.LeaveBout();
        _a = _b = _winner = null;
    }

    public void Update(float dt, Random rng)
    {
        if (!Active) return;
        Dweller a = _a!, b = _b!;
        _t += dt;
        float gap = MathF.Sqrt((a.GX - b.GX) * (a.GX - b.GX) + (a.GY - b.GY) * (a.GY - b.GY));
        float touch = a.ClawReach + b.ClawReach;
        switch (_phase)
        {
            case Phase.Face:
                // Both face each other BEFORE anything else: a display begun with one still side-on had the pair
                // walking into each other in the grapple (middles 6 px apart on one natural run). Not facing in time
                // (a wall, a pivot refused): no fight.
                a.FaceToward(b.GX, b.GY, dt, 5f); b.FaceToward(a.GX, a.GY, dt, 5f);
                if (a.OffFacing(b.GX, b.GY) < 0.15f && b.OffFacing(a.GX, a.GY) < 0.15f)
                { _phase = Phase.Display; _t = 0; _total = 1.2f + 1.3f * (float)rng.NextDouble(); }
                else if (_t > _total) End();
                break;
            case Phase.Display:
                // Claws up, a slow edge forward (a sixth of a walk) while there is still a gap.
                a.FaceToward(b.GX, b.GY, dt, 3f); b.FaceToward(a.GX, a.GY, dt, 3f);
                if (gap > touch * 1.1f) { a.BoutStep(0.2f * a.Pace * dt); b.BoutStep(0.2f * b.Pace * dt); }   // (a fifth of a walk's pace)
                if (_t > _total) { _phase = Phase.Close; _t = 0; }
                break;
            case Phase.Close:
                {
                    a.FaceToward(b.GX, b.GY, dt, 3f); b.FaceToward(a.GX, a.GY, dt, 3f);
                    bool moved = false;
                    if (gap > touch) { moved |= a.BoutStep(a.Pace * dt); moved |= b.BoutStep(b.Pace * dt); }   // (a walk's pace, about 0.02u a second for a crayfish)
                    gap = MathF.Sqrt((a.GX - b.GX) * (a.GX - b.GX) + (a.GY - b.GY) * (a.GY - b.GY));
                    if (gap <= touch) { _phase = Phase.Grapple; _t = 0; _total = 2f + 3f * (float)rng.NextDouble(); }
                    else if (!moved || _t > 5f) { End(); }                               // no room to close, or they never met: it comes to nothing
                    break;
                }
            case Phase.Grapple:
                {
                    // Locked claws: both rock to and fro on the line between them at 1.4 Hz, and the winner gains
                    // ground, about a third of a walk's pace, the loser giving it. A step the bed refuses ends it.
                    _pushPh += dt * MathF.Tau * 1.4f;
                    float rock = MathF.Cos(_pushPh) * 3.1f * dt * a.ClawReach;        // the to-and-fro, as a step per frame (its sum over a cycle is zero; the swing is about a third of the claws' reach, a few pixels)
                    Dweller win = _winner!, lose = Other(win);
                    float gain = 0.3f * win.Pace * dt;                                  // the ground gained a second: a third of a walk
                    // The locked pair moves as ONE along the line between them (winner toward loser), by the same vector,
                    // so the gap never changes: the loser gives way first and only then does the winner follow (a step
                    // the bed refuses to either ends the grapple). Along their own headings, a pair not quite face to
                    // face converged (middles 6 px apart on one natural run).
                    float lx = lose.GX - win.GX, ly = lose.GY - win.GY, ll = MathF.Max(1e-3f, MathF.Sqrt(lx * lx + ly * ly));
                    float mx = lx / ll * (rock + gain), my = ly / ll * (rock + gain);
                    bool ok = lose.BoutStepTo(mx, my) && win.BoutStepTo(mx, my);
                    win.FaceToward(lose.GX, lose.GY, dt, 3f); lose.FaceToward(win.GX, win.GY, dt, 3f);
                    win.BoutRock(0.1f * MathF.Sin(_pushPh) * dt * 6f); lose.BoutRock(-0.1f * MathF.Sin(_pushPh) * dt * 6f);
                    // A refused step (the pair against the deep's rim) does not end the grapple at once: the claws stay
                    // locked and the pair rocks in place, and only six tenths of a second of refusals breaks it (ended at the
                    // first refusal, five of seven grapples on one long run lasted 0.0 s: display, close, and the loser gone).
                    _stuck = ok ? 0 : _stuck + dt;
                    if (_stuck > 0.6f || _t > _total) { _phase = Phase.Break; _t = 0; }
                    break;
                }
            case Phase.Break:
                {
                    Dweller win = _winner!, lose = Other(win);
                    Active = false;
                    lose.LoseBout(); win.WinBout();
                    _a = _b = _winner = null;
                    break;
                }
        }
    }

    private void End()
    {
        Dweller a = _a!, b = _b!;
        Active = false;
        a.LeaveBout(); b.LeaveBout();
        _a = _b = _winner = null;
    }
}
