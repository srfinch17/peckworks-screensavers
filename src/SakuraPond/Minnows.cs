using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Schools of minnows: dozens of tiny fish just under the surface, moving as
/// one, and the dragonfly's playthings.
///
/// WHAT A SCHOOL OF MINNOWS DOES (watch the shallows of any pond): the fish
/// hang close together, all facing much the same way, fidgeting with small
/// quick darts and then holding still. When the school turns, it turns
/// TOGETHER, and the silver sides of the fish catch the light as they roll
/// into the turn, so a flash runs through the school. Anything that touches
/// the surface (an insect, a crumb, a dragonfly's tail) sends out rings, and
/// the school rushes to the spot in a body and swirls there, every fish
/// snapping at nothing, then loses interest and drifts away. A big fish
/// cruising underneath scatters them like a thrown handful of rice, and a
/// moment later they are a school again.
///
/// HOW IT IS BUILT: each minnow is a tiny bending Body (Body.cs), its spine
/// laid along the trail its head has swum, exactly as the koi are, so it
/// turns by bending and never slides or flips. The school is three rules per
/// fish each frame, the classic flocking rules: stay with the school (head for
/// the school's shared target), face the way your neighbours face, and keep
/// a little room between you and the next fish. Add "flee a koi", "turn away
/// from the bank", and a shared MOOD for the whole school (drifting, rushing
/// to a lure, milling round it), and the dozen fish move as one creature.
///
/// The dragonfly's game: Dragonflies.cs asks where the nearest school is,
/// taps the water near it (Lure), and the school comes running.
/// </summary>
internal sealed class Minnows
{
    private readonly School[] _schools;
    private readonly float _u;
    // THE INSTRUMENT LOG (permanent, free unless PECKWORKS_MINNOWLOG names a file): one row per minnow per frame,
    // where it is (ground pixels), which way it faces, how fast it is going, whether it is following a wall and on
    // which side, how much water the room grid gives it where it stands, and whether its step was refused. Added for
    // "a minnow can swim in there and never get back out. it just spins in fast little circles forever" (2026-10-27):
    // a spin is a nose confined to a few pixels at speed with the heading going round, which only a log can count.
    private static readonly System.IO.StreamWriter? MinnowLog = Environment.GetEnvironmentVariable("PECKWORKS_MINNOWLOG") is string lf && lf.Length > 0 ? new System.IO.StreamWriter(lf) { AutoFlush = false } : null;
    private int _logFrame;
    /// <summary>
    /// Where the crayfish and prawns of the deep bed are (DeepDwellers.AnySpot, set by the scene): half of a school's
    /// dips to the bed go to a spot near one of them, so a claw gets its chance at a minnow. Null means dips go anywhere
    /// deep. Static, like the stress dials: one pond per process.
    /// </summary>
    public static Func<Random, PointF?>? DipNear;

    /// <param name="where">Open water in view: where a school near the surface is drawn and may go.</param>
    public Minnows(Pond pond, int schools, bool[] where, Ripples ripples, Random rng)
    {
        _u = pond.U;
        _schools = new School[Math.Max(0, schools)];
        for (int i = 0; i < _schools.Length; i++) _schools[i] = new School(pond, where, ripples, rng, 12 + rng.Next(8));
    }

    /// <param name="koiHeads">Points along each koi near the surface this frame (head and middle), in screen pixels: a school near the surface keeps clear of them.</param>
    /// <param name="bedHeads">The same points of EVERY koi whatever its depth: what a school down on the bed keeps clear of (a forager down there is the threat; a fish at the top is not).</param>
    public void Update(float dt, PointF[] koiHeads, PointF[] bedHeads)
    {
        foreach (School s in _schools) s.Update(dt, s.Deep ? bedHeads : koiHeads);
        if (MinnowLog != null)
        {
            _logFrame++;
            for (int s = 0; s < _schools.Length; s++) _schools[s].LogRows(MinnowLog, _logFrame, s);
            if (_logFrame % 600 == 0) MinnowLog.Flush();
        }
    }

