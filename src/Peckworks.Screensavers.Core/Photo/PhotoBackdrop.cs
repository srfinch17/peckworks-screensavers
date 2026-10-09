using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// A photograph, fitted to the screen, plus where its water is. The base of
/// every photo saver (Cotswold Brook, and any after it): the saver hands over
/// the photo and the water's outline, and gets back the fitted pixels and a
/// water mask for the ripples (WaterRipples.cs) to work on.
///
/// FEYNMAN VERSION: think of a print slid into a picture frame that is a
/// different shape from the print. We never squash the print (that would make
/// the houses fat or thin). Instead we enlarge it just enough to fill the
/// frame in BOTH directions, and whatever sticks out past the frame is
/// trimmed off. On a wide 21:9 screen a strip comes off the top and bottom;
/// on a tall portrait screen most of the sides go. This is called "cover"
/// fitting, the same thing a phone does with a wallpaper.
///
/// Every place in the photo is written down as a FRACTION of the photo
/// (0 = left or top edge, 1 = right or bottom edge), measured once by eye on
/// a grid laid over the picture. ToScreen turns a fraction into a screen
/// pixel, wherever the frame happened to trim.
/// </summary>
public sealed class PhotoBackdrop
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>The fitted photo, one 0x00RRGGBB number per screen pixel (as FrameBuffer stores them).</summary>
    public uint[] Pixels { get; }

    /// <summary>
    /// How tall the whole photo is on this screen, in pixels (taller than the
    /// screen when the frame trims the top and bottom). Every size in the
    /// animation is a fraction of this, so a ripple or a puff of smoke stays
    /// the same size RELATIVE TO THE HOUSES on every screen.
    /// </summary>
    public float P { get; }

    /// <summary>
    /// Where the water is, 0 to 255 per screen pixel: 255 = open water, 0 =
    /// not water, in between = the soft edge. Soft, so the ripples fade out
    /// toward the bank and the reeds instead of stopping at a hard line.
    /// </summary>
    public byte[] Water { get; }

    /// <summary>The same, as plain yes/no ("surely water"), for stencils.</summary>
    public bool[] OpenWater { get; }

    /// <summary>The first and last screen rows that hold any water.</summary>
    public int WaterTop { get; }
    public int WaterBottom { get; }

    /// <summary>
    /// How NEAR each screen pixel's scenery is, 0 (far: sky, distant trees) to
    /// 255 (the nearest grass), or null when the saver gave no depth map. Made
    /// once, outside the saver, by an AI model that guesses distance from a
    /// single picture (scripts/depthmap.py). It lets a moving thing sit IN
    /// the photo: "draw this firefly only where the scenery is no nearer
    /// than it is", so a reed in front of it hides it.
    /// </summary>
    public byte[]? Depth { get; }

    private readonly float _left, _top, _shownW;

    /// <param name="photoFile">The photo's bytes (usually a resource embedded in the saver's .scr).</param>
    /// <param name="waterOutline">
    /// The water's outline, as fractions of the photo (x, y), going round it.
    /// Measured by eye on a grid over the photo; a few percent off only moves
    /// where the soft edge fades.
    /// </param>
    /// <param name="dryInWater">
    /// Shapes standing IN the water (a bridge pier, a boat), cut back out of
    /// it, or they would wobble like a reflection.
    /// </param>
    /// <param name="focusX">
    /// Where the trimming falls when the screen is narrower than the photo:
    /// this fraction of the overflow comes off the left, the rest off the
    /// right. 0.5 trims both sides evenly; less keeps more of the left.
    /// </param>
    /// <param name="focusY">The same for the top and bottom when the screen is wider than the photo.</param>
    /// <param name="depthFile">
    /// Optional: the photo's depth map (a grey picture, white = near), any
    /// size as long as it is the photo's shape. It is fitted exactly like the
    /// photo, so its pixels line up with the photo's on screen.
    /// </param>
    public PhotoBackdrop(int width, int height, Stream photoFile, PointF[] waterOutline, PointF[][] dryInWater,
        float focusX = 0.5f, float focusY = 0.5f, Stream? depthFile = null)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        using var photo = new Bitmap(photoFile);

        // Cover fit: the larger of the two enlargements, so both directions are filled.
        float scale = Math.Max(Width / (float)photo.Width, Height / (float)photo.Height);
        float shownW = photo.Width * scale, shownH = photo.Height * scale;
        _left = (Width - shownW) * focusX;      // the overflow (zero or less) is trimmed focusX off the left, the rest off the right
        _top = (Height - shownH) * focusY;      // and the same up and down
        _shownW = shownW;
        P = shownH;

        Pixels = Fit(photo);
        if (depthFile != null)
        {
            // The depth map is grey, so red = green = blue: keep any one of them.
            using var depth = new Bitmap(depthFile);
            Depth = Fit(depth).Select(c => (byte)(c & 0xFF)).ToArray();
        }

        Water = PaintWaterMask(waterOutline, dryInWater, out int top, out int bottom);
        WaterTop = top;
        WaterBottom = bottom;
        OpenWater = Water.Select(b => b > 200).ToArray();
    }

    /// <summary>
    /// Enlarges a picture of the photo's shape to the screen and trims it,
    /// with the best (slowest) filter the drawing kit has. This happens once
    /// at startup, so the cost does not matter; a cheap filter would leave
    /// the stone and the leaves jagged.
    /// </summary>
    private uint[] Fit(Bitmap picture)
    {
        using var fitted = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(fitted))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var edges = new ImageAttributes();
            edges.SetWrapMode(WrapMode.TileFlipXY);              // no dark seam at the borders from the filter reading past the edge
            g.DrawImage(picture, new Rectangle((int)MathF.Floor(_left), (int)MathF.Floor(_top), (int)MathF.Ceiling(_shownW) + 1, (int)MathF.Ceiling(P) + 1),
                0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel, edges);
        }
        return ReadPixels(fitted);
    }

    /// <summary>A box on the photo (fractions) as a four-corner outline, for dryInWater.</summary>
    public static PointF[] Box(float left, float top, float width, float height) =>
        [new(left, top), new(left + width, top), new(left + width, top + height), new(left, top + height)];

    /// <summary>Turns a spot on the photo (fractions) into a screen pixel.</summary>
    public PointF ToScreen(float fx, float fy) => new(_left + fx * _shownW, _top + fy * P);

    /// <summary>
    /// Draws the water's outline as a white shape on black, softens its edge
    /// with a blur, and keeps the result as the 0 to 255 mask.
    /// </summary>
    private byte[] PaintWaterMask(PointF[] outline, PointF[][] dry, out int top, out int bottom)
    {
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPolygon(Brushes.White, outline.Select(p => ToScreen(p.X, p.Y)).ToArray());
            foreach (PointF[] shape in dry)
                g.FillPolygon(Brushes.Black, shape.Select(p => ToScreen(p.X, p.Y)).ToArray());
        }
        uint[] px = ReadPixels(bmp);
        var mask = new byte[px.Length];
        for (int i = 0; i < px.Length; i++) mask[i] = (byte)(px[i] & 0xFF);

        // Feather: blur the hard edge so it fades over about half a percent of the photo.
        int radius = Math.Max(1, (int)(P * 0.005f));
        mask = BoxBlur(mask, Width, Height, radius);

        top = Height; bottom = -1;
        for (int y = 0; y < Height; y++)
        {
            int row = y * Width;
            for (int x = 0; x < Width; x++)
                if (mask[row + x] > 0) { top = Math.Min(top, y); bottom = y; break; }
        }
        if (bottom < 0) { top = 0; bottom = -1; }                 // no water on screen at all (a very odd shape): nothing to ripple
        return mask;
    }

    /// <summary>
    /// A blur done as "average of the neighbours", sideways then up and down.
    /// Two passes of a flat average look close to a smooth fade, and each
    /// pass is cheap because a running total slides along the row: add the
    /// pixel entering the window, subtract the one leaving.
    /// </summary>
    private static byte[] BoxBlur(byte[] src, int w, int h, int r)
    {
        var tmp = new byte[src.Length];
        var dst = new byte[src.Length];
        int span = 2 * r + 1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w, sum = 0;
            for (int x = -r; x <= r; x++) sum += src[row + Math.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = (byte)(sum / span);
                sum += src[row + Math.Min(w - 1, x + r + 1)] - src[row + Math.Max(0, x - r)];
            }
        }
        for (int x = 0; x < w; x++)
        {
            int sum = 0;
            for (int y = -r; y <= r; y++) sum += tmp[Math.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = (byte)(sum / span);
                sum += tmp[Math.Min(h - 1, y + r + 1) * w + x] - tmp[Math.Max(0, y - r) * w + x];
            }
        }
        return dst;
    }

    private static uint[] ReadPixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new int[bmp.Width * bmp.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            var px = new uint[raw.Length];
            for (int i = 0; i < raw.Length; i++) px[i] = (uint)raw[i] & 0x00FFFFFF;   // drop the top byte: FrameBuffer leaves it unused
            return px;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
