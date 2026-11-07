using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// The farm's yard and stock, up on the hill behind the field (his ask:
/// "maybe some stuff going on back there... maybe some horses, cows, pigs,
/// chickens"): two horses in the paddock past the silos, three cows grazing
/// among the trees left of the house, three pigs in their pen between the
/// orchard and the windmill, a rooster and four hens round the scarecrow's
/// feet, and the farm dog, which lives in the doghouse, sits in the yard,
/// trots the road to the barn and back, and barks at the coyotes and the
/// raccoon.
///
/// FEYNMAN VERSION: every animal here is a resident like the turkeys: it has
/// a MIND from the shared kit (Core/Minds), one doing at a time with a timer
/// and weighted dice for the next, a PACE that eases its speed, and a pen it
/// keeps to (a band of the hill between its back line and its front line).
/// The pictures are FarmArt's, painted once per pose to a sheet; a walk is
/// paged by the distance walked (a planted foot stays planted), a turn is
/// three narrowing pages and the mirror, a tail swishes on the clock. They
/// are stamped through the farm stencil (OpenFromFarm), so the field, the
/// fence, the near ground and the maples all stand in front of them; the
/// paddock's front rail and the pen's front boards are stamped over them
/// each frame so they stand INSIDE their fences. Sized by the hill's one
/// scale (ThanksgivingScenery.HillMetresToU): a horse is 2.4 m long, a hen
/// 0.42, the dog 1.0, all beside a 7 m house.
/// </summary>
internal sealed class FarmYard
{
    private enum Kind { Horse, Cow, Pig, Hen, Dog }
    private enum Doing { Stand, Walk, Trot, Graze, Lie, Peck, Sit, Bark, Home, Run }

    private sealed class Beast
    {
        public Kind Kind;
        public int Look;                            // which coat; for the hens, 0 is the rooster
        public float X, Y;                          // the feet
        public int Dir = 1;
        public float GoalX, GoalY;
        public Mind<Doing> Mind = new(Doing.Stand);
        public Pace Pace = null!;
        public float Clock, Walked;
        public float Busy, Timid;                   // the dials
        public float TurnT = -1f;                   // seconds into a turn on the spot, -1 none
        public float Head;                          // the head's height dial, eased: 0 up, 1 at the grass
        public (float X0, float X1, float Back, float Front) Pen;
        public float Along;                         // the dog on the road: how far along it, 0 at the porch, 1 at the barn
        public float NextBark;
        public float Rested;                        // the dog: the clock before which it will not start another bout of barking (it barked at the coyotes without a pause for fifty seconds, and every squirrel in the patch within earshot froze for all of it)
    }

    private sealed class Sheets
    {
        public float K; public int W, H, Ox, By;
        public Sprite[] Walk = [], Trot = [], Stand = [], Down = [], Lie = [], Sit = [], Bark = [], Turn = [], Dip = [];   // Dip: the head on its way down or up, paged by the Head dial
    }

    private const int WalkPages = 24, DownPages = 6, TailPages = 8, TurnPages = 5, DipPages = 6;
    private const float TurnFor = 0.5f;                                            // at 0.24 s over three pages the turn was a snap
    private static readonly float[] Squash = [0.85f, 0.62f, 0.40f, 0.22f, 0.08f];

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;
    private readonly List<Beast> _all = new();
    private readonly Beast _dog;                    // the list is sorted by depth to draw, so the dog is kept by name
    private readonly Sheets[] _sheets = new Sheets[5];       // by Kind
    private readonly Sheets[] _horseCoats = new Sheets[3], _cowCoats = new Sheets[2];
    private Sheets _henSheets = null!, _roosterSheets = null!;
    private readonly Sprite _paddockFront, _penFront;
    private readonly PointF[] _road;
    private readonly float[] _roadLen;              // the road's cumulative length at each point
    private volatile bool _ready;
    private double _time;