    /// <summary>The schools near the surface: after the koi and the turtles (they are just under the surface, over everything deeper).</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (School s in _schools) if (!s.DrawnDeep) s.Draw(fb);
    }

    /// <summary>
    /// The schools that have dipped toward the bed (round thirty-seven): BEFORE the koi and the turtles, so a fish
    /// swimming over them paints over them. One cut, not a place in the koi's depth order: a school on its way down
    /// is under every koi at once, which is near enough for a dozen fish a few pixels long (stated in the docs).
    /// </summary>
    public void DrawDeep(FrameBuffer fb)
    {
        foreach (School s in _schools) if (s.DrawnDeep) s.Draw(fb);
    }

    /// <summary>
    /// The nearest school whose middle is within "reach" pixels of this point AND in view (open water, not under a
    /// branch or a pad), or false. A koi asks this before it goes to play with a school (Koi.Tease): it reads, it
    /// never changes anything. No allocation: the middle comes back through the out argument. A school down on the
    /// bed is not one a koi at the top rushes.
    /// </summary>
    public bool NearestSchoolWithin(float x, float y, float reach, out PointF centre)
    {
        centre = default;
        float bestD = reach * reach;
        bool found = false;
        foreach (School s in _schools)
        {
            if (!s.CentreInView || s.Deep) continue;
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; centre = s.Centre; found = true; }
        }
        return found;
    }

    /// <summary>
    /// A big fish has rushed a school: the nearest school in view within "reach" (screen pixels) of (x, y) bursts
    /// apart from that point and then gathers itself again a little way off. True if a school was there to scatter.
    /// </summary>
    public bool Scatter(float x, float y, float reach)
    {
        School? best = null;
        float bestD = reach * reach;
        foreach (School s in _schools)
        {
            if (!s.CentreInView) continue;
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s; }
        }
        best?.Scatter(x, y);
        return best != null;
    }

    /// <summary>A school is never hunted down below this many fish, and a taken fish is replaced by a newcomer 3 to 6 minutes later.</summary>
    private const int LeastFish = 4;

    /// <summary>
    /// Like NearestSchool, but only a school with more than four fish alive (a hungry snake has nothing to take from
    /// a school of four) and without the dragonfly's courtesy of skipping the school teased last.
    /// </summary>
    public PointF? NearestHuntable(float x, float y)
    {
        PointF? best = null;
        float bestD = float.MaxValue;
        foreach (School s in _schools)
        {
            if (s.Live <= LeastFish || s.Deep) continue;                   // (the snake hunts at the top of the water: a school down on the bed is out of its reach)
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s.Centre; }
        }
        return best;
    }

    /// <summary>The nose of the nearest living minnow (of a school near the surface that can spare one) within "within" pixels of (x, y), screen pixels. For a snake to aim at.</summary>
    public bool NearestFish(float x, float y, float within, out PointF nose)
    {
        nose = default;
        float bestD = within * within;
        bool found = false;
        foreach (School s in _schools)
        {
            if (s.Live <= LeastFish || s.Deep) continue;
            Minnow? f = s.NearestLive(x, y, ref bestD, -1f, 0.3f);
            if (f != null) { nose = f.Nose; found = true; }
        }
        return found;
    }

    /// <summary>
    /// The nose of the nearest living minnow (of a school that can spare one) within "within" pixels of (x, y), screen
    /// pixels, that is at least zMin deep: what a crayfish or prawn on the bed can reach when a school dips to it
    /// (DeepDwellers). False when none.
    /// </summary>
    public bool NearestFishDeep(float x, float y, float within, float zMin, out PointF nose)
    {
        nose = default;
        float bestD = within * within;
        bool found = false;
        foreach (School s in _schools)
        {
            if (s.Live <= LeastFish || !s.Deep) continue;
            Minnow? f = s.NearestLive(x, y, ref bestD, zMin, 2f);
            if (f != null) { nose = f.Nose; found = true; }
        }
        return found;
    }

    /// <summary>
    /// The jaws of something closed here (screen pixels): the nearest living minnow whose nose is within reachPx of the
    /// point, and whose depth is between zMin and zMax (the snake's jaws at the top of the water, a claw on the bed),
    /// is gone, and true comes back. A school never drops below four. The school goes on with one fewer, and a
    /// newcomer swims in from the picture's edge three to six minutes later (so the pond does not empty over a night).
    /// </summary>
    public bool Take(float x, float y, float reachPx, float zMin = -1f, float zMax = 0.3f)
    {
        School? from = null;
        Minnow? fish = null;
        float bestD = reachPx * reachPx;
        foreach (School s in _schools)
        {
            if (s.Live <= LeastFish) continue;
            Minnow? f = s.NearestLive(x, y, ref bestD, zMin, zMax);
            if (f != null) { fish = f; from = s; }
        }
        if (fish == null || from == null) return false;
        from.Remove(fish);
        return true;
    }

    private School? _lastLured;

    /// <summary>Something touched the water here (screen pixels): the nearest school, if near enough and not down on the bed, rushes to it.</summary>
    public void Lure(float x, float y)
    {
        School? best = null;
        float bestD = _u * _u * 0.8f * 0.8f;
        foreach (School s in _schools)
        {
            if (s.Deep) continue;
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s; }
        }
        best?.Lure(x, y);
        if (best != null) _lastLured = best;
    }

    /// <summary>
    /// The middle of the school a dragonfly should go and tease next (screen pixels), or null when
    /// there are no minnows: the nearest one, except that the school teased last time waits its turn
    /// (with one dragonfly darting about near one school, that school got every game).
    /// </summary>
    public PointF? NearestSchool(float x, float y)
    {
        PointF? best = null;
        float bestD = float.MaxValue;
        foreach (School s in _schools)
        {
            if ((s == _lastLured && _schools.Length > 1) || s.Deep) continue;   // (nor a school down on the bed: the game is at the surface)
            float dx = s.Centre.X - x, dy = s.Centre.Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s.Centre; }
        }
        return best;
    }
}

/// <summary>One school: a dozen or so minnows and the mood they share.</summary>
internal sealed class School
{
    private enum Mood
    {
        Drift,      // idling toward a spot of its own
        Rush,       // flat out to a lure (a dragonfly's tap)
        Mill,       // round and round the lure
        Scatter,    // a koi has rushed the school: every fish bursts straight away from its head, flat out
        Regroup,    // the burst over, the fish gather again a little way off, then drift on
        Dip,        // briskly down to a spot on the deep bed, sinking as it goes (round thirty-seven: "if minnows swim down by them")
        Graze,      // on the bed: a slow mill about the spot, picking at the silt, then up and away on the next drift
    }

    private readonly Pond _pond;
    private readonly Random _rng;
    private readonly Ripples _ripples;
    private readonly Minnow[] _fish;
    private readonly float _u;
    private Mood _mood = Mood.Drift;
    // THE SCHOOL'S DEPTH (0 the surface, 1 the pond's full depth), shared by its fish (each capped by the bed under its own
    // nose), eased toward what the mood wants at a rate held to the school's pace (a school that sank faster than it swam
    // would be seen sliding down the screen: Light.HoldDepth's rule, applied to the school as one). 0 until a Dip.
    private float _z, _zWant;
    /// <summary>Down toward the bed (past 0.3): a school the surface's games, hunts and teases leave alone, that every koi's head whatever its depth scares, and that a crayfish's claw can reach.</summary>
    public bool Deep => _z > 0.3f;
    /// <summary>Drawn before the koi (under them) rather than after: from a quarter of the depth down.</summary>
    public bool DrawnDeep => _z > 0.25f;
    public float Z => _z;
    // A STRESS DIAL for the instruments: PECKWORKS_MINNOWDIP multiplies the chance that a new drift target is a dip to the
    // bed (one in ten in the real pond), so a five-minute run shows what an hour would of the claws' catches.
    private static readonly float DipOften = Environment.GetEnvironmentVariable("PECKWORKS_MINNOWDIP") is string dp && dp.Length > 0 && float.TryParse(dp, System.Globalization.CultureInfo.InvariantCulture, out float dpv) ? dpv : 1f;
    private float _nipTimer;             // milling fish nip at the surface now and then: a fizz of tiny rings
    private float _tx, _ty;              // where the school is heading, ground pixels
    private float _lx, _ly;              // the lure: where the water was touched, ground pixels
    private float _timer, _orbit;
    private float _cx, _cy;              // the school's middle, ground pixels
    private readonly Random _own;        // this school's own dice (a newcomer's place and wait): never the scene's, so a taken fish moves no other roll
    private int _live;                   // fish alive (the rest were taken and have not been replaced yet)

    /// <summary>The middle of the school, screen pixels.</summary>
    public PointF Centre { get; private set; }

    /// <summary>How many fish are alive in it.</summary>
    public int Live => _live;

    /// <summary>The living fish whose nose is nearest to (x, y) and closer than the squared distance in "best2" (which is lowered to the new best). Null if none.</summary>
    public Minnow? NearestLive(float x, float y, ref float best2, float zMin, float zMax)
    {
        Minnow? found = null;
        foreach (Minnow f in _fish)
        {
            if (f.Gone || f.Z < zMin || f.Z > zMax) continue;
            PointF n = f.Nose;
            float dx = n.X - x, dy = n.Y - y, d2 = dx * dx + dy * dy;
            if (d2 < best2) { best2 = d2; found = f; }
        }
        return found;
    }

