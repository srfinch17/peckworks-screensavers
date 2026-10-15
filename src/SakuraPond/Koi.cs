using System.Numerics;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The koi in the pond: a handful of fish, each drawn along its own bending
/// spine (Body.cs), deepest first so a fish near the surface swims over one
/// lower down. Their personalities are dealt here (Core/Minds Traits.Deal),
/// so the school is a spread of natures and no two fish are near-twins.
///
/// The school also keeps the fish OUT of each other (and out of the turtles):
/// after every fish has taken its step, any two bodies that overlap in three
/// dimensions are pushed apart (Core/Minds Chain.Touch) and told they bumped.
/// The fish steer round each other first (their feelers see every other body,
/// Koi.RoomAround), so the push is the last resort, for the moment two really
/// meet; it is what makes "they cannot swim through each other" a fact rather
/// than a hope.
/// </summary>
internal sealed class KoiSchool
{
    private readonly Koi[] _fish;
    private readonly Pond _pond;
    private Body[] _bodies = [];                          // every body in the water this frame: the fish, then the turtles that are in

    /// <summary>Only an animal this much deeper than another receives its shadow: a pair swimming side by side at one depth throw none on each other.</summary>
    private const float ShadowGap = 0.05f;

    /// <param name="play">The "Koi play" knob: 1 = normal, 0 = the fish only wander and rest, 3 = three times as playful.</param>
    /// <param name="petals">The floating petals, for the fish to nose at and eat.</param>
    public KoiSchool(Pond pond, int count, Ripples ripples, bool[] where, Random rng, float play = 1, Petals? petals = null)
    {
        _pond = pond;
        _fish = new Koi[Math.Max(0, count)];
        float[] energy = Traits.Deal(rng, _fish.Length), social = Traits.Deal(rng, _fish.Length), curious = Traits.Deal(rng, _fish.Length);
        for (int i = 0; i < _fish.Length; i++)
            _fish[i] = new Koi(pond, ripples, where, rng, i, (energy[i], social[i], curious[i]), play, petals);
        Room(_fish.Length);
    }

    /// <summary>Scratch for the draw order big enough for n actors (the fish, and the turtles once Draw sees them). Grows once, never per frame.</summary>
    private void Room(int n)
    {
        if (_order.Length >= n) return;
        _order = new int[n]; _layer = new int[n]; _depth = new float[n]; _box = new RectangleF[n];
        _layers = new List<int>[Math.Max(1, n)];
        for (int i = 0; i < _layers.Length; i++) _layers[i] = new List<int>(n);
    }

    /// <param name="turtles">The turtles, so the fish steer round them and are pushed off them (a turtle is the heavier, and is not moved).</param>
    public void Update(float dt, Turtles? turtles = null)
    {
        // The bodies in the water this frame: the fish, and every turtle that is in (not up on a perch).
        IReadOnlyList<Turtle> ts = turtles?.All ?? [];
        int n = _fish.Length, count = n;
        foreach (Turtle t in ts) if (t.InWater) count++;
        if (_bodies.Length != count) _bodies = new Body[count];
        for (int i = 0; i < n; i++) _bodies[i] = _fish[i];
        for (int i = 0, k = n; i < ts.Count; i++) if (ts[i].InWater) _bodies[k++] = ts[i];

        foreach (Koi k in _fish) k.Update(dt, _fish, _bodies);

        // Nobody inside anybody: two fish that overlap are each pushed half
        // the way out along the line between their closest parts; a fish in a
        // turtle is pushed the whole way (the turtle is the heavier). Then
        // both are told, and a shy fish swims off (Koi.Bumped). The push is a
        // RECOIL, not a jump: no faster than a fish swims (a body length a
        // second each), so a bump is a bump, two fish meeting and giving way
        // over a few frames, and their edges may overlap by a few pixels for
        // a fraction of a second while they do. Shoved out in one frame they
        // hopped sideways, which the eye reads as a slide. Two passes, since
        // a fish pushed out of one may be pushed into another (the second
        // only when the first found a touch).
        bool touched = false;
        for (int pass = 0; pass < 2 && (pass == 0 || touched); pass++)
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < count; j++)
                {
                    if (!Chain.Touch(_bodies[i].Shape, _bodies[j].Shape, out Vector3 push, out float depth)) continue;
                    touched = true;
                    Koi a = _fish[i];
                    Koi? b = j < n ? _fish[j] : null;
                    float most = a.Length * dt;                                     // the furthest either may be shoved this frame
                    Shove(a, b, push, depth, most);
                    // Still inside each other, and the push was up or down? The bed
                    // or the surface refused it (two fish crossing in the shallows,
                    // both on the bed, were shoved apart and clamped straight back
                    // 400 frames running). Then shove sideways instead, along the
                    // line between their middles.
                    if (MathF.Abs(push.Z) > 0.7f && Chain.Touch(_bodies[i].Shape, _bodies[j].Shape, out _, out float left) && left > 1)
                    {
                        Vector3 side = a.Centre - (b?.Centre ?? _bodies[j].Shape.Points[_bodies[j].Shape.Points.Length / 2]);
                        side.Z = 0;
                        float l = side.Length();
                        side = l > 1e-3f ? side / l : new Vector3(-MathF.Sin(a.Heading), MathF.Cos(a.Heading), 0);   // middles on one spot: a's own sideways
                        Shove(a, b, side, left, most);
                    }
                    if (pass == 0) { a.Bumped(push); b?.Bumped(-push); }
                }
    }

    /// <summary>Pushes a out of b by "depth" along "push", no more than "most" each: half each for two fish, the whole way for a fish in a turtle (b null: the heavier is not moved).</summary>
    private static void Shove(Koi a, Koi? b, Vector3 push, float depth, float most)
    {
        if (b != null) { float d = MathF.Min(most, depth * 0.5f); a.Nudge(push * d); b.Nudge(-push * d); }
        else a.Nudge(push * MathF.Min(most, depth));
    }

    /// <summary>Something tapped the water here (screen pixels): curious fish nearby come to look.</summary>
    public void Notice(float x, float y)
    {
        foreach (Koi k in _fish) k.Notice(x, y);
    }

    /// <summary>A shadow swept over the water here (screen pixels): fish near it dart away and dive.</summary>
    public void Startle(float x, float y)
    {
        foreach (Koi k in _fish) k.Startle(x, y);
    }

    /// <summary>Each fish's doing this frame, for logs and tests.</summary>
    public IEnumerable<(PointF Head, PointF Tail, string State)> States => _fish.Select(f => (f.Head, f.Tail, f.State));

    /// <summary>
    /// Two points along each koi that is near enough the surface to be met by
    /// anything on it (its head and its middle), screen pixels: the minnows
    /// and the snake keep clear of them. A fish deep down is out of their reach.
    /// </summary>
    public PointF[] Heads => _fish.Where(f => Light.DepthOf(f.Z) < 0.06f).SelectMany(f => new[] { f.Head, f.Middle }).ToArray();

    // Scratch for Draw, kept between frames so drawing allocates nothing (see Room).
    private int[] _order = [], _layer = [];
    private float[] _depth = [];
    private RectangleF[] _box = [];
    private List<int>[] _layers = [];
    private FrameBuffer? _fb;
    private IReadOnlyList<Turtle>? _turtles;
    private bool[] _mask = [];                            // where the animal being drawn painted itself (a stencil for the shadows that land on it), cleared again after

    /// <summary>
    /// Draws the fish deepest first, so a fish nearer the surface covers one
    /// below it. First every fish's shadow on the bed, under all of them (they
    /// lie on the bed). Fish that do not touch cannot paint over each other,
    /// so they are drawn at the same time on different processor cores. Each
    /// fish is put in a LAYER: layer 0 if its box touches no deeper fish,
    /// otherwise one above the deepest-drawn fish it touches. Layer 0 is drawn
    /// first, all its fish at once, then layer 1, and so on. (The first
    /// version started a new group whenever a fish touched any fish in the
    /// group, which with eight fish often made four or five small groups: more
    /// waits for the cores to finish, and less for them to do each time.)
    /// The picture comes out exactly as if drawn one by one.
    ///
    /// The turtles are dealt into the same order by THEIR depth (a turtle keeps
    /// near the top, but a koi gulping at the surface is above it, and drawn
    /// after every koi a turtle's shell once read as a marking on a shallower
    /// fish's back), so each one is drawn after the koi deeper than it and
    /// before the koi shallower than it.
    ///
    /// WHAT SAYS "OVER", NOT "THROUGH": as each animal is drawn, the shadow of
    /// every shallower one whose box overlaps it is thrown onto its back
    /// (CastShadow), down and right by the depth between them and softer the
    /// bigger that gap. Seen from above, a fish passing over another is a fish
    /// with a soft dark fish-shape sliding across its back; without that, two
    /// hazy pictures crossing read as one swimming through the other. The
    /// animal marks the pixels it painted in _mask as it goes, so the shadow
    /// lands on its body and nowhere else (not on the water beside it, not on
    /// the fish below it, whose own turn had its own shadows).
    /// </summary>
    public void Draw(FrameBuffer fb, Turtles? turtles = null)
    {
        int n = _fish.Length;
        IReadOnlyList<Turtle> ts = turtles?.All ?? [];
        int t = ts.Count, all = n + t;
        if (all == 0) return;
        Room(all);
        if (_mask.Length != fb.Width * fb.Height) _mask = new bool[fb.Width * fb.Height];
        foreach (Koi k in _fish) k.DrawShadow(fb);
        for (int i = 0; i < n; i++) { _order[i] = i; _depth[i] = -_fish[i].Z; _box[i] = _fish[i].Box; }
        for (int i = 0; i < t; i++) { _order[n + i] = n + i; _depth[n + i] = ts[i].InWater ? -ts[i].Z : 1f; _box[n + i] = ts[i].Box; }   // a turtle out on a perch is not in this order at all (Turtles.DrawOut paints it over the water's surface)
        Array.Sort(_depth, _order, 0, all);                         // deepest first
        int layers = 0;
        for (int a = 0; a < all; a++)
        {
            int ia = _order[a], layer = 0;
            for (int b = 0; b < a; b++)
                if (_box[ia].IntersectsWith(_box[_order[b]])) layer = Math.Max(layer, _layer[_order[b]] + 1);
            _layer[ia] = layer;
            layers = Math.Max(layers, layer + 1);
        }
        _fb = fb;
        _turtles = ts;
        for (int l = 0; l < layers; l++)
        {
            List<int> group = _layers[l];
            group.Clear();
            for (int a = 0; a < all; a++) if (_layer[_order[a]] == l) group.Add(_order[a]);
            if (group.Count == 1) DrawOne(group[0], fb);
            else Parallel.For(0, group.Count, i => DrawOne(group[i], _fb!));
        }
    }

    /// <summary>
    /// One actor of the draw order: a fish by its index, or a turtle past the
    /// fish. Drawn with the mask marking its pixels, then every shallower
    /// animal overlapping its box throws its shadow onto those pixels, then
    /// the mask is wiped inside the box. Two animals drawn at the same time on
    /// different cores have boxes that do not touch, so each marks and wipes
    /// its own patch of the mask and the shadows are clipped to it.
    /// </summary>
    private void DrawOne(int i, FrameBuffer fb)
    {
        int n = _fish.Length;
        if (i >= n && !_turtles![i - n].InWater) return;                 // up on a perch: painted later, over everything on the water
        Rectangle box = Rectangle.Intersect(Rectangle.Round(_box[i]), new Rectangle(0, 0, fb.Width, fb.Height));
        if (box.Width <= 0 || box.Height <= 0) return;
        float z = i < n ? _fish[i].Z : _turtles![i - n].Z;
        if (i < n) _fish[i].Draw(fb, _mask); else _turtles![i - n].Draw(fb, _mask);
        {
            // Sunlight on its back is what the shallower fish takes away: nothing to
            // take under a branch's shade, and less the deeper this one is (the water
            // above it softens everything, its shadows included).
            PointF mid = i < n ? _fish[i].Middle : _turtles![i - n].Middle;
            int under = Math.Clamp((int)mid.Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)mid.X, 0, _pond.Width - 1);
            float lit = Math.Clamp((_pond.Sun[under] - 154) / 101f, 0, 1);
            float alpha = 0.55f * lit * (1 - 0.35f * z);
            if (alpha > 0.02f)
                for (int k = 0; k < n + _turtles!.Count; k++)
                {
                    if (k == i) continue;
                    float zk = k < n ? _fish[k].Z : _turtles[k - n].Z;
                    if (zk > z - ShadowGap) continue;                      // only those above it
                    if (k >= n && !_turtles[k - n].InWater) continue;      // a turtle out on a rock shades the rock, not the fish
                    if (!_box[k].IntersectsWith(_box[i])) continue;
                    float gap = (z - zk) * Pond.DepthU * _pond.U;
                    Body who = k < n ? _fish[k] : _turtles[k - n];
                    who.CastShadow(fb, _mask, box, gap, alpha, 0.018f * _pond.U);   // held within about a body width of the caster
                }
        }
        for (int y = box.Top; y < box.Bottom; y++) Array.Clear(_mask, y * fb.Width + box.Left, box.Width);
    }
}

