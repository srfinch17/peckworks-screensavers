using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Orizuru: a small flock of folded paper cranes glides across the sky in a
/// loose V, like a mobile hung over a baby's cot that someone has set free.
///
/// A crane is a handful of flat paper triangles, so we draw each one as
/// exactly that: a few straight-edged polygons, each in one of two tones of
/// the same paper (a lighter face and a darker face). Where the two tones
/// meet you see a crisp fold line, which is what makes it read as origami and
/// not as a bird.
///
/// The flapping is a flip-book of 16 drawings of the wings, from raised to
/// lowered and back. Paper does not beat like a bird, so the cycle is slow
/// (a bit over two seconds) and gentle.
///
/// The cranes are painted once per paper color, per size, and per direction
/// of travel (a mirrored crane is just a second set of drawings), so a flight
/// to the left and a flight to the right cost the same.
/// </summary>
internal sealed class PaperCranes : Happening
{
    private const int Poses = 16;                 // pages in the wing flip-book
    private const float SpeedU = 0.17f;           // how fast the flock crosses: screen heights per second
    private const float LengthU = 0.035f;         // one crane, tail tip to beak, as a fraction of U
    private static readonly float[] TierScale = [1f, 1.22f];   // the nearer cranes are a little bigger

    // Washi (Japanese paper) in soft traditional colors: sakura pink, vermilion,
    // indigo, gold, mint, white.
    private static readonly Color[] Washi =
    [
        Color.FromArgb(246, 170, 194), Color.FromArgb(228, 88, 62), Color.FromArgb(78, 98, 158),
        Color.FromArgb(234, 188, 74), Color.FromArgb(152, 210, 178), Color.FromArgb(246, 244, 238),
    ];

    private readonly Scenery _s;
    private readonly Sprite[,,,] _crane;           // [color, size, direction (0 = right), pose]
    private readonly float _seconds;

    // This showing's dice (rolled in Begin)
    private const int MaxCranes = 7;
    private int _n, _dir;
    private float _yLead;
    private readonly int[] _color = new int[MaxCranes], _tier = new int[MaxCranes];
    private readonly float[] _phase = new float[MaxCranes], _rate = new float[MaxCranes];
    private readonly float[] _bobPhase = new float[MaxCranes], _jitterX = new float[MaxCranes], _jitterY = new float[MaxCranes];

    public override float Seconds => _seconds;

    public PaperCranes(Scenery s)
    {
        _s = s;
        float u = s.U;
        // How long a showing lasts: the lead crane starts one margin off one edge
        // and must finish one margin off the other, at SpeedU.
        _seconds = (s.Width + 2 * Margin(u)) / (SpeedU * u);

        _crane = new Sprite[Washi.Length, TierScale.Length, 2, Poses];
        for (int c = 0; c < Washi.Length; c++)
            for (int z = 0; z < TierScale.Length; z++)
                for (int d = 0; d < 2; d++)
                    for (int p = 0; p < Poses; p++)
                        _crane[c, z, d, p] = PaintCrane(u, Washi[c], TierScale[z], d == 1, p);
    }

    // The flock is a V up to three rows deep, so it needs room to hide off the edge.
    private static float Margin(float u) => 0.5f * u;

