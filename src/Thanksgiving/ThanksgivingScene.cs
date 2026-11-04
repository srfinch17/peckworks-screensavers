using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// Maple leaves drifting down over a farm at harvest dusk, a V of geese
/// crossing the sky, smoke from the farmhouse chimney, and a scarecrow's
/// scarf flapping in the breeze.
///
/// The same theater as Halloween and Christmas: a BACKDROP painted once
/// (ThanksgivingPainter.cs) and ACTORS drawn over it every frame:
///
///   - The LEAVES are the shared petal engine (Core/Sakura/PetalField.cs)
///     told to draw leaf shapes in gold through crimson. They fall, sway,
///     spin and tumble exactly as petals do, and let go of the maples.
///   - The GEESE are sprites (Core/Sprite.cs): one goose, painted once per
///     wing position (16 pages of a flap) facing right and again facing
///     left. Each frame the V is stamped a little farther along, each goose
///     on its own page of the flip-book. They are only stamped onto sky
///     pixels (the painter hands over a stencil that says which pixels are
///     sky), so the V passes behind the oak branches in the corners.
///   - The SMOKE from the farmhouse chimney: a few soft grey puffs, each
///     one's place worked out from the clock alone (so there is nothing to
///     keep track of), rising, drifting with the breeze, growing and fading.
///   - The WINDOWS: a warm glow stamped over every lit window, wandering in
///     brightness like lamplight.
///   - The SCARF on the scarecrow: a flip-book of 24 pages of a red scarf
///     streaming in the wind, stamped at its knot.
///   - The HAPPENINGS: twenty small surprises (a turkey strutting through, a
///     raccoon at the cornucopia, a tractor on the far field) that each come
///     on for a few seconds now and then. Each is one class in the Happenings
///     folder; a "director" from the engine (Core/Happenings.cs) decides
///     which goes on when.
/// </summary>
internal sealed class ThanksgivingScene : IScreensaverScene
{
    private const int FlapPages = 16;
    private const int ScarfPages = 24;
    private const int SmokePuffs = 7;
    private const float SmokeCycle = 11f;          // seconds a puff takes to rise, spread and vanish

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly ThanksgivingScenery _scenery;
    private readonly PetalField _leaves;
    private readonly Sprite[] _goose;              // one per wing position, facing right (mirror stamps the left-facing one)
    private readonly float _gs;                    // the goose's size unit (its body length), in pixels
    private readonly int _geese;
    private readonly int _geeseDir;                // +1 the V flies right, -1 left
    private readonly float[] _flapPhase;           // each goose's own place in the flap, so they are not all in step
    private readonly Sprite[] _smoke;              // four puff sizes, small to big
    private readonly Sprite _windowGlow, _loftGlow;
    private readonly float[] _windowPhase;
    private readonly Sprite[] _scarf;
    private readonly HappeningDirector _happenings;
    private readonly float _u;
    private readonly int _w, _h;
    // A "double" (a decimal number with about 15 digits of precision), not a
    // "float" (about 7 digits). A screensaver can run for days. A float clock
    // that large can no longer register a 16 millisecond step, and the geese
    // and the smoke would freeze.
    private double _time;

