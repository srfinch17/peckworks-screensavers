using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// THE SURFACE (round forty-six, 2026-11-02, his lines: "the shimmering light just ends up looking dumb. we need to
/// rethink it and you need to learn how to look at light rays in pond water photos", and "the surface of our water is
/// hard to divulge, need some visual cue. a shimmer. a ripple"). Two things every photo of a sunlit pond seen from above
/// shows, and neither is rays or scattered sparkle:
///
///   1. THE CAUSTIC NET. The surface is never flat; each ripple is a lens, and the lenses focus the sun into bright
///      branching lines on the bottom, a net of irregular cells that slides, stretches and merges all the time as the
///      ripples pass. Sharp and bright where the water is shallow and clear, soft and faint where it is deep (the lines
///      blur on the way down and the murk veils them on the way back up), absent in the shade of a tree. Painted on the
///      bed, UNDER everything that swims.
///   2. THE SHIMMER. Everything under the surface is seen THROUGH the ripples, so it wobbles: the bed, the stones, the
///      log, all of it sways by a pixel or two in a slow pattern that drifts. That wobble is what tells the eye there is a
///      surface between it and the bottom; a still, crisp bottom reads as a painting with fish pasted on it.
///
/// HOW. The net is a Worley (cell) pattern: a few dozen points drift about a square tile, and at every tile pixel the
/// nearest two points are found; the line is where those two are about equally near (the edge between two cells), so the
/// brightness is 1 less the gap between the two distances over a line width. The points move on small circles whose
/// rates are whole numbers of turns per loop, so the pages of the flip-book join end to end with no jump. Two nets at
/// different scales are summed (a fine one over a coarse one: real caustics have both). The shimmer is a second
/// flip-book of the same size: a displacement (two signed bytes, x and y) from a few slow sine waves, also whole turns
/// per loop. Both are baked once at the start, on their own dice from the screen's size (no scene roll moves).
///
/// WHERE IT COSTS LITTLE. Every frame already copies the whole backdrop into the frame (FrameBuffer.Restore, a
/// memory-bound copy). Restore here is that same copy with both folded in, in parallel bands of rows as the ripples are
/// drawn: each water pixel in view is read from the backdrop a shimmer's offset away (only if that spot is water too, so
/// the bank never smears into the pond), then brightened toward a warm white by the net's value times a baked STRENGTH.
/// The strength is per screen pixel, baked once: the sun map (none in the boughs' shade), how much of the bed shows at
/// the drawn depth there (PondPainter.BedClear's curve) and how much light reaches that bed (exp of minus 1.6 times the
/// depth), nothing on land or under the boughs' baked parts, scaled by the knob "Sunlight" (0 none, 100 this, 200
/// brighter). The net is tiled in SCREEN space (the bed is drawn sunk, and the tile is large next to the sink's change
/// across a cell, so the net rides the drawn bed), scaled so a cell is about 0.045u (a hand's width on a real pond next
/// to a koi of 0.08u), drifting slowly with the breeze so the loop is never seen.
///
/// What this does NOT do: light or wobble the animals (they are drawn after, and their painters mix their murk toward the
/// pristine backdrop, Pond.Pixels), or the glitter on the surface, which is Sunlight.cs's and is cut to where the sun's
/// reflection falls.
/// </summary>
internal sealed class Caustics
{
    private const int Tile = 256, Mask = Tile - 1;    // a power of two, so the wrap is a mask
    private const int Pages = 64;                     // 5.3 s of net at 12 pages a second
    private const float PageRate = 12f;
    private const int BandRows = 32;
    private readonly byte[][] _pages;                 // the net, 0 to 255 per tile pixel
    private readonly sbyte[][] _shimX, _shimY;        // the shimmer's offsets, in sixteenths of a pixel
    private readonly byte[] _strength;                // per screen pixel: how much net light lands here (0 to 200), or NotWater
    private const byte NotWater = 255;                // the strength byte of a pixel that is land, or under the boughs' baked parts: copied as it is
    private readonly uint[] _backdrop;                // the pristine picture (Pond.Pixels)
    private readonly uint[] _lit;                     // the backdrop with the shimmer and the net on it, half its bands refreshed each frame
    private int _frame;                               // which half of the bands is refreshed this frame
    private Task? _pending;                           // the half-band refresh of this frame, running while the animals think (Update); Restore waits for it
    private readonly Action _work;
    private readonly int _w, _h, _scaleFix;           // the tile sampled at scaleFix/256 tile pixels per screen pixel
    private readonly float _shimmerPx;                // the shimmer's full swing in screen pixels
    private readonly Action<int> _restoreBand;
    private double _clock;                            // seconds, wrapped at the loop
    private float _driftX, _driftY;                   // the net's slow slide, tile pixels

