using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// How the farm's machinery is drawn: a red International Harvester of about
/// 1980 (the long square red hood with the black band along its side, the
/// black grille with its lamps, the boxy glassed cab with the white roof, the
/// chrome stack beside the hood, big back wheels on red rims) and the two
/// things it tows, a round baler and a hay wagon. The field's own machine
/// (FieldWork.cs) stamps these. An implement is HITCHED, not welded (his line:
/// "make the baler and the tractor move at the hitch so they look independent
/// of each other, but connected"): its sheet is painted at several PITCHES,
/// the whole implement turned a few degrees about the hitch pin, and the
/// field picks the pitch from the distance rolled, so the baler rocks on its
/// own wheel behind a tractor that trembles on its own, and the tongue stays
/// pinned to the clevis through all of it.
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
    public const float CartX0 = 0.60f, CartX1 = 2.05f, BedX0 = 0.05f;               // the wagon's wheels, and the trailing end of its bed (where the loader takes a bale)
    /// <summary>
    /// Where the bales sit on the wagon, in units from the sheet's left and UP from the ground, filled in this order: a
    /// pyramid of three on the bed's rail (0.98), two nestled on them, one on top. A bale is 0.9 units across, so a row
    /// sits 0.78 (0.9 times the root of three over two) above the one below. FieldWork stamps its own bale picture here.
    /// </summary>
    public static readonly (float X, float Y)[] BaleSlots = [(0.50f, 0.98f), (1.30f, 0.98f), (2.10f, 0.98f), (0.90f, 1.76f), (1.70f, 1.76f), (1.30f, 2.54f)];
    public const float BalerWheelX = 2.00f, BalerRearX = 1.27f;                     // the baler's axle, and where a finished bale comes out (the drum sits round 2.05: at 2.5 it was pressed into the rear wheel, "there is a hitch there")
    public const float RearR = 0.50f, FrontR = 0.28f, CartR = 0.30f, BalerR = 0.30f;   // wheel radii, in units
    public const float HitchX = 3.35f, HitchY = 0.40f;                              // the hitch pin: the implement's tongue ends here, and the implement pitches about it
    public const int PitchPages = 11;                                                // the pitch pages (implement AND tractor body): -3.6 to +3.6 degrees in steps of 0.9 (a step is under a pixel at an implement's wheel)
    public const float PitchStep = 1.2f;   // 11 pages of 1.2 degrees: -6 to +6 (a 5 px bump over a wheelbase is about 5 degrees; at 9 pages of 0.9 the bigger bumps clamped)
    /// <summary>The page whose angle is nearest to deg (clamped to the pages there are).</summary>
    public static int PageFor(float deg) => Math.Clamp((int)MathF.Round(deg / PitchStep) + PitchPages / 2, 0, PitchPages - 1);
    /// <summary>The pitch, in degrees, of page i (the middle page is level); positive lifts the implement's tail (screen angles run clockwise, and the tail is at the left of the right-heading sheet).</summary>
    public static float PitchOf(int i) => (i - PitchPages / 2) * PitchStep;
    /// <summary>Where a point of the implement's sheet (units) ends up under a pitch: turned about the hitch pin. For the wheels and the bales, which are stamped on separately.</summary>
    public static (float X, float Y) Pitched(float x, float y, float pitchDeg) => About(HitchX, HitchY, x, y, pitchDeg);
    /// <summary>Where a point (units, y up) ends up when the sheet is turned about a pivot by tailUpDeg: positive lifts the sheet's left (tail) end, the same turn the sheet's RotateTransform makes. The tractor's body turns about its REAR AXLE (nose up is a negative tailUpDeg).</summary>
    public static (float X, float Y) About(float cx, float cy, float x, float y, float tailUpDeg)
    {
        float a = tailUpDeg * MathF.PI / 180f, dx = x - cx, dy = y - cy;
        return (cx + dx * MathF.Cos(a) + dy * MathF.Sin(a), cy - dx * MathF.Sin(a) + dy * MathF.Cos(a));   // y is up: a positive pitch lifts the tail (dx negative)
    }

    // The paint is old and dusty: every red and green is pulled a little toward grey (his line: "the non fall color
    // primaries are a bit too bright"; the baler had been a fresh showroom green against the autumn field).
    private static readonly Color Ink = Color.FromArgb(58, 30, 24);
    private static readonly Color Red = Color.FromArgb(166, 60, 48);
    private static readonly Color RedLit = Color.FromArgb(196, 96, 78);
    private static readonly Color Wood = Color.FromArgb(106, 74, 46);
    private static readonly Color WoodLit = Color.FromArgb(142, 102, 62);
    private static readonly Color Green = Color.FromArgb(86, 104, 72);
    private static readonly Color GreenLit = Color.FromArgb(112, 132, 96);
    private static readonly Color GreenDark = Color.FromArgb(58, 72, 50);
    public static readonly Color Spoke = Color.FromArgb(184, 84, 66);              // the tractor's wheel spokes (FieldWork paints the wheels)

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

    private static Sprite OnSheet(float T, bool flip, Action<Kit> paint, float pitchDeg = 0f, float pivotX = HitchX, float pivotY = HitchY)
    {
        var (len, tall, gy) = Sheet(T);
        return Sprite.Paint(len, tall, g =>
        {
            if (flip) { g.TranslateTransform(len, 0); g.ScaleTransform(-1, 1); }
            if (pitchDeg != 0f)
            {
                // The sheet turned about its pivot (the hitch pin for an implement, the rear axle for the tractor). Screen angles run
                // clockwise, y is down: a positive pitch RAISES the tail at the left.
                float px = 1 + pivotX * T, py = gy - pivotY * T;
                g.TranslateTransform(px, py); g.RotateTransform(pitchDeg); g.TranslateTransform(-px, -py);
            }
            using var k = new Kit(g, T, gy);
            paint(k);
        });
    }

    /// <summary>
    /// The tractor alone (no wheels), on the shared sheet: an International Harvester of the 86 series, about 1980. Back
    /// to front: the drawbar's clevis at the hitch, the chassis, the big square cab (the Control Center: a glass box on
    /// red corner posts with a white roof, a door line and a step), the red fender over the big wheel, the long square
    /// red hood with the black band along its bottom edge and the badge stripe, the chrome stack and the air cleaner's
    /// bowl standing beside the hood on the cab's side, the black grille at the nose with its two lamps, and the front
    /// weights below it.
    /// </summary>
    public static Sprite Tractor(float T, bool flip, float pitchDeg = 0f) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        Color white = Color.FromArgb(226, 222, 212), glass = Color.FromArgb(58, 66, 76), glassLit = Color.FromArgb(104, 116, 128), black = Color.FromArgb(32, 26, 24), chrome = Color.FromArgb(170, 170, 166), chromeLit = Color.FromArgb(214, 214, 210);
        k.Box(Color.FromArgb(50, 36, 30), k.R(3.28f, 0.28f, 3.44f, 0.52f));          // the hitch: the drawbar's clevis the tongue pins into
        k.Box(Color.FromArgb(62, 44, 36), k.R(3.40f, 0.34f, 5.05f, 0.60f));          // the chassis between the wheels
        k.Box(black, k.R(3.62f, 0.60f, 3.90f, 0.72f));                                // the step up to the cab
        // The cab: floor at 0.98 over the fender, roof at 1.72; red posts, glass between, a thin red sill, the white roof over all.
        k.Box(Red, k.R(3.34f, 0.98f, 4.14f, 1.10f));                                  // the sill under the glass
        k.Box(glass, k.R(3.34f, 1.10f, 4.14f, 1.66f));                                // the glass
        using (var lit = new SolidBrush(glassLit))                                     // the sky in the glass: a pale slant across each pane
        {
            g.FillPolygon(lit, [k.P(3.40f, 1.62f), k.P(3.58f, 1.62f), k.P(3.46f, 1.14f), k.P(3.40f, 1.14f)]);
            g.FillPolygon(lit, [k.P(3.84f, 1.62f), k.P(4.08f, 1.62f), k.P(3.96f, 1.14f), k.P(3.84f, 1.14f)]);
        }
        using (var post = new Pen(Red, Math.Max(1.5f, T * 0.07f)))
        {
            g.DrawLine(post, k.P(3.36f, 1.08f), k.P(3.36f, 1.68f));                   // the rear post
            g.DrawLine(post, k.P(3.76f, 1.08f), k.P(3.76f, 1.68f));                   // the door post
            g.DrawLine(post, k.P(4.12f, 1.08f), k.P(4.12f, 1.68f));                   // the front post
        }
        k.Poly(white, k.P(3.26f, 1.66f), k.P(3.28f, 1.75f), k.P(4.20f, 1.75f), k.P(4.22f, 1.66f));   // the white roof, a little proud of the posts
        g.DrawLine(k.Ink, k.P(3.76f, 1.20f), k.P(3.76f, 1.60f));                      // the door line
        using (var handle = new Pen(black, Math.Max(1f, T * 0.04f))) g.DrawLine(handle, k.P(3.70f, 1.32f), k.P(3.64f, 1.32f));
        // The fender over the big back wheel: an arc with a flat top the cab floor sits on.
        using (var fender = new Pen(Red, Math.Max(2f, T * 0.11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            PointF c = k.P(RearX, 0.50f);
            float fr = RearR * T + T * 0.09f;
            g.DrawArc(fender, c.X - fr, c.Y - fr, fr * 2, fr * 2, 196, 148);
        }
        k.Box(Red, k.R(3.28f, 0.94f, 4.14f, 1.00f));                                  // the fender's flat and the cab floor
        // The hood: long and square, level on top, the nose cut square; the black band along its bottom edge; the lit top edge.
        k.Poly(Red, k.P(4.14f, 0.60f), k.P(4.14f, 1.12f), k.P(5.10f, 1.12f), k.P(5.16f, 1.06f), k.P(5.16f, 0.60f));
        k.Box(black, k.R(4.14f, 0.60f, 5.16f, 0.72f));                                // the black band
        using (var shine = new Pen(RedLit, Math.Max(1f, T * 0.05f))) g.DrawLine(shine, k.P(4.20f, 1.08f), k.P(5.06f, 1.08f));
        using (var badge = new Pen(white, Math.Max(1f, T * 0.035f))) g.DrawLine(badge, k.P(4.30f, 0.80f), k.P(4.95f, 0.80f));   // the badge stripe along the side
        // The grille at the nose: black with pale bars, the lit lamp in it (its glow is stamped in Draw) and a second lamp above.
        k.Box(black, k.R(5.06f, 0.62f, 5.20f, 1.08f));
        using (var bar = new Pen(Color.FromArgb(96, 92, 88), Math.Max(1f, T * 0.025f)))
            for (int i = 0; i < 5; i++) g.DrawLine(bar, k.P(5.08f, 0.66f + 0.08f * i), k.P(5.19f, 0.66f + 0.08f * i));
        using (var lens = new SolidBrush(Color.FromArgb(255, 244, 190)))
        {
            g.FillEllipse(lens, k.P(LampX - 0.06f, 0.80f).X - T * 0.06f, k.P(LampX - 0.06f, 0.80f).Y - T * 0.06f, T * 0.12f, T * 0.12f);
            g.FillEllipse(lens, k.P(LampX - 0.06f, 0.98f).X - T * 0.045f, k.P(LampX - 0.06f, 0.98f).Y - T * 0.045f, T * 0.09f, T * 0.09f);
        }
        k.Box(black, k.R(5.00f, 0.36f, 5.22f, 0.60f));                                // the front weights hung under the nose
        // The stack and the air cleaner: chrome, standing beside the hood on the cab's side, the stack with its rain cap.
        k.Box(chrome, k.R(4.22f, 1.12f, 4.32f, 1.72f));
        using (var gleam = new Pen(chromeLit, Math.Max(1f, T * 0.03f))) g.DrawLine(gleam, k.P(4.25f, 1.14f), k.P(4.25f, 1.70f));
        k.Box(chrome, k.R(4.18f, 1.70f, 4.36f, 1.74f));                               // the cap
        k.Box(chrome, k.R(4.44f, 1.12f, 4.56f, 1.30f));                               // the air cleaner's bowl
        k.Box(black, k.R(4.42f, 1.30f, 4.58f, 1.34f));
        // The mirror out from the cab's front post, and the steering wheel seen through the glass.
        using (var arm = new Pen(black, Math.Max(1f, T * 0.03f))) { g.DrawLine(arm, k.P(4.12f, 1.52f), k.P(4.26f, 1.52f)); }
        k.Box(black, k.R(4.24f, 1.44f, 4.30f, 1.60f));
        using (var wheel = new Pen(Color.FromArgb(40, 44, 50), Math.Max(1f, T * 0.035f))) g.DrawLine(wheel, k.P(3.92f, 1.34f), k.P(4.06f, 1.30f));
    }, pitchDeg, RearX, RearR);

    /// <summary>The hay wagon (no wheels) on the shared sheet: an empty plank bed with slats. The bales it carries are not here: FieldWork stamps them in BaleSlots as they are loaded.</summary>
    public static Sprite Wagon(float T, bool flip, float pitchDeg = 0f) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        using (var tongue = new Pen(TractorArt.Ink, Math.Max(1.5f, T * 0.06f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(tongue, k.P(2.55f, HitchY), k.P(HitchX, HitchY));              // the tongue, pinned at the clevis
        k.Box(Wood, k.R(BedX0, 0.42f, 2.55f, 0.98f));
        using (var slat = new Pen(WoodLit, Math.Max(1f, T * 0.045f)))
            for (int i = 1; i < 6; i++) g.DrawLine(slat, k.P(0.05f + 0.42f * i, 0.46f), k.P(0.05f + 0.42f * i, 0.94f));
    }, pitchDeg);

    /// <summary>
    /// The round baler (no wheels) on the shared sheet: a green drum on a frame behind the tractor, its pickup low at the
    /// front, and a TAILGATE, the rear half of the drum, hinged at the top, that swings up ("open" 0 shut to 1 wide) to
    /// let the finished bale roll out the back.
    /// </summary>
    public static Sprite Baler(float T, bool flip, float open, float pitchDeg = 0f) => OnSheet(T, flip, k =>
    {
        var g = k.G;
        // The drum's middle is 2.05: its front edge (2.83) clears the tractor's rear wheel (3.05) by a hand's breadth, so
        // the frame bar between them reads as the drawbar it is hitched by. (At 2.5 the drum overlapped the wheel.)
        PointF c = k.P(2.05f, 0.98f);
        float rx = 0.78f * T, ry = 0.62f * T;
        k.Box(Color.FromArgb(70, 46, 36), k.R(1.50f, 0.30f, 3.35f, 0.42f));          // the frame, axle to hitch
        k.Poly(GreenDark, k.P(2.70f, 0.42f), k.P(2.95f, 0.42f), k.P(2.95f, 0.22f), k.P(2.70f, 0.18f));   // the pickup teeth, low at the front
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
        // Up and BACK, away from the tractor: for the right-heading sheet the gate is the left half, so its foot swings
        // left and up, which is a clockwise turn on screen (GDI's angles run clockwise because y points down). The
        // flipped sheet is mirrored by OnSheet's ScaleTransform, and a mirror reverses every turn with it, so the SAME
        // angle serves both ways. It used to flip the sign as well, which cancelled the mirror: heading right the gate
        // swung down into the drum ("the hay baler seems to open the wrong way").
        g.RotateTransform(70f * open);
        g.TranslateTransform(-hinge.X, -hinge.Y);
        using (var path = new GraphicsPath())
        {
            path.AddArc(c.X - rx, c.Y - ry, rx * 2, ry * 2, 90, 180);
            path.CloseFigure();
            using var b = new SolidBrush(GreenLit);
            g.FillPath(b, path); g.DrawPath(k.Ink, path);
        }
        g.Restore(st);
        using (var band = new Pen(GreenDark, Math.Max(1f, T * 0.05f))) g.DrawLine(band, k.P(2.05f, 0.36f), k.P(2.05f, 1.60f));   // the seam between the halves
    }, pitchDeg);
}
