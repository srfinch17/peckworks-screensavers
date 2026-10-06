using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Yozakura ("night cherry blossoms"): as the evening deepens, small lamps at
/// the feet of the cherry trees come on one after another along each bank, and
/// every tree glows pink-white from below, the way a park lights its blossoms
/// for people to walk under. The blossom tops glitter faintly. Then, together,
/// the lights fade out.
///
/// FEYNMAN VERSION: think of a torch held under a pink paper lampshade. The
/// light does not spill into the room, it only lights the paper. Here the
/// "paper" is the blossom, so the glow must only land on blossom pixels, never
/// on the sky around the tree (that would look like a fog halo). The scene
/// gives us the finished picture (Pixels), so in the constructor we look at
/// every pixel near each canopy and mark the pinkish ones (a trunk is dark,
/// the sky is orange, a blossom is pink). That yes/no map is the stencil that
/// every glow stamp is filtered through.
///
/// Three layers per tree: a big soft pink glow centered low on the canopy
/// (where the lamp would light it), a few tiny twinkles scattered across the
/// upper blossoms (each on its own beat), and a small warm lamp at the
/// trunk's foot.
///
/// Cost: the canopy glow is the big stamp (six of them), so each is only as
/// big as its own canopy and the stencil lets the stamp skip every clear
/// pixel at once.
/// </summary>
internal sealed class Yozakura : Happening
{
    private const float Total = 16f;
    private const float RampSeconds = 1.8f;       // how long one tree takes to come up to full light

    private readonly DuskScenery _s;
    private readonly bool[] _blossom;             // one yes/no per pixel of the frame: "this is blossom"
    private readonly int[][] _lit;                // per tree: the frame positions of its blossom pixels that the glow reaches
    private readonly byte[][] _weight;            // per tree: how strong the glow is on each of those pixels (0 to 255)
    private readonly Sprite _lamp, _spark;
    private readonly PointF[] _lampAt;
    private readonly float[] _start;              // when each tree begins to light, in seconds
    private readonly List<(int Tree, PointF At, float Phase, float Beat)> _sparks = [];

    public override float Seconds => Total;
    public override int Layer => 1;               // on the banks and in the trees, in front of the boat

    public Yozakura(DuskScenery s)
    {
        _s = s;
        int w = s.Width, h = s.Height;
        float u = s.U;
        int n = s.Canopies.Count;
        _blossom = new bool[w * h];
        _lit = new int[n][];
        _weight = new byte[n][];
        _lampAt = new PointF[n];
        _start = new float[n];

        // ---- the stencil: pinkish pixels inside each canopy's circle ----
        for (int i = 0; i < n; i++)
        {
            var (c, r) = s.Canopies[i];
            float reach = r * 1.12f;                       // blossom pokes a little past the nominal circle
            int x0 = Math.Max(0, (int)(c.X - reach)), x1 = Math.Min(w - 1, (int)(c.X + reach));
            int y0 = Math.Max(0, (int)(c.Y - reach)), y1 = Math.Min(h - 1, (int)(c.Y + reach));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - c.X, dy = y - c.Y;
                    if (dx * dx + dy * dy > reach * reach) continue;
                    uint p = s.Pixels[y * w + x];
                    int pr = (int)((p >> 16) & 0xFF), pg = (int)((p >> 8) & 0xFF), pb = (int)(p & 0xFF);
                    // Blossom is pink: red leads, and blue is not far behind green.
                    // The dark trunk fails the "red is high" test; orange sky fails the blue one.
                    if (pr >= 120 && pr - pg >= 30 && pb >= pg - 8) _blossom[y * w + x] = true;
                }


            // The lamp sits at the trunk's foot, on the bank's walk path.
            _lampAt[i] = new PointF(c.X, s.WalkY(c.X) - u * 0.008f);

            // Twinkles: a golden-angle spiral (a pattern that never lines up) over the
            // upper canopy, kept only where the pixel really is blossom. Fixed numbers,
            // no dice, so the constructor stays repeatable.
            int kept = 0;
            for (int k = 0; k < 70 && kept < 14; k++)
            {
                float ang = k * 2.39996f;
                float rr = r * 0.95f * MathF.Sqrt((k + 0.5f) / 70f);
                float sx = c.X + rr * MathF.Cos(ang), sy = c.Y - r * 0.1f + rr * MathF.Sin(ang) * 0.9f;
                if (sy > c.Y + r * 0.1f) continue;         // upper canopy only
                int ix = (int)sx, iy = (int)sy;
                if (ix < 0 || ix >= w || iy < 0 || iy >= h || !_blossom[iy * w + ix]) continue;
                _sparks.Add((i, new PointF(sx, sy), k * 1.7f, 1.6f + (k % 5) * 0.45f));
                kept++;
            }
        }

