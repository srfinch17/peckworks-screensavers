using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// A picture painted once (a dragonfly, a swallow with its wings in one
/// position) that can be stamped at ANY angle and size, every frame.
///
/// FEYNMAN VERSION: hold a postcard over a table and turn it. For each spot
/// on the table under the card, you could ask "which spot on the card is
/// above me?" and that is the colour there. That is how this draws: for each
/// screen pixel in the stamp's area, turn the question backward ("if I
/// un-turn the card, where on the original picture am I?") and read the
/// picture there, blending the four nearest picture pixels by how close each
/// is (so a turn of half a degree moves the picture smoothly, never in
/// pixel-sized jumps).
///
/// The picture is stored "premultiplied": each colour already multiplied by
/// how solid it is. That way, blending a solid black edge pixel with a clear
/// neighbour gives a half-clear black, not a half-clear grey halo.
///
/// The same stamp drawn all dark and faint is the animal's SHADOW on the
/// water or the ground.
/// </summary>
internal sealed class TurningStamp
{
    private readonly float[] _a, _r, _g, _b;       // premultiplied, 0..1
    private readonly int _w, _h;

    /// <summary>The picture faces RIGHT (heading 0) when painted, centred on its middle.</summary>
    public TurningStamp(int width, int height, Action<Graphics> paint)
    {
        _w = Math.Max(2, width);
        _h = Math.Max(2, height);
        using var bmp = new Bitmap(_w, _h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            paint(g);
        }
        var data = bmp.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var raw = new int[_w * _h];
        Marshal.Copy(data.Scan0, raw, 0, raw.Length);
        bmp.UnlockBits(data);
        _a = new float[raw.Length]; _r = new float[raw.Length]; _g = new float[raw.Length]; _b = new float[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            uint c = (uint)raw[i];
            float a = (c >> 24) / 255f;
            _a[i] = a;
            _r[i] = ((c >> 16) & 0xFF) / 255f * a;
            _g[i] = ((c >> 8) & 0xFF) / 255f * a;
            _b[i] = (c & 0xFF) / 255f * a;
        }
    }

    /// <param name="angle">The way it faces, in radians (0 = right, as painted).</param>
    /// <param name="scale">1 = as painted.</param>
    /// <param name="squash">How much flatter top to bottom (the slant we see the pond at), 1 = none.</param>
    /// <param name="opacity">0 = invisible, 1 = as painted.</param>
    /// <param name="shadow">Not null: draw as a shadow of this colour instead of the picture's own colours.</param>
    /// <param name="onlyWhere">Stencil: draw only where true.</param>
    public void Draw(FrameBuffer fb, float cx, float cy, float angle, float scale, float squash, float opacity,
        Color? shadow = null, bool[]? onlyWhere = null)
    {
        if (opacity <= 0.003f) return;
        float ca = MathF.Cos(angle), sa = MathF.Sin(angle);
        // The box on the screen the turned picture can touch: the picture's
        // rectangle turned, not the circle round its diagonal (which visited
        // a third more pixels, all empty, at 45 degrees).
        float aca = MathF.Abs(ca), asa = MathF.Abs(sa);
        float hx = (_w * aca + _h * asa) * 0.5f * scale + 2, hy = (_w * asa + _h * aca) * 0.5f * scale + 2;
        int x0 = Math.Max(0, (int)(cx - hx)), x1 = Math.Min(fb.Width - 1, (int)(cx + hx));
        int y0 = Math.Max(0, (int)(cy - hy * squash)), y1 = Math.Min(fb.Height - 1, (int)(cy + hy * squash));
        float inv = 1 / scale, mx = _w / 2f, my = _h / 2f;
        // Un-turning is a straight-line map: along a row, u and v each change
        // by the same amount per pixel, so they are stepped, not recomputed.
        float du = ca * inv, dv = -sa * inv;
        int op256 = (int)(opacity * 256);
        bool isShadow = shadow is Color;
        int sr = 0, sg = 0, sb = 0;
        if (shadow is Color sc) { sr = sc.R; sg = sc.G; sb = sc.B; }
        uint[] px = fb.Pixels;
        for (int y = y0; y <= y1; y++)
        {
            float dy = (y + 0.5f - cy) / squash;
            int row = y * fb.Width;
            float dx0 = x0 + 0.5f - cx;
            float u = (dx0 * ca + dy * sa) * inv + mx - 0.5f, v = (-dx0 * sa + dy * ca) * inv + my - 0.5f;
            for (int x = x0; x <= x1; x++, u += du, v += dv)
            {
                if (u < -1 || v < -1 || u >= _w || v >= _h) continue;
                int idx = row + x;
                if (onlyWhere != null && !onlyWhere[idx]) continue;
                int iu = (int)MathF.Floor(u), iv = (int)MathF.Floor(v);
                float fu = u - iu, fv = v - iv;
                float w00 = (1 - fu) * (1 - fv), w10 = fu * (1 - fv), w01 = (1 - fu) * fv, w11 = fu * fv;
                float a, r = 0, g = 0, b = 0;
                if (iu >= 0 && iv >= 0 && iu + 1 < _w && iv + 1 < _h)
                {
                    // Inside the picture: the four neighbours are all there, no edge tests.
                    int i = iv * _w + iu;
                    a = _a[i] * w00 + _a[i + 1] * w10 + _a[i + _w] * w01 + _a[i + _w + 1] * w11;
                    if (a <= 0.003f) continue;
                    if (!isShadow)
                    {
                        r = _r[i] * w00 + _r[i + 1] * w10 + _r[i + _w] * w01 + _r[i + _w + 1] * w11;
                        g = _g[i] * w00 + _g[i + 1] * w10 + _g[i + _w] * w01 + _g[i + _w + 1] * w11;
                        b = _b[i] * w00 + _b[i + 1] * w10 + _b[i + _w] * w01 + _b[i + _w + 1] * w11;
                    }
                }
                else
                {
                    // On the picture's edge: only the neighbours that exist.
                    a = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int su = iu + (k & 1), sv = iv + (k >> 1);
                        if (su < 0 || sv < 0 || su >= _w || sv >= _h) continue;
                        float wgt = (k & 1) == 1 ? ((k >> 1) == 1 ? w11 : w10) : ((k >> 1) == 1 ? w01 : w00);
                        int i = sv * _w + su;
                        a += _a[i] * wgt;
                        if (!isShadow) { r += _r[i] * wgt; g += _g[i] * wgt; b += _b[i] * wgt; }
                    }
                    if (a <= 0.003f) continue;
                }
                uint bg = px[idx];
                int a256 = (int)(a * op256);
                if (isShadow)
                {
                    px[idx] = FrameBuffer.Blend256(bg, sr, sg, sb, a256);   // a shadow needs only how solid, not the colours
                    continue;
                }
                // Back from premultiplied: colour = premultiplied / solidness.
                float ia = 255 / a;
                px[idx] = FrameBuffer.Blend256(bg, (int)Math.Min(255, r * ia), (int)Math.Min(255, g * ia), (int)Math.Min(255, b * ia), a256);
            }
        }
    }
}
