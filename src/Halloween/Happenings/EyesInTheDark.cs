using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// Pairs of glowing eyes open in the dark ground at the bottom of the
/// screen, as if creatures were watching from the shadows. Each pair opens
/// its lids, glances to one side and the other, blinks, and closes again.
/// The pairs start at staggered times, so it is a few seconds of "something
/// is out there, and there is another one".
///
/// HOW ONE EYE IS DRAWN. An eye is an almond shape (two curves meeting at
/// the corners). How wide the curves bulge is how open the lid is: 8
/// stages, from shut (nothing drawn) to wide open. That is the flip-book
/// for opening, closing and blinking: to blink, jump down a few stages and
/// back up. Inside the almond sits a thin dark slit of a pupil, like a
/// cat's. There are three versions of each page with the slit shifted left,
/// centre or right, which is how the eyes "look" from side to side. Both
/// eyes of a pair always use the same page, so they look the same way.
///
/// Under each eye goes a soft glow (Sprite.Glow), because light from an eye
/// would spill a little onto the dark around it.
///
/// WHAT IS PAINTED IN THE CONSTRUCTOR: every combination of 3 colors and 3
/// sizes, each as 3 gaze directions times 8 lid stages, so that Begin can
/// choose any color and size per pair without painting anything. That is
/// 216 small pictures, each only a few dozen pixels wide.
///
/// Where the eyes go: the dark ground at the bottom, from a little below the
/// hill's top edge (so they are not on the lit rim) to near the bottom of the
/// screen. A "clearance test" keeps pairs from landing on top of each other.
/// </summary>
internal sealed class EyesInTheDark : Happening
{
    private const int Stages = 8, Gazes = 3;
    private static readonly Color[] Colors =
    [
        Color.FromArgb(255, 215, 60),    // yellow
        Color.FromArgb(110, 255, 90),    // green
        Color.FromArgb(255, 90, 40),     // orange-red
    ];
    private static readonly float[] SizeOfU = [0.006f, 0.010f, 0.014f];   // eye radius, as a fraction of U

    private readonly HalloweenScenery _s;
    private readonly int[] _radius = new int[3];                  // the three sizes in pixels (never under 2)
    private readonly Sprite[,][] _eye = new Sprite[3, 3][];       // [color, size][gaze * Stages + stage]
    private readonly Sprite[,] _glow = new Sprite[3, 3];

    // Everything one pair of eyes decides in Begin.
    private sealed class Pair
    {
        public float X, Y;               // the middle between the two eyes
        public int Color, Size;
        public float Start, Life;        // when it opens (seconds into the showing), and how long it watches
        public float Gap;                // distance between the two eyes' centers, pixels
        public float[] GazeAt = new float[3];    // times (since opening) the gaze changes ...
        public int[] GazeTo = new int[3];        // ... and what it changes to (0 left, 1 centre, 2 right)
        public float Blink1, Blink2;     // when the blinks happen (Blink2 < 0: only one blink)
        public float Flicker;            // a private offset so the glows do not all pulse together
    }
    private readonly List<Pair> _pairs = [];

    public override float Seconds => 10f;

    public EyesInTheDark(HalloweenScenery s)
    {
        _s = s;
        for (int sz = 0; sz < 3; sz++)
        {
            _radius[sz] = Math.Max(2, (int)MathF.Round(s.U * SizeOfU[sz]));
            for (int c = 0; c < 3; c++)
            {
                int r = _radius[sz];
                Color color = Colors[c];
                _eye[c, sz] = new Sprite[Gazes * Stages];
                for (int gaze = 0; gaze < Gazes; gaze++)
                    for (int stage = 0; stage < Stages; stage++)
                    {
                        int gz = gaze, st = stage;
                        _eye[c, sz][gaze * Stages + stage] = Sprite.Paint((int)(r * 2.6f) + 4, r * 2 + 4, g => PaintEye(g, r, color, gz, st));
                    }
                _glow[c, sz] = Sprite.Glow((int)(r * 3.2f), color);
            }
        }
    }

