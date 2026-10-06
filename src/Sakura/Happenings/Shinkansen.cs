using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// The bullet train: the classic photo of Fuji with a shinkansen in front of it.
/// A long white train with a blue stripe slides along the far shore, from off
/// one side of the screen to off the other, in a few seconds.
///
/// How: the train is ONE long thin sprite, painted once in the constructor
/// (two copies, one facing each way). A real shinkansen is very long and very
/// far away, so on screen it is only about a hundredth of the screen tall and
/// nearly half as long as the screen is high. Think of a white pencil laid
/// along the shore.
///
/// Its wheels are hidden in the shore trees. The shore is painted right at the
/// horizon, so instead of a stencil doing the hiding (the shore trees are not
/// "in front" in the stencil's eyes) we paint the bottom fifth of the sprite
/// fading out to nothing, so the train seems to sink into the dark trees.
/// The banks and trees in front DO hide it, through the stencil.
///
/// A faint streak of light is left behind the tail: the shimmer of speed.
/// </summary>
internal sealed class Shinkansen : Happening
{
    private readonly Scenery _s;
    private readonly Sprite _facingRight, _facingLeft;
    private readonly float _length;      // the whole train, in pixels
    private readonly float _seconds;
    private bool _toRight;

    public override float Seconds => _seconds;
    public override int Layer => 0;      // far shore: behind the boat and everything on the water

    public Shinkansen(Scenery s)
    {
        _s = s;
        _length = s.U * 0.28f;                                      // long, but not a third of Fuji: it is far away
        float speed = s.U * 0.2f;                                   // 0.2 U per second: it covers its own length in about 1.4 s, quick but readable
        _seconds = (s.Width + _length) / speed;                     // the time to go from fully off one side to fully off the other
        int h = Math.Max(5, (int)MathF.Round(s.U * 0.014f));
        int w = Math.Max(30, (int)MathF.Round(_length));
        _facingRight = Sprite.Paint(w, h, g => PaintTrain(g, w, h, false));
        _facingLeft = Sprite.Paint(w, h, g => PaintTrain(g, w, h, true));
        _h = h;
    }

    private readonly int _h;

    public override void Begin(Random rng) => _toRight = rng.Next(2) == 0;

    /// <summary>Paints the train facing right (or, mirrored, facing left).</summary>
    private static void PaintTrain(Graphics g, int w, int h, bool mirror)
    {
        if (mirror) g.Transform = new Matrix(-1, 0, 0, 1, w, 0);   // flip left-right: x becomes w - x

        float nose = w * 0.15f;             // the long sloping duck-bill
        float body = w - nose;              // where the straight cars end
        const float Top = 0.06f;            // fractions of the train's height

        // White body with a bottom fifth that fades to clear (the trees swallow the wheels).
        using var white = new LinearGradientBrush(new PointF(0, 0), new PointF(0, h), Color.FromArgb(255, 246, 248, 252), Color.FromArgb(0, 246, 248, 252))
        {
            Blend = new Blend { Positions = [0f, 0.7f, 1f], Factors = [0f, 0f, 1f] },
        };
        g.FillRectangle(white, 0, h * Top, body + 1, h * (1 - Top));
        // The nose: roof dips down in a long curve to a rounded tip.
        g.FillPolygon(white, [
            new PointF(body, h * Top), new PointF(body + nose * 0.45f, h * 0.17f), new PointF(body + nose * 0.85f, h * 0.38f),
            new PointF(w, h * 0.62f), new PointF(body + nose * 0.8f, h), new PointF(body, h)]);

        // Royal-blue stripe along the windows and a thin blue line below it.
        using var blue = new SolidBrush(Color.FromArgb(28, 70, 176));
        float stripeTop = h * 0.40f, stripeH = Math.Max(1f, h * 0.15f);
        g.FillRectangle(blue, 0, stripeTop, body + nose * 0.25f, stripeH);
        g.FillRectangle(blue, 0, h * 0.64f, body + nose * 0.5f, Math.Max(1f, h * 0.07f));

        // Tiny dark windows: a row of dots above the stripe, in groups per car.
        using var glass = new SolidBrush(Color.FromArgb(46, 58, 84));
        float car = body / 7.5f;
        float dot = Math.Max(1f, h * 0.13f);
        for (int c = 0; c < 7; c++)
            for (int k = 0; k < 4; k++)
                g.FillRectangle(glass, c * car + car * (0.14f + 0.2f * k), h * 0.22f, dot * 1.3f, dot);
        // The windscreen, dark, on the sloping nose.
        g.FillPolygon(glass, [
            new PointF(body + nose * 0.14f, h * 0.15f), new PointF(body + nose * 0.52f, h * 0.24f),
            new PointF(body + nose * 0.62f, h * 0.38f), new PointF(body + nose * 0.10f, h * 0.38f)]);

        // Thin gaps between the cars, so it reads as a train and not a pipe.
        using var gap = new Pen(Color.FromArgb(150, 170, 180, 200), 1f);
        for (int c = 1; c < 8; c++)
            g.DrawLine(gap, c * car, h * 0.08f, c * car, h * 0.7f);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float p = Math.Clamp(t / _seconds, 0f, 1f);
        int w = _facingRight.Width;
        // "left" is the train's left edge; it starts fully off one side and ends fully off the other.
        float left = _toRight ? -w + p * (fb.Width + w) : fb.Width - p * (fb.Width + w);
        // Bottom edge a hair below the horizon so the faded fifth sits in the shore trees.
        int top = (int)MathF.Round(_s.HorizonY + _s.U * 0.002f - _h);
        var sprite = _toRight ? _facingRight : _facingLeft;
        sprite.Draw(fb, (int)MathF.Round(left), top, 1f, _s.OpenBehindBanks);

        // The shimmer of speed: two soft white streaks trailing from the tail.
        float tail = _toRight ? left : left + w;
        float dir = _toRight ? -1 : 1;
        float len = _s.U * 0.13f;
        float y1 = top + _h * 0.45f, y2 = top + _h * 0.62f;
        float thick = Math.Max(1f, _s.U * 0.0016f);
        fb.Line(new PointF(tail, y1), new PointF(tail + dir * len, y1), Color.White, 0.30f, thick, _s.OpenBehindBanks);
        fb.Line(new PointF(tail, y2), new PointF(tail + dir * len * 0.6f, y2), Color.White, 0.18f, thick, _s.OpenBehindBanks);
    }
}