        // ---- the glow, as a list: every blossom pixel within reach of each tree's light,
        //      with how strong the light is there. A soft round pool, as wide as the
        //      canopy, centered low on it. Strength is (1 - distance/radius) squared,
        //      which is the same smooth melt-out as Sprite.SoftFalloff. Keeping only the
        //      blossom pixels means Draw never even looks at sky or trunk. ----
        for (int i = 0; i < n; i++)
        {
            var (c, r) = s.Canopies[i];
            float rad = Math.Max(6f, r * 1.05f), cx = c.X, cy = c.Y + r * 0.22f;
            var idx = new List<int>();
            var wgt = new List<byte>();
            int x0 = Math.Max(0, (int)(cx - rad)), x1 = Math.Min(w - 1, (int)(cx + rad));
            int y0 = Math.Max(0, (int)(cy - rad)), y1 = Math.Min(h - 1, (int)(cy + rad));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!_blossom[y * w + x]) continue;
                    float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad;
                    if (d >= 0.97f) continue;   // (the very faint rim of the pool is skipped: too faint to see, costly to blend)
                    float k = 1f - d;
                    if (k * k * 255f < 10f) continue;
                    idx.Add(y * w + x);
                    wgt.Add((byte)(k * k * 255f));
                }
            _lit[i] = [.. idx];
            _weight[i] = [.. wgt];
        }

        // Order of lighting: along each bank, from its outer edge inward (left bank
        // left to right, right bank right to left), the two banks together.
        for (int i = 0; i < n; i++)
        {
            float x = s.Canopies[i].At.X;
            bool left = x < w * 0.5f;
            int rank = 0;
            for (int j = 0; j < n; j++)
            {
                float xj = s.Canopies[j].At.X;
                if ((xj < w * 0.5f) == left && (left ? xj < x : xj > x)) rank++;
            }
            _start[i] = 1.0f + 2.2f * rank + (left ? 0f : 0.5f);
        }

        _lamp = Sprite.Glow(Math.Max(4, (int)(u * 0.036f)), Color.FromArgb(255, 186, 96));
        _spark = Sprite.Glow(Math.Max(2, (int)(u * 0.009f)), Color.FromArgb(255, 240, 230));
    }

    /// <summary>Lights tree i's blossom pixels: each moves toward warm pink-white by (its strength x opacity).</summary>
    private void Glow(FrameBuffer fb, int i, float opacity)
    {
        int[] idx = _lit[i];
        byte[] wgt = _weight[i];
        uint[] px = fb.Pixels;
        int o256 = (int)(Math.Min(1f, opacity) * 256);
        for (int k = 0; k < idx.Length; k++)
        {
            int a = wgt[k] * o256 >> 8;                          // 0 to 255
            if (a == 0) continue;
            // The "two channels at once" blend: red and blue sit in one number and green in
            // another, so three channels cost two multiplies, not three. (Pixels are
            // stored 0x00RRGGBB.)
            uint bg = px[idx[k]];
            uint rb = bg & 0x00FF00FF, gg = bg & 0x0000FF00;
            rb = (rb + ((((uint)0x00FF00E2 - rb) * (uint)a) >> 8)) & 0x00FF00FF;
            gg = (gg + ((((uint)0x0000EC00 - gg) * (uint)a) >> 8)) & 0x0000FF00;
            px[idx[k]] = rb | gg;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float closing = Smooth((Total - t) / 3f);                // all fade out together at the end
        for (int i = 0; i < _lit.Length; i++)
        {
            float on = Smooth((t - _start[i]) / RampSeconds) * closing;
            if (on <= 0.01f) continue;
            // A very slow breathing, so the glow is never perfectly still.
            float breath = 0.94f + 0.06f * MathF.Sin(t * 0.9f + i * 1.3f);
            Glow(fb, i, 0.72f * on * breath);
            _lamp.DrawCentered(fb, _lampAt[i].X, _lampAt[i].Y, 0.9f * on * (0.92f + 0.08f * MathF.Sin(t * 7f + i * 2f)));
        }

        foreach (var (tree, at, phase, beat) in _sparks)
        {
            float on = Smooth((t - _start[tree] - 0.5f) / RampSeconds) * closing;
            if (on <= 0.01f) continue;
            // Mostly dark, with a short bright blink once per beat.
            float blink = MathF.Pow(Math.Max(0f, MathF.Sin(t * beat * 2f + phase)), 8f);
            if (blink > 0.02f) _spark.DrawCentered(fb, at.X, at.Y, 0.85f * on * blink);
        }
    }
}
