namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// Makes a river in a photo FLOW, the way a real one does.
///
/// WHAT A RIVER ACTUALLY DOES: look at rapids for a minute. The big shapes
/// hold still: the rocks, of course, but also the white water piled up
/// behind each rock, the smooth glassy tongue between two boulders, the
/// darker deep run along one bank. Those are made by the riverbed, and the
/// riverbed does not move. What travels is everything SMALL on top: flecks
/// of foam, glints, little ripples and wrinkles, all sliding downstream over
/// that still pattern, faster in the middle of the channel than at the banks.
///
/// SO THE PHOTO IS SPLIT IN TWO. A blurred copy of the water keeps the big
/// shapes and the colour; the photo minus that blur is the fine texture (a
/// sheet of tiny ups and downs in brightness). The big shapes stay exactly
/// where the camera saw them. Only the fine texture is carried downstream.
/// Rocks are found (dark, colourless lumps in the water) and left out of the
/// travelling texture entirely, so they never drift.
///
/// HOW THE TEXTURE IS CARRIED: imagine the fine texture printed on a rubber
/// sheet that we keep dragging downstream; each pixel then shows the texture
/// from a little way UPSTREAM of it. A dragged sheet runs out, so there are
/// TWO sheets half a cycle apart: while one slides, the other is quietly
/// reset, and we show mostly the one mid-slide (cross-fading as they swap).
/// Games have drawn rivers this way for twenty years; they call it a "flow
/// map". Because only fine texture is being cross-faded (the big shapes
/// never move), the swap is far harder to see than it would be with the
/// whole photo. And every stretch of the river has its own clock (smooth
/// random blobs, like dappled light), so the swaps never happen together.
/// </summary>
public sealed class FlowingWater
{
    private const float Cycle = 1.6f;     // seconds for one sheet to slide and reset

    private readonly PhotoBackdrop _photo;
    // The fine texture, per screen pixel, as a colour shifted up by 128 (so
    // "no change" is 128 and it fits the 0 to 255 of an ordinary pixel):
    // 0x00RRGGBB with each channel = 128 + (photo minus its blur).
    private readonly uint[] _detail;
    // Per screen row, the run of pixels that holds water; then, per water
    // pixel in reading order, how far its sheet slides in one cycle (pixels,
    // pointing upstream), its own clock (0 to 1), and how strongly it flows
    // (0 on a rock or out of the water, 256 in open water).
    private readonly int[] _rowY, _rowFrom, _rowStart;
    private readonly float[] _dx, _dy, _clock;
    private readonly short[] _strength;
    // On a big screen the flow is worked out once per square of pixels and
    // the square shares it: at 4K, once per 2 by 2. A quarter of the work.
    private readonly int _block;
    private double _time;