    /// <summary>The lit backdrop: what the animals' painters mix their murk toward from round forty-six, so the net falls on them (the photos: a koi in clear water is netted with light as the bottom is).</summary>
    public uint[] Lit => _lit;

    /// <summary>How many screen pixels the net's light can land on at all (the "is it on screen" count for the start log).</summary>
    public int LitPixels { get; }
    /// <summary>The mean strength over those pixels, 0 to 255.</summary>
    public float MeanStrength { get; }

    public Caustics(Pond pond, bool[] inView, float knob)
    {
        _restoreBand = RestoreBand;                                                 // made once: a lambda per frame is an allocation per frame
        _work = () => Parallel.For(0, ((_h + BandRows - 1) / BandRows + 1) / 2, _restoreBand);
        _w = pond.Width; _h = pond.Height; _backdrop = pond.Pixels;
        _lit = (uint[])pond.Pixels.Clone();
        float u = pond.U;
        _scaleFix = (int)(256f * Tile / (u * 0.24f));                               // the tile covers 0.24u of screen
        _shimmerPx = u * 0.0022f;                                                   // about 2.4 px at 1080 lines, either way
        _strength = new byte[_w * _h];
        long sum = 0; int lit = 0;
        for (int i = 0; i < _strength.Length; i++)
        {
            if (!inView[i] || !pond.Water[i] || pond.Near[i] == 255) { _strength[i] = NotWater; continue; }   // (a stone's dry cap is nearest of all, Near 255: no light net and no shimmer on it)
            if (knob <= 0) continue;
            // The drawn bed's depth here: Pond.Near holds it (260 less 250 z) where the sunk bed was drawn, else the
            // flat pour's depth.
            float z = pond.Near[i] > 0 ? Math.Clamp((260 - pond.Near[i]) / 250f, 0, 1) : pond.Depth[i] / 255f;
            float clear = 0.4f + 0.48f * (1 - z);                                  // PondPainter.BedClear's curve: how much of the bed shows through
            float reach = MathF.Exp(-1.6f * z);                                    // how much of the sun gets down to it
            float s = pond.Sun[i] / 255f * clear * reach * MathF.Min(2f, knob) * 0.85f;   // (0.95 on the first render: a honeycomb over everything; 0.6 with wide lines; the thin lines of the photos take 0.75)
            int b = (int)(Math.Clamp(s, 0, 1) * 200);
            if (b == 0) continue;
            _strength[i] = (byte)b; sum += b; lit++;
        }
        LitPixels = lit; MeanStrength = lit > 0 ? sum / (float)lit : 0;
        _pages = new byte[Pages][]; _shimX = new sbyte[Pages][]; _shimY = new sbyte[Pages][];
        Bake(new Random(_w * 7919 + _h * 104729 + 46), knob > 0);
    }

