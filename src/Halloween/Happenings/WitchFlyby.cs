using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A witch on a broomstick, in silhouette, flies across the sky and passes in
/// front of the moon. A small cat rides on the back of the broom, and the
/// broom's tail sheds a few twinkling sparks that fade behind her.
///
/// How it works:
///   - The witch is two sprites, painted once: one facing right and one
///     facing left (a mirror image). Begin picks which way she flies.
///   - Her path is a nearly straight line across the screen, from just off one
///     edge to just off the other, aimed so she crosses the moon within about
///     a third of its radius of the center. A slow sine wave bobs her up and
///     down, like a boat on a gentle swell.
///   - Everything is worked out from t alone. The sparks too: spark number i
///     is "dropped" at a fixed moment (0.2 s after the one before), so at any
///     time t we can say where she was when it was dropped, and how old it is.
///     That is the same trick ShootingStar uses for its sparks.
///   - Everything goes through the OpenSky stencil, so she passes BEHIND the
///     bare branches, and in front of the moon (the moon counts as open sky).
///
/// The sprite is drawn in "witch units": 1 unit is about the length of the
/// broom, with (0,0) on the broom's middle, x to the right, y down. That way
/// the picture is written once and scaled to any screen.
/// </summary>
internal sealed class WitchFlyby : Happening
{
    private const int Sparks = 46;
    private static readonly Color Ink = Color.FromArgb(12, 8, 20);          // the silhouette color

    private readonly HalloweenScenery _s;
    private readonly Sprite _right, _left, _orange, _purple;
    private readonly float _k;                       // pixels per witch unit
    private readonly float _ox, _oy;                 // where the broom's middle is inside the sprite (facing right)
    private readonly int _w;                         // sprite width, for mirroring _ox

    private float _dir = 1;                          // +1 flying right, -1 flying left
    private float _baseY, _slope, _phase;            // path: height at the moon, tilt, bob phase
    private readonly float[] _dropAt = new float[Sparks];     // when each spark is dropped, in seconds
    private readonly PointF[] _scatter = new PointF[Sparks];  // each spark's small random offset, in pixels

    public override float Seconds => 9f;
    public override string? Claims => "moon";

