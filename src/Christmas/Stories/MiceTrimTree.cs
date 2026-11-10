using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas.Stories;

/// <summary>
/// THE MICE TRIM THE TREE (about 45 s). Three mice come out of the fallen log one behind another, each
/// carrying something over its head, and cross the snow to the tiny fir: the first a red bauble, the second
/// a coil of fairy lights, the third a gold star.
///
///   - The first mouse climbs onto the second one's head and hangs the bauble on a bough.
///   - The second runs round the tree in a spiral (in front, round the back, in front again: while it is
///     behind the trunk the tree hides it, because it is stamped one step FARTHER than the tree) and the cord
///     of lights is wound where it has run.
///   - The third climbs the far side of the tree (hidden by the tree) and comes out at the very tip with the
///     star, puts it on, and hops down.
///   They stand back in a row and look up. The lights come on one after another from the bottom, the star
///   flares and sparkles fall; the mice jump for joy, then go home in a line and hop into the hole.
///   The trimmed tree STAYS lit for four minutes (LogMice's TreeDecor draws it, the very same picture), then
///   the lights fade out one by one from the top, and last the bauble and star wink out.
///
/// HOW IT IS BUILT. Like every story here, Begin rolls the dice and writes a SCRIPT (who is where at which
/// second), and Draw draws from the story's clock "t" alone, so the same seed plays the same story and any
/// second of it can be rendered by itself. Each mouse's script is a list of timed segments (a hop, a walk, a
/// stand, a spiral...). Its pictures are the log mice's own flip-books (LogMiceArt), borrowed through the
/// resident, so a story mouse IS a log mouse: the resident LENDS them (they are inside the log and nothing of
/// theirs is drawn while the story plays) and CanBegin waits until they are all in.
///
/// A walk's pages follow the DISTANCE walked, and every walk covers a whole number of strides (the stride is
/// stretched a few percent to fit), so a mouse stops on the page where both feet are down: no foot slides.
///
/// What it is solid to: the snow (feet on the lane in front of the tree), the tree (a mouse behind it is
/// hidden by it, one in front of it hides the bulbs), and the props nearer than the tree (the depth picture).
/// Claims: ground and sapling.
/// </summary>
internal sealed class MiceTrimTree : Story
{
    private readonly ChristmasWorld _w;
    public MiceTrimTree(ChristmasWorld w) { _w = w; }
    public override string Name => nameof(MiceTrimTree);
    public override string Claims => "ground,sapling";

    public override bool CanBegin
    {
        get
        {
            LogMice m = _w.Mice;
            if (!m.Ready || m.Count == 0 || !m.Decor.Built || m.Decor.Active || m.IsLent) return false;
            // The story waits for the resident's mice to be in; the first time it is asked, it calls them in.
            if (!m.AllHome) { m.RequestHome(); return false; }
            return true;
        }
    }

    // ---------------------------------------------------------------- the script ----

    private enum K { Hop, Walk, Stand, Spiral, Climb, Sit, Jump, Turn }

    private struct Seg
    {
        public float T0, T1;
        public K Kind;
        public PointF A, B;
        public int Dir, Dir0;          // facing (Dir0 is the old facing in a Turn)
        public bool Arms;              // forepaws up
        public int Prop;               // 0 none, 1 bauble, 2 coil, 3 star
        public float Stride;           // pixels in one walking stride, fitted to the length
        public float Arc;              // a hop's height in mouse units
        public bool Stencil;           // a hop through the hole of the log
        public int Near;               // nearness to stamp with; 0 means "the feet's own"
        public float Len;              // a spiral's path length
        public float Phase;            // a jump's offset
    }

    private sealed class Actor
    {
        public readonly List<Seg> Segs = new();
        public readonly MouseTail Tail = new();
        public float LastT = -1f;
    }

    private struct Frame
    {
        public bool Vis, Mirror, Stencil, Upright; public float X, Y, Key; public LogMiceArt.Book Book; public int Page; public int Near; public int Prop;
    }

