using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A white snow rabbit hops across the snow: four hops, then it stops, sits
/// up on its haunches and twitches its ears for a couple of seconds, then
/// hops on the same way, and fades out into the evening.
///
/// FEYNMAN VERSION: a hop is a flip-book of six key moments (crouch, push
/// off, stretch in the air, tuck at the top, reach down, land). We paint 12
/// pages by blending smoothly from each key moment to the next, so the body
/// squashes and stretches like a spring. While the pages play, the whole
/// sprite is also lifted along an arc (a sine wave: up and back down) and
/// slid forward, but only while its feet are off the ground.
///
/// Sitting up is the same skeleton with the body stood on end. Going from
/// crouch to sitting is 5 blended pages, and the same 5 backwards to set
/// off again. In the sit, a small separate flip-book wiggles the ears.
///
/// White on white snow would vanish, so each page is painted in two passes:
/// first every shape with a fat pale grey-blue pen (the outline), then the
/// white on top of it. A soft grey-blue shadow patch is also stamped on the
/// snow under the rabbit and fades as it rises, so the hop feels weighty.
///
/// The rabbit stays away from the cabin (it would look stuck to the wall)
/// and it is in front of the snow, so there is no stencil.
/// </summary>
internal sealed class Rabbit : Happening
{
    private const float Total = 9f;
    private const float Lead = 0.2f;                 // seconds of stillness-in-the-fade before the first hop
    private const int Hops = 4;                      // hops before the sit, and the same number after
    private const float Sit = 2.5f, Trans = 0.3f;
    private const float HopTime = (Total - 2 * Lead - Sit - 2 * Trans) / (2 * Hops);   // seconds per hop, about 0.69
    private const int HopPages = 12, TransPages = 5, EarPages = 6;

    private static readonly Color Fur = Color.FromArgb(250, 252, 255);
    private static readonly Color Haunch = Color.FromArgb(234, 239, 249);
    private static readonly Color Edge = Color.FromArgb(150, 168, 202);
    private static readonly Color Pink = Color.FromArgb(240, 172, 188);
    private static readonly Color Dark = Color.FromArgb(40, 32, 52);

    /// <summary>One skeleton pose: everything the painter needs to draw a rabbit once.</summary>
    private readonly record struct Pose(float Tilt, float Stretch, float HipY, float HindX, float HindY, float ForeX, float ForeY, float Ear);

    // The six key moments of a hop, and the sitting pose. Tilt is degrees nose-up, Stretch is body length,
    // Hind and Fore are where the feet end up (units, from the hip), Ear is how far the ears lean back.
    private static readonly Pose[] Keys =
    [
        new(18, 0.85f, 0.070f,  0.06f, 0.02f, 0.13f, 0.02f, 55),   // crouch
        new(24, 1.20f, 0.090f, -0.10f, 0.03f, 0.17f, 0.07f, 62),   // push off
        new(30, 1.30f, 0.090f, -0.17f, 0.07f, 0.20f, 0.09f, 72),   // stretched in the air
        new(12, 1.00f, 0.090f, -0.03f, 0.05f, 0.15f, 0.05f, 65),   // tucked at the top
        new( 5, 1.15f, 0.080f, -0.10f, 0.08f, 0.19f, 0.02f, 68),   // reaching down
        new(12, 0.90f, 0.070f,  0.02f, 0.02f, 0.15f, 0.02f, 50),   // landing
    ];
    private static readonly Pose Sitting = new(62, 0.90f, 0.075f, 0.085f, 0.02f, 0.115f, 0.03f, 10);

    private readonly ChristmasScenery _s;
    private readonly float _k;                       // pixels per unit; the sitting rabbit is 0.4 units tall = 0.04 U
    private readonly int _w, _h, _ox, _by;
    private readonly Sprite[][] _hop = new Sprite[2][];      // [0] faces right, [1] faces left
    private readonly Sprite[][] _toSit = new Sprite[2][];
    private readonly Sprite[][] _ears = new Sprite[2][];
    private readonly Sprite _shadow;
    private readonly float _hopLen;                  // how far one hop carries it, pixels
    private readonly float _hopHigh;                 // how high a hop goes, pixels

