using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Aosagi, the grey heron. It is standing in the shallows at the foot of the
/// right-hand hill when the showing starts (it fades in), perfectly still. It
/// slowly tilts its head to look at the water, coils its neck like a spring,
/// and STRIKES: the neck shoots out and the bill stabs into the lake (a
/// splash and rings). It lifts a small silver fish, tosses it up to turn it
/// round, swallows it head first (you can see the lump slide down the neck),
/// shakes itself, and stands tall again. Then it fades away.
///
/// FEYNMAN VERSION: a heron is mostly a neck. So each "page" of this
/// flip-book is the same heron (body and legs never change) with the neck and
/// head drawn from a handful of numbers: where the head is, how much the neck
/// bends in an S, which way the bill points. A "pose" is those numbers. We
/// make groups of pages by sliding from one pose to another in small steps:
/// tilt (6 pages), coil (8), strike (14), toss (8), swallow (10), shake (8).
/// Draw is told "t seconds in" and picks the page from a timetable.
///
/// The reflection is the same page painted upside down, then faded and
/// wobbled a few rows at a time, and stamped only where the lake is open (so
/// it never lands on the hill's grass).
///
/// The fish is a separate little picture stamped at the tip of the bill, so
/// the same fish rides in the bill, flies up when tossed and is gone when
/// swallowed.
/// </summary>
internal sealed class Heron : Happening
{
    // The timetable, in seconds.
    private const float TTotal = 13f;
    private const float TTilt = 3.2f, TTiltEnd = 5.0f;
    private const float TCoil = 6.3f, TStrike = 7.2f, TStrikeEnd = 7.34f, THoldEnd = 7.75f, TLiftEnd = 8.45f;
    private const float TFlipEnd = 9.15f, TSwallowEnd = 10.4f, TShakeEnd = 11.4f;

    // A pose: where the head is, how S-bent the neck is, where the bill points,
    // and where the swallowed lump is along the neck (-1 = none).
    private readonly record struct Spec(float Hx, float Hy, float Kink, float Bill, float Bulge)
    {
        public static Spec Lerp(Spec a, Spec b, float s) => new(
            a.Hx + (b.Hx - a.Hx) * s, a.Hy + (b.Hy - a.Hy) * s, a.Kink + (b.Kink - a.Kink) * s,
            a.Bill + (b.Bill - a.Bill) * s, s < 0.001f ? a.Bulge : b.Bulge < -0.5f ? a.Bulge : b.Bulge);
    }

    private static readonly Spec Stand = new(-0.19f, -0.93f, 1.0f, 6f, -1f);
    private static readonly Spec Look = new(-0.21f, -0.90f, 0.9f, 28f, -1f);          // tipped to look at the water
    private static readonly Spec Coil = new(-0.06f, -0.86f, 1.5f, -6f, -1f);          // head pulled back, neck a tight S
    private static readonly Spec Hit = new(-0.50f, -0.10f, 0.0f, 74f, -1f);           // neck out straight, bill in the water
    private static readonly Spec Up = new(-0.17f, -0.97f, 0.8f, -52f, -1f);           // head thrown back to swallow

    private sealed class Pg
    {
        public required Sprite Body, Refl;
        public float TipX, TipY, BillDeg;       // the bill tip, in heron heights from the feet
    }

    private readonly Scenery _s;
    private readonly float _h;                  // the heron's height in pixels
    private readonly int _w, _top, _ox, _reflH; // page size, rows above the feet, feet column, rows of reflection
    private readonly Pg _stand;
    private readonly Pg[] _tilt = new Pg[6], _coil = new Pg[8], _strike = new Pg[14], _toss = new Pg[8], _swallow = new Pg[10], _shake = new Pg[8];
    private readonly Sprite[] _fish = new Sprite[16];
    private readonly int _fishSide;

    // Where it stands (found from the pixels in the constructor).
    private readonly bool _found;
    private readonly float _fx, _fy;            // the feet
    private readonly float[] _dvx = new float[10], _dvy = new float[10];

    public override float Seconds => _found ? TTotal : 0.3f;
    public override bool CanBegin => _found;
    public override int Layer => 1;                         // in front of the lake, boat and glints
    public override string? Claims => "righthill";

