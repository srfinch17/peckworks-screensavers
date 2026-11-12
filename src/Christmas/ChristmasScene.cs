using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;
using Christmas.Happenings;
using Christmas.Stories;

namespace Christmas;

/// <summary>
/// Everything a story may need to reach: the scenery's facts, the
/// noticeboard, Santa, and each resident (so a story can BORROW one: the
/// mice for the tree trimming, the snowman for the carrot heist). One bag,
/// handed to every story's constructor.
/// </summary>
internal sealed class ChristmasWorld
{
    public required ChristmasScenery Scenery { get; init; }
    public required Noticeboard Board { get; init; }
    public required SantaRig Santa { get; init; }
    public required ReindeerHerd Reindeer { get; init; }
    public required FoxAndHares Field { get; init; }
    public required YardSnowman Snowman { get; init; }
    public required FeederBirds Birds { get; init; }
    public required LogMice Mice { get; init; }
    public required Otters Otters { get; init; }
    public required Squirrels Squirrels { get; init; }
}

/// <summary>
/// Christmas Eve at a cabin in the north woods: snow falling, pastel lights
/// twinkling in the pines, a village glowing down in the valley, Santa's
/// sleigh crossing the sky with Rudolph out front, and a snowfield full of
/// animals getting on with their night.
///
/// The same theater as Sakura and Thanksgiving: a BACKDROP painted once
/// (ChristmasPainter.cs) and ACTORS drawn over it every frame:
///
///   - The SNOW is the shared petal engine (Core/Sakura/PetalField.cs) told
///     to draw soft white dots instead of petals. Same fall, sway and depth.
///   - The LIGHTS: the backdrop has every bulb painted as a small dull dot.
///     Each frame a soft glow of the bulb's color is stamped over it,
///     stronger or weaker on each bulb's own slow rhythm: the twinkle. The
///     brighter stars, the village's windows, the cabin's windows, the lamp
///     post and the glitter on the snow are the same trick.
///   - The SMOKE from the cabin's chimney (Core/ChimneySmoke.cs).
///   - SANTA (SantaRig.cs): a flip-book of the sleigh sliding across the
///     sky, behind the corner branches.
///   - The RESIDENTS (Resident.cs): the reindeer, the fox and the hares,
///     the snowman, the birds at the table, the mice in the log, the otters
///     on the pond. Each has a mind (Core/Minds) and reads the NOTICEBOARD,
///     where everything that happens is pinned, so they react to each
///     other, to the happenings and to Santa going over. They are drawn far
///     to near through one queue (DepthQueue), each hidden by the props
///     nearer than its feet.
///   - The STORIES (Stories folder): little scenes of half a minute, dealt
///     one every minute or so by the engine's StoryDirector.
///   - The HAPPENINGS: small surprises of a few seconds (fireworks, the
///     northern lights, an owl), dealt by the engine's HappeningDirector.
/// </summary>
internal sealed class ChristmasScene : IScreensaverScene
{
    /// <summary>One bulb's twinkle: where the bulb is, which color, and its own rhythm.</summary>
    private struct Light
    {
        public float X, Y;
        public int Color;
        public float Phase, Speed;
        public byte On;          // what the bulb hangs on: 0 = a thing that stands still (the cabin), 1 = a pine, 2 = a corner branch. A bulb moves with what it hangs on.
    }

