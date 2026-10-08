using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core.Sakura;

namespace CabinByStream;

/// <summary>The finished backdrop: a still picture plus the facts the animation needs.</summary>
internal sealed class MeadowScenery
{
    public required uint[] Pixels { get; init; }        // the painted picture, same layout as a FrameBuffer
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float U { get; init; }              // the size unit: the height, or less on a tall screen. Size everything by this.
    public required float Horizon { get; init; }        // where the meadow meets the far forest
    public required StreamShape Stream { get; init; }   // where the water is: its middle and its width at any height
    public required bool[] OpenWater { get; init; }     // one true/false per pixel: true where the stream's water is still open (no stone, bank, flower or blade painted over it)
    public required PointF ChimneyTop { get; init; }    // where the smoke comes out
    public required PointF[] Windows { get; init; }     // the middle of each lit window
    public required float WindowSize { get; init; }     // a window's height, for sizing its glow
    public required PointF DoorLamp { get; init; }      // the little lantern beside the door
    public required List<TreeFacts> Trees { get; init; } // every tree, left to right
    public required PointF[] Stars { get; init; }       // the brighter stars, for twinkling
}

/// <summary>One tree: where its trunk meets the ground, where the trunk disappears into the leaves (a squirrel climbs between the two), and how big its crown is.</summary>
internal sealed record TreeFacts(PointF Foot, PointF ClimbTop, float R);

/// <summary>
/// The stream's shape, as two questions: "where is the middle of the water
/// at this height?" and "how wide is it there?". It starts as a thread at
/// the far edge of the meadow and winds toward the viewer, widening as it
/// comes (that is perspective: the same width of water looks wider when it
/// is nearer). Everything that lives on the water (fish, glints, the
/// fireflies' reflections) asks this instead of guessing.
/// </summary>
internal sealed class StreamShape
{
    private readonly float _w, _u, _startX, _endX, _p1, _p2;

    /// <summary>The height where the stream begins (far) and where it leaves the bottom of the picture (near).</summary>
    public float Top { get; }
    public float Bottom { get; }

    public StreamShape(int w, int h, float horizon, float u, Random rng)
    {
        _w = w; _u = u;
        Top = horizon + u * 0.05f;      // it comes out of the mist at the forest's feet, not out of the forest itself
        Bottom = h + u * 0.02f;
        _startX = 0.60f + 0.06f * ((float)rng.NextDouble() - 0.5f);   // as fractions of the width
        _endX = 0.52f + 0.14f * ((float)rng.NextDouble() - 0.5f);
        _p1 = (float)rng.NextDouble() * 6;
        _p2 = (float)rng.NextDouble() * 6;
    }

    /// <summary>How far down the stream a height is: 0 at the far end, 1 at the bottom edge.</summary>
    public float P(float y) => Math.Clamp((y - Top) / (Bottom - Top), 0f, 1f);

    /// <summary>
    /// The middle of the water at height y. A straight slant from where it
    /// starts to where it leaves, plus two gentle sine waves for the
    /// meanders. The waves are multiplied by p so the far end stays put and
    /// the bends grow as the stream comes nearer, as perspective would have it.
    /// </summary>
    public float CenterX(float y)
    {
        float p = P(y);
        return _w * (_startX + (_endX - _startX) * p)
             + _u * (0.09f * MathF.Sin(p * 4.2f + _p1) + 0.035f * MathF.Sin(p * 9.5f + _p2)) * p;
    }

    /// <summary>Half the water's width at height y: a thread far away, a brook near the bottom.</summary>
    public float HalfWidth(float y) => _u * (0.004f + 0.115f * MathF.Pow(P(y), 1.35f));

    /// <summary>The whole water surface as one shape: down the left edge, back up the right.</summary>
    public PointF[] Polygon()
    {
        var left = new List<PointF>();
        var right = new List<PointF>();
        float step = Math.Max(1f, _u / 90f);
        for (float y = Top; y <= Bottom; y += step)
        {
            float c = CenterX(y), hw = HalfWidth(y);
            left.Add(new PointF(c - hw, y));
            right.Add(new PointF(c + hw, y));
        }
        right.Reverse();
        return [.. left, .. right];
    }
}

/// <summary>
/// Paints the backdrop, once, at startup: a dusk sky with the last glow of
/// sunset low on the left and the first stars overhead, a far forest in two
/// hazy layers with mist lying at its feet, a flowery meadow, a stream that
/// winds toward the viewer, a small thatched cottage with lit windows and a
/// chimney, and big leafy trees for the squirrels.
///
/// Like every painter in this repo it works back to front: whatever is
/// farthest away goes down first, and each nearer thing simply covers what
/// is behind it. Two things make this one look more like a photograph than
/// the others:
///
///   - TEXTURE. The grass is not a flat gradient. After the base colours are
///     painted, every meadow pixel is nudged lighter or darker by a little
///     noise (small clumps near the horizon, bigger clumps near the bottom,
///     because near grass looks coarser), which reads as blades.
///   - LIGHT. One light source, the sunset glow on the left, decides
///     everything: the lit faces of the cottage, the warm rim on the tree
///     crowns, and the long soft shadows that all fall to the right. Far
///     things fade toward the violet of the haze. The corners are darkened a
///     little (a "vignette", as a camera lens does).
///
/// Positions are fractions of the width (w) or height (h). Sizes are
/// fractions of "u": the height on a normal wide screen, or less on a tall
/// one, so the cottage and the trees do not swallow a portrait monitor.
/// </summary>
internal static class MeadowPainter
{
    public static MeadowScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float u = Math.Min(h, w * 9f / 16f);
        // Where the meadow meets the forest. On a wide screen a little below
        // the middle. On a tall screen the meadow would run on for ever (the
        // cottage a speck in a field), so its depth is capped at about one u
        // and the sky takes the room above instead.
        float horizon = Math.Max(h * 0.46f, h - u * 1.0f);
        float meadow = h - horizon;                      // the meadow's depth; things stand at fractions of it
        float glowX = w * 0.14f;                         // where the sun went down: the light comes from here

        // ---- The paint order, back to front. Keep this list explicit. ----
        PointF[] stars = PaintSky(g, w, h, horizon, u, glowX, rng);
        PaintForest(g, w, horizon, u, rng);
        PaintMeadowBase(g, w, h, horizon);

        // The grass texture works on the pixels themselves, so the picture
        // goes out to a plain array, gets its noise, and comes back.
        g.Flush();
        uint[] px = Brushwork.ToPixels(bmp);
        TextureGrass(px, w, h, horizon, u);
        FromPixels(bmp, px);

        var stream = new StreamShape(w, h, horizon, u, rng);
        uint[] behindFish = PaintStream(g, bmp, stream, w, h, u, rng);
        PaintMist(g, w, horizon, u);

        // ---- The things that stand on the meadow ----
        // The cottage sits left of the stream; the trees frame the picture. A
        // tree whose foot is lower on the screen is nearer, so the things are
        // sorted by their feet and painted far to near, with the flowers
        // slotted in between at their own heights (a flower in front of a
        // trunk must be painted after the trunk).
        var cottageFoot = new PointF(w * 0.30f, horizon + meadow * 0.34f);
        // Keep the cottage clear of the water on every seed: if the stream's
        // left bank at the cottage's height comes too close, slide it left.
        float cottageRight = cottageFoot.X + u * 0.11f + u * 0.07f + u * 0.26f;   // half the front, the side, and room for the shadow
        float bankLeft = stream.CenterX(cottageFoot.Y) - stream.HalfWidth(cottageFoot.Y);
        if (cottageRight > bankLeft) cottageFoot.X -= cottageRight - bankLeft;

        // (X as a fraction of the width, foot as a fraction of the meadow's depth, crown size in u.)
        (float X, float Foot, float R)[] treeSpots =
        [
            (0.15f, 0.25f, 0.125f),    // a smaller one behind and left of the cottage
            (0.07f, 0.74f, 0.235f),    // the big one at the left edge
            (0.80f, 0.63f, 0.185f),    // the pair on the right that the squirrels run between
            (0.95f, 0.79f, 0.225f),
        ];
        var treeFacts = new List<(float X, TreeFacts Facts)>();
        var things = new List<(float Y, Action Paint)>();

        PaintShadows(g, cottageFoot, u, treeSpots.Select(t => (w * t.X, horizon + meadow * t.Foot, u * t.R)));

        var cottage = new Cottage(cottageFoot, u);
        things.Add((cottageFoot.Y, () => cottage.Paint(g, rng)));
        foreach (var t in treeSpots)
        {
            float footY = horizon + meadow * t.Foot;
            var tree = new Tree(new PointF(w * t.X, footY), u * t.R, u, rng);
            treeFacts.Add((tree.Facts.Foot.X, tree.Facts));
            things.Add((footY, () => tree.Paint(g)));
        }
        AddBushes(things, g, w, h, horizon, u, stream, cottage, rng);
        AddFlowers(things, g, w, h, horizon, u, stream, cottage, rng);
        foreach (var thing in things.OrderBy(t => t.Y)) thing.Paint();

