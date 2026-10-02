using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween;

/// <summary>The finished backdrop: a still picture plus a few facts the animation needs.</summary>
internal sealed class HalloweenScenery
{
    public required uint[] Pixels { get; init; }                     // the painted picture, same layout as a FrameBuffer
    public required List<PointF> LeafSpots { get; init; }            // places in the autumn trees that leaves can drop from
    public required List<(PointF At, float R)> Pumpkins { get; init; } // each jack-o'-lantern's center and size, for its candle glow
}

/// <summary>
/// Paints the backdrop: a purple night sky with a big full moon and a few
/// streaks of cloud, two ranges of dark hills with a haunted house on the far
/// one, a band of mist, a graveyard hill with tombstones, a dead tree, autumn
/// trees in orange leaf, jack-o'-lanterns, and bare branches clawing in from
/// the top corners.
///
/// Like the Sakura painters, this runs ONCE at startup (the scenery never
/// moves) and paints back to front: whatever is farthest away goes down
/// first, and each nearer thing simply covers what is behind it. The trees,
/// the ground, the hills and the branches come from the engine's shared
/// Brushwork helpers, so this scene has the same hand as the sakura ones;
/// only the colors and the props are Halloween's own.
///
/// Positions are fractions of the width (w) or height (h). Sizes are
/// fractions of "u": the height on a normal wide screen, or less on a tall
/// one, so trees and the moon do not swallow a portrait monitor.
/// </summary>
internal static class HalloweenPainter
{
    private static readonly Color Ink = Color.FromArgb(12, 8, 20);   // the near-black of every silhouette

    public static HalloweenScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float horizon = h * 0.76f;
        float u = Math.Min(h, w * 9f / 16f);

        // ---- The paint order, back to front. Keep this list explicit. ----
        PaintSky(g, w, h, horizon, u, rng);
        PaintMoon(g, w * 0.70f, h * 0.27f, u * 0.12f, rng);
        PaintClouds(g, w, h, u, rng);

        PointF[] farRidge = Brushwork.Ridge(w, horizon, u, 0.07f, 0.05f, 0.015f, 0.06f, rng);
        using (var far = new SolidBrush(Color.FromArgb(44, 26, 66)))
            g.FillPolygon(far, farRidge);
        PaintHouse(g, w * 0.26f, Brushwork.RidgeYAt(farRidge, w * 0.26f) + u * 0.012f, u);

        PointF[] nearRidge = Brushwork.Ridge(w, horizon, u, 0.02f, 0.03f, 0.012f, 0.03f, rng);
        using (var near = new SolidBrush(Color.FromArgb(28, 16, 44)))
        {
            g.FillPolygon(near, nearRidge);
            g.FillRectangle(near, 0, horizon, w, h - horizon);   // the valley floor, down to where the graveyard hill covers it
        }
        PaintMist(g, w, horizon, u);

        Bank ground = MakeGround(w, h);
        ground.Paint(g, Color.FromArgb(40, 28, 56), Color.FromArgb(10, 6, 16), Color.FromArgb(110, 130, 104, 150), Math.Max(1.5f, u * 0.004f));

        (float X, float Tilt, bool Cross)[] stones =
            [(0.41f, -6, false), (0.47f, 4, true), (0.535f, -3, false), (0.60f, 8, false), (0.655f, -5, true)];
        foreach (var s in stones)
            PaintTombstone(g, w * s.X, ground.YAt(w * s.X) + u * 0.006f, u, s.Tilt, s.Cross);

        PaintDeadTree(g, w * 0.565f, ground.YAt(w * 0.565f) + u * 0.01f, u, rng);
        List<PointF> leafSpots = PaintAutumnTrees(g, w, u, ground, rng);

