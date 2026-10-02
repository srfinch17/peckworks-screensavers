using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

/// <summary>The finished backdrop: a still picture plus a few facts the animation needs.</summary>
internal sealed class ChristmasScenery
{
    public required uint[] Pixels { get; init; }                        // the painted picture, same layout as a FrameBuffer
    public required List<(PointF At, int Color)> Lights { get; init; }  // every bulb: where it is, and which pastel (an index into Pastels)
    public required bool[] IsSky { get; init; }                         // one true/false per pixel: true where nothing was painted over the sky
}

/// <summary>
/// Paints the backdrop: a deep blue night sky with stars and a moon, two
/// ranges of snowy mountains, a snow-covered hill, a log cabin with warm
/// windows and a smoking chimney, snowy pines strung with pastel lights, and
/// bare branches at the top corners with more lights on their twigs.
///
/// Like the Sakura painters, this runs ONCE at startup (the scenery never
/// moves) and paints back to front. The pines, the ground, the mountains and
/// the branches come from the engine's shared Brushwork helpers.
///
/// Two things here are for the animation, not the picture:
///
///   - Every bulb's position is handed back, so the scene can make each one
///     twinkle by stamping a soft glow over it.
///   - A "sky stencil": after the sky is painted, we keep a copy. At the end
///     we compare it to the finished picture. Any pixel that did not change
///     is still sky. Santa is only ever drawn on sky pixels, and that is the
///     whole trick that makes him pass BEHIND the trees and the mountains.
///
/// Positions are fractions of the width (w) or height (h). Sizes are
/// fractions of "u": the height on a normal wide screen, or less on a tall one.
/// </summary>
internal static class ChristmasPainter
{
    /// <summary>The pastel bulb colors: pink, mint, baby blue, lavender, butter, peach.</summary>
    public static readonly Color[] Pastels =
    [
        Color.FromArgb(255, 176, 204), Color.FromArgb(168, 240, 204), Color.FromArgb(164, 212, 255),
        Color.FromArgb(206, 180, 255), Color.FromArgb(255, 240, 166), Color.FromArgb(255, 204, 164),
    ];

    private const int Butter = 4;   // the star on the tallest tree is butter yellow

    public static ChristmasScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float horizon = h * 0.74f;
        float u = Math.Min(h, w * 9f / 16f);
        var lights = new List<(PointF At, int Color)>();