/// <summary>
/// One koi, seen from above.
///
/// WHAT A KOI DOES (watch any garden pond): it cruises slowly, a few lazy
/// beats of the tail and then a long glide with the tail almost still, and
/// does it again. It turns in smooth curves, its whole body bending into the
/// turn. Its side fins scull and brake. It drifts up and down in the water,
/// paler and hazier when deep, sharp and bright near the top, and now and
/// then it comes right up and gulps at the surface, leaving a ring. It keeps
/// away from the edges and from other fish without fuss.
///
/// HOW THE BODY BENDS: the head swims a path, and the body follows the path
/// the head has ALREADY swum, the way the carriages of a train follow the
/// track the engine took. We remember where the head has been (a trail of
/// points), and lay the spine along that trail. So a turn bends the body
/// exactly through the curve. On top of that a swimming wave runs down the
/// body: the head barely moves, the tail swings widest, the way a fish's
/// body pushes against the water. Nothing is ever flipped or slid.
///
/// THE SLANT: we look down at the pond at an angle, so things lying on the
/// water look a little flatter top to bottom (Ripples.Squash). The fish
/// lives and moves in "ground" coordinates (as if seen straight down), and
/// each edge point is squashed to the screen only when drawn. So a koi
/// swimming toward the top of the screen looks a little shorter than one
/// swimming sideways, as it should.
///
/// THREE DIMENSIONS: the fish is at (_x, _y) on the water's plane and Z below
/// it (0 the surface, 1 the pond's full depth, Pond.DepthU). It may be at any
/// depth the bed allows, and the deeper it is the smaller it is drawn
/// (perspective, Light.Scale), the hazier (Light.Murk) and the lower on the
/// screen (Light.Sink), until past Light.SeeU it is lost in the water the way
/// the bed is. It steers in three dimensions too: its feelers (Core/Minds
/// Feelers) see the bed, the surface and every other body in the water, and
/// when something is in the way it takes the smallest change of course that
/// clears it: a turn, a climb over, or a dive under. If two fish still meet,
/// KoiSchool pushes them apart and each swims shyly off.
/// </summary>
internal sealed class Koi : Body
{
    private const int Points = 22;
    private const int PatLen = 128, PatWide = 32;         // the markings' map: steps along the body, steps across
    private const int SkinLen = 256, SkinWide = 64;       // the finished colour sheet: twice as fine, edges already smoothed

    // How much light the rounded body catches at each step across it (the
    // backbone brightest, the flanks falling away): worked out once for all fish.
    private static readonly float[] Round = Enumerable.Range(0, SkinWide)
        .Select(i => { float v = (i + 0.5f) / SkinWide * 2 - 1; return 0.7f + 0.3f * MathF.Sqrt(MathF.Max(0, 1 - v * v)); }).ToArray();
    private const float FinEnd = 1.3f;                    // the tail fin reaches 30% of a body length past the tail stalk

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _where;
    private readonly Random _rng;
    private readonly float _len, _wide;                   // body length and half its widest width, ground pixels
    private readonly float _cruise;                       // this fish's beat speed, body lengths a second
    private readonly float _w1, _w2, _p1, _p2;            // its own two wander waves
    private readonly byte[] _pattern;                     // which colour at each (s, v): 128 steps along, 32 across
    private readonly (int R, int G, int B)[] _palette;
    private readonly (int R, int G, int B) _fin;
    private readonly float _finAlpha;
    private readonly bool _metal;
    private readonly uint[] _skin;                        // the body's finished colours, 0x00RRGGBB, SkinLen along by SkinWide across
    private readonly uint[] _skinBlur;                    // the same sheet blurred: what the markings look like through a depth of water

    private float _x, _y, _heading, _speed, _amp, _phase;
    private bool _beating;                                // the beat-and-glide rhythm's own clock: beating now?
    private bool _swimming;                               // actually beating this frame (the rhythm, overruled by the mind: a chase beats, a rest glides)
    private float _beatTimer, _depthTimer, _gulpTimer, _zTarget;
    private bool _gulping;
    private float _bumpTimer, _bumpGap;                    // the back-bump: when next, and the gap between its rings
    private bool _bumping;
    private int _bumpRings;
    private float _goalX, _goalY, _goalTimer;            // where it is idly heading, ground pixels, and until when
    private double _time;

    // ---- THE MIND ----
    // PERSONALITY: three numbers rolled once, 0 to 1, that every decision reads.
    // Think of three dials on each fish. Energy: how much of its time it spends
    // beating rather than gliding, and how fast it cruises. Sociable: how much
    // it likes company (follows a friend, starts a chase). Curious: how often
    // it goes to look at things (a petal, a lily pad's edge, a dragonfly's tap).
    private readonly float _energy, _social, _curious;
    private readonly float _play;                         // the "Koi play" knob: 1 = normal, 0 = only wander and rest
    private readonly Petals? _petals;                     // the floating petals, to nose at and eat

    /// <summary>What a koi can be doing. One at a time; Decide() picks the next when the current one ends.</summary>
    private enum Doing
    {
        Wander,        // idle: a spot somewhere in the pond, a slow cruise toward it
        Rest,          // hang deep and still, tail barely moving
        Investigate,   // go and nose at something: a petal, a pad's rim, the cattails, the log, a tap on the water
        Follow,        // swim behind and beside a friend
        Chase,         // a short burst after another fish
        Flee,          // being chased: speed up and swerve away
        Flinch,        // a shadow swept over it (a swallow skimming): a dart away and a dive, then it rests deep
        Shy,           // it bumped into another animal: a short swim away from it, up or down as well as aside
    }
    private readonly Mind<Doing> _mind = new();           // the doing now and how long it may last (Core/Minds)
    private Doing _doing => _mind.Doing;
    private float _targetX, _targetY;                     // what it is investigating, ground pixels
    private bool _targetIsPetal;                          // a petal drifts: the target is refreshed each frame
    private float _hold;                                  // seconds left nosing at the thing it reached (0 = still on the way)
    private Koi? _partner;                                // the fish it follows, chases or flees
    private int _side;                                    // which side of the partner it follows on (+1 or -1)
    private readonly Cooldown _chaseCool = new();         // no chase again until this has run out
    private readonly Cooldown _flinchCool = new();        // no flinch at a shadow again until this has run out
    private readonly Cooldown _shyCool = new();           // no shying from a bump again until this has run out (two fish side by side touch every frame)
    private float _flinchX, _flinchY;                     // where the shadow was, ground pixels
    private float _shyX, _shyY;                           // where to swim to get away from the thing it bumped, ground pixels
    private float _stuckFor;                              // seconds its steps have been refused (nosed against something)
    private float _dodge;                                 // the climb (negative) or dive (positive) its feelers chose this frame, z per unit forward; 0 = holding its depth
    private Body[] _bodies = [];                          // every body in the water this frame (the school and the turtles), for the feelers
    private readonly Feelers.Room _room;                  // RoomAround as a delegate, made once (a new one every frame would be garbage)

