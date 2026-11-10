using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// The turkey vulture that steals the coyotes' kill (his line, 2026-11-09: "that big vulture can swoop in and grab it and
/// fly away with it, leaving those idiot coyotes wondering wtf even just happened"). This file only PAINTS the bird;
/// Pack.cs flies it.
///
/// WHAT A TURKEY VULTURE LOOKS LIKE (looked up, not drawn from memory, because a hawk was the easy wrong answer): a BIG
/// dark brown bird, wings about 1.8 m across held in a SHALLOW V when it soars, broad wings with five spread "fingers" at the
/// tips, the back half of each wing from below a pale SILVERY grey (the flight feathers) against the dark front half,
/// a longish dark tail, and a tiny bare RED head on a thin neck (small for the body, which is the giveaway). It flaps
/// rarely and slowly, a deep heavy beat, and mostly rocks and tilts on the wind. Its feet are pale pinkish grey.
///
/// FEYNMAN VERSION: the bird is built ONCE as a little 3D model (a body, two wings, a tail, a neck and head, two legs), and
/// every page is a photograph of that model taken from a chosen angle. Three dials make a page:
///   - HEADING: which way the nose points, turned like a compass (so a bird going left and a bird going right are
///     different pages and no mirror is needed, and so a bird circling round is a ring of 36 pages);
///   - ELEVATION: how high above us we are looking up at it. A bird far overhead is seen almost from underneath (its
///     belly, both wings flat on the page, the circle it flies looks nearly round). A bird at the height of the coyotes is
///     seen from the SIDE (a body in profile, the near wing a foreshortened fan). One painter makes both, so a bird
///     spiralling down slides from the one picture to the other with no joins;
///   - the WINGS: how far up or down each wing is, with the tip a little behind the wrist (a real wing's tip lags the
///     stroke), plus whether the legs hang down and the talons are open.
/// And a fourth, the SCALE: a bird high up is farther away, so smaller; the page set has five sizes, one per height band.
///
/// THE PAGES: Soar (a V, every heading, five heights: the spiral), Glide (wings flat, rightward and leftward, the long low
/// pass), Drop (legs down, talons open) and Grab (wings up, talons shut) for the grab itself, and Flap (a six-page heavy beat,
/// plus one soaring V for the end of the climb) for the climb away with the rabbit hanging from the feet. Every page that
/// shows the legs records where the grip is, so Pack hangs the rabbit from it.
/// </summary>
internal static class VultureArt
{
    public const int Tiers = 5, Headings = 36, FlapPhases = 6, Ladder = 3;
    /// <summary>Tier 0 is the highest and farthest (small), tier 4 the lowest and nearest (full size, at the lane).</summary>
    public static float Scale(int tier) => 0.45f + 0.55f * tier / (Tiers - 1);
    public static float ElevOf(int tier) => 62f - 38f * tier / (Tiers - 1);
    public const float ClimbElev = 42f, ClimbPitch = 20f;
    /// <summary>The climb-away headings, for a bird going right (a bird going left uses 360 minus these): turning from along the lane toward "away".</summary>
    public static readonly float[] LadderDeg = [70f, 110f, 150f];

    /// <summary>One page's dials. Angles in degrees. Heading 0 is the nose up the page, then clockwise (90 is the nose to the right).</summary>
    public sealed class Look
    {
        public float Heading, Elev = 30f, Pitch;      // pitch: the nose raised above level flight (a climb)
        public float Inner = 14f, Lag = 6f;           // the wing's angle above level near the body, and how much further up (plus) or down (minus) the outer half is
        public bool Legs, Open;                       // legs hanging, talons spread
        public float FootY = 0.05f, FootZ = -0.5f;    // where the foot hangs, in half spans (forward, up)
    }

    private static readonly Color Body = Color.FromArgb(60, 46, 40), WingDark = Color.FromArgb(50, 38, 36), Silver = Color.FromArgb(178, 180, 188),
        TailCol = Color.FromArgb(44, 34, 32), HeadRed = Color.FromArgb(210, 60, 52), LegCol = Color.FromArgb(170, 150, 146), Toe = Color.FromArgb(92, 76, 72);

    // The wing, flat, in "half spans": s runs out along the wing (0 at the shoulder, 1 at the tip), c runs along the chord
    // (plus is toward the leading edge). Dark all over, then the silver rear half is laid over it. Five fingers at the tip.
    private static readonly (float S, float C)[] WingOutline =
    [
        (0.00f, 0.20f), (0.25f, 0.24f), (0.50f, 0.22f), (0.75f, 0.15f), (0.95f, 0.06f),
        (1.00f, 0.00f), (0.94f, -0.03f), (0.99f, -0.09f), (0.91f, -0.11f), (0.95f, -0.18f), (0.86f, -0.18f), (0.88f, -0.26f), (0.78f, -0.24f),
        (0.55f, -0.28f), (0.30f, -0.30f), (0.00f, -0.24f),
    ];
    private static readonly (float S, float C)[] WingSilver =
    [
        (0.00f, -0.02f), (0.55f, -0.02f), (0.82f, -0.04f), (0.84f, -0.17f), (0.86f, -0.18f), (0.88f, -0.26f), (0.78f, -0.24f),
        (0.55f, -0.28f), (0.30f, -0.30f), (0.00f, -0.24f),
    ];

