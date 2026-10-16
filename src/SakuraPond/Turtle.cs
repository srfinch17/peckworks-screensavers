using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>The pond's turtles, drawn after the koi (a turtle keeps near the surface, so it is above them).</summary>
internal sealed class Turtles
{
    private readonly Turtle[] _all;

    /// <param name="petals">The floating petals, for a floating turtle to nibble at (null: none).</param>
    public Turtles(Pond pond, int count, Ripples ripples, bool[] where, Random rng, Petals? petals = null)
    {
        _all = new Turtle[Math.Max(0, count)];
        // Two dials each, dealt (Core/Minds Traits.Deal) so no two turtles are near-twins: one
        // loves the sun, one is jumpy. See Turtle for what each one changes.
        float[] sunny = Traits.Deal(rng, _all.Length), nerve = Traits.Deal(rng, _all.Length);
        // Where a shadow may land when a turtle is up on a perch: the dry things (the island's
        // cap, the log), but not under a branch. One sheet shared by all the turtles.
        bool[] perchMask = new bool[pond.Land.Length];
        if (_all.Length > 0)
            for (int i = 0; i < perchMask.Length; i++) perchMask[i] = pond.Land[i] && pond.Open[i];
        for (int i = 0; i < _all.Length; i++) _all[i] = new Turtle(pond, ripples, where, rng, sunny[i], nerve[i], perchMask, petals);
        foreach (Turtle t in _all) t.MeetPeers(_all);
    }

    /// <param name="snakeHead">The snake's head if one is out (a basking turtle slips into the water when it comes close), else null.</param>
    /// <param name="bodies">Every body in the water this frame (KoiSchool.Bodies: the fish and the turtles), so a turtle's feelers go over, under or round them.</param>
    public void Update(float dt, PointF? snakeHead = null, Body[]? bodies = null)
    {
        foreach (Turtle t in _all) t.Update(dt, snakeHead, bodies);
    }

    /// <summary>Every turtle up at the surface shoves the floating petals in its way aside (Petals.PushAside); a turtle that has dived passes under them.</summary>
    public void PushPetals(Petals petals)
    {
        foreach (Turtle t in _all) if (t.InWater && Light.DepthOf(t.Z) < Koi.PushDepth) petals.PushAside(t.Drawn, t.DrawnHalfWidth);
    }

    /// <summary>A point at each turtle's nose and under its shell this frame, screen pixels (the snake keeps clear of them).</summary>
    public PointF[] Spots => _all.SelectMany(t => new[] { t.Nose, t.Middle }).ToArray();

    /// <summary>The turtles themselves, for KoiSchool to deal into its depth order (a turtle is drawn between the koi deeper and shallower than it).</summary>
    public IReadOnlyList<Turtle> All => _all;

    /// <summary>
    /// Paints the turtles that are up on a perch. KoiSchool leaves those out:
    /// a hauled-out turtle is above the water, so it goes over everything that
    /// paints the water's surface (minnows, rings, floating petals, the snake),
    /// all of which would otherwise be painted across its overhanging shell.
    /// </summary>
    public void DrawOut(FrameBuffer fb)
    {
        foreach (Turtle t in _all) if (!t.InWater) t.Draw(fb);
    }

}

/// <summary>
/// A pond turtle seen from above: a red-eared slider, the turtle of nearly
/// every park pond in Japan (olive shell, striped head, a red streak behind
/// each eye).
///
/// WHAT A POND TURTLE DOES (watch one at any pond): it paddles slowly just
/// under the surface, FRONT LEFT leg together with BACK RIGHT, then the other
/// pair, like a dog swimming: each leg sweeps BACK quickly with its webbed
/// foot spread (that is the push, and the body surges forward), then returns
/// forward slowly with the foot folded. Its shell
/// is rigid, so it turns as one piece, in wide slow arcs, by paddling harder
/// on one side; nothing bends. Every so often it stops, spreads its legs and
/// just floats at the surface for a while, pokes its nose up for a breath
/// (one small ring), then paddles off again. Now and then it dives to the
/// bed for a while. And, the thing every turtle in every park does, it
/// HAULS OUT: it swims up to a rock or a fallen log, climbs onto it, sits
/// with its legs spread to dry in the sun, and slips back in when it has had
/// enough (or when something comes too close).
///
/// THE MIND (the koi's idea, scaled down; Core/Minds, docs/MINDS.md). Two
/// personality dials, rolled once, load the dice:
///   _sunny: how much it loves the sun. A sunny turtle hauls out more often
///           (its "haul out" face on the dice is bigger and it waits less
///           between visits) and sits longer, up to a minute; a shy-of-sun one
///           is out for about twenty seconds, and less often.
///   _nerve: how jumpy it is. A nervous turtle slips back in when the snake
///           is still well off (about 0.1u), a calm one lets it get close
///           (about 0.04u), and the nervous one also cuts its sit short.
/// One thing at a time (Doing): Cruise, Float (rest and breathe), Dive (down
/// to the bed for a while), Approach (swim to the water beside a perch),
/// Climb (haul out), Bask, Slip (off the perch, back into the water).
/// When a doing ends, Decide() rolls weighted dice for the next. The reflexes
/// underneath (steer off the bank, the "never stuck" step rule) keep the last
/// word everywhere except right beside a perch, where the perch itself is the
/// thing being approached (see Swim).
///
/// A PERCH is the island rock's cap or the fallen log. A plan (Plan) is worked
/// out before any swimming: a point on the water just outside, a second point
/// further out on the same line (so it arrives pointing the right way), where
/// the middle of the shell will rest, and which way it will face. A plan is
/// refused if any of those points is not open water or the perch is wanted
/// by another turtle, so a turtle never sets off for a place it cannot reach.
///
/// HOW IT CLIMBS: the nose goes along a smooth curve (a cubic Bezier: think of
/// a bent wire) from the water to the resting pose, and the heading is always
/// along the curve, so it can only ever move nose first. How HIGH each part of
/// the body is above the water is not a timer, it is READ FROM WHERE THAT PART
/// IS: Height() is 0 over water and 1 well inside the perch. So the nose rises
/// first as it reaches the rock, the shell follows, and a slip off is the same
/// thing run backward, for free. Up is drawn as a lift up the screen and a
/// small shadow down and right on the perch (the same rule as the petals and
/// the dragonflies, Light.cs). While any of it is up, it is no longer "in the
/// water": no haze, no sinking, depth 0, and it is painted against the
/// whole view (Pond.Open) instead of the water only, because the cap and the
/// log are not water.
///
/// HOW IT IS DRAWN: along a SPINE like the koi (Body.cs), only the spine is
/// a straight line from nose to tail that turns as a whole. The colours at
/// each spot (how far along, how far across) are worked out once: the head's
/// stripes, the shell's plates (a middle row of five, a row of four each
/// side, and a ring of small ones round the rim), the dome's shading. The
/// four legs are small ovals that swing from under the shell's edge.
/// </summary>
internal sealed class Turtle : Body
{
    private const int Points = 14;
    private const int SkinLen = 256, SkinWide = 64;

    /// <summary>What a turtle can be doing. One at a time; Decide() picks the next when the current one ends.</summary>
    private enum Doing { Cruise, Float, Dive, Approach, Climb, Bask, Slip }

    /// <summary>Which perch it is heading for, sitting on, or leaving.</summary>
    private enum Perch { None, Island, Log }

    /// <summary>Where a haul-out will happen: all screen pixels except the two headings (ground radians).</summary>
    private readonly record struct Plan(Perch Kind, PointF Far, PointF Near, PointF Mid, float ComeIn, float Face);

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly bool[] _where;
    private readonly bool[] _perchMask;                   // dry and in view: where the shadow of a turtle up on a perch may land
    private readonly Random _rng;
    private readonly float _len, _wide;                   // nose to tail tip, and half the shell's width, ground pixels
    private readonly uint[] _skin;
    private readonly float _logAx, _logAy;                // the log's axis, so Height does not redo the trig 22 times a frame
    private readonly Petals? _petals;                     // the floating petals, to nibble at while it floats

    private float _x, _y, _heading, _speed, _phase, _stroke = 1;   // _stroke: 1 paddling, 0 legs still
    private float _turnRate;                              // how fast it is turning, radians a second: eased, never set outright (see Swim)
    private float _neck;                                  // the head's turn on the neck, radians to the left of the shell's heading (it leads a turn; it looks about while basking)
    private float _dodge;                                 // the climb (negative) or dive (positive) its feelers chose this frame, z per unit forward
    private Body[] _bodies = [];                          // every body in the water this frame, for the feelers
    private readonly Feelers.Room _room;                  // RoomAround as a delegate, made once
    private float _goalX, _goalY, _goalTimer, _zTarget, _depthTimer, _z;
    private float _nibbleTimer;                           // floating: when it next looks for a petal to nibble
    private PointF? _nibble;                              // the petal it is drifting toward, screen pixels
    private double _time;

