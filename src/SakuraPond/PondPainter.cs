using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraPond;

/// <summary>
/// A lily pad on the water: where it is now, how big (its radius across, in pixels), and its picture.
///
/// ROUND FORTY ("make the lily pads mostly fixed, but they have to be separate from the water. if a fish swims
/// into one, it will move a little bit ... it's anchored to the ground with a root, so it's basically just like a
/// balloon on a string except in the water"): a pad is no longer painted into the backdrop. Each one is a Sprite
/// stamped on the water every frame at Center, which is pushed a little by any body at the surface that runs into
/// it and eases back to Anchor, the spot its root holds it to (LilyPads.cs). Everything that steers by the pads
/// (the frogs' perches, the snake's and the ducks' obstacle lists) reads Center; the frogs and the dragonflies hold
/// a pad still while they sit on it.
/// </summary>
internal sealed class LilyPad
{
    public PointF Center;          // where it is now, screen pixels
    public PointF Anchor;          // where its root holds it: it drifts about this and comes back to it
    public float Radius;           // across, in pixels
    public Sprite? Picture;        // the pad (its shadow on the water, the leaf, a lily on some), centred in its sheet
    public float Hold;             // seconds for which something sitting on it keeps it still (a frog, a dragonfly)
    public LilyPad(PointF center, float radius) { Center = Anchor = center; Radius = radius; }
}

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
/// How a stone's mound runs out from the boulder, BY DIRECTION (2026-10-30, his line on the sunk bed: "make the
/// underwater landscape look less like boobs. like there is a mount then a small rock protrusion on top ... vary it
/// up"). The one profile (StoneZ) swept round in a circle made every stone a perfect dome with a cap on top. The
/// profile stays one; what varies is how far the mound runs in each direction: longer along one axis (Stretch, a
/// whale-back), longest on one side (Tail: a boulder at one end of the bar of silt the current has banked behind it)
/// and wobbled in three lobes (Wobble), all dealt from the stone's own position, no dice, so the scene's rolls do not
/// move. And since round forty-four a seven-lobed ripple on top (Rubble, a fixed share): a mound's contour that was
/// smooth read as a dome however it was stretched; a broken, buttressed foot reads as a heap. Every reader of the stone (Lower for the bed and the koi's shoal, StoneColour for the flank, SinkBed for the
/// picture) still reads the one terrain this is lowered into.
/// </summary>
internal readonly record struct MoundShape(float Angle, float Stretch, float Tail, float Wobble, float Phase)
{
    /// <summary>The shape dealt from where the stone stands (and the screen width, so a 21:9 screen deals differently).</summary>
    public static MoundShape Of(PointF centre, int w)
    {
        uint hsh = (uint)((int)centre.X * 73856093 ^ (int)centre.Y * 19349663 ^ w * 83492791);
        float Next() { hsh = hsh * 1664525u + 1013904223u; return (hsh >> 8) / 16777216f; }   // a small linear congruential generator: enough for five dials
        return new MoundShape((float)(Next() * Math.Tau), 0.08f + 0.12f * Next(), 0.1f + 0.15f * Next(), 0.1f + 0.1f * Next(), (float)(Next() * Math.Tau));
    }

    /// <summary>
    /// The mound's run in the direction of the offset (dx, dy) of length r from the cap, as a multiple of
    /// PondPainter.MoundU: about 0.7 on the short side to 1.35 on the long (0.5 to 2 until round forty-five, when the
    /// mound became a boulder's skirt and its long silt bars went with the big hills). In cosines of the direction, never an angle:
    /// cos(t - A) is the offset's dot product with the axis over r, cos 2x is 2 cos squared x less 1, and cos 3t and
    /// sin 3t come from the triple-angle formulas, all multiplies (an atan2 per pixel over seven boxes 1.5u wide cost
    /// 600 ms at the start on the first run).
    /// </summary>
    public float Run(float dx, float dy, float r)
    {
        float c = dx / r, s = dy / r;                                                        // cos t, sin t
        float ca = c * CosA + s * SinA;                                                      // cos(t - A)
        float c3 = 4 * c * c * c - 3 * c, s3 = 3 * s - 4 * s * s * s;                        // cos 3t, sin 3t
        float c2 = c * c, c7 = c * (((64 * c2 - 112) * c2 + 56) * c2 - 7), s7 = s * (((64 * c2 - 80) * c2 + 24) * c2 - 1);   // cos 7t, sin 7t (Chebyshev, all multiplies)
        return Math.Clamp(1 + Stretch * (2 * ca * ca - 1) + Tail * ca + Wobble * (c3 * CosP - s3 * SinP) + Rubble * (c7 * CosP + s7 * SinP), 0.5f, 2.2f);
    }
    private const float Rubble = 0.13f;                                                      // the seven-lobed ripple's share of the run
    private float CosA => MathF.Cos(Angle);
    private float SinA => MathF.Sin(Angle);
    private float CosP => MathF.Cos(Phase);
    private float SinP => MathF.Sin(Phase);
}

