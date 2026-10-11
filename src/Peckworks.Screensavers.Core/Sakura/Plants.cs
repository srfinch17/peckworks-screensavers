using System.Buffers;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core.Sakura;

/// <summary>
/// How a scene seen from ABOVE turns height into screen position, shared by
/// everything that stands, flies or falls in it, so heights and shadows
/// agree across plants, petals and dragonflies.
///
/// We look down at the ground from above and a little in front. A circle on
/// the ground therefore looks a little flatter than round ("Squash": screen
/// y per ground y). A thing HIGH above a spot shows a little further UP the
/// screen than the spot itself ("Lift", screen pixels per pixel of height),
/// and its shadow, cast by a sun up and to the left, falls down and to the
/// right of the spot ("ShadowX", "ShadowY", per pixel of height).
/// </summary>
public readonly record struct Overhead(float Squash, float Lift, float ShadowX, float ShadowY);

/// <summary>
/// The wind over a scene, as one number at any spot: the LEAN, 0 for a
/// plant standing upright to about 1 for one pushed flat downwind. ONE
/// clock, shared by every plant and the petals, so the whole scene gusts
/// together: the lawn, the tufts in it, the cattails, the reeds and the
/// falling petals all read the same wind at the same moment.
///
/// FEYNMAN VERSION: watch a lawn from a window above it. A gust is not
/// everywhere at once; it runs ACROSS the grass as a travelling wave, and
/// each blade leans as the wave reaches it and springs back as it passes.
/// Here that is a slow swell that comes and goes over half a minute, times
/// two waves running downwind, a long one and a short one, with the long
/// one's crests sharpened so the field is mostly calm with a band of bent
/// grass rolling through it. Two plants at the same spot lean the same;
/// two plants a little way apart lean a moment apart, because the wave
/// reaches one first. That (and nothing random per plant) is what makes a
/// field move as ONE field rather than as things twitching on their own.
/// </summary>
public sealed class Wind
{
    /// <summary>Which way it blows, on the ground: a unit vector.</summary>
    public readonly float X, Y;

    private readonly float _u, _squash;
    // A screensaver runs for days. Each of the four rhythms keeps its own
    // PHASE (an angle, in radians) as a double, wrapped at a full turn every
    // frame, so no number ever grows large and no sine ever loses precision.
    // (One shared clock wrapped at 100000 s went coarse after 14 hours and
    // snapped every plant on the wrap, once a day.)
    private double _swellA, _swellB;        // the two slow swell sines
    private double _long, _short;           // how far the long and the short travelling waves have run, in turns
    private float _swell, _gust, _longF, _shortF;

    /// <param name="angle">Direction in radians, 0 = to the right, positive = down the screen (toward us).</param>
    /// <param name="u">The scene's size unit (see the repo rules): wave lengths scale with it.</param>
    /// <param name="view">The overhead view: its Squash says how much flatter the screen's y is than the ground's.</param>
    public Wind(float angle, float u, Overhead view)
    {
        X = MathF.Cos(angle);
        Y = MathF.Sin(angle);
        _u = u;
        _squash = view.Squash;
        Update(0);
    }

    /// <summary>How strong the breeze is right now, 0 (a lull) to 1 (a gust), rising and dying over half a minute or so.</summary>
    public float Swell => _swell;

    /// <summary>The swell as a lean multiplier: 0.25 (a breath) to 1 (a gust).</summary>
    public float Gust => _gust;

    /// <summary>Once a frame, before anything asks the wind.</summary>
    public void Update(float dt)
    {
        _swellA = (_swellA + dt * 0.21) % Math.Tau;
        _swellB = (_swellB + dt * 0.073) % Math.Tau;
        _long = (_long + dt * 0.6) % 1.0;
        _short = (_short + dt * 1.5) % 1.0;
        _longF = (float)_long; _shortF = (float)_short;
        _swell = (float)((0.5 + 0.5 * Math.Sin(_swellA)) * (0.6 + 0.4 * Math.Sin(_swellB + 1)));
        _gust = 0.25f + 0.75f * _swell;
    }

