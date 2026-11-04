using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A pale barn owl slips out of the barn's loft window and glides down over the
/// field, low and quiet, flapping slowly, its heart-shaped white face turned to
/// us, then off past the edge of the screen (now and then it climbs away into the
/// sky at the end instead).
///
/// FEYNMAN VERSION: the owl is a flip-book of 16 pages, one wingbeat (wings
/// swept up, down, and up again), painted in "owl units" (one unit is about the
/// length of its body) so the same drawing code makes any size. It starts tiny,
/// at the loft window, and grows as it comes down toward us, because things that
/// come nearer look bigger. A sprite cannot be stretched, so we paint the whole
/// flip-book at 8 sizes and use the size that matches how near it is (each
/// size is only a ninth bigger than the last, so the growth looks smooth).
///
/// Stencil: the owl flies in front of the hills and the barn (it comes out of the
/// loft window, in front of the barn) but BEHIND the scarecrow, the fence, the
/// near ground and the maples. The painter's OpenField stencil does exactly that:
/// it is "nothing painted after the field and its bales", and the scarecrow is
/// painted after them.
///
/// Pale on gold would vanish, so every part has a thin soft-brown edge, and the
/// face is the brightest thing on it. Speed is in U per second, so the trip takes
/// as long as the distance asks for.
/// </summary>
internal sealed class BarnOwl : Happening
{
    private const int Pages = 16, Tiers = 8;
    private const float Speed = 0.16f;           // cruising speed, U per second
    private const float Ramp = 0.9f;             // seconds to get up to it
    private const float Stoop = 2.2f;            // seconds to drop from the loft to the lane (and to grow)
    private const float FirstScale = 0.36f;      // the owl's size at the loft, as a share of its full size

    private static readonly Color Buff = Color.FromArgb(214, 168, 108);
    private static readonly Color BuffDark = Color.FromArgb(176, 128, 80);
    private static readonly Color White = Color.FromArgb(248, 240, 224);
    private static readonly Color Edge = Color.FromArgb(104, 70, 46);
    private static readonly Color Eye = Color.FromArgb(28, 18, 16);

    private readonly ThanksgivingScenery _s;
    private readonly bool[] _stencil;            // the painter's OpenField stencil
    private readonly float _kFull;               // pixels in one owl unit at full size
    private readonly Sprite[][] _page = new Sprite[Tiers][];
    private readonly int[] _w = new int[Tiers], _ox = new int[Tiers], _oy = new int[Tiers];

    private float _dir = 1, _seconds = 6f, _distance;
    private bool _climb;                         // this showing leaves up into the sky instead of off the side

    public override float Seconds => _seconds;
    public override string? Claims => "barn";

    public BarnOwl(ThanksgivingScenery s)
    {
        _s = s;
        _kFull = Math.Max(10f, s.U * 0.038f);
        _stencil = s.OpenField;

        for (int j = 0; j < Tiers; j++)
        {
            float f = FirstScale * MathF.Pow(1f / FirstScale, j / (float)(Tiers - 1));
            float k = _kFull * f;
            _w[j] = (int)MathF.Ceiling(3.0f * k) + 2;
            int h = (int)MathF.Ceiling(2.8f * k) + 2;
            _ox[j] = _w[j] / 2;
            _oy[j] = (int)(1.55f * k) + 1;
            _page[j] = new Sprite[Pages];
            for (int i = 0; i < Pages; i++)
            {
                int tier = j, pg = i;
                _page[j][i] = Sprite.Paint(_w[j], h, g =>
                {
                    g.TranslateTransform(_ox[tier], _oy[tier]);
                    g.ScaleTransform(k, -k);                      // from here: units, x forward, y UP, origin at the body's middle
                    PaintOwl(g, pg, k);
                });
            }
        }
    }


    private static PointF P(float x, float y) => new(x, y);