    /// <param name="path">
    /// The middle of the channel, from upstream to downstream, as fractions
    /// of the photo. Each pixel of water flows along the nearest stretch of it.
    /// </param>
    /// <param name="speed">
    /// How fast the middle of the channel flows at the nearest water, as a
    /// fraction of the photo's height per second. The far water is slower in
    /// pixels (perspective) and the banks slower still.
    /// </param>
    public FlowingWater(PhotoBackdrop photo, PointF[] path, float speed, Random rng)
    {
        _photo = photo;
        int w = photo.Width, h = photo.Height;
        _block = Math.Max(1, (int)MathF.Round(w / 1920f));
        PointF[] line = path.Select(q => photo.ToScreen(q.X, q.Y)).ToArray();

        // The fine texture: the photo minus a slightly blurred copy of it.
        // "Slightly": about a tenth of a percent of the photo, a few pixels,
        // so only flecks, glints and wrinkles count as texture.
        int fine = Math.Max(1, (int)(photo.P * 0.0012f));
        _detail = FineTexture(photo.Pixels, w, h, fine, out byte[] rock);

        // How far from the bank each pixel is: the water mask blurred very
        // wide. Deep in the channel it stays near 255; near a bank it drops.
        byte[] middle = Blur(photo.Water, w, h, Math.Max(2, (int)(photo.P * 0.02f)));
        float[] noiseA = Noise(w, h, photo.P * 0.12f, rng);     // each stretch's clock: big blobs, so a stretch of current moves together
        float[] noiseB = Noise(w, h, photo.P * 0.06f, rng);     // speed and direction wander

        var rowY = new List<int>(); var rowFrom = new List<int>(); var rowStart = new List<int>();
        var dx = new List<float>(); var dy = new List<float>(); var clock = new List<float>(); var strength = new List<short>();
        int span = Math.Max(1, photo.WaterBottom - photo.WaterTop);
        for (int y = Math.Max(0, photo.WaterTop); y <= photo.WaterBottom; y++)
        {
            int row = y * w, from = -1, to = -1;
            for (int x = 0; x < w; x++)
                if (photo.Water[row + x] != 0) { if (from < 0) from = x; to = x; }
            if (from < 0) continue;
            rowY.Add(y); rowFrom.Add(from); rowStart.Add(dx.Count);
            float near = (y - photo.WaterTop) / (float)span;               // 0 far ... 1 nearest
            for (int x = from; x <= to + 1; x++)                           // one extra, so the last run ends cleanly
            {
                int i = row + Math.Min(x, w - 1);
                // The direction: along the nearest stretch of the channel's
                // path, wandering a little either way (up to about 15 degrees).
                PointF dir = Along(line, x, y);
                float wander = (noiseB[i] - 0.5f) * 0.5f;
                float c = MathF.Cos(wander), s = MathF.Sin(wander);
                float fx = dir.X * c - dir.Y * s, fy = dir.X * s + dir.Y * c;
                float mid = middle[i] / 255f;
                float v = photo.P * speed * (0.30f + 0.70f * near) * (0.35f + 0.65f * mid * mid) * (0.67f + 0.67f * noiseB[i]);
                dx.Add(-fx * v * Cycle);                                   // pointing UPSTREAM: each pixel shows the texture from upstream
                dy.Add(-fy * v * Cycle);
                clock.Add(noiseA[i]);
                strength.Add((short)(photo.Water[i] * (255 - rock[i]) / 255));   // 0 on a rock or dry land, up to 255 in open water
            }
        }
        _rowY = rowY.ToArray(); _rowFrom = rowFrom.ToArray(); _rowStart = rowStart.ToArray();
        _dx = dx.ToArray(); _dy = dy.ToArray(); _clock = clock.ToArray(); _strength = strength.ToArray();
    }

    public void Update(double dt) => _time += dt;

