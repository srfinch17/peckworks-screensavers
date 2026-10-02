using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Peckworks.Screensavers.Core.Sakura;

/// <summary>
/// One bank of land: its top edge as a line of points (left to right), and
/// a way to ask "how high is the ground at this x?".
///
/// Why this exists: earlier, the ground was a plain polygon and each tree
/// had its trunk end at a fixed height. Where the bank sloped down toward
/// the water, the trunk stopped in mid air and the tree looked like it was
/// floating. Now the ground is the single source of truth: a tree asks the
/// bank where the ground is under it and plants its trunk there, so a tree
/// placed anywhere along the bank stands on it whatever slope it has.
/// </summary>
public sealed class Bank
{
    private readonly PointF[] _edge;
    private readonly int _h;

    public Bank(PointF[] edge, int h) { _edge = edge; _h = h; }

    /// <summary>The top edge, left to right.</summary>
    public PointF[] Edge => _edge;

    /// <summary>The whole shape to fill: the top edge, then down to the bottom of the screen and back.</summary>
    public PointF[] Polygon() =>
        [.. _edge, new PointF(_edge[^1].X, _h + 2), new PointF(_edge[0].X, _h + 2)];

    /// <summary>
    /// Ground height at x, found by walking along the edge to the two
    /// points that straddle x and blending between them ("linear
    /// interpolation": if x is 30% of the way from point A to point B, the
    /// answer is 30% of the way from A's height to B's height). Past either
    /// end of the edge, the end point's height is returned.
    /// </summary>
    public float YAt(float x)
    {
        if (x <= _edge[0].X) return _edge[0].Y;
        for (int i = 1; i < _edge.Length; i++)
        {
            if (x > _edge[i].X) continue;
            float t = (x - _edge[i - 1].X) / (_edge[i].X - _edge[i - 1].X);
            return _edge[i - 1].Y + (_edge[i].Y - _edge[i - 1].Y) * t;
        }
        return _edge[^1].Y;
    }

    /// <summary>
    /// Paints the bank: a gradient (lit along its top, darker toward the
    /// bottom of the screen) with a thin lighter rim along the top edge, so
    /// it reads as a rounded bank rather than a flat cutout.
    /// </summary>
    /// <param name="rimWidth">The rim line's thickness in pixels (about 0.4% of the size unit reads well).</param>
    public void Paint(Graphics g, Color top, Color bottom, Color rim, float rimWidth)
    {
        float edgeTop = _edge.Min(p => p.Y);
        using (var brush = new LinearGradientBrush(new PointF(0, edgeTop - 1), new PointF(0, _h + 1), top, bottom))
            g.FillPolygon(brush, Polygon());
        using var pen = new Pen(rim, rimWidth) { LineJoin = LineJoin.Round };
        g.DrawLines(pen, _edge);
    }
}

/// <summary>The three pinks a cloud of blossoms is painted with.</summary>
/// <param name="Body">The soft solid color under the dots.</param>
/// <param name="Deep">The darkest dots (in shadow).</param>
/// <param name="Light">The lightest dots (catching the sun).</param>
public sealed record BlossomPalette(Color Body, Color Deep, Color Light)
{
    /// <summary>Blossoms in plain daylight.</summary>
    public static BlossomPalette Daylight { get; } =
        new(Color.FromArgb(240, 210, 134, 166), Color.FromArgb(200, 104, 146), Color.FromArgb(255, 238, 245));
}

