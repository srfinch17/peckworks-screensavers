using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A wild turkey flaps in low over the hill, flares and lands on the cap of
/// one of the silos, and sits up there a good while, looking about, preening,
/// stretching its neck at something, before it drops off and flaps away
/// (his line: "some silos, and maybe a turkey that sometimes sits on one of
/// them"). Wild turkeys really do roost high, and a silo cap is the highest
/// perch on the farm.
///
/// FEYNMAN VERSION: three flip-books. The FLIGHT is this file's own small
/// painting (a turkey in the air: a heavy body, the neck out, the tail
/// folded, two wings beating about the shoulder), twelve pages a beat and
/// one flare page with the wings thrown up and the feet reaching down. The
/// PERCH reuses the walking turkey's painter (Turkey.PaintPage, the same
/// bird the field's flock is drawn with) at the barn's scale: a standing
/// page, a head-up "alert" page, and a head-down "preen" page. Which page
/// shows is read off the clock alone, so the showing can be tested from any
/// second (happening.ps1).
///
/// SCALE: the barn is 0.13 U wide for about twelve metres, so a 1.1 m
/// turkey beside it is 0.012 U, a sliver at 1080p. It is drawn a touch over
/// that (0.014 U) so the red wattle reads, and no more: a turkey as tall as
/// the silo's hatch would be the ridge deer all over again.
///
/// It is drawn through the painter's OpenFromFarm stencil: nothing painted
/// after the farm (the field, the fence, the maples) is ever covered by it,
/// and the silo itself is part of the farm, so the bird sits ON the cap.
/// </summary>
internal sealed class SiloRoost : Happening
{
    private const int FlapPages = 12;
    private const float FlapHz = 3.2f;
    private const float FlyIn = 2.6f, Flare = 0.45f, Crouch = 0.35f, FlyOut = 2.4f;
    private static readonly Color Bronze = Color.FromArgb(92, 58, 34), Dark = Color.FromArgb(58, 38, 24), WingDark = Color.FromArgb(74, 48, 30);
    private static readonly Color Buff = Color.FromArgb(222, 182, 118), HeadBlue = Color.FromArgb(110, 140, 196), Wattle = Color.FromArgb(206, 40, 44);

    private readonly ThanksgivingScenery _s;
    private readonly bool[] _stencil;
    private readonly float _k;                                  // pixels to the turkey-length unit
    private readonly int _fw, _pw, _ph, _pox, _pby;             // the flight sheet (square, the body's middle at its centre) and the perch sheet
    private readonly Sprite[][] _flap = new Sprite[2][];        // [dir][page]
    private readonly Sprite[] _flare = new Sprite[2];           // wings thrown up, feet down, the moment before the landing
    private readonly Sprite[] _stand = new Sprite[2], _alert = new Sprite[2], _preen = new Sprite[2], _crouch = new Sprite[2];

    private int _silo, _dir = 1;
    private float _sit = 24f;                                   // seconds on the cap this showing
    private PointF _feet;                                       // where the feet stand on the cap
    private PointF _a, _c, _b;                                  // the way in: start, pull point, landing (the body's middle)
    private PointF _xa, _xc, _xb;                               // the way out
    private float[] _looks = [];                                // seconds into the sit at which it lifts its head, each for about a second
    private float[] _preens = [];                               // seconds at which it preens

    public override float Seconds => FlyIn + Flare + _sit + Crouch + FlyOut;
    public override string? Claims => "silo";
    public override bool CanBegin => _s.SiloTops.Count > 0;

