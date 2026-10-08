namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// How a stretch of water moves, as fractions of the photo's height. A still
/// brook has small, slow, fine waves; a river running over stones has bigger,
/// faster ones.
/// </summary>
/// <param name="ShiftFar">How far a reflection is shifted sideways at the far edge of the water.</param>
/// <param name="ShiftNear">The same at the nearest water.</param>
/// <param name="WaveFar">How tall one wave is (crest to crest) at the far edge.</param>
/// <param name="WaveNear">The same at the nearest water.</param>
/// <param name="Across">How wide one sideways wave is.</param>
/// <param name="Speed">How fast the waves travel toward the viewer.</param>
public sealed record WaterLook(float ShiftFar, float ShiftNear, float WaveFar, float WaveNear, float Across, float Speed)
{
    /// <summary>A calm brook: the look tuned on Cotswold Brook.</summary>
    public static readonly WaterLook Brook = new(0.00035f, 0.00145f, 0.007f, 0.029f, 0.21f, 1.3f);
}

/// <summary>
/// Makes the still photo's water move: gentle ripples running through the
/// reflections, and now and then the rings of a trout rising.
///
/// FEYNMAN VERSION OF THE RIPPLES: look at the reflection of a house in a
/// brook. The water is never quite flat, so each strip of the reflection is
/// seen through a slightly tilted bit of surface, and appears shifted a
/// little to the side. Strips next to each other tilt differently, so a
/// straight wall in the reflection looks wobbly, and the wobble travels as
/// the little waves travel. We fake exactly that. For every water pixel we
/// do not show the photo's pixel at that spot, but the one a tiny distance
/// to the side, where "how far" is a wave that
/// depends on the row and slides along with time. Nothing new is painted:
/// the photo's own colours are only shuffled about, which is why it still
/// looks like the photo.
///
/// Nearer water (lower on the screen) gets bigger, wider waves: the same
/// size wave close up covers more pixels (perspective).
///
/// FEYNMAN VERSION OF A RISE: a trout sips a fly from the surface and leaves
/// a ring that spreads and fades. Seen from the bank at a low angle, a
/// circle on the water looks like a flat ellipse (a coin on a table seen
/// from the side). Each ring is drawn as a faint bright line (the ring's
/// near slope catches the sky) with a faint dark line just inside it.
/// </summary>
public sealed class WaterRipples
{
    private struct Rise
    {
        public float X, Y, Age;
    }

    // A sine wave written down once, in 4096 steps. Looking an answer up in
    // a table is much cheaper than working out a sine, and the ripples need
    // a few million of them a second at 4K.
    private const int SineSteps = 4096;
    private static readonly float[] Sine = Enumerable.Range(0, SineSteps)
        .Select(i => MathF.Sin(i * MathF.Tau / SineSteps)).ToArray();
    private const float StepsPerRadian = SineSteps / MathF.Tau;

    private readonly PhotoBackdrop _photo;
    private readonly float _amount, _riseRate;
    private readonly Random _rng;
    private readonly float[] _rowPhase, _rowAmp;     // per screen row: where the wave pattern is, and how far it shifts
    private readonly int[] _rowFirst, _rowLast;      // per screen row: the first and last pixel with any water (so dry stretches are skipped)
    private readonly float _kx;                      // how fast the wave changes along a row (long, slow sideways waves)
    private readonly float _speed1, _speed2;         // how fast the two waves travel (turns per second, times Tau)
    private readonly Rise[] _rises = new Rise[6];
    private int _riseCount;
    private double _nextRise;
    private double _time;

    /// <param name="amount">1 = normal ripples, 0 = a perfect mirror.</param>
    /// <param name="riseRate">1 = a rise every several seconds, 0 = no trout.</param>
    /// <param name="look">How this water moves: a still brook or a running river (see WaterLook).</param>
    public WaterRipples(PhotoBackdrop photo, WaterLook look, float amount, float riseRate, Random rng)
    {
        _photo = photo;
        _amount = amount;
        _riseRate = riseRate;
        _rng = rng;
        float p = photo.P;
        _rowPhase = new float[photo.Height];
        _rowAmp = new float[photo.Height];
        // Walk down the water one row at a time. Each row's wave is a step
        // further along than the row above; the step is smaller near the
        // bottom, so the waves get wider as they come nearer.
        float phase = 0;
        int span = Math.Max(1, photo.WaterBottom - photo.WaterTop);
        for (int y = photo.WaterTop; y <= photo.WaterBottom; y++)
        {
            float near = (y - photo.WaterTop) / (float)span;              // 0 far bank ... 1 nearest water
            float wavelength = p * (look.WaveFar + (look.WaveNear - look.WaveFar) * near);
            phase += MathF.Tau / wavelength;
            _rowPhase[y] = phase % (MathF.Tau * 64);                     // kept small (a whole number of turns, so the pattern does not jump)
            _rowAmp[y] = p * (look.ShiftFar + (look.ShiftNear - look.ShiftFar) * near) * amount;
        }
        _kx = MathF.Tau / (p * look.Across);
        _speed1 = look.Speed;
        _speed2 = look.Speed * (2.1f / 1.3f);   // the second wave a little over 1.6 times as fast: unrelated speeds, so the pattern never repeats
        _rowFirst = new int[photo.Height];
        _rowLast = new int[photo.Height];
        for (int y = 0; y < photo.Height; y++)
        {
            int row = y * photo.Width, first = photo.Width, last = -1;
            for (int x = 0; x < photo.Width; x++)
                if (photo.Water[row + x] != 0) { if (first > x) first = x; last = x; }
            _rowFirst[y] = first;
            _rowLast[y] = last;                       // a row with no water: first > last, so the loop below does nothing
        }
        _nextRise = 2 + 4 * rng.NextDouble();
    }

