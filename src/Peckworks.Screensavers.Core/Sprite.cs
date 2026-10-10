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

    // For each row, the first and last column that is not clear glass. A
    // sparse sprite (a tuft of grass is mostly empty box) is stamped row by
    // row only between them, instead of reading every clear pixel to skip it.
    private readonly int[] _rowFirst, _rowLast;

    public int Width { get; }
    public int Height { get; }

    private Sprite(int width, int height, uint[] argb)
    {
        Width = width;
        Height = height;
        _argb = argb;
        _rowFirst = new int[height];
        _rowLast = new int[height];
        for (int y = 0; y < height; y++)
        {
            int first = width, last = -1;
            for (int x = 0; x < width; x++)
                if ((argb[y * width + x] >> 24) != 0) { if (first == width) first = x; last = x; }
            _rowFirst[y] = first;
            _rowLast[y] = last;
        }
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
    /// Makes a sprite from pixels worked out in code rather than painted with
    /// the drawing kit: one 0xAARRGGBB number per pixel, row by row (AA = how
    /// solid, 0 = clear glass).
    /// </summary>
    public static Sprite FromPixels(int width, int height, uint[] argb) => new(width, height, argb);

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
    public void DrawCentered(FrameBuffer fb, float x, float y, float opacity = 1f, bool[]? onlyWhere = null, bool mirror = false,
        byte[]? depth = null, int nearness = 255) =>
        Draw(fb, (int)(x - Width / 2f), (int)(y - Height / 2f), opacity, onlyWhere, mirror, depth, nearness);

    /// <summary>
    /// Stamps the sprite with its top-left corner at (left, top).
    /// </summary>
    /// <param name="opacity">1 = as painted, 0.5 = half see-through, 0 = invisible.</param>
    /// <param name="onlyWhere">
    /// An optional stencil, one true/false per pixel of the frame. Where it is
    /// false the stamp leaves the frame alone. A scene uses this to make
    /// something pass BEHIND the scenery: "only draw where the backdrop is sky".
    /// </param>
    /// <param name="mirror">
    /// True stamps the sprite flipped left to right. Paint a fish or a
    /// squirrel facing right once; the same sticker turned over is the one
    /// facing left, so no second set of sprites is needed.
    /// </param>
    /// <param name="depth">
    /// An optional depth map of the backdrop (PhotoBackdrop.Depth: how near
    /// each pixel's scenery is, 0 far to 255 near). With it, the sprite is
    /// hidden wherever the scenery is NEARER than the sprite's own
    /// "nearness": a reed between us and a firefly hides the firefly. Like
    /// holding a cut-out at arm's length in a garden: the flowers closer to
    /// your eye than your hand cover it, the hedge behind does not.
    /// </param>
    /// <param name="nearness">How near the sprite itself is, on the same 0 to 255 scale.</param>
    public void Draw(FrameBuffer fb, int left, int top, float opacity = 1f, bool[]? onlyWhere = null, bool mirror = false,
        byte[]? depth = null, int nearness = 255)
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

        // A big sprite (a long bank of mist at 4K is a quarter of a million
        // pixels) is shared out across the processor's cores, a band of rows
        // each, like several painters each taking a strip of the same wall.
        // Rows never overlap, so they cannot get in each other's way. Small
        // ones (most glows) are quicker done alone than handed out.
        if ((x1 - x0) * (y1 - y0) > 40_000)
            DrawShared(fb, left, top, x0, x1, y0, y1, opacity256, onlyWhere, mirror, depth, nearness);
        else
            DrawRows(fb, left, top, x0, x1, y0, y1, opacity256, onlyWhere, mirror, depth, nearness);
    }

    // Its own method, so the hand-out (which needs a lambda, and so a little
    // garbage) is only paid for by the big sprites, not every small glow.
    private void DrawShared(FrameBuffer fb, int left, int top, int x0, int x1, int y0, int y1, int opacity256,
        bool[]? onlyWhere, bool mirror, byte[]? depth, int nearness) =>
        Parallel.For(0, (y1 - y0 + 31) / 32, band =>
            DrawRows(fb, left, top, x0, x1, y0 + band * 32, Math.Min(y1, y0 + band * 32 + 32), opacity256, onlyWhere, mirror, depth, nearness));

    /// <summary>Stamps the sprite's rows y0 to y1 (the visible columns x0 to x1).</summary>
    private void DrawRows(FrameBuffer fb, int left, int top, int x0, int x1, int y0, int y1, int opacity256,
        bool[]? onlyWhere, bool mirror, byte[]? depth, int nearness)
    {
        uint[] frame = fb.Pixels;

        for (int sy = y0; sy < y1; sy++)
        {
            int src = sy * Width, dst = (top + sy) * fb.Width + left;
            // Only the painted stretch of this row (mirrored: the stretch counted from the other edge).
            int rx0 = mirror ? Width - 1 - _rowLast[sy] : _rowFirst[sy], rx1 = mirror ? Width - _rowFirst[sy] : _rowLast[sy] + 1;
            int xa = Math.Max(x0, rx0), xb = Math.Min(x1, rx1);
            for (int sx = xa; sx < xb; sx++)
            {
                // Mirrored: read the sheet from its right edge inward instead.
                uint c = _argb[src + (mirror ? Width - 1 - sx : sx)];
                int a = (int)(c >> 24) * opacity256 >> 8;                // how solid this pixel is, 0 to 255
                if (a == 0) continue;                                    // clear glass: nothing to do
                if (onlyWhere != null && !onlyWhere[dst + sx]) continue; // the stencil says hands off
                if (depth != null && depth[dst + sx] > nearness) continue; // something nearer stands in front

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
