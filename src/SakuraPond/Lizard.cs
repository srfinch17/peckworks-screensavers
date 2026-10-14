using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The lizards on the bank: a small group, each with a mind of its own, and
/// the one thing that needs to know about all of them at once, the MEETING.
/// (The same shape as Turtles in Turtle.cs: the scene holds one of these, not
/// the animals themselves.)
///
/// THE MEETING, in plain words. When two lizards are both out and their noses
/// come within about two body lengths of each other, both stop and turn to
/// face each other, like two cats on a fence. After a moment the BOLDER one
/// charges at the other; the other bolts away, and then leaves the screen on
/// its next dash or two. A meeting happens at most once per visit, so it
/// never turns into a loop of chasing. This class only spots the moment and
/// tells the pair; each lizard's own mind does the facing, charging and fleeing.
/// </summary>
internal sealed class Lizards
{
    private readonly Lizard[] _all;
    private readonly Random _rng;
    // The snake wants the noses of whichever lizards are out. Building a new array
    // every frame would be garbage for the collector, so one array of each possible
    // size (0, 1, 2 ...) is made once and the right one is filled in and handed back.
    private readonly PointF[][] _noseArrays;

    public Lizards(Pond pond, int count, Ripples ripples, float often, Random rng)
    {
        _rng = rng;
        int n = Math.Max(0, count);
        _all = new Lizard[n];
        _noseArrays = new PointF[n + 1][];
        for (int k = 0; k <= n; k++) _noseArrays[k] = new PointF[k];
        // PERSONALITY, rolled once. Dice alone could give two near-twins, and then the
        // owner sees no difference. So each dial is rolled inside its own slice of 0 to 1
        // (the first lizard in the low slice, the next in the next ...), and the energy
        // slices run the other way: the boldest lizard is the laziest, the shyest the
        // quickest. Which lizard gets which slice is a coin toss.
        bool flip = rng.Next(2) == 0;
        for (int i = 0; i < n; i++)
        {
            float bold = Traits.Slice(rng, i, n, reverse: flip);
            float energy = Traits.Slice(rng, i, n, reverse: !flip);
            _all[i] = new Lizard(pond, ripples, often, bold, energy, rng);
        }
        foreach (Lizard l in _all) l.Peers = _all;
    }

    /// <summary>Moves every lizard on, then looks for a pair that has just come together.</summary>
    /// <param name="snakeHead">Where the snake's head is (screen pixels) while it is swimming; null when it is not out.</param>
    public void Update(float dt, PointF? snakeHead)
    {
        foreach (Lizard l in _all) l.Update(dt, snakeHead);
        for (int i = 0; i < _all.Length; i++)
            for (int j = i + 1; j < _all.Length; j++)
            {
                Lizard a = _all[i], b = _all[j];
                if (!a.CanMeet || !b.CanMeet || Lizard.Gap(a, b) > a.Len * 2f) continue;
                float hold = 1.6f + 0.6f * (float)_rng.NextDouble();   // the same stand-off time for both
                bool aWins = a.Bold >= b.Bold;                          // the bolder one wins
                a.BeginFace(b, aWins, hold);
                b.BeginFace(a, !aWins, hold);
            }
    }

    /// <summary>The nose of each lizard that is out right now, screen pixels (the snake keeps clear of them). Never allocates.</summary>
    public PointF[] Noses
    {
        get
        {
            int k = 0;
            foreach (Lizard l in _all)
                if (l.Visible) _noseArrays[_all.Length][k++] = l.Nose;
            PointF[] result = _noseArrays[k];
            if (k != _all.Length) Array.Copy(_noseArrays[_all.Length], result, k);
            return result;
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Lizard l in _all) l.Draw(fb);
    }
}

