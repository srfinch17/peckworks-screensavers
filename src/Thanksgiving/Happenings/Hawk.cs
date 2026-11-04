using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A red-tailed hawk soars high above the farm in slow circles, wings held
/// out flat, seen from below: a dark silhouette with fingered wingtips and a
/// rusty tail. It drifts across the sky while it circles two or three
/// times, and glides off the far edge. Slow and calm, the zen one.
///
/// THE PATH. Two motions added together, like a point on a wheel that is
/// itself rolling along:
///   - the circle's middle drifts in a straight line across the sky, and
///   - the hawk goes round that middle once every few seconds. The circle is
///     squashed flat (we see it from the ground, at a slant), so it looks like
///     an oval.
/// Where the hawk is, and which way it is pointing, both come from t alone:
/// the heading is the direction it moved over the last moment.
///
/// THE TURNING. A rubber stamp cannot turn, so the hawk is a flip-book of 36
/// pages, each painted already turned to a heading ten degrees from the next
/// (the engine's TurningStamp lives in another saver, so the pages are
/// painted here). The wings are drawn a little foreshortened (as a bird
/// banked into its turn would look) and the page nearest the heading now is
/// stamped. At 0.3 s a page this reads as a slow, smooth lean round.
///
/// It is drawn through the OpenSky stencil: the hills hide it if it sinks
/// that far, and the corner branches pass in front. It may cross the moon,
/// and a dark hawk against the moon's peach is the prettiest moment.
/// </summary>
internal sealed class Hawk : Happening
{
    private const int Pages = 36;
    private const float Seconds_ = 20f;

    private readonly ThanksgivingScenery _s;
    private readonly Sprite[] _page = new Sprite[Pages];
    private readonly float _span;                                   // wingtip to wingtip, in pixels

    private PointF _from, _to;                                      // where the circle's middle starts and ends
    private float _radius, _turnRate, _phase;                       // the circle's size (pixels), its speed (radians a second), where on it we start
    private float _flip;                                            // +1 or -1: which way round it circles

    public override float Seconds => Seconds_;

    public Hawk(ThanksgivingScenery s)
    {
        _s = s;
        _span = Math.Max(12f, s.U * 0.13f);
        for (int i = 0; i < Pages; i++) _page[i] = PaintHawk(MathF.Tau * i / Pages);
    }

    /// <summary>
    /// One page: the hawk turned so its nose points along "heading" (0 = straight up the screen, then
    /// clockwise). Drawn in a square sheet twice the size of the wing span's half, so no turn clips it.
    /// </summary>
    private Sprite PaintHawk(float heading)
    {
        int n = (int)MathF.Ceiling(_span * 1.2f) | 1;                // an odd number, so there is a middle pixel
        float hs = _span / 2f;                                      // half the span: the unit everything below is measured in
        return Sprite.Paint(n, n, g =>
        {
            g.TranslateTransform(n / 2f, n / 2f);
            g.RotateTransform(heading * 180f / MathF.PI);
            g.ScaleTransform(0.92f * hs, hs);                       // foreshorten the wings a touch: it is banked, and we see it at a slant
            // Everything from here is in "half spans": x across (right is the bird's right wing as we see it),
            // y along the body, the nose at about -0.42 and the tail tip at +0.5.
            var dark = Color.FromArgb(255, 34, 22, 28);
            var wingCol = Color.FromArgb(255, 52, 36, 40);
            var lining = Color.FromArgb(255, 86, 62, 62);
            var rust = Color.FromArgb(255, 176, 82, 44);
            var rustEdge = Color.FromArgb(255, 70, 34, 28);

            // The tail: a rounded fan, rusty, with a dark band near its end.
            using (var tail = new GraphicsPath())
            {
                tail.AddPolygon([new PointF(-0.08f, 0.16f), new PointF(0.08f, 0.16f), new PointF(0.2f, 0.52f), new PointF(0.1f, 0.57f),
                                 new PointF(0f, 0.58f), new PointF(-0.1f, 0.57f), new PointF(-0.2f, 0.52f)]);
                using var b = new SolidBrush(rust);
                g.FillPath(b, tail);
                using var band = new Pen(rustEdge, 0.07f);
                g.DrawLine(band, -0.18f, 0.5f, 0.18f, 0.5f);
            }

            // The two wings: the right wing's outline, then the same mirrored for the left.
            // Leading edge nearly straight out, a wrist bend, then four separated finger feathers at the tip,
            // and a gently curved trailing edge back to the body.
            PointF[] right =
            [
                new(0.08f, -0.22f), new(0.40f, -0.34f), new(0.72f, -0.36f), new(0.96f, -0.30f),
                new(1.02f, -0.22f), new(0.92f, -0.17f),                                       // finger 1
                new(1.00f, -0.08f), new(0.88f, -0.06f),                                       // finger 2
                new(0.94f, 0.04f), new(0.82f, 0.04f),                                         // finger 3
                new(0.84f, 0.14f), new(0.72f, 0.10f),                                         // finger 4
                new(0.52f, 0.12f), new(0.30f, 0.17f), new(0.12f, 0.24f),
            ];
            using (var wing = new SolidBrush(wingCol))
            using (var edge = new Pen(dark, 0.05f))
            {
                for (int side = 0; side < 2; side++)
                {
                    var pts = new PointF[right.Length];
                    for (int i = 0; i < right.Length; i++) pts[i] = new PointF(side == 0 ? right[i].X : -right[i].X, right[i].Y);
                    g.FillPolygon(wing, pts);
                    g.DrawLines(edge, new[] { pts[0], pts[1], pts[2], pts[3], pts[4] });           // a darker leading edge
                    // A lighter lining along the inner trailing edge, the pale underwing of the species.
                    using var lin = new SolidBrush(lining);
                    g.FillPolygon(lin, new[]
                    {
                        pts[12], pts[13], pts[14],
                        new PointF(pts[14].X, pts[14].Y - 0.07f), new PointF(pts[13].X, pts[13].Y - 0.08f), new PointF(pts[12].X, pts[12].Y - 0.08f),
                    });
                }
            }

            // The body: a slim oval, and the head a small round bump at the front with a hint of a beak.
            using var bodyBrush = new SolidBrush(dark);
            g.FillEllipse(bodyBrush, -0.1f, -0.30f, 0.2f, 0.56f);
            g.FillEllipse(bodyBrush, -0.075f, -0.44f, 0.15f, 0.17f);
        });
    }

