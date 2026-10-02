using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

/// <summary>
/// Snow falling on a snowy night, pastel lights twinkling in the trees, and
/// Santa's sleigh crossing the sky with Rudolph out front.
///
/// The same theater as Sakura: a BACKDROP painted once (ChristmasPainter.cs)
/// and ACTORS drawn over it every frame. Three kinds of actor here:
///
///   - The SNOW is the shared petal engine (Core/Sakura/PetalField.cs) told
///     to draw soft white dots instead of petals. Same fall, sway and depth.
///   - The LIGHTS: the backdrop already has every bulb painted as a small
///     dull dot. Each frame we stamp a soft glow of the bulb's color over
///     it, stronger or weaker on each bulb's own slow rhythm. That is the
///     twinkle.
///   - SANTA is a "sprite" (Core/Sprite.cs): the whole sleigh and team is
///     painted once at startup onto a see-through sheet, in four versions
///     with the reindeer's legs at different points of a gallop. Each frame
///     we stamp one of the four a little farther along, like a flip-book
///     sliding across the sky. He is only stamped onto sky pixels (the
///     painter hands over a stencil that says which pixels are sky), so he
///     passes behind the branches that reach in from the top corners, and
///     in front of the moon and stars. (He flies well above the pines, so
///     those never actually cover him.)
/// </summary>
internal sealed class ChristmasScene : IScreensaverScene
{
    /// <summary>One bulb's twinkle: where the bulb is, which color, and its own rhythm.</summary>
    private struct Light
    {
        public float X, Y;
        public int Color;
        public float Phase, Speed;
    }

    private const int GallopFrames = 4;

    // The sleigh and team are laid out in "s" units (roughly the height of a
    // reindeer). These say where things are on the sprite sheet, in s.
    private const float SheetWidth = 5.6f, SheetHeight = 1.5f, TopMargin = 0.32f;
    private const float FirstReindeer = 1.95f, ReindeerGap = 0.95f;
    private const int TeamSize = 4;                 // three reindeer and Rudolph, who leads

    private readonly Random _rng = new();
    private readonly ChristmasScenery _scenery;
    private readonly PetalField _snow;
    private readonly Light[] _lights;
    private readonly Sprite[] _bulbGlows;           // one soft glow per pastel color
    private readonly Sprite[] _sleigh;              // one per gallop frame
    private readonly Sprite _noseGlow, _noseSparkle;
    private readonly float _s;                      // the sleigh's size unit, in pixels
    private readonly float _u;
    private readonly int _w, _h;
    // A "double" (a decimal number with about 15 digits of precision), not a
    // "float" (about 7 digits). A screensaver can run for days. A float clock
    // that large can no longer register a 16 millisecond step, and Santa and
    // every bulb would freeze.
    private double _time;

    public ChristmasScene(int width, int height, ChristmasSettings settings)
    {
        _w = width;
        _h = height;
        _scenery = ChristmasPainter.Paint(width, height, _rng);
        _u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        // ---- Snow: the petal engine drawing soft white dots ----
        // Snowflakes are small, so there are more of them than petals.
        _snow = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent, _u) * 2,
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.FlakeSizePercent / 100f,
            [], _rng, sizeUnit: _u)
        {
            Shape = PetalField.FlakeShape.Snow,
            Colors = (Color.White, Color.FromArgb(222, 234, 255)),   // white, through to a faintly blue white
        };

        // ---- Lights: every bulb gets its own rhythm, so they do not blink in step ----
        _bulbGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow((int)(_u * 0.012f), c)).ToArray();
        _lights = _scenery.Lights.Select(l => new Light
        {
            X = l.At.X, Y = l.At.Y, Color = l.Color,
            Phase = (float)(_rng.NextDouble() * Math.PI * 2),
            Speed = 1.0f + 2.6f * (float)_rng.NextDouble(),
        }).ToArray();

        // ---- Santa: paint the sleigh once per gallop frame ----
        _s = _u * 0.075f;
        _sleigh = new Sprite[GallopFrames];
        for (int i = 0; i < GallopFrames; i++)
            _sleigh[i] = PaintSleigh(_s, i * MathF.Tau / GallopFrames);   // Tau = one full turn, so the frames are quarter turns apart

        _noseGlow = Sprite.Glow((int)(_s * 0.45f), Color.FromArgb(255, 48, 40));
        _noseSparkle = PaintSparkle((int)(_s * 0.9f));
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
    private static Sprite PaintSparkle(int size)
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

    // ==================================================================
    //  UPDATE AND RENDER
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _snow.Update(dt);
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 2. Santa, behind everything that is not sky (in practice, the corner branches).
        DrawSanta(fb);

        // 3. The lights. sin() swings between -1 and 1; the arithmetic turns
        //    that into "between 35% and 100% bright", so a bulb dims but
        //    never goes fully out.
        foreach (ref readonly Light l in _lights.AsSpan())
        {
            float bright = 0.35f + 0.65f * (0.5f + 0.5f * (float)Math.Sin(_time * l.Speed + l.Phase));
            _bulbGlows[l.Color].DrawCentered(fb, l.X, l.Y, bright);
        }

        // 4. The snow, far to near, in front of everything.
        _snow.Draw(fb);
    }

    /// <summary>
    /// Santa's position is worked out straight from the clock, with no
    /// stored state: distance = speed times time. He starts at the left edge,
    /// crosses to the right, flies on a little way off screen (that is the
    /// pause before he comes round again), and wraps back to the left. The
    /// "%" (remainder) is what makes the trip repeat.
    /// </summary>
    private void DrawSanta(FrameBuffer fb)
    {
        Sprite sheet = _sleigh[(int)(_time * 7 % GallopFrames)];    // 7 flip-book pages a second
        double trip = _w + sheet.Width + _u * 0.7f;                 // on-screen crossing plus the off-screen pause
        double travelled = (_time * _u * 0.085f + sheet.Width + _w * 0.03f) % trip;
        int left = (int)(travelled - sheet.Width);
        int top = (int)(_h * 0.12f + _u * 0.022f * Math.Sin(_time * 0.7));   // a gentle rise and dip

        sheet.Draw(fb, left, top, 1f, _scenery.IsSky);

        // Rudolph's nose. Its shine goes through the same sky stencil as the
        // sleigh, pixel by pixel, so a twig in front of him hides exactly the
        // part of the glow it covers. (An earlier version asked one yes/no
        // question about the single pixel under the nose, and the whole glow
        // blinked off every time that pixel crossed a twig.)
        PointF nose = NoseOnSheet(_s);
        int nx = left + (int)nose.X, ny = top + (int)nose.Y;

        float pulse = 0.75f + 0.25f * (float)Math.Sin(_time * 5);               // a steady warm throb
        _noseGlow.DrawCentered(fb, nx, ny, pulse, _scenery.IsSky);
        // The sparkle: sin() raised to a high power is near zero most of the
        // time and spikes briefly, so the glint flashes about once every three seconds.
        float glint = MathF.Pow(MathF.Max(0, (float)Math.Sin(_time * 2.1)), 8);
        _noseSparkle.DrawCentered(fb, nx, ny, glint, _scenery.IsSky);
    }

    public void Dispose() { }
}