    public FarmYard(ThanksgivingScenery s, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());
        _log = MindLog.Open();
        float u = s.U, m = s.HillMetresToU;
        // The pens: each a band of the hill, taken from the painter AS PAINTED. (A clamp here once lifted a band whose
        // hill dipped under the field's edge, and the horses stood above their own back rail; the painter's knoll under
        // the paddock and the pen keeps every band on the hill now, so the rails and the animals agree.)
        var paddock = s.Paddock; var pen = s.PigPen; var pasture = s.Pasture; var yard = s.HenYard;
        float[] busy = Traits.Deal(_rng, 14), timid = Traits.Deal(_rng, 14);
        int n = 0;
        void Add(Kind kind, int look, (float, float, float, float) p, float accel)
        {
            var b = new Beast { Kind = kind, Look = look, Pen = p, Busy = busy[n], Timid = timid[n], Dir = _rng.Next(2) == 0 ? 1 : -1 };
            n++;
            b.X = p.Item1 + (p.Item2 - p.Item1) * (0.25f + 0.5f * (float)_rng.NextDouble());
            b.Y = p.Item3 + (p.Item4 - p.Item3) * (float)_rng.NextDouble();
            b.Pace = new Pace(accel * u);
            b.Mind.Begin(kind == Kind.Dog ? Doing.Sit : Doing.Stand, 1f + 4f * (float)_rng.NextDouble());
            _all.Add(b);
        }
        Add(Kind.Horse, 0, paddock, 0.10f); Add(Kind.Horse, 1, paddock, 0.10f);
        Add(Kind.Cow, 0, pasture, 0.05f); Add(Kind.Cow, 1, pasture, 0.05f); Add(Kind.Cow, 0, pasture, 0.05f);
        Add(Kind.Pig, 0, pen, 0.06f); Add(Kind.Pig, 0, pen, 0.06f); Add(Kind.Pig, 0, pen, 0.06f);
        Add(Kind.Hen, 0, yard, 0.08f); for (int i = 1; i < 5; i++) Add(Kind.Hen, i, yard, 0.08f);
        Add(Kind.Dog, 0, yard, 0.20f);
        _dog = _all[^1];
        _dog.X = s.Doghouse.X; _dog.Y = s.Doghouse.Y; _dog.Dir = -1;
        _road = s.Road;
        _roadLen = new float[_road.Length];
        for (int i = 1; i < _road.Length; i++) _roadLen[i] = _roadLen[i - 1] + Dist(_road[i - 1], _road[i]);

        // The front rail of the paddock and the front boards of the pig pen: stamped over the animals.
        float railW = s.Paddock.X1 - s.Paddock.X0 + u * 0.02f, railH = u * 0.02f;
        _paddockFront = Sprite.Paint((int)railW + 2, (int)railH + 2, g =>
        {
            using var post = new Pen(Color.FromArgb(150, 132, 104), Math.Max(1f, u * 0.0025f));
            using var rail = new Pen(Color.FromArgb(150, 132, 104), Math.Max(1f, u * 0.0016f));
            float tall = u * 0.016f, y = railH;
            for (float px = u * 0.01f; px <= railW - u * 0.01f + 0.5f; px += u * 0.016f) g.DrawLine(post, px, y - tall, px, y);
            g.DrawLine(rail, u * 0.01f, y - tall * 0.78f, railW - u * 0.01f, y - tall * 0.78f);
            g.DrawLine(rail, u * 0.01f, y - tall * 0.42f, railW - u * 0.01f, y - tall * 0.42f);
        });
        float penW = s.PigPen.X1 - s.PigPen.X0 + u * 0.01f, penH = u * 0.012f;
        _penFront = Sprite.Paint((int)penW + 2, (int)penH + 2, g =>
        {
            using var board = new Pen(Color.FromArgb(96, 70, 44), Math.Max(1f, u * 0.0018f));
            g.DrawLine(board, 0, penH - u * 0.006f, penW, penH - u * 0.006f);
            g.DrawLine(board, 0, penH - u * 0.0025f, penW, penH - u * 0.0025f);
            for (float px = u * 0.004f; px <= penW; px += u * 0.014f) g.DrawLine(board, px, penH - u * 0.009f, px, penH);
        });
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ================================================================ painting (once)

