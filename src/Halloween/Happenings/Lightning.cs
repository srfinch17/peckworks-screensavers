using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Halloween.Happenings;

/// <summary>
/// A bolt of lightning: a jagged white line drops from the top of the sky to
/// behind the far hill, with a fork or two splitting off. It flickers the way
/// real lightning does, and for a moment the sky glows soft violet.
///
/// THE FLICKER: real lightning is not one flash. It is a quick strike, a dark
/// beat, then a second, longer strike that dies away. So the brightness "b"
/// (0 to 1) follows this script, in seconds:
///     0.00 to 0.12   bright
///     0.12 to 0.22   nearly dark
///     0.22 to 0.50   bright again, fading out
///     0.50 to the end   a faint afterglow
///
/// THE BOLT: built once in Begin as a list of points from the top of the
/// screen down to the hill. Walking down, each point is nudged sideways at
/// random, which gives the zigzag. Each pair of points is one line segment.
/// A fork is the same trick starting from one of the main bolt's joints.
///
/// Every segment is drawn in three layers, wide and faint first: a violet
/// glow, a paler lilac middle, then a thin white core. Layering is how a
/// light "bleeds" into the dark around it.
///
/// It is drawn through the OpenSky stencil, so the bolt ends BEHIND the hill
/// and the trees stay dark shapes against the lit sky.
/// </summary>
internal sealed class Lightning : Happening
{
    private const int Joints = 24;           // the main bolt is 24 segments
    private static readonly Color Violet = Color.FromArgb(150, 110, 255);
    private static readonly Color Lilac = Color.FromArgb(205, 190, 255);
    private static readonly Color White = Color.FromArgb(255, 252, 255);

    private readonly HalloweenScenery _s;
    private readonly Sprite _skyGlow;
    private readonly List<(PointF[] Points, float Weight)> _bolts = [];   // the main bolt (weight 1) and its forks (thinner)
    private PointF _middle;                                               // the middle of the main bolt: where the sky glow is centered

    public override float Seconds => 1.4f;

    public Lightning(HalloweenScenery s)
    {
        _s = s;

        // The lit-up sky: a big soft violet disc. Made by hand instead of
        // with Sprite.Glow because Glow has a white hot spot in the middle,
        // and here we want an even wash of light, not a lamp.
        // Its size is a trade: this is by far the biggest stamp in the scene
        // (hundreds of thousands of pixels), and every pixel costs time. At
        // 0.35 U it halved the frame rate on a 4K screen; 0.26 U lights the
        // sky around the bolt just as well for about half the work.
        int r = Math.Max(8, (int)(s.U * 0.26f));
        _skyGlow = Sprite.Paint(r * 2, r * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, r * 2, r * 2);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(255, 175, 150, 255),
                SurroundColors = [Color.FromArgb(0, 175, 150, 255)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(brush, path);
        });
    }

    public override void Begin(Random rng)
    {
        _bolts.Clear();
        float u = _s.U;
        float x0 = _s.Width * (0.08f + 0.84f * (float)rng.NextDouble());
        float step = u * 0.03f;                                  // each segment's sideways nudge is about this big

        // First the sideways wander of every joint, a "leaky" random walk:
        // keep most of the last sideways offset (so the bolt meanders
        // instead of jittering in place) and add a new nudge.
        var offs = new float[Joints + 1];
        float off = 0;
        for (int i = 0; i <= Joints; i++)
        {
            off = off * 0.65f + ((float)rng.NextDouble() * 2 - 1) * step * 1.2f;
            offs[i] = off;
        }

        // Then the finish line: the far hill's height under where the bolt
        // really ENDS, which is its last joint, not the x it started above.
        // (Asking the hill at the start x left the bolt hanging in the air
        // whenever it wandered off over lower ground.)
        float yTop = -u * 0.02f;
        float xEnd = Math.Clamp(x0 + offs[Joints], 0, _s.Width - 1);
        float yEnd = Brushwork.RidgeYAt(_s.FarRidge, xEnd) + u * 0.02f;   // a hair below the ridge, so it plunges behind it

        var main = new PointF[Joints + 1];
        for (int i = 0; i <= Joints; i++)
            main[i] = new PointF(x0 + offs[i], yTop + (yEnd - yTop) * i / Joints);
        _bolts.Add((main, 1f));
        _middle = main[Joints / 2];

        // One or two forks, each leaving from a joint in the upper-middle of the bolt.
        int forks = 1 + rng.Next(2);
        for (int f = 0; f < forks; f++)
        {
            int joint = 5 + rng.Next(12);
            float side = rng.Next(2) == 0 ? 1 : -1;
            int len = 7 + rng.Next(5);
            var fork = new PointF[len + 1];
            fork[0] = main[joint];
            for (int i = 1; i <= len; i++)
            {
                float dx = side * step * (0.6f + 0.9f * (float)rng.NextDouble()) + ((float)rng.NextDouble() - 0.5f) * step;
                float dy = step * (0.7f + 0.5f * (float)rng.NextDouble());
                fork[i] = new PointF(fork[i - 1].X + dx, fork[i - 1].Y + dy);
            }
            _bolts.Add((fork, 0.6f));
        }
    }

    /// <summary>The flicker script from the top of this file: 0 = dark, 1 = full brightness.</summary>
    private static float Brightness(float t)
    {
        if (t < 0.12f) return 1f;
        if (t < 0.22f) return 0.04f;
        if (t < 0.50f) return 0.95f * (1 - (t - 0.22f) / 0.28f) + 0.05f;
        return 0.15f * Math.Max(0f, 1 - (t - 0.50f) / 0.9f);     // faint afterglow, dying out by the end
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float b = Brightness(t);
        if (b <= 0.005f) return;
        float u = _s.U;

        // The sky lights up first, so the bolt is painted over the glow. Capped at 0.35: soft, not a strobe.
        // Skipped during the faint afterglow: at that strength the glow is
        // too weak to see, but stamping it would still cost its full price.
        if (b > 0.2f) _skyGlow.DrawCentered(fb, _middle.X, _middle.Y, 0.35f * b, _s.OpenSky);

        float wide = Math.Max(2f, u * 0.011f);
        float mid = Math.Max(1.5f, u * 0.005f);
        float core = Math.Max(1f, u * 0.003f);
        foreach (var (pts, weight) in _bolts)
        {
            // Three passes over the whole bolt, widest first, so a narrow layer never gets buried under a wide one.
            for (int pass = 0; pass < 3; pass++)
            {
                Color c = pass == 0 ? Violet : pass == 1 ? Lilac : White;
                float thick = (pass == 0 ? wide : pass == 1 ? mid : core) * (weight < 1 ? 0.7f : 1f);
                float alpha = (pass == 0 ? 0.25f : pass == 1 ? 0.35f : 1f) * b * (weight < 1 ? 0.85f : 1f);
                for (int i = 0; i + 1 < pts.Length; i++)
                    fb.Line(pts[i], pts[i + 1], c, alpha, Math.Max(1f, thick), _s.OpenSky);
            }
        }
    }
}
