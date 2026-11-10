using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>
/// SANTA: the sleigh, the four reindeer with Rudolph in front, and the
/// flight across the sky. He is a "sprite" (Core/Sprite.cs): the whole
/// sleigh and team is painted once at startup onto a see-through sheet, in
/// four versions with the reindeer's legs at different points of a gallop.
/// Each frame one of the four is stamped a little farther along, like a
/// flip-book sliding across the sky. He is only stamped onto sky pixels
/// (the painter hands over a stencil that says which pixels are sky), so he
/// passes behind the branches that reach in from the top corners, and in
/// front of the moon and stars.
///
/// Moved out of ChristmasScene.cs on 2026-11-10 so that a story can take
/// the reins (bring him down onto the cabin's roof) without the scene
/// knowing how he is drawn.
/// </summary>
internal sealed class SantaRig
{
    private const int GallopFrames = 4;

    // The sleigh and team are laid out in "s" units (roughly the height of a
    // reindeer). These say where things are on the sprite sheet, in s.
    private const float SheetWidth = 5.6f, SheetHeight = 1.5f, TopMargin = 0.32f;
    private const float FirstReindeer = 1.95f, ReindeerGap = 0.95f;
    private const int TeamSize = 4;                 // three reindeer and Rudolph, who leads

    private readonly ChristmasScenery _scenery;
    private readonly Sprite[] _sleigh;              // one per gallop frame
    private readonly Sprite _noseGlow, _noseSparkle;
    private readonly float _s;                      // the sleigh's size unit, in pixels
    private readonly float _u;
    private readonly int _w, _h;
    private double _time;                           // a double: a screensaver runs for days, and a float clock that large stops registering a frame's step
    private Sprite _sheet;
    private int _left, _top;

    public SantaRig(ChristmasScenery scenery)
    {
        _scenery = scenery;
        _w = scenery.Width; _h = scenery.Height; _u = scenery.U;
        _s = _u * 0.075f;
        _sleigh = new Sprite[GallopFrames];
        for (int i = 0; i < GallopFrames; i++)
            _sleigh[i] = PaintSleigh(_s, i * MathF.Tau / GallopFrames);   // Tau = one full turn, so the frames are quarter turns apart
        _noseGlow = Sprite.Glow((int)(_s * 0.45f), Color.FromArgb(255, 48, 40));
        _noseSparkle = PaintSparkle((int)(_s * 0.9f));
        _sheet = _sleigh[0];
        Update(0);
    }

    /// <summary>
    /// Moves him on, and tells the scenery where he is now (a present has to
    /// fall from the real sleigh, and the animals look up at the real one).
    ///
    /// His position is worked out straight from the clock, with no stored
    /// state: distance = speed times time. He starts at the left edge,
    /// crosses to the right, flies on a little way off screen (that is the
    /// pause before he comes round again), and wraps back to the left. The
    /// "%" (remainder) is what makes the trip repeat.
    /// </summary>
    public void Update(float dt)
    {
        _time += dt;
        _sheet = _sleigh[(int)(_time * 7 % GallopFrames)];           // 7 flip-book pages a second
        double trip = _w + _sheet.Width + _u * 0.7f;                 // on-screen crossing plus the off-screen pause
        double travelled = (_time * _u * 0.085f + _sheet.Width + _w * 0.03f) % trip;
        _left = (int)(travelled - _sheet.Width);
        _top = (int)(_h * 0.12f + _u * 0.022f * Math.Sin(_time * 0.7));   // a gentle rise and dip

        PointF nose = NoseOnSheet(_s);
        _scenery.SantaSleigh = new PointF(_left + 0.7f * _s, _top + (TopMargin + 0.5f) * _s);
        _scenery.SantaNose = new PointF(_left + nose.X, _top + nose.Y);
        _scenery.SantaOnScreen = _left + _sheet.Width > 0 && _left < _w;
    }

    public void Draw(FrameBuffer fb)
    {
        _sheet.Draw(fb, _left, _top, 1f, _scenery.IsSky);

        // Rudolph's nose. Its shine goes through the same sky stencil as the
        // sleigh, pixel by pixel, so a twig in front of him hides exactly the
        // part of the glow it covers. (An earlier version asked one yes/no
        // question about the single pixel under the nose, and the whole glow
        // blinked off every time that pixel crossed a twig.)
        PointF nose = NoseOnSheet(_s);
        int nx = _left + (int)nose.X, ny = _top + (int)nose.Y;

        float pulse = 0.75f + 0.25f * (float)Math.Sin(_time * 5);               // a steady warm throb
        _noseGlow.DrawCentered(fb, nx, ny, pulse, _scenery.IsSky);
        // The sparkle: sin() raised to a high power is near zero most of the
        // time and spikes briefly, so the glint flashes about once every three seconds.
        float glint = MathF.Pow(MathF.Max(0, (float)Math.Sin(_time * 2.1)), 8);
        _noseSparkle.DrawCentered(fb, nx, ny, glint, _scenery.IsSky);
    }