    private static readonly Color Edge = Color.FromArgb(150, 70, 84, 104);

    public Heron(Scenery s)
    {
        _s = s;
        _h = Math.Max(26f, s.U * 0.12f);
        _ox = (int)(1.05f * _h);
        _w = (int)(1.45f * _h);
        _top = (int)(1.12f * _h);
        _reflH = (int)(0.8f * _h);

        // Find its spot: the water just off the hill's left end, where the hill rises out of the lake.
        // Feet go a little above the hill's rim there, on open water; try a few places and keep the first that is open all round.
        bool[] open = s.OpenBehindBanks;
        float fx = 0, fy = 0;
        bool ok = false;
        for (float fr = 0.54f; fr <= 0.605f && !ok; fr += 0.005f)
        {
            float x = s.Width * fr;
            float y = s.RightHill.YAt(x) - s.U * 0.035f;
            if (y < s.HorizonY + s.U * 0.14f) continue;
            if (Open(open, s, x, y) && Open(open, s, x - 0.04f * _h, y) && Open(open, s, x + 0.12f * _h, y) &&
                Open(open, s, x - 0.55f * _h, y) && Open(open, s, x, y - 0.9f * _h))
            { fx = x; fy = y; ok = true; }
        }
        _found = ok;
        _fx = fx; _fy = fy;
        if (!ok) { _stand = null!; return; }

        _stand = Make(Stand);
        for (int i = 0; i < _tilt.Length; i++) _tilt[i] = Make(Spec.Lerp(Stand, Look, Smooth(i / (float)(_tilt.Length - 1))));
        for (int i = 0; i < _coil.Length; i++) _coil[i] = Make(Spec.Lerp(Stand, Coil, Smooth(i / (float)(_coil.Length - 1))));
        for (int i = 0; i < _strike.Length; i++) _strike[i] = Make(StrikeSpec(i / (float)(_strike.Length - 1)));
        for (int i = 0; i < _toss.Length; i++) _toss[i] = Make(Spec.Lerp(StrikeSpec(5f / 13f), Up, Smooth(i / (float)(_toss.Length - 1))));
        for (int i = 0; i < _swallow.Length; i++)
        {
            float k = i / (float)(_swallow.Length - 1);
            var sp = Spec.Lerp(Up, Stand, Smooth(Math.Clamp((k - 0.45f) / 0.55f, 0f, 1f)));
            _swallow[i] = Make(sp with { Bulge = k });
        }
        for (int i = 0; i < _shake.Length; i++)
        {
            float a = MathF.Tau * i / _shake.Length;
            _shake[i] = Make(new Spec(Stand.Hx + 0.035f * MathF.Sin(a), Stand.Hy + 0.012f * MathF.Cos(2 * a), Stand.Kink + 0.25f * MathF.Cos(a), Stand.Bill + 9f * MathF.Sin(a), -1f));
        }

        _fishSide = Math.Max(6, (int)(0.32f * _h));
        for (int i = 0; i < _fish.Length; i++) _fish[i] = MakeFish(i * 360f / _fish.Length);
    }

    private static bool Open(bool[] open, Scenery s, float x, float y)
    {
        int xi = (int)x, yi = (int)y;
        return xi >= 0 && xi < s.Width && yi >= 0 && yi < s.Height && open[yi * s.Width + xi];
    }

    /// <summary>The strike: from the coil to the bill in the water, s = 0 to 1. The neck straightens as it goes.</summary>
    private static Spec StrikeSpec(float s) => Spec.Lerp(Coil, Hit, s);

    public override void Begin(Random rng)
    {
        for (int i = 0; i < _dvx.Length; i++)
        {
            float a = (i + 0.5f) / _dvx.Length * 2 - 1 + (float)(rng.NextDouble() - 0.5) * 0.1f;
            _dvx[i] = a * 0.10f;
            _dvy[i] = 0.2f + 0.2f * (1 - MathF.Abs(a)) * (float)rng.NextDouble() + 0.04f * (float)rng.NextDouble();
        }
    }

