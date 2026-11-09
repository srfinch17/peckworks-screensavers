using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// The branch raid. His words: "the squirrels should appear on the foreground branches sometimes. maybe knocking
/// nuts down." A third squirrel (not one of the patch's two) runs in along an oak branch from the screen's edge,
/// stops over the patch, sits up and looks about, then SCRABBLES at the wood: the acorns hanging in the leaf
/// bunches near it shake loose and patter down onto the pumpkin patch. Then one of two endings (rolled):
///
///   - THE SLIP: on the last shake it loses its footing, falls off the top of the branch and HANGS under it by its
///     forepaws, legs pedalling, for a second; scrambles back up, sits, looks round at the camera as if nobody saw,
///     and runs back out the way it came.
///   - THE SNACK: it keeps one for itself, sits up and eats it, then runs out.
///
/// FEYNMAN VERSION: this is a play with one actor. Begin writes the cast list of "moments" (run in, sit up, look at
/// us, scrabble, slip, hang, climb back, run out), each with a start and end time. Drawing a frame is "which moment
/// is the clock in, how far through is it". The acorns are the NutField's: Update tells it to let go of a twig at
/// the scripted moment, and after that the nut is the field's own (it lands, lies on the patch, thuds a Food sense
/// on the board, and the patch squirrels come for it once the story is over).
///
/// WHAT IT IS SOLID TO: nothing. The branches are the nearest layer in the picture, so the squirrel is stamped with
/// no stencil and no depth test, in front of the leaf bunches (the falling leaves, drawn after, still pass over
/// it). The only thing it stands on is the branch's own wood: its feet follow ScenerY.BranchTopY(x), the top edge of
/// the painter's grown timber, so it cannot float or sink.
///
/// THE TILT: a bounding squirrel on a sloping branch leans with it. Its run pages are therefore painted again at
/// the branch's slope (SquirrelArt.Pose.Rotate), one set per side and direction, on a side thread at start. A sitting
/// or scrabbling squirrel keeps level (gravity), so those use the Cast's own sheets, and the stand spot is chosen
/// where the wood is nearly flat.
///
/// Claims: "branches" (the acorn drop and the cardinal use them). Test aid: PECKWORKS_BRANCHRAID, a comma list of
/// left or right (the side), hang or eat (the ending); with PECKWORKS_STORY=1 and PECKWORKS_STORYPICK=BranchRaid
/// the story starts at 4.0 s of process time. PECKWORKS_BRANCHDUMP=file writes the branch plans.
/// </summary>
internal sealed class BranchRaid : Story
{
    private enum Ph { RunIn, RunOut, RiseUp, SinkDown, Sit, ToFront, Front, FromFront, Chat, Crouch, MountFwd, Fall, Hang, ScrambleUp, MountBack }

    private sealed class Phase
    {
        public Ph Kind; public float T0, T1, X0, Y0, X1, Y1; public int Dir; public bool Nut;
        public float Dur => T1 - T0;
    }

    /// <summary>One branch: the chain of wood it follows from the screen's edge, where the squirrel starts, where it stands, and the slope between.</summary>
    private sealed class Plan
    {
        public bool Ok;
        public int Dir, Side;                  // the way in: +1 from the left edge (Side 0), -1 from the right (Side 1)
        public bool Lower;                     // the lower of its side's two branches
        public readonly List<(float AX, float AY, float BX, float BY, float W)> Pieces = new();   // the chain, edge to tip
        public float XEdge, XRun;              // the screen's edge, and how far in the chain stays thick enough to stand on
        public float XStand, YStand, XStart;   // where it stops; where it starts (off screen)
        public float SlopeDeg;                 // the run's mean slope, degrees; the run pages are rotated by Dir times this
        public int Spots;                      // leaf-bunch twigs within reach of the stand
    }

    public override string Name => "BranchRaid";
    public override string Claims => "branches";
    public override bool CanBegin => _ready && _plan.Exists(p => p.Ok);