    /// <summary>
    /// The flip-books. The net: two Worley nets (coarse and fine) summed, each page a moment of the loop; a point's
    /// drift is a circle of a whole number of turns per loop (1 or 2) so page 0 follows the last without a jump. The
    /// shimmer: the sum of four slow plane waves in each of x and y, each a whole number of turns per loop too.
    /// </summary>
    private void Bake(Random rng, bool net)
    {
        (float[] bx, float[] by, float[] rad, int[] nx, int[] ny, float[] phx, float[] phy, float[] bright) Deal(int n, float drift)
        {
            var bx = new float[n]; var by = new float[n]; var rad = new float[n]; var nx = new int[n]; var ny = new int[n]; var phx = new float[n]; var phy = new float[n]; var bright = new float[n];
            for (int k = 0; k < n; k++)
            {
                bright[k] = 0.55f + 0.45f * (float)rng.NextDouble();                          // FROM THE PHOTOS: no two lines of the net are as bright as each other (0.35 to 1 was too faint)
                bx[k] = (float)rng.NextDouble() * Tile; by[k] = (float)rng.NextDouble() * Tile;
                rad[k] = drift * (0.6f + 0.8f * (float)rng.NextDouble());
                nx[k] = 2 + rng.Next(3); ny[k] = 2 + rng.Next(3);                           // turns per loop: 2 to 4 (1 to 2 on the first dump moved 1.5 percent of the pixels in three frames: too slow to read as a ripple's net)
                phx[k] = (float)(rng.NextDouble() * Math.Tau); phy[k] = (float)(rng.NextDouble() * Math.Tau);
            }
            return (bx, by, rad, nx, ny, phx, phy, bright);
        }
        const int Coarse = 18, Fine = 48;
        var c = Deal(Coarse, Tile * 0.08f);
        var f = Deal(Fine, Tile * 0.05f);
        // FROM THE PHOTOS (2026-11-02, Google Images in his Chrome, "sunlight ripples pond bottom caustics" and "koi pond from
        // above sunlight"): the lines are THIN and crisp, near white; the cells are STRETCHED along the ripples' direction,
        // not round; and the brightness varies from line to line, some brilliant, some faint. So: the distance is measured
        // squeezed across one direction (cells 1.7 times as long as wide along it), the line widths are a twelfth of a cell,
        // and every point of the net carries its own brightness (the edge between two cells takes their mean).
        float stretchAng = (float)(rng.NextDouble() * Math.PI);
        _stretchC = MathF.Cos(stretchAng); _stretchS = MathF.Sin(stretchAng);
        float wCoarse = Tile / MathF.Sqrt(Coarse) * 0.11f, wFine = Tile / MathF.Sqrt(Fine) * 0.12f;   // (0.085 and 0.1 with the 1.7 stretch read as fine scratches)
        var px = new float[Coarse]; var py = new float[Coarse]; var qx = new float[Fine]; var qy = new float[Fine];
        var raw = new float[Tile * Tile];
        // The shimmer's waves: wavelengths of a half to a sixth of the tile (whole numbers of waves across it, so they
        // tile), each turning 1 or 2 times per loop, in random directions.
        const int Waves = 4;
        var wk = new float[Waves * 2]; var wl = new float[Waves * 2]; var wn = new int[Waves * 2]; var wp = new float[Waves * 2]; var wa = new float[Waves * 2];
        for (int k = 0; k < Waves * 2; k++)
        {
            int across = 2 + rng.Next(5), along = rng.Next(3) - 1;                     // waves across the tile, and a slant
            wk[k] = across * MathF.Tau / Tile; wl[k] = along * MathF.Tau / Tile;
            wn[k] = 4 + rng.Next(5); wp[k] = (float)(rng.NextDouble() * Math.Tau); wa[k] = 0.5f + 0.5f * (float)rng.NextDouble();   // 4 to 8 turns per loop: three quarters to one and a half wobbles a second, a pond's ripple pace
        }
        for (int p = 0; p < Pages; p++)
        {
            float t = p / (float)Pages * MathF.Tau;
            var sx = new sbyte[Tile * Tile]; var sy = new sbyte[Tile * Tile];
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float ox = 0, oy = 0, norm = 0;
                    for (int k = 0; k < Waves; k++)
                    {
                        ox += wa[k] * MathF.Sin(wk[k] * x + wl[k] * y + wn[k] * t + wp[k]);
                        oy += wa[k + Waves] * MathF.Sin(wk[k + Waves] * y + wl[k + Waves] * x + wn[k + Waves] * t + wp[k + Waves]);
                        norm += wa[k];
                    }
                    sx[y * Tile + x] = (sbyte)Math.Clamp(ox / norm * 127, -127, 127);
                    sy[y * Tile + x] = (sbyte)Math.Clamp(oy / norm * 127, -127, 127);
                }
            _shimX[p] = sx; _shimY[p] = sy;
            var page = new byte[Tile * Tile];
            _pages[p] = page;
            if (!net) continue;
            for (int k = 0; k < Coarse; k++) { px[k] = c.bx[k] + c.rad[k] * MathF.Cos(c.nx[k] * t + c.phx[k]); py[k] = c.by[k] + c.rad[k] * MathF.Sin(c.ny[k] * t + c.phy[k]); }
            for (int k = 0; k < Fine; k++) { qx[k] = f.bx[k] + f.rad[k] * MathF.Cos(f.nx[k] * t + f.phx[k]); qy[k] = f.by[k] + f.rad[k] * MathF.Sin(f.ny[k] * t + f.phy[k]); }
            // The net is brighter here and fainter there (a slow mask of two waves, turning with the loop): on the first
            // render every cell was as bright as every other and the bed was a honeycomb.
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float m = 0.6f + 0.4f * MathF.Sin(x * (MathF.Tau / Tile) + t) * MathF.Sin(y * (MathF.Tau / Tile) * 2 - t + 1.1f);
                    raw[y * Tile + x] = (Net(x, y, px, py, wCoarse, c.bright) + 0.5f * Net(x, y, qx, qy, wFine, f.bright)) * m;
                }
            // A soft glow round every line (a box blur of radius 1 mixed in), then the page.
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float s = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            s += raw[((y + dy) & Mask) * Tile + ((x + dx) & Mask)];
                    float v = raw[y * Tile + x] * 0.7f + s / 9f * 0.3f;                      // (a crisper line than the first render's: the photos' lines are sharp)
                    page[y * Tile + x] = (byte)(Math.Clamp(v, 0, 1) * 255);
                }
        }
    }

    private float _stretchC = 1, _stretchS = 0;                                      // the ripples' direction: cells are squeezed across it

    /// <summary>The net's brightness at a tile pixel: on the edge between the two nearest points (their mean brightness), falling to 0 a line width away; the distance squeezed across the ripples' direction so the cells are stretched along it (periodic in the tile).</summary>
    private float Net(int x, int y, float[] ptsX, float[] ptsY, float width, float[] bright)
    {
        float f1 = float.MaxValue, f2 = float.MaxValue; int i1 = 0, i2 = 0;
        for (int k = 0; k < ptsX.Length; k++)
        {
            float dx = x - ptsX[k], dy = y - ptsY[k];
            if (dx > Tile * 0.5f) dx -= Tile; else if (dx < -Tile * 0.5f) dx += Tile;   // the tile wraps
            if (dy > Tile * 0.5f) dy -= Tile; else if (dy < -Tile * 0.5f) dy += Tile;
            float along = dx * _stretchC + dy * _stretchS, across = -dx * _stretchS + dy * _stretchC;
            float d = MathF.Sqrt(along * along * 0.55f + across * across);          // 1.35 times as long along the ripples as across (1.7 made parallel scratches)
            if (d < f1) { f2 = f1; i2 = i1; f1 = d; i1 = k; } else if (d < f2) { f2 = d; i2 = k; }
        }
        float v = 1 - (f2 - f1) / width;
        if (v <= 0) return 0;
        return v * MathF.Sqrt(v) * 0.5f * (bright[i1] + bright[i2]);                   // a bright core, soft shoulders, the two cells' own brightness
    }

    public void Update(float dt)
    {
        _clock += dt;
        if (_clock >= Pages / PageRate) _clock -= Pages / PageRate;              // the loop, kept short so the double never grows
        _driftX = (_driftX + 5.5f * dt) % Tile; _driftY = (_driftY + 2.2f * dt) % Tile;
        // The refresh of this frame's half of the bands starts NOW, on the pool, and runs while the animals think (the rest
        // of the scene's Update, about two milliseconds on one core): it reads the pristine backdrop and writes the lit copy,
        // which nothing else touches until Restore waits for it. Measured in the frame itself it was 1.3 to 1.6 ms over the
        // plain copy and pushed the average to 15.4 at 1080p, within a hair of the pump's hold-at-thirty line.
        _frame++;
        _pending = Task.Run(_work);
    }

    /// <summary>
    /// The backdrop copied into the frame, shimmering under the surface and with the net's light on the bed: in place of
    /// FrameBuffer.Restore. The lit picture is kept (_lit) and HALF its bands are refreshed each frame, the even ones on
    /// even frames and the odd on odd (the net and the shimmer move slowly: thirty refreshes a second of each row is more
    /// than the eye asks), then the whole of it is copied into the frame as the plain Restore was. Measured on a loaded
    /// machine the lit copy done whole every frame was 6.4 ms against the plain copy's 1.5; half the bands plus the copy
    /// is the cheaper of the two ways to have it every frame.
    /// </summary>
    public void Restore(FrameBuffer fb)
    {
        if (_pending != null) { _pending.Wait(); _pending = null; }
        else { _frame++; Parallel.For(0, ((_h + BandRows - 1) / BandRows + 1) / 2, _restoreBand); }   // (a Render with no Update before it: the preview, a snapshot's first frame)
        fb.Restore(_lit);
    }

    private void RestoreBand(int half)
    {
        int band = half * 2 + (_frame & 1);
        int w = _w, h = _h, step = _scaleFix;
        int y0 = band * BandRows, y1 = Math.Min(h, y0 + BandRows);
        if (y0 >= h) return;
        // No bounds checks in the hot loop: every index is derived from w and h above (Unsafe.Add over the arrays' data).
        ref uint outRef = ref MemoryMarshal.GetArrayDataReference(_lit);
        ref uint bgRef = ref MemoryMarshal.GetArrayDataReference(_backdrop);
        ref byte strRef = ref MemoryMarshal.GetArrayDataReference(_strength);
        int p = (int)(_clock * PageRate) % Pages;
        ref byte pageRef = ref MemoryMarshal.GetArrayDataReference(_pages[p]);
        ref sbyte shxRef = ref MemoryMarshal.GetArrayDataReference(_shimX[p]);
        ref sbyte shyRef = ref MemoryMarshal.GetArrayDataReference(_shimY[p]);
        int dx = (int)(_driftX * 256), dy = (int)(_driftY * 256);
        int swing = (int)(_shimmerPx * 256 / 127);                                 // fixed point: offset pixels = sbyte * swing >> 8
        for (int y = y0; y < y1; y++)
        {
            int row = y * w;
            int prow = ((((y * step) + dy) >> 8) & Mask) * Tile;
            int xAcc = dx;                                                          // the tile's x, in 256ths, walked along the row
            int ox = 0, oy = 0;                                                     // the shimmer's offset, read every other pixel (it is smooth)
            for (int x = 0; x < w; x++, xAcc += step)
            {
                int i = row + x;
                int s = Unsafe.Add(ref strRef, i);
                if (s == NotWater) { Unsafe.Add(ref outRef, i) = Unsafe.Add(ref bgRef, i); continue; }
                int ti = prow + ((xAcc >> 8) & Mask);
                if ((x & 1) == 0) { ox = Unsafe.Add(ref shxRef, ti) * swing >> 8; oy = Unsafe.Add(ref shyRef, ti) * swing >> 8; }
                // The shimmer: read the backdrop a little way off, where that is water in view too.
                int sxp = x + ox, syp = y + oy, j = i;
                if (sxp >= 0 && syp >= 0 && sxp < w && syp < h) { int jj = syp * w + sxp; if (Unsafe.Add(ref strRef, jj) != NotWater) j = jj; }
                uint c = Unsafe.Add(ref bgRef, j);
                if (s != 0)
                {
                    int f = Unsafe.Add(ref pageRef, ti) * s >> 8;                   // 0 to 199: how much of the warm white lands here
                    if (f != 0)
                    {
                        int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                        r += (255 - r) * f >> 8; g += (253 - g) * f >> 8; b += (238 - b) * f >> 8;   // near white (the photos' lines are white, not gold)
                        c = (uint)(r << 16 | g << 8 | b);
                    }
                }
                Unsafe.Add(ref outRef, i) = c;
            }
        }
    }
}