    private Sheets Make(float metres, Action<Graphics, FarmArt.Pose> paint, bool trots, bool lies, bool sits, bool barks)
    {
        float k = Math.Max(6f, _s.U * _s.HillMetresToU * metres);
        int w = (int)(1.7f * k) + 4, h = (int)(1.2f * k) + 4, ox = (int)(0.7f * k) + 2, by = h - 2;
        var sh = new Sheets { K = k, W = w, H = h, Ox = ox, By = by };
        Sprite Page(FarmArt.Pose q, float squash = 1f) => FarmArt.Page(g => paint(g, q), k, w, h, ox, by, squash);
        sh.Walk = new Sprite[WalkPages];
        for (int i = 0; i < WalkPages; i++) sh.Walk[i] = Page(new FarmArt.Pose { Walk = i / (float)WalkPages });
        if (trots) { sh.Trot = new Sprite[WalkPages]; for (int i = 0; i < WalkPages; i++) sh.Trot[i] = Page(new FarmArt.Pose { Walk = i / (float)WalkPages, Trot = true }); }
        sh.Stand = new Sprite[TailPages];
        for (int i = 0; i < TailPages; i++) sh.Stand[i] = Page(new FarmArt.Pose { Tail = MathF.Sin(MathF.Tau * i / TailPages) });
        sh.Down = new Sprite[DownPages];
        for (int i = 0; i < DownPages; i++) sh.Down[i] = Page(new FarmArt.Pose { HeadDown = 1f, Lift = 0.5f + 0.5f * MathF.Sin(MathF.Tau * i / DownPages) });   // head at the grass, nodding (grazing, rooting, pecking)
        sh.Dip = new Sprite[DipPages];
        for (int i = 0; i < DipPages; i++) sh.Dip[i] = Page(new FarmArt.Pose { HeadDown = i / (float)(DipPages - 1) });   // the head between up and the grass (the eased dial picks the page; it used to jump from up to down at 0.85)
        if (lies) sh.Lie = [Page(new FarmArt.Pose { Lie = true })];
        if (sits) { sh.Sit = new Sprite[TailPages]; for (int i = 0; i < TailPages; i++) sh.Sit[i] = Page(new FarmArt.Pose { Sit = true, Tail = MathF.Sin(MathF.Tau * i / TailPages) }); }
        if (barks) sh.Bark = [Page(new FarmArt.Pose { Sit = true, Mouth = false }), Page(new FarmArt.Pose { Sit = true, Mouth = true })];
        sh.Turn = new Sprite[TurnPages];
        for (int i = 0; i < TurnPages; i++) sh.Turn[i] = Page(new FarmArt.Pose(), Squash[i]);
        return sh;
    }

    private void Build()
    {
        FarmArt.HorseCoat[] hc = [FarmArt.HorseCoat.Bay, FarmArt.HorseCoat.Chestnut, FarmArt.HorseCoat.Grey];
        for (int i = 0; i < 3; i++) { var coat = hc[i]; _horseCoats[i] = Make(2.4f, (g, q) => FarmArt.Horse(g, q, coat), true, false, false, false); }
        _cowCoats[0] = Make(2.3f, (g, q) => FarmArt.Cow(g, q, FarmArt.CowCoat.Holstein, 2), false, false, false, false);
        _cowCoats[1] = Make(2.3f, (g, q) => FarmArt.Cow(g, q, FarmArt.CowCoat.Jersey, 0), false, false, false, false);
        _sheets[(int)Kind.Pig] = Make(1.2f, (g, q) => FarmArt.Pig(g, q), false, true, false, false);
        _henSheets = Make(0.42f, (g, q) => FarmArt.Hen(g, q, false), false, false, false, false);
        _roosterSheets = Make(0.48f, (g, q) => FarmArt.Hen(g, q, true), false, false, false, false);
        _sheets[(int)Kind.Dog] = Make(1.0f, (g, q) => FarmArt.Dog(g, q), true, false, true, true);
    }

    private Sheets SheetsOf(Beast b) => b.Kind switch
    {
        Kind.Horse => _horseCoats[b.Look % 3],
        Kind.Cow => _cowCoats[b.Look % 2],
        Kind.Hen => b.Look == 0 ? _roosterSheets : _henSheets,
        _ => _sheets[(int)b.Kind],
    };

    // ================================================================ living

