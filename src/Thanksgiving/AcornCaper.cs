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
///   1. A grey squirrel's head rises out of the hollow in the roots of the
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
///      facing the thief. They chatter at each other, tails lashing, hopping
///      with rage, and go for each other: a rolling cloud of dust with tails
///      and paws sticking out of it, and the acorn is flung out of the
///      scuffle and bounces away toward the corn shock.
///   4. A mouse creeps out from the corn shock, scurries to the acorn, sniffs
///      it, takes it, and scurries home with it, while the squirrels are
///      still rolling.
///   5. The dust settles. The two squirrels sit facing each other, look over
///      at where the acorn was, look at each other, and each goes home: one
///      along the rail and up the left maple, the other up the right.
///
/// FEYNMAN VERSION: this is not a clockwork of states like the turkeys. It is
/// a SCRIPT. When a cycle begins, dice choose the burying spot and the
/// pauses, and Begin writes out every actor's whole part as a list of
/// "segments" (hop from here to there, run along the rail, dig for so many
/// seconds), each with its start and end time on the cycle's clock. Drawing
/// a frame is then only: find the segment whose time it is, and work out
/// where that actor is within it from the clock alone, exactly as a
/// happening does. The pictures are the Squirrel happening's own flip-book
/// (Squirrel.PaintPage), with three poses added for the caper: digging, the
/// climb (the run pages turned on their side), and the head peeking out of
/// the hollow; the mouse and the dust cloud are painted here.
///
/// It holds the director's "ground", "fence" and "cornshock" claims while it
/// plays (HappeningDirector.SceneClaims), so no turkey strolls through the
/// scuffle and no second squirrel runs the rail at the same time, and it
/// never begins while a happening holding one of those is on.
///
/// What it is solid to: the ground (WalkY) and the rail (the painter's own
/// rail curve). On the rail it is stamped through the OpenOfProps stencil,
/// so it passes behind the corn shock and the maples; on the ground and on
/// the trunks it is in front of everything but the leaves.
/// </summary>
internal sealed class AcornCaper
{
    private enum Kind { Peek, Hop, Run, Climb, Sit, Dig, Mouse, Fracas }

    /// <summary>One piece of one actor's part: what it does between T0 and T1, and where.</summary>
    private sealed class Seg
    {
        public Kind Kind;
        public float T0, T1;
        public PointF From, To;             // the feet, in pixels (for Fracas: the cloud's centre on the ground)
        public int Dir = 1;                 // +1 faces right, -1 left
        public bool Acorn, Look, Rail;      // carrying the nut; head up; on the rail (its stencil applies)
        public bool Up;                     // a climb with the head up (false: head first down)
        public float P0, P1;                // extras, by kind: Peek: how far out, from and to. Hop: arc height in squirrel units. Run: the bound length (pixels).
                                            // Climb: opacity from and to. Sit: 0 calm, 1 chattering with rage. Dig: scrabbles a second (0 = just sniffing), P1 = 1 with earth flying.
                                            // Fracas: fade in and fade out seconds.
        public float Dur => T1 - T0;
    }

    private struct Sheet
    {
        public Sprite[] Pages;
        public int W, H, Ox, By;            // the sheet, and where the feet's origin is on it
    }

    private const int RunPages = 16, DigPages = 8, MousePages = 6, CloudPages = 8, PeekPages = 4, AirPages = 8;
    private const float Speed = 0.27f;                  // bounding, U per second (the fence squirrel's own pace)
    private const float ClimbSpeed = 0.20f;
    private const float MouseSpeed = 0.19f;
    private const float HopFor = 0.36f;
    private const string Claims = "ground,fence,cornshock";

