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
/// a broad soft wave rolling downwind (the gust front), times a slowly
/// drifting pattern of PATCHES across and along the wind (a real gust is
/// not a bar the width of the field; it is a blob, stronger here than
/// there, and the next one is a different blob), plus a faint quick ripple.
/// Two plants at the same spot lean the same; two plants a little way apart
/// lean a moment apart, because the wave reaches one first. That (and
/// nothing random per plant) is what makes a field move as ONE field
/// rather than as things twitching on their own.
///
/// The first version was a sharpened bar travelling at 0.4u a second: on
/// screen it read as a ruler sliding over the lawn, and the tufts flipping
/// their pages hid it anyway. Softer, slower and patchy reads as weather.
/// </summary>
public sealed class Wind
{
    /// <summary>Which way it blows, on the ground: a unit vector.</summary>
    public readonly float X, Y;

    private readonly float _u, _squash;
    // A screensaver runs for days. Each of the rhythms keeps its own PHASE
    // (an angle, in radians, or a fraction of a turn) as a double, wrapped
    // every frame, so no number ever grows large and no sine ever loses
    // precision. (One shared clock wrapped at 100000 s went coarse after 14
    // hours and snapped every plant on the wrap, once a day.)
    private double _swellA, _swellB;        // the two slow swell sines
    private double _long, _short;           // how far the long and the short travelling waves have run, in turns
    private double _patchA, _patchB;        // how far the gust patches have drifted, across and along the wind, in turns
    private float _swell, _gust, _longF, _shortF, _patchAF, _patchBF;

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
        _swellA = (_swellA + dt * 0.17) % Math.Tau;
        _swellB = (_swellB + dt * 0.061) % Math.Tau;
        _long = (_long + dt * 0.28) % 1.0;                 // the gust front: 1.1u long, so it crosses the lawn in a few seconds
        _short = (_short + dt * 0.9) % 1.0;
        _patchA = (_patchA + dt * 0.11) % 1.0;
        _patchB = (_patchB + dt * 0.07) % 1.0;
        _longF = (float)_long; _shortF = (float)_short; _patchAF = (float)_patchA; _patchBF = (float)_patchB;
        _swell = (float)((0.5 + 0.5 * Math.Sin(_swellA)) * (0.6 + 0.4 * Math.Sin(_swellB + 1)));
        _gust = 0.2f + 0.8f * _swell;
    }

    /// <summary>The lean at screen point (x, y) right now.</summary>
    public float LeanAt(float x, float y)
    {
        // How far along and across the wind this spot lies, in u, on the ground (the screen's y is squashed).
        float gy = y / _squash;
        float along = (x * X + gy * Y) / _u, across = (gy * X - x * Y) / _u;
        // The gust front: a broad soft hump (a smooth step up and down, no sharp crest) rolling downwind.
        float w1 = 0.5f + 0.5f * MathF.Sin(MathF.Tau * (along / 1.1f - _longF));
        float crest = w1 * w1 * (3 - 2 * w1);
        // The patches: a slow checker of strong and weak, drifting sideways and downwind, so each gust is its own shape.
        float patch = 0.5f + 0.5f * MathF.Sin(MathF.Tau * (across / 0.55f + _patchAF) + 1.3f) * MathF.Sin(MathF.Tau * (along / 0.8f - _patchBF));
        // A faint quick ripple over it all.
        float w2 = MathF.Sin(MathF.Tau * (along / 0.33f - _shortF) + 2.1f);
        return 0.05f + _gust * (0.1f + 0.65f * crest * (0.4f + 0.6f * patch) + 0.08f * w2);
    }
}