    private readonly Cast _c;
    private readonly ThanksgivingScenery _s;
    private readonly Func<NutField> _nutsOf;
    private readonly List<Plan> _plan = new();
    private Sheet[,] _run = new Sheet[0, 0];                // [plan, 0 in / 1 out]
    private volatile bool _ready;
    private readonly string _aid;

    private readonly List<Phase> _ph = new();
    private readonly List<PointF> _twigs = new();
    private readonly List<float> _dropAt = new();
    private int _pi, _dropped, _looseBefore;
    private bool _told, _hang;
    private float _end;
    private const float FootSink = 1.0f;                   // pixels: the feet sit a hair into the bark (measured: at -1.5 the squirrel stood 4 px above the painted wood)
    private const float HangDrop = 0.9f;                   // squirrel units: how far below the branch's top its feet hang

    public BranchRaid(Cast cast, Func<NutField> nuts)
    {
        _c = cast; _s = cast.S; _nutsOf = nuts;
        _aid = Environment.GetEnvironmentVariable("PECKWORKS_BRANCHRAID") ?? "";
        // The branches that come in from an edge: the painter's timber pieces that begin off screen. Each starts a chain.
        foreach (var piece in _s.BranchTimber)
            if (piece.A.X < 0 || piece.A.X > _s.Width) _plan.Add(MakePlan(piece));
        // Lower or upper of its side: by the height they start at.
        foreach (Plan p in _plan)
        {
            float y = p.Pieces[0].AY;
            foreach (Plan q in _plan) if (q.Side == p.Side && q.Pieces[0].AY < y) p.Lower = true;
        }
        string? dump = Environment.GetEnvironmentVariable("PECKWORKS_BRANCHDUMP");
        if (!string.IsNullOrEmpty(dump)) Dump(dump);
        // The tilted run pages: painted on a side thread (the Cast's own build is on another), ready long before the first story.
        Task.Run(() => { try { BuildRuns(); _ready = true; } catch { _ready = false; } });
    }

    // ---------------------------------------------------------------- the branch

    private float MinW => _s.U * 0.009f;

    /// <summary>The top of the wood at x along this branch, the first piece's line carried on beyond the screen's edge (where the squirrel starts).</summary>
    private static float YAt(Plan p, float x)
    {
        var first = p.Pieces[0];
        if ((p.Dir > 0 && x < first.AX) || (p.Dir < 0 && x > first.AX)) { float f0 = (x - first.AX) / (first.BX - first.AX); return first.AY + (first.BY - first.AY) * f0 - first.W / 2f; }
        foreach (var (ax, ay, bx, by, w) in p.Pieces)
        {
            float x0 = MathF.Min(ax, bx), x1 = MathF.Max(ax, bx);
            if (x < x0 || x > x1 || x1 - x0 < 0.5f) continue;
            return ay + (by - ay) * ((x - ax) / (bx - ax)) - w / 2f;
        }
        var last = p.Pieces[^1];
        return last.BY - last.W / 2f;
    }

