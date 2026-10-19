using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

/// <summary>
/// The crayfish: the scene holds this small wrapper (the same shape as Lizards
/// in Lizard.cs); the animal itself is CrayfishBody below. If the knob is 0 the
/// wrapper holds nothing and every call returns at once.
///
/// WORLD LAW, in the pond's own terms: the crayfish is SOLID TO NOTHING. It
/// lives on the bed, under everything that swims (a koi is kept a margin of
/// water above the bed by its own rules, so a fish passes OVER it), and under
/// the lily pads and the petals. It reacts instead: a fish or a turtle over it
/// sends it flipping backward. It does walk round the things that stand on the
/// bed (rocks, the island, the log, the cattails): the pixels that are not open
/// water are walls to it.
/// </summary>
internal sealed class Crayfish
{
    private readonly CrayfishBody? _animal;

    public Crayfish(Pond pond, Ripples ripples, float often, Random rng)
    {
        _animal = often > 0 ? new CrayfishBody(pond, ripples, often, rng) : null;
    }

    /// <summary>Is it out from under its rock (visible or on its way out)?</summary>
    public bool Out => _animal != null && _animal.Out;

    /// <summary>What kind of hiding place it found ("rock", "log" or "none"), for the instruments.</summary>
    public string Home => _animal?.HomeKind ?? "none";

    /// <summary>Its middle, screen pixels, while it is out (the tadpoles flee it); null under its stone.</summary>
    public PointF? Spot => _animal != null && _animal.Out ? _animal.Spot : null;

    /// <param name="threats">Screen points of what sends it flipping backward: koi heads and middles, turtle noses and shells, the ducks' heads.</param>
    public void Update(float dt, PointF[] threats) => _animal?.Update(dt, threats);

    /// <summary>On the bed: before every fish (everything that swims is above it).</summary>
    public void Draw(FrameBuffer fb) => _animal?.Draw(fb);
}

/// <summary>
/// A red swamp crayfish (amerika-zarigani), the crayfish of every Japanese
/// pond and ditch. It lives under a rock at the edge of the shallows and now
/// and then comes out to feed.
///
/// WHAT A CRAYFISH DOES, in the order it does it (watch one in a ditch):
///   * EMERGES from under its rock, claws first, over a couple of seconds.
///   * WALKS slowly on eight thin legs (the legs ripple down each side, front
///     to back), the two big claws held out ahead and a touch open, the long
///     feelers (antennae) sweeping back and always waving a little. It stops
///     very often.
///   * FEEDS: stands still and reaches one claw at a time to its mouth, slowly,
///     over and over.
///   * THREATENS: both claws up and spread wide open, held a second or two.
///   * BACKS UP a little, legs running the other way.
///   * ESCAPES with the TAIL FLIP: the abdomen snaps under the body and the
///     whole animal shoots BACKWARD, claws left ahead, two or three flips with
///     a pause between, then sits still with the claws up before it walks home.
///   * Goes back under its rock tail first (the emerging run in reverse).
/// It turns by pivoting on its legs. It never bends sideways; only the
/// abdomen curls, and only UNDER (seen from above that is a shortening).
///
/// THE FOUR CREATURE GATES (see "Creatures and anything alive" in CLAUDE.md):
///   1. VISIBLE here? Only over a lit, shallow bed, which is why it lives by a
///      bank rock: its home is picked where the floor is 0.02u to 0.12u down
///      (the bed shows), and a step to where the water is too deep (the haze
///      would pass 0.65) is refused.
///   2. In this view's PERSPECTIVE? It is drawn sunk down the screen by its
///      depth (Light.SinkPx), hazed toward the water (Light.Murk), shrunk
///      (Light.Scale) and squashed by the view (Ripples.Squash), like a koi.
///   3. Turns by BENDING or by a pivot? It pivots on its legs; the body is one
///      stiff shell on a straight spine, and the abdomen only curls under.
///   4. MOVES like the animal? A slow walk with long stops, feeding, a threat
///      display, and an escape that goes BACKWARD (a computed test in the
///      instruments: the way the shell faces against the way it moves).
///
/// THE MIND (the same recipe as the lizard; Core/Minds). Two dials are rolled
/// for each visit: BOLD (walks further from its rock, threatens a fish that is
/// not very near instead of flipping, feeds longer) and HUNGRY (feeds more,
/// walks more and a little faster). They only load the dice. A visit is a
/// timer: out of hiding for 30 to 95 seconds of activity (then the walk home),
/// hidden for two to five minutes (scaled by the "often" knob).
///
/// HOW IT COMES FROM UNDER A ROCK. The picture is only drawn on pixels that are
/// open water. A rock's painted pixels are not water, so a crayfish placed
/// under a rock is simply not drawn there, and as it walks out its claws, then
/// head, then tail appear at the rock's edge, exactly as if it came from under
/// the stone. The home is only used if the whole body sits on non-water pixels
/// at the start (checked by laying the body down there, in the constructor).
/// </summary>
internal sealed class CrayfishBody : Body
{
    private const int Points = 14;
    private enum Doing
    {
        Hidden,    // under its rock: nothing is drawn
        Emerge,    // edging out from under the rock
        Walk,      // walking to a spot on the bed
        Feed,      // standing still, a claw reaching to the mouth now and then
        Threat,    // both claws up and open
        Back,      // a slow walk in reverse
        Pivot,     // swinging round (fast) to put a threat in front, before the flips
        Flip,      // the tail-flip escape: backward bursts
        Hold,      // still, claws up, after the flips
        Retreat,   // walking home, turning round, sliding back under the rock
    }

    private const float MurkLimit = 0.65f;      // a step to where the haze would pass this is refused (the bed is lost there)
    private const float FlipTime = 0.15f, FlipPause = 0.4f;

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _often, _L;       // _L = nose to tail-fan length at the surface, ground pixels
    private readonly bool[] _view;               // open water in view: where it may be drawn AND walk
    private readonly Mind<Doing> _mind = new(Doing.Hidden);
    private Doing _doing => _mind.Doing;
    private readonly Cooldown _scareCool = new();