/// <summary>
/// Smooth random blotches, for anything that should vary gently from place
/// to place: where a lawn is lush or worn, where clover has spread, where
/// the flowers are. "Value noise": a random number is pinned to every
/// corner of a grid, and between corners the value is blended smoothly,
/// so neighbours agree and far-apart spots do not. Three sizes of grid are
/// added (big blotches, with smaller ones on top) so the shapes are
/// ragged, not round. The same (x, y, seed) always gives the same answer:
/// a painter and a planter asking about the same spot get the same patch.
/// </summary>
public static class Mottle
{
    private static float Corner(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    /// <summary>One grid of blotches "cell" pixels across: 0 to 1.</summary>
    public static float At(float x, float y, float cell, int seed)
    {
        float gx = x / cell, gy = y / cell;
        int ix = (int)MathF.Floor(gx), iy = (int)MathF.Floor(gy);
        float fx = gx - ix, fy = gy - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float top = Corner(ix, iy, seed) * (1 - fx) + Corner(ix + 1, iy, seed) * fx;
        float bottom = Corner(ix, iy + 1, seed) * (1 - fx) + Corner(ix + 1, iy + 1, seed) * fx;
        return top * (1 - fy) + bottom * fy;
    }

    /// <summary>Three grids added (the biggest "cell" across, then half and a quarter of it, each fainter): 0 to 1, mostly near the middle.</summary>
    public static float Blotch(float x, float y, float cell, int seed)
        => (At(x, y, cell, seed) * 4 + At(x, y, cell * 0.5f, seed + 1) * 2 + At(x, y, cell * 0.25f, seed + 2)) / 7f;
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
    public const int Poses = 48;                             // 6 pages snapped; 24 read as fluid at a glance; 48 so a tall tuft's tip moves under a pixel a page at 1440p (the owner's rule: wind must be smooth)
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

    /// <summary>What sits on top of a blade, if anything.</summary>
    public enum Head
    {
        None,
        Cattail,    // a velvety brown sausage with a pale spike above it
        Seed,       // a grass seed head: a pale, slightly fatter stretch at the top of a thin stem
        Daisy,      // white petals round a yellow eye, seen from above
        Buttercup,  // five glossy yellow petals, small
        Dandelion,  // a yellow pompom
        Clock,      // a dandelion gone to seed: a pale, airy ball
    }

    /// <summary>
    /// One blade: its height, which way it splays, how far, how much the wind
    /// bends it, its width and colours, what sits on top (Head) and how big
    /// that is (HeadSize, pixels: the head's half width).
    /// Taper: 1 = a grass blade, narrowing steadily from root to tip; 2 = a
    /// sword leaf, keeping its width and narrowing only near the tip (painted
    /// with a fold: a lit half, a shaded half and a pale midrib).
    /// </summary>
    public readonly record struct Blade(float H, float AzX, float AzY, float Droop, float Flex, float Wid, Color Root, Color Tip, Head Head = Head.None, float Taper = 1f, float HeadSize = 0f);

    /// <summary>
    /// The kinds of tuft a lawn is made of. Most are plain grass; the rest
    /// give the lawn its variety: dry tawny tufts, taller grass gone to
    /// seed, and the wildflowers, each a few blades with flower stalks
    /// rising out of them.
    /// </summary>
    public enum TuftKind { Grass, Dry, Seeds, Daisies, Buttercups, Dandelions }

    /// <summary>
    /// A tuft: 5 to 10 blades splayed every way, each a little different, all
    /// from one root, in one of a few colour families (a yellower green, a
    /// bluer green, a tawny dry one), and for the flowering kinds two to four
    /// thin stalks with a flower on top. Flower stalks are limber (a high
    /// Flex) so they whip a little more than the blades in a gust.
    /// </summary>
    public static (Blade Blade, PointF Root)[] Tuft(float h, Random rng, TuftKind kind = TuftKind.Grass)
    {
        bool flowers = kind is TuftKind.Daisies or TuftKind.Buttercups or TuftKind.Dandelions;
        int blades = flowers ? 5 + rng.Next(3) : 6 + rng.Next(5);
        int stalks = flowers ? 2 + rng.Next(3) : kind == TuftKind.Seeds ? 2 + rng.Next(2) : 0;
        var all = new (Blade, PointF)[blades + stalks];
        // The colour family: a base green, warmer or cooler, or dry.
        int family = kind == TuftKind.Dry ? 3 : rng.Next(3);
        for (int i = 0; i < blades; i++)
        {
            float az = (float)(rng.NextDouble() * MathF.Tau);
            Color g = family switch
            {
                0 => Color.FromArgb(60 + rng.Next(30), 104 + rng.Next(40), 38 + rng.Next(24)),    // the lawn's own green
                1 => Color.FromArgb(84 + rng.Next(30), 118 + rng.Next(36), 34 + rng.Next(16)),    // yellower, sun-baked
                2 => Color.FromArgb(46 + rng.Next(24), 98 + rng.Next(34), 54 + rng.Next(26)),     // bluer, in damp ground
                _ => Color.FromArgb(132 + rng.Next(30), 118 + rng.Next(24), 64 + rng.Next(20)),   // dry: tawny straw
            };
            float bh = flowers ? h * (0.45f + 0.3f * (float)rng.NextDouble()) : h * (0.55f + 0.45f * (float)rng.NextDouble());
            all[i] = (new Blade(
                bh, MathF.Cos(az), MathF.Sin(az),
                0.25f + 0.45f * (float)rng.NextDouble(), 0.9f + 0.5f * (float)rng.NextDouble(),
                h * (0.08f + 0.04f * (float)rng.NextDouble()),
                Color.FromArgb((int)(g.R * 0.55f), (int)(g.G * 0.6f), (int)(g.B * 0.55f)),
                Color.FromArgb(Math.Min(255, g.R + 90), Math.Min(255, g.G + 70), Math.Min(255, g.B + 40))), PointF.Empty);
        }
        for (int i = 0; i < stalks; i++)
        {
            float az = (float)(rng.NextDouble() * MathF.Tau);
            Head head = kind switch
            {
                TuftKind.Seeds => Head.Seed,
                TuftKind.Daisies => Head.Daisy,
                TuftKind.Buttercups => Head.Buttercup,
                _ => rng.Next(3) == 0 ? Head.Clock : Head.Dandelion,
            };
            float sh = head switch { Head.Seed => h * (1.05f + 0.35f * (float)rng.NextDouble()), Head.Buttercup => h * (0.7f + 0.2f * (float)rng.NextDouble()), _ => h * (0.85f + 0.3f * (float)rng.NextDouble()) };
            float size = head switch { Head.Seed => h * 0.035f, Head.Daisy => h * 0.11f, Head.Buttercup => h * 0.07f, Head.Clock => h * 0.11f, _ => h * 0.085f };
            Color stem = Color.FromArgb(92, 124, 54);
            all[blades + i] = (new Blade(
                sh, MathF.Cos(az), MathF.Sin(az),
                0.08f + 0.22f * (float)rng.NextDouble(), 1.2f + 0.5f * (float)rng.NextDouble(),
                h * (head == Head.Seed ? 0.035f : 0.03f),
                Color.FromArgb((int)(stem.R * 0.6f), (int)(stem.G * 0.65f), (int)(stem.B * 0.6f)),
                Color.FromArgb(Math.Min(255, stem.R + 50), Math.Min(255, stem.G + 40), Math.Min(255, stem.B + 30)), head, HeadSize: size),
                new PointF(((float)rng.NextDouble() - 0.5f) * h * 0.2f, ((float)rng.NextDouble() - 0.5f) * h * 0.1f));
        }
        return all;
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
                Color.FromArgb(Math.Min(255, g.R + 60), Math.Min(255, g.G + 50), Math.Min(255, g.B + 30)),
                stem ? Head.Cattail : Head.None, HeadSize: stem ? h * 0.018f * 1.6f : 0f), root);
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
                Brushwork.Mix(leaf, Color.FromArgb(190, 210, 130), 0.25f), Taper: 2f), PointF.Empty);
        }
        return blades;
    }

    /// <summary>
    /// Paints a clump at one amount of lean: every blade's shadow first,
    /// then the blades, each followed root to tip in short steps, bent a
    /// little further over at each. A cattail stem is painted in three
    /// parts: the stem, the brown head over the top fifth, and a pale
    /// spike above it. A flower stalk is a thin blade with its flower
    /// stamped at the tip, seen from above (a disc of petals round an eye),
    /// and a small round shadow on the ground under it. A sword leaf (Taper
    /// 2) is painted as a lit half and a shaded half either side of a pale
    /// midrib, which is what makes a flat strip read as a leaf with a fold.
    /// </summary>
    public static void PaintClump(Graphics g, (Blade Blade, PointF Root)[] clump, float lean, int ax, int ay, int shadowAlpha, Wind wind, Overhead view)
    {
        using var shadow = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 20, 8));
        foreach (bool shadows in (ReadOnlySpan<bool>)[true, false])
            foreach (var (b, root) in clump)
            {
                int steps = b.Head == Head.Cattail ? 12 : 8;
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
                if (b.Head != Head.Cattail)
                {
                    // A strip tapering from the root's width to a point at the tip
                    // (steadily for grass; a sword leaf keeps its width until near
                    // the tip; a flower stalk keeps most of its width to the top).
                    float endW = b.Head == Head.None ? 0.9f : 0.5f;
                    PointF[] poly = Strip(line, s => b.Wid * (1 - endW * MathF.Pow(s, b.Taper)) * 0.5f);
                    if (shadows)
                    {
                        g.FillPolygon(shadow, poly);
                        if (b.HeadSize > 0) g.FillEllipse(shadow, line[steps].X - b.HeadSize, line[steps].Y - b.HeadSize * view.Squash, b.HeadSize * 2, b.HeadSize * 2 * view.Squash);
                        continue;
                    }
                    if (b.Taper >= 2f)
                    {
                        // The fold: the left half lit, the right half in its own shade, a pale midrib between.
                        int n = line.Length;
                        var left = new PointF[n * 2]; var right = new PointF[n * 2];
                        for (int s = 0; s < n; s++)
                        {
                            left[s] = poly[s]; left[n * 2 - 1 - s] = line[s];
                            right[s] = line[s]; right[n * 2 - 1 - s] = poly[n * 2 - 1 - s];
                        }
                        using (var lit = new LinearGradientBrush(top[0], top[steps], b.Root, b.Tip)) g.FillPolygon(lit, left);
                        using (var dim = new LinearGradientBrush(top[0], top[steps], Brushwork.Mix(b.Root, Color.Black, 0.3f), Brushwork.Mix(b.Tip, Color.Black, 0.22f))) g.FillPolygon(dim, right);
                        using (var rib = new Pen(Color.FromArgb(70, 214, 226, 150), MathF.Max(1f, b.Wid * 0.1f))) g.DrawLines(rib, line[..Math.Max(2, n - 1)]);
                        continue;
                    }
                    using (var brush = new LinearGradientBrush(top[0], top[steps], b.Root, b.Tip)) g.FillPolygon(brush, poly);
                    if (b.Head == Head.Seed)
                    {
                        // The seed head lies ALONG the stem's last stretch (an oat, not a pin head): a pale strip
                        // over the top quarter, fattest in its middle, with a lighter streak down its sunny side.
                        PointF[] headLine = line[(steps - 2)..];
                        PointF[] oat = Strip(headLine, s => b.HeadSize * MathF.Min(1, MathF.Min(s * 3 + 0.3f, (1 - s) * 3 + 0.3f)));
                        using (var ob = new SolidBrush(Color.FromArgb(198, 186, 132))) g.FillPolygon(ob, oat);
                        using (var ol = new Pen(Color.FromArgb(120, 226, 216, 164), MathF.Max(1f, b.HeadSize * 0.5f))) g.DrawLines(ol, headLine);
                    }
                    else if (b.Head != Head.None) Flower(g, b.Head, top[steps], b.HeadSize, view);
                    continue;
                }
                // A cattail: stem (steps 0 to 9), head (9 to 11), spike (11 to 12).
                float headW = b.HeadSize * 2;
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

    /// <summary>
    /// A flower (or a seed head) at the tip of a stalk, seen from above and
    /// squashed like everything on the ground. Sizes are in pixels, so at a
    /// small draw size a daisy is a white dot with a yellow eye and nothing
    /// finer is attempted.
    /// </summary>
    private static void Flower(Graphics g, Head head, PointF at, float r, Overhead view)
    {
        float sq = view.Squash;
        void Disc(Color c, float cx, float cy, float rad)
        {
            using var b = new SolidBrush(c);
            g.FillEllipse(b, cx - rad, cy - rad * sq, rad * 2, rad * 2 * sq);
        }
        switch (head)
        {
            case Head.Seed:
                break;                                              // painted along the stem by PaintClump, not as a disc
            case Head.Daisy:
                // Nine petals round a yellow eye; petals as small discs a little out from the centre (a scalloped rim from above).
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * MathF.Tau;
                    Disc(Color.FromArgb(244, 244, 236), at.X + MathF.Cos(a) * r * 0.55f, at.Y + MathF.Sin(a) * r * 0.55f * sq, r * 0.42f);
                }
                Disc(Color.FromArgb(236, 196, 52), at.X, at.Y, r * 0.38f);
                Disc(Color.FromArgb(214, 160, 36), at.X + r * 0.08f, at.Y + r * 0.1f, r * 0.2f);
                break;
            case Head.Buttercup:
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * MathF.Tau + 0.4f;
                    Disc(Color.FromArgb(246, 206, 40), at.X + MathF.Cos(a) * r * 0.5f, at.Y + MathF.Sin(a) * r * 0.5f * sq, r * 0.5f);
                }
                Disc(Color.FromArgb(226, 160, 24), at.X, at.Y, r * 0.28f);
                break;
            case Head.Dandelion:
                // A pompom: a yellow disc with a ring of short rays and a paler middle.
                Disc(Color.FromArgb(228, 188, 30), at.X, at.Y, r);
                using (var ray = new Pen(Color.FromArgb(246, 216, 60), MathF.Max(1f, r * 0.14f)))
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * MathF.Tau;
                        g.DrawLine(ray, at.X + MathF.Cos(a) * r * 0.4f, at.Y + MathF.Sin(a) * r * 0.4f * sq, at.X + MathF.Cos(a) * r * 1.05f, at.Y + MathF.Sin(a) * r * 1.05f * sq);
                    }
                Disc(Color.FromArgb(250, 228, 90), at.X, at.Y, r * 0.4f);
                break;
            case Head.Clock:
                // Gone to seed: an airy pale ball, mostly see-through, with a few seeds showing as specks.
                Disc(Color.FromArgb(90, 236, 236, 228), at.X, at.Y, r);
                Disc(Color.FromArgb(120, 244, 244, 238), at.X, at.Y, r * 0.6f);
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * MathF.Tau + 0.2f;
                    Disc(Color.FromArgb(160, 206, 200, 180), at.X + MathF.Cos(a) * r * 0.75f, at.Y + MathF.Sin(a) * r * 0.75f * sq, MathF.Max(0.6f, r * 0.08f));
                }
                Disc(Color.FromArgb(150, 130, 90), at.X, at.Y, r * 0.16f);
                break;
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
            widest = MathF.Max(widest, MathF.Max(b.Wid, b.HeadSize * 2.2f));
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
    // The tuft designs: a deck of flip-books dealt to the tufts. Most are
    // plain grass of three heights; the rest are the lawn's variety.
    private const int Designs = 36;
    private static readonly (Flipbook.TuftKind Kind, float Height)[] Deck = BuildDeck();

    private static (Flipbook.TuftKind, float)[] BuildDeck()
    {
        var deck = new List<(Flipbook.TuftKind, float)>();
        for (int i = 0; i < 8; i++) deck.Add((Flipbook.TuftKind.Grass, 0.55f));       // short
        for (int i = 0; i < 8; i++) deck.Add((Flipbook.TuftKind.Grass, 0.8f));        // middling
        for (int i = 0; i < 6; i++) deck.Add((Flipbook.TuftKind.Grass, 1.05f));       // tall
        for (int i = 0; i < 4; i++) deck.Add((Flipbook.TuftKind.Dry, 0.75f));
        for (int i = 0; i < 4; i++) deck.Add((Flipbook.TuftKind.Seeds, 1.1f));
        for (int i = 0; i < 2; i++) deck.Add((Flipbook.TuftKind.Daisies, 0.85f));
        for (int i = 0; i < 2; i++) deck.Add((Flipbook.TuftKind.Buttercups, 0.75f));
        for (int i = 0; i < 2; i++) deck.Add((Flipbook.TuftKind.Dandelions, 0.8f));
        return deck.ToArray();
    }

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
    private readonly int _x0, _y0, _x1, _y1;                    // the grass's box on screen (where grassy pixels are)
    private readonly int _sx0, _sy0, _sw, _sh;                  // the sheet's box: the grass's box widened by a page, so edge tufts keep their blades
    private readonly float _shift;                              // how far (pixels) the carpet slides downwind at lean 1
    private readonly int _cell, _gw, _gh;                       // the wind grid: lean worked out every "cell" pixels, blended between
    private readonly float[] _lean;
    private readonly uint[] _slid;                              // the lawn box with the carpet laid: half its rows are redone each carpet frame
    private int _frame;

    private readonly bool[]? _where;
    private readonly byte[]? _sun;

    // THE LAWN IS FINISHED EVERY OTHER FRAME. The carpet and the tufts are the
    // two dearest things in the whole pond, and they move slowly (a gust
    // takes seconds to cross). So the carpet is laid on even frames (into
    // _slid) and the tufts are stamped on odd frames (onto a copy of the
    // carpet, _sheet, a little picture the size of the lawn's box), and every
    // frame the finished sheet is copied onto the screen. Each frame pays
    // for one of the two jobs plus a copy, instead of both: the slowest frame
    // is what the eye reads as a stutter, and this halves the lawn's share of
    // it. Thirty frames a second is more than enough for grass.
    private readonly FrameBuffer _sheet;                        // the finished lawn, carpet and tufts, box-sized
    private readonly bool[]? _whereBox;                         // the stencil cut to the box, for stamping into the sheet
    private int _tick;

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
        // The lawn's own dice, dealt once from the scene's: however much the
        // lawn rolls (and a new design rolls plenty), everything the scene
        // rolls after it stays where it was.
        rng = new Random(rng.Next());
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
        // The tufts' pages, one flip-book per design in the deck, each page
        // trimmed to the pixels it actually uses. Shadows are faint (26 of
        // 255): hundreds of dark parallel shadows at 48 wove a lattice.
        _pages = new Flipbook.Page[Designs, Flipbook.Poses];
        int ph = 1, pw = 1;                                     // the tallest and widest page, for the bands and the box below
        // The blades of every design are rolled first, in order (the dice must
        // land the same whatever the core count); then the pages are painted
        // on every core at once (each design's pages are its own bitmaps).
        var clumps = new (Flipbook.Blade Blade, PointF Root)[Designs][];
        for (int d = 0; d < Designs; d++) clumps[d] = Flipbook.Tuft(h * Deck[d].Height, rng, Deck[d].Kind);
        var pagesByDesign = new Flipbook.Page[Designs][];
        Parallel.For(0, Designs, d => pagesByDesign[d] = Flipbook.Pages(clumps[d], 26, wind, view));
        for (int d = 0; d < Designs; d++)
            for (int p = 0; p < Flipbook.Poses; p++) { _pages[d, p] = pagesByDesign[d][p]; ph = Math.Max(ph, pagesByDesign[d][p].Sprite.Height); pw = Math.Max(pw, pagesByDesign[d][p].Sprite.Width); }

        // The grass's box: where any grassy pixel is.
        _x0 = width; _y0 = height; _x1 = 0; _y1 = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (grassy[y * width + x]) { _x0 = Math.Min(_x0, x); _x1 = Math.Max(_x1, x); _y0 = Math.Min(_y0, y); _y1 = Math.Max(_y1, y); }
        if (_x1 < _x0) { _x0 = _y0 = 0; _x1 = _y1 = -1; }
        _cell = Math.Max(4, (int)(u / 120));
        _gw = (_x1 - _x0) / _cell + 2; _gh = (_y1 - _y0) / _cell + 2;
        _lean = new float[Math.Max(1, _gw * _gh)];
        // The sheet's box: the grass's box widened by a page, so a tuft rooted
        // at the edge of the grass has room for its blades (a blade past the
        // sheet's edge would be cut off).
        _sx0 = Math.Max(0, _x0 - pw - 2); _sy0 = Math.Max(0, _y0 - ph - 2);
        _sw = Math.Max(1, Math.Min(width - 1, _x1 + pw + 2) - _sx0 + 1); _sh = Math.Max(1, Math.Min(height - 1, _y1 + ph + 2) - _sy0 + 1);
        _slid = new uint[_sw * _sh];
        _sheet = new FrameBuffer(_sw, _sh);
        if (where != null && _x1 >= _x0)
        {
            _whereBox = new bool[_sw * _sh];
            for (int y = 0; y < _sh; y++) Array.Copy(where, (_sy0 + y) * width + _sx0, _whereBox, y * _sw, _sw);
        }

        // How thick the painted carpet is, cell by cell: the share of pixels
        // a blade covers. Tufts follow it (thick grass where the carpet is
        // thick, few where the painter wore it thin or let moss take over),
        // so the standing grass and the painted grass agree about where the
        // lawn is lush. The share is measured against the lawn's typical
        // cell, so a lawn painted thin all over still gets its tufts.
        float space = u * 0.017f;
        int occW = (int)(width / space) + 2, occH = (int)(height / space) + 2;
        var cover = new float[occW * occH];
        var cells = new List<float>();
        for (int cy = 0; cy < occH; cy++)
            for (int cx = 0; cx < occW; cx++)
            {
                int n = 0, grass = 0;
                for (int y = (int)(cy * space); y < (int)((cy + 1) * space) && y < height; y += 2)
                    for (int x = (int)(cx * space); x < (int)((cx + 1) * space) && x < width; x += 2)
                    {
                        if (!grassy[y * width + x]) continue;
                        n++;
                        if ((carpet[y * width + x] >> 24) > 40) grass++;
                    }
                cover[cy * occW + cx] = n == 0 ? 0 : grass / (float)n;
                if (n > 0) cells.Add(cover[cy * occW + cx]);
            }
        cells.Sort();
        float typical = cells.Count == 0 ? 1 : MathF.Max(0.05f, cells[cells.Count * 2 / 3]);

        // Plant the tufts: random spots on the lawn (not sand, stones or
        // rocks, and not right against them), each at least "space" from
        // every tuft already planted, so they scatter the way plants do
        // rather than standing in rows. A coarse grid of "taken" cells
        // makes the spacing test quick. A spot's chance of a tuft is its
        // cell's cover (so the lawn has thick and thin places, not one even
        // crop); what KIND of tuft it gets comes from slow blotches over the
        // lawn (Mottle): the flowers grow in drifts of one kind, the seed
        // heads in their own patches and along the lawn's edges, and the
        // rest is grass, dealt from the deck.
        float keep = h * 0.25f;
        var taken = new List<PointF>[occW * occH];
        var tufts = new List<Tuft>();
        int want = (int)((_x1 - _x0 + 1) * (_y1 - _y0 + 1) / (space * space) * 0.55f);
        int flowerSeed = rng.Next(), seedSeed = rng.Next();
        int grassDesigns = 0; while (grassDesigns < Designs && Deck[grassDesigns].Kind == Flipbook.TuftKind.Grass) grassDesigns++;
        int FirstOf(Flipbook.TuftKind kind) { for (int d = 0; d < Designs; d++) if (Deck[d].Kind == kind) return d; return 0; }
        int dryAt = FirstOf(Flipbook.TuftKind.Dry), seedsAt = FirstOf(Flipbook.TuftKind.Seeds), daisyAt = FirstOf(Flipbook.TuftKind.Daisies), buttercupAt = FirstOf(Flipbook.TuftKind.Buttercups), dandelionAt = FirstOf(Flipbook.TuftKind.Dandelions);
        for (int tries = 0; tries < want * 8 && tufts.Count < want; tries++)
        {
            int x = _x0 + rng.Next(_x1 - _x0 + 1), y = _y0 + rng.Next(_y1 - _y0 + 1);
            if (!Grassy(x, y) || !Grassy(x - keep, y) || !Grassy(x + keep, y) || !Grassy(x, y - keep) || !Grassy(x, y + keep)) continue;
            int ox = (int)(x / space), oy = (int)(y / space);
            float chance = MathF.Min(1f, cover[oy * occW + ox] / typical);
            if (rng.NextDouble() > chance * chance) continue;
            bool crowded = false;
            for (int j = oy - 1; j <= oy + 1 && !crowded; j++)
                for (int i = ox - 1; i <= ox + 1 && !crowded; i++)
                {
                    List<PointF>? near = i >= 0 && j >= 0 && i < occW && j < occH ? taken[j * occW + i] : null;
                    if (near != null) foreach (PointF q in near) if ((q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y) < space * space) { crowded = true; break; }
                }
            if (crowded) continue;
            (taken[oy * occW + ox] ??= new List<PointF>()).Add(new PointF(x, y));
            // The kind. Flower drifts: where the flower blotch is high, one kind per drift
            // (the drift's own corner of a coarse grid picks it), and not every tuft in a
            // drift flowers. Seed heads: their own blotch, and the lawn's edges, where the
            // mower never reached. Dry tufts: a sprinkle anywhere. Else grass.
            int design;
            float flower = Mottle.Blotch(x, y, u * 0.16f, flowerSeed);
            float seeds = Mottle.Blotch(x, y, u * 0.2f, seedSeed);
            float edge = h * 1.2f;
            bool nearEdge = !Grassy(x - edge, y) || !Grassy(x + edge, y) || !Grassy(x, y - edge) || !Grassy(x, y + edge);
            if (flower > 0.6f && rng.NextDouble() < 0.6)
            {
                int drift = (int)(x / (u * 0.22f)) * 31 + (int)(y / (u * 0.18f)) * 17 + flowerSeed;
                int at = (drift & 0xFFFF) % 3 switch { 0 => daisyAt, 1 => buttercupAt, _ => dandelionAt };
                design = at + rng.Next(2);
            }
            else if ((seeds > 0.64f && rng.NextDouble() < 0.3) || (nearEdge && rng.NextDouble() < 0.12)) design = seedsAt + rng.Next(4);
            else if (rng.NextDouble() < 0.08) design = dryAt + rng.Next(4);
            else design = rng.Next(grassDesigns);
            tufts.Add(new Tuft(x, y, (byte)design, 0.7f + 0.6f * (float)rng.NextDouble()));
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
        _tick++;
        bool carpetFrame = _tick <= 2 || (_tick & 1) == 0, tuftFrame = _tick <= 2 || (_tick & 1) == 1;   // the first two frames do both, so the lawn is whole from the start

        if (carpetFrame)
        {
            // The wind on a coarse grid (a sine per pixel would cost more than the drawing).
            for (int gy = 0; gy < _gh; gy++)
                for (int gx = 0; gx < _gw; gx++)
                    _lean[gy * _gw + gx] = _wind.LeanAt(_x0 + gx * _cell, _y0 + gy * _cell);
            SlideCarpet(fb);
        }
        if (tuftFrame)
        {
            // The sheet starts as the carpet as last laid, and the tufts are
            // stamped onto it: far to near within each band; even bands
            // together, then odd bands together (a tuft only ever touches
            // tufts in its own band or the next, never two bands away). Where
            // a band's last tuft meets the next band's first the order is off
            // by one band: that is a blade's edge in front of a neighbour's,
            // which the eye cannot tell.
            Array.Copy(_slid, _sheet.Pixels, _slid.Length);
            foreach (int parity in (ReadOnlySpan<int>)[0, 1])
                Parallel.For(0, (_bands.Length + 1 - parity) / 2, k =>
                {
                    foreach (Tuft tuft in _bands[k * 2 + parity])
                    {
                        Flipbook.Page pg = _pages[tuft.Design, Flipbook.PageOf(_wind.LeanAt(tuft.X, tuft.Y) * tuft.Stiff)];
                        int shade = _sun == null ? 256 : _sun[tuft.Y * _w + tuft.X] + 1;
                        pg.Sprite.Draw(_sheet, tuft.X - _sx0 - pg.Ax, tuft.Y - _sy0 - pg.Ay, 1f, _whereBox, shade: shade);
                    }
                });
        }
        // The finished lawn onto the screen, row by row.
        uint[] px = fb.Pixels, sheet = _sheet.Pixels;
        for (int y = 0; y < _sh; y++) Array.Copy(sheet, y * _sw, px, (_sy0 + y) * fb.Width + _sx0, _sw);
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
        int w = fb.Width;
        uint[] px = fb.Pixels;
        int parity = _frame & 1;
        bool first = _frame < 2;
        _frame++;
        float shiftX = _shift * _wind.X, shiftY = _shift * _wind.Y * _view.Squash;
        Parallel.For(_sy0, _sy0 + _sh, y =>
        {
            int keepRow = (y - _sy0) * _sw;
            if (!first && (y & 1) != parity) return;                  // this row's turn is next time; _slid still holds it as last laid
            Array.Copy(px, y * w + _sx0, _slid, keepRow, _sw);        // the row as it is (ground, sand, stones, and the branches over them): overwritten where grass lands
            if (y < _y0 || y > _y1) return;                           // the sheet's margin above or below the grass: ground only
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
                // Brighter where it leans: bent blades show their pale undersides
                // to the sky, which is what a gust looks like from above (a
                // sheen rolling over the field). In 128ths; the blue channel
                // gains less, so the sheen is a pale yellow-green, not grey.
                // Darker in a branch's shadow.
                int bright = (int)((1 + 0.6f * (lean - 0.2f)) * 128);
                if (_sun != null) bright = bright * (_sun[i] + 1) >> 8;
                int brightB = (bright * 3 + 128) >> 2;
                int r = (int)(rb >> 16) * bright >> 7, g = (int)(ag & 0xFF) * bright >> 7, b = (int)(rb & 0xFF) * brightB >> 7;
                uint bg = px[i];
                int keep = 256 - a;
                int rr = ((int)((bg >> 16) & 0xFF) * keep >> 8) + r, gg = ((int)((bg >> 8) & 0xFF) * keep >> 8) + g, bb = ((int)(bg & 0xFF) * keep >> 8) + b;
                _slid[keepRow + x - _sx0] = (uint)(Math.Min(255, rr) << 16 | Math.Min(255, gg) << 8 | Math.Min(255, bb));
            }
            ArrayPool<float>.Shared.Return(rowLean);
        });
    }
}

