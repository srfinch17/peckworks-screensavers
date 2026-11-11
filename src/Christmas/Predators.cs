using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// THE PREDATORS: a WOLF now and then, and an OWL now and then (the owner: "a wolf should sometimes appear and maul a rabbit, and
/// sometimes an owl will swoop down and grab a mouse or a rabbit"). Nature being nature, told with some dark comedy, in a storybook look.
/// They are residents (last in the list, with their own dice), but VISITORS in life: nothing is on screen, nothing is updated and nothing
/// costs anything until a visit begins.
///
/// FEYNMAN VERSION. A visit is a SCRIPT: a short list of steps (walk in, sniff, look about, pick a hare, creep, rush, ...). Each step starts
/// a DOING, and every doing is a run of drawn pages (PredatorsArt.cs: pose sheets, each page a pose; every sheet starts and ends on a
/// named shared pose, so nothing ever switches from one picture to another). The body's place glides every frame (sub-pixel stamps); a
/// walk's page follows the DISTANCE walked (Pace), so planted paws stay planted; every stop brakes onto a rest page.
///
/// THE TRANSITION TABLE (wolf). Shared poses: STAND (all four paws planted, the base of everything) and CARRY-STAND (the same with the hare in
/// the jaws, head high). Cells (from to): every one is a sheet, an exit into a named pose, or "cannot happen".
///   Off to Lope: the wolf walks in from off screen already moving (no sheet needed, nothing is on screen).
///   Lope to Stand: brake onto lope page 0, then LOPE SETTLE (12 pages: the hovering pair lowers, the head comes up).   Stand to Lope: the same sheet backwards.
///   Stand to Sniff / Look / Howl-in: their own sheets, each starting and ending on Stand page 0 (Howl: in, loop, out).
///   Stand to Turn to Stand: the turn sheet (side, three-quarter, head on, and out the other side, 21 pages each half).
///   Stand to Stalk: STALK SETTLE backwards (16 pages: shoulders drop, head goes below the back, the paws step into the crouch); Stalk to Stand: forwards.
///   Stalk to Gallop: RUSH-IN (14 pages: deeper crouch, gather, spring) ending on gallop page 0, while the pace ramps up.
///   Gallop to Stand (the miss): SKID (22 pages, paced by the distance of the slide: forelegs braced, haunches down, then up into Stand).
///   Gallop to Carry-Stand (the catch): POUNCE (44 pages: the last bound, the strike, the tumble in a cloud of snow, the scramble up with the hare).
///   Carry-Stand to Carry-Shake / Carry-Look and back: sheets on Carry-Stand page 0.   Carry-Stand to Carry-trot: CARRY SETTLE backwards; to Off: it walks off the edge.
///   Stand to Dig-in to Dig (loop) to Dig-out to Stand: the muzzle goes in the burrow's mouth, the forepaws scrape, the sneeze.
///   Interrupts: NOTHING interrupts a wolf (it is the thing the others react to); a visit is never cut short except by the prey
///   going away (the stalked hare dives or leaves: the wolf stops where it is on the next rest page and walks off).
/// THE OWL's table is above its own code (OwlStep).
///
/// WHAT IT PINS: wolf stalking, rushing and carrying: Threat at itself, reach 0.45 U, weight 1, every 0.6 s; while it walks in or out: Movement (reach
/// 0.25 U, weight 0.3, every 1.5 s); a howl: Loud (reach 0.9 U, weight 0.6) once. Owl: Threat at its SHADOW's place (reach 0.35 U, weight 0.9, every
/// 0.6 s) only once it is low (under 2 m). WHAT IT NEEDS from the prey: FoxAndHares.Hare/Seize/Restore/Stalked and LogMice.Mouse_/Seize/Restore/Stalked.
///
/// TEST VARIABLES: PECKWORKS_PREDATOR = wolf (a kill, the catch), wolfmiss, wolfhowl, wolfcross, owlmouse (a catch), owlhare, owlmiss, owlperch, owlcross,
/// many (natural outcomes, every minute or so: the whole-field runs), off. A forced visit begins at 6.5 s. PECKWORKS_SHEET=predators;file.png writes the pose sheet.
/// PECKWORKS_MINDLOG writes a line per frame of a visit and one per step, roll and reaction.
/// </summary>
internal sealed partial class Predators : Resident
{
    // ---------------------------------------------------------------- the wolf's doings and steps
    private enum WD { Off, Walk, SetIn, SetOut, Stand, Sheet, Loop, Turn, RushIn, Gallop, Skid, Pounce, Hold }
    private static readonly bool WolfReady = true;                            // see MaybeStart: false keeps the wolf home in a natural run (the test dials still force it)
    private enum WS { WalkTo, Exit, Sniff, Look, Select, Turn, StartWalk, Stalk, Howl, Dig, CShake, CLook, Idle, Rear, FaceR, FaceL }
    private enum SA { Next, Stand, WalkNow, RushGo }

    private static readonly int[] WalkBook = [PredatorsArt.WLopeB, PredatorsArt.WStalkB, PredatorsArt.WCarryB];
    private static readonly int[] SetBook = [PredatorsArt.WLopeSet, PredatorsArt.WStalkSet, PredatorsArt.WCarrySet];
    private static readonly int[] WalkN = [PredatorsArt.NLope, PredatorsArt.NStalk, PredatorsArt.NCarry];
    private static readonly int[] SetN = [PredatorsArt.NLopeSet, PredatorsArt.NStalkSet, PredatorsArt.NCarrySet];
    private static readonly float[] GaitC = [PredatorsArt.Lope.C, PredatorsArt.Stalk.C, PredatorsArt.CarryG.C];
    private static readonly float[] GaitV = [1.30f, 0.60f, 1.75f];            // metres a second
    private const float GallopC = 3.1f, GallopV = 9.0f;                       // metres a cycle, metres a second
    // How far short of its hare the wolf would like to STAND, best first. A hunt is a creep (at least 2 m of it, seen), then the rush-in
    // (2.2 m), then the gallop, then the pounce (it begins about 1.8 m short of the contact point). So a stand 7.2 m back gives a 2 m creep and
    // a 5.2 m rush; 9.5 m gives a 2 m creep and a 7.5 m rush with most of a gallop cycle in it. Nearer than 7.2 the creep gets short.
    private static readonly float[] BackTries = [9.5f, 9.0f, 8.5f, 10.0f, 8.0f, 7.5f, 7.2f, 6.8f, 6.3f, 5.8f, 5.5f];

    private sealed class Wolf
    {
        public bool On; public float X, Y; public int Dir = 1; public WD Do; public float T;
        public readonly Pace Pace = new(1f);
        public int Gait; public Book? Bk; public int Pg; public bool Mir;
        public readonly List<WS> Plan = new(16);
        public int PreHare = -1; public int PreyKind = 3; public bool Rear; public int Kind; public bool WantCatch; public int Hare = -1; public bool Carrying;
        public float GX, GY; public bool StopAtGoal, Dyn, RushAfter; public int NewDir; public int Tries;
        public int SBook, SN; public float SDur, ST, SCycle, SLoopFor; public bool SRev; public SA After;
        public float PostT, MoveT, BreathT; public float LastFrac, PathT, StuckT; public bool Braking, MissPlanned, Flag1, Flag2;
        public int Reseeks; public float PX0, PY0, PXc, PYc, PV0, PV1, PTc; public int BurrowK = -1; public PointF Hole; public float SkidFrom, SkidD; public float RushGap;
        public Cover? Cov; public PointF Stand; public float DigEmit; public string Log = "";
        public float[]? Road; public int RoadK0; public float PlanY; public bool Arrive, Held; public float HoldT, CreepFrom;          // PlanY: the row it means to stand on (the lane avoidance may detour, never forget it); Arrive: the first stop is still to be checked; Held/HoldT: the crouch held for another hare to clear the line; CreepFrom: where the creep began
    }

    // ---------------------------------------------------------------- the owl
    private enum OD { Off, Flap, Glide, Sheet, Loop, Stoop, Brake, Idle }
    private enum OS { Cruise, Land, PerchWait, Swivel, Takeoff, Hunt, Exit }
    private sealed class Owl
    {
        public bool On; public float X, GY, H, HT; public int Dir = 1; public OD Do; public float T;
        public readonly Pace Pace = new(1f);
        public Book? Bk; public int Pg; public bool Mir; public float PgF;       // PgF: a fractional page for the swivel's blend
        public int Kind;                  // 0 cross, 1 perch, 2 hunt a mouse, 3 hunt a hare
        public bool Catch; public int Prey = -1; public int Carry;              // 0 none, 1 mouse, 2 hare
        public float PX, PY, StrikeX, R0, H0, V0, Rb; public float PerchX, PerchH, HEnd; public Cover? Cov; public int PerchKind;
        public float FlapFor, GlideFor; public bool WantGlide; public int SBook, SN; public float SDur, ST; public bool SRev; public int After; public float SCycle, SLoopFor;
        public float PostT, BreathT, Hold; public int Stage, Swivels; public float YawFrom, YawTo, YawT, YawDur, Yaw;
        public float LastGate = -1f; public bool Struck, SeizeDone, Cloud;
        public float K;                   // how far through the stoop's descent it is: 0 where the stoop began, 1 where the brake begins (the stoop-in's pages follow this)
        public float BrakeD; public bool F1; public int LastPage; public string Log = "";
    }

    // ---------------------------------------------------------------- fields
    private readonly FoxAndHares _field;
    private readonly LogMice _mice;
    private readonly Squirrels _sq;
    private readonly bool _on;
    private readonly string _test;
    private readonly float _pct;
    private readonly float _m;                      // pixels in one metre: the size of every page (the wolf and the owl are each painted at ONE size)
    private volatile Book[]? _wb;
    private Book[]? _ob;
    private Sprite[]? _puff;                        // snow puffs, three sizes; [3] breath, [4] a big cloud puff, [5] a small cloud puff
    private Sprite? _blood; private Sprite[]? _shadow;
    private readonly Wolf _w = new(); private readonly Owl _o = new();
    private double _time, _nextWolf, _nextOwl, _lastWolfEnd = -999, _lastOwlEnd = -999;
    private bool _forced;
    private readonly StreamWriter? _log = MindLog.Open();
    private bool _firstUpdate; private double _buildMs;
    private long _tickU, _tickD; private int _costFrames; private double _costAt;
    private readonly float _yLo, _yHi;

    // little snow: particles in a ring (no allocation per frame)
    private struct Pt { public float X, Y, H, VX, VH, T, Life, Op, Grav; public byte Kind; public bool On; }
    private readonly Pt[] _pts = new Pt[160];
    private struct Cloud { public float X, Y, Born, R; public bool On; }
    private readonly Cloud[] _clouds = new Cloud[3];
    private struct Stain { public float X, Y; public double Born; public byte Size; }
    private readonly Stain[] _stains = new Stain[24];
    private int _stainAt; private bool _anyPts;
    private double _retryAt;

