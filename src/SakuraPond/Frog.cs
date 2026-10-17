using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>What a frog's perch is made of.</summary>
internal enum PerchKind { Pad, Rock, Bank }

/// <summary>
/// A place a frog can sit: a lily pad, a dry bank rock (on its water side) or a
/// spot on the bank's edge. Every perch is worked out once, when the scene is
/// made, and is held by at most one frog at a time (the frog that sits on it or
/// is swimming to it). The log and the island belong to the turtles and the
/// snake (Pond.LogPerch, Pond.IslandPerch), so a frog never uses them.
/// </summary>
internal sealed class FrogPerch
{
    public PerchKind Kind;
    public PointF Centre;        // screen pixels: the pad's or rock's middle, or the spot on the bank where a frog sits
    public float Radius;         // pixels (a bank spot: 0.04u, about the room a frog needs)
    public PointF Out;           // a unit vector on the screen, from the perch toward open water
    public int PadIndex = -1;    // which of Pond.Pads (a pad perch)
    public int Index;            // its place in the list of perches (for logs)
    public Frog? Holder;         // the frog sitting here or on its way here
    public bool Unsitable;       // found to have no bare leaf to sit on (a pad covered by a flower): never chosen to start on
}

/// <summary>
/// The pond's frogs. The scene holds one of these, not the animals themselves
/// (the same shape as Turtles and Lizards). It knows the perches, and it is
/// the one place that needs to see every frog at once: two swimming frogs must
/// not swim through each other, and the scene wants the swimmers as a list.
/// </summary>
internal sealed class Frogs
{
    private readonly Frog[] _all;
    private readonly List<FrogPerch> _perches = new();
    private readonly List<Body> _inWater = new();     // the frogs in the water this frame, refilled each Update and never reallocated

    /// <param name="where">Open water that no branch hides: where a frog under the surface may show.</param>
    public Frogs(Pond pond, int count, Ripples ripples, bool[] where, Random rng, Dragonflies? dragonflies)
    {
        int n = Math.Max(0, count);
        _all = new Frog[n];
        if (n == 0) return;
        BuildPerches(pond, rng);
        // Where a frog's shadow may fall when it sits up on something: everything that is not water
        // (a pad, a rock, the bank), and not under a branch. One sheet shared by all the frogs.
        bool[] shadowMask = new bool[pond.Water.Length];
        for (int i = 0; i < shadowMask.Length; i++) shadowMask[i] = pond.Open[i] && !pond.Water[i];
        // Two dials each, dealt (Traits.Deal) so no two frogs are near-twins: one bold, one restless.
        float[] bold = Traits.Deal(rng, n), restless = Traits.Deal(rng, n);
        for (int i = 0; i < n; i++) _all[i] = new Frog(this, pond, ripples, where, shadowMask, rng, bold[i], restless[i], dragonflies);
        foreach (Frog f in _all) f.Seat();
    }

    public IReadOnlyList<Frog> All => _all;
    public IReadOnlyList<FrogPerch> Perches => _perches;

    /// <summary>The frogs in the water this frame (swimming, floating or dived): solid to the koi.</summary>
    public IReadOnlyList<Body> InWater => _inWater;

    /// <summary>Adds the frogs in the water to the scene's list of what is solid at the surface, without allocating.</summary>
    public void AddSwimmers(List<Body> into)
    {
        for (int i = 0; i < _inWater.Count; i++) into.Add(_inWater[i]);
    }

    /// <param name="snakeHead">The snake's head if it is out (a frog flees it), else null.</param>
    /// <param name="koiHeads">Points on the koi near the surface (KoiSchool.Heads).</param>
    /// <param name="turtleSpots">A point at each turtle's nose and shell (Turtles.Spots).</param>
    /// <param name="lizardNoses">The nose of each lizard that is out (Lizards.Noses).</param>
    public void Update(float dt, PointF? snakeHead, PointF[] koiHeads, PointF[] turtleSpots, PointF[] lizardNoses)
    {
        foreach (Frog f in _all) f.Update(dt, snakeHead, koiHeads, turtleSpots, lizardNoses);
        // Two frogs in the water must not overlap: if their middles are closer than 0.8 of a body
        // length each is eased apart (half the way each, no faster than a body length a second).
        for (int i = 0; i < _all.Length; i++)
            for (int j = i + 1; j < _all.Length; j++)
                if (_all[i].InWater && _all[j].InWater) Frog.KeepApart(_all[i], _all[j], dt);
        _inWater.Clear();
        foreach (Frog f in _all) if (f.InWater) _inWater.Add(f);
    }

    /// <summary>A swallow's shadow swept over the pond here: every frog near it sitting leaps in, one swimming dives.</summary>
    public void Startle(float x, float y)
    {
        foreach (Frog f in _all) f.Startle(x, y);
    }

    /// <summary>Every frog swimming at the surface (or dived but not yet lost to sight) shoves the floating petals in its way aside; a dived frog passes under them.</summary>
    public void PushPetals(Petals petals)
    {
        foreach (Frog f in _all) if (f.InWater && Light.DepthOf(f.Z) < Koi.PushDepth) petals.PushAside(f.Drawn, f.DrawnHalfWidth);
    }

    /// <summary>Paints the frogs that are under the surface (a dive): after the koi, before the minnows.</summary>
    public void DrawUnder(FrameBuffer fb)
    {
        foreach (Frog f in _all) if (f.UnderWater) f.Draw(fb);
    }

    /// <summary>Paints the frogs at the surface, in the air or up on a perch: over the ripples and the floating petals, under the flyers.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (Frog f in _all) if (!f.UnderWater) f.Draw(fb);
    }

    // ---------------------------------------------------------------- perches

    /// <summary>A frog sits at most one to a perch.</summary>
    public bool Take(FrogPerch p, Frog who)
    {
        if (p.Holder != null && p.Holder != who) return false;
        p.Holder = who;
        return true;
    }

    public void Free(FrogPerch? p, Frog who)
    {
        if (p != null && p.Holder == who) p.Holder = null;
    }

    /// <summary>Is another frog (not "me") closer than "within" pixels to this screen point?</summary>
    public bool OtherFrogNear(Frog me, PointF p, float within)
    {
        foreach (Frog f in _all)
        {
            if (f == me) continue;
            PointF c = f.Centre;
            if ((c.X - p.X) * (c.X - p.X) + (c.Y - p.Y) * (c.Y - p.Y) < within * within) return true;
        }
        return false;
    }

    /// <summary>
    /// Works out every place a frog could sit, once. Pads and rocks are found
    /// from the painter's lists; bank spots are found by walking out from a
    /// random bit of ground until the water starts and stepping back in.
    /// A perch is only kept if a frog can both sit there (open to the sun, on
    /// it) and get to it (a spot of open water beside it, with its body trailing
    /// behind in water too).
    /// </summary>
    private void BuildPerches(Pond pond, Random rng)
    {
        float u = pond.U, len = Frog.LenOf(pond);
        bool Water(PointF p) { int x = (int)p.X, y = (int)p.Y; return x >= 0 && y >= 0 && x < pond.Width && y < pond.Height && pond.Water[y * pond.Width + x]; }
        bool OpenAt(PointF p) { int x = (int)p.X, y = (int)p.Y; return x >= 0 && y >= 0 && x < pond.Width && y < pond.Height && pond.Open[y * pond.Width + x]; }
        bool LandAt(PointF p) { int x = (int)p.X, y = (int)p.Y; return x >= 0 && y >= 0 && x < pond.Width && y < pond.Height && pond.Land[y * pond.Width + x]; }

        // Lily pads: any pad over 0.03u across whose middle is not under a branch.
        for (int i = 0; i < pond.Pads.Count; i++)
        {
            LilyPad p = pond.Pads[i];
            if (p.Radius <= u * 0.03f || !OpenAt(p.Center)) continue;
            float a = (float)(rng.NextDouble() * MathF.Tau);
            _perches.Add(new FrogPerch { Index = _perches.Count, Kind = PerchKind.Pad, Centre = p.Center, Radius = p.Radius, Out = new PointF(MathF.Cos(a), MathF.Sin(a)), PadIndex = i });
        }

        // Dry bank rocks (never the island's cap, which belongs to the turtles): the water side of each.
        foreach (BankRock r in pond.Rocks)
        {
            if (r.InWater || r.Radius <= u * 0.03f) continue;
            float bestRoom = 0; PointF bestOut = default;
            for (int k = 0; k < 16; k++)
            {
                float a = k * MathF.Tau / 16;
                var d = new PointF(MathF.Cos(a), MathF.Sin(a));
                var stage = new PointF(r.Centre.X + d.X * (r.Radius + len * 0.55f), r.Centre.Y + d.Y * (r.Radius + len * 0.55f));
                var behind = new PointF(stage.X + d.X * len * 0.45f, stage.Y + d.Y * len * 0.45f);
                if (!Water(stage) || !Water(behind)) continue;
                float room = pond.RoomAt(stage.X, stage.Y);
                if (room > bestRoom) { bestRoom = room; bestOut = d; }
            }
            var sit = new PointF(r.Centre.X + bestOut.X * r.Radius * 0.45f, r.Centre.Y + bestOut.Y * r.Radius * 0.45f);
            if (bestRoom <= len * 0.35f || !LandAt(sit) || !OpenAt(sit)) continue;
            _perches.Add(new FrogPerch { Index = _perches.Count, Kind = PerchKind.Rock, Centre = r.Centre, Radius = r.Radius, Out = bestOut });
        }

        // The bank's edge: walk out from a bit of ground toward the water, stop where the water starts,
        // step back in by 0.012u, and keep the spot if a frog could sit there and get to it.
        int banks = 0;
        for (int tries = 0; tries < 800 && banks < 10; tries++)
        {
            var p0 = new PointF((float)rng.NextDouble() * pond.Width, (float)rng.NextDouble() * pond.Height);
            if (!LandAt(p0) || !OpenAt(p0) || Water(p0)) continue;
            float a = (float)(rng.NextDouble() * MathF.Tau);
            var d = new PointF(MathF.Cos(a), MathF.Sin(a));
            PointF edge = p0; bool found = false;
            for (float t = 0; t < u * 0.25f; t += u * 0.002f)
            {
                edge = new PointF(p0.X + d.X * t, p0.Y + d.Y * t);
                if (Water(edge)) { found = true; break; }
                if (!LandAt(edge)) break;
            }
            if (!found) continue;
            var sit = new PointF(edge.X - d.X * u * 0.012f, edge.Y - d.Y * u * 0.012f);
            var stage = new PointF(sit.X + d.X * (u * 0.012f + len * 0.55f), sit.Y + d.Y * (u * 0.012f + len * 0.55f));
            var behind = new PointF(stage.X + d.X * len * 0.45f, stage.Y + d.Y * len * 0.45f);
            if (!LandAt(sit) || !OpenAt(sit) || !Water(stage) || !Water(behind) || pond.RoomAt(stage.X, stage.Y) <= len * 0.35f) continue;
            if (OnSomethingElse(pond, sit, u)) continue;
            bool crowded = false;
            foreach (FrogPerch q in _perches) if (q.Kind == PerchKind.Bank && Dist(q.Centre, sit) < u * 0.12f) crowded = true;
            if (crowded) continue;
            _perches.Add(new FrogPerch { Index = _perches.Count, Kind = PerchKind.Bank, Centre = sit, Radius = u * 0.04f, Out = d });
            banks++;
        }
    }

    /// <summary>Is this ground point on something that is not the bank itself: a rock, the log, the island's cap, the cattails, an iris clump?</summary>
    private static bool OnSomethingElse(Pond pond, PointF p, float u)
    {
        foreach (BankRock r in pond.Rocks) if (Dist(r.Centre, p) < r.Radius * 1.2f + u * 0.01f) return true;
        if (pond.Log is Log log)
        {
            PointF a = log.At(0), b = log.At(1);
            float ex = b.X - a.X, ey = b.Y - a.Y, e2 = MathF.Max(1e-3f, ex * ex + ey * ey);
            float t = Math.Clamp(((p.X - a.X) * ex + (p.Y - a.Y) * ey) / e2, 0, 1);
            if (Dist(new PointF(a.X + ex * t, a.Y + ey * t), p) < log.Width * 0.5f + u * 0.025f) return true;
        }
        if (pond.Island is Island isle && Dist(isle.Cap, p) < isle.CapRadius * 1.3f) return true;
        if (Dist(pond.CattailSpot, p) < u * 0.08f) return true;
        foreach ((PointF root, _) in pond.IrisSpots) if (Dist(root, p) < u * 0.06f) return true;
        return false;
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Starts a frog somewhere: on a perch that is on screen, in the sun, and
    /// free (every frog should be in view from the first frame). Null if there
    /// is none, and the frog then starts afloat.
    /// </summary>
    public FrogPerch? PickStart(Frog who, Pond pond, Random rng)
    {
        int free = 0;
        foreach (FrogPerch p in _perches) if (p.Holder == null && !p.Unsitable && OnScreen(pond, p.Centre)) free++;
        if (free == 0) return null;
        int pick = rng.Next(free);
        foreach (FrogPerch p in _perches)
        {
            if (p.Holder != null || p.Unsitable || !OnScreen(pond, p.Centre)) continue;
            if (pick-- == 0) { p.Holder = who; return p; }
        }
        return null;
    }

    private static bool OnScreen(Pond pond, PointF c) =>
        c.X > pond.Width * 0.06f && c.X < pond.Width * 0.94f && c.Y > pond.Height * 0.06f && c.Y < pond.Height * 0.94f;
}