    // ---- the animal: where its nose is (ground pixels), which way it faces, how deep, how curled ----
    private float _x, _y, _heading, _z, _curl;
    private float _sc = 1, _len, _sinkPx;        // perspective scale for its depth, its length drawn, how far down the screen it sits
    private float _vel;                          // forward speed along its heading, ground px/s (negative: backward)
    private float _walkK;                        // 0 still to 1 walking at full pace: how hard the legs work
    private float _gait, _wob0, _wob1;           // leg phase (runs backward when it does), the two antennae's wave phases
    private float _bold, _hungry, _visitLeft;
    // ---- the home: where it hides, and the two ends of the way out ----
    private bool _hasHome;
    private string _homeKind = "none";
    private float _p0x, _p0y, _outHeading, _hx, _hy;   // hidden spot (nose), the way out, and where the nose is when Emerge ends
    public string HomeKind => _homeKind;
    public bool Out => _doing != Doing.Hidden;
    public PointF Spot => ToScreen(_x, _y);
    // ---- walking ----
    private float _gx, _gy;                      // the Walk goal (for the nose)
    private float _fromX, _fromY, _toX, _toY, _moveT;   // an Emerge or the last slide home: a straight run from-to
    // ---- retreat ----
    private int _stage;                          // 0 walk to the mouth, 1 turn to face out, 2 slide back under
    private bool _wayValid, _wayHome;
    private float _wx, _wy, _planWait, _stuck;
    // ---- the tail flips ----
    private int _flipsLeft;
    private float _flipT, _flipX, _flipY, _pivotTarget;
    private bool _flipActive, _flipStopped, _aimed;      // _aimed: this pivot was to aim the tail at the roomiest way back
    // ---- claws: arm angle, pincer bend, jaw opening, reach, per claw (0 = left, 1 = right) ----
    private readonly float[] _cArm = new float[2], _cPin = new float[2], _cOpen = new float[2], _cExt = new float[2];
    private readonly float[] _reachLeft = new float[2], _reachSm = new float[2];
    private float _feedNext;
    private int _feedSide;
    private readonly float[] _dice = new float[4];

    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private float CentreY => _pond.Centre.Y;
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float p) => p * p * (3 - 2 * p);
    private float WalkSpeed => _u * 0.02f * (0.85f + 0.3f * _hungry);

    public CrayfishBody(Pond pond, Ripples ripples, float often, Random rng) : base(Points)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _L = _u * 0.06f;
        _len = _L;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        // Where it may be drawn and may walk: open water that is not under the branches. Made once.
        _view = new bool[pond.Width * pond.Height];
        for (int i = 0; i < _view.Length; i++) _view[i] = pond.Water[i] && pond.Open[i];
        _hasHome = FindHome();
        _mind.Begin(Doing.Hidden, _hasHome ? (30 + 60 * (float)rng.NextDouble()) / often : Mind<Doing>.Forever);
        ResetClaws();
    }

    // ==================== THE BED ====================

    /// <summary>The floor under a ground point as a 0 to 1 depth (Pond.Floor), the Z everything that swims uses.</summary>
    private float ZAt(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Floor[iy * _pond.Width + ix] / 255f;
    }

    /// <summary>
    /// May a body part be at this ground point? Open water in view, with the bed
    /// in sight (the haze there under the limit), and the sunk picture of it
    /// also on open water. A pad, the land, a rock, the log, the island, the
    /// cattails' foot and the water that is too deep are all "no".
    /// </summary>
    private bool Free(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = (int)s.X, iy = (int)s.Y;
        if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) return false;
        int i = iy * _pond.Width + ix;
        if (!_view[i]) return false;
        float f = _pond.Floor[i] / 255f;
        if (Light.Murk(f) > MurkLimit) return false;
        int jy = (int)(s.Y + Light.SinkPx(f, _u));
        return jy >= 0 && jy < _pond.Height && _view[jy * _pond.Width + ix];
    }

    /// <summary>
    /// Is a step to this nose position allowed? Its middle and the end it leads
    /// with (the claws going forward, the tail going backward) must be free.
    /// (The snake's rule: a refused step turns the animal, it never walks on.)
    /// </summary>
    private bool FreeStep(float nx, float ny, float heading, bool backward)
    {
        float c = MathF.Cos(heading), s = MathF.Sin(heading);
        if (!Free(nx - c * _len * 0.25f, ny - s * _len * 0.25f)) return false;
        if (_doing == Doing.Retreat) return true;      // going home it only has to keep its MIDDLE on open bed: a claw or the tail may go under a stone's edge
        return backward ? Free(nx - c * _len * 0.9f, ny - s * _len * 0.9f) : Free(nx + c * _len * 0.3f, ny + s * _len * 0.3f);
    }

    /// <summary>Is the straight line from a to b walkable (every few pixels, nose, claws and middle all free)?</summary>
    private bool LineFree(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1e-3f) return true;
        float c = dx / d, s = dy / d;
        int steps = (int)(d / 3) + 1;
        for (int i = 1; i <= steps; i++)
        {
            float px = ax + dx * i / steps, py = ay + dy * i / steps;
            if (!Free(px - c * _len * 0.25f, py - s * _len * 0.25f)) return false;
            if (_doing != Doing.Retreat && !Free(px + c * _len * 0.3f, py + s * _len * 0.3f)) return false;
        }
        return true;
    }

    // ==================== THE HOME ====================

    /// <summary>
    /// Finds a hiding place once, when the scene is made: from the middle of
    /// each bank rock (radius over 0.025u) and from the log, look outward for
    /// the first open-water pixel (the rim), and try laying the crayfish down
    /// there with its nose just under the stone. A place counts only if the
    /// water outside is 0.02u to 0.12u deep (the bed shows), the whole body is
    /// hidden on non-water pixels at the start, and the nose comes out into
    /// walkable water. Of all that pass, one is picked at random.
    /// </summary>
    private bool FindHome()
    {
        var found = new List<(float p0x, float p0y, float hd, float hx, float hy, string kind)>();
        float[] insets = [_u * 0.008f, _u * 0.016f, _u * 0.026f];
        void TryRay(PointF origin, float ang, float maxD, string kind)
        {
            int ox = (int)origin.X, oy = (int)origin.Y;
            if (ox < 0 || oy < 0 || ox >= _pond.Width || oy >= _pond.Height || _view[oy * _pond.Width + ox]) return;
            float c = MathF.Cos(ang), s = MathF.Sin(ang);
            for (float d = 1; d <= maxD; d += 1)
            {
                int ix = (int)(origin.X + c * d), iy = (int)(origin.Y + s * d);
                if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) return;
                if (!_view[iy * _pond.Width + ix]) continue;
                // The rim: the first water. Ground positions of it and of the hidden spot's direction.
                (float ex, float ey) = ToGround(ix + 0.5f, iy + 0.5f);
                (float gox, float goy) = ToGround(origin.X, origin.Y);
                float dx = gox - ex, dy = goy - ey, dl = MathF.Sqrt(dx * dx + dy * dy);
                if (dl < 1) return;
                dx /= dl; dy /= dl;
                float hd = MathF.Atan2(-dy, -dx);
                float hx = ex - dx * _u * 0.03f, hy = ey - dy * _u * 0.03f;
                // The bed must show where it first stands: 0.02u to 0.12u down (the floor is raised right at a rock's foot, so ask a little way out).
                PointF xs = ToScreen(hx, hy);
                int xi = Math.Clamp((int)xs.X, 0, _pond.Width - 1), yi = Math.Clamp((int)xs.Y, 0, _pond.Height - 1);
                float depth = _pond.Floor[yi * _pond.Width + xi] / 255f * Pond.DepthU * _u;
                if (depth < _u * 0.02f || depth > _u * 0.12f) return;
                foreach (float inset in insets)
                    if (TestPose(ex + dx * inset, ey + dy * inset, hd, hx, hy) && OpenGoals(hx, hy) >= 5)
                    {
                        found.Add((ex + dx * inset, ey + dy * inset, hd, hx, hy, kind));
                        return;
                    }
                return;
            }
        }
        foreach (BankRock r in _pond.Rocks)
        {
            if (r.Radius <= _u * 0.025f) continue;
            for (int k = 0; k < 24; k++) TryRay(r.Centre, k * MathF.Tau / 24, r.Radius * 1.2f + _u * 0.015f, "rock");
        }
        if (_pond.Log is Log log)
        {
            TryRay(log.Centre, log.Angle, log.Length / 2 + _u * 0.015f, "log");
            TryRay(log.Centre, log.Angle + MathF.PI, log.Length / 2 + _u * 0.015f, "log");
            for (int k = 1; k <= 7; k++)
            {
                PointF o = log.At(k / 8f);
                TryRay(o, log.Angle + MathF.PI / 2, log.Width / 2 + _u * 0.015f, "log");
                TryRay(o, log.Angle - MathF.PI / 2, log.Width / 2 + _u * 0.015f, "log");
            }
        }
        if (found.Count == 0) return false;
        var pick = found[_rng.Next(found.Count)];
        (_p0x, _p0y, _outHeading, _hx, _hy, _homeKind) = pick;
        return true;
    }

    /// <summary>How many of 60 random spots near this mouth can be walked to in a free line (a nook ringed by stones has none, and a crayfish that cannot leave it never walks).</summary>
    private int OpenGoals(float hx, float hy)
    {
        int ok = 0;
        for (int tries = 0; tries < 60; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), r = _u * (0.03f + 0.22f * (float)_rng.NextDouble());
            float gx = hx + MathF.Cos(a) * r, gy = hy + MathF.Sin(a) * r;
            if (Free(gx, gy) && LineFree(hx, hy, gx, gy)) ok++;
        }
        return ok;
    }

    /// <summary>Lays the crayfish down at a hidden spot and checks: the body (and its sides) on non-water, and the nose free at the mouth.</summary>
    private bool TestPose(float px, float py, float hd, float hx, float hy)
    {
        _x = px; _y = py; _heading = hd; _curl = 0;
        _z = ZAt(px - MathF.Cos(hd) * _L * 0.25f, py - MathF.Sin(hd) * _L * 0.25f);
        _sc = Light.Scale(_z); _len = _L * _sc;
        LaySpine();
        for (int i = 0; i < Points; i++)
            if (Along[i] >= 0.15f || i == 0)
            {
                int ix = (int)Spine[i].X, iy = (int)Spine[i].Y;
                if (ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _view[iy * _pond.Width + ix]) return false;
            }
        float c = MathF.Cos(hd), s = MathF.Sin(hd);
        foreach (float along in (ReadOnlySpan<float>)[0.25f, 0.6f, 0.95f])
            for (int side = -1; side <= 1; side += 2)
            {
                float bx = px - c * _len * along + (-s) * side * _len * 0.12f, by = py - s * _len * along + c * side * _len * 0.12f;
                PointF d = ToScreen(bx, by);
                int ix = (int)d.X, iy = (int)(d.Y + _sinkPx);
                if (ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _view[iy * _pond.Width + ix]) return false;
            }
        return FreeStep(hx, hy, hd, false);
    }

    // ==================== UPDATE ====================

    public void Update(float dt, PointF[] threats)
    {
        _mind.Tick(dt);
        if (_doing == Doing.Hidden)
        {
            if (_mind.Over) StartVisit();
            return;
        }
        _scareCool.Tick(dt);
        _visitLeft -= dt;
        float ox = _x, oy = _y;
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        React(threats);

        switch (_doing)
        {
            case Doing.Emerge:
                {
                    float p = Math.Clamp(1 - _mind.Left / 2f, 0, 1), e = Smooth(p);
                    _x = _fromX + (_toX - _fromX) * e; _y = _fromY + (_toY - _fromY) * e;
                    if (_mind.Over) Decide();
                    break;
                }
            case Doing.Walk:
                DoWalk(dt, 1f, true);
                break;
            case Doing.Feed:
                DoFeed(dt);
                break;
            case Doing.Threat:
                if (_mind.Over) Decide();
                break;
            case Doing.Back:
                {
                    float step = _u * 0.012f * dt;
                    float nx = _x - MathF.Cos(_heading) * step, ny = _y - MathF.Sin(_heading) * step;
                    if (FreeStep(nx, ny, _heading, true)) { _x = nx; _y = ny; }
                    else _mind.End();
                    if (_mind.Over) Decide();
                    break;
                }
            case Doing.Pivot:
                {
                    float err = Wrap(_pivotTarget - _heading);
                    Turn(_heading + Math.Clamp(err, -16f * dt, 16f * dt));
                    if (MathF.Abs(Wrap(_pivotTarget - _heading)) < 0.03f || _mind.Over) { bool again = !_aimed; _aimed = false; StartFlips(again); }
                    break;
                }
            case Doing.Flip:
                DoFlip(dt);
                break;
            case Doing.Hold:
                if (_mind.Over) StartRetreat();
                break;
            case Doing.Retreat:
                DoRetreat(dt);
                break;
        }
        if (_doing == Doing.Hidden) return;

        // ---- legs, feelers, claws, curl and depth ----
        float dx = _x - ox, dy = _y - oy;
        float v = dt > 0 ? (dx * MathF.Cos(_heading) + dy * MathF.Sin(_heading)) / dt : 0;
        _vel += (v - _vel) * (1 - MathF.Exp(-dt * 20));
        float nominal = _u * 0.02f;
        _walkK = Math.Min(1f, MathF.Abs(_vel) / nominal);
        if (_doing == Doing.Pivot || (_doing == Doing.Walk && _walkK < 0.05f)) _walkK = MathF.Max(_walkK, 0.5f);   // legs shuffle while it swings round
        float legRate = MathF.Tau * 3f * (_vel / nominal);
        _gait = (_gait + legRate * dt) % MathF.Tau;
        if (_doing == Doing.Pivot) _gait = (_gait + MathF.Tau * 3f * dt) % MathF.Tau;
        _wob0 = (_wob0 + dt * (1.3f + 0.8f * _walkK)) % MathF.Tau;
        _wob1 = (_wob1 + dt * (1.7f + 0.8f * _walkK)) % MathF.Tau;
        float zt = ZAt(_x - MathF.Cos(_heading) * _len * 0.25f, _y - MathF.Sin(_heading) * _len * 0.25f);
        _z += (zt - _z) * (1 - MathF.Exp(-dt / 0.3f));
        if (_z > zt) _z = zt;                        // never deeper than the bed under it (rising ground lifts it at once; sinking is eased)
        _curl += ((_flipActive ? 1f : 0f) - _curl) * (1 - MathF.Exp(-dt * 30));
        UpdateClaws(dt);
        _sc = Light.Scale(_z);
        _len = _L * _sc;
        LaySpine();
    }

    // ==================== THE MIND ====================
    //
    // FEYNMAN VERSION: the crayfish has one thing on its mind at a time. When it
    // runs out of it, it rolls weighted dice for the next. The two dials (bold,
    // hungry) only change how big each face of the dice is. Underneath, the
    // steering refuses any step onto a pixel that is not open, shallow water,
    // and the scare check can cut in on almost anything.

    /// <summary>A new visit: roll the dials, set the clock, start edging out from under the rock.</summary>
    private void StartVisit()
    {
        if (!_hasHome) { _mind.Begin(Doing.Hidden, Mind<Doing>.Forever); return; }
        _bold = Traits.Roll(_rng);
        _hungry = Traits.Roll(_rng);
        _visitLeft = 30 + 65 * (float)_rng.NextDouble();
        _x = _p0x; _y = _p0y; _heading = _outHeading; _curl = 0; _vel = 0; _walkK = 0;
        _fromX = _p0x; _fromY = _p0y; _toX = _hx; _toY = _hy;
        _z = ZAt(_x - MathF.Cos(_heading) * _L * 0.25f, _y - MathF.Sin(_heading) * _L * 0.25f);
        _flipActive = false; _flipsLeft = 0;
        _scareCool.Start(0);
        ResetClaws();
        _mind.Begin(Doing.Emerge, 2f);
        _sc = Light.Scale(_z); _len = _L * _sc;
        LaySpine();
    }

    private void Hide()
    {
        _mind.Begin(Doing.Hidden, (120 + 180 * (float)_rng.NextDouble()) / _often);
    }

    /// <summary>Rolls the next doing. Feed and Walk dominate; a threat display and a step back are small faces.</summary>
    private void Decide()
    {
        if (_visitLeft <= 0) { StartRetreat(); return; }
        bool haveGoal = PickGoal();
        bool canBack = FreeStep(_x - MathF.Cos(_heading) * _u * 0.03f, _y - MathF.Sin(_heading) * _u * 0.03f, _heading, true);
        // A feed lasts 10 to 30 s and a walk a few, so the walk gets the bigger face to keep the time about even.
        _dice[0] = (0.6f + 0.9f * _hungry) * (_doing == Doing.Feed ? 0.4f : 1f);
        _dice[1] = haveGoal ? 1.8f + 0.8f * _hungry + 0.4f * _bold : 0f;
        _dice[2] = 0.08f + 0.1f * _bold;
        _dice[3] = canBack ? 0.2f : 0f;
        int face = Dice.Pick(_rng, _dice);
        switch (face)
        {
            case 1:
                {
                    float d = MathF.Sqrt((_gx - _x) * (_gx - _x) + (_gy - _y) * (_gy - _y));
                    _mind.Begin(Doing.Walk, d / (WalkSpeed * 0.5f) + 3);
                    break;
                }
            case 2:
                _mind.Begin(Doing.Threat, 1 + 2 * (float)_rng.NextDouble());
                break;
            case 3:
                _mind.Begin(Doing.Back, 2.5f);
                break;
            default:
                {
                    float dur = (10 + 20 * (float)_rng.NextDouble()) * (0.8f + 0.2f * _hungry + 0.25f * _bold);
                    _mind.Begin(Doing.Feed, Math.Clamp(dur, 10, 30));
                    _feedNext = 1 + 1.5f * (float)_rng.NextDouble();
                    break;
                }
        }
    }

    /// <summary>A spot on the bed to walk to, near home (a bold one goes further), reached by a line that is free all the way.</summary>
    private bool PickGoal()
    {
        float range = _u * 0.25f * (1 + 0.4f * _bold);
        for (int tries = 0; tries < 24; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), r = _u * (0.03f + (range / _u - 0.03f) * (float)_rng.NextDouble());
            float gx = _hx + MathF.Cos(a) * r, gy = _hy + MathF.Sin(a) * r;
            float dx = gx - _x, dy = gy - _y;
            if (dx * dx + dy * dy < _u * _u * 0.0009f) continue;       // under 0.03u: not worth a walk
            if (!Free(gx, gy) || !LineFree(_x, _y, gx, gy)) continue;
            _gx = gx; _gy = gy;
            return true;
        }
        return false;
    }

    /// <summary>
    /// One frame of walking toward (_gx, _gy): swing round on its legs first
    /// (it does not move forward while it is more than about a radian off), then
    /// step. A refused step ends the walk and the dice are rolled again.
    /// </summary>
    private void DoWalk(float dt, float pace, bool decideAtEnd)
    {
        float dx = _gx - _x, dy = _gy - _y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < _u * 0.004f || (decideAtEnd && _mind.Over)) { if (decideAtEnd) Decide(); return; }
        float err = Wrap(MathF.Atan2(dy, dx) - _heading);
        Turn(_heading + Math.Clamp(err, -2.6f * dt, 2.6f * dt));
        float go = Math.Clamp(1 - MathF.Abs(err), 0, 1);
        float step = MathF.Min(d, WalkSpeed * pace * go * dt);
        if (step <= 0) return;
        float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
        if (FreeStep(nx, ny, _heading, false)) { _x = nx; _y = ny; }
        else if (decideAtEnd) Decide();
        else _wayValid = false;
    }

    private void DoFeed(float dt)
    {
        for (int c = 0; c < 2; c++) _reachLeft[c] -= dt;
        _feedNext -= dt;
        if (_feedNext <= 0)
        {
            _feedSide ^= 1;
            _reachLeft[_feedSide] = 1.2f;
            _feedNext = 2 + 2 * (float)_rng.NextDouble();
        }
        if (_mind.Over) Decide();
    }

    /// <summary>
    /// Swings it to a new heading ON ITS LEGS, about the middle of the shell
    /// (the nose swings round, the middle stays where it is). Turning about the
    /// nose let the middle sweep sideways across a lily pad or a stone with no
    /// step ever tested: the instruments found it standing on a pad for 48 s.
    /// </summary>
    private void Turn(float heading)
    {
        float mx = _x - MathF.Cos(_heading) * _len * 0.25f, my = _y - MathF.Sin(_heading) * _len * 0.25f;
        _heading = Wrap(heading);
        _x = mx + MathF.Cos(_heading) * _len * 0.25f; _y = my + MathF.Sin(_heading) * _len * 0.25f;
    }

    // ---- the scare ----

    /// <summary>
    /// A fish or a turtle over it. Within 0.09u (bold: 0.06u) of its middle: a
    /// bold one with the threat ahead puts on a display and flips only if it
    /// comes within 0.05u; otherwise it flips at once (swinging round first if
    /// the threat is behind it). A 4 second cooldown, so a koi keeping pace
    /// cannot set it off every frame.
    /// </summary>
    private void React(PointF[] threats)
    {
        if (threats.Length == 0) return;
        if (_doing is Doing.Retreat or Doing.Hold or Doing.Pivot or Doing.Flip) return;
        PointF mid = ToScreen(_x - MathF.Cos(_heading) * _len * 0.25f, _y - MathF.Sin(_heading) * _len * 0.25f);
        float best = float.MaxValue;
        int bi = -1;
        for (int i = 0; i < threats.Length; i++)
        {
            float dx = threats[i].X - mid.X, dy = threats[i].Y - mid.Y, d2 = dx * dx + dy * dy;
            if (d2 < best) { best = d2; bi = i; }
        }
        if (bi < 0) return;
        float dist = MathF.Sqrt(best);
        if (_doing == Doing.Threat)
        {
            if (dist < _u * 0.05f) { _scareCool.Start(4); _flipsLeft = 2 + _rng.Next(2); StartFlips(true); }
            return;
        }
        if (!_scareCool.Ready || dist > _u * (0.09f - 0.03f * _bold)) return;
        _scareCool.Start(4);
        if (_doing == Doing.Emerge)
        {
            // Not out yet: it just slides back under the stone it came from.
            StartRetreat();
            _stage = 2; _moveT = 0; _fromX = _x; _fromY = _y; _toX = _p0x; _toY = _p0y;
            return;
        }
        (float tx, float ty) = ToGround(threats[bi].X, threats[bi].Y);
        float toward = MathF.Atan2(ty - _y, tx - _x), rel = Wrap(toward - _heading);
        bool ahead = MathF.Abs(rel) < MathF.PI / 3, behind = MathF.Abs(rel) > 2 * MathF.PI / 3;
        if (ahead && dist > _u * 0.05f && _rng.NextDouble() < _bold)
        {
            _mind.Begin(Doing.Threat, 1 + 2 * (float)_rng.NextDouble());
            return;
        }
        _flipsLeft = 2 + _rng.Next(2);
        if (behind) { _pivotTarget = toward; _mind.Begin(Doing.Pivot, 0.5f); return; }
        StartFlips(true);
    }

    /// <summary>How far it could flip backward from here along heading h, in ground pixels (up to 0.1u), testing every few pixels.</summary>
    private float BackRoom(float h)
    {
        float c = MathF.Cos(h), s = MathF.Sin(h), room = 0;
        for (float d = 3; d <= _u * 0.1f; d += 3)
        {
            if (!FreeStep(_x - c * d, _y - s * d, h, true)) break;
            room = d;
        }
        return room;
    }

    /// <summary>
    /// The first flip of a scare. A flip is a straight backward burst of 0.1u,
    /// and the bed by the bank is narrow, so before it flips it swings its tail
    /// round toward the roomiest way back within about 45 degrees of straight
    /// (a quick pivot, the threat stays more or less ahead of it). If even that
    /// has no room (the stone is right behind it) it freezes with the claws up
    /// (a Hold) and then goes home, rather than flip on the spot against a
    /// wall. After the first flip, a later one that meets something simply
    /// stops short.
    /// </summary>
    private void StartFlips(bool aim)
    {
        float best = BackRoom(_heading), bestH = _heading;
        if (aim && best < _u * 0.1f)
            foreach (float off in (ReadOnlySpan<float>)[0.4f, -0.4f, 0.8f, -0.8f])
            {
                float room = BackRoom(_heading + off);
                if (room > best + _u * 0.01f) { best = room; bestH = Wrap(_heading + off); }
            }
        if (best < _u * 0.03f) { _mind.Begin(Doing.Hold, 1 + (float)_rng.NextDouble()); return; }
        if (bestH != _heading) { _pivotTarget = bestH; _aimed = true; _mind.Begin(Doing.Pivot, 0.5f); return; }
        BeginFlip();
    }

    private void BeginFlip()
    {
        _mind.Begin(Doing.Flip, Mind<Doing>.Forever);
        _flipActive = true; _flipStopped = false;
        _flipT = 0; _flipX = _x; _flipY = _y;
        // A flip in very shallow water slaps the surface: one tiny ring.
        float mx = _x - MathF.Cos(_heading) * _len * 0.25f, my = _y - MathF.Sin(_heading) * _len * 0.25f;
        if (_z * Pond.DepthU < 0.04f)
        {
            PointF s = ToScreen(mx, my);
            _ripples.Add(s.X, s.Y, 0.15f);
        }
    }

    /// <summary>
    /// One frame of the escape: while a flip is active, slide the nose backward
    /// along its own axis 0.1u over 0.15 s (eased, in sub-steps of two pixels);
    /// a step onto a refused pixel stops the flip short. Between flips a 0.4 s
    /// pause; after the last, sit still with the claws up.
    /// </summary>
    private void DoFlip(float dt)
    {
        _flipT += dt;
        if (_flipActive)
        {
            float p = Math.Min(1f, _flipT / FlipTime), e = Smooth(p);
            if (!_flipStopped)
            {
                float tx = _flipX - MathF.Cos(_heading) * _u * 0.1f * e, ty = _flipY - MathF.Sin(_heading) * _u * 0.1f * e;
                float mx = tx - _x, my = ty - _y;
                int n = (int)(MathF.Sqrt(mx * mx + my * my) / 2) + 1;
                for (int k = 0; k < n; k++)
                {
                    float nx = _x + mx / n, ny = _y + my / n;
                    PointF sc = ToScreen(nx, ny);
                    if (!FreeStep(nx, ny, _heading, true) || sc.X < _len || sc.X > _pond.Width - _len || sc.Y < _len || sc.Y > _pond.Height - _len) { _flipStopped = true; break; }   // and never off the picture (one flipped until its tail was off the left edge)
                    _x = nx; _y = ny;
                }
            }
            if (p >= 1)
            {
                _flipActive = false;
                _flipsLeft--;
                _flipT = 0;
                if (_flipsLeft <= 0) _mind.Begin(Doing.Hold, 1 + (float)_rng.NextDouble());
            }
        }
        else if (_flipT >= FlipPause) BeginFlip();
    }

    // ---- going home ----

    private void StartRetreat()
    {
        _stage = 0; _wayValid = false; _planWait = 0; _stuck = 0;
        _flipActive = false;
        _mind.Begin(Doing.Retreat, Mind<Doing>.Forever);
    }

    /// <summary>
    /// Walk to the mouth of the home (round things if the straight line is not
    /// free), swing round to face out, then slide back under the stone, the
    /// reverse of coming out.
    /// </summary>
    private void DoRetreat(float dt)
    {
        switch (_stage)
        {
            case 0:
                {
                    float hd = MathF.Sqrt((_hx - _x) * (_hx - _x) + (_hy - _y) * (_hy - _y));
                    if (hd < _u * 0.012f) { _stage = 1; break; }
                    _planWait -= dt;
                    if (!_wayValid && _planWait <= 0) PlanWay(hd);
                    if (_wayValid)
                    {
                        _gx = _wx; _gy = _wy;
                        DoWalk(dt, 1.5f, false);
                        float wd = MathF.Sqrt((_wx - _x) * (_wx - _x) + (_wy - _y) * (_wy - _y));
                        if (wd < _u * 0.004f && !_wayHome) _wayValid = false;
                    }
                    else
                    {
                        _stuck += dt;
                        if (_stuck > 6) Hide();      // boxed in with no way round (counted by the instruments): it just goes
                    }
                    break;
                }
            case 1:
                {
                    float err = Wrap(_outHeading - _heading);
                    Turn(_heading + Math.Clamp(err, -3.5f * dt, 3.5f * dt));
                    if (MathF.Abs(Wrap(_outHeading - _heading)) < 0.05f)
                    {
                        _stage = 2; _moveT = 0; _fromX = _x; _fromY = _y; _toX = _p0x; _toY = _p0y;
                    }
                    break;
                }
            default:
                {
                    _moveT += dt;
                    float p = Math.Min(1f, _moveT / 2f), e = Smooth(p);
                    _x = _fromX + (_toX - _fromX) * e; _y = _fromY + (_toY - _fromY) * e;
                    if (p >= 1) Hide();
                    break;
                }
        }
    }

    /// <summary>The next point to walk to on the way home: the mouth itself if the line is free, else the free spot nearest it that can be reached in a free line.</summary>
    private void PlanWay(float distHome)
    {
        _planWait = 0.25f;
        if (LineFree(_x, _y, _hx, _hy)) { _wx = _hx; _wy = _hy; _wayHome = true; _wayValid = true; return; }
        float bestD = float.MaxValue;
        for (int tries = 0; tries < 40; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), r = _u * (0.015f + 0.07f * (float)_rng.NextDouble());
            float px = _x + MathF.Cos(a) * r, py = _y + MathF.Sin(a) * r;
            if (!Free(px, py) || !LineFree(_x, _y, px, py)) continue;
            float d = MathF.Sqrt((px - _hx) * (px - _hx) + (py - _hy) * (py - _hy));
            if (d < bestD) { bestD = d; _wx = px; _wy = py; }
        }
        _wayHome = false;
        _wayValid = bestD < float.MaxValue;
    }

    // ==================== BODY, LEGS, FEELERS, CLAWS ====================

    private void ResetClaws()
    {
        for (int c = 0; c < 2; c++) { _cArm[c] = 0.38f; _cPin[c] = -0.30f; _cOpen[c] = 0.22f; _cExt[c] = 1f; _reachLeft[c] = 0; _reachSm[c] = 0; }
    }

    /// <summary>Eases each claw toward the pose its doing wants: held ahead and a touch open, raised and wide (threat), closed (flip), or one reaching to the mouth (feed).</summary>
    private void UpdateClaws(float dt)
    {
        float k = 1 - MathF.Exp(-dt * 9);
        for (int c = 0; c < 2; c++)
        {
            float a = 0.38f, p = -0.30f, o = 0.22f + 0.07f * MathF.Sin(_wob0 + c * 2), e = 1f;
            if (_doing is Doing.Threat or Doing.Hold or Doing.Pivot) { a = 0.95f; p = -0.55f; o = 1.0f; e = 1.08f; }
            else if (_doing == Doing.Flip) { a = 0.2f; p = -0.15f; o = 0.04f; }
            _reachSm[c] += ((_doing == Doing.Feed && _reachLeft[c] > 0 ? 1f : 0f) - _reachSm[c]) * k;
            float r = _reachSm[c];
            a += (0.55f - a) * r; p += (-1.25f - p) * r; o += (0.25f - o) * r; e += (0.85f - e) * r;
            _cArm[c] += (a - _cArm[c]) * k; _cPin[c] += (p - _cPin[c]) * k; _cOpen[c] += (o - _cOpen[c]) * k; _cExt[c] += (e - _cExt[c]) * k;
        }
    }

    // Half the body's width along its length, as a fraction of the length: a pointed rostrum, the carapace
    // widest about a third of the way back, the abdomen narrowing, the tail fan flaring.
    private static readonly float[] ProfS = [0f, 0.05f, 0.18f, 0.32f, 0.48f, 0.5f, 0.86f, 0.9f, 0.96f, 1f];
    private static readonly float[] ProfW = [0.012f, 0.04f, 0.085f, 0.1f, 0.092f, 0.085f, 0.068f, 0.075f, 0.095f, 0.07f];

    private static float Profile(float s)
    {
        for (int i = 1; i < ProfS.Length; i++)
            if (s <= ProfS[i]) return ProfW[i - 1] + (ProfW[i] - ProfW[i - 1]) * (s - ProfS[i - 1]) / (ProfS[i] - ProfS[i - 1]);
        return ProfW[^1];
    }

    /// <summary>
    /// A straight spine back from the nose. In a flip the abdomen curls under,
    /// which from above is a SHORTENING of everything behind the carapace and a
    /// slightly wider tail (the curl number, 0 to 1). The whole body is drawn
    /// _sc smaller for its depth, moved down the screen by its depth, and the
    /// ground is squashed by the view.
    /// </summary>
    private void LaySpine()
    {
        _sinkPx = Light.SinkPx(_z, _u);
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        for (int i = 0; i < Points; i++)
        {
            float a = Along[i];
            float pos = a < 0.5f ? a : 0.5f + (a - 0.5f) * (1 - 0.5f * _curl);
            float gx = _x - c * _len * pos, gy = _y - s * _len * pos;
            float hw = _len * Profile(a) * (a > 0.5f ? 1 + 0.25f * _curl : 1);
            PointF l = ToScreen(gx - s * hw, gy + c * hw), r = ToScreen(gx + s * hw, gy - c * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2 + _sinkPx);
            HalfWidth[i] = MathF.Max(0.6f, MathF.Sqrt((l.X - r.X) * (l.X - r.X) + (l.Y - r.Y) * (l.Y - r.Y)) / 2);
        }
    }

    private bool _shadowPass;

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        r = g = b = 0; alpha = 1;
        // The tail fan's end is scalloped into five lobes (tips at v = 0, +-0.4, +-0.8).
        if (s > 0.9f && s > 0.93f + 0.07f * (0.5f + 0.5f * MathF.Cos(5 * MathF.PI * v))) return false;
        if (_shadowPass) return true;
        float av = MathF.Abs(v);
        float cr, cg, cb;
        if (s < 0.5f)
        {
            // The carapace: dark brick (a real one is rust and brick, not scarlet: the first was a scarlet stick figure), an
            // olive-brown ridge down the middle, a groove across at the neck, pale speckles.
            cr = 124; cg = 48; cb = 32;
            float ridge = Math.Clamp((0.16f - av) * 6, 0, 1);
            cr += (74 - cr) * 0.6f * ridge; cg += (62 - cg) * 0.6f * ridge; cb += (34 - cb) * 0.6f * ridge;
            float groove = Math.Clamp(1 - MathF.Abs(s - 0.33f) * 70, 0, 1) * 0.5f;
            cr -= 50 * groove; cg -= 14 * groove; cb -= 10 * groove;
            // Pale speckles: a cheap hash of the cell (s, v) falls in, two in thirty-two cells (sines per pixel cost more than the whole body).
            if ((((int)(s * 120f) * 73856093) ^ ((int)((v + 1f) * 9f) * 19349663)) % 16 == 0) { cr += 60; cg += 50; cb += 45; }
        }
        else if (s < 0.9f)
        {
            // The abdomen: six plates, a dark seam at each joint, the plate's edges a touch paler.
            float u = (s - 0.5f) / 0.4f * 6f, f = u - MathF.Floor(u);
            cr = 116; cg = 46; cb = 30;
            float seam = Math.Clamp(1 - MathF.Abs(f - 0.05f) * 9, 0, 1);
            cr -= 60 * seam; cg -= 16 * seam; cb -= 12 * seam;
            float edge = Math.Clamp((av - 0.7f) * 3, 0, 1);
            cr += 36 * edge; cg += 26 * edge; cb += 20 * edge;
        }
        else
        {
            // The tail fan: lobes with dark lines between, paler at the edges.
            float line = Math.Clamp(1 - MathF.Abs(MathF.Sin(2.5f * MathF.PI * v)) * 7, 0, 1) * 0.5f;
            cr = 130; cg = 52; cb = 38;
            float pale = Math.Clamp((s - 0.93f) * 12, 0, 1) * 0.6f + Math.Clamp((av - 0.6f) * 2, 0, 1) * 0.4f;
            cr += 46 * pale; cg += 56 * pale; cb += 50 * pale;
            cr -= 50 * line; cg -= 14 * line; cb -= 10 * line;
        }
        r = Math.Clamp((int)cr, 0, 255); g = Math.Clamp((int)cg, 0, 255); b = Math.Clamp((int)cb, 0, 255);
        return true;
    }

    // ==================== DRAW ====================

    public void Draw(FrameBuffer fb)
    {
        if (_doing == Doing.Hidden) return;
        float murk = Light.Murk(_z);
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        // The shadow on the bed: the body's own shape, a hair down and to the right (never more than 0.012u), faint and soft.
        float reach = _u * 0.012f;
        _shadowPass = true;
        PaintShadow(fb, _view, reach * 0.6f, reach * 0.8f, 0.25f * (1 - murk), soft * 2f);
        _shadowPass = false;
        DrawLegs(fb, murk);
        DrawFeelers(fb, murk);
        Paint(fb, _view, _pond.Sun, murk, soft, _pond.Pixels);
        DrawClaws(fb, murk);
        DrawEyes(fb, murk);
    }

    /// <summary>A ground point on the body at fraction s of its length (nose 0), as a drawn screen point.</summary>
    private PointF BodyAt(float s, float side, float widthK)
    {
        float c = MathF.Cos(_heading), sn = MathF.Sin(_heading);
        float pos = s < 0.5f ? s : 0.5f + (s - 0.5f) * (1 - 0.5f * _curl);
        float hw = _len * Profile(s) * widthK * side;
        PointF p = ToScreen(_x - c * _len * pos - sn * hw, _y - sn * _len * pos + c * hw);
        return new PointF(p.X, p.Y + _sinkPx);
    }

    /// <summary>A small oval given in GROUND terms (a centre, an angle on the ground, semi-length and semi-width), turned into the squashed, sunk picture.</summary>
    private void OvalG(FrameBuffer fb, float gx, float gy, float ang, float semiLen, float semiWid, int r, int g, int b, float alpha, float murk)
    {
        float ca = MathF.Cos(ang), sa = MathF.Sin(ang) * Ripples.Squash;
        float lenK = MathF.Sqrt(ca * ca + sa * sa);
        float pa = -MathF.Sin(ang), pb = MathF.Cos(ang) * Ripples.Squash;
        float widK = MathF.Sqrt(pa * pa + pb * pb);
        PointF c = ToScreen(gx, gy);
        Oval(fb, _view, new PointF(c.X, c.Y + _sinkPx), MathF.Atan2(sa, ca), MathF.Max(0.8f, semiLen * lenK), MathF.Max(0.9f, semiWid * widK),
            r, g, b, alpha, murk, _pond.Sun, rays: false, water: _pond.Pixels);
    }

    /// <summary>Eight thin walking legs, four a side, each swinging fore and aft on a wave down the body (front to back), the two sides half a step apart. Folded back when it curls.</summary>
    private void DrawLegs(FrameBuffer fb, float murk)
    {
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        float ll = _len * 0.13f;
        for (int k = 0; k < 4; k++)
        {
            float along = 0.1f + 0.09f * k, hw = _len * Profile(along);
            float gx = _x - c * _len * along, gy = _y - s * _len * along;
            for (int side = -1; side <= 1; side += 2)
            {
                float swing = 0.45f * _walkK * MathF.Sin(_gait + 0.9f * k + (side > 0 ? 0 : MathF.PI));
                float a = _heading + side * (MathF.PI / 2 - 0.55f + 0.38f * k + swing + 0.9f * _curl);
                float ax = gx - s * hw * 0.85f * side, ay = gy + c * hw * 0.85f * side;
                OvalG(fb, ax + MathF.Cos(a) * ll, ay + MathF.Sin(a) * ll, a, ll, _len * 0.012f, 124, 42, 30, 1f, murk);
            }
        }
    }

    /// <summary>Two long feelers swept back past the tail (half again the body), each of three pieces whose tips wave slowly; two short ones ahead. They grow as it comes out from under the rock.</summary>
    private void DrawFeelers(FrameBuffer fb, float murk)
    {
        float dist = MathF.Sqrt((_x - _p0x) * (_x - _p0x) + (_y - _p0y) * (_y - _p0y));
        float k = Math.Clamp(0.2f + dist / _L, 0.2f, 1f);
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        float th = MathF.Max(1f, _len * 0.012f);
        for (int side = -1; side <= 1; side += 2)
        {
            float ph = side > 0 ? _wob0 : _wob1;
            float px = _x - c * _len * 0.05f - s * side * _len * 0.03f, py = _y - s * _len * 0.05f + c * side * _len * 0.03f;
            PointF prev = D(px, py);
            for (int j = 0; j < 3; j++)
            {
                float a = _heading + MathF.PI - side * (0.35f - 0.1f * j + 0.3f * MathF.Sin(ph + 1.3f * j));
                px += MathF.Cos(a) * _len * 0.5f * k; py += MathF.Sin(a) * _len * 0.5f * k;
                PointF next = D(px, py);
                Stroke(fb, prev, next, 120, 50, 36, 0.85f - 0.15f * j, th, murk);
                prev = next;
            }
            // The short feeler ahead of the nose.
            float aa = _heading + side * (0.25f + 0.15f * MathF.Sin(ph * 1.5f));
            PointF f0 = D(_x, _y), f1 = D(_x + MathF.Cos(aa) * _len * 0.2f, _y + MathF.Sin(aa) * _len * 0.2f);
            Stroke(fb, f0, f1, 130, 62, 46, 0.8f, th, murk);
        }
    }

    private PointF D(float gx, float gy)
    {
        PointF p = ToScreen(gx, gy);
        return new PointF(p.X, p.Y + _sinkPx);
    }

    /// <summary>A thin line hazed toward the water under its start (so a feeler at depth fades as the body does).</summary>
    private void Stroke(FrameBuffer fb, PointF a, PointF b, int r, int g, int bl, float alpha, float thick, float murk)
    {
        int ix = Math.Clamp((int)a.X, 0, _pond.Width - 1), iy = Math.Clamp((int)a.Y, 0, _pond.Height - 1);
        uint wc = _pond.Pixels[iy * _pond.Width + ix];
        r += (int)((((wc >> 16) & 0xFF) - r) * murk); g += (int)((((wc >> 8) & 0xFF) - g) * murk); bl += (int)(((wc & 0xFF) - bl) * murk);
        fb.Line(a, b, Color.FromArgb(r, g, bl), alpha, thick, _view);
    }

    /// <summary>
    /// The two big claws: an arm, a palm, and two fingers (the jaw) that open and
    /// close. Each claw's arm angle, pincer bend, jaw opening and reach are
    /// eased toward the pose its doing wants (UpdateClaws).
    /// </summary>
    private void DrawClaws(FrameBuffer fb, float murk)
    {
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        for (int side = -1; side <= 1; side += 2)
        {
            int cl = side > 0 ? 1 : 0;
            float ext = _cExt[cl];
            float hw = _len * Profile(0.14f);
            float sx = _x - c * _len * 0.14f - s * side * hw * 0.9f, sy = _y - s * _len * 0.14f + c * side * hw * 0.9f;
            float aa = _heading + side * _cArm[cl], armLen = _len * 0.22f * ext;
            OvalG(fb, sx + MathF.Cos(aa) * armLen * 0.5f, sy + MathF.Sin(aa) * armLen * 0.5f, aa, armLen * 0.5f, _len * 0.04f, 118, 46, 32, 1f, murk);
            float ex = sx + MathF.Cos(aa) * armLen, ey = sy + MathF.Sin(aa) * armLen;
            float ap = _heading + side * _cPin[cl], palm = _len * 0.11f * ext;
            float pcx = ex + MathF.Cos(ap) * palm * 0.5f, pcy = ey + MathF.Sin(ap) * palm * 0.5f;
            OvalG(fb, pcx, pcy, ap, palm * 0.5f, _len * 0.055f, 126, 50, 34, 1f, murk);
            OvalG(fb, pcx, pcy, ap, _len * 0.012f, _len * 0.012f, 214, 150, 126, 0.8f, murk);       // a pale bump
            float bx = ex + MathF.Cos(ap) * palm, by = ey + MathF.Sin(ap) * palm, fl = _len * 0.13f * ext;
            for (int j = -1; j <= 1; j += 2)
            {
                float af = ap + j * _cOpen[cl] * 0.5f;
                OvalG(fb, bx + MathF.Cos(af) * fl * 0.5f, by + MathF.Sin(af) * fl * 0.5f, af, fl * 0.5f, _len * 0.022f, 132, 54, 38, 1f, murk);
            }
        }
    }

    private void DrawEyes(FrameBuffer fb, float murk)
    {
        float c = MathF.Cos(_heading), s = MathF.Sin(_heading);
        for (int side = -1; side <= 1; side += 2)
        {
            float gx = _x - c * _len * 0.07f - s * side * _len * 0.04f, gy = _y - s * _len * 0.07f + c * side * _len * 0.04f;
            OvalG(fb, gx, gy, 0, _len * 0.02f, _len * 0.02f, 18, 10, 10, 0.95f, murk);
        }
    }
}