    private int _dir = 1;
    private float _startX, _sitX;

    public override float Seconds => Total;
    public override string? Claims => "snow";

    public Rabbit(ChristmasScenery s)
    {
        _s = s;
        _k = Math.Max(10f, s.U * 0.10f);
        _w = (int)(0.9f * _k) + 4;
        _ox = (int)(0.45f * _k) + 2;
        _h = (int)(0.62f * _k) + 8;
        _by = _h - 4;
        _hopLen = s.U * 0.055f;
        _hopHigh = s.U * 0.03f;

        for (int d = 0; d < 2; d++)
        {
            bool mirror = d == 1;
            _hop[d] = new Sprite[HopPages];
            for (int f = 0; f < HopPages; f++)
            {
                float pos = f * Keys.Length / (float)HopPages;       // where we are among the keys
                int a = (int)pos;
                _hop[d][f] = Page(mirror, Lerp(Keys[a], Keys[(a + 1) % Keys.Length], pos - a), 0, 0);
            }
            _toSit[d] = new Sprite[TransPages];
            for (int i = 0; i < TransPages; i++)
                _toSit[d][i] = Page(mirror, Lerp(Keys[0], Sitting, Smooth((i + 1) / (float)(TransPages + 1))), 0, 0);
            _ears[d] = new Sprite[EarPages];
            float[] flick = [0, 16, -8, 13, -9, 7];                  // degrees each ear is flicked this page
            for (int i = 0; i < EarPages; i++)
                _ears[d][i] = Page(mirror, Sitting, flick[i], -flick[i] * 0.7f);
        }

        // A soft grey-blue patch for the shadow on the snow.
        int sw = Math.Max(6, (int)(0.5f * _k)), sh = Math.Max(3, (int)(0.1f * _k));
        _shadow = Sprite.Paint(sw, sh, g =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, sw - 1, sh - 1);
            using var b = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(150, 110, 130, 175),
                SurroundColors = [Color.FromArgb(0, 110, 130, 175)],
                Blend = Sprite.SoftFalloff,
            };
            g.FillPath(b, path);
        });
    }

    private static Pose Lerp(Pose a, Pose b, float t) => new(
        a.Tilt + (b.Tilt - a.Tilt) * t, a.Stretch + (b.Stretch - a.Stretch) * t, a.HipY + (b.HipY - a.HipY) * t,
        a.HindX + (b.HindX - a.HindX) * t, a.HindY + (b.HindY - a.HindY) * t,
        a.ForeX + (b.ForeX - a.ForeX) * t, a.ForeY + (b.ForeY - a.ForeY) * t, a.Ear + (b.Ear - a.Ear) * t);

    public override void Begin(Random rng)
    {
        _dir = rng.Next(2) == 0 ? 1 : -1;
        float w = _s.Width;
        float cabinL = _s.CabinFoot.X - _s.CabinWall.Width / 2 - _s.U * 0.08f;
        float cabinR = _s.CabinFoot.X + _s.CabinWall.Width / 2 + _s.U * 0.08f;
        // Pick a spot to sit that keeps the whole path clear of the cabin and on screen. A few tries, then settle.
        float reach = Hops * _hopLen + _s.U * 0.05f;
        _sitX = w * 0.5f;
        for (int i = 0; i < 12; i++)
        {
            float x = w * (0.2f + 0.6f * (float)rng.NextDouble());
            float lo = x - reach, hi = x + reach;
            if (lo > 0 && hi < w && (hi < cabinL || lo > cabinR)) { _sitX = x; break; }
        }
        _startX = _sitX - _dir * Hops * _hopLen;
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        int d = _dir > 0 ? 0 : 1;
        float tt = t - Lead;
        float hopsEnd = Hops * HopTime;
        float x, lift = 0;
        Sprite page;

        if (tt < hopsEnd)                                                  // the first set of hops
            page = HopAt(tt, _startX, d, out x, out lift);
        else if (tt < hopsEnd + Trans)                                     // crouch, then rise up onto the haunches
        {
            x = _sitX;
            page = _toSit[d][Math.Min(TransPages - 1, (int)((tt - hopsEnd) / Trans * TransPages))];
        }
        else if (tt < hopsEnd + Trans + Sit)                               // sitting, ears twitching
        {
            x = _sitX;
            float u = tt - hopsEnd - Trans;
            float cycle = u % 1.3f;                                        // a flick of the ears, then still, then again
            int f = cycle < 0.6f ? 1 + (int)(cycle / 0.6f * (EarPages - 1)) : 0;
            page = _ears[d][f];
        }
        else if (tt < hopsEnd + 2 * Trans + Sit)                           // drop back to a crouch
        {
            x = _sitX;
            page = _toSit[d][Math.Max(0, TransPages - 1 - (int)((tt - hopsEnd - Trans - Sit) / Trans * TransPages))];
        }
        else                                                               // the second set of hops, the same way
            page = HopAt(tt - hopsEnd - 2 * Trans - Sit, _sitX, d, out x, out lift);

        float op = Fade(t, Total, 0.4f, 0.5f);
        float y = _s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1)) + _s.U * 0.003f;
        float shade = op * (1 - 0.7f * Math.Min(1, lift / _hopHigh));      // the shadow thins as it rises
        _shadow.DrawCentered(fb, x + _dir * 0.02f * _k, y, shade);
        page.Draw(fb, (int)MathF.Round(x - (d == 0 ? _ox : _w - _ox)), (int)MathF.Round(y - lift) - _by, op);
    }

    /// <summary>The page, x and lift for "tt" seconds into a run of hops that began at fromX.</summary>
    private Sprite HopAt(float tt, float fromX, int d, out float x, out float lift)
    {
        tt = Math.Max(0, tt);
        int n = (int)(tt / HopTime);
        float u = tt / HopTime - n;                                        // 0 to 1 through this hop
        float air = Math.Clamp((u - 1f / 6) / (4f / 6), 0, 1);            // 0 until it pushes off, 1 once it lands
        x = fromX + _dir * _hopLen * (n + air);
        lift = _hopHigh * MathF.Sin(MathF.PI * air);
        return _hop[d][Math.Min(HopPages - 1, (int)(u * HopPages))];
    }

    // ----------------------------------------------------------------- paint

    private Sprite Page(bool mirror, Pose p, float earNear, float earFar) =>
        Sprite.Paint(_w, _h, g =>
        {
            // Drawing units: x along the rabbit (0 is the hip), y UP from the snow, scaled by _k.
            if (mirror) { g.TranslateTransform(_w, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(_ox, _by);
            g.ScaleTransform(_k, -_k);
            float edge = 1.4f / _k;
            for (int pass = 0; pass < 2; pass++) Rabbit_(g, pass, edge, p, earNear, earFar);
        });

    private static void Rabbit_(Graphics g, int pass, float edge, Pose p, float earNear, float earFar)
    {
        using var edgePen = new Pen(Edge, edge * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edgeBrush = new SolidBrush(Edge);

        // One oval at a spot, tilted. Pass 0 is its pale outline, pass 1 its color.
        void Oval(float cx, float cy, float rx, float ry, float tilt, Color c, bool outline = true)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);          // y is up, so a positive turn tips the right end UP
            using var path = new GraphicsPath();
            path.AddEllipse(-rx, -ry, rx * 2, ry * 2);
            if (pass == 0) { if (outline) { g.FillPath(edgeBrush, path); g.DrawPath(edgePen, path); } }
            else { using var b = new SolidBrush(c); g.FillPath(b, path); }
            g.Restore(st);
        }
        void Limb(PointF a, PointF b, float w, Color c)
        {
            using var pen = new Pen(pass == 0 ? Edge : c, pass == 0 ? w + edge * 2 : w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a, b);
        }

        float tr = p.Tilt * MathF.PI / 180;
        float ct = MathF.Cos(tr), st2 = MathF.Sin(tr);
        PointF hip = new(0, p.HipY);
        float bodyLen = 0.17f * p.Stretch;
        PointF sho = new(hip.X + bodyLen * ct, hip.Y + bodyLen * st2);          // the shoulder
        float beta = (25 + 0.2f * p.Tilt) * MathF.PI / 180;                      // which way the head sits from the shoulder
        PointF head = new(sho.X + 0.06f * MathF.Cos(beta), sho.Y + 0.06f * MathF.Sin(beta));
        float headTilt = p.Tilt * 0.3f - 8;

        // ---- the far ear, behind the head ----
        void Ear(float baseX, float baseY, float lean, Color tone, float len)
        {
            var s = g.Save();
            g.TranslateTransform(head.X + baseX, head.Y + baseY);
            g.RotateTransform(lean);                                              // positive = leaning back (toward the tail)
            Oval(0, len / 2, 0.03f, len / 2, 0, tone);
            if (pass == 1)
            {
                using var pink = new SolidBrush(Pink);
                using var path = new GraphicsPath();
                path.AddEllipse(-0.014f, 0.014f, 0.028f, len - 0.04f);
                g.FillPath(pink, path);
            }
            g.Restore(s);
        }
        Ear(0.005f, 0.03f, p.Ear - 7 + earFar, Haunch, 0.15f);

        // ---- tail, hind foot, haunch, body, front paws ----
        Oval(hip.X - 0.075f, hip.Y + 0.005f, 0.038f, 0.036f, 0, Fur);               // the round tail
        Limb(new PointF(hip.X + 0.01f, hip.Y - 0.045f), new PointF(p.HindX, p.HindY), 0.05f, Haunch);   // the hind foot
        Oval(p.HindX + (p.HindX > 0.02f ? 0.015f : -0.01f), p.HindY, 0.035f, 0.022f, 0, Haunch);
        Oval((hip.X + sho.X) / 2, (hip.Y + sho.Y) / 2, bodyLen / 2 + 0.065f, 0.082f, p.Tilt, Fur);   // the body
        Oval(hip.X, hip.Y - 0.005f, 0.09f, 0.078f, p.Tilt * 0.3f, Haunch);          // the big haunch muscle
        Limb(new PointF(sho.X, sho.Y - 0.03f), new PointF(p.ForeX, p.ForeY), 0.032f, Fur);    // the front paws

        // ---- the head ----
        {
            var s = g.Save();
            g.TranslateTransform(head.X, head.Y);
            g.RotateTransform(headTilt);
            Oval(0, 0, 0.066f, 0.054f, 0, Fur);
            Oval(0.05f, -0.014f, 0.032f, 0.026f, 0, Fur);                          // the muzzle
            if (pass == 1)
            {
                using var dark = new SolidBrush(Dark);
                g.FillEllipse(dark, 0.02f - 0.0085f, 0.012f - 0.0085f, 0.017f, 0.017f);                 // the eye
                using var hi = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                g.FillEllipse(hi, 0.023f - 0.003f, 0.016f - 0.003f, 0.006f, 0.006f);                    // a spark of light in it
                using var nose = new SolidBrush(Pink);
                g.FillEllipse(nose, 0.077f - 0.011f, -0.006f - 0.008f, 0.022f, 0.016f);
            }
            g.Restore(s);
        }
        Ear(-0.02f, 0.035f, p.Ear + earNear, Fur, 0.15f);                           // the near ear, in front of the head
    }
}