        PaintGrassBlades(g, w, h, horizon, u, stream, cottage, rng);
        PaintForegroundGrass(g, w, h, u, rng);

        // Everything is painted. The water stencil compares the water as it
        // was before the stones and the banks with the finished picture: a
        // pixel that did not change is open water the fish can swim through.
        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);
        bool[] openWater = Brushwork.Unchanged(behindFish, pixels);
        Vignette(pixels, w, h);

        return new MeadowScenery
        {
            Pixels = pixels, Width = w, Height = h, U = u, Horizon = horizon,
            Stream = stream, OpenWater = openWater,
            ChimneyTop = cottage.ChimneyTop, Windows = cottage.Windows, WindowSize = cottage.WindowHeight, DoorLamp = cottage.DoorLamp,
            Trees = treeFacts.OrderBy(t => t.X).Select(t => t.Facts).ToList(),
            Stars = stars,
        };
    }

    // ================================================================ sky

    /// <summary>
    /// Dusk: a deep blue overhead, through violet and rose, to a band of
    /// amber at the horizon, with a soft glow where the sun went down, a few
    /// streaks of cloud lit from below, and the first stars. Returns the
    /// brighter stars so the scene can twinkle them.
    /// </summary>
    private static PointF[] PaintSky(Graphics g, int w, int h, float horizon, float u, float glowX, Random rng)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(12, 18, 52), Color.FromArgb(50, 44, 98), Color.FromArgb(132, 82, 112), Color.FromArgb(222, 142, 98), Color.FromArgb(252, 200, 136)],
                Positions = [0f, 0.36f, 0.64f, 0.86f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        // The afterglow: strongest at the horizon on the left, fading out in a wide oval.
        using (var path = new GraphicsPath())
        {
            float rx = u * 0.95f, ry = u * 0.34f;
            path.AddEllipse(glowX - rx, horizon + u * 0.03f - ry, rx * 2, ry * 2);
            using var glow = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(175, 255, 214, 150),
                SurroundColors = [Color.FromArgb(0, 255, 190, 130)],
                Blend = Peckworks.Screensavers.Core.Sprite.SoftFalloff,
            };
            g.FillPath(glow, path);
        }

        // Stars: many faint, a few bright, only where the sky is dark enough
        // to show them (the upper two thirds, thinning toward the glow).
        var bright = new List<PointF>();
        int count = (int)(220 * (w / (float)u) / (16f / 9f));
        for (int i = 0; i < count; i++)
        {
            float y = (float)Math.Pow(rng.NextDouble(), 1.6) * horizon * 0.72f;
            float x = (float)rng.NextDouble() * w;
            float dim = 1 - y / (horizon * 0.72f);                    // fainter lower down, where the sky is lighter
            int a = (int)(255 * dim * (0.25f + 0.75f * (float)rng.NextDouble()));
            float r = Math.Max(0.6f, u * 0.0009f * (0.6f + (float)rng.NextDouble()));
            using var star = new SolidBrush(Color.FromArgb(Math.Clamp(a, 0, 255), 255, 246, 228));
            g.FillEllipse(star, x - r, y - r, r * 2, r * 2);
            if (a > 200 && bright.Count < 48) bright.Add(new PointF(x, y));
        }

        // Clouds: long thin streaks. Low ones catch the afterglow on their
        // undersides (salmon); high ones are already in shadow (dusky violet).
        int clouds = 5 + rng.Next(4);
        for (int i = 0; i < clouds; i++)
        {
            float y = horizon * (0.30f + 0.62f * (float)rng.NextDouble());
            float low = y / horizon;                                          // 1 = at the horizon
            float cw = u * (0.25f + 0.55f * (float)rng.NextDouble()), ch = u * (0.012f + 0.03f * (float)rng.NextDouble()) * (0.5f + low);
            float x = (float)rng.NextDouble() * w;
            Color c = low > 0.62f ? Color.FromArgb(120, 250, 160, 125) : Color.FromArgb(110, 84, 66, 112);
            using var path = new GraphicsPath();
            path.AddEllipse(x - cw / 2, y - ch / 2, cw, ch);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = c,
                SurroundColors = [Color.FromArgb(0, c)],
                Blend = Peckworks.Screensavers.Core.Sprite.SoftFalloff,
            };
            g.FillPath(brush, path);
        }
        return bright.ToArray();
    }

    // ================================================================ forest

    /// <summary>
    /// The far forest, two layers deep, each a crowd of individual fir
    /// silhouettes standing shoulder to shoulder (tall ragged triangles),
    /// every one its own height, standing on ground that rolls gently. The far layer is pale and violet
    /// (seen through the evening haze) and slightly uneven in colour from
    /// tree to tree; the near one is dark teal. A soft band of haze is laid
    /// between them so the far layer melts into the sky at its feet.
    /// </summary>
    private static void PaintForest(Graphics g, int w, float horizon, float u, Random rng)
    {
        void Layer(float baseRise, float wave, float treeH, float spacing, Color tint, int vary)
        {
            float p1 = (float)rng.NextDouble() * 6, p2 = (float)rng.NextDouble() * 6;
            // The ground the trees stand on, then a solid fill below it so no sky shows between trunks.
            var ground = new List<PointF> { new(-2, horizon + 2) };
            for (float x = -2; x <= w + 2; x += Math.Max(2f, u / 60f))
                ground.Add(new PointF(x, horizon - u * (baseRise + wave * (MathF.Sin(x / u * 2.2f + p1) + 0.5f * MathF.Sin(x / u * 5.3f + p2)))));
            ground.Add(new PointF(w + 2, horizon + 2));
            using (var fill = new SolidBrush(tint)) g.FillPolygon(fill, ground.ToArray());

            for (float x = -u * 0.05f; x <= w + u * 0.05f; x += spacing * (0.6f + 0.8f * (float)rng.NextDouble()))
            {
                float foot = horizon - u * (baseRise + wave * (MathF.Sin(x / u * 2.2f + p1) + 0.5f * MathF.Sin(x / u * 5.3f + p2))) + u * 0.01f;
                float hgt = u * treeH * (0.55f + 0.7f * (float)rng.NextDouble());
                int d = rng.Next(-vary, vary + 1);
                using var ink = new SolidBrush(Color.FromArgb(Math.Clamp(tint.R + d, 0, 255), Math.Clamp(tint.G + d, 0, 255), Math.Clamp(tint.B + d, 0, 255)));
                // A fir: a narrow tip widening to the foot, with ragged sides (each
                // row of boughs sticks out its own amount and droops at its tip).
                var pts = new List<PointF> { new(x, foot - hgt) };
                var right = new List<PointF>();
                int rows = 7;
                for (int i = 1; i <= rows; i++)
                {
                    float f = i / (float)rows;
                    float hw = hgt * 0.22f * MathF.Pow(f, 0.8f) * (0.7f + 0.6f * (float)rng.NextDouble());
                    pts.Add(new PointF(x - hw, foot - hgt * (1 - f) + hw * 0.3f));
                    right.Add(new PointF(x + hw * (0.7f + 0.6f * (float)rng.NextDouble()), foot - hgt * (1 - f) + hw * 0.3f));
                }
                right.Reverse();
                g.FillPolygon(ink, [.. pts, .. right]);
            }
        }

        Layer(0.075f, 0.018f, 0.075f, u * 0.0075f, Color.FromArgb(88, 74, 116), 6);
        using (var haze = new LinearGradientBrush(new PointF(0, horizon - u * 0.12f), new PointF(0, horizon + 1),
            Color.FromArgb(0, 160, 118, 134), Color.FromArgb(130, 160, 118, 134)))
            g.FillRectangle(haze, 0, horizon - u * 0.12f, w, u * 0.12f + 1);
        Layer(0.028f, 0.012f, 0.062f, u * 0.0095f, Color.FromArgb(28, 44, 44), 5);
        // The near layer is darkest at its foot, where no sky light reaches.
        using (var foot = new LinearGradientBrush(new PointF(0, horizon - u * 0.05f), new PointF(0, horizon + 1),
            Color.FromArgb(0, 16, 26, 28), Color.FromArgb(150, 16, 26, 28)))
            g.FillRectangle(foot, 0, horizon - u * 0.05f, w, u * 0.05f + 1);
    }

    // ================================================================ meadow

    /// <summary>The meadow's base colours: hazy grey-green at the far edge, rich dark green near the bottom.</summary>
    private static void PaintMeadowBase(Graphics g, int w, int h, float horizon)
    {
        using var grass = new LinearGradientBrush(new PointF(0, horizon - 1), new PointF(0, h + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(96, 100, 70), Color.FromArgb(66, 92, 50), Color.FromArgb(44, 74, 36), Color.FromArgb(30, 56, 28)],
                Positions = [0f, 0.3f, 0.7f, 1f],
            },
        };
        g.FillRectangle(grass, 0, horizon, w, h - horizon);
    }

    /// <summary>
    /// A quick "random-looking" number from two whole numbers, always the
    /// same for the same pair (so the texture is stable, not sparkly). It
    /// scrambles the bits a few times; the exact constants do not matter,
    /// only that neighbouring inputs give unrelated outputs. 0 to 1.
    /// </summary>
    private static float Hash(int x, int y)
    {
        uint n = (uint)(x * 374761393 + y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    /// <summary>
    /// Turns the flat green into grass. For every meadow pixel: a coarse
    /// noise (clumps a few pixels wide and taller than they are wide, like
    /// tufts) and a fine per-pixel noise nudge the brightness up or down. The
    /// clumps grow toward the bottom of the screen, because near grass looks
    /// coarser than far grass. Then two touches of light: the side nearer the
    /// afterglow is a little warmer, and the far meadow fades toward the
    /// violet haze.
    /// </summary>
    private static void TextureGrass(uint[] px, int w, int h, float horizon, float u)
    {
        int y0 = Math.Max(0, (int)horizon);
        float big = u * 0.09f;                                        // the size of the broad light and dark patches
        for (int y = y0; y < h; y++)
        {
            float p = (y - horizon) / (h - horizon);
            int cell = Math.Max(1, (int)(u * 0.0035f * (0.15f + 1.3f * p)));
            int row = y / (cell * 3);
            int shift = (int)(Hash(7, row) * cell * 3);               // each row of clumps is offset, so no grid shows
            float hazeAmt = 0.42f * MathF.Pow(1 - p, 3);
            // The broad patches: a coarse noise blended smoothly between its
            // grid points ("bilinear interpolation"), so the meadow has
            // gentle lighter and darker areas the way real grass does.
            float by = y / (big * 0.45f);
            int byi = (int)by; float byf = by - byi;
            int at = y * w;
            for (int x = 0; x < w; x++, at++)
            {
                float bx = x / big;
                int bxi = (int)bx; float bxf = bx - bxi;
                float patch = (Hash(bxi, byi) * (1 - bxf) + Hash(bxi + 1, byi) * bxf) * (1 - byf)
                            + (Hash(bxi, byi + 1) * (1 - bxf) + Hash(bxi + 1, byi + 1) * bxf) * byf;
                float clump = Hash((x + shift) / cell, row);
                float fine = Hash(x, y);
                float f = 1f + 0.26f * (clump - 0.5f) + 0.12f * (fine - 0.5f) + 0.22f * (patch - 0.5f);
                float warm = 0.10f * (1 - x / (float)w);              // the afterglow side
                uint c = px[at];
                float r = ((c >> 16) & 0xFF) * f * (1 + warm), gg = ((c >> 8) & 0xFF) * f * (1 + warm * 0.5f), b = (c & 0xFF) * f;
                // Toward the horizon, mix toward the haze colour.
                r += (146 - r) * hazeAmt; gg += (118 - gg) * hazeAmt; b += (140 - b) * hazeAmt;
                px[at] = (uint)((Math.Clamp((int)r, 0, 255) << 16) | (Math.Clamp((int)gg, 0, 255) << 8) | Math.Clamp((int)b, 0, 255));
            }
        }
    }

    /// <summary>
    /// Blades and seed heads in the nearer meadow, over the flowers: short
    /// strokes of darker and lighter green, and thin pale stalks with a
    /// seed head, which is what the eye reads as "tall grass". They thin
    /// out toward the far meadow, where single blades are too small to see.
    /// </summary>
    private static void PaintGrassBlades(Graphics g, int w, int h, float horizon, float u, StreamShape stream, Cottage cottage, Random rng)
    {
        int n = (int)(2600 * w * (h - horizon) / (u * u));
        for (int i = 0; i < n; i++)
        {
            float p = MathF.Pow((float)rng.NextDouble(), 0.45f);        // mostly near
            float y = horizon + p * (h - horizon), x = (float)rng.NextDouble() * w;
            if (MathF.Abs(x - stream.CenterX(y)) < stream.HalfWidth(y) + u * 0.006f) continue;
            float tall = u * (0.006f + 0.03f * p) * (0.5f + (float)rng.NextDouble());
            // Not on the cottage: a blade rooted in front of it may stand against
            // its foot, but one rooted behind it would be painted across its wall.
            if (cottage.Covers(x, y) || cottage.Covers(x, y - tall)) continue;
            float lean = tall * 0.5f * ((float)rng.NextDouble() - 0.5f);
            float width = Math.Max(1f, u * 0.0012f * (0.5f + p));
            bool seedHead = rng.NextDouble() < 0.18;
            Color c = seedHead ? Color.FromArgb(150, 196, 180, 140)
                    : rng.NextDouble() < 0.6 ? Color.FromArgb(140, 18, 36, 20) : Color.FromArgb(110, 96, 124, 64);
            using var pen = new Pen(c, width) { EndCap = LineCap.Round };
            g.DrawLine(pen, x, y, x + lean, y - tall);
            if (seedHead)
            {
                using var head = new SolidBrush(Color.FromArgb(170, 206, 186, 146));
                g.FillEllipse(head, x + lean - width, y - tall - width * 2.5f, width * 2, width * 5);
            }
        }
    }

    /// <summary>Ground mist lying along the foot of the forest: a soft band, strongest at the horizon, gone a little way into the meadow. The stream's far end is lost in it.</summary>
    private static void PaintMist(Graphics g, int w, float horizon, float u)
    {
        float top = horizon - u * 0.02f, depth = u * 0.14f;
        using var mist = new LinearGradientBrush(new PointF(0, top), new PointF(0, top + depth), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(0, 170, 150, 170), Color.FromArgb(105, 170, 150, 170), Color.FromArgb(55, 170, 150, 170), Color.FromArgb(0, 170, 150, 170)],
                Positions = [0f, 0.2f, 0.55f, 1f],
            },
        };
        g.FillRectangle(mist, 0, top, w, depth);
    }

    /// <summary>
    /// Bushes dotted about the middle distance: low dark clumps of leaves,
    /// each a few overlapping lumps with a spray of dabs like a small tree
    /// crown. They break up the flat sweep of the meadow and give the eye
    /// steps to judge the distance by. None stands in the water or on the
    /// cottage's ground.
    /// </summary>
    private static void AddBushes(List<(float Y, Action Paint)> things, Graphics g, int w, int h, float horizon, float u,
                                  StreamShape stream, Cottage cottage, Random rng)
    {
        int count = 5 + rng.Next(4);
        var placed = new List<(float X, float Y, float R)>();
        for (int attempt = 0; attempt < 120 && placed.Count < count; attempt++)
        {
            float p = 0.12f + 0.45f * (float)rng.NextDouble();
            float y = horizon + p * (h - horizon), x = (float)rng.NextDouble() * w;
            float r = u * (0.018f + 0.05f * p) * (0.7f + 0.6f * (float)rng.NextDouble());
            if (MathF.Abs(x - stream.CenterX(y)) < stream.HalfWidth(y) + r * 1.6f) continue;
            if (cottage.Covers(x, y - r) || cottage.Covers(x - r, y) || cottage.Covers(x + r, y)) continue;
            if (placed.Any(b => MathF.Abs(b.X - x) < (b.R + r) * 1.8f && MathF.Abs(b.Y - y) < (b.R + r))) continue;
            placed.Add((x, y, r));
            float bx = x, by = y, br = r;
            things.Add((y, () =>
            {
                // Its shadow on the grass first, then the bush: no solid body,
                // only a dense spray of leaf dabs, so its edge is soft and
                // ragged and the grass shows through at the fringe. Dabs on
                // the upper left come out lighter (the afterglow side).
                using (var shade = new SolidBrush(Color.FromArgb(45, 14, 26, 24)))
                    g.FillEllipse(shade, bx - br * 0.2f, by - br * 0.12f, br * 2.2f, br * 0.3f);
                Color deep = Color.FromArgb(22, 38, 26), lit = Color.FromArgb(74, 98, 52);
                int dabs = Math.Min(2400, (int)(br * br / (u * 0.0022f * u * 0.0022f) * 1.6f));
                for (int k = 0; k < dabs; k++)
                {
                    double ang = rng.NextDouble() * Math.PI, dist = Math.Sqrt(rng.NextDouble()) * (0.85f + 0.3f * (float)rng.NextDouble());
                    float dx = (float)(Math.Cos(ang) * dist) * br * 1.1f, dy = -(float)(Math.Sin(ang) * dist) * br * 0.7f;
                    float light = Math.Clamp(0.45f - dx / br * 0.3f + dy / br * 0.45f + ((float)rng.NextDouble() - 0.5f) * 0.5f, 0, 1);
                    float dr = u * (0.0012f + 0.0022f * (float)rng.NextDouble());
                    using var dab = new SolidBrush(Color.FromArgb(200, Brushwork.Mix(deep, lit, light)));
                    g.FillEllipse(dab, bx + dx - dr, by + dy - dr, dr * 2, dr * 1.7f);
                }
            }));
        }
    }

    /// <summary>
    /// Two finishing touches a camera gives a picture. A vignette: the
    /// corners a little darker than the middle, as a lens does. And grain: a
    /// tiny random nudge to every pixel, which breaks up the smooth bands a
    /// computer gradient has (the eye reads banding as "drawn" at once).
    /// </summary>
    private static void Vignette(uint[] px, int w, int h)
    {
        float cx = w / 2f, cy = h / 2f;
        for (int y = 0; y < h; y++)
        {
            float dy = (y - cy) / cy;
            int at = y * w;
            for (int x = 0; x < w; x++, at++)
            {
                float dx = (x - cx) / cx;
                float d2 = (dx * dx + dy * dy) * 0.5f;                // 0 at the middle, 1 at a corner
                float f = 1 - 0.30f * d2 * d2;
                int grain = (int)(Hash(x, y) * 7) - 3;                // -3 to +3 levels
                uint c = px[at];
                int r = Math.Clamp((int)(((c >> 16) & 0xFF) * f) + grain, 0, 255);
                int gg = Math.Clamp((int)(((c >> 8) & 0xFF) * f) + grain, 0, 255);
                int b = Math.Clamp((int)((c & 0xFF) * f) + grain, 0, 255);
                px[at] = (uint)((r << 16) | (gg << 8) | b);
            }
        }
    }

    /// <summary>Writes a pixel array back into the bitmap (the reverse of Brushwork.ToPixels).</summary>
    private static void FromPixels(Bitmap bmp, uint[] px)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new int[px.Length];
            Buffer.BlockCopy(px, 0, raw, 0, px.Length * 4);
            Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        }
        finally { bmp.UnlockBits(data); }
    }

    // ================================================================ stream

    /// <summary>
    /// The stream. The water is a gradient that mirrors the sky: warm near
    /// the far end (where it reflects the horizon), cool blue-violet near us
    /// (where it reflects the sky overhead). Then faint ripple streaks, a few
    /// stones with foam at their upstream faces and a wake behind, and the
    /// banks: a dark lip of grass hanging over each edge, with pebbles.
    /// Returns a copy of the picture from just before the stones, for the
    /// open-water stencil.
    /// </summary>
    private static uint[] PaintStream(Graphics g, Bitmap bmp, StreamShape s, int w, int h, float u, Random rng)
    {
        PointF[] water = s.Polygon();
        using (var fill = new LinearGradientBrush(new PointF(0, s.Top - 1), new PointF(0, s.Bottom + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(232, 176, 128), Color.FromArgb(160, 132, 142), Color.FromArgb(78, 88, 118), Color.FromArgb(46, 56, 88)],
                Positions = [0f, 0.3f, 0.66f, 1f],
            },
        })
            g.FillPolygon(fill, water);

        // The water near each bank mirrors the dark grass above it; only the
        // middle of the channel sees the sky. Done on the pixels: each water
        // pixel is pulled toward a dark green by how near the edge it is
        // (the square of the distance, so the middle stays clear).
        g.Flush();
        uint[] wp = Brushwork.ToPixels(bmp);
        for (int y = Math.Max(0, (int)s.Top); y < Math.Min(h, (int)s.Bottom); y++)
        {
            float c = s.CenterX(y), hw = s.HalfWidth(y);
            int x0 = Math.Max(0, (int)(c - hw)), x1 = Math.Min(w - 1, (int)(c + hw));
            for (int x = x0; x <= x1; x++)
            {
                float d = MathF.Abs(x - c) / hw;
                float k = 0.55f * d * d;
                int at = y * w + x;
                uint px = wp[at];
                int r = (int)(((px >> 16) & 0xFF) * (1 - k) + 28 * k), gg = (int)(((px >> 8) & 0xFF) * (1 - k) + 44 * k), b = (int)((px & 0xFF) * (1 - k) + 38 * k);
                wp[at] = (uint)((r << 16) | (gg << 8) | b);
            }
        }
        FromPixels(bmp, wp);

        // Ripples: short horizontal streaks, light and dark, longer and more
        // of them near the bottom. Kept inside the water by a clip.
        GraphicsState before = g.Save();
        using (var clip = new GraphicsPath())
        {
            clip.AddPolygon(water);
            g.SetClip(clip, CombineMode.Intersect);
            using var light = new Pen(Color.FromArgb(50, 236, 226, 236), Math.Max(1f, u / 900f));
            using var dark = new Pen(Color.FromArgb(45, 24, 32, 56), Math.Max(1f, u / 900f));
            int n = (int)(900 * (h - s.Top) / u);
            for (int i = 0; i < n; i++)
            {
                float y = s.Top + (float)Math.Pow(rng.NextDouble(), 0.6) * (s.Bottom - s.Top);
                float hw = s.HalfWidth(y);
                float x = s.CenterX(y) + ((float)rng.NextDouble() * 2 - 1) * hw;
                float len = hw * (0.1f + 0.5f * (float)rng.NextDouble());
                g.DrawLine(i % 2 == 0 ? light : dark, x - len / 2, y, x + len / 2, y);
            }
        }
        g.Restore(before);

        g.Flush();
        uint[] behindFish = Brushwork.ToPixels(bmp);

        // Stones in the water, in the nearer half where they are big enough to
        // read. Each gets a clearance check against the ones already placed.
        var stones = new List<(float X, float Y, float R)>();
        int want = 6 + rng.Next(5);
        for (int attempt = 0; attempt < 200 && stones.Count < want; attempt++)
        {
            float y = s.Top + (0.40f + 0.58f * (float)rng.NextDouble()) * (s.Bottom - s.Top);
            if (y > h - u * 0.02f) continue;
            float hw = s.HalfWidth(y);
            float r = u * (0.006f + 0.016f * s.P(y)) * (0.7f + 0.6f * (float)rng.NextDouble());
            float x = s.CenterX(y) + ((float)rng.NextDouble() * 2 - 1) * (hw - r) * 0.9f;
            if (stones.Any(o => MathF.Abs(o.X - x) < (o.R + r) * 2.2f && MathF.Abs(o.Y - y) < (o.R + r) * 2.5f)) continue;
            stones.Add((x, y, r));
        }
        foreach (var (x, y, r) in stones.OrderBy(st => st.Y))
        {
            // The stone's reflection and the dark water in its lee, then a
            // faint ruffle of foam where the current meets its upstream face,
            // then the stone: a lumpy outline, lit from the upper left, with
            // a little moss on top.
            using (var lee = new SolidBrush(Color.FromArgb(70, 20, 26, 44)))
                g.FillEllipse(lee, x - r * 1.1f, y, r * 2.2f, r * 2.2f);
            using (var foam = new Pen(Color.FromArgb(90, 230, 232, 240), Math.Max(1f, r * 0.14f)))
                g.DrawArc(foam, x - r * 1.2f, y - r * 0.95f, r * 2.4f, r * 1.2f, 200, 140);
            var outline = new PointF[9];
            for (int k = 0; k < outline.Length; k++)
            {
                float a = MathF.Tau * k / outline.Length, wob = 0.8f + 0.35f * (float)rng.NextDouble();
                outline[k] = new PointF(x + MathF.Cos(a) * r * wob, y + MathF.Sin(a) * r * 0.72f * wob);
            }
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve(outline, 0.5f);
                using var shade = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(255, 136, 132, 124),
                    SurroundColors = [Color.FromArgb(255, 54, 54, 58)],
                    CenterPoint = new PointF(x - r * 0.35f, y - r * 0.35f),   // lit from the upper left
                };
                g.FillPath(shade, path);
                GraphicsState st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var moss = new SolidBrush(Color.FromArgb(120, 70, 92, 44)))
                    g.FillEllipse(moss, x - r * 0.7f, y - r * 0.9f, r * 1.1f, r * 0.7f);
                g.Restore(st);
            }
        }

        // The banks. First a strip of wet earth along each edge (darker on
        // the shaded right bank, catching a little light on the left), then
        // a lip of dark grass hanging over the water, ragged on the water
        // side, with blades leaning out over it, and pebbles along the edges.
        foreach (int side in (int[])[-1, 1])
        {
            var outer = new List<PointF>();
            var inner = new List<PointF>();
            var earthOut = new List<PointF>();
            float step = Math.Max(1f, u / 90f);
            for (float y = s.Top; y <= s.Bottom; y += step)
            {
                float c = s.CenterX(y), hw = s.HalfWidth(y), p = s.P(y);
                float over = u * (0.002f + 0.010f * p) * (0.5f + (float)rng.NextDouble());
                outer.Add(new PointF(c + side * (hw + u * 0.003f), y));
                inner.Add(new PointF(c + side * (hw - over), y));
                earthOut.Add(new PointF(c + side * (hw + u * (0.004f + 0.012f * p)), y));
            }
            var earthIn = new List<PointF>(outer);
            earthIn.Reverse();
            using (var earth = new SolidBrush(side < 0 ? Color.FromArgb(78, 62, 46) : Color.FromArgb(46, 38, 30)))
                g.FillPolygon(earth, [.. earthOut, .. earthIn]);
            inner.Reverse();
            using (var lip = new SolidBrush(Color.FromArgb(30, 50, 28)))
                g.FillPolygon(lip, [.. outer, .. inner]);
        }
        // Blades leaning out over the water from both banks.
        int blades = (int)(500 * (s.Bottom - s.Top) / u);
        for (int i = 0; i < blades; i++)
        {
            float y = s.Top + (float)Math.Pow(rng.NextDouble(), 0.6) * (s.Bottom - s.Top);
            float p = s.P(y);
            int side = rng.Next(2) == 0 ? -1 : 1;
            float x = s.CenterX(y) + side * (s.HalfWidth(y) + u * 0.002f);
            float tall = u * (0.004f + 0.022f * p) * (0.5f + (float)rng.NextDouble());
            using var blade = new Pen(Color.FromArgb(200, 26, 46, 24), Math.Max(1f, u * 0.0012f * (0.5f + p))) { EndCap = LineCap.Round };
            g.DrawLine(blade, x, y, x - side * tall * 0.6f, y - tall);
        }
        int pebbles = (int)(220 * (s.Bottom - s.Top) / u);
        for (int i = 0; i < pebbles; i++)
        {
            float y = s.Top + (float)Math.Pow(rng.NextDouble(), 0.7) * (s.Bottom - s.Top);
            float p = s.P(y);
            int side = rng.Next(2) == 0 ? -1 : 1;
            float r = u * (0.0015f + 0.005f * p) * (0.6f + 0.8f * (float)rng.NextDouble());
            float x = s.CenterX(y) + side * (s.HalfWidth(y) + u * 0.004f + r + (float)rng.NextDouble() * u * 0.012f * p);
            int shade = 90 + rng.Next(70);
            using var pebble = new SolidBrush(Color.FromArgb(220, shade, shade - 4, shade - 10));
            g.FillEllipse(pebble, x - r, y - r * 0.7f, r * 2, r * 1.4f);
        }
        return behindFish;
    }

    // ================================================================ shadows

    /// <summary>
    /// Soft shadows on the grass, all falling to the right, away from the
    /// afterglow. The cottage casts a slanted block; each tree a long
    /// ellipse. Painted before the things themselves, so they lie under them.
    /// </summary>
    private static void PaintShadows(Graphics g, PointF cottageFoot, float u, IEnumerable<(float X, float FootY, float R)> trees)
    {
        using (var shade = new SolidBrush(Color.FromArgb(70, 16, 28, 30)))
        {
            float hw = u * 0.11f, fy = cottageFoot.Y, fx = cottageFoot.X;
            g.FillPolygon(shade, [new PointF(fx - hw, fy), new PointF(fx + hw + u * 0.07f, fy - u * 0.02f),
                new PointF(fx + hw + u * 0.30f, fy + u * 0.02f), new PointF(fx - hw + u * 0.18f, fy + u * 0.04f)]);
        }
        foreach (var (x, footY, r) in trees)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(x - r * 0.3f, footY - r * 0.16f, r * 2.4f, r * 0.42f);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(95, 14, 26, 26),
                SurroundColors = [Color.FromArgb(0, 14, 26, 26)],
            };
            g.FillPath(brush, path);
        }
    }

    // ================================================================ flowers

    /// <summary>
    /// Thousands of wildflowers: white daisies, yellow buttercups, violet
    /// clover, pink campion, a few red poppies. Most grow in drifts around a
    /// few dozen random centres; the rest are scattered. Each is a dot whose
    /// size grows toward the bottom of the picture (perspective again). The
    /// bigger ones get a stem and a darker centre. None grows in the water or
    /// on the cottage. Each is added to the sorted list of things so a flower
    /// in front of a trunk is painted after the trunk.
    /// </summary>
    private static void AddFlowers(List<(float Y, Action Paint)> things, Graphics g, int w, int h, float horizon, float u,
                                   StreamShape stream, Cottage cottage, Random rng)
    {
        // (Colour, and how its drift leans: daisies and buttercups like the open
        // meadow, clover and campion grow thick in patches.)
        Color[] palette =
        [
            Color.FromArgb(232, 228, 214), Color.FromArgb(232, 228, 214), Color.FromArgb(232, 228, 214),   // daisies
            Color.FromArgb(226, 196, 86), Color.FromArgb(226, 196, 86),                                    // buttercups
            Color.FromArgb(150, 112, 186), Color.FromArgb(150, 112, 186),                                  // clover
            Color.FromArgb(214, 134, 164),                                                                 // campion
            Color.FromArgb(186, 62, 50),                                                                   // poppies
        ];
        // Each drift favours one kind, as a real patch of one flower does.
        var drifts = new List<(float X, float Y, float R, int Kind)>();
        for (int i = 0; i < 40; i++)
        {
            float p = (float)Math.Pow(rng.NextDouble(), 0.7);
            drifts.Add(((float)rng.NextDouble() * w, horizon + u * 0.04f + p * (h - horizon - u * 0.04f), u * (0.04f + 0.12f * p), rng.Next(palette.Length)));
        }

        float top = horizon + u * 0.035f;
        int count = Math.Min(11000, (int)(4200f * w * (h - top) / (u * u)));
        float stemWidth = Math.Max(1f, u * 0.0011f);
        for (int i = 0; i < count; i++)
        {
            float x, y;
            int kind;
            if (rng.NextDouble() < 0.7)
            {
                var d = drifts[rng.Next(drifts.Count)];
                double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble()) * d.R;
                x = d.X + (float)(Math.Cos(ang) * dist) * 1.5f;
                y = d.Y + (float)(Math.Sin(ang) * dist) * 0.4f;
                kind = rng.NextDouble() < 0.7 ? d.Kind : rng.Next(palette.Length);
            }
            else
            {
                x = (float)rng.NextDouble() * w;
                y = top + (float)Math.Pow(rng.NextDouble(), 0.7) * (h - top);
                kind = rng.Next(palette.Length);
            }
            if (y < top || y >= h) continue;
            float p = (y - horizon) / (h - horizon);
            if (MathF.Abs(x - stream.CenterX(y)) < stream.HalfWidth(y) + u * 0.012f) continue;   // not in the water
            if (cottage.Covers(x, y)) continue;

            Color c = palette[kind];
            // Small. A wildflower is a speck at this distance; only the nearest
            // are big enough to show petals.
            float r = u * (0.0008f + 0.0036f * p) * (0.6f + 0.8f * (float)rng.NextDouble());
            float dim = 0.68f + 0.32f * (float)rng.NextDouble();                      // dusk: no flower is at full brightness
            Color shaded = Color.FromArgb((int)(c.R * dim), (int)(c.G * dim), (int)(c.B * dim));
            bool daisy = kind < 3;
            float fx = x, fy = y, fr = r, turn = (float)rng.NextDouble() * MathF.Tau;
            // Painted LATER, from the sorted list, so the brushes are made inside
            // (a brush made here and disposed at the end of this method would be gone by then).
            things.Add((y, () =>
            {
                if (fr > 1.8f)
                {
                    using var stem = new Pen(Color.FromArgb(180, 30, 52, 26), stemWidth);
                    g.DrawLine(stem, fx, fy, fx + fr * 0.3f, fy + fr * 3f);
                }
                using var brush = new SolidBrush(Color.FromArgb(235, shaded));
                if (daisy && fr > 2.4f)
                {
                    // Near enough to show petals: a ring of five small ovals round a yellow eye.
                    float pr = fr * 0.55f;
                    for (int k = 0; k < 5; k++)
                    {
                        float a = turn + k * MathF.Tau / 5;
                        g.FillEllipse(brush, fx + MathF.Cos(a) * fr * 0.55f - pr, fy + MathF.Sin(a) * fr * 0.55f * 0.7f - pr * 0.7f, pr * 2, pr * 1.4f);
                    }
                    using var eye = new SolidBrush(Color.FromArgb(230, 196, 150, 60));
                    g.FillEllipse(eye, fx - fr * 0.3f, fy - fr * 0.25f, fr * 0.6f, fr * 0.5f);
                }
                else
                {
                    // A speck, or a small irregular head (two or three overlapping dots).
                    g.FillEllipse(brush, fx - fr, fy - fr * 0.8f, fr * 2, fr * 1.6f);
                    if (fr > 1.5f)
                    {
                        g.FillEllipse(brush, fx - fr * 0.3f, fy - fr * 1.1f, fr * 1.2f, fr * 1.0f);
                        g.FillEllipse(brush, fx + fr * 0.2f, fy - fr * 0.3f, fr * 1.1f, fr * 0.9f);
                    }
                }
            }));
        }
    }

    /// <summary>
    /// Blades of grass along the bottom edge, nearest of all, dark against
    /// everything behind them. Each is a thin curved triangle. This is the
    /// "something close in the foreground" that gives a landscape photo its depth.
    /// </summary>
    private static void PaintForegroundGrass(Graphics g, int w, int h, float u, Random rng)
    {
        using var blade = new SolidBrush(Color.FromArgb(235, 22, 40, 22));
        int n = (int)(260 * w / u);
        for (int i = 0; i < n; i++)
        {
            float x = (float)rng.NextDouble() * w;
            float tall = u * (0.025f + 0.07f * (float)rng.NextDouble());
            float lean = u * 0.03f * ((float)rng.NextDouble() - 0.5f);
            float wide = Math.Max(1f, u * 0.003f);
            float foot = h + u * 0.01f;
            g.FillClosedCurve(blade, [new PointF(x - wide, foot), new PointF(x + lean * 0.6f, foot - tall * 0.55f), new PointF(x + lean, foot - tall),
                new PointF(x + lean * 0.7f, foot - tall * 0.5f), new PointF(x + wide, foot)], FillMode.Winding, 0.3f);
        }
    }

    // ================================================================ cottage

    /// <summary>
    /// A small thatched cottage seen from the front and a little to the
    /// left, so its right side wall shows. Whitewashed walls with dark
    /// timbers, two warm windows with flower boxes, an arched door with a
    /// lantern beside it, a stone chimney at the right end of the ridge, and
    /// stepping stones leading away from the door. The lit face is the front
    /// (toward the afterglow); the side wall is in shadow. Light from the
    /// windows spills onto the grass.
    /// </summary>
    private sealed class Cottage
    {
        private readonly PointF _foot;
        private readonly float _u, _w, _h, _d, _x0, _x1, _top, _ridgeY;

        public PointF ChimneyTop { get; }
        public PointF[] Windows { get; }
        public float WindowHeight { get; }
        public PointF DoorLamp { get; }

        public Cottage(PointF foot, float u)
        {
            _foot = foot; _u = u;
            _w = u * 0.22f; _h = u * 0.115f; _d = u * 0.07f;      // front width, wall height, side depth
            _x0 = foot.X - _w / 2; _x1 = foot.X + _w / 2;
            _top = foot.Y - _h;
            _ridgeY = _top - u * 0.105f;
            float chimneyX = _x1 - _w * 0.16f + _d / 2;
            ChimneyTop = new PointF(chimneyX, _ridgeY - u * 0.055f);
            Windows = [new PointF(foot.X - _w * 0.29f, _top + _h * 0.46f), new PointF(foot.X + _w * 0.29f, _top + _h * 0.46f)];
            WindowHeight = u * 0.040f;
            DoorLamp = new PointF(foot.X + u * 0.036f, _top + _h * 0.36f);
        }

        /// <summary>Is this point on the cottage (walls, roof or side)? Used to keep flowers off it.</summary>
        public bool Covers(float x, float y) =>
            x > _x0 - _u * 0.03f && x < _x1 + _d + _u * 0.02f && y > _ridgeY - _u * 0.06f && y < _foot.Y + _u * 0.004f;

        public void Paint(Graphics g, Random rng)
        {
            float u = _u;

            // Light from the windows and the door lamp lying on the grass in front.
            foreach (PointF win in Windows)
                Spill(g, win.X, _foot.Y + u * 0.012f, u * 0.075f, u * 0.028f, Color.FromArgb(110, 255, 190, 110));
            Spill(g, _foot.X + u * 0.02f, _foot.Y + u * 0.01f, u * 0.05f, u * 0.02f, Color.FromArgb(80, 255, 190, 110));

            // Side wall (in shadow), a slanted face going back to the right:
            // the same plaster, but turned away from the glow, so it is
            // darker and cooler, darkest at the corner nearest us where the
            // roof's shadow falls. Then the gable end above it, up to the ridge.
            PointF[] side = [new(_x1, _top), new(_x1 + _d, _top - u * 0.02f), new(_x1 + _d, _foot.Y - u * 0.026f), new(_x1, _foot.Y)];
            PointF[] gableEnd = [new(_x1, _top), new(_x1 + _d, _top - u * 0.02f), new(_x1 + _d / 2, _ridgeY + u * 0.012f)];
            using (var wall = new LinearGradientBrush(new PointF(_x1, 0), new PointF(_x1 + _d, 0), Color.FromArgb(96, 82, 84), Color.FromArgb(124, 104, 98)))
            {
                g.FillPolygon(wall, side);
                g.FillPolygon(wall, gableEnd);
            }
            for (int i = 0; i < 160; i++)
            {
                float x = _x1 + (float)rng.NextDouble() * _d, y = _top - u * 0.02f + (float)rng.NextDouble() * (_h + u * 0.02f);
                float r = Math.Max(0.6f, u * 0.0012f);
                int a = 18 + rng.Next(26);
                using var fleck = new SolidBrush(rng.Next(2) == 0 ? Color.FromArgb(a, 50, 40, 40) : Color.FromArgb(a, 220, 210, 200));
                g.FillEllipse(fleck, x - r, y - r, r * 2, r * 2);
            }
            using (var beam = new Pen(Color.FromArgb(58, 40, 32), Math.Max(1.5f, u * 0.005f)))
            {
                g.DrawLine(beam, _x1 + _d * 0.5f, _top - u * 0.01f, _x1 + _d * 0.5f, _foot.Y - u * 0.013f);   // a post up the middle of the side
                g.DrawLine(beam, _x1, _top + _h * 0.70f, _x1 + _d, _top + _h * 0.70f - u * 0.02f);          // the rail, carried round the corner
            }

            // Front wall: lit plaster, a touch brighter toward the afterglow.
            using (var wall = new LinearGradientBrush(new PointF(_x0, 0), new PointF(_x1, 0), Color.FromArgb(236, 214, 182), Color.FromArgb(206, 176, 150)))
                g.FillRectangle(wall, _x0, _top, _w, _h + u * 0.004f);
            // Plaster texture: faint random speckles.
            for (int i = 0; i < 400; i++)
            {
                float x = _x0 + (float)rng.NextDouble() * _w, y = _top + (float)rng.NextDouble() * _h;
                float r = Math.Max(0.6f, u * 0.0012f);
                int a = 20 + rng.Next(30);
                using var fleck = new SolidBrush(rng.Next(2) == 0 ? Color.FromArgb(a, 90, 70, 60) : Color.FromArgb(a, 255, 250, 240));
                g.FillEllipse(fleck, x - r, y - r, r * 2, r * 2);
            }
            // Warm light on the wall around each window, under the timbers.
            foreach (PointF win in Windows)
                Spill(g, win.X, win.Y, u * 0.07f, u * 0.07f, Color.FromArgb(70, 255, 200, 120));
            // Shadow under the eave.
            using (var eave = new LinearGradientBrush(new PointF(0, _top), new PointF(0, _top + u * 0.02f), Color.FromArgb(90, 40, 30, 30), Color.FromArgb(0, 40, 30, 30)))
                g.FillRectangle(eave, _x0, _top, _w, u * 0.02f);
            // Stone footing along the bottom of the front wall.
            using (var stone = new SolidBrush(Color.FromArgb(118, 112, 104)))
                for (float x = _x0; x < _x1; x += u * 0.014f)
                {
                    float sw = u * (0.010f + 0.004f * (float)rng.NextDouble()), sh = u * (0.007f + 0.004f * (float)rng.NextDouble());
                    g.FillEllipse(stone, x, _foot.Y - sh, sw, sh + u * 0.003f);
                }

            // Timbers: dark beams on the plaster. Top beam, corner posts, a
            // post either side of the door, a diagonal brace in each bay.
            using (var beam = new Pen(Color.FromArgb(70, 46, 34), Math.Max(1.5f, u * 0.006f)))
            {
                g.DrawLine(beam, _x0, _top + u * 0.004f, _x1, _top + u * 0.004f);
                g.DrawLine(beam, _x0 + u * 0.003f, _top, _x0 + u * 0.003f, _foot.Y);
                g.DrawLine(beam, _x1 - u * 0.003f, _top, _x1 - u * 0.003f, _foot.Y);
                g.DrawLine(beam, _x0, _top + _h * 0.70f, _x1, _top + _h * 0.70f);
                foreach (float f in (float[])[0.36f, 0.64f])
                    g.DrawLine(beam, _x0 + _w * f, _top, _x0 + _w * f, _foot.Y);
                g.DrawLine(beam, _x0 + u * 0.003f, _top + _h * 0.70f, _x0 + _w * 0.12f, _top + _h * 0.98f);
                g.DrawLine(beam, _x1 - u * 0.003f, _top + _h * 0.70f, _x1 - _w * 0.12f, _top + _h * 0.98f);
            }

            // Windows: warm glass behind a cross of dark mullions, a pale
            // frame, a sill, and a flower box spilling red and pink.
            foreach (PointF win in Windows)
            {
                float ww = u * 0.036f, wh = WindowHeight;
                using (var frame = new SolidBrush(Color.FromArgb(230, 222, 200)))
                    g.FillRectangle(frame, win.X - ww / 2 - u * 0.004f, win.Y - wh / 2 - u * 0.004f, ww + u * 0.008f, wh + u * 0.008f);
                using (var glass = new LinearGradientBrush(new PointF(0, win.Y - wh / 2), new PointF(0, win.Y + wh / 2), Color.FromArgb(255, 226, 150), Color.FromArgb(255, 186, 96)))
                    g.FillRectangle(glass, win.X - ww / 2, win.Y - wh / 2, ww, wh);
                using (var mullion = new Pen(Color.FromArgb(60, 42, 34), Math.Max(1f, u * 0.0025f)))
                {
                    g.DrawLine(mullion, win.X, win.Y - wh / 2, win.X, win.Y + wh / 2);
                    g.DrawLine(mullion, win.X - ww / 2, win.Y, win.X + ww / 2, win.Y);
                }
                using (var sill = new SolidBrush(Color.FromArgb(200, 190, 170)))
                    g.FillRectangle(sill, win.X - ww / 2 - u * 0.007f, win.Y + wh / 2 + u * 0.003f, ww + u * 0.014f, u * 0.005f);
                using (var box = new SolidBrush(Color.FromArgb(84, 56, 40)))
                    g.FillRectangle(box, win.X - ww / 2 - u * 0.004f, win.Y + wh / 2 + u * 0.008f, ww + u * 0.008f, u * 0.011f);
                for (int i = 0; i < 14; i++)
                {
                    float bx = win.X - ww / 2 + (float)rng.NextDouble() * ww, by = win.Y + wh / 2 + u * (0.004f + 0.009f * (float)rng.NextDouble());
                    float br = u * (0.0025f + 0.002f * (float)rng.NextDouble());
                    using var bloom = new SolidBrush(i % 3 == 0 ? Color.FromArgb(70, 110, 50) : i % 3 == 1 ? Color.FromArgb(210, 60, 60) : Color.FromArgb(236, 130, 160));
                    g.FillEllipse(bloom, bx - br, by - br, br * 2, br * 2);
                }
            }

            // The door: an arch of dark planks with a round-topped frame.
            float dw = u * 0.040f, dh = _h * 0.66f, dx = _foot.X - dw / 2, dy = _foot.Y - dh;
            using (var doorPath = new GraphicsPath())
            {
                doorPath.AddArc(dx, dy, dw, dw, 180, 180);
                doorPath.AddLine(dx + dw, dy + dw / 2, dx + dw, _foot.Y + u * 0.002f);
                doorPath.AddLine(dx + dw, _foot.Y + u * 0.002f, dx, _foot.Y + u * 0.002f);
                doorPath.CloseFigure();
                using (var frame = new Pen(Color.FromArgb(190, 176, 150), Math.Max(1.5f, u * 0.005f)))
                    g.DrawPath(frame, doorPath);
                using (var wood = new SolidBrush(Color.FromArgb(76, 48, 32)))
                    g.FillPath(wood, doorPath);
                GraphicsState st = g.Save();
                g.SetClip(doorPath, CombineMode.Intersect);
                using (var plank = new Pen(Color.FromArgb(52, 32, 22), Math.Max(1f, u * 0.0015f)))
                    for (int i = 1; i < 4; i++) g.DrawLine(plank, dx + dw * i / 4f, dy, dx + dw * i / 4f, _foot.Y);
                using (var knob = new SolidBrush(Color.FromArgb(200, 170, 90)))
                    g.FillEllipse(knob, dx + dw * 0.72f, dy + dh * 0.55f, u * 0.004f, u * 0.004f);
                g.Restore(st);
            }
            // The lantern beside the door: a bracket and a small warm lamp (the scene adds its glow).
            using (var bracket = new Pen(Color.FromArgb(40, 34, 30), Math.Max(1f, u * 0.002f)))
                g.DrawLine(bracket, DoorLamp.X, DoorLamp.Y - u * 0.012f, DoorLamp.X, DoorLamp.Y - u * 0.004f);
            using (var lamp = new SolidBrush(Color.FromArgb(255, 220, 150)))
                g.FillEllipse(lamp, DoorLamp.X - u * 0.004f, DoorLamp.Y - u * 0.004f, u * 0.008f, u * 0.008f);

            // Stepping stones from the door, toward the viewer and the stream.
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                float sx = _foot.X + u * 0.015f + t * u * 0.16f + u * 0.01f * ((float)rng.NextDouble() - 0.5f);
                float sy = _foot.Y + u * 0.012f + t * u * 0.13f;
                float sr = u * (0.009f + 0.011f * t);
                using var slab = new SolidBrush(Color.FromArgb(215, 118, 116, 108));
                g.FillEllipse(slab, sx - sr, sy - sr * 0.45f, sr * 2, sr * 0.9f);
            }

            // The roof: a thatched front slope (a trapezoid: the eave is wide
            // and near, the ridge shorter and set back and up), the thatch
            // drawn as hundreds of thin strokes running up the slope.
            float eaveY = _top + u * 0.014f, over = u * 0.028f;
            PointF e0 = new(_x0 - over, eaveY), e1 = new(_x1 + over, eaveY);
            PointF r0 = new(_x0 - over + _d / 2, _ridgeY), r1 = new(_x1 + over + _d / 2, _ridgeY);
            using (var roofPath = new GraphicsPath())
            {
                roofPath.AddPolygon([e0, e1, r1, r0]);
                using (var thatch = new LinearGradientBrush(new PointF(0, _ridgeY), new PointF(0, eaveY), Color.FromArgb(150, 120, 78), Color.FromArgb(96, 72, 46)))
                    g.FillPath(thatch, roofPath);
                GraphicsState st = g.Save();
                g.SetClip(roofPath, CombineMode.Intersect);
                // Thatch is bundles of reed laid up the slope: short strokes,
                // each starting somewhere on the slope and running a little
                // way up it, in many shades, so no two rows line up.
                int strands = (int)(_w / Math.Max(1f, u * 0.0022f)) * 5;
                for (int i = 0; i < strands; i++)
                {
                    float t = (float)rng.NextDouble(), s0 = (float)rng.NextDouble() * 0.8f, s1 = s0 + 0.12f + 0.25f * (float)rng.NextDouble();
                    PointF eave = new(e0.X + (e1.X - e0.X) * t, eaveY + u * 0.004f), ridge = new(r0.X + (r1.X - r0.X) * t, _ridgeY);
                    PointF a = new(eave.X + (ridge.X - eave.X) * s0, eave.Y + (ridge.Y - eave.Y) * s0);
                    PointF b = new(eave.X + (ridge.X - eave.X) * s1, eave.Y + (ridge.Y - eave.Y) * s1);
                    float k = (float)rng.NextDouble();
                    int shade = (int)(70 + 70 * k);
                    using var strand = new Pen(Color.FromArgb(55, shade + 30, (int)(shade * 0.84f), (int)(shade * 0.55f)), Math.Max(1f, u * 0.0024f));
                    g.DrawLine(strand, a, b);
                }
                // Weathering: a few darker damp patches.
                for (int i = 0; i < 7; i++)
                {
                    float px = e0.X + (e1.X - e0.X) * (float)rng.NextDouble(), py = _ridgeY + (eaveY - _ridgeY) * (float)rng.NextDouble();
                    float pr = u * (0.015f + 0.025f * (float)rng.NextDouble());
                    using var patchPath = new GraphicsPath();
                    patchPath.AddEllipse(px - pr, py - pr * 0.5f, pr * 2, pr);
                    using var patch = new PathGradientBrush(patchPath) { CenterColor = Color.FromArgb(60, 40, 30, 20), SurroundColors = [Color.FromArgb(0, 40, 30, 20)] };
                    g.FillPath(patch, patchPath);
                }
                g.Restore(st);
                // A darker, ragged eave line, and a lighter rounded ridge cap.
                using (var eave = new Pen(Color.FromArgb(70, 50, 32), Math.Max(2f, u * 0.008f)))
                    g.DrawLine(eave, e0, e1);
                using (var ridge = new Pen(Color.FromArgb(170, 140, 92), Math.Max(2f, u * 0.009f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(ridge, r0, r1);
                // The thatch's thickness along the gable edge on the right.
                using (var edge = new Pen(Color.FromArgb(128, 100, 64), Math.Max(2f, u * 0.008f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(edge, e1, r1);
            }

            // The chimney: a column of fieldstone standing on the ridge, lit
            // on its left, with a slab on top. Each stone is its own rounded
            // shape in its own grey, laid in rough courses.
            float cw = u * 0.028f, cx = ChimneyTop.X, chTop = ChimneyTop.Y, chBottom = _ridgeY + u * 0.03f;
            using (var mortar = new SolidBrush(Color.FromArgb(72, 66, 64)))
                g.FillRectangle(mortar, cx - cw / 2, chTop, cw, chBottom - chTop);
            float course = u * 0.0075f;
            for (float y = chTop + u * 0.002f; y < chBottom - course; y += course)
            {
                float x = cx - cw / 2 + u * 0.001f;
                while (x < cx + cw / 2 - u * 0.002f)
                {
                    float sw = Math.Min(cx + cw / 2 - x - u * 0.001f, u * (0.006f + 0.008f * (float)rng.NextDouble()));
                    float lit = 0.55f + 0.45f * (1 - (x - (cx - cw / 2)) / cw);          // the left side catches the glow
                    int shade = (int)((96 + rng.Next(40)) * lit);
                    using var stone = new SolidBrush(Color.FromArgb(Math.Clamp(shade + 8, 0, 255), Math.Clamp(shade, 0, 255), Math.Clamp(shade - 4, 0, 255)));
                    using var sp = new GraphicsPath();
                    sp.AddEllipse(x, y + u * 0.0005f, sw, course - u * 0.001f);
                    g.FillPath(stone, sp);
                    x += sw + u * 0.0012f;
                }
            }
            using (var cap = new SolidBrush(Color.FromArgb(104, 98, 96)))
                g.FillRectangle(cap, cx - cw / 2 - u * 0.004f, chTop - u * 0.006f, cw + u * 0.008f, u * 0.007f);
            using (var pot = new SolidBrush(Color.FromArgb(40, 36, 36)))
                g.FillRectangle(pot, cx - cw * 0.3f, chTop - u * 0.006f, cw * 0.6f, u * 0.0025f);   // the dark mouth
        }

        /// <summary>A soft oval of light on a surface: strong in the middle, nothing at the edge.</summary>
        private static void Spill(Graphics g, float cx, float cy, float rx, float ry, Color c)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = c,
                SurroundColors = [Color.FromArgb(0, c)],
                Blend = Peckworks.Screensavers.Core.Sprite.SoftFalloff,
            };
            g.FillPath(brush, path);
        }
    }

    // ================================================================ trees

    /// <summary>
    /// A broad-leaved tree. A tapered trunk with a slight lean and a flare at
    /// the roots, a few limbs reaching up into the crown, and a crown made of
    /// lumps the way a child draws a cloud, each lump a dark body covered in
    /// hundreds of small leaf-shaped dabs. The dabs on the upper left (toward
    /// the afterglow) come out lighter and warmer; the lower right stays in
    /// shadow. At dusk a tree is mostly silhouette with a warm rim.
    /// </summary>
    private sealed class Tree
    {
        private readonly PointF _foot;
        private readonly float _r, _u, _lean, _trunkH;
        private readonly Random _rng;

        public TreeFacts Facts { get; }

        public Tree(PointF foot, float r, float u, Random rng)
        {
            _foot = foot; _r = r; _u = u; _rng = rng;
            _lean = r * 0.18f * ((float)rng.NextDouble() * 2 - 1);
            _trunkH = r * 1.35f;
            Facts = new TreeFacts(foot, new PointF(foot.X + _lean * 0.8f, foot.Y - _trunkH * 0.82f), r);
        }

        public void Paint(Graphics g)
        {
            float r = _r, u = _u;
            PointF top = new(_foot.X + _lean, _foot.Y - _trunkH);
            Color bark = Color.FromArgb(46, 36, 32);

            // The trunk: a tapered shape with a root flare, then bark lines.
            float wb = r * 0.20f, wt = r * 0.11f;
            using (var wood = new SolidBrush(bark))
                g.FillClosedCurve(wood, [
                    new PointF(_foot.X - wb * 1.6f, _foot.Y + u * 0.006f), new PointF(_foot.X - wb, _foot.Y - r * 0.12f),
                    new PointF(top.X - wt, top.Y), new PointF(top.X + wt, top.Y),
                    new PointF(_foot.X + wb, _foot.Y - r * 0.12f), new PointF(_foot.X + wb * 1.6f, _foot.Y + u * 0.006f)], FillMode.Winding, 0.2f);
            using (var ridge = new Pen(Color.FromArgb(70, 56, 48), Math.Max(1f, u * 0.0015f)))
                for (int i = 0; i < 7; i++)
                {
                    float f = (i + 0.5f) / 7f * 2 - 1;
                    g.DrawLine(ridge, _foot.X + f * wb * 0.8f, _foot.Y - r * 0.05f, top.X + f * wt * 0.8f, top.Y + r * 0.1f);
                }
            // Limbs into the crown.
            using (var limb = new Pen(bark, Math.Max(1.5f, r * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float a in (float[])[-1.1f, -0.35f, 0.4f, 1.0f])
                {
                    float len = r * (0.5f + 0.3f * (float)_rng.NextDouble());
                    g.DrawLine(limb, top, new PointF(top.X + MathF.Sin(a) * len, top.Y - MathF.Cos(a) * len * 0.9f));
                }

            // The crown: many small lumps (so the outline is bumpy, not a
            // balloon), each a dark body, then a fine spray of leaf dabs.
            // At dusk the leaves are nearly black-green; the few that catch
            // the afterglow on the upper left are warm.
            PointF c = new(top.X, top.Y - r * 0.45f);
            int lumpCount = 14 + _rng.Next(6);
            var lumps = new (float X, float Y, float R)[lumpCount];
            for (int i = 0; i < lumpCount; i++)
            {
                double ang = _rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(_rng.NextDouble()) * 0.7;
                lumps[i] = (c.X + (float)(Math.Cos(ang) * dist) * r * 1.25f, c.Y + (float)(Math.Sin(ang) * dist) * r * 0.85f, r * (0.22f + 0.16f * (float)_rng.NextDouble()));
            }
            using (var body = new SolidBrush(Color.FromArgb(235, 22, 34, 24)))
                foreach (var l in lumps)
                    g.FillEllipse(body, l.X - l.R * 0.95f, l.Y - l.R * 0.95f, l.R * 1.9f, l.R * 1.9f);

            Color deep = Color.FromArgb(16, 28, 20), lit = Color.FromArgb(62, 84, 46), rim = Color.FromArgb(168, 128, 72);
            int dabs = Math.Min(9000, (int)(r * r / (u * 0.003f * u * 0.003f) * 1.6f));
            for (int i = 0; i < dabs; i++)
            {
                var l = lumps[_rng.Next(lumpCount)];
                double ang = _rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(_rng.NextDouble()) * 1.08;
                float x = l.X + (float)(Math.Cos(ang) * dist) * l.R, y = l.Y + (float)(Math.Sin(ang) * dist) * l.R;
                float light = Math.Clamp(0.42f - (x - c.X) / r * 0.35f - (y - c.Y) / r * 0.45f + ((float)_rng.NextDouble() - 0.5f) * 0.55f, 0, 1);
                Color col = light > 0.9f ? Brushwork.Mix(lit, rim, (light - 0.9f) / 0.1f) : Brushwork.Mix(deep, lit, light / 0.9f);
                float dr = u * (0.0014f + 0.0022f * (float)_rng.NextDouble());
                using var dab = new SolidBrush(Color.FromArgb(215, col));
                float tilt = (float)_rng.NextDouble();
                g.FillEllipse(dab, x - dr, y - dr * (0.5f + tilt * 0.6f), dr * 2, dr * (1f + tilt * 1.2f));
            }
        }
    }
}
