using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

// ============================================================================
// THE WOOD MICE OF THE FALLEN LOG (LogMice.cs)
//
// Watched first (what a wood mouse does, in the order it does it):
//   It lives in a hole and comes out slowly: a nose, whiskers, then the head, which looks both ways
//   before the mouse commits. It runs in DASHES of half a second with FROZEN stops between (a
//   mouse that stops is a mouse that listens). It sits up on its haunches, nose going, forepaws at
//   its chest; it washes its face with both paws. It carries a seed home in its mouth, in dashes.
//   When frightened it bolts for the nearest cover (a stone, a stump, a brush pile, a bush) that is not on the
//   threat's side, round the back of it, or for the log; there is no hole in the snow (round five).
//   Two mice meet nose to nose and box for a second, and one hops back.
//
// Three dials per mouse, dealt at birth so no two are alike: BOLD (0 timid .. 1 bold: how far it
// roams, whether it commits when it peeks, how readily it flees rather than freezes), CURIOUS (how
// often it peeks, sits up, looks) and PLAYFUL (tussling). A mood: HUNGRY rises
// slowly and sends it to the bird table; a seed found drops it.
//
// EVERY MOVE IS A DASH of a whole number of strides. The stride is the bound's cycle (about 1.3
// body lengths). The page follows the DISTANCE walked (Pace.Page), and a dash always ends on the
// page where all four feet are down, because it is planned as exactly n strides. So no foot ever
// slides, and the stop is a hunched crouch with the nose going, which is what a frightened-still
// mouse looks like anyway. A REVERSAL IS A TURN: it stops, swings its face to us (a head-on page,
// and back), and goes the other way.
//
// WHAT CAN CUT A DOING SHORT, and what state each exit leaves (the rule for every new doing):
//   Home    : cut by SendHome/Lend (stays), by its timer (Peek).      Leaves: inside, tail coiled.
//   Peek    : a Threat/Loud in reach withdraws it (back to Home).     Leaves: inside.
//   Hop     : nothing cuts a hop (it is a third of a second).         Leaves: at the door / inside.
//   Hold    : any reaction; its timer.                                 Leaves: all four feet down.
//   Dash    : never cut mid-dash (a dash is n strides, planned); a Flee waits for the dash to land.
//   Rise/Sit/Wash/Fall: a Threat drops it (Fall) and flees.            Leaves: all four feet down.
//   Behind  : nothing (a hidden mouse is safe: it ignores the board).     Leaves: round the end of its cover, side on, then the next activity.
//   Box     : a Threat/Loud ends it at once.                           Leaves: both Fall, then Hold.
// A state's exit never writes a value the next state eases (the pace's odometer is zeroed when a dash
// STARTS, never when one ends).
// ============================================================================

/// <summary>A mouse's tail: a short chain of points dragged along behind the rump, so it trails the PATH (a bend in the run bends the tail) and never sticks out like a wire.</summary>
internal sealed class MouseTail
{
    private const int N = 8;
    private readonly PointF[] _p = new PointF[N];
    private bool _init;
    private float _t;

    /// <summary>Drags the tail: the root sits on the rump; each point keeps one link's length from the one before; a gentle pull toward the rest shape lays it along the ground behind.</summary>
    public void Update(PointF root, float facing, float groundY, float link, float dt, float speedLinks)
    {
        _t += dt;
        if (!_init)
        {
            for (int i = 0; i < N; i++) _p[i] = new PointF(root.X - facing * link * i, root.Y);
            _init = true;
        }
        _p[0] = root;
        float follow = Math.Min(1f, dt * 8f);
        float wave = 0.25f + 0.5f * Math.Min(1f, speedLinks);
        for (int i = 1; i < N; i++)
        {
            float s = i / (float)(N - 1);
            // rest shape: sinks to the ground within a few links, then lies in a lazy S behind the animal
            float restY = root.Y + (groundY - 0.5f - root.Y) * Math.Min(1f, i / 3f) + MathF.Sin(i * 0.9f + _t * 5f * wave) * link * 0.35f * s * wave
                          + Flick * MathF.Sin(i * 1.1f - _t * 30f) * link * 1.1f * s;       // the tip's flick as it slips into the hole
            var rest = new PointF(root.X - facing * link * i * 0.97f, restY);
            _p[i] = new PointF(_p[i].X + (rest.X - _p[i].X) * follow * 0.5f, _p[i].Y + (rest.Y - _p[i].Y) * follow);
            float dx = _p[i].X - _p[i - 1].X, dy = _p[i].Y - _p[i - 1].Y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1e-4f) { dx /= d; dy /= d; } else { dx = -facing; dy = 0f; }
            // NO SHARP CORNERS: a link may turn only so far from the one before it (about 28 degrees), so a quick turn of the root lays the
            // tail as a sweeping curve instead of a zigzag
            if (i >= 2)
            {
                float ax = _p[i - 1].X - _p[i - 2].X, ay = _p[i - 1].Y - _p[i - 2].Y, ad = MathF.Sqrt(ax * ax + ay * ay);
                if (ad > 1e-4f)
                {
                    ax /= ad; ay /= ad;
                    float ang = MathF.Atan2(ax * dy - ay * dx, ax * dx + ay * dy), lim = 0.5f;
                    if (MathF.Abs(ang) > lim)
                    {
                        float turn = Math.Clamp(ang, -lim, lim) - ang, c = MathF.Cos(turn), sn = MathF.Sin(turn);
                        (dx, dy) = (dx * c - dy * sn, dx * sn + dy * c);
                    }
                }
            }
            _p[i] = new PointF(_p[i - 1].X + dx * link, _p[i - 1].Y + dy * link);
        }
    }

    /// <summary>0 to 1: how much of the tail is drawn, from the root (a mouse that has just climbed out head on lets its tail swing into view).</summary>
    public float Reveal = 1f;

    /// <summary>0 to 1: a travelling wave along the tail (its tip flicks, as a tail does when its owner slips into a hole).</summary>
    public float Flick;

    public int Count => N;
    public PointF Point(int i) => _p[i];

    /// <summary>Lays the whole tail straight out behind the root (a mouse that has just appeared).</summary>
    public void Reset() => _init = false;

    public void Draw(FrameBuffer fb, float thick, byte[]? depth, int nearness, bool[]? stencil)
    {
        int shown = (int)MathF.Ceiling(Math.Clamp(Reveal, 0f, 1f) * (N - 1));       // a tail swinging into view grows from its root
        // drawn as a smooth curve (Catmull-Rom through the points, three short strokes a link), never as straight sticks with corners
        for (int i = 0; i < shown; i++)
        {
            PointF p0 = _p[Math.Max(0, i - 1)], p1 = _p[i], p2 = _p[i + 1], p3 = _p[Math.Min(N - 1, i + 2)];
            PointF prev = p1;
            for (int q = 1; q <= 3; q++)
            {
                float t = q / 3f, t2 = t * t, t3 = t2 * t;
                var cur = new PointF(0.5f * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
                                     0.5f * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
                float f = 1f - (i + t) / (float)(N - 1);
                fb.Line(prev, cur, LogMiceArt.TailColor, 0.95f, Math.Max(1f, thick * (0.45f + 0.55f * f)), stencil, depth, nearness);
                prev = cur;
            }
        }
    }
}

/// <summary>
/// The wood mice that live in the hollow of the fallen log. See the top of this file for what they do.
/// Public shape other code calls: Count, AllHome, SendHome(), Ready, MouseAt(i); and, for the tree-trimming
/// story, Lend / Unlend, Sheet, Decor.
/// </summary>
internal sealed class LogMice : Resident
{
    // -------- what a mouse is doing (see the top of the file for interrupters and exits) --------
    // The PHASE a mouse is in this very moment (what the body is doing)...
    // Enter (round of 2026-11-10): the walk INTO the log after the hop up to the lip. Cut short by: nothing (a mouse going in is committed,
    // like a dash). Leaves: out of sight inside, tail last, then Home.
    private enum Doing { Home, Peek, Hop, Hold, Scurry, Rise, Sit, Wash, Fall, Dig, Behind, Box, Enter }

    // ...and the ACTIVITY it is part of (what a child would say it is doing). One activity runs through many phases:
    // "Wander" is dash, listen, dash, listen; "Hide" is the run to a cover, the wait behind it, the peeks and the way out. Decisions are
    // made once per activity, never per phase, and the mind log's doing column is the ACTIVITY (the phase follows it).
    private enum Act { Home, Emerge, Wander, Sit, Wash, Nibble, Forage, Hide, Tussle, Watch, Flee, Return }

    private sealed class Mouse
    {
        public int Index;
        public float Bold, Curious, Play, Hungry;
        public Attention Senses = null!;
        public Mind<Doing> Mind = new(Doing.Home);
        public float X, Y;                  // the feet (the draw position's anchor)
        public int Dir = 1;                 // 1 faces right, -1 left
        public readonly Pace Pace = new(30f);
        public Act Act = Act.Home, LastAct = Act.Home;
        public int Legs;                    // dashes' worth of wandering still to do in this trip
        public bool Pivoting; public float PivotT; public int PivotDir;   // a turn on the spot: see StartPivot
        public float LookT, LookDur;        // a head-up look in progress (the body carries on where it was)
        public double PendingLook = -99.0;  // a look that arrived mid-doing, taken at the next stop if still fresh
        public bool FirstOut = true;        // the first peek is the entrance: it always commits
        public bool Withdraw; public int DoorWaits; public int Stuck; public bool Trimmed;
        public float SitFor, FallFrom = 1f; public bool FleePending, TussleHop; public int HopAway; public Doing LoggedPhase = Doing.Peek;                // seconds of sitting this activity asks for
        public float StrideNow;             // pixels in one stride, fixed at the start of a dash
        public float Ux, Uy, Remaining;     // the dash's direction and the strides still to go
        public float Speed;                 // pixels per second as last moved (for the log)
        public bool Dashing, Pausing, Fast, Carrying;
        public float PauseLeft, T;   // seconds in the sub-state / doing
        public float GoalX, GoalY;          // where the whole trip is bound
        public int Trip;                    // what the trip is FOR: 0 wander, 1 forage, 2 home, 3 partner
        public bool HomeBound;
        public float Out;                   // seconds spent out of the log
        public float NextBlink, NextEar, BlinkLeft, EarLeft, Seed;
        public Mouse? Partner;
        public bool Starter;                // the one that began a tussle
        public int Peeks, PeekPhase;
        public float PeekLookDir;
        public readonly MouseTail Tail = new();
        // -- how it is drawn this frame --
        public LogMiceArt.Book Book; public int Page; public bool Visible, UseStencil, Mirror;
        public float DrawX, DrawY; public int Size;
        public float HomeFor;
        public float HeadRise; public bool HeadOnly;
        public float Speak;                 // cooldown on the board posts
        public PointF HopFrom, HopTo; public float HopT, HopLen;
        public double Clock;
        public float DashEndX, DashEndY;
        // -- holes (round of 2026-11-10) --
        public float Shade;                 // 0 to 1: how dark the log's inside is on this mouse (only a side-view mouse in the hollow)
        public bool LipOn;                  // the log's front lip is stamped over this mouse this frame
        public float HopArc = 0.30f;        // a hop's height in mouse units
        public float EnterDist;             // pixels the walk into the log covers
        public float GlintT = -1f;          // eyes in the dark (a Home mouse): seconds into the glint, -1 = none
        public float NextGlint = 5f;
        public bool InOnly;
        public float EnterDur = 0.8f;
        // -- COVER (round five): the cover it hides behind, its spot, the stage of the hide, the face's lean --
        public int Cov = -1, HideDir = 1, BStage; public float HideX, HideY, HideEdgeFar, BT, HideT, BFor, PeekHold, NextPeek, FearX; public bool BLeaving;
        // PREDATORS (Predators.cs): Taken is "an owl or a wolf has this one": it is neither thought about nor drawn until Restore's timer runs out and
        // a replacement comes out of the log by the normal coming-out performance. WolfAt is the Posted time of the last predator notice it answered.
        public bool Taken; public float RestoreIn; public double WolfAt = -1;
    }

    private Sprite? _lipSp, _shadeSp; private int _lipX, _lipY, _shadeX, _shadeY;      // the log's two front pieces (the hollow's shade and the wood lip)

    private readonly Mouse[] _mice;
    private readonly int _count;
    private readonly LogMiceArt.Sheet[] _sheets = new LogMiceArt.Sheet[3];
    private readonly Sprite[] _puffGlow = new Sprite[2];
    private Sprite? _seed;
    private readonly float _unit;               // pixels in one mouse unit at the bottom of the screen
    private readonly bool[] _stencilIn;
    private readonly bool[] _stencil;         // true inside the hollow and to the right of the log's sawn face: where a mouse coming out of the hole may show
    private readonly PointF _door, _lip;        // the snow just outside the hollow, and the hollow's lower lip
    private readonly float _faceRx;
    private readonly StreamWriter? _log;
    private readonly string? _force;
    private double _clock;
    private double _buildMs, _firstUpdateMs; private int _updates;      // timings for the log: the painting on the helper thread, and the very first Update
    private float _sendHome;                    // seconds left of "everybody in"
    private bool _lent;
    private readonly float[] _xs;

    // -- little snow particles and footprints, all in rings: no allocation per frame --
    private struct Flake { public float X, Y, Vx, Vy, Age, Life; public int Size; public bool On; }
    private readonly Flake[] _puffs = new Flake[48];
    private int _puffAt;
    private struct Print { public float X, Y, Age; public bool On; }
    private readonly Print[] _prints = new Print[200];
    private int _printAt;

    /// <summary>The tree decorations the story hands over (see LogMiceDecor.cs).</summary>
    public TreeDecor Decor { get; }

    public int Count => _count;
    public bool IsLent => _lent;
    public PointF Door => _door;
    public PointF Lip => _lip;
    public bool[] DoorStencil => _stencil;
    public float Unit => _unit;

