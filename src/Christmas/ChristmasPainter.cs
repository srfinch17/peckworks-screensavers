using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

/// <summary>The finished backdrop: a still picture plus the facts the animation needs.</summary>
internal sealed class ChristmasScenery
{
    public required uint[] Pixels { get; init; }                        // the painted picture, same layout as a FrameBuffer
    public required List<(PointF At, int Color)> Lights { get; init; }  // every bulb: where it is, and which pastel (an index into Pastels)
    public required bool[] IsSky { get; init; }                         // one true/false per pixel: true where nothing was painted over the sky

    // ---- Facts for the happenings (src/Christmas/Happenings): where things are, so a fox can
    //      walk on the real snow and a present can fall from the real sleigh. ----
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float U { get; init; }                              // the size unit: the height, or less on a tall screen. Size everything by this.
    public required float Horizon { get; init; }                        // the y where the sky ends behind the mountains
    public required (PointF At, float R) Moon { get; init; }            // the moon's center and radius
    public bool[] OpenSky => IsSky;                                     // stencil: true where nothing was painted over the sky and moon (the same one Santa uses)
    public required bool[] OpenValley { get; init; }                    // stencil: true where nothing was painted after the near mountains (so: the valley floor and everything above it is clear)
    public required PointF[] FarRange { get; init; }                    // the far mountains' outline; ask Brushwork.RidgeYAt(FarRange, x) for their height
    public required PointF[] NearRange { get; init; }                   // the near mountains' outline, the same way
    public required Bank Ground { get; init; }                          // the snowy hill; Ground.YAt(x) is its far edge, the crest line against the valley
    public required bool[] OpenOfBranches { get; init; }                // stencil: true where the bare corner branches (and their bulbs) did NOT paint. For things nearer than the pines but behind those branches.

    /// <summary>
    /// Where to STAND on the snow at x: a little way down the hill from its
    /// crest. The crest line (Ground.YAt) is the FAR edge of the snow, and
    /// the pines and the cabin are planted just in front of it. A fox whose
    /// paws were on the crest line was behind every tree it was drawn over,
    /// and seemed to trot along their lowest boughs. This path is nearer
    /// than the pines' trunks and the cabin's foot, so a walker on it
    /// passes in front of them, which is how it is drawn. It is the FAR
    /// LANE of the snowfield; FieldPoint gives every lane nearer than it.
    /// </summary>
    public float WalkY(float x) => Ground.YAt(Math.Clamp(x, 0, Width - 1)) + U * 0.04f;
    public required PointF CabinFoot { get; init; }                     // the middle of the cabin's base, on the snow
    public required SizeF CabinWall { get; init; }                      // the cabin wall's width and height
    public required float CabinRoofRise { get; init; }                  // how far the snowy roof rises above the top of the wall
    public required RectangleF[] CabinWindows { get; init; }            // its two warm windows
    public required RectangleF CabinDoor { get; init; }
    public required PointF ChimneyTop { get; init; }                    // where the smoke comes out
    public required int CabinBulbs { get; init; }                       // the first this-many entries of Lights are the bulbs along the cabin's roof
    public required List<(float X, float Foot, float Top)> Pines { get; init; } // each pine: trunk x, where it meets the snow, its tip (ask Brushwork.PineHalfWidth for its width at any height)
    public required int StarPine { get; init; }                         // which pine wears the star
    public required PointF StarAt { get; init; }                        // the star on top of it
    public required List<PointF> CornerBranchSpots { get; init; }       // points on the bare corner branches' thin twigs and tips

    // ---- The world the residents live in (added 2026-11-10, the "do for Christmas what we did for Thanksgiving" round) ----
    public required List<PointF> Stars { get; init; }                   // the brighter stars, for the scene to twinkle
    public required List<PointF> VillageWindows { get; init; }          // every lit window in the far village, for the scene to flicker
    public required PointF Lamp { get; init; }                          // the middle of the lamp post's lantern (the scene stamps its glow here)
    public required PointF LampFoot { get; init; }                      // where the post meets the snow
    public required (PointF At, float Half) LampBar { get; init; }      // the top of the snow on the lamp's crossbar (a perch for a bird) and half the bar's width
    public required (PointF At, float Rx, float Ry) Pond { get; init; } // the frozen pond: its middle and its two radii (it is an ellipse lying on the snow)
    public required (PointF At, float Rx, float Ry) IceHole { get; init; }   // the hole in the ice near the pond's right end: dark open water (the otters come and go through it)
    public required PointF LogHollow { get; init; }                     // the middle of the dark hollow in the fallen log's cut end (the mice live in there)
    public required float LogR { get; init; }                           // the log's radius (half its thickness)
    public required (float Left, float Right, float TopY) LogTop { get; init; }   // the walkable top of the log's snow cap: from x Left to x Right, at this y
    public required PointF FeederFoot { get; init; }                    // the bird table's post, where it meets the snow
    public required (PointF At, float Half) FeederTray { get; init; }   // the middle of the tray's top edge (birds stand on it) and half its width
    public required (PointF At, float Half) FeederRoof { get; init; }   // the ridge of the little roof over the tray, and half the roof's width (a perch too)
    public required PointF SaplingFoot { get; init; }                   // the tiny fir in the near snow (the mice trim it in a story)
    public required float SaplingHeight { get; init; }
    public required PointF SnowmanFoot { get; init; }                   // where the snowman stands (he is a resident, drawn live, not part of the backdrop)
    public required float SnowmanHeight { get; init; }
    public required List<(PointF At, float R)> Bushes { get; init; }    // the holly bushes: middle and radius

    /// <summary>
    /// THE DEPTH PICTURE of the near props (the lamp post, the log, the bird
    /// table, the tiny fir, the holly, the drifts along the bottom edge):
    /// one number per pixel, 0 where none of them painted, otherwise how
    /// near that prop's FOOT is (see Nearness). Stamp anything that moves on
    /// the snowfield with this and the nearness of its own feet, and the
    /// props nearer than it hide it while it walks in front of the rest.
    /// Like cardboard cut-outs standing in rows on a table: each one hides
    /// what is in the rows behind it and nothing in front.
    /// </summary>
    public required byte[] NearDepth { get; init; }
    public required float CrestMin { get; init; }                       // the highest point of the hill's crest (the smallest y): where nearness starts counting
    public required float CrestMax { get; init; }                       // the LOWEST point of the crest (the largest y), in the dip by the cabin

    /// <summary>
    /// THE FIELD'S TWO BANDS. The herd keeps to level rows in a band just below the crest's lowest dip, from
    /// HerdBandTop to HerdBandBottom. Everything smaller (the fox, the hares) lives at or below SmallBandTop, a
    /// clear gap nearer to us. Animals of the two bands then pass in FRONT of or BEHIND each other, never through:
    /// the first build had the hares' lanes running through the herd's rows, and a hare stood inside a reindeer.
    /// </summary>
    public float HerdBandTop => CrestMax + 0.045f * U;
    public float HerdBandBottom => CrestMax + 0.070f * U;
    public float SmallBandTop => CrestMax + 0.105f * U;

    /// <summary>How near something standing with its feet at y is, 1 (at the crest, far) to 254 (at the bottom edge, close).</summary>
    public byte Nearness(float y) => (byte)Math.Clamp(1 + 253 * (y - CrestMin) / (Height - CrestMin), 1, 254);

    /// <summary>
    /// THE FIELD'S ONE SCALE: how big to draw a thing whose feet are at y,
    /// as a multiple of its size at the very bottom of the screen. Things
    /// farther away look smaller: half size up at the crest, full size at
    /// the bottom edge. Every animal on the snow sizes itself by this, so a
    /// fox by the pines and a fox by the log are the same fox.
    /// </summary>
    public float FieldScale(float y) => 0.5f + 0.5f * Math.Clamp((y - CrestMin) / (Height - CrestMin), 0f, 1f);

    /// <summary>
    /// How many pixels ONE METRE is for a thing whose feet are at y. Every
    /// animal is its real size in metres times this (times a small
    /// "storybook" factor for the tiny ones, so a mouse can be seen at all):
    /// a reindeer 1.1 m at the shoulder, a fox 0.40, a hare 0.25. Compare a
    /// new animal to the lamp post (3 m) and the cabin's door (1.9 m) before
    /// anyone else does.
    /// The slope is steeper than FieldScale's (0.36 at the crest, not 0.5):
    /// the cabin up there is painted at about 37 pixels to the metre at
    /// 1080p, and a reindeer walking past its door has to match the door.
    /// (The first herd was sized by FieldScale and stood as tall as the roof.)
    /// </summary>
    public float Metre(float y) => 0.10f * U * (0.36f + 0.64f * Math.Clamp((y - CrestMin) / (Height - CrestMin), 0f, 1f));

