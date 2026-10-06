using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Golden clouds: a few long, thin cloud streaks drift slowly across the sunset
/// sky, their undersides and the ends that face the sun lit gold-pink, their
/// tops a dusky violet. It is the cloud of an anime sunset: flat, soft and
/// painted.
///
/// FEYNMAN VERSION: the sun is below the cloud, so the cloud is lit from
/// underneath, like your chin when you hold a torch under it. Each cloud is
/// three overlapping soft ovals. A soft oval is made by painting the same oval
/// five times, each a bit smaller and each only a little see-through, so the
/// edge fades out the way a watercolor wash does. Each oval is violet at the
/// top and gold-pink at the bottom, and the end of the cloud facing the sun
/// gets an extra brighter touch.
///
/// A sprite cannot be flipped, so each of four lengths is painted twice, once
/// with its bright end on the left and once on the right; in Begin each cloud
/// takes the one whose bright end faces the sun.
/// </summary>
internal sealed class GoldenClouds : Happening
{
    // Length and thickness of the four cloud shapes, in U.
    private static readonly (float Len, float Thick)[] Shapes = [(0.30f, 0.022f), (0.38f, 0.028f), (0.46f, 0.034f), (0.55f, 0.040f)];

    private readonly DuskScenery _s;
    private readonly Sprite[,] _cloud = new Sprite[4, 2];         // [shape, 0 = bright end on the left / 1 = on the right]
    private (int Shape, int Side, float X, float Y, float Speed, float Delay)[] _clouds = [];

    public override float Seconds => 18f;

    public GoldenClouds(DuskScenery s)
    {
        _s = s;
        for (int i = 0; i < 4; i++)
            for (int side = 0; side < 2; side++)
                _cloud[i, side] = Paint(Math.Max(12, (int)(s.U * Shapes[i].Len)), Math.Max(2f, s.U * Shapes[i].Thick), side == 1);
    }

    private static Sprite Paint(int len, float thick, bool brightRight)
    {
        int h = (int)(thick * 4) + 6;
        return Sprite.Paint(len, h, g =>
        {
            float cy = h / 2f;
            // Five ovals strung along the streak, each a different length and
            // height and nudged a little up or down, so it reads as a ragged
            // wisp and not as one tidy lens: (center along, width, height, nudge up/down).
            (float C, float W, float H, float Dy)[] ovals =
                [(0.16f, 0.34f, 0.45f, -0.10f), (0.34f, 0.44f, 0.80f, 0.06f), (0.52f, 0.54f, 1.00f, 0f), (0.70f, 0.42f, 0.72f, 0.08f), (0.86f, 0.28f, 0.42f, -0.06f)];
            foreach (var (c, wf, hf, dy) in ovals)
            {
                float ow = len * wf, oh = thick * hf, oy = cy + thick * dy;
                for (int k = 0; k < 8; k++)                       // eight nested copies make a very soft edge (fewer showed rings)
                {
                    float s = 1 - 0.115f * k;
                    float w = ow * s, hh = Math.Max(1.5f, oh * s);
                    var box = new RectangleF(len * c - w / 2, oy - hh / 2, w, hh);
                    using var br = new LinearGradientBrush(new RectangleF(box.X, box.Y - 1, box.Width, box.Height + 2), Color.Black, Color.Black, 90f)
                    {
                        InterpolationColors = new ColorBlend
                        {
                            Colors = [Color.FromArgb(46, 120, 72, 148), Color.FromArgb(48, 214, 124, 150), Color.FromArgb(68, 255, 186, 132)],
                            Positions = [0f, 0.55f, 1f],
                        },
                    };
                    g.FillEllipse(br, box);
                }
            }
            // The lit underside: a thin bright sliver along the bottom of each big oval (the
            // oval minus a copy slid up), which is what makes a flat shape read as a cloud
            // with the sun beneath it.
            foreach (var (c, wf, hf, dy) in ovals)
            {
                if (hf < 0.7f) continue;
                float ow = len * wf * 0.92f, oh = thick * hf, oy = cy + thick * dy;
                for (int k = 0; k < 3; k++)                       // three slivers, each fainter and thicker, so the glow has no hard line
                {
                    using var whole = new GraphicsPath();
                    whole.AddEllipse(len * c - ow / 2, oy - oh / 2, ow, oh);
                    using var upper = new GraphicsPath();
                    upper.AddEllipse(len * c - ow / 2, oy - oh / 2 - oh * (0.18f + 0.1f * k), ow, oh);
                    using var rim = new Region(whole);
                    rim.Exclude(upper);
                    using var gold = new SolidBrush(Color.FromArgb(70 - 15 * k, 255, 200, 150));
                    g.FillRegion(gold, rim);
                }
            }
            // The extra bright touch at the end that faces the sun.
            float hx = brightRight ? len * 0.78f : len * 0.22f;
            for (int k = 0; k < 6; k++)
            {
                float s = 1 - 0.16f * k;
                float w = len * 0.32f * s, hh = Math.Max(1.5f, thick * 0.55f * s);
                using var warm = new SolidBrush(Color.FromArgb(34, 255, 214, 150));
                g.FillEllipse(warm, hx - w / 2, cy + thick * 0.12f - hh / 2, w, hh);
            }
        });
    }
    public override void Begin(Random rng)
    {
        int n = rng.Next(2, 5);
        var list = new List<(int, int, float, float, float, float)>();
        float u = _s.U;
        for (int tries = 0; tries < 40 && list.Count < n; tries++)
        {
            float y = _s.Height * (0.15f + 0.30f * (float)rng.NextDouble());
            if (list.Any(c => Math.Abs(c.Item4 - y) < _s.Height * 0.07f)) continue;     // keep the streaks in separate lanes
            float x = _s.Width * (0.15f + 0.70f * (float)rng.NextDouble());
            int shape = rng.Next(4);
            int side = _s.Sun.At.X > x ? 1 : 0;                   // the bright end faces the sun
            float speed = u * 0.03f * (0.8f + 0.4f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? 1 : -1);
            list.Add((shape, side, x, y, speed, 0.8f * list.Count));
        }
        _clouds = list.ToArray();
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        foreach (var c in _clouds)
        {
            float on = Fade(t, Seconds, 4f + c.Delay, 4f) * 0.9f;
            var sp = _cloud[c.Shape, c.Side];
            sp.DrawCentered(fb, c.X + c.Speed * t, c.Y, on, _s.OpenSky);
        }
    }
}
