using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The acorn caper: a little story that plays out on the near ground every
/// minute or two, for anyone watching the way a child watches (his line:
/// "imagine you're a little kid and you're watching it out of fascination
/// because the squirrel is coming out of its hiding place to grab some nuts
/// and go bury them and then run up a tree, and then another squirrel
/// unburies it and finds it and the squirrels have a fracas and during the
/// fracas a mouse steals it").
///
/// THE STORY, in five acts:
///   1. A tawny squirrel's head rises out of the hollow in the roots of the
///      left maple, blinks, looks about. It hops out, noses through the
///      acorns lying under the oak branch, picks one, climbs its trunk to the
///      fence's rail, bounds along the rail (behind the corn shock if it
///      passes it), drops to the ground at a spot it has chosen, digs a hole
///      with earth flying behind it, drops the nut in, pats the mound flat,
///      looks about to be sure nobody saw, and goes back along the rail and
///      up its tree into the leaves.
///   2. A second squirrel comes head first down the right maple, onto the
///      rail, and comes bounding along it with its nose down, stopping twice
///      to sniff. It stops above the mound, drops down, sniffs it, digs it
///      up and sits up holding the acorn.
///   3. The first squirrel's head appears in the left maple. It freezes,
///      then races down the trunk and along the rail and drops to the ground
///      facing the thief. They crouch and chatter at each other, tails
///      lashing, paws stamping, and go for each other: a burst of dust that
///      rolls and boils with tails, paws and ears popping out of it, and the
///      acorn is flung out of the scuffle and bounces away toward the corn
///      shock.
///   4. A mouse creeps out from the corn shock in stop-start dashes, sniffs
///      the acorn, takes it, and scurries home with it, while the squirrels
///      are still rolling.
///   5. The dust thins over the two squirrels sitting dazed, heads lolling.
///      They look over at where the acorn was, look at each other, and each
///      goes home: one along the rail and up the left maple, the other up
///      the right.
///
/// FEYNMAN VERSION: this is not a clockwork of states like the turkeys. It is
/// a SCRIPT. When a cycle begins, dice choose the burying spot and the
/// pauses, and Begin writes out every actor's whole part as a list of
/// "segments" (hop from here to there, run along the rail, dig for so many
/// seconds), each with its start and end time on the cycle's clock. Drawing
/// a frame is then only: find the segment whose time it is, and work out
/// where that actor is within it from the clock alone, exactly as a
/// happening does. No change of state happens in one frame: a hop has its
/// crouch, push, flight, landing and settle; a turn goes round through a
/// front view; a climb begins with the body pitching up onto the bark; a
/// run eases into a sit through rising pages. The pictures are the shared
/// squirrel art (SquirrelArt.cs); the mouse and the dust are painted here.
///
/// It holds the director's "ground", "fence" and "cornshock" claims while it
/// plays (HappeningDirector.SceneClaims), so no turkey strolls through the
/// scuffle and no second squirrel runs the rail at the same time, and it
/// never begins while a happening holding one of those is on.
///
/// What it is solid to: the ground (WalkY), the rail (the painter's own
/// rail curve) and the trunks. On the rail and on the trunks it is stamped
/// through the OpenOfProps stencil, so it passes behind the corn shock and
/// into the maples' leaves; on the ground it is in front of everything.
/// </summary>
internal sealed class AcornCaper
{
    private enum Kind { Peek, Hop, Turn, Run, Rise, Climb, Mount, Sit, Chatter, Dazed, Dig, Mouse, Fracas }

    /// <summary>One piece of one actor's part: what it does between T0 and T1, and where.</summary>
    private sealed class Seg
    {
        public Kind Kind;
        public float T0, T1;
        public PointF From, To;             // the feet, in pixels (for Fracas: the cloud's centre on the ground)
        public int Dir = 1;                 // +1 faces right, -1 left (for Turn: the direction it ends facing)
        public bool Acorn, Look, Rail;      // carrying the nut; head up; on the rail or a trunk (the props stencil applies)
        public bool Up;                     // a climb with the head up (false: head first down); a mount that goes up onto the bark (false: comes off it)
        public float P0, P1;                // extras, by kind: Peek: how far out, from and to. Hop: arc height in squirrel units, P1 1 = starts from a run, 2 = ends in a run, 3 = both.
                                            // Run: the bound length (pixels). Sit: 0 calm, 1 with the head wobbling (unused). Dig: scrabbles a second (0 = just sniffing), P1 = 1 with earth flying.
                                            // Fracas: fade in and fade out seconds. Rise: P0 1 = rising to sit, -1 = sinking from a sit to the crouch. Chatter: P0 = which squirrel (0 or 1).
        public float Dur => T1 - T0;
    }

    private struct Sheet
    {
        public Sprite[] Pages;
        public int W, H, Ox, By;            // the sheet, and where the feet's origin is on it
    }

    private struct Pop { public float T; public int Kind; public float Angle, Dur; }

    private const int RunPages = 16, HopPages = 10, TurnPages = 3, RisePages = 5, DigPages = 8, MousePages = 8, CloudPages = 12, PeekPages = 4, AirPages = 8, MountPages = 4;
    private const float Speed = 0.27f;                  // bounding, U per second (the fence squirrel's own pace)
    private const float ClimbSpeed = 0.34f;             // up a trunk: faster than on the ground, in body lengths a second
    private const float MouseSpeed = 0.19f;
    private const float HopFor = 0.36f, TurnFor = 0.2f, RiseFor = 0.22f, MountFor = 0.12f;
    private const string Claims = "ground,fence,cornshock";

    private readonly ThanksgivingScenery _s;
    private readonly HappeningDirector _director;
    private readonly Random _rng;
    private readonly float _k, _m;                      // the squirrel unit and the mouse unit, in pixels
    private Sheet[] _run = new Sheet[2];                // [acorn]
    private Sheet[] _hop = new Sheet[2];
    private Sheet[] _up = new Sheet[2];                 // climbing, head up: [acorn]
    private Sheet _down;                                // climbing, head first down
    private Sheet[] _mount = new Sheet[2];              // the body pitching up onto the bark: [acorn]
    private Sheet _turn;
    private Sheet[] _rise = new Sheet[2];
    private Sheet[,] _sit = new Sheet[2, 4];            // [acorn, tail flick pose]
    private Sheet[] _lookUp = new Sheet[2];
    private Sheet[] _dig = new Sheet[2];
    private Sheet[,] _chatter = new Sheet[4, 2];        // [tail snap, paw]
    private Sheet _dazed;                               // 3 pages
    private Sheet _peek, _mouse, _mouseNut, _mouseStill, _mouseStillNut, _cloud, _air;
    private Sprite[] _limb = new Sprite[4];             // a paw, a hind foot, an ear, a tail tip, for the scuffle
    private Sprite _acorn = null!, _mound = null!, _hole = null!, _speck = null!, _tuft = null!, _star = null!;
    private volatile bool _ready;

    private readonly List<Seg>[] _tracks = [new(), new(), new(), new()];   // 0 the first squirrel, 1 the thief, 2 the mouse, 3 the dust cloud
    private readonly List<Pop> _pops = new();
    private bool _active;
    private double _time, _next;
    private float _t, _end;
    private float _pileX, _pileY, _pileTaken, _spotX, _buryAt, _digUpAt, _flingAt, _flingX, _landX, _grabAt;