    /// <summary>A fish was taken: it is gone, and a newcomer will join in three to six minutes.</summary>
    public void Remove(Minnow f)
    {
        f.Gone = true;
        f.BackIn = 180f + 180f * (float)_own.NextDouble();
        _live--;
    }

    /// <summary>
    /// A newcomer for a taken fish's place: a water pixel on the picture's edge (in view, no branch over it), the nose one
    /// pixel inside and the body trailing out beyond the edge, heading for the school. False if no such pixel was found this
    /// frame (it tries again next frame).
    /// </summary>
    private bool Rejoin(Minnow f)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            int side = _own.Next(4);
            float along = (float)_own.NextDouble();
            int ix = side == 0 ? 1 : side == 1 ? _pond.Width - 2 : (int)(along * (_pond.Width - 4)) + 2;
            int iy = side == 2 ? 1 : side == 3 ? _pond.Height - 2 : (int)(along * (_pond.Height - 4)) + 2;
            if (!InView(ix, iy)) continue;
            (float gx, float gy) = Minnow.ToGround(_pond, ix, iy);
            f.Place(gx, gy, MathF.Atan2(_cy - gy, _cx - gx));
            f.Gone = false;
            return true;
        }
        return false;
    }

    /// <summary>Is the school's middle on open water that can be seen (a safe place to look up, and a pixel inside the picture)?</summary>
    public bool CentreInView => Centre.X >= 0 && Centre.Y >= 0 && Centre.X < _pond.Width && Centre.Y < _pond.Height && InView(Centre.X, Centre.Y);

    public School(Pond pond, bool[] where, Ripples ripples, Random rng, int count)
    {
        _pond = pond;
        _rng = rng;
        _ripples = ripples;
        _u = pond.U;
        // A home in open water with room around it, and the fish gathered close round it, all facing much the same way.
        PointF home = pond.Centre;
        for (int tries = 0; tries < 300; tries++)
        {
            float x = (float)rng.NextDouble() * pond.Width, y = (float)rng.NextDouble() * pond.Height;
            if (pond.RoomAt(x, y) < _u * 0.12f || !InView(x, y)) continue;
            home = new PointF(x, y);
            break;
        }
        (_cx, _cy) = Minnow.ToGround(pond, home.X, home.Y);
        float heading = (float)(rng.NextDouble() * MathF.Tau);
        _fish = new Minnow[count];
        for (int i = 0; i < count; i++)
        {
            float a = (float)(rng.NextDouble() * MathF.Tau), d = _u * 0.045f * MathF.Sqrt((float)rng.NextDouble());
            _fish[i] = new Minnow(pond, where, rng, _cx + MathF.Cos(a) * d, _cy + MathF.Sin(a) * d, heading + 0.5f * ((float)rng.NextDouble() - 0.5f));
        }
        _live = count;
        _own = new Random(unchecked(count * 7919 + (int)home.X * 31 + (int)home.Y * 17 + 12345));
        Centre = home;
        NewTarget();
    }

    /// <summary>Open water that no branch hides (a school idling under the blossom would be a school nobody sees).</summary>
    private bool InView(float x, float y)
    {
        int i = (int)y * _pond.Width + (int)x;
        return _pond.Water[i] && _pond.Open[i];
    }

    /// <summary>Picks somewhere new in open water, in view, for the school to idle toward.</summary>
    // A STRESS DIAL for the instruments: PECKWORKS_MINNOWGOAL names a stone (island, formation, formation2) and every
    // school's idle target is that stone's centre, so the minnows crowd its caps for a whole run (naturally they visited
    // no three-cap formation in fifteen minutes of logs; "a minnow can swim in there and never get back out" needs a run
    // where they try). The dice roll exactly as they would (the spot is picked, then overridden).
    private static readonly string? GoalStone = Environment.GetEnvironmentVariable("PECKWORKS_MINNOWGOAL") is string gs && gs.Length > 0 ? gs : null;

    private void NewTarget()
    {
        (PointF Centre, float Radius)? stone = GoalStone switch
        {
            "island" when _pond.Island is Island i => (i.Centre, i.Radius),
            "formation" when _pond.Formation is Formation f => (f.Stone.Centre, f.Stone.Radius),
            "formation2" when _pond.Formation2 is Formation f2 => (f2.Stone.Centre, f2.Stone.Radius),
            _ => null
        };
        for (int tries = 0; tries < 60; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            if (_pond.RoomAt(x, y) < _u * 0.1f || !InView(x, y)) continue;
            if (stone is (PointF sc, float sr))
            {
                // A spot in the water within 1.6 radii of the stone, from the school's own dice (the scene's are not moved).
                for (int k = 0; k < 40; k++)
                {
                    float a = (float)(_own.NextDouble() * MathF.Tau), d = sr * (0.8f + 0.8f * (float)_own.NextDouble());
                    float nx = sc.X + MathF.Cos(a) * d, ny = sc.Y + MathF.Sin(a) * d;
                    if (nx < 1 || ny < 1 || nx >= _pond.Width - 1 || ny >= _pond.Height - 1 || _pond.RoomAt(nx, ny) < 3 || !InView(nx, ny)) continue;
                    x = nx; y = ny; break;
                }
            }
            (_tx, _ty) = Minnow.ToGround(_pond, x, y);
            break;
        }
        _timer = 14 + 16 * (float)_rng.NextDouble();
        _zWant = 0;                                            // a drift is at the top of the water (a school that was down comes up as it goes)
        // A DIP (round thirty-seven): about one new target in sixteen is a spot on the deep bed instead, and the school
        // goes down to it to pick at the silt (with the graze that follows, about a tenth of its time). From the school's
        // OWN dice (the scene's are not moved by this); a spot with a floor at least 0.65 of the depth, in view, with
        // room, or no dip this time; half the time near one of the deep bed's crayfish or prawns (DipNear), which is how
        // "if minnows swim down by them" comes about at all. The stress dial scales the one in sixteen.
        if (_own.NextDouble() < 0.06 * DipOften)
        {
            PointF? near = _own.NextDouble() < 0.5 ? Minnows.DipNear?.Invoke(_own) : null;
            for (int k = 0; k < 40; k++)
            {
                float x, y;
                if (near is PointF np)
                {
                    PointF ns = Minnow.ToScreen(_pond, np.X, np.Y);
                    float a = (float)(_own.NextDouble() * MathF.Tau), d = _u * (0.04f + 0.1f * (float)_own.NextDouble());
                    x = ns.X + MathF.Cos(a) * d; y = ns.Y + MathF.Sin(a) * d;
                    if (x < 1 || y < 1 || x >= _pond.Width - 1 || y >= _pond.Height - 1) continue;
                }
                else { x = (float)_own.NextDouble() * _pond.Width; y = (float)_own.NextDouble() * _pond.Height; }
                int ix = (int)x, iy = (int)y;
                if (!InView(x, y) || _pond.RoomAt(x, y) < _u * (near != null ? 0.06f : 0.1f) || _pond.Floor[iy * _pond.Width + ix] / 255f * Pond.DepthU < 0.35f) continue;   // (a floor at least 0.35u deep: 0.65 of the 0.54u pond until 2026-10-28, kept in u)
                (_tx, _ty) = Minnow.ToGround(_pond, x, y);
                _zWant = 0.9f * _pond.Floor[iy * _pond.Width + ix] / 255f - 0.04f;
                _mood = Mood.Dip;
                _timer = 30;
                break;
            }
        }
    }

    public void Lure(float x, float y)
    {
        (_lx, _ly) = Minnow.ToGround(_pond, x, y);
        _mood = Mood.Rush;
        _timer = 5;
    }

    // Where the big fish's head was when it rushed the school (ground pixels), and where the school gathers after.
    private float _scX, _scY, _rgX, _rgY;

    /// <summary>
    /// A koi has rushed the school from (x, y) (screen pixels): the school explodes. For most of a second every
    /// fish swims flat out straight away from that point (so the school fans out in a ring, the way a shoal bursts
    /// when a big fish lunges), then they gather again a short way off on the far side from the koi and drift on.
    /// A school already scattering or gathering is scattered afresh (the koi may rush twice).
    /// </summary>
    /// <summary>The instrument log's rows for this school's fish (Minnows.Update, when PECKWORKS_MINNOWLOG names a file).</summary>
    public void LogRows(System.IO.StreamWriter log, int frame, int school)
    {
        for (int i = 0; i < _fish.Length; i++) if (!_fish[i].Gone) log.WriteLine(FormattableString.Invariant($"F,{frame},{school},{i},{_mood},{_fish[i].LogRow()}"));
    }

    public void Scatter(float x, float y)
    {
        (_scX, _scY) = Minnow.ToGround(_pond, x, y);
        float ax = _cx - _scX, ay = _cy - _scY, l = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
        // The gathering point: on from the school's middle, away from the koi, a tenth of the screen's unit.
        _rgX = _cx + ax / l * _u * 0.1f; _rgY = _cy + ay / l * _u * 0.1f;
        PointF rg = Minnow.ToScreen(_pond, _rgX, _rgY);
        if (!(rg.X >= 0 && rg.Y >= 0 && rg.X < _pond.Width && rg.Y < _pond.Height && InView(rg.X, rg.Y) && _pond.RoomAt(rg.X, rg.Y) >= _u * 0.06f))
        {
            _rgX = _cx; _rgY = _cy;                                                // no room that way: gather where the school was
        }
        _mood = Mood.Scatter;
        _timer = 0.6f + 0.3f * (float)_rng.NextDouble();
    }

    public void Update(float dt, PointF[] koiHeads)
    {
        // Where the school is now: the mean of its fish.
        float sx = 0, sy = 0;
        int alive = 0;
        foreach (Minnow f in _fish)
        {
            if (f.Gone)
            {
                f.BackIn -= dt;
                if (f.BackIn <= 0 && Rejoin(f)) _live++;
                continue;
            }
            sx += f.X; sy += f.Y; alive++;
        }
        _cx = sx / MathF.Max(1, alive); _cy = sy / MathF.Max(1, alive);
        Centre = Minnow.ToScreen(_pond, _cx, _cy);

        // The shared mood, and how hard the school swims in it (body lengths a second).
        float pace;
        _timer -= dt;
        switch (_mood)
        {
            case Mood.Rush:
                // Straight at the lure, flat out; there (or bored) it starts to mill.
                _tx = _lx; _ty = _ly;
                pace = 9f;
                float dx = _cx - _lx, dy = _cy - _ly;
                if (dx * dx + dy * dy < _u * _u * 0.035f * 0.035f || _timer <= 0)
                {
                    _mood = Mood.Mill;
                    _timer = 6 + 4 * (float)_rng.NextDouble();
                    _orbit = MathF.Atan2(dy, dx);
                }
                break;
            case Mood.Mill:
                // Round and round the spot where the rings were: a swirl, each fish on its own place
                // in a ring that turns about the spot (the ring is handed out below), then it loses interest.
                // Now and then one nips at the surface where the dragonfly touched (they take anything
                // that lands for food), leaving a ring far smaller than the dragonfly's.
                _orbit = (_orbit + 1.6f * dt) % MathF.Tau;
                pace = 3f;
                _nipTimer -= dt;
                if (_nipTimer <= 0)
                {
                    Minnow nipper = _fish[_rng.Next(_fish.Length)];
                    if (!nipper.Gone) { PointF nose = nipper.Nose; _ripples.Add(nose.X, nose.Y, 0.12f); }
                    _nipTimer = 0.3f + 0.6f * (float)_rng.NextDouble();
                }
                if (_timer <= 0) { _mood = Mood.Drift; NewTarget(); }
                break;
            case Mood.Scatter:
                // Flat out, each fish on its own line away from the koi's head (handed out below).
                pace = 12f;
                if (_timer <= 0) { _mood = Mood.Regroup; _timer = 4; }
                break;
            case Mood.Regroup:
                // Back together at the gathering point, briskly, then the ordinary drift.
                _tx = _rgX; _ty = _rgY;
                pace = 4f;
                float rx = _cx - _rgX, ry = _cy - _rgY;
                if (_timer <= 0 || rx * rx + ry * ry < _u * _u * 0.03f * 0.03f) { _mood = Mood.Drift; NewTarget(); }
                break;
            case Mood.Dip:
                // Briskly to the deep spot, sinking on the way (the depth below follows the pace); there, or bored, it grazes.
                pace = 3f;
                float ddx = _cx - _tx, ddy = _cy - _ty;
                if (ddx * ddx + ddy * ddy < _u * _u * 0.05f * 0.05f || _timer <= 0) { _mood = Mood.Graze; _timer = 10 + 15 * (float)_own.NextDouble(); }
                break;
            case Mood.Graze:
                // On the bed: a slow idle about the spot (the fish fidget and keep their room, which is the mill they make
                // round any spot), then the next drift takes it up and away.
                pace = 0.9f;
                if (_timer <= 0) { _mood = Mood.Drift; NewTarget(); }
                break;
            default:
                pace = 1.3f + 1.5f * _z;                           // a school coming up from a dip swims briskly (the rise below is held to the pace)
                float gx = _cx - _tx, gy = _cy - _ty;
                if (_timer <= 0 || gx * gx + gy * gy < _u * _u * 0.05f * 0.05f) NewTarget();
                break;
        }
        // ---- The school's depth: toward what the mood wants, no faster than half the school's swim can hide ----
        // (sink pixels a second at most half the ground pixels the fish are ACTUALLY swimming a second, their mean
        // speed, Light.HoldDepth's share with the nose across the screen; from the mood's pace it was 77 px/s on the
        // first frames of a scatter, before the fish had got up to speed: a slide).
        float meanSpeed = 0; int swimming = 0;
        foreach (Minnow f in _fish) if (!f.Gone) { meanSpeed += f.Speed; swimming++; }
        meanSpeed /= MathF.Max(1, swimming);
        float maxRate = 0.5f * meanSpeed / (Pond.DepthU * _u * Light.Sink);
        _z += Math.Clamp(_zWant - _z, -maxRate * dt, maxRate * dt);
        foreach (Minnow f in _fish)
        {
            if (f.Gone) continue;
            // Its own depth: the school's, capped by the bed under its nose less a small margin, EASED (at the school's
            // rate, or a quarter of the depth a second at least): the bed is uneven (hollows and shoals), and a fish
            // that took the cap outright dropped 0.03 of the depth a frame crossing a hollow's edge, a slide of 800
            // screen pixels a second on the first run. Its own step rule keeps it off a bed shallower than it (TryStep).
            PointF ns = Minnow.ToScreen(_pond, f.X, f.Y);
            int fx = Math.Clamp((int)ns.X, 0, _pond.Width - 1), fy = Math.Clamp((int)ns.Y, 0, _pond.Height - 1);
            float want = MathF.Max(0f, MathF.Min(_z, 0.9f * _pond.Floor[fy * _pond.Width + fx] / 255f - 0.03f));
            // Down (into a hollow) at the school's own rate, which the pace hides; up (the bed rising under it, which
            // the step rule mostly refuses) at that rate or 0.08 a second at least, so a fish on a slope is lifted off it
            // in a few seconds rather than left inside it. (A floor of 0.25 a second both ways had a grazing fish, barely
            // moving, dropping into a hollow at 77 screen pixels a second: a slide.)
            // And no faster than half ITS OWN swim can hide (a fidgeting fish slower than the school's mean lags behind in
            // depth and catches up when it speeds up: a third of the deep fish-frames read as slides on the mean alone).
            float own = 0.5f * (f.Moved / MathF.Max(1e-3f, dt)) / (Pond.DepthU * _u * Light.Sink);   // (the distance it really stepped last frame, not the speed it wanted: refused at a slope it sank on the spot)
            float lim = (want > f.Z ? MathF.Min(maxRate, own) : MathF.Max(MathF.Min(maxRate, own), 0.03f)) * dt;   // (a rise floor of 0.08 bobbed a grazing fish 17 px/s over a hollow's edge)
            f.Z += Math.Clamp(want - f.Z, -lim, lim);
            float tx = _tx, ty = _ty;
            if (_mood == Mood.Mill)
            {
                float a = _orbit + f.RingPlace, r = _u * 0.045f * f.RingRadius;
                tx = _lx + MathF.Cos(a) * r; ty = _ly + MathF.Sin(a) * r;
            }
            else if (_mood == Mood.Scatter)
            {
                // Straight away from the koi's head: a point well beyond the fish on the line from the head through it.
                float ox = f.X - _scX, oy = f.Y - _scY, l = MathF.Max(1e-3f, MathF.Sqrt(ox * ox + oy * oy));
                tx = f.X + ox / l * _u * 0.2f; ty = f.Y + oy / l * _u * 0.2f;
            }
            f.Update(dt, _fish, tx, ty, pace, koiHeads, bolting: _mood == Mood.Scatter);
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Minnow f in _fish) if (!f.Gone) f.Draw(fb);
    }
}