/// <summary>
/// Boughs that sway in the wind: the painted branches (blossom and wood) on
/// a clear sheet of their own, laid over the finished frame each frame
/// SHIFTED a little downwind, more the further a pixel is from its tree's
/// root (a bough's tip swings, its base barely moves).
///
/// FEYNMAN VERSION: a branch in a breeze is a spring. The wind pushes it
/// over, it bends, and when the gust eases it swings back past upright and
/// settles, a slow swing for the thick base (a couple of seconds a swing) and
/// a quicker one for the thin tips. So each tree keeps two springs, driven by
/// the wind's lean at its root, and a pixel's shift is a mix of the two by how
/// far out it is. The picture is sampled between pixels (four neighbours
/// blended) so the motion is smooth at any fraction of a pixel, the same
/// trick the lawn's carpet uses.
///
/// What this cannot do, said plainly: the branches' shadows on the water are
/// painted once and do not move with them (a soft dappled shadow shifting
/// a few pixels is not missed), and anything that passes BEHIND a branch is
/// cut at the branch's resting footprint, so for the few pixels a bough has
/// swung away from, the water shows between it and the thing behind.
/// </summary>
public sealed class Boughs
{
    private const int Tile = 8;
    private readonly int _w, _h, _y0, _y1, _margin, _tw, _th;
    private readonly uint[] _rb, _ag;                           // the sheet, premultiplied and packed two channels to a number (see Lawn)
    private readonly byte[] _amp;                               // 0 to 255 per pixel: how far out along a bough it is (0 at a root)
    private readonly byte[] _tree;                              // which tree's springs a pixel follows
    private readonly bool[] _tiles;                             // which 16x16 tiles hold any bough pixel (widened by the sway): the rest are skipped
    private readonly int[] _rowMin, _rowMax;                    // the painted stretch of each row (-1 when empty)
    private readonly PointF[] _roots;
    private readonly float _maxPx;
    private readonly Wind _wind;
    private readonly Overhead _view;
    private readonly float[] _base, _baseV, _tip, _tipV;        // the springs: position and speed, base and tip, per tree
    private readonly float[] _shiftBase, _shiftTip;             // this frame's shift per tree, in pixels along the wind, base and tip
    // THE SWAYED SHEET, kept between frames. Shifting the picture by a fraction
    // of a pixel (four neighbours blended) is the dear part, and the boughs
    // move well under a pixel a frame, so only every other row is shifted
    // afresh each frame (odd rows one frame, even the next) into these two
    // arrays, and every frame all rows are laid over the picture from them,
    // which is cheap. The same halving the lawn's carpet uses.
    private readonly uint[] _swayRb, _swayAg;
    private int _frame;

