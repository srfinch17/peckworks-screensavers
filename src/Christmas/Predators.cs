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
    private enum WS { WalkTo, Exit, Sniff, Look, Select, Turn, StartWalk, Stalk, Howl, Dig, CShake, CLook, Idle, Rear }
    private enum SA { Next, Stand, WalkNow, RushGo }

    private static readonly int[] WalkBook = [PredatorsArt.WLopeB, PredatorsArt.WStalkB, PredatorsArt.WCarryB];
    private static readonly int[] SetBook = [PredatorsArt.WLopeSet, PredatorsArt.WStalkSet, PredatorsArt.WCarrySet];
    private static readonly int[] WalkN = [PredatorsArt.NLope, PredatorsArt.NStalk, PredatorsArt.NCarry];
    private static readonly int[] SetN = [PredatorsArt.NLopeSet, PredatorsArt.NStalkSet, PredatorsArt.NCarrySet];
    private static readonly float[] GaitC = [PredatorsArt.Lope.C, PredatorsArt.Stalk.C, PredatorsArt.CarryG.C];
    private static readonly float[] GaitV = [1.30f, 0.60f, 1.75f];            // metres a second
    private const float GallopC = 3.1f, GallopV = 9.0f;                       // metres a cycle, metres a second
    private static readonly bool WolfReady = false;                           // see MaybeStart: the wolf visits on its own only once its hunt has passed on film

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
        public float PX0, PY0, PXc, PYc, PV0, PTc; public int BurrowK = -1; public PointF Hole; public float SkidFrom, SkidD; public float RushGap;
        public Cover? Cov; public PointF Stand; public float DigEmit; public string Log = "";
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
        // ponytail: THE WOLF STAYS HOME in a natural run until its hunt has passed on film (2026-11-11: on the cover scene it stood half behind a shrub,
        // refused every pair of hares, and its walk crossed sitting hares). The test dials still force every wolf visit. Set WolfReady to true to let it out.
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
            bool want = Rng.NextDouble() < 0.67;
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
    private bool LaneClear(float x, float y, int dir, float aheadM, float toY, float room)
    {
        for (int k = 1; k <= 6; k++)
        {
            float f = k / 6f, xx = x + dir * aheadM * _m * f, yy = y + (toY - y) * f;
            if (xx < -4f * _m || xx > S.Width + 4f * _m) continue;                            // off screen: nothing to hit
            if (!Free(xx, yy, room)) return false;
        }
        return true;
    }

    // ================================================================ THE WOLF

    private void ShowW(int book, int page) { if (_wb == null) return; _w.Bk = _wb[book]; _w.Pg = Math.Clamp(page, 0, _w.Bk.Count - 1); }

    private void StartSheet(int book, int n, float dur, bool rev, SA after) { var w = _w; w.Do = WD.Sheet; w.T = 0f; w.SBook = book; w.SN = n; w.SDur = dur; w.ST = 0f; w.SRev = rev; w.After = after; ShowW(book, rev ? n - 1 : 0); }
    private void StartLoop(int book, int n, float cycle, float total, SA after) { var w = _w; w.Do = WD.Loop; w.T = 0f; w.SBook = book; w.SN = n; w.SCycle = cycle; w.SLoopFor = total; w.ST = 0f; w.After = after; ShowW(book, 0); }

    private void StartWolf(int kind, bool wantCatch)
    {
        var w = _w;
        w.On = true; w.Kind = kind; w.PreyKind = kind == 3 ? 4 : 3; w.Rear = false; w.WantCatch = kind == 3 ? false : wantCatch; w.Carrying = false; w.Hare = -1; w.Tries = 0; w.Plan.Clear(); w.MissPlanned = false; w.Cov = null;
        float W = S.Width;
        // the side it comes in from: toward the hares' side for a hunt; for a howl, from the right so it faces the moon
        float avg = 0.7f; int hn = _field.HareCount, cnt = 0; float sum = 0;
        for (int i = 0; i < hn; i++) { sum += _field.FeetOf(i).X; cnt++; }
        if (cnt > 0) avg = sum / cnt / W;
        int dir = kind == 2 ? -1 : avg > 0.5f ? 1 : -1;
        if (kind == 1 && Rng.Next(2) == 0) dir = -dir;
        // A MISS is the hare reaching cover: so a hunt that means to miss starts by finding a hare with cover a few metres BEYOND it, and comes in from the side
        // that puts the wolf, the hare and the cover in a line (the hare runs away from the wolf, into the cover)
        w.PreHare = -1; float preGX = -1f, preGY = -1f;
        if (_log != null) for (int i = 0; i < _hv.Length; i++) { Cover? c0 = NearestCover(_hv[i].Feet, out float d0); Log($"hare{i} at {_hv[i].Feet.X:F0},{_hv[i].Feet.Y:F0} out {_hv[i].Out} speed {_hv[i].Speed:F0}, nearest cover {(c0 is Cover cc ? cc.Kind + " at " + cc.Foot.X.ToString("F0") + "," + cc.Foot.Y.ToString("F0") : "none")} {d0:F1} m (yLo {_yLo:F0} yHi {_yHi:F0})"); }
        if (kind == 0 && !wantCatch)
        {
            float bestRun = 0f, laneY = Math.Clamp(S.FieldPoint(0.5f * W, 0.26f).Y, _yLo, _yHi);
            for (int i = 0; i < _hv.Length; i++)
            {
                PV v = _hv[i];
                if (!Still(v) || v.Feet.Y < _yLo - 2f) continue;
                Cover? cv = NearestCover(v.Feet, out float dm);
                if (cv == null || dm < 0.3f || dm > 7f || !RunIceFree(v.Feet.X - (cv.Value.Hide.X > v.Feet.X ? 1 : -1) * 12f * _m, cv.Value.Hide.X, v.Feet.Y)) continue;
                int d = cv.Value.Hide.X > v.Feet.X ? 1 : -1; float gx = Math.Clamp(v.Feet.X - d * 11f * _m, 0.06f * W, 0.94f * W);
                for (int t = 0; t < 60 && (!Free(gx, laneY, 0.5f * _m) || Covered(gx, laneY, 1.0f * _m, 1.0f * _m)); t++) gx += d * 0.4f * _m;      // where it can really stand and sniff
                float run = (v.Feet.X - gx) * d;
                if (run < 8.0f * _m || run <= bestRun) continue;                                                                                // the longest run-up
                bestRun = run; w.PreHare = i; dir = d; preGX = gx;
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
                NearestCover(v.Feet, out float dm);
                for (int d = -1; d <= 1; d += 2)                 // d: the way the wolf will run (and the way the hare would bolt)
                {
                    float far = (d > 0 ? W - v.Feet.X : v.Feet.X) / _m;
                    if (far < 3.0f) continue;                                       // the end must be on screen
                    // WHERE IT WILL STAND: a free place 4.8 to 11 metres short of the hare (8 is best: room to creep, then the rush), on the hare's own
                    // row or the tree-line lane, with no ice between. The pond takes the middle of the field, so the places are few: it tries them all.
                    float hareRow = Math.Clamp(v.Feet.Y, _yLo, _yHi), gx = 0f, gy = 0f, run = 0f;
                    for (int r = 0; r < 2 && run == 0f; r++)
                    {
                        float row = r == 0 ? hareRow : laneY;
                        for (int k = 0; k < 13 && run == 0f; k++)
                        {
                            float back = k < 7 ? 8f + 0.5f * k : 8f - 0.5f * (k - 6);      // 8, 8.5 ... 11, then 7.5, 7 ... 4.8
                            if (back < 4.8f) back = 4.8f;
                            float x = v.Feet.X - d * back * _m;
                            if (x < 0.06f * W || x > 0.94f * W || !Free(x, row, 0.5f * _m) || Covered(x, row, 1.0f * _m, 1.0f * _m)) continue;
                            if (!RunIceFree(x, v.Feet.X - d * 4.6f * _m, row) || !RunIceFree(v.Feet.X - d * 4.6f * _m, v.Feet.X + d * 3f * _m, v.Feet.Y)) continue;
                            gx = x; gy = row; run = back;
                        }
                    }
                    if (_log != null) Log($"  catch plan: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0} from the {(d > 0 ? "left" : "right")}: far side {far:F1} m, cover {dm:F1} m, {(run > 0f ? $"stand at {gx:F0},{gy:F0}, {run:F1} m back" : "no free place to stand")}");
                    if (run == 0f) continue;
                    float score = dm + 0.1f * run;
                    if (score > bestScore) { bestScore = score; w.PreHare = i; dir = d; preGX = gx; preGY = gy; }
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
        w.Dir = dir; w.X = dir > 0 ? -2.2f * _m : W + 2.2f * _m;
        w.Y = Math.Clamp(S.FieldPoint(0.5f * W, 0.24f + 0.04f * (float)Rng.NextDouble()).Y, _yLo, _yHi); w.GY = w.Y;
        w.Gait = 0; w.Pace.Halt(); w.Pace.Walked = 0f; w.Pace.Resume(GaitV[0] * _m * 0.6f);
        w.PostT = 0f; w.MoveT = 0f; w.BreathT = 2f; w.Do = WD.Walk; w.StopAtGoal = true; w.Dyn = false; w.Braking = false;
        switch (kind)
        {
            case 0: case 3:
                w.GX = preGX > 0f ? preGX : Math.Clamp(dir > 0 ? Rng.Next(25, 42) / 100f * W : Rng.Next(58, 75) / 100f * W, 0.1f * W, 0.9f * W);
                w.Plan.AddRange([WS.Sniff, WS.Look, WS.Select]); break;
            case 1:
                w.GX = Rng.Next(32, 68) / 100f * W;
                w.Plan.AddRange([WS.Sniff, WS.Look, WS.StartWalk, WS.Exit]); break;
            default:
                w.GX = Rng.Next(14, 30) / 100f * W;
                w.Plan.AddRange([WS.Howl, WS.Look, WS.StartWalk, WS.Exit]); break;
        }
        // the lane must be free where it stops (a planned catch has already found its free place, and its row: the walk drifts onto that row)
        if (preGY > 0f) w.GY = preGY;
        else for (int tries = 0; tries < 40 && (!Free(w.GX, w.Y, 0.5f * _m) || Covered(w.GX, w.Y, 1.0f * _m, 1.0f * _m)); tries++) w.GX += dir * 0.4f * _m;
        ShowW(PredatorsArt.WLopeB, 0); w.Mir = dir < 0;
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
            case WS.StartWalk: w.Gait = w.Carrying ? 2 : 0; BeginWalkFromStand(); break;
            case WS.Select: SelectTarget(); break;
            case WS.Stalk: w.Gait = 1; w.Dyn = true; w.StopAtGoal = true; w.RushAfter = true; BeginWalkFromStand(); break;
            case WS.Howl: StartSheet(PredatorsArt.WHowlIn, PredatorsArt.NHowlIn, 0.85f, false, SA.Next); w.Flag1 = true; w.ST = 0f; break;
            case WS.Rear: w.Rear = true; StartSheet(PredatorsArt.WRearIn, PredatorsArt.NRearIn, 0.95f, false, SA.Next); break;
            case WS.Dig: StartSheet(PredatorsArt.WDigIn, PredatorsArt.NDigIn, 0.65f, false, SA.Next); w.Flag1 = true; w.Flag2 = false; break;
            case WS.CShake: StartSheet(PredatorsArt.WCShake, PredatorsArt.NCShake, 0.85f, false, SA.Next); w.Flag1 = true; break;
            case WS.CLook: StartSheet(PredatorsArt.WCLook, PredatorsArt.NCLook, 1.3f, false, SA.Next); break;
            case WS.WalkTo: w.Gait = 0; w.StopAtGoal = true; w.Dyn = false; BeginWalkFromStand(); break;
            case WS.Exit:
                w.GX = w.Dir > 0 ? S.Width + 3.5f * _m : -3.5f * _m; w.GY = w.Y; w.StopAtGoal = false; w.Dyn = false; w.RushAfter = false;
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
        for (int i = 0; i < _hv.Length; i++)
        {
            var v = _hv[i];
            if (_log != null && false) Log($"  select: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0} out {v.Out} speed {v.Speed:F0} still {Still(v)}; wolf {w.X:F0}; rows {_yLo:F0}-{_yHi:F0}");
            if (!Still(v) || v.Feet.Y < _yLo - 2f || v.Feet.Y > _yHi + 0.05f * S.U) continue;
            if (MathF.Abs(v.Feet.X - w.X) < 3.6f * _m) continue;                         // not a hare right under its nose: there would be no run-up
            Cover? cv = NearestCover(v.Feet, out float dCoverM);
            // never across the ice: the wolf walks along ITS OWN row to within four and a half metres of the hare, then changes to the hare's row for the last part
            int toward = v.Feet.X > w.X ? 1 : -1;
            // THE HUNT ENDS ON SCREEN: a hare with under three metres of snow between it and the screen's edge on its far side is left alone (one such
            // chase ran off the side: the "catch" was never seen, and took nothing, since a hare off screen is not in the open)
            float farM = (toward > 0 ? S.Width - v.Feet.X : v.Feet.X) / _m;
            if (_log != null) Log($"  select: hare{i} at {v.Feet.X:F0},{v.Feet.Y:F0}, wolf at {w.X:F0}: gap {MathF.Abs(v.Feet.X - w.X) / _m:F1} m, far side {farM:F1} m, cover {dCoverM:F1} m");
            if (farM < 3.0f) continue;
            if (!RunIceFree(w.X, v.Feet.X - toward * 4.6f * _m, w.Y) || !RunIceFree(v.Feet.X - toward * 4.6f * _m, v.Feet.X + toward * 3f * _m, v.Feet.Y)) continue;
            // NO BODY THROUGH A BODY: another hare standing between the wolf and this one (on the same rows) is in the way of the last bound, so this one is not chosen
            bool inWay = false;
            for (int j = 0; j < _hv.Length; j++)
            {
                if (j == i || !_hv[j].Out) continue;
                float jx = _hv[j].Feet.X;
                if ((jx - w.X) * toward > 0f && (v.Feet.X + toward * 1.2f * _m - jx) * toward > 0f && MathF.Abs(_hv[j].Feet.Y - v.Feet.Y) < 0.07f * S.U) inWay = true;
            }
            if (inWay) continue;
            float score;
            if (w.WantCatch)
            {
                // a catch wants a hare far from any cover: one a stride from a bush cannot be caught from this far (it was the reason the wolf "only missed").
                // After three idle looks it takes what there is, and the outcome is honest (the hare reaches the cover and the wolf skids at it).
                // (It was twelve looks, a second each: on a field where every hare sits by a shrub the wolf stood like a statue for a quarter of a minute.)
                if (dCoverM < 1.3f && w.Tries < 3) continue;
                score = dCoverM;
            }
            else
            {
                // a miss wants cover a few metres off, BEYOND the hare (the wolf, the hare and the cover in a line: the hare runs away from the wolf, into it)
                if (cv == null || dCoverM < 0.3f || dCoverM > 8.0f || (cv.Value.Hide.X - v.Feet.X) * (v.Feet.X - w.X) <= 0f) continue;
                score = -MathF.Abs(dCoverM - 3.0f);
            }
            score -= 0.002f * MathF.Abs(v.Feet.X - w.X) / _m;
            if (i == w.PreHare) score += 100f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0)
        {
            if (!w.WantCatch && w.Tries == 3) { w.WantCatch = true; Log("WOLF: no hare has cover beyond it; it hunts for the catch instead"); }
            if (++w.Tries > 8) { Log("WOLF finds no hare to hunt: it walks off"); w.Plan.Clear(); w.Plan.AddRange([WS.StartWalk, WS.Exit]); }
            else { w.Plan.Insert(0, WS.Select); w.Plan.Insert(0, WS.Idle); }
            NextW(); return;
        }
        var hv = _hv[best];
        NearestCover(hv.Feet, out float dm);
        // an honest outcome: a hare that sits a stride from a shrub cannot be caught from this far; one in the open usually can
        w.Hare = best; w.RushGap = w.WantCatch ? 4.4f : 5.9f;
        if ((!w.WantCatch || dm < 3.0f) && NearestCover(hv.Feet, out _) is Cover cv9)
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
        bool hunting = w.Do is WD.RushIn or WD.Gallop or WD.Pounce || (w.Do == WD.Walk && w.Gait == 1) || (w.Carrying && w.Do != WD.Off) || w.Do == WD.Skid || w.Rear;
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
                ShowW(PredatorsArt.WStalkB, 0);
                if (_sq.PineOf(w.Hare) >= 0 || w.T > 2.2f) BeginRush();
                break;
        }
        if (_log != null && w.Bk != null)
            _log.WriteLine($"{_time,8:F2} predators wolf {w.X:F1} {w.Y:F1} {w.Pace.Speed:F1} {w.Do} {w.Bk.Name} {w.Pg}");
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

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
            if (remaining <= w.Pace.ToRestPage(stride) + 1f) BrakeNow(stride);
        }
        if (!w.Braking) w.Pace.Want = v;
        w.Pace.Accel = 2.2f * v;
        // go ROUND things: look ahead for a prop, the ice or another animal, and take a different lane if the way is shut
        w.PathT -= dt;
        if (w.PathT <= 0f && !w.Braking)
        {
            w.PathT = 0.12f;
            float ahead = w.Gait == 1 ? 1.6f : 2.6f;
            float room = 0.5f * _m;
            bool shut = !LaneClear(w.X, w.Y, w.Dir, ahead, w.GY, room) || S.Crowded(w.X + w.Dir * ahead * 0.7f * _m, w.GY, 0.7f * _m, this, 0);
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
                    if (LaneClear(w.X, w.Y, w.Dir, ahead + 1f, ny, room) && !S.Crowded(w.X + w.Dir * ahead * 0.7f * _m, ny, 0.7f * _m, this, 0)) { w.GY = ny; found = true; break; }
                }
                if (!found) { w.StuckT += 0.12f; if (w.StuckT > 1.0f && w.Pace.Speed > 0f) { w.Pace.Want = 0f; } if (w.StuckT > 8f) { Log("WOLF is boxed in: it gives up"); _field.Stalked(-1); w.Plan.Clear(); w.Plan.Add(WS.Exit); w.Hare = -1; w.Dyn = false; w.GX = w.X + w.Dir * 99999f; w.StuckT = 0f; } }
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
                BeginRush(); return;
            }
            StartSheet(SetBook[w.Gait], SetN[w.Gait], w.Gait == 1 ? 0.55f : 0.42f, false, SA.Stand);
            w.Do = WD.Sheet;
        }
    }

    private void BrakeNow(float stride)
    {
        var w = _w;
        w.Braking = true;
        // a walk that has only just set off is still ON its rest page: there is nothing to brake from (braking from a crawl takes for ever)
        if (w.Pace.Speed < 0.35f * GaitV[w.Gait] * _m && (w.Pace.Walked % stride) < 0.08f * stride) { w.Pace.Halt(); return; }
        if (w.Pace.Speed < 0.5f * GaitV[w.Gait] * _m) { w.Braking = false; return; }       // too slow to brake from: speed up first
        w.Pace.Brake(w.Pace.ToRestPage(stride));
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
        float cyc = GallopC * _m;
        float frac = w.Pace.Walked / cyc; frac -= MathF.Floor(frac);
        ShowW(PredatorsArt.WGallopB, (int)(frac * PredatorsArt.NGallop) % PredatorsArt.NGallop);
        bool gate = frac < w.LastFrac - 0.5f; w.LastFrac = frac;
        NearestCover(hv.Feet, out float dCoverM);
        // THE CATCH: close enough, and the hare has not reached cover: the last bound
        // (a hare that is running needs 0.9 m of open snow left before its cover; one still sitting, unaware, is taken where it sits)
        if (hv.Out && dx <= 2.7f * _m && dx >= -0.3f * _m && MathF.Abs(hv.Feet.Y - w.Y) < 0.10f * S.U && !w.MissPlanned && (Bolting(hv) ? dCoverM > 0.9f : dx < 1.6f * _m)) { BeginPounce(aim); return; }
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
            float D = MathF.Max(2.0f * _m, remaining);
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
            w.NewDir = -d2; w.Plan.AddRange([WS.Turn, WS.StartWalk, WS.Exit]);
            w.Hole = w.Stand;
            _field.PredatorAt = new PointF(w.X, w.Y);
            Log($"WOLF stopped at {w.X:F0}; the {cv.Kind} is at {cv.Foot.X:F0},{cv.Foot.Y:F0}; it will stand at {w.Stand.X:F0} facing {(d2 > 0 ? "right" : "left")}");
        }
    }

    private void BeginPounce(PointF aim)
    {
        var w = _w;
        w.Do = WD.Pounce; w.T = 0f; w.Flag1 = false; w.Flag2 = false;
        w.PX0 = w.X; w.PY0 = w.Y; w.PV0 = w.Pace.Speed; w.PTc = 0.30f;
        w.PXc = aim.X - w.Dir * 1.18f * _m; w.PYc = Math.Clamp(aim.Y, _yLo - 6f, _yHi + 0.1f * S.U);
        w.Hole = new PointF(aim.X, aim.Y);
        Log($"WOLF POUNCES on hare{w.Hare} at {PreyView(w.PreyKind)[w.Hare].Feet.X:F0},{PreyView(w.PreyKind)[w.Hare].Feet.Y:F0}, contact at {aim.X:F0}");
    }

    private void StepPounce(float dt)
    {
        var w = _w;
        float t = w.T, tc = w.PTc;
        float x;
        float v1 = 0.35f * w.PV0 * w.Dir, v0 = w.PV0 * w.Dir;
        if (t < tc)
        {
            // cubic Hermite from (x0, v0) to (xc, v1): the speed is continuous with the gallop's, so there is no lurch
            float s = t / tc, h00 = 2 * s * s * s - 3 * s * s + 1, h10 = s * s * s - 2 * s * s + s, h01 = -2 * s * s * s + 3 * s * s, h11 = s * s * s - s * s;
            x = h00 * w.PX0 + h10 * tc * v0 + h01 * w.PXc + h11 * tc * v1;
            w.Y = Lerp(w.PY0, w.PYc, PredatorsArt.Sm(s));
        }
        else
        {
            float s = Math.Clamp((t - tc) / 1.0f, 0f, 1f);
            x = w.PXc + 0.5f * v1 * 1.0f * (1f - (1f - s) * (1f - s));
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
                for (int k = 0; k < 5; k++) AddStain(w.Hole.X + (float)(Rng.NextDouble() - 0.5) * 0.9f * _m, w.Hole.Y + (float)(Rng.NextDouble() - 0.4) * 0.12f * S.U);
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
            if (w.Carrying) w.Plan.AddRange([WS.CShake, WS.CLook, WS.StartWalk, WS.Exit]); else w.Plan.AddRange([WS.StartWalk, WS.Exit]);
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
