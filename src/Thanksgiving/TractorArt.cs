using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How the farm's machinery is drawn: an old red tractor with big back wheels
/// and one lit headlamp, and the two things it tows, a round baler and a hay
/// wagon. The field's own machine (FieldWork.cs) stamps these.
///
/// FEYNMAN VERSION: everything is painted in "tractor units", T pixels to the
/// unit, where the big rear wheel is one unit across. The machine is laid out
/// on one sheet 5.35 units long for a machine heading RIGHT: the implement at
/// the left (the wagon's bed from 0.05 to 2.55, or the baler's drum round 2.5),
/// a tongue to 3.35, and the tractor from 3.40 to 5.20 with its nose at the
/// right. A machine heading left is the same sheet flipped. The wheels are
/// NOT in the sheet: a wheel must TURN as the machine rolls and the body must
/// not, so each wheel is its own little picture, twelve pages a sixth of a
/// turn apart (six spokes, so the picture repeats every sixty degrees), and
/// the page is picked from the distance rolled: a wheel of radius R that has
/// rolled d has turned d / R radians. Big wheels turn slowly, small ones fast,
/// and none can slide, because all are worked out from one distance.
/// </summary>
internal static class TractorArt
{
    public const int WheelPages = 12;
    public const float SheetLen = 5.35f, SheetTall = 1.75f;                         // the sheet, in units
    // Where things are, in units from the sheet's left, for a machine heading right.
    public const float RearX = 3.55f, FrontX = 4.95f, StackX = 4.78f, LampX = 5.20f;   // the tractor's wheels, stack and lamp
    public const float CartX0 = 0.60f, CartX1 = 2.05f, BedX = 1.30f;                // the wagon's wheels and the middle of its bed
    public const float BalerWheelX = 2.45f, BalerRearX = 1.72f;                     // the baler's axle, and where a finished bale comes out
    public const float RearR = 0.50f, FrontR = 0.28f, CartR = 0.30f, BalerR = 0.30f;   // wheel radii, in units

    private static readonly Color Ink = Color.FromArgb(58, 30, 24);
    private static readonly Color Red = Color.FromArgb(178, 44, 34);
    private static readonly Color RedLit = Color.FromArgb(214, 76, 56);
    private static readonly Color Wood = Color.FromArgb(106, 74, 46);
    private static readonly Color WoodLit = Color.FromArgb(142, 102, 62);
    private static readonly Color Hay = Color.FromArgb(222, 176, 74);
    private static readonly Color HayDark = Color.FromArgb(168, 118, 44);
    private static readonly Color Green = Color.FromArgb(78, 112, 62);
    private static readonly Color GreenLit = Color.FromArgb(108, 146, 86);
    private static readonly Color GreenDark = Color.FromArgb(52, 78, 42);

    /// <summary>A sheet 5.35 by 1.75 units, with the ground line two pixels up from its bottom.</summary>
    public static (int Len, int Tall, int GroundY) Sheet(float T) => ((int)(SheetLen * T) + 2, (int)(SheetTall * T) + 3, (int)(SheetTall * T) + 1);

    /// <summary>One wheel page: dark tyre, dark inner disc, six bright spokes turned by page * 5 degrees, a hub.</summary>
    public static Sprite Wheel(float r, int page, Color spokeColour, bool big)
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

    /// <summary>The little drawing kit every sheet below uses: points in units (x from the left, y UP from the ground), filled shapes with an ink edge.</summary>
    private sealed class Kit : IDisposable
    {
        public readonly Graphics G; public readonly float T, Gy; public readonly Pen Ink;
        public Kit(Graphics g, float t, float gy) { G = g; T = t; Gy = gy; Ink = new Pen(TractorArt.Ink, Math.Max(1f, t * 0.04f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }; }
        public PointF P(float x, float y) => new(1 + x * T, Gy - y * T);
        public void Poly(Color c, params PointF[] pts) { using var b = new SolidBrush(c); G.FillPolygon(b, pts); G.DrawPolygon(Ink, pts); }
        public RectangleF R(float x0, float y0, float x1, float y1) { var a = P(x0, y1); var b = P(x1, y0); return new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y); }
        public void Box(Color c, RectangleF rc) { using var b = new SolidBrush(c); G.FillRectangle(b, rc); G.DrawRectangle(Ink, rc.X, rc.Y, rc.Width, rc.Height); }
        public void Dispose() => Ink.Dispose();
    }