/// <summary>
/// One minnow: a tiny fish seen from above, an olive back with a dark line
/// down it and silver sides that flash when it rolls into a turn, a small
/// forked tail. Drawn along a short bending spine like the koi (Body.cs).
/// </summary>
internal sealed class Minnow : Body
{
    private const int Points = 7;                  // six along the body, one for the tail fin
    private const float FinEnd = 1.25f;

    private readonly Pond _pond;
    private readonly bool[] _where;                 // where it is drawn: open water in view (and the depth test against the stones per paint call, Pond.Near)
    private readonly Random _rng;
    private readonly float _len, _wide, _u;
    private readonly float _jitter;                // this fish swims a little faster or slower than its school

    /// <summary>Its depth, 0 the surface to 1 the pond's full depth: the school's, capped by the bed under it (School.Update sets it). 0 until the school dips.</summary>
    public float Z { get; set; }
    /// <summary>How fast it is swimming, ground pixels a second (the school's depth may change no faster than half of this can hide).</summary>
    public float Speed => _speed;
    /// <summary>How far it actually stepped last frame, ground pixels (its own depth may change no faster than half of THIS can hide: a fish refused at a slope wants a speed but goes nowhere, and sank on the spot).</summary>
    public float Moved => _logMoved;

    /// <summary>Its own place in the swirl round a lure: where on the ring (radians) and how far out (0.5 to 1).</summary>
    public float RingPlace { get; }
    public float RingRadius { get; }
    private float _heading, _speed, _amp, _phase, _flash;
    private float _fidget, _burst;                 // when it next twitches, and how long the twitch lasts
    private readonly PointF[] _trail = new PointF[24];
    private int _trailHead, _trailCount;
    private int _wallSide;                         // 0: free. +1 or -1: following a wall, and which way along it (see Update)
    private float _wallFor;                        // seconds it has been following: with room round it, two seconds is enough whatever the line to the school says
    private float _logRoom, _logMoved;             // the room where it stood and how far it stepped this frame (for the log only)