    private readonly ThanksgivingScenery _s;
    private readonly HappeningDirector _director;
    private readonly Random _rng;
    private readonly float _k, _m;                      // the squirrel unit and the mouse unit, in pixels
    private Sheet[] _run = new Sheet[2];                // [acorn]
    private Sheet[] _up = new Sheet[2];                 // climbing, head up: [acorn]
    private Sheet _down;                                // climbing, head first down
    private Sheet[,] _sit = new Sheet[2, 4];            // [acorn, tail flick pose]
    private Sheet[] _lookUp = new Sheet[2];
    private Sheet[] _dig = new Sheet[2];                // [acorn]
    private Sheet _peek, _mouse, _mouseNut, _cloud, _air;
    private Sprite _acorn = null!, _mound = null!, _hole = null!, _speck = null!;
    private volatile bool _ready;

    private readonly List<Seg>[] _tracks = [new(), new(), new(), new()];   // 0 the first squirrel, 1 the thief, 2 the mouse, 3 the dust cloud
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
        // The pictures take a moment to paint (about a hundred and fifty pages); done on a side thread so the scene starts at once.
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    // ================================================================ painting (once)

    private Sheet MakeSheet(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
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
        for (int a = 0; a < 2; a++)
        {
            bool nut = a == 1;
            _run[a] = MakeSheet(RunPages, w, h, ox, by, i => Squirrel.PaintPage(k, w, h, ox, by, i / (float)RunPages, 0f, 0f, 0, false, bound, nut));
            // Climbing with the head up: the run turned a quarter turn, so forward is up the screen and the back is to the left of the feet.
            int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
            _up[a] = MakeSheet(RunPages, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i => Squirrel.PaintPage(k, cw, ch, (int)(1.9f * k) + 3, (int)(0.8f * k) + 3, i / (float)RunPages, 0f, 0f, 0, false, bound, nut, rotate: -90f));
            for (int tail = 0; tail < 4; tail++)
            {
                int tl = tail;
                _sit[a, tail] = MakeSheet(1, w, h, ox, by, _ => Squirrel.PaintPage(k, w, h, ox, by, 0.97f, 1f, 0f, tl, false, bound, nut));
            }
            _lookUp[a] = MakeSheet(1, w, h, ox, by, _ => Squirrel.PaintPage(k, w, h, ox, by, 0.97f, 1f, 0f, 0, true, bound, nut));
            _dig[a] = MakeSheet(DigPages, w, h, ox, by, i => Squirrel.PaintPage(k, w, h, ox, by, 0f, 0f, 0f, 0, false, bound, nut, dig: 1f, digPhase: i / (float)DigPages));
        }
        {
            int cw = (int)(1.9f * k) + 6, ch = (int)(2.55f * k) + 6;
            _down = MakeSheet(RunPages, cw, ch, 3, (int)(1.75f * k) + 3, i => Squirrel.PaintPage(k, cw, ch, 3, (int)(1.75f * k) + 3, i / (float)RunPages, 0f, 0f, 0, false, bound, false, rotate: 90f));
        }
        {
            int pw = (int)(0.9f * k) + 4, ph = (int)(0.8f * k) + 4, pox = pw / 2, pby = (int)(0.72f * k) + 2;
            _peek = MakeSheet(PeekPages + 1, pw, ph, pox, pby, i => Squirrel.PaintPeek(k, pw, ph, pox, pby, i < PeekPages ? (i + 1) / (float)PeekPages : 1f, i == PeekPages));
        }

        // The nuts on the ground are painted a touch bigger than the one in a mouth, so a child can see what the fuss is about.
        _acorn = Squirrel.PaintAcorn(k * 1.6f, 90f);
        _air = MakeSheet(AirPages, 1, 1, 0, 0, i => Squirrel.PaintAcorn(k * 1.6f, 360f * i / AirPages));

        int mw = (int)(1.9f * _m) + 4, mh = (int)(0.9f * _m) + 4, mox = (int)(1.25f * _m) + 2, mby = mh - 2;
        _mouse = MakeSheet(MousePages, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, i / (float)MousePages, false));
        _mouseNut = MakeSheet(MousePages, mw, mh, mox, mby, i => PaintMouse(mw, mh, mox, mby, i / (float)MousePages, true));

        int fw = (int)(2.8f * k) + 4, fh = (int)(1.9f * k) + 4;
        _cloud = MakeSheet(CloudPages, fw, fh, fw / 2, fh - 2, i => PaintCloud(fw, fh, i * 360f / CloudPages));

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

