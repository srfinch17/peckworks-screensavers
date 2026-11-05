using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The acorn caper: a little story that plays out on the near ground every
/// minute or two, for anyone watching the way a child watches (his line:
/// "imagine you're a little kid and you're watching it out of fascination
/// because the squirrel is coming out of its hiding place to grab some nuts
/// and go bury them and then run up a tree, and then another squirrel
/// unburies it and finds it and the squirrels have a fracas and during the
/// fracas a mouse steals it").
///
/// THE STORY, in five acts:
///   1. A tawny squirrel's head rises out of the hollow in the roots of the
///      left maple, blinks, looks about. It hops out, noses through the
///      acorns lying under the oak branch, picks one, climbs its trunk to the
///      fence's rail, bounds along the rail (behind the corn shock if it
///      passes it), drops to the ground at a spot it has chosen, digs a hole
///      with earth flying behind it, drops the nut in, pats the mound flat,
///      looks about to be sure nobody saw, and goes back along the rail and
///      up its tree into the leaves.
///   2. A second squirrel comes head first down the right maple, onto the
///      rail, and comes bounding along it with its nose down, stopping twice
///      to sniff. It stops above the mound, drops down, sniffs it, digs it
///      up and sits up holding the acorn.
///   3. The first squirrel's head appears in the left maple. It freezes,
///      then races down the trunk and along the rail and drops to the ground
///      facing the thief. They crouch and chatter at each other, tails
///      lashing, paws stamping, and go for each other: a burst of dust that
///      rolls and boils with tails, paws and ears popping out of it, and the
///      acorn is flung out of the scuffle and bounces away toward the corn
///      shock.
///   4. A mouse creeps out from the corn shock in stop-start dashes, sniffs
///      the acorn, takes it, and scurries home with it, while the squirrels
///      are still rolling.
///   5. The dust thins over the two squirrels sitting dazed, heads lolling.
///      They look over at where the acorn was, look at each other, and each
///      goes home: one along the rail and up the left maple, the other up
///      the right.
///
/// FEYNMAN VERSION: this is not a clockwork of states like the turkeys. It is
/// a SCRIPT. When a cycle begins, dice choose the burying spot and the
/// pauses, and Begin writes out every actor's whole part as a list of
/// "segments" (hop from here to there, run along the rail, dig for so many
/// seconds), each with its start and end time on the cycle's clock. Drawing
/// a frame is then only: find the segment whose time it is, and work out
/// where that actor is within it from the clock alone, exactly as a
/// happening does. No change of state happens in one frame: a hop has its
/// crouch, push, flight, landing and settle; a turn goes round through a
/// front view; a climb begins with the body pitching up onto the bark; a
/// run eases into a sit through rising pages. The pictures are the shared
/// squirrel art (SquirrelArt.cs); the mouse and the dust are painted here.
///
/// It holds the director's "ground", "fence" and "cornshock" claims while it
/// plays (HappeningDirector.SceneClaims), so no turkey strolls through the
/// scuffle and no second squirrel runs the rail at the same time, and it
/// never begins while a happening holding one of those is on.
///
/// What it is solid to: the ground (WalkY), the rail (the painter's own
/// rail curve) and the trunks. On the rail and on the trunks it is stamped
/// through the OpenOfProps stencil, so it passes behind the corn shock and
/// into the maples' leaves; on the ground it is in front of everything.
/// </summary>
internal sealed class AcornCaper : Story
{
    private struct Pop { public float T; public int Kind; public float Angle, Dur; }

    public override string Name => "AcornCaper";
    public override string Claims => "ground,fence,cornshock";

    private readonly Cast _c;
    private readonly ThanksgivingScenery _s;
    private Script _script = null!;
    private readonly List<Pop> _pops = new();
    private float _end;
    private float _pileX, _pileY, _pileTaken, _spotX, _buryAt, _digUpAt, _flingAt, _flingX, _landX, _grabAt;

    public AcornCaper(Cast cast) { _c = cast; _s = cast.S; }

    private float WalkY(float x) => _s.WalkY(x);
    private float RailAt(float x) => _c.RailAt(x);
    private static float Smooth(float x) => Cast.Smooth(x);