    /// <summary>
    /// One eye, one page of the flip-book. stage 0 is shut (nothing shows),
    /// stage 7 is wide open. gaze 0, 1, 2 puts the pupil left, centre, right.
    /// </summary>
    private static void PaintEye(Graphics g, int r, Color color, int gaze, int stage)
    {
        if (stage == 0) return;
        float cx = (r * 2.6f + 4) / 2f, cy = (r * 2 + 4) / 2f;
        float open = stage / (float)(Stages - 1);
        float half = r * 1.15f;                                    // half the almond's width: the corners
        float bulge = Math.Max(0.5f, r * open * 1.05f);            // how far the lids stand apart: this is the lid, opening

        using var almond = new GraphicsPath();
        almond.AddBezier(cx - half, cy, cx - half * 0.45f, cy - bulge * 1.45f, cx + half * 0.45f, cy - bulge * 1.45f, cx + half, cy);
        almond.AddBezier(cx + half, cy, cx + half * 0.45f, cy + bulge * 1.45f, cx - half * 0.45f, cy + bulge * 1.45f, cx - half, cy);
        almond.CloseFigure();

        // The eyeball: brightest in the middle (nearly white-hot), the full color at the rim.
        using (var ball = new PathGradientBrush(almond))
        {
            ball.CenterColor = Color.FromArgb(255, Mix(color, Color.White, 0.55f));
            ball.SurroundColors = [Color.FromArgb(255, color)];
            g.FillPath(ball, almond);
        }

        // The slit pupil, shifted by the gaze, clipped to the almond so a half-shut lid cuts it off.
        g.SetClip(almond, CombineMode.Intersect);
        float shift = (gaze - 1) * r * 0.5f;
        float pw = Math.Max(1.2f, r * 0.30f), ph = r * 1.9f;
        using (var pupil = new SolidBrush(Color.FromArgb(240, 14, 6, 10)))
            g.FillEllipse(pupil, cx + shift - pw / 2, cy - ph / 2, pw, ph);
        g.ResetClip();
    }

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public override void Begin(Random rng)
    {
        _pairs.Clear();
        int want = 3 + rng.Next(3);                                // 3 to 5 pairs
        float u = _s.U;
        float bottom = _s.Height - u * 0.03f;

        for (int attempt = 0; attempt < 80 && _pairs.Count < want; attempt++)
        {
            int size = rng.Next(3);
            float gap = _radius[size] * 3.4f;
            float x = _s.Width * (0.03f + 0.94f * (float)rng.NextDouble());
            float top = _s.Ground.YAt(x) + u * 0.06f;              // below the lit rim of the hill
            if (bottom - top < 2) continue;                        // no dark ground here (a tiny or odd screen): try another x
            float y = top + (bottom - top) * (float)rng.NextDouble();

            // Clearance: stay a good distance from every pair already placed,
            // measured from middle to middle, plus both pairs' own widths.
            bool clear = true;
            foreach (Pair p in _pairs)
            {
                float dx = p.X - x, dy = p.Y - y;
                float need = u * 0.07f + (p.Gap + gap) * 0.75f;
                if (dx * dx + dy * dy < need * need) { clear = false; break; }
            }
            if (!clear) continue;

            var pair = new Pair
            {
                X = x, Y = y, Size = size, Color = rng.Next(3), Gap = gap,
                Life = 5.5f + (float)rng.NextDouble(),
                Flicker = (float)rng.NextDouble() * MathF.Tau,
            };
            pair.Start = (float)rng.NextDouble() * (Seconds - 0.1f - pair.Life);   // always finished by Seconds
            // Gaze: centre, then to one side, then the other (or back to centre), then centre.
            int side = rng.Next(2) == 0 ? 0 : 2;
            pair.GazeAt = [1.5f + 0.5f * (float)rng.NextDouble(), 2.9f + 0.4f * (float)rng.NextDouble(), 4.0f + 0.3f * (float)rng.NextDouble()];
            pair.GazeTo = [side, rng.Next(3) == 0 ? 1 : 2 - side, 1];
            pair.Blink1 = 2.3f + 1.0f * (float)rng.NextDouble();
            pair.Blink2 = rng.Next(2) == 0 ? pair.Blink1 + 1.4f + 0.5f * (float)rng.NextDouble() : -1f;
            _pairs.Add(pair);
        }
        _pairs.Sort((a, b) => a.Start.CompareTo(b.Start));
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        foreach (Pair p in _pairs)
        {
            float tau = t - p.Start;                               // seconds since this pair opened
            if (tau < 0 || tau > p.Life) continue;

            // Lid: opens over 0.9 s, closes over the last 0.9 s. "stage" is the flip-book page, 0 to 7.
            float lid = Math.Min(Smooth(tau / 0.9f), Smooth((p.Life - tau) / 0.9f));
            float stage = lid * (Stages - 1);
            // A blink: a quick dip of the lids (0.3 s) down to nearly shut and back.
            foreach (float b in (float[])[p.Blink1, p.Blink2])
            {
                float k = (tau - b) / 0.3f;
                if (b >= 0 && k > 0 && k < 1) stage *= 1 - 0.9f * MathF.Sin(MathF.PI * k);
            }
            int page = Math.Clamp((int)MathF.Round(stage), 0, Stages - 1);
            if (page == 0) continue;

            int gaze = 1;
            for (int i = 0; i < 3; i++) if (tau >= p.GazeAt[i]) gaze = p.GazeTo[i];

            Sprite eye = _eye[p.Color, p.Size][gaze * Stages + page];
            Sprite glow = _glow[p.Color, p.Size];
            // The glow brightens as the lids open, with a faint slow flicker (a sine wave) so it feels alive.
            float shine = (0.35f + 0.65f * page / (Stages - 1)) * (0.92f + 0.08f * MathF.Sin(t * 3.1f + p.Flicker)) * 0.55f;
            float y = MathF.Round(p.Y);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = MathF.Round(p.X + side * p.Gap / 2);
                glow.DrawCentered(fb, x, y, shine);
                eye.DrawCentered(fb, x, y);
            }
        }
    }
}
