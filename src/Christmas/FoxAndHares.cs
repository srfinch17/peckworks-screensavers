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
/// COVER, NOT HOLES (round five; his line: "these holes all over the place for the mice... that's no good. have a bunch of rocks and
/// shrubs and other things for them to hide in. not black holes next to a pond"). There is NO hole in the snow any more. The painter
/// puts stones and bushes on the field (ChristmasScenery.Covers) and puts each in the depth picture with the nearness of its own
/// foot; a hare whose feet stand a little FARTHER than that foot is hidden by it, the way a cat is hidden by a sofa.
///   HIDING: the hare runs to the near END of a cover on a gentle curve, slows, and goes ROUND the end to the back (head first, the body
///   following the same path, the white tail last), settles, and is behind the cover. Behind a LOW stone it lies flat (the LOAF:
///   ears back, chin down); behind a bush tall enough it simply sits. Every 6 to 15 s it PEEKS: the ears go up first, then the head,
///   a slow look one way and the other, a blink, and down again (behind a bush it stands up on its hind legs for a look).
///   COMING OUT: a peek, then it rises, turns if it must, and hops round the end it faces, head first, and sits in the open.
///   THE FOX nosing round a cover: it trots up to the near end, slows, reaches its muzzle at the twigs or over the stones (snow shaken
///   off the bush falls on its head), and goes round the end; the hare breaks out of the OTHER end and bolts for the next cover. A
///   hare with no cover in reach bolts off the side of the screen and comes back the same way later. The fox's MOUSING plunge leaves a
///   soft pale dent, never a hole, and its head is hidden by a low white mound of snow while it is in.
///
/// THE STORY BETWEEN THEM (the part a child watches): when the fox stalks a hare (low, slow, ears flat) the hare notices
/// (a Threat posted on the noticeboard), rears up tall, THUMPS twice (a Friend notice, so every other hare in earshot
/// sits up and bolts too), turns and bolts for the nearest cover on the far side from the fox, round its near end and in behind it. The
/// fox gallops after it, skids to a stop, trots up to the cover, sniffs round its end and goes round it. The hare ALWAYS gets
/// away (it is faster and has the head start). After a while it peeks (ears first, then the head) from behind its new
/// cover, a look round, and it comes out round the end and carries on.
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
///   6 s and the hare bolts to cover, the chase and the nosing follow), sleep (the fox curls up at 6 s), groom, binky, nibble, tall,
///   thump (hare 0 does that at 6 s), look (the fox and hare 0 look up at 6 s), hide (hare 0 is in the open near its cover and
///   goes behind it at 14 s, calmly), peek (hare 0 is behind its cover and peeks at 14 s), out (hare 0 is behind its cover and
///   comes out at 14 s), flee (hare 0 bolts for the nearest cover at 14 s), offscreen (no cover is near: it bolts off the side),
///   nose (the fox is 2.8 m from hare 0's cover, hare 0 is behind it: it walks up, noses round the end, and the hare breaks out the other end).
///   PECKWORKS_COVERLOG=1 writes each hide spot's measured coverage to the mind log.
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
    //   Thump: tall, two strikes, then Bolt (cannot be cut).  Binky: one leap, never cut.  Bolt: runs to cover (or off the screen).
    //   Hidden: behind a cover, in stages (settle into a loaf, idle, a peek: rise, look, lower, and the way out). Only the FOX cutting in
    //   (it reaches the cover) ends it early, by the quickest eased rise; it leaves from the sit pose. Turn: 0.45 s.
    private enum HD { Sit, Look, Nibble, Groom, Go, Hop, Land, Tall, Thump, Binky, Bolt, Hidden, Turn }
    // Fox doings. Mouse is one doing with stages (listen, coil, leap, dive in, plunge, back out, shake, wipe, sneeze):
    // nothing cuts it once the leap has begun; a Loud during the listening stage ends it (the fox looks up).
    private enum FD { Away, Go, Trot, Settle, Stand, Look, Turn, SitIn, Sit, SitOut, CurlIn, Asleep, Wake, Mouse, StalkIn, Stalk, StalkOut, DashIn, Chase, Skid, Nose, Leave, Watch }

    private const int HB_Sit = 0, HB_Look = 1, HB_Reach = 2, HB_Nibble = 3, HB_Groom = 4, HB_Hop = 5, HB_Flee = 6, HB_Settle = 7, HB_TallIn = 8, HB_Tall = 9, HB_Thump = 10, HB_Binky = 11, HB_Turn = 12, HB_Twig = 13, HB_NLook = 14, HB_SettleF = 15, HB_Loaf = 16, HB_LoafRise = 17;
    private const int FB_Stand = 0, FB_Look = 1, FB_Trot = 2, FB_Settle0 = 3, FB_Settle1 = 4, FB_Stalk = 5, FB_StalkIn = 6, FB_Gallop = 7, FB_Skid = 8, FB_Sit = 9, FB_SitIn = 10, FB_CurlIn = 11, FB_Sleep = 12, FB_Listen = 13, FB_Coil = 14, FB_Leap = 15, FB_DiveIn = 16, FB_Plunge = 17, FB_BackOut = 18, FB_Shake = 19, FB_Wipe = 20, FB_Sneeze = 21, FB_NoseIn = 22, FB_Nose = 23, FB_Turn = 24, FB_DashIn = 25, FB_Lower = 26, FB_Watch = 27, FB_Wake0 = 28, FB_DentB = 32, FB_DentF = 33;
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
        public float BX, BY;                           // the hide spot it runs to or sits in (the fox chases toward it)
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
        // COVER (round five). Spot is the hide spot it is behind, or running to (-1 none). THE ROUTE is a path in the open that never doubles back
        // in x: up to four points (RX, RY), the run's own x-distance eased in y between them, so a hare goes round the end of a cover on a curve.
        // ToHide: the run in hand ends in a hide (land, then Hidden). Hidden is played in stages: HStage 0 settle into the loaf, 1 idle,
        // 2 rise (a peek, or the way out), 3 hold and look, 4 lower. HT is the stage's clock, HideT the whole hide's, HideFor how long it means to stay,
        // PkHold how long the head stays up, NextPeek the scene time of the next peek. Leaving: the hide is over (peek, then out); Breaking: out NOW (the fox).
        // Off: it ran off the screen's side and waits there (not drawn) OffFor seconds, then walks back in from OffSide.
        public int Spot = -1, NextSpot = -1, RN, RDir = 1, HStage, OffSide, ExitFails; public bool RouteOn, RouteOff, ToHide, Leaving, Breaking, Off; public float HT, HideT, HideFor, PkHold, OffFor;
        public readonly float[] RX = new float[4], RY = new float[4]; public double NextPeek;
    }
    /// <summary>
    /// A HIDE SPOT: the place behind a cover where a hare's feet go. X, Y are the feet (a little farther than the cover's foot, so the cover's depth
    /// hides what it is taller than); Low is true where only a lying hare (the loaf) is hidden, false where a sitting hare is; CoverU the measured
    /// hidden height there in hare units; EdgeL and EdgeR the x of the cover's two ends at the hare's height (a hare goes round an end); FrontY the
    /// row in the open it comes out on. Who is the hare in or bound for it (-1 none).
    /// </summary>
    private sealed class Spot { public int Cov; public float X, Y, CoverU, EdgeL, EdgeR, FrontY; public bool Low; public int Who = -1; }
    /// <summary>The soft dent of a fox's mousing plunge: it stays on the snow, a pale dimple that fades.</summary>
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
        public int NoseSpot = -1, CraI = -1, CraOpen = 7; public bool Perf;   // the hide spot whose cover it is nosing round; its dent and how far pressed in it is; true while its head is behind the dent's front mound
        public int Rounds; public bool RoundNext, Loose, SnowDone; public int NoseDir = 1;      // how many times round a cover this hunt; set when the sniff is over and it goes round the end; which way it faces at the cover
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
    private readonly List<Spot> _spots = new();
    private readonly bool _coverLog = Environment.GetEnvironmentVariable("PECKWORKS_COVERLOG") == "1";
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
        foreach (Cover cv in s.Covers) if (cv.Kind is CoverKind.Shrub or CoverKind.Rocks) _yLo = MathF.Min(_yLo, cv.Hide.Y - 1f);      // ...except for the hide rows behind the bushes, which lie a little above it
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
        if (_test is "stalk" or "nose" or "peek" or "out" or "break" or "flee" or "hide" or "offscreen") homes[0] = 0.92f;       // the test hare lives at the cover by the far right pines, which has open ground on both sides
        if (float.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_HARE0X"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h0x)) homes[0] = h0x;      // test aid: where hare 0 starts (a fraction of the width), to pick its cover
        var twigs = new List<(float, float)>();
        for (int i = 0; i < _n; i++)
        {
            var h = new H { Timid = timid[i], Curious = curious[i], Play = play[i], IdleOff = (float)Rng.NextDouble() * 24f, Dir = Rng.Next(2) == 0 ? 1 : -1 };
            h.Att = new Attention(0.25f + 0.6f * h.Timid, 0.2f + 0.7f * h.Curious) { Keen = 1.4f };
            PlaceNear(homes[i] * W, 0.5f * W, out h.X, out h.Y, 0.35f * HareScale * 0.1f * s.U);
            h.IdleOff = 2.4f;
            _h[i] = h;
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
        // THE COVER: the hide spots behind the painter's stones and bushes, measured off the depth picture; THE ENTRANCE: every hare begins behind a
        // cover of its own (the nearest free spot to its home) and comes out, one after another, over the first 20 s, by the same performance as ever
        BuildSpots();
        for (int i = 0; i < _n; i++) StartHidden(i, _h[i], homes[i] * W, i == 0 && _test is "peek" or "out" or "nose" or "break" ? 1e9f : 3.5f + 6.5f * i + 3f * (float)Rng.NextDouble());
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
                    Oval(0.035f * m * 1.5f, 0.02f * m, Color.FromArgb(140, 140, 158, 200)),         // 2 fox print
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

    // ================================================================ COVER: the hide spots, the routes round them

    /// <summary>
    /// HOW MUCH COVER STANDS ABOVE (x, y)? Pixels, counted upward from the feet while the depth picture says a thing NEARER than something
    /// standing at y is there (a gap of two pixels or less is bridged: a bush is made of twigs). The depth picture is exactly what will hide
    /// the hare, so this is a MEASUREMENT of what will be hidden, not a guess from the painter's numbers.
    /// </summary>
    private int CoverRun(float x, float y, int limit)
    {
        int W = S.Width, px = (int)x; byte me = S.Nearness(y); byte[] nd = S.NearDepth;
        if (px < 0 || px >= W) return 0;
        int run = 0, gap = 0;
        for (int k = 1; k <= limit; k++)
        {
            int py = (int)y - k; if (py < 0) break;
            if (nd[py * W + px] > me) { run = k; gap = 0; } else if (++gap > 2) break;
        }
        return run;
    }

    /// <summary>The shortest run of cover across a hare's body (from 0.45 of a body length behind the feet to 0.55 ahead of them), in hare units.</summary>
    private float CoverU(float x, float y)
    {
        float hu = HU(y); int lim = (int)(2.2f * hu), best = int.MaxValue;
        for (float dx = -0.45f * hu; dx <= 0.55f * hu; dx += 2f) best = Math.Min(best, CoverRun(x + dx, y, lim));
        return best / hu;
    }

    /// <summary>How far (x) the cover reaches from x in direction dir at a hare's low height (0.18 of a body length up): where its end is. Gaps of three pixels are bridged.</summary>
    private float CoverEdge(float x, float y, int dir, float limit)
    {
        int W = S.Width; byte me = S.Nearness(y); byte[] nd = S.NearDepth;
        int py = (int)(y - 0.18f * HU(y)); float edge = x; int gap = 0;
        if (py < 0 || py >= S.Height) return x;
        for (float d = 0; d < limit; d += 1f)
        {
            int px = (int)(x + dir * d); if (px < 0 || px >= W) break;
            if (nd[py * W + px] > me) { edge = x + dir * d; gap = 0; } else if (++gap > 3) break;
        }
        return edge;
    }

    /// <summary>
    /// THE HIDE SPOTS. For each bush and stone on the field, the best places behind it for a hare (a big cover takes two hares, a small one
    /// one): feet a little farther than the cover's foot, where the depth picture hides the most of a body. A spot where a SITTING hare is
    /// hidden (its ears at 0.93 of a body length) is a sitting spot; one that hides at least 0.46 (a lying hare's height) is a LOAF spot;
    /// less than that is no hiding place for a hare. A hare uses a Shrub or Rocks only.
    /// </summary>
    private void BuildSpots()
    {
        float W = S.Width;
        for (int ci = 0; ci < S.Covers.Count; ci++)
        {
            Cover c = S.Covers[ci];
            if (c.Kind is not (CoverKind.Shrub or CoverKind.Rocks)) continue;
            float y = c.Hide.Y, hu = HU(y);
            float xBest = 0f, uBest = -1f, xOther = 0f, uOther = -1f;
            for (float x = c.Foot.X - c.HalfW * 0.85f; x <= c.Foot.X + c.HalfW * 0.85f; x += 2f)
            {
                float u = CoverU(x, y);
                if (u > uBest + 0.001f) { uBest = u; xBest = x; }
            }
            // the second hare: the best place at least one body length from the first
            for (float x = c.Foot.X - c.HalfW * 0.85f; x <= c.Foot.X + c.HalfW * 0.85f; x += 2f)
            {
                if (MathF.Abs(x - xBest) < 1.05f * hu) continue;
                float u = CoverU(x, y);
                if (u > uOther + 0.001f) { uOther = u; xOther = x; }
            }
            if (Environment.GetEnvironmentVariable("PECKWORKS_COVERLOG") == "1") Log($"cover {ci} {c.Kind} foot {c.Foot.X:F0},{c.Foot.Y:F0} halfW {c.HalfW:F0} h {c.Height:F0} hide row {y:F0}: best x {xBest:F0} hides {uBest:F2} hare lengths, second x {xOther:F0} hides {uOther:F2} (hare unit {hu:F1} px)");
            void Add(float x, float u)
            {
                if (u < 0.46f || x < 0.03f * W || x > 0.97f * W || !S.CanStand(x, y, 0.2f * hu)) return;
                float lim = c.HalfW * 1.5f;
                var sp = new Spot { Cov = ci, X = x, Y = y, CoverU = u, Low = u < 0.97f };
                sp.EdgeL = CoverEdge(x, y, -1, lim); sp.EdgeR = CoverEdge(x, y, 1, lim);
                sp.FrontY = MathF.Max(S.SmallBandTop + 0.006f * S.U, y);
                _spots.Add(sp);
            }
            Add(xBest, uBest);
            if (c.Kind == CoverKind.Shrub || c.HalfW * 2f >= 2.4f * hu) Add(xOther, uOther);
        }
        for (int k = 0; k < _spots.Count; k++) Log($"spot {k}: cover {_spots[k].Cov} at {_spots[k].X:F0},{_spots[k].Y:F0} {(_spots[k].Low ? "loaf" : "sit")} hides {_spots[k].CoverU:F2}, ends {_spots[k].EdgeL:F0} to {_spots[k].EdgeR:F0}");
    }

    /// <summary>The start of the scene: hare i is behind the free spot nearest its home, hidden for "after" seconds, facing the end it will come out of.</summary>
    private void StartHidden(int i, H h, float homeX, float after)
    {
        int best = -1; float bd = float.MaxValue;
        for (int k = 0; k < _spots.Count; k++)
        {
            if (_spots[k].Who >= 0) continue;
            float d = MathF.Abs(_spots[k].X - homeX);
            if (d < bd) { bd = d; best = k; }
        }
        if (best < 0) { h.Mind.Begin(HD.Sit, 4f + 3f * i); h.Clock = 0f; return; }       // no cover for it: it simply sits where it was put
        Spot sp = _spots[best]; sp.Who = i; h.Spot = best; h.X = sp.X; h.Y = sp.Y; h.BX = sp.X; h.BY = sp.Y;
        h.Dir = sp.X < S.Width * 0.5f ? 1 : -1;                                        // toward the middle of the screen
        if (!PlanExit(h, i, sp, h.Dir, false, false)) h.Dir = -h.Dir;
        BeginHidden(h, i, after);
        h.NextPeek = 1e9;                                                              // its only look before the way out is the way out's own
    }

    /// <summary>A hare is behind its cover: the stages begin (a low cover: it settles into a loaf first).</summary>
    private void BeginHidden(H h, int i, float stayFor)
    {
        Spot sp = _spots[h.Spot];
        BeginHare(h, HD.Hidden, Mind<HD>.Forever);
        h.IdleOff = 2.4f; h.HStage = sp.Low ? 0 : 1; h.HT = 0f; h.HideT = 0f; h.HideFor = stayFor;
        h.Leaving = false; h.Breaking = false; h.ToHide = false; h.RouteOn = false; h.Resp = Resp.None; h.GoFlee = false; h.Off = false;
        h.NextPeek = _time + 5.0 + 6.0 * Rng.NextDouble();
        h.BX = sp.X; h.BY = sp.Y;
    }

    // ---- routes: a path in the open that never doubles back in x ----

    private readonly float[] _tx = new float[4], _ty = new float[4]; private int _tn;
    private bool _lax;      // after three failed tries to leave a cover the way out ignores bodies standing on it (a hare that never leaves is worse than one that waits a stride for somebody to move)

    /// <summary>Puts a point on the route being drawn up; false if it doubles back in x (or is too close to the last one) for travel in direction dir.</summary>
    private bool RoutePoint(int dir, float x, float y)
    {
        if (_tn > 0 && (x - _tx[_tn - 1]) * dir < 1f) return false;
        _tx[_tn] = x; _ty[_tn] = y; _tn++;
        return true;
    }

    /// <summary>The y of the route at x: each stretch between two points eased in y (slow, quicker, slow), so a route round the end of a cover is a CURVE, never a corner.</summary>
    private float RouteY(H h, float x)
    {
        float t = (x - h.RX[0]) * h.RDir;
        for (int k = 0; k < h.RN - 1; k++)
        {
            float a = (h.RX[k] - h.RX[0]) * h.RDir, b = (h.RX[k + 1] - h.RX[0]) * h.RDir;
            if (t <= b || k == h.RN - 2) { float u = Math.Clamp((t - a) / MathF.Max(1f, b - a), 0f, 1f); return h.RY[k] + (h.RY[k + 1] - h.RY[k]) * FoxAndHaresArt.Sm(u); }
        }
        return h.RY[h.RN - 1];
    }

    /// <summary>
    /// Is the drawn-up route (in _tx, _ty) walkable and free? Every eight pixels: feet allowed (off the ice, clear of every prop, the cover's own
    /// base strip), nobody else standing there. Past the screen's side edges nothing is tested (a hare running off the screen).
    /// </summary>
    private bool RouteFree(int dir, float unit, int self)
    {
        float W = S.Width;
        float x0 = _tx[0], x1 = _tx[_tn - 1];
        for (float x = x0 + dir * 8f; (x1 - x) * dir > 0f; x += dir * 8f)
        {
            float y = RouteYTemp(x, dir);
            if (x < 0.02f * W || x > 0.98f * W) continue;
            if (!S.CanStand(x, y, 0.25f * unit) || (!_lax && S.Crowded(x, y, 0.5f * unit, this, self))) return false;
        }
        return true;
    }

    private float RouteYTemp(float x, int dir)
    {
        float t = (x - _tx[0]) * dir;
        for (int k = 0; k < _tn - 1; k++)
        {
            float a = (_tx[k] - _tx[0]) * dir, b = (_tx[k + 1] - _tx[0]) * dir;
            if (t <= b || k == _tn - 2) { float u = Math.Clamp((t - a) / MathF.Max(1f, b - a), 0f, 1f); return _ty[k] + (_ty[k + 1] - _ty[k]) * FoxAndHaresArt.Sm(u); }
        }
        return _ty[_tn - 1];
    }

    /// <summary>Every stretch must lean no more than a shallow diagonal (0.28 of its x-length): a lane change is a slant, never a slide up the screen.</summary>
    private bool RouteShallow(int dir, float maxSlope = 0.28f)
    {
        for (int k = 0; k < _tn - 1; k++) if (MathF.Abs(_ty[k + 1] - _ty[k]) > maxSlope * (_tx[k + 1] - _tx[k]) * dir) return false;
        return true;
    }

    /// <summary>n whole bounds of cycle cyc over an x-distance D, each stretched by k within kMin..kMax (the nearest to even), so the last bound lands exactly on the rest page at the end.</summary>
    private static bool PlanBounds(float D, float cyc, float kMin, float kMax, out int n, out float k)
    {
        n = Math.Max(1, (int)MathF.Round(D / cyc)); k = 1f; float best = float.MaxValue; bool ok = false;
        for (int c = Math.Max(1, n - 1); c <= n + 1; c++)
        {
            float kk = D / (c * cyc); if (kk < kMin || kk > kMax) continue;
            float err = MathF.Abs(kk - 1f); if (err < best) { best = err; n = c; k = kk; ok = true; }
        }
        return ok;
    }

    /// <summary>Takes the route in _tx, _ty as hare h's own, with its bounds: the route is now what h.X, h.Y follow.</summary>
    private void CommitRoute(H h, int dir, int n, float k)
    {
        h.RN = _tn; h.RDir = dir; for (int q = 0; q < _tn; q++) { h.RX[q] = _tx[q]; h.RY[q] = _ty[q]; }
        h.Hops = n; h.HopK = k; h.RouteOn = true; h.RouteOff = false;
    }

    /// <summary>
    /// THE RUN TO COVER: from where the hare is (or from behind another cover, "fromHidden": out along that one's row past its far end first),
    /// on a curve to the near END of the cover, a body length short of its edge, then in along the hide row to the spot, the whole body passing
    /// behind the cover's edge a part at a time. Head first, the same way it was running: nothing turns round. commit false only asks "could it".
    /// </summary>
    private bool PlanEnter(H h, int i, int spotIdx, int dir, bool flee, bool commit, bool fromHidden = false)
    {
        Spot sp = _spots[spotIdx]; float hu = HU(sp.Y);
        _tn = 0;
        if (!RoutePoint(dir, h.X, h.Y)) return false;
        if (fromHidden && h.Spot >= 0)
        {
            Spot from = _spots[h.Spot];
            if (!RoutePoint(dir, dir > 0 ? from.EdgeR + 0.8f * hu : from.EdgeL - 0.8f * hu, from.Y)) return false;
        }
        float w1x = dir > 0 ? sp.EdgeL - 0.80f * hu : sp.EdgeR + 0.80f * hu;
        if (!RoutePoint(dir, w1x, sp.Y) || !RoutePoint(dir, sp.X, sp.Y)) return false;
        if (!RouteShallow(dir)) return false;
        float D = (sp.X - h.X) * dir, cyc = (flee ? FoxAndHaresArt.HareFleeD : FoxAndHaresArt.HareHopD) * HU(h.Y);
        if (!PlanBounds(D, cyc, flee ? 0.75f : 0.80f, flee ? 1.35f : 1.25f, out int n, out float k)) return false;
        if (!RouteFree(dir, hu, i)) return false;
        if (commit) { CommitRoute(h, dir, n, k); h.ToHide = true; h.NextSpot = spotIdx; sp.Who = i; h.BX = sp.X; h.BY = sp.Y; }
        return true;
    }

    /// <summary>
    /// THE WAY OUT: from the spot along the hide row to a body length past the cover's end the hare faces (dir), then down on a curve to the row in the
    /// open (the spot's FrontY). Head first; the body follows the same path; it sits when it lands. commit false only asks "could it".
    /// </summary>
    private bool PlanExit(H h, int i, Spot sp, int dir, bool flee, bool commit)
    {
        float hu = HU(sp.Y);
        float p1x = dir > 0 ? sp.EdgeR + 0.80f * hu : sp.EdgeL - 0.80f * hu;
        float cyc = (flee ? FoxAndHaresArt.HareFleeD : FoxAndHaresArt.HareHopD) * hu;
        // a few ways out: the landing a little farther along (a prop may stand at the first), down on the open row or straight on along the hide row
        foreach (float ln in new[] { 1.5f, 2.6f, 3.8f, 5.5f, 7.5f })
            foreach (float ry in new[] { sp.FrontY })
            {
                _tn = 0;
                if (!RoutePoint(dir, sp.X, sp.Y) || !RoutePoint(dir, p1x, sp.Y)) return false;
                float dy = ry - sp.Y;
                float p2x = p1x + dir * MathF.Max(ln * hu, MathF.Abs(dy) / 0.38f);
                if (!RoutePoint(dir, p2x, ry)) continue;
                if (_coverLog) Log($"exit try dir {dir} len {ln} row {ry:F0}: landing {p2x:F0} in-screen {p2x >= 0.04f * S.Width && p2x <= 0.96f * S.Width} stand {S.CanStand(p2x, ry, 0.3f * hu)} crowded {S.Crowded(p2x, ry, 0.6f * hu, this, i)}");
                if (p2x < 0.04f * S.Width || p2x > 0.96f * S.Width || !Valid(p2x, ry, 0.3f * hu, true) || Covered(p2x, ry, 0.5f * hu, 0.9f * hu) || (!_lax && S.Crowded(p2x, ry, 0.6f * hu, this, i))) continue;       // the landing must be open snow, not behind a prop (a hare out of a cover up in the herd's band may land there: it hides again or hops down)
                if (_coverLog) Log($"   landing ok at {p2x:F0}: shallow {RouteShallow(dir, 0.40f)}, bounds {PlanBounds((p2x - sp.X) * dir, cyc, flee ? 0.75f : 0.80f, flee ? 1.35f : 1.25f, out _, out _)} (D {(p2x - sp.X) * dir:F0} cyc {cyc:F0}), free {RouteFree(dir, hu, i)}");
                if (!RouteShallow(dir, 0.40f)) continue;
                float D = (p2x - sp.X) * dir;
                if (!PlanBounds(D, cyc, flee ? 0.75f : 0.80f, flee ? 1.35f : 1.25f, out int n, out float k)) continue;
                if (!RouteFree(dir, hu, i)) continue;
                if (commit) { CommitRoute(h, dir, n, k); h.ToHide = false; }
                return true;
            }
        return false;
    }
    /// <summary>
    /// THE RUN OFF THE SCREEN (no cover in reach): along the hare's row (from behind a cover, out past its far end first) to two and a half body lengths
    /// beyond the side edge in direction dir. It waits there out of sight and walks back in the same way.
    /// </summary>
    private bool PlanOff(H h, int i, int dir, bool flee, bool commit, bool fromHidden)
    {
        float hu = HU(h.Y), W = S.Width;
        _tn = 0;
        if (!RoutePoint(dir, h.X, h.Y)) return false;
        if (fromHidden && h.Spot >= 0)
        {
            Spot from = _spots[h.Spot];
            if (!RoutePoint(dir, dir > 0 ? from.EdgeR + 0.8f * hu : from.EdgeL - 0.8f * hu, from.Y)) return false;
        }
        float ex = dir > 0 ? W + 2.5f * hu : -2.5f * hu;
        if (!RoutePoint(dir, ex, _ty[_tn - 1])) return false;
        float D = (ex - h.X) * dir, cyc = (flee ? FoxAndHaresArt.HareFleeD : FoxAndHaresArt.HareHopD) * hu;
        if (!PlanBounds(D, cyc, flee ? 0.75f : 0.80f, flee ? 1.35f : 1.25f, out int n, out float k)) return false;
        if (_coverLog) Log($"off try dir {dir}: points {_tn}, D {D:F0} cyc {cyc:F0}, free {RouteFree(dir, hu, i)}");
        if (!RouteFree(dir, hu, i)) return false;
        if (commit) { CommitRoute(h, dir, n, k); h.ToHide = false; h.RouteOff = true; h.OffSide = dir; }
        return true;
    }

    private int TierOf(float y) => Math.Clamp((int)MathF.Floor((y - _yLo) / MathF.Max(1f, _yHi - _yLo) * Tiers), 0, Tiers - 1);
    private float HU(float y) => _hUnit[TierOf(y)];
    private float FU(float y) => _fUnit[TierOf(y)];

    /// <summary>May feet stand at (x, y)? On the snowfield between depth 0.18 and 0.60, the ice and every prop's foot excluded (ChristmasScenery.CanStand).</summary>
    private bool Valid(float x, float y, float room, bool loose = false)
    {
        float W = S.Width;
        if (x < 0.02f * W || x > 0.98f * W) return false;
        float wy = S.WalkY(x), d = (y - wy) / (S.Height - wy);
        if (loose) { if (d < 0.10f || d > 0.80f || y < S.HerdBandTop - 0.03f * S.U) return false; }       // a fox at a cover may stand on the rows behind it, up in the herd's band (nobody's feet are there: S.Crowded sees to that)
        else if (d < 0.18f || d > 0.64f || y < S.SmallBandTop) return false;     // never in the herd's band
        // never rest BEHIND the bird table or the tiny fir (the props would hide most of the animal: it would read as missing)
        if (MathF.Abs(x - S.FeederFoot.X) < 0.075f * S.U && y <= S.FeederFoot.Y + 3f && y > S.FeederTray.At.Y) return false;
        if (MathF.Abs(x - S.SaplingFoot.X) < MathF.Max(0.04f * S.U, S.SaplingHeight * 0.45f) && y <= S.SaplingFoot.Y + 3f && y > S.SaplingFoot.Y - S.SaplingHeight) return false;
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
        HD.Thump or HD.Bolt => "Flee",
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
        h.Vis = !h.Off;                       // a hare behind a cover is still DRAWN: the cover hides it, nothing else does (only one off the screen's side is not)
        HD d = h.Mind.Doing;
        if (h.Off)
        {
            // waiting off the side of the screen after a run for the edge: it comes back the same way, walking in
            h.OffFor -= dt;
            if (h.OffFor <= 0f) ReturnFromOff(h, i);
            LogHare(i, h, dt);
            return;
        }

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
                    if (h.GoFlee) { h.Mind.Begin(HD.Bolt, Mind<HD>.Forever); h.Clock = 0; h.Dur = 12f; h.HopsDone = 0; Log(h.RouteOff ? $"hare{i} bolts off the screen's side" : h.ToHide ? $"hare{i} bolts for the cover at {h.BX:F0},{h.BY:F0}" : $"hare{i} bolts out from behind its cover"); }
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
                if (h.RouteOn) h.Y = RouteY(h, h.X);        // a run to or from cover follows its curve
                else
                {
                    float dxLeft = MathF.Max(1f, (h.GX - h.X) * h.Dir);
                    h.Y += Math.Clamp((h.GY - h.Y) * Math.Min(1f, step / dxLeft), -0.6f * step, 0.6f * step);
                }
                float f2 = h.Odo - MathF.Floor(h.Odo);
                Show(h, HB_Hop, (int)(f2 * 24f) % 24, mir);
                HareLandings(h, f2, unit, FoxAndHaresArt.HareHopD, false);
                // at every rest page: is somebody standing where the next hop would land, or has a reaction asked for a stop? Then this is the last hop.
                // (A run to cover is never cut by a reaction: heading for cover IS the reaction; only a body in the way stops it, and then it gives up the hide.)
                int crossed = (int)MathF.Floor(h.Odo - FoxAndHaresArt.HopRest);
                if (crossed > h.HopsDone)
                {
                    h.HopsDone = crossed;
                    bool crowd = crossed < h.Hops && S.Crowded(h.X + h.Dir * cyc * 0.9f, h.RouteOn ? RouteY(h, h.X + h.Dir * cyc * 0.9f) : h.Y, 0.5f * unit, this, i);
                    if (crossed < h.Hops && ((h.Resp != Resp.None && !h.RouteOn) || crowd)) { h.Hops = crossed; if (h.RouteOn) AbortRoute(h); }
                }
                if (h.Odo >= FoxAndHaresArt.HopRest + h.Hops) { h.Pace.Halt(); ArriveRun(h, i); }
                break;
            }
            case HD.Land:
            {
                Show(h, h.GoFlee ? HB_SettleF : HB_Settle, Math.Min(7, (int)(h.Clock / 0.24f * 8f)), mir);
                if (h.Mind.Over)
                {
                    bool wasFlee = h.GoFlee;
                    h.GoFlee = false;
                    if (h.ToHide && h.NextSpot >= 0) { h.Spot = h.NextSpot; h.NextSpot = -1; BeginHidden(h, i, wasFlee ? 14f + 14f * (float)Rng.NextDouble() : 9f + 16f * (float)Rng.NextDouble()); break; }      // it is behind its cover: now it hides
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
                // BOLT: a fast run along its ROUTE (round the near end of a cover and in behind it, or off the screen's side); the pace eases up under a
                // cap, never starts at full speed. The run was PLANNED (PlanEnter, PlanOff, PlanExit): a whole number of bounds (h.Hops), each a touch
                // stretched or squeezed (h.HopK), so the n-th gathered rest page lands the hare exactly at the end of the route.
                h.Pace.Want = BoltSpeed * unit;
                float step = h.Pace.Tick(dt);
                float cyc = FoxAndHaresArt.HareFleeD * unit * h.HopK;
                h.Odo += step / cyc; h.X += h.Dir * step;
                if (h.RouteOn) h.Y = RouteY(h, h.X);
                float f2 = h.Odo - MathF.Floor(h.Odo);
                Show(h, HB_Flee, (int)(f2 * 16f) % 16, mir);
                bool rest = h.LastFrac < 0.9375f && f2 >= 0.9375f && f2 - h.LastFrac < 0.5f;
                HareLandings(h, f2, unit, FoxAndHaresArt.HareFleeD, true);
                if (rest && ++h.HopsDone >= h.Hops) ArriveRun(h, i);
                break;
            }
            case HD.Hidden:
            {
                // BEHIND COVER, in stages (see the field notes on H): 0 settle into the loaf (a low cover only), 1 idle, 2 rise, 3 hold and look, 4 lower.
                // The hare never moves while hidden; the cover's depth hides what it is lower than. Every stage hands over on the pose the next begins on:
                // the loaf rise's page 0 is the loaf and its page 23 is the sit page; the tall-in book's page 0 is the sit page and its page 11 the stand.
                Spot sp = _spots[h.Spot]; bool low = sp.Low;
                h.X = sp.X; h.Y = sp.Y; h.HT += dt; h.HideT += dt;
                bool foxClose = FoxClose(sp);
                if (!h.Breaking && FoxAtCover(sp)) BreakOut(h, i);
                switch (h.HStage)
                {
                    case 0:
                    {
                        float s = 1f - FoxAndHaresArt.Sm(h.HT / 0.7f);
                        Show(h, HB_LoafRise, (int)MathF.Round(s * 23f), mir);
                        if (h.HT >= 0.7f) { h.HStage = 1; h.HT = 0f; h.Clock = 0f; }
                        break;
                    }
                    case 1:
                    {
                        if (low) Show(h, HB_Loaf, Cyc(h.Clock, 8f, 24, 0f), mir); else Show(h, HB_Sit, Cyc(h.Clock, 20f, 48, h.IdleOff), mir);
                        bool gate = low || SitGate(h);
                        bool leave = h.Breaking || h.Leaving || (h.HideT >= h.HideFor && !foxClose) || h.HideT > 60f;
                        if (leave && gate)
                        {
                            // the way out is a PEEK first (a rise and a look), then out; a bolt from the fox skips the look
                            if (!h.Leaving && !h.Breaking) { h.Leaving = true; }
                            if (h.Breaking && !low) StartPlanned(h, i);
                            else { h.HStage = 2; h.HT = 0f; h.PkHold = 0.9f + 0.7f * (float)Rng.NextDouble(); }
                        }
                        else if (_time >= h.NextPeek && !foxClose && gate && h.HideT < h.HideFor - 3.5f) { h.HStage = 2; h.HT = 0f; h.PkHold = 1.3f + 1.4f * (float)Rng.NextDouble(); }
                        break;
                    }
                    case 2:
                    {
                        float dur = h.Breaking ? 0.45f : 0.95f, s = FoxAndHaresArt.Sm(Math.Min(1f, h.HT / dur));
                        if (low) Show(h, HB_LoafRise, (int)MathF.Round(s * 23f), mir); else Show(h, HB_TallIn, (int)MathF.Round(s * 11f), mir);
                        if (h.HT >= dur)
                        {
                            if (low && h.Breaking) StartPlanned(h, i);
                            else { h.HStage = 3; h.HT = 0f; h.Clock = 0f; }
                        }
                        break;
                    }
                    case 3:
                    {
                        // the hold: a sit (or, behind a bush, a stand on the hind legs) with its own idle: the ears swivel one at a time, the nose works, a blink
                        bool gate;
                        if (low) { Show(h, HB_Sit, Cyc(h.Clock, 20f, 48, 2.4f), mir); gate = SitGate(h); }
                        else { int hp = Cyc(h.Clock, 16f, 24, 0f); Show(h, HB_Tall, hp, mir); gate = hp is 0 or 1 or 10 or 22 or 23; }
                        if (h.HT >= (h.Breaking ? 0.25f : h.PkHold) && gate)
                        {
                            if (low && (h.Leaving || h.Breaking)) StartPlanned(h, i);        // already on the sit pose: out
                            else { h.HStage = 4; h.HT = 0f; }
                        }
                        break;
                    }
                    default:
                    {
                        float dur = h.Breaking ? 0.40f : 0.85f, s = 1f - FoxAndHaresArt.Sm(Math.Min(1f, h.HT / dur));
                        if (low) Show(h, HB_LoafRise, (int)MathF.Round(s * 23f), mir); else Show(h, HB_TallIn, (int)MathF.Round(s * 11f), mir);
                        if (h.HT >= dur)
                        {
                            h.HStage = 1; h.HT = 0f; h.Clock = 0f; h.NextPeek = _time + 6.0 + 9.0 * Rng.NextDouble();
                            if (!low && (h.Leaving || h.Breaking)) StartPlanned(h, i);       // lowered to the sit pose: out
                        }
                        break;
                    }
                }
                break;
            }
        }
        if (h.Vis) S.Occupy(this, i, h.X, h.Y, 0.5f * unit);
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
        for (int j = 0; j < _n; j++) if (j != i && MathF.Abs(_h[j].X - h.X) < 0.35f * S.U && _h[j].Mind.Doing is not HD.Hidden && !_h[j].Off) pal = true;
        // a sit is the rest between doings: now it travels, forages (at a twig, or at the snow itself), washes, stands tall, or plays
        int face = Dice.Pick(Rng, [0.0f, 0.28f, 0.18f, lull ? 0.05f : 0.38f, 0.05f, lull ? 0f : 0.03f + (pal ? 0.20f : 0.03f) * h.Play, lull ? 0.10f : 0.42f]);
        if (Covered(h.X, h.Y, 0.5f * HU(h.Y), 0.9f * HU(h.Y))) face = 3;                // sitting half behind a prop: travel to a clear spot first
        switch (face)
        {
            case 1: if (twig >= 0) StartNibbleTrip(h, twig); else StartNibbleNow(h, 5f + 4f * (float)Rng.NextDouble()); break;
            case 2: StartGroom(h); break;
            case 3: StartHop(h, i); break;
            case 4: BeginHare(h, HD.Tall, 5f + 3f * (float)Rng.NextDouble()); break;
            case 5: BeginHare(h, HD.Binky, 1.1f); break;
            case 6: if (!StartHide(h, i)) BeginSit(h, SitFor()); break;                  // rest out of sight behind a cover (a run round its end, a loaf, a peek now and then)
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

    /// <summary>The settings every flight starts from: the bound counter on its rest page, the pace braked and quick to lean in.</summary>
    private void SetBoltRun(H h)
    {
        float unit = HU(h.Y);
        h.GY = h.Y; h.JinkT = 0.2f; h.Odo = FoxAndHaresArt.HopRest; h.LastFrac = FoxAndHaresArt.HopRest;
        h.Pace.Halt(); h.Pace.Accel = 30f * unit;
        h.Turning = false;
    }

    /// <summary>
    /// Starts a bolt: after sitting tall for "alertFor" seconds (a hare that heard a thump, at least 0.4 s), AWAY from the cause, to COVER. It picks
    /// the nearest free hide spot on the far side from the danger (never across it: the fox must not be nearer the cover than the hare is) that a
    /// clean run reaches (PlanEnter: round the cover's near end, in behind it); none in reach, it runs off the screen's side and comes back the same
    /// way later (PlanOff); and if even that is blocked it stands tall and freezes.
    /// </summary>
    private void GoBolt(H h, int i, float alertFor)
    {
        float U = S.U, unit = HU(h.Y);
        int away = h.FearX >= h.X ? -1 : 1;
        // a fox that is close is judged by where it IS now, not where the notice said it was (it may have trotted past)
        if (_fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - h.X) < 0.35f * U && MathF.Abs(_fox.X - h.X) > 0.4f * unit) away = _fox.X >= h.X ? -1 : 1;
        h.Resp = Resp.None; h.ToHide = false; h.RouteOn = false;
        int pick = -1; float bestD = float.MaxValue;
        for (int k = 0; k < _spots.Count; k++)
        {
            Spot sp = _spots[k];
            if (sp.Who >= 0 && sp.Who != i) continue;
            float run = (sp.X - h.X) * away;
            if (run < 1.6f * unit || run > 0.8f * U) continue;
            if (_fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(sp.X - _fox.X) < MathF.Abs(h.X - _fox.X) + 0.05f * U && (sp.X - _fox.X) * (h.X - _fox.X) > 0f) continue;
            if (!PlanEnter(h, i, k, away, true, false)) continue;
            if (run < bestD) { bestD = run; pick = k; }
        }
        SetBoltRun(h);
        if (pick >= 0)
        {
            PlanEnter(h, i, pick, away, true, true);
            Log($"hare{i} flees away from {h.FearX:F0}: cover {_spots[pick].Cov} at {_spots[pick].X:F0},{_spots[pick].Y:F0}, {h.Hops} bounds of {h.HopK:F2}");
        }
        else if (PlanOff(h, i, away, true, true, false) || (_fox == null || _fox.Mind.Doing == FD.Away || MathF.Abs(_fox.X - h.X) > 0.5f * U) && PlanOff(h, i, -away, true, true, false)) Log($"hare{i} flees away from {h.FearX:F0}: no cover in reach, it bolts off the {(h.RDir > 0 ? "right" : "left")} side");
        else { Log($"hare{i} flees away from {h.FearX:F0}: nowhere to run, it freezes"); BeginHare(h, HD.Tall, 5f); return; }
        if (alertFor > 0.01f) { BeginHare(h, HD.Tall, MathF.Max(0.5f, alertFor) + 0.35f); h.BoltPending = true; h.BoltDir = h.RDir; }
        else StartBoltNow(h, h.RDir, i);
    }

    private void StartBoltNow(H h, int away, int i)
    {
        h.BoltPending = false; h.GoFlee = true; h.Resp = Resp.None;
        if (away != h.Dir) StartTurn(h, away, HD.Go, 0.5f, 0.24f);
        else BeginHare(h, HD.Go, 0.24f);
    }

    /// <summary>A planned run is over: the hare is at the last point of its route (the snap is the last bound's few pixels of error). Off the screen: it waits there. At cover: it lands (the settle pages) and hides. Otherwise it lands in the open.</summary>
    private void ArriveRun(H h, int i)
    {
        h.Pace.Halt();
        if (h.RouteOn)
        {
            h.X = h.RX[h.RN - 1]; h.Y = h.RY[h.RN - 1]; h.RouteOn = false;
            if (h.RouteOff)
            {
                h.RouteOff = false; h.Off = true; h.OffFor = 14f + 16f * (float)Rng.NextDouble(); h.OffSide = h.RDir; h.GoFlee = false; h.Vis = false;
                if (h.Spot >= 0) { _spots[h.Spot].Who = -1; h.Spot = -1; }
                BeginHare(h, HD.Hidden, Mind<HD>.Forever);
                Log($"hare{i} is off the screen's {(h.OffSide > 0 ? "right" : "left")} side for {h.OffFor:F0} s");
                return;
            }
            if (h.Spot >= 0) { _spots[h.Spot].Who = -1; h.Spot = -1; }    // out and clear of the cover it was behind: its spot is free again
        }
        BeginHare(h, HD.Land, 0.24f);
    }

    /// <summary>A run to cover that cannot go on (a body in the way): the hare stops where it is, on a rest page, and the hide is given up.</summary>
    private void AbortRoute(H h)
    {
        h.RouteOn = false; h.RouteOff = false;
        if (h.ToHide && h.NextSpot >= 0) { _spots[h.NextSpot].Who = -1; h.NextSpot = -1; }
        h.ToHide = false;
    }

    /// <summary>The hare that waited off the screen's side walks back in from it: facing in, to a goal eight or so hops along its own row.</summary>
    private void ReturnFromOff(H h, int i)
    {
        float W = S.Width, unit = HU(h.Y); int side = h.OffSide >= 0 ? 1 : -1;
        h.Off = false; h.Vis = true; h.Dir = -side;
        h.X = side > 0 ? W + 2.5f * unit : -2.5f * unit;
        float cyc = FoxAndHaresArt.HareHopD * unit;
        for (int tries = 0; tries < 16; tries++)
        {
            float gx = side > 0 ? W * (0.93f - 0.12f * (float)Rng.NextDouble()) : W * (0.07f + 0.12f * (float)Rng.NextDouble());
            float gy = Math.Clamp(h.Y + ((float)Rng.NextDouble() - 0.5f) * 0.08f * S.U, S.SmallBandTop + 0.006f * S.U, S.FieldPoint(gx, 0.62f).Y);
            if (!HRest(gx, gy, 0.3f * unit) || S.Crowded(gx, gy, 0.7f * unit, this, i)) continue;
            h.Y = gy;
            if (PlanHop(h, gx, gy, -side, 0.88f, 1.12f, 0.15f)) { Log($"hare{i} walks back in from the {(side > 0 ? "right" : "left")} side"); return; }
        }
        // nowhere clean to walk to: it appears at the edge's inner foot and sits (never mid-screen out of nothing: it is a body length inside the edge)
        PlaceNear(side > 0 ? 0.92f * W : 0.08f * W, 0.1f * W, out float ox, out float oy, 0.3f * unit); h.X = ox; h.Y = oy;
        BeginSit(h, 3f);
    }

    /// <summary>Hide spot sp's cover has the fox near (within 0.3 U in x, on the screen, awake): no peeking, no leaving.</summary>
    private bool FoxClose(Spot sp) => _fox != null && _fox.Mind.Doing is not (FD.Away or FD.Asleep) && MathF.Abs(_fox.X - sp.X) < 0.30f * S.U && MathF.Abs(_fox.Y - sp.Y) < 0.25f * S.U;

    /// <summary>The fox is AT the cover: at an end of it or behind it (within one and a half metres of the end, on the cover's rows). The hare breaks out of the other end.</summary>
    private bool FoxAtCover(Spot sp)
    {
        if (_fox == null || _fox.Mind.Doing is FD.Away or FD.Asleep) return false;
        Cover c = S.Covers[sp.Cov]; float fu = FU(_fox.Y);
        // only when it comes ROUND (it has sniffed at the end and begun to go round it) or is right at the cover: a fox that merely stands at the near end is sniffing
        bool round = _fox.RoundNext && _fox.NoseSpot >= 0 && _spots[_fox.NoseSpot].Cov == sp.Cov;
        return (round || MathF.Abs(_fox.X - c.Foot.X) < 0.7f * c.HalfW) && MathF.Abs(_fox.Y - sp.Y) < 1.2f * fu;
    }

    /// <summary>
    /// THE FOX HAS COME ROUND TO THE COVER: the hare goes out of the end FARTHEST from the fox, quickly, to the next cover (round the near end of it,
    /// in behind it) or, with none in reach, off the screen's side. It first comes up to the sit pose by the quickest EASED rise (a flight from
    /// danger starts from whatever pose is on screen through a short recover, never a cut), turns if it must, and runs.
    /// </summary>
    private void BreakOut(H h, int i)
    {
        Spot sp = _spots[h.Spot];
        int away = sp.X >= _fox!.X ? 1 : -1;
        h.FearX = _fox.X; h.FearY = _fox.Y; h.FearFox = true;
        int pick = -1; float bestD = float.MaxValue; float unit = HU(h.Y);
        for (int k = 0; k < _spots.Count; k++)
        {
            Spot s2 = _spots[k];
            if (k == h.Spot || (s2.Who >= 0 && s2.Who != i)) continue;
            float run = (s2.X - h.X) * away;
            if (run < 2.2f * unit || run > 0.8f * S.U) continue;
            if (MathF.Abs(s2.X - _fox.X) < MathF.Abs(h.X - _fox.X) + 0.05f * S.U && (s2.X - _fox.X) * (h.X - _fox.X) > 0f) continue;
            if (!PlanEnter(h, i, k, away, true, false, true)) continue;
            if (run < bestD) { bestD = run; pick = k; }
        }
        if (pick >= 0)
        {
            PlanEnter(h, i, pick, away, true, true, true);
            Log($"hare{i} breaks out of the cover (the fox is at its end): away {(away > 0 ? "right" : "left")}, to cover {_spots[pick].Cov} at {_spots[pick].X:F0},{_spots[pick].Y:F0}");
        }
        else if (PlanOff(h, i, away, true, true, true)) Log($"hare{i} breaks out of the cover: no cover in reach, it bolts off the screen's side");
        else return;                       // boxed in: it stays down
        h.Breaking = true; h.Leaving = true;
    }

    /// <summary>
    /// The hare is on the SIT pose behind its cover and means to go: start the planned run (a break-out has planned its own; a calm leave plans the
    /// way out round the end it faces, or the other end after a turn). Turns first if the route faces the other way, then the settle pages backwards
    /// (the sit rising into the gather), then the hops or the bolt.
    /// </summary>
    private void StartPlanned(H h, int i)
    {
        Spot sp = _spots[Math.Max(0, h.Spot)];
        if (!h.RouteOn)
        {
            int dir = h.Dir;
            _lax = h.ExitFails >= 3;
            if (!PlanExit(h, i, sp, dir, h.Breaking, true) && !PlanExit(h, i, sp, -dir, h.Breaking, true) && !PlanOff(h, i, dir, h.Breaking, true, true) && !PlanOff(h, i, -dir, h.Breaking, true, true))
            {
                // both ends blocked (a hare or the fox stands there): it stays down a while longer
                h.ExitFails++; _lax = false;
                h.Leaving = false; h.Breaking = false; h.HideFor = h.HideT + 6f; h.HT = 0f; h.Clock = 0f; h.NextPeek = _time + 8.0;
                h.HStage = sp.Low && h.HStage != 1 ? 4 : 1;            // a low cover: lower from the sit pose back to the loaf (eased), never a cut; a bush: it is already on the sit pose
                return;
            }
        }
        h.Leaving = false;
        bool flee = h.Breaking; h.Breaking = false;
        h.GoFlee = flee; h.ExitFails = 0; _lax = false;
        if (flee) { SetBoltRun(h); h.Resp = Resp.None; }
        else { h.Pace.Halt(); }
        if (h.RDir != h.Dir) StartTurn(h, h.RDir, HD.Go, flee ? 0.5f : 1f, 0.24f);
        else BeginHare(h, HD.Go, 0.24f);
        Log($"hare{i} leaves its cover {(flee ? "at a bolt" : "calmly")}: {(h.RDir > 0 ? "right" : "left")} end, {h.Hops} bounds of {h.HopK:F2}");
    }

    /// <summary>
    /// A CALM HIDE: the hare means to rest out of sight behind the nearest free cover that a clean run reaches (a lollop, not a bolt): round the near
    /// end, in behind it. False if none does (it then simply sits on).
    /// </summary>
    private bool StartHide(H h, int i)
    {
        float U = S.U, unit = HU(h.Y);
        int pick = -1, pdir = 1; float bestD = float.MaxValue;
        for (int k = 0; k < _spots.Count; k++)
        {
            Spot sp = _spots[k];
            if (sp.Who >= 0) continue;
            float dx = sp.X - h.X; int dir = dx >= 0 ? 1 : -1; float run = MathF.Abs(dx);
            if (run < 2.2f * unit || run > 0.62f * U) continue;
            if (MathF.Abs(sp.Y - h.Y) > 0.30f * run) continue;
            if (!PlanEnter(h, i, k, dir, false, false)) continue;
            float cost = run + (dir == h.Dir ? 0f : 0.2f * unit);
            if (cost < bestD) { bestD = cost; pick = k; pdir = dir; }
        }
        if (pick < 0) return false;
        PlanEnter(h, i, pick, pdir, false, true);
        h.GoFlee = false; h.Next = HD.Sit;
        if (pdir != h.Dir) StartTurn(h, pdir, HD.Go, 1f, 0.24f); else BeginHare(h, HD.Go, 0.24f);
        Log($"hare{i} goes to hide behind cover {_spots[pick].Cov} at {_spots[pick].X:F0},{_spots[pick].Y:F0}: {h.Hops} hops of {h.HopK:F2}");
        return true;
    }

    private int CountOwn(int me) { int n = 0; foreach (Spot s in _spots) if (s.Who == me) n++; return n; }

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
            if (_h[j].Mind.Doing is HD.Hidden or HD.Bolt || _h[j].Off || _h[j].ToHide || BoltRun(_h[j])) continue;        // a hare behind cover or running for it is not in sight to be stalked
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
        f.Perf = d == FD.Mouse && f.Stage >= 3 && f.Stage <= 5 && f.CraI >= 0;
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
                if (_fb != null && f.Pg % 6 == 0 && f.Pg != f.LastPrint) { f.LastPrint = f.Pg; Track(f.X + f.Dir * 0.25f * unit, f.Y, 2); }
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
                if (f.Clock >= 0.22f)
                {
                    if (f.NoseNext && f.NoseSpot >= 0 && f.Dir == f.NoseDir && MathF.Abs(f.HoleX - (f.X + f.Dir * 0.46f * unit)) < 0.5f * unit) { f.NoseNext = false; BeginFox(f, FD.Nose, Mind<FD>.Forever); f.Stage = 0; f.SnowDone = false; }
                    else if (f.NoseNext) { f.NoseNext = false; f.Loose = false; Log("fox could not get to the cover: it gives up"); BeginFox(f, FD.Stand, 3.5f + 2f * (float)Rng.NextDouble()); }
                    else if (f.RoundNext) { f.RoundNext = false; f.Rounds++; EndRound(f); }
                    else BeginFox(f, FD.Stand, 4f + 5f * (float)Rng.NextDouble());
                }
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
                    // the hare is behind a cover; the fox stands where it pulled up, watching, until it IS behind it (the tail has gone round), then
                    // walks up to the cover's near end on a planned stride and noses round it
                    f.NoseT += dt;
                    H? tg = f.Target >= 0 ? _h[f.Target] : null;
                    bool under = tg != null && tg.Mind.Doing == HD.Hidden && tg.Spot >= 0 && !tg.Off;
                    if (under && f.NoseT > 0.3f)
                    {
                        Spot sp = _spots[tg!.Spot];
                        int dirF = sp.X >= f.X ? 1 : -1;
                        if (dirF != f.Dir) { StartFoxTurn(f, dirF, FD.Stand); break; }          // face the cover first (a drawn turn)
                        float ne = dirF > 0 ? sp.EdgeL : sp.EdgeR;                              // the near end, where the fox noses
                        f.NoseSpot = tg.Spot; f.NoseDir = dirF; f.Loose = true;
                        f.HoleX = ne + dirF * 0.12f * unit; f.HoleY = sp.Y;
                        float dist = (f.HoleX - f.X) * f.Dir - 0.46f * unit;                    // the muzzle (0.46 m ahead of the fox's feet when it noses) ends a little way over the cover's edge
                        if (dist < 0.25f * unit && MathF.Abs(f.HoleY - f.Y) < 0.12f * unit) { f.NoseNext = false; BeginFox(f, FD.Nose, Mind<FD>.Forever); f.Stage = 0; f.SnowDone = false; }
                        else if (dist < 0.25f * unit) { f.NoseNext = false; f.Loose = false; Log("fox is at the cover's end but on the wrong row: it gives up"); BeginFox(f, FD.Stand, 3f); }
                        else
                        {
                            f.GX = f.X + f.Dir * dist; f.GY = f.HoleY; f.GoLeave = false; f.RestIdx = Rng.Next(2); f.Pace.Halt();
                            // THE APPROACH IS PLANNED: a whole number of half strides, each a touch stretched, ends on a rest page exactly at the spot, so the muzzle lands at the cover's end
                            float dm = dist / unit, half = 0.5f * FoxAndHaresArt.FoxTrotStride; int kk = (int)MathF.Round(dm / half);
                            f.StrideK = 1f; f.Plan = 0;
                            if (kk >= 2) { float kf = dm / (kk * half); if (kf >= 0.8f && kf <= 1.25f) { f.StrideK = kf; f.Plan = kk; } }
                            Log($"fox plans its walk to the cover's end: {kk} half strides of {f.StrideK:F2}, plan {f.Plan}, dist {dist:F0} px"); BeginFox(f, FD.Go, 0.22f);
                        }
                    }
                    else if (f.NoseT > 6f) { f.NoseNext = false; f.Loose = false; }
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
                bool have = f.Target >= 0 && _h[f.Target].Mind.Doing is not HD.Hidden && !_h[f.Target].Off && !Covered(f.X, f.Y, 0.55f * unit, 0.6f * unit);     // a stalk never goes on half behind a prop
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
                    bool lost = t == null || t.Mind.Doing is HD.Hidden || t.Off;
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
                bool hidden = t != null && t.Mind.Doing == HD.Hidden && t.Spot >= 0 && !t.Off;      // behind its cover: out of sight
                bool bound = t != null && t.ToHide && t.NextSpot >= 0;                                             // running for a cover
                bool hareGone = t == null || t.Off || hidden;
                float tx, ty;
                if (t != null && !hareGone) { tx = t.X; ty = t.Y; if (bound) { f.HoleX = t.BX; f.HoleY = t.BY; } }
                else { tx = f.HoleX; ty = f.HoleY; }
                bool hasHole = bound || hidden;
                f.Loose = hasHole || f.NoseNext || f.RoundNext;
                if (hidden) { f.HoleX = _spots[t!.Spot].X; f.HoleY = _spots[t.Spot].Y; }
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
                    if (t != null && !hareGone && (BoltRun(t) || t.ToHide)) easeF = Math.Clamp((gapM - 1.6f) / 0.9f, 0f, 1f);
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
                if (!f.SkidGo && ((hasHole && toStop < 1.5f * unit && (atGather || toStop < 0.7f * unit)) || f.Mind.Over || (!Valid(f.X + f.Dir * 0.3f * unit, f.Y, 0.3f * unit, f.Loose) && f.Clock > 0.5f)))
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
                // SNIFFING ROUND THE COVER (about 5 s): the head reaches out to the cover's end (0.35 s), three sniff cycles of the muzzle at the twigs
                // or over the stones (snow shaken off a bush falls on its head in the second), the head comes back (0.35 s). Then it goes ROUND the end.
                // Every stage is a whole number of cycles, so each joins the next on its first page.
                float c = f.Clock;
                const float NC = 24f / 18f;                                   // one sniff cycle: 24 pages at 18 a second
                float tEnd = 0.35f + 3f * NC;
                if (c < 0.35f) ShowF(f, FB_NoseIn, Math.Min(9, (int)(c / 0.35f * 10f)), mir);
                else if (c < tEnd) ShowF(f, FB_Nose, Cyc(c - 0.35f, 18f, 24, 0f), mir);
                else ShowF(f, FB_NoseIn, Math.Max(0, 9 - (int)((c - tEnd) / 0.35f * 10f)), mir);
                // snow shaken off the bush falls on its head (seven clumps over half a second, from the bush's top down to the head), then a small puff where they land
                if (f.NoseSpot >= 0 && S.Covers[_spots[f.NoseSpot].Cov].Kind == CoverKind.Shrub && c > 1.9f && c < 2.45f && ((int)(c * 14f)) != ((int)((c - dt) * 14f)))
                    Emit(0, f.X + f.Dir * (0.34f + 0.12f * (float)Rng.NextDouble()) * unit, f.Y, 0.78f * unit, f.Dir * 0.05f * unit, -0.1f * unit, 0.32f, 0, 0.95f, 2.4f * unit);
                if (!f.SnowDone && c > 2.25f && f.NoseSpot >= 0 && S.Covers[_spots[f.NoseSpot].Cov].Kind == CoverKind.Shrub)
                {
                    f.SnowDone = true;
                    Burst(f.X + f.Dir * 0.40f * unit, f.Y, unit, 5, 0.30f, 0.30f, 0);
                }
                if (c >= tEnd + 0.35f) { f.Reverse = false; Log("fox is done sniffing at the cover: it goes round the end"); StartRound(f); }
                break;
            }
        }
        LogFox(f, dt);
    }

    /// <summary>
    /// AFTER THE SNIFF: the fox goes ROUND the cover. It trots on along the row it noses on, past the cover's far end, so it passes right behind the
    /// cover (hidden by it a part at a time, head first) and comes out the other side; the hare, behind it, breaks out of the end farthest from the
    /// fox when the fox comes round (FoxAtCover). If the way is not open it sits.
    /// </summary>
    private void StartRound(F f)
    {
        float unit = FU(f.Y);
        Spot sp = _spots[Math.Max(0, f.NoseSpot)];
        float far = f.NoseDir > 0 ? sp.EdgeR : sp.EdgeL;
        float gx = far + f.NoseDir * 1.5f * unit, gy = sp.Y;
        if (gx < 0.04f * S.Width || gx > 0.96f * S.Width || !Valid(gx, gy, 0.4f * unit, true) || S.Crowded(gx, gy, 0.7f * unit, this, 100)) { f.Loose = false; f.Rounds++; BeginFox(f, FD.SitIn, 0.6f); f.Reverse = false; return; }
        f.GX = gx; f.GY = gy; f.GoLeave = false; f.RestIdx = Rng.Next(2); f.Pace.Halt(); f.RoundNext = true; f.Loose = true;
        float dist = (gx - f.X) * f.Dir, dm = dist / unit, half = 0.5f * FoxAndHaresArt.FoxTrotStride; int kk = (int)MathF.Round(dm / half);
        f.StrideK = 1f; f.Plan = 0;
        if (kk >= 2) { float kf = dm / (kk * half); if (kf >= 0.8f && kf <= 1.25f) { f.StrideK = kf; f.Plan = kk; } }
        BeginFox(f, FD.Go, 0.22f);
    }

    /// <summary>The fox has come round the cover. One round or two, then it gives up the hunt: a sit, or a stand (the stalk's cooldown keeps it from starting again at once).</summary>
    private void EndRound(F f)
    {
        f.Loose = false; f.NoseSpot = -1;
        bool again = f.Rounds < 2 && Rng.NextDouble() < 0.5 && !Lull && f.Target >= 0 && _h[f.Target].Mind.Doing == HD.Hidden && _h[f.Target].Spot >= 0 && !_h[f.Target].Off;
        if (again) { f.NoseNext = true; f.NoseT = 0f; f.Loose = true; BeginFox(f, FD.Stand, 3f); Log("fox goes round a second cover"); return; }
        f.Rounds = 0;
        if (Rng.NextDouble() < 0.5) { f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); Log("fox gives up the hunt and sits"); }
        else BeginFox(f, FD.Stand, 3.5f + 2.5f * (float)Rng.NextDouble());
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
            float lowRow = f.Loose ? 0f : S.SmallBandTop + 1f;
            // is the way to row yt (reached over a lane, then held) clear at 1, 1.8, 2.6 and 3.4 m ahead?
            bool BadRow(float yt)
            {
                for (int k = 1; k <= 4; k++)
                {
                    float d = (0.2f + 0.8f * k) * unit, px = f.X + f.Dir * d, py = f.Y + (yt - f.Y) * MathF.Min(1f, d / lane);
                    if ((!edge && !Valid(px, py, 0.3f * unit, f.Loose)) || py < lowRow || S.Crowded(px, py, half, this, 100)) return true;
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
        FD.StalkIn or FD.Stalk or FD.StalkOut or FD.DashIn or FD.Chase or FD.Skid or FD.Nose or FD.Watch => "Hunt",
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
            case "hide":
            case "flee":
            case "offscreen":
            {
                // hare 0 is put in the open 0.30 U from the cover of its own spot (facing it); for "stalk" the fox is on its far side
                // (the hare bolts for the cover, the fox chases, noses round it, and the hare breaks out of the other end); "hide" sends it behind the cover calmly
                // at 6 s; "flee" bolts it there at once; "offscreen" takes every cover away first, so it runs off the side of the screen.
                int sIdx = Math.Max(0, h0.Spot >= 0 ? h0.Spot : 0); Spot s0 = _spots[sIdx];
                int sd = s0.X + 0.36f * S.U < 0.93f * W ? 1 : -1;
                if (_test == "stalk" && Environment.GetEnvironmentVariable("PECKWORKS_STALKSIDE") is string ss) sd = ss == "L" ? -1 : 1;
                if (h0.Spot >= 0) { _spots[h0.Spot].Who = -1; h0.Spot = -1; }
                float hy = MathF.Max(s0.Y, S.SmallBandTop + 0.012f * S.U);
                h0.X = s0.X + sd * 0.30f * S.U; h0.Y = hy; h0.Dir = -sd; h0.Vis = true; h0.Off = false; h0.Timid = 0.6f; h0.Att.Timid = 0.7f;
                BeginHare(h0, HD.Sit, 60f);
                for (int i = 1; i < _n; i++) { PointF q = S.FieldPoint((0.30f + 0.03f * i) * W, 0.22f); _h[i].X = q.X; _h[i].Y = q.Y; if (_h[i].Spot >= 0) { _spots[_h[i].Spot].Who = -1; _h[i].Spot = -1; } _h[i].Vis = true; BeginHare(_h[i], HD.Sit, 60f); }
                if (_test == "offscreen") foreach (Spot sp2 in _spots) sp2.Who = 99;
                if (_test == "hide") { if (!StartHide(h0, 0)) Log("test hide: no clean run to a cover"); }
                else if (_test is "flee" or "offscreen") { h0.FearX = h0.X + h0.Dir * -0.4f * S.U; h0.FearY = h0.Y; h0.FearFox = true; GoBolt(h0, 0, 0f); }
                else { f.X = h0.X + sd * 0.36f * S.U; f.Y = hy; f.Dir = -sd; f.Mind.Begin(FD.Stand, 3f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f; f.Target = 0; StartStalk(f, h0); }
                break;
            }
            case "groom": Calm(); h0.Mind.Begin(HD.Sit, 60f); BeginHare(h0, HD.Groom, 5.5f); break;
            case "binky": Calm(); BeginHare(h0, HD.Binky, 1.1f); break;
            case "nibble": { Calm(); int tw = NearestTwig(h0); if (tw >= 0) StartNibbleTrip(h0, tw); break; }
            case "tall": Calm(); BeginHare(h0, HD.Tall, 4f); break;
            case "thump": Calm(); h0.FearX = h0.X + (h0.Dir * 0.4f * S.U); h0.FearY = h0.Y; h0.FearFox = true; StartThump(h0, 0); break;
            case "break":
            {
                // the fox has sniffed at the near (left) end of hare 0's cover and is going round it: the hare breaks out of the far end, off the screen's side, and comes back
                Calm(); Spot sb = _spots[h0.Spot];
                f.X = sb.EdgeL - 1.2f * funit; f.Y = sb.Y; f.Dir = 1; f.Mind.Begin(FD.Stand, 99f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f;
                f.NoseSpot = h0.Spot; f.NoseDir = 1; f.RoundNext = true; f.Target = 0;
                break;
            }
            case "peek": Calm(); h0.NextPeek = _time; break;                                // hare 0 has been behind its cover since the start (HideFor is huge): it peeks now
            case "out": Calm(); h0.HideFor = 0f; h0.Leaving = false; break;                // ...and now it comes out: a peek, then round the end it faces
            case "nose":
            {
                // the fox stands 3.2 m from the near end of hare 0's cover, facing it; the hare is behind it. The fox walks up, noses round the end, goes round; the hare breaks out the other end.
                Calm(); Spot sn = _spots[h0.Spot]; int sd = sn.X < W * 0.5f ? -1 : 1;
                if (!Valid(sn.X + sd * (MathF.Abs(sn.X - (sd < 0 ? sn.EdgeL : sn.EdgeR)) + 3.2f * funit), sn.Y, 0.3f * funit, true)) sd = -sd;
                float ne = sd < 0 ? sn.EdgeL : sn.EdgeR;
                f.X = ne + sd * 3.2f * funit; f.Y = MathF.Max(sn.Y, S.HerdBandTop); f.Dir = -sd; f.Mind.Begin(FD.Stand, 99f); f.Clock = 0; f.OnScreen = 0; _nextAway = 9999f;
                f.Target = 0; f.NoseNext = true; f.NoseT = 0f; f.Loose = true;
                break;
            }
            case "look": FoxAt(0.78f, 0.30f, 1); Board.Post(Sense.Movement, new PointF(f.X, S.Height * 0.1f), 0.8f * S.U, 1f, this); break;
        }
    }

    private void StartMouseNow(F f) { BeginFox(f, FD.Mouse, Mind<FD>.Forever); f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * FU(f.Y); f.Sneezes = true; f.ListenFor = 2.39f; }

    // ================================================================ DRAW

    public override void Collect(DepthQueue q)
    {
        if (_n == 0 || _hb == null) return;
        for (int i = 0; i < _n; i++) if (_h[i].Vis && !_h[i].Off && _h[i].Bk != null) q.Add(_h[i].Y, this, i);
        if (_fox != null && _fox.Mind.Doing != FD.Away && _fox.Bk != null) q.Add(_fox.Y, this, 100);
        if (_decalLive > 0) q.Add(-1f, this, 200);
        for (int k = 0; k < _twigs.Length; k++) q.Add(_twigs[k].Y - 1f, this, 300 + k);
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

    private void StampCra(FrameBuffer fb, Crater c, int open, bool front, float op)
    {
        Book bk = _fb![front ? FB_DentF : FB_DentB]; int t = TierOf(c.Y);
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
            Stamp(fb, h.Bk.Pages[t][h.Pg], h.X + gxs, h.Y, h.Bk.Ox[t], h.Bk.Oy[t] - sy, h.Mir);          // x goes in as a float (the stamp is sub-pixel), y through the origin so the depth test keeps the true feet
        }
        else if (index == 100)
        {
            F f = _fox!; if (f.Bk == null) return;
            int t = TierOf(f.Y);
            float un = _fUnit[t];
            float gxs = (f.Mir ? -f.Gx : f.Gx) * un; int sy = (int)MathF.Round(-f.Gy * un);
            bool cra = f.Perf && f.Mind.Doing == FD.Mouse;
            if (cra) StampCra(fb, _cra[f.CraI], f.CraOpen, false, 1f);              // the dent's soft back piece, then the fox, then the low white mound in front of its head
            Stamp(fb, f.Bk.Pages[t][f.Pg], f.X + gxs, f.Y, f.Bk.Ox[t], f.Bk.Oy[t] - sy, f.Mir);
            if (cra) StampCra(fb, _cra[f.CraI], f.CraOpen, true, 1f);
        }
        else if (index == 200)
        {
            // the tracks and burrows lying on the snow, fading with age
            for (int i = 0; i < _decalLive; i++)
            {
                Decal d = _decals[i];
                double age = _time - d.Born; float life = 16f;
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