    /// <summary>The doing now, for logs and tests (never read by the drawing).</summary>
    public string State => _doing.ToString();

    /// <summary>Which way it is swimming, ground radians (for a sideways shove when nothing else gives a direction).</summary>
    public float Heading => _heading;

    /// <summary>Its body length, ground pixels (the school's shove is held to a length a second).</summary>
    public float Length => _len;

    // The trail the head has swum, newest last, in ground pixels. A ring
    // buffer: a fixed row of slots used round and round, so remembering never
    // allocates memory while the screensaver runs.
    private readonly PointF[] _trail = new PointF[96];
    private int _trailHead, _trailCount;

    /// <summary>How deep it is now: 0 at the surface, 1 as deep as a fish goes (Pond.DepthU below the surface).</summary>
    public float Z { get; private set; }

    private float _floor = 1;                             // the bed under the whole body, 0 at the bank to 1 at full depth (found as the spine is laid)
    private float _shadowDx, _shadowDy, _shadowSoft;      // its shadow on the bed: how far down and right of the body, and how blurred (screen pixels)
    private float _shadowAlpha;                           // how dark that shadow is (0 = the bed is out of sight, no shadow)
    private float _scale = 1;                             // how much smaller it is drawn for its depth (Light.Scale), set as the spine is laid
    private int _blur256, _dim256 = 256;                  // how far its markings are blurred toward _skinBlur, and how much dimmer, both by depth (256ths)

    /// <summary>Its nose as drawn, screen pixels (the picture of a deep fish sits lower on the screen than the water above it; see Light.Sink).</summary>
    public PointF Head => Spine[0];

    /// <summary>The middle of its body, screen pixels.</summary>
    public PointF Middle => Spine[8];

    /// <summary>The base of its tail, screen pixels (with the head, it says which way the body points).</summary>
    public PointF Tail => Spine[17];

    /// <summary>Just behind the head, screen pixels (head to neck is the way the nose points).</summary>
    public PointF Neck => Spine[2];