    /// <summary>Where the head leaves the shell, as s along the body: the head and neck ahead of this turn on the neck; the shell behind it is rigid.</summary>
    private const float NeckAt = 0.22f;

    // ---- THE MIND ----
    private readonly float _sunny, _nerve;                // the two dials (see the class comment)
    private readonly Mind<Doing> _mind = new(Doing.Cruise);
    private readonly Cooldown _hauls = new();             // not another haul-out until this has run out
    private Doing _doing => _mind.Doing;
    private Turtle[] _peers = [];
    private PointF? _snake;                               // the snake's head, if out (last frame's)

    // The haul-out in hand.
    private Perch _perch;
    private PointF _far, _near, _sitMid;                  // the far staging point, the near one, and where the shell's middle will rest
    private float _comeIn, _sitFace;                      // the heading it arrives with, and the one it sits with (ground radians)
    private float _refused, _replan;                      // seconds in a row the water refused its step (Approach gives up past 1.5), and the wait to re-aim at the island
    private PointF _b0, _b1, _b2, _b3;                    // the curve of a climb or a slip, screen pixels
    private float _scriptDur;
    private bool _ringed;
    // Sitting still, with a little life: a slow turn of the head and now and then a leg stretched out.
    private float _align;                                 // the turn still to make around the middle after a climb (a log is climbed square on and then lain along), radians
    private bool _pivoting;                               // turning on the spot, before a slip, to face the way it will leave
    private float _exitHeading, _exitAngle;
    private float _headOff, _turnFrom, _turnTo, _turnT = 1, _turnTimer, _stretch, _stretchT = 1, _stretchTimer;

    private readonly float[] _h = new float[Points];      // how far up (0 to 1) each spine point is this frame
    private float _out;                                   // the highest of them: 0 = all in the water

    /// <summary>How deep it is: 0 at the surface, 1 as deep as a koi goes. A turtle stays near the top. 0 while any of it is up on a perch.</summary>
    public float Z => _z * (1 - MathF.Min(1, 4 * _out));