/// <summary>
/// A small lizard on the bank: a kanahebi, the Japanese grass lizard, the
/// brown lizard with the very long tail that lives in every sunny Japanese
/// garden. It is hidden most of the time; now and then it comes out, runs
/// about the grass and the rocks, basks on a stone, takes a drink at the
/// water's edge, and is gone.
///
/// WHAT A GRASS LIZARD DOES (watch one on a warm wall): it moves in DASHES,
/// a quick sprint of half a second or so and then dead still, head up,
/// for a second or three, then another dash. Running, its body and tail
/// throw an S-wave side to side, the legs on opposite corners stepping
/// together; still, it is a straight stick with its tail in a lazy curve.
/// It climbs onto a rock and lies flat in the sun for a while. It runs to the
/// water's edge and stands with its nose just over it to drink. Meet another
/// of its kind and it squares up; the shyer one runs. Anything startling (a
/// snake) and it is under the grass in a blink.
///
/// HOW IT IS DRAWN: along a bending spine like the koi (Body.cs), the head
/// following its path and the body and long tail laid along the trail
/// behind it, so every turn bends it through the curve; a running wave is
/// added when it moves and dies away when it stops. The four legs are small
/// ovals swinging from the body, as the turtle's are. A faint shadow is the
/// same body painted dark a little down and to the right, so it sits ON the
/// ground rather than floating over it. Drawn after the lawn (it runs over
/// the grass and the rocks) and before the irises (it can pass under their
/// leaves), only where no branch is in the way, in the branches' shade.
///
/// THE MIND (the koi's idea, scaled down; see "THE MIND" in Koi.cs). A lizard
/// has two dials, rolled once when it is made, each 0 to 1:
///   * BOLD: stays out longer (more dashes), lies on its rock longer, goes
///     looking for the other lizard, and WINS a meeting (the bolder one charges).
///   * ENERGY: longer dashes, a little faster, shorter spells standing still.
/// Like a person's temper, a dial never forces anything; it only loads the
/// dice. When a stand-still ends, Decide() rolls weighted dice among: roam to
/// a nearby spot, climb a rock, go and drink, go and find the other lizard.
/// Three things can cut in: a meeting (Lizards, above), a charge or a flight
/// that comes out of it, and the snake coming too close (run for the nearest
/// edge). Whatever the mind decides, the steering below still asks the land
/// mask before every step, so a lizard can never step onto water.
/// </summary>
internal sealed class Lizard : Body
{
    private const int Points = 16;
    private enum Doing
    {
        Hidden,    // not on screen at all
        Dash,      // a quick run toward the goal; WHY it runs is the Aim
        Freeze,    // dead still, head up
        Bask,      // lying flat on a rock
        Drink,     // standing at the water's edge, nose to the water
        Face,      // squaring up to the other lizard in a meeting
    }
    /// <summary>Why a dash is happening. Changes how fast it runs and what is done when it ends.</summary>
    private enum Aim { Roam, Drink, Charge, Flee }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _len, _wide, _often;
    private readonly float _bold, _energy;        // the two personality dials, 0 to 1
    private float _x, _y, _heading, _speed, _amp, _phase;
    private float _goalX, _goalY, _gait;
    private float _pace = 1, _arrive;             // a dash's speed factor, and how close counts as arrived (ground pixels)
    private int _dashes;                          // stand-stills left before it leaves
    private readonly Mind<Doing> _mind = new(Doing.Hidden);   // the doing now and its timer (Core/Minds); while Hidden the timer is the wait for the next visit
    private Doing _doing => _mind.Doing;
    private Aim _aim;
    private bool _leaving, _shadowPass;
    private bool _stranded;                       // its dash to leave was stopped by water at once: walk somewhere reachable first
    private bool _met;                            // has it had its meeting this visit?
    private bool _approach, _isWinner;            // in a meeting: still walking up to the other / the bolder one
    private Lizard? _other;                       // the lizard in a meeting with it
    private float _drinkLen;                      // how long this drink lasts in all
    private bool _sipped;                         // the drink's one ring has been made
    private readonly PointF[] _trail = new PointF[64];
    private int _trailHead, _trailCount;

    /// <summary>Where its nose is on the screen (for the instruments). Meaningless while hidden.</summary>
    public PointF Nose => Spine[0];
    public PointF TailTip => Spine[Points - 1];
    public bool Visible => _doing != Doing.Hidden;
    /// <summary>Body length, screen pixels (the meeting distance is measured in these).</summary>
    public float Len => _len;
    public float Bold => _bold;
    public float Energy => _energy;
    /// <summary>The doing and its aim, for logs and tests (never read by the drawing).</summary>
    public string State => _doing + "/" + _aim;
    /// <summary>The other lizards, so one can look for company. Set by Lizards once all are made.</summary>
    public Lizard[] Peers { get; set; } = [];

    /// <summary>Free to be drawn into a meeting: out, on screen, not leaving, not already met, not squaring up.</summary>
    public bool CanMeet => Visible && !_leaving && !_met && _doing != Doing.Face && OnScreen(Spine[0]);

    public static float Gap(Lizard a, Lizard b) => Dist(a.Spine[0], b.Spine[0]);

