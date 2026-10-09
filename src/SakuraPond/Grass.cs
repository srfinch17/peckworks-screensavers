using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The lawn in the wind. Two things move:
///
///   1. The CARPET: the thousands of short blades the painter drew (a layer of
///      their own, see ShorePainter.Lawn). Each frame the carpet is laid back
///      over the ground SHIFTED a few pixels downwind by however hard the
///      wind leans the grass at that spot, and a little brighter where it
///      leans hardest (bent blades show more of their pale tips to the sky).
///      So a gust runs across the whole lawn as a band of blades leaning and
///      lightening, the way wind shows on a field.
///   2. The TUFTS: taller clumps standing in the carpet, each a flip-book of
///      24 pages from "springing back a little upwind" to "flat downwind".
///      Every frame each tuft stamps the page nearest its lean.
///
/// FEYNMAN VERSION of the wind: watch a lawn from a window above it. A gust
/// is not everywhere at once; it runs ACROSS the grass as a travelling wave,
/// and each blade leans as the wave reaches it and springs back as it
/// passes. Leaning over, a blade's tip moves downwind along the ground and
/// the blade looks SHORTER from above (it is bending toward the ground); its
/// shadow stretches with it. Here the wind at any spot is one number, the
/// LEAN (0 upright, 1 flat downwind): a slow swell that comes and goes over
/// half a minute, times two waves running downwind, a long one and a short
/// one, with the long one's crests sharpened so the lawn is mostly calm with
/// a band of bent grass rolling through it.
///
/// A tuft's blades are worked out in three dimensions (the bend angle grows
/// from root to tip; each short step of the blade goes a little less UP and
/// a little more ALONG the ground) and projected like everything else in the
/// pond: height lifts a point up the screen (Light.Lift) and throws its
/// shadow down and to the right.
/// </summary>
internal sealed class Grass
{
    private const int Poses = 24, Designs = 10;
    private const float LeanMin = -0.2f, LeanMax = 1.1f;      // the flip-book's range

    private readonly struct Tuft(int x, int y, byte design, float stiff, float jitter)
    {
        public readonly int X = x, Y = y;                       // the root, screen pixels
        public readonly byte Design = design;
        public readonly float Stiff = stiff, Jitter = jitter;   // how far it leans for a given wind; its own offset in the wave
    }

    private readonly struct Page(Sprite sprite, int ax, int ay)
    {
        public readonly Sprite Sprite = sprite;
        public readonly int Ax = ax, Ay = ay;                   // where the root sits inside this page
    }

    private readonly Pond _pond;
    private readonly Page[,] _pages;                            // [design, pose]
    private readonly Tuft[][] _bands;                           // tufts by horizontal band, each band far to near (see Draw)
    private readonly int _bandRows;
    private readonly float _u, _windX, _windY;
    private readonly byte[] _ca, _cr, _cg, _cb;                 // the carpet layer, premultiplied: alpha, and red, green, blue already scaled by it
    private readonly int _x0, _y0, _x1, _y1;                    // the lawn's box on screen
    private readonly float _shift;                              // how far (pixels) the carpet slides downwind at lean 1
    private readonly int _cell, _gw, _gh;                       // the wind grid: lean worked out every "cell" pixels, blended between
    private readonly float[] _lean;
    private double _time;

