using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A Japanese squirrel (nihon risu) scampers in along one of the cherry
/// branches that reach in from the edge of the screen, in quick bursts with
/// stops between. Part way along it sits up on its haunches with its huge
/// fluffy tail curled up behind, nibbles a tiny pink blossom held in its
/// paws, flicks its tail, then drops down and dashes back the way it came.
///
/// FEYNMAN VERSION, in three jobs.
///
/// 1. FINDING A BRANCH. The painter does not hand over the branches as lines,
///    only the finished picture, so we read the picture. A branch is dark
///    brown wood. In the constructor we walk down the left and right edges
///    and along the top, and wherever there is wood on the very edge, a
///    branch comes in there. From that point we "follow" it, like walking
///    along a path in the dark by feeling for the ground: take a small step
///    in the direction we were going; if the ground ahead is wood, good;
///    if not, try a little to the left or right; each step is re-centred on
///    the middle of the wood. Under a clump of blossom the wood is hidden,
///    so we are allowed to walk straight on across blossom for a short way.
///    A path that is too short or too steep is thrown away. What is left is a
///    list of points: a trail the squirrel can run along.
///
/// 2. THE PICTURES. A flip-book of the squirrel: 16 pages of its run (the
///    body stretches out and gathers up, the legs reach and tuck), one
///    "standing alert" page, two pages of it rising, four of nibbling and
///    four of the tail flicking. All of them come from ONE drawing routine
///    with four dials: how far through the run, how upright (0 running, 1
///    sitting), how far the head is dipped to nibble, and the tail's sway. A
///    sprite cannot be turned after it is painted, so the book is painted at
///    three slopes (climbing, level, falling) and in both directions, but
///    only the combinations the real trails need.
///
/// 3. THE TIMETABLE (all from t). The run is "bursts": run, run, run, with a
///    pause after each. Distance along the trail is worked out from t, and
///    the legs follow the distance (so the feet do not slide).
/// </summary>
internal sealed class Squirrel : Happening
{
    private const float Total = 11f;
    private const int PRun = 0, PStand = 16, PRise1 = 17, PRise2 = 18, PNib = 19, PSway = 23, PageCount = 27;
    private const float Slope = 25f;                    // the book is painted at -25, 0 and +25 degrees

    private static readonly Color Coat = Color.FromArgb(184, 98, 52);
    private static readonly Color CoatLight = Color.FromArgb(214, 138, 82);
    private static readonly Color CoatDark = Color.FromArgb(120, 58, 32);
    private static readonly Color Cream = Color.FromArgb(252, 236, 208);
    private static readonly Color Ink = Color.FromArgb(34, 20, 22);

    private readonly Scenery _s;
    private readonly float _k;                          // pixels per drawing unit (about 1.1 units = 0.04 U, the whole squirrel with tail)
    private readonly int _w, _h, _ox, _oy;
    private readonly List<PointF[]> _trails = [];       // trails along branches, from off screen inward
    private readonly List<float[]> _cum = [];           // the distance travelled at each trail point
    private readonly Dictionary<(int Flip, int Bin), Sprite[]> _book = [];

    private bool _ok;
    private int _trail;
    private float _sitAt;                               // how far along the trail it sits up
    private float _stride;                              // run distance for one 16-page leg cycle

    public override float Seconds => _ok ? Total : 0.1f;
    public override string? Claims => "branches";
    public override int Layer => 1;
    public override bool CanBegin => _trails.Count > 0;

    public Squirrel(Scenery s)
    {
        _s = s;
        _k = Math.Max(18f, s.U * 0.036f);
        _w = (int)(2.5f * _k) + 8;
        _h = (int)(2.1f * _k) + 8;
        _ox = _w / 2;
        _oy = _h - (int)(0.4f * _k) - 4;

        FindTrails();

        // Paint only the slope/direction pages the trails will need (going in AND coming back).
        foreach (var tr in _trails)
        {
            for (int i = 4; i < tr.Length - 4; i += 6)
            {
                for (int dir = 0; dir < 2; dir++)
                {
                    var key = Key(tr[i - 3], tr[i + 3], dir == 0);
                    if (_book.ContainsKey(key)) continue;
                    var pages = new Sprite[PageCount];
                    for (int p = 0; p < PageCount; p++) pages[p] = PaintPage(key.Flip == 1, key.Bin * Slope, p);
                    _book[key] = pages;
                }
            }
        }
    }

