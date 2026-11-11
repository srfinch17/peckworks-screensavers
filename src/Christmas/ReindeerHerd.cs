using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// The REINDEER: a small herd (a big bull with a wide rack, cows with smaller antlers, and, from three animals up, a
/// calf as the last of them) that lives on the far side of the snowfield, in front of the pines. They graze the way
/// reindeer do: the animal PAWS the snow with a foreleg (three or four scrapes, snow flying behind the hoof), then
/// noses down into the hole and eats. They walk, look up, lie down together and get up (hind end first) with the snow
/// settled on their backs, shake it off, follow Santa across the sky with their heads, keep together, and bolt for a
/// short trot when something dangerous turns up. The calf plays: springs, a dash, a skid, and now and then it tries
/// to FLY like the ones overhead (a run, a leap with its legs tucked, a hang with sparkles, a plop into the snow).
///
/// FEYNMAN VERSION. Each animal has two machines running side by side, as the Thanksgiving deer does:
///   - A DOING is what its mind wants (graze, look, walk, lie, alert, trot, watch Santa, shake, gambol). It is rolled
///     with weighted dice when the last one ends; every animal's dials (timid, curious) are dealt at birth, so no
///     two behave alike.
///   - A GAIT is what its body is doing right now (standing, rising from a stand into a walk, walking, braking to a
///     stop, settling its feet square, turning on the spot, pawing, lying down, lying, getting up, shaking, leaping).
///     The body never jumps from one gait to the next: a walk starts by playing the settle pages backward, a stop
///     is a brake that ends exactly on the page where all four feet are planted, and a turn is a few narrowing
///     pages. A walk is paged by the DISTANCE walked (never the clock), which is what keeps a planted hoof planted.
///
/// DOINGS AND WHAT CAN CUT EACH SHORT (and the state every exit leaves):
///   Graze  (scan, paw x3, eat, chew)  : any reaction. A scrape in progress is finished first (the pending slot holds the
///                                       reaction), then the animal is Stand with the neck easing to wherever the next doing wants.
///   Look   (head up, ears forward)    : any stronger reaction. Leaves Stand.
///   Walk   (distance or a timer)      : an edge, a prop in the way, any reaction (every walk brakes onto the rest page first).
///   Lie    (down, lie, up)            : a Flee gets it up fast; a Look or Freeze only lifts the head. Leaves Stand after Up.
///   Alert  (head high, still)         : the timer. Leaves Stand, doing Look.
///   Watch  (head follows Santa)       : Santa leaving the screen, the timer, a danger.
///   Trot   (flee, 2 to 3.5 s)         : the edge, the timer. Leaves Stand, doing Look, turned to face what it fled.
///   Shake  (a quick whole-body shake) : none; it is 1 s long. Leaves Stand, doing Look.
///   Gambol (the calf's play)          : only a danger (Threat, a full Loud, a Freeze or Flee) or the cow alarmed. Leaves Stand.
///
/// SENSES: each animal owns an Attention (Senses.cs). Santa overhead is a Movement notice from the scene (a glance, now and
/// then): the calf always lifts its head to follow him, one or two others do. A Threat or a full Loud makes the first one
/// to feel it bolt; its Friend notice and a short delay get the rest to follow.
///
/// PINS: Movement at a walking or trotting animal (reach 0.25 U, weight 0.3): only after it has walked more than 2 s, once a
/// second while the bout lasts, then a rest of 6 s before the next bout. Friend (reach 0.6 U, weight 1) when one bolts: once
/// per alarm, never again within 5 s. Who is in reach of the Movement post: the hares (the fox's territory starts about 0.1 U
/// below the herd's rows), the feeder birds (about 0.05 to 0.15 U away at the bird table), and Santa-looking animals of its own kind.
///
/// TEST AIDS: PECKWORKS_REINDEER=paw|walk|lie|shake|alert|trot|gambol|fly|watch|turn|graze|creep|bellow|nuzzle forces that at 26 s herd time (graze keeps them
/// grazing for ever, walk keeps the leader walking); PECKWORKS_SHEET=reindeer;file.png writes every page of every flip-book.
/// </summary>
internal sealed class ReindeerHerd : Resident
{
    private enum Doing { Graze, Creep, Look, Walk, Lie, Alert, Trot, Watch, Gambol, Shake, Nuzzle, Bellow }
    private enum Gait { Stand, Rising, Walking, Stopping, Settling, Turning, Pawing, Down, Lying, Up, Shaking, Leaping }

    /// <summary>One bit of flying snow, vapour, a puff or a sparkle. Kinds: 0 snow bit, 1 breath, 2 sparkle, 3 puff.</summary>
    private struct Fleck { public float X, Y, VX, VY, Age, Life, Size; public byte Kind; }

    private sealed class Rein
    {
        public ReindeerKind Kind;
        public ReindeerSet Set = null!;
        public float X, Depth, RowY;                        // RowY: the row's own height on the screen (constant: a size painted for it stays right along the whole row)
        public int Dir;
        public bool TrotMode;                               // the walk/trot book in use (a gait never changes book while it is going)
        public Doing Doing;
        public Gait Gait;
        public float Timer, Clock, GaitT, GaitFor = SettleFor, TurnT, TurnFor = 1.2f;
        public int TurnTo, TurnWhenSettled;
        public int TurnFrom, TurnSide = 1, TurnLevel;       // a turn: the way it faced, which half turn (1 toward us, -1 away from us), and the head pitch level it is held at
        public Pace Pace = null!;
        public Attention Senses = null!;
        public float Timid, Curious;
        public float WalkWant;                              // the speed the walk leans toward (px a second)
        public bool HasGoal, Following; public float GoalX;
        public float NeckF, NeckV, Restless;
        public float BiteT = -1, NextBite, ChewT = -1, EarT = -1, NextEar, Breath;
        // ---- the second pass (the review): turns, the entrance, drifting, a herd that walks on, small doings ----
        public int TurnReq; public bool TurnFast; public float TurnWait;        // a turn asked for: it waits until the head is up (TryTurn)
        public bool WalkIntent, StopPending;                                                  // a walk or trot is wanted (a turn or a rise on the way to it)
        public int FollowIdx = -1, FollowDir; public float FollowGo, FollowTry;  // falling in behind another animal (FollowDir 0: the leader's own facing, live)
        public bool Entering; public float EnterDelay;                           // walking in from the edge of the screen
        public float BlockCool, RoomCool, LieCool, NuzzleCool, BellowCool;       // rests: after a blocked start, a step to make room, a lie, a nuzzle, a bellow
        public float LookT; public bool LookPend; public double LookPendAt;      // a glance: the head lifted for a few seconds (LookT), waiting for a break (LookPend)
        public double PendingAt; public float WatchWait, WatchTry;               // a stale reaction is dropped; Santa: lift the head after a stagger, then wait for a break
        public bool ScanTurn; public int ScanPhase; public float ScanTotal;      // the slow look round the horizon (Doing.Look)
        public int GCycles, CSteps, CPhase, CDir; public float CDist;            // a meal of one or two cycles; a drift of one to three steps
        public int Partner = -1, NzPhase; public bool NzLead; public float NzT, NzFor, NzBreath;   // touching noses
        public float BellowT; public bool BellowPosted;
        public Doing LastDoing = Doing.Graze, SeenDoing = Doing.Graze; public float DoingAge;
        public int GamFrom, GamTo, PoseT; public float GamU, PlopT = -1;       // the calf's play poses are keys with drawn pages between them: where it is on the way, and which key it is heading for
        public float AlertWait, AlertTry, EchoT, EchoDist; public int EchoDir;   // a fox alarm lifts heads once, staggered; the calf creeps after its mother
        public float DriftAmp, VisitUntil; public double LastRestWalk = -99;                                                   // how much of the idle head drift is on (eased, so a head never jumps when it starts)
        public int BitesLeft;
        public int GPhase; public float GT, PawT, PawFor = PawTime, EatT; public int Scrapes;   // grazing: scan, paw, eat, chew
        public float MarkX, MarkA; public bool HasMark; public Sprite Mark = null!;             // the scraped hole in the snow (px, opacity)
        public float WatchRest, WalkSecs, PostT, RestT, TurnCool, FollowLag = -1, AlertT, LookX = float.NaN, WatchF = 0.1f;
        public float LieT, LieFor, Snow, DownT, UpT, UpFor = UpTime, LieAfter = -1, ShakeT;
        public bool AfterUpShake, ForceGambol;
        public Reaction Pending; public Notice PendingCause;
        public bool JustGrazed;
        public float Lift;                                  // the calf's leaps: pixels off the ground
        public int GPh, GN, GCount, GBouts; public float GG, GHomeX, GStay; public bool GOut, Fly;
        public float MarkDx;                                // how far ahead of the feet the nose lands when it is down, in heights
        public Fleck[] Fx = new Fleck[48];
        public int FxNext;
    }

    // ---- timings and speeds ----
    private const float BiteTime = 0.26f, EarTime = 0.24f;   // a bite is a nod eased down and up (about 8 frames each way); an ear flick eases out and back
    private const float SettleFor = 0.40f, PawTime = 0.92f, DownTime = 2.5f, UpTime = 1.8f, ShakeTime = 1.0f;
    private const float NeckSpring = 55f, NeckDamp = 14.5f;      // the neck eases to where it is wanted like a damped spring
    private const float SpringFor = 0.52f, SpringGround = 0.10f, BoundFor = 0.34f, BoundGround = 0.05f, SkidFor = 0.40f;
    private const float SpringLift = 0.55f, BoundLift = 0.20f, FlyPeak = 0.62f, SpringSpeed = 1.25f, BoundSpeed = 5.0f;   // in the calf's own heights (a second)

    // The herd stands by a cabin painted at STORYBOOK scale (about 37 pixels to the metre at 1080p where the herd's row gives about 54), so
    // beside it a true-size bull was nearly half the cabin's width. Every reindeer is drawn at this share of its real size.
    private const float HerdScale = 0.78f;

    private readonly Rein[] _r;
    private readonly int _mom;                                   // the cow the calf follows (the second animal; the only one when there is one)
    private readonly float _walkV, _trotV;                       // px a second, the same for the whole herd (a calf takes more, shorter steps)
    private readonly string? _force = Environment.GetEnvironmentVariable("PECKWORKS_REINDEER");
    private readonly StreamWriter? _log = MindLog.Open();
    private Sprite[] _flake = null!, _cloud = null!;
    private Sprite _spark = null!;
    private double _time, _nextGambol = 14, _friendRest = -99, _lastNoise = -99;
    private double _driftPhase;                    // where in its slow sweep from one end of the lane to the other the herd starts
    private const double DriftPeriod = 300.0;      // seconds for the herd's drift from one end of the lane and back (a loose group: each animal keeps its own bias)
    private double _alarmAt = -1; private float _alarmFrom;
    private bool _forced, _santaWas, _passCalfOk;
    private double _ledCool = 40, _nextLie = 90; private int _watchRank;   // the herd walks on rarely; lying down one at a time; heads lift one after another
    private double _alertCool = 20;               // a fox's alarm lifts the herd's heads at most once in 25 s
    private int _home;                           // the open stretch the herd lives in now (OpenSnow's index)
    private int _passAdultSlots;                    // per Santa pass: how many adults may follow him with their heads (0 or 1, so one on average or less)
    private bool _firstUpdateLogged;
    private long _tUpdate, _tCollect, _tDraw; private double _tReport;   // cost instruments, only when the mind log is on

    public int Count => _r.Length;
    public PointF Feet(int i) => new(_r[i].X, FootY(_r[i]));
    public bool IsCalf(int i) => _r[i].Kind == ReindeerKind.Calf;