    public LogMice(ChristmasScenery s, int count, Random sceneRng, Noticeboard board) : base(s, sceneRng, board)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_MICECOUNT"), out int forced)) count = forced;    // test aid: 0 = the scene without the mice (the cost baseline)
        _count = Math.Clamp(count, 0, 6);
        _unit = MathF.Max(2.4f, 0.0215f * S.U);      // storybook factor 2.4 (a 9 cm mouse, 21 cm on screen): 2.2 was too small to carry ears and a face, and at 2.8 a mouse stood half as long as a hare
        _force = Environment.GetEnvironmentVariable("PECKWORKS_MICE");
        _log = MindLog.Open();
        Decor = new TreeDecor(S);

        PointF hollow = S.LogHollow; float r = S.LogR;
        _faceRx = 0.42f * r;
        float footY = hollow.Y + 0.82f * r;
        _door = new PointF(hollow.X + 0.62f * r + 3f, footY + 0.12f * r);
        _lip = new PointF(hollow.X, hollow.Y + 0.50f * r);

        // The stencil: pixels where a mouse that is coming out of (or going into) the hole may be seen: inside the dark
        // hollow, and anything right of the log's sawn face. Elsewhere the log hides it: it is still inside.
        _stencil = new bool[S.Width * S.Height];
        for (int y = Math.Max(0, (int)(hollow.Y - r * 0.6f)); y < Math.Min(S.Height, (int)(hollow.Y + r * 0.62f)); y++)
            for (int x = Math.Max(0, (int)(hollow.X - r)); x < Math.Min(S.Width, (int)(hollow.X + r)); x++)
            {
                float dx = (x + 0.5f - hollow.X) / (0.5f * _faceRx), dy = (y + 0.5f - hollow.Y) / (r * 0.56f);       // exactly the painted dark oval
                if (dx * dx + dy * dy <= 1f) _stencil[y * S.Width + x] = true;
            }
        // Everything right of the oval's right edge shows: a mouse out on the snow, or crossing the sawn face, is IN FRONT of the log (the pale
        // ring used to hide it, which read as a mouse coming out from behind the ring). Only the part still inside is cut, at the oval's own edge.
        _stencilIn = (bool[])_stencil.Clone();          // the oval alone: a head-on mouse (wider than the oval) is cut the same on both sides
        int rightEdge = (int)(hollow.X + 0.5f * _faceRx);
        for (int y = Math.Max(0, (int)(hollow.Y - r * 1.4f)); y < Math.Min(S.Height, (int)(footY + 2.2f * r)); y++)
            for (int x = Math.Max(0, rightEdge); x < Math.Min(S.Width, rightEdge + (int)(r * 6)); x++) _stencil[y * S.Width + x] = true;

        // ---- the mice: dials dealt so no two are alike ----
        _mice = new Mouse[_count];
        float[] bold = Traits.Deal(Rng, _count), curious = Traits.Deal(Rng, _count), play = Traits.Deal(Rng, _count);
        for (int i = 0; i < _count; i++)
        {
            var m = new Mouse
            {
                Index = i, Bold = bold[i], Curious = curious[i], Play = play[i], Hungry = 0.2f + 0.5f * (float)Rng.NextDouble(),
                Seed = (float)Rng.NextDouble() * 30f,
            };
            m.Senses = new Attention(0.45f + 0.55f * (1f - m.Bold), m.Curious) { Keen = 0.8f, Hungry = m.Hungry };
            m.X = _door.X; m.Y = _door.Y;
            // THE ENTRANCE: one at a time, a peek (about 3.4 s) and a hop out, so the three are out by about 15 s in
            m.Mind.Begin(Doing.Home, 0.5f + 4.6f * i + 1.2f * (float)Rng.NextDouble());
            m.NextBlink = 2f + 3f * (float)Rng.NextDouble(); m.NextEar = 3f + 5f * (float)Rng.NextDouble();
            _mice[i] = m;
        }
        _xs = new float[_count];

        if (Environment.GetEnvironmentVariable("PECKWORKS_SHEET") is string sq && sq.StartsWith("LogMiceSeams;")) LogMiceArt.WriteSeams(sq[13..], _unit);
        if (Environment.GetEnvironmentVariable("PECKWORKS_SHEET") is string sh && sh.StartsWith("LogMice"))
            LogMiceArt.WriteSheet(sh.Length > 8 ? sh[8..] : "review/sheet_logmice.png", _unit);

        float u0 = _unit;
        BuildInBackground(() =>
        {
            long b0 = System.Diagnostics.Stopwatch.GetTimestamp();
            float[] ks = { 1.0f, 0.92f, 0.84f };
            for (int i = 0; i < 3; i++)
            {
                float k = MathF.Max(2.2f, u0 * ks[i]);
                _sheets[i] = LogMiceArt.BuildSheet(k);
            }
            BuildLogFront();
            _seed = LogMiceArt.PaintSeed(u0);
            _puffGlow[0] = Sprite.Glow(Math.Max(2, (int)(u0 * 0.35f)), Color.FromArgb(244, 248, 255));
            _puffGlow[1] = Sprite.Glow(Math.Max(3, (int)(u0 * 0.6f)), Color.FromArgb(236, 242, 255));
            Decor.BuildInBackground(S, u0);
            // Exercise the shared kit once on throw-away objects (each of these compiles on first use, 3 to 5 ms the first time a
            // mouse reaches a doing that calls it, which was a visible hitch): the mind, the senses, the pace, the dice.
            {
                var mind = new Mind<Doing>(Doing.Home); mind.Begin(Doing.Hold, 0.2f); mind.Tick(0.1f); _ = mind.Over; mind.End();
                var att = new Attention(0.5f, 0.5f) { Keen = 0.8f, Hungry = 0.5f };
                var bd = new Noticeboard();
                bd.Post(Sense.Movement, new PointF(5, 5), 50f, 0.5f, null); bd.Post(Sense.Food, new PointF(5, 5), 50f, 0.5f, null); bd.Post(Sense.Threat, new PointF(5, 5), 50f, 1f, null);
                bd.Tick(0.1); att.Tick(0.1f); att.Check(bd, new PointF(6, 6), null, out _); att.Check(bd, new PointF(6, 6), null, out _);
                var pc = new Pace(20f) { Want = 10f }; pc.Tick(0.016f); pc.Tick(0.016f); pc.Brake(2f); pc.Tick(0.016f); _ = pc.Page(1f, 12); _ = pc.ToRestPage(1f); pc.Halt();
                var rr = new Random(1); _ = Organic.Jitter(rr, 3f); _ = Organic.Drift(2.0, 0.3f); _ = Organic.Smooth(0.4f); _ = Traits.Deal(rr, 3); _ = Dice.Pick(rr, new float[] { 1f, 2f, 0f, 3f });
                var tl = new MouseTail(); tl.Update(new PointF(10, 10), 1f, 12f, 4f, 0.016f, 0.5f); tl.Update(new PointF(14, 10), 1f, 12f, 4f, 0.016f, 0.5f);
            }
            // WARM THE CODE: with tiered JIT off, each method is compiled the first time it runs. Compile the mice's
            // own methods now, on this helper thread, so the first frame they update does not pay for it.
            foreach (Type t in new[] { typeof(LogMice), typeof(MouseTail), typeof(TreeDecor), typeof(LogMiceArt) })
                foreach (var mi in t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (mi.IsAbstract || mi.ContainsGenericParameters) continue;
                    try { System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(mi.MethodHandle); } catch { }
                }
            _buildMs = (System.Diagnostics.Stopwatch.GetTimestamp() - b0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        });
    }

    // ============================================================
    //  What the story and the scene may ask
    // ============================================================

    /// <summary>The picture set nearest the size of a mouse standing at y.</summary>
    public LogMiceArt.Sheet SheetAt(float y) => _sheets[SizeIdx(y)];
    public LogMiceArt.Sheet Sheet(int idx) => _sheets[idx];
    public int SizeIdx(float y) { float sc = S.FieldScale(y); return sc >= 0.955f ? 0 : sc >= 0.885f ? 1 : 2; }
    /// <summary>Pixels in one mouse unit for a mouse standing at y (the picture set's own size).</summary>
    public float UnitAt(float y) => _sheets[SizeIdx(y)]?.K ?? _unit;
    public Sprite? SnowPuff(int i) => _puffGlow[i];
    public Sprite? SeedSprite => _seed;

    public PointF MouseAt(int i) => i >= 0 && i < _count ? new PointF(_mice[i].X, _mice[i].Y) : _door;

