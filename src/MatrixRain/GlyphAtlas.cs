using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MatrixRain;

/// <summary>
/// Pre-drawn stamps of every character the rain uses.
///
/// FEYNMAN VERSION: drawing text properly is slow. The computer has to read the
/// font's curve outlines, fill them, and smooth the edges. Doing that for
/// thousands of characters, 60 times a second, would crawl.
///
/// So we cheat like a rubber-stamp shop. At startup we draw each character
/// ONCE, carefully, and keep the result as a little grid of numbers: how much
/// ink (0 = none, 255 = full) each pixel of the character has. That grid is
/// called a "mask" or "stamp". Later, drawing a character is just copying its
/// stamp onto the screen in whatever color we want. Copying numbers is fast.
///
/// "Atlas" is the graphics word for "a collection of pre-drawn little pictures
/// you look things up in", like an atlas of maps.
///
/// WHICH CHARACTERS? The film's code is mostly Japanese half-width katakana
/// (a compact phonetic alphabet), drawn MIRRORED (flipped left to right), mixed
/// with some digits and a few Latin symbols. The production designer, Simon
/// Whiteley, has said he scanned characters from his wife's Japanese cookbooks.
/// </summary>
internal sealed class GlyphAtlas
{
    public int CellWidth { get; }
    public int CellHeight { get; }
    public int Count => _masks.Length;

    // One stamp per character. Each is CellWidth * CellHeight bytes, rows end to end.
    private readonly byte[][] _masks;

    /// <summary>
    /// Pick-list of glyph numbers, with katakana listed several times so random
    /// picks land on katakana most of the time, like in the film.
    /// </summary>
    public int[] WeightedPicks { get; }

    // Fonts to try, in order. All ship with Windows and include half-width katakana.
    private static readonly string[] FontCandidates = ["MS Gothic", "Yu Gothic", "MS UI Gothic", "Meiryo", "Consolas"];

    public GlyphAtlas(int cellWidth, int cellHeight)
    {
        CellWidth = cellWidth;
        CellHeight = cellHeight;

        // ---- Build the list of (character, mirrored?) pairs --------------------
        var glyphs = new List<(string Text, bool Mirror)>();
        var picks = new List<int>();

        // Half-width katakana live at Unicode code points FF66 to FF9D (hex).
        // Unicode is the giant numbered list of every character in every language;
        // (char)0xFF71 is "the character numbered FF71", which is ｱ ("a").
        for (int cp = 0xFF66; cp <= 0xFF9D; cp++)
        {
            int index = glyphs.Count;
            glyphs.Add((((char)cp).ToString(), true));
            picks.AddRange([index, index, index, index]);   // 4 tickets each in the raffle
        }

        // Digits appear un-mirrored in the film.
        foreach (char ch in "0123456789")
        {
            picks.AddRange([glyphs.Count, glyphs.Count]);    // 2 tickets each
            glyphs.Add((ch.ToString(), false));
        }

        // A sprinkle of Latin letters, punctuation, and one kanji (日, "sun/day"),
        // all of which show up in frame grabs of the film's code.
        foreach (char ch in "Z:.\"=*+-<>|¦日")
        {
            picks.Add(glyphs.Count);                         // 1 ticket each
            glyphs.Add((ch.ToString(), ch != '日'));
        }

        WeightedPicks = [.. picks];
        _masks = new byte[glyphs.Count][];

        // ---- Draw each character once and keep its stamp ------------------------
        FontFamily family = PickFont();

        // The character's height as a share of the cell. A little under 1.0 leaves
        // a thin gap between vertically stacked characters, as in the film.
        float emSize = cellHeight * 0.95f;

        // MS Gothic's strokes are thinner than the film's. We thicken them by
        // tracing the outline with a pen as well as filling it, like going over
        // a pencil drawing with a marker. Round joins keep the corners soft.
        using var pen = new Pen(Color.White, Math.Max(1f, cellHeight / 18f)) { LineJoin = LineJoin.Round };

        // A scratch canvas one cell big. Format32bppRgb = 4 bytes per pixel.
        using var bmp = new Bitmap(cellWidth, cellHeight, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        // AntiAlias = soften the edges with in-between shades so curves look smooth
        // instead of staircase-jagged.
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        for (int i = 0; i < glyphs.Count; i++)
        {
            g.Clear(Color.Black);

            // We turn the character into a "path" (its outline as geometry)
            // rather than using DrawString, because a path can tell us its exact
            // bounding box. That lets us center every character precisely in its
            // cell, and flip it like a mirror image.
            using var path = new GraphicsPath();
            path.AddString(glyphs[i].Text, family, (int)FontStyle.Regular, emSize,
                PointF.Empty, StringFormat.GenericTypographic);

            RectangleF b = path.GetBounds();
            if (b.Width > 0 && b.Height > 0)
            {
                // Three moves, like handling a paper cut-out:
                //   1. slide it so its center sits at (0, 0)
                //   2. if needed, flip it over left-to-right (x becomes -x)
                //   3. slide it to the middle of the cell
                using (var m = new Matrix(1, 0, 0, 1, -(b.X + b.Width / 2), -(b.Y + b.Height / 2)))
                    path.Transform(m);
                if (glyphs[i].Mirror)
                    using (var m = new Matrix(-1, 0, 0, 1, 0, 0))
                        path.Transform(m);
                using (var m = new Matrix(1, 0, 0, 1, cellWidth / 2f, cellHeight / 2f))
                    path.Transform(m);

                g.FillPath(Brushes.White, path);
                g.DrawPath(pen, path);
            }

            _masks[i] = ReadInk(bmp);
        }
    }

    /// <summary>The stamp for glyph number i.</summary>
    public byte[] Mask(int i) => _masks[i];

    /// <summary>
    /// Copy the drawn character out of the Bitmap into a plain byte array.
    /// We drew white on black, so any one color channel tells us the ink amount.
    /// </summary>
    private byte[] ReadInk(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, CellWidth, CellHeight);
        // LockBits pins the bitmap's pixels in memory and hands us their address,
        // so we can bulk-copy them out instead of calling GetPixel per pixel
        // (which is famously slow).
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new byte[data.Stride * CellHeight];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            var mask = new byte[CellWidth * CellHeight];
            for (int y = 0; y < CellHeight; y++)
                for (int x = 0; x < CellWidth; x++)
                    mask[y * CellWidth + x] = raw[y * data.Stride + x * 4 + 1]; // +1 = the green byte
            return mask;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static FontFamily PickFont()
    {
        using var installed = new InstalledFontCollection();
        foreach (string name in FontCandidates)
            foreach (FontFamily f in installed.Families)
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return new FontFamily(f.Name);
        return FontFamily.GenericMonospace;
    }
}