    private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>One crane, one wing pose, facing right (or mirrored to face left).</summary>
    private static Sprite PaintCrane(float u, Color paper, float tier, bool mirror, int pose)
    {
        // The crane's drawing is laid out in "units" about a body long; S pixels per unit.
        // The floor keeps it a few pixels big even in the tiny preview box.
        float length = Math.Max(11f, LengthU * u) * tier;
        float S = length / 1.2f;
        int w = (int)MathF.Ceiling(1.5f * S) + 4, h = (int)MathF.Ceiling(1.3f * S) + 4;
        float oy = 0.8f * S + 2;

        Color light = Mix(paper, Color.White, 0.42f);
        Color mid = paper;
        Color dark = Mix(paper, Color.FromArgb(40, 40, 70), 0.24f);

        return Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(w / 2f, oy);
            if (mirror) g.ScaleTransform(-1, 1);
            g.ScaleTransform(S, S);

            // The wing angle for this page of the flip-book: a slow sine from
            // raised (about +0.9) to lowered (about -0.3).
            float a = 0.30f + 0.62f * MathF.Sin(MathF.Tau * pose / Poses);
            float ca = MathF.Cos(a), sa = MathF.Sin(a);

            // Fill a polygon in a flat color. Two-tone faces are a light polygon
            // with a darker triangle laid over half of it, so no hairline gap
            // shows between the two halves.
            void Poly(Color c, params PointF[] pts)
            {
                using var br = new SolidBrush(c);
                g.FillPolygon(br, pts);
            }
            PointF P(float x, float y) => new(x, y);

            // 1. The far wing, behind everything, in the shadowy tones.
            PointF farBaseL = P(-0.14f, -0.09f), farBaseR = P(0.18f, -0.09f);
            PointF farTip = P(0.10f - 0.18f * ca, -0.10f - 0.46f * sa);
            Poly(dark, farBaseL, farBaseR, farTip);
            Poly(Mix(dark, Color.Black, 0.16f), P(0.02f, -0.09f), farBaseR, farTip);

            // 2. The tail: a long thin triangle angled up and back, two tones.
            PointF tipT = P(-0.60f, -0.27f);
            Poly(light, P(-0.17f, -0.08f), P(-0.28f, 0.06f), tipT);
            Poly(dark, P(-0.225f, -0.01f), P(-0.28f, 0.06f), tipT);

            // 3. The body: a diamond, top half light, bottom half darker.
            Poly(light, P(-0.30f, 0.0f), P(0.0f, -0.13f), P(0.33f, 0.02f), P(0.02f, 0.27f));
            Poly(mid, P(-0.30f, 0.0f), P(0.33f, 0.02f), P(0.02f, 0.27f));

            // 4. The neck: long, thin, angled up and forward, with the head
            // folded down at the tip (the classic crane silhouette).
            PointF tipN = P(0.52f, -0.35f);
            Poly(light, P(0.18f, -0.09f), P(0.34f, 0.07f), tipN);
            Poly(dark, P(0.26f, -0.01f), P(0.34f, 0.07f), tipN);
            Poly(dark, P(0.47f, -0.33f), P(0.55f, -0.38f), P(0.69f, -0.22f));   // the folded-down head and beak

            // 5. The near wing, in front: bright, two tones, crisp fold down the middle.
            PointF baseL = P(-0.24f, -0.07f), baseM = P(-0.06f, -0.07f), baseR = P(0.13f, -0.07f);
            PointF tip = P(-0.12f - 0.22f * ca, -0.08f - 0.52f * sa);
            Poly(light, baseL, baseR, tip);
            Poly(mid, baseM, baseR, tip);
            using var fold = new Pen(Color.FromArgb(70, dark), 0.02f);
            g.DrawLine(fold, baseM, tip);
        });
    }

    public override void Begin(Random rng)
    {
        _n = rng.Next(5, 8);                                  // 5 to 7 cranes
        _dir = rng.Next(2);                                   // 0 = flying right, 1 = flying left
        // Cruising height: 0.10 to 0.35 of the screen, kept so the whole V fits.
        _yLead = _s.Height * (0.17f + 0.15f * rng.NextSingle());
        for (int i = 0; i < _n; i++)
        {
            _color[i] = rng.Next(Washi.Length);               // one paper color each
            _tier[i] = rng.NextSingle() < 0.4f ? 1 : 0;
            _phase[i] = rng.NextSingle();                     // where each is in its own wing cycle
            _rate[i] = 0.38f + 0.12f * rng.NextSingle();      // wing cycles per second (about 2.2 s each)
            _bobPhase[i] = rng.NextSingle() * MathF.Tau;
            _jitterX[i] = (rng.NextSingle() - 0.5f) * 0.03f;  // so the V is loose, not drilled
            _jitterY[i] = (rng.NextSingle() - 0.5f) * 0.02f;
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U, W = _s.Width;
        float fade = Fade(t, Seconds, 0.5f, 0.5f);
        float margin = Margin(u);
        // Where the lead crane is: it travels the whole width plus a margin each side.
        float travel = (W + 2 * margin) * Math.Clamp(t / Seconds, 0f, 1f);
        float leadX = _dir == 0 ? -margin + travel : W + margin - travel;
        float sign = _dir == 0 ? 1f : -1f;
        float bobMin = Math.Max(2f, u * 0.006f);              // a bob is never under a couple of pixels

        // The back of the V is drawn first so the front ones overlap it.
        for (int i = _n - 1; i >= 0; i--)
        {
            int row = (i + 1) / 2, side = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1);
            float x = leadX - sign * (row * 0.075f + _jitterX[i]) * u;
            float y = _yLead + (side * row * 0.034f + _jitterY[i]) * u
                    + MathF.Sin(t * 1.4f + _bobPhase[i]) * bobMin * 1.6f   // each bobs on its own phase
                    + MathF.Sin(t * 0.5f) * u * 0.012f;                    // the whole flock drifts gently
            int pose = (int)(((t * _rate[i] + _phase[i]) % 1f) * Poses) % Poses;
            var sp = _crane[_color[i], _tier[i], _dir, pose];
            sp.DrawCentered(fb, x, y, fade, _s.OpenSky);      // behind the mountain's crest and the branches
        }
    }
}