    public ReindeerHerd(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        count = Math.Clamp(count, 0, 12);
        if (Environment.GetEnvironmentVariable("PECKWORKS_NOREIN") == "1") count = 0;   // test aid: the scene without the herd, for the cost comparison
        _r = new Rein[count];
        float[] timid = Traits.Deal(Rng, Math.Max(1, count)), curious = Traits.Deal(Rng, Math.Max(1, count));
        _driftPhase = Rng.NextDouble() * Math.Tau;
        var heights = new int[count];
        // THE HERD'S BAND (the scene's HerdBandTop to HerdBandBottom, under 0.03 U deep): every animal keeps its feet on ONE level row inside it
        // (a size painted for a row stays right along it), the rows a few pixels apart so the herd reads as one group on one stretch of snow,
        // and nobody else's feet go in it. The bull stands in the middle, the cow a little farther off, the calf a little nearer.
        // The lamp post's foot stands in the upper part of the band (CanStand refuses 11 px either side of it), which would cut the lane in two:
        // the rows are kept just below it, between its clearance and the band's bottom, so the herd walks past the post in FRONT of its foot.
        float lampClear = s.LampFoot.Y + 0.0125f * s.FieldScale(s.LampFoot.Y) * s.U + 1.5f;
        float rowTop = Math.Min(s.HerdBandBottom, Math.Max(s.HerdBandTop, lampClear)), rowBottom = s.HerdBandBottom;
        float[] rowFrac = [0.50f, 0.0f, 1.0f];
        for (int i = 0; i < count; i++)
        {
            ReindeerKind kind = i == 0 ? ReindeerKind.Bull : (count >= 3 && i == count - 1) ? ReindeerKind.Calf : ReindeerKind.Cow;
            float rowY = rowTop + (rowBottom - rowTop) * rowFrac[i % 3];
            var a = new Rein { Kind = kind, Dir = Rng.Next(2) == 0 ? 1 : -1, Depth = 0f, RowY = rowY };
            float m = s.Metre(rowY);
            heights[i] = Math.Max(10, (int)MathF.Round(m * HerdScale * (kind == ReindeerKind.Bull ? 1.15f : kind == ReindeerKind.Cow ? 1.05f : 0.63f)));
            a.Timid = kind == ReindeerKind.Calf ? 0.45f + 0.5f * timid[i] : kind == ReindeerKind.Bull ? 0.7f * timid[i] : timid[i];
            a.Curious = kind == ReindeerKind.Calf ? 0.8f + 0.2f * curious[i] : curious[i];
            a.Senses = new Attention(a.Timid, a.Curious) { Keen = 1.2f };
            a.Restless = (float)Rng.NextDouble() * 10;
            a.NeckF = 0.4f;
            a.GPhase = 0; a.GT = 0.6f + 1.4f * (float)Rng.NextDouble(); a.GCycles = 1;
            a.NextBite = 0.5f; a.NextEar = 1f + 3f * (float)Rng.NextDouble();
            a.Breath = 1f + 3f * (float)Rng.NextDouble();
            a.Timer = 999f;
            a.Doing = Doing.Graze;
            a.NuzzleCool = 20f * (float)Rng.NextDouble(); a.BellowCool = 20f * (float)Rng.NextDouble();
            _r[i] = a;
        }
        _log?.WriteLine($"Reindeer herd of {count} built for {s.Width}x{s.Height}");
        if (_log != null)
            for (int i = 0; i < count; i++)
            {
                var sb = new System.Text.StringBuilder();
                bool inBad = false;
                for (int x = 0; x <= s.Width; x += 6)
                {
                    bool bad = !s.CanStand(x, _r[i].RowY, 0.3f * heights[i]);
                    if (bad && !inBad) sb.Append($" [{x}"); else if (!bad && inBad) sb.Append($"..{x}]");
                    inBad = bad;
                }
                _log.WriteLine($"Reindeer{i} row {_r[i].RowY:F0}: cannot stand at x in{sb}{(inBad ? "..end]" : "")} (band {s.HerdBandTop:F0} to {s.HerdBandBottom:F0}, crest max {s.CrestMax:F0})");
            }
        _mom = count >= 2 ? 1 : 0;
        float href = count == 0 ? 20 : heights[_mom];
        _walkV = 0.72f * href;
        _trotV = 1.75f * href;
        // THE ENTRANCE: the herd walks in from one side of the screen, the bull first and deepest in, each cow some seconds behind, the calf
        // trotting to keep up. (Not a side with the cabin in the way: the left side has the lane to 0.40 w, the right side from 0.72 w.)
        int enterDir = Rng.Next(2) == 0 ? 1 : -1;                         // 1: walking in from the left edge
        _home = enterDir > 0 ? 0 : 1;                                     // the wide clear snow at the left, or (from the right) the stretch above the pond
        float x0 = float.MaxValue, x1 = float.MinValue;
        for (float x = s.Width * OpenSnow[_home].Lo; count > 0 && x <= s.Width * OpenSnow[_home].Hi; x += 3f) if (RestOk(x - 0.8f * href, href) && RestOk(x, href) && RestOk(x + 0.8f * href, href) && s.CanStand(x, _r[0].RowY, 0.45f * href)) { x0 = MathF.Min(x0, x); x1 = MathF.Max(x1, x); }
        if (x1 < x0) { x0 = s.Width * OpenSnow[_home].Lo; x1 = s.Width * OpenSnow[_home].Hi; }
        float deep = enterDir > 0 ? x1 : x0;
        float[] offs = new float[count]; float cum = 0;
        for (int i = 0; i < count; i++) { offs[i] = cum; cum += (i + 1 < count && _r[i + 1].Kind == ReindeerKind.Calf ? 2.1f : 2.8f) * href; }
        float squeeze = cum - 2.8f * href > (x1 - x0) ? (x1 - x0) / (cum - 2.8f * href) : 1f;    // too many for the stretch: closer together
        for (int i = 0; i < count; i++)
        {
            Rein a = _r[i];
            a.Pace = new Pace(_walkV * 2.2f);
            int rank = a.Kind == ReindeerKind.Calf ? count - 1 : i;          // order of arrival (the calf last)
            a.GoalX = Math.Clamp(deep - enterDir * offs[i] * squeeze, x0, x1);
            a.Dir = enterDir;
            a.X = enterDir > 0 ? -3f * heights[i] : s.Width + 3f * heights[i];
            a.Entering = true;
            a.EnterDelay = 0.2f + rank * (4.2f + 1.0f * (float)Rng.NextDouble()) + (a.Kind == ReindeerKind.Calf ? 1.5f : 0f);
            a.Doing = Doing.Walk;
            a.HasGoal = true;
        }
        for (int i = 0; i < count; i++) _log?.WriteLine($"Reindeer{i} {_r[i].Kind} H {heights[i]} enters {(enterDir > 0 ? "from the left" : "from the right")} after {_r[i].EnterDelay:F1} s to x {_r[i].GoalX:F0} row {_r[i].RowY:F0} width {s.Width} door {DoorLo:F0} {DoorHi:F0}");
        _log?.WriteLine($"Reindeer zones: door {DoorLo:F0} {DoorHi:F0} snowman {s.SnowmanFoot.X:F0} +- {0.55f * s.SnowmanHeight:F0} feeder {s.FeederFoot.X:F0} +- {2.2f * s.FeederTray.Half:F0} lamp {s.LampFoot.X:F0} stretches {string.Join(" ", Array.ConvertAll(OpenSnow, z => $"{z.Lo * s.Width:F0}-{z.Hi * s.Width:F0}"))}");
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        float u = s.U;
        BuildInBackground(() =>
        {
            // The painting must not steal the main thread's frames in the opening seconds: it naps now and
            // then (a lowered priority starved the build on a busy machine: the herd had not appeared after ten seconds) (ReindeerArt.Nap), so the snow keeps its 60 a second while the animals are drawn.
            var oldPriority = Thread.CurrentThread.Priority;
            try {
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            // Compile every method of the herd now, on this thread, so the first Update and the first draw on the frame path never wait for the JIT
            // (the first pass had a 3 to 12 ms first update and a 60 ms first frames).
            foreach (var m in typeof(ReindeerHerd).GetMethods(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (!m.IsAbstract && !m.ContainsGenericParameters) { try { System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { } }
            long jitMs = swAll.ElapsedMilliseconds;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();
            var sets = new ReindeerSet[count];
            // Each animal is painted by its own thread at once (the painter's scratch bitmap is per thread): three animals take about as long as the slowest.
            QuietFor(count, k =>                       // a few low-priority threads, never every core: see Resident.QuietFor
            {
                try
                {
                    sets[k] = ReindeerArt.Build(heights[k], _r[k].Kind);
                    _r[k].Mark = PaintMark(heights[k]);
                    PointF nose = sets[k].Nose[ReindeerArt.HeadPages - 1];
                    _r[k].MarkDx = ReindeerArt.NeckX + nose.X - 0.22f;
                }
                finally { ReindeerArt.ReleaseScratch(); }
            });
            _log?.WriteLine("Reindeer build: " + ReindeerArt.Timing);
            _log?.WriteLine($"Reindeer build: animals painted ({sw.ElapsedMilliseconds} ms, {(GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1048576.0:F1} MB allocated by the build thread)");
            // Small shared sprites: snow bits (three sizes), soft clouds (five sizes, for breath and puffs), one sparkle.
            _flake = new Sprite[3];
            for (int k = 0; k < 3; k++)
            {
                int rad = Math.Max(1, (int)MathF.Round(u * (0.0011f + 0.0007f * k)));
                _flake[k] = Sprite.Paint(rad * 2 + 2, rad * 2 + 2, g => { using var b = new SolidBrush(Color.FromArgb(238, 244, 250, 255)); g.FillEllipse(b, 1, 1, rad * 2 - 0.5f, rad * 2 - 0.5f); });
            }
            _cloud = new Sprite[5];
            for (int k = 0; k < 5; k++) _cloud[k] = Sprite.Glow(Math.Max(2, (int)MathF.Round(u * (0.0042f + 0.0030f * k))), Color.FromArgb(236, 242, 255));
            _spark = SantaRig.PaintSparkle(Math.Max(7, (int)(u * 0.011f)));
            _log?.WriteLine($"Reindeer build: small sprites painted; BUILD TOTAL {swAll.ElapsedMilliseconds} ms (JIT {jitMs} ms)");
            // Publish the sets last: Update reads them only after Ready.
            for (int i = 0; i < count; i++) _r[i].Set = sets[i];
            if (!string.IsNullOrEmpty(sheet) && sheet.StartsWith("reindeer;", StringComparison.OrdinalIgnoreCase) && count > 0)
            {
                ReindeerArt.WriteSheet(sheet.Substring(9), sets);
                ReindeerArt.WriteTurnSheet(System.IO.Path.ChangeExtension(sheet.Substring(9), null) + "_turn.png", sets);
            }
            } catch (Exception e) { _log?.WriteLine("REINDEER BUILD FAILED: " + e); throw; }
            finally { Thread.CurrentThread.Priority = oldPriority; ReindeerArt.ReleaseScratch(); }
        });
    }

    /// <summary>The scraped hole in the snow: a darker blue-grey dish with a rim of lumps and a few green-grey flecks of lichen.</summary>
    private static Sprite PaintMark(int h)
    {
        int w = Math.Max(8, (int)(0.62f * h)), hh = Math.Max(4, (int)(0.13f * h));
        return Sprite.Paint(w, hh, g =>
        {
            using var rim = new SolidBrush(Color.FromArgb(150, 226, 236, 252));
            g.FillEllipse(rim, 0.5f, 0.5f, w - 1f, hh - 1f);
            using var dish = new SolidBrush(Color.FromArgb(185, 96, 112, 156));
            g.FillEllipse(dish, w * 0.12f, hh * 0.18f, w * 0.76f, hh * 0.64f);
            using var deep = new SolidBrush(Color.FromArgb(170, 70, 84, 120));
            g.FillEllipse(deep, w * 0.26f, hh * 0.30f, w * 0.48f, hh * 0.40f);
            using var lichen = new SolidBrush(Color.FromArgb(190, 150, 164, 140));
            g.FillEllipse(lichen, w * 0.40f, hh * 0.42f, Math.Max(1.5f, w * 0.10f), Math.Max(1.2f, hh * 0.18f));
        });
    }

    // ================================================================ places

    private static float FootY(Rein a) => a.RowY;
    private static float FootYAt(Rein a, float x) => a.RowY;
    // THE LANE'S PLACES (second pass, after the film: "the bull grazed for 40 s directly behind the bird table, the cow and calf stood behind the
    // snowman half covered"). The herd's band is farther off than the snowman and the bird table, so anything it does behind them is hidden. Four
    // stretches of clear snow are where the herd LIVES (fractions of the width: the wide clear snow at the left, the stretch in front of the village
    // above the pond, two small clear places at the right); the places behind or beside the snowman, the bird table, the lamp post and the cabin's front
    // are NO-STOP: a walk may go past, nothing stops, grazes, lies, nuzzles or is born there.
    private static readonly (float Lo, float Hi)[] OpenSnow = [(0.03f, 0.26f), (0.31f, 0.46f), (0.70f, 0.76f), (0.90f, 0.98f)];
    /// <summary>Which open stretch x is inside (with "margin" pixels to spare at each end), or -1.</summary>
    private int StretchOf(float x, float margin)
    {
        for (int k = 0; k < OpenSnow.Length; k++) if (x >= OpenSnow[k].Lo * S.Width + margin && x <= OpenSnow[k].Hi * S.Width - margin) return k;
        return -1;
    }
    /// <summary>Is a body (middle at x, h pixels at the shoulder) hidden or crowded by the snowman, the bird table, the lamp post or the cabin's front? (Half a body of margin.)</summary>
    private bool NoStopAt(float x, float h)
    {
        float m = 0.5f * h;
        if (x > DoorLo - m && x < DoorHi + m) return true;
        if (MathF.Abs(x - S.SnowmanFoot.X) < 0.55f * S.SnowmanHeight + m) return true;
        if (MathF.Abs(x - S.FeederFoot.X) < 2.2f * S.FeederTray.Half + m) return true;
        return MathF.Abs(x - S.LampFoot.X) < 1.4f * h + m;      // (a body length, 1.9 heights, each side of the post)
    }
    /// <summary>May something REST here (graze, lie, nuzzle, stand): inside an open stretch and clear of every no-stop place.</summary>
    private bool RestOk(float x, float h) => StretchOf(x, 0.3f * h) >= 0 && !NoStopAt(x, h);
    private bool OkSpot(float x, float rowY, int h)
    {
        if (x < S.Width * 0.02f || x > S.Width * 0.98f) return false;
        // A stop lands up to a stride (about 0.8 heights) either side of the goal the brake was aimed at, so a goal must have good ground on both
        // sides of it too (first pass: a goal at the very edge of a stretch, a stop that came out beyond it, a walk begun again, 54 times in a minute).
        return S.CanStand(x, rowY, 0.45f * h) && RestOk(x, h) && RestOk(x - 0.8f * h, h) && RestOk(x + 0.8f * h, h);
    }
    /// <summary>The cabin's whole front and its woodpile, not only the door (the first herd stood three abreast across the windows for a minute).</summary>
    private float DoorLo => S.CabinFoot.X - S.CabinWall.Width * 0.95f;
    private float DoorHi => S.CabinFoot.X + S.CabinWall.Width * 0.70f;
    private bool NearDoor(float x) => x > DoorLo && x < DoorHi;
    private float Stride(Rein a) => a.TrotMode ? TrotStride(a) : 2 * ReindeerArt.WalkReach * a.Set.H / ReindeerArt.WalkDuty;
    private float TrotStride(Rein a) => 2 * ReindeerArt.TrotReach * a.Set.H / ReindeerArt.TrotDuty;
    private float Lo(Rein a) => S.Width * 0.02f + 1.4f * a.Set.H;
    private float Hi(Rein a) => S.Width * 0.98f - 1.4f * a.Set.H;

    // ================================================================ living

    public override void Update(float dt)
    {
        if (_log == null) { UpdateInner(dt); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateInner(dt);
        long took = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        if (!_firstUpdateLogged) { _firstUpdateLogged = true; _log.WriteLine($"Reindeer FIRST UPDATE {took * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F2} ms"); }
        _tUpdate += took;
        if (_time - _tReport >= 1.0)
        {
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _log.WriteLine($"{_time,8:F2} COST per second: update {_tUpdate * ms:F2} ms, collect {_tCollect * ms:F2} ms, draw {_tDraw * ms:F2} ms");
            _tUpdate = _tCollect = _tDraw = 0; _tReport = _time;
        }
    }

    private void UpdateInner(float dt)
    {
        if (_r.Length == 0) return;
        _time += dt;
        // THE CLOAKROOM LIST: every animal on the screen writes its feet and its half length, for the others (and the fox and the hares) to read next frame.
        for (int i = 0; i < _r.Length; i++)
        {
            Rein o = _r[i];
            if (o.EnterDelay <= 0 && o.X > -0.5f * o.Set.H && o.X < S.Width + 0.5f * o.Set.H) S.Occupy(this, i, o.X, FootY(o), Half(o));
        }
        bool santaOn = S.SantaOnScreen;
        if (santaOn && !_santaWas) { _passAdultSlots = Rng.NextDouble() < 0.9 ? (Rng.NextDouble() < 0.35 ? 2 : 1) : 0; _passCalfOk = Rng.NextDouble() < 0.85; _watchRank = 0; }   // a new pass: who may look up
        _santaWas = santaOn;
        if (_force != null && !_forced && _time >= 26.0) { _forced = ApplyForce(); if (_forced) _log?.WriteLine($"{_time,8:F2} Reindeer FORCED {_force}"); }   // 26 s: the entrance (20 s) is over
        if (_alarmAt >= 0 && _time >= _alarmAt)
        {
            _alarmAt = -1;
            for (int i = 0; i < _r.Length; i++)
            {
                Rein a = _r[i];
                if (a.Doing == Doing.Trot || a.Kind == ReindeerKind.Bull && Rng.NextDouble() < 0.4 || a.Doing == Doing.Gambol) continue;
                a.Pending = Reaction.Flee; a.PendingCause = new Notice(Sense.Friend, new PointF(_alarmFrom, FootY(a)), 1, 1, null, _time);
                if (a.Gait is Gait.Lying) BeginUp(a, true);
            }
        }
        for (int i = 0; i < _r.Length; i++) Step(i, dt);
    }

    /// <summary>Forces one doing (test aid). False: the animal was busy, try again next frame.</summary>
    private bool ApplyForce()
    {
        bool ok = true;
        for (int i = 0; i < _r.Length; i++)
        {
            Rein a = _r[i];
            switch (_force)
            {
                case "paw": if (a.Doing == Doing.Graze) { a.GPhase = 0; a.GT = 0.05f; } break;
                case "lie": a.LieAfter = 0.1f + 1.5f * (float)Rng.NextDouble(); break;
                case "shake": a.Doing = Doing.Shake; a.Timer = 0; break;
                case "alert": a.Doing = Doing.Alert; a.Timer = 4f; break;
                case "trot": a.Pending = Reaction.Flee; a.PendingCause = new Notice(Sense.Threat, new PointF(a.X - a.Dir * 300, FootY(a)), 500, 1, null, _time); a.PendingAt = _time; break;
                case "gambol": case "fly": if (a.Kind == ReindeerKind.Calf) { _nextGambol = 0; a.Fly = _force == "fly"; a.ForceGambol = true; } break;
                case "turn": if (a.Kind != ReindeerKind.Calf) { if (!Idle(a)) ok = false; else { a.Doing = Doing.Look; a.Timer = 6f; a.ScanTotal = 6f; a.ScanTurn = true; a.ScanPhase = 0; } } break;
                case "watch": if (a.Kind == ReindeerKind.Calf || a.Curious > 0.3f) StartWatch(a); break;
                case "creep": if (a.Kind != ReindeerKind.Calf) { if (!Idle(a) || a.Doing != Doing.Graze) ok = false; else StartCreep(a, i, a.Dir, 1.4f * a.Set.H, 2, "forced"); } break;
                case "bellow": if (a.Kind == ReindeerKind.Bull) { if (!Idle(a)) ok = false; else { a.Doing = Doing.Bellow; a.BellowT = 0; a.BellowPosted = false; } } break;
                case "nuzzle": if (i == _mom) { int p = CalfOf(i) >= 0 ? CalfOf(i) : 0; if (!Idle(a) || !Idle(_r[p]) || !StartNuzzle(a, i, p)) ok = false; } break;
            }
        }
        if (_force == "walk") { int li = 0; if (!Idle(_r[li]) || !StartLedWalk(_r[li], li)) ok = false; }
        return ok;
    }

    private void Step(int i, float dt)
    {
        Rein a = _r[i];
        ReindeerSet set = a.Set;
        float H = set.H;
        if (a.Doing != a.SeenDoing) { a.SeenDoing = a.Doing; a.DoingAge = 0; } else a.DoingAge += dt;
        a.Timer -= dt; a.Clock += dt; a.Senses.Tick(dt); a.PostT -= dt; a.RestT -= dt; a.TurnCool -= dt; a.WatchRest -= dt; a.AlertT -= dt; a.GStay -= dt; a.LookT -= dt;
        if (a.AlertWait > 0 && (a.AlertWait -= dt) <= 0) a.AlertTry = 1.5f;
        if (a.WatchWait > 0 && (a.WatchWait -= dt) <= 0) a.WatchTry = 1.5f;   // Santa's stagger is up: lift the head at the next break (up to 1.5 s)
        if (a.LieAfter > 0) a.LieAfter -= dt;
        UpdateFx(a, dt);
        if (a.HasMark) { a.MarkA = a.Gait == Gait.Pawing || a.GPhase >= 1 && a.Doing is Doing.Graze or Doing.Creep ? MathF.Min(1f, a.MarkA + dt * 1.5f) : MathF.Max(0f, a.MarkA - dt / 18f); if (a.MarkA <= 0) a.HasMark = false; }

        // ---- the neck: a damped spring toward where the doing wants it ----
        float want = NeckWant(a);
        a.NeckV += ((want - a.NeckF) * NeckSpring - a.NeckV * NeckDamp) * dt;
        a.NeckF = Math.Clamp(a.NeckF + a.NeckV * dt, 0f, 1f);
        // The idle drift of a standing head is eased in and out (a head that starts to drift by half a page the frame a turn ends is a pop).
        float driftWant = a.Gait is Gait.Stand or Gait.Lying && a.Doing != Doing.Alert && a.Doing != Doing.Watch && a.TurnReq == 0 && NeckWant(a) < 0.85f ? 1f : 0f;     // (the nose in the hole stays put: a drift across a page boundary was a flicker)
        a.DriftAmp += (driftWant - a.DriftAmp) * Math.Min(1f, dt * 2.5f);
        // A glance waiting for a break: lift the head now (never mid-scrape or mid-bite), or drop it if it took more than a second.
        if (a.LookPend)
        {
            if (_time - a.LookPendAt > 1.0 || a.Doing is Doing.Alert or Doing.Trot or Doing.Gambol or Doing.Shake or Doing.Watch or Doing.Bellow || a.Gait is Gait.Down or Gait.Up or Gait.Shaking or Gait.Leaping or Gait.Turning) a.LookPend = false;
            else if (AtBreak(a)) { a.LookPend = false; a.LookT = 1.2f + 1.8f * a.Curious * (0.6f + 0.8f * (float)Rng.NextDouble()); a.EarT = 0; _log?.WriteLine($"{_time,8:F2} Reindeer{i} lifts its head for {a.LookT:F1} s"); }
        }
        // Bites with the nose in the hole: bursts of a few, each a quick tug up, a hold and an ease down, then chewing.
        if (a.BiteT >= 0) { a.BiteT += dt; if (a.BiteT > BiteTime) { a.BiteT = -1; a.ChewT = 0; } }
        if (a.ChewT >= 0) { a.ChewT += dt; if (a.ChewT > 1.5f) a.ChewT = -1; }
        if (a.Doing is Doing.Graze or Doing.Creep && a.GPhase == 2 && a.NeckF > 0.93f && a.BiteT < 0)
        {
            a.NextBite -= dt;
            if (a.NextBite <= 0)
            {
                a.BiteT = 0;
                if (a.BitesLeft <= 0) a.BitesLeft = 3 + Rng.Next(4);
                a.BitesLeft--;
                a.NextBite = a.BitesLeft > 0 ? 0.55f + 0.7f * (float)Rng.NextDouble() : 1.8f + 2.5f * (float)Rng.NextDouble();
            }
        }
        if (a.Gait == Gait.Lying && a.ChewT < 0 && a.LieT > 2f && (float)Rng.NextDouble() < dt / 3.5f) a.ChewT = 0;   // a lying reindeer chews its cud
        if (a.EarT >= 0) { a.EarT += dt; if (a.EarT > EarTime) a.EarT = -1; }
        else { a.NextEar -= dt; if (a.NextEar <= 0) { a.EarT = 0; a.NextEar = 1.8f + 4f * (float)Rng.NextDouble(); } }
        // Breath: a small puff of white vapour from the nostrils every few seconds when still (it is cold).
        a.Breath -= dt;
        if (a.Breath <= 0 && a.Gait is Gait.Stand or Gait.Lying or Gait.Pawing or Gait.Settling)
        {
            PointF nz = NosePoint(a);
            Spawn(a, 1, nz.X, nz.Y, a.Dir * 7f * H / 60f, -9f * H / 60f, 1.5f, 0);
            a.Breath = 2.6f + 2.6f * (float)Rng.NextDouble();
        }

        // ---- the senses ----
        Reaction r = Reaction.None; Notice cause = default;
        if (!a.Entering) r = a.Senses.Check(Board, new PointF(a.X, FootY(a)), a, out cause);
        if (r != Reaction.None)
        {
            if (a.Doing == Doing.Gambol && a.GPh < 90)
            {
                if (cause.Kind == Sense.Threat || (cause.Kind == Sense.Loud && r != Reaction.Look) || r is Reaction.Freeze or Reaction.Flee) AbortGambol(a, $"{cause.Kind} {r}");
                else r = Reaction.None;                              // a glance, a far-off bang: no reason to stop playing
            }
        }
        if (r is Reaction.Freeze or Reaction.Flee && cause.Kind == Sense.Threat && a.Doing != Doing.Gambol)
        {
            // A fox about: unless it is within about three body lengths, the herd only lifts its heads, together, ONCE (and goes back to what it was
            // doing); only a close fox makes them run. (Seed 7: three alarms in ten seconds cut the herd's day into one-second pieces.)
            float dxf = cause.At.X - a.X, dyf = cause.At.Y - FootY(a);
            if (MathF.Sqrt(dxf * dxf + dyf * dyf) > 3f * 1.9f * H) { if (_time >= _alertCool) HerdAlert(); r = Reaction.None; }
        }
        if (r != Reaction.None)
        {
            if (r is Reaction.Look or Reaction.Approach) ReactLook(a, i, r, cause);     // a glance is the head only, taken at a break
            else
            {
                bool free = a.Gait is Gait.Stand or Gait.Walking or Gait.Lying or Gait.Rising or Gait.Settling && a.Doing != Doing.Gambol;
                if (a.Gait == Gait.Pawing) a.Scrapes = Math.Min(a.Scrapes, 1);   // finish the scrape in hand, then the reaction
                if (free && a.Gait is Gait.Stand or Gait.Walking or Gait.Lying) React(a, i, r, cause);
                else if (a.Doing != Doing.Gambol && (a.Pending == Reaction.None || Rank(r) > Rank(a.Pending))) { a.Pending = r; a.PendingCause = cause; a.PendingAt = _time; }
            }
        }

        StepPose(a, dt);
        TickGait(a, i, dt);

        // ---- the pin: a walking animal in the open is a Movement for the others to glance at (a bout of at least 2 s, then a rest) ----
        bool moving = a.Gait == Gait.Walking && a.Pace.Speed > 0.2f * _walkV;
        if (moving)
        {
            a.WalkSecs += dt;
            if (a.WalkSecs > 2f && a.PostT <= 0 && a.RestT <= 0)
            {
                Board.Post(Sense.Movement, new PointF(a.X, FootY(a)), 0.25f * S.U, 0.3f, a);
                a.PostT = 0.9f;
            }
        }
        else if (a.Gait != Gait.Stopping && a.Gait != Gait.Turning)
        {
            if (a.WalkSecs > 2f) a.RestT = 6f;
            a.WalkSecs = 0;
        }

        Think(a, i, dt);
        if (_log != null)
        {
            PickBody(a, out int bodyId);
            int figId = a.Gait == Gait.Turning ? (int)MathF.Round(Smooth(Math.Clamp(a.TurnT / a.TurnFor, 0f, 1f)) * ReindeerArt.TurnSteps) : 0;
            int hpg = Math.Clamp((int)MathF.Round(Math.Clamp(a.NeckF, 0f, 1f) * (ReindeerArt.HeadPages - 1)), 0, ReindeerArt.HeadPages - 1);
            _log.WriteLine($"{_time,8:F3} Reindeer{i} x {a.X:F1} y {FootY(a):F1} v {a.Pace.Speed:F1} {a.Doing}/{a.Gait} neck {a.NeckF:F2} dir {a.Dir} body {bodyId} fig {figId} head {hpg} lift {a.Lift:F1} hk {HeadKind(a)}");
        }
    }

    private static int Rank(Reaction r) => r switch { Reaction.Flee => 4, Reaction.Freeze => 3, Reaction.Approach => 2, Reaction.Look => 1, _ => 0 };

    private float NeckWant(Rein a)
    {
        // A turn is asked for or going: the head comes up to the turn's own level and STAYS there (head up, then the turn, then what is next).
        if (a.TurnReq != 0 || a.Gait == Gait.Turning) return TurnLf;
        if (a.Gait is Gait.Down or Gait.Lying or Gait.Up) return a.AlertT > 0 ? 0.08f : 0.25f;
        if (a.Gait == Gait.Shaking) return 0.45f;
        if (a.LookT > 0 && a.Doing is not (Doing.Trot or Doing.Gambol or Doing.Alert or Doing.Shake or Doing.Bellow or Doing.Watch)) return 0.20f;    // a glance: the head lifted over whatever the doing is
        return a.Doing switch
        {
            Doing.Graze => a.GPhase switch { 0 => 0.42f, 1 => 0.62f, 2 => 1.0f, _ => 0.28f },
            Doing.Creep => a.GPhase == 2 ? 1.0f : a.GPhase == 3 ? 0.28f : 0.58f,     // head low between the steps, nose near the snow (and down in the hole for the meal that ends a drift)
            Doing.Look => TurnLf,
            Doing.Alert => 0.08f,
            Doing.Watch => a.WatchF,
            Doing.Walk => 0.34f,
            Doing.Trot => 0.24f,
            Doing.Gambol => a.Gait == Gait.Leaping ? 0.10f : 0.22f,
            Doing.Shake => 0.45f,
            Doing.Nuzzle => a.NzPhase == 2 ? TurnLf + 0.045f * MathF.Sin(a.NzT * 3.2f) : TurnLf,     // head carried up and level, a little bob when the noses touch
            Doing.Bellow => a.BellowT < 0.25f ? 0.30f : a.BellowT < 2.9f ? 0.0f : 0.25f,                 // right up for the bellow
            _ => 0.25f,
        };
    }

    // ================================================================ the body machine

    private void TickGait(Rein a, int i, float dt)
    {
        float H = a.Set.H;
        switch (a.Gait)
        {
            case Gait.Rising:
                a.GaitT += dt;
                // Somebody stepped into the way while it was rising: it settles back down through the SAME pages (the rise played backward from where
                // it is), instead of walking one frame and stopping (a rise-and-settle stutter).
                if (a.WalkIntent && !a.Entering && a.Doing != Doing.Trot && AheadBlocked(a, i, a.Following ? a.FollowIdx : a.Doing == Doing.Nuzzle ? a.Partner : -1))
                {
                    float left = a.GaitFor - a.GaitT;
                    StopWalk(a);
                    a.BlockCool = 2f;
                    if (a.Doing == Doing.Nuzzle) EndNuzzle(a, i, false);
                    a.Gait = Gait.Settling; a.GaitT = Math.Clamp(left, 0f, a.GaitFor); a.GaitFor = a.TrotMode ? 0.2f : SettleFor;
                    _log?.WriteLine($"{_time,8:F2} Reindeer{i} settles back (somebody stepped into the way) at {a.X:F0}");
                    break;
                }
                if (a.GaitT >= a.GaitFor)
                {
                    if (a.WalkIntent && a.Doing is Doing.Walk or Doing.Trot or Doing.Creep or Doing.Nuzzle) { a.Gait = Gait.Walking; a.Pace.Walked = 0; a.Pace.Want = a.WalkWant; }
                    else { a.Gait = Gait.Settling; a.GaitT = 0; a.GaitFor = a.TrotMode ? 0.2f : SettleFor; }   // the walk was cancelled while it rose: settle again, the pages are continuous
                }
                break;
            case Gait.Walking:
            {
                float move = a.Pace.Tick(dt);
                a.X += a.Dir * move;
                if (a.StopPending) StopWalk(a);                         // leaning into the speed a stop needs, then the brake
                else WalkChecks(a, i, H);
                break;
            }
            case Gait.Stopping:
            {
                float move = a.Pace.Tick(dt);
                a.X += a.Dir * move;
                if (a.Pace.Still) { a.Gait = Gait.Settling; a.GaitT = 0; a.GaitFor = a.TrotMode ? 0.2f : SettleFor; a.Pace.Walked = 0; }
                break;
            }
            case Gait.Settling:
                a.GaitT += dt;
                if (a.GaitT >= a.GaitFor)
                {
                    a.Gait = Gait.Stand; a.TrotMode = false;
                    if (a.TurnWhenSettled != 0) { BeginTurn(a, a.TurnWhenSettled, false); a.TurnWhenSettled = 0; }
                }
                break;
            case Gait.Stand:
                if (a.TurnReq != 0) TryTurn(a, dt);                     // a turn is waiting for the head to come up
                break;
            case Gait.Leaping:
                if (a.Doing != Doing.Gambol && a.GamFrom == 0 && a.GamTo == 0 && a.PlopT < 0f) a.Gait = Gait.Stand;     // a play cut short: on its feet again
                break;
            case Gait.Turning:
            {
                float was = a.TurnT;
                a.TurnT += dt;
                if (was < a.TurnFor / 2 && a.TurnT >= a.TurnFor / 2) a.Dir = a.TurnTo;
                if (a.TurnT >= a.TurnFor) AfterTurn(a);
                break;
            }
            case Gait.Down:
                a.DownT += dt;
                if (a.DownT >= DownTime)
                {
                    a.Gait = Gait.Lying; a.LieT = 0;
                    if (a.Doing == Doing.Lie) a.Timer = a.LieFor;
                }
                break;
            case Gait.Lying:
                a.LieT += dt;
                if (a.LieT > 8f) a.Snow = MathF.Min(1f, a.Snow + dt / 40f);        // the snow settles on a back that lies long
                break;
            case Gait.Up:
                a.UpT += dt;
                if (a.UpT >= a.UpFor)
                {
                    a.Gait = Gait.Stand;
                    if (a.Doing == Doing.Lie) { a.LastDoing = Doing.Lie; if (a.AfterUpShake) { a.Doing = Doing.Shake; a.Timer = 1.2f; } else StartGraze(a); }
                }
                break;
            case Gait.Shaking:
            {
                a.ShakeT += dt;
                float k = a.ShakeT / ShakeTime;
                // Snow flies off the coat in bursts, outward and up, with a little cloud at the middle.
                if (k < 0.8f)
                    for (int n = 0; n < 2; n++)
                    {
                        float sx = (float)(Rng.NextDouble() * 2 - 1) * 0.55f * H, sy = (0.55f + 0.5f * (float)Rng.NextDouble()) * H;
                        Spawn(a, 0, a.X + sx, FootY(a) - sy, (sx / H) * 120f * H / 60f + (float)(Rng.NextDouble() - 0.5) * 30f, -60f * H / 60f * (float)Rng.NextDouble() - 20f, 0.7f + 0.4f * (float)Rng.NextDouble(), Rng.Next(3));
                    }
                if (a.ShakeT >= ShakeTime) { a.Gait = Gait.Stand; a.Snow = 0; a.LastDoing = Doing.Shake; a.Doing = Doing.Look; a.Timer = 2.0f; a.ScanTurn = false; a.ScanPhase = 0; a.ScanTotal = 2f; }
                break;
            }
        }
    }

    // ================================================================ who stands where, and the road ahead

    // FEYNMAN VERSION. Every animal writes its feet and half its length on the scene's cloakroom list each frame (S.Occupy) and
    // asks it before it steps (S.Crowded). The herd's own animals are checked here by hand (so a nose-to-nose nuzzle can name its
    // partner as the one to ignore); everybody else on the snow (the hares, the fox, the mice) is asked of the list.
    // 1.1 heights each side is an adult's body (1.9 heights nose to tail is the body proper) plus a little breathing room.
    private static float Half(Rein a) => 1.1f * a.Set.H;
    private float RowsTol => 0.03f * S.U;                         // the herd's whole band is under 0.03 U deep: every row of it counts as "the same row"

    /// <summary>Would feet at x (this animal's own row) stand inside somebody? "ignore" is a herd-mate that is allowed to be close (the leader it follows, the partner it nuzzles).</summary>
    private bool BlockedAt(Rein a, int i, float x, int ignore = -1)
    {
        float ha = Half(a);
        if (S.Crowded(x, a.RowY, ha, this, -1, RowsTol)) return true;
        for (int k = 0; k < _r.Length; k++)
        {
            if (k == i || k == ignore) continue;
            Rein o = _r[k];
            if (o.EnterDelay > 0) continue;                          // still beyond the edge of the screen
            if (a.Doing == Doing.Trot && o.Doing == Doing.Trot) continue;      // a herd running together keeps its places by speed (WalkChecks), it does not stop for itself
            if (MathF.Abs(o.X - x) < ha + Half(o)) return true;
        }
        return false;
    }

    /// <summary>Is there somebody close ahead, within about the distance a brake takes? (The walk stops short of them, about two body lengths away.)</summary>
    private bool AheadBlocked(Rein a, int i, int ignore) =>
        BlockedAt(a, i, a.X + a.Dir * (a.Doing == Doing.Trot ? 1.9f : 1.4f) * a.Set.H, ignore);

    /// <summary>Is every step of the straight way from the animal to x ground its feet may go, and is nobody standing on the way (or at its end)?</summary>
    private bool PathBlocked(Rein a, int i, float x, int ignore)
    {
        float H = a.Set.H, ha = Half(a);
        int dir = x >= a.X ? 1 : -1;
        float from = MathF.Min(a.X, x), to = MathF.Max(a.X, x), travel = to - from;
        for (float px = from; px <= to; px += 0.6f * H) if (!S.CanStand(px, a.RowY, 0.3f * H)) return true;
        for (int k = 0; k < _r.Length; k++)
        {
            if (k == i || k == ignore) continue;
            Rein o = _r[k];
            if (o.EnterDelay > 0) continue;
            float d = (o.X - a.X) * dir;
            if (d > 0 && d < travel + ha + Half(o) + 1.4f * H) return true;     // ahead of it on the way, or just past the end of it (with the room a brake takes, the same as AheadBlocked asks: a walk is never begun that the next frame would stop)
        }
        for (float px = a.X + dir * 1.2f * H; dir * (x - px) > -1.2f * H; px += dir * 1.2f * H)
            if (S.Crowded(px, a.RowY, ha, this, -1, RowsTol)) return true;   // a hare, the fox, a mouse on the way
        return false;
    }

    /// <summary>The animal stands on its feet and no turn is waiting: a new doing may begin.</summary>
    private static bool Idle(Rein a) => a.Gait == Gait.Stand && a.TurnReq == 0;

    /// <summary>A natural break for a head to lift: not halfway through a scrape, not in the tug of a bite.</summary>
    private static bool AtBreak(Rein a) => a.Gait != Gait.Pawing && a.BiteT < 0;

    // ================================================================ the walk

    /// <summary>
    /// The calf's play pose moves along drawn pages from one key pose to the next (never a jump): an edge takes 0.07 s; a target that changed
    /// mid-edge is taken up at the end of it (or at once if it is the key just left).
    /// </summary>
    private void StepPose(Rein a, float dt)
    {
        if (a.GamFrom == a.GamTo)
        {
            if (a.PoseT == a.GamFrom) return;
            int to = a.PoseT, lo = Math.Min(a.GamFrom, to), hi = Math.Max(a.GamFrom, to);
            if (a.Set.GamEdge[lo, hi] == null) { a.GamFrom = a.GamTo = to; a.GamU = 0; return; }       // no drawn way between these two (not used by the script)
            a.GamTo = to; a.GamU = 0;
        }
        else if (a.PoseT == a.GamFrom) { (a.GamFrom, a.GamTo) = (a.GamTo, a.GamFrom); a.GamU = 1f - a.GamU; }     // turned round on the way: back to the key it left
        a.GamU += dt / 0.08f;
        if (a.GamU >= 1f) { a.GamFrom = a.GamTo; a.GamU = 0; }
    }

    /// <summary>What a walking animal checks every frame: is the road ahead standable and empty, is its errand done, did its time run out.</summary>
    private void WalkChecks(Rein a, int i, float H)
    {
        if (a.Doing is not (Doing.Walk or Doing.Trot or Doing.Creep or Doing.Nuzzle)) { StopWalk(a); return; }
        if (a.Entering)
        {
            // Walking in from the edge of the screen: the places were dealt so that nobody is in the way, and the edge is the way in.
            if (a.Dir * (a.GoalX - a.X) <= Stride(a) * 0.9f) { a.Entering = false; StopWalk(a); _log?.WriteLine($"{_time,8:F2} Reindeer{i} arrived at x {a.X:F0}"); }
            return;
        }
        float look = (a.Doing == Doing.Trot ? 2.8f : 1.6f) * H;
        float ax = a.X + a.Dir * look;
        bool outOfLane = ax < Lo(a) || ax > Hi(a);
        if (a.HasGoal && a.Doing != Doing.Trot) { ax = a.Dir > 0 ? MathF.Min(ax, a.GoalX) : MathF.Max(ax, a.GoalX); outOfLane = false; }   // a walk to a place was checked when it began
        if (outOfLane || !S.CanStand(ax, a.RowY, 0.3f * H))
        {
            int back = -a.Dir;
            StopWalk(a);
            a.TurnWhenSettled = back;                                 // at an edge or a prop: stop, settle, turn, and the next roll may walk on
            a.BlockCool = 2f;
            if (a.Doing == Doing.Nuzzle) EndNuzzle(a, i, false);
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} blocked by an edge at {a.X:F0}");
            return;
        }
        int ignore = a.Following ? a.FollowIdx : a.Doing == Doing.Nuzzle ? a.Partner : -1;
        if (AheadBlocked(a, i, ignore))
        {
            // Somebody is standing ahead: stop short of them (never walk into another animal), and do not start again at once.
            StopWalk(a);
            a.BlockCool = 2f;
            if (a.Doing == Doing.Nuzzle) EndNuzzle(a, i, false);
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} blocked by somebody ahead at {a.X:F0}");
            return;
        }
        if (a.Following)
        {
            Rein L = _r[a.FollowIdx];
            int fd = a.FollowDir != 0 ? a.FollowDir : L.Dir;
            float gapX = Herd.FollowX(L.X, fd, GapFor(a));
            float remaining = a.Dir * (gapX - a.X);
            bool leaderMoving = L.Gait is Gait.Walking or Gait.Rising or Gait.Turning or Gait.Stopping || L.TurnReq != 0;
            // A gentle push on the speed: a long way behind it goes a little faster, close up a little slower, so a file keeps its gaps without stopping and starting.
            a.Pace.Want = _walkV * Math.Clamp(1f + 0.18f * (remaining / H - 0.8f), 0.6f, 1.35f);
            if (!leaderMoving && remaining <= 1.4f * H || a.Timer <= 0 && !leaderMoving || remaining < -2.5f * H) { a.Following = false; StopWalk(a); }
            return;
        }
        if (a.Doing == Doing.Trot)
        {
            // A running herd keeps its places: an animal with another just ahead of it on the way eases off, so they never pile into one body.
            bool crowded = false;
            foreach (Rein o in _r) { float d = (o.X - a.X) * a.Dir; if (o != a && d > 0 && d < 2.6f * H) crowded = true; }
            a.Pace.Want = crowded ? _trotV * 0.8f : _trotV;
        }
        if (a.HasGoal && a.Dir * (a.GoalX - a.X) <= Stride(a) * 0.9f) { if (a.Doing == Doing.Nuzzle && a.NzLead) a.NzPhase = 1; StopWalk(a); return; }
        if (a.Timer <= 0) StopWalk(a);
    }

    private bool AnyPlaying() { foreach (Rein o in _r) if (o.Doing == Doing.Gambol) return true; return false; }

    private float GapFor(Rein a)
    {
        float href = _r[_mom].Set.H;
        return a.Kind == ReindeerKind.Calf ? 2.1f * href : 2.8f * href;
    }

    // ================================================================ turning: head up FIRST, then the turn, then whatever is next

    // THE OWNER'S WORD was "the deer turn left, then right and that's it", and the review found the head popping at the end of 20 of 88 turns
    // (nose on the snow to head high in three frames): the turn pages were drawn at the head pitch the turn began with, while the neck went
    // on easing to what the next doing wanted, and the drawing switched to the live neck the frame the turn ended. Now the turn is drawn at ONE
    // head level (ReindeerArt.TurnF(0), a head carried up and level), and a turn is only begun once the neck is THERE: a turn is a REQUEST
    // (BeginTurn), the neck is told to come up (NeckWant answers the turn's level while a request is waiting or a turn is going), and TryTurn
    // starts the turn when the head has arrived and is still. After the turn the neck simply eases on to wherever the next doing wants it.
    private static float TurnLf => ReindeerArt.TurnF(0);
    private static bool HeadUp(Rein a) => MathF.Abs(a.NeckF - TurnLf) < 0.05f && MathF.Abs(a.NeckV) < 0.7f;

    /// <summary>Asks for a turn to face "dir". It happens when the head is up (see TryTurn).</summary>
    private void BeginTurn(Rein a, int dir, bool fast)
    {
        if (dir == a.Dir) { a.TurnReq = 0; return; }
        a.TurnReq = dir; a.TurnFast = fast; a.TurnWait = 0;
    }

    private void TryTurn(Rein a, float dt)
    {
        a.TurnWait += dt;
        if (HeadUp(a) || a.TurnWait > 2f) DoTurn(a, a.TurnReq, a.TurnFast);
    }

    /// <summary>
    /// A turn is a half turn on the spot drawn as real views (ReindeerArt.PaintFigure): the side, three-quarters, head-on or tail-on,
    /// three-quarters, the other side. About 1.2 to 1.6 s for an adult, 0.95 for the calf, 0.75 when it bolts. The page follows an eased
    /// angle (slow at the ends, quick in the middle) and the animal faces the new way from the middle on (a.Dir flips at half way: the
    /// drawing does not use it, it uses TurnFrom). Toward us three turns in five, away from us (the rump and the short tail) two in five.
    /// </summary>
    private void DoTurn(Rein a, int dir, bool fast)
    {
        a.TurnReq = 0;
        if (dir == a.Dir) { AfterTurn(a); return; }
        a.Gait = Gait.Turning; a.TurnFrom = a.Dir; a.TurnTo = dir; a.TurnT = 0; a.TurnCool = 2.5f;
        a.TurnFor = fast ? 0.75f : a.Kind == ReindeerKind.Calf ? 0.95f : 1.15f + 0.45f * (float)Rng.NextDouble();
        a.TurnSide = Rng.NextDouble() < 0.6 ? 1 : -1;
        a.TurnLevel = 0;
    }

    /// <summary>The turn is over (or was not needed): on with the walk that wanted it, or stand.</summary>
    private void AfterTurn(Rein a)
    {
        if (a.WalkIntent && a.Doing is Doing.Walk or Doing.Trot or Doing.Creep or Doing.Nuzzle) { a.Gait = Gait.Rising; a.GaitT = 0; a.GaitFor = a.TrotMode ? 0.2f : SettleFor; }
        else a.Gait = Gait.Stand;
    }

    /// <summary>Starts a walk or a trot; if the animal faces the other way it asks for a turn first (head up, the turn pages, then the walk rises after them).</summary>
    private void StartWalk(Rein a, int dir, bool trot, float goalX = float.NaN, Doing d = Doing.Walk)
    {
        a.Doing = trot ? Doing.Trot : d;
        a.TrotMode = trot;
        a.WalkWant = trot ? _trotV : _walkV;
        a.HasGoal = !float.IsNaN(goalX); a.GoalX = goalX; a.Following = false;
        a.GPhase = 0; a.WalkIntent = true; a.StopPending = false;
        if (dir != a.Dir) { BeginTurn(a, dir, trot); return; }
        a.TurnReq = 0;
        a.Gait = Gait.Rising; a.GaitT = 0; a.GaitFor = trot ? 0.2f : SettleFor;
    }

    /// <summary>Begins a stop: the brake runs the walk down so it ends exactly on the page where all four feet are planted.</summary>
    private void StopWalk(Rein a)
    {
        bool wasTrot = a.Doing == Doing.Trot;
        a.WalkIntent = false;
        if (a.Doing is Doing.Walk or Doing.Trot)
        {
            if (wasTrot) { a.Doing = Doing.Look; a.Timer = 3f; a.ScanTurn = false; }       // after a flight: a look back at what it fled
            else { a.Doing = Doing.Graze; a.GPhase = 0; a.GT = 0.3f + 0.5f * (float)Rng.NextDouble(); a.Timer = 999f; a.GCycles = 1 + (Rng.NextDouble() < 0.15 ? 1 : 0); }
        }
        else if (a.Doing == Doing.Creep) a.CPhase = 1;                                          // a creep step is over: the scrape comes next
        a.HasGoal = false; a.Following = false;
        if (a.Gait != Gait.Walking) return;
        if (a.Pace.Speed < 0.2f * _walkV)
        {
            // Barely moving (it was only just rising into the walk): a brake sized by this speed would take half a minute. Stand it down.
            a.Pace.Halt(); a.Pace.Walked = 0; a.Gait = Gait.Settling; a.GaitT = 0; a.GaitFor = a.TrotMode ? 0.2f : SettleFor;
            return;
        }
        float dist = a.Pace.ToRestPage(Stride(a));
        float need = 2f * dist / 1.8f;                                  // a stop never takes more than 1.8 s: a slow walker that must reach the rest page speeds up a touch to do it
        if (a.Pace.Speed < 0.97f * need) { a.StopPending = true; a.Pace.Want = need; return; }   // (first pass jumped the speed to "need" in one frame: up to 22 px a second at once; now it leans into the extra speed first)
        a.StopPending = false;
        a.Gait = Gait.Stopping;
        a.Pace.Brake(dist);
    }

    // ================================================================ the mind

    private void Think(Rein a, int i, float dt)
    {
        float H = a.Set.H, u = S.U;
        if (a.Doing == Doing.Gambol) { TickGambol(a, i, dt); return; }

        // THE ENTRANCE: beyond the edge of the screen until its turn, then it walks in (the calf at a trot to keep up) to its place.
        if (a.Entering)
        {
            if (a.EnterDelay > 0)
            {
                a.EnterDelay -= dt;
                if (a.EnterDelay <= 0) { a.EnterDelay = 0; StartWalk(a, a.Dir, a.Kind == ReindeerKind.Calf, a.GoalX); _log?.WriteLine($"{_time,8:F2} Reindeer{i} walks in from the edge to x {a.GoalX:F0}"); }
            }
            return;
        }

        a.BlockCool -= dt; a.RoomCool -= dt; a.LieCool -= dt; a.NuzzleCool -= dt; a.BellowCool -= dt;

        // A reaction that had to wait for a scrape or a turn to finish (a stale one is dropped).
        if (a.Pending != Reaction.None && _time - a.PendingAt > 1.5) a.Pending = Reaction.None;
        if (Idle(a) && a.Pending != Reaction.None)
        {
            Reaction pr = a.Pending; a.Pending = Reaction.None;
            React(a, i, pr, a.PendingCause);
            return;
        }
        // Lying: the timer ends it.
        if (a.Gait is Gait.Down or Gait.Lying or Gait.Up)
        {
            if (a.Gait == Gait.Lying && a.Timer <= 0) BeginUp(a, false);
            return;
        }
        // The herd lies down together: when one lies, the calf gets a lag.
        if (Idle(a) && a.LieAfter <= 0 && a.LieAfter > -0.5f && a.Doing is Doing.Graze or Doing.Look or Doing.Creep)
        {
            a.LieAfter = -1;
            if (RestOk(a.X, H) && a.GPhase != 1) { BeginDown(a, i); return; }
        }
        // The calf of a herd that walks on: it falls in behind (LedWalk's followers are started here when the one ahead is under way).
        if (a.FollowGo > 0) TickFollower(a, i, dt);

        if (a.Doing is Doing.Graze or Doing.Creep && a.Gait is Gait.Stand or Gait.Pawing) ThinkGraze(a, i, dt);
        if (a.Doing == Doing.Nuzzle) ThinkNuzzle(a, i, dt);

        // The herd's one head lift for a fox about (HerdAlert): at a natural break; one that is walking only lifts its head.
        if (a.AlertTry > 0)
        {
            a.AlertTry -= dt;
            if (Idle(a) && AtBreak(a) && a.Doing is Doing.Graze or Doing.Look or Doing.Creep or Doing.Nuzzle && a.GPhase != 1)
            {
                a.AlertTry = 0;
                if (a.Doing == Doing.Nuzzle) EndNuzzle(a, i, false);
                a.LastDoing = a.Doing; a.Doing = Doing.Alert; a.Timer = 3f + 2f * (float)Rng.NextDouble(); a.EarT = 0; a.LookPend = false;
                _log?.WriteLine($"{_time,8:F2} Reindeer{i} lifts its head with the herd for {a.Timer:F1} s");
            }
            else if (a.Gait is Gait.Walking or Gait.Stopping or Gait.Rising) { a.LookT = MathF.Max(a.LookT, 3f); a.AlertTry = 0; }
        }
        // The calf creeps after its mother, a second behind her (not a walk of its own each time she steps on).
        if (a.EchoT > 0 && (a.EchoT -= dt) <= 0 && Idle(a) && a.Doing is Doing.Graze or Doing.Look && a.GPhase != 1 && a.BlockCool <= 0)
            StartCreep(a, i, a.EchoDir, a.EchoDist, 1, "after its mother");
        // A Santa pass: the head is lifted one animal after another (staggered), at a natural break.
        if (a.WatchTry > 0)
        {
            a.WatchTry -= dt;
            if (!S.SantaOnScreen) a.WatchTry = 0;
            else if (Idle(a) && AtBreak(a) && a.Doing is Doing.Graze or Doing.Look or Doing.Creep && a.GPhase != 1) { a.WatchTry = 0; StartWatch(a); }
        }

        if (!Idle(a)) return;

        // THE PLACE RULE (the door rule, widened after the film): nothing rests where it is hidden or crowded (the snowman, the bird table, the lamp
        // post, the cabin's front) or off the open snow, and a visitor whose time is up goes back to the herd's stretch. It walks to the nearest good place,
        // preferring the herd's own stretch; if there is none free it stands and looks, and tries again in two seconds.
        bool homesick = a.VisitUntil > 0 && _time > a.VisitUntil && StretchOf(a.X, 0f) != _home;
        if (a.BlockCool <= 0 && a.DoingAge > 0.8f && _time - a.LastRestWalk > 4.0 && (!RestOk(a.X, H) || homesick) && a.Doing is Doing.Graze or Doing.Look or Doing.Watch or Doing.Alert or Doing.Creep or Doing.Bellow && !(a.Doing == Doing.Graze && a.GPhase == 1))
        {
            float goal = PickRest(a, i);
            if (!float.IsNaN(goal))
            {
                a.VisitUntil = 0; a.LastRestWalk = _time;
                StartWalk(a, goal >= a.X ? 1 : -1, false, goal);
                a.Timer = 999f;
                _log?.WriteLine($"{_time,8:F2} Reindeer{i} walks to a place it may rest, x {goal:F0} (from {a.X:F0})");
                return;
            }
            a.BlockCool = 2f; a.LookT = 2f; a.EarT = 0;               // nowhere free: stand and look, try again in a moment
        }

        // The calf keeps near its mother: it follows her loosely (Herd.cs) and sets off after a lag once it is wrong.
        if (a.Kind == ReindeerKind.Calf && a.Doing is Doing.Look or Doing.Graze && !(a.Doing == Doing.Graze && a.GPhase is 1 or 2) && _r.Length > 1 && i != _mom)
        {
            Rein L = _r[_mom];
            bool leaderMoving = L.Gait is Gait.Walking or Gait.Rising or Gait.Turning;
            if (L.Gait is not (Gait.Down or Gait.Lying or Gait.Up) && L.Doing != Doing.Gambol && a.BlockCool <= 0 && a.DoingAge > 1.5f)
            {
                float slack = leaderMoving ? 4.0f * H : 7.0f * H;     // (the dice's own follow, at 3 heights, comes first; this one is the safety net)
                float err = Herd.FollowError(a.X, L.X, L.Dir, GapFor(a), slack);
                if (err != 0f)
                {
                    if (a.FollowLag < 0) a.FollowLag = 0.5f + 2.0f * (float)Rng.NextDouble();
                    else if ((a.FollowLag -= dt) <= 0)
                    {
                        a.FollowLag = -1;
                        if (StartCalfFollow(a, i, err)) return;
                    }
                }
                else a.FollowLag = -1;
            }
        }

        // Room: standing too close to a neighbour, the one that is not busy steps off a little (a creep, not a march) (Herd.Spacing).
        if (a.Doing == Doing.Look && !AnyPlaying() || a.Doing == Doing.Graze && a.GPhase == 0 && !AnyPlaying())
        {
            if (a.RoomCool <= 0 && a.BlockCool <= 0 && a.Clock > 3f && a.DoingAge > 1.5f)
            {
                float push = 0;
                for (int k = 0; k < _r.Length; k++)
                {
                    if (k == i || _r[k].EnterDelay > 0) continue;
                    if (a.Kind != ReindeerKind.Calf && _r[k].Kind == ReindeerKind.Calf) continue;      // an adult ignores the calf
                    float d = a.X - _r[k].X, room = (a.Kind == ReindeerKind.Calf ? 2.0f : 2.4f) * H;
                    if (MathF.Abs(d) < room) push += d >= 0 ? room - d : -(room + d);
                }
                a.RoomCool = 4f;
                if (push != 0f && StartCreep(a, i, push > 0 ? 1 : -1, MathF.Abs(push) + 0.5f * H, 1, "room")) return;
            }
        }

        // The urge to play: a settled calf with a quiet herd has about a one in fourteen chance each second.
        if (a.Kind == ReindeerKind.Calf && _force is null or "gambol" or "fly" && a.Doing is Doing.Graze or Doing.Look && (a.Doing == Doing.Look || a.GPhase == 0) && a.DoingAge > 2f && _time >= _nextGambol && Rng.NextDouble() < dt / 14.0 && CanGambol(a)) { StartGambol(a); return; }
        if (a.ForceGambol && a.Doing is Doing.Graze or Doing.Look && CanGambol(a)) { a.ForceGambol = false; StartGambol(a); return; }
        if (a.Doing is Doing.Graze or Doing.Creep) return;          // its own phases decide when it is done (ThinkGraze)
        if (a.Doing == Doing.Watch) { ThinkWatch(a, i, dt); return; }
        if (a.Doing == Doing.Nuzzle) return;                        // ThinkNuzzle runs it
        if (a.Doing == Doing.Bellow) { ThinkBellow(a, i, dt); return; }
        if (a.Doing == Doing.Look && a.ScanTurn && a.ScanPhase == 0 && a.Timer < 0.5f * a.ScanTotal)
        {
            // A slow look round the horizon: head up and held, then the animal turns right round and holds the other view, then lowers its head.
            a.ScanPhase = 1;
            BeginTurn(a, -a.Dir, false);
            if (a.TurnReq != 0) return;
        }
        if (a.Doing == Doing.Shake)
        {
            a.Gait = Gait.Shaking; a.ShakeT = 0;
            return;
        }
        if (a.Timer > 0) return;
        Decide(a, i);
    }

    /// <summary>The dice: what next, when a doing has ended.</summary>
    private void Decide(Rein a, int i)
    {
        float u = S.U;
        bool calf = a.Kind == ReindeerKind.Calf, bull = a.Kind == ReindeerKind.Bull;
        bool playing = AnyPlaying();
        Doing last = a.LastDoing;
        // Lying: not in the first 90 s, only one or two at a time (and somebody always stays on its feet), and far enough apart in time.
        int lying = 0, adultsStanding = 0;
        foreach (Rein o in _r) { if (o.Doing == Doing.Lie || o.Gait is Gait.Down or Gait.Lying or Gait.Up || o.LieAfter > 0) lying++; else if (o.Kind != ReindeerKind.Calf) adultsStanding++; }
        bool lieOk = !calf && _r.Length >= 2 && _time > 90.0 && a.LieCool <= 0 && _time >= _nextLie && lying < Math.Max(1, Math.Min(2, _r.Length - 1)) && adultsStanding >= 1 && RestOk(a.X, a.Set.H) && !playing;
        bool ledOk = !calf && !playing && _time > 40.0 && _time >= _ledCool;
        int nuzzlePartner = !playing && a.NuzzleCool <= 0 && _time > 30.0 ? NuzzleCandidate(a, i) : -1;
        float followErr = 0f;
        if (calf && _r.Length > 1 && i != _mom)
        {
            Rein ml = _r[_mom];
            if (ml.Gait is not (Gait.Down or Gait.Lying or Gait.Up) && ml.Doing != Doing.Gambol && a.BlockCool <= 0) followErr = Herd.FollowError(a.X, ml.X, ml.Dir, GapFor(a), 4.5f * a.Set.H);
        }
        float[] w = [
            last == Doing.Graze ? 0.05f : 0.13f,                                             // 0 graze: eat in one place (scan, scrape, eat, chew)
            last == Doing.Creep ? 0.08f : calf ? 0.22f : 0.30f,                              // 1 creep: drift on, a step and a scrape at a time
            (last == Doing.Look ? 0.05f : 0.20f) + 0.10f * a.Timid,                          // 2 a slow look round
            lieOk ? 0.18f : 0f,                                                              // 3 lie down
            0.07f,                                                                           // 4 shake
            ledOk ? 0.16f : 0f,                                                              // 5 the herd walks on (led by this one)
            nuzzlePartner >= 0 ? 0.14f : 0f,                                                 // 6 touch noses
            bull && a.BellowCool <= 0 && _time > 25.0 && !playing ? 0.18f : 0f,              // 7 bellow
            calf && CanGambol(a) && _time >= _nextGambol ? 0.30f : 0f,                       // 8 gambol
            followErr != 0f ? 1.2f : 0f,                                                     // 9 the calf goes after its mother
        ];
        if (a.BlockCool > 0) { w[1] = 0; w[5] = 0; w[6] = 0; w[9] = 0; }                     // a start was just blocked: stand and look, do not walk
        if (_force == "graze") { for (int k = 1; k < w.Length; k++) w[k] = 0; }
        if (_force == "walk" && ledOk) { for (int k = 0; k < w.Length; k++) w[k] = 0; w[5] = 1; }
        int face = Dice.Pick(Rng, w);
        a.LastDoing = a.Doing;
        switch (face)
        {
            case 0: StartGraze(a); break;
            case 1:
            {
                int dir = Rng.NextDouble() < 0.78 ? a.Dir : -a.Dir;
                if (!StartCreep(a, i, dir, (0.5f + 1.0f * (float)Rng.NextDouble()) * 1.9f * a.Set.H, 1 + (Rng.NextDouble() < 0.45 ? 1 : 0), "dice")) StartGraze(a);
                break;
            }
            case 2:
                a.Doing = Doing.Look; a.Timer = Organic.Jitter(Rng, 5.5f, 0.3f); a.ScanTotal = a.Timer; a.ScanPhase = 0; a.ScanTurn = Rng.NextDouble() < 0.45; a.EarT = 0;
                break;
            case 3:
                if (RestOk(a.X, a.Set.H)) { BeginDown(a, i); AlarmLie(i); }
                else StartGraze(a);
                break;
            case 4: a.Doing = Doing.Shake; break;
            case 5: if (!StartLedWalk(a, i)) StartGraze(a); break;
            case 6: if (!StartNuzzle(a, i, nuzzlePartner)) StartGraze(a); break;
            case 7: a.Doing = Doing.Bellow; a.BellowT = 0; a.BellowCool = 45f + 40f * (float)Rng.NextDouble(); a.BellowPosted = false; break;
            case 8: StartGambol(a); break;
            case 9: if (!StartCalfFollow(a, i, followErr)) StartGraze(a); break;
            default: StartGraze(a); break;
        }
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} roll {a.Doing} (face {face}) x {a.X:F0}");
    }