    /// <summary>Which book to use for a stretch of trail from a to b, travelling forward (a to b) or back (b to a).</summary>
    private static (int Flip, int Bin) Key(PointF a, PointF b, bool forward)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        if (!forward) { dx = -dx; dy = -dy; }
        float up = MathF.Atan2(-dy, MathF.Abs(dx)) * 180f / MathF.PI;      // positive when climbing in the direction it faces
        return (dx >= 0 ? 0 : 1, Math.Clamp((int)MathF.Round(up / Slope), -1, 1));
    }

    // ================================================================ finding the branches

    private bool Wood(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _s.Width || iy >= _s.Height) return false;
        uint p = _s.Pixels[iy * _s.Width + ix];
        int r = (int)((p >> 16) & 0xFF), g = (int)((p >> 8) & 0xFF), b = (int)(p & 0xFF);
        return r < 125 && g < 100 && b < 110 && r >= g - 2;
    }

    private bool Blossom(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _s.Width || iy >= _s.Height) return false;
        uint p = _s.Pixels[iy * _s.Width + ix];
        return (int)((p >> 16) & 0xFF) >= (int)(p & 0xFF) + 6;
    }

    private void FindTrails()
    {
        int w = _s.Width, h = _s.Height;
        var entries = new List<(PointF At, PointF In)>();
        // Left edge, right edge: runs of wood going down the column. Top edge: runs going along the row.
        void Scan(int count, Func<int, (float X, float Y)> at, PointF inward)
        {
            int start = -1;
            for (int i = 0; i <= count; i++)
            {
                bool wood = i < count && Wood(at(i).X, at(i).Y);
                if (wood && start < 0) start = i;
                if (!wood && start >= 0)
                {
                    if (i - start >= 2) { var m = at((start + i - 1) / 2); entries.Add((new PointF(m.X, m.Y), inward)); }
                    start = -1;
                }
            }
        }
        Scan((int)(h * 0.65f), i => (1, i), new PointF(1, 0));
        Scan((int)(h * 0.65f), i => (w - 2, i), new PointF(-1, 0));
        Scan(w, i => (i, 1), new PointF(0, 1));

        foreach (var (at, inward) in entries)
        {
            var pts = Trace(at, inward);
            if (pts == null) continue;
            _trails.Add(pts);
            var cum = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++)
                cum[i] = cum[i - 1] + MathF.Sqrt((pts[i].X - pts[i - 1].X) * (pts[i].X - pts[i - 1].X) + (pts[i].Y - pts[i - 1].Y) * (pts[i].Y - pts[i - 1].Y));
            _cum.Add(cum);
        }
    }

    /// <summary>Follows a branch inward from an edge point by feeling for wood; null if it is too short or too steep to run on.</summary>
    private PointF[]? Trace(PointF start, PointF inward)
    {
        float u = _s.U, step = Math.Max(2f, 0.006f * u);
        var pts = new List<PointF> { start };
        PointF p = start, dir = inward;
        int gap = 0, maxGap = (int)(0.05f * u / step);
        float len = 0;
        float[] tries = [0, 8, -8, 16, -16, 26, -26, 38, -38];
        while (len < 0.5f * u)
        {
            bool moved = false;
            PointF q = p;
            foreach (float a in tries)
            {
                float r = a * MathF.PI / 180f, c = MathF.Cos(r), sn = MathF.Sin(r);
                PointF d = new(dir.X * c - dir.Y * sn, dir.X * sn + dir.Y * c);
                q = new PointF(p.X + d.X * step, p.Y + d.Y * step);
                if (!Wood(q.X, q.Y)) continue;
                // Re-centre on the middle of the wood, across the branch.
                PointF n = new(-d.Y, d.X);
                int up = 0, dn = 0, lim = (int)(0.025f * u);
                while (up < lim && Wood(q.X + n.X * (up + 1), q.Y + n.Y * (up + 1))) up++;
                while (dn < lim && Wood(q.X - n.X * (dn + 1), q.Y - n.Y * (dn + 1))) dn++;
                q = new PointF(q.X + n.X * (up - dn) / 2f, q.Y + n.Y * (up - dn) / 2f);
                float nx = dir.X * 0.7f + d.X * 0.3f, ny = dir.Y * 0.7f + d.Y * 0.3f, nl = MathF.Sqrt(nx * nx + ny * ny);
                dir = new PointF(nx / nl, ny / nl);
                gap = 0; moved = true;
                break;
            }
            if (!moved)
            {
                // Hidden under blossom: walk straight on for a short way.
                q = new PointF(p.X + dir.X * step, p.Y + dir.Y * step);
                if (gap < maxGap && Blossom(q.X, q.Y)) { gap++; moved = true; }
            }
            if (!moved || q.X < 0 || q.Y < 0 || q.X >= _s.Width || q.Y >= _s.Height) break;
            pts.Add(q); len += step; p = q;
        }
        if (len < 0.26f * u) return null;

        // Smooth the little wobbles (average each point with its neighbours), keeping the two ends.
        var sm = new PointF[pts.Count];
        for (int i = 0; i < sm.Length; i++)
        {
            int a = Math.Max(0, i - 2), b = Math.Min(sm.Length - 1, i + 2);
            float sx = 0, sy = 0;
            for (int j = a; j <= b; j++) { sx += pts[j].X; sy += pts[j].Y; }
            sm[i] = new PointF(sx / (b - a + 1), sy / (b - a + 1));
        }
        // Too steep anywhere? A squirrel does not run up a wall.
        for (int i = 4; i < sm.Length; i++)
        {
            float dx = sm[i].X - sm[i - 4].X, dy = sm[i].Y - sm[i - 4].Y;
            if (MathF.Abs(MathF.Atan2(dy, MathF.Abs(dx) + 0.001f)) > 41f * MathF.PI / 180f) return null;
        }
        // A run-up from off screen: extra points straight back out past the edge.
        int lead = (int)(0.08f * u / step) + 1;
        var all = new PointF[sm.Length + lead];
        for (int i = 0; i < lead; i++)
            all[i] = new PointF(start.X - inward.X * step * (lead - i), start.Y - inward.Y * step * (lead - i));
        Array.Copy(sm, 0, all, lead, sm.Length);
        return all;
    }

    // ================================================================ timetable

    public override void Begin(Random rng)
    {
        _ok = false;
        if (_trails.Count == 0) return;
        _trail = rng.Next(_trails.Count);
        _sitAt = _cum[_trail][^1] * 0.72f;
        _stride = 0.09f * _s.U;
        _ok = true;
    }

    /// <summary>
    /// Run in bursts: n equal slots, each is "run (eased) for 60 percent, then pause".
    /// Returns how far along (0 to 1) and whether it is running right now.
    /// </summary>
    private static (float Done, bool Running) Bursts(float t, float total, int n)
    {
        if (t >= total) return (1f, false);
        if (t <= 0) return (0f, false);
        float slot = total / n, f = t / slot;
        int k = (int)MathF.Floor(f);
        float within = f - k;
        const float mv = 0.6f;
        float g = Smooth(within / mv);
        return ((k + g) / n, within > 0.02f && within < mv * 0.97f);
    }

    private PointF At(int trail, float s)
    {
        var pts = _trails[trail]; var cum = _cum[trail];
        s = Math.Clamp(s, 0, cum[^1]);
        int i = Array.BinarySearch(cum, s);
        if (i < 0) i = ~i;
        i = Math.Clamp(i, 1, cum.Length - 1);
        float span = cum[i] - cum[i - 1], q = span > 0 ? (s - cum[i - 1]) / span : 0;
        return new PointF(pts[i - 1].X + (pts[i].X - pts[i - 1].X) * q, pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * q);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float u = _s.U;
        float total = _cum[_trail][^1];
        const float inT = 2.4f, riseT = 0.4f, sitT = 4.6f, dropT = 0.4f, outT = 2.8f;
        float t1 = inT, t2 = t1 + riseT, t3 = t2 + sitT, t4 = t3 + dropT;   // 2.4, 2.8, 7.4, 7.8

        float s;
        bool forward;
        int page;
        if (t < t1)
        {
            var (done, running) = Bursts(t, inT, 3);
            s = _sitAt * done; forward = true;
            page = running ? RunPage(s) : PStand;
        }
        else if (t < t4)
        {
            s = _sitAt; forward = true;
            if (t < t2) page = (t - t1) < riseT / 2 ? PRise1 : PRise2;
            else if (t >= t3) page = (t - t3) < dropT / 2 ? PRise2 : PRise1;
            else
            {
                float ts = t - t2;
                // Nibble in two spells (head bobbing about 5 times a second), then a flick of the tail.
                bool nibbling = (ts > 0.4f && ts < 1.9f) || (ts > 2.5f && ts < 3.5f);
                if (ts > 3.7f && ts < 4.3f)
                {
                    float sw = MathF.Sin(MathF.Tau * (ts - 3.7f) / 0.6f);
                    page = PSway + (sw < -0.5f ? 0 : sw < 0 ? 1 : sw < 0.5f ? 2 : 3);
                    // (sway pages are -1, -0.5, +0.5, +1)
                }
                else if (nibbling)
                {
                    float nb = 0.5f - 0.5f * MathF.Cos(MathF.Tau * 5.5f * ts);
                    page = PNib + (int)MathF.Round(nb * 3f);
                }
                else page = PNib;
            }
        }
        else
        {
            var (done, running) = Bursts(t - t4, outT, 3);
            s = _sitAt * (1 - done); forward = false;
            page = running ? RunPage(_sitAt - s) : (t - t4 < outT ? PStand : PStand);
            if (t - t4 >= outT) return;                    // gone off the screen
        }

        PointF here = At(_trail, s);
        PointF ahead = At(_trail, s + 0.05f * u), behind = At(_trail, s - 0.05f * u);
        var key = Key(behind, ahead, forward);
        if (!_book.TryGetValue(key, out var pages))
        {
            // The trail changed slope more than the sampling saw: use the nearest level book we have.
            pages = _book.TryGetValue((key.Flip, 0), out var lvl) ? lvl : _book.Values.First();
        }
        float lift = 0.003f * u;
        pages[page].Draw(fb, (int)MathF.Round(here.X - _ox), (int)MathF.Round(here.Y - lift - _oy));
    }

    /// <summary>The leg-cycle page for a run distance d: one full cycle every _stride, so the feet keep pace with the branch.</summary>
    private int RunPage(float d) => (int)(((d / _stride) % 1f + 1f) % 1f * 16f) % 16;

    // ================================================================ painting

    private Sprite PaintPage(bool flip, float tiltDeg, int page)
    {
        float run = 0.5f, up = 0.25f, nib = 0f, sway = 0f;
        if (page < PStand) { run = page / 16f; up = 0f; }
        else if (page == PStand) { run = 0.5f; up = 0.25f; }
        else if (page == PRise1) { up = 0.45f; }
        else if (page == PRise2) { up = 0.8f; }
        else if (page < PSway) { up = 1f; nib = (page - PNib) / 3f; }
        else { up = 1f; sway = new[] { -1f, -0.5f, 0.5f, 1f }[page - PSway]; }

        return Sprite.Paint(_w, _h, g =>
        {
            if (flip) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_ox, _oy);
            g.RotateTransform(-tiltDeg);                  // lean with the branch (climbing = nose up)
            g.ScaleTransform(_k, _k);
            Squirrel_(g, run, up, nib, sway);
        });
    }

    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>
    /// The squirrel, facing right, feet at y = 0, in "squirrel units" (about the
    /// length of its body and head together, so the whole animal with tail is
    /// roughly 1.1 units). Four dials:
    ///   run   0 to 1 around one leg cycle;
    ///   up    0 running on all fours, 1 sitting bolt upright;
    ///   nib   0 to 1, the head dipping to nibble;
    ///   sway  -1 to 1, the tail swung back or forward.
    /// </summary>
    private static void Squirrel_(Graphics g, float run, float up, float nib, float sway)
    {
        float cyc = MathF.Tau * run;
        float arch = MathF.Cos(cyc);                      // 1 stretched out, -1 gathered up

        // ---- the skeleton: hip, chest, head ----
        float angD = 10f + 64f * up;                      // how steeply the body slopes up toward the head
        float ang = angD * MathF.PI / 180f;
        PointF hip = new(-0.12f + 0.05f * up, -0.17f - (1 - up) * 0.035f * (1 - arch) - 0.04f * up);
        PointF chest = new(hip.X + 0.30f * MathF.Cos(ang), hip.Y - 0.30f * MathF.Sin(ang));
        float hd = (18f + 20f * up) * MathF.PI / 180f;
        PointF head = new(chest.X + 0.12f * MathF.Cos(hd) + nib * 0.035f, chest.Y - 0.12f * MathF.Sin(hd) + nib * 0.05f);
        PointF mid = new((hip.X + chest.X) / 2, (hip.Y + chest.Y) / 2);

        // ---- the tail (behind everything): a chain of fluffy circles along a curve ----
        PointF b = new(hip.X - 0.07f, hip.Y - 0.02f);
        PointF[] run3 = [new(0, 0), new(-0.26f, -0.12f), new(-0.46f, 0.03f), new(-0.62f, -0.18f)];
        PointF[] sit3 = [new(0, 0), new(-0.30f, -0.04f), new(-0.34f, -0.52f), new(-0.06f, -0.70f)];
        var c = new PointF[4];
        for (int i = 0; i < 4; i++) c[i] = Lerp(run3[i], sit3[i], up);
        float wave = (1 - up) * 0.04f * MathF.Sin(cyc + 1f);
        c[2].Y += wave; c[3].Y += wave * 1.6f;
        float sr = sway * 9f * MathF.PI / 180f;
        for (int i = 1; i < 4; i++)                       // swing the tail about its root
        {
            float x = c[i].X, y = c[i].Y;
            c[i] = new PointF(x * MathF.Cos(sr) - y * MathF.Sin(sr), x * MathF.Sin(sr) + y * MathF.Cos(sr));
        }
        const int n = 18;
        var tp = new PointF[n + 1]; var tr = new float[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float s = i / (float)n, m = 1 - s;
            float x = m * m * m * c[0].X + 3 * m * m * s * c[1].X + 3 * m * s * s * c[2].X + s * s * s * c[3].X;
            float y = m * m * m * c[0].Y + 3 * m * m * s * c[1].Y + 3 * m * s * s * c[2].Y + s * s * s * c[3].Y;
            tp[i] = new PointF(b.X + x, b.Y + y);
            tr[i] = 0.075f + 0.075f * MathF.Sin(MathF.PI * MathF.Min(1f, s * 0.95f + 0.1f));      // thin root, fat middle, rounded tip
        }
        using (var dark = new SolidBrush(CoatDark))
            for (int i = 0; i <= n; i++) Disc(g, dark, tp[i], tr[i] + 0.022f);
        using (var coat = new SolidBrush(Coat))
            for (int i = 0; i <= n; i++) Disc(g, coat, tp[i], tr[i]);
        using (var lite = new SolidBrush(Color.FromArgb(150, CoatLight)))
            for (int i = 2; i <= n; i += 2) Disc(g, lite, new PointF(tp[i].X + 0.01f, tp[i].Y - tr[i] * 0.35f), tr[i] * 0.55f);

        // ---- hind leg (far side), then body ----
        PointF hFoot = Lerp(new PointF(hip.X + 0.10f * MathF.Cos(cyc + 0.3f) - 0.02f, -0.035f - 0.075f * MathF.Max(0, MathF.Sin(cyc + 0.3f))), new PointF(hip.X + 0.17f, -0.035f), up);
        Limb(g, hip, hFoot, 0.075f, CoatDark);
        g.TranslateTransform(mid.X, mid.Y);
        g.RotateTransform(-angD);
        using (var body = new SolidBrush(Coat)) g.FillEllipse(body, -0.27f, -0.145f, 0.54f, 0.29f);
        using (var belly = new SolidBrush(Cream)) g.FillEllipse(belly, -0.06f, 0.0f, 0.30f, 0.14f);   // cream underside toward the chest
        using (var back = new SolidBrush(Color.FromArgb(120, CoatDark))) g.FillEllipse(back, -0.22f, -0.15f, 0.40f, 0.07f);
        g.ResetTransform2(mid, angD);

        // ---- hind leg (near side) and thigh ----
        PointF hFoot2 = Lerp(new PointF(hip.X + 0.10f * MathF.Cos(cyc) - 0.02f, -0.035f - 0.075f * MathF.Max(0, MathF.Sin(cyc))), new PointF(hip.X + 0.20f, -0.03f), up);
        Limb(g, hip, hFoot2, 0.085f, Coat);
        Disc(g, Coat, new PointF(hip.X + 0.02f, hip.Y + 0.03f), 0.095f);
        FootPad(g, hFoot2, 0.045f);

        // ---- front legs: reaching when running, holding the blossom to the mouth when sitting ----
        PointF paw = new(head.X + 0.085f, head.Y + 0.085f);
        for (int side = 0; side < 2; side++)
        {
            float ph = cyc + 0.7f + side * 0.35f;
            PointF fRun = new(chest.X + 0.10f * MathF.Cos(ph) + 0.06f, -0.035f - 0.075f * MathF.Max(0, MathF.Sin(ph)));
            PointF fSit = new(paw.X - 0.03f * side, paw.Y + 0.01f * side);
            PointF f = Lerp(fRun, fSit, up);
            Limb(g, new PointF(chest.X - 0.02f, chest.Y + 0.05f), f, 0.065f, side == 0 ? CoatDark : Coat);
            Disc(g, Cream, f, 0.032f);
        }
        if (up > 0.7f) Blossom(g, new PointF(paw.X + 0.01f, paw.Y - 0.015f), 0.034f);

        // ---- head ----
        // Far ear, near ear: small rounded ears with a dark tuft on each tip.
        Ear(g, new PointF(head.X - 0.07f, head.Y - 0.11f), -12f, true);
        Disc(g, Coat, head, 0.125f);
        using (var cheek = new SolidBrush(Cream)) g.FillEllipse(cheek, head.X + 0.01f, head.Y + 0.02f, 0.15f, 0.09f);     // pale muzzle
        Ear(g, new PointF(head.X - 0.015f, head.Y - 0.125f), 6f, false);
        Disc(g, Ink, new PointF(head.X + 0.128f, head.Y + 0.03f), 0.026f);                                              // nose
        using (var blush = new SolidBrush(Color.FromArgb(110, 255, 140, 140))) g.FillEllipse(blush, head.X - 0.01f, head.Y + 0.035f, 0.07f, 0.045f);
        PointF eye = new(head.X + 0.055f, head.Y - 0.02f);
        Disc(g, Ink, eye, 0.04f);                                                                                       // big dark eye
        Disc(g, Color.White, new PointF(eye.X + 0.012f, eye.Y - 0.015f), 0.014f);                                         // highlight
    }

    private static void Disc(Graphics g, Color c, PointF p, float r) { using var b = new SolidBrush(c); g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2); }
    private static void Disc(Graphics g, Brush b, PointF p, float r) => g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);

    private static void Limb(Graphics g, PointF a, PointF z, float w, Color c)
    {
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, a, z);
    }

    private static void FootPad(Graphics g, PointF p, float r) => Disc(g, CoatDark, p, r);

    private static void Ear(Graphics g, PointF at, float tilt, bool far)
    {
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.RotateTransform(tilt);
        using (var b = new SolidBrush(far ? CoatDark : Coat)) g.FillEllipse(b, -0.04f, -0.09f, 0.08f, 0.11f);
        using (var b = new SolidBrush(Color.FromArgb(far ? 120 : 180, 236, 150, 150))) g.FillEllipse(b, -0.022f, -0.07f, 0.044f, 0.07f);
        using (var pen = new Pen(CoatDark, 0.02f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(pen, -0.004f, -0.085f, -0.012f, -0.122f);      // the little tuft
            g.DrawLine(pen, 0.01f, -0.085f, 0.016f, -0.12f);
        }
        g.Restore(st);
    }

    private static void Blossom(Graphics g, PointF c, float r)
    {
        using var petal = new SolidBrush(Color.FromArgb(255, 244, 168, 196));
        for (int i = 0; i < 5; i++)
        {
            float a = MathF.Tau * i / 5;
            Disc(g, petal, new PointF(c.X + MathF.Cos(a) * r * 0.55f, c.Y + MathF.Sin(a) * r * 0.55f), r * 0.5f);
        }
        Disc(g, Color.FromArgb(214, 92, 134), c, r * 0.2f);
    }
}

internal static class GraphicsExt
{
    /// <summary>Undo the body's "move to its middle and tilt" so later parts are drawn in squirrel units again.</summary>
    public static void ResetTransform2(this Graphics g, PointF mid, float angD)
    {
        g.RotateTransform(angD);
        g.TranslateTransform(-mid.X, -mid.Y);
    }
}