    /// <summary>The point on a trunk's line (foot to fork) at height y, set just outside the bark: where a squirrel's feet go.</summary>
    internal static PointF OnTrunk(ThanksgivingScenery s, PointF[] trunk, float y)
    {
        float f = Math.Clamp((trunk[0].Y - y) / (trunk[0].Y - trunk[1].Y), 0f, 1f);
        return new PointF(trunk[0].X + (trunk[1].X - trunk[0].X) * f - s.U * 0.0065f, y);
    }
    private PointF OnTrunk(PointF[] trunk, float y) => OnTrunk(_s, trunk, y);

    /// <summary>Rolls this cycle's dice and writes out everybody's part.</summary>
    public override float Begin(Random _rng)
    {
        _script = new Script(_c, 4);
        _pops.Clear();
        float k = _c.K, u = _s.U, _m = _c.M;
        List<Seg> A = _script[0], B = _script[1], M = _script[2], C = _script[3];
        const float MouseSpeed = Cast.MouseSpeed, TurnFor = Cast.TurnFor;
        float Run(List<Seg> tr, float t0, PointF from, PointF to, bool acorn, bool rail) => _script.Run(tr, t0, from, to, acorn, rail);
        float Climb(List<Seg> tr, float t0, PointF from, PointF to, bool up, bool acorn) => _script.Climb(tr, t0, from, to, up, acorn);
        static float Hop(List<Seg> tr, float t0, PointF from, PointF to, int dir, bool acorn, float height = 0.55f) => Script.Hop(tr, t0, from, to, dir, acorn, height);
        static float Turn(List<Seg> tr, float t0, PointF at, int toDir, bool acorn) => Script.Turn(tr, t0, at, toDir, acorn);
        static float Rise(List<Seg> tr, float t0, PointF at, int dir, bool acorn, int way = 1) => Script.Rise(tr, t0, at, dir, acorn, way);
        static float MountUp(List<Seg> tr, float t0, PointF at, int facing, bool acorn) => Script.MountUp(tr, t0, at, facing, acorn);
        static float Dismount(List<Seg> tr, float t0, PointF at, int nextDir, bool acorn) => Script.Dismount(tr, t0, at, nextDir, acorn);
        static float DismountDown(List<Seg> tr, float t0, PointF at, int nextDir, bool acorn) => Script.DismountDown(tr, t0, at, nextDir, acorn);
        static float Sit(List<Seg> tr, float t0, float dur, PointF at, int dir, bool acorn, bool look = false) => Script.Sit(tr, t0, dur, at, dir, acorn, look);
        static float Chatter(List<Seg> tr, float t0, float dur, PointF at, int dir, bool acorn, int who) => Script.Chatter(tr, t0, dur, at, dir, acorn, who);
        static float Dig(List<Seg> tr, float t0, float dur, PointF at, int dir, float rate, bool earth, bool acorn = false, bool rail = false) => Script.Dig(tr, t0, dur, at, dir, rate, earth, acorn, rail);
        static Seg Add(List<Seg> tr, Kind kind, float t0, float dur, PointF from, PointF to, int dir, bool acorn = false, bool rail = false, float p0 = 0f, float p1 = 0f, bool look = false, bool up = false) => Script.Add(tr, kind, t0, dur, from, to, dir, acorn, rail, p0, p1, look, up);
        PointF[] lt = _s.LeftTrunk, rt = _s.RightTrunk;
        float hx = _s.Hollow.At.X;
        float lRail = RailAt(lt[0].X), rRail = RailAt(rt[0].X);
        PointF lTrunkAtRail = OnTrunk(lt, lRail - 1.5f), rTrunkAtRail = OnTrunk(rt, rRail - 1.5f);
        PointF lFork = new(lt[1].X - u * 0.0065f, lt[1].Y), rFork = new(rt[1].X - u * 0.0065f, rt[1].Y);
        PointF lTop = new(lt[2].X - u * 0.0065f, lt[2].Y), rTop = new(rt[2].X - u * 0.0065f, rt[2].Y);
        PointF OnRail(float x) => _script.OnRail(x);
        PointF OnGround(float x) => _script.OnGround(x);

        // The burying spot: in the open stretch between the cornucopia and the corn shock, clear of both.
        float shockEdge = _s.CornShockFoot.X - _s.CornShockHalfWidth - 2.4f * k;
        float lo = Math.Max(_s.CornucopiaMouth.X + 3.0f * k, _s.Width * 0.30f), hi = Math.Min(_s.Width * 0.46f, shockEdge);   // the face-off spreads 1.6 squirrels to the left of the spot: keep it off the fruit
        _spotX = hi > lo ? lo + (hi - lo) * (float)_rng.NextDouble() : (lo + hi) / 2;   // (a screen too narrow for the open stretch: the middle of what there is)
        _pileX = hx - 1.7f * k;
        _pileY = WalkY(_pileX);

        // ---- Act 1: out of the hollow, a nut, bury it, home up the tree ----
        float t = 0f;
        Add(A, Kind.Peek, t, 0.7f, _s.Hollow.At, _s.Hollow.At, -1, p0: 0f, p1: 1f); t += 0.7f;
        Add(A, Kind.Peek, t, 1.1f + 0.6f * (float)_rng.NextDouble(), _s.Hollow.At, _s.Hollow.At, -1, p0: 1f, p1: 1f); t = A[^1].T1;
        t = Hop(A, t, new PointF(hx - 0.15f * k, _s.Hollow.At.Y + 0.2f * k), OnGround(hx - 0.95f * k), -1, false, 0.5f);
        PointF atPile = OnGround(_pileX + 0.78f * k);                                    // the paws reach the pile's near edge, the nuts in view past its nose
        t = Hop(A, t, OnGround(hx - 0.95f * k), atPile, -1, false, 0.35f);
        t = Rise(A, t, atPile, -1, false, -1);                                           // down onto all fours
        t = Dig(A, t, 0.9f, atPile, -1, 1.6f, false);                                    // nosing through the pile
        _pileTaken = t;
        t = Rise(A, t, atPile, -1, true);
        t = Sit(A, t, 0.5f, atPile, -1, true, look: true);                               // got one
        t = Turn(A, t, atPile, 1, true);                                                 // round to face the tree
        PointF lTrunkFoot = OnTrunk(lt, WalkY(lt[0].X));
        t = Hop(A, t, atPile, lTrunkFoot, 1, true, 0.3f);                                // lands exactly where the mount begins (a 14 px jump between the two before)
        t = MountUp(A, t, lTrunkFoot, 1, true);
        t = Climb(A, t, lTrunkFoot, lTrunkAtRail, true, true);
        t = Dismount(A, t, lTrunkAtRail, 1, true);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, true, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 0.5f * k), true, true);
        t = Hop(A, t, OnRail(_spotX - 0.5f * k), OnGround(_spotX - 0.5f * k), 1, true, 0.2f);
        t = Rise(A, t, OnGround(_spotX - 0.5f * k), 1, true, -1);
        t = Dig(A, t, 1.9f, OnGround(_spotX - 0.5f * k), 1, 3.2f, true, acorn: true);
        _buryAt = t;
        t = Dig(A, t, 0.5f, OnGround(_spotX - 0.5f * k), 1, 0f, false);                 // the nut goes in
        t = Dig(A, t, 0.8f, OnGround(_spotX - 0.5f * k), 1, 1.4f, false);               // pats the earth down
        t = Rise(A, t, OnGround(_spotX - 0.5f * k), 1, false);
        t = Sit(A, t, 0.9f, OnGround(_spotX - 0.5f * k), 1, false, look: true);          // did anybody see?
        t = Turn(A, t, OnGround(_spotX - 0.5f * k), -1, false);
        t = Sit(A, t, 0.3f, OnGround(_spotX - 0.5f * k), -1, false);
        t = Hop(A, t, OnGround(_spotX - 0.5f * k), OnRail(_spotX - 1.3f * k), -1, false, 0.5f);
        t = Run(A, t, OnRail(_spotX - 1.3f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        t = Hop(A, t, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        t = MountUp(A, t, lTrunkAtRail, -1, false);
        t = Climb(A, t, lTrunkAtRail, lFork, true, false);
        t = Climb(A, t, lFork, lTop, true, false);
        float aHome = t;

        // ---- Act 2: the thief ----
        t = aHome + 2.5f + 3f * (float)_rng.NextDouble();
        t = Climb(B, t, rTop, rFork, false, false);
        t = Climb(B, t, rFork, rTrunkAtRail, false, false);
        t = DismountDown(B, t, rTrunkAtRail, -1, false);
        t = Hop(B, t, rTrunkAtRail, OnRail(rt[0].X - 1.0f * k), -1, false, 0.4f);
        float bx = rt[0].X - 1.0f * k;
        float sniff1 = bx - (bx - (_spotX + 0.5f * k)) * (0.30f + 0.15f * (float)_rng.NextDouble());
        float sniff2 = bx - (bx - (_spotX + 0.5f * k)) * (0.65f + 0.15f * (float)_rng.NextDouble());
        // Keep the sniffing stops out of the corn shock's shadow (it would sniff invisibly behind it).
        float shockL = _s.CornShockFoot.X - _s.CornShockHalfWidth - 1.4f * k, shockR = _s.CornShockFoot.X + _s.CornShockHalfWidth + 1.4f * k;
        if (sniff1 > shockL && sniff1 < shockR) sniff1 = shockR;
        if (sniff2 > shockL && sniff2 < shockR) sniff2 = shockL;
        t = Run(B, t, OnRail(bx), OnRail(sniff1), false, true);
        t = Dig(B, t, 0.6f, OnRail(sniff1), -1, 0f, false, rail: true);                   // nose down to the rail
        t = Run(B, t, OnRail(sniff1), OnRail(sniff2), false, true);
        t = Dig(B, t, 0.6f, OnRail(sniff2), -1, 0f, false, rail: true);
        t = Run(B, t, OnRail(sniff2), OnRail(_spotX + 0.5f * k), false, true);
        t = Hop(B, t, OnRail(_spotX + 0.5f * k), OnGround(_spotX + 0.5f * k), -1, false, 0.2f);
        t = Rise(B, t, OnGround(_spotX + 0.5f * k), -1, false, -1);
        t = Dig(B, t, 0.7f, OnGround(_spotX + 0.5f * k), -1, 0f, false);                  // sniffs the mound
        float bDigs = t;
        t = Dig(B, t, 1.0f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        _digUpAt = t;
        t = Dig(B, t, 0.8f, OnGround(_spotX + 0.5f * k), -1, 3.2f, true);
        t = Rise(B, t, OnGround(_spotX + 0.5f * k), -1, true);
        t = Sit(B, t, 1.2f, OnGround(_spotX + 0.5f * k), -1, true, look: true);           // found it!
        float bFound = t;

        // ---- Act 3: caught ----
        t = bDigs;
        t = Climb(A, t, lTop, lFork, false, false);
        Add(A, Kind.Climb, t, 0.7f, lFork, lFork, 1, rail: true); t += 0.7f;             // freezes: what is THAT?
        t = Climb(A, t, lFork, lTrunkAtRail, false, false);
        t = DismountDown(A, t, lTrunkAtRail, 1, false);
        t = Hop(A, t, lTrunkAtRail, OnRail(lt[0].X + 1.0f * k), 1, false, 0.4f);
        t = Run(A, t, OnRail(lt[0].X + 1.0f * k), OnRail(_spotX - 1.6f * k), false, true);
        t = Hop(A, t, OnRail(_spotX - 1.6f * k), OnGround(_spotX - 1.6f * k), 1, false, 0.2f);
        float faceOff = Math.Max(t, bFound);
        if (t < faceOff) Chatter(A, t, faceOff - t, OnGround(_spotX - 1.6f * k), 1, false, 0);
        if (bFound < faceOff) Chatter(B, bFound, faceOff - bFound, OnGround(_spotX + 0.5f * k), -1, true, 1);
        t = faceOff;
        Chatter(A, t, 1.8f, OnGround(_spotX - 1.6f * k), 1, false, 0);
        Chatter(B, t, 1.8f, OnGround(_spotX + 0.5f * k), -1, true, 1);
        t += 1.8f;
        // The lunge: both at each other, the feet stopping a squirrel apart (closer, the two bodies passed through each other
        // for six frames); the dust bursts over them five frames before they meet, so nothing is ever uncovered.
        float cloudX = _spotX - 0.55f * k;
        Add(A, Kind.Run, t, 0.22f, OnGround(_spotX - 1.6f * k), OnGround(cloudX - 0.5f * k), 1, p0: 1.4f * k, p1: 1f);
        Add(B, Kind.Run, t, 0.22f, OnGround(_spotX + 0.5f * k), OnGround(cloudX + 0.5f * k), -1, true, p0: 1.4f * k, p1: 1f);
        t += 0.22f;
        // Both stay drawn under the dust for a few frames more (their raised tails reached past the cloud's first pages and vanished uncovered).
        Add(A, Kind.Run, t, 0.15f, OnGround(cloudX - 0.5f * k), OnGround(cloudX - 0.5f * k), 1, p0: 1.4f * k);
        Add(B, Kind.Run, t, 0.15f, OnGround(cloudX + 0.5f * k), OnGround(cloudX + 0.5f * k), -1, true, p0: 1.4f * k);
        float fracasFor = 3.8f;
        Add(C, Kind.Fracas, t - 0.085f, fracasFor + 0.085f, OnGround(cloudX), OnGround(cloudX), 1, p0: 0.05f, p1: 0.45f);
        _flingAt = t + 0.8f;
        _flingX = cloudX;
        _landX = cloudX + 2.3f * k;
        // Bits of squirrel pop out of the dust every few frames: a paw, a foot, an ear, a tail tip, at a random angle each.
        for (float pt = t + 0.2f; pt < t + fracasFor - 0.6f; pt += (4 + _rng.Next(5)) / 60f)
            _pops.Add(new Pop { T = pt, Kind = _rng.Next(4), Angle = (float)(_rng.NextDouble() * 360), Dur = (3 + _rng.Next(4)) / 60f });

        // ---- Act 4: the mouse, in stop-start dashes ----
        float mx = _s.CornShockFoot.X;
        float m = t + 1.0f;
        PointF nutSpot = OnGround(_landX + 0.45f * _m);
        float[] stops = [0.45f, 0.75f];
        PointF from = OnGround(mx);
        foreach (float f in stops)
        {
            PointF to = OnGround(mx + (nutSpot.X - mx) * f);
            float trip = MathF.Abs(to.X - from.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, from, to, -1); m += trip;
            float freeze = (6 + _rng.Next(5)) / 60f;
            Add(M, Kind.Mouse, m, freeze, to, to, -1); m += freeze;                     // freezes, nose twitching
            from = to;
        }
        {
            float trip = MathF.Abs(nutSpot.X - from.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, from, nutSpot, -1); m += trip;
        }
        Add(M, Kind.Mouse, m, 0.4f, nutSpot, nutSpot, -1); m += 0.4f;                   // sniffs it
        _grabAt = m;
        {
            PointF half = OnGround((nutSpot.X + mx) / 2);
            float trip = MathF.Abs(half.X - nutSpot.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, nutSpot, half, 1, true); m += trip;
            Add(M, Kind.Mouse, m, 0.12f, half, half, 1, true); m += 0.12f;
            trip = MathF.Abs(mx - half.X) / (MouseSpeed * u);
            Add(M, Kind.Mouse, m, trip, half, OnGround(mx), 1, true); m += trip;
        }

        // ---- Act 5: the dust settles over two dazed squirrels ----
        t += fracasFor;
        PointF aAfter = OnGround(cloudX - 0.6f * k), bAfter = OnGround(cloudX + 0.6f * k);                       // inside the cloud's footprint, fading in under it
        Add(A, Kind.Dazed, t - 0.4f, 1.0f, aAfter, aAfter, 1); Add(B, Kind.Dazed, t - 0.4f, 1.0f, bAfter, bAfter, -1);
        t += 0.6f;
        Sit(A, t, 1.2f, aAfter, 1, false, look: true);                                     // where did it go?
        float tb = Turn(B, t, bAfter, 1, false);
        Sit(B, tb, 1.2f - TurnFor, bAfter, 1, false, look: true);
        t += 1.2f;
        tb = Turn(B, t, bAfter, -1, false);
        Sit(A, t, 1.0f, aAfter, 1, false);                                                  // a long look at each other
        Sit(B, tb, 1.3f - TurnFor, bAfter, -1, false);
        t += 1.0f;
        float ta = Turn(A, t, aAfter, -1, false);
        ta = Hop(A, ta, aAfter, OnRail(cloudX - 1.6f * k), -1, false, 0.5f);
        ta = Run(A, ta, OnRail(cloudX - 1.6f * k), OnRail(lt[0].X + 1.0f * k), false, true);
        ta = Hop(A, ta, OnRail(lt[0].X + 1.0f * k), lTrunkAtRail, -1, false, 0.4f);
        ta = MountUp(A, ta, lTrunkAtRail, -1, false);
        ta = Climb(A, ta, lTrunkAtRail, lFork, true, false);
        ta = Climb(A, ta, lFork, lTop, true, false);
        tb = Turn(B, t + 0.3f, bAfter, 1, false);
        tb = Hop(B, tb, bAfter, OnRail(cloudX + 1.6f * k), 1, false, 0.5f);
        tb = Run(B, tb, OnRail(cloudX + 1.6f * k), OnRail(rt[0].X - 1.0f * k), false, true);
        tb = Hop(B, tb, OnRail(rt[0].X - 1.0f * k), rTrunkAtRail, 1, false, 0.4f);
        tb = MountUp(B, tb, rTrunkAtRail, 1, false);
        tb = Climb(B, tb, rTrunkAtRail, rFork, true, false);
        tb = Climb(B, tb, rFork, rTop, true, false);
        _end = Math.Max(ta, tb) + 0.8f;
        _script.FixHops();


        // The script, written out for checking: PECKWORKS_CAPERLOG names a file and every segment of every part goes in it.
        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"cycle: spot {_spotX:F0} pile {_pileX:F0} bury {_buryAt:F2} dugUp {_digUpAt:F2} fling {_flingAt:F2} land {_landX:F0} grab {_grabAt:F2} end {_end:F2}");
            _script.Log(sw, ["A", "B", "mouse", "cloud"]);
        }
        return _end;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float k = _c.K;
        float over = Smooth((_end - t) / 0.6f);                   // everything the caper left on the ground fades at the very end

        // ---- the props: the nut pile, the mound or the hole, the nut that got away ----
        float pileIn = Smooth(t / 0.5f);
        byte[] nd = _s.NearDepth;
        byte pileNear = _s.Nearness(_pileY), spotNear = _s.Nearness(WalkY(_spotX));
        _c.Acorn.DrawCentered(fb, _pileX - 0.16f * k, _pileY - 0.07f * k, pileIn * over, null, false, nd, pileNear);
        _c.Acorn.DrawCentered(fb, _pileX + 0.14f * k, _pileY - 0.07f * k, pileIn * over, null, false, nd, pileNear);
        if (t < _pileTaken) _c.Acorn.DrawCentered(fb, _pileX - 0.01f * k, _pileY - 0.18f * k, pileIn, null, false, nd, pileNear);   // the one on top is the one it takes
        if (t >= _buryAt + 0.3f && t < _digUpAt) _c.Mound.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, Smooth((t - _buryAt - 0.3f) / 0.4f), null, false, nd, spotNear);
        if (t >= _digUpAt) _c.Hole.DrawCentered(fb, _spotX, WalkY(_spotX) - 0.05f * k, over, null, false, nd, spotNear);
        if (t >= _buryAt && t < _buryAt + 0.5f)
        {
            // The nut leaves the mouth (at the nose, down by the paws) and is tipped into the hole.
            float p = (t - _buryAt) / 0.5f;
            float x = _spotX + 0.155f * k * (1 - p), y = WalkY(_spotX) - 0.03f * k - 0.12f * k * 4 * p * (1 - p);
            _c.Air.Pages[(int)(p * 0.5f * Cast.AirPages) % Cast.AirPages].DrawCentered(fb, x, y, 1 - Smooth((p - 0.75f) / 0.25f));
        }
        if (t >= _flingAt && t < _grabAt)
        {
            // Flung out of the scuffle: up and over to the right, two bounces, rolls to a stop.
            float p = t - _flingAt;
            float x, y, ground = WalkY(_landX) - 0.06f * k;
            if (p < 0.6f) { float q = p / 0.6f; x = _flingX + (_landX - 0.5f * k - _flingX) * q; y = WalkY(_flingX) - 0.6f * k + (ground - WalkY(_flingX) + 0.6f * k) * q - 1.3f * k * 4 * q * (1 - q); }
            else if (p < 0.9f) { float q = (p - 0.6f) / 0.3f; x = _landX - 0.5f * k + 0.35f * k * q; y = ground - 0.35f * k * 4 * q * (1 - q); }
            else if (p < 1.1f) { float q = (p - 0.9f) / 0.2f; x = _landX - 0.15f * k + 0.15f * k * q; y = ground - 0.08f * k * 4 * q * (1 - q); }
            else { x = _landX; y = ground; }
            float turn = p < 1.1f ? p * 3.2f : 1.1f * 3.2f;
            _c.Air.Pages[(int)(turn * Cast.AirPages) % Cast.AirPages].DrawCentered(fb, x, y);
        }

        // ---- the actors: the thief, the first squirrel, the mouse, the dust ----
        DrawTrack(fb, _script[1], t);
        DrawTrack(fb, _script[0], t);
        DrawTrack(fb, _script[2], t);
        DrawTrack(fb, _script[3], t);
    }

    private void DrawTrack(FrameBuffer fb, List<Seg> track, float t)
    {
        Seg? seg = null;
        foreach (Seg sg in track)
            if (t >= sg.T0 && t < sg.T1) { seg = sg; break; }
        if (seg == null) return;
        _c.DrawSeg(fb, seg, t);
        if (seg.Kind != Kind.Fracas) return;
        // The scuffle's own extras over the shared cloud: bits of squirrel popping out, tufts of fur and stars flying off on arcs.
        float k = _c.K, s = t - seg.T0;
        float opacity = Smooth(s / seg.P0) * Smooth((seg.Dur - s) / seg.P1);
        float x = seg.From.X + 0.3f * k * MathF.Sin(s * 6.1f), y = seg.From.Y;
        // Bits of squirrel popping out, and tufts of fur and stars flying off on arcs.
        float cx = x, cy = y - 0.62f * k;
        foreach (Pop pop in _pops)
        {
            if (t < pop.T || t >= pop.T + pop.Dur) continue;
            float a = pop.Angle * MathF.PI / 180f;
            int turn = (int)MathF.Round(pop.Angle / 360f * Cast.LimbTurns) % Cast.LimbTurns;              // the bit points outward from the ball (they all pointed right before)
            _c.Limb[pop.Kind, turn].DrawCentered(fb, cx + 0.55f * k * MathF.Cos(a), cy - 0.45f * k * MathF.Sin(a), opacity);
        }
        for (int i = 0; i < 6; i++)
        {
            float p = (s * 1.6f + i / 6f) % 1f;
            float a = (i * 61f + 20f) * MathF.PI / 180f;
            _c.Tuft.DrawCentered(fb, cx + (0.6f + 1.2f * p) * k * MathF.Cos(a), cy - (0.5f + 0.9f * p) * k * MathF.Sin(a) + 1.4f * k * p * p, opacity * (1 - p));
        }
        for (int i = 0; i < 3; i++)
        {
            float a = (s * 1.3f + i * 2.09f);
            float twinkle = 0.5f + 0.5f * MathF.Sin(s * 11f + i * 2f);
            _c.Star.DrawCentered(fb, cx + 1.0f * k * MathF.Cos(a), cy - 0.75f * k * MathF.Sin(a), opacity * twinkle);
        }
    }
}
