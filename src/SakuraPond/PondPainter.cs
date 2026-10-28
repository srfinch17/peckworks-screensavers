using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;
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
/// A small rock formation out in the water at the lower left, where the
/// fallen log used to lie (2026-10-23, his word: "remove the log entirely and
/// instead give me a very small island, or rock formation"): one small
/// submerged stone, painted and wrapped like the island's, with two or three
/// little dry caps breaking the surface. Each cap is a rock in Pond.Rocks (so
/// every animal's obstacle list has it) and in the blockers; the bed climbs
/// the stone to each cap's rim as it does the island's.
/// </summary>
internal readonly record struct Formation(Island Stone, PointF[] Caps, float[] CapRadii);

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

    /// <summary>The finished backdrop WITHOUT the branches, one 0x00RRGGBB number per pixel (the branches are a live layer: see Branches).</summary>
    public required uint[] Pixels { get; init; }

    /// <summary>The cherry branches and their blossom on a clear sheet (0xAARRGGBB): the scene lays them over the frame each frame, swaying in the wind (Core's Boughs).</summary>
    public required uint[] Branches { get; init; }

    /// <summary>Where the two trees' trunks are (off the picture): a bough sways more the further it is from its tree's root.</summary>
    public required PointF[] TreeRoots { get; init; }

    /// <summary>Open water: inside the pond and not under a rock, a lily pad or a bank plant. Koi show only here.</summary>
    public required bool[] Water { get; init; }

    /// <summary>Not under the overhanging branches. Everything below the branches (water, koi, dragonflies) shows only here.</summary>
    public required bool[] Open { get; init; }

    /// <summary>The pixels of the petals painted into the backdrop, drifted against the bank (DriftedPetals), that are still
    /// water in the finished picture. They float on the surface, so each frame, once everything UNDER the water is painted,
    /// the scene copies these pixels back from the backdrop (CoverWithHeap): a tadpole or a koi passing under the heap is
    /// hidden by it, the way the live floating petals paint over the fish. Before this, a froglet in the shallows sat
    /// crisp on top of the heap, which read as a tadpole swimming on the petals.</summary>
    public required int[] Heap { get; init; }

    /// <summary>Paints the drifted petal heap back over whatever was drawn under the water this frame (see Heap).</summary>
    public void CoverWithHeap(FrameBuffer fb)
    {
        uint[] px = fb.Pixels, back = Pixels;
        foreach (int i in Heap) px[i] = back[i];
    }

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

    /// <summary>
    /// How deep the water is where Floor is 255, in u: what Z = 1 means for
    /// everything that swims. Deeper than the depth a thing can be seen at
    /// (Light.SeeU), so the middle of the pond really hides a fish that dives
    /// to the bed, the way its painted bed is hidden; near the bank the floor
    /// is shallow and a fish there is always in sight.
    /// </summary>
    public const float DepthU = 0.6f;    // (0.54 until 2026-10-28: the open pond's typical bed is now 0.88 of this, 0.53u as before, with humps to the full 0.6u and hollows to 0.46u, "the depths of the water are not the same everywhere"; the constants that were in depths, the stones' shoals, the deep bed's floor and the minnows' dip floor, were re-expressed so they stay where they were in u; 0.48 until 2026-10-27 afternoon, "i need a tiny bit more depth": Light.SeeU stayed, so the bed is a touch murkier and Body.Deepen darkens what is on it; 0.4 until 2026-10-27 morning, "make the pond a little deeper": a fish on the bed is drawn a fifth further down the screen and a touch smaller; Light.SeeU rose with it so it keeps the haze it had)

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

    /// <summary>
    /// Screen pixels where a stone's DRAWN surface (the island's and the formation's, sunk by
    /// depth as the fish are, WrapIsland) is within a quarter of the pond's depth of the top:
    /// the caps and the collars round them. A swimmer deeper than that is behind the stone
    /// there, so it is drawn through a stencil that leaves these out (the scene's deep water
    /// stencil) and the stone hides it, as a rock hides a fish behind it. Without this a deep
    /// forager whose sunk picture landed on the collar was painted across it, 17 to 26 s per
    /// five minutes of koi noses on the first runs: the floating cap in another costume.
    /// </summary>
    public required bool[] StoneTop { get; init; }

    /// <summary>
    /// A stone's shoulder with under 0.45 of the pond's depth of water over it (the island's hill and the
    /// formations' hills, marked as Hill raises them): no water for a KOI. A koi could swim anywhere
    /// with any water at all under it, so it foraged, looked and crawled over the pale shoulder right
    /// up to the cap's collar and hung there at the surface, bright, for seconds at a time: a fish
    /// lying on the rock, to the eye ("fish still arent handling the rock well", 2026-10-27; on one
    /// seed 79 standstills of 1.5 s or more at the stones in five minutes, most over a bed under half
    /// depth). Physically the fish fit; the painting says the shoulder is the stone, so the fish keep
    /// to where the flank is clearly under water. The bank's own shallows are NOT in this mask (the
    /// prawns live there and a hungry koi noses down to them); only the stones' hills are.
    /// </summary>
    public required bool[] StoneShoal { get; init; }

    /// <summary>The rocks on the bank, for the lizard to climb and bask on.</summary>
    public required IReadOnlyList<BankRock> Rocks { get; init; }

    /// <summary>The fallen log, if the seed placed one (nearly always).</summary>
    public required Log? Log { get; init; }

    /// <summary>The small rock formation at the lower left (in the log's place when the log is off), if the seed found room for one.</summary>
    public required Formation? Formation { get; init; }

    /// <summary>The second rock formation (upper right, 2026-10-27), painted and walled like the first; null when the first found no place.</summary>
    public required Formation? Formation2 { get; init; }

    /// <summary>The island rock out in the water, if the seed found room for one (nearly always).</summary>
    public required Island? Island { get; init; }

    /// <summary>
    /// The water's edge, alive (Shoreline.cs): it laps a few pixels up the sand and back along the whole bank, is
    /// painted every frame right after the backdrop copy, and answers "is this point under water right now" (WetNow),
    /// "can the water ever reach it" (EverWet: a place to drink or sit is beyond that) and "can it ever be left bare"
    /// (EverDry: a tadpole clutch never settles there). The masks above (Water, Land, Floor) are the painted edge at
    /// rest; the lap moves within Reach of it.
    /// </summary>
    public required Shoreline Shore { get; init; }

    /// <summary>Who holds the island's cap and the log as a perch right now (Core/Minds Claim): a turtle setting out for one takes it, the snake asks before it climbs onto the log.</summary>
    public Claim IslandPerch { get; } = new();
    public Claim LogPerch { get; } = new();

    /// <summary>The pond's middle, and roughly how far it reaches each way (for choosing paths across it).</summary>
    public required PointF Centre { get; init; }
    public required SizeF Reach { get; init; }

    // A coarse grid of "how far is the nearest bank or rock", in pixels, for
    // steering: a koi asks it to know when to turn away from the edge.
    private readonly float[] _room;
    private readonly float[] _roomKoi;                   // the same grid with the stones' shoulders (StoneShoal) as walls: what a KOI steers by (round thirty-nine)
    private readonly int _gw, _gh;
    private readonly float _cell;

    public Pond(float[] room, float[] roomKoi, int gw, int gh, float cell)
    {
        _room = room; _roomKoi = roomKoi; _gw = gw; _gh = gh; _cell = cell;
    }

    /// <summary>
    /// How much open water there is around a point: the distance to the
    /// nearest bank or rock, in pixels (0 on land). Read from the coarse grid
    /// and blended between its four nearest cells, so it changes smoothly as
    /// a fish swims, never in steps.
    /// </summary>
    public float RoomAt(float x, float y) => Sample(_room, x, y);

    /// <summary>
    /// The room a KOI has (round thirty-nine): the same grid with every stone's shoulder (StoneShoal, where a koi may
    /// not swim) as a wall, so the room falls off toward a stone the way it falls off toward the bank. By the shared
    /// grid a stone's shoulder was no wall until a fish stood on it (a cliff, not a slope): the feelers' "uphill on
    /// clearance" fallback pointed INTO a stone while the refused step's probe pointed out, the two balanced, and a
    /// goldfish stood at a shoulder's edge for 115 s with its turn rate flipping sign every frame. The minnows, the
    /// turtle and the snake swim or walk over the shoulders, so they keep the shared grid.
    /// </summary>
    public float KoiRoomAt(float x, float y) => Sample(_roomKoi, x, y);

    private float Sample(float[] grid, float x, float y)
    {
        float gx = Math.Clamp(x / _cell, 0, _gw - 1.001f), gy = Math.Clamp(y / _cell, 0, _gh - 1.001f);
        int ix = (int)gx, iy = (int)gy;
        float fx = gx - ix, fy = gy - iy;
        float a = grid[iy * _gw + ix], b = grid[iy * _gw + ix + 1];
        float c = grid[(iy + 1) * _gw + ix], d = grid[(iy + 1) * _gw + ix + 1];
        return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
    }

    /// <summary>Which way is "more open water" from here: the uphill direction of RoomAt (not normalised).</summary>
    public PointF OpenWay(float x, float y)
    {
        float s = _cell;
        return new PointF(RoomAt(x + s, y) - RoomAt(x - s, y), RoomAt(x, y + s) - RoomAt(x, y - s));
    }

    /// <summary>The koi's "more open water": the uphill direction of KoiRoomAt, the stones' shoulders being walls to it.</summary>
    public PointF KoiOpenWay(float x, float y)
    {
        float s = _cell;
        return new PointF(KoiRoomAt(x + s, y) - KoiRoomAt(x - s, y), KoiRoomAt(x, y + s) - KoiRoomAt(x, y - s));
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

    /// <param name="fallenLog">A fallen log at the lower left (the "Fallen log" setting); without it a small rock formation stands there.</param>
    public static Pond Paint(int w, int h, Random rng, bool fallenLog = false)
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

        uint[] beforeHeap = Read(bmp);
        DriftedPetals(g, inPond, dist, w, h, u, rng);                    // before the rocks, which sit on top of them
        bool[] heapTouched = Brushwork.Unchanged(beforeHeap, Read(bmp)); // false where a drifted petal was painted (its soft rim too)

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
        Log? log = fallenLog ? ShorePainter.PlaceLog(bank, corner, leftCorner, inPond, rocks, cattailsSpotHint: null, w, h, u, rng) : null;
        if (log is Log beached && !beached.Afloat) ShorePainter.Log(g, gb, gn, beached, u, rng);
        int[] landDist = DistanceToEdge(inPond.Select(p => !p).ToArray(), w, h, edgeIsBank: false);   // land side: how far from the water
        WrapWater(bmp, beforeRocks, Read(blockers), inPond, dist, landDist, rocks, w, h, u);
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
        bool[] stoneTop = new bool[w * h];                   // the stones' drawn surface near the top (Pond.StoneTop), filled by the wraps
        if (island is Island isle)
        {
            uint[] beforeIsland = Read(bmp);
            bool[] stone = ShorePainter.IslandStone(g, isle, w, h, u, rng);
            bool[] cap = ShorePainter.IslandCap(g, gb, isle, w, h, u, rng);
            WrapIsland(bmp, beforeIsland, stone, cap, isle, dist, isle.Radius + u * 0.05f, isle.CapRadius * 0.5f, stoneTop, w, h, u);   // the same hill the bed climbs (below): one shape for the picture and for the swimmers
            rocks.Add(new BankRock(isle.Cap, isle.CapRadius, InWater: true));   // the snake keeps off the cap; the lizard never sees an in-water rock
        }
        // The small rock formation at the lower left, where the log lay (none while the log is on): the same
        // painting as the island's, a stone going under with little dry caps, each cap a rock to every animal.
        Formation? formation = fallenLog ? null : ShorePainter.PlaceFormation(inPond, dist, shade, island, w, h, u, rng);
        // A second formation (2026-10-27, "i like the rocks we have. add one more"): the same thing in the upper right
        // half, nearest the upper right corner, clear of the island and the first. Its dice are its own, seeded from
        // the first formation's place, so every roll after it (the pads, the cattails, the irises) is what it was.
        Formation? formation2 = null;
        if (formation is Formation first)
        {
            var rng2 = new Random(unchecked((int)first.Stone.Centre.X * 31 + (int)first.Stone.Centre.Y * 7 + w));
            formation2 = ShorePainter.PlaceFormation(inPond, dist, shade, island, w, h, u, rng2, new RectangleF(0.5f, 0.08f, 0.45f, 0.42f), new PointF(w, 0), first.Stone);
            PaintFormation(first, rng);
            if (formation2 is Formation second) PaintFormation(second, rng2);
        }
        // One stone with little dry caps, painted the island's way: the stone bare, each cap a rock of its own and a
        // blocker, the water wrapped over the union of the caps. The wrap reads the stone's depth from the nearest
        // cap's rim, so between two caps half a radius apart the stone stays shallow and in sight, one stone with two
        // tops (the first render, which faded each cap on its own, left each in a dark cloud of its own). The hill is
        // the one the bed climbs under the fish (Hill, below), with the same numbers.
        void PaintFormation(Formation form, Random r)
        {
            uint[] beforeForm = Read(bmp);
            bool[] stone = ShorePainter.IslandStone(g, form.Stone, w, h, u, r);
            bool[] caps = new bool[w * h];
            for (int k = 0; k < form.Caps.Length; k++)
            {
                bool[] one = ShorePainter.IslandCap(g, gb, form.Stone with { Cap = form.Caps[k], CapRadius = form.CapRadii[k] }, w, h, u, r);
                for (int i = 0; i < caps.Length; i++) caps[i] |= one[i];
                rocks.Add(new BankRock(form.Caps[k], form.CapRadii[k], InWater: true));
            }
            WrapIsland(bmp, beforeForm, stone, caps, form.Stone with { Cap = form.Caps[0], CapRadius = form.CapRadii[0] }, dist, form.Stone.Radius + u * 0.04f, form.CapRadii[0] * 0.4f, stoneTop, w, h, u);
        }
        PointF cattails = CattailSpot(bank, corner, inPond, dist, w, h, u, rng);
        List<(PointF Root, float Toward)> irises = IrisSpots(gn, Read(noGrassSheet), inPond, bank, corner, cattails, u, w, h, rng);   // after the rocks and the sand: on the lawn behind them

        // ---- 4: on the water ----
        var pads = new List<LilyPad>();
        LilyPads(g, gp, pads, inPond, dist, cattails, island, formation?.Stone, formation2?.Stone, w, h, u, rng);

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
        // ---- the shore, alive (Shoreline): built from the finished, shaded picture; it bakes its rest state into px ----
        uint[] blocked = Read(blockers), padded = Read(padSheet), noGrass = Read(noGrassSheet);
        // Every stone's rim, measured in from its edge (tenths of a pixel), so the lapping water climbs the foot of each
        // stone, cap and the log's wet end as it climbs the sand (the blockers as they stand now: the island's and the
        // formation's caps were painted after WrapWater's own measure of the bank rocks).
        int[] rockIn = DistanceToEdge(blocked.Select(c => (c & 0xFF) >= 128).ToArray(), w, h, edgeIsBank: false);
        var shore = new Shoreline(px, inPond, dist, landDist, blocked, padded, rockIn, bank, w, h, u);
        Write(bmp, px);
        uint[] beforeBranches = px;
        // The branches go onto a clear sheet of their own: the scene lays that sheet over the
        // frame every frame, swaying (Core's Boughs). The backdrop the scene restores is the
        // picture WITHOUT them (beforeBranches), or they would show twice. The finished picture
        // (for the "open" stencil below) is the sheet laid over the backdrop once, here, which
        // is what painting the branches straight onto it would have given.
        using var boughSheet = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        List<PointF> spots;
        using (var gbough = Graphics.FromImage(boughSheet))
        {
            gbough.Clear(Color.Transparent);
            gbough.SmoothingMode = SmoothingMode.AntiAlias;
            spots = Branches(gbough, w, h, u, new Random(seed), 0, 0);
        }
        uint[] boughs = ReadArgb(boughSheet);
        var final = new uint[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            uint c = boughs[i], a = c >> 24, bg = beforeBranches[i];
            if (a == 0) { final[i] = bg; continue; }
            uint keep = 255 - a;
            uint rr = (((bg >> 16) & 0xFF) * keep + ((c >> 16) & 0xFF) * a) / 255, gg = (((bg >> 8) & 0xFF) * keep + ((c >> 8) & 0xFF) * a) / 255, bb = ((bg & 0xFF) * keep + (c & 0xFF) * a) / 255;
            final[i] = rr << 16 | gg << 8 | bb;
        }
        PointF[] treeRoots = [new PointF(-u * 0.04f, -u * 0.01f), new PointF(w + u * 0.05f, h * 0.26f)];   // the two crowns' centres (see Branches)

        bool[] open = Brushwork.Unchanged(beforeBranches, final);
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
        // CREVICES CLOSED (round thirty-six). A gap between two of a stone's caps narrower than about 0.032u is no
        // water to steer in: a minnow that followed a cap's wall into one spun there for a whole run (the room
        // grid's "open way" flips across a gap's midline), and a koi's feelers read it as a sliver of room to aim
        // at. A morphological closing of the walls, by a second distance transform: water within r of a wall is
        // "near"; near water further than r from any water that is NOT near is a crevice, filled in. Run only in a
        // box round each stone (the boxed transform, a few ms; whole-frame it cost a second of the start at 1080p),
        // so the bank's own nooks are as they were. Only the room grid changes: the picture, the bed and the Land
        // mask are untouched (a frog still sits on a cap; a koi's step test reads Land, not this).
        int crevT = (int)(u * 0.016f * 10);                                             // r, in the transform's tenths of a pixel
        var near = new bool[w * h];
        for (int i = 0; i < near.Length; i++) near[i] = swimDist[i] <= crevT;          // walls and the water within r of one
        bool closedAny = false;
        foreach (Island? st in (ReadOnlySpan<Island?>)[island, formation?.Stone, formation2?.Stone])
        {
            if (st is not Island stone) continue;
            int boxR = (int)(stone.Radius + u * 0.1f);
            int bx0 = Math.Max(0, (int)stone.Centre.X - boxR), bx1 = Math.Min(w - 1, (int)stone.Centre.X + boxR);
            int by0 = Math.Max(0, (int)stone.Centre.Y - boxR), by1 = Math.Min(h - 1, (int)stone.Centre.Y + boxR);
            int[] toWide = DistanceToEdge(near, w, h, false, bx0, by0, bx1, by1);     // from a near pixel to the nearest wide water, inside the box
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    int i = y * w + x;
                    if (swim[i] && near[i] && toWide[i] > crevT) { swim[i] = false; closedAny = true; }
                }
        }
        if (closedAny) swimDist = DistanceToEdge(swim, w, h, edgeIsBank: true);
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
        // from full depth a little outside the stone to nothing at a COLLAR round
        // the cap (half a cap radius wide; two fifths on the formation's smaller
        // caps), so a fish crossing it rises up and over and goes round the dry
        // top. The collar is the stone's shoulder just under the surface, where
        // the painting (WrapIsland, the same hill) shows it nearly as bright as
        // the cap: with the hill reaching the cap's own rim, a turtle paddled
        // along that shoulder a hand's depth down, drawn over the stone and
        // under the cap, and the cap read as a lid floating above its boulder
        // ("there's a gap, like the top of the rock is just floating in the
        // water", 2026-10-26: 8 s of it in one five-minute run on seed 5).
        // Now nothing swims between the top and its stone.
        // (The rock formation's stone is a hill the same way, once per cap: the
        // bed under it is the lowest of the hills, so between two caps it stays
        // shallow.)
        var stoneShoal = new bool[w * h];                      // the hills' shoulders with too little water for a koi (StoneShoal)
        if (island is Island hill) Hill(floor, stoneShoal, hill.Cap, hill.Radius + u * 0.05f, hill.CapRadius * 1.5f, w, h);
        if (formation is Formation fm)
            for (int k = 0; k < fm.Caps.Length; k++) Hill(floor, stoneShoal, fm.Caps[k], fm.Stone.Radius + u * 0.04f, fm.CapRadii[k] * 1.4f, w, h);
        if (formation2 is Formation fm2)
            for (int k = 0; k < fm2.Caps.Length; k++) Hill(floor, stoneShoal, fm2.Caps[k], fm2.Stone.Radius + u * 0.04f, fm2.CapRadii[k] * 1.4f, w, h);
        // The KOI'S room grid (round thirty-nine): the steering grid again with the stones' shoulders as walls, so a
        // koi's feelers feel a stone coming the way they feel the bank, a slope of falling room, not a cliff at the
        // shoulder's edge (Pond.KoiRoomAt tells the rest). One more distance transform at the start, the same cost
        // as the first.
        var swimKoi = new bool[w * h];
        for (int i = 0; i < swimKoi.Length; i++) swimKoi[i] = swim[i] && !stoneShoal[i];
        int[] swimDistKoi = DistanceToEdge(swimKoi, w, h, edgeIsBank: true);
        var roomKoi = new float[gw * gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                int x = Math.Min(w - 1, (int)(gx * cell)), y = Math.Min(h - 1, (int)(gy * cell));
                roomKoi[gy * gw + gx] = swimDistKoi[y * w + x] / 10f;
            }

        // The heap: every pixel a drifted petal touched that is still open water in the finished picture (a rock or the
        // log painted over it later is not water, so it is left out; the branch shade darkened it, and the backdrop pixel
        // copied back carries that shade).
        var heap = new List<int>();
        for (int i = 0; i < waterMask.Length; i++) if (!heapTouched[i] && waterMask[i]) heap.Add(i);

        return new Pond(room, roomKoi, gw, gh, cell)
        {
            Width = w, Height = h, U = u,
            Pixels = beforeBranches, Branches = boughs, TreeRoots = treeRoots,
            Water = waterMask, Open = open, Grassy = grassy, Carpet = ReadArgb(carpetSheet), Depth = depth, Floor = floor, Sun = sun, Heap = heap.ToArray(), StoneTop = stoneTop, StoneShoal = stoneShoal,
            BlossomSpots = spots, Pads = pads, CattailSpot = cattails, IrisSpots = irises, Centre = centre, Reach = reach,
            Land = inPond.Select((p, i) => !p || (blocked[i] & 0xFF) >= 128).ToArray(),    // the ground, plus whatever stands in the water (a rock's wet side, the log)
            Rocks = rocks, Log = log, Island = island, Formation = formation, Formation2 = formation2,
            Shore = shore,
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
        // And out in the deep: fewer, smaller, darker stones lying in the silt (a pond's bed under deep
        // water is silt with stones in it). Until 2026-10-26 night the deep bed was silt alone, which is
        // why four asks to "make the water more transparent" had each moved the picture by two grey
        // levels: there was nothing down there to see through the water to. These stones roll their
        // OWN dice, seeded from the screen's size, so the scene's shared dice are not moved (every roll
        // after them would have moved, and the island with it, on every seed).
        var deepRng = new Random(w * 7919 + h * 104729);
        for (int i = 0; i < count / 2; i++)
        {
            PointF p = WaterPoint(inPond, w, h, deepRng, k => dist[k] >= shallow);
            float r = u * (0.002f + 0.007f * (float)Math.Pow(deepRng.NextDouble(), 2));   // (0.004 to 0.02 in the first render: big pale discs, polka dots over the whole deep)
            int k0 = 70 + deepRng.Next(50);
            Color stone = Color.FromArgb(k0, k0 - 4 - deepRng.Next(10), k0 - 18 - deepRng.Next(14));
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
        float full = u * 0.36f * 10f;                       // depth reaches its deepest this far from the bank (dist is in tenths); 0.24u until 2026-10-28 ("the land gradually rises underwater until it sticks out of the water and becomes the shore": a wider, gentler slope, so a fish's back is out of the water for the last few hundredths of u and not the last few pixels)
        // THE BED IS UNEVEN (2026-10-27, "make the depths be uneven, like the pondbed has deeper and shallower
        // spots"): a handful of broad, soft hollows and humps (bumps a quarter to a third of u across) scale the
        // depth by up to a quarter either way, and more the further from the bank, so the shallows keep their
        // slope and the open pond has deeper and shallower places. Everything reads the depth from this one
        // array (the water's colour, the bed's clearness, the floor the fish feel, the sink and the haze), so a
        // hump is a real rise of the bed to every swimmer. The bumps roll their OWN dice, seeded from the
        // screen's size, so nothing else in the scene moved when they came.
        var bumpRng = new Random(w * 7919 + h * 104729 + 7);
        const int bumps = 11;                                 // (7 until 2026-10-28, "make sure that the depths of the water are not the same everywhere": more, and the open pond's depth now varies ABOUT its typical depth instead of being clamped at the full one, see below)
        float[] bx = new float[bumps], by = new float[bumps], br = new float[bumps], bk = new float[bumps];
        for (int k = 0; k < bumps; k++)
        {
            bx[k] = (float)bumpRng.NextDouble() * w; by[k] = (float)bumpRng.NextDouble() * h;
            br[k] = 1 / (u * (0.18f + 0.14f * (float)bumpRng.NextDouble()));                  // kept as the reciprocal: a multiply per pixel, not a divide
            bk[k] = (k % 2 == 0 ? 1 : -1) * (0.6f + 0.4f * (float)bumpRng.NextDouble());   // humps and hollows by turns
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inPond[i]) continue;
                float d = Math.Clamp(dist[i] / full, 0f, 1f);
                d = d * d * (3 - 2 * d);                     // smoothstep: the slope eases in from the edge and flattens in the middle
                float n = 0;
                for (int k = 0; k < bumps; k++)
                {
                    float ex = (x - bx[k]) * br[k], ey = (y - by[k]) * br[k], q = ex * ex + ey * ey;
                    if (q < 4) { float f = 1 - q * 0.25f; n += bk[k] * f * f; }   // a smooth bump, gone two radii out
                }
                // The open pond is 0.88 of the full depth where no bump is, a hump up to the full depth and a hollow down to
                // 0.76: before this the bumps were laid on a bed already at the full depth and clamped there, so half the
                // water sat at exactly 255 and only the hollows showed ("not the same everywhere"). Pond.DepthU rose with it
                // (0.54 to 0.6), so the typical bed is as deep as it was, 0.53u, with real humps and hollows either side.
                d = Math.Clamp(d * (0.88f + 0.12f * Math.Clamp(n, -1, 1) * d), 0, 1);   // (a quarter and tighter bumps on the first run: a forager climbing a hump could not rise as fast as the slope, and jammed)
                depth[i] = (byte)(d * 255);

                // (No dark line at the edge here: the edge is alive, Shoreline paints it every frame.)
                px[i] = WaterOver(px[i], d, BedClear(d), y, h, 1);
            }
        }
    }

    /// <summary>
    /// How much of the bed shows through water of depth d (0 the edge, 1 the
    /// pond's full depth), 0 to 1. One curve for everything that lies on the
    /// bed: the pebbles (PourWater) and a submerged stone (WrapIsland), so a
    /// stone is exactly as visible as the bed beside it would be at its depth.
    /// Two fifths still shows at the bottom. (A twentieth until 2026-10-26,
    /// "make the deep parts a little more visible"; a twelfth that evening,
    /// "make the water a little more transparent so i can kinda see what's
    /// going on down there"; two fifths later that night, "again. make the
    /// water more transparent": the fourth ask in four days. Measured in a
    /// box of deep water, the step from a twentieth to a twelfth had moved
    /// the picture by about two grey levels, because the bed's green-tinted
    /// colour is close to the deep water's own; what he had been seeing
    /// change was the fish (Light.SeeU), never the bed. This step is the
    /// first that shows: the pebbles are a texture in the deep, not a ghost.)
    /// </summary>
    private static float BedClear(float d) => 0.4f + 0.48f * (1 - d);

    /// <summary>
    /// The water laid over one pixel of the bed: the water's own colour at
    /// depth d (clear teal near the edge, dark teal in the middle), the bed
    /// under it green-tinted and showing through by "clear" (BedClear, or less
    /// where a painting fades out), a little of the sky's light on top (more
    /// toward the far side of the pond, see PourWater's skyShare), and the
    /// whole dimmed by "dim" (1 for none: the island's waterline uses it).
    /// </summary>
    private static uint WaterOver(uint bed, float d, float clear, int y, int h, float dim)
    {
        float skyShare = 0.20f * MathF.Pow(Math.Clamp(1 - (float)y / h, 0, 1), 1.4f) + 0.03f;
        float wr = Shallow.R + (Deep.R - Shallow.R) * d, wg = Shallow.G + (Deep.G - Shallow.G) * d, wb = Shallow.B + (Deep.B - Shallow.B) * d;
        float br = ((bed >> 16) & 0xFF) * 0.62f, bg = ((bed >> 8) & 0xFF) * 0.8f, bb = (bed & 0xFF) * 0.74f;
        float r = wr + (br - wr) * clear, g = wg + (bg - wg) * clear, b = wb + (bb - wb) * clear;
        float s = skyShare * (0.5f + 0.5f * d);
        r += (Sky.R - r) * s; g += (Sky.G - g) * s; b += (Sky.B - b) * s;
        r *= dim; g *= dim; b *= dim;
        return (uint)(Math.Clamp((int)r, 0, 255) << 16 | Math.Clamp((int)g, 0, 255) << 8 | Math.Clamp((int)b, 0, 255));
    }

    /// <summary>
    /// Clusters of lily pads in calm water away from the middle, and a few
    /// water lilies on them. Each pad is a round leaf with a slit cut to its
    /// middle (the notch real lily pads have), seen at our slant, so a little
    /// flatter than round. Veins run out from the middle.
    /// </summary>
    private static void LilyPads(Graphics g, Graphics gb, List<LilyPad> pads, bool[] inPond, int[] dist, PointF cattails, Island? island, Island? formation, Island? formation2, int w, int h, float u, Random rng)
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
            if (formation is Island fm && Dist(fm.Centre, new PointF(x, y)) < fm.Radius + u * 0.15f) continue;     // nor the rock formation's stone
            if (formation2 is Island fm2 && Dist(fm2.Centre, new PointF(x, y)) < fm2.Radius + u * 0.15f) continue;  // nor the second's
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
    private static void WrapWater(Bitmap bmp, uint[] before, uint[] blocked, bool[] inPond, int[] waterDist, int[] landDist, List<BankRock> rocks, int w, int h, float u)
    {
        uint[] px = Read(bmp);
        // The waterline runs round the rock's own rim, not straight across it:
        // a rock's top stays dry while its flanks slope into the water. So on
        // the water side the stone is mixed back toward the water in a band
        // just inside its edge ("band" wide), fully at the rim and not at all
        // further in, and darkened there (a wet flank). A rock standing well
        // out in the pond sinks further: a floor on the mix that rises with
        // the water's depth under it.
        bool[] isRock = blocked.Select(c => (c & 0xFF) >= 128).ToArray();
        int[] rockDist = DistanceToEdge(isRock, w, h, edgeIsBank: false);                          // inside a stone: how far in from its rim
        int[] outDist = DistanceToEdge(isRock.Select(p => !p).ToArray(), w, h, edgeIsBank: false);   // outside: how far to the nearest stone
        // The wet flank is THIN (half a percent of u): the first version mixed a band a tenth of a
        // stone wide toward the water at 85 percent, and every stone on the shore had its water
        // side turned to a teal ghost with the bank's line cutting straight across it ("the
        // boundary goes over the top of rocks, doesn't wrap"). A stone stays a stone: its flank
        // just inside the rim goes dark and a little blue, the way wet stone does, and the water
        // just OUTSIDE the rim goes dark too (the surface meeting the flank in shadow), so the
        // water visibly goes round the stone.
        //
        // A stone ON THE BANK (any ground under its disc) never sinks and is never darkened along
        // the bank's curve: until 2026-10-26 the part of such a stone that lay past the bank line
        // was mixed toward the water by the water's depth there (half way, for a big stone), and
        // the part on the sand side was dimmed in a strip along the bank, so the bank's curve
        // showed on the stone's own face as a line with a paler half beyond it ("the water line on
        // top of the rock"; found on a 4x crop of the big stone at seed 3). A stone the bank runs
        // under is a stone standing on the ground with water against its far flank: that flank's
        // wet rim and the dark thread round its foot are the whole of the waterline, and both fade
        // out where the rim meets the sand, so the water goes ROUND the stone and stops at the
        // ground. Only a stone standing out in the shallows (no ground under it) still sinks: it
        // takes up to half the water's colour, darkened, once the water under it is deep enough.
        var onBank = new bool[rocks.Count];
        for (int k = 0; k < rocks.Count; k++)
        {
            BankRock rk = rocks[k];
            for (int s = 0; s < 24 && !onBank[k]; s++)
            {
                float a = s * MathF.Tau / 24, rr = rk.Radius * (s % 2 == 0 ? 0.9f : 0.5f);
                int x = (int)(rk.Centre.X + MathF.Cos(a) * rr), y = (int)(rk.Centre.Y + MathF.Sin(a) * rr);
                if (x >= 0 && y >= 0 && x < w && y < h && !inPond[y * w + x]) onBank[k] = true;
            }
        }
        bool OnBankAt(int x, int y)
        {
            int best = -1; float bd = float.MaxValue;
            for (int k = 0; k < rocks.Count; k++)
            {
                float dx = rocks[k].Centre.X - x, dy = rocks[k].Centre.Y - y, d = dx * dx + dy * dy - rocks[k].Radius * rocks[k].Radius;
                if (d < bd) { bd = d; best = k; }
            }
            return best >= 0 && onBank[best];
        }
        float band = u * 0.005f * 10, meniscus = u * 0.004f * 10, deepFrom = u * 0.02f * 10, deepOver = u * 0.045f * 10, shallow = u * 0.012f * 10;
        for (int i = 0; i < px.Length; i++)
        {
            uint c = px[i];
            int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
            if (!isRock[i])
            {
                // Water beside a stone's foot: a dark thread round the rim, strongest against it, gone where the water is
                // too shallow to shade anything (the thread would otherwise end in a dark knot where the rim meets the sand).
                if (!inPond[i] || outDist[i] >= meniscus) continue;
                float k = 1 - 0.3f * (1 - outDist[i] / meniscus) * Math.Clamp(waterDist[i] / shallow, 0, 1);
                r = (int)(r * k); g = (int)(g * k * 1.02f); b = (int)(b * k * 1.05f);
            }
            else if (inPond[i])
            {
                // The wet flank, fading to nothing as the rim comes to the sand.
                float edge = Math.Clamp(1 - rockDist[i] / band, 0, 1) * 0.8f * Math.Clamp(waterDist[i] / shallow, 0, 1);
                r += (int)((r * 0.5f - r) * edge); g += (int)((g * 0.54f - g) * edge); b += (int)((b * 0.66f - b) * edge);
                if (!OnBankAt(i % w, i / w))
                {
                    uint o = before[i];
                    int or_ = (int)((o >> 16) & 0xFF), og = (int)((o >> 8) & 0xFF), ob = (int)(o & 0xFF);
                    float sunk = Math.Clamp((waterDist[i] - deepFrom) / deepOver, 0, 1) * 0.5f;
                    r += (int)((or_ * 0.85f - r) * sunk); g += (int)((og * 0.88f - g) * sunk); b += (int)((ob * 0.9f - b) * sunk);
                }
            }
            else continue;
            px[i] = (uint)(r << 16 | g << 8 | b);
        }
        Write(bmp, px);
    }

    /// <summary>
    /// Raises the bed into a hill under a stone: full depth at "outer" from the
    /// cap, climbing (smoothly) to nothing at the cap's rim ("inner"), so a fish
    /// crossing rises up and over and goes round the dry top.
    /// </summary>
    /// <summary>Raises one hill into the bed (see the island note in Paint), and marks its shoulder where the water left
    /// over it is under 0.45 of the pond's depth (Pond.StoneShoal: a koi keeps off it).</summary>
    private static void Hill(byte[] floor, bool[] shoal, PointF cap, float outer, float inner, int w, int h)
    {
        int x0 = Math.Max(0, (int)(cap.X - outer)), x1 = Math.Min(w - 1, (int)(cap.X + outer));
        int y0 = Math.Max(0, (int)(cap.Y - outer)), y1 = Math.Min(h - 1, (int)(cap.Y + outer));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cap.X, dy = y + 0.5f - cap.Y;
                float d = (MathF.Sqrt(dx * dx + dy * dy) - inner) / (outer - inner);
                if (d >= 1) continue;
                float f = d <= 0 ? 0 : d * d * (3 - 2 * d);
                int i = y * w + x;
                floor[i] = (byte)Math.Min(floor[i], (int)(f * 255));
                if (f * Pond.DepthU < 0.243f) shoal[i] = true;   // (under 0.243u of water over the shoulder: 0.45 of the old 0.54u depth, the number round thirty-six set, kept in u when the depth changed)
            }
    }

    /// <summary>
    /// Lays the water back over the island. The stone and its cap are painted
    /// whole, as if they stood on dry land; this is what makes them a boulder
    /// standing in deep water with its top dry.
    ///
    /// The CAP keeps a waterline round its own rim, the way every bank rock
    /// has one (WrapWater): a thin band just inside its edge is darkened, wet.
    /// The STONE goes under by its distance out from the cap's rim (down the
    /// flank), and fades out over the last fifth of its radius in from its OWN
    /// painted edge (so the painting ends in water on every side; measured
    /// from the cap alone, the stone's top edge was still showing when the
    /// fade ran out). The stone under the water is painted
    /// by the pond's own recipe for the bed (WaterOver and BedClear): its depth
    /// at each pixel is the hill the bed climbs under the swimmers (Hill: at
    /// the surface on a collar round the cap, full depth where the stone ends),
    /// and it shows through the water exactly as much as the pebbles would at
    /// that depth: green-tinted, darker and bluer going down, two fifths still
    /// showing at the bottom; and every point of it is DRAWN SUNK, down the
    /// screen by its depth as a fish at that depth is (Light.SinkPx), so the
    /// deep flank trails below its place on the ground and the far side of
    /// the boulder hides under the cap. So the cap sits on bright stone, the
    /// stone is one boulder to the bottom, and nothing swims between the two (until
    /// 2026-10-26 the stone had a fade of its own that reached plain water
    /// seven tenths of the way down the flank, and a turtle paddled along the
    /// shoulder a hand's depth down, under the cap and over the stone: "the
    /// top of the rock is just floating in the water"). Over the last fifth of
    /// its radius the stone fades out from its own painted edge, so the painting
    /// ends in water. Along the cap's rim, outside it, a dark line: where the
    /// surface meets the stone. Anything the painting touched outside the stone
    /// (its drop shadow and the cap's, on the water) is put back: a boulder's
    /// shadow falls on the bed far below, out of sight.
    /// </summary>
    /// <param name="dist">Tenths of a pixel from the bank, for the water's own depth here (PourWater's number).</param>
    /// <param name="hillOuter">Pixels from the cap's centre at which the stone's hill reaches full depth: the number handed to Hill for the bed.</param>
    /// <param name="collar">Pixels out from the cap's painted rim over which the stone is still at the surface (Hill's inner radius less the cap's).</param>
    /// <param name="stoneTop">Filled: the screen pixels where the stone's drawn surface is within a quarter of the pond's depth of the top, and the cap (Pond.StoneTop).</param>
    private static void WrapIsland(Bitmap bmp, uint[] before, bool[] stone, bool[] cap, Island isle, int[] dist, float hillOuter, float collar, bool[] stoneTop, int w, int h, float u)
    {
        uint[] px = Read(bmp);                                                          // the stone and its cap, painted whole, each at its place on the ground
        uint[] outPx = (uint[])px.Clone();                                              // what the water leaves of them
        float edge = isle.Radius * 0.2f * 10, band = u * 0.012f * 10, full = u * 0.24f * 10;
        float slope = MathF.Max(1, hillOuter - isle.CapRadius - collar);              // pixels from the collar's edge down to full depth
        float sinkMax = Light.SinkPx(1, u);                                            // how far down the screen the deepest part of the stone is drawn
        // The box: the stone, the drop shadows the rock painter threw down and to the right of it and of the
        // caps (0.05u, put back to water below), and the sunk flank under it. (A box of 1.5 radii left the far
        // end of the formation's shadow on the water as a dark wedge: the first render of the sunk stone.)
        float reach = isle.Radius + u * 0.1f;
        int x0 = Math.Max(0, (int)(isle.Centre.X - reach)), x1 = Math.Min(w - 1, (int)(isle.Centre.X + reach));
        int y0 = Math.Max(0, (int)(isle.Centre.Y - reach)), y1 = Math.Min(h - 1, (int)(isle.Centre.Y + reach + sinkMax) + 2);
        int bw = x1 - x0 + 1;
        // The three distance maps, inside the box only (every edge a boxed pixel measures to is in the box).
        int[] inStone = DistanceToEdge(stone, w, h, false, x0, y0, x1, y1);          // tenths of a pixel in from the stone's own edge
        int[] inCap = DistanceToEdge(cap, w, h, false, x0, y0, x1, y1);              // the same for the cap
        bool[] notCap = new bool[w * h];
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) notCap[y * w + x] = !cap[y * w + x];
        int[] outCap = DistanceToEdge(notCap, w, h, false, x0, y0, x1, y1);          // for pixels outside the cap: how far from its rim
        // How deep the stone is at a ground pixel: the hill the bed climbs under the fish (Hill),
        // read from the cap's PAINTED rim so the collar follows the cap's lumps, and never deeper
        // than the water itself is (near the bank the pond is shallow and so is the stone).
        float DepthAt(int k)
        {
            float hd = Math.Clamp((outCap[k] / 10f - collar) / slope, 0, 1);
            float hill = hd * hd * (3 - 2 * hd);
            float wd = Math.Clamp(dist[k] / full, 0, 1);
            wd = wd * wd * (3 - 2 * wd);
            return MathF.Min(hill, wd);
        }
        // 1. Everything the painting touched in the box goes back to water. The stone is painted
        //    again below, in its sunk place; its drop shadow and the cap's, on the water, are gone
        //    for good: a boulder's shadow falls on the bed far below, out of sight.
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                if (!cap[i]) outPx[i] = before[i];
            }
        // 2. The stone, SUNK. A point of the stone at depth d is drawn down the screen by its depth
        //    (Light.SinkPx), exactly as a fish at that depth is: the collar round the cap stays where
        //    it is, the deep flank is drawn well below its place on the ground, and the far side of
        //    the boulder slides in under the cap and is hidden by it. Each ground pixel fills the
        //    screen rows from its own sunk place to the next ground row's, so the stretch leaves no
        //    gaps, and at any screen pixel the nearest (shallowest) point of the stone wins (zbuf).
        //    (Until 2026-10-26 night the stone was drawn flat, at its place on the ground, while the
        //    fish over it were drawn sunk: a forager at the bed, sunk a fifth of u, was painted across
        //    the stone's upper flank, which stands above it, and the cap looked to float.)
        float[] zbuf = new float[bw * (y1 - y0 + 1)];
        Array.Fill(zbuf, 2f);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                if (!stone[i] || cap[i]) continue;
                float depth = DepthAt(i);
                uint c = px[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                // How much of the stone shows through that much water, from the bed's own curve
                // (BedClear, one curve for the pebbles and the stone) with the stone's faster fade
                // below, and fading out over the last fifth of the stone's radius from its own
                // painted edge so the painting ends in water and not a step.
                float kEdge = 1 - Math.Clamp(inStone[i] / edge, 0, 1);
                // The flank darkens fast going down, red most and blue least (it slopes away from
                // the light, and the water takes the warm colours first), and the murk takes it
                // faster than it takes the flat bed: a bright collar, a dark slope, then water by
                // half the pond's depth, which is where the painted stone ends. (Through the bed's
                // recipe alone a flat grey stone at half depth was lighter than the deep water
                // round it, and the whole mound was a pale green ghost with a cap on it: the first
                // two renders of this change. A boulder rising out of murky water is seen at its
                // top and lost on the way down; the bed's own curve is for a bed lit from above.)
                float dk = Math.Clamp(depth / 0.55f, 0, 1);
                dk = dk * dk * (3 - 2 * dk);
                float clear = BedClear(depth) * (1 - dk) * (1 - dk) * (1 - kEdge * kEdge);
                float line = Math.Clamp(1 - outCap[i] / band, 0, 1) * 0.2f;         // the dark line where the surface meets the stone
                // The stone's colour under the water: green-tinted as the bed is (WaterOver's tint),
                // darkened for its depth, and the surface's dark line at the cap's rim. It is mixed
                // toward the water AROUND it (the pixel the water wrap painted there, dark teal out
                // in the deep), never toward the pond's colour for water of the mound's own depth:
                // that colour is the pale green of the shallows, and a mound of pale shallow-water
                // colour in the dark deep was the ghost the first renders showed (the water over a
                // boulder is shallow, but the eye reads the pond's colour as the water's, and the
                // stone's own fade below is what says "deep" instead).
                float sr = r * (1 - 0.8f * dk) * 0.62f * (1 - line), sg = g * (1 - 0.72f * dk) * 0.8f * (1 - line), sb = b * (1 - 0.62f * dk) * 0.74f * (1 - line);
                float sink = depth * sinkMax;
                float sinkBelow = y < y1 && stone[i + w] && !cap[i + w] ? DepthAt(i + w) * sinkMax : sink;
                int sy0 = y + (int)sink, sy1 = Math.Max(sy0, y + (int)sinkBelow);
                for (int sy = sy0; sy <= sy1 && sy <= y1; sy++)
                {
                    int j = sy * w + x;
                    if (cap[j]) continue;                                               // the dry top is nearest the eye of all: nothing is drawn over it
                    int k = (sy - y0) * bw + (x - x0);
                    if (depth >= zbuf[k]) continue;
                    zbuf[k] = depth;
                    uint o = before[j];
                    float wr = (o >> 16) & 0xFF, wg = (o >> 8) & 0xFF, wb = o & 0xFF;
                    int rr = (int)(wr + (sr - wr) * clear), gg = (int)(wg + (sg - wg) * clear), bb = (int)(wb + (sb - wb) * clear);
                    outPx[j] = (uint)(Math.Clamp(rr, 0, 255) << 16 | Math.Clamp(gg, 0, 255) << 8 | Math.Clamp(bb, 0, 255));
                }
            }
        // The stone's drawn surface near the top, for the swimmers' deep stencil (Pond.StoneTop).
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (cap[y * w + x] || zbuf[(y - y0) * bw + (x - x0)] < 0.25f) stoneTop[y * w + x] = true;
        // 3. The dry top, in place and over everything: untouched but for the wet band just inside
        //    its rim, which is the bank rocks' own wet flank (WrapWater): mixed toward the water
        //    darkened by a third, fully at the rim. Until 2026-10-23 the cap was only dimmed a little
        //    there, so its rim stayed a crisp dry edge against the stone going under, and the cap read
        //    as a rock pasted on top of a smudge ("tacked on later").
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                if (!cap[i]) continue;
                float wet = Math.Clamp(1 - inCap[i] / band, 0, 1) * 0.85f;
                if (wet <= 0) continue;
                uint c = px[i], o = before[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                int or_ = (int)((o >> 16) & 0xFF), og = (int)((o >> 8) & 0xFF), ob = (int)(o & 0xFF);
                r += (int)((or_ * 0.6f - r) * wet); g += (int)((og * 0.62f - g) * wet); b += (int)((ob * 0.66f - b) * wet);
                outPx[i] = (uint)(r << 16 | g << 8 | b);
            }
        Write(bmp, outPx);
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
    private static int[] DistanceToEdge(bool[] inside, int w, int h, bool edgeIsBank) => DistanceToEdge(inside, w, h, edgeIsBank, 0, 0, w - 1, h - 1);

    /// <summary>
    /// The same, run only inside a box (the array is still the whole frame,
    /// so the answers index as before; outside the box they are 0). Right
    /// whenever every edge a boxed pixel is nearest to lies inside the box
    /// (a stone and its cap in a box a little bigger than the stone), and
    /// three whole-frame sweeps per stone, three stones, cost a second and a
    /// half of the start (2026-10-27).
    /// </summary>
    private static int[] DistanceToEdge(bool[] inside, int w, int h, bool edgeIsBank, int bx0, int by0, int bx1, int by1)
    {
        const int Far = int.MaxValue / 4;
        var d = new int[w * h];
        for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++) { int i = y * w + x; d[i] = inside[i] ? Far : 0; }
        if (edgeIsBank)
        {
            // A wall just outside the screen: the border pixels are one step from it.
            for (int x = bx0; x <= bx1; x++) { if (by0 == 0) d[x] = Math.Min(d[x], 10); if (by1 == h - 1) d[(h - 1) * w + x] = Math.Min(d[(h - 1) * w + x], 10); }
            for (int y = by0; y <= by1; y++) { if (bx0 == 0) d[y * w] = Math.Min(d[y * w], 10); if (bx1 == w - 1) d[y * w + w - 1] = Math.Min(d[y * w + w - 1], 10); }
        }
        for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++)
            {
                int i = y * w + x;
                if (d[i] == 0) continue;
                int best = d[i];
                if (x > bx0) best = Math.Min(best, d[i - 1] + 10);
                if (y > by0)
                {
                    best = Math.Min(best, d[i - w] + 10);
                    if (x > bx0) best = Math.Min(best, d[i - w - 1] + 14);
                    if (x < bx1) best = Math.Min(best, d[i - w + 1] + 14);
                }
                d[i] = best;
            }
        for (int y = by1; y >= by0; y--)
            for (int x = bx1; x >= bx0; x--)
            {
                int i = y * w + x;
                if (d[i] == 0) continue;
                int best = d[i];
                if (x < bx1) best = Math.Min(best, d[i + 1] + 10);
                if (y < by1)
                {
                    best = Math.Min(best, d[i + w] + 10);
                    if (x < bx1) best = Math.Min(best, d[i + w + 1] + 14);
                    if (x > bx0) best = Math.Min(best, d[i + w - 1] + 14);
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