/// <summary>
/// An old log sunk on the deep bed (2026-10-30, "maybe give me an old submerged log down there. maybe a home for a
/// cray fish or the deep water prawns"): its middle, the way it lies (radians), its length and thickness in pixels
/// (all in the painter's ground pixels), and how high it stands off the bed in Z (HeightZ, 0.8 of its thickness: it
/// has settled into the silt a little). IT IS PART OF THE TERRAIN (PondPainter.LowerLog): the bed the swimmers feel,
/// the painted depth, the hidden-behind test (Pond.Near) and the picture (LogColour, drawn sunk by SinkBed) all read
/// the one map, so the log is solid to everything the bed is solid to, a koi rises over it as over a hump, a deep
/// dweller walks over it, and nothing else needs telling. Its near end (the one whose cut face the eye sees, lower on
/// the screen) is hollow: the Den, the first deep dweller's home.
/// </summary>
internal readonly record struct SunkenLog(PointF Centre, float Angle, float Length, float Width, float HeightZ)
{
    /// <summary>The point a fraction t (0 = one end, 1 = the other) along the log's middle line.</summary>
    public PointF At(float t) => new(Centre.X + MathF.Cos(Angle) * Length * (t - 0.5f), Centre.Y + MathF.Sin(Angle) * Length * (t - 0.5f));
    /// <summary>Half the thickness at a point "along" pixels from the middle: a trunk is not a ruled pipe, it swells and narrows a little (up to 12 percent) along its length. The terrain (LowerLog) and the colour (LogColour) both read this, so the picture and the bed are one shape.</summary>
    public float HalfWidthAt(float along) => Width * 0.5f * (1 + 0.08f * MathF.Sin(along / Width * 1.9f + Angle * 5) + 0.04f * MathF.Sin(along / Width * 4.3f + Angle * 2));
    /// <summary>Which end faces the eye: the lower one on the screen (1 when that is At(1), else 0).</summary>
    public float NearEnd => MathF.Sin(Angle) >= 0 ? 1f : 0f;
    /// <summary>The hollow's mouth, on the bed just outside the near end: where a deep dweller makes its home.</summary>
    public PointF Den
    {
        get
        {
            float s = NearEnd > 0.5f ? 1 : -1;
            PointF e = At(NearEnd);
            return new PointF(e.X + MathF.Cos(Angle) * s * Width * 0.6f, e.Y + MathF.Sin(Angle) * s * Width * 0.6f);
        }
    }
}

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
    public required uint[] Pixels { get; set; }   // (settable since round forty-six: the scene points it at the caustics' lit copy, so every painter that mixes toward "the water" mixes toward the lit water; the pristine picture stays with Caustics)

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

    /// <summary>
    /// How deep the bed is, 0 (the very edge) to 255 (the pond's full depth, DepthU), 0 off the water: the one
    /// terrain (PondPainter.Terrain), read in ground pixels. This is what the water's colour was painted from and
    /// what the bed was DRAWN from (sunk by its depth, SinkBed), and since round forty-one's step 3 it is the very
    /// same array as Floor: the bed a swimmer feels is the bed drawn under it.
    /// </summary>
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
    /// Since round forty-one's step 3 this is Depth itself (one terrain): the
    /// stones' profiles and the rocks' feet are in the painted depth too.
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
    /// How near the eye a stone's DRAWN surface is at each screen pixel, on the sprites' 0 (far, no stone) to 255
    /// (the dry cap) scale (round forty-one, the review's depth test). Every painter of a thing under the water
    /// skips a pixel where this is more than the thing's own nearness (Light.Nearness of its depth), so a stone hides
    /// what is behind it the way a rock hides a fish, bit by bit, fins and all. Until this, one yes-or-no stencil of
    /// the stone's top quarter (StoneTop), chosen by the head's depth, left three holes: a fish with its head above a
    /// quarter of the depth painted across the collar, a deep fish painted over a flank deeper than a quarter, and
    /// the fins at any depth (3 to 9 seconds of every minute with a koi on a rock).
    /// </summary>
    public required byte[] Near { get; init; }

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
    /// <summary>The old log sunk on the deep bed, part of the terrain (see SunkenLog); null when no seed-and-shape spot was found. The deep dwellers read it for the den.</summary>
    public required SunkenLog? Sunken { get; init; }

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

    // A coarse grid of "how far is the nearest wall", in pixels, for steering, STACKED BY DEPTH (round forty-three,
    // the review's step 3 part C: one clearance field). Think of the pond as a stack of sixteen floors, the top one at
    // the surface and the bottom one at the pond's full depth. On each floor the walls are the land, whatever stands
    // in the water, the picture's edge, AND every bit of bed that is shallower than that floor: the deeper the floor,
    // the more of the bed's slopes and the stones' mounds stand up through it as walls. A swimmer asks for its room
    // on the floor its OWN step rule would refuse it at (Koi.WallZ: its cap and its shore; the turtle nine tenths of
    // its depth; the minnows their dip's rule), so a mound rising ahead is falling room to its feelers, a slope they
    // turn from early, instead of an invisible wall its step then refuses. That was round thirty-nine's class of jam
    // at the stones' shoulders: a wall only the step knew was a cliff to the feelers, and the "uphill on the room"
    // fallback climbed INTO it while the refused step's probe pointed out (a goldfish stood 115 s). Between floors
    // the room is blended, so it changes smoothly as a fish rises or sinks, never in steps. The top floor is the old
    // grid exactly: what every placement test and every surface animal reads, as before.
    private readonly float[][] _rooms;
    private readonly float[][] _roomsKoi;                // the same floors with the stones' shoulders (StoneShoal) as walls too: what a KOI steers by (round thirty-nine); a floor the shoal adds no wall to is the shared one
    private readonly int _gw, _gh;
    private readonly float _cell;

    /// <summary>How many depth floors the room is stacked in: the first at the surface, the last at the pond's full depth.</summary>
    public const int Layers = 16;

    public Pond(float[][] rooms, float[][] roomsKoi, int gw, int gh, float cell)
    {
        _rooms = rooms; _roomsKoi = roomsKoi; _gw = gw; _gh = gh; _cell = cell;
    }

    /// <summary>
    /// How much open water there is around a point at the SURFACE: the distance
    /// to the nearest bank or rock, in pixels (0 on land). Read from the coarse
    /// grid and blended between its four nearest cells, so it changes smoothly
    /// as a fish swims, never in steps. What placements and surface animals ask.
    /// </summary>
    public float RoomAt(float x, float y) => Sample(_rooms[0], x, y);

    /// <summary>
    /// The room at a point for a swimmer whose step rule refuses any bed shallower than "wallZ" (0 the surface, 1 the
    /// pond's full depth): the distance to the nearest land, rock, picture edge OR bed that shallow, in pixels. Blended
    /// between the two nearest floors of the stack.
    /// </summary>
    public float RoomAt(float x, float y, float wallZ) => SampleZ(_rooms, x, y, wallZ);

    /// <summary>
    /// The room a KOI has (round thirty-nine): the same stack with every stone's shoulder (StoneShoal, where a koi may
    /// not swim: the boulder itself, collar and side) as a wall on every floor, so the room falls off toward a stone
    /// the way it falls off toward the bank. The minnows, the turtle and the snake swim or walk over the shoulders,
    /// so they keep the shared stack. Without a depth: the surface floor, for placements.
    /// </summary>
    public float KoiRoomAt(float x, float y) => Sample(_roomsKoi[0], x, y);
    public float KoiRoomAt(float x, float y, float wallZ) => SampleZ(_roomsKoi, x, y, wallZ);

    private float SampleZ(float[][] floors, float x, float y, float wallZ)
    {
        float f = Math.Clamp(wallZ, 0f, 1f) * (Layers - 1);
        int k = Math.Min((int)f, Layers - 2);
        float t = f - k;
        float a = Sample(floors[k], x, y);
        return t <= 0 ? a : a + (Sample(floors[k + 1], x, y) - a) * t;
    }

    private float Sample(float[] grid, float x, float y)
    {
        float gx = Math.Clamp(x / _cell, 0, _gw - 1.001f), gy = Math.Clamp(y / _cell, 0, _gh - 1.001f);
        int ix = (int)gx, iy = (int)gy;
        float fx = gx - ix, fy = gy - iy;
        float a = grid[iy * _gw + ix], b = grid[iy * _gw + ix + 1];
        float c = grid[(iy + 1) * _gw + ix], d = grid[(iy + 1) * _gw + ix + 1];
        return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
    }

    /// <summary>Which way is "more open water" from here: the uphill direction of RoomAt (not normalised), on the floor a bed shallower than wallZ is a wall at (the surface when left out).</summary>
    public PointF OpenWay(float x, float y, float wallZ = 0)
    {
        float s = _cell;
        return new PointF(RoomAt(x + s, y, wallZ) - RoomAt(x - s, y, wallZ), RoomAt(x, y + s, wallZ) - RoomAt(x, y - s, wallZ));
    }

    /// <summary>The koi's "more open water": the uphill direction of KoiRoomAt, the stones' shoulders being walls to it, on the floor for wallZ.</summary>
    public PointF KoiOpenWay(float x, float y, float wallZ = 0)
    {
        float s = _cell;
        return new PointF(KoiRoomAt(x + s, y, wallZ) - KoiRoomAt(x - s, y, wallZ), KoiRoomAt(x, y + s, wallZ) - KoiRoomAt(x, y - s, wallZ));
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
    // The water's own colour, by depth (round forty-four, "the underwater look ... look at some online photos"): a pond
    // seen from above is GREEN, an olive green going to a dark bottle green in the deep (algae in the water and on
    // everything under it); the teal-blue it was (22, 66, 72 and 86, 140, 118) was a swimming pool's. Every swimmer's
    // haze mixes toward this, so the whole pond shifted together.
    private static readonly Color Deep = Color.FromArgb(20, 62, 54);
    private static readonly Color Shallow = Color.FromArgb(92, 138, 102);
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
        // THE TERRAIN (round forty-one, the review's step 3): ONE height map of the bed, in ground pixels, is what
        // everything reads. The water's depth from the bank with its humps and hollows now; every stone's profile
        // (StoneZ) is lowered into it once the stones are placed (Lower), and the bank rocks' feet (LowerRocks). The
        // painted depth (Pond.Depth) and the bed the swimmers feel (Pond.Floor) are read off it as ONE array; until
        // this they were two (the floor had the stones' hills and the rocks' feet, the painted depth the mounds), and
        // every sighting of a fish on a rock lived in the gap. It reaches a band ABOVE the screen: the bed is drawn
        // down the screen by its depth (SinkBed), so the deep bed just out of view at the top slides into the picture.
        int band = (int)Light.SinkPx(1, u) + 2;
        float[] terrain = Terrain(inPond, dist, w, h, band, u);
        uint[] px = Read(bmp);
        uint[] bedPx = (uint[])px.Clone();                   // the bed before the water: what SinkBed draws through it, sunk
        PourWater(px, inPond, terrain, band, w, h);      // the water FLAT, each pixel at its place on the ground: the base the land painters paint against (SinkBed draws the real one)
        Write(bmp, px);
        uint[] flat = (uint[])px.Clone();
        // The murk patches and the drifted petals FLOAT: they are painted onto a clear sheet of their own and laid
        // over the sunk water at their own place (the bed under them sinks, they do not). Same calls, same dice.
        using var surfaceSheet = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var gs = Graphics.FromImage(surfaceSheet);
        gs.Clear(Color.Transparent);
        gs.SmoothingMode = SmoothingMode.AntiAlias;
        Murk(gs, inPond, w, h, u, rng);

        uint[] beforeHeap = ReadArgb(surfaceSheet);
        DriftedPetals(gs, inPond, dist, w, h, u, rng);                   // before the rocks, which sit on top of them
        uint[] surface = ReadArgb(surfaceSheet);
        bool[] heapTouched = Brushwork.Unchanged(beforeHeap, surface);   // false where a drifted petal was painted (its soft rim too)

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
        byte[] near = new byte[w * h];                   // the stones' drawn surface as nearness per screen pixel (Pond.Near), filled by the wraps below
        // THE STONES ARE PLACED BEFORE ANY IS PAINTED, AND EACH STANDS ON A MOUND (round forty-one, the review's step 2:
        // "the current rocks look like 40 ft tall"). Until now a stone's hill fell from its dry cap to the full bed within
        // 0.05u of its painted edge: 570 px of height over 102 px of run at 1080 lines, a column five times taller than
        // wide, drawn 508 px tall under the pond's slant. Now one profile (StoneZ) shapes every stone for the picture,
        // the bed the animals feel and the stone's own flank: the collar at the surface, the boulder's side falling at
        // about forty-five degrees to a shoulder 0.12 of the depth down at its painted edge, and from there a mound
        // sloping to the bed over 0.35u. The mound is in the PAINTED depth too (the water poured again over it from
        // the bed kept before the pour), so it shows as the shoal round a boulder that it is, lit by its slope like
        // every hump of the bed (LightBed), and the fish rise over it. Placing both formations before the island is
        // painted moves the painter's dice: a whole-scene change, said as one.
        Formation? formation = fallenLog ? null : ShorePainter.PlaceFormation(inPond, dist, shade, island, w, h, u, rng);
        Formation? formation2 = null;
        Random? rng2 = null;
        if (formation is Formation first)
        {
            rng2 = new Random(unchecked((int)first.Stone.Centre.X * 31 + (int)first.Stone.Centre.Y * 7 + w));
            formation2 = ShorePainter.PlaceFormation(inPond, dist, shade, island, w, h, u, rng2, new RectangleF(0.5f, 0.08f, 0.45f, 0.42f), new PointF(w, 0), first.Stone);
        }
        // Every stone's profile goes into the terrain now, before any is painted: the bed the fish feel, the painted
        // depth and the stone's own flank are all read off the one map below. The boulder itself (collar and side,
        // down to ShoulderZ) is marked as the koi's shoal (Pond.StoneShoal); the mound beyond is bed a fish rises over.
        // Each stone's mound runs out by direction (MoundShape, dealt from where the stone stands): a whale-back, a
        // bar of silt on one side, three lobes; swept round in a circle every stone was a dome with a cap ("boobs").
        var stoneShoal = new bool[w * h];
        // THE ROCK IS THE ROCK (round forty-six, "right now you have the water line stopping in a perfect circle around the rocks"):
        // the slope under the water starts at the rock's own edge (a hair past the cap), not at the old painted footprint, which
        // held a plateau of shallow water that drew as a perfect pale disc; from the edge it runs out unevenly (MoundShape) to the bed.
        // Measured (kit pond46, the depth map across the island): the drawn rock reaches about 0.8 of CapRadius, so the water's
        // depth must start falling THERE; at the cap radius with a collar it held a ring of zero-depth water round the rock,
        // drawn pale, the "perfect circle". The shape's cap is 0.78 of the drawn one, no collar, the shoulder by 0.95.
        if (island is Island mound) Lower(terrain, stoneShoal, inPond, mound.Cap, mound.CapRadius * 0.7f, mound.CapRadius * 0.02f, mound.CapRadius * 0.9f, MoundShape.Of(mound.Centre, w), w, h, band, u);
        if (formation is Formation fm)
            for (int k = 0; k < fm.Caps.Length; k++) Lower(terrain, stoneShoal, inPond, fm.Caps[k], fm.CapRadii[k] * 0.7f, fm.CapRadii[k] * 0.02f, fm.CapRadii[k] * 0.9f, MoundShape.Of(fm.Stone.Centre, w), w, h, band, u);
        if (formation2 is Formation fm2)
            for (int k = 0; k < fm2.Caps.Length; k++) Lower(terrain, stoneShoal, inPond, fm2.Caps[k], fm2.CapRadii[k] * 0.7f, fm2.CapRadii[k] * 0.02f, fm2.CapRadii[k] * 0.9f, MoundShape.Of(fm2.Stone.Centre, w), w, h, band, u);
        // The old log on the deep bed (SunkenLog, 2026-10-30), placed once the mounds are in the terrain (it keeps off
        // them) and lowered into it: from here on it is bed. Its own dice, dealt from the screen and the island's place.
        var logRng = new Random(unchecked(w * 31 + h * 17 + 4242 + (island is Island il ? (int)il.Centre.X * 7 + (int)il.Centre.Y : 0)));
        SunkenLog? sunken = PlaceSunkenLog(inPond, terrain, band, shade, w, h, u, logRng);
        if (sunken is SunkenLog lg) LowerLog(terrain, inPond, lg, w, h, band);
        // The picture before any stone is painted: the flat water with everything the land painters put on it (the
        // bank rocks' wet rims, an afloat log). SinkBed lays that back over the sunk water at its own place, and the
        // caps' wet bands are mixed toward it.
        uint[] beforeStones = Read(bmp);
        // What each stone's flank looks like at every GROUND pixel of its footprint (StoneColour), for SinkBed to draw
        // sunk along with the bed: the colour, how solid it is (0 at the dissolving foot) and whether it is stone at all.
        var stoneCol = new uint[w * h];
        var stoneA = new byte[w * h];
        var isStone = new bool[w * h];
        var hollow = new bool[w * h];                       // the sunken log's hollow and its throat to the tip (ground pixels): nothing in it is nearer than the hollow's floor, so a resident's head in the hole is drawn (round forty-six)
        if (island is Island isle)
        {
            bool[] stone = ShorePainter.IslandStone(g, isle, w, h, u, rng);
            bool[] cap = ShorePainter.IslandCap(g, gb, isle, w, h, u, rng);
            StoneColour(bmp, beforeStones, stone, cap, isle, terrain, band, isle.CapRadius * 0.5f, stoneCol, stoneA, isStone, near, w, h, u);
            rocks.Add(new BankRock(isle.Cap, isle.CapRadius, InWater: true));   // the snake keeps off the cap; the lizard never sees an in-water rock
        }
        if (formation is Formation firstForm)
        {
            PaintFormation(firstForm, rng);
            if (formation2 is Formation second) PaintFormation(second, rng2!);
        }

        // One stone with little dry caps, painted the island's way: the stone bare, each cap a rock of its own and a
        // blocker. The flank's depth is read from the nearest cap's rim, so between two caps half a radius apart the
        // stone stays shallow and in sight, one stone with two tops (the first render, which faded each cap on its
        // own, left each in a dark cloud of its own).
        void PaintFormation(Formation form, Random r)
        {
            bool[] stone = ShorePainter.IslandStone(g, form.Stone, w, h, u, r);
            bool[] caps = new bool[w * h];
            for (int k = 0; k < form.Caps.Length; k++)
            {
                bool[] one = ShorePainter.IslandCap(g, gb, form.Stone with { Cap = form.Caps[k], CapRadius = form.CapRadii[k] }, w, h, u, r);
                for (int i = 0; i < caps.Length; i++) caps[i] |= one[i];
                rocks.Add(new BankRock(form.Caps[k], form.CapRadii[k], InWater: true));
            }
            StoneColour(bmp, beforeStones, stone, caps, form.Stone with { Cap = form.Caps[0], CapRadius = form.CapRadii[0] }, terrain, band, form.CapRadii[0] * 0.4f, stoneCol, stoneA, isStone, near, w, h, u);
        }
        // THE BED, DRAWN SUNK (round forty-one, the review's step 3: "the bed is drawn straight down; everything on it is
        // drawn slanted"). The bank rocks' feet go into the terrain last (the rocks are all placed now), then ONE fill
        // draws every ground pixel of the bed and of the stones' flanks down the screen by its depth, the way every
        // swimmer is drawn (Light.SinkPx), and fills Pond.Near with the bed's drawn depth too.
        if (sunken is SunkenLog wood) LogColour(wood, terrain, band, stoneCol, stoneA, isStone, hollow, w, h, u);   // into the flank arrays: SinkBed draws it sunk like a stone's flank
        LowerRocks(terrain, rocks, inPond, w, h, band, u);
        // The bed's litter (round forty-four): cobbles, rubble at the boulders' feet, leaves, twigs and algae, into the
        // bed's ground picture now that every stone is in the terrain, so SinkBed tints and sinks it with the bed.
        var boulders = new List<Island>();
        if (island is Island b0) boulders.Add(b0);
        if (formation is Formation b1) boulders.Add(b1.Stone);
        if (formation2 is Formation b2) boulders.Add(b2.Stone);
        DressBed(bedPx, inPond, terrain, band, boulders, w, h, u);
        SinkBed(bmp, flat, beforeStones, surface, terrain, band, bedPx, stoneCol, stoneA, isStone, hollow, inPond, Read(blockers), near, w, h, u);
        // The one map, as the byte array everything in the scene reads (Pond.Depth and Pond.Floor are this same array).
        var depth = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                depth[y * w + x] = (byte)(Math.Clamp(terrain[(y + band) * w + x], 0f, 1f) * 255);
        PointF cattails = CattailSpot(bank, corner, inPond, dist, w, h, u, rng);
        List<(PointF Root, float Toward)> irises = IrisSpots(gn, Read(noGrassSheet), inPond, bank, corner, cattails, u, w, h, rng);   // after the rocks and the sand: on the lawn behind them

        // ---- 4: on the water ----
        // The lily pads are placed here (and their pictures painted, each on its own sheet), but not painted into the
        // backdrop: they are a live layer since round forty (see LilyPad). The pad mask sheet stays black, so the water
        // runs on under them and a koi is drawn there, under the pad.
        var pads = new List<LilyPad>();
        LilyPads(pads, inPond, dist, cattails, island, formation?.Stone, formation2?.Stone, w, h, u, rng);

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
        var nearWall = new bool[w * h];
        for (int i = 0; i < nearWall.Length; i++) nearWall[i] = swimDist[i] <= crevT;  // walls and the water within r of one
        foreach (Island? st in (ReadOnlySpan<Island?>)[island, formation?.Stone, formation2?.Stone])
        {
            if (st is not Island stone) continue;
            int boxR = (int)(stone.Radius + u * 0.1f);
            int bx0 = Math.Max(0, (int)stone.Centre.X - boxR), bx1 = Math.Min(w - 1, (int)stone.Centre.X + boxR);
            int by0 = Math.Max(0, (int)stone.Centre.Y - boxR), by1 = Math.Min(h - 1, (int)stone.Centre.Y + boxR);
            int[] toWide = DistanceToEdge(nearWall, w, h, false, bx0, by0, bx1, by1);     // from a near pixel to the nearest wide water, inside the box
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    int i = y * w + x;
                    if (swim[i] && nearWall[i] && toWide[i] > crevT) swim[i] = false;
                }
        }
        // ---- the floor under the fish IS the painted depth: the one terrain (round forty-one, the review's step 3) ----
        // Until this the floor was the painted depth with every rock's foot and every stone's hill raised into it on its
        // own; the stones' profiles and the rocks' feet are in the terrain itself now (Lower, LowerRocks above), so the
        // bed a swimmer is capped by is the bed that is drawn under it, pixel for pixel.
        byte[] floor = depth;
        // THE ROOM, STACKED BY DEPTH (round forty-three, the review's one clearance field; Pond's grid fields tell the
        // rest). Floor k of the stack is for a swimmer whose step rule refuses any bed shallower than k / (Layers - 1) of
        // the pond's full depth: on that floor every such pixel of bed is a wall, as the land is, so the room falls off
        // toward a mound or the bank's slope the way it falls off toward the bank itself. Floor 0 is the surface: the old
        // grid, the land alone. The koi's stack (round thirty-nine) has the stones' shoulders as walls too; a floor the
        // shoal adds nothing to (every shoal pixel already too shallow for it) shares the common floor. One distance
        // transform per floor, four at a time (each wants 8 MB of scratch at 1080p; all sixteen at once would be a
        // burst of 130 MB), about 0.1 s of the start.
        var rooms = new float[Pond.Layers][];
        var roomsKoi = new float[Pond.Layers][];
        float[] Coarse(int[] dist)
        {
            var grid = new float[gw * gh];
            for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    int x = Math.Min(w - 1, (int)(gx * cell)), y = Math.Min(h - 1, (int)(gy * cell));
                    grid[gy * gw + gx] = dist[y * w + x] / 10f;
                }
            return grid;
        }
        Parallel.For(0, Pond.Layers, new ParallelOptions { MaxDegreeOfParallelism = 4 }, k =>
        {
            int wallUnder = (int)MathF.Ceiling(255f * k / (Pond.Layers - 1));      // bed with a depth byte under this is a wall on this floor
            bool[] swimK = swim;
            if (k > 0)
            {
                swimK = new bool[w * h];
                for (int i = 0; i < swimK.Length; i++) swimK[i] = swim[i] && depth[i] >= wallUnder;
            }
            rooms[k] = Coarse(DistanceToEdge(swimK, w, h, edgeIsBank: true));
            bool shoalAdds = false;
            for (int i = 0; i < swimK.Length && !shoalAdds; i++) shoalAdds = swimK[i] && stoneShoal[i];
            if (!shoalAdds) { roomsKoi[k] = rooms[k]; return; }
            var swimKoi = new bool[w * h];
            for (int i = 0; i < swimKoi.Length; i++) swimKoi[i] = swimK[i] && !stoneShoal[i];
            roomsKoi[k] = Coarse(DistanceToEdge(swimKoi, w, h, edgeIsBank: true));
        });

        // The heap: every pixel a drifted petal touched that is still open water in the finished picture (a rock or the
        // log painted over it later is not water, so it is left out; the branch shade darkened it, and the backdrop pixel
        // copied back carries that shade).
        var heap = new List<int>();
        for (int i = 0; i < waterMask.Length; i++) if (!heapTouched[i] && waterMask[i]) heap.Add(i);

        return new Pond(rooms, roomsKoi, gw, gh, cell)
        {
            Width = w, Height = h, U = u,
            Pixels = beforeBranches, Branches = boughs, TreeRoots = treeRoots,
            Water = waterMask, Open = open, Grassy = grassy, Carpet = ReadArgb(carpetSheet), Depth = depth, Floor = floor, Sun = sun, Heap = heap.ToArray(), Near = near, StoneShoal = stoneShoal,
            BlossomSpots = spots, Pads = pads, CattailSpot = cattails, IrisSpots = irises, Centre = centre, Reach = reach,
            Land = inPond.Select((p, i) => !p || (blocked[i] & 0xFF) >= 128).ToArray(),    // the ground, plus whatever stands in the water (a rock's wet side, the log)
            Rocks = rocks, Log = log, Island = island, Formation = formation, Formation2 = formation2, Sunken = sunken,
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
        // The deep bed is dressed later, once the stones and the log are in the terrain (DressBed): cobbles in gravel
        // bars, rubble at every boulder's foot, dead leaves, twigs and algae. (From 2026-10-26 night to round forty-four
        // it was a scatter of small dark discs over the whole deep, "stones lying in the silt": at 2 to 9 px each,
        // alone on a flat field, they read as polka dots, which is the one grain he has always called fake.)
    }

    /// <summary>
    /// THE BED DRESSED (round forty-four, "the underwater look and the terrain need work. look at some online photos").
    /// What the photos of real pond bottoms have that the bed did not: it is a CARPET OF THINGS, not a field with a
    /// grain. Fist-sized stones each with a lit top and a dark underside, packed into gravel bars with silt between;
    /// algae, an olive-green film over every stone that has sat in the water a season; last year's leaves lying flat,
    /// tan and brown and ochre; the odd dark twig; and round the foot of every boulder a heap of rubble, the stones
    /// that broke off it or were banked against it. Painted here into the ground picture of the bed (bedPx, the bed
    /// BEFORE the water is laid over it), so SinkBed tints it by the water's depth and draws it sunk exactly as it
    /// draws the rest: a leaf on a mound's flank sinks with the flank. Everything rolls its OWN dice (seeded from the
    /// screen's size), so no roll of the scene's moves and every seed keeps its pond.
    ///
    /// FEYNMAN VERSION: the old bed was a painted floor with dots on it; this scatters real-looking litter over the
    /// floor before the water goes on top, so what you see through the water has edges, shadows and colours to read.
    ///
    /// THE MOUNDS ("the terrain need work"): a boulder's mound ran out from its painted edge as smooth bed, lit by its
    /// slope, so each stone sat on a bald green hill many times its width, a dome with a pebble on top. The terrain is
    /// unchanged (every swimmer's bed, every placement and the one shape of a stone stand); what changes is what the
    /// hill is MADE of: rubble, densest at the boulder's foot and thinning out over 0.16u, so the eye reads "a boulder
    /// on a heap of stones" where it read "a smooth dome". The slope's lighting is also softer (SinkBed, a quarter where
    /// it was a third).
    /// </summary>
    /// <param name="bedPx">The bed's ground picture before the water (Paint's clone of the bitmap after the sand and the shallows' pebbles); written.</param>
    /// <param name="stones">Every boulder in the water (the island and the formations' stones), for the rubble at their feet.</param>
    private static void DressBed(uint[] bedPx, bool[] inPond, float[] terrain, int band, List<Island> stones, int w, int h, float u)
    {
        var rng = new Random(w * 7919 + h * 104729 + 44);
        float Z(int x, int y) => terrain[(Math.Clamp(y, 0, h - 1) + band) * w + Math.Clamp(x, 0, w - 1)];
        // Steep ground takes no litter (round forty-six, "the textures ... stretch weird when they're on the slope of the rocks"):
        // a cobble or a leaf laid on a flank is drawn sunk down the slope, a vertical oval; the flank keeps the stone's own
        // colour. Gentle is a depth change under 0.045 over six ground pixels either way.
        bool Gentle(int x, int y) => MathF.Abs(Z(x + 3, y) - Z(x - 3, y)) + MathF.Abs(Z(x, y + 3) - Z(x, y - 3)) < 0.045f;
        bool Open(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && inPond[y * w + x] && Z(x, y) > 0.3f && Gentle(x, y);   // the open bed, off the shallows' slope (which keep their sand and pale pebbles) and off the flanks
        bool Dress(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && inPond[y * w + x] && Z(x, y) > PondPainter.ShoulderZ + 0.01f && Gentle(x, y);   // anywhere under the water that is bed, not boulder, not a flank
        float area = w * (float)h / (u * u);                                                        // screen area in u squared (1.78 at 16:9)

        // ---- 1. the silt itself. The sand colour (Bed) is the shallows'; the open bed is darker olive silt with a fine
        // grain (a pond's bottom is mud, not a beach), eased in from 0.2 to 0.7 of the depth so the slope keeps its sand.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inPond[i]) continue;
                float t = Math.Clamp((Z(x, y) - 0.2f) / 0.5f, 0, 1);
                // Round a rock the bed is silt whatever its depth (round forty-six, "odd circles around the top of the rocks"): the slope
                // rising to a boulder is shallow, and shallow bed kept the shore's SAND colour, so every rock sat on a pale dome.
                foreach (Island st in stones)
                {
                    float sdx = x - st.Cap.X, sdy = y - st.Cap.Y, near = st.CapRadius * 2.2f;
                    if (sdx * sdx + sdy * sdy < near * near) { t = MathF.Max(t, Math.Clamp(1f - (MathF.Sqrt(sdx * sdx + sdy * sdy) - st.CapRadius * 1.3f) / (st.CapRadius * 0.9f), 0f, 1f)); }
                }
                if (t <= 0) continue;
                t = t * t * (3 - 2 * t) * 0.85f;
                uint hsh = (uint)(x * 73856093 ^ y * 19349663); hsh = hsh * 1664525u + 1013904223u;
                float grain = ((hsh >> 24) / 255f * 2 - 1) * 7 * t;                                   // plus or minus seven levels of grain, more where the silt is
                uint c = bedPx[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                r = (int)(r + (94 - r) * t + grain); g = (int)(g + (90 - g) * t + grain); b = (int)(b + (62 - b) * t + grain);
                bedPx[i] = Pack(r, g, b);
            }

        // ---- 2. algae: soft patches of a darker olive green where a film of it has grown over the silt. Bigger and
        // fainter than any stone, so they read as colour, not as things.
        for (int n = 0; n < (int)(16 * area); n++)
        {
            PointF p = WaterPoint(inPond, w, h, rng, k => Z(k % w, k / w) > 0.3f);
            float rx = u * (0.05f + 0.12f * (float)rng.NextDouble()), ry = rx * (0.5f + 0.5f * (float)rng.NextDouble());
            float strength = 0.25f + 0.25f * (float)rng.NextDouble();
            int x0 = Math.Max(0, (int)(p.X - rx)), x1 = Math.Min(w - 1, (int)(p.X + rx)), y0 = Math.Max(0, (int)(p.Y - ry)), y1 = Math.Min(h - 1, (int)(p.Y + ry));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ex = (x - p.X) / rx, ey = (y - p.Y) / ry, q = ex * ex + ey * ey;
                    if (q >= 1 || !inPond[y * w + x]) continue;
                    float f = (1 - q) * (1 - q) * strength;
                    Mix(bedPx, y * w + x, 60, 86, 44, f);
                }
        }

        // ---- 3. the cobble carpet: gravel bars, each a stretched patch packed with stones (a few hundred), the stones
        // thinning toward the bar's edge so it has no rim. Six colours of stone, each with a film of algae of its own.
        int bars = 2 + (int)(2 * area);
        for (int n = 0; n < bars; n++)
        {
            PointF p = WaterPoint(inPond, w, h, rng, k => Z(k % w, k / w) > 0.3f);
            float rx = u * (0.07f + 0.12f * (float)rng.NextDouble()), ry = rx * (0.35f + 0.45f * (float)rng.NextDouble());
            float ang = (float)(rng.NextDouble() * Math.PI), ca = MathF.Cos(ang), sa = MathF.Sin(ang);
            int count = (int)(MathF.PI * rx * ry / (u * 0.012f * u * 0.012f) * 0.55f);
            for (int k = 0; k < count; k++)
            {
                float ex = (float)(rng.NextDouble() * 2 - 1), ey = (float)(rng.NextDouble() * 2 - 1), q = ex * ex + ey * ey;
                if (q >= 1 || rng.NextDouble() > MathF.Sqrt(1 - q)) continue;                           // dense in the middle, sparse at the edge
                float cx = p.X + (ex * rx * ca - ey * ry * sa), cy = p.Y + (ex * rx * sa + ey * ry * ca);
                float r = u * (0.004f + 0.009f * MathF.Pow((float)rng.NextDouble(), 1.5f));
                Cobble(bedPx, w, h, cx, cy, r, rng, 0.15f + 0.4f * (float)rng.NextDouble(), Open);
            }
        }

        // ---- 4. rubble at every boulder's foot: cobbles, bigger than the bars', densest against the stone's painted
        // edge and thinning out over 0.16u down its mound; greener (a boulder's foot is where the algae is thickest).
        foreach (Island st in stones)
        {
            float reach = u * 0.16f;
            int count = (int)(2 * MathF.PI * (st.CapRadius * 1.1f + reach * 0.5f) * reach / (u * 0.014f * u * 0.014f) * 0.28f);   // (0.8 on the first render: a dome of bubble wrap)
            for (int k = 0; k < count; k++)
            {
                float a = (float)(rng.NextDouble() * Math.Tau), d = st.CapRadius * 1.1f + reach * MathF.Pow((float)rng.NextDouble(), 2.2f);   // (from the rock's own edge since round forty-six)
                float cx = st.Centre.X + MathF.Cos(a) * d, cy = st.Centre.Y + MathF.Sin(a) * d;
                float r = u * (0.004f + 0.009f * MathF.Pow((float)rng.NextDouble(), 1.3f));
                Cobble(bedPx, w, h, cx, cy, r, rng, 0.3f + 0.45f * (float)rng.NextDouble(), Dress);
            }
        }

        // ---- 5. twigs: a few dark sticks lying on the bed, each with one kink.
        for (int n = 0; n < (int)(9 * area); n++)
        {
            PointF p = WaterPoint(inPond, w, h, rng, k => Z(k % w, k / w) > 0.3f);
            float len = u * (0.03f + 0.06f * (float)rng.NextDouble()), a0 = (float)(rng.NextDouble() * Math.Tau), a1 = a0 + (float)(rng.NextDouble() - 0.5) * 0.7f;
            int tone = rng.Next(16);
            var mid = new PointF(p.X + MathF.Cos(a0) * len * 0.5f, p.Y + MathF.Sin(a0) * len * 0.5f);
            Twig(bedPx, w, h, p, mid, 52 + tone, 42 + tone, 30 + tone, Dress);
            Twig(bedPx, w, h, mid, new PointF(mid.X + MathF.Cos(a1) * len * 0.5f, mid.Y + MathF.Sin(a1) * len * 0.5f), 52 + tone, 42 + tone, 30 + tone, Dress);
        }

        // ---- 6. dead leaves: last autumn's, lying flat on the silt at any angle, five colours from tan to red-brown,
        // a darker midrib down each. The one thing in every photo of a pond bottom.
        (int, int, int)[] leafColours = [(146, 118, 72), (108, 80, 50), (152, 126, 62), (122, 74, 50), (160, 142, 84)];
        for (int n = 0; n < (int)(36 * area); n++)
        {
            PointF p = WaterPoint(inPond, w, h, rng, k => Z(k % w, k / w) > 0.3f);
            float len = u * (0.02f + 0.016f * (float)rng.NextDouble()), ang = (float)(rng.NextDouble() * Math.Tau);
            Leaf(bedPx, w, h, p, len, ang, leafColours[rng.Next(leafColours.Length)], Dress);
        }
    }

    private static uint Pack(int r, int g, int b) => (uint)(Math.Clamp(r, 0, 255) << 16 | Math.Clamp(g, 0, 255) << 8 | Math.Clamp(b, 0, 255));

    /// <summary>Mixes the pixel at i toward (r, g, b) by f (0 none, 1 all).</summary>
    private static void Mix(uint[] px, int i, int r, int g, int b, float f)
    {
        uint c = px[i];
        int cr = (int)((c >> 16) & 0xFF), cg = (int)((c >> 8) & 0xFF), cb = (int)(c & 0xFF);
        px[i] = Pack((int)(cr + (r - cr) * f), (int)(cg + (g - cg) * f), (int)(cb + (b - cb) * f));
    }

    private static readonly (int, int, int)[] CobbleColours = [(104, 104, 94), (92, 96, 70), (118, 106, 80), (116, 88, 62), (76, 78, 64), (118, 116, 100)];

    /// <summary>
    /// One stone lying on the bed, seen from above at the pond's slant (a little flatter than round): one of six stone
    /// colours filmed with algae by "algae" (0 bare, 1 all green), its top left lit and its lower right in shade (the
    /// sun is up and to the left, the scene's rule), the rim darkening all round so it reads as a thing with an
    /// underside, not a disc. Only onto pixels "where" allows.
    /// </summary>
    private static void Cobble(uint[] px, int w, int h, float cx, float cy, float r, Random rng, float algae, Func<int, int, bool> where)
    {
        (int cr, int cg, int cb) = CobbleColours[rng.Next(CobbleColours.Length)];
        float tone = 0.8f + 0.25f * (float)rng.NextDouble();
        int br = (int)((cr + (78 - cr) * algae) * tone), bg = (int)((cg + (102 - cg) * algae) * tone), bb = (int)((cb + (56 - cb) * algae) * tone);
        float ry = r * 0.78f;
        int x0 = (int)(cx - r) - 1, x1 = (int)(cx + r) + 1, y0 = (int)(cy - ry) - 1, y1 = (int)(cy + ry) + 1;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float ex = (x + 0.5f - cx) / r, ey = (y + 0.5f - cy) / ry, q = ex * ex + ey * ey;
                if (q >= 1 || !where(x, y)) continue;
                float lit = 1 - 0.16f * (ex + ey) * 0.7f;                                            // lit toward the top left, shaded toward the lower right
                float rim = q > 0.55f ? 1 - 0.3f * (q - 0.55f) / 0.45f : 1;                          // the rim turns under
                float f = Math.Clamp((1 - q) * 6, 0, 1);                                             // a soft edge, a pixel wide
                Mix(px, y * w + x, (int)(br * lit * rim), (int)(bg * lit * rim), (int)(bb * lit * rim), f);
            }
    }

    /// <summary>A dead leaf lying on the bed: a pointed oval "len" long at angle "ang", a shade darker toward its edges, a darker midrib down its length.</summary>
    private static void Leaf(uint[] px, int w, int h, PointF c, float len, float ang, (int, int, int) colour, Func<int, int, bool> where)
    {
        (int lr, int lg, int lb) = colour;
        float ca = MathF.Cos(ang), sa = MathF.Sin(ang), half = len * 0.5f, hw = half * 0.42f;
        int reach = (int)half + 1;
        for (int y = (int)c.Y - reach; y <= (int)c.Y + reach; y++)
            for (int x = (int)c.X - reach; x <= (int)c.X + reach; x++)
            {
                float dx = x + 0.5f - c.X, dy = y + 0.5f - c.Y;
                float a = (dx * ca + dy * sa) / half, b = (-dx * sa + dy * ca) / hw;
                float edge = MathF.Pow(MathF.Max(0, 1 - a * a), 0.7f);                                // pointed at both ends (an ellipse's ends are round)
                float ab = MathF.Abs(b);
                if (ab >= edge || !where(x, y)) continue;
                float shade = (1 - 0.18f * ab / MathF.Max(0.01f, edge)) * (ab < 0.14f ? 0.8f : 1f);   // the midrib
                float f = Math.Clamp((edge - ab) * hw * 1.5f, 0, 1);                                  // a soft edge
                Mix(px, y * w + x, (int)(lr * shade), (int)(lg * shade), (int)(lb * shade), f);
            }
    }

    /// <summary>A twig: a dark line two pixels wide from a to b, where allowed.</summary>
    private static void Twig(uint[] px, int w, int h, PointF a, PointF b, int r, int g, int bl, Func<int, int, bool> where)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        int steps = (int)(len * 1.5f) + 1;
        for (int k = 0; k <= steps; k++)
        {
            float t = k / (float)steps, x = a.X + dx * t, y = a.Y + dy * t;
            for (int oy = 0; oy <= 1; oy++)
                for (int ox = 0; ox <= 1; ox++)
                {
                    int ix = (int)(x - 0.5f) + ox, iy = (int)(y - 0.5f) + oy;
                    if (where(ix, iy)) Mix(px, iy * w + ix, r, g, bl, 0.85f);
                }
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
    /// THE TERRAIN (round forty-one, the review's step 3): how deep the bed is at every GROUND pixel, 0 (the water's
    /// edge) to 1 (Pond.DepthU), one float per pixel over the screen AND a band of "band" rows above it (row 0 of the
    /// array is "band" rows above the top of the screen; the screen pixel (x, y) is at (y + band) * w + x). This is
    /// the water's own depth from the bank; the stones (Lower) and the bank rocks' feet (LowerRocks) are lowered into
    /// it afterwards, and the painted depth, the bed the swimmers feel, the water's colour and the sunk drawing of the
    /// bed (SinkBed) are all read off it. Land is 0.
    ///
    /// FEYNMAN VERSION: a relief map of the pond's bottom, one number per spot saying how far down the mud is. Before
    /// this there were two maps that disagreed (one the water was coloured from, one the fish were held off), and a
    /// fish stood on one while the picture showed the other.
    ///
    /// The depth eases in from the bank over 0.36u (a wide, gentle slope: "the land gradually rises underwater until
    /// it sticks out of the water and becomes the shore") and the open pond is UNEVEN: eleven broad soft humps and
    /// hollows scale it about 0.88 of the full depth, a hump up to the full depth and a hollow down to 0.76, more the
    /// further from the bank so the shallows keep their slope ("make the depths be uneven ... not the same
    /// everywhere"). The bumps roll their OWN dice, seeded from the screen's size, so nothing else in the scene moves.
    /// Above the screen the bank is taken to be as far as it is at the top row (the land is in a bottom corner) and
    /// the bumps carry on: that deep bed is what SinkBed draws down into the top of the view.
    /// </summary>
    private static float[] Terrain(bool[] inPond, int[] dist, int w, int h, int band, float u)
    {
        float full = u * 0.36f * 10f;                       // depth reaches its deepest this far from the bank (dist is in tenths); 0.24u until 2026-10-28
        var bumpRng = new Random(w * 7919 + h * 104729 + 7);
        const int bumps = 11;                                 // (7 until 2026-10-28: more, and the open pond's depth varies ABOUT its typical depth instead of being clamped at the full one)
        float[] bx = new float[bumps], by = new float[bumps], br = new float[bumps], bk = new float[bumps];
        for (int k = 0; k < bumps; k++)
        {
            bx[k] = (float)bumpRng.NextDouble() * w; by[k] = (float)bumpRng.NextDouble() * h;
            br[k] = 1 / (u * (0.18f + 0.14f * (float)bumpRng.NextDouble()));                  // kept as the reciprocal: a multiply per pixel, not a divide
            bk[k] = (k % 2 == 0 ? 1 : -1) * (0.6f + 0.4f * (float)bumpRng.NextDouble());   // humps and hollows by turns
        }
        // Each bump stretched along an axis of its own (1.4 to 2.6 times as long as wide) and turned: round bumps were
        // more domes on a bed that already had three ("vary it up", 2026-10-30). Rolled AFTER the loop above, so the
        // bumps keep the places and sizes they had.
        float[] bc = new float[bumps], bs = new float[bumps], be = new float[bumps];
        for (int k = 0; k < bumps; k++)
        {
            float a = (float)(bumpRng.NextDouble() * Math.Tau);
            bc[k] = MathF.Cos(a); bs[k] = MathF.Sin(a);
            be[k] = 1 / (1.4f + 1.2f * (float)bumpRng.NextDouble());                            // the reciprocal, a multiply per pixel
        }
        var z = new float[(h + band) * w];
        for (int gy = -band; gy < h; gy++)
        {
            int row = Math.Max(0, gy) * w;                   // a point above the view reads "is it water" and its bank distance from the top row
            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                if (!inPond[i]) continue;
                float d = Math.Clamp(dist[i] / full, 0f, 1f);
                d = d * d * (3 - 2 * d);                     // smoothstep: the slope eases in from the edge and flattens in the middle
                float n = 0;
                for (int k = 0; k < bumps; k++)
                {
                    float ex0 = (x - bx[k]) * br[k], ey0 = (gy - by[k]) * br[k];
                    float ex = (ex0 * bc[k] + ey0 * bs[k]) * be[k], ey = -ex0 * bs[k] + ey0 * bc[k], q = ex * ex + ey * ey;   // turned onto the bump's axis, squeezed along it
                    if (q < 4) { float f = 1 - q * 0.25f; n += bk[k] * f * f; }   // a smooth bump, gone two radii out
                }
                // The open pond is 0.88 of the full depth where no bump is, a hump up to the full depth and a hollow down to
                // 0.76 (before this the bumps were laid on a bed already at the full depth and clamped there, so half the
                // water sat at exactly 255 and only the hollows showed). Pond.DepthU rose with it (0.54 to 0.6).
                z[(gy + band) * w + x] = Math.Clamp(d * (0.88f + 0.12f * Math.Clamp(n, -1, 1) * d), 0, 1);   // (a quarter and tighter bumps on the first run: a forager climbing a hump could not rise as fast as the slope, and jammed)
            }
        }
        return z;
    }

    /// <summary>
    /// Pours the water in FLAT: each pond pixel's colour from the terrain's depth at that very pixel, at its place on
    /// the ground. Shallow water (near the bank) is clear, so the pebbles show, only tinted; deeper water hides the bed
    /// and turns dark teal; and the water reflects a little of the sky, more toward the far (top) side, where we look
    /// at it at more of a slant, as with any pond. This flat pour is what the land painters paint against (a bank
    /// rock's wet rim is mixed toward it) and what fills any screen pixel nothing sinks onto; the water the eye sees is
    /// SinkBed's, the same recipe drawn down the screen by its depth.
    /// </summary>
    private static void PourWater(uint[] px, bool[] inPond, float[] terrain, int band, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inPond[i]) continue;
                float d = terrain[(y + band) * w + x];
                // (No dark line at the edge here: the edge is alive, Shoreline paints it every frame.)
                px[i] = WaterOver(px[i], d, BedClear(d), y, h, 1);
            }
    }

    /// <summary>A picture of floats smoothed by a box blur of radius r, run twice (near enough a soft bell).</summary>
    private static float[] Blur(float[] src, int w, int h, int r)
    {
        var a = (float[])src.Clone();
        var b = new float[src.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            // Across: a running sum along each row, divided by the window it covers (narrower at the edges).
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0; int n = 0;
                for (int x = 0; x < Math.Min(w, r); x++) { sum += a[row + x]; n++; }
                for (int x = 0; x < w; x++)
                {
                    if (x + r < w) { sum += a[row + x + r]; n++; }
                    if (x - r - 1 >= 0) { sum -= a[row + x - r - 1]; n--; }
                    b[row + x] = sum / n;
                }
            }
            // Down: the same along each column, back into a.
            for (int x = 0; x < w; x++)
            {
                float sum = 0; int n = 0;
                for (int y = 0; y < Math.Min(h, r); y++) { sum += b[y * w + x]; n++; }
                for (int y = 0; y < h; y++)
                {
                    if (y + r < h) { sum += b[(y + r) * w + x]; n++; }
                    if (y - r - 1 >= 0) { sum -= b[(y - r - 1) * w + x]; n--; }
                    a[y * w + x] = sum / n;
                }
            }
        }
        return a;
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
    /// The three colour channels of a box of the sheet, box-blurred (two passes, radius r) within the box: the
    /// local colour of the water with its pebbles averaged out. Indexed (y - y0) * (x1 - x0 + 1) + (x - x0).
    /// </summary>
    private static (float[], float[], float[]) BlurBox(uint[] src, int w, int x0, int y0, int x1, int y1, int r)
    {
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        float[][] ch = [new float[bw * bh], new float[bw * bh], new float[bw * bh]];
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                uint c = src[(y + y0) * w + x + x0];
                ch[0][y * bw + x] = (c >> 16) & 0xFF; ch[1][y * bw + x] = (c >> 8) & 0xFF; ch[2][y * bw + x] = c & 0xFF;
            }
        var tmp = new float[bw * bh];
        foreach (float[] a in ch)
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < bh; y++)
                {
                    int row = y * bw; float sum = 0; int n = 0;
                    for (int x = 0; x < Math.Min(bw, r); x++) { sum += a[row + x]; n++; }
                    for (int x = 0; x < bw; x++)
                    {
                        if (x + r < bw) { sum += a[row + x + r]; n++; }
                        if (x - r - 1 >= 0) { sum -= a[row + x - r - 1]; n--; }
                        tmp[row + x] = sum / n;
                    }
                }
                for (int x = 0; x < bw; x++)
                {
                    float sum = 0; int n = 0;
                    for (int y = 0; y < Math.Min(bh, r); y++) { sum += tmp[y * bw + x]; n++; }
                    for (int y = 0; y < bh; y++)
                    {
                        if (y + r < bh) { sum += tmp[(y + r) * bw + x]; n++; }
                        if (y - r - 1 >= 0) { sum -= tmp[(y - r - 1) * bw + x]; n--; }
                        a[y * bw + x] = sum / n;
                    }
                }
            }
        return (ch[0], ch[1], ch[2]);
    }

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
    private static void LilyPads(List<LilyPad> pads, bool[] inPond, int[] dist, PointF cattails, Island? island, Island? formation, Island? formation2, int w, int h, float u, Random rng)
    {
        int clusters = 3;
        int tries = 0;
        var sheets = new List<(Bitmap, Graphics)>();
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
                // Each pad is painted onto a clear sheet of its own (round forty), the pad's middle at the sheet's
                // middle, with room round it for the shadow it throws on the water: the sheet becomes the pad's
                // Sprite below, and the scene stamps it wherever the pad has drifted to. The same rolls of the dice
                // as when it was painted into the backdrop, in the same order, so the pads are where they were.
                int side = (int)MathF.Ceiling(r * 2.4f) + 4;
                var sheet = new Bitmap(side, side, PixelFormat.Format32bppArgb);
                var gs = Graphics.FromImage(sheet);
                gs.SmoothingMode = SmoothingMode.AntiAlias;
                gs.Clear(Color.Transparent);
                Pad(gs, side * 0.5f, side * 0.5f, r, rng);
                sheets.Add((sheet, gs));
                pads.Add(new LilyPad(new PointF(px, py), r));
            }
        }
        // Water lilies on a few of the pads (onto the pad's own sheet, at the same spot on the pad as before).
        foreach (LilyPad p in pads.OrderBy(_ => rng.Next()).Take(Math.Max(1, pads.Count / 4)))
        {
            (Bitmap sheet, Graphics gs) = sheets[pads.IndexOf(p)];
            WaterLily(gs, new PointF(sheet.Width * 0.5f + p.Radius * 0.15f, sheet.Height * 0.5f - p.Radius * 0.2f), p.Radius * 0.75f, rng);
        }
        for (int i = 0; i < pads.Count; i++)
        {
            (Bitmap sheet, Graphics gs) = sheets[i];
            gs.Dispose();
            pads[i].Picture = Sprite.FromPixels(sheet.Width, sheet.Height, ReadArgb(sheet));
            sheet.Dispose();
        }
    }

    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static void Pad(Graphics g, float x, float y, float r, Random rng)
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
        // (Until round forty the pad was also painted white onto a mask sheet that kept the koi from being drawn under
        // it. The pad covers them itself now, stamped over the water after everything under it.)
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

    /// <summary>The shoulder of a stone: how deep the boulder's side has fallen by its painted edge, in Z (0.12 of the pond's depth: about forty-five degrees over the ring from the collar to the edge).</summary>
    public const float ShoulderZ = 0.12f;
    /// <summary>How far past its painted edge a stone's mound slopes down to the bed, in u. 0.08u since round forty-five: a boulder's own skirt of rubble, now that Light.Sink is 0.2 (at 0.55 a tight mound drew as a column, so it was widened to a hill: 0.35u, then 0.28u in round forty-one, 0.24u in round forty-four, and the hill read as an egg ten times the cap). A steeper mound is a wall to more koi steps: measured by causes43 against the old build each time it moves.</summary>
    public const float MoundU = 0.012f;   // (0.08u and then 0.05u earlier in round forty-six: any slope wide enough to see drew as a pale shelf of shallow water round the rock with a hard far edge where the deep bed, drawn down from behind it, took over, "circle areas that surround the top of the rocks and has a hard boundary that the water stops at". A hair of slope: the rock rises out of the deep bed, and the water goes up to it.)

    /// <summary>
    /// THE ONE SHAPE OF A STONE (round forty-one): how deep the stone's surface is, in Z, at a ground distance r
    /// from its cap's centre, for a cap of radius capR with a collar of width collar at the surface and a painted
    /// footprint of radius footR. The collar is at the surface; the boulder's side falls, eased, to ShoulderZ at
    /// the painted edge; beyond it the mound falls, eased, to the full depth over MoundU. The painter (the painted
    /// depth, the stone's own flank in WrapIsland), the bed the animals feel (Floor, through Hill) and the koi's
    /// shoal all read this, never a shape of their own. The caller takes the lower of this and the water's own
    /// depth at the pixel (a stone near the bank stands in shallow water).
    /// </summary>
    /// <param name="run">How far the mound runs in this direction, as a multiple of MoundU (MoundShape.Run; 1 is the round mound every stone had until 2026-10-30).</param>
    public static float StoneZ(float r, float capR, float collar, float footR, float u, float run = 1)
    {
        float side0 = capR + collar;
        if (r <= side0) return 0;
        if (r <= footR) { float t = (r - side0) / MathF.Max(1, footR - side0); return ShoulderZ * t * t * (3 - 2 * t); }
        float m = (r - footR) / (MoundU * u * run);
        if (m >= 1) return 1;
        return ShoulderZ + (1 - ShoulderZ) * m * m * (3 - 2 * m);
    }

    /// <summary>
    /// Lowers one stone's profile (StoneZ) into the terrain, the band above the screen included: at every ground
    /// pixel within the mound's reach the bed is the lower of what it was and the stone's surface (a stone near the
    /// bank stands in shallow water). The boulder itself (the collar and the side, down to ShoulderZ) is marked as the
    /// koi's shoal (Pond.StoneShoal); the mound beyond is bed a fish rises over. Until round forty-one's step 3 the
    /// mound was lowered into the painted depth (Mound) and the hill into the floor (Hill) by two loops over the same
    /// profile, and the stone's own flank read it a third way, from the cap's painted rim.
    /// </summary>
    private static void Lower(float[] terrain, bool[] shoal, bool[] inPond, PointF cap, float capR, float collar, float footR, in MoundShape mound, int w, int h, int band, float u)
    {
        float reach = footR + MoundU * u * 2.2f + 2;                                       // the longest run the shape allows
        int x0 = Math.Max(0, (int)(cap.X - reach)), x1 = Math.Min(w - 1, (int)(cap.X + reach));
        int y0 = Math.Max(-band, (int)(cap.Y - reach)), y1 = Math.Min(h - 1, (int)(cap.Y + reach));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!inPond[Math.Max(0, y) * w + x]) continue;
                float dx = x + 0.5f - cap.X, dy = y + 0.5f - cap.Y;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                if (r >= reach) continue;
                float z = StoneZ(r, capR, collar, footR, u, r > footR ? mound.Run(dx, dy, r) : 1);   // the run matters past the painted edge only
                if (z >= 1) continue;
                int k = (y + band) * w + x;
                if (z < terrain[k]) terrain[k] = z;
                if (y >= 0 && z <= ShoulderZ + 0.005f) shoal[y * w + x] = true;   // the boulder itself (collar and side) is the koi's shoal; the mound beyond is bed a fish rises over
            }
    }

    /// <summary>
    /// Where the old log lies on the deep bed: anywhere in the middle of the view, at any angle, 0.3 to 0.45u long
    /// and 0.06 to 0.08u thick, where the whole of it and a margin either side is deep bed (the terrain at least
    /// 0.64: off every stone's mound but its lowest skirt), under mostly open sky (it is there to be seen), and still on the screen once
    /// drawn sunk by its depth. Three stones' mounds reach 0.5u each, so on a seed with the open water under the
    /// boughs (seed 3 at 16:9) five hundred tries find nothing; a second pass then takes a shorter log (0.22 to
    /// 0.3u), lets it lie on a mound's lower skirt (0.6) and under the boughs' shade (the shade only darkens it; the
    /// swaying boughs uncover what is under them each frame). Its OWN dice (the caller's Random is made for it), so
    /// nothing else in the scene moves. Null when both passes fail (a small or odd screen).
    /// </summary>
    private static SunkenLog? PlaceSunkenLog(bool[] inPond, float[] terrain, int band, byte[] shade, int w, int h, float u, Random rng)
    {
        for (int tries = 0; tries < 1000; tries++)
        {
            bool second = tries >= 500;
            float minZ = second ? 0.55f : 0.64f; int maxShade = second ? 255 : 128;                 // (0.72 on the first pass left seed 3 at 16:9 with the log under the blossom: the mounds' long tails take most of the open water)
            float len = u * (second ? 0.22f + 0.08f * (float)rng.NextDouble() : 0.3f + 0.15f * (float)rng.NextDouble()), wid = u * (0.06f + 0.02f * (float)rng.NextDouble());
            float ang = (float)(rng.NextDouble() * Math.PI);                                    // 0 to pi covers every line (At runs the other way for the rest)
            var c = new PointF(w * (0.12f + 0.76f * (float)rng.NextDouble()), h * (0.1f + 0.7f * (float)rng.NextDouble()));
            var log = new SunkenLog(c, ang, len, wid, wid * 0.8f / (Pond.DepthU * u));
            float nx = -MathF.Sin(ang), ny = MathF.Cos(ang);                                     // across the log
            bool ok = true;
            for (int k = 0; k <= 10 && ok; k++)
                for (int side = -1; side <= 1 && ok; side++)
                {
                    PointF p = log.At(k / 10f);
                    int ix = (int)(p.X + nx * side * wid * 0.9f), iy = (int)(p.Y + ny * side * wid * 0.9f);
                    if (ix < u * 0.05f || iy < 0 || ix >= w - u * 0.05f || iy >= h) { ok = false; break; }
                    int i = iy * w + ix;
                    float z = terrain[(iy + band) * w + ix];
                    if (!inPond[i] || z < minZ || shade[i] > maxShade) ok = false;                  // deep bed off every mound (the hollows go to 0.76), under mostly open sky
                    else if (iy + Light.SinkPx(z, u) > h - u * 0.04f) ok = false;                   // drawn on the screen once sunk
                }
            if (ok) return log;
        }
        return null;
    }

    /// <summary>
    /// Lowers the sunken log into the terrain: a half cylinder lying on the bed (its top HeightZ above the bed under
    /// it, so it follows the bed's own bumps as a log that has settled does), with round ends (the distance is to the
    /// axis segment: a capsule), and a skirt of silt banked against its sides over half its half-width. From here on
    /// it IS the bed: the fish rise over it, the dwellers walk over it, SinkBed draws it sunk, Pond.Near hides what is
    /// behind it.
    /// </summary>
    private static void LowerLog(float[] terrain, bool[] inPond, in SunkenLog log, int w, int h, int band)
    {
        float hw = log.Width * 0.5f, skirt = hw * 0.5f, reach = log.Length * 0.5f + hw * 1.2f + skirt + 2;
        float ca = MathF.Cos(log.Angle), sa = MathF.Sin(log.Angle), halfLen = log.Length * 0.5f - hw;   // the ends are half-discs: the axis segment stops a half-width short
        int x0 = Math.Max(0, (int)(log.Centre.X - reach)), x1 = Math.Min(w - 1, (int)(log.Centre.X + reach));
        int y0 = Math.Max(-band, (int)(log.Centre.Y - reach)), y1 = Math.Min(h - 1, (int)(log.Centre.Y + reach));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!inPond[Math.Max(0, y) * w + x]) continue;
                float dx = x + 0.5f - log.Centre.X, dy = y + 0.5f - log.Centre.Y;
                float along = Math.Clamp(dx * ca + dy * sa, -halfLen, halfLen);
                float ex = dx - along * ca, ey = dy - along * sa, d = MathF.Sqrt(ex * ex + ey * ey);
                float hwHere = log.HalfWidthAt(along);
                if (d >= hwHere + skirt) continue;
                int k = (y + band) * w + x;
                float bed = terrain[k];
                float z;
                if (d < hwHere)
                {
                    // The round top (a half ellipse), its outer third eased down into the silt banked against it: a
                    // trunk lying on a soft bed has no vertical wall at its side, and a wall one ground pixel wide was
                    // drawn as a stair of nine screen rows per column along a slanting log on the first run.
                    float q = d / hwHere, core = MathF.Sqrt(1 - q * q), foot = Math.Clamp((q - 0.65f) / 0.35f, 0, 1);
                    foot = foot * foot * (3 - 2 * foot);
                    z = bed - log.HeightZ * (core * (1 - foot) + 0.12f * foot);
                }
                else z = bed - log.HeightZ * 0.12f * (1 - (d - hwHere) / skirt) * (1 - (d - hwHere) / skirt);   // the silt against its side
                if (z < terrain[k]) terrain[k] = z;
            }
    }

    /// <summary>
    /// What the sunken log looks like at every ground pixel of its footprint, into the same arrays as the stones'
    /// flanks (StoneColour), for SinkBed to draw sunk with the bed. OLD WET WOOD WITH ITS BARK ON (round forty-six, his
    /// line: "the underwater log looks less like a log than it does a log shaped pile of sediment"): until this it was
    /// a pale smooth tube ("driftwood", 200,172,132) with two knots and a split, lit by the half cylinder's slope and
    /// then veiled by the murk toward the bed around it, which left a soft grey pill. What says LOG to the eye is bark:
    /// ridges running along the trunk, dark fissures between them, the ridges breaking and wandering, a patch or two
    /// where the bark has fallen off and the paler wood shows, and at the near end the CUT FACE with its growth rings
    /// round the black hollow. The catfish's lesson rules the colours (round forty-four): on the deep bed the depth rule
    /// keeps a fraction of every channel and the murk mixes most of the water in, so a flat colour lands on the silt's;
    /// what reads is CONTRAST inside the thing (a ridge lit 1.3 times against a fissure at 0.35) and a smaller share of
    /// the haze (0.8 of the murk, the catfish's own). Lit by the terrain's slope toward the sun exactly as the stones
    /// and the bed are (the half cylinder's own slope shades it round), then deepened by the murk at its depth as every
    /// fish is (Body.Deepen). Everything is dealt from the log's own angle and position (no dice).
    /// </summary>
    private static void LogColour(in SunkenLog log, float[] terrain, int band, uint[] stoneCol, byte[] stoneA, bool[] isStone, bool[] hollowMask, int w, int h, float u)
    {
        float hw = log.Width * 0.5f, reach = log.Length * 0.5f + hw + 2;
        float ca = MathF.Cos(log.Angle), sa = MathF.Sin(log.Angle), halfLen = log.Length * 0.5f - hw;
        float endSign = log.NearEnd > 0.5f ? 1 : -1;
        float mx = log.Centre.X + ca * endSign * halfLen, my = log.Centre.Y + sa * endSign * halfLen;   // the near end's disc centre
        int x0 = Math.Max(0, (int)(log.Centre.X - reach)), x1 = Math.Min(w - 1, (int)(log.Centre.X + reach));
        int y0 = Math.Max(0, (int)(log.Centre.Y - reach)), y1 = Math.Min(h - 1, (int)(log.Centre.Y + reach));
        float Z(int x, int y) => terrain[(Math.Clamp(y, 0, h - 1) + band) * w + Math.Clamp(x, 0, w - 1)];
        float zPx = Pond.DepthU * u;
        // The wood's colours before the water: a dark red-brown bark, its lit ridges toward a warm grey-brown, the bare
        // wood where the bark is gone a pale tan, the cut face's sapwood paler still with darker heartwood rings. These
        // are what the depth rule FILTERS; the drawn colours are read off a render (kit pond46), never guessed.
        const float barkR = 96, barkG = 70, barkB = 46;
        const float bareR = 168, bareG = 138, bareB = 92;
        const float faceR = 182, faceG = 150, faceB = 100;
        // Two bare patches (the bark fallen away) and a knot, dealt from the angle (no dice): where along, which side.
        float p1 = -halfLen * 0.45f + halfLen * 0.25f * MathF.Sin(log.Angle * 7), p1side = 0.4f * MathF.Sin(log.Angle * 3);
        float p2 = halfLen * 0.35f + halfLen * 0.25f * MathF.Cos(log.Angle * 5), p2side = -0.35f * MathF.Cos(log.Angle * 9);
        float knotAlong = halfLen * 0.05f * MathF.Cos(log.Angle * 11), knotSide = 0.3f * MathF.Sin(log.Angle * 13);
        float phase = log.Angle * 4;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - log.Centre.X, dy = y + 0.5f - log.Centre.Y;
                float alongRaw = dx * ca + dy * sa, along = Math.Clamp(alongRaw, -halfLen, halfLen);
                float ex = dx - along * ca, ey = dy - along * sa, d = MathF.Sqrt(ex * ex + ey * ey);
                float hwHere = log.HalfWidthAt(along);
                if (d >= hwHere) continue;
                float across = (-dx * sa + dy * ca) / hw;                                               // signed, -1 to 1 across the trunk
                // THE THROAT (round forty-six): from the sawn face's hole out to the capsule's rounded tip the hollow is open, a
                // dark channel 0.6 of the half width wide, so a resident's head in the hole is not under the tip's wood.
                bool throat = alongRaw * endSign > halfLen - 0.02f * hw && MathF.Abs(across) < 0.6f;
                // The half cylinder's shading from the terrain's slope (the bed's rule).
                float gx = (Z(x + 2, y) - Z(x - 2, y)) * zPx / 4f, gy = (Z(x, y + 2) - Z(x, y - 2)) * zPx / 4f;
                float lit = 1 - Math.Clamp(gx * 0.6f + gy * 0.8f, -0.4f, 0.4f);
                lit *= 0.74f + 0.3f * MathF.Sqrt(MathF.Max(0f, 1f - across * across));                 // round in section: lit along the top, darker down the sides (the terrain's slope alone was too soft to say "cylinder")
                // THE BARK. Ridges run along the trunk: a wave across it (about eleven ridges over the width), its phase
                // wandering slowly along the length so no ridge is a ruled line, and broken now and then by a cross crack.
                // The wave is sharpened: most of the surface is ridge (lit), the fissure between is narrow and dark.
                float wander = 0.9f * MathF.Sin(along / hw * 0.55f + phase) + 0.5f * MathF.Sin(along / hw * 1.7f + phase * 2.1f);
                // Each ridge is warped on its own (a term that varies ACROSS as well as along, so no two ridges wobble
                // alike: all wobbling together they were a barcode), and the grooves fade in and out along the trunk
                // like the plates of real bark, so no groove is a line from end to end.
                float warp = 1.3f * MathF.Sin(across * 3.7f + along / hw * 0.9f + phase) + 0.6f * MathF.Sin(across * 6.1f - along / hw * 1.4f);
                float ridge = MathF.Sin(across * 9f + wander + warp);
                float plate = 0.55f + 0.45f * MathF.Sin(along / hw * 1.25f + across * 4.5f + phase * 1.7f);   // how deep the groove is here
                float fissure = Math.Clamp((0.3f - ridge) / 0.5f, 0, 1) * plate;                           // 1 in the groove, 0 on the ridge's top
                fissure = fissure * fissure;
                float crack = MathF.Sin(along / hw * 2.3f + across * 1.5f + phase * 3f);                  // a cross crack where this sine is near its top
                crack = Math.Clamp((crack - 0.93f) / 0.07f, 0, 1);
                float r = barkR, g = barkG, b = barkB;
                float shade = 1.2f - 0.75f * fissure - 0.4f * crack;                                      // ridge 1.2, groove 0.45, a crack darker still (1.3 and 0.35 on the first render: crisp and dark, "doesn't look like it's under water")
                // Bare wood where the bark has fallen off: two ovals, soft edged, paler and smoother (a faint grain only).
                float a1 = (along - p1) / (hw * 1.6f), s1 = (across - p1side) / 0.38f;
                float a2 = (along - p2) / (hw * 1.2f), s2 = (across - p2side) / 0.3f;
                float bare = MathF.Max(1 - (a1 * a1 + s1 * s1), 1 - (a2 * a2 + s2 * s2));
                if (bare > 0)
                {
                    bare = Math.Clamp(bare * 2.5f, 0, 1);
                    float grain = 1 + 0.08f * MathF.Sin(across * 9f + along / hw * 0.4f);
                    r += (bareR * grain - r) * bare; g += (bareG * grain - g) * bare; b += (bareB * grain - b) * bare;
                    shade += (1f - shade) * bare;                                                           // the bark's ridges end at the patch's edge
                }
                // A knot: a dark oval with a pale ring, where a branch was.
                float ka = (along - knotAlong) / (hw * 0.5f), ks = (across - knotSide) / 0.22f, knot = 1 - (ka * ka + ks * ks);
                if (knot > 0) shade *= 1 - 0.5f * Math.Clamp(knot * 3, 0, 1) + 0.35f * Math.Clamp((knot - 0.05f) * 6, 0, 1) * Math.Clamp((0.4f - knot) * 6, 0, 1);
                // THE CUT FACE at the near end: inside the end's disc the bark gives way to sawn wood, pale, with the
                // growth rings as a slow ripple from the middle out (darker every few rings), and the hollow in the
                // middle, black, where the heart has rotted away (the den). The disc is the log's width across.
                // The end, seen from above, is a foreshortened ELLIPSE: the log lies flat, so its sawn face is squashed along the axis to
                // 0.42 of its width (drawn as a full disc it read as a pipe pointed at the eye, "the 3d look is off").
                float ddx = x + 0.5f - mx, ddy = y + 0.5f - my;
                float eAlong = (ddx * ca + ddy * sa) / 0.42f, eAcross = -ddx * sa + ddy * ca;
                float dEnd = MathF.Sqrt(eAlong * eAlong + eAcross * eAcross) / hw;
                if (dEnd < 0.95f)
                {
                    float face = Math.Clamp((0.95f - dEnd) / 0.12f, 0, 1);                                // a soft edge where the bark meets the face
                    float rings = 1 + 0.14f * MathF.Sin(dEnd * 26f + phase) - 0.08f * Math.Clamp(MathF.Sin(dEnd * 9f), 0, 1);
                    float hollow = MathF.Max(throat ? 1f : 0f, Math.Clamp((0.62f - dEnd) / 0.14f, 0, 1));   // 1 in the hole (and its throat), 0 on the wood
                    if (hollow > 0.5f) hollowMask[y * w + x] = true;
                    float fr = faceR * rings, fg = faceG * rings, fb = faceB * rings;
                    // LIGHT IN THE HOLE: the sun comes from up and left (Light.ShadowX, ShadowY run down and right), so of the hole's
                    // inner wall the part on the down-right side of the opening is what the sun reaches, pale wood in half light
                    // near the rim, falling to black toward the middle and the up-left rim, where the wall is in its own shadow.
                    float ux = ddx / (0.62f * hw * 0.42f), uy = ddy / (0.62f * hw);
                    float toward = Math.Clamp(ux * 0.6f + uy * 0.8f, 0f, 1f), rimness = Math.Clamp(MathF.Sqrt(ux * ux + uy * uy), 0f, 1f);
                    float wall = toward * toward * rimness * rimness * 0.7f;
                    fr = fr * (1 - 0.95f * hollow) + faceR * 0.55f * wall * hollow; fg = fg * (1 - 0.95f * hollow) + faceG * 0.5f * wall * hollow; fb = fb * (1 - 0.93f * hollow) + faceB * 0.45f * wall * hollow;
                    r += (fr - r) * face; g += (fg - g) * face; b += (fb - b) * face;
                    shade += (1f - shade) * face;
                }
                if (throat && dEnd >= 0.95f)
                {
                    // The throat beyond the face: the hole's shadow spilling out to the tip, deepest at the face and gone by the tip
                    // (a flat black slot read as a notch cut in the end).
                    float spill = Math.Clamp(1f - (alongRaw * endSign - halfLen) / hw, 0f, 1f);
                    float keep = 0.12f + 0.88f * (1f - spill) * (1f - spill);
                    r *= keep; g *= keep; b *= keep * 1.05f; shade = 1f - 0.5f * spill; hollowMask[y * w + x] = true;
                }
                lit *= shade;
                int ir = (int)(r * lit), ig = (int)(g * lit), ib = (int)(b * lit);
                float depth = Z(x, y), murk = Light.Murk(depth);                                             // the full haze, the stones' rule (0.8 of it on the first render read as pasted on)
                Body.Deepen(ref ir, ref ig, ref ib, (int)(murk * 256));
                int i = y * w + x;
                stoneCol[i] = (uint)(Math.Clamp(ir, 0, 255) << 16 | Math.Clamp(ig, 0, 255) << 8 | Math.Clamp(ib, 0, 255));
                stoneA[i] = (byte)(Math.Clamp(1 - murk, 0, 1) * 255);
                isStone[i] = true;
            }
    }

    /// <summary>
    /// The bank rocks' feet, lowered into the terrain: a rock is painted as a disc at the surface, but under the water
    /// it widens out like the foot of a hill, so the bed comes up to meet its rim over a short slope (0.08u), from the
    /// terrain's depth that far out to nothing at the rim. The in-water caps are in the list too, and THEIR foot is a hair
    /// (round forty-six, "circle areas that surround the top of the rocks and has a hard boundary that the water stops at":
    /// with the stone's own mound cut to nothing, this 0.08u foot was the pale shelf of shallow water round every rock, found
    /// by the depth map's profile out from the cap, 0 to full depth over 0.9 of a cap radius in every direction). A cap is
    /// told from a bank rock by the water round it: a rim all in the pond is a rock standing in the water.
    /// </summary>
    private static void LowerRocks(float[] terrain, List<BankRock> rocks, bool[] inPond, int w, int h, int band, float u)
    {
        foreach (BankRock rock in rocks)
        {
            bool standing = true;
            for (int k = 0; k < 12 && standing; k++)
            {
                float a = k * MathF.Tau / 12;
                int sx = Math.Clamp((int)(rock.Centre.X + MathF.Cos(a) * rock.Radius * 1.15f), 0, w - 1), sy = Math.Clamp((int)(rock.Centre.Y + MathF.Sin(a) * rock.Radius * 1.15f), 0, h - 1);
                standing = inPond[sy * w + sx];
            }
            if (standing) continue;                                                     // (a hair of foot still forced zero depth out to the cap's full circle: a pale rim round the irregular drawn rock)
            float flank = u * 0.08f;
            int x0 = Math.Max(0, (int)(rock.Centre.X - rock.Radius - flank)), x1 = Math.Min(w - 1, (int)(rock.Centre.X + rock.Radius + flank));
            int y0 = Math.Max(0, (int)(rock.Centre.Y - rock.Radius - flank)), y1 = Math.Min(h - 1, (int)(rock.Centre.Y + rock.Radius + flank));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * w + x;
                    if (!inPond[i]) continue;
                    float dx = x + 0.5f - rock.Centre.X, dy = y + 0.5f - rock.Centre.Y;
                    float d = (MathF.Sqrt(dx * dx + dy * dy) - rock.Radius) / flank;
                    if (d >= 1) continue;
                    float f = d <= 0 ? 0 : d * d * (3 - 2 * d);
                    int k = (y + band) * w + x;
                    if (f < terrain[k]) terrain[k] = f;
                }
        }
    }

    /// <summary>
    /// What a stone's flank looks like at every GROUND pixel of its footprint, for SinkBed to draw sunk along with the
    /// bed; and the cap finished in place. The stone and its cap were painted whole, as if they stood on dry land
    /// (ShorePainter.IslandStone and IslandCap); this reads that painting and leaves nothing of the stone itself on the
    /// bitmap (SinkBed draws every water pixel afresh): only the cap stays, dry, nearest the eye of all (Pond.Near 255),
    /// with a wet band just inside its rim, which is the bank rocks' own wet flank (WrapWater): mixed toward the water
    /// darkened by a quarter, fully at the rim (until 2026-10-23 the cap was only dimmed a little there, so its rim
    /// stayed a crisp dry edge against the stone going under, and the cap read as a rock pasted on top of a smudge).
    ///
    /// THE FLANK'S COLOUR (round forty, "take a hard look at the rocks ... looked cut off"; round forty-one,
    /// "transparent rocks ... you can see right through them"): the stone used to be the flat rock painting (a dry
    /// rock's lit face, shaded face, creases and grain) sunk row by row, and sinking stretches the lower flank three
    /// times over: the grain became vertical streaks, a crease a long diagonal seam, the two faces a hard line across
    /// the flank. The submerged stone is a SMOOTH MOUND: the painting's AVERAGE colour (so each seed keeps its stone's
    /// own grey-green) brought to a mid grey (a wet boulder under the water), lit by the terrain's slope toward the sun
    /// (up and left, the bed's rule) and darkened toward its silhouette the way a cylinder is (lit by the sun's side
    /// alone the column was a flat grey slab, "tombstones"), then deepened by the murk at its depth exactly as a koi is
    /// (Body.Deepen: darker and bluer per channel, never paler). How solid it is (the alpha SinkBed mixes it toward the
    /// water around it by) is one less the murk at its depth (Light.Murk, the fish's own veil), and its foot dissolves
    /// over the last 0.35 of the radius in from the painted edge, so the painting ends in water on every side. A fade
    /// of its own that reached nothing by 0.42 of the depth made it glass, with the bed's pebbles through it. A dark
    /// line runs along the cap's rim, outside it: where the surface meets the stone.
    /// </summary>
    /// <param name="before">The picture before any stone was painted: the water under each cap, for the wet band.</param>
    /// <param name="collar">Pixels out from the cap over which the stone is still at the surface.</param>
    /// <param name="stoneCol">Filled: the flank's colour at each ground pixel of the footprint.</param>
    /// <param name="stoneA">Filled: how solid the flank is there, 0 to 255.</param>
    /// <param name="isStone">Filled: true at each ground pixel of the footprint that is flank (not cap).</param>
    /// <param name="near">Filled: 255 on the cap (Pond.Near: the dry top is nearest the eye of all).</param>
    private static void StoneColour(Bitmap bmp, uint[] before, bool[] stone, bool[] cap, Island isle, float[] terrain, int band, float collar, uint[] stoneCol, byte[] stoneA, bool[] isStone, byte[] near, int w, int h, float u)
    {
        uint[] px = Read(bmp);                                                          // the stone and its cap, painted whole, each at its place on the ground
        float bandPx = u * 0.012f * 10;
        float reach = isle.Radius + u * 0.1f;
        int x0 = Math.Max(0, (int)(isle.Centre.X - reach)), x1 = Math.Min(w - 1, (int)(isle.Centre.X + reach));
        int y0 = Math.Max(0, (int)(isle.Centre.Y - reach)), y1 = Math.Min(h - 1, (int)(isle.Centre.Y + reach));
        int[] inCap = DistanceToEdge(cap, w, h, false, x0, y0, x1, y1);              // tenths of a pixel in from the cap's rim, for the wet band
        // NO PAINTED FLANK (round forty-six, his line: "i have no idea what you're trying to achieve with this rock thing ... just make
        // it a rock and have the water go up to it instead of having a bunch of complicated stuff surrounding those rock caps"):
        // the dry rock is the rock, the water meets it at its wet rim, and what is under the water is the bed rising to it, drawn
        // by SinkBed like any slope (steep, so it takes no litter), the net over it. The smooth grey mound this function painted
        // from round forty to forty-five (the painting's average brought to mid grey, lit like a cylinder, dissolving at its foot)
        // was the "plateau" between the water and the rock. The dry top is nearest the eye of all (Pond.Near 255).
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (cap[y * w + x]) near[y * w + x] = 255;
        // The cap, in place: its wet band, written straight into the picture (the cap is a blocker, so SinkBed leaves it).
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                if (!cap[i]) continue;
                float wet = Math.Clamp(1 - inCap[i] / bandPx, 0, 1) * 0.6f;   // (0.85 toward six tenths of the water until round forty: a near-black ring round every cap, an outline by another name)
                if (wet <= 0) continue;
                uint c = px[i], o = before[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                int or_ = (int)((o >> 16) & 0xFF), og = (int)((o >> 8) & 0xFF), ob = (int)(o & 0xFF);
                r += (int)((or_ * 0.75f - r) * wet); g += (int)((og * 0.76f - g) * wet); b += (int)((ob * 0.78f - b) * wet);
                px[i] = (uint)(r << 16 | g << 8 | b);
            }
        Write(bmp, px);
    }

    /// <summary>
    /// THE BED, DRAWN SUNK (round forty-one, the review's step 3): one fill draws every ground pixel of the pond's bed,
    /// and of the stones' flanks, down the screen by its depth, exactly as every swimmer is drawn (Light.SinkPx). Until
    /// this the bed's humps, hollows, light and the slope up to the bank were painted at their place on the ground,
    /// flat, while the stones, the koi, the turtle and the deep dwellers over it were drawn lower by their depth: a
    /// crayfish standing exactly on the bed at its own spot stood, in the picture, on bed that was 0.22 to 0.37 of the
    /// pond's depth away from it ("the bed is drawn straight down; everything on it is drawn slanted", the review's F3),
    /// and the swimmers' bed shadows landed on bed that was not under them.
    ///
    /// FEYNMAN VERSION: look down into a real pond from the bank. The far, deep bottom is not straight under the
    /// surface you see it through; it sits lower in your view than its spot on the map, the deeper the lower, which is
    /// why the deep middle looks pushed away from you and the near shallows look pulled up. Every fish here has been
    /// drawn with that push since round thirty-eight; now the ground under them is too.
    ///
    /// How: each ground pixel's colour is worked out first (the bed through the water, WaterOver, lit by the terrain's
    /// tilt toward the sun as the bed has been since round forty; or a stone's flank from StoneColour, mixed toward
    /// the bed's water around it with the pebbles blurred out, 0.03u, so a boulder covers the bed and is not glass).
    /// Then each ground pixel fills the screen rows from its own sunk place to the next ground row's, so the stretch
    /// leaves no gaps, the colour blended between the two rows (filled with one row's colour the stretch showed as
    /// stairs), and at any screen pixel the NEAREST (shallowest) point of the bed wins (zbuf): the near side of a hump
    /// hides the far side, and the bank's shallow slope, which rises toward the eye, hides the deep bed behind it and
    /// is drawn at about a fifth of its ground width under the pond's slant (Light.Sink). The band of ground above
    /// the screen (Terrain) comes down into the top of the view. Pond.Near takes the drawn depth of every bed pixel,
    /// so what the painters hide behind a stone they now hide behind a hump too. Land and blockers (the caps, the bank
    /// rocks, the log) keep their pixels; any water pixel nothing lands on keeps the flat pour (none, in the renders).
    /// Over the sunk water: the floating sheet (the murk patches and the drifted petals, by their alpha), then whatever
    /// the land painters put on the water (a bank rock's wet rim, an afloat log) at its own place: every pixel where the
    /// picture before the stones differed from the flat pour.
    /// </summary>
    private static void SinkBed(Bitmap bmp, uint[] flat, uint[] beforeStones, uint[] surface, float[] terrain, int band, uint[] bedPx, uint[] stoneCol, byte[] stoneA, bool[] isStone, bool[] hollow, bool[] inPond, uint[] blocked, byte[] near, int w, int h, float u)
    {
        int H = h + band;
        float sinkMax = Light.SinkPx(1, u);                                            // how far down the screen the deepest bed is drawn
        float zPx = Pond.DepthU * u;                                                   // pixels of depth per unit of Z, for the slope
        int s = Math.Max(2, (int)(u * 0.012f));
        // The lighting is read from the terrain smoothed over the stencil's width (twice): read straight off the map
        // a slope over a short stencil jumps at every step of the byte it used to be, and the humps came out as
        // contour rings on the first run.
        float[] smooth = Blur(terrain, w, H, s);
        // ---- 1. the colour of every ground pixel ----
        var col = new uint[H * w];
        var has = new bool[H * w];
        for (int gy = -band; gy < h; gy++)
        {
            int row0 = Math.Max(0, gy) * w, ky = gy + band;
            int yu = Math.Max(0, ky - s), yd = Math.Min(H - 1, ky + s);
            int bedRow = (gy < 0 ? Math.Min(h - 1, -gy - 1) : gy) * w;                  // the bed above the top edge is the top rows mirrored (no pebbles show at that depth; mirrored, no streaks either)
            for (int x = 0; x < w; x++)
            {
                if (!inPond[row0 + x]) continue;
                int k = ky * w + x;
                float z = terrain[k];
                has[k] = true;
                int sy = gy + (int)(z * sinkMax);                                          // where it lands, for the sky's share
                uint c = WaterOver(bedPx[bedRow + x], z, BedClear(z), sy, h, 1);
                // Lit by the bed's own tilt (round forty, "i need this to be a 3d terrain"): the flank of a hump that
                // faces the sun (up and left: its depth FALLS going that way) is brightened, the flank facing away is
                // dimmed, by up to a third, over a stencil a hundredth of u wide. The bank's own slope keeps its painted
                // pale look: the light comes in from a quarter of the depth to full at seven tenths, the open pond.
                {
                    int xl = Math.Max(0, x - s), xr = Math.Min(w - 1, x + s);
                    float gx = (smooth[ky * w + xr] - smooth[ky * w + xl]) * zPx / (xr - xl);   // how fast the bed deepens going right (u per u)
                    float gyy = (smooth[yd * w + x] - smooth[yu * w + x]) * zPx / (yd - yu);    // and going down the screen
                    float open = Math.Clamp((z - 0.25f) / 0.45f, 0f, 1f);
                    if (open > 0)
                    {
                        open = open * open * (3 - 2 * open);
                        float tilt = -Math.Clamp(gx * 0.6f + gyy * 0.8f, -0.12f, 0.12f);   // (a third until round forty-four, a quarter until round forty-five: lit that hard, with the slant stretching the flanks, the slopes were pale diagonal streaks he read as light rays)
                        int light = 256 + (int)(256 * tilt * open);
                        if (light != 256)
                        {
                            int r = Math.Min(255, (int)((c >> 16) & 0xFF) * light >> 8), g = Math.Min(255, (int)((c >> 8) & 0xFF) * light >> 8), b = Math.Min(255, (int)(c & 0xFF) * light >> 8);
                            c = (uint)(r << 16 | g << 8 | b);
                        }
                    }
                    // A CLIFF IS ROCK (round forty-six): where the bed falls more than about a pixel of depth per pixel of ground it is
                    // a rock's underwater side, dark wet stone, not shallow water over sand. Drawn sunk down the screen, the rock's
                    // near face coloured as shallow water was a pale tongue below every rock. The dark deepens with the depth.
                    float steep = Math.Clamp((MathF.Abs(gx) + MathF.Abs(gyy)) / 14f - 0.5f, 0f, 1f);
                    if (steep > 0)
                    {
                        int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                        float dim = 0.55f - 0.25f * z;
                        r += (int)((52 * dim * 1.15f - r) * steep); g += (int)((62 * dim * 1.15f - g) * steep); b += (int)((54 * dim * 1.15f - b) * steep);
                        c = (uint)(Math.Clamp(r, 0, 255) << 16 | Math.Clamp(g, 0, 255) << 8 | Math.Clamp(b, 0, 255));
                    }
                }
                col[k] = c;
            }
        }
        // The stones' flanks, mixed toward the bed's water round them with the pebbles averaged out (blurred over 0.03u):
        // mixed toward the pixel it covered, a flank showed every pebble through itself, a pale ghost over the gravel.
        int sx0 = w, sx1 = -1, sy0 = h, sy1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (isStone[y * w + x]) { sx0 = Math.Min(sx0, x); sx1 = Math.Max(sx1, x); sy0 = Math.Min(sy0, y); sy1 = Math.Max(sy1, y); }
        if (sx1 >= sx0)
        {
            int by0 = sy0 + band, by1 = sy1 + band, bw = sx1 - sx0 + 1;
            (float[] wR, float[] wG, float[] wB) = BlurBox(col, w, sx0, by0, sx1, by1, Math.Max(2, (int)(u * 0.03f)));
            for (int y = sy0; y <= sy1; y++)
                for (int x = sx0; x <= sx1; x++)
                {
                    int i = y * w + x;
                    if (!isStone[i]) continue;
                    int k = (y + band) * w + x, kz = (y - sy0) * bw + (x - sx0);
                    float a = stoneA[i] / 255f;
                    uint sc = stoneCol[i], sharp = col[k];
                    // The water the flank is mixed toward: the SHARP bed where the flank is thin (its dissolving foot, so the litter
                    // runs on under it: mixed toward the blurred water there, the cobbles stopped dead at a line, "cut off early"),
                    // the blurred water where it is solid (there the blur keeps the pebbles from showing through the stone).
                    float wr = ((sharp >> 16) & 0xFF) + (wR[kz] - ((sharp >> 16) & 0xFF)) * a, wg = ((sharp >> 8) & 0xFF) + (wG[kz] - ((sharp >> 8) & 0xFF)) * a, wb = (sharp & 0xFF) + (wB[kz] - (sharp & 0xFF)) * a;
                    int r = (int)(wr + (((sc >> 16) & 0xFF) - wr) * a), g = (int)(wg + (((sc >> 8) & 0xFF) - wg) * a), b = (int)(wb + ((sc & 0xFF) - wb) * a);
                    col[k] = (uint)(Math.Clamp(r, 0, 255) << 16 | Math.Clamp(g, 0, 255) << 8 | Math.Clamp(b, 0, 255));
                }
        }
        // ---- 2. the fill: each ground pixel down the screen by its depth, the nearest winning ----
        uint[] outPx = Read(bmp);
        var water = new bool[w * h];                                                   // where the sunk bed may be drawn: open water, no blocker (the caps, the bank rocks, the log keep their pixels)
        for (int i = 0; i < water.Length; i++)
        {
            water[i] = inPond[i] && (blocked[i] & 0xFF) < 128;
            if (water[i]) outPx[i] = flat[i];
        }
        var zbuf = new float[w * h];
        Array.Fill(zbuf, 2f);
        for (int gy = -band; gy < h; gy++)
        {
            int ky = gy + band;
            for (int x = 0; x < w; x++)
            {
                int k = ky * w + x;
                if (!has[k]) continue;
                int kn = gy + 1 < h && has[k + w] ? k + w : k;                             // the ground row below, where the bed goes on
                float z = terrain[k], zn = terrain[kn];
                int s0 = gy + (int)(z * sinkMax), sN = kn == k ? s0 : gy + 1 + (int)(zn * sinkMax);
                // This pixel owns the screen rows from its own sunk place up to (not including) the next row's: below it
                // where the bed deepens going down the screen, ABOVE it where the bed rises toward the eye faster than the
                // sink shrinks (the far flank of a mound), which left a row of flat water between the two on the first run.
                int lo = Math.Min(s0, sN), hi = Math.Max(s0, sN);
                if (sN > s0) hi--; else if (sN < s0) lo++;
                for (int sy = Math.Max(0, lo); sy <= Math.Min(h - 1, hi); sy++)
                {
                    int j = sy * w + x;
                    if (!water[j]) continue;
                    float f = sN != s0 ? (sy - s0) / (float)(sN - s0) : 0;
                    float depth = z + (zn - z) * f;
                    if (depth >= zbuf[j]) continue;
                    zbuf[j] = gy >= 0 && hollow[gy * w + x] ? depth + 0.35f : depth;              // the log's hollow is a HOLE: nothing in it is nearer than its floor, so a head in it is drawn (round forty-six)
                    uint a = col[k], b = col[kn];
                    int rr = (int)(((a >> 16) & 0xFF) + ((int)((b >> 16) & 0xFF) - (int)((a >> 16) & 0xFF)) * f);
                    int gg = (int)(((a >> 8) & 0xFF) + ((int)((b >> 8) & 0xFF) - (int)((a >> 8) & 0xFF)) * f);
                    int bb = (int)((a & 0xFF) + ((int)(b & 0xFF) - (int)(a & 0xFF)) * f);
                    outPx[j] = (uint)(rr << 16 | gg << 8 | bb);
                }
            }
        }
        // ---- 3. what the painters test against (Pond.Near): the bed's drawn depth as nearness, the nearer winning ----
        for (int j = 0; j < zbuf.Length; j++)
        {
            if (zbuf[j] > 1.5f) continue;                                               // nothing of the bed drawn here
            int n = Math.Clamp((int)(255 - zbuf[j] * 250), 1, 255);
            if (n > near[j]) near[j] = (byte)n;
        }
        // ---- 4. what floats, back on top at its own place ----
        for (int i = 0; i < water.Length; i++)
        {
            if (!water[i]) continue;
            uint sc = surface[i], a = sc >> 24;
            if (a > 0)
            {
                uint bg = outPx[i], keep = 255 - a;
                uint rr = (((bg >> 16) & 0xFF) * keep + ((sc >> 16) & 0xFF) * a) / 255, gg = (((bg >> 8) & 0xFF) * keep + ((sc >> 8) & 0xFF) * a) / 255, bb = ((bg & 0xFF) * keep + (sc & 0xFF) * a) / 255;
                outPx[i] = rr << 16 | gg << 8 | bb;
            }
            if (beforeStones[i] != flat[i]) outPx[i] = beforeStones[i];               // a bank rock's wet rim, an afloat log: the land painters' work on the water
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
