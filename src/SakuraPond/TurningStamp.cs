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
        float half = MathF.Sqrt(_w * _w + _h * _h) * 0.5f * scale + 2;
        int x0 = Math.Max(0, (int)(cx - half)), x1 = Math.Min(fb.Width - 1, (int)(cx + half));
        int y0 = Math.Max(0, (int)(cy - half * squash)), y1 = Math.Min(fb.Height - 1, (int)(cy + half * squash));
        float inv = 1 / scale, mx = _w / 2f, my = _h / 2f;
        uint[] px = fb.Pixels;
        for (int y = y0; y <= y1; y++)
        {
            float dy = (y + 0.5f - cy) / squash;
            int row = y * fb.Width;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                // Un-turn and un-scale: where on the original picture is this pixel?
                float u = (dx * ca + dy * sa) * inv + mx - 0.5f, v = (-dx * sa + dy * ca) * inv + my - 0.5f;
                if (u < -1 || v < -1 || u >= _w || v >= _h) continue;
                int iu = (int)MathF.Floor(u), iv = (int)MathF.Floor(v);
                float fu = u - iu, fv = v - iv;
                float a = 0, r = 0, g = 0, b = 0;
                for (int k = 0; k < 4; k++)
                {
                    int su = iu + (k & 1), sv = iv + (k >> 1);
                    if (su < 0 || sv < 0 || su >= _w || sv >= _h) continue;
                    float wgt = ((k & 1) == 1 ? fu : 1 - fu) * ((k >> 1) == 1 ? fv : 1 - fv);
                    int i = sv * _w + su;
                    a += _a[i] * wgt; r += _r[i] * wgt; g += _g[i] * wgt; b += _b[i] * wgt;
                }
                if (a <= 0.003f) continue;
                int idx = row + x;
                if (onlyWhere != null && !onlyWhere[idx]) continue;
                uint bg = px[idx];
                if (shadow is Color sc)
                {
                    px[idx] = FrameBuffer.Blend(bg, sc.R, sc.G, sc.B, a * opacity);
                    continue;
                }
                // Back from premultiplied: colour = premultiplied / solidness.
                float ia = 1 / a;
                px[idx] = FrameBuffer.Blend(bg, (int)Math.Min(255, r * ia * 255), (int)Math.Min(255, g * ia * 255), (int)Math.Min(255, b * ia * 255), a * opacity);
            }
        }
    }
}
