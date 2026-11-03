using System.Globalization;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The freshwater eel (unagi, Anguilla japonica) that lives INSIDE the old sunken log's hollow on the deep bed (round
/// forty-six, 2026-11-02). His line: "the underwater log looks less like a log than it does a log shaped pile of
/// sediment. try that log again. and let animals go inside. maybe some new type of fish or crab or some other freshwater
/// thing I can't think of right now but is super interesting lives in there? I'm trying to create a fun and interesting
/// little ecosystem here."
///
/// THIS CLASS IS THE WRAPPER (the public surface the scene is wired to, unchanged from the stub): one animal when the knob
/// is 1 and the pond has a sunken log whose hollow's mouth is free deep bed in view, otherwise nothing is made, nothing is
/// drawn, and every call returns at once. The animal is EelBody below. The Random handed in is the eel's OWN: it is read
/// only when there is an eel to make, and only in the constructor and at decisions, so the scene's rolls do not move when
/// the eel is added or taken away.
///
/// WORLD LAW, in the pond's own terms (his: "nothing can clip through anything in this universe"). The eel is SOLID TO THE
/// BED (it is laid on it, one hair under the bed's depth cap), and to the LOG: the log is a wall to every free swimming
/// step, and the only way through it is the hollow's mouth, which the eel takes along the log's own axis (a doorway, not a
/// hole in the wall). It PASSES UNDER every fish (drawn before them) and OVER the deep dwellers (drawn after them: a known
/// pass, listed in the docs). It is never drawn over the log's pixels: inside the hollow it is at the depth of the bed the
/// log lies on, so the depth test (Pond.Near) hides it behind the log's drawn surface with no stencil of its own.
/// </summary>
internal sealed class Eel
{
    private readonly EelBody? _body;

    /// <param name="pond">The painted pond: the log (Pond.Sunken), the bed (Floor), the stacked room grid, the depth test (Near).</param>
    /// <param name="view">Which water pixels are in view (under the boughs' still parts is not).</param>
    /// <param name="minnows">The minnow schools, or null: a dipped school is what it hunts (Minnows.Take).</param>
    /// <param name="deep">The deep bed's crayfish and prawns: the dweller homed at the log's mouth shares the hollow, and a prawn is a meal (ISnackField).</param>
    /// <param name="count">The knob "Eel": 0 none, 1 one.</param>
    /// <param name="rng">The eel's OWN Random.</param>
    public Eel(Pond pond, bool[] view, Minnows? minnows, DeepDwellers deep, int count, Random rng)
    {
        if (count > 0 && pond.Sunken is SunkenLog log) _body = EelBody.TryMake(pond, view, minnows, deep, log, rng);
    }

    /// <summary>Is there an eel at all (the knob is on and the pond has a sunken log with its hollow on open deep bed)?</summary>
    public bool Lives => _body != null;

    /// <summary>Where its head is when any of it is out of the log (ground pixels, for the minnows and the dwellers to fear); null while it is inside or there is no eel.</summary>
    public PointF? Head => _body?.Head;

    /// <summary>Once a frame. threats: every koi's head, the turtles, the diving snake's head (the same list the deep dwellers get).</summary>
    public void Update(float dt, PointF[] threats) => _body?.Update(dt, threats);

    /// <summary>On the deep bed: right after the deep dwellers, under every fish, through the depth test (Pond.Near hides what is inside the log).</summary>
    public void DrawDeep(FrameBuffer fb) => _body?.DrawDeep(fb);
}

/// <summary>
/// The eel itself. WATCH THE ANIMAL FIRST. A Japanese eel in a pond is a long round muscle: thickest a third of the way
/// back, tapering to a flat paddle of a tail, a low fin seam running along the back from the middle and round the tail's
/// tip, a small flattened head with a blunt snout and a lower jaw that sticks out a little, two small dark eyes, and a pale
/// throat. Olive-brown on the back, going to a pale yellowish belly, no pattern. By day it lies INSIDE a hollow (a log, a
/// pipe, under a stone) with only its head showing at the mouth, breathing: the gill covers and the jaw work slowly. It
/// comes out to forage in an S that runs down its whole body from the head, bigger toward the tail, the head almost still
/// (a snake's way, but slower and rounder), along the bed with its nose down. It can swim backward a little, and it goes
/// into a hollow TAIL FIRST: the body slides in until only the head shows.
///
/// HOW THE BODY WORKS (read this first; it is the one new idea). The snake lays its body along the trail the head swam.
/// That cannot go backward, so the eel's body is a ROPE: twenty-two points a fixed distance apart, and ONE END LEADS.
/// Push the leader and every point behind it is pulled along, each to its fixed distance from the one in front of it. A
/// rope pulled by its head follows the head's path like the snake (a tractrix, if you want the word); the same rope
/// pulled by its TAIL follows the tail's path, which is exactly "backing in", with no second body to write. Walking the
/// rope costs one square root a point. The S-wave you see is NOT in the rope: it is a sideways offset added when the spine
/// is laid for drawing, so the rope stays smooth and the wave can be turned up, down or off (off inside the hollow, where
/// it would push the body into the log's wall).
///
/// THE HOLLOW is the log's near end, a corridor along the log's own axis. Everything about it is in ONE little frame: Q is
/// how far OUT of the log's cut face a point is along the axis (0 at the face, negative inside the log), LAT is how far
/// to the side. The mouth is the log's Den (the painter's own number, Q = 0.6 of the log's thickness out). Going in and out
/// is KINEMATIC (the leader is placed on the axis, no steering and no feelers): the corridor is a doorway, and a step rule
/// that refuses the log's wall must not also refuse the door.
///
/// THE LOG IS A WALL to every free step (LogDist is the distance to the log's capsule, in the painter's pixels, with a
/// margin that holds the wave's swing off it), except in the doorway. The bed's own walls are the CATFISH'S rule: one fixed
/// floor, 0.6 of the depth (the bank's slope, the boulders' sides and the upper halves of the mounds are walls; hollows
/// and skirts it follows with the bed cap), and it NEVER answers a wall with a climb: it lives within a hair of its cap.
///
/// THE MIND (Mind&lt;Doing&gt;; the recipe is docs/MINDS.md). Two dials dealt at birth, BOLD (out longer, shies from nearer
/// threats only) and HUNGRY (forages more, sees prey from further; a meal takes the edge off). Hide (inside, the head at
/// the mouth, breathing, one to three minutes), Peek (the head out a little further, looking about), Emerge (the body
/// slides out head first), Forage (an S along the deep bed, a goal at a time, within half a unit of the mouth), Snap (the
/// shared Snatch picks a prawn, or a dipped minnow strays within reach; a sideways flick of the head and a take), Approach
/// and Back (home: swim to a spot straight out from the mouth, then back in TAIL FIRST), Withdraw (a threat: the quick slide
/// back inside, a second) and Flee (out in the open and frightened: a fast tight S for the mouth, then the same back-in).
/// </summary>
internal sealed class EelBody : Body
{
    // ------------------------------------------------------------------ instruments (permanent, free when unset)
    /// <summary>The instrument log (PECKWORKS_EELLOG names a file): one line a frame, the koi log's first nineteen columns, then the drawn spine as x:y pairs, then the eel's own. Null in the real pond.</summary>
    public static Action<string>? MotionLog;
    /// <summary>Instruments only (PECKWORKS_EELRATE): how much oftener than the real pond it leaves the hollow (the Hide timer divided by this). 1 in the real pond.</summary>
    public static float Rate = 1f;

    /// <summary>How far above the bed it lies, in Z: its round body's own radius (0.05 of its length, 0.014 of the depth) and a hair. THE EEL LIES ON THE BED, NOT AT THE KOI'S CAP: the cap (0.9 of the floor less 0.03) leaves a koi a margin of water, which is 0.12 of the depth on a typical bed, and at that depth a thing is SHALLOWER than the sunken log's top (the bed less the log's height, 0.09), so the depth test (Pond.Near) would draw the eel over the log it is meant to be inside. At the bed's own depth less this, inside the hollow it is deeper than the log's top and the log hides it.</summary>
    private const float LieZ = 0.025f;
    private readonly float[] _zTest = new float[N], _inside = new float[N];   // the depth the painter's hide-behind test is given per point: inside the hollow it is 0.25 deeper than the body's own, so the log's thin rounded end still stands in front of it (at the body's depth 255 to 400 frames a run drew spine points through the wood just behind the cut face)

