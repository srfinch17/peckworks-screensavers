using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core.Sakura;

namespace Sakura;

/// <summary>The finished backdrop: a still picture plus a few facts the animation needs.</summary>
internal sealed class Scenery
{
    public required uint[] Pixels { get; init; }             // the painted picture, same layout as a FrameBuffer
    public required float HorizonY { get; init; }            // where the lake meets the far shore
    public required List<PointF> BlossomSpots { get; init; } // clusters on the top branches that petals can drop from
}

/// <summary>
/// Paints the backdrop: blue sky, Mount Fuji with its snow cap, a lake with the
/// mountain reflected in it, a far shoreline, two banks of land in front of the
/// lake with blossoming trees (and one pine) standing on them, and cherry
/// branches framing the top of the screen.
///
/// FEYNMAN VERSION: the scenery never moves, so painting it 60 times a second
/// would be wasted effort. Instead we paint it ONCE, like a stage backdrop,
/// when the screensaver starts. Every frame afterward just copies the finished
/// backdrop and draws the moving petals on top. Copying a picture is far cheaper
/// than painting one.
///
/// We paint back to front, the way a painter layers a landscape: sky first,
/// then the mountain over the sky, then the lake, then the shore, then the
/// banks, then the trees standing on them, and the branches last because they
/// are closest to us. Each layer simply covers whatever is behind it.
///
/// All positions are fractions of the screen's width (w) or height (h), and
/// the sizes of trees and branches are fractions of "u" (the height, or less
/// on a tall screen; see Paint), so the same picture fits any screen size. A
/// few random numbers (branch shapes,
/// snow streaks, tree speckles) make each run a little different.
///
/// This uses System.Drawing (GDI+), the standard .NET 2D drawing kit: shapes,
/// gradients, and anti-aliasing (smoothed edges). It is too slow for the
/// petals, but perfect for painting a backdrop once. The trees, banks and
/// branches themselves are painted by the shared Brushwork helpers in the
/// engine (Core/Sakura/Brushwork.cs), so every sakura scene has the same hand.
/// </summary>
internal static class SceneryPainter
{
    public static Scenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;   // smooth edges instead of staircase pixels

        float horizon = h * 0.78f;
        var fuji = new Mountain(w, h, horizon, rng);

        // Sizes of trees and branches are measured in "u". On a normal wide
        // screen u is simply the height. On a tall (portrait) or square
        // screen, sizing by height would make trees far too big for the
        // width, so u drops to the height a 16:9 screen of this width would
        // have. Positions still use w and h directly.
        float u = Math.Min(h, w * 9f / 16f);

        PaintSky(g, w, horizon);
        PaintMountain(g, fuji, 1f);
        PaintHaze(g, w, fuji);
        PaintHills(g, w, h, horizon, rng);
        PaintLake(g, w, h, horizon, fuji, rng);
        PaintShore(g, w, h, horizon, rng);
        var (leftBank, rightHill) = MakeGround(w, h);
        PaintGround(g, leftBank, rightHill);
        PaintGrove(g, w, h, u, leftBank, rightHill, rng);
        List<PointF> spots = PaintBranches(g, w, h, u, rng);