    public Lizard(Pond pond, Ripples ripples, float often, float bold, float energy, Random rng) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _bold = bold;
        _energy = energy;
        _len = _u * 0.09f;                   // nose to tail tip; the tail is well over half of it
        _wide = _len * 0.05f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _mind.Begin(Doing.Hidden, often > 0 ? (15 + 30 * (float)rng.NextDouble()) / often : Mind<Doing>.Forever);
    }

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private bool OnScreen(PointF s) => s.X >= 0 && s.Y >= 0 && s.X < _pond.Width && s.Y < _pond.Height;

    /// <summary>Is this ground point on the land? A point just off the screen counts as whatever the nearest edge pixel is (the land runs on out of view).</summary>
    private bool OnLand(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Land[iy * _pond.Width + ix];
    }

    private bool LandAt(PointF s)
    {
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Land[iy * _pond.Width + ix];
    }

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <param name="snakeHead">The snake's head in screen pixels while it is swimming, else null.</param>
    public void Update(float dt, PointF? snakeHead = null)
    {
        _mind.Tick(dt);
        if (_doing == Doing.Hidden)
        {
            if (_mind.Over && Appear()) _mind.Begin(Doing.Freeze, 0.5f);
            return;
        }
        Think(snakeHead);

        // ---- Steering toward the goal, turning on a circle no tighter than about half its body ----
        // A charging lizard aims at where the other IS now, not where it was.
        if (_doing == Doing.Dash && _aim == Aim.Charge && _other != null) (_goalX, _goalY) = ToGround(_other.Spine[0].X, _other.Spine[0].Y);
        float gdx = _goalX - _x, gdy = _goalY - _y, gd = MathF.Sqrt(gdx * gdx + gdy * gdy);
        bool moving = _doing == Doing.Dash || (_doing == Doing.Face && _approach);
        float turnErr = 0;                                                  // how far round it still has to turn to face the goal (radians)
        if (moving)
        {
            float want = gd > 1 ? MathF.Atan2(gdy, gdx) : _heading;
            turnErr = Wrap(want - _heading);
            float most = MathF.Min(3.5f, MathF.Max(1.5f, _speed / (_len * 0.4f)));   // a pivot faster than this folded the tail into a hairpin
            _heading = Wrap(_heading + Math.Clamp(Wrap(want - _heading), -most * dt, most * dt));
            if (_doing == Doing.Dash && gd < _arrive && !(_aim == Aim.Charge && _mind.Left > 0.6f)) _mind.End();   // arrived: the dash ends here (a charge runs at least 0.3 s, however near the other starts)
        }
        float targetSpeed = moving ? _len * 2.6f * _pace : 0;
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (moving ? 12f : 10f)));   // off like a shot, stops dead
        float step = _speed * dt;
        if (step > 0.01f)
        {
            float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
            if (OnLand(nx, ny)) { _x = nx; _y = ny; _stranded = false; }
            else if (moving)
            {
                // Water ahead. If the goal is mostly behind or off to one side (it ran up to the shore
                // facing the water and now has to go back), do not give up: step off along the shore, the
                // first of 35, 70, 105, 140 and 170 degrees round toward the goal that is land (a lizard
                // nose-to-water on a straight shore needs nearly a half turn), and carry on that way. (The first version ended the dash here, so it only ever turned one frame at a
                // time: a lizard faced the water, stood for a minute and never moved.) If the goal IS
                // ahead, across the water or at the shore, a dash ends here and a walk up to the other stops.
                bool slid = false;
                if (MathF.Abs(turnErr) > 0.5f)
                    for (int k = 1; k <= 5 && !slid; k++)
                    {
                        float a = _heading + (turnErr > 0 ? 0.6f : -0.6f) * k;
                        float sx = _x + MathF.Cos(a) * step, sy = _y + MathF.Sin(a) * step;
                        if (OnLand(sx, sy)) { _x = sx; _y = sy; _heading = Wrap(a); slid = true; }
                    }
                if (!slid)
                {
                    // Water every way it tried: the dash ends here. If this was the dash to leave,
                    // it is STRANDED (on a stone out in the water, say, reached by a drink spot on
                    // the stone's dry top) and the next dash must go somewhere it can walk to.
                    if (_doing == Doing.Dash) { _mind.End(); _stranded = _leaving; }
                    else _approach = false;
                }
            }
            PointF last = _trail[_trailHead];
            if ((last.X - _x) * (last.X - _x) + (last.Y - _y) * (last.Y - _y) > (_len / 30) * (_len / 30)) Remember(new PointF(_x, _y));
        }
        // Off the screen while leaving: gone until next time.
        PointF here = ToScreen(_x, _y);
        if (_leaving && (here.X < -_len || here.Y > _pond.Height + _len || here.X > _pond.Width + _len))
        {
            _mind.Begin(Doing.Hidden, (60 + 120 * (float)_rng.NextDouble()) / MathF.Max(0.01f, _often));
            _other = null;
            return;
        }

        // ---- The running wave and the legs' rhythm: fast when running, dying to nothing when still ----
        float run = MathF.Min(1, _speed / (_len * 2f));
        _gait = (_gait + MathF.Tau * 5.5f * run * dt) % MathF.Tau;        // about five and a half steps a second flat out
        _phase = _gait;
        _amp += (_wide * 1.3f * run - _amp) * (1 - MathF.Exp(-dt * 8));
        LaySpine();
    }

    /// <summary>Comes out at the screen's edge on the land (the bottom or the side), facing in. False if no spot was found.</summary>
    private bool Appear()
    {
        for (int tries = 0; tries < 80; tries++)
        {
            bool bottom = _rng.Next(2) == 0;
            float x = bottom ? (float)_rng.NextDouble() * _pond.Width : (_pond.Land[_pond.Height - 1] ? 0 : _pond.Width - 1);
            float y = bottom ? _pond.Height - 1 : _pond.Height * (0.6f + 0.38f * (float)_rng.NextDouble());
            int ix = Math.Clamp((int)x, 0, _pond.Width - 1), iy = Math.Clamp((int)y, 0, _pond.Height - 1);
            if (!_pond.Land[iy * _pond.Width + ix] || !_pond.Open[iy * _pond.Width + ix]) continue;
            // Start just off the edge, so it runs IN rather than popping up.
            PointF s = new(bottom ? x : (ix == 0 ? -_len * 0.8f : _pond.Width + _len * 0.8f), bottom ? _pond.Height + _len * 0.8f : y);
            (_x, _y) = ToGround(s.X, s.Y);
            _heading = bottom ? -MathF.PI / 2 : (ix == 0 ? 0 : MathF.PI);
            _speed = 0; _amp = 0; _trailCount = 0; _trailHead = 0;
            for (int i = 20; i >= 0; i--) Remember(new PointF(_x - MathF.Cos(_heading) * _len * i / 18f, _y - MathF.Sin(_heading) * _len * i / 18f));
            _leaving = false;
            _met = false; _other = null; _aim = Aim.Roam;
            _dashes = 5 + _rng.Next(8) + (int)(_bold * 4);                 // a bold lizard stays out longer
            LaySpine();
            return true;
        }
        return false;
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: a lizard has one thing on its mind at a time (a Doing),
    // a feeling for how long to keep at it (a timer), and when that runs out it
    // rolls weighted dice for what to do next. Bold and energy are only
    // weights on the dice faces. Below the mind, the steering is told "run to
    // THIS point, this keenly" and does nothing else.

    /// <summary>How the energy dial stretches a dash: a lazy lizard sprints 0.7 times as long, a keen one 1.5 times.</summary>
    private float DashScale => 0.7f + 0.8f * _energy;
    /// <summary>How the energy dial shrinks a stand-still: a lazy lizard stands 1.4 times as long, a keen one 0.6.</summary>
    private float StillScale => 1.4f - 0.8f * _energy;

    /// <summary>Keeps the current doing going, ends it when it is done, and reacts to the snake.</summary>
    private void Think(PointF? snakeHead)
    {
        // The snake is close: no thinking, just run for the nearest edge and hide. (Even
        // mid-meeting: the other lizard sees the meeting is off, in ThinkFace.)
        if (snakeHead is PointF sh && !_leaving && Dist(Spine[0], sh) < _u * 0.2f)
        {
            _other = null;
            _leaving = true;
            _dashes = 0;
            Go(Aim.Flee, ExitGoal(ToScreen(_x, _y)), 4f);
            return;
        }
        switch (_doing)
        {
            case Doing.Freeze:
            case Doing.Bask:
                if (_mind.Over) Decide();
                break;
            case Doing.Drink:
                // One tiny ring at the water in front of the nose, half a second in: the lip touching the surface.
                if (!_sipped && _mind.Left < _drinkLen - 0.5f) Sip();
                if (_mind.Over) Decide();
                break;
            case Doing.Face:
                ThinkFace();
                break;
            case Doing.Dash:
                if (_mind.Over) EndDash();
                break;
        }
    }

    /// <summary>Squaring up: walk toward the other's nose until about 0.7 body lengths apart, hold still facing it, then (when the stand-off ends) charge or flee.</summary>
    private void ThinkFace()
    {
        if (_mind.Over) { EndFace(); return; }            // checked first: both lizards' stand-offs end in the same frame, whoever is updated first
        if (_other == null || _other._other != this || !_other.Visible)
        {
            // The meeting was called off (the other ran from the snake): carry on.
            _other = null;
            _mind.Begin(Doing.Freeze, 0.5f);
            return;
        }
        PointF on = _other.Spine[0];
        (_goalX, _goalY) = ToGround(on.X, on.Y);
        if (Dist(Spine[0], on) < _len * 0.7f) _approach = false;
    }

    /// <summary>The meeting is over. The bold one runs AT the other; the shy one runs AWAY, far, and will leave in a dash or two.</summary>
    private void EndFace()
    {
        Lizard o = _other!;
        if (_isWinner)
        {
            Go(Aim.Charge, o.Spine[0], 0.9f);
            _arrive = _len * 0.3f;
        }
        else
        {
            Go(Aim.Flee, FleeGoal(o.Spine[0]), 2.5f);
            _dashes = Math.Min(_dashes, 2);
            _other = null;
        }
    }

    /// <summary>Called by Lizards when two have come together: both stop what they are doing and face each other for <paramref name="hold"/> seconds.</summary>
    public void BeginFace(Lizard other, bool winner, float hold)
    {
        _mind.Begin(Doing.Face, hold);
        _aim = Aim.Roam;
        _other = other;
        _isWinner = winner;
        _met = true;
        _approach = true;
        _pace = 0.5f;                      // a wary walk, not a run
    }

    /// <summary>Starts a dash toward a screen point: the aim, how long it may last at most, and how fast it runs.</summary>
    private void Go(Aim aim, PointF screenGoal, float maxTime)
    {
        (_goalX, _goalY) = ToGround(screenGoal.X, screenGoal.Y);
        _mind.Begin(Doing.Dash, maxTime);
        _aim = aim;
        _arrive = _len * (aim == Aim.Drink ? 0.1f : 0.15f);               // a drinker stops close to its spot: the spot is only a body width from the water
        // A flight is the fastest, a charge nearly so; the energy dial adds a little to everything.
        _pace = (0.9f + 0.25f * _energy) * (aim == Aim.Flee ? 1.35f : aim == Aim.Charge ? 1.25f : 1f);
    }

    private void Stand(float seconds) => _mind.Begin(Doing.Freeze, seconds);

    /// <summary>A dash has ended (arrived, ran out of time, or met water). What comes next depends on why it ran.</summary>
    private void EndDash()
    {
        Aim was = _aim;
        _aim = Aim.Roam;
        float gdx = _goalX - _x, gdy = _goalY - _y, gd = MathF.Sqrt(gdx * gdx + gdy * gdy);
        switch (was)
        {
            case Aim.Drink when gd < _len * 0.8f:
                // At the water's edge, nose to the water.
                _drinkLen = 1 + 2 * (float)_rng.NextDouble();
                _mind.Begin(Doing.Drink, _drinkLen);
                _sipped = false;
                return;
            case Aim.Charge:
                _other = null;
                Stand(0.6f + 0.6f * (float)_rng.NextDouble());
                return;
            case Aim.Flee:
                Stand(_leaving ? 0.25f : 0.5f + 0.5f * (float)_rng.NextDouble());
                return;
        }
        // The dash is over: stand still, or lie down if this was the run up onto a rock.
        bool onRock = _pond.Rocks.Any(r => !r.InWater && Dist(r.Centre, ToScreen(_x, _y)) < r.Radius * 0.6f);
        if (onRock && _rng.NextDouble() < 0.7) _mind.Begin(Doing.Bask, (4 + 8 * (float)_rng.NextDouble()) * (0.6f + 0.9f * _bold));   // the bold bask longest
        else Stand((0.6f + 2.4f * (float)_rng.NextDouble()) * StillScale);
    }

    /// <summary>The lip touches the water: one tiny ring on the first water pixel in front of the nose (or, failing that, toward the middle of the pond, where the drink spot was found).</summary>
    private void Sip()
    {
        _sipped = true;
        PointF n = Spine[0];
        float hx = MathF.Cos(_heading), hy = MathF.Sin(_heading) * Ripples.Squash;
        float cx = _pond.Centre.X - n.X, cy = _pond.Centre.Y - n.Y;
        if (!RingAlong(n, hx, hy)) RingAlong(n, cx, cy);
    }

    /// <summary>Looks along a screen direction from the nose for the first water pixel within 0.4 body lengths and makes the ring there. False if there is none.</summary>
    private bool RingAlong(PointF n, float dx, float dy)
    {
        float l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        dx /= l; dy /= l;
        for (float d = 1; d <= _len * 0.4f; d += 1)
        {
            float x = n.X + dx * d, y = n.Y + dy * d;
            int ix = (int)x, iy = (int)y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) return false;
            if (_pond.Water[iy * _pond.Width + ix]) { _ripples.Add(x, y, 0.15f); return true; }
        }
        return false;
    }

    /// <summary>Rolls the next doing, weighted by personality and by what there is to do.</summary>
    private void Decide()
    {
        _dashes--;
        PointF here = ToScreen(_x, _y);
        if (_dashes <= 0)
        {
            // Time to go: toward the nearest screen edge that has land on it, off the screen.
            // Unless the last dash to leave was stopped by water at the first step: then any
            // spot it CAN walk to first (back along the stones toward the shore), and the edge
            // again after that. (A lizard on a wet rock dashed at the far edge, was refused,
            // stood, dashed again, 70 times in 140 seconds, going nowhere.)
            _leaving = true;
            if (_stranded && RoamGoal(here, null) is PointF back) Go(Aim.Roam, back, (0.3f + 0.7f * (float)_rng.NextDouble()) * DashScale);
            else Go(Aim.Roam, ExitGoal(here), (0.3f + 0.7f * (float)_rng.NextDouble()) * DashScale + 3);
            return;
        }
        bool wasDrinking = _doing == Doing.Drink;
        PointF drinkSpot = default;
        bool canDrink = TryDrinkSpot(here, out drinkSpot);
        Lizard? company = FindCompany();
        // The dice faces. The dials load them: the energetic roam more, the bold favour a sunny rock and go
        // looking for company; the drink is the same for everyone (all lizards get thirsty) but not twice running.
        float wRoam = 0.7f + 0.8f * _energy;
        float wRock = _doing != Doing.Bask ? 0.3f + 0.5f * _bold : 0;
        float wDrink = canDrink ? (wasDrinking ? 0.1f : 0.35f) : 0;
        float wSeek = company != null ? 0.1f + 0.6f * _bold : 0;
        int face = Dice.Pick(_rng, [wDrink, wRock, wSeek, wRoam]);
        if (face == 0)
        {
            float dist = Dist(here, drinkSpot);
            Go(Aim.Drink, drinkSpot, dist / (_len * 2.6f * 0.6f) + 1f);       // a long dash, timed for the whole way
            return;
        }
        if (face == 1)
        {
            // A rock to climb: a dry one within reach, not the one it is on. None in reach: it looks for company, or roams.
            var near = _pond.Rocks.Where(r => !r.InWater && r.Radius > _u * 0.02f && Dist(r.Centre, here) < _u * 0.35f && Dist(r.Centre, here) > r.Radius).ToList();
            if (near.Count > 0) { Go(Aim.Roam, near[_rng.Next(near.Count)].Centre, (0.3f + 0.7f * (float)_rng.NextDouble()) * DashScale); return; }
            face = 2;
        }
        PointF? goal = RoamGoal(here, face == 2 && company != null ? (PointF?)company.Spine[0] : null);
        if (goal is not PointF gp) { Stand(1); return; }
        Go(Aim.Roam, gp, (0.3f + 0.7f * (float)_rng.NextDouble()) * DashScale);
    }

    /// <summary>Somewhere on the land a little way off, in view, on a line that stays on land. With <paramref name="toward"/>, the candidate nearest to that point.</summary>
    private PointF? RoamGoal(PointF here, PointF? toward)
    {
        PointF? best = null;
        float bestGap = float.MaxValue;
        for (int tries = 0; tries < 40; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _len * (0.6f + 2f * (float)_rng.NextDouble());
            var s = new PointF(here.X + MathF.Cos(a) * d, here.Y + MathF.Sin(a) * d * Ripples.Squash);
            int ix = (int)s.X, iy = (int)s.Y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
            int i = iy * _pond.Width + ix;
            if (!_pond.Land[i] || !_pond.Open[i]) continue;
            if (!LandAt(new PointF((here.X + s.X) / 2, (here.Y + s.Y) / 2))) continue;   // the way there stays on land (here may be off the screen's edge)
            if (toward is not PointF t) return s;
            float gap = Dist(s, t);
            if (gap < bestGap) { bestGap = gap; best = s; }
        }
        return best;
    }

    /// <summary>Another lizard that is out, free to meet, and on screen: one worth going to look for.</summary>
    private Lizard? FindCompany()
    {
        if (_met) return null;
        foreach (Lizard o in Peers)
            if (o != this && o.CanMeet) return o;
        return null;
    }

    /// <summary>Is the straight line from a to b all land? Checked every few pixels (the way a lizard can actually run).</summary>
    private bool LandLine(PointF a, PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        int steps = (int)(l / 4) + 1;
        for (int i = 1; i <= steps; i++)
            if (!LandAt(new PointF(a.X + dx * i / steps, a.Y + dy * i / steps))) return false;
        return true;
    }

    /// <summary>
    /// The way out: a point just past the screen's edge, reached over land. The land is a corner,
    /// so only part of the bottom edge and of one side edge is land; "the nearest edge" can be water
    /// (the first version ran at it, was stopped at the shore, and stood there for ever). So: look
    /// along the bottom edge and the land's side edge for land pixels, keep the nearest one whose
    /// straight line from here is all land, and aim a body and a half beyond it. If no line is
    /// clear (it is out on a narrow spur of land), hop to a land spot that IS in a clear line and
    /// nearer the nearest land edge, and look again from there.
    /// </summary>
    private PointF ExitGoal(PointF here)
    {
        bool landLeft = _pond.Land[_pond.Height - 1];     // which side the land's corner is (Appear uses the same test)
        int w = _pond.Width, h = _pond.Height;
        PointF bestEdge = default, bestAny = default;
        float bestD = float.MaxValue, bestAnyD = float.MaxValue;
        for (int k = 0; k <= 48; k++)
        {
            // k 0..24 walks the bottom edge, k 25..48 the side edge (its lower half, where the land is).
            PointF p = k <= 24 ? new PointF((w - 1) * k / 24f, h - 1) : new PointF(landLeft ? 0 : w - 1, h * (0.5f + 0.5f * (k - 25) / 23f) - 1);
            if (!LandAt(p)) continue;
            float d = Dist(here, p);
            if (d < bestAnyD) { bestAnyD = d; bestAny = p; }
            if (d < bestD && LandLine(here, p)) { bestD = d; bestEdge = p; }
        }
        if (bestD == float.MaxValue && bestAnyD < float.MaxValue)
        {
            // No edge is in a clear line from here: the first version ran at the edge anyway, was
            // stopped by the water at the spur's side, and a lizard stood on a spur for two minutes.
            PointF hop = default;
            float hopD = bestAnyD - _len * 0.3f;
            for (int tries = 0; tries < 30; tries++)
            {
                float ang = (float)(_rng.NextDouble() * MathF.Tau), dd = _len * (0.5f + 2.5f * (float)_rng.NextDouble());
                var c = new PointF(here.X + MathF.Cos(ang) * dd, here.Y + MathF.Sin(ang) * dd);
                if (!OnScreen(c) || !LandAt(c) || !LandLine(here, c)) continue;
                float cd = Dist(c, bestAny);
                if (cd < hopD) { hopD = cd; hop = c; }
            }
            if (hopD < bestAnyD - _len * 0.3f) return hop;
        }
        PointF e = bestD < float.MaxValue ? bestEdge : bestAnyD < float.MaxValue ? bestAny : new PointF(here.X, h - 1);
        float jitter = (float)(_rng.NextDouble() - 0.5) * _len;
        if (e.Y >= h - 2) return new PointF(e.X + jitter, h + _len * 1.5f);
        return new PointF(landLeft ? -_len * 1.5f : w + _len * 1.5f, e.Y + jitter);
    }

    /// <summary>
    /// Where to run to get away from a point: straight away from it if there is land that way, else
    /// the clearest heading within about 90 degrees of straight away. March along each heading in
    /// small steps while the ground is land and out from under the branches; take the longest run
    /// (a slightly shorter one on the straight line wins, so it does not swerve for nothing).
    /// If nothing is clear for at least a body and a half, take the way out of the screen.
    /// </summary>
    private PointF FleeGoal(PointF from)
    {
        PointF me = Spine[0];
        float ax = me.X - from.X, ay = me.Y - from.Y, al = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
        float baseA = MathF.Atan2(ay / al, ax / al), bestD = 0, bestA = baseA;
        for (int k = 0; k < 7; k++)
        {
            float off = k == 0 ? 0 : ((k + 1) / 2) * 0.5f * (k % 2 == 1 ? 1 : -1);
            float a = baseA + off, cos = MathF.Cos(a), sin = MathF.Sin(a), d = 0;
            for (float t = _len * 0.4f; t <= _len * 6f; t += _len * 0.4f)
            {
                var p = new PointF(me.X + cos * t, me.Y + sin * t);
                if (!LandAt(p) || (OnScreen(p) && !_pond.Open[(int)p.Y * _pond.Width + (int)p.X])) break;
                d = t;
            }
            if (d > bestD + _len * 0.5f) { bestD = d; bestA = a; }
        }
        if (bestD < _len * 1.5f) return ExitGoal(ToScreen(_x, _y));
        return new PointF(me.X + MathF.Cos(bestA) * bestD, me.Y + MathF.Sin(bestA) * bestD);
    }

    /// <summary>
    /// A spot to drink at. Walk from where it stands toward the middle of the pond one small step
    /// at a time, over land, until the next step would be water; stop one body width short of that.
    /// False if it is off screen, the water is too far, or the spot is under a branch.
    /// </summary>
    private bool TryDrinkSpot(PointF here, out PointF spot)
    {
        spot = default;
        if (!OnScreen(here) || !LandAt(here)) return false;
        float dx = _pond.Centre.X - here.X, dy = _pond.Centre.Y - here.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1) return false;
        dx /= l; dy /= l;
        for (float d = 2; d < _u * 0.5f; d += 2)
        {
            var p = new PointF(here.X + dx * d, here.Y + dy * d);
            if (!OnScreen(p)) return false;
            if (LandAt(p)) continue;
            float stop = d - 2 - _wide * 3;                 // the last land step, then a body width and a half back (it stops a few pixels short or long of its spot)
            if (stop < _len * 0.8f) return false;           // already there: it would not even have to turn
            spot = new PointF(here.X + dx * stop, here.Y + dy * stop);
            return _pond.Open[(int)spot.Y * _pond.Width + (int)spot.X];
        }
        return false;
    }

    /// <summary>Lays the spine along the trail with the running wave (the koi's method, with the wave strongest down the tail).</summary>
    private void LaySpine()
    {
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(_x, _y), b = _trail[slot];
        float travelled = 0, segLen = Dist(a, b);
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (travelled + segLen < want && left > 0)
            {
                travelled += segLen;
                a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length;
                left--;
                b = _trail[slot];
                segLen = Dist(a, b);
            }
            if (left <= 0 && travelled + segLen < want)
            {
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(_heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(_heading);
                mid[i] = new PointF(b.X + ex * (want - travelled - segLen), b.Y + ey * (want - travelled - segLen));
            }
            else
            {
                float f = segLen > 1e-4f ? (want - travelled) / segLen : 0;
                mid[i] = new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
            }
        }
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            // The running S-wave: small at the head, growing down the body and tail, two waves along the whole length.
            float sway = _amp * (0.1f + 0.9f * s) * MathF.Sin(MathF.Tau * 1.6f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s);
            PointF l = ToScreen(cx + nx * hw, cy + ny * hw), r = ToScreen(cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
    }

    /// <summary>A pointed head, a slim neck, the body widest behind the front legs, hips, then the long tail tapering to a hair.</summary>
    private static float Profile(float s)
    {
        if (s < 0.08f) return 0.35f + 0.5f * MathF.Sqrt(s / 0.08f);
        if (s < 0.13f) return 0.85f - 0.15f * (s - 0.08f) / 0.05f;
        if (s < 0.4f) { float k = (s - 0.13f) / 0.27f; return 0.7f + 0.3f * MathF.Sin(k * MathF.PI); }
        return MathF.Max(0.06f, 0.7f * (1 - (s - 0.4f) / 0.6f) + 0.06f);
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        if (_shadowPass) { r = 10; g = 16; b = 10; alpha = 0.3f; return true; }
        float av = MathF.Abs(v);
        // Olive-brown back with a darker line down the spine, a pale stripe along each side of the body
        // (the grass lizard's mark), the tail a touch greyer, and a fine speckle so it is not one flat colour.
        float speck = 0.92f + 0.16f * MathF.Abs(MathF.Sin(s * 173f + v * 41f) * MathF.Sin(s * 97f - v * 59f));
        float back = Math.Clamp((0.2f - av) * 8, 0, 1);
        float stripe = s < 0.45f ? Math.Clamp(1 - MathF.Abs(av - 0.62f) * 9, 0, 1) : 0;
        float tail = Math.Clamp((s - 0.4f) * 3, 0, 1);
        r = (int)((112 + (72 - 112) * back + (190 - 112) * stripe + (104 - 112) * tail) * speck);
        g = (int)((92 + (58 - 92) * back + (172 - 92) * stripe + (92 - 92) * tail) * speck);
        b = (int)((54 + (36 - 54) * back + (120 - 54) * stripe + (62 - 54) * tail) * speck);
        alpha = 1;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        if (_doing == Doing.Hidden) return;
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        // The shadow: the same body, dark and faint, a little down and to the right of where it lies.
        float sx = _u * 0.0025f, sy = _u * 0.003f;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X + sx, Spine[i].Y + sy);
        _shadowPass = true;
        Paint(fb, _pond.Open, null, 0, soft * 1.5f);
        _shadowPass = false;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X - sx, Spine[i].Y - sy);
        // Legs first (the body covers their roots): front pair at the shoulders, hind pair at the hips, opposite corners in step.
        Legs(fb, 2, 0.17f, 0.06f, 0);
        Legs(fb, 6, 0.2f, 0.065f, MathF.PI);
        Paint(fb, _pond.Open, _pond.Sun, 0, soft);
        // Two tiny dark eyes.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.6f, _len * 0.012f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ex / el * HalfWidth[1] * 0.3f - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ey / el * HalfWidth[1] * 0.3f + ex / el * HalfWidth[1] * 0.7f * side);
            Oval(fb, _pond.Open, c, 0, er, er, 16, 14, 12, 0.95f, 0, _pond.Sun, rays: false);
        }
    }

    /// <summary>A pair of legs at spine point i: each a thin oval out from the body, swinging fore and aft with the gait (the two sides half a step apart).</summary>
    private void Legs(FrameBuffer fb, int i, float len, float wid, float phaseOffset)
    {
        PointF p = Spine[i], q = Spine[Math.Max(0, i - 1)];
        float back = MathF.Atan2(p.Y - q.Y, p.X - q.X);                   // toward the tail
        float run = MathF.Min(1, _speed / (_len * 2f));
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            // Standing: legs out sideways and a little back. Running: each swings forward and back about that.
            float swing = 0.7f * run * MathF.Sin(_gait + phaseOffset + (side > 0 ? 0 : MathF.PI));
            float a = back - side * (MathF.PI / 2 + 0.35f) + swing;
            float l = _len * len;
            var c = new PointF(p.X + MathF.Cos(a) * (l * 0.5f + HalfWidth[i] * 0.6f), p.Y + MathF.Sin(a) * (l * 0.5f + HalfWidth[i] * 0.6f) * Ripples.Squash);
            Oval(fb, _pond.Open, c, a, l * 0.5f, _len * wid * 0.5f, 98, 80, 48, 1f, 0, _pond.Sun, rays: false);
        }
    }
}
