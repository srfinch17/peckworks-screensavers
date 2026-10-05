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
    public required PointF SkeletonShoulder { get; init; }           // where the waving arm hinges
    public required float SkeletonHeight { get; init; }              // its height in pixels, so the arm is drawn to scale

    // ---- Facts for the happenings (src/Halloween/Happenings): where things are, so a ghost
    //      can rise from a real tombstone and a face can appear in the real moon. ----
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float U { get; init; }                           // the size unit: the height, or less on a tall screen. Size everything by this.
    public required float Horizon { get; init; }                     // the y where the sky ends behind the hills
    public required (PointF At, float R) Moon { get; init; }         // the moon's center and radius
    public required bool[] OpenSky { get; init; }                    // stencil: true where nothing SOLID was painted over the sky, moon and clouds (the mist is haze, not solid: see Paint)
    public required bool[] OpenFromHouse { get; init; }              // stencil: true where nothing was painted after the haunted house (so: nothing is in front of it)
    public required PointF[] FarRidge { get; init; }                 // the far hill's outline; ask Brushwork.RidgeYAt(FarRidge, x) for its height
    public required Bank Ground { get; init; }                       // the graveyard hill; ask Ground.YAt(x) for its height
    public required PointF House { get; init; }                      // the haunted house: the middle of its base (pass to HalloweenPainter.PaintHouse)
    public required RectangleF[] HouseWindows { get; init; }         // its four lit windows; [0] is the tower's, [3] the attic's
    public required PointF HouseTowerTip { get; init; }              // the point of the tower's witch-hat roof
    public required PointF HouseChimneyTop { get; init; }            // the top of the crooked chimney
    public required List<(PointF Foot, float Tilt, bool Cross)> Tombstones { get; init; } // where each stone meets the ground, its lean in degrees, slab or cross
    public required SizeF TombstoneSize { get; init; }               // a slab's width and height
    public required PointF SkeletonFoot { get; init; }               // the spot between the skeleton's feet (see PaintSkeleton for how its points are measured)
    public required PointF DeadTreeFoot { get; init; }               // where the dead tree's trunk meets the ground
    public required List<PointF> DeadTreeSpots { get; init; }        // points on the dead tree's thin branches and at its tips
    public required List<PointF> CornerBranchSpots { get; init; }    // the same for the bare branches in the top corners
    public required List<(PointF At, float R)> Canopies { get; init; } // each autumn tree's cloud of leaves: center and size
}