        var pumpkins = new List<(PointF At, float R)>();
        (float X, float R)[] patch = [(0.30f, 0.030f), (0.345f, 0.020f), (0.73f, 0.024f), (0.775f, 0.032f)];
        foreach (var p in patch)
            pumpkins.Add(PaintPumpkin(g, w * p.X, ground.YAt(w * p.X) + u * 0.012f, u * p.R));

        PaintCornerBranches(g, w, h, u, rng);

        return new HalloweenScenery { Pixels = Brushwork.ToPixels(bmp), LeafSpots = leafSpots, Pumpkins = pumpkins };
    }

    // ================================================================ sky

    /// <summary>
    /// The night sky: near-black violet overhead, through purple, to a dull
    /// orange glow along the horizon (the last of the sunset, and the classic
    /// Halloween pairing of purple and orange). Then a scatter of faint stars
    /// in the darker upper part.
    /// </summary>
    private static void PaintSky(Graphics g, int w, int h, float horizon, float u, Random rng)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(20, 14, 48), Color.FromArgb(54, 30, 92), Color.FromArgb(120, 56, 96), Color.FromArgb(214, 112, 52)],
                Positions = [0f, 0.45f, 0.80f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        for (int i = 0; i < 140; i++)
        {
            float x = (float)rng.NextDouble() * w, y = (float)Math.Pow(rng.NextDouble(), 1.5) * horizon * 0.75f;
            float r = Math.Max(0.6f, u * (0.0008f + 0.0012f * (float)rng.NextDouble()));
            using var star = new SolidBrush(Color.FromArgb(70 + rng.Next(150), 236, 226, 255));
            g.FillEllipse(star, x - r, y - r, r * 2, r * 2);
        }
    }

    /// <summary>
    /// A big full moon: a wide soft glow (a "path gradient": strongest at the
    /// center, fading to nothing at the edge), a pale disc, and a few slightly
    /// darker blotches for its seas. The blotches are clipped to the disc so
    /// one near the edge cannot spill onto the sky.
    /// </summary>
    private static void PaintMoon(Graphics g, float x, float y, float r, Random rng)
    {
        float glowR = r * 3.2f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - glowR, y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(190, 255, 232, 170),
                SurroundColors = [Color.FromArgb(0, 255, 210, 140)],
                Blend = Sprite.SoftFalloff,   // eases out, so the glow has no visible rim
            };
            g.FillPath(glow, glowPath);
        }
        using (var disc = new SolidBrush(Color.FromArgb(255, 244, 204)))
            g.FillEllipse(disc, x - r, y - r, r * 2, r * 2);

        GraphicsState before = g.Save();
        using (var discPath = new GraphicsPath())
        {
            discPath.AddEllipse(x - r, y - r, r * 2, r * 2);
            g.SetClip(discPath, CombineMode.Intersect);
        }
        using (var sea = new SolidBrush(Color.FromArgb(60, 196, 170, 120)))
            for (int i = 0; i < 7; i++)
            {
                float sx = x + r * 0.7f * ((float)rng.NextDouble() * 2 - 1), sy = y + r * 0.7f * ((float)rng.NextDouble() * 2 - 1);
                float sr = r * (0.12f + 0.2f * (float)rng.NextDouble());
                g.FillEllipse(sea, sx - sr, sy - sr * 0.8f, sr * 2, sr * 1.6f);
            }
        g.Restore(before);
    }

    /// <summary>A few long, thin, see-through streaks of cloud drifting across the moon's part of the sky.</summary>
    private static void PaintClouds(Graphics g, int w, int h, float u, Random rng)
    {
        using var cloud = new SolidBrush(Color.FromArgb(70, 34, 20, 58));
        for (int i = 0; i < 7; i++)
        {
            float cx = w * (0.35f + 0.6f * (float)rng.NextDouble()), cy = h * (0.14f + 0.28f * (float)rng.NextDouble());
            float cw = u * (0.25f + 0.35f * (float)rng.NextDouble()), ch = u * (0.012f + 0.016f * (float)rng.NextDouble());
            g.FillEllipse(cloud, cx - cw / 2, cy - ch / 2, cw, ch);
        }
    }

    // ================================================================ far away

    /// <summary>
    /// The haunted house, as a flat silhouette on the far hill: a main block
    /// with a steep roof, a taller tower with a witch-hat roof, a crooked
    /// chimney, and a few windows lit a sickly yellow. Each lit window gets a
    /// small soft glow painted first, underneath, so the window edge stays crisp.
    /// </summary>
    private static void PaintHouse(Graphics g, float x, float baseY, float u)
    {
        using var ink = new SolidBrush(Ink);
        float bodyW = u * 0.10f, bodyH = u * 0.065f;
        float top = baseY - bodyH;

        g.FillRectangle(ink, x - bodyW / 2, top, bodyW, bodyH);
        g.FillPolygon(ink, [new PointF(x - bodyW * 0.6f, top), new PointF(x, top - u * 0.05f), new PointF(x + bodyW * 0.6f, top)]);
        g.FillPolygon(ink, [                                                      // the chimney, leaning a little
            new PointF(x + bodyW * 0.22f, top - u * 0.01f), new PointF(x + bodyW * 0.26f, top - u * 0.055f),
            new PointF(x + bodyW * 0.36f, top - u * 0.053f), new PointF(x + bodyW * 0.34f, top)]);

        float towerW = u * 0.034f, towerH = u * 0.115f, towerX = x - bodyW * 0.5f;
        g.FillRectangle(ink, towerX - towerW / 2, baseY - towerH, towerW, towerH);
        g.FillPolygon(ink, [
            new PointF(towerX - towerW * 0.8f, baseY - towerH), new PointF(towerX, baseY - towerH - u * 0.06f),
            new PointF(towerX + towerW * 0.8f, baseY - towerH)]);

        // (center x, center y, width, height) of each lit window.
        (float X, float Y, float W, float H)[] windows =
        [
            (towerX, baseY - towerH * 0.78f, u * 0.010f, u * 0.016f),
            (x - bodyW * 0.08f, top + bodyH * 0.38f, u * 0.011f, u * 0.014f),
            (x + bodyW * 0.26f, top + bodyH * 0.38f, u * 0.011f, u * 0.014f),
            (x + bodyW * 0.05f, top - u * 0.018f, u * 0.008f, u * 0.008f),      // the attic
        ];
        foreach (var win in windows)
        {
            float glowR = u * 0.022f;
            using var glowPath = new GraphicsPath();
            glowPath.AddEllipse(win.X - glowR, win.Y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(110, 255, 190, 70),
                SurroundColors = [Color.FromArgb(0, 255, 170, 50)],
            };
            g.FillPath(glow, glowPath);
            using var lit = new SolidBrush(Color.FromArgb(255, 206, 96));
            g.FillRectangle(lit, win.X - win.W / 2, win.Y - win.H / 2, win.W, win.H);
        }
    }

    /// <summary>A band of pale mist lying in the valley at the foot of the hills, thickest right at the horizon.</summary>
    private static void PaintMist(Graphics g, int w, float horizon, float u)
    {
        float top = horizon - u * 0.07f, bottom = horizon + u * 0.08f;
        using var mist = new LinearGradientBrush(new PointF(0, top - 1), new PointF(0, bottom + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(0, 170, 150, 190), Color.FromArgb(120, 170, 150, 190), Color.FromArgb(0, 170, 150, 190)],
                Positions = [0f, 0.5f, 1f],
            },
        };
        g.FillRectangle(mist, 0, top, w, bottom - top);
    }

    // ================================================================ foreground

    /// <summary>
    /// The graveyard hill: one bank of ground running the full width of the
    /// screen, a little higher at the sides and dipping in the middle.
    /// Everything that stands on it asks it "how high is the ground here?".
    /// </summary>
    private static Bank MakeGround(int w, int h) => new(
    [
        new PointF(-2, h * 0.80f), new PointF(w * 0.15f, h * 0.79f), new PointF(w * 0.33f, h * 0.825f),
        new PointF(w * 0.50f, h * 0.845f), new PointF(w * 0.68f, h * 0.825f), new PointF(w * 0.86f, h * 0.79f),
        new PointF(w + 2, h * 0.80f),
    ], h);

    /// <summary>
    /// One tombstone: a slab with a rounded top (or a cross), leaning a few
    /// degrees because old stones never stand straight. To lean it, we move
    /// the paper so the stone's foot is at (0, 0), turn the paper, draw the
    /// stone upright, and put the paper back (Save / Restore).
    /// </summary>
    private static void PaintTombstone(Graphics g, float x, float footY, float u, float tiltDegrees, bool cross)
    {
        GraphicsState before = g.Save();
        g.TranslateTransform(x, footY);
        g.RotateTransform(tiltDegrees);

        using var stone = new SolidBrush(Color.FromArgb(58, 48, 78));
        using var edge = new Pen(Color.FromArgb(120, 170, 156, 196), Math.Max(1f, u * 0.002f));
        float sw = u * 0.030f, sh = u * 0.046f;
        if (cross)
        {
            float bar = sw * 0.3f;
            g.FillRectangle(stone, -bar / 2, -sh * 1.15f, bar, sh * 1.15f + u * 0.01f);
            g.FillRectangle(stone, -sw / 2, -sh * 0.9f, sw, bar);
            g.DrawLine(edge, bar / 2, -sh * 1.15f, bar / 2, -sh * 0.9f);        // the side the moon catches
        }
        else
        {
            using var slab = new GraphicsPath();
            slab.AddArc(-sw / 2, -sh, sw, sw, 180, 180);                         // the rounded top
            slab.AddLine(sw / 2, -sh + sw / 2, sw / 2, u * 0.01f);
            slab.AddLine(sw / 2, u * 0.01f, -sw / 2, u * 0.01f);
            slab.CloseFigure();
            g.FillPath(stone, slab);
            g.DrawArc(edge, -sw / 2, -sh, sw, sw, 270, 90);                      // moonlight on the top right corner
            g.DrawLine(edge, sw / 2, -sh + sw / 2, sw / 2, -sh * 0.2f);
        }
        g.Restore(before);
    }

    /// <summary>
    /// A dead tree, grown by the same branch rule the sakura scenes use, with
    /// the flowers switched off. It starts pointing up and a little left; the
    /// rule's built-in "sag" then bends it over to the right as it climbs,
    /// which is what gives it that hunched, reaching look.
    /// </summary>
    private static void PaintDeadTree(Graphics g, float x, float footY, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds = [new(new PointF(x, footY), -MathF.PI / 2 - 0.45f, u * 0.40f, u * 0.024f, 3)];
        Brushwork.Branches(g, seeds, u, rng, wood: Ink, petalLight: Ink, petalDeep: Ink, bare: true);
    }

    /// <summary>
    /// Autumn trees at both sides, each standing on the ground under it. They
    /// are the sakura trees (same trunk, same cloud of dots) in an orange
    /// palette. Returns random spots inside the canopies, so the falling
    /// leaves can let go of the trees they belong to.
    /// </summary>
    private static List<PointF> PaintAutumnTrees(Graphics g, int w, float u, Bank ground, Random rng)
    {
        var autumn = new BlossomPalette(Color.FromArgb(240, 190, 92, 28), Color.FromArgb(136, 46, 16), Color.FromArgb(248, 176, 56));

        // (X as a fraction of the width, canopy center height above the ground in u, canopy size in u)
        (float X, float Lift, float R)[] trees =
        [
            (0.02f, 0.15f, 0.14f), (0.13f, 0.12f, 0.11f), (0.90f, 0.13f, 0.12f), (0.99f, 0.15f, 0.14f),
        ];
        foreach (var t in trees)
            Brushwork.Trunk(g, w * t.X, ground.YAt(w * t.X) + u * 0.03f, ground.YAt(w * t.X) - u * t.Lift, u * t.R, rng, Ink);

        var spots = new List<PointF>();
        foreach (var t in trees)
        {
            float cx = w * t.X, cy = ground.YAt(cx) - u * t.Lift, r = u * t.R;
            Brushwork.Canopy(g, cx, cy, r, u, rng, autumn);
            for (int i = 0; i < 8; i++)
            {
                var spot = new PointF(cx + r * ((float)rng.NextDouble() * 2 - 1), cy + r * 0.5f * ((float)rng.NextDouble() * 2 - 1));
                if (spot.X >= 0 && spot.X < w) spots.Add(spot);
            }
        }
        return spots;
    }

    /// <summary>
    /// A jack-o'-lantern standing on the ground at (x, footY): three
    /// overlapping orange lobes (which is what gives a pumpkin its ribs), a
    /// stubby stem, and a carved face painted in bright candle yellow.
    /// Returns its center and size so the scene can flicker a glow over it.
    /// </summary>
    private static (PointF At, float R) PaintPumpkin(Graphics g, float x, float footY, float r)
    {
        float cy = footY - r * 0.8f;

        using (var stem = new SolidBrush(Color.FromArgb(64, 72, 34)))
            g.FillPolygon(stem, [
                new PointF(x - r * 0.10f, cy - r * 0.70f), new PointF(x - r * 0.02f, cy - r * 1.10f),
                new PointF(x + r * 0.16f, cy - r * 1.05f), new PointF(x + r * 0.12f, cy - r * 0.70f)]);

        using (var side = new SolidBrush(Color.FromArgb(204, 88, 18)))
        {
            g.FillEllipse(side, x - r * 1.0f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
            g.FillEllipse(side, x - r * 0.1f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
        }
        using (var middle = new SolidBrush(Color.FromArgb(234, 118, 28)))
            g.FillEllipse(middle, x - r * 0.55f, cy - r * 0.80f, r * 1.1f, r * 1.60f);

        // The face. Every point is (across, down) from the pumpkin's center, in units of r.
        PointF P(float ax, float ay) => new(x + r * ax, cy + r * ay);
        using var candle = new SolidBrush(Color.FromArgb(255, 222, 110));
        g.FillPolygon(candle, [P(-0.52f, -0.12f), P(-0.30f, -0.42f), P(-0.14f, -0.12f)]);   // left eye
        g.FillPolygon(candle, [P(0.14f, -0.12f), P(0.30f, -0.42f), P(0.52f, -0.12f)]);      // right eye
        g.FillPolygon(candle, [P(-0.08f, 0.10f), P(0f, -0.06f), P(0.08f, 0.10f)]);          // nose
        g.FillPolygon(candle, [                                                             // a jagged grin
            P(-0.62f, 0.16f), P(-0.40f, 0.26f), P(-0.30f, 0.38f), P(-0.18f, 0.27f), P(0.02f, 0.27f),
            P(0.12f, 0.39f), P(0.24f, 0.27f), P(0.40f, 0.26f), P(0.62f, 0.16f),
            P(0.36f, 0.50f), P(0f, 0.58f), P(-0.36f, 0.50f)]);

        return (new PointF(x, cy), r);
    }

    /// <summary>Bare branches reaching in from the top corners, like fingers. The sakura branches with the flowers switched off.</summary>
    private static void PaintCornerBranches(Graphics g, int w, int h, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.04f), 0.30f, u * 0.46f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.30f, u * 0.40f, u * 0.016f, 3),  // top right, reaching left
        ];
        Brushwork.Branches(g, seeds, u, rng, wood: Ink, petalLight: Ink, petalDeep: Ink, bare: true);
    }
}
