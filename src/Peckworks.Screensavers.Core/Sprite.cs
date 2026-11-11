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

    /// <summary>The sheet itself, one 0xAARRGGBB number per pixel, row by row: for a scene that wants to lay a sprite into a backdrop once by its own hand.</summary>
    public ReadOnlySpan<uint> Argb => _argb;

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

    /// <summary>One painted pixel, ARGB (alpha in the top byte): for measuring a page (where its paint actually is).</summary>
    public uint PixelAt(int x, int y) => _argb[y * Width + x];

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
    /// <param name="shade">How much light falls on the sprite, 0 (black) to 256 (as painted): a tuft
    /// standing in a branch's shadow is stamped darker, so the dappled shade on the ground reaches it too.</param>
    public void Draw(FrameBuffer fb, int left, int top, float opacity = 1f, bool[]? onlyWhere = null, bool mirror = false,
        byte[]? depth = null, int nearness = 255, int shade = 256)
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
            DrawShared(fb, left, top, x0, x1, y0, y1, opacity256, onlyWhere, mirror, depth, nearness, shade);
        else
            DrawRows(fb, left, top, x0, x1, y0, y1, opacity256, onlyWhere, mirror, depth, nearness, shade);
    }

    // Its own method, so the hand-out (which needs a lambda, and so a little
    // garbage) is only paid for by the big sprites, not every small glow.
    private void DrawShared(FrameBuffer fb, int left, int top, int x0, int x1, int y0, int y1, int opacity256,
        bool[]? onlyWhere, bool mirror, byte[]? depth, int nearness, int shade) =>
        Parallel.For(0, (y1 - y0 + 31) / 32, band =>
            DrawRows(fb, left, top, x0, x1, y0 + band * 32, Math.Min(y1, y0 + band * 32 + 32), opacity256, onlyWhere, mirror, depth, nearness, shade));

    /// <summary>Stamps the sprite's rows y0 to y1 (the visible columns x0 to x1).</summary>
    private void DrawRows(FrameBuffer fb, int left, int top, int x0, int x1, int y0, int y1, int opacity256,
        bool[]? onlyWhere, bool mirror, byte[]? depth, int nearness, int shade)
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
                int cr = (int)((c >> 16) & 0xFF), cg = (int)((c >> 8) & 0xFF), cb = (int)(c & 0xFF);
                if (shade != 256) { cr = cr * shade >> 8; cg = cg * shade >> 8; cb = cb * shade >> 8; }   // in shadow: darker
                int r = br + ((cr - br) * a >> 8);
                int g = bgn + ((cg - bgn) * a >> 8);
                int b = bb + ((cb - bb) * a >> 8);
                frame[dst + sx] = (uint)((r << 16) | (g << 8) | b);
            }
        }
    }

    /// <summary>
    /// Stamps the sprite with its top-left corner at a FRACTION of a pixel: (left, top) are decimals.
    ///
    /// FEYNMAN VERSION: the screen is a grid, so a plain stamp can only sit on whole pixels. Something walking at
    /// one and a half pixels a frame then moves 1, 2, 1, 2, 1, 2: a judder the eye reads as "not smooth", however
    /// good the drawing is. (His words, twice in one day: "silky".) The cure is what a slow camera pan does: a
    /// sticker that sits 0.3 of a pixel to the right is drawn as 70 percent of itself in each pixel plus 30
    /// percent of itself in the next pixel along. Do that across and down and the picture glides between grid
    /// lines. Each screen pixel under the stamp takes its colour from up to four neighbouring sprite pixels,
    /// weighted by how much of each one lies over it (this is called bilinear filtering).
    ///
    /// It costs about four times a plain stamp, so it is for the things that MOVE (an animal's body), not for
    /// hundreds of glows. When the fraction is tiny it simply calls the plain stamp.
    /// </summary>
    public void DrawSmooth(FrameBuffer fb, float left, float top, float opacity = 1f, bool[]? onlyWhere = null, bool mirror = false,
        byte[]? depth = null, int nearness = 255)
    {
        if (opacity <= 0) return;
        int L = (int)MathF.Floor(left), T = (int)MathF.Floor(top);
        int fx = (int)((left - L) * 256), fy = (int)((top - T) * 256);       // the fractions, as 0 to 255 out of 256
        if (fx < 6 && fy < 6) { Draw(fb, L, T, opacity, onlyWhere, mirror, depth, nearness); return; }
        int opacity256 = (int)(Math.Min(1f, opacity) * 256);
        uint[] frame = fb.Pixels;
        int w = Width, h = Height, fw = fb.Width, fh = fb.Height;

        // Row m of the stamp on screen is fed by sprite rows m - 1 (weight fy) and m (weight 1 - fy); the same across.
        for (int m = 0; m <= h; m++)
        {
            int dy = T + m;
            if (dy < 0 || dy >= fh) continue;
            bool hasUp = m - 1 >= 0, hasDown = m < h;
            // The painted stretch of the two sprite rows that feed this screen row, widened by one for the spill to the right.
            int first = int.MaxValue, last = -1;
            if (hasUp && _rowLast[m - 1] >= _rowFirst[m - 1]) { first = Math.Min(first, _rowFirst[m - 1]); last = Math.Max(last, _rowLast[m - 1]); }
            if (hasDown && _rowLast[m] >= _rowFirst[m]) { first = Math.Min(first, _rowFirst[m]); last = Math.Max(last, _rowLast[m]); }
            if (last < 0) continue;
            int k0, k1;
            if (mirror) { k0 = w - 1 - last; k1 = w - 1 - first + 1; } else { k0 = first; k1 = last + 1; }
            int wyUp = fy, wyDown = 256 - fy;
            for (int k = k0; k <= k1; k++)
            {
                int dx = L + k;
                if (dx < 0 || dx >= fw) continue;
                int at = dy * fw + dx;
                if (onlyWhere != null && !onlyWhere[at]) continue;
                if (depth != null && depth[at] > nearness) continue;

                // The four sprite pixels that lie partly over this screen pixel, each with its share (out of 65536).
                int sa = 0, sr = 0, sg = 0, sb = 0;
                for (int q = 0; q < 4; q++)
                {
                    int sx = (q & 1) == 0 ? k - 1 : k, sy = q < 2 ? m - 1 : m;
                    if (sx < 0 || sx >= w || sy < 0 || sy >= h) continue;
                    int wt = ((q & 1) == 0 ? fx : 256 - fx) * (q < 2 ? wyUp : wyDown);
                    if (wt == 0) continue;
                    uint c = _argb[sy * w + (mirror ? w - 1 - sx : sx)];
                    int a = (int)(c >> 24);
                    if (a == 0) continue;
                    int aw = a * wt >> 8;                                    // this pixel's solidness times its share
                    sa += aw;
                    sr += (int)((c >> 16) & 0xFF) * aw >> 8;
                    sg += (int)((c >> 8) & 0xFF) * aw >> 8;
                    sb += (int)(c & 0xFF) * aw >> 8;
                }
                if (sa <= 0) continue;
                // sa is solidness times 256 (0 to 65280); the colour sums are colour times solidness. Divide the
                // colour back out so a half-covered edge keeps the sprite's colour and only its solidness is halved.
                int cr = Math.Min(255, (sr << 8) / sa), cg = Math.Min(255, (sg << 8) / sa), cb = Math.Min(255, (sb << 8) / sa);
                int alpha = (sa >> 8) * opacity256 >> 8;
                if (alpha <= 0) continue;
                uint bg = frame[at];
                int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                frame[at] = (uint)(((br + ((cr - br) * alpha >> 8)) << 16) | ((bgn + ((cg - bgn) * alpha >> 8)) << 8) | (bb + ((cb - bb) * alpha >> 8)));
            }
        }
    }

    /// <summary>
    /// Stamps a picture that is PART WAY BETWEEN two pages of a flip-book: "w" of the way from page a to page b
    /// (0 = all a, 1 = all b), each page at its own place, both at fractions of a pixel.
    ///
    /// FEYNMAN VERSION: a flip-book has a fixed number of pages. A reindeer's head has 24 of them between "nose on
    /// the snow" and "head high", so each page turn swings the antler tips four pixels: fine while the head moves
    /// fast, but a head that drifts slowly then TICKS, four pixels every few frames, and pale antlers ticking
    /// against a dark pine are exactly what the eye catches (the pop detector found forty in ten seconds). Drawing
    /// four times as many pages would cost four times the memory. Instead, between two neighbouring pages we show a
    /// DISSOLVE: where both pages are solid the result is solid, and where only one of them is, that sliver fades
    /// in or out as the head moves on. The antler tip no longer jumps from one place to the next; one tip fades
    /// out while the next fades in, a pixel or two away, which at this size reads as smooth movement.
    ///
    /// It is the honest way to mix two see-through pictures: each pixel's solidness and its colour-times-solidness
    /// are mixed separately (called premultiplied alpha), so a solid brown pixel mixed with an empty one becomes
    /// half-solid brown, never half-solid mud.
    /// </summary>
    public static void DrawBlend(FrameBuffer fb, Sprite a, float aLeft, float aTop, Sprite b, float bLeft, float bTop, float w,
        bool mirror = false, byte[]? depth = null, int nearness = 255)
    {
        w = Math.Clamp(w, 0f, 1f);
        if (w < 0.02f) { a.DrawSmooth(fb, aLeft, aTop, 1f, null, mirror, depth, nearness); return; }
        if (w > 0.98f) { b.DrawSmooth(fb, bLeft, bTop, 1f, null, mirror, depth, nearness); return; }
        int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(aLeft, bLeft))), y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(aTop, bTop)));
        int x1 = Math.Min(fb.Width - 1, (int)MathF.Ceiling(MathF.Max(aLeft + a.Width, bLeft + b.Width)));
        int y1 = Math.Min(fb.Height - 1, (int)MathF.Ceiling(MathF.Max(aTop + a.Height, bTop + b.Height)));
        uint[] frame = fb.Pixels;
        int fw = fb.Width;
        float wa = 1f - w;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int at = y * fw + x;
                if (depth != null && depth[at] > nearness) continue;
                a.Sample(x - aLeft, y - aTop, mirror, out float aa, out float ar, out float ag, out float ab);
                b.Sample(x - bLeft, y - bTop, mirror, out float ba, out float br, out float bg, out float bb);
                float oa = aa * wa + ba * w;                                // solidness, 0 to 1
                if (oa < 0.004f) continue;
                float r = (ar * wa + br * w) / oa, g = (ag * wa + bg * w) / oa, bl = (ab * wa + bb * w) / oa;
                uint bgp = frame[at];
                float pr = (bgp >> 16) & 0xFF, pg = (bgp >> 8) & 0xFF, pb = bgp & 0xFF;
                int outR = (int)(pr + (r - pr) * oa + 0.5f), outG = (int)(pg + (g - pg) * oa + 0.5f), outB = (int)(pb + (bl - pb) * oa + 0.5f);
                frame[at] = (uint)((Math.Clamp(outR, 0, 255) << 16) | (Math.Clamp(outG, 0, 255) << 8) | Math.Clamp(outB, 0, 255));
            }
    }

    /// <summary>
    /// Reads the sprite at a point between its pixels (u across, v down, decimals): the four pixels round the
    /// point, each weighted by how near it is. Hands back solidness (0 to 1) and colour TIMES solidness (0 to 255).
    /// </summary>
    private void Sample(float u, float v, bool mirror, out float alpha, out float r, out float g, out float b)
    {
        alpha = r = g = b = 0f;
        if (u <= -1f || v <= -1f || u >= Width || v >= Height) return;
        int ux = (int)MathF.Floor(u), vy = (int)MathF.Floor(v);
        float fx = u - ux, fy = v - vy;
        for (int q = 0; q < 4; q++)
        {
            int sx = ux + (q & 1), sy = vy + (q >> 1);
            if (sx < 0 || sy < 0 || sx >= Width || sy >= Height) continue;
            float wt = ((q & 1) == 0 ? 1f - fx : fx) * (q < 2 ? 1f - fy : fy);
            if (wt <= 0f) continue;
            uint c = _argb[sy * Width + (mirror ? Width - 1 - sx : sx)];
            float pa = (c >> 24) / 255f * wt;
            if (pa <= 0f) continue;
            alpha += pa;
            r += ((c >> 16) & 0xFF) * pa; g += ((c >> 8) & 0xFF) * pa; b += (c & 0xFF) * pa;
        }
    }
}
