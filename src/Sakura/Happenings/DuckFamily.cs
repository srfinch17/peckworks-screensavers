using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// A mother mallard and her ducklings paddling across the lake in a line.
///
/// The ducklings are round fuzzy yellow balls with tiny orange bills and big
/// dark eyes. They bob on the water, each with its own rhythm, and each draws
/// a small V of wake behind it. One duckling (the last) now and then falls
/// behind, then paddles fast and splashes to catch up.
///
/// How: the mother follows a gently wiggling path. Each duckling follows the
/// SAME path a moment later ("delay"), the way beads follow a thread, which
/// is what makes a line of ducks bend naturally. Everything comes from t.
///
/// Each bird is two sprites, painted once: the bird above the waterline, and
/// the same bird flipped upside down for its reflection (stamped faint). The
/// part of the body below the waterline is simply not painted: the bird sits
/// IN the water. Each exists facing left and right.
/// </summary>
internal sealed class DuckFamily : Happening
{
    private readonly Scenery _s;
    private readonly Sprite[] _hen = new Sprite[4];       // [facing right, facing left, reflection right, reflection left]
    private readonly Sprite[] _duckling = new Sprite[4];
    private readonly int _henWl, _ducklingWl;             // the waterline row inside each sprite

    private bool _ok;
    private float _dir, _y, _x0, _speed, _sp, _meander, _lagStart;
    private int _n;
    private readonly float[] _phase = new float[7];
    private float _sec = 16f;

    public override float Seconds => _sec;
    public override int Layer => 1;
    public override string? Claims => "water";

    public DuckFamily(Scenery s)
    {
        _s = s;
        float lm = Math.Max(16f, s.U * 0.035f), ld = Math.Max(10f, s.U * 0.018f);
        (_hen, _henWl) = Build(lm, 0.62f, PaintHen);
        (_duckling, _ducklingWl) = Build(ld, 0.82f, PaintDuckling);
    }

    private delegate void Painter(Graphics g, float L, float pad, float wl);