    private Plan MakePlan(in (PointF A, PointF B, float Width) start)
    {
        var p = new Plan { Dir = start.A.X < 0 ? 1 : -1 };
        p.Side = p.Dir > 0 ? 0 : 1;
        float w = _s.Width, k = _c.K;
        p.XEdge = p.Side == 0 ? 0f : w;
        // Follow the chain: at each fork the thickest piece that carries on the same way. Stop when it thins below a squirrel's wood.
        var cur = start;
        while (true)
        {
            p.Pieces.Add((cur.A.X, cur.A.Y, cur.B.X, cur.B.Y, cur.Width));
            bool found = false;
            (PointF A, PointF B, float Width) best = default;
            foreach (var cand in _s.BranchTimber)
            {
                if (MathF.Abs(cand.A.X - cur.B.X) > 0.6f || MathF.Abs(cand.A.Y - cur.B.Y) > 0.6f) continue;
                if ((cand.B.X - cand.A.X) * p.Dir <= 0.5f) continue;                    // it must keep going inward
                if (!found || cand.Width > best.Width) { best = cand; found = true; }
            }
            if (!found || best.Width < MinW) break;
            cur = best;
        }
        // Where the chain is still thick enough, and how far in (never past 0.45 of the width).
        p.XRun = p.XEdge;
        foreach (var (ax, ay, bx, by, wd) in p.Pieces)
        {
            if (wd < MinW) break;
            float endX = bx;
            if ((endX - p.XEdge) * p.Dir > 0.45f * w) { endX = p.XEdge + p.Dir * 0.45f * w; }
            if ((endX - p.XRun) * p.Dir > 0) p.XRun = endX;
        }
        if ((p.XRun - p.XEdge) * p.Dir < 0.08f * w) return p;
        p.XStart = p.XEdge - p.Dir * 2.6f * k;
        // The stand: where the wood is flattest and the most leaf bunches hang within a squirrel and a half.
        float bestScore = float.MinValue;
        for (float sx = p.XEdge + p.Dir * 0.05f * w; (p.XRun - sx) * p.Dir > 0.5f * k; sx += p.Dir * 6f)
        {
            float ya = YAt(p, sx - 1.2f * k), yb = YAt(p, sx + 1.2f * k), yc = YAt(p, sx);
            float slope = MathF.Abs(yb - ya) / (2.4f * k);
            int n = CountSpots(sx, yc, 1.6f * k);
            float score = MathF.Min(n, 8) * 1.5f - slope * 30f;
            if (score > bestScore) { bestScore = score; p.XStand = sx; p.YStand = yc; p.Spots = n; }
        }
        p.SlopeDeg = MathF.Atan((p.YStand - YAt(p, p.XStart)) / (p.XStand - p.XStart)) * 180f / MathF.PI;
        p.Ok = true;
        return p;
    }

    private int CountSpots(float x, float yBranch, float reach)
    {
        int n = 0;
        foreach (PointF s in _s.CornerBranchSpots)
            if (MathF.Abs(s.X - x) < reach && s.Y > yBranch - 0.12f * _s.U && s.Y < yBranch + 0.2f * _s.U) n++;
        return n;
    }

    private void Dump(string file)
    {
        using var sw = new StreamWriter(file, append: true);
        for (int i = 0; i < _plan.Count; i++)
        {
            Plan p = _plan[i];
            sw.WriteLine($"plan {i} side {p.Side} lower {p.Lower} ok {p.Ok} pieces {p.Pieces.Count} start y {p.Pieces[0].AY:F0} run to {p.XRun:F0} stand ({p.XStand:F0},{p.YStand:F0}) slope {p.SlopeDeg:F1} deg spots {p.Spots} chain [{string.Join(" ", p.Pieces.Select(q => $"({q.AX:F0},{q.AY:F0})w{q.W:F1}"))}] W {_s.Width} U {_s.U:F0} K {_c.K:F1}");
        }
    }
    // ---------------------------------------------------------------- the pictures

    private void BuildRuns()
    {
        float k = _c.K;
        int w = (int)(3.4f * k) + 6, h = (int)(2.4f * k) + 6, ox = (int)(2.0f * k) + 3, by = h - (int)(0.35f * k) - 3;
        var runs = new Sheet[_plan.Count, 2];
        for (int side = 0; side < _plan.Count; side++)
        {
            Plan p = _plan[side];
            if (!p.Ok) continue;
            for (int way = 0; way < 2; way++)
            {
                int dir = way == 0 ? p.Dir : -p.Dir;
                float rot = dir * p.SlopeDeg;              // facing right on a slope that descends to the right: nose down, clockwise, positive; mirrored, the sign flips
                runs[side, way] = Cast.MakeSheet(Cast.RunPages, w, h, ox, by, i =>
                {
                    var q = SquirrelArt.Bound(i / (float)Cast.RunPages, Cast.BoundLen);
                    q.Rotate = rot;
                    return SquirrelArt.Page(q, k, w, h, ox, by);
                });
            }
        }
        _run = runs;
    }

    // ---------------------------------------------------------------- the script