    public SiloRoost(ThanksgivingScenery s)
    {
        _s = s;
        _stencil = s.OpenFromFarm;
        _k = Math.Max(9f, s.U * 0.014f);
        _fw = (int)(1.7f * _k) + 4;
        _pw = (int)(1.45f * _k) + 4; _pox = (int)(0.80f * _k) + 2; _ph = (int)(1.08f * _k) + 8; _pby = _ph - 5;   // the walking turkey's own sheet layout
        for (int d = 0; d < 2; d++)
        {
            bool mirror = d == 1;
            _flap[d] = new Sprite[FlapPages];
            for (int i = 0; i < FlapPages; i++) _flap[d][i] = FlightPage(mirror, 72f * MathF.Cos(MathF.Tau * i / FlapPages), 0f, false);
            _flare[d] = FlightPage(mirror, 80f, 28f, true);
            _stand[d] = Turkey.PaintPage(_k, _pw, _ph, _pox, _pby, mirror, 0f, 0f, 0f, 0f, 0f, 0f, legs: 1f);
            _alert[d] = Turkey.PaintPage(_k, _pw, _ph, _pox, _pby, mirror, 0f, 0f, 0f, 0f, 0f, -0.18f, legs: 1f);   // the neck stretched up, as the flock's alert
            _preen[d] = Turkey.PaintPage(_k, _pw, _ph, _pox, _pby, mirror, 0f, 0f, 0f, 0f, 0f, 0.45f, legs: 1f);    // the head down at its breast
            _crouch[d] = Turkey.PaintPage(_k, _pw, _ph, _pox, _pby, mirror, 0f, 0f, 0f, 0f, 0f, 0.25f, legs: 1f);   // gathering to drop off
        }
    }

    public override void Begin(Random rng)
    {
        float U = _s.U, W = _s.Width;
        _silo = rng.Next(_s.SiloTops.Count);
        var (apex, half) = _s.SiloTops[_silo];
        _feet = new PointF(apex.X, apex.Y + half * 0.10f);                       // a hair down the cap's curve, so the toes are on it
        // It comes from whichever side of the screen is nearer, so the flight in is short and low, and leaves the other way.
        _dir = apex.X > W * 0.5f ? -1 : 1;
        _sit = 18f + 22f * rng.NextSingle();
        // The way in: a shallow dive from off the edge, a little above the cap, bending down onto it.
        _b = new PointF(_feet.X, _feet.Y - 0.42f * _k);                          // the body's middle when perched (the stand page's body sits about 0.42 up)
        _a = new PointF(_dir > 0 ? -2f * _k : W + 2f * _k, _b.Y - U * (0.05f + 0.05f * rng.NextSingle()));
        _c = new PointF(_b.X - _dir * MathF.Abs(_b.X - _a.X) * 0.30f, _b.Y - U * 0.015f);
        // The way out: a drop off the far side of the cap, then away and down toward the field's far edge, off the other edge.
        _xa = _b;
        _xb = new PointF(_dir > 0 ? W + 2f * _k : -2f * _k, _b.Y + U * (0.02f + 0.04f * rng.NextSingle()));
        _xc = new PointF(_xa.X + _dir * MathF.Abs(_xb.X - _xa.X) * 0.25f, _xa.Y + U * 0.03f);
        // The looks and the preens, dealt along the sit: a head-up every six seconds or so, a preen every ten, never together.
        var looks = new List<float>(); var preens = new List<float>();
        for (float t = 2f + 3f * rng.NextSingle(); t < _sit - 2f; t += 4f + 5f * rng.NextSingle()) looks.Add(t);
        for (float t = 5f + 4f * rng.NextSingle(); t < _sit - 3f; t += 8f + 6f * rng.NextSingle())
        {
            bool clear = true;
            foreach (float l in looks) if (MathF.Abs(l - t) < 2.2f) clear = false;
            if (clear) preens.Add(t);
        }
        _looks = looks.ToArray(); _preens = preens.ToArray();
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        int d = _dir > 0 ? 0 : 1;
        if (t < FlyIn + Flare)
        {
            // In: along the curve at an eased pace (fast off the edge, slowing into the cap), beating until the flare.
            float q = Smooth(t / (FlyIn + Flare));
            PointF p = Bezier(_a, _c, _b, q);
            Sprite page = t < FlyIn ? _flap[d][(int)(t * FlapHz * FlapPages) % FlapPages] : _flare[d];
            page.Draw(fb, (int)MathF.Round(p.X - _fw / 2f), (int)MathF.Round(p.Y - _fw / 2f), 1f, _stencil);
            return;
        }
        float u = t - FlyIn - Flare;
        if (u < _sit + Crouch)
        {
            // On the cap: standing, a head-up look, a preen, and the crouch before it drops.
            Sprite page = _stand[d];
            if (u >= _sit) page = _crouch[d];
            else
            {
                foreach (float l in _looks) if (u >= l && u < l + 1.1f) page = _alert[d];
                foreach (float pr in _preens) if (u >= pr && u < pr + 1.6f) page = ((int)((u - pr) * 4f)) % 2 == 0 ? _preen[d] : _stand[d];   // the head dips in and out, twice a second
            }
            page.Draw(fb, (int)MathF.Round(_feet.X - (d == 0 ? _pox : _pw - _pox)), (int)MathF.Round(_feet.Y) - _pby, 1f, _stencil);
            return;
        }
        // Out: it drops off the cap and flaps away, gathering speed.
        float v = u - _sit - Crouch;
        float r = v / FlyOut;
        PointF o = Bezier(_xa, _xc, _xb, r * r * (2f - r));                     // starts slow (the drop), ends at speed
        Sprite fly = _flap[d][(int)(v * FlapHz * FlapPages) % FlapPages];
        fly.Draw(fb, (int)MathF.Round(o.X - _fw / 2f), (int)MathF.Round(o.Y - _fw / 2f), 1f, _stencil);
    }

