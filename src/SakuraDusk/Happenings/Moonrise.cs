using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraDusk.Happenings;

/// <summary>
/// Mikazuki, the "three-day moon": a thin crescent hangs low over the far
/// hills in the evening glow, and slowly sinks behind them, like a figure in
/// a woodblock print.
///
/// Why sinking and not rising? A young crescent is only ever seen in the
/// evening, low in the west, following the sun down: it SETS at dusk. (The
/// class keeps its first name, Moonrise, so the test commands that use it
/// still work; it now shows a moonset.)
///
/// Why do the horns point the way they do? The bright edge of a crescent
/// always faces the sun. The sun is low on the right, so the bright edge is
/// on the lower right and the two horns curve away from the sun, toward the
/// upper left. We make the crescent the way you would cut one from paper: a
/// round disc, minus a second round disc slid toward the upper left. What is
/// left is a thin curved sliver. The rest of the disc is not quite black: a
/// whisper of "earthshine" (light bounced off the Earth) lets you just see
/// it, and a soft glow sits around the whole thing.
///
/// It sinks BEHIND the farthest hills: it ends lower than the highest point
/// of the hill line along its path, and the sky stencil hides whatever part
/// is already below the ridge. It is stamped through that stencil, so the
/// hills and branches always stay in front.
/// </summary>
internal sealed class Moonrise : Happening
{
    private readonly DuskScenery _s;
    private readonly Sprite _glow, _moon;
    private readonly float _r;                                    // the moon's radius in pixels
    private float _x0, _y0, _x1, _y1;                             // where it starts and ends (centers)

    public override float Seconds => 18f;
    public override string? Claims => "night,clouds";

    public Moonrise(DuskScenery s)
    {
        _s = s;
        _r = Math.Max(3f, s.U * 0.025f);                          // 0.05 U across
        int gr = (int)(_r * 3.4f);
        _glow = Sprite.Paint(gr * 2, gr * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, gr * 2, gr * 2);
            using var b = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(120, 255, 240, 200),
                SurroundColors = [Color.FromArgb(0, 255, 240, 200)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(b, path);
        });

        int size = (int)(_r * 2) + 6;
        float c = size / 2f;
        _moon = Sprite.Paint(size, size, g =>
        {
            // Earthshine: the whole disc, barely there.
            using var dim = new SolidBrush(Color.FromArgb(10, 225, 215, 235));
            g.FillEllipse(dim, c - _r, c - _r, _r * 2, _r * 2);

            // The crescent: the disc minus a copy slid toward the upper left.
            float slide = _r * 0.36f, cut = _r * 1.04f;           // a slightly bigger cutter and a modest slide leave a thin sliver
            float cx = c - slide * 0.6f, cy = c - slide * 0.8f;
            using var disc = new GraphicsPath();
            disc.AddEllipse(c - _r, c - _r, _r * 2, _r * 2);
            using var bite = new GraphicsPath();
            bite.AddEllipse(cx - cut, cy - cut, cut * 2, cut * 2);
            using var sliver = new Region(disc);
            sliver.Exclude(bite);
            using var lemon = new SolidBrush(Color.FromArgb(255, 248, 220));
            g.FillRegion(lemon, sliver);
        });
    }

    public override void Begin(Random rng)
    {
        float u = _s.U;
        float x = _s.Width * (0.10f + 0.25f * (float)rng.NextDouble());
        float slant = u * 0.04f;                                  // it drifts a little to the right as it sinks
        // The highest point of the far hills anywhere along the path: the moon
        // starts below the LOWEST ridge height there, so it is fully hidden.
        float lowest = 0;
        for (int i = 0; i <= 8; i++)
            lowest = Math.Max(lowest, Brushwork.RidgeYAt(_s.Ridges[0], x - slant * i / 8f));
        _x0 = x; _x1 = x - slant;
        _y0 = lowest + _r + 3;
        _y1 = _y0 - u * 0.18f - _r * 2;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float p = Smooth(t / Seconds);                            // eased, so the sinking starts and stops gently
        // From high (the "1" end) down to behind the hills (the "0" end).
        float x = _x1 + (_x0 - _x1) * p, y = _y1 + (_y0 - _y1) * p;
        float on = Fade(t, Seconds, 3f, 1f);                     // it appears as the sky darkens; the hill hides it at the end
        _glow.DrawCentered(fb, x, y, 0.8f * on, _s.OpenSky);
        _moon.DrawCentered(fb, x, y, on, _s.OpenSky);
    }
}
