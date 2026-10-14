using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>A lily pad on the water: where it is and how big (its radius across, in pixels).</summary>
internal readonly record struct LilyPad(PointF Center, float Radius);

/// <summary>A rock: where, how big (radius, pixels), and whether it stands out in the water.</summary>
internal readonly record struct BankRock(PointF Centre, float Radius, bool InWater);

/// <summary>
/// The fallen log at the lower left: its middle, the way it lies (radians), its length and
/// thickness in pixels, and whether it floats (the land is in the other corner) or lies
/// beached across the water's edge.
/// </summary>
internal readonly record struct Log(PointF Centre, float Angle, float Length, float Width, bool Afloat)
{
    /// <summary>The point a fraction t (0 = one end, 1 = the other) along the log's middle line.</summary>
    public PointF At(float t) => new(Centre.X + MathF.Cos(Angle) * Length * (t - 0.5f), Centre.Y + MathF.Sin(Angle) * Length * (t - 0.5f));
}

/// <summary>
/// The island rock: one big stone out in the open water, most of it under
/// the surface, with a small dry CAP breaking the surface. Centre and Radius
/// are the whole stone as painted (pixels); Cap and CapRadius the dry top.
/// Fish swim over its shoulders, rising as the bed comes up to meet the cap
/// (Pond.Floor), and the cap alone is a wall. A turtle hauls out onto the cap.
/// </summary>
internal readonly record struct Island(PointF Centre, float Radius, PointF Cap, float CapRadius);

/// <summary>
/// Everything the moving things need to know about the painted pond. The
/// painter works it out once; the koi, the petals, the dragonflies and the
/// birds only ever READ it.
/// </summary>
internal sealed class Pond
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>The size unit: the height on a wide screen, less on a tall one (see the repo rules).</summary>
    public required float U { get; init; }

    /// <summary>The finished backdrop, one 0x00RRGGBB number per pixel.</summary>
    public required uint[] Pixels { get; init; }

    /// <summary>Open water: inside the pond and not under a rock, a lily pad or a bank plant. Koi show only here.</summary>
    public required bool[] Water { get; init; }

    /// <summary>Not under the overhanging branches. Everything below the branches (water, koi, dragonflies) shows only here.</summary>
    public required bool[] Open { get; init; }

    /// <summary>The lawn: land that is neither bare sand, a stone nor a rock. Grass tufts root only here.</summary>
    public required bool[] Grassy { get; init; }

    /// <summary>The lawn's blades, on a clear sheet (0xAARRGGBB), laid over the ground by Grass.cs every frame.</summary>
    public required uint[] Carpet { get; init; }

    /// <summary>How deep the water is, 0 (the very edge) to 255 (the middle of the pond). 0 off the water. This is what the water's COLOUR was painted from.</summary>
    public required byte[] Depth { get; init; }

    /// <summary>
    /// How deep a fish may be at each water pixel, 0 (the bed is at the
    /// surface) to 255 (the pond's full depth, DepthU). It is the water's
    /// depth with the rocks taken into account: a rock standing in the water
    /// has a flank sloping out under the surface, so the bed rises to meet it
    /// over a short slope (0.08u), the way it rises to the bank. A fish's depth
    /// is capped by this under every point of its spine and both edges of its
    /// body (Koi.LaySpine), so no fish is inside the ground or a stone; the
    /// water stencil hides the fins' soft edge where it still crosses a rim.
    /// A lily pad is not in it: fish swim under pads. The floating log is not
    /// in it either: it is a wall to the koi's steering (Pond.Land), so no fish
    /// swims under it, and a deep fish's sunk picture slipping under its edge
    /// is what a log floating over a fish looks like.
    /// </summary>
    public required byte[] Floor { get; init; }

    /// <summary>How deep the water is where Floor is 255, in u: what "as deep as a fish goes" means on the screen.</summary>
    public const float DepthU = 0.12f;

    /// <summary>How much sun reaches each pixel, 0 to 255 (the branches' shadows are darker).</summary>
    public required byte[] Sun { get; init; }

    /// <summary>Spots in the blossom where petals let go.</summary>
    public required IReadOnlyList<PointF> BlossomSpots { get; init; }

    /// <summary>Where the cattails stand, in the shallows near the bank (the scene plants a Clumps there).</summary>
    public required PointF CattailSpot { get; init; }

    /// <summary>Where each iris clump roots, on the lawn just behind the rocks, and which way its leaves arch (radians, toward the water).</summary>
    public required IReadOnlyList<(PointF Root, float Toward)> IrisSpots { get; init; }

    /// <summary>The lily pads, for dragonflies to land on.</summary>
    public required IReadOnlyList<LilyPad> Pads { get; init; }

    /// <summary>The land: the corner of ground, everything on it included (grass, sand, rocks, the log). The lizard runs only here.</summary>
    public required bool[] Land { get; init; }

    /// <summary>The rocks on the bank, for the lizard to climb and bask on.</summary>
    public required IReadOnlyList<BankRock> Rocks { get; init; }

    /// <summary>The fallen log, if the seed placed one (nearly always).</summary>
    public required Log? Log { get; init; }

    /// <summary>The island rock out in the water, if the seed found room for one (nearly always).</summary>
    public required Island? Island { get; init; }

    /// <summary>Who holds the island's cap and the log as a perch right now (Core/Minds Claim): a turtle setting out for one takes it, the snake asks before it climbs onto the log.</summary>
    public Claim IslandPerch { get; } = new();
    public Claim LogPerch { get; } = new();

    /// <summary>The pond's middle, and roughly how far it reaches each way (for choosing paths across it).</summary>
    public required PointF Centre { get; init; }
    public required SizeF Reach { get; init; }

    // A coarse grid of "how far is the nearest bank or rock", in pixels, for
    // steering: a koi asks it to know when to turn away from the edge.
    private readonly float[] _room;
    private readonly int _gw, _gh;
    private readonly float _cell;

    public Pond(float[] room, int gw, int gh, float cell)
    {
        _room = room; _gw = gw; _gh = gh; _cell = cell;
    }

    /// <summary>
    /// How much open water there is around a point: the distance to the
    /// nearest bank or rock, in pixels (0 on land). Read from the coarse grid
    /// and blended between its four nearest cells, so it changes smoothly as
    /// a fish swims, never in steps.
    /// </summary>
    public float RoomAt(float x, float y)
    {
        float gx = Math.Clamp(x / _cell, 0, _gw - 1.001f), gy = Math.Clamp(y / _cell, 0, _gh - 1.001f);
        int ix = (int)gx, iy = (int)gy;
        float fx = gx - ix, fy = gy - iy;
        float a = _room[iy * _gw + ix], b = _room[iy * _gw + ix + 1];
        float c = _room[(iy + 1) * _gw + ix], d = _room[(iy + 1) * _gw + ix + 1];
        return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
    }

    /// <summary>Which way is "more open water" from here: the uphill direction of RoomAt (not normalised).</summary>
    public PointF OpenWay(float x, float y)
    {
        float s = _cell;
        return new PointF(RoomAt(x + s, y) - RoomAt(x - s, y), RoomAt(x, y + s) - RoomAt(x, y - s));
    }
}