    /// <summary>A standing animal's speed in U a second, by kind: metres a second at the hill's scale.</summary>
    private float SpeedOf(Beast b, Doing d) => _s.U * _s.HillMetresToU * (b.Kind, d) switch
    {
        (Kind.Horse, Doing.Trot) => 3.2f, (Kind.Horse, _) => 1.0f,
        (Kind.Cow, _) => 0.7f,
        (Kind.Pig, _) => 0.8f,
        (Kind.Hen, Doing.Run) => 2.2f, (Kind.Hen, _) => 0.5f,
        (Kind.Dog, Doing.Trot) => 3.0f, (Kind.Dog, Doing.Run) => 5.0f, (Kind.Dog, _) => 1.2f,
    };

    public void Update(float dt)
    {
        _time += dt;
        if (!_ready || _time < 4.0) return;         // pinned start, like the patch's: a seed is a scenario
        float u = _s.U;
        foreach (Beast b in _all)
        {
            b.Mind.Tick(dt);
            b.Clock += dt;
            _log?.WriteLine($"{_time,8:F3} yard {b.Kind.ToString().ToLowerInvariant()}{b.Look} frame x {b.X:F1} y {b.Y:F1} v {b.Pace.Speed:F2} {b.Mind.Doing}{(b.TurnT >= 0f ? " turn" : "")}");
            // The head eases between up and down: a horse lifts its head over a second, it does not snap.
            float wantHead = b.Mind.Doing is Doing.Graze or Doing.Peck ? 1f : 0f;
            b.Head += (wantHead - b.Head) * Math.Min(1f, dt * 3f);
            if (b.TurnT >= 0f)
            {
                b.TurnT += dt;
                if (b.TurnT >= TurnFor / 2 && b.TurnT - dt < TurnFor / 2) b.Dir = -b.Dir;
                if (b.TurnT >= TurnFor) b.TurnT = -1f;
                continue;
            }
            Act(b, dt);
            if (b.Mind.Over) Decide(b);
        }
    }

    /// <summary>Faces a stopped animal toward its goal before it sets off: a turn on the spot if the goal is behind it.</summary>
    private void Face(Beast b)
    {
        int dir = b.GoalX >= b.X ? 1 : -1;
        if (dir == b.Dir) return;
        if (b.Kind == Kind.Hen) { b.Dir = dir; return; }           // a hen just hops round
        b.TurnT = 0f;
    }

    /// <summary>A spot in the animal's pen to walk to, at least "minDx" away along the hill.</summary>
    private void PickGoal(Beast b, float minDx)
    {
        var p = b.Pen;
        float x, y, inset = SheetsOf(b).K * 1.0f;                    // the nose reaches a full K past the feet point (the page is 1.7 K wide, the feet 0.7 K in): keep it inside the rails
        // A goal on the OTHER side of the pen's middle, so a walk is a walk (in a pen three lengths wide, every goal was
        // within the animal's own length and the horses scooted a few pixels and turned); "minDx" is capped at what
        // the pen allows, else the tries all fail and the goal is wherever the last roll fell.
        float lo = p.X0 + inset, hi = p.X1 - inset, mid = (lo + hi) / 2;
        if (b.X > mid) hi = mid; else lo = mid;
        minDx = Math.Min(minDx, (hi - lo) * 0.5f);
        int tries = 0;
        do
        {
            x = lo + (hi - lo) * (float)_rng.NextDouble();
            y = p.Back + (p.Front - p.Back) * (float)_rng.NextDouble();
        } while ((MathF.Abs(x - b.X) < minDx || InPond(b, x, y)) && ++tries < 24);
        b.GoalX = x;
        b.GoalY = y;
        Face(b);
    }

    /// <summary>Is a goal in the pasture's pond (the cows stand round it, not in it)? The feet and a little of the body either side.</summary>
    private bool InPond(Beast b, float x, float y)
    {
        if (b.Kind != Kind.Cow) return false;
        var (at, rx, ry) = _s.Pond;
        float k = SheetsOf(b).K, dx = (x - at.X) / (rx + 0.5f * k), dy = (y - at.Y) / (ry + 0.2f * k);
        return dx * dx + dy * dy < 1f;
    }

