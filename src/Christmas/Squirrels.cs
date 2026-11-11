using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Animals;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

/// <summary>
/// THE RED SQUIRRELS OF THE PINES (2026-11-10, the owner: "also we did all that squirrel work, but you didnt put any
/// squirrels in there. use all that knowledge you learned about animating that stuff to put them in here. that was the
/// whole point of that animal library.").
///
/// FEYNMAN VERSION: the squirrel is the SHARED ANIMAL LIBRARY's (Core/Animals/Squirrel.cs, the one Thanksgiving's
/// owner approved after many rounds) in a winter coat; this file is the CHRISTMAS BRAIN that gives it a night. Two or
/// so of them LIVE IN THE PINES. Each comes down a pine's trunk head first, spiralling round it (in front, then behind
/// the 7 pixel trunk where the bark hides its body and only the tail shows past the edge, then in front again), bends
/// level, hops off onto the snow, and bounds away. Going home it does the same backwards and goes up INTO the
/// boughs: the pine's lowest boughs hide it from the top down (it passes BEHIND them, nothing fades), the bough it
/// enters dips and sheds a little snow, and it is gone. On the snow it bounds with the pulse the library's pages
/// carry, sits up and looks round, flicks its tail, curls round on a reversal, goes ROUND the painted cover (shrubs,
/// boulders, the stump) and never through it; a stump's snow cushion or a boulder's cap is a perch where one hops
/// up to sit and eat a pine cone. It CACHES: carries a cone off, scrapes a shallow pale dish in the snow, buries it
/// and pats it down; another that watched goes and digs it up, and the owner chases the thief up the nearest pine.
/// It RAIDS THE BIRD TABLE: bounds to the post, climbs it (the bending mount), pulls itself over onto the tray and
/// sits there stuffing its cheeks with seed while the birds scatter. It runs along the fallen log's snow cap. At a
/// Threat every squirrel runs to the nearest pine and up it in a spiral, then scolds from the trunk until the
/// threat has gone. A squirrel NEVER goes into the ground: its escape is always a pine.
///
/// THE TRANSITION TABLE (from to; every cell is a drawn sheet, a shared pose or "cannot"):
///   Den     to Exit     : a bough shakes (the dip), the head and tail come out behind the boughs (the cone stencil)
///   Exit    to Descend  : the same climb pages run backwards, head first, the spiral the other way, the bark hides it
///   Descend to Dismount : the library's MountDown (the front half bends level toward the snow, the hind end follows)
///   Dismount to Hop     : the mount's last page is the hop's first (a level crouch): one hop down off the foot of the trunk
///   Hop     to Sit      : the hop's settle page is the sit
///   Sit     to Go       : the Rise pages (the sit sinks into the crouch the bound begins from, 9 frames)
///   Go      to Sit      : the pace brakes into the goal, the Rise pages stand it up
///   Go      to Go back  : the library's Curl pages (front end plants, back end comes round, tail whips over)
///   Sit     to Sit away : the library's Turn pages (profile, three-quarters, front, mirrored)
///   Sit     to Look     : four Look pages (the head rises); back the same way
///   Sit     to Dig/Eat/Stuff : the paws come up from the thighs in one page (the library's own sit and eat poses meet there)
///   Go      to Mount    : the bound's last bound, a brake onto the rest page, MountUp (bend, then turn up the bark)
///   Mount   to Climb    : MountUp's last page IS the climb's first page (measured: the same cling pose)
///   Climb   to Scold    : the scold's first page IS a climb page (the cling pose), the tail begins to lash
///   Climb   to Den      : the cone stencil hides the squirrel from the top down; Den is nothing drawn
///   Climb   to TrayOver : MountUp played BACKWARDS (upright to level), the feet carried across to the tray
///   TrayOver to Stuff   : the mount's level crouch, then the paws come up with the seed
///   Stuff   to Hop      : the stuff pose is a sit; the Hop pages begin from a sit (crouch, push, flight, land)
///   Hop     to Go       : the hop's last page is a sit; Sit then Go as above
///   any ground state to Flee : two to four frames of the Rise crouch (anticipation) then the bound; a squirrel on a
///                          perch, the tray or the log hops down first (Hop), a squirrel in a pine goes on up it
///
/// WHAT IT PINS: a squirrel bounding in the open pins Movement (reach 0.12 U, weight 0.45, once per half second for
/// a 2.5 s bout, then a 10 s rest), so hares, mice and birds within 0.12 U of one may glance (through their own
/// Check: the calm board rations it); scolding from a trunk pins Call (reach 0.30 U, weight 0.35, a bout of 3 s every
/// 12 s at most); fleeing pins nothing. Who is in reach: a post at the pines reaches whatever stands in the far
/// band (the reindeer, the pine birds); at the table it reaches the birds on the tray and the hares and mice that come
/// to the table; at the log it reaches the mice's door.
///
/// TEST VARIABLES: PECKWORKS_SQUIRRELS=raid|cache|chase|flee|home|log|perch|enter forces that doing at a known second
/// (the first squirrel; chase and cache use the second as the partner); PECKWORKS_SQUIRRELN=n overrides the count;
/// PECKWORKS_SHEET=squirrels writes review\sheet_squirrels.png (the pose sheet); PECKWORKS_MINDLOG logs a line per squirrel per frame and
/// every roll.
/// </summary>
internal sealed class Squirrels : Resident
{
    // ============================================================ what a squirrel does
    // EPISODES (a "doing" in the targets: a few seconds to half a minute of one purpose) and the STEPS inside them.
    // Interrupters of an episode: a Threat or Freeze-grade notice (Flee), a story (Lull: only quiet doings), the
    // timer. Each step leaves the squirrel in a pose the next begins from (the table above).
    private enum Doing { Entrance, Roam, Cache, Retrieve, Raid, LogRun, Perch, Home, Flee, Chase }
    private enum St { Den, Sit, Go, Dig, Bury, Eat, Mount, Climb, Exit, Descend, Dismount, Hop, Scold, TrayOver, Stuff }

    private struct Item { public St Kind; public float X, Y, T; public int Arg; }

    private struct Frame { public Sprite? Page; public int W, Ox, By; public float OX, OY; public bool Mirror; public int Near; public bool Cone, Trunk; public int PineIx; }

    private sealed class Sq
    {
        public int Index;
        public float Timid, Busy, Curious, Playful;
        public Attention Senses = null!;
        public Mind<St> Step = new(St.Den);
        public Doing Ep; public float EpT;
        public float X, Y; public int Dir = 1; public float Heading;
        public Pace Pace = null!;
        public float Phase, Clock;
        public int Level = 1, LevelFrom = 1; public float LevelBlend;
        public PointF Goal; public float Arrive; public bool Dash; public int Refused, Curls;
        public readonly Item[] Q = new Item[12]; public int QN;
        public int Carry;                              // 0 nothing, 1 a pine cone
        public bool Turning, Curl, WasDown; public float TurnT, TurnPre, TurnPost, TurnV;
        public float Blend; public bool Sinking;
        public float Up, UpTo;                         // how far up a trunk or post (pixels), and how far this climb goes
        public int Pine = -1;                          // the pine it is on or going to (-2 the bird table's post)
        public PointF HopFrom, HopTo; public float HopLift;
        public int NearOv = -1;                        // a nearness to stamp with when it is not the feet's own (on a perch, the tray, mid-hop)
        public float LookAmt, LookT, NextLook;
        public float PostBout, PostRest;
        public int Witness = -1; public float WitnessAt;
        public int Mate = -1; public int Legs;
        public Frame Show, ShowOld; public bool Visible;
        public double NextEpisode;
        public float Seconds;                          // its age since it began, for the log
        public int Cover = -1;                         // the cover it perches on
        public bool OnTray;
        public float ScoldFor;
        public float NextScold;
        public int RetrieveIx = -1;                    // the cache this squirrel is on its way to dig up
        public int HopArg;                             // what the hop in progress is: 0 a plain hop, 7 off a trunk, 8 down off cover, 100 plus a cover's number up onto it
        public bool Dipped;                            // this climb has already made its pine's bough dip
        // PREDATORS (Predators.cs): Taken is "an owl has this one": nothing of it runs until Restore's timer is out and a replacement comes down a pine.
        public bool Taken; public float RestoreIn; public int HomePine; public float FearX = float.NaN;
    }

    private struct Cache { public bool On; public float X, Y, Life; public int Owner; }
    private struct Puff { public bool On; public float X, Y, Vx, Vy, T, Life; public int Size; }
    private struct Bough { public bool On, Snowed; public float X, Y, T, Amp; public uint[]? Src; public int Px, Py, Pw, Ph, Pad, Pine; public float[]? Hx; }

    private const int Levels = 4;
    private const float MountFor = 0.42f, HopFor = 0.55f, TurnFor = 0.42f, TurnRise = 0.12f, CurlFor = 0.42f, BlendFor = 0.15f;
    private readonly Sq[] _sq;
    private readonly SquirrelLevel[] _lv = new SquirrelLevel[Levels];
    private readonly float[] _levelMetre = new float[Levels];
    private readonly Sprite[] _scrape = new Sprite[Levels];
    private readonly Sprite[] _speck = new Sprite[3];
    private readonly Cache[] _caches = new Cache[8];
    private readonly Puff[] _puffs = new Puff[64];
    private readonly Bough[] _boughs = new Bough[4];
    private readonly Random _fx = new(31);              // cosmetic dice only (specks): a puff must never move a squirrel's luck
    private readonly float _u;
    private readonly FeederBirds _birds;
    private readonly StreamWriter? _log;
    private readonly string? _force;
    private bool[]? _mask;
    private double _time;
    private bool _forced, _firstUpdate = true;
    private int _tableUser = -1;
    private readonly float[] _pineHalf;                 // each pine's trunk half width (pixels)
    private readonly float[] _pineBough;                // each pine's lowest bough's bottom row near the trunk: above it the boughs hide a squirrel
    private readonly int[] _pineOrder;                  // the pines squirrels live in, nearest first for the entrance
    private readonly (int Cover, float TopY)[] _perches;

    public int Count => _sq.Length;
    public PointF SquirrelAt(int i) => i >= 0 && i < _sq.Length ? new PointF(_sq[i].X, _sq[i].Y) : PointF.Empty;
    public bool IsVisible(int i) => i >= 0 && i < _sq.Length && _sq[i].Visible;