    /// <summary>
    /// True if an animal may STAND with its feet at (x, y): on the screen's
    /// snowfield (at or below the far lane), not on the pond's ice, and not
    /// inside the foot of a near prop (the log, the bird table's post, the
    /// tiny fir, the lamp post, the snowman, the holly). "room" is the
    /// animal's own half width in pixels. Passing BEHIND a prop is fine and
    /// is what the depth picture is for; this is only about where feet go.
    /// </summary>
    public bool CanStand(float x, float y, float room = 0f)
    {
        if (x < 0 || x >= Width || y < WalkY(x) - 1 || y >= Height) return false;
        if (OnPond(x, y, 1.08f + room / Pond.Rx)) return false;
        bool Near(PointF foot, float halfW, float halfH) => MathF.Abs(x - foot.X) < halfW + room && MathF.Abs(y - foot.Y) < halfH;
        float sc = FieldScale(y) * U;
        if (Near(LampFoot, 0.012f * sc, 0.012f * sc) || Near(FeederFoot, 0.012f * sc, 0.012f * sc)) return false;
        if (Near(SaplingFoot, SaplingHeight * 0.30f, 0.016f * sc) || Near(SnowmanFoot, SnowmanHeight * 0.30f, 0.020f * sc)) return false;
        if (x > LogTop.Left - LogR * 2 - room && x < LogTop.Right + LogR * 2 + room && MathF.Abs(y - (LogHollow.Y + LogR * 0.82f)) < LogR * 0.9f) return false;
        foreach (var b in Bushes)
            if (MathF.Abs(x - b.At.X) < b.R + room && y > b.At.Y - b.R * 0.35f) return false;
        return true;
    }

    /// <summary>
    /// A point on the snowfield: x across, and "depth" from 0 (the far lane,
    /// just in front of the pines: the same line as WalkY) to 1 (the bottom
    /// edge of the screen).
    /// </summary>
    public PointF FieldPoint(float x, float depth)
    {
        float far = WalkY(x);
        return new PointF(x, far + (Height - far) * Math.Clamp(depth, 0f, 1f));
    }

    /// <summary>True if (x, y) is on the pond's ice. "grow" widens the test (1.1 = ten percent bigger than the ice).</summary>
    public bool OnPond(float x, float y, float grow = 1f)
    {
        float dx = (x - Pond.At.X) / (Pond.Rx * grow), dy = (y - Pond.At.Y) / (Pond.Ry * grow);
        return dx * dx + dy * dy < 1f;
    }

    /// <summary>
    /// Stamps "page" so that its pixel (originX, originY) lands on a pair of
    /// FEET at (feetX, feetY) on the snowfield, hidden wherever a near prop
    /// stands nearer than those feet (NearDepth). "mirror" flips the page to
    /// face left and flips the origin with it, so the feet stay put. Every
    /// resident and every story draws its ground actors through this one
    /// call; a thing drawn without it floats over the log and the holly.
    /// </summary>
    public void Stamp(FrameBuffer fb, Sprite page, float feetX, float feetY, int originX, int originY, bool mirror = false, float opacity = 1f) =>
        page.Draw(fb, (int)MathF.Round(feetX) - (mirror ? page.Width - 1 - originX : originX), (int)MathF.Round(feetY) - originY,
                  opacity, null, mirror, NearDepth, Nearness(feetY));

    // ---- WHO STANDS WHERE (added after the first review: "no resident knows any other resident exists": the fox
    //      trotted through a reindeer's legs and a hare stood inside one for seventeen seconds) ----
    //
    // FEYNMAN VERSION: a cloakroom list. Every frame each animal on the snow writes down where its feet are and
    // how long it is (Occupy). Before an animal picks a place to go, or takes a step, it asks the list whether
    // somebody ELSE is already standing on that bit of that row (Crowded). Passing in front of or behind another
    // animal is fine and is what the depth picture is for; standing INSIDE one is not. There are two sheets: the
    // one being written this frame and the one finished last frame, which is the one everybody reads, so the answer
    // does not depend on who happened to be updated first.
    private List<(object Who, int Index, float X, float Y, float Half)> _standing = new(64), _writing = new(64);

    /// <summary>Called by the scene once a frame, before the residents think: last frame's sheet becomes the one to read.</summary>
    public void TurnPage() { (_standing, _writing) = (_writing, _standing); _writing.Clear(); }

    /// <summary>Writes one animal on this frame's sheet: its owner and index, its feet, and half its body length in pixels.</summary>
    public void Occupy(object who, int index, float feetX, float feetY, float half) => _writing.Add((who, index, feetX, feetY, half));

    /// <summary>
    /// True if another animal stands on this spot: its feet within "rows" (pixels up or down the screen: the
    /// same row of the field) and its body overlapping a body of half length "half" centred at x. An animal never
    /// counts against itself (same owner AND same index); pass index -1 to ignore everything of that owner.
    /// </summary>
    public bool Crowded(float x, float y, float half, object self, int index, float rows = -1f)
    {
        if (rows < 0) rows = 0.016f * U;
        foreach (var o in _standing)
        {
            if (ReferenceEquals(o.Who, self) && (index < 0 || o.Index == index)) continue;
            if (MathF.Abs(o.Y - y) < rows && MathF.Abs(o.X - x) < o.Half + half) return true;
        }
        return false;
    }

    /// <summary>The nearest other animal's feet to (x, y) within "within" pixels, or null: for stepping round somebody.</summary>
    public PointF? NearestOther(float x, float y, float within, object self, int index)
    {
        PointF? best = null; float bestD = within * within;
        foreach (var o in _standing)
        {
            if (ReferenceEquals(o.Who, self) && (index < 0 || o.Index == index)) continue;
            float d = (o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y);
            if (d < bestD) { bestD = d; best = new PointF(o.X, o.Y); }
        }
        return best;
    }

    // ---- Live facts, set by the scene every frame before the happenings draw ----
    public PointF SantaSleigh { get; set; }                             // the middle of the sleigh's body, in pixels
    public PointF SantaNose { get; set; }                               // Rudolph's nose
    public bool SantaOnScreen { get; set; }                             // false while he is on his off-screen pause
}

