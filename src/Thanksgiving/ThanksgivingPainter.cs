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
    public required (PointF At, float R) Sun { get; init; }          // the setting sun's center and radius (its lower part is behind the far hills)
    public required bool[] OpenSky { get; init; }                    // stencil: true where nothing was painted over the sky, sun and clouds
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
    // ---- The yard and the stock on the hill (FarmYard.cs lives here). Every pen is (X0, X1, Back, Front): its left and right edge and the y of its back line and its front line; an animal stands between the two. ----
    public required PointF[] Road { get; init; }                     // the dirt road's middle line, porch to barn door, left to right
    public required PointF Doghouse { get; init; }                   // the doghouse's foot (its doorway's middle is here; 0.024 U wide, 0.014 U to the eaves)
    public required (PointF At, float Rx, float Ry) Pond { get; init; }   // the pond on the far slope: centre and half sizes
    public required (float X0, float X1, float Back, float Front) Paddock { get; init; }   // the horses', past the silos
    public required (float X0, float X1, float Back, float Front) PigPen { get; init; }    // the pigs', between the orchard and the windmill
    public required (float X0, float X1, float Back, float Front) Pasture { get; init; }   // the cows', among the trees left of the house (no fence)
    public required (float X0, float X1, float Back, float Front) HenYard { get; init; }   // the hens', round the scarecrow between the porch and the doghouse
    /// <summary>How many U a metre is on the farm's hill (the house is 0.0154 U a metre: 7 m to its eaves); the stock and the dog are sized from it.</summary>
    public float HillMetresToU => 0.0154f;

    // ---- The barn and silo (on the near hills, right of middle) ----
    public required PointF BarnFoot { get; init; }                   // the middle of the barn's base
    public required SizeF BarnWall { get; init; }                    // its wall's width and height (the roof rises 0.045 U above the wall)
    public required RectangleF BarnLoft { get; init; }               // the loft window, high in the gable
    public required RectangleF BarnDoor { get; init; }
    public required List<(PointF At, float R)> SiloTops { get; init; }  // the three silos' caps: the apex of each, and the tube's half width (a turkey roosts on one)
    public required (PointF Hub, float R) Windmill { get; init; }       // the water-pump windmill left of the barn: the wheel's hub and radius (the scene turns the wheel)
    public required (PointF A, PointF B) Clothesline { get; init; }     // the clothesline left of the farmhouse: the line's two ends (the scene hangs the washing)

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
    public required List<(PointF A, PointF B, float Width)> BranchTimber { get; init; }   // every piece of the corner branches' wood, as the painter grew it: a round-capped bar from A to B, Width thick. The TOP EDGE a thing stands on is the line A to B lifted by Width / 2 (BranchTopY). Pieces at least U * 0.009 wide bear a squirrel; a sparrow stands on any of them.
    /// <summary>The y of the top edge of the thickest branch piece under x (the surface a bird or a squirrel stands on there), or null if no piece spans that x.</summary>
    public float? BranchTopY(float x, float minWidth = 0f)
    {
        float? best = null; float bestW = -1f;
        foreach (var (a, b, wd) in BranchTimber)
        {
            if (wd < minWidth) continue;
            float x0 = MathF.Min(a.X, b.X), x1 = MathF.Max(a.X, b.X);
            if (x < x0 || x > x1 || x1 - x0 < 0.5f) continue;
            float f = (x - a.X) / (b.X - a.X), y = a.Y + (b.Y - a.Y) * f - wd / 2f;
            if (best == null || y < best.Value || (wd > bestW && MathF.Abs(y - best.Value) < wd)) { best = y; bestW = wd; }
        }
        return best;
    }
    public required (PointF At, float R) LeftMaple { get; init; }    // the inner maple on each side (canopy centre and size); its trunk stands at At.X. The squirrels' trees.
    public required (PointF At, float R) RightMaple { get; init; }
    public required PointF[] LeftTrunk { get; init; }                // each inner maple's trunk as a line to climb: [0] the foot on the ground, [1] the fork, [2] the end of a limb inside the canopy
    public required PointF[] RightTrunk { get; init; }
    public required (PointF At, float R) Hollow { get; init; }       // the hollow in the roots of the left maple: the centre of its dark opening and its half width. A squirrel lives in it.
    public required List<(PointF At, float R)> TreeHoles { get; init; }   // a hole up each inner maple's trunk (the centre of its dark opening and its half width), [0] on the left trunk, [1] on the right: where the patch squirrels den

    // ---- Live facts, set by the scene every frame before the happenings draw ----
    public NutField? Nuts { get; set; }                              // the acorns on the near ground (Nuts.cs): the AcornDrop happening adds to it, the squirrels take from it
    public PointF GeeseLead { get; set; }                            // where the lead goose is
    public bool GeeseOnScreen { get; set; }                          // false while the V is on its off-screen pause
}

