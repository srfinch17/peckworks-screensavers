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
    public required List<PointF> Stars { get; init; }                // the brighter stars, for the scene to twinkle

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
    public required bool[] OpenOfProps { get; init; }                // stencil: true where nothing was painted after the fence (a squirrel on the rail passes behind the corn shock and the maples)
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

    /// <summary>
    /// THE FIELD'S ONE SCALE: how many U a metre is at a given depth across the field (0 the far lane, 1 the fence).
    /// Pinned at the lane by the tractor (its rear wheel, 1.7 m, is 0.040 U) and at the fence by the posts (1.2 m,
    /// about 0.048 U). Every animal that lives on the field takes its size from this and its real height or length,
    /// so a deer is half a tractor wheel tall and a rabbit is a tenth of a tractor long, wherever it stands. (Before
    /// this the deer stood 0.078 U at the shoulder, twice the wheel, four times their size: "all of your background
    /// animals are like 20 ft tall compared to the tractor".) The near ground in front of the fence is a closer plane
    /// with its own, bigger scale (the patch's pumpkins and critters), not this one.
    /// </summary>
    public float MetresToU(float depth) => 0.0235f + (0.040f - 0.0235f) * Math.Clamp(depth, 0f, 1f);

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
    public required List<(PointF At, float R)> FarTrees { get; init; } // the windbreak along the field's far edge: each canopy's centre and size (the ridge deer keep clear of them)

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
    public required List<(PointF At, float R)> Pumpkins { get; init; } // the pumpkin patch: each pumpkin's center and size (its foot is 0.8 R below the center)
    public required (PointF At, float R) Jack { get; init; }          // the carved jack-o'-lantern among them: its center and size, for the candle's flicker
    public required bool[] JackHoles { get; init; }                   // stencil: true on the jack-o'-lantern's carved holes (a mouse peeking out of an eye shows only there)
    public required byte[] NearDepth { get; init; }                   // the near ground's depth picture: how near each pixel's pumpkin, gourd, leaf, the horn or the shock stands (0 = nothing there); see Nearness
    public required float CrestMin { get; init; }
    /// <summary>
    /// How near a thing standing with its feet at y is, on the depth picture's scale (1 far, at the ground's crest,
    /// to 254 at the bottom edge). Stamp a ground actor with NearDepth and this of its feet: it is then hidden by
    /// exactly the pumpkins whose feet are lower on the screen than its own, and shows in front of the rest.
    /// </summary>
    public byte Nearness(float y) => (byte)Math.Clamp(1 + 253 * (y - CrestMin) / (Height + 0.1f * U - CrestMin), 1, 254);
    public required PointF StageLeft { get; init; }                  // the open stretch of the walk line left of the middle: where an apple rolls in to, and the raccoon sniffs at a pumpkin
    public required List<(PointF At, float R)> Canopies { get; init; } // each maple's cloud of leaves: center and size
    public required List<PointF> CornerBranchSpots { get; init; }    // points on the corner branches' twigs and tips
    public required (PointF At, float R) LeftMaple { get; init; }    // the inner maple on each side (canopy centre and size); its trunk stands at At.X. The squirrels' trees.
    public required (PointF At, float R) RightMaple { get; init; }
    public required PointF[] LeftTrunk { get; init; }                // each inner maple's trunk as a line to climb: [0] the foot on the ground, [1] the fork, [2] the end of a limb inside the canopy
    public required PointF[] RightTrunk { get; init; }
    public required (PointF At, float R) Hollow { get; init; }       // the hollow in the roots of the left maple: the centre of its dark opening and its half width. A squirrel lives in it.

    // ---- Live facts, set by the scene every frame before the happenings draw ----
    public PointF GeeseLead { get; set; }                            // where the lead goose is
    public bool GeeseOnScreen { get; set; }                          // false while the V is on its off-screen pause
}

