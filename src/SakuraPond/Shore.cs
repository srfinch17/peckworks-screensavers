using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// Paints the corner of land: the lawn, the bare damp edge where it meets the
/// water, and the rocks. Everything here is painted once, into the backdrop.
/// The grass that MOVES is the shared Lawn (Core/Sakura/Plants.cs).
///
/// FEYNMAN VERSION, how a real pond edge is put together, nearest the water
/// first: the water laps a strip of damp sand and pebbles (grass does not
/// grow where it is always wet); rocks of all sizes sit along that line,
/// half in the water, each a lump with a sunny side and a shaded side, moss
/// on top and a dark wet band where the water touches it; behind them the
/// lawn, which from above is thousands of short blades, each catching the
/// light along its length and throwing a tiny shadow.
/// </summary>
internal static class ShorePainter
{
    /// <summary>
    /// A rock's outline: a lumpy ring of 9 to 13 points whose radius swells
    /// and shrinks round the ring (two slow waves plus a little jitter), a
    /// bit wider than tall because we look down on a rounded lump at a slant.
    /// </summary>
    public static PointF[] RockShape(float cx, float cy, float r, float squash, Random rng)
    {
        int n = 9 + rng.Next(5);
        float a0 = (float)(rng.NextDouble() * MathF.Tau), p2 = (float)(rng.NextDouble() * MathF.Tau), p3 = (float)(rng.NextDouble() * MathF.Tau);
        float k2 = 0.1f + 0.12f * (float)rng.NextDouble(), k3 = 0.05f + 0.08f * (float)rng.NextDouble();
        float stretch = 0.9f + 0.35f * (float)rng.NextDouble();
        var pts = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float a = a0 + i * MathF.Tau / n + ((float)rng.NextDouble() - 0.5f) * 0.5f * MathF.Tau / n;
            float rad = r * (1 + k2 * MathF.Sin(2 * a + p2) + k3 * MathF.Sin(3 * a + p3) + 0.06f * ((float)rng.NextDouble() - 0.5f));
            pts[i] = new PointF(cx + MathF.Cos(a) * rad * stretch, cy + MathF.Sin(a) * rad * squash);
        }
        return pts;
    }

    /// <summary>
    /// The lawn. The GROUND (dark moss with patches of bare earth) goes into
    /// the backdrop. The BLADES go on a layer of their own ("carpet"): a
    /// clear sheet with thousands of short grass blades on it, which Grass.cs
    /// lays back over the ground every frame, slid a little by the wind.
    ///
    /// The blades come in clumps (a few hundred centres, each with a
    /// scattering of blades round it, plus a thinner even sprinkling), so the
    /// lawn has thick and thin places instead of one even hatching. Each
    /// blade is a thin tapered sliver leaning a little downwind (right) and
    /// fanned at random, darker at the root and lighter at the tip, with a
    /// faint shadow thrown down and to the right. That root-to-tip lighting
    /// and the shadow are what make a flat painted lawn read as blades
    /// standing up.
    /// </summary>
    public static void Lawn(Graphics g, Graphics carpet, GraphicsPath land, int w, int h, float u, Random rng)
    {
        RectangleF box = land.GetBounds();
        float x0 = MathF.Max(0, box.Left), x1 = MathF.Min(w, box.Right), y0 = MathF.Max(0, box.Top), y1 = MathF.Min(h, box.Bottom);
        float area = (x1 - x0) * (y1 - y0);

        // THE GROUND under the grass, pixel by pixel from slow blotches
        // (Mottle, in Core): lush dark green where the ground is damp, a
        // warmer yellow-green where the sun bakes it, and in the thinnest
        // places bare earth showing through. The same blotch thins the
        // carpet of blades below, so the earth shows where the grass is
        // worn, not at random. A second blotch marks the clover patches, a
        // third the moss (velvety, no blades). The lawn's standing tufts
        // (Core's Lawn) read the finished carpet's thickness, so they thin
        // out over the worn and mossy ground too.
        int seed = rng.Next();
        float blotchCell = u * 0.14f;
        float Wear(float x, float y) => Mottle.Blotch(x, y, blotchCell, seed);                       // 0 worn to 1 lush
        float Clover(float x, float y) => Mottle.Blotch(x, y, u * 0.11f, seed + 10);
        float Moss(float x, float y) => Mottle.Blotch(x, y, u * 0.06f, seed + 20);           // small patches: a big one read as a dark hole in the lawn
        Color damp = Color.FromArgb(50, 74, 40), lush = Color.FromArgb(64, 92, 44), sunny = Color.FromArgb(96, 118, 50), earth = Color.FromArgb(86, 70, 46), mossy = Color.FromArgb(46, 70, 38);
        int bx0 = (int)x0, by0 = (int)y0, bw = Math.Max(1, (int)(x1 - x0)), bh = Math.Max(1, (int)(y1 - y0));
        using (var groundBmp = new Bitmap(bw, bh, PixelFormat.Format32bppArgb))
        {
            var gpx = new uint[bw * bh];
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    float px = bx0 + x, py = by0 + y;
                    float wear = Wear(px, py);
                    // Lush to sunny by one blotch; earth where it is worn; moss where the moss blotch is high.
                    Color c = wear < 0.3f ? Brushwork.Mix(earth, damp, wear / 0.3f) : Brushwork.Mix(lush, sunny, (wear - 0.3f) / 0.7f);
                    float m = Moss(px, py);
                    if (m > 0.62f) c = Brushwork.Mix(c, mossy, 0.7f * MathF.Min(1, (m - 0.62f) / 0.1f));
                    // A fine grain so the ground is not a smooth gradient (soil is speckled).
                    int grain = (int)((Mottle.At(px, py, 2.3f, seed + 30) - 0.5f) * 18);
                    gpx[y * bw + x] = 0xFF000000u | (uint)Math.Clamp(c.R + grain, 0, 255) << 16 | (uint)Math.Clamp(c.G + grain, 0, 255) << 8 | (uint)Math.Clamp(c.B + grain, 0, 255);
                }
            var data = groundBmp.LockBits(new Rectangle(0, 0, bw, bh), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy((int[])(object)gpx, 0, data.Scan0, gpx.Length);
            groundBmp.UnlockBits(data);
            GraphicsState st = g.Save();
            g.SetClip(land, CombineMode.Intersect);
            g.DrawImageUnscaled(groundBmp, bx0, by0);
            // Moss gets a velvety speckle of lighter and darker dots.
            int dots = (int)Math.Min(20000, area / (u * 0.006f * u * 0.006f));
            for (int i = 0; i < dots; i++)
            {
                float x = x0 + (float)rng.NextDouble() * (x1 - x0), y = y0 + (float)rng.NextDouble() * (y1 - y0);
                if (Moss(x, y) < 0.64f) continue;
                float r = u * (0.0012f + 0.0018f * (float)rng.NextDouble());
                using var b = new SolidBrush(Color.FromArgb(110, rng.Next(2) == 0 ? Color.FromArgb(104, 140, 68) : Color.FromArgb(30, 50, 26)));
                g.FillEllipse(b, x - r, y - r * 0.8f, r * 2, r * 1.6f);
            }
            g.Restore(st);
        }

        // THE BLADES, on the carpet. Lighter toward the upper left, where the
        // sun comes from, and tinted by the ground's blotch (yellower on the
        // sunny patches, bluer on the damp). Thinned where the ground is
        // worn, left out on the moss, and on the clover patches a blade
        // becomes a clover leaf: three small round leaflets on a short stem,
        // a touch bluer than the grass, which from above makes a rounder,
        // denser texture than the slivers round it.
        GraphicsState cst = carpet.Save();
        carpet.SetClip(land, CombineMode.Intersect);
        int blades = (int)Math.Min(45000, area / (u * 0.004f * u * 0.004f));
        int clumps = Math.Max(1, blades / 60);
        var centres = new PointF[clumps];
        for (int i = 0; i < clumps; i++) centres[i] = new PointF(x0 + (float)rng.NextDouble() * (x1 - x0), y0 + (float)rng.NextDouble() * (y1 - y0));
        Color[] greens =
        [
            Color.FromArgb(58, 96, 42), Color.FromArgb(78, 118, 50), Color.FromArgb(96, 134, 56),
            Color.FromArgb(66, 104, 46), Color.FromArgb(110, 146, 64), Color.FromArgb(88, 112, 54),
        ];
        var polyArr = new PointF[4];
        using var shadow = new SolidBrush(Color.FromArgb(16, 6, 22, 10));
        using var cloverShadow = new SolidBrush(Color.FromArgb(22, 6, 22, 10));
        for (int i = 0; i < blades; i++)
        {
            float x, y;
            if (i % 4 == 0) { x = x0 + (float)rng.NextDouble() * (x1 - x0); y = y0 + (float)rng.NextDouble() * (y1 - y0); }
            else
            {
                PointF c = centres[rng.Next(clumps)];
                double a = rng.NextDouble() * Math.Tau, d = Math.Sqrt(rng.NextDouble()) * u * 0.035;
                x = c.X + (float)(Math.Cos(a) * d); y = c.Y + (float)(Math.Sin(a) * d * 0.8);
            }
            float wear = Wear(x, y);
            if (Moss(x, y) > 0.66f) continue;                                    // moss: no blades
            if (wear < 0.35f && rng.NextDouble() > wear / 0.35f) continue;        // worn: thinner, earth showing
            float light = 0.1f * (1 - x / w) + 0.08f * (1 - y / h);
            if (Clover(x, y) > 0.66f)
            {
                // A clover leaf: three leaflets round a point a little up from the stem's foot.
                float r = u * (0.0024f + 0.0012f * (float)rng.NextDouble());
                float cx = x + ((float)rng.NextDouble() - 0.5f) * r, cy = y - r * 1.2f;
                Color leaf = Brushwork.Mix(Color.FromArgb(52, 104, 62), Color.FromArgb(150, 190, 120), light + 0.1f * (float)rng.NextDouble());
                carpet.FillEllipse(cloverShadow, cx - r * 1.3f + r * 0.5f, cy - r * 1.1f + r * 0.5f, r * 2.6f, r * 2.2f);
                using var lb = new SolidBrush(leaf);
                using var lb2 = new SolidBrush(Brushwork.Mix(leaf, Color.FromArgb(220, 236, 200), 0.3f));
                float a0 = (float)rng.NextDouble() * MathF.Tau;
                for (int k = 0; k < 3; k++)
                {
                    float a = a0 + k * MathF.Tau / 3;
                    float lx = cx + MathF.Cos(a) * r * 0.75f, ly = cy + MathF.Sin(a) * r * 0.6f;
                    carpet.FillEllipse(lb, lx - r * 0.72f, ly - r * 0.6f, r * 1.44f, r * 1.2f);
                    carpet.FillEllipse(lb2, lx - r * 0.3f, ly - r * 0.3f, r * 0.5f, r * 0.4f);   // the pale chevron each leaflet carries
                }
                continue;
            }
            float len = u * (0.014f + 0.018f * (float)rng.NextDouble());
            float az = ((float)rng.NextDouble() - 0.5f) * 1.8f;                 // its own fan, left or right of straight up
            float lean = 0.1f + 0.35f * (float)rng.NextDouble();                // all of them lean a little downwind (right)
            float tx = x + len * (lean + 0.55f * MathF.Sin(az)), ty = y - len * Light.Lift * MathF.Cos(az) * 0.9f;
            float wid = u * (0.0017f + 0.0015f * (float)rng.NextDouble());
            // The shadow: the blade flattened onto the ground, down and to the right.
            float sx = x + len * (lean * 0.4f + Light.ShadowX * 0.3f), sy = y + len * Light.ShadowY * 0.28f;
            Sliver(carpet, shadow, x, y, sx, sy, wid * 0.9f, polyArr);
            Color c0 = greens[rng.Next(greens.Length)];
            c0 = Brushwork.Mix(c0, Color.FromArgb(176, 190, 110), light);
            // The ground's blotch tints the blade: yellower where sunny, bluer and darker where damp.
            c0 = wear > 0.5f ? Brushwork.Mix(c0, Color.FromArgb(160, 170, 70), (wear - 0.5f) * 0.5f) : Brushwork.Mix(c0, Color.FromArgb(40, 80, 60), (0.5f - wear) * 0.5f);
            using (var root = new SolidBrush(Brushwork.Mix(c0, Color.FromArgb(24, 40, 20), 0.4f)))
                Sliver(carpet, root, x, y, tx, ty, wid, polyArr);
            // The tip half again in a lighter colour: light along the blade, as a blade in the sun.
            using (var tip = new SolidBrush(Brushwork.Mix(c0, Color.FromArgb(205, 218, 120), 0.35f)))
                Sliver(carpet, tip, (x + tx) / 2, (y + ty) / 2, tx, ty, wid * 0.6f, polyArr);
        }
        carpet.Restore(cst);
    }

    /// <summary>A tapered sliver from (x0, y0), "wid" across at its base, to a point at (x1, y1).</summary>
    private static void Sliver(Graphics g, Brush b, float x0, float y0, float x1, float y1, float wid, PointF[] poly)
    {
        float dx = x1 - x0, dy = y1 - y0, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        float nx = -dy / l * wid * 0.5f, ny = dx / l * wid * 0.5f;
        poly[0] = new PointF(x0 + nx, y0 + ny); poly[1] = new PointF(x1 + nx * 0.15f, y1 + ny * 0.15f);
        poly[2] = new PointF(x1 - nx * 0.15f, y1 - ny * 0.15f); poly[3] = new PointF(x0 - nx, y0 - ny);
        g.FillPolygon(b, poly);
    }

    /// <summary>
    /// The bare edge: a strip of damp sand and pebbles between the lawn and
    /// the water, wider here and narrower there, with a ragged inner edge,
    /// darker (wetter) nearest the water. Only its nearer two thirds go into
    /// "noGrass", so the lawn's blades overhang its far edge and the join is
    /// ragged, not a drawn line.
    /// </summary>
    public static void BareEdge(Graphics g, Graphics noGrass, PointF[] bank, PointF corner, float u, Random rng)
    {
        float ph = (float)(rng.NextDouble() * MathF.Tau), ph2 = (float)(rng.NextDouble() * MathF.Tau);
        var inner = new PointF[bank.Length];
        var core = new PointF[bank.Length];
        for (int i = 0; i < bank.Length; i++)
        {
            PointF p = bank[i];
            float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
            float t = i / (bank.Length - 1f);
            float wide = u * (0.018f + 0.03f * (0.5f + 0.5f * MathF.Sin(t * 9 + ph)) * (0.5f + 0.5f * MathF.Sin(t * 4.3f + 2 * ph))
                + 0.008f * MathF.Sin(t * 31 + ph2));
            inner[i] = new PointF(p.X + ox / ol * wide, p.Y + oy / ol * wide);
            core[i] = new PointF(p.X + ox / ol * wide * 0.65f, p.Y + oy / ol * wide * 0.65f);
        }
        using var strip = Band(bank, inner);
        using var keepOut = Band(bank, core);
        using (var sand = new SolidBrush(Color.FromArgb(104, 98, 78))) g.FillPath(sand, strip);
        // No painted "damp" band along the water any more: with the water's dark edge line it read as one dim
        // ribbon drawn along the bank. The wet sand is the live shore's doing now (Shoreline): the water laps up
        // the strip and back and darkens what it has just left, with a ragged edge, not a band along a curve.
        noGrass.FillPath(Brushes.White, keepOut);
        // Pebbles and specks of soil on the sand, each with a touch of shadow.
        GraphicsState st = g.Save();
        g.SetClip(strip, CombineMode.Intersect);
        RectangleF box = strip.GetBounds();
        int n = (int)Math.Min(5000, box.Width * box.Height / (u * 0.005f * u * 0.005f));
        for (int i = 0; i < n; i++)
        {
            float x = box.Left + (float)rng.NextDouble() * box.Width, y = box.Top + (float)rng.NextDouble() * box.Height;
            float r = u * (0.0012f + 0.0045f * (float)Math.Pow(rng.NextDouble(), 2.2));
            int k = 92 + rng.Next(90);
            Color c = rng.Next(4) == 0 ? Color.FromArgb(66, 54, 40) : Color.FromArgb(k, k - 6, k - 18);
            using var sb = new SolidBrush(Color.FromArgb(40, 10, 16, 12));
            g.FillEllipse(sb, x - r + r * 0.3f, y - r * 0.75f + r * 0.3f, r * 2, r * 1.5f);
            using var b = new SolidBrush(Color.FromArgb(200, c));
            g.FillEllipse(b, x - r, y - r * 0.75f, r * 2, r * 1.5f);
        }
        g.Restore(st);
    }

    /// <summary>The closed shape between two curves that run the same way.</summary>
    private static GraphicsPath Band(PointF[] a, PointF[] b)
    {
        var path = new GraphicsPath();
        path.AddCurve(a, 0.5f);
        path.AddLine(a[^1], b[^1]);
        path.AddCurve(b.Reverse().ToArray(), 0.5f);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Rocks along the water's edge: a few big ones and more small ones, each
    /// sitting mostly on the sand with its lower side in the water, placed
    /// with a clearance test so no two sit inside each other (touching is
    /// fine, as rocks do). One or two more sit out in the shallows. Drawn
    /// far to near, so a nearer rock covers one behind it.
    /// </summary>
    public static List<BankRock> Rocks(Graphics g, Graphics gb, Graphics noGrass, PointF[] bank, PointF corner, float u, Random rng)
    {
        var placed = new List<(PointF C, float R, bool InWater)>();
        for (int tries = 0; tries < 160 && placed.Count < 15; tries++)
        {
            float t = 0.04f + 0.92f * (float)rng.NextDouble();
            int i = (int)(t * (bank.Length - 1));
            float f = t * (bank.Length - 1) - i;
            PointF p = new(bank[i].X + (bank[i + 1].X - bank[i].X) * f, bank[i].Y + (bank[i + 1].Y - bank[i].Y) * f);
            bool big = rng.NextDouble() < 0.3;
            float r = big ? u * (0.04f + 0.035f * (float)rng.NextDouble()) : u * (0.014f + 0.022f * (float)rng.NextDouble());
            float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
            bool inWater = placed.Count(q => q.InWater) < 2 && rng.NextDouble() < 0.12;
            float shift = inWater ? -r * (1.6f + (float)rng.NextDouble()) : r * (0.1f + 0.3f * (float)rng.NextDouble());   // landward, or out into the water
            var c = new PointF(p.X + ox / ol * shift, p.Y + oy / ol * shift);
            if (placed.Any(q => Dist(q.C, c) < (q.R + r) * 0.85f)) continue;
            placed.Add((c, r, inWater));
        }
        foreach (var (c, r, _) in placed.OrderBy(q => q.C.Y))
            Rock(g, gb, noGrass, c.X, c.Y, r, u, rng);
        return placed.Select(q => new BankRock(q.C, q.R, q.InWater)).ToList();
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Chooses where the island rock stands: out in the open water, well
    /// clear of the bank (further out than any lily pad cluster reaches),
    /// clear of the log, away from the screen's edges and from the blossom
    /// along the top. The cap sits a little above the stone's middle (the
    /// high point of a stone seen from our slant). Null if no spot is found
    /// after many tries (a very small or very tall screen).
    /// </summary>
    /// <param name="shade">The branches' shadow map (0 = open sky): the island keeps out from under the blossom.</param>
    public static Island? PlaceIsland(bool[] inPond, int[] dist, byte[] shade, Log? log, int w, int h, float u, Random rng)
    {
        float r = u * (0.09f + 0.03f * (float)rng.NextDouble());
        float capR = r * (0.32f + 0.08f * (float)rng.NextDouble());
        for (int tries = 0; tries < 600; tries++)
        {
            float x = w * (0.12f + 0.76f * (float)rng.NextDouble()), y = h * (0.4f + 0.4f * (float)rng.NextDouble());
            int i = (int)y * w + (int)x;
            // Open water, well out from the bank; when the first three hundred tries find none
            // (a wide screen with the blossom low), nearer will do (the pads keep clear of it).
            if (!inPond[i] || dist[i] < u * (tries < 300 ? 0.45f : 0.3f) * 10) continue;
            bool shaded = false;
            for (int k = 0; k < 12 && !shaded; k++)                                                 // open sky over the whole stone and a margin round it
            {
                float a = k * MathF.Tau / 12, sx = x + MathF.Cos(a) * r * 1.3f, sy = y + MathF.Sin(a) * r * 1.3f;
                int si = Math.Clamp((int)sy, 0, h - 1) * w + Math.Clamp((int)sx, 0, w - 1);
                shaded = shade[si] > 0 || shade[i] > 0;
            }
            if (shaded) continue;
            if (x < r + u * 0.05f || x > w - r - u * 0.05f || y < r + u * 0.05f || y > h - r - u * 0.08f) continue;
            if (log is Log l && Dist(l.Centre, new PointF(x, y)) < l.Length * 0.5f + r + u * 0.08f) continue;
            var cap = new PointF(x, y - r * 0.18f);
            return new Island(new PointF(x, y), r, cap, capR);
        }
        return null;
    }

    /// <summary>
    /// Chooses where the small rock formation stands: out in the water at the
    /// lower left of the screen (where the fallen log used to lie), a stone
    /// half the island's size with two or three little caps on it, each cap
    /// half way out from the stone's middle and evenly round it, so no two
    /// caps touch. Open water a little way out from the bank, under open sky,
    /// clear of the island. Null if no spot is found (a very odd screen).
    /// </summary>
    public static Formation? PlaceFormation(bool[] inPond, int[] dist, byte[] shade, Island? island, int w, int h, float u, Random rng)
    {
        float r = u * (0.055f + 0.02f * (float)rng.NextDouble());
        int n = 2 + rng.Next(2);
        // Of several hundred random spots in the lower left half of the pond that pass the tests,
        // the one nearest the lower left corner wins: when the land is in that corner the open
        // water begins well up and across from it (the first version searched a box by the corner
        // and found nothing on two seeds in three), and when the land is in the other corner the
        // formation sits out near the edge as the floating log did.
        PointF best = default; float bestScore = float.MaxValue;
        for (int tries = 0; tries < 600; tries++)
        {
            float x = w * (0.05f + 0.45f * (float)rng.NextDouble()), y = h * (0.42f + 0.5f * (float)rng.NextDouble());
            int i = (int)y * w + (int)x;
            if (!inPond[i] || dist[i] < u * 0.2f * 10) continue;
            bool shaded = false;
            for (int k = 0; k < 12 && !shaded; k++)
            {
                float a = k * MathF.Tau / 12, sx = x + MathF.Cos(a) * r * 1.3f, sy = y + MathF.Sin(a) * r * 1.3f;
                int si = Math.Clamp((int)sy, 0, h - 1) * w + Math.Clamp((int)sx, 0, w - 1);
                shaded = shade[si] > 0 || shade[i] > 0;
            }
            if (shaded) continue;
            if (x < r + u * 0.05f || x > w - r - u * 0.05f || y < r + u * 0.05f || y > h - r - u * 0.08f) continue;
            if (island is Island isle && Dist(isle.Centre, new PointF(x, y)) < isle.Radius + r + u * 0.1f) continue;
            float score = Dist(new PointF(x, y), new PointF(0, h)) * (0.9f + 0.2f * (float)rng.NextDouble());
            if (score < bestScore) { bestScore = score; best = new PointF(x, y); }
        }
        if (bestScore < float.MaxValue)
        {
            float x = best.X, y = best.Y;
            var caps = new PointF[n];
            var capR = new float[n];
            float a0 = (float)(rng.NextDouble() * MathF.Tau);
            for (int k = 0; k < n; k++)
            {
                float a = a0 + k * MathF.Tau / n + 0.3f * ((float)rng.NextDouble() - 0.5f);
                float d = r * (0.35f + 0.1f * (float)rng.NextDouble());
                caps[k] = new PointF(x + MathF.Cos(a) * d, y + MathF.Sin(a) * d - r * 0.1f);
                capR[k] = r * (0.22f + 0.08f * (float)rng.NextDouble());
            }
            return new Formation(new Island(new PointF(x, y), r, caps[0], capR[0]), caps, capR);
        }
        return null;
    }

    /// <summary>
    /// Paints the island's stone: the whole boulder, painted as any rock is
    /// but bare (no moss, no lichen: it is going under water), into the
    /// picture only (nothing into the blockers: under water it is no wall,
    /// fish swim over it). Returns where the stone is, pixel by pixel, for the
    /// painter's water wrap (WrapIsland), which fades the stone from its OWN
    /// edge inward.
    /// </summary>
    public static bool[] IslandStone(Graphics g, Island isle, int w, int h, float u, Random rng)
    {
        using var sheet = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var gs = Graphics.FromImage(sheet);
        gs.Clear(Color.Black);
        Rock(g, gs, gs, isle.Centre.X, isle.Centre.Y, isle.Radius, u, rng, bare: true);
        return Brushwork.ToPixels(sheet).Select(c => (c & 0xFF) >= 128).ToArray();
    }

    /// <summary>
    /// The island's dry cap: a small rock of its own on top of the stone, with
    /// every rock's faces, creases, grain and moss, and the only part of the
    /// island that goes into the blockers (a wall to the koi, a thing for a
    /// turtle to climb onto). Painted BEFORE the water wrap, so the wrap can
    /// run a waterline round the cap's own rim the way it does round every
    /// bank rock (painted after the wrap, it was a dry cut-out pasted on the
    /// water, with a drop shadow across it). Returns where the cap is.
    /// </summary>
    public static bool[] IslandCap(Graphics g, Graphics gb, Island isle, int w, int h, float u, Random rng)
    {
        using var sheet = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var gs = Graphics.FromImage(sheet);
        gs.Clear(Color.Black);
        Rock(g, gs, gs, isle.Cap.X, isle.Cap.Y, isle.CapRadius, u, rng);
        // The cap's shape into the blockers (black on the sheet is "nothing": keyed out).
        using var key = new ImageAttributes();
        key.SetColorKey(Color.Black, Color.FromArgb(40, 40, 40));
        gb.DrawImage(sheet, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, key);
        return Brushwork.ToPixels(sheet).Select(c => (c & 0xFF) >= 128).ToArray();
    }

    /// <summary>
    /// Chooses where the fallen log lies. He asked for "a log randomly on the
    /// lower left", so it is always at the lower left of the screen. When the
    /// land is in that corner, the log lies BEACHED across the water's edge,
    /// one end in the grass and the other in the shallows, at an angle to
    /// the bank (a trunk that fell toward the water). When the land is in the
    /// other corner, a log FLOATS out on the water at the lower left instead.
    /// Length, thickness and angle are rolled fresh each time. Null if no
    /// spot clear of the rocks and the cattails is found (rare).
    /// </summary>
    public static Log? PlaceLog(PointF[] bank, PointF corner, bool leftCorner, bool[] inPond, List<BankRock> rocks, PointF? cattailsSpotHint, int w, int h, float u, Random rng)
    {
        float len = u * (0.2f + 0.1f * (float)rng.NextDouble()), wid = u * (0.04f + 0.015f * (float)rng.NextDouble());
        for (int tries = 0; tries < 240; tries++)
        {
            Log log;
            // The bank is lined with rocks, so a beached log is only asked to clear the BIG ones (it lies
            // over the small stones, as a fallen trunk would); after many tries it floats just off the bank instead.
            bool beachedTry = leftCorner && tries < 160;
            if (beachedTry)
            {
                // A point along the middle part of the bank, and the bank's direction there.
                float t = 0.3f + 0.45f * (float)rng.NextDouble();
                int i = Math.Min(bank.Length - 2, (int)(t * (bank.Length - 1)));
                PointF p = bank[i], q = bank[i + 1];
                float along = MathF.Atan2(q.Y - p.Y, q.X - p.X);
                // Across the bank at 40 to 75 degrees, either way; the log's middle sits a little on the water side.
                float a = along + (rng.Next(2) == 0 ? 1 : -1) * (0.7f + 0.6f * (float)rng.NextDouble());
                float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
                var c = new PointF(p.X - ox / ol * len * 0.12f, p.Y - oy / ol * len * 0.12f);
                log = new Log(c, a, len, wid, Afloat: false);
            }
            else if (leftCorner)
            {
                // Afloat just off the bank, lying roughly along it, a little way out.
                float t = 0.3f + 0.45f * (float)rng.NextDouble();
                int i = Math.Min(bank.Length - 2, (int)(t * (bank.Length - 1)));
                PointF p = bank[i], q = bank[i + 1];
                float along = MathF.Atan2(q.Y - p.Y, q.X - p.X) + 0.4f * ((float)rng.NextDouble() - 0.5f);
                float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
                var c = new PointF(p.X - ox / ol * u * 0.09f, p.Y - oy / ol * u * 0.09f);
                log = new Log(c, along, len, wid, Afloat: true);
            }
            else
            {
                var c = new PointF(w * (0.1f + 0.14f * (float)rng.NextDouble()), h * (0.78f + 0.12f * (float)rng.NextDouble()));
                log = new Log(c, (float)(rng.NextDouble() * MathF.PI), len, wid, Afloat: true);
            }
            // Both ends on screen, the floating log wholly on water, and the middle line clear of the rocks
            // (all of them for a floating log, only the big ones for a beached one).
            PointF e0 = log.At(0.05f), e1 = log.At(0.95f);
            if (e0.X < wid || e1.X < wid || e0.Y < 0 || e1.Y < 0 || e0.X >= w - wid || e1.X >= w - wid || e0.Y >= h - wid * 0.5f || e1.Y >= h - wid * 0.5f) continue;
            if (log.Afloat && !(inPond[(int)e0.Y * w + (int)e0.X] && inPond[(int)e1.Y * w + (int)e1.X])) continue;
            bool clear = true;
            for (int k = 0; k <= 10 && clear; k++)
            {
                PointF s = log.At(k / 10f);
                foreach (BankRock r in rocks)
                {
                    if (!log.Afloat && r.Radius < u * 0.03f) continue;
                    if (Dist(r.Centre, s) < r.Radius * 0.8f + wid * 0.5f) { clear = false; break; }
                }
            }
            if (clear) return log;
        }
        return null;
    }

    /// <summary>
    /// Paints the fallen log. Seen from above, a log is a long rounded shape
    /// with bark running along it, and we see a little of the near end's cut
    /// face (a disc of rings) because we look from slightly in front.
    /// Paint order, the way light builds it: soft shadow down and right, the
    /// bark body shaded across its width (lit along the upper-left side,
    /// darkest along the lower-right flank), long wavy streaks of bark, a
    /// few cracks across, a knot or two, moss on top, the cut end, then a
    /// faint light edge where the rim faces the sun and a dark edge where it
    /// faces away (50 alpha at most: no outlines). A floating log gets a dark
    /// waterline along its lower side instead of the rocks' water wrap.
    /// </summary>
    public static void Log(Graphics g, Graphics gb, Graphics noGrass, Log log, float u, Random rng)
    {
        float ca = MathF.Cos(log.Angle), sa = MathF.Sin(log.Angle), nx = -sa, ny = ca;   // along, and across
        float half = log.Width * 0.5f, len = log.Length;
        // The outline: 14 points down each long side, each a little in or out (bark is not a ruled line), round ends.
        var pts = new List<PointF>();
        int n = 14;
        float[] wob = new float[n + 1];
        for (int i = 0; i <= n; i++) wob[i] = 1 + 0.06f * MathF.Sin(i * 1.7f + (float)rng.NextDouble() * 0.6f) + 0.03f * ((float)rng.NextDouble() - 0.5f);
        for (int i = 0; i <= n; i++) { PointF p = log.At(i / (float)n); float hw = half * wob[i]; pts.Add(new PointF(p.X + nx * hw, p.Y + ny * hw)); }
        for (int i = n; i >= 0; i--) { PointF p = log.At(i / (float)n); float hw = half * (2 - wob[i]); pts.Add(new PointF(p.X - nx * hw, p.Y - ny * hw)); }
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts.ToArray(), 0.4f);

        // Shadow: three soft layers down and right, like a rock's.
        for (int k = 3; k >= 1; k--)
        {
            using var sh = (GraphicsPath)path.Clone();
            using var m = new Matrix();
            m.Translate(half * 0.35f + k * u * 0.002f, half * 0.5f + k * u * 0.002f);
            sh.Transform(m);
            using var sb = new SolidBrush(Color.FromArgb(28, 4, 14, 14));
            g.FillPath(sb, sh);
        }

        // The body: bark brown, shaded across the width so it reads as round. Which side is "up-left"?
        Color bark = Color.FromArgb(96 + rng.Next(20), 70 + rng.Next(14), 46 + rng.Next(10));
        float sunSide = nx * -0.55f + ny * -0.83f;                        // positive: the +n side faces the sun
        PointF c0 = new(log.Centre.X + nx * half * 1.1f, log.Centre.Y + ny * half * 1.1f), c1 = new(log.Centre.X - nx * half * 1.1f, log.Centre.Y - ny * half * 1.1f);
        if (sunSide < 0) (c0, c1) = (c1, c0);
        using (var body = new LinearGradientBrush(c0, c1, Shade(bark, 1.25f), Shade(bark, 0.62f)))
        {
            body.SetSigmaBellShape(0.35f, 1f);
            g.FillPath(body, path);
        }
        GraphicsState st = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        // Bark: many long streaks along the log, each its own wavering line, darker or lighter than the body.
        int streaks = 18 + rng.Next(10);
        for (int s = 0; s < streaks; s++)
        {
            float off = ((float)rng.NextDouble() * 2 - 1) * half * 0.95f;
            float t0 = (float)rng.NextDouble() * 0.5f, t1 = t0 + 0.25f + (float)rng.NextDouble() * 0.6f;
            bool dark = rng.NextDouble() < 0.65;
            Color sc = dark ? Shade(bark, 0.55f + 0.2f * (float)rng.NextDouble()) : Shade(bark, 1.2f + 0.2f * (float)rng.NextDouble());
            using var pen = new Pen(Color.FromArgb(50 + rng.Next(70), sc), MathF.Max(0.8f, half * (0.05f + 0.08f * (float)rng.NextDouble())));
            var line = new PointF[6];
            for (int k = 0; k < 6; k++)
            {
                PointF p = log.At(t0 + (t1 - t0) * k / 5f);
                float o = off + half * 0.08f * MathF.Sin(k * 1.9f + s);
                line[k] = new PointF(p.X + nx * o, p.Y + ny * o);
            }
            g.DrawCurve(pen, line, 0.5f);
        }
        // A few cracks across the bark, short, from one side part way over.
        for (int k = 0; k < 2 + rng.Next(3); k++)
        {
            float t = 0.1f + 0.8f * (float)rng.NextDouble();
            PointF p = log.At(t);
            float side = rng.Next(2) == 0 ? 1 : -1, reach = half * (0.3f + 0.5f * (float)rng.NextDouble());
            using var pen = new Pen(Color.FromArgb(110, Shade(bark, 0.45f)), MathF.Max(0.8f, half * 0.06f));
            g.DrawLine(pen, p.X + nx * half * side, p.Y + ny * half * side, p.X + nx * (half - reach) * side + ca * reach * 0.3f, p.Y + ny * (half - reach) * side + sa * reach * 0.3f);
        }
        // A knot or two: a dark oval with a ring round it, where a branch once grew.
        for (int k = 0; k < 1 + rng.Next(2); k++)
        {
            PointF p = log.At(0.2f + 0.6f * (float)rng.NextDouble());
            float o = ((float)rng.NextDouble() * 2 - 1) * half * 0.5f, kr = half * (0.18f + 0.14f * (float)rng.NextDouble());
            var kc = new PointF(p.X + nx * o, p.Y + ny * o);
            using var ring = new Pen(Color.FromArgb(120, Shade(bark, 0.5f)), MathF.Max(0.8f, kr * 0.25f));
            g.DrawEllipse(ring, kc.X - kr * 1.3f, kc.Y - kr, kr * 2.6f, kr * 2);
            using var knot = new SolidBrush(Shade(bark, 0.42f));
            g.FillEllipse(knot, kc.X - kr * 0.8f, kc.Y - kr * 0.6f, kr * 1.6f, kr * 1.2f);
        }
        // Moss on the top, toward the sunny side, in soft patches.
        for (int k = 0; k < 2 + rng.Next(3); k++)
        {
            PointF p = log.At(0.1f + 0.8f * (float)rng.NextDouble());
            float o = (sunSide >= 0 ? 1 : -1) * half * (0.1f + 0.5f * (float)rng.NextDouble()), mr = half * (0.3f + 0.4f * (float)rng.NextDouble());
            using var mp = new GraphicsPath();
            mp.AddClosedCurve(RockShape(p.X + nx * o, p.Y + ny * o, mr, 0.7f, rng), 0.5f);
            Color moss = Color.FromArgb(66 + rng.Next(30), 100 + rng.Next(30), 40 + rng.Next(16));
            using var mb = new PathGradientBrush(mp) { CenterColor = Color.FromArgb(190, moss), SurroundColors = [Color.FromArgb(0, moss)] };
            g.FillPath(mb, mp);
        }
        g.Restore(st);

        // The cut end we can see: the end whose direction points down the screen (toward us).
        // A disc of pale wood with growth rings, flattened by the slant (the more the log points
        // at us, the rounder it looks).
        float endDir = sa >= 0 ? 1 : -1;
        PointF end = log.At(endDir > 0 ? 1f : 0f);
        float squash = MathF.Abs(sa) * 0.5f + 0.08f;
        if (squash > 0.14f)
        {
            using var face = new GraphicsPath();
            using var fm = new Matrix();
            face.AddEllipse(-half, -half * squash, half * 2, half * squash * 2);
            fm.Translate(end.X - ca * endDir * half * squash * 0.3f, end.Y - sa * endDir * half * squash * 0.3f);
            fm.Rotate(log.Angle * 180 / MathF.PI);
            face.Transform(fm);
            Color wood = Color.FromArgb(170 + rng.Next(20), 140 + rng.Next(16), 96 + rng.Next(12));
            using (var wb = new SolidBrush(wood)) g.FillPath(wb, face);
            GraphicsState st2 = g.Save();
            g.SetClip(face, CombineMode.Intersect);
            for (int k = 1; k <= 5; k++)
            {
                float f = k / 6f;
                using var rp = new Pen(Color.FromArgb(70, Shade(wood, 0.72f)), MathF.Max(0.7f, half * 0.04f));
                using var rpath = new GraphicsPath();
                using var rm = new Matrix();
                rpath.AddEllipse(-half * f, -half * squash * f, half * 2 * f, half * squash * 2 * f);
                rm.Translate(end.X - ca * endDir * half * squash * 0.3f, end.Y - sa * endDir * half * squash * 0.3f);
                rm.Rotate(log.Angle * 180 / MathF.PI);
                rpath.Transform(rm);
                g.DrawPath(rp, rpath);
            }
            g.Restore(st2);
            using var endEdge = new Pen(Color.FromArgb(90, Shade(bark, 0.5f)), MathF.Max(0.8f, half * 0.05f));
            g.DrawPath(endEdge, face);
        }

        // Rim light and shade by which way each edge faces, never more than 50 alpha of light.
        PointF[] rim = path.PathPoints;
        for (int i = 0; i < rim.Length; i += 2)
        {
            PointF a = rim[i], b = rim[(i + 2) % rim.Length];
            float ex = b.X - a.X, ey = b.Y - a.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
            float onx = ey / el, ony = -ex / el;
            float facing = onx * -0.55f + ony * -0.83f;
            if (facing > 0.2f) using (var lit = new Pen(Color.FromArgb((int)(50 * facing), 250, 240, 220), MathF.Max(1f, half * 0.06f))) g.DrawLine(lit, a, b);
            else if (facing < -0.2f) using (var dark = new Pen(Color.FromArgb((int)(-100 * facing), 14, 12, 10), MathF.Max(1f, half * 0.08f))) g.DrawLine(dark, a, b);
        }
        if (log.Afloat)
        {
            // Floating: a dark waterline hugging the lower side, where the surface meets the wood.
            for (int i = 0; i <= n; i++)
            {
                PointF p = log.At(i / (float)n), q = log.At(Math.Min(n, i + 1) / (float)n);
                float side = ny >= 0 ? 1 : -1;
                using var wl = new Pen(Color.FromArgb(90, 8, 30, 34), MathF.Max(1.2f, half * 0.12f));
                g.DrawLine(wl, p.X + nx * half * side * 1.02f, p.Y + ny * half * side * 1.02f, q.X + nx * half * side * 1.02f, q.Y + ny * half * side * 1.02f);
            }
        }
        gb.FillPath(Brushes.White, path);
        noGrass.FillPath(Brushes.White, path);
    }

    private static Color Shade(Color c, float k) => Color.FromArgb(Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k)));

    /// <summary>
    /// One rock. In paint order: its soft shadow on the ground to the lower
    /// right; the body, lit from the upper left; a couple of creases where
    /// its faces meet; a speckle of grain; moss and a spot of lichen on top;
    /// a light edge where the sun catches its rim and a dark edge on the
    /// shaded side. The water is laid back over its submerged part afterwards.
    /// </summary>
    /// <param name="bare">No moss or lichen: a stone that will be under water (their green survived the water wrap as a lime glow).</param>
    private static void Rock(Graphics g, Graphics gb, Graphics noGrass, float x, float y, float r, float u, Random rng, bool bare = false)
    {
        PointF[] pts = RockShape(x, y, r, 0.8f, rng);
        using var path = new GraphicsPath();
        path.AddClosedCurve(pts, 0.3f);
        // Stones are not one grey: a warm sandstone, a cool blue-grey, a greenish one, lighter and darker.
        int k0 = 96 + rng.Next(60);
        Color body = rng.Next(4) switch
        {
            0 => Color.FromArgb(k0 + 10, k0 + 2, k0 - 12),       // warm
            1 => Color.FromArgb(k0 - 6, k0, k0 + 8),             // cool
            2 => Color.FromArgb(k0 - 4, k0 + 4, k0 - 6),         // greenish
            _ => Color.FromArgb(k0, k0, k0 - 2),                 // plain
        };

        // Shadow: three soft layers, growing and fainter, down and right.
        for (int k = 3; k >= 1; k--)
        {
            using var sh = (GraphicsPath)path.Clone();
            using var m = new Matrix();
            m.Translate(x, y); m.Scale(1 + 0.04f * k, 1 + 0.04f * k); m.Translate(-x, -y);
            m.Translate(r * 0.22f + k * u * 0.002f, r * 0.3f + k * u * 0.002f, MatrixOrder.Append);
            sh.Transform(m);
            using var sb = new SolidBrush(Color.FromArgb(30, 4, 14, 14));
            g.FillPath(sb, sh);
        }
        GraphicsState st;

        // The body: a gentle rounding toward the sun...
        using (var fill = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(x - r * 0.3f, y - r * 0.35f),
            CenterColor = Brushwork.Mix(body, Color.FromArgb(236, 234, 226), 0.22f),
            SurroundColors = [Brushwork.Mix(body, Color.FromArgb(28, 30, 32), 0.4f)],
        })
            g.FillPath(fill, path);

        st = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        // ...then FACES: a rock is not a pillow. The top face, toward the sun,
        // is lit: a patch made of the rim points on the sunny side plus a
        // ragged inner edge near the middle. The far side, away from the sun,
        // is a dark patch made the same way. Each is painted three times at
        // slightly different sizes, so its edge is a soft crease, not a line.
        Face(g, pts, x, y, r, -0.55f, -0.83f, 0.12f, Color.FromArgb(22, 250, 248, 238), rng);
        Face(g, pts, x, y, r, 0.55f, 0.83f, 0.3f, Color.FromArgb(24, 14, 16, 20), rng);
        // Creases: where two faces of the rock meet, a dark line with a lit edge beside it.
        int creases = 1 + rng.Next(3);
        for (int c = 0; c < creases; c++)
        {
            // From a point on the rim part of the way in, with a kink: a crack, not a ruled line.
            PointF a = pts[rng.Next(pts.Length)];
            float reach = 0.35f + 0.35f * (float)rng.NextDouble();
            PointF b = new(a.X + (x - a.X) * reach + ((float)rng.NextDouble() - 0.5f) * r * 0.3f, a.Y + (y - a.Y) * reach + ((float)rng.NextDouble() - 0.5f) * r * 0.3f);
            PointF m = new((a.X + b.X) / 2 + ((float)rng.NextDouble() - 0.5f) * r * 0.2f, (a.Y + b.Y) / 2 + ((float)rng.NextDouble() - 0.5f) * r * 0.2f);
            using var lit = new Pen(Color.FromArgb(30, 240, 238, 230), MathF.Max(1f, r * 0.03f));
            using var dark = new Pen(Color.FromArgb(64, 20, 22, 24), MathF.Max(1f, r * 0.03f));
            g.DrawCurve(lit, [new PointF(a.X - 1, a.Y - 1), new PointF(m.X - 1, m.Y - 1), new PointF(b.X - 1, b.Y - 1)], 0.5f);
            g.DrawCurve(dark, [a, m, b], 0.5f);
        }
        // Grain: a speckle of slightly lighter and darker specks.
        int specks = (int)Math.Min(900, r * r / (u * 0.0022f * u * 0.0022f));
        for (int s = 0; s < specks; s++)
        {
            float sx = x + ((float)rng.NextDouble() * 2 - 1) * r * 1.3f, sy = y + ((float)rng.NextDouble() * 2 - 1) * r;
            float sr = u * (0.0005f + 0.0011f * (float)rng.NextDouble());
            using var sb = new SolidBrush(rng.Next(2) == 0 ? Color.FromArgb(34, 255, 255, 250) : Color.FromArgb(44, 10, 12, 14));
            g.FillEllipse(sb, sx - sr, sy - sr, sr * 2, sr * 2);
        }
        // Moss on the top and sunny side, and a spot or two of pale lichen.
        int mossy = bare ? 0 : 2 + rng.Next(4);
        for (int m = 0; m < mossy; m++)
        {
            float mx = x - r * 0.5f + (float)rng.NextDouble() * r * 0.9f, my = y - r * 0.6f + (float)rng.NextDouble() * r * 0.7f;
            float mr = r * (0.18f + 0.3f * (float)rng.NextDouble());
            using var mp = new GraphicsPath();
            mp.AddClosedCurve(RockShape(mx, my, mr, 0.8f, rng), 0.5f);
            Color moss = Color.FromArgb(70 + rng.Next(30), 104 + rng.Next(30), 44 + rng.Next(16));
            using var mb = new PathGradientBrush(mp) { CenterColor = Color.FromArgb(200, moss), SurroundColors = [Color.FromArgb(0, moss)] };
            g.FillPath(mb, mp);
        }
        if (!bare && rng.Next(3) > 0)
        {
            float lx = x + ((float)rng.NextDouble() - 0.5f) * r, ly = y + ((float)rng.NextDouble() - 0.6f) * r * 0.6f, lr = r * (0.1f + 0.15f * (float)rng.NextDouble());
            using var lp = new GraphicsPath();
            lp.AddClosedCurve(RockShape(lx, ly, lr, 0.9f, rng), 0.5f);
            using var lb = new SolidBrush(Color.FromArgb(120, 176, 184, 150));
            g.FillPath(lb, lp);
        }
        g.Restore(st);

        // The rim: lit where an edge faces the sun (up and left), dark where it faces away.
        for (int i = 0; i < pts.Length; i++)
        {
            PointF a = pts[i], b = pts[(i + 1) % pts.Length];
            float ex = b.X - a.X, ey = b.Y - a.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
            float nx = ey / el, ny = -ex / el;                            // outward (points run anticlockwise on screen)
            float facing = nx * -0.55f + ny * -0.83f;
            if (facing > 0.2f)
                using (var lit = new Pen(Color.FromArgb((int)(50 * facing), 250, 248, 240), MathF.Max(1f, r * 0.04f))) g.DrawLine(lit, a, b);
            else if (facing < -0.2f)
                using (var dark = new Pen(Color.FromArgb((int)(-110 * facing), 14, 16, 18), MathF.Max(1f, r * 0.06f))) g.DrawLine(dark, a, b);
        }
        // The part standing in the water is not painted here: once every rock
        // is down, the painter lays the water back over it pixel by pixel
        // (PondPainter.WrapWater), deeper water hiding more of the stone.

        gb.FillPath(Brushes.White, path);
        noGrass.FillPath(Brushes.White, path);
    }

    /// <summary>
    /// A face of a rock: the rim points within about 70 degrees of a
    /// direction (dx, dy) from the middle, joined by two ragged points near
    /// the middle (pushed "inset" of r along that direction), filled three
    /// times at sizes 0.94, 1 and 1.06 so its edge is soft.
    /// </summary>
    private static void Face(Graphics g, PointF[] pts, float x, float y, float r, float dx, float dy, float inset, Color c, Random rng)
    {
        var side = new List<PointF>();
        foreach (PointF p in pts)
        {
            float px = p.X - x, py = p.Y - y, l = MathF.Max(1e-3f, MathF.Sqrt(px * px + py * py));
            if ((px * dx + py * dy) / l > 0.35f) side.Add(p);
        }
        if (side.Count < 2) return;
        // Order them round the rim, then close the patch through two inner points.
        side = side.OrderBy(p => MathF.Atan2(p.Y - y - dy, p.X - x - dx)).ToList();
        PointF first = side[0], last = side[^1];
        side.Add(new PointF(x + dx * r * inset + (last.X - x) * 0.25f + ((float)rng.NextDouble() - 0.5f) * r * 0.2f, y + dy * r * inset + (last.Y - y) * 0.25f + ((float)rng.NextDouble() - 0.5f) * r * 0.2f));
        side.Add(new PointF(x + dx * r * inset + (first.X - x) * 0.25f + ((float)rng.NextDouble() - 0.5f) * r * 0.2f, y + dy * r * inset + (first.Y - y) * 0.25f + ((float)rng.NextDouble() - 0.5f) * r * 0.2f));
        using var brush = new SolidBrush(c);
        foreach (float k in (ReadOnlySpan<float>)[0.94f, 1f, 1.06f])
            g.FillPolygon(brush, side.Select(p => new PointF(x + (p.X - x) * k, y + (p.Y - y) * k)).ToArray());
    }
}
