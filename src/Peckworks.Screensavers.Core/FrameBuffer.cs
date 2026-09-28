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