    public ThanksgivingScene(int width, int height, ThanksgivingSettings settings)
    {
        _w = width;
        _h = height;
        _scenery = ThanksgivingPainter.Paint(width, height, _rng);
        _u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        // ---- Leaves: the petal engine with a leaf outline and maple colours ----
        _leaves = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent, _u) * 6 / 10,
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.LeafSizePercent / 100f * 1.3f,
            _scenery.LeafSpots, _rng, sizeUnit: _u)
        {
            Shape = PetalField.FlakeShape.Leaf,
            Colors = (Color.FromArgb(250, 186, 56), Color.FromArgb(168, 42, 26)),   // gold through to crimson
            Tint = (1f, 0.97f, 0.92f),                                            // the last of the sunset: a touch warm
        };

        // ---- Geese: paint one goose per wing position, once ----
        _gs = Math.Max(10f, _u * 0.055f);
        _goose = new Sprite[FlapPages];
        for (int i = 0; i < FlapPages; i++)
            _goose[i] = PaintGoose(_gs, i * MathF.Tau / FlapPages);
        _geese = settings.GeeseCount;
        _geeseDir = _rng.Next(2) == 0 ? 1 : -1;
        _flapPhase = new float[Math.Max(1, _geese)];
        for (int i = 0; i < _flapPhase.Length; i++) _flapPhase[i] = (float)(_rng.NextDouble() * Math.PI * 2);

        // ---- Smoke: four sizes of soft grey puff ----
        _smoke = new Sprite[4];
        for (int i = 0; i < _smoke.Length; i++)
            _smoke[i] = Sprite.Glow(Math.Max(2, (int)(_u * (0.010f + 0.011f * i))), Color.FromArgb(214, 206, 206));

        // ---- Windows: one warm glow, and each window's own rhythm ----
        _windowGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.022f)), Color.FromArgb(255, 190, 90));
        _loftGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.020f)), Color.FromArgb(255, 180, 80));
        _windowPhase = new float[_scenery.FarmWindows.Length + 1];
        for (int i = 0; i < _windowPhase.Length; i++) _windowPhase[i] = (float)(_rng.NextDouble() * 20);

        // ---- The scarf: 24 pages of a ribbon streaming in the breeze ----
        _scarf = new Sprite[ScarfPages];
        for (int i = 0; i < ScarfPages; i++)
            _scarf[i] = PaintScarf(_scenery.ScarecrowHeight, i * MathF.Tau / ScarfPages);

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(ThanksgivingHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);
    }

    // ==================================================================
    //  PAINTING THE ACTORS (once, at startup)
    // ==================================================================

    /// <summary>
    /// One Canada goose in flight, seen from the side, flying to the right,
    /// with its wings at one point ("phase") of the beat. "s" is its body
    /// length in pixels; everything below is in fractions of s, with (0, 0)
    /// at the middle of the body, x along the goose and y DOWN the screen.
    ///
    /// The far wing goes down first, then the body, neck and head, then the
    /// near wing on top. A wing is two bones: the upper arm from the shoulder
    /// to the wrist, and the hand from the wrist to the tip, which lags a
    /// little behind the upper arm the way a real wing bends on the
    /// downstroke. Each bone is a thick line with a feathered trailing edge
    /// (a filled shape behind the bone line).
    /// </summary>
    private static Sprite PaintGoose(float s, float phase)
    {
        int w = (int)(s * 1.5f) + 4, h = (int)(s * 1.9f) + 4;
        float cx = w * 0.46f, cy = h * 0.5f;
        return Sprite.Paint(w, h, g =>
        {
            PointF P(float ax, float ay) => new(cx + s * ax, cy + s * ay);
            Color body = Color.FromArgb(96, 84, 70), belly = Color.FromArgb(190, 178, 160), black = Color.FromArgb(28, 24, 24);
            Color wingFar = Color.FromArgb(70, 60, 50), wingNear = Color.FromArgb(104, 90, 74);

            // The flap: the upper arm swings about the shoulder; the hand lags a quarter beat behind it.
            float up = 0.95f * MathF.Sin(phase);                       // +1 wings up, -1 wings down (y is down the screen, so a NEGATIVE y is up)
            float hand = 0.95f * MathF.Sin(phase - 0.7f);

            void Wing(Color c, float shoulderY)
            {
                // The leading edge: shoulder to wrist (the upper arm), wrist to
                // tip (the hand). The trailing edge runs back from the tip to the
                // body, set off from the leading edge by an amount that grows
                // with how far the wing is from level: seen from the side a
                // level wing is edge-on and thin, a raised or lowered one shows
                // its whole surface.
                PointF shoulder = P(0.05f, shoulderY);
                PointF wrist = P(0.0f, shoulderY - 0.40f * up);
                PointF tip = P(-0.12f, shoulderY - 0.40f * up - 0.38f * hand);
                PointF trailTip = new(tip.X - s * 0.08f, tip.Y + s * 0.10f * hand);
                PointF trailWrist = new(wrist.X - s * 0.22f, wrist.Y + s * 0.18f * up);
                PointF root = P(-0.30f, shoulderY + 0.03f);
                using var feather = new SolidBrush(c);
                using var path = new GraphicsPath();
                path.AddLines([shoulder, wrist, tip, trailTip, trailWrist, root]);
                path.CloseFigure();
                g.FillPath(feather, path);
                using var edge = new Pen(c, Math.Max(1f, s * 0.06f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(edge, [shoulder, wrist, tip]);
            }

            Wing(wingFar, -0.02f);                                        // the far wing, behind the body
            using (var b = new SolidBrush(body))
                g.FillEllipse(b, P(-0.45f, -0.14f).X, P(-0.45f, -0.14f).Y, s * 0.90f, s * 0.28f);
            using (var pale = new SolidBrush(belly))
                g.FillEllipse(pale, P(-0.40f, -0.02f).X, P(-0.40f, -0.02f).Y, s * 0.76f, s * 0.16f);
            using (var tail = new SolidBrush(black))
                g.FillPolygon(tail, [P(-0.44f, -0.06f), P(-0.62f, -0.10f), P(-0.62f, 0.04f), P(-0.44f, 0.08f)]);
            using (var rump = new SolidBrush(Color.FromArgb(236, 232, 224)))
                g.FillEllipse(rump, P(-0.52f, -0.06f).X, P(-0.52f, -0.06f).Y, s * 0.12f, s * 0.10f);
            // The neck: stretched straight out in front, as geese fly, with the head at the end.
            using (var neck = new Pen(black, Math.Max(1.5f, s * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(neck, P(0.38f, -0.02f), P(0.72f, -0.06f));
            using (var head = new SolidBrush(black))
                g.FillEllipse(head, P(0.66f, -0.13f).X, P(0.66f, -0.13f).Y, s * 0.18f, s * 0.14f);
            using (var chin = new SolidBrush(Color.FromArgb(240, 238, 232)))                // the white chin strap
                g.FillEllipse(chin, P(0.70f, -0.09f).X, P(0.70f, -0.09f).Y, s * 0.09f, s * 0.07f);
            using (var bill = new SolidBrush(black))
                g.FillPolygon(bill, [P(0.82f, -0.08f), P(0.94f, -0.05f), P(0.82f, -0.03f)]);
            Wing(wingNear, -0.06f);                                       // the near wing, over the body
        });
    }

    /// <summary>
    /// The scarecrow's scarf at one moment of its flutter: a red ribbon
    /// trailing to the right from its knot, in six short pieces, each one
    /// turned a little more by the wave that runs along it. The knot is at
    /// the sprite's left-middle, so the scene stamps it with that point on
    /// the scarecrow's neck.
    /// </summary>
    private static Sprite PaintScarf(float tall, float phase)
    {
        int w = (int)(tall * 0.34f) + 4, h = (int)(tall * 0.24f) + 4;
        float ox = 2, oy = h * 0.5f;
        return Sprite.Paint(w, h, g =>
        {
            using var ribbon = new Pen(Color.FromArgb(188, 44, 40), Math.Max(1.5f, tall * 0.045f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var fringe = new Pen(Color.FromArgb(214, 72, 60), Math.Max(1f, tall * 0.012f)) { EndCap = LineCap.Round };
            var pts = new PointF[7];
            pts[0] = new PointF(ox, oy);
            float x = ox, y = oy;
            for (int k = 1; k < pts.Length; k++)
            {
                // Each piece points a little up (the wind lifts it) plus a wave that travels along the ribbon.
                float angle = -0.25f + 0.55f * MathF.Sin(phase - k * 0.85f) + 0.04f * k;
                x += tall * 0.048f * MathF.Cos(angle);
                y += tall * 0.048f * MathF.Sin(angle);
                pts[k] = new PointF(x, y);
            }
            g.DrawLines(ribbon, pts);
            // The knot: a small round at the start, and a fringe of three threads at the end.
            using var knot = new SolidBrush(Color.FromArgb(160, 34, 32));
            g.FillEllipse(knot, ox - tall * 0.025f, oy - tall * 0.025f, tall * 0.05f, tall * 0.05f);
            for (int i = -1; i <= 1; i++)
                g.DrawLine(fringe, pts[^1], new PointF(pts[^1].X + tall * 0.03f, pts[^1].Y + i * tall * 0.022f));
        });
    }

    // ==================================================================
    //  UPDATE AND RENDER
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _leaves.Update(dt);
        _happenings.Update(elapsedSeconds);
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        fb.Restore(_scenery.Pixels);

        // 2. The FAR happenings (layer 0): the ones in the sky and on the
        //    hills, before the geese so a firework or a balloon is behind
        //    them. They are told where the lead goose is first.
        (float leadX, float leadY) = LeadGoose();
        _scenery.GeeseLead = new PointF(leadX, leadY);
        _scenery.GeeseOnScreen = leadX > -_gs * (_geese + 1) && leadX < _w + _gs * (_geese + 1);
        _happenings.Draw(fb, 0);

        // 3. The smoke, rising behind everything that stands in front of the farm.
        DrawSmoke(fb);

        // 4. The geese, behind everything that is not sky (in practice, the corner branches).
        DrawGeese(fb, leadX, leadY);

        // 5. The windows: lamplight that wanders a little, never off. Two
        //    waves of different speeds added together make the brightness
        //    drift unevenly, the way a lamp seen through old glass does.
        for (int i = 0; i < _scenery.FarmWindows.Length; i++)
        {
            RectangleF win = _scenery.FarmWindows[i];
            float p = _windowPhase[i];
            float lamp = 0.34f + 0.06f * (float)Math.Sin(_time * 1.7 + p) + 0.04f * (float)Math.Sin(_time * 5.3 + p * 2);
            _windowGlow.DrawCentered(fb, win.X + win.Width / 2, win.Y + win.Height / 2, lamp);
        }
        {
            RectangleF loft = _scenery.BarnLoft;
            float p = _windowPhase[^1];
            float lamp = 0.30f + 0.08f * (float)Math.Sin(_time * 2.3 + p) + 0.05f * (float)Math.Sin(_time * 7.1 + p * 2);
            _loftGlow.DrawCentered(fb, loft.X + loft.Width / 2, loft.Y + loft.Height / 2, lamp);
        }

        // 6. The scarf: the wave runs along it about four times a second,
        //    with gusts (a slow wave on the speed) so it does not tick like a clock.
        float gust = 0.7f + 0.3f * (float)Math.Sin(_time * 0.9);
        double flutter = (_time * 4.2 * gust) % (Math.PI * 2);
        int scarfPage = (int)(flutter / (Math.PI * 2) * ScarfPages) % ScarfPages;
        _scarf[scarfPage].Draw(fb, (int)_scenery.ScarecrowNeck.X - 2, (int)_scenery.ScarecrowNeck.Y - _scarf[scarfPage].Height / 2);

        // 7. The NEAR happenings (layer 1): everything in front of the
        //    scenery and the geese, like the turkey on the ground.
        _happenings.Draw(fb, 1);

        // 8. The leaves, far to near, in front of everything.
        _leaves.Draw(fb);

        // 9. Happenings on the GLASS (layer 2), in front of even the leaves.
        _happenings.Draw(fb, 2);
    }

    /// <summary>
    /// Where the lead goose is, worked out straight from the clock with no
    /// stored state: distance = speed times time. The V starts off one side,
    /// crosses, flies on a little way off the other side (the pause before it
    /// comes round again) and wraps. The "%" (remainder) is what makes the
    /// trip repeat. It rises and dips gently as it goes.
    /// </summary>
    private (float X, float Y) LeadGoose()
    {
        float formation = _gs * 1.1f * ((_geese + 1) / 2);              // how far the last goose trails the leader
        double trip = _w + formation + _gs * 2 + _u * 1.2f;             // on-screen crossing plus the off-screen pause
        double travelled = (_time * _u * 0.055 + formation + _gs + _w * 0.15) % trip;
        float x = _geeseDir > 0 ? (float)(travelled - formation - _gs) : (float)(_w + formation + _gs - travelled);
        float y = _h * 0.17f + _u * 0.03f * (float)Math.Sin(_time * 0.45);
        return (x, y);
    }

    /// <summary>
    /// The V: the leader in front, then the followers in two arms, each one
    /// a little farther back than the last; from the side one arm rises
    /// behind the leader and the other drops. Each goose beats its wings
    /// about three times a second, on its own phase, so the V ripples
    /// instead of flapping in step. Geese at the back are drawn first.
    /// </summary>
    private void DrawGeese(FrameBuffer fb, float leadX, float leadY)
    {
        for (int i = _geese - 1; i >= 0; i--)
        {
            int rank = (i + 1) / 2;                                      // how far back along its arm (0 = the leader)
            float side = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1);
            float x = leadX - _geeseDir * rank * _gs * 1.1f;
            float y = leadY + side * rank * _gs * 0.48f;
            double beat = _time * 2.9 + _flapPhase[i];
            int page = (int)((beat % (Math.PI * 2)) / (Math.PI * 2) * FlapPages) % FlapPages;
            Sprite sheet = _goose[page];
            // A goose bobs against its own beat: wings down pushes the body up.
            float bob = _gs * 0.05f * (float)Math.Sin(beat);
            sheet.DrawCentered(fb, x, y + bob, 1f, _scenery.OpenSky, mirror: _geeseDir < 0);
        }
    }

    /// <summary>
    /// Each puff's whole life is one trip from the chimney top: up, drifting
    /// with the breeze, growing and fading. Its age is the clock plus its own
    /// head start, wrapped by the cycle length, so the puffs are spaced out
    /// along the plume and nothing is stored between frames.
    /// </summary>
    private void DrawSmoke(FrameBuffer fb)
    {
        PointF top = _scenery.ChimneyTop;
        for (int i = 0; i < SmokePuffs; i++)
        {
            float age = (float)((_time + i * SmokeCycle / SmokePuffs) % SmokeCycle) / SmokeCycle;   // 0 just out, 1 gone
            float rise = _u * 0.11f * age;
            float drift = _u * 0.07f * age * age + _u * 0.008f * MathF.Sin(age * 9 + i);          // the breeze carries it right, more as it climbs
            float x = top.X + drift, y = top.Y - rise;
            int size = Math.Min(_smoke.Length - 1, (int)(age * _smoke.Length));
            float opacity = 0.75f * MathF.Min(1f, age * 6) * (1 - age) * (1 - age);
            _smoke[size].DrawCentered(fb, x, y, opacity, _scenery.OpenFromFarm);
        }
    }

    public void Dispose() { }
}
