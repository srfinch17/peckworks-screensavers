using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// An apple pops out of the cornucopia's mouth, hops, lands, bounces once,
/// rolls off to the right along the ground, slows to a stop, rocks to and fro
/// a few times as it settles, rests a moment, and fades away.
///
/// FEYNMAN VERSION: a rolling ball is only believable if you can SEE it turn,
/// and a plain red circle looks the same however it turns. So the apple has
/// marks that go round with it: a stem with a leaf, a dimple where the stem
/// grows, a paler blush on one side and a few speckles. We paint the apple
/// 32 times, each time with the marks turned a little further (a flip-book of
/// a turning apple), and Draw picks the page for the angle the apple has
/// rolled through: angle = distance rolled / radius, which is exactly what
/// "rolling without slipping" means. The LIGHT does not turn with the apple
/// (the sun does not move), so the shine and the shaded underside are painted
/// the same on every page and only the marks move.
///
/// The path is worked out from t alone with school-physics sums: a hop under
/// gravity, a bounce that keeps half the speed, then a roll that slows at a
/// steady rate until it stops (friction). A soft shadow on the ground shrinks
/// and fades as the apple rises, which is what makes the hop read as height.
///
/// It is in FRONT of everything (layer 1): the fruit already spilled from the
/// horn is nearer the picture's back than the ground line the apple rolls on.
/// Its colour is picked per showing, red or golden, like the spilled fruit.
/// </summary>
internal sealed class AppleRoll : Happening
{
    private const float Total = 6f;
    private const int Pages = 32;                   // one turn of the apple, in 11 degree steps

    // The physics, in U (a screen's size unit) and seconds.
    private const float Gravity = 2.6f;             // U per second squared
    private const float HopUp = 0.42f;              // how fast the apple is thrown up out of the horn, U per second
    private const float Bounce = 0.5f;              // the share of its landing speed it keeps on the bounce
    private const float RollTime = 2.6f;            // how long the roll takes to slow to nothing
    private const float RockTime = 1.3f;            // the settling wobble
    private const float RestEnds = 5.2f;            // then it fades until Total

    private readonly ThanksgivingScenery _s;
    private readonly float _r;                      // the apple's radius in pixels
    private readonly int _size;                     // the sprite sheet is square, this many pixels
    private readonly Sprite[][] _page = new Sprite[2][];   // [colour][angle]
    private readonly Sprite _shadow;
    private readonly float _x0, _y0h;               // where it starts (x) and how high above its resting height (pixels)
    private readonly float _t1, _t2;                // how long the first flight and the bounce take (seconds)

    private float _vx = 0.15f;                      // sideways speed in U per second (a little different each showing)
    private int _colour;
    private float _turn;                            // the apple's starting angle, so no two showings look alike

    public override float Seconds => Total;
    public override int Layer => 1;
    public override string? Claims => "cornucopia";

    public AppleRoll(ThanksgivingScenery s)
    {
        _s = s;
        _r = Math.Max(3f, s.U * 0.015f);
        _size = (int)(_r * 3.8f) + 4;
        float spillR = s.CornucopiaLength * 0.22f;              // the horn's mouth radius, as the painter works it out
        _x0 = s.CornucopiaMouth.X - spillR * 0.15f;
        // The apple's centre starts in the middle of the horn's mouth; the resting centre is a radius above the walking line.
        float restY = s.WalkY(_x0) - _r;
        _y0h = Math.Max(0f, restY - (s.CornucopiaMouth.Y - spillR));

        // Flight times from the sums: up and down under gravity (the landing speed is worked from the start height too).
        float g = Gravity * s.U, vy = HopUp * s.U;
        float landSpeed = MathF.Sqrt(vy * vy + 2 * g * _y0h);
        _t1 = (vy + landSpeed) / g;
        _t2 = 2 * Bounce * landSpeed / g;

        Color[][] palette =
        [
            [Color.FromArgb(200, 36, 34), Color.FromArgb(150, 20, 28), Color.FromArgb(232, 150, 96)],    // red: body, deep streaks, blush
            [Color.FromArgb(224, 170, 50), Color.FromArgb(196, 120, 36), Color.FromArgb(246, 214, 112)],  // golden: body, deep streaks, blush
        ];
        for (int c = 0; c < 2; c++)
        {
            _page[c] = new Sprite[Pages];
            for (int i = 0; i < Pages; i++)
                _page[c][i] = PaintApple(MathF.Tau * i / Pages, palette[c]);
        }

        // The ground shadow: a few stacked ellipses, each fainter and wider, so the edge melts.
        int sw = (int)(_r * 3.6f) + 4, sh = (int)(_r * 1.2f) + 4;
        _shadow = Sprite.Paint(sw, sh, g2 =>
        {
            for (int i = 0; i < 5; i++)
            {
                float k = 1f - i / 5f;
                using var b = new SolidBrush(Color.FromArgb(34, 30, 16, 6));
                g2.FillEllipse(b, sw / 2f - _r * 1.6f * k, sh / 2f - _r * 0.45f * k, _r * 3.2f * k, _r * 0.9f * k);
            }
        });
    }