    public WitchFlyby(HalloweenScenery s)
    {
        _s = s;
        // The whole witch, hat tip to shoe and bristles to hand, is about 1.1 units
        // long, and we want that to be 0.11 U. Never smaller than 14 px per unit.
        _k = Math.Max(14f, s.U * 0.11f / 1.1f);
        _ox = 0.66f * _k;
        _oy = 0.54f * _k;
        _w = (int)MathF.Ceiling(1.24f * _k);
        int h = (int)MathF.Ceiling(0.82f * _k);

        _right = Sprite.Paint(_w, h, g => PaintWitch(g, false));
        _left = Sprite.Paint(_w, h, g => PaintWitch(g, true));

        _orange = Sprite.Glow(Math.Max(3, (int)(s.U * 0.016f)), Color.FromArgb(255, 150, 40));
        _purple = Sprite.Glow(Math.Max(3, (int)(s.U * 0.013f)), Color.FromArgb(190, 90, 255));
    }

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        // Her height as she passes the moon's middle: within 0.3 of its radius above or below the center.
        _baseY = _s.Moon.At.Y + _s.Moon.R * 0.3f * ((float)rng.NextDouble() * 2 - 1);
        _slope = 0.06f * ((float)rng.NextDouble() * 2 - 1);     // a slight climb or dive across the whole sky
        _phase = (float)rng.NextDouble() * MathF.Tau;
        for (int i = 0; i < Sparks; i++)
        {
            _dropAt[i] = 0.2f * i + 0.08f * (float)rng.NextDouble();
            _scatter[i] = new PointF(_s.U * 0.008f * ((float)rng.NextDouble() * 2 - 1), _s.U * 0.008f * ((float)rng.NextDouble() * 2 - 1));
        }
    }

    /// <summary>Where the middle of the broom is at time t.</summary>
    private PointF At(float t)
    {
        float margin = 0.7f * _k;                                         // start and end just past the screen edge
        float from = _dir > 0 ? -margin : _s.Width + margin;
        float x = from + _dir * (_s.Width + 2 * margin) * (t / Seconds);
        // The bob: one gentle wave every 2.4 s, at least 2 px tall so it still moves on a tiny screen.
        float bob = Math.Max(2f, _s.U * 0.012f) * MathF.Sin(t * MathF.Tau / 2.4f + _phase);
        return new PointF(x, _baseY + _slope * (x - _s.Moon.At.X) + bob);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // The sparks first, so the witch is stamped over them. Each lives 0.8 s,
        // sinking a little and twinkling as it dies.
        for (int i = 0; i < Sparks; i++)
        {
            float age = t - _dropAt[i];
            if (age < 0 || age > 0.8f) continue;
            PointF p = At(_dropAt[i]);                                    // where she was when it dropped
            float tx = p.X - _dir * 0.56f * _k + _scatter[i].X;           // the broom's tail is behind her...
            float ty = p.Y + 0.14f * _k + _scatter[i].Y + _s.U * 0.04f * age * age;   // ...and a little low
            float twinkle = 0.65f + 0.35f * MathF.Sin(age * 38 + i * 2f);
            (i % 2 == 0 ? _orange : _purple).DrawCentered(fb, tx, ty, (1 - age / 0.8f) * twinkle, _s.OpenSky);
        }

        PointF at = At(t);
        if (_dir > 0) _right.Draw(fb, (int)MathF.Round(at.X - _ox), (int)MathF.Round(at.Y - _oy), 1f, _s.OpenSky);
        else _left.Draw(fb, (int)MathF.Round(at.X - (_w - _ox)), (int)MathF.Round(at.Y - _oy), 1f, _s.OpenSky);
    }

    // ---- The painting. Everything below is written for a witch flying to the right. ----

    private void PaintWitch(Graphics g, bool mirror)
    {
        // Move the paper so (0,0) is the broom's middle, and scale it to witch units.
        // To mirror, flip left and right around the middle.
        g.TranslateTransform(mirror ? _w - _ox : _ox, _oy);
        g.ScaleTransform(mirror ? -_k : _k, _k);

        using var ink = new SolidBrush(Ink);
        using var pen = new Pen(Ink, 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        void Poly(params PointF[] pts) => g.FillPolygon(ink, pts);
        void Curve(params PointF[] pts) => g.FillClosedCurve(ink, pts, FillMode.Winding, 0.35f);
        void Stroke(float w, params PointF[] pts) { pen.Width = w; g.DrawLines(pen, pts); }

        // The broom: a handle tilting up toward the front, and a fan of straw at the back.
        Stroke(0.03f, new(-0.34f, 0.10f), new(0.52f, -0.05f));
        Poly(new(-0.33f, 0.075f), new(-0.56f, -0.02f), new(-0.53f, 0.04f), new(-0.60f, 0.07f), new(-0.54f, 0.115f),
             new(-0.60f, 0.17f), new(-0.52f, 0.19f), new(-0.33f, 0.125f));
        Stroke(0.04f, new(-0.345f, 0.055f), new(-0.345f, 0.145f));     // the string binding the straw

        // The cat sitting on the back of the broom: body, head, two pointed ears, and a tail curling up behind.
        g.FillEllipse(ink, -0.31f, -0.03f, 0.08f, 0.125f);
        g.FillEllipse(ink, -0.29f, -0.07f, 0.065f, 0.06f);
        Poly(new(-0.285f, -0.055f), new(-0.292f, -0.105f), new(-0.262f, -0.07f));
        Poly(new(-0.255f, -0.07f), new(-0.232f, -0.105f), new(-0.232f, -0.05f));
        Stroke(0.022f, new(-0.31f, 0.08f), new(-0.37f, 0.075f), new(-0.385f, 0.01f), new(-0.355f, -0.04f));

        // Her legs dangling, and a pointed shoe.
        Stroke(0.05f, new(0.0f, 0.05f), new(0.09f, 0.20f));
        Stroke(0.05f, new(0.03f, 0.04f), new(0.14f, 0.17f));
        Poly(new(0.07f, 0.19f), new(0.19f, 0.205f), new(0.175f, 0.235f), new(0.075f, 0.235f));
        Poly(new(0.12f, 0.16f), new(0.22f, 0.17f), new(0.21f, 0.2f), new(0.125f, 0.2f));

        // The dress: a body leaning into the wind and a skirt streaming out behind with a ragged hem.
        Curve(new(-0.07f, 0.05f), new(0.05f, 0.05f), new(0.14f, -0.09f), new(0.10f, -0.16f), new(0.0f, -0.12f), new(-0.06f, -0.02f));
        Poly(new(0.04f, -0.02f), new(-0.08f, -0.06f), new(-0.17f, -0.07f), new(-0.23f, -0.03f),
             new(-0.17f, 0.0f), new(-0.24f, 0.05f), new(-0.15f, 0.06f), new(-0.19f, 0.12f), new(-0.07f, 0.09f), new(0.0f, 0.12f), new(0.06f, 0.07f));

        // Her arm reaching out to grip the handle.
        Stroke(0.045f, new(0.10f, -0.10f), new(0.29f, -0.02f));
        g.FillEllipse(ink, 0.265f, -0.04f, 0.05f, 0.05f);

        // Long hair streaming behind her head.
        Poly(new(0.10f, -0.25f), new(0.0f, -0.24f), new(-0.12f, -0.22f), new(-0.24f, -0.25f), new(-0.16f, -0.19f),
             new(-0.27f, -0.15f), new(-0.13f, -0.14f), new(-0.02f, -0.12f), new(0.08f, -0.14f));

        // The head, and the hooked nose and pointed chin that make her unmistakably a witch.
        g.FillEllipse(ink, 0.08f, -0.255f, 0.105f, 0.105f);
        Poly(new(0.165f, -0.23f), new(0.245f, -0.175f), new(0.215f, -0.165f), new(0.195f, -0.19f), new(0.165f, -0.185f));
        Poly(new(0.14f, -0.165f), new(0.19f, -0.125f), new(0.12f, -0.15f));

        // The hat: a wide brim, and a tall cone whose tip is bent back by the wind.
        g.FillEllipse(ink, -0.02f, -0.27f, 0.27f, 0.055f);
        Poly(new(0.03f, -0.25f), new(0.05f, -0.36f), new(0.04f, -0.44f), new(-0.04f, -0.49f), new(-0.14f, -0.46f),
             new(-0.04f, -0.53f), new(0.07f, -0.49f), new(0.12f, -0.41f), new(0.15f, -0.33f), new(0.21f, -0.25f));
    }
}