        // ---- The paint order, back to front. Keep this list explicit. ----
        PaintSky(g, w, horizon, u, rng);
        PaintMoon(g, w * 0.24f, h * 0.20f, u * 0.065f);
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);   // the copy the sky stencil is made from (see the class summary)

        PointF[] farRange = Brushwork.Ridge(w, horizon, u, 0.09f, 0.05f, 0.02f, 0.09f, rng);
        using (var far = new SolidBrush(Color.FromArgb(62, 84, 140)))
            g.FillPolygon(far, farRange);
        PointF[] nearRange = Brushwork.Ridge(w, horizon, u, 0.03f, 0.03f, 0.012f, 0.04f, rng);
        using (var near = new SolidBrush(Color.FromArgb(104, 128, 184)))
        {
            g.FillPolygon(near, nearRange);
            g.FillRectangle(near, 0, horizon, w, h - horizon);   // the valley floor, down to where the snowy hill covers it
        }

        Bank ground = MakeGround(w, h);
        ground.Paint(g, Color.FromArgb(222, 233, 252), Color.FromArgb(118, 140, 196), Color.FromArgb(220, 255, 255, 255), Math.Max(1.5f, u * 0.004f));

        PaintCabin(g, w * 0.58f, ground.YAt(w * 0.58f) + u * 0.008f, u, lights);

        // (X as a fraction of the width, height in u). Tallest first in each
        // group, so the shorter trees in front overlap the taller ones behind.
        (float X, float Height)[] pines =
        [
            (0.05f, 0.44f), (0.23f, 0.37f), (0.14f, 0.30f), (0.34f, 0.22f),
            (0.88f, 0.45f), (0.80f, 0.33f), (0.96f, 0.31f),
        ];
        foreach (var p in pines)
        {
            float x = w * p.X, foot = ground.YAt(x) + u * 0.02f, top = foot - u * p.Height;
            Brushwork.Pine(g, x, top, foot, u, rng, snowy: true);
            StringLights(g, x, top, foot, u, rng, lights);
        }
        // The star goes on the tallest tree (the one at 88% across).
        float starX = w * 0.88f, starY = ground.YAt(starX) + u * 0.02f - u * 0.45f - u * 0.008f;
        PaintStar(g, starX, starY, u * 0.016f);
        lights.Add((new PointF(starX, starY), Butter));

        PaintCornerBranches(g, w, h, u, rng, lights);

        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);
        var isSky = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) isSky[i] = pixels[i] == skyOnly[i];

        return new ChristmasScenery { Pixels = pixels, Lights = lights, IsSky = isSky };
    }

    // ================================================================ sky

    /// <summary>
    /// A clear winter night: nearly black blue overhead, lightening toward
    /// the horizon (you look through more air there, so it washes out), with
    /// a scatter of stars, most of them faint and a few bright.
    /// </summary>
    private static void PaintSky(Graphics g, int w, float horizon, float u, Random rng)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(8, 14, 46), Color.FromArgb(20, 36, 92), Color.FromArgb(58, 84, 150)],
                Positions = [0f, 0.55f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        for (int i = 0; i < 220; i++)
        {
            float x = (float)rng.NextDouble() * w, y = (float)Math.Pow(rng.NextDouble(), 1.3) * horizon * 0.85f;
            float r = Math.Max(0.6f, u * (0.0008f + 0.0016f * (float)Math.Pow(rng.NextDouble(), 3)));
            using var star = new SolidBrush(Color.FromArgb(80 + rng.Next(170), 232, 240, 255));
            g.FillEllipse(star, x - r, y - r, r * 2, r * 2);
        }
    }

    /// <summary>A cool white moon with a wide soft glow around it (a "path gradient": strongest at the center, nothing at the edge).</summary>
    private static void PaintMoon(Graphics g, float x, float y, float r)
    {
        float glowR = r * 4f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - glowR, y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(170, 190, 214, 255),
                SurroundColors = [Color.FromArgb(0, 150, 190, 255)],
                Blend = Sprite.SoftFalloff,   // eases out, so the glow has no visible rim
            };
            g.FillPath(glow, glowPath);
        }
        using var disc = new SolidBrush(Color.FromArgb(244, 248, 255));
        g.FillEllipse(disc, x - r, y - r, r * 2, r * 2);
    }

    // ================================================================ ground and cabin

    /// <summary>
    /// The snowy hill: one bank of ground running the full width of the
    /// screen, in soft rolls. Everything that stands on it asks it "how high
    /// is the ground here?".
    /// </summary>
    private static Bank MakeGround(int w, int h) => new(
    [
        new PointF(-2, h * 0.80f), new PointF(w * 0.14f, h * 0.785f), new PointF(w * 0.30f, h * 0.815f),
        new PointF(w * 0.46f, h * 0.84f), new PointF(w * 0.62f, h * 0.825f), new PointF(w * 0.80f, h * 0.79f),
        new PointF(w + 2, h * 0.80f),
    ], h);

    /// <summary>
    /// A log cabin: a brown wall with a few darker lines for the logs, a
    /// thick blanket of snow for a roof, a chimney with smoke, a door, and
    /// two warm windows. Warm light spills onto the snow in front, and a
    /// string of pastel bulbs hangs along the edge of the roof (added to the
    /// same list as the tree lights, so they twinkle too).
    /// </summary>
    private static void PaintCabin(Graphics g, float x, float footY, float u, List<(PointF At, int Color)> lights)
    {
        float wallW = u * 0.15f, wallH = u * 0.062f, top = footY - wallH;

        // Smoke first: it rises behind the roof. Each puff is bigger and fainter than the one below.
        for (int i = 0; i < 5; i++)
        {
            float pr = u * (0.010f + 0.006f * i);
            using var puff = new SolidBrush(Color.FromArgb(70 - i * 11, 210, 220, 240));
            g.FillEllipse(puff, x + wallW * 0.24f + u * 0.012f * i - pr, top - u * (0.075f + 0.030f * i) - pr, pr * 2, pr * 2);
        }

        // Light from the windows lying on the snow.
        using (var spillPath = new GraphicsPath())
        {
            spillPath.AddEllipse(x - wallW * 0.6f, footY - u * 0.012f, wallW * 1.2f, u * 0.05f);
            using var spill = new PathGradientBrush(spillPath)
            {
                CenterColor = Color.FromArgb(120, 255, 206, 130),
                SurroundColors = [Color.FromArgb(0, 255, 206, 130)],
            };
            g.FillPath(spill, spillPath);
        }

        using (var wall = new SolidBrush(Color.FromArgb(86, 54, 40)))
            g.FillRectangle(wall, x - wallW / 2, top, wallW, wallH + u * 0.004f);
        using (var seam = new Pen(Color.FromArgb(60, 36, 28), Math.Max(1f, u * 0.0015f)))
            for (int i = 1; i < 4; i++)
                g.DrawLine(seam, x - wallW / 2, top + wallH * i / 4f, x + wallW / 2, top + wallH * i / 4f);

        using (var brick = new SolidBrush(Color.FromArgb(104, 60, 52)))
            g.FillRectangle(brick, x + wallW * 0.20f, top - u * 0.060f, u * 0.016f, u * 0.04f);

        using (var snow = new SolidBrush(Color.FromArgb(236, 243, 255)))
        {
            g.FillPolygon(snow, [
                new PointF(x - wallW * 0.60f, top + u * 0.004f), new PointF(x - wallW * 0.36f, top - u * 0.042f),
                new PointF(x + wallW * 0.36f, top - u * 0.042f), new PointF(x + wallW * 0.60f, top + u * 0.004f)]);
            g.FillEllipse(snow, x + wallW * 0.20f - u * 0.003f, top - u * 0.066f, u * 0.022f, u * 0.010f);   // the cap on the chimney
        }

        using (var door = new SolidBrush(Color.FromArgb(54, 32, 26)))
            g.FillRectangle(door, x - u * 0.010f, footY - wallH * 0.70f, u * 0.020f, wallH * 0.70f);

        using var glass = new SolidBrush(Color.FromArgb(255, 214, 132));
        using var bar = new Pen(Color.FromArgb(86, 54, 40), Math.Max(1f, u * 0.002f));
        foreach (float side in (float[])[-1f, 1f])
        {
            float wx = x + side * wallW * 0.30f, wy = top + wallH * 0.42f, ww = u * 0.024f, wh = u * 0.022f;
            g.FillRectangle(glass, wx - ww / 2, wy - wh / 2, ww, wh);
            g.DrawLine(bar, wx, wy - wh / 2, wx, wy + wh / 2);            // the cross in the window
            g.DrawLine(bar, wx - ww / 2, wy, wx + ww / 2, wy);
        }

        // Bulbs along the roof edge.
        const int bulbs = 11;
        for (int i = 0; i <= bulbs; i++)
        {
            var at = new PointF(x - wallW * 0.58f + wallW * 1.16f * i / bulbs, top + u * 0.009f);
            PaintBulb(g, at, u, i % Pastels.Length);
            lights.Add((at, i % Pastels.Length));
        }
    }

    // ================================================================ lights

    /// <summary>One unlit bulb: a small dot of its pastel color. The scene adds the glow that makes it shine.</summary>
    private static void PaintBulb(Graphics g, PointF at, float u, int color)
    {
        float r = Math.Max(1.2f, u * 0.0032f);
        using var bulb = new SolidBrush(Pastels[color]);
        g.FillEllipse(bulb, at.X - r, at.Y - r, r * 2, r * 2);
    }

    /// <summary>
    /// Strings lights on one pine. The string zigzags down the tree: left
    /// edge to right edge, then right back to left a bit lower, and so on.
    /// Each run sags slightly in the middle, as a real string does under its
    /// own weight (the sag is biggest halfway along and zero at both ends,
    /// which is what 4 * t * (1 - t) gives as t goes from 0 to 1).
    ///
    /// The string asks the shared pine code how wide the tree is at each
    /// height (Brushwork.PineHalfWidth), so the bulbs always sit on the
    /// boughs, whatever size the tree is.
    /// </summary>
    private static void StringLights(Graphics g, float x, float top, float foot, float u, Random rng,
                                     List<(PointF At, int Color)> lights)
    {
        using var wire = new Pen(Color.FromArgb(150, 16, 30, 24), Math.Max(1f, u * 0.0012f));
        float bottom = foot - u * 0.05f;
        bool leftToRight = rng.Next(2) == 0;
        int color = rng.Next(Pastels.Length);

        for (float y = top + u * 0.045f; y < bottom; y += u * 0.048f)
        {
            float yEnd = Math.Min(bottom, y + u * 0.030f);
            float xa = x - Brushwork.PineHalfWidth(y, top, foot, u) * 0.85f;
            float xb = x + Brushwork.PineHalfWidth(yEnd, top, foot, u) * 0.85f;
            if (!leftToRight) { xa = 2 * x - xa; xb = 2 * x - xb; }   // mirror the run across the trunk

            int n = Math.Max(2, (int)(MathF.Abs(xb - xa) / (u * 0.017f)));
            var run = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                run[i] = new PointF(xa + (xb - xa) * t, y + (yEnd - y) * t + u * 0.007f * 4 * t * (1 - t));
            }
            g.DrawLines(wire, run);
            foreach (PointF at in run)
            {
                PaintBulb(g, at, u, color);
                lights.Add((at, color));
                color = (color + 1) % Pastels.Length;   // the colors take turns along the string
            }
            leftToRight = !leftToRight;
        }
    }

    /// <summary>
    /// A five-pointed star. Ten points around a circle, alternating between
    /// the outer radius (the tips) and a smaller inner radius (the notches).
    /// </summary>
    private static void PaintStar(Graphics g, float x, float y, float r)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -MathF.PI / 2 + i * MathF.PI / 5;     // start pointing straight up; a tenth of a turn per point
            float rr = i % 2 == 0 ? r : r * 0.45f;
            pts[i] = new PointF(x + MathF.Cos(a) * rr, y + MathF.Sin(a) * rr);
        }
        using var gold = new SolidBrush(Pastels[Butter]);
        g.FillPolygon(gold, pts);
    }

    /// <summary>
    /// Bare winter branches reaching in from the top corners (the sakura
    /// branches with the flowers switched off), with a bulb wherever a
    /// blossom cluster would have been.
    /// </summary>
    private static void PaintCornerBranches(Graphics g, int w, int h, float u, Random rng, List<(PointF At, int Color)> lights)
    {
        Color wood = Color.FromArgb(30, 26, 40);
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.05f), 0.30f, u * 0.44f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.03f), MathF.PI - 0.30f, u * 0.42f, u * 0.017f, 3),  // top right, reaching left
        ];
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng, wood, wood, wood, bare: true);
        for (int i = 0; i < spots.Count; i += 3)   // every third spot: a bulb on all of them clumps into one bright smear
        {
            PointF at = spots[i];
            if (at.X < 0 || at.X >= w || at.Y < 0 || at.Y >= h) continue;
            int color = rng.Next(Pastels.Length);
            PaintBulb(g, at, u, color);
            lights.Add((at, color));
        }
    }
}
