using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// A lotus (hasu) opens on the pond. First a round green lily pad fades in on
/// the near water. Then a pink bud rises on its stem beside it and opens,
/// petal by petal, into a full bloom with a yellow seed pod in the middle. It
/// glows a little in the last light, holds open for a few seconds, and fades.
///
/// FEYNMAN VERSION: the bloom is a FLIP-BOOK. We paint twenty little pictures
/// of the flower, page 0 a closed bud and page 19 fully open, each one ONCE at
/// startup. While it plays we just show the page that matches how far along
/// the opening is. Twenty pages (not six) is what makes it look like one
/// smooth unfolding instead of a slideshow.
///
/// Each page is made of three rings of petals (outer, middle, inner), like a
/// real lotus. A petal is a pointed leaf shape that starts at the flower's
/// base and leans outward by an angle. Closed: every petal leans only a few
/// degrees (a bud). Open: the outer ones lean nearly flat. The flower is seen
/// from a little above, so petals at the back are drawn first and petals at
/// the front over them.
///
/// The bloom is a tiny thing (0.04 U), so its look has to read in very few
/// pixels: pale at the base, rose at the tips, a warm glow behind it.
/// </summary>
internal sealed class LotusBloom : Happening
{
    private const int Pages = 20;
    private const float Total = 14f;

    private readonly DuskScenery _s;
    private readonly float _u;
    private readonly int _bloomSize;                  // the open bloom's width in pixels
    private readonly Sprite[] _page = new Sprite[Pages];
    private readonly Sprite _pad, _glow, _puddle;
    private readonly int _stemTop;                    // anchor row inside each page sprite (the flower's base)

    // This showing's spot, rolled in Begin.
    private float _padX, _padY, _bloomX, _bloomY;
    private float _seconds = Total;

    public override float Seconds => _seconds;
    public override string? Claims => "pond";
    public override int Layer => 1;

