using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The wind over the pond, as one number at any spot and moment: the LEAN,
/// 0 for grass standing upright to about 1 for grass pushed flat downwind.
///
/// FEYNMAN VERSION: watch a lawn from a window above it. A gust is not
/// everywhere at once; it runs ACROSS the grass as a travelling wave, and
/// each blade leans as the wave reaches it and springs back as it passes.
/// Here that is a slow swell that comes and goes over half a minute (the
/// same rhythm as the petals' breeze, so grass and petals gust together),
/// times two waves running downwind, a long one and a short one, with the
/// long one's crests sharpened so the lawn is mostly calm with a band of
/// bent grass rolling through it. The breeze blows to the right and a
/// little toward us.
/// </summary>
internal static class Wind
{
    public static readonly float X = MathF.Cos(0.26f), Y = MathF.Sin(0.26f);

    /// <summary>The swell right now, 0.25 (a breath) to 1 (a gust).</summary>
    public static float Gust(float t) => 0.25f + 0.75f * (0.5f + 0.5f * MathF.Sin(t * 0.21f)) * (0.6f + 0.4f * MathF.Sin(t * 0.073f + 1));

    /// <summary>The lean at screen point (x, y) at time t, given the swell; "jitter" is a thing's own offset in the wave.</summary>
    public static float LeanAt(float x, float y, float t, float gust, float u, float jitter)
    {
        // How far along the wind this spot lies, in u, on the ground (the screen's y is squashed).
        float along = (x * X + y / Ripples.Squash * Y) / u;
        float w1 = MathF.Sin(MathF.Tau * (along / 0.7f - t * 0.6f) + jitter);
        float crest = (0.5f + 0.5f * w1); crest *= crest;                         // sharpened: calm troughs, a narrow band of bent grass
        float w2 = MathF.Sin(MathF.Tau * (along / 0.3f - t * 1.5f) + 2.1f + jitter);
        return 0.08f + gust * (0.15f + 0.75f * crest + 0.12f * w2);
    }
}

/// <summary>
/// A plant drawn as a flip-book of pages, one per amount of lean: a tuft of
/// grass, or a clump of cattails. Each page is painted once; every frame the
/// plant stamps the page nearest its lean.
///
/// A blade is worked out in three dimensions: the bend angle grows from root
/// to tip (its own splay in its own direction, plus the wind's push downwind),
/// so each short step of the blade goes a little less UP and a little more
/// ALONG the ground. Then it is projected the way everything else in the pond
/// is: height lifts a point up the screen (Light.Lift), and throws its shadow
/// down and to the right. A cattail is a stiff blade with a brown head near
/// its top and a spike above that.
/// </summary>
internal static class Flipbook
{
    public const int Poses = 24;
    public const float LeanMin = -0.2f, LeanMax = 1.1f;      // the flip-book's range

    /// <summary>Which page shows a given lean.</summary>
    public static int PageOf(float lean) => Math.Clamp((int)MathF.Round((lean - LeanMin) / (LeanMax - LeanMin) * (Poses - 1)), 0, Poses - 1);

    public readonly struct Page(Sprite sprite, int ax, int ay)
    {
        public readonly Sprite Sprite = sprite;
        public readonly int Ax = ax, Ay = ay;                   // where the root sits inside this page
    }

    /// <summary>One blade: its height, which way it splays, how far, how much the wind bends it, its width and colours. Cattail = true for a stem with a head.</summary>
    public readonly record struct Blade(float H, float AzX, float AzY, float Droop, float Flex, float Wid, Color Root, Color Tip, bool Cattail);

    /// <summary>A tuft of grass: 6 to 10 blades splayed every way, each a little different, all from one root.</summary>
    public static (Blade Blade, PointF Root)[] Tuft(float h, Random rng)
    {
        var blades = new (Blade, PointF)[6 + rng.Next(5)];
        for (int i = 0; i < blades.Length; i++)
        {
            float az = (float)(rng.NextDouble() * MathF.Tau);
            Color g = Color.FromArgb(60 + rng.Next(30), 104 + rng.Next(40), 38 + rng.Next(24));
            blades[i] = (new Blade(
                h * (0.55f + 0.45f * (float)rng.NextDouble()), MathF.Cos(az), MathF.Sin(az),
                0.25f + 0.45f * (float)rng.NextDouble(), 0.9f + 0.5f * (float)rng.NextDouble(),
                h * (0.08f + 0.04f * (float)rng.NextDouble()),
                Color.FromArgb((int)(g.R * 0.55f), (int)(g.G * 0.6f), (int)(g.B * 0.55f)),
                Color.FromArgb(Math.Min(255, g.R + 90), Math.Min(255, g.G + 70), Math.Min(255, g.B + 40)), false), PointF.Empty);
        }
        return blades;
    }

