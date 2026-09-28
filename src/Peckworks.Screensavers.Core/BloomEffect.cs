namespace Peckworks.Screensavers.Core;

/// <summary>
/// Adds a soft glow ("bloom") around everything bright in a FrameBuffer.
///
/// FEYNMAN VERSION: squint at a neon sign at night. The letters look like they
/// bleed light into the dark air around them. Cameras and eyes do that with
/// anything bright. The Matrix code has exactly that glow, and without it the
/// rain looks flat, like text in a terminal.
///
/// How we fake it, in three steps:
///
///   1. SHRINK. Make a copy of the picture 4 times smaller in each direction
///      (so 16 times fewer pixels). Each small pixel is the average of a 4x4
///      block of big pixels. This makes the next step 16x cheaper.
///
///   2. SMEAR. Blur the small copy. A "box blur" replaces each pixel with the
///      average of its neighbors in a row, then in a column. Doing it twice
///      turns the boxy smear into a smooth, round-looking fuzz (a known trick:
///      repeated box blurs approximate a Gaussian, the "bell curve" blur).
///
///   3. ADD BACK. Stretch the blurry copy back to full size and ADD its light
///      on top of the sharp original. Adding light (not replacing) is why dark
///      areas near a bright character pick up a faint green haze, while the
///      sharp character itself stays crisp.
/// </summary>
public sealed class BloomEffect
{
    private readonly int _width, _height;      // full-size picture
    private readonly int _factor;              // shrink factor (4 means 1/4 size)
    private readonly int _smallW, _smallH;     // shrunk picture
    private readonly int _radius;              // blur reach, in small pixels

    // The shrunk picture, one array per color channel, as decimals (floats)
    // so repeated averaging does not lose precision to rounding.
    private readonly float[] _r, _g, _b;
    private readonly float[] _scratch;         // temporary space for the blur passes

    // Precomputed lookup tables for step 3 so we do not redo the same
    // division millions of times per frame. For each big column x we remember
    // which two small columns it sits between and how far along (0..1).
    private readonly int[] _x0, _x1; private readonly float[] _xw;
    private readonly int[] _y0, _y1; private readonly float[] _yw;

    /// <summary>How strong the glow is. 0 = off, 1 = normal, 2 = heavy.</summary>
    public float Strength { get; set; }

    public BloomEffect(int width, int height, int factor, int blurRadius, float strength)
    {
        _width = width;
        _height = height;
        _factor = Math.Max(1, factor);
        _radius = Math.Max(1, blurRadius);
        Strength = strength;

        // Round UP so the last partial block still gets a small pixel.
        _smallW = (width + _factor - 1) / _factor;
        _smallH = (height + _factor - 1) / _factor;

        int n = _smallW * _smallH;
        _r = new float[n]; _g = new float[n]; _b = new float[n];
        _scratch = new float[n];

        BuildStretchTable(width, _smallW, out _x0, out _x1, out _xw);
        BuildStretchTable(height, _smallH, out _y0, out _y1, out _yw);
    }

    /// <summary>Runs shrink, smear, add-back on the given picture, in place.</summary>
    public void Apply(FrameBuffer fb)
    {
        if (Strength <= 0.001f) return;
        Shrink(fb);
        Blur(_r); Blur(_g); Blur(_b);
        AddBack(fb);
    }

    // ---------------------------------------------------------------- step 1

    private void Shrink(FrameBuffer fb)
    {
        uint[] px = fb.Pixels;

        // Parallel.For splits the rows across all your CPU cores. Each core does
        // different rows, and no two cores ever write the same small pixel, so
        // they never trip over each other.
        Parallel.For(0, _smallH, sy =>
        {
            int yStart = sy * _factor;
            int yEnd = Math.Min(yStart + _factor, _height);
            for (int sx = 0; sx < _smallW; sx++)
            {
                int xStart = sx * _factor;
                int xEnd = Math.Min(xStart + _factor, _width);
                int sumR = 0, sumG = 0, sumB = 0, count = 0;
                for (int y = yStart; y < yEnd; y++)
                {
                    int row = y * _width;
                    for (int x = xStart; x < xEnd; x++)
                    {
                        uint c = px[row + x];
                        // Unpack 0x00RRGGBB: shift the wanted byte down to the
                        // bottom, then "& 0xFF" keeps only that one byte.
                        sumR += (int)((c >> 16) & 0xFF);
                        sumG += (int)((c >> 8) & 0xFF);
                        sumB += (int)(c & 0xFF);
                        count++;
                    }
                }
                int i = sy * _smallW + sx;
                float inv = 1f / count;
                _r[i] = sumR * inv; _g[i] = sumG * inv; _b[i] = sumB * inv;
            }
        });
    }

    // ---------------------------------------------------------------- step 2

    private void Blur(float[] channel)
    {
        // Two rounds of (sideways smear, then up-down smear).
        for (int pass = 0; pass < 2; pass++)
        {
            BlurRows(channel, _scratch);
            BlurColumns(_scratch, channel);
        }
    }