    public override void Begin(Random rng)
    {
        float W = _s.Width, H = _s.Height, U = _s.U;
        // The middle of the circle drifts across the sky, left to right or right to left, high up
        // (under a third of the way down, well above the hills at 0.62 h or lower).
        _flip = rng.Next(2) == 0 ? 1 : -1;
        float y0 = H * (0.14f + 0.10f * (float)rng.NextDouble());
        float y1 = H * (0.14f + 0.10f * (float)rng.NextDouble());
        _radius = U * (0.075f + 0.03f * (float)rng.NextDouble());
        float out_ = _radius + _span;                               // start and end this far off the edge, so it glides on and off
        bool leftToRight = rng.Next(2) == 0;
        _from = new PointF(leftToRight ? W * 0.12f : W * 0.88f, y0);
        _to = new PointF(leftToRight ? W + out_ : -out_, y1);
        _turnRate = MathF.Tau / (7f + 1.5f * (float)rng.NextDouble());   // one circle every 7 to 8.5 seconds: two and a half or so in a showing
        _phase = (float)rng.NextDouble() * MathF.Tau;
    }

    /// <summary>The hawk's place at time t: the circle's moving middle plus the walk round it.</summary>
    private PointF At(float t)
    {
        // The drift eases in a little at the start and quickens as it glides off: k is a bit below linear first.
        float k = Math.Clamp(t / Seconds_, 0f, 1f);
        k = k * 0.75f + 0.25f * k * k;                              // 0..1, speeding up gently
        float cx = _from.X + (_to.X - _from.X) * k, cy = _from.Y + (_to.Y - _from.Y) * k;
        float a = _phase + _flip * _turnRate * t;
        // The oval: wide across, squashed (0.45) up and down, as a circle seen from the ground at a slant.
        return new PointF(cx + MathF.Cos(a) * _radius, cy + MathF.Sin(a) * _radius * 0.45f);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        PointF p = At(t), before = At(t - 0.05f), after = At(t + 0.05f);
        float vx = after.X - before.X, vy = after.Y - before.Y;
        float heading = MathF.Atan2(vx, -vy);                       // 0 = up the screen, clockwise
        int page = (int)MathF.Round((heading < 0 ? heading + MathF.Tau : heading) / MathF.Tau * Pages) % Pages;
        float fade = Fade(t, Seconds_, 1.5f, 0.6f);                 // it appears softly; at the end it is off the edge anyway
        _page[page].DrawCentered(fb, p.X, p.Y, fade, _s.OpenSky);
    }
}