    /// <summary>
    /// A clump of cattails: stiff stems with heads, standing nearly straight,
    /// and long leaves between them that splay and bend over. The roots are
    /// spread over a patch "spread" across (squashed, as the water is).
    /// </summary>
    public static (Blade Blade, PointF Root)[] CattailClump(float h, float spread, Random rng)
    {
        int stems = 8 + rng.Next(5), leaves = 10 + rng.Next(6);
        var all = new (Blade, PointF)[stems + leaves];
        for (int i = 0; i < all.Length; i++)
        {
            bool stem = i < stems;
            double a = rng.NextDouble() * Math.Tau, d = Math.Sqrt(rng.NextDouble()) * spread;
            var root = new PointF((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d * Ripples.Squash));
            float az = (float)(rng.NextDouble() * MathF.Tau);
            Color g = stem ? Color.FromArgb(96 + rng.Next(20), 124 + rng.Next(24), 56 + rng.Next(16)) : Color.FromArgb(70 + rng.Next(30), 116 + rng.Next(36), 44 + rng.Next(20));
            all[i] = (new Blade(
                stem ? h * (0.8f + 0.2f * (float)rng.NextDouble()) : h * (0.55f + 0.35f * (float)rng.NextDouble()),
                MathF.Cos(az), MathF.Sin(az),
                stem ? 0.04f + 0.1f * (float)rng.NextDouble() : 0.4f + 0.6f * (float)rng.NextDouble(),
                stem ? 0.3f + 0.15f * (float)rng.NextDouble() : 0.7f + 0.4f * (float)rng.NextDouble(),
                stem ? h * 0.018f : h * (0.03f + 0.015f * (float)rng.NextDouble()),
                Color.FromArgb((int)(g.R * 0.6f), (int)(g.G * 0.65f), (int)(g.B * 0.6f)),
                Color.FromArgb(Math.Min(255, g.R + 60), Math.Min(255, g.G + 50), Math.Min(255, g.B + 30)), stem), root);
        }
        return all;
    }

