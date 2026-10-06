using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// An ice halo forms around the moon. On a very cold night, tiny ice crystals
/// high in the air bend the moonlight into a thin bright ring about 2.6 moon
/// radii out (really: 22 degrees). The inside edge is faintly red and the
/// outside faintly blue, and two brighter patches called "moon dogs" sit on
/// the ring level with the moon.
///
/// Two sprites, painted once: the ring (many thin concentric circles, each a
/// slightly different color and brightness, so the ring fades softly at both
/// edges instead of looking like a drawn hard circle) and a small glow for a
/// moon dog. Both are stamped through the sky stencil, so the mountains and
/// branches stay in front.
///
/// It fades in over 4 seconds, holds for 4 with a slow shimmer, and fades out
/// over 4. It claims "moon" so it never runs together with another moon effect.
/// </summary>
internal sealed class MoonHalo : Happening
{
    private const float PeakOpacity = 0.35f;

    private readonly ChristmasScenery _s;
    private const int Tiles = 6;
    private readonly List<(int Left, int Top, Sprite Pic)> _tiles = [];
    private readonly int _tile;             // one tile's side, in pixels
    private readonly Sprite _dog;
    private readonly float _ringR;         // the ring middle radius, in pixels
    private readonly int _ringSize;        // the side of the whole (virtual) sheet, in pixels

    public override float Seconds => 12f;
    public override string? Claims => "moon";

    public MoonHalo(ChristmasScenery s)
    {
        _s = s;
        float moonR = Math.Max(4f, s.Moon.R);
        _ringR = 2.6f * moonR;
        float half = 0.3f * moonR;          // the soft edges reach 0.3 R each side of the middle (the bright part is about 0.4 R wide)
        int size = (int)MathF.Ceiling(2 * (_ringR + half)) + 4;
        _ringSize = size;
        float c = size / 2f;

        // The ring is cut into a grid of small tiles and only the tiles the ring touches are kept. The
        // ring is thin, so a single big sheet would be mostly empty (the middle and the corners), and
        // stamping empty pixels still costs time on a 4K screen. Each tile is painted by the same
        // drawing code, just slid over so it shows its own piece of the sheet.
        _tile = Math.Max(16, size / Tiles + 1);
        for (int ty = 0; ty < Tiles; ty++)
            for (int tx = 0; tx < Tiles; tx++)
            {
                int left = tx * _tile, top = ty * _tile;
                // Skip a tile if the whole of it is inside the ring's hole or outside the ring's outer edge.
                float nearX = Math.Clamp(c, left, left + _tile) - c, nearY = Math.Clamp(c, top, top + _tile) - c;
                float farX = Math.Max(Math.Abs(left - c), Math.Abs(left + _tile - c));
                float farY = Math.Max(Math.Abs(top - c), Math.Abs(top + _tile - c));
                float near = MathF.Sqrt(nearX * nearX + nearY * nearY), far = MathF.Sqrt(farX * farX + farY * farY);
                if (near > _ringR + half || far < _ringR - half) continue;
                _tiles.Add((left, top, Sprite.Paint(_tile, _tile, g =>
                {
                    g.TranslateTransform(-left, -top);
                    PaintRing(g, c, half);
                })));
            }

        // A moon dog: a soft pale glow a bit wider than the ring is.
        _dog = Sprite.Glow(Math.Max(3, (int)(0.45f * moonR)), Color.FromArgb(235, 240, 255));
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 4f, 4f);
        // A slow shimmer, strongest while it is held, like thin cloud passing over it.
        float shimmer = 1f + 0.12f * MathF.Sin(t * 1.3f) + 0.06f * MathF.Sin(t * 3.1f + 1f);
        PointF m = _s.Moon.At;

        int originX = (int)(m.X - _ringSize / 2f), originY = (int)(m.Y - _ringSize / 2f);   // where the whole sheet's top-left would be
        foreach (var (left, top, pic) in _tiles)
            pic.Draw(fb, originX + left, originY + top, PeakOpacity * on * shimmer, _s.OpenSky);

        // The moon dogs, left and right of the moon, level with it, each shimmering on its own beat.
        _dog.DrawCentered(fb, m.X - _ringR, m.Y, 0.55f * on * (1f + 0.2f * MathF.Sin(t * 1.7f)), _s.OpenSky);
        _dog.DrawCentered(fb, m.X + _ringR, m.Y, 0.55f * on * (1f + 0.2f * MathF.Sin(t * 1.9f + 2f)), _s.OpenSky);
    }

    /// <summary>Paints the whole ring onto a sheet whose middle is at (c, c).</summary>
    private void PaintRing(Graphics g, float c, float half)
    {
        // Walk from the inner edge to the outer edge in 48 steps. Each step
        // is one thin circle: its brightness follows a bell curve (zero at
        // both edges, strongest in the middle) and its color slides from
        // red through white to blue.
        const int steps = 48;
        float stepW = 2 * half / steps;
        for (int i = 0; i < steps; i++)
        {
            float k = (i + 0.5f) / steps;                       // 0 inside, 1 outside
            float bell = MathF.Sin(k * MathF.PI);               // 0 at the edges, 1 in the middle
            bell *= bell;
            Color col = k < 0.5f
                ? Lerp(Color.FromArgb(255, 150, 130), Color.FromArgb(255, 250, 245), k * 2)
                : Lerp(Color.FromArgb(255, 250, 245), Color.FromArgb(140, 175, 255), (k - 0.5f) * 2);
            float r = _ringR - half + (i + 0.5f) * stepW;
            using var pen = new Pen(Color.FromArgb((int)(255 * bell), col), stepW + 1f);
            g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
        }
    }

    private static Color Lerp(Color a, Color b, float k) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
}