    private static PointF Bezier(PointF a, PointF c, PointF b, float q)
    {
        float m = 1 - q;
        return new PointF(m * m * a.X + 2 * m * q * c.X + q * q * b.X, m * m * a.Y + 2 * m * q * c.Y + q * q * b.Y);
    }

    /// <summary>
    /// One page of the turkey in the air, facing right (mirrored for left), in turkey units about the body's middle
    /// (y up): a heavy teardrop body, the tail folded and trailing, the neck stretched out ahead, and the two wings as
    /// rounded wedges turned "wing" degrees about the shoulder (up is positive). "pitch" tips the whole bird nose up
    /// (the flare); "feet" hangs the legs down for the landing.
    /// </summary>
    private Sprite FlightPage(bool mirror, float wing, float pitch, bool feet) => Sprite.Paint(_fw, _fw, g =>
    {
        if (mirror) { g.TranslateTransform(_fw, 0); g.ScaleTransform(-1, 1); }
        g.TranslateTransform(_fw / 2f, _fw / 2f);
        g.ScaleTransform(_k, -_k);
        g.RotateTransform(pitch);
        static PointF P(float x, float y) => new(x, y);
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        void Wing(float ang, Color c, float dx)
        {
            var st = g.Save();
            g.TranslateTransform(0.04f + dx, 0.06f);
            g.RotateTransform(ang);
            Blob([P(0f, 0.03f), P(0.22f, 0.06f), P(0.48f, 0.02f), P(0.52f, -0.04f), P(0.30f, -0.10f), P(0.06f, -0.08f)], c, 0.4f);   // the wing along +x, turned up by ang
            g.Restore(st);
        }
        // The far wing, beating a little behind the near one, then the body over it.
        Wing(wing * 0.85f + 10f, WingDark, -0.03f);
        Blob([P(0.30f, 0.06f), P(0.22f, 0.16f), P(0.00f, 0.20f), P(-0.22f, 0.14f), P(-0.30f, 0.02f), P(-0.20f, -0.12f), P(0.04f, -0.16f), P(0.26f, -0.08f)], Bronze);
        Blob([P(-0.24f, 0.10f), P(-0.50f, 0.14f), P(-0.64f, 0.06f), P(-0.62f, -0.06f), P(-0.46f, -0.08f), P(-0.26f, -0.06f)], Dark, 0.4f);   // the tail, folded, trailing
        using (var tip = new SolidBrush(Buff)) g.FillPolygon(tip, [P(-0.58f, 0.10f), P(-0.66f, 0.06f), P(-0.64f, -0.06f), P(-0.56f, -0.08f)]);   // its buff band
        using (var neck = new Pen(HeadBlue, 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(neck, P(0.28f, 0.04f), P(0.40f, 0.08f), P(0.50f, 0.10f), P(0.58f, 0.10f));
        using (var head = new SolidBrush(HeadBlue)) g.FillEllipse(head, 0.52f, 0.04f, 0.14f, 0.12f);
        using (var wat = new SolidBrush(Wattle)) g.FillEllipse(wat, 0.56f, -0.01f, 0.05f, 0.06f);
        using (var beak = new SolidBrush(Buff)) g.FillPolygon(beak, [P(0.66f, 0.11f), P(0.74f, 0.09f), P(0.66f, 0.07f)]);
        if (feet)
            using (var leg = new Pen(Color.FromArgb(182, 132, 122), 0.03f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(leg, P(0.02f, -0.12f), P(0.06f, -0.34f));
                g.DrawLine(leg, P(-0.06f, -0.12f), P(-0.02f, -0.34f));
            }
        Wing(wing, Dark, 0f);
    });
}
