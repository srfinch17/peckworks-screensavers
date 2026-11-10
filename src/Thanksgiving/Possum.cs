using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// THE POSSUM IN THE RIGHT MAPLE (his ask, 2026-11-09: "what if we put a possum in a tree and sometime it hangs by its tail
/// in view. sometimes it goes down and sometimes it grabs nuts when they're around.").
///
/// FEYNMAN VERSION: one possum lives up in the right inner maple's crown and is out of sight almost all the time. Every minute
/// or two (60 to 200 s) it shows itself, one of two ways:
///
///   HANG: it drops out of the underside of the leaves on its TAIL (the tail is wrapped round a branch up in the leaves, so
///         only the lower part of the tail shows, going up into the foliage), bounces once on the stretch of it, and hangs
///         head down for 8 to 25 s, swaying a touch, hands opening and shutting, its head turning to look at the patch and
///         then at us. Then it curls up (a crunch, the head swinging up the belly side, the hands reaching for its own
///         tail) and is pulled up into the leaves.
///   DOWN: it comes down the trunk head first, steps off onto the ground at the trunk's foot and waddles about the patch
///         on its own business (sniffing, looking about, going round the pumpkins) for 15 to 40 s, then goes back to the
///         foot, climbs the trunk head up and is gone into the leaves.
///
/// THE NUTS: if a loose nut lies near when it is down (or appears while it is), it ambles to it, takes it, sits up on its
/// haunches and eats it (chewing), wipes its face with a paw, and carries on. It never digs one up and never buries one:
/// a thief, not a hoarder (the squirrels are the hoarders; it competes for the loose ones only).
///
/// THE GAG: a Sense.Threat within 0.3 U of it on the ground (the pack's coyotes passing on the far lane, the raccoon if it
/// ever comes near) and it PLAYS DEAD: collapses on its side, stiff, legs in the air, mouth open, tongue out, eyes shut
/// (an X), for 6 to 12 s, peeking once with one eye; then it gets up, shakes, and goes on as if nothing happened. If it
/// was carrying a nut, the nut is dropped where it fell. (A Threat within 0.4 U while it hangs: it is pulled up fast.)
///
/// DOINGS AND WHAT BREAKS THEM
///   Hidden   waits; a story playing or a squirrel on (or running for) the right trunk delays the next showing 2.5 s.
///   Drop, Hang, PullUp   a Threat near ends the Hang early (a fast PullUp); nothing breaks a Drop or a PullUp.
///   Descend, Climb, Mount, Dismount   on the trunk nothing breaks them (a Threat is met at the foot, as PlayDead).
///   Still, Walk, Turn, Take, Eat   a Threat within 0.3 U breaks any of them into the faint (a Cooldown of 45 s after,
///            so a coyote hanging about does not keep it down); a story playing sends it home (the trip's time runs out);
///            a squirrel on the trunk makes it WAIT at the foot, sitting up and looking at the tree, never climbing.
///   Faint, Dead, Rise, Shake   run their course.
///
/// SENSES IT POSTS: Sense.Movement at its feet when it lands and every third of a second while it walks (reach 0.15 U,
/// strength 0.6). WHO LISTENS (a new emitter names its listeners): the patch's mice and birds and squirrels, through
/// PatchLife's attention (a mouse near it bolts, a bird may fly or glance); the trunk's own foot is where a squirrel
/// dens, so a squirrel returning to the tree sees it too. Nothing else on the board reads Movement within 0.15 U of the
/// right maple's foot. It POSTS NOTHING ELSE. It reads Sense.Threat (to play dead) and nothing else.
///
/// DRAWING: the hang through the props stencil (the tail's top vanishes INTO the crown's leaves; the body below is in the
/// open); on the trunk through a band stencil that hides it above the leaves' lower edge, in front of the bark; on the
/// ground through the depth picture with the nearness of its own feet, exactly as the patch's critters (so nearer
/// pumpkins hide it). Scale: the near ground's (a 0.4 m body is 0.048 U, about 1.6 squirrels long; the tail as long
/// again).
///
/// TEST AIDS: PECKWORKS_POSSUM=hang (the drop at 5.0 s), down (the descent at 5.0 s), nut (a nut falls near the foot at
/// 3.5 s and the possum comes down at 5.0 s), dead (comes down at 5.0 s; a Threat is pinned 2.5 s after it lands),
/// often (a showing every 8 to 16 s, for seeds and stress). The log (PECKWORKS_MINDLOG) has a frame line each frame and a
/// line for every doing, for every nut, and "SHARED" whenever it is on the trunk while a squirrel is too.
/// PECKWORKS_POSSUMSHEET=path.png writes the pose sheet (every pose at once, big) when the scene is built.
/// </summary>
internal sealed class Possum
{
    private static readonly int[] SniffSeq = [0, 1, 2, 3, 2, 3, 2, 1];   // the sniff pages' order (a static: an array made in Draw was a per-frame allocation)
    private enum Doing { Hidden, Drop, Hang, PullUp, Descend, Dismount, Still, Walk, Turn, Take, Eat, Faint, Dead, Rise, Shake, Mount, Climb }

    private struct Sheet { public Sprite[] Pg; public int W, H; public float Cx, Cy; }

    private const int WalkPages = 12, SwayPages = 7, Looks = 4;
    private const float DropFor = 0.7f, TurnFor = 0.40f, MountFor = 0.5f;

