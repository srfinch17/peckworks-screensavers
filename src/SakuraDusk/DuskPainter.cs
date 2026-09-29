using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraDusk;

/// <summary>The finished backdrop: a still picture plus a few facts the animation needs.</summary>
internal sealed class DuskScenery
{
    public required uint[] Pixels { get; init; }             // the painted picture, same layout as a FrameBuffer
    public required List<PointF> BlossomSpots { get; init; } // clusters on the branches that petals can drop from
    public required RectangleF SunPath { get; init; }        // the patch of water lit by the setting sun (glints live here)
}

/// <summary>
/// Paints the backdrop: a sunset sky with a low sun, a few birds, three
/// layers of hazy hills with a pagoda on the nearest one, a still pond that
/// reflects the sun, two banks with cherry trees, an arched wooden footbridge,
/// a stone lantern with a glowing window, and blossom branches at the top
/// corners.
///
/// Like Sakura's painter, this runs ONCE at startup (the scenery never moves)
/// and paints back to front: whatever is farthest away goes down first, and
/// each nearer thing simply covers what is behind it. The pieces that every
/// sakura scene shares (banks, trunks, blossoms, branches) come from the
/// engine's Brushwork helpers, so the two screensavers have the same hand.
///
/// Positions are fractions of the width (w) or height (h). Sizes are
/// fractions of "u": the height on a normal wide screen, or less on a tall
/// one, so trees and the bridge do not swallow a portrait monitor.
/// </summary>
internal static class DuskPainter
{
    public static DuskScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float horizon = h * 0.70f;                       // where the water meets the hills
        float u = Math.Min(h, w * 9f / 16f);
        var sun = new Sun(w * 0.60f, h * 0.50f, u * 0.09f);

        PaintSky(g, w, horizon, sun, u);
        PaintBirds(g, w, h, u, rng);
        var hills = new Hills(w, h, horizon, u, rng);
        hills.PaintFarLayers(g);
        PaintWaterBase(g, w, h, horizon);

        // The nearest hills and the pagoda, painted twice: once upside down and
        // faint into the water (then washed back toward the water color), and
        // once for real.
        var (left, right) = MakeGround(w, h);
        var bridge = new Bridge(new PointF(w * 0.30f, left.YAt(w * 0.30f)), new PointF(w * 0.69f, right.YAt(w * 0.69f)), h, u);
        PaintReflected(g, w, h, horizon, () => hills.PaintNearLayer(g));
        WashReflection(g, w, h, horizon);
        PaintReflected(g, w, h, bridge.WaterLine, () => bridge.Paint(g, 0.35f));
        PaintRipples(g, w, h, horizon, sun, u, rng);
        hills.PaintNearLayer(g);

        left.Paint(g, Color.FromArgb(70, 52, 84), Color.FromArgb(30, 22, 42), Color.FromArgb(120, 150, 104, 96));
        right.Paint(g, Color.FromArgb(70, 52, 84), Color.FromArgb(30, 22, 42), Color.FromArgb(120, 150, 104, 96));
        bridge.Paint(g, 1f);
        PaintLantern(g, w * 0.26f, left.YAt(w * 0.26f) + u * 0.01f, u);
        PaintTrees(g, w, u, left, right, rng);
        List<PointF> spots = PaintBranches(g, w, h, u, rng);

        // The sun's reflection: a patch just below the horizon, under the sun.
        var sunPath = new RectangleF(sun.X - sun.R * 1.2f, horizon + h * 0.01f, sun.R * 2.4f, bridge.Top - horizon - h * 0.02f);