    /// <summary>The instrument log's columns for this fish: x, y (ground), heading, speed, wall side, room here, moved this frame, flash.</summary>
    public string LogRow() => FormattableString.Invariant($"{X:F1},{Y:F1},{_heading:F3},{_speed:F1},{_wallSide},{_logRoom:F1},{_logMoved:F2},{_flash:F2},{Z:F3}");   // (z last, so readers that count columns from the left are not moved)

    /// <summary>Where its nose is, ground pixels.</summary>
    public float X { get; private set; }
    public float Y { get; private set; }

    /// <summary>Where its nose is on the screen.</summary>
    public PointF Nose => Spine[0];

    /// <summary>Taken by a snake: not drawn, not in the flock, until a newcomer is put in its place (BackIn seconds from the take).</summary>
    public bool Gone { get; set; }
    public float BackIn;

    /// <summary>Starts this fish afresh with its nose at (x, y) ground pixels and its body trailing straight out behind (a newcomer).</summary>
    public void Place(float x, float y, float heading)
    {
        X = x; Y = y; _heading = heading; _speed = _len * 1.3f; _wallSide = 0; _trailCount = 0; _flash = 0;
        for (int i = 12; i >= 0; i--)
            Remember(new PointF(X - MathF.Cos(_heading) * _len * i / 8f, Y - MathF.Sin(_heading) * _len * i / 8f));
        LaySpine();
    }