    /// <summary>One page: the owl facing right, in owl units, wings at the stroke's position pg (0 = up, 8 = down).</summary>
    private static void PaintOwl(Graphics g, int pg, float k)
    {
        float edgeW = MathF.Max(0.035f, 0.9f / k);
        using var edge = new Pen(Edge, edgeW) { LineJoin = LineJoin.Round };
        void Fill(PointF[] pts, Color c, float tension = 0.35f)
        {
            using var b = new SolidBrush(c);
            g.FillClosedCurve(b, pts, FillMode.Winding, tension);
            g.DrawClosedCurve(edge, pts, tension, FillMode.Winding);
        }

        // Where the wing is in its stroke: pointing up and swept back, to down and swept back, and round again.
        float stroke = (1f - MathF.Cos(MathF.Tau * pg / Pages)) / 2f;           // 0 = top, 1 = bottom
        float phi = (98f + (-62f - 98f) * stroke) * MathF.PI / 180f;
        bool underside = stroke < 0.5f;                                          // wing raised: we see its pale underside

        // The wing is drawn along its own axis (+x), with its leading edge on the -y side, then turned to phi.
        void Wing(float sx, float sy, float len, Color colour, Color tipColour)
        {
            var st = g.Save();
            g.TranslateTransform(sx, sy);
            g.RotateTransform(phi * 180f / MathF.PI);                            // y points up in these units, so a positive turn is anticlockwise, as phi is
            g.ScaleTransform(len, len * 1.35f);   // broader than long-and-thin: seen from the side a wing is a wide fan
            PointF[] wing = [P(0, -0.05f), P(0.35f, -0.11f), P(0.75f, -0.12f), P(1.1f, -0.05f), P(1.18f, 0.05f),
                             P(1.04f, 0.17f), P(0.88f, 0.15f), P(0.78f, 0.26f), P(0.58f, 0.23f), P(0.48f, 0.32f), P(0.28f, 0.27f), P(0.12f, 0.25f), P(0f, 0.15f)];
            using var b = new SolidBrush(colour);
            g.FillClosedCurve(b, wing, FillMode.Winding, 0.3f);                  // rounded, so the scallops read as feathers, not as a chopped block
            // The long fingertip feathers: dark lines running from the trailing edge in toward the wing's root.
            using var feather = new Pen(Color.FromArgb(150, tipColour), 0.035f);
            for (int i = 0; i < 4; i++)
            {
                PointF from = wing[5 + i * 2 > 11 ? 11 : 5 + i * 2];
                g.DrawLine(feather, from, P(from.X * 0.45f, from.Y * 0.3f));
            }
            using var pen = new Pen(Edge, edgeW / len * 0.8f) { LineJoin = LineJoin.Round };
            g.DrawClosedCurve(pen, wing, 0.3f, FillMode.Winding);
            g.Restore(st);
        }

        // The far wing, behind the body: a little smaller (further away) and darker.
        Wing(0.02f, 0.12f, 0.95f, underside ? Color.FromArgb(222, 210, 190) : BuffDark, BuffDark);

        // The tail: a short fan of white and buff feathers behind the body.
        Fill([P(-0.40f, 0.04f), P(-0.72f, 0.14f), P(-0.84f, 0.02f), P(-0.72f, -0.12f), P(-0.40f, -0.08f)], White);

        // The body: a plump oval, buff on the back and white below (the belly).
        Fill([P(-0.46f, 0.02f), P(-0.20f, 0.20f), P(0.25f, 0.22f), P(0.52f, 0.08f), P(0.50f, -0.12f), P(0.10f, -0.26f), P(-0.28f, -0.17f)], Buff);
        using (var belly = new SolidBrush(White))
            g.FillClosedCurve(belly, [P(-0.30f, -0.04f), P(0.10f, -0.02f), P(0.46f, -0.04f), P(0.42f, -0.14f), P(0.10f, -0.22f), P(-0.22f, -0.14f)], FillMode.Winding, 0.4f);

        // The head: a round buff cap with the white heart-shaped face turned toward us.
        float hx = 0.58f, hy = 0.15f;
        Fill([P(hx - 0.21f, hy), P(hx - 0.14f, hy + 0.17f), P(hx + 0.06f, hy + 0.22f), P(hx + 0.21f, hy + 0.08f), P(hx + 0.20f, hy - 0.10f), P(hx + 0.02f, hy - 0.20f), P(hx - 0.16f, hy - 0.12f)], Buff, 0.5f);
        using (var face = new SolidBrush(White))
        {
            g.FillEllipse(face, hx - 0.19f, hy - 0.10f, 0.20f, 0.24f);                            // the two lobes of the heart
            g.FillEllipse(face, hx - 0.02f, hy - 0.10f, 0.20f, 0.24f);
            g.FillPolygon(face, [P(hx - 0.15f, hy - 0.08f), P(hx + 0.14f, hy - 0.08f), P(hx - 0.005f, hy - 0.20f)]);   // its point, at the chin
        }
        using (var eye = new SolidBrush(Eye))
        {
            g.FillEllipse(eye, hx - 0.115f, hy + 0.01f, 0.07f, 0.08f);
            g.FillEllipse(eye, hx + 0.045f, hy + 0.01f, 0.07f, 0.08f);
        }
        using (var gleam = new SolidBrush(Color.White))
        {
            g.FillEllipse(gleam, hx - 0.095f, hy + 0.055f, 0.025f, 0.025f);
            g.FillEllipse(gleam, hx + 0.065f, hy + 0.055f, 0.025f, 0.025f);
        }
        using (var beak = new SolidBrush(Color.FromArgb(214, 172, 120)))
            g.FillPolygon(beak, [P(hx - 0.02f, hy + 0.0f), P(hx + 0.03f, hy + 0.0f), P(hx + 0.005f, hy - 0.07f)]);

        // The near wing, in front of the body.
        Wing(0.08f, 0.12f, 1.12f, underside ? White : Buff, underside ? Color.FromArgb(196, 172, 140) : BuffDark);
    }