    /// <summary>
    /// Paints a clump at one amount of lean: every blade's shadow first,
    /// then the blades, each followed root to tip in short steps, bent a
    /// little further over at each. A cattail stem is painted in three
    /// parts: the stem, the brown head over the top fifth, and a pale
    /// spike above it.
    /// </summary>
    public static void PaintClump(Graphics g, (Blade Blade, PointF Root)[] clump, float lean, int ax, int ay, int shadowAlpha)
    {
        using var shadow = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 20, 8));
        foreach (bool shadows in (ReadOnlySpan<bool>)[true, false])
            foreach (var (b, root) in clump)
            {
                int steps = b.Cattail ? 12 : 8;
                var top = new PointF[steps + 1];
                var ground = new PointF[steps + 1];
                // The bend: its own splay in its own direction, plus the wind's push downwind.
                float bx = b.Droop * b.AzX + lean * b.Flex * Wind.X, by = b.Droop * b.AzY + lean * b.Flex * Wind.Y;
                float mag = MathF.Sqrt(bx * bx + by * by);
                float dx = mag > 1e-4f ? bx / mag : b.AzX, dy = mag > 1e-4f ? by / mag : b.AzY;
                float gx = 0, gy = 0, z = 0;
                float rx = ax + root.X, ry = ay + root.Y;
                top[0] = new PointF(rx, ry); ground[0] = new PointF(rx, ry);
                for (int s = 1; s <= steps; s++)
                {
                    float phi = MathF.Min(1.5f, mag * MathF.Pow((s - 0.5f) / steps, 1.3f));
                    float step = b.H / steps;
                    gx += MathF.Sin(phi) * step * dx; gy += MathF.Sin(phi) * step * dy; z += MathF.Cos(phi) * step;
                    top[s] = new PointF(rx + gx, ry + gy * Ripples.Squash - z * Light.Lift);
                    ground[s] = new PointF(rx + gx + z * Light.ShadowX, ry + (gy + z * Light.ShadowY) * Ripples.Squash);
                }
                PointF[] line = shadows ? ground : top;
                if (!b.Cattail)
                {
                    // A strip tapering from the root's width to a point at the tip.
                    PointF[] poly = Strip(line, s => b.Wid * (1 - 0.9f * s) * 0.5f);
                    if (shadows) { g.FillPolygon(shadow, poly); continue; }
                    using var brush = new LinearGradientBrush(top[0], top[steps], b.Root, b.Tip);
                    g.FillPolygon(brush, poly);
                    continue;
                }
                // A cattail: stem (steps 0 to 9), head (9 to 11), spike (11 to 12).
                float headW = b.Wid * 3.2f;
                PointF[] stem = Strip(line[..10], s => b.Wid * (1 - 0.3f * s) * 0.5f);
                PointF[] head = Strip(line[9..12], s => headW * 0.5f * MathF.Min(1, MathF.Min(s * 6 + 0.35f, (1 - s) * 6 + 0.35f)));
                PointF[] spike = Strip(line[11..13], s => b.Wid * 0.35f);
                if (shadows) { g.FillPolygon(shadow, stem); g.FillPolygon(shadow, head); continue; }
                using (var sb = new LinearGradientBrush(top[0], top[9], b.Root, b.Tip)) g.FillPolygon(sb, stem);
                // The head: a velvety brown sausage, lit along its sunny side.
                using (var hb = new LinearGradientBrush(new PointF(top[10].X - headW, top[10].Y), new PointF(top[10].X + headW, top[10].Y), Color.FromArgb(124, 84, 48), Color.FromArgb(70, 44, 24)))
                    g.FillPolygon(hb, head);
                using (var pb = new SolidBrush(Color.FromArgb(150, 128, 84))) g.FillPolygon(pb, spike);
            }
    }

    /// <summary>A strip along a line of points, "halfWidth(s)" wide either side at fraction s along it: left edge out, right edge back.</summary>
    private static PointF[] Strip(PointF[] line, Func<float, float> halfWidth)
    {
        int n = line.Length;
        var poly = new PointF[n * 2];
        for (int s = 0; s < n; s++)
        {
            PointF p = line[s], q = line[Math.Min(n - 1, s + 1)], o = line[Math.Max(0, s - 1)];
            float ex = q.X - o.X, ey = q.Y - o.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
            float wid = halfWidth(s / (n - 1f));
            poly[s] = new PointF(p.X - ey / el * wid, p.Y + ex / el * wid);
            poly[poly.Length - 1 - s] = new PointF(p.X + ey / el * wid, p.Y - ex / el * wid);
        }
        return poly;
    }

    /// <summary>
    /// Paints a page into a full-size sheet, then keeps only the box of
    /// pixels that are not clear, remembering where the root landed in it.
    /// Stamping a trimmed page visits far fewer pixels every frame.
    /// </summary>
    public static Page Trim(int w, int h, int ax, int ay, Action<Graphics> paint)
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

/// <summary>
/// The lawn in the wind. Two things move:
///
///   1. The CARPET: the thousands of blades the painter drew (a layer of
///      their own, see ShorePainter.Lawn). Each frame the carpet is laid back
///      over the ground SHIFTED a few pixels downwind by however hard the
///      wind leans the grass at that spot, and a little brighter where it
///      leans hardest (bent blades show more of their pale tips to the sky).
///      So a gust runs across the whole lawn as a band of blades leaning and
///      lightening, the way wind shows on a field.
///   2. The TUFTS: taller clumps standing in the carpet, each a flip-book
///      (Flipbook) of 24 pages from "springing back a little upwind" to
///      "flat downwind". Every frame each tuft stamps the page nearest its lean.
/// </summary>
internal sealed class Grass
{
    private const int Designs = 10;

    private readonly struct Tuft(int x, int y, byte design, float stiff, float jitter)
    {
        public readonly int X = x, Y = y;                       // the root, screen pixels
        public readonly byte Design = design;
        public readonly float Stiff = stiff, Jitter = jitter;   // how far it leans for a given wind; its own offset in the wave
    }