/// <summary>
/// A pond frog seen from above: a tonosama-gaeru, the Japanese pond frog (olive
/// green with dark brown blotches and a pale line straight down the middle of
/// the back), about as long as a plum.
///
/// WHAT A POND FROG DOES (watch one on a lily pad): it SITS. For half a minute
/// at a time it does nothing at all but breathe: the pale throat under the chin
/// flutters, now and then it swells in two to four slow pulses (a croak, with no
/// sound here), once in a while it shuffles its weight or hops round on the spot
/// to face another way (usually toward the water). If a dragonfly settles close,
/// a bold frog snaps at it with a quick short hop. Then it LEAPS: the hind legs
/// snap straight out behind it, the forelegs tuck in, the body stretches, and it
/// flies in an arc (half a second, up to a few body lengths) and lands in the
/// water with a splash. It SWIMS with frog kicks: both hind legs sweep out and
/// back together in a quick push (the body surges), then fold forward slowly
/// while it glides; a push and a glide, about one a second. It FLOATS with its
/// legs hanging loose and only the eyes and the top of the back out of the
/// water. Frightened, it DIVES: a kick and it is under, hazier and deeper for a
/// few seconds, and it comes up again somewhere else, eyes first, with a tiny
/// ring. And it CLIMBS out: a short hop from the water onto a pad's rim or the
/// bank, and then it sits again, facing the water.
///
/// THE FOUR GATES a creature must pass (see CLAUDE.md, "Creatures"):
///   VISIBLE: a frog is bright olive on a pale-edged leaf or the green bank, not
///     a thing lost in glare; it never sits under a branch's shade at the start,
///     is drawn only where the view is open, and a dive hazes it by the same
///     depth rule as the koi (Light.Murk), so it is simply "under".
///   PERSPECTIVE: it is drawn in the pond's own view: its ground position is
///     squashed down the screen by Ripples.Squash like everything else, its
///     height in a leap is a lift up the screen with its shadow left behind on
///     the pad or the water (the gap between them is the height, Light.cs),
///     and a dive sinks it down the screen (Light.SinkPx).
///   BENDING OR NOT: a frog does not bend. Its body is a rigid wedge; it turns by
///     hopping round on the spot (a pivot), or in a leap by turning during the
///     crouch before it launches, and swimming by easing its heading between
///     kicks. So its spine is laid straight, as the turtle's is.
///   MOVES LIKE IT: sitting is the main thing it does; a leap is a ballistic arc
///     (even speed along the ground, the height a parabola, landing where its
///     shadow is); a swim surges on each kick and glides between them, never one
///     steady speed.
///
/// THE MIND (Core/Minds, docs/MINDS.md). Two dials, dealt at birth:
///   BOLD: sits longer, lets a threat 30% nearer, snaps at dragonflies, croaks more.
///   RESTLESS: shorter sits, takes to the water sooner, swims more than it floats.
/// One thing at a time (Doing): Sit (with the small events croak, shift and pivot
/// rolled inside it), Snap, Leap, Swim, Float, Dive, Climb. When a doing ends,
/// weighted dice pick the next. The reflexes underneath keep the last word: the
/// feelers and the "never stuck" step rule steer a swim, the water mask refuses a
/// step onto land or a pad, and a threat (the snake's head, a koi's head, a
/// turtle, a lizard, a swallow's shadow) cuts in with a cooldown so it cannot
/// fire every frame.
///
/// SOLID: swimming, floating or dived, the frog fills Shape (ground plane, depth
/// in pixels) like the turtle and the snake, and the scene hands the swimmers to
/// KoiSchool, so the fish steer round a frog and a fish that still meets one is
/// pushed out. A frog is never moved by a fish: it dives when a koi's head comes
/// close. On a perch it is above the water and in nobody's way; in a leap it is
/// in the air. It passes UNDER nothing (a pad is an obstacle to a swimming
/// frog, the one thing a koi does swim under), and a dived frog passes under the
/// floating petals; a frog at the surface shoves them aside.
/// </summary>
internal sealed class Frog : Body
{
    private const int Points = 12;
    private const int SkinS = 128, SkinV = 32;

    /// <summary>What a frog can be doing. One at a time; Decide() picks the next when the current one ends.</summary>
    private enum Doing { Sit, Snap, Leap, Swim, Float, Dive, Climb }

    /// <summary>A hop: a short ballistic jump (or a turn on the spot, a Pivot) scripted from its start and end.</summary>
    private enum HopKind { None, Pivot, Snap, Leap, Climb }

    private readonly Frogs _owner;
    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _where, _shadowMask;
    private readonly Random _rng;
    private readonly Dragonflies? _dragonflies;
    private readonly float _u, _len, _wide;                // the frog's length, and half its width at the hips, ground pixels
    private readonly uint[] _skin;

    // The pads and the cattails, as circles, for the swimming steering (the rest of the obstacles are in Pond.RoomAt).
    private readonly PointF[] _padC;
    private readonly float[] _padR;
    private readonly PointF _cattail;
    private readonly float _cattailR;
    private readonly float[] _w;                           // scratch: the weights for choosing a perch
    private readonly PointF[] _stageCache;
    private readonly PointF[] _dirCache;
    private readonly PointF[] _preCache;
    private readonly float[] _bad;                         // per perch: seconds it is to be left alone (a swim to it failed)

    // ---- the mind ----
    private readonly float _bold, _restless;
    private readonly Mind<Doing> _mind = new(Doing.Sit);
    private Doing _doing => _mind.Doing;
    private readonly Cooldown _alarm = new();              // not another fright until this has run out

    // ---- where it is ----
    private float _x, _y, _heading;                        // the body's middle, ground pixels; ground radians
    private float _speed, _turnRate;
    private float _wet;                                    // seconds since it last sat on something: the longer, the keener to get out
    private float _z;                                      // depth: 0 at the surface, 1 as deep as a koi goes
    private float _zTarget;
    private bool _rising;
    private float _height;                                 // how high above the water it is, in u (a leap, plus the little it stands on a perch)
    private float _hopHeight, _perchLift;
    private float _pose, _poseWant;                        // 0 hind legs folded, 1 stretched out behind
    private float _kickT, _period = 1f, _boost;            // swimming: seconds into this kick, the kick's length, a dive's extra push
    private float _fx = 1, _fy;                            // cos and sin of the heading, once a frame
    private double _time;
    private float _throat;                                 // how swollen the throat is, 0 to 1

    // ---- the perch and the plan ----
    private FrogPerch? _perch;                             // the one it sits on
    private FrogPerch? _lastPerch;
    private FrogPerch? _goalPerch;                         // the one it is swimming to
    private PointF _pre;                                  // the point out in the water it swims to first, to line up on the way in
    private bool _lined;                                   // it has reached _pre and is now swimming in along the line
    private PointF _stage, _dirIn;                         // where it waits beside the goal perch (screen), and the unit vector from the perch toward that spot
    private bool _hasGoal;
    private float _goalX, _goalY;                          // a wander goal, ground pixels
    private float _faceOut;                                // the ground heading it should sit facing (the water)
    private bool _faceFirst;                               // after a climb: the first thing it does is turn to face the water

    // ---- sitting events ----
    private float _croakIn, _croakT = -1, _croakPulses, _shiftIn, _shiftT = -1, _pivotIn, _snapIn;

