using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
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
///   - The RESIDENTS: the turkeys in the field (TurkeyFlock.cs), the doe and
///     her fawn (DeerHerd.cs), and the ACORN CAPER (AcornCaper.cs): every
///     minute or two a squirrel comes out of the hollow in the left maple's
///     roots, buries a nut, a second squirrel digs it up, they fight over it
///     in a cloud of dust, and a mouse walks off with it.
///   - The HAPPENINGS: nineteen small surprises (a turkey strutting through, a
///     raccoon on the near ground, a pheasant from the corn shock) that each come
///     on for a few seconds now and then. Each is one class in the Happenings
///     folder; a "director" from the engine (Core/Happenings.cs) decides
///     which goes on when.
/// </summary>
internal sealed class ThanksgivingScene : IScreensaverScene
{
    private const int FlapPages = 16;
    private const int ScarfPages = 32;
    private const int SmokePuffs = 7;
    private const float SmokeCycle = 11f;          // seconds a puff takes to rise, spread and vanish

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly ThanksgivingScenery _scenery;
    private readonly PetalField _leaves;
    private readonly Sprite[] _goose;              // one per wing position, facing right (mirror stamps the left-facing one)
    private readonly float _gs;                    // the goose's size unit (its body length), in pixels
    private readonly int _geese;
    private readonly TurkeyFlock _flock;           // the wild turkeys that live in the field
    private readonly DeerHerd _herd;               // the doe and her fawn at the far side of it
    private readonly FieldWork _work;              // the tractor baling the far lane, and the bales it leaves (FieldWork.cs)
    private readonly Pack _pack;                   // the coyotes and the rabbits on the far lane (Pack.cs)
    private readonly StoryDirector _stories;       // the little scenes (the acorn caper, hide and seek, the fawn's apple, the mouse parade), one every minute or two
    private readonly Noticeboard _board;           // what is going on, for the residents' senses (Core/Minds/Senses.cs)
    private (Sense Kind, float X, float Y, float Reach, float At)? _testNotice;
    private readonly PatchLife _life;              // the mice, birds and squirrel that live in the pumpkin patch
    private readonly int _geeseDir;                // +1 the V flies right, -1 left
    private readonly float[] _flapPhase;           // each goose's own place in the flap, so they are not all in step
    private readonly Sprite[] _smoke;              // four puff sizes, small to big
    private readonly Sprite _windowGlow, _loftGlow;
    private readonly Sprite _starGlow;             // the twinkle stamped on the brighter stars
    private readonly Sprite _candle;               // the jack-o'-lantern's candle, flickering over its carved face
    private readonly float[] _starPhase, _starSpeed;
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
    private double _scarfPhase;                    // where the scarf's wave is, 0 to two pi (its own clock, added up each frame)

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
        // ---- The stars: a small warm glow each, on its own slow rhythm, like the Christmas bulbs ----
        _starGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.0065f)), Color.FromArgb(255, 244, 220));
        _candle = Sprite.Glow(Math.Max(4, (int)(_scenery.Jack.R * 1.6f)), Color.FromArgb(255, 160, 50));
        _starPhase = new float[_scenery.Stars.Count];
        _starSpeed = new float[_scenery.Stars.Count];
        for (int i = 0; i < _starPhase.Length; i++)
        {
            _starPhase[i] = (float)(_rng.NextDouble() * Math.PI * 2);
            _starSpeed[i] = 0.6f + 1.8f * (float)_rng.NextDouble();
        }
        for (int i = 0; i < _windowPhase.Length; i++) _windowPhase[i] = (float)(_rng.NextDouble() * 20);

        // ---- The scarf: 24 pages of a ribbon streaming in the breeze ----
        _scarf = new Sprite[ScarfPages];
        for (int i = 0; i < ScarfPages; i++)
            _scarf[i] = PaintScarf(_scenery.ScarecrowHeight, i * MathF.Tau / ScarfPages);

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(ThanksgivingHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);

        // ---- The noticeboard (Core/Minds/Senses.cs): whatever happens is pinned here, and every resident reads it ----
        _board = new Noticeboard();
        _happenings.Began = Tell;

        // ---- The flock: wild turkeys living in the field (after the director, so its dice are untouched) ----
        _flock = new TurkeyFlock(_scenery, settings.TurkeyCount, _rng, _board);
        _herd = new DeerHerd(_scenery, settings.DeerCount, _rng, _board);
        _work = new FieldWork(_scenery, _rng, _board);
        _pack = new Pack(_scenery, settings.CoyoteCount, 4, _rng, _board);
        _stories = new StoryDirector(_scenery, _happenings, _rng, _board, c => [new AcornCaper(c), new HideAndSeek(c), new FawnApple(c, _herd), new MouseParade(c)]);
        _life = new PatchLife(_scenery, settings.CritterCount, _rng, _board);
        string? notice = Environment.GetEnvironmentVariable("PECKWORKS_NOTICE");      // test aid: "Loud;0.5;0.72;0.8;3" posts that kind at (xf, yf) with reach in U at that second
        if (!string.IsNullOrEmpty(notice))
        {
            string[] p = notice.Split(';');
            _testNotice = (Enum.Parse<Sense>(p[0], true), float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4]));
        }
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
    /// The shapes are smooth closed curves, not ovals and sticks (the first
    /// version read as a frying pan with a black handle): a body that is deep
    /// at the breast and tapers to a pointed tail, a long black neck held
    /// straight out, the black head with the white chinstrap, and two broad
    /// wings. A wing is one closed curve: shoulder, wrist, tip, then back
    /// along the trailing edge with four fingered primaries at the tip. It
    /// swings about the shoulder with the beat; the hand lags the upper arm a
    /// little, as a real wing bends on the downstroke, and the wing's visible
    /// width is widest at the top and bottom of the beat and narrowest when
    /// level, which is how a side view of a flapping wing foreshortens.
    /// </summary>
    private static Sprite PaintGoose(float s, float phase)
    {
        int w = (int)(s * 1.7f) + 4, h = (int)(s * 2.5f) + 4;
        float cx = w * 0.45f, cy = h * 0.5f;
        return Sprite.Paint(w, h, g =>
        {
            PointF P(float ax, float ay) => new(cx + s * ax, cy + s * ay);
            Color body = Color.FromArgb(118, 102, 84), belly = Color.FromArgb(222, 212, 194), black = Color.FromArgb(26, 22, 22);
            Color wingFar = Color.FromArgb(84, 72, 60), wingNear = Color.FromArgb(126, 110, 90), primaries = Color.FromArgb(52, 44, 38);

            float up = MathF.Sin(phase);                                   // +1 wings up, -1 wings down
            float hand = MathF.Sin(phase - 0.6f);                          // the hand lags the arm
            float rising = MathF.Max(0f, MathF.Cos(phase));                // the upstroke: the hand folds back and the fingers close

            void Wing(Color c, float sx, float sy, float scale)
            {
                // Seen from the side, a wing's CHORD (its width from leading edge to trailing edge) lies along the
                // line of flight, so the trailing edge is the leading edge pushed BACK, never up or down; what the
                // flap changes is how much of the wing's span we see: all of it with the wing raised or lowered, a
                // sliver when it is level and pointing at us. (The first version pushed the trailing edge down, so a
                // raised wing was a tall wedge: a plank.)
                // The wing rises high but does not drop far: a downstroke that hung the wing below the belly read as a leg.
                float upY = up > 0 ? up : 0.42f * up, handY = hand > 0 ? hand : 0.42f * hand;   // at 0.55 the TIP dropped 0.58 below the shoulder and hung under the belly as a lobe; at 0.32 the downstroke hid in the flank
                PointF shoulder = P(sx, sy);
                PointF wrist = P(sx - 0.06f, sy - 0.48f * upY * scale);
                PointF tip = P(sx - 0.16f - 0.12f * rising, sy - 0.48f * upY * scale - 0.58f * handY * scale * (1 - 0.25f * rising));
                PointF trailTip = new(tip.X - s * 0.13f, tip.Y + s * 0.02f * up);
                PointF trailWrist = new(wrist.X - s * 0.24f, wrist.Y + s * 0.03f * up);
                PointF root = P(sx - 0.42f, sy + 0.05f);
                // The leading edge bows forward a little between shoulder and wrist (an airfoil, not a straight stick).
                PointF lead = new((shoulder.X + wrist.X) / 2 + s * 0.04f, (shoulder.Y + wrist.Y) / 2);
                using var feather = new SolidBrush(c);
                using var path = new GraphicsPath();
                path.AddClosedCurve([shoulder, lead, wrist, tip, trailTip, trailWrist, root], 0.4f);
                g.FillPath(feather, path);
                // Five primaries fanning from the wrist past the tip, splayed a few degrees apart, lighter at the front and
                // darker at the back; on the upstroke they close together.
                float ax = tip.X - wrist.X, ay = tip.Y - wrist.Y, al = MathF.Sqrt(ax * ax + ay * ay) + 1e-5f;
                for (int i = 0; i < 5; i++)
                {
                    float k = i / 4f;
                    float splay = (k - 0.5f) * (0.30f - 0.18f * rising);              // radians between the outermost fingers
                    float ca = MathF.Cos(splay), sa = MathF.Sin(splay);
                    float dx = (ax * ca - ay * sa) / al, dy = (ax * sa + ay * ca) / al;
                    float len = al * (0.55f + 0.5f * (1 - MathF.Abs(k - 0.5f) * 1.2f)) * (1 - 0.2f * rising);
                    PointF from = new(wrist.X + (trailWrist.X - wrist.X) * k * 0.35f, wrist.Y + (trailWrist.Y - wrist.Y) * k * 0.35f);
                    PointF to = new(from.X + dx * len, from.Y + dy * len);
                    Color fc = Color.FromArgb((int)(140 - 82 * k), (int)(120 - 72 * k), (int)(98 - 58 * k));
                    using var quill = new Pen(fc, Math.Max(1f, s * (0.07f - 0.02f * k))) { EndCap = LineCap.Round, StartCap = LineCap.Round };
                    g.DrawLine(quill, from, to);
                }
            }

            Wing(wingFar, 0.14f, -0.02f, 0.92f);                            // the far wing, a little behind and smaller
            // The body: deep at the breast, tapering to the tail point.
            using (var b = new SolidBrush(body))
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([P(0.40f, -0.02f), P(0.22f, -0.15f), P(-0.10f, -0.16f), P(-0.40f, -0.10f), P(-0.72f, -0.02f),
                                     P(-0.40f, 0.08f), P(-0.08f, 0.16f), P(0.24f, 0.14f)], 0.4f);
                g.FillPath(b, path);
            }
            using (var pale = new SolidBrush(belly))
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([P(0.34f, 0.02f), P(0.10f, 0.02f), P(-0.20f, 0.00f), P(-0.40f, 0.03f), P(-0.20f, 0.12f), P(0.10f, 0.15f), P(0.30f, 0.10f)], 0.4f);
                g.FillPath(pale, path);
            }
            using (var tail = new SolidBrush(black))
                g.FillPolygon(tail, [P(-0.52f, -0.06f), P(-0.74f, -0.03f), P(-0.52f, 0.04f)]);
            using (var rump = new SolidBrush(Color.FromArgb(240, 236, 228)))
            using (var path = new GraphicsPath())
            {
                // The white above the tail: a thin crescent along the top of the tail root (a big white blob read as a second head).
                path.AddClosedCurve([P(-0.44f, -0.09f), P(-0.52f, -0.075f), P(-0.60f, -0.045f), P(-0.54f, -0.05f), P(-0.46f, -0.07f)], 0.4f);
                g.FillPath(rump, path);
            }
            // The neck: a long black shape held out in front, thick at the breast and slimmer at the head, with the head at its end.
            using (var neck = new SolidBrush(black))
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([P(0.30f, -0.10f), P(0.48f, -0.16f), P(0.62f, -0.20f), P(0.74f, -0.21f), P(0.74f, -0.14f), P(0.60f, -0.11f), P(0.46f, -0.06f), P(0.30f, 0.02f)], 0.3f);
                g.FillPath(neck, path);
            }
            using (var head = new SolidBrush(black))
                g.FillEllipse(head, P(0.64f, -0.27f).X, P(0.64f, -0.27f).Y, s * 0.24f, s * 0.18f);
            using (var chin = new SolidBrush(Color.FromArgb(244, 242, 236)))                     // the white chinstrap, under the eye round the throat
            using (var path = new GraphicsPath())
            {
                path.AddClosedCurve([P(0.70f, -0.22f), P(0.80f, -0.19f), P(0.80f, -0.12f), P(0.72f, -0.10f), P(0.66f, -0.14f)], 0.4f);
                g.FillPath(chin, path);
            }
            using (var bill = new SolidBrush(black))
                g.FillPolygon(bill, [P(0.86f, -0.22f), P(1.00f, -0.17f), P(0.86f, -0.13f)]);
            using (var eye = new SolidBrush(Color.FromArgb(150, 150, 140)))
                g.FillEllipse(eye, P(0.76f, -0.22f).X - s * 0.02f, P(0.76f, -0.22f).Y - s * 0.02f, s * 0.04f, s * 0.04f);
            Wing(wingNear, 0.10f, -0.06f, 1f);                              // the near wing, over the body
        });
    }

    /// <summary>
    /// The scarecrow's scarf at one moment of its flutter. The sprite's origin
    /// (ScarfOx, ScarfOy) is the KNOT, which the scene stamps on the
    /// scarecrow's neck.
    ///
    /// FEYNMAN VERSION: the first scarf was six hinged sticks drawn as one
    /// thick line, and every hinge swung by the same amount, so the end near
    /// the neck flapped as hard as the tip and the whole thing read as a red
    /// sausage. A strip of cloth in wind is the opposite: it is held still
    /// at the knot and whips at the free end. So the centre line is built
    /// from 28 tiny steps, and the angle of each step is a wave that travels
    /// toward the tip whose size GROWS along the strip (s to the power 1.6,
    /// where s is 0 at the knot and 1 at the tip), plus a little droop from
    /// gravity that also grows toward the tip. The strip is then FILLED as a
    /// polygon round that line (wide at the knot, narrower at the tip), not
    /// drawn as a pen stroke, and each short piece is shaded by which way the
    /// cloth is curving there: where it curls one way you see its lit face,
    /// where it curls the other way its darker underside. Those light and
    /// dark bands move along with the wave, which is what makes it read as
    /// folding cloth. A band round the neck, a short tail hanging down the
    /// chest (swinging gently, a quarter of a beat behind), a cream stripe
    /// near the end, and tassels that carry the wave one step further.
    /// </summary>
    private const float ScarfOxF = 0.14f, ScarfOyF = 0.42f, ScarfWF = 0.66f, ScarfHF = 0.72f;   // the sheet, and the knot's place on it, in scarecrow heights

    private static Sprite PaintScarf(float tall, float phase)
    {
        int w = (int)(tall * ScarfWF) + 4, h = (int)(tall * ScarfHF) + 4;
        float ox = tall * ScarfOxF + 2, oy = tall * ScarfOyF + 2;
        Color red = Color.FromArgb(188, 44, 40), lit = Color.FromArgb(214, 70, 56), dark = Color.FromArgb(136, 28, 28), cream = Color.FromArgb(232, 196, 120);
        return Sprite.Paint(w, h, g =>
        {
            // A strip of cloth: "steps" short pieces from "from", each turned by angle(s) (radians, 0 = right, down positive),
            // "len" long in all, "w0" wide at the start and "w1" at the end. Returns the centre line and paints the strip.
            PointF[] Strip(PointF from, int steps, float len, float w0, float w1, Func<float, float> angle, Func<float, Color?> tint)
            {
                var c = new PointF[steps + 1];
                c[0] = from;
                for (int i = 0; i < steps; i++)
                {
                    float a = angle((i + 0.5f) / steps);
                    c[i + 1] = new PointF(c[i].X + len / steps * MathF.Cos(a), c[i].Y + len / steps * MathF.Sin(a));
                }
                // The two edges: each centre point pushed out along its normal by half the width there.
                var left = new PointF[steps + 1]; var right = new PointF[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    PointF a = c[Math.Max(0, i - 1)], b = c[Math.Min(steps, i + 1)];
                    float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                    float hw = (w0 + (w1 - w0) * i / steps) * 0.5f;
                    left[i] = new PointF(c[i].X - dy / l * hw, c[i].Y + dx / l * hw);
                    right[i] = new PointF(c[i].X + dy / l * hw, c[i].Y - dx / l * hw);
                }
                // The whole strip first, in the base red, as ONE polygon (both edges joined end to end), so there
                // are no seams between pieces: pieces drawn one by one leave a hairline at every join that reads as stripes.
                var outline = new PointF[(steps + 1) * 2];
                for (int i = 0; i <= steps; i++) { outline[i] = left[i]; outline[2 * steps + 1 - i] = right[i]; }
                using (var b = new SolidBrush(red)) g.FillPolygon(b, outline);
                // Then the folds: runs of pieces where the cloth curls the same way, each run one polygon over the base.
                // Which way it curls is the cross product of the steps either side of the piece; a curl one way shows the
                // lit face, the other way the darker underside. The stripe near the tip is painted the same way.
                float Curl(int i)
                {
                    if (i <= 0 || i >= steps - 1) return 0f;
                    float ax = c[i + 1].X - c[i].X, ay = c[i + 1].Y - c[i].Y, bx = c[i + 2].X - c[i + 1].X, by = c[i + 2].Y - c[i + 1].Y;
                    return (ax * by - ay * bx) / (len / steps * len / steps);
                }
                Color? RunColour(int i)
                {
                    Color? t = tint((i + 0.5f) / steps);
                    if (t != null) return t;
                    // The curl averaged over three pieces, so a fold turns through the base red over a few pieces rather than at one ruled seam.
                    float curl = (Curl(i - 1) + Curl(i) + Curl(i + 1)) / 3f;
                    return curl > 0.10f ? dark : curl < -0.10f ? lit : null;
                }
                int start = 0;
                while (start < steps)
                {
                    Color? col = RunColour(start);
                    int end = start + 1;
                    while (end < steps && RunColour(end) == col) end++;
                    if (col != null)
                    {
                        var run = new PointF[(end - start + 1) * 2];
                        for (int i = start; i <= end; i++) { run[i - start] = left[i]; run[run.Length - 1 - (i - start)] = right[i]; }
                        using var b = new SolidBrush(col.Value);
                        g.FillPolygon(b, run);
                    }
                    start = end;
                }
                return c;
            }
            void Tassels(PointF[] line, float width, float length, int n)
            {
                PointF end = line[^1], prev = line[^2];
                float dx = end.X - prev.X, dy = end.Y - prev.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f;
                dx /= l; dy /= l;
                using var pen = new Pen(dark, Math.Max(1f, tall * 0.010f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                for (int i = 0; i < n; i++)
                {
                    float k = (i + 0.5f) / n - 0.5f;                     // across the end edge
                    PointF at = new(end.X - dy * k * width, end.Y + dx * k * width);
                    // Each tassel carries the wave one step past the end, and the outer ones splay a little.
                    float splay = 0.5f * k;
                    g.DrawLine(pen, at, new PointF(at.X + length * (dx * MathF.Cos(splay) - dy * MathF.Sin(splay)), at.Y + length * (dy * MathF.Cos(splay) + dx * MathF.Sin(splay))));
                }
            }

            var knot = new PointF(ox, oy);
            // The band round the neck: an ellipse under the chin, over the collar, with a paler top lip and the knot's bulge.
            using (var b = new SolidBrush(dark))
                g.FillEllipse(b, knot.X - tall * 0.045f - tall * 0.10f, knot.Y - tall * 0.03f, tall * 0.20f, tall * 0.06f);   // no wider than the head, so no end sticks out past the neck
            using (var b = new SolidBrush(lit))
                g.FillEllipse(b, knot.X - tall * 0.045f - tall * 0.09f, knot.Y - tall * 0.03f, tall * 0.18f, tall * 0.028f);

            // The short tail: hangs down the chest from the knot, swaying gently, a quarter of a beat behind the long one.
            float swayPhase = phase - MathF.PI / 2;
            PointF[] shortLine = Strip(new PointF(knot.X - tall * 0.01f, knot.Y + tall * 0.01f), 8, tall * 0.16f, tall * 0.055f, tall * 0.042f,
                s => MathF.PI / 2 - 0.10f + 0.22f * MathF.Sin(swayPhase - 2.5f * s) * s, s => null);
            Tassels(shortLine, tall * 0.042f, tall * 0.025f, 3);

            // The long tail: streams downwind (right and a touch up), the travelling wave growing toward the tip, with gravity's droop.
            float A = 1.25f, wind = -0.18f, droop = 0.30f;
            PointF[] longLine = Strip(knot, 28, tall * 0.44f, tall * 0.065f, tall * 0.046f,
                s => wind + A * MathF.Pow(s, 2.0f) * MathF.Sin(phase - MathF.Tau * 1.3f * s) + droop * s * s,
                s => s > 0.80f && s < 0.88f ? cream : null);
            Tassels(longLine, tall * 0.046f, tall * 0.03f, 5);

            // The knot itself, over both tails.
            using (var b = new SolidBrush(red))
                g.FillEllipse(b, knot.X - tall * 0.035f, knot.Y - tall * 0.03f, tall * 0.07f, tall * 0.06f);
            using (var b = new SolidBrush(lit))
                g.FillEllipse(b, knot.X - tall * 0.025f, knot.Y - tall * 0.025f, tall * 0.035f, tall * 0.025f);
        });
    }

    // ==================================================================
    //  UPDATE AND RENDER
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        // The scarf's wave: its phase is ADDED UP each frame at the moment's
        // speed (about two beats a second, swelling and easing with slow
        // gusts). The first version multiplied the clock by a changing speed,
        // which is not the same thing: the wave stalled, ran backward and
        // finally strobed after a few minutes. An added-up phase only ever
        // moves forward.
        double gust = 0.95 + 0.30 * Math.Sin(_time * 0.37) + 0.10 * Math.Sin(_time * 1.13);
        _scarfPhase = (_scarfPhase + dt * Math.PI * 2 * 2.1 * gust) % (Math.PI * 2);
        _leaves.Update(dt);
        _board.Tick(dt);
        if (_testNotice is { } tn && _time >= tn.At && _time - dt < tn.At)
            _board.Post(tn.Kind, new PointF(tn.X * _scenery.Width, tn.Y * _scenery.Height), tn.Reach * _scenery.U, 1f, this);
        _flock.Update(dt);
        _stories.Update(dt);                       // before the herd (a story may drive the fawn) and the director (it holds the ground's claims while it plays)
        _life.Lull = _stories.Playing;             // the patch's critters keep out of a story's way
        _herd.Update(dt);
        _work.Update(dt);
        _pack.Update(dt);
        _life.Update(dt);
        _happenings.Update(elapsedSeconds);
    }

    /// <summary>
    /// Tells the residents a surprise has begun: each happening that something in the field or on the ground could
    /// notice is pinned on the board as a kind, a place and a reach (a tractor is loud and carries across the whole
    /// field; a raccoon on the ground is a threat to the small things on the left stage; a crow at the scarecrow is a
    /// movement the deer glance at). The animals' own senses decide what they make of it.
    /// </summary>
    private void Tell(Happening h)
    {
        var s = _scenery;
        float u = s.U;
        switch (h.GetType().Name)
        {
            case "Fireworks": _board.Post(Sense.Loud, new PointF(s.Width * 0.5f, s.Height * 0.25f), 1.6f * u, 0.75f, h); break;
            case "Raccoon": _board.Post(Sense.Threat, s.StageLeft, 0.5f * u, 1f, h); break;
            case "BarnOwl": _board.Post(Sense.Threat, new PointF(s.BarnFoot.X, s.BarnFoot.Y - s.BarnWall.Height), 0.7f * u, 0.6f, h); break;
            case "Crow": _board.Post(Sense.Movement, s.ScarecrowHead, 0.45f * u, 0.8f, h); break;
            case "Pheasant": _board.Post(Sense.Movement, s.CornShockTop, 0.4f * u, 0.8f, h); break;
            case "Turkey": _board.Post(Sense.Movement, new PointF(s.Width * 0.5f, s.WalkY(s.Width * 0.5f)), 0.35f * u, 0.5f, h); break;
            case "Squirrel": _board.Post(Sense.Movement, s.LeftTrunk[0], 0.3f * u, 0.5f, h); break;
            case "AppleRoll": _board.Post(Sense.Food, s.StageLeft, 0.4f * u, 0.8f, h); break;
            case "AcornDrop": _board.Post(Sense.Food, new PointF(s.Hollow.At.X - 1.7f * Math.Max(9f, u * 0.030f), s.WalkY(s.Hollow.At.X)), 0.3f * u, 0.7f, h); break;
        }
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

        // 4b. The turkeys in the field, through the field stencil: behind the
        //     scarecrow, the fence and the near ground, in front of the hills
        //     and anything on the far lane (the tractor is a layer 0 happening).
        //     The tractor and its bales first of all: the lane is the field's far edge, behind every deer and turkey.
        //     Then the deer: they stand nearer the lane than the turkeys wander.
        _work.Draw(fb);
        _pack.Draw(fb);
        _herd.Draw(fb);
        _flock.Draw(fb);

        // 4c. The stars twinkle: sin() swings between -1 and 1; the arithmetic
        //     turns that into "between nothing and full", so a star fades right
        //     out and back on its own rhythm, through the sky stencil so a
        //     star behind a branch's leaf never shines through it.
        for (int i = 0; i < _starPhase.Length; i++)
        {
            float bright = 0.5f + 0.5f * (float)Math.Sin(_time * _starSpeed[i] + _starPhase[i]);
            PointF at = _scenery.Stars[i];
            _starGlow.DrawCentered(fb, at.X, at.Y, bright * bright * 0.9f, _scenery.OpenSky);
        }

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

        // 6. The scarf: the page is picked from its own clock (see Update), so
        //    the wave only ever runs forward along the cloth, one page or so a frame.
        int scarfPage = (int)(_scarfPhase / (Math.PI * 2) * ScarfPages) % ScarfPages;
        float tall = _scenery.ScarecrowHeight;
        _scarf[scarfPage].Draw(fb, (int)MathF.Round(_scenery.ScarecrowNeck.X - tall * ScarfOxF - 2), (int)MathF.Round(_scenery.ScarecrowNeck.Y - tall * ScarfOyF - 2));

        // 6a. The jack-o'-lantern's candle: a warm glow over the carved face that gutters and flares, the Halloween
        //     saver's own trick (two slow waves and a quick one added, so it never ticks like a clock).
        {
            float flick = 0.30f + 0.08f * (float)Math.Sin(_time * 7.3) + 0.06f * (float)Math.Sin(_time * 13.1 + 1.0) + 0.05f * (float)Math.Sin(_time * 2.1);
            _candle.DrawCentered(fb, _scenery.Jack.At.X, _scenery.Jack.At.Y + _scenery.Jack.R * 0.1f, flick);
        }

        // 6a2. The patch's own life: mice, birds and a squirrel among the pumpkins, each hidden by the pumpkins nearer than it.
        _life.Draw(fb);

        // 6b. The little scenes (Stories.cs): the squirrels, the mice, the dust, an apple on the
        //     near ground and the rail, in front of the fence (and behind the corn shock and the
        //     maples where the rail passes them, behind the nearer pumpkins on the ground).
        _stories.Draw(fb);

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
    /// 2.7 times a second (the first version wrote 2.9 RADIANS a second,
    /// one beat every two seconds, and the V paddled like something under
    /// water), on its own phase, so the V ripples instead of flapping in
    /// step. The beat is uneven, as a real one is: the downstroke, the
    /// power stroke with the wing spread, takes 58 percent of the cycle;
    /// the upstroke, with the hand folded back, 42. The body rides the
    /// beat: pushed up through the downstroke. Geese at the back are drawn first.
    /// </summary>
    private void DrawGeese(FrameBuffer fb, float leadX, float leadY)
    {
        for (int i = _geese - 1; i >= 0; i--)
        {
            int rank = (i + 1) / 2;                                      // how far back along its arm (0 = the leader)
            float side = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1);
            float x = leadX - _geeseDir * rank * _gs * 1.1f;
            float y = leadY + side * rank * _gs * 0.48f;
            double beat = (_time * 2.7 + _flapPhase[i] / (Math.PI * 2)) % 1.0;   // 0 to 1 through one beat
            // The pages run through the beat evenly in wing ANGLE; time is dealt unevenly: 0.58 of it to the
            // downstroke (wings from up, a quarter in, to down, three quarters in) and 0.42 to the upstroke.
            double phase = beat < 0.58 ? Math.PI / 2 + Math.PI * (beat / 0.58) : Math.PI * 1.5 + Math.PI * ((beat - 0.58) / 0.42);
            int page = (int)(phase / (Math.PI * 2) * FlapPages) % FlapPages;
            Sprite sheet = _goose[page];
            float bob = _gs * 0.04f * (float)Math.Sin(phase);           // wings up: the body hangs lowest; the downstroke lifts it
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