        return new Scenery { Pixels = Brushwork.ToPixels(bmp), HorizonY = horizon, BlossomSpots = spots };
    }

    // ================================================================ sky

    private static void PaintSky(Graphics g, int w, float horizon)
    {
        // A gradient brush blends smoothly between colors. Here: deep blue at the
        // top, clear mid blue, then pale near the horizon, which is how a real
        // sky looks (you look through more air near the horizon, so it washes out).
        using var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1),
            Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(38, 88, 172), Color.FromArgb(92, 148, 214), Color.FromArgb(204, 224, 242)],
                Positions = [0f, 0.55f, 1f],
            },
        };
        g.FillRectangle(sky, 0, 0, w, horizon + 1);
    }

    // ================================================================ mountain

    /// <summary>
    /// Mount Fuji's outline and snow cap, worked out once so that the mountain
    /// and its reflection in the lake are exactly the same shape.
    /// </summary>
    private sealed class Mountain
    {
        public readonly float CenterX, Top, Base, HalfWidth, TopHalfWidth;
        public readonly PointF[] SnowEdge;          // polygon: everything above the jagged snow line
        public readonly (PointF A, PointF B)[] Gullies; // faint streaks in the snow

        public Mountain(int w, int h, float horizon, Random rng)
        {
            // Width is measured in screen HEIGHTS, so the mountain keeps its
            // shape on wide and narrow screens alike.
            CenterX = w * 0.58f;
            Top = h * 0.40f;
            Base = horizon + 2;
            HalfWidth = h * 0.78f;
            TopHalfWidth = h * 0.045f;

            float height = Base - Top;
            float snowLine = Top + height * 0.36f;

            // The snow's lower edge: a gently wavy line with "fingers" of snow of
            // different widths and lengths reaching down the ravines, like the
            // photo. Each finger is a tapering spike; where fingers overlap, the
            // longest one wins.
            var fingers = new (float X, float Width, float Length)[90];
            for (int i = 0; i < fingers.Length; i++)
                fingers[i] = (CenterX + HalfWidth * 0.8f * ((float)rng.NextDouble() * 2 - 1),
                              HalfWidth * (0.008f + 0.03f * (float)rng.NextDouble()),
                              height * (0.02f + 0.22f * (float)Math.Pow(rng.NextDouble(), 2.2)));

            // The baseline itself wanders up and down: a "random walk" (each
            // step nudges the previous value a little, like a drunk walking a
            // line), smoothed so it drifts rather than jitters.
            const int n = 400;
            var wander = new float[n + 1];
            float walk = 0;
            for (int i = 0; i <= n; i++)
            {
                walk = walk * 0.97f + ((float)rng.NextDouble() - 0.5f) * 0.03f;
                wander[i] = walk;
            }
            for (int pass = 0; pass < 3; pass++)                          // smooth: average each value with its neighbors
                for (int i = 1; i < n; i++)
                    wander[i] = (wander[i - 1] + wander[i] + wander[i + 1]) / 3f;

            var pts = new List<PointF> { new(CenterX + HalfWidth, Top - 20) };
            for (int i = n; i >= 0; i--)
            {
                float x = CenterX - HalfWidth + i * (2 * HalfWidth / n);
                float y = snowLine + height * (0.03f + wander[i] * 0.6f);
                foreach (var f in fingers)
                {
                    float t = 1 - MathF.Abs(x - f.X) / f.Width;          // 1 at the finger's middle, 0 at its edges
                    if (t > 0) y = MathF.Max(y, snowLine + f.Length * MathF.Pow(t, 1.2f));
                }
                pts.Add(new PointF(x, y));
            }
            pts.Add(new PointF(CenterX - HalfWidth, Top - 20));
            SnowEdge = [.. pts];

            // Faint streaks fanning out from the summit down the slopes. Each one
            // starts at a spot along the rim and heads outward in the same
            // direction, so they spread like the spokes of a fan and never cross.
            Gullies = new (PointF, PointF)[18];
            for (int i = 0; i < Gullies.Length; i++)
            {
                float f = (i + (float)rng.NextDouble()) / Gullies.Length * 2 - 1;   // -1 (left) ... +1 (right)
                var a = new PointF(CenterX + f * TopHalfWidth, Top + height * 0.02f);
                var b = new PointF(CenterX + f * HalfWidth * 0.32f,
                                   snowLine + (float)rng.NextDouble() * height * 0.12f);
                Gullies[i] = (a, b);
            }
        }

        /// <summary>
        /// The outline. Fuji's slopes are "concave": steep near the top, then
        /// flaring out gently toward the base. A Bezier curve (a smooth curve
        /// steered by two invisible "handle" points) draws that sweep: we put one
        /// handle low near the base and one high near the top.
        /// </summary>
        public GraphicsPath CreateOutline()
        {
            float height = Base - Top;
            var path = new GraphicsPath();
            // Left slope, base to summit.
            path.AddBezier(
                new PointF(CenterX - HalfWidth, Base),
                new PointF(CenterX - HalfWidth * 0.55f, Base - height * 0.16f),
                new PointF(CenterX - TopHalfWidth * 2.6f, Top + height * 0.10f),
                new PointF(CenterX - TopHalfWidth, Top));
            // The summit is a flattened crater rim with small bumps, not a point.
            path.AddLines([
                new PointF(CenterX - TopHalfWidth, Top),
                new PointF(CenterX - TopHalfWidth * 0.45f, Top - height * 0.012f),
                new PointF(CenterX + TopHalfWidth * 0.05f, Top + height * 0.006f),
                new PointF(CenterX + TopHalfWidth * 0.55f, Top - height * 0.008f),
                new PointF(CenterX + TopHalfWidth, Top),
            ]);
            // Right slope, a touch wider than the left so it is not perfectly symmetric.
            float right = HalfWidth * 1.06f;
            path.AddBezier(
                new PointF(CenterX + TopHalfWidth, Top),
                new PointF(CenterX + TopHalfWidth * 2.6f, Top + height * 0.10f),
                new PointF(CenterX + right * 0.55f, Base - height * 0.16f),
                new PointF(CenterX + right, Base));
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Paints the mountain. "opacity" lets the same code paint the faint
    /// reflection in the lake (opacity 0.3) and the real mountain (opacity 1).
    /// </summary>
    private static void PaintMountain(Graphics g, Mountain m, float opacity)
    {
        int A(int a) => (int)(a * opacity);

        using GraphicsPath outline = m.CreateOutline();

        // Rock: dusky blue-gray, darker toward the base.
        using (var rock = new LinearGradientBrush(new PointF(0, m.Top - 1), new PointF(0, m.Base + 1),
                   Color.FromArgb(A(255), 124, 142, 178), Color.FromArgb(A(255), 82, 100, 138)))
            g.FillPath(rock, outline);

        // "Clipping" = masking tape. From here on, anything we paint only lands
        // INSIDE the mountain outline. That lets us paint the snow cap as a
        // simple wide zigzag shape; the mask trims it to the mountain's edges.
        //
        // Intersect means "keep whatever tape the caller already put down, and
        // add ours on top". (The default would REPLACE the caller's tape, which
        // silently undid the lake's "reflection stops here" clip.) Save/Restore
        // hands the caller's tape back exactly as it was when we are done.
        GraphicsState before = g.Save();
        g.SetClip(outline, CombineMode.Intersect);

        using (var snow = new SolidBrush(Color.FromArgb(A(255), 248, 251, 255)))
            g.FillPolygon(snow, m.SnowEdge);

        using (var streak = new Pen(Color.FromArgb(A(40), 120, 140, 180), Math.Max(1f, m.TopHalfWidth / 25f)))
            foreach (var (a, b) in m.Gullies)
                g.DrawLine(streak, a, b);

        // The sun is on the left, so the right-hand face sits in shadow. The
        // shadow fades in across a band right of the summit (a gradient from
        // clear to shaded), then stays shaded, so there is no hard edge.
        float fadeStart = m.CenterX, fadeEnd = m.CenterX + m.HalfWidth * 0.22f;
        float top = m.Top - 5, bottom = m.Base + 5;
        Color shadeColor = Color.FromArgb(A(85), 30, 45, 90);
        using (var fade = new LinearGradientBrush(new PointF(fadeStart - 1, 0), new PointF(fadeEnd + 1, 0),
                   Color.FromArgb(0, shadeColor), shadeColor))
            g.FillRectangle(fade, fadeStart, top, fadeEnd - fadeStart, bottom - top);
        using (var solid = new SolidBrush(shadeColor))
            g.FillRectangle(solid, fadeEnd, top, m.HalfWidth * 1.2f, bottom - top);

        g.Restore(before);   // peel off our tape, leaving the caller's in place
    }

    /// <summary>A soft band of mist along the mountain's foot, which makes it look far away.</summary>
    private static void PaintHaze(Graphics g, int w, Mountain m)
    {
        float top = m.Base - (m.Base - m.Top) * 0.32f;
        using var haze = new LinearGradientBrush(new PointF(0, top - 1), new PointF(0, m.Base + 1),
            Color.FromArgb(0, 205, 222, 240), Color.FromArgb(175, 205, 222, 240));
        g.FillRectangle(haze, 0, top, w, m.Base - top + 1);
    }

    /// <summary>Low, bluish foothills along the horizon, made of two overlapping waves.</summary>
    private static void PaintHills(Graphics g, int w, int h, float horizon, Random rng)
    {
        double p1 = rng.NextDouble() * 6, p2 = rng.NextDouble() * 6;
        var pts = new List<PointF> { new(0, horizon + 2) };
        for (float x = 0; x <= w; x += Math.Max(2, h / 300f))
        {
            double t = x / (double)h;
            float y = horizon - h * (float)(0.02 + 0.012 * Math.Sin(t * 5 + p1) + 0.007 * Math.Sin(t * 13 + p2));
            pts.Add(new PointF(x, y));
        }
        pts.Add(new PointF(w, horizon + 2));
        using var hills = new SolidBrush(Color.FromArgb(98, 122, 154));
        g.FillPolygon(hills, pts.ToArray());
    }

    // ================================================================ lake

    private static void PaintLake(Graphics g, int w, int h, float horizon, Mountain m, Random rng)
    {
        using (var water = new LinearGradientBrush(new PointF(0, horizon - 1), new PointF(0, h + 1),
                   Color.FromArgb(158, 190, 220), Color.FromArgb(48, 96, 150)))
            g.FillRectangle(water, 0, horizon, w, h - horizon);

        // The reflection: paint the mountain again, flipped upside down around
        // the horizon line, and faint. The "transform" is a rule applied to
        // every point before drawing. Matrix(1, 0, 0, -1, 0, 2*horizon) means
        // "keep x, and replace y with (2 * horizon - y)", which is a mirror
        // flip across the horizon line.
        // The reflection only lives in the upper three quarters of the lake.
        // Farther down, the wash below has all but hidden it anyway, and
        // stopping early keeps the reflected snow cap from poking into the
        // near water as pale ghost spikes. (The clip is set BEFORE the flip,
        // so it is measured in normal screen coordinates.)
        g.SetClip(new RectangleF(0, horizon, w, (h - horizon) * 0.75f));
        g.Transform = new Matrix(1, 0, 0, -1, 0, 2 * horizon);
        PaintMountain(g, m, 0.28f);
        g.ResetTransform();
        g.ResetClip();

        // Real reflections fade as the water comes toward you (you see more
        // of the sky's glare and less of the far shore). Wash the lower lake
        // with water color, from clear just below the shore to mostly solid
        // at the bottom of the screen.
        float fadeTop = horizon + (h - horizon) * 0.2f;
        using (var wash = new LinearGradientBrush(new PointF(0, fadeTop - 1), new PointF(0, h + 1),
                   Color.FromArgb(0, 60, 106, 160), Color.FromArgb(245, 52, 98, 152)))
            g.FillRectangle(wash, 0, fadeTop, w, h - fadeTop);

        // Hundreds of faint horizontal streaks, some light and some dark, break
        // the reflection up the way small ripples do.
        using var light = new Pen(Color.FromArgb(26, 235, 244, 255), Math.Max(1f, h / 700f));
        using var dark = new Pen(Color.FromArgb(22, 20, 50, 90), Math.Max(1f, h / 700f));
        for (int i = 0; i < 500; i++)
        {
            float y = horizon + (float)Math.Pow(rng.NextDouble(), 0.8) * (h - horizon);
            float depth = (y - horizon) / (h - horizon);            // 0 = far, 1 = near
            float len = w * (0.01f + 0.08f * (float)rng.NextDouble()) * (0.4f + depth);
            float x = (float)rng.NextDouble() * w;
            g.DrawLine(i % 2 == 0 ? light : dark, x, y, x + len, y);
        }
    }

    /// <summary>The far shore: a dark band of trees with a few tiny white buildings.</summary>
    private static void PaintShore(Graphics g, int w, int h, float horizon, Random rng)
    {
        float step = Math.Max(2, h * 0.005f);
        for (float x = -step; x <= w + step; x += step)
        {
            float r = h * (0.004f + 0.006f * (float)rng.NextDouble());
            int green = 64 + rng.Next(28);
            using var b = new SolidBrush(Color.FromArgb(34 + rng.Next(18), green, 50 + rng.Next(12)));
            float y = horizon - h * 0.004f - (float)rng.NextDouble() * h * 0.006f;
            g.FillEllipse(b, x - r, y - r, r * 2, r * 1.6f);
        }
        using (var strip = new SolidBrush(Color.FromArgb(30, 46, 40)))
            g.FillRectangle(strip, 0, horizon - h * 0.002f, w, h * 0.006f);

        using var building = new SolidBrush(Color.FromArgb(214, 214, 208));
        for (int i = 0; i < 10; i++)
        {
            float bw = h * (0.004f + 0.006f * (float)rng.NextDouble());
            float bh = h * (0.003f + 0.004f * (float)rng.NextDouble());
            float x = w * (0.40f + 0.45f * (float)rng.NextDouble());
            g.FillRectangle(building, x, horizon - bh, bw, bh);
        }
    }

    // ================================================================ foreground

    /// <summary>The land in front of the lake: a dark bank at the lower left and a grassy rise at the lower right.</summary>
    private static (Bank Left, Bank Right) MakeGround(int w, int h)
    {
        // The left bank slopes down toward the water and meets the bottom of
        // the screen 42% of the way across. The right hill rises out of the
        // water 50% of the way across and runs to the right edge, so the tree
        // on the right has ground to stand on.
        var left = new Bank([
            new PointF(-2, h * 0.885f), new PointF(w * 0.10f, h * 0.893f), new PointF(w * 0.18f, h * 0.90f),
            new PointF(w * 0.28f, h * 0.918f), new PointF(w * 0.36f, h * 0.935f), new PointF(w * 0.42f, h + 2)], h);
        var right = new Bank([
            new PointF(w * 0.50f, h + 2), new PointF(w * 0.56f, h * 0.955f), new PointF(w * 0.68f, h * 0.94f),
            new PointF(w * 0.80f, h * 0.948f), new PointF(w * 0.90f, h * 0.93f), new PointF(w + 2, h * 0.915f)], h);
        return (left, right);
    }

    /// <summary>Paints the two banks: a dark earthy one on the left, a grassy one on the right.</summary>
    private static void PaintGround(Graphics g, Bank left, Bank right)
    {
        left.Paint(g, Color.FromArgb(74, 86, 70), Color.FromArgb(40, 46, 42), Color.FromArgb(120, 104, 122, 92));
        right.Paint(g, Color.FromArgb(134, 146, 88), Color.FromArgb(82, 92, 56), Color.FromArgb(140, 168, 182, 112));
    }

    /// <summary>
    /// The grove of blossoming trees (and one pine) at the lower left, and one
    /// tree standing on the hill at the right. Every trunk's foot is drawn a
    /// little BELOW the ground line under it (asked from the Bank), so the
    /// tree visibly stands on the bank's surface and nothing floats. The foot
    /// is painted over the bank, not hidden by it; it looks planted because
    /// it ends past the rim, the same cue a real drawing uses.
    ///
    /// Each tree must sit within its bank's left-to-right span: past the ends,
    /// YAt hands back the end point's height, which for the left bank is the
    /// bottom of the screen. The x values below were chosen with that in mind.
    ///
    /// Painted in depth order: the pine stands behind the grove, so it goes
    /// first, and wherever a canopy overlaps it the blossoms cover it.
    ///
    /// Each tree is described by where it stands (X, a fraction of the width),
    /// how far its canopy's center sits above the ground there (Lift), and its
    /// canopy size (R). Lift and R are fractions of u, so a tree's height and
    /// width scale together on every screen shape; only its foot follows the
    /// bank, which is measured in h.
    /// </summary>
    private static void PaintGrove(Graphics g, int w, int h, float u, Bank left, Bank right, Random rng)
    {
        (float X, float Lift, float R)[] trees =
        [
            (0.00f, 0.115f, 0.15f), (0.08f, 0.09f, 0.12f), (0.15f, 0.107f, 0.11f),
            (0.26f, 0.105f, 0.10f), (0.32f, 0.09f, 0.08f), (0.38f, 0.097f, 0.055f),
        ];

        float pineFoot = left.YAt(w * 0.21f) + u * 0.02f;
        Brushwork.Pine(g, w * 0.21f, pineFoot - u * 0.37f, pineFoot, u, rng);

        // Trunks first, then all the canopies, so a canopy can hide the top of
        // its neighbor's trunk but a trunk is never painted over blossoms.
        foreach (var t in trees)
            Brushwork.Trunk(g, w * t.X, left.YAt(w * t.X) + u * 0.03f, left.YAt(w * t.X) - u * t.Lift, u * t.R, rng);
        foreach (var t in trees)
            Brushwork.Canopy(g, w * t.X, left.YAt(w * t.X) - u * t.Lift, u * t.R, u, rng);

        // The lone tree on the right hill. It stands inward from the edge so it
        // does not run into the branch that reaches in from the right.
        float rx = w * 0.87f, ry = right.YAt(rx) - u * 0.165f;
        Brushwork.Trunk(g, rx, right.YAt(rx) + u * 0.03f, ry, u * 0.14f, rng);
        Brushwork.Canopy(g, rx, ry, u * 0.14f, u, rng);
    }

    // ================================================================ branches

    /// <summary>
    /// Cherry branches reaching in from the top corners, covered in blossom.
    /// Returns the blossom cluster positions so petals can fall from them.
    /// The growing itself is Brushwork.Branches; this method only says where
    /// each branch starts and which way it heads.
    /// </summary>
    private static List<PointF> PaintBranches(Graphics g, int w, int h, float u, Random rng)
    {
        // Angles are in radians, measured clockwise from "pointing right"
        // (because screen y grows downward): 0 = right, pi/2 = down, pi = left.
        // Start points are positions (w, h); lengths and thicknesses are sizes (u).
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.04f), 0.25f, u * 0.55f, u * 0.020f, 3),             // top left, reaching right
            new(new PointF(w * 0.12f, -h * 0.01f), 1.15f, u * 0.30f, u * 0.012f, 2),             // top left, hanging down
            new(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.35f, u * 0.60f, u * 0.024f, 3),   // top right, reaching left
            new(new PointF(w * 1.01f, h * 0.30f), MathF.PI - 0.12f, u * 0.45f, u * 0.018f, 3),   // right edge, lower
            new(new PointF(w * 0.74f, -h * 0.01f), MathF.PI / 2 + 0.35f, u * 0.32f, u * 0.012f, 2), // top, hanging down
        ];
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng,
            wood: Color.FromArgb(58, 38, 40),
            petalLight: Color.FromArgb(255, 246, 249),
            petalDeep: Color.FromArgb(244, 162, 192));

        // Only clusters actually on screen and in the upper part are good drop points.
        return spots.Where(s => s.X >= 0 && s.X < w && s.Y >= 0 && s.Y < h * 0.7f).ToList();
    }
}