    public AcornCaper(ThanksgivingScenery s, HappeningDirector director, Random sceneRng)
    {
        _s = s;
        _director = director;
        _rng = new Random(sceneRng.Next());             // its own dice, so the rest of the scene never reshuffles
        _k = Math.Max(9f, s.U * 0.055f);
        _m = _k * 0.55f;                                // a mouse is small; a child still has to see it from the sofa
        string? ask = Environment.GetEnvironmentVariable("PECKWORKS_CAPER");         // for test renders: "1" the first cycle at once and short rests, "0" never
        _next = ask == "1" ? 1.0 : ask == "0" ? double.MaxValue : 8 + 14 * _rng.NextDouble();
        // The pictures take a moment to paint (a couple of hundred pages); done on a side thread so the scene starts at once.
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    // ================================================================ painting (once)

    private static Sheet MakeSheet(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
    {
        var sh = new Sheet { Pages = new Sprite[n], W = w, H = h, Ox = ox, By = by };
        for (int i = 0; i < n; i++) sh.Pages[i] = page(i);
        return sh;
    }

    private void Build()
    {
        float k = _k;
        int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
        float bound = 2.2f;                              // the bound length in squirrel units: the pages' feet slide by it
        Sheet Ground(int n, Func<int, SquirrelArt.Pose> pose) => MakeSheet(n, w, h, ox, by, i => SquirrelArt.Page(pose(i), k, w, h, ox, by));
        int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
        int sq = (int)(4.0f * k) + 6, so = sq / 2;       // a square sheet with the origin at its centre: fits the body at any turn
        for (int a = 0; a < 2; a++)
        {
            int nut = a;
            _run[a] = Ground(RunPages, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound); q.Acorn = nut; return q; });
            _hop[a] = Ground(HopPages, i => { var q = SquirrelArt.Hop((i + 0.5f) / HopPages); q.Acorn = nut == 1 ? (q.PawsUp ? 2 : 1) : 0; return q; });
            // Climbing with the head up: the bound turned a quarter turn about the feet, so forward is up the screen and the back is to the left of the feet; clinging.
            _up[a] = MakeSheet(RunPages, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound, cling: true); q.Acorn = nut; q.Rotate = -90f; return SquirrelArt.Page(q, k, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3); });
            _mount[a] = MakeSheet(MountPages, sq, sq, so, so, i => { var q = SquirrelArt.Bound(0.80f, bound); q.Acorn = nut; q.Rotate = -90f * (i + 1) / MountPages; return SquirrelArt.Page(q, k, sq, sq, so, so); });
            _rise[a] = Ground(RisePages, i => { var q = SquirrelArt.Sit(0.2f + 0.8f * i / (RisePages - 1), 0, false, nut == 1); q.Acorn = nut == 1 ? (q.PawsUp ? 2 : 1) : 0; return q; });
            for (int tail = 0; tail < 4; tail++)
            {
                int tl = tail;
                _sit[a, tail] = Ground(1, _ => { var q = SquirrelArt.Sit(1f, tl, false, nut == 1); q.Acorn = nut == 1 ? 2 : 0; return q; });
            }
            _lookUp[a] = Ground(1, _ => { var q = SquirrelArt.Sit(1f, 0, true, nut == 1); q.Acorn = nut == 1 ? 2 : 0; return q; });
            _dig[a] = Ground(DigPages, i => { var q = SquirrelArt.Dig(i / (float)DigPages); q.Acorn = nut; return q; });
        }
        _down = MakeSheet(RunPages, cw, ch, 3, (int)(1.75f * k) + 3, i => { var q = SquirrelArt.Bound(i / (float)RunPages, bound, cling: true); q.Rotate = 90f; return SquirrelArt.Page(q, k, cw, ch, 3, (int)(1.75f * k) + 3); });
        _turn = Ground(TurnPages, i => SquirrelArt.Turn(i * 0.25f));
        for (int snap = 0; snap < 4; snap++)
            for (int paw = 0; paw < 2; paw++)
            {
                int sn = snap, pw = paw;
                _chatter[snap, paw] = Ground(1, _ => SquirrelArt.Chatter(sn, pw));
            }
        _dazed = Ground(3, i => SquirrelArt.Dazed((i - 1) * 5f));
        {
            int pw2 = (int)(0.9f * k) + 4, ph = (int)(0.8f * k) + 4, pox = pw2 / 2, pby = (int)(0.72f * k) + 2;
            _peek = MakeSheet(PeekPages + 1, pw2, ph, pox, pby, i => Squirrel.PaintPeek(k, pw2, ph, pox, pby, i < PeekPages ? (i + 1) / (float)PeekPages : 1f, i == PeekPages));
        }

        // The nuts on the ground are painted a touch bigger than the one in a mouth, so a child can see what the fuss is about.
        _acorn = Squirrel.PaintAcorn(k * 1.6f, 90f);
        _air = MakeSheet(AirPages, 1, 1, 0, 0, i => Squirrel.PaintAcorn(k * 1.6f, 360f * i / AirPages));

        int mw = (int)(1.9f * _m) + 4, mh = (int)(0.95f * _m) + 4, mox = (int)(1.25f * _m) + 2, mby = mh - 2;
        _mouse = MakeSheet(MousePages, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, i / (float)MousePages, false, 0));
        _mouseNut = MakeSheet(MousePages, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, i / (float)MousePages, true, 0));
        _mouseStill = MakeSheet(2, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, 0f, false, i + 1));
        _mouseStillNut = MakeSheet(2, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, 0f, true, i + 1));

        int fw = (int)(3.0f * k) + 4, fh = (int)(2.4f * k) + 4;
        _cloud = MakeSheet(CloudPages, fw, fh, fw / 2, fh - 2, i => PaintCloud(fw, fh, i / (float)CloudPages));
        int lw = (int)(0.9f * k) + 4;
        for (int i = 0; i < 4; i++) { int kind = i; _limb[i] = Sprite.Paint(lw, lw, g => PaintLimb(g, lw, kind)); }
        int tw = Math.Max(4, (int)(k * 0.12f));
        _tuft = Sprite.Paint(tw, tw, g => { using var b = new SolidBrush(SquirrelArt.TailHair); g.FillEllipse(b, 0.5f, tw * 0.25f, tw - 1f, tw * 0.5f); });
        int stw = Math.Max(6, (int)(k * 0.16f));
        _star = Sprite.Paint(stw, stw, g =>
        {
            using var b = new SolidBrush(Color.FromArgb(250, 236, 180));
            float c = stw / 2f, r = stw / 2f - 0.5f, r2 = r * 0.35f;
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++) { float a = i * MathF.PI / 4 - MathF.PI / 2, rr = i % 2 == 0 ? r : r2; pts[i] = new PointF(c + rr * MathF.Cos(a), c + rr * MathF.Sin(a)); }
            g.FillPolygon(b, pts);
        });

        int gw = (int)(0.7f * k) + 4, gh = (int)(0.3f * k) + 4;
        _mound = Sprite.Paint(gw, gh, g =>
        {
            // A low heap of fresh earth, darker than the grass, a lighter crumb or two on top.
            using var earth = new SolidBrush(Color.FromArgb(104, 66, 34));
            g.FillEllipse(earth, 2, gh * 0.30f, gw - 4, gh * 0.75f);
            using var crumb = new SolidBrush(Color.FromArgb(150, 104, 58));
            g.FillEllipse(crumb, gw * 0.30f, gh * 0.30f, gw * 0.28f, gh * 0.28f);
            g.FillEllipse(crumb, gw * 0.55f, gh * 0.42f, gw * 0.18f, gh * 0.22f);
        });
        _hole = Sprite.Paint(gw, gh, g =>
        {
            // The hole after the thief: a dark oval with the dug earth scattered round its rim.
            using var rim = new SolidBrush(Color.FromArgb(118, 76, 40));
            g.FillEllipse(rim, 1, gh * 0.25f, gw - 2, gh * 0.75f);
            using var dark = new SolidBrush(Color.FromArgb(38, 22, 12));
            g.FillEllipse(dark, gw * 0.22f, gh * 0.42f, gw * 0.56f, gh * 0.40f);
        });
        int sp = Math.Max(4, (int)(k * 0.09f));
        _speck = Sprite.Paint(sp, sp, g => { using var b = new SolidBrush(Color.FromArgb(92, 56, 28)); g.FillEllipse(b, 0.5f, 0.5f, sp - 1f, sp - 1f); });
    }

    /// <summary>
    /// A small warm-grey field mouse scurrying to the right (phase 0 to 1 of a stride): a plump body that bobs and
    /// stretches a little with each stride, a big pink-lined ear, a pink nose, a long tapered tail that lags the body,
    /// and four quick little legs. "twitch" 1 or 2 is the standing mouse with its nose up and down (sniffing).
    /// </summary>
    private Sprite PaintMouse(int w, int h, int ox, int by, float phase, bool nut, int twitch) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(_m, -_m);                  // mouse units (its body length), y UP from the ground
            Color fur = Color.FromArgb(150, 126, 102), dark = Color.FromArgb(104, 84, 68), belly = Color.FromArgb(226, 206, 180), pink = Color.FromArgb(226, 156, 150), rim = Color.FromArgb(80, 60, 52);
            bool still = twitch > 0;
            float run = still ? 0f : MathF.Sin(phase * MathF.Tau);
            float bob = still ? 0f : 0.04f * MathF.Max(0f, MathF.Sin(phase * MathF.Tau * 2)), stretch = still ? 1f : 1f + 0.05f * MathF.Sin(phase * MathF.Tau);
            float lag = still ? 0f : MathF.Sin(phase * MathF.Tau - 1.1f);  // the tail wave three frames behind the body
            // The tail: a tapered closed shape (not a pen line) that waves as it runs.
            {
                PointF[] c = new PointF[7];
                for (int i = 0; i < 7; i++) { float s = i / 6f; c[i] = new PointF(-0.36f - 0.90f * s, 0.20f + bob + 0.10f * MathF.Sin(s * 4.5f + lag * 1.2f) * s); }
                var left = new PointF[7]; var right = new PointF[7];
                for (int i = 0; i < 7; i++)
                {
                    PointF a = c[Math.Max(0, i - 1)], b = c[Math.Min(6, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, hw = 0.035f * (1 - i / 6f) + 0.006f;
                    left[i] = new PointF(c[i].X - dy / l * hw, c[i].Y + dx / l * hw); right[i] = new PointF(c[i].X + dy / l * hw, c[i].Y - dx / l * hw);
                }
                var outline = new PointF[14];
                for (int i = 0; i < 7; i++) { outline[i] = left[i]; outline[13 - i] = right[i]; }
                using var tb = new SolidBrush(dark);
                g.FillPolygon(tb, outline);
            }
            // The legs: two pairs, scurrying out of step.
            using (var leg = new Pen(dark, 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(leg, 0.22f, 0.16f + bob, 0.22f + 0.14f * run, 0.0f);
                g.DrawLine(leg, -0.22f, 0.16f + bob, -0.22f - 0.14f * run, 0.0f);
                g.DrawLine(leg, 0.30f, 0.16f + bob, 0.30f - 0.14f * run, 0.02f);
                g.DrawLine(leg, -0.14f, 0.16f + bob, -0.14f + 0.14f * run, 0.02f);
            }
            using (var body = new SolidBrush(fur)) g.FillEllipse(body, -0.42f * stretch, 0.04f + bob, 0.84f * stretch, 0.40f);
            using (var bl = new SolidBrush(belly)) g.FillEllipse(bl, -0.30f * stretch, 0.04f + bob, 0.56f * stretch, 0.18f);
            float nose = still ? (twitch == 2 ? 0.03f : 0f) : 0f;         // the nose twitches up and down when it sniffs
            using (var head = new SolidBrush(fur)) g.FillEllipse(head, 0.26f, 0.12f + bob + nose, 0.36f, 0.32f);
            using (var earRim = new SolidBrush(rim)) g.FillEllipse(earRim, 0.24f, 0.33f + bob + nose, 0.26f, 0.26f);
            using (var ear = new SolidBrush(fur)) g.FillEllipse(ear, 0.26f, 0.35f + bob + nose, 0.22f, 0.22f);
            using (var inner = new SolidBrush(pink)) g.FillEllipse(inner, 0.30f, 0.38f + bob + nose, 0.14f, 0.15f);
            using (var eye = new SolidBrush(Color.FromArgb(24, 18, 18))) g.FillEllipse(eye, 0.46f, 0.28f + bob + nose, 0.07f, 0.07f);
            using (var np = new SolidBrush(pink)) g.FillEllipse(np, 0.59f, 0.22f + bob + nose, 0.07f, 0.06f);
            using (var wh = new Pen(Color.FromArgb(140, 230, 226, 230), 0.015f))
                for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.58f, 0.24f + bob + nose, 0.80f, 0.20f + bob + nose + i * 0.07f);
            if (nut)
            {
                // The acorn, as big as its head, carried in its mouth (the same nut that lay on the ground).
                var st = g.Save();
                g.TranslateTransform(0.78f, 0.22f + bob + nose);
                g.RotateTransform(70f);
                float a = 0.165f;
                using (var b = new SolidBrush(Color.FromArgb(150, 98, 46))) g.FillEllipse(b, -a * 1.3f, -a, a * 2.6f, a * 2f);
                using (var cap = new SolidBrush(Color.FromArgb(84, 54, 28))) g.FillPie(cap, -a, -a * 1.2f, a * 2f, a * 2.4f, 70, 220);
                g.Restore(st);
            }
        });

    /// <summary>
    /// The scuffle at one moment (t 0 to 1 of a third of a second, which is one hop of the whole cloud): a dozen
    /// puffs of dust in a pale, cooler tone than the ground, each pulsing at its own rate and jittering, the whole
    /// ball hopping and squashing as it lands, with two bushy tails sticking out of it and a few bits of grass
    /// flying off. Twelve pages played at 36 a second boil instead of ticking round.
    /// </summary>
    private Sprite PaintCloud(int w, int h, float t) =>
        Sprite.Paint(w, h, g =>
        {
            float k = _k;
            float hop = 0.14f * MathF.Abs(MathF.Sin(MathF.PI * t));                       // the ball in the air
            float squash = 1f + 0.10f * MathF.Max(0f, 1f - MathF.Abs(MathF.Sin(MathF.PI * t)) * 6f);   // flattened just at the landing
            g.TranslateTransform(w / 2f, h - 2 - (0.62f + hop) * k);
            PointF Polar(float deg, float r) { float a = deg * MathF.PI / 180f; return new PointF(r * k * MathF.Cos(a) * squash, r * k * MathF.Sin(a) / squash); }
            // The tails first, behind the dust: bushy flames with scalloped edges, rotating with the roll.
            foreach (float deg in (float[])[25f + 360f * t, 205f + 360f * t])
            {
                PointF a = Polar(deg, 0.30f), b = Polar(deg + 12f, 0.72f), c = Polar(deg - 18f, 1.0f);
                using var hair = new Pen(SquirrelArt.TailHair, k * 0.20f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var core = new Pen(SquirrelArt.TailCore, k * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawBezier(hair, a, b, Polar(deg - 5f, 0.9f), c);
                g.DrawBezier(core, a, b, Polar(deg - 5f, 0.9f), c);
                for (int i = 0; i < 5; i++)
                {
                    PointF p = Polar(deg + 12f - 30f * (i / 4f), 0.5f + 0.5f * (i / 4f));
                    using var tuft = new SolidBrush(i % 2 == 0 ? SquirrelArt.TailHair : SquirrelArt.TailCore);
                    g.FillEllipse(tuft, p.X - k * 0.07f, p.Y - k * 0.07f, k * 0.14f, k * 0.14f);
                }
            }
            // The dust: a dozen puffs, each with its own pulse (six or nine beats a second, so the loop closes) and jitter.
            (float Deg, float R, float S, float F, float Ph)[] puffs =
            [
                (0, 0, 0.46f, 6, 0.0f), (30, 0.34f, 0.30f, 9, 0.2f), (75, 0.40f, 0.26f, 6, 0.5f), (120, 0.36f, 0.30f, 9, 0.8f), (165, 0.42f, 0.26f, 6, 0.1f),
                (210, 0.36f, 0.32f, 9, 0.4f), (250, 0.40f, 0.26f, 6, 0.7f), (290, 0.34f, 0.30f, 9, 0.9f), (330, 0.40f, 0.28f, 6, 0.3f),
                (55, 0.62f, 0.18f, 9, 0.6f), (185, 0.62f, 0.18f, 6, 0.25f), (300, 0.60f, 0.17f, 9, 0.85f),
            ];
            foreach (var p in puffs)
            {
                float pulse = 1f + 0.2f * MathF.Sin(MathF.Tau * (p.F / 3f * t + p.Ph));
                float jx = 0.08f * MathF.Sin(MathF.Tau * (2 * t + p.Ph * 3)), jy = 0.06f * MathF.Cos(MathF.Tau * (2 * t + p.Ph * 5));
                PointF c = Polar(p.Deg, p.R);
                c = new PointF(c.X + jx * k, c.Y + jy * k);
                float s = p.S * pulse * k;
                using var lo = new SolidBrush(Color.FromArgb(236, 214, 188, 150));
                g.FillEllipse(lo, c.X - s, c.Y - s * 0.9f, s * 2, s * 1.9f);
                using var hi = new SolidBrush(Color.FromArgb(220, 240, 224, 196));
                g.FillEllipse(hi, c.X - s * 0.8f, c.Y - s * 1.0f, s * 1.6f, s * 1.3f);
            }
            // Bits of grass flung out, further round than the dust, turning the other way.
            using (var grass = new Pen(Color.FromArgb(214, 166, 84), Math.Max(1f, k * 0.025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float deg in (float[])[65f, 160f, 275f, 330f])
                {
                    PointF a = Polar(deg - 200f * t, 1.0f), b = Polar(deg - 200f * t + 8f, 1.2f);
                    g.DrawLine(grass, a, b);
                }
        });

    /// <summary>A bit of squirrel that pops out of the dust: 0 a paw with spread fingers, 1 a long hind foot, 2 an ear, 3 a tail tip. Painted pointing right from the sheet's centre.</summary>
    private void PaintLimb(Graphics g, int size, int kind)
    {
        float k = _k;
        g.TranslateTransform(size / 2f, size / 2f);
        g.ScaleTransform(k, -k);
        using var limb = new SolidBrush(SquirrelArt.LimbNear);
        using var fur = new SolidBrush(SquirrelArt.Fur);
        switch (kind)
        {
            case 0:
                g.FillEllipse(limb, 0f, -0.04f, 0.26f, 0.08f);
                g.FillEllipse(limb, 0.22f, -0.06f, 0.10f, 0.12f);
                using (var finger = new Pen(SquirrelArt.LimbNear, 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    foreach (float a in (float[])[-0.5f, -0.17f, 0.17f, 0.5f]) g.DrawLine(finger, 0.30f, 0f, 0.30f + 0.10f * MathF.Cos(a), 0.10f * MathF.Sin(a));
                break;
            case 1:
                g.FillEllipse(limb, 0f, -0.045f, 0.24f, 0.09f);
                g.FillEllipse(limb, 0.20f, -0.035f, 0.22f, 0.07f);
                break;
            case 2:
                g.FillEllipse(fur, 0.02f, -0.07f, 0.16f, 0.14f);
                using (var inner = new SolidBrush(Color.FromArgb(190, 140, 120))) g.FillEllipse(inner, 0.07f, -0.04f, 0.09f, 0.08f);
                using (var tuft = new SolidBrush(SquirrelArt.Under)) g.FillEllipse(tuft, 0.16f, -0.03f, 0.06f, 0.06f);
                break;
            default:
                using (var hair = new Pen(SquirrelArt.TailHair, 0.16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(hair, new PointF(0, 0), new PointF(0.15f, 0.05f), new PointF(0.3f, 0.12f), new PointF(0.42f, 0.06f));
                using (var core = new Pen(SquirrelArt.TailCore, 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(core, new PointF(0, 0), new PointF(0.15f, 0.05f), new PointF(0.3f, 0.12f), new PointF(0.42f, 0.06f));
                break;
        }
    }

    // ================================================================ the script

    private float WalkY(float x) => _s.WalkY(x);
    private float RailAt(float x) => Squirrel.RailAt(_s, x);

    private static Seg Add(List<Seg> track, Kind kind, float t0, float dur, PointF from, PointF to, int dir, bool acorn = false, bool rail = false, float p0 = 0f, float p1 = 0f, bool look = false, bool up = false)
    {
        var seg = new Seg { Kind = kind, T0 = t0, T1 = t0 + dur, From = from, To = to, Dir = dir, Acorn = acorn, Rail = rail, P0 = p0, P1 = p1, Look = look, Up = up };
        track.Add(seg);
        return seg;
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>A bounding run from "from" to "to" (the feet), the bound length squeezed so a whole number of bounds fit. Returns when it ends.</summary>
    private float Run(List<Seg> track, float t0, PointF from, PointF to, bool acorn, bool rail)
    {
        float dist = MathF.Abs(to.X - from.X);
        int bounds = Math.Max(1, (int)MathF.Round(dist / (2.2f * _k)));
        float dur = dist / (Speed * _s.U);
        Add(track, Kind.Run, t0, dur, from, to, to.X >= from.X ? 1 : -1, acorn, rail, dist / bounds);
        return t0 + dur;
    }

    private float Hop(List<Seg> track, float t0, PointF from, PointF to, int dir, bool acorn, float height = 0.55f)
    {
        Add(track, Kind.Hop, t0, HopFor, from, to, dir, acorn, p0: height);
        return t0 + HopFor;
    }

    private static float Turn(List<Seg> track, float t0, PointF at, int toDir, bool acorn)
    {
        Add(track, Kind.Turn, t0, TurnFor, at, at, toDir, acorn);
        return t0 + TurnFor;
    }

    /// <summary>Rising from the run's crouch to sitting up (P0 1), or sinking back down (P0 -1).</summary>
    private static float Rise(List<Seg> track, float t0, PointF at, int dir, bool acorn, int way = 1)
    {
        Add(track, Kind.Rise, t0, RiseFor, at, at, dir, acorn, p0: way);
        return t0 + RiseFor;
    }

    private float Climb(List<Seg> track, float t0, PointF from, PointF to, bool up, bool acorn)
    {
        float dur = Dist(from, to) / (ClimbSpeed * _s.U);
        Add(track, Kind.Climb, t0, dur, from, to, 1, acorn, rail: true, up: up);
        return t0 + dur;
    }

    /// <summary>Pitching the body up onto the bark (up true) at the foot of a climb, or coming off it onto the rail (up false).</summary>
    private static float Mount(List<Seg> track, float t0, PointF at, int dir, bool acorn, bool up)
    {
        Add(track, Kind.Mount, t0, MountFor, at, at, dir, acorn, rail: false, up: up);
        return t0 + MountFor;
    }

    private static float Sit(List<Seg> track, float t0, float dur, PointF at, int dir, bool acorn, bool look = false)
    {
        Add(track, Kind.Sit, t0, dur, at, at, dir, acorn, look: look);
        return t0 + dur;
    }

    private static float Chatter(List<Seg> track, float t0, float dur, PointF at, int dir, bool acorn, int who)
    {
        Add(track, Kind.Chatter, t0, dur, at, at, dir, acorn, p0: who);
        return t0 + dur;
    }

    private static float Dig(List<Seg> track, float t0, float dur, PointF at, int dir, float rate, bool earth, bool acorn = false, bool rail = false)
    {
        Add(track, Kind.Dig, t0, dur, at, at, dir, acorn, rail, p0: rate, p1: earth ? 1f : 0f);
        return t0 + dur;
    }

    /// <summary>The point on a trunk's line (foot to fork) at height y, set just outside the bark: where a squirrel's feet go.</summary>
    private PointF OnTrunk(PointF[] trunk, float y)
    {
        float f = Math.Clamp((trunk[0].Y - y) / (trunk[0].Y - trunk[1].Y), 0f, 1f);
        return new PointF(trunk[0].X + (trunk[1].X - trunk[0].X) * f - _s.U * 0.0065f, y);
    }

    /// <summary>Rolls this cycle's dice and writes out everybody's part.</summary>
    private void Begin()
    {
        foreach (var track in _tracks) track.Clear();
        _pops.Clear();
        float k = _k, u = _s.U;
        List<Seg> A = _tracks[0], B = _tracks[1], M = _tracks[2], C = _tracks[3];
        PointF[] lt = _s.LeftTrunk, rt = _s.RightTrunk;
        float hx = _s.Hollow.At.X;
        float lRail = RailAt(lt[0].X), rRail = RailAt(rt[0].X);
        PointF lTrunkAtRail = OnTrunk(lt, lRail - 1.5f), rTrunkAtRail = OnTrunk(rt, rRail - 1.5f);
        PointF lFork = new(lt[1].X - u * 0.0065f, lt[1].Y), rFork = new(rt[1].X - u * 0.0065f, rt[1].Y);
        PointF lTop = new(lt[2].X - u * 0.0065f, lt[2].Y), rTop = new(rt[2].X - u * 0.0065f, rt[2].Y);
        PointF OnRail(float x) => new(x, RailAt(x) - 1.5f);
        PointF OnGround(float x) => new(x, WalkY(x));

        // The burying spot: in the open stretch between the cornucopia and the corn shock, clear of both.
        float shockEdge = _s.CornShockFoot.X - _s.CornShockHalfWidth - 2.4f * k;
        float lo = Math.Max(_s.CornucopiaMouth.X + 3.0f * k, _s.Width * 0.30f), hi = Math.Min(_s.Width * 0.46f, shockEdge);   // the face-off spreads 1.6 squirrels to the left of the spot: keep it off the fruit
        _spotX = hi > lo ? lo + (hi - lo) * (float)_rng.NextDouble() : (lo + hi) / 2;   // (a screen too narrow for the open stretch: the middle of what there is)
        _pileX = hx - 1.7f * k;
        _pileY = WalkY(_pileX);

        // ---- Act 1: out of the hollow, a nut, bury it, home up the tree ----
        float t = 0f;
        Add(A, Kind.Peek, t, 0.7f, _s.Hollow.At, _s.Hollow.At, -1, p0: 0f, p1: 1f); t += 0.7f;
        Add(A, Kind.Peek, t, 1.1f + 0.6f * (float)_rng.NextDouble(), _s.Hollow.At, _s.Hollow.At, -1, p0: 1f, p1: 1f); t = A[^1].T1;
        t = Hop(A, t, new PointF(hx - 0.15f * k, _s.Hollow.At.Y + 0.2f * k), OnGround(hx - 0.95f * k), -1, false, 0.5f);
        PointF atPile = OnGround(_pileX + 0.78f * k);                                    // the paws reach the pile's near edge, the nuts in view past its nose
        t = Hop(A, t, OnGround(hx - 0.95f * k), atPile, -1, false, 0.35f);
        t = Rise(A, t, atPile, -1, false, -1);                                           // down onto all fours
        t = Dig(A, t, 0.9f, atPile, -1, 1.6f, false);                                    // nosing through the pile
        _pileTaken = t;
        t = Rise(A, t, atPile, -1, true);
        t = Sit(A, t, 0.5f, atPile, -1, true, look: true);                               // got one
        t = Turn(A, t, atPile, 1, true);                                                 // round to face the tree
        t = Hop(A, t, atPile, OnGround(lt[0].X - u * 0.0065f - 0.25f * k), 1, true, 0.3f);
        t = Mount(A, t, OnTrunk(lt, WalkY(lt[0].X)), 1, true, true);
        t = Climb(A, t, OnTrunk(lt, WalkY(lt[0].X)), lTrunkAtRail, true, true);
        t = Mount(A, t, lTrunkAtRail, 1, true, false);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, true, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 0.5f * k), true, true);
        t = Hop(A, t, OnRail(_spotX - 0.5f * k), OnGround(_spotX - 0.5f * k), 1, true, 0.2f);
        t = Rise(A, t, OnGround(_spotX - 0.5f * k), 1, true, -1);
        t = Dig(A, t, 1.9f, OnGround(_spotX - 0.5f * k), 1, 3.2f, true, acorn: true);
        _buryAt = t;
        t = Dig(A, t, 0.5f, OnGround(_spotX - 0.5f * k), 1, 0f, false);                 // the nut goes in
        t = Dig(A, t, 0.8f, OnGround(_spotX - 0.5f * k), 1, 1.4f, false);               // pats the earth down
        t = Rise(A, t, OnGround(_spotX - 0.5f * k), 1, false);
        t = Sit(A, t, 0.9f, OnGround(_spotX - 0.5f * k), 1, false, look: true);          // did anybody see?
        t = Turn(A, t, OnGround(_spotX - 0.5f * k), -1, false);
        t = Sit(A, t, 0.3f, OnGround(_spotX - 0.5f * k), -1, false);
        t = Hop(A, t, OnGround(_spotX - 0.5f * k), OnRail(_spotX - 1.3f * k), -1, false, 0.5f);
        t = Run(A, t, OnRail(_spotX - 1.3f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        t = Hop(A, t, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        t = Mount(A, t, lTrunkAtRail, -1, false, true);
        t = Climb(A, t, lTrunkAtRail, lFork, true, false);
        t = Climb(A, t, lFork, lTop, true, false);
        float aHome = t;

        // ---- Act 2: the thief ----
        t = aHome + 2.5f + 3f * (float)_rng.NextDouble();
        t = Climb(B, t, rTop, rFork, false, false);
        t = Climb(B, t, rFork, rTrunkAtRail, false, false);
        t = Hop(B, t, rTrunkAtRail, OnRail(rt[0].X - 1.0f * k), -1, false, 0.4f);
        float bx = rt[0].X - 1.0f * k;
        float sniff1 = bx - (bx - (_spotX + 0.5f * k)) * (0.30f + 0.15f * (float)_rng.NextDouble());
        float sniff2 = bx - (bx - (_spotX + 0.5f * k)) * (0.65f + 0.15f * (float)_rng.NextDouble());
        // Keep the sniffing stops out of the corn shock's shadow (it would sniff invisibly behind it).
        float shockL = _s.CornShockFoot.X - _s.CornShockHalfWidth - 1.4f * k, shockR = _s.CornShockFoot.X + _s.CornShockHalfWidth + 1.4f * k;
        if (sniff1 > shockL && sniff1 < shockR) sniff1 = shockR;
        if (sniff2 > shockL && sniff2 < shockR) sniff2 = shockL;
        t = Run(B, t, OnRail(bx), OnRail(sniff1), false, true);
        t = Dig(B, t, 0.6f, OnRail(sniff1), -1, 0f, false, rail: true);                   // nose down to the rail
        t = Run(B, t, OnRail(sniff1), OnRail(sniff2), false, true);
        t = Dig(B, t, 0.6f, OnRail(sniff2), -1, 0f, false, rail: true);
        t = Run(B, t, OnRail(sniff2), OnRail(_spotX + 0.5f * k), false, true);
        t = Hop(B, t, OnRail(_spotX + 0.5f * k), OnGround(_spotX + 0.5f * k), -1, false, 0.2f);
        t = Rise(B, t, OnGround(_spotX + 0.5f * k), -1, false, -1);
        t = Dig(B, t, 0.7f, OnGround(_spotX + 0.5f * k), -1, 0f, false);                  // sniffs the mound
        float bDigs = t;
        t = Dig(B, t, 1.0f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        _digUpAt = t;
        t = Dig(B, t, 0.8f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        t = Rise(B, t, OnGround(_spotX + 0.5f * k), -1, true);
        t = Sit(B, t, 1.2f, OnGround(_spotX + 0.5f * k), -1, true, look: true);           // found it!
        float bFound = t;

        // ---- Act 3: caught ----
        t = bDigs;
        t = Climb(A, t, lTop, lFork, false, false);
        Add(A, Kind.Climb, t, 0.7f, lFork, lFork, 1, rail: true); t += 0.7f;             // freezes: what is THAT?
        t = Climb(A, t, lFork, lTrunkAtRail, false, false);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, false, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 1.6f * k), false, true);
        t = Hop(A, t, OnRail(_spotX - 1.6f * k), OnGround(_spotX - 1.6f * k), 1, false, 0.2f);
        float faceOff = Math.Max(t, bFound);
        if (t < faceOff) Chatter(A, t, faceOff - t, OnGround(_spotX - 1.6f * k), 1, false, 0);
        if (bFound < faceOff) Chatter(B, bFound, faceOff - bFound, OnGround(_spotX + 0.5f * k), -1, true, 1);
        t = faceOff;
        Chatter(A, t, 1.8f, OnGround(_spotX - 1.6f * k), 1, false, 0);
        Chatter(B, t, 1.8f, OnGround(_spotX + 0.5f * k), -1, true, 1);
        t += 1.8f;
        // The lunge: both at each other; the dust bursts over them the moment they meet.
        float cloudX = _spotX - 0.55f * k;
        Add(A, Kind.Run, t, 0.22f, OnGround(_spotX - 1.6f * k), OnGround(cloudX - 0.2f * k), 1, p0: 1.4f * k, p1: 1f);
        Add(B, Kind.Run, t, 0.22f, OnGround(_spotX + 0.5f * k), OnGround(cloudX + 0.2f * k), -1, true, p0: 1.4f * k, p1: 1f);
        t += 0.22f;
        float fracasFor = 3.8f;
        Add(C, Kind.Fracas, t - 0.035f, fracasFor + 0.035f, OnGround(cloudX), OnGround(cloudX), 1, p0: 0.035f, p1: 0.45f);
        _flingAt = t + 0.8f;
        _flingX = cloudX;
        _landX = cloudX + 2.3f * k;
        // Bits of squirrel pop out of the dust every few frames: a paw, a foot, an ear, a tail tip, at a random angle each.
        for (float pt = t + 0.2f; pt < t + fracasFor - 0.6f; pt += (4 + _rng.Next(5)) / 60f)
            _pops.Add(new Pop { T = pt, Kind = _rng.Next(4), Angle = (float)(_rng.NextDouble() * 360), Dur = (3 + _rng.Next(4)) / 60f });

        // ---- Act 4: the mouse, in stop-start dashes ----
        float mx = _s.CornShockFoot.X;
        float m = t + 1.0f;
        PointF nutSpot = OnGround(_landX + 0.45f * _m);
        float[] stops = [0.45f, 0.75f];
        PointF from = OnGround(mx);
        foreach (float f in stops)
        {
            PointF to = OnGround(mx + (nutSpot.X - mx) * f);
            float trip = MathF.Abs(to.X - from.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, from, to, -1); m += trip;
            float freeze = (6 + _rng.Next(5)) / 60f;
            Add(M, Kind.Mouse, m, freeze, to, to, -1); m += freeze;                     // freezes, nose twitching
            from = to;
        }
        {
            float trip = MathF.Abs(nutSpot.X - from.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, from, nutSpot, -1); m += trip;
        }
        Add(M, Kind.Mouse, m, 0.4f, nutSpot, nutSpot, -1); m += 0.4f;                   // sniffs it
        _grabAt = m;
        {
            PointF half = OnGround((nutSpot.X + mx) / 2);
            float trip = MathF.Abs(half.X - nutSpot.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, nutSpot, half, 1, true); m += trip;
            Add(M, Kind.Mouse, m, 0.12f, half, half, 1, true); m += 0.12f;
            trip = MathF.Abs(mx - half.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, half, OnGround(mx), 1, true); m += trip;
        }

        // ---- Act 5: the dust settles over two dazed squirrels ----
        t += fracasFor;
        PointF aAfter = OnGround(cloudX - 0.8f * k), bAfter = OnGround(cloudX + 0.8f * k);
        Add(A, Kind.Dazed, t - 0.3f, 0.9f, aAfter, aAfter, 1); Add(B, Kind.Dazed, t - 0.3f, 0.9f, bAfter, bAfter, -1);
        t += 0.6f;
        Sit(A, t, 1.2f, aAfter, 1, false, look: true);                                     // where did it go?
        float tb = Turn(B, t, bAfter, 1, false);
        Sit(B, tb, 1.2f - TurnFor, bAfter, 1, false, look: true);
        t += 1.2f;
        tb = Turn(B, t, bAfter, -1, false);
        Sit(A, t, 1.0f, aAfter, 1, false);                                                  // a long look at each other
        Sit(B, tb, 1.3f - TurnFor, bAfter, -1, false);
        t += 1.0f;
        float ta = Turn(A, t, aAfter, -1, false);
        ta = Hop(A, ta, aAfter, OnRail(cloudX - 1.6f * k), -1, false, 0.5f);
        ta = Run(A, ta, OnRail(cloudX - 1.6f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        ta = Hop(A, ta, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        ta = Mount(A, ta, lTrunkAtRail, -1, false, true);
        ta = Climb(A, ta, lTrunkAtRail, lFork, true, false);
        ta = Climb(A, ta, lFork, lTop, true, false);
        tb = Turn(B, t + 0.3f, bAfter, 1, false);
        tb = Hop(B, tb, bAfter, OnRail(cloudX + 1.6f * k), 1, false, 0.5f);
        tb = Run(B, tb, OnRail(cloudX + 1.6f * k), OnRail(rt[0].X - 1.0f * k), false, true);
        tb = Hop(B, tb, OnRail(rt[0].X - 1.0f * k), rTrunkAtRail, 1, false, 0.4f);
        tb = Mount(B, tb, rTrunkAtRail, 1, false, true);
        tb = Climb(B, tb, rTrunkAtRail, rFork, true, false);
        tb = Climb(B, tb, rFork, rTop, true, false);
        _end = Math.Max(ta, tb) + 0.8f;

        // Hops that follow a run start from the push (no crouch); hops that lead into a run end at the landing (no settle).
        foreach (var track in _tracks)
            for (int i = 0; i < track.Count; i++)
            {
                if (track[i].Kind != Kind.Hop) continue;
                bool fromRun = i > 0 && track[i - 1].Kind is Kind.Run or Kind.Mount;
                bool toRun = i + 1 < track.Count && track[i + 1].Kind is Kind.Run or Kind.Mount;
                track[i].P1 = (fromRun ? 1 : 0) + (toRun ? 2 : 0);
            }

        _t = 0f;
        _active = true;
        _director.SceneClaims = Claims;

        // The script, written out for checking: PECKWORKS_CAPERLOG names a file and every segment of every part goes in it.
        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"cycle at {_time:F1}s: spot {_spotX:F0} pile {_pileX:F0} bury {_buryAt:F2} dugUp {_digUpAt:F2} fling {_flingAt:F2} land {_landX:F0} grab {_grabAt:F2} end {_end:F2}");
            string[] names = ["A", "B", "mouse", "cloud"];
            for (int i = 0; i < _tracks.Length; i++)
                foreach (Seg s in _tracks[i])
                    sw.WriteLine($"  {names[i],-5} {s.Kind,-7} {s.T0,6:F2} to {s.T1,6:F2}  ({s.From.X:F0},{s.From.Y:F0}) to ({s.To.X:F0},{s.To.Y:F0}) dir {s.Dir} acorn {s.Acorn} rail {s.Rail} p0 {s.P0:F2} p1 {s.P1:F2}");
        }
    }

    // ================================================================ living

    public void Update(float dt)
    {
        _time += dt;
        if (_active)
        {
            _t += dt;
            if (_t >= _end)
            {
                _active = false;
                _director.SceneClaims = null;
                bool quick = Environment.GetEnvironmentVariable("PECKWORKS_CAPER") == "1";
                _next = _time + (quick ? 3 : 35 + 50 * _rng.NextDouble());
            }
            return;
        }
        if (_ready && _time >= _next && !_director.Claimed(Claims)) Begin();
    }

    // ================================================================ drawing

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }

    /// <summary>
    /// Stamps a page with its feet at (feetX, feetY). Through the stencil if one is given (the rail and the trunks),
    /// and always against the near ground's depth picture: a pumpkin whose foot is lower on the screen than these
    /// feet is nearer, and hides the actor where they overlap.
    /// </summary>
    private void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, float feetX, float feetY, bool mirror, bool[]? stencil, float opacity = 1f)
    {
        int left = (int)MathF.Round(feetX) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(feetY) - sh.By, opacity, stencil, mirror, _s.NearDepth, _s.Nearness(feetY));
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_active || !_ready) return;
        float t = _t, k = _k;
        float over = Smooth((_end - t) / 0.6f);                   // everything the caper left on the ground fades at the very end

        // ---- the props: the nut pile, the mound or the hole, the nut that got away ----
        float pileIn = Smooth(t / 0.5f);
        byte[] nd = _s.NearDepth;
        byte pileNear = _s.Nearness(_pileY), spotNear = _s.Nearness(WalkY(_spotX));
        _acorn.DrawCentered(fb, _pileX - 0.16f * k, _pileY - 0.07f * k, pileIn * over, null, false, nd, pileNear);
        _acorn.DrawCentered(fb, _pileX + 0.14f * k, _pileY - 0.07f * k, pileIn * over, null, false, nd, pileNear);
        if (t < _pileTaken) _acorn.DrawCentered(fb, _pileX - 0.01f * k, _pileY - 0.18f * k, pileIn, null, false, nd, pileNear);   // the one on top is the one it takes
        if (t >= _buryAt + 0.3f && t < _digUpAt) _mound.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, Smooth((t - _buryAt - 0.3f) / 0.4f), null, false, nd, spotNear);
        if (t >= _digUpAt) _hole.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, over, null, false, nd, spotNear);
        if (t >= _buryAt && t < _buryAt + 0.5f)
        {
            // The nut leaves the mouth (at the nose, down by the paws) and is tipped into the hole.
            float p = (t - _buryAt) / 0.5f;
            float x = _spotX + 0.155f * k * (1 - p), y = WalkY(_spotX) - 0.03f * k - 0.12f * k * 4 * p * (1 - p);
            _air.Pages[(int)(p * 0.5f * AirPages) % AirPages].DrawCentered(fb, x, y, 1 - Smooth((p - 0.75f) / 0.25f));
        }
        if (t >= _flingAt && t < _grabAt)
        {
            // Flung out of the scuffle: up and over to the right, two bounces, rolls to a stop.
            float p = t - _flingAt;
            float x, y, ground = WalkY(_landX) - 0.06f * k;
            if (p < 0.6f) { float q = p / 0.6f; x = _flingX + (_landX - 0.5f * k - _flingX) * q; y = WalkY(_flingX) - 0.6f * k + (ground - WalkY(_flingX) + 0.6f * k) * q - 1.3f * k * 4 * q * (1 - q); }
            else if (p < 0.9f) { float q = (p - 0.6f) / 0.3f; x = _landX - 0.5f * k + 0.35f * k * q; y = ground - 0.35f * k * 4 * q * (1 - q); }
            else if (p < 1.1f) { float q = (p - 0.9f) / 0.2f; x = _landX - 0.15f * k + 0.15f * k * q; y = ground - 0.08f * k * 4 * q * (1 - q); }
            else { x = _landX; y = ground; }
            float turn = p < 1.1f ? p * 3.2f : 1.1f * 3.2f;
            _air.Pages[(int)(turn * AirPages) % AirPages].DrawCentered(fb, x, y);
        }

        // ---- the actors: the thief, the first squirrel, the mouse, the dust ----
        DrawTrack(fb, _tracks[1], t);
        DrawTrack(fb, _tracks[0], t);
        DrawTrack(fb, _tracks[2], t);
        DrawTrack(fb, _tracks[3], t);
    }

    private void DrawTrack(FrameBuffer fb, List<Seg> track, float t)
    {
        Seg? seg = null;
        foreach (Seg s in track)
            if (t >= s.T0 && t < s.T1) { seg = s; break; }
        if (seg == null) return;
        float k = _k, u = (t - seg.T0) / Math.Max(0.001f, seg.Dur);
        bool mirror = seg.Dir < 0;
        int nut = seg.Acorn ? 1 : 0;
        bool[]? props = _s.OpenOfProps;

        switch (seg.Kind)
        {
            case Kind.Peek:
            {
                float outBy = seg.P0 + (seg.P1 - seg.P0) * Smooth(u);
                bool blink = seg.P0 >= 1f && (t - seg.T0) % 1.3f < 0.12f;        // a blink once a second or so, while it is looking about
                int page = blink ? PeekPages : Math.Clamp((int)MathF.Ceiling(outBy * PeekPages) - 1, 0, PeekPages - 1);
                Stamp(fb, _peek, _peek.Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Hop:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.From.Y + (seg.To.Y - seg.From.Y) * u - seg.P0 * k * 4 * u * (1 - u);
                // The hop book: crouch, push, flight, landing, settle. After a run it starts at the push; before a run it ends at the landing.
                int flags = (int)seg.P1;
                float k0 = (flags & 1) != 0 ? 4 / 22f : 0f, k1 = (flags & 2) != 0 ? 18 / 22f : 1f;
                float kk = k0 + (k1 - k0) * u;
                int page = Math.Clamp((int)(kk * HopPages), 0, HopPages - 1);
                Stamp(fb, _hop[nut], _hop[nut].Pages[page], x, y, mirror, null);
                break;
            }
            case Kind.Turn:
            {
                // Round through the front view: the first half faces the way it was, the second half (mirrored pages) the way it will.
                int toDir = seg.Dir, fromDir = -seg.Dir;
                int page = u < 0.5f ? Math.Min(TurnPages - 1, (int)(u * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - u) * 2 * TurnPages));
                bool m = u < 0.5f ? fromDir < 0 : toDir < 0;
                Stamp(fb, _turn, _turn.Pages[page], seg.From.X, seg.From.Y, m, null);
                break;
            }
            case Kind.Rise:
            {
                float kk = seg.P0 > 0 ? u : 1 - u;
                int page = Math.Clamp((int)(kk * RisePages), 0, RisePages - 1);
                Stamp(fb, _rise[nut], _rise[nut].Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Run:
            {
                float dist = MathF.Abs(seg.To.X - seg.From.X) * u;
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.Rail ? RailAt(x) - 1.5f : WalkY(x);
                float cyc = dist / Math.Max(1f, seg.P0);
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                Stamp(fb, _run[nut], _run[nut].Pages[page], x, y, mirror, seg.Rail ? props : null);
                break;
            }
            case Kind.Mount:
            {
                // Pitching up onto the bark (or back off it): four pages from level to upright, about the feet.
                float kk = seg.Up ? u : 1 - u;
                int page = Math.Clamp((int)(kk * MountPages), 0, MountPages - 1);
                Stamp(fb, _mount[nut], _mount[nut].Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Climb:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                float cyc = Dist(seg.From, seg.To) * u / (1.6f * k);
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                // Head up: the back is to the left of the feet, so it clings to the trunk's left face as painted.
                // Head first down: the back is to the right as painted, so the sticker is flipped to put it on the same face.
                // Through the props stencil: the leaves hide it as it climbs into them, and uncover it as it comes down.
                if (seg.Up) Stamp(fb, _up[nut], _up[nut].Pages[page], x, y, false, props);
                else Stamp(fb, _down, _down.Pages[page], x, y, true, props);
                break;
            }
            case Kind.Sit:
            {
                float s = t - seg.T0;
                float ft = s % 1.5f;                                              // a flick every second and a half, as on the fence
                int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                Sheet sh = seg.Look ? _lookUp[nut] : _sit[nut, tail];
                Stamp(fb, sh, sh.Pages[0], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Chatter:
            {
                // Rage: the tail lashes in three-frame snaps with holds between, the forepaws stamp by turns, the body shudders a pixel at fifteen a second. The two squirrels are out of step.
                float s = t - seg.T0 + seg.P0 * 0.23f;
                int f = (int)(s * 60);
                int cyc = f % 14;
                int snap = cyc < 6 ? new[] { 1, 2, 3, 3, 2, 1 }[cyc] : 0;
                int paw = (f / 8) % 2;
                float shudder = MathF.Round(MathF.Sin(s * 15f * MathF.Tau));
                Sheet sh = _chatter[snap, paw];
                Stamp(fb, sh, sh.Pages[0], seg.From.X + shudder, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Dazed:
            {
                float s = t - seg.T0;
                int page = (int)MathF.Round(MathF.Sin(s * 3f * MathF.Tau)) + 1;
                Stamp(fb, _dazed, _dazed.Pages[page], seg.From.X, seg.From.Y, mirror, null);
                break;
            }
            case Kind.Dig:
            {
                float s = t - seg.T0;
                int page = seg.P0 <= 0f ? 0 : (int)(s * seg.P0 * DigPages) % DigPages;
                Stamp(fb, _dig[nut], _dig[nut].Pages[page], seg.From.X, seg.From.Y, mirror, seg.Rail ? props : null);
                if (seg.P1 > 0f)
                {
                    // Earth flying: a fountain of specks from the paws, back past the tail, each on its own little arc.
                    float px = seg.From.X + seg.Dir * 0.5f * k, py = seg.From.Y;
                    for (int i = 0; i < 14; i++)
                    {
                        float p = (s * seg.P0 * 0.4f + i / 14f) % 1f;
                        float reach = (1.3f + 0.35f * (i % 4)) * k, lift = (0.9f + 0.2f * (i % 3)) * k;
                        float x = px - seg.Dir * reach * p, y = py - lift * 4 * p * (1 - p) + 0.02f * k;
                        _speck.DrawCentered(fb, x, y, 1 - Smooth((p - 0.8f) / 0.2f));
                    }
                }
                break;
            }
            case Kind.Mouse:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = WalkY(x);
                bool still = seg.From == seg.To;
                float s = t - seg.T0;
                Sheet sh;
                int page;
                if (still) { sh = seg.Acorn ? _mouseStillNut : _mouseStill; page = (int)(s * 12) % 2; }     // the nose twitching
                else
                {
                    float dist = MathF.Abs(seg.To.X - seg.From.X) * u;
                    float stride = 0.8f * _m;                                                              // paged by distance: about eight strides a second at its pace
                    float cyc = dist / stride;
                    sh = seg.Acorn ? _mouseNut : _mouse; page = (int)((cyc - MathF.Floor(cyc)) * MousePages) % MousePages;
                }
                Stamp(fb, sh, sh.Pages[page], x, y, mirror, null);        // the depth picture hides it behind the shock (and any nearer pumpkin)
                break;
            }
            case Kind.Fracas:
            {
                float s = t - seg.T0;
                float opacity = Smooth(s / seg.P0) * Smooth((seg.Dur - s) / seg.P1);
                float spread = 1f + 0.5f * Smooth(1 - (seg.Dur - s) / seg.P1);                    // the cloud spreads as it thins
                float x = seg.From.X + 0.3f * k * MathF.Sin(s * 6.1f), y = seg.From.Y;
                int page = (int)(s * 36) % CloudPages;
                Stamp(fb, _cloud, _cloud.Pages[page], x, y, false, null, opacity / spread);
                // Bits of squirrel popping out, and tufts of fur and stars flying off on arcs.
                float cx = x, cy = y - 0.62f * k;
                foreach (Pop pop in _pops)
                {
                    if (t < pop.T || t >= pop.T + pop.Dur) continue;
                    float a = pop.Angle * MathF.PI / 180f;
                    _limb[pop.Kind].DrawCentered(fb, cx + 0.55f * k * MathF.Cos(a), cy - 0.45f * k * MathF.Sin(a), opacity);
                }
                for (int i = 0; i < 6; i++)
                {
                    float p = (s * 1.6f + i / 6f) % 1f;
                    float a = (i * 61f + 20f) * MathF.PI / 180f;
                    _tuft.DrawCentered(fb, cx + (0.6f + 1.2f * p) * k * MathF.Cos(a), cy - (0.5f + 0.9f * p) * k * MathF.Sin(a) + 1.4f * k * p * p, opacity * (1 - p));
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = (s * 1.3f + i * 2.09f);
                    float twinkle = 0.5f + 0.5f * MathF.Sin(s * 11f + i * 2f);
                    _star.DrawCentered(fb, cx + 1.0f * k * MathF.Cos(a), cy - 0.75f * k * MathF.Sin(a), opacity * twinkle);
                }
                break;
            }
        }
    }
}