    /// <summary>
    /// Swaps the still fine texture in the frame's water for the travelling
    /// one. The frame must already hold the backdrop.
    /// </summary>
    /// <param name="brightness">1 in daylight; less at dusk, when the texture is dimmer along with everything else.</param>
    public void Draw(FrameBuffer fb, float brightness = 1f)
    {
        int w = fb.Width, h = fb.Height, block = _block;
        uint[] frame = fb.Pixels, detail = _detail;
        int bright = (int)(Math.Clamp(brightness, 0, 1) * 256);
        float now = (float)(_time / Cycle % 1.0);          // the river's shared clock (0 to 1), from the double clock
        Parallel.For(0, _rowY.Length, n =>
        {
            int y = _rowY[n], k = _rowStart[n];
            if (y % block != 0) return;                     // rows inside a block are done by the block's top row
            int end = n + 1 < _rowY.Length ? _rowStart[n + 1] - 1 : _dx.Length - 1;
            for (int x = _rowFrom[n]; k < end; x++, k++)
            {
                if (x % block != 0 || _strength[k] == 0) continue;
                // This stretch's own clock, and the two sheets half a cycle apart.
                float a = now + _clock[k];
                a -= MathF.Floor(a);
                float b = a + 0.5f;
                if (b >= 1) b -= 1;
                // Smooth weights: sin squared and cos squared of the same
                // angle always add up to 1, and have no sharp corner at the
                // peak (a straight-line fade has one, and the eye sees a pump).
                float sa = MathF.Sin(MathF.PI * a);
                uint moving = Mix(Sample(detail, w, h, x + _dx[k] * b, y + _dy[k] * b),
                                  Sample(detail, w, h, x + _dx[k] * a, y + _dy[k] * a), (int)(sa * sa * 256));
                int mr = (int)((moving >> 16) & 0xFF) - 128, mg = (int)((moving >> 8) & 0xFF) - 128, mb = (int)(moving & 0xFF) - 128;
                for (int by = y; by < Math.Min(h, y + block); by++)
                    for (int bx = x; bx < Math.Min(w, x + block); bx++)
                    {
                        int at = by * w + bx;
                        if (_photo.Water[at] == 0) continue;           // a block can straddle the bank: leave the dry part alone
                        // Out goes this pixel's own still texture, in comes the
                        // travelling one; by how much depends on this pixel's
                        // strength (0 on a rock) and the light.
                        int str = _strength[k] * bright >> 8;
                        if (str == 0) continue;
                        uint still = detail[at], px = frame[at];
                        int dr = (mr - ((int)((still >> 16) & 0xFF) - 128)) * str >> 8;
                        int dg = (mg - ((int)((still >> 8) & 0xFF) - 128)) * str >> 8;
                        int db = (mb - ((int)(still & 0xFF) - 128)) * str >> 8;
                        int r = Math.Clamp((int)((px >> 16) & 0xFF) + dr, 0, 255);
                        int g = Math.Clamp((int)((px >> 8) & 0xFF) + dg, 0, 255);
                        int bl = Math.Clamp((int)(px & 0xFF) + db, 0, 255);
                        frame[at] = (uint)((r << 16) | (g << 8) | bl);
                    }
            }
        });
    }

    /// <summary>
    /// The fine texture of the photo (photo minus a blur of radius "r"), shifted
    /// up by 128 per channel. Also finds the rocks: in the water, a rock is a
    /// dark lump with little colour; their texture is set to "no change" (128)
    /// so it is never carried off downstream.
    /// </summary>
    private uint[] FineTexture(uint[] px, int w, int h, int r, out byte[] rock)
    {
        var red = new byte[px.Length]; var green = new byte[px.Length]; var blue = new byte[px.Length];
        for (int i = 0; i < px.Length; i++) { red[i] = (byte)(px[i] >> 16); green[i] = (byte)(px[i] >> 8); blue[i] = (byte)px[i]; }
        byte[] br = Blur(red, w, h, r), bg = Blur(green, w, h, r), bb = Blur(blue, w, h, r);
        // A rock: darker than the water around it and nearly grey. Measured on
        // the blurred copy (a rock is a lump, not a speck), then spread a
        // little so its rim is left alone too.
        var rocky = new byte[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            if (_photo.Water[i] == 0) continue;
            int lum = (br[i] * 3 + bg[i] * 6 + bb[i]) / 10;
            rocky[i] = (byte)(Math.Clamp((92 - lum) * 255 / 30, 0, 255));
        }
        rock = Blur(rocky, w, h, Math.Max(1, r * 2));
        for (int i = 0; i < rock.Length; i++) rock[i] = (byte)Math.Min(255, rock[i] * 2);
        var detail = new uint[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            if (_photo.Water[i] == 0 || rock[i] > 200) { detail[i] = 0x808080; continue; }
            int dr = Math.Clamp(128 + red[i] - br[i], 0, 255), dg = Math.Clamp(128 + green[i] - bg[i], 0, 255), db = Math.Clamp(128 + blue[i] - bb[i], 0, 255);
            detail[i] = (uint)((dr << 16) | (dg << 8) | db);
        }
        return detail;
    }