/// <summary>
/// Paints the backdrop: a deep blue night sky with stars and a moon, two
/// ranges of snowy mountains, a far village with lit windows down in the
/// valley, a snow-covered hill with a log cabin and snowy pines strung with
/// pastel lights, and below the hill a wide snowfield with a frozen pond, a
/// lamp post, a fallen log, a bird table, a tiny fir and holly bushes. Bare
/// branches reach in from the top corners with more lights on their twigs.
///
/// Like the Sakura painters, this runs ONCE at startup (the scenery never
/// moves) and paints back to front. The pines, the ground, the mountains and
/// the branches come from the engine's shared Brushwork helpers.
///
/// Three things here are for the animation, not the picture:
///
///   - Every bulb's position is handed back, so the scene can make each one
///     twinkle by stamping a soft glow over it. The same for the brighter
///     stars, the village's windows and the lamp.
///   - STENCILS: at a few points in the painting we keep a copy of the
///     picture so far. At the end we compare each copy to the finished
///     picture. Any pixel that did not change since a copy was taken has
///     nothing painted in front of it since then. Santa is only ever drawn
///     on pixels unchanged since the sky, and that is the whole trick that
///     makes him pass BEHIND the corner branches.
///   - THE DEPTH PICTURE (ChristmasScenery.NearDepth): the same copy trick,
///     once per near prop, so each prop's pixels can be marked with how
///     near that prop stands.
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

    private static readonly Color Snow = Color.FromArgb(236, 243, 255);
    private static readonly Color SnowShade = Color.FromArgb(150, 172, 222);   // snow in shadow is blue, never grey: it is lit by the sky

    /// <summary>What PaintCabin measured while it painted, so the facts handed to the scene are the painted ones and not a second guess.</summary>
    private readonly record struct CabinFacts(SizeF Wall, float RoofRise, RectangleF[] Windows, RectangleF Door, PointF ChimneyTop, int Bulbs);

    public static ChristmasScenery Paint(int w, int h, Random rng)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float horizon = h * 0.60f;
        float u = Math.Min(h, w * 9f / 16f);
        var lights = new List<(PointF At, int Color)>();
        var stars = new List<PointF>();
        var villageWindows = new List<PointF>();

        // ---- The paint order, back to front. Keep this list explicit. ----
        PaintSky(g, w, horizon, u, rng, stars);
        PaintMoon(g, w * 0.24f, h * 0.19f, u * 0.065f);
        g.Flush();
        uint[] skyOnly = Brushwork.ToPixels(bmp);   // the copy the sky stencil is made from (see the class summary)

        PointF[] farRange = Brushwork.Ridge(w, horizon, u, 0.09f, 0.05f, 0.02f, 0.09f, rng);
        using (var far = new SolidBrush(Color.FromArgb(62, 84, 140)))
            g.FillPolygon(far, farRange);
        PointF[] nearRange = Brushwork.Ridge(w, horizon, u, 0.03f, 0.03f, 0.012f, 0.04f, rng);
        using (var near = new SolidBrush(Color.FromArgb(104, 128, 184)))
            g.FillPolygon(near, nearRange);
        // The valley floor: snowfields under the moon, paler the nearer they are.
        using (var valley = new LinearGradientBrush(new PointF(0, horizon - 1), new PointF(0, h * 0.80f),
                   Color.FromArgb(104, 128, 184), Color.FromArgb(158, 180, 226)))
            g.FillRectangle(valley, 0, horizon, w, h - horizon);
        PaintTrack(g, w, horizon + u * 0.008f, u, rng);   // BEFORE the valley's copy is taken: the train is drawn over whatever is in that copy, so it runs ON the rails
        g.Flush();
        uint[] upToValley = Brushwork.ToPixels(bmp);   // a second stencil copy: "unchanged since here" = the valley floor is still clear (for the toy train)

        Bank ground = MakeGround(w, h);
        float crestMin = float.MaxValue;
        float crestMax = 0;
        foreach (PointF p in ground.Edge) { crestMin = Math.Min(crestMin, p.Y); if (p.X >= 0 && p.X <= w) crestMax = Math.Max(crestMax, p.Y); }
        PaintValley(g, w, horizon, u, rng, ground, nearRange, villageWindows);

        ground.Paint(g, Color.FromArgb(222, 233, 252), Color.FromArgb(118, 140, 196), Color.FromArgb(220, 255, 255, 255), Math.Max(1.5f, u * 0.004f));
        PaintDrifts(g, w, h, u, rng, ground);

        // Local copies of the scenery's own lane rules (the facts below repeat them; they are one line each).
        float WalkY(float x) => ground.YAt(Math.Clamp(x, 0, w - 1)) + u * 0.04f;
        PointF Field(float x, float depth) { float far = WalkY(x); return new PointF(x, far + (h - far) * depth); }
        float Scale(float y) => 0.5f + 0.5f * Math.Clamp((y - crestMin) / (h - crestMin), 0f, 1f);

        var pond = (At: new PointF(w * 0.36f, h * 0.875f), Rx: u * 0.27f, Ry: u * 0.046f);
        var iceHole = (At: new PointF(pond.At.X + pond.Rx * 0.52f, pond.At.Y + pond.Ry * 0.10f), Rx: pond.Rx * 0.115f, Ry: pond.Ry * 0.26f);
        PaintPond(g, pond.At, pond.Rx, pond.Ry, u, rng, w * 0.24f, iceHole);

        var cabinFoot = new PointF(w * 0.58f, ground.YAt(w * 0.58f) + u * 0.010f);
        PaintFootprints(g, new PointF(cabinFoot.X, cabinFoot.Y + u * 0.012f), Field(w * 0.295f, 0.20f), u, Scale);
        CabinFacts cabin = PaintCabin(g, cabinFoot.X, cabinFoot.Y, u, rng, lights);

        // (X as a fraction of the width, height in u). Tallest first in each
        // group, so the shorter trees in front overlap the taller ones behind.
        (float X, float Height)[] pines =
        [
            (0.05f, 0.44f), (0.23f, 0.37f), (0.14f, 0.30f), (0.34f, 0.22f),
            (0.88f, 0.45f), (0.80f, 0.33f), (0.96f, 0.31f),
        ];
        var pineFacts = new List<(float X, float Foot, float Top)>();
        foreach (var p in pines)
        {
            float x = w * p.X, foot = ground.YAt(x) + u * 0.02f, top = foot - u * p.Height;
            Brushwork.Pine(g, x, top, foot, u, rng, snowy: true);
            StringLights(g, x, top, foot, u, rng, lights);
            pineFacts.Add((x, foot, top));
        }
        // The star goes on the tallest tree (the one at 88% across, number 4 in the list).
        const int starPine = 4;
        float starX = w * 0.88f, starY = ground.YAt(starX) + u * 0.02f - u * 0.45f - u * 0.008f;
        PaintStar(g, starX, starY, u * 0.016f);
        lights.Add((new PointF(starX, starY), Butter));

        // ---- The near props. Where each stands (its FOOT), then its soft shadow, then the prop itself. ----
        PointF lampFoot = Field(w * 0.285f, 0.18f);
        float lampTall = u * 0.27f * Scale(lampFoot.Y);
        PointF feederFoot = Field(w * 0.665f, 0.66f);
        float feederTall = u * 0.165f * Scale(feederFoot.Y);
        PointF saplingFoot = Field(w * 0.545f, 0.80f);
        float saplingTall = u * 0.15f * Scale(saplingFoot.Y);
        PointF snowmanFoot = Field(w * 0.835f, 0.62f);
        float snowmanTall = u * 0.24f * Scale(snowmanFoot.Y);
        float logR = u * 0.030f, logLeft = w * 0.045f, logRight = logLeft + u * 0.27f, logFootY = h * 0.955f;
        var bushes = new List<(PointF At, float R)>
        {
            (new PointF(w * 0.015f, h * 0.985f), u * 0.075f),
            (new PointF(w * 0.965f, h * 0.975f), u * 0.085f),
        };

        // Shadows and pools of light go on the snow BEFORE the depth picture
        // starts recording: they lie flat on the ground, so nothing walks behind them.
        PaintLampPool(g, lampFoot, u * 0.11f * Scale(lampFoot.Y));
        GroundShadow(g, feederFoot, u * 0.05f * Scale(feederFoot.Y));
        GroundShadow(g, saplingFoot, u * 0.05f * Scale(saplingFoot.Y));
        GroundShadow(g, new PointF((logLeft + logRight) / 2, logFootY), (logRight - logLeft) * 0.56f);
        foreach (var b in bushes) GroundShadow(g, new PointF(b.At.X, b.At.Y + b.R * 0.1f), b.R * 1.2f);

        // ---- The depth picture. "Near" paints one prop and marks every pixel
        //      that prop changed with how near its foot stands (see the class summary). ----
        var nearDepth = new byte[w * h];
        g.Flush();
        uint[] before = Brushwork.ToPixels(bmp);
        void Near(float footY, Action paint)
        {
            paint();
            g.Flush();
            uint[] after = Brushwork.ToPixels(bmp);
            byte n = (byte)Math.Clamp(1 + 253 * (footY - crestMin) / (h - crestMin), 1, 254);
            for (int i = 0; i < after.Length; i++)
                if (after[i] != before[i] && nearDepth[i] < n) nearDepth[i] = n;
            before = after;
        }

        PointF lamp = default;
        Near(lampFoot.Y, () => lamp = PaintLampPost(g, lampFoot, lampTall));
        (PointF At, float Half) tray = default, roof = default;
        Near(feederFoot.Y, () => (tray, roof) = PaintFeeder(g, feederFoot, feederTall));
        Near(saplingFoot.Y, () =>
        {
            // The shared pine brush, handed a smaller "u" so its boughs and snow dabs shrink with the tree.
            float su = saplingTall / 0.30f;
            Brushwork.Pine(g, saplingFoot.X, saplingFoot.Y - saplingTall, saplingFoot.Y, su, rng, snowy: true);
        });
        PointF hollow = default;
        float logTopY = 0;
        Near(logFootY, () => (hollow, logTopY) = PaintLog(g, logLeft, logRight, logFootY, logR, u, rng));
        foreach (var b in bushes)
            Near(b.At.Y + b.R * 0.3f, () => PaintHolly(g, b.At, b.R, u, rng));
        Near(h - 1, () => PaintFrontDrifts(g, w, h, u, rng));

        g.Flush();
        uint[] upToBranches = Brushwork.ToPixels(bmp);   // a third stencil copy: "unchanged since here" = no corner branch in front
        List<PointF> cornerSpots = PaintCornerBranches(g, w, h, u, rng, lights);

        g.Flush();
        uint[] pixels = Brushwork.ToPixels(bmp);
        var isSky = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) isSky[i] = pixels[i] == skyOnly[i];

        return new ChristmasScenery
        {
            Pixels = pixels, Lights = lights, IsSky = isSky,
            Width = w, Height = h, U = u, Horizon = horizon,
            Moon = (new PointF(w * 0.24f, h * 0.19f), u * 0.065f),   // the same numbers PaintMoon was given above
            OpenValley = Brushwork.Unchanged(upToValley, pixels),
            OpenOfBranches = Brushwork.Unchanged(upToBranches, pixels),
            FarRange = farRange, NearRange = nearRange, Ground = ground,
            CabinFoot = cabinFoot, CabinWall = cabin.Wall, CabinRoofRise = cabin.RoofRise,
            CabinWindows = cabin.Windows, CabinDoor = cabin.Door, ChimneyTop = cabin.ChimneyTop, CabinBulbs = cabin.Bulbs,
            Pines = pineFacts, StarPine = starPine, StarAt = new PointF(starX, starY),
            CornerBranchSpots = cornerSpots,
            Stars = stars, VillageWindows = villageWindows,
            Lamp = lamp, LampFoot = lampFoot, LampBar = (new PointF(lamp.X, lamp.Y + lampTall * 0.130f), lampTall * 0.075f), Pond = pond, IceHole = iceHole,
            LogHollow = hollow, LogR = logR, LogTop = (logLeft + logR * 0.4f, logRight - logR * 0.6f, logTopY),
            FeederFoot = feederFoot, FeederTray = tray, FeederRoof = roof,
            SaplingFoot = saplingFoot, SaplingHeight = saplingTall,
            SnowmanFoot = snowmanFoot, SnowmanHeight = snowmanTall,
            Bushes = bushes, NearDepth = nearDepth, CrestMin = crestMin, CrestMax = crestMax,
        };
    }

    // ================================================================ sky

    /// <summary>
    /// A clear winter night: nearly black blue overhead, lightening toward
    /// the horizon (you look through more air there, so it washes out), with
    /// a scatter of stars, most of them faint and a few bright. The bright
    /// ones are handed back so the scene can make them twinkle.
    /// </summary>
    private static void PaintSky(Graphics g, int w, float horizon, float u, Random rng, List<PointF> bright)
    {
        using (var sky = new LinearGradientBrush(new PointF(0, 0), new PointF(0, horizon + 1), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(6, 10, 38), Color.FromArgb(18, 32, 86), Color.FromArgb(60, 86, 152)],
                Positions = [0f, 0.55f, 1f],
            },
        })
            g.FillRectangle(sky, 0, 0, w, horizon + 1);

        for (int i = 0; i < 260; i++)
        {
            float x = (float)rng.NextDouble() * w, y = (float)Math.Pow(rng.NextDouble(), 1.3) * horizon * 0.85f;
            float size = (float)Math.Pow(rng.NextDouble(), 3);
            float r = Math.Max(0.6f, u * (0.0008f + 0.0016f * size));
            using var star = new SolidBrush(Color.FromArgb(80 + rng.Next(170), 232, 240, 255));
            g.FillEllipse(star, x - r, y - r, r * 2, r * 2);
            if (size > 0.35f) bright.Add(new PointF(x, y));
        }
    }

    /// <summary>A cool white moon with a wide soft glow around it (a "path gradient": strongest at the center, nothing at the edge), and a few faint grey seas on its face.</summary>
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
        // The seas: three soft patches a shade darker, the "face" everyone knows. Faint, or the moon reads as dirty.
        using var sea = new SolidBrush(Color.FromArgb(24, 150, 170, 210));
        g.FillEllipse(sea, x - r * 0.55f, y - r * 0.50f, r * 0.60f, r * 0.50f);
        g.FillEllipse(sea, x + r * 0.05f, y - r * 0.20f, r * 0.50f, r * 0.62f);
        g.FillEllipse(sea, x - r * 0.40f, y + r * 0.25f, r * 0.44f, r * 0.34f);
    }

    /// <summary>
    /// The railway the toy train runs on, along the foot of the mountains
    /// (his line: "that train in the back is great except it needs a
    /// track"). We see it from far off and from the side, so it is small:
    /// a low bank of snow the line is built up on, a row of dark sleepers
    /// (the wooden cross-beams) showing as short ticks under the rail, the
    /// rail itself as one thin steel line with a paler glint along its top
    /// where the moon catches it, and a telegraph pole every so often with
    /// a sagging wire between them, which is what makes a far-off line of
    /// track read as a railway and not as a pencil stroke. "railY" is where
    /// the train's wheels sit (ToyTrain.cs uses the same number).
    /// </summary>
    private static void PaintTrack(Graphics g, int w, float railY, float u, Random rng)
    {
        float bank = Math.Max(2f, u * 0.007f);
        using (var snow = new SolidBrush(Color.FromArgb(150, 172, 222)))
            g.FillRectangle(snow, 0, railY, w, bank);                                    // the built-up bed, a shade paler than the valley floor behind it
        using (var lit = new SolidBrush(Color.FromArgb(170, 204, 220, 250)))
            g.FillRectangle(lit, 0, railY + bank * 0.15f, w, Math.Max(1f, bank * 0.3f)); // its moonlit top
        using (var tie = new SolidBrush(Color.FromArgb(54, 46, 62)))
            for (float x = 0; x < w; x += Math.Max(3f, u * 0.0075f))
                g.FillRectangle(tie, x, railY, Math.Max(1f, u * 0.0022f), Math.Max(1.5f, bank * 0.42f));
        using (var rail = new Pen(Color.FromArgb(40, 44, 66), Math.Max(1f, u * 0.0016f)))
            g.DrawLine(rail, 0, railY, w, railY);
        using (var glint = new Pen(Color.FromArgb(120, 206, 220, 250), Math.Max(1f, u * 0.0008f)))
            g.DrawLine(glint, 0, railY - Math.Max(1f, u * 0.0012f), w, railY - Math.Max(1f, u * 0.0012f));

        // Telegraph poles on the far side of the line, and the wire between them. A hanging wire sags in the
        // middle of each span and is tied up at the poles: 4 * t * (1 - t) is zero at both ends and one halfway.
        float tall = u * 0.030f, gap = u * 0.17f, first = (float)rng.NextDouble() * gap;
        using var wood = new Pen(Color.FromArgb(50, 44, 60), Math.Max(1f, u * 0.0018f));
        using var wire = new Pen(Color.FromArgb(110, 40, 44, 66), 1f);
        for (float x = first - gap; x < w + gap; x += gap)
        {
            g.DrawLine(wood, x, railY, x, railY - tall);
            g.DrawLine(wood, x - tall * 0.16f, railY - tall * 0.86f, x + tall * 0.16f, railY - tall * 0.86f);   // the crossarm
            var span = new PointF[9];
            for (int i = 0; i < span.Length; i++)
            {
                float t = i / (float)(span.Length - 1);
                span[i] = new PointF(x + gap * t, railY - tall * 0.86f + tall * 0.22f * 4 * t * (1 - t));
            }
            g.DrawLines(wire, span);
        }
    }

    // ================================================================ the valley

    /// <summary>
    /// The valley between the mountains and our hill: a scatter of tiny dark
    /// firs (smaller the farther off they stand) and a village of little
    /// houses round a church, every window lit. All of it is painted AFTER
    /// the valley stencil's copy was taken, so the toy train, which runs
    /// along the foot of the mountains, passes BEHIND the houses and trees.
    /// </summary>
    private static void PaintValley(Graphics g, int w, float horizon, float u, Random rng, Bank ground, PointF[] nearRange, List<PointF> windows)
    {
        // ---- Far firs: tiny dark triangles with a pale side, in loose clumps ----
        using (var fir = new SolidBrush(Color.FromArgb(40, 58, 96)))
        using (var firLit = new SolidBrush(Color.FromArgb(90, 190, 206, 240)))
        {
            for (int i = 0; i < 90; i++)
            {
                float x = (float)rng.NextDouble() * w;
                float crest = ground.YAt(Math.Clamp(x, 0, w - 1));
                float top = horizon + u * 0.012f;
                if (crest <= top + u * 0.01f) continue;
                float t = (float)rng.NextDouble();
                float y = top + (crest - top) * t;                            // its foot
                float tall = Math.Max(2f, u * (0.010f + 0.022f * t));         // nearer = bigger
                g.FillPolygon(fir, [new PointF(x - tall * 0.32f, y), new PointF(x, y - tall), new PointF(x + tall * 0.32f, y)]);
                g.FillPolygon(firLit, [new PointF(x - tall * 0.32f, y), new PointF(x, y - tall), new PointF(x - tall * 0.05f, y)]);
            }
        }

        // ---- The village: houses in the gap between the left pines and the cabin, the church in the middle ----
        const int n = 9;
        var houses = new List<(float X, float Base, float S, bool Church)>();
        for (int i = 0; i < n; i++)
        {
            float t = (float)rng.NextDouble();
            float x = w * (0.375f + 0.15f * i / (n - 1)) + u * 0.006f * ((float)rng.NextDouble() - 0.5f);
            houses.Add((x, horizon + u * (0.034f + 0.030f * t), Math.Max(4f, u * (0.020f + 0.012f * t)), i == n / 2));
        }
        houses.Sort((a, b) => a.Base.CompareTo(b.Base));                      // farthest first, so nearer houses overlap them
        using var wall = new SolidBrush(Color.FromArgb(56, 62, 100));
        using var wallDark = new SolidBrush(Color.FromArgb(44, 50, 84));
        using var snow = new SolidBrush(Snow);
        using var glass = new SolidBrush(Color.FromArgb(255, 214, 132));
        foreach (var hs in houses)
        {
            float s = hs.S, x = hs.X, by = hs.Base;
            float pane = Math.Max(1f, s * 0.17f);
            void Window(float wx, float wy)
            {
                g.FillRectangle(glass, wx - pane / 2, wy - pane / 2, pane, pane);
                windows.Add(new PointF(wx, wy));
            }
            if (hs.Church)
            {
                // The nave (a long low hall), then the tower with its spire at one end.
                g.FillRectangle(wall, x - s * 0.8f, by - s * 0.7f, s * 1.6f, s * 0.7f);
                g.FillPolygon(snow, [new PointF(x - s * 0.9f, by - s * 0.7f), new PointF(x - s * 0.5f, by - s * 1.1f), new PointF(x + s * 0.8f, by - s * 1.1f), new PointF(x + s * 0.9f, by - s * 0.7f)]);
                g.FillRectangle(wallDark, x - s * 0.9f, by - s * 1.7f, s * 0.5f, s * 1.7f);
                g.FillPolygon(snow, [new PointF(x - s * 0.98f, by - s * 1.7f), new PointF(x - s * 0.65f, by - s * 2.7f), new PointF(x - s * 0.32f, by - s * 1.7f)]);
                Window(x - s * 0.65f, by - s * 1.35f);
                Window(x - s * 0.1f, by - s * 0.36f);
                Window(x + s * 0.4f, by - s * 0.36f);
            }
            else
            {
                g.FillRectangle(wall, x - s * 0.5f, by - s * 0.62f, s, s * 0.62f);
                g.FillPolygon(snow, [new PointF(x - s * 0.64f, by - s * 0.60f), new PointF(x, by - s * 1.12f), new PointF(x + s * 0.64f, by - s * 0.60f)]);
                g.FillRectangle(wallDark, x + s * 0.20f, by - s * 1.08f, Math.Max(1f, s * 0.14f), s * 0.3f);   // a chimney
                Window(x - s * 0.20f, by - s * 0.32f);
                if (rng.Next(3) > 0) Window(x + s * 0.22f, by - s * 0.32f);
            }
        }
    }

    // ================================================================ ground, drifts, pond

    /// <summary>
    /// The snowy hill: one bank of ground running the full width of the
    /// screen, in soft rolls. Everything that stands on it asks it "how high
    /// is the ground here?". Its crest sits about seven tenths of the way
    /// down the screen, which leaves a wide snowfield below it for the
    /// animals to live on.
    /// </summary>
    private static Bank MakeGround(int w, int h) => new(
    [
        new PointF(-2, h * 0.705f), new PointF(w * 0.14f, h * 0.69f), new PointF(w * 0.30f, h * 0.72f),
        new PointF(w * 0.46f, h * 0.745f), new PointF(w * 0.62f, h * 0.73f), new PointF(w * 0.80f, h * 0.695f),
        new PointF(w + 2, h * 0.705f),
    ], h);

    /// <summary>
    /// Gives the snowfield its shape. A flat blue-white gradient reads as a
    /// sheet of paper; real snow lies in long low swells, each one catching
    /// the moonlight along its top and holding a blue shadow in the dip
    /// below it. So: a few dozen very wide, very flat patches, a pale one
    /// with a bluish one tucked under it, bigger toward the bottom of the
    /// screen (nearer to us).
    /// </summary>
    private static void PaintDrifts(Graphics g, int w, int h, float u, Random rng, Bank ground)
    {
        for (int i = 0; i < 26; i++)
        {
            float x = (float)rng.NextDouble() * w;
            float crest = ground.YAt(Math.Clamp(x, 0, w - 1)) + u * 0.03f;
            float t = (float)rng.NextDouble();
            float y = crest + (h - crest) * t;
            float rx = u * (0.10f + 0.22f * t) * (0.7f + 0.6f * (float)rng.NextDouble()), ry = rx * 0.11f;
            Soft(g, x + rx * 0.06f, y + ry * 0.9f, rx, ry, Color.FromArgb(64, 92, 116, 184));   // the shadow in the dip
            Soft(g, x, y, rx, ry, Color.FromArgb(96, 255, 255, 255));                           // the lit swell
        }
    }

    /// <summary>A soft-edged ellipse: full colour in the middle, fading to nothing at the rim.</summary>
    private static void Soft(Graphics g, float cx, float cy, float rx, float ry, Color c)
    {
        if (rx < 1 || ry < 0.5f) return;
        using var path = new GraphicsPath();
        path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = c,
            SurroundColors = [Color.FromArgb(0, c)],
            Blend = Sprite.SoftFalloff,
        };
        g.FillPath(brush, path);
    }

    /// <summary>The dark soft patch under a prop, which is what makes it stand ON the snow instead of floating over it.</summary>
    private static void GroundShadow(Graphics g, PointF foot, float r) =>
        Soft(g, foot.X + r * 0.15f, foot.Y, r, Math.Max(1.5f, r * 0.20f), Color.FromArgb(96, 70, 92, 160));

    /// <summary>
    /// The frozen pond: an ellipse of ice lying on the snow (we look at it
    /// from the side and a little above, so a round pond is squashed flat).
    /// Back to front: a blue shadow where the far bank's lip hangs over the
    /// ice, the ice itself (darker far off, paler near), the moon's long
    /// reflection, a few pale scratches, and lumps of snow along the near
    /// rim so the edge is a broken line of drifts and not a ruled curve.
    /// </summary>
    private static void PaintPond(Graphics g, PointF at, float rx, float ry, float u, Random rng, float moonX, (PointF At, float Rx, float Ry) hole)
    {
        using (var lip = new SolidBrush(SnowShade))
            g.FillEllipse(lip, at.X - rx * 1.03f, at.Y - ry * 1.16f, rx * 2.06f, ry * 2.2f);
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(at.X - rx, at.Y - ry, rx * 2, ry * 2);
            using (var ice = new LinearGradientBrush(new PointF(0, at.Y - ry - 1), new PointF(0, at.Y + ry + 1),
                       Color.FromArgb(88, 122, 190), Color.FromArgb(160, 194, 236)))
                g.FillPath(ice, path);

            GraphicsState saved = g.Save();
            g.SetClip(path, CombineMode.Intersect);      // everything below stays on the ice
            // The moon on the ice: a pale streak on the moon's side of the pond.
            float mx = Math.Clamp(moonX + rx * 0.25f, at.X - rx * 0.6f, at.X + rx * 0.6f);
            Soft(g, mx, at.Y, rx * 0.20f, ry * 0.95f, Color.FromArgb(120, 236, 244, 255));
            Soft(g, mx, at.Y, rx * 0.07f, ry * 0.9f, Color.FromArgb(150, 255, 255, 255));
            // Scratches and cracks: thin pale arcs, as skates and frost leave.
            using (var scratch = new Pen(Color.FromArgb(70, 236, 244, 255), Math.Max(1f, u * 0.0012f)))
                for (int i = 0; i < 9; i++)
                {
                    float cx = at.X + rx * 1.4f * ((float)rng.NextDouble() - 0.5f), cy = at.Y + ry * 1.2f * ((float)rng.NextDouble() - 0.5f);
                    float ax = rx * (0.10f + 0.20f * (float)rng.NextDouble()), ay = ax * ry / rx * 0.8f;
                    g.DrawArc(scratch, cx - ax, cy - ay, ax * 2, ay * 2, rng.Next(360), 40 + rng.Next(110));
                }
            // The hole: open black water with a pale broken rim of ice round it, thicker on the near side
            // (we see the ice's cut edge there, as you see the near wall of a hole in the ground).
            using (var rim = new SolidBrush(Color.FromArgb(214, 232, 250)))
                g.FillEllipse(rim, hole.At.X - hole.Rx * 1.16f, hole.At.Y - hole.Ry * 1.10f, hole.Rx * 2.32f, hole.Ry * 2.5f);
            using (var water = new SolidBrush(Color.FromArgb(16, 28, 64)))
                g.FillEllipse(water, hole.At.X - hole.Rx, hole.At.Y - hole.Ry, hole.Rx * 2, hole.Ry * 2);
            Soft(g, hole.At.X - hole.Rx * 0.3f, hole.At.Y - hole.Ry * 0.2f, hole.Rx * 0.5f, hole.Ry * 0.4f, Color.FromArgb(70, 150, 180, 230));   // the sky's sheen on the water
            g.Restore(saved);
        }
        // Snow lumps along the rim, more and bigger on the near side.
        using var snow = new SolidBrush(Color.FromArgb(232, 240, 255));
        for (int i = 0; i < 46; i++)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2);
            float near = 0.5f + 0.5f * MathF.Sin(a);                          // 1 at the near (bottom) rim, 0 at the far one
            if (rng.NextDouble() > 0.35 + 0.65 * near) continue;
            float lr = u * (0.006f + 0.012f * (float)rng.NextDouble()) * (0.6f + 0.6f * near);
            float lx = at.X + MathF.Cos(a) * rx * 1.01f, ly = at.Y + MathF.Sin(a) * ry * 1.06f;
            g.FillEllipse(snow, lx - lr * 1.6f, ly - lr * 0.5f, lr * 3.2f, lr);
        }
    }

    /// <summary>
    /// A trail of footprints from the cabin's door down to the lamp post:
    /// pairs of small blue dimples along a winding path, bigger as the path
    /// comes nearer. Somebody lives here and walked out to light the lamp.
    /// </summary>
    private static void PaintFootprints(Graphics g, PointF from, PointF to, float u, Func<float, float> scale)
    {
        using var dimple = new SolidBrush(Color.FromArgb(84, 96, 118, 180));
        PointF c1 = new(from.X - u * 0.02f, from.Y + (to.Y - from.Y) * 0.55f), c2 = new(to.X + (from.X - to.X) * 0.55f, to.Y - u * 0.035f);
        const int steps = 38;
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps, m = 1 - t;
            // A cubic Bezier curve: a smooth path pulled toward two "handle" points on its way from one end to the other.
            float x = m * m * m * from.X + 3 * m * m * t * c1.X + 3 * m * t * t * c2.X + t * t * t * to.X;
            float y = m * m * m * from.Y + 3 * m * m * t * c1.Y + 3 * m * t * t * c2.Y + t * t * t * to.Y;
            float s = u * 0.0045f * scale(y), side = (i % 2 == 0 ? 1 : -1) * s * 1.3f;   // left foot, right foot
            g.FillEllipse(dimple, x - s * 1.4f + side * 0.4f, y - s * 0.5f + side, s * 2.8f, s);
        }
    }

    // ================================================================ the cabin

    /// <summary>
    /// A log cabin: a wall of stacked logs with their round ends showing at
    /// the corners, a thick blanket of snow for a roof with icicles hanging
    /// from the eave, a stone chimney, a red door with a wreath, two warm
    /// windows (a Christmas tree stands behind the left one), and a stack of
    /// firewood under its own cap of snow. Warm light spills onto the snow in
    /// front, and a string of pastel bulbs hangs along the edge of the roof
    /// (added to the same list as the tree lights, so they twinkle too).
    /// The smoke is NOT painted: the scene makes it live (Core/ChimneySmoke.cs).
    /// </summary>
    private static CabinFacts PaintCabin(Graphics g, float x, float footY, float u, Random rng, List<(PointF At, int Color)> lights)
    {
        float wallW = u * 0.21f, wallH = u * 0.088f, top = footY - wallH, rise = u * 0.058f;
        float left = x - wallW / 2, right = x + wallW / 2;

        // Light from the windows and the door lying on the snow.
        Soft(g, x, footY + u * 0.016f, wallW * 0.62f, u * 0.030f, Color.FromArgb(130, 255, 206, 130));

        // The firewood, at the left end: three rows of round log ends, under snow.
        using (var endWood = new SolidBrush(Color.FromArgb(176, 132, 88)))
        using (var ring = new SolidBrush(Color.FromArgb(128, 90, 60)))
        using (var snowCap = new SolidBrush(Snow))
        {
            float r = Math.Max(1.5f, u * 0.0075f), px = left - r * 9.5f;
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 4 - row; k++)
                {
                    float cx = px + r * (1 + row) + k * r * 2, cy = footY - r - row * r * 1.75f;
                    g.FillEllipse(endWood, cx - r, cy - r, r * 2, r * 2);
                    g.FillEllipse(ring, cx - r * 0.4f, cy - r * 0.4f, r * 0.8f, r * 0.8f);
                }
            g.FillEllipse(snowCap, px + r * 1.4f, footY - r * 6.4f, r * 5.4f, r * 1.9f);
        }

        // The stone chimney, up the right end of the wall and through the roof.
        float chimW = u * 0.026f, chimX = x + wallW * 0.24f, chimTop = top - rise - u * 0.040f;
        using (var stone = new SolidBrush(Color.FromArgb(112, 108, 122)))
            g.FillRectangle(stone, chimX, chimTop, chimW, top - chimTop + u * 0.01f);
        using (var stoneDark = new SolidBrush(Color.FromArgb(84, 80, 98)))
            for (int i = 0; i < 9; i++)
            {
                float sx = chimX + chimW * (0.1f + 0.6f * (float)rng.NextDouble()), sy = chimTop + (top - chimTop) * (float)rng.NextDouble();
                g.FillRectangle(stoneDark, sx, sy, chimW * 0.3f, Math.Max(1f, u * 0.004f));
            }

        // The wall: logs.
        using (var wall = new SolidBrush(Color.FromArgb(98, 62, 44)))
            g.FillRectangle(wall, left, top, wallW, wallH + u * 0.004f);
        const int logs = 6;
        using (var seam = new Pen(Color.FromArgb(62, 38, 28), Math.Max(1f, u * 0.0018f)))
        using (var sheen = new Pen(Color.FromArgb(60, 190, 140, 100), Math.Max(1f, u * 0.0014f)))
            for (int i = 0; i < logs; i++)
            {
                float y = top + wallH * i / logs;
                if (i > 0) g.DrawLine(seam, left, y, right, y);
                g.DrawLine(sheen, left, y + wallH / logs * 0.3f, right, y + wallH / logs * 0.3f);   // each log's rounded top catches a little light
            }
        using (var endWood = new SolidBrush(Color.FromArgb(150, 108, 74)))
            for (int i = 0; i < logs; i++)
            {
                float r = wallH / logs * 0.5f, cy = top + wallH * (i + 0.5f) / logs;
                g.FillEllipse(endWood, left - r * 1.2f, cy - r, r * 2, r * 2);
                g.FillEllipse(endWood, right - r * 0.8f, cy - r, r * 2, r * 2);
            }

        // The roof: a thick slab of snow, its lower edge sagging in soft scallops, with the eave's shadow under it.
        using (var eave = new SolidBrush(Color.FromArgb(50, 30, 24)))
            g.FillRectangle(eave, left - wallW * 0.06f, top - u * 0.002f, wallW * 1.12f, u * 0.008f);
        using (var snow = new SolidBrush(Snow))
        using (var shade = new SolidBrush(Color.FromArgb(120, SnowShade)))
        {
            PointF[] roof = [
                new PointF(left - wallW * 0.10f, top + u * 0.006f), new PointF(left + wallW * 0.14f, top - rise),
                new PointF(right - wallW * 0.14f, top - rise), new PointF(right + wallW * 0.10f, top + u * 0.006f)];
            g.FillPolygon(snow, roof);
            g.FillPolygon(shade, [roof[0], new PointF(roof[0].X + wallW * 0.05f, top - rise * 0.25f), new PointF(roof[3].X - wallW * 0.05f, top - rise * 0.25f), roof[3]]);   // the roof's lower half lies in its own shadow
            for (int i = 0; i < 9; i++)                                       // scallops along the eave
            {
                float sx = roof[0].X + (roof[3].X - roof[0].X) * (i + 0.5f) / 9, sw = (roof[3].X - roof[0].X) / 9 * 0.62f;
                g.FillEllipse(snow, sx - sw, top - u * 0.004f, sw * 2, u * 0.016f);
            }
            g.FillEllipse(snow, chimX - chimW * 0.2f, chimTop - u * 0.007f, chimW * 1.4f, u * 0.014f);   // the cap on the chimney
        }
        // Icicles: thin pale spikes of uneven length under the eave.
        using (var ice = new SolidBrush(Color.FromArgb(200, 214, 232, 255)))
            for (float ix = left - wallW * 0.07f; ix < right + wallW * 0.07f; ix += u * (0.006f + 0.008f * (float)rng.NextDouble()))
            {
                float len = u * (0.006f + 0.016f * (float)rng.NextDouble()), half = Math.Max(0.6f, u * 0.0016f);
                g.FillPolygon(ice, [new PointF(ix - half, top + u * 0.010f), new PointF(ix + half, top + u * 0.010f), new PointF(ix, top + u * 0.010f + len)]);
            }

        // The door, with a step and a wreath.
        float doorW = u * 0.030f, doorH = wallH * 0.74f;
        var door = new RectangleF(x - doorW / 2, footY - doorH, doorW, doorH);
        using (var doorPaint = new SolidBrush(Color.FromArgb(132, 40, 38)))
            g.FillRectangle(doorPaint, door);
        using (var step = new SolidBrush(Color.FromArgb(214, 226, 250)))
            g.FillRectangle(step, door.X - doorW * 0.25f, footY - u * 0.002f, doorW * 1.5f, u * 0.006f);
        float wr = Math.Max(2f, doorW * 0.30f);
        using (var wreath = new Pen(Color.FromArgb(40, 104, 60), Math.Max(1.2f, wr * 0.55f)))
            g.DrawEllipse(wreath, x - wr, door.Y + doorH * 0.30f - wr, wr * 2, wr * 2);
        using (var bow = new SolidBrush(Color.FromArgb(226, 48, 52)))
            g.FillEllipse(bow, x - wr * 0.45f, door.Y + doorH * 0.30f + wr * 0.55f, wr * 0.9f, wr * 0.8f);

        // The windows. Behind the left one stands the family's tree.
        float winW = u * 0.036f, winH = u * 0.034f;
        var windows = new RectangleF[2];
        using var glass = new SolidBrush(Color.FromArgb(255, 214, 132));
        using var bar = new Pen(Color.FromArgb(72, 44, 32), Math.Max(1f, u * 0.0024f));
        using var sill = new SolidBrush(Snow);
        for (int i = 0; i < 2; i++)
        {
            float wx = x + (i == 0 ? -1 : 1) * wallW * 0.30f, wy = top + wallH * 0.46f;
            var win = new RectangleF(wx - winW / 2, wy - winH / 2, winW, winH);
            windows[i] = win;
            g.FillRectangle(glass, win);
            if (i == 0)
            {
                using var tree = new SolidBrush(Color.FromArgb(52, 110, 64));
                g.FillPolygon(tree, [new PointF(wx - winW * 0.30f, win.Bottom), new PointF(wx, win.Y + winH * 0.12f), new PointF(wx + winW * 0.30f, win.Bottom)]);
                Color[] baubles = [Color.FromArgb(230, 60, 60), Color.FromArgb(90, 150, 240), Color.FromArgb(255, 246, 200)];
                float br = Math.Max(0.8f, winW * 0.05f);
                for (int k = 0; k < 5; k++)
                {
                    using var bb = new SolidBrush(baubles[k % 3]);
                    float ty = 0.35f + 0.13f * k, half = winW * 0.26f * (ty - 0.12f);
                    g.FillEllipse(bb, wx + half * (k % 2 == 0 ? -0.6f : 0.6f) - br, win.Y + winH * ty - br, br * 2, br * 2);
                }
            }
            g.DrawRectangle(bar, win.X, win.Y, win.Width, win.Height);
            g.DrawLine(bar, wx, win.Y, wx, win.Bottom);                       // the cross in the window
            g.DrawLine(bar, win.X, wy, win.Right, wy);
            g.FillEllipse(sill, win.X - winW * 0.1f, win.Bottom - u * 0.003f, winW * 1.2f, u * 0.008f);   // snow on the sill
        }

        // Bulbs along the roof edge, just under the snow.
        const int bulbs = 15;
        for (int i = 0; i <= bulbs; i++)
        {
            var at = new PointF(left - wallW * 0.07f + wallW * 1.14f * i / bulbs, top + u * 0.012f);
            PaintBulb(g, at, u, i % Pastels.Length);
            lights.Add((at, i % Pastels.Length));
        }
        return new CabinFacts(new SizeF(wallW, wallH), rise, windows, door, new PointF(chimX + chimW / 2, chimTop - u * 0.004f), bulbs + 1);
    }

    // ================================================================ the near props

    /// <summary>The pool of warm light the lamp throws on the snow round its foot.</summary>
    private static void PaintLampPool(Graphics g, PointF foot, float r)
    {
        Soft(g, foot.X, foot.Y, r, r * 0.26f, Color.FromArgb(120, 255, 214, 140));
        Soft(g, foot.X, foot.Y, r * 0.45f, r * 0.12f, Color.FromArgb(110, 255, 232, 180));
    }

    /// <summary>
    /// An old iron street lamp standing alone in the snow (the one from the
    /// wardrobe story): a slim dark post with a flared foot, a crossbar, a
    /// glass lantern with a peaked cap, snow on every ledge, and a red bow
    /// tied under the lantern. Returns the middle of the lantern's glass, so
    /// the scene can stamp the live glow there.
    /// </summary>
    private static PointF PaintLampPost(Graphics g, PointF foot, float tall)
    {
        Color iron = Color.FromArgb(26, 30, 44);
        float x = foot.X, y = foot.Y;
        float lw = tall * 0.13f, lh = tall * 0.17f, lanternBottom = y - tall * 0.78f, lanternTop = lanternBottom - lh;
        using (var post = new Pen(iron, Math.Max(1.5f, tall * 0.030f)))
            g.DrawLine(post, x, y, x, lanternBottom);
        using (var ironBrush = new SolidBrush(iron))
        {
            g.FillPolygon(ironBrush, [new PointF(x - tall * 0.05f, y), new PointF(x - tall * 0.018f, y - tall * 0.10f), new PointF(x + tall * 0.018f, y - tall * 0.10f), new PointF(x + tall * 0.05f, y)]);
            g.FillRectangle(ironBrush, x - tall * 0.075f, lanternBottom + tall * 0.06f, tall * 0.15f, Math.Max(1f, tall * 0.014f));   // the crossbar the lamplighter's ladder leans on
            g.FillRectangle(ironBrush, x - lw * 0.62f, lanternBottom - tall * 0.008f, lw * 1.24f, Math.Max(1f, tall * 0.02f));
        }
        // The glass: wider at the top than the bottom, warm.
        PointF[] pane = [new PointF(x - lw * 0.36f, lanternBottom), new PointF(x - lw * 0.5f, lanternTop), new PointF(x + lw * 0.5f, lanternTop), new PointF(x + lw * 0.36f, lanternBottom)];
        using (var glass = new SolidBrush(Color.FromArgb(255, 226, 150)))
            g.FillPolygon(glass, pane);
        using (var flame = new SolidBrush(Color.FromArgb(255, 252, 226)))
            g.FillEllipse(flame, x - lw * 0.16f, lanternBottom - lh * 0.68f, lw * 0.32f, lh * 0.5f);
        using (var frame = new Pen(iron, Math.Max(1f, tall * 0.010f)))
        {
            g.DrawPolygon(frame, pane);
            g.DrawLine(frame, x, lanternBottom, x, lanternTop);
        }
        using (var ironBrush = new SolidBrush(iron))
            g.FillPolygon(ironBrush, [new PointF(x - lw * 0.68f, lanternTop), new PointF(x, lanternTop - lh * 0.5f), new PointF(x + lw * 0.68f, lanternTop)]);   // the cap
        using (var snow = new SolidBrush(Snow))
        {
            g.FillPolygon(snow, [new PointF(x - lw * 0.62f, lanternTop - lh * 0.05f), new PointF(x, lanternTop - lh * 0.62f), new PointF(x + lw * 0.62f, lanternTop - lh * 0.05f), new PointF(x, lanternTop - lh * 0.30f)]);
            g.FillEllipse(snow, x - tall * 0.075f, lanternBottom + tall * 0.045f, tall * 0.15f, tall * 0.022f);   // on the crossbar
            g.FillEllipse(snow, x - tall * 0.06f, y - tall * 0.012f, tall * 0.12f, tall * 0.03f);                 // heaped round the foot
        }
        using (var bow = new SolidBrush(Color.FromArgb(214, 44, 50)))
        {
            float by = lanternBottom + tall * 0.105f, br = tall * 0.028f;   // BELOW the crossbar: tied above it, a red cardinal perched on the bar sat on red ribbon and vanished
            g.FillPolygon(bow, [new PointF(x, by), new PointF(x - br * 1.6f, by - br), new PointF(x - br * 1.6f, by + br)]);
            g.FillPolygon(bow, [new PointF(x, by), new PointF(x + br * 1.6f, by - br), new PointF(x + br * 1.6f, by + br)]);
            g.FillPolygon(bow, [new PointF(x, by), new PointF(x - br * 0.9f, by + br * 2.6f), new PointF(x - br * 0.2f, by + br * 2.4f)]);
            g.FillPolygon(bow, [new PointF(x, by), new PointF(x + br * 0.9f, by + br * 2.6f), new PointF(x + br * 0.2f, by + br * 2.4f)]);
        }
        return new PointF(x, lanternBottom - lh * 0.5f);
    }

    /// <summary>
    /// A bird table: a wooden post, a flat tray with a low rim and a heap of
    /// seed, and a little pitched roof on two corner posts with snow on it.
    /// Returns where birds can stand: the tray's top edge and the roof's ridge.
    /// </summary>
    private static ((PointF At, float Half) Tray, (PointF At, float Half) Roof) PaintFeeder(Graphics g, PointF foot, float tall)
    {
        Color wood = Color.FromArgb(124, 86, 58), woodDark = Color.FromArgb(84, 56, 40);
        float x = foot.X, trayY = foot.Y - tall * 0.66f, half = tall * 0.26f, roofY = trayY - tall * 0.24f;
        using (var post = new Pen(woodDark, Math.Max(1.5f, tall * 0.05f)))
            g.DrawLine(post, x, foot.Y, x, trayY);
        using (var snow = new SolidBrush(Snow))
            g.FillEllipse(snow, x - tall * 0.09f, foot.Y - tall * 0.02f, tall * 0.18f, tall * 0.045f);
        using (var thin = new Pen(woodDark, Math.Max(1f, tall * 0.022f)))
        {
            g.DrawLine(thin, x - half * 0.82f, trayY, x - half * 0.82f, roofY);
            g.DrawLine(thin, x + half * 0.82f, trayY, x + half * 0.82f, roofY);
        }
        using (var seed = new SolidBrush(Color.FromArgb(222, 190, 120)))
            g.FillEllipse(seed, x - half * 0.5f, trayY - tall * 0.035f, half, tall * 0.06f);
        using (var tray = new SolidBrush(wood))
            g.FillRectangle(tray, x - half, trayY, half * 2, Math.Max(2f, tall * 0.035f));
        using (var roofWood = new SolidBrush(woodDark))
            g.FillPolygon(roofWood, [new PointF(x - half * 1.18f, roofY + tall * 0.02f), new PointF(x, roofY - tall * 0.13f), new PointF(x + half * 1.18f, roofY + tall * 0.02f)]);
        using (var snow = new SolidBrush(Snow))
            g.FillPolygon(snow, [new PointF(x - half * 1.22f, roofY + tall * 0.005f), new PointF(x - half * 0.2f, roofY - tall * 0.16f), new PointF(x + half * 0.2f, roofY - tall * 0.16f), new PointF(x + half * 1.22f, roofY + tall * 0.005f), new PointF(x, roofY - tall * 0.10f)]);
        return ((new PointF(x, trayY), half), (new PointF(x, roofY - tall * 0.16f), half * 0.2f));
    }

    /// <summary>
    /// A fallen log lying across the near snow: a long bark cylinder seen
    /// from the side, its sawn end facing right with pale growth rings and a
    /// dark hollow in the middle (a mouse door), and a thick lumpy cap of
    /// snow along its top. Returns the hollow's middle and the y of the top
    /// of the snow cap (something walking along the log walks at that height).
    /// </summary>
    private static (PointF Hollow, float TopY) PaintLog(Graphics g, float left, float right, float footY, float r, float u, Random rng)
    {
        float top = footY - r * 2, faceRx = r * 0.42f;
        using (var bark = new LinearGradientBrush(new PointF(0, top - 1), new PointF(0, footY + 1), Color.FromArgb(104, 74, 56), Color.FromArgb(52, 36, 34)))
        using (var path = new GraphicsPath())
        {
            path.AddArc(left, top, r * 1.2f, r * 2, 90, 180);                 // the rounded far end
            path.AddLine(left + r * 0.6f, top, right, top);
            path.AddLine(right, footY, left + r * 0.6f, footY);
            path.CloseFigure();
            g.FillPath(bark, path);
        }
        // Bark: short dark furrows running along the log.
        using (var furrow = new Pen(Color.FromArgb(120, 34, 24, 24), Math.Max(1f, u * 0.0016f)))
            for (int i = 0; i < 26; i++)
            {
                float fx = left + r + (right - left - r * 2) * (float)rng.NextDouble(), fy = top + r * (0.7f + 1.2f * (float)rng.NextDouble());
                g.DrawLine(furrow, fx, fy, fx + r * (0.5f + (float)rng.NextDouble()), fy + r * 0.05f);
            }
        // The sawn face, an ellipse because we see the round end from the side.
        using (var face = new SolidBrush(Color.FromArgb(196, 156, 110)))
            g.FillEllipse(face, right - faceRx, top, faceRx * 2, r * 2);
        using (var ringPen = new Pen(Color.FromArgb(150, 116, 80), Math.Max(1f, u * 0.0014f)))
            g.DrawEllipse(ringPen, right - faceRx * 0.72f, top + r * 0.28f, faceRx * 1.44f, r * 1.44f);
        var hollow = new PointF(right, footY - r * 0.82f);
        using (var dark = new SolidBrush(Color.FromArgb(24, 16, 18)))
            g.FillEllipse(dark, hollow.X - faceRx * 0.50f, hollow.Y - r * 0.56f, faceRx * 1.0f, r * 1.12f);
        // The snow cap: overlapping lumps along the top, a flat-bottomed heap.
        float capTop = top - r * 0.42f;
        using (var snow = new SolidBrush(Snow))
        using (var shade = new SolidBrush(Color.FromArgb(110, SnowShade)))
        {
            for (float sx = left + r * 0.2f; sx < right - r * 0.3f; sx += r * 0.7f)
            {
                float lr = r * (0.75f + 0.35f * (float)rng.NextDouble());
                g.FillEllipse(snow, sx - lr * 0.4f, capTop + r * 0.05f * (float)rng.NextDouble(), lr * 1.7f, r * 0.95f);
            }
            g.FillRectangle(snow, left + r * 0.9f, top - r * 0.05f, right - left - r * 1.7f, r * 0.34f);
            g.FillEllipse(shade, left + r * 0.9f, top + r * 0.20f, right - left - r * 1.7f, r * 0.16f);     // the cap's underside in shadow
            g.FillEllipse(snow, left - r * 0.5f, footY - r * 0.35f, right - left + r * 1.2f, r * 0.6f);     // snow banked along the foot
        }
        return (hollow, capTop + r * 0.08f);
    }

    /// <summary>
    /// A holly bush under snow: a mound of dark glossy leaves (overlapping
    /// green dabs, lighter toward the upper left where the moon is), red
    /// berries in threes, and snow lying on top.
    /// </summary>
    private static void PaintHolly(Graphics g, PointF at, float r, float u, Random rng)
    {
        Color dark = Color.FromArgb(18, 58, 44), lit = Color.FromArgb(58, 124, 86);
        for (int i = 0; i < 90; i++)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2), d = MathF.Sqrt((float)rng.NextDouble());
            float lx = at.X + MathF.Cos(a) * d * r, ly = at.Y - MathF.Abs(MathF.Sin(a)) * d * r * 0.8f;
            float lw = r * (0.14f + 0.10f * (float)rng.NextDouble());
            float light = Math.Clamp(0.45f - MathF.Cos(a) * d * 0.3f + MathF.Abs(MathF.Sin(a)) * d * 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.4f, 0, 1);
            using var leaf = new SolidBrush(Brushwork.Mix(dark, lit, light));
            // A holly leaf is a pointed oval; a small diamond over an ellipse gives the prickly outline at this size.
            g.FillEllipse(leaf, lx - lw, ly - lw * 0.5f, lw * 2, lw);
            g.FillPolygon(leaf, [new PointF(lx - lw * 1.25f, ly), new PointF(lx, ly - lw * 0.7f), new PointF(lx + lw * 1.25f, ly), new PointF(lx, ly + lw * 0.7f)]);
        }
        using (var berry = new SolidBrush(Color.FromArgb(226, 40, 46)))
        using (var glint = new SolidBrush(Color.FromArgb(200, 255, 200, 200)))
            for (int i = 0; i < 9; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI), d = 0.3f + 0.6f * (float)rng.NextDouble();
                float bx = at.X + MathF.Cos(a) * d * r, by = at.Y - MathF.Sin(a) * d * r * 0.7f, br = Math.Max(1.2f, r * 0.055f);
                for (int k = 0; k < 3; k++)
                {
                    float ox = (k - 1) * br * 1.3f, oy = k == 1 ? -br * 0.9f : 0;
                    g.FillEllipse(berry, bx + ox - br, by + oy - br, br * 2, br * 2);
                    g.FillEllipse(glint, bx + ox - br * 0.5f, by + oy - br * 0.6f, br * 0.6f, br * 0.6f);
                }
            }
        using var snow = new SolidBrush(Snow);
        for (int i = 0; i < 7; i++)
        {
            float f = (i + (float)rng.NextDouble()) / 7f * 2 - 1;
            float sw = r * (0.22f + 0.16f * (float)rng.NextDouble());
            g.FillEllipse(snow, at.X + f * r * 0.8f - sw, at.Y - r * 0.8f * MathF.Sqrt(Math.Max(0, 1 - f * f)) - sw * 0.25f, sw * 2, sw * 0.6f);
        }
    }

    /// <summary>
    /// The drifts right at the bottom edge of the screen, nearer to us than
    /// anything else: three low white swells. They are in the depth picture
    /// at the nearest setting, so an animal that wanders to the very front
    /// sinks behind them to its knees. That one overlap does a lot to make
    /// the snowfield read as deep.
    /// </summary>
    private static void PaintFrontDrifts(Graphics g, int w, int h, float u, Random rng)
    {
        using var snow = new LinearGradientBrush(new PointF(0, h - u * 0.07f), new PointF(0, h + 1), Color.FromArgb(240, 246, 255), Color.FromArgb(178, 196, 236));
        (float X, float Rx, float Rise)[] swells = [(0.26f, 0.30f, 0.030f), (0.60f, 0.22f, 0.022f), (0.80f, 0.20f, 0.034f)];
        foreach (var sw in swells)
        {
            float cx = w * (sw.X + 0.04f * ((float)rng.NextDouble() - 0.5f)), rx = u * sw.Rx, rise = u * sw.Rise;
            g.FillEllipse(snow, cx - rx, h - rise, rx * 2, rise * 2.4f);
        }
    }

    // ================================================================ lights

    /// <summary>One unlit bulb: a small dot of its pastel color. The scene adds the glow that makes it shine.</summary>
    private static void PaintBulb(Graphics g, PointF at, float u, int color)
    {
        float r = Math.Max(0.7f, u * 0.0032f);   // the floor keeps a bulb visible, but small, in the tiny preview box
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

            // Bulbs sit about 1.7% of u apart, but never closer than 4 pixels:
            // in the tiny preview box that keeps a tree from turning into one smear of light.
            int n = Math.Max(2, (int)(MathF.Abs(xb - xa) / Math.Max(4f, u * 0.017f)));
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
    private static List<PointF> PaintCornerBranches(Graphics g, int w, int h, float u, Random rng, List<(PointF At, int Color)> lights)
    {
        Color wood = Color.FromArgb(30, 26, 40);
        Brushwork.BranchSeed[] seeds =
        [
            new(new PointF(-w * 0.01f, h * 0.05f), 0.30f, u * 0.44f, u * 0.018f, 3),            // top left, reaching right
            new(new PointF(w * 1.01f, h * 0.03f), MathF.PI - 0.30f, u * 0.42f, u * 0.017f, 3),  // top right, reaching left
        ];
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng, wood, wood, wood, bare: true);
        // Every third spot: a bulb on all of them clumps into one bright smear.
        // In the tiny preview box the twigs are only a pixel or two apart, so there it is every ninth.
        int skip = u < 250 ? 9 : 3;
        for (int i = 0; i < spots.Count; i += skip)
        {
            PointF at = spots[i];
            if (at.X < 0 || at.X >= w || at.Y < 0 || at.Y >= h) continue;
            int color = rng.Next(Pastels.Length);
            PaintBulb(g, at, u, color);
            lights.Add((at, color));
        }
        return spots;
    }
}
