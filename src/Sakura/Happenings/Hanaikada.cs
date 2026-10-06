using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Hanaikada, "flower raft": cherry petals that have fallen on the water and
/// drifted together into a long pink ribbon, slowly carried along by the
/// current. In Japan people go to see them as much as the blossoms.
///
/// How: about 170 petals. Each has a spot along an invisible curving "spine"
/// (how far along the ribbon) and a sideways offset from it (how far from the
/// middle), both rolled in Begin. In Draw we work out where the spine is right
/// now (it slowly wiggles, and the whole raft slides along) and put each
/// petal at its place beside it. Think of beads on a wobbling string.
///
/// A petal is a small flat pink oval with a notch, seen at a low angle (so
/// squashed to about half its height). We paint 16 turns of it, 3 shades and
/// 2 sizes ahead of time (a flip-book) and just stamp the right page, so a
/// petal can turn slowly. Petals on the far edge of the ribbon are smaller
/// and fainter, which makes the ribbon look like it lies on the water.
/// </summary>
internal sealed class Hanaikada : Happening
{
    private const int PetalCount = 220;
    private const int Pages = 16;                 // turns of one petal in the flip-book

    private readonly Scenery _s;
    private readonly Sprite[,,] _petal = new Sprite[3, 2, Pages];   // [shade, size (0 big, 1 small), turn]
    private readonly Sprite _spark;

    // Rolled in Begin.
    private readonly float[] _u = new float[PetalCount];        // 0..1 along the ribbon
    private readonly float[] _v = new float[PetalCount];        // -1 (far edge) .. 1 (near edge) across it
    private readonly float[] _jx = new float[PetalCount];       // little scatter so it is not a perfect line
    private readonly float[] _turn0 = new float[PetalCount], _turnRate = new float[PetalCount], _phase = new float[PetalCount];
    private readonly int[] _shade = new int[PetalCount];
    private bool _ok;
    private float _cx0, _cy, _dir, _wave, _sec = 18f;

    public override float Seconds => _sec;
    public override int Layer => 1;
    public override string? Claims => "water";

    public Hanaikada(Scenery s)
    {
        _s = s;
        var shades = new[] { Color.FromArgb(255, 228, 234), Color.FromArgb(250, 192, 208), Color.FromArgb(234, 146, 174) };
        for (int c = 0; c < 3; c++)
            for (int z = 0; z < 2; z++)
            {
                float len = Math.Max(z == 0 ? 4f : 3f, s.U * (z == 0 ? 0.011f : 0.0075f));
                int size = (int)MathF.Ceiling(len) + 3;
                for (int k = 0; k < Pages; k++)
                {
                    float ang = k * MathF.Tau / Pages;
                    _petal[c, z, k] = Sprite.Paint(size, size, g => PaintPetal(g, size, len, ang, shades[c]));
                }
            }
        _spark = Sprite.Glow(Math.Max(2, (int)(s.U * 0.004f)), Color.White);
    }