/// <summary>
/// Paints the backdrop: a harvest sunset (violet overhead, through rose and
/// orange to gold at the horizon), the sun itself sinking into the far hills,
/// two ranges of hills with a white farmhouse and a
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
/// one, so trees and the sun do not swallow a portrait monitor.
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
        // The sun sits low at the right, its centre a little above the horizon line, so the far ridge (painted next)
        // covers its lower part: a sun half behind the hills is what "sunset" looks like (his ask, 2026-11-06: "let's
        // make this at sunset not with the moon").
        var sun = (At: new PointF(w * 0.74f, horizon - u * 0.13f), R: u * 0.075f);   // the far ridge there rises about 0.08 U, so the hills take the lower third
        PaintSun(g, sun.At.X, sun.At.Y, sun.R);
        PaintClouds(g, w, h, u, rng);
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);   // the copy the sky stencil is made from (see the class summary)

        PointF[] farRidge = Brushwork.Ridge(w, horizon, u, 0.08f, 0.05f, 0.02f, 0.07f, rng);
        using (var far = new SolidBrush(Color.FromArgb(112, 62, 84)))
            g.FillPolygon(far, farRidge);
        PaintTreeline(g, w, u, farRidge, rng);
        PointF[] nearRidge = Brushwork.Ridge(w, horizon, u, 0.025f, 0.03f, 0.012f, 0.035f, rng);
        // The near ridge swings about 0.04 U either way with the seed, and where it dipped under a pen the rails landed
        // on the field's edge while the yard lifted its animals above them (seed 3: both horses on the far hill behind
        // their own fence, "they are often on the wrong side of their fence"). So the hill has a KNOLL under the
        // paddock and under the pig pen: the ridge is held up to 0.040 U there, blended in over 0.06 U each side.
        // The ridge is the one owner of the ground: the rails, the pens' bands and the bales all ask it.
        float knollBarn = w * 0.60f, knollFarm = w * 0.31f;
        Knoll(nearRidge, knollBarn + u * 0.16f, knollBarn + u * 0.31f, horizon, u);                             // the paddock
        Knoll(nearRidge, knollFarm + u * 0.282f, knollBarn - u * 0.141f, horizon, u);                           // the pig pen
        using (var near = new SolidBrush(Color.FromArgb(126, 78, 54)))
        {
            g.FillPolygon(near, nearRidge);
            g.FillRectangle(near, 0, horizon, w, h - horizon);   // the valley floor, down to where the field covers it
        }

        PaintFieldFloor(g, w, h, u, horizon);

        // The farm stands on the nearer hills, each building's foot a touch below the ridge line so it sits ON the hill.
        // (After the field's floor: a foot that is below the horizon stands on the field, not under it.)
        var farmFoot = new PointF(w * 0.31f, Brushwork.RidgeYAt(nearRidge, w * 0.31f) + u * 0.012f);
        var barnFoot = new PointF(w * 0.60f, Brushwork.RidgeYAt(nearRidge, w * 0.60f) + u * 0.012f);
        // The road first: the buildings' feet cover its ends. Then the pond, up on the far slope past the barn.
        PointF[] road = PaintRoad(g, farmFoot, barnFoot, nearRidge, u);
        // The pond is painted after the road and before the house; it lies in the pasture, so the far trees keep clear of it.
        var pond = (At: new PointF(w * 0.812f, Brushwork.RidgeYAt(nearRidge, w * 0.812f) + u * 0.017f), Rx: u * 0.050f, Ry: u * 0.0065f);   // on the hill's own band, the open stretch past the paddock (on the far slope it read as "that disc thing above the horses"; in the pasture the left maple hid it)
        PaintPond(g, pond.At, pond.Rx, pond.Ry, u);
        PaintFarmhouse(g, farmFoot.X, farmFoot.Y, u);
        // The dog's house in the yard right of the porch, past the scarecrow's garden; the hens' yard is between them.
        var doghouse = new PointF(farmFoot.X + u * 0.135f, Brushwork.RidgeYAt(nearRidge, farmFoot.X + u * 0.135f) + u * 0.016f);
        PaintDoghouse(g, doghouse, u);
        // A clothesline in the yard right beside the house, its near post just past the porch's end and its foot a
        // little up the slope from the house's (so it stands in the side yard, not out in the pasture: his line,
        // 2026-11-06, "the clothesline needs to be back by the house not out in the field"); the washing on it is
        // left out (the scene hangs the cloth, which moves in the breeze). And a steel water-pump windmill left of
        // the barn (the scene turns its wheel).
        float lineX0 = farmFoot.X - u * 0.128f, lineX1 = farmFoot.X - u * 0.094f;   // the porch ends at 0.0864 U
        (PointF A, PointF B) clothesline = PaintClothesline(g, lineX0, lineX1, farmFoot.Y - u * 0.004f, u);
        float millX = barnFoot.X - u * 0.125f;
        (PointF Hub, float R) windmill = PaintWindmill(g, millX, Brushwork.RidgeYAt(nearRidge, millX) + u * 0.012f, u);
        List<(PointF At, float R)> siloTops = PaintBarn(g, barnFoot.X, barnFoot.Y, u);
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
        // The stock (his ask: "horses, cows, pigs, chickens"): a horse paddock on the hill past the silos, a pig pen between
        // the orchard and the windmill, the cows' pasture among the trees left of the house. The back rails and boards are
        // painted here; the front ones are stamped over the animals each frame (FarmYard.cs). Each pen's floor is the band
        // between its back line and its front line.
        Color railWood = Color.FromArgb(150, 132, 104);
        float paddockX0 = barnFoot.X + u * 0.16f, paddockX1 = barnFoot.X + u * 0.31f;   // 0.15 U: room for a walk (at 0.13 U a horse's goals were all within its own length); the left end clear of the silos
        float paddockBack = Brushwork.RidgeYAt(nearRidge, (paddockX0 + paddockX1) / 2) + u * 0.012f, paddockFront = paddockBack + u * 0.016f;
        PaintRails(g, paddockX0, paddockX1, paddockBack, u, railWood);
        float penX0 = orchardX + orchardHalf + u * 0.012f, penX1 = millX - u * 0.016f;
        float penBack = Brushwork.RidgeYAt(nearRidge, (penX0 + penX1) / 2) + u * 0.012f, penFront = penBack + u * 0.014f;
        PaintPigPen(g, penX0, penX1, penBack, penFront, u);
        float pastureX0 = w * 0.09f, pastureX1 = farmFoot.X - u * 0.165f;   // ends short of the clothesline (0.128 U left of the house) by a cow's length
        // A windbreak of trees along the far edge of the field, where the hill meets it: nearer than the hills' treeline,
        // farther than everything that moves on the field (his line: "another set of trees closer to the foreground but not
        // in front of the highest branches"). Painted here, with the farm, so the lane's animals pass in front of them.
        List<(PointF At, float R)> farTrees = PaintFarTrees(g, w, u, nearRidge, farmFoot.X, barnFoot.X, scarecrowX, orchardX, orchardHalf, pond.At.X, pond.Rx, paddockX0, paddockX1, rng);
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

        var (leafSpots, canopies, hollow, leftTrunk, rightTrunk, treeHoles) = PaintMaples(g, w, u, ground, rng, sun.At.X);

        foreach (PatchItem item in patch)
            if (item.Front) PaintPatchItem(g, gd, item, u, NearnessOf(item.Foot.Y), pumpkins, ref jack);

        g.Flush();
        uint[] upToBranches = Brushwork.ToPixels(bmp);  // a fourth copy: "unchanged since here" = no corner branch in front
        List<PointF> cornerSpots = PaintCornerBranches(g, w, h, u, rng, out var branchTimber);
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

        var (wallW, wallH) = FarmWallSize(u); float farmTop = farmFoot.Y - wallH;
        float barnW = u * 0.13f, barnH = u * 0.06f, barnTop = barnFoot.Y - barnH;
        return new ThanksgivingScenery
        {
            Pixels = pixels, LeafSpots = leafSpots, Stars = stars,
            Width = w, Height = h, U = u, Horizon = horizon, Sun = sun,
            OpenSky = Brushwork.Unchanged(skyOnly, pixels),
            OpenFromFarm = Brushwork.Unchanged(upToFarm, pixels),
            OpenField = Brushwork.Unchanged(upToField, pixels),
            OpenOfBranches = Brushwork.Unchanged(upToBranches, pixels),
            OpenOfProps = Brushwork.Unchanged(upToFence, pixels),
            FarRidge = farRidge, NearRidge = nearRidge, Ground = ground,
            FarmFoot = farmFoot, FarmWall = new SizeF(wallW, wallH),
            FarmWindows = FarmWindows(farmFoot.X, farmFoot.Y, u),
            FarmDoor = FarmDoorRect(farmFoot.X, farmFoot.Y, u),
            PorchLight = FarmPorchLight(farmFoot.X, farmFoot.Y, u),
            ChimneyTop = new PointF(farmFoot.X - wallW * 0.38f, farmTop - u * 0.080f),
            Road = road, Doghouse = doghouse, Pond = pond,
            Paddock = (paddockX0, paddockX1, paddockBack, paddockFront), PigPen = (penX0, penX1, penBack, penFront),
            Pasture = (pastureX0, pastureX1, Brushwork.RidgeYAt(nearRidge, (pastureX0 + pastureX1) / 2) + u * 0.014f, Brushwork.RidgeYAt(nearRidge, (pastureX0 + pastureX1) / 2) + u * 0.030f),
            HenYard = (farmFoot.X + u * 0.085f, farmFoot.X + u * 0.125f, Brushwork.RidgeYAt(nearRidge, scarecrowX) + u * 0.012f, Brushwork.RidgeYAt(nearRidge, scarecrowX) + u * 0.024f),
            BarnFoot = barnFoot, BarnWall = new SizeF(barnW, barnH),
            BarnLoft = new RectangleF(barnFoot.X - u * 0.009f, barnTop - u * 0.026f, u * 0.018f, u * 0.016f),
            BarnDoor = new RectangleF(barnFoot.X - barnW * 0.16f, barnFoot.Y - barnH * 0.72f, barnW * 0.32f, barnH * 0.72f),
            SiloTops = siloTops,
            Windmill = windmill, Clothesline = clothesline,
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
            Canopies = canopies, CornerBranchSpots = cornerSpots, BranchTimber = branchTimber,
            LeftMaple = canopies[1], RightMaple = canopies[2], LeftTrunk = leftTrunk, RightTrunk = rightTrunk, Hollow = hollow, TreeHoles = treeHoles,
        };
    }

    /// <summary>
    /// Lifts the ridge's points between x0 and x1 up to at least 0.040 U above the horizon line, easing in over 0.06 U
    /// outside each end (a smoothstep, so the knoll has rounded shoulders, not steps). Where the ridge is already
    /// higher nothing changes.
    /// </summary>
    private static void Knoll(PointF[] ridge, float x0, float x1, float horizon, float u)
    {
        float blend = u * 0.06f;
        for (int i = 0; i < ridge.Length; i++)
        {
            float x = ridge[i].X;
            if (x < x0 - blend || x > x1 + blend) continue;
            float e = Math.Min(1f, Math.Min(x - (x0 - blend), (x1 + blend) - x) / blend);
            e = e * e * (3f - 2f * e);
            ridge[i].Y = Math.Min(ridge[i].Y, horizon - u * 0.040f * e);
        }
    }

    // ================================================================ sky

    /// <summary>
    /// A harvest sunset: a deep blue-violet overhead (the sky is still bright at sunset, so no plum and no black),
    /// through rose and a hot orange, to a wide band of gold right at the horizon where the sun is going down.
    /// No stars: the sun is still up. The old dusk sky rolled 90 stars off the scene's dice here, and every ridge,
    /// cloud and tree after it took the rolls that followed; those rolls are still taken (and thrown away) so every
    /// seed paints the same hills and trees it did before. The returned list is empty: the scene twinkles nothing.
    /// </summary>
    private static List<PointF> PaintSky(Graphics g, int w, int h, float horizon, float u, Random rng)
    {
        var stars = new List<PointF>();
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(44, 40, 96), Color.FromArgb(150, 72, 98), Color.FromArgb(232, 124, 58), Color.FromArgb(255, 214, 112)],
                Positions = [0f, 0.42f, 0.78f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        for (int i = 0; i < 90; i++)
        {
            rng.NextDouble(); rng.NextDouble(); rng.NextDouble(); rng.Next(120);   // the old stars' rolls, kept so the dice do not move
        }
        return stars;
    }

    /// <summary>
    /// The setting sun: a big disc, deep orange at its rim and paler gold at its heart (the thick evening air reddens
    /// the edge first), with a wide soft glow around it (a "path gradient": strongest at the center, nothing at the
    /// edge) that warms the sky and the hilltops it sinks behind. Painted before the hills, so they cover its lower
    /// part.
    /// </summary>
    private static void PaintSun(Graphics g, float x, float y, float r)
    {
        float glowR = r * 4.2f;
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(x - glowR, y - glowR, glowR * 2, glowR * 2);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(190, 255, 200, 100),
                SurroundColors = [Color.FromArgb(0, 255, 160, 70)],
                Blend = Sprite.SoftFalloff,   // eases out, so the glow has no visible rim
            };
            g.FillPath(glow, glowPath);
        }
        using (var discPath = new GraphicsPath())
        {
            discPath.AddEllipse(x - r, y - r, r * 2, r * 2);
            using var disc = new PathGradientBrush(discPath)
            {
                CenterColor = Color.FromArgb(255, 252, 226),
                SurroundColors = [Color.FromArgb(255, 128, 40)],
            };
            g.FillPath(disc, discPath);
        }
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
    /// <summary>The farmhouse's wall: 0.16 U wide, 0.082 U tall (two storeys at 0.0154 U a metre: a 10 m wide, 5.3 m wall).</summary>
    internal static (float W, float H) FarmWallSize(float u) => (u * 0.16f, u * 0.082f);

    /// <summary>
    /// A traditional two-storey farmhouse (his ask: "bigger, more traditional"): white clapboard, a side-gabled roof with a
    /// cross gable over the front door and a window in it, two brick chimneys, five windows with dark green shutters, a
    /// full-width porch on turned posts with a railing and three steps up to the door, a stone foundation, and the porch
    /// lamp beside the door. Everything is painted from the wall's size (FarmWallSize) so the facts the scene uses (the
    /// windows, the door, the chimney top) stay on the drawing.
    /// </summary>
    private static void PaintFarmhouse(Graphics g, float x, float footY, float u)
    {
        var (wallW, wallH) = FarmWallSize(u);
        float top = footY - wallH;
        Color white = Color.FromArgb(232, 220, 200), shade = Color.FromArgb(200, 184, 160), roof = Color.FromArgb(66, 48, 46), roofLit = Color.FromArgb(88, 66, 62);
        Color shutter = Color.FromArgb(52, 78, 58), brick = Color.FromArgb(120, 70, 56), stone = Color.FromArgb(132, 118, 102);
        float ridgeY = top - u * 0.040f, gablePeak = top - u * 0.062f;

        // Chimneys first: the roof covers their feet. One at each end of the ridge.
        using (var b = new SolidBrush(brick))
        using (var cap = new SolidBrush(Color.FromArgb(90, 52, 42)))
            foreach (float cx in new[] { x - wallW * 0.38f, x + wallW * 0.38f })
            {
                g.FillRectangle(b, cx - u * 0.008f, top - u * 0.078f, u * 0.016f, u * 0.045f);
                g.FillRectangle(cap, cx - u * 0.010f, top - u * 0.082f, u * 0.020f, u * 0.005f);
            }

        // The wall, with clapboard lines, on a stone foundation.
        using (var wall = new SolidBrush(white))
            g.FillRectangle(wall, x - wallW / 2, top, wallW, wallH + u * 0.004f);
        using (var board = new Pen(Color.FromArgb(70, 150, 130, 110), Math.Max(1f, u * 0.0012f)))
            for (float y = top + u * 0.006f; y < footY; y += u * 0.006f)
                g.DrawLine(board, x - wallW / 2, y, x + wallW / 2, y);
        using (var found = new SolidBrush(stone))
            g.FillRectangle(found, x - wallW / 2, footY - u * 0.006f, wallW, u * 0.010f);

        // The side-gabled roof: the front slope as a band from the eaves up to the ridge, lit a shade (it faces the sky).
        using (var r = new SolidBrush(roofLit))
            g.FillPolygon(r, [new PointF(x - wallW * 0.58f, top + u * 0.004f), new PointF(x - wallW * 0.50f, ridgeY), new PointF(x + wallW * 0.50f, ridgeY), new PointF(x + wallW * 0.58f, top + u * 0.004f)]);
        using (var shingle = new Pen(Color.FromArgb(60, 120, 96, 90), Math.Max(1f, u * 0.001f)))
            for (int i = 1; i < 6; i++)
            {
                float t = i / 6f, y = ridgeY + (top - ridgeY) * t;
                g.DrawLine(shingle, x - wallW * (0.50f + 0.08f * t), y, x + wallW * (0.50f + 0.08f * t), y);
            }
        using (var ridge = new Pen(roof, Math.Max(1f, u * 0.0025f))) g.DrawLine(ridge, x - wallW * 0.50f, ridgeY, x + wallW * 0.50f, ridgeY);
        // The cross gable over the door: a triangle of wall standing out of the front slope, its own roof edges dark.
        using (var face = new SolidBrush(shade))
            g.FillPolygon(face, [new PointF(x - wallW * 0.22f, top), new PointF(x, gablePeak), new PointF(x + wallW * 0.22f, top)]);
        using (var edge = new Pen(roof, Math.Max(1.5f, u * 0.004f)) { LineJoin = LineJoin.Round })
            g.DrawLines(edge, [new PointF(x - wallW * 0.26f, top + u * 0.003f), new PointF(x, gablePeak - u * 0.003f), new PointF(x + wallW * 0.26f, top + u * 0.003f)]);

        // Windows: glow, pane, shutters, a white frame and the cross bar.
        RectangleF[] windows = FarmWindows(x, footY, u);
        using var glass = new SolidBrush(WindowWarm);
        using var frame = new Pen(Color.FromArgb(244, 238, 226), Math.Max(1f, u * 0.0015f));
        using var bar = new Pen(Color.FromArgb(120, 100, 80), Math.Max(1f, u * 0.0012f));
        using var shut = new SolidBrush(shutter);
        for (int i = 0; i < windows.Length; i++)
        {
            RectangleF win = windows[i];
            PaintWindowGlow(g, win, u * 0.020f);
            if (i != 2)                                                                   // shutters either side (not on the gable window)
            {
                g.FillRectangle(shut, win.X - win.Width * 0.42f, win.Y, win.Width * 0.36f, win.Height);
                g.FillRectangle(shut, win.Right + win.Width * 0.06f, win.Y, win.Width * 0.36f, win.Height);
            }
            g.FillRectangle(glass, win);
            g.DrawLine(bar, win.X + win.Width / 2, win.Y, win.X + win.Width / 2, win.Bottom);
            g.DrawLine(bar, win.X, win.Y + win.Height / 2, win.Right, win.Y + win.Height / 2);
            g.DrawRectangle(frame, win.X, win.Y, win.Width, win.Height);
        }

        // The door, in the middle under the gable, with a fanlight over it.
        RectangleF door = FarmDoorRect(x, footY, u);
        using (var d = new SolidBrush(Color.FromArgb(88, 58, 42))) g.FillRectangle(d, door);
        using (var fan = new SolidBrush(WindowWarm)) g.FillRectangle(fan, door.X, door.Y - u * 0.004f, door.Width, u * 0.003f);
        using (var knob = new SolidBrush(Color.FromArgb(220, 190, 110))) g.FillEllipse(knob, door.Right - u * 0.004f, door.Y + door.Height * 0.55f, u * 0.002f, u * 0.002f);

        // The porch: a shallow roof across the whole front between the storeys, on six turned posts, a railing with
        // balusters either side of the steps, and three steps up to the door.
        float porchTop = top + wallH * 0.47f;
        using (var post = new Pen(white, Math.Max(1f, u * 0.003f)))
            for (int i = 0; i < 6; i++)
            {
                float px = x - wallW * 0.47f + wallW * 0.94f * i / 5f;
                g.DrawLine(post, px, porchTop, px, footY);
            }
        using (var porchRoof = new SolidBrush(roof))
            g.FillRectangle(porchRoof, x - wallW * 0.54f, porchTop - u * 0.007f, wallW * 1.08f, u * 0.007f);
        using (var fascia = new SolidBrush(white))
            g.FillRectangle(fascia, x - wallW * 0.54f, porchTop, wallW * 1.08f, u * 0.002f);
        using (var floor = new SolidBrush(shade))
            g.FillRectangle(floor, x - wallW * 0.54f, footY - u * 0.004f, wallW * 1.08f, u * 0.004f);
        using (var rail = new Pen(white, Math.Max(1f, u * 0.0015f)))
        {
            float ry = footY - u * 0.018f;
            foreach (var (ra, rb) in new[] { (x - wallW * 0.54f, x - u * 0.016f), (x + u * 0.016f, x + wallW * 0.54f) })
            {
                g.DrawLine(rail, ra, ry, rb, ry);
                for (float bx = ra + u * 0.004f; bx < rb; bx += u * 0.005f)
                    g.DrawLine(rail, bx, ry, bx, footY - u * 0.004f);
            }
        }
        using (var step = new SolidBrush(shade))
            for (int i = 0; i < 3; i++)
                g.FillRectangle(step, x - u * 0.012f - u * 0.002f * i, footY - u * 0.004f + u * 0.003f * i, u * 0.024f + u * 0.004f * i, u * 0.003f);
        // The lamp by the door, lit.
        using (var lamp = new SolidBrush(Color.FromArgb(255, 230, 160)))
        {
            PointF at = FarmPorchLight(x, footY, u);
            g.FillEllipse(lamp, at.X - u * 0.0025f, at.Y - u * 0.0025f, u * 0.005f, u * 0.005f);
        }
    }

    internal static RectangleF FarmDoorRect(float x, float footY, float u) { var (_, wallH) = FarmWallSize(u); return new RectangleF(x - u * 0.008f, footY - wallH * 0.40f, u * 0.016f, wallH * 0.40f); }
    internal static PointF FarmPorchLight(float x, float footY, float u) { var (_, wallH) = FarmWallSize(u); return new PointF(x + u * 0.016f, footY - wallH * 0.33f); }

    /// <summary>
    /// The doghouse in the yard right of the farmhouse (his ask): a little red-walled house with a dark gable roof, a
    /// black arched doorway and a bowl by the step. Its foot is "at"; the dog lives here (FarmYard.cs).
    /// </summary>
    private static void PaintDoghouse(Graphics g, PointF at, float u)
    {
        float w = u * 0.024f, h = u * 0.014f;
        using (var wall = new SolidBrush(Color.FromArgb(150, 54, 40))) g.FillRectangle(wall, at.X - w / 2, at.Y - h, w, h);
        using (var board = new Pen(Color.FromArgb(70, 80, 20, 18), Math.Max(1f, u * 0.001f)))
            for (float bx = at.X - w / 2 + u * 0.004f; bx < at.X + w / 2; bx += u * 0.004f) g.DrawLine(board, bx, at.Y - h, bx, at.Y);
        using (var roof = new SolidBrush(Color.FromArgb(60, 44, 40)))
            g.FillPolygon(roof, [new PointF(at.X - w * 0.6f, at.Y - h), new PointF(at.X, at.Y - h - u * 0.011f), new PointF(at.X + w * 0.6f, at.Y - h)]);
        using (var trim = new Pen(Color.FromArgb(228, 214, 196), Math.Max(1f, u * 0.0012f)))
            g.DrawLines(trim, [new PointF(at.X - w * 0.6f, at.Y - h), new PointF(at.X, at.Y - h - u * 0.011f), new PointF(at.X + w * 0.6f, at.Y - h)]);
        using (var hole = new SolidBrush(Color.FromArgb(24, 14, 12)))
        {
            g.FillRectangle(hole, at.X - w * 0.18f, at.Y - h * 0.62f, w * 0.36f, h * 0.62f);
            g.FillEllipse(hole, at.X - w * 0.18f, at.Y - h * 0.80f, w * 0.36f, h * 0.36f);
        }
        using (var bowl = new SolidBrush(Color.FromArgb(200, 196, 190))) g.FillEllipse(bowl, at.X + w * 0.40f, at.Y - u * 0.003f, u * 0.007f, u * 0.003f);
    }

    /// <summary>
    /// A dirt road from the farmhouse's porch along the hill to the barn's big door (his ask: "a small road leading to
    /// the barn"): a pale track with two darker ruts, curving down from the porch, running along below the orchard
    /// and the pig pen, and up to the barn. Returns its middle line (the dog trots along it).
    /// </summary>
    private static PointF[] PaintRoad(Graphics g, PointF farmFoot, PointF barnFoot, PointF[] nearRidge, float u)
    {
        float RY(float x) => Brushwork.RidgeYAt(nearRidge, x);
        PointF[] line =
        [
            new PointF(farmFoot.X + u * 0.012f, farmFoot.Y + u * 0.003f),
            new PointF(farmFoot.X + u * 0.055f, RY(farmFoot.X + u * 0.055f) + u * 0.026f),
            new PointF(farmFoot.X + u * 0.16f, RY(farmFoot.X + u * 0.16f) + u * 0.031f),
            new PointF((farmFoot.X + barnFoot.X) / 2, RY((farmFoot.X + barnFoot.X) / 2) + u * 0.032f),
            new PointF(barnFoot.X - u * 0.12f, RY(barnFoot.X - u * 0.12f) + u * 0.030f),
            new PointF(barnFoot.X - u * 0.04f, RY(barnFoot.X - u * 0.04f) + u * 0.020f),
            new PointF(barnFoot.X, barnFoot.Y + u * 0.003f),
        ];
        using (var bed = new Pen(Color.FromArgb(158, 118, 72), Math.Max(2f, u * 0.0085f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawCurve(bed, line, 0.5f);
        using (var rut = new Pen(Color.FromArgb(118, 84, 50), Math.Max(1f, u * 0.0012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            foreach (float off in new[] { -u * 0.0025f, u * 0.0025f })
            {
                var pts = new PointF[line.Length];
                for (int i = 0; i < line.Length; i++) pts[i] = new PointF(line[i].X, line[i].Y + off);
                g.DrawCurve(rut, pts, 0.5f);
            }
        return line;
    }

    /// <summary>
    /// A pond (his ask: "a pond off in the distance"): the watering pond in the cows' pasture on the hill left of the
    /// house, seen from low and far so it is a flat sliver of water. (On the far hill's slope it read as "that disc
    /// thing above the horses", and a terrace round it only made a bigger disc: water reads as water where the ground
    /// is flat, and the pasture's band is the flattest ground the farm has.) The water is an uneven oval, not a true
    /// ellipse, holding the dusk sky, paler toward its far edge; a dark muddy bank along its near edge, thicker in the
    /// middle, with a few stones; three thin ripple lines; the sun's shimmer as a soft vertical smear; cattails in two
    /// clumps at the near bank and one lone at the far side.
    /// </summary>
    private static void PaintPond(Graphics g, PointF at, float rx, float ry, float u)
    {
        // The water: an uneven oval, a little wider at the left, with a shallow bay at the near right.
        using (var path = new GraphicsPath())
        {
            path.AddClosedCurve([
                new PointF(at.X - rx * 1.0f, at.Y + ry * 0.1f), new PointF(at.X - rx * 0.7f, at.Y - ry * 0.8f), new PointF(at.X - rx * 0.1f, at.Y - ry * 1.0f),
                new PointF(at.X + rx * 0.55f, at.Y - ry * 0.85f), new PointF(at.X + rx * 0.95f, at.Y - ry * 0.2f), new PointF(at.X + rx * 0.8f, at.Y + ry * 0.7f),
                new PointF(at.X + rx * 0.3f, at.Y + ry * 0.75f), new PointF(at.X + rx * 0.05f, at.Y + ry * 1.0f), new PointF(at.X - rx * 0.5f, at.Y + ry * 0.9f)], 0.6f);
            using (var water = new LinearGradientBrush(new PointF(0, at.Y - ry), new PointF(0, at.Y + ry), Color.FromArgb(222, 150, 108), Color.FromArgb(132, 74, 92)))
                g.FillPath(water, path);
            // The near bank: the water's own edge drawn in mud, thicker at the middle of the near side.
            using (var bank = new Pen(Color.FromArgb(70, 44, 32), Math.Max(1.5f, u * 0.0025f)))
                g.DrawPath(bank, path);
            using (var mud = new SolidBrush(Color.FromArgb(78, 50, 36)))
                g.FillEllipse(mud, at.X - rx * 0.55f, at.Y + ry * 0.55f, rx * 1.1f, ry * 0.55f);
        }
        // Ripples: thin pale lines across the water, longer in the middle.
        using (var ripple = new Pen(Color.FromArgb(120, 250, 210, 170), Math.Max(1f, u * 0.0012f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            foreach (var (f, half) in new[] { (-0.45f, 0.35f), (-0.05f, 0.55f), (0.35f, 0.4f) })
                g.DrawLine(ripple, at.X - rx * half + rx * 0.1f, at.Y + ry * f, at.X + rx * half, at.Y + ry * f);
        // The sun's shimmer: a soft vertical smear of its light on the water.
        using (var shimmer = new LinearGradientBrush(new PointF(at.X - rx * 0.12f, 0), new PointF(at.X + rx * 0.12f, 0), Color.FromArgb(0, 255, 230, 180), Color.FromArgb(0, 255, 230, 180)))
        {
            shimmer.InterpolationColors = new ColorBlend { Colors = [Color.FromArgb(0, 255, 230, 180), Color.FromArgb(110, 255, 230, 180), Color.FromArgb(0, 255, 230, 180)], Positions = [0f, 0.5f, 1f] };
            g.FillRectangle(shimmer, at.X - rx * 0.12f, at.Y - ry * 0.7f, rx * 0.24f, ry * 1.4f);
        }
        // Stones at the near bank, and the cattails: two clumps near, one lone at the far left.
        using (var stone = new SolidBrush(Color.FromArgb(104, 90, 80)))
            foreach (var (f, sz) in new[] { (-0.3f, 1.0f), (0.45f, 0.7f), (0.62f, 0.5f) })
                g.FillEllipse(stone, at.X + rx * f, at.Y + ry * 0.95f, u * 0.004f * sz, u * 0.0025f * sz);
        using (var stalk = new Pen(Color.FromArgb(78, 60, 36), Math.Max(1f, u * 0.0012f)))
        using (var head = new SolidBrush(Color.FromArgb(96, 60, 34)))
            foreach (var (f, dy, lean) in new[] { (-0.78f, 0.75f, -0.4f), (-0.70f, 0.95f, 0.2f), (-0.62f, 0.85f, -0.1f), (0.70f, 0.85f, 0.3f), (0.78f, 1.0f, -0.2f), (0.86f, 0.9f, 0.4f), (-0.55f, -0.95f, 0.1f) })
            {
                float sx = at.X + rx * f, sy = at.Y + ry * dy, tall = u * (0.012f + 0.003f * MathF.Abs(lean));
                g.DrawLine(stalk, sx, sy, sx + u * 0.002f * lean, sy - tall);
                g.FillRectangle(head, sx + u * 0.002f * lean - u * 0.0009f, sy - tall, u * 0.0022f, u * 0.0045f);
            }
    }

    /// <summary>
    /// A post-and-rail fence along the hill (a horse paddock's rails, the pasture's edge): posts every so often and two
    /// rails, in weathered grey wood. Painted in the backdrop as the BACK side of a pen; the yard stamps the front rail
    /// over its animals each frame (FarmYard.cs), so a horse stands between the two.
    /// </summary>
    private static void PaintRails(Graphics g, float x0, float x1, float y, float u, Color wood)
    {
        float tall = u * 0.016f;
        using var post = new Pen(wood, Math.Max(1f, u * 0.0025f));
        using var rail = new Pen(wood, Math.Max(1f, u * 0.0016f));
        for (float px = x0; px <= x1 + 0.5f; px += u * 0.016f) g.DrawLine(post, px, y - tall, px, y);
        g.DrawLine(rail, x0, y - tall * 0.78f, x1, y - tall * 0.78f);
        g.DrawLine(rail, x0, y - tall * 0.42f, x1, y - tall * 0.42f);
    }

    /// <summary>The pig pen: a low board fence round a patch of churned earth with a wallow and a trough. The back boards are here; the front ones are the yard's.</summary>
    private static void PaintPigPen(Graphics g, float x0, float x1, float yBack, float yFront, float u)
    {
        using (var mud = new SolidBrush(Color.FromArgb(104, 74, 46)))
            g.FillPolygon(mud, [new PointF(x0, yBack), new PointF(x1, yBack), new PointF(x1 + u * 0.004f, yFront), new PointF(x0 - u * 0.004f, yFront)]);
        using (var wallow = new SolidBrush(Color.FromArgb(70, 48, 32)))
            g.FillEllipse(wallow, x0 + (x1 - x0) * 0.55f, yBack + (yFront - yBack) * 0.35f, (x1 - x0) * 0.32f, (yFront - yBack) * 0.4f);
        using (var trough = new SolidBrush(Color.FromArgb(90, 66, 44)))
            g.FillRectangle(trough, x0 + (x1 - x0) * 0.08f, yBack + (yFront - yBack) * 0.45f, (x1 - x0) * 0.22f, u * 0.004f);
        using var board = new Pen(Color.FromArgb(96, 70, 44), Math.Max(1f, u * 0.0018f));
        g.DrawLine(board, x0, yBack - u * 0.006f, x1, yBack - u * 0.006f);
        g.DrawLine(board, x0, yBack - u * 0.0025f, x1, yBack - u * 0.0025f);
        for (float px = x0; px <= x1 + 0.5f; px += u * 0.014f) g.DrawLine(board, px, yBack - u * 0.009f, px, yBack);
    }

    /// <summary>
    /// A clothesline between two T-posts in the yard, the line sagging a little between them. Returns the line's ends,
    /// where the scene hangs the washing (the cloth moves, so it is the scene's to stamp, not the backdrop's).
    /// </summary>
    private static (PointF A, PointF B) PaintClothesline(Graphics g, float x0, float x1, float footY, float u)
    {
        float tall = u * 0.026f, arm = u * 0.006f;
        Color wood = Color.FromArgb(96, 74, 56);
        using (var post = new Pen(wood, Math.Max(1f, u * 0.0025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            foreach (float px in new[] { x0, x1 })
            {
                g.DrawLine(post, px, footY, px, footY - tall);
                g.DrawLine(post, px - arm, footY - tall, px + arm, footY - tall);         // the crossbar of the T
            }
        var a = new PointF(x0, footY - tall + u * 0.001f);
        var b = new PointF(x1, footY - tall + u * 0.001f);
        using (var line = new Pen(Color.FromArgb(200, 190, 176), Math.Max(1f, u * 0.0010f)))
            g.DrawBezier(line, a.X, a.Y, (a.X + b.X) / 2, a.Y + u * 0.003f, (a.X + b.X) / 2, a.Y + u * 0.003f, b.X, b.Y);
        return (a, b);
    }

    /// <summary>
    /// A steel water-pump windmill: a lattice tower tapering to a platform, a tail vane turned downwind, and the hub of
    /// the wheel. The wheel itself is not here: it turns, so the scene paints its pages and stamps one each frame at the
    /// hub this returns. About 8 m tall at the barn's scale (0.0108 U a metre), the wheel 1.5 m across.
    /// </summary>
    private static (PointF Hub, float R) PaintWindmill(Graphics g, float x, float footY, float u)
    {
        float tall = u * 0.086f, baseHalf = u * 0.012f, topHalf = u * 0.0035f, top = footY - tall;
        Color steel = Color.FromArgb(118, 112, 104), dark = Color.FromArgb(78, 72, 66);
        using (var shadow = new SolidBrush(Color.FromArgb(60, 50, 24, 16)))
            g.FillEllipse(shadow, x - baseHalf * 1.3f, footY - u * 0.003f, baseHalf * 2.6f, u * 0.007f);
        using (var leg = new Pen(steel, Math.Max(1f, u * 0.0016f)))
        using (var brace = new Pen(Color.FromArgb(150, 118, 112, 104), Math.Max(1f, u * 0.0009f)))
        {
            g.DrawLine(leg, x - baseHalf, footY, x - topHalf, top);
            g.DrawLine(leg, x + baseHalf, footY, x + topHalf, top);
            g.DrawLine(leg, x - baseHalf * 0.35f, footY, x - topHalf * 0.3f, top);        // the back legs, seen between the front pair
            g.DrawLine(leg, x + baseHalf * 0.35f, footY, x + topHalf * 0.3f, top);
            // Cross braces up the tower, each X between the legs' width at that height.
            int rungs = 6;
            for (int i = 0; i < rungs; i++)
            {
                float t0 = i / (float)rungs, t1 = (i + 1) / (float)rungs;
                float y0 = footY - tall * t0, y1 = footY - tall * t1;
                float h0 = baseHalf + (topHalf - baseHalf) * t0, h1 = baseHalf + (topHalf - baseHalf) * t1;
                g.DrawLine(brace, x - h0, y0, x + h1, y1);
                g.DrawLine(brace, x + h0, y0, x - h1, y1);
                g.DrawLine(brace, x - h1, y1, x + h1, y1);
            }
        }
        // The platform, the tail vane (a plate on a boom, swung downwind, to the right), and the hub.
        using (var plate = new SolidBrush(dark))
        {
            g.FillRectangle(plate, x - topHalf * 2.2f, top - u * 0.002f, topHalf * 4.4f, u * 0.002f);
            float boom = u * 0.012f, vaneY = top - u * 0.006f;
            using (var boomPen = new Pen(dark, Math.Max(1f, u * 0.0012f))) g.DrawLine(boomPen, x, vaneY, x + boom, vaneY);
            g.FillPolygon(plate, [new PointF(x + boom * 0.7f, vaneY - u * 0.005f), new PointF(x + boom * 1.35f, vaneY - u * 0.004f), new PointF(x + boom * 1.35f, vaneY + u * 0.004f), new PointF(x + boom * 0.7f, vaneY + u * 0.005f)]);
        }
        var hub = new PointF(x - u * 0.0015f, top - u * 0.006f);
        return (hub, u * 0.016f);
    }

    /// <summary>
    /// Where the farmhouse's three windows are, for a house whose base is
    /// centered at (x, footY): [0] and [1] downstairs either side of the
    /// door, [2] up in the gable. The sizes must match PaintFarmhouse.
    /// </summary>
    /// <summary>The farmhouse's lit windows: [0] and [1] downstairs either side of the door, [2] in the cross gable, [3] and [4] upstairs.</summary>
    internal static RectangleF[] FarmWindows(float x, float footY, float u)
    {
        var (wallW, wallH) = FarmWallSize(u);
        float top = footY - wallH;
        RectangleF Window(float cx, float cy, float ww, float wh) => new(cx - ww / 2, cy - wh / 2, ww, wh);
        return
        [
            Window(x - wallW * 0.30f, top + wallH * 0.74f, u * 0.017f, u * 0.020f),
            Window(x + wallW * 0.30f, top + wallH * 0.74f, u * 0.017f, u * 0.020f),
            Window(x, top - u * 0.026f, u * 0.011f, u * 0.015f),
            Window(x - wallW * 0.30f, top + wallH * 0.24f, u * 0.015f, u * 0.018f),
            Window(x + wallW * 0.30f, top + wallH * 0.24f, u * 0.015f, u * 0.018f),
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
    private static List<(PointF At, float R)> PaintBarn(Graphics g, float x, float footY, float u)
    {
        float barnW = u * 0.13f, barnH = u * 0.06f, top = footY - barnH;
        Color red = Color.FromArgb(150, 44, 36), redDark = Color.FromArgb(118, 34, 30), roof = Color.FromArgb(74, 40, 36), roofLit = Color.FromArgb(96, 54, 48);
        Color trim = Color.FromArgb(228, 214, 196);

        // ---- The silos (his line: "some silos"): a tall one standing behind the barn's right corner, the old one against
        //      the wall in front of it, and a squat grain bin with a cone roof past them. Each is a tube shaded round, hoops
        //      down its side, a cap, a hatch and a shadow at its foot. Painted back to front, so each nearer one covers the
        //      last. The tops are handed to the scene: a turkey roosts on one now and then (Happenings/SiloRoost.cs). ----
        var tops = new List<(PointF At, float R)>();
        float sw0 = u * 0.034f, sx0 = x + barnW * 0.5f + sw0 * 0.52f;
        void Silo(float sx, float sw, float sh, bool cone)
        {
            using (var shadow = new SolidBrush(Color.FromArgb(70, 50, 24, 16)))
                g.FillEllipse(shadow, sx - sw * 0.8f, footY - sw * 0.16f, sw * 1.8f, sw * 0.4f);
            using (var silo = new LinearGradientBrush(new PointF(sx - sw / 2, 0), new PointF(sx + sw / 2, 0), Color.FromArgb(214, 204, 188), Color.FromArgb(132, 120, 108)))
                g.FillRectangle(silo, sx - sw / 2, footY - sh, sw, sh + u * 0.004f);
            using (var hoop = new Pen(Color.FromArgb(90, 70, 58, 50), Math.Max(1f, u * 0.0015f)))
                for (int i = 1; i <= 5; i++)
                    g.DrawLine(hoop, sx - sw / 2, footY - sh * i / 6f, sx + sw / 2, footY - sh * i / 6f);
            float capTall = sw * 0.55f;
            using (var cap = new LinearGradientBrush(new PointF(sx - sw / 2, 0), new PointF(sx + sw / 2, 0), Color.FromArgb(150, 136, 124), Color.FromArgb(96, 86, 80)))
            {
                if (cone) g.FillPolygon(cap, [new PointF(sx - sw * 0.56f, footY - sh), new PointF(sx, footY - sh - capTall), new PointF(sx + sw * 0.56f, footY - sh)]);   // the bin's cone, a hair wider than the tube
                else g.FillPie(cap, sx - sw / 2, footY - sh - capTall, sw, capTall * 2, 180, 180);                                                                    // the domed cap: the top half of an ellipse
            }
            using (var hatch = new SolidBrush(Color.FromArgb(70, 58, 52)))
                g.FillRectangle(hatch, sx - sw * 0.14f, footY - sh + sw * 0.25f, sw * 0.28f, sw * 0.32f);
            tops.Add((new PointF(sx, footY - sh - capTall), sw * 0.5f));
        }
        Silo(sx0 + u * 0.030f, u * 0.028f, u * 0.152f, false);     // the tall one, behind
        Silo(sx0, sw0, u * 0.118f, false);                          // the old one, against the wall
        Silo(sx0 + u * 0.064f, u * 0.042f, u * 0.068f, true);      // the grain bin

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
        return tops;
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
    private static List<(PointF At, float R)> PaintFarTrees(Graphics g, int w, float u, PointF[] nearRidge, float farmX, float barnX, float scarecrowX, float orchardX, float orchardHalf, float pondX, float pondRx, float paddockX0, float paddockX1, Random rng)
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
            if (MathF.Abs(x - farmX) < u * 0.16f || MathF.Abs(x - barnX) < u * 0.21f || MathF.Abs(x - scarecrowX) < u * 0.08f || MathF.Abs(x - orchardX) < orchardHalf + u * 0.06f) continue;
            if (MathF.Abs(x - pondX) < pondRx + u * 0.05f || (x > paddockX0 - u * 0.06f && x < paddockX1 + u * 0.06f)) continue;   // clear of the pond and the paddock
            float foot = Brushwork.RidgeYAt(nearRidge, x) + u * 0.014f;
            float cy = foot - u * s.Lift, r = u * s.R * (0.9f + 0.2f * (float)rng.NextDouble());
            Brushwork.Trunk(g, x, foot, cy, r, rng, bark);
            Brushwork.Canopy(g, x, cy, r, u, rng, s.Kind == 0 ? oak : s.Kind == 1 ? maple : gold);
            trees.Add((new PointF(x, cy), r));
        }
        return trees;
    }

    /// <summary>
    /// The orchard beside the farmhouse: two rows of small trees in rows on the hill past the scarecrow, the back row a
    /// little higher up and smaller, each a round canopy (the maples' own brush) in its own autumn orange: the fruit is
    /// long picked and the leaves have turned (his line: "orange fall colored leaves too because it's fall. no fruit").
    /// At the house's scale (0.0154 U a metre) a 3.5 m tree is 0.054 U tall. The trees' middles are "cx" across "half"
    /// either side; the far trees keep clear of that stretch.
    /// </summary>
    private static void PaintOrchard(Graphics g, float u, PointF[] nearRidge, float cx, float half, Random rng)
    {
        var leaf = new BlossomPalette(Color.FromArgb(240, 222, 128, 44), Color.FromArgb(162, 70, 30), Color.FromArgb(248, 184, 84));
        Color bark = Color.FromArgb(70, 50, 38);
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
        // The jack-o'-lantern: a BACK-band pumpkin (behind the walk line, among the small ones: "its supposed to be subtle")
        // nearest x = 0.66 w; made if there is none.
        PatchItem? best = null;
        foreach (PatchItem o in items)
            if (o.Kind == 0 && !o.Front && o.Foot.X > 0.50f * w && o.Foot.X < 0.85f * w && o.R > 0.026f * u
                && (best == null || MathF.Abs(o.Foot.X - 0.66f * w) < MathF.Abs(best.Foot.X - 0.66f * w))) best = o;
        if (best == null)
        {
            best = new PatchItem { Foot = new PointF(0.66f * w, Walk(0.66f * w) - 0.03f * u), R = 0.034f * u, Kind = 0, Front = false, Tint = 0 };
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
            using (var green = new SolidBrush(flat ?? Color.FromArgb(116, 136, 70)))   // softened: the patch's hard greens read too bright against the autumn ("the primaries are a bit too bright")
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
        using (var leaf = new SolidBrush(flat ?? Color.FromArgb(90, 112, 58)))        // softened with the gourd's green
        using (var path = new GraphicsPath())
        {
            path.AddClosedCurve([new PointF(0, -r * 0.1f), new PointF(r * 0.55f, -r * 0.7f), new PointF(r, -r * 0.2f), new PointF(r * 0.8f, r * 0.5f), new PointF(0, r),
                                 new PointF(-r * 0.8f, r * 0.5f), new PointF(-r, -r * 0.2f), new PointF(-r * 0.55f, -r * 0.7f)], 0.5f);
            g.FillPath(leaf, path);
        }
        if (flat == null)
        {
            using var vein = new Pen(Color.FromArgb(114, 140, 80), Math.Max(1f, r * 0.08f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(vein, 0, -r * 0.1f, 0, r * 0.85f);
            g.DrawLine(vein, 0, r * 0.3f, r * 0.5f, -r * 0.1f);
            g.DrawLine(vein, 0, r * 0.3f, -r * 0.5f, -r * 0.1f);
            using var stalk = new Pen(Color.FromArgb(70, 96, 40), Math.Max(1f, r * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(stalk, 0, -r * 0.1f, 0, -r * 0.6f);
        }
        g.Restore(st);
    }

    /// <summary>
    /// Maples at both sides in full autumn colour, each standing on the ground under it. The inner pair are the
    /// squirrels' trees (taller, thicker, a hole up each trunk, the left one with a hollow in its roots).
    ///
    /// How a tree is built, back to front, the way a painter would lay it down:
    ///   1. Its SHADOW on the turf: a long soft smear on the side away from the sun (the sun is low, so shadows are long).
    ///   2. The engine's forked stem (Brushwork.Trunk). Kept because it rolls the trunk's lean off the scene's dice and
    ///      returns the line the squirrels climb; the bole painted next covers its thin stroke.
    ///   3. The BOLE: a tapered column of bark with a lit flank on the sun's side and a dark flank away from it, streaks
    ///      of grain, and a buttress at the foot that runs out into tapered roots (filled shapes, not pen lines: a pen
    ///      line on the ground read as a spider's leg).
    ///   4. The LIMBS: from the fork, four tapering strokes bending up and out, each splitting into twigs that end inside
    ///      the crown.
    ///   5. The CROWN (PaintCrown): not one round cloud but a ring of LOBES, each shaded on its own, the way a real crown
    ///      is a set of leafy masses. Every lobe gets a solid deep body, then three coats of leaves: crimson shade over
    ///      the whole lobe, the body orange pulled toward the sun, and gold where the low sun catches its sunward top.
    ///      Some of the dots are pointed leaves turned their own way, so the edge reads as leaves and not confetti.
    /// (The first version was the sakura brush recoloured: one round blob of same-sized dots, lit from the sakura's
    /// left-hand sun whichever side the real sun was on, on a stick with four pen-line roots. "They need to look better.")
    ///
    /// Returns random spots inside the crowns for the falling leaves, each crown's centre and size, the hollow, the two
    /// inner trunks as climbing lines and the tree holes. The trunk's width rule (HalfAt) is shared with
    /// PatchLife.TrunkHalf: change both or the squirrels' den stencil cuts the wrong trunk.
    /// </summary>
    private static (List<PointF> LeafSpots, List<(PointF At, float R)> Canopies, (PointF At, float R) Hollow, PointF[] LeftTrunk, PointF[] RightTrunk, List<(PointF At, float R)> TreeHoles) PaintMaples(Graphics g, int w, float u, Bank ground, Random rng, float sunX)
    {
        Color bark = Color.FromArgb(62, 44, 36);
        Color barkLit = Color.FromArgb(98, 72, 56), barkDark = Color.FromArgb(40, 27, 22), barkStreak = Color.FromArgb(50, 34, 27);

        // (X as a fraction of the width, crown centre height above the ground in u, crown size in u). The inner pair
        // are the squirrels' trees (his ask: "slightly bigger trees with holes they can hide in"): taller and fuller
        // than the outer pair, on a thick trunk with a hole up it.
        (float X, float Lift, float R)[] trees =
        [
            (0.03f, 0.17f, 0.15f), (0.12f, 0.20f, 0.135f), (0.90f, 0.21f, 0.145f), (0.98f, 0.17f, 0.14f),
        ];

        // 1. The shadows, under everything else: a flat ellipse on the turf, on the side away from the sun.
        using (var shade = new SolidBrush(Color.FromArgb(58, 40, 22, 12)))
            foreach (var t in trees)
            {
                float hx = w * t.X, foot = ground.YAt(hx) + u * 0.03f, r = u * t.R, away = -MathF.Sign(sunX - hx);
                g.FillEllipse(shade, hx + away * r * 0.70f - r * 0.80f, foot - u * 0.008f, r * 1.6f, u * 0.019f);
            }

        // 2. The engine's stems: the lean roll and the climbing line for each tree.
        var trunks = new PointF[trees.Length][];
        for (int i = 0; i < trees.Length; i++)
        {
            var t = trees[i];
            trunks[i] = Brushwork.Trunk(g, w * t.X, ground.YAt(w * t.X) + u * 0.03f, ground.YAt(w * t.X) - u * t.Lift, u * t.R, rng, bark);
        }

        // 3 and 4. The boles, their roots and their limbs. The crown's own dice (one Random per tree, seeded from its
        // place) paint the grain and the leaves, so the scene's dice are rolled exactly as before and every pumpkin
        // and branch painted after the maples lands where it did.
        float HalfAt(float f) => u * (0.026f - 0.010f * f);                     // half the inner trunk's width, f 0 at the foot to 1 at the fork (PatchLife.TrunkHalf is the same rule)
        (PointF At, float R) hollow = default;
        var own = new Random[trees.Length];
        for (int i = 0; i < trees.Length; i++)
        {
            own[i] = new Random(unchecked(7919 * (i + 1) + w));
            bool inner = i is 1 or 2;
            float girth = inner ? 1f : 0.72f;                                   // the outer pair are smaller trees
            float lit = MathF.Sign(sunX - w * trees[i].X);                     // +1: the sun is to this tree's right, so its right flank is lit; -1 the other way
            PointF foot = trunks[i][0], fork = trunks[i][1];
            float cx = w * trees[i].X, cy = ground.YAt(cx) - u * trees[i].Lift, r = u * trees[i].R;
            float H(float f) => HalfAt(f) * girth;
            PointF Along(float f, float across) => new(foot.X + (fork.X - foot.X) * f + across, foot.Y + (fork.Y - foot.Y) * f);

            // The bole: a tapered column, a touch wider at the fork where the limbs leave it.
            using (var bole = new SolidBrush(bark))
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([
                    Along(0f, -H(0f)), Along(0.5f, -H(0.5f)), Along(1f, -H(1f) * 1.15f), Along(1.08f, 0f), Along(1f, H(1f) * 1.15f),
                    Along(0.5f, H(0.5f)), Along(0f, H(0f))], 0.25f);
                g.FillPath(bole, path);
            }
            // The limbs: from the fork, each a tapering stroke bending up and out, splitting once into two twigs that
            // end inside the crown (the leaves reach about 0.75 r up and 1.1 r out from its middle: limbs poking out
            // above it read as antlers).
            var limbs = new (float Dx, float Dy, float Bend)[] { (-0.50f, -0.08f, -0.18f), (-0.18f, -0.42f, 0.08f), (0.34f, -0.36f, 0.20f), (0.52f, 0.06f, 0.10f) };
            foreach (var (dx, dy, bend) in limbs)
            {
                PointF a = Along(0.98f, 0f), b = new PointF(cx + r * dx, cy + r * dy);
                PointF m = new(a.X + (b.X - a.X) * 0.5f + r * bend, a.Y + (b.Y - a.Y) * 0.5f);
                float w0 = H(1f) * 0.9f, w1 = H(1f) * 0.22f;
                for (int seg = 0; seg < 6; seg++)
                {
                    float t0 = seg / 6f, t1 = (seg + 1) / 6f;
                    PointF P(float t) => new((1 - t) * (1 - t) * a.X + 2 * (1 - t) * t * m.X + t * t * b.X, (1 - t) * (1 - t) * a.Y + 2 * (1 - t) * t * m.Y + t * t * b.Y);
                    using var pen = new Pen(bark, Math.Max(1.5f, (w0 + (w1 - w0) * t0) * 2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(pen, P(t0), P(t1));
                }
                using var twig = new Pen(bark, Math.Max(1.2f, w1 * 1.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(twig, b, new PointF(b.X + r * dx * 0.25f, b.Y + r * dy * 0.2f - r * 0.10f));
                g.DrawLine(twig, b, new PointF(b.X + r * dx * 0.15f + r * 0.10f * MathF.Sign(dx + 0.01f), b.Y + r * dy * 0.25f + r * 0.06f));
            }
            // The flanks: a lit strip up the sun's side, a dark strip up the other, each hugging its edge.
            using (var brush = new SolidBrush(barkLit))
            using (var strip = new GraphicsPath())
            {
                strip.AddClosedCurve([Along(0.02f, lit * H(0f) * 0.88f), Along(0.5f, lit * H(0.5f) * 0.88f), Along(0.98f, lit * H(1f) * 0.95f), Along(0.98f, lit * H(1f) * 0.55f), Along(0.5f, lit * H(0.5f) * 0.55f), Along(0.02f, lit * H(0f) * 0.5f)], 0.2f);
                g.FillPath(brush, strip);
            }
            using (var brush = new SolidBrush(barkDark))
            using (var strip = new GraphicsPath())
            {
                strip.AddClosedCurve([Along(0.02f, -lit * H(0f) * 0.92f), Along(0.5f, -lit * H(0.5f) * 0.92f), Along(0.98f, -lit * H(1f) * 0.98f), Along(0.98f, -lit * H(1f) * 0.62f), Along(0.5f, -lit * H(0.5f) * 0.62f), Along(0.02f, -lit * H(0f) * 0.66f)], 0.2f);
                g.FillPath(brush, strip);
            }
            // The bark's grain: streaks up the bole, each wavering a little, in the deeper bark colour.
            using (var streak = new Pen(barkStreak, Math.Max(1f, u * 0.0022f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int s = 0; s < 8; s++)
                {
                    float across = -0.8f + 1.6f * (s + 0.5f) / 8f;
                    float f0 = 0.05f + 0.25f * (float)own[i].NextDouble(), f1 = f0 + 0.25f + 0.4f * (float)own[i].NextDouble();
                    var pts = new PointF[5];
                    for (int q = 0; q <= 4; q++)
                    {
                        float f = f0 + (f1 - f0) * q / 4f;
                        pts[q] = Along(f, across * H(f) + u * 0.003f * ((float)own[i].NextDouble() - 0.5f));
                    }
                    g.DrawCurve(streak, pts, 0.5f);
                }

            // The foot flares into a buttress: the bark widens on both sides in one smooth concave sweep down to the
            // ground and runs on out along it as tapered roots, the way a maple's roots show above the turf. Each root
            // is a filled sliver, thick where it leaves the buttress and vanishing into the grass. (An earlier version
            // was two round humps side by side, which read as something else entirely: "big testicles sitting on
            // the ground"; the version after that drew the roots as pen lines, which read as spider's legs.)
            float hx = cx, footY = ground.YAt(hx) + u * 0.03f, halfTrunk = H(0f) * 0.9f;
            using (var root = new SolidBrush(bark))
            using (var paler = new SolidBrush(barkLit))
            using (var flare = new GraphicsPath())
            {
                flare.AddClosedCurve([
                    new PointF(hx - halfTrunk, footY - u * 0.060f * girth), new PointF(hx - halfTrunk * 1.2f, footY - u * 0.036f * girth),
                    new PointF(hx - u * 0.028f * girth, footY - u * 0.013f * girth), new PointF(hx - u * 0.044f * girth, footY + u * 0.002f),
                    new PointF(hx + u * 0.044f * girth, footY + u * 0.002f), new PointF(hx + u * 0.028f * girth, footY - u * 0.013f * girth),
                    new PointF(hx + halfTrunk * 1.2f, footY - u * 0.036f * girth), new PointF(hx + halfTrunk, footY - u * 0.060f * girth)], 0.35f);
                g.FillPath(root, flare);
                foreach (var (side, len, thick) in new[] { (-1f, 0.080f, 0.0055f), (-1f, 0.050f, 0.0035f), (1f, 0.072f, 0.0055f), (1f, 0.044f, 0.0035f) })
                {
                    float x0 = hx + side * u * 0.034f * girth, x1 = hx + side * u * (0.034f + len) * girth, th = u * thick * girth;
                    using var sliver = new GraphicsPath();
                    sliver.AddClosedCurve([
                        new PointF(x0, footY - th), new PointF(x0 + (x1 - x0) * 0.4f, footY - th * 0.35f), new PointF(x1, footY + u * 0.003f),
                        new PointF(x0 + (x1 - x0) * 0.5f, footY + u * 0.003f), new PointF(x0, footY + u * 0.002f)], 0.3f);
                    g.FillPath(root, sliver);
                }
                // The buttress is rounded like the bole: a paler flank up its sun side, a darker one up the other, so it
                // reads as a swelling of the trunk and not a flat slab it stands on.
                g.FillEllipse(paler, hx + lit * u * 0.014f * girth - u * 0.005f * girth, footY - u * 0.044f * girth, u * 0.010f * girth, u * 0.040f * girth);
                using (var darker = new SolidBrush(barkDark))
                    g.FillEllipse(darker, hx - lit * u * 0.016f * girth - u * 0.004f * girth, footY - u * 0.040f * girth, u * 0.008f * girth, u * 0.036f * girth);
            }
            if (i == 1)
            {
                // Between the left maple's roots the squirrel's hollow: an arch of warm dark, blacker at the bottom
                // where no light reaches. The caper's squirrel comes out of it and dives back in.
                float hw = u * 0.013f, hh = u * 0.026f;
                using (var dark = new SolidBrush(Color.FromArgb(44, 26, 16)))
                using (var path = new GraphicsPath())
                {
                    path.AddClosedCurve([new PointF(hx - hw, footY + u * 0.003f), new PointF(hx - hw * 0.92f, footY - hh * 0.6f), new PointF(hx, footY - hh), new PointF(hx + hw * 0.92f, footY - hh * 0.6f), new PointF(hx + hw, footY + u * 0.003f)], 0.35f);
                    g.FillPath(dark, path);
                }
                using (var deep = new SolidBrush(Color.FromArgb(16, 10, 8)))
                    g.FillEllipse(deep, hx - hw * 0.8f, footY - hh * 0.55f, hw * 1.6f, hh * 0.6f);
                hollow = (new PointF(hx, footY - hh * 0.45f), hw);
            }
        }

        // The holes: one up each inner trunk, a little below the fork, where the squirrels den. A tall dark oval with a
        // raised lip of bark round it (paler on top where the light catches it, darker below) and a black heart.
        var treeHoles = new List<(PointF At, float R)>();
        foreach (int i in new[] { 1, 2 })
        {
            PointF foot = trunks[i][0], fork = trunks[i][1];
            float f = 0.72f;                                                     // seven tenths of the way up to the fork: a climb worth watching before it goes in
            var at = new PointF(foot.X + (fork.X - foot.X) * f, foot.Y + (fork.Y - foot.Y) * f);
            float hw = u * 0.011f, hh = u * 0.016f;
            using (var lipTop = new SolidBrush(Color.FromArgb(96, 72, 56)))
                g.FillEllipse(lipTop, at.X - hw * 1.35f, at.Y - hh * 1.3f, hw * 2.7f, hh * 2.5f);
            using (var lipLow = new SolidBrush(Color.FromArgb(48, 32, 26)))
                g.FillEllipse(lipLow, at.X - hw * 1.3f, at.Y - hh * 1.05f, hw * 2.6f, hh * 2.35f);
            using (var dark = new SolidBrush(Color.FromArgb(34, 20, 14)))
                g.FillEllipse(dark, at.X - hw, at.Y - hh, hw * 2, hh * 2);
            using (var deep = new SolidBrush(Color.FromArgb(12, 8, 6)))
                g.FillEllipse(deep, at.X - hw * 0.7f, at.Y - hh * 0.55f, hw * 1.4f, hh * 1.3f);
            treeHoles.Add((at, hw));
        }

        // 5. The crowns, and the spots the falling leaves let go from (eight per tree, off the scene's dice as before).
        var spots = new List<PointF>();
        var canopies = new List<(PointF At, float R)>();
        for (int i = 0; i < trees.Length; i++)
        {
            var t = trees[i];
            float cx = w * t.X, cy = ground.YAt(cx) - u * t.Lift, r = u * t.R;
            canopies.Add((new PointF(cx, cy), r));
            PaintCrown(g, cx, cy, r, u, MathF.Sign(sunX - cx), own[i]);
            for (int k = 0; k < 8; k++)
            {
                var spot = new PointF(cx + r * ((float)rng.NextDouble() * 2 - 1), cy + r * 0.5f * ((float)rng.NextDouble() * 2 - 1));
                if (spot.X >= 0 && spot.X < w) spots.Add(spot);
            }
        }
        return (spots, canopies, hollow, trunks[1], trunks[2], treeHoles);
    }

    /// <summary>
    /// One maple's crown in full autumn colour, centred on (cx, cy), about 1.1 r wide each way and 0.75 r tall each way.
    ///
    /// The crown is a set of LOBES: eight round the middle in a ring (wider than tall, a maple's shape), two on top and
    /// one at the heart. Think of each lobe as a leafy ball lit by a low sun off to one side: its sunward top is gold,
    /// its body orange, its far side and underside deep crimson. Each lobe gets, in order:
    ///   a solid body in the shade colour (so the crown is never see-through confetti),
    ///   a coat of crimson-to-orange leaves over all of it (the outline comes out bumpy and leafy),
    ///   a coat of orange leaves pulled toward the sun and up, leaving the far rim in shade,
    ///   a small coat of gold leaves on the sunward top, where the low sun catches.
    /// Lobes on the crown's sunward side take more of the light coats, lobes on its far side less, so the whole crown
    /// reads as lit from the sun too, not only each ball. About a third of the leaves in the two outer coats are drawn
    /// as pointed ovals turned their own way instead of round dots.
    /// </summary>
    /// <param name="lit">+1 when the sun is to the crown's right, -1 when it is to its left.</param>
    private static void PaintCrown(Graphics g, float cx, float cy, float r, float u, float lit, Random rng)
    {
        Color shade = Color.FromArgb(128, 28, 20), body = Color.FromArgb(206, 86, 28), warm = Color.FromArgb(236, 138, 40), gold = Color.FromArgb(246, 184, 54), pale = Color.FromArgb(252, 214, 96);

        var lobes = new List<(float X, float Y, float R)>();
        const int ring = 8;
        for (int k = 0; k < ring; k++)
        {
            double a = Math.PI * 2 * (k + 0.5 * rng.NextDouble()) / ring;
            lobes.Add((cx + (float)Math.Cos(a) * r * 0.70f, cy + (float)Math.Sin(a) * r * 0.36f, r * (0.30f + 0.12f * (float)rng.NextDouble())));
        }
        lobes.Add((cx - r * 0.22f, cy - r * 0.46f, r * 0.34f));
        lobes.Add((cx + r * 0.24f, cy - r * 0.42f, r * 0.36f));
        lobes.Add((cx, cy + r * 0.04f, r * 0.42f));

        float dotR = u * 0.0045f;

        // One coat over every lobe: dots (or leaves) of a colour between a and b, over a disc of the lobe shrunk to
        // "scale" and slid "shiftX" lobe-radii toward the sun and "shiftY" up. "density" is how many dots per dot-area.
        void Coat(Color a, Color b, float shiftX, float shiftY, float scale, float density, bool leaves)
        {
            foreach (var l in lobes)
            {
                float lx = l.X + shiftX * l.R * lit, ly = l.Y + shiftY * l.R, lr = l.R * scale;
                float side = Math.Clamp(0.5f + 0.5f * lit * (l.X - cx) / r - 0.35f * (l.Y - cy) / r, 0f, 1f);   // 1 on the crown's sunward top, 0 on its far underside
                int count = (int)(lr * lr / (dotR * dotR) * density * (0.55f + 0.9f * side));
                for (int i = 0; i < count; i++)
                {
                    double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble());
                    float x = lx + (float)(Math.Cos(ang) * dist) * lr, y = ly + (float)(Math.Sin(ang) * dist) * lr * 0.9f;
                    float d = dotR * (0.6f + 0.9f * (float)rng.NextDouble());
                    using var brush = new SolidBrush(Color.FromArgb(235, Brushwork.Mix(a, b, (float)rng.NextDouble())));
                    if (leaves && rng.NextDouble() < 0.35)
                    {
                        // A pointed oval turned its own way: a leaf seen from a distance.
                        float la = (float)(rng.NextDouble() * Math.PI * 2), len = d * 2.4f, ca = MathF.Cos(la), sa = MathF.Sin(la);
                        PointF L(float along, float across) => new(x + along * ca - across * sa, y + along * sa + across * ca);
                        using var leaf = new GraphicsPath();
                        leaf.AddClosedCurve([L(-len * 0.5f, 0), L(0, len * 0.3f), L(len * 0.5f, 0), L(0, -len * 0.3f)], 0.6f);
                        g.FillPath(brush, leaf);
                    }
                    else g.FillEllipse(brush, x - d, y - d, d * 2, d * 2);
                }
            }
        }

        using (var solid = new SolidBrush(Color.FromArgb(240, shade)))
            foreach (var l in lobes)
                g.FillEllipse(solid, l.X - l.R * 0.92f, l.Y - l.R * 0.84f, l.R * 1.84f, l.R * 1.68f);
        Coat(shade, body, 0f, 0f, 1.00f, 1.3f, false);
        Coat(body, warm, 0.22f, -0.18f, 0.80f, 1.1f, true);
        Coat(gold, pale, 0.42f, -0.36f, 0.50f, 0.75f, true);
    }

    /// <summary>
    /// Oak branches reaching in from the top corners, the last leaves of the
    /// year still clinging to them in gold, orange and russet. The wood is
    /// the sakura branches grown bare; then at every spot where a blossom
    /// cluster would have been, a small bunch of leaves is painted instead
    /// (each leaf a pointed oval turned its own way), so the clusters read
    /// as foliage and not as flowers.
    ///
    /// Two branches a side since 2026-11-09 (his ask: "there should be a couple more foreground branches too"): the
    /// high one from the very corner and a lower, flatter one a little way down the edge, so each top corner is a
    /// bit of oak canopy rather than one stick. The wood is handed back as geometry (timber) for the things that
    /// stand on it: the branches are the nearest layer in the picture, so a squirrel running out along one or a
    /// bird perched up there is in front of everything else. The lower pair stay above the maples' crowns.
    /// </summary>
    private static List<PointF> PaintCornerBranches(Graphics g, int w, int h, float u, Random rng, out List<(PointF A, PointF B, float Width)> timber)
    {
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.04f), 0.30f, u * 0.44f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.02f), MathF.PI - 0.30f, u * 0.40f, u * 0.016f, 3),  // top right, reaching left
            new(new PointF(-w * 0.01f, h * 0.19f), 0.10f, u * 0.36f, u * 0.016f, 2),            // lower left, flatter, reaching right
            new(new PointF(w * 1.01f, h * 0.16f), MathF.PI - 0.12f, u * 0.38f, u * 0.016f, 2),  // lower right, flatter, reaching left
        ];
        timber = new List<(PointF A, PointF B, float Width)>();
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng, wood: Wood, petalLight: Wood, petalDeep: Wood, bare: true, timberOut: timber);

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