    private const float Ramp = 0.2f;
    private Actor[] _a = null!;
    private float _end, _k;
    private float _tHangA, _tHangB, _tStarA, _tStarB, _tSpA, _tSpB, _tOn, _tFlare;
    private bool _noticed;
    private LogMiceArt.Sheet _sh = null!;

    /// <summary>Distance covered after a fraction x of a walk (0..1): a lean in, a steady stretch, a run-down.</summary>
    private static float Trap(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        if (x < Ramp) return x * x / (2f * Ramp * (1f - Ramp));
        if (x > 1f - Ramp) return 1f - (1f - x) * (1f - x) / (2f * Ramp * (1f - Ramp));
        return (x - Ramp / 2f) / (1f - Ramp);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public override float Begin(Random rng)
    {
        ChristmasScenery s = _w.Scenery;
        LogMice mice = _w.Mice;
        TreeDecor dec = mice.Decor;
        mice.Lend();
        _noticed = false;
        _sh = mice.Sheet(1);
        float k = _sh.K; _k = k;
        float U = s.U;
        PointF door = mice.Door, lip = mice.Lip, T = s.SaplingFoot;
        float H = s.SaplingHeight, su = H / 0.30f;
        float laneY = T.Y + 0.035f * U;
        float vW = 0.085f * U;                                              // walking speed, pixels a second
        float hwb = Brushwork.PineHalfWidth(T.Y - 0.04f * H, T.Y - H, T.Y, su);
        float xp = dec.BaubleAt.X;                                          // the pyramid stands under the bough the bauble hangs on
        float J(float a) => (float)(rng.NextDouble() * 2 - 1) * a;          // a little spread so no two plays match

        _a = new[] { new Actor(), new Actor(), new Actor() };

        // ---- helpers that append segments and return the time they end ----
        float Hop(Actor a, float t0, PointF from, PointF to, int dir, float dur, float arc, bool stencil = false, int prop = 0, bool arms = false, int near = 0)
        {
            a.Segs.Add(new Seg { T0 = t0, T1 = t0 + dur, Kind = K.Hop, A = from, B = to, Dir = dir, Arc = arc, Stencil = stencil, Prop = prop, Arms = arms, Near = near });
            return t0 + dur;
        }
        float Walk(Actor a, float t0, PointF from, PointF to, bool arms, int prop, int near = 0)
        {
            float L = Dist(from, to);
            int n = Math.Max(2, (int)MathF.Round(L / (LogMiceArt.WalkStride * k)));
            float dur = Math.Max(0.6f, L / (vW * (1f - Ramp * 0.5f)));
            a.Segs.Add(new Seg { T0 = t0, T1 = t0 + dur, Kind = K.Walk, A = from, B = to, Dir = to.X >= from.X ? 1 : -1, Arms = arms, Prop = prop, Stride = L / n * 1f, Near = near });
            return t0 + dur;
        }
        float Stand(Actor a, float t0, float t1, PointF at, int dir, bool arms, int prop, int near = 0)
        {
            if (t1 > t0) a.Segs.Add(new Seg { T0 = t0, T1 = t1, Kind = K.Stand, A = at, B = at, Dir = dir, Arms = arms, Prop = prop, Near = near });
            return Math.Max(t0, t1);
        }
        float Turn(Actor a, float t0, PointF at, int from, int to)
        {
            a.Segs.Add(new Seg { T0 = t0, T1 = t0 + 0.5f, Kind = K.Turn, A = at, B = at, Dir0 = from, Dir = to });
            return t0 + 0.5f;
        }

        int nTree = s.Nearness(T.Y), front = Math.Min(254, nTree + 2), behind = Math.Max(1, nTree - 3);
        PointF Lane(float x) => new(x, laneY);
        PointF xa = Lane(xp - 1.7f * k), xpt = Lane(xp), xc = Lane(T.X + hwb + 1.8f * k);
        PointF shoulder = new(xp, laneY - 1.12f * k);

        // ---- 1. the three come out of the hole, a little apart, and cross the snow ----
        Actor A = _a[0], B = _a[1], C = _a[2];
        float tOutA = 0f, tOutB = 1.9f + J(0.3f), tOutC = 3.7f + J(0.4f);
        float arrA = Walk(A, Hop(A, tOutA, lip, door, 1, 0.45f, 0.5f, true, 1), door, xa, true, 1);
        float arrB = Walk(B, Hop(B, tOutB, lip, door, 1, 0.45f, 0.5f, true, 2), door, xpt, false, 2);
        float arrC = Walk(C, Hop(C, tOutC, lip, door, 1, 0.45f, 0.5f, true, 3), door, xc, true, 3);
        // before they come out they are inside: nothing is drawn (a mouse is visible only inside its own segments)

        // ---- 2. the pyramid: A hops onto B's head and hangs the bauble ----
        float tPyr = Math.Max(arrA, arrB) + 0.5f;
        Stand(A, arrA, tPyr, xa, 1, true, 1);
        Stand(B, arrB, tPyr + 3.4f, xpt, 1, false, 2);
        float t = Hop(A, tPyr, xa, shoulder, 1, 0.5f, 0.45f, false, 1, true);
        t = Stand(A, t, t + 1.2f, shoulder, 1, true, 1);
        _tHangA = t; _tHangB = t + 0.6f;
        t = Stand(A, t, t + 0.6f, shoulder, 1, true, 0);                       // the bauble leaves its hands and flies to the bough
        t = Stand(A, t, t + 0.5f, shoulder, 1, false, 0);
        float tB1 = Hop(A, t, shoulder, xa, -1, 0.5f, 0.45f);
        // A lands facing left; it turns back to the tree while it watches
        float tA1 = Turn(A, tB1, xa, -1, 1);

        // ---- 3. B runs the spiral with the lights ----
        PointF sp0 = dec.Spiral(0f).At;
        float tS0 = Math.Max(tB1, tPyr + 3.4f);
        float tW = Walk(B, tS0, xpt, sp0, false, 2, front);
        _tSpA = tW; _tSpB = tW + 7.5f;
        float pathLen = 0f; PointF prev = sp0;
        for (int i = 1; i <= 80; i++) { PointF q = dec.Spiral(i / 80f).At; pathLen += Dist(prev, q); prev = q; }
        B.Segs.Add(new Seg { T0 = _tSpA, T1 = _tSpB, Kind = K.Spiral, Prop = 2, Len = pathLen });
        PointF spEnd = dec.Spiral(1f).At;
        PointF bLand = Lane(T.X - 0.7f * hwb - 0.6f * k);
        float tB2 = Hop(B, _tSpB, spEnd, bLand, -1, 0.9f, 0.2f, false, 0, false, front);
        float tB3 = Turn(B, tB2, bLand, -1, 1);

        // ---- 4. C climbs the far side and puts the star on the tip ----
        float tC0 = _tSpA + 1.0f;
        Stand(C, arrC, tC0, xc, 1, true, 3);
        PointF cEdge = new(T.X + hwb * 1.3f, T.Y - 2f);
        float tc = Walk(C, tC0, xc, cEdge, true, 3);
        float topY = T.Y - H;
        PointF cTop = new(T.X, topY + 0.16f * H);
        float tClimb = 5.2f;
        C.Segs.Add(new Seg { T0 = tc, T1 = tc + tClimb, Kind = K.Climb, A = cEdge, B = cTop, Dir = 1, Arms = true, Prop = 3, Near = behind, Stride = Dist(cEdge, cTop) / Math.Max(2, (int)MathF.Round(Dist(cEdge, cTop) / (LogMiceArt.WalkStride * k))) });
        tc += tClimb;
        tc = Stand(C, tc, tc + 1.3f, cTop, 1, true, 3, front);
        _tStarA = tc; _tStarB = tc + 0.6f;
        tc = Stand(C, tc, tc + 0.6f, cTop, 1, true, 0, front);
        tc = Stand(C, tc, tc + 0.7f, cTop, 1, false, 0, front);
        PointF cLand = Lane(T.X + 1.05f * hwb + 1.2f * k);
        tc = Hop(C, tc, cTop, cLand, 1, 1.0f, 0.2f, false, 0, false, front);

        // ---- 5. they stand back in a row and look up ----
        float rowX(int i) => T.X - 1.5f * hwb - (1.2f + 2.1f * i) * k;
        PointF[] row = { Lane(rowX(2)), Lane(rowX(1)), Lane(rowX(0)) };       // A is farthest left, C nearest the tree
        float eA = Walk(A, tA1, xa, row[0], false, 0);
        float eB = Walk(B, tB3, bLand, row[1], false, 0);
        float eC = Walk(C, tc, cLand, row[2], false, 0);
        // arrive facing left (they walked left), turn to face the tree and sit
        float rA = Turn(A, eA, row[0], -1, 1), rB = Turn(B, eB, row[1], -1, 1), rC = Turn(C, eC, row[2], -1, 1);
        float tRow = Math.Max(rA, Math.Max(rB, rC)) + 0.4f;
        _tOn = tRow + 0.6f;
        float tOnEnd = _tOn + 5.0f;
        _tFlare = tOnEnd;
        float tJump = tOnEnd + 0.3f, tJumpEnd = tJump + 3.6f;
        PointF[] doorPt = { door, door, door };
        for (int i = 0; i < 3; i++)
        {
            Actor a = _a[i]; float r = i == 0 ? rA : i == 1 ? rB : rC;
            Seg sit = new() { T0 = r, T1 = tJump, Kind = K.Sit, A = row[i], B = row[i], Dir = 1 };
            a.Segs.Add(sit);
            a.Segs.Add(new Seg { T0 = tJump, T1 = tJumpEnd, Kind = K.Jump, A = row[i], B = row[i], Dir = 1, Arms = true, Phase = i * 0.33f });
        }
        // ---- 6. home in a line ----
        float tHome = tJumpEnd + 0.3f;
        float last = 0f;
        for (int i = 0; i < 3; i++)
        {
            Actor a = _a[i];
            float t0 = tHome + i * 1.1f;
            float tt = Turn(a, t0, row[i], 1, -1);
            tt = Walk(a, tt, row[i], door, false, 0);
            tt = Hop(a, tt, door, lip, -1, 0.45f, 0.5f, true);
            last = Math.Max(last, tt);
        }
        _end = last + 0.6f;
        return _end;
    }

    public override void End()
    {
        // The mice are inside again; the tree stays trimmed (the resident draws it now) and the mice may come out.
        float keep = float.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_MICEDECOR"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kv) ? kv : 240f;   // test aid: seconds the tree stays lit
        _w.Mice.Decor.Keep(keep);
        _w.Mice.Unlend();
    }

    public override void Update(float t)
    {
        if (!_noticed && t >= _tOn + 5f)
        {
            _noticed = true;
            // the lights coming on is something to look at: a gentle movement notice on the tree
            _w.Board.Post(Sense.Movement, _w.Scenery.SaplingFoot, 0.35f * _w.Scenery.U, 0.35f, this);
        }
    }

    // ---------------------------------------------------------------- evaluating a mouse at time t ----

    private bool Eval(Actor a, float t, out Frame f)
    {
        f = default;
        ChristmasScenery s = _w.Scenery;
        float k = _k;
        foreach (Seg g in a.Segs)
        {
            if (t < g.T0 || t >= g.T1) continue;
            float u = (t - g.T0) / (g.T1 - g.T0);
            f.Vis = true; f.Prop = g.Prop; f.Near = g.Near; f.Mirror = g.Dir < 0; f.Stencil = g.Stencil;
            f.X = g.A.X; f.Y = g.A.Y;
            switch (g.Kind)
            {
                case K.Hop:
                    float ue = Organic.Smooth(u);                // eased along the ground: it leaves softly and lands softly
                    f.X = g.A.X + (g.B.X - g.A.X) * ue; f.Y = g.A.Y + (g.B.Y - g.A.Y) * ue - g.Arc * k * MathF.Sin(MathF.PI * u);
                    // these mice carry things standing up, so they hop standing up (the all-fours leap page was a different animal)
                    f.Book = g.Arms ? LogMiceArt.Book.WalkUp : LogMiceArt.Book.Walk; f.Page = 0; f.Upright = true;
                    break;
                case K.Walk: case K.Climb:
                {
                    float L = Dist(g.A, g.B), sd = L * Trap(u);
                    float fr = L > 1e-3f ? sd / L : 0f;
                    f.X = g.A.X + (g.B.X - g.A.X) * fr; f.Y = g.A.Y + (g.B.Y - g.A.Y) * fr;
                    float cyc = sd / g.Stride; cyc -= MathF.Floor(cyc);
                    f.Book = g.Arms ? LogMiceArt.Book.WalkUp : LogMiceArt.Book.Walk; f.Page = (int)(cyc * LogMiceArt.WalkPages) % LogMiceArt.WalkPages;
                    f.Upright = true;
                    break;
                }
                case K.Stand:
                    f.Book = g.Arms ? LogMiceArt.Book.WalkUp : LogMiceArt.Book.Walk; f.Page = 0; f.Upright = true;
                    break;
                case K.Spiral:
                {
                    var (p, cs) = _w.Mice.Decor.Spiral(u);
                    var (p2, _) = _w.Mice.Decor.Spiral(Math.Min(1f, u + 0.01f));
                    f.X = p.X; f.Y = p.Y;
                    int nTree = s.Nearness(s.SaplingFoot.Y);
                    f.Near = cs > 0.12f ? Math.Min(254, nTree + 2) : Math.Max(1, nTree - 3);
                    int dir = (p2.X - p.X) >= 0 ? 1 : -1; if (MathF.Abs(p2.X - p.X) < 0.02f) dir = cs > 0 ? 1 : -1;
                    f.Mirror = dir < 0;
                    float cyc = g.Len * u / (LogMiceArt.WalkStride * k); cyc -= MathF.Floor(cyc);
                    f.Book = LogMiceArt.Book.Walk; f.Page = (int)(cyc * LogMiceArt.WalkPages) % LogMiceArt.WalkPages; f.Upright = true;
                    break;
                }
                case K.Sit:
                    f.Book = LogMiceArt.Book.Sit; f.Page = ((int)(t * 3.3f + a.Segs.Count)) % 4; f.Mirror = false;
                    break;
                case K.Jump:
                {
                    float ph = t * 1.7f + g.Phase;
                    float hop = MathF.Abs(MathF.Sin(MathF.PI * ph));
                    f.Y = g.A.Y - 0.75f * k * hop;
                    f.Book = LogMiceArt.Book.WalkUp; f.Page = 0; f.Upright = true;
                    break;
                }
                case K.Turn:
                    int tp = Math.Min(12, (int)(u * 13f));       // thirteen poses over the turn: standing, five in-betweens, head on, five back out, standing
                    if (tp == 0 || tp == 12) { f.Book = LogMiceArt.Book.Walk; f.Page = 0; }
                    else if (tp == 6) { f.Book = LogMiceArt.Book.Front; f.Page = 0; }
                    else { f.Book = LogMiceArt.Book.TurnUp; f.Page = tp < 6 ? tp - 1 : 11 - tp; }
                    f.Mirror = (u < 0.5f ? g.Dir0 : g.Dir) < 0;
                    if (f.Book == LogMiceArt.Book.Front) f.Mirror = false;
                    f.Upright = true;
                    break;
            }
            f.Key = f.Y;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- drawing ----

    public override void Draw(FrameBuffer fb, float t)
    {
        ChristmasScenery s = _w.Scenery;
        LogMice mice = _w.Mice;
        TreeDecor dec = mice.Decor;
        float k = _k;
        bool bauble = t >= _tHangB, star = t >= _tStarB;
        float lit = t < _tOn ? 0f : Math.Min(1f, (t - _tOn) / 5.0f) * TreeDecor.Bulbs;
        float wound = t < _tSpA ? 0f : Math.Min(1f, (t - _tSpA) / (_tSpB - _tSpA));
        float flare = star ? (t < _tFlare ? 0f : Math.Max(0f, 1f - (t - _tFlare) / 2.0f)) : -1f;
        dec.Draw(fb, lit, bauble || star, flare, t, 1f, bauble, star, wound);

        // the mice, far to near (a mouse riding another's head is drawn after its carrier)
        Span<int> order = stackalloc int[3];
        Span<Frame> fr = stackalloc Frame[3];
        Span<bool> vis = stackalloc bool[3];
        for (int i = 0; i < 3; i++) { order[i] = i; vis[i] = Eval(_a[i], t, out fr[i]); }
        for (int i = 1; i < 3; i++)
            for (int j = i; j > 0 && fr[order[j]].Key < fr[order[j - 1]].Key; j--) (order[j], order[j - 1]) = (order[j - 1], order[j]);
        for (int oi = 0; oi < 3; oi++)
        {
            int i = order[oi];
            if (!vis[i]) { _a[i].Tail.Reset(); continue; }
            DrawMouse(fb, _a[i], fr[i], t);
        }

        // the bauble and the star in flight from the hands to their places
        PointF aHand = new(dec.BaubleAt.X, 0);
        if (t >= _tHangA && t < _tHangB && dec.BaubleSprite != null)
        {
            float u = Organic.Smooth((t - _tHangA) / (_tHangB - _tHangA));
            PointF from = new(dec.BaubleAt.X, s.SaplingFoot.Y + 0.035f * s.U - 1.12f * k - 1.5f * k - dec.BaubleR);
            dec.BaubleSprite.DrawCentered(fb, from.X + (dec.BaubleAt.X - from.X) * u, from.Y + (dec.BaubleAt.Y - from.Y) * u, 1f, null, false, s.NearDepth, Math.Min(254, s.Nearness(s.SaplingFoot.Y) + 1));
        }
        if (t >= _tStarA && t < _tStarB && dec.StarSprite != null)
        {
            float u = Organic.Smooth((t - _tStarA) / (_tStarB - _tStarA));
            PointF from = new(dec.StarAt.X, dec.StarAt.Y + 0.28f * dec.Height);
            dec.StarSprite.DrawCentered(fb, from.X, from.Y + (dec.StarAt.Y - from.Y) * u, 1f, null, false, s.NearDepth, Math.Min(254, s.Nearness(s.SaplingFoot.Y) + 1));
        }

        // sparkles falling from the star when the lights are all on
        if (dec.Spark != null && t >= _tFlare && t < _tFlare + 3.6f)
        {
            for (int i = 0; i < 18; i++)
            {
                float t0 = _tFlare + i * 0.13f, age = t - t0;
                if (age < 0f || age > 1.5f) continue;
                float hx = MathF.Sin(i * 12.9898f) * 43758.5453f; hx -= MathF.Floor(hx);
                float x = dec.StarAt.X + (hx - 0.5f) * 0.30f * dec.Height + MathF.Sin(age * 5f + i) * 2f;
                float y = dec.StarAt.Y + age * 0.25f * dec.Height * (0.7f + 0.6f * hx);
                dec.Spark.DrawCentered(fb, x, y, 0.9f * (1f - age / 1.5f), null, false, s.NearDepth, Math.Min(254, s.Nearness(s.SaplingFoot.Y) + 1));
            }
        }
        CaptureFrame(fb, t);
    }

    // TEST AID: PECKWORKS_STORYCAP="fromStorySecond|toStorySecond|folder[|centreX|centreY|width|height]" saves a crop of every frame in that
    // window of the STORY's own clock (default: round the little fir) as frameNNNN.png for the contact-sheet tool.
    private readonly string? _cap = Environment.GetEnvironmentVariable("PECKWORKS_STORYCAP");
    private int _capN;
    private void CaptureFrame(FrameBuffer fb, float t)
    {
        if (_cap == null) return;
        string[] p = _cap.Split('|');
        if (p.Length < 3) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (t < float.Parse(p[0], ci) || t > float.Parse(p[1], ci)) return;
        PointF T = _w.Scenery.SaplingFoot;
        int w = p.Length > 5 ? int.Parse(p[5]) : 260, h = p.Length > 6 ? int.Parse(p[6]) : 240;
        int cx = p.Length > 4 ? int.Parse(p[3]) : (int)T.X, cy = p.Length > 4 ? int.Parse(p[4]) : (int)T.Y - h / 3;
        using var bmp = new Bitmap(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = Math.Clamp(cx - w / 2 + x, 0, fb.Width - 1), sy = Math.Clamp(cy - h / 2 + y, 0, fb.Height - 1);
                bmp.SetPixel(x, y, Color.FromArgb(255, Color.FromArgb((int)fb.Pixels[sy * fb.Width + sx])));
            }
        Directory.CreateDirectory(p[2]);
        bmp.Save(Path.Combine(p[2], $"frame{_capN++:0000}.png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    private void DrawMouse(FrameBuffer fb, Actor a, Frame f, float t)
    {
        ChristmasScenery s = _w.Scenery;
        LogMiceArt.Sheet sh = _sh;
        float k = sh.K;
        Sprite page = sh.Page(f.Book, f.Page);
        int near = f.Stencil ? 254 : f.Near > 0 ? f.Near : s.Nearness(f.Y);
        byte[]? depth = f.Stencil ? null : s.NearDepth;
        bool[]? st = f.Stencil ? _w.Mice.DoorStencil : null;

        // the tail: rooted on the page's rump, dragged along
        MouseAnchors an = sh.Anchor(f.Book, f.Page);
        float sg = f.Mirror ? -1f : 1f;
        if (f.Book != LogMiceArt.Book.Front)
        {
            float dt = a.LastT < 0f ? 0.016f : Math.Clamp(t - a.LastT, 0f, 0.1f);
            a.Tail.Update(new PointF(f.X + sg * an.Tail.X * k, f.Y - an.Tail.Y * k), sg, f.Y, 0.30f * k, dt, 0.4f);
            a.Tail.Draw(fb, MathF.Max(1.2f, 0.11f * k), depth, near, st);
        }
        a.LastT = t;

        int left = (int)MathF.Round(f.X) - (f.Mirror ? page.Width - 1 - sh.OriginX : sh.OriginX), top = (int)MathF.Round(f.Y) - sh.OriginY;
        page.Draw(fb, left, top, 1f, st, f.Mirror, depth, near);

        // what it carries
        if (f.Prop != 0)
        {
            PointF hand = new(f.X + sg * an.Hand.X * k, f.Y - an.Hand.Y * k);
            TreeDecor dec = _w.Mice.Decor;
            if (!f.Upright) hand = new PointF(f.X, f.Y - 0.85f * k);
            switch (f.Prop)
            {
                case 1 when dec.BaubleSprite != null:
                    dec.BaubleSprite.DrawCentered(fb, hand.X, hand.Y - dec.BaubleR * 0.9f, 1f, st, false, depth, near);
                    break;
                case 3 when dec.StarSprite != null:
                    dec.StarSprite.DrawCentered(fb, hand.X, hand.Y - dec.StarSprite.Height * 0.42f, 1f, st, false, depth, near);
                    break;
                case 2:
                    DrawCoil(fb, hand, k, depth, near, st);
                    break;
            }
        }
    }

    /// <summary>The coil of fairy lights: a ring of small coloured bulbs, held at the mouse's chest.</summary>
    private void DrawCoil(FrameBuffer fb, PointF at, float k, byte[]? depth, int near, bool[]? st)
    {
        // a ring of bright small coloured dots (one of each of the eight bulb colours) round a thin cord
        TreeDecor dec = _w.Mice.Decor;
        PointF c = new(at.X, at.Y - 0.12f * k);
        float rx = 0.34f * k, ry = 0.28f * k;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f, a2 = (i + 1) * MathF.Tau / 8f;
            fb.Line(new PointF(c.X + MathF.Cos(a) * rx, c.Y + MathF.Sin(a) * ry), new PointF(c.X + MathF.Cos(a2) * rx, c.Y + MathF.Sin(a2) * ry), Color.FromArgb(120, 190, 130), 0.9f, 1f, st, depth, near);
        }
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            dec.SmallGlow(i)?.DrawCentered(fb, c.X + MathF.Cos(a) * rx, c.Y + MathF.Sin(a) * ry, 1f, st, false, depth, near);
        }
    }
}