    /// <summary>One petal: a rounded oval with a tiny V cut in its tip, turned by ang and squashed flat.</summary>
    private static void PaintPetal(Graphics g, int size, float len, float ang, Color c)
    {
        g.TranslateTransform(size / 2f, size / 2f);
        g.ScaleTransform(1f, 0.55f);                       // lying on water, seen from a low angle
        g.RotateTransform(ang * 180f / MathF.PI);
        float l = len;
        using var path = new GraphicsPath();
        path.AddClosedCurve([
            new PointF(-0.50f * l, 0), new PointF(-0.22f * l, -0.30f * l), new PointF(0.20f * l, -0.30f * l), new PointF(0.50f * l, -0.17f * l),
            new PointF(0.38f * l, 0), new PointF(0.50f * l, 0.17f * l), new PointF(0.20f * l, 0.30f * l), new PointF(-0.22f * l, 0.30f * l)], 0.5f);
        using var fill = new SolidBrush(c);
        g.FillPath(fill, path);
        // a deeper pink vein from the base, the way a real petal is darker where it joins the flower
        using var vein = new Pen(Color.FromArgb(120, Math.Max(0, c.R - 25), Math.Max(0, c.G - 45), Math.Max(0, c.B - 35)), 1f);
        g.DrawLine(vein, -0.45f * l, 0, 0.05f * l, 0);
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, w = _s.Width, len = u * 0.35f;
        float drift = u * 0.025f * 18f;                    // how far the whole raft slides in 18 s
        var open = _s.OpenBehindBanks;
        _ok = false;
        // Find a depth and a start where most of the raft's path is open lake.
        // "Most", not "all": the lake between the grove and the lone tree is
        // narrow on a 16:9 screen, and demanding every sample be open water
        // left the raft with nowhere to go four times in five. Petals are
        // only stamped on open water anyway, so a raft that drifts behind a
        // tree's canopy for a while simply passes behind it. Keep the best
        // of 60 tries, and settle for it if it is at least 70% open.
        float best = 0;
        for (int tries = 0; tries < 60; tries++)
        {
            float dir = rng.Next(2) == 0 ? 1 : -1;
            float cy = _s.HorizonY + u * (0.07f + 0.11f * (float)rng.NextDouble());   // the lake is only 0.22 U deep, banks eat its lower part
            float lo = len / 2 + u * 0.03f, hi = w - len / 2 - drift - u * 0.03f;
            if (hi <= lo) break;
            float cx = lo + (hi - lo) * (float)rng.NextDouble();
            float startX = dir > 0 ? cx : cx + drift;      // the centre where it starts
            int good = 0, all = 0;
            for (int k = 0; k <= 8; k++)
                for (int e = -1; e <= 1; e++)              // both ends and the middle of the ribbon
                    for (int r = -1; r <= 1; r++)          // and a row above and below
                    {
                        float x = startX + dir * drift * k / 8f + e * len / 2;
                        float y = cy + r * u * 0.035f;
                        int ix = (int)x, iy = (int)y;
                        all++;
                        if (ix >= 0 && ix < _s.Width && iy >= 0 && iy < _s.Height && open[iy * _s.Width + ix]) good++;
                    }
            float share = good / (float)all;
            if (share > best) { best = share; _cx0 = startX; _cy = cy; _dir = dir; }
        }
        _ok = best >= 0.7f;
        _sec = _ok ? 18f : 0.3f;                           // no clear water: end at once
        _wave = (float)(rng.NextDouble() * 6.283);
        for (int i = 0; i < PetalCount; i++)
        {
            _u[i] = (float)rng.NextDouble();
            _v[i] = ((float)rng.NextDouble() + (float)rng.NextDouble()) - 1f;   // two dice summed: bunched toward the middle
            _jx[i] = ((float)rng.NextDouble() - 0.5f) * 0.012f;
            _turn0[i] = (float)(rng.NextDouble() * 6.283);
            _turnRate[i] = (float)((rng.NextDouble() - 0.5) * 0.7);
            _phase[i] = (float)(rng.NextDouble() * 6.283);
            double sh = rng.NextDouble();
            _shade[i] = sh < 0.38 ? 0 : sh < 0.8 ? 1 : 2;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;
        float fade = Fade(t, Seconds, 3f, 3f);
        if (fade < 0.01f) return;
        float u = _s.U, len = u * 0.35f;
        var open = _s.OpenBehindBanks;
        float cx = _cx0 + _dir * u * 0.025f * t;           // the current carries the whole raft
        double tt = t;

        for (int i = 0; i < PetalCount; i++)
        {
            float f = _u[i];
            // The ribbon's shape: a gentle S that slowly wiggles, thinning to a point at both ends.
            float spine = u * 0.016f * (float)Math.Sin(f * 5.4 + _wave + tt * 0.35) + u * 0.006f * (float)Math.Sin(f * 12.0 + tt * 0.5)
                        + (f - 0.5f) * u * 0.02f;
            float ends = 0.12f + 0.88f * MathF.Pow((float)Math.Sin(Math.PI * (0.03 + 0.94 * f)), 0.7f);
            float body = ends * (0.75f + 0.25f * (float)Math.Sin(f * 7.0 + _wave));
            float x = cx + (f - 0.5f) * len + _jx[i] * u;
            float y = _cy + spine + _v[i] * u * 0.026f * body;
            y += (float)Math.Sin(tt * 0.9 + _phase[i]) * Math.Max(0.6f, u * 0.0007f);   // each petal bobs on the ripples

            bool far = _v[i] < -0.25f;
            float alpha = fade * (far ? 0.6f : 0.92f);
            double ang = _turn0[i] + _turnRate[i] * tt;
            int page = ((int)Math.Floor(ang / (Math.PI * 2) * Pages) % Pages + Pages) % Pages;
            var sp = _petal[_shade[i], far ? 1 : 0, page];
            int px = (int)MathF.Round(x - sp.Width / 2f), py = (int)MathF.Round(y - sp.Height / 2f);
            int cxp = px + sp.Width / 2, cyp = py + sp.Height / 2;
            if (cxp < 0 || cxp >= fb.Width || cyp < 0 || cyp >= fb.Height || !open[cyp * fb.Width + cxp] || cyp <= _s.HorizonY) continue;
            sp.Draw(fb, px, py, alpha, open);

            // A few petals catch the sun: a twinkle that pulses on and off.
            if (i % 14 == 0)
            {
                float pulse = MathF.Pow(Math.Max(0f, (float)Math.Sin(tt * 1.7 + _phase[i] * 3)), 14);
                if (pulse > 0.05f) _spark.DrawCentered(fb, x, y, pulse * 0.85f * fade);
            }
        }
    }
}