/// <summary>
/// Paints the backdrop: a harvest dusk (plum overhead, gold at the horizon),
/// a big peach harvest moon, two ranges of hills with a white farmhouse and a
/// red barn on the nearer one, a reaped field in gold stripes with round hay
/// bales, a rail fence along the near ground, a scarecrow, a corn shock, a
/// pumpkin patch, maples in full autumn colour
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
        List<PointF> stars = PaintSky(g, w, h, horizon, u, rng);
        var moon = (At: new PointF(w * 0.74f, h * 0.26f), R: u * 0.075f);
        PaintMoon(g, moon.At.X, moon.At.Y, moon.R);
        PaintClouds(g, w, h, u, rng);
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);   // the copy the sky stencil is made from (see the class summary)

        PointF[] farRidge = Brushwork.Ridge(w, horizon, u, 0.08f, 0.05f, 0.02f, 0.07f, rng);
        using (var far = new SolidBrush(Color.FromArgb(112, 62, 84)))
            g.FillPolygon(far, farRidge);
        PaintTreeline(g, w, u, farRidge, rng);
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
        // The scarecrow stands in the garden beside the farmhouse, on the same hill, at the house's scale (his line:
        // "put the scarecrow back by the house. not in the middle of a cut hay field"). Painted with the farm, before the
        // field copy, so everything that moves on the field passes in front of it as it should for a thing behind the field.
        // The house is 0.108 U tall for about 7 m (0.0154 U a metre on its hill), so a 1.8 m scarecrow beside it is 0.028 U
        // (at 0.085 U it was "as big as a house").
        float scarecrowX = farmFoot.X + u * 0.095f, scarecrowTall = u * 0.028f;
        float scarecrowFoot = Brushwork.RidgeYAt(nearRidge, scarecrowX) + u * 0.010f;
        PaintScarecrow(g, scarecrowX, scarecrowFoot, scarecrowTall, u);
        // An apple orchard on the hill past the scarecrow, at the house's scale (his line: "give me something around the
        // house, like an apple orchard or some other farm stuff"). Painted with the farm, so the field's animals pass in front.
        float orchardX = scarecrowX + u * 0.105f, orchardHalf = u * 0.070f;
        PaintOrchard(g, u, nearRidge, orchardX, orchardHalf, rng);
        // A windbreak of trees along the far edge of the field, where the hill meets it: nearer than the hills' treeline,
        // farther than everything that moves on the field (his line: "another set of trees closer to the foreground but not
        // in front of the highest branches"). Painted here, with the farm, so the lane's animals pass in front of them.
        List<(PointF At, float R)> farTrees = PaintFarTrees(g, w, u, nearRidge, farmFoot.X, barnFoot.X, scarecrowX, orchardX, orchardHalf, rng);
        g.Flush();
        uint[] upToFarm = Brushwork.ToPixels(bmp);   // a second copy: "unchanged since here" = nothing stands in front of the farm (the scarecrow is part of it)

        Bank ground = MakeGround(w, h);
        List<(PointF At, float R)> bales = PaintBales(g, w, u, nearRidge, ground, rng);
        g.Flush();
        uint[] upToField = Brushwork.ToPixels(bmp);  // a third copy: for things that move on the field (the tractor), behind the scarecrow, the fence and the near ground

        ground.Paint(g, Color.FromArgb(178, 124, 56), Color.FromArgb(100, 60, 30), Color.FromArgb(150, 214, 166, 84), Math.Max(1.5f, u * 0.004f));
        PaintGrass(g, w, u, ground, rng);

        float[] posts = PaintFence(g, w, u, ground);
        g.Flush();
        uint[] upToFence = Brushwork.ToPixels(bmp);  // a fifth copy: for things on the fence's rail, behind the corn shock and the maples

        var shockFoot = new PointF(w * 0.555f, ground.YAt(w * 0.555f) + u * 0.052f);
        float shockTall = u * 0.15f, shockHalf = u * 0.045f;

        // THE PUMPKIN PATCH covers the whole foreground, in two bands with a clear lane between them: small
        // pumpkins BEHIND the walk line (painted now, so the shock and the maples stand in front of
        // them) and big ones IN FRONT of it along the bottom edge (painted after the maples, in front of their
        // trunks). Every pumpkin, gourd and leaf is painted a second time, flat, into a DEPTH picture, each in a
        // grey that says how near it is (its foot's height on the screen), so anything that moves on the
        // ground can be hidden by exactly the pumpkins nearer than itself and show in front of the rest.
        float crestMin = h * 0.79f;
        byte NearnessOf(float y) => (byte)Math.Clamp(1 + 253 * (y - crestMin) / (h + 0.1f * u - crestMin), 1, 254);
        using var depthBmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var gd = Graphics.FromImage(depthBmp);
        gd.SmoothingMode = SmoothingMode.None;
        List<PatchItem> patch = PlanPatch(w, h, u, ground, rng, hollowX: w * 0.12f, stageRight: w * 0.60f, shockFoot.X, shockHalf);
        PaintVineRunners(g, patch, u);
        var pumpkins = new List<(PointF At, float R)>();
        (PointF At, float R) jack = default;
        foreach (PatchItem item in patch)
            if (!item.Front) PaintPatchItem(g, gd, item, u, NearnessOf(item.Foot.Y), pumpkins, ref jack);

        // The shock stands ON the walk line: in the depth picture it is a hair nearer than it, so a mouse or a turkey at
        // the walk line passes behind it (its silhouette is read off the picture: what changed).
        g.Flush();
        uint[] beforeProps = Brushwork.ToPixels(bmp);
        PaintCornShock(g, shockFoot.X, shockFoot.Y, shockTall, shockHalf, rng);
        g.Flush();
        uint[] afterProps = Brushwork.ToPixels(bmp);

        var (leafSpots, canopies, hollow, leftTrunk, rightTrunk) = PaintMaples(g, w, u, ground, rng);

        foreach (PatchItem item in patch)
            if (item.Front) PaintPatchItem(g, gd, item, u, NearnessOf(item.Foot.Y), pumpkins, ref jack);

        g.Flush();
        uint[] upToBranches = Brushwork.ToPixels(bmp);  // a fourth copy: "unchanged since here" = no corner branch in front
        List<PointF> cornerSpots = PaintCornerBranches(g, w, h, u, rng);
        // Leaves can let go of the corner branches too, not only the maples.
        foreach (PointF spot in cornerSpots)
            if (spot.X >= 0 && spot.X < w && spot.Y >= 0 && spot.Y < h) leafSpots.Add(spot);

        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);

        // The depth picture, as bytes: the grey of each pumpkin, gourd and leaf; the shock's silhouette a hair nearer
        // than the walk line; 0 where nothing on the near ground stands.
        gd.Flush();
        uint[] depthPixels = Brushwork.ToPixels(depthBmp);
        var nearDepth = new byte[depthPixels.Length];
        byte propNear = (byte)Math.Min(254, NearnessOf(ground.YAt(w * 0.5f) + u * 0.052f) + 1);
        for (int i = 0; i < nearDepth.Length; i++)
        {
            byte v = (byte)(depthPixels[i] & 0xFF);
            if (beforeProps[i] != afterProps[i] && v < propNear) v = propNear;
            nearDepth[i] = v;
        }
        // The jack-o'-lantern's carved holes, as a stencil: the pixels of candle yellow inside its bounds (a mouse peeking out of an eye is drawn only there).
        var jackHoles = new bool[pixels.Length];
        if (jack.R > 0)
        {
            int x0 = Math.Max(0, (int)(jack.At.X - jack.R)), x1 = Math.Min(w - 1, (int)(jack.At.X + jack.R));
            int y0 = Math.Max(0, (int)(jack.At.Y - jack.R)), y1 = Math.Min(h - 1, (int)(jack.At.Y + jack.R));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    uint p = pixels[y * w + x] & 0xFFFFFF;
                    int r = (int)(p >> 16), gg = (int)((p >> 8) & 0xFF), b = (int)(p & 0xFF);
                    jackHoles[y * w + x] = r > 240 && gg > 200 && gg < 235 && b > 90 && b < 130;
                }
        }

        float wallW = u * 0.12f, wallH = u * 0.058f, farmTop = farmFoot.Y - wallH;
        float barnW = u * 0.13f, barnH = u * 0.06f, barnTop = barnFoot.Y - barnH;
        return new ThanksgivingScenery
        {
            Pixels = pixels, LeafSpots = leafSpots, Stars = stars,
            Width = w, Height = h, U = u, Horizon = horizon, Moon = moon,
            OpenSky = Brushwork.Unchanged(skyOnly, pixels),
            OpenFromFarm = Brushwork.Unchanged(upToFarm, pixels),
            OpenField = Brushwork.Unchanged(upToField, pixels),
            OpenOfBranches = Brushwork.Unchanged(upToBranches, pixels),
            OpenOfProps = Brushwork.Unchanged(upToFence, pixels),
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
            FarTrees = farTrees,
            FencePosts = posts,
            ScarecrowFoot = new PointF(scarecrowX, scarecrowFoot), ScarecrowHeight = scarecrowTall,
            ScarecrowHead = new PointF(scarecrowX, scarecrowFoot - scarecrowTall * 0.80f),
            ScarecrowNeck = new PointF(scarecrowX + scarecrowTall * 0.045f, scarecrowFoot - scarecrowTall * 0.70f),
            ScarecrowArmL = new PointF(scarecrowX - scarecrowTall * 0.30f, scarecrowFoot - scarecrowTall * 0.62f),
            ScarecrowArmR = new PointF(scarecrowX + scarecrowTall * 0.30f, scarecrowFoot - scarecrowTall * 0.62f),
            CornShockFoot = shockFoot, CornShockTop = new PointF(shockFoot.X, shockFoot.Y - shockTall), CornShockHalfWidth = shockHalf,
            Pumpkins = pumpkins, Jack = jack, JackHoles = jackHoles, NearDepth = nearDepth, CrestMin = crestMin,
            StageLeft = new PointF(w * 0.24f, ground.YAt(w * 0.24f) + u * 0.052f),
            Canopies = canopies, CornerBranchSpots = cornerSpots,
            LeftMaple = canopies[1], RightMaple = canopies[2], LeftTrunk = leftTrunk, RightTrunk = rightTrunk, Hollow = hollow,
        };
    }

    // ================================================================ sky

    /// <summary>
    /// A harvest dusk: deep plum overhead, through rose, to amber and then a
    /// band of gold right at the horizon where the sun has just gone down.
    /// A few faint stars are out in the darker top third.
    /// </summary>
    private static List<PointF> PaintSky(Graphics g, int w, int h, float horizon, float u, Random rng)
    {
        var stars = new List<PointF>();
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
            if (i % 2 == 0) stars.Add(new PointF(x, y));                 // every other star twinkles (the scene stamps a glow on it)
        }
        return stars;
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

    /// <summary>
    /// A few long, thin streaks of cloud lit from below by the sunset: each one
    /// two soft ellipses, a rose-gold underside with a paler top edge set a
    /// little higher, so the streak has a lit side and a shaded side instead
    /// of being one flat smear. Lower clouds are nearer the gold band, so they
    /// are lit warmer and brighter.
    /// </summary>
    private static void PaintClouds(Graphics g, int w, int h, float u, Random rng)
    {
        for (int i = 0; i < 8; i++)
        {
            float cx = w * (0.05f + 0.9f * (float)rng.NextDouble()), cy = h * (0.22f + 0.36f * (float)rng.NextDouble());
            float cw = u * (0.22f + 0.40f * (float)rng.NextDouble()), ch = u * (0.010f + 0.014f * (float)rng.NextDouble());
            float low = (cy - h * 0.22f) / (h * 0.36f);
            using (var under = new SolidBrush(Color.FromArgb(70 + (int)(50 * low), 240, (int)(150 + 50 * low), (int)(120 + 40 * low))))
                g.FillEllipse(under, cx - cw / 2, cy - ch / 2, cw, ch);
            using (var lit = new SolidBrush(Color.FromArgb(60 + (int)(40 * low), 255, (int)(214 + 30 * low), (int)(180 + 40 * low))))
                g.FillEllipse(lit, cx - cw * 0.42f, cy - ch * 0.85f, cw * 0.84f, ch * 0.75f);
        }
    }

    /// <summary>
    /// A distant woodland along the far ridge: small rounded crowns in the
    /// ridge's own dusk plum, a shade darker, in clumps with gaps, so the
    /// skyline is a treeline and not one smooth curve. Painted right after
    /// the far ridge, so everything nearer still covers it.
    /// </summary>
    private static void PaintTreeline(Graphics g, int w, float u, PointF[] farRidge, Random rng)
    {
        using var crown = new SolidBrush(Color.FromArgb(96, 50, 72));
        float step = Math.Max(2f, u * 0.009f);
        bool inClump = true;
        for (float x = 0; x < w; x += step)
        {
            if (rng.NextDouble() < 0.06) inClump = !inClump;        // clumps of trees and open gaps
            if (!inClump) continue;
            float y = Brushwork.RidgeYAt(farRidge, x);
            float r = u * (0.0035f + 0.005f * (float)rng.NextDouble());
            g.FillEllipse(crown, x - r, y - r * 1.3f, r * 2, r * 2.2f);
        }
    }

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

        // Shingle lines across the roof, and a cap on the chimney.
        using (var shingle = new Pen(Color.FromArgb(60, 120, 96, 90), Math.Max(1f, u * 0.001f)))
            for (int i = 1; i < 6; i++)
            {
                float t = i / 6f, y = top - u * 0.050f + (u * 0.054f) * t;
                g.DrawLine(shingle, x - wallW * 0.58f * t, y, x + wallW * 0.58f * t, y);
            }
        using (var cap = new SolidBrush(Color.FromArgb(90, 52, 42)))
            g.FillRectangle(cap, x - wallW * 0.30f - u * 0.010f, top - u * 0.074f, u * 0.020f, u * 0.005f);

        // Windows: glow, then pane, then a white frame and the cross bar.
        RectangleF[] windows = FarmWindows(x, footY, u);
        using var glass = new SolidBrush(WindowWarm);
        using var frame = new Pen(Color.FromArgb(244, 238, 226), Math.Max(1f, u * 0.0015f));
        using var bar = new Pen(Color.FromArgb(120, 100, 80), Math.Max(1f, u * 0.0012f));
        foreach (RectangleF win in windows)
        {
            PaintWindowGlow(g, win, u * 0.020f);
            g.FillRectangle(glass, win);
            g.DrawLine(bar, win.X + win.Width / 2, win.Y, win.X + win.Width / 2, win.Bottom);
            g.DrawLine(bar, win.X, win.Y + win.Height / 2, win.Right, win.Y + win.Height / 2);
            g.DrawRectangle(frame, win.X, win.Y, win.Width, win.Height);
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
        // The porch railing: a rail with balusters, either side of the door.
        using (var rail = new Pen(white, Math.Max(1f, u * 0.0015f)))
        {
            float ry = footY - u * 0.016f;
            foreach (var (ra, rb) in new[] { (x - wallW * 0.50f, x - u * 0.012f), (x + u * 0.030f, x + wallW * 0.50f) })
            {
                g.DrawLine(rail, ra, ry, rb, ry);
                for (float bx = ra + u * 0.004f; bx < rb; bx += u * 0.005f)
                    g.DrawLine(rail, bx, ry, bx, footY - u * 0.004f);
            }
        }
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
    /// A red barn with a gambrel roof (the barn roof with the bend in it) and
    /// its silo, built as ONE thing: the silo stands on the hill beside the
    /// barn's right wall, touching it, with a domed cap, hoop bands down its
    /// side, a hatch near the top and a shadow at its foot (the first version
    /// was a grey tube pasted behind the corner, with nothing holding it to
    /// the ground). The barn has board lines, white trim at the corners and
    /// the eaves, the big door with its white X, a hayloft door above it, the
    /// lit loft window up in the gable, and a weathervane on the ridge.
    /// </summary>
    private static void PaintBarn(Graphics g, float x, float footY, float u)
    {
        float barnW = u * 0.13f, barnH = u * 0.06f, top = footY - barnH;
        Color red = Color.FromArgb(150, 44, 36), redDark = Color.FromArgb(118, 34, 30), roof = Color.FromArgb(74, 40, 36), roofLit = Color.FromArgb(96, 54, 48);
        Color trim = Color.FromArgb(228, 214, 196);

        // ---- The silo, standing beside the right wall ----
        float sw = u * 0.034f, sh = u * 0.118f, sx = x + barnW * 0.5f + sw * 0.52f;
        using (var shadow = new SolidBrush(Color.FromArgb(70, 50, 24, 16)))
            g.FillEllipse(shadow, sx - sw * 0.8f, footY - sw * 0.16f, sw * 1.8f, sw * 0.4f);
        using (var silo = new LinearGradientBrush(new PointF(sx - sw / 2, 0), new PointF(sx + sw / 2, 0), Color.FromArgb(214, 204, 188), Color.FromArgb(132, 120, 108)))
            g.FillRectangle(silo, sx - sw / 2, footY - sh, sw, sh + u * 0.004f);
        using (var hoop = new Pen(Color.FromArgb(90, 70, 58, 50), Math.Max(1f, u * 0.0015f)))
            for (int i = 1; i <= 5; i++)
                g.DrawLine(hoop, sx - sw / 2, footY - sh * i / 6f, sx + sw / 2, footY - sh * i / 6f);
        using (var dome = new LinearGradientBrush(new PointF(sx - sw / 2, 0), new PointF(sx + sw / 2, 0), Color.FromArgb(150, 136, 124), Color.FromArgb(96, 86, 80)))
            g.FillPie(dome, sx - sw / 2, footY - sh - sw * 0.55f, sw, sw * 1.1f, 180, 180);        // the domed cap: the top half of an ellipse
        using (var hatch = new SolidBrush(Color.FromArgb(70, 58, 52)))
            g.FillRectangle(hatch, sx - sw * 0.14f, footY - sh + sw * 0.25f, sw * 0.28f, sw * 0.32f);

        // ---- The barn ----
        using (var wall = new SolidBrush(red))
            g.FillRectangle(wall, x - barnW / 2, top, barnW, barnH + u * 0.004f);
        using (var board = new Pen(Color.FromArgb(70, 80, 20, 18), Math.Max(1f, u * 0.0012f)))
            for (float bx = x - barnW / 2 + u * 0.007f; bx < x + barnW / 2; bx += u * 0.007f)
                g.DrawLine(board, bx, top, bx, footY);
        // The gambrel roof: steep at the eaves, shallow at the top, with the top slopes a shade lighter (they face the sky).
        PointF[] eaveL = [new(x - barnW * 0.58f, top + u * 0.004f), new(x - barnW * 0.43f, top - u * 0.030f)];
        PointF[] eaveR = [new(x + barnW * 0.58f, top + u * 0.004f), new(x + barnW * 0.43f, top - u * 0.030f)];
        PointF peakL = new(x - barnW * 0.17f, top - u * 0.047f), peakR = new(x + barnW * 0.17f, top - u * 0.047f);
        using (var r = new SolidBrush(roof))
            g.FillPolygon(r, [eaveL[0], eaveL[1], peakL, peakR, eaveR[1], eaveR[0]]);
        using (var lit = new SolidBrush(roofLit))
            g.FillPolygon(lit, [eaveL[1], peakL, peakR, eaveR[1], new(x + barnW * 0.40f, top - u * 0.026f), new(x - barnW * 0.40f, top - u * 0.026f)]);
        // The gable end under the roof, in the wall's red, a shade darker: it faces away from the dusk.
        using (var gable = new SolidBrush(redDark))
            g.FillPolygon(gable, [
                new PointF(x - barnW * 0.48f, top), new PointF(x - barnW * 0.38f, top - u * 0.024f),
                new PointF(x - barnW * 0.15f, top - u * 0.039f), new PointF(x + barnW * 0.15f, top - u * 0.039f),
                new PointF(x + barnW * 0.38f, top - u * 0.024f), new PointF(x + barnW * 0.48f, top)]);
        // The weathervane on the ridge: a rod, an arrow and a tiny rooster.
        using (var rod = new Pen(Color.FromArgb(50, 40, 40), Math.Max(1f, u * 0.0015f)))
        {
            g.DrawLine(rod, x, top - u * 0.047f, x, top - u * 0.062f);
            g.DrawLine(rod, x - u * 0.008f, top - u * 0.057f, x + u * 0.008f, top - u * 0.057f);
        }
        using (var vane = new SolidBrush(Color.FromArgb(50, 40, 40)))
        {
            g.FillPolygon(vane, [new PointF(x + u * 0.008f, top - u * 0.059f), new PointF(x + u * 0.012f, top - u * 0.057f), new PointF(x + u * 0.008f, top - u * 0.055f)]);
            g.FillEllipse(vane, x - u * 0.004f, top - u * 0.0645f, u * 0.006f, u * 0.005f);        // the rooster's body
            g.FillPolygon(vane, [new PointF(x - u * 0.002f, top - u * 0.063f), new PointF(x - u * 0.004f, top - u * 0.067f), new PointF(x + u * 0.001f, top - u * 0.065f)]);   // its tail
        }

        // The big door with its white X, the hayloft door above it, and white trim at the corners and the eaves.
        var door = new RectangleF(x - barnW * 0.16f, footY - barnH * 0.72f, barnW * 0.32f, barnH * 0.72f);
        using (var dark = new SolidBrush(Color.FromArgb(70, 30, 26)))
            g.FillRectangle(dark, door);
        var loftDoor = new RectangleF(x - barnW * 0.09f, top - u * 0.004f, barnW * 0.18f, barnH * 0.22f);
        using (var hay = new SolidBrush(Color.FromArgb(206, 166, 86)))
            g.FillRectangle(hay, loftDoor);                                                     // hay stacked in the open loft door
        using (var hayLine = new Pen(Color.FromArgb(120, 150, 110, 50), Math.Max(1f, u * 0.001f)))
            for (int i = 1; i < 4; i++)
                g.DrawLine(hayLine, loftDoor.X, loftDoor.Y + loftDoor.Height * i / 4f, loftDoor.Right, loftDoor.Y + loftDoor.Height * i / 4f);
        using (var white = new Pen(trim, Math.Max(1f, u * 0.0018f)))
        {
            g.DrawRectangle(white, door.X, door.Y, door.Width, door.Height);
            g.DrawLine(white, door.X, door.Y, door.Right, door.Bottom);
            g.DrawLine(white, door.Right, door.Y, door.X, door.Bottom);
            g.DrawRectangle(white, loftDoor.X, loftDoor.Y, loftDoor.Width, loftDoor.Height);
            g.DrawLine(white, x - barnW / 2, top, x - barnW / 2, footY);
            g.DrawLine(white, x + barnW / 2, top, x + barnW / 2, footY);
            g.DrawLines(white, [eaveL[0], eaveL[1], peakL, peakR, eaveR[1], eaveR[0]]);       // the eaves' trim
        }

        // The loft window, warm: hay is being put up by lantern light.
        var loft = new RectangleF(x - u * 0.009f, top - u * 0.026f, u * 0.018f, u * 0.016f);
        PaintWindowGlow(g, loft, u * 0.018f);
        using (var glass = new SolidBrush(WindowWarm))
            g.FillRectangle(glass, loft);
        using (var frame = new Pen(trim, Math.Max(1f, u * 0.0012f)))
            g.DrawRectangle(frame, loft.X, loft.Y, loft.Width, loft.Height);
        using var bar = new Pen(Color.FromArgb(90, 40, 34), Math.Max(1f, u * 0.0015f));
        g.DrawLine(bar, loft.X + loft.Width / 2, loft.Y, loft.X + loft.Width / 2, loft.Bottom);
    }

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

    /// <summary>
    /// The windbreak along the field's far edge: half a dozen trees of mixed colour standing on the line where the
    /// nearer hill meets the field, their feet a touch below it so they stand ON the field's edge. They keep clear of
    /// the farmhouse, the scarecrow and the barn, and stay short enough that their tops never reach the far ridge
    /// where the silhouette deer walk. The maples' own trunk and canopy brushes, at a smaller size.
    /// </summary>
    private static List<(PointF At, float R)> PaintFarTrees(Graphics g, int w, float u, PointF[] nearRidge, float farmX, float barnX, float scarecrowX, float orchardX, float orchardHalf, Random rng)
    {
        var trees = new List<(PointF At, float R)>();
        var oak = new BlossomPalette(Color.FromArgb(240, 196, 120, 40), Color.FromArgb(150, 70, 30), Color.FromArgb(246, 180, 70));
        var maple = new BlossomPalette(Color.FromArgb(240, 204, 92, 30), Color.FromArgb(158, 38, 24), Color.FromArgb(250, 196, 60));
        var gold = new BlossomPalette(Color.FromArgb(240, 222, 170, 50), Color.FromArgb(170, 110, 30), Color.FromArgb(252, 220, 110));
        Color bark = Color.FromArgb(62, 44, 36);
        (float X, float Lift, float R, int Kind)[] spots = [(0.055f, 0.070f, 0.052f, 0), (0.135f, 0.058f, 0.044f, 1), (0.205f, 0.066f, 0.048f, 2), (0.465f, 0.060f, 0.046f, 1), (0.755f, 0.068f, 0.050f, 0), (0.845f, 0.056f, 0.042f, 2)];
        foreach (var s in spots)
        {
            float x = w * s.X;
            if (MathF.Abs(x - farmX) < u * 0.16f || MathF.Abs(x - barnX) < u * 0.17f || MathF.Abs(x - scarecrowX) < u * 0.08f || MathF.Abs(x - orchardX) < orchardHalf + u * 0.06f) continue;
            float foot = Brushwork.RidgeYAt(nearRidge, x) + u * 0.014f;
            float cy = foot - u * s.Lift, r = u * s.R * (0.9f + 0.2f * (float)rng.NextDouble());
            Brushwork.Trunk(g, x, foot, cy, r, rng, bark);
            Brushwork.Canopy(g, x, cy, r, u, rng, s.Kind == 0 ? oak : s.Kind == 1 ? maple : gold);
            trees.Add((new PointF(x, cy), r));
        }
        return trees;
    }

    /// <summary>
    /// The orchard beside the farmhouse: two rows of small apple trees on the hill past the scarecrow, the back row a
    /// little higher up and smaller, each a round green canopy (the maples' own brush, in apple-leaf greens) hung with
    /// red apples, and a few windfalls in the grass under it. At the house's scale (0.0154 U a metre) a 3.5 m tree is
    /// 0.054 U tall. The trees' middles are "cx" across "half" either side; the far trees keep clear of that stretch.
    /// </summary>
    private static void PaintOrchard(Graphics g, float u, PointF[] nearRidge, float cx, float half, Random rng)
    {
        var leaf = new BlossomPalette(Color.FromArgb(240, 104, 136, 58), Color.FromArgb(56, 84, 38), Color.FromArgb(160, 186, 86));
        Color bark = Color.FromArgb(70, 50, 38), apple = Color.FromArgb(198, 44, 38), shine = Color.FromArgb(240, 120, 100), windfall = Color.FromArgb(170, 50, 40);
        using var appleBrush = new SolidBrush(apple);
        using var shineBrush = new SolidBrush(shine);
        using var fallBrush = new SolidBrush(windfall);
        // Back row first (farther up the hill, so the front row stands in front of it): two trees in the gaps between
        // the front row's three. "lift" is how far up the hill the row stands, "scale" how much smaller it is drawn.
        float spacing = half * 2 / 3f;
        foreach (var (lift, scale, count, first) in new[] { (0.022f, 0.82f, 2, 1f), (0f, 1f, 3, 0.5f) })
        {
            for (int i = 0; i < count; i++)
            {
                float x = cx - half + spacing * (i + first);
                float r = u * 0.024f * scale * (0.92f + 0.16f * (float)rng.NextDouble());
                float foot = Brushwork.RidgeYAt(nearRidge, x) + u * 0.012f - u * lift;
                float cy = foot - u * 0.030f * scale;
                Brushwork.Trunk(g, x, foot, cy, r, rng, bark);
                Brushwork.Canopy(g, x, cy, r, u, rng, leaf);
                // The apples: a dozen specks scattered through the canopy's lower two thirds, each with a glint.
                float ar = Math.Max(1f, r * 0.11f);
                for (int k = 0; k < 12; k++)
                {
                    double a = rng.NextDouble() * Math.PI * 2, d = 0.25 + 0.6 * rng.NextDouble();
                    float ax = x + (float)(Math.Cos(a) * d) * r, ay = cy + (float)(Math.Abs(Math.Sin(a)) * d) * r * 0.9f - r * 0.15f;
                    g.FillEllipse(appleBrush, ax - ar, ay - ar, ar * 2, ar * 2);
                    g.FillEllipse(shineBrush, ax - ar * 0.45f, ay - ar * 0.55f, ar * 0.6f, ar * 0.5f);
                }
                // Windfalls in the grass under the tree.
                for (int k = 0; k < 4; k++)
                {
                    float fx = x + r * (1.4f * (float)rng.NextDouble() - 0.7f), fy = foot - u * 0.002f + u * 0.004f * (float)rng.NextDouble();
                    g.FillEllipse(fallBrush, fx - ar * 0.8f, fy - ar * 0.8f, ar * 1.6f, ar * 1.6f);
                }
            }
        }
    }

    internal static void PaintBale(Graphics g, float x, float footY, float r, Random rng)
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
    /// A scarecrow on a pole, arms out on a crossbar: a slouching plaid shirt
    /// stuffed round at the belly, sleeves that droop a little at the cuffs,
    /// denim overalls with two straps and a chest pocket, trouser legs that
    /// flare where they are tied, straw bursting out at the cuffs, the collar
    /// and the ankles, a burlap sack for a head with stitched eyes and a
    /// stitched smile, and a straw hat with a sagging brim. The scarf is NOT
    /// painted here: it blows in the breeze, so the scene stamps it every
    /// frame at ScarecrowNeck.
    ///
    /// Every point is (across, up) from the pole's foot as a fraction of the
    /// scarecrow's height ("tall"), so 0.8 up is the middle of the head. The
    /// head, its face and the hat are measured exactly as ScarecrowWink
    /// repaints them: change them together or the wink shows a seam.
    /// </summary>
    private static void PaintScarecrow(Graphics g, float x, float footY, float tall, float u)
    {
        PointF P(float across, float up) => new(x + tall * across, footY - tall * up);
        Color shirt = Color.FromArgb(158, 98, 54), shirtDark = Color.FromArgb(118, 70, 40), plaidLine = Color.FromArgb(110, 70, 36, 22);
        Color denim = Color.FromArgb(58, 66, 98), denimDark = Color.FromArgb(44, 50, 78), straw = Color.FromArgb(228, 194, 104), strawDark = Color.FromArgb(190, 150, 70);
        using var pole = new Pen(Wood, Math.Max(2f, tall * 0.035f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var strawPen = new Pen(straw, Math.Max(1f, tall * 0.014f)) { EndCap = LineCap.Round };
        using var strawPen2 = new Pen(strawDark, Math.Max(1f, tall * 0.012f)) { EndCap = LineCap.Round };
        using var stitch = new Pen(Color.FromArgb(60, 40, 30), Math.Max(1f, tall * 0.012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        // A spray of straw from a point: "n" strands fanned round the "dir" angle (radians, 0 = right, up positive).
        void Spray(PointF from, float dir, float spread, float length, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = dir + spread * ((i / (n - 1f)) - 0.5f), l = length * (0.75f + 0.25f * ((i * 7) % 3) / 2f);
                g.DrawLine(i % 2 == 0 ? strawPen : strawPen2, from, new PointF(from.X + tall * l * MathF.Cos(a), from.Y - tall * l * MathF.Sin(a)));
            }
        }

        // The pole and the crossbar, behind everything.
        g.DrawLine(pole, P(0, 0), P(0, 0.72f));
        g.DrawLine(pole, P(-0.30f, 0.62f), P(0.30f, 0.62f));

        // Straw out of the trouser cuffs, then the legs over it; straw out of the sleeves, then the sleeves.
        Spray(P(-0.08f, 0.20f), -MathF.PI / 2 - 0.25f, 1.1f, 0.12f, 6);
        Spray(P(0.08f, 0.20f), -MathF.PI / 2 + 0.25f, 1.1f, 0.12f, 6);
        Spray(P(-0.27f, 0.60f), MathF.PI + 0.1f, 1.3f, 0.10f, 7);
        Spray(P(0.27f, 0.60f), -0.1f, 1.3f, 0.10f, 7);

        // Trouser legs: two rounded tubes from the hips, flaring at the tied cuffs.
        Blob([P(-0.13f, 0.50f), P(-0.02f, 0.50f), P(-0.03f, 0.22f), P(-0.06f, 0.19f), P(-0.14f, 0.19f), P(-0.15f, 0.30f)], denim, 0.35f);
        Blob([P(0.02f, 0.50f), P(0.13f, 0.50f), P(0.15f, 0.30f), P(0.14f, 0.19f), P(0.06f, 0.19f), P(0.03f, 0.22f)], denim, 0.35f);
        // The shirt: a stuffed, rounded body, and two sleeves drooping along the crossbar.
        Blob([P(-0.16f, 0.72f), P(0.16f, 0.72f), P(0.19f, 0.60f), P(0.18f, 0.48f), P(0.10f, 0.44f), P(-0.10f, 0.44f), P(-0.18f, 0.48f), P(-0.19f, 0.60f)], shirt);
        Blob([P(-0.14f, 0.71f), P(-0.30f, 0.67f), P(-0.31f, 0.56f), P(-0.26f, 0.53f), P(-0.13f, 0.53f)], shirt, 0.4f);
        Blob([P(0.14f, 0.71f), P(0.30f, 0.67f), P(0.31f, 0.56f), P(0.26f, 0.53f), P(0.13f, 0.53f)], shirt, 0.4f);
        // Plaid: lines both ways, clipped to the shirt and sleeves by drawing only across their extent.
        using (var plaid = new Pen(plaidLine, Math.Max(1f, tall * 0.008f)))
        {
            GraphicsState before = g.Save();
            using var clip = new GraphicsPath();
            clip.AddClosedCurve([P(-0.16f, 0.72f), P(0.16f, 0.72f), P(0.19f, 0.60f), P(0.18f, 0.48f), P(0.10f, 0.44f), P(-0.10f, 0.44f), P(-0.18f, 0.48f), P(-0.19f, 0.60f)], 0.5f);
            clip.AddClosedCurve([P(-0.14f, 0.71f), P(-0.30f, 0.67f), P(-0.31f, 0.56f), P(-0.26f, 0.53f), P(-0.13f, 0.53f)], 0.4f);
            clip.AddClosedCurve([P(0.14f, 0.71f), P(0.30f, 0.67f), P(0.31f, 0.56f), P(0.26f, 0.53f), P(0.13f, 0.53f)], 0.4f);
            g.SetClip(clip, CombineMode.Intersect);
            for (int i = 0; i < 6; i++)
            {
                g.DrawLine(plaid, P(-0.32f, 0.44f + i * 0.058f), P(0.32f, 0.44f + i * 0.058f));
                g.DrawLine(plaid, P(-0.30f + i * 0.12f, 0.42f), P(-0.30f + i * 0.12f, 0.74f));
            }
            using var shade = new SolidBrush(Color.FromArgb(70, 60, 30, 20));
            g.FillRectangle(shade, P(0.06f, 0.74f).X, P(0.06f, 0.74f).Y, tall * 0.30f, tall * 0.32f);   // the side away from the dusk light
            g.Restore(before);
        }
        // Overalls: a bib over the shirt with two straps and a pocket, and a patch on one knee.
        Blob([P(-0.11f, 0.64f), P(0.11f, 0.64f), P(0.12f, 0.48f), P(-0.12f, 0.48f)], denim, 0.2f);
        using (var strap = new Pen(denimDark, Math.Max(1.5f, tall * 0.03f)))
        {
            g.DrawLine(strap, P(-0.08f, 0.64f), P(-0.05f, 0.72f));
            g.DrawLine(strap, P(0.08f, 0.64f), P(0.05f, 0.72f));
        }
        using (var pocket = new Pen(denimDark, Math.Max(1f, tall * 0.01f)))
            g.DrawRectangle(pocket, P(-0.05f, 0.61f).X, P(-0.05f, 0.61f).Y, tall * 0.10f, tall * 0.07f);
        using (var button = new SolidBrush(Color.FromArgb(190, 160, 90)))
            foreach (float bx in (float[])[-0.08f, 0.08f])
                g.FillEllipse(button, P(bx, 0.645f).X - tall * 0.014f, P(bx, 0.645f).Y - tall * 0.014f, tall * 0.028f, tall * 0.028f);
        using (var patch = new SolidBrush(Color.FromArgb(150, 60, 48)))
            g.FillRectangle(patch, P(0.045f, 0.34f).X, P(0.045f, 0.34f).Y, tall * 0.07f, tall * 0.06f);
        using (var patch2 = new SolidBrush(Color.FromArgb(72, 92, 120)))
            g.FillRectangle(patch2, P(-0.28f, 0.64f).X, P(-0.28f, 0.64f).Y, tall * 0.06f, tall * 0.05f);
        // Straw at the collar, bursting up round the neck.
        Spray(P(0, 0.71f), MathF.PI / 2, 2.2f, 0.07f, 9);

        // The head: a burlap sack, tied at the neck, with a stitched face. (Kept to the pixel for ScarecrowWink.)
        using (var sack = new SolidBrush(Color.FromArgb(204, 168, 110)))
            g.FillEllipse(sack, x - tall * 0.11f, footY - tall * 0.91f, tall * 0.22f, tall * 0.23f);
        g.DrawLine(stitch, P(-0.04f, 0.83f), P(-0.02f, 0.80f)); g.DrawLine(stitch, P(-0.02f, 0.83f), P(-0.04f, 0.80f));   // an X for each eye
        g.DrawLine(stitch, P(0.02f, 0.83f), P(0.04f, 0.80f)); g.DrawLine(stitch, P(0.04f, 0.83f), P(0.02f, 0.80f));
        g.DrawBezier(stitch, P(-0.05f, 0.75f), P(-0.02f, 0.72f), P(0.02f, 0.72f), P(0.05f, 0.75f));                      // the smile
        foreach (float sx in (float[])[-0.03f, 0f, 0.03f]) g.DrawLine(stitch, P(sx, 0.725f), P(sx, 0.745f));           // its stitches

        // The straw hat: a wide brim and a crown, a little askew. (Kept for ScarecrowWink too.)
        GraphicsState hatState = g.Save();
        g.TranslateTransform(P(0, 0.90f).X, P(0, 0.90f).Y);
        g.RotateTransform(-6);
        using (var hat = new SolidBrush(Color.FromArgb(214, 176, 92)))
        {
            g.FillEllipse(hat, -tall * 0.17f, -tall * 0.025f, tall * 0.34f, tall * 0.05f);
            g.FillPolygon(hat, [new PointF(-tall * 0.10f, 0), new PointF(-tall * 0.08f, -tall * 0.10f), new PointF(tall * 0.08f, -tall * 0.10f), new PointF(tall * 0.10f, 0)]);
        }
        using (var band = new Pen(Color.FromArgb(110, 60, 40), Math.Max(1f, tall * 0.012f)))
            g.DrawLine(band, -tall * 0.095f, -tall * 0.02f, tall * 0.095f, -tall * 0.02f);
        using (var weave = new Pen(Color.FromArgb(70, 160, 120, 50), Math.Max(1f, tall * 0.006f)))
            for (int i = 1; i < 4; i++)
                g.DrawLine(weave, -tall * 0.085f, -tall * 0.10f + tall * 0.022f * i, tall * 0.085f, -tall * 0.10f + tall * 0.022f * i);
        g.Restore(hatState);
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

    /// <summary>One thing in the pumpkin patch: where its foot is, how big, what it is, which band it is in, and its tint.</summary>
    internal sealed class PatchItem
    {
        public PointF Foot;
        public float R;
        public int Kind;                 // 0 a pumpkin, 1 a green striped gourd, 2 a knobbly yellow gourd, 3 a leaf
        public bool Front;               // in front of the walk line (painted after the maples) or behind it
        public int Tint;                 // a pumpkin's colour, 0 orange, 1 deep, 2 pale
        public bool Jack;                // the carved one
        public float Turn;               // a leaf's turn, degrees
    }

    /// <summary>
    /// Plans the patch: pumpkins, gourds and leaves scattered over the whole near ground by chance with a clearance
    /// test, bigger the nearer they stand, in two bands that leave the WALK LINE clear. Behind the line (a foot
    /// at least 0.04 U above it) they are small; in front of it, across the stage (left of "stageRight"), a
    /// pumpkin's TOP must stay below the line plus a little, so the story's actors are never hidden behind one
    /// (on the right there is no stage, so they may stand anywhere in front). Nothing stands near the hollow or
    /// the nut pile, or in the shock's foot. One pumpkin in the front band, fully on screen,
    /// right of the middle, is the jack-o'-lantern.
    /// </summary>
    private static List<PatchItem> PlanPatch(int w, int h, float u, Bank ground, Random rng, float hollowX, float stageRight, float shockX, float shockHalf)
    {
        var items = new List<PatchItem>();
        float Walk(float x) => ground.YAt(Math.Clamp(x, 0, w - 1)) + u * 0.052f;
        float Crest(float x) => ground.YAt(Math.Clamp(x, 0, w - 1));
        float RAt(float x, float y) => 0.018f * u + 0.047f * u * Math.Clamp((y - Crest(x)) / (h + 0.05f * u - Crest(x)), 0f, 1f);

        bool Allowed(float x, float y, float r, float top, out bool front)
        {
            float walk = Walk(x);
            front = y > walk;
            if (!front && y > walk - 0.024f * u) return false;                                  // straddling the lane (an actor's feet are 0.024 U lower: it is the nearer, drawn over)
            if (front && x < stageRight && y - top < walk + 0.012f * u) return false;             // on the stage: the top stays under the lane
            if (front && y < walk + 0.02f * u) return false;
            if (!front && x > hollowX - 0.14f * u && x < hollowX + 0.07f * u) return false;      // the hollow and the nut pile
            if (MathF.Abs(x - shockX) < shockHalf + r && MathF.Abs(y - Walk(shockX)) < 0.06f * u) return false;   // in the shock's foot
            return true;
        }
        bool Clear(float x, float y, float r)
        {
            foreach (PatchItem o in items)
            {
                if (o.Kind == 3) continue;
                float dx = MathF.Abs(o.Foot.X - x), dy = MathF.Abs(o.Foot.Y - y);
                if (dx < (o.R + r) * 1.15f && dy < (o.R + r) * 0.9f) return false;
            }
            return true;
        }

        int wanted = (int)(22 * w / (u * 16f / 9f)) + 6;                                       // about 28 pumpkins on a 16:9 screen, more on a wider one
        int pumpkinsPlaced = 0;
        for (int tries = 0; tries < 1500 && pumpkinsPlaced < wanted; tries++)
        {
            float x = -0.02f * w + 1.04f * w * (float)rng.NextDouble();
            // Half the tries go to the thin band behind the lane, or chance alone would leave it nearly empty.
            float y = tries % 2 == 0
                ? Crest(x) + 0.012f * u + (0.016f * u) * (float)rng.NextDouble()
                : Crest(x) + 0.012f * u + (h + 0.06f * u - Crest(x) - 0.012f * u) * (float)rng.NextDouble();
            float r = RAt(x, y) * (0.85f + 0.3f * (float)rng.NextDouble());
            if (!Allowed(x, y, r, 1.9f * r, out bool front) || !Clear(x, y, r)) continue;
            int kind = rng.NextDouble() < 0.82 ? 0 : 1 + rng.Next(2);
            if (kind != 0) r *= 0.55f;
            items.Add(new PatchItem { Foot = new PointF(x, y), R = r, Kind = kind, Front = front, Tint = rng.Next(3) });
            if (kind == 0) pumpkinsPlaced++;
        }
        // The leaves: broad, between the pumpkins, never on the lane.
        int leavesPlaced = 0;
        for (int tries = 0; tries < 800 && leavesPlaced < wanted * 2; tries++)
        {
            float x = -0.02f * w + 1.04f * w * (float)rng.NextDouble();
            float y = Crest(x) + 0.01f * u + (h + 0.03f * u - Crest(x) - 0.01f * u) * (float)rng.NextDouble();
            float r = RAt(x, y) * 0.6f * (0.8f + 0.4f * (float)rng.NextDouble());
            if (!Allowed(x, y, r, 0.8f * r, out bool front)) continue;
            items.Add(new PatchItem { Foot = new PointF(x, y), R = r, Kind = 3, Front = front, Turn = (float)(rng.NextDouble() * 360) });
            leavesPlaced++;
        }
        // The jack-o'-lantern: the front-band pumpkin nearest x = 0.66 w that is whole on the screen; made if there is none.
        PatchItem? best = null;
        foreach (PatchItem o in items)
            if (o.Kind == 0 && o.Front && o.Foot.Y <= h - 0.005f * u && o.Foot.X > 0.50f * w && o.Foot.X < 0.85f * w && o.R > 0.035f * u
                && (best == null || MathF.Abs(o.Foot.X - 0.66f * w) < MathF.Abs(best.Foot.X - 0.66f * w))) best = o;
        if (best == null)
        {
            best = new PatchItem { Foot = new PointF(0.66f * w, h - 0.02f * u), R = 0.052f * u, Kind = 0, Front = true, Tint = 0 };
            items.RemoveAll(o => o.Kind != 3 && MathF.Abs(o.Foot.X - best.Foot.X) < (o.R + best.R) * 1.15f && MathF.Abs(o.Foot.Y - best.Foot.Y) < (o.R + best.R) * 0.9f);
            items.Add(best);
        }
        best.Jack = true; best.Tint = 0;
        // Far to near within each band: the painter's order.
        items.Sort((a, b) => a.Foot.Y.CompareTo(b.Foot.Y));
        return items;
    }

    /// <summary>Vine runners: thin dark-green lines joining each pumpkin to its nearest neighbour in its band, with a curling tendril off each.</summary>
    private static void PaintVineRunners(Graphics g, List<PatchItem> patch, float u)
    {
        using var vine = new Pen(Color.FromArgb(70, 96, 40), Math.Max(1f, u * 0.0025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        foreach (PatchItem a in patch)
        {
            if (a.Kind != 0) continue;
            PatchItem? near = null; float best = float.MaxValue;
            foreach (PatchItem b in patch)
            {
                if (b == a || b.Kind != 0 || b.Front != a.Front) continue;
                float d = (a.Foot.X - b.Foot.X) * (a.Foot.X - b.Foot.X) + (a.Foot.Y - b.Foot.Y) * (a.Foot.Y - b.Foot.Y);
                if (d < best) { best = d; near = b; }
            }
            if (near == null || best > (0.25f * u) * (0.25f * u)) continue;
            float mx = (a.Foot.X + near.Foot.X) / 2, my = (a.Foot.Y + near.Foot.Y) / 2 + u * 0.01f;
            g.DrawBezier(vine, a.Foot.X, a.Foot.Y, mx, my + u * 0.008f, mx, my - u * 0.008f, near.Foot.X, near.Foot.Y);
            g.DrawArc(vine, mx, my - a.R * 0.3f, a.R * 0.3f, a.R * 0.3f, 90, 270);
        }
    }

    /// <summary>Paints one thing of the patch into the picture, and the same shape flat into the depth picture in its grey of nearness.</summary>
    private static void PaintPatchItem(Graphics g, Graphics gd, PatchItem item, float u, byte near, List<(PointF At, float R)> pumpkins, ref (PointF At, float R) jack)
    {
        Color flat = Color.FromArgb(near, near, near);
        switch (item.Kind)
        {
            case 0:
                var at = PaintPumpkin(g, item.Foot.X, item.Foot.Y, item.R, item.Tint);
                PaintPumpkin(gd, item.Foot.X, item.Foot.Y, item.R, item.Tint, flat);
                pumpkins.Add(at);
                if (item.Jack) { CarveFace(g, at.At.X, at.At.Y, item.R); jack = at; }
                break;
            case 1:
            case 2:
                PaintGourd(g, item.Foot.X, item.Foot.Y, item.R, item.Kind);
                PaintGourd(gd, item.Foot.X, item.Foot.Y, item.R, item.Kind, flat);
                break;
            default:
                PaintLeaf(g, item.Foot.X, item.Foot.Y, item.R, item.Turn);
                PaintLeaf(gd, item.Foot.X, item.Foot.Y, item.R, item.Turn, flat);
                break;
        }
    }

    /// <summary>
    /// A pumpkin standing on the ground at (x, footY): three overlapping orange
    /// lobes (which is what gives a pumpkin its ribs), a stubby stem, and a
    /// paler highlight on the lit side. "tint" picks orange, deep or pale.
    /// With "flat" set, every shape is painted in that one colour and the
    /// shadow is left out (that is the depth picture). Returns its center and size.
    /// </summary>
    internal static (PointF At, float R) PaintPumpkin(Graphics g, float x, float footY, float r, int tint = 0, Color? flat = null)
    {
        float cy = footY - r * 0.8f;
        (Color side, Color middle) = tint switch
        {
            1 => (Color.FromArgb(188, 78, 16), Color.FromArgb(218, 106, 26)),
            2 => (Color.FromArgb(226, 138, 50), Color.FromArgb(246, 170, 78)),
            _ => (Color.FromArgb(208, 96, 22), Color.FromArgb(236, 124, 34)),
        };
        if (flat == null)
            using (var shadow = new SolidBrush(Color.FromArgb(60, 60, 30, 10)))
                g.FillEllipse(shadow, x - r * 1.1f, footY - r * 0.22f, r * 2.3f, r * 0.45f);
        using (var stem = new SolidBrush(flat ?? Color.FromArgb(80, 92, 40)))
            g.FillPolygon(stem, [
                new PointF(x - r * 0.10f, cy - r * 0.70f), new PointF(x - r * 0.02f, cy - r * 1.10f),
                new PointF(x + r * 0.16f, cy - r * 1.05f), new PointF(x + r * 0.12f, cy - r * 0.70f)]);
        using (var sb = new SolidBrush(flat ?? side))
        {
            g.FillEllipse(sb, x - r * 1.0f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
            g.FillEllipse(sb, x - r * 0.1f, cy - r * 0.76f, r * 1.1f, r * 1.52f);
        }
        using (var mb = new SolidBrush(flat ?? middle))
            g.FillEllipse(mb, x - r * 0.55f, cy - r * 0.80f, r * 1.1f, r * 1.60f);
        if (flat == null)
            using (var hi = new SolidBrush(Color.FromArgb(70, 255, 210, 150)))
                g.FillEllipse(hi, x - r * 0.45f, cy - r * 0.62f, r * 0.4f, r * 0.7f);
        return (new PointF(x, cy), r);
    }

    /// <summary>
    /// The jack-o'-lantern's face, carved in candle yellow: the Halloween saver's own face (the homage), two
    /// triangle eyes, a nose and a jagged grin. Every point is (across, down) from the pumpkin's centre in units of r.
    /// </summary>
    private static void CarveFace(Graphics g, float x, float cy, float r)
    {
        PointF P(float ax, float ay) => new(x + r * ax, cy + r * ay);
        using var candle = new SolidBrush(Color.FromArgb(255, 222, 110));
        g.FillPolygon(candle, [P(-0.52f, -0.12f), P(-0.30f, -0.42f), P(-0.14f, -0.12f)]);   // left eye
        g.FillPolygon(candle, [P(0.14f, -0.12f), P(0.30f, -0.42f), P(0.52f, -0.12f)]);      // right eye
        g.FillPolygon(candle, [P(-0.08f, 0.10f), P(0f, -0.06f), P(0.08f, 0.10f)]);          // nose
        g.FillPolygon(candle, [                                                             // a jagged grin
            P(-0.62f, 0.16f), P(-0.40f, 0.26f), P(-0.30f, 0.38f), P(-0.18f, 0.27f), P(0.02f, 0.27f),
            P(0.12f, 0.39f), P(0.24f, 0.27f), P(0.40f, 0.26f), P(0.62f, 0.16f),
            P(0.36f, 0.50f), P(0f, 0.58f), P(-0.36f, 0.50f)]);
    }

    /// <summary>A gourd on the ground at (x, footY), "r" its size: kind 1 a striped green one, kind 2 a knobbly yellow one, each a pair of ovals.</summary>
    private static void PaintGourd(Graphics g, float x, float footY, float r, int kind, Color? flat = null)
    {
        float gy = footY - r * 0.1f;
        if (kind == 1)
        {
            using (var green = new SolidBrush(flat ?? Color.FromArgb(120, 150, 60)))
            {
                g.FillEllipse(green, x - r * 1.3f, gy - r * 1.1f, r * 2.6f, r * 1.3f);
                g.FillEllipse(green, x - r * 0.8f, gy - r * 1.8f, r * 1.4f, r * 1.2f);
            }
            if (flat == null)
                using (var stripe = new Pen(Color.FromArgb(120, 230, 220, 120), Math.Max(1f, r * 0.12f)))
                    for (int i = -2; i <= 2; i++)
                        g.DrawArc(stripe, x - r * 0.5f + i * r * 0.5f, gy - r * 1.1f, r, r * 1.3f, 180, 180);
        }
        else
        {
            using (var yellow = new SolidBrush(flat ?? Color.FromArgb(226, 190, 60)))
            {
                g.FillEllipse(yellow, x - r * 1.2f, gy - r * 1.2f, r * 2.4f, r * 1.4f);
                g.FillEllipse(yellow, x - r * 0.7f, gy - r * 2.0f, r * 1.3f, r * 1.3f);
            }
            if (flat == null)
                using (var knob = new SolidBrush(Color.FromArgb(120, 100, 50)))
                    foreach (var (kx, ky) in new[] { (-0.6f, -0.9f), (0.3f, -1.5f), (0.7f, -0.6f) })
                        g.FillEllipse(knob, x + r * kx - r * 0.12f, gy + r * ky - r * 0.12f, r * 0.24f, r * 0.24f);
        }
    }

    /// <summary>A broad pumpkin leaf lying on the ground at (x, y), "r" across, turned by "turn": a heart with a notch, a paler vein, a stalk.</summary>
    private static void PaintLeaf(Graphics g, float x, float y, float r, float turn, Color? flat = null)
    {
        var st = g.Save();
        g.TranslateTransform(x, y);
        g.RotateTransform(turn);
        g.ScaleTransform(1f, 0.55f);                                   // lying flat on the ground, seen from a low angle
        using (var leaf = new SolidBrush(flat ?? Color.FromArgb(84, 116, 46)))
        using (var path = new GraphicsPath())
        {
            path.AddClosedCurve([new PointF(0, -r * 0.1f), new PointF(r * 0.55f, -r * 0.7f), new PointF(r, -r * 0.2f), new PointF(r * 0.8f, r * 0.5f), new PointF(0, r),
                                 new PointF(-r * 0.8f, r * 0.5f), new PointF(-r, -r * 0.2f), new PointF(-r * 0.55f, -r * 0.7f)], 0.5f);
            g.FillPath(leaf, path);
        }
        if (flat == null)
        {
            using var vein = new Pen(Color.FromArgb(110, 150, 70), Math.Max(1f, r * 0.08f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(vein, 0, -r * 0.1f, 0, r * 0.85f);
            g.DrawLine(vein, 0, r * 0.3f, r * 0.5f, -r * 0.1f);
            g.DrawLine(vein, 0, r * 0.3f, -r * 0.5f, -r * 0.1f);
            using var stalk = new Pen(Color.FromArgb(70, 96, 40), Math.Max(1f, r * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(stalk, 0, -r * 0.1f, 0, -r * 0.6f);
        }
        g.Restore(st);
    }

    /// <summary>
    /// Maples at both sides in full autumn colour, each standing on the ground
    /// under it. They are the sakura trees (same trunk, same cloud of dots)
    /// in a maple palette: crimson, orange and gold. Returns random spots
    /// inside the canopies, so the falling leaves can let go of the trees
    /// they belong to, and each canopy's center and size.
    /// </summary>
    private static (List<PointF> LeafSpots, List<(PointF At, float R)> Canopies, (PointF At, float R) Hollow, PointF[] LeftTrunk, PointF[] RightTrunk) PaintMaples(Graphics g, int w, float u, Bank ground, Random rng)
    {
        var maple = new BlossomPalette(Color.FromArgb(240, 204, 92, 30), Color.FromArgb(158, 38, 24), Color.FromArgb(250, 196, 60));
        Color bark = Color.FromArgb(62, 44, 36);

        // (X as a fraction of the width, canopy center height above the ground in u, canopy size in u)
        (float X, float Lift, float R)[] trees =
        [
            (0.03f, 0.17f, 0.15f), (0.12f, 0.13f, 0.11f), (0.90f, 0.14f, 0.12f), (0.98f, 0.17f, 0.14f),
        ];
        var trunks = new PointF[trees.Length][];
        for (int i = 0; i < trees.Length; i++)
        {
            var t = trees[i];
            trunks[i] = Brushwork.Trunk(g, w * t.X, ground.YAt(w * t.X) + u * 0.03f, ground.YAt(w * t.X) - u * t.Lift, u * t.R, rng, bark);
        }

        // The squirrel's home: the inner left maple's roots flare out at the
        // foot, and between them is a dark hollow (the opening is painted as an
        // arch, black at the back with a little bark lip). The caper's squirrel
        // comes out of it and dives back in.
        (PointF At, float R) hollow;
        {
            float hx = w * trees[1].X, foot = ground.YAt(hx) + u * 0.03f;
            float hw = u * 0.013f, hh = u * 0.026f;
            // Two roots, one each side, like knuckles pressed into the ground: a rounded hump that
            // rises from the ground and meets the trunk, the lit side a shade paler.
            using (var root = new SolidBrush(bark))
            using (var paler = new SolidBrush(Color.FromArgb(82, 60, 48)))
            {
                g.FillEllipse(root, hx - u * 0.030f, foot - u * 0.030f, u * 0.036f, u * 0.040f);
                g.FillEllipse(root, hx - u * 0.006f, foot - u * 0.030f, u * 0.036f, u * 0.040f);
                g.FillEllipse(paler, hx - u * 0.027f, foot - u * 0.026f, u * 0.012f, u * 0.024f);
                g.FillRectangle(root, hx - u * 0.030f, foot - u * 0.002f, u * 0.060f, u * 0.006f);   // the roots meet the ground on one line
            }
            // Between them the opening: an arch of warm dark, blacker at the bottom where no light reaches.
            using (var dark = new SolidBrush(Color.FromArgb(44, 26, 16)))
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([new PointF(hx - hw, foot + u * 0.003f), new PointF(hx - hw * 0.92f, foot - hh * 0.6f), new PointF(hx, foot - hh), new PointF(hx + hw * 0.92f, foot - hh * 0.6f), new PointF(hx + hw, foot + u * 0.003f)], 0.35f);
                g.FillPath(dark, path);
            }
            using (var deep = new SolidBrush(Color.FromArgb(16, 10, 8)))
                g.FillEllipse(deep, hx - hw * 0.8f, foot - hh * 0.55f, hw * 1.6f, hh * 0.6f);
            hollow = (new PointF(hx, foot - hh * 0.45f), hw);
        }

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
        return (spots, canopies, hollow, trunks[1], trunks[2]);
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
