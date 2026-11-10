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
/// TEST AIDS: PECKWORKS_REINDEER=paw|walk|lie|shake|alert|trot|gambol|fly|watch|turn|graze forces that at 6 s (graze keeps them
/// grazing for ever, walk keeps the leader walking); PECKWORKS_SHEET=reindeer;file.png writes every page of every flip-book.
/// </summary>
internal sealed class ReindeerHerd : Resident
{
    private enum Doing { Graze, Look, Walk, Lie, Alert, Trot, Watch, Gambol, Shake }
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
        public int BitesLeft;
        public int GPhase; public float GT, PawT, PawFor = PawTime, EatT; public int Scrapes;   // grazing: scan, paw, eat, chew
        public float MarkX, MarkA; public bool HasMark; public Sprite Mark = null!;             // the scraped hole in the snow (px, opacity)
        public float WatchRest, WalkSecs, PostT, RestT, TurnCool, FollowLag = -1, AlertT, LookX = float.NaN, WatchF = 0.1f;
        public float LieT, LieFor, Snow, DownT, UpT, UpFor = UpTime, LieAfter = -1, ShakeT;
        public bool AfterUpShake, ForceGambol;
        public Reaction Pending; public Notice PendingCause;
        public bool JustGrazed;
        public float Lift; public int Pose;                 // the calf's leaps: pixels off the ground and which pose page
        public int GPh, GN, GCount, GBouts; public float GG, GHomeX, GStay; public bool GOut, Fly;
        public float RoamBias;                              // where in the herd's drift this animal likes to be (a share of the lane, so the herd is loose)
        public float MarkDx;                                // how far ahead of the feet the nose lands when it is down, in heights
        public Fleck[] Fx = new Fleck[48];
        public int FxNext;
    }

    // ---- timings and speeds ----
    private const float SettleFor = 0.40f, PawTime = 0.92f, DownTime = 2.5f, UpTime = 1.8f, ShakeTime = 1.0f;
    private const float NeckSpring = 55f, NeckDamp = 14.5f;      // the neck eases to where it is wanted like a damped spring
    private const float SpringFor = 0.52f, SpringGround = 0.10f, BoundFor = 0.34f, BoundGround = 0.05f, SkidFor = 0.40f;
    private const float SpringLift = 0.55f, BoundLift = 0.20f, FlyPeak = 0.62f, SpringSpeed = 1.25f, BoundSpeed = 5.0f;   // in the calf's own heights (a second)

    // The herd stands by a cabin painted at STORYBOOK scale (about 37 pixels to the metre at 1080p where the herd's row gives about 54), so
    // beside it a true-size bull was nearly half the cabin's width. Every reindeer is drawn at this share of its real size.
    private const float HerdScale = 0.78f;

    private readonly Rein[] _r;
    private readonly int _leader;
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
    private int _passAdultSlots;                    // per Santa pass: how many adults may follow him with their heads (0 or 1, so one on average or less)
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
        int[] slot = new int[count];
        for (int i = 0; i < count; i++) slot[i] = i;
        for (int i = count - 1; i > 0; i--) { int j = Rng.Next(i + 1); (slot[i], slot[j]) = (slot[j], slot[i]); }   // which stretch of the lane each animal starts in
        var heights = new int[count];
        for (int i = 0; i < count; i++)
        {
            ReindeerKind kind = i == 0 ? ReindeerKind.Bull : (count >= 3 && i == count - 1) ? ReindeerKind.Calf : ReindeerKind.Cow;
            float depth = 0.01f + 0.14f * (float)Rng.NextDouble();
            // THE ROW IS A LEVEL LINE: the hill's crest rises and falls along the screen, but an animal with one painted size must keep one
            // scale, so its row sits at constant y: just below the lowest point of the crest, plus its own depth into the snow. Its size is then
            // read at that very y: a bull 1.15 m at the shoulder, a cow 1.05, a calf three fifths of a cow. (First pass: the row followed the crest and the size drifted up to 16%.)
            float lowest = 0;
            for (int sx = (int)(s.Width * 0.04f); sx < s.Width * 0.96f; sx += 8) lowest = MathF.Max(lowest, s.WalkY(sx));
            float rowY = lowest + depth * (s.Height - lowest);
            var a = new Rein { Kind = kind, Dir = Rng.Next(2) == 0 ? 1 : -1, Depth = depth, RowY = rowY };
            a.RoamBias = count > 1 ? (i / (float)(count - 1) - 0.5f) * 0.30f : 0f;
            float m = s.Metre(rowY);
            heights[i] = Math.Max(10, (int)MathF.Round(m * HerdScale * (kind == ReindeerKind.Bull ? 1.15f : kind == ReindeerKind.Cow ? 1.05f : 0.63f)));
            a.Timid = kind == ReindeerKind.Calf ? 0.45f + 0.5f * timid[i] : kind == ReindeerKind.Bull ? 0.7f * timid[i] : timid[i];
            a.Curious = kind == ReindeerKind.Calf ? 0.8f + 0.2f * curious[i] : curious[i];
            a.Senses = new Attention(a.Timid, a.Curious) { Keen = 1.2f };
            a.Restless = (float)Rng.NextDouble() * 10;
            a.NeckF = 0.4f;
            a.GPhase = 0; a.GT = 0.6f + 1.4f * (float)Rng.NextDouble();
            a.NextBite = 0.5f; a.NextEar = 1f + 3f * (float)Rng.NextDouble();
            a.Breath = 1f + 3f * (float)Rng.NextDouble();
            a.Timer = 999f;
            a.Doing = Doing.Graze;
            _r[i] = a;
        }
        _log?.WriteLine($"Reindeer herd of {count} built for {s.Width}x{s.Height}");
        _leader = count >= 2 ? 1 : 0;
        float href = count == 0 ? 20 : heights[_leader];
        _walkV = 0.72f * href;
        _trotV = 1.75f * href;
        for (int i = 0; i < count; i++)
        {
            Rein a = _r[i];
            a.Pace = new Pace(_walkV * 2.2f);
            // SPREAD ACROSS THE WHOLE FAR LANE: the lane is cut into one slice per animal and each starts in its own slice (the cabin's front
            // excluded by OkSpot, which slides it off to the nearest free ground). The calf starts beside its mother, close but not on her.
            if (a.Kind == ReindeerKind.Calf && count >= 3) continue;
            float lane0 = s.Width * 0.06f, lane1 = s.Width * 0.94f;
            a.X = lane0 + (lane1 - lane0) * (slot[i] + 0.2f + 0.6f * (float)Rng.NextDouble()) / count;
            for (int tries = 0; tries < 60 && !OkSpot(a.X, a.RowY, heights[i]); tries++) a.X += (tries % 2 == 0 ? 1 : -1) * (tries + 1) * heights[i] * 0.7f;
            a.X = Math.Clamp(a.X, s.Width * 0.04f + 2 * heights[i], s.Width * 0.96f - 2 * heights[i]);
        }
        if (count >= 3)
        {
            Rein mom = _r[_leader], calf = _r[count - 1];
            int side = mom.X < s.Width * 0.5f ? 1 : -1;
            calf.X = mom.X + side * (1.9f + 0.8f * (float)Rng.NextDouble()) * heights[count - 1] * 1.6f;
            for (int tries = 0; tries < 60 && !OkSpot(calf.X, calf.RowY, heights[count - 1]); tries++) calf.X += (tries % 2 == 0 ? 1 : -1) * (tries + 1) * heights[count - 1] * 0.7f;
            calf.X = Math.Clamp(calf.X, s.Width * 0.04f + 2 * heights[count - 1], s.Width * 0.96f - 2 * heights[count - 1]);
        }
        for (int i = 0; i < count; i++) _log?.WriteLine($"Reindeer{i} {_r[i].Kind} H {heights[i]} start x {_r[i].X:F0} width {s.Width} door {DoorLo:F0} {DoorHi:F0}");
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        float u = s.U;
        BuildInBackground(() =>
        {
            // The painting must not steal the main thread's frames in the opening seconds: it naps now and
            // then (a lowered priority starved the build on a busy machine: the herd had not appeared after ten seconds) (ReindeerArt.Nap), so the snow keeps its 60 a second while the animals are drawn.
            var oldPriority = Thread.CurrentThread.Priority;
            try {
            // Compile every method of the herd now, on this thread, so the first Update and the first draw on the frame path never wait for the JIT
            // (the first pass had a 3 to 12 ms first update and a 60 ms first frames).
            foreach (var m in typeof(ReindeerHerd).GetMethods(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (!m.IsAbstract && !m.ContainsGenericParameters) { try { System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { } }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();
            var sets = new ReindeerSet[count];
            for (int i = 0; i < count; i++)
            {
                sets[i] = ReindeerArt.Build(heights[i], _r[i].Kind);
                _r[i].Mark = PaintMark(heights[i]);
                PointF nose = sets[i].Nose[ReindeerArt.HeadPages - 1];
                _r[i].MarkDx = ReindeerArt.NeckX + nose.X - 0.22f;
            }
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
            _log?.WriteLine("Reindeer build: small sprites painted");
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
    private bool OkSpot(float x, float rowY, int h)
    {
        if (x < S.Width * 0.04f || x > S.Width * 0.96f) return false;
        return S.CanStand(x, rowY, 0.45f * h) && !NearDoor(x);
    }
    /// <summary>Within 0.03 w of the cabin door's middle: nobody may REST in front of it.</summary>
    // The whole front of the cabin and its woodpile, not only the door: the first herd stood three abreast across the windows for a minute.
    private float DoorLo => S.CabinFoot.X - S.CabinWall.Width * 0.95f;
    private float DoorHi => S.CabinFoot.X + S.CabinWall.Width * 0.70f;
    private bool NearDoor(float x) => x > DoorLo && x < DoorHi;
    private float Stride(Rein a) => a.TrotMode ? TrotStride(a) : 2 * ReindeerArt.WalkReach * a.Set.H / ReindeerArt.WalkDuty;
    private float TrotStride(Rein a) => 2 * ReindeerArt.TrotReach * a.Set.H / ReindeerArt.TrotDuty;
    private float Lo(Rein a) => S.Width * 0.04f + 2f * a.Set.H;
    private float Hi(Rein a) => S.Width * 0.96f - 2f * a.Set.H;

    // ================================================================ living

    public override void Update(float dt)
    {
        if (_log == null) { UpdateInner(dt); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateInner(dt);
        _tUpdate += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
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
        bool santaOn = S.SantaOnScreen;
        if (santaOn && !_santaWas) { _passAdultSlots = Rng.NextDouble() < 0.8 ? 1 : 0; _passCalfOk = Rng.NextDouble() < 0.5; }   // a new pass: who may look up
        _santaWas = santaOn;
        if (_force != null && !_forced && _time >= 6.0) { _forced = true; ApplyForce(); }
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

    private void ApplyForce()
    {
        foreach (Rein a in _r)
        {
            switch (_force)
            {
                case "paw": if (a.Doing == Doing.Graze) { a.GPhase = 0; a.GT = 0.05f; } break;
                case "lie": a.LieAfter = 0.1f + 1.5f * (float)Rng.NextDouble(); break;
                case "shake": a.Doing = Doing.Shake; a.Timer = 0; break;
                case "alert": a.Doing = Doing.Alert; a.Timer = 4f; break;
                case "trot": a.Pending = Reaction.Flee; a.PendingCause = new Notice(Sense.Threat, new PointF(a.X - a.Dir * 300, FootY(a)), 500, 1, null, _time); break;
                case "gambol": case "fly": if (a.Kind == ReindeerKind.Calf) { _nextGambol = 0; a.Fly = _force == "fly"; a.ForceGambol = true; } break;
                case "turn": if (a.Gait == Gait.Stand) { a.Doing = Doing.Look; a.Timer = 3f; a.LookX = float.NaN; BeginTurn(a, -a.Dir, false); } break;
                case "watch": if (a.Kind == ReindeerKind.Calf || a.Curious > 0.3f) StartWatch(a); break;
            }
        }
        if (_force == "walk") { Rein L = _r[_leader]; L.Doing = Doing.Walk; L.Timer = 0; }
    }

    private void Step(int i, float dt)
    {
        Rein a = _r[i];
        ReindeerSet set = a.Set;
        float H = set.H;
        a.Timer -= dt; a.Clock += dt; a.Senses.Tick(dt); a.PostT -= dt; a.RestT -= dt; a.TurnCool -= dt; a.WatchRest -= dt; a.AlertT -= dt; a.GStay -= dt;
        if (a.LieAfter > 0) a.LieAfter -= dt;
        UpdateFx(a, dt);
        if (a.HasMark) { a.MarkA = a.Gait == Gait.Pawing || a.GPhase >= 1 && a.Doing == Doing.Graze ? MathF.Min(1f, a.MarkA + dt * 1.5f) : MathF.Max(0f, a.MarkA - dt / 18f); if (a.MarkA <= 0) a.HasMark = false; }

        // ---- the neck: a damped spring toward where the doing wants it ----
        float want = NeckWant(a);
        a.NeckV += ((want - a.NeckF) * NeckSpring - a.NeckV * NeckDamp) * dt;
        a.NeckF = Math.Clamp(a.NeckF + a.NeckV * dt, 0f, 1f);
        // Bites with the nose in the hole: bursts of a few, each a quick tug up, a hold and an ease down, then chewing.
        if (a.BiteT >= 0) { a.BiteT += dt; if (a.BiteT > 0.30f) { a.BiteT = -1; a.ChewT = 0; } }
        if (a.ChewT >= 0) { a.ChewT += dt; if (a.ChewT > 1.5f) a.ChewT = -1; }
        if (a.Doing == Doing.Graze && a.GPhase == 2 && a.NeckF > 0.93f && a.BiteT < 0)
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
        if (a.EarT >= 0) { a.EarT += dt; if (a.EarT > 0.16f) a.EarT = -1; }
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
        Reaction r = a.Senses.Check(Board, new PointF(a.X, FootY(a)), a, out Notice cause);
        if (r != Reaction.None)
        {
            if (a.Doing == Doing.Gambol && a.GPh < 90)
            {
                if (cause.Kind == Sense.Threat || (cause.Kind == Sense.Loud && r != Reaction.Look) || r is Reaction.Freeze or Reaction.Flee) AbortGambol(a, $"{cause.Kind} {r}");
                else r = Reaction.None;                              // a glance, a far-off bang: no reason to stop playing
            }
        }
        if (r != Reaction.None)
        {
            bool free = a.Gait is Gait.Stand or Gait.Walking or Gait.Lying or Gait.Rising or Gait.Settling && a.Doing != Doing.Gambol;
            if (a.Gait == Gait.Pawing) a.Scrapes = Math.Min(a.Scrapes, 1);   // finish the scrape in hand, then the reaction
            if (free && a.Gait is Gait.Stand or Gait.Walking or Gait.Lying) React(a, i, r, cause);
            else if (a.Doing != Doing.Gambol && (a.Pending == Reaction.None || Rank(r) > Rank(a.Pending))) { a.Pending = r; a.PendingCause = cause; }
        }

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
        _log?.WriteLine($"{_time,8:F3} Reindeer{i} x {a.X:F1} y {FootY(a):F1} v {a.Pace.Speed:F1} {a.Doing}/{a.Gait} neck {a.NeckF:F2}");
    }

    private static int Rank(Reaction r) => r switch { Reaction.Flee => 4, Reaction.Freeze => 3, Reaction.Approach => 2, Reaction.Look => 1, _ => 0 };

    private float NeckWant(Rein a)
    {
        if (a.Gait is Gait.Down or Gait.Lying or Gait.Up) return a.AlertT > 0 ? 0.08f : 0.25f;
        if (a.Gait == Gait.Shaking) return 0.45f;
        return a.Doing switch
        {
            Doing.Graze => a.GPhase switch { 0 => 0.42f, 1 => 0.62f, 2 => 1.0f, _ => 0.28f },
            Doing.Look => 0.22f,
            Doing.Alert => 0.08f,
            Doing.Watch => a.WatchF,
            Doing.Walk => 0.34f,
            Doing.Trot => 0.24f,
            Doing.Gambol => a.Gait == Gait.Leaping ? 0.10f : 0.22f,
            Doing.Shake => 0.45f,
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
                if (a.GaitT >= a.GaitFor)
                {
                    if (a.Doing is Doing.Walk or Doing.Trot) { a.Gait = Gait.Walking; a.Pace.Walked = 0; a.Pace.Want = a.WalkWant; }
                    else { a.Gait = Gait.Settling; a.GaitT = 0; a.GaitFor = a.TrotMode ? 0.2f : SettleFor; }   // the walk was cancelled while it rose: settle again, the pages are continuous
                }
                break;
            case Gait.Walking:
            {
                float move = a.Pace.Tick(dt);
                a.X += a.Dir * move;
                WalkChecks(a, H);
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
            case Gait.Turning:
            {
                float was = a.TurnT;
                a.TurnT += dt;
                if (was < a.TurnFor / 2 && a.TurnT >= a.TurnFor / 2) a.Dir = a.TurnTo;
                if (a.TurnT >= a.TurnFor)
                {
                    if (a.Doing is Doing.Walk or Doing.Trot) { a.Gait = Gait.Rising; a.GaitT = 0; a.GaitFor = a.Doing == Doing.Trot ? 0.2f : SettleFor; }
                    else a.Gait = Gait.Stand;
                }
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
                    if (a.Doing == Doing.Lie) { a.Doing = a.AfterUpShake ? Doing.Shake : Doing.Look; a.Timer = 1.2f; a.GPhase = 3; a.GT = 0; }
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
                if (a.ShakeT >= ShakeTime) { a.Gait = Gait.Stand; a.Snow = 0; a.Doing = Doing.Look; a.Timer = 1.2f; }
                break;
            }
        }
    }

    /// <summary>What a walking animal checks every frame: is the road ahead standable, is its errand done, did its time run out.</summary>
    private void WalkChecks(Rein a, float H)
    {
        if (a.Doing is not (Doing.Walk or Doing.Trot)) { StopWalk(a); return; }
        float look = (a.Doing == Doing.Trot ? 2.8f : 1.6f) * H;
        float ax = a.X + a.Dir * look;
        if (ax < Lo(a) || ax > Hi(a) || !S.CanStand(ax, FootYAt(a, ax), 0.3f * H))
        {
            int back = -a.Dir;
            StopWalk(a);
            a.TurnWhenSettled = back;                                 // at an edge or a prop: stop, settle, turn, and the next roll may walk on
            _log?.WriteLine($"{_time,8:F2} Reindeer blocked at {a.X:F0}");
            return;
        }
        if (a.Kind != ReindeerKind.Calf && !a.Following)
        {
            // Never walk into another adult: stop about two body lengths short of one that is ahead on a close row.
            foreach (Rein o in _r)
            {
                if (o == a || o.Kind == ReindeerKind.Calf && a == _r[_leader] || MathF.Abs(o.RowY - a.RowY) > 1.2f * H) continue;
                float d = (o.X - a.X) * a.Dir;
                if (d > 0 && d < 3.9f * H) { StopWalk(a); return; }
            }
        }
        if (a.Following)
        {
            Rein L = _r[_leader];
            float gapX = Herd.FollowX(L.X, L.Dir, GapFor(a));
            float remaining = a.Dir * (gapX - a.X);
            bool leaderMoving = L.Gait is Gait.Walking or Gait.Rising or Gait.Turning or Gait.Stopping;
            a.Pace.Want = remaining > 3.5f * H ? _walkV * 1.3f : _walkV;
            if (remaining <= (leaderMoving ? 0.3f : 0.9f) * H || a.Timer <= 0 && !leaderMoving) { a.Following = false; StopWalk(a); }
            return;
        }
        if (a.Doing == Doing.Trot)
        {
            // A running herd keeps its places: an animal with another just ahead of it on the way eases off, so they never pile into one body.
            bool crowded = false;
            foreach (Rein o in _r) { float d = (o.X - a.X) * a.Dir; if (o != a && d > 0 && d < 2.6f * H) crowded = true; }
            a.Pace.Want = crowded ? _trotV * 0.8f : _trotV;
        }
        if (a.HasGoal && a.Dir * (a.GoalX - a.X) <= Stride(a) * 0.9f) { StopWalk(a); return; }
        if (a.Timer <= 0) StopWalk(a);
    }

    private bool AnyPlaying() { foreach (Rein o in _r) if (o.Doing == Doing.Gambol) return true; return false; }

    private float GapFor(Rein a)
    {
        float href = _r[_leader].Set.H;
        return a.Kind == ReindeerKind.Calf ? 1.5f * href : a.Kind == ReindeerKind.Bull ? 2.6f * href : 2.4f * href;
    }

    /// <summary>
    /// A turn is a half turn on the spot drawn as real views (ReindeerArt.PaintFigure): the side, three-quarters, head-on or tail-on,
    /// three-quarters, the other side. About 1.2 to 1.6 s for an adult, 0.95 for the calf, 0.75 when it bolts. The page follows an eased
    /// angle (slow at the ends, quick in the middle) and the animal faces the new way from the middle on (a.Dir flips at half way: the
    /// drawing does not use it, it uses TurnFrom). Toward us three turns in five, away from us (the rump and the short tail) two in five.
    /// </summary>
    private void BeginTurn(Rein a, int dir, bool fast)
    {
        a.Gait = Gait.Turning; a.TurnFrom = a.Dir; a.TurnTo = dir; a.TurnT = 0; a.TurnCool = 2.5f;
        a.TurnFor = fast ? 0.75f : a.Kind == ReindeerKind.Calf ? 0.95f : 1.15f + 0.45f * (float)Rng.NextDouble();
        a.TurnSide = Rng.NextDouble() < 0.6 ? 1 : -1;
        a.TurnLevel = ReindeerArt.TurnLevelOf(a.NeckF);
    }

    /// <summary>Starts a walk or a trot; if the animal faces the other way it turns first (the turn pages, then the walk rises after them).</summary>
    private void StartWalk(Rein a, int dir, bool trot, float goalX = float.NaN)
    {
        a.Doing = trot ? Doing.Trot : Doing.Walk;
        a.TrotMode = trot;
        a.WalkWant = trot ? _trotV : _walkV;
        a.HasGoal = !float.IsNaN(goalX); a.GoalX = goalX; a.Following = false;
        a.GPhase = 0;
        if (dir != a.Dir) { BeginTurn(a, dir, trot); return; }
        a.Gait = Gait.Rising; a.GaitT = 0; a.GaitFor = trot ? 0.2f : SettleFor;
    }

    /// <summary>Begins a stop: the brake runs the walk down so it ends exactly on the page where all four feet are planted.</summary>
    private void StopWalk(Rein a)
    {
        bool wasTrot = a.Doing == Doing.Trot;
        if (a.Doing is Doing.Walk or Doing.Trot)
        {
            if (wasTrot) { a.Doing = Doing.Look; a.Timer = 3f; }          // after a flight: a look back at what it fled
            else { a.Doing = Doing.Graze; a.GPhase = 0; a.GT = 0.3f + 0.5f * (float)Rng.NextDouble(); a.Timer = 999f; }
        }
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
        if (a.Pace.Speed < need) a.Pace.Resume(need);
        a.Gait = Gait.Stopping;
        a.Pace.Brake(dist);
    }

    // ================================================================ the mind

    private void Think(Rein a, int i, float dt)
    {
        float H = a.Set.H, u = S.U;
        if (a.Doing == Doing.Gambol) { TickGambol(a, i, dt); return; }
        Rein L = _r[_leader];

        // A reaction that had to wait for a scrape or a turn to finish.
        if (a.Gait == Gait.Stand && a.Pending != Reaction.None)
        {
            Reaction pr = a.Pending; a.Pending = Reaction.None;
            React(a, i, pr, a.PendingCause);
            return;
        }
        // Lying: the herd lies together; the timer ends it.
        if (a.Gait is Gait.Down or Gait.Lying or Gait.Up)
        {
            if (a.Gait == Gait.Lying && a.Timer <= 0) BeginUp(a, false);
            return;
        }
        if (a.Gait == Gait.Pawing || a.Gait == Gait.Stand || a.Gait == Gait.Walking)
        {
            // The herd lies down together: when one rolls "lie" the others get a lag.
            if (a.Gait == Gait.Stand && a.LieAfter <= 0 && a.LieAfter > -0.5f && a.Doing != Doing.Alert) { a.LieAfter = -1; if (!NearDoor(a.X)) { BeginDown(a, i); return; } }
        }
        if (a.Doing == Doing.Graze && (a.Gait is Gait.Stand or Gait.Pawing)) ThinkGraze(a, dt);

        if (a.Gait != Gait.Stand) return;

        // The door rule: nobody rests in front of the cabin door; walk on past it.
        if (NearDoor(a.X) && a.Doing is Doing.Graze or Doing.Look or Doing.Watch or Doing.Alert)
        {
            // Out by the nearer end of the cabin's front (the whole front, DoorLo to DoorHi), a body length and a bit past it; the other end when
            // the nearer one is off the lane. (The first pass aimed a door-width from the door's middle: a goal still INSIDE the zone, reached
            // at once, so a cow stood in front of the cabin for ever, flickering between Walk and Graze.)
            int dir = a.X - DoorLo < DoorHi - a.X ? -1 : 1;
            float goal = dir > 0 ? DoorHi + 2.2f * H : DoorLo - 2.2f * H;
            if (goal < Lo(a) || goal > Hi(a)) { dir = -dir; goal = dir > 0 ? DoorHi + 2.2f * H : DoorLo - 2.2f * H; }
            StartWalk(a, dir, false, goal);
            a.Timer = 999f;
            return;
        }

        // The calf keeps near its mother (the leader): it follows her loosely (Herd.cs) and sets off after a short lag once it is wrong.
        // The adults do NOT follow anybody: they roam the whole lane (PickRoam) and keep their distance (the room rule below).
        if (a.Doing is Doing.Look or Doing.Graze && !(a.Doing == Doing.Graze && a.GPhase is 1 or 2) && _force != "graze")
        {
            bool leaderMoving = L.Gait is Gait.Walking or Gait.Rising or Gait.Turning;
            if (a.Kind == ReindeerKind.Calf && _r.Length > 1 && a != L && L.Gait is not (Gait.Down or Gait.Lying or Gait.Up) && L.Doing != Doing.Gambol)
            {
                float slack = leaderMoving ? 1.1f * H : 2.4f * H;
                float err = Herd.FollowError(a.X, L.X, L.Dir, GapFor(a), slack);
                if (err != 0f)
                {
                    if (a.FollowLag < 0) a.FollowLag = 0.3f + 1.6f * (float)Rng.NextDouble();
                    else if ((a.FollowLag -= dt) <= 0)
                    {
                        a.FollowLag = -1;
                        StartWalk(a, err > 0 ? 1 : -1, false);
                        a.Following = true; a.Timer = 12f;
                        return;
                    }
                }
                else a.FollowLag = -1;
            }
            // Room: standing too close to a neighbour, the one that is not busy steps off (Herd.Spacing). Adults want about two and a half
            // body lengths (4.6 heights) between them; the calf only keeps off its mother's toes (1.4 heights); an adult ignores the calf.
            if (a.Doing == Doing.Look && !AnyPlaying() || a.Doing == Doing.Graze && a.GPhase == 0 && !AnyPlaying())
            {
                float push = 0;
                for (int k = 0; k < _r.Length; k++)
                {
                    if (k == i) continue;
                    if (a == L && _r[k].Kind == ReindeerKind.Calf) continue;                  // a mother does not mind her calf
                    float d = a.X - _r[k].X, room = (a.Kind == ReindeerKind.Calf ? 1.4f : _r[k].Kind == ReindeerKind.Calf ? 4.0f : 4.6f) * H;
                    if (MathF.Abs(d) < room) push += d >= 0 ? room - d : -(room + d);
                }
                if (push != 0f && a.Clock > 1.5f)
                {
                    int dir = push > 0 ? 1 : -1;
                    float gx = a.Kind == ReindeerKind.Calf ? a.X + dir * (MathF.Abs(push) + 0.8f * H) : PickRoam(a, i);
                    if (float.IsNaN(gx)) gx = a.X + dir * (MathF.Abs(push) + 0.8f * H);
                    StartWalk(a, gx >= a.X ? 1 : -1, false, gx);
                    a.Timer = 999f;
                    return;
                }
            }
        }

        // The urge to play: a settled calf with a quiet herd has about a one in fourteen chance each second (it follows its mother and seldom rolls the dice).
        if (a.Kind == ReindeerKind.Calf && _force is null or "gambol" or "fly" && a.Doing is Doing.Graze or Doing.Look && (a.Doing == Doing.Look || a.GPhase == 0) && _time >= _nextGambol && Rng.NextDouble() < dt / 14.0 && CanGambol(a)) { StartGambol(a); return; }
        if (a.ForceGambol && a.Doing is Doing.Graze or Doing.Look && CanGambol(a)) { a.ForceGambol = false; StartGambol(a); return; }
        if (a.Doing == Doing.Graze) return;                 // its own phases decide when it is done (ThinkGraze sets Timer 0)
        if (a.Doing == Doing.Watch) { ThinkWatch(a, i, dt); return; }
        if (a.Doing == Doing.Look && !float.IsNaN(a.LookX))
        {
            int toward = a.LookX >= a.X ? 1 : -1;
            if (toward != a.Dir && a.TurnCool <= 0 && MathF.Abs(a.LookX - a.X) > 0.12f * u) BeginTurn(a, toward, false);
            if (a.Gait == Gait.Turning) return;
        }
        if (a.Doing == Doing.Shake)
        {
            a.Gait = Gait.Shaking; a.ShakeT = 0;
            return;
        }
        if (a.Timer > 0) return;
        a.LookX = float.NaN;
        Decide(a, i);
    }

    /// <summary>The dice: what next, when a doing has ended. Walking is the leader's (or a lone animal's) to start; the followers come after it.</summary>
    private void Decide(Rein a, int i)
    {
        float u = S.U;
        bool leads = i == _leader || _r.Length == 1;
        bool justGrazed = a.JustGrazed; a.JustGrazed = false;
        bool calf = a.Kind == ReindeerKind.Calf;
        bool playing = false;
        foreach (Rein o in _r) if (o.Doing == Doing.Gambol) playing = true;
        float lieW = _force is "graze" or "gambol" or "fly" or "walk" ? 0f : 0.05f;
        float[] w = [
            justGrazed ? 0.10f : 0.46f,                                              // 0 graze
            0.14f + 0.18f * a.Timid,                                                 // 1 look about
            !calf ? (justGrazed ? 0.85f : 0.35f) : 0f,                               // 2 roam (an adult's walk to a quiet stretch of the lane)
            a.Kind != ReindeerKind.Calf || i == _leader ? lieW : 0f,                 // 3 lie down (a calf lies when her mother does)
            0.03f,                                                                   // 4 shake
            calf && CanGambol(a) && _time >= _nextGambol ? 0.30f : 0f,               // 5 gambol
            calf && justGrazed ? 0.35f : 0f,                                         // 6 a short step to a fresh patch (the calf)
        ];
        if (playing) { w[2] = 0; w[6] = 0; w[3] = 0; }                 // nobody walks into the little one's play
        if (_force == "graze") { w[1] = 0; w[2] = 0; w[3] = 0; w[4] = 0; w[5] = 0; w[6] = 0; }
        if (_force == "walk" && leads && !calf) { w[0] = 0; w[1] = 0; w[3] = 0; w[4] = 0; w[2] = 1; }
        int face = Dice.Pick(Rng, w);
        switch (face)
        {
            case 0: StartGraze(a); break;
            case 1: a.Doing = Doing.Look; a.Timer = Organic.Jitter(Rng, 2.6f); a.EarT = 0; break;
            case 2:
            {
                // An adult's roam: to the quietest stretch of the lane that the herd's slow drift favours (PickRoam), however far that is.
                float gx = PickRoam(a, i);
                if (float.IsNaN(gx)) { StartGraze(a); break; }
                StartWalk(a, gx >= a.X ? 1 : -1, false, gx);
                a.Timer = 999f;
                break;
            }
            case 3:
                if (!NearDoor(a.X)) { BeginDown(a, i); AlarmLie(i); }
                else StartGraze(a);
                break;
            case 4: a.Doing = Doing.Shake; break;
            case 5: StartGambol(a); break;
            case 6:
            {
                int dir = Rng.Next(3) == 0 ? -a.Dir : a.Dir;
                StartWalk(a, dir, false, a.X + dir * (1.2f + 1.6f * (float)Rng.NextDouble()) * a.Set.H);
                a.Timer = 20f;
                break;
            }
            default: StartGraze(a); break;
        }
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} roll {a.Doing} (face {face}) x {a.X:F0}");
    }

    /// <summary>
    /// Where an adult walks to next. Fourteen candidate spots across the whole lane are scored: nobody (and nobody's walking goal) within
    /// four body lengths scores best, nearness to this animal's own place in the herd's slow drift (a sweep from one end of the lane to the
    /// other and back every DriftPeriod seconds, each animal with its own bias so the group stays loose) adds a little, and the straight
    /// way there must be ground feet may go. NaN: nothing worth walking to.
    /// </summary>
    private float PickRoam(Rein a, int i)
    {
        float H = a.Set.H, bl = 1.9f * H, lo = Lo(a), hi = Hi(a);
        float drift = 0.5f + 0.5f * MathF.Sin((float)(_time * Math.Tau / DriftPeriod + _driftPhase));
        float tgt = lo + (hi - lo) * Math.Clamp(drift + a.RoamBias, 0f, 1f);
        float best = float.NaN, bs = -1e9f;
        for (int n = 0; n < 14; n++)
        {
            float x = lo + (hi - lo) * (float)Rng.NextDouble();
            if (MathF.Abs(x - a.X) < 2.5f * bl || !OkSpot(x, a.RowY, (int)H) || !PathClear(a, x)) continue;
            float near = 1e9f;
            foreach (Rein o in _r)
            {
                if (o == a || o.Kind == ReindeerKind.Calf && a == _r[_leader]) continue;
                near = MathF.Min(near, MathF.Abs(o.X - x));
                if (o.HasGoal) near = MathF.Min(near, MathF.Abs(o.GoalX - x));
            }
            float score = 2f * MathF.Min(near / (4f * bl), 1f) - 1.5f * MathF.Abs(x - tgt) / (hi - lo) + 0.2f * (float)Rng.NextDouble();
            if (score > bs) { bs = score; best = x; }
        }
        return best;
    }

    /// <summary>Is every step of the straight way from the animal to x ground its feet may go (a prop in the lane would stop it halfway)?</summary>
    private bool PathClear(Rein a, float x)
    {
        float from = MathF.Min(a.X, x), to = MathF.Max(a.X, x), step = 0.6f * a.Set.H;
        for (float px = from; px <= to; px += step) if (!S.CanStand(px, a.RowY, 0.3f * a.Set.H)) return false;
        // An adult never walks THROUGH another: nobody ahead on the way (or within 4.4 heights past its end) on a row close to its own.
        // The calf is not an obstacle (it follows its mother and may be anywhere near her).
        int dir = x >= a.X ? 1 : -1;
        float travel = MathF.Abs(x - a.X), H = a.Set.H;
        foreach (Rein o in _r)
        {
            if (o == a || a.Kind == ReindeerKind.Calf || o.Kind == ReindeerKind.Calf && a == _r[_leader] || MathF.Abs(o.RowY - a.RowY) > 1.2f * H) continue;
            float d = (o.X - a.X) * dir;
            if (d > 0 && d < travel + 4.4f * H) return false;
        }
        return true;
    }

    private void StartGraze(Rein a)
    {
        a.Doing = Doing.Graze; a.GPhase = 0; a.GT = 0.5f + 0.9f * (float)Rng.NextDouble(); a.Timer = 999f;
    }

    /// <summary>
    /// The reindeer's own way of grazing: scan the snow, PAW it (a foreleg lifted, reached forward, set down and raked back,
    /// three or four times, the shoulder dipping at each strike and snow flying behind the hoof), then nose down into the hole
    /// and eat (bites in bursts, chewing between), then lift the head and chew while looking about.
    /// </summary>
    private void ThinkGraze(Rein a, float dt)
    {
        float H = a.Set.H;
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
                float was = a.PawT;
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
                    if (a.Scrapes <= 0) { a.Gait = Gait.Stand; a.GPhase = 2; a.EatT = Organic.Jitter(Rng, 4.6f); a.BitesLeft = 0; }
                    else a.PawFor = PawTime * (0.9f + 0.25f * (float)Rng.NextDouble());
                }
                break;
            }
            case 2:
                a.EatT -= dt;
                if (a.EatT <= 0 && a.BiteT < 0) { a.GPhase = 3; a.GT = 1.0f + 1.6f * (float)Rng.NextDouble(); a.ChewT = 0; }
                break;
            case 3:
                a.GT -= dt;
                if (a.GT <= 0) { a.JustGrazed = true; a.Timer = 0; a.GPhase = 0; a.Doing = Doing.Look; a.Timer = 0f; }
                break;
        }
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }

    // ================================================================ reactions

    /// <summary>
    /// What a reaction is for a reindeer. A glance at Santa is its own thing (the head follows him across the sky). A look at a
    /// friend's alarm is the head up, facing it. A Freeze is the same, longer. A Flee is a short springy trot away, which also
    /// tells the herd.
    /// </summary>
    private void React(Rein a, int i, Reaction r, Notice cause)
    {
        float u = S.U, H = a.Set.H;
        int toward = cause.At.X >= a.X ? 1 : -1;
        bool santa = cause.Who is SantaRig;
        if (a.Gait == Gait.Lying)
        {
            if (r == Reaction.Flee) { BeginUp(a, true); a.Pending = Reaction.Flee; a.PendingCause = cause; }
            else a.AlertT = santa ? 5f : 3f;                           // a lying animal only lifts its head
            _log?.WriteLine($"{_time,8:F2} Reindeer{i} (lying) react {r} to {cause.Kind}");
            return;
        }
        switch (r)
        {
            case Reaction.Look:
            case Reaction.Approach:
                if (santa)
                {
                    bool takes = a.WatchRest <= 0 && (a.Kind == ReindeerKind.Calf ? _passCalfOk : _passAdultSlots > 0 && a.Curious > 0.25f);
                    if (takes && a.Doing is Doing.Graze or Doing.Look or Doing.Alert && a.Gait is Gait.Stand)
                    {
                        if (a.Kind == ReindeerKind.Calf) _passCalfOk = false; else _passAdultSlots--;
                        StartWatch(a);
                    }
                }
                else if (cause.Kind == Sense.Friend)
                {
                    if (a.Doing is Doing.Graze or Doing.Look && a.Gait == Gait.Stand) { a.Doing = Doing.Alert; a.Timer = 1.8f + 1.5f * a.Timid; a.EarT = 0; a.LookX = cause.At.X; FaceToward(a, toward); }
                }
                else if (a.Doing is Doing.Graze or Doing.Look && a.Gait == Gait.Stand)
                {
                    a.Doing = Doing.Look; a.Timer = 1.2f + 1.5f * a.Curious; a.EarT = 0; a.LookX = cause.At.X; a.GPhase = 0; a.GT = 0.3f;
                    FaceToward(a, toward, 0.12f * u, cause.At.X);
                }
                break;
            case Reaction.Freeze:
                if (a.Gait == Gait.Walking) { a.Doing = Doing.Alert; a.Timer = 2.5f + 2.5f * a.Timid; a.LookX = cause.At.X; StopWalk(a); a.TurnWhenSettled = toward != a.Dir ? toward : 0; }
                else { a.Doing = Doing.Alert; a.Timer = 2.5f + 2.5f * a.Timid; a.EarT = 0; a.LookX = cause.At.X; a.GPhase = 0; FaceToward(a, toward); }
                break;
            case Reaction.Flee:
            {
                int away = -toward;
                if (a.Gait == Gait.Walking) { a.Doing = Doing.Alert; a.Timer = 1f; a.Pending = Reaction.Flee; a.PendingCause = cause; }   // brakes first, then goes the other way
                else
                {
                    StartWalk(a, away, true);
                    a.Timer = 2.2f + 1.3f * (float)Rng.NextDouble();
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
        if (toward == a.Dir || a.Gait != Gait.Stand) return;
        if (minDist > 0 && MathF.Abs(atX - a.X) < minDist) return;
        BeginTurn(a, toward, false);
    }

    private void StartWatch(Rein a)
    {
        if (a.Gait != Gait.Stand && a.Gait != Gait.Lying) return;
        if (a.Gait == Gait.Lying) { a.AlertT = 5f; return; }
        a.Doing = Doing.Watch; a.GPhase = 0;
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
        if (a.Timer <= 0) { a.Doing = Doing.Look; a.Timer = 0.8f; a.LookX = float.NaN; a.WatchRest = a.Kind == ReindeerKind.Calf ? 12f + 8f * (float)Rng.NextDouble() : 30f + 30f * (float)Rng.NextDouble(); }
    }

    // ================================================================ lying down

    private void BeginDown(Rein a, int i)
    {
        a.Doing = Doing.Lie; a.Gait = Gait.Down; a.DownT = 0; a.GPhase = 0; a.Snow = 0; a.LieT = 0;
        a.LieFor = 26f + 34f * (float)Rng.NextDouble();
        if (_r.Length > 1 && i != _leader) a.LieFor = MathF.Max(8f, _r[_leader].LieFor - 6f + 12f * (float)Rng.NextDouble());
        a.Timer = 999f;
        _log?.WriteLine($"{_time,8:F2} Reindeer{i} lies down for {a.LieFor:F0} s");
    }

    /// <summary>When one lies down the others follow in a little while (a calf beside her mother).</summary>
    private void AlarmLie(int who)
    {
        for (int k = 0; k < _r.Length; k++)
        {
            if (k == who) continue;
            Rein o = _r[k];
            if (o.Doing is Doing.Gambol or Doing.Trot || o.Gait is Gait.Down or Gait.Lying or Gait.Up) continue;
            if (o.Kind == ReindeerKind.Calf || Rng.NextDouble() < 0.85) o.LieAfter = 2.5f + 6f * (float)Rng.NextDouble();
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
            if (o.Gait is not (Gait.Stand or Gait.Pawing) || o.Doing is not (Doing.Graze or Doing.Look)) return false;
        }
        if (a.Gait != Gait.Stand) return false;
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
        if (cow != null && cow.Gait == Gait.Stand && Rng.NextDouble() < 0.7) { cow.Doing = Doing.Look; cow.Timer = 2.8f; cow.LookX = a.X; cow.EarT = 0; cow.GPhase = 0; }
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
            if (o == a) continue;
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
        if (a.Gait == Gait.Leaping) a.Gait = Gait.Stand;
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
                if (a.Gait != Gait.Stand) return;
                a.GPh = 1; a.GG = 0; a.GN = 0;
                break;
            case 1:
                a.Gait = Gait.Leaping;
                if (a.GG < SpringGround) { a.Pose = 0; a.Lift = 0; }
                else
                {
                    float q = (a.GG - SpringGround) / (SpringFor - SpringGround);
                    a.Pose = 1; a.Lift = SpringLift * H * 4 * q * (1 - q);
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
                if (a.GG < BoundGround) { a.Pose = 3; a.Lift = 0; }
                else
                {
                    float q = (a.GG - BoundGround) / (BoundFor - BoundGround);
                    a.Pose = q < 0.55f ? 2 : 1; a.Lift = BoundLift * H * 4 * q * (1 - q);
                    a.X += a.Dir * spd * H * dt;
                }
                if (a.GG >= BoundFor)
                {
                    LandGlint(a, 0.6f);
                    a.GG = 0; a.GN++;
                    if (a.GN >= a.GCount) { if (a.Fly && a.GOut) { a.GPh = 7; a.Lift = 0; } else { a.GPh = 3; a.Pose = 4; a.Lift = 0; } }
                }
                break;
            }
            case 3:
                a.Gait = Gait.Leaping; a.Pose = 4; a.Lift = 0;
                if (a.GG < SkidFor)
                {
                    a.X += a.Dir * BoundSpeed * H * (1 - a.GG / SkidFor) * dt;
                    if ((float)Rng.NextDouble() < dt * 30f) Spawn(a, 0, a.X - a.Dir * 0.3f * H, FootY(a) - 0.05f * H, -a.Dir * 40f * H / 60f * (float)Rng.NextDouble(), -50f * H / 60f * (float)Rng.NextDouble(), 0.5f, 0);   // the skid throws snow
                }
                else
                {
                    a.Gait = Gait.Stand;
                    if (a.GG >= SkidFor + 0.2f)
                    {
                        if (a.GOut) { a.GOut = false; BeginTurn(a, -a.Dir, false); a.GPh = 4; }
                        else if (--a.GBouts > 0 && BeginBout(a, 2)) { }
                        else { FinishGambol(a); }
                    }
                }
                break;
            case 4:
                if (a.Gait != Gait.Stand) return;
                a.GPh = 2; a.GG = 0; a.GN = 0; a.GCount = Math.Max(2, (int)MathF.Round((MathF.Abs(a.X - a.GHomeX) - 1.05f * H) / (1.5f * H)));
                break;
            case 5:
                if (a.Gait != Gait.Stand) return;
                // Stands and looks at her mother; the cow looks back; both go back to grazing.
                a.Doing = Doing.Look; a.Timer = 2.2f; a.GStay = 2.4f; a.EarT = 0; a.GPh = 0; a.Fly = false;
                if (cow != null && cow.Gait == Gait.Stand && cow.Doing is Doing.Graze or Doing.Look)
                {
                    cow.Doing = Doing.Look; cow.Timer = 2.0f; cow.LookX = a.X; cow.EarT = 0;
                    int toCalf = a.X >= cow.X ? 1 : -1;
                    if (cow.Dir != toCalf) BeginTurn(cow, toCalf, false);
                }
                _nextGambol = _time + 55 + 40 * Rng.NextDouble();
                _log?.WriteLine($"{_time,8:F2} Reindeer calf gambol over x {a.X:F0}");
                break;
            case 7:
            {
                // TAKE-OFF: legs tucked, the lift eases out to FlyPeak while it still runs forward.
                a.Gait = Gait.Leaping; a.Pose = 5;
                float q = Math.Clamp(a.GG / 0.38f, 0f, 1f);
                a.Lift = FlyPeak * H * (1 - (1 - q) * (1 - q));
                a.X += a.Dir * 4.6f * H * (1f - 0.3f * q) * dt;
                if (a.GG < dt * 1.5f)
                {
                    Rein? c2 = NearestAdult(a);
                    if (c2 != null && c2.Gait == Gait.Stand && Rng.NextDouble() < 0.8) { c2.Doing = Doing.Look; c2.Timer = 3f; c2.LookX = a.X; c2.EarT = 0; c2.GPhase = 0; }
                }
                if (Rng.NextDouble() < dt * 14) Sparkle(a, 0.5f);
                if (a.GG >= 0.38f) { a.GPh = 8; a.GG = 0; }
                break;
            }
            case 8:
            {
                // HANG: half a second at the top, a little bob, sparkles drifting off it.
                a.Gait = Gait.Leaping; a.Pose = 5;
                a.Lift = FlyPeak * H + 0.03f * H * MathF.Sin(a.GG * 9f);
                a.X += a.Dir * 2.0f * H * (1f - a.GG / 0.55f) * dt;
                if (Rng.NextDouble() < dt * 22) Sparkle(a, 0.7f);
                if (a.GG >= 0.55f) { a.GPh = 9; a.GG = 0; }
                break;
            }
            case 9:
            {
                a.Gait = Gait.Leaping; a.Pose = 2;
                float q = Math.Clamp(a.GG / 0.28f, 0f, 1f);
                a.Lift = FlyPeak * H * (1 - q * q);
                a.X += a.Dir * 1.0f * H * dt;
                if (a.GG >= 0.28f)
                {
                    // PLOP: belly flat in the snow, a puff all round.
                    a.Lift = 0; a.Gait = Gait.Lying; a.LieT = 0; a.Snow = 0; a.GPh = 11; a.GG = 0;
                    Plop(a);
                }
                break;
            }
            case 11:
                a.Gait = Gait.Lying;
                if (a.GG >= 0.85f) { BeginUp(a, true); a.Doing = Doing.Gambol; a.GPh = 12; a.GG = 0; }
                break;
            case 12:
                if (a.Gait == Gait.Stand) FinishGambol(a);
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

    private RPage PickBody(Rein a, out int turnPage)
    {
        ReindeerSet s = a.Set;
        turnPage = -1;
        switch (a.Gait)
        {
            case Gait.Turning: return s.Stand;                       // (the turn's own pictures are chosen in DrawInner)
            case Gait.Leaping: return s.Gam[a.Pose];
            case Gait.Walking:
            case Gait.Stopping:
            {
                if (a.TrotMode)
                {
                    float c = a.Pace.Walked / TrotStride(a);
                    return s.Trot[(int)MathF.Floor((c - MathF.Floor(c)) * ReindeerArt.TrotPages) % ReindeerArt.TrotPages];
                }
                float cyc = a.Pace.Walked / Stride(a);
                return s.Walk[(int)MathF.Floor((cyc - MathF.Floor(cyc)) * ReindeerArt.WalkPages) % ReindeerArt.WalkPages];
            }
            case Gait.Settling:
            case Gait.Rising:
            {
                float k = Math.Clamp(a.GaitT / a.GaitFor, 0f, 0.999f);
                if (a.TrotMode)
                {
                    int tp = (int)(k * ReindeerArt.TrotInPages);
                    return s.TrotIn[a.Gait == Gait.Rising ? tp : ReindeerArt.TrotInPages - 1 - tp];
                }
                int page = (int)(k * ReindeerArt.SettlePages);
                if (a.Gait == Gait.Rising) page = ReindeerArt.SettlePages - 1 - page;
                return s.Settle[page];
            }
            case Gait.Pawing: return s.Paw[Math.Clamp((int)(a.PawT * ReindeerArt.PawPages), 0, ReindeerArt.PawPages - 1)];
            case Gait.Down: return s.Down[Math.Clamp((int)(a.DownT / DownTime * ReindeerArt.LiePages), 0, ReindeerArt.LiePages - 1)];
            case Gait.Up: return s.Up[Math.Clamp((int)(a.UpT / a.UpFor * ReindeerArt.LiePages), 0, ReindeerArt.LiePages - 1)];
            case Gait.Lying: return s.Lie;
            case Gait.Shaking:
            {
                // A fast oscillation that dies away: the quantised value picks the page.
                float k = a.ShakeT / ShakeTime, amp = 1f - k * k;
                float v = MathF.Sin(a.ShakeT * 2f * MathF.PI * 6.5f) * amp;
                return s.Shake[Math.Clamp((int)MathF.Round((v + 1f) / 2f * (ReindeerArt.ShakePages - 1)), 0, ReindeerArt.ShakePages - 1)];
            }
            default: return s.Stand;
        }
    }

    private void Put(FrameBuffer fb, Sprite spr, float x, float y, int ox, int oy, bool mirror, byte nearness, float opacity = 1f) =>
        spr.Draw(fb, (int)MathF.Round(x) - (mirror ? spr.Width - 1 - ox : ox), (int)MathF.Round(y) - oy, opacity, null, mirror, S.NearDepth, nearness);

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
        if (a.Gait is Gait.Stand or Gait.Lying && a.Doing != Doing.Alert && a.Doing != Doing.Watch) f += 0.035f * Organic.Drift(_time * 0.6, a.Restless);   // a standing head is never quite still
        if (a.BiteT >= 0)
        {
            float t = a.BiteT;
            float tug = t < 0.07f ? t / 0.07f : t < 0.12f ? 1f : 1f - Smooth((t - 0.12f) / 0.18f);
            f -= 0.10f * tug;
        }
        if (turning && !fig)
        {
            // Just before and just after the half turn the head eases between where it was and the pitch level the turn pages hold it at
            // (about nine hundredths of the turn each side), so the first and the last turn page show exactly the head that is on screen.
            float lf = ReindeerArt.TurnF(a.TurnLevel);
            f = tq < 0.5f ? f + (lf - f) * Smooth(tq / 0.09f) : lf + (f - lf) * Smooth((tq - 0.91f) / 0.09f);
        }
        f = Math.Clamp(f, 0f, 1f);
        float nx = a.X + (mirror ? -1 : 1) * pg.NX * H, ny = foot - pg.NY * H - lift;
        if (!fig)
        {
            int hp = Math.Clamp((int)MathF.Round(f * (ReindeerArt.HeadPages - 1)), 0, ReindeerArt.HeadPages - 1);
            int kind = turning ? 0 : a.ChewT >= 0 ? ((int)(a.ChewT * 3) % 2) : a.EarT >= 0 ? 2 : 0;
            RPage hpg = s.Head[hp, kind];
            Put(fb, hpg.S, nx, ny, hpg.OX, hpg.OY, mirror, near);
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