    /// <summary>
    /// The nearest place this animal may rest, on the way it can walk: sampled every half a body length across the lane, scored by distance (in
    /// heights), a heavy penalty outside the herd's own stretch, and a penalty for company (nobody within three heights). NaN: none free.
    /// </summary>
    private float PickRest(Rein a, int i)
    {
        float H = a.Set.H, best = float.NaN, bs = 1e9f;
        for (float x = Lo(a); x <= Hi(a); x += 0.5f * H)
        {
            if (!OkSpot(x, a.RowY, (int)H) || MathF.Abs(x - a.X) < 1.2f * H || PathBlocked(a, i, x, -1)) continue;
            float sc = MathF.Abs(x - a.X) / H + (StretchOf(x, 0f) == _home ? 0f : 9f);
            foreach (Rein o in _r) if (o != a && o.EnterDelay <= 0 && MathF.Abs(o.X - x) < 2.6f * H) sc += 6f;
            if (sc < bs) { bs = sc; best = x; }
        }
        return best;
    }

    private void StartGraze(Rein a)
    {
        a.Doing = Doing.Graze; a.GPhase = 0; a.GT = 0.5f + 0.9f * (float)Rng.NextDouble(); a.Timer = 999f;
        a.GCycles = 1 + (Rng.NextDouble() < 0.15 ? 1 : 0);
    }