    // ---------------------------------------------------------------- draw

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_found) return;
        bool[] st = _s.OpenBehindBanks;
        float vis = Fade(t, TTotal, 1.8f, 1.5f);
        if (vis < 0.01f) return;

        // ---- which page, and is there a fish? ----
        Pg cur = _stand;
        bool fish = false;
        float fishLift = 0, fishAng = 0, fishFade = 1;
        if (t >= TTilt && t < TTiltEnd)
        {
            float u = (t - TTilt) / (TTiltEnd - TTilt);
            float tri = u < 0.4f ? Smooth(u / 0.4f) : u < 0.62f ? 1f : Smooth((1 - u) / 0.38f);   // tip, hold, come back
            cur = _tilt[(int)MathF.Round(tri * (_tilt.Length - 1))];
        }
        else if (t >= TCoil && t < TStrike)
            cur = _coil[(int)MathF.Round(Smooth((t - TCoil) / (TStrike - TCoil)) * (_coil.Length - 1))];
        else if (t >= TStrike && t < TStrikeEnd)
        {
            float u = (t - TStrike) / (TStrikeEnd - TStrike);
            cur = _strike[(int)MathF.Round((1 - (1 - u) * (1 - u)) * (_strike.Length - 1))];     // fast at the start, braking at the end
        }
        else if (t >= TStrikeEnd && t < THoldEnd)
            cur = _strike[^1];
        else if (t >= THoldEnd && t < TLiftEnd)
        {
            float u = Smooth((t - THoldEnd) / (TLiftEnd - THoldEnd));
            cur = _strike[(int)MathF.Round(13 - u * 8)];                                          // pull back to page 5
            fish = u > 0.12f; fishAng = 70f; fishLift = 0;
        }
        else if (t >= TLiftEnd && t < TFlipEnd)
        {
            float u = (t - TLiftEnd) / (TFlipEnd - TLiftEnd);
            cur = _toss[(int)MathF.Round(Smooth(Math.Min(1f, u * 1.1f)) * (_toss.Length - 1))];
            fish = true;
            float f = Math.Clamp(u / 0.85f, 0f, 1f);
            fishLift = 0.12f * MathF.Sin(f * MathF.PI);                                           // the toss: up and back down
            fishAng = 70f + 200f * Smooth(f);                                                     // turning over so it goes in head first
            if (u > 0.85f) fishFade = 1 - (u - 0.85f) / 0.15f;
        }
        else if (t >= TFlipEnd && t < TSwallowEnd)
            cur = _swallow[(int)MathF.Round((t - TFlipEnd) / (TSwallowEnd - TFlipEnd) * (_swallow.Length - 1))];
        else if (t >= TSwallowEnd && t < TShakeEnd)
        {
            float u = (t - TSwallowEnd) / (TShakeEnd - TSwallowEnd);
            float cyc = u * 3f;                                                                   // three quick shakes
            cur = _shake[(int)((cyc - MathF.Floor(cyc)) * _shake.Length) % _shake.Length];
        }

        // ---- the water: constant faint ripples round the legs, then the strike's splash ----
        Wading(fb, t, st, vis);
        Strike(fb, t, st);

        // ---- the heron and its reflection ----
        int left = (int)MathF.Round(_fx - _ox);
        cur.Refl.Draw(fb, left, (int)MathF.Round(_fy), 0.34f * vis, st);
        cur.Body.Draw(fb, left, (int)MathF.Round(_fy) - _top, vis);

        if (fish && fishFade > 0.01f)
        {
            float bx = _fx + cur.TipX * _h, by = _fy + cur.TipY * _h - fishLift * _h;
            // the fish rides a little behind the very tip, as a bird really holds it
            float dirx = -MathF.Cos(cur.BillDeg * MathF.PI / 180f), diry = MathF.Sin(cur.BillDeg * MathF.PI / 180f);
            bx -= dirx * 0.03f * _h; by -= diry * 0.03f * _h;
            int idx = (int)MathF.Round(((fishAng % 360f) + 360f) % 360f / 22.5f) % _fish.Length;
            _fish[idx].DrawCentered(fb, bx, by, vis * fishFade);
        }
    }

    /// <summary>Soft slow ripples round each leg, one ring every 3 seconds, so it clearly stands IN the water.</summary>
    private void Wading(FrameBuffer fb, float t, bool[] st, float vis)
    {
        for (int leg = 0; leg < 2; leg++)
        {
            float age = (t + leg * 1.5f) % 3f;
            Ring(fb, _fx + (leg == 0 ? -0.02f : 0.07f) * _h, _fy + 0.01f * _h, age, 3f, 0.045f * _h, 0.3f * vis, Math.Max(1f, _h * 0.012f), st);
        }
    }

    /// <summary>The splash when the bill goes in: rings, a few thin jets and droplets thrown up.</summary>
    private void Strike(FrameBuffer fb, float t, bool[] st)
    {
        float age = t - (TStrike + 0.1f);
        if (age < 0) return;
        float tx = _fx + _strike[^1].TipX * _h, ty = _fy + _strike[^1].TipY * _h;
        float w = Math.Max(1f, _h * 0.012f);
        Ring(fb, tx, ty, age, 2.2f, 0.34f * _h, 0.8f, w, st);
        Ring(fb, tx, ty, age - 0.3f, 2.0f, 0.28f * _h, 0.55f, w, st);
        // Pulling out: a second, smaller ring where the bill leaves.
        Ring(fb, tx, ty, t - THoldEnd, 1.6f, 0.2f * _h, 0.5f, w, st);

        if (age < 0.9f)
        {
            const float g = 1.5f;
            for (int i = 0; i < _dvx.Length; i++)
            {
                float up = _dvy[i] * age - g * age * age / 2;
                if (up < 0) continue;
                float x = tx + _s.U * _dvx[i] * age * 1.4f, y = ty - _s.U * up;
                float a = Math.Min(1f, (0.9f - age) * 3f);
                Dot(fb, x, y, Math.Max(1f, _s.U * 0.0017f * (0.8f + (i % 3) * 0.3f)), 0.85f * a, st);
            }
        }
    }

    private static void Dot(FrameBuffer fb, float cx, float cy, float r, float alpha, bool[] st)
    {
        for (int y = Math.Max(0, (int)(cy - r - 1)); y <= Math.Min(fb.Height - 1, (int)(cy + r + 1)); y++)
            for (int x = Math.Max(0, (int)(cx - r - 1)); x <= Math.Min(fb.Width - 1, (int)(cx + r + 1)); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float cover = Math.Clamp(r + 0.5f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                int at = y * fb.Width + x;
                if (cover <= 0 || !st[at]) continue;
                fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], 240, 249, 255, alpha * cover);
            }
    }

    /// <summary>A flattened ring on the water, worked out per pixel (see Kingfisher.Ring for how).</summary>
    private void Ring(FrameBuffer fb, float cx, float cy, float age, float life, float reach, float strength, float width, bool[] st)
    {
        if (age <= 0 || age > life) return;
        float q = age / life;
        float rx = _h * 0.02f + reach * MathF.Pow(q, 0.62f), ry = rx * 0.27f;
        float a = strength * 0.6f * MathF.Pow(1 - q, 1.4f) * Smooth(age / 0.08f);
        if (a < 0.02f) return;
        int x0 = Math.Max(0, (int)(cx - rx - width - 2)), x1 = Math.Min(fb.Width - 1, (int)(cx + rx + width + 2));
        int y0 = Math.Max(0, (int)(cy - ry - width - 2)), y1 = Math.Min(fb.Height - 1, (int)(cy + ry + width + 2));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float ex = (x + 0.5f - cx) / rx, ey = (y + 0.5f - cy) / ry;
                float d = MathF.Sqrt(ex * ex + ey * ey);
                if (d < 0.01f) continue;
                float gx = ex / (rx * d), gy = ey / (ry * d);
                float dist = (d - 1) / MathF.Sqrt(gx * gx + gy * gy);
                float cover = Math.Clamp(width / 2 + 0.5f - MathF.Abs(dist), 0f, 1f);
                if (cover <= 0) continue;
                int at = y * fb.Width + x;
                if (!st[at]) continue;
                float side = 0.6f + 0.4f * Math.Clamp(ey, -1f, 1f);
                fb.Pixels[at] = FrameBuffer.Blend(fb.Pixels[at], 238, 247, 255, a * cover * side);
            }
    }

    // ---------------------------------------------------------------- painting

    private static PointF P(float x, float y) => new(x, y);

    /// <summary>The neck curve: a cubic Bezier from the shoulder to the head, S-bent by "kink". Returns the point at s (0 = shoulder, 1 = head).</summary>
    private static PointF NeckAt(Spec sp, float s)
    {
        PointF a = P(-0.17f, -0.665f);
        PointF d = HeadJoin(sp);
        PointF b = P(a.X + 0.11f * sp.Kink, a.Y - 0.12f);
        PointF c = P(d.X + 0.11f * sp.Kink, d.Y + 0.10f);
        float m = 1 - s;
        return P(m * m * m * a.X + 3 * m * m * s * b.X + 3 * m * s * s * c.X + s * s * s * d.X,
                 m * m * m * a.Y + 3 * m * m * s * b.Y + 3 * m * s * s * c.Y + s * s * s * d.Y);
    }

    /// <summary>Where the neck meets the head: a little behind the head's middle, turned with the bill.</summary>
    private static PointF HeadJoin(Spec sp)
    {
        float r = -sp.Bill * MathF.PI / 180f;                       // the head is turned by this much
        float lx = 0.04f, ly = 0.012f;
        return P(sp.Hx + lx * MathF.Cos(r) - ly * MathF.Sin(r), sp.Hy + lx * MathF.Sin(r) + ly * MathF.Cos(r));
    }

    private Pg Make(Spec sp)
    {
        float billRad = sp.Bill * MathF.PI / 180f;
        // the bill tip: 0.245 along the bill from the head's middle
        float tipX = sp.Hx - 0.245f * MathF.Cos(billRad), tipY = sp.Hy + 0.245f * MathF.Sin(billRad);
        Sprite body = Sprite.Paint(_w, _top + (int)(0.12f * _h), g =>
        {
            g.TranslateTransform(_ox, _top);
            g.ScaleTransform(_h, _h);
            Heron_(g, sp);
        });

        // The reflection: paint the heron upside down on a scratch sheet, then copy it
        // across a few rows at a time, each strip a touch fainter and a touch shifted sideways.
        Sprite refl;
        using (var scratch = new Bitmap(_w, _reflH, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(scratch))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.TranslateTransform(_ox, 0);
                g.ScaleTransform(_h, -_h);
                Heron_(g, sp);
            }
            refl = Sprite.Paint(_w, _reflH, g =>
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                const int strip = 2;
                for (int y = 0; y < _reflH; y += strip)
                {
                    float fade = MathF.Pow(1 - y / (float)_reflH, 1.6f);
                    int shift = (int)MathF.Round(MathF.Sin(y * 0.33f) * 0.009f * _h * (0.4f + y / (float)_reflH));
                    var m = new ColorMatrix { Matrix33 = fade };
                    using var ia = new ImageAttributes();
                    ia.SetColorMatrix(m);
                    int hh = Math.Min(strip, _reflH - y);
                    g.DrawImage(scratch, new Rectangle(shift, y, _w, hh), 0, y, _w, hh, GraphicsUnit.Pixel, ia);
                }
            });
        }
        return new Pg { Body = body, Refl = refl, TipX = tipX, TipY = tipY, BillDeg = sp.Bill };
    }

    /// <summary>The whole heron in heron-heights, feet at (0,0), y DOWN, facing left.</summary>
    private static void Heron_(Graphics g, Spec sp)
    {
        using var edge = new Pen(Edge, 0.013f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        Color legC = Color.FromArgb(206, 184, 92), legD = Color.FromArgb(150, 128, 60);

        // legs: long, thin, yellowish, the joint bending backward
        void Leg(float hx, float jx, float fx, float jy)
        {
            using var pen = new Pen(legC, 0.017f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var dark = new Pen(legD, 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(dark, [P(hx, -0.5f), P(jx, jy), P(fx, -0.005f)]);
            g.DrawLines(pen, [P(hx, -0.5f), P(jx, jy), P(fx, -0.005f)]);
            g.DrawLine(pen, P(fx, -0.005f), P(fx - 0.06f, 0.012f));
            g.DrawLine(pen, P(fx, -0.005f), P(fx + 0.03f, 0.012f));
        }
        Leg(0.08f, 0.115f, 0.075f, -0.26f);
        Leg(-0.03f, 0.005f, -0.02f, -0.27f);

        // tail feathers
        using (var b = new SolidBrush(Color.FromArgb(120, 134, 150)))
            g.FillPolygon(b, [P(0.17f, -0.62f), P(0.31f, -0.44f), P(0.27f, -0.45f), P(0.13f, -0.50f)]);

        // body: a tilted oval, chest up
        var st = g.Save();
        g.TranslateTransform(0.04f, -0.57f);
        g.RotateTransform(24f);
        using var bodyPath = new GraphicsPath();
        bodyPath.AddEllipse(-0.21f, -0.10f, 0.42f, 0.20f);
        using (var b = new SolidBrush(Color.FromArgb(176, 188, 200))) g.FillPath(b, bodyPath);
        g.SetClip(bodyPath, CombineMode.Intersect);
        using (var b = new SolidBrush(Color.FromArgb(226, 231, 235))) g.FillEllipse(b, -0.26f, -0.01f, 0.30f, 0.15f);       // paler chest and belly
        using (var b = new SolidBrush(Color.FromArgb(138, 152, 170))) g.FillEllipse(b, -0.06f, -0.12f, 0.30f, 0.12f);      // grey folded wing
        using (var pen = new Pen(Color.FromArgb(54, 62, 78), 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, P(-0.03f, -0.005f), P(0.2f, -0.035f));                                                           // dark flight feathers along the wing's edge
        using (var b = new SolidBrush(Color.FromArgb(36, 40, 52))) g.FillEllipse(b, -0.18f, -0.09f, 0.07f, 0.045f);          // the black shoulder patch
        g.ResetClip();
        g.DrawPath(edge, bodyPath);
        g.Restore(st);

        // long white plumes hanging from the chest
        using (var pen = new Pen(Color.FromArgb(240, 242, 244), 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(pen, P(-0.15f, -0.58f), P(-0.15f, -0.45f));
            g.DrawLine(pen, P(-0.11f, -0.55f), P(-0.105f, -0.42f));
            g.DrawLine(pen, P(-0.19f, -0.6f), P(-0.2f, -0.49f));
        }

        // ---- the neck: a chain of discs along the curve, thinner toward the head ----
        const int steps = 36;
        PointF[] pts = new PointF[steps + 1];
        float stretch = Math.Clamp(1f - sp.Kink * 0.5f, 0f, 1f);            // a straight, stretched neck is thinner
        for (int i = 0; i <= steps; i++) pts[i] = NeckAt(sp, i / (float)steps);
        float Rad(int i) => (0.044f - 0.010f * stretch) - (0.020f - 0.005f * stretch) * (i / (float)steps);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i <= steps; i++)
            {
                float r = Rad(i) + (pass == 0 ? 0.008f : 0f);
                using var b = new SolidBrush(pass == 0 ? Color.FromArgb(150, 118, 132, 150) : Color.FromArgb(246, 246, 242));
                g.FillEllipse(b, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
            }
        // grey streaks down the front of the neck
        using (var pen = new Pen(Color.FromArgb(120, 70, 80, 96), 0.009f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 4; i < steps - 5; i += 4)
            {
                PointF a = pts[i], bb = pts[i + 1];
                float dx = bb.X - a.X, dy = bb.Y - a.Y, n = MathF.Sqrt(dx * dx + dy * dy);
                if (n < 1e-5f) continue;
                float nx = -dy / n, ny = dx / n;
                if (nx > 0) { nx = -nx; ny = -ny; }                          // the front is the left side
                float r = Rad(i) * 0.5f;
                g.DrawLine(pen, P(a.X + nx * r, a.Y + ny * r), P(a.X + nx * r + dx * 1.6f, a.Y + ny * r + dy * 1.6f));
            }
        // the swallowed fish: a lump sliding down the neck
        if (sp.Bulge >= 0)
        {
            int i = (int)((1 - Math.Clamp(sp.Bulge, 0f, 1f)) * (steps - 3)) + 1;
            float r = Rad(i) + 0.016f;
            using var b = new SolidBrush(Color.FromArgb(230, 218, 226, 234));
            g.FillEllipse(b, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
            using var hi = new SolidBrush(Color.FromArgb(160, 170, 190, 210));
            g.FillEllipse(hi, pts[i].X - r * 0.5f, pts[i].Y - r * 0.2f, r, r * 0.8f);
        }

        // ---- the head, drawn facing left and then turned to the bill angle ----
        var save = g.Save();
        g.TranslateTransform(sp.Hx, sp.Hy);
        g.RotateTransform(-sp.Bill);
        // black plume streaming back
        using (var b = new SolidBrush(Color.FromArgb(24, 26, 34)))
        {
            g.FillPolygon(b, [P(0.03f, -0.03f), P(0.20f, -0.005f), P(0.04f, -0.008f)]);
            g.FillPolygon(b, [P(0.035f, -0.015f), P(0.17f, 0.035f), P(0.04f, 0.005f)]);
        }
        using var head = new GraphicsPath();
        head.AddEllipse(-0.06f, -0.045f, 0.13f, 0.09f);
        using (var b = new SolidBrush(Color.FromArgb(250, 250, 246))) g.FillPath(b, head);
        g.DrawPath(edge, head);
        // the black stripe from the eye to the back of the head
        using (var pen = new Pen(Color.FromArgb(24, 26, 34), 0.02f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, P(-0.012f, -0.02f), P(0.075f, -0.027f));
        // long yellow dagger bill
        PointF[] bill = [P(-0.045f, -0.024f), P(-0.245f, 0.004f), P(-0.045f, 0.026f)];
        using (var b = new SolidBrush(Color.FromArgb(242, 202, 70))) g.FillPolygon(b, bill);
        using (var b = new SolidBrush(Color.FromArgb(200, 214, 150, 50))) g.FillPolygon(b, [P(-0.05f, 0.008f), P(-0.245f, 0.004f), P(-0.05f, 0.026f)]);
        using (var pen = new Pen(Color.FromArgb(120, 150, 100, 30), 0.008f)) g.DrawPolygon(pen, bill);
        // the eye: a yellow ring, a dark pupil, one bright highlight
        using (var b = new SolidBrush(Color.FromArgb(244, 214, 80))) g.FillEllipse(b, -0.043f, -0.03f, 0.041f, 0.041f);
        using (var b = new SolidBrush(Color.FromArgb(14, 14, 20))) g.FillEllipse(b, -0.0365f, -0.0235f, 0.028f, 0.028f);
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, -0.0315f, -0.0225f, 0.011f, 0.011f);
        g.Restore(save);
    }

    /// <summary>A small silver fish, head toward the given angle (degrees, 0 = pointing left, positive = turning down).</summary>
    private Sprite MakeFish(float deg)
    {
        float len = 0.2f * _h;
        return Sprite.Paint(_fishSide, _fishSide, g =>
        {
            g.TranslateTransform(_fishSide / 2f, _fishSide / 2f);
            g.ScaleTransform(len, len);
            g.RotateTransform(-deg);                // 0 = head to the left
            using var body = new GraphicsPath();
            body.AddEllipse(-0.5f, -0.17f, 1.0f, 0.34f);
            using (var b = new SolidBrush(Color.FromArgb(226, 236, 244))) g.FillPath(b, body);
            using (var b = new SolidBrush(Color.FromArgb(112, 144, 170))) g.FillEllipse(b, -0.46f, -0.17f, 0.92f, 0.14f);
            using (var b = new SolidBrush(Color.FromArgb(214, 228, 240))) g.FillPolygon(b, [P(0.42f, 0), P(0.72f, -0.2f), P(0.64f, 0), P(0.72f, 0.2f)]);
            using (var b = new SolidBrush(Color.FromArgb(18, 18, 26))) g.FillEllipse(b, -0.4f, -0.07f, 0.1f, 0.1f);
            using (var pen = new Pen(Color.FromArgb(150, 50, 80, 110), 0.04f)) g.DrawPath(pen, body);
        });
    }
}
