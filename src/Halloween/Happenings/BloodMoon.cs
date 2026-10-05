using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// The moon slowly turns blood red and glows red, then returns to normal.
///
/// Two stamps, glow first, disc second:
///   1. A wide red glow (3 moon-radii), faint, so the sky around the moon
///      blushes. It is stamped through the OpenSky stencil, so bare branches
///      in front of the moon stay dark and in front.
///   2. A red disc that covers the moon exactly. It is only about half
///      strength (0.55), so the moon's pale seas still show through it, like
///      a red lantern glass held in front of a candle.
///
/// "Exactly" matters: the moon was painted at a fractional pixel position,
/// and a disc one pixel off leaves a pale sliver on one edge. So (rule 11 of
/// docs/ADDING_HAPPENINGS.md) the sprite gets a whole-pixel left and top, and
/// the ellipse is painted inside it at the moon's true fractional center.
/// </summary>
internal sealed class BloodMoon : Happening
{
    private readonly Sprite _glow, _disc;
    private readonly bool[] _stencil;
    private readonly PointF _glowAt;
    private readonly int _left, _top;

    public override float Seconds => 12f;
    public override string? Claims => "moon";

    public BloodMoon(HalloweenScenery s)
    {
        _stencil = MoonStencil(s);
        float r = s.Moon.R;
        // The red haze around the moon. This is a big stamp, and every pixel
        // of a stamp costs time, so it is cut down two ways. It reaches only
        // 1.8 moon-widths out (at 3 it took more time than the whole rest of
        // the scene on a 4K screen). And it is a RING: the middle is punched
        // out, because the red disc below covers the moon itself anyway.
        // Clear pixels cost almost nothing to stamp.
        int glowR = Math.Max(4, (int)(r * 1.8f));
        _glow = Sprite.Paint(glowR * 2, glowR * 2, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, glowR * 2, glowR * 2);
            using var haze = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(255, 200, 10, 5),
                SurroundColors = [Color.FromArgb(0, 200, 10, 5)],
                // Strong right at the moon's edge (0.44 of the way in from the
                // rim), easing to nothing at the rim so no ring shows.
                Blend = new Blend { Positions = [0f, 0.2f, 0.44f, 1f], Factors = [0f, 0.22f, 0.85f, 1f] },
            };
            g.FillPath(haze, path);
            // Punch the hole. "SourceCopy" means "replace what is there"
            // instead of "paint over it", so painting clear really clears.
            g.CompositingMode = CompositingMode.SourceCopy;
            float hole = r - 2;                                             // a little inside the moon's edge, so ring and disc overlap
            using var clear = new SolidBrush(Color.Transparent);
            if (hole > 1) g.FillEllipse(clear, glowR - hole, glowR - hole, hole * 2, hole * 2);
        });
        _glowAt = s.Moon.At;

        _left = (int)MathF.Floor(s.Moon.At.X - r) - 2;
        _top = (int)MathF.Floor(s.Moon.At.Y - r) - 2;
        int size = (int)MathF.Ceiling(r * 2) + 6;
        float cx = s.Moon.At.X - _left, cy = s.Moon.At.Y - _top;
        _disc = Sprite.Paint(size, size, g =>
        {
            // A hair wider than the moon (0.4 px) so the moon's soft rim is fully covered.
            float rr = r + 0.4f;
            using var red = new SolidBrush(Color.FromArgb(140, 8, 6));
            g.FillEllipse(red, cx - rr, cy - rr, rr * 2, rr * 2);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 4f, 4f);                                // in 4 s, hold 4 s, out 4 s
        // During the hold, a slow pulse (one beat every 3 seconds), like a heart.
        float pulse = 1 + 0.12f * MathF.Sin(t * MathF.Tau / 3f) * on;

        _glow.DrawCentered(fb, _glowAt.X, _glowAt.Y, 0.6f * on * pulse, _stencil);
        _disc.Draw(fb, _left, _top, 0.76f * on * pulse, _stencil);
    }

    /// <summary>
    /// The OpenSky stencil, with one repair for the moon. A thin bare branch
    /// across the moon is soft-edged: its edge pixels are only part branch, so
    /// they are still mostly moon colour, but
    /// the stencil (which asks "is this pixel exactly as painted before?")
    /// marks them as "not sky" and leaves them unpainted. Next to a red moon
    /// that shows as a fringe of pale dots. So inside the moon's box we also
    /// allow any pixel that is not solid branch (red at least 40; the moon is
    /// 255, a branch's solid middle about 12, and an edge pixel that is half
    /// of each about 130). Only the branch's solid middle stays untouched;
    /// its soft edge takes the tint along with the moon showing through it.
    /// </summary>
    internal static bool[] MoonStencil(HalloweenScenery s)
    {
        var stencil = (bool[])s.OpenSky.Clone();
        int x0 = Math.Max(0, (int)(s.Moon.At.X - s.Moon.R) - 3), x1 = Math.Min(s.Width, (int)(s.Moon.At.X + s.Moon.R) + 4);
        int y0 = Math.Max(0, (int)(s.Moon.At.Y - s.Moon.R) - 3), y1 = Math.Min(s.Height, (int)(s.Moon.At.Y + s.Moon.R) + 4);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = y * s.Width + x;
                if (!stencil[i] && ((s.Pixels[i] >> 16) & 0xFF) >= 40) stencil[i] = true;
            }
        return stencil;
    }
}
