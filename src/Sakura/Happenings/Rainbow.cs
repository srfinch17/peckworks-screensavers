using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A soft rainbow (niji) over the lake.
///
/// A rainbow is just a ring of colored light around a point far below the
/// horizon (the point opposite the sun), so we paint a piece of a big circle:
/// seven thin bands, red on the outside to violet on the inside, translucent,
/// fading out into the mist at both feet and a little towards the top.
///
/// (A first version also had the faint second bow that real rainbows show
/// outside the first. At 30% strength it was barely visible, yet it cost as
/// much to draw as the main bow, and the pair slowed a 4K screen by about a
/// sixth for 14 seconds. It went.)
///
/// Cost: a rainbow is long and thin, so one rectangle around it would be
/// mostly empty air, and stamping empty air still costs time. Instead the
/// arc is cut into upright strips, and each strip is only as tall as the
/// part of the arc it holds. Painting them separately leaves no seams,
/// because every strip draws the same big circle, just shifted over to its
/// own corner.
///
/// It is stamped through the open-sky stencil, so it passes behind the
/// mountain, the shore and the branches. It never moves, so it is painted
/// once; Begin only slides it a little left or right (a stamp can start on
/// any whole pixel), so it is not always in the same place.
/// </summary>
internal sealed class Rainbow : Happening
{
    // Red outside to violet inside, soft pastel versions.
    private static readonly Color[] Bands =
    [
        Color.FromArgb(255, 96, 96), Color.FromArgb(255, 162, 76), Color.FromArgb(255, 226, 110),
        Color.FromArgb(128, 224, 128), Color.FromArgb(96, 196, 240), Color.FromArgb(118, 130, 232),
        Color.FromArgb(176, 116, 222),
    ];

    private readonly Scenery _s;
    private readonly List<(Sprite Sp, int Left, int Top)> _strips = [];
    private int _shift;
    private readonly bool _showable;       // false when the mountain would hide nearly the whole bow (a tall screen)

    // Nothing to show? Say so before being dealt: on a tall screen Fuji fills the width and the bow
    // would sit entirely behind it, so the card stays in the deck instead of wasting a turn.
    public override bool CanBegin => _showable;

    public override float Seconds => 14f;
    // "fuji", not "weather": the bow arcs behind the mountain, and sharing the
    // mountain's claim also keeps it from running at the same time as Red Fuji.
    // Those two are the dearest happenings to draw on a 4K screen, and a claim
    // means "never at once", so their costs never add up in one frame.
    public override string? Claims => "fuji,costly";

    public Rainbow(Scenery s)
    {
        _s = s;
        float u = s.U, horizon = s.HorizonY;
        float cx = s.Width * 0.6f;                         // Begin slides it about this
        float cy = horizon + u * 0.55f;                    // the circle's centre, well below the horizon
        float rOut = u * 0.9f;                             // outer edge of the red band
        float bw = u * 0.010f;                             // one band's width
        float rTop = rOut;                                 // the top edge of the picture is the bow's outer edge
        float yEnd = horizon + u * 0.01f;                  // the bows fade to nothing here, in the mist
        float yTop = cy - rTop;

        // Alpha along the height of the picture: faint at the very top, full over most of the
        // arc, then thinning to nothing at the foot (the mist). The bows are symmetric, so a
        // vertical fade gives both feet the same treatment.
        float span = Math.Max(1f, yEnd - yTop);
        float PosOf(float y) => Math.Clamp((y - yTop) / span, 0f, 1f);
        var blend = new ColorBlend
        {
            Positions = [0f, PosOf(horizon - u * 0.18f), PosOf(horizon - u * 0.05f), 1f],
            Colors = [Color.FromArgb(150, 255, 255, 255), Color.White, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)],
        };