    /// <summary>The screen box it can paint in this frame (shell, head, legs and their soft edges), for the shared draw order.</summary>
    public RectangleF Box
    {
        get
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (PointF p in Spine) { x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y); x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y); }
            float m = _wide * 2.5f + _len * 0.5f + _pond.U * 0.01f;       // the shell's width, the legs reaching out, the soft edge
            return RectangleF.FromLTRB(x0 - m, y0 - m, x1 + m, y1 + m);
        }
    }
    public PointF Nose => Spine[0];
    public PointF Middle => Spine[Points / 2];

    /// <summary>True while all of it is in the pond: it then throws and receives the koi's cross shadows, and is hazed and sunk by its depth. False the moment any of it is up on a perch.</summary>
    public bool InWater => _out < 0.001f;

    /// <summary>What it is doing now, and which perch (if any), for logs and tests (never read by the drawing).</summary>
    public string State => _mind.ToString();
    public string PerchName => _perch.ToString();

    /// <summary>The highest part of the body above the water, 0 to 1 (for logs and tests).</summary>
    public float Height01 => _out;

    public Turtle(Pond pond, Ripples ripples, bool[] where, Random rng, float sunny, float nerve, bool[] perchMask, Petals? petals = null) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _where = where;
        _perchMask = perchMask;
        _rng = rng;
        _petals = petals;
        _room = RoomAround;
        _sunny = sunny;
        _nerve = nerve;
        _len = pond.U * 0.1f;
        _wide = _len * 0.24f;
        _skin = Skin(rng);
        if (pond.Log is Log log) { _logAx = MathF.Cos(log.Angle); _logAy = MathF.Sin(log.Angle); }
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);

        for (int tries = 0; tries < 500; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _len * 1.4f) continue;
            (_x, _y) = ToGround(x, y);
            break;
        }
        _heading = (float)(rng.NextDouble() * MathF.Tau);
        _speed = _len * 0.4f;
        _z = 0.1f;
        _zTarget = _z;
        _depthTimer = 5 + 10 * (float)rng.NextDouble();
        _mind.Begin(Doing.Cruise, 15 + 20 * (float)rng.NextDouble());
        _hauls.Start((5 + 35 * (float)rng.NextDouble()) * (1.3f - _sunny));   // the first visit comes sooner for a sunny one
        NewGoal();
        LaySpine();
    }

    /// <summary>The other turtles, so two never choose the same perch.</summary>
    public void MeetPeers(Turtle[] all) => _peers = all;

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static PointF Toward(PointF p, PointF dir, float d) => new(p.X + dir.X * d, p.Y + dir.Y * d);
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>The ground heading (what _heading holds) of a direction drawn on the screen: the slant squashes up and down, so undo that.</summary>
    private static float GroundHeading(float sx, float sy) => MathF.Atan2(sy / Ripples.Squash, sx);

    /// <summary>The screen direction (a unit vector) of a ground heading.</summary>
    private static PointF ScreenDir(float heading)
    {
        float dx = MathF.Cos(heading), dy = MathF.Sin(heading) * Ripples.Squash, l = MathF.Sqrt(dx * dx + dy * dy);
        return new PointF(dx / l, dy / l);
    }

    private bool WaterAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Water[iy * _pond.Width + ix];
    }

    private bool LandAt(PointF p)
    {
        int ix = (int)p.X, iy = (int)p.Y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Land[iy * _pond.Width + ix];
    }

    /// <summary>The bed under a screen point, 0 to 1 of the pond's full depth, rocks included (a point off the screen counts as its nearest edge pixel).</summary>
    private float FloorAt(PointF s) => _pond.Floor[Math.Clamp((int)s.Y, 0, _pond.Height - 1) * _pond.Width + Math.Clamp((int)s.X, 0, _pond.Width - 1)] / 255f;

    /// <summary>The pond's full depth in ground pixels: what Z = 1 is, so the feelers and the chains work in one unit.</summary>
    private float DepthPx => Pond.DepthU * _pond.U;

    /// <summary>Where its nose is in three dimensions: ground pixels across, depth in pixels down.</summary>
    private System.Numerics.Vector3 Here => new(_x, _y, _z * DepthPx);

    /// <summary>
    /// The world as the turtle's feelers see it (Core/Minds Feelers.Room): the
    /// koi's answer (Koi.RoomAround), for a ball as wide as this shell. Nothing
    /// on land, in the bed or above the surface; else the room to the nearest
    /// bank or rock, or to the nearest other body in the water if that is
    /// closer, a gap up or down counting two and a half times a gap sideways
    /// (it passes over a fish's back on less clearance than it passes beside
    /// a bank). Before this a turtle steered round the bank and the other
    /// turtles only, and ploughed through any koi in its way (the koi was
    /// shoved out of it, but a shove is a bump, not a course).
    /// </summary>
    private float RoomAround(in System.Numerics.Vector3 p)
    {
        PointF s = ToScreen(p.X, p.Y);
        if (!WaterAt(s)) return 0;
        if (p.Z < -1 || p.Z > 0.9f * FloorAt(s) * DepthPx) return 0;
        float room = _pond.RoomAt(s.X, s.Y);
        foreach (Body b in _bodies)
        {
            if (b == this || !b.Shape.Near(p, _wide, room)) continue;
            room = MathF.Min(room, b.Shape.Gap(p, _wide, 2.5f));
            if (room <= 0) return 0;
        }
        return room;
    }

    /// <summary>
    /// Moved bodily by KoiSchool when two turtles meet (half the way out each;
    /// a koi in a turtle is the one moved, the turtle being the heavier). Only
    /// while it is swimming: a turtle up on a perch, or climbing, is where it
    /// is. The bed and the surface keep the last word on its depth.
    /// </summary>
    public void Nudge(System.Numerics.Vector3 by)
    {
        if (!InWater || _doing is Doing.Climb or Doing.Slip or Doing.Bask) return;
        PointF to = ToScreen(_x + by.X, _y + by.Y);
        if (WaterAt(to)) { _x += by.X; _y += by.Y; }
        _z = Math.Clamp(_z + by.Z / DepthPx, 0, 0.05f + 0.9f * FloorAt(ToScreen(_x, _y)));
        LaySpine();
    }

    /// <summary>Its body length, ground pixels (the school's shove is held to a length a second).</summary>
    public float Length => _len;

    /// <param name="bodies">Every body in the water this frame (the koi and the turtles); null when there are none to steer round.</param>
    public void Update(float dt, PointF? snakeHead = null, Body[]? bodies = null)
    {
        if (_perch == Perch.None) { _pond.IslandPerch.Release(this); _pond.LogPerch.Release(this); }   // a perch is mine only while _perch names it
        _time += dt;
        float t = (float)(_time % 100000.0);
        _snake = snakeHead;
        _bodies = bodies ?? [];
        _mind.Tick(dt);
        _hauls.Tick(dt);

        switch (_doing)
        {
            case Doing.Climb:
            case Doing.Slip: Script(dt); break;
            case Doing.Bask: Sit(dt); break;
            default: Swim(dt, t); break;
        }

        // ---- Depth: near the surface, a little deeper now and then (and the whole way down in a Dive) ----
        if (_doing == Doing.Cruise)
        {
            _depthTimer -= dt;
            if (_depthTimer <= 0)
            {
                _zTarget = 0.03f + 0.3f * (float)_rng.NextDouble();
                _depthTimer = 8 + 10 * (float)_rng.NextDouble();
            }
        }
        PointF here = ToScreen(_x, _y);
        float bottom = 0.05f + 0.9f * FloorAt(here);   // the bed, rocks included (never inside a stone)
        // Coming up to a perch is quicker than a lazy drift up (it must be at the surface to climb out).
        float rate = _doing is Doing.Approach or Doing.Climb or Doing.Bask or Doing.Slip ? 1.2f : _doing == Doing.Dive ? 0.45f : 0.3f;
        bool swimming = _doing is not (Doing.Climb or Doing.Slip or Doing.Bask);
        // When the feelers chose a climb or a dive to clear something, the depth
        // follows that slope for the distance swum (a straight course in three
        // dimensions); otherwise it eases toward the depth it wants. Either way
        // the change is held to what its own forward motion can hide on the
        // screen (Light.HoldDepth), as the koi's is, so a turtle never slides
        // up or down the screen while it floats or dives.
        float zNext = _dodge != 0 && swimming ? _z + _dodge * _speed * dt / DepthPx : _z + (MathF.Min(_zTarget, bottom) - _z) * (1 - MathF.Exp(-dt * rate));
        zNext = Math.Clamp(zNext, 0, bottom);
        _z = swimming ? Light.HoldDepth(_z, zNext, _heading, _speed, dt, _pond.U) : zNext;

        // The head on its neck: it leads a turn (about half the turn's rate, so
        // the head is already looking round the bend the shell is still
        // swinging into) and, while basking, looks about (_headOff). Eased,
        // so it never flicks.
        float neckWant = _doing == Doing.Bask ? _headOff * 2f : Math.Clamp(_turnRate * 0.45f, -0.5f, 0.5f);
        _neck += (neckWant - _neck) * (1 - MathF.Exp(-dt * 4f));

        LaySpine();
    }

    // ============================== SWIMMING ==============================

    /// <summary>Cruise, Float, Dive and Approach: everything where it is paddling (or floating) in the water.</summary>
    private void Swim(float dt, float t)
    {
        if (_mind.Over)
        {
            if (_doing == Doing.Approach) GiveUp(); else Decide();
        }
        bool floating = _doing == Doing.Float, approach = _doing == Doing.Approach;

        // ---- Paddle, or float and rest ----
        _stroke += ((floating ? 0f : 1f) - _stroke) * (1 - MathF.Exp(-dt * 1.5f));
        _phase = (_phase + MathF.Tau * 0.9f * dt * _stroke) % MathF.Tau;
        // Each pair of legs pushes during its quick sweep back (see Stroke); the push is
        // eased in and out, so the surge is a swell, not a kick.
        float push = MathF.Max(Thrust(_phase), Thrust(_phase + MathF.PI));
        float targetSpeed = floating ? 0 : _len * 0.5f * (0.65f + 0.7f * push);
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (floating ? 0.8f : 2.5f)));

        // ---- Steering: a slow wander, a pull toward its goal, and the bank kept off ----
        PointF here = ToScreen(_x, _y);
        bool free = false;       // "free": right beside a perch, where the bank rules would push it away from the very thing it wants
        float turn = 0, most = 0.35f;
        if (!floating)
        {
            if (approach)
            {
                // The way in is a line from _far to _near, and it must arrive pointing along it. Far out it
                // simply heads for _far; once close it chases a point a little way AHEAD of itself on that
                // line (pure pursuit: a dog running at a ball it is lining up with a path), so it
                // swings onto the line instead of curving past the spot and into the bank.
                PointF e = new(_near.X - _far.X, _near.Y - _far.Y);
                float el = MathF.Max(1e-3f, MathF.Sqrt(e.X * e.X + e.Y * e.Y));
                e = new PointF(e.X / el, e.Y / el);
                float along = (here.X - _near.X) * e.X + (here.Y - _near.Y) * e.Y;            // negative: still before _near
                float cross = MathF.Abs(-(here.X - _near.X) * e.Y + (here.Y - _near.Y) * e.X);
                // "Lined up": close, on the line (not off to one side) and not yet past _near. Only then
                // does the bank rule stand aside; a turtle that comes at the perch from the side, or past
                // the end of a log, simply heads for _far first and lines up from there.
                free = Dist(here, _near) < _len * 3.2f && cross < _len * 1.2f && along < 0.2f * _len;
                if (!free && _perch == Perch.Island)
                {
                    // The island is round, so the best side to come in from is the side the turtle is on NOW.
                    _replan -= dt;
                    if (_replan <= 0)
                    {
                        _replan = 0.5f;
                        if (PlanIsland(here, out Plan q)) { _far = q.Far; _near = q.Near; _sitMid = q.Mid; _comeIn = q.ComeIn; _sitFace = q.Face; }
                    }
                }
                PointF aim = free ? Toward(_near, e, along + 0.8f * _len) : _far;
                (float ax, float ay) = ToGround(aim.X, aim.Y);
                // On the line, close, pointing the way it should, and up at the surface: climb.
                if (free && along > -0.5f * _len && cross < 0.45f * _len && MathF.Abs(Wrap(_heading - _comeIn)) < 0.9f && _z < 0.12f)
                {
                    BeginClimb(here);
                    return;
                }
                // Far out the pull toward the perch is gentle (0.6, like a cruising goal's 0.3 but keener), so the
                // bank reflex below can always win: a strong pull and a bank on the shortest way round is a
                // deadlock (it pushed into the bank for 4 s and gave up). Beside the perch the pull is keen.
                turn = Wrap(MathF.Atan2(ay - _y, ax - _x) - _heading) * (free ? 1.6f : 0.6f);
                most = free ? 0.9f : 0.6f;
                if (!free) Feel(ref turn, ref most, dt);           // the bank, the bed and every other body, on the way to the perch too
            }
            else
            {
                turn = 0.15f * MathF.Sin(t * 0.17f);
                _goalTimer -= dt;
                float gdx = _goalX - _x, gdy = _goalY - _y;
                if (_goalTimer <= 0 || gdx * gdx + gdy * gdy < _len * _len * 4) NewGoal();
                turn += Wrap(MathF.Atan2(gdy, gdx) - _heading) * 0.3f;
                Feel(ref turn, ref most, dt);
            }
            // Room from the other turtles: two shells at one depth must not pass through each
            // other (the koi keep their distance the same way). Measured SHELL to shell (the
            // middles), not nose to nose: a first version measured the noses and changed
            // nothing, because two shells overlap long before two noses are close. The swing
            // away is allowed to be quicker than a cruising turn, and when the other is AHEAD
            // the paddling eases off: a rigid shell cannot turn tight enough to clear a shell
            // right in front of it, so it slows instead, as a real one does.
            foreach (Turtle p in _peers)
            {
                if (p == this || !p.InWater || MathF.Abs(p.Z - Z) > 0.12f) continue;
                PointF m = Middle, pm = p.Middle;
                float ox = pm.X - m.X, oy = pm.Y - m.Y, od = MathF.Sqrt(ox * ox + oy * oy);
                if (od > _len * 1.2f || od < 1e-3f) continue;
                float near = 1 - od / (_len * 1.2f);
                turn += Wrap(GroundHeading(-ox, -oy) - _heading) * near * 3f;
                most = MathF.Max(most, 1.2f);
                if (MathF.Abs(Wrap(GroundHeading(ox, oy) - _heading)) < 1.1f) _speed *= MathF.Max(0f, 1 - 2.5f * near * dt);
            }
            // The turning circle: a rigid shell turns in an arc, and the middle of it drifts sideways
            // if the arc is tighter than about 0.6 of a body length (it would then look to be going
            // tail first). A turn can be no sharper than speed / (0.6 body lengths); never less than
            // the speed of a slow stroke, so a bank can always be answered.
            most = MathF.Min(most, MathF.Max(_speed, _len * 0.25f) / ((approach ? 0.85f : 0.6f) * _len));   // a touch wider when lining up on a perch, where the eye is on it
            turn = Math.Clamp(turn, -most, most);
        }
        else
        {
            // Floating, it does not steer; but a petal within reach is worth a
            // slow drift over (a slider nibbles at what floats by), and the
            // turn eases to nothing otherwise.
            turn = 0;
            if (_petals != null)
            {
                _nibbleTimer -= dt;
                PointF nose = Spine[0];
                if (_nibble is PointF np && !_petals.NearestFloating(np.X, np.Y, _len * 0.15f, out _)) _nibble = null;   // it drifted off or sank
                if (_nibble == null && _nibbleTimer <= 0)
                {
                    _nibbleTimer = 2 + 4 * (float)_rng.NextDouble();
                    if (_petals.NearestFloating(nose.X, nose.Y, _len * 1.5f, out PointF p) && WaterAt(p) && _pond.RoomAt(p.X, p.Y) > _len * 0.3f) _nibble = p;
                }
                if (_nibble is PointF target)
                {
                    if (Dist(nose, target) < _len * 0.3f)
                    {
                        _petals.Take(nose.X, nose.Y, _len * 0.3f);
                        _ripples.Add(nose.X, nose.Y, 0.3f);
                        _nibble = null;
                    }
                    else
                    {
                        (float tx, float ty) = ToGround(target.X, target.Y);
                        turn = Wrap(MathF.Atan2(ty - _y, tx - _x) - _heading) * 0.8f;
                        _speed += (_len * 0.18f - _speed) * (1 - MathF.Exp(-dt * 1.5f));   // a slow paddle, legs barely moving
                    }
                }
            }
            turn = Math.Clamp(turn, -0.4f, 0.4f);
        }
        // The turn is never applied raw. The RATE of turn eases toward what the
        // steering asks over about a sixth of a second: a shell has mass. The
        // steering's sum (a goal pull, the bank's shove, a shell to keep off)
        // switched on and off from one frame to the next, and the heading's
        // rate flipped sign sixty times a second (72 radians a second squared
        // on the timeline, a jitter the owner saw as "not fluid").
        _turnRate += (turn - _turnRate) * (1 - MathF.Exp(-dt * 6f));
        _heading = Wrap(_heading + _turnRate * dt);
        if (floating)
        {
            // A floating turtle does not steer, but it does not let another shell drift into it
            // either: it eases away, slowly, a sixth of a body length a second at most.
            foreach (Turtle p in _peers)
            {
                if (p == this || !p.InWater || MathF.Abs(p.Z - Z) > 0.12f) continue;
                float ox = p._x - _x, oy = p._y - _y, od = MathF.Sqrt(ox * ox + oy * oy);
                if (od > _len * 1.2f || od < 1e-3f) continue;
                float ease = (1 - od / (_len * 1.2f)) * _len * 0.17f * dt;
                PointF to = ToScreen(_x - ox / od * ease, _y - oy / od * ease);
                if (WaterAt(to) && _pond.RoomAt(to.X, to.Y) > _len * 0.2f) { _x -= ox / od * ease; _y -= oy / od * ease; }
            }
        }
        float step = _speed * dt;
        PointF next = ToScreen(_x + MathF.Cos(_heading) * step, _y + MathF.Sin(_heading) * step);
        bool ok;
        if (free) ok = WaterAt(next);              // beside a perch the test is the plain one: is the water there?
        else
        {
            float roomNext = _pond.RoomAt(next.X, next.Y);
            ok = roomNext > _len * 0.2f || roomNext >= _pond.RoomAt(here.X, here.Y);   // never stuck: toward more room is always allowed
        }
        if (ok)
        {
            _x += MathF.Cos(_heading) * step;
            _y += MathF.Sin(_heading) * step;
            _refused = 0;
        }
        else
        {
            // A refused step itself sends the feelers looking next frame (_refused > 0);
            // on an approach, 1.5 s of it gives the perch up: never stand refused.
            _refused += dt;
            if (approach && _refused > 1.5f) GiveUp();
        }
    }

    /// <summary>
    /// The feelers (Core/Minds Feelers) reach out along the heading in three
    /// dimensions, asking RoomAround at each sample: the bank, the bed, the
    /// surface and every other body in the water all count. Something in the
    /// way (or a step refused last frame): the smallest change of course that
    /// clears it, a turn, a climb over or a dive under, is added to "turn",
    /// "most" is raised so it can be made, and it slows for a turn (not for a
    /// climb). A turtle dives shallower than a koi (0.5 of its forward motion
    /// against 0.8): it is a paddler, not a darter. This replaced a bank-only
    /// rule read off the room grid, which switched on and off from frame to
    /// frame and knew nothing of the fish.
    /// </summary>
    private void Feel(ref float turn, ref float most, float dt)
    {
        float look = _len * 1.6f;
        System.Numerics.Vector3 here3 = Here, level = new(MathF.Cos(_heading), MathF.Sin(_heading), 0);
        float roomAhead = Feelers.Along(_room, in here3, in level, look);
        _dodge = 0;
        if (roomAhead >= _len * 0.8f && _refused <= 0) return;
        (float best, float climb, _) = Feelers.Best(_room, in here3, _heading, look, _len * 0.9f, 0.35f, 4, 0.5f, _len * 0.08f, _len * 0.05f);
        _dodge = climb;
        float urgency = Math.Clamp(1 - roomAhead / (_len * 0.8f), 0, 1);
        turn += Wrap(best - _heading) * (1f + 2f * urgency);
        most = MathF.Max(most, 0.6f + 0.6f * urgency);
        if (climb == 0) _speed *= 1 - 0.8f * urgency * dt;
    }

    private void NewGoal()
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _len * 0.6f) continue;
            (_goalX, _goalY) = ToGround(x, y);
            break;
        }
        _goalTimer = 20 + 25 * (float)_rng.NextDouble();
    }

    /// <summary>The open water "dist" ahead along heading a, checked halfway too.</summary>
    private float RoomAlong(float a, float dist)
    {
        PointF mid = ToScreen(_x + MathF.Cos(a) * dist * 0.5f, _y + MathF.Sin(a) * dist * 0.5f);
        PointF end = ToScreen(_x + MathF.Cos(a) * dist, _y + MathF.Sin(a) * dist);
        return MathF.Min(_pond.RoomAt(end.X, end.Y), _pond.RoomAt(mid.X, mid.Y) * 1.5f);
    }

    // ============================== THE DICE ==============================

    /// <summary>When a doing ends: roll for the next. Cruise and Float take turns as they always did; Dive and a haul-out are other faces.</summary>
    private void Decide()
    {
        bool fromCruise = _doing == Doing.Cruise;
        Span<float> w = stackalloc float[4];   // Cruise, Float, Dive, Haul out
        w[0] = fromCruise ? 0.3f : 3f;
        w[1] = fromCruise ? 3f : 0f;
        w[2] = fromCruise ? 1.2f : 0f;
        w[3] = _hauls.Ready ? 0.8f + 5f * _sunny : 0f;     // a sunny turtle takes every chance
        switch (Dice.Pick(_rng, w))
        {
            case 1:
                _mind.Begin(Doing.Float, 8 + 14 * (float)_rng.NextDouble());
                _zTarget = 0;                                           // up to the surface for a breath
                PointF nose = Spine[0];
                _ripples.Add(nose.X, nose.Y, 0.5f);
                break;
            case 2:
                _mind.Begin(Doing.Dive, 8 + 12 * (float)_rng.NextDouble());
                _zTarget = 0.4f + 0.5f * (float)_rng.NextDouble();      // down to the bed (the floor under it caps this)
                NewGoal();
                break;
            case 3:
                if (HaulOut()) break;
                _hauls.Start(20);                                       // nothing to climb onto just now: try again later
                StartCruise();
                break;
            default:
                StartCruise();
                break;
        }
    }

    private void StartCruise()
    {
        _mind.Begin(Doing.Cruise, 20 + 30 * (float)_rng.NextDouble());
        _zTarget = 0.05f + 0.2f * (float)_rng.NextDouble();
        _depthTimer = 8 + 10 * (float)_rng.NextDouble();
        NewGoal();
    }

    // ============================== PERCHES ==============================

    /// <summary>Another animal holds this perch, or is on its way to it (the shared claim in Pond: the other turtle, or the snake for the log).</summary>
    private bool PerchTaken(Perch kind) => !(kind == Perch.Island ? _pond.IslandPerch : _pond.LogPerch).Free(this);

    private bool SnakeNear(PointF p) => _snake is PointF s && Dist(s, p) < _pond.U * 0.15f;

    /// <summary>
    /// Is the straight swim from the nose to the staging point clear of bank and rocks (room for the whole
    /// shell all the way)? The approach is a straight line, and a straight line across a corner of land
    /// is a turtle that pushes into the bank for ever, so a perch with the bank in the way is not chosen
    /// from here; it is chosen later, from wherever the turtle has wandered to.
    /// </summary>
    private bool LineClear(PointF nose, PointF far)
    {
        const int samples = 16;
        for (int i = 3; i <= samples; i++)
        {
            float k = i / (float)samples;
            if (_pond.RoomAt(nose.X + (far.X - nose.X) * k, nose.Y + (far.Y - nose.Y) * k) < _len * 0.45f) return false;
        }
        return true;
    }

    /// <summary>Is this a good place for the nose to wait, with the body trailing out behind it away from the perch (dir points away)? Open water all the way, in view, with room.</summary>
    private bool StageOk(PointF far, PointF near, PointF dir)
    {
        float m = _len;
        if (far.X < m || far.Y < m || far.X > _pond.Width - m || far.Y > _pond.Height - m) return false;
        if (!WaterAt(far) || !WaterAt(near)) return false;
        if (!WaterAt(Toward(near, dir, _len * 0.5f)) || !WaterAt(Toward(near, dir, _len * 0.95f))) return false;   // the shell and tail, trailing behind
        return _pond.RoomAt(far.X, far.Y) > _len * 0.4f;
    }

    /// <summary>
    /// The island's cap. It comes in from the side nearest the turtle (then,
    /// if that side is no good, the sides either way round), to a point just
    /// outside the cap's rim, and sits with the middle of its shell half a cap
    /// radius inside the rim, facing the way it came.
    /// </summary>
    private bool PlanIsland(PointF nose, out Plan plan)
    {
        plan = default;
        if (_pond.Island is not Island isle || PerchTaken(Perch.Island)) return false;
        PointF c = isle.Cap;
        float r = isle.CapRadius, a0 = MathF.Atan2(nose.Y - c.Y, nose.X - c.X);   // from the cap toward the turtle
        ReadOnlySpan<float> turns = [0, 0.7f, -0.7f, 1.4f, -1.4f, 2.1f, -2.1f, MathF.PI];
        foreach (float off in turns)
        {
            PointF outward = new(MathF.Cos(a0 + off), MathF.Sin(a0 + off));
            PointF near = Toward(c, outward, r + 0.4f * _len), far = Toward(c, outward, r + 1.6f * _len);
            if (!StageOk(far, near, outward) || !LineClear(nose, far)) continue;
            PointF mid = Toward(c, outward, 0.1f * r);                      // the shell's middle nearly on the cap's centre: the cap is small, and a shell hung half off it was mostly over water
            float face = GroundHeading(-outward.X, -outward.Y);
            if (SnakeNear(mid)) return false;
            plan = new Plan(Perch.Island, far, near, mid, face, face);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The log. It comes in from either side, square on, to the water beside
    /// the log, climbs onto the middle line and turns to lie ALONG the log.
    /// For a beached log, the water beside the sitting point is tested too:
    /// a log lying over the grass is no place to climb out of the pond.
    /// </summary>
    private bool PlanLog(PointF nose, out Plan plan)
    {
        plan = default;
        if (_pond.Log is not Log log || PerchTaken(Perch.Log)) return false;
        PointF axis = new(_logAx, _logAy), perp = new(-_logAy, _logAx);
        float side0 = (nose.X - log.Centre.X) * perp.X + (nose.Y - log.Centre.Y) * perp.Y >= 0 ? 1 : -1;   // the side the turtle is on first
        for (int tries = 0; tries < 8; tries++)
        {
            PointF m = log.At(0.35f + 0.3f * (float)_rng.NextDouble());
            if (!LandAt(m) || SnakeNear(m)) continue;                // the sitting point is ON the log, which is land
            float side = tries % 2 == 0 ? side0 : -side0;
            PointF outward = new(perp.X * side, perp.Y * side);
            if (!WaterAt(Toward(m, outward, log.Width * 0.5f + 0.15f * _len))) continue;   // water right beside the log here
            PointF near = Toward(m, outward, log.Width * 0.5f + 0.4f * _len), far = Toward(m, outward, log.Width * 0.5f + 1.6f * _len);
            if (!StageOk(far, near, outward) || !LineClear(nose, far)) continue;
            float sit = _rng.Next(2) == 0 ? 1 : -1;
            plan = new Plan(Perch.Log, far, near, m, GroundHeading(-outward.X, -outward.Y), GroundHeading(axis.X * sit, axis.Y * sit));
            return true;
        }
        return false;
    }

    /// <summary>Chooses a perch by distance and dice and sets off for it. False if there is none to go to.</summary>
    private bool HaulOut()
    {
        PointF nose = ToScreen(_x, _y);
        bool island = PlanIsland(nose, out Plan pi), log = PlanLog(nose, out Plan pl);
        Span<float> w = stackalloc float[2];
        w[0] = island ? 1 / (0.25f + Dist(nose, pi.Mid) / _pond.U) : 0;      // the nearer perch is the likelier
        w[1] = log ? 1 / (0.25f + Dist(nose, pl.Mid) / _pond.U) : 0;
        int pick = Dice.Pick(_rng, w);
        if (pick < 0) return false;
        Plan p = pick == 0 ? pi : pl;
        _perch = p.Kind;
        (p.Kind == Perch.Island ? _pond.IslandPerch : _pond.LogPerch).Take(this);   // mine from now until I am off it (released every frame that _perch is None)
        _far = p.Far; _near = p.Near; _sitMid = p.Mid; _comeIn = p.ComeIn; _sitFace = p.Face;
        _refused = 0;
        _zTarget = 0;
        _mind.Begin(Doing.Approach, 50);                                        // not there in 50 s: give it up
        return true;
    }

    private void GiveUp()
    {
        _perch = Perch.None;
        _hauls.Start(25 + 30 * (float)_rng.NextDouble());
        StartCruise();
    }

    /// <summary>The screen spot of the nose when the middle of the shell rests at _sitMid, facing the way it climbed (_comeIn).</summary>
    private PointF SitNose() => NoseFor(_sitMid, _comeIn);

    /// <summary>The nose for a shell whose middle is at the screen point mid and which faces ground heading h.</summary>
    private PointF NoseFor(PointF mid, float h)
    {
        (float mx, float my) = ToGround(mid.X, mid.Y);
        float m = Along[Points / 2] * _len;
        return ToScreen(mx + MathF.Cos(h) * m, my + MathF.Sin(h) * m);
    }

    /// <summary>Lays the climb or slip curve from a to b, leaving a along heading ha and arriving at b along hb.</summary>
    private void SetCurve(PointF a, float ha, PointF b, float hb)
    {
        float k = Dist(a, b) / 3;
        PointF ta = ScreenDir(ha), tb = ScreenDir(hb);
        _b0 = a; _b1 = Toward(a, ta, k); _b2 = Toward(b, tb, -k); _b3 = b;
    }

    private PointF Bezier(float u)
    {
        float v = 1 - u, a = v * v * v, b = 3 * v * v * u, c = 3 * v * u * u, d = u * u * u;
        return new PointF(a * _b0.X + b * _b1.X + c * _b2.X + d * _b3.X, a * _b0.Y + b * _b1.Y + c * _b2.Y + d * _b3.Y);
    }

    private PointF BezierSlope(float u)
    {
        float v = 1 - u;
        return new PointF(
            3 * v * v * (_b1.X - _b0.X) + 6 * v * u * (_b2.X - _b1.X) + 3 * u * u * (_b3.X - _b2.X),
            3 * v * v * (_b1.Y - _b0.Y) + 6 * v * u * (_b2.Y - _b1.Y) + 3 * u * u * (_b3.Y - _b2.Y));
    }

    /// <summary>It has reached the water beside the perch, pointing the right way: haul out.</summary>
    private void BeginClimb(PointF nose)
    {
        PointF f = SitNose();
        SetCurve(nose, _heading, f, _comeIn);
        // Slow enough to read as a climb: about a third of a body length a second, 1.8 to 3 s.
        _scriptDur = Math.Clamp(Dist(nose, f) * 1.15f / (0.3f * _len), 1.8f, 3f);
        _zTarget = 0;
        _ringed = false;
        _mind.Begin(Doing.Climb, _scriptDur);
    }

    /// <summary>
    /// Climb and Slip: the nose follows the curve (_b0 to _b3) and the
    /// heading follows the curve's slope, so it is always nose first.
    /// A climb starts at paddling speed and comes to rest on the perch; a slip
    /// starts from rest and leaves at paddling speed. How high each part is
    /// is never set here: Height() reads it from where the part is.
    /// </summary>
    private void Script(float dt)
    {
        bool climb = _doing == Doing.Climb;
        float p = Math.Clamp(1 - _mind.Left / _scriptDur, 0, 1);
        // The ease: 1.4p + 0.2p^2 - 0.6p^3 starts at 1.4 times the average speed and ends at rest;
        // p^2(2 - p) starts at rest and ends at the average speed.
        float u = climb ? 1.4f * p + 0.2f * p * p - 0.6f * p * p * p : p * p * (2 - p);
        PointF s = Bezier(u), slope = BezierSlope(u);
        PointF was = ToScreen(_x, _y);
        (_x, _y) = ToGround(s.X, s.Y);
        if (slope.X * slope.X + slope.Y * slope.Y > 1e-6f) _heading = GroundHeading(slope.X, slope.Y);
        float moved = Dist(was, s) / MathF.Max(dt, 1e-4f);
        _speed += (moved - _speed) * (1 - MathF.Exp(-dt * 6));

        // Legs: paddling while it swims toward the perch, out and still once it is up; paddling again as it goes in.
        float want = climb ? (p < 0.7f ? 1f : 0f) : (p > 0.3f ? 1f : 0f);
        _stroke += (want - _stroke) * (1 - MathF.Exp(-dt * 3));
        _phase = (_phase + MathF.Tau * 0.9f * dt * _stroke) % MathF.Tau;

        if (!climb && !_ringed && p > 0.55f)
        {
            _ringed = true;
            PointF mid = Spine[Points / 2];
            _ripples.Add(mid.X, mid.Y, 0.6f);                      // the one ring, as it goes in
        }
        if (!_mind.Over) return;

        if (climb)
        {
            (_x, _y) = ToGround(_b3.X, _b3.Y);
            _heading = _comeIn;
            _align = Wrap(_comeIn - _sitFace);                       // 0 on the island; a quarter turn to go on the log
            _pivoting = false;
            _headOff = 0; _turnT = 1; _stretchT = 1; _stretch = 0;
            _turnTimer = 3 + 5 * (float)_rng.NextDouble();
            _stretchTimer = 8 + 12 * (float)_rng.NextDouble();
            float hi = 28 + 32 * _sunny;
            float seconds = (20 + (hi - 20) * (float)_rng.NextDouble()) * (1.1f - 0.3f * _nerve);   // a sunny one up to a minute; a jumpy one cuts it short
            _mind.Begin(Doing.Bask, seconds);
        }
        else
        {
            _perch = Perch.None;                                    // off the perch: free for another turtle
            _turnRate = 0;                                          // the curve set the heading; the paddling starts from a straight course
            _hauls.Start((45 + 60 * (float)_rng.NextDouble()) * (1.5f - _sunny));
            _mind.Begin(Doing.Dive, 4 + 5 * (float)_rng.NextDouble());   // a short dive, then Decide() sends it cruising
            _zTarget = 0.25f + 0.3f * (float)_rng.NextDouble();
            NewGoal();
        }
    }

    /// <summary>Bask: still, legs out, with a slow turn of the head now and then and one leg stretched. Slips in when the time is up or the snake comes near.</summary>
    private void Sit(float dt)
    {
        _speed = 0;
        _stroke += (0 - _stroke) * (1 - MathF.Exp(-dt * 1.5f));
        _zTarget = 0;

        // A few degrees of head turn over a second or two, about the middle of the shell (so the shell does not slide).
        _turnTimer -= dt;
        if (_turnTimer <= 0 && _turnT >= 1 && !_pivoting)
        {
            _turnFrom = _headOff;
            _turnTo = ((float)_rng.NextDouble() - 0.5f) * 0.24f;
            _turnT = 0;
            _turnTimer = 6 + 8 * (float)_rng.NextDouble();
        }
        if (_turnT < 1)
        {
            _turnT = MathF.Min(1, _turnT + dt / 1.8f);
            _headOff = _turnFrom + (_turnTo - _turnFrom) * Smooth(_turnT);
        }
        _stretchTimer -= dt;
        if (_stretchTimer <= 0 && _stretchT >= 1) { _stretchT = 0; _stretchTimer = 15 + 20 * (float)_rng.NextDouble(); }
        if (_stretchT < 1) { _stretchT = MathF.Min(1, _stretchT + dt / 2.2f); _stretch = MathF.Sin(MathF.PI * _stretchT); }
        else _stretch = 0;

        // Turning on the spot, about the middle of the shell (so it does not slide): first the quarter
        // turn that lays it along the log, later the turn to face the way it will leave.
        if (_pivoting)
        {
            float diff = Wrap(_exitHeading - _sitFace);
            _sitFace += Math.Clamp(diff, -0.9f * dt, 0.9f * dt);
        }
        else if (_align != 0)
        {
            float rate = (0.12f + 0.9f * MathF.Min(1, MathF.Abs(_align))) * dt;     // eases out as it comes round
            _align = MathF.Abs(_align) <= rate ? 0 : _align - MathF.Sign(_align) * rate;
        }
        _heading = _sitFace + _align;                                           // the head's look-about (_headOff) is on the neck now (Update), so the shell stays put
        _turnRate = 0;
        (float mx, float my) = ToGround(_sitMid.X, _sitMid.Y);
        float m = Along[Points / 2] * _len;
        _x = mx + MathF.Cos(_heading) * m;
        _y = my + MathF.Sin(_heading) * m;

        if (_pivoting)
        {
            if (MathF.Abs(Wrap(_exitHeading - _sitFace)) < 0.02f) BeginSlip();
            return;
        }
        // A nervous turtle goes in while the snake is still well off; a calm one lets it come close.
        bool scared = _snake is PointF s && Dist(s, Spine[Points / 2]) < _pond.U * (0.04f + 0.06f * _nerve);
        if (scared || _mind.Over) StartSlip();
    }

    /// <summary>
    /// Off the perch and into the water. Island: forward or to either side
    /// (up to a right angle); log: off either side. First it turns on the spot
    /// to face the way out (see Sit), then slides straight in. The exit point
    /// is far enough out that the whole body ends in the water, and is tested
    /// the way an approach point is. If every way is blocked it sits a few
    /// seconds more and tries again.
    /// </summary>
    private void StartSlip()
    {
        float sa = MathF.Atan2(ScreenDir(_heading).Y, ScreenDir(_heading).X);
        Span<float> good = stackalloc float[5];
        int count = 0;
        ReadOnlySpan<float> offs = [0, 0.8f, -0.8f, MathF.PI / 2, -MathF.PI / 2];
        foreach (float off in offs)
        {
            if (_perch == Perch.Log && MathF.Abs(off) < 1.5f) continue;     // off the SIDES of a log, never over its ends
            if (ExitOk(sa + off)) good[count++] = sa + off;
        }
        if (count == 0) { _mind.Left = 6; return; }                          // blocked: sit on and look again
        // Any clear side will do, unless the snake is what sent it: then the side furthest from the snake's head.
        int best = _rng.Next(count);
        if (_snake is PointF sh)
        {
            float furthest = -1;
            for (int k = 0; k < count; k++)
            {
                ExitPoint(good[k], out PointF x);
                float d = Dist(x, sh);
                if (d > furthest) { furthest = d; best = k; }
            }
        }
        _exitAngle = good[best];
        _exitHeading = GroundHeading(MathF.Cos(_exitAngle), MathF.Sin(_exitAngle));
        _sitFace = Wrap(_sitFace + _align);                                   // from now on the pivot starts from where it is facing
        _align = 0; _headOff = 0; _turnT = 1; _stretchT = 1; _stretch = 0;
        _pivoting = true;
    }

    /// <summary>Faced the right way: slide off along a straight line to the exit point.</summary>
    private void BeginSlip()
    {
        _pivoting = false;
        ExitPoint(_exitAngle, out PointF x);
        PointF n0 = ToScreen(_x, _y);
        SetCurve(n0, _heading, x, _exitHeading);
        _scriptDur = 1.7f;
        _ringed = false;
        _mind.Begin(Doing.Slip, _scriptDur);
    }

    /// <summary>Where the nose ends up on leaving along screen angle qa: far enough out that the tail is clear of the perch too.</summary>
    private bool ExitPoint(float qa, out PointF x)
    {
        PointF q = new(MathF.Cos(qa), MathF.Sin(qa));
        if (_perch == Perch.Island && _pond.Island is Island isle) { x = Toward(isle.Cap, q, isle.CapRadius + 1.3f * _len); return true; }
        if (_perch == Perch.Log && _pond.Log is Log log) { x = Toward(_sitMid, q, log.Width * 0.5f + 1.3f * _len); return true; }
        x = _sitMid;
        return false;
    }

    private bool ExitOk(float qa)
    {
        PointF q = new(MathF.Cos(qa), MathF.Sin(qa));
        if (!ExitPoint(qa, out PointF x)) return false;
        if (x.X < _len || x.Y < _len || x.X > _pond.Width - _len || x.Y > _pond.Height - _len) return false;
        if (!WaterAt(x) || !WaterAt(Toward(x, q, _len * 0.4f)) || _pond.RoomAt(x.X, x.Y) < _len * 0.3f) return false;
        // And the way there: where the nose will be once it has turned, then three quarters of the way along, must be in the water.
        float hq = GroundHeading(q.X, q.Y);
        PointF n0 = NoseFor(_sitMid, hq);
        SetCurve(n0, hq, x, hq);
        return WaterAt(Bezier(0.75f));
    }

    /// <summary>
    /// A point of the body on the ground plane (as if seen straight down, before
    /// the slant, the sink and the lift): s along from the nose (0 to 1), v
    /// across (-1 left edge to +1 right edge). The nose is at (_x, _y) and the
    /// shell lies BEHIND it, against the heading; the head and neck (s below
    /// NeckAt) turn as one piece about the shell's front by the neck angle, so
    /// the turtle looks round a bend before its shell has swung into it.
    /// </summary>
    private (float X, float Y) RawGround(float s, float v)
    {
        float dx = MathF.Cos(_heading), dy = MathF.Sin(_heading), k = Scale, across = v * _wide * Profile(s) * k;
        if (s >= NeckAt) return (_x - dx * s * _len * k - dy * across, _y - dy * s * _len * k + dx * across);
        float px = _x - dx * NeckAt * _len * k, py = _y - dy * NeckAt * _len * k;              // the shell's front, where the neck leaves it
        float hx = MathF.Cos(_heading + _neck), hy = MathF.Sin(_heading + _neck), ahead = (NeckAt - s) * _len * k;
        return (px + hx * ahead - hy * across, py + hy * ahead + hx * across);
    }

    /// <summary>The same point in screen pixels, before it sinks or is lifted.</summary>
    private PointF Raw(float s, float v)
    {
        (float gx, float gy) = RawGround(s, v);
        return ToScreen(gx, gy);
    }

    /// <summary>A point of the body as it is drawn: down the screen for its depth, up it for its height above the water.</summary>
    private PointF At(float s, float v)
    {
        PointF p = Raw(s, v);
        return new PointF(p.X, p.Y + Sink - Height(p) * LiftPx);
    }

    /// <summary>How far a hauled-out turtle is lifted up the screen at full height: 0.012u of height, times the view's lift (Light.Lift).</summary>
    private float LiftPx => Light.Lift * 0.012f * _pond.U;

    /// <summary>
    /// How high above the water a point of the body is, 0 to 1, from where it
    /// is: 0 over water, rising smoothly as it comes over the perch's edge, 1
    /// well inside. The perch is read from the PAINTED thing (the blockers in
    /// Pond.Land: the cap as the painter drew it, lumps and all, or the log),
    /// not from a circle or a box: a cap is a lumpy, squashed rock shape, and
    /// a circle of its nominal radius left the shell lifted over open water on
    /// most frames. The height is how much of a small ring round the point
    /// stands on the perch (nine samples), so it rises over a few pixels at
    /// the edge. Nothing when no perch is in hand.
    /// </summary>
    private float Height(PointF p)
    {
        float ring;
        if (_perch == Perch.Island && _pond.Island is Island isle) ring = isle.CapRadius * 0.35f;
        else if (_perch == Perch.Log && _pond.Log is Log log) ring = log.Width * 0.3f;
        else return 0;
        int on = LandAt(p) ? 1 : 0;
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.Tau / 8;
            if (LandAt(new PointF(p.X + MathF.Cos(a) * ring, p.Y + MathF.Sin(a) * ring))) on++;
        }
        return Smooth((on - 1.5f) / 6f);
    }

    /// <summary>How much smaller it is drawn for its depth, like a koi (Light.Scale): we look from a few body lengths up.</summary>
    private float Scale => Light.Scale(Z);

    /// <summary>How far down the screen its picture sits for its depth, like a koi's (Light.Sink). Small while it cruises near the top; a dive takes it down and, past Light.SeeU, out of sight.</summary>
    private float Sink => Light.SinkPx(Z, _pond.U);

    /// <summary>
    /// The straight spine from nose to tail, each pair of edge points squashed
    /// to the screen. The head and the tail are lifted by their own height;
    /// the SHELL is lifted as one piece, by the mean height under it: a shell
    /// is rigid, and lifting its front by its own height while its back stayed
    /// down drew it bent and stretched (up to 14% longer) on a small perch.
    /// </summary>
    private void LaySpine()
    {
        float most = 0, shellSum = 0;
        int shellCount = 0;
        Span<PointF> mid = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            PointF l = Raw(Along[i], -1), r = Raw(Along[i], 1);
            mid[i] = new((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            float h = Height(mid[i]);
            _h[i] = h;
            if (h > most) most = h;
            if (Along[i] >= 0.22f && Along[i] <= 0.88f) { shellSum += h; shellCount++; }
            HalfWidth[i] = Dist(l, r) / 2;
        }
        float shell = shellCount > 0 ? shellSum / shellCount : 0;
        float k = Scale, zPx = Z * Pond.DepthU * _pond.U;
        for (int i = 0; i < Points; i++)
        {
            if (Along[i] >= 0.22f && Along[i] <= 0.88f) _h[i] = shell;
            Spine[i] = new PointF(mid[i].X, mid[i].Y + Sink - _h[i] * LiftPx);
            // The same point for touching (Core/Minds Chain): on the ground
            // plane, before the slant, at its depth, neck bend included. The
            // koi's feelers see it and KoiSchool pushes a fish out of it (the
            // turtle is the heavier); two turtles that meet are each moved
            // half the way out (Nudge).
            float s = Along[i];
            (float gx, float gy) = RawGround(s, 0);
            Shape.Points[i] = new System.Numerics.Vector3(gx, gy, zPx);
            Shape.Radius[i] = _wide * Profile(s) * k;
        }
        Shape.Seal();
        _out = most;
    }

    /// <summary>
    /// How wide the body is along its length, as a fraction of the shell's
    /// half width: a rounded head on a neck, the oval shell from 0.22 to
    /// 0.88, and a short tail tapering to a point.
    /// </summary>
    private static float Profile(float s)
    {
        float head = 0;
        if (s < 0.04f) { float k = (0.04f - s) / 0.04f; head = 0.22f * MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        else if (s < 0.14f) head = 0.22f;
        else if (s < 0.22f) head = 0.22f - 0.06f * (s - 0.14f) / 0.08f;
        float shell = 0;
        if (s >= 0.22f && s <= 0.88f) { float k = (s - 0.55f) / 0.33f; shell = MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        float tail = s > 0.88f ? MathF.Max(0.03f, 0.12f - 0.09f * (s - 0.88f) / 0.12f) : 0;
        float neck = s >= 0.14f && s <= 0.9f ? 0.16f : 0;
        return MathF.Max(MathF.Max(head, shell), MathF.Max(tail, neck));
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        int vi = Math.Clamp((int)((v + 1) * 0.5f * SkinWide), 0, SkinWide - 1);
        uint c = _skin[Math.Clamp((int)(s * SkinLen), 0, SkinLen - 1) * SkinWide + vi];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        alpha = 1;
        return true;
    }

    /// <summary>
    /// The colours at every (s, v), worked out once. Across the shell, v is
    /// measured as a fraction of the shell's width THERE, so a band at
    /// |v| near 1 runs all the way round the rim: that is the ring of small
    /// marginal plates. The middle row (|v| small) and the side rows between
    /// are split along the shell by cross seams. The seams are pale, each
    /// plate a little lighter toward its own middle, and the whole dome is
    /// shaded brightest along the backbone.
    /// </summary>
    private static uint[] Skin(Random rng)
    {
        var skin = new uint[SkinLen * SkinWide];
        float hue = (float)rng.NextDouble();                            // a little variety: browner or greener shells
        (float R, float G, float B) shell = (66 + 20 * hue, 80 - 6 * hue, 40), seam = (118, 128, 66), skin0 = (74, 92, 48), stripe = (214, 196, 80);
        for (int si = 0; si < SkinLen; si++)
            for (int vi = 0; vi < SkinWide; vi++)
            {
                float s = (si + 0.5f) / SkinLen, v = (vi + 0.5f) / SkinWide * 2 - 1, av = MathF.Abs(v);
                (float R, float G, float B) c;
                if (s < 0.22f)
                {
                    // Head and neck: olive, two yellow stripes running back from the eyes, the red "ear" behind them.
                    c = skin0;
                    if (s > 0.03f && MathF.Abs(av - 0.5f) < 0.1f) c = Mix(c, stripe, 0.75f);
                    if (s > 0.1f && s < 0.19f && av > 0.62f && av < 0.95f) c = Mix(c, (196, 58, 40), 0.85f);
                    float dome = 0.7f + 0.3f * MathF.Sqrt(MathF.Max(0, 1 - v * v));
                    c = (c.R * dome, c.G * dome, c.B * dome);
                }
                else if (s <= 0.88f)
                {
                    float along = (s - 0.22f) / 0.66f;                  // 0 at the shell's front, 1 at its back
                    c = shell;
                    // Which plate: the rim ring, the side rows or the middle row.
                    int cols = av > 0.82f ? 10 : 5;                     // marginals are smaller: twice as many along
                    float ps = along * cols, pv = av > 0.82f ? 0 : (av - 0.36f) / 0.46f;   // position inside the plate, along
                    float plateS = ps - MathF.Floor(ps) - 0.5f;
                    float plateV = av > 0.82f ? (av - 0.91f) / 0.09f : av < 0.36f ? v / 0.36f : (av - 0.59f) / 0.23f;
                    float inside = 1 - MathF.Max(MathF.Abs(plateS) * 2, MathF.Abs(plateV));
                    c = Mix(c, (104, 116, 60), 0.35f * MathF.Max(0, inside));
                    // The seams.
                    bool seamS = MathF.Abs(plateS) > 0.5f - 0.022f;
                    bool seamV = MathF.Abs(av - 0.36f) < 0.012f || MathF.Abs(av - 0.82f) < 0.012f;
                    if (seamS || seamV) c = Mix(c, seam, 0.7f);
                    // The dome: brightest along the backbone and over the middle of its length, darker to the rim.
                    float dome = (0.62f + 0.38f * MathF.Sqrt(MathF.Max(0, 1 - v * v))) * (0.86f + 0.14f * MathF.Sin(along * MathF.PI));
                    c = (c.R * dome, c.G * dome, c.B * dome);
                }
                else
                {
                    c = (60, 74, 40);
                }
                skin[si * SkinWide + vi] = (uint)(Math.Min(255, (int)c.R) << 16 | Math.Min(255, (int)c.G) << 8 | Math.Min(255, (int)c.B));
            }
        return skin;
    }

    private static (float, float, float) Mix((float R, float G, float B) a, (float R, float G, float B) b, float k) =>
        (a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k);

    /// <param name="mark">If given, set true at every pixel the shell paints (KoiSchool throws the shallower fishes' shadows onto those).</param>
    public void Draw(FrameBuffer fb, bool[]? mark = null)
    {
        // Up on a perch it is out of the water: no haze at all, and it is painted wherever the
        // view is open (the cap and the log are not water, so the water stencil would cut it off).
        float murk = Light.Murk(Z) * (1 - MathF.Min(1, 6 * _out));
        if (murk > 0.985f) return;                                               // dived out of sight (Light.SeeU): nothing left to paint
        float soft = MathF.Max(1f, _pond.U * 0.0012f) * (1 + 3 * Z);
        bool[] stencil = _out >= 0.001f ? _pond.Open : _where;
        if (_out > 0.02f)
        {
            // Its shadow on the perch: the sun is up and to the left, so it falls down and to the right
            // of where the body would touch the perch, by the height (the same rule as the petals).
            float height = _out * 0.012f * _pond.U;
            PaintShadow(fb, _perchMask, Light.ShadowX * height, (Light.Lift + Light.ShadowY) * height,
                0.4f * MathF.Min(1, _out * 2), MathF.Max(1.5f, _pond.U * 0.002f));
        }
        // Legs first, so the shell covers where they join it.
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            // Front left with back right, then the other pair: the diagonal pairs share a phase.
            float front = _phase + (side > 0 ? 0 : MathF.PI), rear = _phase + (side > 0 ? MathF.PI : 0);
            Leg(fb, stencil, 0.3f, side, 1.22f - 0.7f * Stroke(front) * _stroke - 0.17f * (1 - _stroke), 0.34f, Thrust(front) * _stroke, murk);
            // The stretch (while basking) swings one back leg further back and out.
            Leg(fb, stencil, 0.74f, side, 2.36f - 0.5f * Stroke(rear) * _stroke + 0.26f * (1 - _stroke) + (side > 0 ? 0.4f * _stretch : 0), 0.3f + (side > 0 ? 0.1f * _stretch : 0), Thrust(rear) * _stroke, murk);
        }
        Paint(fb, stencil, _pond.Sun, murk, soft, _pond.Pixels, mark: mark);
        // The eyes: a dark dot each side of the head, toward the front.
        float er = MathF.Max(0.8f, _len * 0.014f * Scale);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
            Oval(fb, stencil, At(0.07f, side * 0.7f), 0, er, er, 20, 24, 16, 0.95f, murk * 0.7f, _pond.Sun, water: _pond.Pixels);
    }

    /// <summary>
    /// Where a leg is in its stroke, +1 swept forward to -1 swept back: a
    /// quick sweep back over the first 35% of the cycle (the push), then a
    /// slow return forward. Both halves are cosine curves that meet with the
    /// same slope, so the leg never jerks at the turn.
    /// </summary>
    private static float Stroke(float phase)
    {
        float p = (phase % MathF.Tau + MathF.Tau) % MathF.Tau / MathF.Tau;
        return p < 0.35f ? MathF.Cos(MathF.PI * p / 0.35f) : -MathF.Cos(MathF.PI * (p - 0.35f) / 0.65f);
    }

    /// <summary>How hard a leg is pushing right now, 0 to 1: a smooth hump over its sweep back.</summary>
    private static float Thrust(float phase)
    {
        float p = (phase % MathF.Tau + MathF.Tau) % MathF.Tau / MathF.Tau;
        return p < 0.35f ? MathF.Sin(MathF.PI * p / 0.35f) : 0;
    }

    /// <summary>
    /// One leg: a broad flat paddle from under the shell's edge at s, on one
    /// side, swung out at "angle" from straight ahead (0 = forward, pi =
    /// backward), with the webbed foot at its end.
    /// </summary>
    private void Leg(FrameBuffer fb, bool[] stencil, float s, int side, float angle, float length, float spread, float murk)
    {
        float a = _heading + side * angle;
        float dx = MathF.Cos(a), dy = MathF.Sin(a);
        float hdx = MathF.Cos(_heading), hdy = MathF.Sin(_heading);
        float k = Scale, l = _len * length * k;
        // The hip, just inside the shell's edge, in ground pixels.
        float hw = _wide * Profile(s) * 0.8f * k;
        float hx = _x - hdx * s * _len * k - hdy * side * hw, hy = _y - hdy * s * _len * k + hdx * side * hw;
        PointF thigh = ToScreen(hx + dx * l * 0.4f, hy + dy * l * 0.4f), foot = ToScreen(hx + dx * l * 0.85f, hy + dy * l * 0.85f);
        // Sunk for its depth in the water, lifted by its own height on a perch (a leg hanging over the edge is lower than the one on the rock).
        thigh.Y += Sink - Height(thigh) * LiftPx; foot.Y += Sink - Height(foot) * LiftPx;
        Oval(fb, stencil, thigh, a, l * 0.5f, l * 0.2f, 66, 84, 44, 1f, murk, _pond.Sun, rays: false, water: _pond.Pixels);
        Oval(fb, stencil, foot, a, l * 0.3f, l * (0.13f + 0.08f * spread), 56, 74, 40, 1f, murk, _pond.Sun, rays: false, water: _pond.Pixels);   // the webbed foot opens on the push, folds on the return
    }
}