    // ==================================================================
    //  PAINTING THE SLEIGH (once, at startup)
    // ==================================================================

    /// <summary>Where Rudolph's nose is on the sprite sheet, in pixels from its top-left corner.</summary>
    private static PointF NoseOnSheet(float s) =>
        new((FirstReindeer + ReindeerGap * (TeamSize - 1) + 0.49f) * s, (TopMargin + 0.23f) * s);

    /// <summary>
    /// Paints Santa, his sleigh, and four reindeer in a line, flying to the
    /// right, with the legs at one point ("phase") of the gallop.
    ///
    /// Every position below is in "s" units and goes through the little
    /// helper P, which turns (across, down) into real pixels. Scaling the
    /// whole drawing is then just a matter of changing s.
    /// </summary>
    private static Sprite PaintSleigh(float s, float phase)
    {
        return Sprite.Paint((int)(SheetWidth * s), (int)(SheetHeight * s), g =>
        {
            PointF P(float ax, float ay) => new(ax * s, (TopMargin + ay) * s);
            void Oval(Brush b, float cx, float cy, float rx, float ry) =>
                g.FillEllipse(b, (cx - rx) * s, (TopMargin + cy - ry) * s, rx * 2 * s, ry * 2 * s);
            Pen Line(Color c, float width) => new(c, Math.Max(1f, width * s)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            using var red = new SolidBrush(Color.FromArgb(206, 34, 46));
            using var white = new SolidBrush(Color.FromArgb(250, 250, 255));
            using var skin = new SolidBrush(Color.FromArgb(255, 212, 186));
            using var dark = new SolidBrush(Color.FromArgb(34, 24, 22));

            // ---- The sleigh's runner: a gold rail that curls up at the front ----
            using (Pen gold = Line(Color.FromArgb(236, 196, 96), 0.035f))
            {
                g.DrawLine(gold, P(0.12f, 0.96f), P(1.15f, 0.96f));
                g.DrawBezier(gold, P(1.15f, 0.96f), P(1.40f, 0.96f), P(1.44f, 0.72f), P(1.28f, 0.66f));
                g.DrawLine(gold, P(0.35f, 0.80f), P(0.35f, 0.96f));
                g.DrawLine(gold, P(0.95f, 0.80f), P(0.95f, 0.96f));
            }

            // ---- The sack of presents, behind Santa ----
            using (var sack = new SolidBrush(Color.FromArgb(134, 96, 58)))
                Oval(sack, 0.36f, 0.34f, 0.21f, 0.23f);

            // ---- Santa: coat, arm reaching to the reins, head, beard, hat ----
            Oval(red, 0.72f, 0.40f, 0.17f, 0.22f);
            using (Pen sleeve = Line(Color.FromArgb(206, 34, 46), 0.08f))
                g.DrawLine(sleeve, P(0.80f, 0.34f), P(1.00f, 0.38f));
            Oval(dark, 1.02f, 0.38f, 0.04f, 0.04f);                                    // mitten
            Oval(skin, 0.80f, 0.13f, 0.085f, 0.085f);
            Oval(white, 0.83f, 0.22f, 0.085f, 0.09f);                                  // beard
            g.FillPolygon(red, [P(0.71f, 0.10f), P(0.58f, -0.03f), P(0.80f, 0.02f), P(0.89f, 0.07f)]);   // hat, blown back
            Oval(white, 0.58f, -0.03f, 0.04f, 0.04f);                                  // pom-pom
            using (Pen brim = Line(Color.FromArgb(250, 250, 255), 0.04f))
                g.DrawLine(brim, P(0.72f, 0.09f), P(0.88f, 0.065f));

            // ---- The sleigh body, in front of Santa: a smooth curved shape through these points ----
            g.FillClosedCurve(red, [
                P(0.10f, 0.26f), P(0.22f, 0.36f), P(0.36f, 0.52f), P(0.95f, 0.52f), P(1.12f, 0.42f),
                P(1.27f, 0.36f), P(1.21f, 0.62f), P(1.05f, 0.82f), P(0.25f, 0.82f), P(0.12f, 0.60f)], FillMode.Winding, 0.35f);
            using (Pen trim = Line(Color.FromArgb(236, 196, 96), 0.025f))
                g.DrawLine(trim, P(0.36f, 0.56f), P(0.98f, 0.56f));

            // ---- The reins: one long strap from Santa's hand to Rudolph's collar ----
            using (Pen reins = Line(Color.FromArgb(120, 84, 56), 0.016f))
                g.DrawLine(reins, P(1.02f, 0.38f), P(FirstReindeer + ReindeerGap * (TeamSize - 1) + 0.20f, 0.42f));

            // ---- The team. The last one painted is the leader. ----
            for (int i = 0; i < TeamSize; i++)
            {
                float cx = FirstReindeer + ReindeerGap * i;
                bool rudolph = i == TeamSize - 1;
                float gait = phase + i * 0.9f;                 // each reindeer is a little out of step with the next
                Color coat = Color.FromArgb(152, 106, 66);
                using var fur = new SolidBrush(coat);

                // Legs: each swings from the hip like a pendulum. Front legs
                // reach forward, back legs trail behind. sin() turns the
                // steadily growing gait number into a back-and-forth swing.
                using (Pen leg = Line(Color.FromArgb(112, 76, 48), 0.05f))
                {
                    void Leg(float hipX, float lean, float swing)
                    {
                        float angle = lean + 0.45f * MathF.Sin(swing);
                        g.DrawLine(leg, P(hipX, 0.58f), P(hipX + 0.34f * MathF.Sin(angle), 0.58f + 0.34f * MathF.Cos(angle)));
                    }
                    Leg(cx + 0.17f, 0.55f, gait);
                    Leg(cx + 0.13f, 0.55f, gait + 1.3f);
                    Leg(cx - 0.18f, -0.55f, gait + MathF.PI);
                    Leg(cx - 0.22f, -0.55f, gait + MathF.PI + 1.3f);
                }

                Oval(white, cx - 0.28f, 0.45f, 0.045f, 0.045f);                        // tail
                Oval(fur, cx, 0.52f, 0.28f, 0.125f);                                   // body
                using (Pen neck = Line(coat, 0.12f))
                    g.DrawLine(neck, P(cx + 0.20f, 0.47f), P(cx + 0.33f, 0.26f));
                Oval(fur, cx + 0.38f, 0.22f, 0.115f, 0.075f);                          // head
                g.FillPolygon(fur, [P(cx + 0.28f, 0.19f), P(cx + 0.24f, 0.08f), P(cx + 0.33f, 0.16f)]);   // ear
                using (Pen collar = Line(Color.FromArgb(206, 34, 46), 0.04f))
                    g.DrawLine(collar, P(cx + 0.19f, 0.36f), P(cx + 0.27f, 0.50f));

                using (Pen antler = Line(Color.FromArgb(214, 186, 140), 0.025f))
                {
                    g.DrawLine(antler, P(cx + 0.33f, 0.16f), P(cx + 0.27f, -0.10f));
                    g.DrawLine(antler, P(cx + 0.30f, 0.03f), P(cx + 0.38f, -0.06f));
                    g.DrawLine(antler, P(cx + 0.29f, -0.02f), P(cx + 0.20f, -0.08f));
                }

                Oval(dark, cx + 0.40f, 0.20f, 0.014f, 0.014f);                         // eye
                if (rudolph)
                {
                    using var nose = new SolidBrush(Color.FromArgb(255, 40, 36));
                    Oval(nose, cx + 0.49f, 0.23f, 0.04f, 0.04f);                       // the famous nose; the scene adds its shine
                }
                else
                    Oval(dark, cx + 0.485f, 0.23f, 0.02f, 0.02f);
            }
        });
    }

    /// <summary>
    /// A four-pointed sparkle: two thin diamonds, one lying down and one
    /// standing up, crossing in the middle. Flashed briefly over the nose
    /// now and then, the way a shiny thing catches the light.
    /// </summary>
    public static Sprite PaintSparkle(int size)
    {
        size = Math.Max(8, size);
        float c = size / 2f, thin = Math.Max(1f, size * 0.035f);
        return Sprite.Paint(size, size, g =>
        {
            using var ray = new SolidBrush(Color.FromArgb(230, 255, 226, 220));
            g.FillPolygon(ray, [new PointF(0, c), new PointF(c, c - thin), new PointF(size, c), new PointF(c, c + thin)]);
            g.FillPolygon(ray, [new PointF(c, 0), new PointF(c + thin, c), new PointF(c, size), new PointF(c - thin, c)]);
        });
    }
}