    private static Sprite OnSheet(float T, bool flip, Action<Kit> paint)
    {
        var (len, tall, gy) = Sheet(T);
        return Sprite.Paint(len, tall, g =>
        {
            if (flip) { g.TranslateTransform(len, 0); g.ScaleTransform(-1, 1); }
            using var k = new Kit(g, T, gy);
            paint(k);
        });
    }

    /// <summary>The tractor alone (no wheels), on the shared sheet: chassis, hood, grille, fender over the big wheel, seat, stack, lamp, and the tongue behind it.</summary>
    public static Sprite Tractor(float T, bool flip) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        g.DrawLine(k.Ink, k.P(2.55f, 0.40f), k.P(3.35f, 0.40f));                     // the tongue to whatever is towed
        k.Box(Color.FromArgb(70, 46, 36), k.R(3.40f, 0.34f, 5.00f, 0.58f));          // chassis between the wheels
        k.Poly(Red, k.P(4.00f, 0.58f), k.P(4.00f, 1.00f), k.P(4.55f, 1.02f), k.P(5.12f, 0.92f), k.P(5.18f, 0.62f), k.P(5.05f, 0.58f));   // the hood
        using (var shine = new Pen(RedLit, Math.Max(1f, T * 0.05f))) g.DrawLine(shine, k.P(4.10f, 0.94f), k.P(5.00f, 0.88f));
        k.Box(Color.FromArgb(214, 196, 150), k.R(5.12f, 0.64f, 5.20f, 0.90f));        // the grille at the nose
        k.Box(Color.FromArgb(40, 28, 24), k.R(4.70f, 1.02f, 4.86f, 1.52f));           // the exhaust stack
        k.Box(Color.FromArgb(40, 28, 24), k.R(4.66f, 1.48f, 4.90f, 1.56f));           // its cap
        using (var fender = new Pen(Red, Math.Max(2f, T * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            PointF c = k.P(RearX, 0.50f);
            float fr = RearR * T + T * 0.08f;                                           // the fender: an arc over the big back wheel (stamped over the body later)
            g.DrawArc(fender, c.X - fr, c.Y - fr, fr * 2, fr * 2, 200, 140);
        }
        k.Box(Red, k.R(3.30f, 0.98f, 3.92f, 1.04f));                                  // the flat of the fender, a step
        k.Poly(Color.FromArgb(60, 40, 34), k.P(3.36f, 1.04f), k.P(3.40f, 1.34f), k.P(3.72f, 1.34f), k.P(3.76f, 1.04f));   // the seat
        g.DrawLine(k.Ink, k.P(3.95f, 1.00f), k.P(3.95f, 1.40f));                      // the steering column
        g.DrawLine(k.Ink, k.P(3.86f, 1.42f), k.P(4.06f, 1.38f));                      // its wheel
        using (var lens = new SolidBrush(Color.FromArgb(255, 244, 190)))               // the headlamp's lens (its glow is stamped in Draw)
            g.FillEllipse(lens, k.P(LampX, 0.80f).X - T * 0.07f, k.P(LampX, 0.80f).Y - T * 0.07f, T * 0.14f, T * 0.14f);
    });