/// <summary>
/// The brushes every sakura backdrop is painted with: banks of land, forked
/// trunks, clouds of blossom, a pine, and branches that grow themselves.
/// Each screensaver decides WHERE things go and in WHAT colors; these
/// helpers decide how a tree or a branch looks, so every scene shares the
/// same illustrative hand.
///
/// This uses System.Drawing (GDI+), the standard .NET 2D drawing kit:
/// shapes, gradients, and anti-aliasing (smoothed edges). It is too slow
/// for the petals, but perfect for painting a backdrop once.
///
/// Sizes are passed in "u": the screen height on a normal wide screen, or
/// less on a tall one (see any painter's Paint method), so a tree's height
/// and width scale together on every screen shape.
/// </summary>
public static class Brushwork
{
    /// <summary>
    /// A trunk that forks: one thick stem from the ground to a little more than
    /// halfway up, then two thinner limbs that lean apart and disappear into the
    /// canopy (the blossoms are painted afterward and cover their ends). The
    /// whole thing leans slightly, and the lean is random, so a grove is not
    /// a row of identical posts. Thickness scales with the canopy's size (r).
    /// </summary>
    public static void Trunk(Graphics g, float x, float baseY, float canopyY, float r, Random rng, Color? bark = null)
    {
        float lean = r * 0.15f * ((float)rng.NextDouble() * 2 - 1);
        var mid = new PointF(x + lean, baseY - (baseY - canopyY) * 0.6f);
        float thick = Math.Max(2f, r * 0.11f);
        Color color = bark ?? Color.FromArgb(52, 38, 40);

        using (var stem = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(stem, new PointF(x, baseY), mid);
        using var limb = new Pen(color, thick * 0.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(limb, mid, new PointF(mid.X + lean - r * 0.25f, canopyY + r * 0.05f));
        g.DrawLine(limb, mid, new PointF(mid.X + lean + r * 0.30f, canopyY - r * 0.05f));
    }

    /// <summary>
    /// A cloud of blossoms, built from "lumps" the way a child draws a cloud:
    /// several overlapping round bunches. Each lump gets a soft pink body, then
    /// hundreds of tiny dots of light and deep pink scattered over it and a bit
    /// past its edge, so the outline comes out bumpy and fluffy instead of a
    /// smooth circle. Dots in the upper left come out lighter, as if the sun
    /// (on the left) catches them.
    /// </summary>
    public static void Canopy(Graphics g, float cx, float cy, float r, float u, Random rng, BlossomPalette? palette = null)
    {
        BlossomPalette pal = palette ?? BlossomPalette.Daylight;
        int lumpCount = 7 + rng.Next(4);
        var lumps = new (float X, float Y, float R)[lumpCount];
        for (int i = 0; i < lumpCount; i++)
        {
            // Random point inside an ellipse: random angle, random distance.
            // Square root on the distance keeps points evenly spread instead of
            // bunched in the middle.
            double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble()) * 0.6;
            lumps[i] = (cx + (float)(Math.Cos(ang) * dist) * r * 1.2f,   // wider than tall
                        cy + (float)(Math.Sin(ang) * dist) * r * 0.6f,
                        r * (0.28f + 0.14f * (float)rng.NextDouble()));
        }

        using (var body = new SolidBrush(pal.Body))
            foreach (var l in lumps)
                g.FillEllipse(body, l.X - l.R * 0.9f, l.Y - l.R * 0.9f, l.R * 1.8f, l.R * 1.8f);

        int dots = Math.Min(1800, (int)(r * r / (u * 0.0045f * u * 0.0045f) * 1.5f));
        for (int i = 0; i < dots; i++)
        {
            var l = lumps[rng.Next(lumpCount)];
            double ang = rng.NextDouble() * Math.PI * 2, dist = Math.Sqrt(rng.NextDouble()) * 1.05;
            float x = l.X + (float)(Math.Cos(ang) * dist) * l.R;
            float y = l.Y + (float)(Math.Sin(ang) * dist) * l.R;
            float light = Math.Clamp(0.5f - (x - cx) / r * 0.3f - (y - cy) / r * 0.4f + ((float)rng.NextDouble() - 0.5f) * 0.6f, 0, 1);
            Color c = Mix(pal.Deep, pal.Light, light);
            float dr = u * (0.003f + 0.004f * (float)rng.NextDouble());
            using var b = new SolidBrush(Color.FromArgb(230, c));
            g.FillEllipse(b, x - dr, y - dr, dr * 2, dr * 2);
        }
    }

    /// <summary>
    /// A tall dark pine, the classic cone shape: narrow at the tip, wide at the
    /// foot. Two passes:
    ///
    ///   1. The SILHOUETTE: one solid dark-green shape, traced down the left
    ///      side and back up the right. Its edge is jagged because each row of
    ///      boughs sticks out a slightly different amount, and each row's tips
    ///      hang a little lower than its middle, the way real boughs droop.
    ///      Painting this first guarantees the tree is one solid shape, never a
    ///      stack of separate pancakes with sky showing between them.
    ///   2. The TEXTURE: small ovals scattered over the silhouette in lighter
    ///      and darker greens, which gives the surface the look of clumps of
    ///      needles. The right side comes out darker (the sun is on the left).
    ///
    /// The trunk runs all the way to the ground, which is passed in, so the
    /// pine stands on the bank like everything else.
    /// </summary>
    /// <param name="snowy">
    /// True lays a dab of snow along the top of each row of boughs, for a
    /// winter scene. Snow settles on top of things, so the dabs sit on the
    /// upper side of each row and leave the green showing underneath.
    /// </param>
    public static void Pine(Graphics g, float x, float top, float ground, float u, Random rng, bool snowy = false)
    {
        using (var trunk = new Pen(Color.FromArgb(48, 38, 34), u * 0.007f))
            g.DrawLine(trunk, x, ground, x, top + u * 0.02f);

        float bottom = ground - u * 0.03f;
        float HalfWidthAt(float y) => PineHalfWidth(y, top, ground, u);

        // Pass 1: the silhouette. Left edge going down, then right edge coming back up.
        var leftEdge = new List<PointF> { new(x, top) };
        var rightEdge = new List<PointF>();
        float rowStep = u * 0.02f;
        for (float y = top + rowStep; y <= bottom; y += rowStep)
        {
            float hw = HalfWidthAt(y);
            float droop = hw * 0.2f;
            leftEdge.Add(new PointF(x - hw * (0.85f + 0.3f * (float)rng.NextDouble()), y + droop));
            leftEdge.Add(new PointF(x - hw * 0.45f, y + droop * 0.4f));                // the notch between one row and the next
            rightEdge.Add(new PointF(x + hw * (0.85f + 0.3f * (float)rng.NextDouble()), y + droop));
            rightEdge.Add(new PointF(x + hw * 0.45f, y + droop * 0.4f));
        }
        rightEdge.Reverse();
        using (var body = new SolidBrush(Color.FromArgb(30, 60, 44)))
            g.FillPolygon(body, [.. leftEdge, .. rightEdge]);

        // Pass 2: the texture of needle clumps.
        Color dark = Color.FromArgb(22, 48, 36), lit = Color.FromArgb(70, 112, 76);
        for (float y = top + u * 0.015f; y < bottom; y += u * 0.014f)
        {
            float hw = HalfWidthAt(y);
            int puffs = 2 + (int)(hw / (u * 0.008f));
            for (int k = 0; k < puffs; k++)
            {
                float f = (k + (float)rng.NextDouble()) / puffs * 2 - 1;          // -1 (left tip) ... +1 (right tip), evenly spread
                float px = x + f * hw;
                float py = y + MathF.Abs(f) * hw * 0.2f;                           // tips hang lower, matching the silhouette
                float pw = u * (0.006f + 0.007f * (float)rng.NextDouble());
                float ph = pw * 0.6f;
                float light = Math.Clamp(0.5f - f * 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.5f, 0, 1);
                using var needles = new SolidBrush(Mix(dark, lit, light));
                g.FillEllipse(needles, px - pw, py - ph, pw * 2, ph * 2);
            }
        }

        if (!snowy) return;

        // Pass 3 (winter only): snow resting on each row of boughs. Flat, wide
        // dabs that follow the same droop as the boughs, with gaps left at
        // random so the tree is frosted and not buried.
        using var snow = new SolidBrush(Color.FromArgb(225, 236, 243, 255));
        for (float y = top + rowStep; y <= bottom; y += rowStep)
        {
            float hw = HalfWidthAt(y);
            int dabs = 2 + (int)(hw / (u * 0.012f));
            for (int k = 0; k < dabs; k++)
            {
                if (rng.NextDouble() < 0.3) continue;
                float f = (k + (float)rng.NextDouble()) / dabs * 2 - 1;
                float pw = u * (0.007f + 0.008f * (float)rng.NextDouble());
                float px = x + f * (hw - pw * 0.5f);
                float py = y + MathF.Abs(f) * hw * 0.2f - rowStep * 0.25f;
                g.FillEllipse(snow, px - pw, py - pw * 0.3f, pw * 2, pw * 0.6f);
            }
        }
        g.FillEllipse(snow, x - u * 0.008f, top - u * 0.002f, u * 0.016f, u * 0.012f);   // a cap on the very tip
    }

    /// <summary>
    /// How far a pine's boughs reach to either side of its trunk at height y.
    /// The pine is a cone: nearly a point at the top, widest at the foot. A
    /// scene that hangs things ON a pine (strings of lights) asks this so the
    /// lights follow the tree's real outline instead of guessing it.
    /// </summary>
    public static float PineHalfWidth(float y, float top, float ground, float u)
    {
        float height = ground - u * 0.03f - top;
        return u * (0.006f + 0.070f * (y - top) / height);   // the cone: wider lower down
    }

    /// <summary>
    /// A range of hills as a filled shape: a wavy top edge made of three sine
    /// waves added together (different wavelengths, random starting points),
    /// closed off along the horizon. Adding waves of different lengths is the
    /// cheap way to get a line that looks natural: no single rhythm repeats.
    /// </summary>
    /// <param name="baseRise">How far the range stands above the horizon on average, in u.</param>
    /// <param name="a1">Height of the long rolling wave, in u.</param>
    /// <param name="a2">Height of the short bumpy wave, in u.</param>
    /// <param name="a3">Height of the rounded peaks, in u.</param>
    public static PointF[] Ridge(int w, float horizon, float u, float baseRise, float a1, float a2, float a3, Random rng)
    {
        float p1 = (float)rng.NextDouble() * 6, p2 = (float)rng.NextDouble() * 6, p3 = (float)rng.NextDouble() * 6;
        var pts = new List<PointF> { new(-2, horizon + 2) };
        for (float x = -2; x <= w + 2; x += Math.Max(2, u / 200f))
        {
            float t = x / u;
            float rise = baseRise
                       + a1 * MathF.Sin(t * 2.6f + p1)
                       + a2 * MathF.Sin(t * 6.1f + p2)
                       + a3 * MathF.Abs(MathF.Sin(t * 1.4f + p3));   // Abs makes rounded peaks
            pts.Add(new PointF(x, horizon - u * rise));
        }
        pts.Add(new PointF(w + 2, horizon + 2));
        return [.. pts];
    }

    /// <summary>The top of a ridge at x (the nearest point on its wavy edge), so a house can stand on it.</summary>
    public static float RidgeYAt(PointF[] ridge, float x)
    {
        PointF best = ridge[1];
        for (int i = 1; i < ridge.Length - 1; i++)   // skip the two closing corners on the horizon
            if (MathF.Abs(ridge[i].X - x) < MathF.Abs(best.X - x)) best = ridge[i];
        return best.Y;
    }

    /// <summary>Where a branch starts and which way it heads. See <see cref="Branches"/>.</summary>
    /// <param name="Start">The root of the branch, usually just off the edge of the screen.</param>
    /// <param name="Angle">Radians, measured clockwise from "pointing right" (screen y grows downward): 0 = right, pi/2 = down, pi = left.</param>
    /// <param name="Length">How far the main stem reaches, in pixels.</param>
    /// <param name="Thickness">The stem's width at its root, in pixels.</param>
    /// <param name="Depth">How many generations of side branches may sprout (0 = none).</param>
    public sealed record BranchSeed(PointF Start, float Angle, float Length, float Thickness, int Depth);

    /// <summary>
    /// Cherry branches covered in blossom, one per seed. Returns the blossom
    /// cluster positions so petals can fall from them.
    ///
    /// Branches are grown, not drawn by hand. Grow() walks forward in small
    /// steps, wobbling a little and sagging downward under its own weight, and
    /// now and then sprouts a thinner side branch that does the same thing.
    /// A rule that calls itself like this is called "recursion", and it is how
    /// most computer-drawn plants are made: a few simple rules, repeated,
    /// produce something that looks organic.
    /// </summary>
    /// <param name="u">The size unit (see the class summary). Flower and cluster sizes come from it.</param>
    /// <param name="petalLight">The palest blossom color.</param>
    /// <param name="petalDeep">The deepest blossom color.</param>
    /// <param name="bare">
    /// True grows the wood only, with no flowers: a winter or dead tree. The
    /// spots where flowers WOULD have been are still returned, which is handy
    /// for hanging something else there (Christmas lights, say).
    /// </param>
    public static List<PointF> Branches(Graphics g, IEnumerable<BranchSeed> seeds, float u, Random rng,
                                        Color wood, Color petalLight, Color petalDeep, bool bare = false)
    {
        var timber = new List<(PointF A, PointF B, float Width)>();
        var flowers = new List<(PointF C, float R, Color Petal, float Turn)>();
        var spots = new List<PointF>();

        void Cluster(PointF at, float spread)
        {
            spots.Add(at);
            if (bare) return;
            int n = 3 + rng.Next(5);
            for (int i = 0; i < n; i++)
            {
                var c = new PointF(at.X + spread * ((float)rng.NextDouble() * 2 - 1),
                                   at.Y + spread * ((float)rng.NextDouble() * 2 - 1));
                float r = u * (0.008f + 0.005f * (float)rng.NextDouble());
                Color petal = Mix(petalLight, petalDeep, (float)Math.Pow(rng.NextDouble(), 0.8));
                flowers.Add((c, r, petal, (float)(rng.NextDouble() * Math.PI * 2)));
            }
        }

        void Grow(PointF p, float angle, float length, float thick, int depth)
        {
            const int steps = 8;
            float stepLen = length / steps;
            for (int i = 0; i < steps; i++)
            {
                angle += ((float)rng.NextDouble() - 0.5f) * 0.35f;       // wobble
                angle += (MathF.PI / 2 - angle) * 0.03f;                  // sag toward "straight down" (pi/2 in screen coordinates)
                var next = new PointF(p.X + MathF.Cos(angle) * stepLen, p.Y + MathF.Sin(angle) * stepLen);
                timber.Add((p, next, thick));
                thick *= 0.88f;                                           // taper

                if (depth > 0 && rng.NextDouble() < 0.45)                 // sprout a side branch
                {
                    float turn = (0.4f + 0.5f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? -1 : 1);
                    Grow(next, angle + turn, length * 0.5f * (1 - i / (float)steps * 0.4f), thick * 0.75f, depth - 1);
                }
                if (thick < u * 0.012f && rng.NextDouble() < 0.5)         // thin enough to bloom
                    Cluster(next, u * 0.02f);
                p = next;
            }
            Cluster(p, u * 0.024f);                                       // every tip ends in a big cluster
        }

        foreach (var s in seeds)
            Grow(s.Start, s.Angle, s.Length, s.Thickness, s.Depth);

        // Wood first, then all the flowers, so no branch is painted over a blossom.
        foreach (var (a, b, width) in timber)
        {
            using var pen = new Pen(wood, Math.Max(1f, width)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }
        foreach (var (c, r, petal, turn) in flowers)
            Flower(g, c, r, petal, turn);

        return spots;
    }

    /// <summary>One cherry blossom: five round petals in a ring around a deep pink center.</summary>
    public static void Flower(Graphics g, PointF c, float r, Color petal, float turn)
    {
        using var petalBrush = new SolidBrush(Color.FromArgb(245, petal));
        float pr = r * 0.5f;
        for (int k = 0; k < 5; k++)
        {
            float a = turn + k * MathF.Tau / 5;   // Tau = 2 * pi = one full turn; five petals, a fifth of a turn apart
            float px = c.X + MathF.Cos(a) * r * 0.5f, py = c.Y + MathF.Sin(a) * r * 0.5f;
            g.FillEllipse(petalBrush, px - pr, py - pr, pr * 2, pr * 2);
        }
        using var center = new SolidBrush(Color.FromArgb(214, 92, 134));
        float cr = r * 0.17f;
        g.FillEllipse(center, c.X - cr, c.Y - cr, cr * 2, cr * 2);
    }

    /// <summary>Blend two colors: t = 0 gives a, t = 1 gives b.</summary>
    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>
    /// Makes a stencil for something that must pass BEHIND the foreground.
    /// Take a copy of the picture at the moment everything behind the moving
    /// thing has been painted ("before"), finish the picture ("after"), and
    /// compare: a pixel that did not change has nothing in front of it. The
    /// answer is one true/false per pixel, true = still open.
    /// </summary>
    public static bool[] Unchanged(uint[] before, uint[] after)
    {
        var open = new bool[after.Length];
        for (int i = 0; i < open.Length; i++) open[i] = before[i] == after[i];
        return open;
    }

    /// <summary>Copy a finished Bitmap into a plain pixel array (the same 0x00RRGGBB layout as a FrameBuffer).</summary>
    public static uint[] ToPixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var raw = new int[bmp.Width * bmp.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);   // 32-bit rows have no padding, so one copy does it
            var pixels = new uint[raw.Length];
            Buffer.BlockCopy(raw, 0, pixels, 0, raw.Length * 4);
            return pixels;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