    // ---------------------------------------------------------------- what a PREDATOR may ask of the squirrels (Predators.cs)
    // FEYNMAN VERSION: the owl may take a squirrel that is out on the snow far from a pine, and the wolf may chase one up a trunk. Both need:
    // is it OUT on the open snow (InOpen: not in a pine, on a perch, on the tray, or taken), where are its feet (FeetOf), may I take it NOW
    // (Seize: false for the last squirrel), and a replacement later (Restore: it comes down its pine, the normal entrance).
    /// <summary>Is squirrel i out on the open snow (not in or on a pine, not on a perch or the tray, not taken)?</summary>
    public bool InOpen(int i)
    {
        if (i < 0 || i >= _sq.Length) return false;
        Sq c = _sq[i];
        // out on the snow: not in or on a pine, a perch or the tray, EXCEPT a squirrel that is bounding for its pine (Flee, a Go step): it is still in the open
        bool onSnow = c.Pine == -1 || c.Ep == Doing.Flee && c.Step.Doing == St.Go;
        return !c.Taken && c.Visible && onSnow && c.Cover < 0 && !c.OnTray && c.Step.Doing is St.Go or St.Sit or St.Dig or St.Bury or St.Eat or St.Stuff;
    }
    /// <summary>The pine squirrel i is bound for or on (-1 none): a wolf that chased it goes to that trunk.</summary>
    /// <summary>The pine squirrel i WOULD run to if the danger stood at dangerX (the very rule BeginFlee uses), so a wolf can choose only a squirrel whose pine lies away from it.</summary>
    public int PineFor(int i, float dangerX) => i >= 0 && i < _sq.Length ? NearestPine(_sq[i], dangerX) : 0;
    public int PineOf(int i) => i >= 0 && i < _sq.Length ? _sq[i].Pine : -1;
    /// <summary>Is squirrel i scolding from a trunk (so a wolf below it may look up)?</summary>
    public bool Scolding(int i) => i >= 0 && i < _sq.Length && _sq[i].Step.Doing == St.Scold;
    /// <summary>Where squirrel i's feet are.</summary>
    public PointF FeetOf(int i) => SquirrelAt(i);
    /// <summary>The squirrel a wolf is chasing or an owl has its eye on has not noticed it yet (-1 none): it does not flee at the predator's Threat until the chase begins.</summary>
    public void Stalked(int i) => _stalked = i;
    private int _stalked = -1;
    /// <summary>How many squirrels are still alive (not taken).</summary>
    public int LeftCount { get { int n = 0; foreach (Sq c in _sq) if (!c.Taken) n++; return n; } }
    /// <summary>Squirrel i is taken THIS frame. False if it is not in the open or it is the last one.</summary>
    public bool Seize(int i)
    {
        if (!InOpen(i) || LeftCount < 2) return false;
        Sq c = _sq[i];
        c.Taken = true; c.RestoreIn = 1e9f; c.Visible = false; c.QN = 0; c.Turning = false; c.Curl = false; c.Carry = 0; c.Witness = -1; c.Cover = -1; c.OnTray = false;
        c.HomePine = NearestPine(c); c.Pace.Halt(); c.Step.Begin(St.Den, 1e9f); c.Ep = Doing.Home; c.NearOv = -1;
        if (_tableUser == i) _tableUser = -1;
        Roll(c, $"SEIZED at {c.X:0},{c.Y:0}");
        return true;
    }
    /// <summary>A replacement for taken squirrel i comes down its pine after "afterSeconds" (the normal entrance).</summary>
    public void Restore(int i, float afterSeconds) { if (i >= 0 && i < _sq.Length && _sq[i].Taken) _sq[i].RestoreIn = Math.Max(0.1f, afterSeconds); }
    private void ReviveSquirrel(Sq c)
    {
        // the replacement waits (a few seconds at a time) while another squirrel is on the same pine's trunk
        foreach (Sq o in _sq) if (o != c && !o.Taken && o.Pine == c.HomePine && o.Step.Doing is St.Mount or St.Climb or St.Descend or St.Exit or St.Scold) { c.RestoreIn = 3f; return; }
        c.Taken = false; c.Pine = c.HomePine; c.Ep = Doing.Entrance;
        var pn = S.Pines[c.Pine];
        c.X = pn.X; c.Y = pn.Foot; c.Level = 0; c.LevelFrom = 0; c.Up = HiddenUp(c.Pine, _lv0K); c.Visible = false;
        c.Step.Begin(St.Den, 0.6f + (float)Rng.NextDouble());
        Roll(c, $"RESTORED: a new squirrel waits in pine {c.Pine} and will come down");
    }