/// <summary>
/// Paints the backdrop: a purple night sky with a big full moon and a few
/// streaks of cloud, two ranges of dark hills with a haunted house on the far
/// one, a band of mist, a graveyard hill with tombstones, a dead tree, autumn
/// trees in orange leaf, jack-o'-lanterns, a waving skeleton, and bare
/// branches clawing in from the top corners.
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
        PaintMoon(g, w * 0.70f, h * 0.27f, u * 0.12f, rng);   // (the same numbers as "moon" a few lines down)
        PaintClouds(g, w, h, u, rng);

        // Stencils for the happenings (the same trick as Santa's sleigh in
        // Christmas): copy the picture now, compare at the very end, and any
        // pixel that did not change still has nothing in front of it. A
        // firework stamped "only where the sky is open" then goes behind the
        // hills and branches without knowing where they are.
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);
        var moon = (At: new PointF(w * 0.70f, h * 0.27f), R: u * 0.12f);

        PointF[] farRidge = Brushwork.Ridge(w, horizon, u, 0.07f, 0.05f, 0.015f, 0.06f, rng);
        using (var far = new SolidBrush(Color.FromArgb(44, 26, 66)))
            g.FillPolygon(far, farRidge);
        var house = new PointF(w * 0.26f, Brushwork.RidgeYAt(farRidge, w * 0.26f) + u * 0.012f);
        PaintHouse(g, house.X, house.Y, u);
        g.Flush();
        uint[] upToHouse = Brushwork.ToPixels(bmp);   // a second copy: "unchanged since here" = nothing stands in front of the house

        PointF[] nearRidge = Brushwork.Ridge(w, horizon, u, 0.02f, 0.03f, 0.012f, 0.03f, rng);
        using (var near = new SolidBrush(Color.FromArgb(28, 16, 44)))
        {
            g.FillPolygon(near, nearRidge);
            g.FillRectangle(near, 0, horizon, w, h - horizon);   // the valley floor, down to where the graveyard hill covers it
        }
        // The mist is haze, not a solid thing, so it must not count as
        // "something in front of the sky". (It did at first. The far hill is
        // often lower than the top of the mist, and a monster rising behind
        // the hill was then cut off in a ruler-straight line along the top
        // of the mist, with open sky between it and the hill.) So we take a
        // copy just before the mist and another just after: "sky" is judged
        // up to the first, "nothing in front" from the second onward.
        g.Flush();
        uint[] beforeMist = Brushwork.ToPixels(bmp);
        PaintMist(g, w, horizon, u);
        g.Flush();
        uint[] afterMist = Brushwork.ToPixels(bmp);

        Bank ground = MakeGround(w, h);
        ground.Paint(g, Color.FromArgb(40, 28, 56), Color.FromArgb(10, 6, 16), Color.FromArgb(110, 130, 104, 150), Math.Max(1.5f, u * 0.004f));

        (float X, float Tilt, bool Cross)[] stones =
            [(0.41f, -6, false), (0.47f, 4, true), (0.535f, -3, false), (0.60f, 8, false), (0.655f, -5, true)];
        var tombstones = new List<(PointF Foot, float Tilt, bool Cross)>();
        foreach (var s in stones)
        {
            var foot = new PointF(w * s.X, ground.YAt(w * s.X) + u * 0.006f);
            PaintTombstone(g, foot.X, foot.Y, u, s.Tilt, s.Cross);
            tombstones.Add((foot, s.Tilt, s.Cross));
        }

        var deadTreeFoot = new PointF(w * 0.565f, ground.YAt(w * 0.565f) + u * 0.01f);
        List<PointF> deadTreeSpots = PaintDeadTree(g, deadTreeFoot.X, deadTreeFoot.Y, u, rng);
        var (leafSpots, canopies) = PaintAutumnTrees(g, w, u, ground, rng);

        var pumpkins = new List<(PointF At, float R)>();
        (float X, float R)[] patch = [(0.30f, 0.030f), (0.345f, 0.020f), (0.73f, 0.024f), (0.775f, 0.032f)];
        foreach (var p in patch)
            pumpkins.Add(PaintPumpkin(g, w * p.X, ground.YAt(w * p.X) + u * 0.012f, u * p.R));

        float skeletonX = w * 0.225f, skeletonFoot = ground.YAt(skeletonX) + u * 0.006f, skeletonTall = u * 0.14f;
        PaintSkeleton(g, skeletonX, skeletonFoot, skeletonTall);

        List<PointF> cornerSpots = PaintCornerBranches(g, w, h, u, rng);

        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);

        // Open sky = still sky when the hills were done (nothing solid behind
        // the mist) AND untouched since the mist (nothing in front of it).
        // The price of leaving the mist out: a stamp low on the horizon is
        // drawn over the haze there instead of under it. That is a faint
        // difference in a thin band; the flat cut was not faint.
        var openSky = new bool[pixels.Length];
        for (int i = 0; i < openSky.Length; i++)
            openSky[i] = skyOnly[i] == beforeMist[i] && afterMist[i] == pixels[i];

        return new HalloweenScenery
        {
            Pixels = pixels, LeafSpots = leafSpots, Pumpkins = pumpkins,
            SkeletonShoulder = new PointF(skeletonX + skeletonTall * 0.105f, skeletonFoot - skeletonTall * 0.765f),
            SkeletonHeight = skeletonTall,

            Width = w, Height = h, U = u, Horizon = horizon, Moon = moon,
            OpenSky = openSky, OpenFromHouse = Brushwork.Unchanged(upToHouse, pixels),
            FarRidge = farRidge, Ground = ground,
            House = house, HouseWindows = HouseWindows(house.X, house.Y, u),
            HouseTowerTip = new PointF(house.X - u * 0.05f, house.Y - u * 0.175f),
            HouseChimneyTop = new PointF(house.X + u * 0.031f, house.Y - u * 0.119f),
            Tombstones = tombstones, TombstoneSize = new SizeF(u * 0.030f, u * 0.046f),
            SkeletonFoot = new PointF(skeletonX, skeletonFoot),
            DeadTreeFoot = deadTreeFoot, DeadTreeSpots = deadTreeSpots, CornerBranchSpots = cornerSpots, Canopies = canopies,
        };
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
    ///
    /// "lights" lets a happening repaint the house onto a sprite with its
    /// windows in another state: one color per window (same order as
    /// HouseWindows), where null means that window is dark. Leave "lights"
    /// out and every window is the usual yellow.
    /// </summary>
    internal static void PaintHouse(Graphics g, float x, float baseY, float u, Color?[]? lights = null)
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

        // The glows are kept ON the walls: a "clip" tells the drawing kit
        // "only paint inside this shape", and the shape here is the house
        // itself. Without it the tower window's glow, which is wider than the
        // tower, spilled a faint yellow smudge onto the sky either side, and
        // that smudge stayed lit when a happening turned the window dark.
        // (Intersect, between Save and Restore: add to the caller's clip,
        // never replace it.)
        GraphicsState before = g.Save();
        using (var walls = new Region(new RectangleF(x - bodyW / 2, top, bodyW, bodyH)))
        {
            walls.Union(new RectangleF(towerX - towerW / 2, baseY - towerH, towerW, towerH));
            using var roofs = new GraphicsPath();
            roofs.AddPolygon([new PointF(x - bodyW * 0.6f, top), new PointF(x, top - u * 0.05f), new PointF(x + bodyW * 0.6f, top)]);
            walls.Union(roofs);
            using var hat = new GraphicsPath();
            hat.AddPolygon([
                new PointF(towerX - towerW * 0.8f, baseY - towerH), new PointF(towerX, baseY - towerH - u * 0.06f),
                new PointF(towerX + towerW * 0.8f, baseY - towerH)]);
            walls.Union(hat);
            g.SetClip(walls, CombineMode.Intersect);
        }

        Color usual = Color.FromArgb(255, 206, 96);
        RectangleF[] windows = HouseWindows(x, baseY, u);
        for (int i = 0; i < windows.Length; i++)
        {
            Color? light = lights == null ? usual : lights[i];
            if (light is not Color lit) continue;                                   // this window is dark
            bool isUsual = lit.ToArgb() == usual.ToArgb();
            RectangleF win = windows[i];
            float cx = win.X + win.Width / 2, cy = win.Y + win.Height / 2, glowR = u * 0.022f;
            using var glowPath = new GraphicsPath();
            glowPath.AddEllipse(cx - glowR, cy - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                // The usual yellow keeps its own hand-picked glow colors. Any
                // other light gets a glow a shade deeper than its pane.
                CenterColor = isUsual ? Color.FromArgb(110, 255, 190, 70) : Color.FromArgb(110, Brushwork.Mix(lit, Color.FromArgb(255, 170, 40), 0.35f)),
                SurroundColors = [isUsual ? Color.FromArgb(0, 255, 170, 50) : Color.FromArgb(0, lit)],
            };
            g.FillPath(glow, glowPath);
            using var pane = new SolidBrush(lit);
            g.FillRectangle(pane, win);
        }
        g.Restore(before);
    }

    /// <summary>
    /// Where the haunted house's four windows are, for a house whose base is
    /// centered at (x, baseY): [0] the tower, [1] and [2] the main floor,
    /// [3] the attic. The sizes here must match PaintHouse above.
    /// </summary>
    internal static RectangleF[] HouseWindows(float x, float baseY, float u)
    {
        float bodyW = u * 0.10f, bodyH = u * 0.065f, top = baseY - bodyH;
        float towerH = u * 0.115f, towerX = x - bodyW * 0.5f;
        RectangleF Window(float cx, float cy, float ww, float wh) => new(cx - ww / 2, cy - wh / 2, ww, wh);
        return
        [
            Window(towerX, baseY - towerH * 0.78f, u * 0.010f, u * 0.016f),
            Window(x - bodyW * 0.08f, top + bodyH * 0.38f, u * 0.011f, u * 0.014f),
            Window(x + bodyW * 0.26f, top + bodyH * 0.38f, u * 0.011f, u * 0.014f),
            Window(x + bodyW * 0.05f, top - u * 0.018f, u * 0.008f, u * 0.008f),
        ];
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
    private static List<PointF> PaintDeadTree(Graphics g, float x, float footY, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds = [new(new PointF(x, footY), -MathF.PI / 2 - 0.45f, u * 0.40f, u * 0.024f, 3)];
        return Brushwork.Branches(g, seeds, u, rng, wood: Ink, petalLight: Ink, petalDeep: Ink, bare: true);
    }

    /// <summary>
    /// Autumn trees at both sides, each standing on the ground under it. They
    /// are the sakura trees (same trunk, same cloud of dots) in an orange
    /// palette. Returns random spots inside the canopies, so the falling
    /// leaves can let go of the trees they belong to, and each canopy's
    /// center and size.
    /// </summary>
    private static (List<PointF> LeafSpots, List<(PointF At, float R)> Canopies) PaintAutumnTrees(Graphics g, int w, float u, Bank ground, Random rng)
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
        var canopies = new List<(PointF At, float R)>();
        foreach (var t in trees)
        {
            float cx = w * t.X, cy = ground.YAt(cx) - u * t.Lift, r = u * t.R;
            canopies.Add((new PointF(cx, cy), r));
            Brushwork.Canopy(g, cx, cy, r, u, rng, autumn);
            for (int i = 0; i < 8; i++)
            {
                var spot = new PointF(cx + r * ((float)rng.NextDouble() * 2 - 1), cy + r * 0.5f * ((float)rng.NextDouble() * 2 - 1));
                if (spot.X >= 0 && spot.X < w) spots.Add(spot);
            }
        }
        return (spots, canopies);
    }

    /// <summary>
    /// A jack-o'-lantern standing on the ground at (x, footY): three
    /// overlapping orange lobes (which is what gives a pumpkin its ribs), a
    /// stubby stem, and a carved face painted in bright candle yellow.
    /// Returns its center and size so the scene can flicker a glow over it.
    /// </summary>
    private static (PointF At, float R) PaintPumpkin(Graphics g, float x, float footY, float r)
    {
        float cy = PaintPumpkinBody(g, x, footY, r).Y;

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

    /// <summary>
    /// The pumpkin with no face: stem and three lobes, standing at (x, footY).
    /// Split out so a happening can paint the same pumpkin onto a sprite and
    /// carve a different face on it. Returns the center the face is measured
    /// from (the same point the Pumpkins list holds). It reaches 1.0 r left
    /// and right of the center, 1.1 r above it and 0.8 r below.
    /// </summary>
    internal static PointF PaintPumpkinBody(Graphics g, float x, float footY, float r)
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
        return new PointF(x, cy);
    }

    /// <summary>
    /// A skeleton standing on the ground at (x, footY), "tall" pixels high,
    /// waving one arm. It is a stick figure drawn in bone color: every bone
    /// is one thick line with rounded ends, plus a skull, a rib cage of four
    /// U-shaped curves, and a pelvis.
    ///
    /// Every point below is (across, up) from the spot between its feet, as
    /// a fraction of its height, so 0.9 up is the middle of the skull. The
    /// little helper P turns those into real pixels. That keeps the whole
    /// figure in proportion at any size.
    /// </summary>
    private static void PaintSkeleton(Graphics g, float x, float footY, float tall)
    {
        PointF P(float across, float up) => new(x + tall * across, footY - tall * up);
        Color boneColor = Color.FromArgb(232, 226, 204);
        using var bone = new Pen(boneColor, Math.Max(1.5f, tall * 0.022f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var thin = new Pen(boneColor, Math.Max(1f, tall * 0.014f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new SolidBrush(boneColor);
        using var hollow = new SolidBrush(Ink);

        // Legs: hip, knee, ankle, then a short foot pointing outward.
        foreach (int side in (int[])[-1, 1])
            g.DrawLines(bone, [P(side * 0.045f, 0.43f), P(side * 0.065f, 0.22f), P(side * 0.055f, 0.01f), P(side * 0.11f, 0f)]);

        // Pelvis, spine, collar bones.
        g.FillEllipse(fill, x - tall * 0.075f, footY - tall * 0.47f, tall * 0.15f, tall * 0.07f);
        g.DrawLine(bone, P(0, 0.44f), P(0, 0.79f));
        g.DrawLine(bone, P(-0.105f, 0.765f), P(0.105f, 0.765f));

        // Ribs: four U shapes hanging from the spine, narrower toward the waist.
        (float Up, float HalfWidth)[] ribs = [(0.72f, 0.085f), (0.67f, 0.092f), (0.62f, 0.085f), (0.57f, 0.07f)];
        foreach (var r in ribs)
            g.DrawBezier(thin, P(-r.HalfWidth, r.Up), P(-r.HalfWidth, r.Up - 0.035f), P(r.HalfWidth, r.Up - 0.035f), P(r.HalfWidth, r.Up));

        // The left arm hangs, with three fingers at the hand. The right arm
        // is NOT painted here: it waves, so the scene stamps it every frame
        // (see HalloweenScene.PaintArm), hinged at this shoulder.
        g.DrawLines(bone, [P(-0.105f, 0.765f), P(-0.155f, 0.60f), P(-0.135f, 0.44f)]);
        foreach (float finger in (float[])[-0.025f, 0f, 0.025f])
            g.DrawLine(thin, P(-0.135f, 0.44f), P(-0.135f + finger, 0.395f));

        // Skull: a round top, a narrower jaw, then the dark hollows that make it a face.
        g.FillEllipse(fill, x - tall * 0.078f, footY - tall * 0.99f, tall * 0.156f, tall * 0.16f);
        g.FillRectangle(fill, x - tall * 0.048f, footY - tall * 0.86f, tall * 0.096f, tall * 0.05f);
        g.FillEllipse(hollow, x - tall * 0.052f, footY - tall * 0.93f, tall * 0.04f, tall * 0.046f);   // left eye socket
        g.FillEllipse(hollow, x + tall * 0.012f, footY - tall * 0.93f, tall * 0.04f, tall * 0.046f);   // right eye socket
        g.FillPolygon(hollow, [P(0, 0.885f), P(-0.012f, 0.86f), P(0.012f, 0.86f)]);                    // nose
        using var gap = new Pen(Ink, Math.Max(1f, tall * 0.008f));
        g.DrawLine(gap, P(-0.04f, 0.835f), P(0.04f, 0.835f));                                           // the line between the teeth
        foreach (float tooth in (float[])[-0.024f, -0.008f, 0.008f, 0.024f])
            g.DrawLine(gap, P(tooth, 0.85f), P(tooth, 0.82f));
    }

    /// <summary>Bare branches reaching in from the top corners, like fingers. The sakura branches with the flowers switched off.</summary>
    private static List<PointF> PaintCornerBranches(Graphics g, int w, int h, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.04f), 0.30f, u * 0.46f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.30f, u * 0.40f, u * 0.016f, 3),  // top right, reaching left
        ];
        return Brushwork.Branches(g, seeds, u, rng, wood: Ink, petalLight: Ink, petalDeep: Ink, bare: true);
    }
}
