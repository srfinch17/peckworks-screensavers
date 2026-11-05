using System.Drawing.Drawing2D;

namespace Thanksgiving;

/// <summary>
/// How a field mouse is drawn, shared by the acorn caper's thief and the mice
/// that live in the pumpkin patch. Everything is in mouse units (its body
/// length), x forward, y UP, the origin at the feet on the ground; the caller
/// sets up that frame.
/// </summary>
internal static class MouseArt
{
    /// <summary>
    /// A small warm-grey field mouse scurrying to the right (phase 0 to 1 of a stride): a plump body that bobs and
    /// stretches a little with each stride, a big pink-lined ear, a pink nose, a long tapered tail that lags the body,
    /// and four quick little legs. "twitch" 1 or 2 is the standing mouse with its nose up and down (sniffing); "nut"
    /// puts an acorn in its mouth.
    /// </summary>
    public static void Paint(Graphics g, float phase, bool nut, int twitch)
    {
        Color fur = Color.FromArgb(150, 126, 102), dark = Color.FromArgb(104, 84, 68), belly = Color.FromArgb(226, 206, 180), pink = Color.FromArgb(226, 156, 150), rim = Color.FromArgb(80, 60, 52);
        bool still = twitch > 0;
        float run = still ? 0f : MathF.Sin(phase * MathF.Tau);
        float bob = still ? 0f : 0.04f * MathF.Max(0f, MathF.Sin(phase * MathF.Tau * 2)), stretch = still ? 1f : 1f + 0.05f * MathF.Sin(phase * MathF.Tau);
        float lag = still ? 0f : MathF.Sin(phase * MathF.Tau - 1.1f);  // the tail wave three frames behind the body
        // The tail: a tapered closed shape (not a pen line) that waves as it runs.
        {
            PointF[] c = new PointF[7];
            for (int i = 0; i < 7; i++) { float s = i / 6f; c[i] = new PointF(-0.36f - 0.90f * s, 0.20f + bob + 0.10f * MathF.Sin(s * 4.5f + lag * 1.2f) * s); }
            var left = new PointF[7]; var right = new PointF[7];
            for (int i = 0; i < 7; i++)
            {
                PointF a = c[Math.Max(0, i - 1)], b = c[Math.Min(6, i + 1)];
                float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, hw = 0.035f * (1 - i / 6f) + 0.006f;
                left[i] = new PointF(c[i].X - dy / l * hw, c[i].Y + dx / l * hw); right[i] = new PointF(c[i].X + dy / l * hw, c[i].Y - dx / l * hw);
            }
            var outline = new PointF[14];
            for (int i = 0; i < 7; i++) { outline[i] = left[i]; outline[13 - i] = right[i]; }
            using var tb = new SolidBrush(dark);
            g.FillPolygon(tb, outline);
        }
        // The legs: two pairs, scurrying out of step.
        using (var leg = new Pen(dark, 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(leg, 0.22f, 0.16f + bob, 0.22f + 0.14f * run, 0.0f);
            g.DrawLine(leg, -0.22f, 0.16f + bob, -0.22f - 0.14f * run, 0.0f);
            g.DrawLine(leg, 0.30f, 0.16f + bob, 0.30f - 0.14f * run, 0.02f);
            g.DrawLine(leg, -0.14f, 0.16f + bob, -0.14f + 0.14f * run, 0.02f);
        }
        using (var body = new SolidBrush(fur)) g.FillEllipse(body, -0.42f * stretch, 0.04f + bob, 0.84f * stretch, 0.40f);
        using (var bl = new SolidBrush(belly)) g.FillEllipse(bl, -0.30f * stretch, 0.04f + bob, 0.56f * stretch, 0.18f);
        float nose = still ? (twitch == 2 ? 0.03f : 0f) : 0f;         // the nose twitches up and down when it sniffs
        using (var head = new SolidBrush(fur)) g.FillEllipse(head, 0.26f, 0.12f + bob + nose, 0.36f, 0.32f);
        using (var earRim = new SolidBrush(rim)) g.FillEllipse(earRim, 0.24f, 0.33f + bob + nose, 0.26f, 0.26f);
        using (var ear = new SolidBrush(fur)) g.FillEllipse(ear, 0.26f, 0.35f + bob + nose, 0.22f, 0.22f);
        using (var inner = new SolidBrush(pink)) g.FillEllipse(inner, 0.30f, 0.38f + bob + nose, 0.14f, 0.15f);
        using (var eye = new SolidBrush(Color.FromArgb(24, 18, 18))) g.FillEllipse(eye, 0.46f, 0.28f + bob + nose, 0.07f, 0.07f);
        using (var np = new SolidBrush(pink)) g.FillEllipse(np, 0.59f, 0.22f + bob + nose, 0.07f, 0.06f);
        using (var wh = new Pen(Color.FromArgb(140, 230, 226, 230), 0.015f))
            for (int i = -1; i <= 1; i++) g.DrawLine(wh, 0.58f, 0.24f + bob + nose, 0.80f, 0.20f + bob + nose + i * 0.07f);
        if (nut)
        {
            // The acorn, as big as its head, carried in its mouth (the same nut that lay on the ground).
            var st = g.Save();
            g.TranslateTransform(0.78f, 0.22f + bob + nose);
            g.RotateTransform(70f);
            float a = 0.165f;
            using (var b = new SolidBrush(Color.FromArgb(150, 98, 46))) g.FillEllipse(b, -a * 1.3f, -a, a * 2.6f, a * 2f);
            using (var cap = new SolidBrush(Color.FromArgb(84, 54, 28))) g.FillPie(cap, -a, -a * 1.2f, a * 2f, a * 2.4f, 70, 220);
            g.Restore(st);
        }
    }
}