    public Squirrels(ChristmasScenery s, int count, Random sceneRng, Noticeboard board, FeederBirds birds) : base(s, sceneRng, board)
    {
        string? ov = Environment.GetEnvironmentVariable("PECKWORKS_SQUIRRELN");
        if (!string.IsNullOrEmpty(ov) && int.TryParse(ov, out int o)) count = o;
        count = Math.Clamp(count, 0, 6);
        _u = s.U;
        _birds = birds;
        _log = MindLog.Open();
        _force = Environment.GetEnvironmentVariable("PECKWORKS_SQUIRRELS");
        _sq = new Sq[count];
        _pineHalf = new float[s.Pines.Count];
        _pineBough = new float[s.Pines.Count];
        for (int i = 0; i < s.Pines.Count; i++)
        {
            _pineHalf[i] = Math.Max(1.5f, 0.0035f * _u);
            _pineBough[i] = s.Pines[i].Foot - 0.022f * _u;     // the painter's lowest row of boughs ends at Foot - 0.03 U with its tips drooping about 0.008 U lower near the trunk
        }
        _pineOrder = [3, 1, 5, 4, 0, 2, 6];
        // The levels: four sizes between the far pines' lane and the bottom edge, equal steps of pixels-to-the-metre.
        float farY = float.MaxValue; foreach (var p in s.Pines) farY = MathF.Min(farY, p.Foot);
        float m0 = s.Metre(farY), m1 = s.Metre(s.Height * 0.995f);
        for (int i = 0; i < Levels; i++) _levelMetre[i] = m0 + (m1 - m0) * i / (Levels - 1);
        // The perches on cover: a stump's snow cushion and each boulder huddle's cap, found on the painted pixels.
        var perches = new List<(int, float)>();
        for (int i = 0; i < s.Covers.Count; i++)
        {
            var c = s.Covers[i];
            if (c.Kind != CoverKind.Stump && c.Kind != CoverKind.Rocks) continue;
            float top = PerchTop(c);
            if (!float.IsNaN(top)) perches.Add((i, top));
        }
        _perches = [.. perches];
        if (count == 0) return;
        _mask = new bool[S.Width * S.Height];
        Array.Fill(_mask, true);
        float[] t = Traits.Deal(Rng, count), bu = Traits.Deal(Rng, count), cu = Traits.Deal(Rng, count), pl = Traits.Deal(Rng, count);
        for (int i = 0; i < count; i++)
        {
            var q = new Sq { Index = i, Timid = 0.15f + 0.6f * t[i], Busy = bu[i], Curious = cu[i], Playful = pl[i] };
            q.Senses = new Attention(q.Timid * 0.8f, q.Curious) { Keen = 0.9f, Hungry = 0.3f };
            q.Pace = new Pace(10f);
            q.Dir = i % 2 == 0 ? 1 : -1;
            q.Pine = _pineOrder[i % _pineOrder.Length];
            q.Ep = Doing.Entrance;
            var pn = S.Pines[q.Pine];
            q.X = pn.X; q.Y = pn.Foot; q.Level = 0; q.LevelFrom = 0;
            q.Up = HiddenUp(q.Pine, _lv0K);
            // The entrance: down their pines one after the other in the first 20 s (a forced test brings it in at once).
            q.Step.Begin(St.Den, _force != null ? 0.6f + 1.2f * i : 1.5f + 6.5f * i + (float)Rng.NextDouble() * 2f);
            q.NextEpisode = 0;
            _sq[i] = q;
        }
        for (int i = 0; i < 3; i++) _speck[i] = SquirrelsArt.Speck(2 + i);
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        BuildInBackground(() =>
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            QuietFor(Levels, i =>
            {
                float k = Math.Max(7f, SquirrelsArt.UnitPerMetre * _levelMetre[i]);
                _lv[i] = SquirrelsArt.BuildLevel(k);
                _scrape[i] = SquirrelsArt.Scrape(k);
            });
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            Warm();
            if (_log != null) lock (_log) _log.WriteLine($"0.000 Squirrels build {System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds:0} ms, warm-up {System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds:0} ms");
            if (!string.IsNullOrEmpty(sheet) && sheet.StartsWith("squirrels")) { string path = Path.Combine(Environment.CurrentDirectory, "review", "sheet_squirrels.png"); try { SquirrelsArt.WriteSheet(_lv[Levels - 1], path); } catch (Exception e) { File.WriteAllText(path + ".err.txt", e.ToString()); } }
        });
        if (_log != null)
        {
            _log.WriteLine($"0.000 Squirrels facts crest {S.CrestMin:0}..{S.CrestMax:0} smallBandTop {S.SmallBandTop:0} levels metre {_levelMetre[0]:0.0} {_levelMetre[1]:0.0} {_levelMetre[2]:0.0} {_levelMetre[3]:0.0}");
            for (int i = 0; i < S.Pines.Count; i++) _log.WriteLine($"0.000 Squirrels pine {i} x={S.Pines[i].X:0} foot={S.Pines[i].Foot:0} top={S.Pines[i].Top:0} bough={_pineBough[i]:0}");
            foreach (var p in _perches) _log.WriteLine($"0.000 Squirrels perch cover {p.Cover} {S.Covers[p.Cover].Kind} x={S.Covers[p.Cover].Foot.X:0} top={p.TopY:0} foot={S.Covers[p.Cover].Foot.Y:0}");
        }
    }

    private float _lv0K => Math.Max(7f, SquirrelsArt.UnitPerMetre * _levelMetre[0]);

    private static void Warm()
    {
        var all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (Type t in new[] { typeof(Squirrels), typeof(Sq), typeof(SquirrelsArt), typeof(SquirrelBooks), typeof(Squirrel) })
            foreach (var m in t.GetMethods(all))
                try { if (!m.IsAbstract && !m.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
    }

    // ============================================================ the world's facts, read off the pixels
    /// <summary>The y of the TOP of a stump's cushion or a boulder huddle's cap: the first row from above where the cover's own pixels begin at its middle (so a squirrel's feet stand exactly on what is painted).</summary>
    private float PerchTop(Cover c)
    {
        int px = Math.Clamp((int)c.Foot.X, 0, S.Width - 1);
        int y0 = Math.Max(2, (int)(c.Foot.Y - 1.5f * c.Height)), y1 = Math.Min(S.Height - 3, (int)c.Foot.Y);
        for (int y = y0; y <= y1; y++)
            if (S.NearDepth[y * S.Width + px] != 0 && S.NearDepth[(y + 1) * S.Width + px] != 0 && S.NearDepth[(y - 1) * S.Width + px] == 0) return y + 1;
        return float.NaN;
    }

    private float KAt(int level) => _lv[level]?.K ?? Math.Max(7f, SquirrelsArt.UnitPerMetre * _levelMetre[level]);
    private float KOf(Sq c) => Math.Max(7f, SquirrelsArt.UnitPerMetre * S.Metre(c.Y));
    private float BodyHalf(Sq c) => 0.5f * KAt(1);                  // one room for every level, so a spot that was fine a moment ago stays fine when the lane carries the squirrel to the next size

    private int LevelAt(float y, int cur)
    {
        float m = S.Metre(y);
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < Levels; i++) { float d = MathF.Abs(m - _levelMetre[i]); if (d < bd) { bd = d; best = i; } }
        if (best == cur) return cur;
        return MathF.Abs(m - _levelMetre[cur]) > 1.10f * bd + 0.5f ? best : cur;   // hysteresis: only after it is clearly nearer the other level
    }

    /// <summary>How far up a pine's trunk a squirrel must have climbed to be wholly hidden by the boughs: the visible trunk plus one and a half bodies.</summary>
    private float HiddenUp(int pine, float k) => (S.Pines[pine].Foot - _pineBough[pine]) + 1.6f * k;

    // ============================================================ where a squirrel may stand
    private bool CanRun(Sq c, float x, float y)
    {
        if (RunOk(c, x, y)) return true;
        // Standing on ground that is not ground (a hop landed a pixel into a keep-out strip, a pond rim): a short step is allowed until it is out.
        return !RunOk(c, c.X, c.Y) && (x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) < 24f * BodyHalf(c) * BodyHalf(c);
    }

    private bool RunOk(Sq c, float x, float y)
    {
        float room = BodyHalf(c);
        if (InApron(x, y)) return true;
        if (!S.CanStand(x, y, room)) return false;
        if (y < S.SmallBandTop && !(c.Y < S.SmallBandTop || c.Goal.Y < S.SmallBandTop)) return false;   // the herd's band is crossed only on the way to or from a pine
        return !S.Crowded(x, y, room, this, c.Index);
    }

    /// <summary>The bare ground at a pine's foot, above the snowfield's far lane: a squirrel stands there to climb or has just come down.</summary>
    private bool InApron(float x, float y)
    {
        foreach (var p in S.Pines)
            if (MathF.Abs(x - p.X) < 0.045f * _u && y >= p.Foot - 2f && y < p.Foot + 0.03f * _u) return true;
        // the foot of the bird table's post is bare ground a squirrel stands on to start up it (CanStand keeps feet off the post itself)
        if (MathF.Abs(x - S.FeederFoot.X) < 0.03f * _u && MathF.Abs(y - (S.FeederFoot.Y + 1.5f)) < 4f) return true;
        return false;
    }

    // ============================================================ UPDATE
    public override void Update(float dt)
    {
        if (_sq.Length == 0) return;
        long u0 = _firstUpdate ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        _time += dt;
        TickFx(dt);
        for (int i = 0; i < _caches.Length; i++) if (_caches[i].On) { _caches[i].Life -= dt; if (_caches[i].Life <= 0f) _caches[i].On = false; }
        if (_force != null && !_forced && _time > 9.0) { _forced = true; Force(); }
        bool table = false;
        foreach (Sq c in _sq)
        {
            if (c.Taken) { c.Visible = false; c.RestoreIn -= dt; if (c.RestoreIn <= 0f) ReviveSquirrel(c); continue; }       // taken by an owl: nothing of it runs until its replacement comes down a pine
            float px = c.X, py = c.Y;
            Tick(c, dt);
            if (c.OnTray) table = true;
            if (c.Visible && c.Pine == -1) S.Occupy(this, c.Index, c.X, c.Y, BodyHalf(c));
            MakeFrame(c, dt);
            if (_log != null)
            {
                float sp = dt > 0 ? MathF.Sqrt((c.X - px) * (c.X - px) + (c.Y - py) * (c.Y - py)) / dt : 0f;
                _log.WriteLine($"{Board.Time:0.000} Squirrels {c.Index} x={c.X:0.0} y={c.Y:0.0} v={sp:0.0} ep={c.Ep} step={c.Step.Doing}{(c.Turning ? (c.Curl ? " curl" : " turn") : "")} page={(c.Show.Page == null ? "-" : c.Show.Page.GetHashCode().ToString("x"))} lv={c.Level} up={c.Up:0.0} dir={c.Dir} vis={(c.Visible ? 1 : 0)}");
            }
        }
        _birds.TrayBlocked = table;
        if (_firstUpdate) { _firstUpdate = false; if (_log != null) _log.WriteLine($"{Board.Time:0.000} Squirrels first-update {System.Diagnostics.Stopwatch.GetElapsedTime(u0).TotalMilliseconds:0.00} ms"); }
    }

    private void Roll(Sq c, string what) { if (_log != null) _log.WriteLine($"{Board.Time:0.000} Squirrels {c.Index} roll {what}"); }

    // ---------------------------------------------------------------- one squirrel, one frame
    private void Tick(Sq c, float dt)
    {
        c.Step.Tick(dt); c.Senses.Tick(dt); c.Clock += dt; c.EpT += dt; c.Seconds += dt;
        c.PostRest -= dt; c.NextLook -= dt; c.NextScold -= dt;
        if (c.PostBout > 0f) c.PostBout -= dt;
        if (c.Blend > 0f) c.Blend -= dt;
        if (c.LevelBlend > 0f) c.LevelBlend -= dt;
        c.LookT -= dt;
        c.LookAmt = Math.Clamp(c.LookAmt + (c.LookT > 0f ? 1f : -1f) * 5f * dt, 0f, 1f);
        // A change of size (the lane has carried it to the next level) happens only on the open snow, and is dissolved over 0.2 s.
        if (c.Pine == -1 && c.Cover < 0 && !c.OnTray && c.LevelBlend <= 0f && !c.Turning && c.Step.Doing is St.Go or St.Sit or St.Dig or St.Eat or St.Bury or St.Hop)
        {
            int lvNew = LevelAt(c.Y, c.Level);
            if (lvNew != c.Level) { c.LevelFrom = c.Level; c.Level = lvNew; c.LevelBlend = 0.2f; }
        }
        if (c.Step.Doing != St.Den) React(c);
        Act(c, dt);
        if (c.Step.Doing != St.Go && c.Step.Doing != St.Hop) c.Pace.Halt();
        bool down = IsDown(c.Step.Doing);
        if (down != c.WasDown && c.Pine == -1 && c.Cover < 0 && !c.OnTray && !c.Turning && c.Step.Doing is St.Go or St.Dig or St.Bury or St.Sit) { c.Blend = BlendFor; c.Sinking = down; }   // the sit sinks into the crouch the bound begins from, or stands up out of it, over nine frames
        c.WasDown = down;
    }

    // ---------------------------------------------------------------- the senses: only through Attention.Check
    private bool Busy(Sq c) => c.Step.Doing is St.Den or St.Exit or St.Mount or St.Climb or St.Descend or St.Dismount or St.Hop or St.TrayOver || c.Turning || c.Ep == Doing.Flee;

    private void React(Sq c)
    {
        if (c.Step.Doing == St.Scold) { ScoldWatch(c); return; }
        if (c.Ep == Doing.Flee) return;
        if (c.Index == _stalked) return;                  // the squirrel a wolf or an owl has its eye on has not noticed yet
        Reaction r = c.Senses.Check(Board, new PointF(c.X, c.Y), c, out Notice n);
        if (r == Reaction.None) return;
        if (_log != null) _log.WriteLine($"{Board.Time:0.000} Squirrels {c.Index} reaction {r} cause {n.Kind} at {n.At.X:0},{n.At.Y:0}");
        bool danger = n.Kind is Sense.Threat || (n.Kind is Sense.Loud && r is Reaction.Flee);
        if (danger) c.FearX = n.At.X;                    // a flight runs to a pine AWAY from the danger (NearestPine penalises pines on its side)
        if (r == Reaction.Flee || (r == Reaction.Freeze && n.Kind == Sense.Threat) || (danger && r == Reaction.Freeze))
        {
            if (c.Step.Doing is St.Den or St.Exit) return;               // inside the pine already: safe
            BeginFlee(c);
            return;
        }
        if (Busy(c) || c.Step.Doing is St.Stuff or St.Eat or St.Dig or St.Bury) return;     // a Look is the HEAD only, and never interrupts a busy doing
        if (c.Step.Doing == St.Sit && c.NextLook <= 0f)
        {
            c.LookT = r == Reaction.Freeze ? 2.8f : 1.8f + 1.4f * c.Curious; c.NextLook = 6f;
            c.Step.Left = Math.Max(c.Step.Left, c.LookT + 0.4f);
            int toward = n.At.X >= c.X ? 1 : -1;
            if (toward != c.Dir && n.Kind != Sense.Movement || toward != c.Dir && n.At.Y > c.Y - 0.2f * _u) { StartTurn(c); }   // a sitting squirrel comes round through the front view (never a one-frame flip)
        }
    }

    // ---------------------------------------------------------------- the step machine
    private void Push(Sq c, St kind, float x = 0f, float y = 0f, float t = 0f, int arg = 0)
    {
        if (c.QN < c.Q.Length) c.Q[c.QN++] = new Item { Kind = kind, X = x, Y = y, T = t, Arg = arg };
    }

    private void Next(Sq c)
    {
        if (c.QN > 0)
        {
            Item it = c.Q[0];
            for (int i = 1; i < c.QN; i++) c.Q[i - 1] = c.Q[i];
            c.QN--;
            BeginItem(c, it);
            return;
        }
        Decide(c);
    }

    private void BeginItem(Sq c, Item it)
    {
        c.Clock = 0f;
        switch (it.Kind)
        {
            case St.Go:
                c.Goal = new PointF(it.X, it.Y); c.Arrive = it.T; c.Dash = it.Arg == 1; c.Curls = 0; c.Refused = 0;
                c.Step.Begin(St.Go, 14f);
                Head(c);
                break;
            case St.Sit: c.Step.Begin(St.Sit, it.T); if (it.Arg == 1) { c.LookT = it.T * 0.6f; } break;
            case St.Dig: case St.Bury: case St.Eat: case St.Stuff: case St.Scold: c.Step.Begin(it.Kind, it.T); break;
            case St.Den: c.Step.Begin(St.Den, it.T); c.Visible = false; break;
            case St.Mount:
                c.Pine = it.Arg; c.Up = 0f; c.Dipped = false; c.Step.Begin(St.Mount, MountFor);
                { float vx = VertX(c); c.Dir = vx >= c.X ? 1 : -1; }
                break;
            case St.Climb: c.UpTo = it.T; c.Phase = 0f; c.Step.Begin(St.Climb, 30f); break;
            case St.Exit: c.Step.Begin(St.Exit, it.T); c.Visible = false; BoughDip(c.Pine, 0.0070f); break;
            case St.Descend:
                c.UpTo = 0f; c.Dir = -c.Dir; c.Step.Begin(St.Descend, 30f);    // the head-down pages face the other way from the climb's: the facing is handed over HERE (the library's note)
                break;
            case St.Dismount: c.Step.Begin(St.Dismount, MountFor); break;
            case St.Hop:
                c.HopFrom = new PointF(c.X, c.Y); c.HopLift = it.T; c.HopArg = it.Arg;
                // Arg 7: off the foot of a trunk, the way the squirrel is facing (X is how far ahead, Y how far down): never a flip of facing in the air.
                c.HopTo = it.Arg == 7 ? new PointF(c.X + c.Dir * it.X, c.Y + it.Y) : new PointF(it.X, it.Y);
                if (it.Arg != 7 && MathF.Abs(it.X - c.X) > 2f && (it.X >= c.X ? 1 : -1) != c.Dir) c.Dir = it.X >= c.X ? 1 : -1;   // (a hop is always begun from a sit that has already faced its way: see the callers)
                if (it.Arg >= 100) { c.Cover = it.Arg - 100; c.NearOv = S.Nearness(S.Covers[c.Cover].Foot.Y + 3f); }
                c.Step.Begin(St.Hop, HopFor);
                break;
            case St.TrayOver: c.HopFrom = new PointF(c.X, c.Y); c.HopTo = new PointF(it.X, it.Y); c.Step.Begin(St.TrayOver, MountFor); _birds.ClearTable(14f); break;
        }
    }

    /// <summary>The x of the vertical thing (the trunk or the post) a squirrel is on or going to.</summary>
    private float VertX(Sq c) => c.Pine >= 0 ? S.Pines[c.Pine].X : S.FeederFoot.X;
    private float VertFoot(Sq c) => c.Pine >= 0 ? S.Pines[c.Pine].Foot : S.FeederFoot.Y;

    // ---------------------------------------------------------------- Act: what the step does this frame
    private void Act(Sq c, float dt)
    {
        float k = KAt(c.Level);
        switch (c.Step.Doing)
        {
            case St.Den:
                c.Visible = false;
                if (c.Step.Over)
                {
                    if (Lull && c.Ep != Doing.Entrance) { c.Step.Left = 2.5f; return; }
                    if (c.Ep == Doing.Entrance && !Ready) { c.Step.Left = 0.5f; return; }
                    BeginExit(c);
                }
                break;
            case St.Exit:
                c.Visible = false;
                if (c.Step.Over) { c.Visible = true; Next(c); }
                break;
            case St.Sit:
                if (c.Turning) { TickTurn(c, dt); break; }
                if (c.Step.Over) Next(c);
                break;
            case St.Dig: case St.Bury: case St.Eat: case St.Stuff:
                if (c.Turning) { TickTurn(c, dt); break; }
                SpawnWork(c, dt);
                if (c.Step.Over) EndWork(c);
                break;
            case St.Go: ActGo(c, dt); break;
            case St.Mount:
                if (c.Step.Over) { c.Visible = true; Next(c); }
                break;
            case St.Climb:
            {
                float d = 4.4f * k * dt;
                c.Up += d;
                c.Phase += d / (2.2f * k);
                if (c.Pine >= 0 && !c.Dipped) { var b = _lv[c.Level].B; float headTop = c.Y - c.Up - b.UpHead; if (headTop <= _pineBough[c.Pine]) { c.Dipped = true; BoughDip(c.Pine, 0.0055f); } }
                if (c.Up >= c.UpTo) { c.Up = c.UpTo; Next(c); }
                break;
            }
            case St.Scold:
                if (c.Step.Over) Next(c);
                break;
            case St.Descend:
            {
                float d = 4.4f * k * dt;
                c.Up = MathF.Max(0f, c.Up - d);
                c.Phase += d / (2.2f * k);
                if (c.Up <= 0f) Next(c);
                break;
            }
            case St.Dismount:
                if (c.Step.Over)
                {
                    float landing = VertX(c) - c.Dir * 0.4f * k;
                    c.X = landing; c.Y = VertFoot(c);
                    c.Up = 0f;
                    if (c.Pine >= 0) c.Pine = -1;
                    Next(c);
                }
                break;
            case St.Hop:
            {
                float f = Math.Clamp(c.Clock / HopFor, 0f, 1f);
                float a = Math.Clamp((f - Squirrel.HopPush) / (Squirrel.HopLand - Squirrel.HopPush), 0f, 1f), sm = a * a * (3f - 2f * a);
                c.X = c.HopFrom.X + (c.HopTo.X - c.HopFrom.X) * sm;
                c.Y = c.HopFrom.Y + (c.HopTo.Y - c.HopFrom.Y) * sm - c.HopLift * MathF.Sin(MathF.PI * a);
                if (c.Step.Over)
                {
                    c.X = c.HopTo.X; c.Y = c.HopTo.Y;
                    if (c.HopTo.Y > c.HopFrom.Y + 0.05f * k) Spray(c.X, c.Y, 6);          // a landing from a height raises a little snow
                    if (c.HopArg < 100) c.NearOv = -1;                                    // back on the open snow: stamped with the nearness of its own feet again (up onto cover it keeps the cover's)
                    Next(c);
                }
                break;
            }
            case St.TrayOver:
            {
                if (c.Step.Over)
                {
                    c.X = c.HopTo.X; c.Y = c.HopTo.Y; c.Up = 0f; c.Pine = -1; c.OnTray = true;
                    c.NearOv = S.Nearness(S.FeederFoot.Y) + 1;                  // on the tray it is stamped in front of the post and the roof
                    Next(c);
                }
                break;
            }
        }
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

    /// <summary>The Den's timer is over: the squirrel comes out of its pine, behind the boughs, head first, down the trunk.</summary>
    private void BeginExit(Sq c)
    {
        var p = S.Pines[c.Pine];
        c.X = p.X; c.Y = p.Foot; c.Up = HiddenUp(c.Pine, KAt(0));
        c.Level = LevelAt(p.Foot, 0); c.LevelFrom = c.Level;
        c.Dir = c.Index % 2 == 0 ? 1 : -1;
        c.Carry = c.Ep == Doing.Entrance || Rng.NextDouble() < 0.35 ? 1 : 0;   // a squirrel often brings a cone down with it
        c.QN = 0;
        Push(c, St.Descend); Push(c, St.Dismount);
        Push(c, St.Hop, 0.07f * _u, 0.045f * _u, 0.3f * KAt(c.Level), 7);
        Push(c, St.Sit, 0, 0, 0.7f + 0.9f * (float)Rng.NextDouble());
        Roll(c, $"exit pine {c.Pine} carry {c.Carry}");
        c.Step.Begin(St.Exit, 0.5f); c.Visible = false; BoughDip(c.Pine, 0.0070f);
        if (c.Ep != Doing.Entrance) { c.Ep = Doing.Roam; c.EpT = 0; }
        else { c.EpT = 0; }
        c.Clock = 0f;
    }

    // ---------------------------------------------------------------- going
    private void Head(Sq c)
    {
        if (c.Pace.Speed > 0.02f * _u) return;
        float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
        if (dx * dx + dy * dy < 1f) return;
        c.Heading = MathF.Atan2(dy, dx);
        if (MathF.Abs(MathF.Cos(c.Heading)) <= 0.2f) return;
        int dir = MathF.Cos(c.Heading) > 0 ? 1 : -1;
        if (dir == c.Dir) return;
        StartTurn(c);
    }

    private static bool IsDown(St s) => s is St.Go or St.Dig or St.Bury;

    private void StartTurn(Sq c)
    {
        c.Turning = true; c.Curl = false; c.TurnT = 0;
        c.TurnPre = c.WasDown ? TurnRise : 0f;
        c.TurnPost = IsDown(c.Step.Doing) ? TurnRise : 0f;
    }

    private void TickTurn(Sq c, float dt)
    {
        c.TurnT += dt;
        float total = c.Curl ? CurlFor : c.TurnPre + TurnFor + c.TurnPost;
        if (c.TurnT < total) { if (c.Curl) CurlMove(c, dt); return; }
        // The picture flips (mirrors) exactly at the middle page, which is mirror symmetric; after the whole turn the facing is the new one.
        c.Turning = false; c.Curl = false;
        c.Dir = -c.Dir;
        if (c.Step.Doing == St.Go) { c.Heading = MathF.Atan2(c.Goal.Y - c.Y, c.Goal.X - c.X); c.Pace.Resume(c.TurnV); }
    }

    private void CurlMove(Sq c, float dt)
    {
        // The curl keeps its momentum: it slides on a little, slowing (the front end has planted).
        float q = c.TurnT / CurlFor;
        float v = c.TurnV * (1f - 0.65f * q);
        float nx = c.X + c.Dir * (q < 0.5f ? 1f : -1f) * v * dt * 0.5f;
        if (CanRun(c, nx, c.Y)) c.X = nx;
    }

    private void CurlBack(Sq c, float stepThisFrame, float dt)
    {
        c.Curls++;
        if (c.Curls >= 3) { c.Pace.Halt(); c.Curls = 0; c.QN = 0; c.Step.Begin(St.Sit, 0.8f + 0.6f * (float)Rng.NextDouble()); c.Clock = 0; Roll(c, "gives up a goal it kept curling at"); return; }
        c.Turning = true; c.Curl = true; c.TurnT = 0;
        c.TurnV = MathF.Max(stepThisFrame > 0f ? stepThisFrame / dt : c.Pace.Speed, 0.1f * _u);
        c.Pace.Halt(); c.Refused = 0;
    }

    private void ActGo(Sq c, float dt)
    {
        if (c.Turning) { TickTurn(c, dt); if (c.Turning) return; }
        float k = KAt(c.Level);
        float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < MathF.Max(c.Arrive, 0.25f * k) || c.Step.Over)
        {
            c.Pace.Halt();
            c.X = dist < 3f * k ? c.Goal.X : c.X; c.Y = dist < 3f * k ? c.Goal.Y : c.Y;   // the last fraction of a length is closed (never a visible hop: it is under a pixel at the slow-in's pace)
            Next(c);
            return;
        }
        float speed = (c.Dash ? 10.5f : 7.4f) * k;
        c.Pace.Accel = 28f * k;
        c.Pace.Want = c.Dash ? speed : MathF.Min(speed, MathF.Max(0.9f * k, dist * 2.5f));
        float want = MathF.Atan2(dy, dx);
        c.Heading = Turn.Toward(c.Heading, want, dist < 2f * k ? 7f : 3.5f, dt);
        float err = MathF.IEEERemainder(want - c.Heading, MathF.Tau);
        float step = c.Pace.Tick(dt);
        if (step <= 0f) return;
        if (MathF.Abs(err) > 1.75f && c.Pace.Speed > 0.03f * _u) { CurlBack(c, step, dt); return; }
        // The bound PULSES: fastest in flight, slowest over the forepaws; the pages follow the pulsed distance.
        float p = c.Phase - MathF.Floor(c.Phase);
        step *= 0.55f + 0.45f * (1f + MathF.Sin(MathF.Tau * (p - 0.35f)));
        float x = c.X, y = c.Y, hd = c.Heading;
        var stepped = Ground.StepRound((px, py) => CanRun(c, px, py), ref x, ref y, ref hd, step, err);
        if (stepped == Ground.Stepped.Refused)
        {
            if (++c.Refused > 8)
            {
                c.Pace.Halt(); c.QN = 0; c.Refused = 0;
                Roll(c, $"refused at {c.X:0},{c.Y:0} going to {c.Goal.X:0},{c.Goal.Y:0}");
                c.Step.Begin(St.Sit, 0.7f); c.Clock = 0;
            }
            return;
        }
        c.Refused = 0;
        c.X = x; c.Y = y; c.Heading = hd;
        c.Phase += step / (2.2f * k);
        if (MathF.Abs(MathF.Cos(hd)) > 0.2f)
        {
            int dir = MathF.Cos(hd) > 0 ? 1 : -1;
            if (dir != c.Dir) { CurlBack(c, step, dt); return; }
        }
        // A squirrel bounding in the open is a thing the small animals notice: a bout of 2.5 s, then ten seconds' rest.
        if (c.PostBout <= 0f && c.PostRest <= 0f && c.Y >= S.SmallBandTop - 1f) { c.PostBout = 2.5f; c.PostRest = 12f; }
        if (c.PostBout > 0f && (int)(c.Clock * 2f) != (int)((c.Clock - dt) * 2f))
            Board.Post(Sense.Movement, new PointF(c.X, c.Y), 0.12f * _u, 0.45f, c);
    }

    // ---------------------------------------------------------------- work: dig, bury, eat, stuff: particles and endings
    private void SpawnWork(Sq c, float dt)
    {
        if (c.Step.Doing is St.Dig or St.Bury && (int)(c.Clock * 6f) != (int)((c.Clock - dt) * 6f))
            Spray(c.X + c.Dir * 0.5f * KAt(c.Level), c.Y, 2);           // snow thrown back between the forepaws, three times a second
    }

    private void EndWork(Sq c)
    {
        if (c.Step.Doing == St.Bury)
        {
            for (int i = 0; i < _caches.Length; i++)
                if (!_caches[i].On)
                {
                    _caches[i] = new Cache { On = true, X = c.X + c.Dir * 0.5f * KAt(c.Level), Y = c.Y, Life = 90f, Owner = c.Index };
                    foreach (Sq o in _sq)   // who is watching: a curious squirrel that is sitting about within 0.4 U
                        if (o != c && o.Step.Doing is St.Sit or St.Go && !Busy(o) && o.Witness < 0 && MathF.Abs(o.X - c.X) < 0.4f * _u && MathF.Abs(o.Y - c.Y) < 0.12f * _u && Rng.NextDouble() < 0.25 + 0.6 * o.Curious)
                        { o.Witness = i; o.WitnessAt = (float)_time + 5f + 7f * (float)Rng.NextDouble(); Roll(o, $"watched squirrel {c.Index} bury a cone"); }
                    Roll(c, $"buried a cone at {_caches[i].X:0},{_caches[i].Y:0}");
                    break;
                }
            c.Carry = 0;
        }
        else if (c.Step.Doing == St.Dig && c.Ep == Doing.Retrieve) { TookCache(c); }
        else if (c.Step.Doing == St.Dig && c.Ep == Doing.Cache) { c.Carry = 1; }      // the cone is out of its hiding place and in the paws
        else if (c.Step.Doing == St.Eat) c.Carry = 0;
        Next(c);
    }

    // ---------------------------------------------------------------- DECIDE: what next, when nothing is queued
    private PointF? PickSpot(Sq c, float minDx, float maxDx, float rowReach)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float wide = tries < 10 ? 1f : tries < 20 ? 2.2f : 3.2f;                         // a squirrel boxed in by the pond and the cover looks farther and farther afield
            float dx = minDx + (maxDx * wide - minDx) * (float)Rng.NextDouble();
            float x = c.X + (Rng.Next(2) == 0 ? -dx : dx);
            if (x < 0.03f * S.Width || x > 0.97f * S.Width) continue;
            float y = Math.Clamp(c.Y + (float)(Rng.NextDouble() * 2 - 1) * rowReach * (tries < 20 ? 1f : 5f), S.SmallBandTop + 2f, S.Height * 0.985f);
            if (dx < 1.4f * MathF.Abs(y - c.Y)) continue;     // never steeper than 35 degrees on the screen (a side-view body going straight up or down the screen slides)
            if (!CanRun(c, x, y) || !Ground.LineClear((px, py) => CanRun(c, px, py), new PointF(c.X, c.Y), new PointF(x, y), 3f)) continue;
            return new PointF(x, y);
        }
        return null;
    }

    private int NearestPine(Sq c, float avoidX = float.NaN)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < S.Pines.Count; i++)
        {
            float d = MathF.Abs(S.Pines[i].X - c.X) + 0.4f * MathF.Abs(S.Pines[i].Foot - c.Y);
            // a pine another squirrel is on or going up is not crowded again this second (they go one after the other)
            foreach (Sq o in _sq) if (o != c && o.Pine == i && o.Step.Doing is St.Mount or St.Climb && o.Up < 1.2f * KAt(o.Level)) d += 0.3f * _u;
            if (!float.IsNaN(avoidX) && (S.Pines[i].X - c.X) * (avoidX - c.X) > 0f) d += 0.6f * _u;       // a pine on the danger's side of the squirrel is the last choice
            if (d < bd && HomeReach(c, i)) { bd = d; best = i; }
        }
        if (best < 0) best = 0;
        return best;
    }

    /// <summary>Can the squirrel get from where it is to the foot of pine pi on clear ground (the same two legs QueueHome plans)?</summary>
    private bool HomeReach(Sq c, int pi)
    {
        PointF st = PineStand(c, pi, out int dir);
        float dy = c.Y - st.Y;
        PointF beside = new(st.X - dir * MathF.Max(0.02f * _u, 1.5f * dy), c.Y);
        Ground.Solid ok = (px, py) => CanRun(c, px, py);
        PointF me = new(c.X, c.Y);
        if (dy > 0.03f * _u && beside.X > 0.02f * S.Width && beside.X < 0.98f * S.Width && CanRun(c, beside.X, beside.Y)) return Ground.LineClear(ok, me, beside, 3f) && Ground.LineClear(ok, beside, st, 3f);
        return Ground.LineClear(ok, me, st, 3f);
    }

    /// <summary>The ground a squirrel stands on to start up pine "pi" from the side it is coming from.</summary>
    private PointF PineStand(Sq c, int pi, out int dir)
    {
        var p = S.Pines[pi];
        float k = KAt(Math.Min(c.Level, 1));
        dir = c.X < p.X ? 1 : -1;
        return new PointF(p.X - dir * 0.4f * k, p.Foot);
    }

    /// <summary>Queues the way home: bound to a pine, up it in a spiral, into the boughs.</summary>
    private void QueueHome(Sq c, int pi, bool dash, float denSeconds)
    {
        PointF st = PineStand(c, pi, out int dir);
        // Two legs when the squirrel is far below the pine: along its own row to beside the trunk, then up the slope to the foot (a goal
        // straight up the screen slides a side-view animal; the diagonal is under 35 degrees).
        float dy = c.Y - st.Y;
        float k = KAt(Math.Min(c.Level, 1));
        PointF beside = new(st.X - dir * MathF.Max(0.02f * _u, 1.5f * dy), c.Y);
        if (dy > 0.03f * _u && beside.X > 0.02f * S.Width && beside.X < 0.98f * S.Width && CanRun(c, beside.X, beside.Y)) Push(c, St.Go, beside.X, beside.Y, 1.2f * k, dash ? 1 : 0);
        Push(c, St.Go, st.X, st.Y, 0.5f * k, dash ? 1 : 0);
        Push(c, St.Mount, 0, 0, 0, pi);
        Push(c, St.Climb, 0, 0, HiddenUp(pi, KAt(0)));
        Push(c, St.Den, 0, 0, denSeconds);
    }

    private void Decide(Sq c)
    {
        c.Clock = 0; c.EpT = 0;
        // On a perch, the tray or the log: the only ways on are down (a hop) or along; a Lull from a story sends it home.
        if (c.Cover >= 0) { LeavePerch(c); return; }
        if (c.OnTray) { LeaveTray(c, false); return; }
        if (Lull)
        {
            c.Ep = Doing.Home; Roll(c, "lull: home");
            int pi = NearestPine(c); c.Pine = pi; QueueHome(c, pi, false, 3f + 5f * (float)Rng.NextDouble());
            Next(c);
            return;
        }
        if (c.Witness >= 0 && _time >= c.WitnessAt) { if (BeginRetrieve(c, c.Witness)) return; c.Witness = -1; }
        // The dice: roam, cache, raid the table, run the log, sit on cover, go home for a rest, romp.
        bool tableFree = _tableUser < 0;
        float[] w =
        [
            0.40f + 0.25f * c.Busy,                                     // 0 roam
            0.20f,                                                      // 1 cache
            tableFree ? 0.17f + 0.10f * c.Curious : 0f,                 // 2 raid
            0.10f,                                                      // 3 log
            _perches.Length > 0 ? 0.14f : 0f,                           // 4 perch
            0.10f + 0.20f * c.Timid,                                    // 5 home
            Playmate(c) != null ? 0.30f * c.Playful : 0f,               // 6 chase
        ];
        int face = Dice.Pick(Rng, w);
        switch (face)
        {
            case 1: if (!BeginCache(c)) BeginRoam(c); break;
            case 2: if (!BeginRaid(c)) BeginRoam(c); break;
            case 3: if (!BeginLog(c)) BeginRoam(c); break;
            case 4: if (!BeginPerch(c)) BeginRoam(c); break;
            case 5: BeginHome(c); break;
            case 6: BeginChase(c); break;
            default: BeginRoam(c); break;
        }
    }

    private void BeginRoam(Sq c)
    {
        c.Ep = Doing.Roam; c.QN = 0;
        int legs = 1 + Rng.Next(2);
        float x = c.X, y = c.Y;
        for (int i = 0; i < legs; i++)
        {
            Sq tmp = c; PointF? g = PickSpot(tmp, 0.08f * _u, 0.30f * _u, 0.045f * _u);
            if (g == null) break;
            Push(c, St.Go, g.Value.X, g.Value.Y, 0.35f * KAt(c.Level));
            Push(c, St.Sit, 0, 0, Organic.Jitter(Rng, 3.8f, 0.8f) + 1.2f, Rng.NextDouble() < 0.4 ? 1 : 0);
            c.X = g.Value.X; c.Y = g.Value.Y;      // (planning only: restored below)
        }
        c.X = x; c.Y = y;
        if (c.QN == 0) { BeginHome(c); return; }                // nowhere to roam to from here: a rest in the pines
        Roll(c, $"roam legs {legs}");
        Next(c);
    }

    private void BeginHome(Sq c)
    {
        c.Ep = Doing.Home; c.QN = 0;
        int pi = NearestPine(c); c.Pine = pi;
        QueueHome(c, pi, false, 8f + 14f * (float)Rng.NextDouble());
        Roll(c, $"home to pine {pi}");
        Next(c);
    }

    private void BeginFlee(Sq c)
    {
        bool tall = c.Cover >= 0 || c.OnTray;
        c.Ep = Doing.Flee; c.EpT = 0; c.QN = 0; c.Turning = false; c.Curl = false;
        int pi = NearestPine(c, c.FearX); c.Pine = pi; c.FearX = float.NaN;
        if (c.Cover >= 0) { var cv = S.Covers[c.Cover]; Push(c, St.Hop, cv.Foot.X + (c.X < cv.Foot.X ? -1 : 1) * cv.HalfW * 0.9f, cv.Foot.Y + 0.02f * _u, 0.4f * KAt(c.Level)); c.Cover = -1; c.NearOv = -1; }
        PointF st = PineStand(c, pi, out int dir);
        float k = KAt(Math.Min(c.Level, 1));
        Push(c, St.Go, st.X, st.Y, 0.5f * k, 1);
        Push(c, St.Mount, 0, 0, 0, pi);
        float visible = (S.Pines[pi].Foot - _pineBough[pi]);
        Push(c, St.Climb, 0, 0, 0.55f * visible + 0.4f * k);
        Push(c, St.Scold, 0, 0, 6f + 6f * (float)Rng.NextDouble());
        Roll(c, $"FLEE to pine {pi}");
        if (tall && c.OnTray) { LeaveTray(c, true); return; }
        // anticipation: two to four frames of the crouch before the first bound (Rise pages through Blend)
        c.Blend = BlendFor; c.Sinking = true;
        Next(c);
    }

    /// <summary>While scolding on the trunk: pins a call now and then, and goes up into the boughs when no threat has been felt for four seconds.</summary>
    private void ScoldWatch(Sq c)
    {
        if (c.NextScold <= 0f && c.Clock > 0.3f)
        {
            Board.Post(Sense.Call, new PointF(c.X, c.Y - c.Up), 0.30f * _u, 0.35f, c);
            c.NextScold = 12f;
        }
        bool threat = Board.NearestInReach(Sense.Threat, new PointF(c.X, c.Y), 0.8f * _u) != null || Board.Nearest(Sense.Threat, new PointF(c.X, c.Y), 0.5f * _u) != null;
        if (threat) c.ScoldFor = 0f; else c.ScoldFor += 1f / 60f;
        if (c.Step.Left < 1f && !threat && c.ScoldFor < 4f) c.Step.Left = 1.5f;     // keeps scolding until the threat has been gone four seconds
        if (c.ScoldFor >= 4f && c.Step.Left > 0.2f) c.Step.Left = 0.2f;
        if (c.Step.Over)
        {
            c.ScoldFor = 0f;
            c.Ep = Doing.Home; c.QN = 0;
            Push(c, St.Climb, 0, 0, HiddenUp(c.Pine, KAt(0)));
            Push(c, St.Den, 0, 0, 5f + 8f * (float)Rng.NextDouble());
            Next(c);
        }
    }

    // ---------------------------------------------------------------- cache, retrieve, chase
    private bool BeginCache(Sq c)
    {
        c.Ep = Doing.Cache; c.QN = 0;
        PointF? spot = PickSpot(c, 0.12f * _u, 0.34f * _u, 0.05f * _u);
        if (spot == null) return false;
        float k = KAt(c.Level);
        if (c.Carry == 0)
        {
            // Under a pine's boughs a squirrel finds a cone: it scrapes it out (snow flying), sits a moment holding it, then carries it off.
            Push(c, St.Dig, 0, 0, 1.4f);
            Push(c, St.Sit, 0, 0, 0.6f, 0);
        }
        Push(c, St.Go, spot.Value.X, spot.Value.Y, 0.4f * k);
        Push(c, St.Bury, 0, 0, 2.2f + 0.6f * (float)Rng.NextDouble());
        Push(c, St.Sit, 0, 0, 2.2f + 1.5f * (float)Rng.NextDouble(), 1);   // sits up and looks about: was anyone watching
        Roll(c, "cache");
        Next(c);
        return true;
    }

    private bool BeginRetrieve(Sq c, int ci)
    {
        if (ci < 0 || ci >= _caches.Length || !_caches[ci].On) return false;
        Cache ch = _caches[ci];
        if (!Ground.LineClear((px, py) => CanRun(c, px, py), new PointF(c.X, c.Y), new PointF(ch.X - c.Dir * 0.5f * KAt(c.Level), ch.Y), 3f) && !CanRun(c, ch.X, ch.Y)) return false;
        c.Ep = Doing.Retrieve; c.QN = 0; c.Witness = -1;
        float k = KAt(c.Level);
        float side = c.X < ch.X ? -1f : 1f;
        Push(c, St.Go, ch.X + side * 0.7f * k, ch.Y, 0.4f * k);
        Push(c, St.Dig, 0, 0, 1.8f);
        Roll(c, $"goes to dig up squirrel {ch.Owner}'s cone");
        c.RetrieveIx = ci;
        Next(c);
        return true;
    }

    /// <summary>The digging squirrel has the cone: the cache is gone; the owner, if it is about, chases the thief up the nearest pine.</summary>
    private void TookCache(Sq thief)
    {
        int ci = thief.RetrieveIx; thief.RetrieveIx = -1;
        if (ci < 0 || ci >= _caches.Length) return;
        int owner = _caches[ci].On ? _caches[ci].Owner : -1;
        _caches[ci].On = false;
        thief.Carry = 1;
        if (owner >= 0 && owner != thief.Index)
        {
            Sq o = _sq[owner];
            if (o.Visible && !Busy(o) && o.Step.Doing is St.Sit or St.Go && o.Cover < 0 && !o.OnTray && MathF.Abs(o.X - thief.X) < 0.6f * _u)
            {
                int pi = NearestPine(thief);
                BeginPursuit(thief, o, pi);
                return;
            }
        }
        thief.Ep = Doing.Retrieve;
        thief.QN = 0; Push(thief, St.Eat, 0, 0, 6f + 3f * (float)Rng.NextDouble());
    }

    /// <summary>The thief bolts for a pine and goes up it; the owner runs after it a beat later and goes up the same pine, one after the other, each in its own spiral.</summary>
    private void BeginPursuit(Sq thief, Sq owner, int pi)
    {
        Roll(thief, $"bolts with the cone, squirrel {owner.Index} chases");
        thief.Ep = Doing.Chase; thief.QN = 0; thief.Pine = pi; thief.Carry = 1;
        QueueHome(thief, pi, true, 8f + 6f * (float)Rng.NextDouble());
        owner.Ep = Doing.Chase; owner.QN = 0; owner.Pine = pi; owner.Turning = false; owner.Curl = false;
        Push(owner, St.Sit, 0, 0, 0.5f, 1);          // a beat to realise
        QueueHome(owner, pi, true, 6f + 5f * (float)Rng.NextDouble());
        owner.Pace.Halt(); owner.Step.Begin(St.Sit, 0.15f); owner.Clock = 0f;
    }

    private Sq? Playmate(Sq c)
    {
        foreach (Sq o in _sq)
            if (o != c && o.Visible && o.Cover < 0 && !o.OnTray && o.Step.Doing is St.Sit or St.Go && o.Ep is Doing.Roam or Doing.Entrance && MathF.Abs(o.X - c.X) < 0.5f * _u) return o;
        return null;
    }

    private void BeginChase(Sq c)
    {
        Sq? m = Playmate(c);
        if (m == null) { BeginRoam(c); return; }
        c.Ep = Doing.Chase; c.QN = 0; c.Mate = m.Index; c.Legs = 2;
        float k = KAt(c.Level);
        // The romp: the chaser runs at the mate, the mate bolts a leg (an ordinary dash on its row), twice; then both run up the nearest pine, mate first.
        PointF? run = Ground.FleeRun((px, py) => CanRun(m, px, py), new PointF(m.X, m.Y), new PointF(c.X, c.Y), 0.6f * k, 0.30f * _u, 0.10f * _u);
        m.Ep = Doing.Chase; m.QN = 0; m.Turning = false;
        if (run != null) { Push(m, St.Go, run.Value.X, run.Value.Y, 0.4f * KAt(m.Level), 1); }
        Push(m, St.Sit, 0, 0, 1.0f, 1);
        m.Step.Begin(St.Sit, 0.35f); m.Clock = 0;
        Push(c, St.Go, m.X, m.Y, 1.6f * k, 1);
        int pi = NearestPine(m);
        c.Pine = pi; m.Pine = pi;
        QueueHome(m, pi, true, 7f + 5f * (float)Rng.NextDouble());
        Push(c, St.Sit, 0, 0, 0.9f);
        QueueHome(c, pi, true, 6f + 5f * (float)Rng.NextDouble());
        Roll(c, $"romps after squirrel {m.Index}");
        Next(c);
    }

    // ---------------------------------------------------------------- the bird table
    /// <summary>
    /// A way to a faraway place on clear ground: straight if the line is clear, else through ONE waypoint (halfway across, on one of
    /// the lanes below the pond and the cover). False when neither works. The pond and the cover make most straight lines across the
    /// field unwalkable, and a goal chosen without asking sent a squirrel nowhere (the forced raid was refused every time).
    /// </summary>
    private bool Route(Sq c, PointF to, out PointF? via)
    {
        via = null;
        Ground.Solid ok = (px, py) => CanRun(c, px, py);
        PointF me = new(c.X, c.Y);
        if (Ground.LineClear(ok, me, to, 3f)) return true;
        float[] lanes = [S.SmallBandTop + 4f, S.Height * 0.945f, S.Height * 0.97f, S.Height * 0.99f, S.Height * 0.925f];
        float[] fracs = [0.5f, 0.3f, 0.7f, 0.15f, 0.85f];
        foreach (float fr in fracs)
            foreach (float ly in lanes)
            {
                var w = new PointF(me.X + (to.X - me.X) * fr, ly);
                if (!CanRun(c, w.X, w.Y)) continue;
                // never a steeper leg than 35 degrees from where it stands (a side-view body going straight down the screen slides)
                if (MathF.Abs(w.X - me.X) < 1.4f * MathF.Abs(w.Y - me.Y)) continue;
                if (Ground.LineClear(ok, me, w, 3f) && Ground.LineClear(ok, w, to, 3f)) { via = w; return true; }
            }
        return false;
    }
    private bool BeginRaid(Sq c)
    {
        var foot = S.FeederFoot;
        float k = KAt(Math.Max(c.Level, 2));
        float side = c.X < foot.X ? -1f : 1f;
        float standX = foot.X - (c.X < foot.X ? 1 : -1) * 0.4f * k;
        var stand = new PointF(standX, foot.Y + 1.5f);
        if (!CanRun(c, stand.X, stand.Y) || !Route(c, stand, out PointF? via)) { Roll(c, $"raid refused: stand {stand.X:0},{stand.Y:0} from {c.X:0},{c.Y:0}"); return false; }
        c.Ep = Doing.Raid; c.QN = 0; _tableUser = c.Index;
        if (via != null) Push(c, St.Go, via.Value.X, via.Value.Y, 1.0f * k, 0);
        Push(c, St.Go, stand.X, stand.Y, 0.35f * k, 1);
        Push(c, St.Mount, 0, 0, 0, -2);
        float up = foot.Y - S.FeederTray.At.Y;
        Push(c, St.Climb, 0, 0, up);
        float landX = S.FeederTray.At.X + side * -0.35f * S.FeederTray.Half;
        Push(c, St.TrayOver, landX, S.FeederTray.At.Y);
        Push(c, St.Stuff, 0, 0, 7f + 4f * (float)Rng.NextDouble());
        Roll(c, "raid the bird table");
        Next(c);
        return true;
    }

    private void LeaveTray(Sq c, bool fleeing)
    {
        // Down off the tray: a hop to the snow beside the post, a puff when it lands, then the bound away. (From the tray the squirrel
        // never goes down the post: the drop is the show.)
        float k = KAt(Math.Max(c.Level, 2));
        float side = c.Dir;
        var land = new PointF(S.FeederFoot.X + side * 0.9f * k + side * 0.02f * _u, S.FeederFoot.Y + 0.022f * _u);
        if (!CanRun(c, land.X, land.Y)) land = new PointF(S.FeederFoot.X - side * 0.9f * k - side * 0.02f * _u, S.FeederFoot.Y + 0.022f * _u);
        int keep = c.QN; Item[] saved = new Item[keep]; Array.Copy(c.Q, saved, keep);
        c.QN = 0;
        Push(c, St.Hop, land.X, land.Y, 0.55f * k);
        foreach (Item it in saved) { if (c.QN < c.Q.Length) c.Q[c.QN++] = it; }
        c.OnTray = false; _tableUser = -1;
        c.Dir = land.X >= c.X ? 1 : -1;
        c.NearOv = S.Nearness(S.FeederFoot.Y) + 1;
        if (!fleeing) { c.Ep = Doing.Roam; Roll(c, "drops from the tray"); }
        Next(c);
    }

    // ---------------------------------------------------------------- the log
    private bool BeginLog(Sq c)
    {
        var lt = S.LogTop;
        float len = lt.Right - lt.Left, k = KAt(Math.Max(c.Level, 2));
        float y = lt.TopY - 3f;
        float x0 = lt.Left + 0.06f * len, x1 = lt.Left + 0.66f * len;
        bool fromLeft = c.X < (x0 + x1) / 2;
        float a = fromLeft ? x0 : x1, b = fromLeft ? x1 : x0;
        if (!CanRun(c, a, y) || !CanRun(c, b, y) || !Route(c, new PointF(a, y), out PointF? via)) return false;
        c.Ep = Doing.LogRun; c.QN = 0;
        if (via != null) Push(c, St.Go, via.Value.X, via.Value.Y, 1.0f * k, 0);
        Push(c, St.Go, a, y, 0.4f * k);
        Push(c, St.Sit, 0, 0, 1.2f, 1);
        Push(c, St.Go, b, y, 0.4f * k);
        Push(c, St.Sit, 0, 0, 4f + 3f * (float)Rng.NextDouble(), 1);
        Roll(c, "run the log");
        Next(c);
        return true;
    }

    // ---------------------------------------------------------------- perching on the stump or a boulder
    private bool BeginPerch(Sq c)
    {
        if (_perches.Length == 0) return false;
        int pick = -1; float bd = float.MaxValue;
        for (int i = 0; i < _perches.Length; i++)
        {
            float d = MathF.Abs(S.Covers[_perches[i].Cover].Foot.X - c.X) + (Rng.NextDouble() < 0.5 ? 0.1f * _u : 0f);
            foreach (Sq o in _sq) if (o.Cover == _perches[i].Cover) d += 10f * _u;
            if (d < bd) { bd = d; pick = i; }
        }
        if (pick < 0 || bd > 5f * _u) return false;
        var cv = S.Covers[_perches[pick].Cover];
        float k = KAt(Math.Max(c.Level, 1));
        int side = c.X < cv.Foot.X ? -1 : 1;
        var stand = new PointF(cv.Foot.X + side * (cv.HalfW * 0.85f + 0.5f * k), cv.Foot.Y + 0.018f * _u);
        if (!CanRun(c, stand.X, stand.Y) || !Route(c, stand, out PointF? via)) return false;
        c.Ep = Doing.Perch; c.QN = 0; c.Cover = -1;
        if (via != null) Push(c, St.Go, via.Value.X, via.Value.Y, 1.0f * k, 0);
        Push(c, St.Go, stand.X, stand.Y, 0.3f * k);
        Push(c, St.Sit, 0, 0, 0.5f);
        // The hop up: onto the cushion or the cap, feet exactly on the painted top (found on the pixels).
        float topY = _perches[pick].TopY;
        float onX = cv.Foot.X + side * cv.HalfW * (cv.Kind == CoverKind.Stump ? 0.18f : 0.10f);
        Push(c, St.Hop, onX, topY, 0.9f * k, 100 + _perches[pick].Cover);
        Push(c, St.Eat, 0, 0, 7f + 5f * (float)Rng.NextDouble());
        Roll(c, $"perch on cover {_perches[pick].Cover}");
        c.Carry = 0;
        Next(c);
        return true;
    }

    private void LeavePerch(Sq c)
    {
        var cv = S.Covers[c.Cover];
        float k = KAt(c.Level);
        int side = c.Dir;
        PointF land = new(cv.Foot.X + side * (cv.HalfW * 0.9f + 0.6f * k), cv.Foot.Y + 0.02f * _u);
        if (!S.CanStand(land.X, land.Y, BodyHalf(c))) land = new PointF(cv.Foot.X - side * (cv.HalfW * 0.9f + 0.6f * k), cv.Foot.Y + 0.02f * _u);
        c.Q[0] = default; c.QN = 0;
        c.Ep = Doing.Roam;
        c.NearOv = S.Nearness(cv.Foot.Y + 3f);
        c.Cover = -1;
        Push(c, St.Hop, land.X, land.Y, 0.7f * k, 8);
        Push(c, St.Sit, 0, 0, 1.2f);
        Next(c);
    }

    // ---------------------------------------------------------------- the forced tests
    private void Force()
    {
        Sq a = _sq[0];
        switch (_force)
        {
            case "raid": a.Ep = Doing.Raid; if (!BeginRaid(a)) BeginRoam(a); break;
            case "cache": a.Carry = 0; if (!BeginCache(a)) BeginRoam(a); if (_sq.Length > 1) { Sq b = _sq[1]; b.Witness = -2; } break;
            case "chase": if (_sq.Length > 1) { _sq[1].Ep = Doing.Roam; BeginChase(a); } break;
            case "flee": Board.Post(Sense.Threat, new PointF(a.X, a.Y), 0.3f * _u, 1f, this); foreach (Sq s in _sq) s.Senses.Timid = 0.9f; break;
            case "home": BeginHome(a); break;
            case "log": if (!BeginLog(a)) BeginRoam(a); break;
            case "perch": if (!BeginPerch(a)) BeginRoam(a); break;
        }
        Roll(a, $"forced {_force}");
    }

    // ============================================================ the cosmetic things: puffs, boughs
    private void Spray(float x, float y, int n)
    {
        for (int i = 0; i < n; i++)
            for (int j = 0; j < _puffs.Length; j++)
                if (!_puffs[j].On)
                {
                    float m = 0.1f * _u, a = (float)_fx.NextDouble() * MathF.PI;
                    _puffs[j] = new Puff { On = true, X = x, Y = y, Vx = MathF.Cos(a) * 0.5f * m * ((float)_fx.NextDouble() + 0.3f), Vy = -MathF.Sin(a) * 0.9f * m, T = 0f, Life = 0.45f + 0.4f * (float)_fx.NextDouble(), Size = _fx.Next(3) };
                    break;
                }
    }

    private void BoughDip(int pineIx, float amp)
    {
        if (pineIx < 0 || pineIx >= S.Pines.Count) return;
        var pine = S.Pines[pineIx];
        var at = new PointF(pine.X, _pineBough[pineIx] - 0.012f * _u);
        int pw = Math.Max(8, (int)(0.085f * _u)), ph = Math.Max(5, (int)(0.045f * _u)), pad = (int)MathF.Ceiling(amp * _u) + 3;
        int x0 = Math.Clamp((int)at.X - pw / 2, 0, Math.Max(0, S.Width - pw)), y0 = Math.Clamp((int)at.Y - ph / 2, 0, Math.Max(0, S.Height - ph));
        uint[] src = new uint[pw * (ph + 2 * pad)];
        for (int y = 0; y < ph + 2 * pad; y++)
        {
            int sy = Math.Clamp(y0 - pad + y, 0, S.Height - 1);
            Array.Copy(S.Pixels, sy * S.Width + x0, src, y * pw, pw);
        }
        var hx = new float[pw]; for (int i = 0; i < pw; i++) hx[i] = MathF.Sin(MathF.PI * (i + 0.5f) / pw);
        for (int i = 0; i < _boughs.Length; i++)
            if (!_boughs[i].On) { _boughs[i] = new Bough { On = true, X = at.X, Y = at.Y, T = 0f, Amp = amp * _u, Src = src, Hx = hx, Px = x0, Py = y0, Pw = pw, Ph = ph, Pad = pad, Pine = pineIx }; return; }
    }

    private void TickFx(float dt)
    {
        for (int i = 0; i < _boughs.Length; i++)
        {
            if (!_boughs[i].On) continue;
            ref Bough b = ref _boughs[i];
            b.T += dt;
            if (!b.Snowed && b.T > 0.04f) { b.Snowed = true; Fall(b.X, b.Y + 0.012f * _u, 9); }
            else if (b.Snowed && b.T > 0.3f && b.T - dt <= 0.3f) Fall(b.X, b.Y + 0.012f * _u, 4);
            if (b.T > 1.6f) { b.On = false; b.Src = null; }
        }
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (!_puffs[i].On) continue;
            ref Puff p = ref _puffs[i];
            p.T += dt;
            if (p.T >= p.Life) { p.On = false; continue; }
            p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 3.0f * 0.1f * _u * dt;
        }
    }

    private void Fall(float x, float y, int n)
    {
        for (int i = 0; i < n; i++)
            for (int j = 0; j < _puffs.Length; j++)
                if (!_puffs[j].On)
                {
                    _puffs[j] = new Puff { On = true, X = x + ((float)_fx.NextDouble() - 0.5f) * 0.05f * _u, Y = y + (float)_fx.NextDouble() * 0.01f * _u, Vx = ((float)_fx.NextDouble() - 0.5f) * 0.03f * _u, Vy = 0.01f * _u, T = 0f, Life = 0.7f + 0.5f * (float)_fx.NextDouble(), Size = _fx.Next(3) };
                    break;
                }
    }

    // ============================================================ FRAMES: which page, where, through what
    private SquirrelSheet SitSheet(SquirrelLevel lv, Sq c, out int page)
    {
        float flickEvery = 0.9f + 1.6f * (1f - c.Timid);
        float ft = c.Clock % flickEvery;
        int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
        page = 0;
        return lv.B.Sit[tail];
    }

    private void MakeFrame(Sq c, float dt)
    {
        c.ShowOld = c.Show;
        if (c.Step.Doing is St.Den or St.Exit || !c.Visible) { c.Show = default; return; }
        if (!Ready) { c.Show = default; return; }
        FrameAt(c, c.Level, out c.Show);
    }

    private static int Clampi(int v, int n) => Math.Clamp(v, 0, n - 1);

    private void Fill(ref Frame f, SquirrelSheet sh, int page, float ox, float oy, bool mirror, int near)
    {
        f.Page = sh.Pages[Clampi(page, sh.Pages.Length)]; f.W = sh.W; f.Ox = sh.Ox; f.By = sh.By; f.OX = ox; f.OY = oy; f.Mirror = mirror; f.Near = near;
    }

    /// <summary>The world x of the sheet's origin that puts the painted body's middle column "col" over the line x (the library's column rule: unmirrored a page column c lands at origin - Ox + c, mirrored at origin + Ox - c).</summary>
    private static float ColOrigin(SquirrelSheet sh, int col, bool mirror, float x) => x + (mirror ? -1f : 1f) * (sh.Ox - col);

    private void FrameAt(Sq c, int level, out Frame f)
    {
        f = default;
        SquirrelLevel lv = _lv[level];
        SquirrelBooks b = lv.B;
        float k = lv.K;
        bool mirror = c.Dir < 0;
        int near = c.NearOv >= 0 ? c.NearOv : S.Nearness(c.Y);
        float x = c.X, y = c.Y;
        if (c.Turning && c.Curl)
        {
            float q = c.TurnT / CurlFor;
            int pg = q < 0.5f ? Math.Min(SquirrelBooks.CurlPages - 1, (int)(q * 2 * SquirrelBooks.CurlPages)) : Math.Min(SquirrelBooks.CurlPages - 1, (int)((1 - q) * 2 * SquirrelBooks.CurlPages));
            bool m2 = q < 0.5f ? mirror : !mirror;                                          // the second half is the first half mirrored: the facing changes through the symmetric front page
            Fill(ref f, b.Curl, pg, x, y, m2, near);
            return;
        }
        if (c.Turning)
        {
            float t = c.TurnT, pre = c.TurnPre, post = c.TurnPost;
            if (t < pre) Fill(ref f, b.Rise, Math.Min(SquirrelBooks.RisePages - 1, (int)(t / pre * SquirrelBooks.RisePages)), x, y, mirror, near);
            else if (t < pre + TurnFor)
            {
                float q = (t - pre) / TurnFor;
                int pg = q < 0.5f ? Math.Min(SquirrelBooks.TurnPages - 1, (int)(q * 2 * SquirrelBooks.TurnPages)) : Math.Min(SquirrelBooks.TurnPages - 1, (int)((1 - q) * 2 * SquirrelBooks.TurnPages));
                Fill(ref f, b.Turn, pg, x, y, q < 0.5f ? mirror : !mirror, near);
            }
            else if (post > 0f) Fill(ref f, b.Rise, Math.Max(0, SquirrelBooks.RisePages - 1 - (int)((t - pre - TurnFor) / post * SquirrelBooks.RisePages)), x, y, !mirror, near);
            else Fill(ref f, b.Rise, SquirrelBooks.RisePages - 1, x, y, !mirror, near);
            return;
        }
        if (c.Blend > 0f && c.Step.Doing is St.Go or St.Dig or St.Bury or St.Sit && c.Carry == 0)
        {
            // The sit sinks into the crouch the bound and the dig begin from, or stands up out of it: the Rise pages, never a one-frame change.
            float ff = Math.Clamp(c.Blend / BlendFor, 0f, 1f);
            Fill(ref f, b.Rise, (int)((c.Sinking ? ff : 1f - ff) * (SquirrelBooks.RisePages - 0.01f)), x, y, mirror, near);
            return;
        }
        switch (c.Step.Doing)
        {
            case St.Go:
            {
                SquirrelSheet run = c.Carry == 1 ? lv.RunCone : b.Run;
                float cyc = c.Phase - MathF.Floor(c.Phase);
                Fill(ref f, run, (int)(cyc * SquirrelBooks.RunPages) % SquirrelBooks.RunPages, x, y, mirror, near);
                break;
            }
            case St.Dig: case St.Bury:
                Fill(ref f, b.Dig, (int)(c.Clock * 3.2f * 8) % 8, x, y, mirror, near); break;
            case St.Eat:
                if (c.Carry == 1 || c.Cover >= 0) Fill(ref f, lv.EatCone, new[] { 0, 1, 2, 3, 2, 1 }[(int)(c.Clock * 9f) % 6], x, y, mirror, near);
                else Fill(ref f, b.Eat, new[] { 0, 1, 2, 3, 2, 1 }[(int)(c.Clock * 9f) % 6], x, y, mirror, near);
                break;
            case St.Stuff:
                Fill(ref f, lv.Stuff, (int)(c.Clock * 11f) % SquirrelsArt.StuffPages, x, y, mirror, near); break;
            case St.Sit:
            {
                if (c.Carry == 1) { Fill(ref f, lv.EatCone, 0, x, y, mirror, near); break; }
                if (c.LookAmt > 0.02f) { Fill(ref f, b.Look, Math.Min(SquirrelBooks.LookPages - 1, (int)MathF.Round(c.LookAmt * (SquirrelBooks.LookPages - 1))), x, y, mirror, near); break; }
                SquirrelSheet sh = SitSheet(lv, c, out int pg);
                Fill(ref f, sh, pg, x, y, mirror, near);
                break;
            }
            case St.Hop:
                Fill(ref f, b.Hop, Math.Clamp((int)(c.Clock / HopFor * SquirrelBooks.HopPages), 0, SquirrelBooks.HopPages - 1), x, y, mirror, near); break;
            case St.Mount:
            {
                // The feet stay where the run ended through the bend, then travel to the trunk's column as the hind end jumps up.
                float ff = c.Clock / MountFor, e = ff < 0.55f ? 0f : Smooth((ff - 0.55f) / 0.45f);
                float vx = VertX(c), tx = ColOrigin(b.MountUp, b.MountUpCol, mirror, vx), ox = c.X + (tx - c.X) * e;
                Fill(ref f, b.MountUp, Math.Clamp((int)(ff * SquirrelBooks.MountPages), 0, SquirrelBooks.MountPages - 1), ox, c.Y, mirror, c.Pine >= 0 ? near : S.Nearness(S.FeederFoot.Y) + 1);
                SetMask(ref f, c, 0f);
                break;
            }
            case St.Climb:
            case St.Scold:
            {
                float yy = c.Y - c.Up;
                SquirrelSheet sh = c.Step.Doing == St.Scold ? lv.Scold : b.Up;
                int pg = c.Step.Doing == St.Scold ? (int)(c.Clock * 12f) % SquirrelsArt.ScoldPages : (int)((c.Phase - MathF.Floor(c.Phase)) * SquirrelBooks.RunPages) % SquirrelBooks.RunPages;
                float ox = ColOrigin(sh, b.UpCol, mirror, SpiralX(c, k));
                Fill(ref f, sh, pg, ox, yy, mirror, c.Pine >= 0 ? near : S.Nearness(S.FeederFoot.Y) + 1);
                SetMask(ref f, c, c.Up);
                break;
            }
            case St.Descend:
            {
                float yy = c.Y - b.DownHead - c.Up;
                int pg = (int)((c.Phase - MathF.Floor(c.Phase)) * SquirrelBooks.RunPages) % SquirrelBooks.RunPages;
                float ox = ColOrigin(b.Down, b.DownCol, mirror, SpiralX(c, k));
                Fill(ref f, b.Down, pg, ox, yy, mirror, near);
                SetMask(ref f, c, c.Up);
                break;
            }
            case St.Dismount:
            {
                float ff = c.Clock / MountFor, e = ff < 0.55f ? 0f : Smooth((ff - 0.55f) / 0.45f);
                float vx = VertX(c), tx = ColOrigin(b.MountDown, b.MountDownCol, mirror, vx), landing = vx - c.Dir * 0.4f * k, ox = tx + (landing - tx) * e;
                Fill(ref f, b.MountDown, Math.Clamp((int)(ff * SquirrelBooks.MountPages), 0, SquirrelBooks.MountPages - 1), ox, c.Y, mirror, near);
                break;
            }
            case St.TrayOver:
            {
                // MountUp played BACKWARDS: upright on the post, then the front half comes over and level, the hind end after it.
                float ff = c.Clock / MountFor, e = Smooth(ff / 0.45f);
                float tx = ColOrigin(b.MountUp, b.MountUpCol, mirror, S.FeederFoot.X);
                float ox = tx + (c.HopTo.X - tx) * e;
                Fill(ref f, b.MountUp, Math.Clamp(SquirrelBooks.MountPages - 1 - (int)(ff * SquirrelBooks.MountPages), 0, SquirrelBooks.MountPages - 1), ox, c.HopTo.Y, mirror, S.Nearness(S.FeederFoot.Y) + 1);
                break;
            }
            default:
                Fill(ref f, b.Sit[0], 0, x, y, mirror, near); break;
        }
    }

    /// <summary>
    /// Where a squirrel on a trunk is, across it: the climb SPIRALS (the library's approved spiral, the owner's "it spirals
    /// round the trunk and up into the leaves"): three quarters of a turn over the visible trunk and the way into the boughs,
    /// so it swings from the front out to one side, round BEHIND (where the bark hides the body) and out the far side. The
    /// bird table's post does not spiral: the squirrel stays in front of it.
    /// </summary>
    private float SpiralX(Sq c, float k)
    {
        float vx = VertX(c);
        if (c.Pine < 0) return vx;
        float hs = HiddenUp(c.Pine, k);
        float theta = 0.75f * MathF.Tau * Math.Clamp(c.Up / hs, 0f, 1f);
        return vx + c.Dir * MathF.Sin(theta) * (_pineHalf[c.Pine] + 0.35f * k);
    }

    private void SetMask(ref Frame f, Sq c, float up)
    {
        if (c.Pine < 0) return;
        float k = KAt(c.Level);
        float hs = HiddenUp(c.Pine, k);
        float theta = 0.75f * MathF.Tau * Math.Clamp(up / hs, 0f, 1f);
        f.PineIx = c.Pine; f.Cone = true; f.Trunk = MathF.Cos(theta) < 0f && c.Step.Doing != St.Mount;
    }

    // ============================================================ DRAWING
    public override void Collect(DepthQueue q)
    {
        if (_sq.Length == 0) return;
        foreach (Sq c in _sq)
        {
            if (!c.Visible || c.Show.Page == null) continue;
            float key = c.Pine == -2 || c.OnTray ? S.FeederFoot.Y + 0.5f : c.Cover >= 0 ? S.Covers[c.Cover].Foot.Y + 1f : c.NearOv >= 0 ? c.Y + 0.5f : c.Y;
            if (c.Pine == -2 && c.Step.Doing != St.Hop) key = S.FeederFoot.Y + 0.5f;
            q.Add(key, this, c.Index);
        }
        for (int i = 0; i < _caches.Length; i++) if (_caches[i].On) q.Add(_caches[i].Y - 0.5f, this, 2000 + i);
        q.Add(200000f, this, 1000);
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (index == 1000) { DrawFx(fb); return; }
        if (index >= 2000) { DrawCache(fb, index - 2000); return; }
        Sq c = _sq[index];
        if (c.Show.Page == null) return;
        SquirrelLevel lv = _lv[c.Level];
        // A shadow under a squirrel on the open snow (faint, blue, part of the stamp order).
        if (c.Pine == -1 && c.Cover < 0 && !c.OnTray && c.Step.Doing is St.Go or St.Sit or St.Dig or St.Bury or St.Eat or St.Hop or St.Stuff)
            Stamp(fb, lv.Shadow, c.X + 0.05f * lv.K * c.Dir, c.Y + 1f, lv.ShadowOx, lv.ShadowOy);
        if (c.LevelBlend > 0f && c.Level != c.LevelFrom && c.Pine == -1)
        {
            // A change of size is a dissolve between the same page at the two sizes: never a pop.
            FrameAt(c, c.LevelFrom, out Frame old);
            if (old.Page != null && old.Page != c.Show.Page)
            {
                Frame n = c.Show;
                float w = 1f - c.LevelBlend / 0.2f;
                float ol = old.OX - (old.Mirror ? old.W - 1 - old.Ox : old.Ox), ot = old.OY - old.By, nl = n.OX - (n.Mirror ? n.W - 1 - n.Ox : n.Ox), nt = n.OY - n.By;
                Sprite.DrawBlend(fb, old.Page, ol, ot, n.Page!, nl, nt, w, n.Mirror, S.NearDepth, n.Near);
                return;
            }
        }
        Put(fb, c.Show);
    }

    private void Put(FrameBuffer fb, in Frame f)
    {
        float left = f.OX - (f.Mirror ? f.W - 1 - f.Ox : f.Ox), top = f.OY - f.By;
        if (!f.Cone || _mask == null) { f.Page!.DrawSmooth(fb, left, top, 1f, null, f.Mirror, S.NearDepth, f.Near); return; }
        // BEHIND THE PINE: for this one stamp the stencil switches off the inside of the boughs' cone above the lowest bough (the
        // squirrel has gone in behind them) and, on the far side of its spiral, the 7 pixel trunk; then it is put back.
        var pine = S.Pines[f.PineIx];
        int x0 = Math.Max(0, (int)MathF.Floor(left)), x1 = Math.Min(S.Width, (int)MathF.Ceiling(left) + f.Page!.Width + 1), y0 = Math.Max(0, (int)MathF.Floor(top)), y1 = Math.Min(S.Height, (int)MathF.Ceiling(top) + f.Page.Height + 1);
        float bough = _pineBough[f.PineIx], th = _pineHalf[f.PineIx];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = y0; y < y1; y++)
            {
                if (y < pine.Top || y > pine.Foot) continue;
                if (y <= bough)
                {
                    float hw = 0.92f * Brushwork.PineHalfWidth(y, pine.Top, pine.Foot, _u);
                    int a = Math.Max(x0, (int)MathF.Ceiling(pine.X - hw)), c2 = Math.Min(x1, (int)MathF.Floor(pine.X + hw) + 1);
                    for (int x = a; x < c2; x++) _mask![y * S.Width + x] = pass == 1;
                }
                else if (f.Trunk)
                {
                    int a = Math.Max(x0, (int)MathF.Ceiling(pine.X - th)), c2 = Math.Min(x1, (int)MathF.Floor(pine.X + th) + 1);
                    for (int x = a; x < c2; x++) _mask![y * S.Width + x] = pass == 1;
                }
            }
            if (pass == 0) f.Page.DrawSmooth(fb, left, top, 1f, _mask, f.Mirror, S.NearDepth, f.Near);
        }
    }

    private void DrawCache(FrameBuffer fb, int i)
    {
        Cache ch = _caches[i];
        int lv = Math.Clamp(LevelAt(ch.Y, 1), 0, Levels - 1);
        Sprite? sp = _scrape[lv];
        if (sp == null) return;
        float fade = Math.Min(1f, ch.Life / 8f);
        Stamp(fb, sp, ch.X, ch.Y, sp.Width / 2, sp.Height / 2, false, 0.9f * fade);
    }

    private void DrawFx(FrameBuffer fb)
    {
        DrawBoughs(fb);
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (!_puffs[i].On) continue;
            Puff p = _puffs[i];
            _speck[p.Size].DrawCentered(fb, p.X, p.Y, 0.9f * (1f - p.T / p.Life));
        }
    }

    /// <summary>The dipping bough: the pine's own picture, rows slid by a damped spring (the birds' technique), only inside the pine's outline.</summary>
    private void DrawBoughs(FrameBuffer fb)
    {
        for (int i = 0; i < _boughs.Length; i++)
        {
            if (!_boughs[i].On || _boughs[i].T <= 0f || _boughs[i].Src == null) continue;
            Bough b = _boughs[i];
            float dy = b.Amp * MathF.Exp(-3.2f * b.T) * MathF.Sin(MathF.Tau * 2.6f * b.T) * MathF.Min(1f, b.T / 0.08f);
            var pine = S.Pines[b.Pine];
            for (int y = 0; y < b.Ph; y++)
            {
                float vy = dy * MathF.Sin(MathF.PI * (y + 0.5f) / b.Ph);
                if (MathF.Abs(vy) < 0.5f) continue;
                int row = b.Py + y;
                if (row < pine.Top || row > pine.Foot || row >= fb.Height) continue;
                float hw = 0.85f * Brushwork.PineHalfWidth(row, pine.Top, pine.Foot, _u);
                int xa = Math.Max(b.Px, (int)MathF.Ceiling(pine.X - hw)), xb = Math.Min(b.Px + b.Pw - 1, (int)MathF.Floor(pine.X + hw));
                for (int x = xa; x <= xb; x++)
                {
                    int off = (int)MathF.Round(vy * b.Hx![x - b.Px]);
                    if (off == 0) continue;
                    int sy = Math.Clamp(y + b.Pad - off, 0, b.Ph + 2 * b.Pad - 1);
                    fb.Pixels[row * fb.Width + x] = b.Src![sy * b.Pw + (x - b.Px)];
                }
            }
        }
    }
}