    // ---- creeping: the way grazing animals drift forward ----

    /// <summary>
    /// A DRIFT: step on half to one and a half body lengths with the head low, stop, scrape once, step again (steps times), then eat at the
    /// last hole. Returns false when the ground ahead is not free (the caller grazes where it stands instead).
    /// </summary>
    private bool StartCreep(Rein a, int i, int dir, float dist, int steps, string why)
    {
        a.Doing = Doing.Creep; a.CSteps = steps; a.CPhase = 0; a.CDir = dir; a.CDist = dist; a.Timer = 999f;
        if (!CreepStep(a, i)) { a.Doing = Doing.Graze; return false; }
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} creeps {steps} step(s) of {dist:F0} px ({why}) at x {a.X:F0}");
        if (i == _mom && a.Kind != ReindeerKind.Calf)
            foreach (Rein o in _r)
                if (o.Kind == ReindeerKind.Calf && o.EnterDelay <= 0 && !o.Entering) { o.EchoT = 0.8f + 0.7f * (float)Rng.NextDouble(); o.EchoDir = a.CDir; o.EchoDist = MathF.Max(0.5f * dist, 0.5f * o.Set.H); }
        return true;
    }

    /// <summary>The calf sets off after its mother (to the place behind her it belongs). False: the way is not free.</summary>
    private bool StartCalfFollow(Rein a, int i, float err)
    {
        int dir = err > 0 ? 1 : -1;
        if (PathBlocked(a, i, a.X + dir * MathF.Min(MathF.Abs(err), 6f * a.Set.H), _mom)) { a.BlockCool = 2f; return false; }
        StartWalk(a, dir, false);
        a.Following = true; a.FollowIdx = _mom; a.FollowDir = 0; a.Timer = 12f;
        return true;
    }

    /// <summary>Begins one creep step (the direction it was going, or the other way if that is blocked). False: nowhere to step.</summary>
    private bool CreepStep(Rein a, int i)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int dir = attempt == 0 ? a.CDir : -a.CDir;
            float gx = a.X + dir * a.CDist;
            if (gx < Lo(a) || gx > Hi(a) || !OkSpot(gx, a.RowY, (int)a.Set.H) || PathBlocked(a, i, gx, -1)) continue;
            a.CDir = dir;
            StartWalk(a, dir, false, gx, Doing.Creep);
            a.Timer = 999f;
            return true;
        }
        a.BlockCool = 2f;
        return false;
    }

    /// <summary>A creep's scrape is done: another step, or the meal.</summary>
    private void CreepNext(Rein a, int i)
    {
        a.CSteps--;
        a.CDist = (0.5f + 1.0f * (float)Rng.NextDouble()) * 1.9f * a.Set.H;
        if (a.CSteps > 0 && CreepStep(a, i)) return;
        // The meal at the last hole belongs to the same doing: a drift is steps, scrapes, and what they were for.
        a.CPhase = 2; a.GPhase = 2; a.EatT = Organic.Jitter(Rng, 4.2f); a.BitesLeft = 0; a.GCycles = 1; a.Timer = 999f;
        a.MarkX = a.X + a.Dir * a.MarkDx * a.Set.H; a.HasMark = true; a.MarkA = MathF.Max(a.MarkA, 0.6f);
    }

    /// <summary>
    /// The reindeer's own way of grazing: scan the snow, PAW it (a foreleg lifted, reached forward, set down and raked back,
    /// three or four times, the shoulder dipping at each strike and snow flying behind the hoof), then nose down into the hole
    /// and eat (bites in bursts, chewing between), then lift the head and chew while looking about. A creep is the same scrape on its
    /// own (one or two strokes) between two steps. A look (LookT) pauses the meal where it is: the head is up, the doing carries on after.
    /// </summary>
    private void ThinkGraze(Rein a, int i, float dt)
    {
        float H = a.Set.H;
        if (a.Doing == Doing.Creep)
        {
            if (a.Gait == Gait.Stand && a.CPhase == 1 && a.TurnReq == 0)
            {
                a.CPhase = 2; a.GPhase = 1; a.Scrapes = 1 + (Rng.NextDouble() < 0.25 ? 1 : 0); a.PawT = 0; a.PawFor = PawTime * (0.9f + 0.25f * (float)Rng.NextDouble()); a.Gait = Gait.Pawing;
                a.MarkX = a.X + a.Dir * a.MarkDx * H; a.HasMark = true; a.MarkA = MathF.Max(a.MarkA, 0.15f);
            }
            else if (a.CPhase != 2) return;
        }
        if (a.LookT > 0 && a.GPhase != 1) return;                    // the head is up for a look: the meal waits
        switch (a.GPhase)
        {
            case 0:
                a.GT -= dt;
                if (a.GT <= 0)
                {
                    if (Rng.NextDouble() < 0.82)
                    {
                        a.GPhase = 1; a.Scrapes = 3 + Rng.Next(2); a.PawT = 0; a.PawFor = PawTime * (0.9f + 0.25f * (float)Rng.NextDouble()); a.Gait = Gait.Pawing;
                        a.MarkX = a.X + a.Dir * a.MarkDx * H; a.HasMark = true; a.MarkA = 0.15f;
                    }
                    else { a.GPhase = 2; a.EatT = Organic.Jitter(Rng, 3.5f); a.BitesLeft = 0; a.MarkX = a.X + a.Dir * a.MarkDx * H; a.HasMark = true; a.MarkA = 0.5f; }
                }
                break;
            case 1:
            {
                a.PawT += dt / a.PawFor;
                // Snow flies as the hoof rakes back along the ground (the second half of each scrape).
                if (a.PawT > 0.5f && a.PawT < 0.88f)
                {
                    float p = a.PawT;
                    float hx = a.X + a.Dir * (0.30f + ReindeerArt.PawReach - (ReindeerArt.PawReach - 0.05f) * Smooth((p - 0.45f) / 0.55f)) * H;
                    float sc = H / 60f;
                    for (int n = 0; n < 2; n++)
                        Spawn(a, 0, hx, FootY(a) - 0.03f * H, -a.Dir * (50f + 110f * (float)Rng.NextDouble()) * sc, -(70f + 150f * (float)Rng.NextDouble()) * sc, 0.45f + 0.35f * (float)Rng.NextDouble(), Rng.Next(3));
                }
                if (a.PawT >= 1f)
                {
                    a.Scrapes--; a.PawT = 0;
                    a.MarkA = MathF.Min(1f, a.MarkA + 0.2f);
                    if (a.Scrapes <= 0)
                    {
                        a.Gait = Gait.Stand;
                        if (a.Doing == Doing.Creep) CreepNext(a, i);
                        else { a.GPhase = 2; a.EatT = Organic.Jitter(Rng, 3.4f); a.BitesLeft = 0; }
                    }
                    else a.PawFor = PawTime * (0.9f + 0.25f * (float)Rng.NextDouble());
                }
                break;
            }
            case 2:
                a.EatT -= dt;
                if (a.EatT <= 0 && a.BiteT < 0) { a.GPhase = 3; a.GT = 0.8f + 1.0f * (float)Rng.NextDouble(); a.ChewT = 0; }
                break;
            case 3:
                a.GT -= dt;
                if (a.GT <= 0)
                {
                    a.JustGrazed = true;
                    if (--a.GCycles > 0) { a.GPhase = 0; a.GT = 0.4f + 0.5f * (float)Rng.NextDouble(); }       // a second helping at the same spot or a step on: the meal goes on
                    else { a.GPhase = 0; Decide(a, i); }
                }
                break;
        }
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }

    // ================================================================ the herd walks on (led)

    /// <summary>
    /// A LED WALK: this animal (the bull or a cow) sets off for a place five to nine body lengths away and the other adults that are behind
    /// it in the way it goes fall in after it, in file, each behind the one before (a gap of about three heights); where it stops they stop,
    /// and then they spread out and graze again. Rare: the herd rolls it at most once in 70 to 110 seconds.
    /// </summary>
    private bool StartLedWalk(Rein L, int li)
    {
        float H = L.Set.H;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            // Where to: the other big stretch most of the time (the wide snow at the left and the stretch above the pond), now and then a visit
            // by the leader ALONE to one of the two small clear places at the right (they hold one animal); from a visit, back to a big one.
            int cur = StretchOf(L.X, 0f); if (cur < 0) cur = _home;
            double rr = Rng.NextDouble();
            int dest = cur <= 1 ? (rr < 0.72 ? 1 - cur : rr < 0.86 ? 2 : 3) : (rr < 0.5 ? 0 : 1);
            bool visit = dest >= 2;
            int dir = OpenSnow[dest].Lo * S.Width + 0.5f * (OpenSnow[dest].Hi - OpenSnow[dest].Lo) * S.Width >= L.X ? 1 : -1;
            // the goal: the far end of that stretch's resting ground in the way it goes (the followers fill in behind)
            float gx = float.NaN;
            for (float x = dir > 0 ? OpenSnow[dest].Hi * S.Width : OpenSnow[dest].Lo * S.Width; dir > 0 ? x >= OpenSnow[dest].Lo * S.Width : x <= OpenSnow[dest].Hi * S.Width; x -= dir * 0.25f * H)
                if (OkSpot(x, L.RowY, (int)H) && x >= Lo(L) && x <= Hi(L)) { gx = x; break; }
            if (float.IsNaN(gx) || MathF.Abs(gx - L.X) < 3f * H || PathBlocked(L, li, gx, -1)) continue;
            // Who falls in: the adults behind it in the way it goes, standing or grazing, nearest first.
            int prev = li, rank = 0;
            bool[] taken = new bool[_r.Length]; taken[li] = true;
            for (;;)
            {
                if (visit) break;
                int best = -1; float bd = float.MaxValue;
                for (int k = 0; k < _r.Length; k++)
                {
                    Rein o = _r[k];
                    if (taken[k] || o.Kind == ReindeerKind.Calf || o.Entering || o.Doing is Doing.Lie or Doing.Gambol or Doing.Alert or Doing.Trot or Doing.Nuzzle || o.Gait is Gait.Down or Gait.Lying or Gait.Up) continue;
                    float behind = -(o.X - L.X) * dir;
                    if (behind > 0 && behind < bd) { bd = behind; best = k; }
                }
                if (best < 0) break;
                taken[best] = true;
                Rein f = _r[best];
                f.FollowIdx = prev; f.FollowDir = dir; f.FollowGo = 0.9f + 0.8f * rank + 0.7f * (float)Rng.NextDouble(); f.FollowTry = 6;
                prev = best; rank++;
            }
            // The calf goes after its mother (whether she leads or follows) if it is behind the leader.
            for (int k = 0; k < _r.Length && !visit; k++)
            {
                Rein o = _r[k];
                if (o.Kind != ReindeerKind.Calf || taken[k] || k == _mom && k == li || o.Entering || o.Doing is Doing.Lie or Doing.Gambol or Doing.Alert or Doing.Trot or Doing.Nuzzle || o.Gait is Gait.Down or Gait.Lying or Gait.Up) continue;
                if (-(o.X - L.X) * dir <= 0) continue;
                o.FollowIdx = (_mom == li || taken[_mom]) && _mom != k ? _mom : li; o.FollowDir = dir; o.FollowGo = 1.6f + 1.2f * rank + 0.7f * (float)Rng.NextDouble(); o.FollowTry = 8;
                taken[k] = true;
            }
            StartWalk(L, dir, false, gx);
            L.Timer = 999f;
            if (visit) L.VisitUntil = (float)(_time + 55 + 20 * Rng.NextDouble()); else { _home = dest; foreach (Rein o in _r) o.VisitUntil = 0; }
            _ledCool = _time + 70 + 40 * Rng.NextDouble();
            _log?.WriteLine($"{_time,8:F2} Reindeer{li} {(visit ? "goes visiting" : "leads the herd")} {dir} to x {gx:F0} (stretch {dest}) with {rank} following");
            return true;
        }
        _ledCool = _time + 15;
        return false;
    }

    /// <summary>A follower waits until the one ahead of it is really under way (no stop-and-start), then sets off after it. Returns true while it is still waiting.</summary>
    private bool TickFollower(Rein a, int i, float dt)
    {
        Rein L = _r[a.FollowIdx];
        a.FollowGo -= dt;
        if (a.FollowGo > 0) return true;
        bool underway = L.Gait == Gait.Walking && L.Pace.Speed > 0.5f * _walkV;
        bool lost = L.Doing is not (Doing.Walk or Doing.Creep) && L.Gait != Gait.Walking && L.Gait != Gait.Stopping && L.FollowGo <= 0;     // the one ahead has already arrived or gave up: nothing to follow
        if (lost || a.FollowTry <= 0) { a.FollowGo = 0; return false; }
        if (underway && Idle(a) && AtBreak(a) && a.Doing is Doing.Graze or Doing.Look or Doing.Creep && a.GPhase != 1 && !AheadBlocked(a, i, a.FollowIdx))
        {
            a.FollowGo = 0;
            StartWalk(a, a.FollowDir, false);
            a.Following = true; a.Timer = 40f;
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} falls in behind Reindeer{a.FollowIdx}");
            return false;
        }
        a.FollowGo = 0.3f; a.FollowTry--;
        return true;
    }

    // ================================================================ touching noses, and a bellow

    /// <summary>The distance from the body's middle to the nostril at the head level of the turn pages, in pixels (what a nose-to-nose standing distance is built from).</summary>
    private float NoseReach(Rein a)
    {
        ReindeerSet s = a.Set;
        int hp = Math.Clamp((int)MathF.Round(TurnLf * (ReindeerArt.HeadPages - 1)), 0, ReindeerArt.HeadPages - 1);
        return (s.Stand.NX + s.Nose[hp].X) * s.H;
    }

    /// <summary>Is another herd-mate free to be visited: standing and calm, on its own side of the cabin, near enough?</summary>
    private int NuzzleCandidate(Rein a, int i)
    {
        int best = -1; float bd = 14f * a.Set.H;
        for (int k = 0; k < _r.Length; k++)
        {
            Rein o = _r[k];
            if (k == i || o.Entering || o.Gait != Gait.Stand || o.TurnReq != 0 || o.Doing is not (Doing.Graze or Doing.Look) || o.GPhase == 1 || !RestOk(o.X, o.Set.H) || o.NuzzleCool > 0) continue;
            if (a.Kind == ReindeerKind.Calf && k != _mom) continue;            // the calf goes to its mother
            if (a.Kind != ReindeerKind.Calf && o.Kind == ReindeerKind.Calf && k != CalfOf(i)) continue;
            float d = MathF.Abs(o.X - a.X);
            if (d < bd) { bd = d; best = k; }
        }
        return best;
    }
    private int CalfOf(int i) => i == _mom ? _r.Length - 1 : -1;

    private bool StartNuzzle(Rein a, int i, int pi)
    {
        if (pi < 0 || pi >= _r.Length) return false;
        Rein p = _r[pi];
        int dirA = p.X >= a.X ? 1 : -1;
        float standOff = NoseReach(a) + NoseReach(p) + 0.08f * a.Set.H;       // noses just touching
        float gx = p.X - dirA * standOff;
        if (gx < Lo(a) || gx > Hi(a) || !OkSpot(gx, a.RowY, (int)a.Set.H) || PathBlocked(a, i, gx, pi)) return false;
        if (MathF.Abs(gx - a.X) < 0.6f * a.Set.H) { gx = a.X + dirA * 0.6f * a.Set.H; }
        // The visited one waits: head up, turns to face the visitor.
        p.Doing = Doing.Nuzzle; p.NzLead = false; p.Partner = i; p.NzPhase = 0; p.NzT = 0; p.Timer = 999f; p.GPhase = 0;
        BeginTurn(p, -dirA, false);
        a.Partner = pi; a.NzLead = true; a.NzPhase = 0; a.NzT = 0;
        StartWalk(a, dirA, false, gx, Doing.Nuzzle);
        a.Timer = 999f;
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} goes to touch noses with Reindeer{pi}, stand-off {standOff:F0} px");
        return true;
    }

    private void ThinkNuzzle(Rein a, int i, float dt)
    {
        if (a.Partner < 0 || a.Partner >= _r.Length) { EndNuzzle(a, i, false); return; }
        Rein p = _r[a.Partner];
        a.NzT += dt;
        if (p.Doing != Doing.Nuzzle || p.Partner != i || a.NzT > (a.NzPhase == 2 ? 12f : 22f) || p.Gait is Gait.Down or Gait.Lying or Gait.Up || a.Gait is Gait.Down or Gait.Lying or Gait.Up) { EndNuzzle(a, i, false); return; }
        if (!a.NzLead) return;                                              // the visited one is told by the visitor
        if (a.NzPhase == 1 && Idle(a) && Idle(p) && p.Dir == -a.Dir && HeadUp(a) && HeadUp(p))
        {
            a.NzPhase = 2; a.NzT = 0; p.NzPhase = 2; p.NzT = 0;
            a.NzFor = p.NzFor = 3.0f + 1.8f * (float)Rng.NextDouble();
            a.EarT = 0; p.EarT = 0;
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} and Reindeer{a.Partner} touch noses (gap {MathF.Abs(a.X - p.X) - NoseReach(a) - NoseReach(p):F0} px) for {a.NzFor:F1} s");
        }
        else if (a.NzPhase == 2)
        {
            if ((a.NzBreath -= dt) <= 0)
            {
                a.NzBreath = 0.9f + 0.5f * (float)Rng.NextDouble();
                PointF n1 = NosePoint(a), n2 = NosePoint(p);
                Spawn(a, 1, n1.X, n1.Y, a.Dir * 6f * a.Set.H / 60f, -8f * a.Set.H / 60f, 1.3f, 0);
                Spawn(p, 1, n2.X, n2.Y, p.Dir * 6f * p.Set.H / 60f, -8f * p.Set.H / 60f, 1.3f, 0);
            }
            if (a.NzT >= a.NzFor) EndNuzzle(a, i, true);
        }
    }

    /// <summary>Both go back to ordinary life: the one that finished here lets its partner go too.</summary>
    private void EndNuzzle(Rein a, int i, bool both)
    {
        int pi = a.Partner;
        a.Partner = -1; a.NuzzleCool = 80f + 60f * (float)Rng.NextDouble();
        if (a.Doing == Doing.Nuzzle) { a.Doing = Doing.Graze; a.GPhase = 0; a.GT = 0.6f + 0.8f * (float)Rng.NextDouble(); a.Timer = 999f; a.GCycles = 1; a.WalkIntent = false; if (a.Gait == Gait.Walking) StopWalk(a); }
        if (pi >= 0 && pi < _r.Length)
        {
            Rein p = _r[pi];
            if (p.Doing == Doing.Nuzzle && p.Partner == i)
            {
                p.Partner = -1; p.NuzzleCool = 80f + 60f * (float)Rng.NextDouble();
                p.Doing = Doing.Graze; p.GPhase = 0; p.GT = 1.2f + 0.8f * (float)Rng.NextDouble(); p.Timer = 999f; p.GCycles = 1; p.WalkIntent = false;
                if (p.Gait == Gait.Walking) StopWalk(p);
            }
        }
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} nuzzle over (finished {both})");
    }

    /// <summary>The bull lifts his head right up and bellows: a plume of breath rolling off the muzzle, and a call the nearest of the herd may glance at.</summary>
    private void ThinkBellow(Rein a, int i, float dt)
    {
        a.BellowT += dt;
        float t = a.BellowT;
        if (t > 0.8f && t < 2.2f && (a.NzBreath -= dt) <= 0)
        {
            a.NzBreath = 0.22f;
            PointF nz = NosePoint(a); float sc = a.Set.H / 60f;
            Spawn(a, 1, nz.X, nz.Y, a.Dir * (16f + 10f * (float)Rng.NextDouble()) * sc, -(8f + 8f * (float)Rng.NextDouble()) * sc, 1.7f, 0);
        }
        if (!a.BellowPosted && t > 0.9f)
        {
            a.BellowPosted = true;
            Board.Post(Sense.Call, new PointF(a.X, FootY(a)), 0.18f * S.U, 0.35f, a);     // reach: the neighbours at the next two or three body lengths
        }
        if (t >= 3.2f) { a.LastDoing = Doing.Bellow; Decide(a, i); }
    }

    // ================================================================ reactions

    /// <summary>A fox is about but not close: every animal lifts its head, one after another (a few tenths of a second apart), once; then it is over for 25 s.</summary>
    private void HerdAlert()
    {
        _alertCool = _time + 25.0;
        int rank = 0;
        for (int k = 0; k < _r.Length; k++)
        {
            Rein o = _r[k];
            if (o.Entering || o.Doing is Doing.Trot or Doing.Gambol or Doing.Alert) continue;
            if (o.Gait is Gait.Down or Gait.Lying or Gait.Up) { o.AlertT = 4f; continue; }
            o.AlertWait = 0.1f + 0.35f * rank++ + 0.2f * (float)Rng.NextDouble();
        }
        _log?.WriteLine($"{_time,8:F2} Reindeer herd alert: heads up once ({rank} animals)");
    }

    /// <summary>
    /// Where a flight goes, decided BEFORE it starts from where the road is clear: the farthest place between 3.5 and 8 heights away, in the way
    /// "away", that is open snow it may stand on, with nothing lying or standing in the road. NaN: it cannot run (it stands tall and watches instead).
    /// </summary>
    private float FleeGoal(Rein a, int i, int dir)
    {
        float H = a.Set.H;
        for (float d = 8f; d >= 3.5f; d -= 0.5f)
        {
            float gx = a.X + dir * d * H;
            if (gx < Lo(a) || gx > Hi(a) || !OkSpot(gx, a.RowY, (int)H)) continue;
            bool ok = true;
            for (float px = a.X; dir * (gx - px) > 0; px += dir * 0.6f * H) if (!S.CanStand(px, a.RowY, 0.3f * H)) { ok = false; break; }
            for (int k = 0; k < _r.Length && ok; k++)
            {
                Rein o = _r[k];
                if (k == i || o.EnterDelay > 0) continue;
                float dd = (o.X - a.X) * dir;
                bool runs = o.Doing == Doing.Trot;
                if (!runs && dd > 0 && dd < d * H + Half(a) + Half(o)) ok = false;      // somebody standing or lying in the road
                if (runs && o.HasGoal && MathF.Abs(o.GoalX - gx) < 2.2f * H) ok = false;
            }
            for (float px = a.X + dir * 1.2f * H; ok && dir * (gx - px) > -1.2f * H; px += dir * 1.2f * H)
                if (S.Crowded(px, a.RowY, Half(a), this, -1, RowsTol)) ok = false;
            if (ok) return gx;
        }
        return float.NaN;
    }

    /// <summary>
    /// A LOOK (a glance at something, Santa excepted) is the HEAD and the ears, never a new body doing: it is held until the animal is at a
    /// natural break (the end of a scrape, between bites), dropped if that takes more than a second, and then the head lifts (about 1 to
    /// 3 s) and eases back, and the doing carries on where it was. It never turns the animal round and never restarts a meal.
    /// </summary>
    private void ReactLook(Rein a, int i, Reaction r, Notice cause)
    {
        bool santa = cause.Who is SantaRig;
        if (a.Gait == Gait.Lying)
        {
            a.AlertT = santa ? 5f : 3f;                                 // a lying animal only lifts its head
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} (lying) react {r} to {cause.Kind}");
            return;
        }
        if (santa)
        {
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} feels Santa: doing {a.Doing}, rest {a.WatchRest:F0}, slots {_passAdultSlots}, calf ok {_passCalfOk}");
            bool takes = a.WatchRest <= 0 && a.WatchWait <= 0 && a.WatchTry <= 0 && (a.Kind == ReindeerKind.Calf ? _passCalfOk : _passAdultSlots > 0 && a.Curious > 0.25f);
            if (takes && a.Doing is Doing.Graze or Doing.Look or Doing.Creep && !a.Entering)
            {
                if (a.Kind == ReindeerKind.Calf) _passCalfOk = false; else _passAdultSlots--;
                a.WatchWait = 0.3f + 1.4f * _watchRank++ + 0.6f * (float)Rng.NextDouble();     // heads lift one after another, not all at once
                _log?.WriteLine($"{_time,8:F2} Reindeer{i} will watch Santa in {a.WatchWait:F1} s");
            }
            return;
        }
        if (a.Doing is Doing.Alert or Doing.Trot or Doing.Gambol or Doing.Shake or Doing.Watch or Doing.Bellow || a.Gait is Gait.Turning or Gait.Shaking or Gait.Leaping) return;
        a.LookPend = true; a.LookPendAt = _time;
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} react {r} to {cause.Kind} -> head (held for a break)");
    }

    /// <summary>
    /// What a Freeze or a Flee is for a reindeer. A Freeze is the head up, ears forward, facing it (head first, then the turn). A Flee is a
    /// short springy trot away, which also tells the herd.
    /// </summary>
    private void React(Rein a, int i, Reaction r, Notice cause)
    {
        float u = S.U, H = a.Set.H;
        int toward = cause.At.X >= a.X ? 1 : -1;
        if (a.Gait == Gait.Lying)
        {
            if (r == Reaction.Flee) { BeginUp(a, true); a.Pending = Reaction.Flee; a.PendingCause = cause; a.PendingAt = _time; }
            else a.AlertT = 3f;
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} (lying) react {r} to {cause.Kind}");
            return;
        }
        if (a.Doing == Doing.Nuzzle) { int pi = a.Partner; if (pi >= 0) EndNuzzle(a, i, false); }
        a.LookPend = false; a.WatchTry = 0; a.WatchWait = 0; a.FollowGo = 0;
        switch (r)
        {
            case Reaction.Look:
            case Reaction.Approach:
                ReactLook(a, i, r, cause);
                return;
            case Reaction.Freeze:
                if (a.Gait == Gait.Walking) { a.Doing = Doing.Alert; a.Timer = 2.5f + 2.5f * a.Timid; a.LookX = cause.At.X; StopWalk(a); a.TurnWhenSettled = toward != a.Dir ? toward : 0; }
                else { a.Doing = Doing.Alert; a.Timer = 2.5f + 2.5f * a.Timid; a.EarT = 0; a.LookX = cause.At.X; a.GPhase = 0; FaceToward(a, toward); }
                break;
            case Reaction.Flee:
            {
                int away = -toward;
                if (a.Gait == Gait.Walking) { a.Doing = Doing.Alert; a.Timer = 1f; a.Pending = Reaction.Flee; a.PendingCause = cause; a.PendingAt = _time; StopWalk(a); }   // brakes first, then goes the other way
                else
                {
                    float fg = FleeGoal(a, i, away);
                    if (float.IsNaN(fg))
                    {
                        // No clear road to run: stand tall and watch (head high, ears forward) rather than start and stop.
                        a.Doing = Doing.Alert; a.Timer = 3f + 2f * a.Timid; a.EarT = 0; a.LookX = cause.At.X; a.GPhase = 0;
                        _log?.WriteLine($"{_time,8:F2} Reindeer{i} cannot run (no clear road), stands tall");
                        break;
                    }
                    StartWalk(a, away, true, fg);
                    a.Timer = 999f;
                    a.LookX = cause.At.X;
                }
                a.GPhase = 0;
                // The herd: one alarm per bout, a rest before the next; the others get a lag and then bolt too.
                if (_time - _friendRest > 5.0)
                {
                    _friendRest = _time;
                    Board.Post(Sense.Friend, new PointF(a.X, FootY(a)), 0.6f * u, 1f, a);
                    _alarmAt = _time + 0.45; _alarmFrom = cause.At.X;
                }
                break;
            }
        }
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} react {r} to {cause.Kind} -> {a.Doing}");
    }

    private void FaceToward(Rein a, int toward, float minDist = 0f, float atX = 0f)
    {
        if (toward == a.Dir) return;
        if (minDist > 0 && MathF.Abs(atX - a.X) < minDist) return;
        BeginTurn(a, toward, false);
    }

    private void StartWatch(Rein a)
    {
        if (a.Gait != Gait.Stand) return;
        a.Doing = Doing.Watch; a.GPhase = 0; a.LookT = 0; a.LookPend = false;
        a.Timer = a.Kind == ReindeerKind.Calf ? 6f : 3.5f + 2f * a.Curious;   // never more than about 6 s of head-following
    }

    /// <summary>The head follows Santa across the sky: its tilt from the angle up to him, and the body turns once if he goes behind.</summary>
    private void ThinkWatch(Rein a, int i, float dt)
    {
        float H = a.Set.H, u = S.U;
        PointF sl = S.SantaSleigh;
        float headY = FootY(a) - 1.2f * H;
        float dx = sl.X - a.X, dy = headY - sl.Y;
        float elev = MathF.Atan2(MathF.Max(0f, dy), MathF.Abs(dx) + 1f);
        a.WatchF = Math.Clamp(0.24f - 0.24f * elev / 0.5f, 0f, 0.24f);
        if (!S.SantaOnScreen) a.Timer = MathF.Min(a.Timer, 0.5f);
        int toward = dx >= 0 ? 1 : -1;
        if (toward != a.Dir && MathF.Abs(dx) > 0.14f * u && a.TurnCool <= 0) BeginTurn(a, toward, false);
        if (a.Timer <= 0)
        {
            a.WatchRest = a.Kind == ReindeerKind.Calf ? 12f + 8f * (float)Rng.NextDouble() : 30f + 30f * (float)Rng.NextDouble();
            a.LastDoing = Doing.Watch; a.TurnReq = 0;
            Decide(a, i);
        }
    }

    // ================================================================ lying down

    private void BeginDown(Rein a, int i)
    {
        a.Doing = Doing.Lie; a.Gait = Gait.Down; a.DownT = 0; a.GPhase = 0; a.Snow = 0; a.LieT = 0; a.LookT = 0; a.LookPend = false;
        // One or two at a time, 12 to 25 s each; the calf lies about as long as its mother.
        a.LieFor = 12f + 13f * (float)Rng.NextDouble();
        if (a.Kind == ReindeerKind.Calf) a.LieFor = MathF.Max(8f, _r[_mom].LieFor - 4f + 8f * (float)Rng.NextDouble());
        a.Timer = 999f;
        a.LieCool = 70f; _nextLie = _time + 30.0;
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} lies down for {a.LieFor:F0} s");
    }

    /// <summary>When the mother lies down the calf follows in a little while (and nobody else is made to: the others roll their own dice, one or two lie at a time).</summary>
    private void AlarmLie(int who)
    {
        if (who != _mom) return;
        for (int k = 0; k < _r.Length; k++)
        {
            Rein o = _r[k];
            if (k == who || o.Kind != ReindeerKind.Calf) continue;
            if (o.Doing is Doing.Gambol or Doing.Trot || o.Gait is Gait.Down or Gait.Lying or Gait.Up) continue;
            if (Rng.NextDouble() < 0.7) o.LieAfter = 3f + 4f * (float)Rng.NextDouble();
        }
    }

    private void BeginUp(Rein a, bool fast)
    {
        if (a.Gait is not (Gait.Lying or Gait.Down)) return;
        a.Gait = Gait.Up; a.UpT = 0; a.UpFor = fast ? 0.9f : UpTime;
        a.AfterUpShake = a.Snow > 0.3f || Rng.NextDouble() < 0.4;
        if (a.Snow > 0.05f)
        {
            // The heap slides off the back as the hind end rises.
            float H = a.Set.H;
            for (int n = 0; n < 12; n++)
            {
                float sx = (float)(Rng.NextDouble() * 2 - 1) * 0.6f * H;
                Spawn(a, 0, a.X + sx, FootY(a) - (0.5f + 0.35f * (float)Rng.NextDouble()) * H, sx / H * 40f * H / 60f, -20f * (float)Rng.NextDouble(), 0.8f + 0.4f * (float)Rng.NextDouble(), Rng.Next(3));
            }
            a.Snow *= 0.0f;
        }
        if (a.Doing != Doing.Gambol) a.Doing = Doing.Lie;
    }


    // ================================================================ the calf's play

    private bool CanGambol(Rein a)
    {
        foreach (Rein o in _r)
        {
            if (o == a) continue;
            if (o.Entering || o.Gait is not (Gait.Stand or Gait.Pawing) || o.Doing is not (Doing.Graze or Doing.Look or Doing.Creep)) return false;
        }
        if (!Idle(a)) return false;
        foreach (Notice n in Board.Live)
        {
            if (n.Kind is not (Sense.Threat or Sense.Loud or Sense.Movement) || n.Who is Rein) continue;
            float dx = n.At.X - a.X, dy = n.At.Y - FootY(a), strength = (1f - MathF.Sqrt(dx * dx + dy * dy) / (n.Reach * 1.3f)) * n.Weight;
            if (strength > 0.25f && n.Who != null) { _lastNoise = _time; break; }
        }
        return _time - _lastNoise > 2.0;
    }

    private Rein? NearestAdult(Rein a)
    {
        Rein? best = null; float bd = float.MaxValue;
        foreach (Rein o in _r) if (o != a && o.Kind != ReindeerKind.Calf && MathF.Abs(o.X - a.X) < bd) { bd = MathF.Abs(o.X - a.X); best = o; }
        return best;
    }

    private void StartGambol(Rein a)
    {
        a.Doing = Doing.Gambol; a.GBouts = 1 + (Rng.NextDouble() < 0.35 ? 1 : 0);
        a.Fly = a.Fly || Rng.NextDouble() < 0.35;
        a.Pace.Halt(); a.Timer = 999f;
        if (!BeginBout(a, 2 + Rng.Next(2))) { a.Doing = Doing.Look; a.Timer = 1f; a.Fly = false; _nextGambol = _time + 12; _log?.WriteLine($"{_time,8:F2} Reindeer calf gambol has no open ground"); return; }
        // The nearest cow looks round at the little one.
        Rein? cow = NearestAdult(a);
        if (cow != null && cow.Gait == Gait.Stand && Rng.NextDouble() < 0.7) { cow.LookT = 2.8f; cow.EarT = 0; }      // the cow lifts her head to the little one (the head only, her meal carries on)
        _log?.WriteLine($"{_time,8:F2} Reindeer calf gambol begins: bouts {a.GBouts} springs {a.GCount} fly {a.Fly} x {a.X:F0}");
    }

    /// <summary>Is the strip of ground from the calf out "reach" in direction dir (and back) clear of every other animal, with a body length to spare on each side?</summary>
    private bool OpenGround(Rein a, int dir, float reach)
    {
        float lo = S.Width * 0.08f, hi = S.Width * 0.92f, x1 = a.X + dir * reach;
        if (x1 < lo || x1 > hi) return false;
        float from = MathF.Min(a.X, x1), to = MathF.Max(a.X, x1);
        foreach (Rein o in _r)
        {
            if (o == a || _force is "fly" or "gambol") continue;          // (the test dials let the calf play through the herd: a film of the play itself)
            float bl = 1.9f * o.Set.H + 0.3f * a.Set.H;                 // a body length of the other animal (nose to tail, antlers aside) and the calf's own half
            if (o.X > from - bl && o.X < to + bl) return false;
        }
        // Every few steps along the way must be ground feet may go (the lamp post's foot, the sapling, the feeder's post, the ice).
        float step = 0.6f * a.Set.H;
        for (float x = from; x <= to; x += step) if (!S.CanStand(x, a.RowY, 0.4f * a.Set.H)) return false;
        return S.CanStand(x1, a.RowY, 0.4f * a.Set.H) && S.CanStand(a.X, a.RowY, 0.1f);
    }

    private bool BeginBout(Rein a, int springs)
    {
        float H = a.Set.H, reach = (a.Fly ? 10.5f : 8.5f) * H;
        Rein? cow = NearestAdult(a);
        int dir = cow == null || a.X >= cow.X ? 1 : -1;               // away from the cow first
        if (!OpenGround(a, dir, reach)) dir = -dir;
        if (!OpenGround(a, dir, reach)) return false;                  // no clear row either way: the play does not start
        a.GPh = 0; a.GCount = springs; a.GHomeX = a.X; a.GOut = true; a.GN = 0; a.GG = 0;
        if (dir != a.Dir) BeginTurn(a, dir, false);
        return true;
    }

    private void AbortGambol(Rein a, string why)
    {
        // Leaping stays on until the pose has come back to the stand through its drawn pages (TickGait): no jump from the air to the ground.
        if (a.Gait == Gait.Leaping) { a.PoseT = 0; a.PlopT = -1; }
        a.Lift = 0; a.Doing = Doing.Look; a.Timer = 1f; a.Pace.Halt();
        if (a.Gait is Gait.Lying or Gait.Down) BeginUp(a, true);
        a.Doing = Doing.Look; a.GPh = 0; a.Fly = false;
        _nextGambol = _time + 60;
        _log?.WriteLine($"{_time,8:F2} Reindeer calf gambol aborted: {why}");
    }

    /// <summary>
    /// The script, once a frame. Phases: 0 wait for the turn, 1 springs, 2 bounds, 3 skid, 4 wait for the turn at the far end,
    /// 5 turn to face her mother and stand, 6 (spare), 7 take-off, 8 hang, 9 fall, 10 plop, 11 lie a moment, 12 get up.
    /// </summary>
    private void TickGambol(Rein a, int i, float dt)
    {
        float H = a.Set.H;
        Rein? cow = NearestAdult(a);
        if (cow != null && cow.Doing is Doing.Alert or Doing.Trot && a.GPh < 90) { AbortGambol(a, "cow alarmed"); return; }
        a.GG += dt;
        switch (a.GPh)
        {
            case 0:
                if (!Idle(a)) return;
                a.GPh = 1; a.GG = 0; a.GN = 0;
                break;
            case 1:
                a.Gait = Gait.Leaping;
                if (a.GG < SpringGround) { a.PoseT = 1; a.Lift = 0; }
                else
                {
                    float q = (a.GG - SpringGround) / (SpringFor - SpringGround);
                    a.PoseT = 2; a.Lift = SpringLift * H * 4 * q * (1 - q);
                    a.X += a.Dir * SpringSpeed * H * dt;
                }
                if (a.GG >= SpringFor)
                {
                    LandGlint(a, 0.4f);
                    a.GG = 0; a.GN++;
                    if (a.GN >= a.GCount) { a.GPh = 2; a.GN = 0; a.GCount = a.GOut ? 3 : Math.Max(2, (int)MathF.Round((MathF.Abs(a.X - a.GHomeX) - 1.05f * H) / (1.5f * H))); }
                }
                break;
            case 2:
            {
                a.Gait = Gait.Leaping;
                float spd = BoundSpeed * (a.Fly && a.GOut ? 1f + 0.18f * a.GN : 1f);
                if (a.GG < BoundGround) { a.PoseT = 4; a.Lift = 0; }
                else
                {
                    float q = (a.GG - BoundGround) / (BoundFor - BoundGround);
                    a.PoseT = q < 0.55f ? 3 : 2; a.Lift = BoundLift * H * 4 * q * (1 - q);
                    a.X += a.Dir * spd * H * dt;
                }
                if (a.GG >= BoundFor)
                {
                    LandGlint(a, 0.6f);
                    a.GG = 0; a.GN++;
                    if (a.GN >= a.GCount) { if (a.Fly && a.GOut) { a.GPh = 7; a.Lift = 0; } else { a.GPh = 3; a.PoseT = 5; a.Lift = 0; } }
                }
                break;
            }
            case 3:
                a.Gait = Gait.Leaping; a.Lift = 0;
                a.PoseT = a.GG < SkidFor ? 5 : 0;                      // the skid, then back to the stand through drawn pages
                if (a.GG < SkidFor)
                {
                    a.X += a.Dir * BoundSpeed * H * (1 - a.GG / SkidFor) * dt;
                    if ((float)Rng.NextDouble() < dt * 30f) Spawn(a, 0, a.X - a.Dir * 0.3f * H, FootY(a) - 0.05f * H, -a.Dir * 40f * H / 60f * (float)Rng.NextDouble(), -50f * H / 60f * (float)Rng.NextDouble(), 0.5f, 0);   // the skid throws snow
                }
                else
                {
                    if (a.GamFrom == 0 && a.GamTo == 0) a.Gait = Gait.Stand;           // on its feet again: the pose has come all the way back
                    if (a.Gait == Gait.Stand && a.GG >= SkidFor + 0.2f)
                    {
                        if (a.GOut) { a.GOut = false; BeginTurn(a, -a.Dir, false); a.GPh = 4; }
                        else if (--a.GBouts > 0 && BeginBout(a, 2)) { }
                        else { FinishGambol(a); }
                    }
                }
                break;
            case 4:
                if (!Idle(a)) return;
                a.GPh = 2; a.GG = 0; a.GN = 0; a.GCount = Math.Max(2, (int)MathF.Round((MathF.Abs(a.X - a.GHomeX) - 1.05f * H) / (1.5f * H)));
                break;
            case 5:
                if (!Idle(a)) return;
                // Stands and looks at her mother; the cow looks back; both go back to grazing.
                a.Doing = Doing.Look; a.Timer = 2.2f; a.ScanTurn = false; a.ScanPhase = 0; a.ScanTotal = 2.2f; a.GStay = 2.4f; a.EarT = 0; a.GPh = 0; a.Fly = false;
                if (cow != null && cow.Gait == Gait.Stand && cow.Doing is Doing.Graze or Doing.Look) { cow.LookT = 2.0f; cow.EarT = 0; }   // the cow looks back (the head only)
                _nextGambol = _time + 55 + 40 * Rng.NextDouble();
                _log?.WriteLine($"{_time,8:F2} Reindeer calf gambol over x {a.X:F0}");
                break;
            case 7:
            {
                // TAKE-OFF: legs tucked, the lift eases out to FlyPeak while it still runs forward.
                a.Gait = Gait.Leaping; a.PoseT = 6;
                float q = Math.Clamp(a.GG / 0.38f, 0f, 1f);
                a.Lift = FlyPeak * H * (1 - (1 - q) * (1 - q));
                a.X += a.Dir * 4.6f * H * (1f - 0.3f * q) * dt;
                if (a.GG < dt * 1.5f)
                {
                    Rein? c2 = NearestAdult(a);
                    if (c2 != null && c2.Gait == Gait.Stand && Rng.NextDouble() < 0.8) { c2.LookT = 3f; c2.EarT = 0; }
                }
                if (Rng.NextDouble() < dt * 14) Sparkle(a, 0.5f);
                if (a.GG >= 0.38f) { a.GPh = 8; a.GG = 0; }
                break;
            }
            case 8:
            {
                // HANG: half a second at the top, a little bob, sparkles drifting off it.
                a.Gait = Gait.Leaping; a.PoseT = 6;
                a.Lift = FlyPeak * H + 0.03f * H * MathF.Sin(a.GG * 9f);
                a.X += a.Dir * 2.0f * H * (1f - a.GG / 0.55f) * dt;
                if (Rng.NextDouble() < dt * 22) Sparkle(a, 0.7f);
                if (a.GG >= 0.55f) { a.GPh = 9; a.GG = 0; }
                break;
            }
            case 9:
            {
                a.Gait = Gait.Leaping; a.PoseT = 6;                     // falls with the legs still tucked
                float q = Math.Clamp(a.GG / 0.28f, 0f, 1f);
                a.Lift = FlyPeak * H * (1 - q * q);
                a.X += a.Dir * 1.0f * H * dt;
                if (a.GG >= 0.28f)
                {
                    // PLOP: the belly meets the snow, a puff all round, and the tucked body settles flat over a fifth of a second (the Plop pages).
                    a.Lift = 0; a.GPh = 10; a.GG = 0; a.PlopT = 0;
                    Plop(a);
                }
                break;
            }
            case 10:
                a.Gait = Gait.Leaping; a.Lift = 0;
                a.PlopT = Math.Clamp(a.GG / 0.22f, 0f, 1f);
                if (a.GG >= 0.22f) { a.PlopT = -1; a.Gait = Gait.Lying; a.LieT = 0; a.Snow = 0; a.GPh = 11; a.GG = 0; a.GamFrom = a.GamTo = a.PoseT = 0; a.GamU = 0; }
                break;
            case 11:
                a.Gait = Gait.Lying;
                if (a.GG >= 0.85f) { BeginUp(a, true); a.Doing = Doing.Gambol; a.GPh = 12; a.GG = 0; }
                break;
            case 12:
                if (Idle(a)) FinishGambol(a);
                break;
        }
    }

    private void FinishGambol(Rein a)
    {
        Rein? cow = NearestAdult(a);
        int toward = cow == null ? a.Dir : cow.X >= a.X ? 1 : -1;
        if (toward != a.Dir) BeginTurn(a, toward, false);
        a.GPh = 5; a.GG = 0;
    }

    private void LandGlint(Rein a, float chance)
    {
        // A sparkle where a hoof lands: the snow glints, and a small puff of powder.
        if (Rng.NextDouble() > chance) return;
        float H = a.Set.H, sc = H / 60f;
        float hx = a.X + a.Dir * 0.4f * H;
        Spawn(a, 2, hx, FootY(a) - 0.04f * H, 0, 0, 0.35f, 0);
        for (int n = 0; n < 3; n++) Spawn(a, 0, hx, FootY(a) - 0.03f * H, (float)(Rng.NextDouble() - 0.5) * 80f * sc, -(30f + 70f * (float)Rng.NextDouble()) * sc, 0.4f, Rng.Next(3));
    }

    private void Sparkle(Rein a, float life)
    {
        float H = a.Set.H;
        Spawn(a, 2, a.X + (float)(Rng.NextDouble() - 0.5) * 1.1f * H, FootY(a) - a.Lift - (0.2f + 0.8f * (float)Rng.NextDouble()) * H, (float)(Rng.NextDouble() - 0.5) * 10f, 6f * H / 60f, life, 0);
    }

    private void Plop(Rein a)
    {
        float H = a.Set.H, sc = H / 60f;
        for (int n = 0; n < 14; n++)
        {
            float ang = (float)Rng.NextDouble() * MathF.PI;           // up and outward on both sides
            float sp = (40f + 90f * (float)Rng.NextDouble()) * sc;
            Spawn(a, 0, a.X + (float)(Rng.NextDouble() - 0.5) * 1.0f * H, FootY(a) - 0.05f * H, MathF.Cos(ang) * sp, -MathF.Sin(ang) * sp * 1.4f, 0.55f + 0.35f * (float)Rng.NextDouble(), Rng.Next(3));
        }
        for (int n = 0; n < 4; n++) Spawn(a, 3, a.X + (float)(Rng.NextDouble() - 0.5) * 0.9f * H, FootY(a) - 0.12f * H, (float)(Rng.NextDouble() - 0.5) * 30f * sc, -8f * sc, 0.9f, 0);
        for (int n = 0; n < 3; n++) Sparkle(a, 0.6f);
    }

    // ================================================================ flying snow, breath, sparkles

    private void Spawn(Rein a, byte kind, float x, float y, float vx, float vy, float life, int size)
    {
        ref Fleck f = ref a.Fx[a.FxNext];
        a.FxNext = (a.FxNext + 1) % a.Fx.Length;
        f.X = x; f.Y = y; f.VX = vx; f.VY = vy; f.Age = 0; f.Life = life; f.Size = size; f.Kind = kind;
    }

    private void UpdateFx(Rein a, float dt)
    {
        if (a.Set == null) return;
        float g = 520f * a.Set.H / 60f;
        for (int n = 0; n < a.Fx.Length; n++)
        {
            ref Fleck f = ref a.Fx[n];
            if (f.Age >= f.Life) continue;
            f.Age += dt;
            f.X += f.VX * dt; f.Y += f.VY * dt;
            if (f.Kind == 0) { f.VY += g * dt; f.VX *= 1f - 0.8f * dt; }
            else if (f.Kind == 1) { f.VX *= 1f - 0.5f * dt; }
        }
    }

    private PointF NosePoint(Rein a)
    {
        ReindeerSet s = a.Set;
        float f = Math.Clamp(a.NeckF, 0f, 1f);
        int hp = Math.Clamp((int)MathF.Round(f * (ReindeerArt.HeadPages - 1)), 0, ReindeerArt.HeadPages - 1);
        RPage pg = PickBody(a, out _);
        PointF nz = s.Nose[hp];
        float dir = a.Dir;
        return new PointF(a.X + dir * (pg.NX + nz.X) * s.H, FootY(a) - (pg.NY + nz.Y) * s.H - a.Lift);
    }

    // ================================================================ drawing

    public override void Collect(DepthQueue q)
    {
        long t0 = _log == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < _r.Length; i++) q.Add(FootY(_r[i]), this, i);
        if (_log != null) _tCollect += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
    }

    /// <summary>
    /// The body page for this frame. "id" names it for the mind log: book number times 100 plus the page (1 walk, 2 trot, 3 settle, 4 trot start/stop,
    /// 5 paw, 6 down, 7 up, 8 lying, 9 shake, 10 the calf's play, 0 and 3x07 the square stance).
    /// </summary>
    /// <summary>
    /// Which head picture: the jaw goes closed, half, open, half, closed while it chews (two and a half times a second); an ear flick goes out
    /// half way, all the way, half way back. (Two pictures swapping three times a second was a jerk each time.) 0 closed, 1 open, 2 ear back,
    /// 3 jaw half open, 4 ear half back.
    /// </summary>
    private static int HeadKind(Rein a)
    {
        if (a.Gait == Gait.Turning) return 0;
        if (a.ChewT >= 0)
        {
            float open = 0.5f - 0.5f * MathF.Cos(MathF.Tau * (a.ChewT * 2.5f % 1f));
            return open < 0.25f ? 0 : open < 0.75f ? 3 : 1;
        }
        if (a.EarT >= 0)
        {
            float e = MathF.Sin(MathF.PI * MathF.Min(1f, a.EarT / EarTime));
            return e < 0.3f ? 0 : e < 0.8f ? 4 : 2;
        }
        return 0;
    }

    private RPage PickBody(Rein a, out int id)
    {
        ReindeerSet s = a.Set;
        id = 300 + ReindeerArt.SettlePages - 1;
        switch (a.Gait)
        {
            case Gait.Turning: return s.Stand;                       // (the turn's own pictures are chosen in DrawInner)
            case Gait.Leaping:
            {
                if (a.PlopT >= 0f) { int pp2 = Math.Clamp((int)MathF.Round(a.PlopT * (ReindeerArt.PlopPages - 1)), 0, ReindeerArt.PlopPages - 1); id = 1100 + pp2; return s.Plop[pp2]; }
                if (a.GamFrom == a.GamTo) { id = 1000 + a.GamFrom; return s.Gam[a.GamFrom]; }
                int lo2 = Math.Min(a.GamFrom, a.GamTo), hi2 = Math.Max(a.GamFrom, a.GamTo);
                float uu = a.GamFrom < a.GamTo ? a.GamU : 1f - a.GamU;        // along the stored pages (lo to hi)
                int ix = Math.Clamp((int)MathF.Round(uu * (ReindeerArt.GamInter + 1)), 0, ReindeerArt.GamInter + 1);
                if (ix == 0) { id = 1000 + lo2; return s.Gam[lo2]; }
                if (ix == ReindeerArt.GamInter + 1) { id = 1000 + hi2; return s.Gam[hi2]; }
                id = 1200 + lo2 * 10 + ix; return s.GamEdge[lo2, hi2]![ix - 1];
            }
            case Gait.Walking:
            case Gait.Stopping:
            {
                if (a.TrotMode)
                {
                    float c = a.Pace.Walked / TrotStride(a);
                    int tpg = (int)MathF.Floor((c - MathF.Floor(c)) * ReindeerArt.TrotPages) % ReindeerArt.TrotPages;
                    id = 200 + tpg;
                    return s.Trot[tpg];
                }
                float cyc = a.Pace.Walked / Stride(a);
                int wpg = (int)MathF.Floor((cyc - MathF.Floor(cyc)) * ReindeerArt.WalkPages) % ReindeerArt.WalkPages;
                id = 100 + wpg;
                return s.Walk[wpg];
            }
            case Gait.Settling:
            case Gait.Rising:
            {
                float k = Math.Clamp(a.GaitT / a.GaitFor, 0f, 0.999f);
                if (a.TrotMode)
                {
                    int tp = (int)(k * ReindeerArt.TrotInPages);
                    int ti = a.Gait == Gait.Rising ? tp : ReindeerArt.TrotInPages - 1 - tp;
                    id = 400 + ti;
                    return s.TrotIn[ti];
                }
                int page = (int)(k * ReindeerArt.SettlePages);
                if (a.Gait == Gait.Rising) page = ReindeerArt.SettlePages - 1 - page;
                id = 300 + page;
                return s.Settle[page];
            }
            case Gait.Pawing: { int pp = Math.Clamp((int)(a.PawT * ReindeerArt.PawPages), 0, ReindeerArt.PawPages - 1); id = 500 + pp; return s.Paw[pp]; }
            case Gait.Down: { int dp = Math.Clamp((int)(a.DownT / DownTime * ReindeerArt.LiePages), 0, ReindeerArt.LiePages - 1); id = 600 + dp; return s.Down[dp]; }
            case Gait.Up: { int up = Math.Clamp((int)(a.UpT / a.UpFor * ReindeerArt.LiePages), 0, ReindeerArt.LiePages - 1); id = 700 + up; return s.Up[up]; }
            case Gait.Lying: id = 800 + ReindeerArt.LiePages - 1; return s.Lie;
            case Gait.Shaking:
            {
                // A fast oscillation that dies away: the quantised value picks the page.
                float k = a.ShakeT / ShakeTime, amp = 1f - k * k;
                float v = MathF.Sin(a.ShakeT * 2f * MathF.PI * 4.2f) * amp;
                int sp = Math.Clamp((int)MathF.Round((v + 1f) / 2f * (ReindeerArt.ShakePages - 1)), 0, ReindeerArt.ShakePages - 1);
                id = 900 + sp;
                return s.Shake[sp];
            }
            default: return s.Stand;
        }
    }

    private void Put(FrameBuffer fb, Sprite spr, float x, float y, int ox, int oy, bool mirror, byte nearness, float opacity = 1f) =>
        spr.DrawSmooth(fb, x - (mirror ? spr.Width - 1 - ox : ox), y - oy, opacity, null, mirror, S.NearDepth, nearness);   // at a fraction of a pixel: a walk glides, and a head that eases between pages does not tick by whole pixels

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (_log == null) { DrawInner(fb, index); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        DrawInner(fb, index);
        _tDraw += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
    }

    private void DrawInner(FrameBuffer fb, int index)
    {
        Rein a = _r[index];
        ReindeerSet s = a.Set;
        float H = s.H, foot = FootY(a);
        if (a.X < -3.5f * H || a.X > S.Width + 3.5f * H) return;                      // still beyond the edge of the screen (the entrance)
        bool turning = a.Gait == Gait.Turning;
        float tq = turning ? Math.Clamp(a.TurnT / a.TurnFor, 0f, 1f) : 0f;
        int figK = turning ? (int)MathF.Round(Smooth(tq) * ReindeerArt.TurnSteps) : 0;      // the page of the half turn, 0 and TurnSteps being the ordinary standing pages
        bool fig = turning && figK > 0 && figK < ReindeerArt.TurnSteps;
        bool mirror = fig ? a.TurnFrom < 0 : a.Dir < 0;
        byte near = S.Nearness(foot);
        bool down = a.Gait is Gait.Down or Gait.Lying or Gait.Up;

        // 1. The shadow on the snow (it stays on the ground when the animal leaps, and thins).
        Sprite sh = down ? s.ShadowLie : s.Shadow;
        float shOp = 1f - Math.Min(0.75f, a.Lift / (0.9f * H));
        Put(fb, sh, a.X - (turning ? 0 : a.Dir) * 0.04f * H, foot + 0.02f * H, sh.Width / 2, sh.Height / 2, false, near, shOp);

        // 2. The scraped hole, where the nose will go (drawn under the animal).
        if (a.HasMark) Put(fb, a.Mark, a.MarkX, foot + 0.01f * H, a.Mark.Width / 2, a.Mark.Height / 2, false, near, Math.Clamp(a.MarkA, 0f, 1f));

        // 3. The body.
        RPage pg = PickBody(a, out _);
        int lift = (int)MathF.Round(a.Lift);
        if (fig)
        {
            // The turn: a whole animal (body and head painted together, so the neck and the antlers sort by depth) at this angle.
            RPage tp = s.Fig[a.TurnSide > 0 ? 0 : 1, a.TurnLevel, figK]!;
            Put(fb, tp.S, a.X, foot - lift, tp.OX, tp.OY, mirror, near);
        }
        else Put(fb, pg.S, a.X, foot - lift, pg.OX, pg.OY, mirror, near);
        if (!fig && a.Gait == Gait.Lying && a.Snow > 0.02f)
            Put(fb, s.CapLie.S, a.X, foot, s.CapLie.OX, s.CapLie.OY, mirror, near, Math.Clamp(a.Snow * 1.2f, 0f, 1f));

        // 4. The head: the neck page, with the bite's tug off it, the chew's jaw, the ear flick.
        float f = a.NeckF;
        f += 0.020f * a.DriftAmp * Organic.Drift(_time * 0.6, a.Restless);   // a standing head is never quite still (eased in and out: see DriftAmp)
        if (a.BiteT >= 0) f -= 0.12f * (0.5f - 0.5f * MathF.Cos(MathF.Tau * MathF.Min(1f, a.BiteT / BiteTime)));   // the nod: one smooth cosine, down and back
        f = Math.Clamp(f, 0f, 1f);
        float nx = a.X + (mirror ? -1 : 1) * pg.NX * H, ny = foot - pg.NY * H - lift;
        if (!fig)
        {
            // BETWEEN two head pages, not on the nearer one: a slow head used to tick from page to page, its antler
            // tips jumping four pixels against the dark pines (forty pops in ten seconds on the detector). The
            // engine dissolves from the page below to the page above by how far between them the head is.
            float fp = f * (ReindeerArt.HeadPages - 1);
            int lo = Math.Clamp((int)MathF.Floor(fp), 0, ReindeerArt.HeadPages - 1), hi = Math.Min(ReindeerArt.HeadPages - 1, lo + 1);
            int kind = HeadKind(a);
            RPage pl = s.Head[lo, kind], ph = s.Head[hi, kind];
            Sprite.DrawBlend(fb, pl.S, nx - (mirror ? pl.S.Width - 1 - pl.OX : pl.OX), ny - pl.OY,
                                 ph.S, nx - (mirror ? ph.S.Width - 1 - ph.OX : ph.OX), ny - ph.OY, fp - lo, mirror, S.NearDepth, near);
        }

        // 5. Flying snow, breath, sparkles.
        for (int n = 0; n < a.Fx.Length; n++)
        {
            ref Fleck p = ref a.Fx[n];
            if (p.Age >= p.Life) continue;
            float k = p.Age / p.Life;
            switch (p.Kind)
            {
                case 0:
                {
                    Sprite fl = _flake[Math.Clamp((int)p.Size, 0, 2)];
                    Put(fb, fl, p.X, p.Y, fl.Width / 2, fl.Height / 2, false, near, 1f - k * k);
                    break;
                }
                case 1:
                {
                    int ci = Math.Min(4, 1 + (int)(k * 4));
                    Sprite c = _cloud[ci];
                    Put(fb, c, p.X, p.Y, c.Width / 2, c.Height / 2, false, near, 0.55f * (1f - k) * Math.Min(1f, p.Age * 8f));
                    break;
                }
                case 2:
                {
                    float tw = MathF.Sin(MathF.PI * k);
                    Put(fb, _spark, p.X, p.Y, _spark.Width / 2, _spark.Height / 2, false, near, tw);
                    break;
                }
                default:
                {
                    int ci = Math.Min(4, 2 + (int)(k * 3));
                    Sprite c = _cloud[ci];
                    Put(fb, c, p.X, p.Y, c.Width / 2, c.Height / 2, false, near, 0.7f * (1f - k));
                    break;
                }
            }
        }
    }
}
