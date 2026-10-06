using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Two or three small spring butterflies dance round one of the blossom
/// trees in the grove: a pale yellow brimstone (kicho), a white cabbage white
/// (monshirocho) with a black wing tip, and a small blue-violet one. They
/// flutter in from the side, chase each other round the tree in looping
/// spirals, settle on the blossoms for a moment with their wings slowly
/// opening and closing, and flutter off again.
///
/// FEYNMAN VERSION: the wings are a flip-book of 16 pages per species. Think
/// of a butterfly seen from the front and a little above. Page 0: wings held
/// straight up together, a narrow sliver. Page 8: wings flat out to both
/// sides, as wide as they go. Pages in between: the wings opening or closing.
/// Each wing is drawn flat (as if seen from above) and then squashed
/// sideways by how open it is (sin of the opening angle) and lifted by how
/// closed it is (cos), which is all the geometry a flap needs.
///
/// The flight path is a recipe, not a tally (Draw only knows t): a moving
/// centre (it glides from the screen edge to the tree) plus a loop that is
/// two sine waves at different speeds (a sine wave is the smooth back and
/// forth of a swing; two different ones added together trace loops and
/// figure-eights). Each butterfly follows the same recipe a fraction of a
/// second later than the one before, which is what makes them look like
/// they are chasing. Near the end of the chase the loop shrinks and each one
/// is eased onto its own spot on the tree; then it later eases off the
/// screen. The body bobs a little with every wing beat.
/// </summary>
internal sealed class Butterflies : Happening
{
    private const float Total = 12f;
    private const int Pages = 16;

    private readonly Scenery _s;
    private readonly float _k;                    // pixels per drawing unit (the wing's length is 1 unit; a butterfly is about 0.02 U across)
    private readonly int _w, _h, _ox, _oy;
    private readonly Sprite[][] _wing = new Sprite[3][];   // [species][page]
    private readonly int _canopies;

    private bool _ok;
    private int _n;                                       // how many butterflies (2 or 3)
    private readonly int[] _species = new int[3];
    private PointF _centre, _entry, _exit;
    private float _loopR;
    private readonly PointF[] _perch = new PointF[3];
    private readonly float[] _ph = new float[3];

    public override float Seconds => _ok ? Total : 0.1f;
    public override int Layer => 1;

    public Butterflies(Scenery s)
    {
        _s = s;
        _canopies = Math.Min(6, s.Canopies.Count);
        _k = Math.Max(2.6f, s.U * 0.0125f);
        _w = (int)(2.4f * _k) + 8;
        _h = (int)(2.3f * _k) + 8;
        _ox = _w / 2;
        _oy = (int)(1.4f * _k) + 4;
        for (int sp = 0; sp < 3; sp++)
        {
            _wing[sp] = new Sprite[Pages];
            for (int i = 0; i < Pages; i++)
            {
                // Page 0 closed (14 degrees), page 8 wide open (90 degrees), a smooth cosine between.
                float open = 0.5f - 0.5f * MathF.Cos(MathF.Tau * i / Pages);
                _wing[sp][i] = Page(sp, 14f + 76f * open);
            }
        }
    }

    private bool Blossomy(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        if (ix < 0 || iy < 0 || ix >= _s.Width || iy >= _s.Height) return false;
        uint p = _s.Pixels[iy * _s.Width + ix];
        return (int)((p >> 16) & 0xFF) >= (int)(p & 0xFF) + 6;     // pink or white-pink, not leaf, sky or wood
    }