    /// <summary>The lean at screen point (x, y) right now.</summary>
    public float LeanAt(float x, float y)
    {
        // How far along the wind this spot lies, in u, on the ground (the screen's y is squashed).
        float along = (x * X + y / _squash * Y) / _u;
        float w1 = MathF.Sin(MathF.Tau * (along / 0.7f - _longF));
        float crest = 0.5f + 0.5f * w1; crest *= crest;                   // sharpened: calm troughs, a narrow band of bent grass
        float w2 = MathF.Sin(MathF.Tau * (along / 0.3f - _shortF) + 2.1f);
        return 0.08f + _gust * (0.15f + 0.75f * crest + 0.12f * w2);
    }
}

/// <summary>
/// A plant drawn as a flip-book of pages, one per amount of lean: a tuft of
/// grass, a clump of cattails, a fan of iris leaves. Each page is painted
/// once; every frame the plant stamps the page nearest its lean.
///
/// A blade is worked out in three dimensions: the bend angle grows from root
/// to tip (its own splay in its own direction, plus the wind's push downwind),
/// so each short step of the blade goes a little less UP and a little more
/// ALONG the ground. Then it is projected the way everything else in an
/// overhead scene is (Overhead): height lifts a point up the screen, and
/// throws its shadow down and to the right. A cattail is a stiff blade with
/// a brown head near its top and a spike above that.
/// </summary>
public static class Flipbook
{
    public const int Poses = 24;                             // 6 pages snapped; 24 read as fluid
    public const float LeanMin = 0f, LeanMax = 1.1f;         // the flip-book's range: upright to flat downwind (the wind never pushes a plant upwind)

    /// <summary>Which page shows a given lean.</summary>
    public static int PageOf(float lean) => Math.Clamp((int)MathF.Round((lean - LeanMin) / (LeanMax - LeanMin) * (Poses - 1)), 0, Poses - 1);

    /// <summary>The lean a given page shows.</summary>
    public static float LeanOf(int page) => LeanMin + (LeanMax - LeanMin) * page / (Poses - 1);

    public readonly struct Page(Sprite sprite, int ax, int ay)
    {
        public readonly Sprite Sprite = sprite;
        public readonly int Ax = ax, Ay = ay;                   // where the root sits inside this page
    }