    /// <summary>Paints the four sprites of one bird: right, left, and their reflections.</summary>
    private static (Sprite[], int) Build(float L, float wlFrac, Painter paint)
    {
        int pad = 2;
        int w = (int)MathF.Ceiling(L * 1.12f) + 2 * pad;
        int wl = (int)MathF.Ceiling(L * wlFrac) + pad;      // row of the waterline
        int h = wl + 1;
        var set = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            bool left = i % 2 == 1, refl = i >= 2;
            set[i] = Sprite.Paint(w, h, g =>
            {
                // x flips for facing left; y flips about the waterline for the reflection.
                g.Transform = new Matrix(left ? -1 : 1, 0, 0, refl ? -1 : 1, left ? w : 0, refl ? wl : 0);
                paint(g, L, pad, wl);
            });
        }
        return (set, wl);
    }

    /// <summary>The mother: a mallard hen, warm brown with a darker pattern, orange bill, eye stripe.</summary>
    private static void PaintHen(Graphics g, float L, float pad, float wl)
    {
        float X(float f) => pad + f * L;
        float Y(float f) => wl + f * L;
        g.SetClip(new RectangleF(-4000, -4000, 8000, 4000 + wl));      // everything below the waterline is under water
        var fur = Color.FromArgb(160, 118, 76);
        var dark = Color.FromArgb(110, 76, 48);
        var light = Color.FromArgb(206, 168, 122);
        using var furB = new SolidBrush(fur);
        using var darkB = new SolidBrush(dark);
        using var lightB = new SolidBrush(light);
        using var outline = new Pen(Color.FromArgb(120, 84, 54, 34), Math.Max(1f, L * 0.012f));

        g.FillPolygon(furB, [new PointF(X(0.0f), Y(-0.36f)), new PointF(X(0.18f), Y(-0.27f)), new PointF(X(0.18f), Y(-0.08f))]);   // tail, tipped up
        g.FillEllipse(furB, X(0.04f), Y(-0.34f), 0.74f * L, 0.58f * L);                                                             // body
        g.DrawEllipse(outline, X(0.04f), Y(-0.34f), 0.74f * L, 0.58f * L);
        for (int i = 0; i < 9; i++)                                                                                                 // darker speckles
            g.FillEllipse(darkB, X(0.10f + 0.07f * i), Y(-0.31f + 0.06f * ((i * 7) % 5)), 0.026f * L, 0.026f * L);
        using (var wingB = new SolidBrush(Color.FromArgb(132, 94, 60))) g.FillEllipse(wingB, X(0.20f), Y(-0.29f), 0.42f * L, 0.19f * L);                                                             // folded wing
        using (var blue = new SolidBrush(Color.FromArgb(76, 112, 182)))
            g.FillRectangle(blue, X(0.30f), Y(-0.18f), 0.15f * L, 0.035f * L);                                                       // the blue wing patch
        g.FillEllipse(lightB, X(0.60f), Y(-0.48f), 0.28f * L, 0.42f * L);                                                            // neck
        g.FillEllipse(lightB, X(0.68f), Y(-0.60f), 0.30f * L, 0.28f * L);                                                            // a big head
        g.FillPie(darkB, X(0.68f), Y(-0.60f), 0.30f * L, 0.28f * L, 180, 180);                                                       // darker crown
        g.DrawEllipse(outline, X(0.68f), Y(-0.60f), 0.30f * L, 0.28f * L);
        using (var stripe = new Pen(dark, Math.Max(1f, L * 0.024f)))
            g.DrawLine(stripe, X(0.93f), Y(-0.43f), X(0.78f), Y(-0.47f));                                                            // eye stripe
        using (var bill = new SolidBrush(Color.FromArgb(244, 152, 40)))
            g.FillEllipse(bill, X(0.90f), Y(-0.445f), 0.16f * L, 0.08f * L);
        float er = Math.Max(1f, L * 0.034f);
        using (var eye = new SolidBrush(Color.FromArgb(28, 20, 20)))
            g.FillEllipse(eye, X(0.835f) - er, Y(-0.48f) - er, er * 2, er * 2);
        using (var glint = new SolidBrush(Color.White))
            g.FillEllipse(glint, X(0.845f) - er * 0.4f, Y(-0.495f) - er * 0.4f, er * 0.8f, er * 0.8f);
        using var foam = new Pen(Color.FromArgb(110, 255, 255, 255), 1f);
        g.DrawLine(foam, X(0.02f), Y(0), X(0.80f), Y(0));                                                                            // a line of foam where she meets the water
    }

    /// <summary>A duckling: a fuzzy yellow ball with a tiny orange bill, a big dark eye with a glint, and a blush.</summary>
    private static void PaintDuckling(Graphics g, float L, float pad, float wl)
    {
        float X(float f) => pad + f * L;
        float Y(float f) => wl + f * L;
        g.SetClip(new RectangleF(-4000, -4000, 8000, 4000 + wl));
        using var yellow = new SolidBrush(Color.FromArgb(255, 226, 94));
        using var shade = new SolidBrush(Color.FromArgb(242, 192, 52));
        using var outline = new Pen(Color.FromArgb(150, 214, 160, 36), Math.Max(1f, L * 0.05f));
        g.FillEllipse(yellow, X(0.02f), Y(-0.52f), 0.74f * L, 0.64f * L);                       // round body
        g.DrawEllipse(outline, X(0.02f), Y(-0.52f), 0.74f * L, 0.64f * L);
        g.FillEllipse(shade, X(0.10f), Y(-0.38f), 0.34f * L, 0.22f * L);                        // a little wing
        g.FillEllipse(yellow, X(0.34f), Y(-0.80f), 0.56f * L, 0.56f * L);                       // a big round head
        g.DrawEllipse(outline, X(0.34f), Y(-0.80f), 0.56f * L, 0.56f * L);
        g.FillPolygon(yellow, [new PointF(X(0.50f), Y(-0.78f)), new PointF(X(0.56f), Y(-0.90f)), new PointF(X(0.62f), Y(-0.78f))]);   // a tuft of fuzz
        using (var bill = new SolidBrush(Color.FromArgb(250, 140, 40)))
            g.FillPolygon(bill, [new PointF(X(0.86f), Y(-0.56f)), new PointF(X(1.04f), Y(-0.48f)), new PointF(X(0.86f), Y(-0.42f))]);
        using (var blush = new SolidBrush(Color.FromArgb(120, 255, 140, 150)))
            g.FillEllipse(blush, X(0.58f), Y(-0.45f), 0.14f * L, 0.10f * L);
        float er = Math.Max(1f, L * 0.085f);
        using (var eye = new SolidBrush(Color.FromArgb(30, 22, 22)))
            g.FillEllipse(eye, X(0.72f) - er, Y(-0.57f) - er, er * 2, er * 2);
        using (var glint = new SolidBrush(Color.White))
            g.FillEllipse(glint, X(0.75f) - er * 0.4f, Y(-0.60f) - er * 0.4f, er * 0.8f, er * 0.8f);
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width;
        _n = 4 + rng.Next(2);                                  // four or five ducklings
        _speed = u * 0.06f;
        _sp = u * 0.034f;                                      // gap between birds in the line
        _meander = u * 0.006f;
        float travel = _speed * 16f;
        float tail = _n * _sp + u * 0.07f;                     // how far behind the mother the last one starts (plus its lagging)
        var open = _s.OpenBehindBanks;
        _ok = false;
        float best = -1;
        for (int tries = 0; tries < 80; tries++)
        {
            float dir = rng.Next(2) == 0 ? 1 : -1;
            float y = _s.HorizonY + u * (0.08f + 0.055f * (float)rng.NextDouble());   // the lake is only 0.22 U deep and the banks start about 0.11 U down, so ducks swim in the strip above them
            float span = travel + tail;
            if (span > w * 0.96f) break;
            float lo = w * 0.02f + tail, hi = w * 0.98f - travel;
            if (dir < 0) { lo = w * 0.02f + travel; hi = w * 0.98f - tail; }
            if (hi <= lo) continue;
            float x0 = lo + (hi - lo) * (float)rng.NextDouble();
            // The grove's blossom trees hang out over the left of the lake, so a long swim
            // often passes BEHIND a tree (the stencil hides the duck there, which looks right).
            // We only insist that most of the swim is in plain view, and that the middle is.
            int total = 0, seen = 0;
            for (float d = -tail; d <= travel; d += u * 0.03f)
            {
                int ix = (int)(x0 + dir * d), iy = (int)y;
                total++;
                if (ix >= 0 && ix < _s.Width && iy >= 0 && iy < _s.Height && open[iy * _s.Width + ix]) seen++;
            }
            float score = seen / (float)total;
            if (score > best) { best = score; _dir = dir; _y = y; _x0 = x0; }   // keep the best of many tries
        }
        _ok = best >= 0.5f;
        _sec = _ok ? 16f : 0.3f;
        _lagStart = 3f + 3f * (float)rng.NextDouble();
        for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)(rng.NextDouble() * 6.283);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float fade = Fade(t, Seconds, 1.5f, 1.5f);
        if (fade < 0.01f) return;
        float u = _s.U;
        var open = _s.OpenBehindBanks;

        // Draw the little ones first (from the back of the line), the mother last.
        for (int k = _n; k >= 0; k--)
        {
            bool mother = k == 0;
            float delay = k * _sp / _speed;                    // seconds behind the mother: the same path, later
            float tau = t - delay;

            // The last duckling falls behind, then hurries back: out by 0.07 U, then in again.
            float extra = 0, hurry = 0;
            if (k == _n)
            {
                float out1 = Smooth((t - _lagStart) / 1.4f), back = Smooth((t - _lagStart - 1.6f) / 1.6f);
                extra = u * 0.07f * (out1 - back);
                hurry = (t > _lagStart + 1.6f && t < _lagStart + 3.2f) ? 1f : 0f;
            }

            float x = _x0 + _dir * (_speed * tau - extra);
            float y = _y + _meander * (float)Math.Sin(tau * 0.8 + _phase[0]);
            if (!mother) y += u * 0.002f * (float)Math.Sin(t * 1.9 + _phase[k]);     // a little wobble in the line
            // Bobbing on the water: its own rhythm, at least a pixel, faster and bigger when hurrying.
            float amp = Math.Max(mother ? 0.9f : 1.0f, u * (mother ? 0.0012f : 0.0018f)) * (1 + hurry);
            float bob = MathF.Round(amp * (float)Math.Sin(t * (mother ? 2.6 : 3.4 + hurry * 2.5) + _phase[k]));
            int wy = (int)MathF.Round(y + bob);                // one rounded number moves the bird AND its reflection

            var set = mother ? _hen : _duckling;
            int wl = mother ? _henWl : _ducklingWl;
            int left = (int)MathF.Round(x - set[0].Width / 2f);
            int faceIdx = _dir > 0 ? 0 : 1;

            // The wake: a V of two thin lines trailing from the stern, fading away.
            float wakeLen = u * (mother ? 0.07f : 0.028f) * (1 + 0.5f * hurry);
            float stern = x - _dir * set[0].Width * 0.38f;
            float wyf = wy;
            for (int side = -1; side <= 1; side += 2)
                for (int seg = 0; seg < 3; seg++)
                {
                    float f0 = seg / 3f, f1 = (seg + 1) / 3f;
                    var a = new PointF(stern - _dir * wakeLen * f0, wyf + side * wakeLen * 0.16f * f0);
                    var b = new PointF(stern - _dir * wakeLen * f1, wyf + side * wakeLen * 0.16f * f1);
                    fb.Line(a, b, Color.FromArgb(238, 246, 255), 0.5f * (1 - f0) * fade, 1f, open);
                }

            // Reflection (upside down, faint), then the bird.
            set[2 + faceIdx].Draw(fb, left, wy, 0.28f * fade, open);
            set[faceIdx].Draw(fb, left, wy - wl, fade, open);

            // A hurrying duckling kicks up a few tiny splashes behind it.
            if (hurry > 0)
                for (int i = 0; i < 4; i++)
                {
                    float ph = (t * 3.5f + i * 0.25f) % 1f;
                    float px = stern - _dir * (i * 0.15f * u * 0.012f), py = wy - (float)Math.Sin(Math.PI * ph) * u * 0.007f;
                    fb.Line(new PointF(px, py), new PointF(px, py - Math.Max(1.5f, u * 0.002f)), Color.White, 0.8f * fade * (1 - ph), 1f, open);
                }
        }
    }
}