    private readonly ThanksgivingScenery _s;
    private readonly Noticeboard _board;
    private readonly NutField _nuts;
    private readonly PatchLife _life;
    private readonly StoryDirector _stories;
    private readonly StreamWriter? _log;
    private readonly Random _rng;
    private readonly Ground.Solid _ground;
    private readonly Mind<Doing> _mind = new(Doing.Hidden);
    private readonly Pace _pace;
    private readonly Cooldown _deadRest = new();
    private readonly object _testWho = new();
    private readonly string? _test;
    private readonly float _p, _u, _stride;
    private readonly float _hangX, _hangEdge;          // the hang: where it hangs (the tail's wrap, in the leaves' lower edge) and the edge's y
    private readonly float _trunkTop;                  // the trunk: the y where the bark first shows under the leaves
    private readonly bool[] _trunkOpen;                // the stencil for the trunk: false above the leaves' edge and away from the bole
    private readonly PointF _foot;                     // the ground spot beside the trunk's foot where it steps off and on
    private volatile bool _ready;

    private Sheet _walk, _sniff, _look, _rise, _sitNut, _sitPlain, _wipe, _turn, _faint, _dead, _shake;
    private Sheet _down, _up, _rotPos, _rotNeg;
    private Sheet _hang, _crunch;

    private double _time;
    private float _clock;                              // seconds in this doing
    private float X, Y;                                // the feet on the ground
    private float _cx, _cy;                            // the body's middle, while on the trunk
    private int _dir = -1;                             // -1 faces left, +1 right (the sprites face right, mirrored for left)
    private float _heading;
    private PointF _goal;
    private int _goalKind;                             // 0 spot, 1 nut, 2 home
    private NutField.Nut? _nut;
    private int _nutTries;
    private float _tripLeft;
    private float _speed;
    private float _nextPost, _nextNutLook, _refused;
    private bool _testDone, _testNutDone, _testThreatDone, _fast, _peeked, _mirror;
    private double _landed = -1;
    private float _idleKind, _eatChew, _deadFor;
    private int _hangLook, _hangTarget;
    private float _lookClock, _lookHold, _handsClock, _swayPhase;
    private bool _hands = true;
    private float _turnTo;
    private float _nextShared;
    private int _showings;

    public Possum(ThanksgivingScenery s, Random sceneRng, Noticeboard board, NutField nuts, PatchLife life, StoryDirector stories)
    {
        _s = s; _board = board; _nuts = nuts; _life = life; _stories = stories;
        _log = MindLog.Open();
        _rng = new Random(sceneRng.Next());             // its own dice (the one roll of the scene's it takes, LAST: no older resident's roll moved)
        _ground = CanStand;
        _u = s.U;
        _p = MathF.Max(6.5f, s.U * 0.0218f);            // a body length (nose to rump) is 2.2 p = 0.048 U: 1.6 of a patch squirrel (0.030 U)
        _stride = 1.4f * _p;                            // one waddle cycle's distance
        _pace = new Pace(0.12f * s.U);
        _test = Environment.GetEnvironmentVariable("PECKWORKS_POSSUM");
        string? sheet = Environment.GetEnvironmentVariable("PECKWORKS_POSSUMSHEET");
        if (!string.IsNullOrEmpty(sheet)) PossumArt.WriteSheet(sheet);

        int W = s.Width, H = s.Height;
        var trunk = s.RightTrunk;

        // THE HANG: a column left of the trunk where the leaves' lower edge is clear of the bole and the body below it is in the open.
        float[] tries = [-0.075f, -0.09f, -0.06f, -0.105f, -0.045f, 0.075f, 0.09f];
        _hangX = trunk[1].X + tries[0] * _u; _hangEdge = s.RightMaple.At.Y + 0.70f * s.RightMaple.R;
        float fallbackX = -1, fallbackEdge = 0;
        foreach (float t in tries)
        {
            float x = Math.Clamp(trunk[1].X + t * _u, 2 * _p, W - 2 * _p);
            float edge = LeafEdge(x);
            if (edge < 0) continue;
            bool clear = true;
            for (int y = (int)edge; y < Math.Min(H, (int)(edge + 3.8f * _p)) && clear; y += 2)
                for (int dx = (int)(-0.8f * _p); dx <= (int)(0.8f * _p); dx += 2)
                    if (!s.OpenOfProps[y * W + Math.Clamp((int)x + dx, 0, W - 1)]) { clear = false; break; }
            if (!clear) { if (_hangEdge > edge - 0.1f && fallbackX < 0) { fallbackX = x; fallbackEdge = edge; } continue; }
            _hangX = x; _hangEdge = edge; fallbackX = -1;
            break;
        }
        if (fallbackX >= 0) { _hangX = fallbackX; _hangEdge = fallbackEdge; }   // no column was clear of everything painted late (the fence rail's far side): the first with a real edge

        // THE TRUNK: the first bark under the leaves, going down the bole's middle.
        _trunkTop = BarkTop();
        _trunkOpen = new bool[W * H];
        for (int y = Math.Max(0, (int)_trunkTop); y < Math.Min(H, (int)(trunk[0].Y + 2 * _p)); y++)
        {
            float cx = TrunkX(y), reach = TrunkHalf(y) + 1.6f * _p;
            for (int x = Math.Max(0, (int)(cx - reach)); x <= Math.Min(W - 1, (int)(cx + reach)); x++) _trunkOpen[y * W + x] = true;
        }

        // THE FOOT: the nearest standable spot beside the trunk's foot, the patch side first.
        PointF f0 = trunk[0];
        _foot = new PointF(f0.X - 0.05f * _u, f0.Y);
        bool found = false;
        foreach (int side in new[] { -1, 1 })
            foreach (float dx in new[] { 0.045f, 0.06f, 0.08f, 0.10f })
                foreach (float dy in new[] { 0f, 0.01f, 0.02f, -0.005f })
                {
                    var spot = new PointF(f0.X + side * dx * _u, f0.Y + dy * _u);
                    if (!found && CanStand(spot.X, spot.Y)) { _foot = spot; found = true; }
                }
        X = _foot.X; Y = _foot.Y;

        bool often = _test == "often";
        _mind.Begin(Doing.Hidden, _test is "hang" or "down" or "nut" or "dead" ? Mind<Doing>.Forever : Wait(first: true, often));
        _log?.WriteLine($"{_time,8:F2} possum built: hang at {_hangX:F0},{_hangEdge:F0} trunk top {_trunkTop:F0} foot {_foot.X:F0},{_foot.Y:F0} p {_p:F1}");
        Task.Run(() => { try { Build(); } catch (Exception e) { _log?.WriteLine("possum build failed: " + e.Message); _ready = false; return; } _ready = true; });
    }

