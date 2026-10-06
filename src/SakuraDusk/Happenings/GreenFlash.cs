using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// The green flash: just as the sun sinks, its top edge turns emerald green
/// for a second or two, flickers, and is gone. It is real (the air bends
/// green light a little more than red, like a weak prism, so for a moment
/// the last sliver of sun is green) and rare enough that seeing one feels
/// like luck.
///
/// Three stamps, all through the OpenSky stencil so the hills and the
/// branches stay in front: a soft green glow above the sun, a thin green
/// crescent lying along the sun's top edge, and a short bright streak
/// standing up from the middle of that crescent, the "flash" itself.
///
/// Where the sun is comes from the painter (Sun: center and radius). The
/// sun's own disc is part of the sky picture, so the stencil lets us paint
/// over it.
/// </summary>
internal sealed class GreenFlash : Happening
{
    private static readonly Color Emerald = Color.FromArgb(96, 255, 150);

    private readonly DuskScenery _s;
    private readonly Sprite _glow, _rim, _streak;
    private readonly int _rimLeft, _rimTop;           // where the rim sprite goes, in whole pixels (rule 11)

    public override float Seconds => 4.5f;
    public override string? Claims => "sun";

    public GreenFlash(DuskScenery s)
    {
        _s = s;
        float r = s.Sun.R;
        PointF c = s.Sun.At;
        _glow = Sprite.Glow((int)(r * 1.1f), Emerald);

        // The crescent: the top of a circle the sun's size, minus the same
        // circle moved down a little. What is left is a thin curved sliver
        // that hugs the sun's rim, thickest in the middle (about 7% of the
        // radius, never under 2 pixels) and thinning to nothing at the ends.
        float thick = Math.Max(2f, r * 0.07f);
        _rimLeft = (int)MathF.Floor(c.X - r) - 2;
        _rimTop = (int)MathF.Floor(c.Y - r) - 2;
        int w = (int)MathF.Ceiling(2 * r) + 6, h = (int)MathF.Ceiling(r + thick) + 6;   // tall enough for the sliver to taper to points at its ends
        float cx = c.X - _rimLeft, cy = c.Y - _rimTop;   // the sun's center inside the sprite, at the same fractional offset
        _rim = Sprite.Paint(w, h, g =>
        {
            using var top = new GraphicsPath();
            top.AddEllipse(cx - r - 0.5f, cy - r - 0.5f, 2 * r + 1, 2 * r + 1);
            using var cut = new GraphicsPath();
            cut.AddEllipse(cx - r, cy - r + thick, 2 * r, 2 * r);
            using var sliver = new Region(top);
            sliver.Exclude(cut);
            using var green = new SolidBrush(Color.FromArgb(225, Emerald));
            g.FillRegion(green, sliver);
        });

        // The flash: a short vertical streak, bright in the middle and fading
        // up and down (a tall thin ellipse with a path gradient).
        int sw = Math.Max(3, (int)(r * 0.10f)), sh = Math.Max(8, (int)(r * 0.9f));
        _streak = Sprite.Paint(sw, sh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, sw, sh);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(255, 220, 255, 230),
                SurroundColors = [Color.FromArgb(0, Emerald)],
            };
            g.FillPath(brush, path);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 0.6f, 1.2f);
        // Real green flashes shimmer: two quick waves of different speeds.
        float shimmer = 0.75f + 0.15f * MathF.Sin(t * 23f) + 0.10f * MathF.Sin(t * 41f);
        // The bright moment: a spike in the middle of the showing.
        float flash = MathF.Exp(-MathF.Pow((t - 1.9f) / 0.35f, 2));

        PointF c = _s.Sun.At;
        float r = _s.Sun.R;
        _glow.DrawCentered(fb, c.X, c.Y - r * 0.85f, on * 0.55f * shimmer, _s.OpenSky);
        _rim.Draw(fb, _rimLeft, _rimTop, on * shimmer, _s.OpenSky);
        _streak.DrawCentered(fb, c.X, c.Y - r * 1.05f, flash * on, _s.OpenSky);
    }
}
