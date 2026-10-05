using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// The skeleton's eye sockets light up red: two embers fade in, throb for a
/// few seconds like coals being blown on, and fade out again.
///
/// Each eye is two stamps. First a wide, faint red halo (the light spilling
/// onto the skull around the socket), then a small bright ember right in
/// the socket. The ember goes on second so it covers the halo's pale center.
///
/// Where the sockets are comes from the skeleton's own measurements in
/// HalloweenPainter.PaintSkeleton: 0.032 of its height either side of the
/// middle, 0.907 of its height above its feet.
/// </summary>
internal sealed class SkeletonEyes : Happening
{
    private readonly Sprite _halo, _ember;
    private readonly PointF _left, _right;

    public override float Seconds => 7f;
    public override string? Claims => "skeleton";

    public SkeletonEyes(HalloweenScenery s)
    {
        float tall = s.SkeletonHeight;
        _left = new PointF(s.SkeletonFoot.X - tall * 0.032f, s.SkeletonFoot.Y - tall * 0.907f);
        _right = new PointF(s.SkeletonFoot.X + tall * 0.032f, _left.Y);

        _halo = Sprite.Glow((int)(tall * 0.12f), Color.FromArgb(255, 36, 12));

        // The ember: a dot that is hot orange in the middle and deep red at
        // its rim, a little smaller than the socket so a ring of dark shows
        // around it. Never under 2 pixels across, or it would vanish on a
        // small screen.
        int d = Math.Max(2, (int)(tall * 0.034f));
        _ember = Sprite.Paint(d, d, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, d, d);
            using var coal = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(255, 255, 120, 60),
                SurroundColors = [Color.FromArgb(235, 215, 10, 0)],
            };
            g.FillPath(coal, path);
        });
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float on = Fade(t, Seconds, 1.2f, 1.5f);
        // Two sine waves of different speeds added together: a slow throb
        // with a faster shimmer on top, so it never looks like a blinker.
        float throb = 0.78f + 0.16f * MathF.Sin(t * 4.2f) + 0.06f * MathF.Sin(t * 13f);
        foreach (PointF eye in (PointF[])[_left, _right])
        {
            _halo.DrawCentered(fb, eye.X, eye.Y, on * throb * 0.7f);
            _ember.DrawCentered(fb, eye.X, eye.Y, on * (0.55f + 0.45f * throb));
        }
    }
}