    /// <param name="argb">The branches on a clear sheet, 0xAARRGGBB, the frame's size.</param>
    /// <param name="roots">Where each tree's trunk is (off the picture is fine): the sway grows with distance from the nearest.</param>
    /// <param name="reachPx">How far from a root a bough reaches its full sway.</param>
    /// <param name="maxSwayPx">The tip's shift at lean 1, in pixels.</param>
    /// <param name="backdrop">The scene's backdrop (0x00RRGGBB): the parts of the boughs that hardly move (the thick
    /// wood and the crowns near the trunks, under a pixel of sway) are painted into it here, once, and left out of
    /// the live sheet. A crown is the densest part of the picture and the stillest; laying it afresh every frame
    /// was half the cost for nothing the eye could see.</param>
    public Boughs(int width, int height, uint[] argb, PointF[] roots, float reachPx, float maxSwayPx, Wind wind, Overhead view, uint[] backdrop)
    {
        _w = width; _h = height;
        _roots = roots;
        _maxPx = maxSwayPx;
        _wind = wind; _view = view;
        _margin = (int)MathF.Ceiling(maxSwayPx) + 2;
        _rb = new uint[argb.Length]; _ag = new uint[argb.Length];
        _amp = new byte[argb.Length]; _tree = new byte[argb.Length];
        _rowMin = new int[height]; _rowMax = new int[height];
        _y0 = height; _y1 = -1;
        // Below this much of the full sway a pixel is "still": with the tip at 26 px it moves under a pixel at a full lean.
        const float still = 0.2f;
        for (int y = 0; y < height; y++)
        {
            _rowMin[y] = -1; _rowMax[y] = -1;
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                // The nearest root, and how far out this pixel is: a gentle curve (the base stiff, the tips free).
                float best = float.MaxValue; int bt = 0;
                for (int t = 0; t < roots.Length; t++)
                {
                    float dx = x - roots[t].X, dy = (y - roots[t].Y) / view.Squash, d = dx * dx + dy * dy;
                    if (d < best) { best = d; bt = t; }
                }
                float outward = MathF.Min(1f, MathF.Sqrt(best) / reachPx);
                float amp = MathF.Pow(outward, 1.4f);
                _amp[i] = (byte)(amp * 255);
                _tree[i] = (byte)bt;
                uint c = argb[i];
                uint a = c >> 24;
                if (a == 0) continue;
                if (amp < still)
                {
                    // Painted into the backdrop once, the way the painter would have.
                    uint bg = backdrop[i];
                    int keep = (int)(255 - a);
                    int rr = ((int)((bg >> 16) & 0xFF) * keep + (int)((c >> 16) & 0xFF) * (int)a) / 255;
                    int gg = ((int)((bg >> 8) & 0xFF) * keep + (int)((c >> 8) & 0xFF) * (int)a) / 255;
                    int bb = ((int)(bg & 0xFF) * keep + (int)(c & 0xFF) * (int)a) / 255;
                    backdrop[i] = (uint)(rr << 16 | gg << 8 | bb);
                    continue;
                }
                uint r = ((c >> 16) & 0xFF) * a / 255, g = ((c >> 8) & 0xFF) * a / 255, b = (c & 0xFF) * a / 255;
                _rb[i] = r << 16 | b;
                _ag[i] = a << 16 | g;
                if (_rowMin[y] < 0) _rowMin[y] = x;
                _rowMax[y] = x;
            }
            if (_rowMin[y] >= 0) { _y0 = Math.Min(_y0, y); _y1 = Math.Max(_y1, y); }
        }
        // The tiles: a tile is live if any live bough pixel lies within ITS OWN sway's reach of it
        // (a pixel near a trunk reaches a pixel or two; a tip reaches the full sway).
        _tw = (width + Tile - 1) / Tile; _th = (height + Tile - 1) / Tile;
        _tiles = new bool[_tw * _th];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if ((_ag[i] >> 16) == 0) continue;
                int reach = (int)MathF.Ceiling(maxSwayPx * _amp[i] / 255f) + 2;
                int reachTiles = (reach + Tile - 1) / Tile;
                int tx = x / Tile, ty = y / Tile;
                for (int j = Math.Max(0, ty - reachTiles); j <= Math.Min(_th - 1, ty + reachTiles); j++)
                    for (int k = Math.Max(0, tx - reachTiles); k <= Math.Min(_tw - 1, tx + reachTiles); k++)
                        _tiles[j * _tw + k] = true;
            }
        _swayRb = new uint[argb.Length]; _swayAg = new uint[argb.Length];
        int n = roots.Length;
        _base = new float[n]; _baseV = new float[n]; _tip = new float[n]; _tipV = new float[n];
        _shiftBase = new float[n]; _shiftTip = new float[n];
        for (int t = 0; t < n; t++) _base[t] = _tip[t] = wind.LeanAt(roots[t].X, roots[t].Y);   // start at rest in today's breeze, not with a lurch from zero
        Update(0);
    }

    /// <summary>Once a frame, after the wind: the springs take a step.</summary>
    public void Update(float dt)
    {
        dt = MathF.Min(dt, 1f / 30f);                           // a long pause (the window hidden) must not fling the springs
        for (int t = 0; t < _roots.Length; t++)
        {
            float lean = _wind.LeanAt(_roots[t].X, _roots[t].Y);
            // A damped spring: pulled toward the wind's lean, slowed by its own speed.
            // The base swings once in 2.6 s, the tips once in 1.3 s; both settle in a swing or two.
            Spring(ref _base[t], ref _baseV[t], lean, 2.6f, 0.35f, dt);
            Spring(ref _tip[t], ref _tipV[t], lean, 1.3f, 0.25f, dt);
            _shiftBase[t] = _base[t] * _maxPx;
            _shiftTip[t] = _tip[t] * _maxPx;
        }
    }

    private static void Spring(ref float s, ref float v, float target, float period, float damping, float dt)
    {
        float k = MathF.Pow(MathF.Tau / period, 2), c = 2 * damping * MathF.Sqrt(k);
        v += (k * (target - s) - c * v) * dt;
        s += v * dt;
    }

    /// <summary>
    /// Lays the boughs over the frame. First the sway: every other row of the
    /// kept sheet is taken afresh from a little upwind of itself by its sway
    /// (the first two frames do every row, so the picture is whole from the
    /// start). Then the lay: every row of the kept sheet over the picture.
    /// Both skip the tiles no bough can reach.
    /// </summary>
    public void Draw(FrameBuffer fb)
    {
        if (_y1 < _y0) return;
        int w = _w, h = _h;
        uint[] px = fb.Pixels;
        float sx = _wind.X, sy = _wind.Y * _view.Squash;
        int parity = _frame & 1;
        bool every = _frame < 2;
        _frame++;
        Parallel.For(_y0, _y1 + 1, y =>
        {
            int x0 = Math.Max(0, _rowMin[y] - _margin), x1 = Math.Min(w - 1, _rowMax[y] + _margin);
            int tileRow = y / Tile * _tw;
            bool sway = every || (y & 1) == parity;
            for (int x = x0; x <= x1; x++)
            {
                if (!_tiles[tileRow + x / Tile]) { x = (x / Tile + 1) * Tile - 1; continue; }   // an empty tile: skip to its end
                int i = y * w + x;
                if (sway)
                {
                    float a = _amp[i] / 255f;
                    int t = _tree[i];
                    float shift = (_shiftBase[t] * (1 - a) + _shiftTip[t] * a) * a;
                    float fx = x - shift * sx, fy = y - shift * sy;
                    int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
                    if (ix < 0 || iy < 0 || ix + 1 >= w || iy + 1 >= h) { _swayAg[i] = 0; _swayRb[i] = 0; continue; }
                    int tx = (int)((fx - ix) * 16), ty = (int)((fy - iy) * 16);
                    uint w11 = (uint)(tx * ty), w10 = (uint)(tx * (16 - ty)), w01 = (uint)((16 - tx) * ty), w00 = (uint)((16 - tx) * (16 - ty));
                    int j = iy * w + ix;
                    _swayAg[i] = ((_ag[j] * w00 + _ag[j + 1] * w10 + _ag[j + w] * w01 + _ag[j + w + 1] * w11) >> 8) & 0x00FF00FF;
                    _swayRb[i] = ((_rb[j] * w00 + _rb[j + 1] * w10 + _rb[j + w] * w01 + _rb[j + w + 1] * w11) >> 8) & 0x00FF00FF;
                }
                uint ag = _swayAg[i];
                int alpha = (int)(ag >> 16);
                if (alpha < 2) continue;
                uint rb = _swayRb[i];
                int r = (int)(rb >> 16), g = (int)(ag & 0xFF), b = (int)(rb & 0xFF);
                uint bg = px[i];
                int keep = 256 - alpha;
                int rr = ((int)((bg >> 16) & 0xFF) * keep >> 8) + r, gg = ((int)((bg >> 8) & 0xFF) * keep >> 8) + g, bb = ((int)(bg & 0xFF) * keep >> 8) + b;
                px[i] = (uint)(Math.Min(255, rr) << 16 | Math.Min(255, gg) << 8 | Math.Min(255, bb));
            }
        });
    }
}
