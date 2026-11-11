using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// THE SNOWFIELD'S MIDDLE: a few SNOWSHOE HARES and ONE RED FOX, living in the same patch of snow and watching each
/// other. (Pattern: Thanksgiving's Pack.cs, the coyotes and the rabbits; the brain is the same kit, Core/Minds.)
///
/// FEYNMAN VERSION. The hares are always about: they sit with twitching noses, nibble at a twig, wash their faces,
/// hop in slow lollops leaving little tracks, stand up tall to look, and now and then one springs into the air for the
/// joy of it (a binky). The fox is a VISITOR: it trots in from a side, stops to listen for a mouse under the snow, leaps
/// and dives in head first (and comes out with a face full of snow, shakes, and finds nothing), sits, curls up and
/// sleeps under a thickening dusting of snow, and then goes away again for a while, so the hares get long quiet
/// stretches. About a third of the time the fox is off screen or asleep.
///
/// THE BURROWS (round three, "holes and hiding"): each hare owns two or three holes, placed at the start on open ground and kept all
/// night. A hole is a PLACE drawn in two pieces (FoxAndHaresArt.PaintHoleBack before the animal, PaintHoleFront after it), so a hare
/// that dives, hides or climbs out goes down BEHIND real snow, never fading. A hare with no burrow in reach digs one (HD.Dig). While
/// down it peeks at the lip now and then (ears, eyes, a look, sinking). The fox noses a burrow with its muzzle over the mouth and its
/// mousing plunge opens a crater with a rim that stays a while.
///
/// THE STORY BETWEEN THEM (the part a child watches): when the fox stalks a hare (low, slow, ears flat) the hare notices
/// (a Threat posted on the noticeboard), rears up tall, THUMPS twice (a Friend notice, so every other hare in earshot
/// sits up and bolts too), turns and bolts in a zigzag for a burrow it picked, and DIVES into the snow with a puff. The
/// fox gallops after it, skids to a stop at the hole, noses it, digs twice, and gives up and sits. The hare ALWAYS gets
/// away (it is faster and has the head start). After a while two ears come up out of the snow somewhere else, then the
/// eyes, a look round, and it pops out and carries on.
///
/// WHAT IS PINNED ON THE BOARD (a poster gets a bout and a rest, never every frame for minutes):
///   fox stalking or chasing: Threat at the fox, reach 0.35 U, weight 1, every 0.5 s while it lasts;
///   fox trotting: Threat, reach 0.2 U, weight 0.4, every 0.8 s;
///   a hare's thump: Friend at the hare, reach 0.4 U, weight 0.6, at each of its two strikes; a hare that has fled does
///   not thump again for 20 s. Who is in reach is in the helper report.
/// WHAT IT READS: Santa overhead (Movement, high up: a glance, head tipped up), Loud (fireworks, the toy train), Threat
/// (the owl, the fox), the other hares' thumps (Friend). A resident reacts to kinds, never to classes.
///
/// DRAWN THROUGH the depth queue, one line per animal, per twig, per live puff, and one for the ground tracks, which sorts
/// first (they lie on the snow under everyone's feet). Sizes: a hare is its real 0.45 m times 1.3 (storybook), a fox its
/// real metres, both times the field's one scale S.Metre(y); the flip-books are painted at four size steps by the lane.
///
/// TEST VARIABLES: PECKWORKS_FOXHARE=natural (default) or one of
///   mouse (the fox is standing at x 0.72 w and starts mousing at 6 s), stalk (fox and hare 0 placed, the stalk starts at
///   6 s and the hare bolts, the chase and the dig follow), sleep (the fox curls up at 6 s), groom, binky, nibble, tall,
///   thump (hare 0 does that at 6 s), peek (hare 0 is in its burrow and comes up at 6 s), look (the fox and hare 0 look
///   up at 6 s), nose (the fox is 2.8 m from hare 0's burrow, hare 0 is down in it: it walks up, noses the mouth, digs, gives up).
///   PECKWORKS_STALKSIDE=L or R puts the stalk's fox on the left or right of hare 0. PECKWORKS_FOXHARES=off makes the count 0 (the "without me" timing). PECKWORKS_SHEET=fox;file.png or
///   hares;file.png writes the pose sheet and a night strip. PECKWORKS_MINDLOG writes a line per animal per frame and
///   one per roll and reaction.
/// </summary>
internal sealed class FoxAndHares : Resident
{
    // Hare doings. Every one lists what can cut it short and what it leaves:
    //   Sit: cut by any reaction; ends in a roll.        Look: head up for 1.8 s; cut by a worse reaction.
    //   Nibble: reach, bite for a few seconds, unreach; cut by Alert/Bolt, leaves the hare sitting where it stands.
    //   Groom: 5.5 s script, cut by a reaction.          Go/Hop/Land: a run of whole hops; a reaction cuts it at the next
    //   rest page (it lands first, so the feet are planted).  Tall: up on its hind legs, cut by nothing but time.
    //   Thump: tall, two strikes, then Bolt (cannot be cut).  Binky: one leap, never cut.  Bolt: runs to its burrow.
    //   Dive: into the snow.  Under: gone, waits for the fox to be far.  Peek: ears, eyes, head, then Sit.  Turn: 0.45 s.
    private enum HD { Sit, Look, Nibble, Groom, Go, Hop, Land, Tall, Thump, Binky, Bolt, Dive, Under, Peek, Turn, Dig }
    // Fox doings. Mouse is one doing with stages (listen, coil, leap, dive in, plunge, back out, shake, wipe, sneeze):
    // nothing cuts it once the leap has begun; a Loud during the listening stage ends it (the fox looks up).
    private enum FD { Away, Go, Trot, Settle, Stand, Look, Turn, SitIn, Sit, SitOut, CurlIn, Asleep, Wake, Mouse, StalkIn, Stalk, StalkOut, DashIn, Chase, Skid, Nose, Dig, Leave, Watch }

    private const int HB_Sit = 0, HB_Look = 1, HB_Reach = 2, HB_Nibble = 3, HB_Groom = 4, HB_Hop = 5, HB_Flee = 6, HB_Settle = 7, HB_TallIn = 8, HB_Tall = 9, HB_Thump = 10, HB_Binky = 11, HB_Dive = 12, HB_Peek = 13, HB_Turn = 14, HB_Twig = 15, HB_NLook = 16, HB_SettleF = 17, HB_BurB = 18, HB_BurF = 19, HB_Dig = 20;
    private const int FB_Stand = 0, FB_Look = 1, FB_Trot = 2, FB_Settle0 = 3, FB_Settle1 = 4, FB_Stalk = 5, FB_StalkIn = 6, FB_Gallop = 7, FB_Skid = 8, FB_Sit = 9, FB_SitIn = 10, FB_CurlIn = 11, FB_Sleep = 12, FB_Listen = 13, FB_Coil = 14, FB_Leap = 15, FB_DiveIn = 16, FB_Plunge = 17, FB_BackOut = 18, FB_Shake = 19, FB_Wipe = 20, FB_Sneeze = 21, FB_Dig = 22, FB_NoseIn = 23, FB_Nose = 24, FB_Turn = 25, FB_DashIn = 26, FB_Lower = 27, FB_Watch = 28, FB_Wake0 = 29, FB_CraB = 33, FB_CraF = 34;
    private const int Tiers = 2;                                         // the lanes are a narrow band now (SmallBandTop down), three size steps are under four percent apart
    private const float HareScale = 0.45f * 1.3f;                         // a hare's body length in metres, with the storybook factor
    private const float HopBase = 1.45f, BoltSpeed = 8.0f;                // hare: units a second (lollop, bolt)
    private const float FoxTrotV = 1.35f, FoxStalkV = 0.55f, FoxChaseV = 3.5f;   // fox: metres a second
    private const float TurnFor = 0.45f, QuickTurn = 0.26f;
    private const float RestA = 0.275f, RestB = 0.775f, StalkRest = 0.30f;

    private sealed class H
    {
        public float X, Y; public int Dir = 1;
        public Mind<HD> Mind = new(HD.Sit); public float Clock;
        public float Timid, Curious, Play, IdleOff;
        public Attention Att = null!;
        public float Odo, GX, GY, LastFrac, HopK = 1f; public int Hops;
        public bool Turning; public float TurnT; public int TurnFrom, TurnTo; public HD After = HD.Sit; public float AfterFor;
        public HD Next = HD.Sit;                       // what to do when a run of hops ends
        public float BX, BY, ToBX, ToBY;               // the burrow it dives into, and where it will come up
        public float ThumpCool, BreathT, JinkT, Dur; public int Thumps; public double HeardAt = -1;
        public float FearX, FearY; public bool FearFox, BoltPending; public int BoltDir; public float TurnQuick = 1f;
        public Pace Pace = new(40f); public int Twig = -1; public float GlanceCool;
        public Book? Bk; public int Pg; public bool Mir; public bool Vis = true; public float LX, LY;
        // round two: the glance (head and ears over a sit), a response waiting for the doing in hand to finish, the scripts' stages
        public bool GoFlee, GlWant, GlSanta, ThOut, DivePuff, PkPuff1, PkPuff2, PendSanta;
        public int Gl, GStage, GN, GM, NStage, NPg, NRp, HopsDone, EDir = 1, FreezeTo = 1;
        public float GlLevel, GlT, GlHold, GlTarget, GT, NT, NHold, ThOutT, RespAlert;
        public double PendAt = -1, Fz1 = -99, Fz2 = -99; public Resp Resp;
        public float Gx, Gy;                              // the GLIDE of a fast sheet: where the body really is minus where this page painted it (units)
        public Book? PBk; public int PPg; public bool PMir, PVis; public float PX, PY, PUnit;   // the previous frame's page, for the pose-pop log
        // THE BURROWS (round three): the hare owns a few holes; Cur is the one it is in, at or going to; Perf is true while the hare is drawn AT its hole
        // (so the hole's back piece goes down before the page and the front lip after it); DOx and DOy blend the last bound's arrival error out of the dive;
        // the hide: HidePk is a peek at the lip while Under, PkT its clock, PkHold how long the eyes stay up, NextPeek the scene time of the next one.
        public int Cur = -1, OpenPg = FoxAndHaresArt.OpenN - 1; public bool Perf, HidePk, DigPending; public float DOx, DOy, PkT, PkHold; public double NextPeek;
    }
    /// <summary>A burrow: a place, there from the first frame to the last. Who is the hare using it right now (-1 none); Open 0..7 how far it is dug.</summary>
    private sealed class Bur { public float X, Y; public int Owner, Who = -1, Open = FoxAndHaresArt.OpenN - 1; }
    /// <summary>The crater of a fox's mousing plunge: it stays on the snow and softens (fades) over a while.</summary>
    private struct Crater { public float X, Y; public double Born; public bool Live; }
    private enum Resp { None, Thump, Bolt, Freeze }

    private sealed class F
    {
        public float X, Y; public int Dir = 1;
        public Mind<FD> Mind = new(FD.Away); public float Clock;
        public Attention Att = null!; public Pace Pace = new(3f);
        public float Odo, GX, GY; public int RestIdx; public bool Braked;
        public bool Turning; public float TurnT; public int TurnFrom, TurnTo; public FD After = FD.Stand;
        public int Stage; public float StageT, LeapX0, LeapDist, HoleX, HoleY, OnScreen, PostT, BreathT, SleepFor, LookUp, LookDur;
        public float GlanceCool; public bool Sneezes; public int Target = -1; public bool SkidGo; public float StopX;
        public Book? Bk; public int Pg; public bool Mir; public float LX, LY;
        public bool Reverse;                            // playing a transition book backwards (getting up)
        public int LastPrint = -1; public FD After2; public bool GoLeave, MouseInit, SleepAfter; public float ListenFor, LastPh;
        public int WakeBook = 29; public bool LookPend; public double LookPendAt;
        public float Slope, DetourT, DetourSlope;       // the lane change as an eased slope, so a path round something is a curve
        public Book? PBk; public int PPg; public bool PMir, PVis; public float PX, PY, PUnit;   // the previous frame page, for the pose-pop log
        public float StalkCool;                          // seconds before it may stalk again
        public bool NoseNext; public float NoseT; public int Fail;        // after the chase: wait for the hare to be under, then walk up to the hole and nose it
        public float Gx, Gy;                             // the glide of the leap sheet
        public float StrideK = 1f, PrevPh; public int Plan;     // a PLANNED walk to a hole: the stride is stretched (0.8 to 1.25) so a whole number of half strides ends exactly at the spot, on a rest page
        public int NoseBur = -1, CraI = -1, CraOpen = 7; public bool Perf;   // the burrow it is nosing (drawn in two pieces round it), its crater and how far open it is, true while drawn AT a hole
    }

    private struct Decal { public float X, Y; public byte Kind; public double Born; }
    private struct Puff { public float X, Y, H, VX, VH, T, Life, Size, Op, Grav; public byte Kind; public bool On; }

    private readonly int _n;
    private readonly H[] _h;
    private readonly F? _fox;
    private readonly float[] _tierY = new float[Tiers], _hUnit = new float[Tiers], _fUnit = new float[Tiers];
    private float _yLo, _yHi;
    private Book[]? _hb, _fb;
    private Sprite[][]? _puffSprites;      // [tier][size 0..2]
    private Sprite[][]? _decalSprites;     // [tier][kind]
    private readonly (float X, float Y)[] _twigs;
    private readonly Decal[] _decals = new Decal[160];
    private int _decalAt, _decalLive;
    private readonly Puff[] _puffs = new Puff[96];
    private readonly List<Bur> _bur = new();
    private readonly Crater[] _cra = new Crater[4];
    private int _craAt;
    private readonly StreamWriter? _log = MindLog.Open();
    private readonly string _test = Environment.GetEnvironmentVariable("PECKWORKS_FOXHARE") ?? "natural";
    private double _time;
    private long _tickU, _tickD; private int _costFrames, _costStamps; private double _costAt;   // the cost log (only while MINDLOG is on)
    private bool _forced, _firstUpdate;
    private float _nextAway;

    public int HareCount => _n;
    public PointF HareFeet(int i) => new(_h[i].X, _h[i].Y);
    public PointF FoxFeet => _fox == null ? new PointF(-1000, -1000) : new PointF(_fox.X, _fox.Y);
    public bool FoxOnScreen => _fox != null && _fox.Mind.Doing != FD.Away && _fox.X > -0.3f * S.U && _fox.X < S.Width + 0.3f * S.U;

    // ================================================================ build

    public FoxAndHares(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        if (Environment.GetEnvironmentVariable("PECKWORKS_FOXHARES") == "off") count = 0;
        _n = Math.Clamp(count, 0, 8);
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_SHEET");
        if (!string.IsNullOrEmpty(sheet) && sheet.IndexOf(';') > 0) { int c = sheet.IndexOf(';'); FoxAndHaresArt.WriteSheet(sheet[..c], sheet[(c + 1)..]); }

        // The lanes this pair lives on, and the four size steps that cover them: a thing's page is painted at the size of
        // its lane, so a hare far up the snowfield is smaller than one by the log, and pages are never scaled per frame.
        float W = s.Width, Hh = s.Height;
        _yLo = float.MaxValue; _yHi = float.MinValue;
        for (int i = 0; i <= 10; i++) { float x = W * (0.02f + 0.96f * i / 10f); _yLo = MathF.Min(_yLo, s.FieldPoint(x, 0.18f).Y); _yHi = MathF.Max(_yHi, s.FieldPoint(x, 0.64f).Y); }
        _yLo = MathF.Max(_yLo, s.SmallBandTop);          // the herd keeps the rows above SmallBandTop: the small animals live at or below it, so the lane table starts there
        for (int t = 0; t < Tiers; t++)
        {
            _tierY[t] = _yLo + (_yHi - _yLo) * (t + 0.5f) / Tiers;
            _fUnit[t] = MathF.Max(3f, s.Metre(_tierY[t]));
            _hUnit[t] = MathF.Max(2.5f, s.Metre(_tierY[t]) * HareScale);
        }

        // ---- the hares: each with its own dials, a home on the right half or the left strip, and two twigs ----
        _h = new H[_n];
        float[] timid = Traits.Deal(Rng, Math.Max(1, _n)), curious = Traits.Deal(Rng, Math.Max(1, _n)), play = Traits.Deal(Rng, Math.Max(1, _n));
        float[] homes = [0.66f, 0.945f, 0.10f, 0.96f, 0.60f, 0.15f, 0.74f, 0.96f];       // (none within 0.6 of the snowman's height of him)
        var twigs = new List<(float, float)>();
        for (int i = 0; i < _n; i++)
        {
            var h = new H { Timid = timid[i], Curious = curious[i], Play = play[i], IdleOff = (float)Rng.NextDouble() * 24f, Dir = Rng.Next(2) == 0 ? 1 : -1 };
            h.Att = new Attention(0.25f + 0.6f * h.Timid, 0.2f + 0.7f * h.Curious) { Keen = 1.4f };
            PlaceNear(homes[i] * W, 0.5f * W, out h.X, out h.Y, 0.35f * HareScale * 0.1f * s.U);
            // ITS BURROWS: two or three holes of its own on open ground (the first is where it starts, underground), kept all night
            AddBurrows(i, h, homes[i] * W);
            // THE ENTRANCE: every hare starts underground in a burrow of its own and comes up, one after another, over the first 20 s
            h.Mind.Begin(HD.Under, 3f + 6.5f * i + 3f * (float)Rng.NextDouble()); h.Vis = false;
            h.BX = h.ToBX = h.X; h.BY = h.ToBY = h.Y; h.EDir = h.Dir;
            if (!Valid(h.X + h.EDir * 0.85f * HU(h.Y), h.Y, 0.3f)) h.EDir = -h.EDir;
            h.NextPeek = 2.0 + 3.0 * Rng.NextDouble() + 5.0 * i;    // the first hide-and-peek at its own hole (the entrance)
            _h[i] = h;                              // its burrows are on the snow from the very first frame (see Collect)
            for (int k = 0; k < 2; k++)
            {
                for (int tries = 0; tries < 30; tries++)
                {
                    float tx = h.X + ((float)Rng.NextDouble() - 0.5f) * 0.30f * s.U; float ty = h.Y + ((float)Rng.NextDouble() - 0.5f) * 0.12f * s.U;
                    if (!Valid(tx, ty, 0.5f * 0.1f * s.U)) continue;
                    bool far = true; foreach (var t2 in twigs) if (MathF.Abs(t2.Item1 - tx) < 0.07f * s.U && MathF.Abs(t2.Item2 - ty) < 0.05f * s.U) far = false;
                    if (far) { twigs.Add((tx, ty)); break; }
                }
            }
        }
        _twigs = [.. twigs];
        if (_n > 0)
        {
            _fox = new F { Att = new Attention(0.30f, 0.55f) { Keen = 1.1f } };
            _fox.X = -3f * _fUnit[0]; _fox.Y = MathF.Max(s.FieldPoint(0.3f * W, 0.3f).Y, s.SmallBandTop + 0.012f * s.U);
            _nextAway = 50f + 20f * (float)Rng.NextDouble();          // the fox trots in from a side a minute or so in
        }
        Log($"sizes: hare unit {_hUnit[0]:F1} to {_hUnit[Tiers - 1]:F1} px, fox metre {_fUnit[0]:F1} to {_fUnit[Tiers - 1]:F1} px, lanes y {_yLo:F0} to {_yHi:F0}");

        BuildInBackground(() =>
        {
            if (_n == 0) return;
            try { BuildPages(); } catch (Exception e) { Log($"BUILD FAILED: {e}"); throw; }
        });
    }

