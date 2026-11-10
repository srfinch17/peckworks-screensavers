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
///   up at 6 s). PECKWORKS_FOXHARES=off makes the count 0 (the "without me" timing). PECKWORKS_SHEET=fox;file.png or
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
    private enum HD { Sit, Look, Nibble, Groom, Go, Hop, Land, Tall, Thump, Binky, Bolt, Dive, Under, Peek, Turn }
    // Fox doings. Mouse is one doing with stages (listen, coil, leap, dive in, plunge, back out, shake, wipe, sneeze):
    // nothing cuts it once the leap has begun; a Loud during the listening stage ends it (the fox looks up).
    private enum FD { Away, Go, Trot, Settle, Stand, Look, Turn, SitIn, Sit, SitOut, CurlIn, Asleep, Wake, Mouse, StalkIn, Stalk, StalkOut, DashIn, Chase, Skid, Nose, Dig, Leave }

    private const int HB_Sit = 0, HB_Look = 1, HB_Reach = 2, HB_Nibble = 3, HB_Groom = 4, HB_Hop = 5, HB_Flee = 6, HB_Settle = 7, HB_TallIn = 8, HB_Tall = 9, HB_Thump = 10, HB_Binky = 11, HB_Dive = 12, HB_Peek = 13, HB_Turn = 14, HB_Twig = 15;
    private const int FB_Stand = 0, FB_Look = 1, FB_Trot = 2, FB_Settle0 = 3, FB_Settle1 = 4, FB_Stalk = 5, FB_StalkIn = 6, FB_Gallop = 7, FB_Skid = 8, FB_Sit = 9, FB_SitIn = 10, FB_CurlIn = 11, FB_Sleep = 12, FB_Listen = 13, FB_Coil = 14, FB_Leap = 15, FB_DiveIn = 16, FB_Plunge = 17, FB_BackOut = 18, FB_Shake = 19, FB_Wipe = 20, FB_Sneeze = 21, FB_Dig = 22, FB_NoseIn = 23, FB_Nose = 24, FB_Turn = 25, FB_DashIn = 26;
    private const int Tiers = 4;
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
        public float ThumpCool, BreathT, JinkT, LookLevel, Dur; public int Thumps; public double HeardAt = -1;
        public float FearX, FearY; public bool FearFox, BoltPending; public int BoltDir; public float TurnQuick = 1f;
        public Pace Pace = new(40f); public int Twig = -1, PeekAt; public float PeekT, GlanceCool;
        public Book? Bk; public int Pg; public bool Mir; public bool Vis = true; public float LX, LY;
    }

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
    private readonly StreamWriter? _log = MindLog.Open();
    private readonly string _test = Environment.GetEnvironmentVariable("PECKWORKS_FOXHARE") ?? "natural";
    private double _time;
    private bool _forced;
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
        for (int i = 0; i <= 10; i++) { float x = W * (0.02f + 0.96f * i / 10f); _yLo = MathF.Min(_yLo, s.FieldPoint(x, 0.18f).Y); _yHi = MathF.Max(_yHi, s.FieldPoint(x, 0.60f).Y); }
        for (int t = 0; t < Tiers; t++)
        {
            _tierY[t] = _yLo + (_yHi - _yLo) * (t + 0.5f) / Tiers;
            _fUnit[t] = MathF.Max(3f, s.Metre(_tierY[t]));
            _hUnit[t] = MathF.Max(2.5f, s.Metre(_tierY[t]) * HareScale);
        }

        // ---- the hares: each with its own dials, a home on the right half or the left strip, and two twigs ----
        _h = new H[_n];
        float[] timid = Traits.Deal(Rng, Math.Max(1, _n)), curious = Traits.Deal(Rng, Math.Max(1, _n)), play = Traits.Deal(Rng, Math.Max(1, _n));
        float[] homes = [0.66f, 0.82f, 0.10f, 0.92f, 0.60f, 0.15f, 0.74f, 0.96f];
        var twigs = new List<(float, float)>();
        for (int i = 0; i < _n; i++)
        {
            var h = new H { Timid = timid[i], Curious = curious[i], Play = play[i], IdleOff = (float)Rng.NextDouble() * 24f, Dir = Rng.Next(2) == 0 ? 1 : -1 };
            h.Att = new Attention(0.25f + 0.6f * h.Timid, 0.2f + 0.7f * h.Curious) { Keen = 1.4f };
            PlaceNear(homes[i] * W, 0.5f * W, out h.X, out h.Y, 0.35f * HareScale * 0.1f * s.U);
            h.Mind.Begin(HD.Sit, 1f + 4f * (float)Rng.NextDouble());
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
        _twigs = [.. twigs];
        if (_n > 0)
        {
            _fox = new F { Att = new Attention(0.30f, 0.55f) { Keen = 1.1f } };
            _fox.X = -3f * _fUnit[0]; _fox.Y = s.FieldPoint(0.3f * W, 0.3f).Y;
            _nextAway = 8f + 12f * (float)Rng.NextDouble();
        }
        Log($"sizes: hare unit {_hUnit[0]:F1} to {_hUnit[Tiers - 1]:F1} px, fox metre {_fUnit[0]:F1} to {_fUnit[Tiers - 1]:F1} px, lanes y {_yLo:F0} to {_yHi:F0}");

        BuildInBackground(() =>
        {
            if (_n == 0) return;
            var hb = FoxAndHaresArt.HareBooks(); var fb = FoxAndHaresArt.FoxBooks();
            var hbooks = new Book[hb.Length]; var fbooks = new Book[fb.Length];
            for (int i = 0; i < hb.Length; i++) hbooks[i] = new Book(hb[i], _hUnit);
            for (int i = 0; i < fb.Length; i++) fbooks[i] = new Book(fb[i], _fUnit);
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
        });
    }

    // ================================================================ places and sizes

    private int TierOf(float y) => Math.Clamp((int)MathF.Floor((y - _yLo) / MathF.Max(1f, _yHi - _yLo) * Tiers), 0, Tiers - 1);
    private float HU(float y) => _hUnit[TierOf(y)];
    private float FU(float y) => _fUnit[TierOf(y)];

    /// <summary>May feet stand at (x, y)? On the snowfield between depth 0.18 and 0.60, the ice and every prop's foot excluded (ChristmasScenery.CanStand).</summary>
    private bool Valid(float x, float y, float room)
    {
        float W = S.Width;
        if (x < 0.02f * W || x > 0.98f * W) return false;
        float wy = S.WalkY(x), d = (y - wy) / (S.Height - wy);
        if (d < 0.18f || d > 0.60f) return false;
        // never rest BEHIND the bird table or the tiny fir (the props would hide most of the animal: it would read as missing)
        if (MathF.Abs(x - S.FeederFoot.X) < 0.075f * S.U && y <= S.FeederFoot.Y + 3f && y > S.FeederTray.At.Y) return false;
        if (MathF.Abs(x - S.SaplingFoot.X) < MathF.Max(0.04f * S.U, S.SaplingHeight * 0.45f) && y <= S.SaplingFoot.Y + 3f && y > S.SaplingFoot.Y - S.SaplingHeight) return false;
        return S.CanStand(x, y, room);
    }

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
            if (Valid(p.X, p.Y, room)) { ox = p.X; oy = p.Y; return; }
        }
        PointF f = S.FieldPoint(x, 0.22f); ox = f.X; oy = f.Y;
    }

    private void Log(string what) => _log?.WriteLine($"{_time,8:F2} foxhare {what}");

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
        _time += dt;
        if (!_forced && _time >= 6.0) { _forced = true; ForceTest(); }
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
    }

    // ---------------------------------------------------------------- hares

    private void Show(H h, int book, int page, bool mirror)
    {
        if (_hb == null) return;
        h.Bk = _hb[book]; h.Pg = Math.Clamp(page, 0, h.Bk.Count - 1); h.Mir = mirror;
    }
    private static int Cyc(double t, float rate, int n, float off) => (int)(((t * rate + off) % n + n) % n);

    private void StartTurn(H h, int to, HD after, float quick = 1f, float afterFor = 0f)
    {
        if (to == h.Dir) return;
        h.Turning = true; h.TurnT = 0; h.TurnFrom = h.Dir; h.TurnTo = to; h.After = after; h.AfterFor = afterFor; h.Pace.Halt();
        h.TurnQuick = quick;
    }

    private void BeginHare(H h, HD d, float dur)
    {
        h.Mind.Begin(d, dur); h.Clock = 0; h.Dur = dur;
        Log($"hare{Array.IndexOf(_h, h)} {d} {dur:F1}s");
    }

    private static bool HareCanReact(HD d) => d is HD.Sit or HD.Look or HD.Nibble or HD.Groom or HD.Go or HD.Hop or HD.Land or HD.Tall;

    private void StepHare(int i, float dt)
    {
        H h = _h[i];
        float U = S.U, W = S.Width;
        h.Mind.Tick(dt); h.Clock += dt; h.Att.Tick(dt); h.ThumpCool -= dt; h.BreathT -= dt; h.GlanceCool -= dt;
        float unit = HU(h.Y);
        bool mir = h.Dir < 0;
        h.Vis = h.Mind.Doing != HD.Under;
        HD d = h.Mind.Doing;

        if (h.Turning)
        {
            // A REVERSAL IS A TURN: the sat hare turns through four drawn views (side, three-quarter, almost head on, head on) and out the other side.
            float dur = h.TurnQuick < 1f ? QuickTurn : TurnFor;
            float was = h.TurnT; h.TurnT += dt;
            float f = Math.Clamp(h.TurnT / dur, 0f, 0.999f);
            int step = (int)(f * 7);                          // 0..6
            if (step < 4) Show(h, HB_Turn, step - 1 < 0 ? 0 : step - 1, h.TurnFrom < 0);
            else Show(h, HB_Turn, 6 - step, h.TurnTo < 0);
            if (step == 0) Show(h, HB_Sit, 0, h.TurnFrom < 0);
            if (step >= 3) h.Dir = h.TurnTo;
            if (h.TurnT >= dur) { h.Turning = false; h.Dir = h.TurnTo; if (h.After == HD.Sit) BeginHare(h, HD.Sit, 1f); else BeginHare(h, h.After, h.AfterFor); }
            LogHare(i, h, dt);
            return;
        }

        // ---- listening: another hare's thump, then the board ----
        if (HareCanReact(d))
        {
            foreach (Notice n in Board.Live)
            {
                if (n.Kind != Sense.Friend || n.Who is not H from || from == h || n.Posted <= h.HeardAt) continue;
                float dx = n.At.X - h.X, dy = n.At.Y - h.Y;
                if (MathF.Sqrt(dx * dx + dy * dy) > n.Reach) continue;
                h.HeardAt = n.Posted; h.FearX = from.FearX; h.FearY = from.FearY; h.FearFox = true;
                Log($"hare{i} hears thump from hare{Array.IndexOf(_h, from)}: sits tall, bolts");
                GoBolt(h, i, 0.25f + 0.5f * (float)Rng.NextDouble() * (1f - h.Timid));
                d = h.Mind.Doing; goto doing;
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
                    if (cause.Kind == Sense.Threat && h.ThumpCool <= 0f && dist > 0.10f * U && !Lull) StartThump(h, i);
                    else GoBolt(h, i, 0f);
                    d = h.Mind.Doing; goto doing;
                }
                if (r == Reaction.Freeze)
                {
                    int to = cause.At.X >= h.X ? 1 : -1;
                    if (!overhead && to != h.Dir) StartTurn(h, to, HD.Tall, 1f, 2f + 2f * (float)Rng.NextDouble());
                    else BeginHare(h, HD.Tall, 2f + 2f * (float)Rng.NextDouble());
                    d = h.Mind.Doing; goto doing;
                }
                if (r == Reaction.Look && d is HD.Sit or HD.Nibble or HD.Groom or HD.Look && (h.GlanceCool <= 0f || !overhead))
                {
                    if (overhead) h.GlanceCool = 14f + 14f * (1f - h.Curious);
                    h.LookLevel = overhead ? 1f : 0.5f;
                    BeginHare(h, HD.Look, 1.8f); d = HD.Look; goto doing;
                }
            }
        }
    doing:
        switch (d)
        {
            case HD.Sit:
            {
                Show(h, HB_Sit, Cyc(h.Clock, 10f, 24, h.IdleOff), mir);
                if (h.BreathT <= 0f && _hb != null) { PointF nz = _hb[HB_Sit].Aux[h.Pg]; Emit(1, h.X + h.Dir * nz.X * unit, h.Y, nz.Y * unit, h.Dir * 0.05f * unit, 0.06f * unit, 1.1f, 0, 0.45f); h.BreathT = 5f + 4f * (float)Rng.NextDouble(); }
                if (h.Mind.Over) RollHare(h, i);
                break;
            }
            case HD.Look:
            {
                float lv = Math.Min(1f, h.Clock / 0.3f) * (h.Clock > h.Dur - 0.3f ? Math.Max(0f, (h.Dur - h.Clock) / 0.3f) : 1f) * h.LookLevel;
                Show(h, HB_Look, (int)MathF.Round(lv * 3f), mir);
                if (h.Mind.Over) BeginHare(h, HD.Sit, 1f + 2f * (float)Rng.NextDouble());
                break;
            }
            case HD.Groom:
            {
                Show(h, HB_Groom, (int)(h.Clock / h.Dur * 23.99f), mir);
                if (h.Mind.Over) BeginHare(h, HD.Sit, 1f + 3f * (float)Rng.NextDouble());
                break;
            }
            case HD.Nibble:
            {
                // reach (0.4 s), bite a few seconds, unreach (0.4 s)
                if (h.Clock < 0.4f) Show(h, HB_Reach, Math.Min(4, (int)(h.Clock / 0.4f * 5f)), mir);
                else if (h.Clock < h.Dur - 0.4f) Show(h, HB_Nibble, Cyc(h.Clock, 14f, 12, h.IdleOff), mir);
                else Show(h, HB_Reach, Math.Max(0, 4 - (int)((h.Clock - (h.Dur - 0.4f)) / 0.4f * 5f)), mir);
                if (h.Mind.Over) BeginHare(h, HD.Sit, 1f + 2f * (float)Rng.NextDouble());
                break;
            }
            case HD.Tall:
            {
                if (h.Clock < 0.35f) Show(h, HB_TallIn, Math.Min(4, (int)(h.Clock / 0.35f * 5f)), mir);
                else if (h.Clock < h.Dur - 0.35f) Show(h, HB_Tall, Cyc(h.Clock, 8f, 12, h.IdleOff), mir);
                else Show(h, HB_TallIn, Math.Max(0, 4 - (int)((h.Clock - (h.Dur - 0.35f)) / 0.35f * 5f)), mir);
                if (h.Mind.Over) { if (h.BoltPending) StartBoltNow(h, h.BoltDir, i); else BeginHare(h, HD.Sit, 1f + 2f * (float)Rng.NextDouble()); }
                break;
            }
            case HD.Thump:
            {
                // tall (0.35 s), then the thump book over 1.2 s (the foot comes down at 0.4 s and at 0.9 s into it), then the bolt
                h.Pace.Halt();
                if (h.Clock < 0.35f) Show(h, HB_TallIn, Math.Min(4, (int)(h.Clock / 0.35f * 5f)), mir);
                else Show(h, HB_Thump, Math.Min(11, (int)((h.Clock - 0.35f) / 1.2f * 12f)), mir);
                int due = h.Clock >= 0.35f + 0.9f ? 2 : h.Clock >= 0.35f + 0.4f ? 1 : 0;
                while (h.Thumps < due)
                {
                    h.Thumps++;
                    PostFriend(h);
                    Burst(h.X - h.Dir * 0.18f * unit, h.Y, unit, 3, 0.25f, 0.18f, 0);
                    Log($"hare{i} THUMP {h.Thumps} (Friend, reach {0.4f * U:F0} px)");
                }
                                // the fox coming within about three metres is no time for theatre (Pack's TooClose): the second strike is skipped and it runs
                bool foxNear = _fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - h.X) < 0.17f * U && MathF.Abs(_fox.Y - h.Y) < 0.1f * U;
                if (h.Clock >= 1.55f || (foxNear && h.Clock > 0.5f)) { h.ThumpCool = 20f; h.FearX = _fox!.X; h.FearY = _fox.Y; GoBolt(h, i, 0f); }
                break;
            }
            case HD.Go:
            {
                int pg = Math.Max(0, 3 - (int)(h.Clock / 0.24f * 4f));
                Show(h, HB_Settle, pg, mir);
                if (h.Clock >= 0.24f) { h.Mind.Begin(HD.Hop, Mind<HD>.Forever); h.Clock = 0; h.Odo = FoxAndHaresArt.HopRest; h.LastFrac = FoxAndHaresArt.HopRest; }
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
                Show(h, HB_Hop, (int)(f2 * 16f) % 16, mir);
                HareLandings(h, f2, unit, FoxAndHaresArt.HareHopD, false);
                if (h.Odo >= FoxAndHaresArt.HopRest + h.Hops) { h.Pace.Halt(); BeginHare(h, HD.Land, 0.24f); }
                break;
            }
            case HD.Land:
            {
                Show(h, HB_Settle, Math.Min(3, (int)(h.Clock / 0.24f * 4f)), mir);
                if (h.Mind.Over)
                {
                    if (h.Next != HD.Sit) { HD nx = h.Next; h.Next = HD.Sit; BeginHare(h, nx, nx == HD.Nibble ? 3.5f + 5f * (float)Rng.NextDouble() : 3f); }
                    else BeginHare(h, HD.Sit, 1f + 4f * (float)Rng.NextDouble());
                }
                break;
            }
            case HD.Binky:
            {
                // a joyful leap: 1.1 s, drifting forward a little; lands with a puff and a few sparkles
                float f = Math.Min(0.999f, h.Clock / 1.1f);
                Show(h, HB_Binky, (int)(f * 16f), mir);
                float prev = Math.Min(0.999f, (h.Clock - dt) / 1.1f);
                float adv = (FoxAndHaresArt.Sm(f) - FoxAndHaresArt.Sm(prev)) * 0.9f * unit;
                float nx = h.X + h.Dir * adv;
                if (Valid(nx, h.Y, 0.3f * unit)) h.X = nx;
                if (prev < 0.78f && f >= 0.78f) { Burst(h.X + h.Dir * 0.3f * unit, h.Y, unit, 5, 0.35f, 0.3f, 1); for (int k = 0; k < 3; k++) Emit(3, h.X + h.Dir * (0.1f + 0.2f * k) * unit, h.Y, 0.15f * unit, (k - 1) * 0.3f * unit, 0.4f * unit, 0.5f, 0, 0.9f, 0.7f * unit); Track(h.X + h.Dir * 0.3f * unit, h.Y, 0); }
                if (h.Clock >= 1.1f) BeginHare(h, HD.Sit, 1f);
                break;
            }
            case HD.Bolt:
            {
                // BOLT: a fast zigzag (the lane wanders) to its burrow; the pace eases up under a cap, never starts at full speed
                h.Pace.Want = BoltSpeed * unit;
                float step = h.Pace.Tick(dt);
                float cyc = FoxAndHaresArt.HareFleeD * unit;
                h.Odo += step / cyc; h.X += h.Dir * step;
                h.JinkT -= dt;
                float toB = MathF.Abs(h.BX - h.X);
                if (h.JinkT <= 0f)
                {
                    h.JinkT = 0.25f + 0.25f * (float)Rng.NextDouble();
                    float ny = h.Y + ((float)Rng.NextDouble() * 2f - 1f) * 0.045f * (S.Height - S.WalkY(h.X));
                    if (toB < 0.12f * U) ny = h.BY;
                    if (Valid(h.X + h.Dir * 0.08f * U, ny, 0.3f * unit) && LineClear(h.X, ny, h.BX, h.BY, 0.3f * unit)) h.GY = ny;
                }
                h.Y += Math.Clamp(h.GY - h.Y, -0.55f * step, 0.55f * step);
                float f2 = h.Odo - MathF.Floor(h.Odo);
                Show(h, HB_Flee, (int)(f2 * 16f) % 16, mir);
                HareLandings(h, f2, unit, FoxAndHaresArt.HareFleeD, true);
                // dive when it is at the burrow and the page is in the flight phase (about page 4 to 6); if the run is far past, dive anyway
                bool atBurrow = (h.BX - h.X) * h.Dir < 0.6f * unit;
                bool pageOk = f2 > 0.22f && f2 < 0.42f;
                if (atBurrow && (pageOk || (h.BX - h.X) * h.Dir < -0.5f * unit)) { h.Pace.Halt(); BeginHare(h, HD.Dive, 0.55f); h.LX = h.X; }
                else if (h.Mind.Over || (!Valid(h.X + h.Dir * 0.05f * U, h.Y, 0.2f * unit) && toB > 0.5f * unit)) { h.BX = h.X; h.BY = h.Y; h.Pace.Halt(); BeginHare(h, HD.Dive, 0.55f); }
                break;
            }
            case HD.Dive:
            {
                float f = Math.Min(0.999f, h.Clock / 0.55f);
                Show(h, HB_Dive, (int)(f * 10f), mir);
                float prev = Math.Min(0.999f, (h.Clock - dt) / 0.55f);
                h.X += h.Dir * (FoxAndHaresArt.Sm(f) - FoxAndHaresArt.Sm(prev)) * 0.8f * unit * 1.0f;
                if (prev < 0.5f && f >= 0.5f)
                {
                    float hx = h.X + h.Dir * 0.5f * unit;
                    h.BX = hx; h.BY = h.Y;
                    Burst(hx, h.Y, unit, 9, 0.45f, 0.55f, 2);
                    Track(hx, h.Y, 2);
                    Log($"hare{i} dives into the snow at {hx:F0},{h.Y:F0}");
                }
                if (h.Clock >= 0.55f) { h.Mind.Begin(HD.Under, 7f + 8f * (float)Rng.NextDouble()); h.Clock = 0; h.Vis = false; PickEmerge(h); }
                break;
            }
            case HD.Under:
            {
                h.Vis = false;
                // wait while the fox is near (it noses the hole): the hare comes up only when the fox is far or gone
                bool foxNear = _fox != null && _fox.Mind.Doing != FD.Away && MathF.Abs(_fox.X - h.ToBX) < (_fox.Mind.Doing == FD.Asleep ? 0.12f : 0.30f) * U;
                if (h.Mind.Over && foxNear && h.Clock < 40f) PickEmerge(h);       // the fox is near the spot it meant to come up at: choose another
                if (h.Mind.Over && (!foxNear || h.Clock >= 40f))
                {
                    h.X = h.ToBX; h.Y = h.ToBY; h.Dir = Rng.Next(2) == 0 ? 1 : -1; h.Vis = true;
                    h.Mind.Begin(HD.Peek, Mind<HD>.Forever); h.Clock = 0; h.PeekAt = 0; h.PeekT = 0;
                    Track(h.X, h.Y, 2); Burst(h.X, h.Y, unit, 3, 0.2f, 0.2f, 0);
                    Log($"hare{i} puts up its ears at {h.X:F0},{h.Y:F0}");
                }
                break;
            }
            case HD.Peek:
            {
                // ears, then the eyes, a look round, then the whole head; then the hare pops out with a puff
                int[] pg = [0, 1, 0, 2, 3, 2, 4, 5];
                float[] du = [0.7f, 0.5f, 0.4f, 0.5f, 0.5f, 0.4f, 0.6f, 0.6f];
                Show(h, HB_Peek, pg[h.PeekAt], mir);
                h.PeekT += dt;
                if (h.PeekT >= du[h.PeekAt]) { h.PeekT = 0; h.PeekAt++; }
                if (h.PeekAt >= pg.Length)
                {
                    Burst(h.X, h.Y, unit, 6, 0.4f, 0.4f, 1);
                    BeginHare(h, HD.Sit, 2f + 3f * (float)Rng.NextDouble());
                }
                break;
            }
        }
        LogHare(i, h, dt);
    }

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

    private void LogHare(int i, H h, float dt)
    {
        if (_log != null)
        {
            float sp = dt > 0 ? MathF.Sqrt((h.X - h.LX) * (h.X - h.LX) + (h.Y - h.LY) * (h.Y - h.LY)) / dt : 0f;
            _log.WriteLine($"{_time,8:F3} hare{i} x {h.X:F1} y {h.Y:F1} v {sp:F1} {h.Mind.Doing}{(h.Turning ? "/Turn" : "")} {h.Bk?.Name}#{h.Pg} dir {h.Dir}{(h.Vis ? "" : " hidden")}");
        }
        h.LX = h.X; h.LY = h.Y;
    }

    /// <summary>What next for a sitting hare: nibble, groom, hop on, stand tall, binky, or sit on.</summary>
    private void RollHare(H h, int i)
    {
        float U = S.U;
        bool lull = Lull;
        int twig = NearestTwig(h);
        bool pal = false;
        for (int j = 0; j < _n; j++) if (j != i && MathF.Abs(_h[j].X - h.X) < 0.35f * U && _h[j].Mind.Doing is not (HD.Under or HD.Peek)) pal = true;
        int face = Dice.Pick(Rng, [0.20f, twig >= 0 ? 0.30f : 0f, 0.20f, lull ? 0.05f : 0.50f, 0.07f, lull ? 0f : 0.04f + (pal ? 0.22f : 0.03f) * h.Play]);
        switch (face)
        {
            case 1: StartNibbleTrip(h, twig); break;
            case 2: BeginHare(h, HD.Groom, 5.5f); break;
            case 3: StartHop(h, 0f); break;
            case 4: BeginHare(h, HD.Tall, 2f + 3f * (float)Rng.NextDouble()); break;
            case 5: BeginHare(h, HD.Binky, 1.1f); break;
            default: BeginHare(h, HD.Sit, Organic.Jitter(Rng, 3.5f)); break;
        }
    }

    private int NearestTwig(H h)
    {
        int best = -1; float bd = 0.30f * S.U;
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
        if (!Valid(sx, sy, 0.3f * unit)) { BeginHare(h, HD.Sit, 2f); return; }
        h.Twig = twig; h.Next = HD.Nibble;
        float nd = 3.5f + 5f * (float)Rng.NextDouble();
        if (MathF.Abs(sx - h.X) < 0.2f * unit && MathF.Abs(sy - h.Y) < 0.1f * unit)
        {
            // close enough to nibble from where it sits (no snap: it just turns to face the twig first if it must)
            h.Next = HD.Sit;
            if (side != h.Dir) StartTurn(h, side, HD.Nibble, 1f, nd); else BeginHare(h, HD.Nibble, nd);
            return;
        }
        PlanHop(h, sx, sy, side);
    }

    private void StartHop(H h, float _)
    {
        float U = S.U;
        for (int tries = 0; tries < 16; tries++)
        {
            float dx = (0.05f + 0.17f * (float)Rng.NextDouble()) * U * (Rng.Next(2) == 0 ? 1 : -1);
            float gx = h.X + dx, gy = h.Y + ((float)Rng.NextDouble() - 0.5f) * 0.4f * MathF.Abs(dx);
            float unit = HU(h.Y);
            if (!Valid(gx, gy, 0.3f * unit) || !LineClear(h.X, h.Y, gx, gy, 0.3f * unit)) continue;
            bool crowd = false; for (int j = 0; j < _n; j++) if (_h[j] != h && MathF.Abs(_h[j].X - gx) < 0.9f * unit * 1.2f && MathF.Abs(_h[j].Y - gy) < 0.05f * U) crowd = true;
            if (crowd) continue;
            h.Next = HD.Sit; PlanHop(h, gx, gy, dx > 0 ? 1 : -1); return;
        }
        BeginHare(h, HD.Sit, 2f);
    }

    /// <summary>Plans a run of whole hops to (gx, gy): the count is the nearest whole number, so the run ends on the rest page. Turns first if it is facing the wrong way.</summary>
    private void PlanHop(H h, float gx, float gy, int dir)
    {
        float unit = HU(h.Y);
        h.GX = gx; h.GY = gy;
        float dist = MathF.Abs(gx - h.X);
        h.Hops = Math.Clamp((int)MathF.Round(dist / (FoxAndHaresArt.HareHopD * unit)), 1, 6);
        // the hop is stretched or squeezed a little (never more than a quarter) so the run ends ON the goal, not up to half a hop off it
        h.HopK = Math.Clamp(dist / (h.Hops * FoxAndHaresArt.HareHopD * unit), 0.78f, 1.28f);
        if (dir != h.Dir) StartTurn(h, dir, HD.Go, 1f, 0.24f);
        else BeginHare(h, HD.Go, 0.24f);
    }

    private void StartThump(H h, int i)
    {
        int to = h.FearX >= h.X ? 1 : -1;
        h.Thumps = 0;
        if (to != h.Dir) { StartTurn(h, to, HD.Thump, 1f, 1.7f); h.Mind.Begin(HD.Sit, 0.2f); }
        else BeginHare(h, HD.Thump, 1.7f);
        Log($"hare{i} sees the {(h.FearFox ? "threat" : "noise")} and rears up to thump");
    }

    /// <summary>Starts a bolt: after sitting tall for "alertFor" seconds (a hare that heard a thump), away from the cause, to a burrow it picks.</summary>
    private void GoBolt(H h, int i, float alertFor)
    {
        float U = S.U, unit = HU(h.Y);
        int away = h.FearX >= h.X ? -1 : 1;
        // a burrow 0.28 to 0.42 U away, on the far side from the danger, somewhere it may stand with a clear run
        h.BX = h.X; h.BY = h.Y;
        for (int tries = 0; tries < 24; tries++)
        {
            int dr = tries < 14 ? away : -away;
            float bx = h.X + dr * (0.28f + 0.14f * (float)Rng.NextDouble()) * U;
            float by = h.Y + ((float)Rng.NextDouble() - 0.5f) * 0.10f * U;
            if (Valid(bx, by, 0.4f * unit) && LineClear(h.X, h.Y, bx, by, 0.3f * unit)) { h.BX = bx; h.BY = by; away = dr; break; }
        }
        if (MathF.Abs(h.BX - h.X) < 0.1f * U) away = h.Dir;
        h.GY = h.Y; h.JinkT = 0.2f; h.Odo = FoxAndHaresArt.HopRest; h.LastFrac = FoxAndHaresArt.HopRest;
        h.Pace.Halt(); h.Pace.Accel = 30f * unit;
                // sit tall for a moment if it heard a thump (alert), then turn away (quickly) and run
        h.Turning = false;
        if (alertFor > 0.01f) { BeginHare(h, HD.Tall, alertFor + 0.7f); h.Mind.Left = alertFor; h.BoltPending = true; h.BoltDir = away; }
        else StartBoltNow(h, away, i);
    }

    private void StartBoltNow(H h, int away, int i)
    {
        h.BoltPending = false;
        if (away != h.Dir) StartTurn(h, away, HD.Bolt, 0.5f, 6f);
        else BeginHare(h, HD.Bolt, 6f);
        Log($"hare{i} bolts to a burrow at {h.BX:F0},{h.BY:F0}");
    }

    private void PickEmerge(H h)
    {
        float U = S.U, unit = HU(h.Y);
        h.ToBX = h.BX; h.ToBY = h.BY;
        for (int tries = 0; tries < 40; tries++)
        {
            float x = h.BX + ((float)Rng.NextDouble() * 2f - 1f) * 0.25f * U, y = h.BY + ((float)Rng.NextDouble() - 0.5f) * 0.10f * U;
            bool awayFromFox = _fox == null || _fox.Mind.Doing == FD.Away || MathF.Abs(_fox.X - x) > 0.35f * U || tries > 22;
            if (MathF.Abs(x - h.BX) > 0.10f * U && awayFromFox && Valid(x, y, 0.4f * unit)) { h.ToBX = x; h.ToBY = y; return; }
        }
    }

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
            if (_h[j].Mind.Doing is HD.Under or HD.Dive or HD.Peek or HD.Bolt) continue;
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
        f.Mind.Tick(dt); f.Clock += dt; f.Att.Tick(dt); f.BreathT -= dt; f.PostT -= dt; f.GlanceCool -= dt;
        FD d = f.Mind.Doing;
        bool mir = f.Dir < 0;
        if (d != FD.Away) f.OnScreen += dt;

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
            int step = (int)(fr * 7);
            if (step < 4) ShowF(f, step == 0 ? FB_Stand : FB_Turn, step == 0 ? 0 : step - 1, f.TurnFrom < 0);
            else ShowF(f, FB_Turn, 6 - step, f.TurnTo < 0);
            if (step >= 3) f.Dir = f.TurnTo;
            if (f.TurnT >= TurnFor) { f.Turning = false; f.Dir = f.TurnTo; BeginFox(f, f.After, f.After == FD.Stand ? Organic.Jitter(Rng, 1.5f) : Mind<FD>.Forever); }
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
                else if (d == FD.Asleep) { BeginFox(f, FD.Wake, 1f); Burst(f.X, f.Y, unit, 10, 0.5f, 0.6f, 2); d = FD.Wake; }
                else if (d == FD.Stand && r != Reaction.Approach && (f.GlanceCool <= 0f || cause.Kind != Sense.Movement || cause.At.Y > f.Y - 0.18f * S.Height))
                {
                    if (cause.At.Y < f.Y - 0.18f * S.Height) f.GlanceCool = 25f;
                    f.LookUp = Math.Clamp((f.Y - cause.At.Y) / (0.5f * S.Height), 0.15f, 1f); f.LookDur = 2.2f;
                    BeginFox(f, FD.Look, f.LookDur); d = FD.Look;
                }
            }
        }

        switch (d)
        {
            case FD.Go:
            {
                // getting going: the settle pages played backwards, then the trot from the same rest page
                int rb = f.RestIdx == 0 ? FB_Settle0 : FB_Settle1;
                ShowF(f, rb, Math.Max(0, 3 - (int)(f.Clock / 0.22f * 4f)), mir);
                if (f.Clock >= 0.22f) { f.Mind.Begin(f.GoLeave ? FD.Leave : FD.Trot, Mind<FD>.Forever); f.Clock = 0; f.Odo = (f.RestIdx == 0 ? RestA : RestB) * FoxAndHaresArt.FoxTrotStride; f.Braked = false; f.Pace.Halt(); f.Pace.Accel = 2.2f * unit; }
                break;
            }
            case FD.Trot:
            case FD.Leave:
            {
                float stride = FoxAndHaresArt.FoxTrotStride;
                f.Pace.Want = FoxTrotV * unit * (d == FD.Leave ? 1.15f : 1f);
                float step = f.Pace.Tick(dt);
                f.Odo += step / unit; f.X += f.Dir * step;
                float dxLeft = MathF.Max(1f, (f.GX - f.X) * f.Dir);
                f.Y += Math.Clamp((f.GY - f.Y) * Math.Min(1f, step / dxLeft), -0.45f * step, 0.45f * step);
                float ph = f.Odo / stride; ph -= MathF.Floor(ph);
                ShowF(f, FB_Trot, (int)(ph * 24f) % 24, mir);
                if (_fb != null && f.Pg % 6 == 0 && f.Pg != f.LastPrint) { f.LastPrint = f.Pg; Track(f.X + f.Dir * 0.25f * unit, f.Y, 3); }
                float left = (f.GX - f.X) * f.Dir;
                if (d == FD.Leave)
                {
                    if (f.X < -1.5f * unit || f.X > W + 1.5f * unit) { f.Mind.Begin(FD.Away, Mind<FD>.Forever); _nextAway = 22f + 28f * (float)Rng.NextDouble(); f.OnScreen = 0; Log($"fox is away for {_nextAway:F0} s"); }
                }
                else if (!f.Braked && left < 0.8f * unit) BrakeToRest(f, stride, unit, [RestA, RestB]);
                else if (f.Braked && f.Pace.Still) { f.Braked = false; BeginFox(f, FD.Settle, 0.22f); }
                break;
            }
            case FD.Settle:
            {
                int rb = f.RestIdx == 0 ? FB_Settle0 : FB_Settle1;
                ShowF(f, rb, Math.Min(3, (int)(f.Clock / 0.22f * 4f)), mir);
                if (f.Clock >= 0.22f) BeginFox(f, FD.Stand, Organic.Jitter(Rng, 2.2f));
                break;
            }
            case FD.Stand:
            {
                ShowF(f, FB_Stand, Cyc(f.Clock, 8f, 24, 0f), mir);
                Breathe(f, unit);
                if (f.Mind.Over) RollFox(f);
                break;
            }
            case FD.Look:
            {
                float lv = Math.Min(1f, f.Clock / 0.35f) * (f.Clock > f.LookDur - 0.35f ? Math.Max(0f, (f.LookDur - f.Clock) / 0.35f) : 1f) * f.LookUp;
                ShowF(f, FB_Look, (int)MathF.Round(lv * 4f), mir);
                if (f.Mind.Over) BeginFox(f, FD.Stand, Organic.Jitter(Rng, 1.5f));
                break;
            }
            case FD.SitIn:
            {
                int pg = f.Reverse ? Math.Max(0, 7 - (int)(f.Clock / 0.6f * 8f)) : Math.Min(7, (int)(f.Clock / 0.6f * 8f));
                ShowF(f, FB_SitIn, pg, mir);
                if (f.Clock >= 0.6f) { if (f.Reverse) { f.Reverse = false; BeginFox(f, FD.Stand, Organic.Jitter(Rng, 1.5f)); } else if (f.SleepAfter) { f.SleepAfter = false; f.SleepFor = 45f; BeginFox(f, FD.CurlIn, 1f); } else BeginFox(f, FD.Sit, 5f + 5f * (float)Rng.NextDouble()); }
                break;
            }
            case FD.Sit:
            {
                ShowF(f, FB_Sit, Cyc(f.Clock, 5f, 12, 0f), mir);
                Breathe(f, unit);
                if (f.Mind.Over)
                {
                    if (Rng.NextDouble() < 0.35 && _test != "mouse") { BeginFox(f, FD.CurlIn, 1f); f.SleepFor = 22f + 25f * (float)Rng.NextDouble(); }
                    else { f.Reverse = true; BeginFox(f, FD.SitIn, 0.6f); }
                }
                break;
            }
            case FD.CurlIn:
            {
                ShowF(f, FB_CurlIn, Math.Min(11, (int)(f.Clock / 1.1f * 12f)), mir);
                if (f.Clock >= 1.1f) { BeginFox(f, FD.Asleep, f.SleepFor); Log("fox is asleep"); }
                break;
            }
            case FD.Asleep:
            {
                int snow = Math.Min(3, (int)(f.Clock / 9f));
                ShowF(f, FB_Sleep, snow * 4 + Cyc(f.Clock, 0.45f, 4, 0f), mir);
                if (f.BreathT <= 0f && _fb != null) { Emit(1, f.X + f.Dir * 0.18f * unit, f.Y, 0.12f * unit, f.Dir * 0.04f * unit, 0.05f * unit, 1.3f, 0, 0.4f); f.BreathT = 3.2f; }
                if (f.Mind.Over) { BeginFox(f, FD.Wake, 1.0f); Burst(f.X, f.Y, unit, 8, 0.45f, 0.5f, 2); }
                break;
            }
            case FD.Wake:
            {
                ShowF(f, FB_CurlIn, Math.Max(0, 11 - (int)(f.Clock / 0.9f * 12f)), mir);
                if (f.Clock >= 0.9f) BeginFox(f, FD.Sit, 3f);
                break;
            }
            case FD.Mouse: StepMouse(f, dt, unit, mir); break;
            case FD.StalkIn:
            {
                int pg = f.Reverse ? Math.Max(0, 4 - (int)(f.Clock / 0.4f * 5f)) : Math.Min(4, (int)(f.Clock / 0.4f * 5f));
                ShowF(f, FB_StalkIn, pg, mir);
                if (f.Clock >= 0.4f)
                {
                    if (f.Reverse) { f.Reverse = false; BeginFox(f, FD.Stand, Organic.Jitter(Rng, 2f)); }
                    else { f.Mind.Begin(FD.Stalk, 16f); f.Clock = 0; f.Odo = StalkRest * FoxAndHaresArt.FoxStalkStride; f.Braked = false; f.Pace.Halt(); f.Pace.Accel = 1.2f * unit; }
                }
                break;
            }
            case FD.Stalk:
            {
                float stride = FoxAndHaresArt.FoxStalkStride;
                bool have = f.Target >= 0 && _h[f.Target].Mind.Doing is not (HD.Under or HD.Dive or HD.Peek);
                H? t = have ? _h[f.Target] : null;
                bool hareAlarmed = t != null && t.Mind.Doing is HD.Thump or HD.Bolt or HD.Tall;
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
                    f.After2 = t != null && (close || hareAlarmed) && !Lull ? FD.DashIn : FD.StalkOut;
                    f.Pace.Accel = 6f * unit;
                    f.Braked = true; f.LastPh = ph;
                    Log($"fox ends the stalk ({(t == null ? "lost it" : close ? "close enough" : hareAlarmed ? "it is alarmed" : "time")}), then {f.After2}");
                }
                else if (f.Braked && f.Pace.Still) { f.Braked = false; if (f.After2 == FD.DashIn) { BeginFox(f, FD.DashIn, 0.3f); } else { f.Reverse = true; BeginFox(f, FD.StalkIn, 0.4f); } }
                break;
            }
            case FD.DashIn:
            {
                ShowF(f, FB_DashIn, Math.Min(3, (int)(f.Clock / 0.3f * 4f)), mir);
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
                bool hasHole = t != null && (t.Mind.Doing is HD.Dive or HD.Under or HD.Peek);
                if (hasHole) { f.StopX = f.HoleX - f.Dir * 0.5f * unit; }
                if (!f.SkidGo)
                {
                    // while the hare is still rearing up to thump the fox eases off, three metres behind it: it never overruns a hare that has not bolted
                    float easeF = 1f;
                    if (t != null && !hareGone && t.Mind.Doing is not (HD.Bolt or HD.Dive)) easeF = Math.Clamp(((t.X - f.X) * f.Dir / unit - 2.2f) / 2.5f, 0.12f, 1f);
                    f.Pace.Want = FoxChaseV * unit * easeF;
                    float step = f.Pace.Tick(dt);
                    f.Odo += step / unit; f.X += f.Dir * step;
                    float dxl = MathF.Max(8f, MathF.Abs(tx - f.X));
                    f.Y += Math.Clamp((ty - f.Y) * Math.Min(1f, step / dxl), -0.5f * step, 0.5f * step);
                }
                float ph = f.Odo / stride; ph -= MathF.Floor(ph);
                ShowF(f, FB_Gallop, (int)(ph * 16f) % 16, mir);
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
                    ShowF(f, FB_Skid, Math.Min(7, (int)(f.Clock / 0.55f * 8f)), mir);
                    if (step > 0.5f * unit * dt) Emit(0, f.X - f.Dir * 0.15f * unit, f.Y, 0.03f * unit, -f.Dir * 0.6f * unit, 0.3f * unit, 0.5f, 0, 0.7f, 1.2f * unit);
                    if (f.Pace.Still && f.Clock > 0.2f)
                    {
                        f.SkidGo = false; f.Braked = false;
                        bool hole = hasHole && MathF.Abs(f.HoleX - f.X) < 1.4f * unit;
                        if (hole) { BeginFox(f, FD.Nose, 4.2f); f.Stage = 0; } else BeginFox(f, FD.Stand, Organic.Jitter(Rng, 2f));
                    }
                }
                break;
            }
            case FD.Nose:
            {
                // nose at the burrow (about 1.6 s), dig twice (1.4 s, snow flying), nose again, give up (then it sits)
                float c = f.Clock;
                if (c < 0.35f) ShowF(f, FB_NoseIn, Math.Min(3, (int)(c / 0.35f * 4f)), mir);
                else if (c < 1.8f) ShowF(f, FB_Nose, Cyc(c, 6f, 8, 0f), mir);
                else if (c < 3.2f)
                {
                    int pg = Cyc(c - 1.8f, 12f / 0.7f, 12, 0f);
                    ShowF(f, FB_Dig, pg, mir);
                    if (pg == 6 && f.Stage == 0) { f.Stage = 1; SprayAt(f, unit); }
                    if (pg == 0 || pg == 11) f.Stage = 0;
                }
                else if (c < 4.0f) ShowF(f, FB_Nose, Cyc(c, 5f, 8, 0f), mir);
                else ShowF(f, FB_NoseIn, Math.Max(0, 3 - (int)((c - 4.0f) / 0.2f * 4f)), mir);
                if (c >= 4.2f) { f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); Log("fox gives up on the burrow and sits"); }
                break;
            }
        }
        LogFox(f, dt);
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
        bool left = Rng.Next(2) == 0;
        for (int tries = 0; tries < 20; tries++)
        {
            float gx = (left ? 0.25f + 0.5f * (float)Rng.NextDouble() : 0.5f + 0.45f * (float)Rng.NextDouble()) * W;
            PointF g = S.FieldPoint(gx, 0.2f + 0.35f * (float)Rng.NextDouble());
            if (!Valid(g.X, g.Y, 0.5f * unit)) continue;
            f.Dir = left ? 1 : -1;
            f.X = left ? -1.6f * unit : W + 1.6f * unit; f.Y = S.FieldPoint(f.X < 0 ? 0.02f * W : 0.98f * W, 0.22f).Y;
            f.GX = g.X; f.GY = g.Y;
            if ((f.GX - f.X) * f.Dir < 3f * unit) continue;
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
        bool hare = FoxHareVisible(out int hi) && !lull;
        float leave = 0.04f + Math.Clamp((f.OnScreen - 40f) / 90f, 0f, 0.6f);
        int face = Dice.Pick(Rng, [lull ? 0.05f : 0.30f, 0.10f, 0.18f, lull ? 0f : 0.26f, hare ? 0.55f : 0f, leave]);
        Log($"fox rolls {face} (onscreen {f.OnScreen:F0} s)");
        switch (face)
        {
            case 0: FoxTrotTo(f, false); break;
            case 1: BeginFox(f, FD.Stand, Organic.Jitter(Rng, 3.5f)); break;
            case 2: f.Reverse = false; BeginFox(f, FD.SitIn, 0.6f); break;
            case 3: StartMouse(f); break;
            case 4: f.Target = hi; StartStalk(f, _h[hi]); break;
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
            if (MathF.Abs(g.X - f.X) < 0.18f * S.U || !Valid(g.X, g.Y, 0.5f * unit)) continue;
            if (MathF.Abs(g.Y - f.Y) > 0.45f * MathF.Abs(g.X - f.X)) continue;
            if (!LineClear(f.X, f.Y, g.X, g.Y, 0.45f * unit)) continue;
            f.GX = g.X; f.GY = g.Y; f.GoLeave = false;
            int to = g.X >= f.X ? 1 : -1;
            // the fox is at a rest page (stand): it turns on the spot if needed, then gets going
            f.RestIdx = Rng.Next(2);
            if (to != f.Dir) StartFoxTurn(f, to, FD.Go); else BeginFox(f, FD.Go, 0.22f);
            return;
        }
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
        if (!Valid(ex, f.Y, 0.4f * unit)) { int to = -f.Dir; ex = f.X + to * 1.0f * unit; if (!Valid(ex, f.Y, 0.4f * unit)) { BeginFox(f, FD.Stand, 2f); return; } StartFoxTurn(f, to, FD.Mouse); f.MouseInit = true; return; }
        BeginFox(f, FD.Mouse, Mind<FD>.Forever); f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * unit; f.Sneezes = Rng.NextDouble() < 0.35;
        f.ListenFor = 3.0f + 2.5f * (float)Rng.NextDouble();
    }

    private void StepMouse(F f, float dt, float unit, bool mir)
    {
        if (f.MouseInit) { f.MouseInit = false; f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * unit; f.Sneezes = Rng.NextDouble() < 0.35; f.ListenFor = 3.0f + 2.5f * (float)Rng.NextDouble(); }
        f.StageT += dt;
        float c = f.StageT;
        switch (f.Stage)
        {
            case 0: // LISTEN: head low and cocked, ears swivelling; a Loud breaks it (checked below), a long listen ends in the coil
                ShowF(f, FB_Listen, Cyc(c, 6.7f, 16, 0f), mir);
                Breathe(f, unit);
                foreach (Notice n in Board.Live) if (n.Kind == Sense.Loud && n.Who != f && MathF.Abs(n.At.X - f.X) < n.Reach) { BeginFox(f, FD.Stand, 2f); Log("fox's listening is broken by a noise"); return; }
                if (c >= f.ListenFor) { f.Stage = 1; f.StageT = 0; }
                break;
            case 1: // COIL
                ShowF(f, FB_Coil, Math.Min(3, (int)(c / 0.45f * 4f)), mir);
                if (c >= 0.45f) { f.Stage = 2; f.StageT = 0; f.LeapX0 = f.X; Log("fox LEAPS"); }
                break;
            case 2: // LEAP: 0.8 s, an arc, carried forward 0.45 m
            {
                float fr = Math.Min(0.999f, c / 0.8f);
                ShowF(f, FB_Leap, (int)(fr * 14f), mir);
                f.X = f.LeapX0 + f.Dir * f.LeapDist * FoxAndHaresArt.Sm(fr);
                if (c >= 0.8f)
                {
                    f.Stage = 3; f.StageT = 0; f.HoleX = f.X + f.Dir * 0.27f * unit; f.HoleY = f.Y;
                    Track(f.HoleX, f.Y, 2); Burst(f.HoleX, f.Y, unit, 12, 0.7f, 0.9f, 2);
                }
                break;
            }
            case 3: // DIVE IN
                ShowF(f, FB_DiveIn, Math.Min(3, (int)(c / 0.25f * 4f)), mir);
                if (c >= 0.25f) { f.Stage = 4; f.StageT = 0; }
                break;
            case 4: // PLUNGE: hind legs kicking, brush waving
                ShowF(f, FB_Plunge, Cyc(c, 10f, 8, 0f), mir);
                if (((int)(c * 6f)) != ((int)((c - dt) * 6f))) Emit(0, f.HoleX, f.Y, 0.05f * unit, f.Dir * (0.2f + 0.3f * (float)Rng.NextDouble()) * unit, 0.5f * unit, 0.5f, 0, 0.8f, 2f * unit);
                if (c >= 1.8f) { f.Stage = 5; f.StageT = 0; }
                break;
            case 5: // BACK OUT with a face full of snow
                ShowF(f, FB_BackOut, Math.Min(7, (int)(c / 1.0f * 8f)), mir);
                if (c >= 1.0f) { f.Stage = 6; f.StageT = 0; Burst(f.HoleX, f.Y, unit, 6, 0.5f, 0.5f, 1); }
                break;
            case 6: // SHAKE the head, flinging snow
                ShowF(f, FB_Shake, Cyc(c, 9f, 8, 0f), mir);
                if (((int)(c * 10f)) != ((int)((c - dt) * 10f)) && _fb != null)
                    Emit(0, f.X + f.Dir * 0.35f * unit, f.Y, 0.35f * unit, (float)(Rng.NextDouble() * 2 - 1) * 0.8f * unit, 0.3f * unit, 0.5f, 0, 0.9f, 2.2f * unit);
                if (c >= 1.1f) { f.Stage = 7; f.StageT = 0; }
                break;
            case 7: // WIPE: the last snow off the muzzle
                ShowF(f, FB_Wipe, Math.Min(3, (int)(c / 0.3f * 4f)), mir);
                if (c >= 0.3f) { if (f.Sneezes) { f.Stage = 8; f.StageT = 0; } else EndMouse(f); }
                break;
            case 8: // SNEEZE
                ShowF(f, FB_Sneeze, Math.Min(7, (int)(c / 0.75f * 8f)), mir);
                if (((int)(c / 0.75f * 8f)) == 4 && ((int)((c - dt) / 0.75f * 8f)) == 3 && _fb != null) { PointF nz = f.Bk!.Aux[f.Pg]; for (int k = 0; k < 4; k++) Emit(0, f.X + f.Dir * nz.X * unit, f.Y, nz.Y * unit, f.Dir * (0.3f + 0.4f * (float)Rng.NextDouble()) * unit, (0.1f + 0.3f * (float)Rng.NextDouble()) * unit, 0.5f, 0, 0.9f, 1.2f * unit); }
                if (c >= 0.75f) EndMouse(f);
                break;
        }
    }

    private void EndMouse(F f) { Log("fox found nothing and carries on"); BeginFox(f, FD.Stand, Organic.Jitter(Rng, 1.2f)); }

    private void LogFox(F f, float dt)
    {
        if (_log != null)
        {
            float sp = dt > 0 ? MathF.Sqrt((f.X - f.LX) * (f.X - f.LX) + (f.Y - f.LY) * (f.Y - f.LY)) / dt : 0f;
            _log.WriteLine($"{_time,8:F3} fox x {f.X:F1} y {f.Y:F1} v {sp:F1} {f.Mind.Doing}{(f.Turning ? "/Turn" : "")}{(f.Mind.Doing == FD.Mouse ? "/stage" + f.Stage : "")} {f.Bk?.Name}#{f.Pg} dir {f.Dir}");
        }
        f.LX = f.X; f.LY = f.Y;
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
                PointF p = S.FieldPoint(0.62f * W, 0.36f); h0.X = p.X; h0.Y = p.Y; h0.Dir = 1; h0.Mind.Begin(HD.Sit, 60f); h0.Timid = 0.6f; h0.Att.Timid = 0.7f;
                for (int i = 1; i < _n; i++) { PointF q = S.FieldPoint((0.30f + 0.03f * i) * W, 0.22f); _h[i].X = q.X; _h[i].Y = q.Y; _h[i].Mind.Begin(HD.Sit, 60f); }
                FoxAt(0.90f, 0.36f, -1); f.Target = 0; StartStalk(f, h0); break;
            }
            case "groom": Calm(); h0.Mind.Begin(HD.Sit, 60f); BeginHare(h0, HD.Groom, 5.5f); break;
            case "binky": Calm(); BeginHare(h0, HD.Binky, 1.1f); break;
            case "nibble": { Calm(); int tw = NearestTwig(h0); if (tw >= 0) StartNibbleTrip(h0, tw); break; }
            case "tall": Calm(); BeginHare(h0, HD.Tall, 4f); break;
            case "thump": Calm(); h0.FearX = h0.X + (h0.Dir * 0.4f * S.U); h0.FearY = h0.Y; h0.FearFox = true; StartThump(h0, 0); break;
            case "peek": Calm(); h0.BX = h0.X; h0.BY = h0.Y; h0.ToBX = h0.X; h0.ToBY = h0.Y; h0.Vis = false; h0.Mind.Begin(HD.Under, 0.1f); break;
            case "look": FoxAt(0.78f, 0.30f, 1); Board.Post(Sense.Movement, new PointF(f.X, S.Height * 0.1f), 0.8f * S.U, 1f, this); break;
        }
    }

    private void StartMouseNow(F f) { BeginFox(f, FD.Mouse, Mind<FD>.Forever); f.Stage = 0; f.StageT = 0; f.LeapDist = 0.45f * FU(f.Y); f.Sneezes = true; f.ListenFor = 3f; }

    // ================================================================ DRAW

    public override void Collect(DepthQueue q)
    {
        if (_n == 0 || _hb == null) return;
        for (int i = 0; i < _n; i++) if (_h[i].Vis && _h[i].Bk != null) q.Add(_h[i].Y, this, i);
        if (_fox != null && _fox.Mind.Doing != FD.Away && _fox.Bk != null) q.Add(_fox.Y, this, 100);
        if (_decalLive > 0) q.Add(-1f, this, 200);
        for (int k = 0; k < _twigs.Length; k++) q.Add(_twigs[k].Y - 1f, this, 300 + k);
        for (int i = 0; i < _puffs.Length; i++) if (_puffs[i].On) q.Add(_puffs[i].Y + 0.5f, this, 1000 + i);
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (_hb == null || _puffSprites == null || _decalSprites == null) return;
        if (index < 100)
        {
            H h = _h[index]; if (h.Bk == null) return;
            int t = TierOf(h.Y);
            Stamp(fb, h.Bk.Pages[t][h.Pg], h.X, h.Y, h.Bk.Ox[t], h.Bk.Oy[t], h.Mir);
        }
        else if (index == 100)
        {
            F f = _fox!; if (f.Bk == null) return;
            int t = TierOf(f.Y);
            Stamp(fb, f.Bk.Pages[t][f.Pg], f.X, f.Y, f.Bk.Ox[t], f.Bk.Oy[t], f.Mir);
        }
        else if (index == 200)
        {
            // the tracks and burrows lying on the snow, fading with age
            for (int i = 0; i < _decalLive; i++)
            {
                Decal d = _decals[i];
                double age = _time - d.Born; float life = d.Kind == 2 ? 45f : 16f;
                if (age >= life) continue;
                int t = TierOf(d.Y); Sprite sp = _decalSprites[t][d.Kind];
                float op = (float)(1.0 - Math.Max(0.0, (age - life * 0.4) / (life * 0.6)));
                Stamp(fb, sp, d.X, d.Y, sp.Width / 2, sp.Height / 2, false, op);
            }
        }
        else if (index >= 300 && index < 1000)
        {
            var tw = _twigs[index - 300]; int t = TierOf(tw.Y); Book b = _hb[HB_Twig];
            Stamp(fb, b.Pages[t][0], tw.X, tw.Y, b.Ox[t], b.Oy[t]);
        }
        else
        {
            ref Puff p = ref _puffs[index - 1000]; if (!p.On) return;
            int t = TierOf(p.Y); Sprite sp = _puffSprites[t][Math.Clamp((int)p.Size, 0, 2)];
            float k = p.T / p.Life;
            // a puff of snow swells a little and fades; a breath swells more, and is thin
            float op = p.Op * (1f - k) * (p.Kind == 1 ? MathF.Min(1f, k * 5f) : 1f);
            int left = (int)MathF.Round(p.X) - sp.Width / 2, top = (int)MathF.Round(p.Y - p.H) - sp.Height / 2;
            sp.Draw(fb, left, top, op, null, false, S.NearDepth, S.Nearness(p.Y));
        }
    }
}
