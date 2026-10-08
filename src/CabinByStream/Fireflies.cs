using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// Fireflies over the meadow, all evening long.
///
/// HOW ONE MOVES. Nothing is tracked from frame to frame: where a firefly is
/// comes from a formula of the clock. Each has a home spot in the grass and
/// wanders around it on two gentle sine waves per direction, at different
/// speeds, which never quite repeats: like a toy on a spring that hangs from
/// a swinging arm. Fireflies near the bottom of the picture are nearer, so
/// they are drawn a little bigger and wander a little farther.
///
/// HOW IT BLINKS. Each has its own cycle of 2.5 to 5 seconds. For about a
/// second of it the light swells up and fades back down (a sine wave
/// squared, so it eases in and out); the rest of the time it is dark. The
/// cycles all start at different moments, so the meadow twinkles at random.
///
/// THE REFLECTION. One hovering over the stream is mirrored in the water,
/// as far below the surface as it is above it. We do not know the exact
/// height of each one, so each is given a hover height, and its reflection
/// is drawn that far again below, only on open water (the stencil), so
/// nothing is mirrored onto a stone or a bank.
/// </summary>
internal sealed class Fireflies
{
    private struct Fly
    {
        public float X0, Y0;                       // home
        public float Ax1, Wx1, Px1, Ax2, Wx2, Px2; // sideways wander: two waves (how far, how fast, where it starts)
        public float Ay1, Wy1, Py1, Ay2, Wy2, Py2; // up-and-down wander
        public double Period, On, Offset;          // the blink
        public float Hover;                        // height above the ground, for the reflection
        public int Size;                           // 0 = far, 1 = near
    }

    private static readonly Color Lime = Color.FromArgb(206, 255, 112);

    private readonly MeadowScenery _s;
    private readonly Fly[] _flies;
    private readonly Sprite[] _glow = new Sprite[2], _halo = new Sprite[2];

    /// <param name="amount">1 = normal (a few dozen on a wide screen), 0 = none.</param>
    /// <param name="breeze">Leans every wander a little to the right.</param>
    public Fireflies(MeadowScenery s, float amount, float breeze, Random rng)
    {
        _s = s;
        float u = s.U, w = s.Width, h = s.Height;
        int count = (int)(44 * amount * (w / u) / (16f / 9f));
        _flies = new Fly[Math.Max(0, count)];
        float R() => (float)rng.NextDouble();
        for (int i = 0; i < _flies.Length; i++)
        {
            float p = MathF.Pow(R(), 0.8f);                                 // most are in the nearer meadow
            float y = s.Horizon + u * 0.08f + p * (h - s.Horizon - u * 0.08f);
            float near = 0.5f + 0.8f * p;                                   // bigger wanders when nearer
            _flies[i] = new Fly
            {
                X0 = R() * w, Y0 = y,
                Ax1 = u * (0.012f + 0.03f * R()) * near, Wx1 = 0.35f + 0.5f * R(), Px1 = R() * MathF.Tau,
                Ax2 = u * (0.004f + 0.010f * R()) * near, Wx2 = 1.2f + 1.2f * R(), Px2 = R() * MathF.Tau,
                Ay1 = u * (0.008f + 0.02f * R()) * near, Wy1 = 0.3f + 0.5f * R(), Py1 = R() * MathF.Tau,
                Ay2 = u * (0.003f + 0.008f * R()) * near, Wy2 = 1.3f + 1.4f * R(), Py2 = R() * MathF.Tau,
                Period = 2.5 + 2.5 * R(), On = 0.8 + 0.6 * R(), Offset = R() * 5,
                Hover = u * (0.02f + 0.03f * R()),
                Size = p > 0.55f ? 1 : 0,
            };
            _flies[i].X0 += breeze * u * 0.02f;                             // a breeze drifts the whole crowd a touch to the right
        }
        for (int k = 0; k < 2; k++)
        {
            int r = Math.Max(3, (int)(u * (0.009f + 0.004f * k)));            // pixel floor: 3, so a light still shows in the preview box
            _glow[k] = Sprite.Glow(r, Lime);
            _halo[k] = Sprite.Glow(Math.Max(7, (int)(u * (0.026f + 0.010f * k))), Lime);
        }
    }

    /// <param name="time">The scene clock, in seconds (a double: it runs for days).</param>
    public void Draw(FrameBuffer fb, double time)
    {
        int w = _s.Width, h = _s.Height;
        float t = (float)(time % 100000.0);   // wrapped, so the sine arguments never grow too large for a float
        foreach (ref readonly Fly f in _flies.AsSpan())
        {
            // The blink: a smooth swell for "On" seconds of each cycle, dark the rest. Worked out in doubles.
            double ph = (time + f.Offset) % f.Period;
            if (ph >= f.On) continue;
            float s = (float)Math.Sin(Math.PI * ph / f.On);
            float b = s * s;
            if (b < 0.03f) continue;

            float x = f.X0 + f.Ax1 * MathF.Sin(t * f.Wx1 + f.Px1) + f.Ax2 * MathF.Sin(t * f.Wx2 + f.Px2);
            float y = f.Y0 + f.Ay1 * MathF.Sin(t * f.Wy1 + f.Py1) + f.Ay2 * MathF.Sin(t * f.Wy2 + f.Py2);
            x = MathF.Round(x);
            y = MathF.Round(y);
            if (x < 0 || x >= w || y < 0 || y >= h) continue;

            // The reflection first (underneath the light), if the water is below it.
            int ry = (int)(y + 2 * f.Hover);
            if (ry < h && _s.OpenWater[ry * w + (int)x])
                _glow[f.Size].DrawCentered(fb, x + MathF.Sin(t * 3f + f.Px1), ry, 0.30f * b, _s.OpenWater);

            _halo[f.Size].DrawCentered(fb, x, y, 0.5f * b);
            _glow[f.Size].DrawCentered(fb, x, y, b);
        }
    }
}