    public override void Begin(Random rng)
    {
        _ok = false;
        if (_canopies == 0) return;
        float u = _s.U, w = _s.Width;
        var (at, r) = _s.Canopies[rng.Next(_canopies)];
        _centre = at;
        _loopR = Math.Max(0.045f * u, r * 0.55f);
        _n = 2 + rng.Next(2);
        // Shuffle the three species so two or three different butterflies come.
        int[] sp = [0, 1, 2];
        for (int i = 2; i > 0; i--) { int j = rng.Next(i + 1); (sp[i], sp[j]) = (sp[j], sp[i]); }
        for (int i = 0; i < 3; i++) _species[i] = sp[i];

        bool left = rng.Next(2) == 0;
        _entry = new PointF(left ? -0.05f * w : 1.05f * w, at.Y - (0.1f + 0.25f * (float)rng.NextDouble()) * u);
        _exit = new PointF(left ? 1.05f * w : -0.05f * w, at.Y - (0.3f + 0.3f * (float)rng.NextDouble()) * u);
        for (int i = 0; i < 3; i++)
        {
            // A spot on the tree with blossom under it; fall back to near the middle.
            PointF p = new(at.X + (i - 1) * r * 0.3f, at.Y - r * 0.2f);
            for (int tries = 0; tries < 30; tries++)
            {
                double a = rng.NextDouble() * Math.Tau, d = Math.Sqrt(rng.NextDouble()) * r * 0.65;
                var c = new PointF(at.X + (float)(Math.Cos(a) * d), at.Y + (float)(Math.Sin(a) * d * 0.8) - r * 0.1f);
                if (Blossomy(c.X, c.Y) && Blossomy(c.X, c.Y + 0.01f * u)) { p = c; break; }
            }
            _perch[i] = p;
            _ph[i] = (float)(rng.NextDouble() * MathF.Tau);
        }
        _ok = true;
    }

    // ================================================================ drawing

    /// <summary>Where butterfly i would be at time t if it never settled: the chase.</summary>
    private PointF Chase(int i, float t)
    {
        float u = _s.U;
        t -= i * 0.45f;                       // each follows the one ahead a little later
        float glide = Smooth(t / 3.2f);       // from the screen edge to the tree
        float cx = _entry.X + (_centre.X - _entry.X) * glide, cy = _entry.Y + (_centre.Y - _entry.Y) * glide;
        // The loop grows as it arrives, and is two sine waves of different speed.
        float grow = Smooth((t - 0.8f) / 2.2f);
        double ta = Math.Max(0, t);
        float ox = _loopR * grow * (float)(Math.Sin(ta * 2.3 + 0.7) + 0.35 * Math.Sin(ta * 5.1));
        float oy = _loopR * 0.75f * grow * (float)(Math.Sin(ta * 3.1) + 0.3 * Math.Cos(ta * 4.4 + 1.0));
        // Plus a little flutter of its own on the way in.
        float flut = 0.012f * u * (float)Math.Sin(ta * 9.0 + i * 2.0) * (1 - Smooth((t - 3f) / 1f));
        return new PointF(cx + ox, cy + oy + flut);
    }

    private PointF Where(int i, float t)
    {
        float u = _s.U;
        const float settle0 = 6.2f, settle1 = 7.6f, leave0 = 9.4f, leave1 = 12.0f;
        PointF ch = Chase(i, MathF.Min(t, settle0 + 1.5f));
        float a = Smooth((t - settle0 - i * 0.2f) / (settle1 - settle0));
        PointF p = new(ch.X + (_perch[i].X - ch.X) * a, ch.Y + (_perch[i].Y - ch.Y) * a);
        if (t > leave0)
        {
            float q = Smooth((t - leave0 - i * 0.25f) / (leave1 - leave0 - 0.5f));
            float wob = MathF.Sin(q * 13f + i * 1.9f) * 0.03f * u * MathF.Sin(MathF.PI * Math.Min(1f, q));
            p = new PointF(_perch[i].X + (_exit.X - _perch[i].X) * q, _perch[i].Y + (_exit.Y - _perch[i].Y) * q + wob);
        }
        return p;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float u = _s.U;
        for (int i = 0; i < _n; i++)
        {
            PointF p = Where(i, t);
            bool perched = t > 7.6f + i * 0.2f && t < 9.4f + i * 0.25f;
            int page;
            float bob;
            if (perched)
            {
                // Slowly open and close, only part way (pages 0 to 7), once every 2.2 seconds.
                float c = 0.5f - 0.5f * MathF.Cos(MathF.Tau * (t - i * 0.3f) / 2.2f);
                page = (int)MathF.Round(c * 7f);
                bob = 0;
            }
            else
            {
                float hz = 7f + 1.2f * i;
                double ph = (double)(t * hz) + _ph[i];
                page = ((int)Math.Floor(ph * Pages) % Pages + Pages) % Pages;
                // The body dips as the wings come down (page 8 is wide open, the middle of the downstroke).
                bob = MathF.Max(1f, 0.0035f * u) * MathF.Cos(MathF.Tau * (float)(ph % 1.0));
            }
            // Fade the very first and last moments (they are off screen then anyway).
            _wing[_species[i]][page].Draw(fb, (int)MathF.Round(p.X - _ox), (int)MathF.Round(p.Y + bob - _oy));
        }
    }

