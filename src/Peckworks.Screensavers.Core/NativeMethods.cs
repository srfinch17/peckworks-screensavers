using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// A small phone book of raw Windows functions that .NET does not wrap for us.
///
/// FEYNMAN VERSION: Windows itself is written in C, and it exposes thousands of
/// functions living inside DLL files like user32.dll ("windows and input") and
/// gdi32.dll ("drawing"). .NET wraps most of the ones you need day to day, but
/// not all. "P/Invoke" (Platform Invoke) is how C# calls one of those raw C
/// functions directly: you write a method signature with no body, mark it
/// [DllImport("which.dll")], and .NET finds the real function at runtime and
/// translates your C# arguments into what the C code expects.
///
/// Think of it like dialing a number from the phone book: the signature below is
/// the number, the DLL is the building, and .NET is the operator connecting you.
/// </summary>
internal static class NativeMethods
{
    // ------------------------------------------------------------------
    // Window plumbing, used by the tiny preview box in Screen Saver Settings.
    // ------------------------------------------------------------------

    /// <summary>
    /// Moves one window inside another. We use it to put our animation INSIDE
    /// the little monitor picture in the Screen Saver Settings dialog.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    /// <summary>Reads a window's "style" flags (a bit field of yes/no settings).</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>Writes a window's "style" flags.</summary>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>Asks "how big is the inside of this window?"</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>Asks "does this window still exist?"</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    /// <summary>Asks Windows to make this the active window (the one that gets keyboard input).</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Index for Get/SetWindowLongPtr meaning "the style flags".</summary>
    public const int GWL_STYLE = -16;

    /// <summary>Style flag meaning "I am a child living inside another window".</summary>
    public const long WS_CHILD = 0x40000000;

    /// <summary>A rectangle the way C code describes one: four edges.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    // ------------------------------------------------------------------
    // Fast drawing: copy our array of pixels straight onto the screen.
    // ------------------------------------------------------------------

    /// <summary>
    /// The "description label" stuck on the front of a block of raw pixels, so
    /// Windows knows how to read it: how wide, how tall, how many bits per pixel.
    /// DIB means "Device Independent Bitmap": plain pixels in memory, not tied to
    /// any particular graphics card.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;       // NEGATIVE height means "row 0 is the TOP row" (see FrameBuffer)
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression; // 0 = BI_RGB = "not compressed, just raw pixels"
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    /// <summary>
    /// Copies a block of pixels from our memory directly onto a window.
    /// One call per frame. This is much faster than drawing thousands of
    /// characters one by one with the normal .NET drawing API.
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern int SetDIBitsToDevice(
        IntPtr hdc,
        int xDest, int yDest,
        uint width, uint height,
        int xSrc, int ySrc,
        uint startScan, uint scanLines,
        uint[] bits,
        ref BITMAPINFOHEADER bitmapInfo,
        uint colorUse);
}