    /// <summary>
    /// Paints one page. L is the half span in pixels. The sheet is square and the bird's middle is the sheet's middle; "foot" is where
    /// the grip hangs, in pixels from that middle (y down).
    /// </summary>
    public static Sprite Page(Look q, float L, out PointF foot)
    {
        int n = (int)MathF.Ceiling(2.6f * L) + 6; if ((n & 1) == 0) n++;
        float cx = n / 2f, cy = n / 2f;
        float rad = MathF.PI / 180f;
        float psi = q.Heading * rad, e = q.Elev * rad, ph = q.Pitch * rad;
        float cps = MathF.Cos(psi), sps = MathF.Sin(psi), ce = MathF.Cos(e), se = MathF.Sin(e), cph = MathF.Cos(ph), sph = MathF.Sin(ph);

        // A point of the model (bx across to the bird's right, by forward, bz up, all in half spans) as a place on the page,
        // and how far from us it is along our line of sight (bigger is farther, for the paint order).
        (float X, float Y, float D) P3(float bx, float by, float bz)
        {
            float by2 = by * cph - bz * sph, bz2 = by * sph + bz * cph;           // the climb: tip the nose up
            float wx = -bx * cps + by2 * sps, wy = -bx * sps - by2 * cps;          // turn to the heading on the ground plane (forward is (sin, -cos): up the page at heading 0)
            return (cx + L * wx, cy + L * (wy * se - bz2 * ce), wy * ce + bz2 * se);
        }
        PointF Pt((float X, float Y, float D) p) => new(p.X, p.Y);
        foot = new PointF(P3(0f, q.FootY, q.FootZ).X - cx, P3(0f, q.FootY, q.FootZ).Y - cy);

        float ti = q.Inner * rad, to = (q.Inner + q.Lag) * rad;
        PointF[] Wing(float side, (float S, float C)[] outline, out float depth)
        {
            var pts = new PointF[outline.Length]; float dsum = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                float s = outline[i].S, c = outline[i].C, a = MathF.Min(s, 0.5f), b = MathF.Max(0f, s - 0.5f);
                float lat = 0.07f + a * MathF.Cos(ti) + b * MathF.Cos(to), up = a * MathF.Sin(ti) + b * MathF.Sin(to);
                var p = P3(side * lat, c - 0.06f * s, up);                       // swept back a little toward the tip
                pts[i] = Pt(p); dsum += p.D;
            }
            depth = dsum / outline.Length;
            return pts;
        }
        var parts = new List<(float D, Action<Graphics> Draw)>();
        for (int side = -1; side <= 1; side += 2)
        {
            int sd = side;
            var dark = Wing(sd, WingOutline, out float depth);
            var silver = Wing(sd, WingSilver, out _);
            parts.Add((depth, g =>
            {
                using var b = new SolidBrush(WingDark); g.FillPolygon(b, dark);
                using var sb = new SolidBrush(Silver); g.FillPolygon(sb, silver);
            }));
        }
        // The tail, a longish dark fan.
        {
            var tail = new[] { P3(-0.05f, -0.28f, 0f), P3(0.05f, -0.28f, 0f), P3(0.15f, -0.74f, 0f), P3(0.05f, -0.80f, 0f), P3(-0.05f, -0.80f, 0f), P3(-0.15f, -0.74f, 0f) };
            var tp = new PointF[tail.Length]; for (int i = 0; i < tail.Length; i++) tp[i] = Pt(tail[i]);
            parts.Add((P3(0f, -0.55f, 0f).D, g => { using var b = new SolidBrush(TailCol); g.FillPolygon(b, tp); }));
        }
        // The body: a fat chest and a thinner rear, two round-capped strokes. The neck and the tiny red head at the front.
        {
            PointF rear = Pt(P3(0f, -0.30f, 0f)), mid = Pt(P3(0f, 0.05f, 0f)), chest = Pt(P3(0f, 0.30f, 0f));
            PointF neckEnd = Pt(P3(0f, 0.50f, -0.03f)), head = Pt(P3(0f, 0.55f, -0.03f));
            float hr = MathF.Max(0.07f * L, 1.5f);                                  // a vulture's head is small, but a speck of red must still read at a distance
            parts.Add((0f, g =>
            {
                using var pr = new Pen(Body, MathF.Max(1.5f, 0.20f * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var pc = new Pen(Body, MathF.Max(1.8f, 0.27f * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pr, rear, mid); g.DrawLine(pc, mid, chest);
            }));
            parts.Add((P3(0f, 0.55f, -0.03f).D, g =>
            {
                using var pn = new Pen(Body, MathF.Max(1f, 0.07f * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pn, chest, neckEnd);
                using var rb = new SolidBrush(HeadRed); g.FillEllipse(rb, head.X - hr, head.Y - hr, hr * 2, hr * 2);
            }));
        }
        // The legs, when they hang: two pale lines from the belly to the feet, the toes spread open or the feet shut.
        if (q.Legs)
        {
            float d = P3(0f, 0.05f, -0.3f).D;
            parts.Add((d, g =>
            {
                using var pen = new Pen(LegCol, MathF.Max(1f, 0.07f * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var toe = new Pen(Toe, MathF.Max(1f, 0.04f * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                for (int side = -1; side <= 1; side += 2)
                {
                    PointF hip = Pt(P3(side * 0.06f, 0f, -0.08f)), f = Pt(P3(side * 0.06f, q.FootY, q.FootZ));
                    g.DrawLine(pen, hip, f);
                    if (q.Open)
                    {
                        g.DrawLine(toe, f, Pt(P3(side * 0.06f + 0.05f, q.FootY + 0.10f, q.FootZ - 0.03f)));
                        g.DrawLine(toe, f, Pt(P3(side * 0.06f, q.FootY + 0.13f, q.FootZ - 0.05f)));
                        g.DrawLine(toe, f, Pt(P3(side * 0.06f - 0.05f, q.FootY + 0.10f, q.FootZ - 0.03f)));
                    }
                    else { using var b = new SolidBrush(Toe); float fr = MathF.Max(0.9f, 0.04f * L); g.FillEllipse(b, f.X - fr, f.Y - fr, fr * 2, fr * 2); }
                }
            }));
        }
        parts.Sort((a, b) => b.D.CompareTo(a.D));                                  // the farthest part first
        return Sprite.Paint(n, n, g => { foreach (var (_, draw) in parts) draw(g); });
    }

    /// <summary>The whole page set for a lane where a coyote is k pixels long (the bird's half span is 0.9 k at full size).</summary>
    public sealed class Set
    {
        public readonly Sprite[,] Soar = new Sprite[Tiers, Headings];
        public readonly Sprite[,] Glide = new Sprite[Tiers, 2];                     // [tier, 0 going right / 1 going left]
        public readonly Sprite[] Drop = new Sprite[2], Grab = new Sprite[2];
        public readonly PointF[] DropFoot = new PointF[2], GrabFoot = new PointF[2];
        public readonly Sprite[,,,] Flap = new Sprite[Tiers, Ladder, FlapPhases + 1, 2];   // [tier, heading rung, beat page (the last is the soaring V), 0 right / 1 left]
        public readonly PointF[,,,] FlapFoot = new PointF[Tiers, Ladder, FlapPhases + 1, 2];

        public Set(float k)
        {
            float full = 0.9f * k;
            for (int t = 0; t < Tiers; t++)
            {
                float L = full * Scale(t), el = ElevOf(t);
                for (int h = 0; h < Headings; h++) Soar[t, h] = Page(new Look { Heading = h * 360f / Headings, Elev = el, Inner = 14f, Lag = 6f }, L, out _);
                for (int d = 0; d < 2; d++) Glide[t, d] = Page(new Look { Heading = d == 0 ? 70f : 290f, Elev = el, Inner = 14f, Lag = 2f }, L, out _);
                // The climb away: seen low (a fixed elevation, the bird only shrinks as it goes), nose raised, the rabbit hanging.
                for (int r = 0; r < Ladder; r++)
                    for (int d = 0; d < 2; d++)
                        for (int p = 0; p <= FlapPhases; p++)
                        {
                            // A heavy beat: the wing's root angle runs 70 (up) to -30 (down) and back; on the way down the tip lags ABOVE the wrist, on the way up BELOW it.
                            float f = p / (float)FlapPhases, inner, lag;
                            if (p == FlapPhases) { inner = 16f; lag = 6f; }
                            else { inner = 20f + 50f * MathF.Cos(MathF.Tau * f); lag = f > 0f && f < 0.5f ? 18f : f > 0.5f ? -18f : 0f; }
                            var look = new Look { Heading = d == 0 ? LadderDeg[r] : 360f - LadderDeg[r], Elev = ClimbElev, Pitch = ClimbPitch, Inner = inner, Lag = lag, Legs = true, FootY = -0.02f, FootZ = -0.36f };
                            Flap[t, r, p, d] = Page(look, L, out FlapFoot[t, r, p, d]);
                        }
            }
            for (int d = 0; d < 2; d++)
            {
                float hd = d == 0 ? 70f : 290f;
                Drop[d] = Page(new Look { Heading = hd, Elev = ElevOf(Tiers - 1), Inner = 22f, Lag = 4f, Legs = true, Open = true, FootY = 0.20f, FootZ = -0.40f }, full, out DropFoot[d]);
                Grab[d] = Page(new Look { Heading = hd, Elev = ElevOf(Tiers - 1), Inner = 62f, Lag = 10f, Legs = true, Open = false, FootY = 0.10f, FootZ = -0.36f }, full, out GrabFoot[d]);
            }
        }
    }
}
