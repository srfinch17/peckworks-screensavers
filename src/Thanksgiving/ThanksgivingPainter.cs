using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Thanksgiving;

/// <summary>The finished backdrop: a still picture plus a few facts the animation needs.</summary>
internal sealed class ThanksgivingScenery
{
    public required uint[] Pixels { get; init; }                     // the painted picture, same layout as a FrameBuffer
    public required List<PointF> LeafSpots { get; init; }            // places in the maples and the corner branches that leaves can drop from

    // ---- Facts for the happenings (src/Thanksgiving/Happenings): where things are, so a turkey
    //      can walk on the real ground and a crow can land on the real scarecrow. ----
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float U { get; init; }                           // the size unit: the height, or less on a tall screen. Size everything by this.
    public required float Horizon { get; init; }                     // the y where the sky ends behind the far hills
    public required (PointF At, float R) Moon { get; init; }         // the harvest moon's center and radius
    public required bool[] OpenSky { get; init; }                    // stencil: true where nothing was painted over the sky, moon and clouds
    public required bool[] OpenFromFarm { get; init; }               // stencil: true where nothing was painted after the farmhouse and barn (so: nothing stands in front of them)
    public required bool[] OpenField { get; init; }                  // stencil: true where nothing was painted after the harvested field and its bales (the tractor drives on the field, behind the scarecrow, the fence and the near ground)
    public required bool[] OpenOfBranches { get; init; }             // stencil: true where the corner branches did NOT paint (for things in front of everything but those branches)
    public required PointF[] FarRidge { get; init; }                 // the far hills' outline; ask Brushwork.RidgeYAt(FarRidge, x) for their height
    public required PointF[] NearRidge { get; init; }                // the nearer hills' outline, where the farm stands
    public required Bank Ground { get; init; }                       // the near ground; Ground.YAt(x) is its crest, the line it makes against the field

    /// <summary>
    /// Where to STAND on the near ground at x: a little way down from its
    /// crest. The crest (Ground.YAt) is the FAR edge of the ground; the fence,
    /// the maples and the scarecrow are planted just in front of it. A walker
    /// on this line is nearer than all of them, so it is drawn in front of
    /// them, which is where its feet say it is.
    /// </summary>
    public float WalkY(float x) => Ground.YAt(Math.Clamp(x, 0, Width - 1)) + U * 0.052f;

    /// <summary>
    /// The lane across the far field at x, for things that drive on it (the
    /// tractor): a little over half way from the ground's crest back to the
    /// near hills. The scarecrow stands nearer than this (0.3 of the way),
    /// so a tractor on the lane passes BEHIND it (use the OpenField stencil).
    /// </summary>
    public float FieldY(float x) => Brushwork.RidgeYAt(NearRidge, Math.Clamp(x, 0, Width - 1)) * 0.55f + Ground.YAt(Math.Clamp(x, 0, Width - 1)) * 0.45f;

    // ---- The farmhouse (on the near hills, left of middle) ----
    public required PointF FarmFoot { get; init; }                   // the middle of the farmhouse's base
    public required SizeF FarmWall { get; init; }                    // its wall's width and height (the gable roof rises 0.05 U above the wall)
    public required RectangleF[] FarmWindows { get; init; }          // its three warm windows: [0] and [1] downstairs, [2] in the gable
    public required RectangleF FarmDoor { get; init; }
    public required PointF PorchLight { get; init; }                 // the lamp beside the door, under the porch roof
    public required PointF ChimneyTop { get; init; }                 // where the smoke comes out

    // ---- The barn and silo (on the near hills, right of middle) ----
    public required PointF BarnFoot { get; init; }                   // the middle of the barn's base
    public required SizeF BarnWall { get; init; }                    // its wall's width and height (the roof rises 0.045 U above the wall)
    public required RectangleF BarnLoft { get; init; }               // the loft window, high in the gable
    public required RectangleF BarnDoor { get; init; }

    // ---- The field ----
    public required List<(PointF At, float R)> HayBales { get; init; } // each round bale: center and radius, standing on the field

    // ---- The near ground and what stands on it ----
    public required float[] FencePosts { get; init; }                // the x of every fence post
    public float FenceFootY(float x) => Ground.YAt(Math.Clamp(x, 0, Width - 1)) + U * 0.03f;   // where a post meets the ground
    public float FencePostHeight => U * 0.075f;
    public float RailY(float x, int rail) => FenceFootY(x) - FencePostHeight * (rail == 0 ? 0.36f : 0.70f);   // the two rails: 0 the lower, 1 the upper
    public required PointF ScarecrowFoot { get; init; }              // where its pole meets the ground
    public required float ScarecrowHeight { get; init; }             // pole foot to hat top, in pixels
    public required PointF ScarecrowHead { get; init; }              // the middle of its burlap head
    public required PointF ScarecrowNeck { get; init; }              // where the scarf is tied (the scene stamps the scarf here)
    public required PointF ScarecrowArmL { get; init; }              // the ends of the crossbar: left, and right
    public required PointF ScarecrowArmR { get; init; }
    public required PointF CornShockFoot { get; init; }              // the corn shock: where it stands, its top, and its widest half width
    public required PointF CornShockTop { get; init; }
    public required float CornShockHalfWidth { get; init; }
    public required List<(PointF At, float R)> Pumpkins { get; init; } // the pumpkin patch: each pumpkin's center and size
    public required PointF CornucopiaMouth { get; init; }            // the open end of the horn (it faces right), on the ground
    public required PointF CornucopiaCenter { get; init; }
    public required float CornucopiaLength { get; init; }
    public required List<(PointF At, float R)> Canopies { get; init; } // each maple's cloud of leaves: center and size
    public required List<PointF> CornerBranchSpots { get; init; }    // points on the corner branches' twigs and tips

    // ---- Live facts, set by the scene every frame before the happenings draw ----
    public PointF GeeseLead { get; set; }                            // where the lead goose is
    public bool GeeseOnScreen { get; set; }                          // false while the V is on its off-screen pause
}

/// <summary>
/// Paints the backdrop: a harvest dusk (plum overhead, gold at the horizon),
/// a big peach harvest moon, two ranges of hills with a white farmhouse and a
/// red barn on the nearer one, a reaped field in gold stripes with round hay
/// bales, a rail fence along the near ground, a scarecrow, a corn shock, a
/// pumpkin patch, a cornucopia spilling fruit, maples in full autumn colour
/// at both sides, and oak branches reaching in from the top corners.
///
/// Like the Halloween and Christmas painters, this runs ONCE at startup (the
/// scenery never moves) and paints back to front: whatever is farthest away
/// goes down first, and each nearer thing simply covers what is behind it.
/// The trees, the ground, the hills and the branches come from the engine's
/// shared Brushwork helpers, so this scene has the same hand as the others.
///
/// Stencils for the animation: at four points in the paint order a copy of
/// the picture is kept, and at the end each copy is compared to the finished
/// picture. A pixel that did not change since a copy still has nothing in
/// front of it from that point on. The geese are only ever drawn on sky
/// pixels, and that is the whole trick that makes them pass BEHIND the
/// corner branches.
///
/// Positions are fractions of the width (w) or height (h). Sizes are
/// fractions of "u": the height on a normal wide screen, or less on a tall
/// one, so trees and the moon do not swallow a portrait monitor.
/// </summary>
internal static class ThanksgivingPainter
{
    private static readonly Color Wood = Color.FromArgb(78, 52, 34);         // fence rails, the scarecrow's pole, the oak branches
    private static readonly Color WindowWarm = Color.FromArgb(255, 204, 112); // every lit window

