using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// The northern lights: two or three see-through curtains of green and
/// violet-pink light ripple across the upper sky for about twenty seconds,
/// then fade away.
///
/// How a curtain is built: it is a row of thin vertical strokes standing side
/// by side, like the bars of a picket fence seen in the dark. Every stroke
/// starts on the curtain's BASE LINE (a slow wave across the screen that also
/// drifts sideways as time passes) and reaches UP by a height that changes
/// along the screen and over time (three sine waves of different speeds added
/// together, so it never repeats in an obvious way). Each stroke is drawn as
/// three stacked pieces: bright green at the bottom, blending to violet-pink
/// at the top, and fainter and fainter going up, which is how real auroras
/// look: a sharp bright lower edge and a soft ghostly top.
///
/// The ripples you see travelling along a curtain are just a brightness wave:
/// a sine of (position along the screen minus time), so the bright patches
/// slide sideways.
///
/// Drawn only where the sky is open (the OpenSky stencil), so the mountains
/// and the corner branches stay in front of it.
///
/// It is wide, so it is the costliest of the sky happenings: strokes are
/// spaced a few pixels apart and kept short to stay cheap at 4K.
/// </summary>
internal sealed class Aurora : Happening
{
    private const int MaxCurtains = 3;
    private static readonly Color Green = Color.FromArgb(120, 255, 170);
    private static readonly Color Violet = Color.FromArgb(220, 120, 255);

    private readonly ChristmasScenery _s;
    private int _curtains;
    // Every curtain rolls its own numbers in Begin: where its base sits, how tall it is, and
    // the speeds and offsets of its waves.
    private readonly float[] _base = new float[MaxCurtains];
    private readonly float[] _from = new float[MaxCurtains], _to = new float[MaxCurtains];   // the part of the width each curtain spans, 0 to 1       // base line height, as a fraction of the height
    private readonly float[] _tall = new float[MaxCurtains];       // how tall it can get, in U
    private readonly float[] _gain = new float[MaxCurtains];       // overall brightness of this curtain
    private readonly float[,] _wave = new float[MaxCurtains, 12];  // 12 numbers: waves for base, height (x3) and ripple

    public override float Seconds => 22f;
    public override string? Claims => "aurora";

    // Strokes are spaced one and a half widths apart (a narrow dark gap between rays), which also saves a third of the pixels to paint at 4K.
    // One ready-painted stroke for each of Levels heights. A stroke is a soft-edged vertical bar,
    // bright green at the foot and fading through violet-pink to faint at the top.
    private const int Levels = 32;
    private readonly Sprite[] _stroke = new Sprite[Levels];
    private readonly int _stride;                                 // pixels from one stroke to the next
    private readonly float _tallScale;                             // taller curtains where it is cheap (a small screen)
    private readonly int _step;                                    // pixels between strokes (and the width of one)

    public Aurora(ChristmasScenery s)
    {
        _s = s;
        _step = Math.Max(2, (int)(s.U * 0.008f));
        // Every pixel of every stroke costs time, and a curtain is wide. On a
        // big (4K) screen the curtains are a little shorter and there are
        // only two of them; and on every screen each curtain spans only part
        // of the sky (see Begin), as a real aurora does. The strokes always
        // stand side by side: spacing them out to save time made a comb.
        bool big = s.U > 1400;
        _stride = _step;
        _tallScale = big ? 0.9f : 1.35f;
        for (int i = 0; i < Levels; i++)
        {
            // Heights run from 0.03 U up to 0.17 U in even steps.
            int h = Math.Max(4, (int)(s.U * (0.03f + 0.14f * i / (Levels - 1))));
            _stroke[i] = Sprite.Paint(_step, h, g =>
            {
                // Paint one pixel-wide column at a time so the bar's sides can be soft: the middle
                // of the bar is full strength and its two edges are dim, like a ray of light.
                for (int x = 0; x < _step; x++)
                {
                    float side = _step < 3 ? 1f : 0.7f + 0.3f * MathF.Sin((x + 0.5f) / _step * MathF.PI);
                    using var brush = new LinearGradientBrush(new Rectangle(x, 0, 1, h), Violet, Green, LinearGradientMode.Vertical)
                    {
                        InterpolationColors = new ColorBlend
                        {
                            Positions = [0f, 0.2f, 0.55f, 1f],
                            Colors = [Color.FromArgb(0, Violet),
                                      Color.FromArgb((int)(255 * 0.10f * side), Violet),
                                      Color.FromArgb((int)(255 * 0.22f * side), Mix(Green, Violet, 0.5f)),
                                      Color.FromArgb((int)(255 * 0.40f * side), Green)],
                        },
                    };
                    g.FillRectangle(brush, x, 0, 1, h);
                }
            });
        }
    }