    public Minnow(Pond pond, bool[] where, Random rng, float x, float y, float heading) : base(Points)
    {
        _pond = pond;
        _where = where;
        _rng = rng;
        _u = pond.U;
        _len = _u * (0.017f + 0.006f * (float)rng.NextDouble());
        _wide = _len * 0.1f;
        _jitter = 0.85f + 0.3f * (float)rng.NextDouble();
        RingPlace = (float)(rng.NextDouble() * MathF.Tau);
        RingRadius = 0.75f + 0.25f * (float)rng.NextDouble();    // a narrow band, so the swirl reads as a ring and not a cloud
        X = x; Y = y;
        _heading = heading;
        _speed = _len * 1.3f;
        _phase = (float)(rng.NextDouble() * MathF.Tau);
        _fidget = (float)rng.NextDouble() * 3;
        for (int i = 0; i < Points; i++) Along[i] = i < 6 ? i / 5f : FinEnd;
        for (int i = 12; i >= 0; i--)
            Remember(new PointF(X - MathF.Cos(_heading) * _len * i / 8f, Y - MathF.Sin(_heading) * _len * i / 8f));
        LaySpine();
    }

    // The fish lives in "ground" coordinates (as if seen straight down); the screen squashes them (see Koi.cs).
    public static (float, float) ToGround(Pond p, float x, float y) => (x, p.Centre.Y + (y - p.Centre.Y) / Ripples.Squash);
    public static PointF ToScreen(Pond p, float gx, float gy) => new(gx, p.Centre.Y + (gy - p.Centre.Y) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    /// <param name="pace">How hard the school is swimming, body lengths a second.</param>
    /// <param name="bolting">The school is scattering from a koi: the pull to the target (straight away from it) counts for more than the schooling, and the fish goes flat out.</param>
    public void Update(float dt, Minnow[] school, float tx, float ty, float pace, PointF[] koiHeads, bool bolting = false)
    {
        // ---- The flocking rules, summed into one "which way" vector ----
        // 0. Keep going: a fish prefers the way it is already swimming, so the pushes below
        //    bend its path rather than flipping it (without this, two fish passing close
        //    shoved each other's heading round every frame and they spun on the spot).
        float sx = MathF.Cos(_heading), sy = MathF.Sin(_heading);
        // 1. Stay with the school: toward its target, harder the further off.
        float dx = tx - X, dy = ty - Y, dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > 1) { float k = MathF.Min(1.5f, dist / (_u * 0.08f)); sx += dx / dist * k; sy += dy / dist * k; }
        // 2. Face the way the neighbours face. 3. Keep a little room.
        float ax = 0, ay = 0; int n = 0;
        float near = _u * 0.08f, close = _len * 1.4f;
        foreach (Minnow o in school)
        {
            if (o == this || o.Gone) continue;
            float ox = o.X - X, oy = o.Y - Y, d2 = ox * ox + oy * oy;
            if (d2 > near * near) continue;
            ax += MathF.Cos(o._heading); ay += MathF.Sin(o._heading); n++;
            if (d2 < close * close && d2 > 0.01f)
            {
                float d = MathF.Sqrt(d2), push = 1.2f * (1 - d / close);
                sx -= ox / d * push; sy -= oy / d * push;
            }
        }
        if (n > 0) { sx += ax / n * 1.2f; sy += ay / n * 1.2f; }
        // 4. A koi underneath: flee, fast.
        bool fleeing = false;
        float scare = _u * 0.11f;
        foreach (PointF k in koiHeads)
        {
            (float kx, float ky) = ToGround(_pond, k.X, k.Y);
            float ox = X - kx, oy = Y - ky, d2 = ox * ox + oy * oy;
            if (d2 > scare * scare || d2 < 0.01f) continue;
            float d = MathF.Sqrt(d2), push = 4f * (1 - d / scare);
            sx += ox / d * push; sy += oy / d * push;
            fleeing = true;
        }
        // 5. The bank (or a rock, or the edge of the view): turn toward more open water.
        PointF here = ToScreen(_pond, X, Y);
        // The SURFACE floor of the stacked room grid, here and in the wall-following below: the land, the stones' caps and
        // the picture's edge. Not the floor of its depth (round forty-three tried it): a dipped school reads the bed it
        // grazes over as a maze of walls a few pixels tall (every bump of the bed within 0.03 of its own), the push toward
        // "more open water" fired all the while, and the schools milled a fifth more (spin36: 496 to 592 fish-seconds).
        // The bed is the STEP'S business (TryStep, on the depth's floor), as it always was for a minnow.
        float roomHere = _pond.RoomAt(here.X, here.Y), wall = _u * 0.04f;
        if (roomHere < wall)
        {
            PointF ow = _pond.OpenWay(here.X, here.Y);
            float l = MathF.Sqrt(ow.X * ow.X + ow.Y * ow.Y);
            if (l > 1e-4f) { float push = 2f * (1 - roomHere / wall); sx += ow.X / l * push; sy += ow.Y / l * push; }
        }

        // ---- Turn toward it, and flash as it rolls ----
        // How sharply depends on how fast it is going: a fish turns on a circle no smaller than
        // about its own length (tighter, and the body curls into a C with its nose off the line
        // it is moving along). Slow fish turn slowly; a rushing fish can whip round at 4 radians a second.
        float want = sx * sx + sy * sy > 1e-6f ? MathF.Atan2(sy, sx) : _heading;
        float most = MathF.Min(4f, MathF.Max(1.2f, _speed / (_len * 0.8f)));
        float turn = Math.Clamp(Wrap(want - _heading), -most * dt, most * dt);
        _heading = Wrap(_heading + turn);
        float rolling = Math.Clamp(MathF.Abs(turn) / dt / 3f, 0, 1);
        _flash += (rolling - _flash) * (1 - MathF.Exp(-dt * 8));

        // ---- Speed: the school's pace, a twitch now and then when idling, flat out from a koi ----
        _fidget -= dt;
        if (_fidget <= 0) { _burst = 0.3f; _fidget = 1 + 3 * (float)_rng.NextDouble(); }
        _burst -= dt;
        float target = _len * pace * _jitter * (_burst > 0 && pace < 2 ? 3f : 1f);
        if (fleeing || bolting) target = _len * 10f;
        _speed += (target - _speed) * (1 - MathF.Exp(-dt * (target > _speed ? 5f : 2.5f)));   // quick off the mark, slower to settle

        // ---- Going round a wall ----
        // A fish that has met a wall (the island's rim, the log, a bank rock) between it and its school FOLLOWS
        // the wall until the school is in clear sight. The soft "turn toward open water" push above is no match
        // for the school's pull when the school is on the far side of the stone: before this rule a fish stood
        // with its nose on the rock, tail beating, trying to swim straight through it (35 such pins in ten
        // minutes on one seed). And a one-frame dodge was not enough either: in the pocket where the beached log
        // meets the bank, "along the wall toward the school" is the dead end, and the pull dragged the fish back
        // into it every frame (eleven fish wiggling there for up to 10 s on seed 5). So the wall-following is a
        // STATE with a remembered side (MINDS.md section 4, the refused step and the committed escape): swim
        // along the wall on that side with a little "away" mixed in, flip the side when the way along is blocked
        // too (a corner), and only when there is open water round the fish AND a clear line to the school's
        // target let the school's pull have it back. A minnow turns in a flick, so the heading is set outright.
        // THE WALL A REFUSED STEP MET IS READ ON THE STEP'S OWN FLOOR (round forty-five). The soft push above reads the
        // surface floor on purpose (the note there), but the step rule refuses on the floor of the fish's depth, and a
        // dipped fish that met a stone's mound, which is open water at the surface, read "room all round, nothing to
        // follow" here, kept its heading on the school's target past the mound and was refused on the spot every frame:
        // a school of five pinned at the island's foot for 55 s at seed 7 the moment the mounds became a boulder's
        // skirt (steep) instead of a hill. So the room round it and the way out, in the wall-following and when the
        // step is refused, are read at WallZ: the same floor TryStep refuses on (0 near the surface, the same as before).
        float wallZ = WallZ(), roomWall = _pond.RoomAt(here.X, here.Y, wallZ);
        if (_wallSide != 0)
        {
            // Let go when there is room round it AND the school's target is in clear sight, or when it has
            // followed for two seconds with room round it whatever the line says (a target behind a stone is
            // never in clear sight: a fish zigzagged on a ridge of open water for 75 s on the stress run).
            _wallFor += dt;
            if (roomWall >= wall && (LineClear(tx, ty) || _wallFor > 2f)) { _wallSide = 0; _wallFor = 0; }
            else FollowWall(here, wallZ);
        }
        float step = _speed * dt, moved = 0;
        if (TryStep(step)) moved = step;
        else
        {
            // THE STEP WAS REFUSED: the nose is on the wall. Pick the side that leads toward the school when the
            // fish was free, flip it when it was already following (the way along is a dead end), and step at once.
            if (_wallSide == 0)
            {
                PointF ow = _pond.OpenWay(here.X, here.Y, wallZ);
                float alongX = -ow.Y, alongY = ow.X;              // the wall runs across the uphill direction
                _wallSide = alongX * (tx - X) + alongY * (ty - Y) < 0 ? -1 : 1;
                _wallFor = 0;
            }
            else _wallSide = -_wallSide;
            FollowWall(here, wallZ);
            if (TryStep(step)) moved = step;
            else
            {
                // Blocked both ways along (a pocket no wider than a step): straight away from the wall.
                PointF ow = _pond.OpenWay(here.X, here.Y);
                float l = MathF.Sqrt(ow.X * ow.X + ow.Y * ow.Y);
                if (l > 1e-4f) { _heading = MathF.Atan2(ow.Y, ow.X); if (TryStep(step)) moved = step; }
            }
        }
        PointF last = _trail[_trailHead];
        if ((last.X - X) * (last.X - X) + (last.Y - Y) * (last.Y - Y) > (_len / 10) * (_len / 10)) Remember(new PointF(X, Y));

        // ---- The tail beat: quick, quicker still when darting, a little wider when working hard.
        // A minnow's body stays nearly straight; only the last third flicks (a wide wave down the
        // whole body made them wriggle like worms). The beat follows the distance actually swum this
        // frame, not the speed the fish wanted: a fish that could not move does not pretend to swim.
        float swim = moved / dt;
        _logRoom = roomHere; _logMoved = moved;
        _phase = (_phase + MathF.Tau * (2.5f + 6f * MathF.Min(1, swim / (_len * 7))) * dt) % MathF.Tau;
        float wantAmp = _len * (0.02f + 0.05f * MathF.Min(1, swim / (_len * 5)));
        _amp += (wantAmp - _amp) * (1 - MathF.Exp(-dt * 4));
        LaySpine();
    }