    /// <summary>A small grey field mouse scurrying to the right: a plump body, a round ear, a pink nose, a long thin tail that waves as it runs, and four quick little legs.</summary>
    private Sprite PaintMouse(int w, int h, int ox, int by, float phase, bool nut) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(_m, -_m);                  // mouse units (its body length), y UP from the ground
            Color fur = Color.FromArgb(132, 118, 108), dark = Color.FromArgb(92, 80, 74), pink = Color.FromArgb(222, 150, 150);
            float run = MathF.Sin(phase * MathF.Tau);
            // The tail: waves side to side (which, seen from the side, is up and down) as it runs.
            using (var tail = new Pen(dark, 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawBezier(tail, new PointF(-0.36f, 0.20f), new PointF(-0.70f, 0.10f + 0.12f * run), new PointF(-0.95f, 0.35f - 0.10f * run), new PointF(-1.20f, 0.30f + 0.15f * run));
            // The legs: two pairs, scurrying out of step.
            using (var leg = new Pen(dark, 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(leg, 0.22f, 0.16f, 0.22f + 0.14f * run, 0.0f);
                g.DrawLine(leg, -0.22f, 0.16f, -0.22f - 0.14f * run, 0.0f);
                g.DrawLine(leg, 0.30f, 0.16f, 0.30f - 0.14f * run, 0.02f);
                g.DrawLine(leg, -0.14f, 0.16f, -0.14f + 0.14f * run, 0.02f);
            }
            using (var body = new SolidBrush(fur)) g.FillEllipse(body, -0.42f, 0.04f, 0.84f, 0.40f);
            using (var belly = new SolidBrush(Color.FromArgb(170, 196, 186, 176))) g.FillEllipse(belly, -0.30f, 0.04f, 0.56f, 0.20f);
            using (var head = new SolidBrush(fur)) g.FillEllipse(head, 0.26f, 0.12f, 0.36f, 0.32f);
            using (var ear = new SolidBrush(fur)) g.FillEllipse(ear, 0.26f, 0.34f, 0.20f, 0.20f);
            using (var inner = new SolidBrush(pink)) g.FillEllipse(inner, 0.30f, 0.37f, 0.12f, 0.12f);
            using (var eye = new SolidBrush(Color.FromArgb(24, 18, 18))) g.FillEllipse(eye, 0.46f, 0.28f, 0.07f, 0.07f);
            using (var nose = new SolidBrush(pink)) g.FillEllipse(nose, 0.59f, 0.22f, 0.07f, 0.06f);
            using (var wh = new Pen(Color.FromArgb(140, 230, 226, 230), 0.015f))
                for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.58f, 0.24f, 0.80f, 0.20f + i * 0.07f);
            if (nut)
            {
                // The acorn, nearly as big as its head, carried in its mouth.
                var st = g.Save();
                g.TranslateTransform(0.78f, 0.22f);
                g.RotateTransform(70f);
                float a = 0.165f;                        // the same nut that lay on the ground: as big as its head
                using (var b = new SolidBrush(Color.FromArgb(150, 98, 46))) g.FillEllipse(b, -a * 1.3f, -a, a * 2.6f, a * 2f);
                using (var cap = new SolidBrush(Color.FromArgb(94, 64, 36))) g.FillPie(cap, -a, -a * 1.2f, a * 2f, a * 2.4f, 70, 220);
                g.Restore(st);
            }
        });