    // ---- a hop in hand ----
    private HopKind _hop;
    private float _hopT, _hopDur, _crouch, _ax, _ay, _bx, _by, _hmax, _h0, _h1;
    private bool _scared, _snapToWater, _splashPending;
    private float _splashIn;
    private PointF _splashAt, _snapBug;
    private float _savedLeft;

    // ---- the world this frame ----
    private PointF? _snakeHead;
    private PointF[] _koiHeads = [], _turtleSpots = [], _lizardNoses = [];
    private PointF? _startleAt;
    private bool _blocked;
    private float _refused;
    private int _skipPad = -1;                             // the pad it is swimming to is not an obstacle

    /// <summary>For instruments: called with a line of text at each leap, climb, dive and threat. Null in the real saver, so nothing is built.</summary>
    public Action<string>? Log { get; set; }

    public Frog(Frogs owner, Pond pond, Ripples ripples, bool[] where, bool[] shadowMask, Random rng, float bold, float restless, Dragonflies? dragonflies) : base(Points)
    {
        _owner = owner;
        _pond = pond;
        _ripples = ripples;
        _where = where;
        _shadowMask = shadowMask;
        _rng = rng;
        _bold = bold;
        _restless = restless;
        _dragonflies = dragonflies;
        _u = pond.U;
        _len = LenOf(pond);
        _wide = _len * 0.30f;
        _skin = Skin(rng);
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _padC = new PointF[pond.Pads.Count];
        _padR = new float[pond.Pads.Count];
        for (int i = 0; i < _padC.Length; i++) { _padC[i] = pond.Pads[i].Center; _padR[i] = pond.Pads[i].Radius; }
        _cattail = pond.CattailSpot;
        _cattailR = _u * 0.06f;
        int n = owner.Perches.Count;
        _w = new float[n]; _stageCache = new PointF[n]; _dirCache = new PointF[n]; _preCache = new PointF[n]; _bad = new float[n];
    }

    /// <summary>How long a frog is, snout to rump, ground pixels.</summary>
    public static float LenOf(Pond pond) => pond.U * 0.045f;

    // ================================================================ state read by the scene, the wrapper and instruments

    /// <summary>The middle of the body, screen pixels, before it is lifted or sunk.</summary>
    public PointF Centre => ToScreen(_x, _y);
    public PointF Nose => Spine[0];
    public PointF Rump => Spine[Points - 1];
    public float Z => _z;
    public float Height => _height;
    public float Speed => _speed;
    public float Heading => _heading;
    public string State => _hop == HopKind.None ? _doing.ToString() : _doing + "/" + _hop;
    public string PerchName => _perch == null ? "-" : _perch.Kind + ":" + _perch.Index;
    public float PerchRadius => _perch?.Radius ?? 0;

    /// <summary>In the water this frame (swimming, floating or dived), so solid to the fish. Not in a hop (it is in the air) and not on a perch.</summary>
    public bool InWater => _hop == HopKind.None && _doing is Doing.Swim or Doing.Float or Doing.Dive;

    /// <summary>Under the surface enough to be drawn with the dived fish (after the koi), else drawn over the water's surface.</summary>
    public bool UnderWater => _z > 0.03f;

    /// <summary>Is its middle on what it sits on? (Pad: inside the leaf; rock: inside the stone; bank: on ground.) For tests.</summary>
    public bool MiddleOnPerch
    {
        get
        {
            if (_perch == null) return false;
            PointF m = Centre;
            switch (_perch.Kind)
            {
                case PerchKind.Pad:
                    float dx = (m.X - _perch.Centre.X) / _perch.Radius, dy = (m.Y - _perch.Centre.Y) / (_perch.Radius * 0.82f);
                    return dx * dx + dy * dy <= 1f;
                case PerchKind.Rock:
                    return Dist(m, _perch.Centre) <= _perch.Radius;
                default:
                    return LandAt(m);
            }
        }
    }

    /// <summary>Is any part of it painted on an open (unshaded by branches) pixel and not lost in the water?</summary>
    public bool VisibleDrawn
    {
        get
        {
            bool air = _hop != HopKind.None || _doing == Doing.Sit;
            if (!air && Light.Murk(_z) > 0.985f) return false;
            for (int i = 0; i < Points; i++)
            {
                int x = (int)Spine[i].X, y = (int)Spine[i].Y;
                if (x >= 0 && y >= 0 && x < _pond.Width && y < _pond.Height && _pond.Open[y * _pond.Width + x]) return true;
            }
            return false;
        }
    }

    // ================================================================ small helpers

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private (float, float) ToGround(PointF p) => ToGround(p.X, p.Y);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float GroundHeading(float sx, float sy) => MathF.Atan2(sy / Ripples.Squash, sx);