    /// <summary>
    /// Takes one step along the heading if the water there is open, and says whether it did. "Open" is the
    /// pond's room grid on this fish's own floor (WallZ: the land, and down on the bed any bed shallower than
    /// it may be over): a step may go anywhere with a couple of pixels of water round it, or anywhere with at
    /// least as much room as here (so a fish already against a wall may slide along it, never into it; and a
    /// fish already inside one may step any way out). The room here is read on the same floor: read at the
    /// surface, as the steering's is, a step beside a hump would always be "less room than here" and refused.
    /// </summary>
    private bool TryStep(float step)
    {
        float nx = X + MathF.Cos(_heading) * step, ny = Y + MathF.Sin(_heading) * step;
        PointF next = ToScreen(_pond, nx, ny), here = ToScreen(_pond, X, Y);
        float wallZ = WallZ();
        float roomNext = _pond.RoomAt(next.X, next.Y, wallZ);
        if (roomNext > 2 || roomNext >= _pond.RoomAt(here.X, here.Y, wallZ))
        {
            X = nx; Y = ny; return true;
        }
        return false;
    }

    /// <summary>
    /// The shallowest bed this fish may be over, as the floor of the pond's stacked room grid it reads (round
    /// forty-three). Down on the bed (a dip, deeper than 0.3) the slope of a hollow or a stone's hill is a wall to it,
    /// as to a koi (the snake's rule, round thirty-five), so the school keeps inside its hollow instead of riding up
    /// the wall and being lifted in a jump: the bed is a wall where nine tenths of it, less 0.03, is shallower than
    /// the fish is deep less 0.02 (the dip's old step test, now a floor of the grid, so the school feels the slope
    /// coming instead of the step alone refusing it). Near the surface the bed is no wall at all: a school swims over
    /// the stones' shoulders and the bank's shallows.
    /// </summary>
    private float WallZ() => Z > 0.3f ? (Z + 0.01f) / 0.9f : 0f;