    // ================================================================ painting one page

    /// <summary>One butterfly with its wings opened to angle a (degrees): 14 is closed up, 90 is flat out.</summary>
    private Sprite Page(int species, float a)
    {
        float sa = MathF.Max(0.1f, MathF.Sin(a * MathF.PI / 180f)), ca = MathF.Cos(a * MathF.PI / 180f);
        Color body, edge, spot, tip;
        Color fore, hind;
        switch (species)
        {
            case 0: fore = Color.FromArgb(244, 236, 112); hind = Color.FromArgb(238, 224, 92); edge = Color.FromArgb(176, 158, 56); spot = Color.FromArgb(236, 150, 50); tip = Color.Transparent; break;
            case 1: fore = Color.FromArgb(252, 252, 248); hind = Color.FromArgb(246, 246, 238); edge = Color.FromArgb(120, 124, 136); spot = Color.FromArgb(60, 60, 66); tip = Color.FromArgb(40, 40, 48); break;
            default: fore = Color.FromArgb(120, 130, 238); hind = Color.FromArgb(160, 170, 250); edge = Color.FromArgb(46, 50, 130); spot = Color.FromArgb(226, 230, 255); tip = Color.FromArgb(70, 76, 170); break;
        }
        body = Color.FromArgb(48, 38, 40);

        return Sprite.Paint(_w, _h, g =>
        {
            g.TranslateTransform(_ox, _oy);
            g.ScaleTransform(_k, _k);

            PointF M(float uu, float v, int side) => new(side * uu * sa, v - uu * ca * 0.55f);
            PointF[] Fore = [new(0.05f, -0.1f), new(0.5f, -0.78f), new(1.0f, -0.72f), new(1.06f, -0.3f), new(0.8f, 0.05f), new(0.1f, 0.1f)];
            PointF[] Hind = [new(0.05f, 0.0f), new(0.7f, 0.05f), new(0.98f, 0.42f), new(0.7f, 0.78f), new(0.3f, 0.72f), new(0.05f, 0.3f)];

            for (int side = -1; side <= 1; side += 2)
            {
                // Hind wing first (it is behind), then the fore wing over it.
                foreach (var (poly, col, isFore) in new[] { (Hind, hind, false), (Fore, fore, true) })
                {
                    PointF[] pts = poly.Select(q => M(q.X, q.Y, side)).ToArray();
                    using (var b = new SolidBrush(col)) g.FillClosedCurve(b, pts, FillMode.Winding, 0.35f);
                    if (isFore && tip.A > 0)
                    {
                        // Dark wing tip (cabbage white's black corner; the blue one's darker rim).
                        PointF[] t = [M(0.55f, -0.72f, side), M(1.0f, -0.72f, side), M(1.06f, -0.3f, side), M(0.95f, -0.45f, side)];
                        using var tb = new SolidBrush(tip);
                        g.FillClosedCurve(tb, t, FillMode.Winding, 0.4f);
                    }
                    using (var pen = new Pen(Color.FromArgb(200, edge), 0.07f) { LineJoin = LineJoin.Round })
                        g.DrawClosedCurve(pen, pts, 0.35f, FillMode.Winding);
                    // One spot on each wing: orange dot (brimstone), grey spot (cabbage white), pale dot (blue).
                    PointF sp = isFore ? M(0.6f, -0.3f, side) : M(0.55f, 0.35f, side);
                    float rr = isFore ? 0.1f : 0.07f;
                    using (var sb = new SolidBrush(Color.FromArgb(species == 2 ? 200 : 230, spot)))
                        g.FillEllipse(sb, sp.X - rr * sa, sp.Y - rr, rr * 2 * sa, rr * 2);
                }
            }

            // Body: a slim dark oval with a round head and two feelers.
            using (var bb = new SolidBrush(body))
            {
                g.FillEllipse(bb, -0.07f, -0.45f, 0.14f, 1.1f);
                g.FillEllipse(bb, -0.1f, -0.58f, 0.2f, 0.2f);
            }
            using (var pen = new Pen(body, 0.04f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, -0.04f, -0.56f, -0.2f, -0.85f);
                g.DrawLine(pen, 0.04f, -0.56f, 0.2f, -0.85f);
            }
        });
    }
}
