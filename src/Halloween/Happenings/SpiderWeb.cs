using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A small spider spins an orb web between three twigs of the dead tree,
/// strand by strand, then sits in the middle while the web catches the
/// moonlight (a glint slides along a strand now and then), then it all
/// fades away.
///
/// FEYNMAN VERSION of how the web is built (it is how a real spider does it):
///  1. First a "frame": three threads joining three twig tips in a triangle.
///  2. Then "spokes": straight threads from the middle (the hub) out to the
///     corners and to points along the sides, like the spokes of a wheel.
///  3. Then the "spiral": rings of short threads joining each spoke to the
///     next one, each ring a bit nearer the hub, each thread sagging a little
///     toward the hub like washing line.
///
/// Everything is a list of straight strands made in Begin. Each strand has a
/// time slot. At time t, a strand that has started is drawn as far as it has
/// grown (a fraction of the way from its start to its end). The spider is
/// always at the growing tip, or, in the first part of a slot, walking along
/// the web from where the last strand ended to where this one starts.
///
/// "Where do the twigs come from?" The scenery lists points on the dead tree's
/// thin branches (DeadTreeSpots). The constructor looks through them for every
/// triple of points that makes a decent triangle (not too small, not too big,
/// not nearly a straight line) and Begin picks one at random. If the tree is
/// so sparse that no triple passes, the limits are relaxed step by step, so
/// the web is never hung on thin air.
///
/// It is stamped and drawn in front of the tree, so no stencil is used.
/// </summary>
internal sealed class SpiderWeb : Happening
{
    private const float Build = 22f;      // seconds spent spinning
    private const float Walk = 2.5f;      // then the spider walks to the hub
    private const float Rest = 10f;       // then the finished web hangs
    private const float FadeOut = 6f;
    private const int Rings = 7;
    private const int Poses = 6;
    private static readonly Color Silk = Color.FromArgb(220, 225, 240);

    private readonly HalloweenScenery _s;
    private readonly List<(PointF A, PointF B, PointF C)> _triangles = [];
    private readonly Sprite[] _spider = new Sprite[Poses];   // legs at slightly different angles: a tiny flip-book
    private readonly Sprite _backlight, _glint;
    private readonly float _thick;

    // One strand: where it runs from and to, and its time slot.
    private struct Strand
    {
        public PointF From, To;      // the silk runs From -> To
        public PointF Walker;        // where the spider was before this slot began
        public float Start;          // slot starts (spider begins walking to From)
        public float Spin;           // spider arrives at From and starts spinning
        public float End;            // strand finished
    }

    private Strand[] _strands = [];
    private PointF _hub, _lastEnd;
    private int _seed;

    // With no branches to hang a web on (it happens on some trees), the
    // showing is over almost at once, so the director deals something else
    // and does not sit on an empty 40 seconds.
    public override float Seconds => _strands is { Length: > 0 } ? Build + Walk + Rest + FadeOut : 0.1f;
    public override string? Claims => "deadtree";