    public Grass(Pond pond, Random rng)
    {
        _pond = pond;
        _u = pond.U;
        float h = _u * 0.036f;                                  // a tall blade, screen pixels
        _shift = _u * 0.009f;
        // The breeze blows to the right and a little toward us.
        _windX = MathF.Cos(0.26f); _windY = MathF.Sin(0.26f);

        // The carpet layer, premultiplied so the slide can blend four neighbours cheaply.
        _ca = new byte[pond.Carpet.Length]; _cr = new byte[pond.Carpet.Length]; _cg = new byte[pond.Carpet.Length]; _cb = new byte[pond.Carpet.Length];
        for (int i = 0; i < pond.Carpet.Length; i++)
        {
            uint c = pond.Carpet[i];
            int a = (int)(c >> 24);
            _ca[i] = (byte)a;
            _cr[i] = (byte)(((c >> 16) & 0xFF) * a / 255); _cg[i] = (byte)(((c >> 8) & 0xFF) * a / 255); _cb[i] = (byte)((c & 0xFF) * a / 255);
        }
        // The lawn's box: where the carpet or any grassy pixel is.
        _x0 = pond.Width; _y0 = pond.Height; _x1 = 0; _y1 = 0;
        for (int y = 0; y < pond.Height; y++)
            for (int x = 0; x < pond.Width; x++)
                if (pond.Grassy[y * pond.Width + x]) { _x0 = Math.Min(_x0, x); _x1 = Math.Max(_x1, x); _y0 = Math.Min(_y0, y); _y1 = Math.Max(_y1, y); }
        if (_x1 < _x0) { _x0 = _y0 = 0; _x1 = _y1 = -1; }
        _cell = Math.Max(4, (int)(_u / 120));
        _gw = (_x1 - _x0) / _cell + 2; _gh = (_y1 - _y0) / _cell + 2;
        _lean = new float[Math.Max(1, _gw * _gh)];

        // The tufts' pages, each trimmed to the pixels it actually uses.
        _pages = new Page[Designs, Poses];
        int pw = (int)MathF.Ceiling(h * 2.2f), ph = (int)MathF.Ceiling(h * 1.7f);
        int ax = (int)(h * 0.85f), ay = (int)(h * 0.95f);
        for (int d = 0; d < Designs; d++)
        {
            Blade[] design = Design(h * (d < Designs / 2 ? 0.72f : 1f), rng);      // half the designs are shorter tufts
            for (int p = 0; p < Poses; p++)
            {
                float lean = LeanMin + (LeanMax - LeanMin) * p / (Poses - 1);
                _pages[d, p] = Trim(pw, ph, ax, ay, g => PaintTuft(g, design, lean, ax, ay));
            }
        }

        // Plant the tufts: random spots on the lawn (not sand, stones or
        // rocks, and not right against them), each at least "space" from
        // every tuft already planted, so they scatter the way plants do
        // rather than standing in rows. A coarse grid of "taken" cells
        // makes the spacing test quick.
        float space = _u * 0.011f;
        float keep = h * 0.25f;
        int occW = (int)(pond.Width / space) + 2, occH = (int)(pond.Height / space) + 2;
        var taken = new List<PointF>[occW * occH];
        var tufts = new List<Tuft>();
        int want = (int)((_x1 - _x0 + 1) * (_y1 - _y0 + 1) / (space * space) * 0.55f);
        for (int tries = 0; tries < want * 6 && tufts.Count < want; tries++)
        {
            int x = _x0 + rng.Next(_x1 - _x0 + 1), y = _y0 + rng.Next(_y1 - _y0 + 1);
            if (!Lawn(x, y) || !Lawn(x - keep, y) || !Lawn(x + keep, y) || !Lawn(x, y - keep) || !Lawn(x, y + keep)) continue;
            int ox = (int)(x / space), oy = (int)(y / space);
            bool crowded = false;
            for (int j = oy - 1; j <= oy + 1 && !crowded; j++)
                for (int i = ox - 1; i <= ox + 1 && !crowded; i++)
                {
                    List<PointF>? near = i >= 0 && j >= 0 && i < occW && j < occH ? taken[j * occW + i] : null;
                    if (near != null) foreach (PointF q in near) if ((q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y) < space * space) { crowded = true; break; }
                }
            if (crowded) continue;
            (taken[oy * occW + ox] ??= new List<PointF>()).Add(new PointF(x, y));
            tufts.Add(new Tuft(x, y, (byte)rng.Next(Designs), 0.7f + 0.6f * (float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.9f));
        }
        // Bands taller than a page: tufts in two bands that are not next to
        // each other can never touch, so those bands can be drawn at the same
        // time on different cores. Even bands go first, then odd bands.
        _bandRows = ph + 2;
        int bands = pond.Height / _bandRows + 1;
        _bands = Enumerable.Range(0, bands).Select(b => tufts.Where(t => t.Y / _bandRows == b).OrderBy(t => t.Y).ToArray()).ToArray();
    }

    private bool Lawn(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Grassy[iy * _pond.Width + ix];
    }

    public void Update(float dt) => _time += dt;

    /// <summary>The lean at one spot right now, 0 upright to about 1 flat downwind.</summary>
    private float LeanAt(float x, float y, float t, float gust, float jitter)
    {
        // How far along the wind this spot lies, in u, on the ground (the screen's y is squashed).
        float along = (x * _windX + y / Ripples.Squash * _windY) / _u;
        float w1 = MathF.Sin(MathF.Tau * (along / 0.7f - t * 0.6f) + jitter);
        float crest = (0.5f + 0.5f * w1); crest *= crest;                         // sharpened: calm troughs, a narrow band of bent grass
        float w2 = MathF.Sin(MathF.Tau * (along / 0.3f - t * 1.5f) + 2.1f + jitter);
        return 0.08f + gust * (0.15f + 0.75f * crest + 0.12f * w2);
    }

    public void Draw(FrameBuffer fb)
    {
        if (_x1 < _x0) return;
        float t = (float)(_time % 100000.0);
        // The swell: the same rhythm as the petals' breeze, so grass and petals gust together.
        float gust = 0.25f + 0.75f * (0.5f + 0.5f * MathF.Sin(t * 0.21f)) * (0.6f + 0.4f * MathF.Sin(t * 0.073f + 1));

        // The wind on a coarse grid (a sine per pixel would cost more than the drawing).
        for (int gy = 0; gy < _gh; gy++)
            for (int gx = 0; gx < _gw; gx++)
                _lean[gy * _gw + gx] = LeanAt(_x0 + gx * _cell, _y0 + gy * _cell, t, gust, 0);

        SlideCarpet(fb);
        // The tufts, far to near within each band; even bands together, then
        // odd bands together (a tuft only ever touches tufts in its own band
        // or the next, never two bands away). Where a band's last tuft meets
        // the next band's first the order is off by one band: that is a
        // blade's edge in front of a neighbour's, which the eye cannot tell.
        foreach (int parity in (ReadOnlySpan<int>)[0, 1])
            Parallel.For(0, (_bands.Length + 1 - parity) / 2, k =>
            {
                foreach (Tuft tuft in _bands[k * 2 + parity])
                {
                    float lean = LeanAt(tuft.X, tuft.Y, t, gust, tuft.Jitter) * tuft.Stiff;
                    int page = Math.Clamp((int)MathF.Round((lean - LeanMin) / (LeanMax - LeanMin) * (Poses - 1)), 0, Poses - 1);
                    Page pg = _pages[tuft.Design, page];
                    pg.Sprite.Draw(fb, tuft.X - pg.Ax, tuft.Y - pg.Ay);
                }
            });
    }

    /// <summary>
    /// Lays the carpet over the lawn, each pixel taken from a little UPWIND
    /// of itself (so the blades appear pushed downwind) and brightened by the
    /// lean. Rows are shared out across the processor's cores.
    /// </summary>
    private void SlideCarpet(FrameBuffer fb)
    {
        int w = fb.Width;
        uint[] px = fb.Pixels;
        Parallel.For(_y0, _y1 + 1, y =>
        {
            float gy = (y - _y0) / (float)_cell;
            int iy = Math.Min(_gh - 2, (int)gy); float fy = gy - iy;
            for (int x = _x0; x <= _x1; x++)
            {
                int i = y * w + x;
                if (!_pond.Grassy[i]) continue;
                float gx = (x - _x0) / (float)_cell;
                int ix = Math.Min(_gw - 2, (int)gx); float fx = gx - ix;
                float a0 = _lean[iy * _gw + ix], a1 = _lean[iy * _gw + ix + 1], b0 = _lean[(iy + 1) * _gw + ix], b1 = _lean[(iy + 1) * _gw + ix + 1];
                float lean = (a0 + (a1 - a0) * fx) * (1 - fy) + (b0 + (b1 - b0) * fx) * fy;
                // Where this pixel's grass came from: upwind by the lean.
                float sx = x - lean * _shift * _windX, sy = y - lean * _shift * _windY * Ripples.Squash;
                int sx0 = (int)MathF.Floor(sx), sy0 = (int)MathF.Floor(sy);
                float tx = sx - sx0, ty = sy - sy0;
                if (sx0 < 0 || sy0 < 0 || sx0 + 1 >= w || sy0 + 1 >= fb.Height) continue;
                int j = sy0 * w + sx0;
                float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
                float a = _ca[j] * w00 + _ca[j + 1] * w10 + _ca[j + w] * w01 + _ca[j + w + 1] * w11;
                if (a < 2) continue;
                // Brighter where it leans: bent blades show their pale tips.
                float bright = 1 + 0.35f * (lean - 0.25f);
                float r = (_cr[j] * w00 + _cr[j + 1] * w10 + _cr[j + w] * w01 + _cr[j + w + 1] * w11) * bright;
                float g = (_cg[j] * w00 + _cg[j + 1] * w10 + _cg[j + w] * w01 + _cg[j + w + 1] * w11) * bright;
                float b = (_cb[j] * w00 + _cb[j + 1] * w10 + _cb[j + w] * w01 + _cb[j + w + 1] * w11) * bright;
                uint bg = px[i];
                float keep = 1 - a / 255f;
                int rr = (int)(((bg >> 16) & 0xFF) * keep + r), gg = (int)(((bg >> 8) & 0xFF) * keep + g), bb = (int)((bg & 0xFF) * keep + b);
                px[i] = (uint)(Math.Min(255, rr) << 16 | Math.Min(255, gg) << 8 | Math.Min(255, bb));
            }
        });
    }

    /// <summary>One blade of a tuft: its height, which way it splays, how far, how much the wind bends it, its width and colours.</summary>
    private readonly record struct Blade(float H, float AzX, float AzY, float Droop, float Flex, float Wid, Color Root, Color Tip);

    private static Blade[] Design(float h, Random rng)
    {
        var blades = new Blade[6 + rng.Next(5)];
        for (int i = 0; i < blades.Length; i++)
        {
            float az = (float)(rng.NextDouble() * MathF.Tau);
            Color g = Color.FromArgb(60 + rng.Next(30), 104 + rng.Next(40), 38 + rng.Next(24));
            blades[i] = new Blade(
                h * (0.55f + 0.45f * (float)rng.NextDouble()), MathF.Cos(az), MathF.Sin(az),
                0.25f + 0.45f * (float)rng.NextDouble(), 0.9f + 0.5f * (float)rng.NextDouble(),
                h * (0.09f + 0.05f * (float)rng.NextDouble()),
                Color.FromArgb((int)(g.R * 0.55f), (int)(g.G * 0.6f), (int)(g.B * 0.55f)),
                Color.FromArgb(Math.Min(255, g.R + 90), Math.Min(255, g.G + 70), Math.Min(255, g.B + 40)));
        }
        return blades;
    }

    /// <summary>
    /// Paints one tuft at one amount of lean: every blade's shadow first,
    /// then the blades, root to tip in eight short steps, each step bent a
    /// little further over (its own splay plus the wind's push).
    /// </summary>
    private static void PaintTuft(Graphics g, Blade[] blades, float lean, int ax, int ay)
    {
        const int Steps = 8;
        float windX = MathF.Cos(0.26f), windY = MathF.Sin(0.26f);
        var top = new PointF[Steps + 1];
        var ground = new PointF[Steps + 1];
        var poly = new PointF[(Steps + 1) * 2];
        using var shadow = new SolidBrush(Color.FromArgb(48, 4, 20, 8));
        foreach (bool shadows in (ReadOnlySpan<bool>)[true, false])
            foreach (Blade b in blades)
            {
                // The bend: its own splay in its own direction, plus the wind's push downwind.
                float bx = b.Droop * b.AzX + lean * b.Flex * windX, by = b.Droop * b.AzY + lean * b.Flex * windY;
                float mag = MathF.Sqrt(bx * bx + by * by);
                float dx = mag > 1e-4f ? bx / mag : b.AzX, dy = mag > 1e-4f ? by / mag : b.AzY;
                float gx = 0, gy = 0, z = 0;
                top[0] = new PointF(ax, ay); ground[0] = new PointF(ax, ay);
                for (int s = 1; s <= Steps; s++)
                {
                    float phi = MathF.Min(1.5f, mag * MathF.Pow((s - 0.5f) / Steps, 1.3f));
                    float step = b.H / Steps;
                    gx += MathF.Sin(phi) * step * dx; gy += MathF.Sin(phi) * step * dy; z += MathF.Cos(phi) * step;
                    top[s] = new PointF(ax + gx, ay + gy * Ripples.Squash - z * Light.Lift);
                    ground[s] = new PointF(ax + gx + z * Light.ShadowX, ay + (gy + z * Light.ShadowY) * Ripples.Squash);
                }
                PointF[] line = shadows ? ground : top;
                // A strip tapering from the root's width to a point at the tip.
                for (int s = 0; s <= Steps; s++)
                {
                    PointF p = line[s], q = line[Math.Min(Steps, s + 1)], o = line[Math.Max(0, s - 1)];
                    float ex = q.X - o.X, ey = q.Y - o.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
                    float wid = b.Wid * (1 - 0.9f * s / Steps) * 0.5f;
                    poly[s] = new PointF(p.X - ey / el * wid, p.Y + ex / el * wid);
                    poly[poly.Length - 1 - s] = new PointF(p.X + ey / el * wid, p.Y - ex / el * wid);
                }
                if (shadows) { g.FillPolygon(shadow, poly); continue; }
                using var brush = new LinearGradientBrush(top[0], top[Steps], b.Root, b.Tip);
                g.FillPolygon(brush, poly);
            }
    }

    /// <summary>
    /// Paints a page into a full-size sheet, then keeps only the box of
    /// pixels that are not clear, remembering where the root landed in it.
    /// Stamping a trimmed page visits far fewer pixels every frame.
    /// </summary>
    private static Page Trim(int w, int h, int ax, int ay, Action<Graphics> paint)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            paint(g);
        }
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var raw = new int[w * h];
        Marshal.Copy(data.Scan0, raw, 0, raw.Length);
        bmp.UnlockBits(data);
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((raw[y * w + x] >>> 24) != 0) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        if (x1 < 0) { x0 = y0 = 0; x1 = y1 = 0; }
        int tw = x1 - x0 + 1, th = y1 - y0 + 1;
        var cut = new uint[tw * th];
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
                cut[y * tw + x] = (uint)raw[(y0 + y) * w + x0 + x];
        return new Page(Sprite.FromPixels(tw, th, cut), ax - x0, ay - y0);
    }
}