    /// <summary>
    /// One blade: its height, which way it splays, how far, how much the wind
    /// bends it, its width and colours. Cattail = true for a stem with a head.
    /// Taper: 1 = a grass blade, narrowing steadily from root to tip; 2 = a
    /// sword leaf, keeping its width and narrowing only near the tip.
    /// </summary>
    public readonly record struct Blade(float H, float AzX, float AzY, float Droop, float Flex, float Wid, Color Root, Color Tip, bool Cattail, float Taper = 1f);

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
    public static (Blade Blade, PointF Root)[] CattailClump(float h, float spread, float squash, Random rng)
    {
        int stems = 8 + rng.Next(5), leaves = 10 + rng.Next(6);
        var all = new (Blade, PointF)[stems + leaves];
        for (int i = 0; i < all.Length; i++)
        {
            bool stem = i < stems;
            double a = rng.NextDouble() * Math.Tau, d = Math.Sqrt(rng.NextDouble()) * spread;
            var root = new PointF((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d * squash));
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
    /// A clump of iris leaves: 8 to 12 sword blades fanning out from one
    /// root, the fan centred on "toward" (radians on the ground: at a pond's
    /// edge, out over the water), stiff, so the wind sways them less than
    /// grass. The blades are dealt EVENLY across the fan (a random spread
    /// came out as a narrow bundle one time in three), each arching well
    /// over, as a sword leaf does: from above an upright leaf is only a
    /// line, and one arching straight away from the viewer foreshortens to
    /// a sliver, so it is the ones to the sides that show the fan.
    /// </summary>
    public static (Blade Blade, PointF Root)[] IrisClump(float h, float toward, Random rng)
    {
        var blades = new (Blade, PointF)[8 + rng.Next(5)];
        const float fan = 3.2f;                                 // radians, edge to edge
        for (int i = 0; i < blades.Length; i++)
        {
            float az = toward - fan / 2 + fan * (i + 0.5f) / blades.Length + ((float)rng.NextDouble() - 0.5f) * 0.3f;
            Color leaf = Color.FromArgb(54 + rng.Next(40), 104 + rng.Next(40), 50 + rng.Next(20));
            blades[i] = (new Blade(
                h * (0.65f + 0.35f * (float)rng.NextDouble()), MathF.Cos(az), MathF.Sin(az),
                1.1f + 0.5f * (float)rng.NextDouble(), 0.35f + 0.25f * (float)rng.NextDouble(),
                h * 0.14f,
                Brushwork.Mix(leaf, Color.Black, 0.25f),
                Brushwork.Mix(leaf, Color.FromArgb(190, 210, 130), 0.25f), false, Taper: 2f), PointF.Empty);
        }
        return blades;
    }

    /// <summary>
    /// Paints a clump at one amount of lean: every blade's shadow first,
    /// then the blades, each followed root to tip in short steps, bent a
    /// little further over at each. A cattail stem is painted in three
    /// parts: the stem, the brown head over the top fifth, and a pale
    /// spike above it.
    /// </summary>
    public static void PaintClump(Graphics g, (Blade Blade, PointF Root)[] clump, float lean, int ax, int ay, int shadowAlpha, Wind wind, Overhead view)
    {
        using var shadow = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 20, 8));
        foreach (bool shadows in (ReadOnlySpan<bool>)[true, false])
            foreach (var (b, root) in clump)
            {
                int steps = b.Cattail ? 12 : 8;
                var top = new PointF[steps + 1];
                var ground = new PointF[steps + 1];
                // The bend: its own splay in its own direction, plus the wind's push downwind.
                float bx = b.Droop * b.AzX + lean * b.Flex * wind.X, by = b.Droop * b.AzY + lean * b.Flex * wind.Y;
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
                    top[s] = new PointF(rx + gx, ry + gy * view.Squash - z * view.Lift);
                    ground[s] = new PointF(rx + gx + z * view.ShadowX, ry + (gy + z * view.ShadowY) * view.Squash);
                }
                PointF[] line = shadows ? ground : top;
                if (!b.Cattail)
                {
                    // A strip tapering from the root's width to a point at the tip
                    // (steadily for grass; a sword leaf keeps its width until near the tip).
                    PointF[] poly = Strip(line, s => b.Wid * (1 - 0.9f * MathF.Pow(s, b.Taper)) * 0.5f);
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
    /// All 24 pages of one clump, each trimmed to the pixels it uses. The
    /// sheet each is painted on is sized from the clump itself: the tallest
    /// blade lifted up the screen, or laid flat along the ground any way,
    /// with its shadow thrown down and to the right, plus the spread of the
    /// roots and a margin for a cattail's wide head. (Sizes guessed by the
    /// caller once cut the cattails' long shadows off with a straight line.)
    /// </summary>
    public static Page[] Pages((Blade Blade, PointF Root)[] clump, int shadowAlpha, Wind wind, Overhead view)
    {
        float tallest = 0, widest = 0, spreadX = 0, spreadY = 0;
        foreach (var (b, root) in clump)
        {
            tallest = MathF.Max(tallest, b.H);
            widest = MathF.Max(widest, b.Cattail ? b.Wid * 3.2f : b.Wid);
            spreadX = MathF.Max(spreadX, MathF.Abs(root.X)); spreadY = MathF.Max(spreadY, MathF.Abs(root.Y));
        }
        float margin = widest + 4;
        int left = (int)MathF.Ceiling(tallest + spreadX + margin), right = (int)MathF.Ceiling(tallest * (1 + view.ShadowX) + spreadX + margin);
        // Up the screen a blade can reach its height lifted PLUS its ground travel away from the viewer (arching up-screen), at right angles to each other.
        float reachUp = tallest * MathF.Sqrt(view.Lift * view.Lift + view.Squash * view.Squash);
        int top = (int)MathF.Ceiling(reachUp + spreadY + margin), bottom = (int)MathF.Ceiling(tallest * (1 + view.ShadowY) * view.Squash + spreadY + margin);
        int pw = left + right, ph = top + bottom, ax = left, ay = top;
        var pages = new Page[Poses];
        for (int p = 0; p < Poses; p++)
        {
            float lean = LeanOf(p);
            pages[p] = Trim(pw, ph, ax, ay, g => PaintClump(g, clump, lean, ax, ay, shadowAlpha, wind, view));
        }
        return pages;
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
/// A few plants that each stand in one place and sway with the wind: a
/// clump of cattails in the shallows, the iris fans along a bank. Each is
/// a flip-book (Flipbook) stamped once a frame at the page nearest its
/// lean, drawn only "where" the stencil allows (so a branch or a rock in
/// front of it stays in front), and darkened by the scene's "sun" at its
/// root (0 to 255 per pixel), so a clump under a branch's shadow stands in
/// that shadow instead of glowing through it.
///
/// Built for a scene seen from ABOVE (the Overhead projection); a side-on
/// scene wants a different projection, not this class.
/// </summary>
public sealed class Clumps
{
    private readonly record struct Clump(int X, int Y, float Stiff, Flipbook.Page[] Pages);

    private readonly List<Clump> _clumps = new();
    private readonly Wind _wind;
    private readonly Overhead _view;
    private readonly bool[]? _where;
    private readonly byte[]? _sun;
    private readonly int _w;

    /// <param name="where">Stencil: draw only where true (null = everywhere).</param>
    /// <param name="sun">Light per pixel, 0 to 255 (null = full sun everywhere); a clump takes the value at its root.</param>
    public Clumps(Wind wind, Overhead view, int width, bool[]? where, byte[]? sun)
    {
        _wind = wind;
        _view = view;
        _w = width;
        _where = where;
        _sun = sun;
    }

    /// <summary>
    /// Plants a clump with its root at "root". "Stiff" scales the wind's lean
    /// for it (1 = grass, 0.7 = cattails, 0.45 = iris). The pages are sized
    /// from the clump (see Flipbook.Pages).
    /// </summary>
    public void Add(PointF root, (Flipbook.Blade Blade, PointF Root)[] clump, float stiff, int shadowAlpha)
        => _clumps.Add(new Clump((int)root.X, (int)root.Y, stiff, Flipbook.Pages(clump, shadowAlpha, _wind, _view)));

    public void Draw(FrameBuffer fb)
    {
        foreach (Clump c in _clumps)
        {
            Flipbook.Page pg = c.Pages[Flipbook.PageOf(_wind.LeanAt(c.X, c.Y) * c.Stiff)];
            int shade = _sun == null ? 256 : _sun[Math.Clamp(c.Y, 0, fb.Height - 1) * _w + Math.Clamp(c.X, 0, _w - 1)] + 1;
            pg.Sprite.Draw(fb, c.X - pg.Ax, c.Y - pg.Ay, 1f, _where, shade: shade);
        }
    }
}

/// <summary>
/// A lawn in the wind. Two things move:
///
///   1. The CARPET: the thousands of blades the painter drew on a clear
///      layer of their own. Each frame the carpet is laid back over the
///      ground SHIFTED a few pixels downwind by however hard the wind leans
///      the grass at that spot, and a little brighter where it leans hardest
///      (bent blades show more of their pale tips to the sky). So a gust
///      runs across the whole lawn as a band of blades leaning and
///      lightening, the way wind shows on a field.
///   2. The TUFTS: taller clumps standing in the carpet, each a flip-book
///      (Flipbook) of 24 pages from "springing back a little upwind" to
///      "flat downwind". Every frame each tuft stamps the page nearest its lean.
///
/// Both read the one shared Wind, so the tufts lean exactly when the carpet
/// around them does.
///
/// The painter supplies the carpet (0xAARRGGBB per pixel, clear where no
/// blade is) and the "grassy" mask (where the lawn is: not sand, stones or
/// rocks); this class owns everything that moves. Two more inputs keep the
/// lawn honest about what is in front of it and above it: a stencil
/// ("where": false under a branch, so the grass never paints over it) and
/// the light per pixel ("sun": the branch's dappled shadow falls on the
/// moving grass as it does on the ground).
///
/// RULE FOR THE CALLER: draw the lawn FIRST each frame, straight after the
/// backdrop is restored and before anything else is painted inside the
/// lawn's box. Half the box's rows are kept from the frame before (see
/// SlideCarpet), so anything drawn there earlier in the frame would freeze
/// on alternate rows. Built for a scene seen from ABOVE (the Overhead
/// projection), with the carpet's blades painted leaning downwind.
/// </summary>
public sealed class Lawn
{
    private const int Designs = 10;

    private readonly struct Tuft(int x, int y, byte design, float stiff)
    {
        public readonly int X = x, Y = y;                       // the root, screen pixels
        public readonly byte Design = design;
        public readonly float Stiff = stiff;                    // how far it leans for a given wind
    }

    private readonly int _w, _h;
    private readonly bool[] _grassy;
    private readonly Wind _wind;
    private readonly Overhead _view;
    private readonly Flipbook.Page[,] _pages;                   // [design, pose]
    private readonly Tuft[][] _bands;                           // tufts by horizontal band, each band far to near (see Draw)
    // The carpet layer, premultiplied (red, green and blue already scaled by
    // alpha) and PACKED two channels to a number with a byte of space between
    // them: _rb holds red in the high half and blue in the low, _ag alpha and
    // green. Blending four neighbours then costs one multiply per PAIR of
    // channels instead of one per channel (see SlideCarpet).
    private readonly uint[] _rb, _ag;
    private readonly int _x0, _y0, _x1, _y1;                    // the lawn's box on screen
    private readonly float _shift;                              // how far (pixels) the carpet slides downwind at lean 1
    private readonly int _cell, _gw, _gh;                       // the wind grid: lean worked out every "cell" pixels, blended between
    private readonly float[] _lean;
    private readonly uint[] _slid;                              // the lawn box as last laid: half its rows are redone each frame
    private int _frame;

    private readonly bool[]? _where;
    private readonly byte[]? _sun;

    /// <param name="where">Stencil: the lawn shows only where true (null = everywhere).</param>
    /// <param name="sun">Light per pixel, 0 to 255 (null = full sun everywhere).</param>
    public Lawn(int width, int height, uint[] carpet, bool[] grassy, float u, Wind wind, Overhead view, Random rng, bool[]? where = null, byte[]? sun = null)
    {
        _w = width; _h = height;
        _grassy = grassy;
        _wind = wind;
        _view = view;
        _where = where;
        _sun = sun;
        float h = u * 0.05f;                                    // a tall blade, screen pixels
        _shift = u * 0.012f;

        _rb = new uint[carpet.Length]; _ag = new uint[carpet.Length];
        for (int i = 0; i < carpet.Length; i++)
        {
            uint c = carpet[i];
            uint a = c >> 24;
            uint r = ((c >> 16) & 0xFF) * a / 255, g = ((c >> 8) & 0xFF) * a / 255, b = (c & 0xFF) * a / 255;
            _rb[i] = r << 16 | b;
            _ag[i] = a << 16 | g;
        }
        // The lawn's box: where any grassy pixel is.
        _x0 = width; _y0 = height; _x1 = 0; _y1 = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (grassy[y * width + x]) { _x0 = Math.Min(_x0, x); _x1 = Math.Max(_x1, x); _y0 = Math.Min(_y0, y); _y1 = Math.Max(_y1, y); }
        if (_x1 < _x0) { _x0 = _y0 = 0; _x1 = _y1 = -1; }
        _cell = Math.Max(4, (int)(u / 120));
        _gw = (_x1 - _x0) / _cell + 2; _gh = (_y1 - _y0) / _cell + 2;
        _lean = new float[Math.Max(1, _gw * _gh)];
        _slid = new uint[Math.Max(1, (_x1 - _x0 + 1) * (_y1 - _y0 + 1))];

        // The tufts' pages, each trimmed to the pixels it actually uses. Half the designs are shorter tufts.
        _pages = new Flipbook.Page[Designs, Flipbook.Poses];
        int ph = 1;                                             // the tallest page, for the bands below
        for (int d = 0; d < Designs; d++)
        {
            Flipbook.Page[] pages = Flipbook.Pages(Flipbook.Tuft(h * (d < Designs / 2 ? 0.72f : 1f), rng), 48, wind, view);
            for (int p = 0; p < Flipbook.Poses; p++) { _pages[d, p] = pages[p]; ph = Math.Max(ph, pages[p].Sprite.Height); }
        }

        // Plant the tufts: random spots on the lawn (not sand, stones or
        // rocks, and not right against them), each at least "space" from
        // every tuft already planted, so they scatter the way plants do
        // rather than standing in rows. A coarse grid of "taken" cells
        // makes the spacing test quick.
        float space = u * 0.017f;
        float keep = h * 0.25f;
        int occW = (int)(width / space) + 2, occH = (int)(height / space) + 2;
        var taken = new List<PointF>[occW * occH];
        var tufts = new List<Tuft>();
        int want = (int)((_x1 - _x0 + 1) * (_y1 - _y0 + 1) / (space * space) * 0.55f);
        for (int tries = 0; tries < want * 6 && tufts.Count < want; tries++)
        {
            int x = _x0 + rng.Next(_x1 - _x0 + 1), y = _y0 + rng.Next(_y1 - _y0 + 1);
            if (!Grassy(x, y) || !Grassy(x - keep, y) || !Grassy(x + keep, y) || !Grassy(x, y - keep) || !Grassy(x, y + keep)) continue;
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
            tufts.Add(new Tuft(x, y, (byte)rng.Next(Designs), 0.7f + 0.6f * (float)rng.NextDouble()));
        }
        // Bands taller than a page: tufts in two bands that are not next to
        // each other can never touch, so those bands can be drawn at the same
        // time on different cores. Even bands go first, then odd bands.
        int bandRows = ph + 2;
        int bands = height / bandRows + 1;
        _bands = Enumerable.Range(0, bands).Select(b => tufts.Where(t => t.Y / bandRows == b).OrderBy(t => t.Y).ToArray()).ToArray();
    }

    private bool Grassy(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < _w && iy < _h && _grassy[iy * _w + ix];
    }

    public void Draw(FrameBuffer fb)
    {
        if (_x1 < _x0) return;

        // The wind on a coarse grid (a sine per pixel would cost more than the drawing).
        for (int gy = 0; gy < _gh; gy++)
            for (int gx = 0; gx < _gw; gx++)
                _lean[gy * _gw + gx] = _wind.LeanAt(_x0 + gx * _cell, _y0 + gy * _cell);

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
                    Flipbook.Page pg = _pages[tuft.Design, Flipbook.PageOf(_wind.LeanAt(tuft.X, tuft.Y) * tuft.Stiff)];
                    int shade = _sun == null ? 256 : _sun[tuft.Y * _w + tuft.X] + 1;
                    pg.Sprite.Draw(fb, tuft.X - pg.Ax, tuft.Y - pg.Ay, 1f, _where, shade: shade);
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
    ///
    /// The arithmetic is all whole numbers. The spot a pixel's grass comes
    /// from falls between four carpet pixels; how much of each to take is
    /// worked out in sixteenths (a sixteenth of a pixel is far below what the
    /// eye can see), so the four weights are whole numbers adding up to 256,
    /// and two channels are blended in one multiply thanks to the packing
    /// (see the fields). The lean itself is only worked out at the grid
    /// corners and walked along each row in equal steps between them.
    /// </summary>
    private void SlideCarpet(FrameBuffer fb)
    {
        int w = fb.Width, boxW = _x1 - _x0 + 1;
        uint[] px = fb.Pixels;
        int parity = _frame & 1;
        bool first = _frame < 2;
        _frame++;
        float shiftX = _shift * _wind.X, shiftY = _shift * _wind.Y * _view.Squash;
        Parallel.For(_y0, _y1 + 1, y =>
        {
            int keepRow = (y - _y0) * boxW;
            if (!first && (y & 1) != parity)
            {
                Array.Copy(_slid, keepRow, px, y * w + _x0, boxW);
                return;
            }
            Array.Copy(px, y * w + _x0, _slid, keepRow, boxW);        // the row as it is (ground, sand, stones): overwritten where grass lands
            // This row's lean at each grid column: blended between the grid rows above and below.
            float gy = (y - _y0) / (float)_cell;
            int iy = Math.Min(_gh - 2, (int)gy); float fy = gy - iy;
            float[] rowLean = ArrayPool<float>.Shared.Rent(_gw);
            for (int gx = 0; gx < _gw; gx++) rowLean[gx] = _lean[iy * _gw + gx] * (1 - fy) + _lean[(iy + 1) * _gw + gx] * fy;
            int ix = 0, left = _cell;
            float lean = rowLean[0], dl = (rowLean[1] - rowLean[0]) / _cell;
            for (int x = _x0; x <= _x1; x++, lean += dl, left--)
            {
                if (left == 0)
                {
                    ix++; left = _cell;
                    lean = rowLean[ix]; dl = (rowLean[Math.Min(ix + 1, _gw - 1)] - lean) / _cell;
                }
                int i = y * w + x;
                if (!_grassy[i] || (_where != null && !_where[i])) continue;   // not lawn, or under a branch
                // Where this pixel's grass came from: upwind by the lean.
                float sx = x - lean * shiftX, sy = y - lean * shiftY;
                int sx0 = (int)MathF.Floor(sx), sy0 = (int)MathF.Floor(sy);
                if (sx0 < 0 || sy0 < 0 || sx0 + 1 >= w || sy0 + 1 >= fb.Height) continue;
                int tx = (int)((sx - sx0) * 16), ty = (int)((sy - sy0) * 16);
                uint w11 = (uint)(tx * ty), w10 = (uint)(tx * (16 - ty)), w01 = (uint)((16 - tx) * ty), w00 = (uint)((16 - tx) * (16 - ty));
                int j = sy0 * w + sx0;
                uint ag = ((_ag[j] * w00 + _ag[j + 1] * w10 + _ag[j + w] * w01 + _ag[j + w + 1] * w11) >> 8) & 0x00FF00FF;
                int a = (int)(ag >> 16);
                if (a < 2) continue;
                uint rb = ((_rb[j] * w00 + _rb[j + 1] * w10 + _rb[j + w] * w01 + _rb[j + w + 1] * w11) >> 8) & 0x00FF00FF;
                // Brighter where it leans: bent blades show their pale tips. In 128ths. Darker in a branch's shadow.
                int bright = (int)((1 + 0.35f * (lean - 0.25f)) * 128);
                if (_sun != null) bright = bright * (_sun[i] + 1) >> 8;
                int r = (int)(rb >> 16) * bright >> 7, g = (int)(ag & 0xFF) * bright >> 7, b = (int)(rb & 0xFF) * bright >> 7;
                uint bg = px[i];
                int keep = 256 - a;
                int rr = ((int)((bg >> 16) & 0xFF) * keep >> 8) + r, gg = ((int)((bg >> 8) & 0xFF) * keep >> 8) + g, bb = ((int)(bg & 0xFF) * keep >> 8) + b;
                px[i] = _slid[keepRow + x - _x0] = (uint)(Math.Min(255, rr) << 16 | Math.Min(255, gg) << 8 | Math.Min(255, bb));
            }
            ArrayPool<float>.Shared.Return(rowLean);
        });
    }
}