    /// <summary>A colour at a spot between pixels: the four around it, each weighted by how near it is.</summary>
    private static uint Sample(uint[] px, int w, int h, float x, float y)
    {
        x = Math.Clamp(x, 0, w - 1.001f);
        y = Math.Clamp(y, 0, h - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        int fx = (int)((x - x0) * 256), fy = (int)((y - y0) * 256);
        int i = y0 * w + x0;
        return Mix(Mix(px[i], px[i + 1], fx), Mix(px[i + w], px[i + w + 1], fx), fy);
    }

    /// <summary>
    /// A blend of two colours: t = 0 gives a, 256 gives b. Red and blue are
    /// blended TOGETHER in one sum: in 0x00RRGGBB they sit 16 bits apart,
    /// and each product fits in 16 bits, so they never spill into each
    /// other. Green gets its own sum. This runs millions of times a frame.
    /// </summary>
    private static uint Mix(uint a, uint b, int t)
    {
        uint s = (uint)t, r = 256 - s;
        uint rb = ((a & 0xFF00FF) * r + (b & 0xFF00FF) * s) >> 8 & 0xFF00FF;
        uint g = ((a & 0x00FF00) * r + (b & 0x00FF00) * s) >> 8 & 0x00FF00;
        return rb | g;
    }

    /// <summary>The direction (unit length) of the stretch of the path nearest to (x, y).</summary>
    private static PointF Along(PointF[] line, float x, float y)
    {
        float best = float.MaxValue;
        PointF dir = new(0, 1);
        for (int i = 0; i + 1 < line.Length; i++)
        {
            PointF a = line[i], b = line[i + 1];
            float vx = b.X - a.X, vy = b.Y - a.Y, len2 = vx * vx + vy * vy;
            float t = len2 > 0 ? Math.Clamp(((x - a.X) * vx + (y - a.Y) * vy) / len2, 0, 1) : 0;
            float px = a.X + vx * t - x, py = a.Y + vy * t - y, d = px * px + py * py;
            if (d < best)
            {
                best = d;
                float len = MathF.Sqrt(len2);
                dir = len > 0 ? new(vx / len, vy / len) : dir;
            }
        }
        return dir;
    }

    /// <summary>
    /// Smooth random blobs, 0 to 1 per pixel: random values on a coarse grid
    /// "cell" pixels apart, blended smoothly between grid points.
    /// </summary>
    private static float[] Noise(int w, int h, float cell, Random rng)
    {
        cell = Math.Max(4, cell);
        int gw = (int)(w / cell) + 2, gh = (int)(h / cell) + 2;
        var grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)rng.NextDouble();
        var noise = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            float gy = y / cell;
            int y0 = (int)gy;
            float ty = gy - y0;
            ty = ty * ty * (3 - 2 * ty);                    // eased, so the blobs have no creases along the grid
            for (int x = 0; x < w; x++)
            {
                float gx = x / cell;
                int x0 = (int)gx;
                float tx = gx - x0;
                tx = tx * tx * (3 - 2 * tx);
                float top = grid[y0 * gw + x0] + (grid[y0 * gw + x0 + 1] - grid[y0 * gw + x0]) * tx;
                float bot = grid[(y0 + 1) * gw + x0] + (grid[(y0 + 1) * gw + x0 + 1] - grid[(y0 + 1) * gw + x0]) * tx;
                noise[y * w + x] = top + (bot - top) * ty;
            }
        }
        return noise;
    }

    /// <summary>Average of the neighbours within r pixels, sideways then up and down (a running total keeps it quick).</summary>
    private static byte[] Blur(byte[] src, int w, int h, int r)
    {
        var tmp = new byte[src.Length];
        var dst = new byte[src.Length];
        int span = 2 * r + 1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w, sum = 0;
            for (int x = -r; x <= r; x++) sum += src[row + Math.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = (byte)(sum / span);
                sum += src[row + Math.Min(w - 1, x + r + 1)] - src[row + Math.Max(0, x - r)];
            }
        }
        for (int x = 0; x < w; x++)
        {
            int sum = 0;
            for (int y = -r; y <= r; y++) sum += tmp[Math.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = (byte)(sum / span);
                sum += tmp[Math.Min(h - 1, y + r + 1) * w + x] - tmp[Math.Max(0, y - r) * w + x];
            }
        }
        return dst;
    }
}