    public static ThanksgivingScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float horizon = h * 0.70f;
        float u = Math.Min(h, w * 9f / 16f);

        // ---- The paint order, back to front. Keep this list explicit. ----
        PaintSky(g, w, h, horizon, u, rng);
        var moon = (At: new PointF(w * 0.74f, h * 0.26f), R: u * 0.075f);
        PaintMoon(g, moon.At.X, moon.At.Y, moon.R);
        PaintClouds(g, w, h, u, rng);
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);   // the copy the sky stencil is made from (see the class summary)

        PointF[] farRidge = Brushwork.Ridge(w, horizon, u, 0.08f, 0.05f, 0.02f, 0.07f, rng);
        using (var far = new SolidBrush(Color.FromArgb(112, 62, 84)))
            g.FillPolygon(far, farRidge);
        PointF[] nearRidge = Brushwork.Ridge(w, horizon, u, 0.025f, 0.03f, 0.012f, 0.035f, rng);
        using (var near = new SolidBrush(Color.FromArgb(126, 78, 54)))
        {
            g.FillPolygon(near, nearRidge);
            g.FillRectangle(near, 0, horizon, w, h - horizon);   // the valley floor, down to where the field covers it
        }

        PaintFieldFloor(g, w, h, u, horizon);

        // The farm stands on the nearer hills, each building's foot a touch below the ridge line so it sits ON the hill.
        // (After the field's floor: a foot that is below the horizon stands on the field, not under it.)
        var farmFoot = new PointF(w * 0.31f, Brushwork.RidgeYAt(nearRidge, w * 0.31f) + u * 0.012f);
        PaintFarmhouse(g, farmFoot.X, farmFoot.Y, u);
        var barnFoot = new PointF(w * 0.60f, Brushwork.RidgeYAt(nearRidge, w * 0.60f) + u * 0.012f);
        PaintBarn(g, barnFoot.X, barnFoot.Y, u);
        g.Flush();
        uint[] upToFarm = Brushwork.ToPixels(bmp);   // a second copy: "unchanged since here" = nothing stands in front of the farm

        Bank ground = MakeGround(w, h);
        List<(PointF At, float R)> bales = PaintBales(g, w, u, nearRidge, ground, rng);
        g.Flush();
        uint[] upToField = Brushwork.ToPixels(bmp);  // a third copy: for things that move on the field (the tractor), behind the scarecrow, the fence and the near ground

        // The scarecrow stands IN the field, behind the fence, where a scarecrow
        // belongs: nearer than the tractor's lane but before the near ground. It
        // is painted AFTER the field copy, so the OpenField stencil hides a
        // tractor behind it (the first build took the copy after the scarecrow,
        // and the tractor drove across its legs).
        float scarecrowX = w * 0.40f, scarecrowTall = u * 0.20f;
        float scarecrowFoot = Brushwork.RidgeYAt(nearRidge, scarecrowX) * 0.30f + ground.YAt(scarecrowX) * 0.70f;
        PaintScarecrow(g, scarecrowX, scarecrowFoot, scarecrowTall, u);

        ground.Paint(g, Color.FromArgb(178, 124, 56), Color.FromArgb(100, 60, 30), Color.FromArgb(150, 214, 166, 84), Math.Max(1.5f, u * 0.004f));
        PaintGrass(g, w, u, ground, rng);

        float[] posts = PaintFence(g, w, u, ground);

        var cornucopiaMouth = new PointF(w * 0.215f, ground.YAt(w * 0.215f) + u * 0.055f);
        float hornLen = u * 0.11f;
        PaintCornucopia(g, cornucopiaMouth.X, cornucopiaMouth.Y, hornLen, u);

        var shockFoot = new PointF(w * 0.555f, ground.YAt(w * 0.555f) + u * 0.052f);
        float shockTall = u * 0.15f, shockHalf = u * 0.045f;
        PaintCornShock(g, shockFoot.X, shockFoot.Y, shockTall, shockHalf, rng);

        var pumpkins = new List<(PointF At, float R)>();
        (float X, float R, float Down)[] patch = [(0.66f, 0.030f, 0.046f), (0.71f, 0.022f, 0.056f), (0.755f, 0.034f, 0.050f), (0.81f, 0.026f, 0.058f)];
        PaintVines(g, w, u, ground, patch, rng);
        foreach (var p in patch)
            pumpkins.Add(PaintPumpkin(g, w * p.X, ground.YAt(w * p.X) + u * p.Down, u * p.R));
        PaintGourds(g, w, u, ground);

        var (leafSpots, canopies) = PaintMaples(g, w, u, ground, rng);

        g.Flush();
        uint[] upToBranches = Brushwork.ToPixels(bmp);  // a fourth copy: "unchanged since here" = no corner branch in front
        List<PointF> cornerSpots = PaintCornerBranches(g, w, h, u, rng);
        // Leaves can let go of the corner branches too, not only the maples.
        foreach (PointF spot in cornerSpots)
            if (spot.X >= 0 && spot.X < w && spot.Y >= 0 && spot.Y < h) leafSpots.Add(spot);

        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);

        float wallW = u * 0.12f, wallH = u * 0.058f, farmTop = farmFoot.Y - wallH;
        float barnW = u * 0.13f, barnH = u * 0.06f, barnTop = barnFoot.Y - barnH;
        return new ThanksgivingScenery
        {
            Pixels = pixels, LeafSpots = leafSpots,
            Width = w, Height = h, U = u, Horizon = horizon, Moon = moon,
            OpenSky = Brushwork.Unchanged(skyOnly, pixels),
            OpenFromFarm = Brushwork.Unchanged(upToFarm, pixels),
            OpenField = Brushwork.Unchanged(upToField, pixels),
            OpenOfBranches = Brushwork.Unchanged(upToBranches, pixels),
            FarRidge = farRidge, NearRidge = nearRidge, Ground = ground,
            FarmFoot = farmFoot, FarmWall = new SizeF(wallW, wallH),
            FarmWindows = FarmWindows(farmFoot.X, farmFoot.Y, u),
            FarmDoor = new RectangleF(farmFoot.X + wallW * 0.02f, farmFoot.Y - wallH * 0.62f, u * 0.014f, wallH * 0.62f),
            PorchLight = new PointF(farmFoot.X + wallW * 0.02f + u * 0.020f, farmFoot.Y - wallH * 0.52f),
            ChimneyTop = new PointF(farmFoot.X - wallW * 0.30f, farmTop - u * 0.072f),
            BarnFoot = barnFoot, BarnWall = new SizeF(barnW, barnH),
            BarnLoft = new RectangleF(barnFoot.X - u * 0.009f, barnTop - u * 0.026f, u * 0.018f, u * 0.016f),
            BarnDoor = new RectangleF(barnFoot.X - barnW * 0.16f, barnFoot.Y - barnH * 0.72f, barnW * 0.32f, barnH * 0.72f),
            HayBales = bales,
            FencePosts = posts,
            ScarecrowFoot = new PointF(scarecrowX, scarecrowFoot), ScarecrowHeight = scarecrowTall,
            ScarecrowHead = new PointF(scarecrowX, scarecrowFoot - scarecrowTall * 0.80f),
            ScarecrowNeck = new PointF(scarecrowX + scarecrowTall * 0.045f, scarecrowFoot - scarecrowTall * 0.70f),
            ScarecrowArmL = new PointF(scarecrowX - scarecrowTall * 0.30f, scarecrowFoot - scarecrowTall * 0.62f),
            ScarecrowArmR = new PointF(scarecrowX + scarecrowTall * 0.30f, scarecrowFoot - scarecrowTall * 0.62f),
            CornShockFoot = shockFoot, CornShockTop = new PointF(shockFoot.X, shockFoot.Y - shockTall), CornShockHalfWidth = shockHalf,
            Pumpkins = pumpkins,
            CornucopiaMouth = cornucopiaMouth, CornucopiaCenter = new PointF(cornucopiaMouth.X - hornLen * 0.45f, cornucopiaMouth.Y - hornLen * 0.22f),
            CornucopiaLength = hornLen,
            Canopies = canopies, CornerBranchSpots = cornerSpots,
        };
    }

    // ================================================================ sky

    /// <summary>
    /// A harvest dusk: deep plum overhead, through rose, to amber and then a
    /// band of gold right at the horizon where the sun has just gone down.
    /// A few faint stars are out in the darker top third.
    /// </summary>
    private static void PaintSky(Graphics g, int w, int h, float horizon, float u, Random rng)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(40, 24, 62), Color.FromArgb(112, 52, 84), Color.FromArgb(204, 110, 72), Color.FromArgb(246, 184, 92)],
                Positions = [0f, 0.40f, 0.78f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        for (int i = 0; i < 90; i++)
        {
            float x = (float)rng.NextDouble() * w, y = (float)Math.Pow(rng.NextDouble(), 1.8) * horizon * 0.5f;
            float r = Math.Max(0.6f, u * (0.0008f + 0.0012f * (float)rng.NextDouble()));
            using var star = new SolidBrush(Color.FromArgb(50 + rng.Next(120), 255, 240, 220));
            g.FillEllipse(star, x - r, y - r, r * 2, r * 2);
        }
    }

    /// <summary>
    /// The harvest moon: big, low and peach coloured, the way a moon looks
    /// through the thick evening air near the horizon, with a wide soft
    /// amber glow around it (a "path gradient": strongest at the center,
    /// nothing at the edge) and a few faint blotches for its seas.
    /// </summary>
    private static void PaintMoon(Graphics g, float x, float y, float r)
    {
        float glowR = r * 3.4f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - glowR, y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(150, 255, 200, 130),
                SurroundColors = [Color.FromArgb(0, 255, 180, 110)],
                Blend = Sprite.SoftFalloff,   // eases out, so the glow has no visible rim
            };
            g.FillPath(glow, glowPath);
        }
        using (var disc = new SolidBrush(Color.FromArgb(255, 220, 164)))
            g.FillEllipse(disc, x - r, y - r, r * 2, r * 2);

        GraphicsState before = g.Save();
        using (var discPath = new GraphicsPath())
        {
            discPath.AddEllipse(x - r, y - r, r * 2, r * 2);
            g.SetClip(discPath, CombineMode.Intersect);
        }
        using (var sea = new SolidBrush(Color.FromArgb(48, 200, 140, 90)))
        {
            g.FillEllipse(sea, x - r * 0.55f, y - r * 0.45f, r * 0.6f, r * 0.45f);
            g.FillEllipse(sea, x - r * 0.1f, y - r * 0.1f, r * 0.7f, r * 0.5f);
            g.FillEllipse(sea, x - r * 0.4f, y + r * 0.25f, r * 0.45f, r * 0.35f);
        }
        g.Restore(before);
    }

    /// <summary>A few long, thin streaks of cloud lit from below by the sunset: rose gold, and see-through.</summary>
    private static void PaintClouds(Graphics g, int w, int h, float u, Random rng)
    {
        for (int i = 0; i < 8; i++)
        {
            float cx = w * (0.05f + 0.9f * (float)rng.NextDouble()), cy = h * (0.22f + 0.36f * (float)rng.NextDouble());
            float cw = u * (0.22f + 0.40f * (float)rng.NextDouble()), ch = u * (0.010f + 0.014f * (float)rng.NextDouble());
            // Lower clouds are nearer the gold band, so they are lit warmer and brighter.
            float low = (cy - h * 0.22f) / (h * 0.36f);
            using var cloud = new SolidBrush(Color.FromArgb(70 + (int)(50 * low), 255, (int)(170 + 50 * low), (int)(130 + 40 * low)));
            g.FillEllipse(cloud, cx - cw / 2, cy - ch / 2, cw, ch);
        }
    }

    // ================================================================ the farm

    /// <summary>
    /// A white clapboard farmhouse on the hill, seen a little from the side: a
    /// wall with a steep gable roof, a chimney (smoke comes from the scene), a
    /// porch along the front with posts and a shallow roof, a door, and three
    /// warm windows (two downstairs, one up in the gable). Each lit window
    /// gets a small soft glow underneath it so it reads as light, not paint.
    /// </summary>
    private static void PaintFarmhouse(Graphics g, float x, float footY, float u)
    {
        float wallW = u * 0.12f, wallH = u * 0.058f, top = footY - wallH;
        Color white = Color.FromArgb(232, 220, 200), shade = Color.FromArgb(196, 178, 156), roof = Color.FromArgb(66, 48, 46);

        // Chimney first: the roof covers its foot.
        using (var brick = new SolidBrush(Color.FromArgb(120, 70, 56)))
            g.FillRectangle(brick, x - wallW * 0.30f - u * 0.008f, top - u * 0.070f, u * 0.016f, u * 0.05f);

        using (var wall = new SolidBrush(white))
            g.FillRectangle(wall, x - wallW / 2, top, wallW, wallH + u * 0.004f);
        // Clapboard: thin shaded lines across the wall.
        using (var board = new Pen(Color.FromArgb(70, 150, 130, 110), Math.Max(1f, u * 0.0012f)))
            for (float y = top + u * 0.006f; y < footY; y += u * 0.006f)
                g.DrawLine(board, x - wallW / 2, y, x + wallW / 2, y);

        // The gable roof, with eaves that overhang the wall a little.
        g.FillPolygon(new SolidBrush(roof), [
            new PointF(x - wallW * 0.58f, top + u * 0.004f), new PointF(x, top - u * 0.050f), new PointF(x + wallW * 0.58f, top + u * 0.004f)]);
        // The triangle of wall under the roof peak (the gable end), a shade darker than the front.
        using (var gable = new SolidBrush(shade))
            g.FillPolygon(gable, [new PointF(x - wallW * 0.46f, top), new PointF(x, top - u * 0.040f), new PointF(x + wallW * 0.46f, top)]);

        // Windows: glow, then pane, then the cross bar.
        RectangleF[] windows = FarmWindows(x, footY, u);
        using var glass = new SolidBrush(WindowWarm);
        using var bar = new Pen(Color.FromArgb(120, 100, 80), Math.Max(1f, u * 0.0015f));
        foreach (RectangleF win in windows)
        {
            PaintWindowGlow(g, win, u * 0.020f);
            g.FillRectangle(glass, win);
            g.DrawLine(bar, win.X + win.Width / 2, win.Y, win.X + win.Width / 2, win.Bottom);
            g.DrawLine(bar, win.X, win.Y + win.Height / 2, win.Right, win.Y + win.Height / 2);
        }

        // The door, right of middle.
        using (var door = new SolidBrush(Color.FromArgb(88, 58, 42)))
            g.FillRectangle(door, x + wallW * 0.02f, footY - wallH * 0.62f, u * 0.014f, wallH * 0.62f);

        // The porch: a shallow roof on four thin posts, along the front of the wall.
        float porchTop = top + wallH * 0.30f;
        using (var post = new Pen(white, Math.Max(1f, u * 0.0025f)))
            for (int i = 0; i < 4; i++)
            {
                float px = x - wallW * 0.42f + wallW * 0.84f * i / 3f;
                g.DrawLine(post, px, porchTop, px, footY);
            }
        using (var porchRoof = new SolidBrush(roof))
            g.FillRectangle(porchRoof, x - wallW * 0.50f, porchTop - u * 0.006f, wallW, u * 0.006f);
        using (var step = new SolidBrush(shade))
            g.FillRectangle(step, x - wallW * 0.50f, footY - u * 0.004f, wallW, u * 0.004f);   // the porch floor
    }

    /// <summary>
    /// Where the farmhouse's three windows are, for a house whose base is
    /// centered at (x, footY): [0] and [1] downstairs either side of the
    /// door, [2] up in the gable. The sizes must match PaintFarmhouse.
    /// </summary>
    internal static RectangleF[] FarmWindows(float x, float footY, float u)
    {
        float wallW = u * 0.12f, wallH = u * 0.058f, top = footY - wallH;
        RectangleF Window(float cx, float cy, float ww, float wh) => new(cx - ww / 2, cy - wh / 2, ww, wh);
        return
        [
            Window(x - wallW * 0.30f, top + wallH * 0.62f, u * 0.016f, u * 0.018f),
            Window(x + wallW * 0.30f, top + wallH * 0.62f, u * 0.016f, u * 0.018f),
            Window(x, top - u * 0.016f, u * 0.012f, u * 0.014f),
        ];
    }

    /// <summary>A soft warm glow behind a lit window, so the light seems to spill out of it.</summary>
    private static void PaintWindowGlow(Graphics g, RectangleF win, float glowR)
    {
        float cx = win.X + win.Width / 2, cy = win.Y + win.Height / 2;
        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(cx - glowR, cy - glowR, glowR * 2, glowR * 2);
        using var glow = new PathGradientBrush(glowPath)
        {
            CenterColor = Color.FromArgb(110, 255, 190, 90),
            SurroundColors = [Color.FromArgb(0, 255, 170, 60)],
            Blend = Sprite.SoftFalloff,
        };
        g.FillPath(glow, glowPath);
    }

    /// <summary>
    /// A red barn with a gambrel roof (the barn roof with the bend in it),
    /// a big door with a white X of trim, a loft window up in the gable, and
    /// a silo beside it: a pale tube with a domed cap.
    /// </summary>
    private static void PaintBarn(Graphics g, float x, float footY, float u)
    {
        float barnW = u * 0.13f, barnH = u * 0.06f, top = footY - barnH;
        Color red = Color.FromArgb(150, 44, 36), roof = Color.FromArgb(74, 40, 36), trim = Color.FromArgb(228, 214, 196);

        // The silo, behind the barn's right side.
        float sx = x + barnW * 0.62f, sw = u * 0.030f, sh = u * 0.11f;
        using (var silo = new LinearGradientBrush(new PointF(sx - sw / 2, 0), new PointF(sx + sw / 2, 0), Color.FromArgb(206, 196, 180), Color.FromArgb(140, 128, 116)))
            g.FillRectangle(silo, sx - sw / 2, footY - sh, sw, sh);
        using (var cap = new SolidBrush(Color.FromArgb(112, 100, 92)))
            g.FillEllipse(cap, sx - sw / 2, footY - sh - sw * 0.35f, sw, sw * 0.7f);

        using (var wall = new SolidBrush(red))
            g.FillRectangle(wall, x - barnW / 2, top, barnW, barnH + u * 0.004f);
        // The gambrel roof: two slopes each side, steep at the eaves and shallow at the top.
        using (var r = new SolidBrush(roof))
            g.FillPolygon(r, [
                new PointF(x - barnW * 0.56f, top + u * 0.004f), new PointF(x - barnW * 0.42f, top - u * 0.028f),
                new PointF(x - barnW * 0.18f, top - u * 0.045f), new PointF(x + barnW * 0.18f, top - u * 0.045f),
                new PointF(x + barnW * 0.42f, top - u * 0.028f), new PointF(x + barnW * 0.56f, top + u * 0.004f)]);
        using (var gable = new SolidBrush(Color.FromArgb(130, 38, 32)))
            g.FillPolygon(gable, [
                new PointF(x - barnW * 0.46f, top), new PointF(x - barnW * 0.36f, top - u * 0.024f),
                new PointF(x - barnW * 0.15f, top - u * 0.038f), new PointF(x + barnW * 0.15f, top - u * 0.038f),
                new PointF(x + barnW * 0.36f, top - u * 0.024f), new PointF(x + barnW * 0.46f, top)]);

        // The big door with its white X, and white trim along the corners.
        var door = new RectangleF(x - barnW * 0.16f, footY - barnH * 0.72f, barnW * 0.32f, barnH * 0.72f);
        using (var dark = new SolidBrush(Color.FromArgb(70, 30, 26)))
            g.FillRectangle(dark, door);
        using (var white = new Pen(trim, Math.Max(1f, u * 0.0018f)))
        {
            g.DrawRectangle(white, door.X, door.Y, door.Width, door.Height);
            g.DrawLine(white, door.X, door.Y, door.Right, door.Bottom);
            g.DrawLine(white, door.Right, door.Y, door.X, door.Bottom);
            g.DrawLine(white, x - barnW / 2, top, x - barnW / 2, footY);
            g.DrawLine(white, x + barnW / 2, top, x + barnW / 2, footY);
        }

        // The loft window, warm: hay is being put up by lantern light.
        var loft = new RectangleF(x - u * 0.009f, top - u * 0.026f, u * 0.018f, u * 0.016f);
        PaintWindowGlow(g, loft, u * 0.018f);
        using (var glass = new SolidBrush(WindowWarm))
            g.FillRectangle(glass, loft);
        using var bar = new Pen(Color.FromArgb(90, 40, 34), Math.Max(1f, u * 0.0015f));
        g.DrawLine(bar, loft.X + loft.Width / 2, loft.Y, loft.X + loft.Width / 2, loft.Bottom);
    }

    // ================================================================ the field and the ground

    /// <summary>
    /// The near ground: one bank running the full width of the screen, a
    /// little higher at the sides and dipping toward the middle. Everything
    /// that stands on it asks it "how high is the ground here?".
    /// </summary>
    private static Bank MakeGround(int w, int h) => new(
    [
        new PointF(-2, h * 0.805f), new PointF(w * 0.16f, h * 0.795f), new PointF(w * 0.34f, h * 0.825f),
        new PointF(w * 0.50f, h * 0.84f), new PointF(w * 0.66f, h * 0.83f), new PointF(w * 0.85f, h * 0.80f),
        new PointF(w + 2, h * 0.81f),
    ], h);

    /// <summary>
    /// The reaped field between the hills and the near ground: a warm ochre
    /// floor over the valley floor, with the stubble left by the harvester in
    /// long curved rows that come closer together toward the far side (the
    /// same rows, seen from farther off). Painted BEFORE the farm, so a
    /// building whose foot is below the horizon stands on the field.
    /// </summary>
    private static void PaintFieldFloor(Graphics g, int w, int h, float u, float horizon)
    {
        using (var field = new LinearGradientBrush(new PointF(0, horizon), new PointF(0, h * 0.86f), Color.FromArgb(172, 118, 60), Color.FromArgb(204, 150, 70)))
            g.FillRectangle(field, 0, horizon - 1, w, h * 0.86f - horizon + 2);

        // The stubble rows. Each is a shallow curve across the whole width;
        // the gap between rows grows as they come nearer (perspective).
        using var row = new Pen(Color.FromArgb(90, 150, 96, 40), Math.Max(1f, u * 0.0015f));
        using var lit = new Pen(Color.FromArgb(50, 236, 196, 110), Math.Max(1f, u * 0.0012f));
        float y = horizon + u * 0.03f, gap = u * 0.006f;
        while (y < h * 0.86f)
        {
            float sag = u * 0.012f * (y - horizon) / (h * 0.86f - horizon);
            g.DrawCurve(row, [new PointF(-2, y), new PointF(w * 0.35f, y + sag), new PointF(w * 0.65f, y + sag), new PointF(w + 2, y)], 0.5f);
            g.DrawCurve(lit, [new PointF(-2, y - gap * 0.4f), new PointF(w * 0.35f, y + sag - gap * 0.4f), new PointF(w * 0.65f, y + sag - gap * 0.4f), new PointF(w + 2, y - gap * 0.4f)], 0.5f);
            y += gap;
            gap *= 1.12f;
        }
    }

    /// <summary>Round hay bales standing on the field, seen end on: a disc with a darker spiral and a shaded underside. Returns them.</summary>
    private static List<(PointF At, float R)> PaintBales(Graphics g, int w, float u, PointF[] nearRidge, Bank ground, Random rng)
    {
        var bales = new List<(PointF At, float R)>();
        (float X, float Depth, float R)[] spots = [(0.12f, 0.35f, 0.024f), (0.52f, 0.50f, 0.030f), (0.78f, 0.30f, 0.022f), (0.90f, 0.70f, 0.032f)];
        foreach (var s in spots)
        {
            float bx = w * s.X;
            float ridgeY = Brushwork.RidgeYAt(nearRidge, bx), crest = ground.YAt(bx);
            float footY = ridgeY + (crest - ridgeY) * s.Depth;
            float r = u * s.R * (0.7f + 0.5f * s.Depth);           // nearer bales are bigger
            PaintBale(g, bx, footY, r, rng);
            bales.Add((new PointF(bx, footY - r), r));
        }
        return bales;
    }

    private static void PaintBale(Graphics g, float x, float footY, float r, Random rng)
    {
        float cy = footY - r;
        using (var shadow = new SolidBrush(Color.FromArgb(70, 80, 40, 20)))
            g.FillEllipse(shadow, x - r * 1.1f, footY - r * 0.25f, r * 2.3f, r * 0.5f);
        using (var hay = new LinearGradientBrush(new PointF(x, cy - r), new PointF(x, cy + r), Color.FromArgb(222, 178, 90), Color.FromArgb(150, 108, 46)))
            g.FillEllipse(hay, x - r, cy - r, r * 2, r * 2);
        // The spiral: a few turns of a thin dark line, each turn a smaller circle.
        using var twist = new Pen(Color.FromArgb(110, 120, 80, 30), Math.Max(1f, r * 0.06f));
        for (float k = 0.9f; k > 0.15f; k -= 0.22f)
            g.DrawArc(twist, x - r * k, cy - r * k, r * k * 2, r * k * 2, 20 + rng.Next(40), 300);
    }

    /// <summary>Dry autumn grass along the crest of the near ground: short strokes leaning with the breeze, paler than the bank.</summary>
    private static void PaintGrass(Graphics g, int w, float u, Bank ground, Random rng)
    {
        using var blade = new Pen(Color.FromArgb(160, 214, 170, 86), Math.Max(1f, u * 0.0014f)) { EndCap = LineCap.Round };
        using var dark = new Pen(Color.FromArgb(120, 130, 84, 36), Math.Max(1f, u * 0.0014f)) { EndCap = LineCap.Round };
        int n = (int)(w / Math.Max(2f, u * 0.004f));
        for (int i = 0; i < n; i++)
        {
            float x = (float)rng.NextDouble() * w;
            float y = ground.YAt(x) + u * (0.004f + 0.05f * (float)Math.Pow(rng.NextDouble(), 2));
            float tall = u * (0.008f + 0.012f * (float)rng.NextDouble()), lean = u * 0.006f * ((float)rng.NextDouble() - 0.3f);
            g.DrawLine(rng.Next(3) == 0 ? dark : blade, x, y, x + lean, y - tall);
        }
    }

    /// <summary>
    /// A split rail fence along the near ground, a little down from the crest:
    /// posts at an even spacing, two rails between each pair, each rail sagging
    /// a touch in the middle. One gap in the middle where the gate stands open
    /// (the gate leans against the next post). Returns the posts' x positions.
    /// </summary>
    private static float[] PaintFence(Graphics g, int w, float u, Bank ground)
    {
        float spacing = u * 0.115f, ph = u * 0.075f;
        var posts = new List<float>();
        for (float x = spacing * 0.4f; x < w + spacing; x += spacing) posts.Add(x);

        using var post = new Pen(Wood, Math.Max(1.5f, u * 0.006f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var rail = new Pen(Color.FromArgb(96, 66, 42), Math.Max(1.2f, u * 0.0045f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var lit = new Pen(Color.FromArgb(120, 170, 130, 80), Math.Max(1f, u * 0.0012f));

        float FootY(float x) => ground.YAt(Math.Clamp(x, 0, w - 1)) + u * 0.03f;
        for (int i = 0; i < posts.Count; i++)
        {
            float x = posts[i], foot = FootY(x);
            g.DrawLine(post, x, foot, x, foot - ph);
            g.DrawLine(lit, x + u * 0.002f, foot - ph + u * 0.004f, x + u * 0.002f, foot - u * 0.006f);   // the side the dusk light catches
            if (i + 1 >= posts.Count) continue;
            float nx = posts[i + 1];
            foreach (float k in (float[])[0.36f, 0.70f])
            {
                float ya = foot - ph * k, yb = FootY(nx) - ph * k;
                g.DrawCurve(rail, [new PointF(x, ya), new PointF((x + nx) / 2, (ya + yb) / 2 + u * 0.004f), new PointF(nx, yb)], 0.5f);
            }
        }
        return [.. posts];
    }

    // ================================================================ things on the near ground

    /// <summary>
    /// A cornucopia lying on the ground with its mouth to the right, spilling
    /// its harvest: a woven horn (a curved brown shape with lighter weave
    /// lines across it, tapering to a curled tip at the left), then apples,
    /// a pear, a bunch of grapes, an ear of corn and a small pumpkin tumbling
    /// out of the mouth onto the grass.
    /// </summary>
    private static void PaintCornucopia(Graphics g, float mouthX, float mouthY, float len, float u)
    {
        float r = len * 0.22f;   // the mouth's radius
        // The horn: a smooth closed curve from the mouth back to a curled tip.
        PointF[] horn = [
            new PointF(mouthX, mouthY - r * 2), new PointF(mouthX - len * 0.35f, mouthY - r * 2.1f), new PointF(mouthX - len * 0.70f, mouthY - r * 1.6f),
            new PointF(mouthX - len * 0.95f, mouthY - r * 1.9f), new PointF(mouthX - len * 1.02f, mouthY - r * 1.3f), new PointF(mouthX - len * 0.90f, mouthY - r * 1.1f),
            new PointF(mouthX - len * 0.60f, mouthY - r * 0.2f), new PointF(mouthX - len * 0.30f, mouthY + r * 0.05f), new PointF(mouthX, mouthY)];
        using (var path = new GraphicsPath())
        {
            path.AddClosedCurve(horn, 0.5f);
            using var weave = new LinearGradientBrush(new PointF(0, mouthY - r * 2.2f), new PointF(0, mouthY), Color.FromArgb(176, 128, 72), Color.FromArgb(104, 70, 38));
            g.FillPath(weave, path);
            // The weave: thin pale lines following the horn's curve, clipped to it.
            GraphicsState before = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            using var strand = new Pen(Color.FromArgb(90, 224, 184, 120), Math.Max(1f, u * 0.0012f));
            for (int i = 0; i < 9; i++)
            {
                float k = i / 8f;
                float x = mouthX - len * 0.08f - len * 0.95f * k;
                g.DrawBezier(strand, x, mouthY - r * 2.4f, x - len * 0.05f, mouthY - r * 1.5f, x + len * 0.03f, mouthY - r * 0.6f, x - len * 0.02f, mouthY + r * 0.2f);
            }
            for (int i = 0; i < 5; i++)
            {
                float k = i / 4f;
                g.DrawBezier(strand, mouthX, mouthY - r * 2 * k, mouthX - len * 0.4f, mouthY - r * 2.1f * k - r * 0.1f, mouthX - len * 0.7f, mouthY - r * 1.8f * k, mouthX - len * 1.0f, mouthY - r * 1.6f * k - r * 0.3f);
            }
            g.Restore(before);
        }
        // The mouth's dark inside: an ellipse at the open end.
        using (var inside = new SolidBrush(Color.FromArgb(58, 36, 22)))
            g.FillEllipse(inside, mouthX - r * 0.6f, mouthY - r * 2, r * 1.2f, r * 2);

        // The spill. Every fruit is a circle or two with a highlight, scattered in front of and to the right of the mouth.
        void Fruit(float cx, float cy, float fr, Color c, Color hi)
        {
            using var b = new SolidBrush(c);
            g.FillEllipse(b, cx - fr, cy - fr, fr * 2, fr * 2);
            using var h = new SolidBrush(hi);
            g.FillEllipse(h, cx - fr * 0.55f, cy - fr * 0.6f, fr * 0.5f, fr * 0.4f);
        }
        Color apple = Color.FromArgb(196, 40, 36), appleHi = Color.FromArgb(120, 255, 170, 150);
        Fruit(mouthX + r * 0.3f, mouthY - r * 0.55f, r * 0.55f, apple, appleHi);
        Fruit(mouthX + r * 1.3f, mouthY - r * 0.45f, r * 0.5f, Color.FromArgb(214, 150, 40), Color.FromArgb(120, 255, 230, 160));   // a golden apple
        Fruit(mouthX + r * 2.3f, mouthY - r * 0.4f, r * 0.45f, apple, appleHi);
        // A pear: a small circle on a bigger one.
        Fruit(mouthX + r * 0.9f, mouthY - r * 1.3f, r * 0.42f, Color.FromArgb(190, 180, 70), Color.FromArgb(110, 255, 250, 180));
        Fruit(mouthX + r * 0.95f, mouthY - r * 1.75f, r * 0.28f, Color.FromArgb(190, 180, 70), Color.FromArgb(0, 0, 0, 0));
        // Grapes: a cluster of small purple circles.
        using (var grape = new SolidBrush(Color.FromArgb(92, 44, 110)))
            for (int row = 0; row < 3; row++)
                for (int i = 0; i <= 2 - row; i++)
                {
                    float gx = mouthX + r * 1.9f + (i - (2 - row) / 2f) * r * 0.34f, gy = mouthY - r * 1.5f + row * r * 0.3f;
                    g.FillEllipse(grape, gx - r * 0.18f, gy - r * 0.18f, r * 0.36f, r * 0.36f);
                }
        // An ear of corn: a yellow oval with a dark line of kernels and a husk leaf.
        using (var husk = new SolidBrush(Color.FromArgb(150, 160, 70)))
            g.FillEllipse(husk, mouthX + r * 2.6f, mouthY - r * 1.0f, r * 1.6f, r * 0.5f);
        using (var corn = new SolidBrush(Color.FromArgb(240, 200, 70)))
            g.FillEllipse(corn, mouthX + r * 2.4f, mouthY - r * 0.95f, r * 1.4f, r * 0.45f);
        using (var kernel = new Pen(Color.FromArgb(120, 190, 140, 30), Math.Max(1f, u * 0.001f)))
            g.DrawLine(kernel, mouthX + r * 2.5f, mouthY - r * 0.72f, mouthX + r * 3.7f, mouthY - r * 0.72f);
        // A small pumpkin at the end of the spill.
        PaintPumpkin(g, mouthX + r * 3.4f, mouthY + r * 0.1f, r * 0.55f);
    }

    /// <summary>
    /// A scarecrow on a pole, arms out on a crossbar, in a patched shirt, with
    /// straw sticking out at the wrists, the ankles and the collar, a burlap
    /// sack for a head with stitched eyes and a stitched smile, and a straw
    /// hat with a crow-pecked brim. The scarf is NOT painted here: it blows
    /// in the breeze, so the scene stamps it every frame at ScarecrowNeck.
    ///
    /// Every point is (across, up) from the pole's foot as a fraction of the
    /// scarecrow's height ("tall"), so 0.8 up is the middle of the head.
    /// </summary>
    private static void PaintScarecrow(Graphics g, float x, float footY, float tall, float u)
    {
        PointF P(float across, float up) => new(x + tall * across, footY - tall * up);
        Color shirt = Color.FromArgb(150, 96, 56), patch = Color.FromArgb(72, 92, 120), straw = Color.FromArgb(224, 190, 100);
        using var pole = new Pen(Wood, Math.Max(2f, tall * 0.035f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var strawPen = new Pen(straw, Math.Max(1f, tall * 0.014f)) { EndCap = LineCap.Round };
        using var stitch = new Pen(Color.FromArgb(60, 40, 30), Math.Max(1f, tall * 0.012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // The pole and the crossbar, behind everything.
        g.DrawLine(pole, P(0, 0), P(0, 0.72f));
        g.DrawLine(pole, P(-0.30f, 0.62f), P(0.30f, 0.62f));

        // Straw at the ankles (the trouser legs are tied off with straw poking out) and the wrists.
        foreach (float side in (float[])[-1, 1])
        {
            for (int i = -2; i <= 2; i++) g.DrawLine(strawPen, P(side * 0.07f, 0.22f), P(side * 0.07f + i * 0.025f, 0.12f + MathF.Abs(i) * 0.01f));
            for (int i = -2; i <= 2; i++) g.DrawLine(strawPen, P(side * 0.30f, 0.62f), P(side * 0.36f + i * 0.012f, 0.58f + i * 0.025f));
        }

        // Trousers: two dark blue legs from the hips to the ankles.
        using (var trouser = new SolidBrush(Color.FromArgb(54, 60, 90)))
        {
            g.FillPolygon(trouser, [P(-0.11f, 0.48f), P(-0.01f, 0.48f), P(-0.04f, 0.20f), P(-0.11f, 0.20f)]);
            g.FillPolygon(trouser, [P(0.01f, 0.48f), P(0.11f, 0.48f), P(0.11f, 0.20f), P(0.04f, 0.20f)]);
        }
        // The shirt: a body and two sleeves along the crossbar, with a lighter patch on each.
        using (var cloth = new SolidBrush(shirt))
        {
            g.FillPolygon(cloth, [P(-0.13f, 0.72f), P(0.13f, 0.72f), P(0.12f, 0.46f), P(-0.12f, 0.46f)]);
            g.FillPolygon(cloth, [P(-0.12f, 0.70f), P(-0.30f, 0.66f), P(-0.30f, 0.57f), P(-0.11f, 0.55f)]);
            g.FillPolygon(cloth, [P(0.12f, 0.70f), P(0.30f, 0.66f), P(0.30f, 0.57f), P(0.11f, 0.55f)]);
        }
        using (var p = new SolidBrush(patch))
        {
            g.FillRectangle(p, P(0.02f, 0.60f).X, P(0.02f, 0.60f).Y, tall * 0.07f, tall * 0.07f);
            g.FillRectangle(p, P(-0.26f, 0.64f).X, P(-0.26f, 0.64f).Y, tall * 0.06f, tall * 0.05f);
        }
        // Plaid: a few thin lines across the shirt.
        using (var plaid = new Pen(Color.FromArgb(90, 60, 30, 20), Math.Max(1f, tall * 0.008f)))
            for (int i = 0; i < 4; i++)
            {
                g.DrawLine(plaid, P(-0.13f, 0.50f + i * 0.06f), P(0.13f, 0.50f + i * 0.06f));
                g.DrawLine(plaid, P(-0.09f + i * 0.06f, 0.46f), P(-0.09f + i * 0.06f, 0.72f));
            }
        // Straw at the collar.
        for (int i = -3; i <= 3; i++) g.DrawLine(strawPen, P(0, 0.71f), P(i * 0.03f, 0.75f + MathF.Abs(i) * 0.005f));

        // The head: a burlap sack, tied at the neck, with a stitched face.
        using (var sack = new SolidBrush(Color.FromArgb(204, 168, 110)))
            g.FillEllipse(sack, x - tall * 0.11f, footY - tall * 0.91f, tall * 0.22f, tall * 0.23f);
        g.DrawLine(stitch, P(-0.04f, 0.83f), P(-0.02f, 0.80f)); g.DrawLine(stitch, P(-0.02f, 0.83f), P(-0.04f, 0.80f));   // an X for each eye
        g.DrawLine(stitch, P(0.02f, 0.83f), P(0.04f, 0.80f)); g.DrawLine(stitch, P(0.04f, 0.83f), P(0.02f, 0.80f));
        g.DrawBezier(stitch, P(-0.05f, 0.75f), P(-0.02f, 0.72f), P(0.02f, 0.72f), P(0.05f, 0.75f));                      // the smile
        foreach (float sx in (float[])[-0.03f, 0f, 0.03f]) g.DrawLine(stitch, P(sx, 0.725f), P(sx, 0.745f));           // its stitches

        // The straw hat: a wide brim and a crown, a little askew.
        GraphicsState before = g.Save();
        g.TranslateTransform(P(0, 0.90f).X, P(0, 0.90f).Y);
        g.RotateTransform(-6);
        using (var hat = new SolidBrush(Color.FromArgb(214, 176, 92)))
        {
            g.FillEllipse(hat, -tall * 0.17f, -tall * 0.025f, tall * 0.34f, tall * 0.05f);
            g.FillPolygon(hat, [new PointF(-tall * 0.10f, 0), new PointF(-tall * 0.08f, -tall * 0.10f), new PointF(tall * 0.08f, -tall * 0.10f), new PointF(tall * 0.10f, 0)]);
        }
        using (var band = new Pen(Color.FromArgb(110, 60, 40), Math.Max(1f, tall * 0.012f)))
            g.DrawLine(band, -tall * 0.095f, -tall * 0.02f, tall * 0.095f, -tall * 0.02f);
        g.Restore(before);
    }

    /// <summary>
    /// A corn shock: a bundle of dried corn stalks stood up together and tied
    /// round the middle, so they splay out at the top and the bottom. Each
    /// stalk is one tan line from the foot to the top, bowed outward; a few
    /// dried leaves hang off them.
    /// </summary>
    private static void PaintCornShock(Graphics g, float x, float footY, float tall, float half, Random rng)
    {
        float tie = footY - tall * 0.42f;
        using var stalk = new Pen(Color.FromArgb(196, 160, 92), Math.Max(1f, tall * 0.012f)) { EndCap = LineCap.Round };
        using var dark = new Pen(Color.FromArgb(150, 116, 60), Math.Max(1f, tall * 0.012f)) { EndCap = LineCap.Round };
        using var leaf = new Pen(Color.FromArgb(180, 170, 134, 70), Math.Max(1f, tall * 0.02f)) { EndCap = LineCap.Round };
        int n = 26;
        for (int i = 0; i < n; i++)
        {
            float k = (i / (n - 1f)) * 2 - 1;                          // -1 at the left of the bundle, +1 at the right
            float bowFoot = half * k * (0.9f + 0.2f * (float)rng.NextDouble());
            float bowTop = half * k * (0.75f + 0.3f * (float)rng.NextDouble());
            float topY = footY - tall * (0.86f + 0.14f * (float)rng.NextDouble());
            Pen pen = (i + rng.Next(2)) % 3 == 0 ? dark : stalk;
            g.DrawBezier(pen, x + bowFoot, footY, x + bowFoot * 0.3f, tie + tall * 0.1f, x + bowTop * 0.3f, tie - tall * 0.1f, x + bowTop, topY);
            if (i % 5 == 2)   // a dried leaf drooping off this stalk
                g.DrawBezier(leaf, x + bowTop * 0.5f, tie - tall * 0.18f, x + bowTop * 0.9f + half * 0.3f * MathF.Sign(k), tie - tall * 0.22f,
                             x + bowTop + half * 0.5f * MathF.Sign(k), tie - tall * 0.05f, x + bowTop + half * 0.55f * MathF.Sign(k), tie + tall * 0.12f);
        }
        // The tie: a band of twine round the middle.
        using var twine = new Pen(Color.FromArgb(120, 84, 44), Math.Max(1.5f, tall * 0.025f));
        g.DrawLine(twine, x - half * 0.45f, tie, x + half * 0.45f, tie);
    }

    /// <summary>Pumpkin vines on the ground under the patch: dark green runners with a few broad leaves and curling tendrils.</summary>
    private static void PaintVines(Graphics g, int w, float u, Bank ground, (float X, float R, float Down)[] patch, Random rng)
    {
        using var vine = new Pen(Color.FromArgb(70, 96, 40), Math.Max(1f, u * 0.0025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var leaf = new SolidBrush(Color.FromArgb(84, 116, 46));
        float x0 = w * (patch[0].X - 0.04f), x1 = w * (patch[^1].X + 0.04f);
        float yAt(float x) => ground.YAt(x) + u * 0.05f;
        g.DrawBezier(vine, x0, yAt(x0), w * patch[1].X, yAt(w * patch[1].X) + u * 0.012f, w * patch[2].X, yAt(w * patch[2].X) - u * 0.01f, x1, yAt(x1));
        for (int i = 0; i < 7; i++)
        {
            float x = x0 + (x1 - x0) * (float)rng.NextDouble(), y = yAt(x) + u * 0.012f * ((float)rng.NextDouble() - 0.5f);
            float r = u * (0.010f + 0.008f * (float)rng.NextDouble());
            g.FillEllipse(leaf, x - r, y - r * 0.6f, r * 2, r * 1.2f);
            // A tendril: a small curl off the runner.
            g.DrawArc(vine, x + r, y - r, r, r, 90, 270);
        }
    }

    /// <summary>
    /// A pumpkin standing on the ground at (x, footY): three overlapping orange
    /// lobes (which is what gives a pumpkin its ribs), a stubby stem, and a
    /// paler highlight on the lit side. Returns its center and size.
    /// </summary>
    internal static (PointF At, float R) PaintPumpkin(Graphics g, float x, float footY, float r)
    {
        float cy = footY - r * 0.8f;
        using (var shadow = new SolidBrush(Color.FromArgb(60, 60, 30, 10)))
            g.FillEllipse(shadow, x - r * 1.1f, footY - r * 0.22f, r * 2.3f, r * 0.45f);
        using (var stem = new SolidBrush(Color.FromArgb(80, 92, 40)))
            g.FillPolygon(stem, [
                new PointF(x - r * 0.10f, cy - r * 0.70f), new PointF(x - r * 0.02f, cy - r * 1.10f),
                new PointF(x + r * 0.16f, cy - r * 1.05f), new PointF(x + r * 0.12f, cy - r * 0.70f)]);
        using (var side = new SolidBrush(Color.FromArgb(208, 96, 22)))
        {
            g.FillEllipse(side, x - r * 1.0f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
            g.FillEllipse(side, x - r * 0.1f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
        }
        using (var middle = new SolidBrush(Color.FromArgb(236, 124, 34)))
            g.FillEllipse(middle, x - r * 0.55f, cy - r * 0.80f, r * 1.1f, r * 1.60f);
        using (var hi = new SolidBrush(Color.FromArgb(70, 255, 210, 150)))
            g.FillEllipse(hi, x - r * 0.45f, cy - r * 0.62f, r * 0.4f, r * 0.7f);
        return (new PointF(x, cy), r);
    }

    /// <summary>Two gourds beside the pumpkins: a striped green one and a knobbly yellow one, each a pair of ovals.</summary>
    private static void PaintGourds(Graphics g, int w, float u, Bank ground)
    {
        float gx = w * 0.735f, gy = ground.YAt(gx) + u * 0.066f, r = u * 0.012f;
        using (var green = new SolidBrush(Color.FromArgb(120, 150, 60)))
        {
            g.FillEllipse(green, gx - r * 1.3f, gy - r * 1.1f, r * 2.6f, r * 1.3f);
            g.FillEllipse(green, gx - r * 0.8f, gy - r * 1.8f, r * 1.4f, r * 1.2f);
        }
        using (var stripe = new Pen(Color.FromArgb(120, 230, 220, 120), Math.Max(1f, u * 0.0015f)))
            for (int i = -2; i <= 2; i++)
                g.DrawArc(stripe, gx - r * 1.3f + i * r * 0.5f + r * 1.3f - r * 0.5f, gy - r * 1.1f, r, r * 1.3f, 180, 180);
        float yx = w * 0.845f, yy = ground.YAt(yx) + u * 0.064f;
        using var yellow = new SolidBrush(Color.FromArgb(226, 190, 60));
        g.FillEllipse(yellow, yx - r * 1.2f, yy - r * 1.2f, r * 2.4f, r * 1.4f);
        g.FillEllipse(yellow, yx - r * 0.7f, yy - r * 2.0f, r * 1.3f, r * 1.3f);
        using var knob = new SolidBrush(Color.FromArgb(120, 100, 50));
        foreach (var (kx, ky) in new[] { (-0.6f, -0.9f), (0.3f, -1.5f), (0.7f, -0.6f) })
            g.FillEllipse(knob, yx + r * kx - r * 0.12f, yy + r * ky - r * 0.12f, r * 0.24f, r * 0.24f);
    }

    /// <summary>
    /// Maples at both sides in full autumn colour, each standing on the ground
    /// under it. They are the sakura trees (same trunk, same cloud of dots)
    /// in a maple palette: crimson, orange and gold. Returns random spots
    /// inside the canopies, so the falling leaves can let go of the trees
    /// they belong to, and each canopy's center and size.
    /// </summary>
    private static (List<PointF> LeafSpots, List<(PointF At, float R)> Canopies) PaintMaples(Graphics g, int w, float u, Bank ground, Random rng)
    {
        var maple = new BlossomPalette(Color.FromArgb(240, 204, 92, 30), Color.FromArgb(158, 38, 24), Color.FromArgb(250, 196, 60));
        Color bark = Color.FromArgb(62, 44, 36);

        // (X as a fraction of the width, canopy center height above the ground in u, canopy size in u)
        (float X, float Lift, float R)[] trees =
        [
            (0.03f, 0.17f, 0.15f), (0.12f, 0.13f, 0.11f), (0.90f, 0.14f, 0.12f), (0.98f, 0.17f, 0.14f),
        ];
        foreach (var t in trees)
            Brushwork.Trunk(g, w * t.X, ground.YAt(w * t.X) + u * 0.03f, ground.YAt(w * t.X) - u * t.Lift, u * t.R, rng, bark);

        var spots = new List<PointF>();
        var canopies = new List<(PointF At, float R)>();
        foreach (var t in trees)
        {
            float cx = w * t.X, cy = ground.YAt(cx) - u * t.Lift, r = u * t.R;
            canopies.Add((new PointF(cx, cy), r));
            Brushwork.Canopy(g, cx, cy, r, u, rng, maple);
            for (int i = 0; i < 8; i++)
            {
                var spot = new PointF(cx + r * ((float)rng.NextDouble() * 2 - 1), cy + r * 0.5f * ((float)rng.NextDouble() * 2 - 1));
                if (spot.X >= 0 && spot.X < w) spots.Add(spot);
            }
        }
        return (spots, canopies);
    }

    /// <summary>
    /// Oak branches reaching in from the top corners, the last leaves of the
    /// year still clinging to them in gold, orange and russet. The wood is
    /// the sakura branches grown bare; then at every spot where a blossom
    /// cluster would have been, a small bunch of leaves is painted instead
    /// (each leaf a pointed oval turned its own way), so the clusters read
    /// as foliage and not as flowers.
    /// </summary>
    private static List<PointF> PaintCornerBranches(Graphics g, int w, int h, float u, Random rng)
    {
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.04f), 0.30f, u * 0.44f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.30f, u * 0.40f, u * 0.016f, 3),  // top right, reaching left
        ];
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng, wood: Wood, petalLight: Wood, petalDeep: Wood, bare: true);

        Color[] leafColours = [Color.FromArgb(244, 176, 48), Color.FromArgb(226, 118, 36), Color.FromArgb(166, 52, 24), Color.FromArgb(250, 204, 86)];
        float len = Math.Max(3f, u * 0.014f);
        foreach (PointF spot in spots)
        {
            int n = 3 + rng.Next(4);
            for (int i = 0; i < n; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), l = len * (0.7f + 0.6f * (float)rng.NextDouble());
                float bx = spot.X + u * 0.008f * ((float)rng.NextDouble() * 2 - 1), by = spot.Y + u * 0.008f * ((float)rng.NextDouble() * 2 - 1);
                float ca = MathF.Cos(a), sa = MathF.Sin(a);
                PointF L(float along, float across) => new(bx + along * ca - across * sa, by + along * sa + across * ca);
                using var path = new GraphicsPath();
                path.AddClosedCurve([L(0, 0), L(l * 0.5f, l * 0.32f), L(l, 0), L(l * 0.5f, -l * 0.32f)], 0.6f);
                using var leaf = new SolidBrush(leafColours[rng.Next(leafColours.Length)]);
                g.FillPath(leaf, path);
            }
        }
        return spots;
    }
}
