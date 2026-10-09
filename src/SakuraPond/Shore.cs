using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// Paints the corner of land: the lawn, the bare damp edge where it meets the
/// water, the rocks, and the stepping stones. Everything here is painted
/// once, into the backdrop. The grass that MOVES is Grass.cs.
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

        // The ground under the grass.
        GraphicsState st = g.Save();
        g.SetClip(land, CombineMode.Intersect);
        using (var moss = new SolidBrush(Color.FromArgb(58, 78, 42))) g.FillPath(moss, land);
        for (int i = 0; i < 40; i++)
        {
            float x = x0 + (float)rng.NextDouble() * (x1 - x0), y = y0 + (float)rng.NextDouble() * (y1 - y0);
            float r = u * (0.03f + 0.08f * (float)rng.NextDouble());
            Color patch = rng.Next(3) == 0 ? Color.FromArgb(78, 64, 44) : Color.FromArgb(80, 102, 50);
            using var path = new GraphicsPath();
            path.AddEllipse(x - r, y - r * 0.7f, r * 2, r * 1.4f);
            using var b = new PathGradientBrush(path) { CenterColor = Color.FromArgb(110, patch), SurroundColors = [Color.FromArgb(0, patch)] };
            g.FillPath(b, path);
        }
        g.Restore(st);

        // The blades, on the carpet. Lighter toward the upper left, where the sun comes from.
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
            float len = u * (0.009f + 0.013f * (float)rng.NextDouble());
            float az = ((float)rng.NextDouble() - 0.5f) * 1.8f;                 // its own fan, left or right of straight up
            float lean = 0.1f + 0.35f * (float)rng.NextDouble();                // all of them lean a little downwind (right)
            float tx = x + len * (lean + 0.55f * MathF.Sin(az)), ty = y - len * Light.Lift * MathF.Cos(az) * 0.9f;
            float wid = u * (0.0015f + 0.0013f * (float)rng.NextDouble());
            // The shadow: the blade flattened onto the ground, down and to the right.
            float sx = x + len * (lean * 0.4f + Light.ShadowX * 0.3f), sy = y + len * Light.ShadowY * 0.28f;
            Sliver(carpet, shadow, x, y, sx, sy, wid * 0.9f, polyArr);
            Color c0 = greens[rng.Next(greens.Length)];
            float light = 0.1f * (1 - x / w) + 0.08f * (1 - y / h);
            c0 = Brushwork.Mix(c0, Color.FromArgb(176, 190, 110), light);
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
        var mid = new PointF[bank.Length];
        for (int i = 0; i < bank.Length; i++)
        {
            PointF p = bank[i];
            float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
            float t = i / (bank.Length - 1f);
            float wide = u * (0.018f + 0.03f * (0.5f + 0.5f * MathF.Sin(t * 9 + ph)) * (0.5f + 0.5f * MathF.Sin(t * 4.3f + 2 * ph))
                + 0.008f * MathF.Sin(t * 31 + ph2));
            inner[i] = new PointF(p.X + ox / ol * wide, p.Y + oy / ol * wide);
            core[i] = new PointF(p.X + ox / ol * wide * 0.65f, p.Y + oy / ol * wide * 0.65f);
            mid[i] = new PointF(p.X + ox / ol * wide * 0.4f, p.Y + oy / ol * wide * 0.4f);
        }
        using var strip = Band(bank, inner);
        using var wet = Band(bank, mid);
        using var keepOut = Band(bank, core);
        using (var sand = new SolidBrush(Color.FromArgb(132, 122, 94))) g.FillPath(sand, strip);
        using (var damp = new SolidBrush(Color.FromArgb(150, 96, 90, 70))) g.FillPath(damp, wet);
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
    public static void Rocks(Graphics g, Graphics gb, Graphics noGrass, PointF[] bank, PointF corner, Region water, float u, Random rng)
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
            Rock(g, gb, noGrass, c.X, c.Y, r, water, u, rng);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// One rock. In paint order: its soft shadow on the ground to the lower
    /// right; a pale rim on the water round its wet foot (water meeting a
    /// stone); the body, lit from the upper left; a couple of creases where
    /// its faces meet; a speckle of grain; moss and a spot of lichen on top;
    /// a light edge where the sun catches its rim and a dark edge on the
    /// shaded side; and the dark wet band where it stands in the water.
    /// </summary>
    private static void Rock(Graphics g, Graphics gb, Graphics noGrass, float x, float y, float r, Region water, float u, Random rng)
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
        // The pale rim on the water round its foot.
        GraphicsState st = g.Save();
        g.SetClip(water, CombineMode.Intersect);
        using (var rim = new Pen(Color.FromArgb(64, 214, 228, 224), MathF.Max(1.2f, r * 0.06f)))
            g.DrawPath(rim, path);
        g.Restore(st);

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
        int mossy = 2 + rng.Next(4);
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
        if (rng.Next(3) > 0)
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
                using (var lit = new Pen(Color.FromArgb((int)(120 * facing), 250, 248, 240), MathF.Max(1f, r * 0.05f))) g.DrawLine(lit, a, b);
            else if (facing < -0.2f)
                using (var dark = new Pen(Color.FromArgb((int)(-110 * facing), 14, 16, 18), MathF.Max(1f, r * 0.06f))) g.DrawLine(dark, a, b);
        }
        // The wet band: the part of the rock standing in the water is darker.
        st = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        g.SetClip(water, CombineMode.Intersect);
        using (var wet = new SolidBrush(Color.FromArgb(95, 8, 26, 30))) g.FillPath(wet, path);
        g.Restore(st);

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

    /// <summary>
    /// Flat stepping stones from the land's corner to the middle of the
    /// water's edge, a stride apart, left foot, right foot: pale lumpy slabs
    /// (the rock outline, flatter) with a soft shadow and a lit edge.
    /// </summary>
    public static void SteppingStones(Graphics g, Graphics gb, Graphics noGrass, PointF[] bank, PointF corner, bool left, float u, int h, Random rng)
    {
        var start = new PointF(corner.X + (left ? -u * 0.02f : u * 0.02f), h + u * 0.03f);
        PointF end = bank[bank.Length / 2 + rng.Next(-4, 5)];
        float dx = end.X - start.X, dy = end.Y - start.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float stride = u * 0.075f;
        int n = Math.Max(2, (int)(d / stride));
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.3f) / n;
            float side = (i % 2 == 0 ? 1 : -1) * u * 0.012f;
            float x = start.X + dx * t - dy / d * side, y = start.Y + dy * t + dx / d * side;
            float r = u * (0.026f + 0.008f * (float)rng.NextDouble());
            PointF[] pts = RockShape(x, y, r, 0.72f, rng);
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, 0.4f);
            using var sh = (GraphicsPath)path.Clone();
            using (var m = new Matrix(1, 0, 0, 1, u * 0.004f, u * 0.006f)) sh.Transform(m);
            using (var sb = new SolidBrush(Color.FromArgb(70, 8, 22, 12))) g.FillPath(sb, sh);
            int k = 148 + rng.Next(25);
            using (var fill = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(x - r * 0.3f, y - r * 0.3f),
                CenterColor = Color.FromArgb(k + 36, k + 32, k + 22),
                SurroundColors = [Color.FromArgb(k - 34, k - 36, k - 40)],
            })
                g.FillPath(fill, path);
            GraphicsState st = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            for (int s = 0; s < 60; s++)
            {
                float sx = x + ((float)rng.NextDouble() * 2 - 1) * r * 1.2f, sy = y + ((float)rng.NextDouble() * 2 - 1) * r * 0.8f;
                float sr = u * (0.0005f + 0.0011f * (float)rng.NextDouble());
                using var sb = new SolidBrush(rng.Next(2) == 0 ? Color.FromArgb(32, 255, 255, 250) : Color.FromArgb(34, 20, 20, 24));
                g.FillEllipse(sb, sx - sr, sy - sr, sr * 2, sr * 2);
            }
            g.Restore(st);
            using (var edge = new Pen(Color.FromArgb(70, 30, 30, 30), MathF.Max(1f, r * 0.04f))) g.DrawPath(edge, path);
            gb.FillPath(Brushes.White, path);
            noGrass.FillPath(Brushes.White, path);
        }
    }
}
