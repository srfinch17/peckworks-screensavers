using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The field mice on the bank: a small group, each with a mind of its own.
/// (The same shape as Lizards in Lizard.cs: the scene holds one of these, not
/// the animals themselves.) Unlike the lizards, mice never meet or fight, so
/// this class only has to hand each mouse the news: where the snake's head is,
/// where the lizards' noses are, and "a shadow just swept over here".
/// </summary>
internal sealed class Mice
{
    private readonly Mouse[] _all;
    // The same trick as Lizards.Noses: one array of each possible size made once,
    // so asking "who is out?" every frame makes no garbage for the collector.
    private readonly PointF[][] _noseArrays;

    public Mice(Pond pond, int count, Ripples ripples, float often, Random rng)
    {
        int n = Math.Max(0, count);
        _all = new Mouse[n];
        _noseArrays = new PointF[n + 1][];
        for (int k = 0; k <= n; k++) _noseArrays[k] = new PointF[k];
        // PERSONALITY, rolled once, the lizards' way: each dial inside its own slice of
        // 0 to 1 so two mice are never near-twins, and the two dials run opposite ways
        // (the most timid mouse is the least greedy: a greedy mouse takes risks for a seed).
        bool flip = rng.Next(2) == 0;
        for (int i = 0; i < n; i++)
        {
            float timid = Traits.Slice(rng, i, n, reverse: flip);
            float greedy = Traits.Slice(rng, i, n, reverse: !flip);
            _all[i] = new Mouse(pond, ripples, often, timid, greedy, rng);
        }
    }

    /// <summary>A shadow swept over the bank here (a swallow passing low): every mouse out and near it bolts for cover.</summary>
    public void Startle(float x, float y)
    {
        foreach (Mouse m in _all) m.Startle(x, y);
    }

    /// <param name="snakeHead">Where the snake's head is (screen pixels) while it is swimming; null when it is not out.</param>
    /// <param name="lizardNoses">The nose of every lizard that is out (screen pixels).</param>
    public void Update(float dt, PointF? snakeHead, PointF[] lizardNoses)
    {
        foreach (Mouse m in _all) m.Update(dt, snakeHead, lizardNoses);
    }

    /// <summary>The nose of each mouse that is out right now, screen pixels. Never allocates.</summary>
    public PointF[] Noses
    {
        get
        {
            int k = 0;
            foreach (Mouse m in _all)
                if (m.Visible) _noseArrays[_all.Length][k++] = m.Nose;
            PointF[] result = _noseArrays[k];
            if (k != _all.Length) Array.Copy(_noseArrays[_all.Length], result, k);
            return result;
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Mouse m in _all) m.Draw(fb);
    }

    /// <summary>For the instruments: the mice themselves.</summary>
    public IReadOnlyList<Mouse> All => _all;
}

/// <summary>
/// A field mouse on the bank: an akanezumi, the Japanese field mouse. It lives
/// in the long grass, so it is hidden most of the time. Now and then it comes
/// out at the edge of the screen, scurries from one bit of cover to the next,
/// and is gone again.
///
/// WHAT A FIELD MOUSE DOES (watch one at the edge of a lawn): it moves in
/// SCURRIES, a burst of a third of a second or so, close to the ground with the
/// thin tail streaming straight out behind, and then it STOPS dead. While it is
/// stopped it does one small thing: it SNIFFS (the nose bobs, very fast), or sits
/// UP on its haunches to look around, or GROOMS (a quick shiver), or NIBBLES at
/// a seed with its head down. It keeps to cover: the grass, the foot of a rock,
/// the side of a log, and crosses open sand only in a dash. It is prey, so
/// anything sudden (a snake's head, a lizard's nose, a bird's shadow) and it
/// bolts for the nearest grass and very often VANISHES into it for half a
/// minute, then pokes out again at the same spot. Once it has made ten or so
/// stops, it leaves the way it came.
///
/// WHAT IT LOOKS LIKE FROM ABOVE: a plump brown oval about 0.035u long with a
/// round head that is nearly part of the body, two big round ears out either
/// side of the head (pink inside), two black bead eyes, a pointed snout, four
/// tiny feet, and a thin pink-grey tail nearly as long as the body. The back is
/// warm brown, the flanks paler.
///
/// HOW IT IS DRAWN: like the lizard (Lizard.cs, which this copies shape for
/// shape): a spine of 14 points laid along the path its head took (Core/Minds
/// Trail). The first 8 points are the body, the last 6 the very thin tail, so
/// a turn bends the whole animal through the curve and the tail follows the
/// path. Still, the tail lies in a lazy curve; running, it streams. Ears, eyes
/// and feet are small ovals stuck to the spine. A faint shadow is the same body
/// painted dark a little down and to the right, so it lies ON the ground. Drawn
/// after the lawn and before the koi, only where no branch is in the way, in the
/// branches' shade, and faded in and out (0.3 s) rather than popped.
///
/// THE MIND (the koi's and lizard's idea, scaled down; see "THE MIND" in Koi.cs
/// and docs/MINDS.md). A mouse has two dials, rolled once, each 0 to 1:
///   * TIMID: flees sooner and from further, stays out for fewer stops, freezes
///     more, and hides more often when it flees.
///   * GREEDY: stays out longer, nibbles more, goes to the water to drink more.
/// A dial never forces anything; it only loads the dice. When a stop ends,
/// Decide() rolls weighted dice among: scurry to a better bit of cover, go and
/// forage on the grass, run along the log, go and drink, or stay put and freeze
/// again. Whatever the mind decides, the shared walking rules (Core/Minds
/// Ground) still ask the land mask before every step, so a mouse can never step
/// onto water.
/// </summary>
internal sealed class Mouse : Body
{
    private const int Points = 14;
    private const int BodyPoints = 8;             // spine points 0 to 7 are the body, 8 to 13 the tail
    private const float RunLengths = 3.4f;        // a scurry's speed, in body lengths a second, at pace 1
    private enum Doing
    {
        Hidden,    // not on screen at all, or tucked into the grass (see _burrow)
        Scurry,    // a burst toward the goal; WHY it runs is the Aim
        Sniff,     // still, nose twitching
        SitUp,     // still, up on its haunches, a little taller
        Groom,     // still, a quick shiver
        Nibble,    // still, head down, chewing
        Drink,     // at the water's edge, nose to the water
    }
    /// <summary>Why a scurry is happening. Changes how fast it runs and what is done when it ends.</summary>
    private enum Aim { Roam, Forage, Drink, Flee }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Cooldown _frightCool = new();   // not startled by a shadow again until this has run out
    private readonly Random _rng;
    private readonly float _u, _len, _tail, _half, _total, _often, _bodyEnd, _invBodyEnd;   // _bodyEnd: where the body stops and the tail starts, as an s
    private readonly float _timid, _greedy;           // the two personality dials, 0 to 1
    private float _x, _y, _heading, _speed, _phase, _gait, _bob, _slow, _sit, _shake, _fade;
    private float _goalX, _goalY;
    private float _pace = 1, _arrive;                 // a scurry's speed factor, and how close counts as arrived (ground pixels)
    private int _stops;                               // stops left before it leaves
    private readonly Mind<Doing> _mind = new(Doing.Hidden);   // the doing now and its timer; while Hidden the timer is the wait
    private Doing _doing => _mind.Doing;
    private Aim _aim;
    private bool _leaving, _shadowPass, _burrow;
    private bool _stranded;                           // its scurry to leave was stopped by water at once: go somewhere reachable first
    private float _drinkLen;                          // how long this drink lasts in all
    private bool _sipped;                             // the drink's one ring has been made
    private readonly Trail _trail = new(96);          // where its head has been (Core/Minds Trail): the body and tail are laid along it
    private readonly float[] _liftPx = new float[Points];   // how far each point is raised up the screen (sitting up), so the shadow can stay on the ground
    // The land, as the shared walking rules ask it (Core/Minds Ground): once in ground
    // units (for stepping) and twice in screen pixels (for lines and runs). Made once
    // each; a new delegate every frame is garbage.
    private readonly Ground.Solid _landGround, _landScreen, _clearScreen;