    private void Decide(Beast b)
    {
        float u = _s.U;
        b.Clock = 0f;
        switch (b.Kind)
        {
            case Kind.Horse:
            {
                // Graze mostly; stand and swish; walk to another spot; now and then a trot down the rails for the joy of it.
                int face = Dice.Pick(_rng, [0.45f, 0.25f, 0.25f + 0.2f * b.Busy, 0.04f + 0.10f * b.Busy]);
                if (face == 0) b.Mind.Begin(Doing.Graze, 6f + 12f * (float)_rng.NextDouble());
                else if (face == 1) b.Mind.Begin(Doing.Stand, 3f + 6f * (float)_rng.NextDouble());
                else if (face == 2) { PickGoal(b, 0.05f * u); b.Mind.Begin(Doing.Walk, 20f); }
                else { float inset = SheetsOf(b).K * 1.0f; b.GoalX = b.X < (b.Pen.X0 + b.Pen.X1) / 2 ? b.Pen.X1 - inset : b.Pen.X0 + inset; b.GoalY = b.Y; Face(b); b.Mind.Begin(Doing.Trot, 12f); }
                break;
            }
            case Kind.Cow:
            {
                int face = Dice.Pick(_rng, [0.6f, 0.25f, 0.15f + 0.15f * b.Busy]);
                if (face == 0) b.Mind.Begin(Doing.Graze, 10f + 20f * (float)_rng.NextDouble());
                else if (face == 1) b.Mind.Begin(Doing.Stand, 4f + 8f * (float)_rng.NextDouble());     // standing, chewing the cud
                else { PickGoal(b, 0.04f * u); b.Mind.Begin(Doing.Walk, 25f); }
                break;
            }
            case Kind.Pig:
            {
                // Root with the snout in the earth, trot about, or lie in the wallow (the pen's right half).
                int face = Dice.Pick(_rng, [0.45f, 0.25f + 0.2f * b.Busy, 0.15f + 0.2f * (1 - b.Busy)]);
                if (face == 0) b.Mind.Begin(Doing.Graze, 4f + 8f * (float)_rng.NextDouble());
                else if (face == 1) { PickGoal(b, 0.015f * u); b.Mind.Begin(Doing.Walk, 10f); }
                else
                {
                    float wx = b.Pen.X0 + (b.Pen.X1 - b.Pen.X0) * (0.60f + 0.2f * (float)_rng.NextDouble());
                    if (MathF.Abs(wx - b.X) > 0.01f * u) { b.GoalX = wx; b.GoalY = b.Pen.Back + (b.Pen.Front - b.Pen.Back) * 0.55f; Face(b); b.Mind.Begin(Doing.Walk, 10f); b.Busy = -1f; }   // Busy -1: lies down on arrival
                    else b.Mind.Begin(Doing.Lie, 10f + 20f * (float)_rng.NextDouble());
                }
                break;
            }
            case Kind.Hen:
            {
                int face = Dice.Pick(_rng, [0.5f, 0.2f, 0.3f + 0.2f * b.Busy]);
                if (face == 0) b.Mind.Begin(Doing.Peck, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 1) b.Mind.Begin(Doing.Stand, 0.5f + 2f * (float)_rng.NextDouble());
                else { PickGoal(b, 0.008f * u); b.Mind.Begin(Doing.Walk, 6f); }
                break;
            }
            case Kind.Dog:
            {
                // In the house a while, out to sit by it, a trot down the road to the barn and back, a sit at the barn.
                switch (b.Mind.Doing)
                {
                    case Doing.Home: b.Mind.Begin(Doing.Sit, 4f + 10f * (float)_rng.NextDouble()); b.X = _s.Doghouse.X; b.Y = _s.Doghouse.Y; b.Dir = -1; b.Along = 0f; break;
                    case Doing.Sit when b.Along <= 0.01f:
                        if (_rng.NextDouble() < 0.45) b.Mind.Begin(Doing.Home, 15f + 40f * (float)_rng.NextDouble());
                        else { b.Mind.Begin(Doing.Trot, 40f); b.GoalX = 1f; b.Dir = 1; _log?.WriteLine($"{_time,8:F2} dog trots to the barn"); }   // GoalX 1: the far end of the road
                        break;
                    case Doing.Sit: b.Mind.Begin(Doing.Trot, 40f); b.GoalX = 0f; b.Dir = -1; _log?.WriteLine($"{_time,8:F2} dog trots home"); break;
                    default: b.Mind.Begin(Doing.Sit, 3f + 6f * (float)_rng.NextDouble()); break;
                }
                break;
            }
        }
    }