    public override void Begin(Random rng)
    {
        _curtains = _s.U > 1400 ? 2 : 2 + rng.Next(2);             // two or three (two on a big screen: see the constructor)
        for (int i = 0; i < MaxCurtains; i++)
        {
            // Spread the base lines down the sky so the curtains overlap but are not twins.
            _base[i] = 0.17f + 0.07f * i + 0.04f * (float)rng.NextDouble();
            // Each curtain covers about two thirds of the width, starting somewhere in the left third.
            _from[i] = 0.03f + 0.30f * (float)rng.NextDouble();
            _to[i] = Math.Min(0.97f, _from[i] + 0.58f + 0.10f * (float)rng.NextDouble());
            _tall[i] = 0.075f + 0.035f * (float)rng.NextDouble();
            _gain[i] = 1.2f + 0.3f * (float)rng.NextDouble();
            for (int k = 0; k < 12; k++) _wave[i, k] = (float)rng.NextDouble();
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 4f, 4f);
        if (on <= 0) return;
        float U = _s.U, W = _s.Width, H = _s.Height;
        double td = t;                                              // sine of a double: no precision worries

        for (int c = 0; c < _curtains; c++)
        {
            var w = new float[12];
            for (int k = 0; k < 12; k++) w[k] = _wave[c, k];
            float speed = 0.7f + 0.6f * w[0];
            float x0 = W * _from[c], x1 = W * _to[c];

            for (int x = (int)x0; x < x1; x += _stride)
            {
                float xn = x / W;                                   // 0 to 1 across the screen

                // The base line: a slow big wave, drifting. Its dip and rise are a few percent of U.
                float by = H * _base[c] + U * 0.05f * (float)Math.Sin(xn * (3 + 3 * w[1]) + td * 0.25 * speed + w[2] * 6.28);

                // The height: three waves of different speeds added together.
                float tall = 0.55f
                    + 0.22f * (float)Math.Sin(xn * (5 + 4 * w[3]) + td * 0.55 * speed + w[4] * 6.28)
                    + 0.14f * (float)Math.Sin(xn * (11 + 6 * w[5]) - td * 0.9 + w[6] * 6.28)
                    + 0.10f * (float)Math.Sin(xn * 23 + td * 1.7 + w[7] * 6.28);
                float h = Math.Max(U * 0.03f, U * _tall[c] * _tallScale * tall);
                h = Math.Min(h, by - H * 0.03f);                    // never above the top of the sky
                if (h < U * 0.03f) continue;

                // The travelling ripple: a brightness wave that slides along the curtain.
                float ripple = 0.7f + 0.3f * (float)Math.Sin(xn * (9 + 5 * w[8]) - td * (0.8 + 0.6 * w[9]) + w[10] * 6.28);
                // Fade the two ends of the curtain so it does not stop dead at a vertical edge.
                float edge = Math.Min(1f, Math.Min(xn - _from[c], _to[c] - xn) / 0.12f);
                // A faint stripe pattern across the strokes, so the curtain reads as vertical rays of light.
                float ray = 0.85f + 0.15f * (float)Math.Sin(x * 0.35 + w[11] * 6.28 + td * 0.5);
                // Keep clear of the moon: a bright disc under a veil of green would look like a smudge.
                float moon = Math.Clamp((MathF.Sqrt((x - _s.Moon.At.X) * (x - _s.Moon.At.X) + (by - h * 0.5f - _s.Moon.At.Y) * (by - h * 0.5f - _s.Moon.At.Y)) / _s.Moon.R - 1.1f) / 1.2f, 0f, 1f);
                float a = Math.Min(1f, on * ripple * ray * edge * moon * _gain[c]);
                if (a <= 0.02f) continue;

                // Pick the ready-made stroke nearest this height and stand it on the base line.
                int level = Math.Clamp((int)MathF.Round((h / U - 0.03f) / 0.14f * (Levels - 1)), 0, Levels - 1);
                _stroke[level].Draw(fb, x, (int)by - _stroke[level].Height, a, _s.OpenSky);
            }
        }
    }

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}