/// <summary>
/// Paints the koi pond, seen from above and a little to the front, the way
/// you would see it leaning on the rail of a bridge. The pond is bigger than
/// the view: water fills the screen, and the only bank in sight is a corner
/// of mossy ground at the bottom left or bottom right (the seed decides).
///
/// FEYNMAN VERSION: think of how a pond is BUILT, and paint it in that order.
///
///   1. The pond bed: sand and pebbles, everywhere the water will be.
///   2. The water: poured over the bed. Shallow water near the bank barely
///      tints the pebbles; deeper water hides the bed and turns dark teal.
///      How deep each pixel is comes from how far it is from the bank
///      ("distance transform", below), the way a real pond slopes. The far
///      side catches a little of the sky's light, as water seen at a slant
///      does.
///   3. The bank: a corner of moss and grass with a wobbly edge (no gardener
///      digs a perfect curve), gravel along the water, rocks half in the
///      water, and the rocks. The iris clumps and the grass are planted
///      by the scene, on top, so they can move.
///   4. On the water: lily pads near the bank, a few water lilies, and
///      petals that have already drifted into the quiet water by the shore.
///   5. The cherry branches reaching over the water from the corners, and
///      first their SHADOWS on the water (the sun is up and to the left, so
///      the shadows fall down and to the right of the branches).
///
/// Sizes come from u = min(h, w * 9 / 16), so the pond looks the same on a
/// wide screen and a tall one.
/// </summary>
internal static class PondPainter
{
    private static readonly Color Bed = Color.FromArgb(112, 104, 78);
    private static readonly Color Deep = Color.FromArgb(22, 66, 72);
    private static readonly Color Shallow = Color.FromArgb(86, 140, 118);
    private static readonly Color Sky = Color.FromArgb(200, 218, 228);

