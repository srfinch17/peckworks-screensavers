using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// Chochin lanterns: a row of red paper lanterns hangs from the bridge's top
/// rail, the way a village strings them up for a summer festival. They start
/// dark, then light one by one from the middle of the bridge outward, each
/// glowing warm orange with a lighter heart, a soft halo, and a long wavering
/// streak of red-gold in the water below. They sway in the breeze, each on
/// its own beat. Then they dim and fade away.
///
/// FEYNMAN VERSION: a chochin is a paper lantern stretched over bamboo hoops.
/// You can see the hoops as thin lines running round it, there is a black
/// lacquered cap at the top and bottom, and a brush-written mark on the front.
/// We paint two pictures of the same lantern, one dark and one lit, ONCE.
/// Lighting it is just stamping the lit picture over the dark one, a little
/// more solid each moment (like turning a dimmer up).
///
/// The reflections cannot be sprites, because they wobble differently on
/// every row (still water is never quite still). Each one is drawn row by row
/// straight into the picture, only on pixels that are open water, so a streak
/// never lands on a bank or the bridge.
/// </summary>
internal sealed class ChochinLanterns : Happening
{
    private const float Total = 18f;
    private const int Count = 7;

    private readonly DuskScenery _s;
    private readonly Sprite _dark, _lit, _halo;
    private readonly int _lw, _lh;                    // the lantern sprite's size
    private readonly PointF[] _hook = new PointF[Count];   // where each cord is tied to the rail
    private readonly float[] _phase = new float[Count], _litAt = new float[Count];
    private readonly float _cord;                     // cord length in pixels

    public override float Seconds => Total;
    public override string? Claims => "bridge";
    public override int Layer => 1;                   // on the bridge, in front of the boat

    public ChochinLanterns(DuskScenery s)
    {
        _s = s;
        float u = s.U;
        _lw = Math.Max(9, (int)(u * 0.028f)) | 1;     // odd, so the middle is a real pixel
        _lh = Math.Max(12, (int)(u * 0.036f));
        _cord = Math.Max(4f, u * 0.012f);
        int tassel = Math.Max(3, _lh / 5);
        _dark = Paint(false, tassel);
        _lit = Paint(true, tassel);
        _halo = Sprite.Glow(Math.Max(8, (int)(u * 0.042f)), Color.FromArgb(255, 150, 64));

        for (int i = 0; i < Count; i++)
        {
            float tt = 0.12f + 0.76f * i / (Count - 1);
            _hook[i] = s.Bridge.RailAt(tt);
            _phase[i] = i * 1.9f;
            // From the middle outward: distance from the middle lantern is the order.
            _litAt[i] = 2.2f + 1.3f * Math.Abs(i - (Count - 1) / 2);
        }
    }

