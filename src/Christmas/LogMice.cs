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
//   When frightened it bolts for the hole, or, with no hole near, dives into the snow where it
//   stands and tunnels, and its head comes up a little way on with a cap of snow on it.
//   Two mice meet nose to nose and box for a second, and one hops back.
//
// Three dials per mouse, dealt at birth so no two are alike: BOLD (0 timid .. 1 bold: how far it
// roams, whether it commits when it peeks, how readily it flees rather than freezes), CURIOUS (how
// often it peeks, sits up, looks) and PLAYFUL (tunnelling and tussling). A mood: HUNGRY rises
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
//   Plunge/Under: nothing (a mouse under the snow hears nothing).      Leaves: Pop, then Hold.
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
            float restY = root.Y + (groundY - 0.5f - root.Y) * Math.Min(1f, i / 3f) + MathF.Sin(i * 0.9f + _t * 5f * wave) * link * 0.35f * s * wave;
            var rest = new PointF(root.X - facing * link * i * 0.97f, restY);
            _p[i] = new PointF(_p[i].X + (rest.X - _p[i].X) * follow * 0.5f, _p[i].Y + (rest.Y - _p[i].Y) * follow);
            float dx = _p[i].X - _p[i - 1].X, dy = _p[i].Y - _p[i - 1].Y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1e-4f) _p[i] = new PointF(_p[i - 1].X + dx / d * link, _p[i - 1].Y + dy / d * link);
        }
    }

    public int Count => N;
    public PointF Point(int i) => _p[i];

    /// <summary>Lays the whole tail straight out behind the root (a mouse that has just appeared).</summary>
    public void Reset() => _init = false;

    public void Draw(FrameBuffer fb, float thick, byte[]? depth, int nearness, bool[]? stencil)
    {
        for (int i = 0; i < N - 1; i++)
        {
            float f = 1f - i / (float)(N - 1);
            fb.Line(_p[i], _p[i + 1], LogMiceArt.TailColor, 0.95f, Math.Max(1f, thick * (0.45f + 0.55f * f)), stencil, depth, nearness);
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
    private enum Doing { Home, Peek, Hop, Hold, Scurry, Rise, Sit, Wash, Fall, Dig, Plunge, Under, Pop, Box, Frozen }

    private sealed class Mouse
    {
        public int Index;
        public float Bold, Curious, Play, Hungry;
        public Attention Senses = null!;
        public Mind<Doing> Mind = new(Doing.Home);
        public float X, Y;                  // the feet (the draw position's anchor)
        public int Dir = 1;                 // 1 faces right, -1 left
        public readonly Pace Pace = new(30f);
        public float StrideNow;             // pixels in one stride, fixed at the start of a dash
        public float Ux, Uy, Remaining;     // the dash's direction and the strides still to go
        public float Speed;                 // pixels per second as last moved (for the log)
        public bool Dashing, Turning, Pausing, Fast, Carrying;
        public float PauseLeft, TurnT, T;   // seconds in the sub-state / doing
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
        public float MoundGrow, MoundX, MoundY; public bool MoundOn, Ridge;
        public float HomeFor, FurrowX, FurrowY;
        public float HeadRise; public bool HeadOnly;
        public float Speak;                 // cooldown on the board posts
        public PointF HopFrom, HopTo; public float HopT, HopLen;
        public PointF UnderFrom, UnderTo; public float UnderLen, UnderDur, UnderT;
        public double Clock;
        public float DashEndX, DashEndY;
    }

    private readonly Mouse[] _mice;
    private readonly int _count;
    private readonly LogMiceArt.Sheet[] _sheets = new LogMiceArt.Sheet[3];
    private readonly Sprite[][] _mounds = new Sprite[3][];
    private readonly Sprite[] _ridges = new Sprite[3];
    private readonly Sprite[] _puffGlow = new Sprite[2];
    private Sprite? _seed;
    private readonly float _unit;               // pixels in one mouse unit at the bottom of the screen
    private readonly bool[] _stencil;           // true inside the hollow and to the right of the log's sawn face: where a mouse coming out of the hole may show
    private readonly PointF _door, _lip;        // the snow just outside the hollow, and the hollow's lower lip
    private readonly float _faceRx;
    private readonly StreamWriter? _log;
    private readonly string? _force;
    private double _clock;
    private float _sendHome;                    // seconds left of "everybody in"
    private bool _lent;
    private readonly float[] _xs;

    // -- little snow particles and footprints, all in rings: no allocation per frame --
    private struct Flake { public float X, Y, Vx, Vy, Age, Life; public int Size; public bool On; }
    private readonly Flake[] _puffs = new Flake[48];
    private int _puffAt;
    private struct Print { public float X, Y, Age; public bool On, Furrow; }
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
                float dx = (x - hollow.X) / (0.5f * _faceRx * 1.15f), dy = (y - hollow.Y) / (r * 0.56f);
                if (dx * dx + dy * dy <= 1f) _stencil[y * S.Width + x] = true;
            }
        int rightEdge = (int)(hollow.X + _faceRx + 1);
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
            m.Mind.Begin(Doing.Home, 1f + 4f * (float)Rng.NextDouble() + i * 2.5f);
            m.NextBlink = 2f + 3f * (float)Rng.NextDouble(); m.NextEar = 3f + 5f * (float)Rng.NextDouble();
            _mice[i] = m;
        }
        _xs = new float[_count];

        if (Environment.GetEnvironmentVariable("PECKWORKS_SHEET") is string sh && sh.StartsWith("LogMice"))
            LogMiceArt.WriteSheet(sh.Length > 8 ? sh[8..] : "review/sheet_logmice.png", _unit);

        float u0 = _unit;
        BuildInBackground(() =>
        {
            float[] ks = { 1.0f, 0.92f, 0.84f };
            for (int i = 0; i < 3; i++)
            {
                float k = MathF.Max(2.2f, u0 * ks[i]);
                _sheets[i] = LogMiceArt.BuildSheet(k);
                _mounds[i] = new[] { LogMiceArt.PaintMound(k, 0.12f), LogMiceArt.PaintMound(k, 0.26f), LogMiceArt.PaintMound(k, 0.40f), LogMiceArt.PaintMound(k, 0.52f) };
            }
            for (int i = 0; i < 3; i++) _ridges[i] = LogMiceArt.PaintRidge(MathF.Max(2.4f, u0 * ks[i]));
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
    public Sprite[] Mounds(float y) => _mounds[SizeIdx(y)];

    public PointF MouseAt(int i) => i >= 0 && i < _count ? new PointF(_mice[i].X, _mice[i].Y) : _door;

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
        long t0 = _log != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        UpdateInner(dt);
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
            if (_log != null)
                _log.WriteLine(FormattableString.Invariant($"{_clock:F3} LogMice {m.Index} {m.X:F1} {m.Y:F1} {m.Speed:F1} {m.Mind.Doing}{(m.Dashing ? "/dash" : m.Pausing ? "/pause" : m.Turning ? "/turn" : "")} {m.Book} {m.Page}"));
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

    private void Begin(Mouse m, Doing d, float seconds) { m.Mind.Begin(d, seconds); m.T = 0f; m.Dashing = false; m.Pausing = false; m.Turning = false; }

    private float Distance(float x, float y, float x2, float y2) => MathF.Sqrt((x - x2) * (x - x2) + (y - y2) * (y - y2));

    private void Think(Mouse m, float dt)
    {
        m.Mind.Tick(dt);
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

        // ---- the senses: only a mouse that is out, and in the open, reacts ----
        Doing now = m.Mind.Doing;
        bool open = now is Doing.Hold or Doing.Scurry or Doing.Sit or Doing.Wash or Doing.Dig or Doing.Frozen or Doing.Rise or Doing.Peek or Doing.Box;
        if (open)
        {
            PointF me = new(m.X, m.Y);
            Reaction r = m.Senses.Check(Board, me, this, out Notice cause);
            if (r != Reaction.None && cause.Who is not LogMice) React(m, r, cause);
        }

        switch (m.Mind.Doing)
        {
            case Doing.Home: ThinkHome(m, dt); break;
            case Doing.Peek: ThinkPeek(m, dt); break;
            case Doing.Hop: ThinkHop(m, dt); break;
            case Doing.Hold: ThinkHold(m, dt); break;
            case Doing.Frozen: if (m.Mind.Over) Begin(m, Doing.Hold, 0.4f); break;
            case Doing.Scurry: ThinkScurry(m, dt); break;
            case Doing.Rise: if (m.T >= 0.28f) Begin(m, Doing.Sit, Organic.Jitter(Rng, 3.5f)); break;
            case Doing.Sit: ThinkSit(m, dt); break;
            case Doing.Wash: if (m.Mind.Over || _sendHome > 0f) Begin(m, Doing.Sit, 0.8f + (float)Rng.NextDouble()); break;
            case Doing.Fall: if (m.T >= 0.28f) Begin(m, Doing.Hold, 0.3f + 0.4f * (float)Rng.NextDouble()); break;
            case Doing.Dig: ThinkDig(m, dt); break;
            case Doing.Plunge: ThinkPlunge(m, dt); break;
            case Doing.Under: ThinkUnder(m, dt); break;
            case Doing.Pop: ThinkPop(m, dt); break;
            case Doing.Box: ThinkBox(m, dt); break;
        }
        if (m.Mind.Doing != Doing.Scurry) { m.Dashing = false; }

        // the footprints: a faint dotted line behind a dashing mouse, laid every stride
        if (m.Dashing && m.Speed > 1f) MaybePrint(m);
    }

    // ---------------- Home and Peek ----------------

    private void ThinkHome(Mouse m, float dt)
    {
        m.X = _door.X; m.Y = _door.Y; m.Out = 0f; m.Visible = false;
        m.HomeFor += dt;
        if (!m.Mind.Over) return;
        if (_sendHome > 0f || Lull && m.Curious < 0.5f) { m.Mind.Left = 4f; return; }
        float p = 0.55f + 0.45f * m.Curious; if (m.HomeFor > 18f) p = 1f;      // nobody stays in much over 20 s: a long stay forces a peek
        if (_force != null) p = 1f;                 // a stress dial: every peek is made, and every one commits
        if (Rng.NextDouble() < p) { Roll(m, "peek"); Begin(m, Doing.Peek, 4.2f + (float)Rng.NextDouble()); m.PeekPhase = 0; m.PeekLookDir = Rng.Next(2) == 0 ? 1f : -1f; }
        else m.Mind.Begin(Doing.Home, Math.Min(12f, Organic.Jitter(Rng, 5f)));
    }

    private void ThinkPeek(Mouse m, float dt)
    {
        // t 0..0.8 the nose and whiskers; 0.8..1.7 the head; 1.7..3.2 it looks left, then right; 3.2.. it commits or withdraws
        float t = m.T;
        if (t > 3.3f)
        {
            bool commit = _sendHome <= 0f && !Lull && (m.Bold > 0.25f || Rng.NextDouble() < 0.5 + 0.5f * m.Bold || m.HomeFor > 16f);
            if (_force != null && _sendHome <= 0f && !Lull) commit = true;
            Roll(m, commit ? "commit" : "withdraw");
            if (commit)
            {
                m.HopFrom = _lip; m.HopTo = _door; m.HopT = 0f; m.HopLen = 0.42f; m.Dir = 1;
                Begin(m, Doing.Hop, Mind<Doing>.Forever);
                m.HomeBound = false;
            }
            else { m.Peeks++; m.Mind.Begin(Doing.Home, Math.Min(10f, Organic.Jitter(Rng, 4f))); m.Visible = false; }
        }
    }

    // ---------------- Hop (out of the hole onto the snow, or from the snow into the hole) ----------------

    private void ThinkHop(Mouse m, float dt)
    {
        m.HopT += dt;
        float f = Math.Clamp(m.HopT / m.HopLen, 0f, 1f);
        if (f >= 1f)
        {
            if (m.HomeBound) { m.Carrying = false; m.Visible = false; m.Out = 0f; m.Mind.Begin(Doing.Home, Math.Min(14f, Organic.Jitter(Rng, m.Hungry < 0.4f ? 8f : 5f))); m.HomeFor = 0f; m.Tail.Reset(); return; }
            m.X = m.HopTo.X; m.Y = m.HopTo.Y;                     // it lands where the hop was bound (the door, or a snow spot after a tussle)
            Begin(m, Doing.Hold, 0.7f + (float)Rng.NextDouble());
            if (m.HopFrom.X < _door.X + 4f) m.Tail.Reset();
        }
    }

    // ---------------- Hold: the frozen stop, and the choosing ----------------

    private void ThinkHold(Mouse m, float dt)
    {
        if (!m.Mind.Over) return;
        Decide(m);
    }

    private void Decide(Mouse m)
    {
        bool lull = Lull;
        bool partner = !lull && FindPartner(m) != null;
        float outFor = m.Out;
        float wHome = 0.05f + outFor / 45f + (_sendHome > 0f ? 50f : 0f) + (m.Carrying ? 50f : 0f) + (lull ? 1.2f : 0f);
        float wWander = 1.2f * (_sendHome > 0f ? 0f : 1f) * (0.6f + 0.6f * m.Bold);
        float wSit = (0.7f + 0.9f * m.Curious) * (_sendHome > 0f ? 0f : 1f);
        float wWash = (0.35f + 0.5f * (1f - m.Bold)) * (_sendHome > 0f ? 0f : 1f);
        float wForage = lull || _sendHome > 0f ? 0f : 0.15f + 1.4f * m.Hungry;
        float wTunnel = _sendHome > 0f ? 0f : 0.35f + 0.8f * m.Play;
        float wTussle = partner && _sendHome <= 0f ? 0.4f + 1.0f * m.Play : 0f;
        Span<float> w = stackalloc float[] { wWander, wSit, wWash, wForage, wTunnel, wTussle, wHome, 0f, 0f };
        int pick = Dice.Pick(Rng, w);
        if (_force != null)
            pick = _force switch { "sit" => 1, "wash" => 2, "forage" => 3, "tunnel" => 4, "tussle" => partner ? 5 : 0, "home" => 6, "flee" => 7, "dive" => 8, _ => pick };
        Roll(m, pick switch { 0 => "wander", 1 => "sit", 2 => "wash", 3 => "forage", 4 => "tunnel", 5 => "tussle", 7 => "flee (test)", 8 => "dive (test)", _ => "home" });
        switch (pick)
        {
            case 1: Begin(m, Doing.Rise, 0.3f); break;
            case 2: Begin(m, Doing.Rise, 0.3f); m.Trip = 9; break;      // trip 9 = "wash when you are up"
            case 3: StartForage(m); break;
            case 4: StartTunnel(m); break;
            case 5: StartTussle(m); break;
            case 6: StartHome(m); break;
            case 7: if (Distance(m.X, m.Y, _door.X, _door.Y) < 0.45f * S.U) { StartHome(m); m.Fast = true; } else StartDive(m); break;   // test dial: a flight
            case 8: StartDive(m); break;                                                                                                // test dial: the dive into the snow
            default: StartWander(m); break;
        }
    }

    // ---------------- trips: a goal and the dashes to it ----------------

    private void StartWander(Mouse m)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            float reach = (0.10f + 0.20f * m.Bold) * S.U;
            float gx = m.X + ((float)Rng.NextDouble() * 2f - 1f) * reach * 1.6f;
            float gy = m.Y + ((float)Rng.NextDouble() * 2f - 1f) * reach * 0.35f;
            gx = Math.Clamp(gx, 0.055f * S.Width, 0.67f * S.Width);
            gy = Math.Clamp(gy, LaneLo(gx), LaneHi(gx));
            if (Distance(gx, gy, m.X, m.Y) < 2.5f * StrideAt(m.Y) || !Can(gx, gy) || !LineOk(m.X, m.Y, gx, gy) || Crowded(m, gx, gy, 2.4f * UnitAt(gy))) continue;
            m.GoalX = gx; m.GoalY = gy; m.Trip = 0; m.Fast = false;
            Begin(m, Doing.Scurry, Mind<Doing>.Forever);
            m.Pausing = true; m.PauseLeft = 0.05f;
            return;
        }
        Begin(m, Doing.Hold, 1f + (float)Rng.NextDouble());
    }

    private void StartHome(Mouse m)
    {
        m.GoalX = _door.X; m.GoalY = _door.Y; m.Trip = 2; m.Fast = _sendHome > 0f || m.Carrying;
        Begin(m, Doing.Scurry, Mind<Doing>.Forever);
        m.Pausing = true; m.PauseLeft = 0.05f;
    }

    private void StartForage(Mouse m)
    {
        // the snow under the bird table: spilled seed lies there. A Food notice (a bird's seed) names the very spot.
        PointF foot = S.FeederFoot;
        for (int tries = 0; tries < 16; tries++)
        {
            float gx = foot.X + ((float)Rng.NextDouble() * 2f - 1f) * 0.035f * S.Width;
            float gy = Math.Clamp(foot.Y + (0.02f + 0.08f * (float)Rng.NextDouble()) * S.U, LaneLo(gx), LaneHi(gx));
            if (!Can(gx, gy, 5f) || !LineOk(m.X, m.Y, gx, gy) || Crowded(m, gx, gy, 2.4f * UnitAt(gy))) continue;
            m.GoalX = gx; m.GoalY = gy; m.Trip = 1; m.Fast = false;
            Begin(m, Doing.Scurry, Mind<Doing>.Forever);
            m.Pausing = true; m.PauseLeft = 0.05f;
            return;
        }
        StartWander(m);
    }

    private void ThinkScurry(Mouse m, float dt)
    {
        float stride = m.Dashing ? m.StrideNow : StrideAt(m.Y);
        if (m.Turning)
        {
            m.TurnT += dt;
            if (m.TurnT >= 0.12f && m.TurnT - dt < 0.12f) m.Dir = -m.Dir;       // the face is toward us: swing round here
            if (m.TurnT >= 0.30f) { m.Turning = false; m.Pausing = false; BeginDash(m); }
            return;
        }
        if (m.Pausing)
        {
            m.PauseLeft -= dt;
            if (m.PauseLeft > 0f) return;
            if (_sendHome > 0f && m.Trip != 2 && m.Trip != 3) { StartHome(m); return; }
            // arrived?
            float toGoal = Distance(m.X, m.Y, m.GoalX, m.GoalY);
            // (a mouse going home hops in from up to a stride and a half away: a dash is two strides at least and would overshoot the door)
            if (toGoal < (m.Trip == 2 ? 1.6f : 0.9f) * stride) { ArriveAtGoal(m); return; }
            // the goal is behind: a REVERSAL IS A TURN
            float dx = m.GoalX - m.X;
            if (MathF.Abs(dx) > 0.5f * stride && MathF.Sign(dx) != m.Dir) { m.Turning = true; m.TurnT = 0f; return; }
            BeginDash(m);
            return;
        }
        if (!m.Dashing) { m.Pausing = true; m.PauseLeft = 0.05f; return; }

        // ---- in the middle of a dash: the odometer is in STRIDES ----
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
        if (dist < (m.Trip == 2 ? 1.6f : 0.4f) * stride) { ArriveAtGoal(m); return; }
        int strides = (int)Math.Clamp(MathF.Round(dist / stride), 2f, m.Fast ? 8f : 5f);
        // do not overshoot: a dash that would pass the goal by over a stride is shortened
        if (strides * stride > dist + 0.6f * stride && strides > 2) strides--;
        float ux = dx / dist, uy = dy / MathF.Max(dist, 1e-3f);
        float ex = m.X + ux * strides * stride, ey = m.Y + uy * strides * stride;
        if (!LineOk(m.X, m.Y, ex, ey))
        {
            // The way is not clear (a prop in the line). GO ROUND IT, the way the patch's critters do (30 and 60 degrees
            // either side, never 90): the first heading whose whole dash is clear is taken. Only when none is, the trip is
            // given up: a flight dives into the snow where it stands, anything else holds still and rolls again.
            bool found = false;
            ReadOnlySpan<float> turns = [0f, 0.5f, -0.5f, 1.0f, -1.0f, 1.4f, -1.4f, 1.8f, -1.8f];
            foreach (float a in turns)
            {
                float c = MathF.Cos(a), sn = MathF.Sin(a);
                float rx = ux * c - uy * sn, ry = ux * sn + uy * c;
                // (a steep turn is allowed here: the mouse may be standing at the very edge of a prop's foot and must step straight out of it)
                for (int n = strides; n >= 2 && !found; n--)           // a shorter dash may clear what a long one does not
                {
                    float tx = m.X + rx * n * stride, ty = m.Y + ry * n * stride;
                    if (ty < LaneLo(tx) - 40f || ty > LaneHi(tx) + 40f || !LineOk(m.X, m.Y, tx, ty)) continue;
                    ux = rx; uy = ry; ex = tx; ey = ty; strides = n; found = true;
                }
                if (found) break;
            }
            if (!found)
            {
                string why = "";
                for (int i = 1; i <= 40 && why == ""; i++)
                {
                    float fx = m.X + (ex - m.X) * i / 40f, fy = m.Y + (ey - m.Y) * i / 40f;
                    if (!Can(fx, fy)) why = FormattableString.Invariant($"first bad point {fx:F0},{fy:F0} onpond {S.OnPond(fx, fy, 1.08f)} canstand {S.CanStand(fx, fy, 3f)} lo {LaneLo(fx):F0}");
                }
                if (_log != null) Roll(m, FormattableString.Invariant($"blocked at {m.X:F0},{m.Y:F0} trip {m.Trip} {why}"));
                m.Remaining = 0;
                if (m.Fast && m.Trip == 2 && Distance(m.X, m.Y, _door.X, _door.Y) > 0.12f * S.U) StartDive(m);
                else Begin(m, Doing.Hold, 0.8f);
                m.Trip = 0;
                return;
            }
        }
        m.StrideNow = stride; m.Ux = ux; m.Uy = uy; m.Remaining = strides;
        m.DashEndX = ex; m.DashEndY = ey;
        m.Pace.Walked = 0f; m.Pace.Halt();
        float sp = (m.Fast ? 0.30f : 0.115f + 0.03f * m.Bold) * S.U / stride;   // strides a second
        m.Pace.Accel = sp * 5f; m.Pace.Want = sp;
        m.Dashing = true; m.Pausing = false; m.Dir = ux >= 0 ? 1 : -1;
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
            default: Begin(m, Doing.Hold, Organic.Jitter(Rng, 1.6f)); break;
        }
    }

    private void EnterHole(Mouse m)
    {
        // it is at the door: turn to face the hole (left), then hop in
        m.Dir = -1;
        m.HopFrom = new PointF(m.X, m.Y);                 // the hop starts where the mouse IS: no snap to the door
        m.HopTo = _lip; m.HopT = 0f; m.HopLen = 0.42f; m.HomeBound = true;
        Begin(m, Doing.Hop, Mind<Doing>.Forever);
    }

    // ---------------- Sit and Wash ----------------

    private void ThinkSit(Mouse m, float dt)
    {
        if (m.Trip == 9 && m.T > 0.6f) { m.Trip = 0; Begin(m, Doing.Wash, 2.6f + 1.6f * (float)Rng.NextDouble()); return; }
        if (m.Mind.Over || _sendHome > 0f && m.T > 0.5f) Begin(m, Doing.Fall, 0.28f);
    }

    // ---------------- Dig (the forager finds a seed) ----------------

    private void ThinkDig(Mouse m, float dt)
    {
        // t 0..1.1 the nose in the snow, snow flicking; then up with a seed in its mouth
        if (m.T > 0.2f && m.T - dt <= 0.2f) Puff(m.X + m.Dir * 6f, m.Y - 2f, m.Dir * 20f, -30f, 0);
        if (m.T > 0.55f && m.T - dt <= 0.55f) Puff(m.X + m.Dir * 6f, m.Y - 2f, m.Dir * 24f, -34f, 1);
        if (m.T >= 1.25f)
        {
            m.Carrying = true; m.Hungry = Math.Max(0f, m.Hungry - 0.5f);
            Roll(m, "found a seed");
            StartHome(m); m.Fast = true;
        }
    }

    // ---------------- Tunnel: plunge, travel under the snow, pop up ----------------

    private void StartTunnel(Mouse m)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            float len = (0.10f + 0.12f * (float)Rng.NextDouble()) * S.U;
            float ang = ((float)Rng.NextDouble() * 2f - 1f) * 0.4f + (Rng.Next(2) == 0 ? 0f : MathF.PI);
            float gx = m.X + len * MathF.Cos(ang), gy = m.Y + len * 0.5f * MathF.Sin(ang);
            gx = Math.Clamp(gx, 0.06f * S.Width, 0.66f * S.Width); gy = Math.Clamp(gy, LaneLo(gx), LaneHi(gx));
            if (!Can(gx, gy, 6f) || !LineOk(m.X, m.Y, gx, gy)) continue;
            m.UnderFrom = new PointF(m.X + m.Dir * 0.55f * UnitAt(m.Y), m.Y); m.UnderTo = new PointF(gx, gy);
            if (!Can(m.UnderFrom.X, m.UnderFrom.Y, 4f)) m.UnderFrom = new PointF(m.X, m.Y);
            m.UnderLen = Distance(m.UnderFrom.X, m.UnderFrom.Y, gx, gy);
            m.UnderDur = Math.Max(1.2f, m.UnderLen / (0.07f * S.U));
            Begin(m, Doing.Plunge, 0.7f);
            m.Dir = m.Dir;
            return;
        }
        StartWander(m);
    }

    private void ThinkPlunge(Mouse m, float dt)
    {
        // t 0..0.32 a hop up (the leap page) and over; 0.32..0.62 nose first into the snow; the mound grows over it
        if (m.T >= 0.32f && m.T - dt < 0.32f) { Puff(m.UnderFrom.X, m.UnderFrom.Y - 1f, -8f, -34f, 1); Puff(m.UnderFrom.X, m.UnderFrom.Y - 1f, 8f, -30f, 0); }
        if (m.T >= 0.70f)
        {
            m.UnderT = 0f; Begin(m, Doing.Under, m.UnderDur);
            m.X = m.UnderFrom.X; m.Y = m.UnderFrom.Y;
        }
    }

    private void ThinkUnder(Mouse m, float dt)
    {
        m.UnderT += dt;
        float f = Math.Clamp(m.UnderT / m.UnderDur, 0f, 1f);
        m.X = m.UnderFrom.X + (m.UnderTo.X - m.UnderFrom.X) * f; m.Y = m.UnderFrom.Y + (m.UnderTo.Y - m.UnderFrom.Y) * f;
        m.Speed = m.UnderLen / m.UnderDur;
        // the groove it leaves: a small dimple every few pixels
        // the furrow it leaves: a faint dimpled line, a dot every couple of pixels, fading over a few seconds
        if (m.UnderT <= dt * 1.5f) { m.FurrowX = m.X; m.FurrowY = m.Y; }
        while (Distance(m.X, m.Y, m.FurrowX, m.FurrowY) >= 2.2f)
        {
            float d = Distance(m.X, m.Y, m.FurrowX, m.FurrowY);
            m.FurrowX += (m.X - m.FurrowX) / d * 2.2f; m.FurrowY += (m.Y - m.FurrowY) / d * 2.2f;
            _prints[_printAt] = new Print { X = m.FurrowX, Y = m.FurrowY + 1.2f, Age = 0f, On = true, Furrow = true }; _printAt = (_printAt + 1) % _prints.Length;
        }
        if (f >= 1f) { Begin(m, Doing.Pop, 1.6f); m.Dir = m.UnderTo.X >= m.UnderFrom.X ? 1 : -1; }
    }

    private void ThinkPop(Mouse m, float dt)
    {
        // the mound sags, the head comes up with snow on it, looks, and the mouse climbs out and shakes
        if (m.T >= 0.35f && m.T - dt < 0.35f)
        {
            Puff(m.X, m.Y - 4f, -10f, -40f, 0); Puff(m.X, m.Y - 4f, 12f, -44f, 1);
            for (int i = 0; i < 9; i++) Puff(m.X + ((float)Rng.NextDouble() - 0.5f) * 6f, m.Y - 5f, ((float)Rng.NextDouble() - 0.5f) * 90f, -70f - 50f * (float)Rng.NextDouble(), 2);   // a few specks flung up
        }
        if (m.T >= 1.6f)
        {
            Begin(m, Doing.Hold, 0.6f + (float)Rng.NextDouble());
            Puff(m.X, m.Y - 2f, 0f, -30f, 1);
        }
    }

    // ---------------- Tussle ----------------

    /// <summary>True if another mouse that is out stands (or is bound) within "r" pixels of (x, y): no two mice share a spot.</summary>
    private bool Crowded(Mouse me, float x, float y, float r)
    {
        foreach (Mouse o in _mice)
        {
            if (o == me || o.Mind.Doing is Doing.Home or Doing.Peek) continue;
            if (Distance(o.X, o.Y, x, y) < r) return true;
            if (o.Mind.Doing == Doing.Scurry && Distance(o.GoalX, o.GoalY, x, y) < r) return true;
        }
        return false;
    }

    private Mouse? FindPartner(Mouse m)
    {
        if (m.Mind.Doing != Doing.Hold) return null;
        foreach (Mouse o in _mice)
        {
            if (o == m || o.Partner != null) continue;
            if (o.Mind.Doing is not (Doing.Hold or Doing.Sit)) continue;
            if (Distance(o.X, o.Y, m.X, m.Y) > 0.30f * S.U) continue;
            if (MathF.Abs(o.Y - m.Y) > 0.05f * S.U) continue;
            return o;
        }
        return null;
    }

    private void StartTussle(Mouse m)
    {
        Mouse? o = FindPartner(m);
        if (o == null) { StartWander(m); return; }
        m.Partner = o; o.Partner = m; m.Starter = true; o.Starter = false;
        // it walks to a point a mouse-length and a half from the other, on the side it is on
        int side = m.X < o.X ? -1 : 1;
        m.GoalX = o.X + side * 1.5f * UnitAt(o.Y); m.GoalY = o.Y; m.Trip = 3; m.Fast = false;
        if (!Can(m.GoalX, m.GoalY, 4f) || !LineOk(m.X, m.Y, m.GoalX, m.GoalY)) { m.Partner = null; o.Partner = null; StartWander(m); return; }
        Begin(m, Doing.Scurry, Mind<Doing>.Forever); m.Pausing = true; m.PauseLeft = 0.05f;
        // the other one stands up and waits for it, facing it
        o.Dir = side == -1 ? -1 : 1; o.Dir = o.X < m.X ? 1 : -1;
        Begin(o, Doing.Rise, 0.28f); o.Trip = 8;      // trip 8 = "waiting for a tussle: sit up and stay"
    }

    private void BeginBoxApproach(Mouse m)
    {
        Mouse? o = m.Partner;
        if (o == null || o.Mind.Doing is not (Doing.Sit or Doing.Rise)) { AbortTussle(m); return; }
        m.Dir = m.X < o.X ? 1 : -1; o.Dir = -m.Dir;
        Begin(m, Doing.Rise, 0.28f); m.Trip = 7;       // trip 7 = "rise and box"
    }

    private void AbortTussle(Mouse m)
    {
        if (m.Partner != null) { Mouse o = m.Partner; if (o.Partner == m) o.Partner = null; if (o.Mind.Doing is Doing.Sit or Doing.Rise) { o.Trip = 0; Begin(o, Doing.Fall, 0.28f); } }
        m.Partner = null;
        Begin(m, Doing.Hold, 0.6f);
    }

    private void ThinkBox(Mouse m, float dt)
    {
        Mouse? o = m.Partner;
        if (o == null) { Begin(m, Doing.Fall, 0.28f); return; }
        if (m.Mind.Over)
        {
            // the loser hops back, the winner drops to all fours
            if (m.Starter)
            {
                int away = m.X < o.X ? -1 : 1;
                float hx = Math.Clamp(m.X + away * 3.2f * UnitAt(m.Y), 0.055f * S.Width, 0.67f * S.Width);
                if (!Can(hx, m.Y, 4f) || !LineOk(m.X, m.Y, hx, m.Y)) hx = m.X;
                m.HopFrom = new PointF(m.X, m.Y); m.HopTo = new PointF(hx, m.Y); m.HopT = 0f; m.HopLen = 0.34f; m.HomeBound = false; m.Dir = -away;
                m.Partner = null; o.Partner = null;
                Begin(m, Doing.Hop, Mind<Doing>.Forever);
                Begin(o, Doing.Fall, 0.28f); o.Trip = 0;
                // after a hop that lands on the snow (not at the door), the mouse holds (see ThinkHop: it snaps to the door only when it is at the log)
            }
        }
    }

    // ---------------- Reactions ----------------

    private void React(Mouse m, Reaction r, Notice cause)
    {
        Doing d = m.Mind.Doing;
        if (m.Dashing) return;                     // a dash is not cut: the reaction comes at the next stop (the cooldown keeps it for a second or two)
        if (_log != null) Roll(m, $"react {r} {cause.Kind}");       // no string is built when nobody is logging
        switch (r)
        {
            case Reaction.Flee:
                if (d is Doing.Box or Doing.Sit or Doing.Wash or Doing.Rise) { if (m.Partner != null) AbortTussle(m); }
                {
                    float toDoor = Distance(m.X, m.Y, _door.X, _door.Y);
                    if (toDoor < 0.45f * S.U || m.Carrying) { StartHome(m); m.Fast = true; m.Carrying = false || m.Carrying; }
                    else StartDive(m);
                }
                if (m.Speak <= 0f) { Board.Post(Sense.Friend, new PointF(m.X, m.Y), 0.20f * S.U, 0.4f, this); m.Speak = 3f; }
                break;
            case Reaction.Freeze:
                if (d is Doing.Hold or Doing.Frozen) Begin(m, Doing.Frozen, 1.2f + 1.5f * (float)Rng.NextDouble());
                else if (d is Doing.Sit or Doing.Wash) { if (d == Doing.Wash) Begin(m, Doing.Sit, 1.5f); }
                break;
            case Reaction.Approach:
                if (cause.Kind == Sense.Food && d == Doing.Hold && !Lull && m.Hungry > 0.3f) StartForage(m);
                break;
            case Reaction.Look:
                if (d == Doing.Hold && m.Curious > 0.4f && cause.At.X != m.X) m.Dir = cause.At.X > m.X ? 1 : -1;
                break;
        }
    }

    private void StartDive(Mouse m)
    {
        // a frightened mouse far from home dives into the snow where it stands and stays under, then peeks
        float len = 0.06f * S.U;
        m.UnderFrom = new PointF(m.X, m.Y);
        float gx = m.X + m.Dir * len; if (!Can(gx, m.Y, 6f)) gx = m.X;
        m.UnderTo = new PointF(gx, m.Y);
        m.UnderLen = len; m.UnderDur = 3.5f + 3f * (float)Rng.NextDouble();
        Begin(m, Doing.Plunge, 0.7f);
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
            if (_prints[i].Age > (_prints[i].Furrow ? 5f : 7f)) _prints[i].On = false;
        }
    }

    // ============================================================
    //  WHAT TO DRAW THIS FRAME
    // ============================================================

    private void PrepareDraw(Mouse m, float dt)
    {
        LogMiceArt.Book book = LogMiceArt.Book.Stand; int page = 0;
        m.Visible = true; m.UseStencil = false; m.MoundOn = false; m.HeadOnly = false; m.Ridge = false;
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
                // the head rises in the hollow: a nose first, then the whole face, looks both ways, sinks if it withdraws
                float t = m.T;
                float rise = t < 0.8f ? Organic.Smooth(t / 0.8f) * 0.55f : t < 1.7f ? 0.55f + 0.45f * Organic.Smooth((t - 0.8f) / 0.9f) : 1f;
                if (t > 3.15f && m.PeekPhase == 0) { }
                float sink = t > 3.15f ? Organic.Smooth((t - 3.15f) / 0.5f) : 0f;
                book = LogMiceArt.Book.Front; page = blink ? 1 : t > 1.7f && t < 1.95f ? 2 : 0;
                m.HeadOnly = true; m.HeadRise = (1f - rise + sink * 0.3f) * 1.0f;
                float look = t < 1.7f ? 0f : t < 2.4f ? m.PeekLookDir * Organic.Smooth((t - 1.7f) / 0.4f) : -m.PeekLookDir * Organic.Smooth((t - 2.4f) / 0.5f) * 1f;
                m.DrawX = S.LogHollow.X + look * 0.22f * k; m.DrawY = _lip.Y + (1f - rise) * 1.05f * k + sink * 0.5f * k;
                m.UseStencil = true; m.Mirror = false;
                break;
            }
            case Doing.Hop:
            {
                float f = Math.Clamp(m.HopT / m.HopLen, 0f, 1f);
                m.DrawX = m.HopFrom.X + (m.HopTo.X - m.HopFrom.X) * f;
                m.DrawY = m.HopFrom.Y + (m.HopTo.Y - m.HopFrom.Y) * f - 0.55f * k * MathF.Sin(MathF.PI * f);
                book = LogMiceArt.Book.Leap; page = f < 0.5f ? 0 : 1;
                bool atLog = m.HopFrom.X < _door.X + 4f || m.HopTo.X < _door.X + 4f;
                m.UseStencil = atLog;
                m.Mirror = m.Dir < 0;
                m.Speed = Distance(m.HopFrom.X, m.HopFrom.Y, m.HopTo.X, m.HopTo.Y) / m.HopLen;
                break;
            }
            case Doing.Hold: case Doing.Frozen:
                book = LogMiceArt.Book.Stand; page = blink ? 4 : m.EarLeft > 0f ? 5 : sniff;
                if (m.Mind.Doing == Doing.Frozen) page = blink ? 4 : 0;
                break;
            case Doing.Scurry:
                if (m.Turning)
                {
                    if (m.TurnT >= 0.07f && m.TurnT < 0.26f) { book = LogMiceArt.Book.Front; page = 0; m.Mirror = false; }
                    else page = 0;
                }
                else if (m.Dashing) { book = LogMiceArt.Book.Run; page = m.Pace.Page(1f, LogMiceArt.RunPages); }
                else { book = LogMiceArt.Book.Stand; page = blink ? 4 : m.EarLeft > 0f ? 5 : sniff; }
                break;
            case Doing.Rise: { book = LogMiceArt.Book.Rise; page = Math.Min(LogMiceArt.RisePages - 1, (int)(m.T / 0.28f * LogMiceArt.RisePages)); break; }
            case Doing.Fall: { book = LogMiceArt.Book.Rise; page = Math.Max(0, LogMiceArt.RisePages - 1 - (int)(m.T / 0.28f * LogMiceArt.RisePages)); break; }
            case Doing.Sit: book = LogMiceArt.Book.Sit; page = blink ? 4 : m.EarLeft > 0f ? 5 : sniff; if (m.Trip == 7) { Begin2Box(m); } break;
            case Doing.Wash: book = LogMiceArt.Book.Wash; page = ((int)(m.T * 9f)) % LogMiceArt.WashPages; break;
            case Doing.Box: book = LogMiceArt.Book.Box; page = ((int)(m.T * 8f + (m.Starter ? 0 : 3))) % LogMiceArt.BoxPages; break;
            case Doing.Dig:
                book = LogMiceArt.Book.Stand; page = m.T < 1.1f ? (((int)(m.T * 9f)) % 2 == 0 ? 7 : 6) : 6;
                break;
            case Doing.Plunge:
            {
                float t = m.T;
                if (t < 0.32f)
                {
                    float f = t / 0.32f;
                    m.DrawX = m.X + (m.UnderFrom.X - m.X) * f; m.DrawY = m.Y - 0.5f * k * MathF.Sin(MathF.PI * f * 0.9f) + (f > 0.8f ? 0 : 0);
                    book = LogMiceArt.Book.Leap; page = f < 0.5f ? 0 : 1;
                }
                else
                {
                    float f = Math.Clamp((t - 0.32f) / 0.30f, 0f, 1f);
                    book = LogMiceArt.Book.Plunge; page = Math.Min(3, (int)(f * 4f));
                    m.DrawX = m.UnderFrom.X + m.Dir * 0.1f * k; m.DrawY = m.UnderFrom.Y + f * 0.35f * k;
                    m.MoundOn = true; m.MoundGrow = f; m.MoundX = m.UnderFrom.X; m.MoundY = m.UnderFrom.Y;
                    if (f >= 1f) m.Visible = false;
                }
                break;
            }
            case Doing.Under:
                m.Visible = false; m.MoundOn = true; m.Ridge = true; m.MoundGrow = 0.7f + 0.15f * MathF.Sin(m.UnderT * 11f); m.MoundX = m.X; m.MoundY = m.Y;
                break;
            case Doing.Pop:
            {
                float t = m.T;
                // the head comes up behind the sagging mound
                float up = t < 0.9f ? Organic.Smooth(t / 0.9f) : 1f;
                book = LogMiceArt.Book.Front; page = 3; m.HeadOnly = true;
                m.DrawX = m.X + (t > 0.9f && t < 1.3f ? MathF.Sin((t - 0.9f) * 18f) * 0.05f * k : 0f);
                m.DrawY = m.Y + 0.95f * k * (1f - up) + 0.1f * k;
                m.MoundOn = true; m.MoundGrow = t < 0.9f ? 0.6f : 0.6f - 0.35f * Organic.Smooth((t - 0.9f) / 0.7f); m.MoundX = m.X; m.MoundY = m.Y;
                if (t > 1.3f) page = blink ? 1 : 0;
                break;
            }
        }
        m.Book = book; m.Page = page; m.Size = SizeIdx(m.Y);

        // ---- the tail: rooted on the page's rump, dragged along the path ----
        if (m.Visible && !m.HeadOnly && _sheets[m.Size] != null)
        {
            MouseAnchors a = _sheets[m.Size].Anchor(book, page);
            float kk = _sheets[m.Size].K;
            float f = m.Mirror ? -1f : 1f;
            var root = new PointF(m.DrawX + f * a.Tail.X * kk, m.DrawY - a.Tail.Y * kk);
            m.Tail.Update(root, f, m.DrawY, 0.30f * kk, dt, m.Speed / MathF.Max(1f, 9f * kk));
        }
    }

    private void Begin2Box(Mouse m)
    {
        // a mouse waiting to box: once both are risen the starter begins the bout
        Mouse? o = m.Partner;
        if (o == null) return;
        if (m.Starter && o.Mind.Doing == Doing.Sit && m.T > 0.4f)
        {
            Roll(m, "box");
            m.Trip = 0; o.Trip = 0;
            Begin(m, Doing.Box, 1.4f + 0.6f * (float)Rng.NextDouble());
            Begin(o, Doing.Box, 3f);
            // a post: a scuffle is something to hear
            Board.Post(Sense.Movement, new PointF(m.X, m.Y), 0.18f * S.U, 0.35f, this);
        }
        else if (!m.Starter && m.T > 6f) { Mouse mm = m; if (mm.Partner != null) mm.Partner.Partner = null; mm.Partner = null; Begin(mm, Doing.Fall, 0.28f); }
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
            if (!m.Visible && !m.MoundOn) continue;
            q.Add(m.Y, this, i);
        }
        Decor.Collect(q, this);
        q.Add(100000f, this, 1000);            // the snow thrown up and the prints: over everything on the ground
    }

    public override void DrawOne(FrameBuffer fb, int index)
    {
        if (index == 1000) { DrawParticles(fb); return; }
        if (index >= 2000) { Decor.DrawIndex(fb, index - 2000); return; }
        Mouse m = _mice[index];
        LogMiceArt.Sheet sh = _sheets[m.Size];
        if (sh == null) return;
        float k = sh.K;
        bool atHole = m.UseStencil;
        byte[]? depth = atHole ? null : S.NearDepth;
        int near = atHole ? 254 : S.Nearness(m.Y);
        bool[]? st = atHole ? _stencil : null;

        // the tail first (the body covers its root); not for a head popping up
        if (m.Visible && !m.HeadOnly && m.Mind.Doing != Doing.Home)
            m.Tail.Draw(fb, MathF.Max(1f, 0.085f * k), depth, near, st);

        if (m.Visible)
        {
            Sprite page = sh.Page(m.Book, m.Page);
            int left = (int)MathF.Round(m.DrawX) - (m.Mirror ? page.Width - 1 - sh.OriginX : sh.OriginX), top = (int)MathF.Round(m.DrawY) - sh.OriginY;
            page.Draw(fb, left, top, 1f, st, m.Mirror, depth, near);
            // a seed in the mouth
            if (m.Carrying && !m.HeadOnly && _seed != null && m.Mind.Doing != Doing.Dig)
            {
                MouseAnchors a = sh.Anchor(m.Book, m.Page);
                float f = m.Mirror ? -1f : 1f;
                _seed.DrawCentered(fb, m.DrawX + f * (a.Mouth.X + 0.05f) * k, m.DrawY - a.Mouth.Y * k, 1f, st, m.Mirror, depth, near);
            }
        }
        if (m.MoundOn)
        {
            if (m.Ridge && _ridges[m.Size] != null)
            {
                // a narrow raised ridge with a lit top and a blue shadow line, bobbing a little as the mouse pushes along under it
                Sprite rd = _ridges[m.Size];
                int lift = (int)MathF.Round(0.04f * k * (m.MoundGrow - 0.7f) * 8f);
                S.Stamp(fb, rd, m.MoundX, m.MoundY + 0.05f * k, rd.Width / 2, rd.Height - 1 - (int)(0.10f * k) + lift, m.Dir < 0, 1f);
            }
            else
            {
            Sprite[] ms = _mounds[m.Size];
            int gi = Math.Clamp((int)(m.MoundGrow * 4f), 0, 3);
            if (m.MoundGrow > 0.03f)
                S.Stamp(fb, ms[gi], m.MoundX, m.MoundY + 0.05f * k, ms[gi].Width / 2, ms[gi].Height - 1 - (int)(0.11f * k), false, 1f);
            }
        }
    }

    private void DrawParticles(FrameBuffer fb)
    {
        // footprints and groove dimples: small pale-blue dots that fade, kept off the props
        for (int i = 0; i < _prints.Length; i++)
        {
            if (!_prints[i].On) continue;
            float a = (_prints[i].Furrow ? 0.62f : 0.5f) * (1f - _prints[i].Age / (_prints[i].Furrow ? 5f : 7f));
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