    /// <summary>
    /// THE SNOW TAKES THE COLOUR OF A LIGHT IT FALLS PAST (his idea, 2026-11-11: "the snow that fell near a light took the lights color in
    /// reflection as it goes past and then goes back to whatever color it was when it gets far enough away").
    ///
    /// The bulbs never move, so the question "what bulb light falls HERE?" has the same answer every frame. So it is answered ONCE, for every
    /// small square of the screen, and kept as a map: like a weather map of colour. Each bulb stamps a soft disc on the map, strongest at the
    /// bulb and fading to nothing at its reach. A flake then only looks up the square it is in (one lookup, not a search through 500 bulbs).
    /// Where two bulbs' discs overlap the square holds their mix, weighted by which is nearer.
    ///
    /// The bulbs' pastels are pale, and a white flake mixed toward pale pink still looks white. So the light's colour is the pastel pushed
    /// AWAY from white by the same distance again (pink becomes rose): the flake reads as coloured at a glance.
    /// </summary>
    private void BuildSnowLight(int width, int height)
    {
        _slCell = Math.Max(2, (int)(_u / 270f));
        _slW = width / _slCell + 1; _slH = height / _slCell + 1;
        int n = _slW * _slH;
        float[] sr = new float[n], sg = new float[n], sb = new float[n], sw = new float[n];
        _slK = new float[n];
        for (int li = 0; li < _lights.Length; li++)
        {
            ref readonly Light l = ref _lights[li];
            bool pine = li >= _pineFirst && li < _pineEnd;
            float reach = (li >= _branchFirst ? _u * BranchBulbU : pine ? _scenery.PineU * ChristmasPainter.PineBulbK : _u) * 0.035f;       // how far a bulb's colour carries: a big pine bulb about 80 pixels at 1080p, a small one about 40
            Color c = ChristmasPainter.Pastels[l.Color];
            float cr = Math.Max(0, 2 * c.R - 255), cg = Math.Max(0, 2 * c.G - 255), cb = Math.Max(0, 2 * c.B - 255);   // the pastel pushed away from white
            int x0 = Math.Max(0, (int)((l.X - reach) / _slCell)), x1 = Math.Min(_slW - 1, (int)((l.X + reach) / _slCell));
            int y0 = Math.Max(0, (int)((l.Y - reach) / _slCell)), y1 = Math.Min(_slH - 1, (int)((l.Y + reach) / _slCell));
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    float dx = (cx + 0.5f) * _slCell - l.X, dy = (cy + 0.5f) * _slCell - l.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d >= reach) continue;
                    float wt = (1f - d / reach) * (1f - d / reach);      // 1 at the bulb, easing to 0 at the reach
                    int i = cy * _slW + cx;
                    sr[i] += cr * wt; sg[i] += cg * wt; sb[i] += cb * wt; sw[i] += wt;
                    if (wt > _slK[i]) _slK[i] = wt;                      // the strength is the NEAREST bulb's, so ten bulbs together are not ten times as strong
                }
        }
        _slR = new byte[n]; _slG = new byte[n]; _slB = new byte[n];
        for (int i = 0; i < n; i++)
        {
            if (sw[i] <= 0f) continue;
            _slR[i] = (byte)(sr[i] / sw[i]); _slG[i] = (byte)(sg[i] / sw[i]); _slB[i] = (byte)(sb[i] / sw[i]);
            _slK[i] *= 0.9f;
        }
    }

    // ================================================================ THE WIND IN THE TREES
    //
    // ONE WIND. The snow already has a breeze that rises and dies (PetalField.Breeze): the flakes stream sideways in a gust and fall
    // straight in a lull. The pines and the corner branches read that SAME number, so when the snow streams the trees bend, and when it
    // falls straight they stand. Two winds that disagree would look like the snow is on a different pane of glass from the trees.
    //
    // HOW A PAINTED TREE MOVES. The pines and the branches are each on their own clear sheet (ChristmasPainter made them). Every frame:
    //   - THE PINES: each ROW of the sheet is slid sideways, a little more the higher the row (ChristmasScenery.PineDx). Slide the rows of
    //     a deck of cards and the deck leans: the same thing.
    //   - THE BRANCHES: each COLUMN of the sheet is lifted or dropped (ChristmasScenery.BranchDy), more the farther out along the bough,
    //     with the lift travelling outward as a wave: a bough bobbing.
    // HALF PIXELS MATTER. A row slid by 3.4 pixels is drawn as a mix of the picture slid by 3 and by 4 (60% and 40%). Without that mix
    // each row would jump a whole pixel at a time and the tree would crawl like a staircase; with it the bend is smooth however slow.

    private int _pineY0, _pineY1;                          // the rows of the screen the pines' sheet has anything on
    private int[] _pineXa = [], _pineXb = [];              // for each of those rows, and each grove (left = even, right = odd): the first and last column with pine on it
    private int _brY1, _brPad;                             // the last row the branches reach at rest, and how many rows of slack the bob needs
    private int[] _pineSeg = [], _pineSegs = [];           // for each row and grove, the SOLID stretches of pine (every pixel fully opaque, nothing nearer in front): row y, grove g's stretches are _pineSegs[_pineSeg[y * 2 + g] .. _pineSeg[y * 2 + g + 1]), in pairs (first column, last column). A solid stretch needs no see-through sums, so it is drawn by the quick loop; only the ragged edges take the slow one.
    private int[] _pineRag = [], _pineRags = [];           // ... and the RAGGED stretches (edges, twig tips: anything on the sheet that is not inside a solid stretch), stored the same way. The empty air between two pines is in neither list, so it is never looked at.
    private uint[] _brT = [];                              // the branches' sheet turned on its side (column after column, each _brH rows): a column is then one run of memory, read front to back
    private int _brH;
    private int[] _brCol = [], _brRuns = [];               // for each COLUMN, the stretches of rows with branch on them at rest: column x's stretches are _brRuns[_brCol[x] .. _brCol[x + 1]), in pairs (first row, last row). A column moves up and down as one piece, so the branches are drawn a column at a time and only where there is wood or bulb: nine tenths of their box is empty air and is never looked at.
    private readonly string? _windTest = Environment.GetEnvironmentVariable("PECKWORKS_WIND");   // test aid: nopines or nobranches leaves that sheet undrawn (to time the other), still holds the air still (to compare a picture with the old backdrop)
    private long _twP, _twB, _twN;                           // test aid (PECKWORKS_WIND=count): time spent drawing each sheet
    private double _swayPhase, _bobPhase;                  // the pines' small rocking and the boughs' bob: each its own wrapped angle (a screensaver runs for days)

    private void BuildWindSheets(int w, int h)
    {
        uint[] P = _scenery.PineLayer, B = _scenery.BranchLayer;
        _pineY0 = h; _pineY1 = -1;
        _pineXa = new int[h * 2]; _pineXb = new int[h * 2];
        for (int y = 0; y < h; y++)
        {
            int la = w, lb = -1, ra = w, rb = -1, half = w / 2;
            for (int x = 0; x < w; x++)
            {
                if ((P[y * w + x] >> 24) == 0) continue;
                if (x < half) { if (x < la) la = x; lb = x; } else { if (x < ra) ra = x; rb = x; }
            }
            _pineXa[y * 2] = la; _pineXb[y * 2] = lb; _pineXa[y * 2 + 1] = ra; _pineXb[y * 2 + 1] = rb;
            if (lb >= 0 || rb >= 0) { if (y < _pineY0) _pineY0 = y; _pineY1 = y; }
        }
        // The solid stretches of each row (see _pineSeg).
        int reach = (int)MathF.Ceiling(LeanMax * 1.2f * _u) + 3;
        bool[] open = _scenery.PineOpen;
        var segs = new List<int>(); _pineSeg = new int[h * 2 + 1];
        var rags = new List<int>(); _pineRag = new int[h * 2 + 1];
        var quick = new bool[w + 2];
        for (int y = 0; y < h; y++)
            for (int grove = 0; grove < 2; grove++)
            {
                _pineSeg[y * 2 + grove] = segs.Count; _pineRag[y * 2 + grove] = rags.Count;
                int a = _pineXa[y * 2 + grove], b = _pineXb[y * 2 + grove];
                if (b < 0) continue;
                Array.Clear(quick, a, b + 2 - a);
                int start = -1;
                for (int x = a; x <= b + 1; x++)
                {
                    bool solid = x <= b && (P[y * w + x] >> 24) == 255;
                    if (solid && start < 0) start = x;
                    else if (!solid && start >= 0)
                    {
                        // a stretch with a nearer prop anywhere within the lean's reach of it is left to the careful loop (the lamp post stands between two pines)
                        bool clear = x - start >= 4;
                        for (int q = Math.Max(0, start - reach); q <= Math.Min(w - 1, x - 1 + reach) && clear; q++) clear = open[y * w + q];
                        if (clear) { segs.Add(start); segs.Add(x - 1); for (int q = start + 1; q <= x - 1; q++) quick[q] = true; }      // (the quick loop draws a stretch from its SECOND pixel: the first still mixes with whatever is on its left)
                        start = -1;
                    }
                }
                // ragged: every column that has pine on it or just to its left (that neighbour's left-over part lands here) and is not the quick loop's
                start = -1;
                for (int x = Math.Max(1, a); x <= Math.Min(w - 1, b + 1) + 1; x++)
                {
                    bool rag = x <= Math.Min(w - 1, b + 1) && !quick[x] && ((P[y * w + x] | P[y * w + x - 1]) >> 24) != 0;
                    if (rag && start < 0) start = x;
                    else if (!rag && start >= 0) { rags.Add(start); rags.Add(x - 1); start = -1; }
                }
            }
        _pineRag[h * 2] = rags.Count; _pineRags = [.. rags];
        _pineSeg[h * 2] = segs.Count; _pineSegs = [.. segs];
        if (Environment.GetEnvironmentVariable("PECKWORKS_WIND") == "count") { long solid = 0, all = 0; for (int q = 0; q < segs.Count; q += 2) solid += segs[q + 1] - segs[q] + 1; foreach (uint c in P) if ((c >> 24) != 0) all++; Console.Error.WriteLine($"pines: {all} pixels, {solid} in solid stretches"); System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pinecount.txt"), $"pines: {all} pixels, {solid} in {segs.Count / 2} solid stretches"); }
        _brPad = (int)MathF.Ceiling(BobMax * _u) + 2;
        _brY1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) if ((B[y * w + x] >> 24) != 0) { _brY1 = y; break; }
        var runs = new List<int>(); _brCol = new int[w + 1];
        for (int x = 0; x < w; x++)
        {
            _brCol[x] = runs.Count;
            int start = -1, last = -9;
            for (int y = 0; y <= _brY1 + 1; y++)
            {
                bool on = y <= _brY1 && y < h && (B[y * w + x] >> 24) != 0;
                if (on) { if (start < 0) start = y; last = y; }
                else if (start >= 0 && y - last > 3) { runs.Add(start); runs.Add(last); start = -1; }       // (a gap of up to three rows is cheaper to draw through than to end a stretch for)
            }
            if (start >= 0) { runs.Add(start); runs.Add(last); }
        }
        _brCol[w] = runs.Count; _brRuns = [.. runs];
        _brH = Math.Min(h, _brY1 + 2);
        _brT = new uint[w * Math.Max(1, _brH)];
        for (int x = 0; x < w; x++) for (int y = 0; y < _brH; y++) _brT[x * _brH + y] = B[y * w + x];
    }

    private const float LeanMax = 0.011f;      // the tallest pine's tip in the strongest gust: this much of U to one side (about 12 pixels at 1080p)
    private const float BobMax = 0.0085f;      // the corner twigs' tips in the strongest gust: this much of U up and down (about 9 pixels at 1080p)

    /// <summary>Once a frame: read the snow's breeze and turn it into the four numbers the scenery hands out (how far the pines lean, how big the boughs' bob is).</summary>
    private void UpdateWind(float dt)
    {
        float breeze = _windTest == "still" ? 0f : Math.Clamp(_snow.Breeze, -0.3f, 1.35f) / 1.35f;      // -0.2 (a breath the other way) to 1 (the strongest gust)
        float strength = MathF.Abs(breeze);
        _swayPhase = (_swayPhase + dt * 2.1) % Math.Tau;                    // a tree held bent by the wind still rocks: about one rock every three seconds
        _bobPhase = (_bobPhase + dt * (2.2 + 1.6 * strength)) % (Math.Tau * 10);   // (times ten: BranchDy also uses 1.7 times this angle, and 17 whole turns wrap cleanly)
        float rockL = 0.16f * strength * (float)Math.Sin(_swayPhase), rockR = 0.16f * strength * (float)Math.Sin(_swayPhase - 1.1);   // the right grove a beat behind the left: the gust reaches it later
        _scenery.PineLeanL = LeanMax * _u * (breeze + rockL);
        _scenery.PineLeanR = LeanMax * _u * (breeze + rockR);
        _scenery.BranchAmp = BobMax * _u * (0.15f + 0.85f * strength);       // never quite still: a bough always stirs a little
        _scenery.BranchPhase = (float)_bobPhase;
    }

    /// <summary>A mix of two pixels of a clear sheet, f out of 256 of b: all four parts (see-through-ness, red, green, blue) in two multiplies.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static uint Mix(uint a, uint b, int f)
    {
        uint inv = (uint)(256 - f), ff = (uint)f;
        uint rb = (((a & 0x00FF00FFu) * inv + (b & 0x00FF00FFu) * ff) >> 8) & 0x00FF00FFu;
        uint ag = (((a >> 8) & 0x00FF00FFu) * inv + ((b >> 8) & 0x00FF00FFu) * ff) & 0xFF00FF00u;
        return rb | ag;
    }

    /// <summary>Lays one pixel of a clear sheet (c, its colour already scaled by its solidness) over a pixel of the frame.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static uint Over(uint c, uint bg)
    {
        uint ia = 256 - (c >> 24);
        uint rb = ((bg & 0x00FF00FFu) * ia >> 8) & 0x00FF00FFu;
        uint g = ((bg & 0x0000FF00u) * ia >> 8) & 0x0000FF00u;
        return (c & 0x00FFFFFFu) + (rb | g);
    }

    private void DrawPines(FrameBuffer fb)
    {
        int w = _w; uint[] P = _scenery.PineLayer; bool[] open = _scenery.PineOpen; uint[] px = fb.Pixels;
        for (int y = _pineY0; y <= _pineY1; y++)
        {
            int row = y * w;
            for (int grove = 0; grove < 2; grove++)
            {
                if (_pineXb[y * 2 + grove] < 0) continue;
                float dx = _scenery.PineDx(grove == 0 ? 0 : w - 1, y);
                int ix = (int)MathF.Floor(dx), f = (int)((dx - ix) * 256f);
                // The frame's pixel x shows the sheet's pixel s = x - ix, mixed with the one to its left by the left-over part of a pixel.
                int sMin = Math.Max(1, 1 - ix), sMax = Math.Min(w - 1, w - 1 - ix);     // the sheet columns whose pixel, its left neighbour and its place in the frame all exist
                // QUICK: inside a solid stretch both pixels are fully opaque, so the mix is opaque too and simply replaces the frame's pixel.
                for (int r = _pineSeg[y * 2 + grove], rEnd = _pineSeg[y * 2 + grove + 1]; r < rEnd; r += 2)
                {
                    int qa = Math.Max(_pineSegs[r] + 1, sMin), qb = Math.Min(_pineSegs[r + 1], sMax);
                    if (qa > qb) continue;
                    // (Spans: the two stretches are checked ONCE to lie inside their arrays, then walked with no check per pixel.)
                    ReadOnlySpan<uint> src = P.AsSpan(row + qa - 1, qb - qa + 2);
                    Span<uint> dst = px.AsSpan(row + qa + ix, qb - qa + 1);
                    uint p0 = src[0];
                    if (f == 0) src[1..].CopyTo(dst);        // slid by a whole number of pixels: a straight copy
                    else for (int k = 0; k < dst.Length; k++) { uint p1 = src[k + 1]; dst[k] = p0 == p1 ? p1 : Mix(p1, p0, f); p0 = p1; }
                }
                // CAREFUL: the ragged edges, where a pixel is partly see-through and is laid over what is behind it.
                for (int r = _pineRag[y * 2 + grove], rEnd = _pineRag[y * 2 + grove + 1]; r < rEnd; r += 2)
                {
                    int qb = Math.Min(_pineRags[r + 1], sMax);
                    for (int sx = Math.Max(_pineRags[r], sMin); sx <= qb; sx++)
                    {
                        uint p1 = P[row + sx], p0 = P[row + sx - 1];
                        uint c = p0 == p1 ? p1 : Mix(p1, p0, f);
                        uint al = c >> 24;
                        if (al == 0 || !open[row + sx + ix]) continue;
                        px[row + sx + ix] = al == 255 ? c : Over(c, px[row + sx + ix]);
                    }
                }
            }
        }
    }

    private void DrawBranches(FrameBuffer fb)
    {
        int w = _w, h = _h, bh = _brH; uint[] T = _brT; uint[] px = fb.Pixels;
        for (int x = 0; x < w; x++)
        {
            int r = _brCol[x], rEnd = _brCol[x + 1];
            if (r == rEnd) continue;
            float dy = _scenery.BranchDy(x); int iy = (int)MathF.Floor(dy), f = (int)((dy - iy) * 256f);
            int col = x * bh;
            for (; r < rEnd; r += 2)
            {
                // The frame's pixel y shows the sheet's pixel sy = y - iy, mixed with the one above it by the left-over part of a pixel.
                // One row past the stretch's end too: that is where the last pixel's left-over part lands.
                int sA = Math.Max(_brRuns[r], Math.Max(1, 1 - iy)), sB = Math.Min(_brRuns[r + 1] + 1, Math.Min(bh - 1, h - 1 - iy));
                for (int sy = sA; sy <= sB; sy++)
                {
                    uint p1 = T[col + sy], p0 = T[col + sy - 1];
                    if ((p0 | p1) >> 24 == 0) continue;
                    uint c = p0 == p1 ? p1 : Mix(p1, p0, f);
                    uint al = c >> 24;
                    if (al == 0) continue;
                    int at = (sy + iy) * w + x;
                    px[at] = al == 255 ? c : Over(c, px[at]);
                }
            }
        }
    }

    /// <summary>The falling snow's question (PetalField.LightAt): the bulb light at a spot, read off the map.</summary>
    private (int R, int G, int B, float K) SnowLight(float x, float y)
    {
        int cx = (int)(x / _slCell), cy = (int)(y / _slCell);
        if (x < 0 || y < 0 || cx >= _slW || cy >= _slH) return (0, 0, 0, 0f);
        int i = cy * _slW + cx;
        return (_slR[i], _slG[i], _slB[i], _slK[i]);
    }

    /// <summary>The residents do nothing before this many seconds: their flip-books are painting on a helper thread, and a start pinned to a time makes every test run begin the same way.</summary>
    private const double ResidentsStart = 6.0;

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly ChristmasScenery _scenery;
    private readonly PetalField _snow;
    private readonly Light[] _lights;
    private readonly Sprite[] _bulbGlows;           // one soft glow per pastel color
    private readonly Sprite[] _pineGlows;           // the same, bigger, for the bulbs on the pines
    private readonly Sprite[] _branchGlows;         // the same, bigger still, for the bulbs on the corner branches
    private readonly int _branchFirst;              // the entries of _lights from here to the end are the corner branches' bulbs
    private const float BranchBulbU = ChristmasPainter.BranchK * ChristmasPainter.BranchBulbK;   // a branch bulb's size as a multiple of a plain bulb's
    private byte[] _slR = [], _slG = [], _slB = [];  // the light map for the falling snow (see BuildSnowLight): the colour of the bulb light in each small square of the screen
    private float[] _slK = [];                       // ... and how strong it is there (0 = none, about 0.9 = right at a bulb)
    private int _slW, _slH, _slCell = 4;             // the map's size in squares, and a square's side in pixels
    private readonly int _pineFirst, _pineEnd;      // which entries of _lights are the pines' bulbs (from _pineFirst up to, not including, _pineEnd)
    private readonly SantaRig _santa;
    private readonly HappeningDirector _happenings;
    private readonly ChimneySmoke _smoke;           // the cabin's chimney, live (Core/ChimneySmoke.cs)
    private readonly Sprite _starGlow, _villageGlow, _windowGlow, _lampGlow, _lampHalo, _glint;
    private readonly float[] _starPhase, _starSpeed, _villagePhase, _villageBulbPhase;
    private readonly Sprite[] _villageBulbGlows;    // one small glow per pastel, for the village's holiday lights
    private readonly (float X, float Y, float Phase, float Speed)[] _glitter;   // the snow's sparkles: where, and each one's own rhythm
    private readonly Noticeboard _board;            // what is going on, for the residents' senses (Core/Minds/Senses.cs)
    private readonly Resident[] _residents;
    private readonly DepthQueue _queue = new();     // draws the residents far to near (Core/Stories.cs)
    private readonly StoryDirector _stories;
    private readonly (Sense Kind, float X, float Y, float Reach, float At)? _testNotice;
    private readonly float _u;
    private readonly int _w, _h;
    // A "double" (a decimal number with about 15 digits of precision), not a
    // "float" (about 7 digits). A screensaver can run for days. A float clock
    // that large can no longer register a 16 millisecond step, and every
    // bulb would freeze.
    private double _time;
    private double _santaTold;                      // when Santa was last pinned on the noticeboard
    // Test aid: PECKWORKS_NOSNOW=1 leaves the falling snow out, so a frame-by-frame comparison (the kit's pop detector)
    // sees only what the animals do, not eight hundred flakes changing every pixel.
    private readonly bool _noSnow = Environment.GetEnvironmentVariable("PECKWORKS_NOSNOW") == "1";
    private readonly bool _pinnedStart = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PECKWORKS_SEED"));   // a test run: see Update
    private bool _started;                          // true once every resident's flip-books are painted: they all begin on that frame

    public ChristmasScene(int width, int height, ChristmasSettings settings)
    {
        _w = width;
        _h = height;
        _scenery = ChristmasPainter.Paint(width, height, _rng);
        _u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        // ---- Snow: the petal engine drawing soft white dots ----
        // Snowflakes are small, so there are more of them than petals.
        _snow = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent, _u) * 2,
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.FlakeSizePercent / 100f,
            [], _rng, sizeUnit: _u)
        {
            Shape = PetalField.FlakeShape.Snow,
            Colors = (Color.White, Color.FromArgb(222, 234, 255)),   // white, through to a faintly blue white
        };

        // ---- Lights: every bulb gets its own rhythm, so they do not blink in step ----
        _bulbGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow((int)(_u * 0.012f), c)).ToArray();
        // The pines' bulbs have their own glow, sized by the pines' own scale (ChristmasPainter.PineK and PineBulbK; both 1 today, so it matches the plain one).
        _pineGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow((int)(_scenery.PineU * ChristmasPainter.PineBulbK * 0.012f), c)).ToArray();
        _pineFirst = _scenery.CabinBulbs; _pineEnd = _scenery.CabinBulbs + _scenery.PineBulbs;
        // ... and the corner branches' bulbs are the biggest of all (the branches hang nearest to us).
        _branchGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow((int)(_u * BranchBulbU * 0.010f), c)).ToArray();
        _branchFirst = _scenery.BranchBulbFirst;
        _lights = _scenery.Lights.Select(l => new Light
        {
            X = l.At.X, Y = l.At.Y, Color = l.Color,
            Phase = (float)(_rng.NextDouble() * Math.PI * 2),
            Speed = 1.0f + 2.6f * (float)_rng.NextDouble(),
        }).ToArray();
        for (int li = _scenery.CabinBulbs; li < _lights.Length; li++) _lights[li].On = (byte)(li >= _scenery.BranchBulbFirst ? 2 : 1);   // (the star, listed right after the pines' bulbs, rides its pine too)
        BuildWindSheets(width, height);
        BuildSnowLight(width, height);
        _snow.LightAt = SnowLight;

        _santa = new SantaRig(_scenery);

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(ChristmasHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);

        // ---- The small live lights. Everything from here on takes its dice from "amb", a
        //      second set seeded by one roll of the scene's, so adding a sparkle later never
        //      shifts what the director and the snow rolled above. ----
        var amb = new Random(_rng.Next());
        _smoke = new ChimneySmoke(_scenery.ChimneyTop, _u, 0.8f, 0.6f, amb);
        _starGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.006f)), Color.FromArgb(236, 244, 255));
        _villageGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.007f)), Color.FromArgb(255, 200, 110));
        _windowGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.030f)), Color.FromArgb(255, 196, 100));
        _lampGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.030f)), Color.FromArgb(255, 220, 150));
        _lampHalo = Sprite.Glow(Math.Max(6, (int)(_u * 0.12f)), Color.FromArgb(255, 204, 130));
        _glint = SantaRig.PaintSparkle(Math.Max(5, (int)(_u * 0.012f)));
        _starPhase = new float[_scenery.Stars.Count];
        _starSpeed = new float[_scenery.Stars.Count];
        for (int i = 0; i < _starPhase.Length; i++)
        {
            _starPhase[i] = (float)(amb.NextDouble() * Math.PI * 2);
            _starSpeed[i] = 0.6f + 1.8f * (float)amb.NextDouble();
        }
        _villagePhase = new float[_scenery.VillageWindows.Count];
        _villageBulbGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow(Math.Max(2, (int)(_u * 0.0042f)), c)).ToArray();
        _villageBulbPhase = new float[_scenery.VillageLights.Count];
        for (int i = 0; i < _villageBulbPhase.Length; i++) _villageBulbPhase[i] = (float)(amb.NextDouble() * Math.PI * 2);
        for (int i = 0; i < _villagePhase.Length; i++) _villagePhase[i] = (float)(amb.NextDouble() * 40);

        // The snow's glitter: points on the open snowfield (not on a prop, not on the ice),
        // each flashing briefly on its own slow rhythm, as moonlit snow does when you move your head.
        var glitter = new List<(float, float, float, float)>();
        for (int tries = 0; tries < 900 && glitter.Count < 150; tries++)
        {
            float gx = (float)amb.NextDouble() * width;
            PointF p = _scenery.FieldPoint(gx, (float)amb.NextDouble());
            int px = (int)p.X, py = (int)p.Y;
            if (px < 0 || px >= width || py < 0 || py >= height) continue;
            if (_scenery.NearDepth[py * width + px] != 0 || _scenery.OnPond(p.X, p.Y, 1.1f)) continue;
            glitter.Add((p.X, p.Y, (float)(amb.NextDouble() * Math.PI * 2), 0.25f + 0.5f * (float)amb.NextDouble()));
        }
        _glitter = [.. glitter];

        // ---- The noticeboard: whatever happens is pinned here, and every resident reads it ----
        // The board is asked for CALM (Core/Minds/Senses.cs): a glance is followed by a rest of about a quarter of
        // a minute, the same source is not interesting again for three quarters of a minute, and the whole field
        // may begin two glances in any ten seconds. Fear is never rationed.
        _board = new Noticeboard { LookRest = 14f, Habituate = 45f, Looks = 2, Window = 10f };
        _happenings.Began = Tell;

        // ---- The residents. Each takes ONE roll of the scene's dice for its own set, in this
        //      order; a new resident goes at the END of the list so nobody else's luck moves. ----
        var reindeer = new ReindeerHerd(_scenery, settings.ReindeerCount, _rng, _board);
        var field = new FoxAndHares(_scenery, settings.HareCount, _rng, _board);
        var snowman = new YardSnowman(_scenery, 1, _rng, _board);
        var birds = new FeederBirds(_scenery, settings.BirdCount, _rng, _board);
        var mice = new LogMice(_scenery, settings.MouseCount, _rng, _board);
        var otters = new Otters(_scenery, settings.OtterCount, _rng, _board);
        var squirrels = new Squirrels(_scenery, settings.SquirrelCount, _rng, _board, birds);
        var predators = new Predators(_scenery, settings.PredatorPercent, _rng, _board, field, mice, squirrels);   // LAST: it takes the dice last, so nobody else's luck moves
        _residents = [reindeer, field, snowman, birds, mice, otters, squirrels, predators];

        // ---- The stories ----
        var world = new ChristmasWorld
        {
            Scenery = _scenery, Board = _board, Santa = _santa,
            Reindeer = reindeer, Field = field, Snowman = snowman, Birds = birds, Mice = mice, Otters = otters, Squirrels = squirrels,
        };
        _stories = new StoryDirector(_happenings, _rng,
            [new SantaDelivery(world), new MiceTrimTree(world), new CarrotHeist(world)], settings.StoryPercent / 100f);
        // NO STORY BEGINS WHILE A HUNTER IS OUT. (On film an owl that had just missed a mouse sat on a mound while the mice's tree-trimming story
        // began beside it: three mice carried baubles and a star past its feet.) A predator already waits for a story to end (Lull); this is the
        // other half: the stories wait for the wolf or the owl to leave.
        _stories.Hold = () => predators.WolfOn || predators.OwlOn;

        // Test aid: PECKWORKS_NOTICE="Loud;0.5;0.8;0.8;6" pins that kind of notice at (x, y as
        // fractions of the screen) with a reach in U at that second, to watch everybody react.
        string? notice = Environment.GetEnvironmentVariable("PECKWORKS_NOTICE");
        if (!string.IsNullOrEmpty(notice))
        {
            string[] p = notice.Split(';');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _testNotice = (Enum.Parse<Sense>(p[0], true), float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv));
        }
    }

    // ==================================================================
    //  UPDATE AND RENDER
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _snow.Update(dt);
        UpdateWind(dt);
        _smoke.Update(dt);
        _santa.Update(dt);
        _board.Tick(dt);

        // Santa going over is something to look up at: pinned once a second
        // while he is in view (a notice lasts one second), high in the sky
        // with a long reach and little weight, so it is a glance, not a fright.
        if (_scenery.SantaOnScreen && _time - _santaTold >= 2.5)
        {
            _santaTold = _time;
            _board.Post(Sense.Movement, _scenery.SantaSleigh, 1.6f * _u, 0.35f, _santa);
        }
        if (_testNotice is { } tn && _time >= tn.At && _time - dt < tn.At)
            _board.Post(tn.Kind, new PointF(tn.X * _w, tn.Y * _h), tn.Reach * _u, 1f, this);

        // ONE START FOR EVERYBODY: nobody begins until every resident's flip-books are painted. (At first each
        // began when its own were done: the mice at 4 s, the herd at 15 to 18, each popping into the scene fully
        // formed, and the same seed played differently from run to run.) Each resident then makes its own
        // entrance: the herd walks in, the birds fly in, the hares come up out of their burrows.
        // THE START. On his screen the residents begin on the first frame after ResidentsStart on which every one
        // of them has finished painting its flip-books: nothing ever waits, the snow never stops. In a TEST (a seed
        // is set) the start is PINNED to ResidentsStart exactly: the scene waits there for whoever is still
        // painting, because a start that follows the painting moves by a second from run to run, and then the same
        // seed plays a different film each time and no test can be repeated. (A test is not watched, so the pause
        // costs nothing; on his screen a pause would be a freeze.)
        if (!_started && _time >= ResidentsStart)
        {
            bool all = true;
            foreach (Resident r in _residents) if (!r.Ready) { all = false; break; }
            if (!all && _pinnedStart)
            {
                var waited = System.Diagnostics.Stopwatch.StartNew();
                while (!all && waited.ElapsedMilliseconds < 60000)
                {
                    Thread.Sleep(5);
                    all = true;
                    foreach (Resident r in _residents) if (!r.Ready) { all = false; break; }
                }
                all = true;                              // a minute and still not painted: something failed; carry on with whoever is ready
            }
            _started = all;
        }
        if (_started) _stories.Update(dt);          // before the residents: a story may borrow one this very frame
        if (_started)
        {
            _scenery.TurnPage();                    // last frame's "who stands where" becomes the sheet to read
            foreach (Resident r in _residents) if (r.Ready) { r.Lull = _stories.Playing; r.Update(dt); }
        }
        _happenings.Update(elapsedSeconds);
    }

    /// <summary>
    /// Tells the residents a surprise has begun: each happening that an
    /// animal could notice is pinned on the board as a kind, a place and a
    /// reach (fireworks are loud and carry across the whole field; an owl on
    /// a branch is a threat to the small things; a snowball rolling by is a
    /// movement to glance at). The animals' own senses decide what they make of it.
    /// </summary>
    private void Tell(Happening h)
    {
        ChristmasScenery s = _scenery;
        float u = s.U;
        var mid = new PointF(s.Width * 0.5f, s.WalkY(s.Width * 0.5f));
        switch (h.GetType().Name)
        {
            case "Fireworks": _board.Post(Sense.Loud, new PointF(s.Width * 0.5f, s.Height * 0.25f), 2.0f * u, 0.75f, h); break;
            case "SnowyOwl": _board.Post(Sense.Threat, new PointF(s.Width * 0.5f, s.Height * 0.3f), 1.2f * u, 0.6f, h); break;
            case "ToyTrain": _board.Post(Sense.Loud, new PointF(s.Width * 0.5f, s.Horizon), 1.0f * u, 0.3f, h); break;
            case "SnowSlide": _board.Post(Sense.Loud, new PointF(s.Pines[s.StarPine].X, s.Pines[s.StarPine].Foot), 0.6f * u, 0.5f, h); break;
            case "RollingSnowball": _board.Post(Sense.Movement, mid, 0.8f * u, 0.7f, h); break;
            case "SnowGust": _board.Post(Sense.Movement, mid, 1.2f * u, 0.5f, h); break;
            case "PresentDrop": _board.Post(Sense.Movement, s.SantaSleigh, 1.6f * u, 0.6f, h); break;
            case "ChimneyRings": _board.Post(Sense.Movement, s.ChimneyTop, 0.5f * u, 0.4f, h); break;
        }
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        fb.Restore(_scenery.Pixels);

        // 2. The FAR happenings (layer 0): the ones in the sky and on the
        //    mountains, before Santa so a firework or the northern lights
        //    are behind him.
        _happenings.Draw(fb, 0);

        // 2b. The stars twinkle, through the sky stencil so a star behind a
        //     branch never shines through it. sin() swings between -1 and 1;
        //     squaring the "0 to 1" version keeps a star dim most of the time
        //     with a short bright peak.
        for (int i = 0; i < _starPhase.Length; i++)
        {
            float bright = 0.5f + 0.5f * (float)Math.Sin(_time * _starSpeed[i] + _starPhase[i]);
            PointF at = _scenery.Stars[i];
            _starGlow.DrawCentered(fb, at.X, at.Y, bright * bright * 0.9f, _scenery.OpenSky);
        }

        // 2c. The village: every window's lamp wanders a little, and now and
        //     then one goes dark for a while (somebody went to bed) and comes back.
        for (int i = 0; i < _villagePhase.Length; i++)
        {
            float p = _villagePhase[i];
            float awake = (float)Math.Sin(_time * 0.05 + p) > -0.86f ? 1f : 0.15f;
            float lamp = 0.55f + 0.20f * (float)Math.Sin(_time * 1.3 + p * 3) + 0.12f * (float)Math.Sin(_time * 4.1 + p);
            PointF at = _scenery.VillageWindows[i];
            _villageGlow.DrawCentered(fb, at.X, at.Y, lamp * awake);
        }

        // 2c2. The village's holiday lights: each bulb on its own slow rhythm, never fully out, like the pines' bulbs.
        for (int i = 0; i < _villageBulbPhase.Length; i++)
        {
            var vl = _scenery.VillageLights[i];
            float bright = 0.40f + 0.60f * (0.5f + 0.5f * (float)Math.Sin(_time * (1.1 + 0.37 * (i % 5)) + _villageBulbPhase[i]));
            _villageBulbGlows[vl.Color].DrawCentered(fb, vl.At.X, vl.At.Y, bright);
        }

        // 2d. The cabin's smoke and its windows' lamplight.
        _smoke.Draw(fb);
        for (int i = 0; i < _scenery.CabinWindows.Length; i++)
        {
            RectangleF win = _scenery.CabinWindows[i];
            float lamp = 0.36f + 0.07f * (float)Math.Sin(_time * 1.7 + i * 2.1) + 0.04f * (float)Math.Sin(_time * 5.3 + i);
            _windowGlow.DrawCentered(fb, win.X + win.Width / 2, win.Y + win.Height / 2, lamp);
        }

        // 3. Santa, behind everything that is not sky (in practice, the corner branches).
        _santa.Draw(fb);

        // 3b. The pines, bent by the wind, then the corner branches, bobbing (see BuildWindSheets). After Santa, so he flies behind both.
        long tw0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_windTest != "nopines") DrawPines(fb);
        long tw1 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_windTest != "nobranches") DrawBranches(fb);
        if (_windTest == "count") { _twP += tw1 - tw0; _twB += System.Diagnostics.Stopwatch.GetTimestamp() - tw1; if (++_twN % 300 == 0) System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "windcost.txt"), $"pines {_twP * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _twN:F3} ms, branches {_twB * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _twN:F3} ms a frame over {_twN} frames"); }

        // 4. The lights. sin() swings between -1 and 1; the arithmetic turns
        //    that into "between 35% and 100% bright", so a bulb dims but
        //    never goes fully out.
        for (int li = 0; li < _lights.Length; li++)
        {
            ref readonly Light l = ref _lights[li];
            float bright = 0.35f + 0.65f * (0.5f + 0.5f * (float)Math.Sin(_time * l.Speed + l.Phase));
            float lx = l.X, ly = l.Y;
            if (l.On == 1) lx += _scenery.PineDx(l.X, l.Y); else if (l.On == 2) ly += _scenery.BranchDy(l.X);       // the glow goes where its bulb went
            (li >= _branchFirst ? _branchGlows : li >= _pineFirst && li < _pineEnd ? _pineGlows : _bulbGlows)[l.Color].DrawCentered(fb, lx, ly, bright);
        }

        // 4a. The snow's glitter and the lamp. A sparkle is sin() raised to a
        //     high power: nothing most of the time, one short flash per cycle.
        foreach (ref readonly var gl in _glitter.AsSpan())
        {
            float flash = MathF.Pow(MathF.Max(0f, (float)Math.Sin(_time * gl.Speed + gl.Phase)), 24);
            if (flash > 0.02f && !_noSnow) _glint.DrawCentered(fb, gl.X, gl.Y, flash * 0.9f);   // off with the snow in the pop detector's runs: a sparkle beside an animal read as the animal popping
        }
        {
            // The lamp's flame gutters: two quick waves and a slow one, so it never ticks like a clock.
            float flick = 0.80f + 0.08f * (float)Math.Sin(_time * 7.3) + 0.06f * (float)Math.Sin(_time * 13.1 + 1.0) + 0.05f * (float)Math.Sin(_time * 2.1);
            _lampHalo.DrawCentered(fb, _scenery.Lamp.X, _scenery.Lamp.Y, 0.50f * flick);
            _lampGlow.DrawCentered(fb, _scenery.Lamp.X, _scenery.Lamp.Y, 0.75f * flick);
        }

        // 5. The residents, far to near: each hands the queue one line per
        //    animal, and the queue draws them in order of their feet.
        if (_started)
            foreach (Resident r in _residents) if (r.Ready) r.Collect(_queue);
        _queue.Flush(fb);

        // 5b. The story that is on, if any: its actors and props, over the residents.
        _stories.Draw(fb);

        // 6. The NEAR happenings (layer 1): everything that is in front of
        //    the scenery, like the owl on its twig. After Santa and the
        //    glows, so the sleigh (which flies behind the branches) cannot
        //    be painted over an owl sitting on one.
        _happenings.Draw(fb, 1);

        // 7. The snow, far to near, in front of everything.
        if (!_noSnow) _snow.Draw(fb);

        // 8. Happenings on the GLASS (layer 2): frost on the window pane is
        //    nearer than the falling snow outside it.
        _happenings.Draw(fb, 2);
    }

    public void Dispose() { }
}