    private readonly Pond _pond;
    private readonly Flipbook.Page[,] _pages;                   // [design, pose]
    private readonly Tuft[][] _bands;                           // tufts by horizontal band, each band far to near (see Draw)
    private readonly float _u;
    private readonly byte[] _ca, _cr, _cg, _cb;                 // the carpet layer, premultiplied: alpha, and red, green, blue already scaled by it
    private readonly int _x0, _y0, _x1, _y1;                    // the lawn's box on screen
    private readonly float _shift;                              // how far (pixels) the carpet slides downwind at lean 1
    private readonly int _cell, _gw, _gh;                       // the wind grid: lean worked out every "cell" pixels, blended between
    private readonly float[] _lean;
    private readonly uint[] _slid;                              // the lawn box as last laid: half its rows are redone each frame
    private int _frame;
    private double _time;

    public Grass(Pond pond, Random rng)
    {
        _pond = pond;
        _u = pond.U;
        float h = _u * 0.05f;                                   // a tall blade, screen pixels
        _shift = _u * 0.012f;

        // The carpet layer, premultiplied so the slide can blend four neighbours cheaply.
        _ca = new byte[pond.Carpet.Length]; _cr = new byte[pond.Carpet.Length]; _cg = new byte[pond.Carpet.Length]; _cb = new byte[pond.Carpet.Length];
        for (int i = 0; i < pond.Carpet.Length; i++)
        {
            uint c = pond.Carpet[i];
            int a = (int)(c >> 24);
            _ca[i] = (byte)a;
            _cr[i] = (byte)(((c >> 16) & 0xFF) * a / 255); _cg[i] = (byte)(((c >> 8) & 0xFF) * a / 255); _cb[i] = (byte)((c & 0xFF) * a / 255);
        }
        // The lawn's box: where any grassy pixel is.
        _x0 = pond.Width; _y0 = pond.Height; _x1 = 0; _y1 = 0;
        for (int y = 0; y < pond.Height; y++)
            for (int x = 0; x < pond.Width; x++)
                if (pond.Grassy[y * pond.Width + x]) { _x0 = Math.Min(_x0, x); _x1 = Math.Max(_x1, x); _y0 = Math.Min(_y0, y); _y1 = Math.Max(_y1, y); }
        if (_x1 < _x0) { _x0 = _y0 = 0; _x1 = _y1 = -1; }
        _cell = Math.Max(4, (int)(_u / 120));
        _gw = (_x1 - _x0) / _cell + 2; _gh = (_y1 - _y0) / _cell + 2;
        _lean = new float[Math.Max(1, _gw * _gh)];
        _slid = new uint[Math.Max(1, (_x1 - _x0 + 1) * (_y1 - _y0 + 1))];

        // The tufts' pages, each trimmed to the pixels it actually uses. Half the designs are shorter tufts.
        _pages = new Flipbook.Page[Designs, Flipbook.Poses];
        int pw = (int)MathF.Ceiling(h * 2.2f), ph = (int)MathF.Ceiling(h * 1.7f);
        int ax = (int)(h * 0.85f), ay = (int)(h * 0.95f);
        for (int d = 0; d < Designs; d++)
        {
            var design = Flipbook.Tuft(h * (d < Designs / 2 ? 0.72f : 1f), rng);
            for (int p = 0; p < Flipbook.Poses; p++)
            {
                float lean = Flipbook.LeanMin + (Flipbook.LeanMax - Flipbook.LeanMin) * p / (Flipbook.Poses - 1);
                _pages[d, p] = Flipbook.Trim(pw, ph, ax, ay, g => Flipbook.PaintClump(g, design, lean, ax, ay, 48));
            }
        }

        // Plant the tufts: random spots on the lawn (not sand, stones or
        // rocks, and not right against them), each at least "space" from
        // every tuft already planted, so they scatter the way plants do
        // rather than standing in rows. A coarse grid of "taken" cells
        // makes the spacing test quick.
        float space = _u * 0.017f;
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
        int bandRows = ph + 2;
        int bands = pond.Height / bandRows + 1;
        _bands = Enumerable.Range(0, bands).Select(b => tufts.Where(t => t.Y / bandRows == b).OrderBy(t => t.Y).ToArray()).ToArray();
    }

    private bool Lawn(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < _pond.Width && iy < _pond.Height && _pond.Grassy[iy * _pond.Width + ix];
    }

    public void Update(float dt) => _time += dt;