    /// <summary>Where the road is at "along" (0 the porch end, 1 the barn end).</summary>
    private PointF RoadAt(float along)
    {
        float d = Math.Clamp(along, 0f, 1f) * _roadLen[^1];
        for (int i = 1; i < _road.Length; i++)
            if (d <= _roadLen[i])
            {
                float t = (d - _roadLen[i - 1]) / MathF.Max(1e-3f, _roadLen[i] - _roadLen[i - 1]);
                return new PointF(_road[i - 1].X + (_road[i].X - _road[i - 1].X) * t, _road[i - 1].Y + (_road[i].Y - _road[i - 1].Y) * t);
            }
        return _road[^1];
    }

    private void Act(Beast b, float dt)
    {
        float u = _s.U;
        switch (b.Mind.Doing)
        {
            case Doing.Walk:
            case Doing.Trot:
            case Doing.Run:
            {
                if (b.Kind == Kind.Dog && b.Mind.Doing == Doing.Trot)
                {
                    // Along the road, to whichever end GoalX names; the road's own line gives the place.
                    float speed = SpeedOf(b, Doing.Trot);
                    b.Pace.Want = speed;
                    float step = b.Pace.Tick(dt);
                    b.Along += (b.GoalX > 0.5f ? 1f : -1f) * step / _roadLen[^1];
                    b.Walked += step;
                    PointF at = RoadAt(b.Along);
                    b.X = at.X; b.Y = at.Y;
                    if (b.Along >= 1f || b.Along <= 0f)
                    {
                        b.Along = Math.Clamp(b.Along, 0f, 1f); b.Pace.Halt();
                        if (b.Along >= 1f) { b.Mind.Begin(Doing.Stand, 2f + 4f * (float)_rng.NextDouble()); b.Dir = -1; }   // at the barn: a sniff round, then back
                        else { b.Mind.Begin(Doing.Sit, 3f + 8f * (float)_rng.NextDouble()); b.Dir = -1; }
                        b.Clock = 0f;
                    }
                    break;
                }
                float dx = b.GoalX - b.X, dy = b.GoalY - b.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
                float want = SpeedOf(b, b.Mind.Doing);
                float arrive = 0.004f * u;
                if (dist < arrive)
                {
                    b.Pace.Halt();
                    if (b.Kind == Kind.Pig && b.Busy < 0f) { b.Busy = (float)_rng.NextDouble(); b.Mind.Begin(Doing.Lie, 10f + 20f * (float)_rng.NextDouble()); }
                    else b.Mind.Begin(b.Kind == Kind.Hen ? Doing.Peck : b.Kind == Kind.Horse && b.Mind.Doing == Doing.Trot ? Doing.Stand : Doing.Graze, 2f + 6f * (float)_rng.NextDouble());
                    b.Clock = 0f;
                    break;
                }
                b.Pace.Want = Math.Min(want, MathF.Max(0.2f * want, dist * 3f));
                float d = b.Pace.Tick(dt);
                if (d <= 0f) break;
                b.X += dx / dist * d; b.Y += dy / dist * d;
                b.Walked += d;
                int dir = dx >= 0 ? 1 : -1;
                if (dir != b.Dir && b.Kind != Kind.Hen) { b.TurnT = 0f; b.Pace.Halt(); }
                else b.Dir = dir;
                break;
            }
            case Doing.Bark:
                if (b.Clock >= b.NextBark)
                {
                    _board.Post(Sense.Loud, new PointF(b.X, b.Y), 0.22f * u, 0.6f, this);   // heard across the yard and the hill, not down in the pumpkin patch (0.35 U reached it)
                    b.NextBark = b.Clock + 0.6f;
                }
                break;
        }
        // The dog hears a threat (the raccoon, the coyotes, the owl) and barks at it from where it sits; the hens run from the dog and from a bark.
        if (b.Kind == Kind.Dog && b.Mind.Doing is Doing.Sit or Doing.Stand && _time >= b.Rested && _board.Nearest(Sense.Threat, new PointF(b.X, b.Y), 2.0f * u) is Notice threat)
        {
            b.Dir = threat.At.X >= b.X ? 1 : -1;
            b.Mind.Begin(Doing.Bark, 2.5f + 2f * (float)_rng.NextDouble()); b.Clock = 0f; b.NextBark = 0f;
            b.Rested = (float)_time + 15f + 25f * (float)_rng.NextDouble();   // a bout, then it settles: a threat that stays (the pack on the lane) gets a bark now and then, not a siren
            _log?.WriteLine($"{_time,8:F2} dog barks at {threat.Kind}");
        }
        if (b.Kind == Kind.Hen && b.Mind.Doing != Doing.Run)
        {
            Beast dog = _dog;
            bool near = dog.Mind.Doing is Doing.Trot or Doing.Run && MathF.Abs(dog.X - b.X) < 0.04f * u && MathF.Abs(dog.Y - b.Y) < 0.03f * u;
            if (near || (dog.Mind.Doing == Doing.Bark && _rng.NextDouble() < dt * 2))
            {
                float away = dog.X >= b.X ? -1f : 1f;
                b.GoalX = Math.Clamp(b.X + away * (0.02f + 0.02f * (float)_rng.NextDouble()) * u, b.Pen.X0, b.Pen.X1);
                b.GoalY = Math.Clamp(b.Y + (0.004f * u) * ((float)_rng.NextDouble() - 0.5f), b.Pen.Back, b.Pen.Front);
                b.Dir = away > 0 ? 1 : -1;
                b.Mind.Begin(Doing.Run, 2f); b.Clock = 0f;
            }
        }
    }