    private enum Doing { Hide, Peek, Emerge, Forage, Snap, Approach, Back, Withdraw, Flee }

    private const int N = 22;                                  // spine points (the koi log's width: the kit's kink41.py reads the eel's log as it is)
    private static readonly float[] AlongAt = [0f, 0.012f, 0.03f, 0.05f, 0.07f, 0.10f, 0.14f, 0.18f, 0.23f, 0.28f, 0.34f, 0.40f, 0.46f, 0.52f, 0.58f, 0.64f, 0.70f, 0.76f, 0.82f, 0.88f, 0.94f, 1f];

    private readonly Pond _pond;
    private readonly DeepBed _bed;
    private readonly Minnows? _minnows;
    private readonly DeepDwellers _deep;
    private readonly SunkenLog _log;
    private readonly Random _rng;
    private readonly Mind<Doing> _mind = new(Doing.Hide);
    private readonly Snatch _snatch;
    private readonly Cooldown _reactCool = new(), _snapCool = new();
    private readonly float _u, _len, _halfW, _bold, _hungryDial;
    private readonly float[] _seg = new float[N - 1];
    private readonly bool[] _view;

    // The hollow's frame, in GROUND pixels (the pond squashed straight): the axis points OUT of the log.
    private readonly float _tx, _ty, _ax, _ay, _nx, _ny;      // the log's cut face (its near end's tip), the outward unit, the sideways unit
    private readonly float _wg, _hwg;                          // the log's thickness and half thickness in ground pixels
    private readonly float _mouthQ;                            // the Den's Q (0.6 of the thickness out)
    private readonly float _zIn;                               // the depth of everything inside: the bed outside the mouth, held for the whole inside
    private readonly float _logHalfLen, _logCa, _logSa;        // the log's capsule in the painter's pixels, for LogDist

    // The rope: ground positions and depths, head first.
    private readonly float[] _gx = new float[N], _gy = new float[N], _z = new float[N], _murk = new float[N];
    private readonly float[] _offX = new float[N], _offY = new float[N];   // the drawn wave's sideways offsets (ground), for the log's violation count
    private Doing _doing => _mind.Doing;

    // The head's state.
    private float _h;                                          // the LEADER's heading: the way the end that leads is moving (the head going forward, the tail going backward)
    private float _turnRate, _speed, _swum;                    // eased turn rate; the speed asked; the speed really made good (the wave beats at this)
    private float _hunger, _hideQ, _dq;                        // 0 fed to 1 starving; where the head wants to be on the axis in a Hide; the head's Q in the door modes
    private float _peekQ, _lookPh, _lookAmp;                   // how far out a Peek reaches; the look-about's phase and size
    private float _wavePh, _waveAmp, _breathPh;                // each rhythm its own wrapped phase (no clock wrapped at one number)
    private float _refFor, _wayHeld = float.NaN;               // seconds refused in a row; the committed escape (round forty-five): the probe's answer kept until the heading reaches it
    private float _goalX, _goalY, _goalLeft;                   // the forage goal and the time left to reach it
    private bool _hasGoal, _sliding, _fast;
    private float _forageLeft;                                // seconds of forage left when a snap interrupted it
    private int _goalsLeft, _fails;
    private double _t;
    private long _frame;
    private ISnack? _prey;
    private bool _snapMinnow;
    private float _flickT = -1f, _flickSide, _tx2, _ty2;      // the sideways snap of the head: seconds into it; which way; the minnow's last place
    private float _lastRefusedRun;
    private int _cause;
    private float _logAhead;                              // the best heading's room when the feelers last looked (the log's roomAhead)

    // Counters for the instruments.
    public int Snaps, Catches, EmergeCount, Withdraws, Flees, Returns;
    private readonly double[] _doingTime = new double[9];
    private double _evStart;