    /// <summary>
    /// Points the fish along the wall it is following: across the "uphill to open water" direction of the
    /// pond's room grid, on its remembered side, with a little "away from the wall" mixed in so it keeps a
    /// hair of water between itself and the stone (straight along, it scraped the rim and was refused).
    /// </summary>
    /// <param name="wallZ">The floor of the room grid to read the wall on: the step's own (WallZ), so a dipped fish follows a mound the surface does not see.</param>
    private void FollowWall(PointF here, float wallZ)
    {
        PointF ow = _pond.OpenWay(here.X, here.Y, wallZ);
        float l = MathF.Sqrt(ow.X * ow.X + ow.Y * ow.Y);
        if (l < 1e-4f) return;
        float awayX = ow.X / l, awayY = ow.Y / l;
        float alongX = -awayY * _wallSide, alongY = awayX * _wallSide;
        float want = MathF.Atan2(alongY + 0.6f * awayY, alongX + 0.6f * awayX);
        // A RIDGE: in a gap between two stones (the formation's caps) the "open way" points at one stone's
        // wall from one side of the gap's midline and at the other from the other side, so a fish crossing
        // the midline by a pixel had its heading flipped right round, every frame, and stood vibrating in
        // the gap for a whole run at full speed ("it just spins in fast little circles forever", 2026-10-27:
        // four fish for 288 s each on the stress run). A heading that would turn more than a right angle
        // from the one it has is the ridge, not a wall: keep the side that carries on along the gap (flip
        // the remembered side, which flips "along" back), and the gap's ends, where the room opens, let
        // the school's pull have it again. A real dead end still flips the side through the refused step.
        if (MathF.Abs(Wrap(want - _heading)) > MathF.PI / 2)
        {
            _wallSide = -_wallSide;
            want = MathF.Atan2(-alongY + 0.6f * awayY, -alongX + 0.6f * awayX);
        }
        _heading = want;
    }

    /// <summary>
    /// Is the way from here toward a point (ground pixels) open water for the next 0.12u, or as far as the
    /// point if nearer? Four samples along the line, each needing a little room (0.012u, about half a minnow).
    /// </summary>
    private bool LineClear(float tx, float ty)
    {
        float dx = tx - X, dy = ty - Y, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1) return true;
        float reach = MathF.Min(d, _u * 0.12f);
        for (int k = 1; k <= 4; k++)
        {
            float f = reach * k / 4;
            PointF p = ToScreen(_pond, X + dx / d * f, Y + dy / d * f);
            if (_pond.RoomAt(p.X, p.Y) < _u * 0.012f) return false;
        }
        return true;
    }

    /// <summary>Lays the spine along the trail behind the head, adds the swimming wave, squashes to the screen (the koi's method, shorter).</summary>
    private void LaySpine()
    {
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(X, Y), b = _trail[slot];
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
            float sway = _amp * (0.03f + s * s * s) * MathF.Sin(MathF.Tau * 0.9f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s);
            PointF l = ToScreen(_pond, cx + nx * hw, cy + ny * hw), r = ToScreen(_pond, cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
        // Depth (round thirty-seven, a school may dip to the bed): the picture shrinks about the nose by the perspective
        // (Light.Scale) and sinks down the screen by its depth (Light.SinkPx), as a koi's does. At the surface this is
        // the identity, so a school that never dips is drawn pixel for pixel as before.
        if (Z > 0.001f)
        {
            float sc = Light.Scale(Z), sink = Light.SinkPx(Z, _u);
            PointF n0 = Spine[0];
            for (int i = 0; i < Points; i++)
            {
                Spine[i] = new PointF(n0.X + (Spine[i].X - n0.X) * sc, n0.Y + (Spine[i].Y - n0.Y) * sc + sink);
                HalfWidth[i] *= sc;
            }
        }
    }

    /// <summary>A blunt little head, widest at the shoulders, tapering to a thin tail stalk, then the fin fanning out.</summary>
    private static float Profile(float s)
    {
        if (s < 0.22f) { float k = (0.22f - s) / 0.25f; return MathF.Sqrt(MathF.Max(0, 1 - k * k)); }
        if (s <= 1f) return 1 - 0.7f * (s - 0.22f) / 0.78f;
        return 0.35f + 0.75f * (s - 1) / (FinEnd - 1);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        float av = MathF.Abs(v);
        if (s > 1f)
        {
            // The tail fin: see-through, forked (the middle ends sooner).
            float end = FinEnd - 0.1f * (1 - av);
            if (s > end) { r = g = b = 0; alpha = 0; return false; }
            r = 150; g = 160; b = 150; alpha = 0.5f;
            return true;
        }
        // Back: a dark line down the middle, olive beside it, silver flanks that go white as it rolls into a turn.
        float stripe = Math.Clamp((0.3f - av) * 8, 0, 1), flank = Math.Clamp((av - 0.55f) * 5, 0, 1);
        r = (int)(120 + (62 - 120) * stripe + (196 - 120) * flank);
        g = (int)(128 + (70 - 128) * stripe + (204 - 128) * flank);
        b = (int)(104 + (60 - 104) * stripe + (200 - 104) * flank);
        if (_flash > 0.01f && flank > 0)
        {
            float f = _flash * flank;
            r += (int)((238 - r) * f); g += (int)((242 - g) * f); b += (int)((238 - b) * f);
        }
        alpha = 0.92f;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        // Just under the surface: a little of the water's colour (0.2), no haze. Down on the bed (a dip): the pond's own
        // haze for its depth (Light.Murk), through the deep stencil, with a softer edge, as a koi down there is drawn.
        float murk = MathF.Max(0.2f, Light.Murk(Z));
        bool[] where = _where;
        Paint(fb, where, _pond.Sun, murk, MathF.Max(0.8f, _u * 0.0007f) * (1 + Z), _pond.Pixels, near: _pond.Near, nearness: Light.Nearness(Z));   // hazed toward the water, not toward a koi it crosses
        // Two tiny dark eyes either side of the head.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.6f, _len * 0.035f * Light.Scale(Z));
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ex / el * HalfWidth[1] * 0.5f - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ey / el * HalfWidth[1] * 0.5f + ex / el * HalfWidth[1] * 0.7f * side);
            Oval(fb, where, c, 0, er, er, 20, 22, 24, 0.9f, murk, _pond.Sun, rays: false, near: _pond.Near, nearness: Light.Nearness(Z));   // (no "water" here: the eye sits on the body and takes its haze from it, as before; with the backdrop it moved a few pixels of every eye at the surface)
        }
    }
}