    // ---------------------------------------------------------------- what a PREDATOR may ask of the mice (Predators.cs)
    // FEYNMAN VERSION: the same four answers a hunter needs about a hare. WHERE are its feet (FeetOf), is it OUT in the open (InOpen: false in
    // the log, behind a cover, or already taken), WHICH cover is it behind (CoverOf), and may I take it NOW (Seize: it vanishes from this frame;
    // false for the last mouse, who is spared). The hunter then calls Restore, and a minute or two later a new mouse comes out of the log by the
    // normal peek and hop.
    private int _stalked = -1;
    /// <summary>The mouse an owl has its eye on: it has not noticed anything yet (-1 none).</summary>
    public void Stalked(int i) => _stalked = i;
    /// <summary>How many mice are still alive (not taken).</summary>
    public int LeftCount { get { int n = 0; foreach (Mouse m in _mice) if (!m.Taken) n++; return n; } }
    /// <summary>Is mouse i out on the snow where a hunter can reach it (not in the log, not behind a cover, not taken, not lent to a story)?</summary>
    public bool InOpen(int i)
    {
        if (i < 0 || i >= _count || _lent) return false;
        Mouse m = _mice[i];
        if (m.Taken || !m.Visible) return false;
        return m.Mind.Doing is Doing.Hold or Doing.Rise or Doing.Sit or Doing.Wash or Doing.Fall or Doing.Box or Doing.Dig || m.Mind.Doing == Doing.Scurry && m.Trip != 21;
    }
    /// <summary>Where mouse i's feet are.</summary>
    public PointF FeetOf(int i) => MouseAt(i);
    /// <summary>The cover mouse i is behind or running to (null in the open or in the log).</summary>
    public Cover? CoverOf(int i)
    {
        if (i < 0 || i >= _count) return null;
        Mouse m = _mice[i];
        bool hiding = m.Mind.Doing == Doing.Behind || m.Mind.Doing == Doing.Scurry && m.Trip is 20 or 21 or 22;
        return hiding && m.Cov >= 0 && m.Cov < S.Covers.Count ? S.Covers[m.Cov] : null;
    }
    /// <summary>Mouse i is taken THIS frame: it stops being thought about and drawn. False if it is not in the open or it is the last mouse.</summary>
    public bool Seize(int i)
    {
        if (!InOpen(i) || LeftCount < 2) return false;
        Mouse m = _mice[i];
        if (m.Partner != null) { Mouse o = m.Partner; if (o.Partner == m) o.Partner = null; if (o.Mind.Doing is Doing.Sit or Doing.Rise) { o.Trip = 0; o.Mind.Left = 0f; } m.Partner = null; }
        m.Taken = true; m.RestoreIn = 1e9f; m.Visible = false; m.Dashing = false; m.Pausing = false; m.Pivoting = false; m.Carrying = false; m.Cov = -1; m.Trip = 0; m.Act = Act.Home; m.HeadOnly = false; m.UseStencil = false;
        m.Mind.Begin(Doing.Home, Mind<Doing>.Forever);
        _log?.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {i} SEIZED at {m.X:F0},{m.Y:F0}"));
        return true;
    }
    /// <summary>A replacement for taken mouse i comes out of the log after "afterSeconds" (the normal peek, then the hop out).</summary>
    public void Restore(int i, float afterSeconds) { if (i >= 0 && i < _count && _mice[i].Taken) _mice[i].RestoreIn = MathF.Max(0.1f, afterSeconds); }
    private void ReviveMouse(Mouse m)
    {
        m.Taken = false; m.X = _door.X; m.Y = _door.Y; m.Visible = false; m.Out = 0f; m.HomeFor = 0f; m.FirstOut = true; m.WolfAt = -1; m.Withdraw = false; m.Stuck = 0;
        m.Mind.Begin(Doing.Home, 0.5f + 1.5f * (float)Rng.NextDouble());
        _log?.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} RESTORED: a new mouse is in the log and will come out"));
    }

    /// <summary>True when every mouse is inside the log and still (the story's CanBegin waits for it).</summary>
    public bool AllHome
    {
        get
        {
            foreach (Mouse m in _mice) if (m.Mind.Doing != Doing.Home) return false;
            return true;
        }
    }

    /// <summary>Calls every mouse in: each finishes its dash, goes to the door and hops into the hole.</summary>
    public void SendHome() { if (_sendHome <= 0f) _sendHome = 14f; }

    private double _lastCall = -100.0;
    /// <summary>The story's polite request: calls the mice in, but at most once in 20 seconds (a story that is not then chosen must not keep sending them home).</summary>
    public void RequestHome()
    {
        if (_clock - _lastCall < 20.0) return;
        _lastCall = _clock;
        SendHome();
    }

    /// <summary>The tree story borrows the mice: they stay in the log (and nothing of theirs is drawn) until Unlend.</summary>
    public void Lend() { _lent = true; _sendHome = 0f; }
    public void Unlend()
    {
        _lent = false;
        foreach (Mouse m in _mice) m.Mind.Begin(Doing.Home, 6f + 10f * (float)Rng.NextDouble() + m.Index * 3f);
    }

    // ============================================================
    //  THINK AND MOVE
    // ============================================================

    public override void Update(float dt)
    {
        long t0 = _log != null || _updates == 0 ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        UpdateInner(dt);
        if (_updates++ == 0)
        {
            _firstUpdateMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _log?.WriteLine(FormattableString.Invariant($"0.000 LogMice BUILD {_buildMs:F0} ms, FIRSTUPDATE {_firstUpdateMs:F2} ms"));
        }
        if (_log != null)
        {
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ms > 2.0) _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice SLOWUPDATE {ms:F1} ms"));
        }
    }

    private void UpdateInner(float dt)
    {
        _clock += dt;
        if (_sendHome > 0f) _sendHome -= dt;
        Decor.Update(dt);
        TickParticles(dt);
        for (int i = 0; i < _count; i++) _xs[i] = _mice[i].X;

        foreach (Mouse m in _mice)
        {
            m.Clock += dt;
            if (m.Taken) { m.Visible = false; m.RestoreIn -= dt; if (m.RestoreIn <= 0f) ReviveMouse(m); continue; }       // taken by a predator: nothing of it runs until its replacement comes out of the log
            long q0 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            if (!_lent) Think(m, dt);
            else m.Visible = false;
            long q1 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            PrepareDraw(m, dt);
            if (_log != null)
            {
                long q2 = System.Diagnostics.Stopwatch.GetTimestamp();
                double thinkMs = (q1 - q0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency, prepMs = (q2 - q1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (thinkMs > 1.5 || prepMs > 1.5) _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice SLOWPART mouse {m.Index} think {thinkMs:F1} ms prepare {prepMs:F1} ms doing {m.Mind.Doing}"));
            }
            // who stands where: a mouse on the snow writes its feet on the scene's sheet (half its length), so nobody steps into it
            if (!_lent && m.Mind.Doing is not (Doing.Home or Doing.Peek)) S.Occupy(this, m.Index, m.X, m.Y, 0.55f * UnitAt(m.Y));
            // the log line keeps its shape (clock, name, index, x, y, speed, doing, book, page); the doing is the ACTIVITY (the phase
            // is in the separate PHASE lines below, written when it changes)
            if (_log != null)
            {
                _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} {m.X:F1} {m.Y:F1} {m.Speed:F1} {m.Act} {(m.Visible ? m.Book.ToString() : "None")} {m.Page}"));
                if (m.LoggedPhase != m.Mind.Doing) { m.LoggedPhase = m.Mind.Doing; _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} PHASE {m.Mind.Doing}{(m.Pausing ? "/pause" : "")}")); }
            }
        }
    }

    private void Roll(Mouse m, string what) =>
        _log?.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} ROLL {what}"));

    // ---- little helpers ----
    private float StrideAt(float y) => LogMiceArt.Stride * UnitAt(y);

    /// <summary>May feet go here? The scenery's rule, plus the snow at the log's door (inside the log's own keep-out strip), and never the ice.</summary>
    private bool Can(float x, float y, float room = 3f)
    {
        if (S.CanStand(x, y, room)) return true;
        float r = S.LogR, footY = S.LogHollow.Y + 0.82f * r;
        return x >= _door.X - 2f && x <= S.LogHollow.X + 2.2f * r && y >= footY - 0.05f * r && y <= footY + 1.0f * r && !S.OnPond(x, y, 1.08f);
    }

    private bool LineOk(float x0, float y0, float x1, float y1)
    {
        float d = MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int n = Math.Max(2, (int)(d / 3f));
        for (int i = 1; i <= n; i++)
        {
            float f = i / (float)n;
            if (!Can(x0 + (x1 - x0) * f, y0 + (y1 - y0) * f)) return false;
        }
        return true;
    }

    private float LaneLo(float x) => S.FieldPoint(x, 0.62f).Y;
    private float LaneHi(float x) => S.FieldPoint(x, 0.885f).Y;      // above the drifts along the bottom edge, which would hide a mouse's feet

    private void Begin(Mouse m, Doing d, float seconds) { m.Mind.Begin(d, seconds); m.T = 0f; m.Dashing = false; m.Pausing = false; m.FallFrom = 1f; }

    private float Distance(float x, float y, float x2, float y2) => MathF.Sqrt((x - x2) * (x - x2) + (y - y2) * (y - y2));

    private void Think(Mouse m, float dt)
    {
        // THE MOUSE AN OWL IS WATCHING STAYS WHERE IT IS. Think of a mouse washing its whiskers a little longer than it meant to: its own clock for
        // "what next" stops while it stands there (it still blinks and flicks an ear), so it does not happen to trot home in the four seconds the owl
        // needs to arrive. (Before this the owl's chosen mouse went home on its own timetable and the owl struck empty snow every time.)
        if (!(m.Index == _stalked && !m.Dashing && !m.Pivoting)) m.Mind.Tick(dt);
        m.Senses.Tick(dt);
        m.Speak -= dt;
        m.T += dt;
        m.Hungry = Math.Min(1f, m.Hungry + dt / 240f);
        m.Senses.Hungry = m.Hungry;
        if (m.Mind.Doing != Doing.Home && m.Mind.Doing != Doing.Peek && m.Mind.Doing != Doing.Hop) m.Out += dt;
        m.Speed = 0f;

        // blinks and ear flicks: a standing mouse is never still
        m.NextBlink -= dt; m.NextEar -= dt; m.BlinkLeft -= dt; m.EarLeft -= dt;
        if (m.NextBlink <= 0f) { m.BlinkLeft = 0.14f; m.NextBlink = Organic.Jitter(Rng, 3.5f); }
        if (m.NextEar <= 0f) { m.EarLeft = 0.22f; m.NextEar = Organic.Jitter(Rng, 6f); }

        // ---- a turn on the spot runs ahead of everything: the doing waits for it, and nothing flips a face but this ----
        if (m.Pivoting)
        {
            m.PivotT += dt;
            if (m.PivotT >= PivotLen * 0.5f && m.Dir != m.PivotDir) m.Dir = m.PivotDir;     // the face is toward us: the swing happens here, drawn
            if (m.PivotT >= PivotLen) m.Pivoting = false;
            return;
        }
        if (m.LookT < m.LookDur) m.LookT += dt;

        // ---- the senses: only a mouse that is out, and in the open, reacts ----
        Doing now = m.Mind.Doing;
        bool open = now is Doing.Hold or Doing.Scurry or Doing.Sit or Doing.Wash or Doing.Dig or Doing.Rise or Doing.Peek or Doing.Box;
        if (open)
        {
            PointF me = new(m.X, m.Y);
            // A PREDATOR'S THREAT (the wolf, the owl: Predators.cs) is heard from FARTHER than the fox's and answered at once (a flight, never rationed:
            // fear is not a glance). The mouse an owl has its eye on has not noticed yet. One answer per notice (WolfAt).
            if (m.Index != _stalked)
                foreach (Notice n in Board.Live)
                {
                    if (n.Kind != Sense.Threat || n.Who is not Predators || n.Posted <= m.WolfAt) continue;
                    float wx = n.At.X - m.X, wy = 2f * (n.At.Y - m.Y);
                    if (MathF.Sqrt(wx * wx + wy * wy) > n.Reach * 1.5f) continue;
                    m.WolfAt = n.Posted; Roll(m, "sees the predator"); React(m, Reaction.Flee, n);
                    break;
                }
            Reaction r = m.Senses.Check(Board, me, this, out Notice cause);
            if (r != Reaction.None && cause.Who is not LogMice and not Predators) React(m, r, cause);
        }

        switch (m.Mind.Doing)
        {
            case Doing.Home: ThinkHome(m, dt); break;
            case Doing.Peek: ThinkPeek(m, dt); break;
            case Doing.Hop: ThinkHop(m, dt); break;
            case Doing.Enter: ThinkEnter(m, dt); break;
            case Doing.Hold: ThinkHold(m, dt); break;
            case Doing.Scurry: ThinkScurry(m, dt); break;
            case Doing.Rise: if (m.T >= RiseLen) { if (m.Trip == 9) Begin(m, Doing.Sit, 5f); else Begin(m, Doing.Sit, m.SitFor); } break;
            case Doing.Sit: ThinkSit(m, dt); break;
            case Doing.Wash: if (m.Mind.Over || _sendHome > 0f) { m.Trip = 10; Begin(m, Doing.Sit, 1.0f + 0.6f * (float)Rng.NextDouble()); } break;
            case Doing.Fall: if (m.T >= RiseLen) { if (m.FleePending) { m.FleePending = false; DoFlee(m); } else if (m.TussleHop) { m.TussleHop = false; HopBack(m); } else EndActivity(m); } break;
            case Doing.Dig: ThinkDig(m, dt); break;
            case Doing.Behind: ThinkBehind(m, dt); break;
            case Doing.Box: ThinkBox(m, dt); break;
        }
        if (m.Mind.Doing != Doing.Scurry) { m.Dashing = false; }

        // the footprints: a faint dotted line behind a dashing mouse, laid every stride
        if (m.Dashing && m.Speed > 1f) MaybePrint(m);
    }

    // ---------------- Home and Peek ----------------

    private void ThinkHome(Mouse m, float dt)
    {
        m.X = _door.X; m.Y = _door.Y; m.Out = 0f; m.Visible = false; m.Act = Act.Home;
        m.HomeFor += dt;
        // HIDING IS ACTING: every 6 to 15 s a mouse that is in puts its eyes up to the dark (two glints that ease in, look left and right, blink, sink)
        if (m.GlintT >= 0f) { m.GlintT += dt; if (m.GlintT > GlintLen) m.GlintT = -1f; }
        else
        {
            m.NextGlint -= dt;
            if (m.NextGlint <= 0f)
            {
                bool busy = false; foreach (Mouse o in _mice) if (o != m && (o.Mind.Doing is Doing.Peek or Doing.Hop or Doing.Enter || o.GlintT >= 0f)) busy = true;
                if (!busy && m.Mind.Left > GlintLen + 0.5f) { m.GlintT = 0f; m.NextGlint = 6f + 9f * (float)Rng.NextDouble(); } else m.NextGlint = 1f;
            }
        }
        if (!m.Mind.Over) return;
        if (_sendHome > 0f || Lull && m.Curious < 0.5f) { m.Mind.Left = 4f; return; }
        // ONE AT A TIME THROUGH THE DOOR: a mouse does not put its head up while another is peeking, hopping out, or still on the doorstep
        foreach (Mouse o in _mice)
            if (o != m && (o.Mind.Doing == Doing.Peek || o.Mind.Doing == Doing.Enter || o.Act == Act.Return && Distance(o.X, o.Y, _door.X, _door.Y) < 0.2f * S.U || o.Mind.Doing == Doing.Hop && (!o.HomeBound || o.HopTo.X < _door.X + 4f) || o.Mind.Doing == Doing.Hold && o.Act == Act.Emerge)) { m.Mind.Left = 0.8f; return; }
        // the next peek: a stay inside is 4 to 8 s (a mouse that has been in over 9 s always comes out), never a loop of
        // peek-and-withdraw: a withdrawn peek is rare and only a timid mouse's
        float p = 0.7f + 0.3f * m.Curious; if (m.HomeFor > 9f) p = 1f;
        if (_force != null || m.FirstOut) p = 1f;   // the entrance (and a stress dial): the peek is made, and it commits
        if (Rng.NextDouble() < p) { Roll(m, "peek"); m.Act = Act.Emerge; Begin(m, Doing.Peek, 4.2f + (float)Rng.NextDouble()); m.PeekPhase = 0; m.Withdraw = false; m.DoorWaits = 0; m.PeekLookDir = Rng.Next(2) == 0 ? 1f : -1f; }
        else m.Mind.Begin(Doing.Home, 2f + 3f * (float)Rng.NextDouble());
    }

    /// <summary>The seconds a turn on the spot takes: seventeen drawn poses (side, seven in-betweens, head on, and back out).</summary>
    private const float PivotLen = 0.50f;
    /// <summary>The seconds it takes to rise onto the haunches or drop back (eased, so it starts and stops softly).</summary>
    private const float RiseLen = 0.42f;

    /// <summary>
    /// THE ONLY PLACE A MOUSE'S FACE CHANGES SIDE: a drawn turn (side, three-quarter, head on, three-quarter, side:
    /// 0.44 s). The direction flips at the middle, when the face is toward us and the flip cannot be seen.
    /// "fromFront" is for a mouse that is already head on (it has just peeked out of the hole, or popped up out of
    /// the snow): it begins at the middle and plays out into the side view, 0.22 s.
    /// </summary>
    private void StartPivot(Mouse m, int dir, bool fromFront)
    {
        m.Pivoting = true; m.PivotDir = dir;
        if (fromFront) { m.PivotT = PivotLen * 0.5f; m.Dir = dir; } else m.PivotT = 0f;
        if (_log != null) _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} FACE {m.Dir} to {dir} {(fromFront ? "fromfront" : "turn")}"));
    }

    private void ThinkPeek(Mouse m, float dt)
    {
        // t 0..0.8 the nose and whiskers; 0.8..1.7 the head; 1.7..3.3 it looks one way, the other, and back to the middle;
        // 3.35 it commits (turns out of head-on and hops) or withdraws (sinks)
        float t = m.T;
        if (m.Withdraw) { if (t > 3.9f) { m.Peeks++; m.Withdraw = false; m.Act = Act.Home; m.Mind.Begin(Doing.Home, 3f + 3f * (float)Rng.NextDouble()); m.Visible = false; } return; }
        if (t > 3.35f)
        {
            bool commit = m.FirstOut || _sendHome <= 0f && !Lull && (m.Bold > 0.25f || Rng.NextDouble() < 0.5 + 0.5f * m.Bold || m.HomeFor > 9f);
            if (_force != null && _sendHome <= 0f && !Lull) commit = true;
            // never hop out onto somebody: wait in the hole (up to six looks at the door) until the doorstep is clear
            if (commit && m.DoorWaits < 40 && (S.Crowded(_door.X, _door.Y, 0.7f * UnitAt(_door.Y), this, m.Index) || Crowded(m, _door.X, _door.Y, 1.6f * UnitAt(_door.Y))))
            { m.DoorWaits++; m.T = 3.0f; return; }
            Roll(m, commit ? "commit" : "withdraw");
            if (commit)
            {
                m.FirstOut = false;
                // THE WAY OUT: the mouse turns in the hollow (head on to side on, the head staying where the face was, so the body slides
                // back into the dark behind it), then walks out along the floor and steps down onto the snow. 0.8 s, a low step.
                m.HopFrom = new PointF(_lip.X - 0.55f * UnitAt(_lip.Y), _lip.Y); m.HopTo = _door; m.HopT = 0f; m.HopLen = 0.8f; m.HopArc = 0.14f;
                m.Tail.Reset();
                Begin(m, Doing.Hop, Mind<Doing>.Forever);
                StartPivot(m, 1, true);                        // head on to side on, then the hop
                m.HomeBound = false;
            }
            else m.Withdraw = true;
        }
    }

    // ---------------- Hop (out of the hole onto the snow, or from the snow into the hole) ----------------

    private void ThinkHop(Mouse m, float dt)
    {
        m.HopT += dt;
        float f = Math.Clamp(m.HopT / m.HopLen, 0f, 1f);
        if (f < 1f) { float fg = Organic.Smooth(f); m.X = m.HopFrom.X + (m.HopTo.X - m.HopFrom.X) * fg; m.Y = m.HopFrom.Y + (m.HopTo.Y - m.HopFrom.Y) * fg; }       // the ground position follows the drawn one (the log reads it)
        if (f >= 1f)
        {
            if (m.HomeBound)
            {
                // up on the lip: now it WALKS IN (a seed in its mouth goes in with it), the body pouring in along its spine
                m.X = m.HopTo.X; m.Y = m.HopTo.Y;
                m.EnterDist = 0.5f * _faceRx + 3.2f * UnitAt(_lip.Y);       // far enough that the tail's tip is inside too
                m.EnterDur = m.Fast ? 0.5f : EnterLen;                       // a flight goes in at twice the pace
                Begin(m, Doing.Enter, m.EnterDur); m.Dir = -1;
                return;
            }
            m.X = m.HopTo.X; m.Y = m.HopTo.Y;                     // it lands where the hop was bound (the door, or a snow spot after a tussle)
            Begin(m, Doing.Hold, 0.9f + 0.7f * (float)Rng.NextDouble());
        }
    }

    private const float EnterLen = 0.8f;

    /// <summary>The walk into the log: the lean in is slow (the weight goes forward first), then the body pours in, run pages by the distance walked so no foot slides.</summary>
    private void ThinkEnter(Mouse m, float dt)
    {
        float f = Math.Clamp(m.T / m.EnterDur, 0f, 1f);
        float x = m.HopTo.X - m.EnterDist * MathF.Pow(f, 1.25f);
        m.Speed = m.EnterDist * 1.25f * MathF.Pow(f, 0.25f) / m.EnterDur;
        // the tip's flick as the tail slips in (a travelling wave along the tail that grows through the second half of the walk)
        m.Tail.Flick = Organic.Smooth((f - 0.35f) / 0.35f) * (1f - Organic.Smooth((f - 0.92f) / 0.08f));
        m.X = x; m.Y = m.HopTo.Y;
        if (f >= 1f)
        {
            m.Carrying = false; m.Visible = false; m.Out = 0f; m.Act = Act.Home; m.Mind.Begin(Doing.Home, 4f + 4f * (float)Rng.NextDouble()); m.HomeFor = 0f; m.Tail.Reset(); m.Tail.Flick = 0f;
        }
    }

    // ---------------- Hold: the frozen stop, and the choosing ----------------

    private void ThinkHold(Mouse m, float dt)
    {
        // the settle on the doorstep after an entrance (or after a hop back): sniffing, ears moving, then the first real doing
        if (!m.Mind.Over) return;
        EndActivity(m);
    }

    /// <summary>An activity has run its course: remember it and roll for the next. There is no pause between (a stop is the first
    /// moment of the next activity: a trip begins with a listen, a sit with the rise), so nothing short sits between two doings.</summary>
    private void EndActivity(Mouse m)
    {
        m.LastAct = m.Act;
        Decide(m);
    }

    private bool OthersOut(Mouse m)
    {
        foreach (Mouse o in _mice) if (o != m && o.Mind.Doing != Doing.Home && o.Mind.Doing != Doing.Peek) return true;
        return false;
    }

    private void Decide(Mouse m)
    {
        bool lull = Lull;
        bool home = _sendHome > 0f;
        bool partner = !lull && !home && FindPartner(m) != null;
        float outFor = m.Out;
        // going in: only after a good long time out (at least 45 s), and not if it would leave the snow empty of mice
        float wHome = home ? 50f : m.Carrying ? 50f : outFor > 45f && (OthersOut(m) || outFor > 110f) ? (outFor - 45f) / 25f : 0f;
        if (lull && outFor > 20f) wHome += 1.2f;
        float L(Act a, float w) => m.LastAct == a ? w * 0.2f : w;                  // the same thing twice running is rare
        float live = home ? 0f : 1f;
        float wWander = L(Act.Wander, 1.4f * (0.6f + 0.6f * m.Bold)) * live;
        bool hid = HiddenAt(m.X, m.Y);                                                 // standing behind the fir: no sitting up out of sight, walk on first
        float wSit = hid ? 0f : L(Act.Sit, 0.9f + 0.9f * m.Curious) * live;
        float wWash = hid ? 0f : L(Act.Wash, 0.5f + 0.7f * (1f - m.Bold)) * live;
        float wForage = lull || home ? 0f : L(Act.Forage, 0.15f + 1.2f * m.Hungry);
        float wHide = lull ? 0.15f : L(Act.Hide, 0.55f + 0.9f * (1f - m.Bold)) * live;       // behind a cover (was the tunnel)
        float wTussle = partner ? L(Act.Tussle, 0.5f + 1.2f * m.Play) : 0f;
        float wWatch = lull || home ? 0f : L(Act.Watch, 0.4f + 0.8f * m.Curious);
        Span<float> w = stackalloc float[] { wWander, wSit, wWash, wForage, wHide, wTussle, wHome, 0f, 0f, wWatch };
        int pick = Dice.Pick(Rng, w);
        if (m.LastAct == Act.Emerge && !home && _force == null) pick = 0;        // the first thing after coming out is a wander away from the doorstep, so the next mouse's way is clear
        if (_force != null)
            pick = _force switch { "sit" => 1, "wash" => 2, "forage" => 3, "hide" => 4, "tussle" => partner ? 5 : 0, "home" => 6, "flee" => 7, "watch" => 9, _ => pick };
        Roll(m, pick switch { 0 => "wander", 1 => "sit", 2 => "wash", 3 => "forage", 4 => "hide", 5 => "tussle", 7 => "flee (test)", 9 => "watch", _ => "home" });
        switch (pick)
        {
            case 1: m.Act = Act.Sit; m.SitFor = 4.5f + 4f * (float)Rng.NextDouble(); m.Trip = 0; Begin(m, Doing.Rise, RiseLen); break;
            case 2: m.Act = Act.Wash; m.Trip = 9; Begin(m, Doing.Rise, RiseLen); break;      // trip 9 = "wash when you are up"
            case 3: StartForage(m); break;
            case 4: if (!StartHide(m, false)) StartWander(m); break;
            case 5: StartTussle(m); break;
            case 6: StartHome(m); break;
            case 7: if (Distance(m.X, m.Y, _door.X, _door.Y) < 0.45f * S.U) { StartHome(m); m.Fast = true; } else if (!StartHide(m, true)) StartHome(m); break;   // test dial: a flight
            case 9: StartWatch(m); break;
            default: StartWander(m); break;
        }
    }

    // ---------------- trips: a goal and the dashes to it ----------------

    /// <summary>Is this spot taken? Another resident's feet (the scene's who-stands-where sheet), or another mouse that stands or is bound there.</summary>
    private bool Blocked(Mouse m, float x, float y)
    {
        float half = 0.55f * UnitAt(y);
        return S.Crowded(x, y, half, this, m.Trip == 3 ? -1 : m.Index, 0.012f * S.U) || Crowded(m, x, y, 1.3f * UnitAt(y));
    }

    /// <summary>Is any of the line from one point to another taken (sampled every half stride)?</summary>
    private bool PathBlocked(Mouse m, float x0, float y0, float x1, float y1)
    {
        float d = Distance(x0, y0, x1, y1), step = 0.5f * StrideAt(y0);
        int n = Math.Max(1, (int)(d / step));
        for (int i = 1; i <= n; i++)
        {
            float f = i / (float)n;
            if (Blocked(m, x0 + (x1 - x0) * f, y0 + (y1 - y0) * f)) return true;
        }
        return false;
    }

    /// <summary>Is a mouse standing here hidden behind something nearer (the little fir's crown, a bush)? A mouse that is meant to be WATCHED never stops where it cannot be seen.</summary>
    private bool HiddenAt(float x, float y)
    {
        byte near = S.Nearness(y); float k = UnitAt(y);
        for (int h = 1; h <= 3; h++)
        {
            int px = (int)x, py = (int)(y - h * 0.3f * k);
            if (px < 0 || py < 0 || px >= S.Width || py >= S.Height) continue;
            if (S.NearDepth[py * S.Width + px] > near) return true;
        }
        return false;
    }

    private bool PickWanderGoal(Mouse m)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            float reach = (0.10f + 0.20f * m.Bold) * S.U;
            float gx = m.X + ((float)Rng.NextDouble() * 2f - 1f) * reach * 1.6f;
            float gy = m.Y + ((float)Rng.NextDouble() * 2f - 1f) * reach * 0.35f;
            gx = Math.Clamp(gx, 0.055f * S.Width, 0.67f * S.Width);
            gy = Math.Clamp(gy, LaneLo(gx), LaneHi(gx));
            if (Distance(gx, gy, m.X, m.Y) < 2.5f * StrideAt(m.Y) || !Can(gx, gy) || !LineOk(m.X, m.Y, gx, gy) || Blocked(m, gx, gy) || HiddenAt(gx, gy) || PathBlocked(m, m.X, m.Y, gx, gy)) continue;
            m.GoalX = gx; m.GoalY = gy; m.Trip = 0; m.Fast = false;
            return true;
        }
        if (_log != null) Roll(m, $"no wander goal from {m.X:F0},{m.Y:F0} hidden-here {HiddenAt(m.X, m.Y)} can-here {Can(m.X, m.Y)}");
        return false;
    }

    private void StartSit(Mouse m) { m.Act = Act.Sit; m.SitFor = 4.5f + 4f * (float)Rng.NextDouble(); m.Trip = 0; Begin(m, Doing.Rise, RiseLen); }

    /// <summary>A wander is two to four goals in a row, a few dashes to each with a listen between: one doing, 6 to 15 s.</summary>
    private void StartWander(Mouse m)
    {
        m.Act = Act.Wander; m.Legs = 1 + Rng.Next(3);
        if (!PickWanderGoal(m)) { StartSit(m); return; }
        Begin(m, Doing.Scurry, Mind<Doing>.Forever);
        m.Pausing = true; m.PauseLeft = 0.5f + 0.7f * (float)Rng.NextDouble();     // it listens before it sets off
        m.Stuck = 0;
    }

    private void StartHome(Mouse m)
    {
        m.Act = Act.Return;
        m.GoalX = _door.X; m.GoalY = _door.Y; m.Trip = 2; m.Fast = _sendHome > 0f || m.Carrying;
        Begin(m, Doing.Scurry, Mind<Doing>.Forever);
        m.Pausing = true; m.PauseLeft = 0.05f; m.Stuck = 0;
    }

    private void StartForage(Mouse m)
    {
        // the snow under the bird table: spilled seed lies there. A Food notice (a bird's seed) names the very spot.
        PointF foot = S.FeederFoot;
        for (int tries = 0; tries < 16; tries++)
        {
            float gx = foot.X + ((float)Rng.NextDouble() * 2f - 1f) * 0.035f * S.Width;
            float gy = Math.Clamp(foot.Y + (0.02f + 0.08f * (float)Rng.NextDouble()) * S.U, LaneLo(gx), LaneHi(gx));
            if (!Can(gx, gy, 5f) || !LineOk(m.X, m.Y, gx, gy) || Blocked(m, gx, gy) || HiddenAt(gx, gy)) continue;
            m.Act = Act.Forage; m.GoalX = gx; m.GoalY = gy; m.Trip = 1; m.Fast = false;
            Begin(m, Doing.Scurry, Mind<Doing>.Forever);
            m.Pausing = true; m.PauseLeft = 0.5f + 0.5f * (float)Rng.NextDouble(); m.Stuck = 0;
            return;
        }
        StartWander(m);
    }

    /// <summary>To a post at the pond's rim, to sit and watch the otters: facing the ice, for 6 to 10 s.</summary>
    private void StartWatch(Mouse m)
    {
        var pond = S.Pond;
        int side = m.X < pond.At.X ? -1 : 1;
        for (int tries = 0; tries < 14; tries++)
        {
            float gx = pond.At.X + side * (pond.Rx * (1.12f + 0.04f * tries) + 0.01f * S.Width);
            float gy = Math.Clamp(pond.At.Y + pond.Ry * (0.2f + 0.05f * tries), LaneLo(gx), LaneHi(gx));
            gx = Math.Clamp(gx, 0.055f * S.Width, 0.67f * S.Width);
            if (Distance(gx, gy, m.X, m.Y) < 3f * StrideAt(m.Y) || !Can(gx, gy, 5f) || !LineOk(m.X, m.Y, gx, gy) || Blocked(m, gx, gy) || HiddenAt(gx, gy) || PathBlocked(m, m.X, m.Y, gx, gy)) continue;
            m.Act = Act.Watch; m.GoalX = gx; m.GoalY = gy; m.Trip = 5; m.Fast = false;
            Begin(m, Doing.Scurry, Mind<Doing>.Forever);
            m.Pausing = true; m.PauseLeft = 0.5f + 0.5f * (float)Rng.NextDouble(); m.Stuck = 0;
            return;
        }
        StartWander(m);
    }

    /// <summary>A head-up look: the head lifts, holds, and eases back while the body carries on (a stop lasts as long as the look).</summary>
    private void StartLook(Mouse m)
    {
        m.LookDur = 1.6f + 1.4f * (float)Rng.NextDouble(); m.LookT = 0f;
        m.PendingLook = -99.0;
        if (m.Mind.Doing == Doing.Scurry && m.Pausing) m.PauseLeft = MathF.Max(m.PauseLeft, m.LookDur + 0.1f);
        else if (m.Mind.Doing == Doing.Hold) m.Mind.Left = MathF.Max(m.Mind.Left, m.LookDur + 0.1f);
    }

    /// <summary>A look is taken now if the mouse is standing still on all fours; otherwise it waits (and is dropped if over a second old).</summary>
    private void OfferLook(Mouse m)
    {
        if (m.LookT < m.LookDur) return;                                    // already looking
        bool standing = m.Mind.Doing == Doing.Hold || m.Mind.Doing == Doing.Scurry && m.Pausing && !m.Pivoting;
        if (standing) StartLook(m); else m.PendingLook = _clock;
    }

    private void ThinkScurry(Mouse m, float dt)
    {
        float stride = m.Dashing ? m.StrideNow : StrideAt(m.Y);
        if (m.Pausing)
        {
            if (m.Index == _stalked) return;              // the mouse an owl is watching goes on listening: its pause does not run out (see Think)
            m.PauseLeft -= dt;
            if (m.PauseLeft > 0f) return;
            if (_sendHome > 0f && m.Trip != 2 && m.Trip != 3) { StartHome(m); return; }
            // arrived?
            float toGoal = Distance(m.X, m.Y, m.GoalX, m.GoalY);
            // (a mouse going home hops in from up to two strides and a half away: a dash is two strides at least and the door is a small place, so a closer test looped at it, turn and turn about)
            if (toGoal < (m.Trip == 2 ? 2.4f : m.Trip == 3 ? 0.9f : m.Trip == 21 ? 0.40f : 1.45f) * stride) { ArriveAtGoal(m); return; }       // (a dash is two strides at least: closer than one and a half, it is there)
            if (m.T > 30f && m.Trip != 2) { m.Trip = 0; EndActivity(m); return; }                    // a trip that has not finished in 30 s is given up
            // the goal is behind: a REVERSAL IS A TURN, drawn
            float dx = m.GoalX - m.X;
            if (MathF.Abs(dx) > 0.5f * stride && MathF.Sign(dx) != m.Dir) { StartPivot(m, (int)MathF.Sign(dx), false); return; }
            BeginDash(m);
            return;
        }
        if (!m.Dashing) { m.Pausing = true; m.PauseLeft = 0.05f; return; }

        // ---- in the middle of a dash: the odometer is in STRIDES ----
        // Somebody stepped onto the line ahead: the dash is cut to the next whole stride (so it still ends with all four feet down).
        if (!m.Trimmed && m.Pace.Speed > 0f && Blocked(m, m.X + m.Ux * m.StrideNow * 1.1f, m.Y + m.Uy * m.StrideNow * 1.1f))
        {
            m.Trimmed = true;
            float keep = MathF.Max(MathF.Ceiling(m.Pace.Walked + 0.45f), 1f);
            if (keep < m.Remaining)
            {
                m.Remaining = keep;
                m.DashEndX = m.X + m.Ux * (keep - m.Pace.Walked) * m.StrideNow; m.DashEndY = m.Y + m.Uy * (keep - m.Pace.Walked) * m.StrideNow;
            }
        }
        float remaining = m.Remaining - m.Pace.Walked;
        if (!m.Pace.Braking && remaining <= MathF.Min(1.0f, m.Remaining * 0.5f) && m.Pace.Speed > 0.05f) m.Pace.Brake(MathF.Max(0.05f, remaining));
        float d = m.Pace.Tick(dt) * m.StrideNow;
        m.X += m.Ux * d; m.Y += m.Uy * d;
        m.Speed = d / MathF.Max(dt, 1e-4f);
        if (!m.Pace.Braking && m.Pace.Speed <= 0f && m.Pace.Walked >= m.Remaining - 0.02f || m.Pace.Walked >= m.Remaining)
        {
            m.X = m.DashEndX; m.Y = m.DashEndY;
            m.Dashing = false; m.Pace.Halt();
            m.Pausing = true;
            // the frozen stop: a mouse that stops is a mouse that listens
            m.PauseLeft = m.Fast ? 0.05f : (m.Trip == 1 ? 0.25f : 0.14f) + 0.5f * (float)Rng.NextDouble() * (1f - 0.5f * m.Bold);
            m.EarLeft = MathF.Max(m.EarLeft, 0.25f);                          // follow-through: the ears settle a moment after the body stops
            if (_clock - m.PendingLook < 1.0 && !m.Fast) StartLook(m);      // a look that came in mid-dash, still fresh: taken here
        }
    }

    // fields for the dash's planned end
    private void BeginDash(Mouse m)
    {
        float stride = StrideAt(m.Y);
        float dx = m.GoalX - m.X, dy = m.GoalY - m.Y;
        // a shallow diagonal only: the vertical part is held to half the horizontal (a lane change is a slant, never a slide up the screen)
        float maxDy = 0.55f * MathF.Max(MathF.Abs(dx), 1.5f * stride);
        dy = Math.Clamp(dy, -maxDy, maxDy);
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (Distance(m.X, m.Y, m.GoalX, m.GoalY) < (m.Trip == 2 ? 2.4f : m.Trip == 3 ? 0.9f : m.Trip == 21 ? 0.40f : 1.45f) * stride) { ArriveAtGoal(m); return; }       // (the TRUE distance, the same test as the pause's: two different tests for 'arrived' was a turn-back-and-forth loop at the door)
        int strides = (int)Math.Clamp(MathF.Round(dist / stride), 2f, m.Fast ? 8f : 5f);
        // do not overshoot: a dash that would pass the goal by over a stride is shortened
        if (strides * stride > dist + 0.6f * stride && strides > 2) strides--;
        float ux = dx / dist, uy = dy / MathF.Max(dist, 1e-3f);
        float ex = m.X + ux * strides * stride, ey = m.Y + uy * strides * stride;
        if (!LineOk(m.X, m.Y, ex, ey) || PathBlocked(m, m.X, m.Y, ex, ey))
        {
            // The way is not clear (a prop in the line, or somebody standing in it). GO ROUND IT, the way the patch's critters do
            // (30 and 60 degrees either side, then steeper): the first heading whose whole dash is clear is taken, but never one that
            // would swing the face to the other side (that is a turn, and a turn is asked for by name). Only when none is, the dash
            // waits a second and a half (somebody walks on), and after four waits the trip is given up.
            bool found = false;
            ReadOnlySpan<float> turns = [0f, 0.5f, -0.5f, 1.0f, -1.0f, 1.4f, -1.4f, 1.8f, -1.8f];
            foreach (float a in turns)
            {
                float c = MathF.Cos(a), sn = MathF.Sin(a);
                float rx = ux * c - uy * sn, ry = ux * sn + uy * c;
                if (rx * m.Dir < 0.05f) continue;
                for (int n = strides; n >= 2 && !found; n--)           // a shorter dash may clear what a long one does not
                {
                    float tx = m.X + rx * n * stride, ty = m.Y + ry * n * stride;
                    if (ty < LaneLo(tx) - 4f || ty > LaneHi(tx) + 4f || !LineOk(m.X, m.Y, tx, ty) || PathBlocked(m, m.X, m.Y, tx, ty)) continue;
                    ux = rx; uy = ry; ex = tx; ey = ty; strides = n; found = true;
                }
                if (found) break;
            }
            if (!found)
            {
                if (_log != null) Roll(m, FormattableString.Invariant($"blocked at {m.X:F0},{m.Y:F0} trip {m.Trip} wait {m.Stuck}"));
                m.Remaining = 0;
                m.Stuck++;
                // standing off the lane (a detour once carried a mouse onto the very edge of the screen, where every step is refused): a short hop back onto it
                float laneY = Math.Clamp(m.Y, LaneLo(m.X), LaneHi(m.X));
                if (m.Stuck >= 2 && MathF.Abs(laneY - m.Y) > 2f && Can(m.X, laneY))
                {
                    m.HopFrom = new PointF(m.X, m.Y); m.HopTo = new PointF(m.X, laneY); m.HopT = 0f; m.HopLen = 0.42f; m.HopArc = 0.30f; m.HomeBound = false; m.Trip = 0;
                    Begin(m, Doing.Hop, Mind<Doing>.Forever);
                    return;
                }
                if (m.Stuck < 4 || m.Trip == 2 && m.Stuck < 10) { m.Pausing = true; m.PauseLeft = 1.5f; return; }       // wait, look, try again
                                m.Trip = 0; EndActivity(m);
                return;
            }
        }
        // a dash that would carry the face to the other side is a turn first (the goal test above makes this rare)
        if (ux * m.Dir < -0.02f) { StartPivot(m, ux >= 0 ? 1 : -1, false); m.Pausing = true; m.PauseLeft = 0f; return; }
        m.StrideNow = stride; m.Ux = ux; m.Uy = uy; m.Remaining = strides;
        m.DashEndX = ex; m.DashEndY = ey;
        m.Pace.Walked = 0f; m.Pace.Halt();
        float sp = (m.Fast ? 0.30f : 0.115f + 0.03f * m.Bold) * S.U / stride;   // strides a second
        m.Pace.Accel = sp * 5f; m.Pace.Want = sp;
        m.Dashing = true; m.Pausing = false; m.Trimmed = false; m.Stuck = 0;
        // a dash in the open is a thing to notice (one post per dash)
        Board.Post(Sense.Movement, new PointF(m.X, m.Y), 0.15f * S.U, 0.3f, this);
    }

    private void ArriveAtGoal(Mouse m)
    {
        m.Pausing = false; m.Dashing = false;
        switch (m.Trip)
        {
            case 1: Begin(m, Doing.Dig, 1.5f); break;
            case 2: EnterHole(m); break;
            case 3: BeginBoxApproach(m); break;
            case 20:
            {
                // at the cover's near end, a stride or three short of the spot: a listen and a look back (the head up), then in along the row behind it
                m.GoalX = m.HideX; m.GoalY = m.HideY; m.Trip = 21; m.Pausing = true; m.PauseLeft = m.Fast ? 0.05f : 0.35f;
                if (!m.Fast) { StartLook(m); }
                break;
            }
            case 21: BeginBehind(m); break;
            case 22: m.Trip = 0; m.Cov = -1; m.Act = Act.Hide; EndActivity(m); break;
            case 5:
            {
                // at the rim: face the ice (a drawn turn if the walk here left it facing away), then sit and watch
                float towards = S.Pond.At.X > m.X ? 1 : -1;
                if (m.Dir != (int)towards) { StartPivot(m, (int)towards, false); m.Pausing = true; m.PauseLeft = 0f; break; }
                m.Trip = 0; m.SitFor = 6f + 4f * (float)Rng.NextDouble(); Begin(m, Doing.Rise, RiseLen);
                break;
            }
            default:
                // the next goal of the same wander (a listen first), or the wander is over
                if (m.Legs > 0 && PickWanderGoal(m)) { m.Legs--; m.Pausing = true; m.PauseLeft = 0.7f + 0.9f * (float)Rng.NextDouble(); m.Stuck = 0; }
                else EndActivity(m);
                break;
        }
    }

    private void EnterHole(Mouse m)
    {
        // it is at the door: turn to face the hole (left; a drawn turn if it is facing right), then hop in
        if (m.Dir != -1) { StartPivot(m, -1, false); m.Pausing = true; m.PauseLeft = 0f; return; }
        m.HopFrom = new PointF(m.X, m.Y);                 // the hop starts where the mouse IS: no snap to the door
        m.HopTo = _lip; m.HopT = 0f; m.HopLen = m.Fast ? 0.34f : 0.5f; m.HopArc = 0.36f; m.HomeBound = true;       // a hop UP onto the hollow's lip
        Begin(m, Doing.Hop, Mind<Doing>.Forever);
    }

    // ---------------- Sit and Wash ----------------

    private void ThinkSit(Mouse m, float dt)
    {
        if (m.FleePending) { Begin(m, Doing.Fall, RiseLen); return; }       // a fright while sitting: drop to all fours first, then run
        if (m.Trip == 9 && m.T > 0.6f) { m.Trip = 0; Begin(m, Doing.Wash, 3.2f + 1.6f * (float)Rng.NextDouble()); return; }
        if (m.Trip == 7) { TryBox(m); return; }
        if (m.Mind.Over || _sendHome > 0f && m.T > 0.5f)
        {
            if (m.Trip == 11) { m.Carrying = false; m.Hungry = Math.Max(0f, m.Hungry - 0.4f); m.Trip = 0; }       // the seed is eaten
            Begin(m, Doing.Fall, RiseLen);
        }
    }

    // ---------------- Dig (the forager finds a seed) ----------------

    private void ThinkDig(Mouse m, float dt)
    {
        // t 0..1.1 the nose in the snow, snow flicking; then up with a seed in its mouth
        if (m.T > 0.2f && m.T - dt <= 0.2f) Puff(m.X + m.Dir * 6f, m.Y - 2f, m.Dir * 20f, -30f, 0);
        if (m.T > 0.55f && m.T - dt <= 0.55f) Puff(m.X + m.Dir * 6f, m.Y - 2f, m.Dir * 24f, -34f, 1);
        if (m.T >= 1.25f)
        {
            m.Carrying = true; m.Hungry = Math.Max(0f, m.Hungry - 0.3f);
            Roll(m, "found a seed");
            if (Rng.NextDouble() < 0.45)
            {
                // sit up and eat it right there, held in both paws: the part of the forage that can be watched
                m.Trip = 11; m.SitFor = 5f + 2.5f * (float)Rng.NextDouble(); m.Act = Act.Nibble;
                Begin(m, Doing.Rise, RiseLen);
            }
            else { StartHome(m); m.Fast = true; m.Act = Act.Forage; }          // or carry it home in a bolt
        }
    }

    // ---------------- COVER: going behind a stone, a stump, a brush pile or a bush (round five, "no holes") ----------------
    //
    // FEYNMAN VERSION. There are no holes in the snow. A mouse hides the way a mouse hides behind a rock: it runs to the near END of the
    // cover, slows (a listen, a look back), and runs on round the end to the back, head first, the body following the same path and the tail
    // last. The painter put the cover in the depth picture with the nearness of ITS foot; the mouse's feet are a little farther than that, so the
    // picture itself hides it a part at a time as it passes behind the edge. Nothing fades, nothing shrinks, nothing sinks.
    // Behind the cover it sits, hidden. Every 6 to 15 s it PEEKS ROUND THE END: it turns head on (the drawn half turn), leans out until a third
    // of its face is past the edge (nose, whiskers, one eye), looks, blinks, leans back and turns side on again. To go, it peeks first, then
    // runs round the end it faces into the open. A cover takes two mice (a brush pile three), each its own place.
    //
    // WHAT CAN CUT A HIDE SHORT: nothing (a hidden mouse is safe, so it ignores the board); SendHome ends it at the next quiet moment. Leaves: in
    // the open on the row behind the cover's end, side on, all four feet down, then the next activity.

    /// <summary>Pixels of cover standing straight above (x, y): the depth picture's nearer pixels, counted up from the feet (gaps of two pixels bridged).</summary>
    private int CoverRunAt(float x, float y, int limit)
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

    /// <summary>Is a mouse standing at (x, y) hidden all over (a mouse's body is 0.5 of a unit each side and 0.7 tall)? Measured on the depth picture, the thing that does the hiding.</summary>
    private bool HiddenFully(float x, float y)
    {
        float k = UnitAt(y); int lim = (int)(1.1f * k), best = int.MaxValue;
        for (float dx = -0.55f * k; dx <= 0.55f * k; dx += 2f) best = Math.Min(best, CoverRunAt(x + dx, y, lim));
        return best >= 0.72f * k;
    }

    /// <summary>x of the cover's end at a mouse's low height, scanning from x in direction dir (gaps of three pixels bridged).</summary>
    private float EdgeOf(float x, float y, int dir, float limit)
    {
        int W = S.Width; byte me = S.Nearness(y); byte[] nd = S.NearDepth;
        int py = (int)(y - 0.2f * UnitAt(y)); float edge = x; int gap = 0;
        if (py < 0 || py >= S.Height) return x;
        for (float d = 0; d < limit; d += 1f)
        {
            int px = (int)(x + dir * d); if (px < 0 || px >= W) break;
            if (nd[py * W + px] > me) { edge = x + dir * d; gap = 0; } else if (++gap > 3) break;
        }
        return edge;
    }

    private int CoverUse(Mouse me, int ci)
    {
        int n = 0;
        foreach (Mouse o in _mice) if (o != me && o.Cov == ci && (o.Mind.Doing == Doing.Behind || o.Trip is 20 or 21)) n++;
        return n;
    }

    /// <summary>
    /// Chooses a cover for mouse m and starts the run to its near end (Trip 20, then in behind it, Trip 21). A mouse fits behind anything the picture
    /// really hides it behind. flee: not on the threat's side, farther afield. False if no cover is near enough (a fleeing mouse then makes for the log).
    /// </summary>
    private bool StartHide(Mouse m, bool flee)
    {
        float U = S.U; int best = -1; float bestCost = float.MaxValue;
        float bx = 0, by = 0, w1x = 0, ef = 0; int bdir = 1;
        for (int ci = 0; ci < S.Covers.Count; ci++)
        {
            Cover c = S.Covers[ci];
            int use = CoverUse(m, ci), cap = c.Kind == CoverKind.BrushPile ? 3 : 2;
            if (use >= cap) continue;
            float y = c.Hide.Y, k = UnitAt(y), stride = StrideAt(y);
            float x = c.Hide.X + (use == 0 ? 0f : use == 1 ? 0.55f : -0.55f) * c.HideHalf;
            if (!HiddenFully(x, y) || !Can(x, y, 3f)) continue;
            float d = Distance(m.X, m.Y, c.Foot.X, c.Foot.Y);
            if (d > (flee ? 0.55f : 0.40f) * U || d < 3f * stride) continue;
            if (flee && (c.Foot.X - m.X) * (m.FearX - m.X) > 0f && MathF.Abs(m.FearX - m.X) < 0.45f * U) continue;
            int dir = m.X < c.Foot.X ? 1 : -1;
            float edge = EdgeOf(x, y, -dir, c.HalfW * 1.6f);
            int n = Math.Max(3, (int)MathF.Ceiling(((x - (edge - dir * 1.0f * k)) * dir) / stride));
            float w1 = x - dir * n * stride;
            if (MathF.Abs(y - m.Y) > 0.45f * MathF.Abs(w1 - m.X)) continue;       // a lane change is a shallow diagonal: the row behind the cover must be reachable from here
            if (!Can(w1, y, 3f) || !LineOk(m.X, m.Y, w1, y) || Blocked(m, w1, y) || PathBlocked(m, m.X, m.Y, w1, y)) continue;
            float cost = d + (use > 0 ? 0.05f * U : 0f);
            if (cost < bestCost) { bestCost = cost; best = ci; bx = x; by = y; w1x = w1; ef = EdgeOf(x, y, dir, c.HalfW * 1.6f); bdir = dir; }
        }
        if (best < 0) return false;
        m.Cov = best; m.HideX = bx; m.HideY = by; m.HideDir = bdir; m.HideEdgeFar = ef;
        m.Act = flee ? Act.Flee : Act.Hide; m.GoalX = w1x; m.GoalY = by; m.Trip = 20; m.Fast = flee;
        Roll(m, $"hide behind cover {best} ({S.Covers[best].Kind}) at {bx:F0},{by:F0}{(flee ? " (flight)" : "")}");
        Begin(m, Doing.Scurry, Mind<Doing>.Forever);
        m.Pausing = true; m.PauseLeft = flee ? 0.05f : 0.4f + 0.4f * (float)Rng.NextDouble(); m.Stuck = 0;
        return true;
    }

    private void BeginBehind(Mouse m)
    {
        m.Pausing = false; m.Dashing = false; m.Pace.Halt();
        Begin(m, Doing.Behind, Mind<Doing>.Forever);
        m.BStage = 0; m.BT = 0f; m.HideT = 0f; m.BLeaving = false;
        m.BFor = (m.Fast ? 14f : 8f) + 16f * (float)Rng.NextDouble();
        m.NextPeek = (float)_clock + 4f + 6f * (float)Rng.NextDouble();
        m.Fast = false; m.Carrying = false;
    }

    /// <summary>How far (px) the head-on face leans out: until the face's middle is a little way inside the cover's far edge (about a third of the face shows).</summary>
    private float LeanFor(Mouse m) => MathF.Max(0f, ((m.HideEdgeFar - m.HideDir * 0.10f * UnitAt(m.HideY)) - m.HideX) * m.HideDir);

    private void ThinkBehind(Mouse m, float dt)
    {
        m.BT += dt; m.HideT += dt; m.Speed = 0f;
        float k = UnitAt(m.HideY);
        switch (m.BStage)
        {
            case 0: if (m.BT >= 0.45f) { m.BStage = 1; m.BT = 0f; } break;
            case 1:
                if (m.HideT >= m.BFor || _sendHome > 0f) { m.BLeaving = true; m.BStage = 2; m.BT = 0f; m.PeekHold = 0.8f + 0.6f * (float)Rng.NextDouble(); }
                else if (_clock >= m.NextPeek) { m.BStage = 2; m.BT = 0f; m.PeekHold = 1.3f + 1.2f * (float)Rng.NextDouble(); }
                break;
            case 2: if (m.BT >= 0.30f) { m.BStage = 3; m.BT = 0f; } break;                                   // the half turn, side on to head on
            case 3: m.X = m.HideX + m.HideDir * LeanFor(m) * Organic.Smooth(m.BT / 0.40f); if (m.BT >= 0.40f) { m.BStage = 4; m.BT = 0f; } break;
            case 4: if (m.BT >= m.PeekHold) { m.BStage = 5; m.BT = 0f; } break;
            case 5:
                m.X = m.HideX + m.HideDir * LeanFor(m) * (1f - Organic.Smooth(m.BT / 0.40f));
                if (m.BT >= 0.40f) { m.X = m.HideX; m.BStage = 6; m.BT = 0f; }
                break;
            default:
                if (m.BT >= 0.30f)
                {
                    m.BStage = 1; m.BT = 0f; m.NextPeek = (float)_clock + 6f + 9f * (float)Rng.NextDouble();
                    if (m.BLeaving) StartExit(m);
                }
                break;
        }
    }

    /// <summary>The way out: run on round the end the mouse faces (head first, the body following), a stride and a half past it, on the same row; the other end if that one is taken.</summary>
    private void StartExit(Mouse m)
    {
        float k = UnitAt(m.HideY), stride = StrideAt(m.HideY);
        for (int pass = 0; pass < 2; pass++)
        {
            int dir = pass == 0 ? m.HideDir : -m.HideDir;
            float edge = pass == 0 ? m.HideEdgeFar : EdgeOf(m.X, m.HideY, dir, 200f);
            float ex = edge + dir * 1.8f * stride;
            if (Can(ex, m.HideY, 3f) && LineOk(m.X, m.HideY, ex, m.HideY) && !Blocked(m, ex, m.HideY))
            {
                m.BLeaving = false; m.GoalX = ex; m.GoalY = m.HideY; m.Trip = 22; m.Fast = false; m.Act = Act.Hide;
                Roll(m, "comes out round the " + (dir > 0 ? "right" : "left") + " end");
                Begin(m, Doing.Scurry, Mind<Doing>.Forever);
                m.Pausing = true; m.PauseLeft = 0.1f; m.Stuck = 0;
                return;
            }
        }
        m.BLeaving = false; m.BFor = m.HideT + 5f;                   // both ends taken: it stays behind a little longer
    }

    // ---------------- Tussle ----------------

    /// <summary>True if another mouse that is out stands (or is bound) within "r" pixels of (x, y): no two mice share a spot.</summary>
    private bool Crowded(Mouse me, float x, float y, float r)
    {
        foreach (Mouse o in _mice)
        {
            if (o == me || o == me.Partner || o.Mind.Doing is Doing.Home or Doing.Peek || (me.Cov >= 0 && o.Cov == me.Cov)) continue;       // (two mice that share a cover keep their own places on its row)
            if (Distance(o.X, o.Y, x, y) < r) return true;
            if (o.Mind.Doing == Doing.Scurry && Distance(o.GoalX, o.GoalY, x, y) < r) return true;
        }
        return false;
    }

    /// <summary>A mouse that is sitting up, long enough, facing a mouse that is standing or walking about on the same row: the starter approaches from IN FRONT of its face, so neither has to turn.</summary>
    private Mouse? FindPartner(Mouse m)
    {
        if (m.Mind.Doing is not (Doing.Scurry or Doing.Hold or Doing.Fall)) return null;        // standing on all fours, at the end of something
        foreach (Mouse o in _mice)
        {
            if (o == m || o.Partner != null) continue;
            if (o.Mind.Doing != Doing.Sit || o.T < 1.5f || o.Mind.Left < 3f || o.Trip is 7 or 8 or 11 or 9) continue;
            if (Distance(o.X, o.Y, m.X, m.Y) > 0.35f * S.U) continue;
            if (MathF.Abs(o.Y - m.Y) > 0.05f * S.U) continue;
            if ((m.X - o.X) * o.Dir <= 0f) continue;          // the sitter must be facing the walker
            return o;
        }
        return null;
    }

    private void StartTussle(Mouse m)
    {
        Mouse? o = FindPartner(m);
        if (o == null) { StartWander(m); return; }
        m.GoalX = o.X + o.Dir * 1.5f * UnitAt(o.Y); m.GoalY = o.Y; m.Trip = 3; m.Fast = false;
        if (!Can(m.GoalX, m.GoalY, 4f) || !LineOk(m.X, m.Y, m.GoalX, m.GoalY)) { StartWander(m); return; }
        m.Partner = o; o.Partner = m; m.Starter = true; o.Starter = false; m.Act = Act.Tussle; o.Act = Act.Tussle;
        Begin(m, Doing.Scurry, Mind<Doing>.Forever); m.Pausing = true; m.PauseLeft = 0.4f; m.Stuck = 0;
        o.Trip = 8; o.Mind.Left = MathF.Max(o.Mind.Left, 12f);      // trip 8 = "waiting for a tussle: it stays sitting, facing the walker"
    }

    private void BeginBoxApproach(Mouse m)
    {
        Mouse? o = m.Partner;
        if (o == null || o.Mind.Doing != Doing.Sit) { AbortTussle(m); return; }
        // it arrives facing the sitter (it walked toward it from in front of its face); if the last dash left it otherwise, ask for a turn
        int want = o.X > m.X ? 1 : -1;
        if (m.Dir != want) { StartPivot(m, want, false); m.Pausing = true; m.PauseLeft = 0f; return; }
        Begin(m, Doing.Rise, RiseLen); m.Trip = 7;       // trip 7 = "rise and box"
    }

    /// <summary>The tussle is off. A mouse that is up drops to all fours (eased); one still on all fours simply carries on to its next doing.</summary>
    private void AbortTussle(Mouse m)
    {
        if (m.Partner != null) { Mouse o = m.Partner; if (o.Partner == m) o.Partner = null; if (o.Mind.Doing is Doing.Sit or Doing.Rise) { o.Trip = 0; o.Mind.Left = 0f; } }
        m.Partner = null; m.Trip = 0;
        if (m.Mind.Doing is Doing.Sit or Doing.Rise or Doing.Box or Doing.Wash) Begin(m, Doing.Fall, RiseLen);
        else EndActivity(m);
    }

    /// <summary>The starter, risen, begins the bout once the other is sitting: boxing with the forepaws, a second and a half to two.</summary>
    private void TryBox(Mouse m)
    {
        Mouse? o = m.Partner;
        if (o == null || o.Mind.Doing != Doing.Sit && o.Mind.Doing != Doing.Rise) { AbortTussle(m); return; }
        if (m.Starter && o.Mind.Doing == Doing.Sit && m.T > 0.15f)
        {
            Roll(m, "box");
            m.Trip = 0; o.Trip = 0;
            Begin(m, Doing.Box, 1.4f + 0.6f * (float)Rng.NextDouble());
            Begin(o, Doing.Box, 3f);
            // a post: a scuffle is something to hear
            Board.Post(Sense.Movement, new PointF(m.X, m.Y), 0.18f * S.U, 0.35f, this);
        }
        else if (m.T > 6f) AbortTussle(m);
    }

    private void ThinkBox(Mouse m, float dt)
    {
        Mouse? o = m.Partner;
        if (o == null) { Begin(m, Doing.Fall, RiseLen); return; }
        if (m.Mind.Over && m.Starter)
        {
            // both drop to all fours (eased); then the loser hops back, still facing the winner (see HopBack)
            m.HopAway = m.X < o.X ? -1 : 1; m.TussleHop = true;
            m.Partner = null; o.Partner = null;
            Begin(m, Doing.Fall, RiseLen); m.TussleHop = true;
            Begin(o, Doing.Fall, RiseLen); o.Trip = 0;
        }
    }

    /// <summary>The loser of a tussle, on all fours again, hops back about three lengths, facing the winner (a backward hop, not a turn).</summary>
    private void HopBack(Mouse m)
    {
        float hx = Math.Clamp(m.X + m.HopAway * 3.2f * UnitAt(m.Y), 0.055f * S.Width, 0.67f * S.Width);
        if (!Can(hx, m.Y, 4f) || !LineOk(m.X, m.Y, hx, m.Y) || Blocked(m, hx, m.Y)) { EndActivity(m); return; }
        m.HopFrom = new PointF(m.X, m.Y); m.HopTo = new PointF(hx, m.Y); m.HopT = 0f; m.HopLen = 0.34f; m.HopArc = 0.30f; m.HomeBound = false;
        Begin(m, Doing.Hop, Mind<Doing>.Forever);
    }

    // ---------------- Reactions ----------------

    /// <summary>
    /// A reaction never restarts or turns a doing. A LOOK is the head (lifted, held 1.6 to 3 s, eased back) taken at the next
    /// stop; a FREEZE lengthens a stop; a FLEE is the only thing that changes what the mouse is doing, and a mouse that is sitting
    /// up drops to all fours first. The calm noticeboard limits how often any of these arrive.
    /// </summary>
    private void React(Mouse m, Reaction r, Notice cause)
    {
        Doing d = m.Mind.Doing;
        m.FearX = cause.At.X;
        if (_log != null) Roll(m, $"react {r} {cause.Kind}");       // no string is built when nobody is logging
        switch (r)
        {
            case Reaction.Flee:
                if (m.Dashing) return;                   // a dash is not cut: the cooldown keeps the fright for a second or two
                if (d == Doing.Peek) { m.Withdraw = true; return; }
                if (d is Doing.Rise or Doing.Sit or Doing.Wash or Doing.Box)
                {
                    if (m.Partner != null) AbortTussle(m);
                    if (m.Mind.Doing != Doing.Fall) { float u = d == Doing.Rise ? Organic.Smooth(m.T / RiseLen) : 1f; Begin(m, Doing.Fall, RiseLen); m.FallFrom = u; }
                    m.FleePending = true;
                }
                else DoFlee(m);
                if (m.Speak <= 0f) { Board.Post(Sense.Friend, new PointF(m.X, m.Y), 0.20f * S.U, 0.4f, this); m.Speak = 3f; }
                break;
            case Reaction.Freeze:
                if (d == Doing.Scurry && m.Pausing) m.PauseLeft += 1.0f + 1.2f * (float)Rng.NextDouble();
                else if (d == Doing.Hold) m.Mind.Left += 1.0f + 1.2f * (float)Rng.NextDouble();
                break;
            case Reaction.Approach:
                if (cause.Kind == Sense.Food && !Lull) m.Hungry = Math.Min(1f, m.Hungry + 0.25f);       // hungrier: the next roll leans to the bird table
                break;
            case Reaction.Look:
                OfferLook(m);
                break;
        }
    }

    private void DoFlee(Mouse m)
    {
        float toDoor = Distance(m.X, m.Y, _door.X, _door.Y);
        // A THREAT: to the nearest cover that is not on the threat's side (round the back of it); none in reach, the log (a flight home)
        if (m.Carrying || !StartHide(m, true)) { StartHome(m); m.Fast = true; }
        m.Act = Act.Flee;
    }

    // ---------------- particles ----------------

    private void Puff(float x, float y, float vx, float vy, int size)
    {
        _puffs[_puffAt] = new Flake { X = x, Y = y, Vx = vx * (0.6f + (float)Rng.NextDouble() * 0.8f), Vy = vy * (0.7f + (float)Rng.NextDouble() * 0.6f), Age = 0f, Life = 0.55f + 0.3f * (float)Rng.NextDouble(), Size = size, On = true };
        _puffAt = (_puffAt + 1) % _puffs.Length;
    }

    private void MaybePrint(Mouse m)
    {
        float step = m.StrideNow * 0.5f;
        float walkedPx = m.Pace.Walked * m.StrideNow;
        if ((int)(walkedPx / step) == (int)((walkedPx - m.Speed * 0.0167f) / step)) return;
        _prints[_printAt] = new Print { X = m.X - m.Dir * 3f, Y = m.Y + 0.5f, Age = 0f, On = true };
        _printAt = (_printAt + 1) % _prints.Length;
    }

    private void TickParticles(float dt)
    {
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (!_puffs[i].On) continue;
            _puffs[i].Age += dt;
            if (_puffs[i].Age >= _puffs[i].Life) { _puffs[i].On = false; continue; }
            _puffs[i].X += _puffs[i].Vx * dt; _puffs[i].Y += _puffs[i].Vy * dt;
            _puffs[i].Vy += 90f * dt * (S.U / 1080f);          // snow falls back down
            _puffs[i].Vx *= 1f - 1.5f * dt;
        }
        for (int i = 0; i < _prints.Length; i++)
        {
            if (!_prints[i].On) continue;
            _prints[i].Age += dt;
            if (_prints[i].Age > 7f) _prints[i].On = false;
        }
    }

    // ============================================================
    //  WHAT TO DRAW THIS FRAME
    // ============================================================

    /// <summary>The page of a standing mouse on all fours: the sniff cycle, a blink, an ear flick, or (while it looks) the head eased up, held, and eased back down.</summary>
    private int StandPage(Mouse m, bool blink, int sniff)
    {
        if (m.LookT < m.LookDur)
        {
            float left = m.LookDur - m.LookT;
            if (left < 0.10f || m.LookT < 0.10f) return blink ? 4 : sniff;                 // the stop's own page first and last: no jump into the look
            if (left < 0.24f || m.LookT < 0.24f) return 8;                                   // easing up, easing back down
            return (((int)(m.LookT * 2.8f)) % 3 == 2 ? 8 : 9);                               // held high, the head dipping now and then
        }
        return blink ? 4 : m.EarLeft > 0f ? 5 : sniff;
    }

    /// <summary>Which page of the Rise book (with the rest page before it) a mouse that is "u" of the way up (0 on all fours, 1 sitting) shows.</summary>
    private static void RisePage(float u, out LogMiceArt.Book book, out int page)
    {
        int k = (int)MathF.Round(Math.Clamp(u, 0f, 1f) * LogMiceArt.RisePages);
        if (k <= 0) { book = LogMiceArt.Book.Stand; page = 0; }
        else { book = LogMiceArt.Book.Rise; page = k - 1; }
    }

    private void PrepareDraw(Mouse m, float dt)
    {
        LogMiceArt.Book book = LogMiceArt.Book.Stand; int page = 0;
        m.Visible = true; m.UseStencil = false; m.HeadOnly = false;
        m.Shade = 0f; m.LipOn = false; m.InOnly = false;
        if (m.Tail.Reveal < 1f) m.Tail.Reveal = Math.Min(1f, m.Tail.Reveal + dt / 0.40f);
        m.DrawX = m.X; m.DrawY = m.Y; m.Mirror = m.Dir < 0;
        float k = UnitAt(m.Y);
        int idle = ((int)(m.Clock * 6.5 + m.Seed)) % 4;
        double burst = Organic.Drift(m.Clock * 0.8, m.Seed);
        int sniff = burst > -0.1 ? idle : 0;
        bool blink = m.BlinkLeft > 0f;

        switch (m.Mind.Doing)
        {
            case Doing.Home: m.Visible = false; break;
            case Doing.Peek:
            {
                // the head rises in the hollow: a nose first, then the whole face, looks both ways and back to the middle,
                // and (only a mouse that withdraws) sinks again
                float t = m.T;
                float rise = t < 0.8f ? Organic.Smooth(t / 0.8f) * 0.55f : t < 1.7f ? 0.55f + 0.45f * Organic.Smooth((t - 0.8f) / 0.9f) : 1f;
                float sink = m.Withdraw ? Organic.Smooth((t - 3.35f) / 0.5f) : 0f;
                book = LogMiceArt.Book.Front; page = blink ? 1 : t > 1.7f && t < 1.95f ? 2 : 0;
                m.HeadOnly = true; m.HeadRise = (1f - rise + sink * 0.3f) * 1.0f;
                float look = t < 1.7f ? 0f : t < 2.4f ? m.PeekLookDir * Organic.Smooth((t - 1.7f) / 0.4f) : t < 2.9f ? -m.PeekLookDir * Organic.Smooth((t - 2.4f) / 0.5f) + m.PeekLookDir * (1f - Organic.Smooth((t - 2.4f) / 0.5f)) : -m.PeekLookDir * (1f - Organic.Smooth((t - 2.9f) / 0.4f));
                m.DrawX = S.LogHollow.X + look * 0.22f * k; m.DrawY = _lip.Y + (1f - rise) * 1.05f * k + sink * 0.5f * k;
                m.UseStencil = true; m.InOnly = true; m.Mirror = false;
                break;
            }
            case Doing.Hop:
            {
                float f = Math.Clamp(m.HopT / m.HopLen, 0f, 1f);
                float fx = Organic.Smooth(f);                 // eased along the ground: a leap leaves softly and lands softly (the arc itself is the height)
                m.DrawX = m.HopFrom.X + (m.HopTo.X - m.HopFrom.X) * fx;
                m.DrawY = m.HopFrom.Y + (m.HopTo.Y - m.HopFrom.Y) * fx - m.HopArc * k * MathF.Sin(MathF.PI * f);
                if (m.Pivoting && !m.HomeBound && m.HopFrom.X < _door.X + 4f)
                {
                    // the turn in the hollow: the head stays where the face was, the body slides back into the dark behind it
                    float g = Organic.Smooth((m.PivotT / PivotLen - 0.5f) / 0.5f);
                    m.DrawX = _lip.X + (m.HopFrom.X - _lip.X) * g; m.DrawY = _lip.Y;
                }
                // a hop is whole cycles of the bound: the run book from its rest page round to its rest page, so it leaves the ground and lands on the standing pose
                { float cycles = MathF.Max(1f, MathF.Round(Distance(m.HopFrom.X, m.HopFrom.Y, m.HopTo.X, m.HopTo.Y) / (LogMiceArt.Stride * k))); book = LogMiceArt.Book.Run; page = Math.Min(LogMiceArt.RunPages - 1, (int)(f * cycles * LogMiceArt.RunPages) % LogMiceArt.RunPages); }
                bool atLog = m.HopFrom.X < _door.X + 4f || m.HopTo.X < _door.X + 4f;
                m.UseStencil = atLog;
                if (atLog)
                {
                    // inside the hollow the mouse is cut at the oval, darkened toward the back, and its feet go under the lip
                    m.Shade = 1f;
                    if (m.HomeBound) m.LipOn = f > 0.3f;
                    else { m.InOnly = m.Pivoting; m.LipOn = m.Pivoting || f < 0.55f; if (m.Pivoting) m.Shade = Organic.Smooth((m.PivotT / PivotLen - 0.5f) / 0.5f); }
                }
                m.Mirror = m.Dir < 0;
                m.Speed = Distance(m.HopFrom.X, m.HopFrom.Y, m.HopTo.X, m.HopTo.Y) / m.HopLen * 6f * f * (1f - f);       // the eased ground speed (zero at both ends)
                if (m.Pivoting) { book = LogMiceArt.Book.Stand; page = 0; }
                break;
            }
            case Doing.Enter:
            {
                // the walk into the hollow: the run pages by the DISTANCE walked (no foot slides), cut at the oval, darkened toward the back, feet under the lip
                float walked = (m.HopTo.X - m.X) / (LogMiceArt.Stride * k);
                book = LogMiceArt.Book.Run; page = (int)(walked * LogMiceArt.RunPages) % LogMiceArt.RunPages;
                m.UseStencil = true; m.Shade = 1f; m.LipOn = true; m.Mirror = true;
                m.DrawY = m.Y;
                break;
            }
            case Doing.Hold:
                book = LogMiceArt.Book.Stand; page = StandPage(m, blink, sniff);
                break;
            case Doing.Scurry:
                if (m.Dashing) { book = LogMiceArt.Book.Run; page = m.Pace.Page(1f, LogMiceArt.RunPages); }
                else { book = LogMiceArt.Book.Stand; page = StandPage(m, blink, sniff); }
                break;
            case Doing.Rise: RisePage(Organic.Smooth(m.T / RiseLen), out book, out page); break;
            case Doing.Fall: RisePage(m.FallFrom * (1f - Organic.Smooth(m.T / RiseLen)), out book, out page); break;
            case Doing.Sit: book = LogMiceArt.Book.Sit; page = blink ? 4 : m.EarLeft > 0f ? 5 : sniff; break;
            case Doing.Wash: book = LogMiceArt.Book.Wash; page = ((int)(m.T * 18f)) % LogMiceArt.WashPages; break;
            case Doing.Box: book = LogMiceArt.Book.Box; page = ((int)(m.T * 24f + (m.Starter ? 0 : 9))) % LogMiceArt.BoxPages; break;
            case Doing.Dig:
                book = LogMiceArt.Book.Stand; page = m.T < 1.1f ? (((int)(m.T * 9f)) % 2 == 0 ? 7 : 6) : 6;
                break;
            case Doing.Behind:
            {
                // hidden behind its cover (the depth picture does the hiding): standing, sniffing, ears moving; to PEEK it turns head on by the drawn half turn
                // (side on, seven in-betweens, head on), leans out until a third of its face is past the edge, looks, blinks, leans back, turns side on again
                book = LogMiceArt.Book.Stand; page = StandPage(m, blink, sniff);
                if (m.BStage >= 2)
                {
                    float u = m.BStage == 2 ? Organic.Smooth(m.BT / 0.30f) : m.BStage == 6 ? 1f - Organic.Smooth(m.BT / 0.30f) : 1f;
                    int pose = Math.Min(8, (int)(u * 8.999f));
                    if (pose == 0) { book = LogMiceArt.Book.Stand; page = 0; }
                    else if (pose == 8) { book = LogMiceArt.Book.Front; page = blink ? 1 : 0; m.Mirror = false; }
                    else book = LogMiceArt.Book.Turn;
                    if (pose >= 1 && pose <= 7) page = pose - 1;
                }
                break;
            }
        }
        // ---- a turn on the spot overrides the page (the position stays the doing's own): side, three-quarters, head on, and out ----
        float tailSide = m.Mirror ? -1f : 1f;
        bool peeking = m.Mind.Doing == Doing.Behind && m.BStage >= 2;
        if (peeking) tailSide = m.Dir >= 0 ? 1f : -1f;
        if (m.Pivoting && m.Visible)
        {
            // seventeen poses over the turn: standing side on, the seven in-betweens, head on, the seven back out, standing side on
            int pose = Math.Min(16, (int)(m.PivotT / PivotLen * 17f));
            if (pose == 0 || pose == 16) { book = LogMiceArt.Book.Stand; page = 0; }
            else if (pose == 8) { book = LogMiceArt.Book.Front; page = blink ? 1 : 0; }
            else { book = LogMiceArt.Book.Turn; page = pose < 8 ? pose - 1 : 15 - pose; }            m.Mirror = m.Dir < 0;
            tailSide = -m.PivotDir * MathF.Cos(MathF.PI * m.PivotT / PivotLen);       // the tail's root slides across with the turn (old side to new side)
        }
        m.Book = book; m.Page = page; m.Size = SizeIdx(m.Y);

        // ---- the tail: rooted on the page's rump (the standing page's, while turning), dragged along the path ----
        if (m.Visible && !m.HeadOnly && _sheets[m.Size] != null)
        {
            MouseAnchors a = _sheets[m.Size].Anchor(m.Pivoting || peeking ? LogMiceArt.Book.Stand : book, m.Pivoting || peeking ? 0 : page);
            float kk = _sheets[m.Size].K;
            var root = new PointF(m.DrawX + tailSide * a.Tail.X * kk, m.DrawY - a.Tail.Y * kk);
            m.Tail.Update(root, tailSide >= 0f ? 1f : -1f, m.DrawY, 0.30f * kk, dt, m.Speed / MathF.Max(1f, 9f * kk));
        }
    }

    // ============================================================
    //  DRAW
    // ============================================================

    public override void Collect(DepthQueue q)
    {
        for (int i = 0; i < _count; i++)
        {
            Mouse m = _mice[i];
            if (m.Mind.Doing == Doing.Home) continue;
            if (!m.Visible) continue;
            q.Add(m.Y, this, i);
        }
        Decor.Collect(q, this);
        q.Add(100000f, this, 1000);            // the snow thrown up and the prints: over everything on the ground
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (index == 1000) { DrawParticles(fb); CaptureFrame(fb); return; }
        if (index >= 2000) { Decor.DrawIndex(fb, index - 2000); return; }
        Mouse m = _mice[index];
        LogMiceArt.Sheet sh = _sheets[m.Size];
        if (sh == null) return;
        float k = sh.K;
        bool atHole = m.UseStencil;
        byte[]? depth = atHole ? null : S.NearDepth;
        int near = atHole ? 254 : S.Nearness(m.Y);
        bool[]? st = atHole ? (m.InOnly ? _stencilIn : _stencil) : null;

        // the tail first (the body covers its root); not for a head popping up
        if (m.Visible && !m.HeadOnly && m.Mind.Doing != Doing.Home)
            m.Tail.Draw(fb, MathF.Max(1.2f, 0.11f * k), depth, near, st);

        if (m.Visible)
        {
            Sprite page = sh.Page(m.Book, m.Page);
            if (atHole)   // (at the log the stencil does the hiding, and the mouse crosses only a few pixels)
            {
                int left = (int)MathF.Round(m.DrawX) - (m.Mirror ? page.Width - 1 - sh.OriginX : sh.OriginX), top = (int)MathF.Round(m.DrawY) - sh.OriginY;
                page.Draw(fb, left, top, 1f, st, m.Mirror, depth, near);
            }
            else S.Stamp(fb, page, m.DrawX, m.DrawY, sh.OriginX, sh.OriginY, m.Mirror, 1f);       // float feet, never rounded here: the scene's stamp places it at fractions of a pixel
            // a seed in the mouth
            if (m.Carrying && !m.HeadOnly && _seed != null && m.Mind.Doing != Doing.Dig)
            {
                MouseAnchors a = sh.Anchor(m.Book, m.Page);
                float f = m.Mirror ? -1f : 1f;
                _seed.DrawCentered(fb, m.DrawX + f * (a.Mouth.X + 0.05f) * k, m.DrawY - a.Mouth.Y * k, 1f, st, m.Mirror, depth, near);
            }
        }
        // the log's FRONT PIECE, stamped after the mouse: the hollow's inside darkens what has gone in (a shade deepening toward the back),
        // and the lip under the hollow (cut from the backdrop's own pixels) hides the feet that are below it
        if (atHole && (m.Shade > 0.01f || m.LipOn)) DrawLogFront(fb, m.Shade, m.LipOn);
    }

    /// <summary>
    /// The log's FRONT piece, stamped after a mouse at the hollow. "shade" darkens the hollow's inside toward its back (a mouse going in
    /// fades into the dark by SHADOW, the way a thing does in a hole); "lip" puts the wood under the hollow, cut from the backdrop's own
    /// pixels, back over the mouse's feet.
    /// </summary>
    public void DrawLogFront(FrameBuffer fb, float shade, bool lip)
    {
        if (shade > 0.01f && _shadeSp != null) _shadeSp.Draw(fb, _shadeX, _shadeY, shade);
        if (lip && _lipSp != null) _lipSp.Draw(fb, _lipX, _lipY, 1f);
    }

    /// <summary>Paints the log's two front pieces from the scenery's own pixels (run on the helper thread).</summary>
    private void BuildLogFront()
    {
        float r = S.LogR; PointF hollow = S.LogHollow; float hw = 0.5f * _faceRx, hh = 0.56f * r;
        // the shade: the hollow's own oval, clear at its right edge and dark (the hollow's own colour) at its back
        int sx0 = (int)MathF.Floor(hollow.X - hw) - 1, sy0 = (int)MathF.Floor(hollow.Y - hh) - 1, sw = (int)MathF.Ceiling(2f * hw) + 3, sh = (int)MathF.Ceiling(2f * hh) + 3;
        var px = new uint[sw * sh];
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                float cx = sx0 + x + 0.5f, cy = sy0 + y + 0.5f;
                float dx = (cx - hollow.X) / hw, dy = (cy - hollow.Y) / hh;
                if (dx * dx + dy * dy > 1f) continue;
                float t = Math.Clamp((hollow.X + hw - cx) / (2f * hw), 0f, 1f);
                uint a = (uint)(255f * 0.94f * t * t * (3f - 2f * t));
                px[y * sw + x] = (a << 24) | (24u << 16) | (16u << 8) | 18u;
            }
        _shadeSp = Sprite.FromPixels(sw, sh, px); _shadeX = sx0; _shadeY = sy0;
        // the lip: the wood below the hollow's floor line (the backdrop's pixels there, kept exactly), only across the hollow's own width
        int lx0 = (int)MathF.Floor(hollow.X - hw) - 1, lx1 = (int)MathF.Ceiling(hollow.X + hw) + 1;
        int ly0 = (int)(hollow.Y + 0.47f * r), ly1 = (int)(hollow.Y + 0.82f * r) + 3;
        int lw = lx1 - lx0 + 1, lh = ly1 - ly0 + 1;
        var lp = new uint[lw * lh];
        for (int y = 0; y < lh; y++)
            for (int x = 0; x < lw; x++)
            {
                int gx = lx0 + x, gy = ly0 + y;
                if (gx < 0 || gy < 0 || gx >= S.Width || gy >= S.Height) continue;
                lp[y * lw + x] = S.Pixels[gy * S.Width + gx] | 0xFF000000u;
            }
        _lipSp = Sprite.FromPixels(lw, lh, lp); _lipX = lx0; _lipY = ly0;
    }

    // ---- TEST AID: PECKWORKS_MICECAP="mouse|fromLogSecond|toLogSecond|folder": while the log's clock (the mice's own, as written in the mind log) is in
    // the window, every frame saves a crop round that mouse as frameNNNN.png, ready for the contact-sheet tool. Same run as the log, so the
    // seconds in the log ARE the seconds of the pictures (the scene's own clock starts later by however long the painting took).
    private readonly string? _cap = Environment.GetEnvironmentVariable("PECKWORKS_MICECAP");
    private int _capN;
    private void CaptureFrame(FrameBuffer fb)
    {
        if (_cap == null) return;
        string[] p = _cap.Split('|');
        if (p.Length < 4 || !int.TryParse(p[0], out int mi) || mi < 0 || mi >= _count) return;
        double from = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), to = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
        if (_clock < from || _clock > to) return;
        Mouse m = _mice[mi];
        int cw = 120, ch = 84, x0 = (int)m.X - cw / 2, y0 = (int)m.Y - ch * 3 / 4;
        using var bmp = new Bitmap(cw, ch);
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int sx = Math.Clamp(x0 + x, 0, fb.Width - 1), sy = Math.Clamp(y0 + y, 0, fb.Height - 1);
                bmp.SetPixel(x, y, Color.FromArgb(255, Color.FromArgb((int)fb.Pixels[sy * fb.Width + sx])));
            }
        Directory.CreateDirectory(p[3]);
        bmp.Save(Path.Combine(p[3], $"frame{_capN++:0000}.png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    private const float GlintLen = 2.0f;

    /// <summary>The eyes in the dark: two pale glints low in the hollow that ease in, look one way and the other, blink once and sink.</summary>
    private void DrawGlints(FrameBuffer fb)
    {
        float k = UnitAt(_lip.Y);
        foreach (Mouse m in _mice)
        {
            if (m.GlintT < 0f || m.Mind.Doing != Doing.Home) continue;
            float t = m.GlintT;
            float a = Organic.Smooth(t / 0.4f) * (1f - Organic.Smooth((t - 1.55f) / 0.4f));
            if (t > 1.1f && t < 1.22f) a *= 0.1f;                                          // the blink
            float look = t < 0.7f ? 0f : t < 1.0f ? -Organic.Smooth((t - 0.7f) / 0.3f) : t < 1.4f ? -1f + 2f * Organic.Smooth((t - 1.0f) / 0.4f) : 1f - Organic.Smooth((t - 1.4f) / 0.3f);
            float cx = S.LogHollow.X + look * 0.07f * k, cy = _lip.Y - (0.50f - 0.05f * Organic.Smooth(t / 0.6f)) * k;
            int sz = S.U > 1500 ? 3 : 2;
            for (int e = -1; e <= 1; e += 2)
            {
                int ex = (int)MathF.Round(cx + e * 0.10f * k - sz * 0.5f), ey = (int)MathF.Round(cy);
                for (int yy = 0; yy < sz; yy++)
                    for (int xx = 0; xx < sz; xx++)
                    {
                        int pxl = ex + xx, pyl = ey + yy;
                        if (pxl < 0 || pyl < 0 || pxl >= fb.Width || pyl >= fb.Height || !_stencilIn[pyl * fb.Width + pxl]) continue;
                        fb.Pixels[pyl * fb.Width + pxl] = FrameBuffer.Blend(fb.Pixels[pyl * fb.Width + pxl], 236, 226, 190, 0.9f * a);
                    }
            }
        }
    }

    private void DrawParticles(FrameBuffer fb)
    {
        DrawGlints(fb);
        // footprints and groove dimples: small pale-blue dots that fade, kept off the props
        for (int i = 0; i < _prints.Length; i++)
        {
            if (!_prints[i].On) continue;
            float a = 0.5f * (1f - _prints[i].Age / 7f);
            int x = (int)_prints[i].X, y = (int)_prints[i].Y;
            if (x < 1 || y < 1 || x >= fb.Width - 1 || y >= fb.Height - 1 || S.NearDepth[y * fb.Width + x] != 0) continue;
            fb.Pixels[y * fb.Width + x] = FrameBuffer.Blend(fb.Pixels[y * fb.Width + x], 120, 142, 200, a);
            if (S.U > 700) fb.Pixels[y * fb.Width + x + 1] = FrameBuffer.Blend(fb.Pixels[y * fb.Width + x + 1], 120, 142, 200, a * 0.6f);
        }
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (!_puffs[i].On) continue;
            float f = _puffs[i].Age / _puffs[i].Life;
            if (_puffs[i].Size == 2)
            {
                // a speck: a tiny white square that falls back down
                int sz = S.U > 1500 ? 3 : 2, sx = (int)_puffs[i].X, sy = (int)_puffs[i].Y;
                for (int yy = 0; yy < sz; yy++)
                    for (int xx = 0; xx < sz; xx++)
                    {
                        int px = sx + xx, py = sy + yy;
                        if (px < 0 || py < 0 || px >= fb.Width || py >= fb.Height) continue;
                        fb.Pixels[py * fb.Width + px] = FrameBuffer.Blend(fb.Pixels[py * fb.Width + px], 250, 252, 255, 0.95f * (1f - f));
                    }
                continue;
            }
            if (_puffGlow[_puffs[i].Size] == null) continue;
            _puffGlow[_puffs[i].Size]!.DrawCentered(fb, _puffs[i].X, _puffs[i].Y, 0.85f * (1f - f) * (1f - f));
        }
    }
}