        // Walk along the middle of the bow and count the spots that are bare sky (at the shift Begin
        // may add, which is up to a fifth of the screen either way, so take the best of three).
        int bestOpen = 0;
        foreach (int shift in new[] { -(int)(s.Width * 0.15f), 0, (int)(s.Width * 0.15f) })
        {
            int open = 0;
            for (int i = 0; i < 120; i++)
            {
                float ang = MathF.PI * (0.1f + 0.8f * i / 119f);       // from the left foot, over the top, to the right foot
                int px = (int)(cx + shift - MathF.Cos(ang) * (rOut - bw * 3.5f)), py = (int)(cy - MathF.Sin(ang) * (rOut - bw * 3.5f));
                if (py < 0 || py >= s.Height || px < 0 || px >= s.Width || py > horizon - u * 0.05f) continue;
                if (s.OpenSky[py * s.Width + px]) open++;
            }
            bestOpen = Math.Max(bestOpen, open);
        }
        _showable = bestOpen >= 25;

        // Cut the bow into upright strips (about 0.03 U wide), each as tall as its own part of the arc.
        int stripW = Math.Max(4, (int)(u * 0.03f));
        {
            float rLo = rOut - bw * 7 - bw, rHi = rOut + bw;
            int xa = (int)(cx - rHi), xb = (int)(cx + rHi) + 1;
            for (int x = xa; x < xb; x += stripW)
            {
                // Highest point of the bow in this strip: the nearest x to the circle's centre.
                float nearDx = (x <= cx && x + stripW >= cx) ? 0f : Math.Min(Math.Abs(x - cx), Math.Abs(x + stripW - cx));
                float farDx = Math.Max(Math.Abs(x - cx), Math.Abs(x + stripW - cx));
                if (nearDx >= rHi) continue;
                float top = cy - MathF.Sqrt(rHi * rHi - nearDx * nearDx);
                float bottom = farDx >= rLo ? yEnd : cy - MathF.Sqrt(rLo * rLo - farDx * farDx);
                int y0 = (int)MathF.Floor(Math.Max(yTop, top)) - 2;
                int y1 = (int)Math.Min(MathF.Ceiling(Math.Min(yEnd, bottom)) + 2, MathF.Ceiling(yEnd));
                if (y1 <= y0) continue;
                int ax = x, ay = y0;
                var sp = Sprite.Paint(stripW, y1 - y0, g =>
                {
                    g.TranslateTransform(-ax, -ay);                        // draw in whole-picture coordinates
                    for (int k = 0; k < 7; k++)
                    {
                        float r = rOut - (k + 0.5f) * bw;
                        Color c = Bands[k];
                        // A little brighter inside the arc than outside.
                        float strength = 0.8f + 0.2f * k / 6f;
                        var colors = new Color[blend.Colors.Length];
                        for (int q = 0; q < colors.Length; q++)
                            colors[q] = Color.FromArgb((int)(blend.Colors[q].A * strength * 0.62f), c);
                        // The vertical fade, tinted this band's color.
                        using var gb = new LinearGradientBrush(new PointF(0, yTop), new PointF(0, yEnd), c, c)
                        {
                            InterpolationColors = new ColorBlend { Positions = blend.Positions, Colors = colors },
                        };
                        // Pens a little wider than a band, so neighbours blend into each other like a real spectrum.
                        using var pen = new Pen(gb, Math.Max(1.6f, bw * 1.7f));
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    }
                });
                _strips.Add((sp, ax, ay));
            }
        }
    }

    public override void Begin(Random rng)
    {
        // Slide the whole bow sideways by up to a fifth of the screen either way.
        _shift = (int)((rng.NextDouble() - 0.5) * 0.3 * _s.Width);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // Fade in 4 s, hold, fade out 5 s.
        float a = 0.55f * Fade(t, Seconds, 4f, 5f);
        if (a < 0.01f) return;
        foreach (var (sp, left, top) in _strips)
            sp.Draw(fb, left + _shift, top, a, _s.OpenSky);
    }
}