    /// <summary>The hay wagon (no wheels) on the shared sheet: a plank bed with slats and a heap of hay on it, "heap" 0 (empty) to 3 (piled high).</summary>
    public static Sprite Wagon(float T, bool flip, int heap) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        if (heap > 0)
        {
            float h = 0.16f * heap;                                                     // how high the heap stands above the bed's rail
            using var hay = new SolidBrush(Hay);
            PointF[] mound = [k.P(0.10f, 0.92f), k.P(0.20f, 0.92f + h * 0.55f), k.P(0.60f, 0.92f + h * 0.95f), k.P(1.10f, 0.92f + h), k.P(1.60f, 0.92f + h * 0.95f), k.P(2.10f, 0.92f + h * 0.75f), k.P(2.45f, 0.92f + h * 0.3f), k.P(2.50f, 0.92f)];
            g.FillClosedCurve(hay, mound, FillMode.Winding, 0.5f);
            g.DrawClosedCurve(k.Ink, mound, 0.5f, FillMode.Winding);
            using var straw = new Pen(HayDark, Math.Max(1f, T * 0.035f));
            for (int i = 0; i < 3 * heap; i++)                                         // a few strokes of straw across the heap
            {
                float x = 0.35f + 0.22f * i;
                g.DrawLine(straw, k.P(x, 0.96f + 0.05f * (i % 3)), k.P(x + 0.16f, 0.96f + h * 0.6f + 0.05f * ((i + 1) % 3)));
            }
        }
        k.Box(Wood, k.R(0.05f, 0.42f, 2.55f, 0.98f));
        using (var slat = new Pen(WoodLit, Math.Max(1f, T * 0.045f)))
            for (int i = 1; i < 6; i++) g.DrawLine(slat, k.P(0.05f + 0.42f * i, 0.46f), k.P(0.05f + 0.42f * i, 0.94f));
    });

    /// <summary>
    /// The round baler (no wheels) on the shared sheet: a green drum on a frame behind the tractor, its pickup low at the
    /// front, and a TAILGATE, the rear half of the drum, hinged at the top, that swings up ("open" 0 shut to 1 wide) to
    /// let the finished bale roll out the back.
    /// </summary>
    public static Sprite Baler(float T, bool flip, float open) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        PointF c = k.P(2.50f, 0.98f);
        float rx = 0.78f * T, ry = 0.62f * T;
        k.Box(Color.FromArgb(70, 46, 36), k.R(1.95f, 0.30f, 3.35f, 0.42f));          // the frame, axle to hitch
        k.Poly(GreenDark, k.P(3.05f, 0.42f), k.P(3.30f, 0.42f), k.P(3.30f, 0.22f), k.P(3.05f, 0.18f));   // the pickup teeth, low at the front
        // The front half of the drum: the right half of an ellipse, fixed.
        using (var path = new GraphicsPath())
        {
            path.AddArc(c.X - rx, c.Y - ry, rx * 2, ry * 2, -90, 180);
            path.CloseFigure();
            using var b = new SolidBrush(Green);
            g.FillPath(b, path); g.DrawPath(k.Ink, path);
        }
        // The tailgate: the left half, turned up about the hinge at the top of the drum.
        var st = g.Save();
        PointF hinge = new(c.X, c.Y - ry);
        g.TranslateTransform(hinge.X, hinge.Y);
        g.RotateTransform(flip ? 70f * open : -70f * open);                         // up and over, the way the sheet faces
        g.TranslateTransform(-hinge.X, -hinge.Y);
        using (var path = new GraphicsPath())
        {
            path.AddArc(c.X - rx, c.Y - ry, rx * 2, ry * 2, 90, 180);
            path.CloseFigure();
            using var b = new SolidBrush(GreenLit);
            g.FillPath(b, path); g.DrawPath(k.Ink, path);
        }
        g.Restore(st);
        using (var band = new Pen(GreenDark, Math.Max(1f, T * 0.05f))) g.DrawLine(band, k.P(2.50f, 0.36f), k.P(2.50f, 1.60f));   // the seam between the halves
    });
}