    public SpiderWeb(HalloweenScenery s)
    {
        _s = s;
        float u = s.U;
        _thick = Math.Max(1f, u * 0.0012f);

        // The spider: body about 0.012 U across (never under 3 pixels), legs
        // reaching about twice that. Dark body, with a faint pale halo painted
        // behind it so it still reads against the dark branches, as if the
        // moon were rimming it.
        int d = Math.Max(3, (int)(u * 0.012f));
        int size = d * 5;
        for (int i = 0; i < Poses; i++)
        {
            float phase = i * MathF.Tau / Poses;
            _spider[i] = Sprite.Paint(size, size, g => PaintSpider(g, size / 2f, size / 2f, d, phase));
        }
        _backlight = Sprite.Glow(d * 2, Color.FromArgb(200, 205, 235));
        _glint = Sprite.Glow(Math.Max(3, (int)(u * 0.007f)), Color.FromArgb(255, 255, 255));

        // Find every usable triangle among the tree's twig spots. A tree can
        // list hundreds of spots and checking all triples would be slow, so
        // keep at most 70 of them, evenly spread through the list.
        var spots = new List<PointF>();
        int stride = Math.Max(1, s.DeadTreeSpots.Count / 70);
        for (int i = 0; i < s.DeadTreeSpots.Count; i += stride) spots.Add(s.DeadTreeSpots[i]);
        if (spots.Count < 3) return;                              // nothing to hang a web on: draw nothing

        // Each row: shortest side, longest side (both fractions of U) and how
        // "fat" the triangle must be (area against longest side squared; an
        // equilateral triangle scores 0.43, a straight line scores 0).
        (float Lo, float Hi, float Fat)[] limits = [(0.06f, 0.17f, 0.15f), (0.04f, 0.24f, 0.10f), (0.02f, 0.40f, 0.05f), (0f, 9f, 0.02f)];
        foreach (var (lo, hi, fat) in limits)
        {
            for (int a = 0; a < spots.Count; a++)
                for (int b = a + 1; b < spots.Count; b++)
                    for (int c = b + 1; c < spots.Count; c++)
                    {
                        float ab = Dist(spots[a], spots[b]) / u, bc = Dist(spots[b], spots[c]) / u, ca = Dist(spots[c], spots[a]) / u;
                        float shortest = Math.Min(ab, Math.Min(bc, ca)), longest = Math.Max(ab, Math.Max(bc, ca));
                        if (shortest < lo || longest > hi) continue;
                        float area = Math.Abs((spots[b].X - spots[a].X) * (spots[c].Y - spots[a].Y)
                                            - (spots[c].X - spots[a].X) * (spots[b].Y - spots[a].Y)) / 2 / (u * u);
                        if (area / (longest * longest) < fat) continue;
                        _triangles.Add((spots[a], spots[b], spots[c]));
                    }
            if (_triangles.Count > 0) break;
        }
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static PointF Lerp(PointF a, PointF b, float k) => new(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k);

    /// <summary>
    /// Paints one spider pose: round abdomen, smaller head, eight jointed
    /// legs (four a side). "phase" nudges each leg's angle so that stepping
    /// through the poses makes the legs ripple.
    /// </summary>
    private static void PaintSpider(Graphics g, float cx, float cy, float d, float phase)
    {
        var ink = Color.FromArgb(12, 8, 20);
        using var leg = new Pen(ink, Math.Max(1f, d * 0.16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                // Legs fan from the front (i = 0, reaching up) to the back (i = 3, reaching down).
                float wiggle = 0.22f * MathF.Sin(phase + i * 1.7f + (side > 0 ? 0 : 2.4f));
                float a = (i - 1.5f) * 0.55f + wiggle;
                var knee = new PointF(cx + side * d * 1.05f * MathF.Cos(a * 0.6f), cy - d * 0.55f + d * 0.9f * MathF.Sin(a) - d * 0.35f);
                var foot = new PointF(knee.X + side * d * 0.75f * MathF.Cos(a * 0.5f), knee.Y + d * 0.9f + d * 0.5f * MathF.Sin(a));
                g.DrawLines(leg, [new PointF(cx + side * d * 0.2f, cy), knee, foot]);
            }
        using var body = new SolidBrush(ink);
        g.FillEllipse(body, cx - d * 0.45f, cy - d * 0.1f, d * 0.9f, d * 1.1f);               // abdomen
        g.FillEllipse(body, cx - d * 0.28f, cy - d * 0.55f, d * 0.56f, d * 0.56f);             // head
    }

    public override void Begin(Random rng)
    {
        _strands = [];
        if (_triangles.Count == 0) return;

        var (a, b, c) = _triangles[rng.Next(_triangles.Count)];
        _hub = new PointF((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3);
        _seed = rng.Next(1000);

        // Where the spokes end, in order around the triangle: each corner and
        // two points along the next side (a little jittered so it is not too neat).
        var ends = new List<PointF>();
        PointF[] corners = [a, b, c];
        for (int i = 0; i < 3; i++)
        {
            PointF p = corners[i], q = corners[(i + 1) % 3];
            ends.Add(p);
            ends.Add(Lerp(p, q, 0.33f + 0.08f * ((float)rng.NextDouble() - 0.5f)));
            ends.Add(Lerp(p, q, 0.67f + 0.08f * ((float)rng.NextDouble() - 0.5f)));
        }

        // The raw list of (from, to) threads, in the order they are spun.
        var raw = new List<(PointF From, PointF To)>();
        for (int i = 0; i < 3; i++) raw.Add((corners[i], corners[(i + 1) % 3]));   // 1. frame
        foreach (PointF e in ends) raw.Add((_hub, e));                              // 2. spokes
        for (int r = 0; r < Rings; r++)                                             // 3. spiral, outside in
        {
            float f = 0.92f - r * 0.115f;                                           // how far out this ring sits
            var ring = new List<(PointF, PointF)>();
            for (int i = 0; i < ends.Count; i++)
            {
                PointF p = Lerp(_hub, ends[i], f), q = Lerp(_hub, ends[(i + 1) % ends.Count], f);
                // Sag: a curve from p to q whose middle is pulled toward the hub,
                // cut into three straight bits.
                PointF mid = Lerp(p, q, 0.5f);
                float len = Dist(p, q);
                PointF toHub = new(_hub.X - mid.X, _hub.Y - mid.Y);
                float tl = Math.Max(0.001f, MathF.Sqrt(toHub.X * toHub.X + toHub.Y * toHub.Y));
                PointF ctrl = new(mid.X + toHub.X / tl * len * 0.14f, mid.Y + toHub.Y / tl * len * 0.14f);
                PointF Q(float k) => new((1 - k) * (1 - k) * p.X + 2 * k * (1 - k) * ctrl.X + k * k * q.X,
                                         (1 - k) * (1 - k) * p.Y + 2 * k * (1 - k) * ctrl.Y + k * k * q.Y);
                ring.Add((p, Q(1 / 3f)));
                ring.Add((Q(1 / 3f), Q(2 / 3f)));
                ring.Add((Q(2 / 3f), q));
            }
            if (r % 2 == 1)                                                         // wind the next ring back the other way
            {
                ring.Reverse();
                for (int i = 0; i < ring.Count; i++) ring[i] = (ring[i].Item2, ring[i].Item1);
            }
            raw.AddRange(ring);
        }

        // Time slots. A strand's slot is long in proportion to how far the
        // spider must walk to reach its start plus how long the strand is.
        // The scale is chosen so the whole build takes exactly Build seconds.
        var weights = new float[raw.Count];
        var walks = new float[raw.Count];
        PointF at = _hub;
        float total = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            walks[i] = Dist(at, raw[i].From) * 0.5f;
            weights[i] = walks[i] + Dist(raw[i].From, raw[i].To);
            total += weights[i];
            at = raw[i].To;
        }
        _lastEnd = at;
        total = Math.Max(1f, total);

        _strands = new Strand[raw.Count];
        at = _hub;
        float clock = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            float dur = Build * weights[i] / total;
            _strands[i] = new Strand
            {
                From = raw[i].From, To = raw[i].To, Walker = at,
                Start = clock,
                Spin = clock + (weights[i] > 0 ? dur * walks[i] / weights[i] : 0),
                End = clock + dur,
            };
            clock += dur;
            at = raw[i].To;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_strands.Length == 0) return;
        float fade = Fade(t, Seconds, 0.5f, FadeOut);
        // After the build, the whole web shimmers faintly (a slow sine wave on its brightness).
        float shimmer = t > Build ? 1f + 0.12f * (float)Math.Sin(t * 2.3) * Smooth((t - Build) / 2f) : 1f;
        float alpha = 0.55f * fade * shimmer;

        foreach (Strand st in _strands)
        {
            float p = Math.Clamp((t - st.Spin) / Math.Max(0.001f, st.End - st.Spin), 0f, 1f);
            if (p <= 0) continue;
            fb.Line(st.From, Lerp(st.From, st.To, p), Silk, alpha, _thick);
        }

        // Where is the spider?
        PointF spider = _hub;
        if (t < Build)
        {
            foreach (Strand st in _strands)
            {
                if (t < st.Start || t >= st.End) continue;
                spider = t < st.Spin
                    ? Lerp(st.Walker, st.From, Smooth((t - st.Start) / Math.Max(0.001f, st.Spin - st.Start)))
                    : Lerp(st.From, st.To, (t - st.Spin) / Math.Max(0.001f, st.End - st.Spin));
                break;
            }
        }
        else if (t < Build + Walk) spider = Lerp(_lastEnd, _hub, Smooth((t - Build) / Walk));

        // The legs ripple while it is moving and are still when it rests.
        bool moving = t < Build + Walk;
        int pose = moving ? (int)(t * 9) % Poses : 0;
        _backlight.DrawCentered(fb, spider.X, spider.Y, 0.22f * fade);
        _spider[pose].DrawCentered(fb, spider.X, spider.Y, fade);

        // Moonlight glints: during the hang, a tiny white spark slides along
        // one strand every ~1.6 s. Which strand is chosen from the seed and
        // the glint's number, so it needs no memory from frame to frame.
        float rest = t - (Build + Walk);
        if (rest > 0.5f)
        {
            float slot = rest / 1.6f;
            int n = (int)slot;
            float k = slot - n;                                                     // 0 to 1 along its strand
            int pick = (n * 7919 + _seed * 31) % Math.Max(1, _strands.Length - 12) + 12;  // skip the 3 frame threads and the spokes: glints run along the rings
            Strand st = _strands[Math.Clamp(pick, 0, _strands.Length - 1)];
            PointF g = Lerp(st.From, st.To, k);
            float glow = MathF.Sin(k * MathF.PI) * fade;
            _glint.DrawCentered(fb, g.X, g.Y, glow);
        }
    }
}
