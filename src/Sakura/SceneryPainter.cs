using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
/// petals, but perfect for painting a backdrop once.
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
        PaintGround(g, h, leftBank, rightHill);
        PaintGrove(g, w, h, u, leftBank, rightHill, rng);
        List<PointF> spots = PaintBranches(g, w, h, u, rng);

        return new Scenery { Pixels = ToPixels(bmp), HorizonY = horizon, BlossomSpots = spots };
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

    /// <summary>
    /// One bank of land: its top edge as a line of points (left to right), and
    /// a way to ask "how high is the ground at this x?".
    ///
    /// Why this exists: earlier, the ground was a plain polygon and each tree
    /// had its trunk end at a fixed height. Where the bank sloped down toward
    /// the water, the trunk stopped in mid air and the tree looked like it was
    /// floating. Now the ground is the single source of truth: a tree asks the
    /// bank where the ground is under it and plants its trunk there, so a tree
    /// placed anywhere along the bank stands on it whatever slope it has.
    /// </summary>
    private sealed class Bank
    {
        private readonly PointF[] _edge;
        private readonly int _h;

        public Bank(PointF[] edge, int h) { _edge = edge; _h = h; }

        /// <summary>The top edge, left to right.</summary>
        public PointF[] Edge => _edge;

        /// <summary>The whole shape to fill: the top edge, then down to the bottom of the screen and back.</summary>
        public PointF[] Polygon() =>
            [.. _edge, new PointF(_edge[^1].X, _h + 2), new PointF(_edge[0].X, _h + 2)];

        /// <summary>
        /// Ground height at x, found by walking along the edge to the two
        /// points that straddle x and blending between them ("linear
        /// interpolation": if x is 30% of the way from point A to point B, the
        /// answer is 30% of the way from A's height to B's height).
        /// </summary>
        public float YAt(float x)
        {
            if (x <= _edge[0].X) return _edge[0].Y;
            for (int i = 1; i < _edge.Length; i++)
            {
                if (x > _edge[i].X) continue;
                float t = (x - _edge[i - 1].X) / (_edge[i].X - _edge[i - 1].X);
                return _edge[i - 1].Y + (_edge[i].Y - _edge[i - 1].Y) * t;
            }
            return _edge[^1].Y;
        }
    }

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

    /// <summary>
    /// Paints the two banks. Each is a gradient (lit along its top, darker
    /// toward the bottom of the screen) with a thin lighter rim along the top
    /// edge, so it reads as a rounded bank rather than a flat cutout.
    /// </summary>
    private static void PaintGround(Graphics g, int h, Bank left, Bank right)
    {
        Fill(left, Color.FromArgb(74, 86, 70), Color.FromArgb(40, 46, 42), Color.FromArgb(120, 104, 122, 92));
        Fill(right, Color.FromArgb(134, 146, 88), Color.FromArgb(82, 92, 56), Color.FromArgb(140, 168, 182, 112));

        void Fill(Bank bank, Color top, Color bottom, Color rim)
        {
            float edgeTop = bank.Edge.Min(p => p.Y);
            using (var brush = new LinearGradientBrush(new PointF(0, edgeTop - 1), new PointF(0, h + 1), top, bottom))
                g.FillPolygon(brush, bank.Polygon());
            using var pen = new Pen(rim, Math.Max(1.5f, h * 0.004f)) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, bank.Edge);
        }
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
        Pine(g, w * 0.21f, pineFoot - u * 0.37f, pineFoot, u, rng);

        // Trunks first, then all the canopies, so a canopy can hide the top of
        // its neighbor's trunk but a trunk is never painted over blossoms.
        foreach (var t in trees)
            Trunk(g, w * t.X, left.YAt(w * t.X) + u * 0.03f, left.YAt(w * t.X) - u * t.Lift, u * t.R, rng);
        foreach (var t in trees)
            Canopy(g, w * t.X, left.YAt(w * t.X) - u * t.Lift, u * t.R, u, rng);

        // The lone tree on the right hill. It stands inward from the edge so it
        // does not run into the branch that reaches in from the right.
        float rx = w * 0.87f, ry = right.YAt(rx) - u * 0.165f;
        Trunk(g, rx, right.YAt(rx) + u * 0.03f, ry, u * 0.14f, rng);
        Canopy(g, rx, ry, u * 0.14f, u, rng);
    }

    /// <summary>
    /// A trunk that forks: one thick stem from the ground to a little more than
    /// halfway up, then two thinner limbs that lean apart and disappear into the
    /// canopy (the blossoms are painted afterward and cover their ends). The
    /// whole thing leans slightly, and the lean is random, so the grove is not
    /// a row of identical posts. Thickness scales with the canopy's size (r).
    /// </summary>
    private static void Trunk(Graphics g, float x, float baseY, float canopyY, float r, Random rng)
    {
        float lean = r * 0.15f * ((float)rng.NextDouble() * 2 - 1);
        var mid = new PointF(x + lean, baseY - (baseY - canopyY) * 0.6f);
        float thick = Math.Max(2f, r * 0.11f);
        Color bark = Color.FromArgb(52, 38, 40);

        using (var stem = new Pen(bark, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(stem, new PointF(x, baseY), mid);
        using var limb = new Pen(bark, thick * 0.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(limb, mid, new PointF(mid.X + lean - r * 0.25f, canopyY + r * 0.05f));
        g.DrawLine(limb, mid, new PointF(mid.X + lean + r * 0.30f, canopyY - r * 0.05f));
    }

    /// <summary>
    /// A cloud of blossoms, built from "lumps" the way a child draws a cloud:
    /// several overlapping round bunches. Each lump gets a soft pink body, then
    /// hundreds of tiny dots of light and deep pink scattered over it and a bit
    /// past its edge, so the outline comes out bumpy and fluffy instead of a
    /// smooth circle. Dots in the upper left come out lighter, as if the sun
    /// (on the left) catches them.
    /// </summary>
    private static void Canopy(Graphics g, float cx, float cy, float r, float u, Random rng)
    {
        int lumpCount = 7 + rng.Next(4);
        var lumps = new (float X, float Y, float R)[lumpCount];
        for (int i = 0; i < lumpCount; i++)
        {
            // Random point inside an ellipse: random angle, random distance.
            // Square root on the distance keeps points evenly spread instead of
            // bunched in the middle.
            double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble()) * 0.6;
            lumps[i] = (cx + (float)(Math.Cos(ang) * dist) * r * 1.2f,   // wider than tall
                        cy + (float)(Math.Sin(ang) * dist) * r * 0.6f,
                        r * (0.28f + 0.14f * (float)rng.NextDouble()));
        }

        using (var body = new SolidBrush(Color.FromArgb(240, 210, 134, 166)))
            foreach (var l in lumps)
                g.FillEllipse(body, l.X - l.R * 0.9f, l.Y - l.R * 0.9f, l.R * 1.8f, l.R * 1.8f);

        int dots = Math.Min(1800, (int)(r * r / (u * 0.0045f * u * 0.0045f) * 1.5f));
        for (int i = 0; i < dots; i++)
        {
            var l = lumps[rng.Next(lumpCount)];
            double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble()) * 1.05;
            float x = l.X + (float)(Math.Cos(ang) * dist) * l.R;
            float y = l.Y + (float)(Math.Sin(ang) * dist) * l.R;
            float light = Math.Clamp(0.5f - (x - cx) / r * 0.3f - (y - cy) / r * 0.4f + ((float)rng.NextDouble() - 0.5f) * 0.6f, 0, 1);
            Color c = Mix(Color.FromArgb(200, 104, 146), Color.FromArgb(255, 238, 245), light);
            float dr = u * (0.003f + 0.004f * (float)rng.NextDouble());
            using var b = new SolidBrush(Color.FromArgb(230, c));
            g.FillEllipse(b, x - dr, y - dr, dr * 2, dr * 2);
        }
    }

    /// <summary>
    /// A tall dark pine, the classic cone shape: narrow at the tip, wide at the
    /// foot. Two passes:
    ///
    ///   1. The SILHOUETTE: one solid dark-green shape, traced down the left
    ///      side and back up the right. Its edge is jagged because each row of
    ///      boughs sticks out a slightly different amount, and each row's tips
    ///      hang a little lower than its middle, the way real boughs droop.
    ///      Painting this first guarantees the tree is one solid shape, never a
    ///      stack of separate pancakes with sky showing between them.
    ///   2. The TEXTURE: small ovals scattered over the silhouette in lighter
    ///      and darker greens, which gives the surface the look of clumps of
    ///      needles. The right side comes out darker (the sun is on the left).
    ///
    /// The trunk runs all the way to the ground, which is passed in, so the
    /// pine stands on the bank like everything else.
    /// </summary>
    private static void Pine(Graphics g, float x, float top, float ground, float u, Random rng)
    {
        using (var trunk = new Pen(Color.FromArgb(48, 38, 34), u * 0.007f))
            g.DrawLine(trunk, x, ground, x, top + u * 0.02f);

        float bottom = ground - u * 0.03f;
        float height = bottom - top;
        float HalfWidthAt(float y) => u * (0.006f + 0.070f * (y - top) / height);   // the cone: wider lower down

        // Pass 1: the silhouette. Left edge going down, then right edge coming back up.
        var leftEdge = new List<PointF> { new(x, top) };
        var rightEdge = new List<PointF>();
        float rowStep = u * 0.02f;
        for (float y = top + rowStep; y <= bottom; y += rowStep)
        {
            float hw = HalfWidthAt(y);
            float droop = hw * 0.2f;
            leftEdge.Add(new PointF(x - hw * (0.85f + 0.3f * (float)rng.NextDouble()), y + droop));
            leftEdge.Add(new PointF(x - hw * 0.45f, y + droop * 0.4f));                // the notch between one row and the next
            rightEdge.Add(new PointF(x + hw * (0.85f + 0.3f * (float)rng.NextDouble()), y + droop));
            rightEdge.Add(new PointF(x + hw * 0.45f, y + droop * 0.4f));
        }
        rightEdge.Reverse();
        using (var body = new SolidBrush(Color.FromArgb(30, 60, 44)))
            g.FillPolygon(body, [.. leftEdge, .. rightEdge]);

        // Pass 2: the texture of needle clumps.
        Color dark = Color.FromArgb(22, 48, 36), lit = Color.FromArgb(70, 112, 76);
        for (float y = top + u * 0.015f; y < bottom; y += u * 0.014f)
        {
            float hw = HalfWidthAt(y);
            int puffs = 2 + (int)(hw / (u * 0.008f));
            for (int k = 0; k < puffs; k++)
            {
                float f = (k + (float)rng.NextDouble()) / puffs * 2 - 1;          // -1 (left tip) ... +1 (right tip), evenly spread
                float px = x + f * hw;
                float py = y + MathF.Abs(f) * hw * 0.2f;                           // tips hang lower, matching the silhouette
                float pw = u * (0.006f + 0.007f * (float)rng.NextDouble());
                float ph = pw * 0.6f;
                float light = Math.Clamp(0.5f - f * 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.5f, 0, 1);
                using var needles = new SolidBrush(Mix(dark, lit, light));
                g.FillEllipse(needles, px - pw, py - ph, pw * 2, ph * 2);
            }
        }
    }

    // ================================================================ branches

    /// <summary>
    /// Cherry branches reaching in from the top corners, covered in blossom.
    /// Returns the blossom cluster positions so petals can fall from them.
    ///
    /// Branches are grown, not drawn by hand. Grow() walks forward in small
    /// steps, wobbling a little and sagging downward under its own weight, and
    /// now and then sprouts a thinner side branch that does the same thing.
    /// A rule that calls itself like this is called "recursion", and it is how
    /// most computer-drawn plants are made: a few simple rules, repeated,
    /// produce something that looks organic.
    /// </summary>
    private static List<PointF> PaintBranches(Graphics g, int w, int h, float u, Random rng)
    {
        var wood = new List<(PointF A, PointF B, float Width)>();
        var flowers = new List<(PointF C, float R, Color Petal, float Turn)>();
        var spots = new List<PointF>();

        void Cluster(PointF at, float spread)
        {
            spots.Add(at);
            int n = 3 + rng.Next(5);
            for (int i = 0; i < n; i++)
            {
                var c = new PointF(at.X + spread * ((float)rng.NextDouble() * 2 - 1),
                                   at.Y + spread * ((float)rng.NextDouble() * 2 - 1));
                float r = u * (0.008f + 0.005f * (float)rng.NextDouble());
                Color petal = Mix(Color.FromArgb(255, 246, 249), Color.FromArgb(244, 162, 192), (float)Math.Pow(rng.NextDouble(), 0.8));
                flowers.Add((c, r, petal, (float)(rng.NextDouble() * Math.PI * 2)));
            }
        }

        void Grow(PointF p, float angle, float length, float thick, int depth)
        {
            const int steps = 8;
            float stepLen = length / steps;
            for (int i = 0; i < steps; i++)
            {
                angle += ((float)rng.NextDouble() - 0.5f) * 0.35f;       // wobble
                angle += (MathF.PI / 2 - angle) * 0.03f;                  // sag toward "straight down" (pi/2 in screen coordinates)
                var next = new PointF(p.X + MathF.Cos(angle) * stepLen, p.Y + MathF.Sin(angle) * stepLen);
                wood.Add((p, next, thick));
                thick *= 0.88f;                                           // taper

                if (depth > 0 && rng.NextDouble() < 0.45)                 // sprout a side branch
                {
                    float turn = (0.4f + 0.5f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? -1 : 1);
                    Grow(next, angle + turn, length * 0.5f * (1 - i / (float)steps * 0.4f), thick * 0.75f, depth - 1);
                }
                if (thick < u * 0.012f && rng.NextDouble() < 0.5)         // thin enough to bloom
                    Cluster(next, u * 0.02f);
                p = next;
            }
            Cluster(p, u * 0.024f);                                       // every tip ends in a big cluster
        }

        // Angles are in radians, measured clockwise from "pointing right"
        // (because screen y grows downward): 0 = right, pi/2 = down, pi = left.
        // Start points are positions (w, h); lengths and thicknesses are sizes (u).
        Grow(new PointF(-w * 0.01f, h * 0.04f), 0.25f, u * 0.55f, u * 0.020f, 3);        // top left, reaching right
        Grow(new PointF(w * 0.12f, -h * 0.01f), 1.15f, u * 0.30f, u * 0.012f, 2);        // top left, hanging down
        Grow(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.35f, u * 0.60f, u * 0.024f, 3); // top right, reaching left
        Grow(new PointF(w * 1.01f, h * 0.30f), MathF.PI - 0.12f, u * 0.45f, u * 0.018f, 3); // right edge, lower
        Grow(new PointF(w * 0.74f, -h * 0.01f), MathF.PI / 2 + 0.35f, u * 0.32f, u * 0.012f, 2); // top, hanging down

        // Wood first, then all the flowers, so no branch is painted over a blossom.
        foreach (var (a, b, width) in wood)
        {
            using var pen = new Pen(Color.FromArgb(58, 38, 40), Math.Max(1f, width)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }
        foreach (var (c, r, petal, turn) in flowers)
            Flower(g, c, r, petal, turn);

        // Only clusters actually on screen and in the upper part are good drop points.
        return spots.Where(s => s.X >= 0 && s.X < w && s.Y >= 0 && s.Y < h * 0.7f).ToList();
    }

    /// <summary>One cherry blossom: five round petals in a ring around a deep pink center.</summary>
    private static void Flower(Graphics g, PointF c, float r, Color petal, float turn)
    {
        using var petalBrush = new SolidBrush(Color.FromArgb(245, petal));
        float pr = r * 0.5f;
        for (int k = 0; k < 5; k++)
        {
            float a = turn + k * MathF.Tau / 5;   // Tau = 2 * pi = one full turn; five petals, a fifth of a turn apart
            float px = c.X + MathF.Cos(a) * r * 0.5f, py = c.Y + MathF.Sin(a) * r * 0.5f;
            g.FillEllipse(petalBrush, px - pr, py - pr, pr * 2, pr * 2);
        }
        using var center = new SolidBrush(Color.FromArgb(214, 92, 134));
        float cr = r * 0.17f;
        g.FillEllipse(center, c.X - cr, c.Y - cr, cr * 2, cr * 2);
    }

    // ================================================================ helpers

    /// <summary>Blend two colors: t = 0 gives a, t = 1 gives b.</summary>
    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>Copy the finished Bitmap into a plain pixel array (the same 0x00RRGGBB layout as a FrameBuffer).</summary>
    private static uint[] ToPixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new int[bmp.Width * bmp.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);   // 32-bit rows have no padding, so one copy does it
            var pixels = new uint[raw.Length];
            Buffer.BlockCopy(raw, 0, pixels, 0, raw.Length * 4);
            return pixels;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
