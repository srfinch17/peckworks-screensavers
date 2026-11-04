using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// An old red tractor with big back wheels and one lit headlamp trundles across
/// the far field pulling a hay wagon heaped with hay, puffing a little grey
/// exhaust now and then. It crosses along the field's lane (behind the scarecrow,
/// the fence and the near ground) from one side to the other.
///
/// FEYNMAN VERSION: the tractor and wagon are ONE picture, painted once with no
/// wheels (the "body"). The wheels are separate little pictures stamped on top,
/// because a wheel must TURN as the machine rolls and the body must not. A
/// wheel's turn is not a clock, it is the ground: a wheel of radius R that has
/// rolled a distance d has turned d / R radians. We paint 12 pages of a wheel
/// (six spokes, so the picture repeats every 60 degrees, so 12 pages are 5
/// degrees apart) and pick the page from the distance rolled. Big wheels turn
/// slowly, small ones fast, and none of them can slide, because they are all
/// worked out from the same distance. The tractor goes left to right (or the
/// mirror image: the body is flipped, the wheel pages are run backwards).
///
/// Exhaust puffs are the toy train's trick: puff number j left the stack at time
/// j * 1.1 s, from wherever the stack WAS then, and from t alone we know how old
/// it is, so how high it has floated and how faded it is.
///
/// Stencil: the field's lane is behind the scarecrow, the fence, the near ground and
/// the maples. The painter's OpenField stencil is exactly that ("nothing painted
/// after the field and its bales"; the scarecrow is painted after them).
/// The speed is in U per second so a wide screen takes longer, and the wheels never race.
/// </summary>
internal sealed class Tractor : Happening
{
    private const float Cruise = 0.115f;        // U per second
    private const int WheelPages = 12;          // one sixth of a turn (six spokes), so 5 degrees a page
    private const float PuffEvery = 1.1f, PuffLife = 2.6f;

    private static readonly Color Ink = Color.FromArgb(58, 30, 24);
    private static readonly Color Red = Color.FromArgb(178, 44, 34);
    private static readonly Color RedLit = Color.FromArgb(214, 76, 56);
    private static readonly Color Wood = Color.FromArgb(106, 74, 46);
    private static readonly Color WoodLit = Color.FromArgb(142, 102, 62);
    private static readonly Color Hay = Color.FromArgb(222, 176, 74);
    private static readonly Color HayDark = Color.FromArgb(168, 118, 44);

    private readonly ThanksgivingScenery _s;
    private readonly bool[] _stencil;
    private readonly float _t;                  // the tractor unit in pixels: the rear wheel is a unit across
    private readonly int _len, _h, _gy;         // body sheet: length, height, and the ground line in it
    private readonly Sprite[] _body = new Sprite[2];          // [0] heads right, [1] heads left (the same sheet flipped)
    private readonly Sprite[] _rear = new Sprite[WheelPages], _front = new Sprite[WheelPages], _cart = new Sprite[WheelPages];
    private readonly float _rRear, _rFront, _rCart;           // wheel radii in pixels
    private readonly Sprite _lamp, _puff;
    private readonly float _trip;
    private bool _left;

    // Where each wheel's middle and the stack are, in units from the wagon's tail, for a machine heading right.
    private const float RearX = 3.55f, FrontX = 4.95f, CartX0 = 0.60f, CartX1 = 2.05f, StackX = 4.78f, LampX = 5.20f;

    public override float Seconds => _trip;
    public override string? Claims => "field,costly";

    public Tractor(ThanksgivingScenery s)
    {
        _s = s;
        _stencil = s.OpenField;
        _t = Math.Max(9f, s.U * 0.040f);
        _len = (int)(5.35f * _t) + 2;
        _h = (int)(1.75f * _t) + 3;
        _gy = _h - 2;
        _trip = (s.Width + _len) / (Cruise * s.U) + 0.1f;
        _rRear = 0.50f * _t; _rFront = 0.28f * _t; _rCart = 0.30f * _t;

        _body[0] = PaintBody(false);
        _body[1] = PaintBody(true);
        for (int i = 0; i < WheelPages; i++)
        {
            _rear[i] = PaintWheel(_rRear, i, Color.FromArgb(206, 64, 46), true);
            _front[i] = PaintWheel(_rFront, i, Color.FromArgb(206, 64, 46), false);
            _cart[i] = PaintWheel(_rCart, i, Color.FromArgb(206, 160, 98), false);
        }
        _lamp = Sprite.Glow(Math.Max(3, (int)(s.U * 0.020f)), Color.FromArgb(255, 226, 150));
        _puff = Sprite.Glow(Math.Max(2, (int)(s.U * 0.010f)), Color.FromArgb(206, 192, 180));
    }