    public static Pond Paint(int w, int h, Random rng)
    {
        float u = Math.Min(h, w * 9f / 16f);
        PointF centre = new(w * 0.5f, h * 0.5f);
        SizeF reach = new(w * 0.45f, h * 0.45f);
        bool leftCorner = rng.Next(2) == 0;
        PointF corner = new(leftCorner ? 0 : w, h);          // the corner of the screen the land sits in
        PointF[] bank = Bank(corner, leftCorner, u, rng);
        using var land = LandPath(bank, corner, leftCorner, h, u);

        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var blockers = new Bitmap(w, h, PixelFormat.Format32bppRgb);   // white where something stands in the water (rocks, irises)
        using var padSheet = new Bitmap(w, h, PixelFormat.Format32bppRgb);   // white where a lily pad floats: hides a koi, but is no wall
        using var g = Graphics.FromImage(bmp);
        using var gb = Graphics.FromImage(blockers);
        using var gp = Graphics.FromImage(padSheet);
        gp.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        gb.SmoothingMode = SmoothingMode.AntiAlias;
        gb.Clear(Color.Black);

        // Where the water is, and how far each water pixel is from the bank
        // (in tenths of a pixel, see the method). The screen's edges are not
        // banks: the pond carries on out of view.
        bool[] inPond = MaskOf(w, h, land);
        for (int i = 0; i < inPond.Length; i++) inPond[i] = !inPond[i];
        int[] dist = DistanceToEdge(inPond, w, h, edgeIsBank: false);

        // ---- 1: the bed, sand and pebbles (pebbles only where the water will be shallow enough to show them) ----
        g.Clear(Bed);
        Silt(g, inPond, w, h, u, rng);
        Pebbles(g, inPond, dist, w, h, u, rng);

        // ---- 2: the water, pixel by pixel ----
        var depth = new byte[w * h];
        uint[] px = Read(bmp);
        PourWater(px, inPond, dist, depth, w, h, u);
        Write(bmp, px);
        Murk(g, inPond, w, h, u, rng);

        DriftedPetals(g, inPond, dist, w, h, u, rng);                    // before the rocks, which sit on top of them

        // ---- 3: the bank (ShorePainter): the lawn, the bare edge, stones, rocks, irises ----
        using var noGrassSheet = new Bitmap(w, h, PixelFormat.Format32bppRgb);   // white where no grass may root (sand, stones, rocks)
        using var gn = Graphics.FromImage(noGrassSheet);
        gn.Clear(Color.Black);
        gn.SmoothingMode = SmoothingMode.AntiAlias;
        using var carpetSheet = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var gc = Graphics.FromImage(carpetSheet);
        gc.Clear(Color.Transparent);
        gc.SmoothingMode = SmoothingMode.AntiAlias;
        ShorePainter.Lawn(g, gc, land, w, h, u, rng);
        ShorePainter.BareEdge(g, gn, bank, corner, u, rng);
        uint[] beforeRocks = Read(bmp);
        List<BankRock> rocks = ShorePainter.Rocks(g, gb, gn, bank, corner, u, rng);
        // The log at the lower left: beached across the water's edge when the land is there
        // (painted now, so the water wraps its wet end like a rock's), or floating out on the
        // water when the land is in the other corner (painted after the wrap: it floats, it does not sink).
        Log? log = ShorePainter.PlaceLog(bank, corner, leftCorner, inPond, rocks, cattailsSpotHint: null, w, h, u, rng);
        if (log is Log beached && !beached.Afloat) ShorePainter.Log(g, gb, gn, beached, u, rng);
        WrapWater(bmp, beforeRocks, Read(blockers), inPond, dist, DistanceToEdge(inPond.Select(p => !p).ToArray(), w, h, edgeIsBank: false), w, h, u);
        if (log is Log afloat && afloat.Afloat) ShorePainter.Log(g, gb, gn, afloat, u, rng);
        // The island rock, out in the open water: painted after the wrap (it has a wrap of its
        // own: a stone that goes down deep, with only a small cap dry), and the cap alone goes
        // into the blockers, so the koi swim over its shoulders and round its top.
        // The branches' shadow map is made now (its dice are rolled here, before the island's),
        // so the island can keep out from under the blossom: a stone nobody sees is no stone.
        int seed = rng.Next();
        float shadowX = u * 0.035f, shadowY = u * 0.05f;      // sun up and to the left: shadows fall down and right
        byte[] shade = BranchShadow(w, h, u, seed, shadowX, shadowY);
        Island? island = ShorePainter.PlaceIsland(inPond, dist, shade, log, w, h, u, rng);
        if (island is Island isle)
        {
            uint[] beforeIsland = Read(bmp);
            bool[] stone = ShorePainter.IslandStone(g, isle, w, h, u, rng);
            bool[] cap = ShorePainter.IslandCap(g, gb, isle, w, h, u, rng);
            WrapIsland(bmp, beforeIsland, stone, cap, isle, w, h, u);
            rocks.Add(new BankRock(isle.Cap, isle.CapRadius, InWater: true));   // the snake keeps off the cap; the lizard never sees an in-water rock
        }
        PointF cattails = CattailSpot(bank, corner, inPond, dist, w, h, u, rng);
        List<(PointF Root, float Toward)> irises = IrisSpots(gn, Read(noGrassSheet), inPond, bank, corner, cattails, u, w, h, rng);   // after the rocks and the sand: on the lawn behind them

        // ---- 4: on the water ----
        var pads = new List<LilyPad>();
        LilyPads(g, gp, pads, inPond, dist, cattails, island, w, h, u, rng);

        // ---- 5: branch shadows, then the branches ----
        px = Read(bmp);
        var sun = new byte[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            // Up to 40% darker in the deepest shadow.
            int keep = 256 - shade[i] * 102 / 255;
            sun[i] = (byte)Math.Min(255, keep);
            if (shade[i] == 0) continue;
            uint c = px[i];
            px[i] = (uint)(((int)((c >> 16) & 0xFF) * keep >> 8) << 16 | ((int)((c >> 8) & 0xFF) * keep >> 8) << 8 | ((int)(c & 0xFF) * keep >> 8));
        }
        Write(bmp, px);
        uint[] beforeBranches = px;
        List<PointF> spots = Branches(g, w, h, u, new Random(seed), 0, 0);
        uint[] final = Read(bmp);

        bool[] open = Brushwork.Unchanged(beforeBranches, final);
        uint[] blocked = Read(blockers), padded = Read(padSheet), noGrass = Read(noGrassSheet);
        var grassy = new bool[w * h];
        for (int i = 0; i < grassy.Length; i++) grassy[i] = !inPond[i] && (blocked[i] & 0xFF) < 128 && (noGrass[i] & 0xFF) < 128;
        var waterMask = new bool[w * h];
        for (int i = 0; i < waterMask.Length; i++) waterMask[i] = inPond[i] && (blocked[i] & 0xFF) < 128 && (padded[i] & 0xFF) < 128;

        // ---- the steering grid: room to swim, in pixels, every "cell" pixels ----
        float cell = Math.Max(4f, u / 90f);
        int gw = (int)(w / cell) + 2, gh = (int)(h / cell) + 2;
        var swim = new bool[w * h];
        for (int i = 0; i < swim.Length; i++) swim[i] = inPond[i] && (blocked[i] & 0xFF) < 128;    // koi swim under pads, so pads are no wall
        // For STEERING the screen's edge counts as a bank: the pond goes on
        // past it, but a koi that swam out of view for a minute would be a koi
        // nobody sees. They turn before the edge, as they turn before the shore.
        int[] swimDist = DistanceToEdge(swim, w, h, edgeIsBank: true);
        var room = new float[gw * gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                int x = Math.Min(w - 1, (int)(gx * cell)), y = Math.Min(h - 1, (int)(gy * cell));
                room[gy * gw + gx] = swimDist[y * w + x] / 10f;
            }

        // ---- the floor under the fish: the water's depth, with every rock's underwater flank raised into it ----
        // A rock is painted as a disc at the surface, but under the water it
        // widens out like the foot of a hill, so the bed comes up to meet its
        // rim over a short slope ("flank"): from full depth that far out, to
        // nothing at the rim. The same smooth ramp the bank uses, only steeper.
        var floor = (byte[])depth.Clone();
        float flank = u * 0.08f;
        foreach (BankRock rock in rocks)
        {
            int x0 = Math.Max(0, (int)(rock.Centre.X - rock.Radius - flank)), x1 = Math.Min(w - 1, (int)(rock.Centre.X + rock.Radius + flank));
            int y0 = Math.Max(0, (int)(rock.Centre.Y - rock.Radius - flank)), y1 = Math.Min(h - 1, (int)(rock.Centre.Y + rock.Radius + flank));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - rock.Centre.X, dy = y + 0.5f - rock.Centre.Y;
                    float d = (MathF.Sqrt(dx * dx + dy * dy) - rock.Radius) / flank;
                    if (d >= 1) continue;
                    float f = d <= 0 ? 0 : d * d * (3 - 2 * d);
                    int i = y * w + x;
                    floor[i] = (byte)Math.Min(floor[i], (int)(f * 255));
                }
        }
        // The island is a hill, not a disc: the bed climbs its painted shoulders
        // from full depth a little outside the stone to nothing at the cap's rim,
        // so a fish crossing it rises up and over and goes round the dry top.
        if (island is Island hill)
        {
            float outer = hill.Radius + u * 0.05f, inner = hill.CapRadius;
            int x0 = Math.Max(0, (int)(hill.Cap.X - outer)), x1 = Math.Min(w - 1, (int)(hill.Cap.X + outer));
            int y0 = Math.Max(0, (int)(hill.Cap.Y - outer)), y1 = Math.Min(h - 1, (int)(hill.Cap.Y + outer));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - hill.Cap.X, dy = y + 0.5f - hill.Cap.Y;
                    float d = (MathF.Sqrt(dx * dx + dy * dy) - inner) / (outer - inner);
                    if (d >= 1) continue;
                    float f = d <= 0 ? 0 : d * d * (3 - 2 * d);
                    int i = y * w + x;
                    floor[i] = (byte)Math.Min(floor[i], (int)(f * 255));
                }
        }

        return new Pond(room, gw, gh, cell)
        {
            Width = w, Height = h, U = u,
            Pixels = final, Water = waterMask, Open = open, Grassy = grassy, Carpet = ReadArgb(carpetSheet), Depth = depth, Floor = floor, Sun = sun,
            BlossomSpots = spots, Pads = pads, CattailSpot = cattails, IrisSpots = irises, Centre = centre, Reach = reach,
            Land = inPond.Select((p, i) => !p || (blocked[i] & 0xFF) >= 128).ToArray(),    // the ground, plus whatever stands in the water (a rock's wet side, the log)
            Rocks = rocks, Log = log, Island = island,
        };
    }


    /// <summary>
    /// The water's edge: a curve from a point part way up the screen's side
    /// to a point part way along its bottom, bowing out into the water, with
    /// two gentle wobbles of random size and phase so every bank is its own
    /// shape. Both ends run a little past the screen's edge, so the land can
    /// be closed off out of sight. Points go from the side edge to the bottom.
    /// </summary>
    private static PointF[] Bank(PointF corner, bool left, float u, Random rng)
    {
        float up = u * (0.34f + 0.1f * (float)rng.NextDouble());         // how far up the side the land reaches
        float along = u * (0.66f + 0.16f * (float)rng.NextDouble());     // how far along the bottom
        float a2 = (float)(rng.NextDouble() * MathF.Tau), a3 = (float)(rng.NextDouble() * MathF.Tau);
        float sx = left ? 1 : -1;                                        // which way "into the screen" is along the bottom
        var pts = new PointF[32];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = i / (pts.Length - 1f);
            float a = -MathF.PI / 2 - 0.12f + t * (MathF.PI / 2 + 0.24f);   // from straight up (and a bit beyond) round to straight along
            float r = 1 + 0.07f * MathF.Sin(2.3f * t * MathF.PI + a2) + 0.04f * MathF.Sin(4.1f * t * MathF.PI + a3);
            pts[i] = new PointF(corner.X + sx * MathF.Cos(a) * along * r, corner.Y + MathF.Sin(a) * up * r);
        }
        return pts;
    }

    /// <summary>The land as a closed shape: the bank curve, then round through the corner, well off the screen.</summary>
    private static GraphicsPath LandPath(PointF[] bank, PointF corner, bool left, int h, float u)
    {
        var path = new GraphicsPath();
        path.AddCurve(bank, 0.5f);
        float m = u * 0.2f, outX = corner.X + (left ? -m : m);
        PointF first = bank[0], last = bank[^1];
        path.AddLine(last, new PointF(last.X, h + m));
        path.AddLine(new PointF(last.X, h + m), new PointF(outX, h + m));
        path.AddLine(new PointF(outX, h + m), new PointF(outX, first.Y));
        path.CloseFigure();
        return path;
    }

    /// <summary>A random point on the water (or anywhere, if 400 tries find none).</summary>
    private static PointF WaterPoint(bool[] inPond, int w, int h, Random rng, Func<int, bool>? also = null)
    {
        for (int tries = 0; tries < 400; tries++)
        {
            int x = rng.Next(w), y = rng.Next(h), i = y * w + x;
            if (inPond[i] && (also == null || also(i))) return new PointF(x, y);
        }
        return new PointF(rng.Next(w), rng.Next(h));
    }

    /// <summary>
    /// Big soft patches of darker silt and paler sand on the bed. They show
    /// through the shallows near the bank as a gentle unevenness.
    /// </summary>
    private static void Silt(Graphics g, bool[] inPond, int w, int h, float u, Random rng)
    {
        for (int i = 0; i < 60; i++)
        {
            PointF p = WaterPoint(inPond, w, h, rng);
            float r = u * (0.05f + 0.12f * (float)rng.NextDouble());
            Color patch = rng.Next(2) == 0 ? Color.FromArgb(70, 72, 52) : Color.FromArgb(150, 140, 104);
            using var path = new GraphicsPath();
            path.AddEllipse(p.X - r, p.Y - r * 0.7f, r * 2, r * 1.4f);
            using var b = new PathGradientBrush(path) { CenterColor = Color.FromArgb(90, patch), SurroundColors = [Color.FromArgb(0, patch)] };
            g.FillPath(b, path);
        }
    }

    /// <summary>
    /// Sand-coloured stones on the pond bed, flattened a little by the slant
    /// we see them at. Only where the water will be shallow enough to show
    /// them: deep water hides the bed completely, so stones there would be
    /// paint for nothing.
    /// </summary>
    private static void Pebbles(Graphics g, bool[] inPond, int[] dist, int w, int h, float u, Random rng)
    {
        int shallow = (int)(u * 0.26f * 10);
        int count = (int)Math.Min(9000, w * (float)h / (u * 0.012f * u * 0.012f) * 0.35f);
        for (int i = 0; i < count; i++)
        {
            PointF p = WaterPoint(inPond, w, h, rng, k => dist[k] < shallow);
            float r = u * (0.003f + 0.008f * (float)Math.Pow(rng.NextDouble(), 2));
            int k0 = 110 + rng.Next(70);
            Color stone = Color.FromArgb(k0, k0 - 6 - rng.Next(10), k0 - 20 - rng.Next(16));
            using var b = new SolidBrush(Color.FromArgb(200, stone));
            g.FillEllipse(b, p.X - r, p.Y - r * 0.75f, r * 2, r * 1.5f);
        }
    }

    /// <summary>
    /// Deep water is not one flat colour: the bed below it rises and falls,
    /// and the light through it is a little uneven. Big, very faint, soft
    /// patches of slightly lighter and slightly darker water, laid over the
    /// poured water, give the open pond that unevenness.
    /// </summary>
    private static void Murk(Graphics g, bool[] inPond, int w, int h, float u, Random rng)
    {
        for (int i = 0; i < 50; i++)
        {
            PointF p = WaterPoint(inPond, w, h, rng);
            float r = u * (0.08f + 0.18f * (float)rng.NextDouble());
            Color patch = rng.Next(2) == 0 ? Color.FromArgb(14, 50, 58) : Color.FromArgb(70, 118, 118);
            using var path = new GraphicsPath();
            path.AddEllipse(p.X - r, p.Y - r * 0.7f, r * 2, r * 1.4f);
            using var b = new PathGradientBrush(path) { CenterColor = Color.FromArgb(34, patch), SurroundColors = [Color.FromArgb(0, patch)] };
            g.FillPath(b, path);
        }
    }

    /// <summary>
    /// Pours the water in: each pond pixel's colour from how deep it is.
    ///
    /// Shallow water (near the bank) is clear, so the pebbles show, only
    /// tinted. Deeper water hides the bed and turns dark teal. And the
    /// water reflects a little of the sky, more toward the far (top) side,
    /// where we look at it at more of a slant, as with any pond.
    /// </summary>
    private static void PourWater(uint[] px, bool[] inPond, int[] dist, byte[] depth, int w, int h, float u)
    {
        float full = u * 0.24f * 10f;                       // depth reaches its deepest this far from the bank (dist is in tenths)
        for (int y = 0; y < h; y++)
        {
            float skyShare = 0.20f * MathF.Pow(Math.Clamp(1 - (float)y / h, 0, 1), 1.4f) + 0.03f;
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inPond[i]) continue;
                float d = Math.Clamp(dist[i] / full, 0f, 1f);
                d = d * d * (3 - 2 * d);                     // smoothstep: the slope eases in from the edge and flattens in the middle
                depth[i] = (byte)(d * 255);

                // Water's own colour at this depth.
                float wr = Shallow.R + (Deep.R - Shallow.R) * d, wg = Shallow.G + (Deep.G - Shallow.G) * d, wb = Shallow.B + (Deep.B - Shallow.B) * d;
                // The bed, seen through the water: green-tinted, and less of it the deeper we look.
                uint c = px[i];
                float br = ((c >> 16) & 0xFF) * 0.62f, bg = ((c >> 8) & 0xFF) * 0.8f, bb = (c & 0xFF) * 0.74f;
                float clear = 0.85f * MathF.Pow(1 - d, 1.4f);
                float r = wr + (br - wr) * clear, gg = wg + (bg - wg) * clear, b = wb + (bb - wb) * clear;
                // The wet line: a thin darker band right at the edge.
                if (dist[i] < u * 0.004f * 10) { r *= 0.8f; gg *= 0.8f; b *= 0.82f; }
                // A little of the sky's light on top.
                float s = skyShare * (0.5f + 0.5f * d);
                r += (Sky.R - r) * s; gg += (Sky.G - gg) * s; b += (Sky.B - b) * s;
                px[i] = (uint)(((int)r << 16) | ((int)gg << 8) | (int)b);
            }
        }
    }

    /// <summary>
    /// Clusters of lily pads in calm water away from the middle, and a few
    /// water lilies on them. Each pad is a round leaf with a slit cut to its
    /// middle (the notch real lily pads have), seen at our slant, so a little
    /// flatter than round. Veins run out from the middle.
    /// </summary>
    private static void LilyPads(Graphics g, Graphics gb, List<LilyPad> pads, bool[] inPond, int[] dist, PointF cattails, Island? island, int w, int h, float u, Random rng)
    {
        int clusters = 3;
        int tries = 0;
        while (clusters > 0 && tries++ < 400)
        {
            int x = rng.Next(w), y = rng.Next(h);
            int i = y * w + x;
            // Calm water: a fair way from the bank, but not out in the open pond.
            if (!inPond[i] || dist[i] < u * 0.07f * 10 || dist[i] > u * 0.3f * 10) continue;
            if (pads.Any(p => Math.Abs(p.Center.X - x) + Math.Abs(p.Center.Y - y) < u * 0.45f)) continue;
            if (Math.Abs(cattails.X - x) + Math.Abs(cattails.Y - y) < u * 0.22f) continue;        // not among the cattails
            if (island is Island isle && Dist(isle.Centre, new PointF(x, y)) < isle.Radius + u * 0.2f) continue;   // not on or against the island rock
            clusters--;
            int n = 8 + rng.Next(8);
            for (int k = 0; k < n; k++)
            {
                float r = u * (0.024f + 0.026f * (float)rng.NextDouble());
                float px = x + (float)(rng.NextDouble() * 2 - 1) * u * 0.11f, py = y + (float)(rng.NextDouble() * 2 - 1) * u * 0.08f;
                int pi = Math.Clamp((int)py, 0, h - 1) * w + Math.Clamp((int)px, 0, w - 1);
                if (!inPond[pi] || dist[pi] < r * 1.2f * 10) continue;
                if (pads.Any(p => Dist(p.Center, new PointF(px, py)) < (p.Radius + r) * 0.75f)) continue;
                Pad(g, gb, px, py, r, rng);
                pads.Add(new LilyPad(new PointF(px, py), r));
            }
        }
        // Water lilies on a few of the pads.
        foreach (LilyPad p in pads.OrderBy(_ => rng.Next()).Take(Math.Max(1, pads.Count / 4)))
            WaterLily(g, new PointF(p.Center.X + p.Radius * 0.15f, p.Center.Y - p.Radius * 0.2f), p.Radius * 0.75f, rng);
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static void Pad(Graphics g, Graphics gb, float x, float y, float r, Random rng)
    {
        float ry = r * 0.82f;
        float notch = (float)(rng.NextDouble() * 360);
        var rect = new RectangleF(x - r, y - ry, r * 2, ry * 2);
        // A faint shadow on the water just under its lower right edge.
        using (var sb = new SolidBrush(Color.FromArgb(60, 8, 22, 24)))
            g.FillPie(sb, rect.X + r * 0.06f, rect.Y + r * 0.08f, rect.Width, rect.Height, notch + 14, 332);
        Color leaf = Color.FromArgb(58 + rng.Next(28), 108 + rng.Next(34), 50 + rng.Next(18));
        using (var path = new GraphicsPath())
        {
            path.AddPie(rect.X, rect.Y, rect.Width, rect.Height, notch + 14, 332);
            using var body = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(x - r * 0.2f, y - ry * 0.25f),
                CenterColor = Brushwork.Mix(leaf, Color.FromArgb(170, 200, 110), 0.35f),
                SurroundColors = [Brushwork.Mix(leaf, Color.FromArgb(30, 60, 30), 0.3f)],
            };
            g.FillPath(body, path);
            // A thin lighter rim: a pad's edge curls up a little and catches the light.
            using var rim = new Pen(Color.FromArgb(110, 180, 200, 120), Math.Max(1f, r * 0.04f));
            g.DrawPath(rim, path);
        }
        // Veins from the middle.
        using (var vein = new Pen(Color.FromArgb(55, 30, 60, 26), Math.Max(1f, r * 0.025f)))
            for (int k = 0; k < 11; k++)
            {
                float a = (notch + 30 + k * 30) * MathF.PI / 180;
                g.DrawLine(vein, x, y, x + MathF.Cos(a) * r * 0.9f, y + MathF.Sin(a) * ry * 0.9f);
            }
        gb.FillPie(Brushes.White, rect.X, rect.Y, rect.Width, rect.Height, notch + 14, 332);
    }

    /// <summary>A water lily seen from above: two rings of pointed petals, white flushed pink, round a gold heart.</summary>
    private static void WaterLily(Graphics g, PointF c, float r, Random rng)
    {
        float turn = (float)(rng.NextDouble() * MathF.Tau);
        Color tip = rng.Next(2) == 0 ? Color.FromArgb(255, 250, 252) : Color.FromArgb(255, 214, 228);
        for (int ring = 0; ring < 2; ring++)
        {
            int n = ring == 0 ? 10 : 8;
            float len = r * (ring == 0 ? 1f : 0.68f);
            for (int k = 0; k < n; k++)
            {
                float a = turn + k * MathF.Tau / n + ring * 0.2f;
                PointF p0 = c, p1 = new(c.X + MathF.Cos(a - 0.22f) * len * 0.5f, c.Y + MathF.Sin(a - 0.22f) * len * 0.42f),
                    p2 = new(c.X + MathF.Cos(a) * len, c.Y + MathF.Sin(a) * len * 0.82f),
                    p3 = new(c.X + MathF.Cos(a + 0.22f) * len * 0.5f, c.Y + MathF.Sin(a + 0.22f) * len * 0.42f);
                using var path = new GraphicsPath();
                path.AddClosedCurve([p0, p1, p2, p3], 0.4f);
                using var b = new LinearGradientBrush(p0, p2, Color.FromArgb(240, 214, 222), tip);
                g.FillPath(b, path);
                using var edge = new Pen(Color.FromArgb(60, 190, 150, 170), Math.Max(1f, r * 0.02f));
                g.DrawPath(edge, path);
            }
        }
        float cr = r * 0.2f;
        using var heart = new SolidBrush(Color.FromArgb(236, 196, 80));
        g.FillEllipse(heart, c.X - cr, c.Y - cr * 0.82f, cr * 2, cr * 1.64f);
    }

    /// <summary>
    /// Petals that fell earlier and drifted into the quiet water near the
    /// bank: a pink scatter, thicker in a few spots, the way the breeze
    /// gathers them.
    /// </summary>
    private static void DriftedPetals(Graphics g, bool[] inPond, int[] dist, int w, int h, float u, Random rng)
    {
        for (int drift = 0; drift < 6; drift++)
        {
            int cx = 0, cy = 0, tries = 0;
            do { cx = rng.Next(w); cy = rng.Next(h); }
            while (tries++ < 400 && !(inPond[cy * w + cx] && dist[cy * w + cx] < u * 0.035f * 10));
            int n = 30 + rng.Next(60);
            for (int k = 0; k < n; k++)
            {
                float x = cx + (float)(rng.NextDouble() * 2 - 1) * u * 0.05f * (float)rng.NextDouble();
                float y = cy + (float)(rng.NextDouble() * 2 - 1) * u * 0.035f * (float)rng.NextDouble();
                int i = Math.Clamp((int)y, 0, h - 1) * w + Math.Clamp((int)x, 0, w - 1);
                if (!inPond[i]) continue;
                float len = u * (0.005f + 0.003f * (float)rng.NextDouble());
                Color c = Brushwork.Mix(Color.FromArgb(255, 244, 248), Color.FromArgb(250, 182, 204), (float)rng.NextDouble());
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.ScaleTransform(1f, 0.82f);
                g.RotateTransform((float)(rng.NextDouble() * 360));
                using var b = new SolidBrush(Color.FromArgb(220, c));
                g.FillEllipse(b, -len, -len * 0.62f, len * 2, len * 1.24f);
                g.Restore(st);
            }
        }
    }

    /// <summary>
    /// Where the iris clumps root: on the lawn a little way behind the rocks
    /// and the sand, at a few points along the bank, with their leaves to
    /// arch out over the stones toward the water. A clump on the lawn is
    /// nearer to us than the rocks in front of it, so its leaves rightly
    /// show over them. The leaves themselves are planted by the scene (a
    /// Clumps from Core/Sakura/Plants.cs) so they can sway; here the root
    /// patch is kept clear of grass, and a spot that is not lawn (sand, a
    /// rock that reaches far up, water) is passed over. Two to six clumps,
    /// each a clear stride from the others and from the cattails (the repo
    /// rule: things scattered at random need a clearance test, or some pair
    /// overlaps on half of seeds). The bank points are visited in a shuffled
    /// order, so the clumps do not favour one end of the bank.
    /// </summary>
    private static List<(PointF Root, float Toward)> IrisSpots(Graphics gn, uint[] noGrass, bool[] inPond, PointF[] bank, PointF corner, PointF cattails, float u, int w, int h, Random rng)
    {
        var spots = new List<(PointF, float)>();
        float r = u * 0.012f, apart = u * 0.12f, want = 2 + rng.Next(5);
        bool Lawn(float x, float y)
        {
            int ix = (int)x, iy = (int)y;
            return ix >= 0 && iy >= 0 && ix < w && iy < h && !inPond[iy * w + ix] && (noGrass[iy * w + ix] & 0xFF) < 128;
        }
        foreach (PointF p in bank.OrderBy(_ => rng.Next()))
        {
            if (spots.Count >= want) break;
            float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
            float back = u * (0.075f + 0.03f * (float)rng.NextDouble());
            var root = new PointF(p.X + ox / ol * back, p.Y + oy / ol * back);
            if (!Lawn(root.X, root.Y) || !Lawn(root.X - r, root.Y) || !Lawn(root.X + r, root.Y) || !Lawn(root.X, root.Y - r) || !Lawn(root.X, root.Y + r)) continue;
            if (Dist(root, cattails) < u * 0.2f || spots.Any(q => Dist(q.Item1, root) < apart)) continue;
            gn.FillEllipse(Brushes.White, root.X - r, root.Y - r * 0.8f, r * 2, r * 1.6f);
            spots.Add((root, MathF.Atan2(-oy, -ox)));          // toward the water
        }
        return spots;
    }

    /// <summary>
    /// The cherry branches reaching over the pond: a big crown in the top left
    /// corner, a smaller one on the right, and branches from both that reach
    /// out over the water. Called twice with the same seed: once to make the
    /// shadow (offset, and blurred), once for real, so shadow and branch
    /// always match.
    /// </summary>
    private static List<PointF> Branches(Graphics g, int w, int h, float u, Random rng, float dx, float dy)
    {
        GraphicsState st = g.Save();
        g.TranslateTransform(dx, dy);
        var wood = Color.FromArgb(60, 42, 40);
        var light = Color.FromArgb(255, 238, 244);
        var deep = Color.FromArgb(246, 166, 192);
        var seeds = new List<Brushwork.BranchSeed>
        {
            new(new PointF(-u * 0.02f, h * 0.12f), 0.22f + 0.15f * (float)rng.NextDouble(), u * 0.66f, u * 0.034f, 2),
            new(new PointF(w * 0.10f, -u * 0.02f), 0.85f + 0.2f * (float)rng.NextDouble(), u * 0.40f, u * 0.026f, 2),
            new(new PointF(w + u * 0.02f, h * 0.34f), MathF.PI - 0.30f - 0.15f * (float)rng.NextDouble(), u * 0.46f, u * 0.03f, 2),
        };
        // The crowns of the two trees, mostly off the screen: from above, a
        // cherry tree in bloom is a cloud of pink with branches reaching out of it.
        Brushwork.Canopy(g, -u * 0.04f, -u * 0.01f, u * 0.22f, u, rng);
        Brushwork.Canopy(g, w + u * 0.05f, h * 0.26f, u * 0.15f, u, rng);
        List<PointF> spots = Brushwork.Branches(g, seeds, u, rng, wood, light, deep);
        g.Restore(st);
        return spots;
    }

    /// <summary>
    /// The branches' shadow on the ground and water, 0 (none) to 255 (full).
    /// Painted as a white silhouette, offset down and right, on a sheet a
    /// quarter of the size and then enlarged smoothly: the enlarging is the
    /// blur. A shadow from something high up is soft-edged, because the sun
    /// is a disc, not a point.
    /// </summary>
    private static byte[] BranchShadow(int w, int h, float u, int seed, float dx, float dy)
    {
        int sw = Math.Max(1, w / 6), sh = Math.Max(1, h / 6);
        using var small = new Bitmap(sw, sh, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(small))
        {
            g.Clear(Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(sw / (float)w, sh / (float)h);
            Branches(g, w, h, u, new Random(seed), dx, dy);
        }
        using var big = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var edges = new ImageAttributes();
            edges.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(small, new Rectangle(0, 0, w, h), 0, 0, sw, sh, GraphicsUnit.Pixel, edges);
        }
        uint[] px = Read(big);
        var shade = new byte[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            // Any colour at all counts as shadow: the blossoms are pink, the wood brown.
            uint c = px[i];
            int m = Math.Max((int)((c >> 16) & 0xFF), Math.Max((int)((c >> 8) & 0xFF), (int)(c & 0xFF)));
            shade[i] = (byte)Math.Min(255, m * 3 / 2);
        }
        return shade;
    }

    /// <summary>
    /// Lays the water back over whatever stands in it (rocks, iris blades).
    /// A stone in a pond is not pasted on top of the water: the part below
    /// the surface is SEEN THROUGH the water, more faintly the deeper it
    /// goes, until a little way out it is gone. So for every pixel of a rock
    /// that lies on the water side of the bank, the painted stone is mixed
    /// back toward the water that was there before the rock, by an amount
    /// that grows with the water's depth there. Just above the waterline the
    /// stone is damp and a little darker (the splash line).
    /// </summary>
    private static void WrapWater(Bitmap bmp, uint[] before, uint[] blocked, bool[] inPond, int[] waterDist, int[] landDist, int w, int h, float u)
    {
        uint[] px = Read(bmp);
        // The waterline runs round the rock's own rim, not straight across it:
        // a rock's top stays dry while its flanks slope into the water. So on
        // the water side the stone is mixed back toward the water in a band
        // just inside its edge ("band" wide), fully at the rim and not at all
        // further in, and darkened there (a wet flank). A rock standing well
        // out in the pond sinks further: a floor on the mix that rises with
        // the water's depth under it.
        int[] rockDist = DistanceToEdge(blocked.Select(c => (c & 0xFF) >= 128).ToArray(), w, h, edgeIsBank: false);
        float band = u * 0.012f * 10, deepFrom = u * 0.02f * 10, deepOver = u * 0.045f * 10;
        int splash = (int)(u * 0.006f * 10);
        for (int i = 0; i < px.Length; i++)
        {
            if ((blocked[i] & 0xFF) < 128) continue;
            uint c = px[i];
            int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
            if (inPond[i])
            {
                // Under water a stone looks DARKER and bluer, never paler: the
                // rim band mixes toward the water darkened by a third (the wet
                // flank and the dark line where the surface meets it), and a
                // stone out in the shallows takes on some of the water's own
                // colour all over, up to about half: its top still breaks the surface.
                uint o = before[i];
                int or_ = (int)((o >> 16) & 0xFF), og = (int)((o >> 8) & 0xFF), ob = (int)(o & 0xFF);
                float edge = Math.Clamp(1 - rockDist[i] / band, 0, 1) * 0.85f;
                r += (int)((or_ * 0.6f - r) * edge); g += (int)((og * 0.62f - g) * edge); b += (int)((ob * 0.66f - b) * edge);
                float sunk = Math.Clamp((waterDist[i] - deepFrom) / deepOver, 0, 1) * 0.5f * (1 - edge);
                r += (int)((or_ * 0.85f - r) * sunk); g += (int)((og * 0.88f - g) * sunk); b += (int)((ob * 0.9f - b) * sunk);
            }
            else if (landDist[i] < splash)
            {
                float k = 0.82f + 0.18f * landDist[i] / splash;
                r = (int)(r * k); g = (int)(g * k); b = (int)(b * k);
            }
            else continue;
            px[i] = (uint)(r << 16 | g << 8 | b);
        }
        Write(bmp, px);
    }

    /// <summary>
    /// Lays the water back over the island. The stone and its cap are painted
    /// whole, as if they stood on dry land; this is what makes them a boulder
    /// standing in deep water with its top dry.
    ///
    /// The CAP keeps a waterline round its own rim, the way every bank rock
    /// has one (WrapWater): a thin band just inside its edge is darkened, wet.
    /// The STONE is faded by two distances, whichever has it further gone:
    /// out from the cap's rim (down the flank) and in from the stone's OWN
    /// painted edge (so the fade ends exactly where the painting does, on
    /// every side; measured from the cap alone, the stone's top edge was still
    /// showing when the fade ran out). Two things happen to a stone as it goes
    /// down: less light reaches it and less comes back, so it gets DARKER, and
    /// the water takes the warm colours first, so it gets BLUER (red is dimmed
    /// most, blue least); and the water between it and the eye adds its own
    /// colour, more the deeper, until only water is left. It is never made
    /// paler than the water (a light stone mixed with dark water is fog).
    /// Along the cap's rim, outside it, a dark line: where the surface meets
    /// the stone. Anything the painting touched outside the stone (its drop
    /// shadow and the cap's, on the water) is put back: a boulder's shadow
    /// falls on the bed far below, out of sight.
    /// </summary>
    private static void WrapIsland(Bitmap bmp, uint[] before, bool[] stone, bool[] cap, Island isle, int w, int h, float u)
    {
        uint[] px = Read(bmp);
        int[] inStone = DistanceToEdge(stone, w, h, edgeIsBank: false);               // tenths of a pixel in from the stone's own edge
        int[] inCap = DistanceToEdge(cap, w, h, edgeIsBank: false);                   // the same for the cap
        int[] outCap = DistanceToEdge(cap.Select(c => !c).ToArray(), w, h, edgeIsBank: false);   // for pixels outside the cap: how far from its rim
        float fade = isle.Radius * 0.45f * 10, band = u * 0.012f * 10, reach = (isle.Radius - isle.CapRadius) * 0.85f * 10;
        int x0 = Math.Max(0, (int)(isle.Centre.X - isle.Radius * 1.8f)), x1 = Math.Min(w - 1, (int)(isle.Centre.X + isle.Radius * 1.8f));
        int y0 = Math.Max(0, (int)(isle.Centre.Y - isle.Radius * 1.8f)), y1 = Math.Min(h - 1, (int)(isle.Centre.Y + isle.Radius * 1.8f));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                uint c = px[i], o = before[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                if (cap[i])
                {
                    // The dry top: untouched but for the wet band just inside its rim.
                    float wet = Math.Clamp(1 - inCap[i] / band, 0, 1);
                    if (wet <= 0) continue;
                    r = (int)(r * (1 - 0.3f * wet)); g = (int)(g * (1 - 0.26f * wet)); b = (int)(b * (1 - 0.2f * wet));
                    px[i] = (uint)(r << 16 | g << 8 | b);
                    continue;
                }
                if (c == o) continue;
                if (!stone[i]) { px[i] = o; continue; }                                 // a shadow on the water: gone
                float kCap = Math.Clamp(outCap[i] / reach, 0, 1), kEdge = 1 - Math.Clamp(inStone[i] / fade, 0, 1);
                float k = MathF.Max(kCap, kEdge);
                float sink = k * k * (3 - 2 * k);                                       // how far gone into the water: a stone for a while, then water
                float line = Math.Clamp(1 - outCap[i] / band, 0, 1) * 0.3f;             // the dark line where the surface meets the stone
                // The stone under water: red dimmed most, blue least (the water takes the warm colours
                // first), so even at the rim the stone is bluer than its dry self, and far down it is
                // the water's own hue before the mix finishes it.
                r = (int)(r * (0.55f - 0.4f * sink - line)); g = (int)(g * (0.72f - 0.37f * sink - line)); b = (int)(b * (0.85f - 0.3f * sink - line));
                float dk = 0.5f + 0.5f * sink, mix = 0.3f + 0.7f * MathF.Pow(sink, 1.1f);
                // The water it mixes toward: darkened near the top, and a touch greener and bluer
                // there too, but that touch fades with the stone: where the stone is gone the mix
                // goes to the plain water, or a faint blue ring would outline every island.
                float tint = 1 - sink;
                int or_ = (int)((o >> 16) & 0xFF), og = (int)((o >> 8) & 0xFF), ob = (int)(o & 0xFF);
                r += (int)((or_ * dk - r) * mix); g += (int)((og * (dk + 0.04f * tint) - g) * mix); b += (int)((ob * (dk + 0.08f * tint) - b) * mix);
                px[i] = (uint)(Math.Clamp(r, 0, 255) << 16 | Math.Clamp(g, 0, 255) << 8 | Math.Clamp(b, 0, 255));
            }
        Write(bmp, px);
    }

    /// <summary>
    /// Where the cattails stand: a spot in the shallows a little way out from
    /// the middle part of the bank, on water (not under a rock).
    /// </summary>
    private static PointF CattailSpot(PointF[] bank, PointF corner, bool[] inPond, int[] dist, int w, int h, float u, Random rng)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            PointF p = bank[bank.Length / 4 + rng.Next(bank.Length / 2)];
            float ox = corner.X - p.X, oy = corner.Y - p.Y, ol = MathF.Max(1, MathF.Sqrt(ox * ox + oy * oy));
            float out_ = u * (0.05f + 0.04f * (float)rng.NextDouble());
            var c = new PointF(p.X - ox / ol * out_, p.Y - oy / ol * out_);
            int ix = (int)c.X, iy = (int)c.Y;
            if (ix < 0 || iy < 0 || ix >= w || iy >= h || !inPond[iy * w + ix]) continue;
            if (iy > h - u * 0.05f || ix < u * 0.06f || ix > w - u * 0.06f) continue;         // room for the clump on screen
            return c;
        }
        return new PointF(w * 0.5f, h * 0.5f);
    }

    /// <summary>Inside the shape (true) or not, one per pixel.</summary>
    private static bool[] MaskOf(int w, int h, GraphicsPath shape)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.FillPath(Brushes.White, shape);
        }
        return Read(bmp).Select(c => (c & 0xFF) > 127).ToArray();
    }

    /// <summary>
    /// For every pixel inside the mask, roughly how far it is to the nearest
    /// pixel outside it, in TENTHS of a pixel (whole numbers are quicker).
    /// With edgeIsBank false the screen's edges do not count as "outside":
    /// the pond carries on past them, so water at the edge of the view is
    /// as deep as its distance from the bank says. With it true the edges
    /// are walls (for the steering grid).
    ///
    /// The "chamfer" trick: two sweeps over the picture. The first goes from
    /// the top left, and each pixel takes the smallest of "my neighbour above
    /// or to the left, plus the step to get here" (10 for a straight step, 14
    /// for a diagonal, since a diagonal is about 1.4 times as long). The second
    /// sweep goes back from the bottom right and does the same with the other
    /// neighbours. Like asking everyone in a crowd "how far to the exit?",
    /// where each person only asks the people next to them and adds one step.
    /// </summary>
    private static int[] DistanceToEdge(bool[] inside, int w, int h, bool edgeIsBank)
    {
        const int Far = int.MaxValue / 4;
        var d = new int[w * h];
        for (int i = 0; i < d.Length; i++) d[i] = inside[i] ? Far : 0;
        if (edgeIsBank)
        {
            // A wall just outside the screen: the border pixels are one step from it.
            for (int x = 0; x < w; x++) { d[x] = Math.Min(d[x], 10); d[(h - 1) * w + x] = Math.Min(d[(h - 1) * w + x], 10); }
            for (int y = 0; y < h; y++) { d[y * w] = Math.Min(d[y * w], 10); d[y * w + w - 1] = Math.Min(d[y * w + w - 1], 10); }
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (d[i] == 0) continue;
                int best = d[i];
                if (x > 0) best = Math.Min(best, d[i - 1] + 10);
                if (y > 0)
                {
                    best = Math.Min(best, d[i - w] + 10);
                    if (x > 0) best = Math.Min(best, d[i - w - 1] + 14);
                    if (x < w - 1) best = Math.Min(best, d[i - w + 1] + 14);
                }
                d[i] = best;
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                if (d[i] == 0) continue;
                int best = d[i];
                if (x < w - 1) best = Math.Min(best, d[i + 1] + 10);
                if (y < h - 1)
                {
                    best = Math.Min(best, d[i + w] + 10);
                    if (x < w - 1) best = Math.Min(best, d[i + w + 1] + 14);
                    if (x > 0) best = Math.Min(best, d[i + w - 1] + 14);
                }
                d[i] = best;
            }
        return d;
    }

    private static uint[] Read(Bitmap bmp) => Brushwork.ToPixels(bmp);

    /// <summary>Reads a clear-sheet bitmap with its alpha kept: one 0xAARRGGBB per pixel.</summary>
    private static uint[] ReadArgb(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var raw = new int[bmp.Width * bmp.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            var px = new uint[raw.Length];
            Buffer.BlockCopy(raw, 0, px, 0, raw.Length * 4);
            return px;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>Writes a pixel array back into a bitmap (the reverse of Brushwork.ToPixels), so painting can carry on over it.</summary>
    private static void Write(Bitmap bmp, uint[] px)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new int[px.Length];
            Buffer.BlockCopy(px, 0, raw, 0, raw.Length * 4);
            Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