    public void Update(double dt)
    {
        _time += dt;

        for (int i = 0; i < _riseCount; i++)
        {
            _rises[i].Age += (float)dt;
            if (_rises[i].Age > RiseLife) _rises[i--] = _rises[--_riseCount];
        }

        // A new rise every 4 to 14 seconds (fewer or more with the knob), on
        // open water away from the very far edge, where a ring would be too
        // thin to read.
        if (_riseRate > 0 && _time >= _nextRise)
        {
            _nextRise = _time + (4 + 10 * _rng.NextDouble()) / _riseRate;
            if (_riseCount < _rises.Length && _photo.WaterBottom > _photo.WaterTop)
            {
                for (int tries = 0; tries < 30; tries++)
                {
                    int x = _rng.Next(_photo.Width);
                    int y = _photo.WaterTop + (int)((_photo.WaterBottom - _photo.WaterTop) * (0.2 + 0.75 * _rng.NextDouble()));
                    if (_photo.Water[y * _photo.Width + x] < 255) continue;
                    _rises[_riseCount++] = new Rise { X = x, Y = y };
                    break;
                }
            }
        }
    }

    private const float RiseLife = 4.5f;

    /// <summary>
    /// Redraws the water area of the frame from the backdrop, shifted by the
    /// ripples. Everything outside the water is left as it is.
    /// </summary>
    public void Draw(FrameBuffer fb, uint[] backdrop)
    {
        int w = fb.Width;
        uint[] frame = fb.Pixels;
        byte[] water = _photo.Water;
        // The two waves' clocks, each wrapped to whole turns in double
        // precision so they stay exact however long the saver runs.
        float t1 = (float)(_time * _speed1 % Math.Tau), t2 = (float)(_time * _speed2 % Math.Tau);
        // Each row of water only reads the backdrop and writes its own row of
        // the frame, so rows can be done at the same time on different
        // processor cores without getting in each other's way. Parallel.For
        // hands the rows out to every core (as BloomEffect.cs does).
        Parallel.For(Math.Max(0, _photo.WaterTop), _photo.WaterBottom + 1, y =>
        {
            float amp = _rowAmp[y];
            if (amp <= 0) return;
            float ph = _rowPhase[y];
            int row = y * w;
            // Two waves crossing: a main one running down the brook and a
            // smaller faster one at an angle. Adding two waves of unrelated
            // speeds is what keeps the pattern from looking like a machine
            // repeating itself.
            //
            // Along a row the waves change slowly (one sideways wave is a
            // fifth of the photo wide), so we work them out exactly only
            // every 16 pixels and slide in a straight line between: the same
            // picture for a fraction of the work. Like a dot-to-dot drawing
            // with the dots close enough together that you cannot see the
            // corners.
            //
            // Inside the loop everything is whole numbers counted in 256ths
            // of a pixel ("fixed point"): a shift of 1.5 pixels is 384. The
            // loop runs about a million times a frame at 4K, and whole-number
            // sums are the cheapest thing a processor does.
            const int Chunk = 16;
            int xFrom = _rowFirst[y], xTo = _rowLast[y];
            for (int cx = xFrom - xFrom % Chunk; cx <= xTo; cx += Chunk)
            {
                int cEnd = cx + Chunk;
                float a1 = Sine[(int)((ph - t1 + cx * _kx) * StepsPerRadian) & (SineSteps - 1)];
                float b1 = Sine[(int)((ph - t1 + cEnd * _kx) * StepsPerRadian) & (SineSteps - 1)];
                float a2 = Sine[(int)((ph * 1.7f + t2 - cx * _kx * 1.9f) * StepsPerRadian) & (SineSteps - 1)];
                float b2 = Sine[(int)((ph * 1.7f + t2 - cEnd * _kx * 1.9f) * StepsPerRadian) & (SineSteps - 1)];
                // The sideways shift at both ends of the chunk, in 256ths of a
                // pixel, and how much it changes per pixel (in 65536ths, for precision).
                int shiftA = (int)(amp * (a1 + 0.55f * a2) * 256 * 256), shiftB = (int)(amp * (b1 + 0.55f * b2) * 256 * 256);
                int shiftStep = (shiftB - shiftA) / Chunk;
                // The surface's tilt also catches a little more or less sky:
                // a brightness of 256 = unchanged, give or take a few percent.
                int shadeA = (int)(a1 * amp * 9f * 256), shadeB = (int)(b1 * amp * 9f * 256);
                int shadeStep = (shadeB - shadeA) / Chunk;
                int x = Math.Max(cx, xFrom), stop = Math.Min(cEnd, xTo + 1);
                int shift = shiftA + shiftStep * (x - cx), shade = shadeA + shadeStep * (x - cx);
                for (; x < stop; x++, shift += shiftStep, shade += shadeStep)
                {
                    int m = water[row + x];
                    if (m == 0) continue;
                    // How far to look sideways, scaled down at the soft edge of the water (m below 255).
                    int sx = (x << 8) + ((shift >> 8) * m >> 8);
                    int x0 = sx >> 8, fi = sx & 255;          // the whole pixel, and how far toward the next one
                    if (x0 < 0) { x0 = 0; fi = 0; } else if (x0 > w - 2) { x0 = w - 2; fi = 255; }

                    // Read the backdrop at the shifted spot. We blend the two
                    // nearest pixels by how close we are to each, so a shift
                    // of a third of a pixel still shows (without this the
                    // small ripples would stutter in whole-pixel steps).
                    uint pa = backdrop[row + x0], pb = backdrop[row + x0 + 1];
                    int k = 256 + ((shade >> 8) * m >> 8);
                    int r = (((int)((pa >> 16) & 0xFF) * (256 - fi) + (int)((pb >> 16) & 0xFF) * fi) >> 8) * k >> 8;
                    int g = (((int)((pa >> 8) & 0xFF) * (256 - fi) + (int)((pb >> 8) & 0xFF) * fi) >> 8) * k >> 8;
                    int bl = (((int)(pa & 0xFF) * (256 - fi) + (int)(pb & 0xFF) * fi) >> 8) * k >> 8;
                    if (r > 255) r = 255;
                    if (g > 255) g = 255;
                    if (bl > 255) bl = 255;
                    frame[row + x] = (uint)((r << 16) | (g << 8) | bl);
                }
            }
        });

        for (int i = 0; i < _riseCount; i++) DrawRise(fb, _rises[i]);
    }