    /// <summary>
    /// The scuffle: a ball of dust with the two squirrels inside it, turned by
    /// "turn" degrees. Dusty puffs in the ground's own colour, two silver
    /// tails sticking out at opposite sides, a paw and an ear poking out at
    /// the others, and a few bits of grass flying off it. The pages differ
    /// only by the turn, so played fast they roll.
    /// </summary>
    private Sprite PaintCloud(int w, int h, float turn) =>
        Sprite.Paint(w, h, g =>
        {
            float k = _k;
            g.TranslateTransform(w / 2f, h - 2 - 0.55f * k);
            PointF Polar(float deg, float r) { float a = (deg + turn) * MathF.PI / 180f; return new PointF(r * k * MathF.Cos(a), r * k * MathF.Sin(a)); }
            // The tails first, behind the dust: thick silver strokes with a curl at the end.
            using (var tail = new Pen(Color.FromArgb(206, 202, 210), k * 0.17f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var core = new Pen(Color.FromArgb(104, 98, 108), k * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float deg in (float[])[20f, 200f])
                {
                    PointF a = Polar(deg, 0.30f), b = Polar(deg + 12f, 0.70f), c = Polar(deg - 20f, 0.95f), d = Polar(deg - 45f, 0.95f);
                    g.DrawBezier(tail, a, b, c, d);
                    g.DrawBezier(core, a, b, c, d);
                }
            // The dust: overlapping puffs, each a shaded ball (a lighter top over a darker one).
            (float Deg, float R, float S)[] puffs = [(0, 0, 0.50f), (40, 0.32f, 0.36f), (130, 0.30f, 0.34f), (210, 0.34f, 0.38f), (290, 0.30f, 0.33f), (350, 0.36f, 0.30f)];
            foreach (var p in puffs)
            {
                PointF c = Polar(p.Deg, p.R);
                using var lo = new SolidBrush(Color.FromArgb(232, 170, 128, 74));
                g.FillEllipse(lo, c.X - p.S * k, c.Y - p.S * k, p.S * 2 * k, p.S * 2 * k);
                using var hi = new SolidBrush(Color.FromArgb(200, 214, 176, 112));
                g.FillEllipse(hi, c.X - p.S * k * 0.8f, c.Y - p.S * k * 0.95f, p.S * 1.6f * k, p.S * 1.4f * k);
            }
            // A paw and an ear poking out of the dust.
            using (var paw = new SolidBrush(Color.FromArgb(120, 114, 122)))
            {
                PointF p = Polar(110f, 0.62f);
                g.FillEllipse(paw, p.X - 0.09f * k, p.Y - 0.06f * k, 0.18f * k, 0.12f * k);
            }
            using (var ear = new SolidBrush(Color.FromArgb(146, 140, 148)))
            {
                PointF p = Polar(250f, 0.60f);
                g.FillEllipse(ear, p.X - 0.06f * k, p.Y - 0.09f * k, 0.12f * k, 0.18f * k);
            }
            // Bits of grass flung out, further round the circle than the dust.
            using (var grass = new Pen(Color.FromArgb(214, 166, 84), Math.Max(1f, k * 0.025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float deg in (float[])[65f, 160f, 275f, 330f])
                {
                    PointF a = Polar(deg * 1.1f, 0.95f), b = Polar(deg * 1.1f + 8f, 1.15f);
                    g.DrawLine(grass, a, b);
                }
        });

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

    private float Climb(List<Seg> track, float t0, PointF from, PointF to, bool up, bool acorn, float fadeFrom = 1f, float fadeTo = 1f)
    {
        float dur = Dist(from, to) / (ClimbSpeed * _s.U);
        Add(track, Kind.Climb, t0, dur, from, to, 1, acorn, p0: fadeFrom, p1: fadeTo, up: up);
        return t0 + dur;
    }

    private static float Sit(List<Seg> track, float t0, float dur, PointF at, int dir, bool acorn, float chatter = 0f, bool look = false)
    {
        Add(track, Kind.Sit, t0, dur, at, at, dir, acorn, p0: chatter, look: look);
        return t0 + dur;
    }

    private static float Dig(List<Seg> track, float t0, float dur, PointF at, int dir, float rate, bool earth, bool acorn = false, bool rail = false)
    {
        Add(track, Kind.Dig, t0, dur, at, at, dir, acorn, rail, p0: rate, p1: earth ? 1f : 0f);
        return t0 + dur;
    }

    /// <summary>The point on a trunk's line (foot to fork) at height y, set a hair to the left of the bark: where a squirrel's feet go.</summary>
    private PointF OnTrunk(PointF[] trunk, float y)
    {
        float f = Math.Clamp((trunk[0].Y - y) / (trunk[0].Y - trunk[1].Y), 0f, 1f);
        return new PointF(trunk[0].X + (trunk[1].X - trunk[0].X) * f - _s.U * 0.004f, y);
    }

    /// <summary>Rolls this cycle's dice and writes out everybody's part.</summary>
    private void Begin()
    {
        foreach (var track in _tracks) track.Clear();
        float k = _k, u = _s.U;
        List<Seg> A = _tracks[0], B = _tracks[1], M = _tracks[2], C = _tracks[3];
        PointF[] lt = _s.LeftTrunk, rt = _s.RightTrunk;
        float hx = _s.Hollow.At.X;
        float lRail = RailAt(lt[0].X), rRail = RailAt(rt[0].X);
        PointF lTrunkAtRail = OnTrunk(lt, lRail - 1.5f), rTrunkAtRail = OnTrunk(rt, rRail - 1.5f);
        PointF lFork = new(lt[1].X - u * 0.004f, lt[1].Y), rFork = new(rt[1].X - u * 0.004f, rt[1].Y);
        PointF lTop = new(lt[2].X - u * 0.004f, lt[2].Y), rTop = new(rt[2].X - u * 0.004f, rt[2].Y);
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
        Add(A, Kind.Peek, t, 0.7f, _s.Hollow.At, _s.Hollow.At, 1, p0: 0f, p1: 1f); t += 0.7f;
        Add(A, Kind.Peek, t, 1.1f + 0.6f * (float)_rng.NextDouble(), _s.Hollow.At, _s.Hollow.At, 1, p0: 1f, p1: 1f); t = A[^1].T1;
        t = Hop(A, t, new PointF(hx - 0.15f * k, _s.Hollow.At.Y + 0.2f * k), OnGround(hx - 0.95f * k), -1, false, 0.5f);
        PointF atPile = OnGround(_pileX + 0.78f * k);                                    // the paws reach the pile's near edge, the nuts in view past its nose
        t = Hop(A, t, OnGround(hx - 0.95f * k), atPile, -1, false, 0.35f);
        t = Dig(A, t, 0.9f, atPile, -1, 1.6f, false);                                    // nosing through the pile
        _pileTaken = t;
        t = Sit(A, t, 0.6f, atPile, -1, true, look: true);                               // got one
        t = Hop(A, t, atPile, atPile, 1, true, 0.3f);                                    // turn round
        t = Run(A, t, atPile, OnGround(lt[0].X - u * 0.004f - 0.25f * k), true, false);
        t = Climb(A, t, OnTrunk(lt, WalkY(lt[0].X)), lTrunkAtRail, true, true);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, true, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 0.5f * k), true, true);
        t = Hop(A, t, OnRail(_spotX - 0.5f * k), OnGround(_spotX - 0.5f * k), 1, true, 0.2f);
        t = Dig(A, t, 1.9f, OnGround(_spotX - 0.5f * k), 1, 3.2f, true, acorn: true);
        _buryAt = t;
        t = Dig(A, t, 0.5f, OnGround(_spotX - 0.5f * k), 1, 0f, false);                 // the nut goes in
        t = Dig(A, t, 0.8f, OnGround(_spotX - 0.5f * k), 1, 1.4f, false);               // pats the earth down
        t = Sit(A, t, 0.9f, OnGround(_spotX - 0.5f * k), 1, false, look: true);          // did anybody see?
        t = Sit(A, t, 0.5f, OnGround(_spotX - 0.5f * k), -1, false);
        t = Hop(A, t, OnGround(_spotX - 0.5f * k), OnRail(_spotX - 1.3f * k), -1, false, 0.5f);
        t = Run(A, t, OnRail(_spotX - 1.3f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        t = Hop(A, t, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        t = Climb(A, t, lTrunkAtRail, lFork, true, false);
        t = Climb(A, t, lFork, lTop, true, false, 1f, 0f);
        float aHome = t;

        // ---- Act 2: the thief ----
        t = aHome + 2.5f + 3f * (float)_rng.NextDouble();
        t = Climb(B, t, rTop, rFork, false, false, 0f, 1f);
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
        t = Dig(B, t, 0.7f, OnGround(_spotX + 0.5f * k), -1, 0f, false);                  // sniffs the mound
        float bDigs = t;
        t = Dig(B, t, 1.0f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        _digUpAt = t;
        t = Dig(B, t, 0.8f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        t = Sit(B, t, 1.4f, OnGround(_spotX + 0.5f * k), -1, true, look: true);           // found it!
        float bFound = t;

        // ---- Act 3: caught ----
        t = bDigs;
        t = Climb(A, t, lTop, lFork, false, false, 0f, 1f);
        Add(A, Kind.Climb, t, 0.7f, lFork, lFork, 1, p0: 1f, p1: 1f); t += 0.7f;         // freezes: what is THAT?
        t = Climb(A, t, lFork, lTrunkAtRail, false, false);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, false, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 1.6f * k), false, true);
        t = Hop(A, t, OnRail(_spotX - 1.6f * k), OnGround(_spotX - 1.6f * k), 1, false, 0.2f);
        float faceOff = Math.Max(t, bFound);
        if (t < faceOff) Sit(A, t, faceOff - t, OnGround(_spotX - 1.6f * k), 1, false, 1f);
        if (bFound < faceOff) Sit(B, bFound, faceOff - bFound, OnGround(_spotX + 0.5f * k), -1, true, 1f);
        t = faceOff;
        Sit(A, t, 1.8f, OnGround(_spotX - 1.6f * k), 1, false, 1f);
        Sit(B, t, 1.8f, OnGround(_spotX + 0.5f * k), -1, true, 1f);
        t += 1.8f;
        // The lunge: both at each other, and the cloud takes over.
        float cloudX = _spotX - 0.55f * k;
        Add(A, Kind.Run, t, 0.22f, OnGround(_spotX - 1.6f * k), OnGround(cloudX - 0.2f * k), 1, p0: 1.4f * k);
        Add(B, Kind.Run, t, 0.22f, OnGround(_spotX + 0.5f * k), OnGround(cloudX + 0.2f * k), -1, true, p0: 1.4f * k);
        t += 0.22f;
        float fracasFor = 3.8f;
        Add(C, Kind.Fracas, t, fracasFor, OnGround(cloudX), OnGround(cloudX), 1, p0: 0.15f, p1: 0.45f);
        _flingAt = t + 0.8f;
        _flingX = cloudX;
        _landX = cloudX + 2.3f * k;

        // ---- Act 4: the mouse ----
        float mx = _s.CornShockFoot.X;
        float m = t + 1.0f;
        float trip = (mx - _landX) / (MouseSpeed * u);
        Add(M, Kind.Mouse, m, trip, OnGround(mx), OnGround(_landX + 0.45f * _m), -1); m += trip;
        Add(M, Kind.Mouse, m, 0.4f, OnGround(_landX + 0.45f * _m), OnGround(_landX + 0.45f * _m), -1); m += 0.4f;   // sniffs it
        _grabAt = m;
        Add(M, Kind.Mouse, m, trip, OnGround(_landX + 0.45f * _m), OnGround(mx), 1, true); m += trip;

        // ---- Act 5: the dust settles ----
        t += fracasFor;
        PointF aAfter = OnGround(cloudX - 0.8f * k), bAfter = OnGround(cloudX + 0.8f * k);
        Sit(A, t, 0.6f, aAfter, 1, false);
        Sit(B, t, 0.6f, bAfter, -1, false);
        t += 0.6f;
        Sit(A, t, 1.2f, aAfter, 1, false, look: true);                                     // where did it go?
        float tb = Hop(B, t, bAfter, bAfter, 1, false, 0.3f);
        Sit(B, tb, 1.2f - HopFor, bAfter, 1, false, look: true);
        t += 1.2f;
        tb = Hop(B, t, bAfter, bAfter, -1, false, 0.3f);
        Sit(A, t, 1.0f, aAfter, 1, false);                                                  // a long look at each other
        Sit(B, tb, 1.3f - HopFor, bAfter, -1, false);
        t += 1.0f;
        float ta = Hop(A, t, aAfter, aAfter, -1, false, 0.3f);
        ta = Hop(A, ta, aAfter, OnRail(cloudX - 1.6f * k), -1, false, 0.5f);
        ta = Run(A, ta, OnRail(cloudX - 1.6f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        ta = Hop(A, ta, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        ta = Climb(A, ta, lTrunkAtRail, lFork, true, false);
        ta = Climb(A, ta, lFork, lTop, true, false, 1f, 0f);
        tb = Hop(B, t + 0.3f, bAfter, bAfter, 1, false, 0.3f);
        tb = Hop(B, tb, bAfter, OnRail(cloudX + 1.6f * k), 1, false, 0.5f);
        tb = Run(B, tb, OnRail(cloudX + 1.5f * k), OnRail(rt[0].X - 1.0f * k), false, true);
        tb = Hop(B, tb, OnRail(rt[0].X - 1.0f * k), rTrunkAtRail, 1, false, 0.4f);
        tb = Climb(B, tb, rTrunkAtRail, rFork, true, false);
        tb = Climb(B, tb, rFork, rTop, true, false, 1f, 0f);
        _end = Math.Max(ta, tb) + 0.8f;

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
                    sw.WriteLine($"  {names[i],-5} {s.Kind,-6} {s.T0,6:F2} to {s.T1,6:F2}  ({s.From.X:F0},{s.From.Y:F0}) to ({s.To.X:F0},{s.To.Y:F0}) dir {s.Dir} acorn {s.Acorn} rail {s.Rail} p0 {s.P0:F2} p1 {s.P1:F2}");
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

    private static void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, float feetX, float feetY, bool mirror, bool[]? stencil, float opacity = 1f)
    {
        int left = (int)MathF.Round(feetX) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(feetY) - sh.By, opacity, stencil, mirror);
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_active || !_ready) return;
        float t = _t, k = _k;
        float over = Smooth((_end - t) / 0.6f);                   // everything the caper left on the ground fades at the very end

        // ---- the props: the nut pile, the mound or the hole, the nut that got away ----
        float pileIn = Smooth(t / 0.5f);
        _acorn.DrawCentered(fb, _pileX - 0.16f * k, _pileY - 0.07f * k, pileIn * over);
        _acorn.DrawCentered(fb, _pileX + 0.14f * k, _pileY - 0.07f * k, pileIn * over);
        if (t < _pileTaken) _acorn.DrawCentered(fb, _pileX - 0.01f * k, _pileY - 0.18f * k, pileIn);   // the one on top is the one it takes
        if (t >= _buryAt + 0.3f && t < _digUpAt) _mound.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, Smooth((t - _buryAt - 0.3f) / 0.4f));
        if (t >= _digUpAt) _hole.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, over);
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

        switch (seg.Kind)
        {
            case Kind.Peek:
            {
                float outBy = seg.P0 + (seg.P1 - seg.P0) * Smooth(u);
                bool blink = seg.P0 >= 1f && (t - seg.T0) % 1.3f < 0.12f;        // a blink once a second or so, while it is looking about
                int page = blink ? PeekPages : Math.Clamp((int)MathF.Ceiling(outBy * PeekPages) - 1, 0, PeekPages - 1);
                // Looking about: the head turns (the sticker flips) a couple of times.
                bool face = seg.P0 >= 1f && (int)((t - seg.T0) / 0.8f) % 2 == 1;
                Stamp(fb, _peek, _peek.Pages[page], seg.From.X, seg.From.Y, face, null);
                break;
            }
            case Kind.Hop:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.From.Y + (seg.To.Y - seg.From.Y) * u - seg.P0 * k * 4 * u * (1 - u);
                bool m = seg.From == seg.To ? (u < 0.5f ? !mirror : mirror) : mirror;   // a hop on the spot is a turn: the sticker flips at the top
                Stamp(fb, _run[nut], _run[nut].Pages[8], x, y, m, null);
                break;
            }
            case Kind.Run:
            {
                float dist = MathF.Abs(seg.To.X - seg.From.X) * u;
                float x = seg.From.X + (seg.To.X - seg.From.X) * u;
                float y = seg.Rail ? RailAt(x) - 1.5f : WalkY(x);
                float cyc = dist / Math.Max(1f, seg.P0);
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                Stamp(fb, _run[nut], _run[nut].Pages[page], x, y, mirror, seg.Rail ? _s.OpenOfProps : null);
                break;
            }
            case Kind.Climb:
            {
                float x = seg.From.X + (seg.To.X - seg.From.X) * u, y = seg.From.Y + (seg.To.Y - seg.From.Y) * u;
                float cyc = Dist(seg.From, seg.To) * u / (1.6f * k);
                int page = (int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages;
                float opacity = seg.P0 + (seg.P1 - seg.P0) * u;
                // Head up: the back is to the left of the feet, so it clings to the trunk's left face as painted.
                // Head first down: the back is to the right as painted, so the sticker is flipped to put it on the same face.
                if (seg.Up) Stamp(fb, _up[nut], _up[nut].Pages[page], x, y, false, null, opacity);
                else Stamp(fb, _down, _down.Pages[page], x, y, true, null, opacity);
                break;
            }
            case Kind.Sit:
            {
                float s = t - seg.T0;
                int tail;
                float bob = 0f;
                if (seg.P0 > 0.5f)
                {
                    // Chattering with rage: the tail lashes through its poses without stopping, and it hops on the spot.
                    tail = new[] { 0, 1, 2, 3, 2, 1 }[(int)(s * 14) % 6];
                    bob = 0.14f * k * MathF.Abs(MathF.Sin(s * MathF.PI * 2.6f));
                }
                else
                {
                    float ft = s % 1.5f;                                          // a flick every second and a half, as on the fence
                    tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                }
                Sheet sh = seg.Look ? _lookUp[nut] : _sit[nut, tail];
                Stamp(fb, sh, sh.Pages[0], seg.From.X, seg.From.Y - bob, mirror, null);
                break;
            }
            case Kind.Dig:
            {
                float s = t - seg.T0;
                int page = seg.P0 <= 0f ? 0 : (int)(s * seg.P0 * DigPages) % DigPages;
                Stamp(fb, _dig[nut], _dig[nut].Pages[page], seg.From.X, seg.From.Y, mirror, seg.Rail ? _s.OpenOfProps : null);
                if (seg.P1 > 0f)
                {
                    // Earth flying: a fountain of specks from the paws, back over the body, each on its own little arc.
                    float px = seg.From.X + seg.Dir * 0.5f * k, py = seg.From.Y;
                    for (int i = 0; i < 14; i++)
                    {
                        float p = (s * seg.P0 * 0.4f + i / 14f) % 1f;
                        float reach = (1.3f + 0.35f * (i % 4)) * k, lift = (0.9f + 0.2f * (i % 3)) * k;   // well past the tail, so it reads as a fountain behind the digger
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
                int page = still ? 0 : (int)(s * 11) % MousePages;
                Sheet sh = seg.Acorn ? _mouseNut : _mouse;
                // Sniffing: it rocks forward a little, nose to the nut.
                float lean = still ? 0.06f * _m * MathF.Sin(s * 9f) : 0f;
                Stamp(fb, sh, sh.Pages[page], x - lean, y, mirror, _s.OpenOfProps);
                break;
            }
            case Kind.Fracas:
            {
                float s = t - seg.T0;
                float opacity = Smooth(s / seg.P0) * Smooth((seg.Dur - s) / seg.P1);
                float x = seg.From.X + 0.3f * k * MathF.Sin(s * 6.1f), y = seg.From.Y + 0.05f * k * MathF.Sin(s * 17f);
                int page = (int)(s * 13) % CloudPages;
                Stamp(fb, _cloud, _cloud.Pages[page], x, y, false, null, opacity);
                break;
            }
        }
    }
}