    private EelBody(Pond pond, bool[] view, Minnows? minnows, DeepDwellers deep, SunkenLog log, Random rng, float tx, float ty, float ax, float ay, float lg, float wg, float mouthQ, float zIn) : base(N)
    {
        _pond = pond; _bed = new DeepBed(pond, view); _view = _bed.View; _minnows = minnows; _deep = deep; _log = log; _rng = rng;
        _u = pond.U;
        _tx = tx; _ty = ty; _ax = ax; _ay = ay; _nx = -ay; _ny = ax; _wg = wg; _hwg = wg * 0.5f; _mouthQ = mouthQ; _zIn = zIn;
        _logCa = MathF.Cos(log.Angle); _logSa = MathF.Sin(log.Angle); _logHalfLen = log.Length * 0.5f - log.Width * 0.5f;
        // Two dials, dealt at birth from its own dice (Traits.Deal with a count of one is one dial in the whole range).
        _bold = Traits.Roll(rng); _hungryDial = Traits.Roll(rng);
        _hunger = 0.3f + 0.5f * _hungryDial;
        // Its length: 0.15 to 0.18u, but never longer than the hollow can hold with the head at the mouth (0.8 of the log's
        // length, less the head's share at the door and a margin): an eel that does not fit is not an eel in a log.
        float want = _u * (0.15f + 0.03f * (float)rng.NextDouble());
        _len = MathF.Min(want, 0.8f * lg - 0.3f * wg - 0.01f * _u);
        _halfW = _len * 0.05f;
        for (int i = 0; i < N; i++) Along[i] = AlongAt[i];
        for (int i = 0; i < N - 1; i++) _seg[i] = (Along[i + 1] - Along[i]) * _len;
        _snatch = new Snatch("eel", Snatch.Seed((int)(_bold * 1e6f), (int)(_hungryDial * 1e6f), 46, 7));
        string? rate = Environment.GetEnvironmentVariable("PECKWORKS_EELRATE");
        if (!string.IsNullOrEmpty(rate) && float.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) && r > 0) Rate = r;
        // It starts lying in its hollow, the head at the mouth, partway through a hide of its own length.
        _hideQ = -0.42f * _wg;
        _dq = _hideQ;
        LayStraight(_dq);
        _mind.Begin(Doing.Hide, HideTime() * (0.1f + 0.4f * (float)rng.NextDouble()));
        _breathPh = (float)rng.NextDouble() * MathF.Tau;
        _lookPh = (float)rng.NextDouble() * MathF.Tau;
        LaySpine();
        OpenLog();
    }

    /// <summary>
    /// Makes the eel if the pond has a hollow for it: the Den (the log's mouth) must be free deep bed in view, the very
    /// test the deep dwellers use for their home there, and the bed outside it must be deep enough for the eel's own wall
    /// (0.6). Returns null otherwise, and then NOTHING is rolled from the Random.
    /// </summary>
    public static EelBody? TryMake(Pond pond, bool[] view, Minnows? minnows, DeepDwellers deep, SunkenLog log, Random rng)
    {
        var bed = new DeepBed(pond, view);
        (float dx, float dy) = bed.ToGround(log.Den.X, log.Den.Y);
        if (!bed.Free(dx, dy) || bed.ZAt(dx, dy) < 0.62f) return null;
        // The log's cut face and axis in the ground plane. The painter's numbers are in its own pixels (the picture's, before
        // the sink), so the two ends are carried to the ground and the axis is the line between them.
        PointF near = log.At(log.NearEnd), far = log.At(1f - log.NearEnd);
        (float tx, float ty) = bed.ToGround(near.X, near.Y);
        (float fx, float fy) = bed.ToGround(far.X, far.Y);
        float lg = MathF.Sqrt((tx - fx) * (tx - fx) + (ty - fy) * (ty - fy));
        if (lg < pond.U * 0.1f) return null;
        float ax = (tx - fx) / lg, ay = (ty - fy) / lg;
        // The log's thickness in the ground plane: the sideways unit, seen on the screen, is squashed; the ground thickness is
        // the painter's thickness over the length of that unit's picture.
        float nsx = -ay, nsy = ax, k = MathF.Sqrt(nsx * nsx * 1f + nsy * Ripples.Squash * nsy * Ripples.Squash);
        float wg = log.Width / MathF.Max(0.3f, k);
        float mouthQ = (dx - tx) * ax + (dy - ty) * ay;
        // The inside's depth is the HOLLOW'S height, a little over half the log's height above the bed it lies on (the bed's own
        // depth until the main model's pass: drawn sunk by that depth the head sat a log's height below the hole, "poking out from
        // under the log, not where the hole in the log is").
        float zIn = bed.ZAt(dx, dy) - log.HeightZ * 0.55f;
        return new EelBody(pond, view, minnows, deep, log, rng, tx, ty, ax, ay, lg, wg, mouthQ, zIn);
    }

    /// <summary>Its head, ground pixels, when any of it is out of the hollow's doorway (Peek and after); null while it lies in (Hide) or has withdrawn.</summary>
    public PointF? Head => _doing == Doing.Hide || HeadQ() < 0.05f * _wg ? null : new PointF(_gx[0], _gy[0]);

    // ================================================================== the hollow's frame
    private void AxisPos(float q, float lat, out float gx, out float gy) { gx = _tx + _ax * q + _nx * lat; gy = _ty + _ay * q + _ny * lat; }
    private float QOf(float gx, float gy) => (gx - _tx) * _ax + (gy - _ty) * _ay;
    private float LatOf(float gx, float gy) => (gx - _tx) * _nx + (gy - _ty) * _ny;
    private float TailQ() => QOf(_gx[N - 1], _gy[N - 1]);
    private float HeadQ() => QOf(_gx[0], _gy[0]);

    /// <summary>How far INSIDE the hollow a ground point is, 0 (outside, or past the cut face) to 1 (well in), smooth: the corridor is the log's middle, from a fifth of its thickness outside the cut face inward.</summary>
    private float Inside(float gx, float gy)
    {
        float q = QOf(gx, gy), lat = MathF.Abs(LatOf(gx, gy));
        float fq = Math.Clamp((0.2f * _wg - q) / (0.5f * _wg), 0f, 1f);
        float fl = Math.Clamp((0.8f * _hwg - lat) / (0.3f * _hwg), 0f, 1f);
        return fq * fl;
    }

    /// <summary>The distance from a ground point to the log's capsule, in the painter's pixels, negative inside (the log is a wall to every free step).</summary>
    private float LogDist(float gx, float gy)
    {
        PointF s = _bed.ToScreen(gx, gy);
        float dx = s.X - _log.Centre.X, dy = s.Y - _log.Centre.Y;
        float along = Math.Clamp(dx * _logCa + dy * _logSa, -_logHalfLen, _logHalfLen);
        float ex = dx - along * _logCa, ey = dy - along * _logSa;
        return MathF.Sqrt(ex * ex + ey * ey) - _log.HalfWidthAt(along);
    }

    /// <summary>
    /// THE DOOR'S LANE: straight out from the cut face along the axis, as wide as four fifths of the log. The log is a wall
    /// to the free steps with a margin of 0.035u (the wave's swing and the body's own half-width), and the mouth itself is
    /// only 0.6 of the log's thickness from its end, so the margin alone shut the door: the feelers saw every heading at it
    /// as no room, and the first run's tail circled the pond for a minute and a half with the mouth in sight. In the lane
    /// the log is no wall (the end is a rounded cap, and what a body does at it is the slide's, kinematic and straight).
    /// </summary>
    private bool InLane(float gx, float gy) => QOf(gx, gy) > 0.25f * _wg && MathF.Abs(LatOf(gx, gy)) < 0.8f * _hwg;

    private float LogMargin => _u * 0.035f;   // the wave swings a body of this half-width this far (0.09 of a length) and the eel's own half-width: the log's pixels are never drawn over

    // ================================================================== the bed's rules (the catfish's: one fixed floor)
    private float FloorAt(float gx, float gy) => _bed.ZAt(gx, gy);

    /// <summary>May the leader step to this ground point? Free deep bed in view, a floor of at least 0.6, and clear of the log (cause 1, 2, 3 for the log).</summary>
    private bool StepOk(float gx, float gy, out int cause)
    {
        cause = 0;
        if (!_bed.Free(gx, gy)) { cause = 1; return false; }
        if (FloorAt(gx, gy) < 0.6f) { cause = 2; return false; }
        if (LogDist(gx, gy) < LogMargin && !InLane(gx, gy)) { cause = 3; return false; }
        return true;
    }

    /// <summary>The room at a ground point for the feelers: the stacked grid on the floor 0.6 (the bank's slope, the boulders, the upper half of each mound, the picture's edge), less the log's margin; 0 where a step is not allowed at all.</summary>
    private float Room(float gx, float gy)
    {
        if (!_bed.Free(gx, gy)) return 0f;
        PointF s = _bed.ToScreen(gx, gy);
        float r = _pond.RoomAt(s.X, s.Y, 0.6f);
        float l = InLane(gx, gy) ? 1e6f : LogDist(gx, gy) - LogMargin + _u * 0.01f;
        return MathF.Max(0f, MathF.Min(r, l));
    }

    private float RoomAlong(float px, float py, float h, float dist)
    {
        float c = MathF.Cos(h), s = MathF.Sin(h);
        float a = Room(px + c * dist * 0.5f, py + s * dist * 0.5f), b = Room(px + c * dist, py + s * dist);
        return MathF.Min(a, b);
    }

    // ================================================================== the rope
    private void LayStraight(float headQ)
    {
        for (int i = 0; i < N; i++) AxisPos(headQ - Along[i] * _len, 0f, out _gx[i], out _gy[i]);
    }

    /// <summary>Pulls every point to its fixed distance from the one nearer the leader, starting at the leader (0 the head, N-1 the tail).</summary>
    private void Drag(int lead)
    {
        if (lead == 0) for (int i = 1; i < N; i++) Follow(i, i - 1, _seg[i - 1]);
        else for (int i = N - 2; i >= 0; i--) Follow(i, i + 1, _seg[i]);
    }

    private void Follow(int i, int j, float d)
    {
        float dx = _gx[i] - _gx[j], dy = _gy[i] - _gy[j], l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1e-4f) { dx = 1f; dy = 0f; l = 1f; }
        float k = d / l;
        _gx[i] = _gx[j] + dx * k; _gy[i] = _gy[j] + dy * k;
    }

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float p) => p * p * (3 - 2 * p);
    private static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    // ================================================================== steering (one step of whichever end leads)
    /// <summary>
    /// One frame of steering for the leader (lead 0 the head going forward, N-1 the tail going backward) toward a ground
    /// point at the given speed. Four feelers fan out from the goal's direction and take the one with the most room, then
    /// the heading eases toward it at the turning circle's own rate (0.32 of a body length, the snake's floor); the step is
    /// tested with the step rule and a refused step turns on the spot (the eel's speed decays; the committed escape of round
    /// forty-five keeps the probe's answer until the heading reaches it). True when the step was refused.
    /// </summary>
    private bool StepLeader(float dt, int lead, float goalX, float goalY, float want)
    {
        float px = _gx[lead], py = _gy[lead];
        float toGoal = MathF.Atan2(goalY - py, goalX - px);
        float distGoal = Dist(px, py, goalX, goalY);
        float look = MathF.Min(_len * 0.9f + _swum * 0.6f, distGoal + _len * 0.2f);   // the feelers never reach past the goal: a wall beyond it is not in the way
        float best = toGoal, bestRoom = look;
        bool inWall = !StepOk(px, py, out _);
        if (inWall)
        {
            // INSIDE A WALL (a flee at full speed drove the head into a rock formation's cliff): the way out is the roomiest or
            // deepest of eight directions a short step away, and it turns straight to it (a held way-out that still pointed into
            // the rock kept it there 140 s on the first natural run: the held answer is dropped here).
            float bestV = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8, ox = px + MathF.Cos(a) * _len * 0.15f, oy = py + MathF.Sin(a) * _len * 0.15f;
                float v = Room(ox, oy) + FloorAt(ox, oy) * _len;
                if (v > bestV) { bestV = v; best = a; }
            }
            _wayHeld = float.NaN; _h = best; _turnRate = 0; _logAhead = 0;
        }
        else if (!float.IsNaN(_wayHeld)) { best = _wayHeld; _logAhead = 0; }
        else
        {
            float bestScore = float.MinValue;
            foreach (float off in (ReadOnlySpan<float>)[0f, 0.45f, -0.45f, 0.9f, -0.9f, 1.4f, -1.4f, 2.1f, -2.1f])
            {
                float h = Wrap(toGoal + off);
                float r = RoomAlong(px, py, h, look);
                float score = MathF.Min(r, look) - MathF.Abs(off) * _len * 0.2f - MathF.Abs(Wrap(h - _h)) * _len * 0.06f;
                if (score > bestScore) { bestScore = score; best = h; bestRoom = r; }
            }
            _logAhead = bestRoom;
        }
        _lastGoalX = goalX; _lastGoalY = goalY; _lastBest = best;
        float dh = Wrap(best - _h);
        // A refused eel PIVOTS at nearly a radian a second (the floor was 0.47: pressed into a cliff's corner at seed 3 the half turn
        // to the way out took longer than the jam memory, each new doing turned it back, and it stood 140 s), and the jam memory
        // is 4 s, long enough for that turn (1.5 s was the koi's, a quicker turner).
        float most = MathF.Max(_swum, _len * 0.3f) / (0.32f * _len);
        float turn = Math.Clamp(dh * 3f, -most, most);
        _turnRate += (turn - _turnRate) * (1 - MathF.Exp(-dt * 8f));
        _h = Wrap(_h + _turnRate * dt);
        float sharp = MathF.Abs(Wrap(best - _h));
        float sp = want * (1f - 0.55f * Math.Clamp(sharp / 1.2f, 0f, 1f));
        _speed += (sp - _speed) * (1 - MathF.Exp(-dt * 2f));
        float step = _speed * dt;
        float c = MathF.Cos(_h), s = MathF.Sin(_h);
        float nx = px + c * step, ny = py + s * step;
        bool ok = StepOk(nx, ny, out int cause);
        // ALREADY INSIDE A WALL, ANY STEP OUT IS ALLOWED (the main model's pass, from a natural run: a flee at full speed drove
        // the head into a rock formation's cliff, where every step was refused as land and every doing jammed in turn for
        // the last 140 s of the run; the koi's rule from round thirty-eight). A step from unfree ground to a point with more
        // room, or deeper water, is the way out and passes.
        if (!ok && inWall && (Room(nx, ny) > Room(px, py) || FloorAt(nx, ny) > FloorAt(px, py) + 1e-4f)) { ok = true; cause = 0; }
        _cause = ok ? 0 : cause;
        if (ok)
        {
            _gx[lead] = nx; _gy[lead] = ny;
            Drag(lead);
            _refFor = 0; _wayHeld = float.NaN;
        }
        else
        {
            _refFor += dt;
            _speed = MathF.Max(_len * 0.12f, _speed * (1 - 3f * dt));
            if (float.IsNaN(_wayHeld) || MathF.Abs(Wrap(_wayHeld - _h)) < 0.15f) _wayHeld = WayOut(px, py, toGoal);
        }
        if (dt > 0) _swum += ((ok ? step : 0f) / dt - _swum) * (1 - MathF.Exp(-dt * 8f));
        return !ok;
    }

    /// <summary>The refused step's probe: the clear heading (a step of a third of a length AND a sixth clear) nearest the goal's, scanned a side at a time; the heading itself when nothing is clear (it turns on the spot until something is).</summary>
    private float WayOut(float px, float py, float toGoal)
    {
        float bestH = _h, bestD = float.MaxValue;
        for (int k = 0; k < 16; k++)
        {
            float h = Wrap(_h + (k - 8) * MathF.Tau / 16f);
            float c = MathF.Cos(h), s = MathF.Sin(h);
            if (!StepOk(px + c * _len * 0.33f, py + s * _len * 0.33f, out _) || !StepOk(px + c * _len * 0.16f, py + s * _len * 0.16f, out _)) continue;
            float d = MathF.Abs(Wrap(h - toGoal));
            if (d < bestD) { bestD = d; bestH = h; }
        }
        return bestH;
    }

    // ================================================================== the mind
    private float HideTime() => (35f + 70f * (float)_rng.NextDouble()) * (1f - 0.4f * _bold) / Rate;   // (60 to 180 s on the helper's build: three natural runs of 180 s never saw it out)

    private void Begin(Doing d, float seconds)
    {
        if (d != _doing) LogEvent("doing", d.ToString());
        _mind.Begin(d, seconds);
        _evStart = _t;
    }

    private void BeginHide(float seconds, float headQ)
    {
        _hideQ = headQ; _sliding = false; _speed = 0; _swum = 0; _turnRate = 0; _wayHeld = float.NaN;
        Begin(Doing.Hide, seconds);
    }

    private void BeginPeek()
    {
        _peekQ = (0.1f + 0.3f * _bold) * _wg;                  // (from 0.3 of a width out until the main model's pass: the head hid a width beyond the end, "poking out from under the log, not where the hole in the log is"; its snout sits in the hole now)
        _lookAmp = 0.3f * _wg * (0.6f + 0.4f * _bold);
        Begin(Doing.Peek, 10f + 10f * (float)_rng.NextDouble());
    }

    private void BeginEmerge()
    {
        EmergeCount++;
        _dq = HeadQ();
        Begin(Doing.Emerge, Mind<Doing>.Forever);
    }

    private void BeginForage(float seconds)
    {
        _h = MathF.Atan2(_ay, _ax); _wayHeld = float.NaN; _hasGoal = false; _fast = false; _sliding = false;
        _goalsLeft = 3 + _rng.Next(4);
        _forageLeft = seconds;
        Begin(Doing.Forage, seconds);
    }

    private void BeginApproach(bool fast)
    {
        _fast = fast; _sliding = false; _wayHeld = float.NaN; _refFor = 0;
        if (fast) { Flees++; Begin(Doing.Flee, 40f); }
        else { Returns++; Begin(Doing.Approach, 45f); }
    }

    private void BeginBack(bool fast)
    {
        _fast = fast; _wayHeld = float.NaN; _refFor = 0;
        // The tail leads now: its heading is the way it is moving, which starts as the direction from the head to the tail.
        _h = MathF.Atan2(_gy[N - 1] - _gy[N - 2], _gx[N - 1] - _gx[N - 2]);
        _turnRate = 0; _speed = 0;
        Begin(Doing.Back, 45f);
    }

    private void BeginWithdraw(float headQ)
    {
        Withdraws++;
        _hideQ = headQ; _sliding = true; _fast = true;
        Begin(Doing.Withdraw, Mind<Doing>.Forever);
    }

    /// <summary>The dice at the end of a doing (Dice.Pick): what a timer-ended doing becomes. Hide always becomes a Peek; a Peek is an Emerge for a bold eel more often; a Forage is another leg for a hungry one, else home.</summary>
    private void Decide()
    {
        switch (_doing)
        {
            case Doing.Hide:
                if (ThreatNear(0.6f)) { _mind.Left = 5f; return; }                // a koi right over the door: not now
                BeginPeek();
                break;
            case Doing.Peek:
            {
                Span<float> w = [0.55f + 0.35f * _bold, 0.45f - 0.2f * _bold];   // out, or back to lying in the mouth (0.3 and 0.5 on the helper's build)
                if (Dice.Pick(_rng, w) == 0 && !ThreatNear(0.6f)) BeginEmerge();
                else { _hideQ = -0.42f * _wg; Begin(Doing.Hide, (15f + 30f * (float)_rng.NextDouble()) / Rate); }
                break;
            }
            case Doing.Forage:
            {
                Span<float> w = [0.1f + 0.3f * _hunger, 1f];                    // another leg, or home
                if (Dice.Pick(_rng, w) == 0) BeginForage(40f + 40f * (float)_rng.NextDouble());
                else BeginApproach(false);
                break;
            }
            case Doing.Approach: case Doing.Flee:
                // Out of time: it gives the staging up, forages a little further, and tries again (a jam has a memory).
                _fails++;
                BeginForage(15f + 20f * (float)_rng.NextDouble());
                break;
            case Doing.Back:
                _fails++;
                BeginForage(15f + 20f * (float)_rng.NextDouble());
                break;
            default:
                BeginForage(30f);
                break;
        }
    }

    /// <summary>Is anything that frightens it within the given multiple of its reach of its head (screen pixels)?</summary>
    private bool ThreatNear(float mul) => _threatMin < ReachPx() * mul;
    private float _threatMin = float.MaxValue;
    private float ReachPx() => _u * (0.07f - 0.025f * _bold);

    /// <summary>What frightens it: a koi, a turtle or a diving snake closer than its reach to its head, its middle or its tail (screen pixels). A cooldown keeps one fright from firing every frame.</summary>
    private void React(PointF[] threats)
    {
        _threatMin = float.MaxValue;
        for (int i = 0; i < threats.Length; i++)
            foreach (int k in (ReadOnlySpan<int>)[0, 10, N - 1])
            {
                // Only what is OUT of the log can be seen by a fish: the head at the mouth, and the middle and the tail once they are
                // beyond the cut face. A koi crossing the log's far end is no threat to an eel lying in its near end (the first runs:
                // a koi was within reach of the buried tail for most of the time, and the eel never came out).
                if (k != 0 && QOf(_gx[k], _gy[k]) < 0.1f * _wg) continue;
                PointF at = _bed.ToScreen(_gx[k], _gy[k]);   // in plan view, where it lies on the surface above it: a koi over the log is over it, however deep the eel is drawn
                float dx = threats[i].X - at.X, dy = threats[i].Y - at.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < _threatMin) _threatMin = d;
            }
        // Already going home (Withdraw, Flee, the backing in) is committed: a second fright on the way changes nothing, and
        // restarting the flight at every one kept a frightened eel from ever arriving (the first stress runs).
        // In its hole it is safe, and a koi is no eater of eels: hidden or peeking it minds only a threat within half its reach
        // (at the full reach, 19 withdrawals in five minutes kept seed 1's eel in for the whole run, every koi that cruised
        // past the door resetting its hide); out in the open the full reach stands.
        float scare = _doing is Doing.Hide or Doing.Peek ? 0.5f : 1f;
        if (!_reactCool.Ready || _threatMin >= ReachPx() * scare || _doing is Doing.Withdraw or Doing.Flee or Doing.Back) return;
        _reactCool.Start(6f);
        _snatch.Abandon("fright"); _prey = null; _flickT = -1f;
        float tq = TailQ(), tl = MathF.Abs(LatOf(_gx[N - 1], _gy[N - 1]));
        bool inFunnel = tq < _mouthQ + 0.4f * _len && tl < 0.45f * _wg + 0.1f * MathF.Max(0f, tq);
        switch (_doing)
        {
            case Doing.Hide: case Doing.Peek:
                BeginWithdraw(-0.12f * _wg);
                break;
            case Doing.Emerge:
                if (TailQ() < 0.35f * _wg) BeginWithdraw(-0.12f * _wg); else BeginApproach(true);
                break;
            default:
                if (inFunnel) BeginWithdraw(-0.12f * _wg); else BeginApproach(true);
                break;
        }
    }

    // ================================================================== update
    public void Update(float dt, PointF[] threats)
    {
        dt = MathF.Min(dt, 0.1f);
        _t += dt; _frame++;
        _mind.Tick(dt); _snatch.Tick(dt); _reactCool.Tick(dt); _snapCool.Tick(dt);
        _hunger = MathF.Min(1f, _hunger + dt / 900f);
        _breathPh = (_breathPh + dt * MathF.Tau * 0.5f) % MathF.Tau;
        _lookPh = (_lookPh + dt * MathF.Tau * 0.11f) % MathF.Tau;
        React(threats);
        _doingTime[(int)_doing] += dt;
        _cause = 0;
        _refusedNow = false;

        switch (_doing)
        {
            case Doing.Hide: UpdateHide(dt); break;
            case Doing.Peek: UpdatePeek(dt); break;
            case Doing.Emerge: UpdateEmerge(dt); break;
            case Doing.Forage: UpdateForage(dt); break;
            case Doing.Snap: UpdateSnap(dt); break;
            case Doing.Approach: case Doing.Flee: UpdateApproach(dt); break;
            case Doing.Back: UpdateBack(dt); break;
            case Doing.Withdraw: UpdateWithdraw(dt); break;
        }

        // The wave: beats at the speed really made good, a wavelength of 0.55 of a body, runs head to tail going forward and
        // tail to head going backward; it is off in the doorway (the per-point Inside factor) and eased on and off.
        bool swimming = _doing is Doing.Forage or Doing.Snap or Doing.Approach or Doing.Flee || (_doing == Doing.Back && !_sliding);
        float ampWant = swimming ? Math.Clamp(_swum / (_len * 0.3f), 0.35f, 1.0f) : (_doing == Doing.Emerge ? 0.5f : 0f);
        _waveAmp += (ampWant - _waveAmp) * (1 - MathF.Exp(-dt * 3f));
        float beat = Math.Clamp(_swum / (0.33f * _len), 0.35f, 2.6f);
        float dirSign = _doing == Doing.Back ? -1f : 1f;
        _wavePh = (_wavePh + dirSign * dt * MathF.Tau * beat) % MathF.Tau;

        LaySpine();
        if (MotionLog != null) LogRow();
    }

    private bool _refusedNow;
    private float _lastGoalX, _lastGoalY, _lastBest;     // what the steering last aimed at (for the log)

    // ---------------------------------------------------------------- Hide: lying in the mouth, breathing
    private void UpdateHide(float dt)
    {
        // The head eases to where it lies (at the cut face, or just inside after a fright) and breathes.
        float step = (_hideQ - _dq) * (1 - MathF.Exp(-dt * 4f));
        _dq += step;
        PlaceHead(_dq);
        TryAmbush(dt);
        if (_mind.Over) Decide();
    }

    // ---------------------------------------------------------------- Peek: the head a little further out, looking about
    private void UpdatePeek(float dt)
    {
        _dq += (_peekQ - _dq) * (1 - MathF.Exp(-dt * 2f));
        PlaceHead(_dq);
        TryAmbush(dt);
        if (_mind.Over) Decide();
    }

    /// <summary>
    /// Moves the head to Q on the axis in a door mode (Hide, Peek, Emerge), by sliding the WHOLE body along the axis. The body
    /// lies straight along it in all three, so a slide is exact. Pulling by the head alone was the first version, and it
    /// FOLDED the body: a head that moves back along its own body (Peek to Hide) passes the point behind it, and a rope
    /// puts that point back on the far side, so the first few points ended up OUT beyond the head (a drawn corner of 180
    /// degrees at joints one to four, in a third of the stress run's hide frames).
    /// </summary>
    private void PlaceHead(float q)
    {
        float dq = q - HeadQ();
        if (MathF.Abs(dq) < 1e-4f) return;
        for (int i = 0; i < N; i++)
        {
            float lat = LatOf(_gx[i], _gy[i]);
            AxisPos(QOf(_gx[i], _gy[i]) + dq, lat, out _gx[i], out _gy[i]);
        }
    }

    // ---------------------------------------------------------------- Emerge: the body slides out of the hollow, head first
    private void UpdateEmerge(float dt)
    {
        _dq += 0.3f * _len * dt;
        PlaceHead(_dq);
        _speed = 0.3f * _len; _swum = _speed;
        if (TailQ() > 0.35f * _wg)
        {
            LogEvent("emerged", (_t - _evStart).ToString("F2", CultureInfo.InvariantCulture));
            BeginForage(60f + 60f * (float)_rng.NextDouble());
            _speed = 0.3f * _len;
        }
    }

    // ---------------------------------------------------------------- Forage: an S along the bed, nose down
    private float Cruise => _len * (0.3f + 0.15f * _hungryDial);

    private void UpdateForage(float dt)
    {
        if (_snapCool.Ready) LookForPrey(dt);
        if (_doing != Doing.Forage) return;
        if (!_hasGoal || _goalLeft <= 0 || Dist(_gx[0], _gy[0], _goalX, _goalY) < _len * 0.25f)
        {
            if (_hasGoal && _goalsLeft-- <= 0) { _mind.Left = 0; }
            else if (!PickGoal()) _mind.Left = 0;
        }
        if (_mind.Over) { Decide(); if (_doing != Doing.Forage) return; }
        _goalLeft -= dt;
        bool refused = StepLeader(dt, 0, _goalX, _goalY, Cruise);
        NoteRefusal(refused, dt);
        if (_refFor > 4f) { LogEvent("jam", "forage"); _hasGoal = false; BeginApproach(false); }
    }

    /// <summary>A goal on open deep bed within half a unit of the mouth: tried thirty times with a line of clear steps to it. False when none is found.</summary>
    private bool PickGoal()
    {
        (float dx, float dy) = _bed.ToGround(_log.Den.X, _log.Den.Y);
        for (int tries = 0; tries < 30; tries++)
        {
            float r = _u * (0.12f + 0.38f * (float)_rng.NextDouble()), a = (float)_rng.NextDouble() * MathF.Tau;
            float gx = dx + MathF.Cos(a) * r, gy = dy + MathF.Sin(a) * r;
            if (Room(gx, gy) < _len * 0.35f) continue;
            if (!LineOk(_gx[0], _gy[0], gx, gy)) continue;
            _goalX = gx; _goalY = gy; _hasGoal = true; _goalLeft = 14f + 10f * (float)_rng.NextDouble();
            return true;
        }
        return false;
    }

    private bool LineOk(float ax, float ay, float bx, float by)
    {
        float l = Dist(ax, ay, bx, by);
        int n = Math.Max(2, (int)(l / (_u * 0.03f)));
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n;
            if (Room(ax + (bx - ax) * t, ay + (by - ay) * t) < _len * 0.15f) return false;
        }
        return true;
    }

    private void NoteRefusal(bool refused, float dt)
    {
        _refusedNow = refused;
        if (refused) _lastRefusedRun += dt;
        else
        {
            if (_lastRefusedRun > 1f) LogEvent("refusedrun", _lastRefusedRun.ToString("F2", CultureInfo.InvariantCulture) + ",cause" + _cause);
            _lastRefusedRun = 0;
        }
    }

    // ---------------------------------------------------------------- Snap: the Snatch kit picked a prey, or a minnow strayed in reach
    private float SeePx => _len * (0.4f + 0.5f * _hunger) ;
    private float ReachSnap => _len * 0.22f;

    private void LookForPrey(float dt)
    {
        PointF mouth = Spine[0];
        // A prawn: the shared Snatch. Its patience is eight seconds, its rest between looks half a minute to a minute.
        if (_snatch.Look(_deep, mouth, SeePx, 0.15f + 0.4f * _hunger, dt, 8f, 30f + 30f * (1f - _hunger), false, s => s is Dweller d && d.Kind == DeepKind.Prawn))
        {
            _prey = _snatch.Prey; _snapMinnow = false; BeginSnap();
            return;
        }
        // A dipped minnow within a head's length or so of the mouth.
        if (_minnows != null && _minnows.NearestFishDeep(mouth.X, mouth.Y, SeePx * 0.8f, _z[0] - 0.12f, out PointF nose))
        {
            (_tx2, _ty2) = _bed.ToGround(nose.X, nose.Y - Light.SinkPx(_z[0], _u));
            _prey = null; _snapMinnow = true; BeginSnap();
        }
    }

    private void BeginSnap()
    {
        Snaps++;
        _flickT = -1f; _snapStart = _t;
        // Forage's remaining time is kept: a snap is an aside, not the end of the outing.
        _forageLeft = _mind.Left;
        Begin(Doing.Snap, 8f);
    }

    private double _snapStart;

    private void UpdateSnap(float dt)
    {
        float tx, ty;
        PointF mouth = Spine[0];
        if (_snapMinnow)
        {
            if (_minnows != null && _minnows.NearestFishDeep(mouth.X, mouth.Y, SeePx * 1.3f, _z[0] - 0.12f, out PointF nose))
                (_tx2, _ty2) = _bed.ToGround(nose.X, nose.Y - Light.SinkPx(_z[0], _u));
            tx = _tx2; ty = _ty2;
        }
        else
        {
            if (_prey == null || !_snatch.Hunting || !_prey.Alive) { EndSnap(false); return; }
            tx = _prey.X; ty = _prey.Y;
        }
        if (_flickT >= 0)
        {
            float before = _flickT;
            _flickT += dt;
            if (before < 0.12f && _flickT >= 0.12f) TakeNow();
            if (_flickT >= 0.32f) { EndSnap(false); }
            return;
        }
        float d = Dist(_gx[0], _gy[0], tx, ty);
        PointF target = _snapMinnow ? new PointF(mouth.X, mouth.Y) : _prey!.Nose;
        float sd = _snapMinnow ? Dist(_gx[0], _gy[0], tx, ty) * Ripples.Squash : Dist(mouth.X, mouth.Y, target.X, target.Y);
        if (sd <= ReachSnap * 1.6f || d <= _len * 0.12f)
        {
            _flickT = 0f; _flickSide = Wrap(MathF.Atan2(ty - _gy[0], tx - _gx[0]) - _h) >= 0 ? 1f : -1f;
            _speed *= 0.3f;
            return;
        }
        bool refused = StepLeader(dt, 0, tx, ty, _len * 0.5f);
        NoteRefusal(refused, dt);
        if (_refFor > 1.0f || _mind.Over) EndSnap(false);
    }

    private void TakeNow()
    {
        PointF mouth = Spine[0];
        bool caught = false;
        if (_snapMinnow)
        {
            if (_minnows != null)
            {
                caught = _minnows.Take(mouth.X, mouth.Y, ReachSnap * 1.5f, _z[0] - 0.12f, 2f);
                _minnows.Scatter(mouth.X, mouth.Y, _u * 0.12f);
            }
        }
        else if (_prey != null)
        {
            _snatch.Chase(_deep, mouth, ReachSnap * 1.5f);          // eats it if the mouth is within reach
            caught = !_prey.Alive;
        }
        if (caught) { Catches++; _hunger *= 0.5f; }
        LogEvent("snap", (_snapMinnow ? "minnow" : "prawn") + "," + (caught ? "caught" : "missed"));
    }

    private void EndSnap(bool _)
    {
        if (!_snapMinnow) _snatch.Abandon("eelend");
        _prey = null; _flickT = -1f; _snapCool.Start(8f);
        _hasGoal = false;
        if (_doing == Doing.Snap) Begin(Doing.Forage, MathF.Max(5f, _forageLeft));
        _forageLeft = _mind.Left;
    }

    /// <summary>In the mouth, a prawn or a minnow within a head's reach is an ambush: a flick of the head, no chase.</summary>
    private void TryAmbush(float dt)
    {
        if (_flickT >= 0) { _flickT += dt; if (_flickT >= 0.32f) _flickT = -1f; return; }
        if (!_snapCool.Ready || _doing == Doing.Withdraw) return;
        PointF mouth = Spine[0];
        if (_minnows != null && _minnows.NearestFishDeep(mouth.X, mouth.Y, _len * 0.3f, _z[0] - 0.12f, out PointF nose))
        {
            _flickT = 0f; _snapMinnow = true; _flickSide = nose.X >= mouth.X ? 1f : -1f; _snapCool.Start(12f);
            Snaps++;
            bool caught = _minnows.Take(mouth.X, mouth.Y, _len * 0.3f, _z[0] - 0.12f, 2f);
            _minnows.Scatter(mouth.X, mouth.Y, _u * 0.12f);
            if (caught) { Catches++; _hunger *= 0.5f; }
            LogEvent("snap", "minnow,ambush," + (caught ? "caught" : "missed"));
        }
    }

    // ---------------------------------------------------------------- Approach / Flee: swim to the staging spot straight out from the mouth
    private void UpdateApproach(float dt)
    {
        // The staging spot: on the axis, a body length and a bit out from the mouth. The head goes there; the body trails
        // behind it; then the TAIL takes the lead (Back).
        AxisPos(_mouthQ + 1.1f * _len + 0.3f * _wg, 0f, out float sx, out float sy);
        float d = Dist(_gx[0], _gy[0], sx, sy);
        bool refused = StepLeader(dt, 0, sx, sy, _fast ? _len * 1.1f : _len * 0.5f);
        NoteRefusal(refused, dt);
        if (d < _len * 0.35f) { BeginBack(_fast); return; }
        if (_refFor > 4f || _mind.Over) { LogEvent("jam", "approach"); _refFor = 0; Decide(); }
    }

    // ---------------------------------------------------------------- Back: the tail leads, backward, into the hollow
    private void UpdateBack(float dt)
    {
        float tq = TailQ(), lat = LatOf(_gx[N - 1], _gy[N - 1]);
        if (!_sliding)
        {
            // The funnel: aim at the axis a little ahead of where the tail is, so it comes in straight. Beside the log (near the
            // cut face but far off the axis) it first backs OUT from the log's side.
            float k = MathF.Max(0.3f * _len, 1.6f * MathF.Abs(lat));
            float gq = MathF.Max(_mouthQ, tq - k);
            float gl = 0f;
            // (The sideways goal was 1.2 times the tail's offset until the main model's pass: each frame it lay further out, and
            // a back-in on seed 5 swept the body 400 px off the axis for 20 s. A fixed offset of a width, and only when the tail
            // really is alongside the log's flank, past the cut face.)
            if (tq < _mouthQ && MathF.Abs(lat) > 0.6f * _wg) { gq = tq + 0.5f * _len; gl = MathF.Sign(lat) * 1.0f * _wg; }
            AxisPos(gq, gl, out float gx, out float gy);
            bool refused = StepLeader(dt, N - 1, gx, gy, _fast ? _len * 0.5f : _len * 0.2f);
            NoteRefusal(refused, dt);
            if (tq < _mouthQ + 0.12f * _len && MathF.Abs(lat) < 0.45f * _wg) { _sliding = true; }
            if (_refFor > 4f || _mind.Over) { LogEvent("jam", "back"); Decide(); }
            return;
        }
        SlideIn(dt, 0.3f * _len);
    }

    // ---------------------------------------------------------------- Withdraw: the quick slide back in
    private void UpdateWithdraw(float dt) => SlideIn(dt, 1.0f * _len);

    /// <summary>
    /// The kinematic slide: the TAIL leads along the axis inward at "speed" pixels a second, its sideways offset dying away
    /// over a fifth of a length of travel, until the head is at the place a Hide wants it (_hideQ). Then the mind is a Hide.
    /// </summary>
    private void SlideIn(float dt, float speed)
    {
        float tq = TailQ(), lat = LatOf(_gx[N - 1], _gy[N - 1]);
        float dq = speed * dt;
        lat *= MathF.Exp(-dq / (0.2f * _len));
        AxisPos(tq - dq, lat, out float x, out float y);
        _gx[N - 1] = x; _gy[N - 1] = y;
        Drag(N - 1);
        _swum = speed; _speed = speed;
        if (HeadQ() <= _hideQ + 0.01f * _len)
        {
            _dq = HeadQ();
            LogEvent(_doing == Doing.Withdraw ? "withdrawn" : "backedin", (_t - _evStart).ToString("F2", CultureInfo.InvariantCulture));
            _fails = 0;
            bool shy = _doing == Doing.Withdraw;
            BeginHide(shy ? (10f + 20f * (float)_rng.NextDouble()) / Rate : HideTime(), _hideQ);
            if (_doing == Doing.Hide && !shy) _hideQ = -0.42f * _wg;
        }
    }

    // ================================================================== laying the drawn spine
    private float DrawQ(int i) => QOf(_gx[i], _gy[i]);

    /// <summary>The drawn spine: the rope's points plus the S-wave's sideways offset (off inside the doorway, nothing at the head), the neck's breathing and look-about, sunk by depth. Depth is the bed's cap at each point, the hollow's constant inside.</summary>
    private void LaySpine()
    {
        float zPx = Pond.DepthU * _u;
        // The wave offsets (ground): perpendicular to the local direction of the rope.
        for (int i = 0; i < N; i++)
        {
            int a = Math.Max(0, i - 1), b = Math.Min(N - 1, i + 1);
            float dx = _gx[a] - _gx[b], dy = _gy[a] - _gy[b], l = MathF.Sqrt(dx * dx + dy * dy);
            float px = 0, py = 0;
            if (l > 1e-3f) { px = -dy / l; py = dx / l; }
            float s = Along[i];
            float amp = _waveAmp * _len * (0.01f + 0.065f * MathF.Pow(s, 1.7f));   // 0.075 of a length at the tip: a joint of 33 degrees at the tail's eleven-pixel segments (0.097 gave 48)
            float wave = MathF.Sin(MathF.Tau * 1.8f * s - _wavePh);
            float inside = Inside(_gx[i], _gy[i]);
            float o = amp * wave * (1f - inside);
            // The neck: breathing sways it a hair, the look-about swings it, a snap's flick throws it sideways. All display only.
            float nw = s < 0.22f ? (1f - s / 0.22f) * (1f - s / 0.22f) : 0f;
            float lat = 0, along = 0;
            if (nw > 0)
            {
                bool door = _doing is Doing.Hide or Doing.Peek or Doing.Withdraw;
                lat += nw * (door ? 0.012f * _wg * MathF.Sin(_breathPh) : 0f);
                along += nw * (door ? 0.02f * _wg * MathF.Sin(_breathPh * 1.0f + 1f) : 0f);
                if (_doing == Doing.Peek) lat += nw * _lookAmp * MathF.Sin(_lookPh) * Math.Clamp((_dq - 0.3f * _wg) / (0.2f * _wg), 0f, 1f);
                if (_flickT >= 0f) lat += nw * _flickSide * _len * 0.1f * MathF.Sin(MathF.Min(1f, _flickT / 0.32f) * MathF.PI) * 1.0f;
            }
            _offX[i] = px * o + _nx * lat + _ax * along;
            _offY[i] = py * o + _ny * lat + _ay * along;
        }
        int capped = 0;
        for (int i = 0; i < N; i++)
        {
            float gx = _gx[i], gy = _gy[i];
            float inside = Inside(gx, gy);
            _inside[i] = inside;
            float zOut = MathF.Max(0f, FloorAt(gx, gy) - LieZ);      // it lies ON the bed: its own round body's radius above the silt
            _z[i] = zOut + (_zIn - zOut) * inside;
        }
        // The body bends, it does not step: no point deeper than its neighbour by more than a slope of 45 degrees (the snake's pass).
        for (int i = 1; i < N; i++) _z[i] = MathF.Min(_z[i], _z[i - 1] + _seg[i - 1] / zPx);   // (each segment's own length: the head's are two pixels, and an average segment's slope there was a corner)
        for (int i = N - 2; i >= 0; i--) _z[i] = MathF.Min(_z[i], _z[i + 1] + _seg[i] / zPx);
        for (int i = 0; i < N; i++) _zTest[i] = _z[i] + 0.25f * _inside[i];   // (a tenth left one point of the tail showing at the hollow's far end, where the log's rounded end is lowest: 327 frames on seed 5)
        for (int i = 0; i < N; i++)
        {
            PointF s = _bed.ToScreen(_gx[i] + _offX[i], _gy[i] + _offY[i]);
            Spine[i] = new PointF(s.X, s.Y + Light.SinkPx(_z[i], _u));
            _murk[i] = Light.Murk(_z[i]) * Math.Clamp(_z[i] / 0.1f, 0f, 1f);
            float breath = Along[i] < 0.14f ? 1f + 0.1f * MathF.Sin(_breathPh * 2f) * (1f - Along[i] / 0.14f) : 1f;
            HalfWidth[i] = MathF.Max(0.6f, _halfW * 1.15f * Total(Along[i]) * Light.Scale(_z[i]) * breath);   // (a seventh wider than the helper's: a thin dark line was a stick on the bed)
            if (Inside(_gx[i], _gy[i]) <= 0.001f && _z[i] > FloorAt(_gx[i], _gy[i]) - 0.01f) capped++;   // deeper than the bed less a hair: in the ground
        }
        _aboveCap = capped;
    }

    private int _aboveCap;

    // ================================================================== shape and colour
    /// <summary>The body's round half-width at s (1 is the widest of the head): a blunt snout, a flat head, the neck, then the girth growing to its thickest a third of the way back and tapering to the paddle.</summary>
    private static float Body_(float s)
    {
        if (s < 0.03f) return 0.4f + 0.6f * MathF.Sin(s / 0.03f * MathF.PI * 0.5f);
        if (s < 0.11f) return 1f;                                                   // the head (a little flattened: as wide as the neck is round)
        if (s < 0.14f) return 1f - 0.08f * (s - 0.11f) / 0.03f;                    // the head narrows to the neck
        if (s < 0.36f) return 0.92f + 0.33f * Smooth((s - 0.14f) / 0.22f);         // the girth grows to 1.25
        float t = (s - 0.36f) / 0.64f;
        return 1.25f * (1f - 0.86f * MathF.Pow(t, 1.15f));                          // the long taper to the tail's tip
    }

    /// <summary>The low fin seam: from the middle of the back to the tail's tip and round it, a fringe added to the body's outline.</summary>
    private static float Fin(float s)
    {
        if (s < 0.45f) return 0f;
        float t = (s - 0.45f) / 0.55f;
        return 0.5f * MathF.Sin(MathF.Min(1f, t) * MathF.PI * 0.5f) * (t > 0.86f ? 1f - (t - 0.86f) / 0.14f * 0.35f : 1f);
    }

    private static float Total(float s) => Body_(s) + Fin(s) * 0.4f;

    /// <summary>Back colour, flank colour and belly colour at the bed (what the depth filter OUTPUTS is what matters: see Palette).</summary>
    // (The helper's first palette, 88,90,44 on the back, drew as a dark thin line on the deep bed, a stick: the depth rule keeps
    // a fraction of every channel and the murk mixes the water in, so a dark colour lands under the silt's. What reads is
    // contrast INSIDE the thing, the catfish's lesson: an olive-grey back against a pale yellow flank that shows from above.)
    private static readonly (int R, int G, int B) BackC = (122, 120, 68), FlankC = (184, 172, 102), BellyC = (226, 212, 142), FinC = (150, 140, 84);

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float tot = Total(s), body = Body_(s), share = body / tot, av = MathF.Abs(v);
        if (av > share)
        {
            // The fin seam: translucent, a little paler than the back, fading to the edge.
            float k = (av - share) / MathF.Max(0.01f, 1f - share);
            (r, g, b) = FinC;
            alpha = 0.55f * (1f - k * k);
            return alpha > 0.02f;
        }
        float vb = av / share;                                                      // 0 on the backbone, 1 at the body's edge
        float round = MathF.Sqrt(MathF.Max(0f, 1f - vb * vb));                      // a round section: 1 on top, 0 at the sides
        float flank = Smooth(Math.Clamp((vb - 0.3f) / 0.6f, 0f, 1f));              // the back gives way to the pale flank and belly toward the sides (from 0.45 out until the main model's pass: too little pale showed from above)
        float belly = flank * flank;
        r = (int)(BackC.R + (FlankC.R - BackC.R) * flank + (BellyC.R - FlankC.R) * belly);
        g = (int)(BackC.G + (FlankC.G - BackC.G) * flank + (BellyC.G - FlankC.G) * belly);
        b = (int)(BackC.B + (FlankC.B - BackC.B) * flank + (BellyC.B - FlankC.B) * belly);
        float shade = 0.8f + 0.2f * round;
        r = (int)(r * shade); g = (int)(g * shade); b = (int)(b * shade);
        if (s < 0.05f && vb > 0.55f) { r += (int)((BellyC.R - r) * 0.5f); g += (int)((BellyC.G - g) * 0.5f); b += (int)((BellyC.B - b) * 0.5f); }   // the pale lower jaw, seen beside the snout
        if (s < 0.12f) { float hp = 0.3f * (1f - s / 0.12f); r += (int)((FlankC.R - r) * hp); g += (int)((FlankC.G - g) * hp); b += (int)((FlankC.B - b) * hp); }   // the head a shade paler than the back, so it reads at the dark mouth
        if (s > 0.45f && vb < 0.1f) { r = r * 9 / 10; g = g * 9 / 10; b = b * 9 / 10; }   // the dorsal seam's ridge
        alpha = 1f;
        return true;
    }

    // ================================================================== drawing
    public void DrawDeep(FrameBuffer fb)
    {
        bool[] st = _pond.Open;
        float soft = MathF.Max(1f, _u * 0.0012f) * (1f + 2f * _z[0]);
        // The bed shadow within 0.012u of the body: a faint soft echo down and to the right.
        PaintShadow(fb, st, 0.006f * _u, 0.010f * _u, 0.16f, soft + 1f, near: _pond.Near, nearness: Light.Nearness(_zTest[N / 2]));
        Paint(fb, st, _pond.Sun, 0, soft, water: _pond.Pixels, murkAt: _murk, near: _pond.Near, zAt: _zTest);
        // The eyes: two small dark dots with a pale rim, just behind the snout on the sides of the flat head.
        PointF e0 = Spine[3], e1 = Spine[4];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.7f, HalfWidth[3] * 0.28f);
        int nearness = Light.Nearness(_zTest[3]);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X - ey / el * HalfWidth[3] * 0.62f * side, e0.Y + ex / el * HalfWidth[3] * 0.62f * side);
            Oval(fb, st, c, 0, er * 1.5f, er * 1.5f, 150, 140, 90, 0.5f, _murk[3], _pond.Sun, rays: false, water: _pond.Pixels, near: _pond.Near, nearness: nearness);
            Oval(fb, st, c, 0, er, er, 14, 14, 10, 0.95f, _murk[3], _pond.Sun, rays: false, water: _pond.Pixels, near: _pond.Near, nearness: nearness);
        }
    }

    // ================================================================== the instruments
    private void LogEvent(string kind, string detail)
    {
        MotionLog?.Invoke(FormattableString.Invariant($"#EV,{_t:F3},{kind},{detail}"));
    }

    private void OpenLog()
    {
        string? file = Environment.GetEnvironmentVariable("PECKWORKS_EELLOG");
        if (string.IsNullOrEmpty(file) || MotionLog != null) return;
        var writer = new StreamWriter(file) { AutoFlush = false };
        writer.WriteLine("# clock,idx,x,y,heading,turnRate,speed,z,doing,dodge,headSX,headSY,refused,roomAhead,roomHere,cause,cap,zwant,zraw, 22 spine x:y, then claim,prey,out,overLog,aboveCap,tailQ,headQ,tailLat,hunger,bold,hungryDial");
        writer.WriteLine(FormattableString.Invariant($"# eel len {_len:F1} u {_u:F1} wg {_wg:F1} mouthQ {_mouthQ:F1} zIn {_zIn:F3} bold {_bold:F3} hungry {_hungryDial:F3} rate {Rate}"));
        MotionLog = line => writer.WriteLine(line);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            writer.WriteLine(FormattableString.Invariant($"# totals snaps {Snaps} catches {Catches} emerges {EmergeCount} withdraws {Withdraws} flees {Flees} returns {Returns}"));
            for (int i = 0; i < _doingTime.Length; i++) writer.WriteLine(FormattableString.Invariant($"# doingtime {(Doing)i} {_doingTime[i]:F1}"));
            writer.Flush();
        };
    }

    private void LogRow()
    {
        var sb = new System.Text.StringBuilder(900);
        float cap = FloorAt(_gx[0], _gy[0]);
        sb.Append(FormattableString.Invariant($"{_t:F3},0,{_gx[0]:F2},{_gy[0]:F2},{_h:F4},{_turnRate:F4},{_swum:F2},{_z[0]:F4},{_doing},0,{Spine[0].X:F2},{Spine[0].Y:F2},{(_refusedNow ? 1 : 0)},{_logAhead:F1},{(_threatMin > 1e8f ? -1f : _threatMin):F0},{_cause},{cap:F4},{_zIn:F4},0"));
        for (int i = 0; i < N; i++) sb.Append(FormattableString.Invariant($",{Spine[i].X:F2}:{Spine[i].Y:F2}"));
        // Over the log: a spine point on the log's footprint that the depth test would really DRAW (its screen pixel's Near is not nearer than the point). Zero is the claim: it is inside (hidden) or outside, never through.
        int over = 0;
        for (int i = 0; i < N; i++)
        {
            if (LogDist(_gx[i], _gy[i]) >= -1f) continue;
            // The mouth is an OPENING: a neck at the cut face (up to a fifth of the log's thickness in) is meant to be seen. Over the log is deeper than that, or off the axis.
            if (QOf(_gx[i], _gy[i]) > -0.2f * _wg && MathF.Abs(LatOf(_gx[i], _gy[i])) < 0.7f * _hwg) continue;
            int ix = (int)Spine[i].X, iy = (int)Spine[i].Y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
            if (_pond.Near[iy * _pond.Width + ix] <= Light.Nearness(_z[i])) over++;
        }
        bool inHollow = TailQ() < 0.35f * _wg;
        float prey = _prey != null ? 1 : (_snapMinnow && _doing == Doing.Snap ? 2 : 0);
        sb.Append(FormattableString.Invariant($",{(_doing is Doing.Hide or Doing.Peek or Doing.Emerge or Doing.Back or Doing.Withdraw ? "eel" : "free")},{prey:F0},{(inHollow ? 0 : 1)},{over},{_aboveCap},{TailQ():F1},{HeadQ():F1},{LatOf(_gx[N - 1], _gy[N - 1]):F1},{_hunger:F3},{_bold:F3},{_hungryDial:F3},{_lastGoalX:F1},{_lastGoalY:F1},{_lastBest:F3},{_gx[N - 1]:F1},{_gy[N - 1]:F1}"));
        MotionLog!(sb.ToString());
    }
}
