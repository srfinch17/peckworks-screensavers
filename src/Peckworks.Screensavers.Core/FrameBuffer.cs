using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// A picture held in memory as one long list of numbers, one number per pixel.
///
/// FEYNMAN VERSION: a screen is a sheet of graph paper where every square
/// (pixel) is colored in. A FrameBuffer is our private sheet of graph paper.
/// We color squares on it as fast as we like, and once per frame we slap the
/// whole finished sheet onto the real screen in one go (that is "Present").
///
/// Why not draw straight onto the screen? Because the viewer would see each
/// half-finished frame being built (flicker). Painting on a private sheet and
/// then swapping it in whole is called "double buffering". Every game does it.
///
/// HOW A PIXEL IS STORED: each pixel is one 32-bit unsigned number (uint).
/// Written in hexadecimal it looks like 0x00RRGGBB:
///     bits 16 to 23 = red   (0 to 255)
///     bits  8 to 15 = green (0 to 255)
///     bits  0 to  7 = blue  (0 to 255)
///     the top 8 bits are unused.
/// So pure green is 0x0000FF00 and white is 0x00FFFFFF.
///
/// HOW THE 2D GRID FITS IN A 1D LIST: rows are laid end to end, top row first.
/// The pixel at column x, row y lives at index  y * Width + x.
/// Like reading a book: finish a line, drop to the next, start at the left again.
/// </summary>
public sealed class FrameBuffer
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>The pixels. Scenes write directly into this array.</summary>
    public uint[] Pixels { get; }

    // The label Windows needs to read our pixels. Built once, reused each frame.
    private NativeMethods.BITMAPINFOHEADER _header;

    public FrameBuffer(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new uint[Width * Height];

        _header = new NativeMethods.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            biWidth = Width,
            // Negative height is a quirk of the old Windows bitmap format:
            // positive means "the first row in memory is the BOTTOM of the picture"
            // (a leftover from 1980s math conventions where y goes up).
            // Negative means "first row is the TOP", which matches how we think.
            biHeight = -Height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
    }

    /// <summary>Paints every pixel black (all zeros). Called at the start of each frame.</summary>
    public void Clear() => Array.Clear(Pixels);

    /// <summary>Packs red, green, blue (each 0 to 255) into one pixel number.</summary>
    public static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

    /// <summary>
    /// Mix a color over a pixel. alpha = how opaque the new color is (0 = invisible, 1 = solid).
    /// result = old + (new - old) * alpha, for each of red, green, and blue.
    /// </summary>
    public static uint Blend(uint bg, int r, int g, int b, float alpha)
    {
        int br = (int)((bg >> 16) & 0xFF), bgc = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
        int nr = br + (int)((r - br) * alpha);
        int ng = bgc + (int)((g - bgc) * alpha);
        int nb = bb + (int)((b - bb) * alpha);
        return (uint)((Math.Clamp(nr, 0, 255) << 16) | (Math.Clamp(ng, 0, 255) << 8) | Math.Clamp(nb, 0, 255));
    }

    /// <summary>
    /// Draws a straight line from a to b, straight into the pixels. For thin
    /// things that change every frame and so cannot be a sprite: a strand of
    /// spider silk being spun, a shooting star's tail.
    ///
    /// How: walk along the line's LONG direction one pixel at a time (across
    /// for a flat line, down for a steep one). At each step the line covers a
    /// short run of pixels in the other direction, "thickness" tall. The
    /// pixels wholly inside the run get the full color; the two at its ends
    /// get only the fraction the line really covers. Those part-colored end
    /// pixels are what make the edge look smooth instead of like a staircase
    /// ("anti-aliasing").
    /// </summary>
    /// <param name="alpha">0 = invisible, 1 = solid.</param>
    /// <param name="onlyWhere">An optional stencil, as in Sprite.Draw: where it is false the line leaves the frame alone.</param>
    public void Line(PointF a, PointF b, Color color, float alpha = 1f, float thickness = 1f, bool[]? onlyWhere = null)
    {
        alpha *= color.A / 255f;
        if (alpha <= 0) return;
        float dx = b.X - a.X, dy = b.Y - a.Y;
        bool flat = Math.Abs(dx) >= Math.Abs(dy);
        int steps = Math.Max(1, (int)MathF.Ceiling(flat ? Math.Abs(dx) : Math.Abs(dy)));
        float half = Math.Max(0.5f, thickness / 2);

        for (int i = 0; i <= steps; i++)
        {
            float x = a.X + dx * i / steps, y = a.Y + dy * i / steps;
            int along = (int)MathF.Floor(flat ? x : y);                 // the pixel we are at in the long direction
            float center = flat ? y : x;                                // where the line crosses it in the other
            float lo = center - half, hi = center + half;
            for (int p = (int)MathF.Floor(lo); p <= (int)MathF.Floor(hi); p++)
            {
                float cover = Math.Min(hi, p + 1) - Math.Max(lo, p);    // how much of this pixel the line covers, 0 to 1
                if (cover <= 0) continue;
                int px = flat ? along : p, py = flat ? p : along;
                if (px < 0 || px >= Width || py < 0 || py >= Height) continue;
                int at = py * Width + px;
                if (onlyWhere != null && !onlyWhere[at]) continue;
                Pixels[at] = Blend(Pixels[at], color.R, color.G, color.B, alpha * cover);
            }
        }
    }

    /// <summary>
    /// Copies the whole sheet onto a real window in one fast call.
    /// "hdc" is a Handle to a Device Context: Windows' name for "a surface you can
    /// draw on", here the inside of our window.
    /// </summary>
    public void PresentTo(IntPtr hdc)
    {
        NativeMethods.SetDIBitsToDevice(
            hdc,
            0, 0,                       // where on the window: top-left corner
            (uint)Width, (uint)Height,  // how much to copy: all of it
            0, 0,                       // where in our sheet to start: top-left
            0, (uint)Height,            // which rows: all of them
            Pixels,
            ref _header,
            0);                         // 0 = DIB_RGB_COLORS, "these are real colors, not palette slots"
    }

    /// <summary>
    /// Copies the sheet onto a window, enlarged to fill destWidth x destHeight
    /// (used when the sheet is smaller than the screen on purpose).
    /// </summary>
    public void PresentTo(IntPtr hdc, int destWidth, int destHeight)
    {
        if (destWidth == Width && destHeight == Height) { PresentTo(hdc); return; }
        NativeMethods.SetStretchBltMode(hdc, 3);   // COLORONCOLOR: repeat pixels, the fastest way to enlarge
        NativeMethods.StretchDIBits(hdc, 0, 0, destWidth, destHeight, 0, 0, Width, Height,
            Pixels, ref _header, 0, NativeMethods.SRCCOPY);
    }

    /// <summary>
    /// Makes a normal .NET Bitmap copy of the current picture (used to save PNG
    /// snapshots for testing and tuning).
    /// </summary>
    public Bitmap ToBitmap()
    {
        // "Pinning" tells .NET's garbage collector: do not move this array in
        // memory while we hand its address to the Bitmap constructor.
        var handle = GCHandle.Alloc(Pixels, GCHandleType.Pinned);
        try
        {
            using var view = new Bitmap(Width, Height, Width * 4,
                PixelFormat.Format32bppRgb, handle.AddrOfPinnedObject());
            // Clone so the returned bitmap owns its own memory and outlives the pin.
            return view.Clone(new Rectangle(0, 0, Width, Height), PixelFormat.Format32bppRgb);
        }
        finally
        {
            handle.Free();
        }
    }
}