    public override void Begin(Random rng)
    {
        _dir = rng.NextDouble() < 0.65 ? -1f : 1f;           // mostly toward the farther side, for a longer flight
        _climb = rng.NextDouble() < 0.35;
        float x0 = _s.BarnLoft.X + _s.BarnLoft.Width / 2;
        float margin = _kFull * 2f;
        _distance = (_dir > 0 ? _s.Width - x0 : x0) + margin;
        // Distance at full speed after the ramp: speed * (t - Ramp / 2).
        _seconds = _distance / (Speed * _s.U) + Ramp / 2 + 0.2f;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float v = Speed * _s.U;
        float travelled = t < Ramp ? v * Ramp * (MathF.Pow(t / Ramp, 3) - MathF.Pow(t / Ramp, 4) / 2f) : v * (t - Ramp / 2);
        float x0 = _s.BarnLoft.X + _s.BarnLoft.Width / 2, y0 = _s.BarnLoft.Y + _s.BarnLoft.Height / 2;
        float x = x0 + _dir * travelled;

        // Down from the loft to the lane across the field, easing in, then low and level with a gentle rise and fall.
        float drop = Smooth(t / Stoop);
        float lane = _s.FieldY(Math.Clamp(x, 0, _s.Width - 1)) - _s.U * 0.02f;
        float y = y0 + (lane - y0) * drop + _s.U * 0.004f * MathF.Sin(t * 2.4f) * drop;
        if (_climb) { float c = Smooth((t - _seconds * 0.55f) / (_seconds * 0.4f)); y -= _s.U * 0.45f * c * c; }

        int tier = Math.Clamp((int)MathF.Round(drop * (Tiers - 1)), 0, Tiers - 1);
        int pg = (int)(t * 3.2f * Pages) % Pages;                              // a wingbeat about three times a second
        float fade = Smooth(t / 0.35f) * Smooth((_seconds - t) / 0.3f);
        bool mirror = _dir < 0;
        int left = (int)MathF.Round(x) - (mirror ? _w[tier] - _ox[tier] : _ox[tier]);
        int top = (int)MathF.Round(y) - _oy[tier];
        _page[tier][pg].Draw(fb, left, top, fade, _stencil, mirror);
    }
}
