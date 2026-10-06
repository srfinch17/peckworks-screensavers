using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Kasa-gumo, the "hat cloud": a smooth, stacked, flying-saucer cloud that
/// forms right on top of Fuji's summit. It is a real thing. Wind pushed up
/// and over the mountain makes the damp air rise, cool and turn to cloud at
/// the crest of each wave, so the cloud stays put while the wind streams
/// through it. Photographers wait years to catch one.
///
/// How: the cap is three or four flattened ellipses stacked like pancakes.
/// It is so small and so simple (a few hundred pixels across) that there is
/// no point painting it as a sprite: Draw works out every pixel of every
/// layer straight from the ellipse sum, like pressing a round cookie cutter
/// into dough, but with a soft edge. That also lets the layers swell and
/// slide a little on slow sine waves for free ("breathing").
///
/// Each layer is white on top (the sun lands there), fading to a grey-blue
/// underside (the shade), and a little see-through right at the thin rims.
/// The shaded undersides are what make the stack read as separate crisp
/// layers instead of one blob.
///
/// Stencil: the cap sits ON the summit, partly in front of the snow, so it
/// may be stamped over bare sky and over the mountain, but never over the
/// overhanging branches (they are nearer than Fuji and stay in front).
/// </summary>
internal sealed class KasaGumo : Happening
{
    private const int Layers = 4;

    private readonly bool[] _where;      // sky or mountain: where the cap may be drawn (not the branches)
    private readonly int _w, _h;
    private readonly float _cx, _cy, _u;

    public override float Seconds => 16f;
    public override string? Claims => "fuji";

    public KasaGumo(Scenery s)
    {
        _w = s.Width; _h = s.Height; _u = s.U;
        _cx = s.FujiSummit.X;
        _cy = s.FujiSummit.Y - s.U * 0.012f;       // the lowest layer sits just over the crater rim
        _where = new bool[s.OpenSky.Length];
        for (int i = 0; i < _where.Length; i++) _where[i] = s.OpenSky[i] || s.FujiMask[i];
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float env = Fade(t, Seconds, 3.5f, 4f);
        if (env < 0.01f) return;
        float grow = Smooth(t / 4f);                              // swells up from nothing over 4 s
        float thin = 1f - 0.55f * Smooth((t - 12f) / 4f);         // and flattens away over the last 4 s
        if (grow < 0.02f) return;

        for (int i = 0; i < Layers; i++)                          // bottom layer first, so upper ones sit on it
        {
            // Half-width: 0.16 U for the bottom pancake, narrower going up.
            float a = _u * 0.16f * (1f - 0.17f * i) * (1f + 0.05f * (float)Math.Sin(0.8 * t + i * 1.9)) * grow;
            // Half-height: 0.0155 U, floored at 2 px so the layers stay visible in the preview box.
            float b = Math.Max(2f, _u * 0.0155f * (1f + 0.12f * (float)Math.Sin(0.6 * t + i * 2.3)) * thin) * Math.Min(1f, grow * 1.5f);
            float cx = _cx + _u * 0.007f * (float)Math.Sin(0.5 * t + i * 1.3);              // slow slide
            float cy = _cy - i * Math.Max(2f, _u * 0.012f) * grow;
            if (a < 2f) continue;

            float soft = Math.Max(0.1f, 1.5f / b);                // edge softness: about a pixel and a half
            int x0 = Math.Max(0, (int)(cx - a) - 1), x1 = Math.Min(_w - 1, (int)(cx + a) + 1);
            int y0 = Math.Max(0, (int)(cy - b) - 1), y1 = Math.Min(_h - 1, (int)(cy + b) + 1);
            for (int y = y0; y <= y1; y++)
            {
                float ny = (y + 0.5f - cy) / b;
                for (int x = x0; x <= x1; x++)
                {
                    int at = y * _w + x;
                    if (!_where[at]) continue;
                    float nx = (x + 0.5f - cx) / a;
                    float d2 = nx * nx + ny * ny;
                    if (d2 >= 1f) continue;
                    float e = Smooth((1f - MathF.Sqrt(d2)) / soft);       // 0 at the edge, 1 inside
                    // Thin rims are a little see-through, the middle nearly solid.
                    float alpha = e * (0.55f + 0.40f * (1f - nx * nx)) * env;
                    // 0 on the sunlit top, 1 on the shaded underside.
                    float shade = Smooth((ny + 0.15f) / 1.15f) * 0.8f;
                    int r = (int)(252 + (170 - 252) * shade);
                    int g = (int)(253 + (186 - 253) * shade);
                    int bl = (int)(255 + (216 - 255) * shade);
                    fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], r, g, bl, alpha);
                }
            }
        }
    }
}