    private bool WaterAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix];
    }

    private bool LandAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Land[iy * _pond.Width + ix];
    }

    private bool OpenAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Open[iy * _pond.Width + ix];
    }

    private float FloorAt(PointF s) => _pond.Floor[Math.Clamp((int)s.Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)s.X, 0, _pond.Width - 1)] / 255f;

    private float DepthPx => Pond.DepthU * _u;

    /// <summary>How much smaller it is drawn for its depth (the koi's perspective rule, Light.Scale).</summary>
    private float Sc => Light.Scale(_z);

    private float Sink => Light.SinkPx(_z, _u);

    /// <summary>How far a point is lifted up the screen at the frog's present height.</summary>
    private float LiftPx => Light.Lift * _height * _u;

    private float BodyLen => _len * Sc * (1 + 0.15f * _pose);

    /// <summary>Half the body's width at s, ground pixels: a wedge with a blunt nose, widest at the hips, a rounded rump.</summary>
    private float Wid(float s) => _wide * Sc * (1 - 0.12f * _pose) * Profile(s);

    private static float Profile(float s)
    {
        if (s < 0.72f) return 0.2f + 0.8f * MathF.Sin(s / 0.72f * MathF.PI * 0.5f);
        float k = (s - 0.72f) / 0.28f;
        return 1 - 0.5f * k * k;
    }

    /// <summary>A point of the body on the ground plane: s along from the snout (0) to the rump (1), v across (-1 to +1 of the half width there). The frog's middle is _x, _y.</summary>
    private (float X, float Y) RawGround(float s, float v)
    {
        float along = (0.5f - s) * BodyLen, across = v * Wid(s);
        return (_x + _fx * along - _fy * across, _y + _fy * along + _fx * across);
    }

    private PointF Raw(float s, float v)
    {
        (float gx, float gy) = RawGround(s, v);
        return ToScreen(gx, gy);
    }

    /// <summary>A point as it is drawn: sunk for its depth, lifted for its height.</summary>
    private PointF Drawn_(float gx, float gy)
    {
        PointF p = ToScreen(gx, gy);
        return new PointF(p.X, p.Y + Sink - LiftPx);
    }

    // ================================================================ placing it

    /// <summary>Starts the frog sitting on a free perch that is on screen (or afloat, if there is none).</summary>
    public void Seat()
    {
        FrogPerch? p = null;
        PointF dir = default;
        for (int attempt = 0; attempt < 8 && p == null; attempt++)
        {
            FrogPerch? q = _owner.PickStart(this, _pond, _rng);
            if (q == null) break;
            dir = q.Out;
            if (q.Kind == PerchKind.Pad)
            {
                // A pad: some way to face on bare leaf (not the notch, not a flower); if there is none this pad is not for sitting on.
                bool ok = PadSolidAt(q, dir);
                for (int k = 0; k < 24 && !ok; k++) { float a = (float)(_rng.NextDouble() * MathF.Tau); dir = new PointF(MathF.Cos(a), MathF.Sin(a)); ok = PadSolidAt(q, dir); }
                if (!ok) { q.Holder = null; q.Unsitable = true; continue; }
            }
            p = q;
        }
        if (p != null)
        {
            _perch = p;
            SitSpot(p, dir, out PointF spot);
            (_x, _y) = ToGround(spot);
            _faceOut = GroundHeading(dir.X, dir.Y);
            _heading = _faceOut;
            _perchLift = 0.005f;
            BeginSit(first: true);
            _faceFirst = false;
        }
        else
        {
            // No perch: afloat in some open water.
            for (int tries = 0; tries < 200; tries++)
            {
                var s = new PointF((float)_rng.NextDouble() * _pond.Width, (float)_rng.NextDouble() * _pond.Height);
                if (!WaterAt(s) || !OpenAt(s) || _pond.RoomAt(s.X, s.Y) < _len * 1.5f || Clear(s.X, s.Y, -1) < _len) continue;
                (_x, _y) = ToGround(s);
                break;
            }
            _heading = (float)(_rng.NextDouble() * MathF.Tau);
            BeginFloat(3 + 4 * (float)_rng.NextDouble());
        }
        _croakIn = 3 + 10 * (float)_rng.NextDouble();
        _shiftIn = 4 + 12 * (float)_rng.NextDouble();
        _pivotIn = 4 + 14 * (float)_rng.NextDouble();
        _snapIn = 1 + (float)_rng.NextDouble();
        _poseWant = 0;
        _pose = 0;
        LaySpine();
    }

    /// <summary>Where the frog's middle sits on a perch: a pad 0.3 of its radius in from the rim toward "dir" (the way it will face), a rock 0.45, the bank's spot itself.</summary>
    private static void SitSpot(FrogPerch p, PointF dir, out PointF spot)
    {
        float f = p.Kind == PerchKind.Pad ? 0.3f : p.Kind == PerchKind.Rock ? 0.45f : 0f;
        spot = new PointF(p.Centre.X + dir.X * p.Radius * f, p.Centre.Y + dir.Y * p.Radius * f);
    }

    // ================================================================ the update

    /// <param name="snakeHead">The snake's head if it is out, else null.</param>
    /// <param name="koiHeads">Points on the koi near the surface.</param>
    /// <param name="turtleSpots">A point at each turtle's nose and shell.</param>
    /// <param name="lizardNoses">The nose of each lizard that is out.</param>
    public void Update(float dt, PointF? snakeHead, PointF[] koiHeads, PointF[] turtleSpots, PointF[] lizardNoses)
    {
        _time += dt;
        _snakeHead = snakeHead; _koiHeads = koiHeads; _turtleSpots = turtleSpots; _lizardNoses = lizardNoses;
        _alarm.Tick(dt);
        _mind.Tick(dt);
        for (int i = 0; i < _bad.Length; i++) if (_bad[i] > 0) _bad[i] -= dt;
        if (_doing == Doing.Sit) _wet = 0; else _wet += dt;
        if (_splashPending)
        {
            _splashIn -= dt;
            if (_splashIn <= 0) { _splashPending = false; _ripples.Add(_splashAt.X, _splashAt.Y, 0.3f); }
        }

        CheckThreats();

        if (_hop != HopKind.None) Hop(dt);
        else if (_doing == Doing.Sit) SitStill(dt);
        else SwimOrFloat(dt);

        // The little it stands up on a perch: eased, so a leap does not start with a drop.
        bool standing = _doing == Doing.Sit || _hop == HopKind.Snap;
        _perchLift += ((standing ? 0.005f : 0f) - _perchLift) * (1 - MathF.Exp(-dt * 12f));
        _height = _hopHeight + _perchLift;
        _pose += (_poseWant - _pose) * (1 - MathF.Exp(-dt * 20f));
        LaySpine();
    }

    // ================================================================ threats

    private static bool NearAny(PointF[] pts, PointF me, float r, out PointF hit)
    {
        hit = default;
        float best = r * r;
        bool found = false;
        for (int i = 0; i < pts.Length; i++)
        {
            float d = (pts[i].X - me.X) * (pts[i].X - me.X) + (pts[i].Y - me.Y) * (pts[i].Y - me.Y);
            if (d < best) { best = d; hit = pts[i]; found = true; }
        }
        return found;
    }

    /// <summary>A swallow's shadow was swept over here: noted, and answered on the next update if it was near.</summary>
    public void Startle(float x, float y)
    {
        if (Dist(Centre, new PointF(x, y)) < _u * 0.15f) _startleAt = new PointF(x, y);
    }

    /// <summary>
    /// The snake's head within 0.2u, a koi's head within 0.04u while it is in
    /// the water, a turtle within 0.06u while it is in the water, a lizard's nose
    /// within 0.08u while it sits on a rock or the bank, or a swallow's shadow:
    /// a sitting frog LEAPS, a swimming or floating one DIVES. A bold frog lets
    /// a threat 30% nearer. A cooldown after each answer (3 to 6 seconds, 10 to 18 after a fish or a turtle, which keep drifting by) so a
    /// koi keeping pace does not set it off every frame.
    /// </summary>
    private void CheckThreats()
    {
        PointF? shadow = _startleAt;
        _startleAt = null;
        if (_hop is HopKind.Snap or HopKind.Leap or HopKind.Climb || _doing is Doing.Dive or Doing.Leap or Doing.Snap or Doing.Climb) return;
        PointF me = Centre;
        float reach = 1f - 0.3f * _bold;
        bool sitting = _doing == Doing.Sit;
        string? src = null;
        PointF at = default;
        if (_snakeHead is PointF sh && Dist(sh, me) < _u * 0.2f * reach) { src = "snake"; at = sh; }
        else if (shadow is PointF sd) { src = "shadow"; at = sd; }
        else if (sitting && _perch != null && _perch.Kind != PerchKind.Pad && NearAny(_lizardNoses, me, _u * 0.08f * reach, out PointF ln)) { src = "lizard"; at = ln; }
        else if (!sitting && NearAny(_koiHeads, me, _u * 0.04f * reach, out PointF kh)) { src = "koi"; at = kh; }
        else if (!sitting && NearAny(_turtleSpots, me, _u * 0.06f * reach, out PointF ts)) { src = "turtle"; at = ts; }
        if (src == null) return;
        Log?.Invoke($"reach {src} ready={(_alarm.Ready ? 1 : 0)}");
        if (!_alarm.Ready) return;

        Log?.Invoke($"threat {src} at={at.X:F0},{at.Y:F0} state={State}");
        // A dive from a fish or a turtle (which keep drifting by) is followed by a longer wait than a flight from a one-off scare.
        _alarm.Start((src is "koi" or "turtle" ? 10 : 3) + (src is "koi" or "turtle" ? 8 : 3) * (float)_rng.NextDouble());
        if (sitting) { if (!BeginLeap(at, src)) _alarm.Start(1f); }
        else BeginDive(at);
    }

    // ================================================================ sitting

    private void BeginSit(bool first = false)
    {
        float dur = (10 + 50 * (float)_rng.NextDouble()) * (0.55f + 0.9f * _bold) * (1.35f - 0.7f * _restless);
        _mind.Begin(Doing.Sit, Math.Clamp(dur, 8f, 70f));
        _speed = 0; _turnRate = 0; _z = 0; _zTarget = 0; _hop = HopKind.None; _hopHeight = 0;
        if (!first) { _faceFirst = true; _pivotIn = 0.45f; }
        _poseWant = 0;
    }

    /// <summary>Sit: still, breathing, with the small events rolled inside it.</summary>
    private void SitStill(float dt)
    {
        _speed = 0;
        float boldMul = 0.6f + 0.9f * _bold;

        // The croak: two to four slow swells of the throat over a second and a half.
        _croakIn -= dt;
        if (_croakT < 0 && _croakIn <= 0)
        {
            _croakT = 0; _croakPulses = 2 + _rng.Next(3);
            Log?.Invoke($"croak pulses={_croakPulses:F0}");
            _croakIn = (7 + 15 * (float)_rng.NextDouble()) / boldMul;
        }
        float breath = 0.12f + 0.07f * MathF.Sin((float)(_time % 1000.0) * 5.2f);          // a fine flutter all the time
        if (_croakT >= 0)
        {
            _croakT += dt;
            float p = _croakT / 1.5f;
            if (p >= 1) _croakT = -1;
            else _throat = breath + (1 - breath) * MathF.Sqrt(MathF.Sin(MathF.PI * p)) * (0.5f - 0.5f * MathF.Cos(MathF.Tau * _croakPulses * p));
        }
        if (_croakT < 0) _throat = breath;

        // A shift of weight: the hind legs settle a little and come back. The body does not move.
        _shiftIn -= dt;
        if (_shiftT < 0 && _shiftIn <= 0) { _shiftT = 0; _shiftIn = 9 + 16 * (float)_rng.NextDouble(); }
        if (_shiftT >= 0)
        {
            _shiftT += dt;
            if (_shiftT > 0.8f) { _shiftT = -1; _poseWant = 0; }
            else _poseWant = 0.14f * MathF.Sin(MathF.PI * _shiftT / 0.8f);
        }

        // A hop round on the spot: first to face the water after a climb, then now and then to face another way.
        _pivotIn -= dt;
        if (_pivotIn <= 0 && _hop == HopKind.None)
        {
            float to;
            if (_faceFirst) { to = _faceOut; _faceFirst = false; }
            else if (_rng.NextDouble() < 0.65) to = _faceOut + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
            else to = _heading + (_rng.Next(2) == 0 ? 1 : -1) * (0.35f + 1.2f * (float)_rng.NextDouble());
            if (MathF.Abs(Wrap(to - _heading)) > 0.1f) BeginHop(HopKind.Pivot, _x, _y, _x, _y, 0, 0.15f, 0.003f * _u, to);
            _pivotIn = (7 + 18 * (float)_rng.NextDouble()) / boldMul * (1.2f - 0.5f * _restless);
        }

        // A dragonfly settled close: a bold frog snaps at it.
        _snapIn -= dt;
        if (_snapIn <= 0)
        {
            _snapIn = 0.3f + 0.5f * (float)_rng.NextDouble();
            if (_dragonflies != null && _hop == HopKind.None && _rng.NextDouble() < 0.1 + 0.6 * _bold) TrySnap();
        }

        if (_mind.Over && _hop == HopKind.None)
        {
            if (!BeginLeap(null, "walk")) _mind.Left = 3 + 4 * (float)_rng.NextDouble();   // no good water to land in just now: sit on and look again
        }
    }

    /// <summary>
    /// A quick short hop toward a dragonfly sitting within 0.07u of the snout (up to 0.03u),
    /// landing on the same perch, or, if that is where the bug is, in the water.
    /// </summary>
    private void TrySnap()
    {
        PointF nose = Nose;
        if (_perch == null || !_dragonflies!.NearestPerched(nose.X, nose.Y, _u * 0.07f, out PointF bug)) return;
        PointF c = Centre;
        float d = Dist(c, bug);
        if (d < _len * 0.3f) return;                                   // it is right under the snout already
        float move = MathF.Min(_u * 0.03f, MathF.Max(0, d - _len * 0.45f));
        if (move < _u * 0.006f) return;
        var dest = new PointF(c.X + (bug.X - c.X) / d * move, c.Y + (bug.Y - c.Y) / d * move);
        bool onPerch;
        switch (_perch.Kind)
        {
            case PerchKind.Pad:
                float dx = (dest.X - _perch.Centre.X) / (_perch.Radius * 0.85f), dy = (dest.Y - _perch.Centre.Y) / (_perch.Radius * 0.85f * 0.82f);
                onPerch = dx * dx + dy * dy <= 1f;
                break;
            case PerchKind.Rock: onPerch = Dist(dest, _perch.Centre) <= _perch.Radius * 0.9f && LandAt(dest); break;
            default: onPerch = LandAt(dest) && OpenAt(dest) && !WaterAt(dest); break;
        }
        bool water = !onPerch && WaterAt(dest) && _pond.RoomAt(dest.X, dest.Y) > _len * 0.6f && Clear(dest.X, dest.Y, -1) > _len * 0.4f;
        if (!onPerch && !water) return;
        _snapToWater = water; _snapBug = bug;
        _savedLeft = _mind.Left;
        _mind.Begin(Doing.Snap);
        (float gx, float gy) = ToGround(dest);
        float face = MathF.Atan2(gy - _y, gx - _x);
        if (water) { _owner.Free(_perch, this); _lastPerch = _perch; _perch = null; }
        BeginHop(HopKind.Snap, _x, _y, gx, gy, 0.18f, 0.06f, 0.008f * _u, face);
        Log?.Invoke($"snap bug={bug.X:F0},{bug.Y:F0} water={water}");
    }

    // ================================================================ hops

    private void BeginHop(HopKind kind, float ax, float ay, float bx, float by, float flight, float crouch, float hmax, float face)
    {
        _hop = kind; _hopT = 0; _crouch = crouch; _hopDur = crouch + flight;
        _ax = ax; _ay = ay; _bx = bx; _by = by; _hmax = hmax;
        _h0 = _heading; _h1 = _h0 + Wrap(face - _h0);
        _scared = false;
    }

    /// <summary>
    /// A hop is a script from its start to its end. First the CROUCH: the legs
    /// stay folded and the frog swings round to face the way it will go (a
    /// frog turns by hopping, never by bending). Then the FLIGHT: even speed
    /// along the ground (a ballistic arc has none to lose sideways), the height
    /// a parabola, so it lands where its shadow is; the legs snap out as it
    /// leaves the ground and fold again as it comes down.
    /// </summary>
    private void Hop(float dt)
    {
        _hopT += dt;
        float tc = _crouch, tf = _hopDur - tc;
        bool pivot = _hop == HopKind.Pivot;
        float turn = tc > 1e-4f ? Smooth(_hopT / tc) : 1f;
        _heading = Wrap(_h0 + (_h1 - _h0) * turn);
        float q = pivot || tf <= 1e-4f ? 0f : Math.Clamp((_hopT - tc) / tf, 0f, 1f);
        if (!pivot)
        {
            _x = _ax + (_bx - _ax) * q;
            _y = _ay + (_by - _ay) * q;
            _hopHeight = 4 * _hmax * q * (1 - q) / _u;
            _poseWant = _hopT < tc ? 0f : q < 0.2f ? Smooth(q / 0.2f) : q < 0.7f ? 1f : 1f - 0.75f * Smooth((q - 0.7f) / 0.3f);
        }
        else
        {
            _hopHeight = _hmax * MathF.Sin(MathF.PI * Math.Clamp(_hopT / _hopDur, 0, 1)) / _u;
            _poseWant = 0.25f * MathF.Sin(MathF.PI * Math.Clamp(_hopT / _hopDur, 0, 1));
        }
        if (_hop == HopKind.Snap && q > 0.6f && !_scared)
        {
            _scared = true;
            _dragonflies?.Scare(_snapBug.X, _snapBug.Y, _u * 0.04f);       // the bug takes off as the frog arrives
        }
        if (_hopT >= _hopDur) EndHop();
    }

    private void EndHop()
    {
        HopKind kind = _hop;
        _hop = HopKind.None; _hopHeight = 0;
        _poseWant = 0;
        switch (kind)
        {
            case HopKind.Pivot:
                _heading = Wrap(_h1);
                break;
            case HopKind.Snap:
                _x = _bx; _y = _by;
                if (_snapToWater) { Splash(); BeginSwim(); }
                else _mind.Begin(Doing.Sit, _savedLeft);
                break;
            case HopKind.Leap:
                _x = _bx; _y = _by;
                Splash();
                BeginSwim();
                break;
            case HopKind.Climb:
                _x = _bx; _y = _by;
                Arrive();
                break;
        }
    }

    /// <summary>The ring a frog makes landing: a big one now, a smaller one a quarter second behind.</summary>
    private void Splash()
    {
        PointF s = Centre;
        _ripples.Add(s.X, s.Y, 0.6f);
        _splashAt = s; _splashIn = 0.25f; _splashPending = true;
    }

    // ================================================================ leaving the perch

    /// <summary>The ground cost of a landing here: open water with room, away from the pads, the fish and the other frogs.</summary>
    private bool LandingOk(PointF p)
    {
        if (!WaterAt(p) || !OpenAt(p)) return false;
        if (_pond.RoomAt(p.X, p.Y) <= _len * 0.6f || Clear(p.X, p.Y, -1) <= _len * 0.5f) return false;
        if (NearAny(_koiHeads, p, _u * 0.05f, out _)) return false;
        if (_owner.OtherFrogNear(this, p, _u * 0.06f)) return false;
        return true;
    }

    /// <summary>
    /// Leaps into the water. The direction wanted is away from the threat, else
    /// toward the perch or open water it means to swim to, else out from its
    /// perch; headings round that are tried in turn, with a landing 0.08 to
    /// 0.18u off, and the first that lands in open water is taken. False if
    /// every one of them was blocked.
    /// </summary>
    private bool BeginLeap(PointF? threat, string src)
    {
        PointF here = Centre;
        float want;
        if (threat is PointF t) want = GroundHeading(here.X - t.X, here.Y - t.Y);
        else
        {
            // Plan the swim first and leap toward it (before, the leap went toward a perch chosen at random and the swim then planned
            // another: a frog leapt away from the pad it then spent thirty seconds swimming to).
            PlanGoal(true);
            if (_goalPerch != null) want = GroundHeading(_pre.X - here.X, _pre.Y - here.Y);
            else if (LeapTarget(here) is PointF to) want = GroundHeading(to.X - here.X, to.Y - here.Y);
            else want = _perch != null ? GroundHeading(_perch.Out.X, _perch.Out.Y) : _heading;
        }
        ReadOnlySpan<float> offs = [0, 0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f, 1.7f, -1.7f, 2.2f, -2.2f, MathF.PI];
        ReadOnlySpan<float> dists = [0f, 0.12f, 0.08f, 0.06f];                // 0 = a random 0.08 to 0.18u
        foreach (float dd in dists)
            foreach (float off in offs)
            {
                float d = dd > 0 ? dd : 0.08f + 0.1f * (float)_rng.NextDouble();
                float a = want + off;
                float gx = _x + MathF.Cos(a) * d * _u, gy = _y + MathF.Sin(a) * d * _u;
                PointF land = ToScreen(gx, gy);
                if (!LandingOk(land)) continue;
                float flight = 0.4f + 0.2f * Math.Clamp((d - 0.08f) / 0.1f, 0, 1);
                float hmax = Math.Clamp(0.18f * d, 0.012f, 0.03f) * _u;
                string from = PerchName;
                _owner.Free(_perch, this); _lastPerch = _perch;
                _perch = null;
                _mind.Begin(Doing.Leap);
                _speed = 0;
                BeginHop(HopKind.Leap, _x, _y, gx, gy, flight, 0.12f, hmax, a);
                Log?.Invoke($"leap src={src} from={from} to={land.X:F0},{land.Y:F0} d={d:F3}");
                return true;
            }
        ReleaseGoal();
        return false;
    }

    /// <summary>Roughly where a leap should head: the middle of a free perch within half a screen (the nearer likelier), or nothing. Nothing is claimed; the swim after the landing plans properly.</summary>
    private PointF? LeapTarget(PointF here)
    {
        var ps = _owner.Perches;
        int usable = 0;
        for (int i = 0; i < ps.Count; i++)
        {
            FrogPerch p = ps[i];
            _w[i] = 0;
            if ((p.Holder != null && p.Holder != this) || p == _perch || p == _lastPerch) continue;
            float d = Dist(here, p.Centre);
            if (d < _u * 0.12f || d > _u * 0.5f) continue;
            _w[i] = 1f / (0.15f + d / _u);
            usable++;
        }
        if (usable == 0) return null;
        int pick = Dice.Pick(_rng, _w);
        return pick < 0 ? null : ps[pick].Centre;
    }

    // ================================================================ choosing where to swim

    /// <summary>
    /// Clearance at a screen point for a swimming frog: the distance to the
    /// nearest bank, rock or other thing standing in the water (Pond.RoomAt),
    /// or to a lily pad or the cattails (a pad is no wall to a koi, which swims
    /// under it, but it is one to a frog, which swims on top). The pad it is
    /// swimming to is not an obstacle.
    /// </summary>
    private float Clear(float x, float y, int skipPad)
    {
        float best = _pond.RoomAt(x, y);
        for (int i = 0; i < _padC.Length; i++)
        {
            if (i == skipPad) continue;
            float dx = x - _padC[i].X, dy = y - _padC[i].Y;
            float d = MathF.Sqrt(dx * dx + dy * dy) - _padR[i];
            if (d < best) best = d;
        }
        float cx = x - _cattail.X, cy = y - _cattail.Y;
        return MathF.Min(best, MathF.Sqrt(cx * cx + cy * cy) - _cattailR);
    }

    /// <summary>Is the straight swim between two screen points clear (sampled every few pixels) of everything but pad "skip"?</summary>
    private bool LineClear(PointF a, PointF b, int skip, float least)
    {
        for (int i = 2; i <= 12; i++)
        {
            float k = i / 12f;
            if (Clear(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k, skip) < least) return false;
        }
        return true;
    }

    /// <summary>Is this screen point bare green leaf in the painted pond (not the notch's open water, not a water lily's pink flower)?</summary>
    private bool LeafAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || _pond.Water[iy * _pond.Width + ix]) return false;
        uint c = _pond.Pixels[iy * _pond.Width + ix];
        int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
        return g - r > 22 && g > b + 12;                    // leaf green; a flower is pink or white (red as high as green), the water teal
    }

    /// <summary>
    /// Is there bare leaf where a frog would sit on this pad facing out along d:
    /// at its middle, a little to each side and a little each way along its length?
    /// Not the notch (a wedge of open water) and not a water lily's flower: a
    /// frog sitting on top of a blossom looked wrong in the first follow sheet.
    /// </summary>
    private bool PadSolidAt(FrogPerch p, PointF d)
    {
        var m = new PointF(p.Centre.X + d.X * p.Radius * 0.3f, p.Centre.Y + d.Y * p.Radius * 0.3f);
        float w = _len * 0.28f;
        return LeafAt(m) && LeafAt(new PointF(m.X - d.Y * w, m.Y + d.X * w)) && LeafAt(new PointF(m.X + d.Y * w, m.Y - d.X * w))
            && LeafAt(new PointF(m.X + d.X * w, m.Y + d.Y * w)) && LeafAt(new PointF(m.X - d.X * w, m.Y - d.Y * w));
    }

    /// <summary>How far from a pad's middle its edge is along unit direction d (a pad is an ellipse, 0.82 as tall as wide).</summary>
    private static float PadEdge(float r, PointF d)
    {
        float k = d.X * d.X + d.Y * d.Y / (0.82f * 0.82f);
        return r / MathF.Sqrt(MathF.Max(1e-6f, k));
    }

    /// <summary>
    /// A spot of open water beside a perch to wait at, with the frog facing the
    /// perch: a few steps' reach (about half a body length) off the rim, with the
    /// frog's body trailing behind it in water too and the swim there clear. For a
    /// pad or a rock the side is the one it is on, then the sides either way
    /// round; the bank's spot has only its own.
    /// </summary>
    private bool StageFor(FrogPerch p, PointF from, out PointF stage, out PointF dirIn, out PointF pre)
    {
        stage = default; dirIn = default; pre = default;
        float margin = 0.55f * _len;
        ReadOnlySpan<float> offs = [0, 0.5f, -0.5f, 1.0f, -1.0f, 1.5f, -1.5f, 2.2f, -2.2f, MathF.PI];
        if (p.Kind == PerchKind.Bank) offs = [0f];
        float baseA = p.Kind == PerchKind.Bank ? MathF.Atan2(p.Out.Y, p.Out.X) : MathF.Atan2(from.Y - p.Centre.Y, from.X - p.Centre.X);
        foreach (float off in offs)
        {
            float a = baseA + off;
            var d = new PointF(MathF.Cos(a), MathF.Sin(a));
            PointF s;
            if (p.Kind == PerchKind.Pad) { float e = PadEdge(p.Radius, d); s = new PointF(p.Centre.X + d.X * (e + margin), p.Centre.Y + d.Y * (e + margin)); }
            else if (p.Kind == PerchKind.Rock) s = new PointF(p.Centre.X + d.X * (p.Radius + margin), p.Centre.Y + d.Y * (p.Radius + margin));
            else s = new PointF(p.Centre.X + d.X * (_u * 0.012f + margin), p.Centre.Y + d.Y * (_u * 0.012f + margin));
            var behind = new PointF(s.X + d.X * _len * 0.45f, s.Y + d.Y * _len * 0.45f);
            if (p.Kind == PerchKind.Pad && !PadSolidAt(p, d)) continue;            // not over the leaf's notch, which is open water
            if (s.X < _len || s.Y < _len || s.X > _pond.Width - _len || s.Y > _pond.Height - _len) continue;
            if (!WaterAt(s) || !WaterAt(behind) || !OpenAt(s) || _pond.RoomAt(s.X, s.Y) <= _len * 0.35f) continue;
            if (Clear(s.X, s.Y, p.PadIndex) <= _len * 0.5f || _owner.OtherFrogNear(this, s, _len * 0.8f)) continue;
            // The way in is a line: out at "pre", two and a bit body lengths straight out from the perch, then in along it,
            // so it arrives pointing at the perch (aiming straight at the spot beside a rim from the side arrived pointing
            // along the rim, and swam on past: the first timeline had a frog circle the bank for a minute).
            var q = new PointF(s.X + d.X * _len * 2.2f, s.Y + d.Y * _len * 2.2f);
            if (q.X < _len * 0.6f || q.Y < _len * 0.6f || q.X > _pond.Width - _len * 0.6f || q.Y > _pond.Height - _len * 0.6f) continue;
            if (!WaterAt(q) || !OpenAt(q) || _pond.RoomAt(q.X, q.Y) <= _len * 0.35f || Clear(q.X, q.Y, p.PadIndex) <= _len * 0.5f) continue;
            // The line in from "pre" must be clear. The way from here to "pre" is not tested as a straight line (pads cluster, and a
            // straight line across a cluster is rarely clear): the feelers find a way round, and a swim that cannot is hopped out of.
            if (!LineClear(q, s, p.PadIndex, _len * 0.4f)) continue;
            stage = s; dirIn = d; pre = q;
            return true;
        }
        return false;
    }

    private void ReleaseGoal()
    {
        _owner.Free(_goalPerch, this);
        _goalPerch = null; _hasGoal = false; _skipPad = -1;
    }

    /// <summary>
    /// Chooses where the next swim goes: a free perch within 0.6u that it can
    /// get to (the nearer likelier), else, or if "perchOk" is false, a spot of
    /// open water not far off. The perch is held from now, so no other frog sets
    /// out for it.
    /// </summary>
    private void PlanGoal(bool perchOk)
    {
        ReleaseGoal();
        PointF here = Centre;
        if (perchOk)
        {
            var ps = _owner.Perches;
            int n = ps.Count, usable = 0;
            for (int i = 0; i < n; i++)
            {
                FrogPerch p = ps[i];
                _w[i] = 0;
                if ((p.Holder != null && p.Holder != this) || (p == _lastPerch && n > 1) || p == _perch || _bad[i] > 0) continue;
                float d = Dist(here, p.Centre);
                if (d > _u * (_wet > 20f ? 1.0f : 0.6f) || d < _len * 1.2f) continue;
                if (!StageFor(p, here, out PointF stage, out PointF dirIn, out PointF pre)) continue;
                _stageCache[i] = stage; _dirCache[i] = dirIn; _preCache[i] = pre;
                _w[i] = 1f / (0.15f + d / _u);
                usable++;
            }
            if (usable > 0)
            {
                int pick = Dice.Pick(_rng, _w);
                if (pick >= 0 && _owner.Take(ps[pick], this))
                {
                    _goalPerch = ps[pick]; _stage = _stageCache[pick]; _dirIn = _dirCache[pick]; _pre = _preCache[pick]; _lined = false;
                    _skipPad = _goalPerch.PadIndex;
                    _hasGoal = true;
                    return;
                }
            }
        }
        for (int tries = 0; tries < 40; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _u * (0.1f + 0.25f * (float)_rng.NextDouble());
            float gx = _x + MathF.Cos(a) * d, gy = _y + MathF.Sin(a) * d;
            PointF s = ToScreen(gx, gy);
            if (!WaterAt(s) || !OpenAt(s) || _pond.RoomAt(s.X, s.Y) < _len * 1.0f || Clear(s.X, s.Y, -1) < _len * 0.6f || !LineClear(here, s, -1, _len * 0.3f)) continue;
            _goalX = gx; _goalY = gy; _hasGoal = true; _skipPad = -1;
            return;
        }
    }

    // ================================================================ in the water

    private void BeginSwim()
    {
        _mind.Begin(Doing.Swim, 30f);
        _kickT = 0; _period = 0.9f + 0.2f * (float)_rng.NextDouble();
        _z = 0; _zTarget = 0; _rising = false; _speed = _u * 0.05f;
        _refused = 0; _blocked = false;
        if (!_hasGoal) PlanGoal(true);
        if (!_hasGoal) BeginFloat(3 + 3 * (float)_rng.NextDouble());
    }

    private void BeginFloat(float seconds)
    {
        _mind.Begin(Doing.Float, seconds);
        ReleaseGoal();
        _zTarget = 0;
    }

    private void BeginDive(PointF threat)
    {
        ReleaseGoal();
        _mind.Begin(Doing.Dive, 3.5f + 5 * (float)_rng.NextDouble());
        _zTarget = 0.2f + 0.1f * (float)_rng.NextDouble();
        _rising = false;
        _kickT = 0; _boost = 1f;
        PointF nose = Nose;
        _ripples.Add(nose.X, nose.Y, 0.3f);
        // A place to swim to under the water: away from the threat, and open (a dived frog is hidden by the pads, but should come up in the open).
        PointF here = Centre;
        float away = GroundHeading(here.X - threat.X, here.Y - threat.Y);
        float bestScore = float.MinValue; float bx = _x, by = _y; bool got = false;
        for (int k = 0; k < 14; k++)
        {
            float a = away + (k % 2 == 0 ? 1 : -1) * (k / 2) * 0.35f, d = _u * (0.12f + 0.12f * (float)_rng.NextDouble());
            float gx = _x + MathF.Cos(a) * d, gy = _y + MathF.Sin(a) * d;
            PointF s = ToScreen(gx, gy);
            if (!WaterAt(s) || !OpenAt(s) || _pond.RoomAt(s.X, s.Y) < _len || Clear(s.X, s.Y, -1) < _len * 0.5f) continue;
            float score = -MathF.Abs(a - away) + 0.4f * MathF.Sin(a);                  // away from it, and a little toward the bottom of the screen (a sink is easier going that way, Light.HoldDepth)
            if (score > bestScore) { bestScore = score; bx = gx; by = gy; got = true; }
        }
        if (got) { _goalX = bx; _goalY = by; _hasGoal = true; }
        Log?.Invoke($"dive z={_zTarget:F2}");
    }

    /// <summary>The deepest it may be: nine tenths of the shallowest bed under the nose, the middle and the rump.</summary>
    private float DepthCap()
    {
        float f = MathF.Min(FloorAt(Centre), MathF.Min(FloorAt(Spine[0]), FloorAt(Spine[Points - 1])));
        return 0.9f * f;
    }

    /// <summary>
    /// Swim, Float and Dive. A swim is a push and a glide, about one a second:
    /// the legs sweep out and back together in the first quarter second (the
    /// body surges), then fold forward slowly while it glides. A float lets the
    /// legs hang. The steering is the snake's: a gentle pull to the goal, six
    /// feelers along the heading, the smallest turn that clears an obstacle, and a
    /// step refused if it would be onto land or a pad; a refused step forces the
    /// avoidance turn next frame.
    /// </summary>
    private void SwimOrFloat(float dt)
    {
        bool floating = _doing == Doing.Float, diving = _doing == Doing.Dive;
        float vPush = _u * (diving ? 0.095f : 0.08f), vGlide = _u * 0.02f;

        // ---- the kick ----
        if (floating)
        {
            _speed *= MathF.Exp(-dt * 3f);
            if (_speed < _u * 0.0003f) _speed = 0;
            _poseWant = 0.5f + 0.07f * MathF.Sin((float)(_time % 1000.0) * 1.3f);
            _kickT = 0;
        }
        else
        {
            _kickT += dt;
            if (_kickT >= _period) { _kickT -= _period; _period = 0.85f + 0.3f * (float)_rng.NextDouble(); }
            float c = _kickT;
            float kick = c < 0.25f ? Smooth(c / 0.25f) : MathF.Exp(-(c - 0.25f) * 3.5f);
            _boost = MathF.Max(0, _boost - dt / 0.9f);
            _speed = vGlide + (vPush - vGlide) * kick + _u * 0.07f * _boost;
            _poseWant = c < 0.25f ? Smooth(c / 0.25f) : c < 0.9f ? 1 - Smooth((c - 0.25f) / 0.65f) : 0f;
        }

        // ---- the doing's own business ----
        PointF here = Centre;
        if (floating)
        {
            if (_mind.Over) { FloatEnds(); return; }
        }
        else if (_doing == Doing.Swim)
        {
            if (_mind.Over) { Log?.Invoke("swim gave up"); if (_goalPerch != null) _bad[_goalPerch.Index] = 90f; BeginFloat(2 + 2 * (float)_rng.NextDouble()); return; }
        }
        else if (diving)
        {
            if (!_rising && _mind.Over) { _rising = true; _zTarget = 0; }
            if (_rising && _z < 0.03f)
            {
                PointF nose = Nose;
                _ripples.Add(nose.X, nose.Y, 0.15f);                                // the eyes break the surface: a tiny ring
                Log?.Invoke("surface");
                BeginFloat(2 + 3 * (float)_rng.NextDouble());
                return;
            }
        }

        // ---- depth ----
        UpdateDepth(dt, diving);

        // ---- steering ----
        float turn = 0, most = 1.2f;
        if (!floating)
        {
            bool approach = _doing == Doing.Swim && _goalPerch != null;
            float gx, gy;
            (float sgx, float sgy) = ToGround(_stage);
            float dStage = MathF.Sqrt((sgx - _x) * (sgx - _x) + (sgy - _y) * (sgy - _y));
            if (approach)
            {
                // First out to "pre"; once there (or if it is already lined up and close in) along the line to the stage.
                (float pgx, float pgy) = ToGround(_pre);
                if (!_lined && MathF.Sqrt((pgx - _x) * (pgx - _x) + (pgy - _y) * (pgy - _y)) < _len * 0.8f) _lined = true;
                if (_lined && dStage > _len * 3.6f) _lined = false;                       // swept off the line: back out to line up again
                if (_lined) { gx = sgx; gy = sgy; } else { gx = pgx; gy = pgy; }
            }
            else { gx = _goalX; gy = _goalY; }
            float dxg = gx - _x, dyg = gy - _y, dg = MathF.Sqrt(dxg * dxg + dyg * dyg);
            float aim = MathF.Atan2(dyg, dxg);
            bool near = false;
            if (approach)
            {
                float faceIn = GroundHeading(-_dirIn.X, -_dirIn.Y);
                near = _lined && dStage < _len * 2.5f;
                // Arrived, pointing at the perch, and at the surface: climb out.
                if (_lined && dStage < _len * 0.45f && MathF.Abs(Wrap(_heading - faceIn)) < 0.8f && _z < 0.02f) { BeginClimb(); return; }
            }
            else if (_hasGoal && dg < _u * 0.05f)
            {
                if (_doing == Doing.Swim) { BeginFloat(2 + 5 * (float)_rng.NextDouble()); return; }
                _goalX = _x + _fx * _u * 0.2f; _goalY = _y + _fy * _u * 0.2f;      // a dive: swim on, the goal is only a direction
            }
            if (approach && _lined) _speed *= Math.Clamp(0.4f + dStage / (_len * 3f), 0.4f, 1f);   // easing in to the perch

            {
                turn = Wrap(aim - _heading) * 1.4f;
                float push = _kickT < 0.25f ? 1f : 0.35f;
                most = (1.1f + 1.4f * push);
                // Feel for the smallest turn that clears something in the way.
                float look = _len * 2.2f + _speed * 0.5f, margin = _len * (near ? 0.12f : 0.55f);
                float clearAhead = _blocked ? 0 : ClearAlong(_heading, look);
                if (clearAhead < margin)
                {
                    float best = _heading, bestScore = MathF.Min(clearAhead, margin * 2);
                    for (int k = 1; k <= 5; k++)
                        foreach (int side in (ReadOnlySpan<int>)[1, -1])
                        {
                            float a = _heading + side * k * 0.35f;
                            float score = MathF.Min(ClearAlong(a, look), margin * 2) - k * margin * 0.15f;
                            if (score > bestScore) { bestScore = score; best = a; }
                        }
                    float urgency = Math.Clamp(1 - clearAhead / MathF.Max(1e-3f, margin), 0, 1);
                    turn += Wrap(best - _heading) * (1.5f + 3f * urgency);
                    most = MathF.Max(most, 2.4f);
                }
                // A step was refused a while ago (a bank in the way): turn toward more open water, as the turtle does.
                if (_refused > 0.4f)
                {
                    PointF open = _pond.OpenWay(here.X, here.Y);
                    if (open.X * open.X + open.Y * open.Y > 1e-6f) turn += Wrap(GroundHeading(open.X, open.Y) - _heading) * 3f;
                    most = MathF.Max(most, 3f);
                }
                // Other frogs in the water: room for each.
                foreach (Frog f in _owner.All)
                {
                    if (f == this || !f.InWater) continue;
                    PointF fc = f.Centre;
                    float d = Dist(here, fc);
                    if (d > _len * 2.2f || d < 1e-3f) continue;
                    float nearF = 1 - d / (_len * 2.2f);
                    turn += Wrap(GroundHeading(here.X - fc.X, here.Y - fc.Y) - _heading) * nearF * 2.5f;
                    most = MathF.Max(most, 2.0f);
                }
            }
        }
        // The rate of turn eases (a body has mass): the steering's sum flips from frame to frame.
        turn = Math.Clamp(turn, -most, most);
        _turnRate += (turn - _turnRate) * (1 - MathF.Exp(-dt * 6f));
        _heading = Wrap(_heading + _turnRate * dt);
        _fx = MathF.Cos(_heading); _fy = MathF.Sin(_heading);

        // ---- forward, unless the step would be onto land or a pad ----
        float step = _speed * dt;
        float nx = _x + _fx * step, ny = _y + _fy * step;
        PointF next = ToScreen(nx, ny);
        float clearNext = Clear(next.X, next.Y, _skipPad), clearNow = Clear(here.X, here.Y, _skipPad);
        bool ok = WaterAt(next) && (clearNext > 0 || clearNext >= clearNow);
        if (!ok && !WaterAt(here)) ok = clearNext >= clearNow;     // starting from the bank or a pad: any step toward more room
        _blocked = !ok;
        if (ok) { _x = nx; _y = ny; _refused = 0; }
        else
        {
            _refused += dt;
            if (_refused > 1.2f)
            {
                // Wedged (a gap between pads narrower than a frog, a bank): a frog does not stay and push; it hops out to open water.
                // The perch it was swimming to is left alone for a while. A dived frog comes up first (it cannot hop from under water).
                if (_goalPerch != null) _bad[_goalPerch.Index] = 90f;
                if (diving) { _rising = true; _zTarget = 0; _refused = 0; Log?.Invoke("dive stuck"); }
                else if (_doing == Doing.Swim)
                {
                    Log?.Invoke("swim stuck");
                    ReleaseGoal();
                    if (!BeginLeap(null, "stuck")) BeginFloat(1 + 2 * (float)_rng.NextDouble());
                    return;
                }
            }
        }
    }

    /// <summary>Float is over: swim to a perch (a frog gets out of the water), or to some open water, or dive.</summary>
    private void FloatEnds()
    {
        Span<float> w = stackalloc float[3];
        bool tired = _wet > 25f;               // it has been in the water a good while: it wants out
        w[0] = (2f + 2f * _restless) * (tired ? 3f : 1f);   // to a perch
        w[1] = tired ? 0.1f : 0.8f + 1.0f * _restless;      // to open water, for the swim of it
        w[2] = 0.25f;                                       // duck under
        switch (Dice.Pick(_rng, w))
        {
            case 2:
                BeginDive(Centre);
                break;
            case 1:
                PlanGoal(false);
                if (_hasGoal) BeginSwimFromWater(); else BeginFloat(3);
                break;
            default:
                PlanGoal(true);
                if (_hasGoal) BeginSwimFromWater(); else BeginFloat(3);
                break;
        }
    }

    private void BeginSwimFromWater()
    {
        _mind.Begin(Doing.Swim, 30f);
        _kickT = 0; _period = 0.9f + 0.2f * (float)_rng.NextDouble();
        _refused = 0; _blocked = false;
    }

    private float ClearAlong(float a, float dist)
    {
        float dx = MathF.Cos(a), dy = MathF.Sin(a), best = _u;
        foreach (float f in (ReadOnlySpan<float>)[0.08f, 0.2f, 0.35f, 0.55f, 0.78f, 1f])
        {
            PointF p = ToScreen(_x + dx * dist * f, _y + dy * dist * f);
            best = MathF.Min(best, Clear(p.X, p.Y, _skipPad));
        }
        return best;
    }

    /// <summary>
    /// Depth. Swimming and floating are at the surface. A dive sinks to its
    /// target (never within a tenth of the bed) and a rise brings it back. The
    /// change is held to what its own swimming can hide on the screen
    /// (Light.HoldDepth, the rule every swimmer here obeys), but never slower than
    /// a floor (0.12 a second down, 0.15 up): a startled frog vanishing is a quick thing, and the rule alone,
    /// for a frog heading up the screen, would take half a minute to sink it.
    /// </summary>
    private void UpdateDepth(float dt, bool diving)
    {
        float cap = DepthCap();
        float want = diving && !_rising ? MathF.Min(_zTarget, cap) : 0f;
        float zNext = _z + (want - _z) * (1 - MathF.Exp(-dt * 3f));
        float z = Light.HoldDepth(_z, zNext, _heading, MathF.Max(_speed, _u * 0.03f), dt, _u);
        if (zNext > _z) z = MathF.Max(z, MathF.Min(zNext, _z + 0.12f * dt));
        else if (zNext < _z) z = MathF.Min(z, MathF.Max(zNext, _z - 0.15f * dt));
        _z = Math.Clamp(z, 0f, MathF.Max(0f, cap));
        if (_z < 0.0005f) _z = 0;
    }

    // ================================================================ climbing out

    private void BeginClimb()
    {
        FrogPerch p = _goalPerch!;
        SitSpot(p, _dirIn, out PointF spot);
        (float gx, float gy) = ToGround(spot);
        float face = MathF.Atan2(gy - _y, gx - _x);
        _mind.Begin(Doing.Climb);
        _speed = 0; _z = 0;
        BeginHop(HopKind.Climb, _x, _y, gx, gy, 0.32f, 0.1f, 0.012f * _u, face);
        Log?.Invoke($"climb to={PerchLabel(p)}");
    }

    private static string PerchLabel(FrogPerch p) => p.Kind + ":" + p.Index;

    /// <summary>The hop is over: it is on the perch. It sits, and its first act is to turn to face the water.</summary>
    private void Arrive()
    {
        _perch = _goalPerch; _goalPerch = null; _hasGoal = false; _skipPad = -1;
        _faceOut = GroundHeading(_dirIn.X, _dirIn.Y);
        BeginSit();
        Log?.Invoke($"sat on={PerchName}");
    }

    // ================================================================ keeping two frogs apart

    /// <summary>Two frogs in the water whose middles are closer than 0.8 body lengths are each eased apart (half the way each, a body length a second at most).</summary>
    public static void KeepApart(Frog a, Frog b, float dt)
    {
        PointF pa = a.Centre, pb = b.Centre;
        float dx = pb.X - pa.X, dy = pb.Y - pa.Y, d = MathF.Sqrt(dx * dx + dy * dy), want = a._len * 0.8f;
        if (d >= want) return;
        if (d < 1e-3f) { dx = 1; dy = 0; d = 1; }
        float push = MathF.Min(a._len * dt, (want - d) * 0.5f);
        a.Shove(-dx / d * push, -dy / d * push);
        b.Shove(dx / d * push, dy / d * push);
    }

    private void Shove(float sx, float sy)
    {
        PointF to = Centre;
        to = new PointF(to.X + sx, to.Y + sy);
        if (!WaterAt(to)) return;
        (float gx, float gy) = ToGround(to);
        _x = gx; _y = gy;
    }

    // ================================================================ the body

    /// <summary>
    /// The straight spine from snout to rump. Each point is the middle of an edge
    /// pair, squashed to the screen, sunk for its depth and lifted for its
    /// height. The same points go into Shape (ground plane, depth in pixels) so
    /// the koi can feel the frog.
    /// </summary>
    private void LaySpine()
    {
        _fx = MathF.Cos(_heading); _fy = MathF.Sin(_heading);
        float zPx = _z * DepthPx, sink = Sink, lift = LiftPx;
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            PointF l = Raw(s, -1), r = Raw(s, 1);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2 + sink - lift);
            HalfWidth[i] = MathF.Max(0.5f, Dist(l, r) / 2);
            (float gx, float gy) = RawGround(s, 0);
            Shape.Points[i] = new System.Numerics.Vector3(gx, gy, zPx);
            Shape.Radius[i] = MathF.Max(Wid(s) * 1.05f, _u * 0.004f);
        }
        Shape.Seal();
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        int vi = Math.Clamp((int)((v + 1) * 0.5f * SkinV), 0, SkinV - 1);
        uint c = _skin[Math.Clamp((int)(s * SkinS), 0, SkinS - 1) * SkinV + vi];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        alpha = 1;
        return true;
    }

    /// <summary>
    /// The colours at every (s, v), worked out once: olive green, paler toward the
    /// flanks, a pale yellow-green line straight down the middle of the back,
    /// dark brown blotches (a different scatter for each frog), the whole thing
    /// shaded like a dome, lightest along the backbone.
    /// </summary>
    private static uint[] Skin(Random rng)
    {
        var skin = new uint[SkinS * SkinV];
        float p1 = (float)(rng.NextDouble() * MathF.Tau), p2 = (float)(rng.NextDouble() * MathF.Tau), p3 = (float)(rng.NextDouble() * MathF.Tau);
        float tone = (float)rng.NextDouble();                                // a little variety: greener or browner
        for (int si = 0; si < SkinS; si++)
            for (int vi = 0; vi < SkinV; vi++)
            {
                float s = (si + 0.5f) / SkinS, v = (vi + 0.5f) / SkinV * 2 - 1, av = MathF.Abs(v);
                (float R, float G, float B) c = (100 + 14 * tone, 128 - 10 * tone, 56);
                if (s < 0.3f) c = Mix(c, (118, 142, 64), 0.5f);
                float flank = Math.Clamp((av - 0.62f) / 0.3f, 0, 1);
                c = Mix(c, (158, 178, 94), flank * 0.75f);
                if (s > 0.2f && av < 0.85f)
                {
                    float n = MathF.Sin(s * 24 + p1) * MathF.Sin(v * 9 + p2) + 0.5f * MathF.Sin(s * 13 - v * 7 + p3);
                    float blot = Smooth((n - 0.25f) / 0.35f) * (1 - flank * 0.6f);
                    c = Mix(c, (62, 48, 26), blot * 0.8f);
                }
                if (s > 0.1f && s < 0.97f)
                {
                    float line = Smooth(1 - av / 0.085f);
                    c = Mix(c, (198, 210, 114), line * 0.9f);
                }
                float dome = 0.72f + 0.28f * MathF.Sqrt(MathF.Max(0, 1 - v * v));
                skin[si * SkinV + vi] = (uint)(Math.Min(255, (int)(c.R * dome)) << 16 | Math.Min(255, (int)(c.G * dome)) << 8 | Math.Min(255, (int)(c.B * dome)));
            }
        return skin;
    }

    private static (float, float, float) Mix((float R, float G, float B) a, (float R, float G, float B) b, float k) =>
        (a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k);

    // ================================================================ drawing

    /// <summary>
    /// Paints the frog: its shadow if it is up in the air or on a perch; the
    /// throat peeking out under the chin; the hind legs and forelegs (first, so
    /// the body covers where they join it); the body; and the eyes, two bulging
    /// domes on top of the head with a pale rim, which from above is the most
    /// frog thing about it. Painted over the whole open view when it is at the
    /// surface or in the air (a pad is not water), and over the water only when
    /// it is under the surface, so a pad hides a dived frog.
    /// </summary>
    public void Draw(FrameBuffer fb)
    {
        bool air = _hop != HopKind.None || _doing == Doing.Sit;
        float murk = air ? 0f : Light.Murk(_z);
        if (murk > 0.985f) return;
        bool under = _z > 0.03f;
        bool[] stencil = under ? _where : _pond.Open;
        float soft = MathF.Max(1f, _u * 0.0012f) * (1 + 3 * _z);
        float L = _len * Sc;
        bool floating = _doing == Doing.Float && _hop == HopKind.None;
        float legMurk = floating ? MathF.Max(murk, 0.5f) : murk;

        // The shadow: the same body, dark and soft, down and to the right by the height (the pond's rule, Light.cs).
        if (air && _height * _u > 0.6f)
        {
            float dx = Light.ShadowX * _height * _u, dy = (Light.Lift + Light.ShadowY) * _height * _u;
            bool flying = _hopHeight * _u > 0.6f;
            PaintShadow(fb, flying ? _pond.Open : _shadowMask, dx, dy, 0.38f * MathF.Min(1f, _height / 0.004f), soft * 1.6f);
        }

        // The throat: a pale oval under the chin whose size pulses in a croak.
        if (_doing == Doing.Sit)
        {
            (float tx, float ty) = RawGround(-0.04f, 0);
            PointF t = Drawn_(tx, ty);
            float tr = L * (0.05f + 0.1f * _throat);
            Oval(fb, stencil, t, 0, tr, tr * Ripples.Squash, 222, 214, 150, 0.92f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
        }

        HindLeg(fb, stencil, 1, L, legMurk);
        HindLeg(fb, stencil, -1, L, legMurk);
        ForeLeg(fb, stencil, 1, L, legMurk);
        ForeLeg(fb, stencil, -1, L, legMurk);

        Paint(fb, stencil, _pond.Sun, murk, soft, _pond.Pixels);

        // The eyes.
        float rr = MathF.Max(1.4f, L * 0.09f), rd = rr * 0.72f;
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            (float ex, float ey) = RawGround(0.2f, 0.62f * side);
            PointF e = Drawn_(ex, ey);
            e.Y -= L * 0.015f;                                                       // the dome stands up from the head
            Oval(fb, stencil, e, 0, rr, rr * 0.9f, 200, 208, 142, 0.95f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
            Oval(fb, stencil, e, 0, rd, rd * 0.9f, 28, 30, 18, 0.97f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
            if (rr > 2.4f) Oval(fb, stencil, new PointF(e.X - rr * 0.22f, e.Y - rr * 0.25f), 0, rr * 0.2f, rr * 0.2f, 190, 196, 168, 0.8f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
        }
    }

    /// <summary>One oval limb segment from screen point a to screen point b, "wid" pixels half-wide.</summary>
    private void Limb(FrameBuffer fb, bool[] stencil, PointF a, PointF b, float wid, int r, int g, int bl, float murk)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        var c = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        Oval(fb, stencil, c, MathF.Atan2(dy, dx), d / 2 + wid * 0.6f, MathF.Max(0.7f, wid), r, g, bl, 1f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
    }

    /// <summary>
    /// A hind leg. Folded (pose 0) it lies in a Z at the side: the thigh runs
    /// forward and out from the hip so the knee sticks out beside the body, the
    /// shin comes back, and the long foot is tucked forward. Stretched (pose 1) it
    /// is straight out behind with the webbed foot opened. In between, the
    /// segments' directions are simply mixed, so nothing snaps. Afloat the
    /// legs hang spread (the splay widens everything outward).
    /// </summary>
    private void HindLeg(FrameBuffer fb, bool[] stencil, int side, float L, float murk)
    {
        float p = Smooth(_pose);
        float splay = _doing == Doing.Float ? 1.5f : 1f;
        (float hx, float hy) = RawGround(0.70f, 0.6f * side);
        float ox = -_fy * side, oy = _fx * side;                                  // the unit vector out to this side, ground plane
        float tf = Lerp(0.17f, -0.36f, p) * L, to = Lerp(0.24f, 0.07f, p) * splay * L;
        float sf = Lerp(-0.30f, -0.40f, p) * L, so = Lerp(-0.04f, 0.04f, p) * splay * L;
        float ff = Lerp(0.20f, -0.20f, p) * L, fo = Lerp(0.03f, 0.08f, p) * splay * L;
        float kx = hx + _fx * tf + ox * to, ky = hy + _fy * tf + oy * to;
        float ax = kx + _fx * sf + ox * so, ay = ky + _fy * sf + oy * so;
        float fx = ax + _fx * ff + ox * fo, fy = ay + _fy * ff + oy * fo;
        PointF hip = Drawn_(hx, hy), knee = Drawn_(kx, ky), ankle = Drawn_(ax, ay), toe = Drawn_(fx, fy);
        Limb(fb, stencil, ankle, toe, L * (0.05f + 0.045f * p), 142, 154, 84, murk);     // the foot: paler, and wider when the web opens
        Limb(fb, stencil, knee, ankle, L * 0.06f, 86, 108, 46, murk);                    // the shin
        Limb(fb, stencil, hip, knee, L * 0.1f, 96, 118, 50, murk);                       // the thigh: chunky
    }

    /// <summary>A foreleg: short, from the shoulder, forward and out when it sits, tucked back along the body in a leap.</summary>
    private void ForeLeg(FrameBuffer fb, bool[] stencil, int side, float L, float murk)
    {
        float p = Smooth(_pose);
        (float sx, float sy) = RawGround(0.24f, 0.7f * side);
        float ox = -_fy * side, oy = _fx * side;
        float f = Lerp(0.17f, -0.02f, p) * L, o = Lerp(0.10f, 0.02f, p) * L;
        float wx = sx + _fx * f + ox * o, wy = sy + _fy * f + oy * o;
        Limb(fb, stencil, Drawn_(sx, sy), Drawn_(wx, wy), L * 0.045f, 112, 134, 62, murk);
    }

    // ================================================================ (kept for the diving frog's goal)
}