    // ================================================================ drawing

    private void Stamp(FrameBuffer fb, Sheets sh, Sprite page, Beast b)
    {
        bool mirror = b.Dir < 0;
        int left = (int)MathF.Round(b.X) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(b.Y) - sh.By, 1f, _s.OpenFromFarm, mirror);
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_ready) return;
        _all.Sort((a, b) => a.Y.CompareTo(b.Y));
        foreach (Beast b in _all)
        {
            if (b.Mind.Doing == Doing.Home) continue;                           // in the doghouse, out of sight
            Sheets sh = SheetsOf(b);
            Sprite page;
            if (b.TurnT >= 0f)
            {
                float q = b.TurnT / TurnFor;
                int pg = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                page = sh.Turn[pg];
            }
            else switch (b.Mind.Doing)
            {
                case Doing.Walk: case Doing.Run: case Doing.Trot:
                {
                    bool trot = b.Mind.Doing is Doing.Trot or Doing.Run && sh.Trot.Length > 0;
                    float stride = sh.K * (trot ? 0.34f : b.Kind == Kind.Hen ? 0.4f : 0.26f) * 2f;   // one page cycle is two steps of the stride
                    float cyc = b.Walked / stride;
                    page = (trot ? sh.Trot : sh.Walk)[(int)((cyc - MathF.Floor(cyc)) * WalkPages) % WalkPages];
                    break;
                }
                case Doing.Graze: case Doing.Peck:
                    page = b.Head < 0.9f ? sh.Dip[(int)(b.Head / 0.9f * (DipPages - 1))] : sh.Down[(int)(b.Clock * (b.Kind == Kind.Hen ? 6f : 1.4f) * DownPages) % DownPages];   // the head comes down page by page, then nods
                    break;
                case Doing.Lie: page = sh.Lie[0]; break;
                case Doing.Sit: page = sh.Sit[(int)(b.Clock * 1.2f * TailPages) % TailPages]; break;
                case Doing.Bark: page = sh.Bark[(int)(b.Clock * 5f) % 2]; break;
                default: page = b.Head > 0.1f ? sh.Dip[(int)(b.Head * (DipPages - 1))] : sh.Stand[(int)(b.Clock * 0.9f * TailPages) % TailPages]; break;   // standing after a graze: the head comes back up page by page
            }
            Stamp(fb, sh, page, b);
        }
        // The pens' front sides, over the animals.
        _paddockFront.Draw(fb, (int)(_s.Paddock.X0 - _s.U * 0.01f), (int)(_s.Paddock.Front - _paddockFront.Height + 2), 1f, _s.OpenFromFarm);
        _penFront.Draw(fb, (int)(_s.PigPen.X0 - _s.U * 0.005f), (int)(_s.PigPen.Front - _penFront.Height + 2), 1f, _s.OpenFromFarm);
    }
}