    /// <summary>
    /// Sideways box blur. The clever bit is the "running sum": to average a
    /// window of 7 pixels at every position we do NOT re-add 7 numbers each
    /// time. We keep a total, and as the window slides one step right we add the
    /// pixel that just entered and subtract the one that just left. Like a
    /// train's passenger count: people get on at the front, off at the back.
    /// </summary>
    private void BlurRows(float[] src, float[] dst)
    {
        int w = _smallW, r = _radius;
        float inv = 1f / (2 * r + 1);
        Parallel.For(0, _smallH, y =>
        {
            int row = y * w;
            float sum = 0;
            for (int i = -r; i <= r; i++) sum += src[row + Math.Clamp(i, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                dst[row + x] = sum * inv;
                sum += src[row + Math.Min(x + r + 1, w - 1)] - src[row + Math.Max(x - r, 0)];
            }
        });
    }

    /// <summary>Same running-sum trick, sliding down each column instead.</summary>
    private void BlurColumns(float[] src, float[] dst)
    {
        int w = _smallW, h = _smallH, r = _radius;
        float inv = 1f / (2 * r + 1);
        Parallel.For(0, w, x =>
        {
            float sum = 0;
            for (int i = -r; i <= r; i++) sum += src[Math.Clamp(i, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum * inv;
                sum += src[Math.Min(y + r + 1, h - 1) * w + x] - src[Math.Max(y - r, 0) * w + x];
            }
        });
    }

    // ---------------------------------------------------------------- step 3

    private void AddBack(FrameBuffer fb)
    {
        uint[] px = fb.Pixels;
        float strength = Strength;
        int sw = _smallW;

        // "Bilinear" stretching: a big pixel sitting between four small pixels
        // takes a weighted blend of all four, weighted by how close it is to
        // each. That keeps the glow smooth instead of blocky.
        //
        // Speed trick: every big pixel in the same row blends the SAME two small
        // rows by the SAME amount. So for each big row we do that vertical blend
        // once (into "line"), and then each pixel only needs a sideways blend.
        // That is 3 blends per pixel instead of 9, which matters at 8 million
        // pixels (4K) per frame.
        Parallel.For(0, _height, y =>
        {
            int rowA = _y0[y] * sw, rowB = _y1[y] * sw;
            float wy = _yw[y];

            // stackalloc = a small scratch array on this thread's own quick-access
            // memory (the "stack"), thrown away automatically when the row is done.
            Span<float> lr = stackalloc float[sw], lg = stackalloc float[sw], lb = stackalloc float[sw];
            for (int sx = 0; sx < sw; sx++)
            {
                lr[sx] = Lerp(_r[rowA + sx], _r[rowB + sx], wy) * strength;
                lg[sx] = Lerp(_g[rowA + sx], _g[rowB + sx], wy) * strength;
                lb[sx] = Lerp(_b[rowA + sx], _b[rowB + sx], wy) * strength;
            }

            int outRow = y * _width;
            for (int x = 0; x < _width; x++)
            {
                int a = _x0[x], b = _x1[x];
                float wx = _xw[x];
                float gr = Lerp(lr[a], lr[b], wx);
                float gg = Lerp(lg[a], lg[b], wx);
                float gb = Lerp(lb[a], lb[b], wx);

                // Glow too faint to change the pixel at all: skip the work.
                if (gr + gg + gb < 1f) continue;

                uint p = px[outRow + x];
                int nr = (int)((p >> 16) & 0xFF) + (int)gr;
                int ng = (int)((p >> 8) & 0xFF) + (int)gg;
                int nb = (int)(p & 0xFF) + (int)gb;

                // A color channel cannot go above 255, so cap it ("saturate").
                // Otherwise 250 + 20 would wrap around to 14 and turn black.
                if (nr > 255) nr = 255;
                if (ng > 255) ng = 255;
                if (nb > 255) nb = 255;
                px[outRow + x] = (uint)((nr << 16) | (ng << 8) | nb);
            }
        });
    }

    /// <summary>"Linear interpolation": the point t of the way from a to b (t = 0..1).</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// For every big coordinate, work out the two small coordinates it falls
    /// between and the blend weight. Done once at startup.
    /// </summary>
    private void BuildStretchTable(int bigSize, int smallSize,
        out int[] lo, out int[] hi, out float[] weight)
    {
        lo = new int[bigSize]; hi = new int[bigSize]; weight = new float[bigSize];
        for (int i = 0; i < bigSize; i++)
        {
            // The center of big pixel i, measured in small-pixel units, minus half
            // a pixel so that small pixel k's center sits at exactly k.
            float s = (i + 0.5f) / _factor - 0.5f;
            if (s < 0) s = 0;
            int s0 = Math.Min((int)s, smallSize - 1);
            lo[i] = s0;
            hi[i] = Math.Min(s0 + 1, smallSize - 1);
            weight[i] = s - s0;
        }
    }
}