    /// <summary>One wheel page: dark tyre, dark inner disc, six bright spokes turned by page * 5 degrees, a hub.</summary>
    private static Sprite PaintWheel(float r, int page, Color spokeColour, bool big)
    {
        int size = (int)MathF.Ceiling(r * 2) + 2;
        return Sprite.Paint(size, size, g =>
        {
            float c = size / 2f;
            using (var tyre = new SolidBrush(Color.FromArgb(26, 22, 22))) g.FillEllipse(tyre, c - r, c - r, r * 2, r * 2);
            float ri = r * (big ? 0.74f : 0.70f);
            using (var disc = new SolidBrush(Color.FromArgb(70, 46, 36))) g.FillEllipse(disc, c - ri, c - ri, ri * 2, ri * 2);
            using (var spoke = new Pen(spokeColour, Math.Max(1f, r * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int k = 0; k < 6; k++)
                {
                    float a = (MathF.PI / 3f) * (k + page / (float)WheelPages);      // clockwise on screen: GDI's y points down
                    g.DrawLine(spoke, c, c, c + MathF.Cos(a) * ri * 0.95f, c + MathF.Sin(a) * ri * 0.95f);
                }
            using (var hub = new SolidBrush(spokeColour)) g.FillEllipse(hub, c - r * 0.16f, c - r * 0.16f, r * 0.32f, r * 0.32f);
        });
    }

    /// <summary>The wagon and the tractor without wheels, heading right (or flipped to head left).</summary>
    private Sprite PaintBody(bool flip) => Sprite.Paint(_len, _h, g =>
    {
        if (flip) { g.TranslateTransform(_len, 0); g.ScaleTransform(-1, 1); }
        float T = _t, gy = _gy;
        float edge = Math.Max(1f, T * 0.04f);
        using var ink = new Pen(Ink, edge) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        PointF P(float x, float y) => new(1 + x * T, gy - y * T);               // units from the tail, y UP from the ground
        void Poly(Color c, params PointF[] pts) { using var b = new SolidBrush(c); g.FillPolygon(b, pts); g.DrawPolygon(ink, pts); }
        RectangleF R(float x0, float y0, float x1, float y1) { var a = P(x0, y1); var b = P(x1, y0); return new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y); }
        void Box(Color c, RectangleF rc) { using var b = new SolidBrush(c); g.FillRectangle(b, rc); g.DrawRectangle(ink, rc.X, rc.Y, rc.Width, rc.Height); }

        // ---- the hay wagon: a plank bed with slats, and a heap of hay on it ----
        using (var heap = new SolidBrush(Hay))
        {
            PointF[] mound = [P(0.10f, 0.92f), P(0.20f, 1.18f), P(0.60f, 1.38f), P(1.10f, 1.44f), P(1.60f, 1.40f), P(2.10f, 1.30f), P(2.45f, 1.08f), P(2.50f, 0.92f)];
            g.FillClosedCurve(heap, mound, FillMode.Winding, 0.5f);
            g.DrawClosedCurve(ink, mound, 0.5f, FillMode.Winding);
            using var straw = new Pen(HayDark, Math.Max(1f, T * 0.035f));
            for (int i = 0; i < 9; i++)                                           // a few strokes of straw across the heap
            {
                float x = 0.35f + 0.22f * i;
                g.DrawLine(straw, P(x, 1.0f + 0.05f * (i % 3)), P(x + 0.16f, 1.22f + 0.05f * ((i + 1) % 3)));
            }
        }
        Box(Wood, R(0.05f, 0.42f, 2.55f, 0.98f));
        using (var slat = new Pen(WoodLit, Math.Max(1f, T * 0.045f)))
            for (int i = 1; i < 6; i++) g.DrawLine(slat, P(0.05f + 0.42f * i, 0.46f), P(0.05f + 0.42f * i, 0.94f));
        g.DrawLine(ink, P(2.55f, 0.40f), P(3.35f, 0.40f));                         // the tongue to the tractor

        // ---- the tractor: chassis, hood, grille, fender over the big wheel, seat, wheel, stack, lamp ----
        Box(Color.FromArgb(70, 46, 36), R(3.40f, 0.34f, 5.00f, 0.58f));            // chassis between the wheels
        Poly(Red, P(4.00f, 0.58f), P(4.00f, 1.00f), P(4.55f, 1.02f), P(5.12f, 0.92f), P(5.18f, 0.62f), P(5.05f, 0.58f));   // the hood
        using (var shine = new Pen(RedLit, Math.Max(1f, T * 0.05f))) g.DrawLine(shine, P(4.10f, 0.94f), P(5.00f, 0.88f));
        Box(Color.FromArgb(214, 196, 150), R(5.12f, 0.64f, 5.20f, 0.90f));          // the grille at the nose
        Box(Color.FromArgb(40, 28, 24), R(4.70f, 1.02f, 4.86f, 1.52f));   // the exhaust stack
        Box(Color.FromArgb(40, 28, 24), R(4.66f, 1.48f, 4.90f, 1.56f));            // its cap
        // The fender: an arc over the big back wheel (the wheel itself is stamped over the body later).
        using (var fender = new Pen(Red, Math.Max(2f, T * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            PointF c = P(RearX, 0.50f);
            float fr = _rRear + T * 0.08f;
            g.DrawArc(fender, c.X - fr, c.Y - fr, fr * 2, fr * 2, 200, 140);
        }
        Box(Red, R(3.30f, 0.98f, 3.92f, 1.04f));                                    // the flat of the fender, a step
        Poly(Color.FromArgb(60, 40, 34), P(3.36f, 1.04f), P(3.40f, 1.34f), P(3.72f, 1.34f), P(3.76f, 1.04f));   // the seat
        g.DrawLine(ink, P(3.95f, 1.00f), P(3.95f, 1.40f));                          // the steering column
        g.DrawLine(ink, P(3.86f, 1.42f), P(4.06f, 1.38f));                          // its wheel
        // The headlamp: a small bright lens on the nose (its glow is stamped in Draw).
        using (var lens = new SolidBrush(Color.FromArgb(255, 244, 190)))
            g.FillEllipse(lens, P(LampX, 0.80f).X - T * 0.07f, P(LampX, 0.80f).Y - T * 0.07f, T * 0.14f, T * 0.14f);
    });

    public override void Begin(Random rng) => _left = rng.Next(2) == 0;

    /// <summary>The body sheet's left edge: starts wholly off one side, ends wholly off the other.</summary>
    private float LeftEdge(float time) => _left ? _s.Width - Cruise * _s.U * time : -_len + Cruise * _s.U * time;

    public override void Draw(FrameBuffer fb, float t)
    {
        float x = LeftEdge(t);
        float rolled = Cruise * _s.U * t;                                           // how far every wheel has rolled
        float cx = x + _len / 2f;
        float ground = _s.FieldY(Math.Clamp(cx, 0, _s.Width - 1)) + _s.U * 0.004f;
        int top = (int)MathF.Round(ground) - _gy;
        int shake = (int)(t * 16) % 2 == 0 ? 0 : 1;                                  // the engine's tremble: the body only, a pixel at a time (more at 4K)
        shake = shake * Math.Max(1, (int)(_s.U / 1100f));
        int left = (int)MathF.Round(x);

        // The smoke first, so the machine is stamped over it. Puff j left the stack at time j * PuffEvery.
        for (int j = 0; j * PuffEvery < _trip; j++)
        {
            float age = t - j * PuffEvery;
            if (age < 0 || age > PuffLife) continue;
            float born = LeftEdge(j * PuffEvery);
            float stackDX = _left ? _len - (1 + StackX * _t) : 1 + StackX * _t;
            float a = age / PuffLife;
            float px = born + stackDX + (_left ? 1 : -1) * _s.U * 0.02f * a;           // blown a little back along the way it came
            float py = ground - 1.56f * _t - _s.U * 0.045f * MathF.Sqrt(a);            // floats up, slowing as it goes
            _puff.DrawCentered(fb, px, py, 0.55f * (1 - a) * Math.Min(1f, age * 3f), _stencil);
        }

        _body[_left ? 1 : 0].Draw(fb, left, top - shake, 1f, _stencil);

        // The wheels. Rolling right turns them clockwise; rolling left, the other way, which is the pages run backwards.
        void Wheel(Sprite[] pages, float r, float unitsX)
        {
            float turn = rolled / r / (MathF.PI / 3f);                               // in sixths of a revolution
            int pg = (int)((turn - MathF.Floor(turn)) * WheelPages) % WheelPages;
            if (_left) pg = (WheelPages - pg) % WheelPages;
            float wx = _left ? x + _len - (1 + unitsX * _t) : x + 1 + unitsX * _t;
            Sprite sp = pages[pg];
            sp.Draw(fb, (int)MathF.Round(wx - sp.Width / 2f), (int)MathF.Round(ground - r - sp.Height / 2f), 1f, _stencil);
        }
        Wheel(_cart, _rCart, CartX0);
        Wheel(_cart, _rCart, CartX1);
        Wheel(_rear, _rRear, RearX);
        Wheel(_front, _rFront, FrontX);

        // The headlamp's glow, at the nose.
        float lampX = _left ? x + _len - (1 + LampX * _t) : x + 1 + LampX * _t;
        _lamp.DrawCentered(fb, lampX + (_left ? -1 : 1) * _t * 0.15f, ground - 0.80f * _t, 0.65f, _stencil);
    }
}