    /// <summary>Paints one lantern, dark (paper unlit) or lit (paper glowing).</summary>
    private Sprite Paint(bool lit, int tassel) => Sprite.Paint(_lw + 4, _lh + tassel + 4, g =>
    {
        float w = _lw, cx = 2 + w / 2f;
        float capH = Math.Max(2f, _lh * 0.12f);
        float bodyTop = 2 + capH * 0.6f, bodyH = _lh - capH * 1.2f;
        float cy = bodyTop + bodyH / 2;

        // The paper: a fat oval. Lit, it is bright in the heart; dark, a dull wine red.
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(2, bodyTop, w, bodyH);
            using var paper = new PathGradientBrush(path)
            {
                CenterColor = lit ? Color.FromArgb(255, 255, 214, 120) : Color.FromArgb(255, 150, 44, 44),
                SurroundColors = [lit ? Color.FromArgb(255, 238, 82, 34) : Color.FromArgb(255, 96, 22, 30)],
            };
            g.FillPath(paper, path);
        }

        // The bamboo hoops: thin dark lines round the belly. Each is as wide as the
        // oval is at that height (a circle slice), so they follow the curve.
        int ribs = 6;
        using (var hoop = new Pen(lit ? Color.FromArgb(120, 150, 30, 16) : Color.FromArgb(120, 50, 8, 16), 1f))
            for (int k = 1; k <= ribs; k++)
            {
                float yy = bodyTop + bodyH * k / (ribs + 1);
                float f = (yy - cy) / (bodyH / 2);
                float half = w / 2f * MathF.Sqrt(Math.Max(0f, 1 - f * f));
                g.DrawArc(hoop, cx - half, yy - 1.2f, half * 2, 2.4f, 0, 180);   // slightly bowed, like a hoop seen from a little above
            }

        // The mark: an abstract brush stroke (a tall stroke, a crossing short one, a dot).
        using (var ink = new Pen(lit ? Color.FromArgb(235, 255, 244, 214) : Color.FromArgb(190, 226, 190, 160), Math.Max(1.4f, w * 0.1f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(ink, cx, cy - bodyH * 0.27f, cx + w * 0.02f, cy + bodyH * 0.27f);
            g.DrawLine(ink, cx - w * 0.2f, cy - bodyH * 0.06f, cx + w * 0.2f, cy - bodyH * 0.1f);
            g.DrawLine(ink, cx - w * 0.15f, cy + bodyH * 0.2f, cx - w * 0.04f, cy + bodyH * 0.08f);
        }

        // Black lacquered caps, and a tassel.
        using var lacquer = new SolidBrush(Color.FromArgb(255, 26, 16, 20));
        float capW = w * 0.5f;
        g.FillRectangle(lacquer, cx - capW / 2, 2, capW, capH);
        g.FillRectangle(lacquer, cx - capW / 2, 2 + _lh - capH, capW, capH);
        using var tas = new Pen(Color.FromArgb(255, 200, 70, 50), Math.Max(1f, w * 0.07f));
        g.DrawLine(tas, cx, 2 + _lh - capH / 2, cx, 2 + _lh + tassel);
    });

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float appear = Fade(t, Total, 1.4f, 1.6f);
        float closing = Smooth((Total - 0.8f - t) / 3.5f);          // the lights dim before the lanterns vanish

        for (int i = 0; i < Count; i++)
        {
            float lit = Smooth((t - _litAt[i]) / 0.9f) * closing;
            // Sway: a slow swing about the hook, a pixel or two at least.
            float swing = MathF.Sin(t * 1.1f + _phase[i]) + 0.4f * MathF.Sin(t * 2.3f + _phase[i] * 2f);
            float len = _cord + _lh / 2f;
            float dx = swing * Math.Max(1.2f, u * 0.0022f);
            PointF hook = _hook[i];
            float lx = hook.X + dx, ly = hook.Y + u * 0.003f + len;          // the lantern's middle
            float flick = 0.94f + 0.06f * MathF.Sin(t * 8f + _phase[i] * 3f);

            if (lit > 0.01f)
            {
                Reflect(fb, lx, ly, lit * appear * flick, _phase[i], t);
                _halo.DrawCentered(fb, lx, ly, 0.55f * lit * appear * flick);
            }
            fb.Line(new PointF(hook.X, hook.Y + u * 0.002f), new PointF(lx, ly - _lh / 2f), Color.FromArgb(255, 30, 18, 22), appear, Math.Max(1f, u * 0.0018f));
            int sx = (int)MathF.Round(lx - _dark.Width / 2f), sy = (int)MathF.Round(ly - _lh / 2f - 2);
            _dark.Draw(fb, sx, sy, appear);
            if (lit > 0.01f) _lit.Draw(fb, sx, sy, lit * appear);
        }
    }

    /// <summary>
    /// The lantern's reflection: a column of red-gold light in the water, row by
    /// row, wavering sideways more the farther it runs, dimmer toward its tail,
    /// broken into shimmering bands. Squashed (a quarter of the real mirror
    /// distance) so even the lantern at the top of the arch has its streak on
    /// the visible pond. Only open water takes it.
    /// </summary>
    private void Reflect(FrameBuffer fb, float x, float lanternY, float amt, float phase, float t)
    {
        float u = _s.U;
        float top = _s.Bridge.WaterLine - u * 0.01f + 0.25f * Math.Max(0f, _s.Bridge.WaterLine - lanternY);
        int len = Math.Max(10, (int)(u * 0.17f));
        float half = Math.Max(2.5f, u * 0.011f);
        int w = fb.Width;
        for (int dy = 0; dy < len; dy++)
        {
            int y = (int)top + dy;
            if (y < 0 || y >= fb.Height) continue;
            float f = dy / (float)len;
            float fall = MathF.Pow(1f - f, 1.4f);
            float wob = MathF.Sin(dy / (u * 0.011f) + t * 1.8f + phase) * u * 0.0045f * (0.3f + f);
            float band = 0.55f + 0.45f * MathF.Sin(dy / (u * 0.0055f) - t * 2.2f + phase * 2f);
            float cx = x + wob;
            // Warmer gold near the top, red as it runs out.
            int r = 255, gg = (int)(190 - 90 * f), b = (int)(96 - 40 * f);
            for (int px = (int)(cx - half); px <= (int)(cx + half); px++)
            {
                if (px < 0 || px >= w) continue;
                if (!_s.OpenWater[y * w + px]) continue;
                float k = 1f - Math.Abs(px - cx) / half;
                float a = 0.9f * amt * fall * band * k;
                if (a < 0.01f) continue;
                fb.Pixels[y * w + px] = FrameBuffer.Blend(fb.Pixels[y * w + px], r, gg, b, a);
            }
        }
    }
}