    public override void Begin(Random rng)
    {
        _vx = 0.15f * (0.9f + 0.25f * (float)rng.NextDouble());
        _colour = rng.Next(10) < 7 ? 0 : 1;      // mostly red: it reads better on the ochre ground
        _turn = MathF.Tau * (float)rng.NextDouble();
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float vx = _vx * u, vy = HopUp * u, g = Gravity * u;
        float vRoll = vx * 0.9f;
        float rollStart = _t1 + _t2;

        // Where the apple is: x along the ground and h, its height above resting, both in pixels.
        float x, h;
        if (t < _t1)
        {
            x = _x0 + vx * t;
            h = _y0h + vy * t - 0.5f * g * t * t;
        }
        else if (t < rollStart)
        {
            float tau = t - _t1;
            float vland = MathF.Sqrt(vy * vy + 2 * g * _y0h);
            x = _x0 + vx * _t1 + vRoll * tau;
            h = Bounce * vland * tau - 0.5f * g * tau * tau;
        }
        else
        {
            float tau = MathF.Min(t - rollStart, RollTime);
            x = _x0 + vx * _t1 + vRoll * _t2 + vRoll * tau - vRoll * tau * tau / (2 * RollTime);   // slowing at a steady rate
            h = 0;
        }
        h = MathF.Max(0f, h);

        float angle = _turn + (x - _x0) / _r;                    // rolling without slipping: angle = distance / radius
        float rocking = 0;
        float stopAt = rollStart + RollTime;
        if (t > stopAt)
        {
            // Settling: it rocks a few times and the rocking dies away. A rocking apple still rolls a little, so x moves with the angle.
            float tau = t - stopAt;
            rocking = 0.45f * MathF.Exp(-3.2f * tau) * MathF.Sin(12f * tau) * Smooth(tau / 0.15f);
            angle += rocking;
            x += rocking * _r;
        }

        float opacity = Fade(t, Total, 0.2f, Total - RestEnds);
        float groundY = _s.WalkY(x);
        float cy = groundY - _r - h;

        // Shadow first (it lies on the ground), fainter and narrower the higher the apple is.
        float shadowOp = opacity * 1.7f / (1f + h / _r);
        _shadow.DrawCentered(fb, x, groundY - _r * 0.05f, Math.Min(1f, shadowOp));

        int page = (int)MathF.Round(((angle / MathF.Tau) % 1f + 1f) % 1f * Pages) % Pages;
        _page[_colour][page].Draw(fb, (int)MathF.Round(x) - _size / 2, (int)MathF.Round(cy) - _size / 2, opacity);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>One page: the apple with its marks turned by "turn" radians (clockwise on the screen).</summary>
    private Sprite PaintApple(float turn, Color[] c) =>
        Sprite.Paint(_size, _size, g =>
        {
            float r = _r, mid = _size / 2f;
            g.TranslateTransform(mid, mid);
            using var body = new GraphicsPath();
            body.AddEllipse(-r, -r, r * 2, r * 2);

            using (var b = new SolidBrush(c[0])) g.FillPath(b, body);

            // ---- the marks that turn with the apple ----
            GraphicsState fixedFrame = g.Save();
            g.SetClip(body, CombineMode.Intersect);
            g.RotateTransform(turn * 180f / MathF.PI);
            using (var blush = new SolidBrush(Color.FromArgb(120, c[2])))               // a paler patch on one side
                g.FillEllipse(blush, r * 0.10f, -r * 0.62f, r * 0.9f, r * 0.8f);
            using (var streak = new Pen(Color.FromArgb(150, c[1]), Math.Max(1f, r * 0.14f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float a in (float[])[-0.9f, -0.35f, 0.25f, 0.8f])               // darker streaks running from stem to base
                    g.DrawArc(streak, -r * 1.05f + a * r * 0.5f, -r * 1.2f, r * 2.1f - a * r, r * 2.4f, 78, 24);
            using (var speck = new SolidBrush(Color.FromArgb(105, 250, 226, 150)))      // a few pale dots
                foreach (var d in (PointF[])[new(0.45f, 0.10f), new(-0.35f, 0.35f), new(0.15f, 0.55f), new(-0.5f, -0.15f), new(0.55f, 0.50f)])
                {
                    float sr = Math.Max(0.6f, r * 0.055f);
                    g.FillEllipse(speck, d.X * r - sr, d.Y * r - sr, sr * 2, sr * 2);
                }
            using (var dimple = new SolidBrush(Color.FromArgb(150, 60, 18, 14)))        // the dent the stem grows from
                g.FillEllipse(dimple, -r * 0.30f, -r * 1.0f, r * 0.6f, r * 0.34f);
            using (var calyx = new SolidBrush(Color.FromArgb(150, 70, 30, 16)))         // and the little dark flower end opposite
                g.FillEllipse(calyx, -r * 0.16f, r * 0.84f, r * 0.32f, r * 0.2f);
            g.Restore(fixedFrame);

            // ---- the stem and its leaf stick out past the body, so they are not clipped ----
            g.TranslateTransform(0, 0);
            g.RotateTransform(turn * 180f / MathF.PI);
            using (var stem = new Pen(Color.FromArgb(92, 58, 30), Math.Max(1.2f, r * 0.13f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(stem, 0, -r * 0.92f, r * 0.08f, -r * 1.32f);
            using (var leaf = new GraphicsPath())
            {
                leaf.AddClosedCurve([new PointF(r * 0.08f, -r * 1.22f), new PointF(r * 0.45f, -r * 1.42f), new PointF(r * 0.85f, -r * 1.30f), new PointF(r * 0.45f, -r * 1.16f)], 0.6f);
                using var lb = new SolidBrush(Color.FromArgb(86, 140, 52));
                g.FillPath(lb, leaf);
                using var vein = new Pen(Color.FromArgb(150, 52, 98, 34), Math.Max(0.8f, r * 0.05f));
                g.DrawLine(vein, r * 0.10f, -r * 1.22f, r * 0.78f, -r * 1.30f);
            }
            g.ResetTransform();

            // ---- the light, which does NOT turn: a shine up and to the left, a shaded underside to the right ----
            g.TranslateTransform(mid, mid);
            GraphicsState lit = g.Save();
            g.SetClip(body, CombineMode.Intersect);
            using (var litCircle = new GraphicsPath())
            {
                litCircle.AddEllipse(-r * 1.2f, -r * 1.2f, r * 2f, r * 2f);             // the body's own size, pushed up and left
                using var litRegion = new Region(litCircle);
                g.ExcludeClip(litRegion);                                               // what is left of the body is a crescent, down and to the right
                using var shade = new SolidBrush(Color.FromArgb(80, 40, 6, 20));
                g.FillEllipse(shade, -r, -r, r * 2, r * 2);
            }
            g.Restore(lit);
            using (var shine = new SolidBrush(Color.FromArgb(150, 255, 240, 220)))
                g.FillEllipse(shine, -r * 0.62f, -r * 0.62f, r * 0.5f, r * 0.34f);
        });
}