    // ---- Counters for the instruments (never read by the drawing) ----
    public int Flights, Hides, Emerged;
    public string LastFlightFrom = "";

    /// <summary>Where its nose is on the screen. Meaningless while hidden.</summary>
    public PointF Nose => Spine[0];
    public PointF TailTip => Spine[Points - 1];
    /// <summary>The middle of the body (for the instruments' land test).</summary>
    public PointF Middle => Spine[BodyPoints / 2];
    /// <summary>Out and drawn (or fading in or out): not away, not tucked in the grass.</summary>
    /// <summary>Out of the grass right now (hiding in it does not count, whatever the fade says).</summary>
    public bool Visible => _doing != Doing.Hidden;

    /// <summary>Anything of it to paint: out, or still fading after it hid.</summary>
    private bool Painted => _doing != Doing.Hidden || (_burrow && _fade > 0.01f);
    public float Fade => _fade;
    public bool Burrowed => _burrow;
    public float Len => _len;
    public float Timid => _timid;
    public float Greedy => _greedy;
    /// <summary>The doing and its aim, for logs and tests.</summary>
    public string State => _doing + "/" + _aim;
    /// <summary>True in the doings where standing still is the point (the instruments' standstill test skips them).</summary>
    public bool StillByDesign => _doing is Doing.Sniff or Doing.SitUp or Doing.Groom or Doing.Nibble or Doing.Drink or Doing.Hidden;
    public bool OnGrass => GrassyAt(ToScreen(_x, _y));