    public override float Begin(Random rng)
    {
        float k = _c.K, u = _s.U;
        var pick = new List<int>();
        for (int i = 0; i < _plan.Count; i++)
        {
            Plan q = _plan[i];
            if (!q.Ok) continue;
            if (_aid.Contains("left") && q.Side != 0) continue;
            if (_aid.Contains("right") && q.Side != 1) continue;
            if (_aid.Contains("upper") && q.Lower) continue;
            if (_aid.Contains("lower") && !q.Lower) continue;
            pick.Add(i);
        }
        if (pick.Count == 0) for (int i = 0; i < _plan.Count; i++) if (_plan[i].Ok) pick.Add(i);
        _pi = pick[rng.Next(pick.Count)];
        _hang = _aid.Contains("hang") ? true : _aid.Contains("eat") ? false : rng.NextDouble() < 0.55;
        Plan p = _plan[_pi];
        int dir = p.Dir, dout = -dir;
        float xs = p.XStand, ys = p.YStand + FootSink;
        NutField nuts = _nutsOf();
        _looseBefore = nuts.LooseCount;
        _dropped = 0; _told = false;

        // The nuts: the twigs nearest the stand (apart from each other), three to six, filled out with spots on the wood itself.
        int n = 3 + rng.Next(4);
        _twigs.Clear(); _dropAt.Clear();
        var near = new List<PointF>();
        foreach (PointF s in _s.CornerBranchSpots)
            if (MathF.Abs(s.X - xs) < 1.8f * k && s.Y > p.YStand - 0.12f * u && s.Y < p.YStand + 0.2f * u && s.X > 0 && s.X < _s.Width) near.Add(s);
        near.Sort((a, b) => MathF.Abs(a.X - xs).CompareTo(MathF.Abs(b.X - xs)));
        foreach (PointF s in near)
        {
            if (_twigs.Count >= n) break;
            bool apart = true;
            foreach (PointF q in _twigs) if (MathF.Abs(q.X - s.X) < 0.3f * k) apart = false;
            if (apart) _twigs.Add(s);
        }
        while (_twigs.Count < n)
        {
            float fx = xs + (float)(rng.NextDouble() * 2 - 1) * 1.4f * k;
            _twigs.Add(new PointF(fx, YAt(p, fx) - 0.5f * k));
        }

        // The moments.
        _ph.Clear();
        float t = 0f;
        float run = Cast.Dist(new PointF(p.XStart, YAt(p, p.XStart)), new PointF(xs, ys)) / (Cast.Speed * u);
        Phase Add(Ph kind, float dur, int d, bool nut = false, float x0 = float.NaN, float y0 = float.NaN, float x1 = float.NaN, float y1 = float.NaN)
        {
            var ph = new Phase { Kind = kind, T0 = t, T1 = t + dur, Dir = d, Nut = nut, X0 = float.IsNaN(x0) ? xs : x0, Y0 = float.IsNaN(y0) ? ys : y0, X1 = float.IsNaN(x1) ? xs : x1, Y1 = float.IsNaN(y1) ? ys : y1 };
            _ph.Add(ph); t += dur;
            return ph;
        }
        Add(Ph.RunIn, run, dir, false, p.XStart, YAt(p, p.XStart) + FootSink, xs, ys);
        Add(Ph.RiseUp, Cast.RiseFor, dir);
        Add(Ph.Sit, 0.5f, dir);
        Add(Ph.ToFront, 0.12f, dir);
        Add(Ph.Front, 0.9f, dir);
        Add(Ph.FromFront, 0.12f, dir);
        Add(Ph.SinkDown, Cast.RiseFor, dir);
        float scrabble = t;
        float chatFor = 2.1f;
        Add(Ph.Chat, chatFor, dir);
        for (int i = 0; i < _twigs.Count; i++) _dropAt.Add(scrabble + 0.25f + i * (chatFor - 0.6f) / _twigs.Count);
        if (_hang)
        {
            Add(Ph.Crouch, 0.08f, dir);
            Add(Ph.MountFwd, 0.25f, dir);
            Add(Ph.Fall, 0.2f, dir);
            Add(Ph.Hang, 1.4f, dir);
            Add(Ph.ScrambleUp, 0.55f, dir);
            Add(Ph.MountBack, 0.3f, dir);
            Add(Ph.RiseUp, Cast.RiseFor, dir);
            Add(Ph.Sit, 0.3f, dir);
            Add(Ph.ToFront, 0.12f, dir);
            Add(Ph.Front, 0.9f, dir);
            Add(Ph.FromFront, 0.12f, dout);
        }
        else
        {
            Add(Ph.RiseUp, Cast.RiseFor, dir, true);
            Add(Ph.Sit, 2.2f, dir, true);
            Add(Ph.ToFront, 0.12f, dir);
            Add(Ph.Front, 0.4f, dir);
            Add(Ph.FromFront, 0.12f, dout);
        }
        Add(Ph.SinkDown, Cast.RiseFor, dout);
        float outRun = Cast.Dist(new PointF(xs, ys), new PointF(p.XStart, YAt(p, p.XStart) + FootSink)) / (Cast.Speed * u * 1.15f);
        Add(Ph.RunOut, outRun, dout, false, xs, ys, p.XStart, YAt(p, p.XStart) + FootSink);
        _end = t + 0.3f;

        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"branchraid: plan {_pi} side {p.Side} lower {p.Lower} dir {dir} stand ({xs:F0},{ys:F0}) slope {p.SlopeDeg:F1} deg, nuts {n} from twigs {near.Count} near, ending {(_hang ? "slip" : "snack")}, scrabble at {scrabble:F2}, end {_end:F2}, loose before {_looseBefore}");
        }
        return _end;
    }

    public override void Update(float t)
    {
        NutField nuts = _nutsOf();
        if (!_told && t >= 0.5f)
        {
            _told = true;
            Plan p = _plan[_pi];
            _c.Board.Post(Sense.Movement, new PointF(p.XStand, p.YStand), 0.3f * _s.U, 0.5f, this);   // a squirrel on the branch: anything within a third of U of its foot glances up (nobody was measured in reach on the ground)
        }
        while (_dropped < _twigs.Count && t >= _dropAt[_dropped]) { nuts.Drop(_twigs[_dropped], 0f); _dropped++; }
    }

    public override void End()
    {
        NutField nuts = _nutsOf();
        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"branchraid ended: nuts dropped {_dropped}, loose now {nuts.LooseCount}, falling {nuts.FallingCount} (loose before {_looseBefore})");
        }
    }

    // ---------------------------------------------------------------- drawing

    private void Put(FrameBuffer fb, in Sheet sh, Sprite page, float fx, float fy, bool mirror, float opacity = 1f)
    {
        int left = (int)MathF.Round(fx) - (mirror ? sh.W - sh.Ox : sh.Ox);
        page.Draw(fb, left, (int)MathF.Round(fy) - sh.By, opacity, null, mirror);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        Plan p = _plan[_pi];
        float k = _c.K;
        foreach (Phase ph in _ph)
        {
            if (t < ph.T0 || t >= ph.T1) continue;
            float u = (t - ph.T0) / Math.Max(0.001f, ph.Dur), s = t - ph.T0;
            bool mir = ph.Dir < 0;
            int nut = ph.Nut ? 1 : 0;
            switch (ph.Kind)
            {
                case Ph.RunIn:
                case Ph.RunOut:
                {
                    float x = ph.X0 + (ph.X1 - ph.X0) * u, y = YAt(p, x) + FootSink;
                    float dist = Cast.Dist(new PointF(ph.X0, ph.Y0), new PointF(ph.X1, ph.Y1)) * u;
                    float cyc = dist / (Cast.BoundLen * k);
                    Sheet sh = _run[_pi, ph.Kind == Ph.RunIn ? 0 : 1];
                    Put(fb, sh, sh.Pages[(int)((cyc - MathF.Floor(cyc)) * Cast.RunPages) % Cast.RunPages], x, y, mir);
                    break;
                }
                case Ph.RiseUp:
                case Ph.SinkDown:
                {
                    float kk = ph.Kind == Ph.RiseUp ? u : 1 - u;
                    Sheet sh = _c.Rise[nut];
                    Put(fb, sh, sh.Pages[Math.Clamp((int)(kk * Cast.RisePages), 0, Cast.RisePages - 1)], ph.X0, ph.Y0, mir);
                    break;
                }
                case Ph.Crouch:
                    Put(fb, _c.Rise[0], _c.Rise[0].Pages[0], ph.X0, ph.Y0, mir);
                    break;
                case Ph.Sit:
                {
                    float ft = s % 1.5f;
                    int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                    Sheet sh = _c.Sit[nut, tail];
                    Put(fb, sh, sh.Pages[0], ph.X0, ph.Y0, mir);
                    break;
                }
                case Ph.ToFront:
                case Ph.FromFront:
                {
                    // The front view is the Cast's turn page 2; the turn pages are painted facing right and mirrored to face left.
                    int page = ph.Kind == Ph.ToFront ? Math.Min(Cast.TurnPages - 1, (int)(u * Cast.TurnPages)) : Math.Min(Cast.TurnPages - 1, (int)((1 - u) * Cast.TurnPages));
                    Sheet sh = _c.Turn[nut];
                    Put(fb, sh, sh.Pages[page], ph.X0, ph.Y0, mir);
                    break;
                }
                case Ph.Front:
                {
                    Sheet sh = _c.Turn[nut];
                    Put(fb, sh, sh.Pages[Cast.TurnPages - 1], ph.X0, ph.Y0, mir);
                    break;
                }
                case Ph.Chat:
                {
                    // The scrabble: the Cast's chattering crouch, a paw stamped, the tail snapping, a shudder through the body.
                    float sc = s;
                    int f = (int)(sc * 60), cyc = f % 14;
                    int snap = cyc < 6 ? new[] { 1, 2, 3, 3, 2, 1 }[cyc] : 0;
                    int paw = (f / 8) % 2;
                    float shudder = MathF.Round(MathF.Sin(sc * 15f * MathF.Tau));
                    Sheet sh = _c.Chatter[snap, paw];
                    Put(fb, sh, sh.Pages[0], ph.X0 + shudder, ph.Y0, mir);
                    break;
                }
                case Ph.MountFwd:
                    Put(fb, _c.Mount[0], _c.Mount[0].Pages[Math.Clamp((int)(u * Cast.MountPages), 0, Cast.MountPages - 1)], ph.X0, ph.Y0, mir);
                    break;
                case Ph.MountBack:
                    Put(fb, _c.Mount[0], _c.Mount[0].Pages[Math.Clamp((int)((1 - u) * Cast.MountPages), 0, Cast.MountPages - 1)], ph.X0, ph.Y0, mir);
                    break;
                case Ph.Fall:
                case Ph.Hang:
                case Ph.ScrambleUp:
                {
                    // Clinging, head up, the forepaws on the branch's top edge and the body swinging under it (the climb pose, which is
                    // exactly that with the wood where the trunk would be). Fall: the feet drop away. Hang: the legs pedal, the body sways.
                    float drop = HangDrop * k;
                    float y = ph.Kind == Ph.Fall ? ph.Y0 + drop * u * u : ph.Kind == Ph.Hang ? ph.Y0 + drop : ph.Y0 + drop * (1f - Cast.Smooth(u / 0.85f));
                    float x = ph.X0 + (ph.Kind == Ph.Hang ? 1.5f * MathF.Sin(s * 5f) : 0f);
                    float rate = ph.Kind == Ph.Fall ? 0.5f : ph.Kind == Ph.Hang ? 1.3f : 2.2f;
                    Sheet sh = _c.Up[0];
                    Put(fb, sh, sh.Pages[(int)(s * rate * Cast.RunPages) % Cast.RunPages], x, y, mir);
                    break;
                }
            }
            break;
        }
    }
}