    public void Draw(FrameBuffer fb)
    {
        if (_x1 < _x0) return;
        float t = (float)(_time % 100000.0);
        float gust = Wind.Gust(t);

        // The wind on a coarse grid (a sine per pixel would cost more than the drawing).
        for (int gy = 0; gy < _gh; gy++)
            for (int gx = 0; gx < _gw; gx++)
                _lean[gy * _gw + gx] = Wind.LeanAt(_x0 + gx * _cell, _y0 + gy * _cell, t, gust, _u, 0);

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
                    float lean = Wind.LeanAt(tuft.X, tuft.Y, t, gust, _u, tuft.Jitter) * tuft.Stiff;
                    Flipbook.Page pg = _pages[tuft.Design, Flipbook.PageOf(lean)];
                    pg.Sprite.Draw(fb, tuft.X - pg.Ax, tuft.Y - pg.Ay);
                }
            });
    }

    /// <summary>
    /// Lays the carpet over the lawn, each pixel taken from a little UPWIND
    /// of itself (so the blades appear pushed downwind) and brightened by the
    /// lean. Rows are shared out across the processor's cores, and only every
    /// other row is worked out afresh each frame (odd rows one frame, even
    /// rows the next); the rest are copied from the last time. The wind moves
    /// the carpet well under a pixel a frame, so a row a frame old is the
    /// same picture, at half the cost. The ground under the carpet never
    /// changes (the grass is laid first, straight onto the backdrop), so the
    /// finished pixels can be kept.
    /// </summary>
    private void SlideCarpet(FrameBuffer fb)
    {
        int w = fb.Width, boxW = _x1 - _x0 + 1;
        uint[] px = fb.Pixels;
        int parity = _frame & 1;
        bool first = _frame < 2;
        _frame++;
        Parallel.For(_y0, _y1 + 1, y =>
        {
            int keepRow = (y - _y0) * boxW;
            if (!first && (y & 1) != parity)
            {
                Array.Copy(_slid, keepRow, px, y * w + _x0, boxW);
                return;
            }
            Array.Copy(px, y * w + _x0, _slid, keepRow, boxW);        // the row as it is (ground, sand, stones): overwritten where grass lands
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
                float sx = x - lean * _shift * Wind.X, sy = y - lean * _shift * Wind.Y * Ripples.Squash;
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
                px[i] = _slid[keepRow + x - _x0] = (uint)(Math.Min(255, rr) << 16 | Math.Min(255, gg) << 8 | Math.Min(255, bb));
            }
        });
    }
}

/// <summary>
/// A clump of cattails standing in the shallows near the bank: stiff stems
/// with velvety brown heads and long leaves between them, swaying in the
/// same wind as the lawn (stiffly: a cattail stem barely bends; its leaves
/// bend more). One flip-book of 24 pages, stamped once a frame, drawn only
/// where no branch is in front. Their long shadows fall on the water.
/// </summary>
internal sealed class Cattails
{
    private readonly Pond _pond;
    private readonly Flipbook.Page[] _pages = new Flipbook.Page[Flipbook.Poses];
    private readonly int _x, _y;
    private readonly float _jitter;
    private double _time;

    public Cattails(Pond pond, Random rng)
    {
        _pond = pond;
        float u = pond.U, h = u * 0.24f;                        // the tallest stem, before foreshortening
        _x = (int)pond.CattailSpot.X; _y = (int)pond.CattailSpot.Y;
        _jitter = ((float)rng.NextDouble() - 0.5f) * 0.6f;
        var clump = Flipbook.CattailClump(h, u * 0.036f, rng);
        int pw = (int)(h * 1.6f), ph = (int)(h * 1.6f);
        int ax = pw / 2, ay = (int)(h * 1.0f);
        for (int p = 0; p < Flipbook.Poses; p++)
        {
            float lean = Flipbook.LeanMin + (Flipbook.LeanMax - Flipbook.LeanMin) * p / (Flipbook.Poses - 1);
            _pages[p] = Flipbook.Trim(pw, ph, ax, ay, g => Flipbook.PaintClump(g, clump, lean, ax, ay, 30));
        }
    }

    public void Update(float dt) => _time += dt;

    public void Draw(FrameBuffer fb)
    {
        float t = (float)(_time % 100000.0);
        float lean = Wind.LeanAt(_x, _y, t, Wind.Gust(t), _pond.U, _jitter) * 0.7f;
        Flipbook.Page pg = _pages[Flipbook.PageOf(lean)];
        pg.Sprite.Draw(fb, _x - pg.Ax, _y - pg.Ay, 1f, _pond.Open);
    }
}
