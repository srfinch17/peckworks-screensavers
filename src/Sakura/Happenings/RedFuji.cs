using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Aka Fuji, "red Fuji": the mountain blushes a deep red, its snow turns
/// pink, and then it slowly cools back to blue. Early on a clear morning
/// the low sun really does this to Fuji for a few minutes, and Hokusai made
/// it one of the most famous prints in Japan.
///
/// How: the painter hands over a stencil of the mountain (FujiMask: true on
/// every pixel the mountain painted that nothing nearer covers). In the
/// constructor we walk that stencil once and work out, for every mountain
/// pixel, the color it should turn: dark rock goes deep red, bright snow
/// goes warm pink, and everything between in proportion to how bright it
/// is. That keeps the snow's ragged edge and the gullies visible, just in
/// new colors. Then Draw only has to blend each pixel toward its red twin
/// by the strength of the blush right now.
///
/// Think of it as two transparencies of the same picture, one blue and one
/// red, and a dimmer that slides from one to the other.
/// </summary>
internal sealed class RedFuji : Happening
{
    private static readonly Color Rock = Color.FromArgb(168, 58, 44);     // Hokusai's red
    private static readonly Color Snow = Color.FromArgb(255, 200, 182);   // pink morning snow

    private readonly int[] _where;     // which pixels are mountain (their places in the picture)
    private readonly uint[] _red;      // the red twin of each of those pixels

    public override float Seconds => 11f;
    public override string? Claims => "fuji,costly";

    public RedFuji(Scenery s)
    {
        // Two passes over the stencil: count, then fill (no growing lists).
        int n = 0;
        foreach (bool m in s.FujiMask) if (m) n++;
        _where = new int[n];
        _red = new uint[n];
        for (int i = 0, k = 0; i < s.FujiMask.Length; i++)
        {
            if (!s.FujiMask[i]) continue;
            uint p = s.Pixels[i];
            int r = (int)((p >> 16) & 0xFF), g = (int)((p >> 8) & 0xFF), b = (int)(p & 0xFF);
            // How bright this pixel is, 0 (dark rock) to 1 (snow). The mountain's rock
            // is about 0.4 to 0.55 and its snow about 0.97, so stretch that range to 0..1.
            float lum = (0.3f * r + 0.59f * g + 0.11f * b) / 255f;
            float snowy = Math.Clamp((lum - 0.40f) / 0.55f, 0f, 1f);
            // Keep a little of the pixel's own light and shade, so the shadowed
            // right-hand face stays darker than the lit left one.
            float shade = 0.75f + 0.45f * lum;
            int nr = (int)Math.Min(255, (Rock.R + (Snow.R - Rock.R) * snowy) * shade);
            int ng = (int)Math.Min(255, (Rock.G + (Snow.G - Rock.G) * snowy) * shade);
            int nb = (int)Math.Min(255, (Rock.B + (Snow.B - Rock.B) * snowy) * shade);
            _where[k] = i;
            _red[k] = (uint)((nr << 16) | (ng << 8) | nb);
            k++;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // Warm up slowly (4 s), glow (3 s), cool down slowly (4 s): the real
        // thing is a slow change of light, never a switch.
        float blush = 0.78f * Fade(t, Seconds, 4f, 4f);
        if (blush < 0.01f) return;

        // Whole-number blending (see Sprite.Draw for why): a over 256.
        int a = (int)(blush * 256);
        uint[] px = fb.Pixels;
        for (int k = 0; k < _where.Length; k++)
        {
            int i = _where[k];
            uint bg = px[i], to = _red[k];
            int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
            int r = br + (((int)((to >> 16) & 0xFF) - br) * a >> 8);
            int g = bgn + (((int)((to >> 8) & 0xFF) - bgn) * a >> 8);
            int b = bb + (((int)(to & 0xFF) - bb) * a >> 8);
            px[i] = (uint)((r << 16) | (g << 8) | b);
        }
    }
}