    /// <summary>The screen box the fish (fins and all) can paint in this frame.</summary>
    public RectangleF Box
    {
        get
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (PointF p in Spine) { x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y); x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y); }
            float m = _wide * 1.3f + _len * 0.25f + _pond.U * 0.01f;      // body width, side fins, soft edge (the bed shadow is drawn separately, before any fish)
            return RectangleF.FromLTRB(x0 - m, y0 - m, x1 + m, y1 + m);
        }
    }

    /// <param name="dials">Its personality, each 0 to 1: energy, sociability, curiosity (dealt by KoiSchool so the school is a spread).</param>
    public Koi(Pond pond, Ripples ripples, bool[] where, Random rng, int index, (float Energy, float Social, float Curious) dials, float play = 1, Petals? petals = null) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _where = where;
        _rng = rng;
        _play = play;
        _petals = petals;
        _room = RoomAround;
        float u = pond.U;
        _len = u * (0.095f + 0.06f * (float)rng.NextDouble());
        _wide = _len * (0.12f + 0.02f * (float)rng.NextDouble());
        // The personality dials. Each is dealt on its own, so a fish can be
        // lazy and sociable, or energetic and solitary: no two alike.
        (_energy, _social, _curious) = dials;
        _cruise = 0.45f + 0.35f * _energy;                // an energetic fish cruises faster
        _chaseCool.Start(30 + 90 * (float)rng.NextDouble());   // nobody chases in the first half minute
        _w1 = 0.11f + 0.12f * (float)rng.NextDouble(); _w2 = 0.31f + 0.2f * (float)rng.NextDouble();
        _p1 = (float)(rng.NextDouble() * MathF.Tau); _p2 = (float)(rng.NextDouble() * MathF.Tau);
        (_palette, _pattern, _fin, _finAlpha, _metal) = Variety(index, rng);
        _skin = Skin();
        _skinBlur = Blur(_skin);

        // Along each spine point: 18 on the body, 4 on the tail fin.
        for (int i = 0; i < Points; i++)
            Along[i] = i < 18 ? i / 17f : 1 + (i - 17) / 4f * (FinEnd - 1);

        // A start in open water, well away from the bank, facing anywhere.
        for (int tries = 0; tries < 500; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _len * 1.4f) continue;
            (_x, _y) = ToGround(x, y);
            break;
        }
        _heading = (float)(rng.NextDouble() * MathF.Tau);
        _speed = _len * 0.3f;
        _beating = rng.Next(2) == 0;
        _beatTimer = (float)rng.NextDouble() * 2;
        Z = 0.1f + 0.3f * (float)rng.NextDouble();
        _zTarget = Z;
        _depthTimer = 3 + (float)rng.NextDouble() * 8;
        _gulpTimer = 10 + (float)rng.NextDouble() * 60;
        _bumpTimer = 40 + (float)rng.NextDouble() * 200;
        NewGoal();
        // A straight trail behind it to start, and the spine laid along it, so
        // Head and the rest mean something before the first frame.
        for (int i = 40; i >= 0; i--)
            Remember(new PointF(_x - MathF.Cos(_heading) * _len * i / 30f, _y - MathF.Sin(_heading) * _len * i / 30f));
        LaySpine();
    }

    /// <summary>The floor under a screen point, 0 to 1 (a point off the screen counts as its nearest edge pixel: the pond and its bank run on out of view).</summary>
    private float FloorAt(PointF s) => _pond.Floor[Math.Clamp((int)s.Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)s.X, 0, _pond.Width - 1)] / 255f;

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);

    /// <summary>The pond's full depth in pixels: what Z = 1 is in the same unit as the ground plane, so the feelers and the chains can work in three dimensions at once.</summary>
    private float DepthPx => Pond.DepthU * _pond.U;

    /// <summary>Where it is in three dimensions, ground pixels across and depth in pixels down.</summary>
    private Vector3 Here => new(_x, _y, Z * DepthPx);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <param name="school">The other koi (for the mind: friends, quarry, chasers).</param>
    /// <param name="bodies">Every body in the water (the school and the turtles that are in): what the feelers must keep clear of.</param>
    public void Update(float dt, Koi[] school, Body[] bodies)
    {
        _time += dt;
        _bodies = bodies;
        float t = (float)(_time % 100000.0);

        // ---- The mind decides what it is doing ----
        Think(dt, school);
        bool rush = _doing is Doing.Chase or Doing.Flee or Doing.Flinch;          // flat out
        bool still = _doing == Doing.Rest || (_doing == Doing.Investigate && _hold > 0);   // hanging in the water, nosing at something

        // ---- Beat and glide ----
        // The rhythm's own clock: an energetic fish beats for longer and glides for less.
        _beatTimer -= dt;
        if (_beatTimer <= 0)
        {
            _beating = !_beating;
            _beatTimer = _beating ? (1.2f + 1.8f * (float)_rng.NextDouble()) * (0.6f + 0.8f * _energy)
                                  : (1.5f + 3.5f * (float)_rng.NextDouble()) * (1.6f - 0.8f * _energy);
        }
        // The mind overrules the rhythm: a chase is all beating, a rest is all glide,
        // and a gulp from deep down is a swim up to the surface, not a float up.
        _swimming = rush || (_beating && !still) || (_gulping && Z > 0.2f) || _doing == Doing.Shy;
        float targetSpeed = rush ? _len * _cruise * 1.6f : _swimming ? _len * _cruise : still ? _len * 0.07f : _len * 0.16f;
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (rush ? 2.2f : _swimming ? 1.1f : 0.45f)));
        float targetAmp = rush ? _len * 0.11f : _swimming ? _len * 0.085f : _len * 0.022f;
        _amp += (targetAmp - _amp) * (1 - MathF.Exp(-dt * 2.5f));
        float beatsPerSecond = rush ? 2.3f : _swimming ? 1.5f : 0.7f;
        _phase = (_phase + MathF.Tau * beatsPerSecond * dt) % MathF.Tau;

        // ---- Steering ----
        // Its own idle wander (quieter when it has somewhere to be), plus a
        // pull toward wherever the mind says: a spot, a thing, a friend, or
        // away from the fish behind it. The pull is gentle for an idle
        // cruise and firm for a chase, so a chase really turns.
        float idle = rush || _doing == Doing.Investigate ? 0.35f : 1f;
        float turn = idle * (0.32f * MathF.Sin(t * _w1 + _p1) + 0.22f * MathF.Sin(t * _w2 + _p2));
        (float gx, float gy, float pull) = Goal();
        float gdx = gx - _x, gdy = gy - _y;
        if (gdx * gdx + gdy * gdy > _len * _len * 0.04f)                         // right on top of it: no direction to pull toward
            turn += Wrap(MathF.Atan2(gdy, gdx) - _heading) * pull;
        float look = _len * 1.0f + _speed * 1.0f;
        PointF here = ToScreen(_x, _y);
        Vector3 here3 = Here, level = new(MathF.Cos(_heading), MathF.Sin(_heading), 0);
        // The feelers reach out along the heading in three dimensions, asking
        // RoomAround at each sample: land, the bed, the surface and every other
        // body in the water all count (Core/Minds Feelers).
        float roomAhead = Feelers.Along(_room, in here3, in level, look), roomHere = _pond.RoomAt(here.X, here.Y);
        bool cornered = false;
        _dodge = 0;
        if (roomAhead < _len * 0.55f || roomHere < _len * 0.35f || _stuckFor > 0)       // a refused step last frame also sends it looking for a way
        {
            // Something is in the way: the bank, a rock, another fish. Like a
            // person feeling their way: try a few headings either side, each
            // one level, climbing and diving, and take the one with the most
            // open water ahead, preferring a smaller change when two are about
            // as good. "Enough room" is enough: past a body length more open
            // water counts for nothing, so it takes the smallest turn that
            // clears the thing and follows its edge, rather than heading for
            // the middle. A climb or dive costs less than a step of turn and is
            // judged on less clearance (passing over a back is not passing
            // beside a bank), so with a fish ahead at nearly its own depth it
            // goes over or under rather than round; with the bank ahead a climb
            // clears nothing and it turns. The climb is steep, 0.8 of the
            // forward motion: a fish's length forward takes it a fish's length
            // up or down, which from above reads as a brisk swerve over.
            (float best, float climb, _) = Feelers.Best(_room, in here3, _heading, look, _len * 0.7f, 0.42f, 4, 0.8f, _len * 0.08f, _len * 0.05f);
            _dodge = climb;
            float urgency = Math.Clamp(1 - MathF.Min(roomAhead, roomHere * 1.5f) / (_len * 0.55f), 0, 1);
            turn += Wrap(best - _heading) * (1f + 3f * urgency);
            cornered = urgency > 0.5f && climb == 0;                                  // a climb or dive is not a corner: no need to slow and swing round
        }
        foreach (Koi other in school)
        {
            // A gentle lean away from any fish close by, before the feelers need
            // to act: measured in three dimensions, so a fish well above or below
            // is as good as far away.
            if (other == this) continue;
            float dx = _x - other._x, dy = _y - other._y, dz = (Z - other.Z) * DepthPx, d2 = dx * dx + dy * dy + dz * dz, near = (_len + other._len) * 0.55f;
            if (other == _partner) near *= _doing == Doing.Chase ? 0.3f : 0.7f;   // a friend may come closer; a chaser nearly touches
            if (d2 > near * near || dx * dx + dy * dy < 1) continue;
            float away = MathF.Atan2(dy, dx);
            turn += Wrap(away - _heading) * 0.5f * (1 - MathF.Sqrt(d2) / near);
        }
        // A fish cannot turn on the spot: the faster, the wider its turns, and
        // however slowly it is moving its turning circle is never tighter than
        // about 0.4 of its length (0.3 when cornered: it slows and swings round
        // tighter, as koi do at a wall). Without that floor a fish holding
        // nearly still at a petal could swing its heading right round in a
        // second and creep off through its own body, tail first to the eye.
        float most = (cornered ? 1.4f : 0.45f) + _speed / (_len * 0.5f) * 0.6f;
        most = MathF.Min(most, MathF.Max(0.12f, _speed / (_len * (cornered ? 0.3f : 0.4f))));
        if (cornered) _speed *= 1 - 0.6f * dt;
        turn = Math.Clamp(turn, -most, most);
        _heading = Wrap(_heading + turn * dt);

        float step = _speed * dt;
        PointF next = ToScreen(_x + MathF.Cos(_heading) * step, _y + MathF.Sin(_heading) * step);
        float roomNext = _pond.RoomAt(next.X, next.Y);
        // Never stuck: a step is refused only onto land or a rock, or when the
        // water is closing in AND it does not open up again half a length on.
        // (A pocket between two rocks is a tiny hill in the coarse room grid:
        // every step off its top loses a little room, so "only step toward
        // more room" wedged a fish there for minutes, facing open water.)
        bool refused = OnLand(next) || (roomNext <= _len * 0.15f && roomNext < roomHere && RoomAlong(_heading, _len * 0.5f) <= roomHere);
        if (!refused)
        {
            _x += MathF.Cos(_heading) * step;
            _y += MathF.Sin(_heading) * step;
            _stuckFor = 0;
        }
        else
        {
            // Nose right at the bank: hold here (still turning) rather than swim into it;
            // and after two seconds of that, whatever it was doing is given up.
            _stuckFor += dt;
            if (_stuckFor > 2) { _stuckFor = 0; Begin(Doing.Wander); }
        }
        PointF last = _trail[_trailHead];
        if ((last.X - _x) * (last.X - _x) + (last.Y - _y) * (last.Y - _y) > (_len / 40) * (_len / 40))
            Remember(new PointF(_x, _y));

        // ---- Up and down in the water, and the occasional gulp ----
        _depthTimer -= dt;
        if (_depthTimer <= 0 && !_gulping && !_bumping)
        {
            // The idle up and down keeps to the upper water, where a fish is
            // seen: clear near the top, hazy at the bottom of this range. Going
            // deeper than that, down to where it is lost, is a rest's or a
            // fright's doing (Begin), not the idle drift's.
            _zTarget = 0.05f + 0.35f * (float)_rng.NextDouble();
            _depthTimer = 6 + 10 * (float)_rng.NextDouble();
        }
        _gulpTimer -= dt;
        if (_gulpTimer <= 0 && !_gulping && !_bumping) { _gulping = true; _zTarget = 0; }

        // ---- The back-bump: now and then a cruising koi rises slowly until its back
        // brushes the surface from below, and three thin rings slip off its back as it
        // glides on; then it sinks back. Rare, slow and quiet: the rise takes ten seconds
        // or so at the ordinary drift rate, and the rings are the thin kind. ----
        _bumpTimer -= dt;
        if (_bumpTimer <= 0 && !_gulping && !_bumping) { _bumping = true; _zTarget = 0; _depthTimer = 40; }
        if (_bumping && Z < 0.06f)
        {
            _bumpGap -= dt;
            if (_bumpGap <= 0)
            {
                PointF back = Spine[7];                                   // the highest point of the back, behind the shoulders
                _ripples.Add(back.X, back.Y, 0.5f, thin: true);
                _bumpRings++;
                _bumpGap = 0.4f;
            }
            if (_bumpRings >= 3)
            {
                _bumping = false;
                _bumpRings = 0;
                _bumpTimer = 120 + 240 * (float)_rng.NextDouble();
                _zTarget = 0.15f + 0.3f * (float)_rng.NextDouble();
                _depthTimer = 6 + 10 * (float)_rng.NextDouble();
            }
        }
        // The bed. A fish may be no deeper than nine tenths of the water under
        // ANY part of its body (_floor, found as the spine was laid), and no
        // deeper than the water a body length and a half ahead of its nose, so
        // it rises up the slope as the bank or a rock comes near, the way a
        // fish follows the bed, rather than finding itself inside it. The rise
        // for the bed is brisker than the idle drift up and down; and whatever
        // the easing says, LaySpine never leaves it below the bed.
        float ahead = FloorAt(ToScreen(_x + MathF.Cos(_heading) * _len * 1.5f, _y + MathF.Sin(_heading) * _len * 1.5f));
        float cap = 0.9f * MathF.Min(_floor, ahead);
        float zWant = MathF.Min(_zTarget, cap);
        bool forBed = Z > cap - 0.03f;
        // Easing toward the depth it wants; or, when the feelers chose a climb
        // or a dive to clear something, that: z changes by the chosen slope
        // times the distance swum, so the course is a straight line in three
        // dimensions, and the easing takes over again once the way is clear.
        float zNext = _dodge != 0 && !refused ? Z + _dodge * step / DepthPx
                                              : Z + (zWant - Z) * (1 - MathF.Exp(-dt * (_gulping ? 0.6f : forBed ? 0.8f : 0.18f)));
        zNext = Math.Clamp(zNext, 0, cap);
        if (zNext < Z)
        {
            // Rising moves the picture UP the screen (its sink shrinks). A fish
            // rising fast while barely swimming would be seen sliding up the
            // screen whichever way its nose pointed, and one heading down the
            // screen going backward, tail first (the review caught an asagi
            // doing just that on a gulp from a deep rest; a resting fish with
            // its nose sideways did it on a slow rise). So the rise on screen
            // is held to a share of the fish's own forward motion: half of it
            // with the nose across the screen, none at all with the nose
            // straight down the screen (every pixel of rise would be a pixel
            // backward), all of it with the nose straight up the screen (the
            // rise only adds to the way it is going). A rise the BED forces is
            // held back too: with the pond this deep, a fish heading for the
            // bank from deep water rose half the pond's depth in a second and
            // its picture slid 50 pixels up the screen while it swam the other
            // way (the biggest slide on the timeline, six times on one fish in
            // three minutes). The bed still wins in LaySpine, and the hop that
            // leaves is one to four pixels a frame; a bed steeper than the fish
            // can climb at its speed is a wall to its feelers (RoomAround), so
            // it turns or climbs before it is lifted.
            float share = MathF.Max(0.02f, 0.5f * (1 - MathF.Sin(_heading)));
            float maxRise = dt * share * _speed / (Pond.DepthU * _pond.U * Light.Sink);
            zNext = MathF.Max(zNext, Z - maxRise);
        }
        else if (zNext > Z)
        {
            // And the mirror of it: sinking moves the picture DOWN the screen
            // (its sink grows). Held the same way, with the nose straight up the
            // screen allowing none. Now that the pond is deep enough to lose a
            // fish in, a resting fish sinking out of sight drifted down the
            // screen at five pixels a second with its nose pointing up it.
            float share = MathF.Max(0.02f, 0.5f * (1 + MathF.Sin(_heading)));
            float maxSink = dt * share * _speed / (Pond.DepthU * _pond.U * Light.Sink);
            zNext = MathF.Min(zNext, Z + maxSink);
        }
        Z = zNext;
        if (_gulping && Z < 0.05f)
        {
            PointF mouth = Spine[0];
            _ripples.Add(mouth.X, mouth.Y, 1f);
            _petals?.Take(mouth.X, mouth.Y, _len * 0.45f);                       // a petal at its mouth is eaten
            _gulping = false;
            _gulpTimer = 40 + 60 * (float)_rng.NextDouble();
            _zTarget = 0.2f + 0.4f * (float)_rng.NextDouble();
            _depthTimer = 8;
        }

        LaySpine();
    }

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    /// <summary>Picks somewhere new to idle toward: a random spot in open water, and how long to keep at it.</summary>
    /// <param name="deep">Only a deep spot (a resting place), if one turns up.</param>
    private void NewGoal(bool deep = false)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _len * 0.4f) continue;
            if (deep && tries < 40 && _pond.Floor[(int)y * _pond.Width + (int)x] < 150) continue;
            (_goalX, _goalY) = ToGround(x, y);
            break;
        }
        _goalTimer = 12 + 18 * (float)_rng.NextDouble();
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: a koi has one thing on its mind at a time (a Doing), a
    // feeling for how long to keep at it (a timer), and when that runs out it
    // decides what to do next by rolling weighted dice. The weights are where
    // the personality lives: a curious fish's dice have more "go and look"
    // faces, a sociable fish's more "follow a friend" and "chase" faces, a lazy
    // one's more "rest" faces. Two things can interrupt: being chased (it
    // flees at once), and a dragonfly tapping the water (a curious fish goes
    // to see). The steering below the mind only ever receives a point to swim
    // toward and how firmly, so whatever the mind asks, the bank, the other
    // fish and the "never stuck" rule still have the last word.

    /// <summary>Keeps the current doing going, ends it when it is done, and reacts to being chased.</summary>
    private void Think(float dt, Koi[] school)
    {
        _mind.Tick(dt);
        _chaseCool.Tick(dt);
        _flinchCool.Tick(dt);
        _shyCool.Tick(dt);

        // Someone is after me: the moment a chaser is close behind, flee (whatever I was doing, except already fleeing).
        if (_doing != Doing.Flee)
            foreach (Koi o in school)
                if (o != this && o._doing == Doing.Chase && o._partner == this && Near(o, 1.6f) && Toward(o, this) > 0.2f)
                {
                    Begin(Doing.Flee, o);
                    break;
                }

        switch (_doing)
        {
            case Doing.Flee:
                // Safe again once the chaser gives up or falls well behind.
                if (_partner == null || _partner._doing != Doing.Chase || _partner._partner != this || !Near(_partner, 3f))
                    Begin(Doing.Wander);
                break;
            case Doing.Chase:
                if (_mind.Over || _partner == null) Decide(school);
                break;
            case Doing.Flinch:
                if (_mind.Over) Begin(Doing.Rest);                            // the dart is over: it sulks deep for a while
                break;
            case Doing.Follow:
                // Lose interest in time, or when the friend has got well away.
                if (_mind.Over || _partner == null || !Near(_partner, 4f)) Decide(school);
                break;
            case Doing.Investigate:
                if (_targetIsPetal && _petals != null)
                {
                    // The petal drifts: keep the target on it (or give up if it sank).
                    PointF was = ToScreen(_targetX, _targetY);
                    if (_petals.NearestFloating(was.X, was.Y, _pond.U * 0.08f, out PointF now)) (_targetX, _targetY) = ToGround(now.X, now.Y);
                    else { Decide(school); break; }
                }
                float tdx = _targetX - _x, tdy = _targetY - _y;
                if (_hold > 0)
                {
                    _hold -= dt;
                    if (_hold <= 0) Decide(school);
                }
                else if (tdx * tdx + tdy * tdy < _len * _len * 0.36f)
                {
                    // Arrived: hang here nosing at it for a moment; a petal gets gulped at (and eaten, if the mouth lands on it).
                    _hold = 1 + 2.5f * (float)_rng.NextDouble();
                    if (_targetIsPetal && !_gulping && !_bumping) { _gulping = true; _zTarget = 0; }
                }
                else if (_mind.Over) Decide(school);
                break;
            case Doing.Rest:
            case Doing.Shy:
                if (_mind.Over) Decide(school);
                break;
            default:
                _goalTimer -= dt;
                float gdx = _goalX - _x, gdy = _goalY - _y;
                if (_goalTimer <= 0 || gdx * gdx + gdy * gdy < _len * _len * 2.25f) Decide(school);
                break;
        }
    }

    /// <summary>Where the mind says to swim (ground pixels), and how firmly to turn toward it.</summary>
    private (float X, float Y, float Pull) Goal()
    {
        switch (_doing)
        {
            case Doing.Chase:
                return (_partner!._x, _partner._y, 1.1f);
            case Doing.Flee:
                {
                    // Straight away from the chaser, a few body lengths ahead of me.
                    float ax = _x - _partner!._x, ay = _y - _partner._y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                    return (_x + ax / l * _len * 3, _y + ay / l * _len * 3, 0.9f);
                }
            case Doing.Flinch:
                {
                    // Straight away from where the shadow was.
                    float ax = _x - _flinchX, ay = _y - _flinchY, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                    return (_x + ax / l * _len * 3, _y + ay / l * _len * 3, 1.0f);
                }
            case Doing.Follow:
                {
                    // A body length behind the friend and half a length to one side, as fish swim in company.
                    float hx = MathF.Cos(_partner!._heading), hy = MathF.Sin(_partner._heading);
                    return (_partner._x - hx * _len * 1.0f - hy * _side * _len * 0.5f, _partner._y - hy * _len * 1.0f + hx * _side * _len * 0.5f, 0.5f);
                }
            case Doing.Investigate:
                return (_targetX, _targetY, _hold > 0 ? 0.25f : 0.6f);
            case Doing.Shy:
                return (_shyX, _shyY, 0.8f);
            case Doing.Rest:
                return (_goalX, _goalY, 0.25f);
            default:
                return (_goalX, _goalY, 0.4f);
        }
    }

    /// <summary>Rolls the next doing, weighted by personality and by what there is to do.</summary>
    private void Decide(Koi[] school)
    {
        // A friend to follow must be near, and a fish to chase nearer still (a
        // chase begun across the pond was over before the chaser got anywhere).
        Koi? friend = Nearest(school, 4f), quarry = Nearest(school, 2.5f);
        bool chaseOn = quarry != null && _chaseCool.Ready && !school.Any(k => k._doing is Doing.Chase or Doing.Flee);   // one chase in the pond at a time
        // The dice faces: each doing's weight. Wander is always on the dice.
        float wWander = 1f;
        float wRest = (0.25f + 0.9f * (1 - _energy)) * (_doing == Doing.Rest ? 0.4f : 1f);               // and a rest is less likely straight after a rest
        float wLook = (0.08f + 0.45f * _curious) * _play * (_doing == Doing.Investigate ? 0.4f : 1f);   // less likely straight after a look
        float wFollow = friend != null ? (0.1f + 0.7f * _social) * _play : 0;
        float wChase = chaseOn ? (0.1f + 0.7f * _social * _energy) * _play : 0;
        switch (Dice.Pick(_rng, [wChase, wFollow, wLook, wRest, wWander]))
        {
            case 0: Begin(Doing.Chase, quarry); break;
            case 1: Begin(Doing.Follow, friend); break;
            case 2 when PickTarget(): Begin(Doing.Investigate); break;        // nothing worth a look in reach: it wanders instead
            case 3: Begin(Doing.Rest); break;
            default: Begin(Doing.Wander); break;
        }
    }

    /// <summary>Starts a doing: its timer, its partner, and where in the water to be for it.</summary>
    private void Begin(Doing doing, Koi? partner = null)
    {
        _partner = partner;
        _hold = 0;
        switch (doing)
        {
            case Doing.Chase:
                _mind.Begin(doing, 4 + 5 * (float)_rng.NextDouble());
                _chaseCool.Start((60 + 140 * (float)_rng.NextDouble()) / MathF.Max(0.1f, _play));
                Depth(0.1f + 0.15f * (float)_rng.NextDouble(), 20);
                break;
            case Doing.Flee:
                _mind.Begin(doing, 10);
                Depth(0.08f + 0.15f * (float)_rng.NextDouble(), 20);
                break;
            case Doing.Flinch:
                _mind.Begin(doing, 0.8f + 0.6f * (float)_rng.NextDouble());
                _flinchCool.Start(15 + 25 * (float)_rng.NextDouble());
                Depth(0.6f + 0.3f * (float)_rng.NextDouble(), 30);               // straight down, and it stays down
                break;
            case Doing.Follow:
                _mind.Begin(doing, 10 + 20 * (float)_rng.NextDouble());
                _side = _rng.Next(2) * 2 - 1;
                Depth(MathF.Max(0.05f, partner!.Z + 0.1f), _mind.Left);        // a little under the friend
                break;
            case Doing.Investigate:
                _mind.Begin(doing, 25);                                        // give up if it is not reached by then
                Depth(0.06f, 30);                                              // right up near the surface, where it can be seen looking
                break;
            case Doing.Rest:
                _mind.Begin(doing, 8 + 14 * (float)_rng.NextDouble());
                NewGoal(deep: true);
                Depth(0.45f + 0.3f * (float)_rng.NextDouble(), _mind.Left);        // deep: from hazy to, at the deep end, out of sight (Light.SeeU)
                break;
            case Doing.Shy:
                _mind.Begin(doing, 1.2f + 0.8f * (float)_rng.NextDouble());        // a short swim away (the place and depth were set by Bumped)
                break;
            default:
                _mind.Begin(doing);                                            // a wander ends when its spot is reached (the goal timer, in Think)
                NewGoal();
                break;
        }
    }

    /// <summary>Asks for a depth and holds it for a while (the idle up-and-down waits).</summary>
    private void Depth(float z, float holdFor)
    {
        if (_gulping || _bumping) return;
        _zTarget = z;
        _depthTimer = holdFor;
    }

    /// <summary>The nearest other fish within "lengths" body lengths, or null.</summary>
    private Koi? Nearest(Koi[] school, float lengths)
    {
        Koi? best = null;
        float bestD = _len * _len * lengths * lengths;
        foreach (Koi o in school)
        {
            if (o == this) continue;
            float dx = o._x - _x, dy = o._y - _y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    /// <summary>
    /// Something to go and look at, near enough to bother: a floating petal
    /// (first choice when there is one close), the rim of a lily pad, the foot
    /// of the cattails, or the side of the floating log. False if nothing is
    /// in reach, and the fish wanders instead.
    /// </summary>
    private bool PickTarget()
    {
        PointF here = ToScreen(_x, _y);
        float u = _pond.U;
        _targetIsPetal = false;
        if (_petals != null && _rng.NextDouble() < 0.4 && _petals.NearestFloating(here.X, here.Y, u * 0.6f, out PointF petal) && _pond.RoomAt(petal.X, petal.Y) >= _len * 0.6f)
        {
            (_targetX, _targetY) = ToGround(petal.X, petal.Y);
            _targetIsPetal = true;
            return true;
        }
        for (int tries = 0; tries < 6; tries++)
        {
            PointF p;
            int kind = _rng.Next(3);
            if (kind == 0 && _pond.Pads.Count > 0)
            {
                // A point just off a pad's rim, on the side facing me.
                LilyPad pad = _pond.Pads[_rng.Next(_pond.Pads.Count)];
                float ax = here.X - pad.Center.X, ay = here.Y - pad.Center.Y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                p = new PointF(pad.Center.X + ax / l * (pad.Radius + _len * 0.3f), pad.Center.Y + ay / l * (pad.Radius + _len * 0.3f));
            }
            else if (kind == 1)
            {
                // The foot of the cattails, from the open-water side.
                PointF c = _pond.CattailSpot;
                float ax = _pond.Centre.X - c.X, ay = _pond.Centre.Y - c.Y, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                p = new PointF(c.X + ax / l * u * 0.1f, c.Y + ay / l * u * 0.1f);
            }
            else if (_pond.Log is Log log && log.Afloat)
            {
                // Alongside the floating log, a little off its side.
                float along = ((float)_rng.NextDouble() - 0.5f) * log.Length * 0.8f;
                float nx = -MathF.Sin(log.Angle), ny = MathF.Cos(log.Angle);
                int side = (here.X - log.Centre.X) * nx + (here.Y - log.Centre.Y) * ny > 0 ? 1 : -1;
                p = new PointF(log.Centre.X + MathF.Cos(log.Angle) * along + nx * side * (log.Width * 0.5f + _len * 0.35f),
                               log.Centre.Y + MathF.Sin(log.Angle) * along + ny * side * (log.Width * 0.5f + _len * 0.35f));
            }
            else continue;
            float dx = p.X - here.X, dy = p.Y - here.Y;
            if (dx * dx + dy * dy > u * u * 0.8f * 0.8f) continue;                // too far to bother
            if (!InWater(p) || _pond.RoomAt(p.X, p.Y) < _len * 0.6f) continue;   // on land, or too tight against the bank or the rocks to swim up to
            (_targetX, _targetY) = ToGround(p.X, p.Y);
            return true;
        }
        return false;
    }

    /// <summary>A dragonfly tapped the water here (screen pixels): a curious fish not otherwise busy goes to see.</summary>
    public void Notice(float x, float y)
    {
        if (_doing is Doing.Chase or Doing.Flee || _rng.NextDouble() > 0.25 + 0.7 * _curious * _play) return;
        PointF here = ToScreen(_x, _y);
        float dx = x - here.X, dy = y - here.Y;
        if (dx * dx + dy * dy > _pond.U * _pond.U * 0.5f * 0.5f || !InWater(new PointF(x, y)) || _pond.RoomAt(x, y) < _len * 0.6f) return;
        (_targetX, _targetY) = ToGround(x, y);
        _targetIsPetal = false;
        Begin(Doing.Investigate);
    }

    /// <summary>The ground, a rock or the log: a koi can never step here. (A lily pad is not land: koi swim under them.)</summary>
    private bool OnLand(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || _pond.Land[iy * _pond.Width + ix];
    }

    /// <summary>Open water in view (not under a pad, not under the branches): where a fish nosing at something can be seen doing it.</summary>
    /// <summary>
    /// A shadow swept over the water here (screen pixels): a swallow skimming.
    /// A fish under or near it darts away and dives, the way pond fish scatter
    /// when a bird's shadow crosses them. A bold fish (energetic and sociable)
    /// mostly ignores it; a timid one flinches at a shadow some way off. Never
    /// twice in quick succession, and never while chasing or being chased.
    /// </summary>
    public void Startle(float x, float y)
    {
        if (!_flinchCool.Ready || _doing is Doing.Chase or Doing.Flee or Doing.Flinch) return;
        float bold = (_energy + _social) * 0.5f;
        float reach = _pond.U * (0.06f + 0.16f * (1 - bold));
        PointF here = ToScreen(_x, _y);
        float dx = x - here.X, dy = y - here.Y;
        if (dx * dx + dy * dy > reach * reach) return;
        (_flinchX, _flinchY) = ToGround(x, y);
        if (_gulping) { _gulping = false; _gulpTimer = 20; }                     // whatever it was about to do at the surface is forgotten
        Begin(Doing.Flinch);
    }

    private bool InWater(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _where[iy * _pond.Width + ix];
    }

    /// <summary>
    /// The world as the feelers see it (Core/Minds Feelers.Room): how much
    /// open space there is round a point of the water, in pixels. Nothing on
    /// land, in the bed or above the surface; otherwise the distance to the
    /// nearest bank or rock (the room grid), or to the nearest other body in
    /// the water if that is closer, measured for a ball as wide as this fish
    /// (so "room" means room for ME). A gap up or down counts two and a half
    /// times a gap sideways (Chain.Gap's stretch): a fish passes over
    /// another's back on a fraction of its width, so a body half a width
    /// below reads as well clear, and the feelers settle on a course over or
    /// under rather than a wide turn round (and stop diving once they are
    /// clear: with the gap counted plain, a fish kept diving under a resting
    /// one for eight seconds, long after it was well below it). The box test
    /// skips bodies further off than the room already found.
    /// </summary>
    private float RoomAround(in Vector3 p)
    {
        PointF s = ToScreen(p.X, p.Y);
        if (OnLand(s)) return 0;
        if (p.Z < -1 || p.Z > 0.9f * FloorAt(s) * DepthPx) return 0;              // above the water, or in the bed
        float room = _pond.RoomAt(s.X, s.Y);
        foreach (Body b in _bodies)
        {
            if (b == this || !b.Shape.Near(p, _wide, room)) continue;
            room = MathF.Min(room, b.Shape.Gap(p, _wide, 2.5f));
            if (room <= 0) return 0;
        }
        return room;
    }

    /// <summary>
    /// Moved bodily by KoiSchool, the way a fish is shoved when it meets
    /// another: across the water and up or down in it, then its spine laid
    /// again so the drawn frame has it out of the thing it met.
    /// </summary>
    public void Nudge(Vector3 by)
    {
        // The WHOLE body moves: the trail the spine is laid along shifts with
        // the head. Moving the head alone left the tail where it was, and two
        // fish whose tails crossed stayed crossed for seconds while their heads
        // were pushed apart every frame (241 touching frames in one run).
        _x += by.X; _y += by.Y;
        for (int i = 0; i < _trailCount; i++)
        {
            int slot = (_trailHead - i + _trail.Length) % _trail.Length;
            _trail[slot] = new PointF(_trail[slot].X + by.X, _trail[slot].Y + by.Y);
        }
        Z = MathF.Max(0, Z + by.Z / DepthPx);
        LaySpine();
    }

    /// <summary>Its middle on the ground plane, for a push along the line between two bodies' middles when the touch gives no direction.</summary>
    public Vector3 Centre => Shape.Points[8];

    /// <summary>
    /// It bumped into another body in the water, "away" being the way out.
    /// Unless it is in a chase (then it presses on, the push keeps them
    /// apart), it shies: a short swim off along "away", and up or down to
    /// whichever side of the other it was pushed. A cooldown, as two fish
    /// swimming side by side touch again and again.
    /// </summary>
    public void Bumped(Vector3 away)
    {
        if (!_shyCool.Ready || _doing is Doing.Chase or Doing.Flee or Doing.Flinch) return;
        _shyCool.Start(4 + 4 * (float)_rng.NextDouble());
        float l = MathF.Max(1e-3f, MathF.Sqrt(away.X * away.X + away.Y * away.Y));
        _shyX = _x + away.X / l * _len * 2; _shyY = _y + away.Y / l * _len * 2;
        Begin(Doing.Shy);
        if (MathF.Abs(away.Z) > 0.3f) Depth(Math.Clamp(Z + MathF.Sign(away.Z) * 0.12f, 0, 1), 6);
    }

    /// <summary>Is the other fish within "lengths" of my body length?</summary>
    private bool Near(Koi o, float lengths)
    {
        float dx = o._x - _x, dy = o._y - _y;
        return dx * dx + dy * dy < _len * _len * lengths * lengths;
    }

    /// <summary>How squarely a is heading at b: 1 straight at it, 0 sideways, -1 away.</summary>
    private static float Toward(Koi a, Koi b)
    {
        float dx = b._x - a._x, dy = b._y - a._y, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        return (dx * MathF.Cos(a._heading) + dy * MathF.Sin(a._heading)) / l;
    }

    /// <summary>
    /// The open water along a feeler "dist" long on heading a: the least room
    /// at four points along it, the nearer points counting for more (0 on
    /// land). The first point is close in, so a small rock in the shallows,
    /// narrower than half a feeler, cannot slip between the samples: with only
    /// the halfway point and the end tested, a koi nosed into one such rock
    /// behind the cattails, found open water beyond it, and stood there for a
    /// minute while every step onto the rock was refused.
    /// </summary>
    private float RoomAlong(float a, float dist)
    {
        float least = float.MaxValue, ca = MathF.Cos(a), sa = MathF.Sin(a);
        foreach (float f in (ReadOnlySpan<float>)[0.12f, 0.35f, 0.65f, 1f])
        {
            PointF p = ToScreen(_x + ca * dist * f, _y + sa * dist * f);
            if (OnLand(p)) return 0;
            least = MathF.Min(least, _pond.RoomAt(p.X, p.Y) * (2 - f));
        }
        return least;
    }

    /// <summary>
    /// Lays the spine along the remembered trail, adds the swimming wave,
    /// and squashes each pair of edge points to the screen.
    /// </summary>
    private void LaySpine()
    {
        // Walk back along the trail, dropping a spine point every so often.
        // SIZE BY DEPTH: we look at the pond from a few body lengths above it,
        // so the bed is further from the eye than the surface, and a fish down
        // there is drawn a little smaller (KoiSchool.Shrink at full depth). The
        // fish itself is no smaller: its steering keeps its true length. A fish
        // rising to gulp grows as it comes up, which is part of what "coming
        // up" looks like from above.
        _scale = Light.Scale(Z);
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(_x, _y), b = _trail[slot];
        float travelled = 0, segLen = Dist(a, b);
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len * _scale;
            while (travelled + segLen < want && left > 0)
            {
                travelled += segLen;
                a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length;
                left--;
                b = _trail[slot];
                segLen = Dist(a, b);
            }
            float f = segLen > 1e-4f ? (want - travelled) / segLen : 0;
            if (left <= 0 && travelled + segLen < want)
            {
                // Ran out of trail (just started): carry on straight back.
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(_heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(_heading);
                mid[i] = new PointF(b.X + ex * (want - travelled - segLen), b.Y + ey * (want - travelled - segLen));
            }
            else mid[i] = new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
        }
        // The direction at each point (nose to tail), for the sideways wave and the edges.
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        // The bed under the body: the shallowest water under any spine point,
        // tail fin included, and under both edges of the body (a rock's flank
        // rises steeply enough that a body's width can span a tenth of it).
        // Its depth is capped by that HERE, after Update's easing, so a fish is
        // never drawn below the bed on any frame, whatever it meant to do.
        float floor = 1;
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i], hw = _wide * Profile(s) * _scale, nx = -dir[i].Y, ny = dir[i].X;
            float sway = _amp * (0.06f + s * s) * MathF.Sin(MathF.Tau * 0.85f * s - _phase);   // the same wave the body is drawn with, below
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            floor = MathF.Min(floor, FloorAt(ToScreen(cx, cy)));
            floor = MathF.Min(floor, FloorAt(ToScreen(cx + nx * hw, cy + ny * hw)));
            floor = MathF.Min(floor, FloorAt(ToScreen(cx - nx * hw, cy - ny * hw)));
        }
        _floor = floor;
        Z = MathF.Min(Z, 0.9f * floor);
        // DEPTH ON THE SCREEN. The fish lives at (_x, _y) on the water's plane
        // and Z below it. We look at the pond at a slant, so the deeper it is,
        // the lower on the screen its picture sits than the water above it
        // (Light.Sink, the lift rule run the other way): a fish that rises to
        // gulp is seen to come UP the screen as well as sharpen.
        float sink = Light.SinkPx(Z, _pond.U);
        // Its shadow on the bed, where the bed shows through the water (the
        // painter's own rule for how much bed shows at the painted depth):
        // thrown down and to the right by the gap between the fish and the bed,
        // as the petals' shadows are by their height, and the blurrier the
        // bigger that gap. Out in the deep water there is no bed to see it on.
        int under = Math.Clamp((int)Spine[8].Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)Spine[8].X, 0, _pond.Width - 1);
        // "How much bed shows" is the painter's depth, or the floor where that is
        // shallower: over the island's shoulders the water is painted deep but the
        // stone is right under the fish, and a fish pushed up over a stone with no
        // shadow on it read as a fish sliding over a picture of a rock.
        float painted = Math.Min(_pond.Depth[under], _pond.Floor[under]) / 255f;
        float clear = 0.85f * MathF.Pow(1 - painted, 1.4f);
        float lit = Math.Clamp((_pond.Sun[under] - 154) / 101f, 0, 1);        // no sun on the bed under a branch's shade: no shadow there (Sun is 154 in full shade)
        float gap = MathF.Max(0, floor - Z) * Pond.DepthU * _pond.U;
        _shadowAlpha = clear > 0.06f ? MathF.Min(0.6f, 0.9f * clear) * lit : 0;   // at half alpha it was lost in the pebbles; a koi's shadow on a clear bed is a bold thing
        // A shadow that is only just showing (the bed barely visible under deep
        // water) keeps close to its fish: thrown the full gap it would be a faint
        // stray mark a body length away, with nothing to say whose it was. How
        // close goes by how much bed shows, not by the light: a fish swimming into
        // a branch's shade keeps its shadow where it was while it fades.
        float hug = MathF.Min(1f, 3f * clear);
        _shadowDx = Light.ShadowX * gap * hug;
        _shadowDy = (Light.ShadowY + Light.Sink) * gap * hug;
        _shadowSoft = MathF.Max(1f, gap * 0.15f);
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            // The swimming wave: tiny at the head, widest at the tail, running from head to tail.
            float sway = _amp * (0.06f + s * s) * MathF.Sin(MathF.Tau * 0.85f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;                 // sideways
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s) * _scale;
            PointF l = ToScreen(cx + nx * hw, cy + ny * hw), r = ToScreen(cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2 + sink);
            HalfWidth[i] = Dist(l, r) / 2;
            // The same point for touching: on the ground plane, at its depth.
            // ponytail: the chain is the DRAWN body (shrunk by depth), not the true one; two deep fish are both shrunk alike, so they still meet when they look as if they do.
            Shape.Points[i] = new Vector3(cx, cy, Z * DepthPx);
            Shape.Radius[i] = hw;
        }
        Shape.Seal();
    }

    /// <summary>
    /// How wide the body is at s, as a fraction of its widest: a blunt
    /// rounded snout, widest just behind the head (at the shoulders), tapering
    /// to a slim tail stalk at s = 1, and then the tail fin fanning out.
    /// </summary>
    private static float Profile(float s)
    {
        if (s < 0.28f) { float k = (0.28f - s) / 0.31f; return MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        if (s <= 1f) return 1 - 0.84f * MathF.Pow((s - 0.28f) / 0.72f, 1.4f);
        float f = (s - 1) / (FinEnd - 1);
        return 0.16f + 0.5f * MathF.Sqrt(f);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        if (s > 1f)
        {
            // The tail fin: forked (the middle ends sooner), see-through, with rays.
            float m = 1 - av, end = FinEnd - 0.09f * m * MathF.Sqrt(m);
            if (s > end) { r = g = b = 0; alpha = 0; return false; }
            (r, g, b) = _fin;
            alpha = _finAlpha * (0.72f + 0.28f * MathF.Cos(v * 15)) * Math.Clamp((end - s) * 30, 0, 1);
            return true;
        }
        // One lookup: the rounding of the body (Round) is already folded into the sheet.
        int vi = Math.Clamp((int)((v + 1) * 0.5f * SkinWide), 0, SkinWide - 1);
        int at = Math.Clamp((int)(s * SkinLen), 0, SkinLen - 1) * SkinWide + vi;
        uint c = _skin[at];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        if (_blur256 > 0)
        {
            // Deep down, the markings go soft (a second, blurred sheet mixed in
            // by depth) and the whole fish dimmer: less light reaches it, and
            // less comes back up through the water.
            uint d = _skinBlur[at];
            r += ((int)((d >> 16) & 0xFF) - r) * _blur256 >> 8; g += ((int)((d >> 8) & 0xFF) - g) * _blur256 >> 8; b += ((int)(d & 0xFF) - b) * _blur256 >> 8;
            r = r * _dim256 >> 8; g = g * _dim256 >> 8; b = b * _dim256 >> 8;
        }
        alpha = 1;
        return true;
    }

    /// <summary>
    /// Works out the body's colours once, when the fish is made: the
    /// markings (each sheet step averaged from four looks at the markings'
    /// map, so their edges come out soft), a metallic koi's sheen, and the
    /// darker line of the dorsal fin folded along its back. Drawing then only
    /// looks a colour up (see Colour), which matters at 4K, where a big koi
    /// is tens of thousands of pixels every frame.
    /// </summary>
    private uint[] Skin()
    {
        var skin = new uint[SkinLen * SkinWide];
        for (int si = 0; si < SkinLen; si++)
            for (int vi = 0; vi < SkinWide; vi++)
            {
                float cr = 0, cg = 0, cb = 0;
                for (int k = 0; k < 4; k++)
                {
                    float s = (si + 0.25f + 0.5f * (k & 1)) / SkinLen, v = (vi + 0.25f + 0.5f * (k >> 1)) / SkinWide * 2 - 1;
                    var c = _palette[_pattern[Math.Min(PatLen - 1, (int)(s * PatLen)) * PatWide + Math.Clamp((int)((v + 1) * 0.5f * PatWide), 0, PatWide - 1)]];
                    cr += c.R / 4f; cg += c.G / 4f; cb += c.B / 4f;
                }
                float sc = (si + 0.5f) / SkinLen, vc = (vi + 0.5f) / SkinWide * 2 - 1, av = MathF.Abs(vc);
                float shade = Round[vi];                                                                             // the rounded body: backbone brightest, flanks falling away
                if (_metal) shade *= 0.95f + 0.14f * (1 - av) * (1 - av);                                         // a metallic koi: a soft sheen down the back
                if (av < 0.1f && sc > 0.34f && sc < 0.74f) shade *= 0.86f;                                       // the dorsal fin, folded flat along the back
                skin[si * SkinWide + vi] = (uint)(Math.Min(255, (int)(cr * shade)) << 16 | Math.Min(255, (int)(cg * shade)) << 8 | Math.Min(255, (int)(cb * shade)));
            }
        return skin;
    }

    /// <summary>
    /// The skin sheet blurred, for a fish seen through a depth of water: each
    /// step averaged with its neighbours, a few steps along the body and a
    /// couple across, done twice (two box blurs make a soft bell). Worked out
    /// once per fish; drawing mixes toward it by depth (see Colour).
    /// </summary>
    private static uint[] Blur(uint[] skin)
    {
        uint[] src = skin, dst = new uint[skin.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            const int alongR = 5, acrossR = 2;
            for (int si = 0; si < SkinLen; si++)
                for (int vi = 0; vi < SkinWide; vi++)
                {
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int ds = -alongR; ds <= alongR; ds++)
                    {
                        int s2 = si + ds;
                        if (s2 < 0 || s2 >= SkinLen) continue;
                        for (int dv = -acrossR; dv <= acrossR; dv++)
                        {
                            int v2 = Math.Clamp(vi + dv, 0, SkinWide - 1);     // across: the edge pixel repeats (the sheet's edge is the body's edge)
                            uint c = src[s2 * SkinWide + v2];
                            r += (int)((c >> 16) & 0xFF); g += (int)((c >> 8) & 0xFF); b += (int)(c & 0xFF); n++;
                        }
                    }
                    dst[si * SkinWide + vi] = (uint)((r / n) << 16 | (g / n) << 8 | (b / n));
                }
            if (pass == 0) { src = dst; dst = new uint[skin.Length]; }
        }
        return dst;
    }

    /// <param name="mark">If given, set true at every pixel the body paints (KoiSchool throws the shallower fishes' shadows onto those).</param>
    public void Draw(FrameBuffer fb, bool[]? mark = null)
    {
        // Lost in the water by depth (Light.Murk): past Light.SeeU there is
        // nothing left of it to paint, and painting it would only wipe the
        // bed shadows under it with the water's own colour.
        float murk = Light.Murk(Z);
        if (murk > 0.985f) return;
        float lost = Math.Clamp(Light.DepthOf(Z) / Light.SeeU, 0, 1);
        float soft = MathF.Max(1f, _pond.U * 0.0012f) * (1 + 3 * lost);
        _blur256 = (int)(Math.Clamp(lost * 1.1f, 0, 1) * 256);
        _dim256 = 256 - (int)(0.3f * lost * 256);
        // Side fins first, so the body covers where they join it.
        float t = (float)(_time % 100000.0);
        float scull = 0.22f * MathF.Sin(t * 1.4f + _p1) + (_swimming ? 0f : 0.25f);    // gliding: fins out, braking a little
        Fins(fb, 4, 0.2f, 0.12f, 0.95f + scull, murk, mark);
        Fins(fb, 11, 0.11f, 0.065f, 0.75f + scull * 0.5f, murk, mark);
        Paint(fb, _where, _pond.Sun, murk, soft, _pond.Pixels, mark: mark);
        // The eyes: a small dark dot on each side of the head.
        PointF e0 = Spine[1], e1 = Spine[2];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.8f, _len * 0.017f * _scale);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X - ey / el * HalfWidth[1] * 0.62f * side, e0.Y + ex / el * HalfWidth[1] * 0.62f * side);
            Oval(fb, _where, c, 0, er, er, 24, 26, 28, 0.95f, murk * 0.7f, _pond.Sun, water: _pond.Pixels);
        }
    }

    /// <summary>
    /// Its shadow on the bed: the same body painted dark and blurred, shifted
    /// down and to the right (worked out in LaySpine). KoiSchool paints every
    /// fish's shadow BEFORE any fish, because the shadows lie on the bed, under
    /// everything that swims (painted inside each fish's own Draw, a shallow
    /// fish's shadow landed on top of a deeper fish). Nothing to paint when the
    /// bed is out of sight.
    /// </summary>
    public void DrawShadow(FrameBuffer fb)
    {
        if (_shadowAlpha <= 0) return;
        PaintShadow(fb, _where, _shadowDx, _shadowDy, _shadowAlpha, _shadowSoft);
    }

    /// <summary>A pair of side fins at spine point i, swept back from the body by "sweep" radians.</summary>
    private void Fins(FrameBuffer fb, int i, float len, float wid, float sweep, float murk, bool[]? mark = null)
    {
        PointF p = Spine[i], q = Spine[Math.Max(0, i - 1)];
        float back = MathF.Atan2(p.Y - q.Y, p.X - q.X);                         // pointing toward the tail
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            float a = back - side * (MathF.PI / 2 - sweep);
            float nx = MathF.Cos(back + side * -MathF.PI / 2), ny = MathF.Sin(back + side * -MathF.PI / 2);
            float l = _len * len * _scale;
            var c = new PointF(p.X + nx * HalfWidth[i] * 0.8f + MathF.Cos(a) * l * 0.5f, p.Y + ny * HalfWidth[i] * 0.8f + MathF.Sin(a) * l * 0.5f * Ripples.Squash);
            Oval(fb, _where, c, a, l * 0.5f, _len * wid * 0.5f * _scale, _fin.R, _fin.G, _fin.B, _finAlpha * 0.9f, murk, _pond.Sun, water: _pond.Pixels, mark: mark);
        }
    }

    /// <summary>
    /// A koi variety: its colours and where they go. Real varieties, chosen
    /// so a pond has a mix (and the first few fish are the classic ones):
    /// kohaku (white with red), sanke (white, red and small black), showa
    /// (black with red and white), yamabuki ogon (metallic gold), chagoi
    /// (bronze), asagi (blue-grey back, orange sides), tancho (white with one
    /// red spot on the head), and plain red-orange.
    /// </summary>
    private static ((int, int, int)[] Palette, byte[] Pattern, (int, int, int) Fin, float FinAlpha, bool Metal) Variety(int index, Random rng)
    {
        (int, int, int) white = (244, 240, 230), red = (214, 58, 34), black = (28, 28, 32), gold = (238, 178, 52),
            bronze = (156, 116, 74), slate = (112, 134, 152), orange = (232, 108, 40);
        int[] order = [0, 1, 2, 3, 0, 4, 5, 6, 1, 7, 3, 2];
        int kind = index < order.Length ? order[index] : rng.Next(8);
        var pattern = new byte[PatLen * PatWide];
        void Blob(byte colour, float s0, float v0, float rs, float rv)
        {
            float wob = (float)(rng.NextDouble() * MathF.Tau);
            for (int si = 0; si < PatLen; si++)
                for (int vi = 0; vi < PatWide; vi++)
                {
                    float s = si / (PatLen - 1f), v = vi / (PatWide - 1f) * 2 - 1;
                    float ds = (s - s0) / rs, dv = (v - v0) / rv;
                    float edge = 1 + 0.22f * MathF.Sin(s * 23 + v * 5 + wob) + 0.12f * MathF.Sin(v * 13 - s * 7);   // ragged, like real markings
                    if (ds * ds + dv * dv < edge) pattern[si * PatWide + vi] = colour;
                }
        }
        // Palette slot 0 is the base colour; the others are the markings.
        (int, int, int)[] palette;
        (int, int, int) fin = (236, 232, 226);
        float finAlpha = 0.5f;
        bool metal = false;
        switch (kind)
        {
            case 0: // kohaku
                palette = [white, red];
                for (int k = 0; k < 2 + rng.Next(2); k++)
                    Blob(1, 0.12f + 0.62f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.6f, 0.1f + 0.12f * (float)rng.NextDouble(), 0.7f + 0.5f * (float)rng.NextDouble());
                break;
            case 1: // sanke
                palette = [white, red, black];
                for (int k = 0; k < 2 + rng.Next(2); k++)
                    Blob(1, 0.12f + 0.62f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.6f, 0.09f + 0.1f * (float)rng.NextDouble(), 0.6f + 0.5f * (float)rng.NextDouble());
                for (int k = 0; k < 3 + rng.Next(3); k++)
                    Blob(2, 0.25f + 0.6f * (float)rng.NextDouble(), (float)(rng.NextDouble() * 2 - 1) * 0.7f, 0.035f + 0.03f * (float)rng.NextDouble(), 0.25f + 0.2f * (float)rng.NextDouble());
                break;
            case 2: // showa
                palette = [black, red, white];
                for (int k = 0; k < 2; k++)
                    Blob(1, 0.1f + 0.5f * (float)rng.NextDouble(), (float)(rng.NextDouble() - 0.5) * 0.8f, 0.1f + 0.1f * (float)rng.NextDouble(), 0.6f + 0.4f * (float)rng.NextDouble());
                for (int k = 0; k < 2; k++)
                    Blob(2, 0.3f + 0.5f * (float)rng.NextDouble(), (float)(rng.NextDouble() * 2 - 1) * 0.6f, 0.07f + 0.08f * (float)rng.NextDouble(), 0.4f + 0.3f * (float)rng.NextDouble());
                fin = (60, 56, 60);
                break;
            case 3: // yamabuki ogon
                palette = [gold];
                fin = (246, 206, 110);
                metal = true;
                break;
            case 4: // chagoi
                palette = [bronze];
                fin = (170, 132, 92);
                finAlpha = 0.55f;
                metal = true;
                break;
            case 5: // asagi: blue-grey back, orange along the sides
                palette = [slate, orange];
                for (int si = 0; si < PatLen; si++)
                    for (int vi = 0; vi < PatWide; vi++)
                        if (MathF.Abs(vi / (PatWide - 1f) * 2 - 1) > 0.72f - 0.1f * MathF.Sin(si * 0.2f)) pattern[si * PatWide + vi] = 1;
                fin = (226, 140, 96);
                break;
            case 6: // tancho: one red spot on the head
                palette = [white, red];
                Blob(1, 0.1f, 0, 0.065f, 0.55f);
                break;
            default: // red-orange
                palette = [orange];
                fin = (240, 150, 96);
                break;
        }
        return (palette, pattern, fin, finAlpha, metal);
    }
}