    /// <summary>Two or three rings spreading from where the trout came up, fading as they grow.</summary>
    private void DrawRise(FrameBuffer fb, Rise rise)
    {
        float nearness = (rise.Y - _photo.WaterTop) / Math.Max(1f, _photo.WaterBottom - _photo.WaterTop);
        float scale = _photo.P * (0.012f + 0.03f * nearness);              // nearer rises look bigger
        for (int ring = 0; ring < 3; ring++)
        {
            float age = rise.Age - ring * 0.45f;                            // the rings set off one after another
            if (age <= 0) continue;
            float q = age / RiseLife;
            float rx = scale * MathF.Sqrt(age) * (1 - 0.25f * ring);        // spreads fast, then slows (the square root)
            float ry = rx * (0.18f + 0.12f * nearness);                     // flattened: we see the water at a low angle
            float alpha = 0.32f * (1 - q) * (1 - q) * (1 - 0.3f * ring) * Math.Min(1f, age * 3f);
            if (alpha < 0.01f || rx < 1.5f) continue;
            Ellipse(fb, rise.X, rise.Y, rx, ry, Color.FromArgb(236, 226, 214), alpha);
            Ellipse(fb, rise.X, rise.Y + 1, rx * 0.93f, ry * 0.93f, Color.FromArgb(20, 22, 18), alpha * 0.8f);
        }
    }

    private void Ellipse(FrameBuffer fb, float cx, float cy, float rx, float ry, Color c, float alpha)
    {
        int steps = Math.Max(12, (int)(rx * 0.8f));
        PointF prev = new(cx + rx, cy);
        for (int i = 1; i <= steps; i++)
        {
            float a = i * MathF.Tau / steps;
            PointF next = new(cx + rx * MathF.Cos(a), cy + ry * MathF.Sin(a));
            fb.Line(prev, next, c, alpha, 1.2f, _photo.OpenWater);       // only on open water: never across the bank or a reed
            prev = next;
        }
    }
}
