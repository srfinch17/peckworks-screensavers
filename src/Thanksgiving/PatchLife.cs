using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The small life of the pumpkin patch (his line: "little mice and squirrels
/// and birds and things running around throughout the patch"): field mice
/// that scurry between the pumpkins, sniff, nibble at them and hide behind
/// them; juncos and sparrows that hop and peck between them, fly up to sit on
/// a pumpkin and look about; and a squirrel or two that bound through, sit
/// up, scratch at the earth and dart behind a pumpkin.
///
/// FEYNMAN VERSION: each critter has a MIND from the shared kit the pond's
/// animals use (Core/Minds): two personality dials rolled once (a timid one
/// hides more, a busy one never sits), ONE doing at a time with a timer, and
/// weighted dice for what to do next when the timer runs out. The mind only
/// chooses a goal; the steps are the kit's ground rules (Ground.Step): the
/// patch is a surface with holes in it (every pumpkin's foot, the horn, the
/// shock), and a step into a hole slides round it. A mouse that a squirrel
/// bounds past bolts (a startle with a cooldown, so it cannot bolt thirty
/// times a second).
///
/// Every critter is stamped against the near ground's depth picture with the
/// nearness of its own feet, so a mouse behind a pumpkin is hidden by it and
/// one in front runs over it: that is the whole of "throughout the patch". A
/// bird on a pumpkin's top is stamped with that pumpkin's nearness plus one.
/// They are drawn far to near.
/// </summary>
internal sealed class PatchLife
{
    private enum Kind { Mouse, Bird, Squirrel }
    private enum Doing { Go, Still, Nibble, Hide, Peck, Fly, Perch, Dig, Bolt }

    private sealed class Critter
    {
        public Kind Kind;
        public int Look;                            // which bird
        public float X, Y;                          // the feet, pixels
        public float Heading;                       // radians, screen (y down)
        public int Dir = 1;
        public float Timid, Busy;                   // the dials
        public Mind<Doing> Mind = new(Doing.Still);
        public Cooldown Startled = new();
        public PointF Goal;
        public float Travelled;                     // for the page
        public float Clock;
        public int Perched = -1;                    // the pumpkin it sits on
        public float FlyFrom, FlyTo, FlyY0, FlyY1, FlyFor;   // a flight's arc
        public int Refused;                         // frames in a row the ground refused
    }

    private struct Sheet { public Sprite[] Pages; public int W, H, Ox, By; }

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly List<Critter> _all = new();
    private readonly float _k, _m, _b;
    private readonly Ground.Solid _ground;
    private Sheet _mouseRun, _mouseStill, _sqRun, _sqLook, _sqDig;
    private Sheet[] _sqSit = new Sheet[4];
    private Sheet[] _bird = new Sheet[2], _birdLook = new Sheet[2], _birdPeck = new Sheet[2], _birdHop = new Sheet[2];
    private Sheet[] _birdFlyKind = new Sheet[2];
    private volatile bool _ready;

    public PatchLife(ThanksgivingScenery s, int count, Random sceneRng)
    {
        _s = s;
        _rng = new Random(sceneRng.Next());         // its own dice
        _k = Math.Max(9f, s.U * 0.055f);
        _m = _k * 0.55f;
        _b = Math.Max(7f, s.U * 0.032f);
        _ground = CanStand;                         // made once: a new delegate every frame is garbage
        int mice = (count + 1) / 2, birds = count / 3, squirrels = Math.Max(0, count - mice - birds);
        float[] timid = Traits.Deal(_rng, Math.Max(1, count)), busy = Traits.Deal(_rng, Math.Max(1, count));
        int n = 0;
        for (int i = 0; i < mice; i++) Add(Kind.Mouse, timid[n], busy[n++]);
        for (int i = 0; i < birds; i++) Add(Kind.Bird, timid[n], busy[n++]);
        for (int i = 0; i < squirrels; i++) Add(Kind.Squirrel, timid[n], busy[n++]);
        Task.Run(() => { try { Build(); } catch { _ready = false; return; } _ready = true; });
    }

    private void Add(Kind kind, float timid, float busy)
    {
        var c = new Critter { Kind = kind, Timid = timid, Busy = busy, Look = _rng.Next(2), Dir = _rng.Next(2) == 0 ? 1 : -1 };
        PointF at = RandomSpot(0, _s.Width, 40);
        c.X = at.X; c.Y = at.Y;
        c.Mind.Begin(Doing.Still, 0.5f + 3f * (float)_rng.NextDouble());
        _all.Add(c);
    }

    // ================================================================ the world

    /// <summary>Can a critter stand here? On the near ground, and not inside a pumpkin's foot, the horn, or the shock's foot.</summary>
    private bool CanStand(float x, float y)
    {
        float u = _s.U;
        if (x < -0.02f * _s.Width || x > 1.02f * _s.Width) return false;
        float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
        if (y < crest + 0.012f * u || y > _s.Height + 0.04f * u) return false;
        foreach (var (at, r) in _s.Pumpkins)
        {
            float foot = at.Y + 0.8f * r;
            if (MathF.Abs(x - at.X) < 1.05f * r && MathF.Abs(y - foot) < 0.35f * r) return false;
        }
        float walk = _s.WalkY(x);
        if (MathF.Abs(x - _s.CornShockFoot.X) < _s.CornShockHalfWidth + 0.01f * u && MathF.Abs(y - walk) < 0.04f * u) return false;
        if (x > _s.CornucopiaMouth.X - _s.CornucopiaLength * 1.15f && x < _s.CornucopiaMouth.X + _s.CornucopiaLength * 0.5f && MathF.Abs(y - _s.CornucopiaMouth.Y) < 0.05f * u) return false;
        return true;
    }

    private PointF RandomSpot(float x0, float x1, int tries)
    {
        float u = _s.U;
        for (int i = 0; i < tries; i++)
        {
            float x = x0 + (x1 - x0) * (float)_rng.NextDouble();
            float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
            float y = crest + 0.012f * u + (_s.Height + 0.04f * u - crest - 0.012f * u) * (float)_rng.NextDouble();
            if (CanStand(x, y)) return new PointF(x, y);
        }
        return new PointF(_s.Width * 0.5f, _s.WalkY(_s.Width * 0.5f));
    }

    /// <summary>A spot just behind a pumpkin (a hair above its foot, within its width): the depth picture then hides most of the critter there.</summary>
    private PointF? BehindPumpkin(float nearX, float within)
    {
        var picks = new List<(PointF At, float R)>();
        foreach (var p in _s.Pumpkins)
            if (MathF.Abs(p.At.X - nearX) < within && p.R > _m * 0.8f) picks.Add(p);
        if (picks.Count == 0) return null;
        var (at, r) = picks[_rng.Next(picks.Count)];
        float foot = at.Y + 0.8f * r;
        return new PointF(at.X + r * 0.5f * (float)(_rng.NextDouble() - 0.5), foot - 0.40f * r);
    }

    // ================================================================ painting (once)

    private Sheet Make(int n, int w, int h, int ox, int by, Func<int, Sprite> page)
    {
        var sh = new Sheet { Pages = new Sprite[n], W = w, H = h, Ox = ox, By = by };
        for (int i = 0; i < n; i++) sh.Pages[i] = page(i);
        return sh;
    }

    private void Build()
    {
        int mw = (int)(1.9f * _m) + 4, mh = (int)(0.95f * _m) + 4, mox = (int)(1.25f * _m) + 2, mby = mh - 2;
        _mouseRun = Make(8, mw, mh, mox, mby, i => Sprite.Paint(mw, mh, g => { g.TranslateTransform(mox, mby); g.ScaleTransform(_m, -_m); MouseArt.Paint(g, i / 8f, false, 0); }));
        _mouseStill = Make(2, mw, mh, mox, mby, i => Sprite.Paint(mw, mh, g => { g.TranslateTransform(mox, mby); g.ScaleTransform(_m, -_m); MouseArt.Paint(g, 0f, false, i + 1); }));

        float k = _k;
        int w = (int)(2.55f * k) + 6, h = (int)(1.9f * k) + 6, ox = (int)(1.75f * k) + 3, by = h - 4;
        _sqRun = Make(16, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Bound(i / 16f, 2.2f), k, w, h, ox, by));
        for (int t = 0; t < 4; t++) { int tl = t; _sqSit[t] = Make(1, w, h, ox, by, _ => SquirrelArt.Page(SquirrelArt.Sit(1f, tl, false, false), k, w, h, ox, by)); }
        _sqLook = Make(1, w, h, ox, by, _ => SquirrelArt.Page(SquirrelArt.Sit(1f, 0, true, false), k, w, h, ox, by));
        _sqDig = Make(8, w, h, ox, by, i => SquirrelArt.Page(SquirrelArt.Dig(i / 8f), k, w, h, ox, by));

        float b = _b;
        int bw = (int)(1.6f * b) + 4, bh = (int)(1.1f * b) + 4, box = (int)(0.9f * b) + 2, bby = bh - 2;
        for (int kind = 0; kind < 2; kind++)
        {
            int kk = kind;
            _bird[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Stand, kk, b, bw, bh, box, bby));
            _birdLook[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Look, kk, b, bw, bh, box, bby));
            _birdPeck[kind] = Make(1, bw, bh, box, bby, _ => BirdArt.Page(BirdArt.Pose.Peck, kk, b, bw, bh, box, bby));
            _birdHop[kind] = Make(3, bw, bh, box, bby, i => BirdArt.Page(i == 0 ? BirdArt.Pose.HopCrouch : i == 1 ? BirdArt.Pose.HopUp : BirdArt.Pose.HopLand, kk, b, bw, bh, box, bby));
            _birdFlyKind[kind] = Make(6, bw, bh, box, bby, i => BirdArt.Page(BirdArt.Pose.Fly, kk, b, bw, bh, box, bby, i));
        }
    }

    // ================================================================ living

    public void Update(float dt)
    {
        if (!_ready) return;
        foreach (Critter c in _all)
        {
            c.Mind.Tick(dt);
            c.Startled.Tick(dt);
            c.Clock += dt;
            // Startles: a mouse near a squirrel (the patch's or the story's) bolts; a bird near anything bigger flies up.
            if (c.Startled.Ready && c.Kind != Kind.Squirrel && c.Mind.Doing != Doing.Bolt && c.Mind.Doing != Doing.Fly)
            {
                foreach (Critter o in _all)
                {
                    if (o == c || o.Kind != Kind.Squirrel || o.Mind.Doing != Doing.Go) continue;
                    float dx = o.X - c.X, dy = o.Y - c.Y;
                    if (dx * dx + dy * dy < (0.10f * _s.U) * (0.10f * _s.U) * (1 + c.Timid)) { Startle(c, new PointF(o.X, o.Y)); break; }
                }
            }
            Act(c, dt);
            if (c.Mind.Over) Decide(c);
        }
    }

    private void Startle(Critter c, PointF from)
    {
        c.Startled.Start(4f + 6f * c.Timid);
        if (c.Kind == Kind.Bird) { BeginFly(c, null); return; }
        PointF? run = Ground.FleeRun(_ground, new PointF(c.X, c.Y), from, _m * 0.5f, 0.25f * _s.U, 0.06f * _s.U);
        c.Goal = run ?? RandomSpot(c.X - 0.3f * _s.U, c.X + 0.3f * _s.U, 20);
        c.Mind.Begin(Doing.Bolt, 2.5f);
        c.Travelled = 0;
    }

    /// <summary>The dice: what next, by kind, dials and what is at hand.</summary>
    /// <summary>
    /// While a story plays on the near ground the patch's own life keeps out of its way (a loose mouse sat at the
    /// squirrel's feet while "the" mouse hid in the jack-o'-lantern): a mouse or squirrel that has to choose goes
    /// behind a pumpkin and stays there, a bird stands still or stays on its perch. Set by the scene each frame.
    /// </summary>
    public bool Lull;

    private void Decide(Critter c)
    {
        float u = _s.U;
        c.Clock = 0;
        if (Lull)
        {
            if (c.Kind == Kind.Bird) c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : _rng.Next(3) == 0 ? Doing.Peck : Doing.Still, 2f + 2f * (float)_rng.NextDouble());
            else
            {
                PointF? g = BehindPumpkin(c.X, 0.4f * u);
                if (g == null) c.Mind.Begin(Doing.Still, 2f);
                else { c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 5f + 3f * (float)_rng.NextDouble()); c.Travelled = 0; }
            }
            return;
        }
        switch (c.Kind)
        {
            case Kind.Mouse:
            {
                // Faces: scurry somewhere, sniff, nibble a pumpkin, hide behind one.
                int face = Dice.Pick(_rng, [0.5f + 0.6f * c.Busy, 0.35f, 0.25f + 0.2f * (1 - c.Timid), 0.15f + 0.5f * c.Timid]);
                if (face == 0) { c.Goal = RandomSpot(c.X - 0.3f * u, c.X + 0.3f * u, 30); c.Mind.Begin(Doing.Go, 6f); c.Travelled = 0; }
                else if (face == 1) c.Mind.Begin(Doing.Still, 0.4f + 1.2f * (float)_rng.NextDouble());
                else if (face == 2) { PointF? g = BehindPumpkin(c.X, 0.3f * u); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = new PointF(g.Value.X, g.Value.Y + 0.5f * _m + 0.3f * _m); c.Mind.Begin(Doing.Nibble, 6f); c.Travelled = 0; }
                else { PointF? g = BehindPumpkin(c.X, 0.35f * u); if (g == null) { c.Mind.Begin(Doing.Still, 0.5f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 8f); c.Travelled = 0; }
                break;
            }
            case Kind.Bird:
            {
                // Faces: hop somewhere, peck, look about, fly to a pumpkin's top, fly to another spot on the ground.
                bool perched = c.Perched >= 0;
                int face = Dice.Pick(_rng, [perched ? 0f : 0.5f + 0.5f * c.Busy, perched ? 0f : 0.45f, 0.35f, perched ? 0.1f : 0.25f + 0.3f * c.Timid, perched ? 0.45f : 0.15f]);
                if (face == 0) { c.Goal = RandomSpot(c.X - 0.15f * u, c.X + 0.15f * u, 30); c.Mind.Begin(Doing.Go, 4f); c.Travelled = 0; }
                else if (face == 1) c.Mind.Begin(Doing.Peck, 0.6f + 1.4f * (float)_rng.NextDouble());
                else if (face == 2) c.Mind.Begin(perched ? Doing.Perch : Doing.Still, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 3) BeginFly(c, PerchNear(c.X, 0.35f * u));
                else BeginFly(c, null);
                break;
            }
            default:
            {
                // Faces: bound somewhere, sit and look, scratch at the earth, dart behind a pumpkin.
                int face = Dice.Pick(_rng, [0.5f + 0.5f * c.Busy, 0.4f, 0.25f, 0.1f + 0.4f * c.Timid]);
                if (face == 0) { c.Goal = RandomSpot(c.X - 0.45f * u, c.X + 0.45f * u, 30); c.Mind.Begin(Doing.Go, 6f); c.Travelled = 0; }
                else if (face == 1) c.Mind.Begin(Doing.Still, 1f + 3f * (float)_rng.NextDouble());
                else if (face == 2) c.Mind.Begin(Doing.Dig, 1f + 1.5f * (float)_rng.NextDouble());
                else { PointF? g = BehindPumpkin(c.X, 0.4f * u); if (g == null) { c.Mind.Begin(Doing.Still, 1f); break; } c.Goal = g.Value; c.Mind.Begin(Doing.Hide, 6f); c.Travelled = 0; }
                break;
            }
        }
    }

    /// <summary>A pumpkin top near x to sit on: its index, or -1.</summary>
    private int PerchNear(float x, float within)
    {
        var picks = new List<int>();
        for (int i = 0; i < _s.Pumpkins.Count; i++)
            if (MathF.Abs(_s.Pumpkins[i].At.X - x) < within && _s.Pumpkins[i].R > _b * 0.9f && _s.Pumpkins[i].At.Y - 1.1f * _s.Pumpkins[i].R > 0) picks.Add(i);
        return picks.Count == 0 ? -1 : picks[_rng.Next(picks.Count)];
    }

    private void BeginFly(Critter c, int? perch)
    {
        float u = _s.U;
        PointF to;
        if (perch.HasValue && perch.Value >= 0)
        {
            var (at, r) = _s.Pumpkins[perch.Value];
            to = new PointF(at.X - 0.1f * r, at.Y - 1.05f * r);          // beside the stem, on the shoulder of the pumpkin
            c.Perched = perch.Value;
        }
        else
        {
            to = RandomSpot(c.X - 0.5f * u, c.X + 0.5f * u, 30);
            c.Perched = -1;
        }
        c.FlyFrom = c.X; c.FlyTo = to.X; c.FlyY0 = c.Y; c.FlyY1 = to.Y;
        c.FlyFor = Math.Max(0.5f, MathF.Abs(to.X - c.X) / (0.45f * u) + 0.3f);
        c.Dir = to.X >= c.X ? 1 : -1;
        c.Mind.Begin(Doing.Fly, c.FlyFor);
    }

    /// <summary>What the doing does this frame: a step toward the goal, a flight along its arc, or nothing.</summary>
    private void Act(Critter c, float dt)
    {
        float u = _s.U;
        switch (c.Mind.Doing)
        {
            case Doing.Go:
            case Doing.Bolt:
            case Doing.Nibble:
            case Doing.Hide:
            {
                float dx = c.Goal.X - c.X, dy = c.Goal.Y - c.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float arrive = c.Kind == Kind.Bird ? 0.2f * _b : 0.25f * _m;
                if (dist < arrive)
                {
                    // Arrived: a scurry ends in a sniff; a nibble and a hide stay put until the timer.
                    if (c.Mind.Doing is Doing.Go or Doing.Bolt) { c.Mind.Begin(Doing.Still, c.Kind == Kind.Bird ? 0.5f : 0.4f + 0.8f * (float)_rng.NextDouble()); c.Clock = 0; }
                    else if (c.Mind.Left > 4f) c.Mind.Left = 2f + 3f * (float)_rng.NextDouble();
                    return;
                }
                float speed = c.Kind switch { Kind.Mouse => c.Mind.Doing == Doing.Bolt ? 0.30f : 0.19f, Kind.Bird => 0.12f, _ => c.Mind.Doing == Doing.Bolt ? 0.32f : 0.24f } * u;
                float want = MathF.Atan2(dy, dx);
                float err = MathF.IEEERemainder(want - c.Heading, MathF.Tau);
                c.Heading += Math.Clamp(err, -6f * dt, 6f * dt);          // it turns at a rate, not in a frame
                float step = speed * dt;
                float x = c.X, y = c.Y, hd = c.Heading;
                var stepped = Ground.Step(_ground, ref x, ref y, ref hd, step, err);
                if (stepped == Ground.Stepped.Refused) { if (++c.Refused > 20) { c.Mind.Begin(Doing.Still, 0.5f); c.Refused = 0; } return; }
                c.Refused = 0;
                c.X = x; c.Y = y; c.Heading = hd;
                c.Travelled += step;
                if (MathF.Abs(MathF.Cos(hd)) > 0.2f) c.Dir = MathF.Cos(hd) > 0 ? 1 : -1;
                if (c.Mind.Doing == Doing.Hide && c.Mind.Over) c.Mind.Begin(Doing.Still, 0.3f);
                break;
            }
            case Doing.Fly:
            {
                float p = Math.Clamp(1f - c.Mind.Left / c.FlyFor, 0f, 1f);
                c.X = c.FlyFrom + (c.FlyTo - c.FlyFrom) * p;
                c.Y = c.FlyY0 + (c.FlyY1 - c.FlyY0) * p - (0.18f * u + 0.1f * MathF.Abs(c.FlyTo - c.FlyFrom)) * 4 * p * (1 - p);
                if (c.Mind.Over) { c.X = c.FlyTo; c.Y = c.FlyY1; c.Mind.Begin(c.Perched >= 0 ? Doing.Perch : Doing.Still, 1f + 3f * (float)_rng.NextDouble()); c.Clock = 0; }
                break;
            }
            case Doing.Perch:
                if (c.Mind.Over && c.Perched >= 0 && _rng.NextDouble() < 0.6) c.Mind.Left = 1f + 3f * (float)_rng.NextDouble();   // often sits a while longer
                break;
        }
    }

    // ================================================================ drawing

    private byte NearnessOf(Critter c) => c.Perched >= 0 && c.Mind.Doing == Doing.Perch
        ? (byte)Math.Min(254, _s.Nearness(_s.Pumpkins[c.Perched].At.Y + 0.8f * _s.Pumpkins[c.Perched].R) + 1)
        : _s.Nearness(c.Y);

    private void Stamp(FrameBuffer fb, in Sheet sh, Sprite page, Critter c, float y)
    {
        bool mirror = c.Dir < 0;
        int left = (int)MathF.Round(c.X) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(y) - sh.By, 1f, null, mirror, _s.NearDepth, NearnessOf(c));
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_ready) return;
        // Far to near.
        _all.Sort((a, b) => a.Y.CompareTo(b.Y));
        foreach (Critter c in _all)
        {
            switch (c.Kind)
            {
                case Kind.Mouse:
                {
                    if (c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide || (c.Mind.Doing == Doing.Nibble && c.Mind.Left > 4f))
                    {
                        float cyc = c.Travelled / (0.8f * _m);
                        Stamp(fb, _mouseRun, _mouseRun.Pages[(int)((cyc - MathF.Floor(cyc)) * 8) % 8], c, c.Y);
                    }
                    else Stamp(fb, _mouseStill, _mouseStill.Pages[(int)(c.Clock * (c.Mind.Doing == Doing.Nibble ? 6 : 12)) % 2], c, c.Y);
                    break;
                }
                case Kind.Bird:
                {
                    int kind = c.Look;
                    if (c.Mind.Doing == Doing.Fly)
                        Stamp(fb, _birdFlyKind[kind], _birdFlyKind[kind].Pages[(int)(c.Clock * 24) % 6], c, c.Y);
                    else if (c.Mind.Doing is Doing.Go or Doing.Bolt)
                    {
                        // Hops: four a second, each a small arc; crouch, up, land.
                        float hop = (c.Clock * 4f) % 1f;
                        int page = hop < 0.2f ? 0 : hop < 0.7f ? 1 : 2;
                        float lift = hop >= 0.2f && hop < 0.7f ? 0.25f * _b * MathF.Sin((hop - 0.2f) / 0.5f * MathF.PI) : 0f;
                        Stamp(fb, _birdHop[kind], _birdHop[kind].Pages[page], c, c.Y - lift);
                    }
                    else if (c.Mind.Doing == Doing.Peck)
                        Stamp(fb, (c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind], ((c.Clock % 0.5f) < 0.3f ? _birdPeck[kind] : _bird[kind]).Pages[0], c, c.Y);
                    else
                    {
                        bool look = ((int)(c.Clock / 1.1f)) % 2 == 1;
                        Stamp(fb, look ? _birdLook[kind] : _bird[kind], (look ? _birdLook[kind] : _bird[kind]).Pages[0], c, c.Y);
                    }
                    break;
                }
                default:
                {
                    if (c.Mind.Doing is Doing.Go or Doing.Bolt or Doing.Hide)
                    {
                        float cyc = c.Travelled / (2.2f * _k);
                        Stamp(fb, _sqRun, _sqRun.Pages[(int)((cyc - MathF.Floor(cyc)) * 16) % 16], c, c.Y);
                    }
                    else if (c.Mind.Doing == Doing.Dig)
                        Stamp(fb, _sqDig, _sqDig.Pages[(int)(c.Clock * 3.2f * 8) % 8], c, c.Y);
                    else
                    {
                        float ft = c.Clock % 1.5f;
                        int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                        bool look = ((int)(c.Clock / 1.3f)) % 3 == 1;
                        Sheet sh = look ? _sqLook : _sqSit[tail];
                        Stamp(fb, sh, sh.Pages[0], c, c.Y);
                    }
                    break;
                }
            }
        }
    }
}