    public Mouse(Pond pond, Ripples ripples, float often, float timid, float greedy, Random rng) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _timid = timid;
        _greedy = greedy;
        _len = _u * 0.035f;                  // nose to rump
        _tail = _len * 0.85f;                // the tail is nearly as long as the body
        _total = _len + _tail;
        _bodyEnd = _len / _total;
        _invBodyEnd = 1 / _bodyEnd;
        _half = _len * 0.29f;                // half the widest part of the body: plump
        _landGround = OnLand;
        _landScreen = LandAt;
        _clearScreen = ClearAt;
        // s along the whole animal (Colour is asked in these), measured from the nose.
        for (int i = 0; i < Points; i++) Along[i] = DistanceAlong(i, _len) / _total;
        _mind.Begin(Doing.Hidden, often > 0 ? (4 + 12 * (float)rng.NextDouble()) / often : Mind<Doing>.Forever);
    }

    /// <summary>How far point i is behind the nose when the body is "bodyLen" long: the body's points are spread evenly over it, the tail's over the tail's length.</summary>
    private float DistanceAlong(int i, float bodyLen) =>
        i < BodyPoints ? bodyLen * i / (BodyPoints - 1f) : bodyLen + _tail * (i - (BodyPoints - 1)) / (Points - BodyPoints + 1f);

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

    private bool LandAt(float sx, float sy)
    {
        int ix = Math.Clamp((int)sx, 0, _pond.Width - 1), iy = Math.Clamp((int)sy, 0, _pond.Height - 1);
        return _pond.Land[iy * _pond.Width + ix];
    }

    private bool LandAt(PointF s) => LandAt(s.X, s.Y);

    /// <summary>Is this screen point on the lawn (the grass a mouse feels safe in)? Off screen: the nearest edge pixel decides.</summary>
    private bool GrassAt(float sx, float sy)
    {
        int ix = Math.Clamp((int)sx, 0, _pond.Width - 1), iy = Math.Clamp((int)sy, 0, _pond.Height - 1);
        return _pond.Grassy[iy * _pond.Width + ix];
    }

    private bool GrassyAt(PointF s) => GrassAt(s.X, s.Y);

    /// <summary>Land, and (on screen) out from under the branches: where a run is worth making.</summary>
    private bool ClearAt(float sx, float sy)
    {
        if (!LandAt(sx, sy)) return false;
        int ix = (int)sx, iy = (int)sy;
        return ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height || _pond.Open[iy * _pond.Width + ix];
    }

    /// <summary>
    /// A shadow swept over the bank here (screen pixels): a swallow passing
    /// low. A mouse out within about 0.15u of it bolts for cover, and will not
    /// be startled again for six seconds (a bird circling would otherwise keep
    /// it bolting every frame). A timid mouse reacts from further off.
    /// </summary>
    public void Startle(float x, float y)
    {
        if (_doing == Doing.Hidden || _leaving || !_frightCool.Ready) return;
        if (Dist(Spine[0], new PointF(x, y)) > _u * 0.15f * (0.8f + 0.4f * _timid)) return;
        _frightCool.Start(6);
        Flee(new PointF(x, y), "shadow");
    }

    public void Update(float dt, PointF? snakeHead, PointF[] lizardNoses)
    {
        _mind.Tick(dt);
        _frightCool.Tick(dt);
        // The fade: in 0.3 s toward seen, out 0.3 s toward gone. Away from the screen altogether it is simply gone.
        float fadeTo = _doing == Doing.Hidden ? 0 : 1;
        if (_doing == Doing.Hidden && !_burrow) _fade = 0;
        else if (_fade != fadeTo) _fade = Math.Clamp(_fade + MathF.Sign(fadeTo - _fade) * dt / 0.3f, 0, 1);   // sign of the gap, never a step when there is none (a step on "equal" blinked a 6% ghost of the mouse every other frame for the whole hide)
        if (_doing == Doing.Hidden)
        {
            if (!_mind.Over) return;
            if (_burrow)
            {
                // Out of the grass at the very spot it went in.
                _burrow = false;
                Emerged++;
                _mind.Begin(Doing.Sniff, 0.5f + (float)_rng.NextDouble());
            }
            else if (Appear()) GoIn();
            return;
        }
        Think(snakeHead, lizardNoses);
        if (_doing == Doing.Hidden) return;       // Think just tucked it into the grass

        // ---- Steering toward the goal, turning on a circle about as wide as its body ----
        float gdx = _goalX - _x, gdy = _goalY - _y, gd = MathF.Sqrt(gdx * gdx + gdy * gdy);
        bool moving = _doing == Doing.Scurry;
        float turnErr = 0;                                                  // how far round it still has to turn to face the goal (radians)
        if (moving)
        {
            float want = gd > 1 ? MathF.Atan2(gdy, gdx) : _heading;
            turnErr = Wrap(want - _heading);
            float most = MathF.Min(7f, MathF.Max(3f, _speed / (_len * 0.5f)));   // quick on its feet, but a pivot on the spot would still fold the tail
            _heading = Wrap(_heading + Math.Clamp(Wrap(want - _heading), -most * dt, most * dt));
            if (gd < _arrive) _mind.End();                                  // arrived: the scurry ends here
        }
        float targetSpeed = moving ? _len * RunLengths * _pace : 0;
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (moving ? 18f : 16f)));   // off like a shot, stops dead
        float step = _speed * dt;
        if (step > 0.01f)
        {
            // The step, by the shared walking rule (Core/Minds Ground.Step): forward if that is
            // land; if water is ahead and the goal is off to one side, along the shore toward
            // it. Water every way: the scurry ends here, and if this was the scurry to leave it
            // is STRANDED (on a stone out in the water) and the next must go somewhere it can
            // walk to. (The lizard learned this the hard way; the mouse inherits it.)
            Ground.Stepped took = Ground.Step(_landGround, ref _x, ref _y, ref _heading, step, moving ? turnErr : 0);
            if (took != Ground.Stepped.Refused) _stranded = false;
            else if (moving) { _mind.End(); _stranded = _leaving; }
            _trail.RememberIfMoved(new PointF(_x, _y), _len / 30);
        }
        // Off the screen while leaving: gone until next time. Its whole length, tail included, has to be out of view.
        PointF here = ToScreen(_x, _y);
        if (_leaving && (here.X < -_total || here.Y > _pond.Height + _total || here.X > _pond.Width + _total))
        {
            _mind.Begin(Doing.Hidden, (30 + 60 * (float)_rng.NextDouble()) / MathF.Max(0.01f, _often));   // a mouse in the grass is MOSTLY in the grass: out about a third of the time (12 to 42 s between visits, chosen against a visibility count the hide blink had inflated, had it out half of every minute)
            _burrow = false;
            return;
        }

        // ---- The legs' rhythm, the sit, the shiver: each eases to its target and back ----
        float run = MathF.Min(1, _speed / (_len * 2.5f));
        _gait = (_gait + MathF.Tau * 9f * run * dt) % MathF.Tau;           // about nine steps a second flat out
        _bob = (_bob + 22f * dt) % MathF.Tau;                              // the sniff and the chew
        _phase = (_phase + 46f * dt) % MathF.Tau;                          // the groom's shiver
        _slow = (_slow + 0.9f * dt) % MathF.Tau;                           // the tail's lazy curve changing its mind
        _sit += ((_doing == Doing.SitUp ? 1f : 0f) - _sit) * (1 - MathF.Exp(-dt * 7f));
        _shake += ((_doing == Doing.Groom ? 1f : 0f) - _shake) * (1 - MathF.Exp(-dt * 14f));
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
            // Start just off the edge, so it runs IN rather than popping up. The whole animal
            // lies behind the nose, so a nose a little way off the screen means all of it is.
            PointF s = new(bottom ? x : (ix == 0 ? -_len * 0.3f : _pond.Width + _len * 0.3f), bottom ? _pond.Height + _len * 0.3f : y);
            (_x, _y) = ToGround(s.X, s.Y);
            _heading = bottom ? -MathF.PI / 2 : (ix == 0 ? 0 : MathF.PI);
            _speed = 0; _sit = 0; _shake = 0; _burrow = false;
            _trail.Straight(new PointF(_x, _y), _heading, _total * 1.2f, 40);   // a straight animal behind it, so the spine means something before it has moved
            _leaving = false;
            _aim = Aim.Roam;
            _stops = 8 + _rng.Next(9) + (int)(_greedy * 4) - (int)(_timid * 3);   // 8 to 16, more for a greedy mouse, fewer for a timid one
            LaySpine();
            return true;
        }
        return false;
    }

    /// <summary>Just come on screen: run in to the first bit of cover, or stand and sniff if there is none.</summary>
    private void GoIn()
    {
        PointF here = ToScreen(_x, _y);
        if (PickSpot(here, false, 0.7f, 2f) is PointF spot) Go(Aim.Roam, spot, 1f);
        else _mind.Begin(Doing.Sniff, 0.5f);
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: a mouse has one thing on its mind at a time (a Doing), a
    // feeling for how long to keep at it (a timer), and when that runs out it
    // rolls weighted dice for what to do next. Timid and greedy are only weights
    // on the dice faces. Below the mind, the steering is told "run to THIS point,
    // this keenly" and does nothing else.

    /// <summary>Keeps the current doing going, ends it when it is done, and reacts to the snake and the lizards.</summary>
    private void Think(PointF? snakeHead, PointF[] lizardNoses)
    {
        // Anything close: no thinking, just bolt for cover. (Not while already bolting or
        // leaving: it is already getting away, and re-picking the goal every frame is the
        // jitter the lizard's first version had.)
        if (!_leaving && !(_doing == Doing.Scurry && _aim == Aim.Flee))
        {
            float snakeReach = _u * 0.2f * (0.8f + 0.5f * _timid), lizardReach = _u * 0.08f * (0.8f + 0.6f * _timid);
            if (snakeHead is PointF sh && Dist(Spine[0], sh) < snakeReach) { Flee(sh, "snake"); return; }
            foreach (PointF ln in lizardNoses)
                if (Dist(Spine[0], ln) < lizardReach) { Flee(ln, "lizard"); return; }
        }
        switch (_doing)
        {
            case Doing.Sniff:
            case Doing.SitUp:
            case Doing.Groom:
            case Doing.Nibble:
                if (_mind.Over) Decide();
                break;
            case Doing.Drink:
                // One tiny ring at the water in front of the nose, half a second in: the lip touching the surface.
                if (!_sipped && _mind.Left < _drinkLen - 0.5f) Sip();
                if (_mind.Over) Decide();
                break;
            case Doing.Scurry:
                if (_mind.Over) EndScurry();
                break;
        }
    }

    /// <summary>Starts a scurry toward a screen point: the aim, and how fast it runs. It may take at most half as long again as the trip needs.</summary>
    private void Go(Aim aim, PointF screenGoal, float pace)
    {
        (_goalX, _goalY) = ToGround(screenGoal.X, screenGoal.Y);
        _aim = aim;
        _pace = pace;
        _arrive = _len * (aim == Aim.Drink ? 0.1f : 0.15f);               // a drinker stops close to its spot: the spot is only a body width from the water
        _mind.Begin(Doing.Scurry, Dist(ToScreen(_x, _y), screenGoal) / (_len * RunLengths * pace) * 1.5f + 0.3f);
    }

    /// <summary>Runs from a threat to cover. A flight is twice as fast as a scurry.</summary>
    private void Flee(PointF from, string why)
    {
        Flights++;
        LastFlightFrom = why;
        Go(Aim.Flee, CoverGoal(from), 2.2f);
    }

    /// <summary>A scurry has ended (arrived, ran out of time, or met water). What comes next depends on why it ran.</summary>
    private void EndScurry()
    {
        Aim was = _aim;
        _aim = Aim.Roam;
        float gdx = _goalX - _x, gdy = _goalY - _y, gd = MathF.Sqrt(gdx * gdx + gdy * gdy);
        PointF here = ToScreen(_x, _y);
        switch (was)
        {
            case Aim.Drink when gd < _len * 0.8f:
                // At the water's edge, nose to the water.
                _drinkLen = 1 + 1.5f * (float)_rng.NextDouble();
                _mind.Begin(Doing.Drink, _drinkLen);
                _sipped = false;
                return;
            case Aim.Forage when gd < _len * 1.2f:
                _mind.Begin(Doing.Nibble, (1 + 2 * (float)_rng.NextDouble()) * (0.7f + 0.6f * _greedy));
                return;
            case Aim.Flee:
                // Safe in the grass? Most of the time (a timid one nearly always) it simply vanishes
                // into it, for 10 to 40 seconds, and comes out at the same spot. Not in grass, or not
                // this time: a freeze and a look round (the timid freeze longer).
                if (!_leaving && GrassyAt(here) && _rng.NextDouble() < 0.7 + 0.25 * _timid)
                {
                    _burrow = true;
                    Hides++;
                    _speed = 0;
                    _mind.Begin(Doing.Hidden, 10 + 30 * (float)_rng.NextDouble());
                    return;
                }
                _mind.Begin(Doing.Sniff, (_leaving ? 0.2f : 0.5f + 0.5f * (float)_rng.NextDouble()) * (1 + _timid));
                return;
        }
        PickStop(here);
    }

    /// <summary>The scurry is over: one small still thing, chosen by the dice. Nibbling needs grass to nibble.</summary>
    private void PickStop(PointF here)
    {
        bool grass = GrassyAt(here);
        float wSniff = 1f + 0.8f * _timid;                         // the timid freeze and sniff more
        float wSit = 0.45f * (1 - 0.6f * _timid);                  // the timid do not stand up where they can be seen
        float wGroom = 0.35f;
        float wNibble = grass ? 0.15f + 0.9f * _greedy : 0;
        int k = Dice.Pick(_rng, [wSniff, wSit, wGroom, wNibble]);
        switch (k)
        {
            case 1: _mind.Begin(Doing.SitUp, 0.7f + 0.9f * (float)_rng.NextDouble()); break;
            case 2: _mind.Begin(Doing.Groom, 0.5f + 0.7f * (float)_rng.NextDouble()); break;
            case 3: _mind.Begin(Doing.Nibble, (1 + 2 * (float)_rng.NextDouble()) * (0.7f + 0.6f * _greedy)); break;
            default: _mind.Begin(Doing.Sniff, (0.3f + 1.7f * (float)_rng.NextDouble()) * (1 + 0.5f * _timid)); break;
        }
    }

    /// <summary>The lip touches the water: one tiny ring on the first water pixel in front of the nose (or, failing that, toward the middle of the pond).</summary>
    private void Sip()
    {
        _sipped = true;
        PointF n = Spine[0];
        float hx = MathF.Cos(_heading), hy = MathF.Sin(_heading) * Ripples.Squash;
        float cx = _pond.Centre.X - n.X, cy = _pond.Centre.Y - n.Y;
        if (!RingAlong(n, hx, hy)) RingAlong(n, cx, cy);
    }

    /// <summary>Looks along a screen direction from the nose for the first water pixel within 1.2 body lengths and makes the ring there. False if there is none.</summary>
    private bool RingAlong(PointF n, float dx, float dy)
    {
        float l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        dx /= l; dy /= l;
        for (float d = 1; d <= _len * 1.2f; d += 1)
        {
            float x = n.X + dx * d, y = n.Y + dy * d;
            int ix = (int)x, iy = (int)y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) return false;
            if (_pond.Water[iy * _pond.Width + ix]) { _ripples.Add(x, y, 0.12f); return true; }
        }
        return false;
    }

    /// <summary>Rolls the next doing, weighted by personality and by what there is to do.</summary>
    private void Decide()
    {
        PointF here = ToScreen(_x, _y);
        if (_leaving || --_stops <= 0)
        {
            // Time to go: toward the nearest screen edge that has land on it, off the screen.
            // Unless the last scurry to leave was stopped by water at the first step: then any
            // spot it CAN walk to first, and the edge again after that.
            _leaving = true;
            if (_stranded && PickSpot(here, false, 0.7f, 2f) is PointF back) Go(Aim.Roam, back, 1.3f);
            else Go(Aim.Roam, ExitGoal(here), 1.5f);
            return;
        }
        bool wasDrinking = _doing == Doing.Drink;
        bool canDrink = TryDrinkSpot(here, out PointF drinkSpot);
        PointF? forage = PickSpot(here, true, 0.8f, 2.4f);
        PointF? onLog = LogSpot(here);
        float wRoam = 1f;
        float wForage = forage != null ? 0.25f + 0.9f * _greedy : 0;
        float wLog = onLog != null ? 0.12f + 0.2f * _greedy : 0;
        float wDrink = canDrink ? (wasDrinking ? 0.05f : 0.1f + 0.45f * _greedy) : 0;
        float wStay = 0.25f + 0.6f * _timid;                       // freeze again where it is: the timid do it most
        switch (Dice.Pick(_rng, [wRoam, wForage, wLog, wDrink, wStay]))
        {
            case 1: Go(Aim.Forage, forage!.Value, 1f); return;
            case 2: Go(Aim.Forage, onLog!.Value, 1f); return;
            case 3: Go(Aim.Drink, drinkSpot, 1f); return;
            case 4: PickStop(here); return;
        }
        if (PickSpot(here, false, 0.7f, 2f) is PointF goal) Go(Aim.Roam, goal, 1f);
        else PickStop(here);
    }

    /// <summary>
    /// A spot to scurry to: tries a dozen or so candidates a short way off (a scurry is
    /// 0.2 to 0.6 seconds, a body length or two), keeps those the whole line to which is
    /// land, and takes the best SCORED. Grass scores best, the foot of a dry rock or the
    /// side of the log is good, bare sand or stone is poor (taken only when nothing
    /// better is in reach). With grassOnly, a spot that is not on the lawn is no use.
    /// </summary>
    /// <param name="near">Nearest candidate, in body lengths.</param>
    /// <param name="far">Furthest candidate, in body lengths.</param>
    private PointF? PickSpot(PointF here, bool grassOnly, float near, float far)
    {
        PointF? best = null;
        float bestScore = float.MinValue;
        for (int tries = 0; tries < 14; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _len * (near + (far - near) * (float)_rng.NextDouble());
            var s = new PointF(here.X + MathF.Cos(a) * d, here.Y + MathF.Sin(a) * d * Ripples.Squash);
            int ix = (int)s.X, iy = (int)s.Y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
            int i = iy * _pond.Width + ix;
            if (!_pond.Land[i] || !_pond.Open[i]) continue;
            bool grass = _pond.Grassy[i];
            if (grassOnly && !grass) continue;
            if (!Ground.LineClear(_landScreen, here, s, 3)) continue;     // the whole way there stays on land
            float score = (grass ? 3f : 0f) + (NearCover(s) ? 2f : 0f) + 0.5f * (float)_rng.NextDouble();   // the last bit breaks ties
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    /// <summary>Is this point within 0.03u of a dry rock's rim or of the log's side: a place a mouse likes to be?</summary>
    private bool NearCover(PointF s)
    {
        float reach = _u * 0.03f;
        IReadOnlyList<BankRock> rocks = _pond.Rocks;
        for (int k = 0; k < rocks.Count; k++)
        {
            BankRock r = rocks[k];
            if (!r.InWater && Dist(r.Centre, s) < r.Radius + reach) return true;
        }
        Log? log = _pond.Log;
        if (log is Log lg && DistToLog(lg, s) < lg.Width * 0.5f + reach) return true;
        return false;
    }

    /// <summary>How far a screen point is from the log's middle line (a segment from end to end), pixels.</summary>
    private static float DistToLog(Log lg, PointF p)
    {
        PointF a = lg.At(0), b = lg.At(1);
        float dx = b.X - a.X, dy = b.Y - a.Y, l2 = MathF.Max(1e-3f, dx * dx + dy * dy);
        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Dist(p, new PointF(a.X + dx * t, a.Y + dy * t));
    }

    /// <summary>A spot on the log's top, close enough for one scurry and reachable over land: to run along it and nibble.</summary>
    private PointF? LogSpot(PointF here)
    {
        Log? log = _pond.Log;
        if (log is not Log lg) return null;
        PointF s = lg.At(0.1f + 0.8f * (float)_rng.NextDouble());
        if (!OnScreen(s) || Dist(s, here) > _len * 4f) return null;
        int i = (int)s.Y * _pond.Width + (int)s.X;
        if (!_pond.Land[i] || !_pond.Open[i] || !Ground.LineClear(_landScreen, here, s, 3)) return null;
        return s;
    }

    /// <summary>
    /// Where to run to get away from a point. First choice: the nearest lawn spot
    /// that is further from the threat than the mouse is now, over a clear line
    /// (the grass is where it can vanish). Failing that, the longest clear run
    /// straight away (Core/Minds Ground.FleeRun). Failing that, the way out of
    /// the screen, and it leaves.
    /// </summary>
    private PointF CoverGoal(PointF from)
    {
        PointF here = ToScreen(_x, _y);
        float now = Dist(here, from);
        PointF? best = null;
        float bestD = float.MaxValue;
        for (int tries = 0; tries < 24; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _len * (1f + 5f * (float)_rng.NextDouble());
            var s = new PointF(here.X + MathF.Cos(a) * d, here.Y + MathF.Sin(a) * d * Ripples.Squash);
            int ix = (int)s.X, iy = (int)s.Y;
            if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
            int i = iy * _pond.Width + ix;
            if (!_pond.Grassy[i] || !_pond.Open[i] || Dist(s, from) < now + _len * 0.5f) continue;
            if (d < bestD && Ground.LineClear(_landScreen, here, s, 3)) { bestD = d; best = s; }
        }
        if (best is PointF cover) return cover;
        PointF? run = Ground.FleeRun(_clearScreen, Spine[0], from, _len * 0.4f, _len * 6f, _len * 1.5f);
        if (run is PointF r) return r;
        _leaving = true;
        _stops = 0;
        return ExitGoal(here);
    }

    /// <summary>
    /// The way out: a point just past the screen's edge, reached over land. The land
    /// is a corner, so only part of the bottom edge and of one side edge is land: look
    /// along both for land pixels, keep the nearest one whose straight line from here
    /// is all land, and aim a body and a half beyond it. If no line is clear, hop to
    /// a land spot that IS in a clear line and nearer the nearest land edge. (Copied
    /// from the lizard, which learned each of these by standing on a spur for two
    /// minutes.) The mouse aims its whole length plus some past the edge, because
    /// its tail drags along behind the nose.
    /// </summary>
    private PointF ExitGoal(PointF here)
    {
        bool landLeft = _pond.Land[_pond.Height - 1];     // which side the land's corner is (Appear uses the same test)
        int w = _pond.Width, h = _pond.Height;
        float over = _total * 1.2f;
        PointF bestEdge = default, bestAny = default;
        float bestD = float.MaxValue, bestAnyD = float.MaxValue;
        for (int k = 0; k <= 48; k++)
        {
            // k 0..24 walks the bottom edge, k 25..48 the side edge (its lower half, where the land is).
            PointF p = k <= 24 ? new PointF((w - 1) * k / 24f, h - 1) : new PointF(landLeft ? 0 : w - 1, h * (0.5f + 0.5f * (k - 25) / 23f) - 1);
            if (!LandAt(p)) continue;
            float d = Dist(here, p);
            if (d < bestAnyD) { bestAnyD = d; bestAny = p; }
            if (d < bestD && Ground.LineClear(_landScreen, here, p, 4)) { bestD = d; bestEdge = p; }
        }
        if (bestD == float.MaxValue && bestAnyD < float.MaxValue)
        {
            PointF hop = default;
            float hopD = bestAnyD - _len * 0.3f;
            for (int tries = 0; tries < 30; tries++)
            {
                float ang = (float)(_rng.NextDouble() * MathF.Tau), dd = _len * (0.5f + 2.5f * (float)_rng.NextDouble());
                var c = new PointF(here.X + MathF.Cos(ang) * dd, here.Y + MathF.Sin(ang) * dd);
                if (!OnScreen(c) || !LandAt(c) || !Ground.LineClear(_landScreen, here, c, 4)) continue;
                float cd = Dist(c, bestAny);
                if (cd < hopD) { hopD = cd; hop = c; }
            }
            if (hopD < bestAnyD - _len * 0.3f) return hop;
        }
        PointF e = bestD < float.MaxValue ? bestEdge : bestAnyD < float.MaxValue ? bestAny : new PointF(here.X, h - 1);
        float jitter = (float)(_rng.NextDouble() - 0.5) * _len;
        if (e.Y >= h - 2) return new PointF(e.X + jitter, h + over);
        return new PointF(landLeft ? -over : w + over, e.Y + jitter);
    }

    /// <summary>
    /// A spot to drink at. Walk from where it stands toward the middle of the pond one small step
    /// at a time, over land, until the next step would be water; stop a little short of that.
    /// False if it is off screen, the water is more than a fifth of the screen away, or the spot
    /// is under a branch.
    /// </summary>
    private bool TryDrinkSpot(PointF here, out PointF spot)
    {
        spot = default;
        if (!OnScreen(here) || !LandAt(here)) return false;
        float dx = _pond.Centre.X - here.X, dy = _pond.Centre.Y - here.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1) return false;
        dx /= l; dy /= l;
        for (float d = 2; d < _u * 0.2f; d += 2)
        {
            var p = new PointF(here.X + dx * d, here.Y + dy * d);
            if (!OnScreen(p)) return false;
            if (LandAt(p)) continue;
            float stop = d - 2 - _len * 0.15f;
            if (stop < _len * 0.8f) return false;           // already there: it would not even have to turn
            spot = new PointF(here.X + dx * stop, here.Y + dy * stop);
            return _pond.Open[(int)spot.Y * _pond.Width + (int)spot.X];
        }
        return false;
    }

    // ==================== THE BODY ====================

    /// <summary>
    /// Lays the spine along the trail (Core/Minds Trail: the body follows the path the head took).
    /// On top of that path: the SNIFF pulls the nose back and forth a hair, the
    /// SIT shortens the body by 15% and lifts the head up the screen, the GROOM
    /// shivers the body sideways, and the TAIL lies in a slow lazy curve when the
    /// animal is still and streams straight when it runs.
    /// </summary>
    private void LaySpine()
    {
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        float bodyLen = _len * (1 - 0.15f * _sit);
        Trail.Walk walk = _trail.Begin(new PointF(_x, _y), 0, 0, _heading);
        for (int i = 0; i < Points; i++) mid[i] = walk.Advance(DistanceAlong(i, bodyLen)).P;
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        float run = MathF.Min(1, _speed / (_len * 2.5f));
        float lift = _u * 0.008f * _sit;   // at 0.004u (3 px at 720 lines) the sit-up read as a twitch
        for (int i = 0; i < Points; i++)
        {
            float off = 0, hw;
            if (i < BodyPoints)
            {
                float sb = i / (BodyPoints - 1f);
                hw = _half * Profile(sb) * (1 + 0.1f * _sit);
                off = _shake * _half * 0.3f * MathF.Sin(_phase + sb * 3);     // the groom's shiver, a quiver down the body
            }
            else
            {
                float tj = (i - (BodyPoints - 1)) / (Points - BodyPoints + 1f);   // 0 at the rump to 1 at the tip
                hw = MathF.Max(0.5f, _half * 0.12f * (1 - 0.6f * tj));
                // Still: a lazy curve to one side that slowly changes its mind. Running: nearly straight, a faint flick.
                off = _len * (0.35f * MathF.Sin(_slow) * (1 - run) * tj * tj + 0.04f * run * MathF.Sin(_gait * 2 - tj * 5) * tj);
            }
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * off, cy = mid[i].Y + ny * off;
            PointF l = ToScreen(cx + nx * hw, cy + ny * hw), r = ToScreen(cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
            // Sitting up lifts the head most and the shoulders a little (the rump stays on the ground).
            float w = i == 0 ? 1f : i == 1 ? 0.8f : i == 2 ? 0.45f : i == 3 ? 0.15f : 0f;
            _liftPx[i] = lift * w;
            Spine[i] = new PointF(Spine[i].X, Spine[i].Y - _liftPx[i]);
        }
        // The nose twitch: a hair of pull back toward the body, fast. Pulled BACK only, so the nose never leaves the land the trail has walked.
        float twitch = _doing == Doing.Sniff ? 0.045f : _doing == Doing.Nibble ? 0.03f : 0f;
        if (twitch > 0)
        {
            float pull = _len * twitch * (0.5f + 0.5f * MathF.Sin(_bob));
            PointF a = Spine[0], b = Spine[1];
            float bx = b.X - a.X, by = b.Y - a.Y, bl = MathF.Max(1e-3f, MathF.Sqrt(bx * bx + by * by));
            Spine[0] = new PointF(a.X + bx / bl * pull, a.Y + by / bl * pull);
            Spine[1] = new PointF(b.X + bx / bl * pull * 0.4f, b.Y + by / bl * pull * 0.4f);
        }
    }

    /// <summary>
    /// The body's width along it, 0 at the nose to 1 at the rump: a pointed snout,
    /// a round head, a slight neck, the plump belly widest at 0.55, a rounded rump.
    /// Eight values for the eight spine points; the painter blends between them.
    /// </summary>
    private static float Profile(float sb) => sb switch
    {
        < 0.1f => 0.3f,
        < 0.2f => 0.62f,
        < 0.35f => 0.6f,
        < 0.5f => 0.86f,
        < 0.62f => 1f,
        < 0.8f => 0.95f,
        < 0.95f => 0.8f,
        _ => 0.5f,
    };

    // The body's colours, worked out ONCE (64 steps along the body by 16 across it, packed 0xRRGGBB):
    // asking this per pixel with the sums done live cost more than the rest of drawing the mouse.
    private static readonly int[] BodyPalette = BuildPalette();

    private static int[] BuildPalette()
    {
        var pal = new int[64 * 16];
        for (int si = 0; si < 64; si++)
            for (int vi = 0; vi < 16; vi++)
            {
                float sb = si / 63f, av = (vi + 0.5f) / 16f;
                // Warm brown back with a faint darker line down the middle, paler flanks, a pink nose.
                float flank = Math.Clamp((av - 0.45f) * 2.5f, 0, 1);
                float line = sb > 0.25f ? Math.Clamp((0.18f - av) * 5, 0, 1) * 0.5f : 0;
                float nose = Math.Clamp((0.1f - sb) * 12, 0, 1);
                int r = (int)(140 + (182 - 140) * flank - 28 * line + (200 - 140) * nose);
                int g = (int)(98 + (146 - 98) * flank - 22 * line + (128 - 98) * nose);
                int b = (int)(60 + (104 - 60) * flank - 14 * line + (122 - 60) * nose);
                pal[si * 16 + vi] = (r << 16) | (g << 8) | b;
            }
        return pal;
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        if (_shadowPass) { r = 10; g = 16; b = 10; alpha = 0.4f * _fade; return true; }
        // Fur grain: a cheap whole-number hash of the (s, v) cell, stuck to the body so it rides
        // along when it bends: 0.93 to 1.07 in 256ths.
        int cell = ((int)(s * 360f) * 73856093) ^ ((int)((v + 1f) * 14f) * 19349663);
        int k = 238 + ((((cell >> 7) & 255) * 36) >> 8);
        alpha = _fade;
        if (s > _bodyEnd)
        {
            // The tail: bare, pink-grey.
            r = 176 * k >> 8; g = 140 * k >> 8; b = 128 * k >> 8;
            return true;
        }
        int c = BodyPalette[(int)(s * _invBodyEnd * 63f) * 16 + (int)(MathF.Abs(v) * 15.99f)];
        r = ((c >> 16) & 255) * k >> 8; g = ((c >> 8) & 255) * k >> 8; b = (c & 255) * k >> 8;
        return true;
    }
    public void Draw(FrameBuffer fb)
    {
        if (!Painted || _fade < 0.01f || !OnScreenBox()) return;
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        // The shadow: the same body, dark and faint, a little down and to the right of where it
        // lies ON THE GROUND. A raised head (sitting up) is drawn higher, so its shadow is moved
        // back DOWN by the same lift: the gap between the head and its shadow is what reads as height.
        float sx = _u * 0.0016f, sy = _u * 0.002f;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X + sx, Spine[i].Y + sy + _liftPx[i]);
        _shadowPass = true;
        Paint(fb, _pond.Open, null, 0, soft * 1.3f);
        _shadowPass = false;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X - sx, Spine[i].Y - sy - _liftPx[i]);
        // Feet first (the body covers their roots): front pair at the shoulders, hind pair at the hips, opposite corners in step.
        Feet(fb, 2, 0);
        Feet(fb, 5, MathF.PI);
        Paint(fb, _pond.Open, _pond.Sun, 0, soft);
        // The ears: round, a brown rim with a smaller pink middle, either side of the head.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float ux = ex / el, uy = ey / el;                                  // from the nose back toward the body
        float er = _len * 0.1f;
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            float ax = e0.X + ex * 0.84f - uy * HalfWidth[1] * 1.0f * side, ay = e0.Y + ey * 0.84f + ux * HalfWidth[1] * 1.0f * side;
            Oval(fb, _pond.Open, new PointF(ax, ay), 0, er, er * Ripples.Squash, 120, 84, 52, _fade, 0, _pond.Sun, rays: false);
            Oval(fb, _pond.Open, new PointF(ax, ay), 0, er * 0.62f, er * 0.62f * Ripples.Squash, 214, 150, 148, _fade, 0, _pond.Sun, rays: false);
        }
        // Two black bead eyes, on the head just in front of the ears.
        float br = MathF.Max(0.7f, _len * 0.04f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ux * HalfWidth[1] * 0.55f - uy * HalfWidth[1] * 0.5f * side, e0.Y + uy * HalfWidth[1] * 0.55f + ux * HalfWidth[1] * 0.5f * side);
            Oval(fb, _pond.Open, c, 0, br, br, 12, 10, 10, 0.95f * _fade, 0, _pond.Sun, rays: false);
        }
    }

    /// <summary>Is the nose near enough the screen for anything of it to show? (The whole animal is under two body lengths.)</summary>
    private bool OnScreenBox() => Spine[0].X > -_total * 1.2f && Spine[0].Y > -_total * 1.2f && Spine[0].X < _pond.Width + _total * 1.2f && Spine[0].Y < _pond.Height + _total * 1.2f;

    /// <summary>A pair of tiny feet at spine point i: each a short oval out from the body, swinging fore and aft with the gait (the two sides half a step apart).</summary>
    private void Feet(FrameBuffer fb, int i, float phaseOffset)
    {
        PointF p = Spine[i], q = Spine[Math.Max(0, i - 1)];
        float back = MathF.Atan2(p.Y - q.Y, p.X - q.X);                   // toward the tail
        float run = MathF.Min(1, _speed / (_len * 2.5f));
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            // Standing: out sideways and a little back. Running: each swings forward and back about that, quick.
            float swing = 0.9f * run * MathF.Sin(_gait + phaseOffset + (side > 0 ? 0 : MathF.PI));
            float a = back - side * (MathF.PI / 2 + 0.3f) + swing;
            float l = _len * 0.13f;
            var c = new PointF(p.X + MathF.Cos(a) * (l * 0.5f + HalfWidth[i] * 0.7f), p.Y + MathF.Sin(a) * (l * 0.5f + HalfWidth[i] * 0.7f) * Ripples.Squash);
            Oval(fb, _pond.Open, c, a, l * 0.5f, _len * 0.025f, 168, 126, 112, _fade, 0, _pond.Sun, rays: false);
        }
    }
}
