using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// A small picture with see-through parts, painted once and then stamped onto
/// the frame as many times as we like.
///
/// FEYNMAN VERSION: think of a rubber stamp, or a sticker. Drawing a bat with
/// curved wings takes the slow drawing kit (GDI+) a noticeable moment. Doing
/// that for twenty bats, sixty times a second, would be far too slow. So we
/// draw the bat ONCE at startup onto a little transparent sheet, keep that
/// sheet, and every frame we just copy it onto the picture wherever the bat
/// is now. Copying is cheap. Games have done this since the 1980s, and they
/// call the little sheet a "sprite".
///
/// Each pixel of the sheet is stored as 0xAARRGGBB: the usual red, green and
/// blue, plus AA, the "alpha": how solid that pixel is (0 = clear glass,
/// 255 = solid paint). The soft edge of a wing is half-solid pixels, which
/// is what lets it blend smoothly into whatever is behind it.
///
/// To animate (flapping wings, galloping legs), make a few sprites, one per
/// pose, and stamp a different one each frame, like a flip-book.
/// </summary>
public sealed class Sprite
{
    private readonly uint[] _argb;

    public int Width { get; }
    public int Height { get; }

    private Sprite(int width, int height, uint[] argb)
    {
        Width = width;
        Height = height;
        _argb = argb;
    }

    /// <summary>
    /// Makes a sprite by handing you a blank transparent sheet to paint on
    /// with the ordinary drawing kit. Whatever you leave unpainted stays clear.
    /// </summary>
    public static Sprite Paint(int width, int height, Action<Graphics> paint)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            paint(g);
        }

        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var raw = new int[width * height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);   // 32-bit rows have no padding, so one copy does it
            var argb = new uint[raw.Length];
            Buffer.BlockCopy(raw, 0, argb, 0, raw.Length * 4);
            return new Sprite(width, height, argb);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>
    /// A soft round light: strongest in the middle, fading to nothing at the
    /// edge, with a small near-white hot spot at the center. Stamp it over a
    /// light bulb, a candle, or a certain reindeer's nose to make it shine.
    /// </summary>
    public static Sprite Glow(int radius, Color color)
    {
        radius = Math.Max(2, radius);
        return Paint(radius * 2, radius * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, radius * 2, radius * 2);
            using var halo = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(200, color),
                SurroundColors = [Color.FromArgb(0, color)],
                Blend = SoftFalloff,
            };
            g.FillPath(halo, path);

            float core = radius * 0.3f;
            using var corePath = new GraphicsPath();
            corePath.AddEllipse(radius - core, radius - core, core * 2, core * 2);
            using var hot = new PathGradientBrush(corePath)
            {
                CenterColor = Color.FromArgb(255, Mix(color, Color.White, 0.7f)),
                SurroundColors = [Color.FromArgb(0, color)],
            };
            g.FillPath(hot, corePath);
        });
    }

    /// <summary>
    /// How a glow fades from its center to its edge. Left alone, the drawing
    /// kit fades in a straight line, and the eye picks out the rim where that
    /// line hits zero as a faint ring. This curve eases out instead (it
    /// follows the square of the distance in from the edge), so the glow
    /// melts into the background with no visible rim. Position 0 is the
    /// edge, 1 is the center; each factor is how much of the center color
    /// shows there. Any painter can hand this to a PathGradientBrush.
    /// </summary>
    public static Blend SoftFalloff => new()
    {
        Positions = [0f, 0.25f, 0.5f, 0.75f, 1f],
        Factors = [0f, 0.06f, 0.25f, 0.56f, 1f],
    };

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>Stamps the sprite with its CENTER at (x, y). Handy for glows.</summary>
    public void DrawCentered(FrameBuffer fb, float x, float y, float opacity = 1f, bool[]? onlyWhere = null) =>
        Draw(fb, (int)(x - Width / 2f), (int)(y - Height / 2f), opacity, onlyWhere);

    /// <summary>
    /// Stamps the sprite with its top-left corner at (left, top).
    /// </summary>
    /// <param name="opacity">1 = as painted, 0.5 = half see-through, 0 = invisible.</param>
    /// <param name="onlyWhere">
    /// An optional stencil, one true/false per pixel of the frame. Where it is
    /// false the stamp leaves the frame alone. A scene uses this to make
    /// something pass BEHIND the scenery: "only draw where the backdrop is sky".
    /// </param>
    public void Draw(FrameBuffer fb, int left, int top, float opacity = 1f, bool[]? onlyWhere = null)
    {
        if (opacity <= 0) return;
        // Work out which part of the sprite is actually on screen, so a sprite
        // hanging off an edge neither crashes nor wastes time.
        int x0 = Math.Max(0, -left), x1 = Math.Min(Width, fb.Width - left);
        int y0 = Math.Max(0, -top), y1 = Math.Min(Height, fb.Height - top);

        // Whole-number math only in the loop below. A glow can cover hundreds
        // of pixels and a scene may stamp hundreds of glows per frame, so this
        // loop runs millions of times a second, and whole numbers are quicker
        // than decimals. The opacity becomes a number from 0 to 256, and
        // ">> 8" (shift right by 8 bits) is a fast "divide by 256".
        int opacity256 = (int)(Math.Min(1f, opacity) * 256);
        uint[] frame = fb.Pixels;

        for (int sy = y0; sy < y1; sy++)
        {
            int src = sy * Width, dst = (top + sy) * fb.Width + left;
            for (int sx = x0; sx < x1; sx++)
            {
                uint c = _argb[src + sx];
                int a = (int)(c >> 24) * opacity256 >> 8;                // how solid this pixel is, 0 to 255
                if (a == 0) continue;                                    // clear glass: nothing to do
                if (onlyWhere != null && !onlyWhere[dst + sx]) continue; // the stencil says hands off

                // result = old + (new - old) * solidness, for red, green and blue.
                uint bg = frame[dst + sx];
                int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                int r = br + (((int)((c >> 16) & 0xFF) - br) * a >> 8);
                int g = bgn + (((int)((c >> 8) & 0xFF) - bgn) * a >> 8);
                int b = bb + (((int)(c & 0xFF) - bb) * a >> 8);
                frame[dst + sx] = (uint)((r << 16) | (g << 8) | b);
            }
        }
    }
}