    public LotusBloom(DuskScenery s)
    {
        _s = s;
        _u = s.U;
        _bloomSize = Math.Max(14, (int)(_u * 0.045f));

        int pw = Math.Max(22, (int)(_u * 0.05f)), ph = Math.Max(9, (int)(pw * 0.40f));
        _pad = PaintPad(pw, ph);

        // Room for the nearest petals to hang BELOW the flower's base (about 0.3 of its width), so none are sliced off.
        int w = (int)(_bloomSize * 1.5f) + 4, h = (int)(_bloomSize * 0.95f) + 8;
        _stemTop = h - (int)(_bloomSize * 0.3f) - 3;
        for (int k = 0; k < Pages; k++)
        {
            float open = k / (float)(Pages - 1);
            _page[k] = Sprite.Paint(w, h, g => PaintBloom(g, w / 2f, _stemTop, _bloomSize, open));
        }
        _glow = Sprite.Glow((int)(_bloomSize * 1.1f), Color.FromArgb(255, 170, 190));

        // The bloom's reflection: a short, flat puddle of pink light.
        int rw = (int)(_bloomSize * 1.4f), rh = Math.Max(6, (int)(_bloomSize * 0.7f));
        _puddle = Sprite.Paint(rw, rh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, rw - 1, rh - 1);
            using var br = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(200, 255, 176, 196),
                SurroundColors = [Color.FromArgb(0, 255, 176, 196)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(br, path);
        });
    }

    // ---------------------------------------------------------------- the lily pad

    /// <summary>
    /// A flat round leaf seen at a slant (so an ellipse), dark green, with a
    /// lighter rim, thin veins, a V-shaped notch cut out of the near edge, a
    /// warm sunset gleam along the far rim, and a faint ring of light on the
    /// water around it.
    /// </summary>
    private static Sprite PaintPad(int pw, int ph)
    {
        int w = pw + 8, h = ph + 8;
        return Sprite.Paint(w, h, g =>
        {
            var leaf = new RectangleF(4, 3, pw, ph);
            float cx = leaf.X + pw / 2f, cy = leaf.Y + ph / 2f;
            using (var ring = new Pen(Color.FromArgb(46, 255, 214, 214), 1f))
                g.DrawEllipse(ring, 1.5f, 1.2f, w - 3f, h - 2f);
            using (var shadow = new SolidBrush(Color.FromArgb(70, 20, 8, 40)))
                g.FillEllipse(shadow, leaf.X, leaf.Y + 1.5f, pw, ph);

            using var path = new GraphicsPath();
            path.AddArc(leaf, 112, 316);                       // most of the way round: the gap is the notch
            path.AddLine(path.GetLastPoint(), new PointF(cx, cy));
            path.CloseFigure();
            using (var fill = new LinearGradientBrush(new PointF(0, leaf.Top), new PointF(0, leaf.Bottom + 1),
                       Color.FromArgb(70, 110, 74), Color.FromArgb(34, 74, 62)))
                g.FillPath(fill, path);
            using (var vein = new Pen(Color.FromArgb(70, 150, 190, 110), 1f))
                for (int i = 0; i < 9; i++)
                {
                    float a = 112 + 316 * (i + 0.5f) / 9f;
                    float rad = a * MathF.PI / 180f;
                    g.DrawLine(vein, cx, cy, cx + MathF.Cos(rad) * pw * 0.46f, cy + MathF.Sin(rad) * ph * 0.46f);
                }
            using (var rim = new Pen(Color.FromArgb(200, 168, 190, 112), MathF.Max(1f, pw * 0.03f)))
                g.DrawPath(rim, path);
            using (var gleam = new Pen(Color.FromArgb(120, 255, 190, 130), 1f))
                g.DrawArc(gleam, leaf, 200, 120);
        });
    }

    // ---------------------------------------------------------------- the bloom (one flip-book page)

    /// <summary>
    /// One page of the flip-book. (bx, by) is where the flower's base is,
    /// "size" the open bloom's width, "open" 0 (bud) to 1 (open).
    /// </summary>
    private static void PaintBloom(Graphics g, float bx, float by, float size, float open)
    {
        float e = open * open * (3 - 2 * open);                 // eased, so the first and last pages change slowly
        float len = size * 0.52f;

        // (petals, length, closed lean, open lean in degrees, first petal's compass angle, width)
        (int N, float L, float A0, float A1, float Rot, float Wd)[] rings =
        [
            (8, 1.00f, 9f, 86f, 0f, 0.62f),     // outer: wide and low when open
            (6, 0.88f, 6f, 62f, 30f, 0.60f),    // middle
            (5, 0.62f, 3f, 52f, 12f, 0.56f),    // inner: more upright, a cup
        ];

        for (int r = 0; r < rings.Length; r++)
        {
            var ring = rings[r];
            float lean = (ring.A0 + (ring.A1 - ring.A0) * e) * MathF.PI / 180f;
            float L = len * ring.L;


            // Back petals first (the compass angle's sine is "how near the viewer").
            var order = Enumerable.Range(0, ring.N).Select(i => (Ang: ring.Rot * MathF.PI / 180f + MathF.Tau * i / ring.N, I: i))
                                  .OrderBy(p => MathF.Sin(p.Ang)).ToArray();
            foreach (var p in order)
            {
                float reach = L * MathF.Sin(lean), rise = L * MathF.Cos(lean);
                // Seen from about 35 degrees above: height is squashed a little, and the near side drops.
                var tip = new PointF(bx + reach * MathF.Cos(p.Ang), by - rise * 0.84f + reach * MathF.Sin(p.Ang) * 0.5f);
                Petal(g, new PointF(bx, by), tip, L * ring.Wd, r);
            }
        }
        // The seed pod sits in the heart of the open flower, over the petals' bases so it shows.
        PaintPod(g, bx, by - len * 0.26f, size, e);
    }

    /// <summary>A pointed petal from base to tip: white at the base, rose at the tip.</summary>
    private static void Petal(Graphics g, PointF b, PointF t, float width, int ring)
    {
        float dx = t.X - b.X, dy = t.Y - b.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        float nx = -dy / len * width / 2, ny = dx / len * width / 2;     // sideways, half a petal wide
        using var path = new GraphicsPath();
        path.AddBezier(b, new PointF(b.X + dx * 0.15f + nx * 1.5f, b.Y + dy * 0.15f + ny * 1.5f),
                          new PointF(b.X + dx * 0.65f + nx * 1.4f, b.Y + dy * 0.65f + ny * 1.4f), t);
        path.AddBezier(t, new PointF(b.X + dx * 0.65f - nx * 1.4f, b.Y + dy * 0.65f - ny * 1.4f),
                          new PointF(b.X + dx * 0.15f - nx * 1.5f, b.Y + dy * 0.15f - ny * 1.5f), b);
        path.CloseFigure();
        using (var fill = new LinearGradientBrush(b, t, Color.FromArgb(255, 252, 244), Color.FromArgb(244, 126, 168))
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(255, 252, 246), Color.FromArgb(255, 226, 232), Color.FromArgb(246, 142, 178)],
                Positions = [0f, 0.45f, 1f],
            }
        })
            g.FillPath(fill, path);
        using var edge = new Pen(Color.FromArgb(ring == 0 ? 120 : 90, 206, 92, 138), 1f);
        g.DrawPath(edge, path);
    }

    /// <summary>The yellow seed pod (a flat-topped cone) with a fringe of golden stamens, visible once the bud has opened.</summary>
    private static void PaintPod(Graphics g, float x, float y, float size, float e)
    {
        if (e < 0.3f) return;
        float a = Math.Min(1f, (e - 0.3f) / 0.4f);
        float r = size * 0.11f;
        using var stamens = new SolidBrush(Color.FromArgb((int)(220 * a), 255, 214, 96));
        g.FillEllipse(stamens, x - r * 1.9f, y - r * 0.9f, r * 3.8f, r * 1.8f);
        using var pod = new SolidBrush(Color.FromArgb((int)(255 * a), 236, 182, 52));
        g.FillEllipse(pod, x - r, y - r * 1.2f, r * 2, r * 1.5f);
    }

    // ---------------------------------------------------------------- the showing

    public override void Begin(Random rng)
    {
        // Find a spot on open near water where the pad AND the flower's whole
        // footprint are open pond (not bridge, not bank). Trust the pixels.
        _seconds = Total;
        float top = _s.Bridge.WaterLine + _u * 0.03f, bottom = _s.Height - _u * 0.05f;
        float pw = _pad.Width, ph = _pad.Height;
        for (int tries = 0; tries < 60; tries++)
        {
            float x = _s.Width * (0.38f + 0.24f * (float)rng.NextDouble());
            float y = top + (bottom - top) * (float)rng.NextDouble();
            float bx = x + pw * 0.62f, by = y - ph * 0.1f;
            if (Open(x - pw / 2, y - ph / 2, pw, ph) && Open(bx - _bloomSize * 0.8f, by - _bloomSize * 1.0f, _bloomSize * 1.6f, _bloomSize * 1.3f))
            {
                _padX = x; _padY = y; _bloomX = bx; _bloomY = by;
                return;
            }
        }
        _seconds = 0.3f;                      // nowhere clear this time: end at once, the director deals another
    }

    /// <summary>True if every sampled pixel of the box is open water.</summary>
    private bool Open(float x, float y, float w, float h)
    {
        for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 4; j++)
            {
                int px = (int)(x + w * i / 4f), py = (int)(y + h * j / 4f);
                if (px < 0 || py < 0 || px >= _s.Width || py >= _s.Height || !_s.OpenWater[py * _s.Width + px]) return false;
            }
        return true;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_seconds < 1f) return;
        // Timeline (14 s): pad fades in 0 to 1.6, bud rises 1.6 to 3.4, opens 3.4 to 9.4, holds, all fades 11.4 to 14.
        float outFade = Smooth((Total - t) / 2.6f);
        float padOn = Smooth(t / 1.6f) * outFade;
        _pad.DrawCentered(fb, _padX, _padY, padOn, _s.OpenWater);

        float rise = Smooth((t - 1.6f) / 1.8f);
        if (rise <= 0) return;
        float openP = Smooth((t - 3.4f) / 6f);
        int k = Math.Clamp((int)MathF.Round(openP * (Pages - 1)), 0, Pages - 1);
        float on = Math.Min(rise, outFade);

        // The bud climbs out of the water on a stem; it ends 0.03 U above the surface.
        float stem = _u * 0.03f * rise;
        float baseY = _bloomY - stem;
        float glowAmt = Smooth((t - 5f) / 4f) * outFade;

        // Reflection under the bloom, brighter as it opens.
        _puddle.DrawCentered(fb, _bloomX, _bloomY + _bloomSize * 0.35f, (0.25f + 0.55f * openP) * on, _s.OpenWater);

        // Stem: a thin dark-green line from the water up to the flower's base.
        fb.Line(new PointF(_bloomX, _bloomY), new PointF(_bloomX, baseY), Color.FromArgb(70, 110, 70), on, MathF.Max(1f, _u * 0.003f), _s.OpenWater);

        _glow.DrawCentered(fb, _bloomX, baseY - _bloomSize * 0.3f, 0.4f * glowAmt, _s.OpenWater);
        Sprite flower = _page[k];
        flower.Draw(fb, (int)MathF.Round(_bloomX - flower.Width / 2f), (int)MathF.Round(baseY) - _stemTop, on, _s.OpenWater);
    }
}