    private void BuildPages()
    {
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var hb = FoxAndHaresArt.HareBooks(); var fb = FoxAndHaresArt.FoxBooks();
            var hbooks = new Book[hb.Length]; var fbooks = new Book[fb.Length];
            // each book is painted on its own thread (a page is its own bitmap and its own Graphics: nothing is shared)
            QuietFor(hb.Length, i => hbooks[i] = new Book(hb[i], _hUnit));   // a few low-priority threads, never every core: see Resident.QuietFor
            QuietFor(fb.Length, i => fbooks[i] = new Book(fb[i], _fUnit));
            // the soft snow puffs (three sizes) and the ground tracks, per size step
            var puffs = new Sprite[Tiers][]; var decals = new Sprite[Tiers][];
            for (int t = 0; t < Tiers; t++)
            {
                float m = _fUnit[t];
                puffs[t] = [Sprite.Glow((int)MathF.Max(2, 0.045f * m), Color.FromArgb(240, 246, 255)), Sprite.Glow((int)MathF.Max(3, 0.085f * m), Color.FromArgb(240, 246, 255)), Sprite.Glow((int)MathF.Max(4, 0.15f * m), Color.FromArgb(240, 246, 255))];
                float hu = _hUnit[t];
                Sprite Oval(float w, float h2, Color c) => Sprite.Paint((int)MathF.Max(3, w * 2 + 2), (int)MathF.Max(3, h2 * 2 + 2), g => { using var b = new SolidBrush(c); g.FillEllipse(b, 1, 1, MathF.Max(1.5f, w * 2), MathF.Max(1.2f, h2 * 2)); });
                decals[t] =
                [
                    Oval(0.07f * hu * 1.4f, 0.035f * hu, Color.FromArgb(150, 150, 168, 206)),       // 0 hare hind print
                    Oval(0.03f * hu * 1.4f, 0.02f * hu, Color.FromArgb(140, 150, 168, 206)),        // 1 hare fore print
                    Sprite.Paint((int)MathF.Max(6, 0.5f * hu) + 2, (int)MathF.Max(4, 0.2f * hu) + 2, g =>
                    {
                        float w = MathF.Max(6, 0.5f * hu), h2 = MathF.Max(4, 0.2f * hu);
                        using var rim = new SolidBrush(Color.FromArgb(235, 244, 255)); using var hole = new SolidBrush(Color.FromArgb(200, 70, 90, 150));
                        g.FillEllipse(rim, 1, 1, w, h2); g.FillEllipse(hole, 1 + w * 0.2f, 1 + h2 * 0.25f, w * 0.6f, h2 * 0.5f);
                    }),                                                                              // 2 a burrow
                    Oval(0.035f * m * 1.5f, 0.02f * m, Color.FromArgb(140, 140, 158, 200)),         // 3 fox print
                ];
            }
            _puffSprites = puffs; _decalSprites = decals; _hb = hbooks; _fb = fbooks;
            long painted = sw.ElapsedMilliseconds;
            // WARM-UP: the first real frame compiled every method of this class at once (a 160 ms hitch with everybody starting together).
            // PrepareMethod compiles a method WITHOUT running it, so no animal's state is touched.
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (Type ty in new[] { typeof(FoxAndHares), typeof(FoxAndHaresArt), typeof(Book) })
                foreach (var m in ty.GetMethods(all))
                    try { if (!m.IsAbstract && !m.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch { }
            Log($"build: painted in {painted} ms, warmed in {sw.ElapsedMilliseconds - painted} ms");
        }
    }

    // ================================================================ places and sizes

    /// <summary>
    /// MAY A BURROW BE DUG HERE? Open ground the hare can stand on at both sides of the hole (it dives in from one side and
    /// climbs out on either), not behind or half behind a prop (the bird table's post, the tiny fir, the lamp post, the snowman),
    /// at least two hare lengths from every prop's foot, off the pond's rim, and well apart from any other burrow.
    /// </summary>
    private bool BurrowOk(float x, float y, bool strict = true)
    {
        float unit = HU(y);
        if (!HRest(x, y, 0.4f * unit) || !HRest(x - 1.45f * unit, y, 0.3f * unit) || !HRest(x + 1.45f * unit, y, 0.3f * unit)) return false;      // the hare gathers a body length and a half short of the hole, on either side
        if (S.OnPond(x, y, 1.30f)) return false;
        foreach (PointF p in new[] { S.FeederFoot, S.SaplingFoot, S.SnowmanFoot, S.LampFoot })
        {
            float dx = x - p.X, dy = 2f * (y - p.Y);
            if (MathF.Sqrt(dx * dx + dy * dy) < 2.0f * unit) return false;
        }
        foreach (Bur b in _bur) { float dx = x - b.X, dy = 1.4f * (y - b.Y); if (MathF.Sqrt(dx * dx + dy * dy) < 1.8f * unit) return false; }
        float fu = FU(y);
        // the fox must be able to WALK up from the side it comes from: a clear lane from 2.6 m out to 1.4 m out on AT LEAST ONE side (a snowman or a prop must never close the approach from both)
        if (!LineClear(x - 2.6f * fu, y, x - 1.4f * fu, y, 0.35f * fu) && !LineClear(x + 2.6f * fu, y, x + 1.4f * fu, y, 0.35f * fu)) return false;
        if (!strict) return true;                                                  // (after many failures the fox is not asked to stand on both sides)
        if (!FRest(x - 1.4f * fu, y, 0.5f * fu) || !FRest(x + 1.4f * fu, y, 0.5f * fu)) return false;
        return true;
    }

    /// <summary>Deals a hare its burrows at the start: the first one is where it starts (underground), then two more spread over its ground.</summary>
    private void AddBurrows(int owner, H h, float cx)
    {
        int first = -1;
        for (int n = 0; n < 3; n++)
        {
            float spread = n == 0 ? 0.06f : 0.55f;
            for (int tries = 0; tries < 160; tries++)
            {
                float x = (n == 0 || first < 0 ? cx : _bur[first].X) + ((float)Rng.NextDouble() * 2f - 1f) * spread * S.U * (1f + tries / 20f);
                PointF p = S.FieldPoint(x, 0.22f + 0.38f * (float)Rng.NextDouble());
                if (!BurrowOk(p.X, p.Y, tries < 70)) continue;
                _bur.Add(new Bur { X = p.X, Y = p.Y, Owner = owner });
                if (first < 0) first = _bur.Count - 1;
                break;
            }
        }
        if (first < 0) { _bur.Add(new Bur { X = h.X, Y = h.Y, Owner = owner }); first = _bur.Count - 1; }      // nowhere clean at all: its start spot
        h.Cur = first; _bur[first].Who = owner; h.X = _bur[first].X; h.Y = _bur[first].Y;
        int own = 0; foreach (Bur b in _bur) if (b.Owner == owner) own++;
        Log($"hare{owner} owns {own} burrows, starting in the one at {h.X:F0},{h.Y:F0}");
    }

    private int TierOf(float y) => Math.Clamp((int)MathF.Floor((y - _yLo) / MathF.Max(1f, _yHi - _yLo) * Tiers), 0, Tiers - 1);
    private float HU(float y) => _hUnit[TierOf(y)];
    private float FU(float y) => _fUnit[TierOf(y)];

    /// <summary>May feet stand at (x, y)? On the snowfield between depth 0.18 and 0.60, the ice and every prop's foot excluded (ChristmasScenery.CanStand).</summary>
    private bool Valid(float x, float y, float room)
    {
        float W = S.Width;
        if (x < 0.02f * W || x > 0.98f * W) return false;
        float wy = S.WalkY(x), d = (y - wy) / (S.Height - wy);
        if (d < 0.18f || d > 0.64f || y < S.SmallBandTop) return false;     // never in the herd's band
        // never rest BEHIND the bird table or the tiny fir (the props would hide most of the animal: it would read as missing)
        if (MathF.Abs(x - S.FeederFoot.X) < 0.075f * S.U && y <= S.FeederFoot.Y + 3f && y > S.FeederTray.At.Y) return false;
        if (MathF.Abs(x - S.SaplingFoot.X) < MathF.Max(0.04f * S.U, S.SaplingHeight * 0.45f) && y <= S.SaplingFoot.Y + 3f && y > S.SaplingFoot.Y - S.SaplingHeight) return false;
        // a burrow is a hole in the ground: nobody walks, hops or stops ON one (the hare's own dive and the fox's nosing keep their distance by their own rules)
        float bu = HU(y);
        for (int k = 0; k < _bur.Count; k++) { Bur b = _bur[k]; if (MathF.Abs(x - b.X) < 0.55f * bu && MathF.Abs(y - b.Y) < 0.35f * bu) return false; }
        return S.CanStand(x, y, room);
    }

    /// <summary>
    /// IS A BODY HALF HIDDEN BEHIND A NEARER THING? Passing behind a prop is fine; STANDING, stalking, sitting or sleeping half
    /// covered by one is not (the fox waited with its rump inside the snowman). The body is a rectangle (halfW either side of
    /// the feet, bodyH tall): sample it on a small grid against the depth picture; a painted pixel whose nearness is HIGHER than
    /// the animal's own feet is nearer, so it covers. The snowman is drawn live, not in the picture: a thing farther away than
    /// his foot must keep its x more than 0.6 of his height from his.
    /// </summary>
    private bool Covered(float x, float y, float halfW, float bodyH)
    {
        byte me = S.Nearness(y); int W = S.Width, Hh = S.Height; byte[] nd = S.NearDepth;
        for (int iy = 0; iy < 4; iy++)
            for (int ix = -2; ix <= 2; ix++)
            {
                int px = (int)(x + ix * halfW * 0.5f), py = (int)(y - 1 - iy * bodyH / 3f);
                if (px < 0 || py < 0 || px >= W || py >= Hh) continue;
                if (nd[py * W + px] > me) return true;
            }
        return y < S.SnowmanFoot.Y && MathF.Abs(x - S.SnowmanFoot.X) < 0.6f * S.SnowmanHeight;
    }

    /// <summary>A hare may rest, sit, nibble or dive here: feet allowed and the body not behind a prop.</summary>
    private bool HRest(float x, float y, float room) { float u = HU(y); return Valid(x, y, room) && !Covered(x, y, 0.5f * u, 0.9f * u); }
    /// <summary>The fox may stop, sit, watch, listen or sleep here.</summary>
    private bool FRest(float x, float y, float room) { float u = FU(y); return Valid(x, y, room) && !Covered(x, y, 0.55f * u, 0.6f * u); }

    private bool LineClear(float x0, float y0, float x1, float y1, float room)
    {
        float len = MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int n = Math.Max(1, (int)(len / 9f));
        for (int i = 1; i <= n; i++) { float t = i / (float)n; if (!Valid(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, room)) return false; }
        return true;
    }

    private void PlaceNear(float x, float spread, out float ox, out float oy, float room)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float px = x + ((float)Rng.NextDouble() - 0.5f) * 0.1f * S.U * (1 + tries / 10);
            PointF p = S.FieldPoint(px, 0.28f + 0.26f * (float)Rng.NextDouble());
            if (Valid(p.X, p.Y, room) && !Covered(p.X, p.Y, 0.5f * _hUnit[TierOf(p.Y)], 0.9f * _hUnit[TierOf(p.Y)])) { ox = p.X; oy = p.Y; return; }
        }
        PointF f = S.FieldPoint(x, 0.22f); ox = f.X; oy = f.Y;
    }

    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} foxhare {what} sim {Board.Time:F2}");

    // ================================================================ particles and tracks

    private void Emit(byte kind, float x, float y, float h, float vx, float vh, float life, int size, float op, float grav = 0f)
    {
        for (int i = 0; i < _puffs.Length; i++)
            if (!_puffs[i].On) { _puffs[i] = new Puff { X = x, Y = y, H = h, VX = vx, VH = vh, T = 0, Life = life, Size = size, Op = op, Kind = kind, Grav = grav, On = true }; return; }
    }

    /// <summary>A little cloud of snow where a hind foot lands or something dives in: n puffs thrown out and up, each sized by the lane.</summary>
    private void Burst(float x, float y, float unit, int n, float spread, float up, int size = 1)
    {
        for (int i = 0; i < n; i++)
        {
            float a = (float)(Rng.NextDouble() * 2 - 1);
            Emit(0, x + a * 0.1f * unit, y, 0.02f * unit, a * spread * unit, (0.2f + 0.8f * (float)Rng.NextDouble()) * up * unit, 0.5f + 0.4f * (float)Rng.NextDouble(), size, 0.85f, 1.6f * unit);
        }
    }

    private void Track(float x, float y, byte kind) { _decals[_decalAt] = new Decal { X = x, Y = y, Kind = kind, Born = _time }; _decalAt = (_decalAt + 1) % _decals.Length; _decalLive = Math.Min(_decals.Length, _decalLive + 1); }

    // ================================================================ the board

    private void PostFriend(H h) { Board.Post(Sense.Friend, new PointF(h.X, h.Y), 0.4f * S.U, 0.6f, h); }

    // ================================================================ UPDATE

    public override void Update(float dt)
    {
        if (_n == 0) return;
        long t0 = _firstUpdate && _log == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
        _time += dt;
        if (!_forced && _time >= (_test is "groom" or "binky" or "nibble" or "tall" or "thump" or "peek" ? 14.0 : 6.0)) { _forced = true; ForceTest(); }     // the hare tests wait until every hare has come up (the entrance is over by about 14 s)
        for (int i = 0; i < _puffs.Length; i++)
        {
            ref Puff p = ref _puffs[i];
            if (!p.On) continue;
            p.T += dt; p.X += p.VX * dt; p.H += p.VH * dt; p.VH -= p.Grav * dt;
            if (p.Kind == 1) { p.VX *= 0.97f; }
            if (p.T >= p.Life || p.H < -2f) p.On = false;
        }
        for (int i = 0; i < _n; i++) StepHare(i, dt);
        StepFox(dt);
        if (_log != null)
        {
            _tickU += System.Diagnostics.Stopwatch.GetTimestamp() - t0; _costFrames++;
            if (_time - _costAt >= 5.0 && _costFrames > 0)
            {
                double us = 1e6 / System.Diagnostics.Stopwatch.Frequency / _costFrames;
                Log($"cost per frame: update {_tickU * us:F0} us, draw {_tickD * us:F0} us, {(float)_costStamps / _costFrames:F1} stamps, {_decalLive} live decals");
                _tickU = 0; _tickD = 0; _costFrames = 0; _costStamps = 0; _costAt = _time;
            }
        }
        if (!_firstUpdate) { _firstUpdate = true; Log($"first update took {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F2} ms"); }
    }

    // ---------------------------------------------------------------- hares

    private void Show(H h, int book, int page, bool mirror)
    {
        if (_hb == null) return;
        h.Bk = _hb[book]; h.Pg = Math.Clamp(page, 0, h.Bk.Count - 1); h.Mir = mirror;
    }
    private static int Cyc(double t, float rate, int n, float off) => (int)(((t * rate + off) % n + n) % n);

    /// <summary>The doing as a viewer would name it (Look is a glance inside a Sit; Go, Hop and Land are one MOVE; thump, bolt and dive are one FLIGHT): what the mind log counts.</summary>
    private static string Coarse(H h) => h.Mind.Doing switch
    {
        HD.Sit or HD.Look => "Sit",
        HD.Go => h.GoFlee ? "Flee" : "Move",
        HD.Hop or HD.Land => "Move",
        HD.Thump or HD.Bolt or HD.Dive => "Flee",
        _ => h.Mind.Doing.ToString(),
    };

    private static bool BoltRun(H h) => h.Mind.Doing is HD.Bolt || (h.Mind.Doing == HD.Go && h.GoFlee);

    private void StartTurn(H h, int to, HD after, float quick = 1f, float afterFor = 0f)
    {
        if (to == h.Dir) return;
        h.Turning = true; h.TurnT = 0; h.TurnFrom = h.Dir; h.TurnTo = to; h.After = after; h.AfterFor = afterFor; h.Pace.Halt();
        h.TurnQuick = quick;
    }

    private void BeginHare(H h, HD d, float dur)
    {
        h.Mind.Begin(d, dur); h.Clock = 0; h.Dur = dur;
        h.ThOut = false; h.ThOutT = 0f; h.DivePuff = false; h.PkPuff1 = false; h.PkPuff2 = false; h.HopsDone = 0;
        Log($"hare{Array.IndexOf(_h, h)} {d} {dur:F1}s");
    }

    /// <summary>
    /// A SIT begins on page 1 of the sit book, which is the pose (HSit(0.05)) that every other sheet starts from and ends
    /// on (reach, groom, tall, settle, emerge): no sheet ever hands over to a sit that looks different. A glance that was
    /// asked for in the last second (and held back by a nibble or a wash) is taken now.
    /// </summary>
    private void BeginSit(H h, float dur)
    {
        BeginHare(h, HD.Sit, dur);
        h.IdleOff = 2.4f; h.Gl = 0; h.GlLevel = 0f;
        if (h.PendAt > 0 && _time - h.PendAt < 1.0) WantGlance(h, h.PendSanta);
        h.PendAt = -1;
    }

    private void WantGlance(H h, bool overhead)
    {
        h.GlWant = true; h.GlSanta = overhead;
        h.GlHold = overhead ? 3.5f + 2.5f * (float)Rng.NextDouble() : 1.4f + 1.4f * (float)Rng.NextDouble();
        h.GlTarget = overhead ? 0.8f : 0.45f;
    }

    private static bool HareCanReact(H h) => !h.GoFlee && h.Mind.Doing is HD.Sit or HD.Nibble or HD.Groom or HD.Go or HD.Hop or HD.Land or HD.Tall;

    /// <summary>A reaction asks for a response; the doing in progress finishes its own way out first (a nibble unreaches, a wash lowers its paws, a hop lands) and then the response begins.</summary>
    private void Ask(H h, Resp r, float alert)
    {
        if (h.Resp == Resp.Bolt && r != Resp.Bolt) return;
        h.Resp = r; h.RespAlert = alert;
    }

    private void ApplyResp(H h, int i)
    {
        Resp r = h.Resp; h.Resp = Resp.None;
        switch (r)
        {
            case Resp.Thump: StartThump(h, i); break;
            case Resp.Bolt: GoBolt(h, i, h.RespAlert); break;
            case Resp.Freeze:
            {
                int to = h.FreezeTo;
                float dur = 5f + 3f * (float)Rng.NextDouble();
                if (to != h.Dir) StartTurn(h, to, HD.Tall, 1f, dur); else BeginHare(h, HD.Tall, dur);
                break;
            }
        }
    }

    /// <summary>The end of a doing that ends on the sit pose: a waiting response first, else the next sit (with the glance that was held for it).</summary>
    private void AfterDoing(H h, int i, float sitFor)
    {
        if (h.Resp != Resp.None) ApplyResp(h, i); else BeginSit(h, sitFor);
    }

    /// <summary>The sit cycle's quiet moments (both ears level, eyes open): the only places a glance may begin or end, so the head-up pages join the sit pages without a pop.</summary>
    private static bool SitGate(H h)
    {
        float ph = (((h.Clock * 20f + h.IdleOff) % 48f) + 48f) % 48f / 48f;
        return ph < 0.08f || (ph > 0.34f && ph < 0.44f) || (ph > 0.86f && ph < 0.93f);
    }

    /// <summary>The fox's stand has quiet moments (the ear still, the eye open): a glance or the next doing begins there, so it joins the stand pages without a pop.</summary>
    private static bool StandGate(F f)
    {
        float ph = (((f.Clock * 16f + 4.8f) % 48f) + 48f) % 48f / 48f;
        return ph < 0.28f || (ph > 0.45f && ph < 0.68f) || ph > 0.80f;
    }

    private static bool SitGate(F f)
    {
        float ph = (((f.Clock * 15f + 3.6f) % 36f) + 36f) % 36f / 36f;
        return ph < 0.20f || (ph > 0.28f && ph < 0.55f) || ph > 0.68f;
    }

    private float SantaLevel(H h)
    {
        float dx = S.SantaSleigh.X - h.X;
        return Math.Clamp(0.30f + 0.70f * (1f - MathF.Abs(dx) / (0.55f * S.U)), 0.25f, 1f);
    }

    /// <summary>
    /// THE GLANCE: the head (and ears) only, over the sit. 1 head up and held (following the sleigh if it is Santa),
    /// 2 head down, 3 waiting for the sit's quiet moment to hand back.
    /// </summary>
    private void StepGlance(H h, float dt)
    {
        h.GlT += dt;
        if (h.Gl == 1)
        {
            float tgt = h.GlSanta && S.SantaOnScreen ? SantaLevel(h) : h.GlTarget;
            tgt = Math.Clamp(tgt + 0.15f * MathF.Sin(h.GlT * 3.4f), 0f, 1f);          // the held head still drifts a little: a glance is never a statue
            float want = (tgt - h.GlLevel) * (1f - MathF.Exp(-4.0f * dt));
            float cap = 1.6f * dt;
            h.GlLevel += Math.Clamp(want, -cap, cap);
            if (h.GlT > h.GlHold || h.Resp != Resp.None) h.Gl = 2;
        }
        else if (h.Gl == 2)
        {
            h.GlLevel -= Math.Min(h.GlLevel, Math.Max(0.5f * dt, h.GlLevel * (1f - MathF.Exp(-5f * dt))));
            if (h.GlLevel < 0.02f) { h.GlLevel = 0f; h.Gl = 3; }
        }
        else if (SitGate(h)) h.Gl = 0;
    }

    private void StepHare(int i, float dt)
    {
        H h = _h[i];
        float U = S.U, W = S.Width;
        h.Mind.Tick(dt); h.Clock += dt; h.Att.Tick(dt); h.ThumpCool -= dt; h.BreathT -= dt; h.GlanceCool -= dt;
        h.Gx = 0f; h.Gy = 0f;
        float unit = HU(h.Y);
        bool mir = h.Dir < 0;
        h.Vis = h.Mind.Doing != HD.Under || h.HidePk;
        HD d = h.Mind.Doing;
        h.Perf = d is HD.Dive or HD.Peek or HD.Dig || (d == HD.Under && h.HidePk);

        if (h.Turning)
        {
            // A REVERSAL IS A TURN: the sat hare turns through four drawn views (side, three-quarter, almost head on, head on) and out the other side.
            float dur = h.TurnQuick < 1f ? QuickTurn : TurnFor;
            h.TurnT += dt;
            float f = Math.Clamp(h.TurnT / dur, 0f, 0.999f);
            // 14 views from side-on to head-on (6.4 degrees a view), then 14 back out the other side: the head-on view is the middle
            bool first = f < 0.5f; float uu = first ? f * 2f : (1f - f) * 2f;
            Show(h, HB_Turn, Math.Clamp((int)(uu * 14f), 0, 13), first ? h.TurnFrom < 0 : h.TurnTo < 0);
            if (!first) h.Dir = h.TurnTo;
            if (h.TurnT >= dur) { h.Turning = false; h.Dir = h.TurnTo; if (h.After == HD.Sit) BeginSit(h, 1f); else if (h.After == HD.Nibble) StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); else BeginHare(h, h.After, h.AfterFor); }
            S.Occupy(this, i, h.X, h.Y, 0.5f * unit);
            LogHare(i, h, dt);
            return;
        }

        // ---- listening: another hare's thump, then the board ----
        if (HareCanReact(h))
        {
            foreach (Notice n in Board.Live)
            {
                if (n.Kind != Sense.Friend || n.Who is not H from || from == h || n.Posted <= h.HeardAt) continue;
                float dx = n.At.X - h.X, dy = n.At.Y - h.Y;
                if (MathF.Sqrt(dx * dx + dy * dy) > n.Reach) continue;
                h.HeardAt = n.Posted; h.FearX = from.FearX; h.FearY = from.FearY; h.FearFox = true;
                Log($"hare{i} hears thump from hare{Array.IndexOf(_h, from)}: sits tall, bolts");
                Ask(h, Resp.Bolt, 0.5f + 0.5f * (float)Rng.NextDouble() * (1f - h.Timid));
                break;
            }
            Reaction r = h.Att.Check(Board, new PointF(h.X, h.Y), h, out Notice cause);
            if (r != Reaction.None)
            {
                bool fox = cause.Who is F;
                bool overhead = cause.At.Y < h.Y - 0.18f * S.Height;
                Log($"hare{i} feels {r} from {cause.Kind} at {cause.At.X:F0},{cause.At.Y:F0}{(fox ? " (the fox)" : "")}");
                h.FearX = cause.At.X; h.FearY = cause.At.Y; h.FearFox = fox || cause.Kind == Sense.Threat;
                float dist = MathF.Abs(cause.At.X - h.X);
                if (r == Reaction.Flee)
                {
                    if (cause.Kind == Sense.Threat && h.ThumpCool <= 0f && dist > 0.10f * U && !Lull) Ask(h, Resp.Thump, 0f);
                    else Ask(h, Resp.Bolt, 0f);
                }
                else if (r == Reaction.Freeze)
                {
                    h.FreezeTo = !overhead && cause.At.X < h.X ? -1 : !overhead ? 1 : h.Dir;
                    int recent = (_time - h.Fz1 < 20.0 ? 1 : 0) + (_time - h.Fz2 < 20.0 ? 1 : 0);
                    if (recent >= 2 && !overhead) { Log($"hare{i} has frozen twice in 20 s: it bolts"); Ask(h, Resp.Bolt, 0.5f); }
                    else { h.Fz2 = h.Fz1; h.Fz1 = _time; Ask(h, Resp.Freeze, 0f); }
                }
                else if (r == Reaction.Look && (h.GlanceCool <= 0f || !overhead))
                {
                    // A LOOK is the HEAD, taken at a natural break: at once over a sit, at the next bite of a nibble
                    // (held up to a second), never in the middle of a wash, a hop or a stand.
                    if (overhead) h.GlanceCool = 14f + 14f * (1f - h.Curious);
                    if (d == HD.Sit) { if (!h.GlWant && h.Gl == 0) WantGlance(h, overhead); }
                    else if (d == HD.Nibble) { h.PendAt = _time; h.PendSanta = overhead; WantGlance(h, overhead); h.GlWant = false; }
                }
            }
        }

        switch (d)
        {
            case HD.Sit:
            {
                if (h.Gl == 0 && h.GlWant && SitGate(h)) { h.Gl = 1; h.GlWant = false; h.GlT = 0f; h.GlLevel = 0f; }
                if (h.Gl != 0) { StepGlance(h, dt); Show(h, HB_Look, (int)MathF.Round(h.GlLevel * 8f), mir); }
                else Show(h, HB_Sit, Cyc(h.Clock, 20f, 48, h.IdleOff), mir);
                if (h.BreathT <= 0f && _hb != null && h.Gl == 0) { PointF nz = _hb[HB_Sit].Aux[h.Pg]; Emit(1, h.X + h.Dir * nz.X * unit, h.Y, nz.Y * unit, h.Dir * 0.05f * unit, 0.06f * unit, 1.1f, 0, 0.45f); h.BreathT = 5f + 4f * (float)Rng.NextDouble(); }
                if (h.Gl == 0 && h.Resp != Resp.None) ApplyResp(h, i);
                else if (h.Gl == 0 && h.Mind.Over && SitGate(h)) RollHare(h, i);          // a sit is left only at a quiet page (ears level, eyes open)
                break;
            }
            case HD.Groom:
            {
                // A SCRIPT OF LOOPS (see FoxAndHaresArt.HGroom2): lead-in, wash cycles, up to the ear, lick cycles, release.
                // A reaction waits for the end of a cycle and takes the short way out (a wash) or the release (a lick).
                float was = h.GT; h.GT += dt;
                int pg; bool quit = h.Resp != Resp.None;
                switch (h.GStage)
                {
                    case 0: pg = FoxAndHaresArt.GrLead + Math.Min(11, (int)(h.GT / 0.4f * 12f)); if (h.GT >= 0.4f) { h.GStage = 1; h.GT = 0f; } break;
                    case 1:
                    {
                        float fr = h.GT / 0.6f; pg = FoxAndHaresArt.GrWash + Math.Min(17, (int)((fr - MathF.Floor(fr)) * 18f));
                        if (fr >= h.GN) { h.GStage = 2; h.GT = 0f; pg = FoxAndHaresArt.GrWash; }
                        else if (quit && MathF.Floor(fr) > MathF.Floor(was / 0.6f)) { h.GStage = 5; h.GT = 0f; pg = FoxAndHaresArt.GrWash; }
                        break;
                    }
                    case 2: pg = FoxAndHaresArt.GrToPull + Math.Min(11, (int)(h.GT / 0.4f * 12f)); if (h.GT >= 0.4f) { h.GStage = 3; h.GT = 0f; } break;
                    case 3:
                    {
                        float fr = h.GT / 0.5f; pg = FoxAndHaresArt.GrLick + Math.Min(14, (int)((fr - MathF.Floor(fr)) * 15f));
                        if (fr >= h.GM || (quit && MathF.Floor(fr) > MathF.Floor(was / 0.5f))) { h.GStage = 4; h.GT = 0f; pg = FoxAndHaresArt.GrLick; }
                        break;
                    }
                    case 4: pg = FoxAndHaresArt.GrRelease + Math.Min(11, (int)(h.GT / 0.4f * 12f)); if (h.GT >= 0.4f) { AfterActivity(h, i, HD.Groom); } break;
                    default: pg = FoxAndHaresArt.GrOut + Math.Min(8, (int)(h.GT / 0.3f * 9f)); if (h.GT >= 0.3f) { AfterActivity(h, i, HD.Groom); } break;
                }
                Show(h, HB_Groom, pg, mir);
                break;
            }
            case HD.Nibble:
            {
                // reach (0.45 s), bites in cycles of 12 pages, a glance up at a cycle's end if one is waiting, unreach (0.45 s)
                h.NT += dt;
                switch (h.NStage)
                {
                    case 0:
                    {
                        h.NRp = Math.Min(13, (int)(h.NT / 0.45f * 14f));
                        Show(h, HB_Reach, h.NRp, mir);
                        if (h.Resp != Resp.None) { h.NStage = 5; h.NT = 0f; }
                        else if (h.NT >= 0.45f) { h.NStage = 1; h.NT = 0f; h.NPg = -1; }
                        break;
                    }
                    case 1:
                    {
                        int pg = Cyc(h.NT, 28f, 24, 0f);
                        bool wrapped = pg < h.NPg;
                        h.NPg = pg;
                        Show(h, HB_Nibble, pg, mir);
                        if (wrapped)
                        {
                            if (h.PendAt > 0 && _time - h.PendAt < 1.0) { h.NStage = 2; h.NT = 0f; h.PendAt = -1; h.NHold = 1.1f + 1.4f * (float)Rng.NextDouble(); }
                            else if (h.Resp != Resp.None || h.Clock >= h.Dur) { h.NStage = 5; h.NT = 0f; h.NRp = 13; }
                        }
                        break;
                    }
                    case 2: Show(h, HB_NLook, Math.Min(9, (int)(h.NT / 0.33f * 10f)), mir); if (h.NT >= 0.33f) { h.NStage = 3; h.NT = 0f; } break;
                    case 3: Show(h, HB_NLook, 10 + (int)(h.NT * 10f) % 12, mir); if (h.NT >= h.NHold || h.Resp != Resp.None) { h.NStage = 4; h.NT = 0f; } break;
                    case 4: Show(h, HB_NLook, 8 - Math.Min(8, (int)(h.NT / 0.28f * 9f)), mir); if (h.NT >= 0.28f) { h.NStage = 1; h.NT = 0f; h.NPg = -1; } break;
                    default:
                    {
                        int pg = Math.Max(0, h.NRp - Math.Min(h.NRp, (int)(h.NT / 0.45f * 14f)));
                        Show(h, HB_Reach, pg, mir);
                        if (h.NT >= 0.45f * h.NRp / 13f) AfterActivity(h, i, HD.Nibble);
                        break;
                    }
                }
                break;
            }
            case HD.Tall:
            {
                // up (0.35 s), a held stand (the ears swivel, the nose works), down (0.35 s). The way down begins only when the
                // hold is on a quiet page (ears level, eyes open: pages 0, 5, 11) so it joins the stand-up pages without a pop.
                int hp = Cyc(h.Clock, 16f, 24, 0f);
                if (!h.ThOut && h.Clock >= 0.35f && ((h.Resp != Resp.None && !h.BoltPending) || h.Clock >= h.Dur - 0.35f) && hp is 0 or 1 or 10 or 22 or 23) { h.ThOut = true; h.ThOutT = 0f; }
                if (h.Clock < 0.35f) Show(h, HB_TallIn, Math.Min(11, (int)(h.Clock / 0.35f * 12f)), mir);
                else if (!h.ThOut) Show(h, HB_Tall, hp, mir);
                else { h.ThOutT += dt; Show(h, HB_TallIn, Math.Max(0, 11 - (int)(h.ThOutT / 0.35f * 12f)), mir); }
                if (h.ThOut && h.ThOutT >= 0.35f) { if (h.BoltPending) StartBoltNow(h, h.BoltDir, i); else AfterDoing(h, i, SitFor()); }
                break;
            }
            case HD.Thump:
            {
                // tall (0.35 s), then the thump book over 1.2 s (the foot comes down at 0.4 s and at 0.9 s into it), then it DROPS
                // to all fours (0.24 s, the tall pages backwards) and bolts
                h.Pace.Halt();
                int tpg = Math.Clamp((int)((h.Clock - 0.35f) / 1.2f * 36f), 0, 35);
                bool footLow = FoxAndHaresArt.ThumpFoot(tpg / 35f) < 0.04f;
                if (!h.ThOut)
                {
                    if (h.Clock < 0.35f) Show(h, HB_TallIn, Math.Min(11, (int)(h.Clock / 0.35f * 12f)), mir);
                    else Show(h, HB_Thump, tpg, mir);
                }
                int due = h.Clock >= 0.35f + 0.9f ? 2 : h.Clock >= 0.35f + 0.4f ? 1 : 0;
                while (h.Thumps < due)
                {
                    h.Thumps++;
                    PostFriend(h);
                    Burst(h.X - h.Dir * 0.18f * unit, h.Y, unit, 3, 0.25f, 0.18f, 0);
                    Log($"hare{i} THUMP {h.Thumps} (Friend, reach {0.4f * U:F0} px)");
                }
                // the fox coming within about three metres is no time for theatre (Pack's TooClose): it runs at the next quiet foot
                bool foxNear = _fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - h.X) < 0.17f * U && MathF.Abs(_fox.Y - h.Y) < 0.1f * U;
                if (!h.ThOut && (h.Clock >= 1.55f || (foxNear && h.Clock > 0.5f && footLow))) { h.ThOut = true; h.ThOutT = 0f; }
                if (h.ThOut)
                {
                    h.ThOutT += dt;
                    Show(h, HB_TallIn, Math.Max(0, 11 - (int)(h.ThOutT / 0.24f * 12f)), mir);
                    if (h.ThOutT >= 0.24f) { h.ThumpCool = 20f; if (_fox != null) { h.FearX = _fox.X; h.FearY = _fox.Y; } GoBolt(h, i, 0f); }
                }
                break;
            }
            case HD.Go:
            {
                // setting off: the settle pages backwards (the sit rising into the gather), then the hop or the bolt from the same rest page
                int np = 8;
                Show(h, h.GoFlee ? HB_SettleF : HB_Settle, Math.Max(0, np - 1 - (int)(h.Clock / 0.24f * np)), mir);
                if (h.Clock >= 0.24f)
                {
                    h.Odo = FoxAndHaresArt.HopRest; h.LastFrac = FoxAndHaresArt.HopRest;
                    if (h.GoFlee && h.DigPending) { h.DigPending = false; StartDig(h, i); }
                    else if (h.GoFlee) { h.Mind.Begin(HD.Bolt, 12f); h.Clock = 0; h.Dur = 12f; h.HopsDone = 0; Log($"hare{i} bolts to a burrow at {h.BX:F0},{h.BY:F0}"); }
                    else { h.Mind.Begin(HD.Hop, Mind<HD>.Forever); h.Clock = 0; h.HopsDone = 0; }
                }
                break;
            }
            case HD.Hop:
            {
                // THE LOLLOP: slow, with a stop-start in every hop (slowest in the gather, quickest in the stretch). The page follows the
                // DISTANCE walked in hop cycles, so the planted feet never skate.
                float frac = h.Odo - MathF.Floor(h.Odo);
                float mult = 0.55f + 0.85f * MathF.Sin(MathF.PI * ((frac + 0.05f) % 1f));
                float step = HopBase * mult * unit * dt;
                float cyc = FoxAndHaresArt.HareHopD * unit * h.HopK;
                h.Odo += step / cyc; h.X += h.Dir * step;
                float dxLeft = MathF.Max(1f, (h.GX - h.X) * h.Dir);
                h.Y += Math.Clamp((h.GY - h.Y) * Math.Min(1f, step / dxLeft), -0.6f * step, 0.6f * step);
                float f2 = h.Odo - MathF.Floor(h.Odo);
                Show(h, HB_Hop, (int)(f2 * 24f) % 24, mir);
                HareLandings(h, f2, unit, FoxAndHaresArt.HareHopD, false);
                // at every rest page: is somebody standing where the next hop would land, or has a reaction asked for a stop? Then this is the last hop.
                int crossed = (int)MathF.Floor(h.Odo - FoxAndHaresArt.HopRest);
                if (crossed > h.HopsDone)
                {
                    h.HopsDone = crossed;
                    if (crossed < h.Hops && (h.Resp != Resp.None || S.Crowded(h.X + h.Dir * cyc * 0.9f, h.Y, 0.5f * unit, this, i))) h.Hops = crossed;
                }
                if (h.Odo >= FoxAndHaresArt.HopRest + h.Hops) { h.Pace.Halt(); BeginHare(h, HD.Land, 0.24f); }
                break;
            }
            case HD.Land:
            {
                Show(h, h.GoFlee ? HB_SettleF : HB_Settle, Math.Min(7, (int)(h.Clock / 0.24f * 8f)), mir);
                if (h.Mind.Over)
                {
                    h.GoFlee = false;
                    if (h.Resp != Resp.None) { h.Next = HD.Sit; ApplyResp(h, i); }
                    else if (h.Next != HD.Sit) { HD nx = h.Next; h.Next = HD.Sit; StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); }
                    else AfterActivity(h, i, HD.Hop);
                }
                break;
            }
            case HD.Binky:
            {
                // a joyful leap: 1.1 s, drifting forward a little; lands with a puff and a few sparkles
                float f = Math.Min(0.999f, h.Clock / 1.1f);
                Show(h, HB_Binky, Math.Min(35, (int)(f * 36f)), mir);
                { PointF gl = h.Bk!.GlideAt(f, h.Pg); h.Gx = gl.X; h.Gy = gl.Y; }
                float prev = Math.Min(0.999f, (h.Clock - dt) / 1.1f);
                float adv = (FoxAndHaresArt.Sm(f) - FoxAndHaresArt.Sm(prev)) * 0.9f * unit;
                float nx = h.X + h.Dir * adv;
                if (Valid(nx, h.Y, 0.3f * unit) && !S.Crowded(nx, h.Y, 0.5f * unit, this, i)) h.X = nx;
                if (prev < 0.78f && f >= 0.78f) { Burst(h.X + h.Dir * 0.3f * unit, h.Y, unit, 5, 0.35f, 0.3f, 1); for (int k = 0; k < 3; k++) Emit(3, h.X + h.Dir * (0.1f + 0.2f * k) * unit, h.Y, 0.15f * unit, (k - 1) * 0.3f * unit, 0.4f * unit, 0.5f, 0, 0.9f, 0.7f * unit); Track(h.X + h.Dir * 0.3f * unit, h.Y, 0); }
                if (h.Clock >= 1.1f) AfterDoing(h, i, SitFor());
                break;
            }
            case HD.Bolt:
            {
                // BOLT: a fast zigzag (the lane wanders) to ITS burrow; the pace eases up under a cap, never starts at full speed.
                // The run was PLANNED in GoBolt: a whole number of bounds (h.Hops), each a touch stretched or squeezed (h.HopK), so the
                // n-th gathered rest page lands the hare exactly DiveRun short of the hole. It dives there, from the rest page.
                h.Pace.Want = BoltSpeed * unit;
                float step = h.Pace.Tick(dt);
                float cyc = FoxAndHaresArt.HareFleeD * unit * h.HopK;
                h.Odo += step / cyc; h.X += h.Dir * step;
                h.JinkT -= dt;
                float toB = (h.BX - h.X) * h.Dir;
                if (h.JinkT <= 0f)
                {
                    h.JinkT = 0.25f + 0.25f * (float)Rng.NextDouble();
                    float lastLeg = 1.6f * FoxAndHaresArt.HareFleeD * unit;
                    float amp = Math.Clamp(toB / lastLeg, 0f, 1f);                      // the zigzag dies away as the hole comes up, so the last bounds run straight at it
                    float ny = h.Y + ((float)Rng.NextDouble() * 2f - 1f) * 0.045f * amp * (S.Height - S.WalkY(h.X));
                    if (toB < lastLeg) ny = h.Y + (h.BY - h.Y) * Math.Clamp(1f - toB / lastLeg, 0f, 1f);
                    if (Valid(h.X + h.Dir * 0.08f * U, ny, 0.3f * unit) && LineClear(h.X, ny, h.BX, h.BY, 0.3f * unit)) h.GY = ny;
                }
                h.Y += Math.Clamp(h.GY - h.Y, -0.55f * step, 0.55f * step);
                float f2 = h.Odo - MathF.Floor(h.Odo);
                Show(h, HB_Flee, (int)(f2 * 16f) % 16, mir);
                bool rest = h.LastFrac < 0.9375f && f2 >= 0.9375f && f2 - h.LastFrac < 0.5f;
                HareLandings(h, f2, unit, FoxAndHaresArt.HareFleeD, true);
                if (rest && ++h.HopsDone >= h.Hops) BeginDive(h, i);
                break;
            }
            case HD.Dive:
            {
                // the sheet carries the whole motion; the sprite's origin is the HOLE. The burrow's two pieces are stamped round it (DrawOne2):
                // the back before the page, the front lip after it, so whatever is lower than the lip's top is really hidden by snow in front.
                float f = Math.Min(0.999f, h.Clock / FoxAndHaresArt.DiveSecs);
                {
                    int pg = Math.Min(FoxAndHaresArt.DiveN - 1, (int)MathF.Round(f * (FoxAndHaresArt.DiveN - 1))); Show(h, HB_Dive, pg, mir); PointF gl = h.Bk!.GlideAt(f, pg);
                    float blend = 1f - FoxAndHaresArt.Sm(f / 0.30f);                       // the last bound's arrival error (a few pixels) melts away over the first 0.3 s
                    h.Gx = gl.X + h.DOx * blend; h.Gy = gl.Y + h.DOy * blend;
                }
                if (!h.DivePuff && f >= 0.46f)
                {
                    h.DivePuff = true;
                    Burst(h.X + h.Dir * 0.10f * unit, h.Y, unit, 8, 0.45f, 0.55f, 1);        // the nose goes in: snow jumps off the lip
                    Log($"hare{i} dives into its burrow at {h.X:F0},{h.Y:F0}");
                }
                if (!h.PkPuff1 && f >= 0.68f) { h.PkPuff1 = true; Burst(h.X, h.Y, unit, 5, 0.35f, 0.40f, 0); }       // the hind feet kick
                if (h.Clock >= FoxAndHaresArt.DiveSecs)
                {
                    h.Mind.Begin(HD.Under, 7f + 8f * (float)Rng.NextDouble()); h.Clock = 0; h.Dur = 9f; h.Vis = false; h.DivePuff = false; h.PkPuff1 = false;
                    PickEmerge(h); h.NextPeek = _time + 3.5 + 3.0 * Rng.NextDouble(); h.HidePk = false;
                }
                break;
            }
            case HD.Dig:
            {
                // a hare with no burrow in reach digs one: see StartDig. 10 pages in (the gather to the dig crouch), TWO scrape cycles of
                // 24 pages (0.7 s each), 10 pages out (back to the gather pose the dive starts from); the hole opens in 12 steps as it goes.
                const float T1 = 10f / 30f, T2 = 2f * 24f / 34f, T3 = 10f / 30f;
                float c = h.Clock; int pg; int cycPg = -1;
                if (c < T1) pg = (int)(c / T1 * 10f);
                else if (c < T1 + T2) { cycPg = (int)((c - T1) / T2 * 48f) % 24; pg = 10 + cycPg; }
                else pg = 34 + Math.Min(9, (int)((c - T1 - T2) / T3 * 10f));
                Show(h, HB_Dig, pg, mir);
                h.OpenPg = Math.Clamp((int)(c / (T1 + T2 + T3) * FoxAndHaresArt.OpenN), 0, FoxAndHaresArt.OpenN - 1); _bur[h.Cur].Open = h.OpenPg;
                // the paw throws snow BACK out of the hole at the two strokes of every cycle: a fan of clods and powder over the shoulder
                bool stroke = cycPg == 5 || cycPg == 17;
                if (stroke)
                {
                    if (!h.DivePuff)
                    {
                        h.DivePuff = true;
                        for (int k = 0; k < 11; k++) Emit(0, h.X + h.Dir * 0.60f * unit, h.Y, 0.10f * unit, -h.Dir * (0.35f + 0.9f * (float)Rng.NextDouble()) * unit, (0.7f + 0.9f * (float)Rng.NextDouble()) * unit, 0.55f + 0.2f * (float)Rng.NextDouble(), k % 3 == 0 ? 1 : 0, 0.95f, 2.6f * unit);
                        Burst(h.X + h.Dir * 0.55f * unit, h.Y, unit, 4, 0.5f, 0.4f, 1);
                    }
                }
                else h.DivePuff = false;
                if (c >= T1 + T2 + T3) { _bur[h.Cur].Open = FoxAndHaresArt.OpenN - 1; h.OpenPg = FoxAndHaresArt.OpenN - 1; h.DivePuff = false; BeginDive(h, i); }
                break;
            }
            case HD.Under:
            {
                // Underground: nothing is drawn but the burrow itself, except now and then (every 6 to 15 s) when the ears, then the eyes, come up
                // to the lip, look round and sink again: somebody is always in there. It surfaces only when the fox is far.
                Bur b = _bur[h.Cur]; h.X = b.X; h.Y = b.Y;
                bool foxNear = _fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - b.X) < (_fox.Mind.Doing == FD.Asleep ? 0.12f : 0.30f) * U;
                if (h.HidePk)
                {
                    // ears up (1.0 s, eased), a held look with the head breathing (0.6 to 1.4 s), sinking (0.9 s): pages 0..30 of the climb-out sheet
                    h.PkT += dt;
                    const float up = 1.0f, down = 0.9f;
                    int pg;
                    if (h.PkT < up) pg = (int)MathF.Round(FoxAndHaresArt.Sm(h.PkT / up) * 30f);
                    else if (h.PkT < up + h.PkHold) pg = 30 - (int)MathF.Round(2.4f * (0.5f - 0.5f * MathF.Cos(MathF.Tau * (h.PkT - up) / h.PkHold)));
                    else pg = 30 - (int)MathF.Round(FoxAndHaresArt.Sm((h.PkT - up - h.PkHold) / down) * 30f);
                    Show(h, HB_Peek, pg, h.EDir < 0);
                    if (h.PkT >= up + h.PkHold + down) { h.HidePk = false; h.NextPeek = _time + 6.0 + 9.0 * Rng.NextDouble(); h.Vis = false; }
                    else h.Vis = true;
                    break;
                }
                h.Vis = false;
                if (h.Mind.Over && foxNear && h.Clock < 40f) PickEmerge(h);       // the fox is near the hole it meant to come up at: choose another
                b = _bur[h.Cur];
                int mi = Array.IndexOf(_h, h);
                bool crowded = S.Crowded(b.X, b.Y, 0.5f * unit + 0.9f * unit, this, mi) || S.Crowded(b.X + h.EDir * 0.85f * unit, b.Y, 0.5f * unit, this, mi);
                if (h.Mind.Over && crowded && h.Clock < 60f) { PickEmerge(h); h.Mind.Left = 1.0f; }
                else if (h.Mind.Over && (!foxNear || h.Clock >= 40f))
                {
                    h.Dir = h.EDir; h.Vis = true;
                    h.Mind.Begin(HD.Peek, Mind<HD>.Forever); h.Clock = 0;
                    Burst(h.X, h.Y, unit, 3, 0.2f, 0.2f, 0);
                    Show(h, HB_Peek, 0, h.Dir < 0);
                    Log($"hare{i} climbs out of its burrow at {h.X:F0},{h.Y:F0}");
                }
                else if (!h.Mind.Over && _time >= h.NextPeek && !foxNear && h.Mind.Left > 4f && _hb != null)
                {
                    Log($"hare{i} peeks out of its burrow");
                    h.HidePk = true; h.PkT = 0f; h.PkHold = 0.6f + 0.8f * (float)Rng.NextDouble(); h.Vis = true;
                    Show(h, HB_Peek, 0, h.EDir < 0);
                }
                break;
            }
            case HD.Peek:
            {
                // ears, eyes, head, a look round, shoulders, a hop OUT and a settle (FoxAndHaresArt.HEmerge, 2.4 s, anchored at the hole)
                // the sheet has 40 pages on a time map: slow through the ears, eyes and head (the first 60 percent of the time covers
                // 62 percent of the pages), quick through the hop out (22 percent of the time, 31 percent of the pages: about 21 pages a second)
                float c = Math.Min(0.999f, h.Clock / 2.6f);
                float u = c < 0.60f ? c / 0.60f * 0.62f : c < 0.82f ? 0.62f + (c - 0.60f) / 0.22f * 0.26f : 0.88f + (c - 0.82f) / 0.18f * 0.12f;
                { int pg = Math.Min(71, (int)MathF.Round(u * 71f)); Show(h, HB_Peek, pg, mir); PointF gl = h.Bk!.GlideAt(u, pg); h.Gx = gl.X; h.Gy = gl.Y; }
                if (!h.PkPuff1 && u >= 0.70f) { h.PkPuff1 = true; Burst(h.X, h.Y, unit, 5, 0.35f, 0.35f, 1); }
                if (!h.PkPuff2 && u >= 0.88f) { h.PkPuff2 = true; Burst(h.X + h.Dir * 0.85f * unit, h.Y, unit, 3, 0.25f, 0.2f, 0); }
                if (h.Clock >= 2.6f)
                {
                    h.X += h.Dir * 0.85f * unit;                 // the sheet's last page has the body 0.85 ahead of the hole: the origin moves to the body
                    if (h.Cur >= 0 && _bur[h.Cur].Who == i) _bur[h.Cur].Who = -1;          // the hole is free again (it stays the hare's own)
                    BeginSit(h, 4f + 5f * (float)Rng.NextDouble());
                    Show(h, HB_Sit, 2, mir);                      // and the sit page is on the frame at once (the origin and the page change together)
                }
                break;
            }
        }
        if (h.Vis && h.Mind.Doing != HD.Under) S.Occupy(this, i, h.X, h.Y, 0.5f * unit);
        LogHare(i, h, dt);
    }

    private float SitFor() => 7f + 9f * (float)Rng.NextDouble();

    /// <summary>The marks a lollop leaves: where the forefeet and then the hind feet land, a track pair at each, and a small puff at the hind landing.</summary>
    private void HareLandings(H h, float f, float unit, float D, bool flee)
    {
        float last = h.LastFrac;
        float fl = (flee ? 0.46f * 1.22f : 0.46f), hl = (flee ? 0.22f * 1.22f : 0.22f);
        if (last < 0.52f && f >= 0.52f && f - last < 0.5f) { Track(h.X + h.Dir * fl * unit, h.Y, 1); Track(h.X + h.Dir * (fl - 0.1f) * unit, h.Y + 0.02f * unit, 1); }
        if (last < 0.88f && f >= 0.88f && f - last < 0.5f)
        {
            Track(h.X + h.Dir * hl * unit, h.Y - 0.03f * unit, 0); Track(h.X + h.Dir * (hl + 0.04f) * unit, h.Y + 0.05f * unit, 0);
            Burst(h.X + h.Dir * hl * unit, h.Y, unit, flee ? 3 : 2, 0.2f, flee ? 0.3f : 0.18f, 0);
        }
        h.LastFrac = f;
    }

    /// <summary>
    /// The mind log's line for a hare. "c" is the doing as a viewer names it. "pj" appears on a frame where the page changed:
    /// how far (pixels) the pose moved relative to the body since the last frame, from a few body points of the old and the
    /// new page: a cycling page moves a few pixels, a pop moves a dozen.
    /// </summary>
    private void LogHare(int i, H h, float dt)
    {
        if (_log != null)
        {
            float sp = dt > 0 ? MathF.Sqrt((h.X - h.LX) * (h.X - h.LX) + (h.Y - h.LY) * (h.Y - h.LY)) / dt : 0f;
            string pj = "";
            float unit = HU(h.Y);
            if (h.Vis && h.PVis && h.Bk != null && h.PBk != null && h.Bk.Keys[h.Pg].Length > 1 && h.PBk.Keys[h.PPg].Length > 1)
            {
                // the speed of the body AS DRAWN (the sprite's origin may hop between sheets without the body moving)
                float sb2 = h.Mir ? -1f : 1f, sa2 = h.PMir ? -1f : 1f;
                float bx2 = h.X + sb2 * h.Bk.Keys[h.Pg][1].X * unit, by2 = h.Y - h.Bk.Keys[h.Pg][1].Y * unit;
                float ax2 = h.PX + sa2 * h.PBk.Keys[h.PPg][1].X * h.PUnit, ay2 = h.PY - h.PBk.Keys[h.PPg][1].Y * h.PUnit;
                pj = $" dv {MathF.Sqrt((bx2 - ax2) * (bx2 - ax2) + (by2 - ay2) * (by2 - ay2)) / MathF.Max(dt, 1e-4f):F0}";
            }
            if (h.Vis && h.PVis && h.Bk != null && h.PBk != null && (h.Bk != h.PBk || h.Pg != h.PPg))
            {
                PointF[] a = h.PBk.Keys[h.PPg], b = h.Bk.Keys[h.Pg];
                if (a.Length > 1 && a.Length == b.Length)
                {
                    float sa = h.PMir ? -1f : 1f, sb = h.Mir ? -1f : 1f, worst = 0f;
                    for (int k = 2; k < a.Length; k++)
                    {
                        float ax = sa * (a[k].X - a[1].X), ay = -(a[k].Y - a[1].Y), bx = sb * (b[k].X - b[1].X), by = -(b[k].Y - b[1].Y);
                        // the body point itself moves by its own steps between pages (a run): compare the SHAPE, body to point
                        worst = MathF.Max(worst, MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) * unit);
                    }
                    // the head and body too (their offset from the body's centre)
                    { float ax = sa * (a[0].X - a[1].X), ay = -(a[0].Y - a[1].Y), bx = sb * (b[0].X - b[1].X), by = -(b[0].Y - b[1].Y); worst = MathF.Max(worst, MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) * unit); }
                    // the body's own jump in the world: where the body centre was drawn, then and now (the sheet anchors differ)
                    float wax = h.PX + sa * a[1].X * h.PUnit, way = h.PY - a[1].Y * h.PUnit, wbx = h.X + sb * b[1].X * unit, wby = h.Y - b[1].Y * unit;
                    float bodyJump = MathF.Sqrt((wax - wbx) * (wax - wbx) + (way - wby) * (way - wby));
                    pj += $" pj {worst:F1} bj {bodyJump:F1}";
                }
            }
            _log.WriteLine($"{_time,8:F3} hare{i} x {h.X:F1} y {h.Y:F1} v {sp:F1} {h.Mind.Doing}{(h.Turning ? "/Turn" : "")} {h.Bk?.Name}#{h.Pg} dir {h.Dir}{(h.Vis ? "" : " hidden")} c {Coarse(h)}{pj} cv {(h.Vis && Covered(h.X, h.Y, 0.5f * unit, 0.9f * unit) ? 1 : 0)} sim {Board.Time:F2}");
        }
        h.LX = h.X; h.LY = h.Y;
        h.PBk = h.Bk; h.PPg = h.Pg; h.PMir = h.Mir; h.PX = h.X; h.PY = h.Y; h.PUnit = HU(h.Y); h.PVis = h.Vis;
    }

    /// <summary>What next for a sitting hare: nibble, groom, hop on, stand tall, binky, or sit on (a longer sit).</summary>
    private void RollHare(H h, int i)
    {
        bool lull = Lull;
        int twig = NearestTwig(h);
        bool pal = false;
        for (int j = 0; j < _n; j++) if (j != i && MathF.Abs(_h[j].X - h.X) < 0.35f * S.U && _h[j].Mind.Doing is not (HD.Under or HD.Peek)) pal = true;
        // a sit is the rest between doings: now it travels, forages (at a twig, or at the snow itself), washes, stands tall, or plays
        int face = Dice.Pick(Rng, [0.0f, 0.28f, 0.18f, lull ? 0.05f : 0.38f, 0.05f, lull ? 0f : 0.03f + (pal ? 0.20f : 0.03f) * h.Play]);
        if (Covered(h.X, h.Y, 0.5f * HU(h.Y), 0.9f * HU(h.Y))) face = 3;                // sitting half behind a prop: travel to a clear spot first
        switch (face)
        {
            case 1: if (twig >= 0) StartNibbleTrip(h, twig); else StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); break;
            case 2: StartGroom(h); break;
            case 3: StartHop(h, i); break;
            case 4: BeginHare(h, HD.Tall, 5f + 3f * (float)Rng.NextDouble()); break;
            case 5: BeginHare(h, HD.Binky, 1.1f); break;
            default: BeginSit(h, SitFor()); break;
        }
    }

    private float SitAfter() => 5f + 5f * (float)Rng.NextDouble();

    /// <summary>What follows a doing that was an activity: another activity (a move into a nibble, a nibble into a wash) or a short sit, so the day is mostly doing things.</summary>
    private void AfterActivity(H h, int i, HD was)
    {
        if (h.Resp != Resp.None) { ApplyResp(h, i); return; }
        float r = (float)Rng.NextDouble(); bool lull = Lull;
        switch (was)
        {
            case HD.Hop: if (r < 0.50f) StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); else if (r < 0.72f) StartGroom(h); else BeginSit(h, SitAfter()); break;
            case HD.Nibble: if (r < 0.35f) StartGroom(h); else if (r < 0.58f && !lull) StartHop(h, i); else BeginSit(h, SitAfter()); break;
            default: if (r < 0.40f && !lull) StartHop(h, i); else if (r < 0.58f) StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); else BeginSit(h, SitAfter()); break;
        }
    }

    private void StartGroom(H h)
    {
        BeginHare(h, HD.Groom, Mind<HD>.Forever);
        h.GStage = 0; h.GT = 0f; h.GN = 3 + Rng.Next(4); h.GM = 3 + Rng.Next(2);
    }

    private void StartNibbleNow(H h, float dur)
    {
        BeginHare(h, HD.Nibble, Mind<HD>.Forever);
        h.Dur = dur; h.NStage = 0; h.NT = 0f; h.NRp = 0; h.NPg = -1;
    }

    private int NearestTwig(H h)
    {
        int best = -1; float bd = 0.55f * S.U;
        for (int k = 0; k < _twigs.Length; k++)
        {
            float d = MathF.Abs(_twigs[k].X - h.X) + 1.5f * MathF.Abs(_twigs[k].Y - h.Y);
            if (d < bd) { bd = d; best = k; }
        }
        return best;
    }

    private void StartNibbleTrip(H h, int twig)
    {
        float unit = HU(h.Y);
        int side = _twigs[twig].X >= h.X ? 1 : -1;
        float sx = _twigs[twig].X - side * 0.64f * unit, sy = _twigs[twig].Y;
        if (!HRest(sx, sy, 0.3f * unit)) { BeginSit(h, SitFor()); return; }
        h.Twig = twig; h.Next = HD.Nibble;
        float nd = 5f + 4f * (float)Rng.NextDouble();
        if (MathF.Abs(sx - h.X) < 0.2f * unit && MathF.Abs(sy - h.Y) < 0.1f * unit)
        {
            // close enough to nibble from where it sits (no snap: it just turns to face the twig first if it must)
            h.Next = HD.Sit;
            if (side != h.Dir) StartTurn(h, side, HD.Nibble, 1f, 0f); else StartNibbleNow(h, nd);
            return;
        }
        if (!PlanHop(h, sx, sy, side, 0.88f, 1.12f, 0.15f)) { h.Next = HD.Sit; BeginSit(h, SitFor()); }
    }

    /// <summary>A WHOLE number of EVEN hops (the count and the stretch of one hop never move a planted foot by more than a tenth).</summary>
    private void StartHop(H h, int idx)
    {
        float U = S.U, unit = HU(h.Y), cyc = FoxAndHaresArt.HareHopD * unit, W = S.Width;
        for (int tries = 0; tries < 24; tries++)
        {
            // a TRAVEL: four to six hops half the time, seven to twelve the rest, to a new row of the territory on a shallow diagonal
            int n = tries >= 12 ? 1 + Rng.Next(3) : Rng.NextDouble() < 0.45 ? 4 + Rng.Next(3) : 7 + Rng.Next(6);      // after a dozen failures short hops (a hare in a pocket can still shuffle out)
            int dir = Rng.Next(2) == 0 ? 1 : -1;
            float dx = dir * n * cyc;
            float gxx = Math.Clamp(h.X + dx, 0.03f * W, 0.97f * W);
            float lo = S.SmallBandTop + 0.004f * U, hi = S.FieldPoint(gxx, 0.64f).Y - 0.004f * U;
            float ty = lo + (float)Rng.NextDouble() * MathF.Max(1f, hi - lo);
            float gy = h.Y + Math.Clamp(ty - h.Y, -0.35f * MathF.Abs(dx), 0.35f * MathF.Abs(dx));
            float gx = h.X + dx;
            if (!HRest(gx, gy, 0.3f * unit) || !LineClear(h.X, h.Y, gx, gy, 0.3f * unit)) continue;
            bool crowd = false;
            for (int k = 1; k <= 6; k++) if (S.Crowded(h.X + dx * k / 6f, h.Y + (gy - h.Y) * k / 6f, 0.5f * unit, this, idx)) crowd = true;
            if (crowd) continue;
            h.Next = HD.Sit;
            PlanHop(h, gx, gy, dir, 0.999f, 1.001f);
            return;
        }
        Log($"hare{idx} finds no clear run of hops"); BeginSit(h, SitFor());
    }

    /// <summary>Plans a run of whole hops to (gx, gy): the count is the nearest whole number whose hop is stretched or squeezed by no more than kMin..kMax; false if none is. Turns first if it faces the wrong way.</summary>
    private bool PlanHop(H h, float gx, float gy, int dir, float kMin, float kMax, float tol = 0.02f)
    {
        float unit = HU(h.Y);
        float cyc = FoxAndHaresArt.HareHopD * unit;
        float dist = MathF.Abs(gx - h.X);
        int n0 = Math.Clamp((int)MathF.Round(dist / cyc), 1, 14), hops = n0; float k = 1f, bestErr = float.MaxValue;
        for (int n = Math.Max(1, n0 - 1); n <= n0 + 1; n++)
        {
            float kk = Math.Clamp(dist / (n * cyc), kMin, kMax), err = MathF.Abs(dist - n * cyc * kk);
            if (err < bestErr) { bestErr = err; hops = n; k = kk; }
        }
        if (bestErr > tol * unit) return false;               // no whole number of nearly even hops ends near enough: do something else
        gx = h.X + dir * hops * cyc * k;
        h.GX = gx; h.GY = gy; h.Hops = hops; h.HopK = k;
        if (dir != h.Dir) StartTurn(h, dir, HD.Go, 1f, 0.24f);
        else BeginHare(h, HD.Go, 0.24f);
        h.GoFlee = false;
        return true;
    }

    private void StartThump(H h, int i)
    {
        int to = h.FearX >= h.X ? 1 : -1;
        h.Thumps = 0;
        if (to != h.Dir) StartTurn(h, to, HD.Thump, 1f, 1.7f);
        else BeginHare(h, HD.Thump, 1.7f);
        Log($"hare{i} sees the {(h.FearFox ? "threat" : "noise")} and rears up to thump");
    }

    /// <summary>Starts a bolt: after sitting tall for "alertFor" seconds (a hare that heard a thump, at least 0.4 s), away from the cause, to a burrow it picks.</summary>
    private void GoBolt(H h, int i, float alertFor)
    {
        float U = S.U, unit = HU(h.Y);
        int away = h.FearX >= h.X ? -1 : 1;
        // a fox that is close is judged by where it IS now, not where the notice said it was (it may have trotted past)
        if (_fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - h.X) < 0.35f * U && MathF.Abs(_fox.X - h.X) > 0.4f * unit) away = _fox.X >= h.X ? -1 : 1;
        // THE FLIGHT RUNS AWAY, TO ONE OF ITS OWN BURROWS. One on the far side from the danger (never across it: the fox must not be
        // nearer the hole than the hare is), at least a bound and a half off, within 0.75 U, with a clear run and nobody using it.
        // The run is then PLANNED as a whole number of bounds, each a touch stretched or squeezed (0.8 to 1.3), so the hare arrives
        // on the gathered rest page exactly DiveRun short of the hole. No burrow like that: it will DIG one where it is (StartDig).
        h.BX = h.X; h.BY = h.Y; h.Resp = Resp.None; h.DigPending = false;
        float cycU = FoxAndHaresArt.HareFleeD * unit;
        int pick = -1; float bestD = float.MaxValue;
        for (int k = 0; k < _bur.Count; k++)
        {
            Bur b = _bur[k];
            if (b.Who >= 0 && b.Who != i) continue;
            if (b.Owner != i && _bur.Count > 3 * _n) continue;
            float run = (b.X - h.X) * away - FoxAndHaresArt.DiveRun * unit;
            if (run < 0.8f * cycU || run > 0.75f * U) continue;
            if (MathF.Abs(b.Y - h.Y) > 0.45f * run + 0.08f * U) continue;
            if (_fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(b.X - _fox.X) < MathF.Abs(h.X - _fox.X) + 0.05f * U && (b.X - _fox.X) * (h.X - _fox.X) > 0f) continue;
            if (!LineClear(h.X, h.Y, b.X - away * (FoxAndHaresArt.DiveRun + 0.1f) * unit, b.Y, 0.3f * unit) || S.Crowded(b.X, b.Y, 0.9f * unit, this, i)) continue;
            if (run < bestD) { bestD = run; pick = k; }
        }
        h.GY = h.Y; h.JinkT = 0.2f; h.Odo = FoxAndHaresArt.HopRest; h.LastFrac = FoxAndHaresArt.HopRest;
        h.Pace.Halt(); h.Pace.Accel = 30f * unit;
        h.Turning = false;
        if (pick >= 0)
        {
            Bur b = _bur[pick];
            if (h.Cur >= 0 && _bur[h.Cur].Who == i) _bur[h.Cur].Who = -1;
            h.Cur = pick; b.Who = i; h.BX = b.X; h.BY = b.Y;
            float run = (b.X - h.X) * away - FoxAndHaresArt.DiveRun * unit;
            int n = Math.Max(1, (int)MathF.Round(run / cycU));
            float kk = run / (n * cycU);
            if (kk > 1.3f && n > 0) { n++; kk = run / (n * cycU); }
            h.Hops = n; h.HopK = Math.Clamp(kk, 0.75f, 1.35f);
            Log($"hare{i} flees away from {h.FearX:F0}: its burrow at {h.BX:F0},{h.BY:F0}, {n} bounds of {h.HopK:F2}");
        }
        else { h.DigPending = true; h.HopK = 1f; Log($"hare{i} flees away from {h.FearX:F0}: no burrow in reach, it will dig one"); }
        if (alertFor > 0.01f) { BeginHare(h, HD.Tall, MathF.Max(0.5f, alertFor) + 0.35f); h.BoltPending = true; h.BoltDir = away; }
        else StartBoltNow(h, away, i);
    }

    /// <summary>
    /// THE DIVE BEGINS: the hare is on a gathered rest page DiveRun short of the hole (give or take the little arrival error, which
    /// the dive's glide melts away). From here the sprite's origin is the HOLE, and the hole's two pieces are stamped round the page.
    /// </summary>
    private void BeginDive(H h, int i)
    {
        float unit = HU(h.Y); Bur b = _bur[h.Cur];
        h.DOx = ((h.X - (b.X - h.Dir * FoxAndHaresArt.DiveRun * unit)) * h.Dir) / unit;
        h.DOy = (b.Y - h.Y) / unit;
        h.X = b.X; h.Y = b.Y; h.BX = b.X; h.BY = b.Y; h.Pace.Halt();
        h.OpenPg = FoxAndHaresArt.OpenN - 1; b.Open = FoxAndHaresArt.OpenN - 1;
        BeginHare(h, HD.Dive, FoxAndHaresArt.DiveSecs); Show(h, HB_Dive, 0, h.Dir < 0);
    }

    /// <summary>
    /// A hare with no burrow in reach DIGS one where it stands, ahead of its nose (a hole's place is DiveRun in front of the body, the
    /// dive sheet's own spot): the settle pages have already put it on the gather; it crouches, scrapes with the forepaws, snow flies
    /// back, the hole opens in steps, it rises back to the gather and dives. The new hole is its own from then on.
    /// </summary>
    private void StartDig(H h, int i)
    {
        float unit = HU(h.Y);
        float hx = h.X + h.Dir * FoxAndHaresArt.DiveRun * unit;
        if (!Valid(hx, h.Y, 0.3f * unit) || !HRest(hx, h.Y, 0.3f * unit))
        {
            // nowhere to dig: it settles back from the gather into a sit (the bolt's own settle sheet, forwards: no pop) and sits tight
            Log($"hare{i} cannot dig here: it settles and sits tight"); h.Pace.Halt(); BeginHare(h, HD.Land, 0.24f); return;
        }
        if (h.Cur >= 0 && _bur[h.Cur].Who == i) _bur[h.Cur].Who = -1;
        _bur.Add(new Bur { X = hx, Y = h.Y, Owner = i, Who = i, Open = 0 });
        h.Cur = _bur.Count - 1; h.BX = hx; h.BY = h.Y; h.OpenPg = 0;
        h.Pace.Halt();
        BeginHare(h, HD.Dig, Mind<HD>.Forever); h.DivePuff = false;
        Show(h, HB_Dig, 0, h.Dir < 0);
        Log($"hare{i} digs a new burrow at {hx:F0},{h.Y:F0}");
    }

    private void StartBoltNow(H h, int away, int i)
    {
        h.BoltPending = false; h.GoFlee = true; h.Resp = Resp.None;
        if (away != h.Dir) StartTurn(h, away, HD.Go, 0.5f, 0.24f);
        else BeginHare(h, HD.Go, 0.24f);
    }

    /// <summary>
    /// Picks which of the hare's OWN burrows it will come up out of (another one than it went into if there is one: it travelled under the
    /// snow), and which way round it faces: a free burrow, not under the fox, nobody standing on it, with room for the hop out.
    /// </summary>
    private void PickEmerge(H h)
    {
        float U = S.U, unit = HU(h.Y);
        int me = Array.IndexOf(_h, h);
        int old = h.Cur;
        if (old >= 0 && _bur[old].Who == me) _bur[old].Who = -1;
        int best = -1, bestN = 0;
        for (int pass = 0; pass < 2 && best < 0; pass++)
        {
            for (int k = 0; k < _bur.Count; k++)
            {
                Bur b = _bur[k];
                if (b.Who >= 0 || (b.Owner != me && pass == 0) || (k == old && pass == 0 && CountOwn(me) > 1)) continue;
                bool foxNear = _fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - b.X) < 0.35f * U;
                if (foxNear && pass == 0) continue;
                float u = HU(b.Y);
                if (S.Crowded(b.X, b.Y, 1.4f * u, this, me)) continue;
                // the weight: the dice choose among those that qualify (reservoir sampling, no allocation)
                bestN++; if (Rng.Next(bestN) == 0) best = k;
            }
        }
        if (best < 0) best = old;
        h.Cur = best; _bur[best].Who = me;
        Bur c = _bur[best]; h.BX = h.ToBX = c.X; h.BY = h.ToBY = c.Y;
        h.EDir = Rng.Next(2) == 0 ? 1 : -1;
        float cu = HU(c.Y);
        if (!HRest(c.X + h.EDir * 0.85f * cu, c.Y, 0.3f * cu) || S.Crowded(c.X + h.EDir * 0.85f * cu, c.Y, 0.5f * cu, this, me)) h.EDir = -h.EDir;
    }

    /// <summary>The burrow nearest (x, y) within 0.4 U, or -1.</summary>
    private int NearestBur(float x, float y)
    {
        int best = -1; float bd = 0.4f * S.U;
        for (int k = 0; k < _bur.Count; k++) { float dx = _bur[k].X - x, dy = 2f * (_bur[k].Y - y); float d = MathF.Sqrt(dx * dx + dy * dy); if (d < bd) { bd = d; best = k; } }
        return best;
    }

    private int CountOwn(int me) { int n = 0; foreach (Bur b in _bur) if (b.Owner == me) n++; return n; }

    // ---------------------------------------------------------------- the fox

    private void ShowF(F f, int book, int page, bool mirror)
    {
        if (_fb == null) return;
        f.Bk = _fb[book]; f.Pg = Math.Clamp(page, 0, f.Bk.Count - 1); f.Mir = mirror;
    }

    private void BeginFox(F f, FD d, float dur)
    {
        f.Mind.Begin(d, dur); f.Clock = 0;
        Log($"fox {d} {dur:F1}s at {f.X:F0},{f.Y:F0}");
    }

    private void StartFoxTurn(F f, int to, FD after)
    {
        if (to == f.Dir) { BeginFox(f, after, after == FD.Stand ? 2f : Mind<FD>.Forever); return; }
        f.Turning = true; f.TurnT = 0; f.TurnFrom = f.Dir; f.TurnTo = to; f.After = after; f.Pace.Halt();
    }

    private float Phase(F f, float stride) { float p = f.Odo / stride; return p - MathF.Floor(p); }

    /// <summary>Begins a stop on a rest page: the next phase of the stride (from "rests") that is at least a quarter stride ahead; the pace brakes to land exactly on it.</summary>
    private void BrakeToRest(F f, float stride, float unit, float[] rests, int bookIdxBase = 0)
    {
        float ph = Phase(f, stride), best = float.MaxValue; int bi = 0;
        for (int i = 0; i < rests.Length; i++)
        {
            float d = rests[i] - ph; if (d < 0.25f * (1f / rests.Length)) d += 1f; if (d > 1f) d -= 1f;
            if (d < best) { best = d; bi = i; }
        }
        f.RestIdx = bi; f.Pace.Brake(best * stride * unit); f.Braked = true;
    }

    private bool FoxHareVisible(out int idx)
    {
        idx = -1; if (_fox == null) return false;
        float best = 0.55f * S.U;
        for (int j = 0; j < _n; j++)
        {
            if (_h[j].Mind.Doing is HD.Under or HD.Dive or HD.Peek or HD.Bolt or HD.Dig) continue;
            float d = MathF.Abs(_h[j].X - _fox.X) + 1.3f * MathF.Abs(_h[j].Y - _fox.Y);
            if (d < best) { best = d; idx = j; }
        }
        return idx >= 0;
    }

    private void StepFox(float dt)
    {
        F? f = _fox; if (f == null) return;
        float U = S.U, W = S.Width;
        float unit = FU(f.Y);
        f.Mind.Tick(dt); f.Clock += dt; f.Att.Tick(dt); f.BreathT -= dt; f.PostT -= dt; f.GlanceCool -= dt; f.StalkCool -= dt;
        f.Gx = 0f; f.Gy = 0f;
        FD d = f.Mind.Doing;
        bool mir = f.Dir < 0;
        f.Perf = (d == FD.Nose && f.NoseBur >= 0) || (d == FD.Mouse && f.Stage >= 3 && f.Stage <= 5 && f.CraI >= 0);
        if (d != FD.Away) { f.OnScreen += dt; S.Occupy(this, 100, f.X, f.Y, 0.5f * 0.95f * unit); }

        if (d == FD.Away)
        {
            _nextAway -= dt;
            if (_nextAway <= 0f && _fb != null && _test is "natural") EnterFox(f);
            LogFox(f, dt); return;
        }

        // the fox's weak and strong Threat pins: a bout every 0.5 s while it stalks or chases, every 0.8 s while it trots
        if (d is FD.Stalk or FD.Chase or FD.DashIn or FD.StalkIn) { if (f.PostT <= 0f) { Board.Post(Sense.Threat, new PointF(f.X, f.Y), 0.35f * U, 1f, f); f.PostT = 0.5f; } }
        else if (d is FD.Trot or FD.Go or FD.Leave) { if (f.PostT <= 0f) { Board.Post(Sense.Threat, new PointF(f.X, f.Y), 0.2f * U, 0.4f, f); f.PostT = 0.8f; } }

        if (f.Turning)
        {
            f.TurnT += dt;
            float fr = Math.Clamp(f.TurnT / TurnFor, 0f, 0.999f);
            bool first = fr < 0.5f; float uu = first ? fr * 2f : (1f - fr) * 2f;
            ShowF(f, FB_Turn, Math.Clamp((int)(uu * 14f), 0, 13), first ? f.TurnFrom < 0 : f.TurnTo < 0);
            if (!first) f.Dir = f.TurnTo;
            if (f.TurnT >= TurnFor) { f.Turning = false; f.Dir = f.TurnTo; BeginFox(f, f.After, f.After == FD.Stand ? 3.5f + 2.5f * (float)Rng.NextDouble() : Mind<FD>.Forever); }
            LogFox(f, dt); return;
        }

        // ---- senses: a standing or sleeping fox notices things ----
        if (d is FD.Stand or FD.Asleep or FD.Sit)
        {
            Reaction r = f.Att.Check(Board, new PointF(f.X, f.Y), f, out Notice cause);
            if (r != Reaction.None)
            {
                Log($"fox feels {r} from {cause.Kind} at {cause.At.X:F0},{cause.At.Y:F0}");
                if (d == FD.Asleep && cause.Kind is not (Sense.Loud or Sense.Threat)) { }                       // a sleeper wakes at a noise or a danger, not at Santa going over or a hare's thump
                else if (d == FD.Asleep) { f.WakeBook = FB_Wake0 + (int)MathF.Round(Math.Min(7, (int)(f.Clock / 4.5f)) / 7f * 3f); BeginFox(f, FD.Wake, 1.1f); Burst(f.X, f.Y, unit, 10, 0.5f, 0.6f, 2); d = FD.Wake; }
                else if (d == FD.Stand && r != Reaction.Approach && (f.GlanceCool <= 0f || cause.Kind != Sense.Movement || cause.At.Y > f.Y - 0.18f * S.Height))
                {
                    if (cause.At.Y < f.Y - 0.18f * S.Height) f.GlanceCool = 25f;
                    f.LookUp = Math.Clamp((f.Y - cause.At.Y) / (0.5f * S.Height), 0.15f, 1f); f.LookDur = 2.2f;
                    f.LookPend = true; f.LookPendAt = _time;
                }
            }
        }

        switch (d)
        {
            case FD.Go:
            {
                // getting going: the settle pages played backwards, then the trot from the same rest page
                int rb = f.RestIdx == 0 ? FB_Settle0 : FB_Settle1;
                ShowF(f, rb, Math.Max(0, 7 - (int)(f.Clock / 0.22f * 8f)), mir);
                if (f.Clock >= 0.22f) { f.Mind.Begin(f.GoLeave ? FD.Leave : FD.Trot, Mind<FD>.Forever); f.Clock = 0; f.Odo = (f.RestIdx == 0 ? RestA : RestB) * FoxAndHaresArt.FoxTrotStride * f.StrideK; f.PrevPh = f.RestIdx == 0 ? RestA : RestB; f.Braked = false; f.Pace.Halt(); f.Pace.Accel = 2.2f * unit; }
                break;
            }
            case FD.Trot:
            case FD.Leave:
            {
                float stride = FoxAndHaresArt.FoxTrotStride;
                f.Pace.Want = FoxTrotV * unit * (d == FD.Leave ? 1.15f : 1f);
                float step = f.Pace.Tick(dt);
                f.Odo += step / unit; f.X += f.Dir * step;
                bool stopNow = SteerFox(f, dt, step, unit);
                float ph = f.Odo / (stride * f.StrideK); ph -= MathF.Floor(ph);
                ShowF(f, FB_Trot, (int)(ph * 24f) % 24, mir);
                if (f.Plan > 0 && !f.Braked)
                {
                    // count the rest pages passed; at the one before the last, brake: the last rest page is then exactly where the walk meant to stop
                    bool Crossed(float r) => f.PrevPh <= ph ? (f.PrevPh < r && ph >= r) : (r > f.PrevPh || r <= ph);
                    if ((Crossed(RestA) || Crossed(RestB)) && --f.Plan == 1) { Log($"fox brakes at {f.X:F0} (stop meant at {f.GX:F0}) phase {ph:F3} odo {f.Odo:F2}"); BrakeToRest(f, stride * f.StrideK, unit, [RestA, RestB]); }
                }
                f.PrevPh = ph;
                if (_fb != null && f.Pg % 6 == 0 && f.Pg != f.LastPrint) { f.LastPrint = f.Pg; Track(f.X + f.Dir * 0.25f * unit, f.Y, 3); }
                float left = (f.GX - f.X) * f.Dir;
                if (d == FD.Leave)
                {
                    if (f.X < -1.5f * unit || f.X > W + 1.5f * unit) { f.Mind.Begin(FD.Away, Mind<FD>.Forever); _nextAway = 22f + 28f * (float)Rng.NextDouble(); f.OnScreen = 0; Log($"fox is away for {_nextAway:F0} s"); }
                }
                else if (!f.Braked && ((f.Plan == 0 && left < 0.8f * unit) || stopNow)) { f.Plan = 0; BrakeToRest(f, stride * f.StrideK, unit, [RestA, RestB]); }
                else if (f.Braked && f.Pace.Still) { f.Braked = false; f.Plan = 0; f.StrideK = 1f; BeginFox(f, FD.Settle, 0.22f); }
                break;
            }
            case FD.Settle:
            {
                int rb = f.RestIdx == 0 ? FB_Settle0 : FB_Settle1;
                ShowF(f, rb, Math.Min(7, (int)(f.Clock / 0.22f * 8f)), mir);
                if (f.Clock >= 0.22f) { if (f.NoseNext && MathF.Abs(f.HoleX - f.X) < 1.3f * unit) { f.NoseNext = false; BeginFox(f, FD.Nose, Mind<FD>.Forever); f.Stage = 0; f.NoseBur = NearestBur(f.HoleX, f.Y); } else if (f.NoseNext) { f.NoseNext = false; Log("fox could not get to the hole: it gives up"); BeginFox(f, FD.Stand, 3.5f + 2f * (float)Rng.NextDouble()); } else BeginFox(f, FD.Stand, 4f + 5f * (float)Rng.NextDouble()); }
                break;
            }
            case FD.Stand:
            {
                ShowF(f, FB_Stand, Cyc(f.Clock, 16f, 48, 4.8f), mir);          // 48 pages at 16 a second, starting on the pose every sheet joins (t = 0.1)
                Breathe(f, unit);
                if (f.LookPend)
                {
                    // a glance waits for the stand's quiet phase (ear still, eye open), then begins; one that waited over 1.5 s is dropped
                    if (_time - f.LookPendAt > 1.5) f.LookPend = false;
                    else if (StandGate(f)) { f.LookPend = false; BeginFox(f, FD.Look, f.LookDur); break; }
                }
                if (f.NoseNext)
                {
                    // the hare dived; the fox stands where it pulled up, watching, until the tail has gone, then walks up to the hole
                    f.NoseT += dt;
                    H? tg = f.Target >= 0 ? _h[f.Target] : null;
                    bool under = tg == null || tg.Mind.Doing is HD.Under or HD.Peek;
                    if (under && f.NoseT > 0.3f)
                    {
                        float dist = (f.HoleX - f.X) * f.Dir - 0.42f * unit;       // the muzzle (0.46 m ahead of the fox's feet when it noses) ends a little way over the mouth
                        if (dist < 0.25f * unit) { f.NoseNext = false; BeginFox(f, FD.Nose, Mind<FD>.Forever); f.Stage = 0; f.NoseBur = NearestBur(f.HoleX, f.Y); }
                        else
                        {
                            f.GX = f.X + f.Dir * dist; f.GY = f.Y; f.GoLeave = false; f.RestIdx = Rng.Next(2); f.Pace.Halt();
                            // THE APPROACH IS PLANNED: a whole number of half strides, each a touch stretched, ends on a rest page exactly at the spot, so the muzzle lands over the burrow's mouth
                            float dm = dist / unit, half = 0.5f * FoxAndHaresArt.FoxTrotStride; int kk = (int)MathF.Round(dm / half);
                            f.StrideK = 1f; f.Plan = 0;
                            if (kk >= 2) { float kf = dm / (kk * half); if (kf >= 0.8f && kf <= 1.25f) { f.StrideK = kf; f.Plan = kk; } }
                            Log($"fox plans its walk to the hole: {kk} half strides of {f.StrideK:F2}, plan {f.Plan}, dist {dist:F0} px"); BeginFox(f, FD.Go, 0.22f);
                        }
                    }
                    else if (f.NoseT > 6f) f.NoseNext = false;
                    break;
                }
                if (f.Mind.Over && StandGate(f)) RollFox(f);
                break;
            }
            case FD.Look:
            {
                float lv = Math.Min(1f, f.Clock / 0.35f) * (f.Clock > f.LookDur - 0.35f ? Math.Max(0f, (f.LookDur - f.Clock) / 0.35f) : 1f) * f.LookUp;
                ShowF(f, FB_Look, (int)MathF.Round(lv * 11f), mir);
                if (f.Mind.Over) BeginFox(f, FD.Stand, 3f + 3f * (float)Rng.NextDouble());
                break;
            }
            case FD.SitIn:
            {
                int pg = f.Reverse ? Math.Max(0, 17 - (int)(f.Clock / 0.6f * 18f)) : Math.Min(17, (int)(f.Clock / 0.6f * 18f));
                ShowF(f, FB_SitIn, pg, mir);
                if (f.Clock >= 0.6f) { if (f.Reverse) { f.Reverse = false; BeginFox(f, FD.Stand, 3.5f + 2.5f * (float)Rng.NextDouble()); } else if (f.SleepAfter) { f.SleepAfter = false; f.SleepFor = 45f; BeginFox(f, FD.CurlIn, 1f); } else BeginFox(f, FD.Sit, 6f + 6f * (float)Rng.NextDouble()); }
                break;
            }
            case FD.Sit:
            {
                ShowF(f, FB_Sit, Cyc(f.Clock, 15f, 36, 3.6f), mir);
                Breathe(f, unit);
                if (f.Mind.Over && SitGate(f))
                {
                    if (Rng.NextDouble() < 0.35 && _test != "mouse") { BeginFox(f, FD.CurlIn, 1f); f.SleepFor = 22f + 25f * (float)Rng.NextDouble(); }
                    else { f.Reverse = true; BeginFox(f, FD.SitIn, 0.6f); }
                }
                break;
            }
            case FD.CurlIn:
            {
                ShowF(f, FB_CurlIn, Math.Min(32, (int)(f.Clock / 1.1f * 33f)), mir);
                if (f.Clock >= 1.1f) { BeginFox(f, FD.Asleep, f.SleepFor); Log("fox is asleep"); }
                break;
            }
            case FD.Asleep:
            {
                int snow = Math.Min(7, (int)(f.Clock / 4.5f));
                ShowF(f, FB_Sleep, snow * 8 + Cyc(f.Clock, 2.3f, 8, 0f), mir);
                if (f.BreathT <= 0f && _fb != null) { Emit(1, f.X + f.Dir * 0.18f * unit, f.Y, 0.12f * unit, f.Dir * 0.04f * unit, 0.05f * unit, 1.3f, 0, 0.4f); f.BreathT = 3.2f; }
                if (f.Mind.Over) { f.WakeBook = FB_Wake0 + (int)MathF.Round(snow / 7f * 3f); BeginFox(f, FD.Wake, 1.1f); Burst(f.X, f.Y, unit, 8, 0.45f, 0.5f, 2); }
                break;
            }
            case FD.Wake:
            {
                ShowF(f, f.WakeBook, Math.Min(32, (int)(f.Clock / 1.1f * 33f)), mir);
                if (f.Clock >= 1.1f) BeginFox(f, FD.Sit, 3f);
                break;
            }
            case FD.Mouse: StepMouse(f, dt, unit, mir); break;
            case FD.StalkIn:
            {
                int pg = f.Reverse ? Math.Max(0, 13 - (int)(f.Clock / 0.4f * 14f)) : Math.Min(13, (int)(f.Clock / 0.4f * 14f));
                ShowF(f, FB_StalkIn, pg, mir);
                if (f.Clock >= 0.4f)
                {
                    if (f.Reverse) { f.Reverse = false; BeginFox(f, FD.Stand, 4f + 4f * (float)Rng.NextDouble()); }
                    else { f.Mind.Begin(FD.Stalk, 16f); f.Clock = 0; f.Odo = StalkRest * FoxAndHaresArt.FoxStalkStride; f.Braked = false; f.Pace.Halt(); f.Pace.Accel = 1.2f * unit; }
                }
                break;
            }
            case FD.Stalk:
            {
                float stride = FoxAndHaresArt.FoxStalkStride;
                bool have = f.Target >= 0 && _h[f.Target].Mind.Doing is not (HD.Under or HD.Dive or HD.Peek) && !Covered(f.X, f.Y, 0.55f * unit, 0.6f * unit);     // a stalk never goes on half behind a prop
                H? t = have ? _h[f.Target] : null;
                bool hareAlarmed = t != null && (t.Mind.Doing is HD.Thump or HD.Bolt || BoltRun(t));       // a hare that only freezes (Tall) is still being crept up on
                {
                    if (t != null) { f.GX = t.X - f.Dir * 1.6f * unit; f.GY = t.Y; }
                    f.Pace.Want = FoxStalkV * unit;
                    float step = f.Pace.Tick(dt);
                    f.Odo += step / unit; f.X += f.Dir * step;
                    f.Y += Math.Clamp((f.GY - f.Y) * 0.8f * Math.Min(1f, step / MathF.Max(8f, MathF.Abs(f.GX - f.X))), -0.45f * step, 0.45f * step);
                }
                float ph = f.Odo / stride; ph -= MathF.Floor(ph);
                // a stalk is too slow to brake over a distance (it would take seconds), so it simply keeps creeping to the rest page and stops there
                if (f.Braked && f.LastPh < StalkRest && ph >= StalkRest && ph - f.LastPh < 0.5f) { f.Pace.Halt(); ph = StalkRest; }
                f.LastPh = ph;
                ShowF(f, FB_Stalk, (int)(ph * 24f) % 24, mir);
                float gap = t == null ? 99f : (t.X - f.X) * f.Dir / unit;      // metres to the hare (positive: ahead)
                bool close = gap < 2.6f && gap > -0.5f;
                if (!f.Braked && (t == null || close || hareAlarmed || f.Mind.Over || gap < -0.5f))
                {
                    f.After2 = t != null && BoltRun(t) && !Lull ? FD.DashIn : t != null && (close || hareAlarmed) && !Lull ? FD.Watch : FD.StalkOut;
                    f.Pace.Accel = 6f * unit;
                    f.Braked = true; f.LastPh = ph;
                    Log($"fox ends the stalk ({(t == null ? "lost it" : close ? "close enough" : hareAlarmed ? "it is alarmed" : "time")}), then {f.After2}");
                }
                else if (f.Braked && f.Pace.Still) { f.Braked = false; if (f.After2 == FD.DashIn) { BeginFox(f, FD.DashIn, 0.3f); } else if (f.After2 == FD.Watch) { BeginFox(f, FD.Watch, Mind<FD>.Forever); f.Stage = 0; } else { f.Reverse = true; BeginFox(f, FD.StalkIn, 0.4f); } }
                break;
            }
            case FD.Watch:
            {
                // A PULL-UP, not a shuffle: the stalk last creeping step settles into a braced stand, weight back on the haunches,
                // head low, ears on the hare. It holds there (breathing, an ear, the brush tip) until the hare BOLTS (then it rises
                // out of the brace and dashes) or four and a half seconds pass (then it gives up the stalk the way it began it).
                H? t = f.Target >= 0 ? _h[f.Target] : null;
                if (f.Stage == 0)
                {
                    ShowF(f, FB_Watch, Math.Min(11, (int)(f.Clock / 0.4f * 12f)), mir);
                    if (f.Clock >= 0.4f) { f.Stage = 1; f.StageT = 0f; }
                }
                else if (f.Stage == 1)
                {
                    f.StageT += dt;
                    ShowF(f, FB_Watch, 12 + (int)(f.StageT * 12f) % 24, mir);
                    Breathe(f, unit);
                    bool lost = t == null || t.Mind.Doing is HD.Under or HD.Dive or HD.Peek;
                    if (!lost && BoltRun(t!) && !Lull) { f.After2 = FD.DashIn; f.Stage = 2; f.StageT = 0f; }
                    else if (lost || f.StageT > 4.5f || Lull) { f.After2 = FD.StalkOut; f.Stage = 2; f.StageT = 0f; }
                }
                else
                {
                    f.StageT += dt;
                    ShowF(f, FB_Watch, Math.Max(0, 11 - (int)(f.StageT / 0.3f * 12f)), mir);
                    if (f.StageT >= 0.3f)
                    {
                        if (f.After2 == FD.DashIn) BeginFox(f, FD.DashIn, 0.3f);
                        else { f.Reverse = true; BeginFox(f, FD.StalkIn, 0.4f); }
                    }
                }
                break;
            }
            case FD.DashIn:
            {
                ShowF(f, FB_DashIn, Math.Min(8, (int)(f.Clock / 0.3f * 9f)), mir);
                if (f.Clock >= 0.3f)
                {
                    f.Mind.Begin(FD.Chase, 9f); f.Clock = 0; f.Odo = 0; f.Braked = false; f.SkidGo = false; f.Pace.Halt(); f.Pace.Accel = 9f * unit;
                    Log($"fox DASHES after hare{f.Target}");
                }
                break;
            }
            case FD.Chase:
            {
                float stride = FoxAndHaresArt.FoxGallopStride;
                H? t = f.Target >= 0 ? _h[f.Target] : null;
                bool hareGone = t == null || t.Mind.Doing is HD.Under or HD.Peek;
                float tx, ty;
                if (t != null && !hareGone) { tx = t.Mind.Doing is HD.Dive ? t.BX : t.X; ty = t.Y; f.HoleX = t.BX; f.HoleY = t.BY; }
                else { tx = f.HoleX; ty = f.HoleY; }
                bool hasHole = t != null && (t.Mind.Doing is HD.Dive or HD.Under or HD.Peek or HD.Dig);
                if (hasHole)
                {
                    f.StopX = f.HoleX - f.Dir * 1.45f * unit;                    // it pulls up short of the hole: the diving hare's body lies 0.6 m behind it, and nothing stands inside anything
                    // ...and never half behind a prop. (Seen on film: the fox skidded to a stop with its body behind the
                    // snowman's bottom ball and stood there, a head poking out, for four seconds.) If the stopping place
                    // is covered, stop a little shorter, step by step, until the whole fox is in the open.
                    float fu = FU(f.Y);
                    for (int back = 0; back < 14 && Covered(f.StopX, f.Y, 0.55f * fu, 0.6f * fu); back++) f.StopX -= f.Dir * 0.3f * unit;
                }
                if (!f.SkidGo)
                {
                    // while the hare is still rearing up to thump the fox eases off, three metres behind it: it never overruns a hare that has not bolted
                    // BEHIND, never across: a hare that is running (or diving) is never nearer than 1.6 m centre to centre (the fox is
                    // 0.9 m long, the hare 0.6), a hare that is only rearing up is kept three metres off
                    float easeF = 1f;
                    float gapM = t == null ? 99f : (t.X - f.X) * f.Dir / unit;
                    if (t != null && !hareGone && (BoltRun(t) || t.Mind.Doing == HD.Dive)) easeF = Math.Clamp((gapM - 1.6f) / 0.9f, 0f, 1f);
                    else if (t != null && !hareGone) easeF = Math.Clamp((gapM - 2.2f) / 2.5f, 0.12f, 1f);
                    f.Pace.Want = FoxChaseV * unit * easeF;
                    float step = f.Pace.Tick(dt);
                    f.Odo += step / unit; f.X += f.Dir * step;
                    float dxl = MathF.Max(8f, MathF.Abs(tx - f.X));
                    f.Y += Math.Clamp((ty - f.Y) * Math.Min(1f, step / dxl), -0.5f * step, 0.5f * step);
                }
                float ph = f.Odo / stride; ph -= MathF.Floor(ph);
                ShowF(f, FB_Gallop, (int)(ph * 24f) % 24, mir);
                float gatherPh = FoxAndHaresArt.GallopGather(stride);
                bool atGather = MathF.Abs(ph - gatherPh) < 1.2f / 16f || MathF.Abs(ph - gatherPh) > 1f - 1.2f / 16f;
                float toStop = hasHole ? (f.StopX - f.X) * f.Dir : 99f;
                bool pastIt = hasHole && toStop < -0.2f * unit;
                if (!f.SkidGo && ((hasHole && toStop < 1.5f * unit && (atGather || toStop < 0.7f * unit)) || f.Mind.Over || (!Valid(f.X + f.Dir * 0.3f * unit, f.Y, 0.3f * unit) && f.Clock > 0.5f)))
                {
                    f.SkidGo = true; f.Pace.Brake(MathF.Max(0.5f * unit, toStop < 99f ? toStop : 0.8f * unit)); f.Braked = true; f.Clock = 0;
                    Log($"fox skids to a stop (hole at {f.HoleX:F0}, stop {f.StopX:F0}, at {f.X:F0})");
                }
                if (f.SkidGo)
                {
                    float step = f.Pace.Tick(dt); f.X += f.Dir * step; f.Odo += step / unit;
                    ShowF(f, FB_Skid, Math.Min(15, (int)(f.Clock / 0.55f * 16f)), mir);
                    if (step > 0.5f * unit * dt) Emit(0, f.X - f.Dir * 0.15f * unit, f.Y, 0.03f * unit, -f.Dir * 0.6f * unit, 0.3f * unit, 0.5f, 0, 0.7f, 1.2f * unit);
                    if (f.Pace.Still && f.Clock > 0.2f)
                    {
                        f.SkidGo = false; f.Braked = false;
                        bool hole = hasHole;
                        BeginFox(f, FD.Stand, 3.5f + 2.5f * (float)Rng.NextDouble());
                        if (hole) { f.NoseNext = true; f.NoseT = 0f; Log("fox waits for the hare to be under, then goes to the hole"); }
                    }
                }
                break;
            }
            case FD.Nose:
            {
                // nose at the burrow (about 1.6 s), dig twice (1.4 s, snow flying), nose again, give up (then it sits)
                float c = f.Clock;
                // every stage is a whole number of cycles, so each joins the next on its first page: nose-in 0.35 s, one nose cycle
                // (24 pages at 18 a second = 1.333 s), two dig cycles (21 pages at 30 a second = 0.7 s each), one more nose cycle, nose-out 0.35 s
                const float NC = 24f / 18f;
                float tN1 = 0.35f + NC, tD = tN1 + 1.4f, tN2 = tD + NC;
                if (c < 0.35f) ShowF(f, FB_NoseIn, Math.Min(9, (int)(c / 0.35f * 10f)), mir);
                else if (c < tN1) ShowF(f, FB_Nose, Cyc(c - 0.35f, 18f, 24, 0f), mir);
                else if (c < tD)
                {
                    int pg = Cyc(c - tN1, 30f, 21, 0f);
                    ShowF(f, FB_Dig, pg, mir);
                    if (pg == 10 && f.Stage == 0) { f.Stage = 1; SprayAt(f, unit); }
                    if (pg == 0 || pg == 20) f.Stage = 0;
                }
                else if (c < tN2) ShowF(f, FB_Nose, Cyc(c - tD, 18f, 24, 0f), mir);
                else ShowF(f, FB_NoseIn, Math.Max(0, 9 - (int)((c - tN2) / 0.35f * 10f)), mir);
                if (c >= 0.35f + NC + 1.4f + NC + 0.35f) { f.NoseBur = -1; f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); Log("fox gives up on the burrow and sits"); }
                break;
            }
        }
        LogFox(f, dt);
    }

    /// <summary>
    /// GOING ROUND THINGS. The trot looks three points ahead along the lane it is on (1, 1.8 and 2.6 m): is a prop foot, the ice,
    /// the band edge or ANOTHER ANIMAL there? If so it tries a gentler or a sharper lane change, one side or the other, and
    /// takes the first that is clear; the change is an EASED slope, so the path bends like a walk round somebody. True means
    /// there is no way round: stop (brake to a rest page) and choose again.
    /// </summary>
    private bool SteerFox(F f, float dt, float step, float unit)
    {
        float W = S.Width;
        bool edge = f.X < 0.05f * W || f.X > 0.95f * W;            // at the screen's edge the ground is not tested (it is off the snowfield), animals still are
        float dxLeft = MathF.Max(1f, (f.GX - f.X) * f.Dir);
        float lane = 1.2f * unit;                                  // a lane change is made over 1.2 m of walking: slope never above 0.45
        float want = Math.Clamp((f.GY - f.Y) / MathF.Max(dxLeft, 2.5f * unit), -0.40f, 0.40f);
        float target = want; bool stop = false;
        if (step > 0f)
        {
            float half = 0.5f * 0.95f * unit;
            float lowRow = S.SmallBandTop + 1f;
            // is the way to row yt (reached over a lane, then held) clear at 1, 1.8, 2.6 and 3.4 m ahead?
            bool BadRow(float yt)
            {
                for (int k = 1; k <= 4; k++)
                {
                    float d = (0.2f + 0.8f * k) * unit, px = f.X + f.Dir * d, py = f.Y + (yt - f.Y) * MathF.Min(1f, d / lane);
                    if ((!edge && !Valid(px, py, 0.3f * unit)) || py < lowRow || S.Crowded(px, py, half, this, 100)) return true;
                }
                return false;
            }
            f.DetourT -= dt;
            float wantRow = f.Y + want * lane;
            if (f.DetourT > 0f && !BadRow(f.DetourSlope)) target = Math.Clamp((f.DetourSlope - f.Y) / lane, -0.45f, 0.45f);
            else if (BadRow(wantRow))
            {
                // another row, nearest first, one side and the other: a body length or less off the straight line, then a bit more
                bool found = false;
                float[] offs = [0.3f, -0.3f, 0.5f, -0.5f, 0.75f, -0.75f, 1.0f, -1.0f];
                for (int i = 0; i < offs.Length && !found; i++)
                {
                    float o = offs[i] * unit; if (want < 0f) o = -o;
                    float yt = f.Y + o;
                    if (!BadRow(yt)) { f.DetourSlope = yt; f.DetourT = 2.5f; target = Math.Clamp((yt - f.Y) / lane, -0.45f, 0.45f); found = true; }
                }
                if (!found) { stop = true; target = 0f; Log($"fox path blocked at {f.X:F0},{f.Y:F0}"); }
            }
        }
        f.Slope += Math.Clamp(target - f.Slope, -2.5f * dt, 2.5f * dt);
        f.Y += f.Slope * step;
        return stop;
    }

    private void SprayAt(F f, float unit)
    {
        // the paw sweeps back along the snow: snow flies out behind it
        for (int k = 0; k < 4; k++) Emit(0, f.X + f.Dir * 0.30f * unit, f.Y, 0.04f * unit, -f.Dir * (0.5f + 0.5f * (float)Rng.NextDouble()) * unit, (0.5f + 0.7f * (float)Rng.NextDouble()) * unit, 0.55f, 0, 0.9f, 2.4f * unit);
    }

    private void Breathe(F f, float unit)
    {
        if (f.BreathT > 0f || _fb == null || f.Bk == null) return;
        PointF nz = f.Bk.Aux[f.Pg];
        Emit(1, f.X + f.Dir * nz.X * unit, f.Y, nz.Y * unit, f.Dir * 0.07f * unit, 0.08f * unit, 1.2f, 0, 0.5f);
        f.BreathT = 3.0f + 2f * (float)Rng.NextDouble();
    }

    private void EnterFox(F f)
    {
        float W = S.Width, unit = FU(f.Y);
        bool left = true;       // always in from the LEFT: the right edge is a pocket behind the snowman (the snowman's foot and the holly close it) that a fox cannot leave
        for (int tries = 0; tries < 20; tries++)
        {
            float gx = (left ? 0.25f + 0.5f * (float)Rng.NextDouble() : 0.5f + 0.45f * (float)Rng.NextDouble()) * W;
            PointF g = S.FieldPoint(gx, 0.2f + 0.35f * (float)Rng.NextDouble());
            if (!FRest(g.X, g.Y, 0.5f * unit)) continue;
            f.Dir = left ? 1 : -1;
            f.X = left ? -1.6f * unit : W + 1.6f * unit; f.Y = MathF.Max(S.FieldPoint(f.X < 0 ? 0.02f * W : 0.98f * W, 0.22f).Y, S.SmallBandTop + 0.012f * S.U);
            f.GX = g.X; f.GY = g.Y;
            if ((f.GX - f.X) * f.Dir < 3f * unit) continue;
            // the way in must be clear of animals for the first four metres (the fox walked in on top of a hare sitting by the edge)
            bool clearIn = true;
            for (int k = 1; k <= 5; k++) if (S.Crowded(left ? k * 0.9f * unit : W - k * 0.9f * unit, f.Y, 0.55f * unit, this, 100)) clearIn = false;
            if (!clearIn) continue;
            f.Odo = RestA * FoxAndHaresArt.FoxTrotStride; f.Braked = false; f.Pace.Halt(); f.Pace.Accel = 2.2f * unit; f.OnScreen = 0;
            f.Mind.Begin(FD.Trot, Mind<FD>.Forever); f.Clock = 0;
            Log($"fox trots in from the {(left ? "left" : "right")} toward {f.GX:F0},{f.GY:F0}");
            return;
        }
        _nextAway = 5f;
    }

    /// <summary>What next for a standing fox: trot on, linger, sit, mouse, stalk a hare, or leave (more likely the longer it has been about).</summary>
    private void RollFox(F f)
    {
        float U = S.U, unit = FU(f.Y);
        bool lull = Lull;
        bool hare = FoxHareVisible(out int hi) && !lull && f.StalkCool <= 0f;
        float leave = 0.04f + Math.Clamp((f.OnScreen - 40f) / 90f, 0f, 0.6f);
        int face = Dice.Pick(Rng, [lull ? 0.05f : 0.30f, 0.10f, 0.18f, lull ? 0f : 0.26f, hare ? 0.55f : 0f, leave]);
        if (Covered(f.X, f.Y, 0.55f * unit, 0.6f * unit)) face = 0;          // standing half behind a prop: move to a clear spot first, whatever the dice said
        Log($"fox rolls {face} (onscreen {f.OnScreen:F0} s)");
        switch (face)
        {
            case 0: FoxTrotTo(f, false); break;
            case 1: BeginFox(f, FD.Stand, 4f + 4f * (float)Rng.NextDouble()); break;
            case 2: f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); break;
            case 3: StartMouse(f); break;
            case 4: f.Target = hi; f.StalkCool = 50f; StartStalk(f, _h[hi]); break;
            case 5: FoxTrotTo(f, true); break;
        }
    }

    private void FoxTrotTo(F f, bool leave)
    {
        float W = S.Width, unit = FU(f.Y);
        if (leave)
        {
            int to = f.X < W * 0.5f ? -1 : 1;
            f.GX = to < 0 ? -2f * unit : W + 2f * unit; f.GY = f.Y;
            f.RestIdx = 0; f.Odo = RestA * FoxAndHaresArt.FoxTrotStride;
            f.After = FD.Go; f.Reverse = false;
            if (f.Dir != to) { StartFoxTurn(f, to, FD.Go); f.GoLeave = true; } else { f.GoLeave = true; BeginFox(f, FD.Go, 0.22f); }
            return;
        }
        for (int tries = 0; tries < 20; tries++)
        {
            float gx = (0.04f + 0.92f * (float)Rng.NextDouble()) * W;
            PointF g = S.FieldPoint(gx, 0.2f + 0.38f * (float)Rng.NextDouble());
            if (MathF.Abs(g.X - f.X) < 0.18f * S.U || !FRest(g.X, g.Y, 0.5f * unit) || S.Crowded(g.X, g.Y, 0.7f * unit, this, 100)) continue;
            if (MathF.Abs(g.Y - f.Y) > 0.45f * MathF.Abs(g.X - f.X)) continue;
            if (Valid(f.X, f.Y, 0.3f * unit) && !LineClear(f.X, f.Y, g.X, g.Y, 0.45f * unit)) continue;     // a fox that is not on valid snow just goes: the steering finds the way
            f.GX = g.X; f.GY = g.Y; f.GoLeave = false; f.Fail = 0;
            int to = g.X >= f.X ? 1 : -1;
            // the fox is at a rest page (stand): it turns on the spot if needed, then gets going
            f.RestIdx = Rng.Next(2);
            if (to != f.Dir) StartFoxTurn(f, to, FD.Go); else BeginFox(f, FD.Go, 0.22f);
            return;
        }
        if (++f.Fail >= 3) { f.Fail = 0; FoxTrotTo(f, true); return; }          // three tries and no goal: it leaves for a while
        BeginFox(f, FD.Stand, 2f);
    }

    private void StartStalk(F f, H t)
    {
        int to = t.X >= f.X ? 1 : -1;
        f.GX = t.X; f.GY = t.Y; f.Reverse = false;
        if (to != f.Dir) { StartFoxTurn(f, to, FD.StalkIn); }
        else BeginFox(f, FD.StalkIn, 0.4f);
        Log($"fox begins to stalk hare{f.Target}");
    }

    // ---- mousing ----
    private void StartMouse(F f)
    {
        float unit = FU(f.Y);
        // a hunting spot: facing the way it already faces, with room for the leap and the dive (0.9 m plus)
        float ex = f.X + f.Dir * 1.0f * unit;
        if (!FRest(ex, f.Y, 0.4f * unit) || S.Crowded(ex, f.Y, 0.7f * unit, this, 100)) { int to = -f.Dir; ex = f.X + to * 1.0f * unit; if (!FRest(ex, f.Y, 0.4f * unit) || S.Crowded(ex, f.Y, 0.7f * unit, this, 100)) { BeginFox(f, FD.Stand, 2f); return; } StartFoxTurn(f, to, FD.Mouse); f.MouseInit = true; return; }
        BeginFox(f, FD.Mouse, Mind<FD>.Forever); f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * unit; f.Sneezes = Rng.NextDouble() < 0.35;
        f.ListenFor = 2.39f * (1 + Rng.Next(2));
    }

    private void StepMouse(F f, float dt, float unit, bool mir)
    {
        if (f.MouseInit) { f.MouseInit = false; f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * unit; f.Sneezes = Rng.NextDouble() < 0.35; f.ListenFor = 2.39f * (1 + Rng.Next(2)); }
        f.StageT += dt;
        float c = f.StageT;
        switch (f.Stage)
        {
            case 0: // LISTEN: the head lowers (0.35 s), then low and cocked, ears swivelling; a Loud breaks it (checked below: the head comes back up), a listen of whole ear cycles ends in the coil
                if (c < 0.35f) ShowF(f, FB_Lower, Math.Min(9, (int)(c / 0.35f * 10f)), mir);
                else { ShowF(f, FB_Listen, Cyc(c - 0.35f, 36f / 2.39f, 36, 0f), mir); Breathe(f, unit); }
                foreach (Notice n in Board.Live) if (n.Kind == Sense.Loud && n.Who != f && MathF.Abs(n.At.X - f.X) < n.Reach) { f.Stage = 9; f.StageT = 0; Log("fox's listening is broken by a noise"); break; }
                if (f.Stage == 0 && c >= 0.35f + f.ListenFor) { f.Stage = 1; f.StageT = 0; }
                break;
            case 9: // the head comes back up from the listening pose (the lowering pages backwards), then a stand
                ShowF(f, FB_Lower, Math.Max(0, 9 - (int)(c / 0.3f * 10f)), mir);
                if (c >= 0.3f) BeginFox(f, FD.Stand, 3f + 2f * (float)Rng.NextDouble());
                break;
            case 1: // COIL
                ShowF(f, FB_Coil, Math.Min(13, (int)(c / 0.45f * 14f)), mir);
                if (c >= 0.45f) { f.Stage = 2; f.StageT = 0; f.LeapX0 = f.X; Log("fox LEAPS"); }
                break;
            case 2: // LEAP: 0.8 s, an arc, carried forward 0.45 m
            {
                float fr = Math.Min(0.999f, c / 0.8f);
                ShowF(f, FB_Leap, Math.Min(27, (int)(fr * 28f)), mir);
                { PointF gl = f.Bk!.GlideAt(fr, f.Pg); f.Gx = gl.X; f.Gy = gl.Y; }
                f.X = f.LeapX0 + f.Dir * f.LeapDist * FoxAndHaresArt.Sm(fr);
                if (c >= 0.8f)
                {
                    f.Stage = 3; f.StageT = 0; f.HoleX = f.X + f.Dir * 0.16f * unit; f.HoleY = f.Y;
                    // the CRATER: it opens as the head goes in (eight steps over the dive-in pages) and stays on the snow afterwards (softening, fading)
                    _cra[_craAt] = new Crater { X = f.HoleX, Y = f.Y, Born = _time, Live = true }; f.CraI = _craAt; _craAt = (_craAt + 1) % _cra.Length; f.CraOpen = 0;
                    Burst(f.HoleX, f.Y, unit, 12, 0.7f, 0.9f, 2);
                }
                break;
            }
            case 3: // DIVE IN
                ShowF(f, FB_DiveIn, Math.Min(7, (int)(c / 0.25f * 8f)), mir);
                f.CraOpen = Math.Min(7, 1 + (int)(c / 0.25f * 6f));
                if (c >= 0.25f) { f.Stage = 4; f.StageT = 0; }
                break;
            case 4: // PLUNGE: hind legs kicking, brush waving
                ShowF(f, FB_Plunge, Cyc(c, 30f, 24, 0f), mir); f.CraOpen = 7;
                if (((int)(c * 6f)) != ((int)((c - dt) * 6f))) Emit(0, f.HoleX, f.Y, 0.05f * unit, f.Dir * (0.2f + 0.3f * (float)Rng.NextDouble()) * unit, 0.5f * unit, 0.5f, 0, 0.8f, 2f * unit);
                if (c >= 2.4f) { f.Stage = 5; f.StageT = 0; }
                break;
            case 5: // BACK OUT with a face full of snow
                ShowF(f, FB_BackOut, Math.Min(29, (int)(c / 1.0f * 30f)), mir);
                if (c >= 1.0f) { f.Stage = 6; f.StageT = 0; Burst(f.HoleX, f.Y, unit, 6, 0.5f, 0.5f, 1); }
                break;
            case 6: // SHAKE the head, flinging snow
                ShowF(f, FB_Shake, Math.Min(47, (int)(c / 1.8f * 48f)), mir);
                if (((int)(c * 10f)) != ((int)((c - dt) * 10f)) && _fb != null)
                    Emit(0, f.X + f.Dir * 0.35f * unit, f.Y, 0.35f * unit, (float)(Rng.NextDouble() * 2 - 1) * 0.8f * unit, 0.3f * unit, 0.5f, 0, 0.9f, 2.2f * unit);
                if (c >= 1.8f) { f.Stage = 7; f.StageT = 0; }
                break;
            case 7: // WIPE: the last snow off the muzzle
                ShowF(f, FB_Wipe, Math.Min(8, (int)(c / 0.3f * 9f)), mir);
                if (c >= 0.3f) { if (f.Sneezes) { f.Stage = 8; f.StageT = 0; } else EndMouse(f); }
                break;
            case 8: // SNEEZE
                ShowF(f, FB_Sneeze, Math.Min(23, (int)(c / 0.75f * 24f)), mir);
                if (((int)(c / 0.75f * 24f)) == 12 && ((int)((c - dt) / 0.75f * 24f)) == 11 && _fb != null) { PointF nz = f.Bk!.Aux[f.Pg]; for (int k = 0; k < 4; k++) Emit(0, f.X + f.Dir * nz.X * unit, f.Y, nz.Y * unit, f.Dir * (0.3f + 0.4f * (float)Rng.NextDouble()) * unit, (0.1f + 0.3f * (float)Rng.NextDouble()) * unit, 0.5f, 0, 0.9f, 1.2f * unit); }
                if (c >= 0.75f) EndMouse(f);
                break;
        }
    }

    private void EndMouse(F f) { Log("fox found nothing and carries on"); BeginFox(f, FD.Stand, 3f + 3f * (float)Rng.NextDouble()); }

    /// <summary>The fox doing as a viewer names it: going and settling are part of a TROT, a look is part of a STAND, getting up is part of a SIT, curling and waking are part of a SLEEP, and the stalk, watch, dash, skid, nosing and digging are one HUNT.</summary>
    private static string Coarse(F f) => f.Mind.Doing switch
    {
        FD.Go or FD.Trot or FD.Settle or FD.Leave => "Trot",
        FD.Stand or FD.Look => "Stand",
        FD.SitIn or FD.Sit => "Sit",
        FD.CurlIn or FD.Asleep or FD.Wake => "Sleep",
        FD.StalkIn or FD.Stalk or FD.StalkOut or FD.DashIn or FD.Chase or FD.Skid or FD.Nose or FD.Dig or FD.Watch => "Hunt",
        _ => f.Mind.Doing.ToString(),
    };

    private void LogFox(F f, float dt)
    {
        if (_log != null)
        {
            float sp = dt > 0 ? MathF.Sqrt((f.X - f.LX) * (f.X - f.LX) + (f.Y - f.LY) * (f.Y - f.LY)) / dt : 0f;
            string pj = "";
            float unit = FU(f.Y);
            if (f.Mind.Doing != FD.Away && f.PVis && f.Bk != null && f.PBk != null && f.Bk.Keys[f.Pg].Length > 1 && f.PBk.Keys[f.PPg].Length > 1)
            {
                float sb2 = f.Mir ? -1f : 1f, sa2 = f.PMir ? -1f : 1f;
                float bx2 = f.X + sb2 * f.Bk.Keys[f.Pg][1].X * unit, by2 = f.Y - f.Bk.Keys[f.Pg][1].Y * unit;
                float ax2 = f.PX + sa2 * f.PBk.Keys[f.PPg][1].X * f.PUnit, ay2 = f.PY - f.PBk.Keys[f.PPg][1].Y * f.PUnit;
                pj = $" dv {MathF.Sqrt((bx2 - ax2) * (bx2 - ax2) + (by2 - ay2) * (by2 - ay2)) / MathF.Max(dt, 1e-4f):F0}";
            }
            if (f.Mind.Doing != FD.Away && f.PVis && f.Bk != null && f.PBk != null && (f.Bk != f.PBk || f.Pg != f.PPg))
            {
                PointF[] a = f.PBk.Keys[f.PPg], b = f.Bk.Keys[f.Pg];
                if (a.Length > 1 && a.Length == b.Length)
                {
                    float sa = f.PMir ? -1f : 1f, sb = f.Mir ? -1f : 1f, worst = 0f;
                    for (int k = 0; k < a.Length; k++)
                    {
                        if (k == 1) continue;
                        float ax = sa * (a[k].X - a[1].X), ay = -(a[k].Y - a[1].Y), bx = sb * (b[k].X - b[1].X), by = -(b[k].Y - b[1].Y);
                        worst = MathF.Max(worst, MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) * unit);
                    }
                    float wax = f.PX + sa * a[1].X * f.PUnit, way = f.PY - a[1].Y * f.PUnit, wbx = f.X + sb * b[1].X * unit, wby = f.Y - b[1].Y * unit;
                    float bodyJump = MathF.Sqrt((wax - wbx) * (wax - wbx) + (way - wby) * (way - wby));
                    pj += $" pj {worst:F1} bj {bodyJump:F1}";
                }
            }
            _log.WriteLine($"{_time,8:F3} fox x {f.X:F1} y {f.Y:F1} v {sp:F1} {f.Mind.Doing}{(f.Turning ? "/Turn" : "")}{(f.Mind.Doing == FD.Mouse ? "/stage" + f.Stage : "")} {f.Bk?.Name}#{f.Pg} dir {f.Dir} c {Coarse(f)}{pj} cv {(f.Mind.Doing != FD.Away && Covered(f.X, f.Y, 0.55f * unit, 0.6f * unit) ? 1 : 0)} sim {Board.Time:F2}");
        }
        f.LX = f.X; f.LY = f.Y;
        f.PBk = f.Bk; f.PPg = f.Pg; f.PMir = f.Mir; f.PX = f.X; f.PY = f.Y; f.PUnit = FU(f.Y); f.PVis = f.Mind.Doing != FD.Away;
    }

    // ---------------------------------------------------------------- test forcing

    private void ForceTest()
    {
        if (_test == "natural") return;
        F f = _fox!; H h0 = _h[0]; float W = S.Width;
        float funit = FU(f.Y);
        void FoxAt(float xf, float depth, int dir) { PointF p = S.FieldPoint(xf * W, depth); f.X = p.X; f.Y = p.Y; f.Dir = dir; f.Mind.Begin(FD.Stand, 3f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f; }
        void Calm() { for (int i = 1; i < _n; i++) _h[i].Mind.Begin(HD.Sit, 60f); }
        switch (_test)
        {
            case "mouse": FoxAt(0.72f, 0.35f, 1); Calm(); f.Mind.Begin(FD.Stand, 0.2f); f.Clock = 0; StartMouseNow(f); break;
            case "sleep": FoxAt(0.80f, 0.30f, -1); f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); f.SleepAfter = true; break;
            case "stalk":
            {
                // hare 0 sits 0.30 U from one of ITS burrows, the fox on its far side: the hare bolts for the burrow
                Bur b0 = _bur[Math.Max(0, h0.Cur)]; int sd = b0.X + 0.36f * S.U < 0.93f * W ? 1 : -1;
                if (!LineClear(b0.X + sd * 0.30f * S.U, b0.Y, b0.X, b0.Y, 0.3f * HU(b0.Y))) sd = -sd;          // the hare's way to the burrow must be open (no bird table between)
                if (_test == "stalk" && Environment.GetEnvironmentVariable("PECKWORKS_STALKSIDE") is string ss) sd = ss == "L" ? -1 : 1;
                PointF p = new PointF(b0.X + sd * 0.30f * S.U, b0.Y); h0.X = p.X; h0.Y = p.Y; h0.Dir = -sd; h0.Mind.Begin(HD.Sit, 60f); h0.Timid = 0.6f; h0.Att.Timid = 0.7f; h0.Vis = true; h0.HidePk = false;
                for (int i = 1; i < _n; i++) { PointF q = S.FieldPoint((0.30f + 0.03f * i) * W, 0.22f); _h[i].X = q.X; _h[i].Y = q.Y; _h[i].Mind.Begin(HD.Sit, 60f); }
                f.X = h0.X + sd * 0.36f * S.U; f.Y = h0.Y; f.Dir = -sd; f.Mind.Begin(FD.Stand, 3f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f; f.Target = 0; StartStalk(f, h0); break;
            }
            case "groom": Calm(); h0.Mind.Begin(HD.Sit, 60f); BeginHare(h0, HD.Groom, 5.5f); break;
            case "binky": Calm(); BeginHare(h0, HD.Binky, 1.1f); break;
            case "nibble": { Calm(); int tw = NearestTwig(h0); if (tw >= 0) StartNibbleTrip(h0, tw); break; }
            case "tall": Calm(); BeginHare(h0, HD.Tall, 4f); break;
            case "thump": Calm(); h0.FearX = h0.X + (h0.Dir * 0.4f * S.U); h0.FearY = h0.Y; h0.FearFox = true; StartThump(h0, 0); break;
            case "peek": Calm(); h0.BX = h0.X; h0.BY = h0.Y; h0.ToBX = h0.X; h0.ToBY = h0.Y; h0.Vis = false; h0.Mind.Begin(HD.Under, 0.1f); break;
            case "nose":
            {
                // the fox stands 3.2 m from the last hare's burrow (it is down in it), facing it, and walks up to nose it
                Calm(); int ti = _n - 1; H ht = _h[ti]; Bur b = _bur[Math.Max(0, ht.Cur)]; ht.Mind.Begin(HD.Under, 200f); ht.Vis = false; ht.HidePk = false; ht.NextPeek = 1e9;
                int sd = -1;
                if (!LineClear(b.X - 2.8f * funit, b.Y, b.X - 0.45f * funit, b.Y, 0.4f * funit)) sd = 1;       // the side with an open lane
                f.X = b.X + sd * 2.8f * funit; f.Y = b.Y; f.Dir = -sd; f.Mind.Begin(FD.Stand, 99f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f;
                f.HoleX = b.X; f.HoleY = b.Y; f.Target = ti; f.NoseNext = true; f.NoseT = 0f; break;
            }
            case "look": FoxAt(0.78f, 0.30f, 1); Board.Post(Sense.Movement, new PointF(f.X, S.Height * 0.1f), 0.8f * S.U, 1f, this); break;
        }
    }

    private void StartMouseNow(F f) { BeginFox(f, FD.Mouse, Mind<FD>.Forever); f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * FU(f.Y); f.Sneezes = true; f.ListenFor = 2.39f; }

    // ================================================================ DRAW

    public override void Collect(DepthQueue q)
    {
        if (_n == 0 || _hb == null) return;
        for (int i = 0; i < _n; i++) if (_h[i].Vis && _h[i].Bk != null) q.Add(_h[i].Y, this, i);
        if (_fox != null && _fox.Mind.Doing != FD.Away && _fox.Bk != null) q.Add(_fox.Y, this, 100);
        if (_decalLive > 0) q.Add(-1f, this, 200);
        for (int k = 0; k < _twigs.Length; k++) q.Add(_twigs[k].Y - 1f, this, 300 + k);
        // the burrows are places: on the snow from the first frame to the last. One a hare (or the fox, nosing it) is performing at draws itself, in two pieces round the animal.
        for (int k = 0; k < _bur.Count; k++)
        {
            Bur b = _bur[k];
            if (b.Who >= 0 && _h[b.Who].Perf && _h[b.Who].Cur == k) continue;
            if (_fox != null && _fox.Perf && _fox.NoseBur == k) continue;
            q.Add(b.Y - 0.5f, this, 400 + k);
        }
        for (int k = 0; k < _cra.Length; k++)
            if (_cra[k].Live && _time - _cra[k].Born < 42.0 && !(_fox != null && _fox.Perf && _fox.CraI == k && _fox.Mind.Doing == FD.Mouse)) q.Add(_cra[k].Y - 0.5f, this, 700 + k);
        for (int i = 0; i < _puffs.Length; i++) if (_puffs[i].On) q.Add(_puffs[i].Y + 0.5f, this, 1000 + i);
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (_hb == null || _puffSprites == null || _decalSprites == null) return;
        if (_log == null) { DrawOne2(fb, index); return; }
        long d0 = System.Diagnostics.Stopwatch.GetTimestamp(); DrawOne2(fb, index); _tickD += System.Diagnostics.Stopwatch.GetTimestamp() - d0; _costStamps += index == 200 ? _decalLive : 1;
    }

    /// <summary>One piece of a burrow (front: the near lip, else the back with the dark mouth), at the hole's own place and the lane's size.</summary>
    private void StampBur(FrameBuffer fb, Bur b, bool front)
    {
        Book bk = _hb![front ? HB_BurF : HB_BurB]; int t = TierOf(b.Y);
        Stamp(fb, bk.Pages[t][Math.Clamp(b.Open, 0, bk.Count - 1)], b.X, b.Y, bk.Ox[t], bk.Oy[t]);
    }

    private void StampCra(FrameBuffer fb, Crater c, int open, bool front, float op)
    {
        Book bk = _fb![front ? FB_CraF : FB_CraB]; int t = TierOf(c.Y);
        Stamp(fb, bk.Pages[t][Math.Clamp(open, 0, bk.Count - 1)], c.X, c.Y, bk.Ox[t], bk.Oy[t], false, op);
    }

    private void DrawOne2(FrameBuffer fb, int index)
    {
        if (index < 100)
        {
            H h = _h[index]; if (h.Bk == null) return;
            int t = TierOf(h.Y);
            // the glide moves the DRAWING (through the page's origin) without moving the feet the depth picture is asked about
            float un = _hUnit[t];
            float gxs = (h.Mir ? -h.Gx : h.Gx) * un; int sy = (int)MathF.Round(-h.Gy * un);
            if (h.Perf && h.Cur >= 0) StampBur(fb, _bur[h.Cur], false);                                   // the hole's back, then the hare, then the hole's front lip OVER the hare
            Stamp(fb, h.Bk.Pages[t][h.Pg], h.X + gxs, h.Y, h.Bk.Ox[t], h.Bk.Oy[t] - sy, h.Mir);          // x goes in as a float (the stamp is sub-pixel), y through the origin so the depth test keeps the true feet
            if (h.Perf && h.Cur >= 0) StampBur(fb, _bur[h.Cur], true);          // x goes in as a float (the stamp is sub-pixel), y through the origin so the depth test keeps the true feet
        }
        else if (index == 100)
        {
            F f = _fox!; if (f.Bk == null) return;
            int t = TierOf(f.Y);
            float un = _fUnit[t];
            float gxs = (f.Mir ? -f.Gx : f.Gx) * un; int sy = (int)MathF.Round(-f.Gy * un);
            bool nose = f.Perf && f.Mind.Doing == FD.Nose, cra = f.Perf && f.Mind.Doing == FD.Mouse;
            if (nose) StampBur(fb, _bur[f.NoseBur], false); else if (cra) StampCra(fb, _cra[f.CraI], f.CraOpen, false, 1f);
            Stamp(fb, f.Bk.Pages[t][f.Pg], f.X + gxs, f.Y, f.Bk.Ox[t], f.Bk.Oy[t] - sy, f.Mir);
            if (nose) StampBur(fb, _bur[f.NoseBur], true); else if (cra) StampCra(fb, _cra[f.CraI], f.CraOpen, true, 1f);
        }
        else if (index == 200)
        {
            // the tracks and burrows lying on the snow, fading with age
            for (int i = 0; i < _decalLive; i++)
            {
                Decal d = _decals[i];
                double age = _time - d.Born; float life = d.Kind == 2 ? 45f : 16f;
                if (age >= life) continue;
                int t = TierOf(d.Y); Sprite sp = _decalSprites![t][d.Kind];
                float op = (float)(1.0 - Math.Max(0.0, (age - life * 0.4) / (life * 0.6)));
                Stamp(fb, sp, d.X, d.Y, sp.Width / 2, sp.Height / 2, false, op);
            }
        }
        else if (index >= 700 && index < 1000)
        {
            Crater c = _cra[index - 700]; double age = _time - c.Born;
            float op = age < 12.0 ? 1f : (float)Math.Max(0.0, 1.0 - (age - 12.0) / 30.0);          // the crater softens: it holds for twelve seconds, then fades over thirty
            StampCra(fb, c, 7, false, op); StampCra(fb, c, 7, true, op);
        }
        else if (index >= 400 && index < 700)
        {
            Bur b = _bur[index - 400]; StampBur(fb, b, false); StampBur(fb, b, true);
        }
        else if (index >= 300 && index < 400)
        {
            var tw = _twigs[index - 300]; int t = TierOf(tw.Y); Book b = _hb![HB_Twig];
            Stamp(fb, b.Pages[t][0], tw.X, tw.Y, b.Ox[t], b.Oy[t]);
        }
        else
        {
            ref Puff p = ref _puffs[index - 1000]; if (!p.On) return;
            int t = TierOf(p.Y); Sprite sp = _puffSprites![t][Math.Clamp((int)p.Size, 0, 2)];
            float k = p.T / p.Life;
            // a puff of snow swells a little and fades; a breath swells more, and is thin
            float op = p.Op * (1f - k) * (p.Kind == 1 ? MathF.Min(1f, k * 5f) : 1f);
            int left = (int)MathF.Round(p.X) - sp.Width / 2, top = (int)MathF.Round(p.Y - p.H) - sp.Height / 2;
            sp.Draw(fb, left, top, op, null, false, S.NearDepth, S.Nearness(p.Y));
        }
    }
}