        return new DuskScenery { Pixels = Brushwork.ToPixels(bmp), BlossomSpots = spots, SunPath = sunPath };
    }

    private sealed record Sun(float X, float Y, float R);

    // ================================================================ sky

    /// <summary>
    /// The sunset: deep violet overhead, through rose and coral, to a warm
    /// yellow at the horizon, with a soft glow around the sun and the sun
    /// itself as a pale disc. The glow is a "path gradient": a color that is
    /// strongest at the center of a shape and fades to nothing at its edge.
    /// </summary>
    private static void PaintSky(Graphics g, int w, float horizon, Sun sun, float u)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(46, 28, 84), Color.FromArgb(146, 70, 112), Color.FromArgb(236, 132, 98), Color.FromArgb(255, 216, 148)],
                Positions = [0f, 0.38f, 0.72f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        float glowR = sun.R * 4.5f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(sun.X - glowR, sun.Y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(170, 255, 226, 160),
                SurroundColors = [Color.FromArgb(0, 255, 200, 140)],
            };
            g.FillPath(glow, glowPath);
        }

        // A soft rim, then the disc.
        using (var rim = new SolidBrush(Color.FromArgb(90, 255, 240, 200)))
            g.FillEllipse(rim, sun.X - sun.R * 1.12f, sun.Y - sun.R * 1.12f, sun.R * 2.24f, sun.R * 2.24f);
        using (var disc = new SolidBrush(Color.FromArgb(255, 244, 206)))
            g.FillEllipse(disc, sun.X - sun.R, sun.Y - sun.R, sun.R * 2, sun.R * 2);
    }

    /// <summary>
    /// A loose flock of distant birds: each one is two little curved wing
    /// strokes meeting in the middle, the shorthand every illustrator uses.
    /// </summary>
    private static void PaintBirds(Graphics g, int w, int h, float u, Random rng)
    {
        using var pen = new Pen(Color.FromArgb(220, 44, 22, 56), Math.Max(1f, u * 0.0025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int count = 7;
        float cx = w * 0.33f, cy = h * 0.40f;
        for (int i = 0; i < count; i++)
        {
            float x = cx + u * 0.22f * ((float)rng.NextDouble() - 0.5f);
            float y = cy + u * 0.10f * ((float)rng.NextDouble() - 0.5f);
            float s = u * (0.008f + 0.008f * (float)rng.NextDouble());   // wingspan, half
            float lift = s * (0.3f + 0.4f * (float)rng.NextDouble());     // how high the wing tips are
            g.DrawBezier(pen, new PointF(x - s, y - lift), new PointF(x - s * 0.5f, y - lift * 0.2f), new PointF(x - s * 0.3f, y), new PointF(x, y));
            g.DrawBezier(pen, new PointF(x, y), new PointF(x + s * 0.3f, y), new PointF(x + s * 0.5f, y - lift * 0.2f), new PointF(x + s, y - lift));
        }
    }

    // ================================================================ hills

    /// <summary>
    /// Three ranges of hills, far to near, each one a wavy line made of a few
    /// sine waves added together (different wavelengths, random phases). Far
    /// ranges are paler and pinker, as if seen through the evening haze;
    /// the near range is dark plum. The pagoda stands on the near range,
    /// to the right of the sun.
    /// </summary>
    private sealed class Hills
    {
        private readonly PointF[][] _layers = new PointF[3][];
        private readonly float _pagodaX, _pagodaBase;
        private readonly float _u;

        public Hills(int w, int h, float horizon, float u, Random rng)
        {
            _u = u;
            // Each layer: base height above the horizon, then three wave amplitudes (all in u).
            (float Base, float A1, float A2, float A3)[] shape =
            [
                (0.050f, 0.045f, 0.015f, 0.055f),  // far
                (0.030f, 0.032f, 0.014f, 0.035f),  // middle
                (0.012f, 0.024f, 0.012f, 0.026f),  // near
            ];
            for (int k = 0; k < 3; k++)
            {
                float p1 = (float)rng.NextDouble() * 6, p2 = (float)rng.NextDouble() * 6, p3 = (float)rng.NextDouble() * 6;
                var pts = new List<PointF> { new(-2, horizon + 2) };
                for (float x = -2; x <= w + 2; x += Math.Max(2, u / 200f))
                {
                    float t = x / u;
                    float rise = shape[k].Base
                               + shape[k].A1 * MathF.Sin(t * 2.6f + p1)
                               + shape[k].A2 * MathF.Sin(t * 6.1f + p2)
                               + shape[k].A3 * MathF.Abs(MathF.Sin(t * 1.4f + p3));   // Abs makes rounded peaks
                    pts.Add(new PointF(x, horizon - u * rise));
                }
                pts.Add(new PointF(w + 2, horizon + 2));
                _layers[k] = [.. pts];
            }
            _pagodaX = w * 0.72f;
            _pagodaBase = HeightAt(2, _pagodaX) + u * 0.005f;
        }

        /// <summary>The top of layer k at x (nearest point on its wavy edge).</summary>
        private float HeightAt(int k, float x)
        {
            PointF best = _layers[k][1];
            foreach (var p in _layers[k])
                if (MathF.Abs(p.X - x) < MathF.Abs(best.X - x)) best = p;
            return best.Y;
        }

        public void PaintFarLayers(Graphics g)
        {
            using (var far = new SolidBrush(Color.FromArgb(216, 134, 140)))
                g.FillPolygon(far, _layers[0]);
            using (var mid = new SolidBrush(Color.FromArgb(150, 84, 124)))
                g.FillPolygon(mid, _layers[1]);
        }

        public void PaintNearLayer(Graphics g)
        {
            using (var near = new SolidBrush(Color.FromArgb(72, 40, 84)))
                g.FillPolygon(near, _layers[2]);
            PaintPagoda(g, _pagodaX, _pagodaBase, _u);
        }
    }

    /// <summary>
    /// A five-story pagoda as a flat silhouette. Each story is a roof (a wide,
    /// low trapezoid whose corners kick up a little at the eaves, the classic
    /// East Asian roof line) sitting on a narrower body. Stories shrink as
    /// they go up, and a thin spire with a few rings finishes the top.
    /// </summary>
    private static void PaintPagoda(Graphics g, float x, float baseY, float u)
    {
        using var ink = new SolidBrush(Color.FromArgb(50, 26, 60));
        using var pen = new Pen(Color.FromArgb(50, 26, 60), Math.Max(1f, u * 0.003f));

        const int stories = 5;
        float roofH = u * 0.013f, bodyH = u * 0.014f;
        float y = baseY;
        g.FillRectangle(ink, x - u * 0.036f, y - u * 0.005f, u * 0.072f, u * 0.005f);   // the plinth
        y -= u * 0.005f;
        for (int i = 0; i < stories; i++)
        {
            float wide = u * 0.066f * (1 - 0.13f * i);      // this story's roof width
            float body = wide * 0.5f;
            g.FillRectangle(ink, x - body / 2, y - bodyH, body, bodyH);
            y -= bodyH;
            PointF[] roof =
            [
                new(x - wide / 2, y - roofH * 0.25f),        // left eave, kicked up
                new(x - wide * 0.30f, y - roofH),
                new(x + wide * 0.30f, y - roofH),
                new(x + wide / 2, y - roofH * 0.25f),        // right eave
                new(x + wide * 0.42f, y + roofH * 0.15f),
                new(x - wide * 0.42f, y + roofH * 0.15f),
            ];
            g.FillPolygon(ink, roof);
            y -= roofH;
        }
        // The spire.
        float spireH = u * 0.03f;
        g.DrawLine(pen, x, y, x, y - spireH);
        for (int i = 1; i <= 3; i++)
        {
            float ry = y - spireH * i / 4f, rr = u * 0.0035f * (1 - 0.2f * i);
            g.FillEllipse(ink, x - rr, ry - rr * 0.5f, rr * 2, rr);
        }
    }

    // ================================================================ water

    /// <summary>
    /// The pond: warm near the far shore where it mirrors the sky, cooling to
    /// deep violet near us. Under the sun, hundreds of short bright streaks
    /// make the sun's reflection, widening toward the viewer the way a real
    /// sun path does. Elsewhere, faint light and dark ripples.
    /// </summary>
    private static void PaintWaterBase(Graphics g, int w, int h, float horizon)
    {
        using var water = new LinearGradientBrush(new PointF(0, horizon - 1), new PointF(0, h + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(246, 194, 138), Color.FromArgb(184, 116, 128), Color.FromArgb(62, 36, 78)],
                Positions = [0f, 0.35f, 1f],
            },
        };
        g.FillRectangle(water, 0, horizon, w, h - horizon);
    }

    /// <summary>The sun path and the ripples, painted after the reflections so the wash does not dull them.</summary>
    private static void PaintRipples(Graphics g, int w, int h, float horizon, Sun sun, float u, Random rng)
    {
        // The sun path.
        float depthOfLake = h - horizon;
        for (int i = 0; i < 700; i++)
        {
            float depth = (float)Math.Pow(rng.NextDouble(), 1.4);                       // most streaks near the far edge
            float y = horizon + depth * depthOfLake * 0.8f;
            float spread = sun.R * (0.5f + 2.2f * depth);                                // widens toward us
            float across = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1) * spread;  // bunched under the sun
            float len = u * (0.006f + 0.04f * (float)rng.NextDouble()) * (0.4f + depth);
            int alpha = (int)(190 * (1 - depth * 0.7f) * (1 - MathF.Abs(across) / spread * 0.6f));
            using var pen = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), 255, 230, 176), Math.Max(1f, u / 900f));
            g.DrawLine(pen, sun.X + across - len / 2, y, sun.X + across + len / 2, y);
        }

        // Ripples everywhere.
        using var light = new Pen(Color.FromArgb(22, 255, 224, 196), Math.Max(1f, u / 700f));
        using var dark = new Pen(Color.FromArgb(26, 30, 14, 50), Math.Max(1f, u / 700f));
        for (int i = 0; i < 500; i++)
        {
            float y = horizon + (float)Math.Pow(rng.NextDouble(), 0.8) * depthOfLake;
            float depth = (y - horizon) / depthOfLake;
            float len = w * (0.01f + 0.08f * (float)rng.NextDouble()) * (0.4f + depth);
            float x = (float)rng.NextDouble() * w;
            g.DrawLine(i % 2 == 0 ? light : dark, x, y, x + len, y);
        }
    }

    /// <summary>
    /// Paints something upside down and faint into the water, as its
    /// reflection. The "transform" flips every point across the water line:
    /// Matrix(1, 0, 0, -1, 0, 2 * line) means "keep x, replace y with
    /// (2 * line - y)". The clip keeps the reflection in the upper part of the
    /// water, where a still pond really shows one, and Save/Restore hands the
    /// canvas back exactly as it was.
    /// </summary>
    private static void PaintReflected(Graphics g, int w, int h, float waterLine, Action paint)
    {
        GraphicsState before = g.Save();
        g.SetClip(new RectangleF(0, waterLine, w, (h - waterLine) * 0.6f), CombineMode.Intersect);
        g.Transform = new Matrix(1, 0, 0, -1, 0, 2 * waterLine);
        paint();
        g.Restore(before);
    }

    /// <summary>
    /// Washes the reflected hills back toward the water color, from a light
    /// wash near the far shore to nearly solid where the reflection ends, so
    /// the reflection reads as a reflection and not a second copy of the hills.
    /// </summary>
    private static void WashReflection(Graphics g, int w, int h, float horizon)
    {
        float depth = (h - horizon) * 0.6f;
        using var wash = new LinearGradientBrush(new PointF(0, horizon - 1), new PointF(0, horizon + depth + 1),
            Color.FromArgb(185, 226, 156, 134), Color.FromArgb(242, 124, 72, 104));
        g.FillRectangle(wash, 0, horizon, w, depth);
    }

    // ================================================================ foreground

    /// <summary>The two banks of the pond: one at the lower left, one at the lower right, with open water between them.</summary>
    private static (Bank Left, Bank Right) MakeGround(int w, int h)
    {
        var left = new Bank([
            new PointF(-2, h * 0.79f), new PointF(w * 0.10f, h * 0.80f), new PointF(w * 0.22f, h * 0.83f),
            new PointF(w * 0.32f, h * 0.87f), new PointF(w * 0.40f, h + 2)], h);
        var right = new Bank([
            new PointF(w * 0.58f, h + 2), new PointF(w * 0.66f, h * 0.88f), new PointF(w * 0.78f, h * 0.84f),
            new PointF(w * 0.90f, h * 0.815f), new PointF(w + 2, h * 0.80f)], h);
        return (left, right);
    }

    /// <summary>
    /// An arched wooden footbridge from one bank to the other. The deck is a
    /// curve that bows upward (a Bezier with its steering point above the
    /// middle), the underside is a lower curve, and the space between is
    /// filled in as the arch. On top: a railing of short posts and a top
    /// rail following the same curve, with a thin warm highlight where the
    /// last light catches the rail.
    /// </summary>
    private sealed class Bridge
    {
        private readonly PointF _a, _b, _ctrlDeck, _ctrlRail, _ctrlUnder;
        private readonly float _u;

        /// <summary>The height of the deck at its peak (nothing on the water should be drawn above it under the bridge).</summary>
        public float Top { get; }

        /// <summary>Where the bridge's reflection folds: the water at its feet.</summary>
        public float WaterLine { get; }

        public Bridge(PointF a, PointF b, int h, float u)
        {
            _a = a; _b = b; _u = u;
            float midX = (a.X + b.X) / 2, feet = Math.Max(a.Y, b.Y);
            _ctrlDeck = new PointF(midX, feet - u * 0.20f);
            _ctrlRail = new PointF(midX, feet - u * 0.20f - u * 0.045f);
            _ctrlUnder = new PointF(midX, feet - u * 0.12f);
            // A quadratic Bezier passes through 1/4 A + 1/2 C + 1/4 B at its middle.
            Top = 0.25f * a.Y + 0.5f * _ctrlDeck.Y + 0.25f * b.Y;
            WaterLine = feet + u * 0.01f;
        }

        /// <summary>A point on the quadratic curve from p0 through control c to p1, at t (0 to 1).</summary>
        private static PointF On(PointF p0, PointF c, PointF p1, float t)
        {
            float s = 1 - t;
            return new PointF(s * s * p0.X + 2 * s * t * c.X + t * t * p1.X,
                              s * s * p0.Y + 2 * s * t * c.Y + t * t * p1.Y);
        }

        private static PointF[] Curve(PointF p0, PointF c, PointF p1, int n = 40)
        {
            var pts = new PointF[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = On(p0, c, p1, i / (float)n);
            return pts;
        }

        public void Paint(Graphics g, float opacity)
        {
            int A(int a) => (int)(a * opacity);
            Color wood = Color.FromArgb(A(255), 92, 38, 46), deck = Color.FromArgb(A(255), 122, 54, 56);
            Color rail = Color.FromArgb(A(255), 132, 58, 58), gleam = Color.FromArgb(A(140), 236, 150, 120);

            // The arch: deck curve across, underside curve back.
            PointF[] top = Curve(_a, _ctrlDeck, _b);
            PointF[] under = Curve(new PointF(_b.X, _b.Y + _u * 0.02f), _ctrlUnder, new PointF(_a.X, _a.Y + _u * 0.02f));
            using (var body = new SolidBrush(wood))
                g.FillPolygon(body, [.. top, .. under]);
            using (var edge = new Pen(deck, Math.Max(1.5f, _u * 0.012f)))
                g.DrawLines(edge, top);

            // Railing: posts every so often, then the top rail.
            using var post = new Pen(rail, Math.Max(1f, _u * 0.006f));
            const int posts = 14;
            for (int i = 0; i <= posts; i++)
            {
                float t = i / (float)posts;
                PointF foot = On(_a, _ctrlDeck, _b, t), head = On(_a, _ctrlRail, _b, t);
                g.DrawLine(post, foot, head);
            }
            using (var railPen = new Pen(rail, Math.Max(1.5f, _u * 0.009f)) { LineJoin = LineJoin.Round })
                g.DrawLines(railPen, Curve(_a, _ctrlRail, _b));
            using (var gleamPen = new Pen(gleam, Math.Max(1f, _u * 0.003f)) { LineJoin = LineJoin.Round })
                g.DrawLines(gleamPen, Curve(new PointF(_a.X, _a.Y - _u * 0.004f), new PointF(_ctrlRail.X, _ctrlRail.Y - _u * 0.004f), new PointF(_b.X, _b.Y - _u * 0.004f)));
        }
    }

    /// <summary>
    /// A stone lantern (a "toro"): plinth, post, a light box with a warm
    /// glowing window, a roof with kicked-up corners, and a knob on top.
    /// The glow around the window is a path gradient, like the sun's.
    /// </summary>
    private static void PaintLantern(Graphics g, float x, float groundY, float u)
    {
        using var stone = new SolidBrush(Color.FromArgb(58, 44, 66));
        float plinthW = u * 0.034f, plinthH = u * 0.012f;
        float postW = u * 0.012f, postH = u * 0.032f;
        float boxW = u * 0.028f, boxH = u * 0.024f;
        float roofW = u * 0.044f, roofH = u * 0.014f;

        float y = groundY;
        g.FillRectangle(stone, x - plinthW / 2, y - plinthH, plinthW, plinthH);
        y -= plinthH;
        g.FillRectangle(stone, x - postW / 2, y - postH, postW, postH);
        y -= postH;

        // The glow goes under the box so the box's stone frame stays crisp.
        float glowR = u * 0.06f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - glowR, y - boxH / 2 - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(150, 255, 196, 120),
                SurroundColors = [Color.FromArgb(0, 255, 180, 100)],
            };
            g.FillPath(glow, glowPath);
        }
        g.FillRectangle(stone, x - boxW / 2, y - boxH, boxW, boxH);
        using (var window = new SolidBrush(Color.FromArgb(255, 214, 140)))
            g.FillRectangle(window, x - boxW * 0.28f, y - boxH * 0.82f, boxW * 0.56f, boxH * 0.62f);
        y -= boxH;

        PointF[] roof =
        [
            new(x - roofW / 2, y - roofH * 0.2f), new(x - roofW * 0.25f, y - roofH), new(x + roofW * 0.25f, y - roofH),
            new(x + roofW / 2, y - roofH * 0.2f), new(x + roofW * 0.4f, y + roofH * 0.1f), new(x - roofW * 0.4f, y + roofH * 0.1f),
        ];
        g.FillPolygon(stone, roof);
        y -= roofH;
        float knob = u * 0.006f;
        g.FillEllipse(stone, x - knob, y - knob * 1.6f, knob * 2, knob * 2);
    }

    /// <summary>
    /// Cherry trees on both banks, each standing on the ground under it (the
    /// bank answers "how high is the ground here?"). Trunks first, then all
    /// the canopies. The blossoms use a warmer, deeper palette than daylight
    /// Sakura, because the low sun is lighting them from behind and below.
    /// </summary>
    private static void PaintTrees(Graphics g, int w, float u, Bank left, Bank right, Random rng)
    {
        var dusk = new BlossomPalette(Color.FromArgb(240, 222, 140, 156), Color.FromArgb(182, 84, 118), Color.FromArgb(255, 216, 198));
        Color bark = Color.FromArgb(44, 26, 40);

        // (X as a fraction of the width, canopy center height above the ground in u, canopy size in u)
        (float X, float Lift, float R, Bank On)[] trees =
        [
            (0.02f, 0.13f, 0.13f, left), (0.12f, 0.11f, 0.115f, left), (0.20f, 0.10f, 0.10f, left),
            (0.80f, 0.10f, 0.10f, right), (0.89f, 0.13f, 0.13f, right), (0.99f, 0.11f, 0.115f, right),
        ];
        foreach (var t in trees)
            Brushwork.Trunk(g, w * t.X, t.On.YAt(w * t.X) + u * 0.03f, t.On.YAt(w * t.X) - u * t.Lift, u * t.R, rng, bark);
        foreach (var t in trees)
            Brushwork.Canopy(g, w * t.X, t.On.YAt(w * t.X) - u * t.Lift, u * t.R, u, rng, dusk);
    }

    /// <summary>Two blossom branches reaching in from the top corners. Returns the cluster spots petals can fall from.</summary>
    private static List<PointF> PaintBranches(Graphics g, int w, int h, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.05f), 0.30f, u * 0.50f, u * 0.020f, 3),           // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.03f), MathF.PI - 0.30f, u * 0.46f, u * 0.018f, 3), // top right, reaching left
        ];
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng,
            wood: Color.FromArgb(44, 26, 40),
            petalLight: Color.FromArgb(255, 228, 216),
            petalDeep: Color.FromArgb(236, 148, 170));
        return spots.Where(s => s.X >= 0 && s.X < w && s.Y >= 0 && s.Y < h * 0.6f).ToList();
    }
}
