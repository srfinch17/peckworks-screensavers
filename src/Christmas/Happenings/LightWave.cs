using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A wave of brightness runs through every light in the scene, like a
/// stadium wave: a bright front sweeps from the left edge to the right edge,
/// then back again. As the front passes a bulb, the bulb swells with extra
/// light in its own pastel color and a small white sparkle flashes at its
/// crest, then it settles back.
///
/// How bright a bulb is depends only on how far it is (sideways) from the
/// front: exp(-(dx / width)^2). That is a "bell curve", 1 right under the
/// front and falling smoothly to nothing on either side. Only bulbs near
/// the front are drawn at all, so 400 bulbs cost the same as 40.
///
/// Stamps: one soft Glow per pastel color (shared by every bulb of that
/// color) and one sparkle sprite (two thin white diamonds crossed, like a
/// glint on glass). The scene's own twinkle glows are drawn after this, over
/// it; that is fine, it only adds light.
/// </summary>
internal sealed class LightWave : Happening
{
    private const float Leg = 3.8f;                         // seconds for one sweep across

    private readonly ChristmasScenery _s;
    private readonly Sprite[] _glow;                        // one per pastel
    private readonly Sprite _sparkle;

    public override float Seconds => 8f;
    public override string? Claims => "lights";

    public LightWave(ChristmasScenery s)
    {
        _s = s;
        int r = Math.Max(3, (int)(s.U * 0.016f));
        _glow = ChristmasPainter.Pastels.Select(c => Sprite.Glow(r, c)).ToArray();

        int size = Math.Max(7, (int)(s.U * 0.03f)) | 1;     // odd, so there is a true middle pixel
        float c = size / 2f, thin = Math.Max(0.8f, size * 0.05f);
        _sparkle = Sprite.Paint(size, size, g =>
        {
            using var ray = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
            g.FillPolygon(ray, [new PointF(0, c), new PointF(c, c - thin), new PointF(size, c), new PointF(c, c + thin)]);
            g.FillPolygon(ray, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, size), new PointF(c - thin, c)]);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 0.5f, 0.5f);

        // Where the front is: out and back. It starts and ends a little past
        // the edges so it enters and leaves the screen fully.
        float margin = _s.U * 0.2f, run = _s.Width + 2 * margin;
        float k = Math.Min(t / Leg, 2f);
        float along = k <= 1 ? k : 2 - k;                   // 0 to 1 and back to 0
        float front = -margin + run * along;

        // Only bulbs near the front are stamped, and only while the stamp
        // would show: there are over four hundred bulbs, and on a 4K screen
        // stamping every one within reach cost more than the whole scene.
        float width = _s.U * 0.08f, reach = _s.U * 0.14f;
        foreach (var (at, color) in _s.Lights)
        {
            float dx = at.X - front;
            if (MathF.Abs(dx) > reach) continue;            // too far from the front to matter
            float b = MathF.Exp(-(dx / width) * (dx / width));
            if (b * on < 0.06f) continue;                   // too faint to see; skip the stamp
            _glow[color].DrawCentered(fb, at.X, at.Y, b * on);
            if (b > 0.35f) _glow[color].DrawCentered(fb, at.X, at.Y, b * on);   // a second stamp on top: one alone is too faint against dark green needles
            if (b > 0.4f) _sparkle.DrawCentered(fb, at.X, at.Y, (b - 0.4f) / 0.6f * on);   // a glint only at the crest
        }
    }
}