    private float Wait(bool first, bool often)
    {
        if (often) return 8f + 8f * (float)_rng.NextDouble();
        return first ? 25f + 60f * (float)_rng.NextDouble() : 60f + 140f * (float)_rng.NextDouble();
    }

    // ================================================================ the world

    /// <summary>Can it stand here? The patch's own test (PatchLife.CanStand): on the near ground, not inside a pumpkin's foot or the corn shock's.</summary>
    private bool CanStand(float x, float y)
    {
        float u = _s.U;
        if (x < -0.02f * _s.Width || x > 1.02f * _s.Width) return false;
        float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
        if (y < crest + 0.012f * u || y > _s.Height - 0.012f * u) return false;
        foreach (var (at, r) in _s.Pumpkins)
        {
            float foot = at.Y + 0.8f * r;
            if (MathF.Abs(x - at.X) < 1.05f * r && MathF.Abs(y - foot) < 0.35f * r) return false;
        }
        float walk = _s.WalkY(x);
        if (MathF.Abs(x - _s.CornShockFoot.X) < _s.CornShockHalfWidth + 0.01f * u && MathF.Abs(y - walk) < 0.04f * u) return false;
        return true;
    }

    private PointF? RandomSpot(float x0, float x1, int tries, float yNear, float yReach, float minDx, PointF from)
    {
        float u = _s.U, xm = (x0 + x1) / 2;
        x0 = Math.Max(x0, 0.30f * _s.Width); x1 = Math.Min(x1, 0.99f * _s.Width);
        if (x1 <= x0) return null;
        for (int i = 0; i < tries; i++)
        {
            float x = x0 + (x1 - x0) * (float)_rng.NextDouble();
            if (MathF.Abs(x - xm) < minDx) continue;
            float crest = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1));
            float y = Math.Clamp(yNear + yReach * (float)(_rng.NextDouble() * 2 - 1), crest + 0.012f * u, _s.Height - 0.012f * u);
            if (!CanStand(x, y)) continue;
            if (!Ground.LineClear(_ground, from, new PointF(x, y), 2f)) continue;
            return new PointF(x, y);
        }
        return null;
    }

    /// <summary>The lower edge of the leaves at column x: the first run of eight open pixels going down from the crown's middle; -1 if none.</summary>
    private float LeafEdge(float x)
    {
        int W = _s.Width, H = _s.Height, xi = Math.Clamp((int)x, 0, W - 1), run = 0;
        for (int y = Math.Max(0, (int)_s.RightMaple.At.Y); y < H; y++)
        {
            if (_s.OpenOfProps[y * W + xi]) { if (++run >= 8) return y - 7; } else run = 0;
        }
        return -1;
    }

    /// <summary>Where the bole's bark first shows going down from the crown's middle (the leaves are bright or red; the bark is dull brown).</summary>
    private float BarkTop()
    {
        int W = _s.Width, H = _s.Height, run = 0;
        for (int y = Math.Max(0, (int)_s.RightMaple.At.Y); y < H; y++)
        {
            int x = Math.Clamp((int)TrunkX(y), 0, W - 1);
            uint px = _s.Pixels[y * W + x];
            int r = (int)((px >> 16) & 0xFF), g = (int)((px >> 8) & 0xFF);
            bool bark = r < 125 && g * 100 >= 62 * r;
            if (bark) { if (++run >= 4) return y - 3; } else run = 0;
        }
        return _s.RightMaple.At.Y + 0.7f * _s.RightMaple.R;
    }

    private float TrunkX(float y)
    {
        var t = _s.RightTrunk;
        float f = Math.Clamp((t[0].Y - y) / MathF.Max(1f, t[0].Y - t[1].Y), 0f, 1f);
        return t[0].X + (t[1].X - t[0].X) * f;
    }

    private float TrunkHalf(float y)
    {
        var t = _s.RightTrunk;
        float f = Math.Clamp((t[0].Y - y) / MathF.Max(1f, t[0].Y - t[1].Y), 0f, 1f);
        return _s.U * (0.026f - 0.010f * f);
    }

    // ================================================================ painting (once)

    private static Sheet Make(int n, int w, int h, float cx, float cy, Func<int, Sprite> page)
    {
        var sh = new Sheet { Pg = new Sprite[n], W = w, H = h, Cx = cx, Cy = cy };
        for (int i = 0; i < n; i++) sh.Pg[i] = page(i);
        return sh;
    }

    private void Build()
    {
        float p = _p;
        // The ground sheets: the tail to the left (3.1 units), the nose and a swinging head to the right, the sitting body above; the body's middle at (cx, cy).
        int gw = (int)(5.3f * p) + 4, gh = (int)(3.4f * p) + 4;
        float gcx = 3.5f * p + 2, gcy = 2.55f * p + 2;
        Sheet G(int n, Func<int, PossumArt.Pose> pose) => Make(n, gw, gh, gcx, gcy, i => PossumArt.Page(pose(i), p, gw, gh, gcx, gcy));
        _walk = G(WalkPages, i => PossumArt.Walk(i / (float)WalkPages));
        _sniff = G(4, i => PossumArt.Sniff(i / 3f));
        _look = G(1, _ => PossumArt.LookUp());
        _rise = G(2, i => PossumArt.Sit((i + 1) / 3f, 0f, false));
        _sitNut = G(3, i => PossumArt.Sit(1f, i / 2f, true));
        _sitPlain = G(1, _ => PossumArt.Sit(1f, 0f, false));
        _wipe = G(3, i => PossumArt.Sit(1f, 0f, false, 0.3f + 0.35f * i));
        _turn = G(3, i => PossumArt.TurnPose((i + 1) / 3f));
        _faint = G(2, i => PossumArt.Faint(0.5f + 0.5f * i));
        _dead = G(3, i => i < 2 ? PossumArt.Dead(i, false) : PossumArt.Dead(0f, true));
        _shake = G(2, i => PossumArt.Shake(i == 0 ? -1f : 1f));

        // The trunk: the walk turned a quarter (head down, head up), and the pages between the level and the climb.
        int vw = (int)(3.2f * p) + 4, vh = (int)(5.4f * p) + 4;
        float vcx = vw / 2f;
        _down = Make(WalkPages, vw, vh, vcx, 3.8f * p, i => PossumArt.Page(PossumArt.Walk(i / (float)WalkPages), p, vw, vh, vcx, 3.8f * p, 90f));
        _up = Make(WalkPages, vw, vh, vcx, 1.8f * p, i => PossumArt.Page(PossumArt.Walk(i / (float)WalkPages), p, vw, vh, vcx, 1.8f * p, -90f));
        int rs = (int)(7.2f * p) + 4;
        float rc = rs / 2f;
        float[] pos = [68f, 45f, 23f];
        _rotPos = Make(3, rs, rs, rc, rc, i => PossumArt.Page(PossumArt.Walk(0f), p, rs, rs, rc, rc, pos[i]));
        _rotNeg = Make(3, rs, rs, rc, rc, i => PossumArt.Page(PossumArt.Walk(0f), p, rs, rs, rc, rc, -pos[2 - i]));

        // The hang: the wrap point is the sheet's (cx, cy); seven sways, four looks, hands shut or open.
        int hw = (int)(4.6f * p) + 4, hh = (int)(6.0f * p) + 4;
        float hcx = hw / 2f, hcy = 1.4f * p;
        _hang = Make(SwayPages * Looks * 2, hw, hh, hcx, hcy, i =>
        {
            int sway = i / (Looks * 2), look = (i / 2) % Looks, hands = i % 2;
            return PossumArt.HangPage(PossumArt.Hang(look, hands), p, hw, hh, hcx, hcy, (sway - 3) * 1.8f);
        });
        _crunch = Make(4, hw, hh, hcx, hcy, i => PossumArt.HangPage(PossumArt.Hang(0, 0f, 35f + 35f * i), p, hw, hh, hcx, hcy, 0f));
    }

    // ================================================================ living

    private void Begin(Doing d, float seconds)
    {
        _mind.Begin(d, seconds); _clock = 0;
        _log?.WriteLine($"{_time,8:F2} possum -> {d} at {X:F0},{Y:F0}");
    }

    private bool Quiet => !_stories.Playing && !_life.TrunkBusy(1);

    public void Update(float dt)
    {
        _time += dt;
        if (!_ready) return;
        _mind.Tick(dt); _deadRest.Tick(dt); _clock += dt;
        if (_log != null && _mind.Doing != Doing.Hidden)
            _log.WriteLine($"{_time,8:F3} possum frame x {(OnTrunk ? _cx : InHang ? _hangX : X):F1} y {(OnTrunk ? _cy : InHang ? _hangEdge : Y):F1} v {_pace.Speed:F2} {_mind.Doing} d{_dir}");

        if (OnTrunk && _time >= _nextShared && _life.TrunkBusy(1)) { _nextShared = (float)_time + 0.5f; _log?.WriteLine($"{_time,8:F2} possum SHARED trunk with a squirrel ({_mind.Doing})"); }

        if (_test == "nut" && !_testNutDone && _time >= 3.5)
        {
            _testNutDone = true;
            var tw = new PointF(_foot.X - 0.07f * _u, _s.RightMaple.At.Y + 0.4f * _s.RightMaple.R);
            _nuts.Drop(tw, 0f);
            _log?.WriteLine($"{_time,8:F2} possum test: a nut dropped from {tw.X:F0},{tw.Y:F0}");
        }

        switch (_mind.Doing)
        {
            case Doing.Hidden: UpdateHidden(); break;
            case Doing.Drop: if (_mind.Over) { Begin(Doing.Hang, 8f + 17f * (float)_rng.NextDouble()); StartHang(); } break;
            case Doing.Hang: UpdateHang(dt); break;
            case Doing.PullUp:
                if (_mind.Over)
                {
                    bool nut = _nuts.NearestLoose(_foot, 0.25f * _u) != null;
                    Begin(Doing.Hidden, nut ? 1.0f : Wait(false, _test == "often")); _showings++;
                    _wantDown = nut;
                }
                break;
            case Doing.Descend: UpdateDescend(dt); break;
            case Doing.Dismount: UpdateDismount(dt); break;
            case Doing.Mount: UpdateMount(dt); break;
            case Doing.Climb: UpdateClimb(dt); break;
            case Doing.Faint: if (_mind.Over) { _deadFor = 6f + 6f * (float)_rng.NextDouble(); _peeked = false; Begin(Doing.Dead, _deadFor); } break;
            case Doing.Dead: UpdateDead(); break;
            case Doing.Rise: if (_mind.Over) { Begin(Doing.Shake, 0.7f); } break;
            case Doing.Shake: if (_mind.Over) { _tripLeft = MathF.Min(_tripLeft, 8f); Begin(Doing.Still, 1.5f); _idleKind = 0; } break;
            default: UpdateGround(dt); break;
        }
    }

    private bool _wantDown, _moved;
    private bool OnTrunk => _mind.Doing is Doing.Descend or Doing.Climb or Doing.Mount or Doing.Dismount;
    private bool InHang => _mind.Doing is Doing.Drop or Doing.Hang or Doing.PullUp;

    // ---------------------------------------------------------------- hidden: the choice

    private void UpdateHidden()
    {
        bool test = _test is "hang" or "down" or "nut" or "dead";
        if (test && !_testDone && _time >= 5.0)
        {
            _testDone = true;
            if (_test == "hang") StartDrop(); else StartDescend();
            return;
        }
        if (test) { if (_testDone && _mind.Left == Mind<Doing>.Forever) _mind.Begin(Doing.Hidden, 60f + 80f * (float)_rng.NextDouble()); }
        if (!_mind.Over || test && !_testDone) return;
        // Time to show itself, if the tree is quiet: a story or a squirrel on the trunk puts it off a moment.
        if (!Quiet) { _mind.Left = 2.5f; return; }
        bool nutNear = _wantDown || _nuts.NearestLoose(_foot, 0.25f * _u) != null;
        _wantDown = false;
        if (nutNear || _rng.NextDouble() < 0.5) StartDescend(); else StartDrop();
    }

    // ---------------------------------------------------------------- hang

    private void StartDrop()
    {
        _mirror = _rng.Next(2) == 0;
        _hangLook = 0; _hangTarget = 0; _lookClock = 0; _lookHold = 2.0f + 1.5f * (float)_rng.NextDouble();
        _hands = true; _handsClock = 0; _swayPhase = (float)(_rng.NextDouble() * Math.Tau); _fast = false;
        Begin(Doing.Drop, DropFor);
    }

    private void StartHang() { _clock = 0; }

    private void UpdateHang(float dt)
    {
        // Looking about: hold a look, then move to the next through the pages between (never a jump from the side to face on).
        _lookClock += dt;
        if (_hangLook == _hangTarget && _lookClock >= _lookHold)
        {
            // at the patch (3) or at us (2) or straight down (0); never the same one twice
            int next = _rng.Next(3) switch { 0 => 3, 1 => 2, _ => 0 };
            if (next == _hangTarget) next = _hangTarget == 0 ? 2 : 0;
            _hangTarget = next; _lookClock = 0; _lookHold = 2.2f + 2.3f * (float)_rng.NextDouble();
        }
        else if (_hangLook != _hangTarget && _lookClock >= 0.14f)
        {
            _lookClock = 0;
            _hangLook = _hangLook switch
            {
                0 => _hangTarget == 3 ? 3 : 1,
                1 => _hangTarget == 2 ? 2 : 0,
                2 => 1,
                _ => 0,                                       // from the side look back through straight down
            };
        }
        _handsClock += dt;
        if (_handsClock >= 1.1f + 0.9f * (float)_rng.NextDouble()) { _hands = !_hands; _handsClock = 0; }
        _swayPhase += dt * MathF.Tau / 4.3f;
        if (_swayPhase > MathF.Tau) _swayPhase -= MathF.Tau;

        // A Threat near ends it at once, pulled up fast.
        var body = new PointF(_hangX, _hangEdge + 2.3f * _p);
        if (_board.Nearest(Sense.Threat, body, 0.4f * _u) is Notice) { _fast = true; _log?.WriteLine($"{_time,8:F2} possum: a threat, pulled up fast"); BeginPullUp(); return; }
        if (_mind.Over) BeginPullUp();
    }

    private void BeginPullUp()
    {
        float crunch = _fast ? 0.28f : 0.56f, rise = _fast ? 0.5f : 0.95f;
        _hangLook = 0;
        Begin(Doing.PullUp, crunch + rise);
    }

    // ---------------------------------------------------------------- the trunk

    private void StartDescend()
    {
        _dir = -1; _cx = TrunkX(_trunkTop); _cy = _trunkTop - 1.4f * _p;
        _pace.Halt(); _pace.Walked = 0f; _tripLeft = 15f + 25f * (float)_rng.NextDouble();
        _goalKind = 0; _nutTries = 0; _nut = null;
        Begin(Doing.Descend, Mind<Doing>.Forever);
    }

    private void UpdateDescend(float dt)
    {
        float v = 0.075f * _u;
        _cy += v * dt; _pace.Walked += v * dt;
        _cx = TrunkX(_cy);
        if (_cy >= _foot.Y - 1.45f * _p)
        {
            _dir = -1;
            Begin(Doing.Dismount, MountFor);
        }
    }

    private void UpdateDismount(float dt)
    {
        if (!_mind.Over) return;
        X = _foot.X; Y = _foot.Y; _heading = MathF.PI; _dir = -1;
        _landed = _time; _tripLeft = MathF.Max(_tripLeft, 12f);
        _board.Post(Sense.Movement, new PointF(X, Y), 0.15f * _u, 0.6f, this);
        _log?.WriteLine($"{_time,8:F2} possum lands at {X:F0},{Y:F0}");
        Begin(Doing.Still, 1.0f + 1.0f * (float)_rng.NextDouble()); _idleKind = 2;
    }

    private void StartMount()
    {
        _dir = 1; _heading = 0f;
        _pace.Halt();
        Begin(Doing.Mount, MountFor);
    }

    private void UpdateMount(float dt)
    {
        if (!_mind.Over) return;
        _cy = _foot.Y - 1.6f * _p; _cx = TrunkX(_cy); _pace.Walked = 0f;
        Begin(Doing.Climb, Mind<Doing>.Forever);
    }

    private void UpdateClimb(float dt)
    {
        float v = 0.09f * _u;
        _cy -= v * dt; _pace.Walked += v * dt; _cx = TrunkX(_cy);
        if (_cy + 3.3f * _p < _trunkTop)
        {
            _log?.WriteLine($"{_time,8:F2} possum is up into the leaves");
            Begin(Doing.Hidden, Wait(false, _test == "often")); _showings++;
        }
    }

    // ---------------------------------------------------------------- the ground

    private bool OnGround => _mind.Doing is Doing.Still or Doing.Walk or Doing.Turn or Doing.Take or Doing.Eat;

    private void UpdateGround(float dt)
    {
        var feet = new PointF(X, Y);

        // THE GAG: a Threat close by, and it plays dead (a rest of 45 s after, so a coyote hanging about does not keep it down).
        if (OnGround && _deadRest.Ready && _board.Nearest(Sense.Threat, feet, 0.3f * _u) is Notice th)
        {
            if (_nut != null && _nut.State == NutField.State.Carried) _nuts.DropAt(_nut, X + _dir * 0.7f * _p, Y);
            _nut = null; _pace.Halt(); _deadRest.Start(45f);
            _log?.WriteLine($"{_time,8:F2} possum PLAYS DEAD: a threat at {th.At.X:F0},{th.At.Y:F0}, {Dist(th.At, feet) / _u:F2} U away");
            Begin(Doing.Faint, 0.30f);
            return;
        }
        if (_test == "dead" && !_testThreatDone && _landed > 0 && _time >= _landed + 2.5)
        {
            _testThreatDone = true;
            _board.Post(Sense.Threat, new PointF(X - 0.12f * _u, Y), 0.3f * _u, 1f, _testWho);
        }
        // a story starting sends it home: its trip's time runs out
        if (_stories.Playing && _tripLeft > 0f) _tripLeft = 0f;
        _tripLeft -= dt;

        switch (_mind.Doing)
        {
            case Doing.Still: UpdateStill(dt); break;
            case Doing.Walk: UpdateWalk(dt); break;
            case Doing.Turn: UpdateTurn(); break;
            case Doing.Take:
                if (_nut != null && _nut.State == NutField.State.Loose && _clock >= 0.3f) { _nuts.Take(_nut); _log?.WriteLine($"{_time,8:F2} possum takes a nut at {_nut.X:F0},{_nut.Y:F0}"); }
                if (_mind.Over)
                {
                    if (_nut != null && _nut.State == NutField.State.Carried) { _eatChew = 5f + 3f * (float)_rng.NextDouble(); Begin(Doing.Eat, 0.5f + _eatChew + 1.3f + 0.4f); }
                    else { _nut = null; Begin(Doing.Still, 1.0f); _idleKind = 2; }     // someone else got it first: a look round
                }
                break;
            case Doing.Eat: UpdateEat(); break;
        }
    }

    private static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return MathF.Sqrt(dx * dx + dy * dy); }

    private void UpdateStill(float dt)
    {
        if (!_mind.Over) { WatchNuts(); return; }
        // At the foot with a squirrel on the trunk: wait, looking up at the tree.
        if (_goalKind == 2 && Dist(new PointF(X, Y), _foot) < 2.4f * _p)
        {
            if (!Quiet && !_stories.Playing) { Begin(Doing.Still, 0.5f); _idleKind = 2; return; }
            if (_stories.Playing) { Begin(Doing.Still, 1.0f); _idleKind = 2; return; }
            if (_dir < 0) { BeginTurn(1); return; }
            StartMount(); return;
        }
        // The next thing: a nut near first; then home when the trip is spent; else another spot to amble to.
        var feet = new PointF(X, Y);
        if (_nuts.NearestLoose(feet, 0.3f * _u) is NutField.Nut n && _nutTries < 3 && !_stories.Playing && _tripLeft > -20f)
        {
            _nut = n; _goalKind = 1; _goal = new PointF(n.X, n.Y); _nutTries++;
            _log?.WriteLine($"{_time,8:F2} possum sees a nut at {n.X:F0},{n.Y:F0}");
            BeginWalk(); return;
        }
        if (_tripLeft <= 0f || _goalKind == 2)
        {
            _goalKind = 2; _goal = _foot;
            if (Dist(feet, _foot) < 2.4f * _p) { Begin(Doing.Still, 0.3f); return; }
            BeginWalk(); return;
        }
        PointF? spot = RandomSpot(X - 0.35f * _u, X + 0.35f * _u, 30, Y, 0.035f * _u, 0.07f * _u, feet);
        if (spot == null) { Begin(Doing.Still, 1.5f); return; }
        _goalKind = 0; _goal = spot.Value; _nutTries = 0;
        BeginWalk();
    }

    /// <summary>While standing, a loose nut that lands near is noticed at once (a thud on the board is the Food sense; it looks, then goes).</summary>
    private void WatchNuts()
    {
        if (_mind.Left > 1.0f && _nuts.NearestLoose(new PointF(X, Y), 0.3f * _u) != null && !_stories.Playing) _mind.Left = 0.3f;
    }

    private void BeginWalk()
    {
        float dx = _goal.X - X;
        int want = dx >= 0 ? 1 : -1;
        _speed = _u * (0.040f + 0.012f * (float)_rng.NextDouble());
        if (want != _dir) { BeginTurn(want); return; }
        _heading = MathF.Atan2(_goal.Y - Y, dx);
        _pace.Want = _speed; _refused = 0; _moved = false;
        _pace.Walked = MathF.Floor(_pace.Walked / _stride) * _stride;     // start on the rest page: the planted feet the stop left stay where they are
        Begin(Doing.Walk, 14f);
    }

    private void BeginTurn(int want)
    {
        _turnTo = want; _pace.Halt();
        Begin(Doing.Turn, TurnFor);
    }

    private void UpdateTurn()
    {
        if (_clock >= TurnFor * 0.5f && _dir != (int)_turnTo) _dir = (int)_turnTo;   // the flip is made at the end-on page, which is its own mirror
        if (!_mind.Over) return;
        if (_goalKind == 2 && Dist(new PointF(X, Y), _foot) < 2.4f * _p) { Begin(Doing.Still, 0.2f); return; }
        BeginWalk();
    }

    private void UpdateWalk(float dt)
    {
        var feet = new PointF(X, Y);
        float dx = _goal.X - X, dy = _goal.Y - Y, remaining = MathF.Sqrt(dx * dx + dy * dy);
        // a nut that appears on the way, or that someone else takes
        if (_goalKind == 1 && (_nut == null || _nut.State != NutField.State.Loose))
        {
            _log?.WriteLine($"{_time,8:F2} possum: the nut is gone");
            _nut = null; _goalKind = 0; _pace.Halt(); Begin(Doing.Still, 1.0f); _idleKind = 2; return;
        }
        if (_goalKind == 0 && _time >= _nextNutLook)
        {
            _nextNutLook = (float)_time + 1f;
            if (_nuts.NearestLoose(feet, 0.3f * _u) != null && !_stories.Playing) { _pace.Halt(); Begin(Doing.Still, 0.2f); return; }
        }
        if (_stories.Playing && _goalKind != 2) { _tripLeft = 0; _goalKind = 2; _goal = _foot; dx = _goal.X - X; dy = _goal.Y - Y; remaining = MathF.Sqrt(dx * dx + dy * dy); if ((dx >= 0 ? 1 : -1) != _dir) { _pace.Halt(); BeginTurn(dx >= 0 ? 1 : -1); return; } }

        // slow in: brake to land on the rest page, so the planted feet stay planted
        if (!_pace.Braking)
        {
            float d = _pace.ToRestPage(_stride);
            if (remaining <= d + 0.3f * _stride)
            {
                if (_pace.Speed > 0f) _pace.Brake(d);
                else { Arrive(); return; }                  // a goal at its feet: it is there
            }
        }
        else _pace.Want = 0f;
        float step = _pace.Tick(dt);
        if (step > 0f) _moved = true;
        float target = MathF.Atan2(dy, dx);
        _heading = Turn.Toward(_heading, target, 2.5f, dt);
        float x = X, y = Y, h = _heading;
        var st = Ground.StepRound(_ground, ref x, ref y, ref h, step, Turn.Error(_heading, target));
        if (st == Ground.Stepped.Refused) { if (step > 0 && ++_refused > 10) { _pace.Halt(); _goalKind = _goalKind == 2 ? 2 : 0; Begin(Doing.Still, 1.0f); _log?.WriteLine($"{_time,8:F2} possum refused at {X:F0},{Y:F0}"); } }
        else { X = x; Y = y; _heading = h; _refused = 0; }

        if (_time >= _nextPost) { _board.Post(Sense.Movement, new PointF(X, Y), 0.15f * _u, 0.6f, this); _nextPost = (float)_time + 0.33f; }

        if (_moved && _pace.Still) { Arrive(); return; }
        if (_mind.Over) { _pace.Halt(); Begin(Doing.Still, 1.0f); }
    }

    private void Arrive()
    {
        _pace.Halt();
        var feet = new PointF(X, Y);
        if (_goalKind == 1 && _nut != null && _nut.State == NutField.State.Loose)
        {
            if (MathF.Abs(_nut.X - X) <= 1.3f * _p && MathF.Abs(_nut.Y - Y) <= 0.06f * _u)
            {
                if (_nut.X > X + 0.1f * _p) _dir = 1; else if (_nut.X < X - 0.1f * _p) _dir = -1;
                Begin(Doing.Take, 0.6f);
                return;
            }
            if (_nutTries < 3) { _goal = new PointF(_nut.X, _nut.Y); _nutTries++; BeginWalk(); return; }   // stopped a little short: step on
            _nut = null; _goalKind = 0;
        }
        Begin(Doing.Still, 1.2f + 2.2f * (float)_rng.NextDouble());
        _idleKind = (float)_rng.NextDouble() * 3f;
    }

    private void UpdateEat()
    {
        float c = _clock, chewEnd = 0.5f + _eatChew, wipeEnd = chewEnd + 1.3f;
        if (c >= chewEnd && _nut != null && _nut.State == NutField.State.Carried)
        {
            _nuts.Eat(_nut); _log?.WriteLine($"{_time,8:F2} possum has eaten the nut"); _nut = null;
        }
        if (_mind.Over) { _goalKind = 0; _nutTries = 0; Begin(Doing.Still, 0.8f); _idleKind = 0; }
    }

    private void UpdateDead()
    {
        if (!_peeked && _clock >= _deadFor * 0.55f) _peeked = true;
        if (_mind.Over)
        {
            // still a threat close by, and it has not been down long: a little longer (once)
            if (_deadFor < 13f && _board.Nearest(Sense.Threat, new PointF(X, Y), 0.4f * _u) is Notice) { _deadFor += 3f; _mind.Left = 3f; return; }
            _log?.WriteLine($"{_time,8:F2} possum gets up after {_clock:F1} s");
            Begin(Doing.Rise, 0.3f);
        }
    }

    // ================================================================ drawing

    private void Stamp(FrameBuffer fb, in Sheet sh, int page, float px, float py, bool mirror, bool[]? stencil, byte[]? depth, int nearness)
    {
        int left = (int)MathF.Round(px - (mirror ? sh.W - sh.Cx : sh.Cx));
        int top = (int)MathF.Round(py - sh.Cy);
        sh.Pg[Math.Clamp(page, 0, sh.Pg.Length - 1)].Draw(fb, left, top, 1f, stencil, mirror, depth, nearness);
    }

    /// <summary>One stamp on the near ground: through the depth picture with the nearness of its own feet.</summary>
    private void Ground1(FrameBuffer fb, in Sheet sh, int page)
    {
        Stamp(fb, sh, page, X, Y + PossumArt.PivotY * _p, _dir < 0, null, _s.NearDepth, _s.Nearness(Y));
    }

    public void Draw(FrameBuffer fb)
    {
        if (!_ready || _mind.Doing == Doing.Hidden) return;
        float p = _p;
        switch (_mind.Doing)
        {
            case Doing.Drop:
            case Doing.Hang:
            case Doing.PullUp:
                DrawHang(fb);
                break;
            case Doing.Descend:
                Stamp(fb, _down, _pace.Page(_stride, WalkPages), _cx, _cy, _dir < 0, _trunkOpen, null, 255);
                break;
            case Doing.Climb:
                Stamp(fb, _up, _pace.Page(_stride, WalkPages), _cx, _cy, _dir < 0, _trunkOpen, null, 255);
                break;
            case Doing.Dismount:
            {
                // From the trunk's face (head down) to the level: the quarter-turn pages, the body's middle moving to the ground spot.
                float k = Math.Clamp(_clock / MountFor, 0f, 1f);
                float sx = _cx + (_foot.X - _cx) * k * k, sy = _cy + ((_foot.Y + PossumArt.PivotY * p) - _cy) * k;
                if (k < 0.25f) Stamp(fb, _down, _pace.Page(_stride, WalkPages), sx, sy, true, _trunkOpen, null, 255);
                else if (k < 1f) Stamp(fb, _rotPos, Math.Min(2, (int)((k - 0.25f) / 0.75f * 3f)), sx, sy, true, null, null, 255);
                else Ground1(fb, _walk, 0);
                break;
            }
            case Doing.Mount:
            {
                float k = Math.Clamp(_clock / MountFor, 0f, 1f);
                float ty = _foot.Y - 1.6f * p;
                float sx = X + (TrunkX(ty) - X) * k * k, sy = (Y + PossumArt.PivotY * p) + (ty - (Y + PossumArt.PivotY * p)) * k;
                if (k < 0.75f) Stamp(fb, _rotNeg, Math.Min(2, (int)(k / 0.75f * 3f)), sx, sy, false, null, null, 255);
                else Stamp(fb, _up, 0, sx, sy, false, _trunkOpen, null, 255);
                break;
            }
            case Doing.Walk:
                Ground1(fb, _walk, _pace.Page(_stride, WalkPages));
                break;
            case Doing.Turn:
            {
                float q = Math.Clamp(_clock / TurnFor, 0f, 1f);
                int pg = q < 0.5f ? Math.Min(2, (int)(q * 2f * 3f)) : Math.Min(2, (int)((1f - q) * 2f * 3f));
                Ground1(fb, _turn, pg);
                break;
            }
            case Doing.Still:
            {
                // standing: mostly still, now and then the nose to the ground (sniffing), or the head up to look about
                if (_idleKind < 1f) Ground1(fb, _walk, 0);
                else if (_idleKind < 2f)
                {
                    Ground1(fb, _sniff, SniffSeq[(int)(_clock * 3.2f) % SniffSeq.Length]);
                }
                else Ground1(fb, _look, 0);
                break;
            }
            case Doing.Take:
                Ground1(fb, _sniff, _clock < 0.2f ? 2 : 3);
                break;
            case Doing.Eat:
            {
                float c = _clock, chewEnd = 0.5f + _eatChew, wipeEnd = chewEnd + 1.3f;
                if (c < 0.5f) Ground1(fb, _rise, Math.Min(1, (int)(c / 0.25f)));                                       // up onto the haunches
                else if (c < chewEnd)
                {
                    int[] seq = [0, 1, 2, 1];                                                                         // the nut dips to the mouth and the jaw works
                    Ground1(fb, _sitNut, seq[(int)(c * 7f) % seq.Length]);
                }
                else if (c < wipeEnd) Ground1(fb, _wipe, (int)((c - chewEnd) * 5f) % 3);                              // wiping its face with a paw
                else if (c < wipeEnd + 0.15f) Ground1(fb, _sitPlain, 0);
                else Ground1(fb, _rise, 1 - Math.Min(1, (int)((c - wipeEnd - 0.15f) / 0.12f)));                       // back down
                break;
            }
            case Doing.Faint:
                Ground1(fb, _faint, _clock < 0.15f ? 0 : 1);
                break;
            case Doing.Dead:
            {
                int pg = _clock >= _deadFor * 0.55f && _clock < _deadFor * 0.55f + 0.7f ? 2 : ((int)(_clock * 0.8f) % 2);
                Ground1(fb, _dead, pg);
                break;
            }
            case Doing.Rise:
                Ground1(fb, _faint, _clock < 0.15f ? 1 : 0);
                break;
            case Doing.Shake:
                Ground1(fb, _shake, (int)(_clock / 0.09f) % 2);
                break;
        }
    }

    private void DrawHang(FrameBuffer fb)
    {
        float p = _p, off;
        int page;
        if (_mind.Doing == Doing.Drop)
        {
            float s = Math.Clamp(_clock / DropFor, 0f, 1f);
            off = -4.2f * p * (1f - s * s);                                              // falling: faster all the way down, till the tail takes its weight
            page = HangIndex(3, 0, 1);
        }
        else if (_mind.Doing == Doing.Hang)
        {
            float c = _clock;
            off = 0.5f * p * MathF.Exp(-4.5f * c) * MathF.Sin(MathF.Tau * 1.7f * c);     // the tail's stretch: one bounce, dying away
            int sway = (int)MathF.Round(MathF.Sin(_swayPhase) * 3f) + 3;
            page = HangIndex(Math.Clamp(sway, 0, SwayPages - 1), _hangLook, _hands ? 1 : 0);
        }
        else
        {
            float crunch = _fast ? 0.28f : 0.56f, rise = _fast ? 0.5f : 0.95f;
            if (_clock < crunch)
            {
                int pg = Math.Min(3, (int)(_clock / crunch * 4f));
                Stamp(fb, _crunch, pg, _hangX, _hangEdge, _mirror, _s.OpenOfProps, null, 255);
                return;
            }
            float u = Math.Clamp((_clock - crunch) / rise, 0f, 1f);
            off = -4.6f * p * u * u;
            Stamp(fb, _crunch, 3, _hangX, _hangEdge + off, _mirror, _s.OpenOfProps, null, 255);
            return;
        }
        Stamp(fb, _hang, page, _hangX, _hangEdge + off, _mirror, _s.OpenOfProps, null, 255);
    }

    private static int HangIndex(int sway, int look, int hands) => (sway * Looks + look) * 2 + hands;
}