    // WHAT A HUNTER KNOWS ABOUT EACH PREY ANIMAL (from InOpen and FeetOf only: the prey's own code is none of its business)
    private struct PV { public PointF Feet; public bool Out; public float Vx, Vy, Speed; public bool Seen; }
    private PV[] _hv = Array.Empty<PV>(), _mv = Array.Empty<PV>(), _sv = Array.Empty<PV>();
    /// <summary>The hunter's view of one kind of prey: 2 mice, 3 hares, 4 squirrels.</summary>
    private PV[] PreyView(int kind) => kind == 2 ? _mv : kind == 4 ? _sv : _hv;
    private bool PreySeize(int kind, int i) => kind == 2 ? _mice.Seize(i) : kind == 4 ? _sq.Seize(i) : _field.Seize(i);
    private void PreyRestore(int kind, int i, float after) { if (kind == 2) _mice.Restore(i, after); else if (kind == 4) _sq.Restore(i, after); else _field.Restore(i, after); }
    private void PreyStalked(int kind, int i) { if (kind == 2) _mice.Stalked(i); else if (kind == 4) _sq.Stalked(i); else _field.Stalked(i); }
    /// <summary>How far (pixels) a point on the snow is from the nearest pine's foot.</summary>
    private float PineGap(PointF p) { float best = float.MaxValue; foreach (var pn in S.Pines) { float dx = pn.X - p.X, dy = 2f * (pn.Foot - p.Y); best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy)); } return best; }
    private void Track(float dt)
    {
        if (_hv.Length != _field.HareCount) _hv = new PV[_field.HareCount];
        if (_mv.Length != _mice.Count) _mv = new PV[_mice.Count];
        if (_sv.Length != _sq.Count) _sv = new PV[_sq.Count];
        for (int i = 0; i < _sv.Length; i++) TrackOne(ref _sv[i], _sq.FeetOf(i), _sq.InOpen(i), dt);
        for (int i = 0; i < _hv.Length; i++) TrackOne(ref _hv[i], _field.FeetOf(i), _field.InOpen(i), dt);
        for (int i = 0; i < _mv.Length; i++) TrackOne(ref _mv[i], _mice.FeetOf(i), _mice.InOpen(i), dt);
    }
    private static void TrackOne(ref PV v, PointF feet, bool open, float dt)
    {
        if (v.Seen && open && dt > 0f)
        {
            v.Vx = Lerp(v.Vx, (feet.X - v.Feet.X) / dt, 0.35f); v.Vy = Lerp(v.Vy, (feet.Y - v.Feet.Y) / dt, 0.35f);
        }
        else { v.Vx = 0f; v.Vy = 0f; }
        v.Speed = MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy); v.Feet = feet; v.Out = open; v.Seen = true;
    }
    private bool Still(in PV v) => v.Out && v.Speed < 0.16f * _m;
    private bool Bolting(in PV v) => v.Out && v.Speed > 1.1f * _m;

    // COVER: the rocks, shrubs, stumps and brush piles the prey hides behind (ChristmasScenery.Covers)
    private bool CoverOk(Cover c) => c.Foot.Y >= _yLo - 10f && c.Foot.Y <= _yHi + 0.14f * S.U;
    private Cover? NearestCover(PointF p, out float distM)
    {
        Cover? best = null; float bd = float.MaxValue;
        foreach (Cover c in S.Covers)
        {
            if (!CoverOk(c)) continue;
            float dx = c.Hide.X - p.X, dy = 2f * (c.Hide.Y - p.Y), d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = c; }
        }
        distM = best == null ? 99f : MathF.Sqrt(bd) / _m;
        return best;
    }
    /// <summary>Where the wolf stands to put its muzzle into a piece of cover from the left or right: the muzzle tip a little way inside the near edge, the body outside, the feet just behind the cover's foot (so the cover's own pixels, in front, hide the muzzle).</summary>
    private PointF CoverStand(Cover c, int dir) => new(c.Foot.X - dir * (0.30f * c.HalfW + 1.30f * _m), Math.Clamp(c.Hide.Y, _yLo - 6f, _yHi + 0.1f * S.U));

    public bool WolfOn => _w.On;
    public bool OwlOn => _o.On;

    public Predators(ChristmasScenery s, int percent, Random sceneRng, Noticeboard board, FoxAndHares field, LogMice mice, Squirrels squirrels) : base(s, sceneRng, board)
    {
        _field = field; _mice = mice; _sq = squirrels;
        _test = Environment.GetEnvironmentVariable("PECKWORKS_PREDATOR") ?? "natural";
        _pct = Math.Clamp(percent, 0, 300) / 100f;
        _on = _pct > 0f && _test != "off";
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        if (!string.IsNullOrEmpty(sheet) && sheet.StartsWith("predators;")) PredatorsArt.WriteSheet(sheet[10..]);
        float yMid = s.FieldPoint(0.5f * s.Width, 0.38f).Y;
        _m = MathF.Max(5f, s.Metre(yMid));
        _yLo = s.SmallBandTop - 14f; _yHi = s.FieldPoint(0.5f * s.Width, 0.58f).Y;
        bool many = _test == "many";
        double f = Math.Max(0.2, _pct);
        _nextWolf = many ? 14.0 : 90.0 + (30.0 + 150.0 * Rng.NextDouble()) / f;
        _nextOwl = many ? 9.0 : 60.0 + (20.0 + 100.0 * Rng.NextDouble()) / f;
        // THE BUILD COST NOTHING AT THE START. No visit can begin before 60 s (the owl) or 90 s (the wolf), so the resident is Ready AT ONCE and its
        // 1,200 pages are painted afterwards, on a low-priority thread of their own, while the herd and the hares and the mice are still painting
        // theirs (it begins only when they have had a head start: a second and a half). Update does nothing until the books are there (_wb is the last thing set).
        BuildInBackground(() => { });
        if (!_on) return;
        Quietly(() => { Thread.Sleep(1500); try { Build(); } catch (Exception e) { Log($"BUILD FAILED: {e}"); } });
    }

    private void Build()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float[] tier = [_m];
        var wb = PredatorsArt.WolfBooks(); var ob = PredatorsArt.OwlBooks();
        var wbooks = new Book[wb.Length]; var obooks = new Book[ob.Length];
        QuietFor(wb.Length, i => wbooks[i] = new Book(wb[i], tier));
        QuietFor(ob.Length, i => obooks[i] = new Book(ob[i], tier));
        Sprite Soft(float r, float a) => Sprite.Glow((int)MathF.Max(2, r * _m), Color.FromArgb(a > 0 ? 255 : 255, 244, 248, 255));
        _puff = [Soft(0.07f, 1), Soft(0.14f, 1), Soft(0.24f, 1), Sprite.Glow((int)MathF.Max(3, 0.20f * _m), Color.FromArgb(235, 242, 252)), Sprite.Glow((int)MathF.Max(4, 0.55f * _m), Color.FromArgb(248, 250, 255)), Sprite.Glow((int)MathF.Max(3, 0.34f * _m), Color.FromArgb(248, 250, 255))];
        int bs = (int)MathF.Max(2, 0.035f * _m);
        _blood = Sprite.Paint(bs * 2 + 2, bs * 2 + 2, g => { using var b = new SolidBrush(Color.FromArgb(214, 170, 30, 36)); g.FillEllipse(b, 1, 1, bs * 2, bs * 2); });
        // the owl's shadow: a soft ellipse, three sharpnesses (a flier high up throws a wide soft one, a low one a small dark one)
        _shadow = new Sprite[3];
        for (int k = 0; k < 3; k++)
        {
            int w = (int)(0.80f * _m), h = (int)MathF.Max(4, 0.16f * _m); int sk = k;
            _shadow[k] = Sprite.Paint(w * 2 + 4, h * 2 + 4, g =>
            {
                using var path = new System.Drawing.Drawing2D.GraphicsPath(); path.AddEllipse(2, 2, w * 2, h * 2);
                using var br = new System.Drawing.Drawing2D.PathGradientBrush(path) { CenterColor = Color.FromArgb(96, 30, 44, 100), SurroundColors = [Color.FromArgb(0, 30, 44, 100)] };
                float edge = sk == 0 ? 0.75f : sk == 1 ? 0.45f : 0.15f;          // how far in the solid part reaches: sharp edge for 0, a long soft fall for 2
                br.Blend = new System.Drawing.Drawing2D.Blend { Positions = [0f, 0.001f + (1f - edge) * 0.7f, 1f], Factors = [0f, 0.6f, 1f] };
                g.FillPath(br, path);
            });
        }
        _ob = obooks; _wb = wbooks;                 // (_wb last: Update starts as soon as it is set)
        long painted = sw.ElapsedMilliseconds;
        // WARM-UP: compile our methods now (without running them), so the first real frame costs nothing
        const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (Type ty in new[] { typeof(Predators), typeof(PredatorsArt), typeof(Book) })
            foreach (var mm in ty.GetMethods(all))
                try { if (!mm.IsAbstract && !mm.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(mm.MethodHandle); } catch { }
        _buildMs = sw.ElapsedMilliseconds;
        _log?.WriteLine($"   0.00 predators BUILD painted {painted} ms, warmed {sw.ElapsedMilliseconds - painted} ms, unit {_m:F1} px a metre, wolf pages {wbooks.Sum(b => b.Count)}, owl pages {obooks.Sum(b => b.Count)}");
    }

    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} predators {what}");

    // ================================================================ UPDATE

    public override void Update(float dt)
    {
        if (!_on || _wb == null) return;
        long t0 = _firstUpdate && _log == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
        _time += dt;
        if (_anyPts) TickParticles(dt);
        if (_w.On || _o.On || _time + 2.0 >= Math.Min(_nextWolf, _nextOwl) || (_test is not ("natural" or "many") && !_forced && _time > 4.0)) Track(dt);
        if (_w.On) UpdateWolf(dt);
        else if (_o.On) UpdateOwl(dt);
        else MaybeStart();
        if (_log != null)
        {
            _tickU += System.Diagnostics.Stopwatch.GetTimestamp() - t0; _costFrames++;
            if (_time - _costAt >= 10.0 && _costFrames > 0) { Log($"cost per frame: update {_tickU * 1e6 / System.Diagnostics.Stopwatch.Frequency / _costFrames:F1} us, draw {_tickD * 1e6 / System.Diagnostics.Stopwatch.Frequency / _costFrames:F1} us"); _tickU = 0; _tickD = 0; _costFrames = 0; _costAt = _time; }
        }
        if (!_firstUpdate) { _firstUpdate = true; Log($"first update took {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F2} ms (build {_buildMs:F0} ms)"); }
    }

    private void MaybeStart()
    {
        if (Lull) return;
        double f = Math.Max(0.2, _pct);
        bool many = _test == "many";
        if (!_forced && _test is not ("natural" or "many") && _time >= (_test.StartsWith("wolf") ? 20.0 : 12.0) && _time >= _retryAt)
        {
            _forced = true;
            switch (_test)
            {
                case "wolf": StartWolf(0, true); break;
                case "wolfmiss": StartWolf(0, false); break;
                case "wolfhowl": StartWolf(2, true); break;
                case "wolfcross": StartWolf(1, true); break;
                case "wolfsquirrel": StartWolf(3, false); break;
                case "owlmouse": StartOwl(2, true); break;
                case "owlhare": StartOwl(3, true); break;
                case "owlsquirrel": StartOwl(4, true); break;
                case "owlmiss": StartOwl(2, false); break;
                case "owlperch": StartOwl(1, true); break;
                case "owlcross": StartOwl(0, true); break;
            }
            if (_test.StartsWith("wolf") && !_w.On) { _forced = false; _retryAt = _time + 1.0; }          // no squirrel (or hare) was placed for it yet: ask again in a second
            return;
        }
        if (_test is not ("natural" or "many")) return;
        // The wolf was let out on 2026-11-11, once its catch, its howl, its crossing and its look-and-leave had each been filmed on the merged scene.
        if (WolfReady && _time >= _nextWolf && _time - _lastOwlEnd > (many ? 12.0 : 40.0))
        {
            double r = Rng.NextDouble();
            int kind = r < 0.60 ? 0 : r < 0.76 ? 3 : r < 0.88 ? 1 : 2;
            // ponytail: the squirrel hunts (wolf kind 3, owl kind 4) are OFF in a natural run. Nobody asked for them (the ask was a hare for the wolf, a
            // mouse or a hare for the owl) and on film neither worked (the wolf never found a squirrel in 180 s, the owl picked one still up its pine).
            // The code and the test dials (wolfsquirrel, owlsquirrel) stay; turn the two lines back on when those two films pass.
            if (kind == 3) kind = 0;
            if (kind == 0 && _field.HareCount < 2) kind = 1;
            if (kind == 3 && _sq.Count < 2) kind = 0;
            // ponytail: a natural wolf always means to CATCH. The planned miss (the hare reaches cover, the wolf skids) has never played on film: the hare
            // finds "nowhere to run" and freezes, and the wolf catches it anyway. The roll is still taken so the dice stay where they were. The miss code
            // and its dial (PECKWORKS_PREDATOR=wolfmiss) stay; give the roll back when a hare beside cover really bolts for it.
            bool want = Rng.NextDouble() < 2.0;
            StartWolf(kind, want);
            if (!_w.On && kind == 3) StartWolf(0, want);                  // no squirrel is out with a clear run (the pond is in the wolf's way): a hare hunt instead
            if (!_w.On) _nextWolf = _time + 8.0;                       // no squirrel was out on the snow with its pine beyond it: try again in a few seconds
        }
        else if (_time >= _nextOwl && _time - _lastWolfEnd > (many ? 12.0 : 40.0))
        {
            double r = Rng.NextDouble();
            int kind = r < 0.12 ? 0 : r < 0.24 ? 1 : r < 0.56 ? 2 : r < 0.80 ? 3 : 4;
            if (kind == 4) kind = 2;                                      // (squirrel hunts are off: see the wolf's roll above)
            StartOwl(kind, Rng.NextDouble() < 0.67);
        }
    }

    // ================================================================ PARTICLES AND STAINS

    private void Emit(byte kind, float x, float y, float h, float vx, float vh, float life, float op, float grav = 0f)
    {
        for (int i = 0; i < _pts.Length; i++)
            if (!_pts[i].On) { _anyPts = true; _pts[i] = new Pt { X = x, Y = y, H = h, VX = vx, VH = vh, T = 0, Life = life, Op = op, Kind = kind, Grav = grav, On = true }; return; }
    }
    private void SnowBurst(float x, float y, int n, float spread, float up, byte kind = 0)
    {
        for (int i = 0; i < n; i++)
        {
            float a = (float)(Rng.NextDouble() * 2 - 1);
            Emit((byte)(kind + (i % 3 == 0 ? 1 : 0)), x + a * 0.15f * _m, y, 0.04f * _m, a * spread * _m, (0.25f + 0.75f * (float)Rng.NextDouble()) * up * _m, 0.5f + 0.5f * (float)Rng.NextDouble(), 0.85f, 2.0f * _m);
        }
    }
    private void BreathAt(float x, float y, float big)
    {
        Emit(3, x, y, 0f, 0.10f * _m * _w.Dir, 0.10f * _m, 1.4f + 0.6f * big, 0.55f);
        if (big > 0.5f) Emit(3, x + 0.05f * _m * _w.Dir, y, 0.0f, 0.18f * _m * _w.Dir, 0.14f * _m, 1.8f, 0.45f);
    }
    private void TickParticles(float dt)
    {
        for (int i = 0; i < _pts.Length; i++)
        {
            ref Pt p = ref _pts[i];
            if (!p.On) continue;
            p.T += dt; p.X += p.VX * dt; p.H += p.VH * dt; p.VH -= p.Grav * dt;
            if (p.Kind == 3) p.VX *= 0.985f;
            if (p.T >= p.Life || (p.Kind != 3 && p.H < -2f)) p.On = false;
        }
        bool live = false;
        for (int i = 0; i < _pts.Length; i++) if (_pts[i].On) { live = true; break; }
        for (int i = 0; i < _clouds.Length; i++) if (_clouds[i].On) { _clouds[i].Born += dt; if (_clouds[i].Born > 1.8f) _clouds[i].On = false; else live = true; }
        _anyPts = live;
    }
    private void StartCloud(float x, float y, float r)
    {
        _anyPts = true;
        for (int i = 0; i < _clouds.Length; i++) if (!_clouds[i].On) { _clouds[i] = new Cloud { X = x, Y = y, Born = 0f, R = r, On = true }; return; }
        _clouds[0] = new Cloud { X = x, Y = y, Born = 0f, R = r, On = true };
    }
    private float CloudOp(in Cloud c) => c.Born < 0.12f ? c.Born / 0.12f : c.Born < 0.75f ? 1f : MathF.Max(0f, 1f - (c.Born - 0.75f) / 1.05f);
    private void AddStain(float x, float y) { _stains[_stainAt] = new Stain { X = x, Y = y, Born = _time, Size = (byte)Rng.Next(2) }; _stainAt = (_stainAt + 1) % _stains.Length; }

    private void Post(Sense kind, float x, float y, float reach, float weight, ref float timer, float dt, float every)
    {
        timer -= dt;
        if (timer > 0f) return;
        Board.Post(kind, new PointF(x, y), reach * S.U, weight, this); timer = every;
    }

    // ================================================================ SHARED PLACE TESTS

    private bool Free(float x, float y, float room) => S.CanStand(x, y, room);
    /// <summary>Is the straight run along row y from x0 to x1 clear of the pond's ice? (The wolf never sets a paw on the ice: it hunts only where the whole run is snow.)</summary>
    private bool RunIceFree(float x0, float x1, float y)
    {
        int n = (int)(MathF.Abs(x1 - x0) / (0.4f * _m)) + 1;
        for (int k = 0; k <= n; k++) if (S.OnPond(x0 + (x1 - x0) * k / n, y, 1.18f)) return false;
        return true;
    }
    /// <summary>Is a body of half length halfW and height bodyH standing at (x, y) partly hidden behind a NEARER prop? (A wolf may pass behind a prop, but never stop, sniff or howl half covered by one.)</summary>
    private bool Covered(float x, float y, float halfW, float bodyH)
    {
        byte me = S.Nearness(y); int W = S.Width, Hh = S.Height; byte[] nd = S.NearDepth;
        for (int iy = 0; iy < 4; iy++)
            for (int ix = -3; ix <= 3; ix++)
            {
                int px = (int)(x + ix * halfW / 3f), py = (int)(y - 1 - iy * bodyH / 3f);
                if (px < 0 || py < 0 || px >= W || py >= Hh) continue;
                if (nd[py * W + px] > me) return true;
            }
        return y < S.SnowmanFoot.Y && MathF.Abs(x - S.SnowmanFoot.X) < 0.6f * S.SnowmanHeight;
    }
    /// <summary>Is another animal standing where the wolf would put its body? A WIDE row band (about 0.7 m of snow each side): the old 17 px band let the wolf
    /// walk 19 px in front of two sitting hares, which on film is a wolf walking through their ears.</summary>
    private bool CrowdedW(float x, float y) => S.Crowded(x, y, 0.6f * _m, this, 0, 0.035f * S.U);
    // The body tested is as tall as a HOWLING wolf's raised head (1.7 m) and a little longer than the wolf: on film (2026-11-11) a wolf tested only to
    // its back stood for fifteen seconds with its nose inside the bird table's tray, and howled up through it.
    /// <summary>A place the wolf may STAND, sniff, howl or stalk from: bare snow (not a prop's foot, not the ice), no nearer prop hiding its body, nobody else there.</summary>
    private bool StandOk(float x, float y) =>
        Free(x, y, 0.5f * _m) && !Covered(x, y, 1.5f * _m, 1.7f * _m) && !S.OnPond(x, y, 1.10f) && !CrowdedW(x, y) && x > 0.04f * S.Width && x < 0.96f * S.Width;
    /// <summary>The nearest good place to stand to (x, y): along the row first in the walking direction, then back, on this row and on the planned row.</summary>
    private bool FindStand(float x, float y, int dir, out PointF at)
    {
        float hRow = _w.Hare >= 0 && _w.PreyKind == 3 ? Math.Clamp(_hv[_w.Hare].Feet.Y, _yLo, _yHi) : y;
        float[] rows = [y, _w.PlanY > 0f ? _w.PlanY : y, hRow];
        for (int k = 0; k <= 24; k++)
            // (only AHEAD: the wolf walks the way it faces; walking backwards would be a slide)
            foreach (float row in rows)
                {
                    float xx = x + dir * k * 0.4f * _m;
                    if (StandOk(xx, row) && RunIceFree(Math.Min(x, xx), Math.Max(x, xx), row)) { at = new PointF(xx, row); return true; }
                }
        at = default; return false;
    }
    /// <summary>Is the whole run along row y from x0 to x1 free (snow, no prop's foot, no ice, nobody standing there)? The planned row is taken up again only when ALL of the way to the goal is clear (a look 3 m ahead walked the wolf to the pond's rim and it was boxed in for 8 s).</summary>
    private bool RowClear(float x0, float x1, float y)
    {
        float a = Math.Min(x0, x1), b = Math.Max(x0, x1);
        for (float x = a; x <= b + 1f; x += 0.4f * _m)
            if (!Free(x, y, 0.5f * _m) || S.OnPond(x, y, 1.18f) || CrowdedW(x, y)) return false;
        return true;
    }
    /// <summary>Is a hare that is out on the snow within 4.4 m of x along the row? (A hunter does not choose to stand on top of its prey.)</summary>
    private bool HareNear(float x)
    {
        for (int j = 0; j < _hv.Length; j++) if (_hv[j].Out && MathF.Abs(_hv[j].Feet.X - x) < 4.4f * _m) return true;
        return false;
    }
    /// <summary>Row y is a road from x0 to x1: bare snow all the way (a prop's foot or the ice stops it), nobody standing on it. Passing BEHIND a nearer prop is fine on the road (not at the stand).</summary>
    private bool RowFree(float x0, float x1, float y)
    {
        float a = Math.Min(x0, x1), b = Math.Max(x0, x1);
        for (float x = a; x <= b + 1f; x += 0.4f * _m)
            if (x > 4f && x < S.Width - 4f && (!Free(x, y, 0.5f * _m) || S.OnPond(x, y, 1.18f) || CrowdedW(x, y))) return false;
        return true;
    }
    // ---- THE ROAD: where the wolf's feet go between two places (a small shortest-path search) ----
    //
    // FEYNMAN VERSION. The wolf's ground is a crowded strip: the pond fills every row of its left half, and on the right a tree, a bird table, a snowman,
    // shrubs and rock huddles stand in the way. A wolf that only looks three metres ahead and swerves wanders into a dead end (it was boxed in, or it
    // walked over the ice). So before it walks, it PLANS: the strip is cut into columns 0.4 m wide and rows 10 px apart, each cell is "bare snow" or not,
    // and a search finds the cheapest way from the start column to the end column moving one column at a time, up or down by at most one row a column
    // (a slope of 0.32, just inside what the walk can follow without sliding sideways). The answer is one row per column; the walk reads it like a
    // railway line. Rows are free to pass BEHIND a nearer prop (the depth picture hides the wolf) but never through its foot.
    private const float RoadRowStep = 10f;
    private float RoadCell => 0.4f * _m;
    /// <summary>Plans the road from x0 to x1 (either direction; off screen ends are fine). It starts on the free row nearest yFrom, and ends on the row nearest yTo that is a good place to stand (if needStand) or just bare snow. Returns false if there is no road.</summary>
    private bool PlanRoad(float x0, float x1, float yFrom, float yTo, bool needStand, out float[]? rows, out int k0)
    {
        float cell = RoadCell, yMax = MathF.Min(S.Height - 50f, _yHi + 0.06f * S.U);
        int nr = (int)((yMax - _yLo) / RoadRowStep) + 1;
        int ka = (int)MathF.Round(x0 / cell), kb = (int)MathF.Round(x1 / cell), step = kb >= ka ? 1 : -1, n = Math.Abs(kb - ka) + 1;
        k0 = Math.Min(ka, kb); rows = null;
        float[,] cost = new float[n, nr]; sbyte[,] from = new sbyte[n, nr];
        for (int i = 0; i < n; i++) for (int r = 0; r < nr; r++) cost[i, r] = float.MaxValue;
        bool Pass(int k, int r)
        {
            float x = k * cell, y = _yLo + r * RoadRowStep;
            if (x < 4f || x > S.Width - 4f) return true;                              // off screen: nothing there
            return Free(x, y, 0.5f * _m) && !S.OnPond(x, y, 1.18f) && !CrowdedW(x, y);
        }
        for (int r = 0; r < nr; r++) if (Pass(ka, r)) cost[0, r] = 0.01f * MathF.Abs(_yLo + r * RoadRowStep - yFrom);
        for (int i = 1; i < n; i++)
            for (int r = 0; r < nr; r++)
            {
                if (!Pass(ka + i * step, r)) continue;
                for (int d = -1; d <= 1; d++)
                {
                    int rp = r + d; if (rp < 0 || rp >= nr || cost[i - 1, rp] == float.MaxValue) continue;
                    float c = cost[i - 1, rp] + 1f + (d == 0 ? 0f : 0.05f);
                    if (c < cost[i, r]) { cost[i, r] = c; from[i, r] = (sbyte)d; }
                }
            }
        int best = -1; float bc = float.MaxValue;
        for (int r = 0; r < nr; r++)
        {
            if (cost[n - 1, r] == float.MaxValue) continue;
            float y = _yLo + r * RoadRowStep;
            if (needStand && !StandOk(x1, y)) continue;
            float c = cost[n - 1, r] + 0.02f * MathF.Abs(y - yTo);
            if (c < bc) { bc = c; best = r; }
        }
        if (best < 0) return false;
        rows = new float[n];
        int rr = best;
        for (int i = n - 1; i >= 0; i--) { rows[i] = _yLo + rr * RoadRowStep; if (i > 0) rr += from[i, rr]; }
        // rows[] is in walking order (index 0 = the start column); make it indexable by column: the caller gets k0 and a direction-free table by column
        if (step < 0) Array.Reverse(rows);
        return true;
    }
    /// <summary>The road's row at x (linear between columns), or -1 if the wolf has no road.</summary>
    private float RoadY(float x)
    {
        var w = _w; if (w.Road == null) return -1f;
        float f = x / RoadCell - w.RoadK0;
        f = Math.Clamp(f, 0f, w.Road.Length - 1f);
        int i = (int)f; float t = f - i;
        return i + 1 < w.Road.Length ? w.Road[i] + (w.Road[i + 1] - w.Road[i]) * t : w.Road[i];
    }
    /// <summary>The nearest row to "pref" that is a road from x0 to x1 (searched down the screen and up it, 6 px at a time, from the top of the wolf's ground to the bottom drifts).</summary>
    private bool TransitRow(float x0, float x1, float pref, out float row)
    {
        float yMax = MathF.Min(S.Height - 50f, _yHi + 0.06f * S.U);
        for (int k = 0; k <= 60; k++)
            for (int sg = 1; sg >= -1; sg -= 2)
            {
                if (k == 0 && sg == -1) continue;
                float r = pref + sg * k * 6f;
                if (r < _yLo || r > yMax) continue;
                if (RowFree(x0, x1, r)) { row = r; return true; }
            }
        row = 0f; return false;
    }
    private bool LaneClear(float x, float y, int dir, float aheadM, float toY, float room)
    {
        for (int k = 1; k <= 6; k++)
        {
            float f = k / 6f, xx = x + dir * aheadM * _m * f, yy = y + (toY - y) * f;
            if (xx < 4f || xx > S.Width - 4f) continue;                                   // off screen: nothing to hit (and CanStand says no there, which is not a prop)
            if (!Free(xx, yy, room)) return false;
        }
        return true;
    }

    // ================================================================ THE WOLF

    private void ShowW(int book, int page) { if (_wb == null) return; _w.Bk = _wb[book]; _w.Pg = Math.Clamp(page, 0, _w.Bk.Count - 1); }

    private void StartSheet(int book, int n, float dur, bool rev, SA after) { var w = _w; w.Do = WD.Sheet; w.T = 0f; w.SBook = book; w.SN = n; w.SDur = dur; w.ST = 0f; w.SRev = rev; w.After = after; ShowW(book, rev ? n - 1 : 0); }
    private void StartLoop(int book, int n, float cycle, float total, SA after) { var w = _w; w.Do = WD.Loop; w.T = 0f; w.SBook = book; w.SN = n; w.SCycle = cycle; w.SLoopFor = total; w.ST = 0f; w.After = after; ShowW(book, 0); }

    private bool _mapped;
    private void DumpMap(float W)
    {
        if (_log != null)
        {
            foreach (float tx in new[] { 700f, 1100f, 1300f, 1450f })
            {
                var sb2 = new System.Text.StringBuilder();
                for (int iy = 0; iy < 14; iy++) sb2.Append(S.NearDepth[(int)(904 - iy * 10) * S.Width + (int)tx]).Append(' ');
                Log($"  nd at x {tx:F0}, rows 904 up by 10: me {S.Nearness(904f)}: {sb2}");
            }
        }
        if (_log != null)
            for (float ry = _yLo; ry <= 1040f; ry += 10f)
            {
                var sb = new System.Text.StringBuilder();
                for (float rx = 0f; rx < W; rx += 40f) sb.Append(S.OnPond(rx, ry, 1.18f) ? 'P' : !Free(rx, ry, 0.5f * _m) ? '#' : CrowdedW(rx, ry) ? 'h' : Covered(rx, ry, 1.0f * _m, 0.9f * _m) ? 'c' : '.');
                Log($"  map y {ry:F0} {sb} road to 1190 {RowFree(W + 2.2f * _m, 1190f, ry)}, to 1450 {RowFree(W + 2.2f * _m, 1450f, ry)}");
            }
    }

    private void StartWolf(int kind, bool wantCatch)
    {
        var w = _w;
        if (_log != null && !_mapped) { _mapped = true; DumpMap(S.Width); }
        w.On = true; w.Kind = kind; w.PreyKind = kind == 3 ? 4 : 3; w.Rear = false; w.WantCatch = kind == 3 ? false : wantCatch; w.Carrying = false; w.Hare = -1; w.Tries = 0; w.Reseeks = 0; w.Plan.Clear(); w.MissPlanned = false; w.Cov = null;
        float W = S.Width;
        // the side it comes in from: toward the hares' side for a hunt; for a howl, from the right so it faces the moon
        float avg = 0.7f; int hn = _field.HareCount, cnt = 0; float sum = 0;
        for (int i = 0; i < hn; i++) { sum += _field.FeetOf(i).X; cnt++; }
        if (cnt > 0) avg = sum / cnt / W;
        int dir = kind == 2 ? -1 : avg > 0.5f ? 1 : -1;
        if (kind == 1 && Rng.Next(2) == 0) dir = -dir;
        // A MISS is the hare reaching cover: so a hunt that means to miss starts by finding a hare with cover a few metres BEYOND it, and comes in from the side
        // that puts the wolf, the hare and the cover in a line (the hare runs away from the wolf, into the cover)
        w.PreHare = -1; float preGX = -1f, preGY = -1f, preTR = -1f;
        if (_log != null) for (int i = 0; i < _hv.Length; i++) { Cover? c0 = NearestCover(_hv[i].Feet, out float d0); Log($"hare{i} at {_hv[i].Feet.X:F0},{_hv[i].Feet.Y:F0} out {_hv[i].Out} speed {_hv[i].Speed:F0}, nearest cover {(c0 is Cover cc ? cc.Kind + " at " + cc.Foot.X.ToString("F0") + "," + cc.Foot.Y.ToString("F0") : "none")} {d0:F1} m (yLo {_yLo:F0} yHi {_yHi:F0})"); }
        if (kind == 0 && !wantCatch)
        {
            float bestRun = 0f, laneY = Math.Clamp(S.FieldPoint(0.5f * W, 0.26f).Y, _yLo, _yHi);
            for (int i = 0; i < _hv.Length; i++)
            {
                PV v = _hv[i];
                if (!Still(v) || v.Feet.Y < _yLo - 2f) continue;
                Cover? cv = NearestCover(v.Feet, out float dm);
                if (cv == null || dm < 0.3f || dm > 7f || !RunIceFree(v.Feet.X - (cv.Value.Hide.X > v.Feet.X ? 1 : -1) * 4.6f * _m, cv.Value.Hide.X, v.Feet.Y)) continue;
                int d = cv.Value.Hide.X > v.Feet.X ? 1 : -1; float gx = Math.Clamp(v.Feet.X - d * 11f * _m, 0.06f * W, 0.94f * W);
                float hareRow = Math.Clamp(v.Feet.Y, _yLo, _yHi), grow = hareRow;
                for (int t = 0; t < 60 && !(StandOk(gx, hareRow) || StandOk(gx, laneY)); t++) gx += d * 0.4f * _m;      // where it can really stand and sniff
                if (StandOk(gx, laneY) && !StandOk(gx, hareRow)) grow = laneY;
                float run = (v.Feet.X - gx) * d;
                if (_log != null) Log($"  miss plan: hare{i} d {d} stand {gx:F0},{grow:F0} run {run / _m:F1} m, standOk {StandOk(gx, grow)}");
                if (run < 4.6f * _m || run <= bestRun || !StandOk(gx, grow) || !PlanRoad(W + 2.2f * _m, gx, grow, grow, true, out _, out _)) continue;                  // the longest run-up
                bestRun = run; w.PreHare = i; dir = d; preGX = gx; preGY = grow; preTR = grow;
            }
        }
        if (kind == 0 && wantCatch)
        {
            // A CATCH NEEDS A RUN-UP TOO. (Before this the wolf stopped at a place rolled without a thought for where the hares were: on every seed it
            // parked right beside them, "a hare under its nose" has no run-up, and it stood there choosing nothing until it gave up and left.) So, like
            // the miss above, it picks its hare BEFORE it walks in: one sitting still with eleven metres of clear snow on the wolf's side for the walk
            // and the rush, and five metres on the far side so the end is on screen; of those, the one farthest from any cover. It comes in from that side.
            float bestScore = -1e9f, laneY = Math.Clamp(S.FieldPoint(0.5f * W, 0.26f).Y, _yLo, _yHi);
            for (int i = 0; i < _hv.Length; i++)
            {
                PV v = _hv[i];
                if (!Still(v) || v.Feet.Y < _yLo - 2f || v.Feet.Y > _yHi + 0.05f * S.U) continue;
                if (Covered(v.Feet.X - 0.3f * _m, v.Feet.Y, 1.5f * _m, 0.9f * _m)) continue;                // (the catch must be in the open: see SelectTarget)
                NearestCover(v.Feet, out float dm);
                for (int d = 1; d <= 1; d += 2)                 // d: the way the wolf will run (and the way the hare would bolt). ONLY to the right: the wolf comes in from the right edge, and after a catch it trots off to the right edge with the hare (the left half is the pond: a wolf carrying a hare would have to turn round on the spot)
                {
                    float far = (d > 0 ? W - v.Feet.X : v.Feet.X) / _m;
                    if (far < 3.0f) continue;                                       // the end must be on screen
                    // WHERE IT WILL STAND: a free place 4.8 to 11 metres short of the hare (8 is best: room to creep, then the rush), on the hare's own
                    // row or the tree-line lane, with no ice between. The pond takes the middle of the field, so the places are few: it tries them all.
                    float hareRow = Math.Clamp(v.Feet.Y, _yLo, _yHi), gx = 0f, gy = 0f, run = 0f, ptr = 0f;
                    for (int r = 0; r < 2 && run == 0f; r++)
                    {
                        float row = r == 0 ? hareRow : laneY;
                        for (int k = 0; k < BackTries.Length && run == 0f; k++)
                        {
                            float back = BackTries[k];                                    // 9.5 first (a long creep, then the rush), down to 5.5
                            float x = v.Feet.X - d * back * _m;
                            if (x < 0.06f * W || x > 0.94f * W || !StandOk(x, row)) continue;
                            if (!RunIceFree(x, v.Feet.X - d * 4.6f * _m, row) || !RunIceFree(v.Feet.X - d * 4.6f * _m, v.Feet.X + d * 3f * _m, v.Feet.Y)) continue;
                            // THE ROAD IN: the wolf comes in from the RIGHT edge (the pond fills every row of the left half of its ground, so no road
                            // from the left edge to the right half is bare snow; before this it walked straight over the ice)
                            if (!PlanRoad(W + 2.2f * _m, x, row, row, true, out _, out _)) continue;
                            gx = x; gy = row; run = back; ptr = row;
                        }
                    }
                    if (_log != null) Log($"  catch plan: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0} from the {(d > 0 ? "left" : "right")}: far side {far:F1} m, cover {dm:F1} m, {(run > 0f ? $"stand at {gx:F0},{gy:F0}, {run:F1} m back" : "no free place to stand")}");
                    if (run == 0f) continue;
                    float score = dm + 0.1f * run;
                    if (score > bestScore) { bestScore = score; w.PreHare = i; dir = d; preGX = gx; preGY = gy; preTR = ptr; }
                }
            }
        }
        if (kind == 3)
        {
            // A CHASE UP A PINE: find a squirrel out on the snow with its pine on the far side from where the wolf will stand (wolf, squirrel, pine in a line:
            // the squirrel runs AWAY from the wolf, to the trunk), and come in from the side that makes that line
            float bestRun = 0f, laneY = Math.Clamp(S.FieldPoint(0.5f * W, 0.26f).Y, _yLo, _yHi);
            for (int i = 0; i < _sv.Length; i++)
            {
                PV v = _sv[i];
                if (_log != null && false) Log($"squirrel{i} at {v.Feet.X:F0},{v.Feet.Y:F0} out {v.Out} speed {v.Speed:F0} still {Still(v)} (yLo {_yLo:F0} yHi {_yHi:F0})");
                if (!Still(v) || v.Feet.Y < _yLo - 2f || v.Feet.Y > _yHi + 0.05f * S.U) continue;
                float gx = 0f; int d = 0;
                for (int side = -1; side <= 1 && d == 0; side += 2)
                {
                    float tryGx = Math.Clamp(v.Feet.X - side * 11f * _m, 0.06f * W, 0.94f * W);
                    float px0 = S.Pines[_sq.PineFor(i, tryGx)].X;
                    if ((px0 - v.Feet.X) * side > 0f && RunIceFree(v.Feet.X - side * 4.5f * _m, px0, v.Feet.Y)) { d = side; gx = tryGx; }
                }
                if (d == 0) continue;
                for (int t = 0; t < 60 && (!Free(gx, laneY, 0.5f * _m) || Covered(gx, laneY, 1.0f * _m, 1.0f * _m)); t++) gx += d * 0.4f * _m;
                float run = (v.Feet.X - gx) * d;
                if (run < 6.0f * _m || run <= bestRun) continue;
                bestRun = run; w.PreHare = i; dir = d; preGX = gx;
            }
            if (w.PreHare < 0) { w.On = false; return; }
        }
        // EVERY WOLF BUT THE SQUIRREL CHASER COMES IN FROM THE RIGHT EDGE (see the catch plan: the ice shuts the left). It walks in facing left, to a stand
        // on the right half of the field; when its work is on the right of the stand (a hare to hunt, or the way out) it turns there on the spot.
        int huntDir = dir;
        float standGX = preGX, standRow = preGY, transit = preTR;
        if (kind != 3)
        {
            if (kind == 0) { if (w.PreHare < 0) { w.On = false; Log("WOLF visit not begun: no hare can be hunted from a place the wolf can reach"); return; } }
            else
            {
                // a crossing or a howl: any bare place on the right half with a road to it
                float laneRow = S.FieldPoint(0.5f * W, 0.40f).Y; bool okp = false;
                for (int t = 0; t < 40 && !okp; t++)
                {
                    standGX = (kind == 2 ? 0.62f + 0.16f * (float)Rng.NextDouble() : 0.58f + 0.30f * (float)Rng.NextDouble()) * W;
                    standRow = Math.Clamp(laneRow + (float)(Rng.NextDouble() - 0.5) * 0.08f * S.U, _yLo, _yHi);
                    okp = StandOk(standGX, standRow) && PlanRoad(W + 2.2f * _m, standGX, standRow, standRow, true, out _, out _);
                }
                if (!okp) { w.On = false; Log("WOLF visit not begun: no bare place on the right half to stand"); return; }
            }
            dir = -1; preGX = standGX; preGY = standRow;
            // the road itself (it was only tested above): rows[0] is the stand's column, the last is the edge
            if (PlanRoad(W + 2.2f * _m, standGX, standRow, standRow, true, out float[]? rds, out int rk0) && rds != null)
            { w.Road = rds; w.RoadK0 = rk0; standRow = rds[0]; transit = rds[^1]; preGY = standRow; }
            else { w.On = false; Log("WOLF visit not begun: the road vanished"); return; }
        }
        w.Dir = dir; w.X = dir > 0 ? -2.2f * _m : W + 2.2f * _m;
        w.Y = Math.Clamp(S.FieldPoint(0.5f * W, 0.24f + 0.04f * (float)Rng.NextDouble()).Y, _yLo, _yHi); w.GY = w.Y;
        if (kind != 3) { w.Y = transit; w.GY = transit; } else w.Road = null;
        w.Gait = 0; w.Pace.Halt(); w.Pace.Walked = 0f; w.Pace.Resume(GaitV[0] * _m * 0.6f);
        w.PostT = 0f; w.MoveT = 0f; w.BreathT = 2f; w.Do = WD.Walk; w.StopAtGoal = true; w.Dyn = false; w.Braking = false;
        switch (kind)
        {
            case 0:
                w.GX = preGX;
                if (huntDir > 0) w.Plan.Add(WS.FaceR);
                w.Plan.AddRange([WS.Sniff, WS.Look, WS.Select]); break;
            case 3:
                w.GX = preGX > 0f ? preGX : Math.Clamp(dir > 0 ? Rng.Next(25, 42) / 100f * W : Rng.Next(58, 75) / 100f * W, 0.1f * W, 0.9f * W);
                w.Plan.AddRange([WS.Sniff, WS.Look, WS.Select]); break;
            case 1:
                w.GX = preGX;
                w.Plan.AddRange([WS.Sniff, WS.Look, WS.FaceR, WS.StartWalk, WS.Exit]); break;
            default:
                w.GX = preGX;
                w.Plan.AddRange([WS.Howl, WS.Look, WS.FaceR, WS.StartWalk, WS.Exit]); break;
        }
        // the lane must be free where it stops (a planned catch has already found its free place, and its row: the walk drifts onto that row)
        if (kind == 3) { if (preGY > 0f) w.GY = preGY; else for (int tries = 0; tries < 40 && (!Free(w.GX, w.Y, 0.5f * _m) || Covered(w.GX, w.Y, 1.0f * _m, 1.0f * _m)); tries++) w.GX += dir * 0.4f * _m; }
        w.PlanY = kind == 3 ? w.GY : standRow; w.Arrive = true; w.Held = false;
        ShowW(PredatorsArt.WLopeB, 0); w.Mir = dir < 0;
        // THE HARE THE PLAN WAS MADE FOR STAYS WHERE IT IS from the moment the wolf sets out (it goes on with what it was doing, but starts nothing new).
        // On film (2026-11-11) the walk in took fourteen seconds, the hare had hopped off behind a shrub by then, and the wolf looked about and left: in
        // twenty minutes of natural runs it never hunted once. EndWolf lets the hare go again whatever happens.
        if (kind == 0 && w.PreHare >= 0) PreyStalked(3, w.PreHare);
        Log($"WOLF visit begins: kind {(kind == 0 ? "kill" : kind == 3 ? "squirrel chase" : kind == 1 ? "cross" : "howl")}, intends {(wantCatch ? "a catch" : "a miss")}, from the {(dir > 0 ? "left" : "right")}, to x {w.GX:F0}");
    }

    private void EndWolf()
    {
        var w = _w;
        _field.Stalked(-1); _sq.Stalked(-1); _field.PredatorAt = null;
        w.On = false; w.Do = WD.Off; _lastWolfEnd = _time;
        double f = Math.Max(0.2, _pct);
        _nextWolf = _time + (_test == "many" ? 32.0 + 12.0 * Rng.NextDouble() : (180.0 + 180.0 * Rng.NextDouble()) / f);
        Log($"WOLF visit ends; the next is not before {_nextWolf:F0} s");
    }

    private bool OffScreen(float x) => x < -2.4f * _m || x > S.Width + 2.4f * _m;

    /// <summary>The next step of the script.</summary>
    private void NextW()
    {
        var w = _w;
        if (w.Plan.Count == 0) { EndWolf(); return; }
        WS s = w.Plan[0]; w.Plan.RemoveAt(0);
        Log($"WOLF step {s}");
        switch (s)
        {
            case WS.Sniff: StartSheet(PredatorsArt.WSniff, PredatorsArt.NSniff, 2.5f, false, SA.Next); w.Flag1 = false; w.Flag2 = false; break;
            case WS.Look: StartSheet(PredatorsArt.WLook, PredatorsArt.NLook, 1.9f, false, SA.Next); break;
            case WS.Idle: w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 1.0f; break;
            case WS.Turn: BeginTurn(w.NewDir); break;
            case WS.FaceR: if (w.Dir == 1) NextW(); else BeginTurn(1); break;
            case WS.FaceL: if (w.Dir == -1) NextW(); else BeginTurn(-1); break;
            // (StartWalk only chooses the gait now. It used to START a walk, and then the Exit that followed stopped it again on the next frame: a settle,
            // one frame of walk, and a settle again, a stutter at the start of every leaving)
            case WS.StartWalk: w.Gait = w.Carrying ? 2 : 0; NextW(); break;
            case WS.Select: SelectTarget(); break;
            case WS.Stalk: w.Road = null; w.Gait = 1; w.Dyn = true; w.StopAtGoal = true; w.RushAfter = true; BeginWalkFromStand(); break;
            case WS.Howl: StartSheet(PredatorsArt.WHowlIn, PredatorsArt.NHowlIn, 0.85f, false, SA.Next); w.Flag1 = true; w.ST = 0f; break;
            case WS.Rear: w.Rear = true; StartSheet(PredatorsArt.WRearIn, PredatorsArt.NRearIn, 0.95f, false, SA.Next); break;
            case WS.Dig: StartSheet(PredatorsArt.WDigIn, PredatorsArt.NDigIn, 0.65f, false, SA.Next); w.Flag1 = true; w.Flag2 = false; break;
            case WS.CShake: StartSheet(PredatorsArt.WCShake, PredatorsArt.NCShake, 0.85f, false, SA.Next); w.Flag1 = true; break;
            case WS.CLook: StartSheet(PredatorsArt.WCLook, PredatorsArt.NCLook, 1.3f, false, SA.Next); break;
            case WS.WalkTo:
                w.Gait = 0; w.StopAtGoal = true; w.Dyn = false; w.Road = null;
                if (PlanRoad(w.X, w.GX, w.Y, w.GY, false, out float[]? wr, out int wk0) && wr != null) { w.Road = wr; w.RoadK0 = wk0; }
                BeginWalkFromStand(); break;
            case WS.Exit:
                if (w.X > 0.5f * S.Width && w.Dir < 0 && !w.Carrying) { w.Plan.Insert(0, WS.Exit); BeginTurn(1); break; }       // the right half leaves by the right edge
                w.Gait = w.Carrying ? 2 : 0;
                w.GX = w.Dir > 0 ? S.Width + 3.5f * _m : -3.5f * _m; w.GY = w.Y; w.StopAtGoal = false; w.Dyn = false; w.RushAfter = false;
                // THE ROAD OUT: a row that is bare snow all the way to the edge and keeps clear of whoever is sitting there
                w.Road = null;
                if (PlanRoad(w.X, w.GX, w.Y, w.Y, false, out float[]? xr, out int xk0) && xr != null) { w.Road = xr; w.RoadK0 = xk0; w.GY = RoadY(w.X); w.PlanY = w.GY; }
                else if (TransitRow(w.X, w.GX, w.Y, out float exitRow)) { w.GY = exitRow; w.PlanY = exitRow; }
                if (w.Do != WD.Walk) BeginWalkFromStand();
                break;
        }
    }

    private void BeginWalkFromStand()
    {
        var w = _w;
        w.Pace.Halt(); w.Pace.Walked = 0f; w.Braking = false; w.StuckT = 0f; w.PathT = 0f;
        StartSheet(SetBook[w.Gait], SetN[w.Gait], w.Gait == 1 ? 0.55f : 0.42f, true, SA.WalkNow);
    }

    private void BeginTurn(int newDir) { var w = _w; w.Do = WD.Turn; w.T = 0f; w.NewDir = newDir; w.Pace.Halt(); }

    // ---- the script's one decision: which hare ----
    private void SelectTarget()
    {
        var w = _w;
        if (w.PreyKind == 4) { SelectSquirrel(); return; }
        int best = -1; float bestScore = -1e9f;
        if (w.Tries > 20) { Log("WOLF has failed too often: it walks off"); w.Plan.Clear(); w.Plan.AddRange([WS.StartWalk, WS.Exit]); NextW(); return; }
        for (int i = 0; i < _hv.Length; i++)
        {
            var v = _hv[i];
            if (_log != null && (w.Tries & 3) == 1) Log($"  select: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0} out {v.Out} speed {v.Speed:F0} still {Still(v)}; wolf {w.X:F0},{w.Y:F0}; rows {_yLo:F0}-{_yHi:F0}");
            if (!Still(v) || v.Feet.Y < _yLo - 2f || v.Feet.Y > _yHi + 0.05f * S.U) continue;
            if (MathF.Abs(v.Feet.X - w.X) < 4.6f * _m) continue;                         // not a hare right under its nose: there would be no creep and no run-up
            Cover? cv = NearestCover(v.Feet, out float dCoverM);
            // never across the ice: the wolf walks along ITS OWN row to within four and a half metres of the hare, then changes to the hare's row for the last part
            int toward = v.Feet.X > w.X ? 1 : -1;
            // THE HUNT ENDS ON SCREEN: a hare with under three metres of snow between it and the screen's edge on its far side is left alone (one such
            // chase ran off the side: the "catch" was never seen, and took nothing, since a hare off screen is not in the open)
            // THE CATCH MUST BE IN THE OPEN: the tumble, the cloud and the carry-stand happen on and round the hare's place. A hare sitting at a snowman's foot (on film the
            // wolf rolled in a snow cloud and then stood with the snowman's belly across its back) is left alone.
            if (w.WantCatch && Covered(v.Feet.X - toward * 0.3f * _m, v.Feet.Y, 1.5f * _m, 0.9f * _m)) continue;
            float farM = (toward > 0 ? S.Width - v.Feet.X : v.Feet.X) / _m;
            if (_log != null) Log($"  select: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0}, wolf at {w.X:F0}: gap {MathF.Abs(v.Feet.X - w.X) / _m:F1} m, far side {farM:F1} m, cover {dCoverM:F1} m");
            if (farM < 3.0f) continue;
            if (!RunIceFree(w.X + toward * 0.9f * _m, v.Feet.X - toward * 4.6f * _m, w.Y) || !RunIceFree(v.Feet.X - toward * 4.6f * _m, v.Feet.X + toward * 3f * _m, v.Feet.Y)) continue;
            // NO BODY THROUGH A BODY: another hare standing between the wolf and this one is in the way of the last bound. It used to REJECT the hare,
            // and hares sit in PAIRS, so both were rejected every time and the wolf never hunted. Now it only counts against the choice here; the real
            // test is at the END OF THE CREEP (LineBlocked): by then the partner has been frightened by the Threat and has usually gone to cover.
            float score = LineBlocked(i, w.X, toward) ? -0.5f : 0f;
            if (w.WantCatch)
            {
                // a catch wants a hare far from any cover: one a stride from a bush cannot be caught from this far (it was the reason the wolf "only missed").
                // After three idle looks it takes what there is, and the outcome is honest (the hare reaches the cover and the wolf skids at it).
                // (It was twelve looks, a second each: on a field where every hare sits by a shrub the wolf stood like a statue for a quarter of a minute.)
                if (dCoverM < 1.3f && w.Tries < 3) continue;
                score += dCoverM;
            }
            else
            {
                // a miss wants cover a few metres off, BEYOND the hare (the wolf, the hare and the cover in a line: the hare runs away from the wolf, into it)
                if (cv == null || dCoverM < 0.3f || dCoverM > 8.0f || (cv.Value.Hide.X - v.Feet.X) * (v.Feet.X - w.X) <= 0f) continue;
                score += -MathF.Abs(dCoverM - 3.0f);
            }
            score -= 0.002f * MathF.Abs(v.Feet.X - w.X) / _m;
            if (i == w.PreHare) score += 100f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0)
        {
            if (!w.WantCatch && w.Tries == 3) { w.WantCatch = true; Log("WOLF: no hare has cover beyond it; it hunts for the catch instead"); }
            if (++w.Tries > 6) { Log("WOLF finds no hare to hunt: it walks off"); w.Plan.Clear(); w.Plan.AddRange([WS.StartWalk, WS.Exit]); }
            else { w.Plan.Insert(0, WS.Select); w.Plan.Insert(0, w.Tries == 4 ? WS.Look : WS.Idle); }          // (it waits for a hare to come out: a look about now and then, not a statue)
            NextW(); return;
        }
        var hv = _hv[best];
        NearestCover(hv.Feet, out float dm);
        // A HARE FAR OFF is approached at a lope first (the stalk's 0.6 m a second over twelve metres would be a twelve second crawl): to a place 7.5 to 9.5 m
        // short of it, on bare snow, ahead of the wolf; then it settles, and thinks again from there (the hare may have moved).
        if (w.WantCatch && MathF.Abs(hv.Feet.X - w.X) > 11.0f * _m)
        {
            int need0 = hv.Feet.X > w.X ? 1 : -1;
            if (need0 != w.Dir) { w.NewDir = need0; w.Plan.Insert(0, WS.Select); w.Plan.Insert(0, WS.Turn); NextW(); return; }
            float row0 = Math.Clamp(hv.Feet.Y, _yLo, _yHi);
            foreach (float back in new[] { 9.5f, 9.0f, 8.5f, 8.0f, 7.5f })
            {
                float gx0 = hv.Feet.X - need0 * back * _m;
                if ((gx0 - w.X) * need0 < 1.5f * _m || !StandOk(gx0, row0) || !RowClear(w.X + need0 * 0.5f * _m, gx0, row0)) continue;
                w.GX = gx0; w.GY = row0; w.PlanY = row0; w.Arrive = true;
                Log($"WOLF: hare{best} is {MathF.Abs(hv.Feet.X - w.X) / _m:F1} m off: it lopes to {gx0:F0},{row0:F0} ({back:F1} m short of it) first");
                w.Plan.Insert(0, WS.Select); w.Plan.Insert(0, WS.WalkTo);
                NextW(); return;
            }
        }
        // an honest outcome: a hare that sits a stride from a shrub cannot be caught from this far; one in the open usually can
        // THE CREEP IS SEEN: from where it stands it creeps at least 2 m, and the rush is as long as the ground gives (the rush-in is 2.2 m and the
        // pounce begins about 1.8 m short of the contact point, so 5.2 m is the least; 8.3 m holds a whole gallop cycle).
        float gapNowM = MathF.Abs(hv.Feet.X - w.X) / _m;
        w.Hare = best; w.RushGap = w.WantCatch ? Math.Clamp(gapNowM - 2.0f, 5.2f, 8.3f) : 5.9f; w.CreepFrom = w.X;
        // (only a hunt that means to MISS plans its rush round the cover: a catch takes a hare that sits, unaware, wherever its cover is)
        if (!w.WantCatch && NearestCover(hv.Feet, out _) is Cover cv9)
        {
            // far enough back that the gallop can end in a skid AT the cover (the rush-in eats 2.6 m, the first rest page to skid from comes 3.1 m on, the skid needs 2.6 m)
            int dw = hv.Feet.X > w.X ? 1 : -1;
            float off = 0.3f * cv9.HalfW + 1.3f * _m, gapPx = 7.8f * _m - (cv9.Foot.X - hv.Feet.X) * dw + off;
            w.RushGap = Math.Clamp(gapPx / _m, 5.9f, 11f);
        }
        _field.Stalked(best);
        Log($"WOLF picks hare{best} at {hv.Feet.X:F0},{hv.Feet.Y:F0} (cover {dm:F1} m off), will try for {(w.WantCatch ? "the catch" : "a miss")}");
        int need = hv.Feet.X > w.X ? 1 : -1;
        if (need != w.Dir) { w.NewDir = need; w.Plan.Insert(0, WS.Stalk); w.Plan.Insert(0, WS.Turn); }
        else w.Plan.Insert(0, WS.Stalk);
        NextW();
    }

    /// <summary>The pine a squirrel at p runs to when the danger is at avoidX (the same rule as Squirrels.NearestPine: pines on the danger's side are penalised by 0.6 U).</summary>
    private int NearestPineIx(PointF p, float avoidX = float.NaN) { int best = 0; float bd = float.MaxValue; for (int i = 0; i < S.Pines.Count; i++) { float d = MathF.Abs(S.Pines[i].X - p.X) + 0.4f * MathF.Abs(S.Pines[i].Foot - p.Y); if (!float.IsNaN(avoidX) && (S.Pines[i].X - p.X) * (avoidX - p.X) > 0f) d += 0.6f * S.U; if (d < bd) { bd = d; best = i; } } return best; }

    /// <summary>A pine's trunk as a piece of "cover" for the wolf's skid and its standing place: the wolf stops on the top row of its lane, 1.3 m short of the trunk.</summary>
    private Cover TreeCover(int pine)
    {
        float x = S.Pines[pine].X, y = Math.Clamp(_yLo + 8f, _yLo, _yHi);
        return new Cover(CoverKind.Rocks, new PointF(x, y + 4f), 0f, 1.0f * _m, new PointF(x, y), 0f, 0f);
    }

    /// <summary>The script's one decision in a squirrel chase: which squirrel (out, still, its pine on the far side from the wolf), then the stalk and the rush.</summary>
    private void SelectSquirrel()
    {
        var w = _w;
        int best = -1; float bestScore = -1e9f;
        for (int i = 0; i < _sv.Length; i++)
        {
            var v = _sv[i];
            if (_log != null && false) Log($"  select: squirrel{i} at {v.Feet.X:F0},{v.Feet.Y:F0} out {v.Out} speed {v.Speed:F0}; wolf at {w.X:F0}; pine {S.Pines[NearestPineIx(v.Feet)].X:F0}");
            if (!Still(v) || v.Feet.Y < _yLo - 2f || v.Feet.Y > _yHi + 0.05f * S.U) continue;
            int pi = _sq.PineFor(i, w.X); float px = S.Pines[pi].X;
            if ((px - v.Feet.X) * (v.Feet.X - w.X) <= 0f) continue;                                   // the pine must lie beyond the squirrel, away from the wolf
            int ap = v.Feet.X > w.X ? 1 : -1;
            if (!RunIceFree(v.Feet.X - ap * 4.5f * _m, px, v.Feet.Y)) continue;                // the last nine metres of approach AND the squirrel's run to its pine must be snow along the wolf's row (it never sets a paw on the ice)
            float score = -MathF.Abs(v.Feet.X - w.X) / _m + (i == w.PreHare ? 100f : 0f);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0)
        {
            if (++w.Tries > 30) { Log("WOLF finds no squirrel to chase: it walks off"); w.Plan.Clear(); w.Plan.AddRange([WS.StartWalk, WS.Exit]); }
            else { w.Plan.Insert(0, WS.Select); w.Plan.Insert(0, WS.Idle); }
            NextW(); return;
        }
        var sv = _sv[best];
        w.Hare = best; w.RushGap = 3.2f; _sq.Stalked(best);
        Log($"WOLF picks squirrel{best} at {sv.Feet.X:F0},{sv.Feet.Y:F0}, its pine at {S.Pines[_sq.PineFor(best, w.X)].X:F0}");
        int need = sv.Feet.X > w.X ? 1 : -1;
        if (need != w.Dir) { w.NewDir = need; w.Plan.Insert(0, WS.Stalk); w.Plan.Insert(0, WS.Turn); }
        else w.Plan.Insert(0, WS.Stalk);
        NextW();
    }

    private void UpdateWolf(float dt)
    {
        var w = _w;
        w.T += dt;
        _field.PredatorAt = w.Do == WD.Off ? null : new PointF(w.X, w.Y); _field.PredatorIsWolf = true;
        float U = S.U;
        // the pins
        bool hunting = w.Do is WD.RushIn or WD.Gallop or WD.Pounce or WD.Hold || (w.Do == WD.Walk && w.Gait == 1) || (w.Carrying && w.Do != WD.Off) || w.Do == WD.Skid || w.Rear;
        if (hunting) Post(Sense.Threat, w.X, w.Y, 0.45f, 1f, ref w.PostT, dt, 0.6f);
        else if (w.Do == WD.Walk) Post(Sense.Movement, w.X, w.Y, 0.25f, 0.3f, ref w.MoveT, dt, 1.5f);
        S.Occupy(this, 0, w.X, w.Y, 0.85f * _m);

        switch (w.Do)
        {
            case WD.Walk: StepWalk(dt); break;
            case WD.SetIn:
            case WD.SetOut: break;
            case WD.Stand:
            {
                int book = w.Carrying ? PredatorsArt.WCStand : PredatorsArt.WStandB, n = w.Carrying ? PredatorsArt.NCStand : PredatorsArt.NStand;
                ShowW(book, (int)(w.T * 14f) % n);
                w.BreathT -= dt; if (w.BreathT <= 0f) { PointF nz = _w.Bk!.Aux[0]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 0f); w.BreathT = 3.2f + 1.5f * (float)Rng.NextDouble(); }
                if (w.T >= w.SLoopFor) NextW();
                break;
            }
            case WD.Sheet: StepSheet(dt); break;
            case WD.Loop: StepLoop(dt); break;
            case WD.Turn: StepTurn(dt); break;
            case WD.RushIn: StepRushIn(dt); break;
            case WD.Gallop: StepGallop(dt); break;
            case WD.Skid: StepSkid(dt); break;
            case WD.Pounce: StepPounce(dt); break;
            case WD.Hold:
                // THE STARE (squirrel chase only): the wolf has stopped on its crouched rest page and the squirrel has just heard it. It holds, still but for a
                // breath, until the squirrel has bolted (so the squirrel is already running to a trunk), then it explodes into the rush: it never gallops
                // into a squirrel that has not had time to move.
                if (w.Held) { HoldForLine(dt); break; }
                ShowW(PredatorsArt.WStalkB, 0);
                if (_sq.PineOf(w.Hare) >= 0 || w.T > 2.2f) BeginRush();
                break;
        }
        if (_log != null && w.Bk != null)
            _log.WriteLine($"{_time,8:F2} predators wolf {w.X:F1} {w.Y:F1} {w.Pace.Speed:F1} {w.Do} {w.Bk.Name} {w.Pg}");
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Is another hare (out on the snow, not the chosen one) between the wolf and hare i, or within 1.2 m beyond it, on about the same row?</summary>
    private bool LineBlocked(int i, float wx, int toward)
    {
        PV v = _hv[i];
        for (int j = 0; j < _hv.Length; j++)
        {
            if (j == i || !_hv[j].Out) continue;
            float jx = _hv[j].Feet.X;
            if ((jx - wx) * toward > 0f && (v.Feet.X + toward * 1.2f * _m - jx) * toward > 0f && MathF.Abs(_hv[j].Feet.Y - v.Feet.Y) < 0.07f * S.U) return true;
        }
        return false;
    }

    /// <summary>The wolf holds its crouch (the stalk's rest page, its breath curling up) until the hare in the line has gone: up to 3 s; if it has not, it rises and thinks again.</summary>
    private void HoldForLine(float dt)
    {
        var w = _w;
        w.HoldT += dt;
        ShowW(PredatorsArt.WStalkB, 0);
        w.BreathT -= dt; if (w.BreathT <= 0f && w.Bk != null) { PointF nz = w.Bk.Aux[0]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 0f); w.BreathT = 1.1f + 0.5f * (float)Rng.NextDouble(); }
        var hv = _hv[w.Hare];
        bool gone = !hv.Out || Bolting(hv);
        bool blocked = LineBlocked(w.Hare, w.X, w.Dir);
        if (!gone && !blocked) { w.Held = false; Log($"WOLF: the line is clear after {w.HoldT:F1} s; it rushes"); BeginRush(); return; }
        if (gone || w.HoldT > 3.0f)
        {
            Log(gone ? "WOLF: the chosen hare went while it held; it rises" : "WOLF: the line did not clear in 3 s; it rises and looks again");
            w.Held = false; _field.Stalked(-1); w.Hare = -1; w.Dyn = false; w.RushAfter = false; w.Tries += gone ? 1 : 3;
            w.Plan.Clear(); w.Plan.Add(WS.Select);
            StartSheet(SetBook[1], SetN[1], 0.55f, false, SA.Stand);
        }
    }

    private void StepWalk(float dt)
    {
        var w = _w;
        float v = GaitV[w.Gait] * _m, stride = GaitC[w.Gait] * _m;
        // a stalk or a rush's approach aims at the hare's place every frame
        if (w.Dyn && w.Hare >= 0)
        {
            var hv = PreyView(w.PreyKind)[w.Hare];
            // the hare went away (behind cover, off on a hop): give up the hunt on the next rest page
            if (!hv.Out || (Bolting(hv) && w.PreyKind != 4) || MathF.Abs(hv.Feet.X - w.X) > 0.8f * S.Width)
            {
                if (!w.Braking) { Log("WOLF: the hare is gone; it stops and walks off"); _field.Stalked(-1); w.Hare = -1; w.Dyn = false; w.RushAfter = false; w.Plan.Clear(); w.Plan.AddRange([WS.StartWalk, WS.Exit]); w.GX = w.X + w.Dir * 99999f; BrakeNow(stride); }
            }
            else
            {
                w.GX = hv.Feet.X - w.Dir * w.RushGap * _m; w.GY = Math.Clamp(hv.Feet.Y, _yLo, _yHi);
                if ((w.GX - w.X) * w.Dir < 0f && !w.Braking) BrakeNow(stride);          // already inside the range: stop on the next rest page
            }
        }
        if (w.StopAtGoal && !w.Braking)
        {
            float remaining = (w.GX - w.X) * w.Dir;
            if (remaining <= RestDist(stride) + 1f) BrakeNow(stride);
        }
        if (!w.Braking) w.Pace.Want = v;
        w.Pace.Accel = 2.2f * v;
        // go ROUND things: look ahead for a prop, the ice or another animal, and take a different lane if the way is shut
        w.PathT -= dt;
        if (w.Road != null && !w.Dyn)
        {
            // ON THE ROAD (planned before the walk began: see PlanRoad): the goal row is the road's row half a metre ahead, nothing to look for
            w.GY = RoadY(w.X + w.Dir * 0.4f * _m); w.StuckT = 0f;
        }
        else if (w.PathT <= 0f && !w.Braking)
        {
            w.PathT = 0.12f;
            float ahead = w.Gait == 1 ? 1.6f : 2.6f;
            float room = 0.5f * _m;
            // BACK TO THE PLANNED ROW: a detour round a prop moved the goal row and nothing ever moved it back (the wolf planned to stand at y 938
            // and stood at y 904, half behind a shrub). Whenever the planned row is clear ahead again, aim at it again.
            if (!w.Dyn && w.StopAtGoal && w.PlanY > 0f && MathF.Abs(w.GY - w.PlanY) > 1f && RowClear(w.X + w.Dir * 0.5f * _m, w.GX, w.PlanY)) w.GY = w.PlanY;
            bool shut = !LaneClear(w.X, w.Y, w.Dir, ahead, w.GY, room) || CrowdedW(w.X + w.Dir * ahead * 0.7f * _m, w.GY);
            if (shut && _log != null && w.StuckT == 0f)
            {
                string why = ""; for (int k = 1; k <= 6 && why == ""; k++) { float f = k / 6f, xx = w.X + w.Dir * ahead * _m * f, yy = w.Y + (w.GY - w.Y) * f; if (!Free(xx, yy, room)) why = $"not free at {xx:F0},{yy:F0}"; }
                Log($"WOLF path shut at {w.X:F0},{w.Y:F0} -> goal lane {w.GY:F0}: {(why == "" ? "crowded" : why)}");
            }
            if (shut)
            {
                float[] offs = [-0.05f, 0.05f, -0.10f, 0.10f, -0.16f, 0.16f, -0.24f, 0.24f];
                bool found = false;
                foreach (float o in offs)
                {
                    float ny = Math.Clamp(w.Y + o * S.U, _yLo, _yHi);
                    if (LaneClear(w.X, w.Y, w.Dir, ahead + 1f, ny, room) && !CrowdedW(w.X + w.Dir * ahead * 0.7f * _m, ny)) { w.GY = ny; found = true; break; }
                }
                if (!found) { w.StuckT += 0.12f; if (w.StuckT > 1.0f && w.Pace.Speed > 0f) { w.Pace.Want = 0f; } if (w.StuckT > 8f) { Log("WOLF is boxed in: it gives up"); _field.Stalked(-1); w.Plan.Clear(); w.Plan.Add(WS.Exit); w.Hare = -1; w.Dyn = false; w.StopAtGoal = false; w.RushAfter = false; w.GX = w.X + w.Dir * 99999f; w.StuckT = 0f; } }
                else w.StuckT = 0f;
            }
            else w.StuckT = 0f;
        }
        float step = w.Pace.Tick(dt);
        w.X += w.Dir * step;
        float dy = Math.Clamp(w.GY - w.Y, -0.35f * step, 0.35f * step); w.Y += dy;
        ShowW(WalkBook[w.Gait], w.Pace.Page(stride, WalkN[w.Gait]));
        // breath
        w.BreathT -= dt; if (w.BreathT <= 0f && w.Bk != null) { PointF nz = w.Bk.Aux[w.Pg]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 0f); w.BreathT = 2.6f + 1.2f * (float)Rng.NextDouble(); }
        if (!w.StopAtGoal && OffScreen(w.X)) { EndWolf(); return; }
        if (w.Braking && !w.Pace.Braking)
        {
            w.Braking = false;
            // THE STOP IS CHECKED WHERE THE WOLF REALLY STOPPED (before this the plan said "stand at 1169,938" and the wolf stopped at 1170,904 with its hind
            // legs behind a shrub and its muzzle over the bird table's roof). If this is not a place to stand, it walks on to one that is (it is on a rest
            // page, so setting off again is seamless).
            if (w.Arrive && w.StopAtGoal && !w.Dyn && w.Hare < 0)
            {
                w.Arrive = false;
                // AT MOST THREE TIMES: on film (2026-11-11) a wolf whose way to the better place was shut braked where it stood, asked again, and asked
                // 339 times in 5.7 seconds. After three it makes do with where it is.
                if (w.Reseeks++ < 3 && !StandOk(w.X, w.Y) && FindStand(w.X, w.Y, w.Dir, out PointF ns))
                {
                    Log($"WOLF stopped at {w.X:F0},{w.Y:F0}, no place to stand (covered, crowded or on ice): walks on to {ns.X:F0},{ns.Y:F0}");
                    w.GX = ns.X; w.GY = ns.Y; w.PlanY = ns.Y; w.Arrive = true; w.StuckT = 0f; w.PathT = 0f;
                    w.Pace.Halt(); w.Pace.Resume(0.3f * GaitV[w.Gait] * _m);
                    return;
                }
            }
            if (w.RushAfter && w.Hare >= 0 && w.PreyKind == 4)
            {
                w.RushAfter = false; w.Do = WD.Hold; w.T = 0f; w.Pace.Halt();
                PreyStalked(4, -1); Board.Post(Sense.Threat, new PointF(w.X, w.Y), 0.45f * S.U, 1f, this); w.PostT = 0.6f;
                return;
            }
            if (w.RushAfter && w.Hare >= 0)
            {
                // THE RUN-UP MUST BE LONG ENOUGH (a hare that hopped toward the wolf while it braked left 0.9 m: the gallop overshot by 6.7 m and the skid
                // stopped the wrong side of the cover). Too close: no rush; it settles back to a stand, thinks again, and picks again from where it is.
                float gap = MathF.Abs(PreyView(w.PreyKind)[w.Hare].Feet.X - w.X);
                if (gap < 3.0f * _m && w.PreyKind != 4)
                {
                    Log($"WOLF: the hare is only {gap / _m:F1} m off, no room to run: it settles and looks again");
                    PreyStalked(w.PreyKind, -1); w.Hare = -1; w.Dyn = false; w.RushAfter = false; w.Plan.Clear(); w.Plan.Insert(0, WS.Select);
                    StartSheet(SetBook[w.Gait], SetN[w.Gait], 0.55f, false, SA.Stand);
                    return;
                }
                // THE LINE IS CHECKED NOW, at the end of the creep (not when the hare was chosen): another hare still on the snow between the wolf and its
                // hare, or just beyond it, would be run through. The wolf holds its crouch for it to go (the Threat keeps frightening it).
                if (w.PreyKind == 3 && LineBlocked(w.Hare, w.X, w.Dir))
                {
                    w.Do = WD.Hold; w.T = 0f; w.Held = true; w.Pace.Halt(); w.HoldT = 0f;
                    Log("WOLF holds its crouch: another hare is on the line, it waits for it to go");
                    return;
                }
                BeginRush(); return;
            }
            StartSheet(SetBook[w.Gait], SetN[w.Gait], w.Gait == 1 ? 0.55f : 0.42f, false, SA.Stand);
            w.Do = WD.Sheet;
        }
    }

    /// <summary>How far to the next rest page. Pace.ToRestPage never answers less than a quarter stride (closer than that it takes one stride more), and when the wolf was
    /// walking to a place that was exactly right, that wrap made it overshoot by up to a whole stride (0.7 m, on to the pond's rim). A stop in an eighth of a stride is a
    /// firm one (a quarter second at a lope), which a wolf can do; a stop a stride late is a wolf standing on the ice.</summary>
    private float RestDist(float stride)
    {
        float d = stride - _w.Pace.Walked % stride;
        return d < 0.15f * stride ? d + stride : d;
    }
    private void BrakeNow(float stride)
    {
        var w = _w;
        w.Braking = true;
        // a walk that has only just set off is still ON its rest page: there is nothing to brake from (braking from a crawl takes for ever)
        if (w.Pace.Speed < 0.35f * GaitV[w.Gait] * _m && (w.Pace.Walked % stride) < 0.08f * stride) { w.Pace.Halt(); return; }
        if (w.Pace.Speed < 0.5f * GaitV[w.Gait] * _m) { w.Braking = false; return; }       // too slow to brake from: speed up first
        w.Pace.Brake(RestDist(stride));
    }

    private void StepSheet(float dt)
    {
        var w = _w;
        w.ST += dt;
        float u = Math.Clamp(w.ST / w.SDur, 0f, 1f);
        int pg = (int)MathF.Round(u * (w.SN - 1)); if (w.SRev) pg = w.SN - 1 - pg;
        ShowW(w.SBook, pg);
        // the sheet's own events
        if (w.SBook == PredatorsArt.WSniff && w.Bk != null)
        {
            if (!w.Flag1 && u > 0.34f) { w.Flag1 = true; PointF nz = w.Bk.Aux[w.Pg]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 1f); }
            if (!w.Flag2 && u > 0.58f) { w.Flag2 = true; PointF nz = w.Bk.Aux[w.Pg]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 1f); }
        }
        if (w.SBook == PredatorsArt.WCShake && w.Flag1 && u > 0.3f) { w.Flag1 = false; for (int k = 0; k < 14; k++) Emit(0, w.X + (float)(Rng.NextDouble() - 0.5) * 1.0f * _m, w.Y, 0.55f * _m, (float)(Rng.NextDouble() - 0.5) * 1.4f * _m, (0.2f + 0.8f * (float)Rng.NextDouble()) * 0.9f * _m, 0.7f, 0.8f, 2.2f * _m); }
        if (w.SBook == PredatorsArt.WDigOut && !w.Flag2 && u > 0.40f && w.Bk != null) { w.Flag2 = true; PointF nz = w.Bk.Aux[w.Pg]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 1f); for (int k = 0; k < 10; k++) Emit(0, w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 0f, w.Dir * (0.2f + (float)Rng.NextDouble() * 0.7f) * _m, (0.3f + (float)Rng.NextDouble() * 0.6f) * _m, 0.6f, 0.8f, 2f * _m); }
        if (w.SBook == PredatorsArt.WDigIn && u > 0.55f && !w.Flag2 && w.Cov is Cover cv0)
        {
            // the muzzle goes into the shrub and SNOW FALLS ON ITS HEAD: flakes let go from the cover's top, right over the muzzle
            w.Flag2 = true;
            float mx = w.X + w.Dir * 1.15f * _m;
            for (int k = 0; k < 14; k++) Emit(0, mx + ((float)Rng.NextDouble() - 0.5f) * 0.7f * _m, w.Y, cv0.Height * (0.55f + 0.4f * (float)Rng.NextDouble()), ((float)Rng.NextDouble() - 0.5f) * 0.2f * _m, 0f, 0.8f + 0.5f * (float)Rng.NextDouble(), 0.9f, 1.0f * _m);
        }
        if (w.ST < w.SDur) return;
        // the sheet is over: where next?
        if (w.SBook == PredatorsArt.WHowlIn) { Board.Post(Sense.Loud, new PointF(w.X, w.Y), 0.9f * S.U, 0.6f, this); Log("WOLF HOWLS: Loud pinned (reach 0.9 U, weight 0.6)"); w.Flag2 = false; StartLoop(PredatorsArt.WHowl, PredatorsArt.NHowl, 1.1f, 4.4f, SA.Next); w.DigEmit = 0f; return; }
        if (w.SBook == PredatorsArt.WRearIn) { StartLoop(PredatorsArt.WRearB, PredatorsArt.NRear, 1.3f, 4.2f + 2.4f * (float)Rng.NextDouble(), SA.Next); return; }
        if (w.SBook == PredatorsArt.WRearOut) { w.Rear = false; w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.4f; return; }
        if (w.SBook == PredatorsArt.WDigIn) { StartLoop(PredatorsArt.WDig, PredatorsArt.NDig, 0.9f, 3.4f, SA.Next); w.DigEmit = 0f; return; }
        if (w.SBook == PredatorsArt.WDig) { return; }
        if (w.SBook == PredatorsArt.WHowlOut) { w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.4f; w.After = SA.Next; return; }
        if (w.SBook == PredatorsArt.WDigOut) { w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.3f; return; }
        switch (w.After)
        {
            case SA.WalkNow:
                w.Do = WD.Walk; w.Pace.Halt(); w.Pace.Walked = 0f; w.Braking = false;
                w.Pace.Resume(0.05f * GaitV[w.Gait] * _m);
                break;
            case SA.Stand: w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.5f + 0.5f * (float)Rng.NextDouble(); w.Pace.Halt(); break;
            default: w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.0f; break;
        }
    }

    private void StepLoop(float dt)
    {
        var w = _w;
        w.ST += dt;
        ShowW(w.SBook, (int)(w.ST / w.SCycle * w.SN) % w.SN);
        if (w.Bk == null) return;
        if (w.SBook == PredatorsArt.WHowl)
        {
            w.DigEmit -= dt;
            if (w.DigEmit <= 0f) { PointF nz = w.Bk.Aux[w.Pg]; BreathAt(w.X + w.Dir * nz.X * _m, w.Y - nz.Y * _m, 1f); w.DigEmit = 0.55f; }
            if (w.ST >= w.SLoopFor) StartSheet(PredatorsArt.WHowlOut, PredatorsArt.NHowlOut, 0.85f, false, SA.Next);
        }
        else if (w.SBook == PredatorsArt.WRearB)
        {
            if (w.ST >= w.SLoopFor) StartSheet(PredatorsArt.WRearOut, PredatorsArt.NRearOut, 0.95f, false, SA.Next);
        }
        else if (w.SBook == PredatorsArt.WDig)
        {
            w.DigEmit -= dt;
            if (w.DigEmit <= 0f)
            {
                w.DigEmit = 0.13f;
                for (int k = 0; k < 4; k++) Emit(0, w.X - w.Dir * (0.1f + 0.2f * (float)Rng.NextDouble()) * _m, w.Y, 0.15f * _m, -w.Dir * (0.4f + 1.1f * (float)Rng.NextDouble()) * _m, (0.5f + 1.2f * (float)Rng.NextDouble()) * _m, 0.6f, 0.9f, 3.0f * _m);
            }
            if (w.ST >= w.SLoopFor) StartSheet(PredatorsArt.WDigOut, PredatorsArt.NDigOut, 0.95f, false, SA.Next);
        }
    }

    private void StepTurn(float dt)
    {
        var w = _w;
        const float dur = 1.5f;
        float u = w.T / dur;
        if (u < 0.5f) { ShowW(PredatorsArt.WTurn, (int)MathF.Round(u * 2f * (PredatorsArt.NTurn - 1))); w.Mir = w.Dir < 0; }
        else { if (w.Dir != w.NewDir) w.Dir = w.NewDir; ShowW(PredatorsArt.WTurn, PredatorsArt.NTurn - 1 - (int)MathF.Round((u - 0.5f) * 2f * (PredatorsArt.NTurn - 1))); w.Mir = w.Dir < 0; }
        if (w.T >= dur) { w.Dir = w.NewDir; w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.2f; }
    }

    // ---- the rush, the catch and the miss ----
    private void BeginRush()
    {
        var w = _w;
        w.Do = WD.RushIn; w.T = 0f; w.RushAfter = false; w.Dyn = false; w.MissPlanned = false;
        // a rush that means to miss runs for the COVER the hare will reach (it ends in a skid at the cover, whatever the hare does)
        if (w.PreyKind == 4) { int fp = _sq.PineOf(w.Hare); w.Cov = TreeCover(fp >= 0 ? fp : _sq.PineFor(w.Hare, w.X)); w.MissPlanned = true; }
        else if (!w.WantCatch) { w.Cov = NearestCover(PreyView(w.PreyKind)[w.Hare].Feet, out _); w.MissPlanned = w.Cov != null; }
        w.Pace.Halt(); w.Pace.Walked = 0f; w.Pace.Accel = GallopV * _m / 0.5f; w.Pace.Want = GallopV * _m; w.LastFrac = 0f;
        // the prey may notice now (the Threat below reaches it)... EXCEPT the hare of a rush that means to catch: it never saw it coming. It sits,
        // unaware, until the wolf is on it (every hare here sits a stride from a shrub: one that looked up at the first bound was behind its shrub
        // before the wolf had covered half the gap, so the wolf "only missed"). If the rush turns into a miss it notices at once (StepGallop).
        w.Held = false;
        if (!(w.WantCatch && w.PreyKind == 3)) PreyStalked(w.PreyKind, -1);
        Board.Post(Sense.Threat, new PointF(w.X, w.Y), 0.45f * S.U, 1f, this); w.PostT = 0.6f;
        Log($"WOLF RUSHES at hare{w.Hare} (gap {Math.Abs(PreyView(w.PreyKind)[w.Hare].Feet.X - w.X) / _m:F1} m)");
    }

    private void SteerGallop(float dt, float step, out float dxHare, out PointF aim)
    {
        var w = _w;
        var hv = PreyView(w.PreyKind)[w.Hare];
        // CUT THE CORNER: run to where the hare will be a third of a second from now, not where it is
        aim = new PointF(hv.Feet.X + hv.Vx * 0.33f, hv.Feet.Y + hv.Vy * 0.33f);
        if (!hv.Out && w.Cov is Cover cv) aim = cv.Hide;                              // the hare has gone behind cover: the wolf runs for the cover
        w.GY = Math.Clamp(aim.Y, _yLo - 6f, _yHi + 0.1f * S.U);
        float dy = Math.Clamp(w.GY - w.Y, -0.5f * step, 0.5f * step); w.Y += dy;
        dxHare = (aim.X - w.X) * w.Dir;
    }

    private void StepRushIn(float dt)
    {
        var w = _w;
        float step = w.Pace.Tick(dt); w.X += w.Dir * step;
        float u = Math.Clamp(w.T / 0.47f, 0f, 1f);
        ShowW(PredatorsArt.WRushIn, (int)MathF.Round(u * (PredatorsArt.NRushIn - 1)));
        SteerGallop(dt, step, out _, out _);
        if (w.T >= 0.47f) { w.Do = WD.Gallop; w.Pace.Walked = 0f; w.LastFrac = 0f; w.T = 0f; }
    }

    private void StepGallop(float dt)
    {
        var w = _w;
        w.Pace.Want = GallopV * _m; w.Pace.Accel = 40f * _m;
        // a squirrel is never run down: while it is still in the open the wolf keeps three metres behind it (and goes on to the trunk when it has gone up)
        if (w.PreyKind == 4 && w.Hare >= 0) { var sv0 = _sv[w.Hare]; float gap0 = (sv0.Feet.X - w.X) * w.Dir; if (sv0.Out && gap0 < 4.0f * _m) w.Pace.Want = MathF.Max(0.6f * _m, MathF.Min(GallopV * _m, sv0.Speed * 1.0f + (gap0 - 3.0f * _m))); }
        float step = w.Pace.Tick(dt); w.X += w.Dir * step;
        var hv = PreyView(w.PreyKind)[w.Hare];
        // THE HARE REACHES COVER: it is out of the open now (behind a shrub, a rock, a stump); the wolf remembers which cover
        if (w.PreyKind == 4 && _sq.PineOf(w.Hare) >= 0 && (w.Cov is not Cover tc || MathF.Abs(tc.Foot.X - S.Pines[_sq.PineOf(w.Hare)].X) > 2f)) w.Cov = TreeCover(_sq.PineOf(w.Hare));
        if (!hv.Out && !w.MissPlanned)
        {
            w.MissPlanned = true;
            w.Cov = _field.CoverOf(w.Hare) ?? NearestCover(hv.Feet, out _);
            Log($"WOLF: the hare is behind cover at {(w.Cov is Cover c0 ? c0.Foot.X : -1):F0} ({(w.Cov is Cover c1 ? c1.Kind.ToString() : "?")})");
        }
        SteerGallop(dt, step, out float dx, out PointF aim);
        // a rush that means to end at a piece of cover eases off in the last five metres (a 9 m/s gallop needs 2 m to stop; at 6.5 m/s the skid is a short one)
        if (w.MissPlanned && w.Cov is Cover cvs && (CoverStand(cvs, w.Dir).X - w.X) * w.Dir < 5.0f * _m) w.Pace.Want = 0.72f * GallopV * _m;
        float cyc = GallopC * _m;
        float frac = w.Pace.Walked / cyc; frac -= MathF.Floor(frac);
        ShowW(PredatorsArt.WGallopB, (int)(frac * PredatorsArt.NGallop) % PredatorsArt.NGallop);
        bool gate = frac < w.LastFrac - 0.5f; w.LastFrac = frac;
        NearestCover(hv.Feet, out float dCoverM);
        // THE CATCH: close enough, and the hare has not reached cover: the last bound
        // (a hare that is running needs 0.9 m of open snow left before its cover; one still sitting, unaware, is taken where it sits)
        // (the pounce begins where a straight slow-down from the gallop's speed to a third of it, in 0.3 s, lands exactly 1.18 m short of the hare:
        // about 1.8 m short of the contact point at full speed. A sitting, unaware hare is taken; a bolting one only with open snow left before its cover.)
        float dPounce = dx - 1.18f * _m;
        // A HARE THAT FROZE: a miss was planned (the hare was to run to its cover), but this one has not moved (the hares' own flight said "nowhere to run,
        // it freezes"). The wolf would gallop straight through it. So the miss becomes a catch: the hare's bad luck, and no body through a body.
        if (w.MissPlanned && w.PreyKind == 3 && hv.Out && !Bolting(hv) && dPounce <= 0.2f * w.Pace.Speed + 0.4f * _m && dx >= -0.3f * _m)
        { w.MissPlanned = false; w.WantCatch = true; Log("WOLF: the hare froze instead of running; the miss becomes a catch"); }
        if (hv.Out && dPounce <= 0.2f * w.Pace.Speed && dx >= -0.3f * _m && MathF.Abs(hv.Feet.Y - w.Y) < 0.10f * S.U && !w.MissPlanned && (Bolting(hv) || w.Pace.Speed > 0.8f * GallopV * _m) && (!Bolting(hv) || dCoverM > 0.9f)) { BeginPounce(aim); return; }
        if (dx < -1.0f * _m && hv.Out) w.MissPlanned = true;                  // it got past or away: no catch
        if (dx > 16f * _m) w.MissPlanned = true;
        if (w.MissPlanned) PreyStalked(w.PreyKind, -1);                       // no catch after all: the hare is free to notice and run
        if (w.MissPlanned && w.Cov == null) w.Cov = NearestCover(hv.Feet, out _);
        float remAtGate = 0f;
        if (w.MissPlanned && gate && w.Cov is Cover cvg) remAtGate = (CoverStand(cvg, w.Dir).X - w.X) * w.Dir;
        if (w.MissPlanned && gate && remAtGate <= (2.0f + GallopC) * _m)
        {
            // THE SKID: stop with the muzzle at the cover (the stand point), or if it is already too close, stop short and walk up
            Cover cv = w.Cov ?? new Cover(CoverKind.Rocks, new PointF(w.X + w.Dir * 4f * _m, w.Y + 10f), 1f * _m, 0.5f * _m, new PointF(w.X + w.Dir * 4f * _m, w.Y), 0.5f * _m, 0.4f * _m);
            PointF st = CoverStand(cv, w.Dir);
            float remaining = (st.X - w.X) * w.Dir;
            float D = MathF.Max(1.2f * _m, remaining);                 // (it was 2.0 m: a place 0.6 m ahead became a 2 m skid, 1.4 m PAST the cover, and the wolf had to turn round and walk to the cover's far side, at the screen's edge)
            w.Do = WD.Skid; w.T = 0f; w.SkidFrom = w.Pace.Walked; w.SkidD = D; w.Pace.Brake(D); w.Cov = cv;
            Log($"WOLF skids: {D / _m:F1} m to stop, its place at the cover is {remaining / _m:F1} m ahead");
        }
    }

    private void StepSkid(float dt)
    {
        var w = _w;
        float step = w.Pace.Tick(dt); w.X += w.Dir * step;
        float u = Math.Clamp((w.Pace.Walked - w.SkidFrom) / MathF.Max(1f, w.SkidD), 0f, 1f);
        ShowW(PredatorsArt.WSkid, (int)MathF.Round(u * (PredatorsArt.NSkid - 1)));
        // a spray of snow from the braced paws while it slides
        if (step > 0.3f * _m * dt * 2f && Rng.NextDouble() < 0.7) Emit(0, w.X + w.Dir * 0.5f * _m, w.Y, 0.05f * _m, w.Dir * (0.4f + (float)Rng.NextDouble()) * _m, (0.3f + (float)Rng.NextDouble() * 0.8f) * _m, 0.55f, 0.9f, 2.4f * _m);
        if (!w.Pace.Braking)
        {
            w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.8f; w.Pace.Halt();
            // then: to the cover (turning round if it has overshot), muzzle into it, root at its foot, the sneeze, a look about as if nobody saw, turn away and go
            Cover cv = w.Cov!.Value;
            int d2 = (CoverStand(cv, w.Dir).X - w.X) * w.Dir >= -0.3f * _m ? w.Dir : -w.Dir;
            w.Stand = CoverStand(cv, d2);
            w.Plan.Clear();
            if (d2 != w.Dir) { w.NewDir = d2; w.Plan.Add(WS.Turn); }
            if (MathF.Abs(w.Stand.X - w.X) > 0.3f * _m) { w.GX = w.Stand.X; w.GY = w.Stand.Y; w.StuckT = 0f; w.Plan.Add(WS.WalkTo); }
            if (w.PreyKind == 4) w.Plan.AddRange([WS.Rear, WS.Look]); else w.Plan.AddRange([WS.Dig, WS.Look]);
            w.Plan.AddRange([w.Stand.X > 0.5f * S.Width ? WS.FaceR : WS.FaceL, WS.StartWalk, WS.Exit]);       // out by the nearer edge (the right: the left half is the pond)
            w.Hole = w.Stand;
            _field.PredatorAt = new PointF(w.X, w.Y);
            Log($"WOLF stopped at {w.X:F0}; the {cv.Kind} is at {cv.Foot.X:F0},{cv.Foot.Y:F0}; it will stand at {w.Stand.X:F0} facing {(d2 > 0 ? "right" : "left")}");
        }
    }

    private void BeginPounce(PointF aim)
    {
        var w = _w;
        w.Do = WD.Pounce; w.T = 0f; w.Flag1 = false; w.Flag2 = false;
        w.PX0 = w.X; w.PY0 = w.Y; w.PV0 = w.Pace.Speed; w.PV1 = 0.35f * w.PV0;
        w.PXc = aim.X - w.Dir * 1.18f * _m; w.PYc = Math.Clamp(aim.Y, _yLo - 6f, _yHi + 0.1f * S.U);
        // A STRAIGHT SLOW-DOWN from the gallop's speed to a third of it, over exactly the distance to the contact point (the old cubic curve, started 1.4 m
        // short at 9 m/s, shot 22 px past its place and slid 12 px BACK: a wolf moonwalking in mid-pounce). Its time follows from the distance.
        float dist = MathF.Max(0.15f * _m, (w.PXc - w.X) * w.Dir);
        w.PTc = Math.Clamp(2f * dist / MathF.Max(1f, w.PV0 + w.PV1), 0.18f, 0.45f);
        w.PV1 = MathF.Max(0.05f * w.PV0, 2f * dist / w.PTc - w.PV0);              // (if the time had to be clamped, the end speed takes up the difference)
        w.Hole = new PointF(aim.X, aim.Y);
        Log($"WOLF POUNCES on hare{w.Hare} at {PreyView(w.PreyKind)[w.Hare].Feet.X:F0},{PreyView(w.PreyKind)[w.Hare].Feet.Y:F0}, contact at {aim.X:F0}");
    }

    private void StepPounce(float dt)
    {
        var w = _w;
        float t = w.T, tc = w.PTc;
        float x;
        float v1 = w.PV1 * w.Dir, v0 = w.PV0 * w.Dir;
        float xEnd = w.PX0 + 0.5f * (v0 + v1) * tc;                                   // where the slow-down ends (the contact place, up to the clamp above)
        if (t < tc)
        {
            // a steady slow-down from v0 to v1: the speed is continuous with the gallop's (no lurch) and never goes backwards
            x = w.PX0 + v0 * t - 0.5f * (v0 - v1) / tc * t * t;
            w.Y = Lerp(w.PY0, w.PYc, PredatorsArt.Sm(t / tc));
        }
        else
        {
            float s = Math.Clamp((t - tc) / 1.0f, 0f, 1f);
            x = xEnd + 0.5f * v1 * 1.0f * (1f - (1f - s) * (1f - s));
        }
        w.X = x;
        int pg = Math.Min(PredatorsArt.NPounce - 1, (int)(t * 30f));
        ShowW(PredatorsArt.WPounce, pg);
        // the cloud begins just before contact (it must be dense BEFORE the hare is taken)
        if (!w.Flag1 && t >= 0.20f) { w.Flag1 = true; StartCloud(w.Hole.X, w.Hole.Y, 0.85f); SnowBurst(w.Hole.X, w.Hole.Y, 16, 1.0f, 1.2f); }
        if (!w.Flag2 && t >= 0.31f)
        {
            w.Flag2 = true;
            if (_field.Seize(w.Hare))
            {
                _field.Restore(w.Hare, 60f + 60f * (float)Rng.NextDouble());
                w.Carrying = true;
                for (int k = 0; k < 5; k++) AddStain(w.Hole.X + (float)(Rng.NextDouble() - 0.5) * 1.6f * _m, w.Hole.Y + (float)(Rng.NextDouble() - 0.5) * 0.025f * S.U);       // (a flat scatter: a wide spread of x and a hair of y; it was 50 px of y, a vertical string of drops)
                Log($"WOLF CATCHES hare{w.Hare}: seized at {w.Hole.X:F0},{w.Hole.Y:F0}; it comes back in about a minute or two");
            }
            else { Log("WOLF: the hare could not be taken (the last ones are spared): it lets go"); }
        }
        if (t < 1.1f && Rng.NextDouble() < 0.5 && t > 0.3f) Emit(0, w.X + (float)(Rng.NextDouble() - 0.5) * 1.6f * _m, w.Y, 0.2f * _m, (float)(Rng.NextDouble() - 0.5) * _m, (0.3f + (float)Rng.NextDouble()) * 0.9f * _m, 0.7f, 0.8f, 2.2f * _m);
        if (t >= PredatorsArt.NPounce / 30f)
        {
            w.Do = WD.Stand; w.T = 0f; w.SLoopFor = 0.5f; w.Pace.Halt();
            _field.Stalked(-1);
            w.Plan.Clear();
            if (w.Carrying) w.Plan.AddRange([WS.CShake, WS.CLook, WS.StartWalk, WS.Exit]); else w.Plan.AddRange([WS.FaceR, WS.StartWalk, WS.Exit]);
        }
    }

    // ================================================================ THE OWL (see PredatorsOwl.cs for the logic; the draw is below)
    private void StartOwl(int kind, bool wantCatch) { if (!OwlStart(kind, wantCatch)) { _retryAt = _time + 1.5; _forced = false; _nextOwl = Math.Max(_nextOwl, _time + 6.0); } }
    private void UpdateOwl(float dt) => OwlUpdate(dt);

    // ================================================================ DRAW

    public override void Collect(DepthQueue q)
    {
        if (!_on || _wb == null) return;
        if (_w.On && _w.Bk != null) q.Add(_w.Y, this, 0);
        if (_o.On && _o.Bk != null) { q.Add(_o.GY - 0.6f, this, 3); q.Add(_o.H > 0.9f ? _o.GY + 5000f : _o.GY, this, 4); }
        if (_anyPts) q.Add(1e5f, this, 1);
        if (_stainAt > 0 || _stains[0].Born > 0) q.Add(-1f, this, 2);
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (!_on || _wb == null || _puff == null) return;
        long d0 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        switch (index)
        {
            case 0: DrawWolf(fb); break;
            case 1: DrawParticles(fb); break;
            case 2: DrawStains(fb); break;
            case 3: DrawOwlShadow(fb); break;
            case 4: DrawOwl(fb); break;
        }
        if (_log != null) _tickD += System.Diagnostics.Stopwatch.GetTimestamp() - d0;
    }

    private void DrawWolf(FrameBuffer fb)
    {
        var w = _w;
        if (w.Bk == null) return;
        bool mir = w.Do == WD.Turn ? w.Mir : w.Dir < 0;
        Sprite pg = w.Bk.Pages[0][w.Pg];
        S.Stamp(fb, pg, w.X, w.Y, w.Bk.Ox[0], w.Bk.Oy[0], mir);
    }

    private void DrawParticles(FrameBuffer fb)
    {
        byte near = S.Nearness(_w.On ? _w.Y : _o.GY);
        for (int i = 0; i < _pts.Length; i++)
        {
            ref Pt p = ref _pts[i];
            if (!p.On) continue;
            float k = p.T / p.Life;
            Sprite sp = _puff![Math.Clamp((int)p.Kind, 0, 5)];
            float op = p.Op * (1f - k) * (p.Kind == 3 ? MathF.Min(1f, k * 6f) * 0.8f : 1f);
            sp.DrawCentered(fb, p.X, p.Y - p.H, op, null, false, S.NearDepth, Math.Min(254, near + 6));
        }
        for (int i = 0; i < _clouds.Length; i++)
        {
            Cloud c = _clouds[i];
            if (!c.On) continue;
            float op = CloudOp(c); if (op <= 0.01f) continue;
            Sprite big = _puff![4], small = _puff[5];
            // six overlapping puffs make a billowing cloud: they climb and spread a little as it settles
            float rise = 0.2f * _m * c.Born, sp = (1f + 0.6f * c.Born) * c.R * _m;
            for (int k = 0; k < 9; k++)
            {
                float a = k * 0.698f + 0.5f, rr = (k % 2 == 0 ? 0.6f : 0.28f) * sp;
                Sprite s = c.R > 0.45f ? (k % 3 == 0 ? small : big) : small;
                s.DrawCentered(fb, c.X + MathF.Cos(a) * rr, c.Y - (0.30f * _m + rise) + MathF.Sin(a) * rr * 0.45f, op * 0.95f, null, false, S.NearDepth, Math.Min(254, S.Nearness(c.Y) + 6));
            }
        }
    }

    private void DrawStains(FrameBuffer fb)
    {
        if (_blood == null) return;
        for (int i = 0; i < _stains.Length; i++)
        {
            Stain s = _stains[i]; if (s.Born <= 0 && s.X == 0) continue;
            double age = _time - s.Born; float life = 150f;
            if (age >= life) continue;
            float op = (float)(age < 0.4 * life ? 1.0 : 1.0 - (age - 0.4 * life) / (0.6 * life));
            _blood.DrawCentered(fb, s.X, s.Y, op * 0.9f, null, false, S.NearDepth, S.Nearness(s.Y));
        }
    }
}
